using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SqlSource.Tool.Describing.Postgres;

namespace SqlSource.Tool.Bundles.Renaming.Postgres;

/// <summary>
/// PostgreSQL's words for the renaming: its rule of a word, how it folds a spelling, and which words are its own and
/// kept verbatim.
/// </summary>
/// <remarks>
/// The vocabulary is <c>postgres-vocabulary.txt</c>, embedded in the tool and read once, which
/// <c>VocabularyTests</c> of <c>tests/DatabaseTests/Postgres</c> makes from the containers of the floor and default
/// images, merged as a union, and checks against them.
/// </remarks>
internal static class PostgresWords
{
    private const string VocabularyResource = "SqlSource.Tool.Bundles.Renaming.Postgres.postgres-vocabulary.txt";

    /// <summary>What starts a line of the vocabulary's header.</summary>
    public const string HeaderMark = "# ";

    /// <summary>
    /// The committed vocabulary, each word folded: <c>pg_get_keywords()</c>' words, <c>pg_catalog</c>'s functions,
    /// operators and types, <c>information_schema</c>'s types, and the system columns.
    /// </summary>
    public static IReadOnlySet<string> Vocabulary { get; } = ReadVocabulary();

    /// <summary>The schemas that are the engine's own, whose names are kept.</summary>
    public static IReadOnlySet<string> EngineSchemas { get; } =
        FrozenSet.ToFrozenSet(
            [SystemSchemas.Postgres, "information_schema", "pg_toast", "public"],
            StringComparer.Ordinal
        );

    /// <summary>Whether a character starts a word, by PostgreSQL's lexer.</summary>
    public static bool IsWordStart(char character) => PostgresIdentifiers.IsWordStart(character);

    /// <summary>Whether a character goes on with a word, by PostgreSQL's lexer.</summary>
    public static bool IsWordPart(char character) => PostgresIdentifiers.IsWordPart(character);

    /// <summary>
    /// A spelling as PostgreSQL compares it: a quoted one as it is, and an unquoted one with <c>A</c> to <c>Z</c>
    /// lower-cased and every other character kept, as the server folds an identifier.
    /// </summary>
    public static string Fold(string spelling, bool quoted)
    {
        if (quoted)
        {
            return spelling;
        }

        return string.Create(
            spelling.Length,
            spelling,
            static (folded, spelling) =>
            {
                for (var index = 0; index < spelling.Length; index++)
                {
                    var character = spelling[index];
                    folded[index] = char.IsAsciiLetterUpper(character) ? (char)(character | 0x20) : character;
                }
            }
        );
    }

    /// <summary>
    /// Whether a spelling, once folded, is the engine's: a word of <see cref="Vocabulary" />, one of
    /// <see cref="EngineSchemas" />, or a name the planner makes.
    /// </summary>
    public static bool IsEngineWord(string spelling, bool quoted)
    {
        var folded = Fold(spelling, quoted);
        return Vocabulary.Contains(folded) || EngineSchemas.Contains(folded) || PostgresPlannerNames.Is(folded);
    }

    // One word a line.  A line of the header starts with "# ", since an operator such as # or #>> starts with #
    // alone and no word holds a space; a blank line is skipped too.
    private static FrozenSet<string> ReadVocabulary()
    {
        using var stream =
            Assembly.GetExecutingAssembly().GetManifestResourceStream(VocabularyResource)
            ?? throw new InvalidOperationException($"The resource '{VocabularyResource}' is not in the tool.");
        using var reader = new StreamReader(stream);
        var words = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith(HeaderMark, StringComparison.Ordinal))
            {
                words.Add(line);
            }
        }

        return words.ToFrozenSet(StringComparer.Ordinal);
    }
}
