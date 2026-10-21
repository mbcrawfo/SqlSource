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
// that gives it, makes the generator parse and emit again.
public sealed class CachingOptionsTests
{
    private readonly CachingScene _scene = new();

    [Fact]
    public void Run_GeneratorParametersPropertyChanged_EmitsEveryTypeAgainAndParsesNoFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var users = new InMemoryAdditionalText(
            _scene.Users.Path,
            "-- name: GetUser\nSELECT 1;\n-- name: ListFrom\nSELECT * FROM {{table}};\n"
        );
        var driver = _scene.FirstRun(compilation, users, new TestOptionsProvider(OutputKind.Sql, null));

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider(OutputKind.Sql, "no-token-validation")),
            compilation
        );

        AllReasons(result, TrackingNames.ProjectSettings).ShouldBe([IncrementalStepRunReason.Modified]);

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
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.TypeQueries).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);

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

    // The class is named again for each type, and each type is emitted again, but no file is read again.
    [Fact]
    public void Run_NestedClassPropertyChanged_EmitsEveryTypeAgainAndParsesNoFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRun(OutputKind.Sql, compilation);

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(
                new TestOptionsProvider(
                    OutputKind.Sql,
                    null,
                    properties: new Dictionary<string, string?> { ["SqlSourceNestedClass"] = "Queries" }
                )
            ),
            compilation
        );

        AllReasons(result, TrackingNames.NestedClassCheck)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Modified);
        AllReasons(result, TrackingNames.ClaimedPaths).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.TypeOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Modified);
        result
            .GeneratedTrees.Single(tree => tree.FilePath.EndsWith("UserQueries.g.cs", StringComparison.Ordinal))
            .ToString()
            .ShouldContain("private static class Queries\n");
    }

    [Fact]
    public void Run_OptionsReplacedWithoutChangingTheProperty_EmitsNothingAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRun(OutputKind.Sql, compilation);

        // What the IDE does when any other property or an .editorconfig changes: new options, the same value.
        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider(OutputKind.Sql, null)),
            compilation
        );

        AllReasons(result, TrackingNames.ProjectSettings).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.ProjectDialect).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.FileParseInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.TypeOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void Run_DialectPropertyChanged_ParsesOnlyTheFilesThatFallBackToIt()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var users = new InMemoryAdditionalText(_scene.Users.Path, "-- name: GetUser\nSELECT 1; # c\n");
        var orderDialect = new Dictionary<string, string> { [_scene.Orders.Path] = "mssql" };
        var driver = _scene.FirstRun(
            compilation,
            users,
            new TestOptionsProvider(OutputKind.Sql, null, null, orderDialect)
        );

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(
                new TestOptionsProvider(OutputKind.Sql, null, "mysql", orderDialect)
            ),
            compilation
        );

        AllReasons(result, TrackingNames.ProjectDialect).ShouldBe([IncrementalStepRunReason.Modified]);
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
                    ["App.Orders.OrderQueries.g.cs"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );

        // MySQL reads the # as a comment, where ANSI kept it.
        result
            .GeneratedTrees.Single(tree => tree.FilePath.EndsWith("UserQueries.g.cs", StringComparison.Ordinal))
            .ToString()
            .ShouldContain("\"SELECT 1;\"");
    }

    [Fact]
    public void Run_DialectMetadataOfOneFileChanged_ParsesOnlyThatFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var users = new InMemoryAdditionalText(_scene.Users.Path, "-- name: GetUser\nSELECT 1; # c\n");
        var driver = _scene.FirstRun(
            compilation,
            users,
            new TestOptionsProvider(
                OutputKind.Sql,
                null,
                null,
                new Dictionary<string, string> { [_scene.Users.Path] = "mssql" }
            )
        );

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(
                new TestOptionsProvider(
                    OutputKind.Sql,
                    null,
                    null,
                    new Dictionary<string, string> { [_scene.Users.Path] = "mysql" }
                )
            ),
            compilation
        );

        AllReasons(result, TrackingNames.ProjectDialect).ShouldBe([IncrementalStepRunReason.Unchanged]);
        ParseInputs(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_scene.Users.Path] = IncrementalStepRunReason.Modified,
                    [_scene.Orders.Path] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.ClaimedPaths).ShouldBe([IncrementalStepRunReason.Cached]);
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
                    ["App.Orders.OrderQueries.g.cs"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
    }

    [Fact]
    public void Run_DialectChangedToOneThatReadsTheFilesTheSameWay_EmitsNothingAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRun(OutputKind.Sql, compilation);

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider(OutputKind.Sql, null, "postgres")),
            compilation
        );

        // Every file was parsed again, under the new dialect.  The file's value names the dialect it was read by, so
        // it is not equal, and each type's output was worked out again; it came out equal, so nothing was emitted.
        AllReasons(result, TrackingNames.FileParseInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Modified);
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Modified);
        AllReasons(result, TrackingNames.TypeOutput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Unchanged);

        // The step that reports the problems of the files ran again, for files and a dialect that are not the ones it
        // had, and reported the same nothing.  The steps that add source did not run.
        var outputs = OutputReasons(result);
        outputs.ShouldAllBe(reason =>
            reason == IncrementalStepRunReason.Cached || reason == IncrementalStepRunReason.Unchanged
        );
        outputs.Count(reason => reason == IncrementalStepRunReason.Unchanged).ShouldBe(1);
        result.Diagnostics.ShouldBeEmpty();
    }

    // Every file is read again.  Orders.sql has no comment to keep, so it gives an equal value: the step named
    // ParsedFile is the one after the read, which reports such a value as taken from the previous run.
    [Fact]
    public void Run_KeepCommentsTurnedOnByTheProperty_ParsesTheFilesAgainAndOnlyThose()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var users = new InMemoryAdditionalText(_scene.Users.Path, "-- name: GetUser\nSELECT 1; -- c\n");
        var driver = _scene.FirstRun(compilation, users, new TestOptionsProvider(OutputKind.Sql, null));

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider(OutputKind.Sql, "keep-comments")),
            compilation
        );

        AllReasons(result, TrackingNames.FileParseInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Modified);
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
    public void Run_PropertyGainsAParameterThatIsNotKeepComments_ParsesNoFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRun(OutputKind.Sql, compilation);

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider(OutputKind.Sql, "sort-input")),
            compilation
        );

        AllReasons(result, TrackingNames.ProjectSettings).ShouldBe([IncrementalStepRunReason.Modified]);

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
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.TypeOutput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Unchanged);
    }

    [Fact]
    public void Run_MetadataOfOneFileGainsAParameter_ParsesNoFileAndEmitsOnlyItsType()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRun(OutputKind.Sql, compilation);
        var metadata = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [_scene.Users.Path] = new Dictionary<string, string> { ["SqlSourceGeneratorParameters"] = "sort-input" },
        };

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(
                new TestOptionsProvider(OutputKind.Sql, null, fileMetadata: metadata)
            ),
            compilation
        );

        AllReasons(result, TrackingNames.FileSettings).ShouldBe([IncrementalStepRunReason.New]);

        // No file was read.  The metadata of Users.sql is an input of the step that resolves what its parse depends
        // on, so that step ran for it, and gave the value it had: the file's settings are no part of that value.
        // Were they, this would be Modified and the file would be read on every edit of its metadata.  ParsedFile
        // cannot show it: it is the step after the read, and is Cached for a file that is read again and gives an
        // equal value.
        ParseInputs(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_scene.Users.Path] = IncrementalStepRunReason.Unchanged,
                    [_scene.Orders.Path] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.ClaimedPaths).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
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
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
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

    [Fact]
    public void Run_MetadataOfOneFileGainsKeepComments_ParsesOnlyThatFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var users = new InMemoryAdditionalText(_scene.Users.Path, "-- name: GetUser\nSELECT 1; -- c\n");
        var driver = _scene.FirstRun(compilation, users, new TestOptionsProvider(OutputKind.Sql, null));
        var metadata = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [_scene.Users.Path] = new Dictionary<string, string> { ["SqlSourceGeneratorParameters"] = "keep-comments" },
        };

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(
                new TestOptionsProvider(OutputKind.Sql, null, fileMetadata: metadata)
            ),
            compilation
        );

        ParseInputs(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_scene.Users.Path] = IncrementalStepRunReason.Modified,
                    [_scene.Orders.Path] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.ClaimedPaths).ShouldBe([IncrementalStepRunReason.Cached]);
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
                    ["App.Orders.OrderQueries.g.cs"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        result
            .GeneratedTrees.Single(tree => tree.FilePath.EndsWith("UserQueries.g.cs", StringComparison.Ordinal))
            .ToString()
            .ShouldContain("SELECT 1; -- c");
    }
}
