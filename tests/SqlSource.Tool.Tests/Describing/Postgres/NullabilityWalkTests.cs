using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using Xunit;
using static SqlSource.Tool.Tests.PostgresPlans;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// NullabilityWalk over plans a test writes: each column's answer, and the plan's value with its reason.
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

    // The walk's rule over a foreign relkind, through a plain scan, as through a Foreign Scan.
    [Fact]
    public void Run_ColumnOfAForeignTable_SaysForeignTableUnlessAJoinExtendsIt() =>
        AnswersOf(Run(LeftJoin("a.id", "b.id", "a.id + 1"), OfForeign, OfForeign, OfForeign))
            .ShouldBe([(null, "foreign-table"), (true, "outer-join"), (null, "foreign-table")]);

    // Under a join that null-extends, an expression is answered only where the walk finds the node that computes it:
    // here the scans print no Output, and may compute anything.  A column without an origin is never mapped.
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
    // expressions are not read, and a gated query's columns with an origin are walked.
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
    // for another statement's plan, and the join's answer stands.
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

    // And a NOT NULL column of a table with an INSTEAD rule: the INSERT into t2 is replaced by the rule's INSERT
    // into t3, so the root ModifyTable is t3's and its Output names t3, while the row description keeps t2 as the
    // column's origin.  The walk maps t3, nothing extends it, and the answer is t2's.
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
    public void Run_PlanThatCannotBeUsed_IsUnavailableWithItsReasonAndEveryColumnKeepsWhatItWasBuiltWith(
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

    private static readonly Asked OfB = new(TableB, 1);

    private static readonly Asked OfA = new(TableA, 1);

    // A Subquery Scan aliased "s" over the child plan, with its own Output.
    private static PlanNode Subquery(PlanNode child, params string[] output) =>
        Over("Subquery Scan", child, "Subquery") with
        {
            Alias = "s",
            Output = output,
        };

    // The row-number dedupe: the subquery keeps a LEFT JOIN, which the planner swapped into a Right join with b, the
    // side it extends, as its outer child.  The scan prints the columns in its own order, and they are found by their
    // origins.
    [Fact]
    public void Run_ColumnThroughASubqueryScan_IsAnsweredByItsOrigin()
    {
        var child = Over(
            "WindowAgg",
            Join("Right", Scan("b"), Over("Hash", Scan("t"), Inner)) with
            {
                Output = ["t.id", "t.opt", "b.id"],
            }
        ) with
        {
            Output = ["t.id", "t.opt", "b.id", "row_number() OVER w1"],
        };
        var plan = Over("Sort", Subquery(child, "s.y", "s.x", "s.z")) with { Output = ["s.x", "s.y", "s.z"] };

        var outcome = Run(plan, NotNull, Optional, OfB);

        AnswersOf(outcome).ShouldBe([(false, "catalog"), (true, "catalog"), (true, "outer-join")]);
        outcome.Columns.Select(static walked => walked.Mapped).ShouldBe([true, true, true]);
    }

    // SELECT s.id, s.opt FROM (SELECT t.opt AS id, t.id AS opt FROM t) s: matched by name, the columns would be read
    // crossed; by their origins they are not.
    [Fact]
    public void Run_SubqueryColumnsRenamedToEachOthersNames_AreNotReadCrossed()
    {
        var plan = Subquery(Scan("t", "t.opt", "t.id"), "s.id", "s.opt") with { Output = ["s.id", "s.opt"] };

        AnswersOf(Run(plan, Optional, NotNull)).ShouldBe([(true, "catalog"), (false, "catalog")]);
    }

    // a.id and a2.id are both a column of a: which one the subquery's column is cannot be told, and a2 is extended.
    [Fact]
    public void Run_SubqueryColumnThatTwoInstancesOfItsTableMayBe_KeepsTheHeuristic()
    {
        var child = Join("Left", Scan("a"), Scan("a2") with { RelationName = "a" }) with { Output = ["a.id", "a2.id"] };

        AnswersOf(Run(Subquery(child, "s.x") with { Output = ["s.x"] }, OfA)).ShouldBe([(true, "heuristic")]);
    }

    // When every entry the column can be is of an extended instance, the column is the join's.
    [Fact]
    public void Run_SubqueryColumnOfAnExtendedInstance_IsTheOuterJoins()
    {
        var child = Join("Left", Scan("t"), Scan("b")) with { Output = ["t.id", "b.id", "b.opt"] };

        AnswersOf(Run(Subquery(child, "s.x") with { Output = ["s.x"] }, OfB)).ShouldBe([(true, "outer-join")]);
    }

    // A scan that a join above it extends extends every column of it.
    [Fact]
    public void Run_SubqueryScanThatAJoinExtends_IsTheOuterJoins()
    {
        var plan = Join("Left", Scan("a"), Subquery(Scan("t", "t.id"), "s.id")) with { Output = ["a.id", "s.id"] };

        AnswersOf(Run(plan, OfA, NotNull)).ShouldBe([(false, "catalog"), (true, "outer-join")]);
    }

    public static TheoryData<string> UnresolvableChildren =>
        [
            // A LATERAL subquery's Output may name the query above, which is no instance of its plan.
            Text(Scan("t", "t.id", "u.id")),
            // A bare name says nothing of which instance.
            Text(Join("Inner", Scan("t"), Scan("b")) with { Output = ["t.id", "id"] }),
            // A set operation prints its first branch.
            Text(Over("Sort", Over("Append", Scan("t"), "Member")) with { Output = ["t.id"] }),
            // No entry is of the origin's table.
            Text(Scan("b", "b.id")),
            // No Output at all.
            Text(Scan("t")),
        ];

    [Theory]
    [MemberData(nameof(UnresolvableChildren))]
    public void Run_SubqueryColumnThatCannotBeResolved_KeepsTheHeuristic(string child) =>
        AnswersOf(Run(Subquery(PlanNode.Read(child).ShouldNotBeNull(), "s.x") with { Output = ["s.x"] }, NotNull))
            .ShouldBe([(true, "heuristic")]);

    // A subquery inside a subquery is resolved in turn.
    [Fact]
    public void Run_ColumnThroughNestedSubqueryScans_IsAnsweredByItsOrigin()
    {
        var inner = Over(
            "Subquery Scan",
            Join("Left", Scan("t"), Scan("b")) with
            {
                Output = ["t.id", "b.id"],
            },
            "Subquery"
        ) with
        {
            Alias = "s2",
            Output = ["s2.x", "s2.y"],
        };
        var plan = Subquery(Over("Limit", inner) with { Output = ["s2.x", "s2.y"] }, "s.x", "s.y") with
        {
            Output = ["s.x", "s.y"],
        };

        AnswersOf(Run(plan, NotNull, OfB)).ShouldBe([(false, "catalog"), (true, "outer-join")]);
    }

    // A CTE used twice: one plan, two scans, each with its own path.
    [Fact]
    public void Run_ColumnsOfACteScannedTwice_AreAnsweredForEachScan()
    {
        var cte = Scan("t", "t.id", "t.opt") with { ParentRelationship = "InitPlan", SubplanName = "CTE recent" };
        var join = Join(
            "Left",
            Scan("r") with
            {
                NodeType = "CTE Scan",
                CteName = "recent",
                RelationName = null,
            },
            Scan("n") with
            {
                NodeType = "CTE Scan",
                CteName = "recent",
                RelationName = null,
            }
        );
        var plan = Over("Sort", join) with
        {
            Plans = [cte, .. Over("Sort", join).Plans],
            Output = ["r.id", "r.opt", "n.id"],
        };

        AnswersOf(Run(plan, NotNull, Optional, NotNull))
            .ShouldBe([(false, "catalog"), (true, "catalog"), (true, "outer-join")]);
    }

    // The relations scanned here, as "partitions" answers: t_1 and t_2, scanned as p_1 and p_2, are partitions of t.
    private static readonly Dictionary<PostgresRelationName, IReadOnlyList<long>> Partitions = new()
    {
        [new PostgresRelationName("public", "t_1")] = [Table.Oid],
        [new PostgresRelationName("public", "t_2")] = [Table.Oid],
    };

    private static PlanNode PartitionsOfT() =>
        Over("Append", Scan("x")) with
        {
            Plans =
            [
                Scan("p_1", "p_1.id", "p_1.opt") with
                {
                    RelationName = "t_1",
                    ParentRelationship = "Member",
                },
                Scan("p_2", "p_2.id", "p_2.opt") with
                {
                    RelationName = "t_2",
                    ParentRelationship = "Member",
                },
            ],
        };

    private static WalkOutcome RunWithPartitions(PlanNode root, params Asked[] asked)
    {
        var (columns, origins) = Built(asked);
        return NullabilityWalk.Run(root, columns, origins, Scope(true) with { Partitions = Partitions });
    }

    [Fact]
    public void Run_ColumnOfAPartitionedTable_IsAnsweredByItsSide()
    {
        var preserved = Join("Left", PartitionsOfT(), Scan("b")) with { Output = ["p.id", "p.opt", "b.id"] };
        var extended = Join("Left", Scan("b"), PartitionsOfT()) with { Output = ["p.id", "b.id"] };

        AnswersOf(RunWithPartitions(preserved, NotNull, Optional, OfB))
            .ShouldBe([(false, "catalog"), (true, "catalog"), (true, "outer-join")]);
        AnswersOf(RunWithPartitions(extended, NotNull, OfB)).ShouldBe([(true, "outer-join"), (false, "catalog")]);
    }

    // Without the answer of "partitions" the Append is read as a set operation.
    [Fact]
    public void Run_PartitionedTableWithoutPartitions_KeepsTheHeuristic() =>
        AnswersOf(Run(Join("Left", PartitionsOfT(), Scan("b")) with { Output = ["p.id"] }, NotNull))
            .ShouldBe([(true, "heuristic")]);

    // A partitioned table is no set operation: its expressions are read, and a reference to it is the table's.
    [Fact]
    public void Run_ExpressionOverAPartitionedTable_IsAnswered()
    {
        var plan = Over("Aggregate", PartitionsOfT()) with { Output = ["count(*)", "GREATEST(p.opt, p.id)"] };

        AnswersOf(RunWithPartitions(plan, Expression, Expression))
            .ShouldBe([(false, "expression"), (false, "expression")]);
    }

    // A foreign table's scan is an instance: unknown when nothing extends it, and the join's when one does.
    [Fact]
    public void Run_ColumnOfAForeignScan_IsForeignTableOrTheOuterJoins()
    {
        var foreign = Scan("ft") with { NodeType = "Foreign Scan" };

        AnswersOf(Run(Join("Left", foreign, Scan("b")) with { Output = ["ft.id"] }, OfForeign))
            .ShouldBe([(null, "foreign-table")]);
        AnswersOf(Run(Join("Left", Scan("b"), foreign) with { Output = ["ft.id"] }, OfForeign))
            .ShouldBe([(true, "outer-join")]);
    }

    // A plan of one relation prints bare names at every node, here a Limit over a Sort over the scan.
    [Fact]
    public void Run_BareNameAtARootOverOneScan_IsThatRelationsColumn() =>
        AnswersOf(Run(Over("Limit", Over("Sort", Scan("t"))) with { Output = ["id", "opt"] }, NotNull, Optional))
            .ShouldBe([(false, "catalog"), (true, "catalog")]);

    // A value the root computes over a join that extends is answered; one a node under the extended side computes,
    // printed in parentheses at the root and as itself there, is not.
    [Fact]
    public void Run_ExpressionTheRootComputesOverAJoinThatExtends_IsAnswered()
    {
        var plan = Join(
            "Right",
            Scan("b", "b.opt", "b.id", "'x'::text"),
            Over("Hash", Scan("a", "a.id"), Inner) with
            {
                Output = ["a.id"],
            }
        ) with
        {
            Output =
            [
                "('x'::text)",
                "'k'::text",
                "COALESCE(b.opt, 'z'::text)",
                "COALESCE(b.opt, a.id)",
                "COALESCE(b.opt, b.id)",
            ],
        };

        AnswersOf(RunUngated(plan, Expression, Expression, Expression, Expression, Expression))
            .ShouldBe([
                (null, "no-origin"),
                (false, "expression"),
                (false, "expression"),
                (false, "expression"),
                (null, "no-origin"),
            ]);
    }

    // The value is followed through every node that prints it, to the node that computes it.
    [Fact]
    public void Run_ExpressionPrintedThroughSeveralNodes_IsFollowedToWhereItIsComputed()
    {
        var join = Join(
            "Right",
            Scan("b", "b.id", "'x'::text"),
            Over("Hash", Scan("a", "a.id"), Inner) with
            {
                Output = ["a.id"],
            }
        ) with
        {
            Output = ["('x'::text)", "'k'::text"],
        };
        var plan = Over("Limit", Over("Sort", join) with { Output = ["('x'::text)", "'k'::text"] }) with
        {
            Output = ["('x'::text)", "'k'::text"],
        };

        AnswersOf(RunUngated(plan, Expression, Expression)).ShouldBe([(null, "no-origin"), (false, "expression")]);
    }

    // A LEFT JOIN LATERAL count whose Subquery Scan the planner removed: the Aggregate on the extended side computes
    // it, and the root prints it in parentheses.
    [Fact]
    public void Run_CountComputedOnTheExtendedSide_KeepsNoOrigin()
    {
        var plan = Join(
            "Left",
            Scan("a", "a.id"),
            Over("Aggregate", Scan("b", "b.id")) with
            {
                Output = ["count(*)"],
            }
        ) with
        {
            Output = ["a.id", "(count(*))"],
        };

        AnswersOf(RunUngated(plan, OfA, Expression)).ShouldBe([(false, "catalog"), (null, "no-origin")]);
    }

    // A node that prints no Output may compute any value, so it must be preserved.
    [Fact]
    public void Run_ExpressionBesideAChildWithoutOutputThatIsExtended_KeepsNoOrigin() =>
        AnswersOf(RunUngated(Join("Left", Scan("a", "a.id"), Scan("b")) with { Output = ["'k'::text"] }, Expression))
            .ShouldBe([(null, "no-origin")]);

    // A constant the root computes over an anti join is never NULL, whatever the anti join returns.
    [Fact]
    public void Run_ExpressionTheRootComputesOverAnAntiJoin_IsAnswered() =>
        AnswersOf(
                RunAs(
                    Scope(true),
                    Join("Anti", Scan("a", "a.id"), Scan("b", "b.id")) with
                    {
                        Output = ["'k'::text", "(b.id IS NULL)"],
                    },
                    Expression,
                    Expression
                )
            )
            .ShouldBe([(false, "expression"), (false, "expression")]);

    // The root computes the CASE, but takes ('x'::text) from the anti join's inner side, where a pulled-up constant
    // is computed and which the anti join returns NULL: every value an expression takes from below is followed too.
    [Fact]
    public void Run_ExpressionTheRootComputesFromAValueOfTheExtendedSide_KeepsNoOrigin()
    {
        var hash = Over("Hash", Scan("b", "b.user_id", "'x'::text"), Inner) with
        {
            Output = ["b.user_id", "('x'::text)"],
        };
        var plan = Join("Anti", Scan("a", "a.id"), hash) with
        {
            Output = ["CASE WHEN (a.id > 0) THEN ('x'::text) ELSE 'y'::text END", "COALESCE(('x'::text))", "'k'::text"],
        };

        AnswersOf(RunUngated(plan, Expression, Expression, Expression))
            .ShouldBe([(null, "no-origin"), (null, "no-origin"), (false, "expression")]);
    }

    // The join computes the COALESCE from a sum of the side it extends, which it prints in parentheses: the sum may be
    // NULL, and the constant still proves the COALESCE; a CASE whose one result is the sum proves nothing.
    [Fact]
    public void Run_ExpressionWithAConstantBesideAValueOfTheExtendedSide_IsAnsweredByTheConstant()
    {
        var aggregate = Over("HashAggregate", Scan("b", "b.id", "b.opt")) with { Output = ["b.id", "sum(b.opt)"] };
        var plan = Join("Right", aggregate, Over("Hash", Scan("a", "a.id"), Inner) with { Output = ["a.id"] }) with
        {
            Output =
            [
                "COALESCE((sum(b.opt)), '0'::numeric)",
                "CASE WHEN (a.id > 0) THEN (sum(b.opt)) ELSE '0'::numeric END",
            ],
        };

        AnswersOf(RunUngated(plan, Expression, Expression)).ShouldBe([(false, "expression"), (null, "no-origin")]);
    }

    // A LATERAL reference to the query above, or a placeholder over a column, prints as the column in parentheses: the
    // subquery's column may be that entry, which is no reference, and another entry of its table and column decides
    // nothing.
    [Theory]
    [InlineData("(t.id)")]
    [InlineData("((t.id))")]
    [InlineData("upper((t.id)::text)")]
    [InlineData("$1")]
    public void Run_SubqueryColumnThatAnExpressionMayPrint_KeepsTheHeuristic(string entry) =>
        AnswersOf(Run(Subquery(Scan("t", entry, "t.id"), "s.x") with { Output = ["s.x"] }, NotNull))
            .ShouldBe([(true, "heuristic")]);

    // An expression that does not name the column is never it.
    [Fact]
    public void Run_SubqueryColumnBesideAnExpressionOfAnotherName_IsAnswered() =>
        AnswersOf(
                Run(
                    Subquery(Scan("t", "row_number() OVER w1", "(t.opt)", "t.id"), "s.x") with
                    {
                        Output = ["s.x"],
                    },
                    NotNull
                )
            )
            .ShouldBe([(false, "catalog")]);

    // A relation the planner proved empty is a Result under the outer join, and its columns print bare though the
    // plan has another relation: a bare name in an expression is no column of that one.
    [Fact]
    public void Run_BareNameInAnExpressionOfAPlanOfSeveralRelations_IsUnproved()
    {
        var empty = Scan("x") with { NodeType = "Result", Alias = null, RelationName = null, Output = ["id", "opt"] };
        var plan = Join("Left", Scan("t", "t.id"), empty) with
        {
            Output = ["t.id", "COALESCE(id, id)", "CASE WHEN (t.id > 0) THEN id ELSE 0 END"],
        };

        AnswersOf(RunUngated(plan, NotNull, Expression, Expression))
            .ShouldBe([(false, "catalog"), (null, "no-origin"), (null, "no-origin")]);
    }

    // The set operations: every column is of no origin, and its type is the row description's.  "u" and "o" are two
    // tables whose columns are of the types the type rule reads; "u_1" and "u_2" are partitions of a table like "u".
    private const long Int4 = 23;
    private const long Int8 = 20;
    private const long Text = 25;
    private const long Domain = 16800;

    private static readonly PostgresTableRow SetU = PostgresTables.TableOf(
        16810,
        "u",
        PostgresTables.Attribute(1, "id", Int4, notNull: true),
        PostgresTables.Attribute(2, "name", Text, notNull: true),
        PostgresTables.Attribute(3, "email", Text),
        PostgresTables.Attribute(4, "dom", Domain, notNull: true)
    );

    private static readonly PostgresTableRow SetO = PostgresTables.TableOf(
        16811,
        "o",
        PostgresTables.Attribute(1, "id", Int8, notNull: true),
        PostgresTables.Attribute(2, "note", Text),
        PostgresTables.Attribute(3, "user_id", Int4, notNull: true),
        PostgresTables.Attribute(4, "label", Text, notNull: true),
        PostgresTables.Attribute(5, "ref", Int4)
    );

    private static readonly IReadOnlyDictionary<long, PostgresTableRow> SetTables = PostgresTables.Of(
        SetU,
        SetO,
        SetU with
        {
            Oid = 16812,
            Name = "u_1",
        },
        SetU with
        {
            Oid = 16813,
            Name = "u_2",
        }
    );

    private static readonly (bool?, string?) FromBranches = (false, "set-operation");

    private static readonly (bool?, string?) NoOrigin = (null, "no-origin");

    private static WalkOutcome RunSet(PlanNode root, params long[] types)
    {
        var (columns, origins) = Built(false, [.. types.Select(static _ => Expression)]);
        return NullabilityWalk.Run(
            root,
            columns,
            origins,
            new WalkScope(false, false, true, SetTables) { ColumnTypes = types }
        );
    }

    private static PlanNode AppendOf(params PlanNode[] members) =>
        Over("Append", Scan("x")) with
        {
            Plans = [.. members.Select(static member => member with { ParentRelationship = "Member" })],
        };

    // A set operation's leaf scan, "*SELECT* <k>", over the branch's plan.
    private static PlanNode Select(int k, PlanNode branch, params string[] output) =>
        Over("Subquery Scan", branch, "Subquery") with
        {
            Alias = $"*SELECT* {k}",
            Output = output,
        };

    // PostgreSQL 18's SetOp, with the left input first: or 14's, with one child, the flagged Append.
    private static PlanNode SetOp(string command, PlanNode outer, PlanNode? inner = null) =>
        (
            inner is null
                ? Over("SetOp", outer)
                : Join("Inner", outer, inner) with
                {
                    NodeType = "SetOp",
                    JoinType = null,
                }
        ) with
        {
            Command = command,
        };

    [Fact]
    public void Run_UnionAllOfNotNullColumnsOfTheResultsType_IsFalseFromTheBranches()
    {
        var outcome = RunSet(AppendOf(Scan("u", "u.name", "u.id"), Scan("o", "o.label", "o.user_id")), Text, Int4);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        outcome.Reason.ShouldBeNull();
        AnswersOf(outcome).ShouldBe([FromBranches, FromBranches]);
    }

    public static TheoryData<string> BranchesThatMayBeNull =>
        [
            Text(Scan("x", "NULL::text") with { NodeType = "Result", Alias = null, RelationName = null }),
            Text(Scan("o", "o.note")),
            Text(Join("Left", Scan("u", "u.id"), Scan("o", "o.label")) with { Output = ["o.label"] }),
            Text(Scan("w", "w.name")),
            Text(Over("Subquery Scan", Scan("u", "u.name"), "Subquery") with { Alias = "s", Output = ["s.name"] }),
        ];

    // A NULL, a nullable column, the side a LEFT JOIN extends, a relation that "tables" and "relations" did not
    // answer, and a user subquery's scan, which is mapped by no position.
    [Theory]
    [MemberData(nameof(BranchesThatMayBeNull))]
    public void Run_UnionAllWithABranchThatMayBeNull_KeepsNoOrigin(string branch) =>
        AnswersOf(RunSet(AppendOf(Scan("u", "u.name"), PlanNode.Read(branch).ShouldNotBeNull()), Text))
            .ShouldBe([NoOrigin]);

    // The branch's cast to the result's type is not printed: an integer under a bigint is in the family, an integer
    // under a text and a domain under its base are not.
    [Theory]
    [InlineData("u.id", "o.id", Int8, true)]
    [InlineData("u.id", "o.label", Text, false)]
    [InlineData("u.dom", "o.label", Text, false)]
    [InlineData("u.dom", "u_1.dom", Domain, true)]
    public void Run_BranchOfAnotherTypeThanTheResult_CountsWithinAFamily(
        string first,
        string second,
        long type,
        bool answered
    ) =>
        AnswersOf(RunSet(AppendOf(Scan("u", first), Scan(second.Split('.')[0], second)), type))
            .ShouldBe([answered ? FromBranches : NoOrigin]);

    // A constant the planner put in the leaf scan's place is a constant at any position.
    [Fact]
    public void Run_ConstantOfALeafScanUnderAnAppend_IsAnswered() =>
        AnswersOf(
                RunSet(
                    AppendOf(
                        Scan("u", "'a'::text", "u.name"),
                        Select(2, Scan("o", "o.label"), "'b'::text", "\"*SELECT* 2\".label")
                    ),
                    Text,
                    Text
                )
            )
            .ShouldBe([FromBranches, NoOrigin]);

    // An appendrel, a UNION ALL that a FROM subquery, a CTE or an inlined SQL function holds, prints a branch that the
    // planner keeps as a "*SELECT* <k>" scan in the order the outer query needs: SELECT s.b, s.a FROM (SELECT
    // p.sku AS a, p.name AS b ... UNION ALL (SELECT p2.sku, NULLIF(p2.name, 'x') ... LIMIT 5)) s.  Its column 0 is
    // NULLIF's, and by position it would be p2.sku.
    public static TheoryData<string> Appendrels =>
        [
            Text(
                AppendOf(
                    Scan("u", "u.name", "u.name"),
                    Select(
                        2,
                        Over("Limit", Scan("u_1", "u_1.name", "NULLIF(u_1.name, 'x'::text)")) with
                        {
                            Output = ["u_1.name", "(NULLIF(u_1.name, 'x'::text))"],
                        },
                        "\"*SELECT* 2\".\"nullif\"",
                        "\"*SELECT* 2\".name"
                    )
                )
            ),
            Text(
                Over(
                    "Sort",
                    AppendOf(
                        Scan("u", "u.name", "u.name"),
                        Select(
                            2,
                            Over("Limit", Scan("u_1", "u_1.name", "NULLIF(u_1.name, u_1.name)")) with
                            {
                                Output = ["u_1.name", "(NULLIF(u_1.name, u_1.name))"],
                            },
                            "\"*SELECT* 2\".\"nullif\"",
                            "\"*SELECT* 2\".name"
                        )
                    )
                ) with
                {
                    Output = ["u.name", "u.name"],
                }
            ),
        ];

    [Theory]
    [MemberData(nameof(Appendrels))]
    public void Run_LeafScanUnderAnAppend_IsNotMappedByPosition(string plan) =>
        AnswersOf(RunSet(PlanNode.Read(plan).ShouldNotBeNull(), Text, Text)).ShouldBe([NoOrigin, NoOrigin]);

    // A 14 SetOp's flagged member is its own leaf scan, mapped by position: SELECT u.email AS name, u.name AS email
    // FROM u EXCEPT SELECT o.label, o.label FROM o prints the crossed names, and column 0 is u.email.
    private static PlanNode CrossedExcept14(string alias, PlanNode? branch = null) =>
        SetOp(
            "Except",
            AppendOf(
                Over("Subquery Scan", branch ?? Scan("u", "u.email", "u.name"), "Subquery") with
                {
                    Alias = alias,
                    Output = [$"\"{alias}\".name", $"\"{alias}\".email", "0"],
                },
                Select(2, Scan("o", "o.label", "o.label"), "\"*SELECT* 2\".label", "\"*SELECT* 2\".label", "1")
            )
        ) with
        {
            Output = [$"\"{alias}\".name", $"\"{alias}\".email", "(0)"],
        };

    [Fact]
    public void Run_FlaggedLeafScanOn14_IsMappedByPosition() =>
        AnswersOf(RunSet(CrossedExcept14("*SELECT* 1"), Text, Text)).ShouldBe([NoOrigin, FromBranches]);

    [Fact]
    public void Run_FlaggedSubqueryScanOfAnotherAlias_IsNotMappedByPosition() =>
        AnswersOf(RunSet(CrossedExcept14("s"), Text, Text)).ShouldBe([NoOrigin, NoOrigin]);

    // A user's subquery of the same name under the planner's flagged scan, through a view, prints its columns in the
    // order the branch needs them: only the flagged member itself is the planner's.
    [Fact]
    public void Run_UserScanOfTheSameNameUnderTheFlaggedScan_IsNotMappedByPosition()
    {
        var user = Over("Subquery Scan", Scan("u", "u.email", "u.name"), "Subquery") with
        {
            Alias = "*SELECT* 1",
            Output = ["\"*SELECT* 1\".name", "\"*SELECT* 1\".email"],
        };

        AnswersOf(RunSet(CrossedExcept14("*SELECT* 1", user), Text, Text)).ShouldBe([NoOrigin, NoOrigin]);
    }

    // On 18 the planner drops its own leaf scan when it adds nothing, and an input may be a user's subquery of that
    // name: CREATE VIEW v AS SELECT "*SELECT* 9".b, "*SELECT* 9".a FROM (SELECT p.sku AS a, NULLIF(p.name, 'x') AS b
    // FROM products p OFFSET 0) AS "*SELECT* 9"; SELECT b, a FROM v EXCEPT SELECT 'zz', 'zz', whose b is NULL.
    [Theory]
    [InlineData("Except")]
    [InlineData("Intersect")]
    public void Run_SubqueryScanOfTheLeafAliasAsAnInputOn18_IsNotMappedByPosition(string command)
    {
        var user = Over("Subquery Scan", Scan("u", "u.name", "NULLIF(u.name, 'x'::text)"), "Subquery") with
        {
            Alias = "*SELECT* 9",
            Output = ["\"*SELECT* 9\".b", "\"*SELECT* 9\".a"],
        };
        // EXCEPT 'zz', 'zz'; and INTERSECT a nullable column, since an input of constants would prove it.
        var other =
            command == "Except"
                ? Scan("x", "'zz'::text", "'zz'::text") with
                {
                    NodeType = "Result",
                    Alias = null,
                    RelationName = null,
                }
                : Scan("o", "o.note", "o.note");

        AnswersOf(RunSet(Inputs18(command, user, other), Text, Text)).ShouldBe([NoOrigin, NoOrigin]);
    }

    // UNION on 14 is a HashAggregate over the Append, which prints a value the branch computed in parentheses and its
    // cast to the result's type; on 18 a Unique over a Sort.
    [Fact]
    public void Run_UnionUnderAHashAggregate_IsFollowedThroughWhatItPrints()
    {
        var plan = Over("HashAggregate", AppendOf(Scan("u", "u.id", "'k'::text"), Scan("o", "o.id", "'j'::text"))) with
        {
            Output = ["((u.id)::bigint)", "('k'::text)"],
        };

        AnswersOf(RunSet(plan, Int8, Text)).ShouldBe([FromBranches, FromBranches]);
    }

    [Fact]
    public void Run_UnionUnderAUniqueOverASort_IsAnswered()
    {
        var append = AppendOf(Scan("u", "u.name"), Scan("o", "o.label"));
        var plan = Over("Unique", Over("Sort", append) with { Output = ["u.name"] }) with { Output = ["u.name"] };

        AnswersOf(RunSet(plan, Text)).ShouldBe([FromBranches]);
    }

    // A node that computes over the set operation passes on only an entry that is the child's at the same ordinal:
    // crossed entries, an aggregate, and a text the first branch prints twice are not.
    [Theory]
    [InlineData(new[] { "u.email", "u.name" }, new[] { "u.name", "u.email" }, false, false)]
    [InlineData(new[] { "u.name", "count(*)" }, new[] { "u.name", "u.email" }, true, false)]
    [InlineData(new[] { "('k'::text)", "('k'::text)" }, new[] { "'k'::text", "'k'::text" }, false, false)]
    [InlineData(new[] { "u.name", "('k'::text)" }, new[] { "u.name", "'k'::text" }, true, true)]
    public void Run_NodeThatComputesOverTheSetOperation_PassesOnTheChildsEntryAlone(
        string[] printed,
        string[] first,
        bool firstAnswered,
        bool secondAnswered
    )
    {
        var plan = Over("HashAggregate", AppendOf(Scan("u", first), Scan("o", "o.label", "'k'::text"))) with
        {
            Output = printed,
        };

        AnswersOf(RunSet(plan, Text, Text))
            .ShouldBe([firstAnswered ? FromBranches : NoOrigin, secondAnswered ? FromBranches : NoOrigin]);
    }

    // INTERSECT returns rows of both inputs: one that is never NULL proves the column.  On 14 the right branch may
    // be the first member, flagged 1; on 18 the inputs may be swapped.
    private static PlanNode Flagged14(string command, PlanNode left, PlanNode right, bool rightFirst = false)
    {
        var first = Select(1, left, "\"*SELECT* 1\".c", "0");
        var second = Select(2, right, "\"*SELECT* 2\".c", "1");
        return SetOp(command, AppendOf(rightFirst ? [second, first] : [first, second])) with
        {
            Output = rightFirst ? ["\"*SELECT* 2\".c", "(1)"] : ["\"*SELECT* 1\".c", "(0)"],
        };
    }

    private static PlanNode Inputs18(string command, PlanNode outer, PlanNode inner) =>
        SetOp(command, outer, inner) with
        {
            Output = outer.Output,
        };

    public static TheoryData<string, bool> Intersects =>
        new()
        {
            { Text(Flagged14("Intersect", Scan("o", "o.note"), Scan("u", "u.name"), rightFirst: true)), true },
            { Text(Flagged14("Intersect All", Scan("u", "u.name"), Scan("o", "o.note"))), true },
            { Text(Flagged14("Intersect", Scan("o", "o.note"), Scan("u", "u.email"))), false },
            { Text(Inputs18("Intersect", Scan("o", "o.note"), Scan("u", "u.name"))), true },
            { Text(Inputs18("Intersect All", Scan("u", "u.name"), Scan("o", "o.note"))), true },
            { Text(Inputs18("Intersect", Scan("o", "o.note"), Scan("u", "u.email"))), false },
        };

    [Theory]
    [MemberData(nameof(Intersects))]
    public void Run_Intersect_IsFalseWhenEitherInputIs(string plan, bool answered) =>
        AnswersOf(RunSet(PlanNode.Read(plan).ShouldNotBeNull(), Text)).ShouldBe([answered ? FromBranches : NoOrigin]);

    // EXCEPT returns rows of its left input alone: on 14 the member flagged 0, on 18 the Outer child.
    public static TheoryData<string, bool> Excepts =>
        new()
        {
            { Text(Flagged14("Except", Scan("u", "u.name"), Scan("o", "o.note"))), true },
            { Text(Flagged14("Except All", Scan("u", "u.name"), Scan("o", "o.note"), rightFirst: true)), true },
            { Text(Flagged14("Except", Scan("o", "o.note"), Scan("u", "u.name"))), false },
            { Text(Flagged14("Except", Scan("o", "o.note"), Scan("u", "u.name"), rightFirst: true)), false },
            { Text(Inputs18("Except", Scan("u", "u.name"), Scan("o", "o.note"))), true },
            { Text(Inputs18("Except All", Scan("o", "o.note"), Scan("u", "u.name"))), false },
        };

    [Theory]
    [MemberData(nameof(Excepts))]
    public void Run_Except_IsItsLeftInputs(string plan, bool answered) =>
        AnswersOf(RunSet(PlanNode.Read(plan).ShouldNotBeNull(), Text)).ShouldBe([answered ? FromBranches : NoOrigin]);

    public static TheoryData<string> SetOpsOfAnotherShape =>
        [
            // Two members flagged 0, three members, and none flagged.
            Text(
                SetOp(
                    "Except",
                    AppendOf(
                        Select(1, Scan("u", "u.name"), "\"*SELECT* 1\".c", "0"),
                        Select(2, Scan("u_1", "u_1.name"), "\"*SELECT* 2\".c", "0")
                    )
                ) with
                {
                    Output = ["\"*SELECT* 1\".c", "(0)"],
                }
            ),
            Text(
                SetOp(
                    "Intersect",
                    AppendOf(
                        Select(1, Scan("u", "u.name"), "\"*SELECT* 1\".c", "0"),
                        Select(2, Scan("u_1", "u_1.name"), "\"*SELECT* 2\".c", "1"),
                        Select(3, Scan("u_2", "u_2.name"), "\"*SELECT* 3\".c", "1")
                    )
                ) with
                {
                    Output = ["\"*SELECT* 1\".c", "(0)"],
                }
            ),
            Text(
                SetOp(
                    "Except",
                    AppendOf(Select(1, Scan("u", "u.name"), "\"*SELECT* 1\".c"), Scan("u_1", "u_1.name"))
                ) with
                {
                    Output = ["\"*SELECT* 1\".c"],
                }
            ),
            // A command the walk does not know, and none.
            Text(Inputs18("Merge", Scan("u", "u.name"), Scan("u_1", "u_1.name"))),
            Text(Inputs18("Intersect", Scan("u", "u.name"), Scan("u_1", "u_1.name")) with { Command = null }),
            // A recursive CTE's union.
            Text(
                Over(
                    "Sort",
                    AppendOf(
                        Join("Inner", Scan("u", "u.name"), Scan("u_1", "u_1.name")) with
                        {
                            NodeType = "Recursive Union",
                            JoinType = null,
                            Output = ["u.name"],
                        },
                        Scan("u_2", "u_2.name")
                    )
                ) with
                {
                    Output = ["u.name"],
                }
            ),
        ];

    [Theory]
    [MemberData(nameof(SetOpsOfAnotherShape))]
    public void Run_SetOperationOfAnotherShape_KeepsNoOrigin(string plan) =>
        AnswersOf(RunSet(PlanNode.Read(plan).ShouldNotBeNull(), Text)).ShouldBe([NoOrigin]);

    // a EXCEPT b EXCEPT c: on 14 the inner SetOp is a Result member that prints its entries and a flag of its own, on
    // 18 the Outer input.  The inner one's type is the common type of its own inputs, which the row description does
    // not give, so it is false only when each of its inputs is: then each is of the result's family.
    private static PlanNode NestedExcept14(string innerRight)
    {
        var inner = SetOp(
            "Except",
            AppendOf(
                Select(1, Scan("u", "u.id"), "\"*SELECT* 1\".id", "0"),
                Select(2, Scan("o", innerRight), "\"*SELECT* 2\".c", "1")
            )
        ) with
        {
            Output = ["\"*SELECT* 1\".id", "(0)"],
        };
        var result = Over("Result", inner) with { Output = ["\"*SELECT* 1\".id", "0"] };
        return SetOp("Except", AppendOf(result, Select(3, Scan("u_1", "u_1.id"), "\"*SELECT* 3\".id", "1"))) with
        {
            Output = ["\"*SELECT* 1\".id", "(0)"],
        };
    }

    private static PlanNode NestedExcept18(string innerRight) =>
        Inputs18("Except", Inputs18("Except", Scan("u", "u.id"), Scan("o", innerRight)), Scan("u_1", "u_1.id"));

    [Theory]
    [InlineData("o.user_id", true)]
    [InlineData("o.ref", false)]
    public void Run_NestedExcept_IsFalseWhenEveryInputOfTheInnerOneIs(string innerRight, bool answered)
    {
        AnswersOf(RunSet(NestedExcept14(innerRight), Int4)).ShouldBe([answered ? FromBranches : NoOrigin]);
        AnswersOf(RunSet(NestedExcept18(innerRight), Int4)).ShouldBe([answered ? FromBranches : NoOrigin]);
    }

    // An INTERSECT that is a branch of a UNION ALL: its one input that is never NULL says nothing, since its type
    // may be the other's.
    [Fact]
    public void Run_NestedIntersectWithAnInputThatMayBeNull_KeepsNoOrigin() =>
        AnswersOf(
                RunSet(
                    AppendOf(Inputs18("Intersect", Scan("u", "u.id"), Scan("o", "o.ref")), Scan("u_1", "u_1.id")),
                    Int4
                )
            )
            .ShouldBe([NoOrigin]);

    // A partitioned table in a branch is an Append of its partitions nested as a member, a union of them; an Append
    // without a member returns nothing that proves anything.
    [Fact]
    public void Run_PartitionedTableAsAMember_IsAUnionOfItsPartitions()
    {
        var partitions = AppendOf(
            Scan("p_1", "p_1.name") with
            {
                RelationName = "u_1",
            },
            Scan("p_2", "p_2.name") with
            {
                RelationName = "u_2",
            }
        );

        AnswersOf(RunSet(AppendOf(Scan("o", "o.label"), partitions), Text)).ShouldBe([FromBranches]);
        AnswersOf(RunSet(AppendOf(), Text)).ShouldBe([NoOrigin]);
    }

    // A partitioned table's root Append prints no Output, and is a union of its partitions: its constant is answered,
    // as an expression when "partitions" says the Append is the table's, and as a set operation's when it was not
    // answered and the Append is read as one.
    [Theory]
    [InlineData(true, "expression")]
    [InlineData(false, "set-operation")]
    public void Run_ConstantOverAPartitionedTablesRootAppend_IsAnsweredWithTheAppendsReading(
        bool answered,
        string source
    )
    {
        var root = AppendOf(
            Scan("p_1", "p_1.id", "'x'::text") with
            {
                RelationName = "u_1",
            },
            Scan("p_2", "p_2.id", "'x'::text") with
            {
                RelationName = "u_2",
            }
        );
        var (columns, origins) = Built(false, Expression, Expression);
        var partitions = new Dictionary<PostgresRelationName, IReadOnlyList<long>>
        {
            [new PostgresRelationName("public", "u_1")] = [SetU.Oid],
            [new PostgresRelationName("public", "u_2")] = [SetU.Oid],
        };

        var outcome = NullabilityWalk.Run(
            root,
            columns,
            origins,
            new WalkScope(false, false, true, SetTables)
            {
                Partitions = answered ? partitions : null,
                ColumnTypes = [Int4, Text],
            }
        );

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(outcome).ShouldBe([(false, source), (false, source)]);
    }

    // The walk does not take on a set operation under a join, nor one under grouping sets, which put NULL in a key.
    [Fact]
    public void Run_SetOperationUnderAJoin_KeepsNoOrigin() =>
        AnswersOf(
                RunSet(
                    Join("Left", Scan("u", "u.name"), AppendOf(Scan("u_1", "u_1.name"), Scan("u_2", "u_2.name"))) with
                    {
                        Output = ["u_1.name"],
                    },
                    Text
                )
            )
            .ShouldBe([NoOrigin]);

    [Fact]
    public void Run_SetOperationUnderGroupingSets_KeepsNoOrigin() =>
        AnswersOf(
                RunSet(
                    Over("Aggregate", AppendOf(Scan("u", "u.name"), Scan("o", "o.label"))) with
                    {
                        HasGroupingSets = true,
                        Output = ["u.name"],
                    },
                    Text
                )
            )
            .ShouldBe([NoOrigin]);

    // A branch is no part of the root's plan, whose instances find no grouping set in it: the node is no pass-through.
    [Fact]
    public void Run_GroupingSetsInsideABranch_KeepsNoOrigin() =>
        AnswersOf(
                RunSet(
                    AppendOf(
                        Over("Aggregate", Scan("u", "u.name")) with
                        {
                            HasGroupingSets = true,
                            Output = ["u.name"],
                        },
                        Scan("o", "o.label")
                    ),
                    Text
                )
            )
            .ShouldBe([NoOrigin]);

    // Without the row description's types the walk answers no set operation.
    [Fact]
    public void Run_SetOperationWithoutTheColumnsTypes_IsUnavailable()
    {
        var (columns, origins) = Built(false, Expression);

        var outcome = NullabilityWalk.Run(
            AppendOf(Scan("u", "u.name"), Scan("o", "o.label")),
            columns,
            origins,
            new WalkScope(false, false, true, SetTables)
        );

        outcome.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        AnswersOf(outcome).ShouldBe([NoOrigin]);
    }

    // A column with an origin beside the set operation is kept, for a root without Output.
    [Fact]
    public void Run_ColumnWithAnOriginBesideASetOperation_IsKept()
    {
        var (columns, origins) = Built(true, NotNull, Expression);

        var outcome = NullabilityWalk.Run(
            AppendOf(Scan("u", "u.id", "u.name"), Scan("o", "o.user_id", "o.label")),
            columns,
            origins,
            new WalkScope(true, false, true, SetTables) { ColumnTypes = [Int4, Text] }
        );

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(outcome).ShouldBe([(true, "heuristic"), FromBranches]);
    }

    [Theory]
    [InlineData("Append", true)]
    [InlineData("SetOp", true)]
    [InlineData("Recursive Union", false)]
    public void ReachesSetOperation_ThroughNodesThatPassTheirChildOn_FindsIt(string nodeType, bool reaches)
    {
        var node = Scan("u", "u.name") with { NodeType = nodeType, Alias = null, RelationName = null };

        NullabilityWalk.ReachesSetOperation(node).ShouldBe(reaches);
        NullabilityWalk.ReachesSetOperation(Over("Limit", Over("Result", Over("Sort", node)))).ShouldBe(reaches);
        NullabilityWalk.ReachesSetOperation(Join("Left", Scan("u"), node)).ShouldBeFalse();
        NullabilityWalk.ReachesSetOperation(Over("Aggregate", node) with { HasGroupingSets = true }).ShouldBeFalse();
    }
}
