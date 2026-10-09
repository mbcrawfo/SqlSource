using System;
using System.Globalization;

namespace SqlSource.Snapshot;

/// <summary>
/// What <c>describe --check</c> compares, by the sidecar format design, section 3: <c>hash</c>, <c>engine</c>,
/// <c>database</c>, <c>resultKind</c>, <c>matchesTable</c>, and each parameter's and column's <c>name</c>,
/// <c>ordinal</c>, <c>type</c> and <c>nullable</c>, in that order.  The server's version, provenance, origin,
/// identity and computed are not compared: they are informational, or differ between machines by design.
/// </summary>
/// <remarks>
/// Each comparison returns a difference whose path starts at what it compares, and its caller puts its own path in
/// front.  So two entries that agree build no path.
/// </remarks>
internal static class SidecarComparer
{
    private const string Null = "null";

    private const string Nothing = "nothing";

    private const string AnObject = "an object";

    private const string AnArray = "an array";

    /// <summary>
    /// Returns the first difference between two entries, or null when they agree on everything that is compared.
    /// </summary>
    public static SidecarDifference? FindDifference(SidecarEntry committed, SidecarEntry described) =>
        Text(SidecarKeys.Hash, committed.Hash, described.Hash)
        ?? Text(SidecarKeys.Engine, committed.Engine, described.Engine)
        ?? Text(SidecarKeys.Database, committed.Database, described.Database, StringComparison.OrdinalIgnoreCase)
        ?? Text(SidecarKeys.ResultKind, ResultKind(committed.ResultKind), ResultKind(described.ResultKind))
        ?? Table(committed.MatchesTable, described.MatchesTable)
        ?? Parameters(committed.Parameters, described.Parameters)
        ?? Columns(committed.Columns, described.Columns);

    private static string ResultKind(SidecarResultKind kind) =>
        kind == SidecarResultKind.Rows ? SidecarValues.ResultKind.Rows : SidecarValues.ResultKind.None;

    private static SidecarDifference? Table(SidecarTable? committed, SidecarTable? described)
    {
        if (committed is null || described is null)
        {
            return Presence(SidecarKeys.MatchesTable, committed is not null, described is not null, AnObject, Null);
        }

        return Under(
            SidecarKeys.MatchesTable,
            Text(SidecarKeys.Schema, committed.Schema, described.Schema)
                ?? Text(SidecarKeys.Table, committed.Table, described.Table)
        );
    }

    private static SidecarDifference? Parameters(
        EquatableArray<SidecarParameter> committed,
        EquatableArray<SidecarParameter> described
    )
    {
        if (committed.Count != described.Count)
        {
            return Length(SidecarKeys.Parameters, committed.Count, described.Count);
        }

        for (var index = 0; index < committed.Count; index++)
        {
            var left = committed[index];
            var right = described[index];
            var difference =
                Text(SidecarKeys.Name, left.Name, right.Name)
                ?? Number(SidecarKeys.Ordinal, left.Ordinal, right.Ordinal)
                ?? Type(left.Type, right.Type)
                ?? Flag(SidecarKeys.Nullable, left.Nullable, right.Nullable);
            if (difference is not null)
            {
                return Under(Element(SidecarKeys.Parameters, index), difference);
            }
        }

        return null;
    }

    private static SidecarDifference? Columns(
        EquatableArray<SidecarColumn>? committed,
        EquatableArray<SidecarColumn>? described
    )
    {
        if (committed is not { } left || described is not { } right)
        {
            return Presence(SidecarKeys.Columns, committed.HasValue, described.HasValue, AnArray, Nothing);
        }

        if (left.Count != right.Count)
        {
            return Length(SidecarKeys.Columns, left.Count, right.Count);
        }

        for (var index = 0; index < left.Count; index++)
        {
            var difference =
                Text(SidecarKeys.Name, left[index].Name, right[index].Name)
                ?? Number(SidecarKeys.Ordinal, left[index].Ordinal, right[index].Ordinal)
                ?? Type(left[index].Type, right[index].Type)
                ?? Flag(SidecarKeys.Nullable, left[index].Nullable, right[index].Nullable);
            if (difference is not null)
            {
                return Under(Element(SidecarKeys.Columns, index), difference);
            }
        }

        return null;
    }

    private static SidecarDifference? Type(SidecarType? committed, SidecarType? described)
    {
        if (committed is null || described is null)
        {
            return Presence(SidecarKeys.Type, committed is not null, described is not null, AnObject, Null);
        }

        // Two entries of one engine hold types of one shape.  Two shapes can only be told apart by their names.
        var difference = (committed, described) switch
        {
            (PostgresType left, PostgresType right) => Postgres(left, right),
            (SqlServerType left, SqlServerType right) => SqlServer(left, right),
            _ => Text(SidecarKeys.Name, committed.Name, described.Name),
        };
        return Under(SidecarKeys.Type, difference);
    }

    private static SidecarDifference? Postgres(PostgresType committed, PostgresType described) =>
        Text(SidecarKeys.Name, committed.Name, described.Name)
        ?? Text(SidecarKeys.Kind, committed.Kind, described.Kind)
        ?? Text(SidecarKeys.Schema, committed.Schema, described.Schema)
        ?? Text(SidecarKeys.InternalName, committed.InternalName, described.InternalName)
        ?? Number(SidecarKeys.Length, committed.Length, described.Length)
        ?? Number(SidecarKeys.Precision, committed.Precision, described.Precision)
        ?? Number(SidecarKeys.Scale, committed.Scale, described.Scale)
        ?? Nested(SidecarKeys.Element, committed.Element, described.Element)
        ?? Nested(SidecarKeys.Base, committed.Base, described.Base)
        ?? Labels(committed.Labels, described.Labels)
        ?? Nested(SidecarKeys.Subtype, committed.Subtype, described.Subtype);

    private static SidecarDifference? Nested(string key, PostgresType? committed, PostgresType? described) =>
        committed is null || described is null
            ? Presence(key, committed is not null, described is not null, AnObject, Null)
            : Under(key, Postgres(committed, described));

    private static SidecarDifference? Labels(EquatableArray<string>? committed, EquatableArray<string>? described)
    {
        if (committed is not { } left || described is not { } right)
        {
            return Presence(SidecarKeys.Labels, committed.HasValue, described.HasValue, AnArray, Null);
        }

        if (left.Count != right.Count)
        {
            return Length(SidecarKeys.Labels, left.Count, right.Count);
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
            {
                var path = SidecarKeys.Labels + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                return new SidecarDifference(path, Json(left[index]), Json(right[index]));
            }
        }

        return null;
    }

    private static SidecarDifference? SqlServer(SqlServerType committed, SqlServerType described) =>
        Text(SidecarKeys.Name, committed.Name, described.Name)
        ?? Number(SidecarKeys.MaxLength, committed.MaxLength, described.MaxLength)
        ?? Number(SidecarKeys.Precision, committed.Precision, described.Precision)
        ?? Number(SidecarKeys.Scale, committed.Scale, described.Scale)
        ?? UserType(committed.UserType, described.UserType);

    private static SidecarDifference? UserType(SqlServerUserType? committed, SqlServerUserType? described)
    {
        if (committed is null || described is null)
        {
            return Presence(SidecarKeys.UserType, committed is not null, described is not null, AnObject, Null);
        }

        return Under(
            SidecarKeys.UserType,
            Text(SidecarKeys.Schema, committed.Schema, described.Schema)
                ?? Text(SidecarKeys.Name, committed.Name, described.Name)
                ?? Text(
                    SidecarKeys.AssemblyQualifiedName,
                    committed.AssemblyQualifiedName,
                    described.AssemblyQualifiedName
                )
        );
    }

    private static SidecarDifference? Text(
        string path,
        string? committed,
        string? described,
        StringComparison comparison = StringComparison.Ordinal
    ) =>
        string.Equals(committed, described, comparison)
            ? null
            : new SidecarDifference(path, Json(committed), Json(described));

    private static SidecarDifference? Number(string path, int? committed, int? described) =>
        committed == described ? null : new SidecarDifference(path, Json(committed), Json(described));

    private static SidecarDifference? Flag(string path, bool? committed, bool? described) =>
        committed == described ? null : new SidecarDifference(path, Json(committed), Json(described));

    // A member that one side has and the other lacks.  Null when both have it or both lack it.
    private static SidecarDifference? Presence(
        string path,
        bool committed,
        bool described,
        string present,
        string absent
    )
    {
        if (committed == described)
        {
            return null;
        }

        return committed ? new SidecarDifference(path, present, absent) : new SidecarDifference(path, absent, present);
    }

    private static SidecarDifference Length(string array, int committed, int described) =>
        new(array + ".length", Json(committed), Json(described));

    private static string Element(string array, int index) =>
        array + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";

    // Puts a path in front of a difference found under it.  The path is built only when there is one.
    private static SidecarDifference? Under(string path, SidecarDifference? difference) =>
        difference is null ? null : difference with { Path = path + "." + difference.Path };

    private static string Json(string? value) => value is null ? Null : SidecarJsonBuilder.Quote(value);

    private static string Json(int? value) =>
        value is { } number ? number.ToString(CultureInfo.InvariantCulture) : Null;

    private static string Json(bool? value) =>
        value switch
        {
            true => "true",
            false => "false",
            null => Null,
        };
}
