using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation.Methods;
using Xunit;

namespace SqlSource.Tests.Generation.Methods;

public sealed class MethodPlannerTests
{
    private static readonly LocationInfo Location = new(
        "/app/Repo/Users.sql",
        new TextSpan(0, 3),
        new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 3))
    );

    // Two problems at one place with one id are ordered by their arguments, each kept apart from the next: "GetA" and
    // "Sync" run together as "Get" and "ASync" do, but come after them.
    [Fact]
    public void Distinct_ProblemsAtOnePlaceOfOneId_AreOrderedByTheirArgumentsApart()
    {
        var first = Taken("GetA", "Sync");
        var second = Taken("Get", "ASync");

        var distinct = MethodPlanner.Distinct([new EquatableArray<DiagnosticInfo>([first, second])]);

        distinct.ShouldBe([second, first]);
    }

    private static DiagnosticInfo Taken(string query, string name) =>
        DiagnosticInfo.Create(
            SqlDiagnostics.MethodNameTaken,
            Location,
            query,
            name,
            "'App.Sample' has a member of that name"
        );
}
