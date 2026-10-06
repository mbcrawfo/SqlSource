using Shouldly;
using Xunit;

namespace SqlSource.Tests.Generator;

// The problems of the attributed type.  A type with any of them gets no generated file.
public class TypeDiagnosticsTests
{
    private const string AttributeOnly = "SqlQueriesAttribute.g.cs";

    private static readonly SqlFile Users = new("/app/Repo/Users.sql", "-- name: GetUser\nSELECT 1;\n");

    [Fact]
    public void Run_TypeThatIsNotPartial_IsAnErrorAtItsName()
    {
        var run = GeneratorHarness.Run(
            """
            using SqlSource;
            namespace App;
            [SqlQueries]
            public class Sample { }
            """,
            Users
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC001 /app/Repo/Sample.cs(4,14)-(4,20): 'Sample' must be declared partial so that SqlSource can add "
                + "members to it",
        ]);
        run.Sources.Keys.ShouldBe([AttributeOnly]);
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_ContainingTypesThatAreNotPartial_AreEachAnError()
    {
        var run = GeneratorHarness.Run(
            """
            using SqlSource;
            namespace App;
            public class Outer
            {
                public partial struct Middle
                {
                    public record @class
                    {
                        [SqlQueries]
                        public partial class Sample { }
                    }
                }
            }
            """,
            Users
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC001 /app/Repo/Sample.cs(3,14)-(3,19): 'Outer' must be declared partial so that SqlSource can add "
                + "members to it",
            "SQLSRC001 /app/Repo/Sample.cs(7,23)-(7,29): 'class' must be declared partial so that SqlSource can add "
                + "members to it",
        ]);
        run.Sources.Keys.ShouldBe([AttributeOnly]);
    }

    [Fact]
    public void Run_FileLocalType_IsAnErrorAtItsName()
    {
        var run = GeneratorHarness.Run(
            """
            using SqlSource;
            namespace App;
            [SqlQueries]
            file partial class Sample { }
            """,
            Users
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC002 /app/Repo/Sample.cs(4,20)-(4,26): 'Sample' is a file-local type, and SqlSource cannot add "
                + "members to it",
        ]);
        run.Sources.Keys.ShouldBe([AttributeOnly]);
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_TypeNestedInAFileLocalType_IsAnErrorAtTheFileLocalType()
    {
        var run = GeneratorHarness.Run(
            """
            using SqlSource;
            namespace App;
            file static partial class Outer
            {
                [SqlQueries]
                public partial class Sample { }
            }
            """,
            Users
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC002 /app/Repo/Sample.cs(3,27)-(3,32): 'Outer' is a file-local type, and SqlSource cannot add "
                + "members to it",
        ]);
        run.Sources.Keys.ShouldBe([AttributeOnly]);
    }

    [Fact]
    public void Run_ProjectThatTargetsAFrameworkOlderThanNet8_IsAnErrorAtEachAttribute()
    {
        var run = GeneratorHarness.Run(
            [
                new SourceFile(
                    GeneratorHarness.SourcePath,
                    """
                    using SqlSource;
                    namespace App;
                    [SqlQueries]
                    public partial class First { }
                    [SqlQueries(Mode = SqlQueriesMode.Direct)]
                    public partial class Second { }
                    """
                ),
            ],
            [Users],
            supportedFramework: false
        );

        run.Diagnostics.ShouldBe(
            [
                "SQLSRC003 /app/Repo/Sample.cs(3,2)-(3,12): SqlSource generates code for .NET 8 and later, and this "
                    + "project targets an older framework",
                "SQLSRC003 /app/Repo/Sample.cs(5,2)-(5,42): SqlSource generates code for .NET 8 and later, and this "
                    + "project targets an older framework",
            ],
            ignoreOrder: true
        );
        run.Sources.Keys.ShouldBe([AttributeOnly]);
    }

    [Theory]
    [InlineData("Missing.sql")]
    [InlineData("Missing")]
    [InlineData("Sub")]
    [InlineData("../../../Users.sql")]
    [InlineData("/app/Repo/Users.sql")]
    public void Run_PathThatMatchesNothing_IsAnErrorAtTheAttribute(string path)
    {
        var run = GeneratorHarness.Run(
            $$"""
            using SqlSource;
            namespace App;
            [SqlQueries(Path = "{{path}}")]
            public partial class Sample { }
            """,
            Users,
            new SqlFile("/app/Repo/Sub/Deeper/Orders.sql", "SELECT 2;\n")
        );

        run.Diagnostics.ShouldHaveSingleItem()
            .ShouldBe(
                $"SQLSRC004 /app/Repo/Sample.cs(3,2)-(3,{23 + path.Length}): Path '{path}' matches no .sql file.  It "
                    + "is relative to the folder of this file; a value that ends in .sql is one file, and any other "
                    + "value is a folder."
            );
        run.Sources.Keys.ShouldBe([AttributeOnly]);
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_NoPathAndNoSqlFileInTheFolder_IsAnErrorAtTheAttribute()
    {
        var run = GeneratorHarness.Run(
            """
            using SqlSource;
            namespace App;
            [SqlQueries]
            public partial class Sample { }
            """,
            new SqlFile("/app/Repo/Sub/Users.sql", "SELECT 1;\n"),
            new SqlFile("/app/Users.sql", "SELECT 2;\n"),
            new SqlFile("/app/Repo/Users.txt", "SELECT 3;\n")
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC005 /app/Repo/Sample.cs(3,2)-(3,12): The folder of this file has no .sql file.  Add one, or set "
                + "Path.",
        ]);
        run.Sources.Keys.ShouldBe([AttributeOnly]);
    }

    [Theory]
    [InlineData("(SqlQueriesMode)2", "2")]
    [InlineData("(SqlQueriesMode)(-1)", "-1")]
    public void Run_ModeThatIsNotDefined_IsAnErrorAtTheAttribute(string mode, string value)
    {
        var run = GeneratorHarness.Run(
            $$"""
            using SqlSource;
            namespace App;
            [SqlQueries(Mode = {{mode}})]
            public partial class Sample { }
            """,
            Users
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC006 /app/Repo/Sample.cs(3,2)-(3,{21 + mode.Length}): '{value}' is not a value of SqlQueriesMode",
        ]);
        run.Sources.Keys.ShouldBe([AttributeOnly]);
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("private string Sql { get; set; } = string.Empty;")]
    [InlineData("private const int Sql = 1;")]
    [InlineData("public static void Sql() { }")]
    [InlineData("private static class Sql { }")]
    public void Run_NestedModeAndAMemberNamedSql_IsAnErrorAtTheAttribute(string member)
    {
        var run = GeneratorHarness.Run(
            $$"""
            using SqlSource;
            namespace App;
            [SqlQueries]
            public partial class Sample
            {
                {{member}}
            }
            """,
            Users
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC007 /app/Repo/Sample.cs(3,2)-(3,12): 'Sample' already has a member named 'Sql'.  Rename it, or "
                + "use SqlQueriesMode.Direct.",
        ]);
        run.Sources.Keys.ShouldBe([AttributeOnly]);
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_DirectModeAndAMemberNamedSql_IsAllowed()
    {
        var run = GeneratorHarness.Run(
            """
            using SqlSource;
            namespace App;
            [SqlQueries(Mode = SqlQueriesMode.Direct)]
            public partial class Sample
            {
                public static string Sql => GetUser;
            }
            """,
            Users
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources.Keys.ShouldContain("App.Sample.g.cs");
    }

    [Fact]
    public void Run_TypeWithSeveralProblems_ReportsThemAll()
    {
        var run = GeneratorHarness.Run(
            """
            using SqlSource;
            namespace App;
            [SqlQueries(Path = "Missing", Mode = (SqlQueriesMode)7)]
            public class Sample
            {
                public int Sql;
            }
            """,
            Users
        );

        // A mode that is not valid is read as Nested, so the member named Sql is a problem too.
        run.Diagnostics.Count.ShouldBe(4);
        run.Diagnostics[0].ShouldStartWith("SQLSRC001 /app/Repo/Sample.cs(4,14)-(4,20)");
        run.Diagnostics[1].ShouldStartWith("SQLSRC006 /app/Repo/Sample.cs(3,2)-(3,56)");
        run.Diagnostics[2].ShouldStartWith("SQLSRC007 /app/Repo/Sample.cs(3,2)-(3,56)");
        run.Diagnostics[3].ShouldStartWith("SQLSRC004 /app/Repo/Sample.cs(3,2)-(3,56)");
        run.Sources.Keys.ShouldBe([AttributeOnly]);
    }

    [Fact]
    public void Run_TypeWithAProblem_StillReportsTheErrorsOfItsSqlFiles()
    {
        var run = GeneratorHarness.Run(
            """
            using SqlSource;
            namespace App;
            [SqlQueries]
            public class Sample { }
            """,
            new SqlFile("/app/Repo/Users.sql", "-- name: 1x\nSELECT 1;\n")
        );

        run.Diagnostics.Count.ShouldBe(2);
        run.Diagnostics.ShouldContain(diagnostic => diagnostic.StartsWith("SQLSRC001 /app/Repo/Sample.cs"));
        run.Diagnostics.ShouldContain(diagnostic =>
            diagnostic.StartsWith("SQLSRC103 /app/Repo/Users.sql(1,10)-(1,12)")
        );
    }

    [Fact]
    public void Run_AttributeOnAnInterfaceOrAnEnum_IsLeftToTheCompiler()
    {
        var run = GeneratorHarness.Run(
            """
            using SqlSource;
            namespace App;
            [SqlQueries]
            public partial interface ISample { }
            [SqlQueries]
            public enum Kind { None }
            """,
            Users
        );

        run.Diagnostics.ShouldBeEmpty();
        run.Sources.Keys.ShouldBe([AttributeOnly]);
        run.CompilationErrors.Count.ShouldBe(2);
        run.CompilationErrors.ShouldAllBe(error => error.StartsWith("CS0592 "));
    }
}
