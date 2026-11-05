using System.Text.Json.Nodes;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>One call of the capture, as the original holds it and as the renaming is writing it.</summary>
/// <param name="Index">The call's index in the capture, from zero.</param>
/// <param name="Method">The call's method: <c>open</c>, <c>prepare</c> and the like.</param>
/// <param name="Original">The call as captured, with its <c>request</c> and <c>answer</c>.  Never changed.</param>
/// <param name="Renamed">The call being renamed, with its <c>request</c> and <c>answer</c>.</param>
internal sealed record RenamingCall(int Index, string Method, JsonObject Original, JsonObject Renamed);
