using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing.Bundles;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.SqlServer;

// Every fixture of the SQL Server describer as a raw bundle, renamed by SQL Server's part: the proof inside the
// renaming replays it to its renamed entry, the entry keeps the original's provenance, and nothing of the names and
// literals of the schema and the query is left in a string the renaming renames.
public sealed class SqlServerFixtureRenamingTests
{
    private const string Engine = "mssql";

    private static IEngineRenaming Part => EngineRenamings.For(DatabaseEngine.SqlServer).ShouldNotBeNull();

    public static IEnumerable<TheoryDataRow<string>> Cases =>
        FixtureCases.Of(Engine).Select(static found => new TheoryDataRow<string>(found.Name));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RenameAsync_Fixture_GivesABundleThatReplaysToItsRenamedEntry(string name)
    {
        var raw = RawOf(name);

        var renamed = await RenamedAsync(raw, key: 1);

        renamed.Bundle.Info.ShouldBe(raw.Info.ShouldNotBeNull() with { Renamed = true });
        _ = renamed.Bundle.Expected.ShouldNotBeNull();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RenameAsync_Fixture_KeepsTheOriginalsProvenance(string name)
    {
        var raw = RawOf(name);

        var renamed = await RenamedAsync(raw, key: 1);

        BundleLeaks
            .ProvenanceOf(renamed.Bundle.Expected.ShouldNotBeNull())
            .ShouldBe(BundleLeaks.ProvenanceOf(raw.Expected.ShouldNotBeNull()));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RenameAsync_Fixture_LeaksNoNameOrLiteralOfTheUsers(string name)
    {
        var raw = RawOf(name);
        var schema = await File.ReadAllTextAsync(
            Path.Combine(FixtureCases.Root, Engine, "schema.sql"),
            TestContext.Current.CancellationToken
        );

        var renamed = await RenamedAsync(raw, key: 1);

        BundleLeaks
            .Find(renamed.Bundle, Part, BundleLeaks.EngineRowNames(raw, Part), schema, raw.Query)
            .ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RenameAsync_FixtureWithTwoKeys_GivesOtherNamesAndTheSameProvenance(string name)
    {
        var raw = RawOf(name);

        var first = await RenamedAsync(raw, key: 1);
        var second = await RenamedAsync(raw, key: 2);

        second.Bundle.Query.ShouldNotBe(first.Bundle.Query);
        BundleLeaks
            .ProvenanceOf(second.Bundle.Expected.ShouldNotBeNull())
            .ShouldBe(BundleLeaks.ProvenanceOf(first.Bundle.Expected.ShouldNotBeNull()));
    }

    // msdb.dbo.sysjobs is a table of dbo, renamed as a user's table of a system database is, and master.sys.databases
    // a view of sys, kept.
    [Fact]
    public async Task RenameAsync_RelationsOfSystemDatabases_KeepOnlyTheNamesOfSys()
    {
        var raw = RawOf("select-other-database");

        var renamed = await RenamedAsync(raw, key: 1);

        foreach (var name in new[] { "sysjobs", "job_id" })
        {
            renamed.Bundle.Query.ShouldNotContain(name, Case.Insensitive);
            renamed.Bundle.Capture.ShouldNotContain(name, Case.Insensitive);
        }

        renamed.Bundle.Query.ShouldContain("msdb.dbo.");
        renamed.Bundle.Query.ShouldContain("master.sys.databases");
        renamed.Bundle.Query.ShouldContain("database_id");
    }

    private static Bundle RawOf(string name) =>
        RawBundles.OfFixture(FixtureCases.Of(Engine).Single(found => found.Name == name));

    private static async Task<RenamedBundle> RenamedAsync(Bundle raw, byte key)
    {
        var renamed = await BundleRenamer.RenameAsync(
            raw,
            Part,
            new SqlServerDescriber(),
            RenamingBundles.Key(key),
            TestContext.Current.CancellationToken
        );
        renamed.Failure.ShouldBeNull();
        return renamed.Value.ShouldNotBeNull();
    }
}
