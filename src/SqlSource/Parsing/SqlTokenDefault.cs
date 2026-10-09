namespace SqlSource.Parsing;

/// <summary>
/// A default that a <c>-- token:</c> marker gives.
/// </summary>
/// <param name="Name">The token's name.</param>
/// <param name="Text">The default, trimmed, as the marker writes it.</param>
/// <param name="Offset">Where <paramref name="Text" /> starts in the file.</param>
/// <param name="Marker">The marker.</param>
internal readonly record struct SqlTokenDefault(string Name, string Text, int Offset, SqlMarker Marker);
