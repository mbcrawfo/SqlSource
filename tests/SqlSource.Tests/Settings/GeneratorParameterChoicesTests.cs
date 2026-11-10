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
        GeneratorParameterChoices.None.Resolve().ShouldBe(GeneratorParameterValues.Defaults);
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
    public void With_DefaultOfAnOnByDefaultParameter_TurnsItOn() =>
        GeneratorParameterChoices
            .None.With(GeneratorParameters.ArrayPool, GeneratorParameterState.Default)
            .StateOf(GeneratorParameters.ArrayPool)
            .ShouldBe(true);

    [Fact]
    public void Reset_Resolves_ToTheDefaults() =>
        GeneratorParameterChoices
            .Reset.Resolve()
            .ShouldBe(
                SwitchValues.Of(
                    GeneratorParameters.TokenValidation
                        | GeneratorParameters.TableModels
                        | GeneratorParameters.ArrayPool
                        | GeneratorParameters.ListPadding
                )
            );

    [Fact]
    public void Over_ResetOverAnOffBelow_TurnsItBackOn() =>
        GeneratorParameterChoices
            .Reset.Over(GeneratorParameterChoices.None.With(GeneratorParameters.ArrayPool, GeneratorParameterState.Off))
            .StateOf(GeneratorParameters.ArrayPool)
            .ShouldBe(true);

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

        resolved.IsOn(GeneratorParameters.KeepComments).ShouldBeTrue();
        resolved.IsOn(GeneratorParameters.SortInput).ShouldBeTrue();
    }

    [Fact]
    public void Over_ResetAbove_HidesEveryLowerState()
    {
        var merged = GeneratorParameterChoices.Reset.Over(
            GeneratorParameterChoices.None.With(GeneratorParameters.KeepComments, GeneratorParameterState.On)
        );

        merged.Named.ShouldBe(GeneratorParameterList.All);
        merged.StateOf(GeneratorParameters.KeepComments).HasValue.ShouldBeTrue();
        merged.Resolve().ShouldBe(GeneratorParameterValues.Defaults);
    }

    [Fact]
    public void With_TheSameStateTwice_IsEqual()
    {
        var once = GeneratorParameterChoices.None.With(GeneratorParameters.KeepComments, GeneratorParameterState.On);
        var twice = once.With(GeneratorParameters.KeepComments, GeneratorParameterState.On);

        twice.ShouldBe(once);
        GeneratorParameterChoices
            .None.With(GeneratorParameters.KeepComments, GeneratorParameterState.Off)
            .ShouldBe(
                new GeneratorParameterChoices(
                    GeneratorParameters.KeepComments,
                    default(GeneratorParameterValues).With(
                        GeneratorParameters.KeepComments,
                        GeneratorParameterValues.Off
                    )
                )
            );
    }

    // Equal choices compare equal however they were built, or the generator's cache misses.
    [Fact]
    public void With_ThenOverNone_EqualsItself() =>
        GeneratorParameterChoices
            .None.With(GeneratorParameters.KeepComments, GeneratorParameterState.Off)
            .Over(GeneratorParameterChoices.None)
            .ShouldBe(
                GeneratorParameterChoices.None.With(GeneratorParameters.KeepComments, GeneratorParameterState.Off)
            );

    [Fact]
    public void Reset_OverAnything_IsReset() =>
        GeneratorParameterChoices
            .Reset.Over(GeneratorParameterChoices.None.With(GeneratorParameters.SortInput, GeneratorParameterState.On))
            .ShouldBe(GeneratorParameterChoices.Reset);

    [Fact]
    public void With_AValue_IsReadBackByValueOf() =>
        GeneratorParameterChoices
            .None.With(GeneratorParameters.SortInput, GeneratorParameterValues.Off)
            .ValueOf(GeneratorParameters.SortInput)
            .ShouldBe(GeneratorParameterValues.Off);

    [Fact]
    public void ValueOf_NotNamed_IsNull() =>
        GeneratorParameterChoices.None.ValueOf(GeneratorParameters.SortInput).ShouldBeNull();

    [Fact]
    public void Resolve_NamesNothing_IsTheDefaults() =>
        GeneratorParameterChoices.None.Resolve().ShouldBe(SwitchValues.Of(GeneratorParameterList.Defaults));
}
