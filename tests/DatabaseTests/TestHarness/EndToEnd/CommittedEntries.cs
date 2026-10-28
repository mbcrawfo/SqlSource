using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot.Reading;

namespace SqlSource.DatabaseTests.TestHarness.EndToEnd;

// The entries that committed sidecars hold, which the end-to-end CheckTests of both engines compare with the counts
// that "describe --check" prints.
internal static class CommittedEntries
{
    // The number of entries of these sidecar files that were described against the logical database.
    public static int Count(IEnumerable<string> sidecars, string database) =>
        sidecars
            .SelectMany(static path => SidecarReader.Read(File.ReadAllText(path)).Sidecar.ShouldNotBeNull(path).Queries)
            .Count(entry => entry.Database == database);
}
