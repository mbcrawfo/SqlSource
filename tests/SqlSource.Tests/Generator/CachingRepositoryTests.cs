// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Repositories;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.StepReasons;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The caching of the repositories' connection candidates, which are read for every compilation, since a repository's
// base class may be declared in a file that no edit of the repository's own file touches, and of the methods planned
// from them, which take a type's shape only when it has a repository query.
public sealed class CachingRepositoryTests
{
    private const string BasePath = "/app/Repo/RepositoryBase.cs";

    private const string RepositoryPath = "/app/Repo/UserRepository.cs";

    private const string Base = """
        namespace App
        {
            public abstract class RepositoryBase
            {
                protected string Name { get; } = "";
            }
        }
        """;

    private const string Repository = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate]
            public partial class UserRepository : RepositoryBase;
        }
        """;

    [Fact]
    public void Run_BaseMemberMarkedLater_ResolvesTheDerivedRepository()
    {
        var compilation = GeneratorHarness.CreateCompilation([
            new SourceFile(BasePath, Base),
            new SourceFile(RepositoryPath, Repository),
        ]);
        var driver = FirstRun(compilation);

        var marked = Base.Replace(
            "protected string Name",
            "[SqlSource.SqlSourceConnection] protected string Name",
            System.StringComparison.Ordinal
        );
        var result = Run(driver, WithTree(compilation, BasePath, marked));

        AllReasons(result, TrackingNames.RepositoryShapes).ShouldBe([IncrementalStepRunReason.Modified]);
        var candidate = ShapesOf(result).ShouldHaveSingleItem().Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("Name");
        candidate.IsMarked.ShouldBeTrue();
        candidate.IsInherited.ShouldBeTrue();
    }

    // The repository's declaration is not read again, since its file did not change, and the step still sees the
    // base's new candidate.
    [Fact]
    public void Run_BaseMemberRenamedToAConvention_ReadsItWithoutReadingTheRepositoryAgain()
    {
        var compilation = GeneratorHarness.CreateCompilation([
            new SourceFile(BasePath, Base),
            new SourceFile(RepositoryPath, Repository),
        ]);
        var driver = FirstRun(compilation);

        var renamed = Base.Replace("string Name", "string ConnectionString", System.StringComparison.Ordinal);
        var result = Run(driver, WithTree(compilation, BasePath, renamed));

        AllReasons(result, TrackingNames.TargetTypes).ShouldAllBeCached();
        AllReasons(result, TrackingNames.RepositoryShapes).ShouldBe([IncrementalStepRunReason.Modified]);
        var candidate = ShapesOf(result).ShouldHaveSingleItem().Candidates.ShouldHaveSingleItem();
        candidate.Name.ShouldBe("ConnectionString");
        candidate.IsMarked.ShouldBeFalse();
        candidate.IsInherited.ShouldBeTrue();
    }

    // An edit that changes no candidate reads them again, as every compilation does, and gives an equal value, so
    // nothing after the step runs again.
    [Fact]
    public void Run_BaseEditedWithoutChangingACandidate_LeavesTheShapesUnchanged()
    {
        var compilation = GeneratorHarness.CreateCompilation([
            new SourceFile(BasePath, Base),
            new SourceFile(RepositoryPath, Repository),
        ]);
        var driver = FirstRun(compilation);

        var edited = Base.Replace("= \"\";", "= \"name\";", System.StringComparison.Ordinal);
        var result = Run(driver, WithTree(compilation, BasePath, edited));

        AllReasons(result, TrackingNames.TargetTypes).ShouldAllBeCached();
        AllReasons(result, TrackingNames.RepositoryShapes).ShouldBe([IncrementalStepRunReason.Unchanged]);
        OutputReasonsAfter(result, TrackingNames.RepositoryShapes).ShouldAllBeCached();
    }

    // A methods type takes no shape, so a candidate of it that moves plans none of its methods again.
    [Fact]
    public void Run_CandidateOfAMethodsTypeMoved_LeavesItsMethodsCached()
    {
        var compilation = CachingScene.MethodsCompilation(UserQueries(ConnectionString));
        var driver = new CachingScene().FirstRunOfMethods(compilation);

        var result = Run(driver, CachingScene.WithUserQueries(compilation, UserQueries("\n" + ConnectionString)));

        AllReasons(result, TrackingNames.RepositoryShapes).ShouldBe([IncrementalStepRunReason.Modified]);
        MethodReasons(result, TrackingNames.TypeMethods)
            .ShouldBe(
                CachingScene.UsersAndOrders(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
    }

    // Each repository takes its own shape, so the candidate of one that moves plans no other's methods again.
    [Fact]
    public void Run_CandidateOfOneRepositoryMoved_LeavesTheOthersMethodsCached()
    {
        var scene = new CachingScene();
        var compilation = CachingScene.MethodsCompilation(
            UserQueries(ConnectionString),
            CachingScene.TypeSource("App", "OrderQueries", members: ConnectionString)
        );
        var driver = CachingScene
            .CreateDriver(
                CachingScene.ProjectFile(OutputKind.Repository, "postgres"),
                [
                    scene.Users,
                    scene.Orders,
                    CachingScene.TypedSidecar(scene.Users, "GetUser", "int4"),
                    CachingScene.TypedSidecar(scene.Orders, "GetOrder", "int4"),
                ]
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();

        var result = Run(driver, CachingScene.WithUserQueries(compilation, UserQueries("\n" + ConnectionString)));

        MethodReasons(result, TrackingNames.MethodsInput)
            .ShouldBe(
                CachingScene.UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Unchanged),
                ignoreOrder: true
            );
        MethodReasons(result, TrackingNames.TypeMethods)[CachingScene.OrderMethods]
            .ShouldBe(IncrementalStepRunReason.Cached);
    }

    private const string ConnectionString = "    private readonly string connectionString = \"\";\n";

    // UserQueries of the namespace App, with these members.
    private static string UserQueries(string members) =>
        CachingScene.TypeSource("App", "UserQueries", members: members);

    private static GeneratorDriver FirstRun(Compilation compilation) =>
        GeneratorHarness
            .CreateDriver([], new TestOptionsProvider())
            .RunGenerators(compilation, TestContext.Current.CancellationToken);

    private static Compilation WithTree(Compilation compilation, string path, string text) =>
        compilation.ReplaceSyntaxTree(
            compilation.SyntaxTrees.Single(tree => tree.FilePath == path),
            CSharpSyntaxTree.ParseText(
                text,
                GeneratorHarness.ParseOptions,
                path,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );

    private static EquatableArray<RepositoryShape> ShapesOf(GeneratorDriverRunResult result) =>
        Outputs(result, TrackingNames.RepositoryShapes)
            .Select(output => (EquatableArray<RepositoryShape>)output.Value)
            .ShouldHaveSingleItem();
}
