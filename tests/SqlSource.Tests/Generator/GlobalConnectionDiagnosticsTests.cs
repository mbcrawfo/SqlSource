// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System.Linq;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Repositories;
using SqlSource.Testing.Generator;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The marks of the project's global connections that can never give one, each reported once at its mark by the stage
// that finds them, however many repositories there are, and none at all.  Each run is under postgres on Npgsql 10, at
// Repository.
public sealed class GlobalConnectionDiagnosticsTests
{
    private static readonly SqlFile UsersFile = new(
        GeneratorHarness.UsersPath,
        "-- name: GetUser\nSELECT id FROM users;\n"
    );

    private static readonly SqlFile UserRepositoryFile = new(
        "/app/Repo/UserRepository.sql",
        "-- name: GetUser\nSELECT id FROM users;\n"
    );

    private static readonly SqlFile OrderRepositoryFile = new(
        "/app/Repo/OrderRepository.sql",
        "-- name: GetOrder\nSELECT id FROM orders;\n"
    );

    // Two repositories, each with its own connection, that claim their files by their names.
    private const string TwoRepositories = """

            [SqlSourceGenerate]
            public partial class UserRepository(NpgsqlDataSource dataSource);

            [SqlSourceGenerate]
            public partial class OrderRepository(NpgsqlDataSource dataSource);
        }
        """;

    [Fact]
    public void Run_GlobalInGenericType_ReportsSqlsrc032OnceAtTheMark()
    {
        const string Source =
            """
                using Npgsql;
                using SqlSource;

                namespace App
                {
                    public static class Box<T>
                    {
                        [SqlSourceConnection]
                        public static string Main = "";
                    }
                """ + TwoRepositories;

        var run = RunTwoRepositories(Source);

        run.Problems().ShouldBe([Sqlsrc032("(9,30)-(9,34)", "Main", "App.Box<T>")]);
    }

    [Fact]
    public void Run_StaticAbstractInterfaceMember_ReportsSqlsrc032()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public interface IDb
                {
                    [SqlSourceConnection]
                    static abstract string Main { get; }
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource dataSource);
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.Problems().ShouldBe([Sqlsrc032("(9,32)-(9,36)", "Main", "App.IDb")]);
    }

    [Fact]
    public void Run_MarkedMethodParameter_ReportsSqlsrc032()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public class Helper
                {
                    public static string Use([SqlSourceConnection] string connectionString) => connectionString;
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource dataSource);
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.Problems().ShouldBe([Sqlsrc032("(8,63)-(8,79)", "connectionString", "App.Helper")]);
    }

    [Fact]
    public void Run_MarkedPrimaryConstructorParameterOfNonRepository_ReportsSqlsrc032()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public class Helper([SqlSourceConnection] string connectionString)
                {
                    public string Value => connectionString;
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource dataSource);
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.Problems().ShouldBe([Sqlsrc032("(6,54)-(6,70)", "connectionString", "App.Helper")]);
    }

    // A parameter of a local function is never a source either.
    [Fact]
    public void Run_MarkedLocalFunctionParameter_ReportsSqlsrc032()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public static class Helper
                {
                    public static string Use()
                    {
                        return Local("");

                        static string Local([SqlSourceConnection] string text) => text;
                    }
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource dataSource);
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.Problems().ShouldBe([Sqlsrc032("(12,62)-(12,66)", "text", "App.Helper")]);
    }

    // A parameter of a lambda is no member's, and is found again by where it is.
    [Fact]
    public void Run_MarkedLambdaParameter_ReportsSqlsrc032()
    {
        const string Source = """
            using System;
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public static class Helper
                {
                    public static Func<string, string> Use = ([SqlSourceConnection] string text) => text;
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource dataSource);
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.Problems().ShouldBe([Sqlsrc032("(9,80)-(9,84)", "text", "App.Helper")]);
    }

    // A local function is no member, whose id could name a method of the type, so it is found by where it is.
    [Fact]
    public void Run_MarkedLocalFunction_ReportsSqlsrc032()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public static class Helper
                {
                    public static string Use()
                    {
                        return Local();

                        [SqlSourceConnection]
                        static string Local() => "";
                    }

                    public static string Local() => "";
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource dataSource);
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.Problems().ShouldBe([Sqlsrc032("(13,27)-(13,32)", "Local", "App.Helper")]);
    }

    // The accessor of a static property is not the property, and the mark of either is a mark the stage finds.
    [Fact]
    public void Run_MarkedAccessorOfAStaticProperty_ReportsSqlsrc032()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public static class Db
                {
                    public static string Main { [SqlSourceConnection] get; } = "";
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource dataSource);
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.Problems().ShouldBe([Sqlsrc032("(8,59)-(8,62)", "Main", "App.Db")]);
    }

    [Fact]
    public void Run_UnsupportedGlobal_ReportsSqlsrc032OnceAtTheMark()
    {
        const string Source =
            """
                using Npgsql;
                using SqlSource;

                namespace App
                {
                    public static class Db
                    {
                        [SqlSourceConnection]
                        public static string Main(int id) => "";
                    }
                """ + TwoRepositories;

        var run = RunTwoRepositories(Source);

        run.Problems().ShouldBe([Sqlsrc032("(9,30)-(9,34)", "Main", "App.Db")]);
    }

    [Fact]
    public void Run_BrokenGlobalWithoutRepositories_StillReported()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                public static class Db
                {
                    [SqlSourceConnection]
                    public static int Main = 0;
                }
            }
            """;

        var run = GeneratorHarness.Run(OutputKind.Repository, Source);

        run.Problems().ShouldBe([Sqlsrc032("(8,27)-(8,31)", "Main", "App.Db")]);
    }

    // An instance member outside a repository is a factory's, read through a holder that holds it.
    [Fact]
    public void Run_MarkedInstanceMemberOutsideRepository_ReportsNothing()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public class Factory
                {
                    [SqlSourceConnection]
                    public string Main { get; } = "";
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource dataSource);
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.ShouldBeClean();
    }

    // A static member of a repository is the repository's own, which its repository reports when it is broken, and no
    // other repository's global.
    [Fact]
    public void Run_StaticMemberOfARepository_IsNoGlobalOfAnother()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                public partial class UserRepository
                {
                    [SqlSourceConnection]
                    public static int Broken = 0;
                }

                [SqlSourceGenerate]
                public partial class OrderRepository
                {
                    [SqlSourceConnection]
                    public static NpgsqlDataSource Main { get; set; } = null!;
                }
            }
            """;

        var run = RunTwoRepositories(Source);

        run.Problems().ShouldBe([Sqlsrc032("(10,27)-(10,33)", "Broken", "App.UserRepository")]);
        var shapes = StepOutputs.Single<EquatableArray<RepositoryShape>>(run, TrackingNames.RepositoryShapes);
        shapes.Select(shape => shape.GlobalCandidates.Count).ShouldBe([0, 0]);
        shapes.Select(shape => shape.Candidates.Single().Name).ShouldBe(["Main", "Broken"]);
    }

    // The source and the two files that UserRepository and OrderRepository claim by their names, with their sidecars.
    private static GeneratorRun RunTwoRepositories(string source) =>
        GeneratorHarness.Run(
            OutputKind.Repository,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            [
                UserRepositoryFile,
                Sidecars.Of(UserRepositoryFile, Entry(UserRepositoryFile, "GetUser", [], [Column("id")])),
                OrderRepositoryFile,
                Sidecars.Of(OrderRepositoryFile, Entry(OrderRepositoryFile, "GetOrder", [], [Column("id")])),
            ],
            references: [NpgsqlReferences.Npgsql10],
            dialect: "postgres"
        );

    private static string Sqlsrc032(string span, string member, string type) =>
        "SQLSRC032 "
        + GeneratorHarness.SourcePath
        + span
        + ": '"
        + member
        + "' of '"
        + type
        + "' is marked [SqlSourceConnection], and cannot give a connection.  Mark a field, property or "
        + "primary-constructor parameter of a string, a DbDataSource or a DbConnection, or a method that is not "
        + "generic, takes nothing or a CancellationToken, and returns a string, a DbConnection, or a Task or "
        + "ValueTask of a DbConnection.";
}
