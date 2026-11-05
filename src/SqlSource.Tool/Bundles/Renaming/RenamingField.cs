using System.Text.Json.Nodes;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>One string member of a call of the capture, as the renaming meets it.</summary>
/// <param name="Call">The call's index in the capture, from zero.</param>
/// <param name="Method">The call's method: <c>open</c>, <c>prepare</c> and the like.</param>
/// <param name="Path">
/// The member's path from the call: <c>request.sql</c>, <c>answer.rows[].name</c>, <c>answer.error.message</c>.
/// </param>
/// <param name="Value">The member's original value.</param>
/// <param name="Row">The object that holds the member when it is a row of an answer, or null.</param>
internal sealed record RenamingField(int Call, string Method, string Path, string Value, JsonObject? Row);
