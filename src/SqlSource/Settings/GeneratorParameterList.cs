using System;
using System.Collections.Generic;

namespace SqlSource.Settings;

/// <summary>
/// The names of the generator parameters, and a list of them as the attribute or a settings file writes it.  This is
/// the only place the names are known.
/// </summary>
/// <remarks>
/// <para>
/// A list is words separated by white space.  Each level that gives a list gives it whole: nothing is added to the
/// list of a level below.  The word <c>default</c>, alone or repeated, is the empty list.
/// </para>
/// <para>
/// A <c>-- generator:</c> marker walks the same words, with <see cref="Words" />, and holds them to a rule of its
/// own, in <c>SqlGeneratorParameterScope</c>.  The two rules differ on purpose.  A marker has a position for each
/// word and adds up over several lines, so it reports the one word that conflicts, and a word with <c>=</c> as a
/// value that is not valid.  An argument of the attribute has no position inside the value, so
/// <c>default</c> beside another word makes the whole value the thing to quote.
/// </para>
/// </remarks>
internal static class GeneratorParameterList
{
    /// <summary>The word that stands for the empty list.</summary>
    public const string Default = "default";

    /// <summary>
    /// Each name of a parameter, with the parameter.  The schema of a settings file repeats them in its list and its
    /// patterns, and a test compares the two.
    /// </summary>
    internal static readonly (string Name, GeneratorParameters Parameter)[] Names =
    [
        ("keep-comments", GeneratorParameters.KeepComments),
        ("no-token-validation", GeneratorParameters.NoTokenValidation),
        ("sort-input", GeneratorParameters.SortInput),
        ("sort-output", GeneratorParameters.SortOutput),
        ("no-table-models", GeneratorParameters.NoTableModels),
        ("async-method-suffix", GeneratorParameters.AsyncMethodSuffix),
        ("no-array-pool", GeneratorParameters.NoArrayPool),
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
    /// The words of <paramref name="text" />: each run of characters that are not white space, in order.  Nothing
    /// is allocated.
    /// </summary>
    public static WordEnumerator Words(ReadOnlySpan<char> text) => new(text);

    /// <summary>
    /// Reads a list that is not in a marker.  Null when the value is empty, or holds no word that is a parameter or
    /// <c>default</c>: the level then gives no list.  <c>default</c> alone or repeated is the empty list.  A word that
    /// is no parameter is added to <paramref name="invalid" /> and the other words apply.  <c>default</c> beside a word
    /// other than itself makes the whole value invalid.
    /// </summary>
    public static GeneratorParameters? Parse(string? value, List<string> invalid)
    {
        var whole = value.AsSpan().Trim();
        if (whole.IsEmpty)
        {
            return null;
        }

        GeneratorParameters? parameters = null;
        var hasDefault = false;
        var hasOther = false;
        foreach (var word in Words(whole))
        {
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

    /// <summary>
    /// Reads a list written as an array, as a settings file writes it, each element one word.  An element is trimmed,
    /// and one that is empty or white space is skipped.  No word at all is the empty list, which the level then gives,
    /// and so is <c>default</c> alone or repeated.  The index of a word that is no parameter is added to
    /// <paramref name="invalid" /> and the other words apply; null when every word is one.  <c>default</c> beside
    /// another word gives null, with the index of each <c>default</c> and each word that is no parameter added, in
    /// order: an array has a position for each element, so each is reported on its own.
    /// </summary>
    public static GeneratorParameters? ReadElements(IReadOnlyList<string> elements, List<int> invalid)
    {
        var start = invalid.Count;
        GeneratorParameters? parameters = null;
        var hasDefault = false;
        var hasOther = false;
        for (var index = 0; index < elements.Count; index++)
        {
            var word = elements[index].AsSpan().Trim();
            if (word.IsEmpty)
            {
                continue;
            }

            if (IsDefault(word))
            {
                // Named only if a word beside it makes the list invalid, and taken out again below otherwise.
                hasDefault = true;
                invalid.Add(index);
            }
            else if (TryFind(word, out var parameter))
            {
                hasOther = true;
                parameters = (parameters ?? GeneratorParameters.None) | parameter;
            }
            else
            {
                hasOther = true;
                invalid.Add(index);
            }
        }

        if (!hasDefault)
        {
            return hasOther ? parameters : GeneratorParameters.None;
        }

        if (hasOther)
        {
            return null;
        }

        // default alone: every index added is a default's.
        invalid.RemoveRange(start, invalid.Count - start);
        return GeneratorParameters.None;
    }

    /// <summary>Walks the words of a text.  <see cref="Start" /> is an offset into that text.</summary>
    internal ref struct WordEnumerator
    {
        private readonly ReadOnlySpan<char> _text;

        private int _next;

        public WordEnumerator(ReadOnlySpan<char> text)
        {
            _text = text;
            _next = 0;
            Start = 0;
            Length = 0;
        }

        /// <summary>Where the current word starts.</summary>
        public int Start { get; private set; }

        /// <summary>How long the current word is.</summary>
        public int Length { get; private set; }

        public readonly ReadOnlySpan<char> Current => _text.Slice(Start, Length);

        public readonly WordEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            var start = _next;
            while (start < _text.Length && char.IsWhiteSpace(_text[start]))
            {
                start++;
            }

            if (start == _text.Length)
            {
                return false;
            }

            var end = start;
            while (end < _text.Length && !char.IsWhiteSpace(_text[end]))
            {
                end++;
            }

            Start = start;
            Length = end - start;
            _next = end;
            return true;
        }
    }
}
