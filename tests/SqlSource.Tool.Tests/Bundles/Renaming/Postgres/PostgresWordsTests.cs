using Shouldly;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.Postgres;

public sealed class PostgresWordsTests
{
    [Fact]
    public void Fold_Unquoted_LowerCasesAsciiLetters() => PostgresWords.Fold("Users", quoted: false).ShouldBe("users");

    [Fact]
    public void Fold_Quoted_KeepsTheSpelling() => PostgresWords.Fold("Users", quoted: true).ShouldBe("Users");

    // PostgreSQL folds A to Z alone, so É stays upper case beside the folded t.
    [Fact]
    public void Fold_NonAscii_IsKept() => PostgresWords.Fold("ÉTÉ", quoted: false).ShouldBe("ÉtÉ");

    [Theory]
    [InlineData("SELECT", false)]
    [InlineData("name", false)]
    [InlineData("count", false)]
    [InlineData("int4", false)]
    [InlineData("_text", false)]
    [InlineData("ctid", false)]
    [InlineData("xmax", false)]
    [InlineData("pg_catalog", false)]
    [InlineData("PUBLIC", false)]
    [InlineData("EXCLUDED", false)]
    [InlineData("json_build_object", false)]
    [InlineData("count", true)]
    [InlineData("*SELECT* 2", true)]
    [InlineData("*VALUES*_2", true)]
    [InlineData("column3", false)]
    [InlineData("w1", false)]
    // An operator that starts with #, as a line of the list's header does.
    [InlineData("#", false)]
    [InlineData("#>>", false)]
    public void IsEngineWord_OfTheEngine_IsTrue(string spelling, bool quoted) =>
        PostgresWords.IsEngineWord(spelling, quoted).ShouldBeTrue();

    [Theory]
    [InlineData("users", false)]
    [InlineData("Count", true)]
    [InlineData("account_status", false)]
    [InlineData("*SELECT*x", false)]
    public void IsEngineWord_OfTheUser_IsFalse(string spelling, bool quoted) =>
        PostgresWords.IsEngineWord(spelling, quoted).ShouldBeFalse();

    [Fact]
    public void Vocabulary_Count_ExceedsThreeThousand() => PostgresWords.Vocabulary.Count.ShouldBeGreaterThan(3000);

    [Fact]
    public void EngineSchemas_OfPostgres_AreTheEnginesOwn() =>
        PostgresWords.EngineSchemas.ShouldBe(
            ["pg_catalog", "information_schema", "pg_toast", "public"],
            ignoreOrder: true
        );
}
