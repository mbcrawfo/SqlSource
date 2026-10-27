# Settings files, layer 4: the tool reads the chains - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `sqlsource describe` reads the settings files the compiler is given, resolves each claimed `.sql` file's dialect, output and database from the chain of its folder as the generator does, and reports the problems of every settings file a chain reaches at the file, line and column the build gives.

**Architecture:** `RunPlanner` builds one `SettingsFileSet` per project from the manifest's `Settings=` lines with layer 3's `ListedSettingsFile.Read(..., SettingsFileSide.Tool, ...)` and `SettingsFileSet.Of`, each file read once per run.  The parse takes the file's chain through `FileParseInput.Resolve`, and the output and the database take `FileLevel.Resolve`'s level in place of the metadata's.  Reachability is `SettingsFileSet.Reached` over the manifest's `.sql` paths and its `Compile=` paths, less those under the build's folders, which the manifest now carries as `Property.SqlSourceBuildFolders` and which go through one shared rule, `BuildFolders.ReachingPaths`, that the generator calls too.

**Tech Stack:** C# (`net8.0` tool; the shared code in the `netstandard2.0` generator, Roslyn 4.8.0 floor), MSBuild targets, xUnit v3, Shouldly.

**Spec:** [`docs/superpowers/specs/2026-10-25-settings-files-design.md`](../specs/2026-10-25-settings-files-design.md), "The layers" item 4, "Who reports what", "What changes in the generator and the tool".  Read the root `AGENTS.md`, `src/SqlSource.Tool/AGENTS.md`, `src/SqlSource/AGENTS.md`, `tests/AGENTS.md` and `docs/architecture/tool.md` ("The plan") before any task.

## Where the merged code differs from the spec

- The spec says the tool builds the chains "from the manifest's `Settings=` lines, the `.sql` files' paths and the project's path".  The project's path is not needed: a chain is found from a file's own folder (`SettingsFileSet.ChainOf`), and the manifest lists every file the build found above every `.sql` and C# file.
- Layer 3 added the rule that a compile path under the build's folders reaches nothing (owner, 2026-10-26: "F1, fix with a").  The manifest has no line for those folders yet, so this layer adds `Property.SqlSourceBuildFolders` and puts `SqlSourceFindBuildFolders` in `SqlSourceManifestDependsOn` (a new name under `Property.` is an addition: the version stays `1`).
- The generator's private `NormalizedCompilePaths` becomes `BuildFolders.ReachingPaths`, so the tool calls the rule rather than copying it (AGENTS.md: "copies no rule").
- Owner's decision of 2026-10-26: a settings file is checked only when the chain of the folder of some `.sql` file (`File=`) or C# file (`Compile=`) reaches it, whether or not the `.sql` file is claimed.  The manifest's `Compile=` paths are full paths, and post-initialization trees never appear in it, so no extra filter is needed beyond `ReachingPaths`.
- **Builder's calls of this layer** (say so in the PR):
  - A settings file reached in two projects of one run is read once and reported once, under the first project that reaches it, as a `.sql` file claimed by two projects is planned once.
  - In a project's errors the settings files' come last: `SQLSRC208`, then `SQLSRC011`, then `SQLSRC014`, then `SQLSRC015` to `SQLSRC020` file by file in the order of `SettingsFileSet.Files` (folders by `SqlPath.Comparer`), each file's in the order of their positions.
  - A `Settings=` path whose last segment is not `sqlsource.json` exactly is ignored (`ListedSettingsFile.IsSettingsFile`), as the generator ignores it.  A listed file that cannot be read is read as empty text: one `SQLSRC015` at the file, as in the generator.

## Global Constraints

- Shared code goes in `src/SqlSource/` (`netstandard2.0`, Roslyn 4.8.0); the tool never copies a rule of the generator's.
- `RunPlanner` reports nothing: errors are data in `RunPlanResult.Errors`, in the order they are to be reported.
- The tool reports a settings file's error when `SettingsFileError.IsAboutTheFile`, or when its `Setting` is `dialect`, `output` or `database` (`SettingsFileSide.Tool`); never `typeOverrides`, `columnTypes` or another setting's value.
- The MSBuild readers (`ManifestOptions`, `ProjectSettings`, `FileMetadata`, `DialectSetting`) stay, below the settings files, until layer 6.
- Paths go through `SqlPath.Normalize` and compare with `SqlPath.Comparer`.
- Every commit passes `./pre-commit-validation.sh`; run `./format.sh` before it.  AGENTS.md files are updated in the commit that renames or moves what they name.
- Every MSBuild property of the package starts with `SqlSource` (`BuildFileTests` checks it).

## Review Focus

1. **One settings file shared by two projects of a solution**, with an error: reported once, at its line and column.  Test in Task 3.
2. **A broken `sqlsource.json` above the root file of every chain**: never reported, and never changes a value.  Test in Task 3 and Task 5.
3. **A settings file whose text is not JSON between a `.sql` file and a farther file**: the chain passes through it, so the farther file is checked and applies.  Test in Task 3 and Task 4.
4. **A project folder or `obj` path holding `%`, `;`, `#` or `|`** (the `OddPaths` fixture): the manifest's build folders decode to the real folders.  Test in Task 1.
5. **A `.sql` file whose header marker sets the dialect or database while its chain sets another**: the marker wins, as in the build.  Test in Task 4.

---

### Task 1: The manifest carries the build's folders

**Files:**
- Modify: `src/SqlSource/build/SqlSource.targets` (the `SqlSourceManifestDependsOn` group and the `SqlSourceWriteManifest` lines)
- Test: `tests/SqlSource.Tests/Package/BuildFileTests.cs`, `tests/SqlSource.Tool.Tests/Projects/ManifestTargetTests.cs`
- Docs: `docs/architecture/msbuild-and-manifest.md` (the format, the manifest's targets), `src/SqlSource/AGENTS.md` (only if a rule there names the manifest's `Property.` lines or `SqlSourceManifestDependsOn`'s value)

**Interfaces:**
- Produces: a manifest line `Property.SqlSourceBuildFolders=<value of the property>`, read by `ManifestReader` into `ProjectManifest.Properties["SqlSourceBuildFolders"]`, so `BuildFolders.Read(ManifestOptions.ForProject(manifest))` gives the folders.

- [ ] **Step 1: Write the failing tests**
  - `BuildFileTests.Targets_Root_HoldsTargetsTheManifestsListTheNestingAndTheSettingsFiles`: the second line's value becomes `"SqlSourceTrimProperties;SqlSourceTrimMetadataOfFiles;" + SearchTarget + ";" + BuildFoldersTarget`.
  - `BuildFileTests.Targets_ManifestTarget_WritesEverySettingOfTheProjectAndOfEachFile`: the `Property.` lines are `properties.Concat(BuildFolders)`, each `$"Property.{name}=$([MSBuild]::Escape($({name})))"` (the `File.` lines unchanged, without it).
  - `ManifestTargetTests.WriteManifest_ProjectWithOneFramework_HoldsTheBuildsFolders`: run the target on the `Single` fixture (`Plain` imports no package targets); `BuildFolders.Read(ManifestOptions.ForProject(manifest)).Folders` contains `SqlPath.Normalize` of `<project folder>/obj/` and of `<project folder>/bin/`.
  - `ManifestTargetTests.WriteManifest_BuildFoldersOfAnOddPath_DecodeToTheRealFolders`: the same on the `OddPaths` fixture copied into a folder whose name holds `;`, `=`, `%41`, `'` and `é`; the decoded `obj/` folder equals the real one.  Any existing test that asserts the exact set of `Property.` lines a real manifest holds adds `SqlSourceBuildFolders` to it.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~BuildFileTests"` and `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~ManifestTargetTests"`
Expected: the new and changed tests FAIL.

- [ ] **Step 3: Change the targets**
  - Append `;SqlSourceFindBuildFolders` to the first `SqlSourceManifestDependsOn` line, and say in the group's comment why: the property is set by a target, so the manifest's target must run it.
  - Add `<SqlSourceManifestLine Include="Property.SqlSourceBuildFolders=$([MSBuild]::Escape($(SqlSourceBuildFolders)))" />` after `Property.SqlSourceNestedClass`.  The property holds `%25`, `%7C`, `%23` and `%3B` as text; `Escape` and `WriteLinesToFile` keep it as it is, and `BuildFolders.Of` decodes it.

- [ ] **Step 4: Run them to see them pass**, then the whole of both test projects' `Package/` and `Projects/` folders.

- [ ] **Step 5: Update the docs** named under Files: the manifest's format lists the new line and says it is no setting (the tool reads it only for which compile paths reach a settings file); the manifest's dependencies list `SqlSourceFindBuildFolders`.

- [ ] **Step 6: Format, validate, commit**

```bash
./format.sh && ./pre-commit-validation.sh
git commit -am "Give the manifest the build's folders, which the tool leaves out of the reach"
```

### Task 2: One rule for the compile paths that reach a settings file

**Files:**
- Modify: `src/SqlSource/Generation/Chains/BuildFolders.cs`, `src/SqlSource/SqlSourceGenerator.cs` (`NormalizedCompilePaths` and its caller in `ReportSettingsFiles`)
- Test: `tests/SqlSource.Tests/Generation/Chains/BuildFoldersTests.cs`
- Docs: `src/SqlSource/AGENTS.md` and `docs/architecture/generator.md` where they name `NormalizedCompilePaths` or say which compile paths reach nothing

**Interfaces:**
- Produces: `public IEnumerable<string> ReachingPaths(IEnumerable<string> compilePaths)` on `BuildFolders`: each path that `SqlPath.IsRooted`, that normalises, and whose normalised path no folder `Contains`, normalised, in the order given.  Lazy (`yield`), as the generator's method is.

- [ ] **Step 1: Write the failing tests** in `BuildFoldersTests`:
  - `ReachingPaths_PathsUnderAFolder_AreLeftOut`: folders `Of("|/repo/App/obj/|")`, paths `["/repo/App/A.cs", "/repo/App/obj/G.cs", "/repo/App/objects/B.cs"]` give `["/repo/App/A.cs", "/repo/App/objects/B.cs"]` as `SqlPath.Normalize` gives them.
  - `ReachingPaths_PathThatIsNotRooted_IsLeftOut`: `BuildFolders.None.ReachingPaths(["Gen/Attribute.g.cs", "/repo/A.cs"])` gives only the second, normalised.
  - `ReachingPaths_PathsWithBackslashes_AreNormalised`: on `None`, `[@"C:\repo\A.cs"]` gives `[SqlPath.Normalize(@"C:\repo\A.cs")]`.

- [ ] **Step 2: Run to see them fail** (`dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~BuildFoldersTests"`: does not compile).

- [ ] **Step 3: Move the body** of `SqlSourceGenerator.NormalizedCompilePaths` into `BuildFolders.ReachingPaths`, with the rule's doc comment (rooted, normalised, under no folder).  The generator calls `input.Right.Right.ReachingPaths(input.Right.Left)` and keeps, at the call, its comment on why post-initialization trees and the SDK's `obj` items reach nothing.  Delete the private method.

- [ ] **Step 4: Run** `BuildFoldersTests` and `tests/SqlSource.Tests/Generator/SettingsFileTests.cs` and `CachingSettingsFileTests.cs`: PASS, unchanged.

- [ ] **Step 5: Update the docs** under Files, then format, validate, commit: `"Share the rule of which compile paths reach a settings file"`.

### Task 3: The planner reads the settings files and reports the ones a chain reaches

**Files:**
- Create: `src/SqlSource.Tool/Planning/RunSettingsFiles.cs`
- Modify: `src/SqlSource.Tool/Planning/RunPlanner.cs`, `src/SqlSource.Tool/Planning/SqlFileText.cs` (its summary: a `.sql` file or a settings file on the disk), `tests/SqlSource.Tool.Testing/TestProject.cs`
- Test: `tests/SqlSource.Tool.Tests/Planning/RunPlannerErrorTests.cs` (or a new `tests/SqlSource.Tool.Tests/Planning/RunPlannerSettingsFileTests.cs` if that file would pass ~600 lines), `tests/SqlSource.Tool.Tests/TestProjectTests.cs`

**Interfaces:**
- Consumes: `BuildFolders.ReachingPaths` (Task 2), `Property.SqlSourceBuildFolders` (Task 1); layer 3's `ListedSettingsFile.IsSettingsFile`, `ListedSettingsFile.Read(AdditionalText, SettingsFileSide, CancellationToken)`, `SettingsFileSet.Of`, `SettingsFileSet.Reached`, `ToolDiagnostic.From(DiagnosticInfo)`.
- Produces:
  - `internal sealed class RunSettingsFiles` (one per run, made in `RunPlanner.Plan`):
    - `public SettingsFileSet Of(ProjectManifest manifest, CancellationToken cancellationToken)`: the set of the manifest's `SettingsFiles` that `IsSettingsFile` takes, each read with `ListedSettingsFile.Read(new SqlFileText(path), SettingsFileSide.Tool, ...)` once per run (cache by path, `StringComparer.Ordinal`); `SettingsFileSet.Empty` when none.
    - `public void AddProblems(SettingsFileSet set, IEnumerable<string> reachingPaths, ImmutableArray<ToolDiagnostic>.Builder errors)`: for each folder of `set.Reached(reachingPaths)` not reported before in this run (`HashSet<string>` with `SqlPath.Comparer`), the file's `Diagnostics` through `ToolDiagnostic.From`, in the order of the set's files.
  - `RunPlanner.ReadProject` takes the run's `RunSettingsFiles`, and its `ClaimedFile` gains `SettingsFileSet SettingsFiles` (used by Task 4).  After `AddInvalid` it calls `AddProblems(set, listed.Paths.Concat(BuildFolders.Read(options).ReachingPaths(manifest.CompileFiles)), errors)`.
  - `TestProject.AddSettings(string relativePath, string json) -> string` writes the file and lists it; `TestProject.ListSettings(string fullPath)` lists a path without writing it.  `ManifestText` writes one `Settings=<path>` line each after the `File=` lines and before the `Compile=` lines.

- [ ] **Step 1: Write the failing tests.** Each builds a `TestProject` with a type at the root (`Plans.Type`) and runs `RunPlanner.Plan`.  Expected errors are compared as `(Id, Path, Position, message)` (the helpers of the error tests).
  - `TestProjectTests`: a project with a `.sql` file, a settings file and a source has its `Settings=` line between the `File=` and `Compile=` lines, and `Manifest().SettingsFiles` is `[path]`.
  - `Plan_SettingsFileWithErrorsOfTheFile_ReportsEachAtItsLineAndColumn`: `App/sqlsource.json` = `"{\n  \"Dialect\": \"postgres\",\n  \"output\": 1,\n  \"output\": \"sql\",\n}"` gives `SQLSRC016` at (1,2), `SQLSRC018` at (2,12), `SQLSRC017` at (3,2), positions zero-based, path the file's full path.
  - `Plan_SettingsFileThatIsNotJson_IsSqlsrc015`: text `"{ \"output\": "` gives one `SQLSRC015`.
  - `Plan_InvalidValuesOfSettingsTheToolReads_AreReported`: `dialect` `"pgsql"`, `output` `"model"`, `database` `"a b"` give three `SQLSRC019`.
  - `Plan_InvalidValuesOfSettingsTheToolDoesNotRead_AreNotReported`: `typeOverrides` `{"[dbo].[Email]": "X"}`, `columnTypes` `{"id": "X"}`, `inputModelSuffix` `"-"`, a repeated `typeOverrides` key: no error.
  - `Plan_SettingsFileAboveTheRootFile_IsNeverReported`: `sqlsource.json` at the temp folder's root (above `App/`) is `"{ nope"`, `App/sqlsource.json` is `{"root": true}`: no error.
  - `Plan_SettingsFileReachedOnlyByACSharpFile_IsReported`: `App/Code/sqlsource.json` with an unknown key beside `App/Code/Repository.cs`, no `.sql` under `Code/`: one `SQLSRC016`.
  - `Plan_SettingsFileReachedOnlyByAnUnclaimedSqlFile_IsReported`: the type claims `Queries`; `App/Other/X.sql` and `App/Other/sqlsource.json` with an unknown key: one `SQLSRC016`.
  - `Plan_SettingsFileThatNoFileReaches_IsNeitherReadNorReported`: `App/Unused/sqlsource.json` with errors, listed, nothing in `Unused/`: no error.
  - `Plan_ChainThroughAFileThatIsNotJson_ReachesTheFartherFile`: `App/Queries/sqlsource.json` not JSON, `App/sqlsource.json` with an unknown key, `.sql` under `Queries/`: `SQLSRC015` and `SQLSRC016`, in folder order (`App/` first).
  - `Plan_CompileFileUnderABuildFolder_ReachesNothing`: `Properties["SqlSourceBuildFolders"] = "|" + PathOf("obj") + Path.DirectorySeparatorChar`; `App/obj/Gen.cs` compiled; `App/obj/sqlsource.json` with an unknown key: no error.
  - `Plan_SettingsFileOfTwoProjects_IsReportedOnce`: two projects, `App/` and `Lib/`, each listing the shared `<root>/sqlsource.json` (unknown key) with a `.sql` file in each: one `SQLSRC016`.
  - `Plan_SettingsFileNamedInAnotherCase_IsNotRead`: `App/SqlSource.json` listed, with errors: no error.
  - `Plan_SettingsFileThatIsGone_IsSqlsrc015`: `ListSettings` of a path that does not exist, beside a `.sql` file: one `SQLSRC015` at that path.
  - `Plan_ErrorsOfAProject_AreInvalidValuesThenSettingsFiles`: property `SqlSourceOutput=nothing` and two reached settings files with an error each, `App/b/` and `App/A/`: `SQLSRC014`, then `App/A`'s, then `App/b`'s.

- [ ] **Step 2: Run to see them fail.** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~RunPlanner|FullyQualifiedName~TestProjectTests"`: compile errors, then failures.

- [ ] **Step 3: Implement** `TestProject`'s additions, `RunSettingsFiles`, and the calls in `RunPlanner` named under Interfaces.  Check `cancellationToken` before reading each file.  Update `RunPlanner`'s remarks: the settings files are read by the generator's `ListedSettingsFile` and `SettingsFileSet`.

- [ ] **Step 4: Run** the planner and command tests (`dotnet test tests/SqlSource.Tool.Tests`): PASS.

- [ ] **Step 5: Format, validate, commit:** `"Report the settings files that a chain reaches in the tool, once each"`.

### Task 4: The dialect, the output and the database come from the chains

**Files:**
- Modify: `src/SqlSource.Tool/Planning/RunPlanner.cs` (`Parse`, `PlanQuery`, `GreatestOutput`, `ClaimedFile`)
- Test: `tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs`

**Interfaces:**
- Consumes: `ClaimedFile.SettingsFiles` (Task 3); `SettingsFileSet.ChainOf(string normalizedFilePath)`; `FileParseInput.Resolve(FileMetadata, string? normalizedPath, SettingsChain, DialectSetting, bool, EquatableArray<string>)`; `FileLevel.Resolve(FileMetadata, string? normalizedPath, SettingsChain) -> FileLevel?`.
- Produces: `ClaimedFile` holds `SettingsChain Chain` (the chain of its path in its own project's set) and `SettingsLevel Level` (`FileLevel.Resolve(...)?.Level ?? SettingsLevel.None`), computed in `ReadProject`; `Parse` passes `Chain`; `GreatestOutput` passes each claimant's `Level` where it passed the metadata's level; `PlanQuery` passes the owner's `Level` to `QuerySettings.ResolveDatabase`.

- [ ] **Step 1: Write the failing tests** (each a planned file's `Dialect` or a query's `NeedsEntry`/`Database`):
  - `Plan_DialectOfTheChain_IsTheFilesDialect`: `App/sqlsource.json` `{"dialect": "postgres"}` with no property: the file is `Postgres`.
  - `Plan_NearerFilesDialect_WinsOverTheFarther`: `App/` `ansi`, `App/Queries/` `mssql`: a file in `Queries/` is `SqlServer`.
  - `Plan_HeaderMarkersDialect_WinsOverTheChain`: chain `mssql`, file header `-- dialect: postgres`: `Postgres`.
  - `Plan_ChainsDialect_WinsOverMetadataAndProperty`: property `sqlite`, metadata `mysql`, chain `postgres`: `Postgres` (and the metadata's invalid value, if any, is still `SQLSRC011`, unchanged).
  - `Plan_RootFile_StopsTheChain`: `<root>/sqlsource.json` `{"database": "far"}`, `App/sqlsource.json` `{"root": true, "dialect": "postgres"}`: the query's database is `postgres`, the dialect's name.
  - `Plan_OutputOfTheChain_DecidesWhetherAQueryNeedsAnEntry`: property `SqlSourceOutput=models`, chain `{"output": "sql"}`: `NeedsEntry` false; with the chain absent, true.
  - `Plan_AttributesOutput_WinsOverTheChain`: chain `sql`, attribute `Output = GeneratorOutput.Models`: `NeedsEntry` true.
  - `Plan_DatabaseOfTheChain_IsTheQuerysDatabase`: `{"dialect": "postgres", "database": "billing"}`: `billing`; a query with `-- database: other` gets `other`; property and metadata `SqlSourceDatabase` lose to the chain.
  - `Plan_FileClaimedByTwoProjects_TakesEachProjectsChainForItsOutput`: the file is in project A's folder and claimed by A and by B (which links it); A's chain says `sql`, B's property says `models`: `NeedsEntry` true (the greatest over every claim).

- [ ] **Step 2: Run to see them fail.**

- [ ] **Step 3: Implement** the changes under Interfaces.  Remove the now-wrong comment of `Parse` only where it names the metadata alone.

- [ ] **Step 4: Run** `dotnet test tests/SqlSource.Tool.Tests`: PASS.

- [ ] **Step 5: Format, validate, commit:** `"Resolve the dialect, the output and the database of the tool from the chains"`.

### Task 5: The generator and the tool agree on the settings files

**Files:**
- Test: `tests/SqlSource.Tool.Tests/Planning/GeneratorParityTests.cs`, `tests/SqlSource.Tool.Tests/Commands/` (the file that runs a describe over a `TestProject` with errors of the plan; add a fact there)

**Interfaces:**
- Consumes: `TestProject.AddSettings` (Task 3); `GeneratorHarness.SettingsFile(path, json)` and `GeneratorHarness.Run(..., properties:)` (`properties["SqlSourceBuildFolders"]` reaches the generator as `build_property.SqlSourceBuildFolders`; check `TestOptionsProvider` and add the key if it filters names).

- [ ] **Step 1: Write the parity theory** `Errors_OfSettingsFiles_AreTheGeneratorsAtTheSamePlaces` over a dictionary of cases `(Sources, SqlFiles, SettingsFiles, BuildFolders?)`, each file written once to the disk by the `TestProject` and given to both sides by its full path.  Compared: every diagnostic with an id from `SQLSRC015` to `SQLSRC020`, by id, path, start position and message, in `InOrder`.  The cases hold only errors both sides report (no `database`, no `typeOverrides`), so both lists are compared whole, and each case asserts its expected count:
  - "errors of the file": `App/sqlsource.json` with an unknown key, a repeated key and a wrong type (3).
  - "invalid dialect and output": `{"dialect": "pgsql", "output": "model"}` (2).
  - "text that is not JSON, and the farther file through it": `App/Queries/` not JSON, `App/` with an unknown key (2).
  - "a file above the root file": a broken root-of-temp file, `App/` `{"root": true, "Root": 1}` (1: the unknown key of `App/`, never the one above).
  - "reached by a C# file alone": `App/Code/` beside `Repository.cs` (1).
  - "reached by an unclaimed .sql file": the type claims `Queries`, `App/Other/` beside `X.sql` (1).
  - "a C# file under a build folder": `obj/Gen.cs` and `obj/sqlsource.json` with an error, build folders `|<App>/obj/`, plus `App/sqlsource.json` with an unknown key (1).
- [ ] **Step 2: Write the parity fact** `Errors_OfDialectAndOutputFromSettingsFiles_AreTheGeneratorsSqlsrc209And210`: no properties; `App/sqlsource.json` `{"dialect": "ansi", "output": "models"}`, `App/Lite/sqlsource.json` `{"dialect": "sqlite"}`, a `.sql` file in each folder with a token without a default in one: the `SQLSRC209`/`SQLSRC210` of both sides are equal, as in `Errors_OfTheSameSourcesAndFiles_AreTheGeneratorsSqlsrc209And210`, and non-empty.
- [ ] **Step 3: Write the command fact**: a describe over a `TestProject` whose `App/sqlsource.json` has `"Dialect": "postgres"` prints `<full path>(2,3): error SQLSRC016: ...` and its `see:` line on standard error, and exits 1.
- [ ] **Step 4: Run** them: PASS (Tasks 3 and 4 made them so; a failure is a disagreement to fix in the planner, never in the test's expectations).  Update the class comment of `GeneratorParityTests`: it compares the settings files' errors too.
- [ ] **Step 5: Format, validate, commit:** `"Hold the tool to the generator on the settings files"`.

### Task 6: Documentation of the tool's settings files

**Files:**
- Modify: `docs/architecture/tool.md` ("The plan", the `Planning/` row of "Folders"), `docs/architecture/generator.md` ("The tool runs the same code", "Settings files" — "The tool reads none yet" —, "Which files are checked"), `src/SqlSource.Tool/AGENTS.md` (the rules "The plan must agree with the generator" and "Of the settings that are not valid"), `docs/manual/diagnostics.md` (the paragraph on what the tool reports, after the table), `docs/superpowers/specs/2026-10-25-settings-files-design.md` (the "Builder's call, for layer 4" sentence of "Who reports what": now built, and the reported-once call)

- [ ] **Step 1: Rewrite each place** to say what the code now does: the tool reads the manifest's settings files with `ListedSettingsFile` and `SettingsFileSet`, resolves the dialect (`FileParseInput.Resolve` with the file's chain), the output and the database (`FileLevel.Resolve`), checks the files the chains of its `.sql` and `Compile=` paths reach (less the build's folders, `BuildFolders.ReachingPaths`), reports each once per run after `SQLSRC014`, and `GeneratorParityTests` compares `SQLSRC015` to `SQLSRC020` and the `SQLSRC209`/`210` of settings-file values.  `diagnostics.md` says, as an effect: `sqlsource describe` reports the problems of a settings file that applies to a `.sql` or C# file of the project, at the same place as the build, and the values of `dialect`, `output` and `database` alone.
- [ ] **Step 2: Validate** (the documentation tests run in it) and commit: `"Say how the tool reads the settings files"`.
- [ ] **Step 3: Write the hand-over notes** `4-tool-notes.md` in the shared notes folder (not in the repository): which MSBuild readers of the tool remain (`ManifestOptions.ForFile`, `ProjectSettings.Read(options, MSBuildSettings.ManifestProperty)`, `FileMetadata.Read`, `DialectSetting.ReadProperty`, `InvalidValues` and `AddInvalid`'s `SQLSRC011`/`SQLSRC014`, `MSBuildSettings.ManifestProperty`/`ManifestMetadata`), and what layer 6 must remove or keep (`Property.SqlSourceBuildFolders` stays; `ManifestOptions.ForProject` stays for it).
