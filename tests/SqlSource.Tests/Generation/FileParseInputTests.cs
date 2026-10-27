using System;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Chains;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generation;

// What a file's parse depends on, stage one of the settings: the dialect and whether comments may be wanted, from the
// chain of its folder and the types whose attribute asks for comments.
public sealed class FileParseInputTests
{
    [Theory]
    // The chain's dialect, when it sets one.
    [InlineData("postgres", nameof(SqlDialect.PostgreSql))]
    [InlineData("MySQL", nameof(SqlDialect.MySql))]
    // Or else ANSI.
    [InlineData(null, nameof(SqlDialect.Ansi))]
    public void Resolve_Dialect_IsTheChainsOrElseAnsi(string? chain, string dialect)
    {
        var file = new InMemoryAdditionalText(GeneratorHarness.UsersPath, "SELECT 1;");

        var resolved = Resolve(file, ChainOf(chain, null));

        resolved.File.ShouldBeSameAs(file);
        resolved.NormalizedPath.ShouldBe(GeneratorHarness.UsersNormalizedPath);
        resolved.Dialect.ShouldBe(new SqlDialectChoice(Enum.Parse<SqlDialect>(dialect), SqlDialectOptions.None));
        resolved.CommentsWanted.ShouldBeFalse();
    }

    // The chain's dialect is taken with its options, whole.
    [Fact]
    public void Resolve_ChainWithAnOption_TakesTheOption() =>
        Resolve(new InMemoryAdditionalText(GeneratorHarness.UsersPath, "SELECT 1;"), ChainOf("mysql,ansi-quotes", null))
            .Dialect.ShouldBe(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.AnsiQuotes));

    [Theory]
    // The chain's list decides.
    [InlineData("keep-comments", true)]
    [InlineData("sort-input keep-comments", true)]
    [InlineData("sort-input", false)]
    // The empty list is a list, which asks for nothing.
    [InlineData("default", false)]
    // No list, no comments.
    [InlineData(null, false)]
    public void Resolve_CommentsWanted_IsWhatTheChainsListAsksFor(string? chainParameters, bool expected) =>
        Resolve(new InMemoryAdditionalText(GeneratorHarness.UsersPath, ""), ChainOf(null, chainParameters))
            .CommentsWanted.ShouldBe(expected);

    // A type that claims the file asks for comments, whatever the chain says.
    [Fact]
    public void Resolve_FileOfATypeThatKeepsComments_WantsThemWhateverTheChainSays() =>
        Resolve(
            new InMemoryAdditionalText(GeneratorHarness.UsersPath, ""),
            ChainOf(null, "default"),
            GeneratorHarness.UsersNormalizedPath
        )
            .CommentsWanted.ShouldBeTrue();

    // A type that claims another file asks for nothing of this one.
    [Fact]
    public void Resolve_FileOfNoTypeThatKeepsComments_WantsNone() =>
        Resolve(new InMemoryAdditionalText(GeneratorHarness.UsersPath, ""), SettingsChain.None, "app/Repo/Other.sql")
            .CommentsWanted.ShouldBeFalse();

    [Fact]
    public void Resolve_SameFileAndSettings_GivesEqualValues()
    {
        var file = new InMemoryAdditionalText(GeneratorHarness.UsersPath, "SELECT 1;");

        Resolve(file, ChainOf("mysql", "sort-input")).ShouldBe(Resolve(file, ChainOf(" MySQL ", "sort-input")));
    }

    // A chain with this dialect and this list of generator parameters, or SettingsChain.None for neither.
    private static SettingsChain ChainOf(string? dialect, string? parameters)
    {
        if (dialect is null && parameters is null)
        {
            return SettingsChain.None;
        }

        SqlDialectChoice? choice = null;
        if (dialect is not null)
        {
            SqlDialectName.TryParse(dialect, out var parsed).ShouldBeTrue();
            choice = parsed;
        }

        return new SettingsChain(
            parameters is null
                ? SettingsLevel.None
                : new SettingsLevel { Parameters = GeneratorParameterList.Parse(parameters, []) },
            choice
        );
    }

    private static FileParseInput Resolve(
        InMemoryAdditionalText file,
        SettingsChain chain,
        params string[] commentPaths
    ) => FileParseInput.Resolve(file, GeneratorHarness.UsersNormalizedPath, chain, TestModels.Array(commentPaths));
}
