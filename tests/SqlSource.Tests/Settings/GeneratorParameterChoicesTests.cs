using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public sealed class GeneratorParameterChoicesTests
{
    [Fact]
    public void Default_IsEmpty_AndResolvesToTheDefaults()
    {
        default(GeneratorParameterChoices).IsEmpty.ShouldBeTrue();
        GeneratorParameterChoices.None.IsEmpty.ShouldBeTrue();
        GeneratorParameterChoices.None.Resolve().ShouldBe(GeneratorParameterList.Defaults);
    }

    [Fact]
    public void With_On_NamesItAndTurnsItOn() =>
        GeneratorParameterChoices
            .None.With(GeneratorParameters.KeepComments, GeneratorParameterState.On)
            .StateOf(GeneratorParameters.KeepComments)
            .ShouldBe(true);

    [Fact]
    public void With_Off_NamesItAndTurnsItOff() =>
        GeneratorParameterChoices
            .None.With(GeneratorParameters.KeepComments, GeneratorParameterState.Off)
            .StateOf(GeneratorParameters.KeepComments)
            .ShouldBe(false);

    [Fact]
    public void With_Default_NamesItWithItsDefault() =>
        GeneratorParameterChoices
            .None.With(GeneratorParameters.KeepComments, GeneratorParameterState.Default)
            .StateOf(GeneratorParameters.KeepComments)
            .ShouldBe((GeneratorParameterList.Defaults & GeneratorParameters.KeepComments) != 0);

    [Fact]
    public void StateOf_NotNamed_IsNull() =>
        GeneratorParameterChoices.None.StateOf(GeneratorParameters.KeepComments).ShouldBeNull();

    [Fact]
    public void Over_OptionNamedAbove_TakesTheUpperState() =>
        GeneratorParameterChoices
            .None.With(GeneratorParameters.KeepComments, GeneratorParameterState.Off)
            .Over(GeneratorParameterChoices.None.With(GeneratorParameters.KeepComments, GeneratorParameterState.On))
            .StateOf(GeneratorParameters.KeepComments)
            .ShouldBe(false);

    [Fact]
    public void Over_OptionNotNamedAbove_KeepsTheLowerState()
    {
        var resolved = GeneratorParameterChoices
            .None.With(GeneratorParameters.SortInput, GeneratorParameterState.On)
            .Over(GeneratorParameterChoices.None.With(GeneratorParameters.KeepComments, GeneratorParameterState.On))
            .Resolve();

        resolved.HasFlag(GeneratorParameters.KeepComments).ShouldBeTrue();
        resolved.HasFlag(GeneratorParameters.SortInput).ShouldBeTrue();
    }

    [Fact]
    public void Over_ResetAbove_HidesEveryLowerState()
    {
        var merged = GeneratorParameterChoices.Reset.Over(
            GeneratorParameterChoices.None.With(GeneratorParameters.KeepComments, GeneratorParameterState.On)
        );

        merged.Named.ShouldBe(GeneratorParameterList.All);
        merged.StateOf(GeneratorParameters.KeepComments).HasValue.ShouldBeTrue();
        merged.Resolve().ShouldBe(GeneratorParameterList.Defaults);
    }

    [Fact]
    public void With_TheSameStateTwice_IsEqual()
    {
        var once = GeneratorParameterChoices.None.With(GeneratorParameters.KeepComments, GeneratorParameterState.On);
        var twice = once.With(GeneratorParameters.KeepComments, GeneratorParameterState.On);

        twice.ShouldBe(once);
        GeneratorParameterChoices
            .None.With(GeneratorParameters.KeepComments, GeneratorParameterState.Off)
            .ShouldBe(new GeneratorParameterChoices(GeneratorParameters.KeepComments, GeneratorParameters.None));
    }
}
