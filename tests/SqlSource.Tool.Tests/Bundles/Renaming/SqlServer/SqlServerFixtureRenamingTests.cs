using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing.Bundles;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.SqlServer;

// Every fixture of the SQL Server describer as a raw bundle, renamed by SQL Server's part: the proof inside the
// renaming replays it to its renamed entry, the entry keeps the original's provenance, and nothing of the names and
// literals of the schema and the query is left in a string the renaming renames.
public sealed class SqlServerFixtureRenamingTests
{
    public static IEnumerable<TheoryDataRow<string>> Cases =>
        FixtureCases.Of(SqlServerRenamings.Engine).Select(static found => new TheoryDataRow<string>(found.Name));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RenameAsync_Fixture_GivesABundleThatReplaysToItsRenamedEntry(string name)
    {
        var raw = SqlServerRenamings.Raw(name);

        var renamed = await SqlServerRenamings.RenamedAsync(raw);

        renamed.Bundle.Info.ShouldBe(raw.Info.ShouldNotBeNull() with { Renamed = true });
        _ = renamed.Bundle.Expected.ShouldNotBeNull();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RenameAsync_Fixture_KeepsTheOriginalsProvenance(string name)
    {
        var raw = SqlServerRenamings.Raw(name);

        var renamed = await SqlServerRenamings.RenamedAsync(raw);

        Provenance
            .Of(renamed.Bundle.Expected.ShouldNotBeNull())
            .ShouldBe(Provenance.Of(raw.Expected.ShouldNotBeNull()));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RenameAsync_Fixture_LeaksNoNameOrLiteralOfTheUsers(string name)
    {
        var raw = SqlServerRenamings.Raw(name);
        var schema = await SqlServerRenamings.SchemaAsync();

        var renamed = await SqlServerRenamings.RenamedAsync(raw);

        SqlServerBundleLeaks.Find(renamed.Bundle, SqlServerRenamings.Part, raw, schema, raw.Query).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RenameAsync_FixtureWithTwoKeys_GivesOtherNamesAndTheSameProvenance(string name)
    {
        var raw = SqlServerRenamings.Raw(name);

        var first = await SqlServerRenamings.RenamedAsync(raw, BundleRenamings.Key(1));
        var second = await SqlServerRenamings.RenamedAsync(raw, BundleRenamings.Key(2));

        second.Bundle.Query.ShouldNotBe(first.Bundle.Query);
        Provenance
            .Of(second.Bundle.Expected.ShouldNotBeNull())
            .ShouldBe(Provenance.Of(first.Bundle.Expected.ShouldNotBeNull()));
    }

    // msdb.dbo.sysjobs is a table of dbo, renamed as a user's table of a system database is, and master.sys.databases
    // a view of sys, kept.
    [Fact]
    public async Task RenameAsync_RelationsOfSystemDatabases_KeepOnlyTheNamesOfSys()
    {
        var raw = SqlServerRenamings.Raw("select-other-database");

        var renamed = await SqlServerRenamings.RenamedAsync(raw);

        foreach (var name in new[] { "sysjobs", "job_id" })
        {
            renamed.Bundle.Query.ShouldNotContain(name, Case.Insensitive);
            renamed.Bundle.Capture.ShouldNotContain(name, Case.Insensitive);
        }

        renamed.Bundle.Query.ShouldContain("msdb.dbo.");
        renamed.Bundle.Query.ShouldContain("master.sys.databases");
        renamed.Bundle.Query.ShouldContain("database_id");
    }
}
