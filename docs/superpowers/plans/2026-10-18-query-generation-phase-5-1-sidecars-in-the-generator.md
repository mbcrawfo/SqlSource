# Query generation, phase 5.1: sidecars in the generator - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The generator reads and caches each claimed `.sql` file's sidecar, `SqlSourceOutput` takes effect, and a build reports `SQLSRC237` to `SQLSRC245`, `SQLSRC209` and `SQLSRC210`, once per file.  No model is emitted.

**Architecture:** Sidecars are an input of their own, filtered by extension, read once each into a value-equal `SidecarFile` that holds every position a diagnostic needs.  Each type says which queries of its files need an entry; those needs are collected, merged by file and sorted.  One node per parsed file gathers the file, its sidecar and its needs, and a `SidecarCheck` step runs the checks in the spec's order and gives the diagnostics and the usable entries.

**Tech Stack:** C# on `netstandard2.0`, Roslyn incremental generators pinned to 4.8.0, xunit v3 with Shouldly, MSBuild props and targets.

**Spec:** [`docs/superpowers/specs/2026-10-18-query-generation-phase-5-1-sidecars-in-the-generator-design.md`](../specs/2026-10-18-query-generation-phase-5-1-sidecars-in-the-generator-design.md).  Read it with this plan; its "Calls made" bind every task.

## Global Constraints

- Every commit passes `./pre-commit-validation.sh` on the exact tree committed.  One commit per task, in order; the plan itself goes in Task 1's commit.
- Root `AGENTS.md`, `src/SqlSource/AGENTS.md`, `tests/AGENTS.md`, `tests/SqlSource.Tests/Generator/AGENTS.md`, `docs/manual/AGENTS.md`, `docs/architecture/AGENTS.md` and `docs/tech-debt/AGENTS.md` win over this plan and over any skill's default.
- Generator code: `netstandard2.0`, Roslyn 4.8.0 API only, no collection expression targeting `ImmutableArray<T>`, records with `EquatableArray<T>` between steps, no Roslyn object (`AdditionalText` aside, inside the input record of a read, as `FileParseInput` does) past the step that reads a sidecar, nothing that can throw on a user's file.
- Tests in `tests/SqlSource.Tests/Generator/` start with the `// Shared:` line, end in `Tests.cs`, use Roslyn 4.8.0 API and C# 12 at most.  Every run states its output; `null` only where the test is of the default, with a comment.
- Diagnostic ids: `SQLSRC237` malformed, `SQLSRC238` newer format, `SQLSRC239` older format, `SQLSRC240` tool version (warning), `SQLSRC241` missing entry, `SQLSRC242` engine not the file's, `SQLSRC243` no type map for the engine, `SQLSRC244` stale, `SQLSRC245` orphan (warning).  All in `SqlDiagnostics`; `SQLSRC240` and `SQLSRC245` are `DiagnosticSeverity.Warning` without `NotConfigurable`, every other new one an error with it.
- `SQLSRC209` and `SQLSRC210` stay in `ToolDiagnostics`.
- The generator never reads `SqlSourceDatabase`: an entry is current when its hash matches.
- Version comparison uses the prefix alone: everything from the first `-` or `+` is stripped from the sidecar's `toolVersion` before comparing with `PackageVersion.Prefix`.
- User documents name no internals (`docs/manual/AGENTS.md`, "Write effects, never internals"), and the manual's diagnostics index stays in id order across both classes.

## Review Focus

- **An empty sidecar file** (zero bytes) is `SQLSRC237` at (1,1) of the sidecar, never an exception.  Test in Task 5.
- **The same sidecar listed twice** (two `AdditionalFiles` items of one path) is read once and gives each diagnostic once.  Test in Task 4.
- **An entry whose name differs from its query's by case** (`getuser` for `GetUser`): names compare ordinally, so the query gets `SQLSRC241` and the entry `SQLSRC245`.  Test in Task 5.
- **An entry of an engine nobody knows** (`"engine": "oracle"`) is `SQLSRC242` at the query, never a crash and never usable.  Test in Task 5.
- **A malformed sidecar of a file whose every query is at `Sql`** is still `SQLSRC237`: every existing sidecar of a claimed file is read.  Test in Task 5.

---

### Task 1: The plan, the nesting spike and the epic's row

**Files:**
- Create: `docs/superpowers/plans/2026-10-18-query-generation-phase-5-1-sidecars-in-the-generator.md` (this file)
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (the 5.1 row: `Specified` to `In progress`)
- Scratch, never committed: `<scratchpad>/nesting-spike/`

- [ ] **Step 1: Build the scratch project.**  `NestingSpike.csproj` (`Microsoft.NET.Sdk`, `net8.0`), `Queries/Users.sql`, `Queries/Users.sql.json` (a copy of `tests/SqlSource.Tests/Snapshot/Examples/Users.sql.json`), and `build/SqlSource.props` and `build/SqlSource.targets` copied from `src/SqlSource/build/` with Task 2's two additions made by hand.  The project imports the props at the top of its body and the targets at the end, as NuGet would.
- [ ] **Step 2: Check the evaluation.**  Run `dotnet msbuild NestingSpike.csproj -getItem:None -getItem:AdditionalFiles` in the folder.  Expected: `Queries/Users.sql.json` is in both item types with `DependentUpon` = `Users.sql`; `Queries/Users.sql` has no `DependentUpon`.
- [ ] **Step 3: Ask the user** to open the scratch project in Rider and in Visual Studio, if they have it, and say whether `Users.sql.json` shows nested under `Users.sql`.  Do not wait on the answer: the rest of the work does not depend on it.  Record the answer in the pull request's description; where it never comes, the description says the nesting is unverified in that IDE.  If an IDE nests on only one item type, Task 2's update is kept on that one; if neither nests, Task 7 drops the group, says so in the manual and records a `docs/tech-debt` item.
- [ ] **Step 4: Set the 5.1 row** of the epic outline's Phases table to `In progress`.
- [ ] **Step 5: Validate and commit** the plan and the row: `./pre-commit-validation.sh`, then `git commit -m "Plan phase 5.1 and mark it in progress"`.

### Task 2: The build files

**Files:**
- Modify: `src/SqlSource/build/SqlSource.props`, `src/SqlSource/build/SqlSource.targets`
- Test: `tests/SqlSource.Tests/Package/BuildFileTests.cs`

**Interfaces:**
- Produces: `**/*.sql.json` as `AdditionalFiles` under `SqlSourceIncludeFiles`; an evaluation-time nesting `ItemGroup` in the targets.

- [ ] **Step 1: Change the tests first.**
  - `Props_SqlFilesOfTheProject_AreLeftOutWhenSqlSourceIncludeFilesIsFalse` becomes about both items: `Props.Descendants("AdditionalFiles")` has exactly two elements, `Include` `**/*.sql` and `**/*.sql.json`, both with `Exclude` `$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)`, in one `ItemGroup` whose condition is `'$(SqlSourceIncludeFiles)' != 'false'`.
  - `Targets_Root_HoldsTargetsAndTheListThatTheManifestTargetDependsOn`: the elements of the root that are not `Target` are exactly the `PropertyGroup` already checked and one `ItemGroup` with no condition holding exactly `<AdditionalFiles Update="**/*.sql.json" DependentUpon="%(Filename)" />` and `<None Update="**/*.sql.json" DependentUpon="%(Filename)" />` (rename the test to `Targets_Root_HoldsTargetsTheManifestsListAndTheNesting`).
  - `Targets_MetadataOfEverySqlFile_IsTrimmed`: `Targets.Descendants("AdditionalFiles")` now finds the nesting item too; select the one inside a `Target`.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~BuildFileTests"`.  Expected: the three fail.
- [ ] **Step 3: Add the items.**  In the props, `<AdditionalFiles Include="**/*.sql.json" Exclude="$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)" />` in the existing conditioned `ItemGroup`, and its comment says a `.sql` file's sidecar goes to the compiler too, and that a project with `SqlSourceIncludeFiles` off lists its sidecars itself.  In the targets, the nesting `ItemGroup` after the root `PropertyGroup`, with a comment: the IDEs nest from evaluated items, so it is outside every target; it is in the targets so that it reaches a sidecar a project lists itself; it is on `None` too, since an IDE may nest on either.  `SqlSourceWriteManifest` keeps listing `.sql` files alone (its `'%(Extension)' == '.sql'` condition already does).
- [ ] **Step 4: Run** the same filter.  Expected: PASS.  Then run `tests/SqlSource.Tool.Tests` `ManifestTargetTests` to see the manifest unchanged.
- [ ] **Step 5: Update `docs/architecture/msbuild-and-manifest.md`** ("What the props and targets do") for the new item and the nesting group, and `src/SqlSource/AGENTS.md`'s manifest section where it says the one `PropertyGroup` is the only element at the root.
- [ ] **Step 6: Validate and commit**: `git commit -m "Give sidecars to the compiler and nest them under their .sql files"`.

### Task 3: The harness's sidecars

**Files:**
- Create: `tests/SqlSource.Testing/Generator/Sidecars.cs`
- Test: `tests/SqlSource.Tests/Generator/SidecarHarnessTests.cs`

**Interfaces:**
- Produces, in `SqlSource.Testing.Generator`:
  - `static string Sidecars.HashOf(SqlFile sqlFile, string queryName, SqlDialect dialect = SqlDialect.Ansi)`: parses `sqlFile.Text` with `SqlFileParser.Parse(text, SqlPath.GetFileName(path), dialect)` (a `-- dialect:` marker wins, as in a build), finds the block by name, and returns `SqlQueryHash.Compute(result.Dialect, block.Segments, block.Tokens, block.Parameters)`.  Throws on a missing query: a test bug.
  - `static SidecarEntry Sidecars.Entry(string name, string hash, string engine = "postgres")`: no rows, no parameters, database `"main"`, server version `"18.0"`.
  - `static SqlFile Sidecars.Of(SqlFile sqlFile, params SidecarEntry[] entries)` and `static SqlFile Sidecars.Of(SqlFile sqlFile, int formatVersion, string toolVersion, params SidecarEntry[] entries)`: `new SqlFile(sqlFile.Path + ".json", SidecarWriter.Write(new Sidecar(...)))`, by default `SidecarFormat.Version` and `PackageVersion.Prefix`.
  - `static SqlFile Sidecars.Current(SqlFile sqlFile, SqlDialect dialect, params string[] queryNames)`: an entry with the current hash for each name, engine `SqlDialectName.Canonical` of the dialect the parse ends with.
  - `static SqlFile Sidecars.Raw(SqlFile sqlFile, string text)`: the sidecar as written, for the malformed cases.

- [ ] **Step 1: Write the failing tests** `SidecarHarnessTests`:
  - `HashOf_Query_IsTheHashOfItsSqlUnderTheDialect`: for `"-- name: Q\nSELECT @id;\n"` under `SqlDialect.PostgreSql`, `HashOf` equals `SqlQueryHash.Compute(SqlDialect.PostgreSql, ...)` of the same block parsed directly, and differs from the hash under `Ansi`.
  - `Current_File_ReadsBackWithAnEntryOfEachQuery`: `SidecarReader.Read(Current(...).Text!)` gives a `Sidecar` whose entries are the names given, with `HashOf` hashes and engine `postgres`, and the path is the `.sql` path plus `.json`.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SidecarHarnessTests"`.  Expected: compile failure.
- [ ] **Step 3: Implement `Sidecars`** with a comment that it stays within Roslyn 4.8.0, as the folder's other files do.
- [ ] **Step 4: Run** the filter, then the same tests in `tests/SqlSource.Tests.RoslynFloor`.  Expected: PASS.
- [ ] **Step 5: Update `tests/README.md`** where it lists the harness, and `tests/SqlSource.Tests/Generator/AGENTS.md`'s list of the harness's files.
- [ ] **Step 6: Validate and commit**: `git commit -m "Let the generator's tests hand it a sidecar"`.

### Task 4: Reading sidecars

**Files:**
- Modify: `src/SqlSource/Generation/Paths/SqlPath.cs`, `src/SqlSource/Generation/TrackingNames.cs`, `src/SqlSource/SqlSourceGenerator.cs`, `src/SqlSource/PackageVersion.cs`
- Create: `src/SqlSource/Generation/Sidecars/SidecarInput.cs`, `SidecarFile.cs`, `SidecarFileReader.cs`
- Test: `tests/SqlSource.Tests/Generation/Sidecars/SidecarFileReaderTests.cs`, `tests/SqlSource.Tests/Generation/Paths/SqlPathTests.cs`, `tests/SqlSource.Tests/Generator/CachingTests.cs`
- Docs: `docs/architecture/AGENTS.md` (the `Generation` row names `Sidecars/`), `src/SqlSource/AGENTS.md` (pipeline rules)

**Interfaces:**
- Produces:
  - `SqlPath.IsSidecarFile(string path) => path.EndsWith(".sql.json", OrdinalIgnoreCase)` and `string? SqlPath.SqlPathOfSidecar(string sidecarPath)`: `Normalize(path)` without its last five characters, or null.
  - `sealed record SidecarInput(AdditionalText File, string SqlPath)` with `static SidecarInput? Create(AdditionalText file, EquatableArray<string> claimedPaths)`: null unless `SqlPathOfSidecar` is claimed.  Compared by the file's reference, as `FileParseInput` is.
  - `sealed record SidecarFile(string SqlPath, string Path, SidecarReadResult Result, LocationInfo Start, LocationInfo? ErrorLocation, EquatableArray<LocationInfo> EntryLocations)`: `EntryLocations[i]` is where the key of `Result.Sidecar!.Queries[i]` is; `Start` is the empty span at offset 0.
  - `static SidecarFile SidecarFileReader.Read(SidecarInput input, CancellationToken cancellationToken)`.
  - `static string PackageVersion.PrefixOf(string version)`: the text before the first `-` or `+`.
  - `TrackingNames.SidecarInput`, `TrackingNames.SidecarFile`, `TrackingNames.Sidecars`.
  - In `SqlSourceGenerator.Initialize`, `IncrementalValueProvider<EquatableArray<SidecarFile>> sidecars`, one per `.sql` path ignoring case, sorted with `ToSorted(files, file => file.SqlPath)`.

- [ ] **Step 1: Write the failing reader tests** `SidecarFileReaderTests`:
  - `Read_CurrentFile_GivesTheSidecarAndWhereEachKeyIs`: a two-entry file's `EntryLocations` are the line and column of each `"Name"` key, quotes included.
  - `Read_FileWithAByteOrderMark_ReadsItAndKeepsThePositionsOfTheFile`: `"﻿"` + a valid file reads as `Of`, and a key's `Span.Start` is one more than without the mark.
  - `Read_TextThatCannotBeHad_IsMalformedAtTheStart`: an `AdditionalText` whose `GetText` is null gives `Result.Error` of kind `InvalidJson` and `ErrorLocation` (0,0)-(0,0).
  - `Read_MalformedFile_KeepsWhereTheErrorIs` and `Read_FileOfAnotherVersion_KeepsTheVersionAlone`.
  - In `SqlPathTests`: `IsSidecarFile` true for `Users.sql.json` and `USERS.SQL.JSON`, false for `Users.json` and `Users.sql`; `SqlPathOfSidecar("/app/Users.sql.json")` is `/app/Users.sql`.
  - `PackageVersion.PrefixOf`: `"0.1.0-dev"`, `"0.1.0-pr-42"`, `"0.1.0+abc"` and `"0.1.0"` all give `"0.1.0"` (in `tests/SqlSource.Tests/PackageVersionTests.cs`, created if absent).
- [ ] **Step 2: Run** them.  Expected: compile failure.
- [ ] **Step 3: Implement** `SqlPath`'s two members, `PackageVersion.PrefixOf`, the three records and the reader.  The reader strips one leading `'﻿'` before `SidecarReader.Read`, and adds its length back to every span it turns into a `LocationInfo` with `LocationInfo.From(path, sourceText, span)`.  A null text reads as `SidecarReadResult.Malformed(new SidecarError(SidecarErrorKind.InvalidJson, default, null))` over an empty text.  Correct `PackageVersion`'s remark that no code of the generator reads it.
- [ ] **Step 4: Wire the steps** in `SqlSourceGenerator`:
  ```csharp
  var sidecars = context.AdditionalTextsProvider
      .Where(static file => SqlPath.IsSidecarFile(file.Path))
      .Combine(claimedPaths)
      .Select(static (input, _) => SidecarInput.Create(input.Left, input.Right))
      .WithTrackingName(TrackingNames.SidecarInput)
      .Where(static input => input is not null)
      .Select(static (input, cancellationToken) => SidecarFileReader.Read(input!, cancellationToken))
      .WithTrackingName(TrackingNames.SidecarFile)
      .Collect()
      .Select(static (files, _) => ToSorted(files, static file => file.SqlPath))
      .WithTrackingName(TrackingNames.Sidecars);
  ```
  Nothing consumes `sidecars` yet, so register a temporary no-op output over it only if a test needs its steps tracked; Task 5 replaces it.  (The driver tracks a step only when it reaches an output.)
- [ ] **Step 5: Write the caching tests**, in `CachingTests`, over a fixture of their own: the two types at `OutputKind.Models` under `SqlSourceDialect` `postgres`, each `.sql` file with a current sidecar from `Sidecars.Current`.
  - `Run_SidecarEdited_ReadsOnlyThatSidecarAndParsesNoFile`: `SidecarFile` is `Modified` for the edited one and `Cached` for the other; every `FileParseInput` and `ParsedFile` is `Cached`.
  - `Run_SqlFileEdited_ReadsNoSidecar`: every `SidecarInput` and `SidecarFile` output is `Cached`.
  - `Run_SidecarAddedForAFileNoTypeClaims_ReadsNothing`: a `/app/Migrations/001_init.sql.json` gives a `SidecarInput` of null, no `SidecarFile` output for it, and `Sidecars` `Cached`.
  - `Run_SameSidecarListedTwice_ReadsItOnce`: `Sidecars` holds one element for the path (Review Focus).
  - `Run_MethodBodyEdited_TakesEveryStepFromThePreviousRun` lists the three new steps; `AllReasons`'s exception for steps that give nothing extends to `SidecarInput` and `SidecarFile`, with its comment.
- [ ] **Step 6: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~CachingTests|FullyQualifiedName~SidecarFileReaderTests|FullyQualifiedName~SqlPathTests"` and the RoslynFloor project.  Expected: PASS.
- [ ] **Step 7: Rules.**  `src/SqlSource/AGENTS.md`'s pipeline: "A sidecar is read only when its `.sql` file is claimed, and no step after `SidecarFile` holds its text."  `docs/architecture/AGENTS.md`: the `Generation` row becomes "with `Paths/` and `Sidecars/`".
- [ ] **Step 8: Validate and commit**: `git commit -m "Read the sidecar of each claimed file once"`.

### Task 5: The output takes effect, and the checks

**Files:**
- Create: `src/SqlSource/Generation/Sidecars/QueryNeed.cs`, `FileNeeds.cs`, `SidecarNeeds.cs`, `SidecarCheckInput.cs`, `SidecarCheck.cs`, `SidecarChecker.cs`
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/Diagnostics/DiagnosticInfo.cs` (its descriptor is of either class), `src/SqlSource/AnalyzerReleases.Unshipped.md`, `src/SqlSource/Generation/TrackingNames.cs`, `src/SqlSource/SqlSourceGenerator.cs`, `src/SqlSource/AGENTS.md`, `docs/manual/diagnostics.md`
- Test: `tests/SqlSource.Tests/Generator/SidecarTests.cs`, `CachingTests.cs`, `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs`, `ToolDiagnosticsTests.cs`, `DiagnosticsDocumentTests.cs`

**Interfaces:**
- Consumes: Task 4's `SidecarFile`, `sidecars`, `PackageVersion.PrefixOf`; Task 3's `Sidecars`.
- Produces:
  - `sealed record QueryNeed(string Name, OutputKind Output)`; `sealed record FileNeeds(string NormalizedPath, EquatableArray<QueryNeed> Queries)`, its queries in file order, each `Output` the greatest over every claiming type.
  - `static EquatableArray<FileNeeds> SidecarNeeds.OfType(TypeQueries type, SettingsLevel property)`: for each of `type.Files` with its `type.FileSettings`, the queries whose `QuerySettings.Resolve(query.Markers, type.Type.Type.Settings, metadata, property).Output` is not `Sql`; a file with none is left out.  A type with diagnostics counts too, since the tool plans its files.
  - `static EquatableArray<FileNeeds> SidecarNeeds.Merge(ImmutableArray<FileNeeds> files)`: by path ignoring case, the union of names with the greatest output, sorted by `SqlPath.Comparer`.
  - `sealed record SidecarCheckInput(ParsedSqlFile File, SidecarFile? Sidecar, FileNeeds? Needs)` with `static SidecarCheckInput Gather(ParsedSqlFile file, EquatableArray<FileNeeds> needs, EquatableArray<SidecarFile> sidecars)` by `SqlPath.IndexOf`.
  - `sealed record SidecarCheck(string NormalizedPath, EquatableArray<DiagnosticInfo> Diagnostics, EquatableArray<SidecarEntry> UsableEntries)`.
  - `static SidecarCheck SidecarChecker.Check(SidecarCheckInput input)`.
  - `TrackingNames.TypeNeeds`, `FileNeeds`, `SidecarCheckInput`, `SidecarCheck`.
  - Descriptors, in this order in `SqlDiagnostics.All` after `InputModelWithoutParameters`: `SidecarMalformed` (237), `SidecarFormatTooNew` (238), `SidecarFormatTooOld` (239), `SidecarToolVersionDiffers` (240), `QueryHasNoEntry` (241), `EntryEngineDiffers` (242), `EngineHasNoTypeMap` (243), `EntryIsStale` (244), `OrphanEntry` (245).

**The checks.**  `SidecarChecker.Check` returns empty at once for a file with no sidecar and no needs.  Otherwise, with `needing` the file's queries whose names are in `Needs`, in file order:

1. **Parse errors** (`File.Errors.Count > 0`): only steps 4 and 5 run, then stop.
2. **Not describable**: when `needing` is not empty and `!SqlDescribableDialects.Contains(File.Dialect)`, no query is checked against the sidecar (Task 6 reports `SQLSRC209` here).
3. **No type map**: when `needing` is not empty and the dialect is describable but not `SqlDialect.PostgreSql` (a private `HasTypeMap`, whose comment says phase 6 adds `mssql`), one `SQLSRC243` at `needing[0].NameLocation` naming `SqlDialectName.Canonical(dialect)`, and no query gets 241, 242 or 244.
4. **File level**, when the sidecar exists: malformed is `SQLSRC237` at `ErrorLocation` with the reason (below); `FormatVersion > SidecarFormat.Version` is `SQLSRC238` at `Start` with both versions; lower is `SQLSRC239` at `Start` with both; otherwise a `PrefixOf(toolVersion)` that is not `PackageVersion.Prefix` is the warning `SQLSRC240` at `Start` with both versions, and the file is used.  After 237, 238 or 239 no entry is usable and no query or orphan check runs.
5. **Per query** of `needing`, unless step 2 or 3 stopped the file (Task 6 adds: or the query has a token without a default): no sidecar or `Sidecar.Find(name)` null is `SQLSRC241`; `entry.Engine != SqlDialectName.Canonical(dialect)` is `SQLSRC242` (entry's engine, file's dialect); `entry.Hash != SqlQueryHash.Compute(dialect, query.Segments, query.Tokens, query.Parameters)` is `SQLSRC244`; otherwise the entry is usable.  All at the query's `NameLocation`.  The hash is computed here alone.
6. **Orphans**, when the sidecar was read: each entry whose name is not in `needing`, compared ordinally, is the warning `SQLSRC245` at its `EntryLocations` position.  Not for a file with parse errors.

`SQLSRC237`'s reason, by `SidecarErrorKind`: `InvalidJson` "it is not one JSON object"; `MissingKey` "'{arg}' is missing"; `WrongType` "'{arg}' has a value of the wrong type"; `DuplicateKey` "'{arg}' is written twice"; `OrdinalMismatch` "the ordinal {arg} is not the position of its element"; `ColumnsWithoutRows` "it has columns for a query without rows"; `UnknownResultKind` "'{arg}' is not a result kind".  Each message is the problem, then the fix, in the house style; the fix of 237, 239, 241, 242 and 244 is to run `sqlsource describe`, of 238 to update the `SqlSource` package, of 240 to bring the tool and the package to one version and run `sqlsource describe`, of 243 to set the output to `sql`, of 245 to run `sqlsource describe`, which removes it.

- [ ] **Step 1: Change the descriptor tests.**
  - `SqlDiagnosticsTests.All_EveryDescriptor_IsAnErrorThatCannotBeConfigured` becomes `All_EveryDescriptor_IsAnErrorThatCannotBeConfiguredButTheListedWarnings`: `Configurable = ["SQLSRC240", "SQLSRC245"]` are `Warning`, enabled, without `NotConfigurable`; every other is `Error` with it.
  - `ToolDiagnosticsTests.All_Ids_ComeAfterEveryIdOfTheGenerator` becomes `All_Ids_AreUniqueAcrossBothClasses`.
  - `DiagnosticsDocumentTests`: the expected sections are `SqlDiagnostics.All` and `ToolDiagnostics.All` together, ordered by id.
  - A message test for each new descriptor, as `ToolDiagnosticsTests` has for its own.
- [ ] **Step 2: Write the failing `SidecarTests`** (`OutputKind.Models` and the dialect `postgres` unless the case says otherwise; positions asserted as the harness formats them):
  - One test per row: `Run_MalformedSidecar_IsSqlsrc237AtTheError`, `Run_SidecarOfANewerFormat_IsSqlsrc238AtTheStart`, `..._OlderFormat_IsSqlsrc239...`, `Run_SidecarOfAnotherToolVersion_IsAWarningAndTheEntryIsUsed` (240), `Run_QueryWithoutAnEntry_IsSqlsrc241AtTheQuery` (sidecar absent, and sidecar without the name), `Run_EntryOfAnotherEngine_IsSqlsrc242` (`mssql`, and `oracle` from Review Focus), `Run_MssqlFileAtModels_IsOneSqlsrc243AndNoSqlsrc241`, `Run_StaleEntry_IsSqlsrc244`, `Run_OrphanEntry_IsAWarningAtItsKey` (245).  Each asserts the id, the severity, the position, and, from `SidecarCheck`'s tracked output, whether the query's entry is usable.
  - `Run_FileWithParseErrors_GetsNoOrphanAndNoQueryError` (a malformed sidecar there still gives 237).
  - `Run_SidecarOfAFileNoTypeClaims_IsNotRead`, `Run_SidecarWithoutItsSqlFile_IsIgnored`, `Run_FileAtSqlWithoutASidecar_GetsNothing`, `Run_MalformedSidecarOfAFileAtSql_IsSqlsrc237` (Review Focus), `Run_EmptySidecar_IsSqlsrc237AtTheStart` (Review Focus), `Run_EntryNamedByAnotherCase_IsMissingAndAnOrphan` (Review Focus).
  - `Run_QueryAtModelsAndQueryAtSql_OnlyTheFirstNeedsAnEntry` (a `-- output: sql` marker on the second; its entry is an orphan).
  - `Run_TwoTypesClaimingOneFile_ReportOnceAndNeedTheUnion` (one at `Sql`, one at `Models`: one 241, and the entry of the other's query is not an orphan).
  - `Run_ToolVersionWithASuffix_MatchesThePrefix` (`PackageVersion.Prefix + "-dev"`: no 240).
  - `Run_SidecarWithAByteOrderMark_IsRead`, `Run_TwoSidecarsThatDifferOnlyByCase_ReadTheFirst`.
  - `Run_EveryCheck_GivesNoUsableEntryThatFailedOne`: one file holding a current, a stale, a missing and an other-engine query, plus a malformed file and a newer-format file; the union of `UsableEntries` over every `SidecarCheck` output is exactly the current entry (the anti-goal).
- [ ] **Step 3: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SidecarTests|FullyQualifiedName~Diagnostics"`.  Expected: compile failure.
- [ ] **Step 4: Implement** the descriptors (full `new DiagnosticDescriptor(...)`, literal ids; 240 and 245 each with a comment saying why it is a configurable warning), their rows in `AnalyzerReleases.Unshipped.md` (240 and 245 `Warning`), the records, `SidecarNeeds`, `SidecarChecker`, and the steps:
  ```csharp
  var typeQueries = /* the existing typeFiles ... Select(SelectFiles) ... TypeQueries step */;
  var projectLevel = projectSettings.Select(static (settings, _) => settings.Level);
  var fileNeeds = typeQueries.Combine(projectLevel)
      .Select(static (input, _) => SidecarNeeds.OfType(input.Left, input.Right))
      .WithTrackingName(TrackingNames.TypeNeeds)
      .SelectMany(static (files, _) => files)
      .Collect()
      .Select(static (files, _) => SidecarNeeds.Merge(files))
      .WithTrackingName(TrackingNames.FileNeeds);
  var sidecarChecks = parsedFiles.SelectMany(static (files, _) => files)
      .Combine(fileNeeds.Combine(sidecars))
      .Select(static (input, _) => SidecarCheckInput.Gather(input.Left, input.Right.Left, input.Right.Right))
      .WithTrackingName(TrackingNames.SidecarCheckInput)
      .Select(static (input, _) => SidecarChecker.Check(input))
      .WithTrackingName(TrackingNames.SidecarCheck);
  ```
  and a `RegisterSourceOutput(sidecarChecks, ...)` that reports each check's diagnostics, replacing Task 4's temporary output.  `TypeEmitter` is unchanged.
- [ ] **Step 5: Caching tests** on Task 4's fixture: `Run_SqlFileEdited_ChecksOnlyThatFile` (`SidecarCheck` `Modified` for it, `Cached` for the other), `Run_SidecarEdited_ChecksOnlyItsFile`, `Run_MethodBodyEdited_...` gains the four steps, `Run_AttributeEditedWithoutChangingTheOutput_ChecksNothing` (a `ModelNamespace` added on `UserQueries`: `FileNeeds` `Unchanged` or `Cached`, every `SidecarCheck` `Cached`), `Run_ModelSuffixPropertyChanged_ChecksNothing` (`SqlSourceOutputModelSuffix`), and `Run_ProjectAtSql_ComputesNoHash`: at `Sql` with no sidecar, `FileNeeds` is empty and every `SidecarCheck` output has no diagnostic.
- [ ] **Step 6: Document the ids** in `docs/manual/diagnostics.md`, which `DiagnosticsDocumentTests` holds in this commit: a new index table after the PostgreSQL one, "The sidecar of a `.sql` file", rows 237 to 245; the paragraph under the index gives its range; a `## SQLSRCnnn` section for each, its first bold line the title, saying what it means and how to fix it, and for 240 and 245 that they are warnings a project can list in `NoWarn` or `WarningsNotAsErrors`.  The page's opening line that nothing can be turned off says which two can.
- [ ] **Step 7: The rule.**  `src/SqlSource/AGENTS.md`'s Diagnostics: "Every diagnostic is an error tagged `NotConfigurable` unless its descriptor says why it is a warning a project may configure; `SqlDiagnosticsTests` lists those by id."  Its id range line: `SQLSRC200` to `SQLSRC245` are taken, and phase 5's later sub-phases take the ids their specs assign.  `SqlDiagnostics`' summary says the same.
- [ ] **Step 8: Run** the whole `tests/SqlSource.Tests` and `tests/SqlSource.Tests.RoslynFloor`.  Expected: PASS.  A failure of a test that does not pass `null` for the output is a bug of phase 4: stop and report it.
- [ ] **Step 9: Validate and commit**: `git commit -m "Check each claimed file's sidecar against what its types need"`.

### Task 6: `SQLSRC209` and `SQLSRC210` in the generator

**Files:**
- Modify: `src/SqlSource/Generation/Sidecars/SidecarChecker.cs`, `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `docs/manual/diagnostics.md`
- Test: `tests/SqlSource.Tests/Generator/SidecarTests.cs`, `SettingLevelTests.cs`, `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`, `tests/SqlSource.Tool.Tests/Planning/GeneratorParityTests.cs`, and every tool test that asserts the two messages (`grep -rn "described with a sample\|needs a dialect that can be described" tests/`)

**Interfaces:**
- Consumes: Task 5's checker steps 2 and 5.

- [ ] **Step 1: Write the failing tests.**
  - `SidecarTests.Run_AnsiFileAtModels_IsOneSqlsrc209AtItsFirstQueryThatNeedsAnEntry`: two queries, the first at `Sql` by marker; one 209 at the second's name with `models` and `ansi`, and no 241.  The type's constants are generated.
  - `SidecarTests.Run_TokenWithoutADefault_IsSqlsrc210AtTheQueryAndItIsNotChecked`: `{{where}}` at `Models` under `postgres` with no sidecar: one 210 at the query, no 241; a token with a default gets 241.  Also in an `ansi` file: 209 and 210, as the tool gives both.
  - `ToolDiagnosticsTests`: the two message tests with the new wording.
  - `GeneratorParityTests.Errors_OfTheSameSourcesAndFiles_AreTheGeneratorsSqlsrc209And210`: cases for an `ansi` file at `Models`, an `ansi` file whose first query is at `Sql`, a token without a default under `postgres`, and both in one file.  Each runs the generator through the harness and `RunPlanner.Plan` over the `TestProject`'s manifest, keeps the 209 and 210 diagnostics of each, and compares id, path, start line and column, and message arguments.
  - `SettingLevelTests`: each test that passes `null` asserts the diagnostics the default `CodeGen` gives: one 209 at `Q`, with `codegen` (or the output the case sets) and `ansi`.
- [ ] **Step 2: Run** them.  Expected: FAIL.
- [ ] **Step 3: Implement.**  In step 2 of the checks, one `DiagnosticInfo.Create(ToolDiagnostics.OutputNeedsDescribableDialect, needing[0].NameLocation, SettingValue.NameOf(needs output of needing[0]), SqlDialectName.Canonical(dialect))`.  Before step 5, for each query of `needing` in file order, one `TokenHasNoDefault` per token whose `Default` is null, with the token's name and `SettingValue.NameOf` of its output; such a query is skipped in step 5 and its entry is not an orphan.  Reword the two messages to read right from the build and the tool: 209 states the output and the dialect and the fix; 210 "The token '{0}' has no default, which a query whose output is '{1}' needs.  Give it a default, or set the output to 'sql'."  Correct `ToolDiagnostics`' summary: the generator reports 209 and 210 too.  Rewrite the 209 and 210 sections of `diagnostics.md`: a build reports them as well as the tool.
- [ ] **Step 4: Run** `tests/SqlSource.Tests`, `tests/SqlSource.Tests.RoslynFloor` and `tests/SqlSource.Tool.Tests`.  Expected: PASS.
- [ ] **Step 5: Validate and commit**: `git commit -m "Report an output the file's dialect cannot give, and a token without a default, in the build"`.

### Task 7: The documents

**Files:** the spec's Documentation list: `docs/manual/diagnostics.md`, `settings.md`, `tool.md`, `concepts.md`, `README.md` (the manual's entry page, its "What works today" `Output` row), the root `README.md`, `docs/architecture/generator.md`, `docs/architecture/msbuild-and-manifest.md` if Task 2 left it short, `docs/tech-debt/TD-0025`, `TD-0026`, `TD-0031`, `TD-0032`, `TD-0033` and `docs/tech-debt/README.md`, the epic outline.

- [ ] **Step 1: The manual.**  `settings.md`'s Output row and its status: it decides whether a query needs a sidecar entry, and models come in a later release.  The entry page's "What works today" likewise.  `tool.md` and `concepts.md`: rewrite the sentences that say nothing in a build reads a sidecar, and "Commit the sidecars with the project" is now a build requirement.  The sidecar page the phase map names gets the VS Code line, `"explorer.fileNesting.patterns": { "*.sql": "${capture}.sql.json" }`, and the spike's IDE result.  Run the two `git grep` finders of `docs/manual/AGENTS.md` and the internals check over each changed page.
- [ ] **Step 2: The README.**  Step 5's "Nothing in a build reads the sidecars yet" becomes what a build now reports; a project with `SqlSourceIncludeFiles` off lists `**/*.sql.json` itself.  Nothing else of phase 5 moves.
- [ ] **Step 3: Architecture.**  `generator.md`: the pipeline's diagram and text gain the sidecar steps, the needs and the checks, written from the code; the Snapshot section loses "No step of the generator reads a sidecar yet"; the "Reading sidecars" placeholder is replaced by a section on pairing, reading and checking; "Caching tests" names the new cases.
- [ ] **Step 4: Tech debt.**  Re-read `TD-0032` and say what the generator now does with a file that is not UTF-8; re-read `TD-0025`, `TD-0026`, `TD-0031` and `TD-0033` and turn each "from phase 5" or "will" into what a build now reports.  Keep their rows in `README.md` current.
- [ ] **Step 5: The epic outline.**  The 5.1 row `Done`; the sidecar section says the engine with no type map is `SQLSRC243`, once for the file at its first query that needs an entry; the phase 5 Technical notes and the Diagnostics section agree with what was built.
- [ ] **Step 6: Validate and commit**: `git commit -m "Document sidecars in the build"`.

### Task 8: Measure, review and open the pull request

- [ ] **Step 1: Measure.**  In a scratch console outside the repository, a `Release` build of the generator run by `CSharpGeneratorDriver` over 200 `.sql` files of 5 queries each: at `Sql` without sidecars on `main` and on the branch, and at `Models` under `postgres` with current sidecars on the branch.  A `Stopwatch` over 50 full runs and over 50 runs after one `.sql` edit, and `GC.GetAllocatedBytesForCurrentThread` for each.  The numbers go in the pull request's description.
- [ ] **Step 2: Review** the whole branch with superpowers:requesting-code-review and fix every valid finding, minor ones included, or ask the owner whether to record one as tech debt.
- [ ] **Step 3: Open the pull request** by the root `AGENTS.md`'s rules: `git fetch --tags origin` and the version check (no `v*` tag: nothing to compare), 150 files or fewer, the branch up to date with `main`, `./pre-commit-validation.sh` on the exact tree, the CodeRabbit manager's clearance before the push.  The description holds the spike's result and the measurements.  Turn on Auto-fix.
