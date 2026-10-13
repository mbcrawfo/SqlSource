using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Tests.Fixtures;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// The fixture cases of the PostgreSQL describer against a real server: describing a case's query.sql gives the
// entry of its expected.json.  With SQLSOURCE_RECORD_FIXTURES set it writes the case's capture.json and
// expected.json and compares nothing: tools/record-fixtures.sh runs it so.  The cases are in the repository, in
// tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres, where the tool's tests replay them with no database.
[Collection(UsesPostgres.Name)]
public sealed class FixtureCasesTests(PostgresContainer postgres)
{
    public const string RecordVariable = "SQLSOURCE_RECORD_FIXTURES";

    private const string Engine = "postgres";

    // As SidecarWriter writes the key.
    private const string VersionKey = "\"serverVersion\": \"";

    private static readonly IReadOnlyList<FixtureCase> Found = FixtureCases.Of(
        Engine,
        RepositoryPaths.DescriberFixtures
    );

    public static TheoryData<string> Cases => [with(Found.Select(static found => found.Name))];

    private static bool Recording => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RecordVariable));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Describe_Case_GivesTheExpectedEntry(string name)
    {
        var fixture = Found.Single(found => found.Name == name);
        if (Recording)
        {
            await RecordAsync(fixture);
            return;
        }

        var live = await FixtureReplay.DescribeThroughAsync(
            fixture,
            static _ => new PostgresDescriber(),
            postgres.ConnectionString,
            LiveExchange.Instance
        );

        WithOneServerVersion(live).ShouldBe(WithOneServerVersion(fixture.Expected));
    }

    private async Task RecordAsync(FixtureCase fixture)
    {
        var token = TestContext.Current.CancellationToken;
        var capture = new DescribeCapture(Engine);

        var expected = await FixtureReplay.DescribeThroughAsync(
            fixture,
            static _ => new PostgresDescriber(),
            postgres.ConnectionString,
            new CapturingExchange(LiveExchange.Instance, capture)
        );

        await File.WriteAllTextAsync(
            Path.Combine(fixture.Folder, FixtureCase.CaptureFile),
            CaptureJson.Write(capture),
            token
        );
        await File.WriteAllTextAsync(Path.Combine(fixture.Folder, FixtureCase.ExpectedFile), expected, token);
    }

    // An expected.json keeps the version it was recorded against.  CI describes the same cases on two servers, and
    // the default tag moves from one patch to the next, so the two texts are compared with one value there.
    private static string WithOneServerVersion(string sidecar)
    {
        var key = sidecar.IndexOf(VersionKey, StringComparison.Ordinal);
        key.ShouldBeGreaterThanOrEqualTo(0, "the entry has no serverVersion");
        var value = key + VersionKey.Length;
        var end = sidecar.IndexOf('"', value);
        return string.Concat(sidecar.AsSpan(0, value), "*", sidecar.AsSpan(end));
    }
}
