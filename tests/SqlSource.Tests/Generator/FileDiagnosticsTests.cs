// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Testing;
using SqlSource.Testing.Generator;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The problems of a type's .sql files, and of its queries taken together.
public sealed class FileDiagnosticsTests
{
    private const string Source = """
        using SqlSource;
        namespace App;
        [SqlSourceGenerate]
        public partial class Sample { }
        """;

    private const string EmptySqlClass = $$"""
        {{GeneratedFile.Header}}
        namespace App
        {
            partial class Sample
            {
                private static class Sql
                {
                }
            }
        }

        """;

    [Fact]
    public void Run_FileWithParserErrors_ReportsEachAtItsLineAndColumnInTheSqlFile()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            Source,
            new SqlFile(
                GeneratorHarness.UsersPath,
                """
                -- generator: keep-comments shout
                -- name: GetUser
                SELECT 1;

                -- name: get-user
                SELECT 2;
                -- name: GetUser
                SELECT 3;
                """
            )
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC109 {GeneratorHarness.UsersPath}(1,29)-(1,34): 'shout' is not a generator parameter",
            $"SQLSRC103 {GeneratorHarness.UsersPath}(5,10)-(5,18): 'get-user' is not a valid query name.  A name is a "
                + "C# identifier that is not a reserved keyword.",
            $"SQLSRC104 {GeneratorHarness.UsersPath}(7,10)-(7,17): The query name 'GetUser' is used more than once in "
                + "this file",
        ]);
        run.CompilationErrors.ShouldBeEmpty();
    }

    // A query's -- column: names a result's column; a column's source is the file header's.
    [Fact]
    public void Run_DottedColumnMarkerInAQuery_IsAnErrorThatPointsToTheHeader()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            Source,
            new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\n-- column: posts.id -> PostId\nSELECT 1;\n")
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC111 {GeneratorHarness.UsersPath}(2,12)-(2,30): 'column: posts.id -> PostId' lacks a value it "
                + "needs, has one it does not take, or has one that is not valid.  A column's source is mapped in the "
                + "file header",
        ]);
    }

    [Fact]
    public void Run_DialectMarkerInsideAQuery_IsAnErrorAtTheMarker()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            Source,
            new SqlFile(GeneratorHarness.UsersPath, "-- name: GetUser\n-- dialect: mysql\nSELECT 1;\n")
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC115 {GeneratorHarness.UsersPath}(2,1)-(2,18): The '-- dialect:' marker must come before the file's "
                + "first query and before any SQL",
        ]);
        run.Sources[GeneratorHarness.SampleFile].ShouldBe(EmptySqlClass);
    }

    [Fact]
    public void Run_DialectMarkerThatIsNotValidOrConflicts_IsAnErrorAtItsValue()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            Source,
            new SqlFile(
                GeneratorHarness.UsersPath,
                "-- dialect: pgsql\n-- dialect: mysql\n-- dialect: oracle\n" + SqlTexts.GetUser
            )
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC111 {GeneratorHarness.UsersPath}(1,13)-(1,18): 'dialect: pgsql' lacks a value it needs, has one it "
                + "does not take, or has one that is not valid",
            $"SQLSRC112 {GeneratorHarness.UsersPath}(3,13)-(3,19): 'dialect: oracle' conflicts with a setting given "
                + "earlier in the same scope",
        ]);
    }

    private const string TwoSpellings = "-- dialect: mssql\n-- name: SetId\nUPDATE dbo.t SET a = @Id WHERE b = @id;\n";

    // A warning and not an error: the file keeps its query, as a project that sees no problem in the other spelling
    // needs.
    [Fact]
    public void Run_SqlServerParameterInTwoSpellings_WarnsAndStillGeneratesTheQuery()
    {
        var run = GeneratorHarness.Run(OutputKind.Sql, Source, new SqlFile(GeneratorHarness.UsersPath, TwoSpellings));

        run.Diagnostics.ShouldBe([
            $"SQLSRC121 {GeneratorHarness.UsersPath}(3,36)-(3,39): The parameter '@Id' is also written '@id'.  "
                + "SqlSource binds it as '@Id', and a SQL Server whose collation is case-sensitive reads '@id' as "
                + "another variable, which the query does not declare.  Write it '@Id' throughout the query.",
        ]);
        run.SeverityOf("SQLSRC121").ShouldBe(DiagnosticSeverity.Warning);
        run.Sources[GeneratorHarness.SampleFile]
            .ShouldContain("public const string SetId = \"UPDATE dbo.t SET a = @Id WHERE b = @id;\";");
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_SqlServerParameterInTwoSpellings_IsHiddenBySuppressingTheId()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [new SqlFile(GeneratorHarness.UsersPath, TwoSpellings)],
            diagnosticOptions: new Dictionary<string, ReportDiagnostic> { ["SQLSRC121"] = ReportDiagnostic.Suppress }
        );

        run.Diagnostics.ShouldBeEmpty();
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("public const string SetId");
    }

    [Fact]
    public void Run_FileWithAnError_GivesNoMembersAndTheTypeKeepsItsEmptySqlClass()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            Source,
            new SqlFile(GeneratorHarness.UsersPath, "SELECT 'unfinished;\n")
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC101 {GeneratorHarness.UsersPath}(1,8)-(1,9): A quoted string or identifier is not closed",
        ]);
        run.Sources[GeneratorHarness.SampleFile].ShouldBe(EmptySqlClass);
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_OneFileWithAnErrorAmongSeveral_KeepsTheMembersOfTheOthers()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            Source,
            new SqlFile("/app/Repo/Broken.sql", "/* open\nSELECT 1;\n"),
            GeneratorHarness.UsersFile
        );

        run.Diagnostics.ShouldBe(["SQLSRC102 /app/Repo/Broken.sql(1,1)-(1,3): A block comment or hint is not closed"]);
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("public const string GetUser = \"SELECT 1;\";");
    }

    [Fact]
    public void Run_FileWhoseNameIsNotAnIdentifier_IsAnErrorAtTheStartOfTheFile()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            Source,
            new SqlFile("/app/Repo/001_init.sql", "CREATE TABLE t (id int);\n")
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC105 /app/Repo/001_init.sql(1,1)-(1,1): The file has no '-- name:' marker, and its name "
                + "'001_init.sql' does not give a valid query name.  Add a marker or rename the file.",
        ]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-- nothing here\n")]
    public void Run_UnreadableOrEmptyFile_IsAnErrorAndNotAnException(string? text)
    {
        var run = GeneratorHarness.Run(OutputKind.Sql, Source, new SqlFile(GeneratorHarness.UsersPath, text));

        run.Diagnostics.ShouldBe([$"SQLSRC113 {GeneratorHarness.UsersPath}(1,1)-(1,1): The query has no SQL"]);
        run.Sources[GeneratorHarness.SampleFile].ShouldBe(EmptySqlClass);
    }

    [Fact]
    public void Run_FileSharedByTwoTypes_ReportsItsErrorsOnce()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate]
            public partial class First { }
            [SqlSourceGenerate(Path = "Users.sql")]
            public partial class Second { }
            """,
            new SqlFile(GeneratorHarness.UsersPath, "-- name: 1x\nSELECT 1;\n")
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith($"SQLSRC103 {GeneratorHarness.UsersPath}(1,10)-(1,12)");
    }

    [Fact]
    public void Run_FileListedTwice_GivesEachMemberAndEachErrorOnce()
    {
        var broken = new SqlFile("/app/Repo/Broken.sql", "-- name: 1x\nSELECT 1;\n");
        var users = GeneratorHarness.UsersFile;

        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            Source,
            broken,
            users,
            new SqlFile("/app/Repo/../Repo/Broken.sql", broken.Text),
            new SqlFile("\\app\\Repo\\Users.sql", users.Text)
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC103 /app/Repo/Broken.sql(1,10)-(1,12)");
        run.Sources[GeneratorHarness.SampleFile].Split("public const string GetUser").Length.ShouldBe(2);
        run.CompilationErrors.ShouldBeEmpty();
    }

    // The generator compares paths ignoring case and may not ask the file system, so it cannot tell two files of a
    // case-sensitive file system from one file that the project lists under two spellings.
    [Theory]
    [InlineData("/app/Repo/users.sql")]
    [InlineData("/app/repo/Users.sql")]
    [InlineData("\\app\\Repo\\Sub\\..\\USERS.SQL")]
    public void Run_TwoFilesOfATypeWhosePathsDifferOnlyByCase_IsAnErrorAtTheSecond(string second)
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            Source,
            GeneratorHarness.UsersFile,
            new SqlFile(second, "-- name: GetOrder\nSELECT 2;\n")
        );

        run.Diagnostics.ShouldBe([
            $"SQLSRC013 {second}(1,1)-(1,1): This file's path differs only by case from "
                + $"'{GeneratorHarness.UsersPath}', and SqlSource compares paths ignoring case",
        ]);
    }

    [Fact]
    public void Run_FileListedTwiceAndAnotherThatDiffersByCase_IsOneErrorForEachSpelling()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            Source,
            GeneratorHarness.UsersFile,
            new SqlFile("/app/Repo/users.sql", "-- name: GetOrder\nSELECT 2;\n"),
            new SqlFile("/app/Repo/./users.sql", "-- name: GetOrder\nSELECT 2;\n"),
            new SqlFile("/app/Repo/USERS.sql", "-- name: GetItem\nSELECT 3;\n")
        );

        run.Diagnostics.Count.ShouldBe(2);
        run.Diagnostics[0].ShouldStartWith("SQLSRC013 /app/Repo/users.sql(1,1)-(1,1)");
        run.Diagnostics[1].ShouldStartWith("SQLSRC013 /app/Repo/USERS.sql(1,1)-(1,1)");
    }

    [Fact]
    public void Run_FilesThatNoTypeClaimsWhosePathsDifferOnlyByCase_AreIgnored()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            Source,
            GeneratorHarness.UsersFile,
            new SqlFile("/app/Migrations/001_init.sql", "SELECT 1;\n"),
            new SqlFile("/app/Migrations/001_INIT.sql", "SELECT 2;\n")
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
    }

    // A ".." with no folder before it leaves a path that cannot be compared, so no type can claim the file.
    [Fact]
    public void Run_FileWhosePathLeavesItsRoot_IsIgnoredWithItsErrors()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            Source,
            GeneratorHarness.UsersFile,
            new SqlFile("../Users.sql", "-- name: 1x\nSELECT 2;\n")
        );

        run.Diagnostics.ShouldBeEmpty();
        run.Sources[GeneratorHarness.SampleFile].ShouldContain("public const string GetUser");
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_FileThatNoTypeClaims_IsIgnoredWithItsErrors()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            Source,
            GeneratorHarness.UsersFile,
            new SqlFile("/app/Migrations/001_init.sql", "SELECT 'unfinished;\n"),
            new SqlFile("/app/Repo/Sub/002-more.sql", "/* open\n")
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_QueryNameUsedInTwoFilesOfOneType_IsAnErrorAtTheSecondAndThatFileGivesNoMembers()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            Source,
            new SqlFile("/app/Repo/A.sql", SqlTexts.GetUser),
            new SqlFile("/app/Repo/B.sql", "-- name: Other\nSELECT 2;\n\n-- name: GetUser\nSELECT 3;\n")
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC008 /app/Repo/B.sql(4,10)-(4,17): The query name 'GetUser' is already used in 'A.sql', which also "
                + "belongs to 'Sample'",
        ]);
        var source = run.Sources[GeneratorHarness.SampleFile];
        source.ShouldContain("public const string GetUser = \"SELECT 1;\";");
        source.ShouldNotContain("Other");
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_QueryNameUsedInTwoFilesOfDifferentTypes_IsAllowed()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(Path = "A.sql")]
            public partial class First { }
            [SqlSourceGenerate(Path = "B.sql")]
            public partial class Second { }
            """,
            new SqlFile("/app/Repo/A.sql", SqlTexts.GetUser),
            new SqlFile("/app/Repo/B.sql", "-- name: GetUser\nSELECT 2;\n")
        );

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_NestedLocationAndAQueryNamedSql_IsAnErrorAtTheQuery()
    {
        var run = GeneratorHarness.Run(OutputKind.Sql, Source, new SqlFile("/app/Repo/Sql.sql", "SELECT 1;\n"));

        run.Diagnostics.ShouldBe([
            "SQLSRC009 /app/Repo/Sql.sql(1,1)-(1,1): A query of 'Sample' cannot be named 'Sql', because its member "
                + "would have the name of the type that contains it",
        ]);
        run.Sources[GeneratorHarness.SampleFile].ShouldBe(EmptySqlClass);
        run.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Run_DirectLocationAndATypeNamedWithAKeyword_LeavesAQueryOfThatNameToTheParser()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
            public partial class @class<T> { }
            """,
            new SqlFile(GeneratorHarness.UsersPath, "-- name: Sql\nSELECT 1;\n"),
            new SqlFile("/app/Repo/Other.sql", "-- generator: keep-comments\n-- name: class\nSELECT 2;\n")
        );

        // "class" is a reserved keyword, so the parser has already refused it as a name.
        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC103 /app/Repo/Other.sql(2,10)-(2,15)");
        run.Sources["App.class-1.g.cs"].ShouldContain("public const string Sql = \"SELECT 1;\";");
    }

    [Fact]
    public void Run_DirectLocationAndAQueryNamedLikeTheType_IsAnErrorAndItsFileGivesNoMembers()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            """
            using SqlSource;
            namespace App;
            [SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
            public partial class Sample { }
            """,
            new SqlFile("/app/Repo/A.sql", "-- name: Kept\nSELECT 1;\n"),
            new SqlFile("/app/Repo/B.sql", "-- name: Dropped\nSELECT 2;\n-- name: Sample\nSELECT 3;\n")
        );

        run.Diagnostics.ShouldBe([
            "SQLSRC009 /app/Repo/B.sql(3,10)-(3,16): A query of 'Sample' cannot be named 'Sample', because its member "
                + "would have the name of the type that contains it",
        ]);
        var source = run.Sources[GeneratorHarness.SampleFile];
        source.ShouldContain("public const string Kept = \"SELECT 1;\";");
        source.ShouldNotContain("Dropped");
        run.CompilationErrors.ShouldBeEmpty();
    }
}
