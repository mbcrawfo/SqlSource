using System;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SqlSource.Generation.Exceptions;

/// <summary>
/// Whether the project writes its exceptions <c>public</c>: the project's own MSBuild property, which
/// <c>build/SqlSource.props</c> shows to the compiler.  It is one value for the project, not a setting of its queries,
/// since a type has one accessibility.
/// </summary>
internal static class PublicExceptions
{
    public const string PropertyName = "SqlSourcePublicExceptions";

    // Where the compiler puts the property: build/SqlSource.props shows it as a CompilerVisibleProperty.
    private const string OptionKey = "build_property." + PropertyName;

    /// <summary>
    /// Whether the property is <c>true</c>, ignoring case.  Any other value, and none, means <c>internal</c>.
    /// </summary>
    public static bool Read(AnalyzerConfigOptions globalOptions) =>
        globalOptions.TryGetValue(OptionKey, out var value)
        && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
