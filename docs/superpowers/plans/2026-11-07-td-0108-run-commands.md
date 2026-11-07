# TD-0108 items 5, 6, 16, 19, 20: the run's commands and context — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix TD-0108 items 5, 6, 16, 19 and 20 of `src/SqlSource.Tool` (and item 20's dead member of `src/SqlSource`) as a refactor: the tool's output, exit codes and logs stay exactly the same.

**Architecture:** Five commits, one per item, each with its doc and AGENTS.md updates and its TD-0108 strike.  The shared option declarations and command-line helpers move to a new root class `SharedOptions`, and `Cli` iterates one list of `ToolCommand` records; the plan and connection steps move out of `DescribeCommand` into a new root class `PlanSteps`; a new `RunContext(ToolHost, Reporter, CancellationToken)` replaces the three parameters that travel together.

**Tech Stack:** C# 12 / .NET 8 tool, System.CommandLine, xUnit + Shouldly.

**Spec:** `docs/tech-debt/TD-0108-duplicated-and-uneven-patterns-in-src.md`, items 5, 6, 16, 19, 20 (Problem and Proposed fix).

## Global Constraints

- No behavior change: every line on standard output and standard error, every exit code and every log event stays the same, in the same order.
- Read root `AGENTS.md`, `src/SqlSource.Tool/AGENTS.md` and the `AGENTS.md` of every folder touched.  `src/SqlSource.Tool/AGENTS.md` is at 8,184 of 8,192 bytes: a line that grows there must be shortened elsewhere or pushed to a folder AGENTS.md or `docs/architecture/tool.md`.  `tests/SqlSource.Repository.Tests/Documentation/AgentsFileTests.cs` checks it.
- An AGENTS.md or `docs/architecture/` page that names a moved member is updated in the same commit (`docs/architecture/tool.md`, `docs/architecture/bundles.md`, `src/SqlSource.Tool/AGENTS.md`, `src/SqlSource.Tool/Planning/AGENTS.md`, `src/SqlSource.Tool/Bundles/AGENTS.md`).
- Test changes are only what a changed signature forces; no test is renamed or rewritten otherwise (unit 109-tool refactors the tests after this one).
- TD-0108 is shared by nine units.  Strike only items 5, 6, 16, 19, 20, in place, in both lists: the Problem item becomes `N. ~~<the item's text>~~  Fixed: <what is true now>.` and the Proposed fix item `N. ~~<the fix's text>~~  Done.` (add a short clause after `Done` only where the fix differs from the proposal).  Nothing is renumbered.  Do not touch the row in `docs/tech-debt/README.md` (other items stay open).
- TD-0037 (`Logging/` and the run's steps refer to each other) must not get worse: nothing in `Logging/` may reference `RunContext`, `PlanSteps`, `SharedOptions` or `ToolCommand`.
- Before each commit: `./format.sh`, then `./pre-commit-validation.sh` must pass.  Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Tests to run while working: `dotnet test --project tests/SqlSource.Tool.Tests` (and `tests/SqlSource.Tests` for task 1).

## Review Focus

1. A command line that runs the help of a command (`describe --help`, `diagnose --help`) must still open no log, give no reporter, and empty no `SQLSOURCE_LOG` file: `SharedOptions.Runs` must keep the `ReferenceEquals(parsed.Action, parsed.CommandResult.Command.Action)` test.  Covered by existing Cli/log tests; the task 3 implementer runs them.
2. `describe --online` must still not read `SQLSOURCE_LOG`, while `replay` and `diagnose` always do; and `describe --watch` and `--online` must never print `Cli.RunAgainLine`.  Existing tests in `tests/SqlSource.Tool.Tests/Commands/` and `Logging/` hold these.
3. `diagnose --query` must still log, not write, a plan error at another `.sql` file, and write one at its own file: the predicate of task 4 must give the same answer as `elsewhereIsLogged` did.  Existing `DiagnoseCommand` tests hold it.
4. The order of `replay`'s and `diagnose --scrub`'s errors (read, capture, plan, describer, expected; then scrub's not-raw) and `replay`'s "not renamed" line before the capture is read must not move.
5. A watch pass keeps a reporter of its own: the `RunContext` of a pass carries the pass's reporter, never the session's.

---

### Task 1: Item 20 — one line prefix, one path-error filter, no dead member

**Files:**
- Create: `src/SqlSource.Tool/FileErrors.cs`
- Modify: `src/SqlSource.Tool/Cli.cs`, `UsageCheck.cs`, `DescribeCommand.cs`, `DiagnoseCommand.cs`, `ReplayCommand.cs`, `Logging/RunTrace.cs`, `Logging/RunLogFile.cs`, `Bundles/DiagnoseRun.cs`, `Bundles/ReplayLines.cs`, `Describing/DescribeRun.cs`, `Watching/WatchSession.cs`, `Describing/Postgres/PostgresConnection.cs`, `src/SqlSource/Generation/Emitting/QueryMethods/ListWriter.cs`
- Delete: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/ListWriterTests.cs` (its one test only calls `ParameterLimitOf`; the limits are pinned by both engines' end-to-end tests, as `ListWriter.LimitOf`'s comment says)
- Modify docs: `docs/architecture/tool.md` (the root's row of the folders table, `## Folders`, names `FileErrors`), TD-0108 item 20.

**Interfaces:**
- Produces: `internal const string Cli.LinePrefix = "sqlsource: ";` and `internal static class FileErrors { public static bool IsPathError(Exception exception) }` (true for `IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException`), namespace `SqlSource.Tool`.

- [ ] **Step 1:** Add `Cli.LinePrefix` with a summary saying it starts every line the tool writes of its own, which the build's `SqlSourceRunOnline` reads.  Remove the four private `Prefix` constants (`ReplayCommand`, `Logging/RunTrace.cs`, `Bundles/DiagnoseRun.cs`, `Bundles/ReplayLines.cs`) and use `Cli.LinePrefix`.  Replace every string literal in `src/SqlSource.Tool` that starts with `sqlsource: ` by `Cli.LinePrefix + "…"` or `$"{Cli.LinePrefix}…"` (constants stay constants: `RunAgainLine`, `WatchSession.ChangeDetectedMarker`, `WatchingMarker`, `DescribeRun.SummaryPrefix`).  `grep -rn '"sqlsource: ' src/SqlSource.Tool` must then find nothing.
- [ ] **Step 2:** Add `FileErrors.IsPathError` and use it in the three `when (...)` filters (`Logging/RunLogFile.cs`, `Bundles/DiagnoseRun.cs`, `Describing/Postgres/PostgresConnection.cs`).  `Projects/RunProjects.cs`'s narrower `IOException or UnauthorizedAccessException` is a different rule and stays.
- [ ] **Step 3:** Delete `ListWriter.ParameterLimitOf` and `ListWriterTests.cs`.
- [ ] **Step 4:** Run `dotnet test --project tests/SqlSource.Tool.Tests` and `dotnet test --project tests/SqlSource.Tests`.  Expected: PASS, no test changed but the deleted file.
- [ ] **Step 5:** Docs and TD strike for item 20; `./format.sh`; `./pre-commit-validation.sh`; commit `refactor(tool): one line prefix and one path-error filter, and no dead ListWriter member (TD-0108 item 20)`.

### Task 2: Item 6 — one preparation of a replay

**Files:**
- Create: `src/SqlSource.Tool/Bundles/PreparedReplay.cs`
- Modify: `Bundles/BundleReplay.cs`, `Bundles/ReplayOutcome.cs`, `ReplayCommand.cs`, `Bundles/DiagnoseRun.cs`, `Bundles/Renaming/RenamingProof.cs`
- Test (forced): `tests/SqlSource.Tool.Tests/BundleReplays.cs` (its two `BundleReplay.RunAsync` calls)
- Docs: `docs/architecture/bundles.md` (`## replay` › `### The steps` and its diagram, `### The run of --scrub`), `src/SqlSource.Tool/Bundles/AGENTS.md` line on `BundleReplay.cs` if it names the steps, TD-0108 item 6.

**Interfaces:**
- Produces:
  - `internal sealed record PreparedReplay(PlannedQuery Query, SqlDialect Dialect, DescribeCapture Capture, IQueryDescriber Describer, ReplayOutcome? Expected)` — what a replay needs, as the steps gave it.
  - `public static Outcome<PreparedReplay, ToolDiagnostic> BundleReplay.Prepare(Bundle bundle, string path, DescriberRegistry describers, CancellationToken token)`: in order `CaptureOf`, `EngineOf`, `Plan`, `BundleDescribers.For(dialect, describers, bundle.ExplainSettings, engine, path)`, `ExpectedOf`; the first failure is returned.  (The spec's `Prepare(bundle, path, describers)` plus the token `Plan` needs.)
  - `public static Task<ReplayOutcome> BundleReplay.RunAsync(PreparedReplay replay, IRunLog log, TimeProvider time, CancellationToken token)` replaces the seven-parameter one (also item 19's `BundleReplay.cs:186`).  `RenamingProof` keeps its steps apart and builds the record itself with its own describer.
  - `ReplayOutcome.Refused.ShortText` property: `"{Id} at {Step}"` when `Step` is set, else `Id`.  Used by `ReplayCommand.ShortTextOf` and `RenamingProof.TextOf`.
  - `private static string? DiagnoseRun.OutPathOf(UsageValue output, ToolHost host, Reporter reporter)`: the full path of `--out`, or null when `RefusedOutOf` refused it, which is reported.  Used by `QueryAsync` and `ScrubAsync`.

- [ ] **Step 1:** Add the record, `Prepare`, the new `RunAsync`, `ShortText` and `OutPathOf`.
- [ ] **Step 2:** `ReplayCommand.ReplayAsync`: read; the "not renamed" line; `Prepare`; `RunAsync`; the rest as today.  `DiagnoseRun.ScrubAsync`: `OutPathOf`; read; `Prepare`; the not-raw check; `RenameAndWriteAsync` with `prepared.Describer` and `prepared.Dialect`.  `DiagnoseRun.QueryAsync` uses `OutPathOf`.
- [ ] **Step 3:** Fix `BundleReplays.cs`'s two calls to build a `PreparedReplay`.
- [ ] **Step 4:** Run `dotnet test --project tests/SqlSource.Tool.Tests`.  Expected: PASS.
- [ ] **Step 5:** Docs and TD strike; format; validate; commit `refactor(tool): replay and diagnose --scrub prepare a bundle in one step (TD-0108 item 6)`.

### Task 3: Item 5 — shared options and one list of commands

**Files:**
- Create: `src/SqlSource.Tool/SharedOptions.cs`, `src/SqlSource.Tool/ToolCommand.cs`
- Modify: `Cli.cs`, `DescribeCommand.cs`, `ReplayCommand.cs`, `DiagnoseCommand.cs`, and every caller of a moved member (`grep` for `DescribeCommand.ValuesOf|ReadsAs|ReadsAsOne|ProblemOf|SplitConnectionOf|LogOption|LogVariable|ProjectOption|ConnectionOption|EnvFileOption` across `src` and `tests`)
- Docs: `docs/architecture/tool.md` (`### Reading a command line`, `### The steps of a run`, `## Folders` root row, `### --manifest and --online` line on `LogPathOf`), `docs/architecture/bundles.md` (`## replay`, `## diagnose` › `### The command line` where they say how `Cli` reads them), `src/SqlSource.Tool/AGENTS.md` (the rule "An option is declared by the rules at the top of `DescribeCommand.Create`" now names `SharedOptions`), TD-0108 item 5.

**Interfaces:**
- Produces, in `SharedOptions` (`internal static class`, namespace `SqlSource.Tool`):
  - Constants `Project = "--project"`, `Connection = "--connection"`, `Verbose = "--verbose"`, `Log = "--log"`, `EnvFile = "--env-file"`, `LogVariable = "SQLSOURCE_LOG"`.  `DescribeCommand` keeps its own options' constants (`DatabaseOption`, `ManifestOption`, `OnlineOption`, …).
  - Factories `Option<string[]> ProjectOption()`, `Option<string[]> ConnectionOption()`, `Option<bool> VerboseOption(string description)` (each command keeps its own text), `Option<string> LogOption()`, `Option<string> EnvFileOption()`, with today's descriptions, help names and arities.  The comment of declaration rules at the top of `DescribeCommand.Create` moves to the class's remarks; the commands' "The rules of DescribeCommand.Create hold" comments point here.
  - `bool Runs(ParseResult parsed, string command)`, `bool IsVerbose(ParseResult parsed, string command)`, `string? LogPathOf(ParseResult parsed, string command, bool readsVariable, Func<string, string?> getEnvironmentVariable)`.
  - Moved from `DescribeCommand` unchanged in behavior: `ValuesOf`, `ReadsAs`, `ReadsAsOne`, `ProblemOfPath`, `ProblemOfLogPath`, `ProblemOfEnvFile`, `ProblemOfConnection`, `SplitConnectionOf`.
- Produces `internal sealed record ToolCommand` (one per command, in `Cli.Commands`, an `ImmutableArray<ToolCommand>` in the order describe, replay, diagnose), with these members, each required:
  - `string Name`, `Func<Command> Create`, `Func<Usage, IReadOnlyList<string>> CheckUsage`, `Func<Usage, ParseResult, bool> ReadsValuesAs`;
  - `Func<ParseResult, bool> ReadsLogVariable` (describe: `OnlineOf(parsed) == OnlineMode.Off`; others: `true`);
  - `Func<ParseResult, bool> WritesOneLine` (describe: `OnlineOf(parsed) != OnlineMode.Off`; others: `false`) — the reporter's `oneLine`;
  - `Func<ParseResult, bool> MayPoint` (describe: online off and not `Watches`; others: `false`) — `RunState.MayPoint` is this and no log path;
  - `Action<ParseResult, Usage, ToolHost, Reporter, Action<LogValue>> Bind` (replay and diagnose ignore the last).
- `Cli`'s six lists become loops over `Commands`: `BuildCommands` (adds each `Create()`), `CheckUsageOfTheCommand`, the `ReadsValuesAs` checks, `RunningOf` (gives the `ToolCommand?` that `SharedOptions.Runs`, null for help and version; the `Running` enum goes), `OpenLog` and `InvokeAsync`.  `state.Watches` still comes from `DescribeCommand.Watches`.  `DescribeCommand.Runs` stays as `SharedOptions.Runs(parsed, Name)` (it is used inside the command); the other commands' `Runs`, `IsVerbose` and `LogPathOf`, and describe's `IsVerbose` and `LogPathOf`, are removed.

- [ ] **Step 1:** Create `SharedOptions` and move the members; point every caller at it.
- [ ] **Step 2:** Create `ToolCommand` and `Cli.Commands`; rewrite the six lists.
- [ ] **Step 3:** Run `dotnet test --project tests/SqlSource.Tool.Tests`.  Expected: PASS, with only the renames step 1 forces in tests.
- [ ] **Step 4:** `grep -rn '"--verbose"\|"--log"\|"--project"\|"--connection"\|"--env-file"' src/SqlSource.Tool` finds only `SharedOptions.cs` (UsageCheck may name none of them).
- [ ] **Step 5:** Docs and TD strike; format; validate; commit `refactor(tool): shared options and one list of commands in Cli (TD-0108 item 5)`.

### Task 4: Item 16 — the plan and connection steps out of `DescribeCommand`

**Files:**
- Create: `src/SqlSource.Tool/PlanSteps.cs`
- Modify: `DescribeCommand.cs`, `Bundles/DiagnoseRun.cs`, `Watching/WatchSession.cs`, `tests/SqlSource.Tool.Testing/CliRun.cs` (its `DescribeCommand.PlanAsync` call)
- Docs: `docs/architecture/tool.md` (`### The steps of a run`, `## A describe run` and its diagram's subgraph name, `### Connections`, `## Watch mode` › `### The steps a pass reuses` and the diagram, `## Folders` root row, `### --manifest and --online`), `docs/architecture/bundles.md` (`## diagnose` diagram and `### The run of --query`), `src/SqlSource.Tool/Planning/AGENTS.md` ("`DescribeCommand.cs` reports them" → `PlanSteps.cs`), `src/SqlSource.Tool/Bundles/AGENTS.md` (the `DiagnoseRun.cs` line names `DescribeCommand`), `src/SqlSource.Tool/AGENTS.md` (the `Outcome` rule's example `DescribeCommand.FindUnit`), TD-0108 item 16.

**Interfaces:**
- Produces `internal static class PlanSteps` (namespace `SqlSource.Tool`): the steps of a run up to its plan, and its connections, which `describe`, each pass of a watch and `diagnose` share.  Moved from `DescribeCommand`, same behavior: `PlanAsync`, `PlanUnitAsync` (now internal), `FindUnit`, `FindManifestsAsync`, both `PlanFromAsync`, `FilesNotNamed`, `IsAtOneOf`, `IsReportedByTheCompiler`, `ConnectionsOf(Usage)`, `UnitFolderOf`, `EnvFileOf`, `ArgumentsOf`.  Signatures keep `ToolHost, Reporter, CancellationToken` in this task (task 5 changes them).
- The eight-parameter `PlanFromAsync` takes `Func<RunPlan, ToolDiagnostic, bool> isLoggedOnly` in place of `bool isOnline, bool elsewhereIsLogged`: an error for which it is true goes to `reporter.ReportWithoutWriting`, any other to `reporter.Report`.  It takes the plan too, which the spec's `Func<ToolDiagnostic, bool>` does not, because `diagnose`'s rule is "at a planned `.sql` file that no file filter names", known only once the plan is made.  The usage overload passes `(_, error) => isOnline && IsReportedByTheCompiler(error)`; `diagnose` passes `PlanSteps.IsAtAFileNotNamed`, `internal static bool IsAtAFileNotNamed(RunPlan plan, ToolDiagnostic error) => IsAtOneOf(error, FilesNotNamed(plan))`.
- `DescribeCommand` keeps the command line (`Create`, `Bind`, `CheckUsage` and its checks, `OnlineOf`, `Watches`, `ModeOf`, `ReadsValuesAs`), `OptionsOf` and `DescribePlanAsync`.

- [ ] **Step 1:** Create `PlanSteps`, move the members, replace the two bools; point `DescribeCommand`, `DiagnoseRun`, `WatchSession` and `CliRun` at it.
- [ ] **Step 2:** Run `dotnet test --project tests/SqlSource.Tool.Tests`.  Expected: PASS.
- [ ] **Step 3:** `wc -l src/SqlSource.Tool/DescribeCommand.cs` is about 600 or fewer.
- [ ] **Step 4:** Docs and TD strike; format; validate; commit `refactor(tool): the plan and connection steps leave DescribeCommand for PlanSteps (TD-0108 item 16)`.

### Task 5: Item 19 — a `RunContext`, and no list of seven parameters

**Files:**
- Create: `src/SqlSource.Tool/RunContext.cs`, `src/SqlSource.Tool/Describing/ConnectionSources.cs`, `src/SqlSource.Tool/Bundles/DescribedQuery.cs`, `src/SqlSource.Tool/Describing/Describers/DeclaredParameter.cs`
- Modify: `PlanSteps.cs`, `DescribeCommand.cs`, `ReplayCommand.cs`, `DiagnoseCommand.cs`, `Projects/RunProjects.cs`, `Bundles/DiagnoseRun.cs`, `Bundles/DiagnoseBundles.cs`, `Describing/DatabaseRuns.cs`, `Describing/DescribeRun.cs`, `Describing/DescribeOptions.cs`, `Describing/SidecarStore.cs`, `Describing/Describers/DescribeFailures.cs`, `Describing/Postgres/PostgresErrors.cs`, `Describing/SqlServer/SqlServerErrors.cs`, `Watching/WatchSession.cs`
- Test (forced): `tests/SqlSource.Tool.Tests/DatabaseRunsScene.cs`, `Projects/RunProjectsTests.cs`, `Bundles/DiagnoseBundlesTests.cs`, `Describing/SidecarStoreTests.cs`, `Describing/Describers/DescribeFailuresTests.cs`, `Bundles/Renaming/SqlServer/SqlServerErrorRenamingTests.cs` if it calls a changed signature, `tests/SqlSource.Tool.Testing/CliRun.cs`
- Docs: `docs/architecture/tool.md` (`### The steps of a run`, `### Connections`, `### Describing`, `## Folders` root row), `docs/architecture/bundles.md` (`### The raw bundle of a described query`, `### The run of --query`), TD-0108 item 19.  Say in `tool.md`'s folders section that `RunContext` is at the root and `Logging/` does not use it.

**Interfaces:**
- Produces `internal sealed record RunContext(ToolHost Host, Reporter Reporter, CancellationToken CancellationToken)` (namespace `SqlSource.Tool`), made in each command's action from the token System.CommandLine gives it, and for each watch pass with the pass's own reporter.
- These take a `RunContext` in place of `ToolHost`, `Reporter` and `CancellationToken` (or the two of them it had): `PlanSteps.PlanAsync`, `PlanUnitAsync`, `FindUnit`, `FindManifestsAsync`, both `PlanFromAsync`; `DescribeCommand.DescribePlanAsync(plan, usage, unit, mode, context, counted)`; `ReplayCommand.ReplayAsync(argument, verbose, context)`; `RunProjects.FindAsync(unit, named, context)`, `FromManifestAsync(manifestPath, context)`; `DiagnoseRun.QueryAsync(usage, query, output, raw, context)`, `ScrubAsync(scrub, output, context)`, its private `PlanAsync`, `DescribeAsync`, `RenameAndWriteAsync(raw, describer, dialect, outPath, context)`, `OutPathOf`, `Write`; `DatabaseRuns.RunAsync`, `RunDatabaseAsync`; `DescribeRun.RunAsync(plan, options, context, counted)`, `DescribeRun.ConnectionsOf`; `WatchSession.RunAsync(unit, usage, parsed, context, counted)`.
- `DatabaseRuns.RunAsync(ImmutableArray<PlannedDatabase> selected, ImmutableArray<FileWork> files, Connections connections, RunContext context, DatabaseMode mode)`; `DatabaseMode` becomes `internal sealed record DatabaseMode(OnlineMode Online, bool Offline, RunCapture? Capture)` (same file or `Describing/DatabaseMode.cs`).  `RunDatabaseAsync(database, files, queries, connections, context, mode)`.
- `DatabaseRuns.ReportFailure` (8) becomes `private static (DescribeFailure Failure, ToolDiagnostic Error) Redacted(Guard guard, Func<DescribeFailure?> failureOf, PlannedQuery planned, PlannedDatabase database, ServerInfo? server, OnlineMode online)`; each of its two callers reports the error as it does today (the open failure always written; a query's failure written only when the run has no capture).
- `internal sealed record ConnectionSources(ImmutableArray<ConnectionArgument> Given, string UnitFolder, string? EnvFile)`: replaces `DescribeOptions`' `Connections`, `UnitFolder` and `EnvFile` (`DescribeOptions(Mode, HasFilter, Sources, Databases, Online)`); `PlanSteps.SourcesOf(Usage usage, RunUnit unit, ToolHost host)` makes it from `ConnectionsOf`, `UnitFolderOf` and `EnvFileOf`, for `OptionsOf` and `diagnose`.  `DescribeRun.ConnectionsOf(RunPlan plan, ImmutableArray<PlannedDatabase> selected, ImmutableArray<PlannedDatabase> describable, ConnectionSources sources, RunContext context)`.
- `SidecarStore`: a nested `internal sealed record FileActions(Action<string, string> Move, Action<string> Delete, Action<TimeSpan, CancellationToken> Wait)` with `static FileActions System` (today's `MoveOverwriting`, `DeleteFile`, `WaitFor`); the test overload is `Apply(outcomes, reporter, time, onTheWhole, FileActions files, cancellationToken)`.
- `internal sealed record DescribedQuery(RunCapture Capture, SidecarEntry? Entry, DescribeFailure? Failure)` replaces `DiagnoseRun`'s private `DescribedTarget`; `DiagnoseBundles.Raw(PlannedQuery query, string fileText, SqlDialect dialect, DescribedQuery described, ToolHost host)`, with the same `ArgumentException`s (parameter name `described`).
- `internal readonly record struct DeclaredParameter(string Written, string? Type)` (the parameter as the SQL writes it and the type its marker declares); `DescribeFailures.DeclaredType(DescribeRequest query, DeclaredParameter parameter, string reason, DescribeStep step, EquatableArray<string> serverLines, string help)`.

- [ ] **Step 1:** Add `RunContext` and move the 18 methods onto it, outermost first (`DescribeCommand.Bind`, `ReplayCommand.Bind`, `DiagnoseCommand.Bind` make it; `WatchSession` makes one per pass).
- [ ] **Step 2:** `DatabaseMode`, `Redacted`, `ConnectionSources`, `FileActions`, `DescribedQuery`, `DeclaredParameter`.
- [ ] **Step 3:** Fix the forced test call sites.
- [ ] **Step 4:** Run `dotnet test --project tests/SqlSource.Tool.Tests`.  Expected: PASS.
- [ ] **Step 5:** Check no method of `src/SqlSource.Tool` named in item 19 still takes seven or more parameters, and no method takes `ToolHost`, `Reporter` and `CancellationToken` together; `grep -rn "RunContext" src/SqlSource.Tool/Logging` finds nothing.
- [ ] **Step 6:** Docs and TD strike; format; validate; commit `refactor(tool): a RunContext carries the host, the reporter and the token (TD-0108 item 19)`.

### Task 6 (lead): full validation, describer floor images, review

- [ ] `./pre-commit-validation.sh --full`.  Task 5 changes the describers' error builders (`PostgresErrors`, `SqlServerErrors`), so also run `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/DatabaseTests/Postgres` and `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/DatabaseTests/SqlServer`.
- [ ] Shared notes: write `108-run-notes.md` in the run's shared notes folder with the new names (`SharedOptions`, `ToolCommand`, `PlanSteps`, `RunContext`, `PreparedReplay`, `ConnectionSources`, `DescribedQuery`, `DeclaredParameter`, `SidecarStore.FileActions`, `Cli.LinePrefix`, `FileErrors`) for 109-tool.
- [ ] `/branch-review:review --base origin/main`.
