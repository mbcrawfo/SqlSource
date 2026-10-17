using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Postgres.Walk;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Fixtures;

// The matrix of the nullability walk: what the expected.json of each walk-* case must say, written here by hand from
// "The matrix" of docs/superpowers/specs/2026-10-12-query-generation-phase-3-5-nullability-walk-design.md.  A
// recording that says otherwise is a bug of the walk or of the case, never a reason to change this table without
// knowing which.  DescriberFixtureTests replays the same cases, and tests/SqlSource.Tests.Postgres describes them on
// PostgreSQL 14 and 18.
public sealed class WalkMatrixTests
{
    // A NOT NULL column that nothing extends.
    private const string Kept = "false catalog";

    // A column that an outer join null-extends.
    private const string Joined = "true outer-join";

    // A column of a table whose entry of the plan cannot be mapped, or whose plan cannot be used.
    private const string Heuristic = "true heuristic";

    // A column of a view that nothing extends.
    private const string OfView = "null view";

    // An expression the plan does not answer.
    private const string NoOrigin = "null no-origin";

    // An expression the plan shows can never be NULL.
    private const string Expression = "false expression";

    private const string Walked = "walked";
    private const string Unavailable = "unavailable";

    private static readonly IReadOnlyList<FixtureCase> Found = FixtureCases.Of(SidecarValues.Engine.Postgres);

    private static readonly Dictionary<string, (string Plan, string[] Columns)> Rows = new(StringComparer.Ordinal)
    {
        ["walk-left"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-left-hash"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-left-merge"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-left-nestloop"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-right"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-right-hash"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-right-merge"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-right-nestloop"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-full"] = (Walked, [Joined, Joined]),
        ["walk-full-hash"] = (Walked, [Joined, Joined]),
        ["walk-full-merge"] = (Walked, [Joined, Joined]),
        ["walk-nested"] = (Walked, [Kept, Joined, Joined]),
        ["walk-chain"] = (Walked, [Kept, Joined, Joined]),
        ["walk-self-join"] = (Walked, [Kept, Kept, Joined, Joined]),
        ["walk-reduced-by-where"] = (Walked, [Kept, Kept]),
        ["walk-reduced-by-join"] = (Walked, [Kept, Kept, Kept]),
        ["walk-removed"] = (Walked, [Kept, Kept]),
        ["walk-subquery-flattened"] = (Walked, [Kept, Joined]),
        ["walk-subquery-kept"] = (Walked, [Kept, Joined, NoOrigin]),
        ["walk-cte-inlined"] = (Walked, [Kept, Joined]),
        ["walk-cte-materialized"] = (Walked, [Kept, Joined]),
        ["walk-lateral"] = (Walked, [Kept, Joined]),
        ["walk-view"] = (Walked, [OfView, OfView, Joined, Joined, Joined]),
        ["walk-view-nullable-side"] = (Walked, [Kept, Joined]),
        ["walk-using"] = (Walked, [NoOrigin, Joined, Joined]),
        ["walk-natural"] = (Walked, [NoOrigin, NoOrigin, Joined, Joined]),
        ["walk-aggregate"] = (Walked, [Kept, Joined, Expression]),
        ["walk-union"] = (Walked, [NoOrigin, NoOrigin]),
        ["walk-returning"] = (Walked, [Kept, Kept, Joined]),
        ["walk-proved-empty"] = (Walked, [Kept, Heuristic]),
        ["walk-quoted-alias"] = (Walked, [Kept, Joined]),
        ["walk-semi-anti"] = (Walked, [Kept, NoOrigin]),
        ["walk-anti"] = (Walked, [Kept, Joined, Joined]),
        ["walk-rollup"] = (Unavailable, [Heuristic, NoOrigin]),
        ["walk-cube"] = (Unavailable, [Heuristic, Heuristic, NoOrigin]),
        ["walk-grouping-sets"] = (Unavailable, [Heuristic, NoOrigin]),
        ["walk-order-by-unselected"] = (Walked, [Kept, Joined]),
    };

    public static TheoryData<string> Names => [.. Rows.Keys];

    private static FixtureCase CaseOf(string name) => Found.Single(found => found.Name == name);

    // The one call of "explain" a case's capture holds.
    private static CaptureCall ExplainOf(string name) =>
        CaptureJson
            .Read(CaseOf(name).Capture)
            .Value.ShouldNotBeNull()
            .Calls.Single(static call => call.Method == "explain");

    // The plan the server answered, as it wrote it.
    private static string PlanOf(string name) =>
        ExplainOf(name).Answer.GetProperty("plan").GetString().ShouldNotBeNull();

    [Theory]
    [MemberData(nameof(Names))]
    public void Expected_CaseOfTheMatrix_SaysThePlansValueAndEachColumnsAnswer(string name)
    {
        var (plan, columns) = Rows[name];
        using var sidecar = JsonDocument.Parse(CaseOf(name).Expected);
        var entry = sidecar.RootElement.GetProperty("queries").EnumerateObject().Single().Value;

        entry.GetProperty("plan").GetString().ShouldBe(plan);
        entry
            .GetProperty("columns")
            .EnumerateArray()
            .Select(static column =>
                $"{column.GetProperty("nullable").GetRawText()} {column.GetProperty("nullableSource").GetString()}"
            )
            .ShouldBe(columns);
    }

    // A recording that added or lost a case of the matrix does not pass silently.
    [Fact]
    public void Matrix_AndTheCasesNamedWalk_AreTheSame() =>
        Found
            .Select(static found => found.Name)
            .Where(static name => name.StartsWith("walk-", StringComparison.Ordinal))
            .ShouldBe(Rows.Keys, ignoreOrder: true);

    // A case named for a join method was planned with it: its request holds the settings that turned the other
    // two off, and the root of its plan is the join.
    [Theory]
    [InlineData("walk-left-hash", "Hash Join")]
    [InlineData("walk-left-merge", "Merge Join")]
    [InlineData("walk-left-nestloop", "Nested Loop")]
    [InlineData("walk-right-hash", "Hash Join")]
    [InlineData("walk-right-merge", "Merge Join")]
    [InlineData("walk-right-nestloop", "Nested Loop")]
    [InlineData("walk-full-hash", "Hash Join")]
    [InlineData("walk-full-merge", "Merge Join")]
    public void Capture_CaseNamedForAJoinMethod_WasPlannedWithIt(string name, string node)
    {
        var fixture = CaseOf(name);

        fixture.ExplainSettings.Count.ShouldBe(2);
        ExplainOf(name)
            .Request.GetProperty("settings")
            .EnumerateArray()
            .Select(static setting => setting.GetString())
            .ShouldBe(fixture.ExplainSettings);
        PlanNode.Read(PlanOf(name)).ShouldNotBeNull().NodeType.ShouldBe(node);
    }

    // The shapes whose plan is what the case is for.
    [Theory]
    [InlineData("walk-subquery-kept", "\"Node Type\": \"Subquery Scan\"")]
    [InlineData("walk-cte-materialized", "\"Node Type\": \"CTE Scan\"")]
    [InlineData("walk-union", "\"Node Type\": \"Append\"")]
    [InlineData("walk-returning", "\"Node Type\": \"ModifyTable\"")]
    [InlineData("walk-proved-empty", "\"Node Type\": \"Result\"")]
    [InlineData("walk-rollup", "\"Grouping Sets\"")]
    [InlineData("walk-cube", "\"Grouping Sets\"")]
    [InlineData("walk-grouping-sets", "\"Grouping Sets\"")]
    [InlineData("walk-full", "\"Join Type\": \"Full\"")]
    [InlineData("walk-anti", "Anti\"")]
    public void Capture_CaseOfAShape_HoldsTheShape(string name, string holds) => PlanOf(name).ShouldContain(holds);

    [Theory]
    [InlineData("walk-subquery-flattened", "Subquery Scan")]
    [InlineData("walk-cte-inlined", "CTE Scan")]
    [InlineData("walk-reduced-by-where", "\"Join Type\": \"Left\"")]
    [InlineData("walk-reduced-by-where", "\"Join Type\": \"Right\"")]
    [InlineData("walk-reduced-by-join", "\"Join Type\": \"Left\"")]
    [InlineData("walk-reduced-by-join", "\"Join Type\": \"Right\"")]
    [InlineData("walk-removed", "Join Type")]
    public void Capture_CaseThePlannerSimplified_HoldsNoTraceOfWhatItRemoved(string name, string lacks) =>
        PlanOf(name).ShouldNotContain(lacks);
}
