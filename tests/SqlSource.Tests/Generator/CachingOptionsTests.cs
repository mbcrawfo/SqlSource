// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.CachingScene;
using static SqlSource.Testing.Generator.StepReasons;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The caching of the settings: what a change to the generator parameters, the dialect or keep-comments, at each level
// that gives it, and to a setting of stage two in a settings file, makes the generator parse and emit again.
public sealed class CachingOptionsTests
{
    private readonly CachingScene _scene = new();

    // An edit of the project's settings file's parameters parses no file, emits every type again, and only the one with
    // a method comes out different.  The parameters are in each file's level of stage two, so every file's level
    // changes, and so does each type's TypeQueries, which holds its files' levels.
    [Fact]
    public void Run_GeneratorParametersOfTheProjectsFileChanged_EmitsEveryTypeAgainAndParsesNoFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var users = new InMemoryAdditionalText(
            _scene.Users.Path,
            "-- name: GetUser\nSELECT 1;\n-- name: ListFrom\nSELECT * FROM {{table}};\n"
        );
        var project = ProjectFile(OutputKind.Sql);
        var driver = _scene.FirstRun(compilation, users, project);

        var edited = ProjectFile(
            OutputKind.Sql,
            settings: /*lang=json,strict*/
            """{"generatorParameters": ["no-token-validation"]}"""
        );
        var result = Run(driver.ReplaceAdditionalText(project, edited), compilation);

        AllReasons(result, TrackingNames.SettingsFile).ShouldBe([IncrementalStepRunReason.Modified]);

        // No file was read.  The set of settings files changed, so every file's input was resolved again from it, and
        // each came out equal: the parameters are no part of it, and keep-comments, which is, did not change.
        ParseInputs(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_scene.Users.Path] = IncrementalStepRunReason.Unchanged,
                    [_scene.Orders.Path] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.ClaimedPaths).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBeCached();
        AllReasons(result, TrackingNames.FileLevel).ShouldAllBe(IncrementalStepRunReason.Modified);
        AllReasons(result, TrackingNames.TypeQueries).ShouldAllBe(IncrementalStepRunReason.Modified);

        // Both types were emitted again.  Only the one with a method came out different.
        Reasons<TypeOutput>(result, TrackingNames.TypeOutput, output => output.HintName)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Users.UserQueries.g.cs"] = IncrementalStepRunReason.Modified,
                    ["App.Orders.OrderQueries.g.cs"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        result
            .GeneratedTrees.Single(tree => tree.FilePath.EndsWith("UserQueries.g.cs", StringComparison.Ordinal))
            .ToString()
            .ShouldNotContain("Throw");
    }

    // The project's settings file names a dialect that reads the files as the one before did.  Every file is parsed
    // again, under the new dialect, and each type's output is worked out again and comes out equal, so nothing is
    // emitted.
    [Fact]
    public void Run_ProjectsFileDialectChangedToOneThatReadsTheFilesTheSameWay_EmitsNothingAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var project = ProjectFile(OutputKind.Sql);
        var driver = _scene.FirstRun(compilation, _scene.Users, project);

        var result = Run(driver.ReplaceAdditionalText(project, ProjectFile(OutputKind.Sql, "postgres")), compilation);

        AllReasons(result, TrackingNames.SettingsFile).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.FileParseInput).ShouldAllBe(IncrementalStepRunReason.Modified);
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(IncrementalStepRunReason.Modified);
        AllReasons(result, TrackingNames.TypeOutput).ShouldAllBe(IncrementalStepRunReason.Unchanged);

        // Two steps that report ran again and reported the same nothing: that of the problems of the files, for files
        // that are not the ones it had, and that of the problems of the settings files, whose set changed.  The steps
        // that add source did not run.
        var outputs = OutputReasons(result);
        outputs.ShouldAllBe(reason =>
            reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged
        );
        outputs.Count(reason => reason == IncrementalStepRunReason.Unchanged).ShouldBe(2);
        result.Diagnostics.ShouldBeEmpty();
    }

    // The project's settings file turns keep-comments on: every file is read again, and Orders.sql, which has no
    // comment to keep, gives an equal value.
    [Fact]
    public void Run_KeepCommentsTurnedOnByTheProjectsFile_ParsesTheFilesAgainAndOnlyThose()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var users = new InMemoryAdditionalText(_scene.Users.Path, "-- name: GetUser\nSELECT 1; -- c\n");
        var project = ProjectFile(OutputKind.Sql);
        var driver = _scene.FirstRun(compilation, users, project);

        var edited = ProjectFile(
            OutputKind.Sql,
            settings: /*lang=json,strict*/
            """{"generatorParameters": ["keep-comments"]}"""
        );
        var result = Run(driver.ReplaceAdditionalText(project, edited), compilation);

        AllReasons(result, TrackingNames.FileParseInput).ShouldAllBe(IncrementalStepRunReason.Modified);
        Reasons<ParsedSqlFile>(result, TrackingNames.ParsedFile, file => file.FileName)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["Users.sql"] = IncrementalStepRunReason.Modified,
                    ["Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        Reasons<TypeOutput>(result, TrackingNames.TypeOutput, output => output.HintName)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Users.UserQueries.g.cs"] = IncrementalStepRunReason.Modified,
                    ["App.Orders.OrderQueries.g.cs"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        result
            .GeneratedTrees.Single(tree => tree.FilePath.EndsWith("UserQueries.g.cs", StringComparison.Ordinal))
            .ToString()
            .ShouldContain("SELECT 1; -- c");
    }

    [Fact]
    public void Run_AttributeGainsKeepComments_ParsesOnlyTheFilesOfThatType()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRun(OutputKind.Sql, compilation);

        var result = Run(driver, WithParametersOnUserQueries(compilation, "keep-comments"));

        AllReasons(result, TrackingNames.CommentPaths).ShouldBe([IncrementalStepRunReason.Modified]);

        // Users.sql is read again, as it has to be: its type now asks for comments.  Orders.sql is not: the step that
        // resolves its input ran, because the list of paths it takes changed, and gave the value it had.
        ParseInputs(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_scene.Users.Path] = IncrementalStepRunReason.Modified,
                    [_scene.Orders.Path] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.ClaimedPaths).ShouldBe([IncrementalStepRunReason.Cached]);
        Reasons<ParsedSqlFile>(result, TrackingNames.ParsedFile, file => file.FileName)["Orders.sql"]
            .ShouldBe(IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void Run_AttributeGainsAParameterThatIsNotKeepComments_ParsesNoFileAndEmitsOnlyItsType()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRun(OutputKind.Sql, compilation);

        var result = Run(driver, WithParametersOnUserQueries(compilation, "sort-input"));

        AllReasons(result, TrackingNames.CommentPaths).ShouldBe([IncrementalStepRunReason.Cached]);

        // No file was read.
        ParseInputs(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_scene.Users.Path] = IncrementalStepRunReason.Cached,
                    [_scene.Orders.Path] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.ClaimedPaths).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBeCached();
        Reasons<TypeOutput>(result, TrackingNames.TypeOutput, output => output.HintName)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Users.UserQueries.g.cs"] = IncrementalStepRunReason.Unchanged,
                    ["App.Orders.OrderQueries.g.cs"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
    }

    // An edit of the settings file of Users' folder that turns keep-comments on: only Users.sql is read again, and only
    // its type is emitted again.  The set of settings files changed, so the input of Orders.sql was resolved again from
    // it and came out equal, Unchanged.
    [Fact]
    public void Run_FolderFileGainsKeepComments_ParsesOnlyThatFoldersFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var users = new InMemoryAdditionalText(_scene.Users.Path, "-- name: GetUser\nSELECT 1; -- c\n");
        var folder = new InMemoryAdditionalText("/app/Users/sqlsource.json", "{}");
        var driver = _scene.FirstRun(compilation, users, ProjectFile(OutputKind.Sql), folder);

        var edited = new InMemoryAdditionalText(
            folder.Path, /*lang=json,strict*/
            """{"generatorParameters": ["keep-comments"]}"""
        );
        var result = Run(driver.ReplaceAdditionalText(folder, edited), compilation);

        _scene.ShouldHaveReadOnly(result, _scene.Users, IncrementalStepRunReason.Unchanged);
        AllReasons(result, TrackingNames.ClaimedPaths).ShouldBe([IncrementalStepRunReason.Cached]);
        Reasons<TypeOutput>(result, TrackingNames.TypeOutput, output => output.HintName)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Users.UserQueries.g.cs"] = IncrementalStepRunReason.Modified,
                    ["App.Orders.OrderQueries.g.cs"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        result
            .GeneratedTrees.Single(tree => tree.FilePath.EndsWith("UserQueries.g.cs", StringComparison.Ordinal))
            .ToString()
            .ShouldContain("SELECT 1; -- c");
    }

    // The edited file's folder holds one type's file, whose level changes, and its query loses its constant to the
    // method of codegen.  The other file has a level from the file above both folders, which is resolved again and
    // comes out equal: the step named FileLevel is the one after the filter of nulls, which reports such a value as
    // taken from the previous run.  No file is parsed: the output is no part of what a parse depends on.
    [Fact]
    public void Run_SettingsFileOutputChanged_EmitsTheTypesOfItsFolderAndParsesNoFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var settings = new InMemoryAdditionalText(
            "/app/Users/sqlsource.json", /*lang=json,strict*/
            """{"output":"sql"}"""
        );
        var above = new InMemoryAdditionalText(
            "/app/sqlsource.json", /*lang=json,strict*/
            """{"inputModelSuffix":"In"}"""
        );
        var driver = _scene.FirstRunWithSettingsFiles(compilation, settings, above);

        var edited = new InMemoryAdditionalText(
            settings.Path, /*lang=json,strict*/
            """{"output":"codegen"}"""
        );
        var result = Run(driver.ReplaceAdditionalText(settings, edited), compilation);

        Reasons<FileLevel>(result, TrackingNames.FileLevel, level => level.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.FileLevels).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.ParsedFile)
            .ShouldBe([IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached]);
        Reasons<TypeQueries>(result, TrackingNames.TypeQueries, type => type.HintName)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Users.UserQueries.g.cs"] = IncrementalStepRunReason.Modified,
                    ["App.Orders.OrderQueries.g.cs"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        Reasons<TypeOutput>(result, TrackingNames.TypeOutput, output => output.HintName)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Users.UserQueries.g.cs"] = IncrementalStepRunReason.Modified,
                    ["App.Orders.OrderQueries.g.cs"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
    }

    // The class is named again for every type, from the new set, and only the type in the edited folder gets another
    // name.  No file is parsed.
    [Fact]
    public void Run_SettingsFileNestedClassChanged_ChecksOnlyTheTypesOfItsFolder()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var settings = new InMemoryAdditionalText(
            "/app/Users/sqlsource.json", /*lang=json,strict*/
            """{"nestedClass":"Queries"}"""
        );
        var driver = _scene.FirstRunWithSettingsFiles(compilation, settings);

        var edited = new InMemoryAdditionalText(
            settings.Path, /*lang=json,strict*/
            """{"nestedClass":"Statements"}"""
        );
        var result = Run(driver.ReplaceAdditionalText(settings, edited), compilation);

        Reasons<TargetType>(result, TrackingNames.NestedClassCheck, type => type.Name)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["UserQueries"] = IncrementalStepRunReason.Modified,
                    ["OrderQueries"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.ParsedFile)
            .ShouldBe([IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached]);
        result
            .GeneratedTrees.Single(tree => tree.FilePath.EndsWith("UserQueries.g.cs", StringComparison.Ordinal))
            .ToString()
            .ShouldContain("private static class Statements\n");
    }
}
