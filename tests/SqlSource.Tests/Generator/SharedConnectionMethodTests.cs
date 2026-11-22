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

    // The factory and its struct, which implement the marked member explicitly, and the attribute of the repository
    // that a test declares after them.
    private const string ExplicitFactory = """
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

            public class DatabaseFactory : IDatabaseFactory
            {
                Task<NpgsqlConnection> IDatabaseFactory.Open(CancellationToken ct) => null!;
            }

            public struct StructFactory : IDatabaseFactory
            {
                readonly Task<NpgsqlConnection> IDatabaseFactory.Open(CancellationToken ct) => null!;
            }

            [SqlSourceGenerate(Source = ".")]

        """;

    private const string CastOpen = ")).Open(cancellationToken).ConfigureAwait(false) ?? throw ";

    // A member that the holder's class implements explicitly is called through a cast of the holder, checked for null
    // first, to the interface, from a holder of each kind: a parameter, a Nullable<T> of a struct, a static field and a
    // property; a struct's holder, which cannot be null, is cast unchecked.  The member's message names it through the
    // holder.
    [Theory]
    [InlineData(
        "public partial class Sample(DatabaseFactory dbf);",
        "await ((global::App.IDatabaseFactory)(dbf ?? " + HolderIsNull + CastOpen
    )]
    [InlineData(
        "public partial class Sample(StructFactory? dbf);",
        "await ((global::App.IDatabaseFactory)(dbf ?? " + HolderIsNull + CastOpen
    )]
    [InlineData(
        "public partial class Sample { private static readonly DatabaseFactory dbf = new(); }",
        "await ((global::App.IDatabaseFactory)(global::App.Sample.dbf ?? " + HolderIsNull + CastOpen
    )]
    [InlineData(
        "public partial class Sample { private DatabaseFactory dbf { get; } = new(); }",
        "await ((global::App.IDatabaseFactory)(this.dbf ?? " + HolderIsNull + CastOpen
    )]
    [InlineData(
        "public partial class Sample(StructFactory dbf);",
        "await ((global::App.IDatabaseFactory)dbf).Open(cancellationToken).ConfigureAwait(false) ?? throw "
    )]
    public void Run_HeldExplicitImplementation_CastsTheHolderToTheInterface(string repository, string open)
    {
        var source = ExplicitFactory + "    " + repository + "\n}\n";

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain("var connection = " + open);
        methods.ShouldContain("\"The connection source 'dbf.Open' of 'App.Sample' is null.\"");
    }

    // A data source that a factory implements explicitly is read through the cast, then opened, and a synchronous
    // factory of a generic interface is called through the interface closed as the class implements it, with its ?.
    [Theory]
    [InlineData(
        """
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    NpgsqlDataSource? DataSource { get; }
                }

                public sealed class DatabaseFactory : IDatabaseFactory
                {
                    NpgsqlDataSource? IDatabaseFactory.DataSource => null;
                }
            """,
        "await (((global::App.IDatabaseFactory)(dbf ?? "
            + HolderIsNull
            + ")).DataSource ?? throw new global::System.InvalidOperationException("
            + "\"The connection source 'dbf.DataSource' of 'App.Sample' is null.\"))"
            + ".OpenConnectionAsync(cancellationToken)"
    )]
    [InlineData(
        """
                public interface IFactory<TConnection>
                {
                    [SqlSourceConnection]
                    TConnection Open();
                }

                public sealed class DatabaseFactory : IFactory<NpgsqlConnection?>
                {
                    NpgsqlConnection? IFactory<NpgsqlConnection?>.Open() => null;
                }
            """,
        "((global::App.IFactory<global::Npgsql.NpgsqlConnection?>)(dbf ?? " + HolderIsNull + ")).Open() ?? throw "
    )]
    public void Run_HeldExplicitImplementationOfAnotherKind_CastsTheHolder(string factory, string open)
    {
        var source =
            "#nullable enable\nusing Npgsql;\nusing SqlSource;\n\nnamespace App\n{\n"
            + factory
            + "\n    [SqlSourceGenerate(Source = \".\")]\n    public partial class Sample(DatabaseFactory dbf);\n}\n";

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], source);

        run.ShouldBeClean();
        run.Sources[MethodsFile].ShouldContain("var connection = " + open);
    }

    // A struct's streaming method copies the holder, checked when it can be null, before its iterator, which casts the
    // copy to the interface.
    [Theory]
    [InlineData("DatabaseFactory", "var source = dbf ?? " + HolderIsNull + ";\n")]
    [InlineData("StructFactory", "var source = dbf;\n")]
    public void Run_StructStreamingThroughAnExplicitImplementation_CastsTheCopy(string holderType, string copy)
    {
        var source = ExplicitFactory + "    public partial struct Sample(" + holderType + " dbf);\n}\n";
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: StreamUsers -> many\n-- collection-type: IAsyncEnumerable\nSELECT id FROM users;\n"
        );

        var run = RepositoryRuns.Run(file, [Entry(file, "StreamUsers", [], [Column("id")])], source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        var copied = methods.IndexOf(copy, StringComparison.Ordinal);
        var iterator = methods.IndexOf(" ReadRows(", StringComparison.Ordinal);
        var open = methods.IndexOf(
            "var connection = await ((global::App.IDatabaseFactory)source).Open(cancellationToken)"
                + ".ConfigureAwait(false) ?? throw ",
            StringComparison.Ordinal
        );
        copied.ShouldBeGreaterThan(0);
        copied.ShouldBeLessThan(iterator);
        open.ShouldBeGreaterThan(iterator);
    }

    // A member that a new member of the holder's class hides implements the marked interface member, and is called
    // through a cast of the holder to the interface, as an explicit implementation is; the message of a null member
    // names it through the holder.
    [Fact]
    public void Run_HeldImplementationThatANewMemberHides_CastsTheHolderToTheInterface()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public interface IDatabaseFactory
                {
                    [SqlSourceConnection]
                    NpgsqlConnection Open();
                }

                public class FactoryBase : IDatabaseFactory
                {
                    public NpgsqlConnection Open() => new();
                }

                public sealed class DatabaseFactory : FactoryBase
                {
                    public new NpgsqlConnection Open() => new();
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(DatabaseFactory dbf);
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], Source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodsFile];
        methods.ShouldContain(
            "var connection = ((global::App.IDatabaseFactory)(dbf ?? " + HolderIsNull + ")).Open() ?? throw "
        );
        methods.ShouldContain("\"The connection source 'dbf.Open' of 'App.Sample' is null.\"");
    }

    // A member of one name of two interfaces of the holder's class, called directly through one and through a cast to
    // the other, or through a cast to each of two constructions of one generic interface, is two connections, which
    // SQLSRC029 names apart by the interface of each one reached through a cast.
    [Theory]
    [InlineData(
        """
                public interface IMainOpener
                {
                    [SqlSourceConnection]
                    NpgsqlConnection Open();
                }

                public interface IAuditOpener
                {
                    [SqlSourceConnection]
                    NpgsqlConnection Open();
                }

                public sealed class DatabaseFactory : IMainOpener, IAuditOpener
                {
                    public NpgsqlConnection Open() => new();

                    NpgsqlConnection IAuditOpener.Open() => new();
                }
            """,
        "'dbf.Open', 'dbf.IAuditOpener.Open'"
    )]
    [InlineData(
        """
                public interface IFactory<TConnection>
                {
                    [SqlSourceConnection]
                    TConnection Open();
                }

                public sealed class DatabaseFactory : IFactory<NpgsqlConnection>, IFactory<DbConnection>
                {
                    NpgsqlConnection IFactory<NpgsqlConnection>.Open() => new();

                    DbConnection IFactory<DbConnection>.Open() => new NpgsqlConnection();
                }
            """,
        "'dbf.IFactory<NpgsqlConnection>.Open', 'dbf.IFactory<DbConnection>.Open'"
    )]
    public void Run_HeldMembersOfOneNameThroughACast_ReportsSqlsrc029NamingEachApart(string factory, string members)
    {
        var source =
            "using System.Data.Common;\nusing Npgsql;\nusing SqlSource;\n\nnamespace App\n{\n"
            + factory
            + "\n    [SqlSourceGenerate(Source = \".\")]\n    public partial class Sample(DatabaseFactory dbf);\n}\n";

        var run = RepositoryRuns.Run(UsersFile, [GetUserEntry()], source);

        var problem = run.Problems().ShouldHaveSingleItem();
        problem.ShouldStartWith("SQLSRC029 ");
        problem.ShouldContain(
            "'App.Sample' has more than one connection for the database 'postgres': " + members + ".  Mark only "
        );
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

    // A static holder of a base type may have the repository's name, and a token of that name hides nothing of it,
    // since the method reaches it from global::.
    [Fact]
    public void Run_TokenNamedLikeAStaticHolderOfTheTypesName_ReportsNothing()
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

        run.ShouldBeClean();
        run.Sources[MethodsFile].ShouldContain("global::App.Sample.Sample");
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
