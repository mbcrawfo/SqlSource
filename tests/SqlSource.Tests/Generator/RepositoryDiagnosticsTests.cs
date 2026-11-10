// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

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

        // The file holds the type's interface alone, empty.
        run.Sources[MethodsFile].ShouldContain("    public partial interface ISample\n    {\n    }\n");
        run.Sources[MethodsFile].ShouldNotContain(" async ");
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

        // A static class cannot implement the interface, so it gets none, and no file.
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

        // The file holds the type's interface alone, empty.
        run.Sources[MethodsFile].ShouldContain("    public partial interface ISample\n    {\n    }\n");
        run.Sources[MethodsFile].ShouldNotContain(" async ");
    }

    private static readonly SqlFile ParameterizedFile = new(
        GeneratorHarness.UsersPath,
        "-- name: GetUser -> one-optional\nSELECT id FROM users WHERE id = @id;\n"
    );

    private const string ParametersSource = """
        using Npgsql;
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample(NpgsqlDataSource parameters);
        }
        """;

    // A query with parameters has an input model, whose argument is named parameters, which hides the parameter.
    [Fact]
    public void Run_ConnectionParameterNamedParametersWithAQueryThatHasParameters_ReportsSqlsrc036()
    {
        var run = RepositoryRuns.Run(
            ParameterizedFile,
            [Entry(ParameterizedFile, "GetUser", [Parameter("id")], [Column("id")])],
            ParametersSource
        );

        run.Problems()
            .ShouldBe([
                "SQLSRC036 /app/Repo/Sample.cs(7,50)-(7,60): 'parameters' of 'App.Sample' is the "
                    + "primary-constructor parameter that gives the database 'postgres' its connection, and the "
                    + "repository methods have an argument named 'parameters', which hides it.  Rename the "
                    + "parameter, or store it in a field or property and give the connection from that.",
            ]);
    }

    // The database takes parameters when any of its queries does, not only its first.
    [Fact]
    public void Run_ConnectionParameterNamedParametersWithALaterQueryThatHasParameters_ReportsSqlsrc036()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: CountUsers\nSELECT id FROM users;\n\n"
                + "-- name: GetUser -> one-optional\nSELECT id FROM users WHERE id = @id;\n"
        );

        var run = RepositoryRuns.Run(
            file,
            [Entry(file, "CountUsers", [], [Column("id")]), Entry(file, "GetUser", [Parameter("id")], [Column("id")])],
            ParametersSource
        );

        run.Problems().ShouldHaveSingleItem().ShouldStartWith("SQLSRC036 ");
    }

    // No method takes an input model, so no argument is named parameters and the parameter is the connection.
    [Fact]
    public void Run_ConnectionParameterNamedParametersWithOnlyParameterlessQueries_IsTheSource()
    {
        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], ParametersSource);

        run.ShouldBeClean();
        run.Sources[MethodsFile].ShouldContain("var connection = await (parameters ?? ");
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

    // A method that captured a parameter that the type also passes to its base type (CS9107), or uses to initialize a
    // field that is no source (CS9124), would make the compiler warn in the user's code, so the parameter is not
    // chosen, and the type has no connection.
    [Theory]
    [InlineData(
        """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public abstract class RepositoryBase
                {
                    protected RepositoryBase(NpgsqlDataSource source) { }
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource dataSource) : RepositoryBase(dataSource);
            }
            """,
        "(11,6)-(11,37)",
        "dataSource"
    )]
    [InlineData(
        """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(string connectionString)
                {
                    private readonly string _value = connectionString;

                    public override string ToString() => _value;
                }
            }
            """,
        "(5,6)-(5,37)",
        "connectionString"
    )]
    public async Task Run_ParameterPassedOnOrStored_ReportsSqlsrc028AndCompilesWithoutCapture(
        string source,
        string attribute,
        string parameter
    )
    {
        SqlFile[] files = [UsersFile, Sidecars.Of(UsersFile, Entry(UsersFile, "GetUser", [], [Column("id")]))];

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], source);
        var build = await GeneratorHarness.BuildAsync(
            OutputKind.Repository,
            source,
            files,
            [NpgsqlReferences.Npgsql10],
            dialect: "postgres"
        );

        run.Problems()
            .ShouldBe([
                "SQLSRC028 /app/Repo/Sample.cs"
                    + attribute
                    + ": 'App.Sample' has no connection for the database 'postgres', which its repository queries "
                    + "use.  The primary-constructor parameter '"
                    + parameter
                    + "' is not chosen by its name or type, since the type also uses it to initialize a field or "
                    + "property, or passes it to its base type: keep the value in a field or property named "
                    + "connectionString or of a DbDataSource type, or mark the parameter [SqlSourceConnection].  Or "
                    + "set the output of its repository queries to methods.",
            ]);
        run.Sources[MethodsFile].ShouldNotContain(" async ");
        build.Where(IsCaptureWarning).ShouldBeEmpty();
    }

    // A marked parameter is the user's choice, so its methods capture it though the type passes it on, and the
    // compiler's warning of that is the user's to see.
    [Fact]
    public async Task Run_MarkedParameterPassedOn_UsesTheParameter()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public abstract class RepositoryBase
                {
                    protected RepositoryBase(NpgsqlDataSource source) { }
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample([SqlSourceConnection] NpgsqlDataSource dataSource)
                    : RepositoryBase(dataSource);
            }
            """;
        SqlFile[] files = [UsersFile, Sidecars.Of(UsersFile, Entry(UsersFile, "GetUser", [], [Column("id")]))];

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);
        var build = await GeneratorHarness.BuildAsync(
            OutputKind.Repository,
            Source,
            files,
            [NpgsqlReferences.Npgsql10],
            dialect: "postgres"
        );

        run.ShouldBeClean();
        run.Sources[MethodsFile].ShouldContain("var connection = await (dataSource ?? ");
        build.Where(IsCaptureWarning).ShouldHaveSingleItem().ShouldStartWith("CS9107 /app/Repo/Sample.cs(13,");
    }

    // A repository nested in a public Outer, in a Middle of the accessibility when it is not empty, whose primary
    // constructor takes the data source of its methods.
    private static string NestedRepository(string middle, string accessibility) =>
        "using Npgsql;\nusing SqlSource;\n\nnamespace App\n{\n    public partial class Outer\n    {\n"
        + (middle.Length > 0 ? "        " + middle + " partial class Middle\n        {\n" : string.Empty)
        + "        [SqlSourceGenerate(Source = \".\")]\n        "
        + accessibility
        + " partial class Sample(NpgsqlDataSource dataSource);\n"
        + (middle.Length > 0 ? "        }\n" : string.Empty)
        + "    }\n}\n";

    // The entry of GetUser, which takes an id and reads it, written for the file's hash.
    private static SidecarEntry UserEntry(SqlFile file) => Entry(file, "GetUser", [Parameter("id")], [Column("id")]);

    // The compiler's warning of a parameter that the type captures and also passes to its base type, or stores.
    private static bool IsCaptureWarning(string diagnostic) =>
        diagnostic.StartsWith("CS9107 ", System.StringComparison.Ordinal)
        || diagnostic.StartsWith("CS9124 ", System.StringComparison.Ordinal);

    // The default visibility of a repository method is public, which a derived type of another assembly reaches on a
    // type nested as protected or protected internal, or in one, so the method could not take the type's internal
    // models (CS0051).  The query that writes no visibility is refused at its name, or at its member-location marker,
    // its method alone left out.
    [Theory]
    [InlineData("", "protected", "", "App.Outer.Sample", "(1,10)-(1,17)")]
    [InlineData("", "protected internal", "", "App.Outer.Sample", "(1,10)-(1,17)")]
    [InlineData("protected", "public", "", "App.Outer.Middle.Sample", "(1,10)-(1,17)")]
    [InlineData("", "protected", "-- member-location: class\n", "App.Outer.Sample", "(2,1)-(2,26)")]
    public void Run_DefaultVisibilityReachableAsProtectedWithModels_ReportsSqlsrc123(
        string middle,
        string accessibility,
        string marker,
        string type,
        string span
    )
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser -> one-optional\n" + marker + "SELECT id FROM users WHERE id = @id;\n"
        );

        var run = RepositoryRuns.Run(file, [UserEntry(file)], NestedRepository(middle, accessibility));

        run.Problems()
            .ShouldBe([
                $"SQLSRC123 /app/Repo/Users.sql{span}: The query 'GetUser' of '{type}' cannot be public, the default "
                    + $"of the repository output, at the member location class, because '{type}' is reachable as "
                    + "protected, so its models are internal, which a method reachable from another assembly cannot "
                    + "take or return.  Write -- visibility: on the query.",
            ]);
        run.Sources.Values.ShouldAllBe(source => !source.Contains(" async ", System.StringComparison.Ordinal));
    }

    // A public type of the namespace makes its models public, which its public method takes.
    [Fact]
    public void Run_DefaultVisibilityOfAPublicTypeWithModels_WritesThePublicMethod()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetUser -> one-optional\nSELECT id FROM users WHERE id = @id;\n"
        );

        var run = RepositoryRuns.Run(file, [UserEntry(file)]);

        run.ShouldBeClean();
        run.Sources[MethodsFile]
            .ShouldContain(" public async global::System.Threading.Tasks.Task<global::App.GetUserDto?> ");
    }

    // A method that takes and returns no model fits the default on a type reachable as protected, and so does an
    // internal one with models.
    [Theory]
    [InlineData("", "DELETE FROM users;", false, "public async global::System.Threading.Tasks.Task<int> GetUser(")]
    [InlineData(
        "-- visibility: internal\n",
        "SELECT id FROM users WHERE id = @id;",
        true,
        "internal async global::System.Threading.Tasks.Task<global::App.GetUserDto[]> GetUser("
    )]
    public void Run_ReachableAsProtectedWithAMethodThatFits_WritesIt(
        string marker,
        string sql,
        bool hasModels,
        string signature
    )
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\n" + marker + sql + "\n");
        var entry = hasModels ? UserEntry(file) : Entry(file, "GetUser", [], null);

        var run = RepositoryRuns.Run(file, [entry], NestedRepository("", "protected"));

        run.ShouldBeClean();
        run.Sources["App.Outer.Sample-Methods.g.cs"].ShouldContain(signature);
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
