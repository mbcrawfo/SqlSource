using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// The directives given by the <c>-- SqlSource:</c> markers of one scope: a block, or a file's preamble.
/// </summary>
/// <param name="headerEnd">
/// Where the file's header ends, as <see cref="SqlPreambleDialect.Apply" /> gives it.  A <c>dialect=</c> directive
/// is accepted only before it.
/// </param>
internal sealed class SqlDirectiveScope(int headerEnd)
{
    private const string KeepCommentsName = "keep-comments";
    private const string TokenValidationName = "token-validation";
    private const string NoTokenValidationName = "no-token-validation";
    private const string TokenIgnoreName = "token-ignore";
    private const string DialectName = "dialect";

    public bool KeepComments { get; private set; }

    public bool? TokenValidation { get; private set; }

    /// <summary>
    /// The dialect that a <c>dialect=</c> directive of this scope names, or null.  It is already in effect by the
    /// time the scope is read; it is kept here to find a second directive that names another.
    /// </summary>
    public SqlDialectChoice? Dialect { get; private set; }

    public HashSet<string> IgnoredTokens { get; } = [];

    /// <summary>
    /// Finds the first <c>dialect=</c> directive of <paramref name="marker" /> that names a dialect.  Nothing is
    /// allocated and nothing is reported.
    /// </summary>
    public static bool TryFindDialect(string text, SqlMarker marker, out SqlDialectChoice dialect)
    {
        var start = marker.ValueSpan.Start;
        var end = marker.ValueSpan.End;
        var valueOffset = DialectName.Length + 1;
        while (start < end)
        {
            var wordEnd = FindWordEnd(text, start, end);
            if (
                wordEnd - start >= valueOffset
                && text[start + DialectName.Length] == '='
                && string.Compare(text, start, DialectName, 0, DialectName.Length, StringComparison.OrdinalIgnoreCase)
                    == 0
                && SqlDialectName.TryParse(text.AsSpan(start + valueOffset, wordEnd - start - valueOffset), out dialect)
            )
            {
                return true;
            }

            start = SkipWhiteSpace(text, wordEnd, end);
        }

        dialect = default;
        return false;
    }

    /// <summary>
    /// Reports every <c>dialect</c> directive of <paramref name="marker" /> as misplaced, whatever it names.  For a
    /// marker that is not read into a scope because it comes after the last SQL of its block: such a directive is
    /// past the header by definition, and would otherwise be reported only as a marker at the end of its block.
    /// </summary>
    public static void ReportMisplacedDialects(string text, SqlMarker marker, List<SqlParseError> errors)
    {
        var start = marker.ValueSpan.Start;
        var end = marker.ValueSpan.End;
        while (start < end)
        {
            var wordEnd = FindWordEnd(text, start, end);
            var directive = text.Substring(start, wordEnd - start);
            var separator = directive.IndexOf('=');
            if (Is(separator < 0 ? directive : directive.Substring(0, separator), DialectName))
            {
                errors.Add(
                    SqlParseError.Create(
                        SqlParseErrorKind.MisplacedDialect,
                        TextSpan.FromBounds(start, wordEnd),
                        directive
                    )
                );
            }

            start = SkipWhiteSpace(text, wordEnd, end);
        }
    }

    /// <summary>
    /// Applies the directives of one <c>-- SqlSource:</c> marker to this scope, adding any problems to
    /// <paramref name="errors" />.
    /// </summary>
    public void Read(string text, SqlMarker marker, List<SqlParseError> errors)
    {
        if (marker.ValueSpan.IsEmpty)
        {
            errors.Add(SqlParseError.Create(SqlParseErrorKind.EmptyDirectiveLine, marker.Span));
            return;
        }

        var start = marker.ValueSpan.Start;
        var end = marker.ValueSpan.End;
        while (start < end)
        {
            var wordEnd = FindWordEnd(text, start, end);
            Apply(text.Substring(start, wordEnd - start), TextSpan.FromBounds(start, wordEnd), errors);
            start = SkipWhiteSpace(text, wordEnd, end);
        }
    }

    private static int FindWordEnd(string text, int start, int end)
    {
        while (start < end && !char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        return start;
    }

    private static int SkipWhiteSpace(string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        return start;
    }

    private static bool Is(string name, string directive) =>
        string.Equals(name, directive, StringComparison.OrdinalIgnoreCase);

    private void Apply(string directive, TextSpan span, List<SqlParseError> errors)
    {
        var separator = directive.IndexOf('=');
        var name = separator < 0 ? directive : directive.Substring(0, separator);
        var value = separator < 0 ? null : directive.Substring(separator + 1);
        SqlParseErrorKind? problem;
        if (Is(name, TokenIgnoreName))
        {
            problem = ApplyTokenIgnore(value);
        }
        else if (Is(name, DialectName))
        {
            problem = ApplyDialect(value, span);
        }
        else if (Is(name, KeepCommentsName) || Is(name, TokenValidationName) || Is(name, NoTokenValidationName))
        {
            problem = value is null ? ApplyFlag(name) : SqlParseErrorKind.InvalidDirectiveValue;
        }
        else
        {
            problem = SqlParseErrorKind.UnknownDirective;
        }

        if (problem is { } kind)
        {
            errors.Add(SqlParseError.Create(kind, span, directive));
        }
    }

    private SqlParseErrorKind? ApplyTokenIgnore(string? value)
    {
        if (value is null || !SqlIdentifier.IsValid(value))
        {
            return SqlParseErrorKind.InvalidDirectiveValue;
        }

        _ = IgnoredTokens.Add(value);
        return null;
    }

    // The place is checked first: a directive in the wrong place is reported as that, whatever it names.
    private SqlParseErrorKind? ApplyDialect(string? value, TextSpan span)
    {
        if (span.Start >= headerEnd)
        {
            return SqlParseErrorKind.MisplacedDialect;
        }

        if (!SqlDialectName.TryParse(value, out var dialect))
        {
            return SqlParseErrorKind.InvalidDirectiveValue;
        }

        if (Dialect is { } existing && existing != dialect)
        {
            return SqlParseErrorKind.ConflictingDirectives;
        }

        Dialect = dialect;
        return null;
    }

    private SqlParseErrorKind? ApplyFlag(string name)
    {
        if (Is(name, KeepCommentsName))
        {
            KeepComments = true;
            return null;
        }

        var validate = Is(name, TokenValidationName);
        if (TokenValidation is { } existing && existing != validate)
        {
            return SqlParseErrorKind.ConflictingDirectives;
        }

        TokenValidation = validate;
        return null;
    }
}
