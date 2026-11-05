using Shouldly;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Testing.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests;

public sealed class BundleLeaksTests
{
    private const string Source =
        "-- name: FindUser\nSELECT u.id, \"Full Name!\" FROM users u WHERE u.status = 'active' AND u.id = @id;\n";

    private static bool IsEngineWord(string word) => word.ToUpperInvariant() is "SELECT" or "FROM" or "WHERE" or "AND";

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

    private static Bundle BundleOf(string query, string capture) => new(null, query, capture, null, []);
}
