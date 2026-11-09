// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using Shouldly;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The problems of a repository's connection: a type whose repository queries have no member to give it one
// (SQLSRC028), a static type, which has no instance to call them on (SQLSRC034), and the types that have none.  Each
// run is under postgres on Npgsql 10, at Repository but for the type whose queries are all methods.
public sealed class RepositoryDiagnosticsTests
{
    private const string MethodsFile = "App.Sample-Methods.g.cs";

    private static readonly SqlFile UsersFile = new(
        GeneratorHarness.UsersPath,
        "-- name: GetUser\nSELECT id FROM users;\n"
    );

    [Fact]
    public void Run_RepositoryWithoutAConnection_ReportsSqlsrc028AtTheAttribute()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample;
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC028 /app/Repo/Sample.cs(5,6)-(5,37): 'App.Sample' has no connection for the database "
                    + "'postgres', which its repository queries use.  Add a string field, property or "
                    + "primary-constructor parameter named connectionString, or one of a DbDataSource type, or "
                    + "mark the member that gives the connection [SqlSourceConnection].  Or set the output of its "
                    + "repository queries to methods.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodsFile);
    }

    [Fact]
    public void Run_StaticRepository_ReportsSqlsrc034()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public static partial class Sample;
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC034 /app/Repo/Sample.cs(5,6)-(5,37): 'App.Sample' is static, and the method of a repository "
                    + "query is an instance method.  Set the output of its repository queries to methods, or remove "
                    + "static.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodsFile);
    }

    // The method's argument would hide the parameter, so the parameter cannot give the connection.
    [Fact]
    public void Run_ConnectionParameterNamedAsAnArgument_ReportsSqlsrc036()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource cancellationToken);
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC036 /app/Repo/Sample.cs(7,50)-(7,67): 'cancellationToken' of 'App.Sample' is the "
                    + "primary-constructor parameter that gives the database 'postgres' its connection, and the "
                    + "repository methods have an argument named 'cancellationToken', which hides it.  Rename the "
                    + "parameter, or store it in a field or property and give the connection from that.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodsFile);
    }

    // The members come before the primary constructor's parameters, so the parameter that the type stores in a field
    // is not captured a second time, which the compiler would warn of.
    [Fact]
    public void Run_ParameterStoredInAField_UsesTheFieldAndCompilesWithoutWarnings()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource dataSource)
                {
                    private readonly NpgsqlDataSource _dataSource = dataSource;
                }
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.ShouldBeClean();
        run.Sources[MethodsFile].ShouldContain("var connection = await (this._dataSource ?? ");
    }

    // A query's database is the one its settings name, so a member marked for the settings file's database gives its
    // connection.
    [Fact]
    public void Run_MarkedForTheDatabaseOfTheSettingsFile_ReportsNothing()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample([SqlSourceConnection(Database = "main")] NpgsqlDataSource dataSource);
            }
            """;

        var run = RepositoryRuns.Run(
            UsersFile,
            [Entry(UsersFile, "GetUser", [], [Column("id")])],
            Source,
            settings: /*lang=json,strict*/
            """{ "database": "main" }"""
        );

        run.ShouldBeClean();
    }

    // A type whose queries are all of another output needs no connection, and a member it marks is not checked.
    [Fact]
    public void Run_MarkedMemberOfAMethodsType_ReportsNothing()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample
                {
                    [SqlSourceConnection]
                    private int Connection => 0;
                }
            }
            """;

        var run = MethodRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.ShouldBeClean();
    }
}
