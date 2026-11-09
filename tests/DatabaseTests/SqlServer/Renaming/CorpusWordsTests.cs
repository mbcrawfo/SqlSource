using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.DatabaseTests.SqlServer.Realistic;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Parsing;
using SqlSource.Tool.Bundles.Renaming.SqlServer;
using SqlSource.Tool.Testing.Bundles;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Renaming;

// Every word of SQL Server's two corpora, its describer fixtures and its realistic cases, is classified: the engine's,
// which SqlServerVocabulary holds, or the user's, which CorpusUserWords holds.  The renaming keeps an engine word
// verbatim and renames a user's, and a renamed engine word still replays, so only this test finds an engine word that
// the vocabulary lacks.  A word is each word of the query's plain text by the engine's word rule, a leading @ or #
// trimmed and an @@ word whole, and each bracketed or double-quoted name, as SqlServerBundleLeaks reads them for the
// leak search; comments, literals and parameters are left out.  The class needs no server.
public sealed class CorpusWordsTests
{
    private static readonly SqlServerRenaming Engine = SqlServerRenaming.Instance;

    [Fact]
    public void WordsByCase_EveryWord_IsTheEnginesOrTheUsers()
    {
        var unclassified = WordsByCase()
            .Where(pair =>
                !Engine.IsEngineWord(pair.Key, quoted: false)
                && !CorpusUserWords.Words.Contains(Folded(pair.Key))
                && !CorpusUserWords.EngineRowNames.Contains(Folded(pair.Key))
            )
            .Select(pair => $"{pair.Key} ({string.Join(", ", pair.Value.Take(3))})")
            .ToList();

        unclassified.ShouldBeEmpty("Neither the engine's nor the user's: " + string.Join("; ", unclassified));
    }

    [Fact]
    public void CorpusUserWords_EverySpelling_IsNoEngineWord()
    {
        var engines = CorpusUserWords.Spellings.Where(word => Engine.IsEngineWord(word, quoted: false)).ToList();

        engines.ShouldBeEmpty("Engine words among the user's: " + string.Join(", ", engines));
    }

    [Fact]
    public void CorpusUserWords_EverySpelling_IsInSomeCase()
    {
        var present = WordsByCase().Keys.ToHashSet(StringComparer.Ordinal);
        var absent = CorpusUserWords.Spellings.Where(word => !present.Contains(Folded(word))).ToList();

        absent.ShouldBeEmpty("User words in no case: " + string.Join(", ", absent));
    }

    [Fact]
    public void CorpusUserWords_EngineRowNames_AreInSomeCaseAndNoEngineWord()
    {
        var present = WordsByCase().Keys.ToHashSet(StringComparer.Ordinal);
        var misplaced = CorpusUserWords
            .EngineRowNames.Where(word => !present.Contains(word) || Engine.IsEngineWord(word, quoted: false))
            .ToList();

        misplaced.ShouldBeEmpty("Engine row names in no case, or engine words: " + string.Join(", ", misplaced));
    }

    [Fact]
    public void CorpusUserWords_Spellings_AreSortedAndDistinct()
    {
        var spelled = CorpusUserWords.Spellings;

        spelled.ShouldBe(spelled.OrderBy(static word => word, StringComparer.OrdinalIgnoreCase));
        CorpusUserWords.Words.Count.ShouldBe(spelled.Count);
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
                    var words = SqlServerBundleLeaks.PlainWordsOf(
                        source,
                        start,
                        end,
                        quoteFollows,
                        Engine,
                        keepsSystemFunctions: true
                    );
                    foreach (var word in words)
                    {
                        yield return word;
                    }

                    break;
                case SqlLexemeKind.Quoted:
                    if (SqlServerBundleLeaks.QuotedNameOf(source[start..end]) is { } name)
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
}
