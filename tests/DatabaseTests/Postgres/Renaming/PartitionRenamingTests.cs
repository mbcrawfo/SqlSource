using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.Postgres.Describer;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Snapshot;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Bundles;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Renaming;

// A partitioned table whose partitions are named as the planner names their scans, scratch_parts_1 and
// scratch_parts_2 of scratch_parts, described live on the database "fixtures" through a capturing exchange, made a
// raw bundle as diagnose makes one, then renamed with PostgreSQL's part, whose replay proves it.  The members' aliases
// are the partitions' own names, which the catalog's answers map as tables, and the renaming must still keep them
// one prefix, or the replay reads the Append as a set operation.
[Collection(UsesPostgres.Name)]
public sealed class PartitionRenamingTests(PostgresDatabases databases)
{
    // The table scanned without an alias, so that its partitions' scans are scratch_parts_1 and scratch_parts_2, and
    // the members of a UNION ALL of the partitions themselves, scanned under their own names, on the null-extended
    // side of a LEFT JOIN, whose nullability the walk reads from the partitions.
    public static TheoryData<string> Queries =>
        [
            "SELECT u.name, scratch_parts.note FROM users u LEFT JOIN scratch_parts ON scratch_parts.id = u.id",
            "SELECT u.name, x.note FROM users u LEFT JOIN (SELECT scratch_parts_1.id, scratch_parts_1.note FROM"
                + " scratch_parts_1 UNION ALL SELECT scratch_parts_2.id, scratch_parts_2.note FROM scratch_parts_2) x"
                + " ON x.id = u.id",
        ];

    [Theory]
    [MemberData(nameof(Queries))]
    public async Task RenameAsync_MembersNamedAsTheirPartitions_ReplaysWithTheOriginalsProvenanceAndLeaksNothing(
        string sql
    )
    {
        await databases.ExecuteAsync(ScratchSchema.Partitioned);
        using var folder = new TempFolder();
        var query = "-- name: ListNotes\n" + sql + ";\n";
        _ = folder.WriteFile(FixtureCase.QueryFile, query);
        var fixture = new FixtureCase(SidecarValues.Engine.Postgres, "partition-named-as-its-scan", folder.Path);
        var capture = new DescribeCapture(fixture.Engine);
        var described = await FixtureReplay.DescribeOrRefuseThroughAsync(
            fixture,
            static _ => new PostgresDescriber(),
            databases.ConnectionString,
            new CapturingExchange(LiveExchange.Instance, capture)
        );
        var expected = described.Value.ShouldNotBeNull(described.Failure?.ToString());
        var raw = RawBundles.Of(query, capture, expected, BundleOutcome.Entry, []);
        MemberAliasesOf(raw).ShouldBe(["scratch_parts_1", "scratch_parts_2"]);

        var renamed = await BundleRenamings.RenamedAsync(
            raw,
            PostgresRenaming.Instance,
            new PostgresDescriber(),
            BundleRenamings.Key(BundleRenamings.AnyKey)
        );

        var bundle = renamed.Bundle;
        Provenance.Of(bundle.Expected.ShouldNotBeNull()).ShouldBe(Provenance.Of(expected));
        var schema = await File.ReadAllTextAsync(
            DescriberFixtures.Postgres.Schema,
            TestContext.Current.CancellationToken
        );
        BundleLeaks.Of(bundle, [schema, ScratchSchema.Partitioned, query], PostgresWords.IsEngineWord).ShouldBeEmpty();
    }

    // The aliases of the members of the plan's Appends that the walk reads as one prefix, in the order of the plan.
    private static string[] MemberAliasesOf(Bundle bundle)
    {
        var read = CaptureJson.Read(bundle.Capture);
        var capture = read.Value.ShouldNotBeNull(read.Failure?.ToString());
        var plan = capture.Calls.Single(static call => call.Method == PostgresCallNames.Explain).Answer;
        var root = PlanNode.Read(plan.GetProperty("plan").GetString()!).ShouldNotBeNull();
        return
        [
            .. root.SelfAndDescendants()
                .Where(static node => PlanPartitions.ShapeOf(node) is not null)
                .SelectMany(static node => node.Plans)
                .Select(static member => member.Alias!),
        ];
    }
}
