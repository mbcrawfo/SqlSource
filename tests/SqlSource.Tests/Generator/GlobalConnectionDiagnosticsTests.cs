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

    // The generated code is in another file than a file-local type, so it can never reach its global.
    [Fact]
    public void Run_GlobalInFileLocalType_ReportsSqlsrc032()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                file static class Db
                {
                    [SqlSourceConnection]
                    public static string Main = "";
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource dataSource);
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.Problems().ShouldBe([Sqlsrc032("(9,30)-(9,34)", "Main", "App.Db")]);
    }

    // A file-local type and an ordinary one of one name are two types, and only the file-local one's global is broken.
    [Fact]
    public void Run_FileLocalAndOrdinaryTypesOfOneName_ReportOnlyTheFileLocalOne()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                file static class Db
                {
                    [SqlSourceConnection]
                    public static string Main = "";
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource dataSource);
            }
            """;
        const string Ordinary = """
            using SqlSource;

            namespace App
            {
                public static class Db
                {
                    [SqlSourceConnection]
                    public static string Main = "";
                }
            }
            """;

        var run = GeneratorHarness.Run(
            OutputKind.Repository,
            [new SourceFile(GeneratorHarness.SourcePath, Source), new SourceFile("/app/Repo/Db.cs", Ordinary)],
            [UsersFile, Sidecars.Of(UsersFile, Entry(UsersFile, "GetUser", [], [Column("id")]))],
            references: [NpgsqlReferences.Npgsql10],
            dialect: "postgres"
        );

        run.Problems().ShouldBe([Sqlsrc032("(9,30)-(9,34)", "Main", "App.Db")]);
        var shape = StepOutputs
            .Single<EquatableArray<RepositoryShape>>(run, TrackingNames.RepositoryShapes)
            .ShouldHaveSingleItem();
        var global = shape.GlobalCandidates.ShouldHaveSingleItem();
        global.GlobalTypeText.ShouldBe("global::App.Db");
        global.Location.Path.ShouldBe("/app/Repo/Db.cs");
    }

    // A marked static of a repository's base type is the repository's own, which its own candidates read, even in a
    // generic base type, whose global would be broken.
    [Fact]
    public void Run_StaticOfAGenericBaseOfARepository_IsTheRepositorysOwn()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                public abstract class RepositoryBase<T>
                {
                    [SqlSourceConnection]
                    protected static string Cs { get; } = "";
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample : RepositoryBase<Sample>;
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.ShouldBeClean();
        run.Sources["App.Sample-Methods.g.cs"].ShouldContain(" async ");
    }

    // A broken static of a base type is each derived repository's own problem, which each reports, and not a global's.
    [Fact]
    public void Run_BrokenStaticOfABaseOfTwoRepositories_IsReportedByEachRepository()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                public abstract class RepositoryBase
                {
                    [SqlSourceConnection]
                    protected static int Cs = 0;
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase;

                [SqlSourceGenerate]
                public partial class OrderRepository : RepositoryBase;
            }
            """;

        var run = RunTwoRepositories(Source);

        run.Problems()
            .OrderBy(problem => problem, System.StringComparer.Ordinal)
            .ShouldBe([
                Sqlsrc032("(8,30)-(8,32)", "Cs", "App.OrderRepository"),
                Sqlsrc032("(8,30)-(8,32)", "Cs", "App.UserRepository"),
            ]);
    }

    // A static of a repository's base type is the repository's own member, so one that a member of its name hides is
    // not the repository's source as a global either; a repository that does not derive from the type still has it.
    [Fact]
    public void Run_HiddenStaticOfARepositorysBase_IsNoGlobalOfThatRepository()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                public abstract class RepositoryBase
                {
                    [SqlSourceConnection]
                    public static string Cs = "";
                }

                [SqlSourceGenerate]
                public partial class UserRepository : RepositoryBase
                {
                    public static new int Cs = 0;
                }

                [SqlSourceGenerate]
                public partial class OrderRepository;
            }
            """;

        var run = RunTwoRepositories(Source);

        var shapes = StepOutputs.Single<EquatableArray<RepositoryShape>>(run, TrackingNames.RepositoryShapes);
        shapes.Single(shape => shape.TypeKey == "App.UserRepository").GlobalCandidates.ShouldBeEmpty();
        shapes
            .Single(shape => shape.TypeKey == "App.OrderRepository")
            .GlobalCandidates.ShouldHaveSingleItem()
            .GlobalTypeText.ShouldBe("global::App.RepositoryBase");
        var problem = run.Problems().ShouldHaveSingleItem();
        problem.ShouldStartWith("SQLSRC028 ");
        problem.ShouldContain("'App.UserRepository'");
    }

    // A lambda is a method, which the attribute may mark, and never a source.  A lambda has no name, so the problem is
    // at the whole lambda, its mark too.
    [Fact]
    public void Run_MarkedLambda_ReportsSqlsrc032()
    {
        const string Source = """
            using System;
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public static class Helper
                {
                    public static Func<string> Use = [SqlSourceConnection] () => "";
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource dataSource);
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.Problems().ShouldBe([Sqlsrc032("(9,42)-(9,72)", "lambda expression", "App.Helper")]);
    }

    // An enum member is a field, which the attribute may mark, and never a source.
    [Fact]
    public void Run_MarkedEnumMember_ReportsSqlsrc032()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public enum Kind
                {
                    [SqlSourceConnection]
                    Main,
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource dataSource);
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.Problems().ShouldBe([Sqlsrc032("(9,9)-(9,13)", "Main", "App.Kind")]);
    }

    // The marks are reported in their order, by their declarations' ids: a field's before a method's.
    [Fact]
    public void Run_SeveralBrokenMarks_ReportedInTheOrderOfTheMarks()
    {
        const string Source = """
            using Npgsql;
            using SqlSource;

            namespace App
            {
                public static class Db
                {
                    [SqlSourceConnection]
                    public static string Alpha(int id) => "";

                    [SqlSourceConnection]
                    public static int Zed = 0;
                }

                [SqlSourceGenerate(Source = ".")]
                public partial class Sample(NpgsqlDataSource dataSource);
            }
            """;

        var run = RepositoryRuns.Run(UsersFile, [Entry(UsersFile, "GetUser", [], [Column("id")])], Source);

        run.Problems()
            .ShouldBe([Sqlsrc032("(12,27)-(12,30)", "Zed", "App.Db"), Sqlsrc032("(9,30)-(9,35)", "Alpha", "App.Db")]);
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
