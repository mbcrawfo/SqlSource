using System;
using System.Linq;
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public sealed class GeneratorParameterDefinitionTests
{
    private static readonly GeneratorParameterDefinition Mode = new(
        "mode",
        GeneratorParameters.SortInput,
        ["on", "off", "fast", "read-committed"],
        0
    );

    [Fact]
    public void TryReadValue_AWordOfTheList_IsItsIndexIgnoringCase()
    {
        Mode.TryReadValue("FAST".AsSpan(), out var fast).ShouldBeTrue();
        fast.ShouldBe(2);
        Mode.TryReadValue("Read-Committed".AsSpan(), out var readCommitted).ShouldBeTrue();
        readCommitted.ShouldBe(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("default")]
    [InlineData("slow")]
    [InlineData("on ")]
    public void TryReadValue_ATextNotInTheList_IsFalse(string text) =>
        Mode.TryReadValue(text.AsSpan(), out _).ShouldBeFalse();

    [Fact]
    public void ValuesHelp_NamesEachWordThenDefault() =>
        GeneratorParameterDefinition
            .Switch("keep-comments", GeneratorParameters.KeepComments, onByDefault: false)
            .ValuesHelp.ShouldBe(".  The values of keep-comments are on, off and default");

    [Fact]
    public void Switch_OnByDefault_HasTheWordsOnAndOffAndItsDefault()
    {
        var on = GeneratorParameterDefinition.Switch("array-pool", GeneratorParameters.ArrayPool, onByDefault: true);
        var off = GeneratorParameterDefinition.Switch("sort-input", GeneratorParameters.SortInput, onByDefault: false);

        on.Words.ShouldBe(["on", "off"]);
        on.Default.ShouldBe(GeneratorParameterValues.On);
        off.Default.ShouldBe(GeneratorParameterValues.Off);
    }

    [Fact]
    public void Slot_IsThePositionOfTheParametersFlag()
    {
        GeneratorParameterDefinition.Switch("a", GeneratorParameters.KeepComments, false).Slot.ShouldBe(0);
        GeneratorParameterDefinition.Switch("b", GeneratorParameters.ListPadding, false).Slot.ShouldBe(7);
    }

    [Fact]
    public void Constructor_SeventeenWords_Throws() =>
        Should.Throw<ArgumentException>(() =>
            new GeneratorParameterDefinition(
                "many",
                GeneratorParameters.SortInput,
                [.. Enumerable.Range(0, 17).Select(index => "w" + index)],
                0
            )
        );

    [Fact]
    public void Constructor_SixteenWords_IsAllowed()
    {
        var many = new GeneratorParameterDefinition(
            "many",
            GeneratorParameters.SortInput,
            [.. Enumerable.Range(0, 16).Select(index => "w" + index)],
            15
        );

        many.TryReadValue("w15".AsSpan(), out var last).ShouldBeTrue();
        last.ShouldBe(15);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Constructor_DefaultOutsideTheWords_Throws(int @default) =>
        Should.Throw<ArgumentException>(() =>
            new GeneratorParameterDefinition("two", GeneratorParameters.SortInput, ["on", "off"], @default)
        );

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Constructor_NotOneFlag_Throws(int parameter) =>
        Should.Throw<ArgumentException>(() =>
            new GeneratorParameterDefinition("bad", (GeneratorParameters)parameter, ["on", "off"], 0)
        );
}
