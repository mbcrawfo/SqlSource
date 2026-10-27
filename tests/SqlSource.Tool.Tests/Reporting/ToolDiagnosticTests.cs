using System.Linq;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests.Reporting;

public sealed class ToolDiagnosticTests
{
    [Fact]
    public void ForFile_PathAndTheRest_IsAtThePathAndQuotesItFirst()
    {
        var diagnostic = ToolDiagnostic.ForFile(ToolDiagnostics.FileCannotBeChanged, "/work/a.json", "written", "no");

        diagnostic.Path.ShouldBe("/work/a.json");
        diagnostic.Position.ShouldBeNull();
        diagnostic.Arguments.ToArray().ShouldBe(["/work/a.json", "written", "no"]);
    }

    [Fact]
    public void ForFile_PathAlone_QuotesThePath()
    {
        var diagnostic = ToolDiagnostic.ForFile(ToolDiagnostics.ProjectNotRestored, "/work/App.csproj");

        diagnostic.Arguments.ToArray().ShouldBe(["/work/App.csproj"]);
    }

    [Fact]
    public void AtFile_Arguments_IsAtThePathAndQuotesOnlyWhatItWasGiven()
    {
        var diagnostic = ToolDiagnostic.AtFile(
            ToolDiagnostics.SidecarOutOfDate,
            "/work/Users.sql.json",
            "/work/Users.sql",
            "an entry is missing"
        );

        diagnostic.Path.ShouldBe("/work/Users.sql.json");
        diagnostic.Position.ShouldBeNull();
        diagnostic.Arguments.ToArray().ShouldBe(["/work/Users.sql", "an entry is missing"]);
    }
}
