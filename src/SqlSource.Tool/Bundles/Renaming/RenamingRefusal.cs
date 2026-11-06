namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>
/// The step of the renaming that refused a bundle, each listed with who refuses by it in
/// <c>docs/architecture/bundles.md</c>, "Refusals".
/// </summary>
internal enum RefusalStep
{
    /// <summary>The SQL of a request is not the one the pipeline makes from the renamed query.</summary>
    RequestSql,

    /// <summary>A plan's string the walk reads does not read.</summary>
    PlanString,

    /// <summary>
    /// A string the renaming cannot rename safely, such as a member with no rule, SQL that does not lex or a position
    /// it cannot map, or a token of the renamed bundle that is no replacement, no word of the engine's, no number and
    /// no punctuation.
    /// </summary>
    Token,

    /// <summary>The renamed bundle does not replay to its renamed entry.</summary>
    Replay,
}

/// <summary>Why a bundle cannot be renamed safely, and where.</summary>
/// <param name="Step">The step that refused it.</param>
/// <param name="Place">
/// Where: a file of the bundle, <c>query.sql</c>; a call, <c>capture.json call &lt;n&gt; '&lt;method&gt;'</c>, and a
/// member of it, <c>capture.json call &lt;n&gt; '&lt;method&gt;' &lt;path&gt;</c>; a call the replay asked and the
/// capture lacks, <c>capture.json call '&lt;method&gt;'</c>; or a member of the entry, <c>expected.json
/// &lt;path&gt;</c>.
/// </param>
/// <param name="Token">The text refused, which is the user's own, or null when the step names none.</param>
internal sealed record RenamingRefusal(RefusalStep Step, string Place, string? Token);
