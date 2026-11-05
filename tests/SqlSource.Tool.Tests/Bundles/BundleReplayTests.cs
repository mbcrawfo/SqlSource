using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles;

// A bundle replayed through its engine's describer with no database: what it gives, what it refuses, and the question
// its capture does not answer.
public sealed class BundleReplayTests
{
    private const string Path = "/work/report.zip";

    // Each case of the describers' fixtures as its engine and its name.  DescriberFixtureTests lists them, so a build
    // that copied none fails there.
    public static TheoryData<string, string> Cases =>
        [
            with(
                FixtureCases
                    .Of("postgres")
                    .Concat(FixtureCases.Of("mssql"))
                    .Select(static found => (found.Engine, found.Name))
            ),
        ];

    // The bundle of a case with one call removed from its capture.
    private static Bundle Without(string engine, string name, string method)
    {
        var bundle = BundleReplays.OfCase(engine, name);
        var cut = new DescribeCapture(engine);
        foreach (var call in BundleReplays.CaptureOf(bundle).Calls.Where(call => call.Method != method))
        {
            cut.Add(call);
        }

        return BundleReplays.WithCapture(bundle, cut);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RunAsync_EveryFixture_GivesItsExpectedEntry(string engine, string name)
    {
        var bundle = BundleReplays.OfCase(engine, name);
        var expected = BundleReplay.ExpectedOf(bundle, Path);

        var replayed = await BundleReplays.ReplayAsync(bundle, Path);

        expected.Failure.ShouldBeNull();
        var entry = expected.Value.ShouldBeOfType<ReplayOutcome.Described>().Entry;
        replayed.ShouldBeOfType<ReplayOutcome.Described>().Entry.ShouldBe(entry);
    }

    [Fact]
    public async Task RunAsync_CaptureWithoutItsExplain_IsMissedNamingExplain()
    {
        var replayed = await BundleReplays.ReplayAsync(Without("postgres", "select-left-join", "explain"), Path);

        var missed = replayed.ShouldBeOfType<ReplayOutcome.Missed>();
        missed.Method.ShouldBe("explain");
        missed.Reason.ShouldStartWith("The capture has no further answer to 'explain' for the request ");
    }

    [Fact]
    public async Task RunAsync_CaptureWithoutItsDeallocate_IsMissedWhileDescribing()
    {
        var replayed = await BundleReplays.ReplayAsync(Without("postgres", "update-inferred", "deallocate"), Path);

        replayed.ShouldBeOfType<ReplayOutcome.Missed>().Method.ShouldBe("deallocate");
    }

    // The answer of open lacks the value of a setting, a member that is not nullable.
    [Fact]
    public async Task RunAsync_AnswerMissingAMemberThatIsNotNullable_IsMissed()
    {
        var bundle = BundleReplays.OfCase("postgres", "select-plain");
        var capture = BundleReplays.CaptureOf(bundle);
        var cut = new DescribeCapture(capture.Engine);
        foreach (var call in capture.Calls)
        {
            if (call.Method != "open")
            {
                cut.Add(call);
                continue;
            }

            var answer = JsonNode.Parse(call.Answer.GetRawText()).ShouldNotBeNull();
            answer["settings"].ShouldNotBeNull()[0].ShouldNotBeNull().AsObject().Remove("value").ShouldBeTrue();
            using var document = JsonDocument.Parse(answer.ToJsonString());
            cut.Add(new CaptureCall(call.Method, call.Request, document.RootElement.Clone()));
        }

        var replayed = await BundleReplays.ReplayAsync(BundleReplays.WithCapture(bundle, cut), Path);

        var missed = replayed.ShouldBeOfType<ReplayOutcome.Missed>();
        missed.Method.ShouldBe("open");
        missed.Reason.ShouldBe(
            "The capture's answer to 'open' cannot be read as PostgresOpenAnswer: its member "
                + "'settings[0].value' is null or missing"
        );
    }

    // A miss made without a method is one still, and never an exception of the run.
    [Fact]
    public async Task RunAsync_MissWithoutAMethod_IsMissedAsAnUnknownQuestion()
    {
        var replayed = await BundleReplays.ReplayAsync(
            BundleReplays.OfCase("postgres", "select-plain"),
            Path,
            new DescriberRegistry([new MissingDescriber()])
        );

        replayed.ShouldBe(new ReplayOutcome.Missed("an unknown question", "No answer."));
    }

    // A describer whose open throws a miss made without a method, before it asks its exchange anything.
    private sealed class MissingDescriber : IQueryDescriber
    {
        public SqlDialect Dialect => SqlDialect.PostgreSql;

        public IReadOnlyList<string> SecretsOf(string connection, Func<string, string?> getEnvironmentVariable) => [];

        public string? RewriteOf(string connection) => null;

        public Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
            OpenRequest request,
            IRunLog log,
            CancellationToken cancellationToken
        ) => throw new CaptureMissException("No answer.");
    }

    [Fact]
    public async Task RunAsync_PrepareAnsweredWith42P18_IsRefusedWithIdAndStep()
    {
        var replayed = await BundleReplays.ReplayAsync(BundleReplays.PrepareTypeNotDetermined(), Path);

        replayed.ShouldBe(new ReplayOutcome.Refused("SQLSRC226", "describe parameters"));
    }

    [Fact]
    public async Task RunAsync_Events_HaveTheEngineBeforeTheirNames()
    {
        var log = new RecordingLog();

        _ = await BundleReplays.ReplayAsync(BundleReplays.OfCase("postgres", "select-left-join"), Path, log: log);

        log.Events.Select(static written => written.Name).ShouldContain("postgres.walk.node");
        log.Events.ShouldAllBe(static written => written.Name.StartsWith("postgres.", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_DialectMarkerOfAnotherDialect_IsSqlsrc288()
    {
        var bundle = BundleReplays.OfCase("postgres", "select-plain") with
        {
            Query = "-- dialect: mssql\n-- name: GetOne\nSELECT 1;\n",
        };

        var planned = BundleReplay.Plan(bundle, "postgres", Path, TestContext.Current.CancellationToken);

        var error = planned.Failure.ShouldNotBeNull();
        error.Descriptor.Id.ShouldBe("SQLSRC288");
        error.Path.ShouldBe(Path);
        error
            .Arguments.ToArray()
            .ShouldBe(["query.sql is read as 'mssql', and the bundle is of the engine 'postgres'"]);
    }

    [Fact]
    public void Plan_TwoQueries_IsSqlsrc288()
    {
        var bundle = BundleReplays.OfCase("postgres", "select-plain") with
        {
            Query = "-- name: One\nSELECT 1;\n\n-- name: Two\nSELECT 2;\n",
        };

        var planned = BundleReplay.Plan(bundle, "postgres", Path, TestContext.Current.CancellationToken);

        planned
            .Failure.ShouldNotBeNull()
            .Arguments.ToArray()
            .ShouldBe(["query.sql holds 2 queries, and a bundle holds one"]);
    }

    [Fact]
    public void Plan_QueryThatCannotBeParsed_IsSqlsrc288()
    {
        var bundle = BundleReplays.OfCase("postgres", "select-plain") with
        {
            Query = "-- name: GetOne\nSELECT 'unterminated;\n",
        };

        var planned = BundleReplay.Plan(bundle, "postgres", Path, TestContext.Current.CancellationToken);

        var error = planned.Failure.ShouldNotBeNull();
        error.Descriptor.Id.ShouldBe("SQLSRC288");
        error.Arguments[0].ShouldStartWith("query.sql cannot be parsed: SQLSRC");
    }

    [Fact]
    public void Plan_EngineThatIsNoDialect_IsSqlsrc291()
    {
        var bundle = BundleReplays.OfCase("postgres", "select-plain");

        var planned = BundleReplay.Plan(bundle, "db2", Path, TestContext.Current.CancellationToken);

        var error = planned.Failure.ShouldNotBeNull();
        error.Descriptor.Id.ShouldBe("SQLSRC291");
        error.Arguments.ToArray().ShouldBe(["db2"]);
    }

    [Fact]
    public void Plan_Case_IsAsTheFixturesPlanIt()
    {
        var fixture = FixtureCases.Of("postgres").Single(static found => found.Name == "select-left-join");
        var token = TestContext.Current.CancellationToken;

        var planned = BundleReplay.Plan(BundleReplays.Read(fixture.Folder), "postgres", Path, token);

        var (query, dialect) = FixtureReplay.Plan(fixture, token);
        planned.Value.Dialect.ShouldBe(dialect);
        planned.Value.Query.Hash.ShouldBe(query.Hash);
        planned.Value.Query.Database.ShouldBe(query.Database);
    }

    [Fact]
    public void EngineOf_BundleJson_IsItsEngine()
    {
        var bundle = BundleReplays.OfCase("postgres", "select-plain") with { Info = Info("mssql") };

        BundleReplay.EngineOf(bundle).ShouldBe("mssql");
    }

    [Fact]
    public void EngineOf_FolderWithoutBundleJson_IsTheCapturesEngine() =>
        BundleReplay.EngineOf(BundleReplays.OfCase("mssql", "select-left-join")).ShouldBe("mssql");

    [Fact]
    public void CaptureOf_CaptureVersionTwo_IsSqlsrc289()
    {
        var bundle = BundleReplays.OfCase("postgres", "select-plain");
        bundle = bundle with
        {
            Capture = bundle.Capture.Replace(
                "\"captureVersion\": 1",
                "\"captureVersion\": 2",
                StringComparison.Ordinal
            ),
        };

        var error = BundleReplay.CaptureOf(bundle, Path).Failure.ShouldNotBeNull();

        error.Descriptor.Id.ShouldBe("SQLSRC289");
        error.Arguments.ToArray().ShouldBe(["captureVersion", "2", "1"]);
    }

    [Fact]
    public void CaptureOf_CaptureThatIsNotJson_IsSqlsrc288()
    {
        var bundle = BundleReplays.OfCase("postgres", "select-plain") with { Capture = "{ calls" };

        var error = BundleReplay.CaptureOf(bundle, Path).Failure.ShouldNotBeNull();

        error.Descriptor.Id.ShouldBe("SQLSRC288");
        error.Arguments[0].ShouldStartWith("the capture is not JSON: ");
    }

    [Fact]
    public void CaptureOf_EngineOtherThanBundleJsons_IsSqlsrc288()
    {
        var bundle = BundleReplays.OfCase("postgres", "select-plain") with { Info = Info("mssql") };

        var error = BundleReplay.CaptureOf(bundle, Path).Failure.ShouldNotBeNull();

        error.Descriptor.Id.ShouldBe("SQLSRC288");
        error.Arguments.ToArray().ShouldBe(["bundle.json names the engine 'mssql', and capture.json 'postgres'"]);
    }

    [Fact]
    public void ExpectedOf_NoExpectedAndAFailure_IsTheFailure()
    {
        var bundle = BundleReplays.OfCase("postgres", "select-plain") with
        {
            Info = Info("postgres", BundleOutcome.Failure("SQLSRC226", "describe parameters")),
            Expected = null,
        };

        BundleReplay
            .ExpectedOf(bundle, Path)
            .Value.ShouldBe(new ReplayOutcome.Refused("SQLSRC226", "describe parameters"));
    }

    [Fact]
    public void ExpectedOf_NoExpectedAndNoBundleJson_IsNull()
    {
        var bundle = BundleReplays.OfCase("postgres", "select-plain") with { Expected = null };

        var expected = BundleReplay.ExpectedOf(bundle, Path);

        expected.Succeeded.ShouldBeTrue();
        expected.Value.ShouldBeNull();
    }

    [Fact]
    public void ExpectedOf_SidecarOfTwoEntries_IsSqlsrc288()
    {
        var bundle = BundleReplays.OfCase("postgres", "select-plain");
        var sidecar = JsonNode.Parse(bundle.Expected.ShouldNotBeNull()).ShouldNotBeNull();
        var queries = sidecar["queries"].ShouldNotBeNull().AsObject();
        queries["Other"] = queries["GetEverything"].ShouldNotBeNull().DeepClone();

        var error = BundleReplay.ExpectedOf(bundle with { Expected = sidecar.ToJsonString() }, Path).Failure;

        error.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC288");
        error.Arguments.ToArray().ShouldBe(["expected.json holds 2 entries, and a bundle's holds one"]);
    }

    [Fact]
    public void ExpectedOf_SidecarThatIsMalformed_IsSqlsrc288()
    {
        var bundle = BundleReplays.OfCase("postgres", "select-plain") with { Expected = "[]" };

        var error = BundleReplay.ExpectedOf(bundle, Path).Failure.ShouldNotBeNull();

        error.Descriptor.Id.ShouldBe("SQLSRC288");
        error.Arguments.ToArray().ShouldBe(["expected.json is not a sidecar: it is not one JSON object"]);
    }

    private static BundleInfo Info(string engine, BundleOutcome? outcome = null) =>
        new(
            BundleVersion: 1,
            Renamed: false,
            ToolVersion: "1.4.0",
            FormatVersion: 1,
            Engine: engine,
            ServerVersion: "18.6",
            DriverVersion: null,
            Runtime: ".NET 8.0.11",
            OperatingSystem: "Linux 6.8",
            Outcome: outcome ?? BundleOutcome.Entry
        );
}
