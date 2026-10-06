using System;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SqlSource.Generation;

/// <summary>
/// What the project's <c>SqlSourceTokenValidation</c> property asks for.  A query's own directive comes before it.
/// </summary>
/// <param name="Validate">
/// Whether a generated method checks its arguments.  True when the property is missing, empty or not valid.
/// </param>
/// <param name="InvalidValue">The property's value as written when it is not valid, and null otherwise.</param>
internal sealed record TokenValidationSetting(bool Validate, string? InvalidValue)
{
    /// <summary>
    /// Where the compiler puts the property.  It is there only because <c>build/SqlSource.props</c> lists the
    /// property as a <c>CompilerVisibleProperty</c>.
    /// </summary>
    public const string OptionName = "build_property.SqlSourceTokenValidation";

    private static readonly TokenValidationSetting On = new(true, null);

    private static readonly TokenValidationSetting Off = new(false, null);

    public static TokenValidationSetting Read(AnalyzerConfigOptions globalOptions) =>
        globalOptions.TryGetValue(OptionName, out var value) ? Parse(value) : On;

    public static TokenValidationSetting Parse(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase))
        {
            return On;
        }

        return string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase)
            ? Off
            : new TokenValidationSetting(true, value);
    }
}
