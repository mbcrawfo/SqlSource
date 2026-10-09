using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Settings;

namespace SqlSource.Parsing;

/// <summary>
/// The generator parameters given by the <c>-- generator:</c> markers of one scope: a block, or a file's preamble.
/// </summary>
/// <remarks>
/// The markers of one scope add up to the scope's list.  A list is never added to another scope's: a query that has
/// one uses it whole.
/// </remarks>
internal sealed class SqlGeneratorParameterScope
{
    // Whether the scope's list is the word "default".
    private bool _isDefault;

    /// <summary>The scope's list, or null when no marker of the scope gave a parameter.</summary>
    public GeneratorParameters? Parameters { get; private set; }

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
            if (Apply(text.AsSpan(start, wordEnd - start)) is { } problem)
            {
                errors.Add(
                    SqlParseError.Create(
                        problem,
                        TextSpan.FromBounds(start, wordEnd),
                        text.Substring(start, wordEnd - start)
                    )
                );
            }

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

    private SqlParseErrorKind? Apply(ReadOnlySpan<char> word)
    {
        var separator = word.IndexOf('=');
        var name = separator < 0 ? word : word.Slice(0, separator);
        if (GeneratorParameterList.IsDefault(name))
        {
            return ApplyDefault(separator >= 0);
        }

        if (!GeneratorParameterList.TryFind(name, out var parameter))
        {
            return SqlParseErrorKind.UnknownGeneratorParameter;
        }

        if (separator >= 0)
        {
            return SqlParseErrorKind.InvalidMarkerValue;
        }

        if (_isDefault)
        {
            return SqlParseErrorKind.ConflictingSettings;
        }

        Parameters = (Parameters ?? GeneratorParameters.None) | parameter;
        return null;
    }

    private SqlParseErrorKind? ApplyDefault(bool hasValue)
    {
        if (hasValue)
        {
            return SqlParseErrorKind.InvalidMarkerValue;
        }

        if (Parameters is not null && !_isDefault)
        {
            return SqlParseErrorKind.ConflictingSettings;
        }

        _isDefault = true;
        Parameters = GeneratorParameters.None;
        return null;
    }
}
