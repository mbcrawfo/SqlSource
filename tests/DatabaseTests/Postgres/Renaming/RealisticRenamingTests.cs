using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.Postgres.Realistic;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing.Bundles;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Renaming;

// The realistic corpus renamed: each case the server runs, described live on the database "realistic" through a
// capturing exchange, made a raw bundle as diagnose makes one, of its entry or of the failure of a case the describer
// refuses, then renamed with PostgreSQL's part, whose replay proves it.  A case is captured and renamed once in a run,
// so that the word check reads the mappings the theory made.
[Collection(UsesPostgres.Name)]
public sealed class RealisticRenamingTests(PostgresDatabases databases)
{
    // A query of a set operation, whose arms the planner names before PostgreSQL 18.
    private const string IntersectCase = "intersect-accounts-with-orders-and-events";

    // What the planner names an arm of a set operation in a plan before PostgreSQL 18, followed by its number.
    private const string SetName = "*SELECT* ";

    // The first major whose plan names no arm of a set operation so.
    private const int NoSetNameFrom = 18;

    // The names of the server's words that a name of the user's may not be: its keywords, folded as unquoted, and
    // its catalog's functions and types, as the catalog holds them.
    private static readonly (string Sql, bool Quoted)[] ServerWords =
    [
        ("SELECT word FROM pg_catalog.pg_get_keywords()", false),
        (
            "SELECT DISTINCT p.proname FROM pg_catalog.pg_proc p"
                + " JOIN pg_catalog.pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname = 'pg_catalog'",
            true
        ),
        (
            "SELECT DISTINCT t.typname FROM pg_catalog.pg_type t"
                + " JOIN pg_catalog.pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname = 'pg_catalog'",
            true
        ),
    ];

    // Each case's raw bundle and its renaming, by the case's name, made once in a run on the one container.
    private static readonly ConcurrentDictionary<string, (Bundle Raw, RenamedBundle Renamed)> Renamings = new(
        StringComparer.Ordinal
    );

    public static TheoryData<string> Cases => [with(RealisticCases.Corpus.Names)];

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RenameAsync_Case_ReplaysWithTheOriginalsProvenanceAndLeaksNothing(string name)
    {
        var found = RealisticCases.Corpus.ReadNamed(name);
        found.SkipUnlessRunsOn(databases.Major);

        var (raw, renamed) = await RenamedAsync(found);

        var outcome = raw.Info.ShouldNotBeNull().Outcome;
        outcome.Id.ShouldBe(found.Refusal);
        if (raw.Expected is { } expected)
        {
            Provenance.Of(renamed.Bundle.Expected.ShouldNotBeNull()).ShouldBe(Provenance.Of(expected));
        }
        else
        {
            renamed.Bundle.Info.ShouldNotBeNull().Outcome.ShouldBe(outcome);
        }

        var schema = await File.ReadAllTextAsync(RealisticCases.Corpus.Schema, TestContext.Current.CancellationToken);
        PostgresBundleLeaks
            .Of(renamed.Bundle, [schema, found.AsFixture.Query], PostgresWords.IsEngineWord)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task RenameAsync_Intersect_KeepsThePlannersSetName()
    {
        Assert.SkipUnless(
            databases.Major < NoSetNameFrom,
            $"PostgreSQL {NoSetNameFrom} and later name no arm of a set operation '{SetName}<n>'."
        );

        var (raw, renamed) = await RenamedAsync(RealisticCases.Corpus.ReadNamed(IntersectCase));

        PlanOf(raw).ShouldContain(SetName);
        PlanOf(renamed.Bundle).ShouldContain(SetName);
    }

    // A name the renaming replaced that is a word of the running server is one the committed vocabulary lacks.  A
    // literal is replaced whatever it spells, so it is no name to check.
    [Fact]
    public async Task RenamedWords_AreNoWordOfTheServer()
    {
        var words = await ServerWordsAsync();
        var originals = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var found in RealisticCases.Corpus.All.Where(found => found.RunsOn(databases.Major)))
        {
            var (_, renamed) = await RenamedAsync(found);
            originals.UnionWith(
                renamed
                    .Mapping.Where(static mapped => mapped.Class != NameClass.Literal)
                    .Select(static mapped => PostgresWords.Fold(mapped.Original, quoted: false))
            );
        }

        originals.ShouldNotBeEmpty();
        originals
            .Where(words.Contains)
            .ShouldBeEmpty(
                $"{EngineImage.Postgres.Image} has words that the renaming replaced; the committed vocabulary lacks"
                    + " them, and VocabularyTests' explicit test adds them."
            );
    }

    // The case's raw bundle, captured live, and its renaming, which must succeed.
    private async Task<(Bundle Raw, RenamedBundle Renamed)> RenamedAsync(RealisticCase found)
    {
        if (Renamings.TryGetValue(found.Name, out var kept))
        {
            return kept;
        }

        var raw = await RawAsync(found);
        var renamed = await BundleRenamings.RenamedAsync(
            raw,
            PostgresRenaming.Instance,
            new PostgresDescriber(),
            BundleRenamings.Key(BundleRenamings.AnyKey)
        );
        return Renamings.GetOrAdd(found.Name, (raw, renamed));
    }

    // The case described through a capturing exchange, as diagnose captures one: its entry, or the failure's id and
    // step for a case the describer refuses.  No realistic case has planner settings, as the corpus is described with
    // none.
    private Task<Bundle> RawAsync(RealisticCase found) =>
        RawBundles.CaptureAsync(
            found.AsFixture,
            static _ => new PostgresDescriber(),
            databases.RealisticConnectionString,
            LiveExchange.Instance
        );

    // The answers of the capture's calls of explain, as their JSON holds them.
    private static string PlanOf(Bundle bundle)
    {
        var read = CaptureJson.Read(bundle.Capture);
        var capture = read.Value.ShouldNotBeNull(read.Failure?.ToString());
        return string.Join(
            '\n',
            capture
                .Calls.Where(static call => call.Method == PostgresCallNames.Explain)
                .Select(static call => call.Answer.GetRawText())
        );
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL is a constant of this test."
    )]
    private async Task<HashSet<string>> ServerWordsAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = databases.SourceOf(PostgresDatabases.RealisticDatabase);
        await using var connection = await source.OpenConnectionAsync(token);
        var words = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (sql, quoted) in ServerWords)
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
