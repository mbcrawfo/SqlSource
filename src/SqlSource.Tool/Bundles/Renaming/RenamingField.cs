using System.Text.Json.Nodes;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>One string member of a call of the capture, or of the entry, as the renaming meets it.</summary>
/// <param name="Call">
/// The call's index in the capture, from zero, or <see cref="NoCall" /> for a member of the entry.
/// </param>
/// <param name="Method">
/// The call's method: <c>open</c>, <c>prepare</c> and the like, or <see cref="BundleFiles.Expected" /> for the entry.
/// </param>
/// <param name="Path">
/// The member's path from the call: <c>request.sql</c>, <c>answer.rows[].name</c>, <c>answer.error.message</c>; or from
/// the entry: <c>columns[].type.name</c>.
/// </param>
/// <param name="Value">The member's original value.</param>
/// <param name="Row">The object that holds the member, or null for an element of an array.</param>
internal sealed record RenamingField(int Call, string Method, string Path, string Value, JsonObject? Row)
{
    /// <summary>The <see cref="Call" /> of a member of the entry, which is of no call.</summary>
    public const int NoCall = -1;
}
