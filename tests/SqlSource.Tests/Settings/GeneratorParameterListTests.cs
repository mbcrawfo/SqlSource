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
            .ReadElements([" keep-comments ", "SORT-INPUT=off", "", "no-array-pool=Default"], invalid, repeated)
            .ShouldBe(
                KeepCommentsOn
                    .With(GeneratorParameters.SortInput, GeneratorParameterState.Off)
                    .With(GeneratorParameters.NoArrayPool, GeneratorParameterState.Default)
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
}
