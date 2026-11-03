using Shouldly;
using SqlSource.Generation.Emitting;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting;

public sealed class GeneratedTextTests
{
    [Fact]
    public void IndentOf_ThreeLevels_IsTwelveSpaces() => GeneratedText.IndentOf(3).ShouldBe(new string(' ', 12));

    [Fact]
    public void Deeper_ACachedIndent_GivesTheCachedNext() =>
        ReferenceEquals(GeneratedText.Deeper(GeneratedText.IndentOf(2)), GeneratedText.IndentOf(3)).ShouldBeTrue();

    [Fact]
    public void Deeper_IndentNotFromTheCache_AddsOneLevel() =>
        GeneratedText.Deeper(new string(' ', 8)).ShouldBe(new string(' ', 12));

    [Fact]
    public void Deeper_IndentPastTheCache_IsBuilt() =>
        GeneratedText.Deeper(GeneratedText.IndentOf(20)).ShouldBe(new string(' ', 84));
}
