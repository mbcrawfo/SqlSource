using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;
using SqlSource.Generation.Paths;
using SqlSource.Settings;
using SqlSource.SettingsFiles;

namespace SqlSource.Generation.Chains;

/// <summary>
/// One settings file the build or the tool lists, read, with the problems that its side reports.
/// </summary>
/// <param name="Path">The file's path as the compiler gives it.</param>
/// <param name="NormalizedFolder">The normalised path of the file's folder, empty for the root.</param>
/// <param name="Depth">The number of segments of <paramref name="NormalizedFolder" />, 0 for the root.</param>
/// <param name="File">What the file says.</param>
/// <param name="Diagnostics">The problems of the file that its side reports, in the order the reader gave them.</param>
internal sealed record ListedSettingsFile(
    string Path,
    string NormalizedFolder,
    int Depth,
    SettingsFile File,
    EquatableArray<DiagnosticInfo> Diagnostics
)
{
    /// <summary>
    /// Whether the last segment of <paramref name="path" /> is <see cref="SettingsFile.FileName" />, exactly.
    /// </summary>
    public static bool IsSettingsFile(string path)
    {
        const string Name = SettingsFile.FileName;
        if (!path.EndsWith(Name, StringComparison.Ordinal))
        {
            return false;
        }

        var before = path.Length - Name.Length - 1;
        return before < 0 || path[before] is '/' or '\\';
    }

    /// <summary>
    /// Reads <paramref name="file" />.  Null when it is not a settings file or its path does not normalise.  A text
    /// the compiler cannot give is read as empty, which is not JSON.
    /// </summary>
    public static ListedSettingsFile? Read(
        AdditionalText file,
        SettingsFileSide side,
        CancellationToken cancellationToken
    )
    {
        if (!IsSettingsFile(file.Path) || SqlPath.Normalize(file.Path) is not { } normalized)
        {
            return null;
        }

        var folder = SqlPath.GetFolder(normalized);
        var depth = DepthOf(folder);
        var text = file.GetText(cancellationToken) ?? SourceText.From(string.Empty);
        var read = SettingsFileReader.Read(text.ToString(), SettingPlace.OfSettingsFile(file.Path, depth));
        return new ListedSettingsFile(file.Path, folder, depth, read, DiagnosticsOf(read, side, file.Path, text));
    }

    // The number of segments of a normalised folder, which has no empty segment.
    private static int DepthOf(string normalizedFolder)
    {
        if (normalizedFolder.Length == 0)
        {
            return 0;
        }

        var depth = 1;
        for (var i = 0; i < normalizedFolder.Length; i++)
        {
            if (normalizedFolder[i] == '/')
            {
                depth++;
            }
        }

        return depth;
    }

    // The array is made only for a file with problems.
    private static EquatableArray<DiagnosticInfo> DiagnosticsOf(
        SettingsFile read,
        SettingsFileSide side,
        string path,
        SourceText text
    )
    {
        ImmutableArray<DiagnosticInfo>.Builder? diagnostics = null;
        for (var i = 0; i < read.Errors.Count; i++)
        {
            var error = read.Errors[i];
            if (SettingsFileSides.Reports(side, error))
            {
                (diagnostics ??= ImmutableArray.CreateBuilder<DiagnosticInfo>()).Add(
                    SettingsFileDiagnostics.Create(error, path, text)
                );
            }
        }

        return diagnostics is null
            ? EquatableArray<DiagnosticInfo>.Empty
            : new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable());
    }
}
