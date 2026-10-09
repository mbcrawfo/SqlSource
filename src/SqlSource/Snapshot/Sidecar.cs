using System;

namespace SqlSource.Snapshot;

/// <summary>
/// A sidecar: what <c>sqlsource describe</c> learned about the queries of one <c>.sql</c> file.  The sidecar format
/// design is the contract, and this is its model.
/// </summary>
/// <param name="FormatVersion">The version of the format.</param>
/// <param name="ToolVersion">The version of the tool that wrote the file, as <c>major.minor.patch</c>.</param>
/// <param name="Queries">An entry for each query that needs types, in the file's order.</param>
internal sealed record Sidecar(int FormatVersion, string ToolVersion, EquatableArray<SidecarEntry> Queries)
{
    /// <summary>Returns the entry of the query with this name, compared ordinally, or null.</summary>
    public SidecarEntry? Find(string name)
    {
        // By index: Sonar asks for LINQ in place of a foreach that returns its item, and LINQ would box the array.
        for (var index = 0; index < Queries.Count; index++)
        {
            if (string.Equals(Queries[index].Name, name, StringComparison.Ordinal))
            {
                return Queries[index];
            }
        }

        return null;
    }
}
