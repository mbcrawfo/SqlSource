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

        [SqlQueries]
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

            [SqlQueries]
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
            TrackingNames.TypeFiles,
            TrackingNames.ClaimedPaths,
            TrackingNames.ParsedFile,
            TrackingNames.ParsedFiles,
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
        Reasons<ParsedSqlFile>(result, TrackingNames.ParsedFile, file => file.FileName)
            .Keys.ShouldBe(["Users.sql", "Orders.sql"], ignoreOrder: true);
        AllReasons(result, TrackingNames.ParsedFile).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        AllReasons(result, TrackingNames.TypeOutput).ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached);
        result.Diagnostics.ShouldBeEmpty();
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

    private GeneratorDriver FirstRun(Compilation compilation)
    {
        var driver = GeneratorHarness
            .CreateDriver([_users, _orders])
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();
        return driver;
    }

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

    private static IncrementalStepRunReason[] AllReasons(GeneratorDriverRunResult result, string step) =>
        [.. result.Results.Single().TrackedSteps[step].SelectMany(run => run.Outputs).Select(output => output.Reason)];

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
