using System;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SqlSource.Generation.Repositories;

/// <summary>
/// Whether the project's repositories get their interfaces: the project's own MSBuild property, which
/// <c>build/SqlSource.props</c> shows to the compiler.  A type's <c>GenerateInterface</c> overrides it.
/// </summary>
internal static class RepositoryInterfaces
{
    public const string PropertyName = "SqlSourceRepositoryInterfaces";

    // Where the compiler puts the property: build/SqlSource.props shows it as a CompilerVisibleProperty.
    private const string OptionKey = "build_property." + PropertyName;

    /// <summary>
    /// Whether the property is anything but <c>false</c>, ignoring case.  None means on.
    /// </summary>
    public static bool Read(AnalyzerConfigOptions globalOptions) =>
        !(
            globalOptions.TryGetValue(OptionKey, out var value)
            && string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
        );
}
