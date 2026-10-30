using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Settings.Libraries;

/// <summary>One entry of the library types: what a level says about one library.</summary>
/// <param name="Library">The library.</param>
/// <param name="State">What the level says.</param>
/// <param name="Place">Where the entry was written.</param>
/// <param name="Marker">
/// Where the marker that gave the entry is in its file's text, with the line and column of that span, which a
/// diagnostic about the entry reports.  Null for an entry of a settings file, whose reader keeps the position of each
/// member apart from the level, so that an edit that only moves the member changes no level.
/// </param>
internal sealed record LibrarySetting(
    SqlLibrary Library,
    LibraryState State,
    SettingPlace Place,
    (TextSpan Span, LinePositionSpan LineSpan)? Marker = null
);
