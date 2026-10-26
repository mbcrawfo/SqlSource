# Settings files, layer 3: the generator reads the chains - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The source generator reads every `sqlsource.json` the build gives it, resolves each `.sql` file's and each type's settings from the chain of its folder, and reports each reachable settings file's problems at its file, line and column.

**Architecture:** Each settings file is read once, by a step of its own, into a `ListedSettingsFile`; the files are collected into a `SettingsFileSet`, sorted by folder, which precomputes the resolved chain that starts at each file, so that the chain of any folder is one search for its nearest settings file.  Stage one puts the chain's dialect and generator parameters into `FileParseInput`; stage two joins a `FileLevel` per `.sql` file (the chain over the file's metadata) where `FileSettings` joins today; the nested class takes the chain of the type's C# file.  `SettingPlace` becomes a value with a kind, and for a settings file its path and its folder's depth, which `SettingPlaces.Specificity` ranks.

**Tech Stack:** C# (`netstandard2.0` generator, Roslyn 4.8.0 floor), xUnit v3, Shouldly.

**Spec:** [`docs/superpowers/specs/2026-10-25-settings-files-design.md`](../specs/2026-10-25-settings-files-design.md), "The layers" item 3, "Which settings apply", "What changes in the generator and the tool".  Read the root `AGENTS.md`, `src/SqlSource/AGENTS.md`, `tests/AGENTS.md` and `tests/SqlSource.Tests/Generator/AGENTS.md` before any task.

## Where the merged code differs from the spec

- The reader is in `src/SqlSource/SettingsFiles/` (layer 1), which may use nothing of `Generation/`.  The chains need `SqlPath`, so they go in a new folder, `src/SqlSource/Generation/Chains/` (namespace `SqlSource.Generation.Chains`), which the tool can call as it calls `PathResolver`.
- `SettingPlace` is an enum today, and `TypeOverride`/`ColumnType` carry it.  The spec leaves "a depth on the entry or a place per depth" to the builder: here `SettingPlace` becomes a `readonly record struct` carrying the kind and, for a settings file, its path and depth, so the reader's existing `place` parameter stamps every entry with no re-stamping per chain.  Depth is the folder's own depth (segments of its normalised path), which orders the files of any one chain nearest first, since they are all ancestors of one folder.
- The owner's decision of 2026-10-26 narrows "Every settings file the build lists is checked": only files that the chain of some `.sql` item's or C# `Compile` item's folder reaches are reported.  Task 5 implements it and amends the spec's third bullet of "Who reports what".
- The MSBuild levels stay below every settings file until layer 6: markers, attribute, chain (nearest first), metadata, property, default.  The dialect: header marker, chain, metadata, property, `ansi`.  `SQLSRC011` for a metadata value that is not a dialect is still reported for a claimed file whether or not the chain overrides it, as the tool does today.

## Global Constraints

- `netstandard2.0` only; Roslyn API of 4.8.0 only; no collection expression targets `ImmutableArray<T>`; tests in `tests/SqlSource.Tests/Generator/` start with the shared line and use C# 12 at most.
- Models between steps are records holding `EquatableArray<T>`; no Roslyn object (`AdditionalText` excepted where `src/SqlSource/AGENTS.md` lists it) flows between steps; every `Collect()` is followed by a `Select` giving an `EquatableArray<T>`.
- Nothing a user writes may make the generator throw.
- Paths go through `SqlPath.Normalize` and compare with `SqlPath.Comparer`; never `System.IO.Path` or the file system.
- A settings file is named `sqlsource.json` exactly (`SettingsFile.FileName`, ordinal).  A file named otherwise is not read and not reported (builder's call, recorded in the PR).
- The build reports a `SettingsFileError` when `IsAboutTheFile`, or when its `Setting` is any key but `database`; the tool's rule (layer 4) is `dialect`, `output` and `database`.
- Every commit passes `./pre-commit-validation.sh`; run `./format.sh` before it.  AGENTS.md files are updated in the commit that renames or moves what they name.
- A step whose re-runs matter has a name in `Generation/TrackingNames.cs` and a caching test.

## Review Focus

1. **A path with backslashes or another case** (`C:\App\Queries\a.sql` against `c:/app/sqlsource.json`): the chain is found, since lookups use normalised paths and `SqlPath.Comparer`.  Test in Task 2.
2. **A `.sql` file or a settings file at the root folder** (normalised folder empty, depth 0): the chain is found and ranks below any deeper file.  Test in Task 2.
3. **One settings file listed twice, or two paths that differ only by case**: one file is kept (the first), its problems reported once.  Test in Task 2 and Task 5.
4. **A settings file edited from valid JSON to text that is not**: its `root` becomes unknown, so the chain passes through it to the farther files, and it gives no settings.  Test in Task 2.
5. **A linked `.sql` or C# file outside the project's folder**: it uses the chain of its own folder.  Test in Task 4.

---

### Task 1: `SettingPlace` with a kind, a path and a depth

**Files:**
- Modify: `src/SqlSource/Settings/SettingPlace.cs` (enum becomes `SettingPlaceKind` plus the struct `SettingPlace`)
- Modify: `src/SqlSource/Settings/SettingPlaces.cs`
- Modify: `src/SqlSource/Generation/Types/QueryTyper.cs` (`ColumnTypePlaceOf`, `PlaceOf`)
- Modify: every `switch` on a `SettingPlace` constant the build finds
- Modify: `docs/architecture/generator.md` (the type override's ranking sentence; the column types)
- Test: `tests/SqlSource.Tests/Settings/SettingPlacesTests.cs` (create), `tests/SqlSource.Tests/Generation/Types/QueryTyperTests.cs`

**Interfaces:**
- Produces:
  - `internal enum SettingPlaceKind { Property, Metadata, Attribute, FileHeader, Query, SettingsFile }` (same numbers for the first five).
  - `internal readonly record struct SettingPlace(SettingPlaceKind Kind, string? Path, int Depth)` with static properties `Property`, `Metadata`, `Attribute`, `FileHeader`, `Query` (path null, depth 0) and `static SettingPlace OfSettingsFile(string path, int depth)`.  `Path` is the settings file's path as the compiler gives it.
  - `SettingPlaces.Specificity(SettingPlace place) -> int`: `Query` `int.MaxValue`, `FileHeader` `int.MaxValue - 1`, `SettingsFile` `2 + Depth`, `Metadata` 1, `Property` and `Attribute` 0.

- [ ] **Step 1: Write the failing tests** in `SettingPlacesTests`: `Specificity_OfEachPlace_RanksQueryThenHeaderThenSettingsFilesThenMetadataThenProperty` (Query > FileHeader > OfSettingsFile("/a/b/sqlsource.json", 2) > OfSettingsFile("/a/sqlsource.json", 1) > OfSettingsFile("/sqlsource.json", 0) > Metadata > Property, and Attribute == Property); `Specificity_OfADeepSettingsFile_StaysBelowTheFileHeader` (depth 10 000 < FileHeader).  In `QueryTyperTests`, a test that `SQLSRC250`'s place for an entry at `SettingPlace.OfSettingsFile("/app/sqlsource.json", 1)` reads `in the typeOverrides of the settings file /app/sqlsource.json`, and the column types' `in the columnTypes of the settings file /app/sqlsource.json` (follow the existing tests of `PlaceOf` there).
- [ ] **Step 2: Run them to see them fail** (`dotnet test --project tests/SqlSource.Tests --filter-class "*SettingPlacesTests"`; compile error expected).
- [ ] **Step 3: Implement.**  Rewrite the two switches in `QueryTyper` as switches on `place.Kind`, adding the settings file's wording above.  Everything that wrote `SettingPlace.X` keeps compiling through the static properties.
- [ ] **Step 4: Update `docs/architecture/generator.md`**: the type override's ranking ("`Query`, then `FileHeader`, then `Metadata`, then `Property`") names the settings files between `FileHeader` and `Metadata`, nearer above farther, by their folder's depth; and the reading-a-level paragraph says what `SettingPlace` holds.
- [ ] **Step 5: Format, validate (`./format.sh && ./pre-commit-validation.sh`), commit** "Give SettingPlace a settings file's path and depth, ranked by depth".

### Task 2: Settings files read, and their chains

**Files:**
- Create: `src/SqlSource/Generation/Chains/ListedSettingsFile.cs`, `SettingsFileSet.cs`, `SettingsChain.cs`, `SettingsFileSide.cs`
- Test: `tests/SqlSource.Tests/Generation/Chains/ListedSettingsFileTests.cs`, `SettingsFileSetTests.cs`
- Modify: `src/SqlSource/AGENTS.md` (a `Generation/Chains/` rule set: chains are built only here, for the generator and the tool; never resolve a chain by a pass over the files)

**Interfaces:**
- Consumes: `SettingsFileReader.Read(string text, SettingPlace place) -> SettingsFile`; `SettingsFileDiagnostics.Create(SettingsFileError, string path, SourceText) -> DiagnosticInfo`; `SettingPlace.OfSettingsFile` (Task 1).
- Produces:
  - `internal enum SettingsFileSide { Build, Tool }` and `internal static class SettingsFileSides { static bool Reports(SettingsFileSide side, SettingsFileError error) }`: Build reports `IsAboutTheFile` or `Setting != SettingsFileKeys.Database`; Tool reports `IsAboutTheFile` or `Setting` is `dialect`, `output` or `database`.
  - `internal sealed record ListedSettingsFile(string Path, string NormalizedFolder, int Depth, SettingsFile File, EquatableArray<DiagnosticInfo> Diagnostics)`.
    - `static bool IsSettingsFile(string path)`: the last segment equals `SettingsFile.FileName` ordinally.
    - `static ListedSettingsFile? Read(AdditionalText file, SettingsFileSide side, CancellationToken cancellationToken)`: null when `IsSettingsFile` is false or the path does not normalise; a text the compiler cannot give is read as `""`.  `Depth` is the number of segments of `NormalizedFolder` (0 for the root).  The place given to the reader is `SettingPlace.OfSettingsFile(file.Path, Depth)`.  `Diagnostics` holds the errors `side` reports, in the reader's order, made with `file.Path` and the file's `SourceText`.
  - `internal sealed record SettingsChain(SettingsLevel Level, SqlDialectChoice? Dialect)` with `static SettingsChain None { get; }` (`SettingsLevel.None`, null), shared.  `Level` is the chain's files nearest over farther with `SettingsLevel.Over`; `Dialect` the nearest file's that sets one.
  - `internal sealed record SettingsFileSet(EquatableArray<ListedSettingsFile> Files, EquatableArray<int> Parents, EquatableArray<SettingsChain> Chains)`:
    - `static SettingsFileSet Empty { get; }`
    - `static SettingsFileSet Of(IEnumerable<ListedSettingsFile> files)`: sorted by `NormalizedFolder` with `SqlPath.Comparer`, the first of each folder kept; `Parents[i]` the index of the nearest file in a folder above file `i`'s, or -1; `Chains[i]` the chain that starts at file `i`: it ends after the first file whose `File.Root` is `true`, and goes on past a file whose `Root` is false or null.
    - `SettingsChain ChainOf(string normalizedFilePath)`: the chain of the file's folder, from the nearest file in that folder or one above it, or `SettingsChain.None`.  It searches each folder above the file's without allocating (compare spans with `StringComparison.OrdinalIgnoreCase`, the order of `SqlPath.Comparer`), and returns the shared `Chains[i]` object.
    - `EquatableArray<string> Reached(IEnumerable<string> normalizedFilePaths)`: the `NormalizedFolder` of every file that the chain of some path's folder reaches, in the order of `Files`.  It stops reading paths once every file is reached.

- [ ] **Step 1: Write the failing tests.**  Build each `ListedSettingsFile` from `InMemoryAdditionalText` (in `SqlSource.Testing/Generator/`).
  - `ListedSettingsFileTests`: `Read_FileNamedExactly_IsRead` (path `/app/Queries/sqlsource.json`, `NormalizedFolder` `app/Queries`, `Depth` 2); `Read_FileNamedInAnotherCase_IsNull` (`SqlSource.json`); `Read_FileAtTheRoot_HasDepthZero` (`/sqlsource.json`); `Read_TypeOverride_IsAtTheFilesPlace` (`{"typeOverrides": {"int4": "long"}}` gives an entry whose `Place` is `OfSettingsFile(path, depth)`); `Read_InvalidDatabase_IsNotReportedByTheBuild_AndIsByTheTool` (`{"database": ""}`-style invalid value; use a value `SettingsLevelReader` rejects); `Read_UnknownKey_IsReportedAtItsLineAndColumn` (`{\n  "dialekt": "postgres"\n}` gives one `SQLSRC016` whose location's line is 1 and character 2); `Read_TextTheCompilerCannotGive_IsInvalidJson` (an `AdditionalText` whose `GetText` returns null gives one `SQLSRC015`).
  - `SettingsFileSetTests`: `ChainOf_NearerFile_OverridesFartherSettingBySetting` (`/app/sqlsource.json` `{"output":"sql","inputModelSuffix":"In"}`, `/app/Queries/sqlsource.json` `{"output":"models"}`: chain of `app/Queries/a.sql` has `Output` Models and `InputModelSuffix` "In"); `ChainOf_RootFile_EndsTheChain` (`root: true` in `app/Queries` hides `/app`'s settings); `ChainOf_FileThatIsNotJson_IsPassedThrough` (`app/Queries/sqlsource.json` text `{` leaves `/app`'s settings applying); `ChainOf_RootFalse_GoesOn`; `ChainOf_Dialect_IsTheNearestFilesThatSetsOne`; `ChainOf_FolderWithNoFileAbove_IsNone` (reference-equal to `SettingsChain.None`); `ChainOf_FilesOfOneFolder_ShareOneChain` (reference-equal); `ChainOf_PathInAnotherCaseOrWithBackslashes_FindsTheChain` (Review Focus 1: file at `C:\App\sqlsource.json`, lookup `c:/app/Queries/a.sql` after `SqlPath.Normalize`); `ChainOf_FileAtTheRoot_AppliesToEveryFolder` (Review Focus 2); `Of_FolderListedTwice_KeepsTheFirst` (Review Focus 3); `ChainOf_FileEditedToTextThatIsNotJson_PassesThrough` (Review Focus 4: two sets, before and after); `Of_SameFiles_AreEqual` (two sets from equal inputs compare equal); `Reached_FileBeyondEveryRoot_IsNotReached` (`/sqlsource.json` broken, `/app/sqlsource.json` root, paths under `app/` only: reached is `["app"]`); `Reached_FileReachedThroughASiblingFolder_IsReached` (`/a` file, `/a/b` root, a path under `a/c`: both reached); `Reached_FileWithNoPathBelow_IsNotReached`; `Reached_FileWhoseRootIsUnknown_LetsTheChainGoOn`.
- [ ] **Step 2: Run them to see them fail** (`dotnet test --project tests/SqlSource.Tests --filter-namespace "*Generation.Chains"`).
- [ ] **Step 3: Implement** the four files.  `Of` computes `Parents` with one pass over the sorted folders (a stack of the open ancestors works, since a folder's ancestors sort before it and its subfolders follow it), then `Chains` in that order: `Root == true` gives the file's own level and dialect, otherwise its level over its parent's chain's (`SettingsLevel.Over`) and its dialect or its parent's.  `Reached` walks `Parents` from each path's nearest file, marking files until one whose `Root` is true.
- [ ] **Step 4: Run the tests; they pass.**
- [ ] **Step 5: Add the `Generation/Chains/` rules to `src/SqlSource/AGENTS.md`, format, validate, commit** "Read settings files and build their chains, for the generator and the tool".

### Task 3: Stage one: the chain's dialect and `keep-comments` reach the parse

**Files:**
- Modify: `src/SqlSource/Generation/FileParseInput.cs`, `src/SqlSource/SqlSourceGenerator.cs`, `src/SqlSource/Generation/TrackingNames.cs`
- Modify: `src/SqlSource.Tool/Planning/RunPlanner.cs` (pass `SettingsChain.None`; layer 4 replaces it)
- Test: `tests/SqlSource.Tests/Generator/SettingsFileTests.cs` (create), `tests/SqlSource.Tests/Generator/CachingParseTests.cs` or a new `CachingSettingsFileTests.cs`, `tests/SqlSource.Tests/Generation/MSBuild/DialectSettingTests.cs` (callers of `Resolve`)
- Modify: `tests/SqlSource.Testing/Generator/GeneratorHarness.cs` (a `SettingsFile(string path, string json)` helper giving an `SqlFile`, so a test passes settings files as additional texts)

**Interfaces:**
- Consumes: `ListedSettingsFile.Read`, `SettingsFileSet.Of`, `SettingsFileSet.ChainOf` (Task 2).
- Produces:
  - `FileParseInput.Resolve(FileMetadata metadata, SettingsChain chain, DialectSetting projectDialect, bool projectKeepsComments, EquatableArray<string> commentPaths)`: `Dialect` is `chain.Dialect ?? metadata.Dialect.Dialect ?? projectDialect.Dialect ?? default`; `InvalidDialect` is `metadata.Dialect.InvalidValue` (whether or not the chain sets one); `CommentsWanted` is true when the first list of `chain.Level.Parameters`, the metadata's, has `keep-comments`, else when `projectKeepsComments` and neither gives a list, or when the path is in `commentPaths`.
  - In `SqlSourceGenerator`, a stage method `ReadSettingsFiles(context)` called first after `RegisterAttribute`, returning `IncrementalValueProvider<SettingsFileSet>`: `AdditionalTextsProvider.Where(IsSettingsFile)` -> `Read(..., SettingsFileSide.Build, ct)` -> `WhereNotNull` (tracking `SettingsFile`) -> `Collect` -> `SettingsFileSet.Of` (tracking `SettingsFiles`).  `FileParseInput`'s step combines it and resolves `set.ChainOf(normalizedPath)` for each file (`SettingsChain.None` for a path that does not normalise).

- [ ] **Step 1: Write the failing generator tests** in `SettingsFileTests` (through `GeneratorHarness.Run` with settings files among the `SqlFile`s): `Run_DialectInTheFolder_ParsesTheFileWithIt` (`/app/Repo/sqlsource.json` `{"dialect":"postgres"}`, a query whose parse differs between `ansi` and `postgres`, as `DialectTests` does); `Run_DialectInTheFolder_OverridesTheProperty`; `Run_DialectMarker_OverridesTheFolder`; `Run_KeepCommentsInTheFolder_KeepsTheComments` (`{"generatorParameters":["keep-comments"]}`); `Run_EmptyListInANearerFile_DropsTheFartherKeepComments`.
- [ ] **Step 2: Write the failing caching tests**: `Run_SettingsFileEditChangesOneFoldersDialect_ParsesOnlyThatFoldersFiles` (two folders `/app/Users` and `/app/Orders`, the `CachingScene` files, one `sqlsource.json` in each; replacing `/app/Users/sqlsource.json`'s dialect gives `FileParseInput` Modified for `Users.sql` and Unchanged for `Orders.sql`, `ParsedFile` re-run for `Users.sql` alone); `Run_SettingsFileEditLeavesTheStageOneValues_ParsesNoFile` (change `inputModelSuffix`: every `FileParseInput` Unchanged, no `ParsedFile` re-run); `Run_SettingsFileAdded_ParsesOnlyTheFilesItReaches`.  Use `driver.ReplaceAdditionalText` / `AddAdditionalTexts` and `StepReasons` as the other `Caching*Tests` do.
- [ ] **Step 3: Run them to see them fail.**
- [ ] **Step 4: Implement** `Resolve`'s new parameter, the stage method, the tracking names `SettingsFile` and `SettingsFiles`, and the tool's call with `SettingsChain.None`.  Update every test caller of `Resolve`.
- [ ] **Step 5: Run all of `tests/SqlSource.Tests` and `tests/SqlSource.Tool.Tests`** (`GeneratorParityTests` must pass unchanged).
- [ ] **Step 6: Update `docs/architecture/generator.md`** ("The settings have two stages", "How the dialect reaches the parse", "The tool runs the same code": the new `Resolve` argument) and `src/SqlSource/AGENTS.md` (the dialect rule names the chain); format, validate, commit "Resolve a file's dialect and keep-comments from its chain before the parse".

### Task 4: Stage two: each file's level and the type's nested class

**Files:**
- Create: `src/SqlSource/Generation/FileLevel.cs`
- Modify: `src/SqlSource/SqlSourceGenerator.cs`, `TrackingNames.cs`, `Generation/NestedClassCheck.cs`, `Generation/TypeQueries.cs` (doc), `Generation/Types/MappedTypeNames.cs`, `Generation/Types/FileTypesInput.cs`, `Settings/QuerySettings.cs` (`ResolveNestedClass`; rename the `metadata` parameters to `file` and say in each doc that the file's level is its chain over its metadata)
- Test: `tests/SqlSource.Tests/Generator/SettingsFileTests.cs`, `CachingOptionsTests.cs`, `tests/SqlSource.Tests/Generation/Types/MappedTypeNamesTests.cs`, `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs`

**Interfaces:**
- Consumes: `SettingsFileSet` provider (Task 3), `SettingsFileSet.ChainOf`.
- Produces:
  - `internal sealed record FileLevel(string NormalizedPath, SettingsLevel Level)` with `static FileLevel? Resolve(FileMetadata metadata, SettingsChain chain)`: `chain.Level.Over(metadata.Settings?.Level ?? SettingsLevel.None)`; null when that is `SettingsLevel.None` or the path does not normalise.
  - Pipeline: `fileMetadata.Combine(settingsFiles)` -> `FileLevel.Resolve` -> `WhereNotNull` (tracking `FileLevel`) -> `Collect` -> `ToSorted` (tracking `FileLevels`).  `FileLevels` replaces `FilesSettings` as the input of `SelectTypeQueries`, `MappedTypeNames.Collect` and `FileTypesInput.Gather`; `FilesSettings` stays only for `SQLSRC014`.
  - `MappedTypeNames.Collect(ProjectSettings project, EquatableArray<FileLevel> files, EquatableArray<ParsedSqlFile> parsed)`; `FileTypesInput.Gather(..., EquatableArray<FileLevel> fileLevels, ...)` in the place of `filesSettings`.
  - `QuerySettings.ResolveNestedClass(SettingsLevel attribute, SettingsLevel chain, SettingsLevel property)`: attribute, chain, property, `Sql`.
  - `NestedClassCheck.Apply(DeclaredType declared, SettingsFileSet settingsFiles, SettingsLevel property)`: the chain of `SqlPath.Normalize(declared.Type.FilePath)`; the step combines the set with the project's level.

- [ ] **Step 1: Write the failing generator tests** in `SettingsFileTests`: `Run_OutputInTheFolder_Applies` (`{"output":"sql"}` gives no models); `Run_AttributeOverridesTheFolder`; `Run_FolderOverridesTheMetadataAndTheProperty` (a property and a metadata of `SqlSourceInputModelSuffix`, a file's `inputModelSuffix`: the file's wins); `Run_NearerFileOverridesAFartherOne`; `Run_RootFileHidesTheFilesAbove`; `Run_GeneratorParametersOfANearerFile_ReplaceTheFartherListWhole`; `Run_TypeOverridePlainKeyInANearerFile_BeatsAnExactKeyInAFartherOne` (`/app/sqlsource.json` `{"typeOverrides":{"pg_catalog.int4":"long"}}`, `/app/Repo/sqlsource.json` `{"typeOverrides":{"int4":"short"}}`, a sidecar typing a column `int4`: the C# type is `short`; follow `TypeOverrideLevelTests`' scenes); `Run_TypeOverrideInTheFolder_BeatsTheProperty`; `Run_ColumnTypeWithoutASchemaInANearerFile_BeatsOneWithASchemaFarther` (follow `ColumnTypeTests`); `Run_MappedTypeNotFound_NamesTheSettingsFile` (`SQLSRC250` message contains `in the typeOverrides of the settings file /app/Repo/sqlsource.json`); `Run_NestedClassInTheTypesFolder_NamesTheClass` (`{"nestedClass":"Queries"}` beside `GeneratorHarness.SourcePath`, `SqlLocation.Nested`; the folder of the `.sql` file holds another value that is not used); `Run_LinkedFileOutsideTheProject_UsesItsOwnFoldersChain` (Review Focus 5: a `.sql` at `/shared/Users.sql` claimed through `Path = "../../shared/Users.sql"` from `/app/Repo/Sample.cs`, a settings file in `/shared` and another in `/app/Repo`: the `/shared` one applies).
- [ ] **Step 2: Write the failing caching tests** in `CachingOptionsTests`: `Run_SettingsFileOutputChanged_EmitsTheTypesOfItsFolderAndParsesNoFile` (`TypeQueries` Modified only for the type whose file is under the edited folder; `ParsedFile` all Cached); `Run_SettingsFileNestedClassChanged_ChecksOnlyTheTypesOfItsFolder` (`NestedClassCheck` Modified for the type in that folder only).
- [ ] **Step 3: Run them to see them fail.**
- [ ] **Step 4: Implement.**
- [ ] **Step 5: Run `tests/SqlSource.Tests` and `tests/SqlSource.Tool.Tests`.**
- [ ] **Step 6: Update `docs/architecture/generator.md`** (the mermaid diagram and the step list: `SettingsFile`, `SettingsFiles`, `FileLevel`, `FileLevels`, `NestedClassCheck`'s inputs; "Settings" intro and "The settings have two stages") and `src/SqlSource/AGENTS.md` where it names `FileSettings` as the stage-two join; format, validate, commit "Join each file's chain over its metadata in stage two, and the nested class from the type's folder".

### Task 5: The settings files' diagnostics, for reachable files only

**Files:**
- Modify: `src/SqlSource/SqlSourceGenerator.cs`, `TrackingNames.cs`
- Modify: `docs/superpowers/specs/2026-10-25-settings-files-design.md` (third bullet of "Who reports what")
- Test: `tests/SqlSource.Tests/Generator/SettingsFileTests.cs`, a caching test in `CachingSettingsFileTests.cs` (or wherever Task 3 put its caching tests)

**Interfaces:**
- Consumes: `SettingsFileSet.Reached`, `ListedSettingsFile.Diagnostics` (Task 2); the settings provider (Task 3); `sqlPaths` (expose it from `ClaimFiles`' `ClaimedFiles`).
- Produces, in a stage method `ReportSettingsFiles` called after `ClaimFiles`:
  - `CompilePaths`: `CompilationProvider.Combine(settingsFiles)` -> the `FilePath` of each of `compilation.SyntaxTrees`, as an `EquatableArray<string>`, and `Empty` without touching the trees when the set has no file.  It runs on every run, like `SupportedFramework`, and is equal until a file is added, removed or renamed.
  - `ReachedSettingsFiles`: `settingsFiles.Combine(sqlPaths).Combine(compilePaths)` -> `set.Reached(sqlPaths followed by each compile path that normalises)`.
  - A source output over `settingsFiles.Combine(reachedSettingsFiles)` reporting the `Diagnostics` of each reached file, in the order of `Files`.

- [ ] **Step 1: Write the failing generator tests**: `Run_UnknownKey_IsReportedAtTheSettingsFile` (`SQLSRC016` with the settings file's path, line and column in the formatted diagnostic); `Run_InvalidTypeOverride_IsReported` (`SQLSRC019`); `Run_InvalidDatabase_IsNotReported`; `Run_FileAboveARoot_IsNotReported` (`/sqlsource.json` text `{`, `/app/sqlsource.json` `{"root": true}`, every item under `/app`: no `SQLSRC015`); `Run_FileReachedOnlyByACSharpFile_IsReported` (a settings file in the C# file's folder, which holds no `.sql` file); `Run_FileReachedByAnUnclaimedSqlFile_IsReported`; `Run_FileNoItemReaches_IsNotReported` (`/other/sqlsource.json` with an unknown key); `Run_FileListedTwice_IsReportedOnce` (Review Focus 3); `Run_MisnamedSettingsFile_IsNotRead` (`/app/Repo/SqlSource.json` with a dialect: not applied, nothing reported).
- [ ] **Step 2: Write the failing caching tests**: `Run_CSharpEditThatAddsNoFile_ReachesNoSettingsFileAgain` (`CompilePaths` Unchanged, `ReachedSettingsFiles` Cached); `Run_SettingsFileEditedToAnError_ReportsItWithoutParsing` (`ParsedFile` all Cached).
- [ ] **Step 3: Run them to see them fail; implement; run `tests/SqlSource.Tests` and `tests/SqlSource.Tool.Tests`.**
- [ ] **Step 4: Amend the spec's third bullet of "Who reports what"** to the owner's decision of 2026-10-26, marked so: a listed settings file is checked only when the chain of the folder of some `.sql` item or C# `Compile` item reaches it, whether or not that `.sql` file is claimed or that C# file declares a type; a file beyond the first `"root": true` of every such chain is listed but never checked; a file whose text is not JSON has an unknown `root`, so chains pass through it.
- [ ] **Step 5: Update `docs/architecture/generator.md`** (the diagram and step list: `CompilePaths`, `ReachedSettingsFiles`, the new source output and its place in the order of outputs; "Settings files": no longer "only its reader is built"; "Diagnostics") and `src/SqlSource/AGENTS.md` (the list of steps that touch the compilation names `CompilePaths`, which reads the trees' paths and nothing else); format, validate, commit "Report the settings files that a chain reaches, at their file, line and column".

### Task 6: Hand-over notes and the cost of the new steps

**Files:**
- Create: `/Users/michael/Library/Application Support/Claude/side-session-notes/local_4cf760b4-56e0-49bb-9eff-d89dd6351edd/3-generator-notes.md` (outside the repository; not committed)

- [ ] **Step 1: Measure** in a `Release` build, with a `Stopwatch` loop in a throwaway program under the scratchpad (not committed): a project of 1 000 C# files and 1 000 `.sql` files in 100 folders, with and without three settings files: the time and allocation of one run after a C# edit (`CompilePaths` runs), and after a settings file edit that changes no stage-one value, before and after this branch.  The numbers go in the PR description, as `src/SqlSource/AGENTS.md` asks for a hot path.
- [ ] **Step 2: Write `3-generator-notes.md`** for layers 4 to 6: the API of `Generation/Chains/` the tool calls (`ListedSettingsFile.Read` with `SettingsFileSide.Tool`, `SettingsFileSet.Of`, `ChainOf`, `Reached` with the `.sql` paths and the manifest's `Compile=` paths), `FileParseInput.Resolve`'s chain argument, `FileLevel.Resolve`, `QuerySettings`' `file` parameter, how depth ranks, `SettingPlaceKind.SettingsFile` and the wording `the typeOverrides of the settings file <path>`, and what layer 6 removes (`SettingPlace.Property`, `Metadata`, the metadata half of `FileLevel`).
