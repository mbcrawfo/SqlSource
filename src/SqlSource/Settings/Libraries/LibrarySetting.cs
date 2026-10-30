using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Settings.Libraries;

/// <summary>One entry of the library types: what a level says about one library.</summary>
/// <param name="Library">The library.</param>
/// <param name="State">What the level says.</param>
/// <param name="Place">Where the entry was written.</param>
/// <param name="Span">
/// Where the entry's member is in the text of its source, from its key to its value, which a diagnostic about the entry
/// reports.
/// </param>
/// <param name="LineSpan">The line and column of <paramref name="Span" />.</param>
internal sealed record LibrarySetting(
    SqlLibrary Library,
    LibraryState State,
    SettingPlace Place,
    TextSpan Span,
    LinePositionSpan LineSpan
);
