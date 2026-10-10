using System;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Looks for a connection's value in the text that a sidecar would hold for an entry.
/// </summary>
/// <remarks>
/// A sidecar is a file that is committed, and what a describer returns goes into it: the server's version, the names
/// and types of columns and parameters, the provenance.  The search is in the text as <see cref="SidecarWriter" />
/// writes it, for the value as <see cref="SidecarJsonBuilder" /> escapes it, since a value with a quote or a
/// backslash is in the file escaped and a search for the value as it is would not find it.
/// </remarks>
internal static class EntrySecretCheck
{
    /// <summary>Whether the text of a sidecar that holds only <paramref name="entry" /> holds the value.</summary>
    public static bool Holds(SidecarEntry entry, string secret)
    {
        if (secret.Length == 0)
        {
            return false;
        }

        var text = SidecarWriter.Write(
            new Sidecar(SidecarFormat.Version, PackageVersion.Prefix, new EquatableArray<SidecarEntry>([entry]))
        );

        // As written: the value between the quotes the builder puts round it.
        var written = SidecarJsonBuilder.Quote(secret)[1..^1];
        return text.Contains(written, StringComparison.Ordinal);
    }
}
