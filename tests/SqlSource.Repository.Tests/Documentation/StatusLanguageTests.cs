using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using Xunit;

namespace SqlSource.Repository.Tests.Documentation;

// The user documents state what SqlSource does, as fact: no page says that a feature is coming, works "today" or is in
// development.  A line of a user document, fenced blocks and link targets included, that holds one of the phrases
// below, in any case, fails the test.  The list is short and exact, since words such as "yet" or "later" have many
// proper uses; add a phrase only when a real sentence of status got past it.
public sealed class StatusLanguageTests
{
    private static readonly IReadOnlyList<string> Phrases =
    [
        "Coming in a later release",
        "works today",
        "work today",
        "no effect yet",
        "early development",
    ];

    public static TheoryData<string> PhraseData => [.. Phrases];

    [Fact]
    public void UserDocs_HoldNoPhraseOfStatus() =>
        UserDocs.Files.SelectMany(file => Hits(file, File.ReadAllLines(RepositoryRoot.Path(file)))).ShouldBeEmpty();

    [Theory]
    [MemberData(nameof(PhraseData))]
    public void Hits_FindEachPhrase_InAnyCase(string phrase)
    {
        Hits("page.md", [$"A sentence: {phrase}."]).ShouldContain($"page.md:1: {phrase}");
        Hits("page.md", ["", phrase.ToUpperInvariant()]).ShouldContain($"page.md:2: {phrase}");
    }

    [Theory]
    [InlineData("The method reads the rows still to come.")]
    [InlineData("Describing queries works with PostgreSQL 14 or later.")]
    [InlineData("Other editors' support has not been checked.")]
    public void Hits_OfAFact_AreNone(string line) => Hits("page.md", [line]).ShouldBeEmpty();

    // Each phrase that a line holds, as `file:line: phrase`, with the first line 1.
    internal static IEnumerable<string> Hits(string file, IEnumerable<string> lines) =>
        lines.SelectMany(
            (text, index) =>
                Phrases
                    .Where(phrase => text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                    .Select(phrase => $"{file}:{index + 1}: {phrase}")
        );
}
