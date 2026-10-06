using Microsoft.CodeAnalysis.CSharp;

namespace SqlSource.Parsing;

/// <summary>
/// The C# identifier rules that query names and token names must meet.
/// </summary>
internal static class SqlIdentifier
{
    /// <summary>True when <paramref name="value" /> has the form of an identifier.  Keywords pass.</summary>
    public static bool IsValid(string value) => SyntaxFacts.IsValidIdentifier(value);

    /// <summary>
    /// True for a reserved keyword such as <c>class</c>.  A contextual keyword such as <c>where</c> is not one.
    /// </summary>
    public static bool IsReservedKeyword(string value) => SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None;

    /// <summary>True when <paramref name="value" /> can be the name of a generated member or parameter.</summary>
    public static bool IsUsableName(string value) => IsValid(value) && !IsReservedKeyword(value);
}
