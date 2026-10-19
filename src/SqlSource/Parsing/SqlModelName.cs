using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// The name that an <c>-- input-model:</c> or <c>-- output-model:</c> marker gives, with where the marker is.
/// </summary>
/// <param name="Name">The full name or the name the marker gives.</param>
/// <param name="Span">The whole marker comment in the file.</param>
internal sealed record SqlModelName(string Name, TextSpan Span);
