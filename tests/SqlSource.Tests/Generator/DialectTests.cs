// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Collections.Generic;
using System.Text.Json.Nodes;
using Shouldly;
using SqlSource.Testing.Generator;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// Which dialect a file is read by: the -- dialect: marker at the top of the file decides, then the chain's settings
// files, nearest first, then on the MSBuild path the SqlSourceDialect metadata of the file's item and then the
// project's SqlSourceDialect property, and without any of them it is ANSI.  The project's settings file is the
// harness's dialect, and a folder's file is the sqlsource.json of the .sql file's folder.
//
// The tests of what only MSBuild reads, an empty value, a value over several lines, SQLSRC011 and the metadata of a
// file that no type claims, give their output and dialect as the project's properties and write no settings file,
// which would rank above them.
public sealed class DialectTests
{
    // Three dialects read this line three ways.  ANSI: the # is SQL and the two dashes start a comment.  MySQL: the #
    // starts a comment.  SQL Server: the brackets are an identifier, and the comment starts after it.
    private const string Query = "SELECT 1 # x [y--z] -- c\n";

    private const string Ansi = "SELECT 1 # x [y";

    private const string MySql = "SELECT 1";

    private const string SqlServer = "SELECT 1 # x [y--z]";

    private const string Invalid =
        "' is not a SQL dialect.  SqlSourceDialect accepts ansi, mssql, postgres, cockroachdb, mysql, mariadb, "
        + "sqlite and oracle.";

    // Valid only where a backslash does not escape.  Under plain MySQL the string is never closed.
    private const string PathQuery = "SELECT 'C:\\temp\\' AS p;\n";

    private const string QueryPath = "/app/Repo/Q.sql";

    [Theory]
    // Nothing set.
    [InlineData(null, null, null, Ansi)]
    // The project's file alone, in any case.
    [InlineData(null, null, "mysql", MySql)]
    [InlineData(null, null, "MySQL", MySql)]
    [InlineData(null, null, "tsql", SqlServer)]
    // The folder's file beats the project's.
    [InlineData(null, "mssql", null, SqlServer)]
    [InlineData(null, "mssql", "mysql", SqlServer)]
    [InlineData(null, "ansi", "mysql", Ansi)]
    // The file's marker beats both.
    [InlineData("mysql", null, null, MySql)]
    [InlineData("mysql", "mssql", null, MySql)]
    [InlineData("mssql", "mysql", "mysql", SqlServer)]
    [InlineData("ansi", "mssql", "mysql", Ansi)]
    public void Run_File_IsReadByItsMarkerThenItsFoldersFileThenTheProjectsFile(
        string? marker,
        string? folderFile,
        string? projectFile,
        string expected
    )
    {
        var run = Run(projectFile, Files(QueryPath, Query, marker, folderFile));

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + expected + "\";");
    }

    // Only what MSBuild gives can be empty, which is none, or written over several lines, which are trimmed.
    [Theory]
    [InlineData(null, "", Ansi)]
    [InlineData("", null, Ansi)]
    [InlineData("", "mysql", MySql)]
    [InlineData(null, "\n    MySQL\n  ", MySql)]
    public void Run_MSBuildValueThatIsEmptyOrOverLines_IsNoneOrTheDialectItNames(
        string? metadata,
        string? property,
        string expected
    )
    {
        var run = RunOnMSBuild(property, new SqlFile(QueryPath, Query, metadata));

        run.ShouldBeClean();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + expected + "\";");
    }

    // Each file in a folder of its own, claimed by a type of that folder: A under the project's file alone, B under its
    // folder's, and C under its marker over its folder's.
    [Fact]
    public void Run_FilesWithDifferentFoldersFiles_AreEachReadByTheirOwnDialect()
    {
        var run = GeneratorHarness.Run(
            OutputKind.Sql,
            [TypeIn("A"), TypeIn("B"), TypeIn("C")],
            [
                .. Files("/app/A/Q.sql", Query, null, null),
                .. Files("/app/B/Q.sql", Query, null, "mssql"),
                .. Files("/app/C/Q.sql", Query, "ansi", "mssql"),
            ],
            dialect: "mysql"
        );

        run.Diagnostics.ShouldBeEmpty();
        run.Sources["App.A.g.cs"].ShouldContain("public const string Q = \"" + MySql + "\";");
        run.Sources["App.B.g.cs"].ShouldContain("public const string Q = \"" + SqlServer + "\";");
        run.Sources["App.C.g.cs"].ShouldContain("public const string Q = \"" + Ansi + "\";");
    }

    [Theory]
    // In the marker, in the folder's file and in the project's.
    [InlineData("mysql,no-backslash-escapes", null, null)]
    [InlineData(null, "mysql,no-backslash-escapes", null)]
    [InlineData(null, null, "mysql,no-backslash-escapes")]
    // As the server spells it, with spaces around the commas.
    [InlineData(null, "mariadb , ansi_quotes , no_backslash_escapes", null)]
    // In the marker the value is the rest of the line, read as a settings file's is.
    [InlineData("mysql, no-backslash-escapes", null, null)]
    [InlineData("MySQL ,  NO_BACKSLASH_ESCAPES", null, null)]
    // The value that wins has the option, and the one it beats has none.
    [InlineData("mariadb,no-backslash-escapes", "mysql", "postgres")]
    [InlineData(null, "mysql,no-backslash-escapes", "mysql")]
    public void Run_OptionOfADialect_IsReadFromTheMarkerTheFoldersFileAndTheProjectsFile(
        string? marker,
        string? folderFile,
        string? projectFile
    )
    {
        var run = Run(projectFile, Files(QueryPath, PathQuery, marker, folderFile));

        run.ShouldBeClean();
    }

    // A property can be written over several lines, as the server spells the value.
    [Fact]
    public void Run_OptionOfADialectInAPropertyOverLines_IsRead()
    {
        var run = RunOnMSBuild("\n    MySQL,\n    NO_BACKSLASH_ESCAPES\n  ", new SqlFile(QueryPath, PathQuery));

        run.ShouldBeClean();
    }

    // The value that wins names no option, so the file is read without one: options are not merged.
    [Theory]
    [InlineData("mysql", null, "mysql,no-backslash-escapes")]
    [InlineData("mysql", "mysql,no-backslash-escapes", null)]
    [InlineData(null, "mysql", "mysql,no-backslash-escapes")]
    public void Run_ValueWithoutTheOption_ReplacesAValueWithItWhole(
        string? marker,
        string? folderFile,
        string? projectFile
    )
    {
        var run = Run(projectFile, Files(QueryPath, PathQuery, marker, folderFile));

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC101 ");
    }

    [Theory]
    [InlineData("postgres,ansi-quotes")]
    [InlineData("mysql,")]
    [InlineData("mysql,ansi")]
    public void Run_PropertyWithAnOptionThatIsNotValid_IsAnErrorThatQuotesTheWholeValue(string property)
    {
        var run = RunOnMSBuild(property, new SqlFile("/app/Repo/Q.sql", Query));

        run.Diagnostics.ShouldBe(["SQLSRC011 (1,1)-(1,1): '" + property + Invalid]);
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + Ansi + "\";");
    }

    [Fact]
    public void Run_MarkerWithAnOptionThatIsNotValid_IsAnErrorAtItsValue()
    {
        var run = Run(null, new SqlFile(QueryPath, "-- dialect: postgres,ansi-quotes\n" + Query));

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC111 /app/Repo/Q.sql(1,13)-(1,33): ");
    }

    // A bytes literal that ends with an escaped quote.  PostgreSQL has no escape there, and does not close it.
    [Theory]
    [InlineData("cockroachdb", null)]
    [InlineData("Cockroach", null)]
    [InlineData("postgres", "SQLSRC101 ")]
    public void Run_BytesLiteralWithAnEscapedQuote_IsReadOnlyByCockroachDb(string dialect, string? error)
    {
        var run = Run(dialect, new SqlFile(QueryPath, "SELECT b'\\'' AS x; -- c\n"));

        if (error is null)
        {
            run.Diagnostics.ShouldBeEmpty();
            run.CompilationErrors.ShouldBeEmpty();
        }
        else
        {
            run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith(error);
        }
    }

    [Theory]
    [InlineData("pgsql")]
    [InlineData("sql server")]
    [InlineData(" Postgres 16 ")]
    public void Run_PropertyThatIsNotADialect_IsAnErrorWithoutAPositionAndTheFilesAreReadAsAnsi(string property)
    {
        var run = RunOnMSBuild(property, new SqlFile("/app/Repo/Q.sql", Query));

        run.Diagnostics.ShouldBe(["SQLSRC011 (1,1)-(1,1): '" + property + Invalid]);

        // The members are still there, so the build reports this error and not one for each use of a query.
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + Ansi + "\";");
    }

    [Fact]
    public void Run_MetadataThatIsNotADialect_IsReportedOnceForEachValueAndTheFileIsReadAsAnsi()
    {
        var run = RunOnMSBuild(
            "mysql",
            new SqlFile("/app/Repo/A.sql", Query, "zeta"),
            new SqlFile("/app/Repo/B.sql", Query, "alpha"),
            new SqlFile("/app/Repo/C.sql", Query, "zeta"),
            new SqlFile("/app/Repo/D.sql", Query)
        );

        // In ordinal order, whatever the order of the files.
        run.Diagnostics.ShouldBe(["SQLSRC011 (1,1)-(1,1): 'alpha" + Invalid, "SQLSRC011 (1,1)-(1,1): 'zeta" + Invalid]);
        run.CompilationErrors.ShouldBeEmpty();

        // Not the project's dialect: the file said something, and what it said is wrong.
        var source = run.Sources["App.Sample.g.cs"];
        source.ShouldContain("public const string A = \"" + Ansi + "\";");
        source.ShouldContain("public const string D = \"" + MySql + "\";");
    }

    [Fact]
    public void Run_SameInvalidValueInThePropertyAndInMetadata_IsReportedOnce()
    {
        var run = RunOnMSBuild("nope", new SqlFile("/app/Repo/Q.sql", Query, "nope"));

        run.Diagnostics.ShouldBe(["SQLSRC011 (1,1)-(1,1): 'nope" + Invalid]);
    }

    [Fact]
    public void Run_InvalidMetadataOfAFileThatNoTypeClaims_IsNotReported()
    {
        var run = RunOnMSBuild(
            null,
            new SqlFile("/app/Repo/Q.sql", Query),
            new SqlFile("/app/Migrations/001_init.sql", "SELECT 'x;\n", "nope")
        );

        run.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Run_InvalidMetadataOfAFileWithAMarker_IsStillReportedAndTheMarkerIsUsed()
    {
        var run = RunOnMSBuild(null, new SqlFile("/app/Repo/Q.sql", "-- dialect: mysql\n" + Query, "nope"));

        run.Diagnostics.ShouldBe(["SQLSRC011 (1,1)-(1,1): 'nope" + Invalid]);
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + MySql + "\";");
    }

    [Fact]
    public void Run_InvalidPropertyInAProjectWithoutAnAttributedType_IsStillAnError()
    {
        var run = GeneratorHarness.Run(
            null,
            [new SourceFile(GeneratorHarness.SourcePath, "public class Sample;")],
            [],
            properties: new Dictionary<string, string?> { ["SqlSourceOutput"] = "Sql", ["SqlSourceDialect"] = "nope" }
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC011 ");
        run.Sources.Keys.ShouldBe(["SqlSourceGenerateAttribute.g.cs"]);
    }

    // The two settings of the project are read apart: a value of one is never taken for the other.
    [Fact]
    public void Run_BothPropertiesInvalid_ReportsEachUnderItsOwnId()
    {
        var run = GeneratorHarness.Run(
            null,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            [new SqlFile("/app/Repo/Q.sql", Query)],
            properties: new Dictionary<string, string?>
            {
                ["SqlSourceOutput"] = "Sql",
                ["SqlSourceGeneratorParameters"] = "mysql",
                ["SqlSourceDialect"] = "false",
            }
        );

        run.Diagnostics.Count.ShouldBe(2);
        run.Diagnostics.ShouldContain(diagnostic => diagnostic.StartsWith("SQLSRC014 (1,1)-(1,1): '"));
        run.Diagnostics.ShouldContain("SQLSRC011 (1,1)-(1,1): 'false" + Invalid);
    }

    // The files at sql under the project's file of the dialect, or of none when it is null.
    private static GeneratorRun Run(string? projectFile, params SqlFile[] files) =>
        GeneratorHarness.Run(
            OutputKind.Sql,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            files,
            dialect: projectFile
        );

    // The files at sql under the dialect, null for none, each the project's property, for a test of the MSBuild path.
    private static GeneratorRun RunOnMSBuild(string? property, params SqlFile[] files) =>
        GeneratorHarness.Run(
            null,
            [new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))],
            files,
            properties: new Dictionary<string, string?> { ["SqlSourceOutput"] = "Sql", ["SqlSourceDialect"] = property }
        );

    // The .sql file at the path, under its marker of the dialect when one is given, and then the settings file of its
    // folder when that file's dialect is given.
    private static SqlFile[] Files(string path, string text, string? marker, string? folderFile)
    {
        var file = new SqlFile(path, (marker is null ? string.Empty : "-- dialect: " + marker + "\n") + text);
        if (folderFile is null)
        {
            return [file];
        }

        var folder = path[..(path.LastIndexOf('/') + 1)];
        return
        [
            file,
            GeneratorHarness.SettingsFile(
                folder + "sqlsource.json",
                GeneratorHarness.Json(new JsonObject { ["dialect"] = folderFile })
            ),
        ];
    }

    // The type App.<name> in /app/<name>/, which claims the .sql files of that folder.
    private static SourceFile TypeIn(string name) =>
        new(
            "/app/" + name + "/" + name + ".cs",
            "using SqlSource;\n\nnamespace App;\n\n[SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]\n"
                + "public partial class "
                + name
                + ";\n"
        );
}
