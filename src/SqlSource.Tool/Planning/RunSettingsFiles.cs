using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using SqlSource.Generation.Chains;
using SqlSource.Generation.Paths;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Planning;

/// <summary>
/// The settings files of a run, each read once however many projects list it, and reported once however many
/// projects reach it.  Each file is read by the generator's own <c>ListedSettingsFile</c>, for the tool's side, and a
/// project's files make a <c>SettingsFileSet</c>, as the generator makes one.
/// </summary>
internal sealed class RunSettingsFiles
{
    private readonly Dictionary<string, ListedSettingsFile?> _read = [with(StringComparer.Ordinal)];

    private readonly HashSet<string> _reported = [with(SqlPath.Comparer)];

    /// <summary>
    /// The set of the settings files that <paramref name="manifest" /> lists, of those named <c>sqlsource.json</c>
    /// exactly.  A file that cannot be read is read as empty, which is not JSON.
    /// </summary>
    public SettingsFileSet Of(ProjectManifest manifest, CancellationToken cancellationToken)
    {
        List<ListedSettingsFile>? files = null;
        foreach (var path in manifest.SettingsFiles)
        {
            if (!ListedSettingsFile.IsSettingsFile(path))
            {
                continue;
            }

            if (!_read.TryGetValue(path, out var file))
            {
                // A run that was cancelled reads no further file.
                cancellationToken.ThrowIfCancellationRequested();
                file = ListedSettingsFile.Read(new SqlFileText(path), SettingsFileSide.Tool, cancellationToken);
                _read.Add(path, file);
            }

            if (file is not null)
            {
                (files ??= []).Add(file);
            }
        }

        return files is null ? SettingsFileSet.Empty : SettingsFileSet.Of(files);
    }

    /// <summary>
    /// Adds the problems of each file of <paramref name="set" /> that the chain of the folder of some path of
    /// <paramref name="reachingPaths" /> reaches, in the order of the set's files, unless the file's folder was
    /// reported before in this run.
    /// </summary>
    /// <param name="set">The settings files of a project.</param>
    /// <param name="reachingPaths">The normalised paths of the project's files that reach a settings file.</param>
    /// <param name="errors">The errors of the run.</param>
    public void AddProblems(
        SettingsFileSet set,
        IEnumerable<string> reachingPaths,
        ImmutableArray<ToolDiagnostic>.Builder errors
    )
    {
        foreach (var file in set.FilesIn(set.Reached(reachingPaths)))
        {
            // Add is false for a folder that was reported before, which is then left out.
            if (!_reported.Add(file.NormalizedFolder))
            {
                continue;
            }

            errors.AddRange(file.Diagnostics.Select(ToolDiagnostic.From));
        }
    }
}
