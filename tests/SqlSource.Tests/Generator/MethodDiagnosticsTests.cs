// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The problems of one type's methods: a shape that needs rows on a statement that has none (SQLSRC272), a token named
// as a fixed argument (SQLSRC275), a method's name that something else takes (SQLSRC274), and a list after a NOT where
// SQL Server refuses an empty one (SQLSRC127).  A query with one gets no method, and the rest of the project still
// compiles with no warning in what is generated.  Each run is at CodeGen, under postgres on Npgsql 10 but for those of
// SQLSRC127, under mssql on SqlClient 7.
public sealed class MethodDiagnosticsTests
{
    private const string Path = GeneratorHarness.UsersPath;

    // The header of a file whose methods go on the type, public.
    private const string OnTheType = "-- member-location: class\n-- visibility: public\n\n";

    [Theory]
    [InlineData("one")]
    [InlineData("one-optional")]
    [InlineData("many")]
    public void Run_ShapeOfRowsOnAStatementWithoutRows_IsSqlsrc272(string shape)
    {
        var file = new SqlFile(Path, "-- name: SetName -> " + shape + "\nUPDATE users SET name = 'x';\n");

        var run = MethodRuns.Run(file, [Entry(file, "SetName", [], null)]);

        run.Problems()
            .ShouldBe([
                "SQLSRC272 /app/Repo/Users.sql(1,10)-(1,17): The query 'SetName' has the shape '"
                    + shape
                    + "', and its statement returns no rows.  Remove the shape, or write 'rowcount' or 'none'.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("rowcount")]
    public void Run_ShapeWithoutRowsOnAStatementWithRows_IsAMethod(string shape)
    {
        var file = new SqlFile(Path, "-- name: GetUser -> " + shape + "\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(file, [Entry(file, "GetUser", [], [Column("id")])]);

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldContain(" GetUser(\n");
    }

    // The shape changes nothing under Models, so it is not an error there.
    [Fact]
    public void Run_ShapeOfRowsOnAStatementWithoutRowsUnderModels_IsNoProblem()
    {
        var file = new SqlFile(Path, "-- name: SetName -> one\n-- output: models\nUPDATE users SET name = 'x';\n");

        var run = MethodRuns.Run(file, [Entry(file, "SetName", [], null)]);

        run.ShouldBeClean();
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // Two types that claim one file plan its queries alike, and a problem is reported once.
    [Fact]
    public void Run_TwoTypesClaimingOneFile_ReportSqlsrc272Once()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class First;

                [SqlSourceGenerate(Source = ".")]
                public partial class Second;
            }
            """;
        var file = new SqlFile(Path, "-- name: SetName -> one\nUPDATE users SET name = 'x';\n");

        var run = MethodRuns.Run(file, [Entry(file, "SetName", [], null)], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC272 /app/Repo/Users.sql(1,10)-(1,17): The query 'SetName' has the shape 'one', and its "
                    + "statement returns no rows.  Remove the shape, or write 'rowcount' or 'none'.",
            ]);
    }

    // A query gets every problem it has, not only the first.
    [Fact]
    public void Run_ShapeThatNeedsRowsAndATokenNamedAsAFixedArgument_ReportsBoth()
    {
        var file = new SqlFile(
            Path,
            "-- name: SetName -> one\n-- token: {{transaction:users}}\nUPDATE {{transaction}} SET name = 'x';\n"
        );

        var run = MethodRuns.Run(file, [Entry(file, "SetName", [], null)]);

        run.Problems()
            .ShouldBe([
                "SQLSRC272 /app/Repo/Users.sql(1,10)-(1,17): The query 'SetName' has the shape 'one', and its "
                    + "statement returns no rows.  Remove the shape, or write 'rowcount' or 'none'.",
                "SQLSRC275 /app/Repo/Users.sql(1,10)-(1,17): The token 'transaction' of the query 'SetName' has the "
                    + "name of an argument that a query method can have: connection, parameters, transaction, "
                    + "commandTimeout or cancellationToken.  Rename the token.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    [Fact]
    public void Run_TokenNamedAsAFixedArgument_IsSqlsrc275()
    {
        var file = new SqlFile(
            Path,
            "-- name: List\n-- token: {{transaction:users}}\nSELECT id FROM {{transaction}};\n"
        );

        var run = MethodRuns.Run(file, [Entry(file, "List", [], [Column("id")])]);

        run.Problems()
            .ShouldBe([
                "SQLSRC275 /app/Repo/Users.sql(1,10)-(1,14): The token 'transaction' of the query 'List' has the "
                    + "name of an argument that a query method can have: connection, parameters, transaction, "
                    + "commandTimeout or cancellationToken.  Rename the token.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // The SQL of SQLSRC127's runs: a list after a NOT inside an aggregate.
    private const string CountOthers =
        "-- name: CountOthers\nSELECT COUNT(IIF(id NOT IN @ids, 1, NULL)) AS c FROM dbo.users;\n";

    private const string RefusedEmpty =
        "SQLSRC127 /app/Repo/Users.sql(2,28)-(2,32): The list '@ids' stands after a NOT inside an aggregate, where "
        + "SQL Server refuses the form an empty list is sent in.  Test the list in a WITH query, or a subquery in "
        + "FROM, and use its result here.";

    [Fact]
    public void RunSqlServer_NegatedListInsideAnAggregate_IsSqlsrc127()
    {
        var file = new SqlFile(Path, CountOthers);

        var run = MethodRuns.RunSqlServer(file, [CountOthersEntry(file)]);

        run.Problems().ShouldBe([RefusedEmpty]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // The error is in the SQL, so it is known before the query is described.
    [Fact]
    public void RunSqlServer_NegatedListInsideAnAggregateWithoutAnEntry_IsSqlsrc127BesideTheMissingEntry()
    {
        var file = new SqlFile(Path, CountOthers);

        var run = MethodRuns.RunSqlServer(file, []);

        run.Problems().ShouldContain(RefusedEmpty);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // Only a method sends a list, so the SQL is no problem under the other outputs.  A query under sql needs no entry.
    [Theory]
    [InlineData("sql")]
    [InlineData("models")]
    public void RunSqlServer_NegatedListInsideAnAggregateUnderAnotherOutput_IsNoProblem(string output)
    {
        var file = new SqlFile(Path, "-- output: " + output + "\n\n" + CountOthers);

        var run = MethodRuns.RunSqlServer(file, output == "sql" ? [] : [CountOthersEntry(file)]);

        run.ShouldBeClean();
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    private static SidecarEntry CountOthersEntry(SqlFile file) =>
        MethodRuns.SqlServerEntry(
            file,
            "CountOthers",
            [TestSidecars.Parameter(0, "ids", TestSidecars.SqlServer(), false)],
            [TestSidecars.Column(0, "c", TestSidecars.SqlServer())]
        );

    // An instance member of the connection is found before an extension method of its name.
    [Theory]
    [InlineData("Open")]
    [InlineData("ToString")]
    public void Run_QueryNamedAsAMemberOfTheConnection_IsSqlsrc274(string name)
    {
        var file = new SqlFile(Path, "-- name: " + name + "\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(file, [Entry(file, name, [], [Column("id")])]);

        run.Problems()
            .ShouldBe([
                $"SQLSRC274 /app/Repo/Users.sql(1,10)-(1,{10 + name.Length}): The method of the query '{name}' "
                    + $"cannot be named '{name}', because DbConnection has a member of that name, which a call on a "
                    + "connection finds before the method.  Rename the query.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    [Fact]
    public void Run_QueryNamedAsTheExtensionClass_IsSqlsrc274()
    {
        var file = new SqlFile(Path, "-- name: SampleExtensions\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(file, [Entry(file, "SampleExtensions", [], [Column("id")])]);

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(1,10)-(1,26): The method of the query 'SampleExtensions' cannot be "
                    + "named 'SampleExtensions', because the class that holds the method has that name.  Rename the "
                    + "query.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    [Fact]
    public void Run_QueryNamedAsAMemberOfTheTypeUnderClass_IsSqlsrc274()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample
                {
                    public static int Count => 0;
                }
            }
            """;
        var file = new SqlFile(Path, OnTheType + "-- name: Count\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(file, [Entry(file, "Count", [], [Column("id")])], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(4,10)-(4,15): The method of the query 'Count' cannot be named 'Count', "
                    + "because 'App.Sample' has a member of that name.  Rename the query.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // A CodeGen query on the type has no constant there, so its name is judged as its method's.
    [Fact]
    public void Run_QueryNamedAsTheTypeUnderClass_IsSqlsrc274()
    {
        var file = new SqlFile(Path, OnTheType + "-- name: Sample\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(file, [Entry(file, "Sample", [], [Column("id")])]);

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(4,10)-(4,16): The method of the query 'Sample' cannot be named "
                    + "'Sample', because it is the name of the type 'App.Sample' that holds the method.  Rename the "
                    + "query.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    [Fact]
    public void Run_QueryNamedAsATypeParameterUnderClass_IsSqlsrc274()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample<TRow>;
            }
            """;
        var file = new SqlFile(Path, OnTheType + "-- name: TRow\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(file, [Entry(file, "TRow", [], [Column("id")])], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(4,10)-(4,14): The method of the query 'TRow' cannot be named 'TRow', "
                    + "because 'App.Sample<TRow>' has a type parameter of that name.  Rename the query.",
            ]);
        run.Sources.Keys.ShouldNotContain("App.Sample-1-Methods.g.cs");
    }

    // The nested class that holds the constant of a query whose output is sql is a member of the type, and the
    // method of Get on the type under async-method-suffix would take its name.
    [Fact]
    public void Run_MethodNamedAsTheNestedClassUnderClass_IsSqlsrc274()
    {
        var file = new SqlFile(
            Path,
            "-- name: Get\n-- generator: async-method-suffix\n-- member-location: class\n-- visibility: public\n"
                + "SELECT id FROM users;\n"
                + "-- name: Other\n-- output: sql\n-- nested-class: GetAsync\nSELECT 1;\n"
        );

        var run = MethodRuns.Run(file, [Entry(file, "Get", [], [Column("id")])]);

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(1,10)-(1,13): The method of the query 'Get' cannot be named 'GetAsync', "
                    + "because 'App.Sample' has a nested class of that name.  Rename the query.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // Under the member location class the constant of a query whose output is sql is a member of the type, and the
    // method of Get under async-method-suffix would take its name.
    [Fact]
    public void Run_MethodNamedAsTheConstantOfAnotherQueryUnderClass_IsSqlsrc274()
    {
        var file = new SqlFile(
            Path,
            "-- generator: async-method-suffix\n-- member-location: class\n-- visibility: public\n\n"
                + "-- name: Get\nSELECT id FROM users;\n-- name: GetAsync\n-- output: sql\nSELECT 1;\n"
        );

        var run = MethodRuns.Run(file, [Entry(file, "Get", [], [Column("id")])]);

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(5,10)-(5,13): The method of the query 'Get' cannot be named 'GetAsync', "
                    + "because the query 'GetAsync' of 'App.Sample' has a constant or method of that name.  Rename the "
                    + "query.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // Get under async-method-suffix and GetAsync without it give two methods of one name: neither is generated.
    [Fact]
    public void Run_TwoMethodsOfOneName_IsSqlsrc274AtBoth()
    {
        var file = new SqlFile(
            Path,
            "-- name: Get\n-- generator: async-method-suffix\nSELECT id FROM users;\n"
                + "-- name: GetAsync\nSELECT id FROM users;\n"
        );

        var run = MethodRuns.Run(
            file,
            [Entry(file, "Get", [], [Column("id")]), Entry(file, "GetAsync", [], [Column("id")])]
        );

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(1,10)-(1,13): The method of the query 'Get' cannot be named 'GetAsync', "
                    + "because the method of the query 'GetAsync' has that name too.  Rename the query.",
                "SQLSRC274 /app/Repo/Users.sql(4,10)-(4,18): The method of the query 'GetAsync' cannot be named "
                    + "'GetAsync', because the method of the query 'Get' has that name too.  Rename the query.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // A query that gets no method takes no name: GetAsync has a problem of its own, so Get under async-method-suffix
    // keeps its method.
    [Fact]
    public void Run_QueryWithAProblemNamedAsAnotherMethod_LeavesTheOtherItsMethod()
    {
        var file = new SqlFile(
            Path,
            "-- name: GetAsync -> one\nUPDATE users SET name = 'x';\n"
                + "-- name: Get\n-- generator: async-method-suffix\nSELECT id FROM users;\n"
        );

        var run = MethodRuns.Run(file, [Entry(file, "GetAsync", [], null), Entry(file, "Get", [], [Column("id")])]);

        run.Problems()
            .ShouldBe([
                "SQLSRC272 /app/Repo/Users.sql(1,10)-(1,18): The query 'GetAsync' has the shape 'one', and its "
                    + "statement returns no rows.  Remove the shape, or write 'rowcount' or 'none'.",
            ]);
        run.Sources[MethodRuns.MethodsFile].ShouldContain(" GetAsync(\n");
        run.Sources[MethodRuns.MethodsFile].ShouldContain("/// The <c>Get</c> query from <c>Users.sql</c>.\n");
    }

    // Two types of one name in two namespaces each report the member that takes their method's name.
    [Fact]
    public void Run_TwoTypesOfOneNameClaimingOneFile_EachReportSqlsrc274()
    {
        const string Source = """
            using SqlSource;

            namespace App.A
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample
                {
                    public static int Count => 0;
                }
            }

            namespace App.B
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample
                {
                    public static int Count => 0;
                }
            }
            """;
        var file = new SqlFile(Path, OnTheType + "-- name: Count\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(file, [Entry(file, "Count", [], [Column("id")])], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(4,10)-(4,15): The method of the query 'Count' cannot be named 'Count', "
                    + "because 'App.A.Sample' has a member of that name.  Rename the query.",
                "SQLSRC274 /app/Repo/Users.sql(4,10)-(4,15): The method of the query 'Count' cannot be named 'Count', "
                    + "because 'App.B.Sample' has a member of that name.  Rename the query.",
            ]);
    }
}
