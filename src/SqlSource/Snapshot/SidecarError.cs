using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Snapshot;

/// <summary>Why a sidecar is malformed.  There is one for a file: the first that the reader finds.</summary>
/// <param name="Kind">What is wrong.</param>
/// <param name="Span">Where it is, as offsets into the text that was read.</param>
/// <param name="Argument">The text a message quotes, when the kind has one.</param>
internal sealed record SidecarError(SidecarErrorKind Kind, TextSpan Span, string? Argument);
