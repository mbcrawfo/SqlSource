using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresWalkColumns;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// NullabilityWalk over the plans of set operations a test writes: UNION, INTERSECT and EXCEPT as PostgreSQL 14 and 18
// plan them, each column's answer from its branches, and the plan's value.
public sealed class NullabilityWalkSetOperationTests
{
    // Every column of a set operation is of no origin, and its type is the row description's.  "u" and "o" are two
    // tables whose columns are of the types the type rule reads; "u_1" and "u_2" are partitions of a table like "u".
    private const long Int4Oid = BuiltInTypes.Int4;
    private const long Int8Oid = BuiltInTypes.Int8;
    private const long TextOid = BuiltInTypes.Text;
    private const long DomainOid = 16800;

    private static readonly PostgresTableRow SetU = PostgresTables.TableOf(
        16810,
        "u",
        PostgresTables.Attribute(1, "id", Int4Oid, notNull: true),
        PostgresTables.Attribute(2, "name", TextOid, notNull: true),
        PostgresTables.Attribute(3, "email", TextOid),
        PostgresTables.Attribute(4, "dom", DomainOid, notNull: true)
    );

    private static readonly PostgresTableRow SetO = PostgresTables.TableOf(
        16811,
        "o",
        PostgresTables.Attribute(1, "id", Int8Oid, notNull: true),
        PostgresTables.Attribute(2, "note", TextOid),
        PostgresTables.Attribute(3, "user_id", Int4Oid, notNull: true),
        PostgresTables.Attribute(4, "label", TextOid, notNull: true),
        PostgresTables.Attribute(5, "ref", Int4Oid)
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

    private static WalkOutcome RunSet(PlanNode root, params long[] types)
    {
        var (columns, origins) = Built(false, [.. types.Select(static _ => Expression)]);
        return NullabilityWalk.Run(
            new PlanInstancesCache(root, null),
            columns,
            origins,
            new WalkScope(false, false, true, SetTables, types)
        );
    }

    // An Append whose children are all its members.
    private static PlanNode AppendOf(params PlanNode[] members) => Append([.. members.Select(Member)]);

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
        var outcome = RunSet(
            AppendOf(Scan("u", "u.name", "u.id"), Scan("o", "o.label", "o.user_id")),
            TextOid,
            Int4Oid
        );

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        outcome.Reason.ShouldBeNull();
        AnswersOf(outcome).ShouldBe([NotNullSetOperation, NotNullSetOperation]);
    }

    // An Append that partition pruning removed a member of shows not every partition, and another execution may read
    // the removed one's rows.
    [Fact]
    public void Run_AppendWithSubplansRemoved_KeepsNoOrigin() =>
        AnswersOf(
                RunSet(AppendOf(Scan("u_1", "u_1.name"), Scan("u_2", "u_2.name")) with { SubplansRemoved = 1 }, TextOid)
            )
            .ShouldBe([UnknownNoOrigin]);

    // SELECT xmin FROM u UNION ALL SELECT xmin FROM o: a system column of a table is never NULL, and each branch's is
    // of the column's type.
    [Fact]
    public void Run_UnionAllOfASystemColumnOfEachBranch_IsFalseFromTheBranches() =>
        AnswersOf(RunSet(AppendOf(Scan("u", "u.xmin"), Scan("o", "o.xmin")), BuiltInTypes.Xid))
            .ShouldBe([NotNullSetOperation]);

    // A system column of a relation that "tables" and "relations" did not answer, or of the side a LEFT JOIN extends.
    [Fact]
    public void Run_UnionAllOfASystemColumnNotProvedInABranch_KeepsNoOrigin()
    {
        var extended = Join("Left", Scan("u", "u.id"), Scan("o", "o.xmin")) with { Output = ["o.xmin"] };

        AnswersOf(RunSet(AppendOf(Scan("u", "u.xmin"), Scan("w", "w.xmin")), BuiltInTypes.Xid))
            .ShouldBe([UnknownNoOrigin]);
        AnswersOf(RunSet(AppendOf(Scan("u", "u.xmin"), extended), BuiltInTypes.Xid)).ShouldBe([UnknownNoOrigin]);
    }

    private static readonly Dictionary<string, string> BranchesThatMayBeNullRows = new(StringComparer.Ordinal)
    {
        {
            "null-constant",
            Text(Scan("x", "NULL::text") with { NodeType = "Result", Alias = null, RelationName = null })
        },
        { "nullable-column", Text(Scan("o", "o.note")) },
        {
            "side-a-left-join-extends",
            Text(Join("Left", Scan("u", "u.id"), Scan("o", "o.label")) with { Output = ["o.label"] })
        },
        { "unanswered-relation", Text(Scan("w", "w.name")) },
        {
            "user-subquery-scan",
            Text(Over("Subquery Scan", Scan("u", "u.name"), "Subquery") with { Alias = "s", Output = ["s.name"] })
        },
    };

    public static TheoryData<string> BranchesThatMayBeNull => [.. BranchesThatMayBeNullRows.Keys];

    // A NULL, a nullable column, the side a LEFT JOIN extends, a relation that "tables" and "relations" did not
    // answer, and a user subquery's scan, which is mapped by no position.
    [Theory]
    [MemberData(nameof(BranchesThatMayBeNull))]
    public void Run_UnionAllWithABranchThatMayBeNull_KeepsNoOrigin(string name) =>
        AnswersOf(
                RunSet(
                    AppendOf(Scan("u", "u.name"), PlanNode.Read(BranchesThatMayBeNullRows[name]).ShouldNotBeNull()),
                    TextOid
                )
            )
            .ShouldBe([UnknownNoOrigin]);

    // The branch's cast to the result's type is not printed: an integer under a bigint is in the family, an integer
    // under a text and a domain under its base are not.
    [Theory]
    [InlineData("u.id", "o.id", Int8Oid, true)]
    [InlineData("u.id", "o.label", TextOid, false)]
    [InlineData("u.dom", "o.label", TextOid, false)]
    [InlineData("u.dom", "u_1.dom", DomainOid, true)]
    public void Run_BranchOfAnotherTypeThanTheResult_CountsWithinAFamily(
        string first,
        string second,
        long type,
        bool answered
    ) =>
        AnswersOf(RunSet(AppendOf(Scan("u", first), Scan(second.Split('.')[0], second)), type))
            .ShouldBe([answered ? NotNullSetOperation : UnknownNoOrigin]);

    // A constant the planner put in the leaf scan's place is a constant at any position.
    [Fact]
    public void Run_ConstantOfALeafScanUnderAnAppend_IsAnswered() =>
        AnswersOf(
                RunSet(
                    AppendOf(
                        Scan("u", "'a'::text", "u.name"),
                        Select(2, Scan("o", "o.label"), "'b'::text", "\"*SELECT* 2\".label")
                    ),
                    TextOid,
                    TextOid
                )
            )
            .ShouldBe([NotNullSetOperation, UnknownNoOrigin]);

    // An appendrel, a UNION ALL that a FROM subquery, a CTE or an inlined SQL function holds, prints a branch that the
    // planner keeps as a "*SELECT* <k>" scan in the order the outer query needs: SELECT s.b, s.a FROM (SELECT
    // p.sku AS a, p.name AS b ... UNION ALL (SELECT p2.sku, NULLIF(p2.name, 'x') ... LIMIT 5)) s.  Its column 0 is
    // NULLIF's, and by position it would be p2.sku.
    private static readonly Dictionary<string, string> AppendrelsRows = new(StringComparer.Ordinal)
    {
        {
            "nullif-against-a-constant",
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
            )
        },
        {
            "nullif-of-the-column-under-a-sort",
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
            )
        },
    };

    public static TheoryData<string> Appendrels => [.. AppendrelsRows.Keys];

    [Theory]
    [MemberData(nameof(Appendrels))]
    public void Run_LeafScanUnderAnAppend_IsNotMappedByPosition(string name) =>
        AnswersOf(RunSet(PlanNode.Read(AppendrelsRows[name]).ShouldNotBeNull(), TextOid, TextOid))
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

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
        AnswersOf(RunSet(CrossedExcept14("*SELECT* 1"), TextOid, TextOid))
            .ShouldBe([UnknownNoOrigin, NotNullSetOperation]);

    [Fact]
    public void Run_FlaggedSubqueryScanOfAnotherAlias_IsNotMappedByPosition() =>
        AnswersOf(RunSet(CrossedExcept14("s"), TextOid, TextOid)).ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

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

        AnswersOf(RunSet(CrossedExcept14("*SELECT* 1", user), TextOid, TextOid))
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);
    }

    // On 18 the planner drops its own leaf scan when it adds nothing, and an input may be a user's subquery of that
    // name: CREATE VIEW v AS SELECT "*SELECT* 9".b, "*SELECT* 9".a FROM (SELECT p.sku AS a, NULLIF(p.name, 'x') AS b
    // FROM products p OFFSET 0) AS "*SELECT* 9"; SELECT b, a FROM v EXCEPT SELECT 'zz', 'zz', whose b is NULL.
    private static void ShouldNotMapTheLeafAliasByPosition(string command, PlanNode other)
    {
        var user = Over("Subquery Scan", Scan("u", "u.name", "NULLIF(u.name, 'x'::text)"), "Subquery") with
        {
            Alias = "*SELECT* 9",
            Output = ["\"*SELECT* 9\".b", "\"*SELECT* 9\".a"],
        };

        AnswersOf(RunSet(Inputs18(command, user, other), TextOid, TextOid))
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);
    }

    // EXCEPT 'zz', 'zz'.
    [Fact]
    public void Run_SubqueryScanOfTheLeafAliasAsAnExceptInputOn18_IsNotMappedByPosition() =>
        ShouldNotMapTheLeafAliasByPosition(
            "Except",
            Scan("x", "'zz'::text", "'zz'::text") with
            {
                NodeType = "Result",
                Alias = null,
                RelationName = null,
            }
        );

    // INTERSECT a nullable column, since an input of constants would prove it.
    [Fact]
    public void Run_SubqueryScanOfTheLeafAliasAsAnIntersectInputOn18_IsNotMappedByPosition() =>
        ShouldNotMapTheLeafAliasByPosition("Intersect", Scan("o", "o.note", "o.note"));

    // UNION on 14 is a HashAggregate over the Append, which prints a value the branch computed in parentheses and its
    // cast to the result's type; on 18 a Unique over a Sort.
    [Fact]
    public void Run_UnionUnderAHashAggregate_IsFollowedThroughWhatItPrints()
    {
        var plan = Over("HashAggregate", AppendOf(Scan("u", "u.id", "'k'::text"), Scan("o", "o.id", "'j'::text"))) with
        {
            Output = ["((u.id)::bigint)", "('k'::text)"],
        };

        AnswersOf(RunSet(plan, Int8Oid, TextOid)).ShouldBe([NotNullSetOperation, NotNullSetOperation]);
    }

    [Fact]
    public void Run_UnionUnderAUniqueOverASort_IsAnswered()
    {
        var append = AppendOf(Scan("u", "u.name"), Scan("o", "o.label"));
        var plan = Over("Unique", Over("Sort", append) with { Output = ["u.name"] }) with { Output = ["u.name"] };

        AnswersOf(RunSet(plan, TextOid)).ShouldBe([NotNullSetOperation]);
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

        AnswersOf(RunSet(plan, TextOid, TextOid))
            .ShouldBe([
                firstAnswered ? NotNullSetOperation : UnknownNoOrigin,
                secondAnswered ? NotNullSetOperation : UnknownNoOrigin,
            ]);
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

    private static readonly Dictionary<string, (string Plan, bool Answered)> IntersectsRows = new(
        StringComparer.Ordinal
    )
    {
        {
            "pg14-intersect-right-first",
            (Text(Flagged14("Intersect", Scan("o", "o.note"), Scan("u", "u.name"), rightFirst: true)), true)
        },
        { "pg14-intersect-all", (Text(Flagged14("Intersect All", Scan("u", "u.name"), Scan("o", "o.note"))), true) },
        {
            "pg14-intersect-of-two-nullable-inputs",
            (Text(Flagged14("Intersect", Scan("o", "o.note"), Scan("u", "u.email"))), false)
        },
        { "pg18-intersect", (Text(Inputs18("Intersect", Scan("o", "o.note"), Scan("u", "u.name"))), true) },
        { "pg18-intersect-all", (Text(Inputs18("Intersect All", Scan("u", "u.name"), Scan("o", "o.note"))), true) },
        {
            "pg18-intersect-of-two-nullable-inputs",
            (Text(Inputs18("Intersect", Scan("o", "o.note"), Scan("u", "u.email"))), false)
        },
    };

    public static TheoryData<string> Intersects => [.. IntersectsRows.Keys];

    [Theory]
    [MemberData(nameof(Intersects))]
    public void Run_Intersect_IsFalseWhenEitherInputIs(string name)
    {
        var (plan, answered) = IntersectsRows[name];

        AnswersOf(RunSet(PlanNode.Read(plan).ShouldNotBeNull(), TextOid))
            .ShouldBe([answered ? NotNullSetOperation : UnknownNoOrigin]);
    }

    // EXCEPT returns rows of its left input alone: on 14 the member flagged 0, on 18 the Outer child.
    private static readonly Dictionary<string, (string Plan, bool Answered)> ExceptsRows = new(StringComparer.Ordinal)
    {
        { "pg14-except", (Text(Flagged14("Except", Scan("u", "u.name"), Scan("o", "o.note"))), true) },
        {
            "pg14-except-all-right-first",
            (Text(Flagged14("Except All", Scan("u", "u.name"), Scan("o", "o.note"), rightFirst: true)), true)
        },
        {
            "pg14-except-of-a-nullable-left",
            (Text(Flagged14("Except", Scan("o", "o.note"), Scan("u", "u.name"))), false)
        },
        {
            "pg14-except-of-a-nullable-left-right-first",
            (Text(Flagged14("Except", Scan("o", "o.note"), Scan("u", "u.name"), rightFirst: true)), false)
        },
        { "pg18-except", (Text(Inputs18("Except", Scan("u", "u.name"), Scan("o", "o.note"))), true) },
        {
            "pg18-except-all-of-a-nullable-left",
            (Text(Inputs18("Except All", Scan("o", "o.note"), Scan("u", "u.name"))), false)
        },
    };

    public static TheoryData<string> Excepts => [.. ExceptsRows.Keys];

    [Theory]
    [MemberData(nameof(Excepts))]
    public void Run_Except_IsItsLeftInputs(string name)
    {
        var (plan, answered) = ExceptsRows[name];

        AnswersOf(RunSet(PlanNode.Read(plan).ShouldNotBeNull(), TextOid))
            .ShouldBe([answered ? NotNullSetOperation : UnknownNoOrigin]);
    }

    private static readonly Dictionary<string, string> SetOpsOfAnotherShapeRows = new(StringComparer.Ordinal)
    {
        // Two members flagged 0, three members, and none flagged.
        {
            "two-members-flagged-0",
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
            )
        },
        {
            "three-members",
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
            )
        },
        {
            "no-member-flagged",
            Text(
                SetOp(
                    "Except",
                    AppendOf(Select(1, Scan("u", "u.name"), "\"*SELECT* 1\".c"), Scan("u_1", "u_1.name"))
                ) with
                {
                    Output = ["\"*SELECT* 1\".c"],
                }
            )
        },
        // A command the walk does not know, and none.
        { "unknown-command", Text(Inputs18("Merge", Scan("u", "u.name"), Scan("u_1", "u_1.name"))) },
        {
            "no-command",
            Text(Inputs18("Intersect", Scan("u", "u.name"), Scan("u_1", "u_1.name")) with { Command = null })
        },
        // A recursive CTE's union.
        {
            "recursive-union",
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
            )
        },
    };

    public static TheoryData<string> SetOpsOfAnotherShape => [.. SetOpsOfAnotherShapeRows.Keys];

    [Theory]
    [MemberData(nameof(SetOpsOfAnotherShape))]
    public void Run_SetOperationOfAnotherShape_KeepsNoOrigin(string name) =>
        AnswersOf(RunSet(PlanNode.Read(SetOpsOfAnotherShapeRows[name]).ShouldNotBeNull(), TextOid))
            .ShouldBe([UnknownNoOrigin]);

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
        AnswersOf(RunSet(NestedExcept14(innerRight), Int4Oid))
            .ShouldBe([answered ? NotNullSetOperation : UnknownNoOrigin]);
        AnswersOf(RunSet(NestedExcept18(innerRight), Int4Oid))
            .ShouldBe([answered ? NotNullSetOperation : UnknownNoOrigin]);
    }

    // An INTERSECT that is a branch of a UNION ALL: its one input that is never NULL says nothing, since its type
    // may be the other's.
    [Fact]
    public void Run_NestedIntersectWithAnInputThatMayBeNull_KeepsNoOrigin() =>
        AnswersOf(
                RunSet(
                    AppendOf(Inputs18("Intersect", Scan("u", "u.id"), Scan("o", "o.ref")), Scan("u_1", "u_1.id")),
                    Int4Oid
                )
            )
            .ShouldBe([UnknownNoOrigin]);

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

        AnswersOf(RunSet(AppendOf(Scan("o", "o.label"), partitions), TextOid)).ShouldBe([NotNullSetOperation]);
        AnswersOf(RunSet(AppendOf(), TextOid)).ShouldBe([UnknownNoOrigin]);
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
            new PlanInstancesCache(root, answered ? partitions : null),
            columns,
            origins,
            new WalkScope(false, false, true, SetTables, [Int4Oid, TextOid])
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
                    TextOid
                )
            )
            .ShouldBe([UnknownNoOrigin]);

    [Fact]
    public void Run_SetOperationUnderGroupingSets_KeepsNoOrigin() =>
        AnswersOf(
                RunSet(
                    Over("Aggregate", AppendOf(Scan("u", "u.name"), Scan("o", "o.label"))) with
                    {
                        HasGroupingSets = true,
                        Output = ["u.name"],
                    },
                    TextOid
                )
            )
            .ShouldBe([UnknownNoOrigin]);

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
                    TextOid
                )
            )
            .ShouldBe([UnknownNoOrigin]);

    // Without the row description's types, an empty list, the walk answers no set operation.
    [Fact]
    public void Run_SetOperationWithoutTheColumnsTypes_IsUnavailable()
    {
        var (columns, origins) = Built(false, Expression);

        var outcome = NullabilityWalk.Run(
            new PlanInstancesCache(AppendOf(Scan("u", "u.name"), Scan("o", "o.label")), null),
            columns,
            origins,
            new WalkScope(false, false, true, SetTables, [])
        );

        outcome.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        AnswersOf(outcome).ShouldBe([UnknownNoOrigin]);
    }

    // A column with an origin beside the set operation is kept, for a root without Output.
    [Fact]
    public void Run_ColumnWithAnOriginBesideASetOperation_IsKept()
    {
        var (columns, origins) = Built(true, NotNull, Expression);

        var outcome = NullabilityWalk.Run(
            new PlanInstancesCache(AppendOf(Scan("u", "u.id", "u.name"), Scan("o", "o.user_id", "o.label")), null),
            columns,
            origins,
            new WalkScope(true, false, true, SetTables, [Int4Oid, TextOid])
        );

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(outcome).ShouldBe([NullableHeuristic, NotNullSetOperation]);
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
}
