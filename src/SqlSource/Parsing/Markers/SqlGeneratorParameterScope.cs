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

    // The parameters the scope's words have turned on, turned off and given their default.  Each parameter is in one
    // of them at most: a word that would put it in another is a contradiction.
    private GeneratorParameters _on;

    private GeneratorParameters _off;

    private GeneratorParameters _default;

    /// <summary>What the scope's words say about the generator parameters: empty when they name none.</summary>
    public GeneratorParameterChoices Parameters =>
        (_reset ? GeneratorParameterChoices.Reset : GeneratorParameterChoices.None)
            .With(_on, GeneratorParameterState.On)
            .With(_off, GeneratorParameterState.Off)
            .With(_default, GeneratorParameterState.Default);

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
                problem == SqlParseErrorKind.InvalidMarkerValue
                    ? SqlParseError.Create(problem, span, word, string.Empty)
                    : SqlParseError.Create(problem, span, word)
            );
        }
    }

    private SqlParseErrorKind? Apply(ReadOnlySpan<char> word)
    {
        var kind = GeneratorParameterList.ReadWord(word, out var parameter, out var state);
        switch (kind)
        {
            case GeneratorWordKind.Default:
                _reset = true;
                return null;
            case GeneratorWordKind.UnknownName:
                return SqlParseErrorKind.UnknownGeneratorParameter;
            case GeneratorWordKind.InvalidState:
                return SqlParseErrorKind.InvalidMarkerValue;
            case GeneratorWordKind.Parameter:
                break;
            default:
                throw UnexpectedEnumValueException.Of(kind);
        }

        // The same state again is no contradiction.
        ref var states = ref StatesOf(state);
        if (((_on | _off | _default) & ~states & parameter) != GeneratorParameters.None)
        {
            return SqlParseErrorKind.ConflictingSettings;
        }

        states |= parameter;
        return null;
    }

    private ref GeneratorParameters StatesOf(GeneratorParameterState state)
    {
        switch (state)
        {
            case GeneratorParameterState.On:
                return ref _on;
            case GeneratorParameterState.Off:
                return ref _off;
            case GeneratorParameterState.Default:
                return ref _default;
            default:
                throw UnexpectedEnumValueException.Of(state);
        }
    }
}
