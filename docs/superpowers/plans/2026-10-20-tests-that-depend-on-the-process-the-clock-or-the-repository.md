# Tests that depend on the process, the clock or the repository Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve TD-0065: every program a test starts runs through one helper with a limit and the tool's own `dotnet`, the shared helpers stand on a test clock, every wait has one limit, the repository is read by one route, and no object a test leaves on the shared PostgreSQL server depends on which class ran first.

**Architecture:** Three engine-neutral helpers join `tests/SqlSource.Tool.Testing`: `Programs.RunAsync` and `Programs.Dotnet`, `Eventually`, and `TestClock`, moved from `tests/SqlSource.Tool.Tests` and made the default of `CliRun`, `Hosts` and `Describers.OpenAsync`.  The default PostgreSQL image moves to `DefaultImages` of `tests/SqlSource.Testing`, which both PostgreSQL projects and both recording scripts read.  Every object a container test leaves on the server gets its text in `ScratchSchema`.

**Tech Stack:** C# 14 / .NET 10, xUnit v3 4.0.1 on Microsoft.Testing.Platform, Shouldly, Npgsql, Testcontainers, bash.

**Spec:** `docs/tech-debt/TD-0065-tests-that-depend-on-the-process-the-clock-or-the-repository.md` (Problem and Proposed fix).

## Global Constraints

- Every commit passes `./pre-commit-validation.sh`, run after the last edit.  A change to `tests/SqlSource.Tests.Postgres` is also run with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` before a push.
- Test names are `Method_Scenario_Outcome`; every test class is `sealed`; assertions are Shouldly's; helper types stay `internal`.
- A helper that one test project alone uses stays in that project; tests of a helper of `tests/SqlSource.Tool.Testing` are at the root of `tests/SqlSource.Tool.Tests`.
- An AGENTS.md or README that names a moved or renamed type is updated in the same commit (rule 6 of the root `AGENTS.md`, rule 4 for the READMEs).
- The image tag `postgres:18.6` does not change; only where it is declared moves.
- The new helpers name no engine, so phase 6's SQL Server tests can use them.
- No test is lost: the count of test methods per project and of test cases per run is compared before and after.  Baseline on `main` (2420a98): 8764 cases (8682 passed, 82 skipped); methods: SqlSource.Tests 1293, RoslynFloor 377, Tool.Tests 1497, Postgres 358, Npgsql8 3.  Count with `dotnet tests/<P>/bin/Debug/net10.0/<P>.dll -list full | grep -c 'Test method:'` after `sort -u`.
- One pull request (well under 150 files).

## Where the code differs from the TD

- Item 3 is gone: TD-0064 deleted `EndToEndTool`.  `CliRun.Real` is the real host's run.
- `TestClock` is used by `DescribeScene`, `RunLogTests`, `RunLogFileTests` and `SidecarStoreTests` today; all keep working through the moved type.
- The survey of item 7 found more leftovers than the TD names: `NpgsqlCheckTests`' types, `ExpressionTests`' `expression_amounts` and `expression_lonely`, `ScanTests`' foreign server and table, `SessionTests`' `session_gone` (dropped with no `finally`), and `DescribeCommandTests`' role and database.  All are in scope.
- `inh_parent` and `inh_child` are `ColumnTests`' own tables, not the fixture's.

## Review Focus

1. A program that never ends (MSBuild waiting on a lock, a tool that hangs): `RunAsync` kills the whole tree and throws `TimeoutException` naming the program and the limit, with what it printed — `ProgramsTests.RunAsync_ProgramThatOutlivesThePatience_IsKilledAndThrows`.
2. A test cancelled while its program runs: the tree is killed and `OperationCanceledException` is thrown, not `TimeoutException` — `ProgramsTests.RunAsync_WhileRunningThatIsCancelled_KillsTheProgramAndThrowsTheCancellation`.
3. A program that writes more than a pipe's buffer to both streams: no deadlock, both read whole — `ProgramsTests.RunAsync_ProgramThatWritesBothStreams_GivesBothWhole`.
4. `DOTNET_HOST_PATH` set to an empty string: `dotnet` from the path, as the tool does — `ProgramsTests.Dotnet_HostPathEmpty_IsDotnet` (in `ChangesTheEnvironment`).
5. A container test class that runs first on a fresh server, or after every other: passes either way — the order runs of Task 5.

---

### Task 1: The helpers `Programs.Dotnet`, `Programs.RunAsync` and `Eventually`

**Files:**
- Modify: `src/SqlSource.Tool/Projects/ProjectEvaluator.cs` (around line 274)
- Modify: `tests/SqlSource.Tool.Testing/Programs.cs`
- Create: `tests/SqlSource.Tool.Testing/Eventually.cs`
- Create: `tests/SqlSource.Tool.Tests/ProgramsTests.cs`, `tests/SqlSource.Tool.Tests/EventuallyTests.cs`
- Commit this plan with the task.

**Interfaces:**
- Produces:
  - `ProjectEvaluator.DotnetOf(Func<string, string?> getEnvironmentVariable) : string` — `internal static`; the value of `DOTNET_HOST_PATH` when it is not empty, else `"dotnet"`.  `RunAsync` of `ProjectEvaluator` calls it.
  - `Programs.Dotnet : string` — `ProjectEvaluator.DotnetOf(Environment.GetEnvironmentVariable)`.
  - `Programs.ToolStart(string workingDirectory, string temp, params string[] arguments) : ProcessStartInfo` — replaces `StartTool`: `Programs.Dotnet` with `--roll-forward LatestMajor`, `ToolPath`, the arguments, and `SetToolEnvironment`.  Starts nothing.
  - `record ProgramResult(int ExitCode, string Output, string Error)` in `Programs.cs`.
  - `Programs.RunAsync(ProcessStartInfo start, TimeSpan patience, Func<Process, Task>? whileRunning = null) : Task<ProgramResult>` — sets the three `Redirect*` to true, starts the program, closes its input, reads both streams at once, runs `whileRunning` while it runs, and waits for its exit on `TestContext.Current.CancellationToken` linked with `CancelAfter(patience)`.  Whenever it does not end normally (patience, the test's token, or `whileRunning` throwing), it kills the whole tree (ignoring `InvalidOperationException` for one that has just ended) and waits for the exit; then a cancelled test rethrows `OperationCanceledException`, a spent patience throws `TimeoutException($"'{start.FileName}' did not end within {patience}.  It wrote: {output}{error}")`, and a `whileRunning` failure rethrows that exception.
  - `Eventually.Patience : TimeSpan` = 2 minutes — the one limit of every wait of the tests.
  - `Eventually.TrueAsync(Func<bool> condition, string otherwise) : Task` and `Eventually.TrueAsync(Func<Task<bool>> condition, string otherwise) : Task` — try at once, then every 50 ms on the test's token; `TimeoutException(otherwise)` after `Patience`.
  - `Eventually.ValueAsync<T>(Func<Task<T?>> attempt, string otherwise) : Task<T>` where `T : struct` — the first value that is not null, on the same terms.
  - `Eventually.WithinAsync<T>(Task<T> task) : Task<T>` — `task.WaitAsync(Patience, TestContext.Current.CancellationToken)`.

- [ ] **Step 1: Write the failing tests**

`ProgramsTests` (`[Collection(nameof(ChangesTheEnvironment))]` for the two that set the variable, and they put back the value they found):
- `Dotnet_HostPathSet_IsThatPath`: set `DOTNET_HOST_PATH` to `"/opt/dotnet/dotnet"`; `Programs.Dotnet.ShouldBe("/opt/dotnet/dotnet")`.
- `Dotnet_HostPathEmpty_IsDotnet`: set it to `""`; `Programs.Dotnet.ShouldBe("dotnet")`.
- `RunAsync_Dotnet_GivesItsExitCodeAndOutput`: `new ProcessStartInfo(Programs.Dotnet, ["--version"])`; `ExitCode` 0, `Output.Trim()` not empty, `Error` empty.
- `RunAsync_ProgramThatWritesBothStreams_GivesBothWhole`: with `sh -c` (skip unless on path, `Assert.SkipWhen(OperatingSystem.IsWindows(), …)`), write 200 000 `o` to standard output and 200 000 `e` to standard error; both lengths, after trimming the line end, are 200 000.
- `RunAsync_ProgramThatOutlivesThePatience_IsKilledAndThrows`: `sleep 30` (or `ping -n 30 127.0.0.1` on Windows, skip unless on path), patience 200 ms; throws `TimeoutException` whose message contains `"did not end within"`, within 10 s of wall time.
- `RunAsync_WhileRunningThatIsCancelled_KillsTheProgramAndThrowsTheCancellation`: the same program, with `whileRunning` that throws `OperationCanceledException`; `Should.ThrowAsync<OperationCanceledException>` and the process given to `whileRunning` has exited afterwards.
- `RunAsync_WhileRunningThatFails_KillsTheProgramAndThrowsItsException`: `whileRunning` throws `InvalidOperationException("seen")`; that exception is thrown and the program has exited.

`EventuallyTests`:
- `TrueAsync_ConditionThatComesTrue_Returns`: a counter that is true on the third call; returns, called 3 times.
- `ValueAsync_AttemptThatGivesAValue_GivesIt`: null twice then 7; gives 7.
- `Patience_IsTwoMinutes`: `Eventually.Patience.ShouldBe(TimeSpan.FromMinutes(2))`.

`ProjectEvaluatorTests.Evaluate_Project_RunsTheDotnetThatStartedTheToolOrTheOneOnThePath` already holds `DotnetOf`'s three cases through the evaluator; it stays as it is.

- [ ] **Step 2: Run them; they fail to compile** — `dotnet build tests/SqlSource.Tool.Tests`.
- [ ] **Step 3: Implement** the produced interfaces.  `StartTool` stays until Task 2 removes its last caller.
- [ ] **Step 4: Run** `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class '*ProgramsTests' --filter-class '*EventuallyTests' --filter-class '*ProjectEvaluatorTests'`; all pass.
- [ ] **Step 5: Validate and commit** — `./pre-commit-validation.sh`, then `git commit -m "Run a test's programs with a limit and the tool's dotnet"`.

### Task 2: Every program and every wait through the helpers (items 1, 2 and 5)

**Files:**
- Modify: `tests/SqlSource.Tool.Testing/Fixtures/FixtureProjects.cs`, `tests/SqlSource.Tool.Testing/Programs.cs` (remove `StartTool`)
- Modify: `tests/SqlSource.Tool.Tests/Projects/ManifestTargetTests.cs` (all 12 tests async), `tests/SqlSource.Tool.Tests/Commands/TerminationTests.cs`, `tests/SqlSource.Tool.Tests/Processes/ProcessRunnerTests.cs`, `tests/SqlSource.Tool.Tests/ProcessGate.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Describer/ToolProcessTests.cs`, `tests/SqlSource.Tests.Postgres/Describer/SessionTests.cs`
- Modify: `tests/AGENTS.md`, `tests/README.md`

**Interfaces:**
- Consumes: Task 1's `Programs.Dotnet`, `Programs.ToolStart`, `Programs.RunAsync`, `ProgramResult`, `Eventually.*`.
- Produces: `FixtureProjects.WriteManifestAsync(string project, params string[] properties) : Task<string[]>` and `FixtureProjects.RunMSBuildAsync(string project, IEnumerable<string> arguments) : Task<(int ExitCode, string Output)>` (output then error, as today), replacing the synchronous pair.  `RunMSBuildAsync` builds its `ProcessStartInfo` on `Programs.Dotnet` with the same arguments and environment as today and runs it with `Programs.RunAsync(start, Eventually.Patience)`.

- [ ] **Step 1: Convert the callers**
  - `ManifestTargetTests`: each test becomes `public async Task`, awaiting the two new methods; assertions unchanged.
  - `ToolProcessTests`: `var result = await Programs.RunAsync(Programs.ToolStart(...), Eventually.Patience);` then the same three assertions on `result`; no `try`/`finally`.
  - `TerminationTests`, signal test: `Programs.RunAsync(Programs.ToolStart(_fixtures.Root, temp, "describe", project), Eventually.Patience, async tool => { await Eventually.TrueAsync(() => File.Exists(waiting), …); send the signal with Programs.RunAsync(new ProcessStartInfo("kill", [signal, tool.Id…]), Eventually.Patience); })`, then the same assertions on the result.  Windows test: the `cmd /c start "<title>" /wait "<Programs.Dotnet>" --roll-forward LatestMajor "<ToolPath>" describe "<project>"` start, run by `Programs.RunAsync` with `whileRunning` doing what the test does today before it waits (wait for the file through `Eventually`, the deliberate 3 s `Task.Delay`, `HasExited.ShouldBeFalse`, one run folder, `CloseConsoleAsync`); a `TimeoutException` from the run becomes `Assert.Fail($"The run did not end when its console was closed.  taskkill said: {closed}")`.  `CloseConsoleAsync` runs `taskkill` through `Programs.RunAsync`.  `AppearsAsync` is deleted.  The 3 s delay stays: it is not a limit but how long the run is seen to go on.
  - `ProcessRunnerTests`: `Request(Programs.Dotnet, …)` in the two `dotnet` tests; `ReadIdAsync` through `Eventually.ValueAsync`; `EndsAsync` replaced by `Eventually.TrueAsync(() => !IsRunning(id), "<the same message as today>")`.
  - `ProcessGate.ArrivalAsync`: `.WaitAsync(Eventually.Patience)`; its `Patience` field is deleted.
  - `SessionTests` (around line 133): the loop becomes `await Eventually.TrueAsync(async () => await BackendsOfAsync(own, application, token) == 0, "the failed session left its connection open")`.
- [ ] **Step 2: Check** that `git grep -n 'new ProcessStartInfo("dotnet"\|Task.Delay(\|WaitForExit()' tests -- '*.cs'` finds only `TerminationTests`' 3 s delay and the `Timeout.Infinite` delays of `ProjectEvaluatorTests` and `DescribeTests`.
- [ ] **Step 3: Docs** — `tests/AGENTS.md`, rule "No test of the tool waits…": a test starts a program through `Programs.RunAsync`, never `Process.Start`, and `dotnet` as `Programs.Dotnet`; a test that waits for what a program or the server does waits through `Eventually`, whose `Patience` is the one limit, which `ProcessGate` and `Programs.RunAsync` also use; replace `Programs.StartTool` with `Programs.ToolStart`.  `tests/README.md`: the library list of `tests/SqlSource.Tool.Testing` says `Programs` starts and runs a program with a limit and the tool's `dotnet`, and lists `Eventually`.
- [ ] **Step 4: Run** `dotnet test --project tests/SqlSource.Tool.Tests` and `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class '*ToolProcessTests' --filter-class '*SessionTests'`; all pass.
- [ ] **Step 5: Validate and commit** — `git commit -m "Start every program of the tests through Programs.RunAsync and wait through Eventually"`.

### Task 3: The test clock as the default (item 4)

**Files:**
- Move: `tests/SqlSource.Tool.Tests/TestClock.cs` → `tests/SqlSource.Tool.Testing/TestClock.cs` (namespace `SqlSource.Tool.Testing`, unchanged otherwise); `TestClockTests` stays at the root of `tests/SqlSource.Tool.Tests`.
- Modify: `tests/SqlSource.Tool.Testing/CliRun.cs`, `Hosts.cs`, `Describers.cs`
- Create: `tests/SqlSource.Tool.Tests/ClockedExchange.cs`
- Modify: `tests/SqlSource.Tool.Tests/PostgresScript.cs`, `PostgresEventCases.cs`, `Describing/Postgres/PostgresEventsTests.cs`, `Describing/Postgres/PostgresEventsPlanTests.cs`
- Modify: `tests/AGENTS.md`, `tests/README.md`

**Interfaces:**
- Produces:
  - `CliRun.Time` defaults to `new TestClock()`; `Hosts.Create(…, TimeProvider? time = null)` and `Hosts.Real(…, TimeProvider? time = null)` use `time ?? new TestClock()`; `Describers.OpenAsync` uses `time ?? new TestClock()`.  Comments say "a clock that stands still" where they said "the machine's clock".
  - `ClockedExchange(IDescribeExchange inner, TestClock clock, TimeSpan perCall) : IDescribeExchange` — advances the clock by `perCall` and then asks `inner`.
  - `PostgresScript.OpenAsync`/`SessionAsync` take `TimeProvider? time = null`, passed to `Describers.OpenAsync`.
  - `PostgresEventCases.CallTime = TimeSpan.FromMilliseconds(7)`; `PostgresEventCases.DescribeAsync` opens on a new `TestClock` through `new ClockedExchange(script.Play(), clock, CallTime)`.

- [ ] **Step 1: Write the failing assertions** — in `DescribeAsync_QueryWithAParameter_WritesPrepareTypesAndDeallocateInOrder` every event's `durationMs` is `(LogValue)7L` (prepare, columns, types, check, deallocate); in the test of `PostgresEventsPlanTests` at line 88, `Member(explain, "durationMs").ShouldBe((LogValue)7L)`.  Replace the two `ShouldBeOfType<LogValue.Number>()`.
- [ ] **Step 2: Run them; they fail** (the machine's clock gives 0 or a number that is not 7).
- [ ] **Step 3: Implement** the move, the defaults and `ClockedExchange`.  `DescribeScene` keeps its own `Clock` and sets `Run.Time` as today.
- [ ] **Step 4: Run** `dotnet test --project tests/SqlSource.Tool.Tests` and `dotnet test --project tests/SqlSource.Tests.Postgres`; all pass.  A test that fails because it relied on the machine's clock gets a clock of its own and a comment saying why; list any in the commit message.
- [ ] **Step 5: Docs** — `tests/README.md` lists `TestClock` in `tests/SqlSource.Tool.Testing` and says a run, a host and a session of the tests stand on it unless a test brings a clock; `tests/AGENTS.md` gains that sentence in the rule of the tool's tests.
- [ ] **Step 6: Validate and commit** — `git commit -m "Stand the shared helpers on TestClock and assert the describer's durations"`.

### Task 4: The repository by one route (item 6)

**Files:**
- Create: `tests/SqlSource.Testing/DefaultImages.cs`
- Modify: `tests/SqlSource.Tests.Postgres/PostgresContainer.cs`, `PostgresContainerTests.cs`, `Describer/FixtureVersionTests.cs`, `tests/SqlSource.Tests.Postgres.Npgsql8/AppContainer.cs`
- Modify: `tools/record-fixtures.sh`, `tools/describe-end-to-end.sh`
- Modify: `tests/SqlSource.Tests/Diagnostics/DiagnosticsDocumentTests.cs`, `tests/SqlSource.Tests/SqlSource.Tests.csproj`
- Modify: `AGENTS.md` (rule 5), `tests/AGENTS.md`, `tests/README.md`, `tests/SqlSource.Tests.Postgres/README.md`, `tools/AGENTS.md`, `tools/README.md`, `examples/AGENTS.md`

**Interfaces:**
- Produces: `internal static class DefaultImages` in namespace `SqlSource.Testing` with `public const string Postgres = "postgres:18.6";`, its comment carrying what `PostgresContainer.DefaultImage`'s comment says (the scripts read this line by its shape, the Compose file repeats the tag, a major and a minor), and that each engine's default image is one constant here.  `PostgresContainer.DefaultImage` is deleted; `PostgresContainer.Image` falls back to `DefaultImages.Postgres`.

- [ ] **Step 1: Move the image** — the two tests and `PostgresContainer` use `DefaultImages.Postgres`; `AppContainer` builds on `DefaultImages.Postgres`, loses the regex, `System.IO`, `System.Text.RegularExpressions` and `partial`, and its comment says it reads the default image of `DefaultImages`.
- [ ] **Step 2: The scripts** — both read `sed -n 's/.*const string Postgres = "\(.*\)";.*/\1/p' tests/SqlSource.Testing/DefaultImages.cs`, with the path in a variable beside `PROJECT`; each script fails with a message when the value is empty (`[[ -n "$image" ]] || { echo '…' >&2; exit 1; }`).  Run `bash -c 'source <(sed -n "/default_image=/p" tools/record-fixtures.sh)'`-style check by hand: print what each `sed` gives and see `postgres:18.6`.
- [ ] **Step 3: The document** — `DiagnosticsDocumentTests.Lines` reads `Path.Combine(RepositoryRoot.Folder, "docs", "manual", "diagnostics.md")`; the `None` item of `diagnostics.md` and its comment leave `SqlSource.Tests.csproj`.
- [ ] **Step 4: Docs** — every sentence that names `PostgresContainer.DefaultImage` or "the line of `PostgresContainer.cs` that declares `DefaultImage`" names `DefaultImages.Postgres` in `tests/SqlSource.Testing/DefaultImages.cs`: rule 5 of `AGENTS.md`, `tests/AGENTS.md` (the UsesPostgres rule: `PostgresContainer.cs` is no longer "the one place in `tests/` that names the image"), `tests/README.md` (the `SqlSource.Testing` row lists `DefaultImages`; the Npgsql 8 paragraph; what each project takes), the image section of `tests/SqlSource.Tests.Postgres/README.md`, `tools/AGENTS.md` (the `sed` of the two scripts; no regex of `AppContainer` any more), `tools/README.md` (Recording scripts) and `examples/AGENTS.md` rule 5.  Check with `git grep -n 'DefaultImage\b\|declares `DefaultImage`'` — only this plan and TD-0065 may remain.
- [ ] **Step 5: Run** `dotnet test --project tests/SqlSource.Tests -- --filter-class '*DiagnosticsDocumentTests'`, `dotnet test --project tests/SqlSource.Tests.Postgres.Npgsql8` and `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class '*PostgresContainerTests' --filter-class '*FixtureVersionTests'`; all pass.
- [ ] **Step 6: Validate and commit** — `git commit -m "Name the default image in DefaultImages and read the manual through RepositoryRoot"`.

### Task 5: Every object left on the server in ScratchSchema (item 7)

**Files:**
- Modify: `tests/SqlSource.Tests.Postgres/Describer/ScratchSchema.cs`
- Modify: `Describer/ColumnTests.cs`, `TableMatchTests.cs`, `ExplainTests.cs`, `SetOperationTests.cs`, `ExpressionTests.cs`, `NpgsqlCheckTests.cs`, `ScanTests.cs`, `SessionTests.cs`, `DescribeCommandTests.cs` (all under `tests/SqlSource.Tests.Postgres/`)
- Modify: `tests/AGENTS.md`, `tests/SqlSource.Tests.Postgres/README.md`

**Interfaces:**
- Produces, in `ScratchSchema`, each with a `<summary>` saying what it is and what a test may rely on, SQL that can run again, and the text moved verbatim unless said otherwise:
  - `ColumnTypes` (`column_types`, `column_empty`), `NotValid` (`column_not_valid`; still run only after the test's `SkipBelow(18)`), `MaterializedUsers` (`column_users`), `InsteadRule` (`ir_target`, `ir_other`, the rule), `DroppedColumn` (`match_dropped`), `ExplainRows` (`walk_explain_rows` and its row), `LeafAliasView` (`setop_leaf_alias`), `PairsFunction` (`setop_pairs()`), `Amounts` (`expression_amounts`), `LonelyView` (`expression_lonely`), `CheckTypes` (`"My Schema"."Status"`, `check_d2`, `check_pair`, the long enum, `check_canary`), with `LongTypeName` moved here from `NpgsqlCheckTests`.
  - `Inheritance` — `inh_parent`, `inh_child`, and the `DROP NOT NULL` of `inh_child.v` before 18 in the same text: `DO $$ BEGIN IF current_setting('server_version_num')::int < 180000 THEN ALTER TABLE inh_child ALTER COLUMN v DROP NOT NULL; END IF; END $$;` — so the object is the same whichever test makes it, and `ColumnTests` no longer branches on `Major` to make it.
  - `ForeignServer(NpgsqlConnectionStringBuilder connection) : string` — one loopback server `scratch_loop` and its user mapping, made as `ColumnTests.ForeignServer` makes `fp_loop` today; `ForeignPartitions(connection)` (`fp_remote`, `fp_parted*`), `ForeignTable(connection)` (`ft_remote`, `ft_foreign`) and `ScanForeign(connection)` (`scan_foreign` over `orders`) each start with it.  `ScanTests`' own `scan_loop` and its DO block go.
  - `LogRole` (a DO block that creates `sqlsrc_wombat` with `LOGIN PASSWORD '<Secrets.Marker>'` unless `pg_roles` has it) and `LogDatabase` (`CREATE DATABASE sqlsrc_numbat OWNER sqlsrc_wombat`, run by `DescribeCommandTests` only when `pg_database` lacks it, since `CREATE DATABASE` cannot run in a DO block); the names stay constants of `DescribeCommandTests` or move here, whichever keeps one spelling of each.
- The class comment says: every object that a test of `UsesPostgres` leaves on the server has its text here alone, made by SQL that can run again and never dropped; an object that changes what another test sees while it exists (`public."numeric"`, the schema `"user"`, `describer_canary`, `command_widgets`, `error_path_widgets`, `session_gone`) is made by its test and dropped in a `finally`.

- [ ] **Step 1: Move the texts** and point each call site at its constant.  `SessionTests`' `session_gone` test drops the table with `DROP TABLE IF EXISTS` in a `finally` too, keeping the drop in the exchange hook that the test is about.
- [ ] **Step 2: Check** — `git grep -nE '"(CREATE|ALTER|DROP) |\b(CREATE|ALTER) (TABLE|VIEW|MATERIALIZED|FUNCTION|TYPE|DOMAIN|SCHEMA|EXTENSION|SERVER|RULE|FOREIGN|ROLE|DATABASE)' tests/SqlSource.Tests.Postgres -- '*.cs'` finds DDL only in `ScratchSchema`, in a test that drops it in a `finally`, in `ErrorPathTests`' table (dropped in a `finally`) and inside rolled-back transactions.
- [ ] **Step 3: Docs** — `tests/AGENTS.md`, the UsesPostgres rule: "An object that a test leaves on the server has its text in `Describer/ScratchSchema.cs`, made by SQL that can run again; one that changes what other tests see is dropped by its test in a `finally`."  The Postgres README's Running section says the same in a sentence.
- [ ] **Step 4: Order runs** — the default image, three ways, all passing:
  1. `dotnet test --project tests/SqlSource.Tests.Postgres` (the default order).
  2. The same with a temporary, uncommitted `[assembly: Xunit.TestClassOrderer(typeof(…ReversedClassOrderer))]` that orders the classes by name descending (xUnit v3 4.0.1 has no command-line order switch; `--seed` moves only the order of collections, and the project has one).
  3. Each class of the collection alone on a fresh container: for each `[Collection(UsesPostgres.Name)]` class, `dotnet test --project tests/SqlSource.Tests.Postgres --no-build -- --filter-class '<full name>'`, so every class runs first.
  Then `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres` once.  Remove the orderer; `git status` shows none of it.
- [ ] **Step 5: Validate and commit** — `git commit -m "Give every object left on the server its text in ScratchSchema"`.

### Task 6: Resolve TD-0065

**Files:**
- Delete: `docs/tech-debt/TD-0065-tests-that-depend-on-the-process-the-clock-or-the-repository.md`
- Modify: `docs/tech-debt/README.md` (remove its row; `Next id` unchanged)

- [ ] **Step 1:** `git grep -n 'TD-0065'` — remove every reference outside this plan.
- [ ] **Step 2: Count** the test methods of each project and the cases of a full run, and compare with the baseline in Global Constraints; every method of the baseline is still listed (`comm -23 baseline after` is empty, but for renames this plan made, listed in the PR).
- [ ] **Step 3: Validate and commit** — `./pre-commit-validation.sh` and the Postgres project on `postgres:14.24`, then `git commit -m "Resolve TD-0065"`.
