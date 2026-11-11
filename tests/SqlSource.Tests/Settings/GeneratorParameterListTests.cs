using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public sealed class GeneratorParameterListTests
{
    private static readonly GeneratorParameterChoices KeepCommentsOn = GeneratorParameterChoices.None.With(
        GeneratorParameters.KeepComments,
        GeneratorParameterState.On
    );

    [Theory]
    [InlineData([new string[] { }])]
    [InlineData([new[] { "  " }])]
    [InlineData([new[] { "", " \t" }])]
    public void ReadElements_NoElement_NamesNothing(string[] elements)
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        GeneratorParameterList.ReadElements(elements, invalid, repeated).IsEmpty.ShouldBeTrue();
        invalid.ShouldBeEmpty();
        repeated.ShouldBeEmpty();
    }

    [Theory]
    [InlineData([new[] { "default" }])]
    [InlineData([new[] { "default", "DEFAULT" }])]
    [InlineData([new[] { " default", "default " }])]
    public void ReadElements_Default_IsTheReset(string[] elements)
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        GeneratorParameterList.ReadElements(elements, invalid, repeated).ShouldBe(GeneratorParameterChoices.Reset);
        invalid.ShouldBeEmpty();
        repeated.ShouldBeEmpty();
    }

    [Theory]
    [InlineData([new[] { "keep-comments", "default" }])]
    [InlineData([new[] { "default", " Keep-Comments=ON " }])]
    public void ReadElements_DefaultBesideOtherWords_ResetsThenAppliesThem(string[] elements)
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        GeneratorParameterList
            .ReadElements(elements, invalid, repeated)
            .ShouldBe(
                GeneratorParameterChoices.Reset.With(GeneratorParameters.KeepComments, GeneratorParameterState.On)
            );
        invalid.ShouldBeEmpty();
        repeated.ShouldBeEmpty();
    }

    [Fact]
    public void ReadElements_Words_NameTheirStates()
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        GeneratorParameterList
            .ReadElements([" keep-comments ", "SORT-INPUT=off", "", "array-pool=Default"], invalid, repeated)
            .ShouldBe(
                KeepCommentsOn
                    .With(GeneratorParameters.SortInput, GeneratorParameterState.Off)
                    .With(GeneratorParameters.ArrayPool, GeneratorParameterState.Default)
            );
        invalid.ShouldBeEmpty();
        repeated.ShouldBeEmpty();
    }

    [Fact]
    public void ReadElements_UnknownWordOrState_IsItsIndexAndTheOthersApply()
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        GeneratorParameterList
            .ReadElements(["keep-comments", "bogus", "sort-input=maybe"], invalid, repeated)
            .ShouldBe(KeepCommentsOn);
        invalid.ShouldBe([1, 2]);
        repeated.ShouldBeEmpty();
    }

    [Fact]
    public void ReadElements_OldNegativeWord_IsItsIndexAndTheOthersApply()
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        GeneratorParameterList
            .ReadElements(["no-array-pool", "keep-comments"], invalid, repeated)
            .ShouldBe(KeepCommentsOn);
        invalid.ShouldBe([0]);
        repeated.ShouldBeEmpty();
    }

    [Fact]
    public void All_OfTheList_IsTheUnionOfTheNamedParameters()
    {
        var union = GeneratorParameters.None;
        foreach (var definition in GeneratorParameterList.Definitions)
        {
            union |= definition.Parameter;
        }

        GeneratorParameterList.All.ShouldBe(union);
    }

    // An element is one word: two words in one element are no parameter.
    [Theory]
    [InlineData(new[] { "bogus" }, new[] { 0 })]
    [InlineData(new[] { "keep-comments sort-input" }, new[] { 0 })]
    [InlineData(new[] { "", "bogus", "default=off" }, new[] { 1, 2 })]
    public void ReadElements_OnlyUnknownWords_NameNothing(string[] elements, int[] expected)
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        GeneratorParameterList.ReadElements(elements, invalid, repeated).IsEmpty.ShouldBeTrue();
        invalid.ShouldBe(expected);
    }

    [Fact]
    public void ReadElements_OneOptionInTwoStates_IsARepeatAndTheFirstApplies()
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        GeneratorParameterList
            .ReadElements(["keep-comments", "keep-comments=off"], invalid, repeated)
            .ShouldBe(KeepCommentsOn);
        invalid.ShouldBeEmpty();
        repeated.ShouldBe([(1, 0)]);
    }

    // The default is another value than any word's, even where it is the same in effect.
    [Theory]
    [InlineData([new[] { "keep-comments=off", "keep-comments=default" }])]
    [InlineData([new[] { "keep-comments=default", "keep-comments=off" }])]
    [InlineData([new[] { "keep-comments", "keep-comments=default" }])]
    public void ReadElements_DefaultBesideAValue_IsARepeatAndTheFirstApplies(string[] elements)
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        var choices = GeneratorParameterList.ReadElements(elements, invalid, repeated);

        choices.ShouldBe(GeneratorParameterList.ReadElements([elements[0]], [], []));
        invalid.ShouldBeEmpty();
        repeated.ShouldBe([(1, 0)]);
    }

    [Fact]
    public void ReadElements_OneOptionInOneStateTwice_IsNoRepeat()
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        GeneratorParameterList
            .ReadElements(["keep-comments", "KEEP-COMMENTS=on"], invalid, repeated)
            .ShouldBe(KeepCommentsOn);
        invalid.ShouldBeEmpty();
        repeated.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("", "")]
    [InlineData(" \t ", "")]
    [InlineData("one", "0:one")]
    [InlineData("  one\ttwo   three ", "2:one 6:two 12:three")]
    [InlineData("a=b  c", "0:a=b 5:c")]
    public void Words_AnyText_GivesEachWordWithWhereItStarts(string text, string expected)
    {
        var found = new List<string>();
        var words = GeneratorParameterList.Words(text.AsSpan());
        while (words.MoveNext())
        {
            words.Length.ShouldBe(words.Current.Length);
            found.Add($"{words.Start}:{words.Current}");
        }

        string.Join(" ", found).ShouldBe(expected);
    }

    [Fact]
    public void Words_InAForeach_GivesTheWords()
    {
        var found = new List<string>();
        foreach (var word in GeneratorParameterList.Words(" keep-comments  default ".AsSpan()))
        {
            found.Add(word.ToString());
        }

        found.ShouldBe(["keep-comments", "default"]);
    }

    [Fact]
    public void Definitions_AreInFlagOrderAndWithinTheSlots()
    {
        GeneratorParameterList.Definitions.Length.ShouldBeLessThanOrEqualTo(16);
        for (var index = 0; index < GeneratorParameterList.Definitions.Length; index++)
        {
            GeneratorParameterList.Definitions[index].Parameter.ShouldBe((GeneratorParameters)(1 << index));
        }
    }

    [Theory]
    [InlineData("Keep-Comments=OFF")]
    [InlineData(" keep-comments=Off ")]
    public void ReadElements_AValueInAnyCase_IsRead(string element)
    {
        var (invalid, repeated) = (new List<int>(), new List<(int Index, int First)>());

        GeneratorParameterList
            .ReadElements([element], invalid, repeated)
            .ShouldBe(
                GeneratorParameterChoices.None.With(GeneratorParameters.KeepComments, GeneratorParameterState.Off)
            );
        invalid.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("transaction=serializable", 5)]
    [InlineData("Transaction=READ-COMMITTED", 3)]
    [InlineData("transaction", 0)]
    public void ReadElements_TransactionValue_IsItsWord(string element, int value) =>
        GeneratorParameterList.ReadElements([element], [], []).ValueOf(GeneratorParameters.Transaction).ShouldBe(value);

    [Theory]
    [InlineData("method-type=async", 0)]
    [InlineData("method-type=sync", 1)]
    [InlineData("method-type=both", 2)]
    [InlineData("Method-Type=SYNC", 1)]
    [InlineData("METHOD-TYPE=Both", 2)]
    public void ReadWord_MethodTypeWords_ReadsEachIndex(string word, int expected)
    {
        var kind = GeneratorParameterList.ReadWord(word.AsSpan(), out var definition, out var value);

        kind.ShouldBe(GeneratorWordKind.Parameter);
        definition.ShouldNotBeNull().Parameter.ShouldBe(GeneratorParameters.MethodType);
        value.ShouldBe(expected);
    }

    // method-type alone reads as method-type=on, and on is no word of it.
    [Fact]
    public void ReadWord_MethodTypeAlone_IsInvalidState()
    {
        var kind = GeneratorParameterList.ReadWord("method-type".AsSpan(), out var definition, out var value);

        kind.ShouldBe(GeneratorWordKind.InvalidState);
        definition.ShouldNotBeNull().Parameter.ShouldBe(GeneratorParameters.MethodType);
        value.ShouldBeNull();
    }

    [Fact]
    public void Resolve_NoChoices_HasCollectionMethodOff() =>
        GeneratorParameterChoices.None.Resolve().IsOn(GeneratorParameters.CollectionMethod).ShouldBeFalse();
}
