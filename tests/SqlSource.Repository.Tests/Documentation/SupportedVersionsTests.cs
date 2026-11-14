using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;

namespace SqlSource.Repository.Tests.Documentation;

// The table of supported versions on site/reference/databases/index.md is the one home of the versions a user can run:
// its server minimums are the ones the describers refuse below, its tested servers the images the tests run on, and its
// driver minimums the ones the generator refuses below.  The images are read as text, since this project does not
// reference the test harness, which brings Testcontainers.  The driver versions tested are not held by a test.
public sealed partial class SupportedVersionsTests
{
    private const string Page = "site/reference/databases/index.md";
    private const string AnchorLine = "<a id=\"supported-versions\"></a>";

    private const string Postgres = "PostgreSQL";
    private const string SqlServer = "SQL Server";

    private const string ServerSupported = "Server versions supported";
    private const string ServerTested = "Server versions tested";
    private const string DriverSupported = "Driver versions supported";

    private const string DefaultImagesFile = "tests/DatabaseTests/TestHarness/DefaultImages.cs";
    private const string NightlyFile = ".github/workflows/nightly.yml";
    private const string BuildFile = ".github/workflows/build.yml";
    private const string SqlServerContainerFile = "tests/DatabaseTests/TestHarness/SqlServerContainer.cs";

    // The two server minimums of the table, as its "Server versions supported" cells give them.  For the tests that
    // hold a restatement of them on another page.
    internal static (int Postgres, int SqlServer) ServerMinimums()
    {
        var table = ReadTable(File.ReadAllLines(RepositoryRoot.Path(Page)));
        return (
            FirstNumber(table, Postgres, ServerSupported).Major,
            FirstNumber(table, SqlServer, ServerSupported).Major
        );
    }

    // Fails when the PostgreSQL row's "Server versions supported" is not PostgresSession.MinimumMajorVersion, the
    // major below which the describer refuses a server.
    [Fact]
    public void PostgresServerSupported_IsTheDescribersMinimum() =>
        FirstNumber(Table(), Postgres, ServerSupported)
            .ShouldBe(
                VersionOf(PostgresSession.MinimumMajorVersion.ToString(CultureInfo.InvariantCulture)),
                $"{Page}, {Postgres}, '{ServerSupported}' should be PostgresSession.MinimumMajorVersion"
            );

    // Fails when the SQL Server row's "Server versions supported" is not the year of SqlServerErrors.MinimumRelease,
    // the release the describer's refusal names.
    [Fact]
    public void SqlServerServerSupported_IsTheYearOfMinimumRelease()
    {
        var year = YearPattern().Match(SqlServerErrors.MinimumRelease);
        year.Success.ShouldBeTrue($"SqlServerErrors.MinimumRelease, '{SqlServerErrors.MinimumRelease}', has no year");

        FirstNumber(Table(), SqlServer, ServerSupported)
            .ShouldBe(
                VersionOf(year.Value),
                $"{Page}, {SqlServer}, '{ServerSupported}' should be the year of SqlServerErrors.MinimumRelease, "
                    + $"'{SqlServerErrors.MinimumRelease}'"
            );
    }

    // Fails when the SQL Server row's "Server versions supported" is not the year of the image of
    // SqlServerContainer.ImageBuilds whose build has SqlServerSession.MinimumMajorVersion, the major below which the
    // describer refuses a server, and when no image, or images of more than one year, have that major.
    [Fact]
    public void SqlServerServerSupported_IsTheYearOfTheDescribersMinimumMajor()
    {
        var major = SqlServerSession.MinimumMajorVersion;
        var years = YearsOfMajor(ImageBuilds(), major);
        var year = years.ShouldHaveSingleItem(
            $"{SqlServerContainerFile} should have, in ImageBuilds, the image of one year whose build starts with "
                + $"'{major}.', SqlServerSession.MinimumMajorVersion; it has {years.Count}: {string.Join(", ", years)}"
        );

        FirstNumber(Table(), SqlServer, ServerSupported)
            .ShouldBe(
                VersionOf(year.ToString(CultureInfo.InvariantCulture)),
                $"{Page}, {SqlServer}, '{ServerSupported}' should be {year}, the year of the image of ImageBuilds "
                    + $"whose build has the major SqlServerSession.MinimumMajorVersion, {major}"
            );
    }

    // Fails when YearsOfMajor reads a build's major by a prefix without its dot, so that major 1 finds the build
    // 14.0, misses the image whose build has the major, or gives a year twice for two images of one release.
    [Fact]
    public void YearsOfMajor_OfMadeUpBuilds_FindsTheImagesOfThatMajor()
    {
        List<ImageBuild> builds =
        [
            new("mcr.microsoft.com/mssql/server:2019-CU1-ubuntu-20.04", 2019, "15.0.4000.1"),
            new("mcr.microsoft.com/mssql/server:2017-CU1-ubuntu-18.04", 2017, "14.0.3000.1"),
            new("mcr.microsoft.com/mssql/server:2017-CU2-ubuntu-18.04", 2017, "14.0.3100.1"),
        ];

        YearsOfMajor(builds, 14).ShouldBe([2017]);
        YearsOfMajor(builds, 15).ShouldBe([2019]);
        YearsOfMajor(builds, 1).ShouldBeEmpty();
        YearsOfMajor(builds, 13).ShouldBeEmpty();
    }

    // Fails when the PostgreSQL row's "Server versions tested" is not the set of the majors of the images the tests
    // run: DefaultImages.PostgresFloor and DefaultImages.Postgres, and each `image: postgres:` of the nightly run.
    [Fact]
    public void PostgresServerTested_IsTheMajorsOfTheTestImages() =>
        AllNumbers(Table(), Postgres, ServerTested)
            .Select(version => version.Major)
            .Distinct()
            .Order()
            .ShouldBe(
                PostgresImageMajors(),
                ignoreOrder: false,
                customMessage: $"{Page}, {Postgres}, '{ServerTested}' should be the majors of PostgresFloor and "
                    + $"Postgres in {DefaultImagesFile} and of each `image: postgres:` of {NightlyFile}"
            );

    // Fails when the SQL Server row's "Server versions tested" is not the set of the years of the images the tests
    // run: DefaultImages.SqlServer, each `image: mcr.microsoft.com/mssql/server:` of the nightly run, and the floor,
    // SQLSERVER_FLOOR_IMAGE of the build.
    [Fact]
    public void SqlServerServerTested_IsTheYearsOfTheTestImages() =>
        AllNumbers(Table(), SqlServer, ServerTested)
            .Select(version => version.Major)
            .Distinct()
            .Order()
            .ShouldBe(
                [.. SqlServerTestImages().Select(YearOfImage).Distinct().Order()],
                ignoreOrder: false,
                customMessage: $"{Page}, {SqlServer}, '{ServerTested}' should be the years of SqlServer in "
                    + $"{DefaultImagesFile}, of each `image: mcr.microsoft.com/mssql/server:` of {NightlyFile} and "
                    + $"of SQLSERVER_FLOOR_IMAGE in {BuildFile}"
            );

    // Fails when the keys of SqlServerContainer.ImageBuilds are not the images the tests run, so that a row stays for
    // an image no test runs, or an image runs that has no build recorded.
    [Fact]
    public void SqlServerImageBuilds_AreTheTestImages()
    {
        var keys = ImageBuilds().Select(build => build.Image).ToList();
        var images = SqlServerTestImages();

        keys.Except(images, StringComparer.Ordinal)
            .Select(image => $"ImageBuilds in {SqlServerContainerFile} has a row for {image}, which no test runs")
            .Concat(
                images
                    .Except(keys, StringComparer.Ordinal)
                    .Select(image => $"{image} is run by the tests and has no row of ImageBuilds")
            )
            .ShouldBeEmpty();
    }

    // Fails when the PostgreSQL row's "Driver versions supported" is not the version that ends the title of
    // SQLSRC247, SqlDiagnostics.NpgsqlTooOld, below which no query method is generated.
    [Fact]
    public void PostgresDriverSupported_IsTheVersionOfNpgsqlTooOld() =>
        FirstNumber(Table(), Postgres, DriverSupported)
            .ShouldBe(
                TitleVersion(SqlDiagnostics.NpgsqlTooOld.Title.ToString(CultureInfo.InvariantCulture)),
                $"{Page}, {Postgres}, '{DriverSupported}' should be the version that ends the title of "
                    + "SqlDiagnostics.NpgsqlTooOld"
            );

    // Fails when the SQL Server row's "Driver versions supported" is not the version that ends the title of
    // SQLSRC267, SqlDiagnostics.SqlClientTooOld, below which no query method is generated.
    [Fact]
    public void SqlServerDriverSupported_IsTheVersionOfSqlClientTooOld() =>
        FirstNumber(Table(), SqlServer, DriverSupported)
            .ShouldBe(
                TitleVersion(SqlDiagnostics.SqlClientTooOld.Title.ToString(CultureInfo.InvariantCulture)),
                $"{Page}, {SqlServer}, '{DriverSupported}' should be the version that ends the title of "
                    + "SqlDiagnostics.SqlClientTooOld"
            );

    // Fails when the least server version a row says is tested is not the one it says is the minimum supported: a
    // minimum that is never tested, or a tested version that is not supported.
    [Theory]
    [InlineData(Postgres)]
    [InlineData(SqlServer)]
    public void LeastServerTested_IsTheMinimumSupported(string database)
    {
        var table = Table();
        AllNumbers(table, database, ServerTested)
            .Min()
            .ShouldBe(
                FirstNumber(table, database, ServerSupported),
                $"{Page}, {database}: the least of '{ServerTested}' should be '{ServerSupported}'"
            );
    }

    // Fails when the reading finds no table after the anchor, no row of either database, no header the tests above
    // name, or no number in a cell they read, and when the sources find no image: a reading that finds nothing would
    // fail the tests above with less to say, or pass them.
    [Fact]
    public void Reading_OfTheTableAndTheSources_FindsEveryCellAndImage()
    {
        var table = Table();
        foreach (var header in new[] { ServerSupported, ServerTested, DriverSupported })
        {
            table.Headers.ShouldContain(header, $"{Page}#supported-versions should have the column '{header}'");
        }

        foreach (var database in new[] { Postgres, SqlServer })
        {
            table.Rows.ShouldContain(
                row => row.Count > 0 && row[0] == database,
                $"{Page}#supported-versions should have the row '{database}'"
            );
            foreach (var header in new[] { ServerSupported, ServerTested, DriverSupported })
            {
                Numbers(Cell(table, database, header))
                    .ShouldNotBeEmpty($"{Page}, {database}, '{header}' has no number");
            }
        }

        PostgresDefaultImageMajors().Count.ShouldBe(2, $"{DefaultImagesFile} should name PostgresFloor and Postgres");
        PostgresNightlyMajors().ShouldNotBeEmpty($"{NightlyFile} should name the images of the nightly run");

        Images(DefaultImagesFile, SqlServerConstantPattern())
            .Count.ShouldBe(1, $"{DefaultImagesFile} should name SqlServer");
        Images(NightlyFile, NightlySqlServerPattern())
            .ShouldNotBeEmpty($"{NightlyFile} should name the SQL Server images of the nightly run");
        Images(BuildFile, SqlServerFloorPattern()).Count.ShouldBe(1, $"{BuildFile} should name SQLSERVER_FLOOR_IMAGE");

        var text = File.ReadAllText(RepositoryRoot.Path(SqlServerContainerFile));
        ImageBuilds()
            .Count.ShouldBe(
                text.Split("[\"mcr.microsoft.com/mssql/server:").Length - 1,
                $"{SqlServerContainerFile}: each key of ImageBuilds should be read with its build"
            );
        ImageBuilds().ShouldNotBeEmpty($"{SqlServerContainerFile} should name the images of ImageBuilds");
    }

    // Fails when ReadTable reads a table before the anchor or after the first one, keeps the delimiter row, or does not
    // find a row by its first cell and a cell by its header; or when Numbers does not read `6.1` as one number.
    [Fact]
    public void ReadTable_OfMadeUpLines_FindsTheFirstTableAfterTheAnchor()
    {
        var table = ReadTable([
            "| Database | Server versions supported |",
            "|----|----|",
            "| PostgreSQL | 9 or later |",
            "",
            AnchorLine,
            "",
            "## Supported versions",
            "",
            "| Database | Server versions supported | Driver versions supported |",
            "|----|----|----|",
            "| PostgreSQL | 14 or later | 8 or later |",
            "| SQL Server | 2017 or later | 6.1 or later |",
            "",
            "| Database | Server versions supported |",
            "|----|----|",
            "| PostgreSQL | 15 or later |",
        ]);

        table.Headers.ShouldBe(["Database", "Server versions supported", "Driver versions supported"]);
        table.Rows.Count.ShouldBe(2);
        Cell(table, Postgres, ServerSupported).ShouldBe("14 or later");
        Numbers(Cell(table, SqlServer, DriverSupported)).ShouldBe([VersionOf("6.1")]);
        Numbers("14, 15, 16").ShouldBe([VersionOf("14"), VersionOf("15"), VersionOf("16")]);
        VersionOf("8").ShouldBe(VersionOf("8.0"));
    }

    // Fails when ReadTable finds a table in lines that have no anchor.
    [Fact]
    public void ReadTable_WithoutTheAnchor_FindsNothing() =>
        ReadTable(["| Database | Server versions supported |", "|----|----|", "| PostgreSQL | 14 or later |"])
            .Headers.ShouldBeEmpty();

    private static SupportedTable Table() => ReadTable(File.ReadAllLines(RepositoryRoot.Path(Page)));

    // The first Markdown table after the line of the anchor: its headers, and its rows but the delimiter row, each cell
    // trimmed.  Empty when there is no anchor or no table after it.
    internal static SupportedTable ReadTable(IReadOnlyList<string> lines)
    {
        var rows = lines
            .SkipWhile(line => line.Trim() != AnchorLine)
            .SkipWhile(line => !line.TrimStart().StartsWith('|'))
            .TakeWhile(line => line.TrimStart().StartsWith('|'))
            .Select(Cells)
            .ToList();

        return rows.Count == 0
            ? new SupportedTable([], [])
            : new SupportedTable(rows[0], [.. rows.Skip(1).Where(row => !row.All(DelimiterPattern().IsMatch))]);
    }

    private static IReadOnlyList<string> Cells(string row) =>
        [.. row.Trim().Trim('|').Split('|').Select(cell => cell.Trim())];

    // The cell of the row whose first cell is `database`, in the column whose header is `header`.
    private static string Cell(SupportedTable table, string database, string header)
    {
        var column = table.Headers.ToList().IndexOf(header);
        column.ShouldBeGreaterThanOrEqualTo(0, $"{Page}#supported-versions has no column '{header}'");

        var row = table.Rows.FirstOrDefault(row => row.Count > 0 && row[0] == database);
        _ = row.ShouldNotBeNull($"{Page}#supported-versions has no row '{database}'");
        row.Count.ShouldBeGreaterThan(column, $"{Page}, {database} has no cell for '{header}'");

        return row[column];
    }

    // Every number of the text, in order, each with its dots: `6.1` is one number.
    private static List<Version> Numbers(string text) =>
        [.. NumberPattern().Matches(text).Select(match => VersionOf(match.Value))];

    private static Version FirstNumber(SupportedTable table, string database, string header)
    {
        var cell = Cell(table, database, header);
        var numbers = Numbers(cell);
        numbers.ShouldNotBeEmpty($"{Page}, {database}, '{header}' is '{cell}', which has no number");
        return numbers[0];
    }

    private static List<Version> AllNumbers(SupportedTable table, string database, string header)
    {
        var cell = Cell(table, database, header);
        var numbers = Numbers(cell);
        numbers.ShouldNotBeEmpty($"{Page}, {database}, '{header}' is '{cell}', which has no number");
        return numbers;
    }

    // A version of up to four parts, the missing ones zero, so that 8 and 8.0 are equal.
    private static Version VersionOf(string text)
    {
        var parts = text.Split('.').Select(part => int.Parse(part, CultureInfo.InvariantCulture)).ToList();
        parts.Count.ShouldBeLessThanOrEqualTo(4, $"'{text}' has more than four parts");
        while (parts.Count < 4)
        {
            parts.Add(0);
        }

        return new Version(parts[0], parts[1], parts[2], parts[3]);
    }

    // The version that ends a diagnostic's title, "... is older than 8.0".
    private static Version TitleVersion(string title)
    {
        var match = TitleVersionPattern().Match(title);
        match.Success.ShouldBeTrue($"the title '{title}' does not end with a version");
        return VersionOf(match.Groups[1].Value);
    }

    private static List<int> PostgresImageMajors() =>
        [.. PostgresDefaultImageMajors().Concat(PostgresNightlyMajors()).Distinct().Order()];

    // The majors of DefaultImages.PostgresFloor and DefaultImages.Postgres.
    private static List<int> PostgresDefaultImageMajors() =>
        [
            .. PostgresConstantPattern()
                .Matches(File.ReadAllText(RepositoryRoot.Path(DefaultImagesFile)))
                .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)),
        ];

    private static List<int> PostgresNightlyMajors() =>
        [
            .. File.ReadAllLines(RepositoryRoot.Path(NightlyFile))
                .Select(line => NightlyPostgresPattern().Match(line))
                .Where(match => match.Success)
                .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)),
        ];

    // Each row of SqlServerContainer.ImageBuilds: its image, the image's year and the build its server reports.
    private static List<ImageBuild> ImageBuilds() =>
        [
            .. ImageBuildsRowPattern()
                .Matches(File.ReadAllText(RepositoryRoot.Path(SqlServerContainerFile)))
                .Select(match => new ImageBuild(
                    match.Groups[1].Value,
                    YearOfImage(match.Groups[1].Value),
                    match.Groups[2].Value
                )),
        ];

    // The years of the images whose build has the major: its first part, read up to its dot.
    private static List<int> YearsOfMajor(IReadOnlyList<ImageBuild> builds, int major) =>
        [
            .. builds
                .Where(build => build.Build.StartsWith($"{major}.", StringComparison.Ordinal))
                .Select(build => build.Year)
                .Distinct()
                .Order(),
        ];

    // The SQL Server images the tests run: DefaultImages.SqlServer, the images of the nightly run and the floor of the
    // build.
    private static List<string> SqlServerTestImages() =>
        [
            .. Images(DefaultImagesFile, SqlServerConstantPattern())
                .Concat(Images(NightlyFile, NightlySqlServerPattern()))
                .Concat(Images(BuildFile, SqlServerFloorPattern()))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

    // The first group of each line of the file that the pattern matches.
    private static List<string> Images(string file, Regex pattern) =>
        [
            .. File.ReadAllLines(RepositoryRoot.Path(file))
                .Select(line => pattern.Match(line))
                .Where(match => match.Success)
                .Select(match => match.Groups[1].Value),
        ];

    private static int YearOfImage(string image)
    {
        var match = SqlServerImageYearPattern().Match(image);
        match.Success.ShouldBeTrue($"the image '{image}' has no year after its colon");
        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    internal sealed record SupportedTable(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

    private sealed record ImageBuild(string Image, int Year, string Build);

    [GeneratedRegex(@"\d+(?:\.\d+)*")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^:?-+:?$")]
    private static partial Regex DelimiterPattern();

    [GeneratedRegex(@"\b\d{4}$")]
    private static partial Regex YearPattern();

    [GeneratedRegex(@"(\d+(?:\.\d+)*)$")]
    private static partial Regex TitleVersionPattern();

    // `const string Postgres = "postgres:18.6"` and `const string PostgresFloor = "postgres:14.24"`.
    [GeneratedRegex(@"const string (?:Postgres|PostgresFloor) = ""postgres:(\d+)\.\d+""")]
    private static partial Regex PostgresConstantPattern();

    // `image: postgres:15.19`, an entry of the nightly run's matrix.
    [GeneratedRegex(@"^\s*image:\s*postgres:(\d+)\.\d+\s*$")]
    private static partial Regex NightlyPostgresPattern();

    // `["mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04"] = "17.0.5015.4"`, a row of ImageBuilds.
    [GeneratedRegex(@"\[""(mcr\.microsoft\.com/mssql/server:[^""]+)""\]\s*=\s*""([^""]+)""")]
    private static partial Regex ImageBuildsRowPattern();

    // `const string SqlServer = "mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04"`.
    [GeneratedRegex(@"const string SqlServer = ""(mcr\.microsoft\.com/mssql/server:[^""]+)""")]
    private static partial Regex SqlServerConstantPattern();

    // `image: mcr.microsoft.com/mssql/server:2019-CU32-GDR12-ubuntu-20.04`, an entry of the nightly run's matrix.
    [GeneratedRegex(@"^\s*image:\s*(mcr\.microsoft\.com/mssql/server:\S+)\s*$")]
    private static partial Regex NightlySqlServerPattern();

    // `SQLSERVER_FLOOR_IMAGE: mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`, of the build.
    [GeneratedRegex(@"^\s*SQLSERVER_FLOOR_IMAGE:\s*(mcr\.microsoft\.com/mssql/server:\S+)\s*$")]
    private static partial Regex SqlServerFloorPattern();

    // The year of `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.
    [GeneratedRegex(@"^mcr\.microsoft\.com/mssql/server:(\d{4})-")]
    private static partial Regex SqlServerImageYearPattern();
}
