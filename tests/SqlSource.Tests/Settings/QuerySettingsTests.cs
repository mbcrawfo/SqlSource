using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

// Every setting resolves by one rule: the most specific level that has a value gives it.
public class QuerySettingsTests
{
    private static readonly SettingsLevel Keep = new() { Parameters = GeneratorParameters.KeepComments };

    private static readonly SettingsLevel Sort = new() { Parameters = GeneratorParameters.SortInput };

    private static readonly SettingsLevel Empty = new() { Parameters = GeneratorParameters.None };

    [Fact]
    public void Resolve_NoLevelSaysAnything_GivesTheDefaults()
    {
        var settings = QuerySettings.Resolve(
            SettingsLevel.None,
            SettingsLevel.None,
            SettingsLevel.None,
            SettingsLevel.None
        );

        settings.Parameters.ShouldBe(GeneratorParameters.None);
        settings.KeepComments.ShouldBeFalse();
        settings.ValidateTokens.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_Parameters_ComeWholeFromTheMostSpecificLevelThatGivesAList()
    {
        QuerySettings
            .Resolve(SettingsLevel.None, SettingsLevel.None, SettingsLevel.None, Keep)
            .KeepComments.ShouldBeTrue();
        QuerySettings
            .Resolve(SettingsLevel.None, SettingsLevel.None, Sort, Keep)
            .Parameters.ShouldBe(GeneratorParameters.SortInput);
        QuerySettings
            .Resolve(SettingsLevel.None, Keep, Sort, Sort)
            .Parameters.ShouldBe(GeneratorParameters.KeepComments);
        QuerySettings.Resolve(Empty, Keep, Keep, Keep).Parameters.ShouldBe(GeneratorParameters.None);
    }

    [Fact]
    public void Resolve_NoTokenValidation_TurnsValidationOff() =>
        QuerySettings
            .Resolve(new SettingsLevel { Parameters = GeneratorParameters.NoTokenValidation }, Keep, Keep, Keep)
            .ValidateTokens.ShouldBeFalse();

    [Fact]
    public void Over_EachMember_ComesFromThisLevelWhenItHasOne()
    {
        Keep.Over(Sort).ShouldBe(Keep);
        SettingsLevel.None.Over(Sort).ShouldBeSameAs(Sort);
        Keep.Over(SettingsLevel.None).ShouldBeSameAs(Keep);
    }

    [Fact]
    public void None_IsEqualToALevelThatSetsNothing() => new SettingsLevel().ShouldBe(SettingsLevel.None);
}
