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
        methods.ShouldContain("    partial class Sample\n    {\n");
        methods.ShouldContain(
            "        public async global::System.Threading.Tasks.Task<global::App.GetUserDto?> GetUser(\n"
                + "            global::App.GetUserParams parameters,\n"
                + "            global::System.Threading.CancellationToken cancellationToken = default)\n"
        );
        methods.ShouldContain(
            "            await using var connection = await (dataSource ?? "
                + "throw new global::System.InvalidOperationException("
                + "\"The connection source 'dataSource' of 'App.Sample' is null.\"))"
                + ".OpenConnectionAsync(cancellationToken).ConfigureAwait(false);\n"
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
        methods.ShouldContain("await using var connection = await (main ?? ");
        methods.ShouldContain("await using var connection = new global::Npgsql.NpgsqlConnection(reports ?? ");
        methods
            .IndexOf("(main ?? ", System.StringComparison.Ordinal)
            .ShouldBeLessThan(methods.IndexOf(" GetReport(", System.StringComparison.Ordinal));
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
        run.Sources.Keys.ShouldNotContain(MethodsFile);
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
        run.Sources.Keys.ShouldNotContain(MethodsFile);
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
        methods.ShouldContain("await using var connection1 = await (connection ?? ");
        methods.ShouldContain("var command = connection1.CreateCommand();\n");
    }

    // A token is an argument of the method, which would hide the parameter, or the type of the static member, that the
    // method reaches its connection through.
    [Theory]
    [InlineData(
        "public partial class Sample(NpgsqlDataSource dataSource);",
        "dataSource",
        "the primary-constructor parameter 'dataSource', which gives the repository methods of 'App.Sample' their "
            + "connection"
    )]
    [InlineData(
        "public partial class Sample { private static readonly string ConnectionString = \"\"; }",
        "Sample",
        "the type 'Sample', through which the repository methods of 'App.Sample' reach their connection "
            + "'ConnectionString'"
    )]
    public void Run_TokenNamedLikeTheStartOfTheSource_ReportsSqlsrc275(string type, string token, string reason)
    {
        var source =
            "using Npgsql;\nusing SqlSource;\n\nnamespace App\n{\n    [SqlSourceGenerate(Source = \".\")]\n    "
            + type
            + "\n}\n";
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetFrom\n-- token: {{" + token + ":users}}\nSELECT id FROM {{" + token + "}};\n"
        );

        var run = RepositoryRuns.Run(file, [Entry(file, "GetFrom", [], [Column("id")])], source);

        run.Problems()
            .ShouldBe([
                "SQLSRC275 /app/Repo/Users.sql(1,10)-(1,17): The token '"
                    + token
                    + "' of the query 'GetFrom' has the name of "
                    + reason
                    + ".  Rename the token.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodsFile);
    }

    // The entry of GetUser, of the file given or UsersFile: the parameter id and the column id.
    private static SidecarEntry GetUserEntry(SqlFile? file = null) =>
        Entry(file ?? UsersFile, "GetUser", [Parameter("id")], [Column("id")]);
}
