using Microsoft.CodeAnalysis.Text;
using SqlSource.Settings.Libraries;

namespace SqlSource.SettingsFiles;

/// <summary>
/// Where the member of the library types that gives a library's entry is, kept apart from the level, as the errors are,
/// so that an edit that only moves the member changes no level.
/// </summary>
/// <param name="Library">The library of the entry.</param>
/// <param name="Span">The member in the file's text, from its key to its value.</param>
/// <param name="LineSpan">The line and column of <paramref name="Span" />.</param>
internal sealed record LibraryMember(SqlLibrary Library, TextSpan Span, LinePositionSpan LineSpan);
