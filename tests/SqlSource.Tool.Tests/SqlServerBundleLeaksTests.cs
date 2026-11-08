using System;
using Shouldly;
using SqlSource.Tool.Testing.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests;

// SQL Server's search for what a renamed bundle still holds of the user's, Find, on raw bundles of SQL Server's
// fixtures, which hold all of it, so that a search that finds nothing is known to look; and its reading of plain words
// and quoted names, which CorpusWordsTests shares.
public sealed class SqlServerBundleLeaksTests
{
    [Fact]
    public void Find_RawBundle_FindsTheUsersNamesInEveryFile()
    {
        var raw = SqlServerRenamings.Raw("select-left-join");

        var found = SqlServerBundleLeaks.Find(raw, SqlServerRenamings.Part, raw, raw.Query);

        found.ShouldContain("users in query.sql");
        found.ShouldContain("users in capture.json call 2 'columns' answer.rows[].sourceTable");
        found.ShouldContain("UsersWithOrders in expected.json key");
        found.ShouldContain("users in expected.json columns[].origin.table");
    }

    [Fact]
    public void Find_RawBundle_LeavesOutWhatIsFixed()
    {
        var raw = SqlServerRenamings.Raw("select-left-join");

        var found = SqlServerBundleLeaks.Find(raw, SqlServerRenamings.Part, raw, raw.Query);

        found.ShouldNotContain(place => place.Contains("serverVersion") || place.Contains("productVersion"));
    }

    [Fact]
    public void Find_DeclaredParameterOnlyInAMarker_IsFound()
    {
        var raw = SqlServerRenamings.Raw("delete-declared-only");

        var found = SqlServerBundleLeaks.Find(raw, SqlServerRenamings.Part, raw, raw.Query);

        found.ShouldContain("tenant in query.sql");
        found.ShouldContain("PurgeOrders in query.sql");
    }

    [Fact]
    public void Find_MarkersWordAndShape_AreNoLeak()
    {
        var raw = SqlServerRenamings.Raw("select-left-join") with { Query = "-- name: q_1 -> many\nSELECT 1 AS a;\n" };

        var found = SqlServerBundleLeaks.Find(raw, SqlServerRenamings.Part, raw, "SELECT name, many FROM t;");

        SqlServerRenamings.Part.IsEngineWord("many", quoted: false).ShouldBeFalse();
        found.ShouldNotContain(place => place.EndsWith("in query.sql"));
    }

    [Fact]
    public void Find_LiteralOfOneLetter_IsFoundAsAWholeTokenOnly()
    {
        var raw = SqlServerRenamings.Raw("select-left-join") with
        {
            Query = "-- name: q_1\nSELECT 'xml' AS a, 'x' AS b;\n",
        };

        var found = SqlServerBundleLeaks.Find(raw, SqlServerRenamings.Part, raw, "SELECT N'x';");

        found.ShouldBe(["x in query.sql"]);
    }

    [Fact]
    public void Find_LiteralOfTwoWords_IsFoundAnywhereInAString()
    {
        var raw = SqlServerRenamings.Raw("select-left-join") with
        {
            Query = "-- name: q_1\nSELECT 'big red dog' AS a;\n",
        };

        var found = SqlServerBundleLeaks.Find(raw, SqlServerRenamings.Part, raw, "SELECT 'red dog';");

        found.ShouldBe(["'red dog' in query.sql"]);
    }

    [Fact]
    public void Find_LiteralWithNoToken_IsNoLeak()
    {
        var raw = SqlServerRenamings.Raw("select-left-join") with
        {
            Query = "-- name: q_1\nSELECT a, b, 'ab, cd' AS c;\n",
        };

        var found = SqlServerBundleLeaks.Find(raw, SqlServerRenamings.Part, raw, "SELECT ', ', 'ab, cd';");

        found.ShouldBe(["'ab, cd' in query.sql"]);
    }

    [Fact]
    public void EngineRowNames_OfAViewOfSys_AreTheEnginesNames()
    {
        var raw = SqlServerRenamings.Raw("select-other-database");

        var names = SqlServerBundleLeaks.EngineRowNames(raw, SqlServerRenamings.Part);

        names.ShouldContain("databases");
        names.ShouldContain("database_id");
        names.ShouldNotContain("sysjobs");
        names.ShouldNotContain("JobsAndDatabases");
    }

    // A row whose name is an alias of the user's is no row of the engine's, so none of its names is kept.
    [Fact]
    public void EngineRowNames_OfAnAliasOfAColumnOfSys_LeaveOutItsRow()
    {
        var raw = SqlServerRenamings.Raw("select-other-database");
        raw = raw with
        {
            Capture = raw.Capture.Replace(
                "\"name\": \"database_id\"",
                "\"name\": \"acme_payroll_db\"",
                StringComparison.Ordinal
            ),
        };

        var names = SqlServerBundleLeaks.EngineRowNames(raw, SqlServerRenamings.Part);

        raw.Capture.ShouldContain("acme_payroll_db");
        names.ShouldNotContain("acme_payroll_db");
        names.ShouldNotContain("database_id");
        names.ShouldNotContain("databases");
    }

    [Fact]
    public void PlainWordsOf_SystemFunction_IsWholeWhenKeptAndNoneWhenNot()
    {
        const string Text = "SELECT @@ROWCOUNT, @x, #t";

        var kept = SqlServerBundleLeaks.PlainWordsOf(
            Text,
            0,
            Text.Length,
            quoteFollows: false,
            SqlServerRenamings.Part,
            keepsSystemFunctions: true
        );
        var left = SqlServerBundleLeaks.PlainWordsOf(
            Text,
            0,
            Text.Length,
            quoteFollows: false,
            SqlServerRenamings.Part,
            keepsSystemFunctions: false
        );

        kept.ShouldBe(["SELECT", "@@ROWCOUNT", "x", "t"]);
        left.ShouldBe(["SELECT", "x", "t"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PlainWordsOf_AtOrHashAlone_YieldsNoWord(bool keepsSystemFunctions)
    {
        const string Text = "@ # a";

        var words = SqlServerBundleLeaks.PlainWordsOf(
            Text,
            0,
            Text.Length,
            quoteFollows: false,
            SqlServerRenamings.Part,
            keepsSystemFunctions
        );

        words.ShouldBe(["a"]);
    }

    [Theory]
    [InlineData("[Full Name?]", "Full Name")]
    [InlineData("[a]]b]", "a]b")]
    [InlineData("\"x!\"", "x")]
    [InlineData("[?]", "?")]
    [InlineData("'lit'", null)]
    public void QuotedNameOf_Region_IsItsName(string quoted, string? name) =>
        SqlServerBundleLeaks.QuotedNameOf(quoted).ShouldBe(name);
}
