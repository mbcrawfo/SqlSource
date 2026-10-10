using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

// How the tool asks MSBuild about a project, with a runner that answers in MSBuild's place.  FixtureProjectTests
// asks the real one.
public sealed class ProjectEvaluatorTests : IDisposable
{
    private static readonly string[] Evaluation =
    [
        "-nologo",
        "-getProperty:SqlSourceImported",
        "-getProperty:TargetFramework",
        "-getProperty:TargetFrameworks",
        "-getProperty:ProjectAssetsFile",
        "-getItem:PackageReference",
    ];

    private readonly TempFolder _folder = new();
    private readonly FakeProcessRunner _runner = new();
    private readonly Dictionary<string, string> _environment = [];
    private readonly string _project;
    private string _temp;
    private int _processors = 4;

    public ProjectEvaluatorTests()
    {
        _project = _folder.WriteFile("App/App.csproj");
        _temp = _folder.CreateFolder("tmp");
    }

    public void Dispose() => _folder.Dispose();

    private Task<System.Collections.Immutable.ImmutableArray<ProjectEvaluation>> EvaluateAsync(
        IReadOnlyList<string> projects,
        string? solution = null,
        CancellationToken? cancellationToken = null
    ) =>
        ProjectEvaluator.EvaluateAsync(
            Hosts.Create(_folder.Path, _runner, _temp, _processors, _environment),
            projects,
            solution,
            cancellationToken ?? TestContext.Current.CancellationToken
        );

    private async Task<ProjectEvaluation> EvaluateAsync(string? solution = null) =>
        (await EvaluateAsync([_project], solution)).ShouldHaveSingleItem();

    private string[] Projects(int count) =>
        [.. Enumerable.Range(0, count).Select(index => _folder.WriteFile($"P{index}/P{index}.csproj"))];

    [Fact]
    public async Task Evaluate_ProjectWithOneFramework_EvaluatesItAndRunsTheManifestTarget()
    {
        var evaluation = await EvaluateAsync();

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource);
        evaluation.ProjectPath.ShouldBe(_project);
        evaluation.Manifest.ShouldNotBeNull().ProjectPath.ShouldBe(_project);
        evaluation.Failure.ShouldBeNull();

        _runner.Requests.Count.ShouldBe(2);
        _runner.Requests[0].Arguments.ShouldBe(["msbuild", _project, .. Evaluation]);
        var target = _runner.Requests[1].Arguments;
        target.Length.ShouldBe(5);
        target.Take(4).ShouldBe(["msbuild", _project, "-nologo", "-t:SqlSourceWriteManifest"]);
        target[4].ShouldStartWith("-p:SqlSourceManifestFile=" + Path.Combine(_temp, "sqlsource-"));
        target[4].ShouldEndWith(Path.DirectorySeparatorChar + "0.manifest");
    }

    [Fact]
    public async Task Evaluate_ProjectWithSeveralFrameworks_AsksAgainWithTheFirstAndWritesItsManifest()
    {
        _runner.Default = new FakeProject { TargetFrameworks = " net8.0 ; net10.0" };

        var evaluation = await EvaluateAsync();

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource);
        evaluation.Manifest.ShouldNotBeNull().TargetFramework.ShouldBe("net8.0");
        _runner.Requests.Count.ShouldBe(3);
        _runner.Requests[0].Arguments.ShouldBe(["msbuild", _project, .. Evaluation]);
        _runner.Requests[1].Arguments.ShouldBe(["msbuild", _project, .. Evaluation, "-p:TargetFramework=net8.0"]);
        _runner.Requests[2].Arguments[3].ShouldBe("-t:SqlSourceWriteManifest");
        _runner.Requests[2].Arguments[5].ShouldBe("-p:TargetFramework=net8.0");
        _runner.Requests[2].Arguments.Length.ShouldBe(6);
    }

    // With no framework, NuGet's condition on TargetFramework keeps the package's props out, so the first
    // evaluation never has the marker.  The second is the one the table reads.
    [Fact]
    public async Task Evaluate_ProjectWithSeveralFrameworks_ReadsTheTableFromTheSecondEvaluation()
    {
        var assets = _folder.WriteFile("App/obj/project.assets.json");
        var project = new FakeProject { TargetFrameworks = "net8.0;net10.0", ProjectAssetsFile = assets };
        project.ImportedByFramework["net8.0"] = "";
        _runner.Default = project;

        var evaluation = await EvaluateAsync();

        // TD-0026: SqlSource for the second framework alone is no SqlSource.
        evaluation.State.ShouldBe(ProjectState.DoesNotUseSqlSource);
        _runner.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Evaluate_ProjectWithBothKindsOfFramework_KeepsTheOneItHas()
    {
        _runner.Default = new FakeProject { TargetFramework = "net9.0" };

        _ = await EvaluateAsync();

        _runner.Requests.Count.ShouldBe(2);
        _runner.Requests.ShouldAllBe(request => FakeProcessRunner.PropertyOf(request, "TargetFramework") == null);
    }

    [Fact]
    public async Task Evaluate_ProjectOfASolution_GivesBothRunsWhatABuildOfTheSolutionGives()
    {
        var solution = _folder.WriteFile("Acme, Inc;100%/App.slnx");
        var directory = Path.GetDirectoryName(solution) + Path.DirectorySeparatorChar;
        _runner.Default = new FakeProject { TargetFrameworks = "net8.0;net10.0" };

        var evaluation = await EvaluateAsync(solution);

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource);
        _runner.Requests.Count.ShouldBe(3);
        foreach (var request in _runner.Requests)
        {
            // The fake reads a switch as MSBuild does, and throws at a ; or a , that was not escaped.
            FakeProcessRunner.PropertyOf(request, "SolutionDir").ShouldBe(directory);
            FakeProcessRunner.PropertyOf(request, "SolutionPath").ShouldBe(solution);
            FakeProcessRunner.PropertyOf(request, "SolutionName").ShouldBe("App");
            FakeProcessRunner.PropertyOf(request, "SolutionFileName").ShouldBe("App.slnx");
            FakeProcessRunner.PropertyOf(request, "SolutionExt").ShouldBe(".slnx");
            request
                .Arguments.Single(argument => argument.StartsWith("-p:SolutionDir=", StringComparison.Ordinal))
                .ShouldEndWith("Acme%2C Inc%3B100%25" + Path.DirectorySeparatorChar);
        }
    }

    [Fact]
    public async Task Evaluate_ProjectAlone_GivesNoPropertyOfASolution()
    {
        _ = await EvaluateAsync();

        _runner.Requests.ShouldAllBe(request =>
            !request.Arguments.Any(argument => argument.StartsWith("-p:Solution", StringComparison.Ordinal))
        );
    }

    [Fact]
    public async Task Evaluate_TemporaryDirectoryWithCharactersThatMSBuildReads_NamesTheManifestEscaped()
    {
        _temp = _folder.CreateFolder("tmp;a,b%41");

        var evaluation = await EvaluateAsync();

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource);
        _runner.Requests[1].Arguments[4].ShouldContain("tmp%3Ba%2Cb%2541");
        FakeProcessRunner.PropertyOf(_runner.Requests[1], "SqlSourceManifestFile").ShouldStartWith(_temp);
    }

    // The table of the spec: the first row that holds.
    [Theory]
    [InlineData("true", "", "", nameof(ProjectState.UsesSqlSource))]
    [InlineData("True", "SqlSource", "missing", nameof(ProjectState.UsesSqlSource))]
    [InlineData("", "SqlSource", "exists", nameof(ProjectState.NotRestored))]
    [InlineData("", "Dapper;sqlsource", "", nameof(ProjectState.NotRestored))]
    [InlineData("false", "Dapper", "", nameof(ProjectState.DoesNotUseSqlSource))]
    [InlineData("", "", "missing", nameof(ProjectState.NotRestored))]
    [InlineData("", "SqlSource.Tool", "missing", nameof(ProjectState.NotRestored))]
    [InlineData("", "Dapper", "exists", nameof(ProjectState.DoesNotUseSqlSource))]
    [InlineData("", "SqlSource.Tool", "exists", nameof(ProjectState.DoesNotUseSqlSource))]
    [InlineData("", "", "relative", nameof(ProjectState.DoesNotUseSqlSource))]
    public async Task Evaluate_Project_IsReadByTheFirstRowOfTheTableThatHolds(
        string imported,
        string references,
        string assets,
        string state
    )
    {
        var expected = Enum.Parse<ProjectState>(state);
        var existing = _folder.WriteFile("App/obj/project.assets.json");
        var project = new FakeProject
        {
            Imported = imported,
            ProjectAssetsFile = assets switch
            {
                "exists" => existing,
                "relative" => "obj/project.assets.json",
                "missing" => _folder.PathOf("App/obj/none/project.assets.json"),
                _ => "",
            },
        };
        project.PackageReferences.AddRange(references.Split(';', StringSplitOptions.RemoveEmptyEntries));
        _runner.Default = project;

        var evaluation = await EvaluateAsync();

        evaluation.State.ShouldBe(expected);
        evaluation.Failure.ShouldBeNull();
        // The manifest target is run for a project that uses SqlSource, and for no other.
        _runner.Requests.Count.ShouldBe(expected == ProjectState.UsesSqlSource ? 2 : 1);
        (evaluation.Manifest is not null).ShouldBe(expected == ProjectState.UsesSqlSource);
    }

    private static void ShouldBeSqlsrc205(ProjectEvaluation evaluation, params (string Label, string Text)[] lines)
    {
        evaluation.State.ShouldBe(ProjectState.Failed);
        evaluation.Manifest.ShouldBeNull();
        var failure = evaluation.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC205");
        failure.Path.ShouldBe(evaluation.ProjectPath);
        failure.Position.ShouldBeNull();
        failure.Arguments.ShouldBe([evaluation.ProjectPath]);
        failure.Lines.ShouldBe(lines.Select(line => new ContinuationLine(line.Label, line.Text)));
    }

    [Fact]
    public async Task Evaluate_EvaluationThatFails_IsSqlsrc205WithMSBuildsErrorOutputFirst()
    {
        _runner.Default = new FakeProject
        {
            EvaluationExitCode = 1,
            Error = "App.csproj(3,5): error MSB4019: no such import\r\n\n   \n",
            Output = "\nBuild FAILED.  \n",
        };

        var evaluation = await EvaluateAsync();

        ShouldBeSqlsrc205(
            evaluation,
            ("reason", "dotnet msbuild ended with the exit code 1"),
            ("msbuild", "App.csproj(3,5): error MSB4019: no such import"),
            ("msbuild", "Build FAILED.")
        );
        _runner.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Evaluate_RunThatPrintsManyLines_ShowsTheFirstTwenty()
    {
        _runner.Default = new FakeProject
        {
            EvaluationExitCode = 1,
            Error = string.Join('\n', Enumerable.Range(1, 15).Select(number => $"error {number}")),
            Output = string.Join('\n', Enumerable.Range(1, 15).Select(number => $"line {number}")),
        };

        var lines = (await EvaluateAsync()).Failure.ShouldNotBeNull().Lines;

        lines.Count.ShouldBe(1 + ProjectEvaluator.MaxOutputLines);
        lines[1].ShouldBe(new ContinuationLine("msbuild", "error 1"));
        lines[15].ShouldBe(new ContinuationLine("msbuild", "error 15"));
        lines[^1].ShouldBe(new ContinuationLine("msbuild", "line 5"));
    }

    [Theory]
    [InlineData("MSBuild version 18.0.0\n")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData( /*lang=json,strict*/
        "{\"Properties\":{\"SqlSourceImported\":\"true\"}}"
    )]
    [InlineData(
        "{\"Properties\":{\"SqlSourceImported\":true,\"TargetFramework\":\"\",\"TargetFrameworks\":\"\","
            + "\"ProjectAssetsFile\":\"\"},\"Items\":{\"PackageReference\":[]}}"
    )]
    [InlineData(
        "{\"Properties\":{\"SqlSourceImported\":\"\",\"TargetFramework\":\"\",\"TargetFrameworks\":\"\","
            + "\"ProjectAssetsFile\":\"\"},\"Items\":{\"PackageReference\":[{\"Version\":\"1.0.0\"}]}}"
    )]
    public async Task Evaluate_OutputThatIsNotTheJsonExpected_IsSqlsrc205(string output)
    {
        _runner.Default = new FakeProject { EvaluationOutput = output };

        var failure = (await EvaluateAsync()).Failure.ShouldNotBeNull();

        failure.Descriptor.Id.ShouldBe("SQLSRC205");
        failure
            .Lines[0]
            .ShouldBe(new ContinuationLine("reason", "dotnet msbuild did not print the JSON of an evaluation"));
        _runner.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Evaluate_ManifestTargetThatFails_IsSqlsrc205WithItsOutput()
    {
        _runner.Default = new FakeProject
        {
            TargetExitCode = 1,
            Output = "App.csproj : error MSB4057: The target \"SqlSourceWriteManifest\" does not exist in the project.",
        };

        var evaluation = await EvaluateAsync();

        ShouldBeSqlsrc205(
            evaluation,
            ("reason", "dotnet msbuild ended with the exit code 1"),
            (
                "msbuild",
                "App.csproj : error MSB4057: The target \"SqlSourceWriteManifest\" does not exist in the project."
            )
        );
    }

    [Fact]
    public async Task Evaluate_ManifestTargetThatWritesNoFile_IsSqlsrc205()
    {
        _runner.Default = new FakeProject { WritesManifest = false, Output = "Build succeeded." };

        var evaluation = await EvaluateAsync();

        ShouldBeSqlsrc205(
            evaluation,
            ("reason", "the target SqlSourceWriteManifest left no file that can be read"),
            ("msbuild", "Build succeeded.")
        );
    }

    [Fact]
    public async Task Evaluate_DotnetThatCannotBeStarted_IsSqlsrc205WithTheReason()
    {
        _runner.Default = new FakeProject { EvaluationExitCode = -1, Error = "No such file or directory" };

        var evaluation = await EvaluateAsync();

        ShouldBeSqlsrc205(
            evaluation,
            ("reason", "dotnet could not be started"),
            ("msbuild", "No such file or directory")
        );
    }

    [Fact]
    public async Task Evaluate_ManifestThatCannotBeRead_IsSqlsrc206WithTheReason()
    {
        _runner.Default = new FakeProject { Manifest = "SqlSourceManifest=1\nCompile=/work/A.cs\n" };

        var evaluation = await EvaluateAsync();

        evaluation.State.ShouldBe(ProjectState.Failed);
        evaluation.Manifest.ShouldBeNull();
        var failure = evaluation.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC206");
        failure.Path.ShouldBe(_project);
        failure.Arguments.ShouldBe([_project, "it names no project"]);
        failure.Lines.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("/opt/dotnet/dotnet", "/opt/dotnet/dotnet")]
    [InlineData("", "dotnet")]
    [InlineData(null, "dotnet")]
    public async Task Evaluate_Project_RunsTheDotnetThatStartedTheToolOrTheOneOnThePath(
        string? hostPath,
        string expected
    )
    {
        if (hostPath is not null)
        {
            _environment["DOTNET_HOST_PATH"] = hostPath;
        }

        _ = await EvaluateAsync();

        _runner.Requests.Count.ShouldBe(2);
        _runner.Requests.ShouldAllBe(request => request.Program == expected);
    }

    [Fact]
    public async Task Evaluate_Project_RunsInTheProjectsFolderWithItsVariables()
    {
        _ = await EvaluateAsync();

        _runner.Requests.Count.ShouldBe(2);
        foreach (var request in _runner.Requests)
        {
            // The project's own global.json picks the SDK.
            request.WorkingDirectory.ShouldBe(Path.GetDirectoryName(_project));
            request
                .SetVariables.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + "=" + pair.Value)
                .ShouldBe(["DOTNET_CLI_FORCE_UTF8_ENCODING=true", "DOTNET_NOLOGO=true"]);
            request.RemovedVariables.ShouldBe(["MSBuildSDKsPath", "MSBuildExtensionsPath"]);
        }
    }

    [Fact]
    public async Task Evaluate_NoProjects_StartsNothingAndMakesNoFolder()
    {
        (await EvaluateAsync([])).ShouldBeEmpty();

        _runner.Requests.ShouldBeEmpty();
        Directory.EnumerateFileSystemEntries(_temp).ShouldBeEmpty();
    }

    [Fact]
    public async Task Evaluate_SeveralProjects_GivesThemInTheirOrderWhateverEndsFirst()
    {
        var projects = Projects(6);
        // The first project is the last to answer.
        _runner.BeforeAnswer = (request, token) =>
            Task.Delay(60 - (10 * Array.IndexOf(projects, request.Arguments[1])), token);

        var evaluations = await EvaluateAsync(projects);

        evaluations.Select(evaluation => evaluation.ProjectPath).ShouldBe(projects);
        evaluations.Select(evaluation => evaluation.Manifest.ShouldNotBeNull().ProjectPath).ShouldBe(projects);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(64, ProjectEvaluator.MaxAtOnce)]
    [InlineData(0, 1)]
    public async Task Evaluate_SeveralProjects_AsksAboutAsManyAtOnceAsTheMachineHasProcessorsAndEightAtMost(
        int processors,
        int expected
    )
    {
        _processors = processors;
        _runner.BeforeAnswer = (_, token) => Task.Delay(30, token);

        var evaluations = await EvaluateAsync(Projects(20));

        evaluations.ShouldAllBe(evaluation => evaluation.State == ProjectState.UsesSqlSource);
        _runner.MostAtOnce.ShouldBe(expected);
    }

    // Two runs at one time, as from two terminals, or from an editor and a terminal: each has a folder of its own.
    [Fact]
    public async Task Evaluate_TwoRunsAtOnceOnOneProject_DoNotShareAManifestFile()
    {
        _runner.BeforeAnswer = (_, token) => Task.Delay(20, token);

        var both = await Task.WhenAll(EvaluateAsync([_project]), EvaluateAsync([_project]));

        both.ShouldAllBe(evaluations => evaluations[0].State == ProjectState.UsesSqlSource);
        _runner
            .Requests.Where(FakeProcessRunner.IsManifestRun)
            .Select(request => FakeProcessRunner.PropertyOf(request, "SqlSourceManifestFile"))
            .Distinct()
            .Count()
            .ShouldBe(2);
        Directory.EnumerateFileSystemEntries(_temp).ShouldBeEmpty();
    }

    [Fact]
    public async Task Evaluate_AfterTheRun_TheTemporaryFolderIsGone()
    {
        _ = await EvaluateAsync();

        Directory.EnumerateFileSystemEntries(_temp).ShouldBeEmpty();
    }

    [Fact]
    public async Task Evaluate_AfterARunThatFailed_TheTemporaryFolderIsGone()
    {
        _runner.Default = new FakeProject { Manifest = "not a manifest" };

        (await EvaluateAsync()).State.ShouldBe(ProjectState.Failed);

        Directory.EnumerateFileSystemEntries(_temp).ShouldBeEmpty();
    }

    [Fact]
    public async Task Evaluate_Cancelled_StartsNoFurtherProcessAndLeavesNoFolder()
    {
        _processors = 1;
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _runner.BeforeAnswer = async (_, token) =>
        {
            await cancel.CancelAsync();
            await Task.Delay(Timeout.Infinite, token);
        };

        _ = await Should.ThrowAsync<OperationCanceledException>(EvaluateAsync(Projects(3), null, cancel.Token));

        _runner.Requests.Count.ShouldBe(1);
        Directory.EnumerateFileSystemEntries(_temp).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("net8.0;net10.0", "net8.0")]
    [InlineData(" ; net8.0 ;net10.0", "net8.0")]
    [InlineData("net10.0", "net10.0")]
    [InlineData("", null)]
    [InlineData(" ; ", null)]
    public void FirstFramework_List_IsItsFirstEntryThatIsNotEmpty(string targetFrameworks, string? expected) =>
        ProjectEvaluator.FirstFramework(targetFrameworks).ShouldBe(expected);
}
