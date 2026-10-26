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
    /// The folders of two values as the build gives them, each a full path with a separator at its end.  A value that
    /// is not, such as one the compiler cut at a <c>#</c> or a <c>;</c>, names no folder, so that it never leaves out
    /// a path beside or above the folder it was cut from.  The targets give no value for such a folder, so this is a
    /// second guard.
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
        && SqlPath.Normalize(value) is { Length: > 0 } normalized
            ? normalized
            : null;
}
