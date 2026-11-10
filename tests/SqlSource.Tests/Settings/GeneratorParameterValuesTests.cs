using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public sealed class GeneratorParameterValuesTests
{
    [Fact]
    public void With_OneSlot_LeavesTheOthers()
    {
        var values = default(GeneratorParameterValues)
            .With(GeneratorParameters.SortInput, 5)
            .With(GeneratorParameters.KeepComments, 1);

        values.Get(GeneratorParameters.SortInput).ShouldBe(5);
        values.Get(GeneratorParameters.KeepComments).ShouldBe(1);
        values.Get(GeneratorParameters.ListPadding).ShouldBe(0);
    }

    [Fact]
    public void With_AValueAgain_ReplacesIt() =>
        default(GeneratorParameterValues)
            .With(GeneratorParameters.SortInput, 5)
            .With(GeneratorParameters.SortInput, 2)
            .Get(GeneratorParameters.SortInput)
            .ShouldBe(2);

    [Fact]
    public void With_TheHighestValueInTheLastSlot_IsKept() =>
        default(GeneratorParameterValues)
            .With((GeneratorParameters)(1 << 15), 15)
            .Get((GeneratorParameters)(1 << 15))
            .ShouldBe(15);

    [Fact]
    public void Over_TakesTheMaskedSlotsFromThisAndTheRestFromLower()
    {
        var upper = default(GeneratorParameterValues)
            .With(GeneratorParameters.KeepComments, 1)
            .With(GeneratorParameters.SortInput, 3);
        var lower = default(GeneratorParameterValues)
            .With(GeneratorParameters.KeepComments, 2)
            .With(GeneratorParameters.SortInput, 4);

        var merged = upper.Over(GeneratorParameters.KeepComments, lower);

        merged.Get(GeneratorParameters.KeepComments).ShouldBe(1);
        merged.Get(GeneratorParameters.SortInput).ShouldBe(4);
    }

    [Fact]
    public void IsOn_IsTheValueOn()
    {
        var values = default(GeneratorParameterValues).With(
            GeneratorParameters.SortInput,
            GeneratorParameterValues.Off
        );

        values.IsOn(GeneratorParameters.KeepComments).ShouldBeTrue();
        values.IsOn(GeneratorParameters.SortInput).ShouldBeFalse();
    }
}
