// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.CachingScene;
using static SqlSource.Testing.Generator.StepReasons;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The caching of the methods: which types' methods an edit makes the generator write again.  Users is claimed by
// UserQueries and Orders by OrderQueries, both in the namespace App and at CodeGen, so each type has one method, in its
// extension class unless the test says otherwise.  One test adds AdminQueries, in the namespace App.Admin, which claims
// Users too.
public sealed class CachingMethodTests
{
    private const string AdminMethods = "App.Admin.AdminQueries-Methods.g.cs";

    private readonly CachingScene _scene = new();

    // Orders with its query renamed GetUser, as UserQueries' is, and a model of its own by its marker.
    private readonly InMemoryAdditionalText _clashingOrders;

    private readonly InMemoryAdditionalText _clashingOrdersSidecar;

    public CachingMethodTests()
    {
        _clashingOrders = new InMemoryAdditionalText(
            _scene.Orders.Path,
            "-- name: GetUser\n-- output-model: App.OrderRow\nSELECT 2;\n"
        );
        _clashingOrdersSidecar = TypedSidecar(_clashingOrders, "GetUser", "int4");
    }

    // The edit changes the SQL of Users within its line, with its sidecar written for the new hash, so the entry stays
    // current.  Only UserQueries plans and writes its methods again.
    [Fact]
    public void Run_EditToOneQuery_ReEmitsOnlyTheMethodsOfItsClaimingType()
    {
        var compilation = MethodsCompilation(UserQueries());
        var usersSidecar = TypedSidecar(_scene.Users, "GetUser", "int4");
        var driver = _scene.FirstRunOfMethods(compilation, usersSidecar);

        var result = Run(EditUsersSql(driver, usersSidecar, "SELECT 10;"), compilation);

        result.Diagnostics.ShouldBeEmpty();

        // The models are as they were, so OrderQueries' input is taken from the previous run.
        MethodReasons(result, TrackingNames.MethodsInput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
        MethodReasons(result, TrackingNames.TypeMethods)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
        MethodReasons(result, TrackingNames.MethodOutput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
        TypeReasons(result)["App.OrderQueries.g.cs"].ShouldBe(IncrementalStepRunReason.Cached);
        MethodsOf(result, UserMethods).ShouldContain("SELECT 10;");
    }

    // An entry is no part of the type's file, so the file is not written again, and the methods of Users are.
    [Fact]
    public void Run_SidecarEdit_ReEmitsTheMethodsAndNotTheTypesFile()
    {
        var compilation = MethodsCompilation(UserQueries());
        var usersSidecar = TypedSidecar(_scene.Users, "GetUser", "int4");
        var driver = _scene.FirstRunOfMethods(compilation, usersSidecar);
        MethodsOf(driver.GetRunResult(), UserMethods).ShouldContain("GetFieldValue<int>(");

        // The column of Users is now text.
        var result = Run(
            driver.ReplaceAdditionalText(usersSidecar, TypedSidecar(_scene.Users, "GetUser", "text")),
            compilation
        );

        result.Diagnostics.ShouldBeEmpty();
        AllReasons(result, TrackingNames.TypeOutput)
            .ShouldBe([IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached]);
        MethodReasons(result, TrackingNames.TypeMethods)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
        MethodReasons(result, TrackingNames.MethodOutput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );

        // A call gives no column, so the signatures are equal and the conflicts are not looked for again.
        AllReasons(result, TrackingNames.MethodSignatures).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.MethodConflicts).ShouldBe([IncrementalStepRunReason.Cached]);
        MethodsOf(result, UserMethods).ShouldContain("GetFieldValue<string>(");
    }

    // The methods of UserQueries move from its extension class onto the type, in the file of the same name.  The
    // conflicts are found again, since the extension classes changed, and are none, as before, so OrderQueries' methods
    // are not written again.
    [Fact]
    public void Run_MethodLocationEdit_ReEmitsThatTypesMethods()
    {
        var compilation = MethodsCompilation(UserQueries());
        var driver = _scene.FirstRunOfMethods(compilation);
        MethodsOf(driver.GetRunResult(), UserMethods).ShouldContain("class UserQueriesExtensions");

        var result = Run(driver, WithUserQueries(compilation, UserQueries("MethodLocation = MethodLocation.Public")));

        result.Diagnostics.ShouldBeEmpty();
        MethodReasons(result, TrackingNames.MethodOutput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.ExtensionClassNames).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.MethodConflicts).ShouldBe([IncrementalStepRunReason.Unchanged]);
        var methods = MethodsOf(result, UserMethods);
        methods.ShouldNotContain("UserQueriesExtensions");
        methods.ShouldContain("partial class UserQueries");
    }

    // The suffix renames the model of UserQueries, which its method reads.  The models' names are compared with the
    // extension classes again, and clash with none.
    [Fact]
    public void Run_ModelSettingEdit_ReEmitsTheMethodsThatReadTheModel()
    {
        var compilation = MethodsCompilation(UserQueries());
        var driver = _scene.FirstRunOfMethods(compilation);

        var result = Run(driver, WithUserQueries(compilation, UserQueries("OutputModelSuffix = \"Row\"")));

        result.Diagnostics.ShouldBeEmpty();
        MethodReasons(result, TrackingNames.MethodsInput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Unchanged),
                ignoreOrder: true
            );
        MethodReasons(result, TrackingNames.MethodOutput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.MethodConflicts).ShouldBe([IncrementalStepRunReason.Unchanged]);
        MethodsOf(result, UserMethods).ShouldContain("global::App.GetUserRow");
    }

    // UserQueries' methods go on itself, so the names of its members are among what it plans them from.  The new
    // member takes no method's name, so the plan is equal, and nothing is written again.
    [Fact]
    public void Run_MemberAddedToATypePlacedOnItself_ReachesNoOutput()
    {
        const string Arguments = "MethodLocation = MethodLocation.Public";
        var compilation = MethodsCompilation(UserQueries(Arguments));
        var driver = _scene.FirstRunOfMethods(compilation);

        var result = Run(
            driver,
            WithUserQueries(compilation, UserQueries(Arguments, "    public static int Count() => 0;\n"))
        );

        result.Diagnostics.ShouldBeEmpty();
        AllReasons(result, TrackingNames.TypeMemberNames).ShouldBe([IncrementalStepRunReason.Modified]);
        MethodReasons(result, TrackingNames.MethodsInput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Unchanged),
                ignoreOrder: true
            );
        MethodReasons(result, TrackingNames.TypeMethods)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Unchanged, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
        MethodReasons(result, TrackingNames.MethodOutput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
        OutputReasons(result).ShouldAllBeCached();
    }

    // A C# edit gives the generator a new compilation, so the probes and the lookup of the extension classes run
    // again, and give equal values.  No type plans or writes its methods again.
    [Fact]
    public void Run_CSharpEdit_ReEmitsNoMethod()
    {
        var compilation = MethodsCompilation(UserQueries(members: "    public static int Count() => 0;\n"));
        var driver = _scene.FirstRunOfMethods(compilation);

        var result = Run(
            driver,
            WithUserQueries(compilation, UserQueries(members: "    public static int Count() => 1;\n"))
        );

        result.Diagnostics.ShouldBeEmpty();
        AllReasons(result, TrackingNames.MethodDrivers).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.TakenExtensionClasses).ShouldBe([IncrementalStepRunReason.Unchanged]);
        MethodReasons(result, TrackingNames.MethodsInput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
        OutputReasons(result).ShouldAllBeCached();
    }

    // Npgsql 9 allows the methods as Npgsql 10 does, so the drivers' value is equal, and no type gathers its input
    // again.
    [Fact]
    public void Run_DriverVersionEdit_ReEmitsNoMethod()
    {
        var compilation = MethodsCompilation(UserQueries());
        var driver = _scene.FirstRunOfMethods(compilation);

        var result = Run(driver, compilation.ReplaceReference(NpgsqlReferences.Npgsql10, NpgsqlReferences.Npgsql9));

        result.Diagnostics.ShouldBeEmpty();
        AllReasons(result, TrackingNames.MethodDrivers).ShouldBe([IncrementalStepRunReason.Unchanged]);
        MethodReasons(result, TrackingNames.MethodsInput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
        MethodReasons(result, TrackingNames.MethodOutput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
    }

    // A class of the project takes the name of UserQueries' extension class, which SQLSRC276 reports, and UserQueries
    // gets no methods.  OrderQueries' are not written again.
    [Fact]
    public void Run_TypeTakesAnExtensionClassName_ReEmitsOnlyThatTypesMethods()
    {
        var compilation = MethodsCompilation(UserQueries());
        var driver = _scene.FirstRunOfMethods(compilation);
        var taken = CSharpSyntaxTree.ParseText(
            "namespace App;\n\npublic static class UserQueriesExtensions;\n",
            GeneratorHarness.ParseOptions,
            "/app/Users/UserQueriesExtensions.cs",
            cancellationToken: TestContext.Current.CancellationToken
        );

        var result = Run(driver, compilation.AddSyntaxTrees(taken));

        result.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("SQLSRC276");
        AllReasons(result, TrackingNames.TakenExtensionClasses).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.MethodConflicts).ShouldBe([IncrementalStepRunReason.Modified]);
        MethodReasons(result, TrackingNames.TypeMethods)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
        MethodReasons(result, TrackingNames.MethodOutput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
        result.GeneratedTrees.ShouldNotContain(tree => tree.FilePath.EndsWith(UserMethods, StringComparison.Ordinal));
    }

    // Orders' query is renamed GetUser, its model kept apart by its marker, so that its method and UserQueries' have
    // one name and one parameter list in two extension classes of App: SQLSRC274 at each, and neither is written.
    // UserQueries planned nothing again; its methods are written again from its slice of the conflicts.
    [Fact]
    public void Run_EditThatMakesAClash_ReEmitsBothTypes()
    {
        var compilation = MethodsCompilation(UserQueries());
        var ordersSidecar = TypedSidecar(_scene.Orders, "GetOrder", "int4");
        var driver = _scene.FirstRunOfMethods(compilation, ordersSidecar);

        var result = Run(MakeClash(driver, ordersSidecar), compilation);

        result.Diagnostics.Select(diagnostic => diagnostic.Id).ShouldBe(["SQLSRC274", "SQLSRC274"]);
        AllReasons(result, TrackingNames.MethodConflicts).ShouldBe([IncrementalStepRunReason.Modified]);
        MethodReasons(result, TrackingNames.TypeMethods)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Cached, IncrementalStepRunReason.Modified),
                ignoreOrder: true
            );
        MethodReasons(result, TrackingNames.MethodOutput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Modified),
                ignoreOrder: true
            );
        result.GeneratedTrees.ShouldNotContain(tree =>
            tree.FilePath.EndsWith("-Methods.g.cs", StringComparison.Ordinal)
        );
    }

    // The edit back to GetOrder removes the clash, and both types' methods are written again.
    [Fact]
    public void Run_EditThatRemovesAClash_ReEmitsBothTypes()
    {
        var compilation = MethodsCompilation(UserQueries());
        var ordersSidecar = TypedSidecar(_scene.Orders, "GetOrder", "int4");
        var driver = _scene.FirstRunOfMethods(compilation, ordersSidecar);
        var clashing = MakeClash(driver, ordersSidecar)
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        clashing.GetRunResult().Diagnostics.ShouldNotBeEmpty();

        var result = Run(
            clashing
                .ReplaceAdditionalText(_clashingOrders, _scene.Orders)
                .ReplaceAdditionalText(_clashingOrdersSidecar, ordersSidecar),
            compilation
        );

        result.Diagnostics.ShouldBeEmpty();
        AllReasons(result, TrackingNames.MethodConflicts).ShouldBe([IncrementalStepRunReason.Modified]);
        MethodReasons(result, TrackingNames.MethodOutput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Modified),
                ignoreOrder: true
            );
        MethodsOf(result, UserMethods).ShouldContain("GetUser(");
        MethodsOf(result, OrderMethods).ShouldContain("GetOrder(");
    }

    // The edit changes the SQL of Users within its line, so every query's name keeps its place, and its method's
    // signature, what a call sees of it, is equal.  The conflicts are not looked for again, and nothing that every
    // type's methods give together changes.
    [Fact]
    public void Run_EditThatLeavesTheSignaturesAlone_ChecksNoConflictAgain()
    {
        var compilation = MethodsCompilation(UserQueries());
        var usersSidecar = TypedSidecar(_scene.Users, "GetUser", "int4");
        var driver = _scene.FirstRunOfMethods(compilation, usersSidecar);

        var result = Run(EditUsersSql(driver, usersSidecar, "SELECT 7;"), compilation);

        result.Diagnostics.ShouldBeEmpty();
        MethodReasons(result, TrackingNames.TypeMethods)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
        AllReasons(result, TrackingNames.MethodSignatures).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.ExtensionClassNames).ShouldBe([IncrementalStepRunReason.Cached]);

        // The lookup in the project runs on every run, and gives the same answer.
        AllReasons(result, TrackingNames.TakenExtensionClasses).ShouldBe([IncrementalStepRunReason.Unchanged]);
        AllReasons(result, TrackingNames.MethodConflicts).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.MethodDiagnostics).ShouldBe([IncrementalStepRunReason.Cached]);
        AllReasons(result, TrackingNames.CodeGenEngines).ShouldBe([IncrementalStepRunReason.Cached]);
    }

    // AdminQueries, in a namespace of its own so that its method and model clash with none, claims Users too.  The edit
    // to Users re-emits the methods of both types that claim it, and not those of OrderQueries.
    [Fact]
    public void Run_EditToAFileTwoTypesClaim_ReEmitsTheMethodsOfBothAndNoOther()
    {
        var compilation = MethodsCompilation(UserQueries())
            .AddSyntaxTrees(
                CSharpSyntaxTree.ParseText(
                    TypeSource("App.Admin", "AdminQueries", "Path = \"Users.sql\""),
                    GeneratorHarness.ParseOptions,
                    "/app/Users/AdminQueries.cs",
                    cancellationToken: TestContext.Current.CancellationToken
                )
            );
        var usersSidecar = TypedSidecar(_scene.Users, "GetUser", "int4");
        var driver = _scene.FirstRunOfMethods(compilation, usersSidecar);

        var result = Run(EditUsersSql(driver, usersSidecar, "SELECT 10;"), compilation);

        result.Diagnostics.ShouldBeEmpty();
        var expected = new Dictionary<string, IncrementalStepRunReason>
        {
            [UserMethods] = IncrementalStepRunReason.Modified,
            [OrderMethods] = IncrementalStepRunReason.Cached,
            [AdminMethods] = IncrementalStepRunReason.Modified,
        };
        MethodReasons(result, TrackingNames.TypeMethods).ShouldBe(expected, ignoreOrder: true);
        MethodReasons(result, TrackingNames.MethodOutput).ShouldBe(expected, ignoreOrder: true);
        MethodsOf(result, UserMethods).ShouldContain("SELECT 10;");
        MethodsOf(result, AdminMethods).ShouldContain("SELECT 10;");
    }

    // The query of Users is renamed FindUser, so its method's signature changes, and the conflicts are looked for
    // again.  There are none, as before, so OrderQueries' methods are not written again.
    [Fact]
    public void Run_QueryRenamed_ComparesTheSignaturesAgain()
    {
        var compilation = MethodsCompilation(UserQueries());
        var usersSidecar = TypedSidecar(_scene.Users, "GetUser", "int4");
        var driver = _scene.FirstRunOfMethods(compilation, usersSidecar);

        var result = Run(EditUsers(driver, usersSidecar, "FindUser", "-- name: FindUser\nSELECT 1;\n"), compilation);

        result.Diagnostics.ShouldBeEmpty();
        AllReasons(result, TrackingNames.MethodSignatures).ShouldBe([IncrementalStepRunReason.Modified]);
        AllReasons(result, TrackingNames.MethodConflicts).ShouldBe([IncrementalStepRunReason.Unchanged]);
        MethodReasons(result, TrackingNames.MethodOutput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
        MethodsOf(result, UserMethods).ShouldContain("FindUser(");
    }

    // The entry of Users now has no rows, and the query asks for one, which SQLSRC272 reports at the query, and
    // UserQueries gets no method.  OrderQueries' methods are not written again.
    [Fact]
    public void Run_QueryGainsAProblem_ReportsTheMethodsProblemsAgain()
    {
        var compilation = MethodsCompilation(UserQueries());
        var usersSidecar = TypedSidecar(_scene.Users, "GetUser", "int4");
        var driver = _scene.FirstRunOfMethods(compilation, usersSidecar);
        var edited = new SqlFile(_scene.Users.Path, "-- name: GetUser -> one\nSELECT 1;\n");
        var noRows = Sidecars.Of(edited, ModelRuns.Entry(edited, "GetUser", [], null));

        var result = Run(
            driver
                .ReplaceAdditionalText(_scene.Users, new InMemoryAdditionalText(edited.Path, edited.Text))
                .ReplaceAdditionalText(usersSidecar, new InMemoryAdditionalText(noRows.Path, noRows.Text)),
            compilation
        );

        result.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("SQLSRC272");
        AllReasons(result, TrackingNames.MethodDiagnostics).ShouldBe([IncrementalStepRunReason.Modified]);
        MethodReasons(result, TrackingNames.MethodOutput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
    }

    // Both types are at Models by their attributes, so the project has no CodeGen query, and then UserQueries is at
    // CodeGen again: its query's engine is now among those of the project's methods.
    [Fact]
    public void Run_TypeTurnedToCodeGen_FindsTheEnginesAgain()
    {
        const string AtModels = "Output = GeneratorOutput.Models";
        var compilation = MethodsCompilation(UserQueries(AtModels), TypeSource("App", "OrderQueries", AtModels));
        var driver = _scene.FirstRunOfMethods(compilation);

        var result = Run(driver, WithUserQueries(compilation, UserQueries()));

        result.Diagnostics.ShouldBeEmpty();
        AllReasons(result, TrackingNames.CodeGenEngines).ShouldBe([IncrementalStepRunReason.Modified]);
        MethodReasons(result, TrackingNames.MethodOutput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
        MethodsOf(result, UserMethods).ShouldContain("GetUser(");
    }

    // Under mssql a parameter is bound by the facets of its described type, which change no model's C#: an edit of
    // the length of Users' parameter alone writes UserQueries' methods again, with the new Size.
    [Fact]
    public void Run_ParameterFacetsEdit_ReEmitsTheMethodsWithTheNewSize()
    {
        var users = new InMemoryAdditionalText(_scene.Users.Path, "-- name: GetUser\nSELECT 1 WHERE @name = N'a';\n");
        var compilation = GeneratorHarness.CreateCompilation(
            [new(UsersSourcePath, UserQueries()), new(OrdersSourcePath, TypeSource("App", "OrderQueries"))],
            references: [SqlClientReferences.SqlClient7]
        );
        var usersSidecar = SqlServerSidecar(users, "GetUser", TestSidecars.SqlServer("nvarchar(50)", 100, 0));
        var driver = CreateDriver(
                ProjectFile(OutputKind.CodeGen, "mssql"),
                [users, _scene.Orders, usersSidecar, SqlServerSidecar(_scene.Orders, "GetOrder", null)]
            )
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        driver.GetRunResult().Diagnostics.ShouldBeEmpty();
        MethodsOf(driver.GetRunResult(), UserMethods).ShouldContain("parameter.Size = 50;\n");

        var result = Run(
            driver.ReplaceAdditionalText(
                usersSidecar,
                SqlServerSidecar(users, "GetUser", TestSidecars.SqlServer("nvarchar(60)", 120, 0))
            ),
            compilation
        );

        result.Diagnostics.ShouldBeEmpty();
        AllReasons(result, TrackingNames.TypeOutput)
            .ShouldBe([IncrementalStepRunReason.Cached, IncrementalStepRunReason.Cached]);
        MethodReasons(result, TrackingNames.MethodOutput)
            .ShouldBe(
                UsersAndOrders(IncrementalStepRunReason.Modified, IncrementalStepRunReason.Cached),
                ignoreOrder: true
            );
        var methods = MethodsOf(result, UserMethods);
        methods.ShouldContain("parameter.Size = 60;\n");
        methods.ShouldNotContain("parameter.Size = 50;\n");
    }

    // The source of UserQueries, in the namespace App, with these arguments on its attribute and these members.
    private static string UserQueries(string? arguments = null, string members = "") =>
        TypeSource("App", "UserQueries", arguments, members);

    // A current mssql sidecar of the file whose entry has one column, of int, and a parameter called name of this
    // type when it is given.
    private static InMemoryAdditionalText SqlServerSidecar(
        InMemoryAdditionalText sqlFile,
        string queryName,
        SqlServerType? parameterType
    )
    {
        var file = ToSqlFile(sqlFile);
        var sidecar = Sidecars.Of(
            file,
            MethodRuns.SqlServerEntry(
                file,
                queryName,
                parameterType is null ? [] : [TestSidecars.Parameter(0, "name", parameterType)],
                [TestSidecars.Column(0, "value", TestSidecars.SqlServer())]
            )
        );
        return new InMemoryAdditionalText(sidecar.Path, sidecar.Text);
    }

    // Users with this line of SQL in place of SELECT 1;, and its sidecar, which the first run had, written for the new
    // hash.
    private GeneratorDriver EditUsersSql(GeneratorDriver driver, InMemoryAdditionalText usersSidecar, string line) =>
        EditUsers(driver, usersSidecar, "GetUser", "-- name: GetUser\n" + line + "\n");

    // Users with this text, and its sidecar, which the first run had, written for the query of this name, with one
    // column, of int4.
    private GeneratorDriver EditUsers(
        GeneratorDriver driver,
        InMemoryAdditionalText usersSidecar,
        string queryName,
        string text
    )
    {
        var edited = new InMemoryAdditionalText(_scene.Users.Path, text);
        return driver
            .ReplaceAdditionalText(_scene.Users, edited)
            .ReplaceAdditionalText(usersSidecar, TypedSidecar(edited, queryName, "int4"));
    }

    // Orders and its sidecar, which the first run had, with the query renamed GetUser.
    private GeneratorDriver MakeClash(GeneratorDriver driver, InMemoryAdditionalText ordersSidecar) =>
        driver
            .ReplaceAdditionalText(_scene.Orders, _clashingOrders)
            .ReplaceAdditionalText(ordersSidecar, _clashingOrdersSidecar);
}
