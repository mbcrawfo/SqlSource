# Phase 9.1: connections from a `.env` file - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `.env` file is the third source of a connection for `sqlsource describe`, after `--connection` and the process environment, read only for its `SQLSOURCE_CONNECTION*` keys, with its values treated as secrets by every existing rule.

**Architecture:** A new folder `src/SqlSource.Tool/DotEnv/` parses the file and finds it (walking up from the run unit's folder to the repository's root).  A new `ToolHost.ReadTextFile` member is the one way the file is read, so tests never read a developer's `.env`.  `Describing/ConnectionEnvironment.cs` merges the process environment with the file's values, variable by variable, and is what `Connections.Resolve` reads in place of `ToolHost.GetEnvironmentVariable`; it also names the source (`SQLSOURCE_CONNECTION_X in /work/.env`) for `--verbose`, the log and `SQLSRC213`'s help.

**Tech Stack:** C# (`net8.0` tool), xUnit v3, Shouldly.

**Spec:** `docs/superpowers/specs/2026-11-02-query-generation-phase-9-build-integration-design.md`, sections "Connections from a `.env` file", "Diagnostics" and "9.1 `.env`".

## Global Constraints

- Root `AGENTS.md`, `src/SqlSource.Tool/AGENTS.md`, `src/SqlSource.Tool/Describing/AGENTS.md` and `src/SqlSource/Diagnostics/AGENTS.md` win over this plan.  Read them before you start.
- Nothing reads `Environment`, `Console` or the current directory except `ToolHost.Create` and `Cli.Run`.  The `.env` file is read only through `ToolHost.ReadTextFile`.  Checking for a `.git` folder or file is a plain disk check (`Directory.Exists`/`File.Exists`), as other disk reads of the tool are.
- A connection's value from `.env` goes to the describer and nowhere else: never into an error, `--verbose`, the log, a sidecar, or the environment of a process the tool starts.  An error about a line names the key, never the value.
- Only `SQLSOURCE_CONNECTION` and keys that start with `SQLSOURCE_CONNECTION_` are read (ordinal, case-sensitive).  Every other key is passed over without a word, a malformed line of another key included.
- Precedence: for each variable, a value in the process environment that is not empty or white space wins; otherwise the file's.  `Connections.Resolve`'s order is unchanged: `--connection`, the variable of the name, then `SQLSOURCE_CONNECTION` for the one describable database.
- One file is read, never merged.  Search: from the run unit's folder up, reading the nearest file named `.env`; stop after the first folder that holds a `.git` folder or file.  When no folder up to the file system's root holds `.git`, only the unit's folder is searched.  A folder named `.env` is no file and is passed over.
- A `.env` that cannot be read (by the system, or a malformed line of a key the tool reads) is an error, and the run goes on as if the file gave nothing (no value of it is used, even from good lines).
- Diagnostics: `SQLSRC285` (file cannot be read) and `SQLSRC286` (a line of a key the tool reads cannot be read).  `SQLSRC200` to `SQLSRC284` are taken today.
- Every commit passes `./pre-commit-validation.sh`; run `./format.sh -- <changed .cs files>` (the repository's formatter, see `CONTRIBUTING.md`) before validating.

## Where the merged code differs from the spec

- The spec's example source is `SQLSOURCE_CONNECTION_BILLING from /work/.env`.  `--verbose` already prints `connection from <source>`, so the source is written `SQLSOURCE_CONNECTION_BILLING in /work/.env`, giving `connection from SQLSOURCE_CONNECTION_BILLING in /work/.env`.  The log's `connectionSource` holds the same text.
- `DescribeRun` does not know the run unit today; `DescribeOptions` gains the unit's folder (Task 3).

## Review Focus

1. **A Python virtual environment named `.env/`** in the unit's folder: passed over as no file, the search goes on upward, no error. (Task 2)
2. **A connection string unquoted in `.env`**, holding `=`, `;` and a `#` with no space before it (`Password=a#b`): the value is whole. (Task 1)
3. **A file written on Windows**: `\r\n` line ends, a byte order mark, no line break after the last line. (Task 1)
4. **A malformed tool line that holds a secret** (an unclosed quote, a missing `=` after a key followed by the string, an unknown escape): the error text holds no part of the value. (Tasks 1 and 3)
5. **A blank value**: `SQLSOURCE_CONNECTION_X=` in the file with the variable unset is "not set" (`SQLSRC213`); a blank variable in the environment with a value in the file uses the file's value. (Task 3)

---

### Task 1: The `.env` parser

**Files:**
- Create: `src/SqlSource.Tool/DotEnv/DotEnvParser.cs`, `src/SqlSource.Tool/DotEnv/DotEnvContents.cs`, `src/SqlSource.Tool/DotEnv/DotEnvLineError.cs`
- Test: `tests/SqlSource.Tool.Tests/DotEnv/DotEnvParserTests.cs`

**Interfaces:**
- Produces:
  - `internal sealed record DotEnvLineError(int Line, int Column, string Key, string Reason)`: `Line` and `Column` counted from zero (the column of the key's first character); `Key` is what the error may name (see below); `Reason` one of the fixed texts below.
  - `internal sealed record DotEnvContents(ImmutableDictionary<string, string> Values, ImmutableArray<DotEnvLineError> Errors)`: `Values` holds only the tool's keys (ordinal), the last of a key winning; it overrides `ToString` to leave the values out.
  - `internal static class DotEnvParser { public static DotEnvContents Parse(string text); public static bool IsToolKey(string key); }`

**Rules** (the spec's dotenv subset; each is a row of the test table):

- A leading `﻿` is dropped.  Lines split at `\n`; a trailing `\r` is dropped.
- A line that is blank, or whose first non-blank character is `#`, is passed over.
- An optional `export` followed by white space before the key is dropped.
- The key is the text before the first `=`, trimmed.  A tool key is `SQLSOURCE_CONNECTION` or starts with `SQLSOURCE_CONNECTION_`, and is a name `[A-Za-z_][A-Za-z0-9_]*`.
- The value is the text after the first `=`, its leading white space dropped:
  - starting with `'`: the text up to the next `'`, as written;
  - starting with `"`: the text up to the next unescaped `"`, taking `\\`, `\"`, `\n`, `\r`, `\t`;
  - after a closing quote only white space, or white space then a `#` comment, may follow;
  - otherwise unquoted: ends at a `#` that follows white space, then trimmed.  `#` with no white space before it is part of the value.
- A line that cannot be read is passed over, unless it is a tool line, which is an error with these reasons, word for word:
  - no `=` and the trimmed line is a tool key: `it has no '='`;
  - text before `=` starts with `SQLSOURCE_CONNECTION` but is not a valid name: `the key is not a valid name` (its `Key` is the leading run of `[A-Za-z0-9_]` only, so nothing past the identifier is ever named);
  - an unclosed quote: `a quote is not closed`;
  - text after the closing quote: `text follows the closing quote`;
  - a `\` before any other character in double quotes: `a backslash starts no escape that .env takes`.

- [ ] **Step 1: Write the failing tests.**  `DotEnvParserTests` with a `[Theory]` over the syntax table (name each case for its rule), asserting `Values` exactly, e.g.:
  - `"SQLSOURCE_CONNECTION_A=Host=x;Password=a#b"` gives `A`'s value `Host=x;Password=a#b`;
  - `"SQLSOURCE_CONNECTION_A=Host=x # the dev box"` gives `Host=x`;
  - `"export  SQLSOURCE_CONNECTION_A = 'a \\n b'"` gives `a \n b` with a literal backslash;
  - `"SQLSOURCE_CONNECTION_A=\"a\\tb\\\"c\""` gives a tab and a quote;
  - `"﻿SQLSOURCE_CONNECTION_A=1\r\nSQLSOURCE_CONNECTION_A=2"` (no final break) gives `2`;
  - `"SQLSOURCE_CONNECTION=x\nOTHER=y\nsqlsource_connection_a=z"` gives only `SQLSOURCE_CONNECTION`;
  - `"OTHER=\"unclosed\nNOT A LINE"` gives nothing and no error;
  - `"SQLSOURCE_CONNECTION_A="` gives the empty string.
  A second `[Theory]` for each error reason asserts `Errors` holds one error with `Line`, `Column`, `Key` and `Reason`, `Values` is empty of that key, and that neither `Key` nor `Reason` contains the secret `hunter2` placed in the value (e.g. `"  SQLSOURCE_CONNECTION_A=\"Password=hunter2"` is line 0, column 2, key `SQLSOURCE_CONNECTION_A`, `a quote is not closed`; `"SQLSOURCE_CONNECTION_A-B=Password=hunter2"` names `SQLSOURCE_CONNECTION_A`).  A `[Fact]` that `ToString()` of contents holding `hunter2` does not contain it.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~DotEnvParserTests"`.  Expected: compile failure.
- [ ] **Step 3: Implement** the three files by the rules above.  Namespace `SqlSource.Tool.DotEnv`.
- [ ] **Step 4: Run** the same command.  Expected: all pass.
- [ ] **Step 5: Commit** `feat(tool): read the connections of a .env file`.

### Task 2: The host member, the search, the diagnostics

**Files:**
- Modify: `src/SqlSource.Tool/ToolHost.cs` (new member `ReadTextFile`, real reader `ToolHost.ReadText`)
- Create: `src/SqlSource.Tool/DotEnv/DotEnvSearch.cs`, `src/SqlSource.Tool/DotEnv/DotEnvFile.cs`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs` (two descriptors and `All`), `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md` (index rows and the two sections), `src/SqlSource/Diagnostics/AGENTS.md` ("`SQLSRC200` to `SQLSRC286` are taken")
- Modify: `tests/SqlSource.Tool.Testing/Hosts.cs`, every other `new ToolHost(` in tests
- Test: `tests/SqlSource.Tool.Tests/DotEnv/DotEnvSearchTests.cs`, `tests/SqlSource.Tool.Tests/ToolHostTests.cs`

**Interfaces:**
- Consumes: `DotEnvParser.Parse`, `DotEnvContents` (Task 1).
- Produces:
  - `ToolHost.ReadTextFile`: `Func<string, string?>`, a new positional member after `CreateFile`: the text of the file at a full path, or null when there is no file there (none, or a folder); it throws `IOException` or `UnauthorizedAccessException` when the file cannot be read.  `ToolHost.Create` gives `ToolHost.ReadText(string path)`: null when `Directory.Exists(path)`, `File.ReadAllText` otherwise, null on `FileNotFoundException`/`DirectoryNotFoundException`.
  - `ToolDiagnostics.DotEnvCannotBeRead`: `SQLSRC285`, title `.env file cannot be read`, message `{0} cannot be read: {1}`, reported with `ToolDiagnostic.ForFile(descriptor, path, exception.Message)`.
  - `ToolDiagnostics.DotEnvLineCannotBeRead`: `SQLSRC286`, title `Connection in .env cannot be read`, message `The line that sets {0} cannot be read: {1}`, reported with `ToolDiagnostic.At(descriptor, path, new LinePosition(error.Line, error.Column), error.Key, error.Reason)`.
  - `internal sealed record DotEnvFile(string? Path, ImmutableDictionary<string, string> Values, ImmutableArray<ToolDiagnostic> Errors)`: the file read (null when none was found), its tool values (empty when it had an error), its errors.  `ToString` leaves the values out.
  - `internal static class DotEnvSearch`:
    - `public static ImmutableArray<string> FoldersOf(string unitFolder)`: the folders searched, nearest first, by the Global Constraints' rule.
    - `public static DotEnvFile Read(string unitFolder, Func<string, string?> readTextFile)`: the nearest `.env` of `FoldersOf`, parsed; a read that throws gives `SQLSRC285` and no values; parse errors give one `SQLSRC286` each and no values.
    - `public static ImmutableArray<string> NotRead(string unitFolder, string? read, IEnumerable<string> projectFolders, Func<string, string?> readTextFile)`: each `.env` that exists (the reader gives text or throws) in a folder from a project's folder up to, and not including, the unit's folder or one of its ancestors, other than `read`; each path once, in the order found.  Empty for a project in the unit's folder.

- [ ] **Step 1: Write the failing tests.**  In `DotEnvSearchTests` (real temp folders from `TempFolder`, the reader `ToolHost.ReadText`):
  - `Read_EnvInTheUnitFolder_IsRead`; `Read_EnvsInTheUnitFolderAndAbove_NearestWins` (a `.git` folder at the top);
  - `Read_EnvAboveTheRepositoryRoot_IsNotRead` (`.env` above a folder with `.git`); `Read_GitFileMarksTheRoot` (a worktree's `.git` file);
  - `Read_OutsideARepository_ReadsOnlyTheUnitFolder` (a `.env` in the parent of a unit folder, no `.git` anywhere: not read);
  - `Read_EnvIsAFolder_IsPassedOverAndTheSearchGoesOn`;
  - `Read_ReaderThrows_ReportsSqlsrc285AndGivesNothing`;
  - `Read_MalformedToolLine_ReportsSqlsrc286AtTheLineAndGivesNothing` (asserts the reported line text `<path>(2,1): error SQLSRC286: The line that sets SQLSOURCE_CONNECTION_A cannot be read: a quote is not closed` through `Reporter` and that `hunter2` appears nowhere);
  - `NotRead_SolutionWithAProjectEnv_NamesIt` and `NotRead_ProjectInTheUnitFolder_IsEmpty`.
  In `ToolHostTests`: `Create_RealHost_ReadTextFile_GivesTextAndNullForNoFileOrAFolder`.  A test that `Hosts.Create(...).ReadTextFile` gives null for a file outside the system's temporary folder (write none: ask for the repository's own `README.md`) and the text for one inside.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~DotEnvSearchTests|FullyQualifiedName~ToolHostTests"`.  Expected: compile failure.
- [ ] **Step 3: Implement** `ToolHost.ReadTextFile` and `ReadText` (update its XML docs).  Test hosts (`Hosts.Create`, `Hosts.Real`, and any other `new ToolHost(` the build finds) give a reader that reads with `ToolHost.ReadText` only paths under `Path.GetTempPath()` and gives null elsewhere, so no test reads a developer's `.env`; say so in a comment.  Then `DotEnvFile`, `DotEnvSearch`, the descriptors, `AnalyzerReleases.Unshipped.md` rows, and the two `docs/manual/diagnostics.md` sections (index rows under the tool's errors; each section: what it means, an example error line, how to fix it, `See: [Connections](tool.md#connections)`; the `SQLSRC286` section lists the five reasons and says the value is never printed).
- [ ] **Step 4: Run** the Step 2 command and `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Diagnostics"`.  Expected: all pass.
- [ ] **Step 5: Commit** `feat(tool): find the .env of a run`.

### Task 3: The run reads its connections from `.env`

**Files:**
- Create: `src/SqlSource.Tool/Describing/ConnectionEnvironment.cs`
- Modify: `src/SqlSource.Tool/Describing/Connections.cs`, `DescribeRun.cs`, `DescribeOptions.cs`, `DescribeErrors.cs` (`NoConnection`), `DatabaseRuns.cs` (the call to `NoConnection`), `src/SqlSource.Tool/DescribeCommand.cs` (`PlanAsync`, `OptionsOf`, `Bind`)
- Test: `tests/SqlSource.Tool.Tests/Describing/ConnectionTests.cs`, `tests/SqlSource.Tool.Tests/Commands/DotEnvRunTests.cs` (new), `tests/SqlSource.Tool.Tests/Commands/SecretTests.cs`, `tests/SqlSource.Tool.Tests/Logging/RunLogRedactionTests.cs`, `tests/SqlSource.Tool.Tests/Commands/VerboseTests.cs`

**Interfaces:**
- Consumes: `DotEnvSearch.Read`, `DotEnvSearch.NotRead`, `DotEnvFile` (Task 2); `ToolHost.ReadTextFile`.
- Produces:
  - `internal sealed class ConnectionEnvironment`, the merged lookup that units 9.2 and 9.3 reuse:
    - `public static ConnectionEnvironment Of(Func<string, string?> getEnvironmentVariable, DotEnvFile file)`;
    - `public static ConnectionEnvironment Load(string unitFolder, ToolHost host)`: `Of(host.GetEnvironmentVariable, DotEnvSearch.Read(unitFolder, host.ReadTextFile))`.  `--watch` calls it once a pass, so it caches nothing;
    - `public ImmutableArray<ToolDiagnostic> Errors`, `public string? FilePath`, `public string UnitFolder`;
    - `public (string Value, string Source)? Find(string variable)`: the environment's value when not blank, with the variable as source; else the file's when not blank, with `"{variable} in {FilePath}"` as source; else null;
    - `ToString` leaves values out.
  - `Connections.Resolve(..., ConnectionEnvironment environment)` in place of `Func<string, string?> getEnvironmentVariable`; `DatabaseConnection.Source` takes `Find`'s source.  `Connections` keeps the environment for `SQLSRC213`.
  - `DescribeOptions` gains `string UnitFolder` (the folder of the run unit's file).  `DescribeCommand.PlanAsync` stays the tests' entry; a private method returns the unit with the plan, for `Bind`.

- [ ] **Step 1: Write the failing tests.**
  - `ConnectionTests` (with `ConnectionEnvironment.Of` and a `DotEnvFile` built in the test): the file fills a variable the environment does not set; the environment wins over the file; a blank environment value lets the file's through; a blank file value is not set; `--connection` wins over both; the named variable in the file beats `SQLSOURCE_CONNECTION` in the environment; `SQLSOURCE_CONNECTION` in the file with two describable databases sets `UnnamedIsSetAndNotUsed`; the source of a file value is `SQLSOURCE_CONNECTION_BILLING in /work/.env`.
  - `DotEnvRunTests` through `CliRun` and a `FakeDescriber` (mirror `SecretTests`' set-up): a run with the value only in `Folder`'s `.env` describes and exits `0`; `--verbose` prints `database 'billing' (postgres): connection from SQLSOURCE_CONNECTION_BILLING in <path>`; a malformed tool line prints `SQLSRC286` and the database then has no connection (`SQLSRC213`), exit `1`; an unreadable `.env` (a test reader that throws, via `CliRun`'s host: add a `ReadTextFile` override property to `CliRun` as it has `CreateFile`) prints `SQLSRC285`; a `.env` of other keys alone says nothing; `SQLSRC213`'s help with no `.env` is `pass --connection billing=<connection string>, or set SQLSOURCE_CONNECTION_BILLING in the environment or in <unit folder>/.env`, and with one it names the file read; a solution run whose project folder holds a `.env` ends the help with `.  The run did not read <project folder>/.env: it reads the .env nearest to <unit folder>` (several: `<a> and <b>`, `<a>, <b> and <c>`); the `dotnet msbuild` requests of `Processes.Requests` hold the value in none of `SetVariables`.
  - `SecretTests`: `Run_ValueInTheDotEnvFileAndEveryError_IsNeverPrinted`, `..._WithVerbose_IsNeverSaid`, and `Run_MalformedDotEnvLineThatHoldsTheValue_PrintsNothingOfIt`, in the manner of `Run_ValueInTheVariableOfTheNameAndEveryError_IsNeverPrinted`.
  - `RunLogRedactionTests`: `Run_ConnectionFromTheDotEnvFile_LogsItsSourceAndNotItsValue` (the `database` event's `connectionSource` is `SQLSOURCE_CONNECTION_BILLING in <path>`, the log nowhere holds the value), and a describer that logs its `.env` connection has the mark in every place, as `Run_DescriberThatLogsItsConnection_HasTheMarkInEveryPlaceAndTheValueNowhere`.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tool.Tests`.  Expected: the new tests fail.
- [ ] **Step 3: Implement.**  In `DescribeRun.RunAsync` step 3: `ConnectionEnvironment.Load(options.UnitFolder, host)`, report its `Errors`, then `Connections.Resolve` with it.  `DescribeErrors.NoConnection` builds the help above from `Connections`' environment and the project folders of the database's queries (`PlannedFile.ProjectPath`), with `DotEnvSearch.NotRead`.  Paths in help go in as they are; the reporter puts every line on one.  Update the XML docs that say "the variable" (`DatabaseConnection.Source`, `RunEvents.Database`, `LoggedArguments`).
- [ ] **Step 4: Run** `dotnet test tests/SqlSource.Tool.Tests`.  Expected: all pass.
- [ ] **Step 5: Commit** `feat(tool): take a connection from .env when the environment has none`.

### Task 4: End-to-end, a connection through `.env` alone

**Files:**
- Modify: `tests/DatabaseTests/Postgres/Describer/DescribeCommandTests.cs`, `tests/DatabaseTests/SqlServer/Describer/DescribeCommandTests.cs`

- [ ] **Step 1: Write** in each `Describe_ConnectionInADotEnvFileAlone_WritesItsSidecar`: copy the fixture project, write `.env` beside it holding `SQLSOURCE_CONNECTION_<DATABASE>="<container's connection string>"` (the variable from the database name the test already uses), run `describe <project>` with `CliRun.Real` and no `--connection`, assert exit `0`, the summary line the existing test asserts, and the sidecar's entry.
- [ ] **Step 2: Run** each project's tests with that filter (Docker running).  Expected: pass.  Then each project on its floor image, as root `AGENTS.md` Commits rule 2 says: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test tests/DatabaseTests/Postgres --filter "FullyQualifiedName~DescribeCommandTests"` and `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test tests/DatabaseTests/SqlServer --filter "FullyQualifiedName~DescribeCommandTests"`.
- [ ] **Step 3: Commit** `test(database): describe through a .env alone`.

### Task 5: Documentation

**Files:**
- Modify: `docs/manual/tool.md` (Connections, Environment variables), `README.md` (step 5), `docs/manual/README.md` ("What works today"), `docs/manual/diagnostics.md` (`SQLSRC213`), `docs/architecture/tool.md` (the host, connections, where a value goes, folders), `src/SqlSource.Tool/AGENTS.md` ("A connection's value"), `src/SqlSource.Tool/README.md` if it shows how to give a connection, `tests/README.md` if it describes the hosts of the tests, `.gitignore` (`.env`)

- [ ] **Step 1: Manual.**  `tool.md` Connections: a `.env` example first; the table gains row 3, "A `.env` file", and its precedence ("the environment wins, variable by variable"); where it is found, in effects (the nearest `.env` from the folder of the solution or project you run on, up to the repository's root; outside a repository only that folder; a solution run reads the solution folder's, so put it there or at the repository's root); the syntax in user terms (quotes, comments, `export`, escapes, no multi-line values, no `${VAR}`); only the connection keys are read, the rest of the file is the application's; keep `.env` out of git (`.gitignore`) and commit a `.env.example`; `--verbose` names the file; the values are kept to the tool as the other sources' are and are never passed to `dotnet msbuild`.  Environment variables: a note that each connection variable can be in `.env`.  Never say how the tool does it (`docs/manual/AGENTS.md`), and run its grep.
- [ ] **Step 2: README step 5**: the `.env` file as the way to keep the connection (a fenced sample of the file, then `dotnet sqlsource describe`), the variable and `--connection` as the alternatives; every link absolute; no raw HTML.
- [ ] **Step 3: "What works today"**: a row `Connections in a .env file` | Available | `[Connections](tool.md#connections)`.  `SQLSRC213`'s section says the `help:` line names the `.env` file and a `.env` the run did not read.
- [ ] **Step 4: Architecture and agents.**  `docs/architecture/tool.md`: `ReadTextFile` in "The host" (and that test hosts read only temporary folders); "Connections" describes `ConnectionEnvironment` and `DotEnvSearch`; "Where a connection's value goes" names the file as a source; the folders table gains `DotEnv/` (uses `Reporting/`; from the generator the descriptors) and `Describing/`'s row uses `DotEnv/`.  `src/SqlSource.Tool/AGENTS.md`: what the tool may say of a connection is where it came from, `--connection`, a variable's name, or a variable's name and the `.env` file.  `.gitignore`: add `.env`.
- [ ] **Step 5: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Documentation|FullyQualifiedName~Diagnostics"`.  Expected: pass.
- [ ] **Step 6: Commit** `docs: connections from a .env file`.

### Hand-over

Write `9-1-notes.md` in the shared notes folder: `ToolHost.ReadTextFile`, `DotEnvSearch`/`DotEnvFile` in `src/SqlSource.Tool/DotEnv/`, the merged lookup `Describing/ConnectionEnvironment` (`Load(unitFolder, host)`, called once a pass), `DescribeOptions.UnitFolder`, and the ids `SQLSRC285`/`SQLSRC286`.
