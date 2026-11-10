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

    /// <summary>
    /// True for a contextual keyword such as <c>file</c> or <c>where</c>, and for <c>extension</c>, which is one from
    /// C# 14, whatever this compiler knows of it.
    /// </summary>
    public static bool IsContextualKeyword(string value) =>
        SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None || value == "extension";

    /// <summary>True when <paramref name="value" /> can be the name of a generated member or parameter.</summary>
    public static bool IsUsableName(string value) => IsValid(value) && !IsReservedKeyword(value);

    /// <summary>
    /// True when <paramref name="value" /> can be the name of a generated type: a usable name that is not one of the
    /// contextual keywords that C# refuses as the name of a type, <c>file</c> (CS9056), <c>required</c> (CS9029),
    /// <c>scoped</c> (CS9062) and, from C# 14, <c>extension</c> (CS9306).  They are refused in every version of the
    /// language, so that a project keeps building when it moves to a newer one.
    /// </summary>
    public static bool IsUsableTypeName(string value) =>
        IsUsableName(value) && value is not ("file" or "required" or "scoped" or "extension");

    private static bool IsFormatCharacter(char character) =>
        char.GetUnicodeCategory(character) == UnicodeCategory.Format;
}
