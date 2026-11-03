# Phase 9.2: the online build - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A build of a project with a connection runs `dotnet sqlsource describe` on that project before the compiler, so it compiles what the schema says and reports a query the database rejects at the query, in the IDE's error list; a build with no connection, in CI or with `SqlSourceOnline` `false` builds from the committed sidecars as today.

**Architecture:** The tool gains `--manifest <path>`, which reads the manifest the build wrote instead of starting MSBuild, and `--online <auto|true>`, under which every error is one line, a run with no connection says it is offline and exits `0`, and a run with nothing to describe says so.  The package's `build/SqlSource.targets` gains `SqlSourceDescribeOnline`, a dependency of `SqlSourceTrackAdditionalFiles`, which runs the tool through `SqlSourceRunOnline` (its own target, with `Inputs`, `Outputs` and a stamp, run once for the first framework) and then gives the compiler the sidecars the run created and takes away the ones it deleted.  A new `buildMultiTargeting/SqlSource.targets` calls `SqlSourceRunOnline` from the outer build before `DispatchToInnerBuilds`.

**Tech Stack:** C# (`net8.0` tool), MSBuild (no task assembly), xUnit v3, Shouldly, bash (`tools/check-package-install.sh`).

**Spec:** `docs/superpowers/specs/2026-11-02-query-generation-phase-9-build-integration-design.md`, sections "The online build", "Diagnostics", "9.2 The online build" and "Testing"; the spike's findings in `docs/superpowers/specs/2026-11-02-query-generation-phase-9-spike.md` ("1. The target's place", "2. The connection in the log", "4. `Exec` and the error format", "5. The cost", "7. The CI variables", "What the later sub-phases take from this").  Both win over this plan where it is silent; the spike's amendments are already in the design.

## Global Constraints

- Root `AGENTS.md`, `src/SqlSource/build/AGENTS.md`, `src/SqlSource.Tool/AGENTS.md`, `src/SqlSource/Diagnostics/AGENTS.md`, `tests/SqlSource.Tool.Tests/AGENTS.md` and every `AGENTS.md` of a folder you touch win over this plan.  Read them first.
- **No file of the package reads `$(SQLSOURCE_CONNECTION)` as a property.**  The connection source is `$([System.String]::IsNullOrWhiteSpace($([System.Environment]::GetEnvironmentVariable('SQLSOURCE_CONNECTION'))))`, computed into a property that holds only `true` or `false`.  A test holds it (Task 4).
- Every MSBuild property the package's files set starts with `SqlSource`; every SDK property or environment variable they read as a property is in `SdkProperties` of `tests/SqlSource.Tests/Package/BuildFileTests.cs`: add `DesignTimeBuild`, `BuildingProject`, `TargetFrameworks`, `SQLSOURCE_LOG`, `CI`, `TF_BUILD`, `TEAMCITY_VERSION`, `JENKINS_URL`, `MSBuildWarningsNotAsErrors`, and whatever else the new targets read (`MSBuildProjectDirectory` is already there).  Never `SQLSOURCE_CONNECTION`.
- CI is `'$(CI)' == 'true' or '$(TF_BUILD)' == 'True' or '$(TEAMCITY_VERSION)' != '' or '$(JENKINS_URL)' != ''`.  The first framework is `$([System.Text.RegularExpressions.Regex]::Match($(TargetFrameworks), '[^;\s]+'))`.
- The generator package stays one DLL under `analyzers/dotnet/cs`: no MSBuild task assembly.  `buildMultiTargeting/SqlSource.targets` holds no DLL.
- The tool's error format stays the contract it is; the one-line form exists only under `--online`.
- Everything the tool takes from outside comes through `ToolHost`; tests never depend on a developer's `.env` or environment.  The MSBuild tests clear `SQLSOURCE_CONNECTION*`, `SQLSOURCE_LOG` and the CI variables (`CI`, `TF_BUILD`, `TEAMCITY_VERSION`, `JENKINS_URL`, and `GITHUB_ACTIONS` for good measure) from every process they start, or set them on purpose.
- Diagnostic ids, taken now: targets `SQLSRC024` (invalid `SqlSourceOnline`), `SQLSRC025` (cannot run the tool under `true`), `SQLSRC026` (warning: cannot run the tool under `auto`), `SQLSRC027` (the tool exited non-zero with no error of its own).  They are `SqlDiagnostics` descriptors beside `CompilerTooOld` (`SQLSRC021`), each touching the four places of `src/SqlSource/Diagnostics/AGENTS.md`.  No tool (`SQLSRC2xx`) id is added.
- The repository's root `Directory.Build.props` sets `<SqlSourceOnline>false</SqlSourceOnline>`.
- Every commit passes `./pre-commit-validation.sh`; run `./format.sh -- <changed .cs files>` first.  Implementers run the tests that cover what they changed; the full `--full` run is the controller's.
- Shared files with unit 9-3 (`describe --watch`), being built beside this one: in `DescribeCommand.Create`, `DescribeCommand.CheckUsage`, `UsageCheck`, their tests, `docs/manual/tool.md`'s command reference and `docs/architecture/tool.md`'s "Build integration options", every addition of this unit is a block of its own (its own option declarations, its own `else if` arms or helper method, its own rows, its own subsection) so the second to merge keeps both.  `Reporter.cs` and `SidecarStore.cs` are this unit's.  `ToolHost.cs` and `Cli.cs` get nothing unless a task below says so.

## Where the merged code differs from the spec, and calls made here

- **The tool's command is a property, `SqlSourceToolCommand`, default `dotnet sqlsource`.**  The design leaves how the MSBuild tests stand in for the tool to the builder.  The fixture tests set it to a stand-in, and the end-to-end tests to the tool built beside them.  It is not in the user manual (no user needs it); `docs/architecture/msbuild-and-manifest.md` and `src/SqlSource/build/AGENTS.md` name it.  Said in the PR as a builder's call.
- **The stand-in is a small console project, `tests/SqlSource.Tool.StandIn`**, not a shell script, since the tool's tests also run nightly on Windows.
- **The outer build's properties.**  NuGet imports `buildMultiTargeting/` alone into the outer build, so that file defines the properties its condition reads (online value, CI, connection source, first framework) as `build/SqlSource.targets` defines them; `BuildFileTests` holds the two definitions equal, text for text.
- **An offline run changes the sidecars as a run that reported `SQLSRC213` would** (it deletes none, and holds back a file with a query to describe), without reporting it.  That is today's behaviour of a run with no connection, less the error.
- **The value file** is `$(IntermediateOutputPath)$(MSBuildProjectFile).SqlSource.online`, the list `....SqlSource.sidecars`, the stamp `....SqlSource.stamp`, the log `....SqlSource.log`, as the spike named the first two.

## Review Focus

1. **A path with a space, `;`, `%` or `$(`** in the project's folder or the manifest's path: the `Exec` command quotes it and MSBuild does not expand or split it; the one-line error still reads whole. (Tasks 2, 5)
2. **A multi-line connection error from the server** (`server: ERROR: ...` with a line break in its text) under `--online`: one line, no `error:` read as a second error. (Task 2)
3. **`SqlSourceOnline` written ` True ` or `AUTO`**: matched ignoring case and surrounding white space; `yes` is `SQLSRC024`. (Tasks 4, 5)
4. **A build run twice with nothing changed after the run created a sidecar**: the second build launches no tool (list rewritten after the run), and the third compiles nothing new. (Task 5)
5. **A tool whose first line is `sqlsource: offline: ...` under `true`**: never happens (offline is `auto` only); under `true` a missing connection is `SQLSRC213` and the build fails at the query. (Tasks 2, 5)

---

### Task 1: `--manifest` and `--online` on the command line, and a run from a manifest

**Files:**
- Modify: `src/SqlSource.Tool/DescribeCommand.cs` (options, `CheckUsage`, `ReadsValuesAs`, `PlanUnitAsync`, `OptionsOf`)
- Modify: `src/SqlSource.Tool/Projects/RunProjects.cs` (a second entry: the manifest of a file)
- Modify: `src/SqlSource.Tool/Describing/DescribeOptions.cs` (`Online`)
- Test: `tests/SqlSource.Tool.Tests/Commands/UsageCheckTests.cs` (vocabulary of the two "read the same" tests), `tests/SqlSource.Tool.Tests/Commands/ManifestRunTests.cs` (new)

**Interfaces:**
- Produces:
  - `DescribeCommand.ManifestOption = "--manifest"`, `DescribeCommand.OnlineOption = "--online"` (`internal const string`).
  - `enum OnlineMode { Off, Auto, True }` in `src/SqlSource.Tool/Describing/OnlineMode.cs`; `DescribeOptions` gains a last member `OnlineMode Online`.
  - `DescribeCommand.OnlineOf(ParseResult parsed) -> OnlineMode` (Off when not given or not `describe`).
  - `RunProjects.FromManifestAsync(string manifestPath, ToolHost host, Reporter reporter, CancellationToken) -> Task<ImmutableArray<ProjectManifest>>`: reads the file with `ManifestReader.Read`; an unreadable or missing file, or one `ManifestReader` rejects, is `SQLSRC206` (`ToolDiagnostics.ManifestCannotBeRead`) as today, and gives empty.

- [ ] **Step 1: Write the failing tests.**  In `UsageCheckTests`, add `--manifest <path>` and `--online auto` to the vocabulary of `Check_EveryAcceptedCommandLineOfTheTool_IsReadTheSameBySystemCommandLine`.  New usage tests (each asserts the one line written to the error stream and exit `1`; a line names a position, never a token):
  - `--manifest m --online auto` alone: accepted.
  - `--manifest m` with a unit path, a `.sql` path, `--project` or `--database`: `sqlsource: option '--manifest' takes no <path|.sql path|'--project'|'--database'> (position <n>)`; exact words yours, one test per kind, a line per offending value.
  - `--online auto` without `--manifest`: `sqlsource: option '--online' needs '--manifest'`.
  - `--online yes`, `--online ""`, `--online false`: `sqlsource: the value of option '--online' at position <n> is not 'auto' or 'true'`.  `AUTO` and `True` are accepted (ignoring case).
  - `--manifest m` with `--connection`, `--verbose`, `--log`, `--force`, `--check`: accepted.
  - `--manifest` value empty or holding `=`/`;` is rejected as `--log`'s is.
  In `ManifestRunTests` (through `CliRun`, a fake describer, a manifest written to a temp folder by the test): a run from a manifest plans the manifest's project, starts no process (`FakeProcessRunner` sees no call), searches `.env` from the project's folder (a `.env` there gives the connection), and writes the sidecar; a manifest file that does not exist is `SQLSRC206` at that path, exit `1`.
- [ ] **Step 2: Run them; they fail.**  `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "*UsageCheckTests" --filter-class "*ManifestRunTests"` (the xUnit v3 filter form the repository uses; see `tests/README.md`).
- [ ] **Step 3: Implement.**  Declare both options in `Create` by its rules (`ExactlyOne`, `HelpName` `path` and `auto|true`, descriptions saying they are for the build's use).  Add the rules to `CheckUsage` as one block of their own, after the existing arms.  `ReadsValuesAs` compares both values.  `PlanUnitAsync`: with `--manifest`, skip `RunUnitFinder` and `RunProjects.FindAsync`; the unit is `new RunUnit(RunUnitKind.Project, manifest.ProjectPath)` and the folder for `.env` its folder; log `RunEvents.Unit` with `wasGiven: true`.
- [ ] **Step 4: Run the tests; they pass.**  Also run `*CliTests`, `*DescribeTests`, `*SecretTests`.
- [ ] **Step 5: Commit** `feat(tool): describe from a manifest with --manifest, and --online`.

### Task 2: What a run says under `--online`

**Files:**
- Modify: `src/SqlSource.Tool/Reporting/Reporter.cs` (one-line form), `src/SqlSource.Tool/Cli.cs` (no `RunAgainLine` and the reporter's form under `--online`), `src/SqlSource.Tool/DescribeCommand.cs` (`LogPathOf` ignores `SQLSOURCE_LOG` under `--online`), `src/SqlSource.Tool/Describing/DescribeRun.cs` (offline, nothing to describe), `src/SqlSource.Tool/Describing/DatabaseRuns.cs` or where `SQLSRC213` and the "database cannot be opened" error are built (`DescribeErrors`)
- Test: `tests/SqlSource.Tool.Tests/Reporting/ReporterTests.cs`, `tests/SqlSource.Tool.Tests/Commands/OnlineRunTests.cs` (new)

**Interfaces:**
- Consumes: `OnlineMode`, `DescribeOptions.Online`, `DescribeCommand.OnlineOf` (Task 1).
- Produces:
  - `new Reporter(TextWriter error, RunTrace? trace = null, bool oneLine = false)`.
  - The offline line, exactly: `sqlsource: offline: no connection for <db1>, <db2>; the build uses the committed sidecars` (databases in the plan's order, joined `, `, each through `OneLine.Of`).
  - The nothing-to-describe line, exactly: `sqlsource: online: nothing to describe`.
  - Both to standard output, exit `0`.  Task 4's target matches them by the prefixes `sqlsource: offline:` and `sqlsource: online:`.

- [ ] **Step 1: Write the failing tests.**
  - `ReporterTests`: with `oneLine: true`, an error with two continuation lines is written `<first line>; help: <text>; query: <text>; see: <link>` followed by one newline, and nothing else; a continuation text holding a line break is one line (`OneLine`); without the flag, the output is unchanged (existing tests).
  - `OnlineRunTests` (through `CliRun` with `--manifest` and a fake describer):
    - `--online auto`, no connection anywhere, a query to describe: standard output is exactly the offline line, standard error empty, exit `0`, no sidecar written or deleted.
    - `--online auto`, two databases, a connection for one: the other gets `SQLSRC213` on one line, exit `1`.
    - `--online auto`, bare `SQLSOURCE_CONNECTION` and two describable databases: `SQLSRC215`, not offline.
    - `--online true`, no connection: `SQLSRC213`, one line, exit `1`, no offline line.
    - `--online auto`, every entry current, no connection: the normal summary, exit `0`, no offline line.
    - `--online auto`, every query resolves to `sql` (and, a second case, no file claimed): standard output exactly the nothing-to-describe line, exit `0`.
    - Any error under `--online`: no `Cli.RunAgainLine` on standard error.
    - `SQLSOURCE_LOG` set in the host's environment and `--online` given without `--log`: no log file is opened (the path given stays absent); `--log` given: written.
    - A database that cannot be opened, under `--online`: its error's help names `SqlSourceOnline` (start the database, remove its connection, or set `SqlSourceOnline` to `false`); without `--online` the help is as today.
    - A server error whose text holds a line break (`server: ERROR: ...\nDETAIL ...`) under `--online`: one line on standard error.
- [ ] **Step 2: Run them; they fail.**
- [ ] **Step 3: Implement.**  `Reporter.Format` writes `"; " + label + ": " + text` per line and `"; see: " + link` in one-line mode.  `Cli.InvokeAsync` makes the reporter with `oneLine: DescribeCommand.OnlineOf(parsed) != OnlineMode.Off`, and `OpenLog` sets `MayPoint` false under `--online`.  `DescribeRun.RunAsync`: under `Auto`, when no selected database that has a query to describe has a connection and `Connections.UnnamedIsSetAndNotUsed` is false, report no `SQLSRC213`, compute `mayDelete` as false, write the offline line in place of the summary.  Under any online mode, when the summary has no line, write the nothing-to-describe line.  Keep each addition small and commented in the repository's voice.
- [ ] **Step 4: Run the tests; they pass.**  Also `*SummaryTests`, `*ExitCodeTests`, `*VerboseTests`, `*SecretTests`, `*CliTests`.
- [ ] **Step 5: Commit** `feat(tool): one line per error, the offline line and no run-again line under --online`.

### Task 3: `SidecarStore` retries a move a sharing violation stops

**Files:**
- Modify: `src/SqlSource.Tool/Describing/SidecarStore.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/SidecarStoreTests.cs`

**Interfaces:**
- Produces: an internal seam in `SidecarStore` for the move and the wait (for example `internal static Func<string, string, ...>` passed to an internal overload of `Apply`, or a small `SidecarMove` type); the public `Apply` keeps its signature.

- [ ] **Step 1: Write the failing tests.**  A move that throws `IOException` with `HResult` `unchecked((int)0x80070020)` (sharing violation) twice and then succeeds: the sidecar is written, no error.  One that throws it every time: after the retries (10 tries over about 1 s; the test's wait is a fake that records its delays and does not sleep) the error is the one reported today.  An `IOException` of another `HResult`: reported at once, no retry.  Also `0x80070021` (lock violation) retries.
- [ ] **Step 2: Run them; they fail.**
- [ ] **Step 3: Implement** the retry around `File.Move(temporary, path, overwrite: true)` only; the wait comes from the seam (the real one `Thread.Sleep` of 100 ms, or `host.Time`-based if `Apply` already has it); cancellation stops retrying.
- [ ] **Step 4: Run `*SidecarStoreTests`; they pass.**
- [ ] **Step 5: Commit** `feat(tool): retry a sidecar's move that another process holds open`.

### Task 4: The package's online targets, the four diagnostics and the static rules

**Files:**
- Modify: `src/SqlSource/build/SqlSource.targets`, `src/SqlSource/build/SqlSource.props` (only what must be in props)
- Create: `src/SqlSource/buildMultiTargeting/SqlSource.targets`
- Modify: `src/SqlSource/SqlSource.csproj` (pack `buildMultiTargeting/SqlSource.targets` as `buildMultiTargeting/SqlSource.targets`), and whatever `tools/check-package.sh` lists as the package's files
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md`, `src/SqlSource/Diagnostics/AGENTS.md` if a list there changes, the test that lists the configurable descriptors (`SqlDiagnosticsTests`) for `SQLSRC026`
- Modify: `Directory.Build.props` (root): `<SqlSourceOnline>false</SqlSourceOnline>` with a comment why (the anti-goal; the sidecars are written by `tools/describe-end-to-end.sh`)
- Modify: `tests/Fixtures.props` (copy `buildMultiTargeting/` beside the fixtures, as `build/` is)
- Test: `tests/SqlSource.Tests/Package/BuildFileTests.cs`
- Modify: `src/SqlSource/build/AGENTS.md` (the new rules)

**Interfaces:**
- Produces (MSBuild names later tasks and docs use):
  - User property `SqlSourceOnline` (`auto`|empty, `true`, `false`, ignoring case and surrounding white space).
  - `SqlSourceToolCommand`, default `dotnet sqlsource`.
  - Computed: `SqlSourceOnlineValue` (`auto`, `true`, `false`, or the trimmed original when invalid), `SqlSourceInCI`, `SqlSourceHasConnectionVariable`, `SqlSourceEnvFolder` (`GetDirectoryNameOfFileAbove($(MSBuildProjectDirectory), '.env')`), `SqlSourceFirstFramework`, `SqlSourceGoesOnline` (`true` when the value is `true` or `auto` with a source, and not CI).  Names may change if `BuildFileTests` and the docs follow; keep them `SqlSource*`.
  - Targets: `SqlSourceListSidecars` (writes the list of existing sidecars and the value file, `SqlSourceOnlineValue` and `$(MSBuildThisFileDirectory)`, both with `WriteOnlyWhenDifferent`, adds them to `FileWrites`), `SqlSourceRunOnline`, `SqlSourceDescribeOnline`, and in `buildMultiTargeting/SqlSource.targets` `SqlSourceRunOnlineBeforeDispatch`.
  - Diagnostics: `SqlDiagnostics.OnlineValueInvalid` (`SQLSRC024`, error, `{0}` the value), `SqlDiagnostics.OnlineToolCannotRun` (`SQLSRC025`, error), `SqlDiagnostics.OnlineToolCannotRunOffline` (`SQLSRC026`, warning, configurable, its reason in the descriptor), `SqlDiagnostics.OnlineToolFailedSilently` (`SQLSRC027`, error, `{0}` exit code, `{1}` the tool's first line, `{2}` a sentence the target picks: the `dotnet tool update` advice when the first line starts `sqlsource: ` and is no line the tool writes on success, else empty).  Texts are yours within the design: `SQLSRC025` says to install the tool, run `dotnet tool restore`, or set `SqlSourceOnline` to `false`; `SQLSRC026` names `dotnet tool restore`, says the build goes on with the committed sidecars and that `SqlSourceOnline` `false` silences it.

**The targets' rules** (from the design and spike; each is a `BuildFileTests` assertion in Step 1):

- `SqlSourceTrackAdditionalFiles`' `DependsOnTargets` is `SqlSourceFindSettingsFiles;SqlSourceDescribeOnline`, in that order.
- `SqlSourceDescribeOnline`: `Condition="'$(DesignTimeBuild)' != 'true' and '$(BuildingProject)' == 'true'"`; no `BeforeTargets`/`AfterTargets`.  It reports `SQLSRC024` for an invalid value; when going online it depends on `SqlSourceRunOnline` in the first framework (or a project with no `TargetFrameworks`), and in any other framework calls `<MSBuild Projects="$(MSBuildProjectFullPath)" Targets="SqlSourceRunOnline" Properties="TargetFramework=$(SqlSourceFirstFramework)" />`; then includes `**/*.sql.json` again under the props' excludes and `SqlSourceIncludeFiles` condition, adds each file `@(AdditionalFiles)` lacks, and removes each `.sql.json` item whose file is gone.
- `SqlSourceRunOnline`: condition keeps `DesignTimeBuild`, the value, CI and the connection source, not `BuildingProject`; `DependsOnTargets="SqlSourceWriteManifest;SqlSourceListSidecars"`; no hook; `Inputs`: the manifest, the `.sql` and `sqlsource.json` items and the sidecar items of `@(AdditionalFiles)`, the list, the value file, `@(Compile)`, `$(SqlSourceEnvFolder).env` when found, the tool manifests `.config/dotnet-tools.json` and `dotnet-tools.json` found above the project; `Outputs`: the stamp.  It `Exec`s `$(SqlSourceToolCommand) describe --manifest "<manifest>" --online <value>` (plus `--log "$(IntermediateOutputPath)$(MSBuildProjectFile).SqlSource.log"` when `'$(SQLSOURCE_LOG)' != ''`) with `WorkingDirectory="$(MSBuildProjectDirectory)"`, `IgnoreExitCode="true"`, `ConsoleToMSBuild="true"`, `StandardOutputImportance="low"`, the default canonical-error reading.  Then: a tool line is one starting `sqlsource:`, one matching `: error SQLSRC\d{3}:`, or a summary line (its form from `Describing/RunSummary.cs`).  No tool line: `SQLSRC025` under `true`, `SQLSRC026` under `auto`.  Exit not `0` with tool lines and no `SQLSRC` error line: `SQLSRC027`.  It writes the list again after the run, and touches the stamp (and adds it to `FileWrites`) only when the exit code is `0` and no line starts `sqlsource: offline:`.
- The package appends `SQLSRC026` to `MSBuildWarningsNotAsErrors` in the targets (after the project's content), in both files.
- `buildMultiTargeting/SqlSource.targets`: `SqlSourceRunOnlineBeforeDispatch`, `BeforeTargets="DispatchToInnerBuilds"`, `Condition` the outer build (`'$(TargetFramework)' == '' and '$(TargetFrameworks)' != ''`) and `SqlSourceRunOnline`'s condition; it calls `SqlSourceRunOnline` with `Properties="TargetFramework=$(SqlSourceFirstFramework)"`.  It defines the properties it reads with the same text as `build/SqlSource.targets`.  The fixtures import it by path under NuGet's outer-build condition; NuGet imports it in a packed consumer.
- `SqlSourceWriteManifest`, `SqlSourceListFiles` and the search keep their rules; `SqlSourceListSidecars`, `SqlSourceRunOnline` and `SqlSourceDescribeOnline` hook nothing.

- [ ] **Step 1: Write the failing `BuildFileTests`:** the dependency order above; the conditions of the two targets (text compared after collapsing white space); that none of the three new targets has `BeforeTargets`/`AfterTargets` and the outer one hooks `DispatchToInnerBuilds` alone; `SqlSourceRunOnline`'s `DependsOnTargets`; the `Exec` attributes; that no file of `build/` or `buildMultiTargeting/` contains `$(SQLSOURCE_CONNECTION` (any case); the property rules applied to `buildMultiTargeting/SqlSource.targets`; the outer file's property definitions equal `build/SqlSource.targets`'s; the four `Error`/`Warning` elements' `Code` and `Text` equal their descriptors' messages with the placeholders the target writes, as for `SQLSRC021`; `SQLSRC026` in `MSBuildWarningsNotAsErrors`.
- [ ] **Step 2: Run `dotnet test --project tests/SqlSource.Tests -- --filter-class "*BuildFileTests"`; they fail.**
- [ ] **Step 3: Implement** the targets, the outer file, the packing, the descriptors and their three other places, the root property, `tests/Fixtures.props`, and `src/SqlSource/build/AGENTS.md`'s new rules (one bullet each: the online targets hook nothing but through `SqlSourceTrackAdditionalFiles`; the run is `SqlSourceRunOnline` once for the first framework; the outer target lives in `buildMultiTargeting/` and repeats the properties `BuildFileTests` holds equal; never read `$(SQLSOURCE_CONNECTION)`; the four targets diagnostics' texts change with their descriptors; `SqlSourceToolCommand` is the tests' seam).  Each target gets a comment in the file's voice saying why.
- [ ] **Step 4: Run `*BuildFileTests`, `*SqlDiagnosticsTests`, the diagnostics-doc test in `tests/SqlSource.Tests/Diagnostics/`, and `tests/SqlSource.Tool.Tests` `*ManifestTargetTests`, `*SettingsFileSearchTests`; they pass.  Then `dotnet build SqlSource.slnx` passes (the root property keeps this repository offline).**
- [ ] **Step 5: Commit** `feat(package): run the tool before the compiler when a connection is configured`.

### Task 5: The online build, run for real against a stand-in

**Files:**
- Create: `tests/SqlSource.Tool.StandIn/` (`SqlSource.Tool.StandIn.csproj`, `Program.cs`): a `net10.0` console app, added to `SqlSource.slnx`, referenced by `tests/SqlSource.Tool.Tests` so its DLL is in the tests' output.  Reads `stand-in.json` from its working directory (the project's folder): lines to print to each stream, sidecars to create (path, text) or delete, the exit code; appends its arguments and a copy of the manifest it was given to `stand-in.log` in that folder.  No file: prints a summary-form line and exits `0`.
- Create: fixtures under `tests/SqlSource.Tool.Tests/Fixtures/Projects/` (`Online`, one framework with one `.sql` file and its sidecar; `OnlineMulti`, `net8.0;net10.0` that imports `../buildMultiTargeting/SqlSource.targets` under `'$(TargetFramework)' == '' and '$(TargetFrameworks)' != ''`; `OnlineReference`, a `net10.0` project referencing `OnlineMulti`).  A `ShowItems` target in a fixture's `Directory.Build.targets` or the test's `-t:` that writes `@(AdditionalFiles)` given to `CoreCompile` to a file is how a test sees what the compiler got, since the generator is not loaded.
- Test: `tests/SqlSource.Tool.Tests/Projects/OnlineBuildTests.cs`, `tests/SqlSource.Tool.Tests/Projects/OnlineMultiTargetTests.cs`, and helpers in `tests/SqlSource.Tool.Testing/Fixtures/FixtureProjects.cs` (a `BuildAsync(project, environment, args)` that runs `dotnet build`, clears the variables of the Global Constraints and sets those given).
- Modify: `tests/README.md` (the new project and fixtures), `CONTRIBUTING.md` (one sentence for the new project).

**Interfaces:**
- Consumes: Task 4's names; `Programs.Dotnet`.
- Produces: `FixtureProjects.BuildAsync`; `StandIn` helpers (`StandIn.Command` giving `dotnet "<path>"` for `-p:SqlSourceToolCommand=`).

- [ ] **Step 1: Write the failing tests** (each restores and builds a copy with `-tl:off`, the stand-in as the command unless said):
  - `SqlSourceOnline=false`: the stand-in never runs, no manifest, stamp, list or value file under `obj`.
  - `auto` with no `.env` and no variable: same.  `auto` with `SQLSOURCE_CONNECTION` set: runs once, with `--manifest <obj path> --online auto`; with a `.env` in a parent folder: runs.  `true` with neither: runs.
  - `CI=true`, `TF_BUILD=True`, `TEAMCITY_VERSION=1`, `JENKINS_URL=x`: none runs under `auto` or `true`; `CI=1`: runs.
  - `SqlSourceOnline=yes`: fails with `SQLSRC024` naming `yes`; ` TRUE ` runs.
  - Up to date: a second build runs nothing (`stand-in.log` has one call); then each input touched alone runs it once: a `.sql` file, `sqlsource.json`, a sidecar, a deleted sidecar, a `.cs` file, `.env`, `dotnet-tools.json`, `-p:SqlSourceOnline=true` after `auto`, and a change of the package's folder alone (copy the fixture's `build/` to a second folder and point the import at it).
  - A run that exits `1` with a `SQLSRC` error line: the build fails with that error, no `MSB3073`, no stamp; the next build runs again.  An offline line with exit `0`: build passes, no stamp.
  - A stand-in that creates a sidecar: the same build's compiler items hold it; the next build launches nothing.  One that deletes a sidecar: the compiler items lack it, no `CS2001`.
  - Under `true` the stand-in printing `sqlsource: online: nothing to describe` touches the stamp and reports nothing.
  - A design-time build (`-p:DesignTimeBuild=true -p:BuildingProject=false -p:SkipCompilerExecution=true -t:Compile`) runs nothing.
  - The connection's value set in `SQLSOURCE_CONNECTION` is in no line of a `-v:diag` log of a build (`-flp:v=diag;logfile=...`) nor of a `-bl` binary log replayed to text.
  - The tool cannot start, with the real `dotnet sqlsource` (default command): no tool manifest; and a `dotnet-tools.json` naming `sqlsource.tool` at a version no feed has.  Under `auto` with current sidecars the build passes with `warning SQLSRC026`, still a warning under `-warnaserror`, no stamp.  Under `true` it fails with `SQLSRC025` alone, no `MSB3073`.
  - The stand-in printing `sqlsource: unknown option at position 2` and exiting `1`: `SQLSRC027` naming exit code `1`, that line and the `dotnet tool update` advice, under both values.
  - `SQLSOURCE_LOG` set: the command has `--log` with the path under `obj`.
  - Multi-targeting (`OnlineMulti`, a tree with no `obj`): `dotnet build` runs the stand-in once, for `net8.0`, with a manifest of that build, and both frameworks' compiler items hold the sidecar it created; the outer target runs before `DispatchToInnerBuilds` and the inner builds overlap (the `-v:diag` log's target start/finish order); `dotnet build -f net10.0` runs it once for `net8.0`; a build of `OnlineReference` runs it once for `net8.0`.
- [ ] **Step 2: Run them; the ones Task 4 does not already satisfy fail.**  Fix the targets until every case holds; a fix in `build/` updates `BuildFileTests` in the same commit.
- [ ] **Step 3: Run `*OnlineBuildTests`, `*OnlineMultiTargetTests`, `*BuildFileTests`, `*ManifestTargetTests`; they pass.**  Time the two classes; if one test takes over 30 s, say so in the hand-back.
- [ ] **Step 4: Commit** `test(package): the online build against a stand-in of the tool`.

### Task 6: Through the packed package

**Files:**
- Modify: `tools/check-package-install.sh` (a multi-targeted consumer that references the packed package, its command set to the stand-in or a small script, checks that the tool ran once before the dispatch and both frameworks compiled; and that a single-framework consumer with `SqlSourceOnline` unset and no connection builds offline with no warning), `tools/package-install/` (fixture files it needs), `tools/check-package.sh` (the package holds `buildMultiTargeting/SqlSource.targets`), `tools/README.md`.

- [ ] **Step 1: Extend the scripts**, keeping their style (`set -euo pipefail`, comments saying why, shellcheck- and shfmt-clean).
- [ ] **Step 2: Run `tools/check-package.sh` and `tools/check-package-install.sh`; they pass, and they fail when `buildMultiTargeting/` is left out of the pack (try it, then put it back).**
- [ ] **Step 3: Run `tools/shellcheck.sh` and `tools/shfmt.sh`.**
- [ ] **Step 4: Commit** `test(package): the outer build's run through the packed package`.

### Task 7: One online build in each database's end-to-end tests, and the cost

**Files:**
- Test: `tests/DatabaseTests/Postgres/EndToEnd/OnlineBuildTests.cs`, `tests/DatabaseTests/SqlServer/EndToEnd/OnlineBuildTests.cs`, a shared step in `tests/SqlSource.Tool.Testing` (beside `ErrorPaths`), a fixture project each (or `ErrorPaths` reused) that loads the generator by path (`<Analyzer Include=...SqlSource.dll />`).
- Modify: `tests/DatabaseTests/Postgres/README.md`, `tests/DatabaseTests/SqlServer/README.md`.

- [ ] **Step 1: Write the test**: copy the project out of the repository with no sidecar, set `SQLSOURCE_CONNECTION` to the container's connection for the build process, clear the CI variables, `dotnet build -p:SqlSourceOnline=true -p:SqlSourceToolCommand=<the tool beside the tests, as Programs.ToolStart runs it>`; the build passes, the sidecar exists, and the compiled assembly holds the model of the query (reflection on the output, or a `Program` that prints it).  A second case: a query the server refuses fails the build with one error at the `.sql` file's position.
- [ ] **Step 2: Run `dotnet test --project tests/DatabaseTests/Postgres -- --filter-class "*OnlineBuildTests"` and the same for SQL Server; they pass.  Then on the floor images: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` and `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.**
- [ ] **Step 3: Measure** the design's rule: a scratch project with 202 C# files, one claiming class, its sidecar current, PostgreSQL up; five builds after two warm-ups, each after a C# body change, with and without `SqlSourceOnline` `true`; the target's time from `-clp:PerformanceSummary`.  Report the medians in the hand-back.  Over about 1 s added: say so and stop; the controller decides.
- [ ] **Step 4: Commit** `test(e2e): a build that goes online against each database`.

### Task 8: Documentation

**Files:**
- Modify: `docs/manual/tool.md` ("Running in the build": when it runs, `auto`/`true`/`false`, offline, errors in the IDE, one failing query holds back its file, multi-targeting, CI, `SQLSOURCE_LOG` and where the build's logs are, `-tl:off -v:d` to see the summary, `MSBUILDLOGALLENVIRONMENTVARIABLES` logs every secret; the `--manifest` and `--online` rows of the command reference, as for the build's use), `docs/manual/settings.md` (`SqlSourceOnline` among the MSBuild properties and its status row), `README.md` step 5, `docs/manual/README.md` ("Build integration" row: Available, narrowed to the build; watch mode stays Coming), `docs/architecture/msbuild-and-manifest.md` ("Build integration" placeholder replaced: the targets, their order, inputs, the stamp, the outer build, the tool's seam, the measured cost), `docs/architecture/tool.md` (its own subsection of "Build integration options" for `--manifest` and `--online`; leave 9-3's part of the placeholder), `docs/tech-debt/TD-0025-*.md` (the online build does not have the limit but in a build of a later framework alone), `docs/tech-debt/TD-0026-*.md` (the online build has it), `src/SqlSource.Tool/AGENTS.md` if a rule changed, `examples/` only if something they show changed (they stay offline by the root property).
- [ ] **Step 1: Write them**, by root `AGENTS.md` rule 4: user docs describe effects, never internals; the packed readmes' rules; `sql` samples name their dialect.
- [ ] **Step 2: Run the documentation tests** (`dotnet test --project tests/SqlSource.Tests -- --filter-namespace "*Documentation*"` and the diagnostics doc test) **and `./pre-commit-validation.sh`.**
- [ ] **Step 3: Commit** `docs: the online build`.

### Hand-over

- Write `9-2-notes.md` in the shared notes folder: the option names and usage rules (`--manifest` excludes `--watch`: whichever of 9-2 and 9-3 merges second adds that rule), the diagnostic ids `SQLSRC024`-`SQLSRC027`, the property and target names, `SqlSourceToolCommand`, the stand-in project, what `check-package-install.sh` now checks (for 9.4), and the measured cost.
