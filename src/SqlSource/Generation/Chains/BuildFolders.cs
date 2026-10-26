using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.Diagnostics;
using SqlSource.Generation.MSBuild;
using SqlSource.Generation.Paths;

namespace SqlSource.Generation.Chains;

/// <summary>
/// The folders a build writes C# trees and its outputs into, whose files are the build's and not the project's: a
/// compile path under any of them reaches no settings file.  The generator reads them from the compiler's options
/// with <see cref="Read" />, and the <c>sqlsource</c> tool can read them from a manifest's properties the same way.
/// </summary>
/// <param name="Folders">The normalised folders, in the order the build gives them.</param>
internal sealed record BuildFolders(EquatableArray<string> Folders)
{
    /// <summary>
    /// The property that holds the folders, which <c>build/SqlSource.targets</c> sets and <c>build/SqlSource.props</c>
    /// shows to the compiler: the full paths of <c>BaseIntermediateOutputPath</c>, <c>IntermediateOutputPath</c>,
    /// <c>BaseOutputPath</c>, <c>OutDir</c> and, when the compiler writes its generated files,
    /// <c>CompilerGeneratedFilesOutputPath</c>, escaped and joined by <c>|</c>.
    /// </summary>
    public const string PropertyName = "SqlSourceBuildFolders";

    private const char Delimiter = '|';

    /// <summary>A build that gives no folder, which leaves no compile path out.</summary>
    public static BuildFolders None { get; } = new(EquatableArray<string>.Empty);

    /// <summary>The folders of the project's property in <paramref name="globalOptions" />.</summary>
    public static BuildFolders Read(AnalyzerConfigOptions globalOptions) =>
        Of(globalOptions.TryGetValue(MSBuildSettings.PropertyPrefix + PropertyName, out var value) ? value : null);

    /// <summary>
    /// The folders of the property as the build gives it: entries joined by <c>|</c>, each a full path with a
    /// separator at its end, its <c>%</c>, <c>|</c>, <c>#</c> and <c>;</c> written <c>%25</c>, <c>%7C</c>,
    /// <c>%23</c> and <c>%3B</c>, which this decodes.  The targets escape them because the compiler cuts a value at its
    /// first <c>#</c> or <c>;</c>, and <c>|</c> parts the entries.  An empty entry is left out, and so is one that is
    /// not a full path with a separator at its end, such as a raw path cut inside a segment, so that it never leaves
    /// out a path beside the folder it was cut from; the other entries still apply.
    /// </summary>
    public static BuildFolders Of(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return None;
        }

        ImmutableArray<string>.Builder? folders = null;
        var start = 0;
        while (start <= value!.Length)
        {
            var end = value.IndexOf(Delimiter, start);
            if (end < 0)
            {
                end = value.Length;
            }

            if (FolderOf(value.Substring(start, end - start)) is { } folder)
            {
                (folders ??= ImmutableArray.CreateBuilder<string>()).Add(folder);
            }

            start = end + 1;
        }

        return folders is null ? None : new BuildFolders(new EquatableArray<string>(folders.ToImmutable()));
    }

    /// <summary>
    /// Whether <paramref name="normalizedPath" /> is under any of the folders, segment by segment, by
    /// <see cref="SqlPath.IsInFolder" />.  The build gives a handful, so each is tested.
    /// </summary>
    public bool Contains(string normalizedPath)
    {
        for (var i = 0; i < Folders.Count; i++)
        {
            if (SqlPath.IsInFolder(normalizedPath, Folders[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The paths that reach a settings file, normalised, in the order given: each that is rooted, by
    /// <see cref="SqlPath.IsRooted" />, that normalises, and that is under none of the folders.  A path that is not
    /// rooted names no place to look for a settings file, and one under a folder is the build's file and not the
    /// project's.
    /// </summary>
    /// <param name="compilePaths">The paths of the C# files, as the compiler gives them.</param>
    public IEnumerable<string> ReachingPaths(IEnumerable<string> compilePaths)
    {
        foreach (var path in compilePaths)
        {
            if (SqlPath.IsRooted(path) && SqlPath.Normalize(path) is { } normalized && !Contains(normalized))
            {
                yield return normalized;
            }
        }
    }

    // A folder whose normalised path is empty would be the root, which holds every path.
    private static string? FolderOf(string entry) =>
        entry.Length > 0
        && SqlPath.IsRooted(entry)
        && entry[entry.Length - 1] is '/' or '\\'
        && SqlPath.Normalize(Unescape(entry)) is { Length: > 0 } normalized
            ? normalized
            : null;

    // The entry with %25, %7C, %23 and %3B, in either case, made the character each stands for, in one pass, so that
    // %2523 is %23.  Any other % is kept.  An entry without a % is returned as it is.
    private static string Unescape(string entry)
    {
        var start = entry.IndexOf('%');
        if (start < 0)
        {
            return entry;
        }

        var unescaped = new StringBuilder(entry.Length);
        _ = unescaped.Append(entry, 0, start);
        var i = start;
        while (i < entry.Length)
        {
            if (entry[i] == '%' && i + 2 < entry.Length && Escaped(entry[i + 1], entry[i + 2]) is { } character)
            {
                _ = unescaped.Append(character);
                i += 3;
            }
            else
            {
                _ = unescaped.Append(entry[i]);
                i++;
            }
        }

        return unescaped.ToString();
    }

    private static char? Escaped(char first, char second) =>
        (first, second) switch
        {
            ('2', '5') => '%',
            ('7', 'C' or 'c') => '|',
            ('2', '3') => '#',
            ('3', 'B' or 'b') => ';',
            _ => null,
        };
}
