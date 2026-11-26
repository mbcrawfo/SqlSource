// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The methods of a repository's queries: instance methods of the type that get their connection from the member that
// gives it, named as a method on the type is, and the problems of a token or a name that would hide that member.  Each
// run is under postgres on Npgsql 10, at Repository.
public sealed class RepositoryMethodTests
{
    private const string MethodsFile = "App.Sample-Methods.g.cs";

    private static readonly SqlFile UsersFile = new(
        GeneratorHarness.UsersPath,
        "-- name: GetUser -> one-optional\nSELECT id FROM users WHERE id = @id;\n"
    );

    [Fact]
    public void Run_DataSourceParameter_WritesAnInstanceMethod()
    {
        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()]);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("    partial class Sample : global::App.ISample\n    {\n");
        methods.ShouldContain(
            "        public async global::System.Threading.Tasks.Task<global::App.GetUserDto?> GetUser(\n"
                + "            global::App.GetUserParams parameters,\n"
                + "            global::System.Threading.CancellationToken cancellationToken = default)\n"
        );
        methods.ShouldContain(
            "            var connection = await (dataSource ?? "
                + "throw new global::System.InvalidOperationException("
                + "\"The connection source 'dataSource' of 'App.Sample' is null.\"))"
                + ".OpenConnectionAsync(cancellationToken).ConfigureAwait(false);\n"
                + "            await using var connectionScope = "
                + "global::System.Threading.Tasks.TaskAsyncEnumerableExtensions.ConfigureAwait(connection, false);\n"
        );
        methods.ShouldNotContain("static");
        methods.ShouldNotContain("transaction");
    }

    // Each database's queries get their connection from the member marked for it.
    [Fact]
    public void Run_TwoDatabases_UsesEachMarkedSource()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(
                    [SqlSourceConnection(Database = "main")] NpgsqlDataSource main,
                    [SqlSourceConnection(Database = "reports")] string reports
                );
            }
            """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- database: main\nSELECT id FROM users;\n"
                + "-- name: GetReport\n-- database: reports\nSELECT id FROM reports;\n"
        );

        var run = RepositoryRuns.Run(
            file,
            [Entry(file, "GetUser", [], [Column("id")]), Entry(file, "GetReport", [], [Column("id")])],
            Source
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("var connection = await (main ?? ");
        methods.ShouldContain("var connection = new global::Npgsql.NpgsqlConnection(reports ?? ");
        methods
            .IndexOf("(main ?? ", System.StringComparison.Ordinal)
            .ShouldBeLessThan(methods.IndexOf(" GetReport(", System.StringComparison.Ordinal));
    }

    // Two names that differ only in case are one database, so each query finds the source that was resolved for the
    // spelling of the first.
    [Fact]
    public void Run_DatabaseSpelledInTwoCases_GivesEachQueryItsMethod()
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
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser\n-- database: Main\nSELECT id FROM users;\n"
                + "-- name: GetReport\n-- database: MAIN\nSELECT id FROM reports;\n"
        );

        var run = RepositoryRuns.Run(
            file,
            [Entry(file, "GetUser", [], [Column("id")]), Entry(file, "GetReport", [], [Column("id")])],
            Source
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain(" GetUser(\n");
        methods.ShouldContain(" GetReport(\n");
    }

    // A repository's method is on the type, so a member of the type takes its name.
    [Fact]
    public void Run_RepositoryMethodNamedLikeAMember_ReportsSqlsrc274()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource dataSource)
                {
                    public int GetUser => 0;
                }
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(1,10)-(1,17): The method of the query 'GetUser' cannot be named "
                    + "'GetUser', because 'App.Sample' has a member of that name.  Rename the query.",
            ]);

        // The file holds the type's interface alone, empty.
        run.Sources[MethodsFile].ShouldContain("    public partial interface ISample\n    {\n    }\n");
        run.Sources[MethodsFile].ShouldNotContain(" async ");
    }

    // A method of the source's name would hide the member of a base type, or the primary constructor's parameter,
    // that the methods reach their connection through.
    [Theory]
    [InlineData(
        """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public abstract class RepositoryBase
                {
                    protected NpgsqlDataSource? GetUser { get; }
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample : RepositoryBase;
            }
            """
    )]
    [InlineData(
        """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource GetUser);
            }
            """
    )]
    public void Run_RepositoryMethodNamedLikeItsSource_ReportsSqlsrc274(string source)
    {
        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], source);

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(1,10)-(1,17): The method of the query 'GetUser' cannot be named "
                    + "'GetUser', because the repository methods of 'App.Sample' get their connection from "
                    + "'GetUser'.  Rename the query.",
            ]);

        // The file holds the type's interface alone, empty.
        run.Sources[MethodsFile].ShouldContain("    public partial interface ISample\n    {\n    }\n");
        run.Sources[MethodsFile].ShouldNotContain(" async ");
    }

    // A connection member of the type's own that the method would take the name of is found as any member is, which is
    // said first of what the type has: the member is not only the source.
    [Fact]
    public void Run_RepositoryMethodNamedLikeItsOwnSource_ReportsTheMemberOfTheType()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample
                {
                    private NpgsqlDataSource? GetUser { get; }
                }
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(1,10)-(1,17): The method of the query 'GetUser' cannot be named "
                    + "'GetUser', because 'App.Sample' has a member of that name.  Rename the query.",
            ]);
    }

    // A source that the method would hide, and the interface that holds it, of one name: the source is said.
    [Fact]
    public void Run_RepositoryMethodNamedLikeItsSourceAndItsInterface_ReportsTheSource()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Users(NpgsqlDataSource IUsers);
            }
            """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: IUsers -> one-optional\nSELECT id FROM users WHERE id = @id;\n"
        );

        var run = RepositoryRuns.Run(file, [Entry(file, "IUsers", [Parameter("id")], [Column("id")])], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(1,10)-(1,16): The method of the query 'IUsers' cannot be named "
                    + "'IUsers', because the repository methods of 'App.Users' get their connection from 'IUsers'.  "
                    + "Rename the query.",
            ]);
    }

    // The visibility is the query's, and the method stays an instance method.
    [Fact]
    public void Run_ProtectedVisibility_WritesAProtectedInstanceMethod()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser -> one-optional\n-- visibility: protected\nSELECT id FROM users WHERE id = @id;\n"
        );

        var run = RepositoryRuns.Run(file, [GetUserEntry(file)]);

        run.ShouldBeClean();
        run.Sources[MethodsFile]
            .ShouldContain(
                "        protected async global::System.Threading.Tasks.Task<global::App.GetUserDto?> GetUser(\n"
            );
    }

    // The source's parameter is reached by its name, so the method's local of the connection takes another.
    [Fact]
    public void Run_SourceParameterNamedConnection_RenamesTheLocal()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource connection);
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], Source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("var connection1 = await (connection ?? ");
        methods.ShouldContain("var command = connection1.CreateCommand();\n");
    }

    // A token is an argument of the method, which would hide the parameter that the method reaches its connection
    // through.
    [Fact]
    public void Run_TokenNamedLikeTheStartOfTheSource_ReportsSqlsrc275()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource dataSource);
            }
            """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetFrom\n-- token: {{dataSource:users}}\nSELECT id FROM {{dataSource}};\n"
        );

        var run = RepositoryRuns.Run(file, [Entry(file, "GetFrom", [], [Column("id")])], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC275 /app/Repo/Users.sql(1,10)-(1,17): The token 'dataSource' of the query 'GetFrom' has the "
                    + "name of the primary-constructor parameter 'dataSource', which gives the repository methods of "
                    + "'App.Sample' their connection.  Rename the token.",
            ]);

        // The file holds the type's interface alone, empty.
        run.Sources[MethodsFile].ShouldContain("    public partial interface ISample\n    {\n    }\n");
        run.Sources[MethodsFile].ShouldNotContain(" async ");
    }

    // A repository nested in a generic type reaches a static source through its own name, which a token of that name
    // would hide when the repository is not generic.
    [Fact]
    public void Run_TokenNamedLikeATypeNestedInAGenericType_ReportsSqlsrc275()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                public partial class A<T>
                {
                    public partial class B<T>
                    {
                        [SqlSourceGenerate(Source = ".", GenerateInterface = false)]
                        public partial class Sample
                        {
                            private static readonly string ConnectionString = "";
                        }
                    }
                }
            }
            """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetFrom\n-- token: {{Sample:users}}\nSELECT id FROM {{Sample}};\n"
        );

        var run = RepositoryRuns.Run(file, [Entry(file, "GetFrom", [], [Column("id")])], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC275 /app/Repo/Users.sql(1,10)-(1,17): The token 'Sample' of the query 'GetFrom' has the name "
                    + "of the type 'Sample', through which the repository methods of 'App.A<T>.B<T>.Sample' reach "
                    + "their connection 'ConnectionString'.  Rename the token.",
            ]);
    }

    // SqlSource can write a nested class with the name of an outer type parameter, which the access must not write.
    [Fact]
    public void Run_GeneratedNestedClassNamedAsAnOuterTypeParameter_Compiles()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                public partial class Outer<T>
                {
                    [SqlSourceGenerate(Source = ".", GenerateInterface = false)]
                    public partial class Sample
                    {
                        private static readonly string ConnectionString = "";
                    }
                }
            }
            """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetFrom\nSELECT id FROM users;\n-- name: Plain\n-- output: sql\n-- nested-class: T\nSELECT 1;\n"
        );

        var run = RepositoryRuns.Run(file, [Entry(file, "GetFrom", [], [Column("id")])], Source);

        run.ShouldBeClean();
        run.Sources["App.Outer-1.Sample-Methods.g.cs"].ShouldContain("(Sample.ConnectionString ?? ");
    }

    // A token named like the type hides nothing of a static source, which is reached from global::.
    [Theory]
    [InlineData("Sample", "App.Sample-Methods.g.cs", "global::App.Sample.ConnectionString ?? ")]
    [InlineData("Sample<T>", "App.Sample-1-Methods.g.cs", "global::App.Sample<T>.ConnectionString ?? ")]
    public void Run_TokenNamedLikeTheTypeOfAStaticSource_ReportsNothing(string type, string hintName, string access)
    {
        var source =
            "using SqlSource;\n\nnamespace App\n{\n    [SqlSourceGenerate(Source = \".\")]\n    public partial class "
            + type
            + "\n    {\n        private static readonly string ConnectionString = \"\";\n    }\n}\n";
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetFrom\n-- token: {{Sample:users}}\nSELECT id FROM {{Sample}};\n"
        );

        var run = RepositoryRuns.Run(file, [Entry(file, "GetFrom", [], [Column("id")])], source);

        run.ShouldBeClean();
        run.Sources[hintName].ShouldContain(access);
    }

    // The entry of GetUser, of the file given or UsersFile: the parameter id and the column id.
    private static SidecarEntry GetUserEntry(SqlFile? file = null) =>
        Entry(file ?? UsersFile, "GetUser", [Parameter("id")], [Column("id")]);
}
