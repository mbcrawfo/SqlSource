using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// The directives given by the <c>-- SqlSource:</c> markers of one scope: a block, or a file's preamble.
/// </summary>
internal sealed class SqlDirectiveScope
{
    private const string KeepCommentsName = "keep-comments";
    private const string TokenValidationName = "token-validation";
    private const string NoTokenValidationName = "no-token-validation";
    private const string TokenIgnoreName = "token-ignore";

    public bool KeepComments { get; private set; }

    public bool? TokenValidation { get; private set; }

    public HashSet<string> IgnoredTokens { get; } = [];

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
            var wordEnd = start;
            while (wordEnd < end && !char.IsWhiteSpace(text[wordEnd]))
            {
                wordEnd++;
            }

            Apply(text.Substring(start, wordEnd - start), TextSpan.FromBounds(start, wordEnd), errors);
            start = wordEnd;
            while (start < end && char.IsWhiteSpace(text[start]))
            {
                start++;
            }
        }
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
