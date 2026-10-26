# Settings files, layer 2: the build files deliver the files - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The package's build files find every `sqlsource.json` above every `.sql` and C# file of a project and give each to the compiler as an `AdditionalFiles` item, keep them out of the output and publish folders, and list them in the manifest as `Settings=` lines that the tool's `ManifestReader` reads.

**Architecture:** A new target, `SqlSourceFindSettingsFiles`, runs before `GenerateMSBuildEditorConfigFileCore` and before the manifest, and searches the way the SDK searches for `.editorconfig` files.  The manifest lists every `AdditionalFiles` item named `sqlsource.json`, so it holds exactly what the compiler is given.  Purely additive: every MSBuild setting still flows, and the manifest stays version 1.

**Tech Stack:** MSBuild (`SqlSource.targets`), C# (`SqlSource.Tool`), xUnit v3 with Shouldly, the real `dotnet msbuild` on fixture projects.

**Spec:** [`docs/superpowers/specs/2026-10-25-settings-files-design.md`](../specs/2026-10-25-settings-files-design.md), "The layers" item 2, with "Finding the files", "What changes in the build files" and "The manifest".

## Global Constraints

- Every MSBuild property the build files set, read or show to the compiler starts with `SqlSource` (AGENTS.md core rule 6); an item type of the package does too, as `Targets_ManifestTarget_MakesOnlyItemTypesThatStartWithSqlSource` holds for the manifest.
- The file name is `sqlsource.json`, lower case.
- The manifest stays `SqlSourceManifest=1`: a new key is an addition (src/SqlSource/AGENTS.md, the format).
- `Settings=` lines come after the `File=` lines and before the `Compile=` lines, one per file, with its full path.
- Item metadata is never the argument of a property function (`Targets_ItemMetadata_IsNeverTheArgumentOfAPropertyFunction`).
- No fixture `sqlsource.json` sits above a project the solution builds with the package; the repository's root file is layer 5's.
- Every commit passes `./pre-commit-validation.sh`; run `./format.sh` first.

## Where the code differs from the spec (said in the PR)

- **The `Content` exclusion is in the targets, not the props.**  NuGet imports a package's props inside `Microsoft.Common.props`, before the Web SDK adds its `Content` items, so an `Update` there changes nothing (checked with a Web SDK project and `Directory.Build.props`, which is imported at the same point).  The sidecars' exclusion is in the targets for the same reason.
- **`Visible="false"` is on every settings file the target adds, not only those outside the project's folder.**  An IDE lists a project's evaluated items, never one a target adds, so the metadata only matters to an IDE that might; a settings file inside the folder is listed anyway, as `None` or `Content`.  Splitting by folder would need a path comparison per file through a property, since item metadata is never the argument of a property function.
- **The manifest lists every `AdditionalFiles` item named `sqlsource.json`**, not only the ones the search found, so that a file the project lists itself is in it too: the tool must read exactly what the compiler is given.  The search excludes files already listed, by full path, so the compiler is given none twice.
- **A file whose name differs from `sqlsource.json` only by case is found where the file system ignores case**, under the lower-case path, as the SDK's search finds an `.EditorConfig`.  The spec says such a file is not read; telling its real case would take a glob of every folder and a comparison per file through a property, and the generator and the tool, which see only the lower-case path, could not tell either.
- **`SqlSourceTrackAdditionalFiles` depends on the search**, so that its hash covers the settings files; MSBuild promises no order for two targets that hook the same one.
- **The search depends on `SqlSourceTrimMetadataOfFiles`**, the hook the manual gives for a target that adds a `.sql` file, so such a target runs before the search too.  Layer 6 replaces the hook.

## The search runs last (owner's answer to review finding R1-F1)

The first review found that a `.sql` file added by a target declared after the package's targets, from a hook of `CoreCompile` in `Directory.Build.targets` for one, reached the compiler without the settings file beside it.  The owner asked for a fix rather than a tech-debt record.  NuGet imports a package's targets before `Directory.Build.targets`, and MSBuild runs the targets that hook one target in the order they are declared, so no target in `SqlSource.targets` can run after such a hook.  The SDK imports every file of `AfterMicrosoftNETSdkTargets` after all of its targets, `Directory.Build.targets` included (SDK 6, 8 and 10 checked), so:

- `SqlSource.props` appends `$(MSBuildThisFileDirectory)SqlSource.AfterSdk.targets` to `AfterMicrosoftNETSdkTargets`, keeping its value.
- `SqlSource.AfterSdk.targets` empties `SqlSourceSettingsSearchHook` and holds `SqlSourceSettingsFilesBeforeCompile`, `BeforeTargets="CoreCompile"`, `DependsOnTargets="SqlSourceFindSettingsFiles;SqlSourceTrackAdditionalFiles"`.
- In `SqlSource.targets`, the search and the tracking hook `$(SqlSourceSettingsSearchHook)`, which is `GenerateMSBuildEditorConfigFileCore` unless the late file emptied it: a project that is not of the SDK, or that overwrites `AfterMicrosoftNETSdkTargets`, still searches, as before.
- The new file is packed, listed by `tools/check-package.sh`, and copied beside the other two wherever a test or a script copies them.
- `SettingsFileSearchTests` covers a `CoreCompile` hook in the fixture's `Directory.Build.targets`, with the late file and without it (`-p:AfterMicrosoftNETSdkTargets=`).

## Verify first

A design-time build runs the target and passes its items to the compiler: `dotnet msbuild -t:Compile -p:DesignTimeBuild=true -p:BuildingProject=false -p:SkipCompilerExecution=true -p:ProvideCommandLineArgs=true -getItem:CscCommandLineArgs`, the properties Roslyn's language server and `MSBuildWorkspace` set, lists every settings file as `/additionalfile:`.  Done by hand before planning; Task 3 pins it in a test.  Visual Studio's `CompileDesignTime` is not in the .NET SDK, and calls `Compile` with the same two properties `SkipCompilerExecution` and `ProvideCommandLineArgs`.

## Review Focus

1. A `.sql` file linked from outside the project's folder: the files above it are found, and the manifest lists them.
2. A path holding `;`, `=`, `%`, a quote or a letter outside ASCII: the `Settings=` line holds it whole (fixture `OddPaths`).
3. A settings file the project lists itself, by a relative path: given to the compiler once, listed once.
4. A `.sql` file a target adds through the documented hook: the files above it are found, in a build and in the manifest.
5. A folder with a `sqlsource.json` and no `.sql` or C# file at or below it: not found.

---

### Task 1: The search target, the `Content` exclusion and the manifest lines

**Files:**
- Modify: `src/SqlSource/build/SqlSource.targets`
- Test: `tests/SqlSource.Tests/Package/BuildFileTests.cs`

**Interfaces:**
- Produces: target `SqlSourceFindSettingsFiles`; item types `SqlSourceSettingsSearchStart`, `SqlSourceSettingsSearchFolder`, `SqlSourceSettingsFileFound`, `SqlSourceManifestSettingsFile`; manifest line `Settings=<full path>`.

- [ ] **Step 1: Write the failing tests** in `BuildFileTests`:
  - `Targets_SettingsFileSearch_FindsEverySqlsourceJsonAboveTheSqlFilesAndTheCompileFiles`: the target named `SqlSourceFindSettingsFiles` has `BeforeTargets="GenerateMSBuildEditorConfigFileCore"`, `DependsOnTargets="SqlSourceTrimMetadataOfFiles"`, no condition; its items are, in order, `SqlSourceSettingsSearchStart` from `@(AdditionalFiles)` with `Condition="'%(Extension)' == '.sql'"` and from `@(Compile)`, `SqlSourceSettingsSearchFolder` from `@(SqlSourceSettingsSearchStart->GetPathsOfAllDirectoriesAbove())`, `SqlSourceSettingsFileFound` from `@(SqlSourceSettingsSearchFolder->'%(FullPath)'->Distinct()->Combine('sqlsource.json')->Exists())`, and `AdditionalFiles` from `@(SqlSourceSettingsFileFound)` with `Exclude="@(AdditionalFiles->'%(FullPath)')"` and `Visible="false"`.
  - `Targets_ListOfAdditionalFiles_IsAnInputOfTheCompiler` also asserts the tracking target depends on `SqlSourceFindSettingsFiles`.
  - `Targets_Root_...`: the root holds `PropertyGroup`, `ItemGroup`, `ItemGroup`; the first depends-on line is `SqlSourceTrimProperties;SqlSourceTrimMetadataOfFiles;SqlSourceFindSettingsFiles`; the third group is one `Content` item, `Update="**/sqlsource.json"`, `CopyToOutputDirectory="Never"`, `CopyToPublishDirectory="Never"`.
  - `Targets_ManifestTarget_ListsTheSettingsFilesBetweenTheSqlFilesAndTheCompileFiles`: `SqlSourceManifestSettingsFile` from `@(AdditionalFiles)` with `Condition="'%(Filename)%(Extension)' == 'sqlsource.json'"`, and the line `@(SqlSourceManifestSettingsFile->'%(FullPath)'->Distinct()->'Settings=%(Identity)')` after the `File` transform and before the `Compile` one.
  - Count of targets of a build becomes 5; `Targets_MetadataOfEverySqlFile_IsTrimmed` finds its item inside `SqlSourceTrimMetadataOfFiles`; the manifest's item types add `SqlSourceManifestSettingsFile`; a new test holds every item type the targets make starting with `SqlSource` but `AdditionalFiles`, `CustomAdditionalCompileInputs`, `FileWrites`, `Content`, `None`.
- [ ] **Step 2: Run** `dotnet test --project tests/SqlSource.Tests -- --filter-class "*BuildFileTests"`; expected FAIL on the new assertions.
- [ ] **Step 3: Implement** the target, the tracking dependency, the depends-on line, the `Content` group and the two manifest elements, each with a comment in the file's style.
- [ ] **Step 4: Run** the same tests; expected PASS.
- [ ] **Step 5: Commit.**

### Task 2: `ManifestReader` reads `Settings=`

**Files:**
- Modify: `src/SqlSource.Tool/Projects/ManifestReader.cs`, `src/SqlSource.Tool/Projects/ProjectManifest.cs`
- Test: `tests/SqlSource.Tool.Tests/Projects/ManifestReaderTests.cs`

**Interfaces:**
- Produces: `ProjectManifest.SettingsFiles`, `required ImmutableArray<string>`: the paths of the `Settings=` lines as they are, in their order, as `CompileFiles` keeps the `Compile=` lines.

- [ ] **Step 1: Write the failing tests:** `Example` gains two `Settings=` lines between its `File` and `Compile` lines, and `Read_ExampleOfTheSpec_GivesEveryMember` asserts `SettingsFiles` is `["/work/src/sqlsource.json", "/work/src/App/sqlsource.json"]`; `Read_OnlyTheVersionAndTheProject_GivesEmptyMembers` asserts it empty; `Read_SettingsLine_IsThePathAsItIs`; `Read_SettingsLineAmongTheLinesOfAFile_KeepsTheMetadataWithTheFile`.
- [ ] **Step 2: Run** `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "*ManifestReaderTests"`; expected compile failure.
- [ ] **Step 3: Implement** a `case "Settings":` that adds a non-empty value.
- [ ] **Step 4: Run**; expected PASS.
- [ ] **Step 5: Commit.**

### Task 3: The fixture and the tests that run MSBuild

**Files:**
- Create: `tests/SqlSource.Tool.Tests/Fixtures/Projects/SettingsFiles/` with `sqlsource.json` (above the project), `Unused/sqlsource.json` (no `.sql` or C# file at or below), `Shared/sqlsource.json` and `Shared/Linked.sql`, and `App/App.csproj` (imports `../../build/`, links `../Shared/Linked.sql`, lists `../sqlsource.json` itself), `App/Directory.Build.targets` (adds `Generated/Late.sql` from a target with `BeforeTargets="SqlSourceTrimMetadataOfFiles"`), `App/sqlsource.json`, `App/Queries/Users.sql`, `App/Queries/sqlsource.json`, `App/Generated/Late.sql`, `App/Generated/sqlsource.json`, `App/Code/Repository.cs`, `App/Code/sqlsource.json`.  Each `sqlsource.json` holds `{}` or a valid file of the spec's format.
- Create: `tests/SqlSource.Tool.Tests/Fixtures/Projects/OddPaths/<odd folder>/sqlsource.json`.
- Modify: `tests/SqlSource.Tool.Tests/Projects/ManifestTargetTests.cs`; Create: `tests/SqlSource.Tool.Tests/Projects/SettingsFileSearchTests.cs`.

- [ ] **Step 1: Write the tests:**
  - `WriteManifest_SettingsFilesAboveTheFiles_AreListedBetweenTheSqlFilesAndTheCompileFiles` (copy `SettingsFiles`, project `App/App.csproj`): the `Settings=` lines are the six files other than `Unused/`, each once, as a set; every `Settings=` line is after the last `File=` line and before the first `Compile=` line.
  - `WriteManifest_PathsWithCharactersThatMSBuildReads_AreListedWhole` also expects `Settings=` + the odd folder's file.
  - `WriteManifest_ProjectWithOneFramework_HoldsWhatTheCompilerIsGiven` keeps its exact list (no settings file).
  - `SettingsFileSearchTests.Compile_DesignTimeBuild_GivesTheCompilerEverySettingsFileOnce`: runs `-restore -t:Compile` with the design-time properties of Verify first and `-getItem:CscCommandLineArgs`, and asserts the `/additionalfile:` arguments that end in `sqlsource.json`, as full paths, are the same six, each once.
- [ ] **Step 2: Run** `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "*ManifestTargetTests" --filter-class "*SettingsFileSearchTests"`; expected PASS with Task 1 in place.
- [ ] **Step 3: Commit.**

### Task 4: The publish check, the docs and the AGENTS.md rules

**Files:**
- Modify: `tools/check-package-install.sh` (web stage writes `Queries/sqlsource.json`, and fails when one is copied), `tools/README.md`.
- Modify: `docs/architecture/msbuild-and-manifest.md` (the search, the third group, the `Settings=` line, the reader), `src/SqlSource/AGENTS.md` (root elements, the format's keys), `tests/README.md` if it lists fixtures, the comments of `SqlSource.targets`.

- [ ] **Step 1:** Edit, keeping edits in shared files small and local.
- [ ] **Step 2: Run** `./format.sh` then `./pre-commit-validation.sh`; expected every step passed.
- [ ] **Step 3: Commit.**

### Task 5: Hand-over notes

- [ ] Write `2-build-notes.md` in the shared notes folder: the search target's name and where it runs, the `Settings=` format and `ProjectManifest.SettingsFiles`, the Verify-first result, and that MSBuild matches the name ignoring case while the spec reads `sqlsource.json` exactly.
