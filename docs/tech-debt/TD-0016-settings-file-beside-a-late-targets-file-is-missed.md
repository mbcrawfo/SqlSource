# TD-0016 - The settings file beside a file that a late target adds is missed where the search runs early

## Problem

The build finds the settings files in the target `SqlSourceFindSettingsFiles` of [`build/SqlSource.targets`](../../src/SqlSource/build/SqlSource.targets), which searches the folders above every `.sql` item of `AdditionalFiles` and every `Compile` item and adds each `sqlsource.json` it finds.  In a project of the SDK it runs just before `CoreCompile`, from [`build/SqlSource.AfterSdk.targets`](../../src/SqlSource/build/SqlSource.AfterSdk.targets), after every other target that hooks `CoreCompile`.

A project that is not of the SDK does not import that file, and neither does one that sets `AfterMicrosoftNETSdkTargets` without keeping its value.  There the search hooks `GenerateMSBuildEditorConfigFileCore` instead, through `SqlSourceSettingsSearchHook`.  A target that hooks `GenerateMSBuildEditorConfigFileCore` and is declared after the package's targets, as one in `Directory.Build.targets` is, or one that hooks `CoreCompile`, runs after the search.  A `.sql` file that it adds is compiled, but the folders above it are not searched, so a `sqlsource.json` that no other item reaches, the one beside it for example, is not given to the compiler.  Nothing is reported: the file gets the settings of the files the search did find, or the defaults.

## Why it exists

The search has to run in a target, so that it sees a `.sql` file that a target adds, and the package cannot make its target the last one: MSBuild runs the targets that hook the same target in the order they are declared in, and NuGet imports the package's targets before `Directory.Build.targets`.  `SqlSource.AfterSdk.targets` gets round that for a project of the SDK, which imports `AfterMicrosoftNETSdkTargets` after every other target file; a project that is not of the SDK has no such list.

## Impact

Low.  It takes a project that is not of the SDK, or one that drops the package's file from `AfterMicrosoftNETSdkTargets`, with a target that adds `.sql` files during the build from a hook of the build, and a settings file that only those files reach.  A target that hooks `SqlSourceListFiles`, as the manual tells it to, is not affected.

## Proposed fix

None is known that runs last in a project that is not of the SDK.  Such a target hooks `SqlSourceListFiles`, which the search depends on, so that it runs first wherever it is declared; the comment of `SqlSourceFindSettingsFiles` says so, and the known issues of [`docs/manual/settings.md`](../manual/settings.md#known-issues) say so with a sample target.

## Trigger

A user reports that the settings of a folder are ignored for a `.sql` file that a target adds, in a project that is not of the SDK.
