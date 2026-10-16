using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic.Describer;

// What the describer gives for each realistic case against a real server, whole: the refusal its case says or none,
// and the description recorded of it, its kind, plan, table match, parameters and columns.  Whether its nullability
// is safe is RealisticNullabilitySafetyTests'.  With SQLSOURCE_RECORD_REALISTIC set the theory of the recorded
// description writes the case's expected-pg{major}.txt and compares nothing: tools/record-realistic.sh runs it so.
[Collection(UsesPostgres.Name)]
public sealed class RealisticDescriptionTests(PostgresContainer postgres)
{
    public const string RecordVariable = "SQLSOURCE_RECORD_REALISTIC";

    private const string KnownCase = "aggregate-cte-left-joined";

    private const string RecordScript = "tools/record-realistic.sh";

    private static bool Recording => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RecordVariable));

    [Fact]
    public async Task DescribeAsync_KnownCase_IsDescribedAndRendered()
    {
        var found = RealisticCases.Read(Path.Combine(RepositoryPaths.RealisticCases, KnownCase));

        var described = await RealisticDescribe.DescribeAsync(found, postgres.RealisticConnectionString);

        described.Failure.ShouldBeNull();
        RealisticDescribe.Render(described).ShouldStartWith("kind: rows\n");
    }

    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public async Task Describe_Case_IsRefusedOnlyAsItsCaseSays(string name)
    {
        var found = RealisticDescribe.Start(name, postgres.Major);

        var described = await RealisticDescribe.DescribeOnceAsync(found, postgres.RealisticConnectionString);

        if (found.Refusal is { } refusal)
        {
            described.Succeeded.ShouldBeFalse($"{found} is refused as {refusal}, and was described");
            described.Failure.Descriptor.Id.ShouldBe(refusal, $"{found} is refused as {refusal}");
            return;
        }

        _ = RealisticDescribe.ColumnsOf(found, described);
    }

    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public async Task Describe_Case_IsItsRecordedDescription(string name)
    {
        var found = RealisticDescribe.Start(name, postgres.Major);
        var path = found.ExpectedPath(postgres.Major);
        var expectation = RealisticDescribe.ExpectationOn(Recording, postgres.Major);
        // A recording on a major that is not recorded fails before the skip, which would hide it.
        if (expectation == Expectation.Refuse)
        {
            Assert.Fail(RealisticDescribe.RefuseMessage);
        }

        postgres.SkipUnlessRecordedMajor("The expectations");

        var rendered = RealisticDescribe.Render(
            await RealisticDescribe.DescribeOnceAsync(found, postgres.RealisticConnectionString)
        );
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
}
