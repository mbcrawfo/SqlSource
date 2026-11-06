using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.DotEnv;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Projects;

// The online build of build/SqlSource.targets, run for real: "dotnet build" of a copy of the fixture Online, one
// framework with one .sql file and its sidecar, with the stand-in of the tool as its command unless a test says
// otherwise.  Each test restores and builds the copy one to three times, a second or two each.
public sealed class OnlineBuildTests : IDisposable
{
    private const string ManifestSuffix = ".SqlSource.manifest";
    private const string StampSuffix = ".SqlSource.stamp";

    // The files of the online build under obj: the manifest, the stamp, the list of sidecars and the value file.
    private static readonly string[] OnlineFileSuffixes =
    [
        ManifestSuffix,
        StampSuffix,
        ".SqlSource.sidecars",
        ".SqlSource.online",
    ];

    private const string DotEnvText = "SQLSOURCE_CONNECTION=Host=localhost\n";

    private static readonly Dictionary<string, string> NoVariables = [];

    private static readonly Dictionary<string, string> Connection = new()
    {
        ["SQLSOURCE_CONNECTION"] = "Host=localhost;Database=billing",
    };

    // The items each project shape adds to the project file, before the import of the targets.
    private static readonly Dictionary<string, string> ItemsOfShape = new()
    {
        ["the project's folder"] = "",
        // The manual's example of a project that lists its own files.
        ["SqlSourceIncludeFiles false"] = """
            <PropertyGroup>
                <SqlSourceIncludeFiles>false</SqlSourceIncludeFiles>
            </PropertyGroup>
            <ItemGroup>
                <AdditionalFiles Include="Queries/**/*.sql" />
                <AdditionalFiles Include="Queries/**/*.sql.json" />
            </ItemGroup>
            """,
        // A .sql file linked from a folder beside the project's, listed as the manual says.
        ["a linked file"] = """
            <ItemGroup>
                <AdditionalFiles Include="../Shared/*.sql;../Shared/*.sql.json" />
            </ItemGroup>
            """,
        // A .sql file the project lists again, beside the props' pattern.
        ["a file listed twice"] = """
            <ItemGroup>
                <AdditionalFiles Include="Queries/Users.sql" />
            </ItemGroup>
            """,
        // A project that lists its own files, its sidecar by name, which an item names before the file exists.
        ["the sidecar listed by name"] = """
            <PropertyGroup>
                <SqlSourceIncludeFiles>false</SqlSourceIncludeFiles>
            </PropertyGroup>
            <ItemGroup>
                <AdditionalFiles Include="Queries/Users.sql;Queries/Users.sql.json" />
            </ItemGroup>
            """,
        // A project that lists its own files and forgets its sidecars.
        ["SqlSourceIncludeFiles false, the .sql file alone"] = """
            <PropertyGroup>
                <SqlSourceIncludeFiles>false</SqlSourceIncludeFiles>
            </PropertyGroup>
            <ItemGroup>
                <AdditionalFiles Include="Queries/**/*.sql" />
            </ItemGroup>
            """,
        // A .sql file linked from a folder beside the project's, without the line of its sidecar.
        ["a linked file without its sidecar"] = """
            <ItemGroup>
                <AdditionalFiles Include="../Shared/*.sql" />
            </ItemGroup>
            """,
    };

    private readonly FixtureProjects _fixtures = new();
    private readonly string _project;
    private readonly string _folder;

    public OnlineBuildTests()
    {
        _project = _fixtures.Copy("Online");
        _folder = Path.GetDirectoryName(_project)!;
    }

    public void Dispose() => _fixtures.Dispose();

    private string Sidecar => Path.Combine(_folder, "Queries", "Users.sql.json");

    private string SqlFile => Path.Combine(_folder, "Queries", "Users.sql");

    [Fact]
    public async Task Build_OnlineFalse_RunsNothingAndWritesNoFileOfIt()
    {
        await PassesAsync(Connection, "-p:SqlSourceOnline=false");

        StandIn.RunsIn(_folder).ShouldBeEmpty();
        OnlineFiles().ShouldBeEmpty();
    }

    [Fact]
    public async Task Build_AutoWithoutAConnectionSource_RunsNothingAndWritesNoFileOfIt()
    {
        await PassesAsync(NoVariables);

        StandIn.RunsIn(_folder).ShouldBeEmpty();
        OnlineFiles().ShouldBeEmpty();
    }

    [Fact]
    public async Task Build_AutoWithTheConnectionVariable_RunsTheToolOnceOnTheManifestOfTheBuild()
    {
        await PassesAsync(Connection);

        var run = StandIn.RunsIn(_folder).ShouldHaveSingleItem();
        run.Arguments.Count.ShouldBe(5);
        run.Arguments.Take(2).ShouldBe(["describe", "--manifest"]);
        run.Arguments.Skip(3).ShouldBe(["--online", "auto"]);
        var manifest = Path.GetFullPath(run.Arguments[2], _folder);
        manifest.ShouldStartWith(Path.Combine(_folder, "obj") + Path.DirectorySeparatorChar);
        Path.GetFileName(manifest).ShouldBe("App.csproj" + ManifestSuffix);
        run.Manifest.ShouldNotBeNull().ShouldContain("Project=" + _project);
        run.Manifest.ShouldContain("File=" + SqlFile);
    }

    // The build looks for a .env in the project's folder alone, as the tool does.
    [Fact]
    public async Task Build_AutoWithADotEnvInAFolderAbove_RunsNothing()
    {
        _ = _fixtures.WriteFile(".env", DotEnvText);

        await PassesAsync(NoVariables);

        StandIn.RunsIn(_folder).ShouldBeEmpty();
        OnlineFiles().ShouldBeEmpty();
    }

    // Under auto the build goes online exactly when the tool, given the command the build gives it, reads a file, and
    // that is the file the layout means, by its path from the folder of the copies.  A build under true shows the
    // command when auto runs nothing.
    [Theory]
    [InlineData("the project's .env", "Online/.env")]
    [InlineData("a .env above", null)]
    [InlineData("SqlSourceEnvFile relative", "shared.env")]
    [InlineData("SqlSourceEnvFile absolute", "shared.env")]
    [InlineData("SqlSourceEnvFile in a Directory.Build.props", "shared.env")]
    [InlineData("SqlSourceEnvFile missing", null)]
    [InlineData("a folder named .env", null)]
    [InlineData("SqlSourceEnvFile none with the project's .env", null)]
    public async Task Build_EnvLayout_GoesOnlineExactlyWhenTheToolReadsAFile(string layout, string? expected)
    {
        var arguments = Lay(layout);

        await PassesAsync(NoVariables, arguments);
        var wentOnline = StandIn.RunsIn(_folder).Count == 1;
        await PassesAsync(NoVariables, [.. arguments, "-p:SqlSourceOnline=true"]);

        var runs = StandIn.RunsIn(_folder);
        runs.Count.ShouldBe(wentOnline ? 2 : 1);
        var read = DotEnvSearch
            .Read(DotEnvSearch.FileOf(EnvFileOption(runs[^1]), _folder, _folder), ToolHost.ReadText)
            .Path;
        wentOnline.ShouldBe(read is not null);
        read.ShouldBe(expected is null ? null : _fixtures.PathOf(expected));
    }

    [Fact]
    public async Task Build_TrueWithEnvFileNone_PassesEnvFileNone()
    {
        _ = _fixtures.WriteFile("Online/.env", DotEnvText);

        await PassesAsync(
            NoVariables,
            "-p:SqlSourceOnline=true",
            FixtureProjects.PropertySwitch("SqlSourceEnvFile", " NONE ")
        );

        StandIn
            .RunsIn(_folder)
            .ShouldHaveSingleItem()
            .Arguments.Skip(3)
            .ShouldBe(["--online", "true", "--env-file", "none"]);
    }

    // The path is in a folder whose name the shell would read a $, a " or a % of, and cmd a %; Windows allows no " in
    // a name.
    [Fact]
    public async Task Build_EnvFileInAFolderWithShellCharacters_PassesItsFullPath()
    {
        var folder = OperatingSystem.IsWindows() ? "a $b c %d" : "a $b \"c\" %d";
        var file = _fixtures.WriteFile(folder + "/.env", DotEnvText);

        await PassesAsync(NoVariables, FixtureProjects.PropertySwitch("SqlSourceEnvFile", file));

        StandIn.RunsIn(_folder).ShouldHaveSingleItem().Arguments.TakeLast(2).ShouldBe(["--env-file", file]);
    }

    // A build with SqlSourceEnvFile not set, or blank, gives the tool the command it always has, which reads the
    // project folder's .env as the build does.
    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Build_DefaultEnvFile_PassesNoEnvFileOption(string? value)
    {
        _ = _fixtures.WriteFile("Online/.env", DotEnvText);

        await PassesAsync(
            NoVariables,
            value is null ? [] : [FixtureProjects.PropertySwitch("SqlSourceEnvFile", value)]
        );

        StandIn.RunsIn(_folder).ShouldHaveSingleItem().Arguments.Skip(3).ShouldBe(["--online", "auto"]);
    }

    [Fact]
    public async Task Build_TrueWithoutAConnectionSource_RunsTheTool()
    {
        await PassesAsync(NoVariables, "-p:SqlSourceOnline=true");

        StandIn.RunsIn(_folder).ShouldHaveSingleItem().Arguments.TakeLast(2).ShouldBe(["--online", "true"]);
    }

    // The variables the CI services set keep every build offline, under auto with a connection source and under true.
    [Theory]
    [InlineData("CI", "true", "auto")]
    [InlineData("CI", "true", "true")]
    [InlineData("TF_BUILD", "True", "auto")]
    [InlineData("TF_BUILD", "True", "true")]
    [InlineData("TEAMCITY_VERSION", "1", "auto")]
    [InlineData("TEAMCITY_VERSION", "1", "true")]
    [InlineData("JENKINS_URL", "x", "auto")]
    [InlineData("JENKINS_URL", "x", "true")]
    public async Task Build_InCI_RunsNothing(string variable, string value, string online)
    {
        await PassesAsync(new(Connection) { [variable] = value }, $"-p:SqlSourceOnline={online}");

        StandIn.RunsIn(_folder).ShouldBeEmpty();
    }

    // CI is recognised by the value GitHub and the others give it, true, and 1 is not that.
    [Fact]
    public async Task Build_CIOfOne_RunsTheTool()
    {
        await PassesAsync(new(Connection) { ["CI"] = "1" });

        _ = StandIn.RunsIn(_folder).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Build_OnlineYes_FailsWithTheErrorOfAValueNotKnown()
    {
        var output = await OutputOfFailingAsync(Connection, "-p:SqlSourceOnline=yes");

        output.ShouldContain("error SQLSRC024: SqlSourceOnline is 'yes'");
        StandIn.RunsIn(_folder).ShouldBeEmpty();
    }

    // A project writes the value in its file or in a variable, where the white space around it is kept.
    [Fact]
    public async Task Build_OnlineTrueInCapitalsWithSpaces_RunsTheTool()
    {
        await PassesAsync(new() { ["SqlSourceOnline"] = " TRUE " });

        StandIn.RunsIn(_folder).ShouldHaveSingleItem().Arguments.TakeLast(2).ShouldBe(["--online", "true"]);
    }

    // The second build, with nothing changed, finds the run up to date; then the one input touched runs it again.
    // The files above the project are written before the first build, as a repository holds them.
    [Theory]
    [InlineData("Online/Queries/Users.sql")]
    [InlineData("Online/sqlsource.json")]
    [InlineData("Online/Queries/Users.sql.json")]
    [InlineData("Online/UserRepository.cs")]
    [InlineData("Online/.env")]
    [InlineData("shared.env", "../shared.env")]
    [InlineData(".config/dotnet-tools.json")]
    [InlineData("dotnet-tools.json")]
    public async Task Build_AfterAnInputIsTouched_RunsTheToolOnceMore(string input, string? envFile = null)
    {
        var path = _fixtures.PathOf(input);
        if (!File.Exists(path))
        {
            _ = _fixtures.WriteFile(input, input.EndsWith(".json", StringComparison.Ordinal) ? EmptyToolManifest : "");
        }

        string[] arguments = envFile is null
            ? ["-p:SqlSourceOnline=true"]
            : ["-p:SqlSourceOnline=true", $"-p:SqlSourceEnvFile={envFile}"];
        await PassesAsync(NoVariables, arguments);
        await PassesAsync(NoVariables, arguments);
        StandIn.RunsIn(_folder).Count.ShouldBe(1, "the second build, with nothing changed");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        await PassesAsync(NoVariables, arguments);

        StandIn.RunsIn(_folder).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Build_AfterASidecarIsDeleted_RunsTheToolOnceMoreAndCompilesWithoutIt()
    {
        await PassesAsync(NoVariables, "-p:SqlSourceOnline=true");
        await PassesAsync(NoVariables, "-p:SqlSourceOnline=true");
        File.Delete(Sidecar);
        var output = await OutputOfPassingAsync(NoVariables, "-p:SqlSourceOnline=true");

        StandIn.RunsIn(_folder).Count.ShouldBe(2);
        CompiledItems().ShouldNotContain(Sidecar);
        output.ShouldNotContain("CS2001");
    }

    [Fact]
    public async Task Build_TrueAfterAuto_RunsTheToolOnceMore()
    {
        await PassesAsync(Connection);
        await PassesAsync(Connection);
        await PassesAsync(Connection, "-p:SqlSourceOnline=true");

        StandIn.RunsIn(_folder).Select(run => run.Arguments[^1]).ShouldBe(["auto", "true"]);
    }

    // A package of another version is in another folder of NuGet's cache, with files packed at the same time.
    [Fact]
    public async Task Build_PackageInAnotherFolder_RunsTheToolOnceMore()
    {
        await PassesAsync(NoVariables, "-p:SqlSourceOnline=true");
        await PassesAsync(NoVariables, "-p:SqlSourceOnline=true");
        _ = _fixtures.CopyBuildFolder("build2");
        await File.WriteAllTextAsync(
            _project,
            (await File.ReadAllTextAsync(_project, TestContext.Current.CancellationToken)).Replace(
                "../build/",
                "../build2/",
                StringComparison.Ordinal
            ),
            TestContext.Current.CancellationToken
        );
        await PassesAsync(NoVariables, "-p:SqlSourceOnline=true");

        StandIn.RunsIn(_folder).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Build_ToolReportsAnError_FailsWithItAloneAndRunsTheToolAgainNextTime()
    {
        StandIn.Tell(
            _folder,
            output: [$"{SqlFile}(4,1): error SQLSRC213: no connection for the database 'billing'"],
            exitCode: 1
        );

        var output = await OutputOfFailingAsync(NoVariables, "-p:SqlSourceOnline=true");

        output.ShouldContain("error SQLSRC213: no connection for the database 'billing'");
        output.ShouldNotContain("MSB3073");
        output.ShouldNotContain("SQLSRC027");
        Stamp().ShouldBeNull();
        await FailsAsync(NoVariables, "-p:SqlSourceOnline=true");
        StandIn.RunsIn(_folder).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Build_ToolIsOffline_PassesWithoutAStampAndRunsTheToolAgainNextTime()
    {
        StandIn.Tell(
            _folder,
            output: ["sqlsource: offline: no connection for billing; the build uses the committed sidecars"]
        );

        await PassesAsync(Connection);

        Stamp().ShouldBeNull();
        await PassesAsync(Connection);
        StandIn.RunsIn(_folder).Count.ShouldBe(2);
    }

    // The tool's sidecar is no item of the project as evaluated, and the compiler is still given it.  The run is up
    // to date at the next build, and the build after it compiles nothing.
    [Fact]
    public async Task Build_ToolCreatesASidecar_GivesItToTheCompilerAndBuildsNothingNextTime()
    {
        File.Delete(Sidecar);
        StandIn.Tell(_folder, create: new() { ["Queries/Users.sql.json"] = "{}" });

        await PassesAsync(NoVariables, "-p:SqlSourceOnline=true");
        CompiledItems().ShouldContain(Sidecar);
        await PassesAsync(NoVariables, "-p:SqlSourceOnline=true");
        var output = await OutputOfPassingAsync(NoVariables, "-p:SqlSourceOnline=true", "-v:d");

        StandIn.RunsIn(_folder).Count.ShouldBe(1);
        output.ShouldContain("Skipping target \"CoreCompile\" because all output files are up-to-date");
    }

    // The run creates the sidecar of a .sql item, wherever the item comes from, and the compiler of the same build is
    // given it once, whether or not the project as evaluated lists it.  At the next build, which does not run the
    // tool, the compiler is given it once when the project lists it, by a path of its own, and not at all when the
    // project does not, as in an offline build or CI.
    [Theory]
    [InlineData("the project's folder", "Online/Queries/Users.sql.json", true)]
    [InlineData("SqlSourceIncludeFiles false", "Online/Queries/Users.sql.json", true)]
    [InlineData("a linked file", "Shared/Linked.sql.json", true)]
    [InlineData("a file listed twice", "Online/Queries/Users.sql.json", true)]
    [InlineData("the sidecar listed by name", "Online/Queries/Users.sql.json", true)]
    [InlineData("SqlSourceIncludeFiles false, the .sql file alone", "Online/Queries/Users.sql.json", false)]
    [InlineData("a linked file without its sidecar", "Shared/Linked.sql.json", false)]
    public async Task Build_ToolCreatesTheSidecarOfAnItem_GivesItToTheCompilerOnce(
        string shape,
        string sidecar,
        bool listedNextTime
    )
    {
        File.Delete(Sidecar);
        _ = _fixtures.WriteFile("Shared/Linked.sql", "-- name: GetLinked\nSELECT 1 AS one;\n");
        AddToProject(ItemsOfShape[shape]);
        var created = _fixtures.PathOf(sidecar);
        StandIn.Tell(_folder, create: new() { [Path.GetRelativePath(_folder, created)] = "{}" });

        await PassesAsync(NoVariables, "-p:SqlSourceOnline=true");
        var first = CompiledItems();
        await PassesAsync(NoVariables, "-p:SqlSourceOnline=true");

        StandIn.RunsIn(_folder).Count.ShouldBe(1);
        first.Count(item => item == created).ShouldBe(1, string.Join(Environment.NewLine, first));
        var second = CompiledItems();
        second
            .Count(item => item == created)
            .ShouldBe(listedNextTime ? 1 : 0, string.Join(Environment.NewLine, second));
    }

    // A sidecar that existed before the build and that the project does not list is not given to the compiler,
    // though the run writes it again: the online build gives the compiler what an offline build or CI does.
    [Theory]
    [InlineData("SqlSourceIncludeFiles false, the .sql file alone", "Online/Queries/Users.sql.json")]
    [InlineData("a linked file without its sidecar", "Shared/Linked.sql.json")]
    public async Task Build_SidecarThatExistedBeforeAndIsNotListed_IsNotGivenToTheCompiler(string shape, string sidecar)
    {
        _ = _fixtures.WriteFile("Shared/Linked.sql", "-- name: GetLinked\nSELECT 1 AS one;\n");
        var existing = _fixtures.WriteFile(sidecar, "{}");
        AddToProject(ItemsOfShape[shape]);
        StandIn.Tell(_folder, create: new() { [Path.GetRelativePath(_folder, existing)] = "{ }" });

        await PassesAsync(NoVariables, "-p:SqlSourceOnline=true");
        var online = CompiledItems();
        await PassesAsync(NoVariables, "-p:SqlSourceOnline=false");
        var offline = CompiledItems();

        StandIn.RunsIn(_folder).Count.ShouldBe(1);
        (await ReadAsync(existing)).ShouldBe("{ }", "the run wrote the sidecar again");
        online.ShouldNotContain(existing, string.Join(Environment.NewLine, online));
        online.ShouldBe(offline);
    }

    // Only a .sql item has a sidecar: a JSON file beside an item of another kind is not given to the compiler.
    [Fact]
    public async Task Build_JsonFileBesideAnItemThatIsNoSqlFile_IsNotGivenToTheCompiler()
    {
        _ = _fixtures.WriteFile("Online/Queries/notes.txt", "notes");
        var json = _fixtures.WriteFile("Online/Queries/notes.txt.json", "{}");
        AddToProject(
            """
            <ItemGroup>
                <AdditionalFiles Include="Queries/notes.txt" />
            </ItemGroup>
            """
        );

        await PassesAsync(NoVariables, "-p:SqlSourceOnline=true");

        CompiledItems().ShouldContain(Path.Combine(_folder, "Queries", "notes.txt"));
        CompiledItems().ShouldNotContain(json);
    }

    [Fact]
    public async Task Build_ToolDeletesASidecar_CompilesWithoutIt()
    {
        StandIn.Tell(_folder, delete: ["Queries/Users.sql.json"]);

        var output = await OutputOfPassingAsync(NoVariables, "-p:SqlSourceOnline=true");

        File.Exists(Sidecar).ShouldBeFalse();
        CompiledItems().ShouldNotContain(Sidecar);
        output.ShouldNotContain("CS2001");
    }

    [Fact]
    public async Task Build_ToolHasNothingToDescribe_TouchesTheStampAndReportsNothing()
    {
        StandIn.Tell(_folder, output: ["sqlsource: online: nothing to describe"]);

        var output = await OutputOfPassingAsync(NoVariables, "-p:SqlSourceOnline=true");

        _ = Stamp().ShouldNotBeNull();
        output.ShouldContain("0 Warning(s)");
        output.ShouldContain("0 Error(s)");
    }

    // Exec reads a line that starts "error " or "warning " as an error or a warning of the build, and a database may
    // have either name, so under --online the tool writes each line of its summary after a prefix.  -warnaserror
    // turns a warning into a failure, so a build that passes reported neither.
    [Theory]
    [InlineData("error")]
    [InlineData("warning")]
    public async Task Build_ToolSummaryOfADatabaseNamedAsAnErrorOrAWarning_PassesAndTouchesTheStamp(string name)
    {
        StandIn.Tell(_folder, output: [$"sqlsource: summary: {name} (postgres): 1 described, 0 skipped, 0 failed"]);

        var output = await OutputOfPassingAsync(NoVariables, "-p:SqlSourceOnline=true", "-warnaserror");

        _ = Stamp().ShouldNotBeNull();
        output.ShouldNotContain("SQLSRC");
    }

    // A line of the summary is the tool's own, and no sign of a tool older than the package.
    [Fact]
    public async Task Build_ToolExitsWithACodeAndOnlyItsSummary_FailsWithoutSayingToUpdateTheTool()
    {
        StandIn.Tell(
            _folder,
            output: ["sqlsource: summary: billing (postgres): 1 described, 0 skipped, 0 failed"],
            exitCode: 1
        );

        var output = await OutputOfFailingAsync(NoVariables, "-p:SqlSourceOnline=true");

        output.ShouldContain("error SQLSRC027: The sqlsource tool exited with the code 1");
        output.ShouldNotContain("dotnet tool update");
        Stamp().ShouldBeNull();
    }

    // A project of one framework runs the tool in its own build, and calls no configuration of its own for the run or
    // for the sidecars it created, which would cost the build an evaluation of the project.
    [Fact]
    public async Task Build_OneFramework_CallsNoConfigurationOfItsOwnForTheRun()
    {
        var output = await OutputOfPassingAsync(NoVariables, "-p:SqlSourceOnline=true", "-v:d");

        _ = StandIn.RunsIn(_folder).ShouldHaveSingleItem();
        output.ShouldContain("Target \"SqlSourceRunOnline");
        output.ShouldNotContain("SqlSourceRunOnline target(s)");
        output.ShouldNotContain("SqlSourceCreatedSidecars target(s)");
    }

    // The properties a design-time build of an IDE sets, as SettingsFileSearchTests sets them.
    [Fact]
    public async Task Build_DesignTime_RunsNothing()
    {
        await PassesAsync(
            NoVariables,
            "-p:SqlSourceOnline=true",
            "-p:DesignTimeBuild=true",
            "-p:BuildingProject=false",
            "-p:SkipCompilerExecution=true",
            "-t:Compile"
        );

        StandIn.RunsIn(_folder).ShouldBeEmpty();
    }

    // MSBuild logs a variable that a project reads as a property with its value, at the diagnostic verbosity and in a
    // binary log; the build that goes online for the variable logs no word of it.
    [Fact]
    public async Task Build_WithTheConnectionVariable_PutsItsValueInNoLog()
    {
        var secret = "Host=localhost;Password=" + Guid.NewGuid().ToString("N");
        var log = _fixtures.PathOf("diag.log");
        var binaryLog = _fixtures.PathOf("build.binlog");
        var replay = _fixtures.PathOf("replay.log");

        var output = await OutputOfPassingAsync(
            new() { ["SQLSOURCE_CONNECTION"] = secret },
            $"-flp:v=diag;logfile={log}",
            $"-bl:{binaryLog}"
        );
        var (exitCode, replayOutput) = await FixtureProjects.RunMSBuildAsync(
            binaryLog,
            ["-noconlog", $"-flp:v=diag;logfile={replay}"]
        );

        exitCode.ShouldBe(0, replayOutput);
        _ = StandIn.RunsIn(_folder).ShouldHaveSingleItem();
        output.ShouldNotContain(secret);
        string[] logs = [await ReadAsync(log), await ReadAsync(replay)];
        foreach (var text in logs)
        {
            text.ShouldContain("Target \"SqlSourceRunOnline");
            text.ShouldNotContain(secret);
        }
    }

    // The real "dotnet sqlsource", the default command, with no tool manifest above the project, or with one that
    // names the tool at a version that no feed has and was never restored.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_AutoAndTheToolCannotStart_PassesWithAWarningThatWarnAsErrorLeaves(bool toolManifest)
    {
        WriteToolManifest(toolManifest);

        var (exitCode, output) = await FixtureProjects.BuildAsync(_project, Connection, "-warnaserror");

        exitCode.ShouldBe(0, output);
        output.ShouldContain("warning SQLSRC026");
        output.ShouldNotContain("error SQLSRC026");
        Stamp().ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Build_TrueAndTheToolCannotStart_FailsWithTheErrorOfThePackageAlone(bool toolManifest)
    {
        WriteToolManifest(toolManifest);

        var (exitCode, output) = await FixtureProjects.BuildAsync(_project, NoVariables, "-p:SqlSourceOnline=true");

        exitCode.ShouldNotBe(0, output);
        output.ShouldContain("error SQLSRC025");
        output.ShouldNotContain("MSB3073");
        output
            .Split('\n')
            .Where(line => line.Contains(": error ", StringComparison.Ordinal))
            .ShouldAllBe(line => line.Contains("error SQLSRC025", StringComparison.Ordinal));
        Stamp().ShouldBeNull();
    }

    // A tool older than the package, which does not know the options the build gives it.
    [Theory]
    [InlineData("auto")]
    [InlineData("true")]
    public async Task Build_ToolPrintsAUsageError_FailsSayingToUpdateTheTool(string online)
    {
        StandIn.Tell(_folder, output: [], error: ["sqlsource: unknown option at position 2"], exitCode: 1);

        var output = await OutputOfFailingAsync(Connection, $"-p:SqlSourceOnline={online}");

        output.ShouldContain("error SQLSRC027: The sqlsource tool exited with the code 1");
        output.ShouldContain("'dotnet tool update SqlSource.Tool --version <version>'");
        output.ShouldContain("It printed first: 'sqlsource: unknown option at position 2'.");
    }

    [Fact]
    public async Task Build_WithSqlSourceLog_GivesTheToolALogOfTheProjectUnderObj()
    {
        await PassesAsync(new() { ["SQLSOURCE_LOG"] = "1" }, "-p:SqlSourceOnline=true");

        var arguments = StandIn.RunsIn(_folder).ShouldHaveSingleItem().Arguments;
        var log = Path.GetFullPath(arguments[arguments.ToList().IndexOf("--log") + 1], _folder);
        log.ShouldStartWith(Path.Combine(_folder, "obj") + Path.DirectorySeparatorChar);
        Path.GetFileName(log).ShouldBe("App.csproj.SqlSource.log");
    }

    // The command names the manifest and the log by paths that hold the project's name, which the shell would read
    // a $( or a ` of, and run in the project's folder.  A % in the folder's name breaks NuGet's restore, so it is in
    // the project's name alone.
    [Fact]
    public async Task Build_ProjectWithCharactersAShellReadsInItsPath_RunsTheToolOnItsManifest()
    {
        const string OddName = "p q;r%41$(s)`t`.csproj";
        var copied = _fixtures.Copy("Online", folder: "a b;c$(d)`e`");
        var folder = Path.GetDirectoryName(copied)!;
        var odd = Path.Combine(folder, OddName);
        File.Move(copied, odd);

        var (exitCode, output) = await FixtureProjects.BuildAsync(
            odd,
            new Dictionary<string, string> { ["SQLSOURCE_LOG"] = "1" },
            StandIn.CommandSwitch,
            "-p:SqlSourceOnline=true"
        );

        exitCode.ShouldBe(0, output);
        var run = StandIn.RunsIn(folder).ShouldHaveSingleItem();
        Path.GetFileName(run.Arguments[2]).ShouldBe(OddName + ManifestSuffix);
        run.Manifest.ShouldNotBeNull().ShouldContain("Project=" + odd);
        Path.GetFileName(run.Arguments[^1]).ShouldBe(OddName + ".SqlSource.log");
    }

    // Writes the files of a layout of Build_EnvLayout_GoesOnlineExactlyWhenTheToolReadsAFile, and gives the switches
    // of its build.
    private string[] Lay(string layout)
    {
        switch (layout)
        {
            case "the project's .env":
                _ = _fixtures.WriteFile("Online/.env", DotEnvText);
                return [];
            case "a .env above":
                _ = _fixtures.WriteFile(".env", DotEnvText);
                return [];
            case "SqlSourceEnvFile relative":
                _ = _fixtures.WriteFile("shared.env", DotEnvText);
                return ["-p:SqlSourceEnvFile=../shared.env"];
            case "SqlSourceEnvFile absolute":
                return
                [
                    FixtureProjects.PropertySwitch("SqlSourceEnvFile", _fixtures.WriteFile("shared.env", DotEnvText)),
                ];
            case "SqlSourceEnvFile in a Directory.Build.props":
                _ = _fixtures.WriteFile("shared.env", DotEnvText);
                _ = _fixtures.WriteFile(
                    "Directory.Build.props",
                    """
                    <Project>
                        <PropertyGroup>
                            <SqlSourceEnvFile>$(MSBuildThisFileDirectory)shared.env</SqlSourceEnvFile>
                        </PropertyGroup>
                    </Project>
                    """
                );
                return [];
            case "SqlSourceEnvFile missing":
                return ["-p:SqlSourceEnvFile=../missing.env"];
            case "a folder named .env":
                _ = Directory.CreateDirectory(Path.Combine(_folder, ".env"));
                return [];
            case "SqlSourceEnvFile none with the project's .env":
                _ = _fixtures.WriteFile("Online/.env", DotEnvText);
                return [FixtureProjects.PropertySwitch("SqlSourceEnvFile", " None ")];
            default:
                throw new ArgumentOutOfRangeException(nameof(layout), layout, null);
        }
    }

    // The value of the run's --env-file, or null when it has none.
    private static string? EnvFileOption(StandInRun run)
    {
        var index = run.Arguments.ToList().IndexOf("--env-file");
        return index < 0 ? null : run.Arguments[index + 1];
    }

    private const string EmptyToolManifest = /*lang=json,strict*/
        """{ "version": 1, "isRoot": true, "tools": {} }""";

    // A tool manifest that names the tool at a version no feed has, which was never restored.
    private void WriteToolManifest(bool write)
    {
        if (write)
        {
            _ = _fixtures.WriteFile(
                ".config/dotnet-tools.json",
                /*lang=json,strict*/
                """
                {
                    "version": 1,
                    "isRoot": true,
                    "tools": {
                        "sqlsource.tool": { "version": "0.0.0-none", "commands": ["sqlsource"], "rollForward": false }
                    }
                }
                """
            );
        }
    }

    // Builds the copy with the stand-in as its command, and checks that it passed.
    private async Task PassesAsync(Dictionary<string, string> environment, params string[] arguments) =>
        _ = await OutputOfPassingAsync(environment, arguments);

    // Builds the copy with the stand-in as its command, and gives what it wrote once it passed.
    private async Task<string> OutputOfPassingAsync(Dictionary<string, string> environment, params string[] arguments)
    {
        var (exitCode, output) = await FixtureProjects.BuildAsync(
            _project,
            environment,
            [StandIn.CommandSwitch, .. arguments]
        );
        exitCode.ShouldBe(0, output);
        return output;
    }

    private async Task FailsAsync(Dictionary<string, string> environment, params string[] arguments) =>
        _ = await OutputOfFailingAsync(environment, arguments);

    private async Task<string> OutputOfFailingAsync(Dictionary<string, string> environment, params string[] arguments)
    {
        var (exitCode, output) = await FixtureProjects.BuildAsync(
            _project,
            environment,
            [StandIn.CommandSwitch, .. arguments]
        );
        exitCode.ShouldNotBe(0, output);
        return output;
    }

    // Adds elements to the copy's project file, before the import of the targets, as a project writes them.
    private void AddToProject(string elements)
    {
        const string TargetsImport = """<Import Project="../build/SqlSource.targets" />""";
        var text = File.ReadAllText(_project);
        text.ShouldContain(TargetsImport);
        File.WriteAllText(
            _project,
            text.Replace(TargetsImport, elements + "\n" + TargetsImport, StringComparison.Ordinal)
        );
    }

    // The full paths of the AdditionalFiles items the compiler was given, which the fixture's Directory.Build.targets
    // writes.
    private string[] CompiledItems() => File.ReadAllLines(Path.Combine(_folder, "obj", "compiled.net10.0.txt"));

    private List<string> OnlineFiles()
    {
        var obj = Path.Combine(_folder, "obj");
        return Directory.Exists(obj)
            ?
            [
                .. Directory
                    .EnumerateFiles(obj, "*", SearchOption.AllDirectories)
                    .Where(file => OnlineFileSuffixes.Any(suffix => file.EndsWith(suffix, StringComparison.Ordinal))),
            ]
            : [];
    }

    private string? Stamp() =>
        OnlineFiles().SingleOrDefault(file => file.EndsWith(StampSuffix, StringComparison.Ordinal));

    private static Task<string> ReadAsync(string path) =>
        File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
}
