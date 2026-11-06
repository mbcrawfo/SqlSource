# The `.env` file: one folder, or the one you name — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** the tool and the build read one `.env`: the unit folder's, the one `--env-file` / `SqlSourceEnvFile` names, or none; nothing looks above the unit.

**Architecture:** the tool resolves the run's file once, from the command line, to a full path or none (`DotEnvSearch.FileOf`), and every reader (connections, SQLSRC213's help, the watch) takes that path.  The build computes the same path from `SqlSourceEnvFile`, tests that the file exists to decide `auto`, and passes it to the tool when the property is set, so the two agree by construction.

**Tech Stack:** C# (net8.0 tool), System.CommandLine, MSBuild property functions, xUnit + Shouldly.

**Spec:** `docs/superpowers/specs/2026-11-06-env-file-one-folder-design.md`

## Where the code differs from the spec's assumptions

- `DotEnvSearch.FoldersOf` and `IsInRepository` are also used by `Watching/WatchSet.cs` for settings files and a restore's assets folder.  Those uses stay (they read no secret); the two methods move to `Watching/RepositoryFolders.cs`.
- SQLSRC213's `help:` line already names a solution run's unread project `.env` files (`DotEnvSearch.NotRead`); it stays and its sentence changes.
- td-0113 is not merged yet: its skip helper `AboveTheTempFolder` is not on this branch.  Task 6 runs after a sync that brings it.

## Global Constraints

- Spellings: tool option `--env-file <path>`; build property `SqlSourceEnvFile`; the word `none`, matched ignoring case and the white space around it, turns `.env` off.  (Owner Q3; recommended, pending.)
- Default folder: the unit's (the solution's or project's folder; for `--manifest`, the project's).  (Owner Q1; recommended, pending.)  If the owner picks the working folder, only the `defaultFolder` argument of `DotEnvSearch.FileOf`'s callers changes.
- A named file that does not exist, or is a folder, is no file: not an error.  (Q3.)
- A relative `--env-file` is from the working folder; a relative `SqlSourceEnvFile` from the project's folder.
- The build never reads the file's text.  The environment wins over the file, variable by variable.
- Option declarations keep `DescribeCommand.Create`'s current style (108-run refactors them later).
- Nothing is released: say the breaking change plainly in the docs; no migration.
- Every commit passes `./pre-commit-validation.sh`; run `./format.sh` first.

## Review Focus

- A `SqlSourceEnvFile` path with a space, `$`, `"` or `%` reaches the tool intact — the `Exec` escaping (Task 4 test `Build_EnvFileInAFolderWithShellCharacters_PassesItsFullPath`).
- `--env-file none` vs a file named `none`: `./none` reads the file (Task 2 test).
- A `.env` that is a folder: the build stays offline under `auto`, the tool reads nothing (Task 4 agreement theory row).
- Editing the named file outside the unit wakes `--watch` (Task 3 test).
- `--env-file` given twice, or blank, is a usage error that never prints the value (Task 2 tests).

---

### Task 1: The tool reads one file

**Files:**
- Modify: `src/SqlSource.Tool/DotEnv/DotEnvSearch.cs`, `src/SqlSource.Tool/Describing/ConnectionEnvironment.cs`, `DescribeOptions.cs`, `DescribeRun.cs`, `DescribeErrors.cs`, `src/SqlSource.Tool/DescribeCommand.cs` (`OptionsOf`), `src/SqlSource.Tool/Bundles/DiagnoseRun.cs:316`, `src/SqlSource.Tool/Watching/WatchSet.cs` (calls only)
- Create: `src/SqlSource.Tool/Watching/RepositoryFolders.cs` (`FoldersOf`, `IsInRepository`, moved as they are, with the `.git` remarks)
- Test: `tests/SqlSource.Tool.Tests/DotEnv/DotEnvSearchTests.cs`, `tests/SqlSource.Tool.Tests/Commands/DotEnvRunTests.cs`, `tests/SqlSource.Tool.Tests/Describing/ConnectionTests.cs`, `DatabaseRunsScene.cs`

**Interfaces:**
- Produces:
  - `DotEnvSearch.NoneValue = "none"`.
  - `static string? DotEnvSearch.FileOf(string? option, string defaultFolder, string workingDirectory)`: `null` option → full path of `defaultFolder/.env`; option trimmed equal to `none` ignoring case → `null`; else `Path.GetFullPath(option, workingDirectory)`.
  - `static DotEnvFile DotEnvSearch.Read(string? path, Func<string, string?> readTextFile)`: reads that one file; `null` path or a file the host does not find → `DotEnvFile(Path: null, [], [])`; errors as today (SQLSRC285, SQLSRC286).
  - `DotEnvSearch.NotRead(string unitFolder, string? read, IEnumerable<string> projectFolders, readTextFile)` unchanged.
  - `DescribeOptions` gains `string? EnvFile` (after `UnitFolder`): the full path the run reads, or `null` for none.
  - `ConnectionEnvironment.Load(string unitFolder, string? envFile, ToolHost host)`; new property `string? LookedFor` (the path looked for, `null` under none); `FilePath` stays "the file read".
  - `DescribeRun.ConnectionsOf(..., string unitFolder, string? envFile, ToolHost host, Reporter reporter)`.
  - `RepositoryFolders.FoldersOf(string folder)`, `RepositoryFolders.IsInRepository(string folder)`.
- In this task `OptionsOf` and `DiagnoseRun` pass `DotEnvSearch.FileOf(null, UnitFolderOf(unit), host.WorkingDirectory)`; Task 2 passes the option.

- [ ] **Step 1: Rewrite `DotEnvSearchTests`** for the new API.  Keep the parser-facing cases (SQLSRC285 on a throwing reader, SQLSRC286 lines, a folder named `.env` gives no file, `NotRead` cases).  Replace the climbing cases with:
  - `Read_EnvAboveTheUnitFolderInARepository_IsNotRead`: `repo/.git`, `repo/.env`, unit `repo/src/app` → `Read(FileOf(null, unit, unit))` has `Path` null.
  - `Read_EnvAboveTheUnitFolderOutsideARepository_IsNotRead`: same without `.git`, with a `.git` two levels up (the planted case) → `Path` null.
  - `FileOf_NoOption_IsTheEnvOfTheDefaultFolder`, `FileOf_None_IsNull` (`"none"`, `" NONE "`), `FileOf_RelativePath_IsFromTheWorkingDirectory`, `FileOf_DotSlashNone_IsAFileNamedNone`, `FileOf_AbsolutePath_IsItself`.
  - `Read_NamedFileMissing_GivesNoFileAndNoError`.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~DotEnvSearchTests"` — expect compile failure.
- [ ] **Step 3: Implement** the Interfaces above; move `FoldersOf`/`IsInRepository` to `RepositoryFolders` and point `WatchSet` at it (its `_envFolders` still uses `RepositoryFolders.FoldersOf(unitFolder)` until Task 3).  Update the class remarks of `DotEnvSearch` (no search: one folder or a named file).
- [ ] **Step 4: SQLSRC213's help** in `DescribeErrors.NoConnection`:
  - with a path: `... or set VAR in the environment or in <LookedFor>` (as today);
  - under none: `... or set VAR in the environment.  The run reads no .env file`;
  - the unread-files sentence becomes `.  The run did not read <files>: it reads <LookedFor> alone, and --env-file, or SqlSourceEnvFile in a build, names another` (under none: `: it reads no .env file, and ...` the same tail).
  Update `DotEnvRunTests`: `Run_NoConnectionAndADotEnvFileAboveTheUnit_HelpNamesTheFileRead` becomes `..._HelpNamesTheUnitsFile` (the file above is not read); the `NotRead` tests expect the new sentence.
- [ ] **Step 5: Run** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~DotEnv|FullyQualifiedName~Connection|FullyQualifiedName~DatabaseRuns|FullyQualifiedName~Watch|FullyQualifiedName~Diagnose"` — PASS.
- [ ] **Step 6: Commit** `feat(tool): a run reads the .env of its unit's folder alone`.

### Task 2: `--env-file` on `describe` and `diagnose`

**Files:**
- Modify: `src/SqlSource.Tool/DescribeCommand.cs` (constant `EnvFileOption = "--env-file"`, declaration after `--log`, `CheckUsage`, `ReadsValuesAs`, `OptionsOf`), `src/SqlSource.Tool/DiagnoseCommand.cs` (declaration, `ReadsValuesAs`), `src/SqlSource.Tool/Bundles/DiagnoseRun.cs`, `src/SqlSource.Tool/UsageCheck.cs` (remarks list of compared options)
- Test: `tests/SqlSource.Tool.Tests/Commands/DotEnvRunTests.cs`, `UsageCheckTests.cs` (vocabulary of both cross-check tests), `DiagnoseTests.cs`, `FilterTests.cs` (help lists it)

**Interfaces:**
- Consumes: `DotEnvSearch.FileOf`, `DescribeOptions.EnvFile`.
- Produces: `DescribeCommand.EnvFileOption`; `static string? DescribeCommand.EnvFileOf(Usage usage, string unitFolder, ToolHost host)` used by describe, watch passes and diagnose.

- [ ] **Step 1: Tests** in `DotEnvRunTests`:
  - `Run_EnvFileNamed_ReadsItAndNotTheUnitsOwn`: unit `.env` gives `A=wrong`, `--env-file other/.env.local` gives the right connection → describes with it; `--verbose` names `other/.env.local`'s full path.
  - `Run_EnvFileRelative_IsFromTheWorkingFolder`.
  - `Run_EnvFileNone_ReadsNoFile_AndHelpSaysSo` → SQLSRC213, help ends `The run reads no .env file`.
  - `Run_EnvFileMissing_IsNoErrorAndHelpNamesIt`.
  - `Run_EnvFileDotSlashNone_ReadsTheFileNamedNone`.
  - In `UsageCheckTests`: `--env-file` given twice → `sqlsource: option '--env-file' is given more than once`; blank value → `sqlsource: the value of option '--env-file' at position <n> is not a path`; a value holding `=` or `;` without `--online` → `... holds '=' or ';', which a .env file's path may not`, allowed under `--online`; neither prints the value.  Add `--env-file` and a value to the vocabularies of `Check_EveryAcceptedCommandLine_IsReadTheSameBySystemCommandLine` and `..._OfTheTool_...`.
  - `DiagnoseTests`: `Diagnose_EnvFileNamed_TakesTheConnectionFromIt`; `--scrub` with `--env-file` is the existing "takes no other option" line.
- [ ] **Step 2: Run** the filters `DotEnvRunTests|UsageCheckTests|DiagnoseTests` — FAIL.
- [ ] **Step 3: Declare** `new Option<string>(EnvFileOption) { Description = "Read the connections from this .env file instead of the one in the folder of the solution or project; 'none' reads none.", HelpName = "path", Arity = ArgumentArity.ExactlyOne }` in both commands; check its value in `DescribeCommand.CheckUsage` (and diagnose's) the way `ProblemOfManifestPath` checks `--manifest`; wire `EnvFileOf` into `OptionsOf`, the watch's passes and `DiagnoseRun`.
- [ ] **Step 4: Run** the same filters plus `CliTests|FilterTests|WatchTests` — PASS.
- [ ] **Step 5: Commit** `feat(tool): --env-file names the run's .env, or none`.

### Task 3: The watch watches the run's file

**Files:**
- Modify: `src/SqlSource.Tool/Watching/WatchSet.cs` (`Of(string unitPath, string unitFolder, string? envFile, FoundProjects found)`; `_envFolders` becomes `_envFile`; `FoldersOf` adds the file's folder alone instead of `FoldersOf(unitFolder)` for `.env`), `WatchSession.cs` (computes the env file once from `_usage`)
- Test: `tests/SqlSource.Tool.Tests/Watching/WatchSetTests.cs`, `tests/SqlSource.Tool.Tests/Commands/Watch/WatchChangeTests.cs`

- [ ] **Step 1: Tests**: `Classify_EnvOfTheUnitFolder_Describes`; `Classify_EnvAboveTheUnitFolder_DoesNothing` (was Describe); `Classify_NamedEnvFileOutsideTheUnit_Describes` and `Folders_NamedEnvFileOutsideTheUnit_WatchesItsFolderAlone`; `Classify_AnyEnvUnderNone_DoesNothing`; in `WatchChangeTests` a pass after saving the file named by `--env-file`.  Keep the folders above the unit that the settings and assets rules still watch.
- [ ] **Step 2: Run** `--filter "FullyQualifiedName~Watch"` — FAIL.
- [ ] **Step 3: Implement**; the remarks of `WatchSet` name `RepositoryFolders` for the `.git` markers.
- [ ] **Step 4: Run** — PASS.
- [ ] **Step 5: Commit** `feat(tool): --watch watches the one .env the run reads`.

### Task 4: The build looks in one place and tells the tool

**Files:**
- Modify: `src/SqlSource/build/SqlSource.targets` (online group, comment, `SqlSourceListSidecars` input, `SqlSourceRunOnline` command), `src/SqlSource/buildMultiTargeting/SqlSource.targets`
- Test: `tests/SqlSource.Tests/Package/BuildFileTests.Online.cs`, `tests/SqlSource.Tool.Tests/Projects/OnlineBuildTests.cs`

**Interfaces:**
- Produces, in this order in the online `PropertyGroup`, in place of `SqlSourceEnvFolder`:
  - `SqlSourceEnvFileValue` = `$(SqlSourceEnvFile.Trim())`
  - `SqlSourceEnvFilePath` = `$([MSBuild]::NormalizePath($(MSBuildProjectDirectory), '.env'))`; with `'$(SqlSourceEnvFileValue)' != ''`: `$([MSBuild]::NormalizePath($(MSBuildProjectDirectory), $(SqlSourceEnvFileValue)))`; with `'$(SqlSourceEnvFileValue)' == 'none'` (case-insensitive in a condition, and `none` is no boolean word): empty.
  - `SqlSourceHasEnvFile` = `false`; with `'$(SqlSourceEnvFilePath)' != '' and $([System.IO.File]::Exists($(SqlSourceEnvFilePath)))`: `true`.
  - `SqlSourceGoesOnline`'s condition reads `'$(SqlSourceHasEnvFile)' == 'true'` where it read `'$(SqlSourceEnvFolder)' != ''`.
  - In `SqlSourceRunOnline`: `SqlSourceToolEnvFile` empty; ` --env-file &quot;<SqlSourceEnvFilePath through Regex::Replace with SqlSourceShellSpecial/Escape>&quot;` when the value is set; ` --env-file none` when it is `none`; appended to the `Exec` command after `--online $(SqlSourceOnlineValue)`.
  - The run's input is `$(SqlSourceEnvFilePath)`, escaped, when `SqlSourceHasEnvFile` is true.
- The three properties join `SharedOnlineProperties` and are repeated with the same text in `buildMultiTargeting/SqlSource.targets`.

- [ ] **Step 1: BuildFileTests**: update `Targets_OnlineProperties_AreTheDesignsExpressions`, `SharedOnlineProperties`, the input line test (around line 383) and the `Exec` command test to the expressions above.
- [ ] **Step 2: OnlineBuildTests**: replace `Build_AutoWithADotEnvInAFolderAbove_RunsTheTool` with `Build_AutoWithADotEnvInAFolderAbove_RunsNothing`, and add the agreement theory `Build_EnvLayout_GoesOnlineExactlyWhenTheToolReadsAFile(string layout)` over: the project's `.env`; one above; `SqlSourceEnvFile` relative (`../shared.env`, existing); absolute; missing; a folder named `.env`; `none` with a project `.env`.  Each row asserts that the stand-in ran (under `auto`) iff `DotEnvSearch.Read(DotEnvSearch.FileOf(<the run's --env-file value or null>, <project folder>, <project folder>), ToolHost.ReadText).Path` is not null, and that it equals the file the row expects.  Also `Build_TrueWithEnvFileNone_PassesEnvFileNone`, `Build_EnvFileInAFolderWithShellCharacters_PassesItsFullPath` (folder `a $b "c" %d`), `Build_DefaultEnvFile_PassesNoEnvFileOption`, and add `SqlSourceEnvFile`'s file as a row of `Build_AfterAnInputIsTouched_RunsTheToolOnceMore`.
- [ ] **Step 3: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~BuildFileTests"` and `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~OnlineBuildTests"` — FAIL.
- [ ] **Step 4: Implement**; rewrite the targets' comment paragraph on the connection source ("a file named by SqlSourceEnvFile, or the project folder's .env; never a folder above").
- [ ] **Step 5: Run** both — PASS; and `ManifestRunTests` (a `--manifest` run reads the project folder's `.env`).
- [ ] **Step 6: Commit** `feat(build): SqlSourceEnvFile, and auto looks only at the project's .env`.

### Task 5: Docs and examples

**Files:**
- Modify: `README.md` (step with the `.env`: beside the project, or a root `.env` named by `SqlSourceEnvFile` in `Directory.Build.props`; the "nearest ... up to the root" sentence; absolute URLs, no raw HTML), `src/SqlSource.Tool/README.md` (lines 16, 38), `docs/manual/tool.md` (`#env-file` section, connections table text, running in the build, what a watch reacts to, command reference `--env-file` for describe and diagnose), `docs/manual/settings.md` (table row, `auto` row and the "Any `.env` file" bullet, new section `<a id="setting-env-file"></a>` "Choosing the `.env` file" with example, values, relative path, `none`, where it is written), `docs/manual/diagnostics.md` (SQLSRC026's connection sentence; SQLSRC213's help paragraph and example sentence), `docs/manual/README.md` (find-what-you-need row if a need is new), `docs/architecture/tool.md` (the `.env` section; `RepositoryFolders`), `docs/architecture/msbuild-and-manifest.md` (the online build's connection source and the new properties), `examples/*/README.md` if a sentence changes, `src/SqlSource.Tool/AGENTS.md` / `src/SqlSource/build/AGENTS.md` only where a named member moved (both are at their size limit: detail goes to `docs/architecture/`).
- [ ] **Step 1:** Edit each, saying plainly that the build and the tool no longer look above the project's or solution's folder, and nothing is released so no migration is offered.
- [ ] **Step 2:** Run the grep of `docs/manual/AGENTS.md` on changed pages and `dotnet test tests/SqlSource.Repository.Tests` — PASS.
- [ ] **Step 3: Commit** `docs: one .env folder, and SqlSourceEnvFile / --env-file`.

### Task 6: After td-0113 merges

- [ ] **Step 1:** Sync with `main`; remove the skip of `OnlineBuildTests.Build_AutoWithoutAConnectionSource_RunsNothingAndWritesNoFileOfIt` under a `.env` above the temporary folder, and the helper's `.env` branch if nothing else uses it; keep each `.git` skip whose test still depends on a repository's root (the watch's settings and assets rules).  Grep `docs/tech-debt` for anything left about them.
- [ ] **Step 2:** Run the affected test classes — PASS.  Commit `test: drop the .env-above skip the one-folder lookup made moot`.
