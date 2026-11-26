using System;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing.Markers;

/// <summary>
/// Recognises marker comments.
/// </summary>
internal static class SqlMarkerReader
{
    private static readonly (string Keyword, SqlMarkerKind Kind)[] Keywords =
    [
        ("name:", SqlMarkerKind.Name),
        ("summary:", SqlMarkerKind.Summary),
        ("generator:", SqlMarkerKind.GeneratorParameters),
        ("dialect:", SqlMarkerKind.Dialect),
        ("token:", SqlMarkerKind.Token),
        ("token-ignore:", SqlMarkerKind.TokenIgnore),
        ("param:", SqlMarkerKind.Param),
        ("database:", SqlMarkerKind.Database),
        ("output:", SqlMarkerKind.Output),
        ("input-model-suffix:", SqlMarkerKind.InputModelSuffix),
        ("output-model-suffix:", SqlMarkerKind.OutputModelSuffix),
        ("model-namespace:", SqlMarkerKind.ModelNamespace),
        ("input-model-type:", SqlMarkerKind.InputModelType),
        ("output-model-type:", SqlMarkerKind.OutputModelType),
        ("input-model:", SqlMarkerKind.InputModel),
        ("output-model:", SqlMarkerKind.OutputModel),
        ("collection-type:", SqlMarkerKind.CollectionType),
        ("type-override:", SqlMarkerKind.TypeOverride),
        ("column:", SqlMarkerKind.Column),
        ("library-types:", SqlMarkerKind.LibraryTypes),
        ("member-location:", SqlMarkerKind.MemberLocation),
        ("visibility:", SqlMarkerKind.Visibility),
        ("nested-class:", SqlMarkerKind.NestedClass),
        ("variable:", SqlMarkerKind.Variable),
    ];

    /// <summary>
    /// Returns the marker that <paramref name="lexeme" /> is, or null.  A marker is a line comment that is the first
    /// thing on its line and reads <c>--</c>, optional blanks, a keyword, a colon.  <paramref name="rules" /> say what
    /// ends a line: they are those the lexeme was read by.
    /// </summary>
    public static SqlMarker? Read(string text, SqlLexeme lexeme, SqlDialectRules rules)
    {
        if (!IsDashComment(text, lexeme) || !StartsLine(text, lexeme.Span.Start, rules))
        {
            return null;
        }

        var index = FindKeyword(text, lexeme.Span, out var valueStart);
        return index < 0
            ? null
            : new SqlMarker(Keywords[index].Kind, lexeme.Span, SqlText.Trim(text, valueStart, lexeme.Span.End));
    }

    /// <summary>
    /// True for a line comment in the form of a marker, wherever it stands: <c>--</c>, optional blanks, a keyword, a
    /// colon.  One that is not the first thing on its line is no marker, but a line break before it would make it one.
    /// </summary>
    public static bool HasMarkerForm(string text, SqlLexeme lexeme) =>
        IsDashComment(text, lexeme) && FindKeyword(text, lexeme.Span, out _) >= 0;

    /// <summary>The word of a marker, without its colon: <c>token</c>.</summary>
    public static string WordOf(SqlMarkerKind kind)
    {
        foreach (var (keyword, candidate) in Keywords)
        {
            if (candidate == kind)
            {
                return keyword.Substring(0, keyword.Length - 1);
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// The marker as a message names it: its word, a colon, a space and its value, or the word and the colon for a
    /// marker without a value.
    /// </summary>
    public static string Describe(string text, SqlMarker marker)
    {
        var word = WordOf(marker.Kind) + ":";
        return marker.ValueSpan.IsEmpty
            ? word
            : word + " " + text.Substring(marker.ValueSpan.Start, marker.ValueSpan.Length);
    }

    /// <summary>
    /// A name marker's value, "Name" or "Name -> shape", split at its first arrow.  Allocates nothing.
    /// </summary>
    /// <param name="text">The text that holds the marker.</param>
    /// <param name="value">The marker's value.</param>
    /// <returns>
    /// The span of the name, without the white space before the arrow, and the index in <paramref name="text" /> of the
    /// first <c>-&gt;</c> in the value, or -1.  The shape is the text from <c>Arrow + 2</c> to the value's end.
    /// </returns>
    public static (TextSpan Name, int Arrow) SplitName(string text, TextSpan value)
    {
        var arrow = text.IndexOf("->", value.Start, value.Length, StringComparison.Ordinal);
        if (arrow < 0)
        {
            return (value, -1);
        }

        var nameEnd = arrow;
        while (nameEnd > value.Start && char.IsWhiteSpace(text[nameEnd - 1]))
        {
            nameEnd--;
        }

        return (TextSpan.FromBounds(value.Start, nameEnd), arrow);
    }

    // A line comment can start with a # in some dialects.  A marker always starts with two dashes.
    private static bool IsDashComment(string text, SqlLexeme lexeme) =>
        lexeme.Kind == SqlLexemeKind.LineComment && text[lexeme.Span.Start] == '-';

    // The index in Keywords of the keyword after the comment's dashes and blanks, and where its value starts, or -1.
    private static int FindKeyword(string text, TextSpan comment, out int valueStart)
    {
        var end = comment.End;
        var keywordStart = comment.Start + 2;
        while (keywordStart < end && text[keywordStart] is ' ' or '\t')
        {
            keywordStart++;
        }

        for (var index = 0; index < Keywords.Length; index++)
        {
            var keyword = Keywords[index].Keyword;
            valueStart = keywordStart + keyword.Length;
            if (
                valueStart <= end
                && string.Compare(text, keywordStart, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase)
                    == 0
            )
            {
                return index;
            }
        }

        valueStart = -1;
        return -1;
    }

    // Only blanks may come before the comment on its line.  The lexer guarantees that a line break found this way is
    // not inside a quoted region or a block comment: those end with their closing delimiter, never with a blank.
    private static bool StartsLine(string text, int start, SqlDialectRules rules)
    {
        var index = start - 1;
        while (index >= 0 && text[index] is ' ' or '\t')
        {
            index--;
        }

        return index < 0 || rules.IsLineTerminator(text[index]);
    }
}
