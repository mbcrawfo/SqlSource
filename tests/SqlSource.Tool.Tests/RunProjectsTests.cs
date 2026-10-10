using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

// Which projects a run is on, and what it says of the ones it cannot read, with a runner that answers in MSBuild's
// place.
public sealed class RunProjectsTests : IDisposable
{
    private const string See = "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc";

    private readonly TempFolder _folder = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly StringWriter _error = new(CultureInfo.InvariantCulture) { NewLine = "\n" };
    private readonly string _solution;
    private readonly string _a;
    private readonly string _b;
    private readonly string _c;

    public RunProjectsTests()
    {
        _a = _folder.WriteFile("A/A.csproj");
        _b = _folder.WriteFile("B/B.csproj");
        _c = _folder.WriteFile("C/C.csproj");
        // Not in the order of their paths, and with a project of another language.
        _solution = _folder.WriteFile(
            "App.slnx",
            """
            <Solution>
                <Project Path="C/C.csproj" />
                <Project Path="A/A.csproj" />
                <Project Path="F/F.fsproj" />
                <Project Path="B/B.csproj" />
            </Solution>
            """
        );
    }

    public void Dispose()
    {
        _folder.Dispose();
        _error.Dispose();
    }

    private Task<ImmutableArray<ProjectManifest>> FindAsync(RunUnit unit, params string[] named) =>
        RunProjects.FindAsync(
            unit,
            named,
            Hosts.Create(_folder.Path, _runner, _folder.CreateFolder("tmp")),
            new Reporter(_error),
            TestContext.Current.CancellationToken
        );

    private Task<ImmutableArray<ProjectManifest>> FindInSolutionAsync(params string[] named) =>
        FindAsync(new RunUnit(RunUnitKind.Solution, _solution), named);

    private Task<ImmutableArray<ProjectManifest>> FindInProjectAsync(params string[] named) =>
        FindAsync(new RunUnit(RunUnitKind.Project, _a), named);

    // The projects MSBuild was asked about, each once, in the order of the first question.
    private IEnumerable<string> Asked() => _runner.Requests.Select(request => request.Arguments[1]).Distinct();

    private FakeProject DoesNotUse(string project)
    {
        var assets = Path.Combine(Path.GetDirectoryName(project)!, "obj", "project.assets.json");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(assets)!);
        File.WriteAllText(assets, "{}");
        return _runner.Projects[project] = new FakeProject { Imported = "", ProjectAssetsFile = assets };
    }

    private FakeProject NotRestored(string project) =>
        _runner.Projects[project] = new FakeProject
        {
            Imported = "",
            ProjectAssetsFile = Path.Combine(Path.GetDirectoryName(project)!, "obj", "project.assets.json"),
        };

    private static string Sqlsrc204(string project) =>
        $"{project} : error SQLSRC204: '{project}' does not use SqlSource\n"
        + "    help: add the SqlSource package to the project\n"
        + See
        + "204\n";

    private static string Sqlsrc220(string project) =>
        $"{project} : error SQLSRC220: '{project}' has not been restored, or not since the SqlSource package was "
        + "added to it\n"
        + "    help: run 'dotnet restore'\n"
        + See
        + "220\n";

    private string Sqlsrc207(string path, string? unit = null) =>
        $"sqlsource : error SQLSRC207: '{path}' is not a project of '{unit ?? _solution}'\n" + See + "207\n";

    [Fact]
    public async Task Find_ProjectThatUsesSqlSource_GivesItsManifest()
    {
        var manifests = await FindInProjectAsync();

        manifests.ShouldHaveSingleItem().ProjectPath.ShouldBe(_a);
        _error.ToString().ShouldBeEmpty();
        // A project alone is given nothing of a solution.
        _runner.Requests.ShouldAllBe(request => FakeProcessRunner.PropertyOf(request, "SolutionDir") == null);
    }

    [Fact]
    public async Task Find_ProjectThatDoesNotUseSqlSource_IsSqlsrc204()
    {
        _ = DoesNotUse(_a);

        (await FindInProjectAsync()).ShouldBeEmpty();

        _error.ToString().ShouldBe(Sqlsrc204(_a));
    }

    [Fact]
    public async Task Find_ProjectThatWasNotRestored_IsSqlsrc220()
    {
        _ = NotRestored(_a);

        (await FindInProjectAsync()).ShouldBeEmpty();

        _error.ToString().ShouldBe(Sqlsrc220(_a));
    }

    [Fact]
    public async Task Find_Solution_GivesTheManifestsOfItsCSharpProjectsInOrdinalOrder()
    {
        var manifests = await FindInSolutionAsync();

        manifests.Select(manifest => manifest.ProjectPath).ShouldBe([_a, _b, _c]);
        _error.ToString().ShouldBeEmpty();
        Asked().ShouldBe([_a, _b, _c], ignoreOrder: true);
        _runner.Requests.ShouldAllBe(request => FakeProcessRunner.PropertyOf(request, "SolutionPath") == _solution);
    }

    [Fact]
    public async Task Find_SolutionWithAProjectThatDoesNotUseSqlSource_LeavesItOutAndSaysNothing()
    {
        _ = DoesNotUse(_b);

        var manifests = await FindInSolutionAsync();

        manifests.Select(manifest => manifest.ProjectPath).ShouldBe([_a, _c]);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Find_SolutionWhereNoProjectUsesSqlSource_GivesNoneAndSaysNothing()
    {
        _ = DoesNotUse(_a);
        _ = DoesNotUse(_b);
        _ = DoesNotUse(_c);

        (await FindInSolutionAsync()).ShouldBeEmpty();

        _error.ToString().ShouldBeEmpty();
    }

    // A fresh checkout: nothing was restored, so no project has the package's props.  The run must not pass.
    [Fact]
    public async Task Find_SolutionWithAProjectThatWasNotRestored_IsSqlsrc220AndReadsTheRest()
    {
        _ = NotRestored(_b);

        var manifests = await FindInSolutionAsync();

        manifests.Select(manifest => manifest.ProjectPath).ShouldBe([_a, _c]);
        _error.ToString().ShouldBe(Sqlsrc220(_b));
    }

    [Fact]
    public async Task Find_SolutionWithProjectsThatFail_ReportsThemInTheOrderOfTheProjects()
    {
        _runner.Projects[_a] = new FakeProject { EvaluationExitCode = 1, Error = "error of A" };
        _runner.Projects[_c] = new FakeProject { Manifest = "SqlSourceManifest=7\n" };
        // A answers last.
        _runner.BeforeAnswer = (request, token) => Task.Delay(request.Arguments[1] == _a ? 80 : 0, token);

        var manifests = await FindInSolutionAsync();

        manifests.ShouldHaveSingleItem().ProjectPath.ShouldBe(_b);
        var errors = _error.ToString();
        errors.ShouldStartWith(
            $"{_a} : error SQLSRC205: MSBuild could not evaluate '{_a}'\n"
                + "    reason: dotnet msbuild ended with the exit code 1\n"
                + "    msbuild: error of A\n"
                + See
                + "205\n"
                + $"{_c} : error SQLSRC206: The project manifest of '{_c}' cannot be read: it has version '7'"
        );
        errors.ShouldEndWith(See + "206\n");
    }

    [Fact]
    public async Task Find_OneProjectNamed_IsTheOnlyOneAskedAbout()
    {
        var manifests = await FindInSolutionAsync(_b);

        manifests.ShouldHaveSingleItem().ProjectPath.ShouldBe(_b);
        Asked().ShouldBe([_b]);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Find_TwoProjectsNamed_AreGivenInTheOrderOfTheSolution()
    {
        // A relative path is resolved against the working directory, and a path is compared ignoring case.
        var manifests = await FindInSolutionAsync(Path.Combine("C", "C.csproj"), _a.ToUpperInvariant());

        manifests.Select(manifest => manifest.ProjectPath).ShouldBe([_a, _c]);
        Asked().ShouldBe([_a, _c], ignoreOrder: true);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Find_ProjectNamedTwice_IsAskedAboutOnce()
    {
        var manifests = await FindInSolutionAsync(_b, Path.Combine("B", "B.csproj"), _b);

        manifests.ShouldHaveSingleItem().ProjectPath.ShouldBe(_b);
        _runner.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Find_NamedPathOutsideTheSolution_IsSqlsrc207AndTheRestIsRead()
    {
        var outside = _folder.WriteFile("Other/Other.csproj");

        var manifests = await FindInSolutionAsync(Path.Combine("Other", "Other.csproj"), _b, outside);

        manifests.ShouldHaveSingleItem().ProjectPath.ShouldBe(_b);
        Asked().ShouldBe([_b]);
        // Once, though it was named twice.
        _error.ToString().ShouldBe(Sqlsrc207(outside));
    }

    [Theory]
    [InlineData("F/F.fsproj")]
    [InlineData("App.slnx")]
    [InlineData("A")]
    public async Task Find_NamedPathThatIsNoCSharpProjectOfTheSolution_IsSqlsrc207(string path)
    {
        (await FindInSolutionAsync(path)).ShouldBeEmpty();

        _runner.Requests.ShouldBeEmpty();
        _error.ToString().ShouldBe(Sqlsrc207(_folder.PathOf(path)));
    }

    // What --project "$UNSET" gives.
    [Fact]
    public async Task Find_EmptyNamedPath_IsSqlsrc207AndNotTheWorkingDirectory()
    {
        (await FindInSolutionAsync("")).ShouldBeEmpty();

        _runner.Requests.ShouldBeEmpty();
        _error.ToString().ShouldBe(Sqlsrc207(""));
    }

    [Fact]
    public async Task Find_NamedProjectThatDoesNotUseSqlSource_IsSqlsrc204()
    {
        _ = DoesNotUse(_a);
        _ = DoesNotUse(_b);

        var manifests = await FindInSolutionAsync(_b, _c);

        manifests.ShouldHaveSingleItem().ProjectPath.ShouldBe(_c);
        // B was named.  A was not, and is not asked about.
        _error.ToString().ShouldBe(Sqlsrc204(_b));
        Asked().ShouldBe([_b, _c], ignoreOrder: true);
    }

    [Fact]
    public async Task Find_NamedProjectThatWasNotRestored_IsSqlsrc220()
    {
        _ = NotRestored(_b);

        (await FindInSolutionAsync(_b)).ShouldBeEmpty();

        _error.ToString().ShouldBe(Sqlsrc220(_b));
    }

    [Fact]
    public async Task Find_ProjectAsTheUnitAndNamed_IsThatProject()
    {
        var manifests = await FindInProjectAsync(Path.Combine("A", "A.csproj"));

        manifests.ShouldHaveSingleItem().ProjectPath.ShouldBe(_a);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task Find_ProjectAsTheUnitAndAnotherNamed_IsSqlsrc207AndNothingIsRead()
    {
        (await FindInProjectAsync(_b)).ShouldBeEmpty();

        _runner.Requests.ShouldBeEmpty();
        _error.ToString().ShouldBe(Sqlsrc207(_b, _a));
    }

    [Fact]
    public async Task Find_SolutionThatCannotBeRead_IsSqlsrc223AndNothingIsRead()
    {
        var solution = _folder.WriteFile("Broken.sln", "this is not a solution\n");

        (await FindAsync(new RunUnit(RunUnitKind.Solution, solution), _a)).ShouldBeEmpty();

        _runner.Requests.ShouldBeEmpty();
        var lines = _error.ToString().Split('\n');
        lines.Length.ShouldBe(3, _error.ToString());
        // The reason is the library's own text.
        lines[0].ShouldStartWith($"{solution} : error SQLSRC223: '{solution}' cannot be read: ");
        lines[0].Length.ShouldBeGreaterThan($"{solution} : error SQLSRC223: '{solution}' cannot be read: ".Length);
        lines[1].ShouldBe(See + "223");
    }

    [Fact]
    public async Task Find_SolutionWithNoCSharpProject_GivesNoneAndStartsNothing()
    {
        var solution = _folder.WriteFile("Empty.slnx", "<Solution><Project Path=\"F/F.fsproj\" /></Solution>");

        (await FindAsync(new RunUnit(RunUnitKind.Solution, solution))).ShouldBeEmpty();

        _runner.Requests.ShouldBeEmpty();
        _error.ToString().ShouldBeEmpty();
    }
}
