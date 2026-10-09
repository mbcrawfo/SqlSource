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

        public static readonly T[] Values = (T[])Enum.GetValues(typeof(T));
    }
}
