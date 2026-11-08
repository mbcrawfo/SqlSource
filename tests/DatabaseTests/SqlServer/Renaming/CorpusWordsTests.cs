using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SqlSource.DatabaseTests.SqlServer.Realistic;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Parsing;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.SqlServer;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Renaming;

// Every word of SQL Server's two corpora, its describer fixtures and its realistic cases, is classified: the engine's,
// which SqlServerVocabulary holds, or the user's, which CorpusUserWords holds.  The renaming keeps an engine word
// verbatim and renames a user's, and a renamed engine word still replays, so only this test finds an engine word that
// the vocabulary lacks.  A word is each word of the query's plain text by the engine's word rule, a leading @ or #
// trimmed and an @@ word whole, and each bracketed or double-quoted name; comments, literals and parameters are left
// out.  The class needs no server.
public sealed class CorpusWordsTests
{
    private static readonly SqlServerRenaming Engine = SqlServerRenaming.Instance;

    [Fact]
    public void EveryWord_OfTheCorpora_IsTheEnginesOrTheUsers()
    {
        var unclassified = WordsByCase()
            .Where(pair =>
                !Engine.IsEngineWord(pair.Key, quoted: false)
                && !CorpusUserWords.Words.Contains(Folded(pair.Key))
                && !CorpusUserWords.EngineRowNames.Contains(Folded(pair.Key))
            )
            .Select(pair => $"{pair.Key} ({string.Join(", ", pair.Value.Take(3))})")
            .ToList();

        Assert.True(unclassified.Count == 0, "Neither the engine's nor the user's: " + string.Join("; ", unclassified));
    }

    [Fact]
    public void EveryUserWord_IsNoEngineWord()
    {
        var engines = CorpusUserWords.Spellings.Where(word => Engine.IsEngineWord(word, quoted: false)).ToList();

        Assert.True(engines.Count == 0, "Engine words among the user's: " + string.Join(", ", engines));
    }

    [Fact]
    public void EveryUserWord_IsInSomeCase()
    {
        var present = WordsByCase().Keys.ToHashSet(StringComparer.Ordinal);
        var absent = CorpusUserWords.Spellings.Where(word => !present.Contains(Folded(word))).ToList();

        Assert.True(absent.Count == 0, "User words in no case: " + string.Join(", ", absent));
    }

    [Fact]
    public void EveryEngineRowName_IsInSomeCase_AndNoEngineWord()
    {
        var present = WordsByCase().Keys.ToHashSet(StringComparer.Ordinal);
        var misplaced = CorpusUserWords
            .EngineRowNames.Where(word => !present.Contains(word) || Engine.IsEngineWord(word, quoted: false))
            .ToList();

        Assert.True(
            misplaced.Count == 0,
            "Engine row names in no case, or engine words: " + string.Join(", ", misplaced)
        );
    }

    [Fact]
    public void UserWords_AreSortedAndDistinct()
    {
        var spelled = CorpusUserWords.Spellings;

        Assert.Equal(spelled.OrderBy(static word => word, StringComparer.OrdinalIgnoreCase), spelled);
        Assert.Equal(spelled.Count, CorpusUserWords.Words.Count);
    }

    private static string Folded(string word) => Engine.Fold(word, quoted: false);

    // Each folded word of the corpora, with the cases that hold it.
    private static Dictionary<string, List<string>> WordsByCase()
    {
        var words = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (name, query) in QueriesOfCorpora())
        {
            foreach (var word in WordsOf(query))
            {
                var key = Folded(word);
                if (!words.TryGetValue(key, out var cases))
                {
                    words[key] = cases = [];
                }

                if (!cases.Contains(name))
                {
                    cases.Add(name);
                }
            }
        }

        return words;
    }

    private static IEnumerable<(string Name, string Query)> QueriesOfCorpora()
    {
        var fixtures = DescriberFixtures.SqlServer.Folder;
        foreach (var folder in Directory.GetDirectories(fixtures).Order(StringComparer.Ordinal))
        {
            yield return ("fixture " + Path.GetFileName(folder), ReadQuery(folder));
        }

        foreach (var realistic in RealisticCases.Corpus.All)
        {
            yield return ("realistic " + realistic.Name, ReadQuery(realistic.Folder));
        }
    }

    private static string ReadQuery(string folder) => File.ReadAllText(Path.Combine(folder, RealisticCase.QueryFile));

    // The words of a query, each spelled as the query spells it, once for each occurrence.
    private static IEnumerable<string> WordsOf(string source)
    {
        var lexed = SqlLexer.Lex(source, SqlDialectRules.For(Engine.Dialect));
        if (lexed.Error is { } error)
        {
            throw new InvalidOperationException($"A query of the corpora does not lex: {error.Kind} at {error.Span}.");
        }

        var lexemes = lexed.Lexemes;
        for (var index = 0; index < lexemes.Count; index++)
        {
            var lexeme = lexemes[index];
            var (start, end) = (lexeme.Span.Start, lexeme.Span.End);
            switch (lexeme.Kind)
            {
                case SqlLexemeKind.Text:
                case SqlLexemeKind.Hint:
                    var quoteFollows = index + 1 < lexemes.Count && lexemes[index + 1].Kind == SqlLexemeKind.Quoted;
                    foreach (var word in PlainWordsOf(source, start, end, quoteFollows))
                    {
                        yield return word;
                    }

                    break;
                case SqlLexemeKind.Quoted:
                    if (NameOf(source[start..end]) is { } name)
                    {
                        yield return name;
                    }

                    break;
                case SqlLexemeKind.Parameter:
                case SqlLexemeKind.LineComment:
                case SqlLexemeKind.BlockComment:
                    break;
                default:
                    throw new InvalidOperationException($"A lexeme of the kind {lexeme.Kind} is not classified.");
            }
        }
    }

    private static IEnumerable<string> PlainWordsOf(string text, int start, int end, bool quoteFollows)
    {
        var at = start;
        while (at < end)
        {
            var character = text[at];
            if (SqlRenamer.NumberAt(Engine, text, at, end) is (var runEnd, _))
            {
                at = runEnd;
            }
            else if (Engine.IsWordStart(character))
            {
                var wordStart = at;
                at++;
                while (at < end && Engine.IsWordPart(text[at]))
                {
                    at++;
                }

                var word = text[wordStart..at];
                var isPrefix =
                    quoteFollows
                    && word.Length == 1
                    && SqlRenamer.LiteralPrefixes.Contains(word[0], StringComparison.Ordinal)
                    && (at == end || (at + 1 == end && text[at] == '&'));
                if (isPrefix)
                {
                    continue;
                }

                var trimmed = word.StartsWith("@@", StringComparison.Ordinal) ? word : word.TrimStart('@', '#');
                if (trimmed.Length > 0)
                {
                    yield return trimmed;
                }
            }
            else
            {
                at++;
            }
        }
    }

    // A bracketed or double-quoted name, with a nullability mark after its stem left out; null for a literal.
    private static string? NameOf(string quoted)
    {
        var closer = quoted[0] switch
        {
            '"' => "\"",
            '[' => "]",
            _ => null,
        };
        if (closer is null)
        {
            return null;
        }

        var content = quoted[1..^1].Replace(closer + closer, closer, StringComparison.Ordinal);
        return content.Length > 1 && content[^1] is '!' or '?' ? content[..^1] : content;
    }
}
