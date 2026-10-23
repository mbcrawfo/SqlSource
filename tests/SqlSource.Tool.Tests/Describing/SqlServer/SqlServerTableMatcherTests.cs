using System;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// The six checks of the table match, in the format's order: the first that fails is the answer.  A table is the rows
// the server gives for SELECT * from it, as the test writes them.
public sealed class SqlServerTableMatcherTests
{
    // dbo.items: id not null, name not null, note nullable.
    private static readonly SqlServerColumnRow[] Items =
    [
        TableRow(1, "id", nullable: false),
        TableRow(2, "name", nullable: false),
        TableRow(3, "note", nullable: true),
    ];

    private static readonly SidecarColumn Expression = TestSidecars.Column(
        0,
        "n",
        TestSidecars.SqlServer(),
        true,
        SidecarValues.NullableSource.Server
    );

    private static SqlServerColumnRow TableRow(int ordinal, string name, bool? nullable, bool hidden = false) =>
        new(
            hidden,
            ordinal,
            name,
            nullable,
            "int",
            4,
            10,
            0,
            null,
            null,
            null,
            null,
            "app",
            "dbo",
            "items",
            name,
            false,
            false,
            "U"
        );

    // A column of the query as the describer builds it: the server's name and nullability, and its origin.
    private static SidecarColumn From(string column, bool nullable, string table = "items", string schema = "dbo") =>
        TestSidecars.Column(
            0,
            column,
            TestSidecars.SqlServer(),
            nullable,
            SidecarValues.NullableSource.Server,
            new SidecarOrigin(schema, table, column)
        );

    // SELECT * FROM dbo.items, as the server describes it.
    private static SidecarColumn[] Whole() =>
        [From("id", nullable: false), From("name", nullable: false), From("note", nullable: true)];

    [Fact]
    public void Match_ColumnWithoutAnOrigin_IsNoOrigin() =>
        SqlServerTableMatcher.Match([.. Whole(), Expression], null).ShouldBe((null, SidecarValues.TableMatch.NoOrigin));

    [Fact]
    public void Match_ColumnsOfTwoTables_IsSeveralTables() =>
        SqlServerTableMatcher
            .Match([.. Whole(), From("id", nullable: false, table: "other")], null)
            .ShouldBe((null, SidecarValues.TableMatch.SeveralTables));

    // A table of the same name in another schema is another table.
    [Fact]
    public void Match_ColumnsOfTwoSchemas_IsSeveralTables() =>
        SqlServerTableMatcher
            .Match([.. Whole(), From("id", nullable: false, schema: "billing")], null)
            .ShouldBe((null, SidecarValues.TableMatch.SeveralTables));

    // SELECT id, name AS label, note.
    [Fact]
    public void Match_ColumnUnderAnAlias_IsNamesDiffer() =>
        SqlServerTableMatcher
            .Match(
                [
                    From("id", nullable: false),
                    From("name", nullable: false) with
                    {
                        Name = "label",
                    },
                    From("note", true),
                ],
                Items
            )
            .ShouldBe((null, SidecarValues.TableMatch.NamesDiffer));

    // The name as the server returned it: one that differs in case alone, or has an override suffix, is another.
    [Theory]
    [InlineData("Name")]
    [InlineData("name!")]
    public void Match_NameThatDiffersInCaseOrHasASuffix_IsNamesDiffer(string name) =>
        SqlServerTableMatcher
            .Match(
                [From("id", nullable: false), From("name", nullable: false) with { Name = name }, From("note", true)],
                Items
            )
            .ShouldBe((null, SidecarValues.TableMatch.NamesDiffer));

    [Fact]
    public void Match_ColumnLeftOut_IsColumnsDiffer() =>
        SqlServerTableMatcher
            .Match([From("id", nullable: false), From("name", nullable: false)], Items)
            .ShouldBe((null, SidecarValues.TableMatch.ColumnsDiffer));

    [Fact]
    public void Match_ColumnTwice_IsColumnsDiffer() =>
        SqlServerTableMatcher
            .Match([.. Whole(), From("id", nullable: false)], Items)
            .ShouldBe((null, SidecarValues.TableMatch.ColumnsDiffer));

    // As many columns as the table has, and not its list: the lists are compared, not their lengths.
    [Fact]
    public void Match_ColumnTwiceInThePlaceOfAnother_IsColumnsDiffer() =>
        SqlServerTableMatcher
            .Match([From("id", nullable: false), From("id", nullable: false), From("note", true)], Items)
            .ShouldBe((null, SidecarValues.TableMatch.ColumnsDiffer));

    [Fact]
    public void Match_TwoColumnsSwapped_IsOrderDiffers() =>
        SqlServerTableMatcher
            .Match([From("name", nullable: false), From("id", nullable: false), From("note", true)], Items)
            .ShouldBe((null, SidecarValues.TableMatch.OrderDiffers));

    // An outer join makes a NOT NULL column nullable in the query.
    [Fact]
    public void Match_NotNullColumnNullableInTheQuery_IsNullabilityDiffers() =>
        SqlServerTableMatcher
            .Match([From("id", nullable: true), From("name", nullable: false), From("note", true)], Items)
            .ShouldBe((null, SidecarValues.TableMatch.NullabilityDiffers));

    [Fact]
    public void Match_EveryColumnOfTheTableInItsOrder_IsTheTable() =>
        SqlServerTableMatcher
            .Match(Whole(), Items)
            .ShouldBe((new SidecarTable("dbo", "items"), SidecarValues.TableMatch.Matched));

    // The server gives no is_nullable only where it cannot say, and the describer records such a column nullable.
    [Fact]
    public void Match_TableRowWithoutNullability_IsNullable() =>
        SqlServerTableMatcher
            .Match(Whole(), [Items[0], Items[1], Items[2] with { IsNullable = null }])
            .ShouldBe((new SidecarTable("dbo", "items"), SidecarValues.TableMatch.Matched));

    // The first two checks need no table: the session asks for none when either fails.
    [Fact]
    public void Match_TwoChecksFail_GivesTheFirst()
    {
        SqlServerTableMatcher
            .Match([Expression, From("id", nullable: false, table: "other"), .. Whole()], null)
            .ShouldBe((null, SidecarValues.TableMatch.NoOrigin));
        SqlServerTableMatcher
            .Match([From("id", nullable: false, table: "other") with { Name = "label" }, .. Whole()], null)
            .ShouldBe((null, SidecarValues.TableMatch.SeveralTables));
        SqlServerTableMatcher
            .Match([From("name", nullable: false) with { Name = "label" }, From("id", nullable: true)], Items)
            .ShouldBe((null, SidecarValues.TableMatch.NamesDiffer));
        SqlServerTableMatcher
            .Match([From("note", true), From("id", nullable: false)], Items)
            .ShouldBe((null, SidecarValues.TableMatch.ColumnsDiffer));
        SqlServerTableMatcher
            .Match([From("name", nullable: false), From("id", nullable: true), From("note", true)], Items)
            .ShouldBe((null, SidecarValues.TableMatch.OrderDiffers));
    }

    // A view is described as a table is, and is matched so.
    [Fact]
    public void Match_View_Matches()
    {
        SqlServerColumnRow[] view =
        [
            .. Items.Select(static row => row with { SourceTable = "user_orders", SourceType = "V" }),
        ];

        SqlServerTableMatcher
            .Match(
                [
                    From("id", nullable: false, table: "user_orders"),
                    From("name", nullable: false, table: "user_orders"),
                    From("note", true, table: "user_orders"),
                ],
                view
            )
            .ShouldBe((new SidecarTable("dbo", "user_orders"), SidecarValues.TableMatch.Matched));
    }

    // The server judges a computed column by its expression: a PERSISTED NOT NULL one may be nullable in SELECT *
    // and in the query alike, and the table is still matched.
    [Fact]
    public void Match_ComputedNullableInBoth_Matches() =>
        SqlServerTableMatcher
            .Match(
                [From("id", nullable: false), From("name", nullable: false), From("total", nullable: true)],
                [Items[0], Items[1], TableRow(3, "total", nullable: true) with { IsComputedColumn = true }]
            )
            .ShouldBe((new SidecarTable("dbo", "items"), SidecarValues.TableMatch.Matched));

    // A period column declared HIDDEN is out of SELECT *, so a query that names it is not the table's list, and one
    // that leaves it out is.
    [Fact]
    public void Match_HiddenTableRow_IsNotAColumnOfTheTable()
    {
        SqlServerColumnRow[] table = [.. Items, TableRow(4, "valid_from", nullable: false, hidden: true)];

        SqlServerTableMatcher
            .Match([.. Whole(), From("valid_from", nullable: false)], table)
            .ShouldBe((null, SidecarValues.TableMatch.ColumnsDiffer));
        SqlServerTableMatcher
            .Match(Whole(), table)
            .ShouldBe((new SidecarTable("dbo", "items"), SidecarValues.TableMatch.Matched));
    }

    // The first two checks passed, so the session had to ask for the table.
    [Fact]
    public void Match_NoTableWhereTheChecksPass_ThrowsInvalidOperationException() =>
        Should.Throw<InvalidOperationException>(() => SqlServerTableMatcher.Match(Whole(), null));

    [Fact]
    public void Match_NoColumn_ThrowsInvalidOperationException() =>
        Should.Throw<InvalidOperationException>(() => SqlServerTableMatcher.Match([], Items));

    [Fact]
    public void TableOf_ColumnsOfOneTable_IsThatTable() =>
        SqlServerTableMatcher.TableOf(Whole()).ShouldBe(new SidecarOrigin("dbo", "items", null));

    [Fact]
    public void TableOf_ColumnsOfTwoTables_IsNull() =>
        SqlServerTableMatcher.TableOf([.. Whole(), From("id", nullable: false, table: "other")]).ShouldBeNull();

    [Fact]
    public void TableOf_ColumnWithoutAnOrigin_IsNull() =>
        SqlServerTableMatcher.TableOf([.. Whole(), Expression]).ShouldBeNull();

    [Fact]
    public void TableOf_NoColumn_IsNull() => SqlServerTableMatcher.TableOf([]).ShouldBeNull();
}
