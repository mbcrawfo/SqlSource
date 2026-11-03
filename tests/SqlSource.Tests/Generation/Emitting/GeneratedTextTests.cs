using Shouldly;
using SqlSource.Generation.Emitting;
using Xunit;

namespace SqlSource.Tests.Generation.Emitting;

public sealed class GeneratedTextTests
{
    [Fact]
    public void IndentOf_ThreeLevels_IsTwelveSpaces() => GeneratedText.IndentOf(3).ShouldBe(new string(' ', 12));

    // The cache holds levels 0 to 15: the last level it holds and the first it builds are four spaces a level too.
    [Theory]
    [InlineData(15)]
    [InlineData(16)]
    public void IndentOf_LastCachedAndFirstBuiltLevel_AreFourSpacesALevel(int depth) =>
        GeneratedText.IndentOf(depth).ShouldBe(new string(' ', 4 * depth));

    [Fact]
    public void Deeper_ACachedIndent_GivesTheCachedNext() =>
        ReferenceEquals(GeneratedText.Deeper(GeneratedText.IndentOf(2)), GeneratedText.IndentOf(3)).ShouldBeTrue();

    [Fact]
    public void Deeper_TheNextToLastCachedIndent_GivesTheLastCached() =>
        ReferenceEquals(GeneratedText.Deeper(GeneratedText.IndentOf(14)), GeneratedText.IndentOf(15)).ShouldBeTrue();

    [Fact]
    public void Deeper_TheLastCachedIndent_IsBuilt() =>
        GeneratedText.Deeper(GeneratedText.IndentOf(15)).ShouldBe(new string(' ', 64));

    [Fact]
    public void Deeper_IndentNotFromTheCache_AddsOneLevel() =>
        GeneratedText.Deeper(new string(' ', 8)).ShouldBe(new string(' ', 12));

    // An indent the cache cannot have given gets one level after it, never the cached indent of a level near its
    // length.
    [Fact]
    public void Deeper_IndentOfNoWholeLevel_AddsOneLevel() =>
        GeneratedText.Deeper(new string(' ', 6)).ShouldBe(new string(' ', 10));

    [Fact]
    public void Deeper_IndentPastTheCache_IsBuilt() =>
        GeneratedText.Deeper(GeneratedText.IndentOf(20)).ShouldBe(new string(' ', 84));
}
