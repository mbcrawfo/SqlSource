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

    // The target that runs the tool, once, on the manifest of every framework.
    private const string RunOnlineTarget = "SqlSourceRunOnline";

    // The target that answers the framework of a configuration that imports the package, and that a configuration
    // that does not has none of.
    private const string OnlineFrameworkTarget = "SqlSourceOnlineFramework";

    // The target that gives the run every other framework's manifest and inputs, and the one it calls in each.
    private const string FrameworkManifestsTarget = "SqlSourceWriteFrameworkManifests";
    private const string FrameworkInputsTarget = "SqlSourceFrameworkOnlineInputs";

    // The target that writes the files the run compares and gathers its inputs.
    private const string ListSidecarsTarget = "SqlSourceListSidecars";

    // The target that gives the sidecars the run created, from the configuration the run was called in.
    private const string CreatedSidecarsTarget = "SqlSourceCreatedSidecars";

    // The outer build's target, in buildMultiTargeting/SqlSource.targets.
    private const string OuterTarget = "SqlSourceRunOnlineBeforeDispatch";

    // The configuration of its own that the run is called in: the first framework's that has SqlSource, and a property
    // that no build of a framework has, so that no target of a framework's own build runs early in it.
    private const string OnlineRunConfiguration =
        "TargetFramework=$(SqlSourceOnlineRunFramework);SqlSourceOnlineRun=true";

    // The configuration of its own of each framework, which the build asks whether it has SqlSource.
    private const string AskedConfiguration =
        "TargetFramework=%(SqlSourceOnlineFrameworkAsked.Identity);SqlSourceOnlineRun=true";

    // The run's condition: not a design-time build, and the build goes online.  BuildingProject is left out: a
    // configuration that another build calls by name starts with it false.
    private const string RunCondition = "'$(DesignTimeBuild)' != 'true' and '$(SqlSourceGoesOnline)' == 'true'";

    // The connection source, which gives true or false and never the variable's value: MSBuild logs a variable that a
    // file reads as a property with its value, at the detailed verbosity and in a binary log.
    private const string NoConnectionVariable =
        "$([System.String]::IsNullOrWhiteSpace("
        + "$([System.Environment]::GetEnvironmentVariable('SQLSOURCE_CONNECTION'))))";

    private static readonly XDocument Outer = Load("buildMultiTargeting", "SqlSource.targets");

    // The targets of the online build in build/SqlSource.targets.
    private static readonly string[] OnlineTargets =
    [
        DescribeOnlineTarget,
        RunOnlineTarget,
        ListSidecarsTarget,
        CreatedSidecarsTarget,
        OnlineFrameworkTarget,
        FrameworkManifestsTarget,
        FrameworkInputsTarget,
    ];

    // The properties that the outer build's file defines, which build/SqlSource.targets defines too.
    private static readonly string[] SharedOnlineProperties =
    [
        "SqlSourceOnlineValue",
        "SqlSourceInCI",
        "SqlSourceHasConnectionVariable",
        "SqlSourceEnvFileValue",
        "SqlSourceEnvFilePath",
        "SqlSourceHasEnvFile",
        "SqlSourceGoesOnline",
        "MSBuildWarningsNotAsErrors",
    ];

    // The online properties, each as (name, condition, value) in the order it is set: SqlSourceOnline matched
    // ignoring case and the white space around it, by Equals, since a comparison of a condition reads yes, on and no
    // as booleans and would take yes for true; CI by the variables its services set; the connection source by its
    // answer alone; the .env file, the one SqlSourceEnvFile names from the project's folder, trimmed, or the project
    // folder's own, or none under none, which a condition matches ignoring case, and whether a file, not a folder, is
    // at its path, which the build never reads; and the run only outside CI, under true, or under auto with a
    // connection source.  The package's warning of a tool that cannot start stays a warning under -warnaserror.  The
    // shell's characters and their escape, for every path of the run's command, the other frameworks' manifests
    // among them, which those frameworks escape.  The tool's command is the tests' seam.
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
                ("SqlSourceEnvFileValue", null, "$(SqlSourceEnvFile.Trim())"),
                ("SqlSourceEnvFilePath", null, "$([MSBuild]::NormalizePath($(MSBuildProjectDirectory), '.env'))"),
                (
                    "SqlSourceEnvFilePath",
                    "'$(SqlSourceEnvFileValue)' != ''",
                    "$([MSBuild]::NormalizePath($(MSBuildProjectDirectory), $(SqlSourceEnvFileValue)))"
                ),
                ("SqlSourceEnvFilePath", "'$(SqlSourceEnvFileValue)' == 'none'", ""),
                ("SqlSourceHasEnvFile", null, "false"),
                (
                    "SqlSourceHasEnvFile",
                    "'$(SqlSourceEnvFilePath)' != '' and $([System.IO.File]::Exists($(SqlSourceEnvFilePath)))",
                    "true"
                ),
                ("SqlSourceGoesOnline", null, "false"),
                (
                    "SqlSourceGoesOnline",
                    "'$(SqlSourceInCI)' != 'true' and ($(SqlSourceOnlineValue.Equals('true'))"
                        + " or ($(SqlSourceOnlineValue.Equals('auto'))"
                        + " and ('$(SqlSourceHasConnectionVariable)' == 'true'"
                        + " or '$(SqlSourceHasEnvFile)' == 'true')))",
                    "true"
                ),
                (
                    "MSBuildWarningsNotAsErrors",
                    null,
                    "$(MSBuildWarningsNotAsErrors);" + SqlDiagnostics.OnlineToolCannotRunOffline.Id
                ),
                ("SqlSourceShellSpecial", null, "[$`\"\\\\]"),
                ("SqlSourceShellEscape", null, "\"'$0'\""),
                ("SqlSourceShellSpecial", "'$(OS)' == 'Windows_NT'", "%"),
                ("SqlSourceShellEscape", "'$(OS)' == 'Windows_NT'", "%$0"),
                ("SqlSourceDescribeOnlineDependsOn", null, ""),
                ("SqlSourceDescribeOnlineDependsOn", "'$(TargetFrameworks)' == ''", RunOnlineTarget),
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
    // search, so it runs in both places the search runs, and SqlSourceDescribeOnline runs the others, or calls them.
    // The outer build's target hooks the dispatch of the inner builds alone.
    [Fact]
    public void Targets_OnlineTargets_HookNothing()
    {
        foreach (var name in OnlineTargets)
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
    // -t:Compile, which leaves BuildingProject false.  It runs the tool itself in a project with no TargetFrameworks,
    // and every framework of a multi-targeted project finds the first framework that has SqlSource, calls the run in
    // that framework's configuration of its own, adds the files the run wrote to FileWrites, and calls it again for the
    // sidecars the run created; it calls nothing when no framework has SqlSource.  Then it takes away the sidecars the
    // run deleted, and gives the compiler each sidecar the run created that no item names, compared by full path,
    // once, by the path of its first .sql item with .json after it.
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
            .ShouldBe([
                "Error",
                "ItemGroup",
                "MSBuild",
                "PropertyGroup",
                "MSBuild",
                "MSBuild",
                "ItemGroup",
                "ItemGroup",
                "ItemGroup",
            ]);
        ShouldFindTheRunsFramework(target, "'$(SqlSourceGoesOnline)' == 'true' and '$(TargetFrameworks)' != ''");
        var calls = target.Elements("MSBuild").Skip(1).ToList();
        foreach (
            var (call, targetName, itemName) in new[]
            {
                (calls[0], RunOnlineTarget, "SqlSourceOnlineFile"),
                (calls[1], CreatedSidecarsTarget, "SqlSourceSidecarCreated"),
            }
        )
        {
            AttributesOf(call)
                .ShouldBe(
                    new Dictionary<string, string>
                    {
                        ["Projects"] = "$(MSBuildProjectFullPath)",
                        ["Targets"] = targetName,
                        ["Properties"] = OnlineRunConfiguration,
                        ["Condition"] = "'$(SqlSourceOnlineRunFramework)' != ''",
                    }
                );
            AttributesOf(call.Elements().ShouldHaveSingleItem())
                .ShouldBe(
                    new Dictionary<string, string> { ["TaskParameter"] = "TargetOutputs", ["ItemName"] = itemName }
                );
        }

        var groups = target.Elements("ItemGroup").Skip(1).ToList();
        groups[0].Attributes().ShouldBeEmpty();
        groups[0].Elements().Select(Item).ShouldBe([("FileWrites", "Include=@(SqlSourceOnlineFile)")]);
        groups.RemoveAt(0);
        groups[0].Attribute("Condition").ShouldNotBeNull().Value.ShouldBe("'$(SqlSourceGoesOnline)' == 'true'");
        groups[0]
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
        groups[1].Attribute("Condition").ShouldNotBeNull().Value.ShouldBe("'$(SqlSourceGoesOnline)' == 'true'");
        groups[1]
            .Elements()
            .Select(Item)
            .ShouldBe([
                ("SqlSourceListedFile", "Include=@(AdditionalFiles->'%(FullPath)')"),
                ("SqlSourceSidecarNotListed", "Include=@(SqlSourceSidecarCreated) Exclude=@(SqlSourceListedFile)"),
                (
                    "SqlSourceSidecarOfSqlItem",
                    "Include=@(AdditionalFiles->'%(FullPath).json') Condition='%(Extension)' == '.sql' "
                        + "SqlSourceItem=%(Identity).json"
                ),
                (
                    "SqlSourceSidecarNotAdded",
                    "Include=@(SqlSourceSidecarOfSqlItem) Exclude=@(SqlSourceSidecarNotListed)"
                ),
                ("SqlSourceSidecarOfSqlItem", "Remove=@(SqlSourceSidecarNotAdded)"),
                ("AdditionalFiles", "Include=@(SqlSourceSidecarOfSqlItem->Distinct()->'%(SqlSourceItem)')"),
            ]);
    }

    // The sidecars the run created, which a framework's build that called the run in a configuration of its own asks
    // that configuration for: the run returns the files for FileWrites.
    [Fact]
    public void Targets_CreatedSidecars_ReturnsTheSidecarsTheRunCreated()
    {
        var target = Target(CreatedSidecarsTarget);

        AttributesOf(target)
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Name"] = CreatedSidecarsTarget,
                    ["Returns"] = "@(SqlSourceSidecarCreated)",
                }
            );
        target.Elements().ShouldBeEmpty();
    }

    // A configuration of a framework that imports the package answers its framework, and one that does not has no
    // such target, which a call with SkipNonexistentTargets passes over.
    [Fact]
    public void Targets_OnlineFramework_AnswersTheFrameworkOfAConfigurationThatHasSqlSource()
    {
        var target = Target(OnlineFrameworkTarget);

        AttributesOf(target)
            .ShouldBe(
                new Dictionary<string, string> { ["Name"] = OnlineFrameworkTarget, ["Returns"] = "$(TargetFramework)" }
            );
        target.Elements().ShouldBeEmpty();
    }

    // In a multi-targeted project the run asks each other framework of TargetFrameworks, in their order, in that
    // framework's configuration of its own, for its manifest and inputs, and a framework without SqlSource gives
    // nothing.  A manifest's name given as a global property is the run's alone, so each other framework writes its
    // own.  A project with no TargetFrameworks skips it.
    [Fact]
    public void Targets_WriteFrameworkManifests_AsksEveryOtherFrameworkForItsManifest()
    {
        var target = Target(FrameworkManifestsTarget);

        AttributesOf(target)
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Name"] = FrameworkManifestsTarget,
                    ["Condition"] = "'$(TargetFrameworks)' != ''",
                }
            );
        target.Elements().Select(element => element.Name.LocalName).ShouldBe(["ItemGroup", "MSBuild"]);
        target
            .Element("ItemGroup")!
            .Elements()
            .Select(Item)
            .ShouldBe([("SqlSourceOtherFramework", "Include=$(TargetFrameworks) Exclude=$(TargetFramework)")]);
        var call = target.Element("MSBuild")!;
        AttributesOf(call)
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Projects"] = "$(MSBuildProjectFullPath)",
                    ["Targets"] = FrameworkInputsTarget,
                    ["Properties"] = "TargetFramework=%(SqlSourceOtherFramework.Identity);SqlSourceOnlineRun=true",
                    ["RemoveProperties"] = "SqlSourceManifestFile",
                    ["SkipNonexistentTargets"] = "true",
                }
            );
        AttributesOf(call.Elements().ShouldHaveSingleItem())
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["TaskParameter"] = "TargetOutputs",
                    ["ItemName"] = "SqlSourceFrameworkInput",
                }
            );
    }

    // A framework writes its manifest and gives it with the option that names it to the tool, its path escaped for
    // the shell as the run escapes its own; its .sql files by their full paths, whose sidecars the run lists; and its
    // settings files and Compile items, which are inputs of the run.
    [Fact]
    public void Targets_FrameworkOnlineInputs_GivesTheManifestItsSqlFilesAndItsInputs()
    {
        var target = Target(FrameworkInputsTarget);

        AttributesOf(target)
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Name"] = FrameworkInputsTarget,
                    ["DependsOnTargets"] = ManifestTarget,
                    ["Returns"] = "@(SqlSourceFrameworkOnlineInput)",
                }
            );
        target
            .Elements()
            .ShouldHaveSingleItem()
            .Elements()
            .Select(Item)
            .ShouldBe([
                (
                    "SqlSourceFrameworkOnlineInput",
                    "Include=$(SqlSourceManifestFile) SqlSourceOnlineKind=Manifest"
                        + " SqlSourceManifestIsDefault=$(SqlSourceManifestIsDefault)"
                        + " SqlSourceToolArgument= --manifest \""
                        + ShellEscaped("$(SqlSourceManifestFile)")
                        + "\""
                ),
                (
                    "SqlSourceFrameworkOnlineInput",
                    "Include=@(AdditionalFiles->'%(FullPath)') Condition='%(Extension)' == '.sql'"
                        + " SqlSourceOnlineKind=Sql"
                ),
                (
                    "SqlSourceFrameworkOnlineInput",
                    "Include=@(AdditionalFiles) Condition='%(Filename)%(Extension)' == 'sqlsource.json'"
                        + " SqlSourceOnlineKind=Input"
                ),
                ("SqlSourceFrameworkOnlineInput", "Include=@(Compile) SqlSourceOnlineKind=Input"),
            ]);
    }

    // The run's condition leaves BuildingProject out, which a configuration that another build calls by name starts
    // with false.  It depends on the manifest, on the target that gives it the other frameworks' manifests and on the
    // target that writes the files it compares, so that they are written in the configuration that runs it, wherever
    // it is called from.  It is up to date when every input is older than the stamp.
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
                    ["DependsOnTargets"] = ManifestTarget + ";" + FrameworkManifestsTarget + ";" + ListSidecarsTarget,
                    ["Inputs"] = "@(SqlSourceOnlineInput);@(Compile)",
                    ["Outputs"] = "$(SqlSourceOnlineStamp)",
                    ["Returns"] = "@(SqlSourceOnlineFile)",
                }
            );
    }

    // The inputs of the run: the manifest; the .sql files and the settings files among the AdditionalFiles items; the
    // sidecars of the .sql files that exist; the list of them, which a deleted sidecar changes; the file of the value,
    // the package's folder and the .env file's path, none under none, which a change of SqlSourceEnvFile changes when
    // the file it names is older than the stamp; the .env file, when a file is at its path; the tool manifests above
    // the project; and what the other frameworks gave.  The sidecars are those of every framework's .sql files, each
    // once by its full path, and the other frameworks' manifests are kept for the command.  Each file the target
    // writes, it writes only when its text changes, under obj, and adds to FileWrites.
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
                (
                    "SqlSourceFrameworkSqlFile",
                    "Include=@(SqlSourceFrameworkInput)"
                        + " Condition='%(SqlSourceFrameworkInput.SqlSourceOnlineKind)' == 'Sql'"
                ),
                (
                    "SqlSourceSidecarOfSqlFile",
                    "Include=@(SqlSourceFrameworkSqlFile->'%(FullPath).json'->Distinct())"
                        + " Exclude=@(SqlSourceSidecarOfSqlFile)"
                ),
                (
                    "SqlSourceFrameworkManifest",
                    "Include=@(SqlSourceFrameworkInput)"
                        + " Condition='%(SqlSourceFrameworkInput.SqlSourceOnlineKind)' == 'Manifest'"
                ),
                ("SqlSourceSidecarListed", "Include=@(SqlSourceSidecarOfSqlFile->Exists())"),
                ("SqlSourceOnlineValueLine", "Include=$(SqlSourceOnlineValue)"),
                ("SqlSourceOnlineValueLine", "Include=$(MSBuildThisFileDirectory)"),
                ("SqlSourceOnlineValueLine", "Include=$([MSBuild]::Escape($(SqlSourceEnvFilePath)))"),
                ("FileWrites", "Include=$(SqlSourceSidecarList)"),
                ("FileWrites", "Include=$(SqlSourceOnlineValueFile)"),
            ]);
        target
            .Elements("WriteLinesToFile")
            .Select(AttributesOf)
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
                    "Include=$([MSBuild]::Escape($(SqlSourceEnvFilePath))) Condition='$(SqlSourceHasEnvFile)' == 'true'"
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
                ("SqlSourceOnlineInput", "Include=@(SqlSourceFrameworkInput)"),
            ]);
    }

    // The tool is run on the manifest this build wrote and then on each other framework's, each with an option of its
    // own, from the project's folder, with the value of SqlSourceOnline,
    // with the .env file's full path, or none, when SqlSourceEnvFile is set, so that a default build's command is
    // unchanged, and with a log of this project and framework when SQLSOURCE_LOG is set.  Exec reads its errors as a
    // compiler's, ignores the exit code, which would add an error of Exec's own to the tool's, and keeps every line
    // for the target to read; the summary and the offline line are messages of low importance.  The paths are
    // escaped for the shell Exec runs the command in, which reads $, `, " and \ within quotes on Unix, each put in
    // single quotes, since MSBuild turns a \ of a path into a /, and % in cmd, each doubled.  On Windows the
    // backslashes that end the .env file's path, a folder's, are doubled, or the tool would read its closing quote.
    [Fact]
    public void Targets_RunOnline_ExecsTheToolOnTheManifest()
    {
        var target = Target(RunOnlineTarget);
        var exec = target.Elements("Exec").ShouldHaveSingleItem();

        AttributesOf(exec)
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Command"] =
                        "$(SqlSourceToolCommand) describe --manifest \"$(SqlSourceToolManifestArgument)\""
                        + "@(SqlSourceFrameworkManifest->'%(SqlSourceToolArgument)', '')"
                        + " --online $(SqlSourceOnlineValue)$(SqlSourceToolEnvFile)$(SqlSourceToolLog)",
                    ["WorkingDirectory"] = "$(MSBuildProjectDirectory)",
                    ["IgnoreExitCode"] = "true",
                    ["ConsoleToMSBuild"] = "true",
                    ["StandardOutputImportance"] = "low",
                }
            );
        exec.Elements("Output")
            .Select(AttributesOf)
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
                ("SqlSourceToolManifestArgument", null, ShellEscaped("$(SqlSourceManifestFile)")),
                ("SqlSourceToolEnvFileArgument", null, ShellEscaped("$(SqlSourceEnvFilePath)")),
                (
                    "SqlSourceToolEnvFileArgument",
                    "'$(OS)' == 'Windows_NT'",
                    "$([System.Text.RegularExpressions.Regex]::Replace($(SqlSourceToolEnvFileArgument), '\\\\+$',"
                        + " '$0$0'))"
                ),
                ("SqlSourceToolEnvFile", null, ""),
                (
                    "SqlSourceToolEnvFile",
                    "'$(SqlSourceEnvFileValue)' != ''",
                    " --env-file \"$(SqlSourceToolEnvFileArgument)\""
                ),
                ("SqlSourceToolEnvFile", "'$(SqlSourceEnvFileValue)' == 'none'", " --env-file none"),
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

    // A line only the tool writes: one starting sqlsource:, as each line of its summary does under --online, or an
    // error of its own.  None of dotnet's lines when it cannot start the tool is one, in any language, since none has
    // those forms.  A line of the summary without the prefix is not the tool's under --online.
    [Theory]
    [InlineData("sqlsource: offline: no connection for main; the build uses the committed sidecars", true)]
    [InlineData("sqlsource: online: nothing to describe", true)]
    [InlineData("sqlsource: unknown option at position 2", true)]
    [InlineData("/repo/Queries/Users.sql(3,8): error SQLSRC213: No connection.; help: Set it.; see: https://x", true)]
    [InlineData("sqlsource : error SQLSRC206: The manifest cannot be read.; see: https://x", true)]
    [InlineData("sqlsource: summary: main (postgres): 2 described, 1 skipped, 0 failed", true)]
    [InlineData("sqlsource: summary: orders (mssql): no connection, 3 skipped", true)]
    [InlineData("main (postgres): 2 described, 1 skipped, 0 failed", false)]
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
    [InlineData("sqlsource: summary: main (postgres): 0 described, 0 skipped, 1 failed", false)]
    [InlineData("sqlsource: summary: error (postgres): 1 described, 0 skipped, 0 failed", false)]
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
                    + " and !$(SqlSourceToolFirstLine.StartsWith('sqlsource: summary:'))"
            );
        advice[1].Value.ShouldEndWith(".  ");
        advice[1].Value.ShouldContain("'dotnet tool update SqlSource.Tool --version <version>'");
    }

    // The list of sidecars is written again after the run, or a sidecar the run created or deleted would run the tool
    // once more at the next build.  The sidecars on it after the run and not before are the ones the run created, each
    // once.  The stamp is touched only after a run of the tool that exited with 0 and was not
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
            .ShouldBe([
                ("SqlSourceSidecarAfterRun", "Include=@(SqlSourceSidecarOfSqlFile->Exists())"),
                (
                    "SqlSourceSidecarCreated",
                    "Include=@(SqlSourceSidecarAfterRun->Distinct()) Exclude=@(SqlSourceSidecarListed)"
                ),
            ]);
        AttributesOf(target.Element("WriteLinesToFile")!)
            .ShouldBe(WrittenWhenItChanges("$(SqlSourceSidecarList)", "@(SqlSourceSidecarAfterRun)"));
        target.Element("WriteLinesToFile")!.IsAfter(exec).ShouldBeTrue();
        AttributesOf(target.Element("Touch")!)
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Files"] = "$(SqlSourceOnlineStamp)",
                    ["AlwaysCreate"] = "true",
                    ["Condition"] = Succeeded,
                }
            );
        // In FileWrites whatever the run did, as when it is skipped, or IncrementalClean would delete the stamp of an
        // earlier build.  The files it writes under obj, the manifests, the other frameworks' among them, only when
        // they are the build's own, are what it returns, for a framework's build that called it in a configuration of
        // its own to add to FileWrites.
        var written = target.Elements("ItemGroup").Last();
        written.Attributes().ShouldBeEmpty();
        written
            .Elements()
            .Select(Item)
            .ShouldBe([
                ("FileWrites", "Include=$(SqlSourceOnlineStamp)"),
                (
                    "SqlSourceOnlineFile",
                    "Include=$(SqlSourceOnlineStamp);$(SqlSourceSidecarList);$(SqlSourceOnlineValueFile)"
                ),
                (
                    "SqlSourceOnlineFile",
                    "Include=$(SqlSourceManifestFile) Condition='$(SqlSourceManifestIsDefault)' == 'true'"
                ),
                (
                    "SqlSourceOnlineFile",
                    "Include=@(SqlSourceFrameworkManifest)"
                        + " Condition='%(SqlSourceFrameworkManifest.SqlSourceManifestIsDefault)' == 'true'"
                ),
            ]);
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

    // The outer build of a multi-targeted project runs the tool before it dispatches the inner builds, under the run's
    // condition, in the configuration of its own of the first framework that has SqlSource, which it finds as every
    // inner build does, so that their calls are answered at once.  It calls nothing when no framework has SqlSource.
    [Fact]
    public void Outer_Target_RunsTheToolForTheFirstFrameworkWithSqlSourceBeforeTheDispatch()
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
        target
            .Elements()
            .Select(element => element.Name.LocalName)
            .ShouldBe(["ItemGroup", "MSBuild", "PropertyGroup", "MSBuild"]);
        ShouldFindTheRunsFramework(target, null);
        AttributesOf(target.Elements("MSBuild").Last())
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["Projects"] = "$(MSBuildProjectFullPath)",
                    ["Targets"] = RunOnlineTarget,
                    ["Properties"] = OnlineRunConfiguration,
                    ["Condition"] = "'$(SqlSourceOnlineRunFramework)' != ''",
                }
            );
        target.Elements("MSBuild").Last().HasElements.ShouldBeFalse();
    }

    // The run's framework is the first of TargetFrameworks whose configuration of its own answers
    // SqlSourceOnlineFramework: the call is batched over the frameworks, which MSBuild runs in the order of the items,
    // and passes over one without the target.  The groups and the call have the condition given, and the property is
    // empty when none answers.
    private static void ShouldFindTheRunsFramework(XElement target, string? condition)
    {
        var asked = target.Element("ItemGroup")!;
        (asked.Attribute("Condition")?.Value).ShouldBe(condition);
        asked.Elements().Select(Item).ShouldBe([("SqlSourceOnlineFrameworkAsked", "Include=$(TargetFrameworks)")]);
        var call = target.Element("MSBuild")!;
        var attributes = new Dictionary<string, string>
        {
            ["Projects"] = "$(MSBuildProjectFullPath)",
            ["Targets"] = OnlineFrameworkTarget,
            ["Properties"] = AskedConfiguration,
            ["SkipNonexistentTargets"] = "true",
        };
        if (condition is not null)
        {
            attributes["Condition"] = condition;
        }

        AttributesOf(call).ShouldBe(attributes);
        AttributesOf(call.Elements().ShouldHaveSingleItem())
            .ShouldBe(
                new Dictionary<string, string>
                {
                    ["TaskParameter"] = "TargetOutputs",
                    ["ItemName"] = "SqlSourceOnlineFrameworkFound",
                }
            );
        var found = target.Element("PropertyGroup")!;
        (found.Attribute("Condition")?.Value).ShouldBe(condition);
        found
            .Elements()
            .Select(Line)
            .ShouldBe([
                ("SqlSourceOnlineFrameworksFound", null, "@(SqlSourceOnlineFrameworkFound)"),
                (
                    "SqlSourceOnlineRunFramework",
                    null,
                    "$([System.Text.RegularExpressions.Regex]::Match($(SqlSourceOnlineFrameworksFound), '[^;]+'))"
                ),
            ]);
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
