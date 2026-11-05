using System;
using System.Collections.Generic;

namespace SqlSource.Settings;

/// <summary>
/// The names of the generator parameters, how a word of them is read, and a list of them as a settings file writes it.
/// This is the only place the names are known.
/// </summary>
/// <remarks>
/// <para>
/// A level's list is words, and each word is read by <see cref="ReadWord" />: a parameter's name, alone for
/// <c>on</c> or followed by <c>=on</c>, <c>=off</c> or <c>=default</c>, or <c>default</c> alone, which gives every
/// parameter its default at that level.  A level names only the parameters it changes, as
/// <see cref="GeneratorParameterChoices" />: the levels merge per parameter, the nearer over the farther.  Its other
/// words apply over its <c>default</c>, wherever that stands.  One parameter given two different states in one level
/// is a contradiction; the same state given twice is not.
/// </para>
/// <para>
/// A <c>-- generator:</c> marker walks the words of its line, with <see cref="Words" />, and reports its problems in
/// <c>SqlGeneratorParameterScope</c>: it adds up over several lines, so it reports the word that contradicts an
/// earlier one and does not apply it.  A settings file's array is read by <see cref="ReadElements" />, which gives
/// the index of each problem.
/// </para>
/// </remarks>
internal static class GeneratorParameterList
{
    /// <summary>The word that gives every parameter its default.</summary>
    public const string Default = "default";

    /// <summary>Every parameter.</summary>
    public const GeneratorParameters All =
        GeneratorParameters.KeepComments
        | GeneratorParameters.NoTokenValidation
        | GeneratorParameters.SortInput
        | GeneratorParameters.SortOutput
        | GeneratorParameters.NoTableModels
        | GeneratorParameters.AsyncMethodSuffix
        | GeneratorParameters.NoArrayPool;

    /// <summary>The parameters that are on when no level says otherwise.  Every parameter is off for now.</summary>
    public const GeneratorParameters Defaults = GeneratorParameters.None;

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

    private static readonly GeneratorParameterState[] States =
    [
        GeneratorParameterState.On,
        GeneratorParameterState.Off,
        GeneratorParameterState.Default,
    ];

    /// <summary>
    /// Reads one word, ignoring case: a parameter's name, alone or followed by <c>=on</c>, <c>=off</c> or
    /// <c>=default</c>, or <see cref="Default" /> alone.  <paramref name="parameter" /> and <paramref name="state" />
    /// are set when the word is a <see cref="GeneratorWordKind.Parameter" />.  The name is what stands before the first
    /// <c>=</c>, so a word whose name is no parameter is <see cref="GeneratorWordKind.UnknownName" /> whatever
    /// follows.
    /// </summary>
    public static GeneratorWordKind ReadWord(
        ReadOnlySpan<char> word,
        out GeneratorParameters parameter,
        out GeneratorParameterState state
    )
    {
        parameter = GeneratorParameters.None;
        state = GeneratorParameterState.On;
        var separator = word.IndexOf('=');
        var name = separator < 0 ? word : word.Slice(0, separator);
        if (name.Equals(Default.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return separator < 0 ? GeneratorWordKind.Default : GeneratorWordKind.InvalidState;
        }

        if (!TryFind(name, out var found))
        {
            return GeneratorWordKind.UnknownName;
        }

        if (separator >= 0 && !TryReadState(word.Slice(separator + 1), out state))
        {
            return GeneratorWordKind.InvalidState;
        }

        parameter = found;
        return GeneratorWordKind.Parameter;
    }

    /// <summary>
    /// The words of <paramref name="text" />: each run of characters that are not white space, in order.  Nothing
    /// is allocated.
    /// </summary>
    public static WordEnumerator Words(ReadOnlySpan<char> text) => new(text);

    /// <summary>
    /// Reads a list written as an array, as a settings file writes it, each element one word read by
    /// <see cref="ReadWord" />.  An element is trimmed, and one that is empty or white space is skipped, so no word at
    /// all names nothing.  The index of a word that is no parameter, or whose state is not valid, is added to
    /// <paramref name="invalid" />, and the other words apply.  A word that gives a parameter another state than an
    /// earlier word gave it is added to <paramref name="repeated" />, with the index of that earlier word, and the
    /// earlier applies.  <c>default</c> gives every parameter its default, and the other words apply over it, wherever
    /// it stands.
    /// </summary>
    public static GeneratorParameterChoices ReadElements(
        IReadOnlyList<string> elements,
        List<int> invalid,
        List<(int Index, int First)> repeated
    )
    {
        var reset = false;
        var choices = GeneratorParameterChoices.None;
        // Beside each parameter written, the index of the first element that wrote it, and its state.
        var written = new List<(GeneratorParameters Parameter, GeneratorParameterState State, int Index)>();
        for (var index = 0; index < elements.Count; index++)
        {
            var word = elements[index].AsSpan().Trim();
            if (word.IsEmpty)
            {
                continue;
            }

            var kind = ReadWord(word, out var parameter, out var state);
            switch (kind)
            {
                case GeneratorWordKind.Default:
                    reset = true;
                    break;
                case GeneratorWordKind.Parameter:
                    var first = IndexOf(written, parameter);
                    if (first < 0)
                    {
                        written.Add((parameter, state, index));
                        choices = choices.With(parameter, state);
                    }
                    else if (written[first].State != state)
                    {
                        repeated.Add((index, written[first].Index));
                    }

                    break;
                case GeneratorWordKind.UnknownName:
                case GeneratorWordKind.InvalidState:
                    invalid.Add(index);
                    break;
                default:
                    throw UnexpectedEnumValueException.Of(kind);
            }
        }

        return reset ? choices.Over(GeneratorParameterChoices.Reset) : choices;
    }

    private static int IndexOf(
        List<(GeneratorParameters Parameter, GeneratorParameterState State, int Index)> written,
        GeneratorParameters parameter
    )
    {
        for (var index = 0; index < written.Count; index++)
        {
            if (written[index].Parameter == parameter)
            {
                return index;
            }
        }

        return -1;
    }

    // Finds a parameter by its name, ignoring case.
    private static bool TryFind(ReadOnlySpan<char> name, out GeneratorParameters parameter)
    {
        foreach (var (candidate, value) in Names)
        {
            if (name.Equals(candidate.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                parameter = value;
                return true;
            }
        }

        parameter = GeneratorParameters.None;
        return false;
    }

    // Reads on, off or default, ignoring case.
    private static bool TryReadState(ReadOnlySpan<char> text, out GeneratorParameterState state)
    {
        foreach (var candidate in States)
        {
            if (text.Equals(SettingValue.NameOf(candidate).AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                state = candidate;
                return true;
            }
        }

        state = GeneratorParameterState.On;
        return false;
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

/// <summary>
/// What a word of a list of generator parameters is, as <see cref="GeneratorParameterList.ReadWord" /> reads it.
/// </summary>
internal enum GeneratorWordKind
{
    /// <summary>A parameter's name, with a state or without one, which is <c>on</c>.</summary>
    Parameter,

    /// <summary><see cref="GeneratorParameterList.Default" /> alone: every parameter's default.</summary>
    Default,

    /// <summary>A word whose name, before any <c>=</c>, is no parameter.</summary>
    UnknownName,

    /// <summary>
    /// A parameter's name followed by <c>=</c> and a text that is not <c>on</c>, <c>off</c> or <c>default</c>, or
    /// <see cref="GeneratorParameterList.Default" /> followed by <c>=</c> and anything.
    /// </summary>
    InvalidState,
}
