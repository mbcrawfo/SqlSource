// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System.Linq;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The second method that a query with `collection-method` on is planned: its place beside the query method, what it
// takes from the query's settings, and the isolation levels that PostgreSQL does not have.  The methods are read from
// the planner's step, since their text is the emitter's.  Each run is under postgres on Npgsql 10 unless it says
// otherwise.
public sealed class CollectionMethodTests
{
    private static SqlFile AddUser(string markers = "-- generator: collection-method\n") =>
        new(GeneratorHarness.UsersPath, markers + "-- name: AddUser -> one\nINSERT INTO users (id) VALUES (@id);\n");

    private static GeneratorRun Plan(SqlFile file, bool hasParameter = true) =>
        MethodRuns.Run(file, [Entry(file, "AddUser", hasParameter ? [Parameter("id")] : [], [Column("id")])]);

    private static QueryMethod[] MethodsOf(GeneratorRun run) =>
        [.. StepOutputs.Single<TypeMethods>(run, TrackingNames.TypeMethods).Methods];

    [Fact]
    public void Run_CollectionMethodOn_PlansASecondMethodWithBatch()
    {
        var run = Plan(AddUser());

        run.ShouldBeClean();
        var methods = MethodsOf(run);
        methods.Select(method => method.Name).ShouldBe(["AddUser", "AddUser"]);
        methods[0].Batch.ShouldBeNull();
        methods[1].Batch.ShouldBe(new BatchCall(TransactionMode.On));
        methods[1].ShouldBe(methods[0] with { Batch = new BatchCall(TransactionMode.On) });
    }

    // Until the collection method has a signature of its own it is not written, so the run has no duplicate member,
    // under either output.
    [Fact]
    public void Run_CollectionMethodUnderMethodsAndRepository_WritesOneMethod()
    {
        var file = AddUser();
        var entries = new[] { Entry(file, "AddUser", [Parameter("id")], [Column("id")]) };

        var methods = MethodRuns.Run(file, entries);
        var repository = RepositoryRuns.Run(file, entries);

        methods.ShouldBeClean();
        repository.ShouldBeClean();
        CountOf(methods.Sources[MethodRuns.MethodsFile], " AddUser(\n").ShouldBe(1);
        // The method of the type and the member of its interface.
        CountOf(repository.Sources[MethodRuns.MethodsFile], " AddUser(\n").ShouldBe(2);
        MethodsOf(repository).Length.ShouldBe(2);
        _ = MethodsOf(repository)[1].Batch.ShouldNotBeNull();
    }

    [Fact]
    public void Run_CollectionMethodOff_PlansNoSecondMethod()
    {
        var run = Plan(AddUser(markers: ""));

        run.ShouldBeClean();
        MethodsOf(run).ShouldHaveSingleItem().Batch.ShouldBeNull();
    }

    [Fact]
    public void Run_CollectionMethodOnAQueryWithoutParameters_PlansNoSecondMethodAndReportsNothing()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- generator: collection-method\n-- name: AddUser -> one\nSELECT 1 AS id;\n"
        );

        var run = Plan(file, hasParameter: false);

        run.ShouldBeClean();
        MethodsOf(run).ShouldHaveSingleItem().Batch.ShouldBeNull();
    }

    [Fact]
    public void Run_StreamingCollectionType_GivesTheSecondMethodAnArray()
    {
        var file = AddUser("-- generator: collection-method\n-- collection-type: IAsyncEnumerable\n") with
        {
            Text =
                "-- generator: collection-method\n-- collection-type: IAsyncEnumerable\n"
                + "-- name: AddUser -> many\nINSERT INTO users (id) VALUES (@id) RETURNING id;\n",
        };

        var run = Plan(file);

        run.ShouldBeClean();
        var methods = MethodsOf(run);
        methods[0].Collection.ShouldBe(CollectionKind.IAsyncEnumerable);
        methods[1].Collection.ShouldBe(CollectionKind.Array);
    }

    [Fact]
    public void Run_TransactionSerializable_IsTheSecondMethodsMode()
    {
        var run = Plan(AddUser("-- generator: collection-method transaction=serializable\n"));

        run.ShouldBeClean();
        MethodsOf(run)[1].Batch.ShouldBe(new BatchCall(TransactionMode.Serializable));
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("read-uncommitted")]
    public void Run_TransactionNotOnPostgres_IsSqlsrc297AndKeepsTheQueryMethod(string level)
    {
        var file = AddUser("-- generator: collection-method transaction=" + level + "\n");

        var run = Plan(file);

        run.Problems()
            .ShouldBe([
                "SQLSRC297 "
                    + GeneratorHarness.UsersPath
                    + "(2,10)-(2,17): The query 'AddUser' asks for the transaction isolation level '"
                    + level
                    + "', which PostgreSQL does not have",
            ]);
        MethodsOf(run).ShouldHaveSingleItem().Batch.ShouldBeNull();
        run.Sources[MethodRuns.MethodsFile].ShouldContain(" AddUser(\n");
    }

    [Fact]
    public void Run_TransactionWithoutCollectionMethodOnPostgres_IsNoError()
    {
        var run = Plan(AddUser("-- generator: transaction=snapshot\n"));

        run.ShouldBeClean();
        MethodsOf(run).ShouldHaveSingleItem().Batch.ShouldBeNull();
    }

    [Fact]
    public void Run_TransactionSnapshotOnSqlServer_IsNoError()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- generator: collection-method transaction=snapshot\n-- name: AddUser -> one\n"
                + "INSERT INTO dbo.users (id) OUTPUT inserted.id VALUES (@id);\n"
        );
        var entry = MethodRuns.SqlServerEntry(
            file,
            "AddUser",
            [TestSidecars.Parameter(0, "id", TestSidecars.SqlServer())],
            [TestSidecars.Column(0, "id", TestSidecars.SqlServer())]
        );

        var run = MethodRuns.RunSqlServer(file, [entry]);

        run.ShouldBeClean();
        MethodsOf(run)[1].Batch.ShouldBe(new BatchCall(TransactionMode.Snapshot));
    }

    // The two methods of a type share a name by design, which is no clash of the query with itself; a clash with
    // another type's method of the name is reported once for each query and takes out both methods of the type.
    [Fact]
    public void Run_CollectionMethodBesideAnotherTypesMethodOfItsName_IsOneSqlsrc274ForEachQueryAndDropsBoth()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                public partial class First
                {
                    [SqlSourceGenerate(Source = "A.sql")]
                    public partial class Queries;
                }

                public partial class Second
                {
                    [SqlSourceGenerate(Source = "B.sql")]
                    public partial class Queries;
                }
            }
            """;
        var a = new SqlFile("/app/Repo/A.sql", AddUser().Text);
        var b = new SqlFile("/app/Repo/B.sql", "-- name: AddUser -> one\nSELECT 1 AS id;\n");

        var run = MethodRuns.Run(
            Source,
            a,
            Sidecars.Of(a, Entry(a, "AddUser", [Parameter("id")], [Column("id")])),
            b,
            Sidecars.Of(b, Entry(b, "AddUser", [], [Column("id")]))
        );

        run.Problems().Count.ShouldBe(2);
        run.Problems().ShouldAllBe(problem => problem.StartsWith("SQLSRC274 ", System.StringComparison.Ordinal));
        run.Sources.Keys.ShouldNotContain("App.First.Queries-Methods.g.cs");
        run.Sources.Keys.ShouldNotContain("App.Second.Queries-Methods.g.cs");
    }

    private static int CountOf(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, System.StringComparison.Ordinal); at >= 0; )
        {
            count++;
            at = text.IndexOf(part, at + part.Length, System.StringComparison.Ordinal);
        }

        return count;
    }
}
