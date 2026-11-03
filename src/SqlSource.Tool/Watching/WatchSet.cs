using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Tool.DotEnv;
using SqlSource.Tool.Projects;
using Path = System.IO.Path;

namespace SqlSource.Tool.Watching;

/// <summary>
/// Which folders a <c>describe --watch</c> session watches, and what a change to a file in them asks of it.
/// </summary>
/// <remarks>
/// Pure: it reads nothing but the <c>.git</c> markers that <see cref="DotEnvSearch.FoldersOf" /> looks for.  A set is
/// made from the projects of one evaluation, so a session makes another when it evaluates again.  Every comparison of
/// paths ignores case, as <see cref="RunProjects.PathComparer" /> does.
/// </remarks>
internal sealed class WatchSet
{
    private const string AssetsFileName = "project.assets.json";
    private const string SettingsFileName = "sqlsource.json";
    private const string SidecarSuffix = ".sql.json";
    private const string TemporarySuffix = ".tmp";
    private const int TemporaryIdLength = 32;

    private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdef");

    private static readonly string[] IgnoredFolders = ["bin", "obj", ".git"];

    // Any change to one of these asks for another evaluation, wherever it is.
    private static readonly string[] EvaluationFileNames =
    [
        "Directory.Build.props",
        "Directory.Build.targets",
        "Directory.Packages.props",
    ];

    private static readonly string[] EvaluationExtensions = [".csproj", ".sln", ".slnx"];

    private static StringComparer Comparer => RunProjects.PathComparer;

    private readonly string _unitPath;
    private readonly ImmutableArray<string> _projectFolders;
    private readonly HashSet<string> _listed = [with(Comparer)];
    private readonly HashSet<string> _envFolders = [with(Comparer)];

    private WatchSet(string unitPath, string unitFolder, ImmutableArray<ProjectManifest> manifests)
    {
        _unitPath = unitPath;
        _projectFolders = [.. manifests.Select(manifest => FolderOf(manifest.ProjectPath)).Distinct(Comparer)];
        foreach (var manifest in manifests)
        {
            _listed.UnionWith(manifest.Files);
            _listed.UnionWith(manifest.SettingsFiles);
            _listed.UnionWith(manifest.CompileFiles);
        }

        _envFolders.UnionWith(DotEnvSearch.FoldersOf(unitFolder));
        Folders = FoldersOf(unitFolder, manifests);
    }

    /// <summary>
    /// The folders to watch, sorted by ordinal comparison so that two sets compare: the unit's folder and each
    /// project's folder with their folders below, and each other folder that holds a listed file or may hold a
    /// <c>.env</c> or a <c>Directory.*</c> file, alone.  None is below another that is watched with its folders below,
    /// and none is there twice.  A folder may not exist, which the session skips.
    /// </summary>
    public ImmutableArray<(string Folder, bool Recursive)> Folders { get; }

    /// <summary>
    /// Makes the set of a unit: the solution or the project <paramref name="unitPath" /> in
    /// <paramref name="unitFolder" />, and the manifests of the projects it runs.
    /// </summary>
    public static WatchSet Of(string unitPath, string unitFolder, ImmutableArray<ProjectManifest> manifests) =>
        new(unitPath, unitFolder, manifests);

    /// <summary>
    /// What the change asks of the session.  A <see cref="FileChangeKind.Renamed" /> asks as much as the larger of its
    /// two sides, the old path as a deletion and the new as a creation.  A deletion of a listed path, or of a folder
    /// above one, asks nothing here: <see cref="Gone" /> names the paths it may have removed.
    /// </summary>
    public WatchEffect Classify(FileChange change) =>
        change.Kind switch
        {
            FileChangeKind.Changed => EffectOf(change.Path, change.IsFolder, Side.Changed),
            FileChangeKind.Created => EffectOf(change.Path, change.IsFolder, Side.Created),
            FileChangeKind.Deleted => EffectOf(change.Path, isFolder: false, Side.Removed),
            FileChangeKind.Renamed => EffectOfRename(change),
            _ => throw new ArgumentOutOfRangeException(nameof(change), change.Kind, "Not a kind of change."),
        };

    /// <summary>
    /// The listed paths that the change may have removed: the path of a deletion or the old path of a rename, when it
    /// is listed or holds listed paths, each once.  Empty for every other change, and for a path that is ignored.
    /// </summary>
    public ImmutableArray<string> Gone(FileChange change)
    {
        var removed = change.Kind == FileChangeKind.Deleted ? change.Path : null;
        if (change.Kind == FileChangeKind.Renamed)
        {
            removed = change.OldPath;
        }

        if (removed is null || IsIgnored(removed))
        {
            return [];
        }

        removed = Path.TrimEndingDirectorySeparator(removed);
        return
        [
            .. _listed
                .Where(listed => Comparer.Equals(listed, removed) || IsBelow(listed, removed))
                .Order(StringComparer.Ordinal),
        ];
    }

    private WatchEffect EffectOfRename(FileChange change)
    {
        var created = EffectOf(change.Path, change.IsFolder, Side.Created);
        var removed = change.OldPath is null
            ? WatchEffect.None
            : EffectOf(change.OldPath, isFolder: false, Side.Removed);
        return created > removed ? created : removed;
    }

    private WatchEffect EffectOf(string path, bool isFolder, Side side)
    {
        if (IsIgnored(path))
        {
            return WatchEffect.None;
        }

        var name = Path.GetFileName(path);
        if (side != Side.Removed && _listed.Contains(path))
        {
            return WatchEffect.Describe;
        }

        if (!isFolder && IsEvaluationFile(path, name))
        {
            return WatchEffect.Evaluate;
        }

        if (side == Side.Created && IsNewProjectContent(path, name, isFolder))
        {
            return WatchEffect.Evaluate;
        }

        return
            !isFolder
            && Comparer.Equals(name, DotEnvSearch.FileName)
            && Path.GetDirectoryName(path) is { } folder
            && _envFolders.Contains(folder)
            ? WatchEffect.Describe
            : WatchEffect.None;
    }

    private bool IsEvaluationFile(string path, string name) =>
        Comparer.Equals(path, _unitPath)
        || EvaluationFileNames.Contains(name, Comparer)
        || EvaluationExtensions.Any(extension => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        || IsProjectAssets(path, name);

    // A file or a folder that a project's folder did not have, and a project may list now: a .sql or .cs file, a
    // settings file, or a folder the globs may reach.
    private bool IsNewProjectContent(string path, string name, bool isFolder) =>
        _projectFolders.Any(project => IsBelow(path, project))
        && (
            isFolder
            || name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            || Comparer.Equals(name, SettingsFileName)
        );

    private static bool IsProjectAssets(string path, string name) =>
        Comparer.Equals(name, AssetsFileName) && Comparer.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "obj");

    // Sidecars and the temporary files the sidecar store makes are the tool's own writes.  Everything in bin, obj and
    // .git is the build's, or the repository's, except the one file of obj that a restore writes.  Only the parts of
    // the path below the nearest watched folder count, so that a folder named bin above the project is not one.
    private bool IsIgnored(string path)
    {
        var name = Path.GetFileName(path);
        if (name.EndsWith(SidecarSuffix, StringComparison.OrdinalIgnoreCase) || IsTemporary(name))
        {
            return true;
        }

        var parts = PartsBelowWatched(path);
        if (parts.Length >= 2 && IsProjectAssets(path, name))
        {
            parts = parts[..^2];
        }

        return parts.Any(part => IgnoredFolders.Contains(part, Comparer));
    }

    // <sidecar>.<32 digits of a Guid>.tmp, as the sidecar store names them.
    private static bool IsTemporary(string name)
    {
        var sidecarLength = name.Length - 1 - TemporaryIdLength - TemporarySuffix.Length;
        return sidecarLength > 0
            && name.EndsWith(TemporarySuffix, StringComparison.Ordinal)
            && name[sidecarLength] == '.'
            && name[..sidecarLength].EndsWith(SidecarSuffix, StringComparison.OrdinalIgnoreCase)
            && name.AsSpan(sidecarLength + 1, TemporaryIdLength).IndexOfAnyExcept(HexDigits) < 0;
    }

    private string[] PartsBelowWatched(string path)
    {
        var watched = Folders
            .Where(entry => Comparer.Equals(entry.Folder, path) || IsBelow(path, entry.Folder))
            .Select(entry => entry.Folder)
            .OrderByDescending(folder => folder.Length)
            .FirstOrDefault();
        var relative = watched is null ? path : path[watched.Length..];
        return relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries
        );
    }

    private static ImmutableArray<(string Folder, bool Recursive)> FoldersOf(
        string unitFolder,
        ImmutableArray<ProjectManifest> manifests
    )
    {
        var recursive = new List<string> { Trim(unitFolder) };
        var alone = new List<string>();
        foreach (var manifest in manifests)
        {
            var projectFolder = FolderOf(manifest.ProjectPath);
            recursive.Add(projectFolder);
            alone.AddRange(DotEnvSearch.FoldersOf(projectFolder));
            alone.AddRange(
                manifest.Files.Concat(manifest.SettingsFiles).Concat(manifest.CompileFiles).Select(FolderOf)
            );
        }

        alone.AddRange(DotEnvSearch.FoldersOf(unitFolder));

        // Sorted first, so that of two spellings in different case the same one stays whatever the order of the lists.
        var roots = new List<string>();
        foreach (var folder in recursive.Order(StringComparer.Ordinal).Where(folder => !IsAtOrBelowAny(folder, roots)))
        {
            roots.Add(folder);
        }

        var others = new List<string>();
        foreach (
            var folder in alone
                .Order(StringComparer.Ordinal)
                .Where(folder => !IsAtOrBelowAny(folder, roots) && !others.Contains(folder, Comparer))
        )
        {
            others.Add(folder);
        }

        return
        [
            .. roots
                .Select(folder => (Folder: folder, Recursive: true))
                .Concat(others.Select(folder => (Folder: folder, Recursive: false)))
                .OrderBy(entry => entry.Folder, StringComparer.Ordinal),
        ];
    }

    private static bool IsAtOrBelowAny(string path, List<string> folders) =>
        folders.Any(folder => Comparer.Equals(folder, path) || IsBelow(path, folder));

    private static string FolderOf(string file) => Trim(Path.GetDirectoryName(file) ?? file);

    private static string Trim(string folder) => Path.TrimEndingDirectorySeparator(folder);

    // True when the path is strictly below the folder.
    private static bool IsBelow(string path, string folder)
    {
        var prefix = Path.EndsInDirectorySeparator(folder) ? folder : folder + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && path.Length > prefix.Length;
    }

    private enum Side
    {
        Changed,
        Created,
        Removed,
    }
}
