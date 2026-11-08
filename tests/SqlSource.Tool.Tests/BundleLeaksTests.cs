using System;
using Shouldly;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Testing.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests;

// The searches for what a renamed bundle still holds of the user's: Of on bundles made for each test, and Find on raw
// bundles of SQL Server's fixtures, which hold all of it, so that a search that finds nothing is known to look.
public sealed class BundleLeaksTests
{
    // The source that Of is run on.
    private const string Source =
        "-- name: FindUser\nSELECT u.id, \"Full Name!\" FROM users u WHERE u.status = 'active' AND u.id = @id;\n";

    private const string QuotedOrder = "SELECT 1 AS \"Order\" ORDER BY 1;";

    // The keywords of the sources, folded as PostgreSQL folds them.
    private static bool IsEngineWord(string word, bool quoted) =>
        PostgresWords.Fold(word, quoted) is "select" or "from" or "where" or "and" or "as" or "order" or "by";

    private static Bundle BundleOf(string query, string capture) => new(null, query, capture, null, []);

    [Fact]
    public void Of_NothingLeft_IsEmpty() =>
        BundleLeaks
            .Of(
                BundleOf(
                    "SELECT a_1 FROM t_2",
                    /*lang=json,strict*/
                    """{"calls": [{"answer": {"relkind": "u"}}]}"""
                ),
                [Source],
                IsEngineWord
            )
            .ShouldBeEmpty();

    [Fact]
    public void Of_NamesAndLiteralLeft_NamesEachAndItsFile() =>
        BundleLeaks
            .Of(
                BundleOf(
                    "-- name: FindUser\nSELECT Id FROM t_2",
                    /*lang=json,strict*/
                    """
                    {
                        "calls": [
                            {"answer": {"plan": "[{\"Alias\": \"u\", \"Filter\": \"users.status = 'not active'\"}]"}}
                        ]
                    }
                    """
                ),
                [Source],
                IsEngineWord
            )
            .ShouldBe(
                [
                    "name FindUser in query.sql",
                    "name id in query.sql",
                    "name status in capture.json",
                    "name u in capture.json",
                    "name users in capture.json",
                    "literal 'active' in capture.json",
                ],
                ignoreOrder: true
            );

    [Fact]
    public void Of_QuotedNameWithItsMark_IsSoughtWithoutTheMark() =>
        BundleLeaks
            .Of(BundleOf("SELECT \"Full Name?\" FROM t_2", "{}"), [Source], IsEngineWord)
            .ShouldBe(["name Full Name in query.sql"]);

    [Fact]
    public void Of_NameInsideAWord_IsNoLeak() =>
        BundleLeaks
            .Of(
                BundleOf(
                    "SELECT a_1 FROM t_2",
                    /*lang=json,strict*/
                    """{"x": "ids users_2"}"""
                ),
                [Source],
                IsEngineWord
            )
            .ShouldBeEmpty();

    [Fact]
    public void Of_LiteralThatIsAWordInsideAnotherWord_IsNoLeak() =>
        BundleLeaks
            .Of(
                BundleOf(
                    "SELECT a_1 FROM t_2",
                    /*lang=json,strict*/
                    """{"calls": [{"answer": {"name": "active_since"}}]}"""
                ),
                [Source],
                IsEngineWord
            )
            .ShouldBeEmpty();

    [Fact]
    public void Of_LiteralThatIsTheMethodOfACall_IsNoLeak() =>
        BundleLeaks
            .Of(
                BundleOf(
                    "SELECT a_1 FROM t_2",
                    /*lang=json,strict*/
                    """{"calls": [{"method": "active", "request": {}, "answer": {}}]}"""
                ),
                [Source],
                IsEngineWord
            )
            .ShouldBeEmpty();

    [Fact]
    public void Of_LiteralOfSeveralWords_IsSoughtAsASubstring() =>
        BundleLeaks
            .Of(
                BundleOf(
                    "SELECT a_1 FROM t_2",
                    /*lang=json,strict*/
                    """{"calls": [{"answer": {"message": "x_on holdout"}}]}"""
                ),
                ["SELECT 'on hold' AS l;"],
                IsEngineWord
            )
            .ShouldBe(["literal 'on hold' in capture.json"]);

    // "Order" is the user's name though its fold is the keyword order, and is sought as written.
    [Fact]
    public void Of_QuotedNameFoldedToAnEngineWord_IsSought() =>
        BundleLeaks
            .Of(BundleOf("SELECT 1 AS \"Order\"", "{}"), [QuotedOrder], IsEngineWord)
            .ShouldBe(["name Order in query.sql"]);

    [Fact]
    public void Of_QuotedName_IsSoughtInItsCase() =>
        BundleLeaks.Of(BundleOf("SELECT 1 AS a_1 ORDER BY 1", "{}"), [QuotedOrder], IsEngineWord).ShouldBeEmpty();

    [Fact]
    public void Find_RawBundle_FindsTheUsersNamesInEveryFile()
    {
        var raw = SqlServerRenamings.Raw("select-left-join");

        var found = BundleLeaks.Find(
            raw,
            SqlServerRenamings.Part,
            BundleLeaks.EngineRowNames(raw, SqlServerRenamings.Part),
            raw.Query
        );

        found.ShouldContain("users in query.sql");
        found.ShouldContain("users in capture.json call 2 'columns' answer.rows[].sourceTable");
        found.ShouldContain("UsersWithOrders in expected.json key");
        found.ShouldContain("users in expected.json columns[].origin.table");
    }

    [Fact]
    public void Find_RawBundle_LeavesOutWhatIsFixed()
    {
        var raw = SqlServerRenamings.Raw("select-left-join");

        var found = BundleLeaks.Find(
            raw,
            SqlServerRenamings.Part,
            BundleLeaks.EngineRowNames(raw, SqlServerRenamings.Part),
            raw.Query
        );

        found.ShouldNotContain(place => place.Contains("serverVersion") || place.Contains("productVersion"));
    }

    [Fact]
    public void Find_DeclaredParameterOnlyInAMarker_IsFound()
    {
        var raw = SqlServerRenamings.Raw("delete-declared-only");

        var found = BundleLeaks.Find(
            raw,
            SqlServerRenamings.Part,
            BundleLeaks.EngineRowNames(raw, SqlServerRenamings.Part),
            raw.Query
        );

        found.ShouldContain("tenant in query.sql");
        found.ShouldContain("PurgeOrders in query.sql");
    }

    [Fact]
    public void Find_MarkersWordAndShape_AreNoLeak()
    {
        var raw = SqlServerRenamings.Raw("select-left-join") with { Query = "-- name: q_1 -> many\nSELECT 1 AS a;\n" };

        var found = BundleLeaks.Find(
            raw,
            SqlServerRenamings.Part,
            BundleLeaks.EngineRowNames(raw, SqlServerRenamings.Part),
            "SELECT name, many FROM t;"
        );

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

        var found = BundleLeaks.Find(
            raw,
            SqlServerRenamings.Part,
            BundleLeaks.EngineRowNames(raw, SqlServerRenamings.Part),
            "SELECT N'x';"
        );

        found.ShouldBe(["x in query.sql"]);
    }

    [Fact]
    public void Find_LiteralOfTwoWords_IsFoundAnywhereInAString()
    {
        var raw = SqlServerRenamings.Raw("select-left-join") with
        {
            Query = "-- name: q_1\nSELECT 'big red dog' AS a;\n",
        };

        var found = BundleLeaks.Find(
            raw,
            SqlServerRenamings.Part,
            BundleLeaks.EngineRowNames(raw, SqlServerRenamings.Part),
            "SELECT 'red dog';"
        );

        found.ShouldBe(["'red dog' in query.sql"]);
    }

    [Fact]
    public void Find_LiteralWithNoToken_IsNoLeak()
    {
        var raw = SqlServerRenamings.Raw("select-left-join") with
        {
            Query = "-- name: q_1\nSELECT a, b, 'ab, cd' AS c;\n",
        };

        var found = BundleLeaks.Find(
            raw,
            SqlServerRenamings.Part,
            BundleLeaks.EngineRowNames(raw, SqlServerRenamings.Part),
            "SELECT ', ', 'ab, cd';"
        );

        found.ShouldBe(["'ab, cd' in query.sql"]);
    }

    [Fact]
    public void EngineRowNames_OfAViewOfSys_AreTheEnginesNames()
    {
        var raw = SqlServerRenamings.Raw("select-other-database");

        var names = BundleLeaks.EngineRowNames(raw, SqlServerRenamings.Part);

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

        var names = BundleLeaks.EngineRowNames(raw, SqlServerRenamings.Part);

        raw.Capture.ShouldContain("acme_payroll_db");
        names.ShouldNotContain("acme_payroll_db");
        names.ShouldNotContain("database_id");
        names.ShouldNotContain("databases");
    }

    [Fact]
    public void Provenance_Entry_IsItsPlanTableMatchAndSources()
    {
        var raw = SqlServerRenamings.Raw("select-left-join");

        var provenance = Provenance.Of(raw.Expected.ShouldNotBeNull());

        provenance.ShouldBe(
            string.Join(
                '\n',
                "plan \"skipped\"",
                "tableMatch \"several-tables\"",
                "columns[0].nullableSource \"server\"",
                "columns[1].nullableSource \"server\"",
                "columns[2].nullableSource \"server\"",
                "columns[3].nullableSource \"server\""
            )
        );
    }
}
