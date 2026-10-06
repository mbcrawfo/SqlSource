using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SqlSource.Generation;

/// <summary>
/// Whether the project's language version compiles the file generated for a type, which may use C# 12.
/// </summary>
internal static class LanguageSupport
{
    /// <summary>
    /// The project's language version as <c>LangVersion</c> writes it, when it is older than C# 12, and null
    /// otherwise.
    /// </summary>
    public static string? FindUnsupportedVersion(ParseOptions options) =>
        options is CSharpParseOptions { LanguageVersion: < LanguageVersion.CSharp12 } csharp
            ? csharp.LanguageVersion.ToDisplayString()
            : null;
}
