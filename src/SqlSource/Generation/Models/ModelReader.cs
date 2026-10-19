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
/// rows is <c>SQLSRC259</c>, at the marker.  Every error of a query is reported, and a query with any has no models.
/// None depends on a setting of a type, so each is found once for the file, whatever the number of types that claim
/// it.
/// </remarks>
internal static class ModelReader
{
    private const string ParameterOwner = "parameter";

    private const string ColumnOwner = "column";

    // PostgreSQL's name for a column that has none.
    private const string NoName = "?column?";

    // Each help is given without its period.
    private const string AliasIt = "Give it an alias";

    private const string RenameIt = "Rename it";

    private const string AliasOne = "Give one of them an alias";

    private const string RenameOne = "Rename one of them";

    private const string TakenByARecord = "a record or object already has";

    /// <summary>The models of the file's queries whose types have no error, and the errors of their names.</summary>
    public static FileModels Read(TypedFile file)
    {
        var types = file.Types;
        if (file.Input.File is not { } parsed || types.Queries.Count == 0)
        {
            return new FileModels(
                types.NormalizedPath,
                file.Input.File?.FileName ?? string.Empty,
                EquatableArray<QueryModels>.Empty,
                EquatableArray<DiagnosticInfo>.Empty
            );
        }

        var entries = file.Input.Check.UsableEntries;
        var queries = ImmutableArray.CreateBuilder<QueryModels>(types.Queries.Count);
        var reading = new Reading(ImmutableArray.CreateBuilder<DiagnosticInfo>(), new StringBuilder(), []);
        var nextEntry = 0;
        var nextQuery = 0;
        foreach (var queryTypes in types.Queries)
        {
            // The types, the entries and the queries are all in the order of the file, so each search starts after
            // the last.
            var entryAt = IndexOfEntry(entries, queryTypes.Name, nextEntry);
            var queryAt = IndexOfQuery(parsed.Queries, queryTypes.Name, nextQuery);
            if (entryAt < 0 || queryAt < 0)
            {
                continue;
            }

            nextEntry = entryAt + 1;
            nextQuery = queryAt + 1;
            if (ReadQuery(entries[entryAt], parsed.Queries[queryAt], queryAt, queryTypes, reading) is { } models)
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
        if (entry.Columns is { } columns && types.Columns is { } columnTypes)
        {
            output = ReadColumns(columns, columnTypes, query, reading);
        }
        else if (query.OutputModel is { } marker)
        {
            reading.Diagnostics.Add(
                DiagnosticInfo.Create(SqlDiagnostics.OutputModelWithoutRows, marker.Location, query.Name)
            );
        }

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
                entry.MatchesTable
            );
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
                    Subject(ParameterOwner, parameter.Name)
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
                    IsNullable(column),
                    ColumnDocumentation(column, reading.Text),
                    Subject(ColumnOwner, column.Name)
                )
            );
        }

        return new EquatableArray<ModelProperty>(properties.ToImmutable());
    }

    // Whether the property name of the parameter or column at the index is usable, after its error is added when it
    // is not.  A name that is an identifier is remembered for the model, for SQLSRC255, even when it is reserved.
    private static bool IsUsable(string name, string written, int index, bool isColumn, SqlQuery query, Reading reading)
    {
        var owner = isColumn ? ColumnOwner : ParameterOwner;
        if (!PropertyNames.IsIdentifier(name))
        {
            reading.Diagnostics.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.PropertyNameNotIdentifier,
                    query.NameLocation,
                    Subject(owner, written),
                    name,
                    isColumn ? AliasIt : RenameIt
                )
            );
            return false;
        }

        for (var seen = 0; seen < reading.Seen.Count; seen++)
        {
            var (earlierName, earlierWritten, earlierIndex) = reading.Seen[seen];
            if (string.Equals(earlierName, name, StringComparison.OrdinalIgnoreCase))
            {
                reading.Diagnostics.Add(
                    DiagnosticInfo.Create(
                        SqlDiagnostics.PropertyNameRepeated,
                        query.NameLocation,
                        Subject(owner, earlierWritten) + " at position " + PositionOf(earlierIndex),
                        Subject(owner, written) + " at position " + PositionOf(index),
                        earlierName,
                        isColumn ? AliasOne : RenameOne
                    )
                );
                return false;
            }
        }

        reading.Seen.Add((name, written, index));
        if (PropertyNames.IsReserved(name))
        {
            reading.Diagnostics.Add(
                DiagnosticInfo.Create(
                    SqlDiagnostics.PropertyNameTaken,
                    query.NameLocation,
                    Subject(owner, written),
                    name,
                    TakenByARecord,
                    isColumn ? AliasIt : RenameIt
                )
            );
            return false;
        }

        return true;
    }

    // A trailing ? makes a column nullable and a trailing ! not; otherwise only a column known not to be null is not.
    private static bool IsNullable(SidecarColumn column)
    {
        var name = column.Name;
        var last = name.Length > 0 ? name[name.Length - 1] : '\0';
        return last == '?' || (last != '!' && column.Nullable != false);
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

    // "column 'id'", as a message names a parameter or column.
    private static string Subject(string owner, string name) => owner + " '" + name + "'";

    // The 1-based position of the index.
    private static string PositionOf(int index) => (index + 1).ToString(CultureInfo.InvariantCulture);

    private static int IndexOfEntry(EquatableArray<SidecarEntry> entries, string name, int from)
    {
        for (var index = from; index < entries.Count; index++)
        {
            if (string.Equals(entries[index].Name, name, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static int IndexOfQuery(EquatableArray<SqlQuery> queries, string name, int from)
    {
        for (var index = from; index < queries.Count; index++)
        {
            if (string.Equals(queries[index].Name, name, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    // Where one file's errors go, a builder its documentation is written in, and the property names a model has so
    // far, each with its parameter's or column's name as the sidecar has it and its index.
    private readonly record struct Reading(
        ImmutableArray<DiagnosticInfo>.Builder Diagnostics,
        StringBuilder Text,
        List<(string Name, string Written, int Index)> Seen
    );
}
