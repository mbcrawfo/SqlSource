using System;
using System.Collections.Generic;

namespace SqlSource.Settings;

/// <summary>
/// The names of the generator parameters, and a list of them as a property, the metadata of an item or the
/// attribute writes it.  This is the only place the names are known.
/// </summary>
/// <remarks>
/// A list is words separated by white space.  Each level that gives a list gives it whole: nothing is added to the
/// list of a level below.  The word <c>default</c>, alone or repeated, is the empty list.
/// </remarks>
internal static class GeneratorParameterList
{
    /// <summary>The word that stands for the empty list.</summary>
    public const string Default = "default";

    private static readonly (string Name, GeneratorParameters Parameter)[] Names =
    [
        ("keep-comments", GeneratorParameters.KeepComments),
        ("no-token-validation", GeneratorParameters.NoTokenValidation),
        ("sort-input", GeneratorParameters.SortInput),
        ("sort-output", GeneratorParameters.SortOutput),
        ("no-table-models", GeneratorParameters.NoTableModels),
        ("async-method-suffix", GeneratorParameters.AsyncMethodSuffix),
    ];

    /// <summary>Finds a parameter by its name, ignoring case.</summary>
    public static bool TryFind(ReadOnlySpan<char> word, out GeneratorParameters parameter)
    {
        foreach (var (name, candidate) in Names)
        {
            if (word.Equals(name.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                parameter = candidate;
                return true;
            }
        }

        parameter = GeneratorParameters.None;
        return false;
    }

    /// <summary>Whether <paramref name="word" /> is <see cref="Default" />, ignoring case.</summary>
    public static bool IsDefault(ReadOnlySpan<char> word) =>
        word.Equals(Default.AsSpan(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads a list that is not in a marker.  Null when the value is empty or holds no word that is a parameter:
    /// the level then gives no list.  A word that is no parameter is added to <paramref name="invalid" /> and the
    /// other words apply.  <c>default</c> beside a word other than itself makes the whole value invalid.
    /// </summary>
    public static GeneratorParameters? Parse(string? value, List<string> invalid)
    {
        var rest = value.AsSpan().Trim();
        if (rest.IsEmpty)
        {
            return null;
        }

        var whole = rest;
        GeneratorParameters? parameters = null;
        var hasDefault = false;
        var hasOther = false;
        while (!rest.IsEmpty)
        {
            var length = 0;
            while (length < rest.Length && !char.IsWhiteSpace(rest[length]))
            {
                length++;
            }

            var word = rest.Slice(0, length);
            rest = rest.Slice(length).TrimStart();
            if (IsDefault(word))
            {
                hasDefault = true;
                continue;
            }

            hasOther = true;
            if (TryFind(word, out var parameter))
            {
                parameters = (parameters ?? GeneratorParameters.None) | parameter;
            }
            else
            {
                invalid.Add(word.ToString());
            }
        }

        if (!hasDefault)
        {
            return parameters;
        }

        if (!hasOther)
        {
            return GeneratorParameters.None;
        }

        invalid.Clear();
        invalid.Add(whole.ToString());
        return null;
    }
}
