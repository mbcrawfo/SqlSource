using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresWalkColumns;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// NullabilityWalk over a plan that cannot be used, and the shape of a column the walk has answered.
public sealed class NullabilityWalkUnusablePlanTests
{
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

    // The two "no-output" rows: a UNION's Append, which has no Output, and an Output with fewer entries than the result
    // has columns.
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
}
