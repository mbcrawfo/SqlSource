using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

// The hash ties a query to the description a later phase keeps of it.  Its input is fixed: the engine's name, a line
// feed, the SQL without comments and with each token as {{name:default}}, a line feed, and a line for each parameter
// that a marker declares.
public class SqlQueryHashTests
{
    private const string Simple = "-- name: Q\nSELECT 1;\n";

    // printf 'postgres\nSELECT 1;\n' | shasum -a 256
    [Fact]
    public void Compute_SimpleQuery_IsTheSha256OfItsInput() =>
        Hash(Simple).ShouldBe("3a2a9df518545e35e03bfdafb532ac9ee2b87ba74fbd0805047634d82b7c8cac");

    // printf 'mssql\nSELECT 1;\n' | shasum -a 256
    [Fact]
    public void Compute_AnotherEngine_StartsFromItsCanonicalName() =>
        Hash(Simple, SqlDialect.SqlServer).ShouldBe("5b37670f9fd87ce771169402163baa1ec54b97293d4fa4454897400defac7cf9");

    // printf 'postgres\nSELECT id FROM users WHERE id = @id {{filter:AND x = 1}} ORDER BY {{orderBy}}{{tail:}}\n
    // id\nsince timestamptz not null\npage int\nq text null\n' | shasum -a 256, on one line.
    [Fact]
    public void Compute_QueryWithTokensAndDeclarations_RendersEachAsTheDefinitionSays()
    {
        const string Text =
            "-- name: Q\n-- param: @since timestamptz not null\n-- param: @page int\n-- param: @q text null\n"
            + "-- param: @id\n-- token: {{filter:AND x = 1}}\n"
            + "SELECT id FROM users WHERE id = @id {{filter}} ORDER BY {{orderBy}}{{tail:}}\n";

        Hash(Text).ShouldBe("71e8d6c8d93f1de8eefcc9a1e00c48c4455a0d416c23002d24ead2c0c09399f9");
    }

    [Theory]
    [InlineData("-- name: Q\r\nSELECT 1;\r\n")]
    [InlineData("-- name: Q\n-- summary: Text.\nSELECT 1; -- a comment\n")]
    [InlineData("-- name: Q\n\n  /* a comment */\nSELECT 1;\n\n")]
    [InlineData("-- name: Other\nSELECT 1;\n")]
    public void Compute_ChangeThatLeavesTheSqlAlone_GivesTheSameHash(string text) => Hash(text).ShouldBe(Hash(Simple));

    [Theory]
    // A default, an empty default, and none.
    [InlineData("SELECT {{a}}", "SELECT {{a:x}}")]
    [InlineData("SELECT {{a}}", "SELECT {{a:}}")]
    [InlineData("SELECT {{a:x}}", "SELECT {{a:y}}")]
    // A token's name.
    [InlineData("SELECT {{a}}", "SELECT {{b}}")]
    // A parameter's name, and its case.
    [InlineData("SELECT @a", "SELECT @b")]
    [InlineData("SELECT @a", "SELECT @A")]
    // A declaration, its type and its nullability.
    [InlineData("SELECT @a", "-- param: @a\nSELECT @a")]
    [InlineData("-- param: @a\nSELECT @a", "-- param: @a int\nSELECT @a")]
    [InlineData("-- param: @a int\nSELECT @a", "-- param: @a bigint\nSELECT @a")]
    [InlineData("-- param: @a int\nSELECT @a", "-- param: @a int null\nSELECT @a")]
    [InlineData("-- param: @a int\nSELECT @a", "-- param: @a int not null\nSELECT @a")]
    [InlineData("-- param: @a int null\nSELECT @a", "-- param: @a int not null\nSELECT @a")]
    public void Compute_ChangeToWhatIsDescribed_GivesAnotherHash(string first, string second) =>
        Hash("-- name: Q\n" + first + "\n").ShouldNotBe(Hash("-- name: Q\n" + second + "\n"));

    // A marker that types a parameter of the SQL can move without changing what is described.
    [Fact]
    public void Compute_MarkersOfSqlParametersInAnotherOrder_GivesTheSameHash() =>
        Hash("-- name: Q\n-- param: @a int\n-- param: @b text\nSELECT @a, @b\n")
            .ShouldBe(Hash("-- name: Q\n-- param: @b text\n-- param: @a int\nSELECT @a, @b\n"));

    [Fact]
    public void Compute_AnyQuery_IsSixtyFourLowerCaseHexCharacters() => Hash(Simple).ShouldMatch("^[0-9a-f]{64}$");

    private static string Hash(string text, SqlDialect dialect = SqlDialect.PostgreSql)
    {
        var result = SqlFileParser.Parse(text, "Query.sql", new SqlDialectChoice(dialect, SqlDialectOptions.None));
        result.Errors.ShouldBeEmpty();
        var block = result.Blocks.ShouldHaveSingleItem();
        return SqlQueryHash.Compute(result.Dialect, block.Segments, block.Tokens, block.Parameters);
    }
}
