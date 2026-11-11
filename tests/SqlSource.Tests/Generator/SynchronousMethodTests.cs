// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System.Linq;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Methods;
using SqlSource.Settings;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The methods that method-type gives a query: a synchronous method named as the query under sync, beside the
// asynchronous one, which always ends in Async, under both, their names' clashes, and SQLSRC298 for a stream under
// sync.  In this part a collection method and a repository's method stay asynchronous.  Each run is under postgres on
// Npgsql 10.
public sealed class SynchronousMethodTests
{
    private const string GetUserText = "-- name: GetUser -> one\nSELECT id FROM users WHERE id = @id;\n";

    private const string StreamText =
        "-- collection-type: IAsyncEnumerable\n-- name: ListUsers\nSELECT id FROM users WHERE id > @id;\n";

    private const string SyncGetUser = "internal static global::App.GetUserDto GetUser(\n";

    private const string AsyncGetUserAsync =
        "internal static async global::System.Threading.Tasks.Task<global::App.GetUserDto> GetUserAsync(\n";

    private static SqlFile UsersFile(string text) => new(GeneratorHarness.UsersPath, text);

    private static GeneratorRun RunGetUser(string text)
    {
        var file = UsersFile(text);
        return MethodRuns.Run(file, [Entry(file, "GetUser", [Parameter("id")], [Column("id")])]);
    }

    private static GeneratorRun RunStream(string text, bool isRepository = false)
    {
        var file = UsersFile(text);
        SidecarEntry[] entries = [Entry(file, "ListUsers", [Parameter("id")], [Column("id")])];
        return isRepository ? RepositoryRuns.Run(file, entries) : MethodRuns.Run(file, entries);
    }

    private static QueryMethod[] MethodsOf(GeneratorRun run) =>
        [.. StepOutputs.Single<TypeMethods>(run, TrackingNames.TypeMethods).Methods];

    private static int CountOf(string text, string value) =>
        text.Split([value], System.StringSplitOptions.None).Length - 1;

    [Fact]
    public void Run_MethodTypeSync_WritesOnlyASynchronousMethod()
    {
        var run = RunGetUser("-- generator: method-type=sync\n" + GetUserText);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(SyncGetUser);
        methods.ShouldNotContain("Task");
        methods.ShouldNotContain("GetUserAsync");
        var method = MethodsOf(run).ShouldHaveSingleItem();
        method.Name.ShouldBe("GetUser");
        method.Synchrony.ShouldBe(MethodSynchrony.Synchronous);
    }

    // Under both the asynchronous method ends in Async whatever async-method-suffix says, so that the two never share
    // a name.
    [Theory]
    [InlineData("method-type=both")]
    [InlineData("method-type=both async-method-suffix")]
    [InlineData("method-type=both async-method-suffix=off")]
    public void Run_MethodTypeBoth_WritesGetUserAndGetUserAsync(string parameters)
    {
        var run = RunGetUser("-- generator: " + parameters + "\n" + GetUserText);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(SyncGetUser);
        methods.ShouldContain(AsyncGetUserAsync);
        var planned = MethodsOf(run);
        planned.Select(method => method.Name).ShouldBe(["GetUser", "GetUserAsync"]);
        planned
            .Select(method => method.Synchrony)
            .ShouldBe([MethodSynchrony.Synchronous, MethodSynchrony.Asynchronous]);
    }

    // GetUser's asynchronous method takes the name of GetUserAsync's synchronous one: SQLSRC274 at both, never CS0111,
    // and neither query gets a method.
    [Fact]
    public void Run_MethodTypeBothAndAQueryNamedWithAsync_ReportsSQLSRC274()
    {
        var file = UsersFile(
            "-- generator: method-type=both\n\n-- name: GetUser\nSELECT id FROM users;\n"
                + "-- name: GetUserAsync\nSELECT id FROM users;\n"
        );

        var run = MethodRuns.Run(
            file,
            [Entry(file, "GetUser", [], [Column("id")]), Entry(file, "GetUserAsync", [], [Column("id")])]
        );

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(3,10)-(3,17): The method of the query 'GetUser' cannot be named "
                    + "'GetUserAsync', because the method of the query 'GetUserAsync' has that name too.  Rename the "
                    + "query.",
                "SQLSRC274 /app/Repo/Users.sql(5,10)-(5,22): The method of the query 'GetUserAsync' cannot be named "
                    + "'GetUserAsync', because the method of the query 'GetUser' has that name too.  Rename the query.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // A member of the type takes the name of the asynchronous method on it, and the query gets no method.
    [Fact]
    public void Run_MethodTypeBothAndAMemberNamedGetUserAsync_ReportsSQLSRC274()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample
                {
                    public static int GetUserAsync => 0;
                }
            }
            """;
        var file = UsersFile(
            "-- generator: method-type=both\n-- member-location: class\n-- visibility: public\n\n" + GetUserText
        );

        var run = MethodRuns.Run(file, [Entry(file, "GetUser", [Parameter("id")], [Column("id")])], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(5,10)-(5,17): The method of the query 'GetUser' cannot be named "
                    + "'GetUserAsync', because 'App.Sample' has a member of that name.  Rename the query.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // A stream has no synchronous method, and the query gets none, its collection method neither.
    [Fact]
    public void Run_MethodTypeSyncStream_ReportsSQLSRC298AndWritesNoMethod()
    {
        var run = RunStream("-- generator: method-type=sync collection-method\n" + StreamText);

        run.Problems()
            .ShouldBe([
                "SQLSRC298 /app/Repo/Users.sql(3,10)-(3,19): The query 'ListUsers' streams its rows as an "
                    + "IAsyncEnumerable, which has no synchronous method; give it method-type=async or "
                    + "method-type=both",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // Under both a stream gets its asynchronous method alone, named with Async, and its collection method, which
    // returns an array, of the same name.
    [Fact]
    public void Run_MethodTypeBothStream_WritesOnlyTheAsynchronousStreamNamedAsync()
    {
        var run = RunStream("-- generator: method-type=both collection-method\n" + StreamText);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(
            "global::System.Collections.Generic.IAsyncEnumerable<global::App.ListUsersDto> ListUsersAsync(\n"
        );
        methods.ShouldNotContain(" ListUsers(\n");
        var planned = MethodsOf(run);
        planned.Select(method => method.Name).ShouldBe(["ListUsersAsync", "ListUsersAsync"]);
        planned.ShouldAllBe(method => method.Synchrony == MethodSynchrony.Asynchronous);
        planned[0].Streams.ShouldBeTrue();
        planned[1].Collection.ShouldBe(CollectionKind.Array);
    }

    // In this part a collection method stays asynchronous, and under sync takes the synchronous method's name, as an
    // overload by its items.
    [Fact]
    public void Run_MethodTypeSyncCollectionMethod_WritesTheSynchronousMethodAndAnAsyncCollectionMethodOfOneName()
    {
        var run = RunGetUser("-- generator: method-type=sync collection-method\n" + GetUserText);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(SyncGetUser);
        methods.ShouldContain("global::System.Threading.Tasks.Task<global::App.GetUserDto[]> GetUser(\n");
        methods.ShouldNotContain("GetUserAsync");
        var planned = MethodsOf(run);
        planned.Select(method => method.Name).ShouldBe(["GetUser", "GetUser"]);
        planned
            .Select(method => method.Synchrony)
            .ShouldBe([MethodSynchrony.Synchronous, MethodSynchrony.Asynchronous]);
        planned[0].Batch.ShouldBeNull();
        _ = planned[1].Batch.ShouldNotBeNull();
    }

    // Under both the collection method is the asynchronous method's overload, named with Async.
    [Fact]
    public void Run_MethodTypeBothCollectionMethod_WritesTheCollectionMethodNamedAsync()
    {
        var run = RunGetUser("-- generator: method-type=both collection-method\n" + GetUserText);

        run.ShouldBeClean();
        var planned = MethodsOf(run);
        planned.Select(method => method.Name).ShouldBe(["GetUser", "GetUserAsync", "GetUserAsync"]);
        planned
            .Select(method => method.Synchrony)
            .ShouldBe([MethodSynchrony.Synchronous, MethodSynchrony.Asynchronous, MethodSynchrony.Asynchronous]);
        _ = planned[2].Batch.ShouldNotBeNull();
    }

    // In this part a repository's method stays asynchronous, named under sync as under async.
    [Theory]
    [InlineData("method-type=sync", "GetUser")]
    [InlineData("method-type=sync async-method-suffix", "GetUserAsync")]
    public void Run_MethodTypeSyncRepository_WritesTheAsynchronousMethod(string parameters, string name)
    {
        var file = UsersFile("-- generator: " + parameters + "\n" + GetUserText);

        var run = RepositoryRuns.Run(file, [Entry(file, "GetUser", [Parameter("id")], [Column("id")])]);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(
            "        public async global::System.Threading.Tasks.Task<global::App.GetUserDto> " + name + "(\n"
        );
        methods.ShouldContain("        global::System.Threading.Tasks.Task<global::App.GetUserDto> " + name + "(\n");
        var method = MethodsOf(run).ShouldHaveSingleItem();
        method.Name.ShouldBe(name);
        method.Synchrony.ShouldBe(MethodSynchrony.Asynchronous);
    }

    // Under both a repository's query gets its asynchronous method alone, named with Async, on the type and in its
    // interface.
    [Fact]
    public void Run_MethodTypeBothRepository_WritesOnlyTheAsynchronousMethodNamedAsync()
    {
        var file = UsersFile("-- generator: method-type=both\n" + GetUserText);

        var run = RepositoryRuns.Run(file, [Entry(file, "GetUser", [Parameter("id")], [Column("id")])]);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(
            "        public async global::System.Threading.Tasks.Task<global::App.GetUserDto> GetUserAsync(\n"
        );
        methods.ShouldContain("        global::System.Threading.Tasks.Task<global::App.GetUserDto> GetUserAsync(\n");
        CountOf(methods, " GetUserAsync(\n").ShouldBe(2);
        methods.ShouldNotContain(" GetUser(\n");
        var method = MethodsOf(run).ShouldHaveSingleItem();
        method.Name.ShouldBe("GetUserAsync");
        method.Synchrony.ShouldBe(MethodSynchrony.Asynchronous);
    }

    [Fact]
    public void Run_MethodTypeSyncRepositoryStream_ReportsSQLSRC298()
    {
        var run = RunStream("-- generator: method-type=sync\n" + StreamText, isRepository: true);

        run.Problems()
            .ShouldBe([
                "SQLSRC298 /app/Repo/Users.sql(3,10)-(3,19): The query 'ListUsers' streams its rows as an "
                    + "IAsyncEnumerable, which has no synchronous method; give it method-type=async or "
                    + "method-type=both",
            ]);
        run.Sources[MethodRuns.MethodsFile].ShouldNotContain(" ListUsers");
    }

    // At models a query has no method, so method-type does nothing: the run is the one without it.
    [Fact]
    public void Run_MethodTypeSyncAtModels_WritesNoMethod()
    {
        var file = UsersFile("-- output: models\n" + StreamText);
        var entry = Entry(file, "ListUsers", [Parameter("id")], [Column("id")]);

        var sync = Run(
            file,
            [entry], /*lang=json,strict*/
            """{"generatorParameters": ["method-type=sync"]}"""
        );
        var asynchronous = Run(file, [entry], null);

        sync.ShouldBeClean();
        sync.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
        sync.Sources.ShouldBe(asynchronous.Sources);
    }

    // The asynchronous method of GetUser would have the name of the nested class that holds it.
    [Fact]
    public void Run_MethodTypeBothNestedClassNamedGetUserAsync_ReportsSQLSRC009()
    {
        var run = RunGetUser(
            "-- generator: method-type=both\n-- member-location: nested-class\n-- nested-class: GetUserAsync\n\n"
                + GetUserText
        );

        run.Problems()
            .ShouldBe([
                "SQLSRC009 /app/Repo/Users.sql(5,10)-(5,17): A query of 'Sample' cannot be named 'GetUser', because "
                    + "its member would have the name of the type that contains it",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // Under sync the method has no Async after its name, so a nested class of that name holds it, whatever
    // async-method-suffix says.
    [Fact]
    public void Run_MethodTypeSyncNestedClassNamedGetUserAsync_WritesTheMethod()
    {
        var run = RunGetUser(
            "-- generator: method-type=sync async-method-suffix\n-- member-location: nested-class\n"
                + "-- nested-class: GetUserAsync\n\n"
                + GetUserText
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldContain("public static global::App.GetUserDto GetUser(\n");
    }
}
