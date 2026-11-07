using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using SqlSource.Parsing;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>
/// A copy of a reused parameter that a call records, as <see cref="IEngineRenaming.CopiesIn" /> reads it.
/// </summary>
/// <param name="Name">The copy's name, without its prefix.</param>
/// <param name="Parameter">The name of the query's parameter it copies, without its prefix.</param>
/// <param name="Suffix">
/// The text after the parameter in the copy that the pipeline makes of the renamed parameter, such as <c>_1</c>,
/// which holds nothing of the user's.
/// </param>
internal sealed record ParameterCopy(string Name, string Parameter, string Suffix);

/// <summary>
/// The copies of reused parameters that a capture's calls record, found by a copy's name as the pipeline compares
/// parameters' names, ignoring case.
/// </summary>
internal sealed class RenamingCopies
{
    private readonly Dictionary<string, ParameterCopy> _copies;

    private RenamingCopies(Dictionary<string, ParameterCopy> copies)
    {
        _copies = copies;
    }

    /// <summary>No copy.</summary>
    public static RenamingCopies None { get; } = new([]);

    /// <summary>
    /// The copies that the engine's part reads from the calls, in order: the first record of a name is kept.
    /// </summary>
    /// <param name="engine">The engine's part.</param>
    /// <param name="calls">The calls.</param>
    /// <param name="side">The side of each call to read, <see cref="RenamingCall.Original" /> or
    /// <see cref="RenamingCall.Renamed" />.</param>
    public static RenamingCopies Of(
        IEngineRenaming engine,
        IEnumerable<RenamingCall> calls,
        Func<RenamingCall, JsonObject> side
    )
    {
        var copies = new Dictionary<string, ParameterCopy>(SqlParameterName.Comparer);
        foreach (var call in calls)
        {
            foreach (var copy in engine.CopiesIn(call.Method, side(call)))
            {
                _ = copies.TryAdd(copy.Name, copy);
            }
        }

        return new RenamingCopies(copies);
    }

    /// <summary>The copy of the name, ignoring case, or null when no call records one.</summary>
    public ParameterCopy? Find(string name) => _copies.GetValueOrDefault(name);
}
