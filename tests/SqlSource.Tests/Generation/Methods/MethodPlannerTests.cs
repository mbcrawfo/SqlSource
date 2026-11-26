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

    // Both forms of one query taken, at its one place: each argument is compared apart from the next, so the name
    // GetUser comes before GetUserAsync, though the reason after it would sort the other way if the two ran together.
    [Fact]
    public void Distinct_BothFormsOfAQueryTaken_AreOrderedByNameBeforeReason()
    {
        var synchronous = Taken("GetUser", "the class that holds the method has that name");
        var asynchronous = Taken("GetUserAsync", "'App.Sample' has a member of that name");

        var distinct = MethodPlanner.Distinct([new EquatableArray<DiagnosticInfo>([asynchronous, synchronous])]);

        distinct.ShouldBe([synchronous, asynchronous]);
    }

    private static DiagnosticInfo Taken(string name, string reason) =>
        DiagnosticInfo.Create(SqlDiagnostics.MethodNameTaken, Location, "GetUser", name, reason);
}
