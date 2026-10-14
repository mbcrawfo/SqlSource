using System;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tests.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The six checks of the table match, in the format's order: the first that fails is the answer.
public class TableMatcherTests
{
    // Attribute 3 was dropped: the answer of "tables" leaves a dropped attribute out, so the numbers have a gap.
    private static readonly PostgresTableRow Items = PostgresTables.TableOf(
        7,
        "items",
        PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true),
        PostgresTables.Attribute(2, "name", PostgresRows.Text.Oid, notNull: true),
        PostgresTables.Attribute(4, "note", PostgresRows.Text.Oid)
    );

    private static readonly PostgresTableRow Other = PostgresTables.TableOf(
        8,
        "other",
        PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true)
    );

    // A column of an expression.
    private static readonly Cell Expression = new(
        new SidecarColumn(
            0,
            "n",
            TestSidecars.Postgres(),
            null,
            SidecarValues.NullableSource.NoOrigin,
            null,
            null,
            null
        ),
        null
    );

    // A column as the describer builds one in a query that is not gated: the attribute's name, and what the
    // catalog says of its nullability.  The match reads no ordinal and no type.
    private static Cell From(PostgresTableRow table, int attnum)
    {
        var origin = new ColumnOrigin(table, table.Columns.Single(attribute => attribute.Attnum == attnum));
        var (nullable, source) = ColumnNullability.Of(origin, gated: false);
        return new Cell(
            new SidecarColumn(
                0,
                origin.Attribute.Name,
                TestSidecars.Postgres(),
                nullable,
                source,
                new SidecarOrigin(table.Schema, table.Name, origin.Attribute.Name),
                false,
                false
            ),
            origin
        );
    }

    // The relation's list in its order: what SELECT * gives.
    private static Cell[] Whole(PostgresTableRow table) =>
        [.. table.Columns.Select(attribute => From(table, attribute.Attnum))];

    private static (SidecarTable? Table, string Result) Match(params Cell[] cells) =>
        TableMatcher.Match(
            [.. cells.Select(static cell => cell.Column)],
            [.. cells.Select(static cell => cell.Origin)]
        );

    [Fact]
    public void Match_EveryColumnOfATableInItsOrder_IsTheTable() =>
        Match(Whole(Items)).ShouldBe((new SidecarTable("public", "items"), SidecarValues.TableMatch.Matched));

    [Fact]
    public void Match_TableOfOneColumn_IsTheTable() =>
        Match(Whole(Other)).ShouldBe((new SidecarTable("public", "other"), SidecarValues.TableMatch.Matched));

    [Fact]
    public void Match_RelationOfAnotherSchema_SaysItsSchema() =>
        Match(Whole(Items with { Schema = "billing" }))
            .ShouldBe((new SidecarTable("billing", "items"), SidecarValues.TableMatch.Matched));

    [Fact]
    public void Match_ColumnWithoutAnOrigin_IsNoOrigin() =>
        Match([.. Whole(Items), Expression]).ShouldBe((null, SidecarValues.TableMatch.NoOrigin));

    [Fact]
    public void Match_ColumnsOfTwoTables_IsSeveralTables() =>
        Match([.. Whole(Items), .. Whole(Other)]).ShouldBe((null, SidecarValues.TableMatch.SeveralTables));

    // An alias, a name that differs in case alone, and an override suffix: the name is compared ordinally, as the
    // server returned it.
    [Theory]
    [InlineData("label")]
    [InlineData("Name")]
    [InlineData("name!")]
    public void Match_ColumnUnderAnotherName_IsNamesDiffer(string name) =>
        Match(From(Items, 1), From(Items, 2).Named(name), From(Items, 4))
            .ShouldBe((null, SidecarValues.TableMatch.NamesDiffer));

    [Fact]
    public void Match_ColumnLeftOut_IsColumnsDiffer() =>
        Match(From(Items, 1), From(Items, 2)).ShouldBe((null, SidecarValues.TableMatch.ColumnsDiffer));

    [Fact]
    public void Match_ColumnTwiceBesideTheWholeList_IsColumnsDiffer() =>
        Match(From(Items, 1), From(Items, 1), From(Items, 2), From(Items, 4))
            .ShouldBe((null, SidecarValues.TableMatch.ColumnsDiffer));

    // As many columns as the table has, and not its list: the lists are compared, not their lengths.
    [Fact]
    public void Match_ColumnTwiceInThePlaceOfAnother_IsColumnsDiffer() =>
        Match(From(Items, 1), From(Items, 1), From(Items, 4)).ShouldBe((null, SidecarValues.TableMatch.ColumnsDiffer));

    // SELECT a.*, b.* of a table joined to itself: one relation, every name its own, every attribute there.
    [Fact]
    public void Match_TableSelectedWholeTwice_IsColumnsDiffer() =>
        Match([.. Whole(Items), .. Whole(Items)]).ShouldBe((null, SidecarValues.TableMatch.ColumnsDiffer));

    [Fact]
    public void Match_TwoColumnsSwapped_IsOrderDiffers() =>
        Match(From(Items, 2), From(Items, 1), From(Items, 4)).ShouldBe((null, SidecarValues.TableMatch.OrderDiffers));

    // What the heuristic of a gated query does to a NOT NULL column.
    [Fact]
    public void Match_NotNullColumnRecordedNullable_IsNullabilityDiffers() =>
        Match(From(Items, 1).WithNullable(true), From(Items, 2), From(Items, 4))
            .ShouldBe((null, SidecarValues.TableMatch.NullabilityDiffers));

    // A gated query over a table of nullable columns: each is nullable by the heuristic, as the catalog says too.
    [Fact]
    public void Match_NullableColumnsRecordedNullable_IsTheTable()
    {
        var notes = PostgresTables.TableOf(
            9,
            "notes",
            PostgresTables.Attribute(1, "a", PostgresRows.Text.Oid),
            PostgresTables.Attribute(2, "b", PostgresRows.Text.Oid)
        );

        Match([.. Whole(notes).Select(static cell => cell.WithNullable(true))])
            .ShouldBe((new SidecarTable("public", "notes"), SidecarValues.TableMatch.Matched));
    }

    // PostgreSQL records no NOT NULL for a column of a view, and does not enforce a foreign table's: every column
    // is unknown, in the result and in the relation, whatever the catalog's flag says.  The recorded value is set
    // here and not taken from the rule the match asks, so the relation's side is what is held.
    [Theory]
    [InlineData("v")]
    [InlineData("m")]
    [InlineData("f")]
    public void Match_ViewMaterializedViewOrForeignTableWithEveryColumnUnknown_IsTheRelation(string relkind) =>
        Match([.. Whole(Items with { Relkind = relkind }).Select(static cell => cell.WithNullable(null))])
            .ShouldBe((new SidecarTable("public", "items"), SidecarValues.TableMatch.Matched));

    // A view's column that the walk found on the nullable side of a join is recorded nullable, which the match reads.
    [Fact]
    public void Match_ColumnOfAViewRecordedNullable_IsNullabilityDiffers()
    {
        var view = Items with { Relkind = "v" };

        Match(From(view, 1).WithNullable(true), From(view, 2), From(view, 4))
            .ShouldBe((null, SidecarValues.TableMatch.NullabilityDiffers));
    }

    // A partitioned table enforces its constraints as a table does: its columns have what the catalog says, which
    // is set here and not taken from the rule the match asks.
    [Fact]
    public void Match_PartitionedTable_IsTheTable()
    {
        var parted = Items with { Relkind = "p" };

        Match(
                From(parted, 1).WithNullable(false),
                From(parted, 2).WithNullable(false),
                From(parted, 4).WithNullable(true)
            )
            .ShouldBe((new SidecarTable("public", "items"), SidecarValues.TableMatch.Matched));
    }

    // PostgreSQL 18 sets attnotnull for a NOT NULL that is NOT VALID, and the describer records the column as
    // nullable.  The table's column is nullable by the same rule, so SELECT * still matches.
    [Fact]
    public void Match_NotNullThatIsNotValidated_MatchesAsTheNullableColumnItIs()
    {
        var loose = PostgresTables.TableOf(
            10,
            "loose",
            PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true, notNullValidated: false)
        );

        Match(Whole(loose)).ShouldBe((new SidecarTable("public", "loose"), SidecarValues.TableMatch.Matched));
        Match(From(loose, 1).WithNullable(false)).ShouldBe((null, SidecarValues.TableMatch.NullabilityDiffers));
    }

    // A table with inheritance children has nullable columns by the same rule, so SELECT * from it still matches.
    [Fact]
    public void Match_TableWithChildren_MatchesAsTheNullableColumnsItHas()
    {
        var parent = PostgresTables.TableOf(
            11,
            "parent",
            PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true)
        ) with
        {
            Relhassubclass = true,
        };

        Match(Whole(parent)).ShouldBe((new SidecarTable("public", "parent"), SidecarValues.TableMatch.Matched));
        Match(From(parent, 1).WithNullable(false)).ShouldBe((null, SidecarValues.TableMatch.NullabilityDiffers));
    }

    // So does a partitioned table with a foreign partition.
    [Fact]
    public void Match_PartitionedTableWithAForeignPartition_MatchesAsTheNullableColumnsItHas()
    {
        var parted = PostgresTables.TableOf(
            12,
            "parted",
            PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true)
        ) with
        {
            Relkind = "p",
            Relhassubclass = true,
            HasForeignPartition = true,
        };

        Match(Whole(parted)).ShouldBe((new SidecarTable("public", "parted"), SidecarValues.TableMatch.Matched));
        Match(From(parted, 1).WithNullable(false)).ShouldBe((null, SidecarValues.TableMatch.NullabilityDiffers));
    }

    // And a table with an INSTEAD rule, whose RETURNING rows come from the rule's action.
    [Fact]
    public void Match_TableWithAnInsteadRule_MatchesAsTheNullableColumnsItHas()
    {
        var redirected = PostgresTables.TableOf(
            13,
            "redirected",
            PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true)
        ) with
        {
            HasInsteadRule = true,
        };

        Match(Whole(redirected)).ShouldBe((new SidecarTable("public", "redirected"), SidecarValues.TableMatch.Matched));
        Match(From(redirected, 1).WithNullable(false)).ShouldBe((null, SidecarValues.TableMatch.NullabilityDiffers));
    }

    // Each case fails two checks, and the answer is the one that comes first in the format's order.
    [Fact]
    public void Match_TwoChecksThatFail_GivesTheFirst()
    {
        var alias = From(Items, 2).Named("label");
        var loosened = From(Items, 1).WithNullable(true);

        Match(Expression, From(Other, 1), From(Items, 1)).Result.ShouldBe(SidecarValues.TableMatch.NoOrigin);
        Match(From(Other, 1), alias).Result.ShouldBe(SidecarValues.TableMatch.SeveralTables);
        Match(From(Items, 1), alias).Result.ShouldBe(SidecarValues.TableMatch.NamesDiffer);
        Match(From(Items, 4), From(Items, 1)).Result.ShouldBe(SidecarValues.TableMatch.ColumnsDiffer);
        Match(From(Items, 2), loosened, From(Items, 4)).Result.ShouldBe(SidecarValues.TableMatch.OrderDiffers);
    }

    // A statement without rows has no match, and the session never asks for one.
    [Fact]
    public void Match_NoColumn_Throws() => Should.Throw<InvalidOperationException>(() => TableMatcher.Match([], []));

    [Fact]
    public void Match_OriginsOfAnotherCount_Throws() =>
        Should.Throw<InvalidOperationException>(() => TableMatcher.Match([From(Items, 1).Column], []));

    // A column and where it comes from, as the session holds the two side by side.
    private sealed record Cell(SidecarColumn Column, ColumnOrigin? Origin)
    {
        public Cell Named(string name) => this with { Column = Column with { Name = name } };

        public Cell WithNullable(bool? nullable) => this with { Column = Column with { Nullable = nullable } };
    }
}
