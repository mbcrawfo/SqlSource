using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Fixtures;

// Every case of every engine's fixtures, replayed with the engine's describer and no database: the text of the
// sidecar is the case's expected.json, byte for byte.  tests/SqlSource.Tests.Postgres describes the same cases
// against a server, and tools/record-fixtures.sh records them.
public sealed class DescriberFixtureTests : IDisposable
{
    private static readonly IReadOnlyList<FixtureCase> Found = FindAll();

    private readonly TempFolder _folder = new();

    // Each case as "engine/name".
    public static TheoryData<string> Cases => [with(Found.Select(static found => found.ToString()))];

    public void Dispose() => _folder.Dispose();

    // A folder under the root is an engine.
    private static List<FixtureCase> FindAll()
    {
        if (!Directory.Exists(FixtureCases.Root))
        {
            return [];
        }

        return
        [
            .. new DirectoryInfo(FixtureCases.Root)
                .GetDirectories()
                .Select(static folder => folder.Name)
                .Order(StringComparer.Ordinal)
                .SelectMany(static engine => FixtureCases.Of(engine)),
        ];
    }

    private static IQueryDescriber DescriberOf(FixtureCase fixture) =>
        fixture.Engine switch
        {
            "postgres" => new PostgresDescriber(),
            _ => throw new InvalidOperationException($"No describer is known for the engine of the case {fixture}."),
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Replay_Case_GivesTheExpectedEntry(string name)
    {
        var fixture = Found.Single(found => found.ToString() == name);

        var replayed = await FixtureReplay.DescribeAsync(fixture, DescriberOf);

        replayed.ShouldBe(fixture.Expected);
    }

    // The cases of the PostgreSQL describer.  A build that copied none would leave the theory with nothing to run.
    [Fact]
    public void Cases_OfPostgres_HoldEveryCase()
    {
        var names = Found.Select(static found => found.ToString()).ToList();

        foreach (
            var name in new[]
            {
                "postgres/delete-declared-only",
                "postgres/insert-declared",
                "postgres/insert-returning",
                "postgres/select-declared-domain",
                "postgres/select-expressions",
                "postgres/select-left-join",
                "postgres/select-other-schema",
                "postgres/select-plain",
                "postgres/select-suffix-names",
                "postgres/select-view",
                "postgres/update-array-and-range",
                "postgres/update-inferred",
            }
        )
        {
            names.ShouldContain(name);
        }
    }

    // A describer whose calls changed fails its fixtures, and never passes silently.
    [Theory]
    [InlineData("prepare")]
    [InlineData("columns")]
    [InlineData("tables", "select-plain")]
    [InlineData("types")]
    [InlineData("check")]
    [InlineData("deallocate")]
    public async Task Replay_CaptureWithACallRemoved_ThrowsACaptureMiss(string method, string from = "update-inferred")
    {
        var source = Found.Single(found => found.ToString() == "postgres/" + from);
        var capture = CaptureJson.Read(source.Capture).Value.ShouldNotBeNull();
        var cut = new DescribeCapture(capture.Engine);
        foreach (var call in capture.Calls.Where(call => call.Method != method))
        {
            cut.Add(call);
        }

        _ = _folder.WriteFile($"postgres/cut/{FixtureCase.QueryFile}", source.Query);
        _ = _folder.WriteFile($"postgres/cut/{FixtureCase.CaptureFile}", CaptureJson.Write(cut));
        var fixture = new FixtureCase("postgres", "cut", _folder.PathOf("postgres/cut"));

        _ = await Should.ThrowAsync<CaptureMissException>(() => FixtureReplay.DescribeAsync(fixture, DescriberOf));
    }
}
