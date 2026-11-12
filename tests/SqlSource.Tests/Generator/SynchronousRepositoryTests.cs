// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System.Linq;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Methods;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The methods that method-type gives a repository's query, on the type and in its interface: a synchronous method
// under sync, both under both, each getting its connection from the source of its synchrony, a pair's or the one
// source's, and SQLSRC299 for a synchronous method whose database has only a source that returns a task.  Each run
// is under postgres on Npgsql 10, at Repository.
public sealed class SynchronousRepositoryTests
{
    private const string MethodsFile = "App.Sample-Methods.g.cs";

    private const string GetUserText = "-- name: GetUser -> one\nSELECT id FROM users WHERE id = @id;\n";

    private const string SyncGetUser = "        public global::App.GetUserDto GetUser(\n";

    private const string InterfaceSyncGetUser = "        global::App.GetUserDto GetUser(\n";

    private const string AsyncGetUserAsync =
        "        public async global::System.Threading.Tasks.Task<global::App.GetUserDto> GetUserAsync(\n";

    private const string InterfaceAsyncGetUserAsync =
        "        global::System.Threading.Tasks.Task<global::App.GetUserDto> GetUserAsync(\n";

    // A type whose one source is a factory that returns a task of a connection.
    private const string AsyncFactoryOnly = """
        using System.Threading;
        using System.Threading.Tasks;
        using Npgsql;
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample
            {
                [SqlSourceConnection]
                private Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken) =>
                    Task.FromResult(new NpgsqlConnection());
            }
        }
        """;

    // A type with a pair of factories, each of which takes a token: one that returns a task and one that does not.
    private const string PairOfFactories = """
        using System.Threading;
        using System.Threading.Tasks;
        using Npgsql;
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample
            {
                [SqlSourceConnection]
                private Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken) =>
                    Task.FromResult(new NpgsqlConnection());

                [SqlSourceConnection]
                private NpgsqlConnection Open(CancellationToken cancellationToken) => new NpgsqlConnection();
            }
        }
        """;

    private static SqlFile UsersFile(string text) => new(GeneratorHarness.UsersPath, text);

    private static GeneratorRun RunGetUser(string parameters, string source = RepositoryRuns.Source)
    {
        var file = UsersFile("-- generator: " + parameters + "\n" + GetUserText);
        return RepositoryRuns.Run(file, [Entry(file, "GetUser", [Parameter("id")], [Column("id")])], source);
    }

    private static QueryMethod[] MethodsOf(GeneratorRun run) =>
        [.. StepOutputs.Single<TypeMethods>(run, TrackingNames.TypeMethods).Methods];

    private static int CountOf(string text, string value) =>
        text.Split([value], System.StringSplitOptions.None).Length - 1;

    // A type of the source's declaration, which claims the folder.
    private static string SampleOf(string declaration) =>
        "using System.Threading;\nusing System.Threading.Tasks;\nusing Npgsql;\nusing SqlSource;\n\nnamespace App\n{\n"
        + "    [SqlSourceGenerate(Source = \".\")]\n    "
        + declaration
        + "\n}\n";

    // Each kind of source that returns no task gives a synchronous method its connection by its synchronous form: a
    // new connection of a string, one a data source opens, a factory's, given default for its token, and a borrowed
    // one, each opened when it is closed.
    [Theory]
    [InlineData(
        "public partial class Sample(string connectionString);",
        "var connection = new global::Npgsql.NpgsqlConnection(connectionString ?? "
    )]
    [InlineData(
        "public partial class Sample(NpgsqlDataSource dataSource);",
        "var connection = (dataSource ?? throw new global::System.InvalidOperationException("
            + "\"The connection source 'dataSource' of 'App.Sample' is null.\")).OpenConnection();\n"
    )]
    [InlineData("public partial class Sample([SqlSourceConnection] NpgsqlConnection db);", "var connection = db ?? ")]
    [InlineData(
        "public partial class Sample { [SqlSourceConnection] private NpgsqlConnection Open() => new(); }",
        "var connection = this.Open() ?? "
    )]
    [InlineData(
        "public partial class Sample { [SqlSourceConnection] private NpgsqlConnection Open(CancellationToken token) "
            + "=> new(); }",
        "var connection = this.Open(default) ?? "
    )]
    public void Run_MethodTypeSyncWithEachSource_WritesASynchronousMethod(string declaration, string acquire)
    {
        var run = RunGetUser("method-type=sync", SampleOf(declaration));

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain(SyncGetUser);
        methods.ShouldContain(InterfaceSyncGetUser);
        CountOf(methods, " GetUser(\n").ShouldBe(2);
        methods.ShouldContain(acquire);
        methods.ShouldContain("                connection.Open();\n");
        methods.ShouldNotContain("async");
        methods.ShouldNotContain("Task");
        methods.ShouldNotContain("await");
        methods.ShouldNotContain("ConfigureAwait");
        methods.ShouldNotContain("cancellationToken");
        var method = MethodsOf(run).ShouldHaveSingleItem();
        method.Synchrony.ShouldBe(MethodSynchrony.Synchronous);
        _ = method.Source.ShouldNotBeNull();
    }

    // Under both the type and its interface have GetUser and GetUserAsync.
    [Fact]
    public void Run_MethodTypeBoth_WritesBothMethodsAndBothMembers()
    {
        var run = RunGetUser("method-type=both");

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain(SyncGetUser);
        methods.ShouldContain(AsyncGetUserAsync);
        methods.ShouldContain(InterfaceSyncGetUser);
        methods.ShouldContain(InterfaceAsyncGetUserAsync);
        methods.ShouldContain(".OpenConnection();\n");
        methods.ShouldContain(".OpenConnectionAsync(cancellationToken).ConfigureAwait(false);\n");
        MethodsOf(run)
            .Select(method => (method.Name, method.Synchrony))
            .ShouldBe([("GetUser", MethodSynchrony.Synchronous), ("GetUserAsync", MethodSynchrony.Asynchronous)]);
    }

    // The four methods in part 2's order, each with its member of the interface.
    [Fact]
    public void Run_MethodTypeBothCollectionMethod_WritesFourMethodsAndFourMembers()
    {
        var run = RunGetUser("method-type=both collection-method");

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        CountOf(methods, " GetUser(\n").ShouldBe(4);
        CountOf(methods, " GetUserAsync(\n").ShouldBe(4);
        var planned = MethodsOf(run);
        planned.Select(method => method.Name).ShouldBe(["GetUser", "GetUserAsync", "GetUser", "GetUserAsync"]);
        planned
            .Select(method => method.Synchrony)
            .ShouldBe([
                MethodSynchrony.Synchronous,
                MethodSynchrony.Asynchronous,
                MethodSynchrony.Synchronous,
                MethodSynchrony.Asynchronous,
            ]);
        planned.Select(method => method.Batch is null).ShouldBe([true, true, false, false]);
        planned.ShouldAllBe(method => method.Source != null);
    }

    // Each method calls the factory of its synchrony, the asynchronous one with its token and the synchronous one with
    // default, and never the other's.
    [Fact]
    public void Run_MethodTypeBothWithAPair_GivesEachMethodItsOwnFactory()
    {
        var run = RunGetUser("method-type=both", PairOfFactories);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        CountOf(methods, "this.OpenAsync(cancellationToken)").ShouldBe(1);
        CountOf(methods, "this.Open(default)").ShouldBe(1);
        methods.ShouldNotContain("this.Open(cancellationToken)");
        methods.ShouldNotContain("this.OpenAsync(default)");
        var planned = MethodsOf(run);
        planned.Select(method => method.Source!.MemberName).ShouldBe(["Open", "OpenAsync"]);
    }

    // A pair's collection methods each take their own form's factory, as the methods do: the collection method is a
    // copy of the query's, and each copy has the source of its form.
    [Fact]
    public void Run_MethodTypeBothCollectionMethodWithAPair_GivesEachCollectionMethodItsOwnFactory()
    {
        var run = RunGetUser("method-type=both collection-method", PairOfFactories);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        CountOf(methods, "this.OpenAsync(cancellationToken)").ShouldBe(2);
        CountOf(methods, "this.Open(default)").ShouldBe(2);
        methods.ShouldNotContain("this.Open(cancellationToken)");
        methods.ShouldNotContain("this.OpenAsync(default)");
        MethodsOf(run).Select(method => method.Source!.MemberName).ShouldBe(["Open", "OpenAsync", "Open", "OpenAsync"]);
    }

    // The synchronous factory of a pair that no synchronous method uses is no problem.
    [Fact]
    public void Run_MethodTypeAsyncWithAPair_UsesTheAsynchronousFactoryAlone()
    {
        var run = RunGetUser("method-type=async", PairOfFactories);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("this.OpenAsync(cancellationToken)");
        methods.ShouldNotContain("this.Open(");
    }

    // A synchronous method cannot call a factory that returns a task, so under sync the query gets no method.
    [Fact]
    public void Run_MethodTypeSyncWithOnlyAnAsyncFactory_ReportsSqlsrc299AndWritesNoMethod()
    {
        var run = RunGetUser("method-type=sync", AsyncFactoryOnly);

        run.Problems().ShouldBe([Sqlsrc299At(2)]);
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("    public partial interface ISample\n    {\n    }\n");
        methods.ShouldNotContain(" GetUser");
        MethodsOf(run).ShouldBeEmpty();
    }

    // Under both the query keeps its asynchronous method and member, and loses the synchronous ones.
    [Fact]
    public void Run_MethodTypeBothWithOnlyAnAsyncFactory_ReportsSqlsrc299AndKeepsTheAsynchronousMethod()
    {
        var run = RunGetUser("method-type=both", AsyncFactoryOnly);

        run.Problems().ShouldBe([Sqlsrc299At(2)]);
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain(AsyncGetUserAsync);
        methods.ShouldContain(InterfaceAsyncGetUserAsync);
        methods.ShouldNotContain(" GetUser(\n");
        MethodsOf(run).Select(method => method.Name).ShouldBe(["GetUserAsync"]);
    }

    // Its asynchronous collection method stays, and the synchronous one goes with the synchronous method.
    [Fact]
    public void Run_MethodTypeBothCollectionMethodWithOnlyAnAsyncFactory_KeepsTheAsynchronousCollectionMethod()
    {
        var run = RunGetUser("method-type=both collection-method", AsyncFactoryOnly);

        run.Problems().ShouldBe([Sqlsrc299At(2)]);
        var planned = MethodsOf(run);
        planned.Select(method => method.Name).ShouldBe(["GetUserAsync", "GetUserAsync"]);
        planned.ShouldAllBe(method => method.Synchrony == MethodSynchrony.Asynchronous);
        planned.Select(method => method.Batch is null).ShouldBe([true, false]);
        run.Sources[MethodsFile].ShouldNotContain(" GetUser(\n");
    }

    // A stream's synchronous form is its collection method alone, which goes; its stream and asynchronous collection
    // method stay.
    [Fact]
    public void Run_MethodTypeBothStreamCollectionMethodWithOnlyAnAsyncFactory_KeepsTheAsynchronousMethods()
    {
        var file = UsersFile(
            "-- generator: method-type=both collection-method\n-- collection-type: IAsyncEnumerable\n"
                + "-- name: GetUser\nSELECT id FROM users WHERE id > @id;\n"
        );

        var run = RepositoryRuns.Run(
            file,
            [Entry(file, "GetUser", [Parameter("id")], [Column("id")])],
            AsyncFactoryOnly
        );

        run.Problems().ShouldBe([Sqlsrc299At(3)]);
        var planned = MethodsOf(run);
        planned.Select(method => method.Name).ShouldBe(["GetUserAsync", "GetUserAsync"]);
        planned.ShouldAllBe(method => method.Synchrony == MethodSynchrony.Asynchronous);
        planned.Select(method => method.Batch is null).ShouldBe([true, false]);
    }

    [Fact]
    public void Run_MethodTypeAsyncWithOnlyAnAsyncFactory_ReportsNothing() =>
        RunGetUser("method-type=async", AsyncFactoryOnly).ShouldBeClean();

    // SQLSRC299 is reported beside the query's other problems.
    [Fact]
    public void Run_MethodTypeSyncWithOnlyAnAsyncFactoryAndAnotherProblem_ReportsBoth()
    {
        var file = UsersFile(
            "-- generator: method-type=sync\n-- name: GetFrom\n-- token: {{connection:users}}\n"
                + "SELECT id FROM {{connection}};\n"
        );

        var run = RepositoryRuns.Run(file, [Entry(file, "GetFrom", [], [Column("id")])], AsyncFactoryOnly);

        run.Problems().Select(problem => problem[..9]).ShouldBe(["SQLSRC275", "SQLSRC299"]);
    }

    // SQLSRC299 excuses no other error: under both the query that has one beside it keeps no method, not even the
    // asynchronous one that its form alone would have kept.
    [Fact]
    public void Run_MethodTypeBothWithOnlyAnAsyncFactoryAndAnotherProblem_LeavesTheQueryNoMethod()
    {
        var file = UsersFile(
            "-- generator: method-type=both\n-- name: GetFrom\n-- token: {{connection:users}}\n"
                + "SELECT id FROM {{connection}};\n"
        );

        var run = RepositoryRuns.Run(file, [Entry(file, "GetFrom", [], [Column("id")])], AsyncFactoryOnly);

        run.Problems().Select(problem => problem[..9]).ShouldBe(["SQLSRC275", "SQLSRC299"]);
        MethodsOf(run).ShouldBeEmpty();
        run.Sources[MethodsFile].ShouldNotContain("GetFrom");
    }

    // The synchronous form's source is a base type's member of the query's name, which the method would hide; the
    // asynchronous form's is another member, so under async the query is no problem.
    [Theory]
    [InlineData(
        "method-type=sync",
        "SQLSRC274 /app/Repo/Users.sql(2,10)-(2,14): The method of the query 'Open' cannot be named 'Open', because "
            + "the repository methods of 'App.Sample' get their connection from 'Open'.  Rename the query."
    )]
    [InlineData("method-type=async", null)]
    public void Run_QueryNamedAsThePairsSynchronousMember_ReportsSqlsrc274ForTheSynchronousForm(
        string parameters,
        string? problem
    )
    {
        const string Source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public abstract class RepositoryBase
                {
                    [SqlSourceConnection]
                    protected Task<NpgsqlConnection> ConnectAsync(CancellationToken cancellationToken) =>
                        Task.FromResult(new NpgsqlConnection());

                    [SqlSourceConnection]
                    protected NpgsqlConnection Open() => new NpgsqlConnection();
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample : RepositoryBase;
            }
            """;
        var file = UsersFile("-- generator: " + parameters + "\n-- name: Open\nSELECT id FROM users;\n");

        var run = RepositoryRuns.Run(file, [Entry(file, "Open", [], [Column("id")])], Source);

        run.Problems().ShouldBe(problem is null ? [] : [problem]);
    }

    // A token of the synchronous source's parameter's name would hide it in the synchronous method.
    [Theory]
    [InlineData(
        "method-type=sync",
        "SQLSRC275 /app/Repo/Users.sql(2,10)-(2,17): The token 'dataSource' of the query 'GetFrom' has the name of "
            + "the primary-constructor parameter 'dataSource', which gives the repository methods of 'App.Sample' "
            + "their connection.  Rename the token."
    )]
    [InlineData("method-type=async", null)]
    public void Run_TokenNamedAsThePairsSynchronousParameter_ReportsSqlsrc275ForTheSynchronousForm(
        string parameters,
        string? problem
    )
    {
        var source = SampleOf(
            "public partial class Sample([SqlSourceConnection] NpgsqlDataSource dataSource)\n    {\n"
                + "        [SqlSourceConnection]\n"
                + "        private Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken) =>\n"
                + "            Task.FromResult(new NpgsqlConnection());\n    }"
        );
        var file = UsersFile(
            "-- generator: "
                + parameters
                + "\n-- name: GetFrom\n-- token: {{dataSource:users}}\nSELECT id FROM {{dataSource}};\n"
        );

        var run = RepositoryRuns.Run(file, [Entry(file, "GetFrom", [], [Column("id")])], source);

        run.Problems().ShouldBe(problem is null ? [] : [problem]);
    }

    // A source that both forms share is checked once, so that its token is a problem once and not for each form.
    [Fact]
    public void Run_MethodTypeBothTokenNamedAsTheSharedSource_ReportsSqlsrc275Once()
    {
        var source = SampleOf("public partial class Sample(NpgsqlDataSource dataSource);");
        var file = UsersFile(
            "-- generator: method-type=both\n-- name: GetFrom\n-- token: {{dataSource:users}}\n"
                + "SELECT id FROM {{dataSource}};\n"
        );

        var run = RepositoryRuns.Run(file, [Entry(file, "GetFrom", [], [Column("id")])], source);

        run.Problems()
            .ShouldBe([
                "SQLSRC275 /app/Repo/Users.sql(2,10)-(2,17): The token 'dataSource' of the query 'GetFrom' has the "
                    + "name of the primary-constructor parameter 'dataSource', which gives the repository methods of "
                    + "'App.Sample' their connection.  Rename the token.",
            ]);
        // The run's report lists a problem once whatever the planner found, so the planner's own list says it too.
        StepOutputs
            .Single<TypeMethods>(run, TrackingNames.TypeMethods)
            .Diagnostics.Count(diagnostic => diagnostic.Descriptor.Id == "SQLSRC275")
            .ShouldBe(1);
    }

    // A pair's synchronous factory, an instance member, hides no type, and its asynchronous one, a static member, is
    // reached through the type, which a token of the type's name would hide: the second form is checked too.
    [Fact]
    public void Run_MethodTypeBothTokenNamedAsTheTypeOfTheAsynchronousFactory_ReportsSqlsrc275()
    {
        var source = SampleOf(
            "public partial class Sample\n    {\n"
                + "        [SqlSourceConnection]\n"
                + "        private static Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken) =>\n"
                + "            Task.FromResult(new NpgsqlConnection());\n\n"
                + "        [SqlSourceConnection]\n"
                + "        private NpgsqlConnection Open() => new NpgsqlConnection();\n    }"
        );
        var file = UsersFile(
            "-- generator: method-type=both\n-- name: GetFrom\n-- token: {{Sample:users}}\n"
                + "SELECT id FROM {{Sample}};\n"
        );

        var run = RepositoryRuns.Run(file, [Entry(file, "GetFrom", [], [Column("id")])], source);

        var problems = run.Problems();
        problems.Count.ShouldBe(1);
        problems[0].ShouldStartWith("SQLSRC275 ");
        problems[0].ShouldContain("'Sample'");
    }

    // A method of no argument named Finalize has the signature of object.Finalize, which the compiler warns about
    // (CS0465), so a synchronous repository method, whose arguments may be none, cannot have the name; an asynchronous
    // one takes the token.  Under both the synchronous form is the one that is named so.
    [Theory]
    [InlineData("method-type=sync")]
    [InlineData("method-type=both")]
    public void Run_SynchronousRepositoryMethodNamedFinalizeWithNoArguments_ReportsSqlsrc274(string parameters)
    {
        var file = UsersFile("-- generator: " + parameters + "\n-- name: Finalize\nSELECT id FROM users;\n");

        var run = RepositoryRuns.Run(file, [Entry(file, "Finalize", [], [Column("id")])], RepositoryRuns.Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(2,10)-(2,18): The method of the query 'Finalize' cannot be named "
                    + "'Finalize', because object.Finalize has that signature, and a method of the type with no "
                    + "argument that is named so is taken for the type's finalizer.  Rename the query.",
            ]);
        run.Sources[MethodsFile].ShouldNotContain("Finalize");
    }

    // A Finalize that the type has already takes the name, which is one SQLSRC274 for the form and not a second for its
    // signature.  A destructor is the type's finalizer, which may or may not take the name; either way it is reported
    // once.
    [Theory]
    [InlineData("method-type=sync", "public void Finalize(int x) { }")]
    [InlineData("method-type=both", "public void Finalize(int x) { }")]
    [InlineData("method-type=sync", "~Sample() { }")]
    [InlineData("method-type=both", "~Sample() { }")]
    public void Run_SynchronousRepositoryMethodNamedAFinalizeTheTypeHas_ReportsSqlsrc274Once(
        string parameters,
        string member
    )
    {
        var source = SampleOf(
            "public partial class Sample(string connectionString)\n    {\n        " + member + "\n    }"
        );
        var file = UsersFile("-- generator: " + parameters + "\n-- name: Finalize\nSELECT id FROM users;\n");

        var run = RepositoryRuns.Run(file, [Entry(file, "Finalize", [], [Column("id")])], source);

        var problems = run.Problems();
        problems.Count.ShouldBe(1);
        problems[0].ShouldStartWith("SQLSRC274 ");
        StepOutputs
            .Single<TypeMethods>(run, TrackingNames.TypeMethods)
            .Diagnostics.Count(diagnostic => diagnostic.Descriptor.Id == "SQLSRC274")
            .ShouldBe(1);
    }

    // The asynchronous method takes the token, and a method that takes a parameter has another signature, so neither is
    // object.Finalize.
    [Theory]
    [InlineData("method-type=async", false)]
    [InlineData("method-type=sync", true)]
    [InlineData("method-type=both", true)]
    public void Run_RepositoryMethodNamedFinalizeWithAnotherSignature_IsClean(string parameters, bool takesParameter)
    {
        var file = UsersFile(
            "-- generator: "
                + parameters
                + "\n-- name: Finalize\nSELECT id FROM users"
                + (takesParameter ? " WHERE id = @id" : "")
                + ";\n"
        );
        var entry = takesParameter
            ? Entry(file, "Finalize", [Parameter("id")], [Column("id")])
            : Entry(file, "Finalize", [], [Column("id")]);

        var run = RepositoryRuns.Run(file, [entry], RepositoryRuns.Source);

        run.ShouldBeClean();
        run.Sources[MethodsFile].ShouldContain(" Finalize");
    }

    // A token is an argument, so a Finalize whose only argument is one has another signature than object.Finalize.
    [Theory]
    [InlineData("method-type=sync")]
    [InlineData("method-type=both")]
    public void Run_SynchronousRepositoryMethodNamedFinalizeWithATokenAlone_IsClean(string parameters)
    {
        var file = UsersFile(
            "-- generator: "
                + parameters
                + "\n-- name: Finalize\n-- token: {{table:users}}\nSELECT id FROM {{table}};\n"
        );

        var run = RepositoryRuns.Run(file, [Entry(file, "Finalize", [], [Column("id")])], RepositoryRuns.Source);

        run.ShouldBeClean();
        run.Sources[MethodsFile].ShouldContain(" Finalize(\n");
    }

    // Only Finalize has the signature of an object's method that the compiler warns about, so the names of the others
    // with no argument are no problem.
    [Theory]
    [InlineData("ToString")]
    [InlineData("GetHashCode")]
    public void Run_SynchronousRepositoryMethodNamedAsAnotherMethodOfObject_IsClean(string name)
    {
        var file = UsersFile("-- generator: method-type=sync\n-- name: " + name + "\nSELECT id FROM users;\n");

        var run = RepositoryRuns.Run(file, [Entry(file, name, [], [Column("id")])], RepositoryRuns.Source);

        run.ShouldBeClean();
        run.Sources[MethodsFile].ShouldContain(" " + name + "()\n");
    }

    // Every method's cancellation token would hide the parameter, whatever the method-type.
    [Fact]
    public void Run_MethodTypeSyncSourceParameterNamedCancellationToken_ReportsSqlsrc036()
    {
        var run = RunGetUser(
            "method-type=sync",
            SampleOf("public partial class Sample(NpgsqlDataSource cancellationToken);")
        );

        run.Problems().ShouldHaveSingleItem().ShouldStartWith("SQLSRC036 ");
    }

    // A synchronous method without parameters or tokens takes no argument at all.
    [Fact]
    public void Run_MethodTypeSyncWithoutArguments_DeclaresAnEmptyArgumentList()
    {
        var file = UsersFile("-- generator: method-type=sync\n-- name: CountUsers -> one\nSELECT id FROM users;\n");

        var run = RepositoryRuns.Run(file, [Entry(file, "CountUsers", [], [Column("id")])]);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("        public global::App.CountUsersDto CountUsers()\n        {\n");
        methods.ShouldContain("        global::App.CountUsersDto CountUsers();\n");
    }

    // A query of none returns nothing, and documents no return.
    [Fact]
    public void Run_MethodTypeSyncNone_ReturnsVoid()
    {
        var file = UsersFile("-- generator: method-type=sync\n-- name: Touch -> none\nUPDATE users SET id = @id;\n");

        var run = RepositoryRuns.Run(file, [Entry(file, "Touch", [Parameter("id")], [])]);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("        public void Touch(\n");
        methods.ShouldContain("        void Touch(\n");
        methods.ShouldNotContain("<returns>");
    }

    // SQLSRC299 at GetUser's name on the line of the file.
    private static string Sqlsrc299At(int line) =>
        "SQLSRC299 /app/Repo/Users.sql("
        + line
        + ",10)-("
        + line
        + ",17): The query 'GetUser' would have a synchronous method, but the one connection of its database "
        + "'postgres' in 'App.Sample', 'OpenAsync', returns a task, which a synchronous method cannot call.  Mark a "
        + "member that returns no task [SqlSourceConnection] beside it, or give the query method-type=async.";
}
