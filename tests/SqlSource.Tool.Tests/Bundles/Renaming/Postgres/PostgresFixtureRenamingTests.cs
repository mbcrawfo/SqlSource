using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Testing.Bundles;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.Postgres;

// Every case of PostgreSQL's describer fixtures, as a raw bundle, renamed: it replays with the original's provenance,
// holds no name or literal of the schema or the query, and gives other names under another key.
public sealed class PostgresFixtureRenamingTests
{
    // The cases the renaming must refuse, each with why.  None is: a case refused is a bug of the renaming.
    private static readonly FrozenDictionary<string, string> MustBeRefused = FrozenDictionary<string, string>.Empty;

    public static TheoryData<string> Cases => [with(FixtureCases.Of(PostgresRenamings.Engine).Select(Name))];

    public static TheoryData<string> Renamed =>
        [with(FixtureCases.Of(PostgresRenamings.Engine).Select(Name).Where(name => !MustBeRefused.ContainsKey(name)))];

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RenameAsync_Fixture_ReplaysWithTheOriginalsProvenance(string name)
    {
        var raw = RawBundles.OfFixture(PostgresRenamings.Fixture(name));

        var renamed = await PostgresRenamings.RenameAsync(raw);

        if (MustBeRefused.ContainsKey(name))
        {
            _ = renamed.Failure.ShouldNotBeNull();
            return;
        }

        var result = renamed.Value.ShouldNotBeNull(renamed.Failure?.ToString());
        Provenance.Of(result.Bundle.Expected.ShouldNotBeNull()).ShouldBe(Provenance.Of(raw.Expected!));
    }

    [Theory]
    [MemberData(nameof(Renamed))]
    public async Task RenameAsync_Fixture_LeaksNothing(string name)
    {
        var fixture = PostgresRenamings.Fixture(name);

        var renamed = await PostgresRenamings.RenamedAsync(RawBundles.OfFixture(fixture));

        PostgresBundleLeaks
            .Of(renamed.Bundle, [PostgresRenamings.Schema, fixture.Query], PostgresWords.IsEngineWord)
            .ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(Renamed))]
    public async Task RenameAsync_TwiceWithTwoKeys_GivesOtherNamesAndTheSameProvenance(string name)
    {
        var raw = RawBundles.OfFixture(PostgresRenamings.Fixture(name));

        var first = await PostgresRenamings.RenamedAsync(raw, key: 1);
        var second = await PostgresRenamings.RenamedAsync(raw, key: 2);

        first.Mapping.ShouldNotBeEmpty();
        var replacements = new HashSet<string>(first.Mapping.Select(Replacement), StringComparer.Ordinal);
        second.Mapping.Select(Replacement).Where(replacements.Contains).ShouldBeEmpty();
        Provenance.Of(second.Bundle.Expected!).ShouldBe(Provenance.Of(first.Bundle.Expected!));
    }

    private static string Name(FixtureCase fixture) => fixture.Name;

    private static string Replacement(MappedName mapped) => mapped.Replacement;
}
