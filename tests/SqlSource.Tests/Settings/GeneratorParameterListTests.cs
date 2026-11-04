using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public sealed class GeneratorParameterListTests
{
    [Theory]
    // An internal enum cannot be the parameter of a public test method, so a row gives the flags as a number.
    [InlineData(new string[] { }, 0)]
    [InlineData(new[] { "", "  " }, 0)]
    [InlineData(new[] { "DEFAULT" }, 0)]
    [InlineData(new[] { " default", "default " }, 0)]
    [InlineData(new[] { " keep-comments ", "sort-input" }, 5)]
    [InlineData(new[] { "keep-comments", "", "keep-comments" }, 1)]
    public void ReadElements_ValidElements_AreTheirParameters(string[] elements, int expected)
    {
        var invalid = new List<int>();

        ((int?)GeneratorParameterList.ReadElements(elements, invalid)).ShouldBe(expected);
        invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadElements_UnknownWord_IsItsIndexAndTheOthersApply()
    {
        var invalid = new List<int>();

        GeneratorParameterList
            .ReadElements(["keep-comments", "bogus"], invalid)
            .ShouldBe(GeneratorParameters.KeepComments);
        invalid.ShouldBe([1]);
    }

    // An element is one word: two words in one element are no parameter.
    [Theory]
    [InlineData(new[] { "bogus" }, new[] { 0 })]
    [InlineData(new[] { "keep-comments sort-input" }, new[] { 0 })]
    [InlineData(new[] { "", "bogus", "nope" }, new[] { 1, 2 })]
    public void ReadElements_OnlyUnknownWords_GivesNoList(string[] elements, int[] expected)
    {
        var invalid = new List<int>();

        GeneratorParameterList.ReadElements(elements, invalid).ShouldBeNull();
        invalid.ShouldBe(expected);
    }

    [Theory]
    [InlineData(new[] { "default", "keep-comments" }, new[] { 0 })]
    [InlineData(new[] { "keep-comments", " DEFAULT " }, new[] { 1 })]
    [InlineData(new[] { "bogus", "default", "default" }, new[] { 0, 1, 2 })]
    public void ReadElements_DefaultBesideAnotherWord_GivesNoListAndNamesEachDefaultAndUnknownWord(
        string[] elements,
        int[] expected
    )
    {
        var invalid = new List<int>();

        GeneratorParameterList.ReadElements(elements, invalid).ShouldBeNull();
        invalid.ShouldBe(expected);
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
