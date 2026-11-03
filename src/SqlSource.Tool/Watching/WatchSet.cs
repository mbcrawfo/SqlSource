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
/// It reads nothing but the <c>.git</c> markers that <see cref="DotEnvSearch.FoldersOf" /> looks for, whether the
/// folder of a project's assets file, and those above it, exist, and, outside a repository, where the nearest
/// <c>Directory.Build.props</c> above a project is.  A set is made from the projects of one evaluation,
/// so a session makes another when it evaluates again.  Every comparison of paths ignores case, as
/// <see cref="RunProjects.PathComparer" /> does.
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

    // The folders whose project.assets.json is ignored too: a restore writes it below an obj folder, wherever the
    // project puts that, and never below these.
    private static readonly string[] IgnoredAssetsFolders = ["bin", ".git"];

    // Any change to one of these asks for another evaluation, wherever it is.
    private const string BuildPropsFileName = "Directory.Build.props";

    private static readonly string[] EvaluationFileNames =
    [
        BuildPropsFileName,
        "Directory.Build.targets",
        "Directory.Packages.props",
    ];

    private static readonly string[] EvaluationExtensions = [".csproj", ".sln", ".slnx"];

    private static StringComparer Comparer => RunProjects.PathComparer;

    private readonly string _unitPath;
    private readonly ImmutableArray<string> _projectFolders;
    private readonly HashSet<string> _listed = [with(Comparer)];
    private readonly HashSet<string> _listedFolders = [with(Comparer)];
    private readonly HashSet<string> _settingsFolders = [with(Comparer)];
    private readonly HashSet<string> _envFolders = [with(Comparer)];

    private WatchSet(string unitPath, string unitFolder, FoundProjects found)
    {
        var manifests = found.Manifests;
        _unitPath = unitPath;
        _projectFolders = [.. manifests.Select(manifest => FolderOf(manifest.ProjectPath)).Distinct(Comparer)];
        foreach (var manifest in manifests)
        {
            _listed.UnionWith(manifest.Files);
            _listed.UnionWith(manifest.SettingsFiles);
            _listed.UnionWith(manifest.CompileFiles);
        }

        // A settings file applies to the files of its folder and of the folders below it, so a new one in the folder of
        // a listed file, or above it up to the root of its repository, may apply to that file.
        _listedFolders.UnionWith(_listed.Select(FolderOf));
        foreach (var folder in _listedFolders)
        {
            _settingsFolders.UnionWith(DotEnvSearch.FoldersOf(folder));
        }

        _envFolders.UnionWith(DotEnvSearch.FoldersOf(unitFolder));
        Folders = FoldersOf(unitFolder, found, _settingsFolders);
    }

    /// <summary>
    /// The folders to watch, sorted by ordinal comparison so that two sets compare: the unit's folder, the folder of
    /// each project, with or without a manifest, and the folder of each project's assets file, or the nearest that
    /// exists above it in the project's repository, with their folders below; and each other folder that holds a listed
    /// file, or may hold a settings file of one, a <c>.env</c> or a <c>Directory.*</c> file, or is above an assets
    /// file's folder in its repository, alone.  So each folder above one of these, up to the root of its repository,
    /// is watched too, and sees it deleted or made; outside a repository the folder above each is watched alone.  None
    /// is below another that is watched with its folders below, and none is there twice.  A folder may not exist, which
    /// the session skips.
    /// </summary>
    public ImmutableArray<(string Folder, bool Recursive)> Folders { get; }

    /// <summary>
    /// Makes the set of a unit: the solution or the project <paramref name="unitPath" /> in
    /// <paramref name="unitFolder" />, and what an evaluation of it found: the projects it runs, the manifests of those
    /// that use SqlSource, and the assets files of their restores.  A project without a manifest is watched too, so
    /// that its restore, or the package added to it, starts a pass.
    /// </summary>
    public static WatchSet Of(string unitPath, string unitFolder, FoundProjects found) =>
        new(unitPath, unitFolder, found);

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

        if (side == Side.Created && IsNewContent(path, name, isFolder))
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
        || Comparer.Equals(name, AssetsFileName);

    // A file or a folder that a project may list now, or a settings file that may apply to a listed file now: a .sql
    // or .cs file, a settings file, or a folder the globs may reach, below a project's folder; a .sql or .cs file or a
    // folder in a folder that holds a listed file, as a glob of another folder lists; and a settings file in such a
    // folder or above it, up to the root of its repository.
    private bool IsNewContent(string path, string name, bool isFolder)
    {
        var settings = Comparer.Equals(name, SettingsFileName);
        var content =
            isFolder
            || name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
        if (!settings && !content)
        {
            return false;
        }

        if (_projectFolders.Any(project => IsBelow(path, project)))
        {
            return true;
        }

        var folder = FolderOf(path);
        return _listedFolders.Contains(folder) || (settings && !isFolder && _settingsFolders.Contains(folder));
    }

    // Sidecars and the temporary files the sidecar store makes are the tool's own writes.  Everything in bin, obj and
    // .git is the build's, or the repository's, except the one file that a restore writes, at any depth below obj, as
    // an artifacts layout or a custom MSBuildProjectExtensionsPath puts it.  Only the parts of the path below the
    // nearest watched folder count, so that a folder named bin above the project is not one.
    private bool IsIgnored(string path)
    {
        var name = Path.GetFileName(path);
        if (name.EndsWith(SidecarSuffix, StringComparison.OrdinalIgnoreCase) || IsTemporary(name))
        {
            return true;
        }

        var ignored = Comparer.Equals(name, AssetsFileName) ? IgnoredAssetsFolders : IgnoredFolders;
        return PartsBelowWatched(path).Any(part => ignored.Contains(part, Comparer));
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
        FoundProjects found,
        IEnumerable<string> settingsFolders
    )
    {
        var manifests = found.Manifests;
        var recursive = new List<string> { Trim(unitFolder) };
        var alone = new List<string>();
        var projectFolders = found
            .Projects.Concat(manifests.Select(manifest => manifest.ProjectPath))
            .Select(FolderOf)
            .Distinct(Comparer);
        foreach (var projectFolder in projectFolders)
        {
            recursive.Add(projectFolder);
            alone.AddRange(DotEnvSearch.FoldersOf(projectFolder));
        }

        foreach (var (project, assetsFile) in found.AssetsFiles)
        {
            if (AssetsFolderOf(project, assetsFile) is { } assetsFolder)
            {
                // With the folders above it, so that its deletion is seen.
                recursive.Add(assetsFolder);
                alone.AddRange(DotEnvSearch.FoldersOf(assetsFolder));
            }
        }

        // The folder of each listed file, and each folder above it that may hold a settings file of it.
        alone.AddRange(settingsFolders);
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

        // Outside a repository no folder above one of these is in the list, so the folder above each is watched alone
        // too, so that its deletion and its making again are seen: on Linux a watch says nothing when its folder goes.
        others.AddRange([
            .. roots
                .Concat(others)
                .Where(folder => !DotEnvSearch.IsInRepository(folder))
                .Select(Path.GetDirectoryName)
                .OfType<string>()
                .Select(Trim)
                .Where(parent => !IsAtOrBelowAny(parent, roots) && !others.Contains(parent, Comparer))
                .Order(StringComparer.Ordinal)
                .Distinct(Comparer),
        ]);

        return
        [
            .. roots
                .Select(folder => (Folder: folder, Recursive: true))
                .Concat(others.Select(folder => (Folder: folder, Recursive: false)))
                .OrderBy(entry => entry.Folder, StringComparer.Ordinal),
        ];
    }

    // The folder that a restore writes the project's assets file into, which an artifacts layout may put outside every
    // folder of the unit.  Before the restore makes it, the nearest folder above it that exists, at or below the root
    // of the project's repository, so that the restore is seen.  Outside a repository the bound is the folder of the
    // nearest Directory.Build.props above the project, where UseArtifactsOutput puts its artifacts folder, or the
    // project's folder when there is none.  Null when there is no such folder.
    private static string? AssetsFolderOf(string project, string assetsFile)
    {
        var folder = FolderOf(assetsFile);
        if (System.IO.Directory.Exists(folder))
        {
            return folder;
        }

        var projectFolder = FolderOf(project);
        var root = DotEnvSearch.IsInRepository(projectFolder)
            ? Trim(DotEnvSearch.FoldersOf(projectFolder)[^1])
            : BuildPropsFolderOf(projectFolder) ?? projectFolder;
        var above = Path.GetDirectoryName(folder);
        while (above is not null && (Comparer.Equals(Trim(above), root) || IsBelow(above, root)))
        {
            if (System.IO.Directory.Exists(above))
            {
                return Trim(above);
            }

            above = Path.GetDirectoryName(above);
        }

        return null;
    }

    // The nearest folder at or above the project's that holds a Directory.Build.props, as MSBuild finds it.
    private static string? BuildPropsFolderOf(string projectFolder)
    {
        for (var folder = projectFolder; folder is not null; folder = Path.GetDirectoryName(folder))
        {
            if (System.IO.File.Exists(Path.Combine(folder, BuildPropsFileName)))
            {
                return Trim(folder);
            }
        }

        return null;
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
