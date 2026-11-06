# Query generation 10.4: `diagnose`, `--scrub` and close - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `sqlsource diagnose --query <file>:<name> --out <zip>` describes one query again through a capturing exchange and writes a renamed, proven bundle and its mapping; `--no-obfuscate` writes a raw one; `--scrub` renames a raw one with no database; the user documentation, the issue template and the close of phase 10 and of the epic.

**Architecture:** A `DiagnoseCommand` at the tool's root beside `ReplayCommand`, wired into `Cli` as a third command.  The run reuses `describe`'s planning (`DescribeCommand`'s unit, manifests and planner, with the `--query` file as the one `.sql` filter) and `DatabaseRuns`, which gains an optional capture: the database's exchange is wrapped in a `CapturingExchange`, a describe failure is kept on the `QueryWork` and reported without being written, and once the session closes the capture is written through `CaptureJson.Write(DescribeCapture, ConnectionSecret)` inside `DatabaseRuns`, so the secret never leaves it.  `Bundles/` gains the raw bundle of a described query (`DiagnoseBundles`) and its `query.sql` (`BundleQueryFile`); renaming is 10.1's `BundleRenamer.RenameAsync` with `EngineRenamings.For`, written by `BundleWriter` and `MappingFile`.

**Tech Stack:** C# (net8.0 tool), System.CommandLine, xunit + Shouldly, Testcontainers (PostgreSQL, SQL Server).

**Spec:** `docs/superpowers/specs/2026-11-04-query-generation-phase-10-bug-reports-design.md`, sections "`diagnose`", "The issue template", "Diagnostics", "10.4", "Documentation", "Changes to the epic outline"; the merged code of 10.1 to 10.3 (`docs/architecture/bundles.md`).

## Where the merged code differs from the spec, and the calls this plan makes

- **`--out` refused or not written is `SQLSRC218`** ("File could not be changed"), which `--log` already uses for a path ending in `.sql`, `.sql.json`, `.csproj`, `.sln` or `.slnx` and for a file that cannot be written.  No new id: one id per actionable cause.  An `--out` that holds `=` or `;`, or is blank, is a wrong command line, as `--log`'s is (`DescribeCommand.ProblemOfLogPath`).
- **A `--query` value with no `:`, an empty file or name part, or a file part that is no `.sql` path, is a wrong command line** (a usage line with no id, `src/SqlSource.Tool/AGENTS.md`'s rule).  Only "names no query of the file" needs the plan, and is `SQLSRC292`.  A file that no type claims is `describe`'s `SQLSRC212`, as a `.sql` filter is.
- **New ids** (the next free, `SQLSRC292` onward):
  - `SQLSRC292` "Query is not in the file": `--query` names no query of the file, or one whose output needs no description (`sql`).
  - `SQLSRC293` "Bundle cannot be made safe to post": a `RenamingRefusal` of step `Token`, `RequestSql` or `PlanString`.
  - `SQLSRC294` "Renamed bundle does not reproduce the query's answer": a refusal of step `Replay`.
  - `SQLSRC295` "Bundle is not a raw bundle": `--scrub` of a renamed bundle, or of one with no `bundle.json`.
- **A session that does not open makes no bundle.**  `BundleInfo.ServerVersion` is required and the capture holds nothing of the query; the open's error is reported as `describe` reports it, exit `1`.  A query the describer refuses makes a bundle with its failure, exit `0`, the error logged and not printed (`Reporter.ReportWithoutWriting`), and the line `sqlsource: the query was refused: SQLSRC<n>`.
- **A capture that holds the connection's secret** is a fault of the describer, `SQLSRC200`, as an entry that holds it is (`EntrySecretCheck`).
- **`--scrub` takes `--out` alone** (and `--help`), as the spec's synopsis has it.
- **`EngineRenamings.For` becomes non-nullable**: both arms are built.
- **The packed `replay`** is proven by `tools/check-package-install.sh` on a committed bundle folder of `tests/SqlSource.Tool.Tests/Fixtures/Bundles/`, so no zip is committed.
- **The private route** (owner, 2026-11-04) is said in the manual, each refusal's help, `--no-obfuscate`'s line and the issue template, and nowhere as tech debt or deferred work: open an issue on GitHub that says a raw bundle is ready, do not attach it, and a maintainer says how to send it.

## Global Constraints

- `diagnose` requires `--out`, with `--scrub` too; there is no default folder (owner, R3-F2).  An `--out` inside a project's folder is allowed; the printed mapping line and the manual advise a path outside the project's folder (owner, R5-F1).
- The mapping is `<bundle>` + `BundleFiles.MappingSuffix` (`.mapping.json`) beside the zip, never inside it, and no key is written.
- `diagnose` writes no sidecar and deletes none; it describes the one query whatever its hash.  Its connections, `.env`, `--verbose` and `--log` are `describe`'s; it has no `--online`, `--manifest`, `--watch`, `--check`, `--force` or `--database`.
- A bundle `diagnose` describes is written through `CaptureJson.Write(DescribeCapture, ConnectionSecret)`; `--scrub` writes through `BundleRenamer`, which uses `CaptureJson.Write(DescribeCapture)`.
- Exit codes: `0` when a bundle was written (a refused query included), `1` otherwise, by `Cli.ExitCodeOf`.
- No raw log in a bundle; a server error keeps only its code and the parameters it names; never blank out a part, refuse instead; a user's name spelled as a keyword or engine word stays verbatim, and numbers are kept (owner, 2026-11-04).  The manual says both.
- Every text a command prints that the tool did not write goes through `OneLine`; a path in a message is a full path; a refusal's token is printed (the design's step 12), and the manual says not to paste a refusal's line into an issue.
- Repository rules: root `AGENTS.md` rule 4 (docs in the same PR, effects not internals in user docs, packed readmes' absolute links), `docs/manual/AGENTS.md` ("When a phase lands"), `src/SqlSource/Diagnostics/AGENTS.md` (four places for a new diagnostic), `src/SqlSource.Tool/AGENTS.md` (host, `Outcome`, reporter, `UsageCheck`).  `src/SqlSource.Tool/AGENTS.md` is at its 8 KB limit: a rule of `diagnose` goes in `src/SqlSource.Tool/Bundles/AGENTS.md`.

## Review Focus

1. **A query whose dialect and database come from a settings file**, with markers `diagnose` drops (`-- summary:`, `-- column:`, `-- output:`): its bundle must replay to `reproduced`, so `query.sql` carries the effective dialect (with its options) and database, and the entry's `hash` and name position are those `BundleReplay.Plan` gives the bundle's `query.sql`.  Test in Task 2.
2. **A `--no-obfuscate` bundle then `--scrub`** must give the same kind of bundle `diagnose` gives directly, and `replay` of each reproduces.  Tests in Tasks 3 and 4.
3. **A file of several queries**: only the named one is described, the other queries' sidecar is untouched, and no sidecar is written or deleted.  Test in Task 3.
4. **An `--out` whose folder does not exist, or whose mapping cannot be written after the zip was**: `SQLSRC218` at that path, exit `1`.  Test in Task 3.
5. **A query name given in another case than its `-- name:`** is no query of the file (`SQLSRC292`, names compared by ordinal, as C# members are).  Test in Task 3.

---

### Task 1: The diagnostics, and `DatabaseRuns`' capture

**Files:**
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs` (four descriptors and `All`), `src/SqlSource/AnalyzerReleases.Unshipped.md`, `src/SqlSource/Diagnostics/AGENTS.md` ("`SQLSRC200` to `SQLSRC295` are taken"), `docs/manual/diagnostics.md` (index rows and `## SQLSRC292` to `## SQLSRC295`, the tool's range paragraph), `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs` if it lists ids.
- Modify: `src/SqlSource.Tool/Describing/QueryWork.cs`, `src/SqlSource.Tool/Describing/DatabaseRuns.cs`
- Create: `src/SqlSource.Tool/Describing/Capture/RunCapture.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsCaptureTests.cs` (with `DatabaseRunsScene`)

**Interfaces:**
- Produces: `ToolDiagnostics.QueryNotInFile` (`SQLSRC292`, args: query name, file path, reason), `BundleCannotBeMadeSafe` (`SQLSRC293`, args: reason, place, token or `(none)`), `RenamedBundleDoesNotReproduce` (`SQLSRC294`, args: place), `BundleIsNotRaw` (`SQLSRC295`, args: bundle path, reason).  The help of `SQLSRC293` and `SQLSRC294` is the private route: make a raw bundle with `--no-obfuscate`, keep it, and open an issue that says a raw bundle is ready without attaching it.
- Produces: `sealed class RunCapture(string engine)` with `DescribeCapture Capture`, `string? Text` (the capture as `CaptureJson.Write(capture, secret)` wrote it, once the session closed), `ServerInfo? Server`.
- Produces: `QueryWork.Failure` (`DescribeFailure?`, redacted, set when the describer refused it).
- Produces: `DatabaseRuns.RunAsync(..., RunCapture? capture = null)`: with a capture, the database's exchange is `new CapturingExchange(host.Exchange(name), capture.Capture)`, a query's failure is reported through `ReportWithoutWriting` and kept on `QueryWork.Failure`, `capture.Server` is the session's server, and after the session closes `capture.Text` is the written capture, or an `InvalidOperationException` under the guard (so `SQLSRC200`) when the capture holds the secret.  Without one, nothing changes.

- [ ] **Step 1: Write the failing tests**: `RunAsync_WithACapture_RecordsEveryCallAndWritesItOnClose` (Text reads back by `CaptureJson.Read` with the calls of the fake describer), `RunAsync_WithACapture_ARefusalIsKeptAndNotWritten` (`Failure.Descriptor.Id` set, standard error empty, `reporter.UnwrittenCount == 1`), `RunAsync_ACaptureThatHoldsTheSecret_IsSQLSRC200`, `RunAsync_WithoutACapture_IsUnchanged` (existing `DatabaseRuns` tests pass untouched).  Add the four descriptors to the diagnostics tests' expectations as they require.
- [ ] **Step 2: Run them, see them fail**: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~DatabaseRunsCaptureTests"`.
- [ ] **Step 3: Implement** the descriptors (written out in full, literal ids), their four places, `RunCapture`, `QueryWork.Failure` and the capture parameter.
- [ ] **Step 4: Run** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~DatabaseRuns"` and `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Diagnostics"`: pass.
- [ ] **Step 5: Commit** `feat(tool): DatabaseRuns captures a run for diagnose; SQLSRC292 to SQLSRC295`.

### Task 2: The raw bundle of a described query

**Files:**
- Create: `src/SqlSource.Tool/Bundles/BundleQueryFile.cs`, `src/SqlSource.Tool/Bundles/DiagnoseBundles.cs`
- Modify: `src/SqlSource.Tool/Bundles/Renaming/EngineRenamings.cs` (non-nullable) and its callers' tests
- Test: `tests/SqlSource.Tool.Tests/Bundles/BundleQueryFileTests.cs`, `tests/SqlSource.Tool.Tests/Bundles/DiagnoseBundlesTests.cs`

**Interfaces:**
- Consumes: `RunCapture` (Task 1), `PlannedQuery`, `PlannedFile`, `BundleReplay.Plan`.
- Produces: `BundleQueryFile.Of(PlannedQuery query, string fileText, SqlDialect dialect) -> string`: `-- dialect:` (the effective dialect with its options, as the generator reads it back), `-- database:` (the query's logical database), then the query's own `-- name:` (with its shape), `-- param:` (without `-> <C# type>`), `-- token:` and `-- token-ignore:` lines and its SQL as the file holds them; every other marker dropped.  Only the six markers `SqlRenamer.QueryFile` reads are ever written.
- Produces: `DiagnoseBundles.Raw(PlannedQuery query, string fileText, SqlDialect dialect, RunCapture capture, SidecarEntry? entry, DescribeFailure? failure, ToolHost host) -> Bundle`: `bundle.json` with `renamed: false`, `PackageVersion.Prefix`, `SidecarFormat.Version`, the capture's engine, `capture.Server.Version` and `DriverVersion`, `host.Runtime`, `host.OperatingSystem`, the outcome (`BundleOutcome.Entry`, or `Failure(id, DescribeStepName.Of(step))`); `capture.json` = `capture.Text`; `expected.json` the one entry as a sidecar of it alone (`SidecarWriter`), its `Hash` and `NameSpan` those `BundleReplay.Plan` gives the bundle's `query.sql`; no `explain-settings.txt`.
- Produces: `EngineRenamings.For(DatabaseEngine) -> IEngineRenaming`.

- [ ] **Step 1: Write the failing tests**: `Of_AQueryOfAFileOfSeveral_HoldsItAlone`; `Of_DialectAndDatabaseFromSettings_WritesThemAsMarkers` (a planned query from `Plans.Postgres` with `Settings["database"] = "app"`); `Of_DropsEveryOtherMarker` (`-- summary:`, `-- column:`, `-- output:`, `-- param: @id int -> UserId` keeps `-- param: @id int`); `Of_ReadsBackToTheSameSampleSqlAndParameters` (`BundleReplay.Plan` of the result gives `SampleSql.Build` and parameters equal to the original's); `Raw_AnEntry_ReplaysToReproduced` and `Raw_ARefusal_ReplaysToTheSameFailure` (through `BundleReplay` with a fixture's capture answering a real `PostgresDescriber`, as `BundleReplays` does).
- [ ] **Step 2: Run them, see them fail**: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~BundleQueryFileTests|FullyQualifiedName~DiagnoseBundlesTests"`.
- [ ] **Step 3: Implement** both, reading the query's region from the parser's spans of `fileText`.
- [ ] **Step 4: Run** the same filter and `--filter "FullyQualifiedName~Renaming"`: pass.
- [ ] **Step 5: Commit** `feat(tool): the raw bundle of a described query`.

### Task 3: `sqlsource diagnose`

**Files:**
- Create: `src/SqlSource.Tool/DiagnoseCommand.cs` (the command line), `src/SqlSource.Tool/Bundles/DiagnoseRun.cs` (the run of `--query`, and of `--scrub`)
- Modify: `src/SqlSource.Tool/Cli.cs` (`Running.Diagnose`, `BuildCommands`, `CheckUsageOfTheCommand`, `ReadCommandLineAsync`'s `ReadsValuesAs`, `OpenLog`, `InvokeAsync`), `src/SqlSource.Tool/DescribeCommand.cs` (expose the planning steps `diagnose` shares: `FindUnit`, `FindManifestsAsync`, and a `PlanFromAsync` that takes its `RunFilters`), `src/SqlSource.Tool/UsageCheck.cs` remarks, `src/SqlSource.Tool/Bundles/AGENTS.md`
- Test: `tests/SqlSource.Tool.Tests/Commands/DiagnoseTests.cs`, `DiagnoseScrubTests.cs`, `UsageCheckTests.cs` (vocabulary of the "read the same" tests), `SecretTests.cs` (a `diagnose` line)

**Interfaces:**
- Consumes: Tasks 1 and 2; `BundleRenamer.RenameAsync`, `BundleDescribers.For`, `BundleReplay`, `BundleWriter.Write`, `MappingFile.Write`, `RenamingKey.Random`.
- Produces: the command `diagnose` with `[<path>]` (one unit, no `.sql`), `--project` (repeatable), `--connection` (repeatable), `--query <file>:<name>` (the last `:`), `--out <path>`, `--no-obfuscate`, `--verbose`, `--log <path>`, `--scrub <bundle>`.  `DiagnoseCommand.Name = "diagnose"`, `Create`, `Runs`, `IsVerbose`, `LogPathOf`, `LogOptionOf`, `CheckUsage`, `ReadsValuesAs`, `Bind`, as `ReplayCommand` has them.

Lines (exact text; `<path>` a full path in quotes by `UsageValue.Quoted`):
- `sqlsource: the query was refused: SQLSRC<n>`
- `sqlsource: bundle <path> written` (renamed), then `sqlsource: mapping <path> written`, then `sqlsource: the mapping holds your own names: keep it private, outside your project's folder, since a build may copy a .json file into its output`
- `--no-obfuscate`: `sqlsource: raw bundle <path> written`, then `sqlsource: this bundle holds your own names and SQL, and must not be posted: open an issue that says a raw bundle is ready, without attaching it, and a maintainer will say how to send it`
- Usage lines (no id), each naming positions, never tokens: `command 'diagnose' needs --query or --scrub`; `option '--out' is required`; `the value of option '--query' at position <n> is not <file>:<name>`; `the file of option '--query' at position <n> is not a .sql path`; `option '--scrub' takes no other option but '--out'`; `--query` beside `--scrub`; `--out`'s blank or `=`/`;` value as `--log`'s; a `.sql` argument.

- [ ] **Step 1: Write the failing tests**, in process through `CliRun` with the real `PostgresDescriber` answered by a fixture's capture (`host.Exchange = _ => new ReplayingExchange(capture)`), and SQL Server's likewise for one case:
  - `Diagnose_AQuery_WritesARenamedBundleAndItsMapping` (exit `0`; the zip reads by `BundleReader`, `renamed: true`; the mapping beside it parses with `mappingVersion` 1; `BundleLeaks` finds no name of the fixture's `query.sql`; `replay <zip>` prints `reproduced`).
  - `Diagnose_NoObfuscate_WritesARawBundleAndNoMapping`, `Diagnose_ScrubOfTheRawBundle_ReplaysToReproduced`, `Diagnose_ScrubOfARenamedBundle_IsSQLSRC295`, `Diagnose_ScrubOfAFolderWithoutBundleJson_IsSQLSRC295`.
  - `Diagnose_ARefusedQuery_WritesItsFailure` (`bundle.json` outcome `failure` with id and step; standard error empty; exit `0`; no `RunAgainLine`).
  - `Diagnose_ASessionThatDoesNotOpen_WritesNothing` (exit `1`, the open's error printed).
  - `Diagnose_AFileOfSeveralQueries_DescribesTheOneAndWritesNoSidecar` (Review Focus 3).
  - `Diagnose_ANameInAnotherCase_IsSQLSRC292`, `Diagnose_AnSqlOutputQuery_IsSQLSRC292` (Review Focus 5).
  - `Diagnose_OutEndsInSql_IsSQLSRC218`, `Diagnose_OutInAFolderThatDoesNotExist_IsSQLSRC218` (Review Focus 4).
  - `Diagnose_ATokenThatCannotBeMadeSafe_IsSQLSRC293WithThePrivateRoute` (a capture whose answer holds a character no word rule reads; the line names the place and token; nothing is written).
  - Each usage line above; `--help` lists the options.
- [ ] **Step 2: Run them, see them fail**: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~Diagnose"`.
- [ ] **Step 3: Implement** the command and the run.  The run: unit, manifests, the plan with the `--query` file as the one filter; `RunDecisions.Decide(plan, RunMode.Force, errors)`; the named query of the filtered file (ordinal), every other query of the run set to `LeftOut`; its database's connection as `DescribeRun` resolves it; `DatabaseRuns.RunAsync` with a `RunCapture`; no `SidecarStore`.  Then the raw bundle (Task 2); for `--no-obfuscate` write it; else `BundleDescribers.For`, `EngineRenamings.For`, `BundleRenamer.RenameAsync` with `RenamingKey.Random()`, a refusal reported as `SQLSRC293`/`SQLSRC294`; write the zip by `host.CreateFile`, then the mapping.  `--scrub`: `BundleReader.Read`, `BundleReplay.CaptureOf`, `Plan`, `BundleDescribers.For`, `ExpectedOf` (their errors as `replay` reports them), `SQLSRC295`, then the same renaming and writing.
- [ ] **Step 4: Run** `dotnet test tests/SqlSource.Tool.Tests`: pass.
- [ ] **Step 5: Commit** `feat(tool): sqlsource diagnose, --no-obfuscate and --scrub`.

### Task 4: The end-to-end tests and the packed `replay`

**Files:**
- Create: `tests/DatabaseTests/Postgres/Describer/DiagnoseCommandTests.cs`, `tests/DatabaseTests/SqlServer/Describer/DiagnoseCommandTests.cs`
- Modify: `tools/check-package-install.sh` (replay one committed folder of `tests/SqlSource.Tool.Tests/Fixtures/Bundles/` with the packed tool, expecting `reproduced` and exit `0`), `tools/README.md`, `tests/DatabaseTests/Postgres/README.md`, `tests/DatabaseTests/SqlServer/README.md`, `tests/README.md` where it lists command tests

**Interfaces:** Consumes Task 3's command.

- [ ] **Step 1: Write the tests**, as each engine's `DescribeCommandTests` runs `describe` (a project whose settings file names the database `app`, a `--connection app=` to the container): `Diagnose_AQueryOfApp_LeaksNoNameOfTheSchemaAndReproduces` (`BundleLeaks` against the fixtures' `schema.sql` and the query; then `replay` → `reproduced`), and `Diagnose_NoObfuscateThenScrub_Reproduces`.
- [ ] **Step 2: Run** `dotnet test tests/DatabaseTests/Postgres --filter "FullyQualifiedName~DiagnoseCommandTests"` and the same for `tests/DatabaseTests/SqlServer`: pass (Docker running).
- [ ] **Step 3: Change the script**, and run `tools/check-package-install.sh`: passes, printing the replay's outcome.
- [ ] **Step 4: Commit** `test: diagnose against each engine; the packed replay`.

### Task 5: The user documentation, the issue template and the close

**Files:**
- Create: `.github/ISSUE_TEMPLATE/bug-report.yml` (what was expected and what was given, the versions of `sqlsource`, the database and .NET, the bundle; the lines that the mapping and a raw bundle are never attached, and that a user with a raw bundle says so in the issue)
- Modify: `docs/manual/tool.md` (a workflow section "Reporting a wrong model" anchored `report-a-bug`: making a bundle, `--out` outside the project's folder, what the bundle holds and what is renamed, names spelled as the database's own words and numbers kept, a refusal and the private route, `--no-obfuscate` and `--scrub`, not pasting a refusal's line into an issue; the command reference's synopsis, `diagnose`'s options, its exit codes; `replay`'s section extended to a user's own bundle; `#logs` says a log is the user's own record, sent privately when a maintainer asks, and a bundle is what an issue attaches; Known issues: a SQL Server query that spells a name in another case than its table cannot be renamed, send it raw (TD-0102's effect, no id)), `docs/manual/README.md` (row "Bug-report bundles" Available, a "Find what you need" row for reporting a wrong model), `README.md` (FAQ: "How do I report a wrong model?", absolute links), `src/SqlSource.Tool/README.md` (`Use`: `diagnose` and `replay`, absolute links)
- Modify: `docs/architecture/bundles.md` (a `diagnose` section: the command, the run, `RunCapture`, the raw bundle, `query.sql`, the renaming and the writing; "No command writes a bundle" and "No command calls it" made true; `EngineRenamings.For` non-nullable), `docs/architecture/tool.md` (the commands, `Cli`'s steps for `diagnose`), `docs/architecture/README.md` (three commands), `docs/architecture/AGENTS.md` if it lists files
- Modify: `docs/tech-debt/TD-0034-*.md` (the capture paragraph, Fix and Trigger: `diagnose` writes a capture through the secret's check, and an error keeps its code and the parameters it names alone), `docs/tech-debt/TD-0046-*.md` (trigger: a user's bundle of `diagnose`), `docs/tech-debt/TD-0102-*.md`, `TD-0103-*.md`, `TD-0104-*.md` ("No user sees it yet" sentences made true now)
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (rows 10.1 to 10.4 Done; the paragraph under the table says phase 10 is closed and with it the epic)
- Modify: comments that say what `diagnose` "will" do (`tests/SqlSource.Tool.Tests/RenamingBundles.cs` and any `git grep -n "diagnose will"` finds)

- [ ] **Step 1: Write** the pages, then read every page of phase 10 whole, and run `docs/manual/AGENTS.md`'s two greps (internals, and "coming/today/yet/will") over the changed pages, fixing every hit but text the user sees anyway.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Documentation|FullyQualifiedName~Diagnostics"` and `tools/actionlint.sh`: pass.
- [ ] **Step 3: Commit** `docs: bug reports with diagnose; phase 10 and the epic closed`.
