using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;
using SqlSource.Tool.Describing.Describers;
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

    // The script that writes the sidecars of the realistic cases.
    private const string SidecarScript = "tools/describe-end-to-end.sh";

    // What a run knows of the server, but its version, which an entry compared here does not hold.
    private static readonly ServerInfo AnyServer = new(AnyVersion, null, null, new EquatableArray<ServerSetting>([]));

    private const string AnyVersion = "*";

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
    public async Task DescribeAsync_Case_IsRefusedOnlyAsItsCaseSays(string name)
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
    public async Task DescribeAsync_Case_IsItsRecordedDescription(string name)
    {
        var found = RealisticDescribe.Start(name, postgres.Major);
        var path = found.ExpectedPath(postgres.Major);
        var expectation = RealisticDescribe.ExpectationOn(Recording.IsAsked(RecordVariable), postgres.Major);
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

    // The entry of each case with models is the one its sidecar holds, whole: what --check compares, and the
    // provenance and the origins, which it does not.  Only the server's version and where the query's name stands in
    // the text are left out.  A case that an earlier major describes otherwise is left out on it by its checkedFrom.
    [Theory]
    [MemberData(nameof(RealisticCasesTests.Cases), MemberType = typeof(RealisticCasesTests))]
    public async Task DescribeAsync_Case_IsItsCommittedEntry(string name)
    {
        var found = RealisticDescribe.Start(name, postgres.Major);
        postgres.SkipUnlessRecordedMajor("The committed entries");
        Assert.SkipUnless(found.HasModels, $"{found} has no models, and so no sidecar.");
        Assert.SkipUnless(
            found.IsCheckedOn(postgres.Major),
            $"{found} is checked from PostgreSQL {found.CheckedFrom?.Major}: {found.CheckedFrom?.Why}"
        );
        var token = TestContext.Current.CancellationToken;
        var committed = SidecarReader
            .Read(await File.ReadAllTextAsync(found.SidecarPath, token))
            .Sidecar.ShouldNotBeNull($"{found.SidecarPath} cannot be read")
            .Queries.ShouldHaveSingleItem(found.ToString());

        var described = await RealisticDescribe.DescribeOnceAsync(found, postgres.RealisticConnectionString);

        _ = RealisticDescribe.ColumnsOf(found, described);
        TextOf(RealisticDescribe.EntryOf(found, AnyServer, described.Value!))
            .ShouldBe(
                TextOf(committed with { ServerVersion = AnyVersion }),
                $"{found} is described otherwise than {found.SidecarPath} holds: read the difference, then write the "
                    + $"sidecars again with {SidecarScript}"
            );
    }

    // The entry as the sidecar's writer writes it, alone in a file: the text of the field that differs reads as a
    // diff of the sidecar.  The writer does not write where the name stands.
    private static string TextOf(SidecarEntry entry) =>
        SidecarWriter.Write(new Sidecar(SidecarFormat.Version, AnyVersion, new EquatableArray<SidecarEntry>([entry])));
}
