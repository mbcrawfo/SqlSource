using Shouldly;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Testing.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests;

// PostgreSQL's search for what a renamed bundle still holds of the user's, Of, on bundles made for each test.
public sealed class PostgresBundleLeaksTests
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
        PostgresBundleLeaks
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
        PostgresBundleLeaks
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
        PostgresBundleLeaks
            .Of(BundleOf("SELECT \"Full Name?\" FROM t_2", "{}"), [Source], IsEngineWord)
            .ShouldBe(["name Full Name in query.sql"]);

    [Fact]
    public void Of_NameInsideAWord_IsNoLeak() =>
        PostgresBundleLeaks
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
        PostgresBundleLeaks
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
        PostgresBundleLeaks
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
        PostgresBundleLeaks
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
        PostgresBundleLeaks
            .Of(BundleOf("SELECT 1 AS \"Order\"", "{}"), [QuotedOrder], IsEngineWord)
            .ShouldBe(["name Order in query.sql"]);

    [Fact]
    public void Of_QuotedName_IsSoughtInItsCase() =>
        PostgresBundleLeaks
            .Of(BundleOf("SELECT 1 AS a_1 ORDER BY 1", "{}"), [QuotedOrder], IsEngineWord)
            .ShouldBeEmpty();
}
