using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Tests.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests;

// The tool's runs of MSBuild on the projects of Fixtures/Projects, with the real "dotnet msbuild": what
// ProjectEvaluatorTests shows with a runner of its own, shown true of MSBuild.  Each test starts MSBuild once to
// three times.
public sealed class FixtureProjectTests : IDisposable
{
    private readonly FixtureProjects _fixtures = new();

    public void Dispose() => _fixtures.Dispose();

    // The temporary directory has a name that MSBuild would split, as a user's may.
    private async Task<ProjectEvaluation> EvaluateAsync(string project, string? solution = null)
    {
        var temp = Directory.CreateDirectory(_fixtures.PathOf("tmp ;,%41")).FullName;
        var evaluations = await ProjectEvaluator.EvaluateAsync(
            Hosts.Real(_fixtures.Root, temp),
            [project],
            solution,
            TestContext.Current.CancellationToken
        );
        Directory.EnumerateFileSystemEntries(temp).ShouldBeEmpty();
        return evaluations.ShouldHaveSingleItem();
    }

    private static string Failure(ProjectEvaluation evaluation) =>
        evaluation.Failure is { } failure ? string.Join('\n', failure.Lines.Select(line => line.Text)) : "";

    // An assets file, as a restore leaves one.  The tool looks for the file and does not read it.
    private static void Restore(string project) =>
        File.WriteAllText(
            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(project)!, "obj")).FullName
                + Path.DirectorySeparatorChar
                + "project.assets.json",
            "{}"
        );

    [Fact]
    public async Task Evaluate_ProjectWithOneFramework_GivesItsManifest()
    {
        var project = _fixtures.Copy("Single");
        var folder = Path.GetDirectoryName(project)!;

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource, Failure(evaluation));
        var manifest = evaluation.Manifest.ShouldNotBeNull();
        manifest.ProjectPath.ShouldBe(project);
        manifest.TargetFramework.ShouldBe("net10.0");
        manifest.LangVersion.ShouldBe("14.0");
        manifest.DefineConstants.ShouldContain("NET8_0_OR_GREATER");
        manifest.Properties.ShouldHaveSingleItem().ShouldBe(new("SqlSourceDialect", "postgres"));
        var file = manifest.Files.ShouldHaveSingleItem();
        file.Path.ShouldBe(Path.Combine(folder, "Queries", "Users.sql"));
        file.Metadata.ShouldHaveSingleItem().ShouldBe(new("SqlSourceDatabase", "billing"));
        manifest.CompileFiles.ShouldBe([Path.Combine(folder, "UserRepository.cs")]);
        // The project was never restored or built, and the tool made it neither.
        Directory.Exists(Path.Combine(folder, "obj")).ShouldBeFalse();
    }

    // Evaluated with no framework, this project has nothing of the package, as one that NuGet restored.
    [Fact]
    public async Task Evaluate_ProjectWithSeveralFrameworks_GivesTheManifestOfTheFirst()
    {
        var project = _fixtures.Copy("Multi");
        var folder = Path.GetDirectoryName(project)!;

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource, Failure(evaluation));
        var manifest = evaluation.Manifest.ShouldNotBeNull();
        manifest.TargetFramework.ShouldBe("net8.0");
        manifest.DefineConstants.ShouldContain("NET8_0");
        manifest.DefineConstants.ShouldNotContain("NET10_0");
        // TD-0026: Later/ is the second framework's alone.
        manifest.Files.Select(file => file.Path).ShouldBe([Path.Combine(folder, "Queries", "Users.sql")]);
        manifest.CompileFiles.ShouldBe([Path.Combine(folder, "Queries.cs")]);
        Directory.Exists(Path.Combine(folder, "obj")).ShouldBeFalse();
    }

    // TD-0026: the project uses SqlSource for its second framework, and the tool reads the first.
    [Fact]
    public async Task Evaluate_ProjectWithSqlSourceForItsSecondFrameworkAlone_DoesNotUseSqlSource()
    {
        var project = _fixtures.Copy("SecondOnly");
        Restore(project);

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.DoesNotUseSqlSource, Failure(evaluation));
    }

    [Fact]
    public async Task Evaluate_RestoredProjectWithoutThePackage_DoesNotUseSqlSource()
    {
        var project = _fixtures.Copy("Plain");
        Restore(project);

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.DoesNotUseSqlSource, Failure(evaluation));
    }

    [Fact]
    public async Task Evaluate_ProjectWithoutThePackageThatWasNeverRestored_IsNotRestored()
    {
        var project = _fixtures.Copy("Plain");

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.NotRestored, Failure(evaluation));
        Directory.Exists(Path.Combine(Path.GetDirectoryName(project)!, "obj")).ShouldBeFalse();
    }

    // The reference is an item of the evaluation, so it shows without a restore, whatever its case.
    [Fact]
    public async Task Evaluate_ProjectThatGainedThePackageAfterItsLastRestore_IsNotRestored()
    {
        var project = _fixtures.Copy("StaleRestore");
        Restore(project);

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.NotRestored, Failure(evaluation));
    }

    [Fact]
    public async Task Evaluate_ProjectOfASolution_EvaluatesAsInABuildOfTheSolution()
    {
        // The folder of the solution has a name that MSBuild would split.
        var solution = _fixtures.Copy("InSolution", "App.slnx", folder: "Acme, Inc;100%");
        var project = Path.Combine(Path.GetDirectoryName(solution)!, "App", "App.csproj");

        var evaluation = await EvaluateAsync(project, solution);

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource, Failure(evaluation));
        // Shared.props, which the project imports through SolutionDir, sets it.
        evaluation.Manifest.ShouldNotBeNull().Properties["SqlSourceDialect"].ShouldBe("mssql");
    }

    [Fact]
    public async Task Evaluate_ProjectOfASolutionAskedAboutAlone_IsSqlsrc205WithMSBuildsError()
    {
        var project = _fixtures.Copy("InSolution", "App/App.csproj");

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.Failed);
        var failure = evaluation.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC205");
        failure.Lines[0].Text.ShouldBe("dotnet msbuild ended with the exit code 1");
        Failure(evaluation).ShouldContain("Shared.props");
    }

    [Fact]
    public async Task Evaluate_PathsWithCharactersThatMSBuildReads_AreInTheManifestWhole()
    {
        const string Odd = "q;=%41 'é";
        var project = _fixtures.Copy("OddPaths");
        var folder = Path.Combine(Path.GetDirectoryName(project)!, Odd);

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource, Failure(evaluation));
        var manifest = evaluation.Manifest.ShouldNotBeNull();
        manifest.Files.ShouldHaveSingleItem().Path.ShouldBe(Path.Combine(folder, "a;b=c%41 'é.sql"));
        manifest.CompileFiles.ShouldBe([Path.Combine(folder, "a;b=c%41 'é.cs")]);
    }

    // MSBuild writes a warning of an evaluation to its error output, and the JSON alone to the other.
    [Fact]
    public async Task Evaluate_ProjectWhoseEvaluationWarns_IsReadAllTheSame()
    {
        // The second import of one file is the warning MSB4011.
        var project = _fixtures.WriteFile(
            "Warns/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
                <Import Project="../build/SqlSource.props" />
                <Import Project="../build/SqlSource.props" />
                <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                </PropertyGroup>
                <Import Project="../build/SqlSource.targets" />
            </Project>
            """
        );

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource, Failure(evaluation));
    }

    // MSBuild reads Directory.Build.rsp beside a project by itself.  What it holds applies to the tool's runs as to
    // a build, and a verbosity it asks for puts nothing before the JSON.
    [Fact]
    public async Task Evaluate_ProjectWithAResponseFile_IsReadWithWhatTheFileSets()
    {
        var project = _fixtures.Copy("Single");
        _ = _fixtures.WriteFile("Single/Directory.Build.rsp", "-v:diag\n-p:SqlSourceOutput=sql\n");

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource, Failure(evaluation));
        evaluation.Manifest.ShouldNotBeNull().Properties["SqlSourceOutput"].ShouldBe("sql");
    }

    // A solution may list a project that is not on the disk, as after a branch was switched.
    [Fact]
    public async Task Evaluate_ProjectFileThatDoesNotExist_IsSqlsrc205WithMSBuildsError()
    {
        var project = Path.Combine(Path.GetDirectoryName(_fixtures.Copy("Single"))!, "Gone.csproj");

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.Failed);
        evaluation.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC205");
        Failure(evaluation).ShouldContain("Gone.csproj");
    }

    // The repository's own test project of the generator, where it is: it takes the package's files by path, and
    // its end-to-end files have a dialect each way a file can be given one.
    [Fact]
    public async Task Evaluate_TestProjectOfTheGenerator_FindsItsFilesWithTheirDialects()
    {
        var folder = Path.Combine(RepositoryRoot(), "tests", "SqlSource.Tests");
        var project = Path.Combine(folder, "SqlSource.Tests.csproj");

        var evaluation = await EvaluateAsync(project);

        evaluation.State.ShouldBe(ProjectState.UsesSqlSource, Failure(evaluation));
        var manifest = evaluation.Manifest.ShouldNotBeNull();
        manifest.ProjectPath.ShouldBe(project);
        manifest.TargetFramework.ShouldBe("net10.0");
        manifest.DefineConstants.ShouldContain("NET10_0");
        manifest.Properties["SqlSourceDialect"].ShouldBe("postgres");
        manifest.Properties["SqlSourceGeneratorParameters"].ShouldBe("sort-input no-token-validation");

        string EndToEnd(params string[] parts) => Path.Combine([folder, "EndToEnd", .. parts]);
        var files = manifest.Files.ToDictionary(file => file.Path, file => file.Metadata);
        files[EndToEnd("Users.sql")].ShouldBeEmpty();
        files[EndToEnd("Dialects", "ByMetadata.sql")]["SqlSourceDialect"].ShouldBe("mysql");
        files[EndToEnd("Dialects", "ByOption.sql")]["SqlSourceDialect"].ShouldBe("mysql, no-backslash-escapes");
        files[EndToEnd("Parameters", "Kept.sql")]
            ["SqlSourceGeneratorParameters"]
            .ShouldBe("no-token-validation keep-comments");
        manifest.CompileFiles.ShouldContain(EndToEnd("UserQueries.cs"));
    }

    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "SqlSource.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException("The tests do not run from a folder of the repository.");
    }
}
