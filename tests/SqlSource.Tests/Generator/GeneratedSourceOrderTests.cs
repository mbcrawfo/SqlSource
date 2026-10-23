// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Linq;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Testing;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The files a type's path selects, and the order their queries take in the type's file.
public sealed class GeneratedSourceOrderTests
{
    [Fact]
    public void Run_SeveralFiles_OrdersMembersByFilePathIgnoringCaseThenByPositionInTheFile()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            GeneratorHarness.SampleSource(null),
            new SqlFile("/app/Repo/c.sql", "-- name: Fifth\nSELECT 5;\n"),
            new SqlFile("/app/Repo/B.sql", "-- name: Third\nSELECT 3;\n-- name: Fourth\nSELECT 4;\n"),
            new SqlFile("/app/Repo/a.sql", "-- name: Second\nSELECT 2;\n"),
            new SqlFile("/app/Repo/A_First.SQL", "SELECT 1;\n"),
            new SqlFile("/app/Repo/Sub/Nested.sql", "SELECT 0;\n"),
            new SqlFile("/app/Other/Elsewhere.sql", "SELECT 0;\n")
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"]
            .Split('\n')
            .Where(line => line.Contains("public const string", StringComparison.Ordinal))
            .Select(line => line.Trim())
            .ShouldBe([
                "public const string Second = \"SELECT 2;\";",
                "public const string A_First = \"SELECT 1;\";",
                "public const string Third = \"SELECT 3;\";",
                "public const string Fourth = \"SELECT 4;\";",
                "public const string Fifth = \"SELECT 5;\";",
            ]);
    }

    [Theory]
    [InlineData("../Queries/Users.sql", "FromQueriesUsers")]
    [InlineData("..\\Queries\\USERS.SQL", "FromQueriesUsers")]
    [InlineData("../Queries", "FromQueriesOrders", "FromQueriesUsers")]
    [InlineData("Sub", "FromSub")]
    [InlineData("./Sub/", "FromSub")]
    [InlineData("", "FromRepo")]
    public void Run_Path_SelectsAFileOrTheFilesOfAFolder(string path, params string[] expectedMembers)
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            $$"""
            using SqlSource;

            namespace App;

            [SqlSourceGenerate(Path = @"{{path}}", SqlLocation = SqlLocation.Direct)]
            public partial class Sample;
            """,
            new SqlFile(GeneratorHarness.UsersPath, "-- name: FromRepo\nSELECT 1;\n"),
            new SqlFile("/app/Repo/Sub/Users.sql", "-- name: FromSub\nSELECT 2;\n"),
            new SqlFile("/app/Queries/Users.sql", "-- name: FromQueriesUsers\nSELECT 3;\n"),
            new SqlFile("/app/Queries/Orders.sql", "-- name: FromQueriesOrders\nSELECT 4;\n")
        );

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"]
            .Split('\n')
            .Where(line => line.Contains("public const string", StringComparison.Ordinal))
            .Select(line => line.Trim().Split(' ')[3])
            .ShouldBe(expectedMembers);
    }

    [Fact]
    public void Run_WindowsPaths_ResolveTheSameWay()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [
                new SourceFile(
                    @"C:\app\Repo\Sample.cs",
                    "[SqlSource.SqlSourceGenerate]\n"
                        + "public partial class Sample { public string Value => Sql.GetUser; }\n"
                ),
            ],
            [new SqlFile(@"c:\App\repo\Users.sql", SqlTexts.GetUser)]
        );

        run.ShouldBeClean();
        run.Sources["Sample.g.cs"].ShouldContain("/// The <c>GetUser</c> query from <c>Users.sql</c>.\n");
    }

    [Fact]
    public void Run_FileSharedByTwoTypes_GivesBothItsConstants()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;

            namespace App;

            [SqlSourceGenerate]
            public partial class First
            {
                public static string Value => Sql.GetUser;
            }

            [SqlSourceGenerate(Path = "Users.sql", SqlLocation = SqlLocation.Direct)]
            public partial class Second
            {
                public static string Value => GetUser;
            }
            """,
            GeneratorHarness.UsersFile
        );

        run.ShouldBeClean();
        run.Sources["App.First.g.cs"].ShouldContain("public const string GetUser = \"SELECT 1;\";");
        run.Sources["App.Second.g.cs"].ShouldContain("public const string GetUser = \"SELECT 1;\";");
    }
}
