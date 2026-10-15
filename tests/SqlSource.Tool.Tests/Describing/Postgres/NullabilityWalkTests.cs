using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using Xunit;
using static SqlSource.Tool.Tests.PostgresPlans;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// Steps 3 and 4 of the walk, over plans a test writes: each row of the spec's table of a column, and the plan's
// value with its reason.
public class NullabilityWalkTests
{
    // A table with a NOT NULL column and a nullable one, and a view of the same two.
    private static readonly PostgresTableRow Table = PostgresTables.TableOf(
        16700,
        "t",
        PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true),
        PostgresTables.Attribute(2, "opt", PostgresRows.Int4.Oid)
    );

    private static readonly PostgresTableRow View = Table with { Oid = 16701, Name = "v", Relkind = "v" };

    private static readonly PostgresTableRow Foreign = Table with { Oid = 16703, Name = "ft", Relkind = "f" };

    private static readonly IReadOnlyDictionary<long, PostgresTableRow> Tables = PostgresTables.Of(
        Table,
        View,
        Foreign
    );

    // A column of the table, of the view, or of an expression when the table is null.
    private sealed record Asked(PostgresTableRow? Of, int Attnum);

    private static readonly Asked NotNull = new(Table, 1);
    private static readonly Asked Optional = new(Table, 2);
    private static readonly Asked OfView = new(View, 1);
    private static readonly Asked OfForeign = new(Foreign, 1);
    private static readonly Asked Expression = new(null, 0);

    // The columns as the session builds them for a gated query, with the heuristic's answer, and their origins.
    private static (SidecarColumn[] Columns, ColumnOrigin?[] Origins) Built(params Asked[] asked)
    {
        var columns = new SidecarColumn[asked.Length];
        var origins = new ColumnOrigin?[asked.Length];
        for (var index = 0; index < asked.Length; index++)
        {
            var row = asked[index].Of is { } table
                ? PostgresTables.ColumnOf(index, $"c{index}", 23, table, asked[index].Attnum)
                : new PostgresColumnRow(index, $"c{index}", 23, 0, 0, null, null, null);
            origins[index] = ColumnOrigin.Find(row, Tables);
            columns[index] = PostgresColumns.Build(
                row,
                origins[index],
                PostgresRows.Of(PostgresRows.Int4),
                TypeSpellings.None,
                gated: true
            );
        }

        return (columns, origins);
    }

    private static WalkOutcome Run(PlanNode root, params Asked[] asked) => Run(root, false, asked);

    private static WalkOutcome Run(PlanNode root, bool oldAndNewMayBeNamed, params Asked[] asked)
    {
        var (columns, origins) = Built(asked);
        return NullabilityWalk.Run(root, columns, origins, oldAndNewMayBeNamed);
    }

    private static (bool? Nullable, string? Source)[] AnswersOf(WalkOutcome outcome) =>
        [.. outcome.Columns.Select(static walked => (walked.Column.Nullable, walked.Column.NullableSource))];

    // "a" is the side a LEFT JOIN keeps and "b" the side it null-extends.
    private static PlanNode LeftJoin(params string[] output) =>
        Join("Left", Scan("a"), Scan("b")) with
        {
            Output = output,
        };

    [Fact]
    public void Run_ColumnsOfATableThatNothingExtends_TakeTheCatalogsAnswer()
    {
        var outcome = Run(LeftJoin("a.id", "a.opt"), NotNull, Optional);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        outcome.Reason.ShouldBeNull();
        AnswersOf(outcome).ShouldBe([(false, "catalog"), (true, "catalog")]);
    }

    [Fact]
    public void Run_ColumnsOfATableThatAJoinExtends_AreNullableByTheOuterJoin() =>
        AnswersOf(Run(LeftJoin("b.id", "b.opt"), NotNull, Optional))
            .ShouldBe([(true, "outer-join"), (true, "outer-join")]);

    [Theory]
    [InlineData("a.id + 1")]
    [InlineData("(a.id)")]
    [InlineData("COALESCE(a.id, b.id)")]
    [InlineData("$1")]
    [InlineData("old.id")]
    [InlineData("id")]
    public void Run_ColumnOfATableWhoseEntryCannotBeMapped_KeepsTheHeuristic(string entry) =>
        AnswersOf(Run(LeftJoin(entry, entry), NotNull, Optional)).ShouldBe([(true, "heuristic"), (true, "heuristic")]);

    [Fact]
    public void Run_ColumnOfAView_SaysViewUnlessAJoinExtendsIt() =>
        AnswersOf(Run(LeftJoin("a.id", "b.id", "a.id + 1"), OfView, OfView, OfView))
            .ShouldBe([(null, "view"), (true, "outer-join"), (null, "view")]);

    // The walk's rule over a foreign relkind, through a plain scan: a real Foreign Scan is mapped to no instance
    // (TD-0046).
    [Fact]
    public void Run_ColumnOfAForeignTable_SaysForeignTableUnlessAJoinExtendsIt() =>
        AnswersOf(Run(LeftJoin("a.id", "b.id", "a.id + 1"), OfForeign, OfForeign, OfForeign))
            .ShouldBe([(null, "foreign-table"), (true, "outer-join"), (null, "foreign-table")]);

    // Nothing of the plan can make it non-nullable, so it is not looked up, whatever its entry is.
    [Fact]
    public void Run_ColumnWithoutAnOrigin_IsUnknownAndIsNotLookedUp()
    {
        var outcome = Run(LeftJoin("b.id", "count(*)"), Expression, Expression);

        AnswersOf(outcome).ShouldBe([(null, "no-origin"), (null, "no-origin")]);
        outcome.Columns.ShouldAllBe(static walked => !walked.Mapped && walked.Reference == null);
        outcome.Columns[0].Output.ShouldBe("b.id");
    }

    // The rule of a NOT NULL is written once, in ColumnNullability: one that is NOT VALID is nullable here too.
    [Fact]
    public void Run_NotNullThatIsNotValidated_IsNullableByTheCatalog()
    {
        var table = PostgresTables.TableOf(
            16702,
            "n",
            PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true, notNullValidated: false)
        );
        var row = PostgresTables.ColumnOf(0, "id", 23, table, 1);
        var origin = ColumnOrigin.Find(row, PostgresTables.Of(table));
        var column = PostgresColumns.Build(
            row,
            origin,
            PostgresRows.Of(PostgresRows.Int4),
            TypeSpellings.None,
            gated: true
        );

        var outcome = NullabilityWalk.Run(Scan("n", "id"), [column], [origin], oldAndNewMayBeNamed: false);

        AnswersOf(outcome).ShouldBe([(true, "catalog")]);
    }

    // And a NOT NULL column of a table that has inheritance children, whose child may have dropped it.
    [Fact]
    public void Run_NotNullColumnOfATableWithChildren_IsNullableByTheCatalog()
    {
        var parent = PostgresTables.TableOf(
            16703,
            "p",
            PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true)
        ) with
        {
            Relhassubclass = true,
        };
        var row = PostgresTables.ColumnOf(0, "id", 23, parent, 1);
        var origin = ColumnOrigin.Find(row, PostgresTables.Of(parent));
        var column = PostgresColumns.Build(
            row,
            origin,
            PostgresRows.Of(PostgresRows.Int4),
            TypeSpellings.None,
            gated: true
        );

        var outcome = NullabilityWalk.Run(Scan("p", "id"), [column], [origin], oldAndNewMayBeNamed: false);

        AnswersOf(outcome).ShouldBe([(true, "catalog")]);
    }

    // And a NOT NULL column of a partitioned table with a foreign partition, which nothing holds to it.
    [Fact]
    public void Run_NotNullColumnOfAPartitionedTableWithAForeignPartition_IsNullableByTheCatalog()
    {
        var parted = PostgresTables.TableOf(
            16704,
            "pt",
            PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true)
        ) with
        {
            Relkind = "p",
            Relhassubclass = true,
            HasForeignPartition = true,
        };
        var row = PostgresTables.ColumnOf(0, "id", 23, parted, 1);
        var origin = ColumnOrigin.Find(row, PostgresTables.Of(parted));
        var column = PostgresColumns.Build(
            row,
            origin,
            PostgresRows.Of(PostgresRows.Int4),
            TypeSpellings.None,
            gated: true
        );

        var outcome = NullabilityWalk.Run(Scan("pt", "id"), [column], [origin], oldAndNewMayBeNamed: false);

        AnswersOf(outcome).ShouldBe([(true, "catalog")]);
    }

    // And a NOT NULL column of a table with an INSTEAD rule, in the plan of the defect: the INSERT into t2 is
    // replaced by the rule's INSERT into t3, so the root ModifyTable is t3's and its Output names t3, while the row
    // description keeps t2 as the column's origin.  The walk maps t3, nothing extends it, and the answer is t2's.
    [Fact]
    public void Run_NotNullColumnOfATableWithAnInsteadRule_IsNullableByTheCatalog()
    {
        var ruled = PostgresTables.TableOf(
            16705,
            "t2",
            PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true)
        ) with
        {
            HasInsteadRule = true,
        };
        var row = PostgresTables.ColumnOf(0, "id", 23, ruled, 1);
        var origin = ColumnOrigin.Find(row, PostgresTables.Of(ruled));
        var column = PostgresColumns.Build(
            row,
            origin,
            PostgresRows.Of(PostgresRows.Int4),
            TypeSpellings.None,
            gated: true
        );
        var plan = Over("ModifyTable", Scan("x") with { NodeType = "Result", Alias = null }) with
        {
            Alias = "t3",
            Output = ["t3.id"],
        };

        var outcome = NullabilityWalk.Run(plan, [column], [origin], oldAndNewMayBeNamed: false);

        AnswersOf(outcome).ShouldBe([(true, "catalog")]);
    }

    [Theory]
    [InlineData("Subquery Scan")]
    [InlineData("CTE Scan")]
    [InlineData("Function Scan")]
    [InlineData("Values Scan")]
    [InlineData("WorkTable Scan")]
    public void Run_AliasOfANodeThatIsNoScanOfARelation_IsUnmappable(string nodeType)
    {
        var plan = Join("Inner", Scan("a"), Scan("s") with { NodeType = nodeType }) with { Output = ["s.id"] };

        var outcome = Run(plan, NotNull);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(outcome).ShouldBe([(true, "heuristic")]);
    }

    // A CTE's own plan is another plan: its alias is reached from here through the CTE Scan alone.
    [Fact]
    public void Run_AliasUnderAnInitPlan_IsNotFound()
    {
        var plan = Over("CTE Scan", Scan("u"), "InitPlan") with { Alias = "s", Output = ["u.id"] };

        AnswersOf(Run(plan, NotNull)).ShouldBe([(true, "heuristic")]);
    }

    // A single-relation query prints bare names.
    [Fact]
    public void Run_BareNameAtARootThatScansARelation_IsThatRelationsColumn() =>
        AnswersOf(Run(Scan("users", "id", "opt"), NotNull, Optional)).ShouldBe([(false, "catalog"), (true, "catalog")]);

    // So does a Result that replaced a scan the planner proved empty, beside a scan that remains: there the name
    // says nothing of which relation.
    [Fact]
    public void Run_BareNameUnderAJoin_IsUnmappable() =>
        AnswersOf(Run(LeftJoin("a.id", "opt"), NotNull, Optional)).ShouldBe([(false, "catalog"), (true, "heuristic")]);

    // A DELETE of one table prints bare names, and its ModifyTable is its target.
    [Fact]
    public void Run_BareNameAtARootModifyTable_IsTheTargetsColumn()
    {
        var plan = Over("ModifyTable", Scan("orders")) with { Alias = "orders", Output = ["id", "opt"] };

        AnswersOf(Run(plan, NotNull, Optional)).ShouldBe([(false, "catalog"), (true, "catalog")]);
    }

    // A bare name under a join at a ModifyTable may be a column of a relation the planner proved empty and replaced
    // with a Result, which the join null-extends: here "id" is the left join's inner side, NULL on every row.
    [Fact]
    public void Run_BareNameAtARootModifyTableOverAJoin_IsUnmappable()
    {
        var plan = Over(
            "ModifyTable",
            Join("Inner", Join("Left", Scan("a"), Scan("x") with { NodeType = "Result", Alias = null }), Scan("t"))
        ) with
        {
            Alias = "t",
            Output = ["id", "t.id"],
        };

        AnswersOf(Run(plan, NotNull, NotNull)).ShouldBe([(true, "heuristic"), (false, "catalog")]);
    }

    // A MERGE that inserts what the target lacks and deletes what the source lacks: the row returned is the
    // target's own, and the source's columns are NULL for a row that was deleted.
    [Fact]
    public void Run_TargetUnderAFullJoin_IsNotExtendedAndTheSourceIs()
    {
        var plan = Over("ModifyTable", Join("Full", Scan("t"), Scan("s"))) with
        {
            Alias = "t",
            Output = ["t.id", "s.id"],
        };

        AnswersOf(Run(plan, NotNull, NotNull)).ShouldBe([(false, "catalog"), (true, "outer-join")]);
    }

    // PostgreSQL 18 prints its old row as "old.id", and an inner scope may alias a table "old" as well: in a
    // statement that names OLD or NEW, only the target's alias is mapped at a ModifyTable.
    private static readonly PlanNode ModifyOverOld = Over("ModifyTable", Scan("old")) with
    {
        Alias = "ord2",
        Output = ["old.id", "ord2.id"],
    };

    [Fact]
    public void Run_AliasAtAModifyTableThatIsNotTheTargets_WhenOldOrNewMayBeNamed_KeepsTheHeuristic() =>
        AnswersOf(Run(ModifyOverOld, true, NotNull, NotNull)).ShouldBe([(true, "heuristic"), (false, "catalog")]);

    // The flag is what decides: without it the same plan maps the alias.
    [Fact]
    public void Run_AliasAtAModifyTableThatIsNotTheTargets_WhenNeitherIsNamed_TakesTheCatalogsAnswer() =>
        AnswersOf(Run(ModifyOverOld, false, NotNull, NotNull)).ShouldBe([(false, "catalog"), (false, "catalog")]);

    // The old and the new row are a RETURNING list's alone.
    [Fact]
    public void Run_RootThatIsNoModifyTable_IsNotAffectedByOldOrNew() =>
        AnswersOf(Run(LeftJoin("a.id", "b.id"), true, NotNull, NotNull))
            .ShouldBe([(false, "catalog"), (true, "outer-join")]);

    [Fact]
    public void Run_QuotedAlias_NamesTheInstanceOfThatName()
    {
        var plan = Join("Left", Scan("u"), Scan("Order")) with { Output = ["u.id", "\"Order\".id"] };

        AnswersOf(Run(plan, NotNull, NotNull)).ShouldBe([(false, "catalog"), (true, "outer-join")]);
    }

    // PostgreSQL prints the target list first and what a sort or a group added after it.
    [Fact]
    public void Run_OutputWithMoreEntriesThanColumns_UsesTheFirst()
    {
        var plan = Over("Sort", LeftJoin()) with { Output = ["a.id", "b.id", "b.opt"] };

        var outcome = Run(plan, NotNull, NotNull);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(outcome).ShouldBe([(false, "catalog"), (true, "outer-join")]);
    }

    public static TheoryData<string, string> Unusable =>
        new()
        {
            { "no-output", Text(Over("Append", Scan("a"), "Member")) },
            { "no-output", Text(LeftJoin("a.id")) },
            { "unknown-join-type", Text(Join("Sideways", Scan("a"), Scan("b")) with { Output = ["a.id", "a.opt"] }) },
            { "doubled-alias", Text(Join("Inner", Scan("a"), Scan("a")) with { Output = ["a.id", "a.opt"] }) },
            {
                "grouping-sets",
                Text(Over("Aggregate", Scan("a")) with { HasGroupingSets = true, Output = ["a.id", "a.opt"] })
            },
        };

    // The first is a UNION's Append, which has no Output; the second has fewer entries than the result has columns.
    [Theory]
    [MemberData(nameof(Unusable))]
    public void Run_PlanThatCannotBeUsed_IsUnavailableWithItsReasonAndEveryColumnKeepsTheHeuristic(
        string reason,
        string plan
    )
    {
        var outcome = Run(PlanNode.Read(plan).ShouldNotBeNull(), NotNull, OfView);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        outcome.Reason.ShouldBe(reason);
        AnswersOf(outcome).ShouldBe([(true, "heuristic"), (null, "view")]);
        outcome.Nodes.ShouldNotBeEmpty();
        outcome.Columns.ShouldAllBe(static walked => !walked.Mapped && !walked.NullExtended);
    }

    [Theory]
    [InlineData("explain-failed")]
    [InlineData("unreadable")]
    public void Unavailable_ReasonWithoutAPlan_LeavesEveryColumnAsItWasBuilt(string reason)
    {
        var (columns, _) = Built(NotNull, OfView, Expression);

        var outcome = NullabilityWalk.Unavailable(reason, columns);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        outcome.Reason.ShouldBe(reason);
        outcome.Nodes.ShouldBeEmpty();
        outcome.Columns.Select(static walked => walked.Column).ShouldBe(columns);
        outcome.Columns.ShouldAllBe(static walked => walked.Output == null && !walked.Mapped);
        AnswersOf(outcome).ShouldBe([(true, "heuristic"), (null, "view"), (null, "no-origin")]);
    }

    // What the log says of a column.
    [Fact]
    public void Run_Column_KeepsItsEntryItsReferenceAndWhatWasFound()
    {
        var outcome = Run(LeftJoin("a.id", "b.id", "a.id + 1"), NotNull, NotNull, NotNull);

        outcome
            .Columns.Select(static walked => (walked.Output, walked.Reference, walked.Mapped, walked.NullExtended))
            .ShouldBe([
                ("a.id", new OutputReference("a", "id"), true, false),
                ("b.id", new OutputReference("b", "id"), true, true),
                ("a.id + 1", null, false, false),
            ]);
        outcome.Nodes.Select(static visited => visited.Node.NodeType).ShouldBe(["Hash Join", "Seq Scan", "Seq Scan"]);
    }

    // Nothing but the two members of a column changes.
    [Fact]
    public void Run_Column_ChangesItsNullabilityAndNothingElse()
    {
        var (columns, origins) = Built(NotNull);

        var walked = NullabilityWalk
            .Run(LeftJoin("a.id"), columns, origins, oldAndNewMayBeNamed: false)
            .Columns.ShouldHaveSingleItem()
            .Column;

        walked.ShouldBe(columns[0] with { Nullable = false, NullableSource = "catalog" });
    }
}
