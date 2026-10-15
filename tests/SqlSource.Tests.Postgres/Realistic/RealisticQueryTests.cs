using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic;

// The checks of the realistic cases against a real server, each its own theory so that a failure says which check
// failed: the refusal, the safety of the description against the truth, the oracle that executes the query over the
// seed, and the recorded expectation.  Each describes the case afresh.  With SQLSOURCE_RECORD_REALISTIC set the
// expectation theory writes the case's expected-pg{major}.txt and compares nothing: tools/record-realistic.sh runs
// it so.  The cases are in Realistic/Cases.
[Collection(UsesPostgres.Name)]
public sealed class RealisticQueryTests(PostgresContainer postgres)
{
    public const string RecordVariable = "SQLSOURCE_RECORD_REALISTIC";

    private const string RecordScript = "tools/record-realistic.sh";

    public static TheoryData<string> Cases => [with(RealisticCases.All.Select(static found => found.Name))];

    private static bool Recording => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RecordVariable));

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Describe_Case_IsRefusedOnlyAsItsCaseSays(string name)
    {
        var (found, _) = await StartAsync(name);

        var described = await RealisticRun.DescribeAsync(found, postgres.RealisticConnectionString);

        if (found.Refusal is { } refusal)
        {
            described.Succeeded.ShouldBeFalse($"{found} is refused as {refusal}, and was described");
            described.Failure.Descriptor.Id.ShouldBe(refusal, $"{found} is refused as {refusal}");
            return;
        }

        _ = RealisticRun.ColumnsOf(found, described);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Describe_Case_IsNotNullOnlyWhereTheTruthIs(string name)
    {
        var (found, _) = await StartAsync(name);
        var (columns, _) = await DescribedAsync(found);

        for (var index = 0; index < columns.Count; index++)
        {
            var truth = found.Columns[index];
            if (truth.Nullable && columns[index].Nullable == false)
            {
                Assert.Fail($"{ColumnOf(found, index)} is not null, and can be NULL: {truth.Why}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Execute_Case_ReturnsNoNullWhereEitherSaysNotNull(string name)
    {
        var (found, _) = await StartAsync(name);
        var (columns, parameters) = await DescribedAsync(found);
        RealisticOracle.CheckSampleKeys(found, parameters.Select(static parameter => parameter.Name));
        var sql = RealisticRun.RequestOf(found).Sql;
        var types = parameters
            .Where(static parameter => parameter.Type is not null)
            .ToDictionary(static parameter => parameter.Name, static parameter => parameter.Type!.Name);

        var sawNull = new bool[columns.Count];
        foreach (var sample in found.Samples)
        {
            var run = await RealisticOracle.RunAsync(
                postgres.RealisticConnectionString,
                RealisticOracle.Substitute(sql, sample, types)
            );

            run.Names.ShouldBe(columns.Select(static column => column.Name), $"{found} names its columns otherwise");
            for (var index = 0; index < sawNull.Length; index++)
            {
                sawNull[index] |= run.SawNull[index];
            }
        }

        for (var index = 0; index < sawNull.Length; index++)
        {
            var truth = found.Columns[index];
            var column = ColumnOf(found, index);
            if (columns[index].Nullable == false && sawNull[index])
            {
                Assert.Fail($"{column} is described not null, and held a NULL over the seed");
            }

            if (!truth.Nullable && sawNull[index])
            {
                Assert.Fail($"{column} is not null in truth, and held a NULL over the seed: the truth is wrong");
            }

            if (truth.Nullable && truth.Exempt is null && !sawNull[index])
            {
                Assert.Fail($"{column} can be NULL, and the seed shows none: {truth.Why}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Describe_Case_IsItsExpectation(string name)
    {
        var (found, major) = await StartAsync(name);
        var path = found.ExpectedPath(major);
        var expectation = RealisticRun.ExpectationOn(Recording, major);
        if (expectation == Expectation.Skip)
        {
            Assert.Skip(RealisticRun.SkipMessage);
        }

        if (expectation == Expectation.Refuse)
        {
            Assert.Fail(RealisticRun.RefuseMessage);
        }

        var rendered = RealisticRun.Render(await RealisticRun.DescribeAsync(found, postgres.RealisticConnectionString));
        if (expectation == Expectation.Record)
        {
            await File.WriteAllTextAsync(path, rendered, TestContext.Current.CancellationToken);
            return;
        }

        File.Exists(path).ShouldBeTrue($"{found} has no expectation at {path}: run {RecordScript}");
        rendered.ShouldBe(
            await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken),
            $"{found} is described otherwise than {path} says; {RecordScript} records it"
        );
    }

    // How a failure names a column: the case, the position and the truth's name.
    private static string ColumnOf(RealisticCase found, int index) =>
        $"{found} column {index} {found.Columns[index].Name}";

    // The case, read from its own folder, and the server's major, once the server runs the case.
    private async Task<(RealisticCase Found, int Major)> StartAsync(string name)
    {
        var found = RealisticCases.Read(Path.Combine(RepositoryPaths.RealisticCases, name));
        var major = await RealisticRun.ServerMajorAsync(postgres.RealisticConnectionString);
        if (!RealisticRun.RunsOn(found, major))
        {
            Assert.Skip($"The case needs PostgreSQL {found.MinimumMajor} or later.");
        }

        return (found, major);
    }

    // The columns and the parameters of a case that is not refused, which must be described with its truth's columns.
    private async Task<(
        IReadOnlyList<SidecarColumn> Columns,
        IReadOnlyList<DescribedParameter> Parameters
    )> DescribedAsync(RealisticCase found)
    {
        if (found.Refusal is not null)
        {
            Assert.Skip($"{found} is refused as {found.Refusal}, so it has no columns to check.");
        }

        var described = await RealisticRun.DescribeAsync(found, postgres.RealisticConnectionString);
        var columns = RealisticRun.ColumnsOf(found, described);
        return (columns, described.Value!.Parameters);
    }
}
