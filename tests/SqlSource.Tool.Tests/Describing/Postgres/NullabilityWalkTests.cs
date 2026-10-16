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

    // Two more tables like the first, for the scans "a" and "b" of PostgresPlans, which name the table of the alias.
    private static readonly PostgresTableRow TableA = Table with { Oid = 16706, Name = "a" };

    private static readonly PostgresTableRow TableB = Table with { Oid = 16707, Name = "b" };

    private static readonly IReadOnlyDictionary<long, PostgresTableRow> Tables = PostgresTables.Of(
        Table,
        View,
        Foreign,
        TableA,
        TableB
    );

    // A column of the table, of the view, or of an expression when the table is null.
    private sealed record Asked(PostgresTableRow? Of, int Attnum);

    private static readonly Asked NotNull = new(Table, 1);
    private static readonly Asked Optional = new(Table, 2);
    private static readonly Asked OfView = new(View, 1);
    private static readonly Asked OfForeign = new(Foreign, 1);
    private static readonly Asked Expression = new(null, 0);

    // The columns as the session builds them for a gated query, with the heuristic's answer, and their origins.
    private static (SidecarColumn[] Columns, ColumnOrigin?[] Origins) Built(params Asked[] asked) => Built(true, asked);

    // The columns as the session builds them, for a gated query or for one that is not.
    private static (SidecarColumn[] Columns, ColumnOrigin?[] Origins) Built(bool gated, params Asked[] asked)
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
                gated
            );
        }

        return (columns, origins);
    }

    private static WalkOutcome Run(PlanNode root, params Asked[] asked) => Run(root, false, asked);

    private static WalkOutcome Run(PlanNode root, bool oldAndNewMayBeNamed, params Asked[] asked)
    {
        var (columns, origins) = Built(asked);
        return NullabilityWalk.Run(root, columns, origins, Scope(true, oldAndNewMayBeNamed));
    }

    private static WalkScope Scope(bool gated, bool oldAndNewMayBeNamed = false, bool catalogFunctionsFirst = true) =>
        new(gated, oldAndNewMayBeNamed, catalogFunctionsFirst, Tables);

    private static WalkOutcome RunAs(WalkScope scope, PlanNode root, params Asked[] asked)
    {
        var (columns, origins) = Built(scope.Gated, asked);
        return NullabilityWalk.Run(root, columns, origins, scope);
    }

    // A query that is planned for a column with no origin, and has no word of the gate.
    private static WalkOutcome RunUngated(PlanNode root, params Asked[] asked)
    {
        var (columns, origins) = Built(false, asked);
        return NullabilityWalk.Run(root, columns, origins, Scope(false));
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

    // Under a join that null-extends, an expression is not answered: the text cannot tell one the root computes from
    // one a node under the join computed.  A column without an origin is never mapped.
    [Fact]
    public void Run_ColumnWithoutAnOrigin_IsUnknownAndIsNotLookedUp()
    {
        var outcome = Run(LeftJoin("b.id", "count(*)"), Expression, Expression);

        AnswersOf(outcome).ShouldBe([(null, "no-origin"), (null, "no-origin")]);
        outcome.Columns.ShouldAllBe(static walked => !walked.Mapped && walked.Reference == null);
        outcome.Columns[0].Output.ShouldBe("b.id");
    }

    // A query without a word of the gate is planned for its expressions alone: a column with an origin keeps the
    // catalog's answer whatever its entry, since no outer join can have touched it.
    [Fact]
    public void Run_UngatedQueryWhoseEntryCannotBeMapped_KeepsTheCatalogsAnswer()
    {
        var plan = Over("Subquery Scan", Scan("t"), "Subquery") with { Alias = "s", Output = ["s.id", "count(*)"] };

        var outcome = RunUngated(plan, NotNull, Expression);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(outcome).ShouldBe([(false, "catalog"), (false, "expression")]);
    }

    [Fact]
    public void Run_UngatedQueryWithAPlanThatCannotBeUsed_KeepsTheCatalogsAnswer()
    {
        var plan = Join("Inner", Scan("a"), Scan("a")) with { Output = ["a.id"] };

        var outcome = RunUngated(plan, NotNull);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        outcome.Reason.ShouldBe(WalkReasons.DoubledAlias);
        AnswersOf(outcome).ShouldBe([(false, "catalog")]);
    }

    // A plan whose reference names another column than the one the row description gives may be no plan of this
    // statement: a pooler can hand the explain another backend's statement of the same name (TD-0049).  Its
    // expressions are not read, and a gated query's columns with an origin are walked as they always were.
    [Theory]
    [InlineData("t.opt")]
    [InlineData("opt")]
    public void Run_ReferenceThatNamesAnotherColumnThanTheOrigin_IsWalkedAndReadsNoExpression(string entry)
    {
        var outcome = Run(Scan("t", entry, "'k'::text"), NotNull, Expression);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        outcome.Reason.ShouldBe(WalkReasons.OutputDiffers);
        AnswersOf(outcome).ShouldBe([(false, "catalog"), (null, "no-origin")]);
    }

    // A query that gives a table's columns aliases, users u(a, b), prints them under the aliases: the check takes it
    // for another statement's plan, and the join's answer stands as it always did.
    [Fact]
    public void Run_ReferenceThatNamesAnotherColumnUnderALeftJoin_KeepsTheJoinsAnswer() =>
        AnswersOf(Run(LeftJoin("a.other", "b.other"), NotNull, NotNull))
            .ShouldBe([(false, "catalog"), (true, "outer-join")]);

    [Fact]
    public void Run_UngatedQueryWhoseReferenceNamesAnotherColumn_IsUnavailableAndKeepsTheCatalogsAnswer()
    {
        var outcome = RunUngated(Scan("t", "t.opt"), NotNull);

        outcome.Reason.ShouldBe(WalkReasons.OutputDiffers);
        AnswersOf(outcome).ShouldBe([(false, "catalog")]);
    }

    // A view's column prints as the column of the table under the view, under that column's name.
    [Fact]
    public void Run_ViewsColumnPrintedUnderTheBaseTablesName_IsWalked()
    {
        var outcome = Run(Scan("t", "t.opt"), OfView);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(outcome).ShouldBe([(null, "view")]);
    }

    [Fact]
    public void Run_UnmappedReferenceAndExpression_PassTheCheck() =>
        Run(LeftJoin("x.other", "a.id + 1"), NotNull, NotNull).Plan.ShouldBe(SidecarValues.Plan.Walked);

    [Fact]
    public void Run_ExpressionWithNoJoin_IsAnsweredFromItsEntry()
    {
        var plan = Over("Sort", Scan("t")) with
        {
            Output = ["(count(*))", "'a'::text", "NULL::text", "upper((t.opt)::text)"],
        };

        var outcome = RunUngated(plan, Expression, Expression, Expression, Expression);

        AnswersOf(outcome)
            .ShouldBe([(false, "expression"), (false, "expression"), (true, "expression"), (null, "no-origin")]);
    }

    [Theory]
    [InlineData("Inner", true)]
    [InlineData("Inner", false)]
    [InlineData("Semi", true)]
    [InlineData("Right Semi", false)]
    public void Run_ExpressionUnderAJoinThatExtendsNothing_IsAnswered(string joinType, bool gated) =>
        AnswersOf(RunAs(Scope(gated), Join(joinType, Scan("a"), Scan("b")) with { Output = ["'k'::text"] }, Expression))
            .ShouldBe([(false, "expression")]);

    // A constant or a COALESCE of a subquery the planner pulled up from the side a LEFT JOIN extends prints at the root
    // as the same text, and is NULL for a row the join did not match.
    [Theory]
    [InlineData("Left", true)]
    [InlineData("Right", true)]
    [InlineData("Full", true)]
    [InlineData("Left", false)]
    public void Run_ExpressionUnderAJoinThatExtends_KeepsNoOrigin(string joinType, bool gated) =>
        AnswersOf(
                RunAs(Scope(gated), Join(joinType, Scan("a"), Scan("b")) with { Output = ["('x'::text)"] }, Expression)
            )
            .ShouldBe([(null, "no-origin")]);

    // An anti join may be a LEFT JOIN ... IS NULL, which returns its other side NULL, and a view brings one into a
    // query without a word of the gate: the words do not say what the plan holds.
    [Theory]
    [InlineData("Anti", false)]
    [InlineData("Right Anti", false)]
    [InlineData("Anti", true)]
    [InlineData("Right Anti", true)]
    public void Run_ExpressionUnderAnAntiJoin_KeepsNoOrigin(string joinType, bool gated)
    {
        var outcome = RunAs(
            Scope(gated),
            Join(joinType, Scan("a"), Scan("b")) with
            {
                Output = ["('x'::text)"],
            },
            Expression
        );

        AnswersOf(outcome).ShouldBe([(null, "no-origin")]);
    }

    public static TheoryData<string> SetOperations =>
        [
            Text(
                Over(
                    "Aggregate",
                    Over("Append", Scan("a"), "Member") with
                    {
                        Plans =
                        [
                            Scan("a") with
                            {
                                ParentRelationship = "Member",
                            },
                            Scan("b") with
                            {
                                ParentRelationship = "Member",
                            },
                        ],
                    }
                ) with
                {
                    Output = ["('a'::text)"],
                }
            ),
            Text(Over("Sort", Over("Append", Scan("a"), "Member")) with { Output = ["('a'::text)"] }),
            Text(
                Join("Inner", Scan("a"), Scan("b")) with
                {
                    NodeType = "SetOp",
                    JoinType = null,
                    Output = ["('a'::text)"],
                }
            ),
        ];

    // The root of a set operation prints its first branch, which says nothing of the others.
    [Theory]
    [MemberData(nameof(SetOperations))]
    public void Run_ExpressionOverASetOperation_KeepsNoOrigin(string plan)
    {
        var outcome = RunUngated(PlanNode.Read(plan).ShouldNotBeNull(), Expression);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(outcome).ShouldBe([(null, "no-origin")]);
    }

    [Fact]
    public void Run_FunctionWhenTheCatalogIsNotFirst_KeepsNoOrigin() =>
        AnswersOf(RunAs(Scope(false, catalogFunctionsFirst: false), Scan("t", "count(*)"), Expression))
            .ShouldBe([(null, "no-origin")]);

    [Theory]
    [InlineData("GREATEST(a.id, a.opt)", false)]
    [InlineData("GREATEST(a.opt, b.opt)", null)]
    [InlineData("GREATEST(c.id, a.opt)", null)]
    public void Run_ReferenceArgumentUnderAnInnerJoin_IsTheCatalogsAnswerOfItsInstance(string entry, bool? nullable) =>
        AnswersOf(RunUngated(Join("Inner", Scan("a"), Scan("b")) with { Output = [entry] }, Expression))
            .ShouldBe([(nullable, nullable is null ? "no-origin" : "expression")]);

    // A plan of one relation prints its columns bare.
    [Fact]
    public void Run_BareReferenceArgumentOfThePlansOneRelation_IsTheCatalogsAnswer() =>
        AnswersOf(RunUngated(Over("Sort", Scan("a")) with { Output = ["(GREATEST(id, opt))"] }, Expression))
            .ShouldBe([(false, "expression")]);

    [Fact]
    public void Run_BareReferenceArgumentUnderAJoinOfTwo_KeepsNoOrigin() =>
        AnswersOf(RunUngated(Join("Inner", Scan("a"), Scan("b")) with { Output = ["GREATEST(id, opt)"] }, Expression))
            .ShouldBe([(null, "no-origin")]);

    // PostgreSQL 18's old row is no instance: only the target's alias is a reference the walk proves.
    [Fact]
    public void Run_ReferenceToTheOldRow_IsNotProved()
    {
        var plan = Over("ModifyTable", Scan("t")) with
        {
            Alias = "t",
            RelationName = "t",
            Schema = "public",
            Output = ["COALESCE(old.id, NULL::integer)", "COALESCE(t.id, NULL::integer)"],
        };

        AnswersOf(RunAs(Scope(true, oldAndNewMayBeNamed: true), plan, Expression, Expression))
            .ShouldBe([(null, "no-origin"), (false, "expression")]);
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

        var outcome = NullabilityWalk.Run(Scan("n", "id"), [column], [origin], Scope(true));

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

        var outcome = NullabilityWalk.Run(Scan("p", "id"), [column], [origin], Scope(true));

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

        var outcome = NullabilityWalk.Run(Scan("pt", "id"), [column], [origin], Scope(true));

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

        var outcome = NullabilityWalk.Run(plan, [column], [origin], Scope(true));

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
            .Run(LeftJoin("a.id"), columns, origins, Scope(true))
            .Columns.ShouldHaveSingleItem()
            .Column;

        walked.ShouldBe(columns[0] with { Nullable = false, NullableSource = "catalog" });
    }
}
