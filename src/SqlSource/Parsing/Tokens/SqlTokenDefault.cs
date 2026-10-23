using SqlSource.Parsing.Markers;

namespace SqlSource.Parsing.Tokens;

/// <summary>
/// A default that a <c>-- token:</c> marker gives.
/// </summary>
/// <param name="Name">The token's name.</param>
/// <param name="Text">
/// The default, trimmed, as the marker writes it, comments included.  The parameters of the default are read from it.
/// </param>
/// <param name="Value">
/// The default without its comments, trimmed, as an inline default is read: what two defaults are compared by and what
/// the query's token gets.  The same string as <paramref name="Text" /> when the default has no comment.
/// </param>
/// <param name="Offset">Where <paramref name="Text" /> starts in the file.</param>
/// <param name="Marker">The marker.</param>
internal readonly record struct SqlTokenDefault(string Name, string Text, string Value, int Offset, SqlMarker Marker);
