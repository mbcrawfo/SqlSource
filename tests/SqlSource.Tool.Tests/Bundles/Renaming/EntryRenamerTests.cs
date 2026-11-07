using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot.Reading;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming;

// The entry of expected.json renamed by the mapping of the capture and the query.
public sealed class EntryRenamerTests
{
    private static RenamingRun Run(FakeEngineRenaming engine, string query) =>
        RenamingRun.For(engine, new NameMapping(RenamingBundles.Key(1), engine.Fold), query, RenamingCopies.None);

    [Fact]
    public async Task Rename_HashIsOfTheRenamedQuery()
    {
        var token = TestContext.Current.CancellationToken;
        var raw = await RenamingBundles.RawAsync();
        var engine = new FakeEngineRenaming();
        var run = Run(engine, raw.Query);
        var query = run.RenameQueryFile(raw.Query);

        var renamed = EntryRenamer.Rename(engine, run, raw.Expected.ShouldNotBeNull(), query, token);

        run.Refusal.ShouldBeNull();
        var entry = SidecarReader.Read(renamed.ShouldNotBeNull()).Sidecar.ShouldNotBeNull().Queries[0];
        var planned = BundleReplay.Plan(
            raw with
            {
                Query = query,
            },
            RenamingBundles.Engine,
            RenamingBundles.Path,
            token
        );
        entry.Hash.ShouldBe(planned.Value.Query.Hash);
        var original = SidecarReader.Read(raw.Expected).Sidecar.ShouldNotBeNull().Queries[0];
        entry.Hash.ShouldNotBe(original.Hash);
        entry.Name.ShouldBe(run.Mapping.Name(RenamingBundles.Name, quoted: false, NameClass.Query));
    }

    [Fact]
    public async Task Rename_MemberWithNoRule_IsRefusedAtItsPlace()
    {
        var token = TestContext.Current.CancellationToken;
        var raw = await RenamingBundles.RawAsync();
        var engine = new FakeEngineRenaming();
        var run = Run(engine, raw.Query);
        var expected = raw
            .Expected.ShouldNotBeNull()
            .Replace("\"resultKind\"", "\"note\": \"secret\",\n\"resultKind\"", System.StringComparison.Ordinal);

        _ = EntryRenamer.Rename(engine, run, expected, run.RenameQueryFile(raw.Query), token);

        run.Refusal.ShouldBe(new RenamingRefusal(RefusalStep.Token, "expected.json note", "secret"));
    }
}
