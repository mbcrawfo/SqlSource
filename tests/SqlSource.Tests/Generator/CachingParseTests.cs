// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.CachingScene;
using static SqlSource.Testing.Generator.StepReasons;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The caching of the read and parse of the .sql files: what an edit to a .sql file or to C#, or a file added, makes
// the generator read and emit again.
public sealed class CachingParseTests
{
    private readonly CachingScene _scene = new();

    [Fact]
    public void Run_SqlFileEdited_ParsesOnlyThatFileAndEmitsOnlyTheTypeThatUsesIt()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRun(OutputKind.Sql, compilation);

        var edited = new InMemoryAdditionalText(_scene.Users.Path, "-- name: GetUser\nSELECT 1, 2;\n");
        var result = Run(driver.ReplaceAdditionalText(_scene.Users, edited), compilation);

        // Only the edited file was read: the other one, and the claimed paths, are the inputs the read had.
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
        AllReasons(result, TrackingNames.TypeFiles).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.CaseCollisions).ShouldBe([IncrementalStepRunReason.Cached]);
        result
            .GeneratedTrees.Single(tree => tree.FilePath.EndsWith("UserQueries.g.cs", StringComparison.Ordinal))
            .ToString()
            .ShouldContain("SELECT 1, 2;");
    }

    [Fact]
    public void Run_SqlFileEditedWithoutChangingItsQueries_EmitsNothingAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRun(OutputKind.Sql, compilation);

        var edited = new InMemoryAdditionalText(_scene.Users.Path, "-- name: GetUser\nSELECT 1; -- a new comment\n");
        var result = Run(driver.ReplaceAdditionalText(_scene.Users, edited), compilation);

        // The file was parsed again and gave an equal value, so nothing after the parse ran.
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.TypeOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void Run_MethodBodyEdited_TakesEveryStepFromThePreviousRun()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRun(OutputKind.Sql, compilation);

        var result = Run(driver, WithMethodBodyEdited(compilation));

        // The run saw the new compilation: the framework check ran again and gave the same answer.  The declaration
        // was read again too, and gave an equal value, so nothing after it ran.
        AllReasons(result, TrackingNames.SupportedFramework).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.NpgsqlProbe).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.MappedTypes).ShouldBe([IncrementalStepRunReason.Unchanged]);
        string[] steps =
        [
            TrackingNames.TargetTypes,
            TrackingNames.NestedClassCheck,
            TrackingNames.SqlPaths,
            TrackingNames.CaseCollisions,
            TrackingNames.UnsupportedLanguageVersion,
            TrackingNames.TypeFiles,
            TrackingNames.ClaimedPaths,
            TrackingNames.CommentPaths,
            TrackingNames.ProjectDialect,
            TrackingNames.ProjectSettings,
            TrackingNames.FileParseInput,
            TrackingNames.FileClaim,
            TrackingNames.ParsedFile,
            TrackingNames.ParsedFiles,
            TrackingNames.FileSettings,
            TrackingNames.FilesSettings,
            TrackingNames.TypeQueries,
            TrackingNames.TypeOutput,
            TrackingNames.SidecarInput,
            TrackingNames.SidecarFile,
            TrackingNames.Sidecars,
            TrackingNames.TypeNeeds,
            TrackingNames.FileNeeds,
            TrackingNames.SidecarCheckInput,
            TrackingNames.SidecarCheck,
            TrackingNames.UsesPostgresEntries,
            TrackingNames.MappedTypeNames,
            TrackingNames.FileTypesInput,
            TrackingNames.FileTypes,
        ];
        foreach (var step in steps)
        {
            AllReasons(result, step).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached, step);
        }

        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    // The declaration has a new member, so the names its nested class cannot take changed, and the check ran again.
    // The member does not take the name, so the check gave an equal type and nothing after it ran.
    [Fact]
    public void Run_MemberAdded_ChecksTheNestedClassAndRunsNothingAfterIt()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRun(OutputKind.Sql, compilation);

        var result = Run(
            driver,
            compilation.ReplaceSyntaxTree(
                compilation.SyntaxTrees.Single(tree => tree.FilePath == UsersSourcePath),
                CSharpSyntaxTree.ParseText(
                    UsersSource.Replace("{\n", "{\n    private static int _count;\n", StringComparison.Ordinal),
                    GeneratorHarness.ParseOptions,
                    UsersSourcePath,
                    cancellationToken: TestContext.Current.CancellationToken
                )
            )
        );

        Reasons<DeclaredType>(result, TrackingNames.TargetTypes, declared => declared.Type.Name)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["UserQueries"] = IncrementalStepRunReason.Modified,
                    ["OrderQueries"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        Reasons<TargetType>(result, TrackingNames.NestedClassCheck, type => type.Name)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["UserQueries"] = IncrementalStepRunReason.Unchanged,
                    ["OrderQueries"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.TypeFiles).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    // A project that references Npgsql and needs entries of postgres: the probe runs on the new compilation, gives an
    // equal value, and nothing after it runs again.
    [Fact]
    public void Run_CSharpEdit_RunsTheProbeAndNothingAfterIt()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var driver = _scene.FirstRunWithSidecars(compilation);
        var before = Outputs(driver.GetRunResult(), TrackingNames.NpgsqlProbe).ShouldHaveSingleItem().Value;
        (
            (bool)Outputs(driver.GetRunResult(), TrackingNames.UsesPostgresEntries).ShouldHaveSingleItem().Value
        ).ShouldBeTrue();

        var result = Run(driver, WithMethodBodyEdited(compilation));

        var (probe, reason) = Outputs(result, TrackingNames.NpgsqlProbe).ShouldHaveSingleItem();
        reason.ShouldBe(IncrementalStepRunReason.Unchanged);
        probe.ShouldBe(before);
        AllReasons(result, TrackingNames.UsesPostgresEntries)
            .ShouldHaveSingleItem()
            .ShouldBeOneOf(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged);
        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void Run_SqlFileAddedThatNoTypeClaims_ReadsNoFileAndEmitsNothingAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRun(OutputKind.Sql, compilation);

        var result = Run(
            driver.AddAdditionalTexts([new InMemoryAdditionalText("/app/Migrations/001_init.sql", "SELECT 'x;\n")]),
            compilation
        );

        AllReasons(result, TrackingNames.SqlPaths).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.TypeFiles).ShouldAllBe(reason => reason == IncrementalStepRunReason.Unchanged);

        // The files that were read before were not read again: each is the input it was, and so are the claimed
        // paths.  The new file is not listed, here or under ParsedFile: the read gives nothing for a file that no type
        // claims, and the driver tracks a step only when something it gave reaches an output.
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
        Reasons<ParsedSqlFile>(result, TrackingNames.ParsedFile, file => file.FileName)
            .Keys.ShouldBe(["Users.sql", "Orders.sql"], ignoreOrder: true);
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.TypeOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Run_SqlFileAddedWhosePathDiffersOnlyByCase_ReportsItAndEmitsNothingAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRun(OutputKind.Sql, compilation);

        var result = Run(
            driver.AddAdditionalTexts([new InMemoryAdditionalText("/app/Users/users.sql", "SELECT 3;\n")]),
            compilation
        );

        // The type keeps the file it had, so nothing about it is worked out again.
        AllReasons(result, TrackingNames.CaseCollisions).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.SqlPaths).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.TypeFiles).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.TypeOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        result.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("SQLSRC013");
    }

    [Fact]
    public void Run_SqlFileAddedToATypesFolder_EmitsOnlyThatTypeAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRun(OutputKind.Sql, compilation);

        var result = Run(
            driver.AddAdditionalTexts([new InMemoryAdditionalText("/app/Orders/Count.sql", "SELECT 3;\n")]),
            compilation
        );

        Reasons<TypeOutput>(result, TrackingNames.TypeOutput, output => output.HintName)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Users.UserQueries.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Orders.OrderQueries.g.cs"] = IncrementalStepRunReason.Modified,
                },
                ignoreOrder: true
            );
        Reasons<ParsedSqlFile>(result, TrackingNames.ParsedFile, file => file.FileName)["Users.sql"]
            .ShouldBe(IncrementalStepRunReason.Cached);
    }

    // A file added to a folder a type claims changes the claimed paths.  Every file's claim is worked out again, and
    // only the added file's is new, so it alone is read.
    [Fact]
    public void Run_SqlFileAddedToAClaimedFolder_ReadsOnlyThatFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = _scene.FirstRun(OutputKind.Sql, compilation);
        var count = new InMemoryAdditionalText("/app/Orders/Count.sql", "SELECT 3;\n");

        var result = Run(driver.AddAdditionalTexts([count]), compilation);

        AllReasons(result, TrackingNames.ClaimedPaths).ShouldBe([IncrementalStepRunReason.Modified]);
        Claims(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_scene.Users.Path] = IncrementalStepRunReason.Unchanged,
                    [_scene.Orders.Path] = IncrementalStepRunReason.Unchanged,
                    [count.Path] = IncrementalStepRunReason.New,
                },
                ignoreOrder: true
            );
        Reasons<ParsedSqlFile>(result, TrackingNames.ParsedFile, file => file.FileName)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["Users.sql"] = IncrementalStepRunReason.Cached,
                    ["Orders.sql"] = IncrementalStepRunReason.Cached,
                    ["Count.sql"] = IncrementalStepRunReason.New,
                },
                ignoreOrder: true
            );
    }
}
