using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests;

// The raw bundles of the renaming's tests: a query that FakeDescriber describes, captured through a capturing exchange
// as diagnose captures one, with its expected.json made by FixtureReplay's steps.  The server has a setting whose
// value holds a schema of the user's.
internal static class RenamingBundles
{
    public const string Name = "GetUsers";

    public const string Query =
        "-- name: GetUsers\nSELECT u.id, u.tags FROM users u WHERE u.status = 'active' AND u.id = @id;\n";

    public const string Engine = "postgres";

    public const string ToolVersion = "1.2.3-test";

    // The bundle's path, where an error of reading it would be.  Nothing is read there.
    public const string Path = "/work/report.zip";

    public static FakeDescriber Describer() =>
        new()
        {
            Server = new ServerInfo(
                "16.4",
                "FakeDriver",
                "1.2.3",
                new EquatableArray<ServerSetting>([new ServerSetting("search_path", "accounts, public")])
            ),
        };

    // The raw bundle of the query that the describer is told TypedRows for, or what the test tells it.
    public static async Task<Bundle> RawAsync(FakeDescriber? describer = null)
    {
        if (describer is null)
        {
            describer = Describer();
            describer.Descriptions[Name] = FakeDescriber.TypedRows;
        }

        var token = TestContext.Current.CancellationToken;
        var planned = BundleReplay.Plan(new Bundle(null, Query, string.Empty, null, []), Engine, Path, token);
        planned.Failure.ShouldBeNull();
        var (query, dialect) = planned.Value;
        var database = query.Database.ShouldNotBeNull();
        var capture = new DescribeCapture(Engine);
        var opened = await describer.OpenAsync(
            database,
            "Host=db",
            new CapturingExchange(LiveExchange.Instance, capture)
        );
        string? expected = null;
        BundleOutcome outcome;
        await using (var session = opened.Value.ShouldNotBeNull())
        {
            var sql = query.Query;
            var described = await session.DescribeAsync(
                new DescribeRequest(sql.Name, SampleSql.Build(sql, dialect), sql.Parameters),
                token
            );
            if (described.Value is { } description)
            {
                var entry = EntryBuilder.Build(query, dialect, database, session.Server, description);
                expected = SidecarWriter.Write(
                    new Sidecar(SidecarFormat.Version, ToolVersion, new EquatableArray<SidecarEntry>([entry]))
                );
                outcome = BundleOutcome.Entry;
            }
            else
            {
                var failure = described.Failure.ShouldNotBeNull();
                outcome = BundleOutcome.Failure(
                    failure.Descriptor.Id,
                    failure.Step is { } step ? DescribeStepName.Of(step) : null
                );
            }
        }

        var info = new BundleInfo(
            BundleJson.Version,
            Renamed: false,
            ToolVersion,
            SidecarFormat.Version,
            Engine,
            "16.4",
            "1.2.3",
            ".NET 10.0.0",
            "Test OS",
            outcome
        );
        return new Bundle(info, Query, CaptureJson.Write(capture), expected, []);
    }

    // A key of 32 bytes of one value.
    public static RenamingKey Key(byte fill) => RenamingKey.Of(Enumerable.Repeat(fill, RenamingKey.Length).ToArray());

    // The bundle renamed with the fake engine part, or the one given, and a fresh describer.
    public static Task<Outcome<RenamedBundle, RenamingRefusal>> RenameAsync(
        Bundle raw,
        FakeEngineRenaming? engine = null,
        byte key = 1
    ) =>
        BundleRenamer.RenameAsync(
            raw,
            engine ?? new FakeEngineRenaming(),
            new FakeDescriber(),
            Key(key),
            TestContext.Current.CancellationToken
        );

    // The text of every file of a bundle, as a zip holds it.
    public static IEnumerable<string> FilesOf(Bundle bundle)
    {
        if (bundle.Info is { } info)
        {
            yield return BundleJson.Write(info);
        }

        yield return bundle.Query;
        yield return bundle.Capture;
        if (bundle.Expected is { } expected)
        {
            yield return expected;
        }

        yield return string.Join('\n', bundle.ExplainSettings);
    }
}
