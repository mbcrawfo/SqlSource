using System;
using System.Collections.Generic;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.SqlServer;

/// <summary>
/// The columns of a sidecar from the rows of <c>columns</c>: the server's answer as given, with the hidden rows
/// dropped.
/// </summary>
/// <remarks>
/// The nullability is the server's <c>is_nullable</c> and is never narrowed: the server says it through every join,
/// and the describer corrects it nowhere a test has not proved it wrong.  An origin is a column of a table or a view
/// of the session's database alone.  The sidecar's origin names no database: a table of another database, of a linked
/// server or of the system resource is not one the table match may find in the session's.  A table-valued function's
/// column has none, as a function's column has none on PostgreSQL, and the table match cannot read a function whose
/// parameters it does not have.
/// </remarks>
internal static class SqlServerColumns
{
    /// <summary>The reason of a column the server names no source for, or names one only in part.</summary>
    public const string Expression = "expression";

    /// <summary>The reason of a column of a linked server.</summary>
    public const string AnotherServer = "another server";

    /// <summary>
    /// The reason of a column of a database not the session's, or of a session whose database is unknown.
    /// </summary>
    public const string AnotherDatabase = "another database";

    /// <summary>
    /// The reason of a column whose source is neither a table nor a view, such as a table-valued function's, or
    /// whose source's type the tool could not read.
    /// </summary>
    public const string NotATableOrView = "not a table or view";

    // The source types, OBJECTPROPERTYEX's BaseType, of a table and of a view.
    private const string Table = "U";
    private const string View = "V";

    /// <summary>
    /// The visible columns, in order, each with why it has no origin; or the ordinal of the first that has no name.
    /// </summary>
    /// <remarks>No visible row is an empty list: a statement with no result set.</remarks>
    /// <param name="rows">The rows of <c>columns</c>, in the order of their ordinals.</param>
    /// <param name="database">The session's database, or null when <c>open</c> did not say it.</param>
    /// <exception cref="InvalidOperationException">A row has no type name or lacks a facet.</exception>
    public static Outcome<IReadOnlyList<BuiltColumn>, int> Build(
        IReadOnlyList<SqlServerColumnRow> rows,
        string? database
    )
    {
        var columns = new List<BuiltColumn>();
        foreach (var row in rows)
        {
            if (row.IsHidden)
            {
                continue;
            }

            if (row.Name is not { } name)
            {
                return Outcome<IReadOnlyList<BuiltColumn>, int>.Failed(row.ColumnOrdinal);
            }

            var (origin, reason) = OriginOf(row, database);
            columns.Add(
                new BuiltColumn(
                    new SidecarColumn(
                        columns.Count,
                        name,
                        SqlServerTypes.FromColumn(row),
                        row.IsNullable ?? true,
                        SidecarValues.NullableSource.Server,
                        origin,
                        row.IsIdentityColumn,
                        row.IsComputedColumn
                    ),
                    reason
                )
            );
        }

        return Outcome<IReadOnlyList<BuiltColumn>, int>.Of(columns);
    }

    /// <summary>
    /// The base column a row comes from, or null with the reason: the source's schema, table and column, when no
    /// linked server is given, the three are, the source's database is the session's, and the source is a table or a
    /// view.  The reasons are tried in that order: the server, the parts, the database, then the type.
    /// </summary>
    /// <param name="row">A visible row of <c>columns</c>.</param>
    /// <param name="database">The session's database, or null when <c>open</c> did not say it.</param>
    public static (SidecarOrigin? Origin, string? Reason) OriginOf(SqlServerColumnRow row, string? database)
    {
        if (row.SourceServer is not null)
        {
            return (null, AnotherServer);
        }

        if (row is not { SourceSchema: { } schema, SourceTable: { } table, SourceColumn: { } column })
        {
            return (null, Expression);
        }

        // Ordinally, as the server gave both: a database of a name that differs in case alone is another one.
        if (database is null || !string.Equals(row.SourceDatabase, database, StringComparison.Ordinal))
        {
            return (null, AnotherDatabase);
        }

        return row.SourceType is Table or View
            ? (new SidecarOrigin(schema, table, column), null)
            : (null, NotATableOrView);
    }
}

/// <summary>A column of a sidecar, and why it has no origin when it has none.</summary>
/// <param name="Column">The column.</param>
/// <param name="NoOriginReason">
/// <see cref="SqlServerColumns.Expression" />, <see cref="SqlServerColumns.AnotherServer" />,
/// <see cref="SqlServerColumns.AnotherDatabase" /> or <see cref="SqlServerColumns.NotATableOrView" />, or null for a
/// column with an origin.
/// </param>
internal sealed record BuiltColumn(SidecarColumn Column, string? NoOriginReason);
