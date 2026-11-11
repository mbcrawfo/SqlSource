// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System.Linq;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Emitting;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
using SqlSource.Snapshot;
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

    // The collection method is written beside the query method, as an overload of it, in the extension class, the
    // repository's class and the repository's interface, and the generated code compiles with no warning.
    [Fact]
    public void Run_CollectionMethodUnderMethodsAndRepository_WritesTheOverloadBesideTheQueryMethod()
    {
        var file = AddUser();
        var entries = new[] { Entry(file, "AddUser", [Parameter("id")], [Column("id")]) };

        var methods = MethodRuns.Run(file, entries);
        var repository = RepositoryRuns.Run(file, entries);

        methods.ShouldBeClean();
        repository.ShouldBeClean();
        CountOf(methods.Sources[MethodRuns.MethodsFile], " AddUser(\n").ShouldBe(2);
        // The method of the type and the member of its interface, for each of the two.
        CountOf(repository.Sources[MethodRuns.MethodsFile], " AddUser(\n").ShouldBe(4);
        CountOf(methods.Sources[MethodRuns.MethodsFile], "AddUserParams> parameters,\n").ShouldBe(1);
        CountOf(repository.Sources[MethodRuns.MethodsFile], "AddUserParams> parameters,\n").ShouldBe(2);
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

    // End to end: the query method streams, and the collection method beside it, an overload that takes the items,
    // returns an array.
    [Fact]
    public void Run_StreamingCollectionType_WritesTheCollectionMethodReturningAnArray()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- generator: collection-method\n-- collection-type: IAsyncEnumerable\n"
                + "-- name: AddUser -> many\nINSERT INTO users (id) VALUES (@id) RETURNING id;\n"
        );

        var run = MethodRuns.Run(file, [Entry(file, "AddUser", [Parameter("id")], [Column("id")])]);

        run.ShouldBeClean();
        var source = run.Sources[MethodRuns.MethodsFile];
        source.ShouldContain("global::System.Collections.Generic.IAsyncEnumerable<");
        CountOf(source, "global::System.Threading.Tasks.Task<").ShouldBe(1);
        source.ShouldContain("[]> AddUser(\n");
        source.ShouldContain("AddUserParams> parameters,\n");
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

        // One for each query: the type's two methods give one report, as MethodConflictsTests holds.
        run.Problems().Count.ShouldBe(2);
        run.Problems().ShouldAllBe(problem => problem.StartsWith("SQLSRC274 ", System.StringComparison.Ordinal));
        run.Sources.Keys.ShouldNotContain("App.First.Queries-Methods.g.cs");
        run.Sources.Keys.ShouldNotContain("App.Second.Queries-Methods.g.cs");
    }

    // Npgsql refuses a batch command whose text ends in a line comment after its final semicolon, which keep-comments
    // can leave, so a PostgreSQL collection method sends the query's SQL without comments, and its query method keeps
    // them.
    [Fact]
    public void Run_KeepCommentsOnPostgres_GivesTheCollectionMethodTheSqlWithoutComments()
    {
        var file = AddUser() with
        {
            Text =
                "-- generator: collection-method keep-comments\n-- name: AddUser -> one\n"
                + "INSERT INTO users (id) /* kept */ VALUES (@id) RETURNING id; -- done\n",
        };

        var run = Plan(file);

        run.ShouldBeClean();
        var methods = MethodsOf(run);
        SqlOf(methods[0]).ShouldContain("/* kept */");
        SqlOf(methods[1]).ShouldNotContain("/* kept */");
        SqlOf(methods[1]).ShouldNotContain("-- done");
    }

    // SQL Server takes a comment in a batch command, so its collection method sends what its query method sends.
    [Fact]
    public void Run_KeepCommentsOnSqlServer_KeepsTheCommentsOfTheCollectionMethod()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- generator: collection-method keep-comments\n-- name: AddUser -> one\n"
                + "INSERT INTO dbo.users (id) /* kept */ OUTPUT inserted.id VALUES (@id);\n"
        );

        var run = MethodRuns.RunSqlServer(file, [SqlServerAddUser(file, hasRows: true)]);

        run.ShouldBeClean();
        SqlOf(MethodsOf(run)[1]).ShouldContain("/* kept */");
    }

    // The collection method of each shape, beside its query method, compiles with no error and no warning, at both
    // outputs that write methods and on both engines.
    [Theory]
    [MemberData(nameof(ShapesOutputsAndEngines))]
    public void Run_EachShapeOutputAndEngine_CompilesTheCollectionMethod(string shape, string output, string engine)
    {
        var run = RunCollectionMethod(shape, output, engine, "collection-method");

        run.ShouldBeClean();
        CountOf(run.Sources[MethodRuns.MethodsFile], ".CreateBatch();\n").ShouldBe(1);
    }

    // The settings that change the body's statements: no transaction, an isolation level, and no array pool.
    [Theory]
    [InlineData("collection-method transaction=off", "methods", "postgres")]
    [InlineData("collection-method transaction=off", "repository", "mssql")]
    [InlineData("collection-method transaction=serializable", "methods", "mssql")]
    [InlineData("collection-method transaction=repeatable-read", "repository", "postgres")]
    [InlineData("collection-method array-pool=off", "methods", "postgres")]
    public void Run_TransactionAndArrayPool_CompilesTheCollectionMethod(string parameters, string output, string engine)
    {
        foreach (var shape in new[] { "many", "one", "rowcount" })
        {
            var run = RunCollectionMethod(shape, output, engine, parameters);

            run.ShouldBeClean();
            CountOf(run.Sources[MethodRuns.MethodsFile], ".CreateBatch();\n").ShouldBe(1);
        }
    }

    // A list's count, check and SQL are written for each item.
    [Fact]
    public void Run_QueryWithAList_CompilesTheCollectionMethod()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- generator: collection-method\n-- name: FindUsers -> many\n"
                + "SELECT id FROM users WHERE id IN @ids OR @ids IS NULL;\n"
        );

        var run = MethodRuns.Run(file, [Entry(file, "FindUsers", [Parameter("ids")], [Column("id")])]);

        run.ShouldBeClean();
        var source = run.Sources[MethodRuns.MethodsFile];
        source.ShouldContain("var list0 = item.Ids;\n");
        source.ShouldContain("var list0 = parameters.Ids;\n");
    }

    public static TheoryData<string, string, string> ShapesOutputsAndEngines()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var shape in new[] { "many", "one", "one-optional", "rowcount", "none" })
        {
            foreach (var output in new[] { "methods", "repository" })
            {
                foreach (var engine in new[] { "postgres", "mssql" })
                {
                    data.Add(shape, output, engine);
                }
            }
        }

        return data;
    }

    // AddUser of the shape with the generator parameters, under the output and on the engine, its entry with rows for
    // a shape that reads them.
    private static GeneratorRun RunCollectionMethod(string shape, string output, string engine, string parameters)
    {
        var readsRows = shape is "many" or "one" or "one-optional";
        var markers = "-- generator: " + parameters + "\n-- name: AddUser -> " + shape + "\n";
        if (engine == "postgres")
        {
            var file = new SqlFile(
                GeneratorHarness.UsersPath,
                markers + "INSERT INTO users (id) VALUES (@id)" + (readsRows ? " RETURNING id;\n" : ";\n")
            );
            SidecarEntry[] entries = [Entry(file, "AddUser", [Parameter("id")], readsRows ? [Column("id")] : null)];
            return output == "methods" ? MethodRuns.Run(file, entries) : RepositoryRuns.Run(file, entries);
        }

        var sqlServerFile = new SqlFile(
            GeneratorHarness.UsersPath,
            markers + "INSERT INTO dbo.users (id)" + (readsRows ? " OUTPUT inserted.id" : "") + " VALUES (@id);\n"
        );
        SidecarEntry[] sqlServerEntries = [SqlServerAddUser(sqlServerFile, readsRows)];
        return output == "methods"
            ? MethodRuns.RunSqlServer(sqlServerFile, sqlServerEntries)
            : RepositoryRuns.Run(
                sqlServerFile,
                sqlServerEntries,
                SqlServerRepositorySource,
                [SqlClientReferences.SqlClient7],
                "mssql"
            );
    }

    // A repository on SQL Server, whose methods make their connection from a connection string.
    private const string SqlServerRepositorySource = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample(string connectionString);
        }
        """;

    private static SidecarEntry SqlServerAddUser(SqlFile file, bool hasRows) =>
        MethodRuns.SqlServerEntry(
            file,
            "AddUser",
            [TestSidecars.Parameter(0, "id", TestSidecars.SqlServer())],
            hasRows ? [TestSidecars.Column(0, "id", TestSidecars.SqlServer())] : null
        );

    private static string SqlOf(QueryMethod method) => MethodWriter.GetSql(method.Segments);

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
