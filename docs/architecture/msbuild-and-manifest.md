# The MSBuild files and the manifest

How [`build/SqlSource.props`](../../src/SqlSource/build/SqlSource.props) and [`build/SqlSource.targets`](../../src/SqlSource/build/SqlSource.targets) hand a project's `.sql` files, their sidecars and its settings files to the compiler, and how the target `SqlSourceWriteManifest` writes the [manifest](README.md#manifest) for the `sqlsource` tool.  What a user writes in MSBuild is in the manual, [which `.sql` files are included](../manual/settings.md#included-files), and what a user writes in a settings file is under [settings files](../manual/settings.md#settings-files).  The terms are the [glossary's](README.md#glossary).

## What the props and targets do

NuGet imports the props into each project that references the package before the project's own content, and the targets after it.  The props have the SDK import a third file, [`build/SqlSource.AfterSdk.targets`](../../src/SqlSource/build/SqlSource.AfterSdk.targets), after all of its own targets: see [finding the settings files](#finding-the-settings-files).

The props do four things.

- They set `SqlSourceImported` to `true`, with no condition.  The tool asks a project for this property, and for nothing else, to tell whether it uses SqlSource.  It is in the props because they reach a project whether the package came from NuGet or its files are imported by path.  A project that was never restored has no props of a package, and so no marker.
- They add `SqlSource.AfterSdk.targets` to `AfterMicrosoftNETSdkTargets`, the list of files the SDK imports last, keeping the list's value.
- They add every `.sql` file under the project's folder as an `AdditionalFiles` item, the only way a source generator sees a file, unless the project sets `SqlSourceIncludeFiles` to `false`.  The item comes before the project's content, so a project can remove files from it.  The same condition and the same excludes give the compiler every `.sql.json` file too, a `.sql` file's sidecar, which the generator reads; a project that sets `SqlSourceIncludeFiles` to `false` lists its sidecars itself.  Items are evaluated after every property, so `DefaultItemExcludes` already holds the output and intermediate folders, whatever the project sets them to.
- They list `SqlSourceBuildFolders` as a `CompilerVisibleProperty` item, and nothing else.  A generator sees only the properties and item metadata that are listed.  `SqlSourceBuildFolders` is no setting: the targets set it, as [the build's folders](#the-builds-folders) says.  It is not under the condition of `SqlSourceIncludeFiles`, since a project that lists its own `.sql` files still needs it.

No setting is a property or item metadata of the build.  The settings are in settings files, which the targets give the compiler as `AdditionalFiles` items and the generator reads as JSON, so no value of a setting passes through the file the compiler reads its properties from.  Nothing is trimmed, and no value is cut at a `;` or a `#`.

The targets file holds five targets.  `SqlSourceFindBuildFolders` hooks `GenerateMSBuildEditorConfigFileCore`, the target of the SDK that writes the file the compiler reads its properties from.  The search for settings files and the tracking of additional files run just before `CoreCompile`, from `SqlSourceSettingsFilesBeforeCompile` of `SqlSource.AfterSdk.targets`, or hook `GenerateMSBuildEditorConfigFileCore` where that file is not imported.  `SqlSourceListFiles` and `SqlSourceWriteManifest` hook nothing: the first is the hook for a target of a project that adds a `.sql` file, and the tool runs the second by name.  The file also holds the `PropertyGroup` that sets what the search hooks and what the manifest's target depends on, an `ItemGroup` that nests each sidecar under its `.sql` file and keeps it out of the output and publish folders, and an `ItemGroup` that keeps the settings files out of them too.  An arrow means "runs before":

```mermaid
flowchart LR
    folders["SqlSourceFindBuildFolders"] --> sdk["GenerateMSBuildEditorConfigFileCore (SDK)"]
    list["SqlSourceListFiles"] --> find["SqlSourceFindSettingsFiles"]
    find --> track["SqlSourceTrackAdditionalFiles"]
    track --> late["SqlSourceSettingsFilesBeforeCompile (SqlSource.AfterSdk.targets)"]
    late --> compile["CoreCompile (SDK)"]
    track -.->|"without SqlSource.AfterSdk.targets"| sdk
    list --> manifest["SqlSourceWriteManifest (run by the tool)"]
    find --> manifest
    folders --> manifest
    defines["AddImplicitDefineConstants (SDK)"] -.->|"with a TargetFramework"| manifest
```

The hook and the search are described under [finding the settings files](#finding-the-settings-files), the folders under [the build's folders](#the-builds-folders), the tracking under [tracking additional files](#tracking-additional-files), and the manifest target under [the manifest](#the-manifest).

### Nesting a sidecar

The `ItemGroup` sets `DependentUpon="%(Filename)"` on every `**/*.sql.json` item, with `Update`, so that an IDE can show `Users.sql.json` under `Users.sql`.  `Update` changes the items that are listed and adds none.  It is on `AdditionalFiles`, on `None`, the item type under which the SDK lists every file of the project, and on `Content`, under which the Web SDK lists a `.json` file in place of `None`, since an IDE may nest on any of them.  The IDEs nest from evaluated items, so the group is outside every target.  It is in the targets and not in the props, so that it also reaches a sidecar that a project lists itself when `SqlSourceIncludeFiles` is `false`.  `SqlSourceWriteManifest` lists no sidecar, so the manifest does not change.

The `Content` item also sets `CopyToOutputDirectory` and `CopyToPublishDirectory` to `Never`.  A sidecar is an input of the generator and nothing else, and the Web SDK copies its `Content`, a `.json` file among it, to the output folder and to what `dotnet publish` writes.  The targets are imported after the project's content, so `Never` wins over the Web SDK and over the project file; a web project that wants its sidecars copied sets the metadata again in its `Directory.Build.targets`, which the SDK imports after the targets of a package.  The `None` item keeps the project's copy metadata: the SDK copies a `None` file only when the project asks, as `tests/SqlSource.Tests` does for the examples of the sidecar format.  A `.sql` file is `None` in the SDK and in the Web SDK, so it needs nothing.  The last stage of [`check-package-install.sh`](../../tools/README.md#check-package-installsh) publishes a web project and fails when either file is copied.

### Finding the settings files

A settings file, `sqlsource.json`, applies to its folder and every folder below it, and a generator cannot read the disk, so every one that can apply to a `.sql` file or to a type must be an `AdditionalFiles` item.  `SqlSourceFindSettingsFiles` finds them the way the SDK finds `.editorconfig` files in `Roslyn/Microsoft.Managed.Core.targets`: every folder above a `.sql` item of `AdditionalFiles` or a `Compile` item, to the top of the drive, by `GetPathsOfAllDirectoriesAbove()`, each made a full path and distinct, combined with `sqlsource.json`, and kept where the file exists.  The step through `FullPath` and `Distinct` is the SDK's too: `GetPathsOfAllDirectoriesAbove()` can give one folder twice for a file linked with a relative path (msbuild issue 4392).  A folder that holds no `.sql` or C# file, and has none below it, is not searched, so a settings file there applies to nothing.  The search is not under `SqlSourceIncludeFiles`: a project that lists its own `.sql` files still gets its settings.  `Exists()` asks the file system, so where names are compared ignoring case, as on macOS and Windows, a `SqlSource.json` is found under the path `sqlsource.json`, as the SDK's search finds an `.EditorConfig`.

The target adds each file it finds as an `AdditionalFiles` item, unless the project lists it already: the comparison is of full paths, since a project may list the file by a relative one, and the compiler is given none twice.  Each item it adds is `Visible="false"`.  An IDE lists the items of a project as evaluated, never one a target adds, so the metadata is for one that might; a settings file in the project's folder is listed anyway, as `None`, or as `Content` in the Web SDK.

It runs in a target, and not where the targets are imported, so that a `.sql` file a target adds is searched above too, and it runs as late as it can: just before `CoreCompile`, after every target of the project that hooks it.  MSBuild runs the targets that hook one target in the order they are declared, and NuGet imports the package's targets before `Directory.Build.targets`, so a target there that hooks `CoreCompile` to add a `.sql` file would run after a search declared in `SqlSource.targets`.  So the props add `SqlSource.AfterSdk.targets` to `AfterMicrosoftNETSdkTargets`, which the SDK imports after all of its targets and after `Directory.Build.targets`, and `SqlSourceSettingsFilesBeforeCompile` there hooks `CoreCompile` and depends on the search and on the tracking.  That file also empties `SqlSourceSettingsSearchHook`, the property the two targets hook in `SqlSource.targets`, which is `GenerateMSBuildEditorConfigFileCore` otherwise; so they run once, from there.  Where the file is not imported, in a project that is not of the SDK or one that sets `AfterMicrosoftNETSdkTargets` without keeping its value, they run before `GenerateMSBuildEditorConfigFileCore`, as `SqlSourceFindBuildFolders` does, and a `.sql` file that a later target adds is not searched above: [TD-0016](../tech-debt/TD-0016-settings-file-beside-a-late-targets-file-is-missed.md).

The search depends on `SqlSourceListFiles`, the target that the manual tells a target that adds a `.sql` file to hook with `BeforeTargets`, so that such a target runs first wherever it is declared, before the manifest too.  `SqlSourceListFiles` is empty and hooks nothing, so it runs only as what the search and the manifest's target depend on, and MSBuild passes over a `BeforeTargets` that names a target that does not exist, so its name is a contract with the users that the manual gives it to.  A design-time build of an IDE runs the search, since that build runs `Compile` with the compiler skipped, and the compiler's arguments then hold every file found, the one beside a file that a target of `Directory.Build.targets` adds from a hook of `CoreCompile` included: `SettingsFileSearchTests` in `tests/SqlSource.Tool.Tests/Projects/` holds it, with and without `SqlSource.AfterSdk.targets`.

`SqlSourceTrackAdditionalFiles` depends on it, so that a settings file added, deleted or moved changes the hash, and the next build compiles again.  It runs where the search runs, so a `.sql` file that a target adds before the compiler is in the hash too.

### Keeping the settings files out of the output

The last `ItemGroup` at the root of the targets sets `CopyToOutputDirectory` and `CopyToPublishDirectory` to `Never` on every `Content` item named `sqlsource.json`, as the nesting group does for a sidecar, since the Web SDK lists a `.json` file as `Content` and copies it.  It is in the targets and not in the props: NuGet imports a package's props from `Microsoft.Common.props`, before the Web SDK adds its `Content` items, and `Update` changes only the items already listed.  The last stage of [`check-package-install.sh`](../../tools/README.md#check-package-installsh) publishes a web project that has one, and fails when it is copied.

### The build's folders

`SqlSourceFindBuildFolders` sets `SqlSourceBuildFolders` to the folders the build writes C# files and its outputs into, and the NuGet package folders: `BaseIntermediateOutputPath`, `IntermediateOutputPath`, `BaseOutputPath`, `OutDir`, `CompilerGeneratedFilesOutputPath` when `EmitCompilerGeneratedFiles` is `true`, `NuGetPackageRoot`, and each other folder of `NuGetPackageFolders`, in that order, each after a `|`, made against `MSBuildProjectDirectory` by `$([MSBuild]::NormalizeDirectory(...))`, which ends it with a separator.  A default Debug build of a restored project gives `|/repo/App/obj/|/repo/App/obj/Debug/net10.0/|/repo/App/bin/|/repo/App/bin/Debug/net10.0/|/home/me/.nuget/packages/`.  The props show the property to the compiler, and the generator leaves a C# file under any of the folders out of the chains that decide which settings files are checked, so that the SDK's own files in `obj`, the files of the generators and the C# files that packages add reach no settings file above the project's root file wherever they are ([which files are checked](generator.md#which-files-are-checked)).  A folder that has no value is left out, since `NormalizeDirectory` would make it the project's own folder.  The target first empties the property and then sets it, so it holds the build's folders whatever the project wrote: it is no setting.  It runs in a target, before `GenerateMSBuildEditorConfigFileCore`, since every property of the SDK, of the artifacts layout and of the restore is set by then, while `Directory.Build.targets`, which is imported after the package's targets, could still change one where they are imported.

The package folders are where NuGet restored the packages, and so where a package that ships C# source as `contentFiles`, as Polyfill does, has the `Compile` items it adds: under `~/.nuget/packages/`, whose chain never meets the project's root file, so a broken `sqlsource.json` in the home folder would fail the build and `sqlsource describe`.  Both properties come from the file `obj/<project>.nuget.g.props` that a restore writes and the project imports, so neither has a value before a restore.  `NuGetPackageRoot` is the global packages folder, `RestorePackagesPath` or `NUGET_PACKAGES`; `NuGetPackageFolders` is it and then each fallback folder, `RestoreFallbackFolders` and those of `NuGet.Config`, parted by `;`.  The restore writes both, and no targets file of the SDK sets either.  The target splits that list as the item `SqlSourcePackageFolder`, leaving out `NuGetPackageRoot` by `Exclude`, so that the global packages folder is listed once where the two are spelled alike, and escapes each folder in the item's metadata `SqlSourceFolder`, which batches over the items, before it joins them with `|` by a transform: a property batched over the items would keep only the last.  The global packages folder is added from `NuGetPackageRoot` itself, so it is listed whatever its name holds.  A fallback folder is not: one whose name holds a `;` cannot be told apart in that list, as NuGet itself cannot, and one whose name holds a `*` or a `?` is read by the item's `Include` as a pattern, so either is left out of `SqlSourceBuildFolders`, and the files of its packages reach the settings files above it, as they did before the package folders were listed.

The compiler reads a value up to its first `#` or `;`, and `|` parts the folders, so the target escapes each folder's `%`, `|`, `#` and `;` as `%25`, `%7C`, `%23` and `%3B`, `%` first, with `Replace`, and `BuildFolders.Of` decodes them.  MSBuild reads `%XX` in a project file as the character it stands for, so each escape is written with its `%` as `%25`, `%2523` for `%23`: a real build of a project under `/repo/hash#repo` with `UseArtifactsOutput` gives `|/repo/hash%23repo/artifacts/obj/App/|/repo/hash%23repo/artifacts/obj/App/debug/|...`.  The manifest carries the property, escaped as it is: [the format](#the-format) says what the tool reads it for.

### Tracking additional files

`SqlSourceTrackAdditionalFiles` makes the build after a `.sql` file or a settings file is deleted, renamed or moved compile again.  A file that is gone has no timestamp to compare, and the SDK's own record of what the compiler was given does not hold `AdditionalFiles`.  So the target writes a hash of the list of `AdditionalFiles` to a file under the intermediate folder, and names that file as a `CustomAdditionalCompileInputs` item, the item the SDK reads for more inputs of the compiler.  It writes the file only when the hash changes, so a build with the same files does not compile for it.  It depends on [the search](#finding-the-settings-files), so that the list holds the settings files, and runs where the search runs: just before `CoreCompile` in a project of the SDK.

## The manifest

`SqlSourceWriteManifest` writes what the compiler is given, for the `sqlsource` tool, which runs it by name with `dotnet msbuild` and names the file to write with `SqlSourceManifestFile`.  How the tool runs it and uses what it reads is on [the tool's page](tool.md#evaluating-a-project).

The target hooks nothing: it has no `BeforeTargets`, no `AfterTargets` and no condition.  A hook would make every build write the manifest, and a condition would make a run of the tool find no file.  So nothing of a build runs before it but what it depends on, which is the property `SqlSourceManifestDependsOn`.  That property is set in a `PropertyGroup` at the root of the targets file, because a target's `DependsOnTargets` is read before the target runs; the group also sets `SqlSourceSettingsSearchHook`.  The only elements of the file that are not targets are that group, the [nesting of the sidecars](#nesting-a-sidecar) and the [group of the settings files](#keeping-the-settings-files-out-of-the-output), and the one condition outside a target is on the second of that `PropertyGroup`'s two `SqlSourceManifestDependsOn` elements, the one that adds `AddImplicitDefineConstants`.

- `SqlSourceListFiles` comes first, so that a target of the project that hooks it to add a `.sql` file runs before the rest.
- [The search](#finding-the-settings-files) adds the settings files to the `AdditionalFiles` items.
- [`SqlSourceFindBuildFolders`](#the-builds-folders) sets `SqlSourceBuildFolders`, which only that target gives a value, so that the manifest holds the folders.
- `AddImplicitDefineConstants`, when the project has a `TargetFramework`, adds the constants of the framework, `NET10_0` and `NET8_0_OR_GREATER` among them, which the SDK otherwise adds only on the way to a compile.  A project that is not of the SDK has no `TargetFramework` and no such target.

Without `SqlSourceManifestFile` the target writes to `$(IntermediateOutputPath)$(MSBuildProjectFile).SqlSource.manifest`, and adds that file to `FileWrites`, which tells `dotnet clean` of it only once a build runs the target.  A file the tool named is the tool's to delete.  Either way the file is written only when its text changes.

### The format

The manifest's format is the contract between the package and the tool, as the sidecar's is between the tool and the generator.  A project's package may be of another version than the tool that reads its manifest.  The decisions behind version 1 are in the design of [sub-phase 2.3](../superpowers/specs/2026-10-09-query-generation-phase-2-3-project-manifest-design.md).

The file is lines of `key=value`, and the key ends at the first `=`.  The target writes them in this order:

1. `SqlSourceManifest=2`, the version of the format.
2. `Project=`, the full path of the project file.
3. `TargetFramework=`, `LangVersion=` and `DefineConstants=`, three properties of the SDK.
4. `SqlSourceBuildFolders=`, the value of [the build's folders](#the-builds-folders), the NuGet package folders among them, as the target sets it, with each folder's `%`, `|`, `#` and `;` escaped.  It is no setting: the tool reads it, with `BuildFolders.Of`, only to leave out of the reach of a settings file the compile paths under those folders, as the generator does.
5. `File=` and the full path of each `AdditionalFiles` item whose extension is `.sql`, compared ignoring case, in the order of the items.
6. `Settings=` and the full path of each `AdditionalFiles` item named `sqlsource.json`, compared ignoring case, once each: those the search added and any the project lists itself, so that the tool reads exactly the settings files the compiler is given.
7. `Compile=` and the full path of each `Compile` item.

No line holds a setting: the tool reads the settings from the files of the `Settings` lines, as the generator does.  Version 1 had a `Property.<Name>=` line for each setting of the package and `File.<Name>=` lines for the item metadata of each `.sql` file.  Version 2 dropped them, and so the version rose: this tool would read a manifest of version 1, whose settings are on those lines, as setting nothing, without a word.

Two things keep each value on its own line:

- A property goes through `$([MSBuild]::Escape(...))`, or MSBuild would make an item of each part of a value that holds `;`, as `DefineConstants` does.  `WriteLinesToFile` undoes the escape, so the line of the build's folders holds the target's own escapes, `%23` for a `#`, as the compiler is given them.
- A property of the SDK is first put on one line with `Regex.Replace(value, '\s+', ' ').Trim()`.  A project may write its constants one on each line, and a line break inside a value would end its line of the file.

A path comes from an item's `FullPath`, and MSBuild does not split it.  [`tests/SqlSource.Tool.Tests/Projects/ManifestTargetTests.cs`](../../tests/SqlSource.Tool.Tests/Projects/ManifestTargetTests.cs) runs the target with the real `dotnet msbuild` on the projects of [`tests/SqlSource.Tool.Tests/Fixtures/Projects/`](../../tests/SqlSource.Tool.Tests/Fixtures/Projects), among them `OddPaths`, whose paths hold `;`, `=`, `%`, a quote, a space and a letter outside ASCII.  [`BuildFileTests`](../../tests/SqlSource.Tests/Package/BuildFileTests.cs) compares the lines the target writes, and their order, with its own list.  The manifest is what the compiler is given, whole: a settings file that the compiler is given and the manifest does not list would be read by the generator and not by the tool.

### Reading it

[`ManifestReader.Read`](../../src/SqlSource.Tool/Projects/ManifestReader.cs) gives a [`ProjectManifest`](../../src/SqlSource.Tool/Projects/ProjectManifest.cs), or the reason the text is not a manifest.  A `ProjectManifest` holds the project's path in `ProjectPath`, `TargetFramework`, `LangVersion` and `DefineConstants`, the value of the build's folders as it is in `BuildFolders`, which `BuildFolders.Of` decodes, and the paths of the `File`, `Settings` and `Compile` lines in `Files`, `SettingsFiles` and `CompileFiles`.

- The first line must be `SqlSourceManifest=2`.  A manifest of another version is not read at all, and the reason says that the package and the tool are out of step; the tool reports it as [SQLSRC206](../manual/diagnostics.md#sqlsrc206).  A byte order mark before it is passed over, and a line may end with `\r\n` as well as `\n`.
- A key the reader does not know is passed over, and so is an empty line: both are how a later package adds to the format.  A `Property.` or `File.<Name>` line, which no package of version 2 writes, is such a key.
- A value is the rest of the line as it is.  A line that is missing leaves its value empty.
- `DefineConstants` is split at `;`, `,`, a space and a tab, which is where the compiler separates constants.
- The reader keeps the `File`, `Settings` and `Compile` lines in their order, which is the order of the items.  The tool relies on it for a file listed twice: [`Planning/ListedFiles.cs`](../../src/SqlSource.Tool/Planning/ListedFiles.cs) keeps a `.sql` file once, compared ignoring case, with the spelling of its first line, and `SettingsFileSet.Of` keeps the first settings file of a folder, as the generator does.
- An empty text, a line without `=`, and a manifest that names no project are not manifests.

So an addition to the format keeps its version, as `Settings` did: a new key is passed over by a tool that does not know it.  A change to what an existing key means, or to how a line is read, would be misread by such a tool without a word, so it raises the version, as version 2 did, and then a reader of another version reads nothing and says why.

## Known limits

- **The manifest misses a `.sql` file or a `Compile` item that a hook of the build adds**, and a settings file that only such a file reaches, since the tool runs the target by name and no build has run its hooks.  A target that hooks `SqlSourceListFiles` is the exception.  [TD-0025](../tech-debt/TD-0025-manifest-misses-a-file-that-a-build-hook-adds.md).
- **A project with several target frameworks has the first one's manifest alone**, because the tool passes that framework as `TargetFramework`.  [TD-0026](../tech-debt/TD-0026-manifest-of-a-multi-targeted-project-is-the-first-frameworks.md).
- **The target depends on `AddImplicitDefineConstants`, a target of the SDK, by name**, which is no contract of the SDK.  [TD-0027](../tech-debt/TD-0027-manifest-target-depends-on-a-target-of-the-sdk.md).

## Not built yet

### Build integration

> **Not built yet.**  How a target of the build runs the tool before the compile in the online mode.  Delivered by phase 9 of the [query generation epic](../superpowers/specs/2026-10-07-query-generation-epic-design.md).
