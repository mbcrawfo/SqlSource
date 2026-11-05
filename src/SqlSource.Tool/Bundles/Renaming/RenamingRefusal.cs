namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>The step of the renaming that refused a bundle.</summary>
internal enum RefusalStep
{
    /// <summary>The SQL of a request is not the one the pipeline makes from the renamed query (the design's step 8).</summary>
    RequestSql,

    /// <summary>A plan's string the walk reads does not read (step 9).</summary>
    PlanString,

    /// <summary>A token that is no replacement, no word of the engine's, no number and no punctuation (step 12).</summary>
    Token,

    /// <summary>The renamed bundle does not replay to its renamed entry (step 13).</summary>
    Replay,
}

/// <summary>Why a bundle cannot be renamed safely, and where.</summary>
/// <param name="Step">The step that refused it.</param>
/// <param name="Place">
/// Where: a file of the bundle, <c>query.sql</c>, or a member of a call, <c>capture.json call &lt;n&gt; '&lt;method&gt;'
/// &lt;path&gt;</c>.
/// </param>
/// <param name="Token">The text refused, which is the user's own, or null when the step names none.</param>
internal sealed record RenamingRefusal(RefusalStep Step, string Place, string? Token);
