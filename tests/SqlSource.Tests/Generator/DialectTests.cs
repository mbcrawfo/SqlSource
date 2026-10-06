using Shouldly;
using Xunit;

namespace SqlSource.Tests.Generator;

// Which dialect a file is read by: the dialect= directive at the top of the file decides, then the SqlSourceDialect
// metadata of the file's item, then the project's SqlSourceDialect property, and without any of them it is ANSI.
public class DialectTests
{
    private const string Source = """
        using SqlSource;

        namespace App;

        [SqlQueries(Mode = SqlQueriesMode.Direct)]
        public partial class Sample;
        """;

    // Three dialects read this line three ways.  ANSI: the # is SQL and the two dashes start a comment.  MySQL: the #
    // starts a comment.  SQL Server: the brackets are an identifier, and the comment starts after it.
    private const string Query = "SELECT 1 # x [y--z] -- c\n";

    private const string Ansi = "SELECT 1 # x [y";

    private const string MySql = "SELECT 1";

    private const string SqlServer = "SELECT 1 # x [y--z]";

    private const string Invalid =
        "' is not a SQL dialect.  SqlSourceDialect accepts ansi, mssql, postgres, mysql, mariadb, sqlite and oracle.";

    [Theory]
    // Nothing set.
    [InlineData(null, null, null, Ansi)]
    [InlineData(null, null, "", Ansi)]
    [InlineData(null, "", null, Ansi)]
    // The property alone, in any case and with the whitespace of a value on its own line.
    [InlineData(null, null, "mysql", MySql)]
    [InlineData(null, null, "\n    MySQL\n  ", MySql)]
    [InlineData(null, null, "tsql", SqlServer)]
    // The file's metadata beats the property.  Empty metadata is none.
    [InlineData(null, "mssql", null, SqlServer)]
    [InlineData(null, "mssql", "mysql", SqlServer)]
    [InlineData(null, "ansi", "mysql", Ansi)]
    [InlineData(null, "", "mysql", MySql)]
    // The file's directive beats both.
    [InlineData("mysql", null, null, MySql)]
    [InlineData("mysql", "mssql", null, MySql)]
    [InlineData("mssql", "mysql", "mysql", SqlServer)]
    [InlineData("ansi", "mssql", "mysql", Ansi)]
    public void Run_File_IsReadByItsDirectiveThenItsMetadataThenTheProperty(
        string? directive,
        string? metadata,
        string? property,
        string expected
    )
    {
        var sql = (directive is null ? string.Empty : "-- SqlSource: dialect=" + directive + "\n") + Query;

        var run = Run(property, new SqlFile("/app/Repo/Q.sql", sql, metadata));

        run.Diagnostics.ShouldBeEmpty();
        run.CompilationErrors.ShouldBeEmpty();
        run.GeneratedCodeWarnings.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + expected + "\";");
    }

    [Fact]
    public void Run_FilesWithDifferentMetadata_AreEachReadByTheirOwnDialect()
    {
        var run = Run(
            "mysql",
            new SqlFile("/app/Repo/A.sql", Query),
            new SqlFile("/app/Repo/B.sql", Query, "mssql"),
            new SqlFile("/app/Repo/C.sql", "-- SqlSource: dialect=ansi\n" + Query, "mssql")
        );

        run.Diagnostics.ShouldBeEmpty();
        var source = run.Sources["App.Sample.g.cs"];
        source.ShouldContain("public const string A = \"" + MySql + "\";");
        source.ShouldContain("public const string B = \"" + SqlServer + "\";");
        source.ShouldContain("public const string C = \"" + Ansi + "\";");
    }

    [Theory]
    [InlineData("pgsql")]
    [InlineData("sql server")]
    [InlineData(" Postgres 16 ")]
    public void Run_PropertyThatIsNotADialect_IsAnErrorWithoutAPositionAndTheFilesAreReadAsAnsi(string property)
    {
        var run = Run(property, new SqlFile("/app/Repo/Q.sql", Query));

        run.Diagnostics.ShouldBe(["SQLSRC011 (1,1)-(1,1): '" + property + Invalid]);

        // The members are still there, so the build reports this error and not one for each use of a query.
        run.CompilationErrors.ShouldBeEmpty();
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + Ansi + "\";");
    }

    [Fact]
    public void Run_MetadataThatIsNotADialect_IsReportedOnceForEachValueAndTheFileIsReadAsAnsi()
    {
        var run = Run(
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
        var run = Run("nope", new SqlFile("/app/Repo/Q.sql", Query, "nope"));

        run.Diagnostics.ShouldBe(["SQLSRC011 (1,1)-(1,1): 'nope" + Invalid]);
    }

    [Fact]
    public void Run_InvalidMetadataOfAFileThatNoTypeClaims_IsNotReported()
    {
        var run = Run(
            null,
            new SqlFile("/app/Repo/Q.sql", Query),
            new SqlFile("/app/Migrations/001_init.sql", "SELECT 'x;\n", "nope")
        );

        run.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Run_InvalidMetadataOfAFileWithADirective_IsStillReportedAndTheDirectiveIsUsed()
    {
        var run = Run(null, new SqlFile("/app/Repo/Q.sql", "-- SqlSource: dialect=mysql\n" + Query, "nope"));

        run.Diagnostics.ShouldBe(["SQLSRC011 (1,1)-(1,1): 'nope" + Invalid]);
        run.Sources["App.Sample.g.cs"].ShouldContain("public const string Q = \"" + MySql + "\";");
    }

    [Fact]
    public void Run_InvalidPropertyInAProjectWithoutAnAttributedType_IsStillAnError()
    {
        var run = GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, "public class Sample;")],
            [],
            dialect: "nope"
        );

        run.Diagnostics.ShouldHaveSingleItem().ShouldStartWith("SQLSRC011 ");
        run.Sources.Keys.ShouldBe(["SqlQueriesAttribute.g.cs"]);
    }

    // The two settings of the project are read apart: a value of one is never taken for the other.
    [Fact]
    public void Run_BothPropertiesInvalid_ReportsEachUnderItsOwnId()
    {
        var run = GeneratorHarness.Run(
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [new SqlFile("/app/Repo/Q.sql", Query)],
            tokenValidation: "mysql",
            dialect: "false"
        );

        run.Diagnostics.Count.ShouldBe(2);
        run.Diagnostics.ShouldContain(diagnostic =>
            diagnostic.StartsWith("SQLSRC010 (1,1)-(1,1): The MSBuild property ")
        );
        run.Diagnostics.ShouldContain("SQLSRC011 (1,1)-(1,1): 'false" + Invalid);
    }

    private static GeneratorRun Run(string? property, params SqlFile[] files) =>
        GeneratorHarness.Run([new SourceFile(GeneratorHarness.SourcePath, Source)], files, dialect: property);
}
