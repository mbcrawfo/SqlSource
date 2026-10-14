using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Whether a column can be <c>NULL</c>, as far as the catalog can answer, and where the answer came from.  The
/// describer records; the generator decides what an unknown means.
/// </summary>
/// <remarks>
/// An answer is never "not nullable" unless the catalog says <c>NOT NULL</c>, the origin is a table, and no outer
/// join can have touched the column.  A gated query makes every column of a table nullable here, and
/// <see cref="NullabilityWalk" /> then answers for each column whose relation instance the plan names.  A
/// <c>NOT NULL</c> that PostgreSQL 18 holds as not validated (<c>NOT VALID</c>) sets the catalog's flag while rows
/// that were there before may hold <c>NULL</c>, so it is nullable too.  So is a <c>NOT NULL</c> column of a table
/// that has or had inheritance children (<c>relhassubclass</c>): before PostgreSQL 18 a child may drop a
/// <c>NOT NULL</c> it inherited, and a query of the parent reads the child's rows.  The rule is the same on every
/// version, and costs the parent's columns on 18, where a child cannot drop it, and those of a table whose last
/// child was dropped, whose flag PostgreSQL clears lazily.  A partition can never drop its parent's
/// <c>NOT NULL</c>, but a foreign table may be a partition: it inherits the parent's <c>NOT NULL</c>, which nothing
/// enforces on its remote rows, and a query of the parent reads them.  So a <c>NOT NULL</c> column of a partitioned
/// table with a foreign table anywhere in its partition tree is nullable too, and one of a partitioned table without
/// one is the catalog's.  So is a <c>NOT NULL</c> column of a table or a partitioned table with a
/// <c>DO INSTEAD</c> rule on <c>INSERT</c>, <c>UPDATE</c> or <c>DELETE</c>: the statement is replaced by the rule's
/// action, usually on another relation, whose rows the <c>RETURNING</c> list gives, where the column may be nullable
/// or set to <c>NULL</c>, while the row description keeps this table as the column's origin.  The rule costs every
/// <c>NOT NULL</c> column of such a table wherever the table is a column's origin, a plain <c>SELECT</c> from it
/// included, and also when the rule returns no rows, <c>DO INSTEAD NOTHING</c>, a conditional rule or a disabled
/// one: a column of a table with any <c>INSTEAD</c> rule on those events is never "not nullable".
/// </remarks>
internal static class ColumnNullability
{
    /// <summary>The first row of the table that fits.</summary>
    /// <param name="origin">Where the column comes from, or null.</param>
    /// <param name="gated">Whether <see cref="OuterJoinGate" /> found a word in the query.</param>
    public static (bool? Nullable, string Source) Of(ColumnOrigin? origin, bool gated) =>
        origin switch
        {
            null => (null, SidecarValues.NullableSource.NoOrigin),

            // PostgreSQL records no NOT NULL for a view's column and does not enforce a foreign table's.
            { Table.Relkind: "v" or "m" or "f" } => (null, SidecarValues.NullableSource.View),
            _ when gated => (true, SidecarValues.NullableSource.Heuristic),

            // A child of an inheritance parent may have dropped the NOT NULL it inherited, nothing enforces it on the
            // remote rows of a foreign partition, and the RETURNING rows of an INSTEAD rule are its action's.
            {
                Table: { Relkind: "r", Relhassubclass: true }
                    or { Relkind: "p", HasForeignPartition: true }
                    or { Relkind: "r" or "p", HasInsteadRule: true }
            } => (true, SidecarValues.NullableSource.Catalog),
            { } found => (
                found.Attribute is not { NotNull: true, NotNullValidated: not false },
                SidecarValues.NullableSource.Catalog
            ),
        };
}
