using System;
using System.Linq;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Looks for a connection's value in what a describer gave of the text that a sidecar would hold for an entry.
/// </summary>
/// <remarks>
/// <para>
/// A sidecar is a file that is committed, and what a describer returns goes into it: the server's version, the names
/// and types of columns and parameters, the provenance.  The search is in the text as <see cref="SidecarWriter" />
/// writes it, for the value as <see cref="SidecarJsonBuilder" /> escapes it, since a value with a quote or a
/// backslash is in the file escaped and a search for the value as it is would not find it.
/// </para>
/// <para>
/// The text holds more than a describer gave: the words of the format, the engine, and the names of the query, of
/// its database and of its parameters.  A value that the text holds without the describer's part is in the file
/// whatever the describer did, so it is none the describer put there: a dummy value of a test or of a replay, such
/// as <c>postgres</c>, is no fault.
/// </para>
/// <para>
/// A member of an entry that a describer gives must be left out of that second text.  One that is kept there holds
/// the value in both texts, and the value in it is then never found.  So the second entry is made with the
/// constructors and not with <c>with</c>: a new member of an entry or of a parameter does not compile here until it
/// is given to one side, and <c>EntrySecretCheckTests</c> has a fact for each member that a describer gives.
/// </para>
/// </remarks>
internal static class EntrySecretCheck
{
    /// <summary>
    /// Whether the text of a sidecar that holds only <paramref name="entry" /> holds the value, and the same text
    /// without what a describer gave does not.
    /// </summary>
    public static bool Holds(SidecarEntry entry, string secret)
    {
        if (secret.Length == 0)
        {
            return false;
        }

        // As written: the value between the quotes the builder puts round it.
        var written = SidecarJsonBuilder.Quote(secret)[1..^1];
        return TextOf(entry).Contains(written, StringComparison.Ordinal)
            && !TextOf(WithoutTheDescribersPart(entry)).Contains(written, StringComparison.Ordinal);
    }

    private static string TextOf(SidecarEntry entry) =>
        SidecarWriter.Write(
            new Sidecar(SidecarFormat.Version, PackageVersion.Prefix, new EquatableArray<SidecarEntry>([entry]))
        );

    // What EntryBuilder takes from the plan alone, with the kind of result, which is one of two words of the format.
    private static SidecarEntry WithoutTheDescribersPart(SidecarEntry entry) =>
        new(
            entry.Name,
            entry.NameSpan,
            entry.Hash,
            entry.Engine,
            entry.Database,
            ServerVersion: null,
            entry.ResultKind,
            MatchesTable: null,
            Plan: null,
            TableMatch: null,
            new EquatableArray<SidecarParameter>([
                .. entry.Parameters.Select(static parameter => new SidecarParameter(
                    parameter.Name,
                    parameter.Ordinal,
                    Type: null,
                    parameter.Nullable,
                    TypeSource: null
                )),
            ]),
            Columns: null
        );
}
