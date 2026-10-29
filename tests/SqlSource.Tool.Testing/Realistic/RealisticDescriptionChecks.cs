using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tool.Testing.Realistic;

// What the describer gives for each realistic case against a real server, whole: the refusal its case says or none,
// and for a case with models the entry its committed sidecar holds, every field of it.  Both engines'
// RealisticDescriptionTests run it, on the server of that major and the connection to its database "realistic".
// Whether its nullability is safe is RealisticNullabilitySafetyTests'.
internal sealed class RealisticDescriptionChecks(RealisticDescribe describe, int major, string connection)
{
    // The script that writes the sidecars of the realistic cases.
    private const string SidecarScript = "tools/describe-end-to-end.sh";

    // A server for EntryBuilder, which reads only its version: the placeholder that both entries are compared with.
    private static readonly ServerInfo AnyServer = new(AnyVersion, null, null, new EquatableArray<ServerSetting>([]));

    private const string AnyVersion = "*";

    public async Task ShouldBeRefusedOnlyAsItsCaseSaysAsync(string name)
    {
        var found = describe.Start(name, major);

        var described = await describe.DescribeOnceAsync(found, connection);

        if (found.Refusal is { } refusal)
        {
            described.Succeeded.ShouldBeFalse($"{found} is refused as {refusal}, and was described");
            described.Failure.Descriptor.Id.ShouldBe(refusal, $"{found} is refused as {refusal}");
            return;
        }

        _ = RealisticDescriptions.ColumnsOf(found, described);
    }

    // The entry of each case with models is the one its sidecar holds, whole: what --check compares, the origins
    // among it, and the provenance, which it does not.  Only the server's version and where the query's name stands in
    // the text are left out.  A case that an earlier major describes otherwise is left out on it by its checkedFrom.
    public async Task ShouldBeItsCommittedEntryAsync(string name)
    {
        var found = describe.Start(name, major);
        describe.Corpus.Image.SkipUnlessRecordedMajor(major, "The committed entries");
        Assert.SkipUnless(found.HasModels, $"{found} has no models, and so no sidecar.");
        Assert.SkipUnless(
            found.IsCheckedOn(major),
            $"{found} is checked from {describe.Corpus.Engine.Product} {found.CheckedFrom?.Major}: "
                + $"{found.CheckedFrom?.Why}"
        );
        var committed = RealisticModels.EntryOf(found);

        var described = await describe.DescribeOnceAsync(found, connection);

        _ = RealisticDescriptions.ColumnsOf(found, described);
        TextOf(describe.EntryOf(found, AnyServer, described.Value!))
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
