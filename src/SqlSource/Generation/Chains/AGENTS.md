# AGENTS.md - src/SqlSource/Generation/Chains

- **Never resolve a chain by a pass over the files.**  `SettingsFileSet.Of` computes the chain that starts at each file once, and `ChainOf` searches each folder above a path's for its nearest file with `SqlPath.IndexOf`, in place and in the order of `SqlPath.Comparer`, and allocates nothing, which `SettingsFileSetTests` holds.
- **A file's parent is searched for, never kept on a stack.**  `SqlPath.Comparer` sorts `app-tests` between `app` and `app/Queries`, since `-` sorts before the separator.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
