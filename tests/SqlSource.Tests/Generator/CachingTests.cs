// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Models;
using SqlSource.Generation.Sidecars;
using SqlSource.Generation.Types;
using SqlSource.Parsing;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The generator runs again on every edit in the IDE.  These tests pin what an edit makes it redo: each step of the
// pipeline reports, for each of its outputs, whether it was taken from the previous run.
public sealed class CachingTests
{
    private const string UsersSourcePath = "/app/Users/UserQueries.cs";

    private const string UsersSource = """
        using SqlSource;

        namespace App.Users;

        [SqlSourceGenerate]
        public partial class UserQueries
        {
            public static string Get() => Sql.GetUser;
        }
        """;

    private static readonly SourceFile[] Sources =
    [
        new(UsersSourcePath, UsersSource),
        new(
            "/app/Orders/OrderQueries.cs",
            """
            using SqlSource;

            namespace App.Orders;

            [SqlSourceGenerate]
            public partial class OrderQueries;
            """
        ),
    ];

    private readonly InMemoryAdditionalText _users = new("/app/Users/Users.sql", "-- name: GetUser\nSELECT 1;\n");

    private readonly InMemoryAdditionalText _orders = new("/app/Orders/Orders.sql", "-- name: GetOrder\nSELECT 2;\n");

    // The fixture of the tests of sidecars: a current sidecar beside each file, which a run at Models under postgres
    // reads.
    private readonly InMemoryAdditionalText _usersSidecar;

    private readonly InMemoryAdditionalText _ordersSidecar;

    // A third file for OrderQueries, which the tests of the models use, with its sidecar of one column, of int4.
    private readonly InMemoryAdditionalText _payments = new(
        "/app/Orders/Payments.sql",
        "-- name: GetPayment\nSELECT 3;\n"
    );

    private readonly InMemoryAdditionalText _paymentsSidecar;

    public CachingTests()
    {
        _usersSidecar = CurrentSidecar(_users, "GetUser");
        _ordersSidecar = CurrentSidecar(_orders, "GetOrder");
        _paymentsSidecar = TypedSidecar(_payments, "GetPayment", "int4");
    }

    [Fact]
    public void Run_SqlFileEdited_ParsesOnlyThatFileAndEmitsOnlyTheTypeThatUsesIt()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = FirstRun(OutputKind.Sql, compilation);

        var edited = new InMemoryAdditionalText(_users.Path, "-- name: GetUser\nSELECT 1, 2;\n");
        var result = Run(driver.ReplaceAdditionalText(_users, edited), compilation);

        // Only the edited file was read: the other one, and the claimed paths, are the inputs the read had.
        ParseInputs(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_users.Path] = IncrementalStepRunReason.Modified,
                    [_orders.Path] = IncrementalStepRunReason.Cached,
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
        var driver = FirstRun(OutputKind.Sql, compilation);

        var edited = new InMemoryAdditionalText(_users.Path, "-- name: GetUser\nSELECT 1; -- a new comment\n");
        var result = Run(driver.ReplaceAdditionalText(_users, edited), compilation);

        // The file was parsed again and gave an equal value, so nothing after the parse ran.
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.TypeOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void Run_MethodBodyEdited_TakesEveryStepFromThePreviousRun()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = FirstRun(OutputKind.Sql, compilation);

        var result = Run(driver, WithMethodBodyEdited(compilation));

        // The run saw the new compilation: the framework check ran again and gave the same answer.  The declaration
        // was read again too, and gave an equal value, so nothing after it ran.
        AllReasons(result, TrackingNames.SupportedFramework).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.NpgsqlProbe).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.MappedTypes).ShouldBe([IncrementalStepRunReason.Unchanged]);
        string[] steps =
        [
            TrackingNames.TargetTypes,
            TrackingNames.SqlPaths,
            TrackingNames.CaseCollisions,
            TrackingNames.UnsupportedLanguageVersion,
            TrackingNames.TypeFiles,
            TrackingNames.ClaimedPaths,
            TrackingNames.CommentPaths,
            TrackingNames.ProjectDialect,
            TrackingNames.ProjectSettings,
            TrackingNames.FileParseInput,
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

    // A project that references Npgsql and needs entries of postgres: the probe runs on the new compilation, gives an
    // equal value, and nothing after it runs again.
    [Fact]
    public void Run_CSharpEdit_RunsTheProbeAndNothingAfterIt()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var driver = FirstRunWithSidecars(compilation);
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
        var driver = FirstRun(OutputKind.Sql, compilation);

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
                    [_users.Path] = IncrementalStepRunReason.Cached,
                    [_orders.Path] = IncrementalStepRunReason.Cached,
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
        var driver = FirstRun(OutputKind.Sql, compilation);

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
        var driver = FirstRun(OutputKind.Sql, compilation);

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

    [Fact]
    public void Run_GeneratorParametersPropertyChanged_EmitsEveryTypeAgainAndParsesNoFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var users = new InMemoryAdditionalText(
            _users.Path,
            "-- name: GetUser\nSELECT 1;\n-- name: ListFrom\nSELECT * FROM {{table}};\n"
        );
        var driver = FirstRun(compilation, users, new TestOptionsProvider(OutputKind.Sql, null));

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
                    [_users.Path] = IncrementalStepRunReason.Cached,
                    [_orders.Path] = IncrementalStepRunReason.Cached,
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

    [Fact]
    public void Run_OptionsReplacedWithoutChangingTheProperty_EmitsNothingAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = FirstRun(OutputKind.Sql, compilation);

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
        var users = new InMemoryAdditionalText(_users.Path, "-- name: GetUser\nSELECT 1; # c\n");
        var orderDialect = new Dictionary<string, string> { [_orders.Path] = "mssql" };
        var driver = FirstRun(compilation, users, new TestOptionsProvider(OutputKind.Sql, null, null, orderDialect));

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
                    [_users.Path] = IncrementalStepRunReason.Modified,
                    [_orders.Path] = IncrementalStepRunReason.Unchanged,
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
        var users = new InMemoryAdditionalText(_users.Path, "-- name: GetUser\nSELECT 1; # c\n");
        var driver = FirstRun(
            compilation,
            users,
            new TestOptionsProvider(
                OutputKind.Sql,
                null,
                null,
                new Dictionary<string, string> { [_users.Path] = "mssql" }
            )
        );

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(
                new TestOptionsProvider(
                    OutputKind.Sql,
                    null,
                    null,
                    new Dictionary<string, string> { [_users.Path] = "mysql" }
                )
            ),
            compilation
        );

        AllReasons(result, TrackingNames.ProjectDialect).ShouldBe([IncrementalStepRunReason.Unchanged]);
        ParseInputs(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_users.Path] = IncrementalStepRunReason.Modified,
                    [_orders.Path] = IncrementalStepRunReason.Cached,
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
        var driver = FirstRun(OutputKind.Sql, compilation);

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
        var users = new InMemoryAdditionalText(_users.Path, "-- name: GetUser\nSELECT 1; -- c\n");
        var driver = FirstRun(compilation, users, new TestOptionsProvider(OutputKind.Sql, null));

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
        var driver = FirstRun(OutputKind.Sql, compilation);

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
                    [_users.Path] = IncrementalStepRunReason.Cached,
                    [_orders.Path] = IncrementalStepRunReason.Cached,
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
        var driver = FirstRun(OutputKind.Sql, compilation);
        var metadata = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [_users.Path] = new Dictionary<string, string> { ["SqlSourceGeneratorParameters"] = "sort-input" },
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
                    [_users.Path] = IncrementalStepRunReason.Unchanged,
                    [_orders.Path] = IncrementalStepRunReason.Cached,
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
        var driver = FirstRun(OutputKind.Sql, compilation);

        var result = Run(driver, WithParametersOnUserQueries(compilation, "keep-comments"));

        AllReasons(result, TrackingNames.CommentPaths).ShouldBe([IncrementalStepRunReason.Modified]);

        // Users.sql is read again, as it has to be: its type now asks for comments.  Orders.sql is not: the step that
        // resolves its input ran, because the list of paths it takes changed, and gave the value it had.
        ParseInputs(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_users.Path] = IncrementalStepRunReason.Modified,
                    [_orders.Path] = IncrementalStepRunReason.Unchanged,
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
        var driver = FirstRun(OutputKind.Sql, compilation);

        var result = Run(driver, WithParametersOnUserQueries(compilation, "sort-input"));

        AllReasons(result, TrackingNames.CommentPaths).ShouldBe([IncrementalStepRunReason.Cached]);

        // No file was read.
        ParseInputs(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_users.Path] = IncrementalStepRunReason.Cached,
                    [_orders.Path] = IncrementalStepRunReason.Cached,
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
        var users = new InMemoryAdditionalText(_users.Path, "-- name: GetUser\nSELECT 1; -- c\n");
        var driver = FirstRun(compilation, users, new TestOptionsProvider(OutputKind.Sql, null));
        var metadata = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [_users.Path] = new Dictionary<string, string> { ["SqlSourceGeneratorParameters"] = "keep-comments" },
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
                    [_users.Path] = IncrementalStepRunReason.Modified,
                    [_orders.Path] = IncrementalStepRunReason.Cached,
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

    [Fact]
    public void Run_SidecarEdited_ReadsOnlyThatSidecarAndParsesNoFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = FirstRunWithSidecars(compilation);

        var stale = Sidecars.Of(ToSqlFile(_users), Sidecars.Entry("GetUser", "stale"));
        var edited = new InMemoryAdditionalText(stale.Path, stale.Text);
        var result = Run(driver.ReplaceAdditionalText(_usersSidecar, edited), compilation);

        Reasons<SidecarFile>(result, TrackingNames.SidecarFile, file => file.SqlPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.Sidecars).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.FileParseInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.TypeOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void Run_SqlFileEdited_ReadsNoSidecar()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = FirstRunWithSidecars(compilation);

        var edited = new InMemoryAdditionalText(_users.Path, "-- name: GetUser\nSELECT 1, 2;\n");
        var result = Run(driver.ReplaceAdditionalText(_users, edited), compilation);

        Reasons<ParsedSqlFile>(result, TrackingNames.ParsedFile, file => file.FileName)["Users.sql"]
            .ShouldBe(IncrementalStepRunReason.Modified);
        Reasons<SidecarInput>(result, TrackingNames.SidecarInput, input => input.SqlPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        Reasons<SidecarFile>(result, TrackingNames.SidecarFile, file => file.SqlPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.Sidecars).ShouldBe([IncrementalStepRunReason.Cached]);
    }

    [Fact]
    public void Run_MethodBodyEdited_ReadsNoSidecar()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = FirstRunWithSidecars(compilation);

        var result = Run(driver, WithMethodBodyEdited(compilation));

        Reasons<SidecarInput>(result, TrackingNames.SidecarInput, input => input.SqlPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        Reasons<SidecarFile>(result, TrackingNames.SidecarFile, file => file.SqlPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.Sidecars).ShouldBe([IncrementalStepRunReason.Cached]);
        Reasons<EquatableArray<FileNeeds>>(result, TrackingNames.TypeNeeds, NeedsKey)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        Checks(result).Values.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    // A sidecar with no .sql file beside it, which no type can claim.
    [Fact]
    public void Run_SidecarAddedForAFileNoTypeClaims_ReadsNothing()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = FirstRunWithSidecars(compilation);

        var result = Run(
            driver.AddAdditionalTexts([new InMemoryAdditionalText("/app/Migrations/001_init.sql.json", "{\n")]),
            compilation
        );

        // The new sidecar is not listed, here or under SidecarFile: its input is null, so nothing is read for it, and
        // the driver tracks a step only when something it gave reaches an output.  The others are the inputs they
        // were.
        Reasons<SidecarInput>(result, TrackingNames.SidecarInput, input => input.SqlPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        Reasons<SidecarFile>(result, TrackingNames.SidecarFile, file => file.SqlPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.Sidecars).ShouldBe([IncrementalStepRunReason.Cached]);
        result.Diagnostics.ShouldBeEmpty();
    }

    // Two items of one path, as a project that lists a sidecar itself beside the package's glob would give.  Each
    // item is an input of its own, and the collected value keeps the first, so every step after it sees the file once.
    [Fact]
    public void Run_SameSidecarListedTwice_KeepsOneSidecar()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var again = new InMemoryAdditionalText(
            _usersSidecar.Path,
            _usersSidecar.GetText(TestContext.Current.CancellationToken)!.ToString()
        );

        var result = GeneratorHarness
            .CreateDriver(
                [_users, _orders, _usersSidecar, _ordersSidecar, again],
                new TestOptionsProvider(OutputKind.Models, null, "postgres")
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken)
            .GetRunResult();

        var sidecars =
            (EquatableArray<SidecarFile>)Outputs(result, TrackingNames.Sidecars).ShouldHaveSingleItem().Value;
        sidecars.Select(file => file.SqlPath).ShouldBe(["app/Orders/Orders.sql", "app/Users/Users.sql"]);
    }

    [Fact]
    public void Run_SqlFileEdited_ChecksOnlyThatFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = FirstRunWithSidecars(compilation);

        var edited = new InMemoryAdditionalText(_users.Path, "-- name: GetUser\nSELECT 1, 2;\n");
        var result = Run(driver.ReplaceAdditionalText(_users, edited), compilation);

        // The type's needs were found again and are the same, so only the edited file was checked, and its entry is
        // now stale.
        Reasons<EquatableArray<FileNeeds>>(result, TrackingNames.TypeNeeds, NeedsKey)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Unchanged,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.FileNeeds).ShouldBe([IncrementalStepRunReason.Cached]);
        Checks(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        result.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("SQLSRC244");
    }

    [Fact]
    public void Run_SidecarEdited_ChecksOnlyItsFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = FirstRunWithSidecars(compilation);

        var stale = Sidecars.Of(ToSqlFile(_users), Sidecars.Entry("GetUser", "stale"));
        var edited = new InMemoryAdditionalText(stale.Path, stale.Text);
        var result = Run(driver.ReplaceAdditionalText(_usersSidecar, edited), compilation);

        // Every file gathered its input again, and only the one whose sidecar changed has another.
        Reasons<SidecarCheckInput>(result, TrackingNames.SidecarCheckInput, input => input.File.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        Checks(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.TypeNeeds).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        result.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("SQLSRC244");
    }

    // TD-0019: the check of each file is a node of its own, but the nodes are matched by their place in the sorted
    // list of parsed files, not by path.  So a claimed file added to the list checks again every file sorted after
    // it, and none before it: Payments takes the place Users had, and Users is checked as a new node after it.
    [Fact]
    public void Run_ClaimedSqlFileAdded_ChecksTheFilesSortedAfterIt()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = FirstRunWithSidecars(compilation);
        var payments = new InMemoryAdditionalText("/app/Orders/Payments.sql", "-- name: GetPayment\nSELECT 3;\n");

        var result = Run(driver.AddAdditionalTexts([payments, CurrentSidecar(payments, "GetPayment")]), compilation);

        Checks(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                    ["app/Orders/Payments.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.New,
                },
                ignoreOrder: true
            );
        result.Diagnostics.ShouldBeEmpty();
    }

    // The attribute changes the type, so its needs are found again, and they are the same.
    [Fact]
    public void Run_AttributeEditedWithoutChangingTheOutput_ChecksNothing()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = FirstRunWithSidecars(compilation);

        var result = Run(driver, WithArgumentsOnUserQueries(compilation, "ModelNamespace = \"App.Models\""));

        Reasons<TypeQueries>(result, TrackingNames.TypeQueries, type => type.HintName)["App.Users.UserQueries.g.cs"]
            .ShouldBe(IncrementalStepRunReason.Modified);
        Reasons<EquatableArray<FileNeeds>>(result, TrackingNames.TypeNeeds, NeedsKey)["app/Users/Users.sql"]
            .ShouldBe(IncrementalStepRunReason.Unchanged);
        AllReasons(result, TrackingNames.FileNeeds)
            .ShouldAllBe(reason =>
                reason == IncrementalStepRunReason.Unchanged || reason == IncrementalStepRunReason.Cached
            );
        Checks(result).Values.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void Run_ModelSuffixPropertyChanged_ChecksNothing()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = FirstRunWithSidecars(compilation);

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(
                new TestOptionsProvider(
                    OutputKind.Models,
                    null,
                    "postgres",
                    properties: new Dictionary<string, string?> { ["SqlSourceOutputModelSuffix"] = "Row" }
                )
            ),
            compilation
        );

        // Every type's needs were found again, under the new property, and are the same.
        AllReasons(result, TrackingNames.ProjectSettings).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.TypeNeeds).ShouldAllBe(reason => reason == IncrementalStepRunReason.Unchanged);
        AllReasons(result, TrackingNames.FileNeeds).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.SidecarCheckInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        Checks(result).Values.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);

        // The types read the property's type override alone, which did not change.
        AllReasons(result, TrackingNames.FileTypesInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    // A project at Sql with no sidecar: no query needs an entry, so no check has anything to do and no hash is
    // computed.
    [Fact]
    public void Run_ProjectAtSql_ComputesNoHash()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);

        var result = FirstRun(OutputKind.Sql, compilation).GetRunResult();

        (
            (EquatableArray<FileNeeds>)Outputs(result, TrackingNames.FileNeeds).ShouldHaveSingleItem().Value
        ).ShouldBeEmpty();
        Outputs(result, TrackingNames.SidecarCheck)
            .Select(output => (SidecarCheckResult)output.Value)
            .ShouldAllBe(check => check.Diagnostics.Count == 0 && check.UsableEntries.Count == 0);
    }

    // A project whose property maps a type: the resolution runs on the new compilation, gives an equal value, and
    // nothing after it runs again.
    [Fact]
    public void Run_CSharpEdit_ResolvesTheMappedTypesAndNothingAfterThem()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = FirstRun(
            compilation,
            _users,
            new TestOptionsProvider(
                OutputKind.Sql,
                null,
                properties: new Dictionary<string, string?> { ["SqlSourceTypeOverrides"] = "uuid=System.Guid" }
            )
        );
        var before = Outputs(driver.GetRunResult(), TrackingNames.MappedTypes).ShouldHaveSingleItem().Value;
        ((EquatableArray<MappedType>)before).ShouldHaveSingleItem().Name.ShouldBe("global::System.Guid");

        var result = Run(driver, WithMethodBodyEdited(compilation));

        AllReasons(result, TrackingNames.MappedTypeNames).ShouldBe([IncrementalStepRunReason.Cached]);
        var (types, reason) = Outputs(result, TrackingNames.MappedTypes).ShouldHaveSingleItem();
        reason.ShouldBe(IncrementalStepRunReason.Unchanged);
        types.ShouldBe(before);
        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void Run_NoOverride_ResolvesNothing()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);

        var result = FirstRun(OutputKind.Sql, compilation).GetRunResult();

        (
            (EquatableArray<string>)Outputs(result, TrackingNames.MappedTypeNames).ShouldHaveSingleItem().Value
        ).ShouldBeEmpty();
        (
            (EquatableArray<MappedType>)Outputs(result, TrackingNames.MappedTypes).ShouldHaveSingleItem().Value
        ).ShouldBeEmpty();
    }

    // The query's marker names a type.  An edit of its SQL parses the file again and leaves the names as they were.
    // The driver hands the generator a new compilation on every run, a .sql edit's too, so the resolution runs again
    // as the probe does, and gives an equal value: nothing after it runs.
    [Fact]
    public void Run_SqlEditThatLeavesTheMarkersAlone_ChangesNoMappedType()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var users = new InMemoryAdditionalText(
            _users.Path,
            "-- name: GetUser\n-- type-override: uuid = System.Guid\nSELECT 1;\n"
        );
        var driver = FirstRun(compilation, users, new TestOptionsProvider(OutputKind.Sql, null));

        var edited = new InMemoryAdditionalText(
            _users.Path,
            "-- name: GetUser\n-- type-override: uuid = System.Guid\nSELECT 1, 2;\n"
        );
        var result = Run(driver.ReplaceAdditionalText(users, edited), compilation);

        AllReasons(result, TrackingNames.ParsedFiles).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.MappedTypeNames).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.SupportedFramework).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.MappedTypes).ShouldBe([IncrementalStepRunReason.Unchanged]);
        ((EquatableArray<string>)Outputs(result, TrackingNames.MappedTypeNames).ShouldHaveSingleItem().Value).ShouldBe([
            "global::System.Guid",
        ]);
    }

    // The probe and the resolution run on the new compilation and give equal values, so no file is typed again.
    [Fact]
    public void Run_CSharpEdit_TypesNoQuery()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var driver = FirstRunWithTypes(compilation, OptionsWithTypeOverrides(null));
        Outputs(driver.GetRunResult(), TrackingNames.FileTypes)
            .Select(output => (FileTypes)output.Value)
            .ShouldAllBe(types => types.Queries.Count == 1);

        var result = Run(driver, WithMethodBodyEdited(compilation));

        AllReasons(result, TrackingNames.FileTypesInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.FileTypes).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    // The edit makes the entry of Users stale, so Users has no types now, and Orders is not typed again.
    [Fact]
    public void Run_SqlFileEdited_TypesOnlyThatFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var driver = FirstRunWithTypes(compilation, OptionsWithTypeOverrides(null));

        var edited = new InMemoryAdditionalText(_users.Path, "-- name: GetUser\nSELECT 1, 2;\n");
        var result = Run(driver.ReplaceAdditionalText(_users, edited), compilation);

        // Every file gathered its input again, since the parsed files changed, and Orders' is equal.
        Reasons<FileTypesInput>(result, TrackingNames.FileTypesInput, input => input.Check.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        TypesOf(result, "app/Users/Users.sql").Queries.ShouldBeEmpty();
    }

    // The new entry of Users has a column of text, and Orders is not typed again.
    [Fact]
    public void Run_SidecarEdited_TypesOnlyItsFile()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var usersSidecar = TypedSidecar(_users, "GetUser", "int4");
        var driver = FirstRunWithTypes(compilation, OptionsWithTypeOverrides(null), usersSidecar);

        var edited = TypedSidecar(_users, "GetUser", "text");
        var result = Run(driver.ReplaceAdditionalText(usersSidecar, edited), compilation);

        // Orders' check was taken from the previous run, and so was every other input of its types.
        Reasons<FileTypesInput>(result, TrackingNames.FileTypesInput, input => input.Check.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        TypesOf(result, "app/Users/Users.sql")
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("string");
    }

    // Each file's map of overrides comes from the property, so every file is typed again, and each column of int4
    // now has the property's type.
    [Fact]
    public void Run_TypeOverridesPropertyChanged_TypesEveryFileAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var driver = FirstRunWithTypes(compilation, OptionsWithTypeOverrides(null));

        var result = Run(driver.WithUpdatedAnalyzerConfigOptions(OptionsWithTypeOverrides("int4=long")), compilation);

        AllReasons(result, TrackingNames.FileTypesInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Modified);
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Modified,
                },
                ignoreOrder: true
            );
        TypesOf(result, "app/Orders/Orders.sql")
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("long");
    }

    // The probe and the resolution of the property's one type run on the new compilation and give equal values, so
    // a project with a type override types no file again either.
    [Fact]
    public void Run_MethodBodyEditedWithATypeOverride_TypesNoQuery()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var driver = FirstRunWithTypes(compilation, OptionsWithTypeOverrides("int4=long"));

        var result = Run(driver, WithMethodBodyEdited(compilation));

        AllReasons(result, TrackingNames.MappedTypes).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.FileTypesInput)
            .ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.FileTypes).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        TypesOf(result, "app/Users/Users.sql")
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("long");
    }

    // The property maps int4 to a type the project does not have, so Users, whose column is of int4, is SQLSRC250.
    // A new file declares the type: its resolution changes, and Users is typed again without the error.  Every file
    // with a usable entry carries the resolved types, so Orders, whose column is of text, is typed again too, and
    // gives an equal value, which nothing after it reads again.
    [Fact]
    public void Run_CSharpEditAddsTheMappedType_TypesTheFileThatUsesItAnew()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);
        var driver = GeneratorHarness
            .CreateDriver(
                [_users, _orders, TypedSidecar(_users, "GetUser", "int4"), TypedSidecar(_orders, "GetOrder", "text")],
                OptionsWithTypeOverrides("int4=MyApp.Missing")
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.Select(diagnostic => diagnostic.Id).ShouldBe(["SQLSRC250"]);

        var result = Run(
            driver,
            compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText(
                    "namespace MyApp;\n\npublic sealed class Missing;\n",
                    GeneratorHarness.ParseOptions,
                    "/app/MyApp/Missing.cs",
                    cancellationToken: TestContext.Current.CancellationToken
                )
            )
        );

        AllReasons(result, TrackingNames.MappedTypes).ShouldBe([IncrementalStepRunReason.Modified]);
        Reasons<FileTypes>(result, TrackingNames.FileTypes, file => file.NormalizedPath)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["app/Users/Users.sql"] = IncrementalStepRunReason.Modified,
                    ["app/Orders/Orders.sql"] = IncrementalStepRunReason.Unchanged,
                },
                ignoreOrder: true
            );
        result.Diagnostics.ShouldBeEmpty();
        TypesOf(result, "app/Users/Users.sql")
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("global::MyApp.Missing");
    }

    // Without Npgsql, date has no C# type, SQLSRC249.  The reference changes the probe, and every file is typed
    // again, with the type of Npgsql 10.
    [Fact]
    public void Run_NpgsqlReferenceAdded_TypesEveryFileAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = GeneratorHarness
            .CreateDriver(
                [_users, _orders, TypedSidecar(_users, "GetUser", "date"), TypedSidecar(_orders, "GetOrder", "date")],
                OptionsWithTypeOverrides(null)
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.Select(diagnostic => diagnostic.Id).ShouldBe(["SQLSRC249", "SQLSRC249"]);

        var result = Run(driver, compilation.AddReferences(NpgsqlReferences.Npgsql10));

        AllReasons(result, TrackingNames.NpgsqlProbe).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.FileTypes).ShouldAllBe(reason => reason == IncrementalStepRunReason.Modified);
        result.Diagnostics.ShouldBeEmpty();
        TypesOf(result, "app/Orders/Orders.sql")
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldNotBeNull()
            .ShouldHaveSingleItem()
            .Text.ShouldBe("global::System.DateOnly");
    }

    // The three files of the models tests: Orders and Payments are claimed by OrderQueries and Users by UserQueries,
    // so the models are App.Orders.GetOrderDto, App.Orders.GetPaymentDto and App.Users.GetUserDto, in that order.
    // Each file has its own model, with one column, of int4.
    [Fact]
    public void Run_SqlFileEditedChangingOneModel_EmitsOnlyThatModelAgain()
    {
        var compilation = ModelsCompilation();
        var ordersSidecar = TypedSidecar(_orders, "GetOrder", "int4");
        var driver = FirstRunOfModels(compilation, _users, _orders, ordersSidecar);

        // The column of Orders is now text.
        var result = Run(
            driver.ReplaceAdditionalText(ordersSidecar, TypedSidecar(_orders, "GetOrder", "text")),
            compilation
        );

        ModelReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Orders.GetOrderDto-Model.g.cs"] = IncrementalStepRunReason.Modified,
                    ["App.Orders.GetPaymentDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Users.GetUserDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.Models).ShouldBe([IncrementalStepRunReason.Modified]);

        // The names are the same as before.  The lookup in the project still ran, since the driver gives every run a
        // new compilation, and it gave the same answer.
        AllReasons(result, TrackingNames.ModelNames).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.TakenModelNames).ShouldBe([IncrementalStepRunReason.Unchanged]);
        result
            .GeneratedTrees.Single(tree => tree.FilePath.EndsWith("GetOrderDto-Model.g.cs", StringComparison.Ordinal))
            .ToString()
            .ShouldContain("GetOrderDto(string Value)");
    }

    // A -- column: marker is a comment, so the query's hash, and so its sidecar, stays current: only Users is parsed
    // again, and only its model is emitted again, with the column nullable.
    [Fact]
    public void Run_ColumnMarkerAdded_ParsesOnlyThatFileAndEmitsOnlyItsModelAgain()
    {
        var compilation = ModelsCompilation();
        var driver = FirstRunOfModels(compilation, _users, _orders);

        var edited = new InMemoryAdditionalText(_users.Path, "-- name: GetUser\n-- column: value null\nSELECT 1;\n");
        var result = Run(driver.ReplaceAdditionalText(_users, edited), compilation);

        result.Diagnostics.ShouldBeEmpty();
        ParseInputs(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    [_users.Path] = IncrementalStepRunReason.Modified,
                    [_orders.Path] = IncrementalStepRunReason.Cached,
                    [_payments.Path] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        ModelReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Orders.GetOrderDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Orders.GetPaymentDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Users.GetUserDto-Model.g.cs"] = IncrementalStepRunReason.Modified,
                },
                ignoreOrder: true
            );
        result
            .GeneratedTrees.Single(tree => tree.FilePath.EndsWith("GetUserDto-Model.g.cs", StringComparison.Ordinal))
            .ToString()
            .ShouldContain("GetUserDto(int? Value)");
    }

    // The model App.Shared.Row is the marker's in Orders and Users alike.  Orders comes first, so its request is the
    // model.  Whichever file is edited, the two now differ, which is SQLSRC257 at Users, and the model is the request
    // of Orders alone, so its summary no longer names Users.  The edit of Orders changes the model's property too;
    // the edit of Users changes the model's summary alone.  The model of Payments is no part of it.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_SharedModelEdited_EmitsOnlyThatModelAgain(bool editsTheFileThatGivesIt)
    {
        var compilation = ModelsCompilation();
        var users = new InMemoryAdditionalText(
            _users.Path,
            "-- name: GetUser\n-- output-model: App.Shared.Row\nSELECT 1;\n"
        );
        var orders = new InMemoryAdditionalText(
            _orders.Path,
            "-- name: GetOrder\n-- output-model: App.Shared.Row\nSELECT 2;\n"
        );
        var usersSidecar = TypedSidecar(users, "GetUser", "int4");
        var ordersSidecar = TypedSidecar(orders, "GetOrder", "int4");
        var driver = FirstRunOfModels(compilation, users, orders, usersSidecar, ordersSidecar);
        var before = driver.GetRunResult().GeneratedTrees.Single(IsShared).ToString();
        before.ShouldContain("queries <c>GetOrder</c> from <c>Orders.sql</c> and <c>GetUser</c> from <c>Users.sql</c>");
        before.ShouldContain("Row(int Value)");

        var result = Run(
            editsTheFileThatGivesIt
                ? driver.ReplaceAdditionalText(ordersSidecar, TypedSidecar(orders, "GetOrder", "text"))
                : driver.ReplaceAdditionalText(usersSidecar, TypedSidecar(users, "GetUser", "text")),
            compilation
        );

        ModelReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Orders.GetPaymentDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Shared.Row-Model.g.cs"] = IncrementalStepRunReason.Modified,
                },
                ignoreOrder: true
            );
        var diagnostic = result.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("SQLSRC257");
        diagnostic.Location.GetLineSpan().Path.ShouldBe(_users.Path);
        var after = result.GeneratedTrees.Single(IsShared).ToString();
        after.ShouldContain("A row of the query <c>GetOrder</c> from <c>Orders.sql</c>.");
        after.ShouldNotContain("GetUser");
        after.ShouldContain(editsTheFileThatGivesIt ? "Row(string Value)" : "Row(int Value)");
        static bool IsShared(SyntaxTree tree) => tree.FilePath.EndsWith("Row-Model.g.cs", StringComparison.Ordinal);
    }

    // The accepted cost of SelectMany (TD-0060): its nodes are matched by their place in the list, not by name, and
    // the list is sorted by the models' full names.  A model added sorted first takes the place of the one that was
    // first, and every model after it moves down a place, so each is emitted again.  This pins the cost so that it is
    // not made worse unseen.
    [Fact]
    public void Run_ModelAdded_EmitsTheModelsAfterItAgain()
    {
        var compilation = ModelsCompilation();
        var driver = FirstRunOfModels(compilation, _users, _orders);
        var archive = new InMemoryAdditionalText("/app/Orders/Archive.sql", "-- name: GetArchive\nSELECT 4;\n");

        var result = Run(
            driver.AddAdditionalTexts([archive, TypedSidecar(archive, "GetArchive", "int4")]),
            compilation
        );

        ModelReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Orders.GetArchiveDto-Model.g.cs"] = IncrementalStepRunReason.Modified,
                    ["App.Orders.GetOrderDto-Model.g.cs"] = IncrementalStepRunReason.Modified,
                    ["App.Orders.GetPaymentDto-Model.g.cs"] = IncrementalStepRunReason.Modified,
                    ["App.Users.GetUserDto-Model.g.cs"] = IncrementalStepRunReason.New,
                },
                ignoreOrder: true
            );
    }

    // The same cost for a model that goes: the models before it are left alone, and each one after it takes the place
    // of the one before it.
    [Fact]
    public void Run_ModelRemoved_EmitsTheModelsAfterItAgain()
    {
        var compilation = ModelsCompilation();
        var driver = FirstRunOfModels(compilation, _users, _orders);

        var result = Run(driver.RemoveAdditionalTexts([_payments, _paymentsSidecar]), compilation);

        ModelReasons(result)
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Orders.GetOrderDto-Model.g.cs"] = IncrementalStepRunReason.Cached,
                    ["App.Users.GetUserDto-Model.g.cs"] = IncrementalStepRunReason.Modified,
                },
                ignoreOrder: true
            );
        ModelsRemoved(result).ShouldBe(["App.Users.GetUserDto-Model.g.cs"]);
    }

    // The edit gives the pipeline a new compilation: the names the project has are looked up again and found the
    // same, so the models are not worked out again, and none is emitted.
    [Fact]
    public void Run_CSharpEdit_EmitsNoModelAgain()
    {
        var compilation = ModelsCompilation();
        var driver = FirstRunOfModels(compilation, _users, _orders);

        var result = Run(driver, WithMethodBodyEdited(compilation));

        AllReasons(result, TrackingNames.TakenModelNames).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.Models).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.ModelOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        ModelReasons(result).Count.ShouldBe(3);
        OutputReasons(result).Distinct().ShouldBe([IncrementalStepRunReason.Cached]);
    }

    // No model reads the collection type, so the requests of each type come out equal and nothing after them runs.
    [Fact]
    public void Run_CollectionTypePropertyChanged_EmitsNoModelAgain()
    {
        var compilation = ModelsCompilation();
        var driver = FirstRunOfModels(compilation, _users, _orders);

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(
                new TestOptionsProvider(
                    OutputKind.Models,
                    null,
                    "postgres",
                    properties: new Dictionary<string, string?> { ["SqlSourceCollectionType"] = "List" }
                )
            ),
            compilation
        );

        AllReasons(result, TrackingNames.ProjectSettings).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.TypeModels)
            .ShouldBe([IncrementalStepRunReason.Unchanged, IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.ModelGroups).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.Models).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.ModelOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        ModelReasons(result).Count.ShouldBe(3);
    }

    // An attribute argument that no model reads: the type is read again, and its requests come out equal.
    [Fact]
    public void Run_AttributeEditedWithoutChangingAModel_EmitsNoModelAgain()
    {
        var compilation = ModelsCompilation();
        var driver = FirstRunOfModels(compilation, _users, _orders);

        var result = Run(
            driver,
            WithArgumentsOnUserQueries(compilation, "CollectionType = GeneratorCollectionType.List")
        );

        Reasons<TypeQueries>(result, TrackingNames.TypeQueries, type => type.HintName)["App.Users.UserQueries.g.cs"]
            .ShouldBe(IncrementalStepRunReason.Modified);
        Reasons<EquatableArray<ModelRequest>>(
                result,
                TrackingNames.TypeModels,
                requests => requests[0].Source.ClaimingType
            )
            .ShouldBe(
                new Dictionary<string, IncrementalStepRunReason>
                {
                    ["App.Users.UserQueries"] = IncrementalStepRunReason.Unchanged,
                    ["App.Orders.OrderQueries"] = IncrementalStepRunReason.Cached,
                },
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.ModelGroups).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.Models).ShouldBe([IncrementalStepRunReason.Cached]);
        ModelReasons(result).Count.ShouldBe(3);
        AllReasons(result, TrackingNames.ModelOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    // A project at Sql has no model, so there is no name to look up in the compilation.
    [Fact]
    public void Run_ProjectWithoutModels_LooksUpNoName()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);

        var result = FirstRun(OutputKind.Sql, compilation).GetRunResult();

        (
            (EquatableArray<string>)Outputs(result, TrackingNames.ModelNames).ShouldHaveSingleItem().Value
        ).ShouldBeEmpty();
        (
            (EquatableArray<TakenName>)Outputs(result, TrackingNames.TakenModelNames).ShouldHaveSingleItem().Value
        ).ShouldBeEmpty();
    }

    private static CSharpCompilation ModelsCompilation() =>
        GeneratorHarness.CreateCompilation(Sources, references: [NpgsqlReferences.Npgsql10]);

    // Both types at Models under postgres, with Users, Orders and Payments, each with a current sidecar whose entry has
    // one column, of int4.  A sidecar given is used in place of the one of its file.
    private GeneratorDriver FirstRunOfModels(
        Compilation compilation,
        InMemoryAdditionalText users,
        InMemoryAdditionalText orders,
        params InMemoryAdditionalText[] given
    )
    {
        var sidecars = new List<InMemoryAdditionalText>
        {
            TypedSidecar(users, "GetUser", "int4"),
            TypedSidecar(orders, "GetOrder", "int4"),
            _paymentsSidecar,
        };
        foreach (var sidecar in given)
        {
            _ = sidecars.RemoveAll(other => other.Path == sidecar.Path);
            sidecars.Add(sidecar);
        }

        var driver = GeneratorHarness
            .CreateDriver([users, orders, _payments, .. sidecars], OptionsWithTypeOverrides(null))
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();
        return driver;
    }

    // The models the run has, by the name of their file, and why each ran.  A model that went is no part of them: see
    // ModelsRemoved.
    private static Dictionary<string, IncrementalStepRunReason> ModelReasons(GeneratorDriverRunResult result) =>
        Outputs(result, TrackingNames.ModelOutput)
            .Where(output => output.Reason != IncrementalStepRunReason.Removed)
            .ToDictionary(output => HintNameOf(output.Value), output => output.Reason);

    // A model output is its hint name and its source.
    private static string HintNameOf(object modelOutput)
    {
        var (hintName, _) = ((string HintName, string Source))modelOutput;
        return hintName;
    }

    // The files of the models that went.  Nodes are matched by their place, so what goes is the last place the list
    // had, and not the model that was removed.
    private static string[] ModelsRemoved(GeneratorDriverRunResult result) =>
        [
            .. Outputs(result, TrackingNames.ModelOutput)
                .Where(output => output.Reason == IncrementalStepRunReason.Removed)
                .Select(output => HintNameOf(output.Value)),
        ];

    private GeneratorDriver FirstRun(OutputKind? output, Compilation compilation) =>
        FirstRun(compilation, _users, new TestOptionsProvider(output, null));

    private GeneratorDriver FirstRun(Compilation compilation, InMemoryAdditionalText users, TestOptionsProvider options)
    {
        var driver = GeneratorHarness
            .CreateDriver([users, _orders], options: options)
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();
        return driver;
    }

    // Both types at Models under postgres, and each file with its current sidecar.
    private GeneratorDriver FirstRunWithSidecars(Compilation compilation)
    {
        var driver = GeneratorHarness
            .CreateDriver(
                [_users, _orders, _usersSidecar, _ordersSidecar],
                new TestOptionsProvider(OutputKind.Models, null, "postgres")
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();
        return driver;
    }

    // Both types at Models under postgres, and each file with a current sidecar whose entry has a column of int4,
    // Users with this one when it is given.
    private GeneratorDriver FirstRunWithTypes(
        Compilation compilation,
        TestOptionsProvider options,
        InMemoryAdditionalText? usersSidecar = null
    )
    {
        var driver = GeneratorHarness
            .CreateDriver(
                [
                    _users,
                    _orders,
                    usersSidecar ?? TypedSidecar(_users, "GetUser", "int4"),
                    TypedSidecar(_orders, "GetOrder", "int4"),
                ],
                options
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();
        return driver;
    }

    private static TestOptionsProvider OptionsWithTypeOverrides(string? overrides) =>
        new(
            OutputKind.Models,
            null,
            "postgres",
            properties: new Dictionary<string, string?> { ["SqlSourceTypeOverrides"] = overrides }
        );

    // A current sidecar whose entry has one column, of the type of pg_catalog.
    private static InMemoryAdditionalText TypedSidecar(
        InMemoryAdditionalText sqlFile,
        string queryName,
        string internalName
    )
    {
        var file = ToSqlFile(sqlFile);
        var sidecar = Sidecars.Of(
            file,
            TestSidecars.Entry(
                queryName,
                Sidecars.HashOf(file, queryName, SqlDialect.PostgreSql),
                "postgres",
                "main",
                "18.0",
                columns:
                [
                    TestSidecars.Column(0, "value", TestSidecars.Postgres(internalName, internalName: internalName)),
                ]
            )
        );
        return new InMemoryAdditionalText(sidecar.Path, sidecar.Text);
    }

    // The types of the file of the path.
    private static FileTypes TypesOf(GeneratorDriverRunResult result, string path) =>
        Outputs(result, TrackingNames.FileTypes)
            .Select(output => (FileTypes)output.Value)
            .Single(types => types.NormalizedPath == path);

    private static InMemoryAdditionalText CurrentSidecar(InMemoryAdditionalText sqlFile, string queryName)
    {
        var sidecar = Sidecars.Current(ToSqlFile(sqlFile), SqlDialect.PostgreSql, queryName);
        return new InMemoryAdditionalText(sidecar.Path, sidecar.Text);
    }

    private static SqlFile ToSqlFile(InMemoryAdditionalText file) => new(file.Path, file.GetText()!.ToString());

    // The compilation with the body of UserQueries.Get changed, and its attribute left alone.
    private static Compilation WithMethodBodyEdited(Compilation compilation) =>
        compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.Single(tree => tree.FilePath == UsersSourcePath),
            CSharpSyntaxTree.ParseText(
                UsersSource.Replace("=> Sql.GetUser;", "=> Sql.GetUser + \" \";", StringComparison.Ordinal),
                GeneratorHarness.ParseOptions,
                UsersSourcePath,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

    // The compilation with a list of generator parameters on the attribute of UserQueries.
    private static Compilation WithParametersOnUserQueries(Compilation compilation, string parameters) =>
        WithArgumentsOnUserQueries(compilation, "Parameters = \"" + parameters + "\"");

    // The compilation with these arguments on the attribute of UserQueries.
    private static Compilation WithArgumentsOnUserQueries(Compilation compilation, string arguments) =>
        compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.Single(tree => tree.FilePath == UsersSourcePath),
            CSharpSyntaxTree.ParseText(
                UsersSource.Replace(
                    "[SqlSourceGenerate]",
                    "[SqlSourceGenerate(" + arguments + ")]",
                    StringComparison.Ordinal
                ),
                GeneratorHarness.ParseOptions,
                UsersSourcePath,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

    // Why the check of each file ran, by the file's path.
    private static Dictionary<string, IncrementalStepRunReason> Checks(GeneratorDriverRunResult result) =>
        Reasons<SidecarCheckResult>(result, TrackingNames.SidecarCheck, check => check.NormalizedPath);

    // What one type needs, named by the path of its one file.
    private static string NeedsKey(EquatableArray<FileNeeds> needs) => needs.ShouldHaveSingleItem().NormalizedPath;

    private static GeneratorDriverRunResult Run(GeneratorDriver driver, Compilation compilation) =>
        driver.RunGenerators(compilation, TestContext.Current.CancellationToken).GetRunResult();

    // Every output of a step, with its value and why it ran.
    private static (object Value, IncrementalStepRunReason Reason)[] Outputs(
        GeneratorDriverRunResult result,
        string step
    ) => [.. result.Results.Single().TrackedSteps[step].SelectMany(run => run.Outputs)];

    // Why each output of a step ran, keyed by something that names the output.
    private static Dictionary<string, IncrementalStepRunReason> Reasons<T>(
        GeneratorDriverRunResult result,
        string step,
        Func<T, string> key
    ) =>
        result
            .Results.Single()
            .TrackedSteps[step]
            .SelectMany(run => run.Outputs)
            .ToDictionary(output => key((T)output.Value), output => output.Reason);

    // Why the input of each file's read is what it is, by the file's path.  The read is the step before the one named
    // ParsedFile, which reports a file that was read again and gave an equal value as Cached.  So that a file was not
    // read is shown by its input here, and by ClaimedPaths, the other input of every read: neither is Modified or New.
    private static Dictionary<string, IncrementalStepRunReason> ParseInputs(GeneratorDriverRunResult result) =>
        Reasons<FileParseInput>(result, TrackingNames.FileParseInput, file => file.File.Path);

    // A step that is not among the tracked steps fails the test: it has lost its name.  The exceptions are the steps
    // that give nothing, and so are not tracked, in a run without what they read: FileSettings where no file has
    // metadata, SidecarInput and SidecarFile where there is no sidecar, and TypeNeeds where no query needs an entry.
    // Run_MetadataOfOneFileGainsAParameter_ParsesNoFileAndEmitsOnlyItsType reads the first in a run where a file has
    // metadata, and Run_MethodBodyEdited_ReadsNoSidecar, with the other tests of sidecars, the other three.
    private static IncrementalStepRunReason[] AllReasons(GeneratorDriverRunResult result, string step)
    {
        var steps = result.Results.Single().TrackedSteps;
        return
            step
                is TrackingNames.FileSettings
                    or TrackingNames.SidecarInput
                    or TrackingNames.SidecarFile
                    or TrackingNames.TypeNeeds
            && !steps.ContainsKey(step)
            ? []
            : [.. steps[step].SelectMany(run => run.Outputs).Select(output => output.Reason)];
    }

    // The steps that add source and report diagnostics.
    private static IncrementalStepRunReason[] OutputReasons(GeneratorDriverRunResult result) =>
        [
            .. result
                .Results.Single()
                .TrackedOutputSteps.SelectMany(steps => steps.Value)
                .SelectMany(run => run.Outputs)
                .Select(output => output.Reason),
        ];
}
