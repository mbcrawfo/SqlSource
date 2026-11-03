using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Diagnostics;
using Xunit;

namespace SqlSource.Tests.Package;

// The online build: the targets of build/SqlSource.targets that run the sqlsource tool before the compiler, and the
// outer build's target of buildMultiTargeting/SqlSource.targets.  The tests of tests/SqlSource.Tool.Tests run them
// against a stand-in of the tool; these hold the rules a build does not show.
public sealed partial class BuildFileTests
{
    // The target that runs the tool and gives the compiler the sidecars it left, which the hash depends on.
    private const string DescribeOnlineTarget = "SqlSourceDescribeOnline";

    // The target that runs the tool, once, in the first framework's configuration.
    private const string RunOnlineTarget = "SqlSourceRunOnline";

    // The target that writes the files the run compares and gathers its inputs.
    private const string ListSidecarsTarget = "SqlSourceListSidecars";

    // The outer build's target, in buildMultiTargeting/SqlSource.targets.
    private const string OuterTarget = "SqlSourceRunOnlineBeforeDispatch";

    // The run's condition: not a design-time build, and the build goes online.  BuildingProject is left out: a
    // configuration that another build calls by name starts with it false.
    private const string RunCondition = "'$(DesignTimeBuild)' != 'true' and '$(SqlSourceGoesOnline)' == 'true'";

    // The connection source, which gives true or false and never the variable's value: MSBuild logs a variable that a
    // file reads as a property with its value, at the detailed verbosity and in a binary log.
    private const string NoConnectionVariable =
        "$([System.String]::IsNullOrWhiteSpace("
        + "$([System.Environment]::GetEnvironmentVariable('SQLSOURCE_CONNECTION'))))";

    private static readonly XDocument Outer = XDocument.Load(
        Path.Combine(AppContext.BaseDirectory, "buildMultiTargeting", "SqlSource.targets")
    );

    // The properties that the outer build's file defines, which build/SqlSource.targets defines too.
    private static readonly string[] SharedOnlineProperties =
    [
        "SqlSourceOnlineValue",
        "SqlSourceInCI",
        "SqlSourceHasConnectionVariable",
        "SqlSourceEnvFolder",
        "SqlSourceFirstFramework",
        "SqlSourceGoesOnline",
        "MSBuildWarningsNotAsErrors",
    ];

    // The design's expressions, each property as (name, condition, value) in the order it is set: SqlSourceOnline
    // matched ignoring case and the white space around it, by Equals, since a comparison of a condition reads yes, on
    // and no as booleans and would take yes for true; CI by the
    // variables its services set; the connection source by its answer alone; the .env above the project; the first
    // framework as ProjectEvaluator.FirstFramework of the tool reads it; and the run only outside CI, under true, or
    // under auto with a connection source.  The package's warning of a tool that cannot start stays a warning under
    // -warnaserror.  The tool's command is the tests' seam.
    [Fact]
    public void Targets_OnlineProperties_AreTheDesignsExpressions() =>
        OnlineProperties(Targets)
            .Elements()
            .Select(Line)
            .ShouldBe([
                ("SqlSourceToolCommand", "'$(SqlSourceToolCommand)' == ''", "dotnet sqlsource"),
                ("SqlSourceOnlineValue", null, "$(SqlSourceOnline.Trim())"),
                (
                    "SqlSourceOnlineValue",
                    "'$(SqlSourceOnlineValue)' == '' or $(SqlSourceOnlineValue.ToLowerInvariant().Equals('auto'))",
                    "auto"
                ),
                ("SqlSourceOnlineValue", "$(SqlSourceOnlineValue.ToLowerInvariant().Equals('true'))", "true"),
                ("SqlSourceOnlineValue", "$(SqlSourceOnlineValue.ToLowerInvariant().Equals('false'))", "false"),
                ("SqlSourceInCI", null, "false"),
                (
                    "SqlSourceInCI",
                    "'$(CI)' == 'true' or '$(TF_BUILD)' == 'True' or '$(TEAMCITY_VERSION)' != ''"
                        + " or '$(JENKINS_URL)' != ''",
                    "true"
                ),
                ("SqlSourceHasConnectionVariable", null, "true"),
                ("SqlSourceHasConnectionVariable", NoConnectionVariable, "false"),
                (
                    "SqlSourceEnvFolder",
                    null,
                    "$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildProjectDirectory), '.env'))"
                ),
                (
                    "SqlSourceFirstFramework",
                    null,
                    @"$([System.Text.RegularExpressions.Regex]::Match($(TargetFrameworks), '[^;\s]+'))"
                ),
                ("SqlSourceGoesOnline", null, "false"),
                (
                    "SqlSourceGoesOnline",
                    "'$(SqlSourceInCI)' != 'true' and ($(SqlSourceOnlineValue.Equals('true'))"
                        + " or ($(SqlSourceOnlineValue.Equals('auto'))"
                        + " and ('$(SqlSourceHasConnectionVariable)' == 'true' or '$(SqlSourceEnvFolder)' != '')))",
                    "true"
                ),
                (
                    "MSBuildWarningsNotAsErrors",
                    null,
                    "$(MSBuildWarningsNotAsErrors);" + SqlDiagnostics.OnlineToolCannotRunOffline.Id
                ),
                ("SqlSourceDescribeOnlineDependsOn", null, ""),
                (
                    "SqlSourceDescribeOnlineDependsOn",
                    "'$(TargetFrameworks)' == '' or '$(TargetFramework)' == '$(SqlSourceFirstFramework)'",
                    RunOnlineTarget
                ),
            ]);

    // NuGet imports buildMultiTargeting/ alone into the outer build, so that file defines the properties its condition
    // reads, with the text of build/SqlSource.targets: a change to one is made to both.
    [Fact]
    public void Outer_Properties_AreDefinedAsTheTargetsDefineThem()
    {
        var root = Outer.Root.ShouldNotBeNull();

        root.Elements().Select(element => element.Name.LocalName).ShouldBe(["PropertyGroup", "Target"]);
        var group = root.Element("PropertyGroup")!;
        group.Attributes().ShouldBeEmpty();
        group.Elements().Select(element => element.Name.LocalName).Distinct().ShouldBe(SharedOnlineProperties);
        var targets = OnlineProperties(Targets);
        foreach (var name in SharedOnlineProperties)
        {
            group.Elements(name).Select(Line).ShouldBe(targets.Elements(name).Select(Line), name);
        }
    }

    // The package never reads SQLSOURCE_CONNECTION as a property, in any spelling: MSBuild would log its value with
    // the condition or the property that reads it.  The property that says whether it is set holds true or false.
    [Fact]
    public void BuildFiles_ConnectionVariable_IsNeverReadAsAProperty()
    {
        var files = Directory
            .EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "build"))
            .Concat(Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "buildMultiTargeting")))
            .ToList();

        files.Count.ShouldBe(4);
        foreach (var file in files)
        {
            ConnectionVariableAsAProperty().IsMatch(File.ReadAllText(file)).ShouldBeFalse(file);
        }

        SdkProperties.ShouldNotContain(name => name.Equals("SQLSOURCE_CONNECTION", StringComparison.OrdinalIgnoreCase));
    }

    // A comparison of a condition reads yes, on and no as booleans, so '$(SqlSourceOnlineValue)' == 'true' would hold
    // for yes, which must be SQLSRC024.  The value is compared by Equals, and == only with the empty string.
    [Fact]
    public void BuildFiles_OnlineValue_IsNeverComparedAsABoolean() =>
        Values(Targets)
            .Concat(Values(Outer))
            .SelectMany(value => ValueComparison().Matches(value))
            .Select(match => match.Value)
            .ShouldBe(["'$(SqlSourceOnlineValue)' == ''", "'$(SqlSourceOnlineValue)' == ''"]);

    // The online targets hook nothing: SqlSourceTrackAdditionalFiles depends on SqlSourceDescribeOnline after the
    // search, so it runs in both places the search runs, and SqlSourceDescribeOnline runs the others.  The outer
    // build's target hooks the dispatch of the inner builds alone.
    [Fact]
    public void Targets_OnlineTargets_HookNothing()
    {
        foreach (var name in new[] { DescribeOnlineTarget, RunOnlineTarget, ListSidecarsTarget })
        {
            Target(name).Attribute("BeforeTargets").ShouldBeNull(name);
            Target(name).Attribute("AfterTargets").ShouldBeNull(name);
        }

        Target(ListSidecarsTarget).Attributes().Select(attribute => attribute.Name.LocalName).ShouldBe(["Name"]);
        var outer = Outer.Root!.Elements("Target").ShouldHaveSingleItem();
        outer.Attribute("BeforeTargets").ShouldNotBeNull().Value.ShouldBe("DispatchToInnerBuilds");
        outer.Attribute("AfterTargets").ShouldBeNull();
    }

    // The online build is no part of a design-time build, which an IDE runs on every edit, nor of a compile by
    // -t:Compile, which leaves BuildingProject false.  It runs the tool itself in the first framework, or a project of
    // one, and every other framework calls the run in the first framework's configuration.  Then it gives the
    // compiler the sidecars the run created, as the props' pattern does, and takes away the ones it deleted.
    [Fact]
    public void Targets_DescribeOnline_RunsTheToolInABuildAndGivesTheCompilerItsSidecars()
    {
        var target = Target(DescribeOnlineTarget);

        target
            .Attributes()
            .ToDictionary(attribute => attribute.Name.LocalName, attribute => OneLine(attribute.Value))
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Name"] = DescribeOnlineTarget,
                    ["Condition"] = "'$(DesignTimeBuild)' != 'true' and '$(BuildingProject)' == 'true'",
                    ["DependsOnTargets"] = "$(SqlSourceDescribeOnlineDependsOn)",
                }
            );
        target
            .Elements()
            .Select(element => element.Name.LocalName)
            .ShouldBe(["Error", "MSBuild", "ItemGroup", "ItemGroup"]);
        Attributes(target.Element("MSBuild")!)
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Projects"] = "$(MSBuildProjectFullPath)",
                    ["Targets"] = RunOnlineTarget,
                    ["Properties"] = "TargetFramework=$(SqlSourceFirstFramework)",
                    ["Condition"] =
                        "'$(SqlSourceGoesOnline)' == 'true' and '$(TargetFrameworks)' != ''"
                        + " and '$(TargetFramework)' != '$(SqlSourceFirstFramework)'",
                }
            );

        var groups = target.Elements("ItemGroup").ToList();
        groups[0]
            .Attribute("Condition")
            .ShouldNotBeNull()
            .Value.ShouldBe("'$(SqlSourceGoesOnline)' == 'true' and '$(SqlSourceIncludeFiles)' != 'false'");
        groups[0]
            .Elements()
            .Select(Item)
            .ShouldBe([
                (
                    "SqlSourceSidecarFound",
                    "Include=**/*.sql.json Exclude=$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)"
                ),
                ("AdditionalFiles", "Include=@(SqlSourceSidecarFound) Exclude=@(AdditionalFiles)"),
            ]);
        groups[1].Attribute("Condition").ShouldNotBeNull().Value.ShouldBe("'$(SqlSourceGoesOnline)' == 'true'");
        groups[1]
            .Elements()
            .Select(Item)
            .ShouldBe([
                (
                    "SqlSourceJsonFileGone",
                    "Include=@(AdditionalFiles) Condition='%(Extension)' == '.json' and !Exists('%(FullPath)')"
                ),
                (
                    "SqlSourceJsonFileGoneStem",
                    "Include=@(SqlSourceJsonFileGone->'%(RootDir)%(Directory)%(Filename)') SqlSourceItem=%(Identity)"
                ),
                (
                    "SqlSourceSidecarGone",
                    "Include=@(SqlSourceJsonFileGoneStem->'%(SqlSourceItem)') Condition='%(Extension)' == '.sql'"
                ),
                ("AdditionalFiles", "Remove=@(SqlSourceSidecarGone)"),
            ]);
    }

    // The props' pattern for a sidecar is the one the online build expands again, with the same excludes and under
    // the same condition.
    [Fact]
    public void Targets_DescribeOnline_ExpandsThePropsPatternForSidecars()
    {
        var props = Props
            .Descendants("AdditionalFiles")
            .Single(item => item.Attribute("Include")!.Value == "**/*.sql.json");
        var online = Target(DescribeOnlineTarget).Descendants("SqlSourceSidecarFound").ShouldHaveSingleItem();

        online.Attribute("Exclude")!.Value.ShouldBe(props.Attribute("Exclude")!.Value);
        online
            .Parent!.Attribute("Condition")!
            .Value.ShouldEndWith(" and " + props.Parent!.Attribute("Condition")!.Value);
    }

    // The run's condition is the design's: not BuildingProject, which a configuration that another build calls by
    // name starts with false.  It depends on the manifest and on the target that writes the files it compares, so
    // that they are written in the configuration that runs it, wherever it is called from.  It is up to date when
    // every input is older than the stamp.
    [Fact]
    public void Targets_RunOnline_RunsWhenAnInputIsNewerThanTheStamp()
    {
        var target = Target(RunOnlineTarget);

        target
            .Attributes()
            .ToDictionary(attribute => attribute.Name.LocalName, attribute => OneLine(attribute.Value))
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Name"] = RunOnlineTarget,
                    ["Condition"] = RunCondition,
                    ["DependsOnTargets"] = ManifestTarget + ";" + ListSidecarsTarget,
                    ["Inputs"] = "@(SqlSourceOnlineInput);@(Compile)",
                    ["Outputs"] = "$(SqlSourceOnlineStamp)",
                }
            );
    }

    // The inputs of the run: the manifest; the .sql files and the settings files among the AdditionalFiles items; the
    // sidecars of the .sql files that exist; the list of them, which a deleted sidecar changes; the file of the value
    // and the package's folder; the .env the connection source found; and the tool manifests above the project.  Each
    // file the target writes, it writes only when its text changes, under obj, and adds to FileWrites.
    [Fact]
    public void Targets_ListSidecars_WritesTheFilesTheRunComparesAndGathersItsInputs()
    {
        var target = Target(ListSidecarsTarget);

        target
            .Elements("PropertyGroup")
            .ShouldHaveSingleItem()
            .Elements()
            .Select(Line)
            .ShouldBe([
                ("SqlSourceSidecarList", null, "$(IntermediateOutputPath)$(MSBuildProjectFile).SqlSource.sidecars"),
                ("SqlSourceOnlineValueFile", null, "$(IntermediateOutputPath)$(MSBuildProjectFile).SqlSource.online"),
                ("SqlSourceOnlineStamp", null, "$(IntermediateOutputPath)$(MSBuildProjectFile).SqlSource.stamp"),
                (
                    "SqlSourceToolManifestFolder",
                    null,
                    "$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildProjectDirectory), '.config/dotnet-tools.json'))"
                ),
                (
                    "SqlSourceRootToolManifestFolder",
                    null,
                    "$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildProjectDirectory), 'dotnet-tools.json'))"
                ),
            ]);
        target
            .Descendants("ItemGroup")
            .Elements()
            .Where(item => item.Name.LocalName != "SqlSourceOnlineInput")
            .Select(Item)
            .ShouldBe([
                (
                    "SqlSourceSidecarOfSqlFile",
                    "Include=@(AdditionalFiles->'%(FullPath).json') Condition='%(Extension)' == '.sql'"
                ),
                ("SqlSourceSidecarListed", "Include=@(SqlSourceSidecarOfSqlFile->Exists())"),
                ("SqlSourceOnlineValueLine", "Include=$(SqlSourceOnlineValue)"),
                ("SqlSourceOnlineValueLine", "Include=$(MSBuildThisFileDirectory)"),
                ("FileWrites", "Include=$(SqlSourceSidecarList)"),
                ("FileWrites", "Include=$(SqlSourceOnlineValueFile)"),
            ]);
        target
            .Elements("WriteLinesToFile")
            .Select(Attributes)
            .ShouldBe([
                WrittenWhenItChanges("$(SqlSourceSidecarList)", "@(SqlSourceSidecarListed)"),
                WrittenWhenItChanges("$(SqlSourceOnlineValueFile)", "@(SqlSourceOnlineValueLine)"),
            ]);

        target
            .Descendants("SqlSourceOnlineInput")
            .Select(Item)
            .ShouldBe([
                ("SqlSourceOnlineInput", "Include=$(SqlSourceManifestFile)"),
                (
                    "SqlSourceOnlineInput",
                    "Include=@(AdditionalFiles) Condition='%(Extension)' == '.sql'"
                        + " or '%(Filename)%(Extension)' == 'sqlsource.json'"
                ),
                ("SqlSourceOnlineInput", "Include=@(SqlSourceSidecarListed)"),
                ("SqlSourceOnlineInput", "Include=$(SqlSourceSidecarList)"),
                ("SqlSourceOnlineInput", "Include=$(SqlSourceOnlineValueFile)"),
                (
                    "SqlSourceOnlineInput",
                    "Include=$([MSBuild]::Escape($([MSBuild]::NormalizePath($(SqlSourceEnvFolder), '.env'))))"
                        + " Condition='$(SqlSourceEnvFolder)' != ''"
                ),
                (
                    "SqlSourceOnlineInput",
                    "Include=$([MSBuild]::Escape($([MSBuild]::NormalizePath($(SqlSourceToolManifestFolder), '.config',"
                        + " 'dotnet-tools.json')))) Condition='$(SqlSourceToolManifestFolder)' != ''"
                ),
                (
                    "SqlSourceOnlineInput",
                    "Include=$([MSBuild]::Escape($([MSBuild]::NormalizePath($(SqlSourceRootToolManifestFolder),"
                        + " 'dotnet-tools.json')))) Condition='$(SqlSourceRootToolManifestFolder)' != ''"
                ),
            ]);
    }

    // The tool is run on the manifest this build wrote, from the project's folder, with the value of SqlSourceOnline,
    // and with a log of this project and framework when SQLSOURCE_LOG is set.  Exec reads its errors as a
    // compiler's, ignores the exit code, which would add an error of Exec's own to the tool's, and keeps every line
    // for the target to read; the summary and the offline line are messages of low importance.  Both paths are
    // escaped for the shell Exec runs the command in, which reads $, `, " and \ within quotes on Unix, each put in
    // single quotes, since MSBuild turns a \ of a path into a /, and % in cmd, each doubled.
    [Fact]
    public void Targets_RunOnline_ExecsTheToolOnTheManifest()
    {
        var target = Target(RunOnlineTarget);
        var exec = target.Elements("Exec").ShouldHaveSingleItem();

        Attributes(exec)
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Command"] =
                        "$(SqlSourceToolCommand) describe --manifest \"$(SqlSourceToolManifestArgument)\""
                        + " --online $(SqlSourceOnlineValue)$(SqlSourceToolLog)",
                    ["WorkingDirectory"] = "$(MSBuildProjectDirectory)",
                    ["IgnoreExitCode"] = "true",
                    ["ConsoleToMSBuild"] = "true",
                    ["StandardOutputImportance"] = "low",
                }
            );
        exec.Elements("Output")
            .Select(Attributes)
            .ShouldBe([
                new Dictionary<string, string>
                {
                    ["TaskParameter"] = "ExitCode",
                    ["PropertyName"] = "SqlSourceToolExitCode",
                },
                new Dictionary<string, string>
                {
                    ["TaskParameter"] = "ConsoleOutput",
                    ["ItemName"] = "SqlSourceToolOutput",
                },
            ]);
        target
            .Elements("PropertyGroup")
            .First()
            .Elements()
            .Select(Line)
            .ShouldBe([
                ("SqlSourceShellSpecial", null, "[$`\"\\\\]"),
                ("SqlSourceShellEscape", null, "\"'$0'\""),
                ("SqlSourceShellSpecial", "'$(OS)' == 'Windows_NT'", "%"),
                ("SqlSourceShellEscape", "'$(OS)' == 'Windows_NT'", "%$0"),
                ("SqlSourceToolManifestArgument", null, ShellEscaped("$(SqlSourceManifestFile)")),
                ("SqlSourceToolLogFile", null, "$(IntermediateOutputPath)$(MSBuildProjectFile).SqlSource.log"),
                ("SqlSourceToolLog", null, ""),
                (
                    "SqlSourceToolLog",
                    "'$(SQLSOURCE_LOG)' != ''",
                    " --log \"" + ShellEscaped("$(SqlSourceToolLogFile)") + "\""
                ),
            ]);
        target.Elements("PropertyGroup").First().IsBefore(exec).ShouldBeTrue();
    }

    // A line only the tool writes: one starting sqlsource:, an error of its own, or a line of its summary, in the
    // forms of RunSummary.Lines of the tool.  None of dotnet's lines when it cannot start the tool is one, in any
    // language, since none has those forms.
    [Theory]
    [InlineData("sqlsource: offline: no connection for main; the build uses the committed sidecars", true)]
    [InlineData("sqlsource: online: nothing to describe", true)]
    [InlineData("sqlsource: unknown option at position 2", true)]
    [InlineData("/repo/Queries/Users.sql(3,8): error SQLSRC213: No connection.; help: Set it.; see: https://x", true)]
    [InlineData("sqlsource : error SQLSRC206: The manifest cannot be read.; see: https://x", true)]
    [InlineData("main (postgres): 2 described, 1 skipped, 0 failed", true)]
    [InlineData("orders (mssql): no connection, 3 skipped", true)]
    [InlineData("Could not execute because the specified command or file was not found.", false)]
    [InlineData("Possible reasons for this include:", false)]
    [InlineData("  * You misspelled a built-in dotnet command.", false)]
    [InlineData("Run \"dotnet tool restore\" to make the \"sqlsource\" command available.", false)]
    [InlineData("", false)]
    public void Targets_RunOnline_TellsALineOfTheToolsOwn(string line, bool isTheTools) =>
        Regex.IsMatch(line, PatternOf("SqlSourceToolStarted")).ShouldBe(isTheTools);

    [Theory]
    [InlineData("/repo/Queries/Users.sql(3,8): error SQLSRC213: No connection.; help: Set it.; see: https://x", true)]
    [InlineData("sqlsource : error SQLSRC206: The manifest cannot be read.; see: https://x", true)]
    [InlineData("sqlsource: unknown option at position 2", false)]
    [InlineData("main (postgres): 0 described, 0 skipped, 1 failed", false)]
    public void Targets_RunOnline_TellsAnErrorOfTheToolsOwn(string line, bool isAnError) =>
        Regex.IsMatch(line, PatternOf("SqlSourceToolReportedError")).ShouldBe(isAnError);

    // The lines are joined with line breaks, so the first is the text before the first break, and each pattern that
    // reads a line from its start is multi-line.  A line can hold any character but a line break.
    [Fact]
    public void Targets_RunOnline_ReadsTheLinesJoinedByLineBreaks()
    {
        var lines = Target(RunOnlineTarget).Elements("PropertyGroup").ElementAt(1).Elements().ToList();

        lines[0].Name.LocalName.ShouldBe("SqlSourceToolOutputText");
        lines[0].Value.ShouldBe("@(SqlSourceToolOutput, '%0A')");
        lines[1].Name.LocalName.ShouldBe("SqlSourceToolFirstLine");
        lines[1]
            .Value.ShouldBe(@"$([System.Text.RegularExpressions.Regex]::Match($(SqlSourceToolOutputText), '^[^\n]*'))");
        PatternOf("SqlSourceToolStarted").ShouldStartWith("(?m)^");
        PatternOf("SqlSourceToolOffline").ShouldBe("(?m)^sqlsource: offline:");
        foreach (var name in new[] { "SqlSourceToolStarted", "SqlSourceToolReportedError", "SqlSourceToolOffline" })
        {
            lines.First(line => line.Name.LocalName == name).Attributes().ShouldBeEmpty(name);
            lines.First(line => line.Name.LocalName == name).Value.ShouldBe("false", name);
        }

        var advice = lines.Where(line => line.Name.LocalName == "SqlSourceToolUpdateAdvice").ToList();
        advice.Count.ShouldBe(2);
        advice[0].Value.ShouldBeEmpty();
        advice[1]
            .Attribute("Condition")!
            .Value.ShouldBe(
                "$(SqlSourceToolFirstLine.StartsWith('sqlsource: '))"
                    + " and !$(SqlSourceToolFirstLine.StartsWith('sqlsource: offline:'))"
                    + " and !$(SqlSourceToolFirstLine.StartsWith('sqlsource: online:'))"
            );
        advice[1].Value.ShouldEndWith(".  ");
        advice[1].Value.ShouldContain("'dotnet tool update SqlSource.Tool --version <version>'");
    }

    // The list of sidecars is written again after the run, or a sidecar the run created or deleted would run the tool
    // once more at the next build.  The stamp is touched only after a run of the tool that exited with 0 and was not
    // offline, so that the next build tries a failed or an offline run again.  The errors
    // come last, since they end the target.
    [Fact]
    public void Targets_RunOnline_TouchesTheStampOnlyAfterARunThatSucceededOnline()
    {
        const string Succeeded =
            "'$(SqlSourceToolExitCode)' == '0' and '$(SqlSourceToolStarted)' == 'true'"
            + " and '$(SqlSourceToolOffline)' != 'true'";
        var target = Target(RunOnlineTarget);
        var exec = target.Element("Exec")!;

        target
            .Elements()
            .Select(element => element.Name.LocalName)
            .ShouldBe([
                "PropertyGroup",
                "Exec",
                "PropertyGroup",
                "ItemGroup",
                "WriteLinesToFile",
                "Touch",
                "ItemGroup",
                "Warning",
                "Error",
                "Error",
            ]);
        target
            .Elements("ItemGroup")
            .First()
            .Elements()
            .Select(Item)
            .ShouldBe([("SqlSourceSidecarAfterRun", "Include=@(SqlSourceSidecarOfSqlFile->Exists())")]);
        Attributes(target.Element("WriteLinesToFile")!)
            .ShouldBe(WrittenWhenItChanges("$(SqlSourceSidecarList)", "@(SqlSourceSidecarAfterRun)"));
        target.Element("WriteLinesToFile")!.IsAfter(exec).ShouldBeTrue();
        Attributes(target.Element("Touch")!)
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Files"] = "$(SqlSourceOnlineStamp)",
                    ["AlwaysCreate"] = "true",
                    ["Condition"] = Succeeded,
                }
            );
        // In FileWrites whatever the run did, as when it is skipped, or IncrementalClean would delete the stamp of an
        // earlier build.
        var written = target.Elements("ItemGroup").Last();
        written.Attributes().ShouldBeEmpty();
        written.Elements().Select(Item).ShouldBe([("FileWrites", "Include=$(SqlSourceOnlineStamp)")]);
    }

    // SqlSourceOnline is auto, true or false; any other value stops the build.
    [Fact]
    public void Targets_InvalidOnlineValue_IsTheDescriptorsError() =>
        ShouldReport(
            Target(DescribeOnlineTarget).Element("Error")!,
            SqlDiagnostics.OnlineValueInvalid,
            "!$(SqlSourceOnlineValue.Equals('auto')) and !$(SqlSourceOnlineValue.Equals('true'))"
                + " and !$(SqlSourceOnlineValue.Equals('false'))",
            "$(SqlSourceOnlineValue)"
        );

    // A tool that printed no line of its own did not start: under true the build stops, and under auto it goes on with
    // the committed sidecars, with a warning.
    [Fact]
    public void Targets_ToolThatCannotStart_IsTheDescriptorsErrorUnderTrueAndWarningUnderAuto()
    {
        var target = Target(RunOnlineTarget);

        ShouldReport(
            target.Element("Error")!,
            SqlDiagnostics.OnlineToolCannotRun,
            "'$(SqlSourceToolStarted)' != 'true' and $(SqlSourceOnlineValue.Equals('true'))"
        );
        ShouldReport(
            target.Element("Warning")!,
            SqlDiagnostics.OnlineToolCannotRunOffline,
            "'$(SqlSourceToolStarted)' != 'true' and $(SqlSourceOnlineValue.Equals('auto'))"
        );
        SqlDiagnostics.OnlineToolCannotRunOffline.DefaultSeverity.ShouldBe(DiagnosticSeverity.Warning);
    }

    // A tool that started, exited other than 0 and reported no error of its own would let the build pass with stale
    // sidecars, since Exec ignores the exit code, so the target fails the build, naming the code and the first line.
    [Fact]
    public void Targets_ToolThatFailsWithoutAnError_IsTheDescriptorsError() =>
        ShouldReport(
            Target(RunOnlineTarget).Elements("Error").Last(),
            SqlDiagnostics.OnlineToolFailedSilently,
            "'$(SqlSourceToolStarted)' == 'true' and '$(SqlSourceToolExitCode)' != '0'"
                + " and '$(SqlSourceToolReportedError)' != 'true'",
            "$(SqlSourceToolExitCode)",
            "$(SqlSourceToolFirstLine)",
            "$(SqlSourceToolUpdateAdvice)"
        );

    // MSBuild keeps a target's warning a warning under -warnaserror and MSBuildTreatWarningsAsErrors when its code is
    // in MSBuildWarningsNotAsErrors, which it reads from the project as evaluated: so the package sets it where its
    // files are imported, after the project's content, adding to the project's value, and with no condition.
    [Theory]
    [InlineData("SqlSource.targets")]
    [InlineData("buildMultiTargeting/SqlSource.targets")]
    public void BuildFiles_WarningOfAToolThatCannotStart_StaysAWarningUnderWarnAsError(string file)
    {
        var document = file == "SqlSource.targets" ? Targets : Outer;

        var line = document.Descendants("MSBuildWarningsNotAsErrors").ShouldHaveSingleItem();

        line.Value.ShouldBe("$(MSBuildWarningsNotAsErrors);" + SqlDiagnostics.OnlineToolCannotRunOffline.Id);
        line.Parent!.Parent.ShouldBe(document.Root);
        ConditionsAround(line).ShouldBeEmpty();
    }

    // The outer build of a multi-targeted project runs the tool for the first framework before it dispatches the inner
    // builds, under the run's condition, so that they still run side by side: each framework but the first calls the
    // run in the first framework's configuration, which this call has built.
    [Fact]
    public void Outer_Target_RunsTheToolForTheFirstFrameworkBeforeTheDispatch()
    {
        var target = Outer.Root!.Elements("Target").ShouldHaveSingleItem();

        target
            .Attributes()
            .ToDictionary(attribute => attribute.Name.LocalName, attribute => OneLine(attribute.Value))
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Name"] = OuterTarget,
                    ["BeforeTargets"] = "DispatchToInnerBuilds",
                    ["Condition"] = "'$(TargetFramework)' == '' and '$(TargetFrameworks)' != '' and " + RunCondition,
                }
            );
        Attributes(target.Elements().ShouldHaveSingleItem())
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Projects"] = "$(MSBuildProjectFullPath)",
                    ["Targets"] = RunOnlineTarget,
                    ["Properties"] = "TargetFramework=$(SqlSourceFirstFramework)",
                }
            );
        target.Elements().Single().Name.LocalName.ShouldBe("MSBuild");
    }

    // The code, the help link and the text of a targets diagnostic are its descriptor's, the text with {0}, {1} and
    // {2} where the target writes the properties given, in order.  It is reported at the project file, which sets
    // SqlSourceOnline, and not at a line of the package's targets in NuGet's cache.
    private static void ShouldReport(
        XElement element,
        DiagnosticDescriptor descriptor,
        string condition,
        params string[] placeholders
    )
    {
        element.Attribute("Code").ShouldNotBeNull().Value.ShouldBe(descriptor.Id);
        element.Attribute("File").ShouldNotBeNull().Value.ShouldBe("$(MSBuildProjectFullPath)");
        element.Attribute("HelpLink").ShouldNotBeNull().Value.ShouldBe(descriptor.HelpLinkUri);
        element.Attribute("Condition").ShouldNotBeNull().Value.ShouldBe(condition);
        var text = element.Attribute("Text").ShouldNotBeNull().Value;
        for (var index = 0; index < placeholders.Length; index++)
        {
            text = text.Replace(
                placeholders[index],
                "{" + index.ToString(CultureInfo.InvariantCulture) + "}",
                StringComparison.Ordinal
            );
        }

        text.ShouldBe(descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture));
    }

    // The PropertyGroup at the root of a file that sets the online properties.
    private static XElement OnlineProperties(XDocument document) =>
        document.Root!.Elements("PropertyGroup").Single(group => group.Element("SqlSourceOnlineValue") is not null);

    // The pattern of the property function IsMatch in the condition of the second line of a property.
    private static string PatternOf(string property)
    {
        var line = Target(RunOnlineTarget).Descendants(property).ElementAt(1);
        var match = IsMatchPattern().Match(line.Attribute("Condition").ShouldNotBeNull().Value);
        match.Success.ShouldBeTrue(property);
        return match.Groups["pattern"].Value;
    }

    private static Dictionary<string, string> WrittenWhenItChanges(string file, string lines) =>
        new()
        {
            ["File"] = file,
            ["Lines"] = lines,
            ["Overwrite"] = "true",
            ["WriteOnlyWhenDifferent"] = "true",
        };

    private static Dictionary<string, string> Attributes(XElement element) =>
        element.Attributes().ToDictionary(attribute => attribute.Name.LocalName, attribute => attribute.Value);

    // A path escaped for the shell that Exec runs the command in.
    private static string ShellEscaped(string path) =>
        $"$([System.Text.RegularExpressions.Regex]::Replace({path}, "
        + "$(SqlSourceShellSpecial), $(SqlSourceShellEscape)))";

    private static (string Name, string? Condition, string Value) Line(XElement line) =>
        (line.Name.LocalName, line.Attribute("Condition")?.Value, line.Value);

    private static (string Name, string Attributes) Item(XElement item) =>
        (
            item.Name.LocalName,
            string.Join(" ", item.Attributes().Select(attribute => $"{attribute.Name.LocalName}={attribute.Value}"))
        );

    // A value with each run of white space made one space, so that a condition wrapped over lines reads as one.
    private static string OneLine(string value) => WhiteSpace().Replace(value, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    [GeneratedRegex(@"IsMatch\(\$\(SqlSourceToolOutputText\), '(?<pattern>[^']*)'\)")]
    private static partial Regex IsMatchPattern();

    [GeneratedRegex(@"'\$\(SqlSourceOnlineValue\)'\s*[!=]=\s*'[^']*'")]
    private static partial Regex ValueComparison();

    [GeneratedRegex(@"\$\(\s*SQLSOURCE_CONNECTION", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionVariableAsAProperty();
}
