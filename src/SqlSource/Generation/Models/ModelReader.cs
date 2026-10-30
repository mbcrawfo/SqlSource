using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using SqlSource.Diagnostics;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Types;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Models;

/// <summary>
/// Gives each query of a file whose types have no error the properties of its models: a property for each parameter
/// of its entry in its input model, and for each column in its output model, each with its name, C# type, nullability
/// and documentation.
/// </summary>
/// <remarks>
/// A property's name is the PascalCase of its parameter's or column's, and the first of these that applies is its
/// error, at the query's name: a column with no name (<c>SQLSRC253</c>), a name that is no identifier
/// (<c>SQLSRC254</c>), a name equal ignoring case to an earlier one of the model (<c>SQLSRC255</c>), and a name that
/// a record or <see cref="object" /> takes (<c>SQLSRC256</c>).  An <c>-- output-model:</c> marker on a query without
/// rows is <c>SQLSRC259</c>, and a <c>-- column:</c> marker that names no column of the query's result, or a column
/// whose name ends in <c>!</c> or <c>?</c>, is <c>SQLSRC260</c>, each at its marker, also for a query whose types have
/// an error, whose names are not read.  Every error of a query is reported, and a query with any has no models.  None
/// depends on a setting of a type, so each is found once for the file, whatever the number of types that claim it.
/// </remarks>
internal static class ModelReader
{
    // PostgreSQL's name for a column that has none.
    private const string NoName = "?column?";

    // Each help is given without its period.
    private const string AliasIt = "Give it an alias";

    private const string RenameIt = "Rename it";

    private const string AliasOne = "Give one of them an alias";

    private const string RenameOne = "Rename one of them";

    private const string TakenByARecord = "a record or object already has";

    private const string NamesNoColumn = "names no column of the query's result";

    private const string NameAColumn = "Name a column as the query returns it";

    private const string NamesAMarkedColumn = "names a column whose alias already ends in ! or ?";

    private const string RemoveTheMark = "Remove the mark from the alias, or the marker";

    /// <summary>
    /// The models of the file's queries whose types have no error, the errors of their names, and the
    /// <c>SQLSRC259</c> and <c>SQLSRC260</c> of each query with a usable entry, whatever its types.
    /// </summary>
    public static FileModels Read(TypedFile file)
    {
        var types = file.Types;
        var entries = file.Input.Check.UsableEntries;
        if (file.Input.File is not { } parsed || entries.Count == 0)
        {
            return new FileModels(
                types.NormalizedPath,
                file.Input.File?.FileName ?? string.Empty,
                EquatableArray<QueryModels>.Empty,
                EquatableArray<DiagnosticInfo>.Empty
            );
        }

        var queries = ImmutableArray.CreateBuilder<QueryModels>(types.Queries.Count);
        var seen = new Dictionary<string, (string Name, string Written, int Index)>(StringComparer.OrdinalIgnoreCase);
        var reading = new Reading(ImmutableArray.CreateBuilder<DiagnosticInfo>(), new StringBuilder(), seen);
        var nextTypes = 0;
        var nextQuery = 0;
        foreach (var entry in entries)
        {
            // The entries, the queries and the types, which are of some of the entries, are all in the order of the
            // file, so each search starts after the last.
            var queryAt = QueryNames.IndexOf(parsed.Queries, entry.Name, nextQuery);
            if (queryAt < 0)
            {
                continue;
            }

            nextQuery = queryAt + 1;
            var query = parsed.Queries[queryAt];
            if (
                nextTypes >= types.Queries.Count
                || !string.Equals(types.Queries[nextTypes].Name, entry.Name, StringComparison.Ordinal)
            )
            {
                // A query with a type error has no types, and its names are not read.
                AddOutputModelWithoutRows(entry, query, reading);
                AddUnusableColumnMarkers(entry, query, reading);
                continue;
            }

            if (ReadQuery(entry, query, queryAt, types.Queries[nextTypes++], reading) is { } models)
            {
                queries.Add(models);
            }
        }

        return new FileModels(
            types.NormalizedPath,
            parsed.FileName,
            new EquatableArray<QueryModels>(queries.ToImmutable()),
            new EquatableArray<DiagnosticInfo>(reading.Diagnostics.ToImmutable())
        );
    }

    // The query's models, or null after each of its errors is added.
    private static QueryModels? ReadQuery(
        SidecarEntry entry,
        SqlQuery query,
        int index,
        QueryTypes types,
        Reading reading
    )
    {
        // QueryTyper gives one type for each parameter and column, so the counts agree.  A mismatch is skipped rather
        // than thrown on.
        if (types.Parameters.Count != entry.Parameters.Count || types.Columns?.Count != entry.Columns?.Count)
        {
            return null;
        }

        var errors = reading.Diagnostics.Count;
        EquatableArray<ModelProperty>? input =
            entry.Parameters.Count == 0 ? null : ReadParameters(entry.Parameters, types.Parameters, query, reading);
        EquatableArray<ModelProperty>? output = null;
        EquatableArray<string>? columnNames = null;
        if (entry.Columns is { } columns && types.Columns is { } columnTypes)
        {
            output = ReadColumns(columns, columnTypes, query, reading);
            columnNames = NamesOf(columns);
        }
        else
        {
            AddOutputModelWithoutRows(entry, query, reading);
        }

        AddUnusableColumnMarkers(entry, query, reading);

        var parameterTypes = input is null ? null : TypesOf(entry.Parameters);
        return reading.Diagnostics.Count != errors
            ? null
            : new QueryModels(
                query.Name,
                index,
                query.NameLocation,
                query.Summary,
                query.InputModel,
                query.OutputModel,
                input,
                output,
                columnNames,
                entry.MatchesTable,
                parameterTypes,
                types.UnavailableLibraries,
                types.WithoutLibraryFailure
            );
    }

    // The type of each parameter, or null when one has none: QueryTyper reports a parameter without a type, so a
    // query that reaches its models has a type for each.
    private static EquatableArray<SidecarType>? TypesOf(EquatableArray<SidecarParameter> parameters)
    {
        var types = ImmutableArray.CreateBuilder<SidecarType>(parameters.Count);
        foreach (var parameter in parameters)
        {
            if (parameter.Type is not { } type)
            {
                return null;
            }

            types.Add(type);
        }

        return new EquatableArray<SidecarType>(types.MoveToImmutable());
    }

    private static EquatableArray<string> NamesOf(EquatableArray<SidecarColumn> columns)
    {
        var names = ImmutableArray.CreateBuilder<string>(columns.Count);
        foreach (var column in columns)
        {
            names.Add(column.Name);
        }

        return new EquatableArray<string>(names.MoveToImmutable());
    }

    // SQLSRC259, at the marker, when the query has an -- output-model: marker and its entry has no rows.
    private static void AddOutputModelWithoutRows(SidecarEntry entry, SqlQuery query, Reading reading)
    {
        if (entry.Columns is null && query.OutputModel is { } marker)
        {
            reading.Diagnostics.Add(
                DiagnosticInfo.Create(SqlDiagnostics.OutputModelWithoutRows, marker.Location, query.Name)
            );
        }
    }

    // SQLSRC260, at the marker, for each -- column: marker that names no column of the entry's, or that sets the
    // nullability of one whose name ends in a mark, which sets it already.  A marker that gives a type alone may name
    // a marked column.
    private static void AddUnusableColumnMarkers(SidecarEntry entry, SqlQuery query, Reading reading)
    {
        foreach (var marker in query.ColumnMarkers)
        {
            var column = entry.Columns is { } columns ? FindColumn(columns, marker) : null;
            if (column is null || (ColumnMarker.HasMark(column.Name) && marker.IsNullable is not null))
            {
                reading.Diagnostics.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.ColumnMarkerUnusable,
                        marker.Location,
                        marker.Name,
                        column is null ? NamesNoColumn : NamesAMarkedColumn,
                        column is null ? NameAColumn : RemoveTheMark
                    )
                );
            }
        }
    }

    // The first column the marker names, ignoring case, with its mark or without it, or null.
    private static SidecarColumn? FindColumn(EquatableArray<SidecarColumn> columns, ColumnMarker marker)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (marker.Names(columns[index].Name))
            {
                return columns[index];
            }
        }

        return null;
    }

    private static EquatableArray<ModelProperty> ReadParameters(
        EquatableArray<SidecarParameter> parameters,
        EquatableArray<PropertyType> types,
        SqlQuery query,
        Reading reading
    )
    {
        reading.Seen.Clear();
        var properties = ImmutableArray.CreateBuilder<ModelProperty>(parameters.Count);
        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            var name = PropertyNames.PascalCase(parameter.Name);
            if (!IsUsable(name, parameter.Name, index, isColumn: false, query, reading))
            {
                continue;
            }

            properties.Add(
                new ModelProperty(
                    name,
                    types[index],
                    parameter.Nullable == true,
                    ParameterDocumentation(parameter, reading.Text),
                    MessageSubject.Of(MessageSubject.Parameter, parameter.Name),
                    false
                )
            );
        }

        return new EquatableArray<ModelProperty>(properties.ToImmutable());
    }

    private static EquatableArray<ModelProperty> ReadColumns(
        EquatableArray<SidecarColumn> columns,
        EquatableArray<PropertyType> types,
        SqlQuery query,
        Reading reading
    )
    {
        reading.Seen.Clear();
        var properties = ImmutableArray.CreateBuilder<ModelProperty>(columns.Count);
        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            if (string.Equals(column.Name, NoName, StringComparison.Ordinal))
            {
                reading.Diagnostics.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.ColumnHasNoName,
                        query.NameLocation,
                        PositionOf(index),
                        query.Name
                    )
                );
                continue;
            }

            var name = PropertyNames.PascalCase(column.Name);
            if (!IsUsable(name, column.Name, index, isColumn: true, query, reading))
            {
                continue;
            }

            properties.Add(
                new ModelProperty(
                    name,
                    types[index],
                    IsNullable(column, query.ColumnMarkers),
                    ColumnDocumentation(column, reading.Text),
                    MessageSubject.Of(MessageSubject.Column, column.Name),
                    IsArrayType(column.Type)
                )
            );
        }

        return new EquatableArray<ModelProperty>(properties.ToImmutable());
    }

    // Whether Npgsql reads the type as an array, which is also how it reads a domain whose base type is one.
    private static bool IsArrayType(SidecarType type) =>
        type is PostgresType postgres
        && postgres.TryGetKind(out var kind)
        && (
            kind == PostgresTypeKind.Array
            || (kind == PostgresTypeKind.Domain && postgres.Base is { } baseType && IsArrayType(baseType))
        );

    // Whether the property name of the parameter or column at the index is usable, after its error is added when it
    // is not.  A name that is an identifier is remembered for the model, for SQLSRC255, even when it is reserved.
    private static bool IsUsable(string name, string written, int index, bool isColumn, SqlQuery query, Reading reading)
    {
        var owner = isColumn ? MessageSubject.Column : MessageSubject.Parameter;
        if (!PropertyNames.IsIdentifier(name))
        {
            reading.Diagnostics.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.PropertyNameNotIdentifier,
                    query.NameLocation,
                    MessageSubject.Of(owner, written),
                    name,
                    isColumn ? AliasIt : RenameIt
                )
            );
            return false;
        }

        if (reading.Seen.TryGetValue(name, out var earlier))
        {
            reading.Diagnostics.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.PropertyNameRepeated,
                    query.NameLocation,
                    MessageSubject.Of(owner, earlier.Written) + " at position " + PositionOf(earlier.Index),
                    MessageSubject.Of(owner, written) + " at position " + PositionOf(index),
                    earlier.Name,
                    isColumn ? AliasOne : RenameOne
                )
            );
            return false;
        }

        reading.Seen.Add(name, (name, written, index));
        if (PropertyNames.IsReserved(name))
        {
            reading.Diagnostics.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.PropertyNameTaken,
                    query.NameLocation,
                    MessageSubject.Of(owner, written),
                    name,
                    TakenByARecord,
                    isColumn ? AliasIt : RenameIt
                )
            );
            return false;
        }

        return true;
    }

    // A trailing ? makes a column nullable and a trailing ! not, and a -- column: marker that names the column and says
    // null or not null sets it; otherwise only a column known not to be null is not.  A column with a mark and a marker
    // that sets its nullability is SQLSRC260 and has no model.
    private static bool IsNullable(SidecarColumn column, EquatableArray<ColumnMarker> markers)
    {
        var name = column.Name;
        var last = name.Length > 0 ? name[name.Length - 1] : '\0';
        if (last is '?' or '!')
        {
            return last == '?';
        }

        foreach (var marker in markers)
        {
            if (
                marker.IsNullable is { } nullable
                && string.Equals(marker.Name, name, StringComparison.OrdinalIgnoreCase)
            )
            {
                return nullable;
            }
        }

        return column.Nullable != false;
    }

    // "The column <c>id</c>, <c>integer</c>, from <c>public.users.id</c>.  An identity column."
    private static string ColumnDocumentation(SidecarColumn column, StringBuilder text)
    {
        _ = text.Clear()
            .Append("The column <c>")
            .Append(XmlDocWriter.Escape(column.Name))
            .Append("</c>, <c>")
            .Append(XmlDocWriter.Escape(column.Type.Name))
            .Append("</c>");
        if (column.Origin is { Schema: { } schema, Table: { } table, Column: { } name })
        {
            _ = text.Append(", from <c>")
                .Append(XmlDocWriter.Escape(schema))
                .Append('.')
                .Append(XmlDocWriter.Escape(table))
                .Append('.')
                .Append(XmlDocWriter.Escape(name))
                .Append("</c>");
        }

        _ = text.Append('.');
        if (column.Identity == true)
        {
            _ = text.Append("  An identity column.");
        }

        if (column.Computed == true)
        {
            _ = text.Append("  A computed column.");
        }

        return text.ToString();
    }

    // "The parameter <c>id</c>, <c>integer</c>."  A parameter without a type has no types, so never reaches here.
    private static string ParameterDocumentation(SidecarParameter parameter, StringBuilder text) =>
        text.Clear()
            .Append("The parameter <c>")
            .Append(XmlDocWriter.Escape(parameter.Name))
            .Append("</c>, <c>")
            .Append(XmlDocWriter.Escape(parameter.Type?.Name ?? string.Empty))
            .Append("</c>.")
            .ToString();

    // The 1-based position of the index.
    private static string PositionOf(int index) => (index + 1).ToString(CultureInfo.InvariantCulture);

    // Where one file's errors go, a builder its documentation is written in, and the property names a model has so
    // far, by name ignoring case, each with its name, its parameter's or column's name as the sidecar has it and its
    // index.
    private readonly record struct Reading(
        ImmutableArray<DiagnosticInfo>.Builder Diagnostics,
        StringBuilder Text,
        Dictionary<string, (string Name, string Written, int Index)> Seen
    );
}
