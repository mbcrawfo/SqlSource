using System.Linq;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Testing.Bundles;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests;

// The search for what a renamed bundle still holds of the user's, run on raw bundles, which hold all of it, so that a
// search that finds nothing is known to look.
public sealed class BundleLeaksTests
{
    private const string Engine = "mssql";

    private static IEngineRenaming Part => EngineRenamings.For(DatabaseEngine.SqlServer).ShouldNotBeNull();

    private static Bundle RawOf(string name) =>
        RawBundles.OfFixture(FixtureCases.Of(Engine).Single(found => found.Name == name));

    [Fact]
    public void Find_RawBundle_FindsTheUsersNamesInEveryFile()
    {
        var raw = RawOf("select-left-join");

        var found = BundleLeaks.Find(raw, Part, BundleLeaks.EngineRowNames(raw, Part), raw.Query);

        found.ShouldContain("users in query.sql");
        found.ShouldContain("users in capture.json call 2 'columns' answer.rows[].sourceTable");
        found.ShouldContain("UsersWithOrders in expected.json key");
        found.ShouldContain("users in expected.json columns[].origin.table");
    }

    [Fact]
    public void Find_RawBundle_LeavesOutWhatIsFixed()
    {
        var raw = RawOf("select-left-join");

        var found = BundleLeaks.Find(raw, Part, BundleLeaks.EngineRowNames(raw, Part), raw.Query);

        found.ShouldNotContain(place => place.Contains("serverVersion") || place.Contains("productVersion"));
    }

    [Fact]
    public void Find_DeclaredParameterOnlyInAMarker_IsFound()
    {
        var raw = RawOf("delete-declared-only");

        var found = BundleLeaks.Find(raw, Part, BundleLeaks.EngineRowNames(raw, Part), raw.Query);

        found.ShouldContain("tenant in query.sql");
        found.ShouldContain("PurgeOrders in query.sql");
    }

    [Fact]
    public void Find_MarkersWordAndShape_AreNoLeak()
    {
        var raw = RawOf("select-left-join") with { Query = "-- name: q_1 -> many\nSELECT 1 AS a;\n" };

        var found = BundleLeaks.Find(raw, Part, BundleLeaks.EngineRowNames(raw, Part), "SELECT name, many FROM t;");

        Part.IsEngineWord("many", quoted: false).ShouldBeFalse();
        found.ShouldNotContain(place => place.EndsWith("in query.sql"));
    }

    [Fact]
    public void Find_LiteralOfOneLetter_IsFoundAsAWholeTokenOnly()
    {
        var raw = RawOf("select-left-join") with { Query = "-- name: q_1\nSELECT 'xml' AS a, 'x' AS b;\n" };

        var found = BundleLeaks.Find(raw, Part, BundleLeaks.EngineRowNames(raw, Part), "SELECT N'x';");

        found.ShouldBe(["x in query.sql"]);
    }

    [Fact]
    public void Find_LiteralOfTwoWords_IsFoundAnywhereInAString()
    {
        var raw = RawOf("select-left-join") with { Query = "-- name: q_1\nSELECT 'big red dog' AS a;\n" };

        var found = BundleLeaks.Find(raw, Part, BundleLeaks.EngineRowNames(raw, Part), "SELECT 'red dog';");

        found.ShouldBe(["'red dog' in query.sql"]);
    }

    [Fact]
    public void Find_LiteralWithNoToken_IsNoLeak()
    {
        var raw = RawOf("select-left-join") with { Query = "-- name: q_1\nSELECT a, b, 'ab, cd' AS c;\n" };

        var found = BundleLeaks.Find(raw, Part, BundleLeaks.EngineRowNames(raw, Part), "SELECT ', ', 'ab, cd';");

        found.ShouldBe(["'ab, cd' in query.sql"]);
    }

    [Fact]
    public void EngineRowNames_OfAViewOfSys_AreTheEnginesNames()
    {
        var raw = RawOf("select-other-database");

        var names = BundleLeaks.EngineRowNames(raw, Part);

        names.ShouldContain("databases");
        names.ShouldContain("database_id");
        names.ShouldNotContain("sysjobs");
        names.ShouldNotContain("JobsAndDatabases");
    }

    [Fact]
    public void ProvenanceOf_Entry_IsItsPlanTableMatchAndSources()
    {
        var raw = RawOf("select-left-join");

        var provenance = BundleLeaks.ProvenanceOf(raw.Expected.ShouldNotBeNull());

        provenance.ShouldBe([
            ("plan", "skipped"),
            ("tableMatch", "several-tables"),
            ("columns[0].nullableSource", "server"),
            ("columns[1].nullableSource", "server"),
            ("columns[2].nullableSource", "server"),
            ("columns[3].nullableSource", "server"),
        ]);
    }
}
