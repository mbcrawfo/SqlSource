using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// The generator parameters given by the <c>-- generator:</c> markers of one scope: a block, or a file's preamble.
/// </summary>
internal sealed class SqlGeneratorParameterScope
{
    private const string KeepCommentsName = "keep-comments";
    private const string TokenValidationName = "token-validation";
    private const string NoTokenValidationName = "no-token-validation";
    private const string TokenIgnoreName = "token-ignore";

    public bool KeepComments { get; private set; }

    public bool? TokenValidation { get; private set; }

    public HashSet<string> IgnoredTokens { get; } = [];

    /// <summary>
    /// Applies the generator parameters of one <c>-- generator:</c> marker to this scope, adding any problems to
    /// <paramref name="errors" />.
    /// </summary>
    public void Read(string text, SqlMarker marker, List<SqlParseError> errors)
    {
        if (marker.ValueSpan.IsEmpty)
        {
            errors.Add(SqlParseError.Create(SqlParseErrorKind.EmptyGeneratorLine, marker.Span));
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

    private static bool Is(string name, string parameter) =>
        string.Equals(name, parameter, StringComparison.OrdinalIgnoreCase);

    private void Apply(string parameter, TextSpan span, List<SqlParseError> errors)
    {
        var separator = parameter.IndexOf('=');
        var name = separator < 0 ? parameter : parameter.Substring(0, separator);
        var value = separator < 0 ? null : parameter.Substring(separator + 1);
        SqlParseErrorKind? problem;
        if (Is(name, TokenIgnoreName))
        {
            problem = ApplyTokenIgnore(value);
        }
        else if (Is(name, KeepCommentsName) || Is(name, TokenValidationName) || Is(name, NoTokenValidationName))
        {
            problem = value is null ? ApplyFlag(name) : SqlParseErrorKind.InvalidMarkerValue;
        }
        else
        {
            problem = SqlParseErrorKind.UnknownGeneratorParameter;
        }

        if (problem is { } kind)
        {
            errors.Add(SqlParseError.Create(kind, span, parameter));
        }
    }

    private SqlParseErrorKind? ApplyTokenIgnore(string? value)
    {
        if (value is null || !SqlIdentifier.IsValid(value))
        {
            return SqlParseErrorKind.InvalidMarkerValue;
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
            return SqlParseErrorKind.ConflictingSettings;
        }

        TokenValidation = validate;
        return null;
    }
}
