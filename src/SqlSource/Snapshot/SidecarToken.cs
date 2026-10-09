using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Snapshot;

/// <summary>One token of a sidecar's JSON.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Span">Where it is, as offsets into the text.</param>
/// <param name="IsPlain">
/// For a string, that it has no escape, so its value is the text between its quotes.  For a number, that it has no
/// fraction and no exponent.  False for every other kind.
/// </param>
internal readonly record struct SidecarToken(SidecarTokenKind Kind, TextSpan Span, bool IsPlain = false);
