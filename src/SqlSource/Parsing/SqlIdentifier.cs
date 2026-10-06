using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace SqlSource.Parsing;

/// <summary>
/// The C# identifier rules that query names and token names must meet.
/// </summary>
internal static class SqlIdentifier
{
    /// <summary>
    /// True when <paramref name="value" /> has the form of an identifier.  Keywords pass.  A Unicode formatting
    /// character such as a zero-width space does not: C# allows one in an identifier and then ignores it, so two names
    /// that differ only by one would collide in generated code.
    /// </summary>
    public static bool IsValid(string value) =>
        SyntaxFacts.IsValidIdentifier(value)
        && !value.Any(static c => char.GetUnicodeCategory(c) == UnicodeCategory.Format);

    /// <summary>
    /// True for a reserved keyword such as <c>class</c>.  A contextual keyword such as <c>where</c> is not one.
    /// </summary>
    public static bool IsReservedKeyword(string value) => SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None;

    /// <summary>True when <paramref name="value" /> can be the name of a generated member or parameter.</summary>
    public static bool IsUsableName(string value) => IsValid(value) && !IsReservedKeyword(value);
}
