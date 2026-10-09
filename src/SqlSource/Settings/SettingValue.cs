using System;
using System.Globalization;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Parsing;

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

    /// <summary>
    /// Whether <paramref name="value" /> can end a type's name: one or more characters that can follow the first of
    /// a C# identifier.
    /// </summary>
    public static bool IsSuffix(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (
                !SyntaxFacts.IsIdentifierPartCharacter(character)
                || char.GetUnicodeCategory(character) == UnicodeCategory.Format
            )
            {
                return false;
            }
        }

        return !value.IsEmpty;
    }

    /// <summary>
    /// Whether <paramref name="value" /> is a namespace: identifiers that are not reserved keywords, joined by
    /// periods.
    /// </summary>
    public static bool IsNamespace(ReadOnlySpan<char> value)
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

    /// <summary>
    /// Whether <paramref name="value" /> names a type: an identifier that is not a reserved keyword, alone or after
    /// a namespace and a period.  With a period it is a full name.
    /// </summary>
    public static bool IsTypeName(ReadOnlySpan<char> value) => IsNamespace(value);

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
