using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.SqlServer.Realistic;
using SqlSource.Settings;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing.Bundles;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Renaming;

// Every case of SQL Server's realistic corpus captured on the server as diagnose captures one, then renamed by SQL
// Server's part: the proof inside the renaming replays the renamed bundle to its renamed answer, a described case
// keeps the original's provenance, a refused one keeps the server's failure, its id and its step, and nothing of the
// names and literals of the corpus's schema, its seed and the case's query is left in a string the renaming renames.
// Run on the floor image and on the default one.
[Collection(UsesSqlServer.Name)]
public sealed class RealisticRenamingTests(SqlServerDatabases sqlServer)
{
    private static IEngineRenaming Part => EngineRenamings.For(DatabaseEngine.SqlServer).ShouldNotBeNull();

    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public async Task RenameAsync_RealisticCase_ReplaysToItsRenamedAnswerAndLeaksNothing(string name)
    {
        var token = TestContext.Current.CancellationToken;
        var found = RealisticCases.Corpus.ReadNamed(name);
        found.SkipUnlessRunsOn(sqlServer.Major);
        var raw = await RawBundles.CaptureAsync(
            found.AsFixture,
            static _ => new SqlServerDescriber(),
            sqlServer.RealisticConnectionString,
            LiveExchange.Instance
        );
        var info = raw.Info.ShouldNotBeNull();
        info.Outcome.ShouldBe(
            found.Refusal is { } refusal ? BundleOutcome.Failure(refusal, info.Outcome.Step) : BundleOutcome.Entry
        );

        var renamed = await BundleRenamer.RenameAsync(
            raw,
            Part,
            new SqlServerDescriber(),
            RenamingKey.Of(Enumerable.Repeat((byte)1, RenamingKey.Length).ToArray()),
            token
        );

        renamed.Failure.ShouldBeNull();
        var bundle = renamed.Value.ShouldNotBeNull().Bundle;
        bundle.Info.ShouldBe(info with { Renamed = true });
        if (raw.Expected is { } expected)
        {
            BundleLeaks.ProvenanceOf(bundle.Expected.ShouldNotBeNull()).ShouldBe(BundleLeaks.ProvenanceOf(expected));
        }
        else
        {
            bundle.Expected.ShouldBeNull();
        }

        var schema = await File.ReadAllTextAsync(RealisticCases.Corpus.Schema, token);
        var seed = await File.ReadAllTextAsync(RealisticCases.Corpus.Seed, token);
        BundleLeaks.Find(bundle, Part, BundleLeaks.EngineRowNames(raw, Part), schema, seed, raw.Query).ShouldBeEmpty();
    }
}
