using System.Collections.Generic;
using Shouldly;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Settings;

public class GeneratorParameterListTests
{
    [Theory]
    // An internal enum cannot be the parameter of a public test method, so a row gives the flags as a number.
    [InlineData("keep-comments", 1)]
    [InlineData("NO-TOKEN-VALIDATION", 2)]
    [InlineData("sort-input", 4)]
    [InlineData("sort-output", 8)]
    [InlineData("no-table-models", 16)]
    [InlineData("async-method-suffix", 32)]
    [InlineData("  keep-comments\tsort-input  keep-comments ", 5)]
    [InlineData("default", 0)]
    [InlineData(" Default ", 0)]
    public void Parse_ValidList_IsItsParameters(string value, int expected)
    {
        var invalid = new List<string>();

        ((int?)GeneratorParameterList.Parse(value, invalid)).ShouldBe(expected);
        invalid.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t ")]
    public void Parse_NoValue_GivesNoList(string? value)
    {
        var invalid = new List<string>();

        GeneratorParameterList.Parse(value, invalid).ShouldBeNull();
        invalid.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_UnknownWords_AreNamedAndTheOthersApply()
    {
        var invalid = new List<string>();

        GeneratorParameterList
            .Parse("keep-comments token-validation keep-comments=1 nope", invalid)
            .ShouldBe(GeneratorParameters.KeepComments);
        invalid.ShouldBe(["token-validation", "keep-comments=1", "nope"]);
    }

    [Fact]
    public void Parse_OnlyUnknownWords_GivesNoList()
    {
        var invalid = new List<string>();

        GeneratorParameterList.Parse("nope", invalid).ShouldBeNull();
        invalid.ShouldBe(["nope"]);
    }

    [Fact]
    public void Parse_DefaultBesideAnotherWord_IsInvalidWhole()
    {
        var invalid = new List<string>();

        GeneratorParameterList.Parse(" default  keep-comments ", invalid).ShouldBeNull();
        invalid.ShouldBe(["default  keep-comments"]);
    }
}
