# TD-0079: a build folder whose path holds `#` or `;` reaches the settings files above the project

## Problem

The generator leaves a C# file under the project's intermediate or output folder out of the chains that decide which settings files are checked, so that the SDK's own files in `obj` never reach a settings file above the project's `"root": true` ([which files are checked](../architecture/generator.md#which-files-are-checked)).  It learns the folders from `SqlSourceIntermediateFolder` and `SqlSourceOutputFolder`, which `SqlSourceFindBuildFolders` in [`build/SqlSource.targets`](../../src/SqlSource/build/SqlSource.targets) sets.  The compiler reads a value up to its first `#` or `;`, so the target gives a folder whose full path holds either as empty, and [`BuildFolders.Of`](../../src/SqlSource/Generation/Chains/BuildFolders.cs) takes no value that does not end with a separator.  Such a project's build files then reach the chains as its own files do.

## Why it exists

A cut value names a folder above the real one or beside it, and leaving out the paths under it would leave out files of the project, so that a settings file they reach is never checked.  Giving no folder is the safe side: it checks more, as the build did before the folders were given.  Passing the path through the compiler intact needs an encoding that MSBuild's own escaping of `%` makes fragile, for a path that few projects have.

## Impact

Only a project whose `obj` or `bin` is outside its folder, as with `UseArtifactsOutput`, and whose full path holds `#` or `;`, such as a repository cloned into `C#Projects/`: a broken `sqlsource.json` above the project's root file, at the repository's root, fails its build with `SQLSRC015` to `SQLSRC020`, which `"root": true` is meant to prevent.  A project whose `obj` is inside its folder is not affected, since its build files share its own chain.

## Proposed fix

Give the compiler each folder in a form it does not cut: the target replaces `%`, `#` and `;` with an escape the generator decodes, written so that MSBuild's unescaping of `%XX` in a project file leaves it as written (`%2523` for `%23`), and `BuildFolders.Of` decodes it before it normalises.  Check it with a real build in a folder named with each character, and keep the empty value for a path the decoding cannot read.

## Trigger

A report of a settings file above a root that fails a build in such a folder, or the tool's check of settings files in layer 4 of the settings files, which reads the folders from the manifest and should apply the same rule.
