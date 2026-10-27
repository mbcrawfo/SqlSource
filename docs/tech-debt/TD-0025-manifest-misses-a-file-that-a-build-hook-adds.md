# TD-0025 - The project manifest misses a `.sql` file that a target adds from a hook of the build

## Problem

The `sqlsource` tool learns what the compiler is given from the project manifest, which the target `SqlSourceWriteManifest` in [`build/SqlSource.targets`](../../src/SqlSource/build/SqlSource.targets) writes.  The tool runs that target by name, so nothing of a build runs before it but what it depends on: the package's own targets and, for a project of the SDK, `AddImplicitDefineConstants`.

A `.sql` file that a target of the project adds as an `AdditionalFiles` item is therefore in the manifest only when that target hooks `SqlSourceListFiles`.  One that hooks `BeforeBuild`, or any other target of a build, has not run.  The build compiles the file, and the tool does not know it: it describes none of its queries.  The generator needs a sidecar entry for each of them whose output is not `Sql`, so the build reports each as missing, `SQLSRC241`.

The same holds for a `Compile` item that a target adds: a `[SqlSourceGenerate]` in such a file is not read.  Every project of the SDK has such items, the files it writes under `obj` for the assembly's attributes and for the global usings.  None holds the attribute, but a global `using` alias that names it is in one of them, and the tool's reader of attributes, in sub-phase 2.4, does not see that alias.

`tests/SqlSource.Tool.Tests/Projects/ManifestTargetTests.cs` pins it with the fixture `BuildHookFile`, and `tools/check-package-install.sh` with the file that `tools/package-install/Directory.Build.targets` adds.

## Why it exists

Depending on `BeforeBuild` would run whatever a project hangs on it, a code generator or a package install, on every `sqlsource describe`.  The owner weighed that and rejected it.

## Impact

Low.  It takes a target that adds `.sql` files while the build runs.  The failure is loud: the build says which queries have no entry.

## Proposed fix

A target that adds a `.sql` file hooks `SqlSourceListFiles`, which runs it before the manifest is written as well as before the search for settings files; the comment at the top of `build/SqlSource.targets` says the same for a build.  The known issues of [`docs/manual/settings.md`](../manual/settings.md#known-issues) say so, and those of `docs/manual/tool.md` point there.  Phase 9's run inside a build does not have the limit: the build has run its hooks by then.

## Trigger

A user reports that `sqlsource describe` leaves out the queries of a file that their build adds.  Or phase 9, which may close it for a run inside a build.
