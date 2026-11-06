using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Repository;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Renaming;

// The renaming's committed vocabulary of PostgreSQL, src/SqlSource.Tool/Bundles/Renaming/Postgres/
// postgres-vocabulary.txt: the words of the floor and default images, merged as a union.  The check runs on those two
// images alone; the explicit test adds the words of the run's image to the list, on each of the two in turn.  See
// "The renaming's vocabulary" in tests/DatabaseTests/Postgres/README.md.
[Collection(UsesPostgres.Name)]
public sealed class VocabularyTests(PostgresDatabases databases)
{
    // The oldest supported image, which CI runs and whose words the list holds beside the default's.
    private const string FloorImage = "postgres:14.24";

    private const string RecordCommand =
        "SQLSOURCE_TESTS_POSTGRES_IMAGE=<image> dotnet test --project tests/DatabaseTests/Postgres -- --filter-class"
        + " \"SqlSource.DatabaseTests.Postgres.Renaming.VocabularyTests\" --explicit only";

    // The five sources of the list, each read whole.  A keyword is folded as unquoted, as the user writes one, and a
    // catalog's name is kept as the catalog holds it.
    private static readonly (string Sql, bool Quoted)[] Sources =
    [
        ("SELECT word FROM pg_catalog.pg_get_keywords()", false),
        (
            "SELECT DISTINCT p.proname FROM pg_catalog.pg_proc p"
                + " JOIN pg_catalog.pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname = 'pg_catalog'",
            true
        ),
        (
            "SELECT DISTINCT o.oprname FROM pg_catalog.pg_operator o"
                + " JOIN pg_catalog.pg_namespace n ON n.oid = o.oprnamespace WHERE n.nspname = 'pg_catalog'",
            true
        ),
        (
            "SELECT DISTINCT t.typname FROM pg_catalog.pg_type t"
                + " JOIN pg_catalog.pg_namespace n ON n.oid = t.typnamespace"
                + " WHERE n.nspname IN ('pg_catalog', 'information_schema')",
            true
        ),
        (
            "SELECT a.attname FROM pg_catalog.pg_attribute a"
                + " WHERE a.attrelid = 'pg_catalog.pg_class'::pg_catalog.regclass AND a.attnum < 0",
            true
        ),
    ];

    // The header of the file, which says what it holds and how it is made.  Each line starts with
    // PostgresWords.HeaderMark.
    private static readonly string[] Header =
    [
        "PostgreSQL's words, which the renaming of a bundle keeps verbatim: pg_get_keywords()' words, pg_catalog's",
        "functions, operators and types, information_schema's types and the system columns of pg_class, each folded,",
        "one a line, sorted by ordinal.  Made by the explicit test of",
        "tests/DatabaseTests/Postgres/Renaming/VocabularyTests.cs on postgres:14.24 and on the default image,",
        "merged as a union, never by hand; see \"The renaming's vocabulary\" in",
        "tests/DatabaseTests/Postgres/README.md.",
    ];

    private static string ListPath =>
        RepositoryRoot.Path("src", "SqlSource.Tool", "Bundles", "Renaming", "Postgres", "postgres-vocabulary.txt");

    [Fact]
    public async Task Vocabulary_OfThisImage_IsInTheCommittedList()
    {
        SkipUnlessListed();

        var missing = (await ImageWordsAsync()).Where(word => !PostgresWords.Vocabulary.Contains(word)).ToList();

        var image = EngineImage.Postgres.Image;
        missing.ShouldBeEmpty(
            $"{image} has words the committed vocabulary lacks: {string.Join(' ', missing)}.  "
                + $"Add them with {RecordCommand.Replace("<image>", image, StringComparison.Ordinal)}"
        );
    }

    [Fact(Explicit = true)]
    public async Task Record_TheImagesWords_MergesThemIntoTheList()
    {
        SkipUnlessListed();
        var token = TestContext.Current.CancellationToken;

        var words = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var line in await File.ReadAllLinesAsync(ListPath, token))
        {
            if (
                !string.IsNullOrWhiteSpace(line) && !line.StartsWith(PostgresWords.HeaderMark, StringComparison.Ordinal)
            )
            {
                _ = words.Add(line);
            }
        }

        words.UnionWith(await ImageWordsAsync());

        var text = new StringBuilder();
        foreach (var line in Header)
        {
            _ = text.Append(PostgresWords.HeaderMark).Append(line).Append('\n');
        }

        foreach (var word in words)
        {
            _ = text.Append(word).Append('\n');
        }

        await File.WriteAllTextAsync(ListPath, text.ToString(), token);
    }

    // The list holds the words of these two images alone, so the check means nothing on another.
    private static void SkipUnlessListed() =>
        Assert.SkipUnless(
            EngineImage.Postgres.Image is FloorImage or DefaultImages.Postgres,
            $"The vocabulary is made from {FloorImage} and {DefaultImages.Postgres} alone."
        );

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL is a constant of this test."
    )]
    private async Task<SortedSet<string>> ImageWordsAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = databases.SourceOf(PostgresDatabases.Database);
        await using var connection = await source.OpenConnectionAsync(token);
        var words = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (sql, quoted) in Sources)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                _ = words.Add(PostgresWords.Fold(reader.GetString(0), quoted));
            }
        }

        return words;
    }
}
