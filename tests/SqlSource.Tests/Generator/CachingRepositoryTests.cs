// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Repositories;
using SqlSource.Testing;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.StepReasons;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The caching of the repositories' connection candidates, which are read for every compilation, since a repository's
// base class may be declared in a file that no edit of the repository's own file touches, but only for a type that has
// a repository query, and of the methods planned from them, which take a type's shape only when it has one.
public sealed class CachingRepositoryTests
{
    private const string BasePath = "/app/Repo/RepositoryBase.cs";

    private const string ConnectionString = "    private readonly string connectionString = \"\";\n";

    private const string RepositoryPath = "/app/Repo/UserRepository.cs";

    // The file the repository claims by its name, and the file of its methods.
    private const string RepositoryQueriesPath = "/app/Repo/UserRepository.sql";

    private const string RepositoryMethods = "App.UserRepository-Methods.g.cs";

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

    private readonly CachingScene _scene = new();

    // The sidecar of Users that FirstRunWithoutARepository gives at models, which EditUsers replaces.
    private readonly InMemoryAdditionalText _usersSidecar;

    private bool _sidecarsGiven;

    public CachingRepositoryTests()
    {
        _usersSidecar = CachingScene.TypedSidecar(_scene.Users, "GetUser", "int4");
    }

    // The repository's query gets its method once the base's member gives it a connection.  Before, its file holds
    // the interface alone, of the same name, so the interfaces compared across types are equal.
    [Fact]
    public void Run_BaseMemberMarkedLater_ResolvesTheDerivedRepository()
    {
        var compilation = RepositoryCompilation();
        var driver = FirstRunOfRepository(compilation);
        driver
            .GetRunResult()
            .Results.ShouldHaveSingleItem()
            .GeneratedSources.ShouldContain(source =>
                source.HintName == RepositoryMethods && !source.SourceText.ToString().Contains(" async ")
            );

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
        result.Diagnostics.ShouldBeEmpty();
        CachingScene
            .MethodsOf(result, RepositoryMethods)
            .ShouldContain("var connection = new global::Npgsql.NpgsqlConnection(this.Name ?? ");

        // The interface had its name before, so the conflicts are not looked for again.
        AllReasons(result, TrackingNames.InterfaceSignatures).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.MethodConflicts).ShouldBe([IncrementalStepRunReason.Cached]);
    }

    // The repository's declaration is not read again, since its file did not change, and the step still sees the
    // base's new candidate.
    [Fact]
    public void Run_BaseMemberRenamedToAConvention_ReadsItWithoutReadingTheRepositoryAgain()
    {
        var compilation = RepositoryCompilation();
        var driver = FirstRunOfRepository(compilation);

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
        var compilation = RepositoryCompilation();
        var driver = FirstRunOfRepository(compilation);

        var edited = Base.Replace("= \"\";", "= \"name\";", System.StringComparison.Ordinal);
        var result = Run(driver, WithTree(compilation, BasePath, edited));

        AllReasons(result, TrackingNames.TargetTypes).ShouldAllBeCached();
        AllReasons(result, TrackingNames.RepositoryShapes).ShouldBe([IncrementalStepRunReason.Unchanged]);
        OutputReasonsAfter(result, TrackingNames.RepositoryShapes).ShouldAllBeCached();
    }

    // A methods type has no shape read, so a candidate of it that moves reads nothing and plans none of its methods
    // again.
    [Fact]
    public void Run_CandidateOfAMethodsTypeMoved_LeavesItsMethodsCached()
    {
        var compilation = CachingScene.MethodsCompilation(UserQueries(ConnectionString));
        var driver = new CachingScene().FirstRunOfMethods(compilation);

        var result = Run(driver, CachingScene.WithUserQueries(compilation, UserQueries("\n" + ConnectionString)));

        AllReasons(result, TrackingNames.RepositoryShapes).ShouldBe([IncrementalStepRunReason.Unchanged]);
        ShapesOf(result).ShouldBeEmpty();
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

    // In a project whose queries are at Sql or Models no type has a repository query, so no shape is read: a C# edit
    // that moves a type's candidate gives the same empty shapes, and nothing after them runs again.
    [Theory]
    [InlineData("sql")]
    [InlineData("models")]
    public void Run_CandidateMovedInAProjectWithoutARepository_ReadsNoShape(string output)
    {
        var compilation = TwoTypesWithCandidates(UserQueries(ConnectionString));
        var driver = FirstRunWithoutARepository(output, compilation);
        ShapesOf(driver.GetRunResult()).ShouldBeEmpty();

        var result = Run(driver, CachingScene.WithUserQueries(compilation, UserQueries("\n" + ConnectionString)));

        AllReasons(result, TrackingNames.RepositoryTypes).ShouldAllBeCached();
        AllReasons(result, TrackingNames.RepositoryShapes).ShouldBe([IncrementalStepRunReason.Unchanged]);
        ShapesOf(result).ShouldBeEmpty();
        OutputReasonsAfter(result, TrackingNames.RepositoryShapes).ShouldAllBeCached();
    }

    // The same project with a query of Users edited: the types with a repository query are the same, none, and no
    // shape is read.
    [Theory]
    [InlineData("sql")]
    [InlineData("models")]
    public void Run_SqlFileEditedInAProjectWithoutARepository_ReadsNoShape(string output)
    {
        var compilation = TwoTypesWithCandidates(UserQueries(ConnectionString));
        var driver = FirstRunWithoutARepository(output, compilation);

        var result = Run(EditUsers(driver, "-- name: GetUser\nSELECT 10;\n"), compilation);

        result.Diagnostics.ShouldBeEmpty();
        AllReasons(result, TrackingNames.RepositoryTypes).ShouldAllBeCached();
        AllReasons(result, TrackingNames.RepositoryShapes).ShouldBe([IncrementalStepRunReason.Unchanged]);
        ShapesOf(result).ShouldBeEmpty();
    }

    // The header of Users puts UserQueries at Repository in a project at Models: its shape is read, and it gets its
    // repository method, while OrderQueries, still at Models, has none read.
    [Fact]
    public void Run_FileTurnedToRepository_ReadsTheTypesShapeAndPlansItsMethods()
    {
        var compilation = TwoTypesWithCandidates(UserQueries(ConnectionString));
        var driver = FirstRunWithoutARepository("models", compilation);

        var result = Run(EditUsers(driver, "-- output: repository\n\n" + SqlTexts.GetUser), compilation);

        result.Diagnostics.ShouldBeEmpty();
        AllReasons(result, TrackingNames.RepositoryTypes).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.RepositoryShapes).ShouldBe([IncrementalStepRunReason.Modified]);
        var shape = ShapesOf(result).ShouldHaveSingleItem();
        shape.TypeKey.ShouldBe("App.UserQueries");
        shape.Candidates.ShouldHaveSingleItem().Name.ShouldBe("connectionString");
        CachingScene
            .MethodsOf(result, CachingScene.UserMethods)
            .ShouldContain("new global::Npgsql.NpgsqlConnection(this.connectionString");
    }

    // The reverse: the header of Users that put UserQueries at Repository goes, so its shape is no longer read, and
    // it has no repository method.
    [Fact]
    public void Run_FileTurnedFromRepository_ReadsNoShapeAndPlansNoMethod()
    {
        var compilation = TwoTypesWithCandidates(UserQueries(ConnectionString));
        var atRepository = new InMemoryAdditionalText(
            _scene.Users.Path,
            "-- output: repository\n\n" + SqlTexts.GetUser
        );
        var atRepositorySidecar = CachingScene.TypedSidecar(atRepository, "GetUser", "int4");
        var driver = CachingScene
            .CreateDriver(
                CachingScene.ProjectFile(OutputKind.Models, "postgres"),
                [
                    atRepository,
                    _scene.Orders,
                    atRepositorySidecar,
                    CachingScene.TypedSidecar(_scene.Orders, "GetOrder", "int4"),
                ]
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();
        ShapesOf(driver.GetRunResult()).ShouldHaveSingleItem().TypeKey.ShouldBe("App.UserQueries");

        var plain = new InMemoryAdditionalText(_scene.Users.Path, SqlTexts.GetUser);
        var result = Run(
            driver
                .ReplaceAdditionalText(atRepository, plain)
                .ReplaceAdditionalText(atRepositorySidecar, CachingScene.TypedSidecar(plain, "GetUser", "int4")),
            compilation
        );

        result.Diagnostics.ShouldBeEmpty();
        AllReasons(result, TrackingNames.RepositoryTypes).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.RepositoryShapes).ShouldBe([IncrementalStepRunReason.Modified]);
        ShapesOf(result).ShouldBeEmpty();
        result.GeneratedTrees.ShouldNotContain(tree =>
            tree.FilePath.EndsWith(CachingScene.UserMethods, System.StringComparison.Ordinal)
        );
    }

    // UserQueries of the namespace App, with these members.
    private static string UserQueries(string members) =>
        CachingScene.TypeSource("App", "UserQueries", members: members);

    // RepositoryBase and UserRepository, which derives from it, with the reference to Npgsql 10.
    private static CSharpCompilation RepositoryCompilation() =>
        GeneratorHarness.CreateCompilation(
            [new SourceFile(BasePath, Base), new SourceFile(RepositoryPath, Repository)],
            references: [NpgsqlReferences.Npgsql10]
        );

    // The project at Repository under postgres, with the file UserRepository claims and its sidecar, whose entry has
    // one column, of int4.
    private static GeneratorDriver FirstRunOfRepository(Compilation compilation)
    {
        var queries = new InMemoryAdditionalText(RepositoryQueriesPath, SqlTexts.GetUser);
        return CachingScene
            .CreateDriver(
                CachingScene.ProjectFile(OutputKind.Repository, "postgres"),
                [queries, CachingScene.TypedSidecar(queries, "GetUser", "int4")]
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
    }

    // UserQueries with this source, and OrderQueries with a candidate of its own, both in the namespace App.
    private static CSharpCompilation TwoTypesWithCandidates(string usersSource) =>
        CachingScene.MethodsCompilation(
            usersSource,
            CachingScene.TypeSource("App", "OrderQueries", members: ConnectionString)
        );

    // Both types with Users and Orders in a project at this output, sql or models, under postgres, which reports
    // nothing: at models each file with a sidecar whose entry has one column, of int4, and at sql, which needs no
    // entry, without one.
    private GeneratorDriver FirstRunWithoutARepository(string output, Compilation compilation)
    {
        var atSql = output == "sql";
        _sidecarsGiven = !atSql;
        var driver = CachingScene
            .CreateDriver(
                CachingScene.ProjectFile(atSql ? OutputKind.Sql : OutputKind.Models, "postgres"),
                atSql
                    ? [_scene.Users, _scene.Orders]
                    :
                    [
                        _scene.Users,
                        _scene.Orders,
                        _usersSidecar,
                        CachingScene.TypedSidecar(_scene.Orders, "GetOrder", "int4"),
                    ]
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();
        return driver;
    }

    // Users, which the first run had, with this text, and its sidecar, when the run had one, written for it, of the
    // query GetUser with one column, of int4.
    private GeneratorDriver EditUsers(GeneratorDriver driver, string text)
    {
        var edited = new InMemoryAdditionalText(_scene.Users.Path, text);
        var withText = driver.ReplaceAdditionalText(_scene.Users, edited);
        return _sidecarsGiven
            ? withText.ReplaceAdditionalText(_usersSidecar, CachingScene.TypedSidecar(edited, "GetUser", "int4"))
            : withText;
    }

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
