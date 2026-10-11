using System;

namespace SqlSource.Settings;

/// <summary>
/// Reads the values of settings.  A marker, an MSBuild property and the metadata of an item all read through here,
/// so that one setting has one set of values wherever it is written.
/// </summary>
internal static class SettingValue
{
    /// <summary>
    /// Reads a value from a fixed list: the name of a member of <typeparamref name="T" />, ignoring case and
    /// ignoring the hyphens, spaces and tabs inside <paramref name="value" />.  So <c>CodeGen</c>, <c>codegen</c>
    /// and <c>code-gen</c> are one value, and so are <c>SealedRecord</c> and <c>sealed record</c>.  Nothing is
    /// allocated.
    /// </summary>
    public static bool TryReadChoice<T>(ReadOnlySpan<char> value, out T choice)
        where T : struct, Enum
    {
        var names = Choices<T>.Names;
        for (var index = 0; index < names.Length; index++)
        {
            if (Matches(value, names[index]))
            {
                choice = Choices<T>.Values[index];
                return true;
            }
        }

        choice = default;
        return false;
    }

    /// <summary>
    /// The name of a member of <typeparamref name="T" /> in lower case, as a message names a choice.
    /// <see cref="TryReadChoice{T}" /> reads it back.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="choice" /> is no member.</exception>
    public static string NameOf<T>(T choice)
        where T : struct, Enum
    {
        var index = Array.IndexOf(Choices<T>.Values, choice);
        return index < 0 ? throw new ArgumentOutOfRangeException(nameof(choice)) : Choices<T>.LowerNames[index];
    }

    /// <summary>
    /// Whether <paramref name="value" /> names a database: one word of letters, digits, <c>-</c>, <c>_</c> and
    /// <c>.</c>.  The tool makes the name of an environment variable from it, so the rule is narrow on purpose.
    /// </summary>
    public static bool IsDatabaseName(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character) && character is not ('-' or '_' or '.'))
            {
                return false;
            }
        }

        return !value.IsEmpty;
    }

    /// <summary>
    /// Whether <paramref name="value" /> can end a type's name: one or more characters that can follow the first of
    /// a C# identifier.
    /// </summary>
    public static bool IsSuffix(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!SqlIdentifier.IsPartCharacter(character))
            {
                return false;
            }
        }

        return !value.IsEmpty;
    }

    /// <summary>
    /// Whether <paramref name="value" /> is identifiers that are not reserved keywords, joined by periods.  It is
    /// the rule of a namespace and of the name of a type, which is one identifier or a full name: nothing in the
    /// text tells the two apart.
    /// </summary>
    public static bool IsQualifiedName(ReadOnlySpan<char> value)
    {
        while (true)
        {
            var period = value.IndexOf('.');
            if (!SqlIdentifier.IsUsableName((period < 0 ? value : value.Slice(0, period)).ToString()))
            {
                return false;
            }

            if (period < 0)
            {
                return true;
            }

            value = value.Slice(period + 1);
        }
    }

    private static bool Matches(ReadOnlySpan<char> value, string name)
    {
        var matched = 0;
        foreach (var character in value)
        {
            if (character is '-' or ' ' or '\t')
            {
                continue;
            }

            if (matched == name.Length || char.ToUpperInvariant(character) != char.ToUpperInvariant(name[matched]))
            {
                return false;
            }

            matched++;
        }

        return matched == name.Length;
    }

    // The members of an enum, read once for each enum.
    private static class Choices<T>
        where T : struct, Enum
    {
        public static readonly string[] Names = Enum.GetNames(typeof(T));

        // From the enum and not from Names: S2743 wants a static field of a generic type to name its parameter.
        public static readonly string[] LowerNames = Array.ConvertAll(Enum.GetNames(typeof(T)), Lower);

        public static readonly T[] Values = (T[])Enum.GetValues(typeof(T));

        // Character by character: the build runs every analyzer rule, and CA1308 rejects string.ToLowerInvariant.
        private static string Lower(string name)
        {
            var characters = name.ToCharArray();
            for (var index = 0; index < characters.Length; index++)
            {
                characters[index] = char.ToLowerInvariant(characters[index]);
            }

            return new string(characters);
        }
    }
}
