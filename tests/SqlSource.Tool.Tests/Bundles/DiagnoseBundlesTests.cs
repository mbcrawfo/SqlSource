using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles;

// The raw bundle of a query of a project, described as diagnose describes it: through a capturing exchange over one
// that a capture answers, as the live database would, by the real PostgreSQL describer.  The query is the recorded
// case update-inferred's, in a file of several, its database from the settings file and with markers that query.sql
// drops.
public sealed class DiagnoseBundlesTests
{
    private const string UsersFile = """
        -- output: codegen

        -- name: First
        SELECT 1;

        -- name: RenameUser
        -- summary: Renames a user.
        -- param: @id -> UserId
        -- output: models
        UPDATE users SET name = @name WHERE id = @id;

        -- name: Last
        SELECT 2;

        """;

    [Fact]
    public async Task Raw_AnEntry_ReplaysToReproduced()
    {
        using var folder = new TempFolder();
        var raw = await RawAsync(folder, CaptureOf(PostgresRenamings.Fixture("update-inferred")));

        var expected = BundleReplay.ExpectedOf(raw, BundleInfos.Path);
        expected.Failure.ShouldBeNull();
        var answer = expected.Value.ShouldBeOfType<ReplayOutcome.Described>();
        answer.Entry.Database.ShouldBe("app");
        (await BundleReplays.ReplayAsync(raw, BundleInfos.Path)).ShouldBe(answer);
    }

    [Fact]
    public async Task Raw_AnEntry_IsRenamedAndProven()
    {
        using var folder = new TempFolder();
        var raw = await RawAsync(folder, CaptureOf(PostgresRenamings.Fixture("update-inferred")));

        var renamed = await PostgresRenamings.RenamedAsync(raw);

        renamed.Bundle.Info.ShouldNotBeNull().Renamed.ShouldBeTrue();
    }

    [Fact]
    public async Task Raw_ARefusal_ReplaysToTheSameFailure()
    {
        using var folder = new TempFolder();
        var raw = await RawAsync(folder, BundleReplays.CaptureOf(BundleReplays.PrepareTypeNotDetermined()));

        raw.Expected.ShouldBeNull();
        var outcome = raw.Info.ShouldNotBeNull().Outcome;
        outcome.Kind.ShouldBe(BundleOutcomeKind.Failure);
        var expected = BundleReplay.ExpectedOf(raw, BundleInfos.Path);
        expected.Failure.ShouldBeNull();
        expected.Value.ShouldBe(new ReplayOutcome.Refused(outcome.Id.ShouldNotBeNull(), outcome.Step));
        (await BundleReplays.ReplayAsync(raw, BundleInfos.Path)).ShouldBe(expected.Value);
    }

    [Fact]
    public async Task Raw_AnEntry_SaysWhatMadeItAndWhatItGave()
    {
        using var folder = new TempFolder();
        var raw = await RawAsync(folder, CaptureOf(PostgresRenamings.Fixture("update-inferred")));

        raw.Info.ShouldBe(
            new BundleInfo(
                BundleJson.Version,
                Renamed: false,
                PackageVersion.Prefix,
                SidecarFormat.Version,
                "postgres",
                "18.6",
                "10.0.3",
                Hosts.Runtime,
                Hosts.OperatingSystem,
                BundleOutcome.Entry
            )
        );
        raw.ExplainSettings.ShouldBeEmpty();
        CaptureJson.Read(raw.Capture).Value.ShouldNotBeNull().Engine.ShouldBe("postgres");
        raw.Query.ShouldStartWith("-- dialect: postgres\n-- database: app\n-- name: RenameUser\n");
    }

    private static DescribeCapture CaptureOf(FixtureCase fixture) =>
        CaptureJson.Read(fixture.Capture).Value.ShouldNotBeNull();

    // The query RenameUser of the project's file described as diagnose describes it, each call answered from the
    // capture given and recorded, and the raw bundle of what it gave.
    private static async Task<Bundle> RawAsync(TempFolder folder, DescribeCapture answers)
    {
        var token = TestContext.Current.CancellationToken;
        var project = Plans.Postgres(folder);
        project.Settings["database"] = "app";
        var path = project.AddSql("Users.sql", UsersFile);
        var file = Plans.Of(project).Files.Single();
        var planned = file.Queries.Single(static query => query.Query.Name == "RenameUser");
        var database = planned.Database.ShouldNotBeNull();

        var run = new RunCapture("postgres");
        var opened = await new PostgresDescriber().OpenAsync(
            database,
            FixtureReplay.Connection,
            new CapturingExchange(new ReplayingExchange(answers), run.Capture)
        );
        var session = opened.Value.ShouldNotBeNull();
        run.Server = session.Server;
        SidecarEntry? entry = null;
        DescribeFailure? failure = null;
        await using (session)
        {
            var query = planned.Query;
            var described = await session.DescribeAsync(
                new DescribeRequest(query.Name, SampleSql.Build(query, file.Dialect), query.Parameters),
                token
            );
            if (described.Succeeded)
            {
                entry = EntryBuilder.Build(planned, file.Dialect, database, session.Server, described.Value);
            }
            else
            {
                failure = described.Failure;
            }
        }

        run.Text = CaptureJson.Write(run.Capture);
        var host = Hosts.Create(folder.Path, new FakeProcessRunner(), folder.Path);
        return DiagnoseBundles.Raw(
            planned,
            await System.IO.File.ReadAllTextAsync(path, token),
            file.Dialect,
            new DescribedQuery(run, entry, failure),
            host
        );
    }
}
