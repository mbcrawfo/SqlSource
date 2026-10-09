using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace SqlSource;

/// <summary>
/// The C# identifier rules that a name must meet, whether a query, a token or a setting gives it.
/// </summary>
internal static class SqlIdentifier
{
    /// <summary>
    /// True when <paramref name="value" /> has the form of an identifier.  Keywords pass.  A Unicode formatting
    /// character such as a zero-width space does not: C# allows one in an identifier and then ignores it, so two names
    /// that differ only by one would collide in generated code.
    /// </summary>
    public static bool IsValid(string value) =>
        SyntaxFacts.IsValidIdentifier(value) && !value.Any(static c => IsFormatCharacter(c));

    /// <summary>
    /// True when <paramref name="character" /> can follow the first character of an identifier.  A Unicode formatting
    /// character cannot, for the reason <see cref="IsValid" /> gives.
    /// </summary>
    public static bool IsPartCharacter(char character) =>
        SyntaxFacts.IsIdentifierPartCharacter(character) && !IsFormatCharacter(character);

    /// <summary>
    /// True for a reserved keyword such as <c>class</c>.  A contextual keyword such as <c>where</c> is not one.
    /// </summary>
    public static bool IsReservedKeyword(string value) => SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None;

    /// <summary>True when <paramref name="value" /> can be the name of a generated member or parameter.</summary>
    public static bool IsUsableName(string value) => IsValid(value) && !IsReservedKeyword(value);

    private static bool IsFormatCharacter(char character) =>
        char.GetUnicodeCategory(character) == UnicodeCategory.Format;
}
