// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The methods of a repository whose connection comes from a marked member of a factory it holds, reached through the
// holder, which a method checks for null before it reads the member, or from a marked static of the project, reached
// through its type in full.  Each run is under postgres on Npgsql 10, at Repository.
public sealed class SharedConnectionMethodTests
{
    private const string MethodsFile = "App.Sample-Methods.g.cs";

    private const string HolderIsNull =
        "throw new global::System.InvalidOperationException("
        + "\"The connection source 'dbf' of 'App.Sample' is null.\")";

    // The factory the repositories hold, whose marked method opens a connection.
    private const string Factory = """
        using System.Threading;
        using System.Threading.Tasks;
        using Npgsql;
        using SqlSource;

        namespace App
        {
            public interface IDatabaseFactory
            {
                [SqlSourceConnection]
                Task<NpgsqlConnection> Open(CancellationToken ct);
            }

        """;

    private static readonly SqlFile UsersFile = new(
        GeneratorHarness.UsersPath,
        "-- name: GetUser -> one-optional\nSELECT id FROM users WHERE id = @id;\n"
    );

    [Fact]
    public void Run_HeldAsyncFactory_CallsThroughTheHolder()
    {
        const string Source =
            Factory
            + """
                    [SqlSourceGenerate(Source = ".")]
                    public partial class Sample(IDatabaseFactory dbf);
                }
                """;

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], Source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("(dbf ?? " + HolderIsNull + ").Open(cancellationToken)");
        methods.ShouldContain("\"The connection source 'dbf.Open' of 'App.Sample' is null.\"");
    }

    [Fact]
    public void Run_HeldDataSourceProperty_OpensThroughTheHolder()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public class DatabaseFactory
                {
                    [SqlSourceConnection]
                    public NpgsqlDataSource? DataSource { get; set; }
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample
                {
                    private readonly DatabaseFactory _dbf = new();
                }
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], Source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain(
            "await ((this._dbf ?? throw new global::System.InvalidOperationException("
                + "\"The connection source '_dbf' of 'App.Sample' is null.\")).DataSource ?? throw "
                + "new global::System.InvalidOperationException("
                + "\"The connection source '_dbf.DataSource' of 'App.Sample' is null.\"))"
                + ".OpenConnectionAsync(cancellationToken)"
        );
    }

    [Fact]
    public void Run_GlobalConnectionString_UsesTheFullName()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                public static class Db
                {
                    [SqlSourceConnection]
                    public static string? ConnectionString { get; set; }
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample;
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], Source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("new global::Npgsql.NpgsqlConnection(global::App.Db.ConnectionString ?? throw ");
        methods.ShouldContain("\"The connection source 'Db.ConnectionString' of 'App.Sample' is null.\"");
    }

    [Fact]
    public void Run_GlobalInNestedType_UsesTheFullName()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                public static class Outer
                {
                    public static class Db
                    {
                        [SqlSourceConnection]
                        public static string ConnectionString = "";
                    }
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample;
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], Source);

        run.ShouldBeClean();
        run.Sources[MethodsFile]
            .ShouldContain("new global::Npgsql.NpgsqlConnection(global::App.Outer.Db.ConnectionString ?? throw ");
    }

    // A type of the global namespace is reached from global::, and a message names it without that.
    [Fact]
    public void Run_GlobalInTheGlobalNamespace_NamesTheTypeAlone()
    {
        const string Source = """
            using SqlSource;

            public static class Db
            {
                [SqlSourceConnection]
                public static string? ConnectionString { get; set; }
            }

            namespace App
            {
                [SqlSourceGenerate(Source = ".")]
                public partial class Sample;
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], Source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("new global::Npgsql.NpgsqlConnection(global::Db.ConnectionString ?? throw ");
        methods.ShouldContain("\"The connection source 'Db.ConnectionString' of 'App.Sample' is null.\"");
    }

    // A struct's local function reaches no primary-constructor parameter, so its streaming method copies the holder,
    // checked, into a local before the iterator, which opens the connection through that local.
    [Fact]
    public void Run_StructStreamingThroughHolder_ChecksTheHolderBeforeTheIterator()
    {
        const string Source =
            Factory
            + """
                    [SqlSourceGenerate(Source = ".")]
                    public partial struct Sample(IDatabaseFactory dbf);
                }
                """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: StreamUsers -> many\n-- collection-type: IAsyncEnumerable\nSELECT id FROM users;\n"
        );

        var run = RepositoryRuns.Run(file, [Entry(file, "StreamUsers", [], [Column("id")])], Source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        var copy = methods.IndexOf("var source = dbf ?? " + HolderIsNull + ";\n", StringComparison.Ordinal);
        var iterator = methods.IndexOf(" ReadRows(", StringComparison.Ordinal);
        var open = methods.IndexOf(
            "var connection = await source.Open(cancellationToken).ConfigureAwait(false) ?? throw ",
            StringComparison.Ordinal
        );
        copy.ShouldBeGreaterThan(0);
        copy.ShouldBeLessThan(iterator);
        open.ShouldBeGreaterThan(iterator);
    }

    // A token of the holder's name would hide it, as it would hide a parameter that gives the connection.
    [Fact]
    public void Run_TokenNamedLikeTheHolder_ReportsSqlsrc275()
    {
        const string Source =
            Factory
            + """
                    [SqlSourceGenerate(Source = ".")]
                    public partial class Sample(IDatabaseFactory dbf);
                }
                """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: GetFrom\n-- token: {{dbf:users}}\nSELECT id FROM {{dbf}};\n"
        );

        var run = RepositoryRuns.Run(file, [Entry(file, "GetFrom", [], [Column("id")])], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC275 /app/Repo/Users.sql(1,10)-(1,17): The token 'dbf' of the query 'GetFrom' has the name of "
                    + "the primary-constructor parameter 'dbf', through which the repository methods of 'App.Sample' "
                    + "reach their connection 'dbf.Open'.  Rename the token.",
            ]);
    }

    // A static holder of a base type may have the repository's name, through which the method reaches it: the token
    // hides the type, not a parameter.
    [Fact]
    public void Run_TokenNamedLikeAStaticHolderOfTheTypesName_ReportsSqlsrc275ForTheType()
    {
        const string Source =
            Factory
            + """
                    public abstract class RepositoryBase
                    {
                        protected static IDatabaseFactory? Sample { get; set; }
                    }

                    [SqlSourceGenerate(Source = ".")]
                    public partial class Sample : RepositoryBase;
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
                    + "of the type 'Sample', through which the repository methods of 'App.Sample' reach their "
                    + "connection 'Sample.Open'.  Rename the token.",
            ]);
    }

    // Under method-type=both, each method of a held pair calls its own member through the holder: the asynchronous
    // method awaits the asynchronous one with its token, and the synchronous method calls the synchronous one.
    [Fact]
    public void Run_MethodTypeBothThroughAHeldPair_GivesEachMethodItsOwnMember()
    {
        const string Source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    Task<NpgsqlConnection> OpenAsync(CancellationToken ct);

                    [SqlSourceConnection]
                    NpgsqlConnection Open();
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(IDatabaseFactory dbf);
            }
            """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- generator: method-type=both\n-- name: GetUser -> one-optional\nSELECT id FROM users WHERE id = @id;\n"
        );

        var run = RepositoryRuns.Run(file, [Entry(file, "GetUser", [Parameter("id")], [Column("id")])], Source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        var synchronous = methods.IndexOf(" GetUser(\n", StringComparison.Ordinal);
        var asynchronous = methods.IndexOf(" GetUserAsync(\n", StringComparison.Ordinal);
        synchronous.ShouldBeGreaterThan(0);
        asynchronous.ShouldBeGreaterThan(synchronous);
        var synchronousBody = methods[synchronous..asynchronous];
        var asynchronousBody = methods[asynchronous..];
        synchronousBody.ShouldContain("var connection = (dbf ?? " + HolderIsNull + ").Open() ?? throw ");
        synchronousBody.ShouldNotContain("OpenAsync");
        asynchronousBody.ShouldContain(
            "var connection = await (dbf ?? "
                + HolderIsNull
                + ").OpenAsync(cancellationToken).ConfigureAwait(false) ?? throw "
        );
        asynchronousBody.ShouldNotContain(").Open()");
    }

    // A method of the holder's name would hide it, as it would hide a parameter that gives the connection.
    [Fact]
    public void Run_RepositoryMethodNamedLikeTheHolder_ReportsSqlsrc274()
    {
        const string Source =
            Factory
            + """
                    [SqlSourceGenerate(Source = ".")]
                    public partial class Sample(IDatabaseFactory dbf);
                }
                """;
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: dbf\nSELECT id FROM users;\n");

        var run = RepositoryRuns.Run(file, [Entry(file, "dbf", [], [Column("id")])], Source);

        run.Problems()
            .ShouldBe([
                "SQLSRC274 /app/Repo/Users.sql(1,10)-(1,13): The method of the query 'dbf' cannot be named 'dbf', "
                    + "because the repository methods of 'App.Sample' get their connection through 'dbf'.  Rename "
                    + "the query.",
            ]);
    }

    // The entry of GetUser: the parameter id and the column id.
    private static SidecarEntry GetUserEntry() => Entry(UsersFile, "GetUser", [Parameter("id")], [Column("id")]);
}
