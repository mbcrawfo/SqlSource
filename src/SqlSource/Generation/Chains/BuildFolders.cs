using System.Text;
using Microsoft.CodeAnalysis.Diagnostics;
using SqlSource.Generation.MSBuild;
using SqlSource.Generation.Paths;

namespace SqlSource.Generation.Chains;

/// <summary>
/// The folders a build writes into, the project's intermediate and output folders, whose files are the build's and
/// not the project's: a compile path under either reaches no settings file.  The generator reads them from the
/// compiler's options with <see cref="Read" />, and the <c>sqlsource</c> tool can read them from a manifest's
/// properties the same way.
/// </summary>
/// <param name="IntermediateFolder">The normalised intermediate folder, or null when the build gives none.</param>
/// <param name="OutputFolder">The normalised output folder, or null when the build gives none.</param>
internal sealed record BuildFolders(string? IntermediateFolder, string? OutputFolder)
{
    /// <summary>
    /// The property that holds the full path of the project's <c>BaseIntermediateOutputPath</c>, which
    /// <c>build/SqlSource.targets</c> sets and <c>build/SqlSource.props</c> shows to the compiler.
    /// </summary>
    public const string IntermediateFolderName = "SqlSourceIntermediateFolder";

    /// <summary>The property that holds the full path of the project's <c>BaseOutputPath</c>.</summary>
    public const string OutputFolderName = "SqlSourceOutputFolder";

    /// <summary>A build that gives no folder, which leaves no compile path out.</summary>
    public static BuildFolders None { get; } = new(null, null);

    /// <summary>The folders of the project's properties in <paramref name="globalOptions" />.</summary>
    public static BuildFolders Read(AnalyzerConfigOptions globalOptions) =>
        Of(Value(globalOptions, IntermediateFolderName), Value(globalOptions, OutputFolderName));

    /// <summary>
    /// The folders of two values as the build gives them, each a full path with a separator at its end, its <c>%</c>,
    /// <c>#</c> and <c>;</c> written <c>%25</c>, <c>%23</c> and <c>%3B</c>, which this decodes.  The targets escape
    /// them because the compiler cuts a value at its first <c>#</c> or <c>;</c>.  A value that is not a full path with
    /// a separator at its end, such as a raw path cut inside a segment, names no folder, so that it never leaves out a
    /// path beside the folder it was cut from.
    /// </summary>
    public static BuildFolders Of(string? intermediateFolder, string? outputFolder)
    {
        var intermediate = FolderOf(intermediateFolder);
        var output = FolderOf(outputFolder);
        return intermediate is null && output is null ? None : new BuildFolders(intermediate, output);
    }

    /// <summary>
    /// Whether <paramref name="normalizedPath" /> is under either folder, segment by segment, by
    /// <see cref="SqlPath.IsInFolder" />.
    /// </summary>
    public bool Contains(string normalizedPath) =>
        (IntermediateFolder is not null && SqlPath.IsInFolder(normalizedPath, IntermediateFolder))
        || (OutputFolder is not null && SqlPath.IsInFolder(normalizedPath, OutputFolder));

    private static string? Value(AnalyzerConfigOptions globalOptions, string name) =>
        globalOptions.TryGetValue(MSBuildSettings.PropertyPrefix + name, out var value) ? value : null;

    // A folder whose normalised path is empty would be the root, which holds every path.
    private static string? FolderOf(string? value) =>
        value is { Length: > 0 }
        && SqlPath.IsRooted(value)
        && value[value.Length - 1] is '/' or '\\'
        && SqlPath.Normalize(Unescape(value)) is { Length: > 0 } normalized
            ? normalized
            : null;

    // The value with %25, %23 and %3B, or %3b, made the character each stands for, in one pass, so that %2523
    // is %23.  Any other % is kept.  A value without a % is returned as it is.
    private static string Unescape(string value)
    {
        var start = value.IndexOf('%');
        if (start < 0)
        {
            return value;
        }

        var unescaped = new StringBuilder(value.Length);
        _ = unescaped.Append(value, 0, start);
        var i = start;
        while (i < value.Length)
        {
            if (value[i] == '%' && i + 2 < value.Length && Escaped(value[i + 1], value[i + 2]) is { } character)
            {
                _ = unescaped.Append(character);
                i += 3;
            }
            else
            {
                _ = unescaped.Append(value[i]);
                i++;
            }
        }

        return unescaped.ToString();
    }

    private static char? Escaped(char first, char second) =>
        (first, second) switch
        {
            ('2', '5') => '%',
            ('2', '3') => '#',
            ('3', 'B' or 'b') => ';',
            _ => null,
        };
}
