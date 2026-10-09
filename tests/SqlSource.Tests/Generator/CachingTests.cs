using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation;
using Xunit;

namespace SqlSource.Tests.Generator;

// The generator runs again on every edit in the IDE.  These tests pin what an edit makes it redo: each step of the
// pipeline reports, for each of its outputs, whether it was taken from the previous run.
public class CachingTests
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

    [Fact]
    public void Run_SqlFileEdited_ParsesOnlyThatFileAndEmitsOnlyTheTypeThatUsesIt()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = FirstRun(compilation);

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
        var driver = FirstRun(compilation);

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
        var driver = FirstRun(compilation);

        var tree = compilation.SyntaxTrees.Single(tree => tree.FilePath == UsersSourcePath);
        var edited = compilation.ReplaceSyntaxTree(
            tree,
            CSharpSyntaxTree.ParseText(
                UsersSource.Replace("=> Sql.GetUser;", "=> Sql.GetUser + \" \";", StringComparison.Ordinal),
                GeneratorHarness.ParseOptions,
                UsersSourcePath,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        var result = Run(driver, edited);

        // The run saw the new compilation: the framework check ran again and gave the same answer.  The declaration
        // was read again too, and gave an equal value, so nothing after it ran.
        AllReasons(result, TrackingNames.SupportedFramework).ShouldBe([IncrementalStepRunReason.Unchanged]);
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
        ];
        foreach (var step in steps)
        {
            AllReasons(result, step).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached, step);
        }

        OutputReasons(result).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void Run_SqlFileAddedThatNoTypeClaims_ReadsNoFileAndEmitsNothingAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation(Sources);
        var driver = FirstRun(compilation);

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
        var driver = FirstRun(compilation);

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
        var driver = FirstRun(compilation);

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
        var driver = FirstRun(compilation, users);

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider("no-token-validation")),
            compilation
        );

        AllReasons(result, TrackingNames.ProjectSettings).ShouldBe([IncrementalStepRunReason.Modified]);

        // No file was read: the two inputs of the read, the file with what its parse depends on and the claimed
        // paths, are the ones it had.
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
        var driver = FirstRun(compilation);

        // What the IDE does when any other property or an .editorconfig changes: new options, the same value.
        var result = Run(driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider(null)), compilation);

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
        var driver = FirstRun(compilation, users, new TestOptionsProvider(null, null, orderDialect));

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider(null, "mysql", orderDialect)),
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
            new TestOptionsProvider(null, null, new Dictionary<string, string> { [_users.Path] = "mssql" })
        );

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(
                new TestOptionsProvider(null, null, new Dictionary<string, string> { [_users.Path] = "mysql" })
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
        var driver = FirstRun(compilation);

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider(null, "postgres")),
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
        var driver = FirstRun(compilation, users);

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider("keep-comments")),
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
        var driver = FirstRun(compilation);

        var result = Run(driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider("sort-input")), compilation);

        AllReasons(result, TrackingNames.ProjectSettings).ShouldBe([IncrementalStepRunReason.Modified]);

        // No file was read: the two inputs of the read, the file with what its parse depends on and the claimed
        // paths, are the ones it had.
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
        var driver = FirstRun(compilation);
        var metadata = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [_users.Path] = new Dictionary<string, string> { ["SqlSourceGeneratorParameters"] = "sort-input" },
        };

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider(null, fileMetadata: metadata)),
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
        var driver = FirstRun(compilation);

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
        var driver = FirstRun(compilation);

        var result = Run(driver, WithParametersOnUserQueries(compilation, "sort-input"));

        AllReasons(result, TrackingNames.CommentPaths).ShouldBe([IncrementalStepRunReason.Cached]);

        // No file was read: the two inputs of the read, the file with what its parse depends on and the claimed
        // paths, are the ones it had.
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
        var driver = FirstRun(compilation, users);
        var metadata = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [_users.Path] = new Dictionary<string, string> { ["SqlSourceGeneratorParameters"] = "keep-comments" },
        };

        var result = Run(
            driver.WithUpdatedAnalyzerConfigOptions(new TestOptionsProvider(null, fileMetadata: metadata)),
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

    private GeneratorDriver FirstRun(Compilation compilation) => FirstRun(compilation, _users);

    private GeneratorDriver FirstRun(
        Compilation compilation,
        InMemoryAdditionalText users,
        TestOptionsProvider? options = null
    )
    {
        var driver = GeneratorHarness
            .CreateDriver([users, _orders], options: options)
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();
        return driver;
    }

    // The compilation with a list of generator parameters on the attribute of UserQueries.
    private static Compilation WithParametersOnUserQueries(Compilation compilation, string parameters) =>
        compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.Single(tree => tree.FilePath == UsersSourcePath),
            CSharpSyntaxTree.ParseText(
                UsersSource.Replace(
                    "[SqlSourceGenerate]",
                    "[SqlSourceGenerate(Parameters = \"" + parameters + "\")]",
                    StringComparison.Ordinal
                ),
                GeneratorHarness.ParseOptions,
                UsersSourcePath,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

    private static GeneratorDriverRunResult Run(GeneratorDriver driver, Compilation compilation) =>
        driver.RunGenerators(compilation, TestContext.Current.CancellationToken).GetRunResult();

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

    // A step that is not among the tracked steps fails the test: it has lost its name.  FileSettings is the one
    // exception, as it gives nothing, and so is not tracked, in a run where no file has metadata.
    // Run_MetadataOfOneFileGainsAParameter_ParsesNoFileAndEmitsOnlyItsType reads it in a run where one has.
    private static IncrementalStepRunReason[] AllReasons(GeneratorDriverRunResult result, string step)
    {
        var steps = result.Results.Single().TrackedSteps;
        return step == TrackingNames.FileSettings && !steps.ContainsKey(step)
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
