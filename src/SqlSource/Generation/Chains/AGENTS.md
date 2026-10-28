# AGENTS.md - src/SqlSource/Generation/Chains

The settings files read, with the problems each side reports, and the chain of each folder.  See [settings files](../../../../docs/architecture/generator.md#settings-files).

- **A chain is built only here, for the generator and the `sqlsource` tool alike**: a file is read by `ListedSettingsFile.Read` and the chains by `SettingsFileSet.Of`.  A second builder would let a build and the tool give one folder different settings.
- **Never resolve a chain by a pass over the files.**  `SettingsFileSet.Of` computes the chain that starts at each file once, and `ChainOf` searches each folder above a path's for its nearest file with `SqlPath.IndexOf`, in place and in the order of `SqlPath.Comparer`, and allocates nothing, which `SettingsFileSetTests` holds.
- **A file's parent is searched for, never kept on a stack.**  `SqlPath.Comparer` sorts `app-tests` between `app` and `app/Queries`, since `-` sorts before the separator.
- **What a side reports is `SettingsFileSides.Reports` alone**: both report a problem of the file as a file; the build reports a value of any setting but `database`, the tool a value of `dialect`, `output` or `database`.

## The build's folders

- **Which compile paths reach nothing is `BuildFolders.ReachingPaths` alone**, for the generator and, from a manifest, the tool: one that is not rooted, or that `BuildFolders.Contains` puts under a folder the build writes C# files or its outputs into or a NuGet package folder.  The `BuildFolders` step reads those folders from `SqlSourceBuildFolders`.  See [which files are checked](../../../../docs/architecture/generator.md#which-files-are-checked).
- **A folder is an entry of the `|`-parted `SqlSourceBuildFolders` that is rooted and ends with a separator**, with the `%25`, `%7C`, `%23` and `%3B` that `build/SqlSource.targets` escapes decoded in one pass, compared segment by segment through `SqlPath.IsInFolder`, so that a value cut at a `#` or a `;` never leaves out a path the real folder does not hold.
- **The escaping in the target and the decoding here change together**, and a folder the build writes C# files into, or a NuGet package folder, that the target leaves out lets its files reach a file above the root.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
