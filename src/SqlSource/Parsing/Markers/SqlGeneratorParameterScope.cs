using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Settings;

namespace SqlSource.Parsing.Markers;

/// <summary>
/// The generator parameters given by the <c>-- generator:</c> markers of one scope: a block, or a file's preamble.
/// </summary>
/// <remarks>
/// The markers of one scope add up to one level, which names only the parameters its words give a state, and merges
/// per parameter over the levels below it.  <c>default</c> gives every parameter its default at the scope's level, and
/// the scope's other words apply over it, wherever it stands.  A word that gives a parameter another state than an
/// earlier word of the scope gave it is a contradiction, reported and not applied.
/// </remarks>
internal sealed class SqlGeneratorParameterScope
{
    // Whether a word of the scope is "default".
    private bool _reset;

    // The values the scope's words have given, and the parameters they have given their default.  Each parameter is
    // in one of them at most, with one value: a word that would give it another is a contradiction.
    private GeneratorParameterChoices _written;

    private GeneratorParameters _defaulted;

    /// <summary>What the scope's words say about the generator parameters: empty when they name none.</summary>
    public GeneratorParameterChoices Parameters =>
        _written
            .With(_defaulted, GeneratorParameterState.Default)
            .Over(_reset ? GeneratorParameterChoices.Reset : GeneratorParameterChoices.None);

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

        // A marker's value has no white space around it, so the first word starts where the value does.
        var words = GeneratorParameterList.Words(text.AsSpan(marker.ValueSpan.Start, marker.ValueSpan.Length));
        while (words.MoveNext())
        {
            if (Apply(words.Current) is not { } problem)
            {
                continue;
            }

            var span = new TextSpan(marker.ValueSpan.Start + words.Start, words.Length);
            var word = words.Current.ToString();
            errors.Add(
                problem.Kind == SqlParseErrorKind.InvalidMarkerValue
                    ? SqlParseError.Create(problem.Kind, span, word, problem.Help)
                    : SqlParseError.Create(problem.Kind, span, word)
            );
        }
    }

    // The problem of a word, with the help that ends an invalid value's message, or null when it applies.
    private (SqlParseErrorKind Kind, string Help)? Apply(ReadOnlySpan<char> word)
    {
        var kind = GeneratorParameterList.ReadWord(word, out var definition, out var value);
        switch (kind)
        {
            case GeneratorWordKind.Default:
                _reset = true;
                return null;
            case GeneratorWordKind.UnknownName:
                return (SqlParseErrorKind.UnknownGeneratorParameter, string.Empty);
            case GeneratorWordKind.InvalidState:
                return (SqlParseErrorKind.InvalidMarkerValue, string.Empty);
            case GeneratorWordKind.Parameter:
                break;
            default:
                throw UnexpectedEnumValueException.Of(kind);
        }

        // The same value again is no contradiction, and the default is another value than any word's.
        var parameter = definition!.Parameter;
        var defaulted = (_defaulted & parameter) != GeneratorParameters.None;
        if (_written.ValueOf(parameter) is { } earlier ? earlier != value : defaulted && value is not null)
        {
            return (SqlParseErrorKind.ConflictingSettings, string.Empty);
        }

        if (value is { } given)
        {
            _written = _written.With(parameter, given);
        }
        else
        {
            _defaulted |= parameter;
        }

        return null;
    }
}
