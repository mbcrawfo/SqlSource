using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;
using SqlSource.Testing.Snapshot;
using SqlSource.Tool.Planning;

namespace SqlSource.Tool.Tests;

// A sidecar as a file: one that a test puts beside a .sql file, or reads from there.  TestSidecars, of the
// generator's tests, builds the records.
internal static class SidecarFiles
{
    // A time long before any run, which a test gives a sidecar so that a write to it would show.
    public static DateTime Old { get; } = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);

    // The text of a sidecar that a newer tool wrote: a format above the one this tool writes.
    public static string Newer { get; } = Empty(2, "9.9.9");

    // An entry that is current for the query: its hash and its database, and a statement without rows.
    public static SidecarEntry EntryFor(PlannedQuery query) =>
        TestSidecars.Entry(
            query.Query.Name,
            query.Hash.ShouldNotBeNull(),
            database: query.Database,
            resultKind: SidecarResultKind.None
        );

    // The text of a sidecar with no entry.  The versions are the tool's unless given.
    public static string Empty(int formatVersion = SidecarFormat.Version, string? toolVersion = null) =>
        SidecarWriter.Write(
            new Sidecar(formatVersion, toolVersion ?? PackageVersion.Prefix, EquatableArray<SidecarEntry>.Empty)
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
