# TD-0016 - The dialect of a file that a late target adds is lost

## Problem

The compiler reads the `SqlSourceDialect` metadata of a `.sql` file from the items of `SqlSourceDialectFile`.  The target `SqlSourceCollectDialectFiles` in [`build/SqlSource.targets`](../../src/SqlSource/build/SqlSource.targets) fills that item type from the `AdditionalFiles` items that have a dialect, before `GenerateMSBuildEditorConfigFileCore`, the target of the SDK that writes the file the compiler reads.

A target that hooks `GenerateMSBuildEditorConfigFileCore` itself, and is declared after the package's targets, as one in `Directory.Build.targets` is, runs after the collecting.  A `.sql` file that it adds as an `AdditionalFiles` item is compiled, and its `SqlSourceDialect` metadata does not reach the compiler.  Nothing is reported: the file is read by the project's dialect or as `ansi`.

Before the metadata was collected, such an item kept a dialect that was written on one line, and lost only one written over several lines.  So this made a form that worked stop working.

## Why it exists

Listing the metadata for `AdditionalFiles` itself gave every `.sql` file of a project a section in the file the compiler reads, which cost a project with 5,000 files about 0.4 s on each build.  Collecting the files that have a dialect removes that cost, and it has to happen in a target.  MSBuild runs the targets that hook the same target in the order they are declared in, and the SDK imports the targets of a package before `Directory.Build.targets`, so the package cannot make its target the last one.

## Impact

Low.  It takes a target that adds `.sql` files during the build, gives them a dialect as metadata, hooks that one target of the SDK, and is declared after the package's.  A target that hooks anything earlier, `BeforeBuild` for example, is not affected.

## Proposed fix

None is known that runs last.  Such a target hooks `SqlSourceTrimDialectOfFiles` instead, which runs it before the trim and the collecting wherever it is declared; the comment at the top of `build/SqlSource.targets` says so.  If it is reported, say the same in the "Dialect" section of `README.md`.

## Trigger

A user reports that the dialect of a `.sql` file that a target adds is ignored.
