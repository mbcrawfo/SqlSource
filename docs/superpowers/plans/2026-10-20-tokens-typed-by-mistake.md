# Stop Printing and Logging Tokens Typed by Mistake Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The `sqlsource` tool prints a token of the command line, and writes it to the log's `arguments`, only when it cannot be a secret typed in the wrong place; otherwise it names the token by its position.

**Architecture:** Every value that `describe` reads comes from `Usage` (what `UsageCheck` read, each value with its position), and System.CommandLine's values must agree with it for every option that takes one, or nothing runs.  A path is printed when its token holds no `=` and no `;`; an unknown option, a `--database` name and a `--connection` name are printed when they are within a small edit distance of a name the tool or the plan knows.  `LoggedArguments` holds `run.start` to the same rule.

**Tech Stack:** C# (`net8.0`), System.CommandLine, xUnit v3, Shouldly.

**Spec:** [`docs/tech-debt/TD-0023-path-without-an-equals-sign-is-printed-as-given.md`](../../tech-debt/TD-0023-path-without-an-equals-sign-is-printed-as-given.md), its Problem and Proposed fix sections, with the run's notes: this reverses the choice that `Run_ConnectionStringGivenWithoutAName_IsNeverPrinted` pinned.

## Global Constraints

- Every commit passes `./pre-commit-validation.sh` (root `AGENTS.md`, Commits).  Run it after the last edit of each task, before the commit.
- A wrong command line's lines have no id and never repeat a token unless this plan's rule allows it (`src/SqlSource.Tool/AGENTS.md`).
- `Reporting/OneLine.cs` puts on one line every text the tool prints and did not write itself.
- A diagnostic whose message changes updates `docs/manual/diagnostics.md` in the same commit.
- The `arguments` member keeps its name; an event's members are never renamed.
- Overlap with the other open units (PostgreSQL describer nullability, generator array keys and generic constraints) stays nil: touch nothing under `Describing/Postgres/` or `src/SqlSource/Generation/`.

## Where the code differs from the TD file

- The contract unit (#85) already redacts a connection's secret parts in printed, verbose and logged text.  This plan does not touch that.
- `SQLSRC204` and `SQLSRC220` name a project of the run: a project of the solution, or a unit `.csproj` that exists.  That path is printed, logged and used as an error's location throughout the run, so it is not a token that a rule could hide.  They stay as they are.  `SQLSRC207` is the one `--project` error that prints a token as typed.
- `--log` is not in the TD.  Its path is printed by `SQLSRC218` at the error's location.  So that no rule has to hide that location, `CheckUsage` refuses a `--log` value that holds `=` or `;`, by its position, as it refuses an empty one.  A path from `SQLSOURCE_LOG` is not a command-line token and is not held to this.
- `run.start` is written before the plan exists, so `LoggedArguments` cannot know whether a `--database` name or a `--connection` name is close to a database of the plan.  It writes them by position always.  The `database` events and `run.end` name every selected database, and `connectionSource` says which got its connection from `--connection`.
- What is deliberately left: a token that holds neither `=` nor `;` and stands where a path stands (the unit, a `.sql` path, a `--project` value) is printed and logged as a path.  TD-0023 is narrowed to that rather than deleted.

## The rules

| Token | Printed | Otherwise |
|---|---|---|
| Unknown option (cut at its first `=` or `:`) | within distance 2, ordinal, of a name or alias of any option of the tool | `sqlsource: unknown option at position <n>` |
| Path of `describe`, `.sql` path, `--project` value | the token holds no `=` and no `;` | `The path at position <n>` in the message |
| `--database` value in the summary | equal to a database of the plan ignoring case (the plan's label, as today), or within distance 2 ignoring case of one | `--database at position <n>: 0 described, 0 skipped, 0 failed` |
| `--connection` name in the help of `SQLSRC213` | within distance 2, ignoring case, of a database of the plan | `the value of --connection at position <n> names no database of this run` |
| `arguments` of `run.start` | command, options, a `--log` value, a path or `--project` value with no `=` and no `;` | the value replaced by `***`, any `--option=` or `--option:` before it kept |

Distance is the optimal string alignment distance (Levenshtein with adjacent transpositions), with `NearName.MaxDistance = 2`.

## Review Focus

- **A `--connection` value with no name whose first word is close to a database** (`--connection "Host=db;Password=x"` beside a database `hosts` prints `Host`, the connection string's first keyword and not its secret, and nothing after the first `=`).  Test: `Run_ConnectionStringWithoutANameCloseToADatabase_PrintsOnlyItsFirstWord` in Task 4.
- **A path with `=` in a folder of the working directory** (the `OddPaths` fixture) must still print: the rule reads the token, not the full path.  Test: `Find_RelativePathInAFolderWithAnEqualsSign_PrintsTheFullPath` in Task 3.
- **The same `.sql` path given twice, once with `=`**: the filter keeps the first spelling, so the message follows the first token.  Test: `Plan_SqlPathGivenTwice_IsNamedByTheFirstToken` in Task 3.
- **A short unknown option such as `-x` or `--`** is within distance 2 of `-h` and is still printed by name, as today.  Test: the existing `Run_DoubleHyphen_IsAnUnknownOption` and `DescribeTests` line 53 keep passing in Task 1.
- **An option of `describe` given before `describe`** (`--force describe`) is still named, since it is an option of the tool.  Test: the existing `Check_OptionOfACommandGivenBeforeTheCommand_IsUnknownThere` keeps passing in Task 1.

---

### Task 1: Name an unknown option only when it is close to one of the tool's

**Files:**
- Create: `src/SqlSource.Tool/Reporting/NearName.cs`
- Modify: `src/SqlSource.Tool/UsageCheck.cs`
- Test: `tests/SqlSource.Tool.Tests/Reporting/NearNameTests.cs` (create), `tests/SqlSource.Tool.Tests/Commands/UsageCheckTests.cs`, `tests/SqlSource.Tool.Tests/Commands/CliTests.cs`, `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`

**Interfaces:**
- Produces: `internal static class NearName { public const int MaxDistance = 2; public static bool IsNear(string given, string known, bool ignoreCase); }` in namespace `SqlSource.Tool.Reporting`.

- [ ] **Step 1: Write the failing tests**

`NearNameTests`: `IsNear("--conection", "--connection", false)` true; `IsNear("--conncetion", "--connection", false)` true (one transposition); `IsNear("--force", "--force", false)` true; `IsNear("-pS3cret", "--project", false)` false; `IsNear("BILING", "billing", true)` true; `IsNear("BILING", "billing", false)` false; `IsNear("Host", "postgres", true)` false.

`UsageCheckTests`:
- `Check_UnknownOptionFarFromEveryOption_IsNamedByItsPosition`: `["describe", "App.csproj", "-pS3cret"]` gives `["sqlsource: unknown option at position 3"]`.
- `Check_UnknownOptionCloseToAnOptionOfAnotherCommand_IsNamed`: `["--forse", "describe"]` gives `["sqlsource: unknown option '--forse'"]`.
- Change `Check_AnyTokenThatStartsWithAHyphen_IsAnOption`: `-` and `--` keep their name; `-s3cret` gives `unknown option at position 2`.  Split into two theories.
- Change `Check_UnknownOptionWithALineBreak_IsNamedOnOneLine`: the token `"--forc\ne"` is within distance 2 of `--force` and gives `["sqlsource: unknown option '--forc e'"]`.  The old token, far from every option, gets `Check_UnknownOptionWithALineBreakFarFromEveryOption_IsNamedByItsPosition`: `unknown option at position 2`.

`CliTests`: `Run_TwoUnknownOptions_ReportsTheFirstAlone` expects `unknown option at position 1`; `Run_UnknownOptionAfterAnotherMistake_ReportsTheOptionAlone` expects `unknown option at position 4`.

`SecretTests`: add `Run_SecretTypedAsAShortOption_IsNamedByItsPosition`: `describe -p` + `Secrets.Marker` gives exit 1, empty output, `"sqlsource: unknown option at position 2\n"`.

- [ ] **Step 2: Run the tests, see the new ones fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "*UsageCheckTests" --filter-class "*NearNameTests" --filter-class "*CliTests" --filter-class "*SecretTests"`
Expected: the new and changed facts FAIL; `NearName` does not compile until Step 3.

- [ ] **Step 3: Implement**

`NearName.IsNear`: optimal string alignment distance over two rows plus the one before them, comparing `char`s ordinally or with `char.ToUpperInvariant` on both sides when `ignoreCase`; return early false when the lengths differ by more than `MaxDistance`.  `UsageCheck.Check` collects the names and aliases of every option of `root` and of every command under it, once, and the unknown-option line names the token when `NearName.IsNear(name, known, ignoreCase: false)` holds for one, through `OneLine.Of`, and otherwise `unknown option at position {index}`.  Update the class's remarks.

- [ ] **Step 4: Run the tests, see them pass**

Same command.  Expected: PASS.

- [ ] **Step 5: Validate and commit**

Update `src/SqlSource.Tool/AGENTS.md` (the rule of a wrong command line) and `docs/architecture/tool.md#reading-a-command-line` (the unknown-option bullet) to the new rule.  Run `./pre-commit-validation.sh`; commit `Name an unknown option only when it is close to one of the tool's`.

### Task 2: Read every value of describe from Usage, with its position

Behaviour stays the same but for `--log`.  This is the step that gives later tasks their positions.

**Files:**
- Modify: `src/SqlSource.Tool/DescribeCommand.cs`, `src/SqlSource.Tool/Cli.cs`, `src/SqlSource.Tool/Describing/ConnectionArgument.cs`, `src/SqlSource.Tool/Describing/DescribeOptions.cs`, `src/SqlSource.Tool/Describing/DescribeRun.cs`, `tests/SqlSource.Tool.Testing/CliRun.cs`
- Test: `tests/SqlSource.Tool.Tests/Commands/UsageCheckTests.cs`, `tests/SqlSource.Tool.Tests/Commands/LogTests` or wherever `--log` usage lines are tested (find `is not a path`)

**Interfaces:**
- Produces:
  - `DescribeCommand.ReadsValuesAs(Usage usage, ParseResult parsed) : bool` replaces `ReadsConnectionsAs`: the values of `--project`, `--database`, `--connection` and `--log` are the same, in order, ordinally.
  - `DescribeCommand.Bind(ParseResult parsed, Usage usage, ToolHost host, Reporter reporter, Action<LogValue> counted)`.
  - `DescribeCommand.PlanAsync(Usage usage, ToolHost host, Reporter reporter, CancellationToken)`.
  - `DescribeCommand.ValuesOf(Usage usage, string option) : ImmutableArray<UsageValue>` (private is fine) and `ArgumentsOf(Usage)`.
  - `ConnectionArgument` gains `public int Position { get; init; }`, the `UsageValue.Position` it was read from.
  - `DescribeOptions.Databases : ImmutableArray<UsageValue>`, each name once ignoring case, first kept.  `SidecarCheck.Compare` is given their texts.

- [ ] **Step 1: Write the failing tests**

`UsageCheckTests`: rename the `ReadsConnectionsAs` facts to `ReadsValuesAs` and add `ReadsValuesAs_ProjectReadInAnotherOrder_IsFalse` (a `ParseResult` of `describe --project a --project b` against a `Usage` whose `--project` values are `b`, `a`).  A `CheckUsage` fact `CheckUsage_LogPathWithAnEqualsSignOrASemicolon_IsNamedByItsPosition` with `--log` `a=b.log` and `--log=a;b.log`: `sqlsource: the value of option '--log' at position <n> holds '=' or ';', which a log's path may not`.

- [ ] **Step 2: Run them, see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "*UsageCheckTests"`
Expected: FAIL (no `ReadsValuesAs`, no `--log` line).

- [ ] **Step 3: Implement**

`PlanAsync` and `OptionsOf` read the unit, the `.sql` paths, `--project`, `--database` and `--connection` from `usage.Values`; the flags stay on `parsed`.  `Cli` passes `commandLine.Usage` to `Bind` and calls `ReadsValuesAs`.  `CliRun.PlanAsync` builds its `Usage` with `UsageCheck.Check(Cli.BuildCommands(), args)`.  Keep the remarks of `ReadsValuesAs` on why: the run reads `Usage`, the log is built from it, and a disagreement could put a token where the other reader would not.  In the `--log` branch of `CheckUsage`, after the white-space check, refuse a value with `=` or `;`.

- [ ] **Step 4: Run the whole tool's tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS, the old expectations unchanged.

- [ ] **Step 5: Validate and commit**

Update `ReadsConnectionsAs` in `src/SqlSource.Tool/AGENTS.md`, `docs/architecture/tool.md` and `docs/tech-debt/TD-0024-...md`; add the `--log` rule to `docs/architecture/tool.md#reading-a-command-line` and to the `--log` row of `docs/manual/tool.md#options`.  `./pre-commit-validation.sh`; commit `Read every value of describe from Usage, with its position`.

### Task 3: Name a path by its position when it holds = or ;

**Files:**
- Modify: `src/SqlSource.Tool/UsageValue.cs`, `src/SqlSource/Diagnostics/ToolDiagnostics.cs` (the formats of `SQLSRC201`, `202`, `203`, `207`, `212`, `222`), `src/SqlSource.Tool/Projects/RunUnitFinder.cs`, `src/SqlSource.Tool/Projects/RunProjects.cs`, `src/SqlSource.Tool/Planning/RunFilters.cs`, `src/SqlSource.Tool/Planning/FileFilter.cs`, `src/SqlSource.Tool/Planning/RunPlanner.cs`, `src/SqlSource.Tool/DescribeCommand.cs`
- Test: `tests/SqlSource.Tool.Tests/Projects/RunUnitTests.cs`, `tests/SqlSource.Tool.Tests/Projects/RunProjectsTests.cs`, `tests/SqlSource.Tool.Tests/Commands/FilterTests.cs`, `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`, `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`

**Interfaces:**
- Consumes: Task 2's `UsageValue`s for the unit, the `.sql` paths and `--project`.
- Produces:
  - `UsageValue.MayBePrintedAsPath : bool`, true when `Text` holds no `=` and no `;`.
  - `UsageValue.NamedAsPath(string fullPath) : string`: `'<fullPath on one line>'` or `The path at position <n>`.
  - The six formats start with `{0}` in place of `'{0}'`, and every caller passes a named path: `{0} holds no .sln, .slnx or .csproj file` and so on.
  - `RunUnitFinder.Find(UsageValue? argument, string workingDirectory)`.  `null` names the working directory in quotes.  The empty argument stays `''`.  `SQLSRC222` of a path named by position has the reason `the system would not list its files`, since the system's message quotes the path.
  - `RunProjects.FindAsync(RunUnit unit, IReadOnlyList<UsageValue> named, ...)`.
  - `FileFilter(string Path, string NormalizedPath, string Named)`, and `RunFilters.Create(IEnumerable<UsageValue> sqlPaths, IEnumerable<string> databases, string workingDirectory)`.

- [ ] **Step 1: Write the failing tests**

- `RunUnitTests`: `Find_PathWithAnEqualsSign_IsNamedByItsPosition` (`Host=db;Password=<Marker>` at position 2 gives `SQLSRC203` whose message is `The path at position 2 is not a .sln, .slnx or .csproj file, or a directory that holds one`); `Find_RelativePathInAFolderWithAnEqualsSign_PrintsTheFullPath` (a working directory `a=b` and the argument `App`: the message holds `'<wd>/App'`); the existing facts compare the quoted forms.
- `RunProjectsTests`: `FindAsync_ProjectWithASemicolon_IsNamedByItsPosition`: `SQLSRC207` reads `The path at position 4 is not a project of '<unit>'`.
- `FilterTests`: `Plan_SqlPathWithAnEqualsSign_IsNamedByItsPosition` with no `--connection` (`describe a=b.sql`: `SQLSRC212` `The path at position 2 is not a .sql file that a type of the run claims`); `Plan_SqlPathGivenTwice_IsNamedByTheFirstToken`.
- `SecretTests`: `Run_ConnectionStringWherePathsStand_IsNeverPrinted`, a theory over `[Secrets.ConnectionWithMarker]`, `[Secrets.ConnectionWithMarker + ".sql"]` and `["--project", Secrets.ConnectionWithMarker]`, each with no `--connection`: exit 1, `Out` and `Error` without `Secrets.Marker`, and the expected id.
- `ToolDiagnosticsTests`: `All_Messages_FormatWithTheirArguments` passes `'/work/app'` as the first argument for the six; the exact-format facts of `ProjectNotInRun` and `DirectoryCannotBeRead` pass the quoted path.

- [ ] **Step 2: Run them, see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests` and `dotnet test --project tests/SqlSource.Tests -- --filter-class "*ToolDiagnosticsTests"`
Expected: the new facts FAIL.

- [ ] **Step 3: Implement**

As in Interfaces.  `RunPlanner.Select` reports `FileNotInRun` with `filter.Named`.

- [ ] **Step 4: Run the tests, see them pass**

Same commands.  Expected: PASS.

- [ ] **Step 5: Validate and commit**

`docs/manual/diagnostics.md` for `SQLSRC201`, `202`, `203`, `207`, `212` and `222`: a sentence that a path holding `=` or `;` is named by its position on the command line, since it may be a connection string typed in the wrong place.  `docs/architecture/tool.md` where it describes the unit and the filters.  Leave `CheckUsage`'s refusal of `=` and `;` on a line with a `--connection` as it is (it says to quote the value).  `./pre-commit-validation.sh`; commit `Name a path by its position when it holds = or ;`.

### Task 4: Print a database name only when it is close to one of the plan

**Files:**
- Modify: `src/SqlSource.Tool/Describing/RunSummary.cs`, `src/SqlSource.Tool/Describing/Connections.cs`, `src/SqlSource.Tool/Describing/DescribeErrors.cs`, `src/SqlSource.Tool/Describing/DescribeRun.cs`
- Test: `tests/SqlSource.Tool.Tests/Commands/SummaryTests.cs`, `tests/SqlSource.Tool.Tests/Describing/ConnectionTests.cs`, `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsTests.cs`, `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`

**Interfaces:**
- Consumes: `NearName.IsNear` (Task 1), `DescribeOptions.Databases : ImmutableArray<UsageValue>` and `ConnectionArgument.Position` (Task 2).
- Produces:
  - `RunSummary.Lines(..., ImmutableArray<UsageValue> named, RunMode mode)`.
  - `Connections.Resolve(IReadOnlyList<string> selected, IReadOnlyList<string> describable, IReadOnlyList<string> planned, IReadOnlyList<ConnectionArgument> given, Func<string, string?> getEnvironmentVariable)`.
  - `Connections.UnusedNames` holds only the names near a database of `planned`; `Connections.UnusedPositions : ImmutableArray<int>` the positions of the rest, in order.

- [ ] **Step 1: Write the failing tests**

- `SummaryTests`: `Run_DatabaseFarFromEveryDatabase_IsNamedByItsPosition`: `describe --database` `Secrets.Marker` (a database name by the rule): a line `--database at position 3: 0 described, 0 skipped, 0 failed`, nothing of it in `Out`.  `Run_DatabaseCloseToADatabase_IsNamed` (`biling` beside `billing`): `biling: 0 described, 0 skipped, 0 failed`.  Same for `--check` with its counts.
- `ConnectionTests`: `UnusedNames_NameFarFromEveryDatabase_IsKeptByItsPosition`; change `UnusedNames_NameThatIsNoSelectedDatabase_IsNoErrorAndIsKept` to give `planned` holding `billing` and `reports`.
- `SecretTests`: replace `Run_ConnectionStringGivenWithoutAName_IsNeverPrinted` with `Run_ConnectionStringGivenWithoutAName_SaysItWasMisreadAndPrintsNothingOfIt`: `--connection Secrets.ConnectionWithMarker` gives `213`, `Error` contains `the value of --connection at position 2 names no database of this run`, and `Error` does not contain `Host`.  Add `Run_ConnectionStringWithoutANameCloseToADatabase_PrintsOnlyItsFirstWord` (Review Focus).  Add `Run_SecretTypedAfterDatabase_IsNeverPrinted`: `--database` `Secrets.Marker` leaves `Secrets.Marker`-free output.

- [ ] **Step 2: Run them, see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: the new facts FAIL.

- [ ] **Step 3: Implement**

`RunSummary.Lines`: a named database equal to one of the plan keeps `Label(known)`; one near one prints its text; any other prints `--database at position <n>`.  `Connections.Resolve` splits the unused names by `NearName.IsNear(name, database, ignoreCase: true)` over `planned`.  `DescribeErrors.NoConnection` appends the shown names as today, then `.  The value of --connection at position 2 names no database of this run` (or `at positions 2 and 4 name no databases`, with `, ` and ` and ` between the positions).  `DescribeRun` passes `[.. plan.Databases.Select(d => d.Name)]`.

- [ ] **Step 4: Run the tests, see them pass**

Same command.  Expected: PASS.

- [ ] **Step 5: Validate and commit**

`docs/manual/diagnostics.md#sqlsrc213` (the `help:` line names a name close to a database of the run, and counts any other by its position), `docs/manual/tool.md#the-summary` (a name far from every database is shown by its position), `docs/architecture/tool.md` (connections, the summary).  `./pre-commit-validation.sh`; commit `Print a database name only when it is close to one of the plan`.

### Task 5: Log a token by its position where the rule would print it so

**Files:**
- Modify: `src/SqlSource.Tool/Logging/LoggedArguments.cs`
- Test: `tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs`, `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`

**Interfaces:**
- Consumes: `UsageValue.MayBePrintedAsPath` (Task 3).

- [ ] **Step 1: Write the failing tests**

`RunLogTests`: `RunStart_Arguments_HoldEachTokenByTheRuleOfWhatIsPrinted` over `describe App.csproj Users.sql a=b.sql --project=x;y --database billing --connection billing=<conn> --log run.log --force` (the files made so the run starts): `arguments` is `["describe", "App.csproj", "Users.sql", "***", "--project=***", "--database", "***", "--connection", "***", "--log", "run.log", "--force"]`.  Change the facts that expect `billing=***` to expect `***`.  `SecretTests`: the log of `Run_ConnectionStringWherePathsStand_IsNeverPrinted` holds no `Secrets.Marker` (make it a log run too).

- [ ] **Step 2: Run them, see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "*RunLogTests" --filter-class "*SecretTests"`
Expected: FAIL.

- [ ] **Step 3: Implement**

For each `UsageValue`: an argument or a `--project` value that may not be printed as a path, every `--database` value and every `--connection` value is replaced by `Redaction.Mark` after the option and separator of its token.  `--log` and flags stay.  The remarks say why `--database` and `--connection` names are never written: `run.start` comes before the plan.

- [ ] **Step 4: Run the tests, see them pass**

Same command.  Expected: PASS.

- [ ] **Step 5: Validate and commit**

`docs/architecture/tool.md#the-log` and the `arguments` sentences of `src/SqlSource.Tool/AGENTS.md`.  `./pre-commit-validation.sh`; commit `Log a token by its position where the rule would print it so`.

### Task 6: Manual and tech debt

**Files:**
- Modify: `docs/manual/tool.md` (Logs, Known issues), `docs/tech-debt/TD-0023-...md` (narrowed, renamed to `TD-0023-path-without-an-equals-sign-is-printed-as-given.md`), `docs/tech-debt/README.md`, `src/SqlSource.Tool/AGENTS.md` (its link to TD-0023), `docs/architecture/tool.md` (any link to TD-0023)

- [ ] **Step 1: Rewrite the manual**

Logs: a connection string typed where a path goes, holding `=` or `;`, or after `--project`, `--database` or `--connection` without its name, is written `***`; what is left is a secret with neither `=` nor `;` typed where a path goes.  Known issues: the same, in one sentence, and drop the `-pS3cret` sentence (a far option is named by position) and the "start printed and logged as a name" clause.

- [ ] **Step 2: Narrow TD-0023**

Problem: a token with neither `=` nor `;` where the unit, a `.sql` path or a `--project` value stands is printed as a path (`SQLSRC201`–`203`, `207`, `212`, `222`) and logged as typed.  Why it exists: a path must be shown to be of use, and a path can hold anything.  Proposed fix: none planned; a trigger of a report.  Update its row in `docs/tech-debt/README.md` and every link (`grep -rn TD-0023`).

- [ ] **Step 3: Validate and commit**

`./pre-commit-validation.sh`; commit `Narrow TD-0023 to a path with no = and no ;`.
