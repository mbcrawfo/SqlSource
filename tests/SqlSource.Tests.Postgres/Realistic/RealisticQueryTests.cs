using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
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

    // The majors that an expectation is recorded on, as CI runs them.
    private static readonly int[] Recorded = [14, 18];

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

        described.Succeeded.ShouldBeTrue($"{found} was refused:\n{RealisticRun.Render(described)}");
        var columns = described.Value.Columns ?? [];
        columns.Count.ShouldBe(found.Columns.Count, $"{found} has a column count other than its truth's");
        columns.Select(static column => column.Name).ShouldBe(found.Columns.Select(static truth => truth.Name));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Describe_Case_IsNotNullOnlyWhereTheTruthIs(string name)
    {
        var (found, _) = await StartAsync(name);
        var description = await DescribedAsync(found);

        var columns = description.Columns ?? [];
        columns.Count.ShouldBe(found.Columns.Count, $"{found} has a column count other than its truth's");
        for (var index = 0; index < found.Columns.Count; index++)
        {
            var truth = found.Columns[index];
            var column = $"{found} column {columns[index].Ordinal} {truth.Name}";
            if (truth.Nullable)
            {
                columns[index].Nullable.ShouldNotBe(false, $"{column} is not null, and can be NULL: {truth.Why}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Execute_Case_ReturnsNoNullWhereEitherSaysNotNull(string name)
    {
        var (found, _) = await StartAsync(name);
        var description = await DescribedAsync(found);
        var columns = description.Columns ?? [];
        columns.Count.ShouldBe(found.Columns.Count, $"{found} has a column count other than its truth's");
        var sql = RealisticRun.RequestOf(found).Sql;
        var types = description
            .Parameters.Where(static parameter => parameter.Type is not null)
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
            var column = $"{found} column {columns[index].Ordinal} {truth.Name}";
            if (columns[index].Nullable == false)
            {
                sawNull[index].ShouldBeFalse($"{column} is recorded not null, and held a NULL");
            }

            if (!truth.Nullable)
            {
                sawNull[index].ShouldBeFalse($"{column} is true not null, and held a NULL: the truth is wrong");
            }
            else if (truth.Exempt is null)
            {
                sawNull[index].ShouldBeTrue($"{column} can be NULL, and the seed shows none: {truth.Why}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Describe_Case_IsItsExpectation(string name)
    {
        var (found, major) = await StartAsync(name);
        var path = found.ExpectedPath(major);
        var expectation = ExpectationOn(Recording, major);
        if (expectation == Expectation.Skip)
        {
            Assert.Skip(SkipMessage);
        }

        if (expectation == Expectation.Refuse)
        {
            Assert.Fail(RefuseMessage);
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

    // What the expectation theory does with a run: record or compare on a recorded major, skip or refuse on another.
    public enum Expectation
    {
        Compare,
        Record,
        Skip,
        Refuse,
    }

    internal const string SkipMessage = "The expectations are recorded on PostgreSQL 14 and 18 alone.";

    internal const string RefuseMessage = "Record on PostgreSQL 14 or 18.";

    internal static Expectation ExpectationOn(bool recording, int major) =>
        (recording, Recorded.Contains(major)) switch
        {
            (true, true) => Expectation.Record,
            (true, false) => Expectation.Refuse,
            (false, true) => Expectation.Compare,
            (false, false) => Expectation.Skip,
        };

    [Theory]
    [InlineData(true, 14, Expectation.Record)]
    [InlineData(true, 18, Expectation.Record)]
    [InlineData(true, 16, Expectation.Refuse)]
    [InlineData(false, 14, Expectation.Compare)]
    [InlineData(false, 18, Expectation.Compare)]
    [InlineData(false, 17, Expectation.Skip)]
    public void ExpectationOn_RecordingAndMajor_IsWhatTheTheoryDoes(bool recording, int major, Expectation expected) =>
        ExpectationOn(recording, major).ShouldBe(expected);

    // The case and the server's major, once the server runs the case.
    private async Task<(RealisticCase Found, int Major)> StartAsync(string name)
    {
        var found = RealisticCases.All.Single(candidate => candidate.Name == name);
        var major = await RealisticRun.ServerMajorAsync(postgres.RealisticConnectionString);
        SkipUnlessTheServerRunsIt(found, major);
        return (found, major);
    }

    private static void SkipUnlessTheServerRunsIt(RealisticCase found, int major)
    {
        if (found.MinimumMajor is { } minimum && minimum > major)
        {
            Assert.Skip($"The case needs PostgreSQL {minimum} or later.");
        }
    }

    // The description of a case that is not refused, which it must be able to give.
    private async Task<QueryDescription> DescribedAsync(RealisticCase found)
    {
        if (found.Refusal is not null)
        {
            Assert.Skip($"{found} is refused as {found.Refusal}, so it has no columns to check.");
        }

        var described = await RealisticRun.DescribeAsync(found, postgres.RealisticConnectionString);
        described.Succeeded.ShouldBeTrue($"{found} was refused:\n{RealisticRun.Render(described)}");
        return described.Value;
    }
}
