using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;
using Xunit;

namespace SqlSource.Repository.Tests;

// CI runs each test project in a job of build.yml that names it, not through `dotnet test --solution`, so a test
// project added to the solution runs in CI only once a job names it.  These tests fail until one does, or until the
// project is named here as one that only nightly.yml runs.
public sealed class CiTestProjectsTests
{
    private static readonly string[] NightlyOnly =
    [
        "tests/SqlSource.Tests.RoslynFloor",
        "tests/DatabaseTests/QueryMethodAllocation",
        "tests/DatabaseTests/Postgres.SyncAsync",
        "tests/DatabaseTests/SqlServer.SyncAsync",
    ];

    private static string BuildYml => File.ReadAllText(RepositoryRoot.Path(".github", "workflows", "build.yml"));

    private static string NightlyYml => File.ReadAllText(RepositoryRoot.Path(".github", "workflows", "nightly.yml"));

    [Fact]
    public void TestProjects_Solution_AreTheElevenTestProjects()
    {
        var projects = TestProjects();

        projects.Count.ShouldBe(11);
        projects.ShouldContain("tests/SqlSource.Tests");
        projects.ShouldNotContain("tests/DatabaseTests/TestHarness");
    }

    [Fact]
    public void Unrun_EveryProjectButTheNightlyOnesInTheBuildWorkflow_IsEmpty() =>
        Unrun(TestProjects().Except(NightlyOnly), BuildYml).ShouldBeEmpty();

    [Fact]
    public void Unrun_NightlyOnlyProjects_AreAllUnrunByBuildAndNoneByNightly()
    {
        Unrun(NightlyOnly, BuildYml).ShouldBe(NightlyOnly);
        Unrun(NightlyOnly, NightlyYml).ShouldBeEmpty();
    }

    [Fact]
    public void Unrun_ProjectNamedOnlyInAComment_IsUnrun() =>
        Unrun(["tests/A"], "  # runs tests/A\nrun: true").ShouldBe(["tests/A"]);

    [Fact]
    public void Unrun_ProjectWhosePathStartsAnother_IsUnrunWhenOnlyTheOtherIsNamed() =>
        Unrun(["tests/DatabaseTests/Postgres"], "run: dotnet test tests/DatabaseTests/Postgres.Npgsql8")
            .ShouldBe(["tests/DatabaseTests/Postgres"]);

    [Fact]
    public void Unrun_ProjectNamedOnlyInACachePath_IsUnrun() =>
        Unrun(["tests/A"], "cache-dependency-path: tests/A/packages.lock.json").ShouldBe(["tests/A"]);

    [Fact]
    public void Unrun_ProjectsNamedInALoop_AreRun() =>
        Unrun(["tests/A", "tests/B"], "for project in tests/A \\\n    tests/B; do").ShouldBeEmpty();

    // The folder of each project of the solution under tests/ that references xunit.v3: the libraries the test
    // projects share reference only its parts.
    private static List<string> TestProjects() =>
        [
            .. XDocument
                .Load(RepositoryRoot.Path("SqlSource.slnx"))
                .Descendants("Project")
                .Select(project => (string)project.Attribute("Path")!)
                .Where(path => path.StartsWith("tests/", StringComparison.Ordinal))
                .Where(path =>
                    XDocument
                        .Load(RepositoryRoot.Path(path))
                        .Descendants()
                        .Any(item =>
                            item.Name.LocalName == "PackageReference"
                            && (string?)item.Attribute("Include") == "xunit.v3"
                        )
                )
                .Select(path => path[..path.LastIndexOf('/')]),
        ];

    // The projects that the workflow does not name outside its comment lines.  A path names a project only where no
    // character of a path stands before or after it, so tests/A/packages.lock.json does not name tests/A, nor
    // tests/A.B.
    private static List<string> Unrun(IEnumerable<string> projects, string workflow)
    {
        var text = string.Join('\n', workflow.Split('\n').Where(line => !line.TrimStart().StartsWith('#')));

        return [.. projects.Where(project => !Regex.IsMatch(text, $@"(?<![\w./-]){Regex.Escape(project)}(?![\w./-])"))];
    }
}
