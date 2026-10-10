using System;

namespace SqlSource.Tool.Projects;

/// <summary>
/// A property given to MSBuild on its command line.
/// </summary>
internal static class MSBuildProperty
{
    /// <summary>
    /// The switch that sets a property: <c>-p:Name=value</c>, with the value escaped.
    /// </summary>
    /// <remarks>
    /// MSBuild splits what follows <c>-p:</c> at each <c>;</c> and <c>,</c> into several properties, and reads
    /// <c>%3B</c> as a semicolon.  A path holds any of the three: <c>-p:SolutionDir=/work/Acme, Inc/</c> is
    /// <c>MSB1006</c> as it stands.
    /// </remarks>
    public static string Switch(string name, string value) => "-p:" + name + "=" + Escape(value);

    /// <summary>
    /// The value as MSBuild must be given it to read it back whole.  The percent sign goes first, or the two
    /// after it would be escaped twice.
    /// </summary>
    public static string Escape(string value) =>
        value.AsSpan().IndexOfAny('%', ';', ',') < 0
            ? value
            : value
                .Replace("%", "%25", StringComparison.Ordinal)
                .Replace(";", "%3B", StringComparison.Ordinal)
                .Replace(",", "%2C", StringComparison.Ordinal);
}
