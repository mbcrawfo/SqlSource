// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using Shouldly;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The problems of one type's methods: a shape that needs rows on a statement that has none (SQLSRC272), a token named
// as a fixed argument (SQLSRC275), and a method's name that something else takes (SQLSRC274).  A query with one gets
// no method, and the rest of the project still compiles with no warning in what is generated.  Each run is at CodeGen
// under postgres on Npgsql 10.
public sealed class MethodDiagnosticsTests
{
    private const string Path = GeneratorHarness.UsersPath;

    [Theory]
    [InlineData("one")]
    [InlineData("one-optional")]
    [InlineData("many")]
    public void Run_ShapeOfRowsOnAStatementWithoutRows_IsSqlsrc272(string shape)
    {
        var file = new SqlFile(Path, "-- name: SetName -> " + shape + "\nUPDATE users SET name = 'x';\n");

        var run = MethodRuns.Run(file, [Entry(file, "SetName", [], null)]);

        Problems(run)
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

    [Fact]
    public void Run_TokenNamedAsAFixedArgument_IsSqlsrc275()
    {
        var file = new SqlFile(
            Path,
            "-- name: List\n-- token: {{transaction:users}}\nSELECT id FROM {{transaction}};\n"
        );

        var run = MethodRuns.Run(file, [Entry(file, "List", [], [Column("id")])]);

        Problems(run)
            .ShouldBe([
                "SQLSRC275 /app/Repo/Users.sql(1,10)-(1,14): The token 'transaction' of the query 'List' has the "
                    + "name of an argument that every query method has: connection, parameters, transaction, "
                    + "commandTimeout or cancellationToken.  Rename the token.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // An instance member of the connection is found before an extension method of its name.
    [Theory]
    [InlineData("Open")]
    [InlineData("ToString")]
    public void Run_QueryNamedAsAMemberOfTheConnection_IsSqlsrc274(string name)
    {
        var file = new SqlFile(Path, "-- name: " + name + "\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(file, [Entry(file, name, [], [Column("id")])]);

        Problems(run)
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

        Problems(run)
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(1,10)-(1,26): The method of the query 'SampleExtensions' cannot be "
                    + "named 'SampleExtensions', because the class that holds the method has that name.  Rename the "
                    + "query.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    [Fact]
    public void Run_QueryNamedAsAMemberOfTheTypeUnderPublic_IsSqlsrc274()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(MethodLocation = MethodLocation.Public)]
                public partial class Sample
                {
                    public static int Count => 0;
                }
            }
            """;
        var file = new SqlFile(Path, "-- name: Count\nSELECT id FROM users;\n");

        var run = MethodRuns.Run(file, [Entry(file, "Count", [], [Column("id")])], Source);

        Problems(run)
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(1,10)-(1,15): The method of the query 'Count' cannot be named 'Count', "
                    + "because 'Sample' has a member of that name.  Rename the query.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // Under SqlLocation.Direct the constant of a query whose output is sql is a member of the type, and the method of
    // Get under async-method-suffix would take its name.
    [Fact]
    public void Run_MethodNamedAsTheConstantOfAnotherQueryUnderDirect_IsSqlsrc274()
    {
        var source = MethodRuns.SampleSource(
            "SqlLocation = SqlLocation.Direct, MethodLocation = MethodLocation.Public, "
                + "Parameters = \"async-method-suffix\""
        );
        var file = new SqlFile(
            Path,
            "-- name: Get\nSELECT id FROM users;\n-- name: GetAsync\n-- output: sql\nSELECT 1;\n"
        );

        var run = MethodRuns.Run(file, [Entry(file, "Get", [], [Column("id")])], source);

        Problems(run)
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(1,10)-(1,13): The method of the query 'Get' cannot be named 'GetAsync', "
                    + "because the query 'GetAsync' of 'Sample' has a constant or method of that name.  Rename the "
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

        Problems(run)
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(1,10)-(1,13): The method of the query 'Get' cannot be named 'GetAsync', "
                    + "because the method of the query 'GetAsync' has that name too.  Rename the query.",
                "SQLSRC274 /app/Repo/Users.sql(4,10)-(4,18): The method of the query 'GetAsync' cannot be named "
                    + "'GetAsync', because the method of the query 'Get' has that name too.  Rename the query.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
    }

    // The diagnostics of a run whose generated code compiles with no error and no warning.
    private static IReadOnlyList<string> Problems(GeneratorRun run)
    {
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        return run.Diagnostics;
    }
}
