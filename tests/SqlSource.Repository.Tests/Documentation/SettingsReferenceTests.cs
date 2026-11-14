using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;
using SqlSource.Parsing.Markers;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Repository.Tests.Documentation;

// The table of site/reference/settings.md#all-settings, the matrix, is where a user looks up every place a setting can
// be written.  These tests fail when a key of the settings file's schema, a marker that is a setting, or a property of
// this test's list of MSBuild properties has no row of the matrix or has two; when its key column holds a name the
// schema does not, or its MSBuild property column a property the list does not; when its file header and query columns
// disagree with where the parser lets a marker stand; or when an <a id="setting-..."> of the manual is linked by no row
// or by two.  They also fail when a marker has no row of the marker index of site/reference/sql-files.md, a generator
// parameter no row, or no section with an h3 that names it, of site/reference/generator-parameters.md, and a property
// of the list no row of site/reference/msbuild.md#properties, or no read in the package's build files.  They check
// neither the defaults nor the words of a row.  A finder that finds nothing fails too, so that a change to how a page
// is read cannot pass them all by finding no row.
public sealed partial class SettingsReferenceTests
{
    private const string SettingsPage = "site/reference/settings.md";
    private const string SqlFilesPage = "site/reference/sql-files.md";
    private const string GeneratorParametersPage = "site/reference/generator-parameters.md";
    private const string MsBuildPage = "site/reference/msbuild.md";

    private const string SettingColumn = "Setting";
    private const string KeyColumn = "`sqlsource.json` key";
    private const string MarkerColumn = "Marker";
    private const string FileHeaderColumn = "In the file header";
    private const string InsideAQueryColumn = "Inside a query";
    private const string MsBuildColumn = "MSBuild property";

    // The markers that describe one query's own content, its name, its summary, its parameters and its tokens: they
    // are not settings, so the matrix has no row for them.
    private static readonly SqlMarkerKind[] NotSettings =
    [
        SqlMarkerKind.Name,
        SqlMarkerKind.Summary,
        SqlMarkerKind.Param,
        SqlMarkerKind.Token,
        SqlMarkerKind.TokenIgnore,
    ];

    // The MSBuild properties a project sets for SqlSource.  The properties the package sets for itself are left out on
    // purpose: a user does not set them, so neither the matrix nor the table of #properties lists them.
    private static readonly string[] ProjectProperties =
    [
        "SqlSourceOnline",
        "SqlSourceEnvFile",
        "SqlSourceIncludeFiles",
        "SqlSourcePublicExceptions",
        "SqlSourceRepositoryInterfaces",
    ];

    [Fact]
    public void SchemaKeys_EachOne_IsInTheKeyCellOfOneRow()
    {
        using var schema = JsonDocument.Parse(
            File.ReadAllText(RepositoryRoot.Path("schemas", "settings-v1.schema.json"))
        );
        var keys = schema
            .RootElement.GetProperty("properties")
            .EnumerateObject()
            .Select(static property => property.Name)
            .Where(static name => name is not "$schema" and not "root")
            .ToList();
        keys.ShouldNotBeEmpty();
        var cells = Matrix().Select(static row => Code(row[KeyColumn])).ToList();

        var notOnce = NotInExactlyOneRow(keys, name => cells.Count(cell => cell.Contains(name)));
        var unknown = cells.SelectMany(static cell => cell).Where(name => !keys.Contains(name)).ToList();

        notOnce.ShouldBeEmpty("Each key of the settings file's schema is in the key cell of exactly one row");
        unknown.ShouldBeEmpty("The key column of the matrix holds only keys of the settings file's schema");
    }

    [Fact]
    public void Markers_EachOne_HasARowOfTheMarkerIndex()
    {
        var kinds = Enum.GetValues<SqlMarkerKind>();
        kinds.ShouldNotBeEmpty();
        var cells = Table(PageLines(SqlFilesPage), SqlFilesPage, "marker-index").Rows.Select(static row => row[0]);

        var missing = kinds
            .Select(static kind => Marker(kind))
            .Where(marker => !cells.Any(cell => cell.Contains(marker, StringComparison.Ordinal)))
            .ToList();

        missing.ShouldBeEmpty($"Each marker has a row of the marker index of {SqlFilesPage}");
    }

    [Fact]
    public void Markers_EachSetting_IsInTheMarkerCellOfOneRow()
    {
        var markers = Enum.GetValues<SqlMarkerKind>().Except(NotSettings).Select(static kind => Marker(kind)).ToList();
        markers.ShouldNotBeEmpty();
        var cells = Matrix().Select(static row => row[MarkerColumn]).ToList();

        var notOnce = NotInExactlyOneRow(
            markers,
            marker => cells.Count(cell => cell.Contains(marker, StringComparison.Ordinal))
        );

        notOnce.ShouldBeEmpty("Each marker that is a setting is in the marker cell of exactly one row");
    }

    // The parser reads -- dialect: itself, before the markers of a scope, so SqlMarkerScope names it in neither method.
    [Fact]
    public void Markers_FileHeaderAndQueryCells_SayWhereTheParserAllowsThem()
    {
        var kinds = Enum.GetValues<SqlMarkerKind>().ToDictionary(static kind => SqlMarkerReader.WordOf(kind));
        var checkedMarkers = 0;
        var wrong = new List<string>();

        foreach (var row in Matrix())
        {
            var words = MarkerWord().Matches(row[MarkerColumn]).Select(static match => match.Groups["word"].Value);
            foreach (var word in words)
            {
                if (!kinds.TryGetValue(word, out var kind))
                {
                    wrong.Add($"-- {word}: is not a marker");
                    continue;
                }

                if (kind == SqlMarkerKind.Dialect)
                {
                    continue;
                }

                checkedMarkers++;
                wrong.AddRange([
                    .. Disagreement(
                        word,
                        FileHeaderColumn,
                        row[FileHeaderColumn],
                        SqlMarkerScope.IsAllowedInPreamble(kind)
                    ),
                    .. Disagreement(
                        word,
                        InsideAQueryColumn,
                        row[InsideAQueryColumn],
                        SqlMarkerScope.IsAllowedInQuery(kind)
                    ),
                ]);
            }
        }

        checkedMarkers.ShouldBeGreaterThan(0);
        wrong.ShouldBeEmpty("The file header and query cells of each marker's row say what the parser allows");
    }

    [Fact]
    public void GeneratorParameterNames_All_AreTheNamesOfTheDefinitions() =>
        GeneratorParameterNames.All.ShouldBe(
            GeneratorParameterList.Definitions.Select(static definition => definition.Name)
        );

    [Fact]
    public void GeneratorParameters_EachOne_HasARowAndASection()
    {
        var lines = PageLines(GeneratorParametersPage);
        var cells = Table(lines, GeneratorParametersPage, "setting-generator-parameters")
            .Rows.Select(static row => row[0]);

        var noRow = GeneratorParameterNames
            .All.Where(name => !cells.Any(cell => cell.Contains($"[`{name}`](#gp-{name})", StringComparison.Ordinal)))
            .ToList();
        var noSection = GeneratorParameterNames.All.Where(name => !HasSection(lines, name)).ToList();

        noRow.ShouldBeEmpty($"Each generator parameter has a row of the table of {GeneratorParametersPage}");
        noSection.ShouldBeEmpty(
            $"Each generator parameter has an <a id=\"gp-<name>\"> of {GeneratorParametersPage}, and an h3 under it "
                + "that names it"
        );
    }

    // Whether the page has the line <a id="gp-<name>">, and the first line after it that is neither blank nor another
    // anchor is an h3 that holds the name in code font: sort-input and sort-output share one heading, with both anchors
    // above it.
    private static bool HasSection(string[] lines, string name)
    {
        var at = Array.IndexOf(lines, $"<a id=\"gp-{name}\"></a>");
        if (at < 0)
        {
            return false;
        }

        var next = lines
            .Skip(at + 1)
            .FirstOrDefault(static line =>
                line.Trim().Length > 0 && !line.StartsWith("<a id=", StringComparison.Ordinal)
            );
        return next is not null
            && next.StartsWith("### ", StringComparison.Ordinal)
            && next.Contains($"`{name}`", StringComparison.Ordinal);
    }

    // Each name of the list is read by the package's build files, as $(Name), $(Name.Trim()) or a
    // CompilerVisibleProperty, so that a property renamed there fails.  A new property there fails nothing: the list is
    // this test's, and the documentation overhaul left out on purpose a classification of every property the build
    // reads.
    [Fact]
    public void ProjectProperties_EachOne_IsReadByTheBuildFiles()
    {
        var build = string.Concat(
            File.ReadAllText(RepositoryRoot.Path("src", "SqlSource", "build", "SqlSource.props")),
            File.ReadAllText(RepositoryRoot.Path("src", "SqlSource", "build", "SqlSource.targets"))
        );

        var unread = ProjectProperties
            .Where(name =>
                !build.Contains($"$({name})", StringComparison.Ordinal)
                && !build.Contains($"$({name}.", StringComparison.Ordinal)
                && !build.Contains($"<CompilerVisibleProperty Include=\"{name}\"", StringComparison.Ordinal)
            )
            .ToList();

        unread.ShouldBeEmpty("Each property of the list is read by SqlSource.props or SqlSource.targets");
    }

    [Fact]
    public void ProjectProperties_EachOne_HasARowOfTheMsBuildPage()
    {
        var cells = Table(PageLines(MsBuildPage), MsBuildPage, "properties").Rows.Select(static row => row[0]);

        var missing = ProjectProperties.Where(name => !cells.Contains($"`{name}`")).ToList();

        missing.ShouldBeEmpty($"Each property has a row of the table of {MsBuildPage}#properties");
    }

    [Fact]
    public void ProjectProperties_EachOne_IsInTheMsBuildCellOfOneRow()
    {
        var cells = Matrix().Select(static row => Code(row[MsBuildColumn])).ToList();

        var notOnce = NotInExactlyOneRow(ProjectProperties, name => cells.Count(cell => cell.Contains(name)));
        var unknown = cells
            .SelectMany(static cell => cell)
            .Where(static name => !ProjectProperties.Contains(name))
            .ToList();

        notOnce.ShouldBeEmpty("Each property is in the MSBuild property cell of exactly one row");
        unknown.ShouldBeEmpty(
            "The MSBuild property column of the matrix holds only the properties of this test's list"
        );
    }

    [Fact]
    public void SettingAnchors_EachOne_IsLinkedByOneRow()
    {
        var anchors = UserDocs
            .SitePages.SelectMany(static page =>
                PageLines(page)
                    .Select(static line => SettingAnchor().Match(line))
                    .Where(static match => match.Success)
                    .Select(match => $"{page}#{match.Groups["anchor"].Value}")
            )
            .ToList();
        anchors.ShouldNotBeEmpty();
        var links = Matrix()
            .Select(static row =>
                Link()
                    .Matches(row[SettingColumn])
                    .Select(static match => UserDocs.Resolve(SettingsPage, match.Groups["target"].Value))
                    .OfType<(string Path, string Anchor)>()
                    .Select(static target => $"{target.Path}#{target.Anchor}")
                    .ToList()
            )
            .ToList();

        var notOnce = NotInExactlyOneRow(anchors, anchor => links.Count(row => row.Contains(anchor)));

        notOnce.ShouldBeEmpty(
            "Each <a id=\"setting-...\"> of the manual is linked by the setting cell of exactly one row"
        );
    }

    private static string Marker(SqlMarkerKind kind)
    {
        var word = SqlMarkerReader.WordOf(kind);
        word.ShouldNotBeEmpty(kind.ToString());
        return $"`-- {word}:`";
    }

    // The names that are in no row, or in more than one, with the number of rows that hold each.
    private static List<string> NotInExactlyOneRow(IEnumerable<string> names, Func<string, int> rows) =>
        [
            .. names
                .Select(name => (Name: name, Rows: rows(name)))
                .Where(static name => name.Rows != 1)
                .Select(static name => $"{name.Name} ({name.Rows} rows)"),
        ];

    // The cell's disagreement with the parser, if it has one: it starts with "yes" where the marker may stand, and "no"
    // where it may not.
    private static IEnumerable<string> Disagreement(string word, string column, string cell, bool allowed)
    {
        var expected = allowed ? "yes" : "no";
        return cell.StartsWith(expected, StringComparison.Ordinal)
            ? []
            : [$"-- {word}: has \"{cell}\" under {column}, and the parser says {expected}"];
    }

    // The names between backticks of a cell.
    private static List<string> Code(string cell) =>
        [.. CodeSpan().Matches(cell).Select(static match => match.Groups["code"].Value)];

    private static string[] PageLines(string page) => File.ReadAllLines(RepositoryRoot.Path(page));

    // The rows of the matrix, each cell under the text of its column's header.
    private static List<Dictionary<string, string>> Matrix()
    {
        var (header, rows) = Table(PageLines(SettingsPage), SettingsPage, "all-settings");
        string[] columns =
        [
            SettingColumn,
            KeyColumn,
            MarkerColumn,
            FileHeaderColumn,
            InsideAQueryColumn,
            MsBuildColumn,
        ];
        columns.Where(column => !header.Contains(column)).ShouldBeEmpty($"The columns of {SettingsPage}#all-settings");

        return
        [
            .. rows.Select(row =>
                header
                    .Zip(row)
                    .ToDictionary(static cell => cell.First, static cell => cell.Second, StringComparer.Ordinal)
            ),
        ];
    }

    // The first table after <a id="anchor"></a>: its header's cells, and the cells of each row under the rule line.
    private static (List<string> Header, List<List<string>> Rows) Table(string[] lines, string page, string anchor)
    {
        var start = Array.IndexOf(lines, $"<a id=\"{anchor}\"></a>");
        start.ShouldBeGreaterThanOrEqualTo(0, $"{page} has no anchor {anchor}");
        var table = lines
            .Skip(start + 1)
            .SkipWhile(static line => !line.StartsWith('|'))
            .TakeWhile(static line => line.StartsWith('|'))
            .ToList();
        table.Count.ShouldBeGreaterThan(2, $"{page} has no table with rows under the anchor {anchor}");

        return (Cells(table[0]), [.. table.Skip(2).Select(Cells)]);
    }

    // A row's cells, trimmed, between its first pipe and its last.  A cell ends at a pipe that no backslash escapes.
    private static List<string> Cells(string line)
    {
        var row = line.Trim();
        row.ShouldEndWith("|", Case.Sensitive, $"A row of a table ends with a pipe: {line}");
        return [.. CellSeparator().Split(row[1..^1]).Select(static cell => cell.Trim())];
    }

    [GeneratedRegex(@"(?<!\\)\|")]
    private static partial Regex CellSeparator();

    [GeneratedRegex("`(?<code>[^`]+)`")]
    private static partial Regex CodeSpan();

    [GeneratedRegex("`-- (?<word>[a-z-]+):`")]
    private static partial Regex MarkerWord();

    [GeneratedRegex(@"\]\((?<target>[^)\s]+)\)")]
    private static partial Regex Link();

    [GeneratedRegex("<a id=\"(?<anchor>setting-[^\"]+)\"></a>")]
    private static partial Regex SettingAnchor();
}
