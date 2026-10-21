using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Columns;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresWalkColumns;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// NullabilityWalk over plans a test writes: each column's answer, and the plan's value with its reason.
public sealed class NullabilityWalkTests
{
    private static WalkOutcome Run(PlanNode root, params Asked[] asked) => Run(root, false, asked);

    private static WalkOutcome Run(PlanNode root, bool oldAndNewMayBeNamed, params Asked[] asked)
    {
        var (columns, origins) = Built(asked);
        return NullabilityWalk.Run(
            new PlanInstancesCache(root, null),
            columns,
            origins,
            Scope(true, oldAndNewMayBeNamed)
        );
    }

    private static WalkScope Scope(bool gated, bool oldAndNewMayBeNamed = false, bool catalogFunctionsFirst = true) =>
        new(gated, oldAndNewMayBeNamed, catalogFunctionsFirst, Tables, []);

    private static WalkOutcome RunAs(WalkScope scope, PlanNode root, params Asked[] asked)
    {
        var (columns, origins) = Built(scope.Gated, asked);
        return NullabilityWalk.Run(new PlanInstancesCache(root, null), columns, origins, scope);
    }

    // A query that is planned for a column with no origin, and has no word of the gate.
    private static WalkOutcome RunUngated(PlanNode root, params Asked[] asked)
    {
        var (columns, origins) = Built(false, asked);
        return NullabilityWalk.Run(new PlanInstancesCache(root, null), columns, origins, Scope(false));
    }

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
        AnswersOf(outcome).ShouldBe([NotNullCatalog, NullableCatalog]);
    }

    [Fact]
    public void Run_ColumnsOfATableThatAJoinExtends_AreNullableByTheOuterJoin() =>
        AnswersOf(Run(LeftJoin("b.id", "b.opt"), NotNull, Optional)).ShouldBe([NullableOuterJoin, NullableOuterJoin]);

    [Theory]
    [InlineData("a.id + 1")]
    [InlineData("(a.id)")]
    [InlineData("COALESCE(a.id, b.id)")]
    [InlineData("$1")]
    [InlineData("old.id")]
    [InlineData("id")]
    public void Run_ColumnOfATableWhoseEntryCannotBeMapped_KeepsTheHeuristic(string entry) =>
        AnswersOf(Run(LeftJoin(entry, entry), NotNull, Optional)).ShouldBe([NullableHeuristic, NullableHeuristic]);

    [Fact]
    public void Run_ColumnOfAView_SaysViewUnlessAJoinExtendsIt() =>
        AnswersOf(Run(LeftJoin("a.id", "b.id", "a.id + 1"), OfView, OfView, OfView))
            .ShouldBe([UnknownView, NullableOuterJoin, UnknownView]);

    // The walk's rule over a foreign relkind, through a plain scan, as through a Foreign Scan.
    [Fact]
    public void Run_ColumnOfAForeignTable_SaysForeignTableUnlessAJoinExtendsIt() =>
        AnswersOf(Run(LeftJoin("a.id", "b.id", "a.id + 1"), OfForeign, OfForeign, OfForeign))
            .ShouldBe([UnknownForeignTable, NullableOuterJoin, UnknownForeignTable]);

    // Under a join that null-extends, an expression is answered only where the walk finds the node that computes it:
    // here the scans print no Output, and may compute anything.  A column without an origin is never mapped.
    [Fact]
    public void Run_ColumnWithoutAnOrigin_IsUnknownAndIsNotLookedUp()
    {
        var outcome = Run(LeftJoin("b.id", "count(*)"), Expression, Expression);

        AnswersOf(outcome).ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);
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
        AnswersOf(outcome).ShouldBe([NotNullCatalog, NotNullExpression]);
    }

    [Fact]
    public void Run_UngatedQueryWithAPlanThatCannotBeUsed_KeepsTheCatalogsAnswer()
    {
        var plan = Join("Inner", Scan("a"), Scan("a")) with { Output = ["a.id"] };

        var outcome = RunUngated(plan, NotNull);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        outcome.Reason.ShouldBe(WalkReasons.DoubledAlias);
        AnswersOf(outcome).ShouldBe([NotNullCatalog]);
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
        AnswersOf(outcome).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
    }

    // A query that gives a table's columns aliases, users u(a, b), prints them under the aliases: the check takes it
    // for another statement's plan, and the join's answer stands.
    [Fact]
    public void Run_ReferenceThatNamesAnotherColumnUnderALeftJoin_KeepsTheJoinsAnswer() =>
        AnswersOf(Run(LeftJoin("a.other", "b.other"), NotNull, NotNull)).ShouldBe([NotNullCatalog, NullableOuterJoin]);

    [Fact]
    public void Run_UngatedQueryWhoseReferenceNamesAnotherColumn_IsUnavailableAndKeepsTheCatalogsAnswer()
    {
        var outcome = RunUngated(Scan("t", "t.opt"), NotNull);

        outcome.Reason.ShouldBe(WalkReasons.OutputDiffers);
        AnswersOf(outcome).ShouldBe([NotNullCatalog]);
    }

    // A view's column prints as the column of the table under the view, under that column's name.
    [Fact]
    public void Run_ViewsColumnPrintedUnderTheBaseTablesName_IsWalked()
    {
        var outcome = Run(Scan("t", "t.opt"), OfView);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(outcome).ShouldBe([UnknownView]);
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

        AnswersOf(outcome).ShouldBe([NotNullExpression, NotNullExpression, NullableExpression, UnknownNoOrigin]);
    }

    [Theory]
    [InlineData("Inner", true)]
    [InlineData("Inner", false)]
    [InlineData("Semi", true)]
    [InlineData("Right Semi", false)]
    public void Run_ExpressionUnderAJoinThatExtendsNothing_IsAnswered(string joinType, bool gated) =>
        AnswersOf(RunAs(Scope(gated), Join(joinType, Scan("a"), Scan("b")) with { Output = ["'k'::text"] }, Expression))
            .ShouldBe([NotNullExpression]);

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
            .ShouldBe([UnknownNoOrigin]);

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

        AnswersOf(outcome).ShouldBe([UnknownNoOrigin]);
    }

    private static readonly Dictionary<string, string> SetOperationsRows = new(StringComparer.Ordinal)
    {
        {
            "aggregate-over-an-append",
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
            )
        },
        {
            "sort-over-an-append",
            Text(Over("Sort", Over("Append", Scan("a"), "Member")) with { Output = ["('a'::text)"] })
        },
        {
            "set-op-over-two-scans",
            Text(
                Join("Inner", Scan("a"), Scan("b")) with
                {
                    NodeType = "SetOp",
                    JoinType = null,
                    Output = ["('a'::text)"],
                }
            )
        },
    };

    public static TheoryData<string> SetOperations => [.. SetOperationsRows.Keys];

    // The root of a set operation prints its first branch, which says nothing of the others.
    [Theory]
    [MemberData(nameof(SetOperations))]
    public void Run_ExpressionOverASetOperation_KeepsNoOrigin(string name)
    {
        var outcome = RunUngated(PlanNode.Read(SetOperationsRows[name]).ShouldNotBeNull(), Expression);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(outcome).ShouldBe([UnknownNoOrigin]);
    }

    [Fact]
    public void Run_FunctionWhenTheCatalogIsNotFirst_KeepsNoOrigin() =>
        AnswersOf(RunAs(Scope(false, catalogFunctionsFirst: false), Scan("t", "count(*)"), Expression))
            .ShouldBe([UnknownNoOrigin]);

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
            .ShouldBe([NotNullExpression]);

    [Fact]
    public void Run_BareReferenceArgumentUnderAJoinOfTwo_KeepsNoOrigin() =>
        AnswersOf(RunUngated(Join("Inner", Scan("a"), Scan("b")) with { Output = ["GREATEST(id, opt)"] }, Expression))
            .ShouldBe([UnknownNoOrigin]);

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
            .ShouldBe([UnknownNoOrigin, NotNullExpression]);
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
        var row = PostgresTables.ColumnOf(0, "id", PostgresRows.Int4.Oid, table, 1);
        var origin = ColumnOrigin.Find(row, PostgresTables.Of(table));
        var column = PostgresColumns.Build(
            row,
            origin,
            PostgresRows.Of(PostgresRows.Int4),
            TypeSpellings.None,
            gated: true
        );

        var outcome = NullabilityWalk.Run(
            new PlanInstancesCache(Scan("n", "id"), null),
            [column],
            [origin],
            Scope(true)
        );

        AnswersOf(outcome).ShouldBe([NullableCatalog]);
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
        var row = PostgresTables.ColumnOf(0, "id", PostgresRows.Int4.Oid, parent, 1);
        var origin = ColumnOrigin.Find(row, PostgresTables.Of(parent));
        var column = PostgresColumns.Build(
            row,
            origin,
            PostgresRows.Of(PostgresRows.Int4),
            TypeSpellings.None,
            gated: true
        );

        var outcome = NullabilityWalk.Run(
            new PlanInstancesCache(Scan("p", "id"), null),
            [column],
            [origin],
            Scope(true)
        );

        AnswersOf(outcome).ShouldBe([NullableCatalog]);
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
        var row = PostgresTables.ColumnOf(0, "id", PostgresRows.Int4.Oid, parted, 1);
        var origin = ColumnOrigin.Find(row, PostgresTables.Of(parted));
        var column = PostgresColumns.Build(
            row,
            origin,
            PostgresRows.Of(PostgresRows.Int4),
            TypeSpellings.None,
            gated: true
        );

        var outcome = NullabilityWalk.Run(
            new PlanInstancesCache(Scan("pt", "id"), null),
            [column],
            [origin],
            Scope(true)
        );

        AnswersOf(outcome).ShouldBe([NullableCatalog]);
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
        var row = PostgresTables.ColumnOf(0, "id", PostgresRows.Int4.Oid, ruled, 1);
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

        var outcome = NullabilityWalk.Run(new PlanInstancesCache(plan, null), [column], [origin], Scope(true));

        AnswersOf(outcome).ShouldBe([NullableCatalog]);
    }

    // A table whose NOT NULL columns the catalog answers, as "tables" gives it: no attribute is a system column.
    private static readonly PostgresTableRow Products = Table with { Oid = 16708, Name = "products" };

    // An INSERT into the table, with its RETURNING list as the root's Output.
    private static PlanNode InsertInto(PostgresTableRow table, params string[] output) =>
        Over("ModifyTable", Scan("x") with { NodeType = "Result", Alias = null }) with
        {
            Alias = table.Name,
            RelationName = table.Name,
            Schema = "public",
            Output = output,
        };

    private static WalkOutcome RunOver(PostgresTableRow table, PlanNode root)
    {
        var (columns, origins) = Built(false, Expression);
        return NullabilityWalk.Run(
            new PlanInstancesCache(root, null),
            columns,
            origins,
            new WalkScope(false, false, true, PostgresTables.Of(table), [])
        );
    }

    // A system column is never NULL in a row of a table, and "tables" reads no attribute of it.
    [Fact]
    public void Run_SystemColumnOfATable_IsAnsweredInAnExpression() =>
        AnswersOf(RunOver(Products, InsertInto(Products, "(products.xmax = 0)"))).ShouldBe([NotNullExpression]);

    // A partitioned table with no foreign partition keeps its NOT NULL in every partition, and every row has its system
    // columns.
    [Fact]
    public void Run_SystemColumnOfAPartitionedTable_IsAnsweredInAnExpression()
    {
        var parted = Products with { Relkind = "p", Relhassubclass = true };

        AnswersOf(RunOver(parted, InsertInto(parted, "(products.xmax = 0)"))).ShouldBe([NotNullExpression]);
    }

    // A system column of the side a join null-extends is NULL for a row the join did not match.
    [Theory]
    [InlineData("(a.xmax = 0)", false)]
    [InlineData("(b.xmax = 0)", null)]
    public void Run_SystemColumnThroughAnInstance_IsProvedOnlyWhereNoJoinExtendsIt(string entry, bool? nullable) =>
        AnswersOf(
                RunUngated(
                    Join("Left", Scan("a", "a.id", "a.xmax"), Scan("b", "b.id", "b.xmax")) with
                    {
                        Output = [entry],
                    },
                    Expression
                )
            )
            .ShouldBe([(nullable, nullable is null ? "no-origin" : "expression")]);

    // Nothing holds a foreign table's rows to anything.
    [Fact]
    public void Run_SystemColumnOfAForeignTable_KeepsNoOrigin() =>
        AnswersOf(RunOver(Foreign, Scan("ft", "(ft.xmax = 0)") with { NodeType = "Foreign Scan" }))
            .ShouldBe([UnknownNoOrigin]);

    // A table whose NOT NULL column the catalog would not answer false proves no system column either: an inheritance
    // child, a foreign partition or an INSTEAD rule brings rows from elsewhere.
    [Theory]
    [InlineData("children")]
    [InlineData("foreign-partition")]
    [InlineData("instead-rule")]
    public void Run_SystemColumnOfATableThatMayReadRowsFromElsewhere_KeepsNoOrigin(string reason)
    {
        var table = reason switch
        {
            "children" => Products with { Relhassubclass = true },
            "foreign-partition" => Products with { Relkind = "p", HasForeignPartition = true },
            _ => Products with { HasInsteadRule = true },
        };

        AnswersOf(RunOver(table, InsertInto(table, "(products.xmax = 0)"))).ShouldBe([UnknownNoOrigin]);
    }

    // Only the six names of the server's system columns are one, each of its own type.
    [Theory]
    [InlineData("(products.xmin = 0)", false)]
    [InlineData("((products.ctid)::text = 'x'::text)", null)]
    [InlineData("(products.oid = 0)", null)]
    [InlineData("(products.xmax = products.id)", false)]
    [InlineData("(products.xmax = products.opt)", null)]
    public void Run_SystemColumnInAnOperator_IsOfItsType(string entry, bool? nullable) =>
        AnswersOf(RunOver(Products, InsertInto(Products, entry)))
            .ShouldBe([(nullable, nullable is null ? "no-origin" : "expression")]);

    // Orders of the accounts "a", with columns that are never NULL and one that may be.
    private static readonly PostgresTableRow OrdersO = PostgresTables.TableOf(
        16709,
        "o",
        PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true),
        PostgresTables.Attribute(2, "placed_at", PostgresRows.Timestamptz.Oid, notNull: true),
        PostgresTables.Attribute(3, "quantity", PostgresRows.Int4.Oid, notNull: true),
        PostgresTables.Attribute(4, "note", PostgresRows.Text.Oid),
        PostgresTables.Attribute(5, "user_id", PostgresRows.Int4.Oid, notNull: true),
        PostgresTables.Attribute(6, "parent_id", PostgresRows.Int4.Oid)
    );

    private static WalkOutcome RunOverOrders(PlanNode root)
    {
        var (columns, origins) = Built(false, Expression);
        return NullabilityWalk.Run(
            new PlanInstancesCache(root, null),
            columns,
            origins,
            new WalkScope(false, false, true, PostgresTables.Of(TableA, OrdersO), [])
        );
    }

    // A join of "a" and "o" that prints what the aggregate above it reads.
    private static PlanNode JoinOfAAndO(string joinType) =>
        Join(joinType, Scan("a", "a.id"), Scan("o", "o.placed_at", "o.quantity", "o.note")) with
        {
            Output = ["a.id", "o.placed_at", "o.quantity", "o.note"],
        };

    // An Aggregate with a Group Key over the join of "a" and "o", which prints its aggregate bare, under a Sort that
    // prints it in parentheses or as the root.
    private static PlanNode GroupedOverAAndO(bool sorted, string aggregate)
    {
        var grouped = Aggregate("Hashed", true, JoinOfAAndO("Inner"), "a.id", aggregate);
        return sorted
            ? Over("Sort", grouped) with
            {
                Output = ["(" + aggregate + ")", "a.id"],
            }
            : grouped with
            {
                Output = [aggregate, "a.id"],
            };
    }

    // Each group of an Aggregate with a Group Key is made of the rows that share its key, so it has one at least, and
    // the node prints its aggregate bare where a node above prints it in parentheses.
    [Theory]
    [InlineData(true, "max(o.placed_at)")]
    [InlineData(false, "max(o.placed_at)")]
    [InlineData(true, "sum(o.quantity)")]
    public void Run_AggregateOfAGroupedNode_IsNotNull(bool sorted, string aggregate) =>
        AnswersOf(RunOverOrders(GroupedOverAAndO(sorted, aggregate))).ShouldBe([NotNullExpression]);

    // A group's every value may be NULL, array_agg is in no row of the table, and a FILTER may leave a group no row.
    [Theory]
    [InlineData("max(o.note)")]
    [InlineData("array_agg(o.quantity)")]
    [InlineData("max(o.placed_at) FILTER (WHERE (o.quantity > 1))")]
    public void Run_AggregateOfAGroupedNodeThatMayBeNull_KeepsNoOrigin(string aggregate) =>
        AnswersOf(RunOverOrders(GroupedOverAAndO(sorted: true, aggregate))).ShouldBe([UnknownNoOrigin]);

    // An Aggregate with no Group Key returns a row for an input of none, and a node that is no Aggregate computes no
    // aggregate.
    [Theory]
    [InlineData("Aggregate", "Plain", false)]
    [InlineData("Aggregate", "Sorted", false)]
    [InlineData("Group", "Sorted", true)]
    [InlineData("WindowAgg", null, true)]
    public void Run_AggregateOfAPlainNode_KeepsNoOrigin(string nodeType, string? strategy, bool groupKey)
    {
        var plain = Aggregate(strategy ?? "Plain", groupKey, JoinOfAAndO("Inner"), "max(o.placed_at)") with
        {
            NodeType = nodeType,
            Strategy = strategy,
        };

        AnswersOf(RunOverOrders(Over("Sort", plain) with { Output = ["(max(o.placed_at))"] }))
            .ShouldBe([UnknownNoOrigin]);
    }

    // A node with Grouping Sets may have a group of no row, the one of the empty set: the walk does not use its plan.
    [Fact]
    public void Run_AggregateOfANodeWithGroupingSets_KeepsNoOrigin() =>
        AnswersOf(
                RunOverOrders(
                    Aggregate("Mixed", true, JoinOfAAndO("Inner"), "max(o.placed_at)") with
                    {
                        HasGroupingSets = true,
                    }
                )
            )
            .ShouldBe([UnknownNoOrigin]);

    // A grouped Aggregate over a join of "a" and "o" that null-extends "o", under a Sort.
    private static PlanNode GroupedOverALeftJoin(string aggregate) =>
        Over("Sort", Aggregate("Sorted", true, JoinOfAAndO("Left"), "a.id", aggregate)) with
        {
            Output = ["(" + aggregate + ")", "a.id"],
        };

    // A value of the side a join null-extends may be NULL in every row of a group.
    [Fact]
    public void Run_AggregateOfAnExtendedSide_KeepsNoOrigin() =>
        AnswersOf(RunOverOrders(GroupedOverALeftJoin("max(o.placed_at)"))).ShouldBe([UnknownNoOrigin]);

    // A value of the side the join keeps is in every row of a group.
    [Fact]
    public void Run_AggregateOfThePreservedSideOfAnOuterJoin_IsNotNull() =>
        AnswersOf(RunOverOrders(GroupedOverALeftJoin("max(a.id)"))).ShouldBe([NotNullExpression]);

    // A value the node takes from a child was computed under the child's grouping, here a plain one that returns a row
    // of NULL for an input of none.
    [Fact]
    public void Run_AggregateTakenFromAChild_KeepsNoOrigin() =>
        AnswersOf(
                RunOverOrders(
                    Aggregate(
                        "Sorted",
                        true,
                        Aggregate("Plain", false, Scan("o", "o.quantity"), "sum(o.quantity)"),
                        "((sum(o.quantity)) * 2)"
                    )
                )
            )
            .ShouldBe([UnknownNoOrigin]);

    // The same entry the node computes itself, over the rows its child scans.
    [Fact]
    public void Run_AggregateTheNodeComputesOverAChild_IsAnswered() =>
        AnswersOf(RunOverOrders(Aggregate("Sorted", true, Scan("o", "o.quantity"), "(sum(o.quantity) * 2)")))
            .ShouldBe([NotNullExpression]);

    // A column of no origin for each type of the row description, over "a" and "o".
    private static WalkOutcome RunTyped(PlanNode root, params long[] types)
    {
        var (columns, origins) = Built(false, [.. types.Select(static _ => Expression)]);
        return NullabilityWalk.Run(
            new PlanInstancesCache(root, null),
            columns,
            origins,
            new WalkScope(false, false, true, PostgresTables.Of(TableA, OrdersO), types)
        );
    }

    // SELECT a.id, (SELECT count(*) FROM o WHERE ...) FROM a: a Plain Aggregate returns one row for any input.
    private static PlanNode CountOfO() => Aggregate("Plain", false, Scan("o", "o.user_id"), "count(*)");

    // A scan of "a" that evaluates the subplan in its Output.
    private static PlanNode ScanOfA(string entry, params PlanNode[] subPlans) =>
        Scan("a", "a.id", entry) with
        {
            Plans = subPlans,
        };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_ScalarSubPlanOfAPlainCount_IsAnswered(bool sorted)
    {
        var scan = ScanOfA("(SubPlan 1)", SubPlan("SubPlan 1", CountOfO()));
        var root = sorted ? Over("Sort", scan) with { Output = ["a.id", "((SubPlan 1))"] } : scan;

        AnswersOf(RunTyped(root, BuiltInTypes.Int4, BuiltInTypes.Int8)).ShouldBe([UnknownNoOrigin, NotNullExpression]);
    }

    // On 14 a.id IN (SELECT count(*) ...) prints (SubPlan 1) too: its column is a boolean, which is no subquery's
    // value of a family.
    [Fact]
    public void Run_ScalarSubPlanOfABooleanColumn_KeepsNoOrigin() =>
        AnswersOf(
                RunTyped(ScanOfA("(SubPlan 1)", SubPlan("SubPlan 1", CountOfO())), BuiltInTypes.Int4, BuiltInTypes.Bool)
            )
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

    // A subquery's value counts only as the whole column, where its type is the column's.
    [Fact]
    public void Run_ScalarSubPlanInsideAnExpression_KeepsNoOrigin() =>
        AnswersOf(
                RunTyped(
                    ScanOfA("((SubPlan 1) + 1)", SubPlan("SubPlan 1", CountOfO())),
                    BuiltInTypes.Int4,
                    BuiltInTypes.Int8
                )
            )
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

    // A subplan that may return no row, or a row whose value may be NULL, or one whose name is no single subplan.
    [Theory]
    [InlineData("group-key")]
    [InlineData("sorted-without-a-key")]
    [InlineData("grouping-sets")]
    [InlineData("filter")]
    [InlineData("limit")]
    [InlineData("max")]
    [InlineData("two-outputs")]
    [InlineData("found-twice")]
    [InlineData("other-name")]
    [InlineData("init-plan")]
    public void Run_ScalarSubPlanThatMayReturnNoRow_KeepsNoOrigin(string shape)
    {
        var count = CountOfO();
        PlanNode[] subPlans = shape switch
        {
            "group-key" => [SubPlan("SubPlan 1", count with { Strategy = "Sorted", HasGroupKey = true })],
            "sorted-without-a-key" => [SubPlan("SubPlan 1", count with { Strategy = "Sorted" })],
            "grouping-sets" => [SubPlan("SubPlan 1", count with { HasGroupingSets = true })],
            "filter" => [SubPlan("SubPlan 1", count with { HasFilter = true })],
            "limit" => [SubPlan("SubPlan 1", Over("Limit", count) with { Output = ["(count(*))"] })],
            "max" => [SubPlan("SubPlan 1", count with { Output = ["max(o.placed_at)"] })],
            "two-outputs" => [SubPlan("SubPlan 1", count with { Output = ["count(*)", "count(*)"] })],
            "found-twice" => [SubPlan("SubPlan 1", count), SubPlan("SubPlan 1", count)],
            "other-name" => [SubPlan("SubPlan 2", count)],
            _ => [count with { ParentRelationship = "InitPlan", SubplanName = "SubPlan 1" }],
        };
        var type = shape == "max" ? BuiltInTypes.Timestamptz : BuiltInTypes.Int8;

        AnswersOf(RunTyped(ScanOfA("(SubPlan 1)", subPlans), BuiltInTypes.Int4, type))
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);
    }

    // A subquery evaluated on the side a join null-extends is NULL for a row the join did not match, and one evaluated
    // on the side it keeps is answered.
    [Theory]
    [InlineData(true, null)]
    [InlineData(false, false)]
    public void Run_ScalarSubPlanOnAJoinSide_IsAnsweredOnlyWhereKept(bool extended, bool? nullable)
    {
        var evaluates = Scan("b", "b.id", "(SubPlan 1)") with { Plans = [SubPlan("SubPlan 1", CountOfO())] };
        var join = extended ? Join("Left", Scan("a", "a.id"), evaluates) : Join("Left", evaluates, Scan("a", "a.id"));

        AnswersOf(RunTyped(join with { Output = ["a.id", "((SubPlan 1))"] }, BuiltInTypes.Int4, BuiltInTypes.Int8))
            .ShouldBe([UnknownNoOrigin, (nullable, nullable is null ? "no-origin" : "expression")]);
    }

    // a.id IN (SELECT o.user_id FROM o), as 18 prints it, over a subplan that scans "o" for the column and o.user_id.
    private static WalkOutcome RunAny(string entry, string column) =>
        RunTyped(
            ScanOfA(entry, SubPlan("SubPlan 2", Scan("o", column, "o.user_id"))),
            BuiltInTypes.Int4,
            BuiltInTypes.Bool
        );

    // An ANY is NULL only for a NULL on either side.
    [Theory]
    [InlineData("(ANY (a.id = (hashed SubPlan 2).col1))", "o.user_id")]
    [InlineData("(ANY (a.id = (SubPlan 2).col1))", "o.user_id")]
    [InlineData("(ANY (a.id = (SubPlan 2).col2))", "o.note")]
    public void Run_AnyOverAProvedSubPlanColumn_IsNotNull(string entry, string column) =>
        AnswersOf(RunAny(entry, column)).ShouldBe([UnknownNoOrigin, NotNullExpression]);

    // A column of the subplan that may be NULL, a left side that is not proved, and a subplan or a column that is not
    // there.
    [Theory]
    [InlineData("(ANY (a.id = (hashed SubPlan 2).col1))", "o.note")]
    [InlineData("(ANY (a.id = (hashed SubPlan 2).col1))", "o.parent_id")]
    [InlineData("(ANY (a.opt = (hashed SubPlan 2).col1))", "o.user_id")]
    [InlineData("(ANY (a.id = (hashed SubPlan 3).col1))", "o.user_id")]
    [InlineData("(ANY (a.id = (hashed SubPlan 2).col3))", "o.user_id")]
    public void Run_AnyOverASubPlanColumnNotProved_KeepsNoOrigin(string entry, string column) =>
        AnswersOf(RunAny(entry, column)).ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

    // A column of the subplan is proved as its own plan proves it: here the subplan's join null-extends "o".
    [Fact]
    public void Run_AnyOverASubPlanColumnThatAJoinExtends_KeepsNoOrigin()
    {
        var join = Join("Left", Scan("b", "b.id"), Scan("o", "o.user_id")) with { Output = ["o.user_id"] };

        AnswersOf(
                RunTyped(
                    ScanOfA("(ANY (a.id = (hashed SubPlan 2).col1))", SubPlan("SubPlan 2", join)),
                    BuiltInTypes.Int4,
                    BuiltInTypes.Bool
                )
            )
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);
    }

    // A subplan that names itself is followed no deeper than the walk follows plans.
    [Fact]
    public void Run_SubPlanThatNamesItself_KeepsNoOrigin()
    {
        var entry = "(ANY (a.id = (SubPlan 2).col1))";
        var subPlan = SubPlan("SubPlan 2", Scan("o", "(ANY (o.user_id = (SubPlan 2).col1))"));

        AnswersOf(RunTyped(ScanOfA(entry, subPlan), BuiltInTypes.Int4, BuiltInTypes.Bool))
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);
    }

    // In a statement that names OLD or NEW, the old or the new row may print under an alias a relation of the subplan
    // has, and no subplan is read.
    [Fact]
    public void Run_SubPlanWhenOldOrNewMayBeNamed_KeepsNoOrigin()
    {
        var (columns, origins) = Built(false, Expression, Expression);
        var outcome = NullabilityWalk.Run(
            new PlanInstancesCache(ScanOfA("(SubPlan 1)", SubPlan("SubPlan 1", CountOfO())), null),
            columns,
            origins,
            new WalkScope(false, true, true, PostgresTables.Of(TableA, OrdersO), [BuiltInTypes.Int4, BuiltInTypes.Int8])
        );

        AnswersOf(outcome).ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);
    }

    // EXISTS on 18 starts false and turns true at the subplan's first row.
    [Fact]
    public void Run_ExistsOfASubPlan_IsAnswered() =>
        AnswersOf(
                RunTyped(
                    ScanOfA("EXISTS(SubPlan 1)", SubPlan("SubPlan 1", Scan("o", "o.note"))),
                    BuiltInTypes.Int4,
                    BuiltInTypes.Bool
                )
            )
            .ShouldBe([UnknownNoOrigin, NotNullExpression]);

    // MERGE ... RETURNING merge_action() on 18, over the join of the source and the target that null-extends.
    [Fact]
    public void Run_MergeActionAtARootModifyTable_IsAnswered()
    {
        var join = Join("Right", Scan("o", "o.id"), Scan("a", "a.id")) with { Output = ["o.id", "a.id"] };
        var plan = Over("ModifyTable", join) with
        {
            Alias = "o",
            RelationName = "o",
            Schema = "public",
            Output = ["MERGE_ACTION()"],
        };

        AnswersOf(RunOverOrders(plan)).ShouldBe([NotNullExpression]);
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
        AnswersOf(outcome).ShouldBe([NullableHeuristic]);
    }

    // A CTE's own plan is another plan: its alias is reached from here through the CTE Scan alone.
    [Fact]
    public void Run_AliasUnderAnInitPlan_IsNotFound()
    {
        var plan = Over("CTE Scan", Scan("u"), "InitPlan") with { Alias = "s", Output = ["u.id"] };

        AnswersOf(Run(plan, NotNull)).ShouldBe([NullableHeuristic]);
    }

    // A single-relation query prints bare names.
    [Fact]
    public void Run_BareNameAtARootThatScansARelation_IsThatRelationsColumn() =>
        AnswersOf(Run(Scan("users", "id", "opt"), NotNull, Optional)).ShouldBe([NotNullCatalog, NullableCatalog]);

    // So does a Result that replaced a scan the planner proved empty, beside a scan that remains: there the name
    // says nothing of which relation.
    [Fact]
    public void Run_BareNameUnderAJoin_IsUnmappable() =>
        AnswersOf(Run(LeftJoin("a.id", "opt"), NotNull, Optional)).ShouldBe([NotNullCatalog, NullableHeuristic]);

    // A DELETE of one table prints bare names, and its ModifyTable is its target.
    [Fact]
    public void Run_BareNameAtARootModifyTable_IsTheTargetsColumn()
    {
        var plan = Over("ModifyTable", Scan("orders")) with { Alias = "orders", Output = ["id", "opt"] };

        AnswersOf(Run(plan, NotNull, Optional)).ShouldBe([NotNullCatalog, NullableCatalog]);
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

        AnswersOf(Run(plan, NotNull, NotNull)).ShouldBe([NullableHeuristic, NotNullCatalog]);
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

        AnswersOf(Run(plan, NotNull, NotNull)).ShouldBe([NotNullCatalog, NullableOuterJoin]);
    }

    // PostgreSQL 18 prints its old row as "old.id", and an inner scope may alias a table "old" as well: in a
    // statement that names OLD or NEW, only the target's alias is mapped at a ModifyTable.
    private static readonly PlanNode ModifyOverOld = Over("ModifyTable", Scan("old")) with
    {
        Alias = "ord2",
        Output = ["old.id", "ord2.id"],
    };

    [Fact]
    public void Run_AliasAtAModifyTableThatIsNotTheTargetsWhereOldOrNewMayBeNamed_KeepsTheHeuristic() =>
        AnswersOf(Run(ModifyOverOld, true, NotNull, NotNull)).ShouldBe([NullableHeuristic, NotNullCatalog]);

    // The flag is what decides: without it the same plan maps the alias.
    [Fact]
    public void Run_AliasAtAModifyTableThatIsNotTheTargetsWhereNeitherIsNamed_TakesTheCatalogsAnswer() =>
        AnswersOf(Run(ModifyOverOld, false, NotNull, NotNull)).ShouldBe([NotNullCatalog, NotNullCatalog]);

    // The old and the new row are a RETURNING list's alone.
    [Fact]
    public void Run_RootThatIsNoModifyTable_IsNotAffectedByOldOrNew() =>
        AnswersOf(Run(LeftJoin("a.id", "b.id"), true, NotNull, NotNull)).ShouldBe([NotNullCatalog, NullableOuterJoin]);

    [Fact]
    public void Run_QuotedAlias_NamesTheInstanceOfThatName()
    {
        var plan = Join("Left", Scan("u"), Scan("Order")) with { Output = ["u.id", "\"Order\".id"] };

        AnswersOf(Run(plan, NotNull, NotNull)).ShouldBe([NotNullCatalog, NullableOuterJoin]);
    }

    // PostgreSQL prints the target list first and what a sort or a group added after it.
    [Fact]
    public void Run_OutputWithMoreEntriesThanColumns_UsesTheFirst()
    {
        var plan = Over("Sort", LeftJoin()) with { Output = ["a.id", "b.id", "b.opt"] };

        var outcome = Run(plan, NotNull, NotNull);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(outcome).ShouldBe([NotNullCatalog, NullableOuterJoin]);
    }

    private static readonly Dictionary<string, (string Reason, string Plan)> UnusableRows = new(StringComparer.Ordinal)
    {
        { "no-output-of-an-append", ("no-output", Text(Over("Append", Scan("a"), "Member"))) },
        { "fewer-outputs-than-columns", ("no-output", Text(LeftJoin("a.id"))) },
        {
            "unknown-join-type",
            ("unknown-join-type", Text(Join("Sideways", Scan("a"), Scan("b")) with { Output = ["a.id", "a.opt"] }))
        },
        {
            "doubled-alias",
            ("doubled-alias", Text(Join("Inner", Scan("a"), Scan("a")) with { Output = ["a.id", "a.opt"] }))
        },
        {
            "grouping-sets",
            (
                "grouping-sets",
                Text(Over("Aggregate", Scan("a")) with { HasGroupingSets = true, Output = ["a.id", "a.opt"] })
            )
        },
    };

    public static TheoryData<string> Unusable => [.. UnusableRows.Keys];

    // The first is a UNION's Append, which has no Output; the second has fewer entries than the result has columns.
    [Theory]
    [MemberData(nameof(Unusable))]
    public void Run_PlanThatCannotBeUsed_IsUnavailableWithItsReasonAndEveryColumnKeepsWhatItWasBuiltWith(string name)
    {
        var (reason, plan) = UnusableRows[name];
        var outcome = Run(PlanNode.Read(plan).ShouldNotBeNull(), NotNull, OfView);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        outcome.Reason.ShouldBe(reason);
        AnswersOf(outcome).ShouldBe([NullableHeuristic, UnknownView]);
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
        AnswersOf(outcome).ShouldBe([NullableHeuristic, UnknownView, UnknownNoOrigin]);
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
            .Run(new PlanInstancesCache(LeftJoin("a.id"), null), columns, origins, Scope(true))
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

        AnswersOf(outcome).ShouldBe([NotNullCatalog, NullableCatalog, NullableOuterJoin]);
        outcome.Columns.Select(static walked => walked.Mapped).ShouldBe([true, true, true]);
    }

    // SELECT s.id, s.opt FROM (SELECT t.opt AS id, t.id AS opt FROM t) s: matched by name, the columns would be read
    // crossed; by their origins they are not.
    [Fact]
    public void Run_SubqueryColumnsRenamedToEachOthersNames_AreNotReadCrossed()
    {
        var plan = Subquery(Scan("t", "t.opt", "t.id"), "s.id", "s.opt") with { Output = ["s.id", "s.opt"] };

        AnswersOf(Run(plan, Optional, NotNull)).ShouldBe([NullableCatalog, NotNullCatalog]);
    }

    // a.id and a2.id are both a column of a: which one the subquery's column is cannot be told, and a2 is extended.
    [Fact]
    public void Run_SubqueryColumnThatTwoInstancesOfItsTableMayBe_KeepsTheHeuristic()
    {
        var child = Join("Left", Scan("a"), Scan("a2") with { RelationName = "a" }) with { Output = ["a.id", "a2.id"] };

        AnswersOf(Run(Subquery(child, "s.x") with { Output = ["s.x"] }, OfA)).ShouldBe([NullableHeuristic]);
    }

    // When every entry the column can be is of an extended instance, the column is the join's.
    [Fact]
    public void Run_SubqueryColumnOfAnExtendedInstance_IsTheOuterJoins()
    {
        var child = Join("Left", Scan("t"), Scan("b")) with { Output = ["t.id", "b.id", "b.opt"] };

        AnswersOf(Run(Subquery(child, "s.x") with { Output = ["s.x"] }, OfB)).ShouldBe([NullableOuterJoin]);
    }

    // A scan that a join above it extends extends every column of it.
    [Fact]
    public void Run_SubqueryScanThatAJoinExtends_IsTheOuterJoins()
    {
        var plan = Join("Left", Scan("a"), Subquery(Scan("t", "t.id"), "s.id")) with { Output = ["a.id", "s.id"] };

        AnswersOf(Run(plan, OfA, NotNull)).ShouldBe([NotNullCatalog, NullableOuterJoin]);
    }

    private static readonly Dictionary<string, string> UnresolvableChildrenRows = new(StringComparer.Ordinal)
    {
        // A LATERAL subquery's Output may name the query above, which is no instance of its plan.
        { "lateral-names-the-query-above", Text(Scan("t", "t.id", "u.id")) },
        // A bare name says nothing of which instance.
        { "bare-name", Text(Join("Inner", Scan("t"), Scan("b")) with { Output = ["t.id", "id"] }) },
        // A set operation prints its first branch.
        { "set-operation", Text(Over("Sort", Over("Append", Scan("t"), "Member")) with { Output = ["t.id"] }) },
        // No entry is of the origin's table.
        { "no-entry-of-the-origins-table", Text(Scan("b", "b.id")) },
        // No Output at all.
        { "no-output", Text(Scan("t")) },
    };

    public static TheoryData<string> UnresolvableChildren => [.. UnresolvableChildrenRows.Keys];

    [Theory]
    [MemberData(nameof(UnresolvableChildren))]
    public void Run_SubqueryColumnThatCannotBeResolved_KeepsTheHeuristic(string name) =>
        AnswersOf(
                Run(
                    Subquery(PlanNode.Read(UnresolvableChildrenRows[name]).ShouldNotBeNull(), "s.x") with
                    {
                        Output = ["s.x"],
                    },
                    NotNull
                )
            )
            .ShouldBe([NullableHeuristic]);

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

        AnswersOf(Run(plan, NotNull, OfB)).ShouldBe([NotNullCatalog, NullableOuterJoin]);
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

        AnswersOf(Run(plan, NotNull, Optional, NotNull)).ShouldBe([NotNullCatalog, NullableCatalog, NullableOuterJoin]);
    }

    // The relations scanned here, as "partitions" answers: t_1 and t_2, scanned as p_1 and p_2, are partitions of t.
    private static readonly Dictionary<PostgresRelationName, IReadOnlyList<long>> Partitions = new()
    {
        [new PostgresRelationName("public", "t_1")] = [Table.Oid],
        [new PostgresRelationName("public", "t_2")] = [Table.Oid],
    };

    private static PlanNode PartitionsOfT() =>
        Append(Member("p_1", "t_1", "p_1.id", "p_1.opt"), Member("p_2", "t_2", "p_2.id", "p_2.opt"));

    private static WalkOutcome RunWithPartitions(PlanNode root, params Asked[] asked)
    {
        var (columns, origins) = Built(asked);
        return NullabilityWalk.Run(new PlanInstancesCache(root, Partitions), columns, origins, Scope(true));
    }

    [Fact]
    public void Run_ColumnOfAPartitionedTable_IsAnsweredByItsSide()
    {
        var preserved = Join("Left", PartitionsOfT(), Scan("b")) with { Output = ["p.id", "p.opt", "b.id"] };
        var extended = Join("Left", Scan("b"), PartitionsOfT()) with { Output = ["p.id", "b.id"] };

        AnswersOf(RunWithPartitions(preserved, NotNull, Optional, OfB))
            .ShouldBe([NotNullCatalog, NullableCatalog, NullableOuterJoin]);
        AnswersOf(RunWithPartitions(extended, NotNull, OfB)).ShouldBe([NullableOuterJoin, NotNullCatalog]);
    }

    // Without the answer of "partitions" the Append is read as a set operation.
    [Fact]
    public void Run_PartitionedTableWithoutPartitions_KeepsTheHeuristic() =>
        AnswersOf(Run(Join("Left", PartitionsOfT(), Scan("b")) with { Output = ["p.id"] }, NotNull))
            .ShouldBe([NullableHeuristic]);

    // A partitioned table is no set operation: its expressions are read, and a reference to it is the table's.
    [Fact]
    public void Run_ExpressionOverAPartitionedTable_IsAnswered()
    {
        var plan = Over("Aggregate", PartitionsOfT()) with { Output = ["count(*)", "GREATEST(p.opt, p.id)"] };

        AnswersOf(RunWithPartitions(plan, Expression, Expression)).ShouldBe([NotNullExpression, NotNullExpression]);
    }

    // The partitions of t as "relations" answers them, by schema and name: like t, with a NOT NULL id.
    private static readonly PostgresTableRow PartitionOne = Table with { Oid = 16720, Name = "t_1" };

    private static readonly PostgresTableRow PartitionTwo = Table with { Oid = 16721, Name = "t_2" };

    // An expression of each kind over the partitioned table: a column's and a system column's.
    private static readonly string[] OverPartitions = ["(p.id = 0)", "(p.xmax = 0)"];

    // A walk of the expressions over t's partitions, with the rows the test gives in place of "tables" and "relations".
    private static WalkOutcome RunOverPartitions(params PostgresTableRow[] rows) =>
        RunOverPartitions(PartitionsOfT(), rows);

    private static WalkOutcome RunOverPartitions(PlanNode partitions, params PostgresTableRow[] rows)
    {
        var plan = Over("Aggregate", partitions) with { Output = OverPartitions };
        var (columns, origins) = Built(false, Expression, Expression);
        return NullabilityWalk.Run(
            new PlanInstancesCache(plan, Partitions),
            columns,
            origins,
            new WalkScope(false, false, true, PostgresTables.Of(rows), [])
        );
    }

    // "relations" asks for the relations the plan scans, which are the partitions and never their parent: with no row
    // of the parent, a reference is proved by every member's row.
    [Fact]
    public void Run_ExpressionOverAPartitionedTableWithoutItsRow_IsProvedByItsPartitions() =>
        AnswersOf(RunOverPartitions(PartitionOne, PartitionTwo)).ShouldBe([NotNullExpression, NotNullExpression]);

    // Every member must prove it: a member without a row, a foreign partition, whose remote rows nothing holds to its
    // NOT NULL, a partition whose column is nullable, or two partitions whose column is of two types each leave it.
    [Theory]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("nullable")]
    [InlineData("another-type")]
    public void Run_ExpressionOverPartitionsThatDoNotAllProveIt_KeepsNoOrigin(string reason)
    {
        var (second, answers) = reason switch
        {
            "missing" => ((PostgresTableRow?)null, new[] { UnknownNoOrigin, UnknownNoOrigin }),
            "foreign" => (PartitionTwo with { Relkind = "f" }, [UnknownNoOrigin, UnknownNoOrigin]),
            "nullable" => (
                PartitionTwo with
                {
                    Columns = [PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid), Table.Columns[1]],
                },
                [UnknownNoOrigin, NotNullExpression]
            ),
            _ => (
                PartitionTwo with
                {
                    Columns =
                    [
                        PostgresTables.Attribute(1, "id", PostgresRows.Int8.Oid, notNull: true),
                        Table.Columns[1],
                    ],
                },
                [UnknownNoOrigin, NotNullExpression]
            ),
        };

        AnswersOf(RunOverPartitions(second is null ? [PartitionOne] : [PartitionOne, second])).ShouldBe(answers);
    }

    // An Append that partition pruning removed a member of shows not every partition: its members' rows prove nothing.
    [Fact]
    public void Run_ExpressionOverPartitionsWithSubplansRemoved_KeepsNoOrigin() =>
        AnswersOf(RunOverPartitions(PartitionsOfT() with { SubplansRemoved = 1 }, PartitionOne, PartitionTwo))
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

    // The parent's own row, when a column names it, decides as it did: here it holds id nullable, which a partition
    // may make NOT NULL of its own, and the reference is not proved.
    [Fact]
    public void Run_ExpressionOverAPartitionedTableWithItsRow_IsDecidedByThatRow()
    {
        var parent = Table with
        {
            Relkind = "p",
            Columns = [PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid), Table.Columns[1]],
        };

        AnswersOf(RunOverPartitions(parent, PartitionOne, PartitionTwo)).ShouldBe([UnknownNoOrigin, NotNullExpression]);
    }

    // A foreign table's scan is an instance: unknown when nothing extends it, and the join's when one does.
    [Fact]
    public void Run_ColumnOfAForeignScan_IsForeignTableOrTheOuterJoins()
    {
        var foreign = Scan("ft") with { NodeType = "Foreign Scan" };

        AnswersOf(Run(Join("Left", foreign, Scan("b")) with { Output = ["ft.id"] }, OfForeign))
            .ShouldBe([UnknownForeignTable]);
        AnswersOf(Run(Join("Left", Scan("b"), foreign) with { Output = ["ft.id"] }, OfForeign))
            .ShouldBe([NullableOuterJoin]);
    }

    // A plan of one relation prints bare names at every node, here a Limit over a Sort over the scan.
    [Fact]
    public void Run_BareNameAtARootOverOneScan_IsThatRelationsColumn() =>
        AnswersOf(Run(Over("Limit", Over("Sort", Scan("t"))) with { Output = ["id", "opt"] }, NotNull, Optional))
            .ShouldBe([NotNullCatalog, NullableCatalog]);

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
            .ShouldBe([UnknownNoOrigin, NotNullExpression, NotNullExpression, NotNullExpression, UnknownNoOrigin]);
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

        AnswersOf(RunUngated(plan, Expression, Expression)).ShouldBe([UnknownNoOrigin, NotNullExpression]);
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

        AnswersOf(RunUngated(plan, OfA, Expression)).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
    }

    // A node that prints no Output may compute any value, so it must be preserved.
    [Fact]
    public void Run_ExpressionBesideAChildWithoutOutputThatIsExtended_KeepsNoOrigin() =>
        AnswersOf(RunUngated(Join("Left", Scan("a", "a.id"), Scan("b")) with { Output = ["'k'::text"] }, Expression))
            .ShouldBe([UnknownNoOrigin]);

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
            .ShouldBe([NotNullExpression, NotNullExpression]);

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
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin, NotNullExpression]);
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

        AnswersOf(RunUngated(plan, Expression, Expression)).ShouldBe([NotNullExpression, UnknownNoOrigin]);
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
            .ShouldBe([NullableHeuristic]);

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
            .ShouldBe([NotNullCatalog]);

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
            .ShouldBe([NotNullCatalog, UnknownNoOrigin, UnknownNoOrigin]);
    }
}
