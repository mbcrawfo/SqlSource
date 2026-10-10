using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Planning;

namespace SqlSource.Tool.Tests;

// A sidecar that a test puts beside a .sql file, or reads from there.
internal static class TestSidecar
{
    // An entry that is current for the query: its hash and its database, and a statement without rows.
    public static SidecarEntry EntryFor(PlannedQuery query) =>
        new(
            query.Query.Name,
            default,
            query.Hash.ShouldNotBeNull(),
            "postgres",
            query.Database,
            "16.4",
            SidecarResultKind.None,
            null,
            null,
            null,
            EquatableArray<SidecarParameter>.Empty,
            null
        );

    // Writes a sidecar beside the file and gives its path.  The versions are the tool's unless given.
    public static string Write(
        PlannedFile file,
        IEnumerable<SidecarEntry> entries,
        string? toolVersion = null,
        int formatVersion = SidecarFormat.Version
    )
    {
        var path = SidecarFormat.PathFor(file.Path);
        var sidecar = new Sidecar(
            formatVersion,
            toolVersion ?? PackageVersion.Prefix,
            new EquatableArray<SidecarEntry>([.. entries])
        );
        File.WriteAllText(path, SidecarWriter.Write(sidecar));
        return path;
    }

    // A sidecar with a current entry for every query of the file that needs one.
    public static string WriteCurrent(PlannedFile file) =>
        Write(file, file.Queries.Where(query => query.NeedsEntry).Select(EntryFor));

    public static Sidecar Read(string sqlPath) =>
        SidecarReader.Read(File.ReadAllText(SidecarFormat.PathFor(sqlPath))).Sidecar.ShouldNotBeNull();
}
