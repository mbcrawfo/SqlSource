using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Fixtures;

// Every case of every engine's fixtures, replayed with the engine's describer and no database: the text of the
// sidecar is the case's expected.json, byte for byte.  tests/SqlSource.Tests.Postgres and
// tests/SqlSource.Tests.SqlServer describe the same cases against a server, and tools/record-fixtures.sh records
// them.
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
            "postgres" => new PostgresDescriber(fixture.ExplainSettings),
            "mssql" => new SqlServerDescriber(),
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
                "postgres/match-alias",
                "postgres/match-explicit-list",
                "postgres/match-expression",
                "postgres/match-left-join-nullability",
                "postgres/match-missing-column",
                "postgres/match-reordered",
                "postgres/match-star",
                "postgres/match-suffix",
                "postgres/match-twice",
                "postgres/match-two-tables",
                "postgres/match-view-star",
                "postgres/select-declared-domain",
                "postgres/select-domain-parameter",
                "postgres/select-expressions",
                "postgres/select-left-join",
                "postgres/select-modifiers",
                "postgres/select-modifiers-expression",
                "postgres/select-other-schema",
                "postgres/select-plain",
                "postgres/select-range-intersection",
                "postgres/select-suffix-names",
                "postgres/select-view",
                "postgres/update-array-and-range",
                "postgres/update-inferred",
                "postgres/walk-aggregate",
                "postgres/walk-anti",
                "postgres/walk-chain",
                "postgres/walk-count-argument",
                "postgres/walk-cte-inlined",
                "postgres/walk-cte-materialized",
                "postgres/walk-cube",
                "postgres/walk-full",
                "postgres/walk-full-hash",
                "postgres/walk-full-merge",
                "postgres/walk-grouping-sets",
                "postgres/walk-lateral",
                "postgres/walk-left",
                "postgres/walk-left-hash",
                "postgres/walk-left-merge",
                "postgres/walk-left-nestloop",
                "postgres/walk-natural",
                "postgres/walk-nested",
                "postgres/walk-order-by-unselected",
                "postgres/walk-proved-empty",
                "postgres/walk-quoted-alias",
                "postgres/walk-reduced-by-join",
                "postgres/walk-reduced-by-where",
                "postgres/walk-removed",
                "postgres/walk-returning",
                "postgres/walk-right",
                "postgres/walk-right-hash",
                "postgres/walk-right-merge",
                "postgres/walk-right-nestloop",
                "postgres/walk-rollup",
                "postgres/walk-self-join",
                "postgres/walk-semi-anti",
                "postgres/walk-subquery-flattened",
                "postgres/walk-subquery-kept",
                "postgres/walk-union",
                "postgres/walk-using",
                "postgres/walk-view",
                "postgres/walk-view-nullable-side",
            }
        )
        {
            names.ShouldContain(name);
        }
    }

    // The cases of the SQL Server describer.  A build that copied none would leave the theory with nothing to run.
    [Fact]
    public void Cases_OfSqlServer_HoldEveryCase()
    {
        var names = Found.Select(static found => found.ToString()).ToList();

        foreach (
            var name in new[]
            {
                "mssql/delete-declared-only",
                "mssql/insert-declared",
                "mssql/insert-output",
                "mssql/merge-output",
                "mssql/select-aggregates",
                "mssql/select-alias-and-clr",
                "mssql/select-columns-differ",
                "mssql/select-computed-not-null",
                "mssql/select-computed-outer-join",
                "mssql/select-first-result-set",
                "mssql/select-hidden-period",
                "mssql/select-left-join",
                "mssql/select-names-differ",
                "mssql/select-nullability-differs",
                "mssql/select-order-differs",
                "mssql/select-other-database",
                "mssql/select-proved-forms",
                "mssql/select-several-tables",
                "mssql/select-table-function",
                "mssql/select-table-star",
                "mssql/select-temporal-all",
                "mssql/select-view-star",
                "mssql/update-copies-agree",
                "mssql/update-inferred",
                "mssql/update-optional-filter",
                "mssql/update-wide-types",
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
    [InlineData("explain", "select-left-join")]
    [InlineData("deallocate")]
    public Task Replay_CaptureWithACallRemoved_ThrowsACaptureMiss(string method, string from = "update-inferred") =>
        ReplayWithoutAsync("postgres/" + from, method);

    // The same of the SQL Server describer, whose cases hold each of its calls.
    [Theory]
    [InlineData("open")]
    [InlineData("parameters")]
    [InlineData("probe", "update-optional-filter")]
    [InlineData("declared", "insert-declared")]
    [InlineData("columns")]
    [InlineData("operands", "select-aggregates")]
    [InlineData("table", "select-table-star")]
    public Task Replay_SqlServerCaptureWithACallRemoved_ThrowsACaptureMiss(
        string method,
        string from = "update-inferred"
    ) => ReplayWithoutAsync("mssql/" + from, method);

    private async Task ReplayWithoutAsync(string name, string method)
    {
        var source = Found.Single(found => found.ToString() == name);
        var capture = CaptureJson.Read(source.Capture).Value.ShouldNotBeNull();
        var cut = new DescribeCapture(capture.Engine);
        foreach (var call in capture.Calls.Where(call => call.Method != method))
        {
            cut.Add(call);
        }

        var fixture = FixtureCaseFiles.Create(_folder.Path, source.Engine, "cut", source.Query);
        await FixtureCaseFiles.WriteCaptureAsync(fixture, cut, TestContext.Current.CancellationToken);

        _ = await Should.ThrowAsync<CaptureMissException>(() => FixtureReplay.DescribeAsync(fixture, DescriberOf));
    }
}
