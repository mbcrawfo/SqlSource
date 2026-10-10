# Query generation, phase 2.6: `--check` and logging - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver `describe --check`, which describes every query, compares with the committed sidecars and writes nothing, and the two ways a run says what it did: `--verbose` on the console and `--log` as JSON Lines in a file.  Close phase 2.

**Architecture:** `--check` is the run of sub-phase 2.5 with its last step replaced: `Describing/SidecarCheck.cs` compares where `SidecarStore` writes, and returns its differences as data.  `Reporter` counts `SQLSRC219` apart from every other error, and `Cli.RunAsync` takes the exit code `2` from that.  A new folder, `src/SqlSource.Tool/Logging/`, holds the log: `IRunLog` and its closed set of values, `RunLogFile`, which writes a line for each event, `SessionRunLog`, which removes a connection's value, and `RunTrace`, the one object a run says everything but its errors and its summary through.  `Cli.RunAsync` opens the log and closes it; the reporter carries the trace to every step that already has the reporter.

**Tech Stack:** C# on `net8.0` in `src/SqlSource.Tool` with System.CommandLine 2.0.12 and `System.Text.Json`'s `Utf8JsonWriter` of the framework; the sidecar model, reader and comparer of `src/SqlSource/Snapshot/`; xunit v3 on Microsoft.Testing.Platform and Shouldly in `tests/SqlSource.Tool.Tests` on `net10.0`.

**Spec:** [`docs/superpowers/specs/2026-10-09-query-generation-phase-2-6-check-and-logging-design.md`](../specs/2026-10-09-query-generation-phase-2-6-check-and-logging-design.md).  Read it first; this plan argues from it.  The [epic outline](../specs/2026-10-07-query-generation-epic-design.md) holds what it builds on: "Workflow", "Output of the tool", "Diagnostics" and "Phase 2".  [`src/SqlSource.Tool/AGENTS.md`](../../../src/SqlSource.Tool/AGENTS.md) holds the rules of the code this plan changes; read it before task 2.

## Global Constraints

- Branch: `claude/query-generation-phase-2-6-check-and-logging`, which task 1 creates from `main`.  One commit per task, in task order.
- Every commit passes `./pre-commit-validation.sh`, which needs Docker running.  **The closing steps of every task** are: run `./format.sh`; run `./pre-commit-validation.sh` as its own command and fix what it reports; then commit in a separate command.  A hook runs the validation again before any Bash command that contains the words of the commit command; never work around it, and keep those words out of any other command.
- **The code of this plan was written against the repository as it stood and was not compiled.**  Where the compiler, the formatter or an analyzer objects, change the code to satisfy it and keep the names and the behaviour that the **Interfaces** blocks and the tests give.  Never suppress a rule, and never edit `.editorconfig`.
- `src/SqlSource` stays on `netstandard2.0` and Roslyn 4.8.0 and takes no new package reference.  This plan adds one descriptor to it, `SQLSRC219`, and changes nothing else there.  The tool targets `net8.0`: do not raise it, and do not use an API that .NET 8 lacks.  **No project takes a new package reference**: `TimeProvider` and `System.Text.Json` are the framework's, and the clock of the tests is written by hand.
- **No value of a connection is ever printed or logged.**  The tool hands a value to `OpenRequest` and to nothing else.  Every text that a describer gave, printed, said under `--verbose` or written to the log, goes through `Redaction`, which writes the whole value as `***`.  `arguments` of `run.start` is built from what `UsageCheck` read, never from the text of the command line.
- Nothing in the tool reads `Console`, `Environment` or the current directory except `ToolHost.Create` and `Cli.Run`.  Time comes from `ToolHost.Time`.
- Every error goes through `Reporter` with a descriptor.  A wrong command line is the exception: its lines have no id, do not go through the reporter, and **never repeat a token's text**.
- `SQLSRC219` touches four places: the descriptor and `All` in `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md` (the table row and the section), each between `SQLSRC218` and `SQLSRC220`, and the tests of `tests/SqlSource.Tests/Diagnostics/`.
- Exit codes: `0` success; `1` an error other than `SQLSRC219` was reported, a wrong command line, or a cancelled run; `2` `SQLSRC219` was reported and nothing else.  A command reports and goes on, and never returns `1` itself.
- An event's name and its members are read by people and by phase 10's bundle: a member is added and never renamed.  A value is a string, a whole number, a boolean, null, a list or a map.
- A path in a message is a full path.  Database names are compared with `StringComparer.OrdinalIgnoreCase`, query names with `StringComparer.Ordinal`.
- A file is at most 120 characters wide, apart from Markdown.  `./format.sh` breaks code that is wider, but never a string: split a string literal that is too wide with `+`, at a word.
- A file this plan gives in full ends with one line break, which the block that shows it does not.
- Types under `src/` carry XML documentation as their neighbours do; test files use `//` comments.  Prose uses two spaces after a full stop.
- Files under `docs/superpowers/` are not rewritten, apart from the epic outline in tasks 1 and 9.
- The repository's `README.md`, `CONTRIBUTING.md` and `docs/publishing.md` do not change.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Before the pull request: `git fetch --tags origin`, then `git tag --list 'v*' --sort=-v:refname | head -n 1`.  When this plan was written there was no `v*` tag and `VersionPrefix` was `0.1.0`: nothing to compare.  If there is a tag now and `VersionPrefix` in `Directory.Build.props` is not greater, ask the owner for the new version.

Commands used throughout:

```bash
dotnet build SqlSource.slnx
```

```bash
dotnet test --solution SqlSource.slnx
```

One class of the tool's tests:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.CheckTests
```

## Where this plan departs from the spec

1. **A claim is named by where its attribute stands**, `Queries.cs` and a line, in `file.claims` and under `--verbose`.  The tool's reader of attributes is syntax-only and keeps no type name.
2. **`sidecar.action` has a seventh value, `failed`**, for a sidecar that could not be written or deleted.  `written` would say what did not happen.
3. **`database.open` has `driverVersion` beside `driver`**, as `ServerInfo` holds the two apart.
4. **`project.usesSqlSource` is null** for a project that was not restored or that MSBuild failed on: nothing says whether it does.  The `diagnostic` that follows says why.
5. **The log is opened only for a `describe` that runs.**  `describe --help` with `SQLSOURCE_LOG` set must not empty a user's log.
6. **A log that stops being writable while the run goes on** does not end the run: the writer goes quiet, and the run ends with one `SQLSRC218` for the log's path and exit `1`.
7. **`--log` with an empty value is a wrong command line**, `sqlsource: the value of option '--log' at position <n> is not a path`, and a second `--log` is `sqlsource: option '--log' is given more than once`.  `SQLSOURCE_LOG` that is empty or white space is not set.
8. **Three test classes the spec does not name**: `RunLogFileTests` and `SessionRunLogTests`, for the two writers alone, and a hand-written `TestClock`.

## Review Focus

What the spec implies and its own test table does not name.  Each has a test in the task that owns the code.

1. **`--log` given a directory, a folder that does not exist, or nothing.**  `SQLSRC218` with `written`, or the line of a wrong command line; never `SQLSRC200`, and no query is described.  Task 5.
2. **A log that fails while the run goes on**, a full disk.  The run finishes, its sidecars are written, and one `SQLSRC218` names the log.  Task 5.
3. **`describe --help` or `--version` with `SQLSOURCE_LOG` set.**  The file is neither made nor emptied.  Task 5.
4. **A connection value that JSON escapes**, one with `"` and `\`.  It is removed before the line is written, so the file holds it in neither form.  Task 6.
5. **A committed sidecar that a checkout gave `\r\n` and a byte order mark.**  `--check` compares entries and not text: exit `0`, and the line of an entry that is reported is still right.  Task 3.

## File structure

| File | Task | Responsibility |
|----|----|----|
| `src/SqlSource.Tool/Describing/SidecarCheck.cs`, `FileCheck.cs` | 3, 4 | Compares what was described with the sidecars on disk; returns differences as data |
| `src/SqlSource.Tool/Describing/SidecarOnDisk.cs` | 3 | Gains what the comparison needs: whether a file is there, the sidecar whatever tool wrote it, its format version |
| `src/SqlSource.Tool/Logging/LogValue.cs`, `LogMember.cs`, `IRunLog.cs`, `NullRunLog.cs` | 5 | An event's name and members, and the log that takes them |
| `src/SqlSource.Tool/Logging/RunLogFile.cs` | 5 | Writes each event as one JSON object on a line; cuts a string over 1 MiB |
| `src/SqlSource.Tool/Logging/LoggedArguments.cs` | 5 | The command line with each `--connection` value replaced |
| `src/SqlSource.Tool/Logging/RunTrace.cs` | 5, 6, 7 | What a run says of itself beside its errors and its summary: the log, and the lines of `--verbose` |
| `src/SqlSource.Tool/Logging/SessionRunLog.cs` | 6 | Removes one connection's value from every event; puts the engine before a describer's event |
| `src/SqlSource.Tool/Logging/RunEvents.cs` | 6, 7 | The events of the spec's table, each built in one place |
| `tests/SqlSource.Tool.Tests/TestClock.cs`, `LogReader.cs` | 5 | A clock a test moves; the lines of a log as JSON |

---

### Task 1: The branch, and the outline says In progress

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (the row of 2.6 in the table under "Phases")
- Commit with it: the amended spec and this plan, which are in the working tree

- [ ] **Step 1: Create the branch**

```bash
git switch -c claude/query-generation-phase-2-6-check-and-logging
```

- [ ] **Step 2: Set the row**

In the table under `## Phases`, in the row that starts `| 2.6 \`--check\` and logging |`, replace `Not started` with `In progress`.  Change nothing else.

- [ ] **Step 3: Validate**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers
git commit -m "Start sub-phase 2.6: the spec as reviewed, its plan, and the outline's row

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `--check` describes every query and writes nothing

**Files:**
- Modify: `src/SqlSource.Tool/Describing/DescribeOptions.cs`
- Modify: `src/SqlSource.Tool/DescribeCommand.cs`
- Modify: `src/SqlSource.Tool/Describing/RunDecisions.cs`
- Modify: `src/SqlSource.Tool/Describing/DescribeRun.cs`
- Modify: `tests/SqlSource.Tool.Tests/UsageCheckTests.cs` (the vocabulary of `Check_EveryAcceptedCommandLineOfTheTool_IsReadTheSameBySystemCommandLine`)
- Test: `tests/SqlSource.Tool.Tests/CheckTests.cs` (create), `tests/SqlSource.Tool.Tests/RunDecisionsTests.cs`

**Interfaces:**
- Consumes: `RunDecisions.Decide(RunPlan, bool force, ICollection<ToolDiagnostic>)`, `DescribeRun.RunAsync`, `DescribeScene`, `FakeDescriber`.
- Produces:
  - `DescribeOptions(bool Force, bool Check, bool HasFilter, ImmutableArray<ConnectionArgument> Connections, ImmutableArray<string> Databases)`.
  - `RunDecisions.Decide(RunPlan plan, bool force, ICollection<ToolDiagnostic> errors, bool check = false)`: under `check` a sidecar of a higher format version is no error and fails no query.
  - The option `--check` of `describe`, a flag, hidden until task 3.

The exit code in this task is `0` or `1`.  `2` comes with the comparison, in task 3, so no test here asserts the exit code of a run whose sidecars differ, and none asserts the summary line, which task 4 changes.

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tool.Tests/CheckTests.cs`:

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tool.Tests;

// "describe --check": every query is described, the sidecars on the disk are compared, and no file is changed.
public sealed class CheckTests : IDisposable
{
    private const string Users = "-- name: GetUser\nSELECT 1;\n\n-- name: ListUsers\nSELECT 2;\n";

    private static readonly DateTime Old = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);

    private readonly DescribeScene _scene = new();
    private readonly string _users;

    public CheckTests()
    {
        _users = _scene.Project.AddSql("Users.sql", Users);
    }

    private string Sidecar => _users + ".json";

    public void Dispose() => _scene.Dispose();

    // Describes, and makes the sidecar look old, so that a write would show.
    private async Task<byte[]> DescribeFirstAsync()
    {
        (await _scene.DescribeAsync()).ExitCode.ShouldBe(0);
        File.SetLastWriteTimeUtc(Sidecar, Old);
        return await File.ReadAllBytesAsync(Sidecar, TestContext.Current.CancellationToken);
    }

    private async Task ShouldBeUntouchedAsync(byte[] before)
    {
        (await File.ReadAllBytesAsync(Sidecar, TestContext.Current.CancellationToken)).ShouldBe(before);
        File.GetLastWriteTimeUtc(Sidecar).ShouldBe(Old);
    }

    [Theory]
    [InlineData("--check")]
    [InlineData("--check", "--force")]
    public async Task Check_SidecarsThatDescribeWrote_DescribesEveryQueryAgainAndChangesNoFile(params string[] args)
    {
        var before = await DescribeFirstAsync();

        var result = await _scene.DescribeAsync(args);

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBeEmpty();
        // Two by the first run, and two by the check: none is skipped for a current entry.
        _scene.Describer.Described.Count.ShouldBe(4);
        await ShouldBeUntouchedAsync(before);
    }

    [Fact]
    public async Task Check_NoSidecar_MakesNone()
    {
        _ = await _scene.DescribeAsync("--check");

        _scene.Describer.Described.Count.ShouldBe(2);
        File.Exists(Sidecar).ShouldBeFalse();
        Directory.GetFiles(Path.GetDirectoryName(_users)!, "*.tmp").ShouldBeEmpty();
    }

    [Fact]
    public async Task Check_QueryThatFails_ExitsOneAndChangesNoFile()
    {
        var before = await DescribeFirstAsync();
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC999: ");
        // A description that is not saved is no error of a check, which saves none.
        result.Error.ShouldNotContain("SQLSRC217");
        await ShouldBeUntouchedAsync(before);
    }

    // Without --check a database whose queries are all current needs no connection.
    [Fact]
    public async Task Check_DatabaseWithoutAConnection_IsSqlsrc213ThoughEveryEntryIsCurrent()
    {
        _ = await DescribeFirstAsync();
        _ = _scene.Run.Environment.Remove("SQLSOURCE_CONNECTION_POSTGRES");

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC213: No connection is given for the database 'postgres'");
    }

    [Fact]
    public async Task Check_SidecarOfAHigherFormatVersion_IsNotSqlsrc221AndItsQueriesAreDescribed()
    {
        _ = await DescribeFirstAsync();
        var newer = SidecarWriter.Write(TestSidecar.Read(_users) with { FormatVersion = 2 });
        await File.WriteAllTextAsync(Sidecar, newer, TestContext.Current.CancellationToken);

        var result = await _scene.DescribeAsync("--check");

        result.Error.ShouldNotContain("SQLSRC221");
        _scene.Describer.Described.Count.ShouldBe(4);
        (await File.ReadAllTextAsync(Sidecar, TestContext.Current.CancellationToken)).ShouldBe(newer);
    }
}
```

Add to `tests/SqlSource.Tool.Tests/RunDecisionsTests.cs`, beside the test of a higher format version that is there (read that test first, and build the plan and the sidecar the way it does):

```csharp
    // "--check" writes nothing, so a sidecar of a newer tool is no error for it: it is a difference, and the queries
    // of its file are described as any other.
    [Fact]
    public void Decide_SidecarOfAHigherFormatVersionUnderCheck_IsNoErrorAndItsQueriesAreDescribed()
    {
        var plan = Plans.Of(_project);
        _ = TestSidecar.Write(plan.Files[0], [], formatVersion: SidecarFormat.Version + 1);

        var file = RunDecisions.Decide(plan, force: true, _errors, check: true)[0];

        _errors.ShouldBeEmpty();
        file.Queries.Where(query => query.Planned.NeedsEntry)
            .ShouldAllBe(query => query.State == QueryState.ToDescribe);
    }
```

In `tests/SqlSource.Tool.Tests/UsageCheckTests.cs`, in the vocabulary of `Check_EveryAcceptedCommandLineOfTheTool_IsReadTheSameBySystemCommandLine`, add `"--check",` after `"--force=V",`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL, `Decide` has no parameter named `check`.

- [ ] **Step 3: Give the options and the command the flag**

In `src/SqlSource.Tool/Describing/DescribeOptions.cs`, add the parameter and its documentation after `Force`:

```csharp
/// <param name="Check">
/// Whether the run is <c>--check</c>: every selected query is described, as under <c>--force</c>, the sidecars on
/// the disk are compared with what was described, and no file is changed.
/// </param>
```

```csharp
internal sealed record DescribeOptions(
    bool Force,
    bool Check,
    bool HasFilter,
    ImmutableArray<ConnectionArgument> Connections,
    ImmutableArray<string> Databases
);
```

In `src/SqlSource.Tool/DescribeCommand.cs`:

```csharp
    private const string CheckOption = "--check";
```

After the declaration of `force` in `Create`:

```csharp
        // Hidden until it compares: an option is in the help once it does what it says.
        var check = new Option<bool>(CheckOption)
        {
            Description =
                "Describe every query of the run and compare with the sidecars that are there.  Writes nothing, "
                + "and exits with 2 when a sidecar is out of date.  The command for a build server.",
            Arity = ArgumentArity.Zero,
            Hidden = true,
        };
```

Add `check,` after `force,` in the command's initializer, and in `OptionsOf` pass it second:

```csharp
        return new DescribeOptions(
            parsed.GetValue<bool>(ForceOption),
            parsed.GetValue<bool>(CheckOption),
            projects.Length > 0 || databases.Length > 0 || paths.Any(SqlPath.IsSqlFile),
            connections.ToImmutable(),
            [.. databases.Distinct(StringComparer.OrdinalIgnoreCase)]
        );
```

- [ ] **Step 4: Let a check describe the queries of a newer tool's sidecar**

In `src/SqlSource.Tool/Describing/RunDecisions.cs`, change the public `Decide`: its signature, the documentation of the new parameter, the condition of `SQLSRC221`, and the call of the private `Decide`.

```csharp
    /// <param name="check">
    /// Whether the run is <c>--check</c>, which writes nothing: a sidecar of a newer tool is then no error, and
    /// the queries of its file are described as any other.  The comparison reports the file as a difference.
    /// </param>
    public static ImmutableArray<FileWork> Decide(
        RunPlan plan,
        bool force,
        ICollection<ToolDiagnostic> errors,
        bool check = false
    )
```

```csharp
            if (!check && onDisk?.NewerFormat is { } newer)
```

```csharp
            // Only a run that writes must leave a newer tool's file alone.
            var newerFails = !check && onDisk?.NewerFormat is not null;
            files.Add(
                new FileWork(
                    file,
                    onDisk,
                    [.. file.Queries.Select(query => Decide(query, ready, onDisk, force, newerFails))]
                )
            );
```

And the private one:

```csharp
    private static QueryWork Decide(PlannedQuery query, bool ready, SidecarOnDisk? onDisk, bool force, bool newerFails)
    {
        if (!query.NeedsEntry)
        {
            return new QueryWork(query, QueryState.NotNeeded);
        }

        // A file that is not ready has the plan's error, and a sidecar of a newer tool has SQLSRC221.
        if (!ready || query.Problems != QueryProblems.None || (query.IsSelected && newerFails))
        {
            return new QueryWork(query, QueryState.Failed);
        }
```

The rest of the private `Decide` stays as it is.

- [ ] **Step 5: Describe everything and apply nothing**

In `src/SqlSource.Tool/Describing/DescribeRun.cs`, replace the call of `RunDecisions.Decide` and the call of `SidecarStore.Apply`:

```csharp
        // --check describes every selected query, as --force does.
        var files = RunDecisions.Decide(plan, options.Force || options.Check, errors, options.Check);
```

```csharp
        var outcomes = FileOutcomes.Decide(files, options.HasFilter, reporter.Count > 0, SidecarOnDisk.Read);
        if (!options.Check)
        {
            SidecarStore.Apply(outcomes, reporter, cancellationToken);
        }
```

Update the remark of the class: where it says that sub-phase 2.6 compares where `SidecarStore` writes, say that `--check` applies nothing.

- [ ] **Step 6: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.CheckTests`
Expected: PASS, 6 tests.

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.  `DescribeTests` has a test of what `describe --help` shows; it still passes, since the option is hidden.

- [ ] **Step 7: Format, validate, commit**

Run `./format.sh`, then `./pre-commit-validation.sh` as its own command.

```bash
git add src tests
git commit -m "Describe every query under --check and write nothing

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The comparison, `SQLSRC219`, and the exit code `2`

**Files:**
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`
- Modify: `src/SqlSource.Tool/Describing/SidecarOnDisk.cs`, `QueryWork.cs`, `DescribeRun.cs`
- Create: `src/SqlSource.Tool/Describing/FileCheck.cs`, `src/SqlSource.Tool/Describing/SidecarCheck.cs`
- Modify: `src/SqlSource.Tool/Reporting/Reporter.cs`, `src/SqlSource.Tool/Cli.cs`, `src/SqlSource.Tool/DescribeCommand.cs`
- Modify: `src/SqlSource.Tool/AGENTS.md`, `src/SqlSource.Tool/README.md`
- Test: `tests/SqlSource.Tool.Tests/CheckTests.cs`, `ReporterTests.cs`, `DescribeTests.cs`

**Interfaces:**
- Consumes: `SidecarComparer.FindDifference(SidecarEntry committed, SidecarEntry described)` returning `SidecarDifference(Path, Committed, Described)` or null; `FileOutcomes.Decide`; `FileAction.Delete`; `SidecarEntry.NameSpan`; `ToolDiagnostic.At` and `ForFile`.
- Produces:
  - `ToolDiagnostics.SidecarOutOfDate`, `SQLSRC219`, message `The sidecar is out of date for '{0}': {1}`.
  - `SidecarOnDisk(string Path, bool Exists, string? Text, Sidecar? Sidecar, int? FormatVersion)` with the computed `Usable` and `NewerFormat` it had as parameters.
  - `QueryWork.Differs`, a `bool` the comparison sets.
  - `FileCheck(PlannedFile File, string SidecarPath, bool WasCompared, EquatableArray<ToolDiagnostic> Differences)`.
  - `SidecarCheck.Compare(ImmutableArray<FileWork> files, ImmutableArray<FileOutcome> outcomes)` returning `ImmutableArray<FileCheck>`, one for each file in the files' order.  Task 4 gives it a third parameter.
  - `Reporter.DifferenceCount`, and `Cli.ExitCodeOf(Reporter reporter, int exitCode)`.

- [ ] **Step 1: Add the descriptor in its four places**

In `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, after `FileCannotBeChanged`:

```csharp
    public static readonly DiagnosticDescriptor SidecarOutOfDate = new(
        id: "SQLSRC219",
        title: "Sidecar is out of date",
        messageFormat: "The sidecar is out of date for '{0}': {1}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc219",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

In `All`, add `SidecarOutOfDate,` after `FileCannotBeChanged,`.

In `src/SqlSource/AnalyzerReleases.Unshipped.md`, after the row of `SQLSRC218`:

```
SQLSRC219 | SqlSource | Error | Sidecar is out of date
```

In `docs/diagnostics.md`, in the table at the top, after the row of `SQLSRC218`:

```markdown
| [SQLSRC219](#sqlsrc219) | Sidecar is out of date |
```

In the paragraph under that table, replace `and exits with the code 1.` with `and exits with the code 1, or with 2 when it reported nothing but \`SQLSRC219\`.`

Between the sections of `SQLSRC218` and `SQLSRC220`:

````markdown
## SQLSRC219

**Sidecar is out of date**

`describe --check` asked the database to describe every query of the run and compared what it said with the sidecars that are there.  It changes no file.  The message names a query and says what differs, or names the `.sql` file when the whole sidecar does:

| The message ends with | What it means |
|----|----|
| `the sidecar does not exist` | A query of the file needs an entry, and the file has no sidecar |
| `the sidecar cannot be read`, or `the sidecar has format 2, and this tool writes 1` | The sidecar is no sidecar of this version of the tool.  A merge conflict in it is the usual cause of the first |
| `the sidecar has no entry` | The query was added, or renamed, since the sidecar was written |
| `the query has changed since it was described` | The SQL, a `-- param:` marker or the default of a token changed |
| `columns[2].nullable is true, and the database says false` | The database describes the query another way than the sidecar holds.  A change to the schema is the usual cause |
| `database is "billing", and the project says "reports"` | The query belongs to another database, or another engine, than the one it was described against |
| `the query is not in the file`, or `the query needs no entry` | The sidecar holds an entry for a query that was removed, or whose output is now `sql` |
| `no query of the file needs an entry` | The sidecar belongs to a file that needs none any more |

```console
/work/App/Queries/Users.sql(3,10): error SQLSRC219: The sidecar is out of date for 'GetUser': columns[2].nullable is true, and the database says false
    help: run 'dotnet sqlsource describe' and commit the sidecars
```

Run `dotnet sqlsource describe` with the filters the check had, and commit the sidecars it writes.  After a change to the schema, run it with `--force`: the tool cannot see such a change, and skips a query whose SQL is as it was.  Only the first difference of a query is named.  What is compared is what changes generated code: a query's hash, engine, database and result kind, the table it matches, and the name, the position, the type and the nullability of each parameter and column.  The versions of the tool and of the server, and where a value came from, are not.

The tool exits with the code 2 when it reported this error and no other, so that a build can tell a sidecar that is out of date from a check that could not be made.  A query that could not be described has an error of its own, and the exit code is then 1.
````

- [ ] **Step 2: Write the failing tests**

In `tests/SqlSource.Tool.Tests/ReporterTests.cs`, add:

```csharp
    // The exit code 2 is for a run that reported nothing but SQLSRC219.
    [Fact]
    public void Report_Sqlsrc219_IsCountedAsAnErrorAndAsADifference()
    {
        var reporter = new Reporter(_error);

        reporter.Report(Failure);
        reporter.Report(ToolDiagnostic.Create(ToolDiagnostics.SidecarOutOfDate, "GetUser", "the sidecar has no entry"));

        reporter.Count.ShouldBe(2);
        reporter.DifferenceCount.ShouldBe(1);
    }
```

In `tests/SqlSource.Tool.Tests/DescribeTests.cs`, in `Run_Help_ShowsTheProjectOption`, add `result.Out.ShouldContain("--check");`.

In `tests/SqlSource.Tool.Tests/CheckTests.cs`, add these usings and members.  `QueryDescription` and `SidecarColumn` come from `SqlSource.Tool.Describing` and `SqlSource.Snapshot`.

```csharp
using System.Linq;
using System.Text;
using SqlSource.Tool.Describing;
```

```csharp
    private const string Help = "    help: run 'dotnet sqlsource describe' and commit the sidecars\n";

    // A statement that produces rows, where the describer of the tests says none unless told.
    private static readonly QueryDescription Rows = new(
        SidecarResultKind.Rows,
        EquatableArray<DescribedParameter>.Empty,
        EquatableArray<SidecarColumn>.Empty,
        null,
        "not-needed",
        "no-origin"
    );

    // One SQLSRC219 as the reporter writes it.  The origin is a path with a position, or a path and a space.
    private static string Difference(string origin, string subject, string what) =>
        $"{origin}: error SQLSRC219: The sidecar is out of date for '{subject}': {what}\n{Help}{DescribeScene.See}219\n";

    // Where a key stands in a text, as the reporter writes a position: "(line,column)", each counted from one.
    private static string PositionOf(string text, string key)
    {
        var index = text.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
        var line = text[..index].Count(character => character == '\n') + 1;
        return $"({line},{index - text.LastIndexOf('\n', index)})";
    }

    // Writes the sidecar again with a change, and makes it look old.
    private async Task<string> RewriteAsync(Func<Sidecar, Sidecar> change)
    {
        var text = SidecarWriter.Write(change(TestSidecar.Read(_users)));
        await File.WriteAllTextAsync(Sidecar, text, TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(Sidecar, Old);
        return text;
    }

    private static Sidecar WithEntries(Sidecar sidecar, params SidecarEntry[] entries) =>
        sidecar with
        {
            Queries = new EquatableArray<SidecarEntry>([.. entries]),
        };

    [Fact]
    public async Task Check_NoSidecar_IsOneDifferenceAtTheFirstQueryAndExitsTwo()
    {
        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(Difference($"{_users}(1,10)", _users, "the sidecar does not exist"));
        File.Exists(Sidecar).ShouldBeFalse();
    }

    [Theory]
    [InlineData("a merge conflict")]
    [InlineData("a directory")]
    public async Task Check_SidecarThatCannotBeRead_IsOneDifferenceAtTheSidecar(string kind)
    {
        var written = Encoding.UTF8.GetString(await DescribeFirstAsync());
        if (kind == "a directory")
        {
            File.Delete(Sidecar);
            _ = Directory.CreateDirectory(Sidecar);
        }
        else
        {
            await File.WriteAllTextAsync(Sidecar, "<<<<<<< HEAD\n" + written, TestContext.Current.CancellationToken);
        }

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(Difference($"{Sidecar} ", _users, "the sidecar cannot be read"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Check_SidecarOfAnotherFormatVersion_IsOneDifferenceAtTheSidecar(int version)
    {
        _ = await DescribeFirstAsync();
        _ = await RewriteAsync(sidecar => sidecar with { FormatVersion = version });

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(
            Difference(
                $"{Sidecar} ",
                _users,
                $"the sidecar has format {version}, and this tool writes {SidecarFormat.Version}"
            )
        );
    }

    [Fact]
    public async Task Check_QueryWithoutAnEntry_IsADifferenceAtTheQuery()
    {
        _ = await DescribeFirstAsync();
        _ = await RewriteAsync(sidecar => WithEntries(sidecar, sidecar.Find("GetUser").ShouldNotBeNull()));

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(Difference($"{_users}(4,10)", "ListUsers", "the sidecar has no entry"));
    }

    // The commonest difference: SQL that was edited and not described again.  Neither hash is printed.
    [Fact]
    public async Task Check_QueryThatChanged_SaysSoWithoutTheHashes()
    {
        _ = await DescribeFirstAsync();
        var hash = TestSidecar.Read(_users).Find("GetUser").ShouldNotBeNull().Hash;
        await File.WriteAllTextAsync(
            _users,
            Users.Replace("SELECT 1;", "SELECT 11;", StringComparison.Ordinal),
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(
            Difference($"{_users}(1,10)", "GetUser", "the query has changed since it was described")
        );
        result.Error.ShouldNotContain(hash);
    }

    [Fact]
    public async Task Check_DatabaseDescribesTheQueryAnotherWay_NamesThePathAndTheTwoValues()
    {
        var before = await DescribeFirstAsync();
        _scene.Describer.Descriptions["ListUsers"] = Rows;

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(
            Difference($"{_users}(4,10)", "ListUsers", "resultKind is \"none\", and the database says \"rows\"")
        );
        await ShouldBeUntouchedAsync(before);
    }

    // The database of a query comes from the project, not from the server.
    [Fact]
    public async Task Check_EntryOfAnotherDatabase_SaysWhatTheProjectSays()
    {
        _ = await DescribeFirstAsync();
        _ = await RewriteAsync(sidecar =>
            WithEntries(
                sidecar,
                sidecar.Find("GetUser").ShouldNotBeNull() with
                {
                    Database = "elsewhere",
                },
                sidecar.Find("ListUsers").ShouldNotBeNull()
            )
        );

        var result = await _scene.DescribeAsync("--check");

        result.Error.ShouldBe(
            Difference(
                $"{_users}(1,10)",
                "GetUser",
                "database is \"elsewhere\", and the project says \"postgres\""
            )
        );
    }

    [Fact]
    public async Task Check_EntryOfAQueryThatIsGone_IsADifferenceAtTheEntrysLineInTheSidecar()
    {
        _ = await DescribeFirstAsync();
        var text = await RewriteAsync(sidecar =>
            WithEntries(
                sidecar,
                [
                    .. sidecar.Queries,
                    sidecar.Find("GetUser").ShouldNotBeNull() with
                    {
                        Name = "Gone",
                    },
                ]
            )
        );

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(Difference(Sidecar + PositionOf(text, "Gone"), "Gone", "the query is not in the file"));
    }

    [Fact]
    public async Task Check_EntryOfAQueryThatNeedsNone_IsADifferenceAtTheEntrysLineInTheSidecar()
    {
        _ = await DescribeFirstAsync();
        var text = await RewriteAsync(sidecar =>
            WithEntries(
                sidecar,
                [
                    .. sidecar.Queries,
                    sidecar.Find("GetUser").ShouldNotBeNull() with
                    {
                        Name = "Plain",
                    },
                ]
            )
        );
        await File.AppendAllTextAsync(
            _users,
            "\n-- name: Plain\n-- output: sql\nSELECT 3;\n",
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync("--check");

        result.Error.ShouldBe(Difference(Sidecar + PositionOf(text, "Plain"), "Plain", "the query needs no entry"));
    }

    // The sidecar of a file that needs none any more, which "describe" would delete.
    private string AddPlainWithASidecar(int formatVersion = SidecarFormat.Version)
    {
        var plain = _scene.Project.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 4;\n");
        File.WriteAllText(
            plain + ".json",
            SidecarWriter.Write(new Sidecar(formatVersion, PackageVersion.Prefix, EquatableArray<SidecarEntry>.Empty))
        );
        return plain;
    }

    [Fact]
    public async Task Check_SidecarOfAFileThatNeedsNone_IsADifferenceAndTheFileStays()
    {
        _ = await DescribeFirstAsync();
        var plain = AddPlainWithASidecar();

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldBe(Difference($"{plain}.json ", plain, "no query of the file needs an entry"));
        File.Exists(plain + ".json").ShouldBeTrue();
    }

    // "describe" deletes nothing after an error, and leaves a newer tool's file alone: so the check says nothing.
    [Fact]
    public async Task Check_SidecarOfAFileThatNeedsNoneAfterAnError_IsNotReported()
    {
        _ = await DescribeFirstAsync();
        _ = AddPlainWithASidecar();
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldNotContain("SQLSRC219");
    }

    [Fact]
    public async Task Check_SidecarOfANewerToolBesideAFileThatNeedsNone_IsNotReported()
    {
        _ = await DescribeFirstAsync();
        _ = AddPlainWithASidecar(formatVersion: SidecarFormat.Version + 1);

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBeEmpty();
    }

    // What differs between a developer's machine and a build server by design, and the order of the entries.
    [Fact]
    public async Task Check_MembersThatAreNotCompared_AreNoDifference()
    {
        _ = await DescribeFirstAsync();
        _ = await RewriteAsync(sidecar =>
            WithEntries(
                sidecar with
                {
                    ToolVersion = "0.0.1",
                },
                [.. sidecar.Queries.Reverse().Select(entry => entry with { ServerVersion = "99.9" })]
            )
        );

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBeEmpty();
    }

    // "Could not check" is not "differs".
    [Fact]
    public async Task Check_FileNoneOfWhoseQueriesCouldBeDescribed_IsComparedWithNothing()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");
        _scene.Describer.Failures["ListUsers"] = FakeDescriber.Failure("ListUsers");

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldNotContain("SQLSRC219");
    }

    [Fact]
    public async Task Check_FailureBesideADifference_ExitsOneAndReportsBoth()
    {
        _ = await DescribeFirstAsync();
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");
        _scene.Describer.Descriptions["ListUsers"] = Rows;

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC999: ");
        result.Error.ShouldContain("error SQLSRC219: The sidecar is out of date for 'ListUsers': ");
    }

    // A checkout may give the file other line endings and an editor a byte order mark.  Entries are compared, not
    // text, and the line of an entry is counted in the text as it is.
    [Fact]
    public async Task Check_SidecarWithAByteOrderMarkAndCarriageReturns_IsReadAsAnyOther()
    {
        _ = await DescribeFirstAsync();
        var text = SidecarWriter.Write(TestSidecar.Read(_users));
        await File.WriteAllTextAsync(
            Sidecar,
            text.Replace("\n", "\r\n", StringComparison.Ordinal),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public async Task Check_EntryThatIsGoneInASidecarWithCarriageReturns_HasTheLineOfTheTextAsItIs()
    {
        _ = await DescribeFirstAsync();
        var text = await RewriteAsync(sidecar =>
            WithEntries(
                sidecar,
                [
                    .. sidecar.Queries,
                    sidecar.Find("GetUser").ShouldNotBeNull() with
                    {
                        Name = "Gone",
                    },
                ]
            )
        );
        await File.WriteAllTextAsync(
            Sidecar,
            text.Replace("\n", "\r\n", StringComparison.Ordinal),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync("--check");

        // A line break of two characters moves no column: a key stands after the indent of its own line.
        result.Error.ShouldBe(Difference(Sidecar + PositionOf(text, "Gone"), "Gone", "the query is not in the file"));
    }
```

Change the two tests of task 2 that now have an exit code of their own: `Check_NoSidecar_MakesNone` is replaced by `Check_NoSidecar_IsOneDifferenceAtTheFirstQueryAndExitsTwo` above, so delete it and keep its assertion on `*.tmp` by adding `Directory.GetFiles(Path.GetDirectoryName(_users)!, "*.tmp").ShouldBeEmpty();` to the new one.  In `Check_SidecarOfAHigherFormatVersion_IsNotSqlsrc221AndItsQueriesAreDescribed`, add `result.ExitCode.ShouldBe(2);`.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL, `ToolDiagnostics.SidecarOutOfDate` exists by step 1 and `Reporter.DifferenceCount` does not.

- [ ] **Step 4: Count `SQLSRC219` apart, and take the exit code from the two counts**

In `src/SqlSource.Tool/Reporting/Reporter.cs`, add `using SqlSource.Diagnostics;`, a field `private int _differences;`, and:

```csharp
    /// <summary>
    /// How many of the errors written were <c>SQLSRC219</c>, a sidecar that <c>--check</c> found out of date.  A
    /// run that wrote no other error exits with <c>2</c>.
    /// </summary>
    public int DifferenceCount
    {
        get
        {
            lock (_gate)
            {
                return _differences;
            }
        }
    }
```

In `Report`, inside the lock, after `_count++;`:

```csharp
            if (ReferenceEquals(diagnostic.Descriptor, ToolDiagnostics.SidecarOutOfDate))
            {
                _differences++;
            }
```

In `src/SqlSource.Tool/Cli.cs`, replace `return reporter.Count > 0 ? 1 : exitCode;` with `return ExitCodeOf(reporter, exitCode);` and add:

```csharp
    /// <summary>
    /// The exit code of a run: <c>1</c> when an error other than <c>SQLSRC219</c> was reported, else what the
    /// command gave when that is not <c>0</c>, else <c>2</c> when <c>SQLSRC219</c> was reported.
    /// </summary>
    internal static int ExitCodeOf(Reporter reporter, int exitCode)
    {
        if (reporter.Count > reporter.DifferenceCount)
        {
            return 1;
        }

        if (exitCode != 0)
        {
            return exitCode;
        }

        return reporter.DifferenceCount > 0 ? 2 : 0;
    }
```

- [ ] **Step 5: Keep what the comparison needs of a sidecar on disk**

Replace the record and its `Read` in `src/SqlSource.Tool/Describing/SidecarOnDisk.cs`.  `Usable` and `NewerFormat` become computed, so every reader of them stays as it is.

```csharp
/// <summary>
/// The sidecar beside a <c>.sql</c> file as a run found it.
/// </summary>
/// <param name="Path">The full path of the sidecar.</param>
/// <param name="Exists">
/// Whether something is at the path: a file, one the system will not open included, or a directory.
/// </param>
/// <param name="Text">The text of the file, or null when there is none that can be opened.</param>
/// <param name="Sidecar">
/// The sidecar, when it was read in this version of the format, whichever version of the tool wrote it.
/// <c>--check</c> compares it; a run that writes keeps entries of <see cref="Usable" /> alone.
/// </param>
/// <param name="FormatVersion">The format version of the file, or null when it is absent or malformed.</param>
internal sealed record SidecarOnDisk(string Path, bool Exists, string? Text, Sidecar? Sidecar, int? FormatVersion)
{
    /// <summary>
    /// The sidecar, when this version of the tool wrote it in this version of the format.  Only such a one has
    /// entries a run keeps: an entry from an older tool never stands beside a new one.
    /// </summary>
    public Sidecar? Usable => Sidecar is { } sidecar && sidecar.IsWrittenBy(PackageVersion.Prefix) ? sidecar : null;

    /// <summary>The format version of the file, when it is higher than the tool's.</summary>
    public int? NewerFormat => FormatVersion is { } version && version > SidecarFormat.Version ? version : null;

    /// <summary>
    /// Reads the sidecar of a <c>.sql</c> file.  One that is absent, that the system will not open, that is
    /// malformed, or that has a lower format version or another tool version is not usable, and is written again.
    /// </summary>
    public static SidecarOnDisk Read(string sqlPath)
    {
        var path = SidecarFormat.PathFor(sqlPath);
        string text;
        try
        {
            // Without a byte order mark, as the reader asks.
            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // No file, a directory, or a file that is locked: writing it will say so if it cannot be mended.
            return new SidecarOnDisk(path, File.Exists(path) || Directory.Exists(path), null, null, null);
        }

        var read = SidecarReader.Read(text);
        return new SidecarOnDisk(path, Exists: true, text, read.Sidecar, read.FormatVersion);
    }
}
```

In `src/SqlSource.Tool/Describing/QueryWork.cs`, add:

```csharp
    /// <summary>
    /// Whether <c>--check</c> reported a difference at the query, or at the whole of its sidecar.  The summary of
    /// a check counts it.
    /// </summary>
    public bool Differs { get; set; }
```

- [ ] **Step 6: Write the comparison**

Create `src/SqlSource.Tool/Describing/FileCheck.cs`:

```csharp
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// What <c>--check</c> found for the sidecar of one file, as data.
/// </summary>
/// <param name="File">The file of the plan.</param>
/// <param name="SidecarPath">The full path of its sidecar.</param>
/// <param name="WasCompared">
/// Whether the sidecar was compared with anything.  It was not for a file that is not ready, that has no selected
/// query that needs an entry, or none of whose selected queries could be described.
/// </param>
/// <param name="Differences">An <c>SQLSRC219</c> for each difference, in the order they are to be reported.</param>
internal sealed record FileCheck(
    PlannedFile File,
    string SidecarPath,
    bool WasCompared,
    EquatableArray<ToolDiagnostic> Differences
);
```

Create `src/SqlSource.Tool/Describing/SidecarCheck.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Diagnostics;
using SqlSource.Snapshot;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The last step of <c>describe --check</c>, in the place of <see cref="SidecarStore" />: compares what was
/// described with the sidecars on the disk.  It reads nothing, changes nothing and reports nothing: each difference
/// is an <c>SQLSRC219</c> in what it returns.
/// </summary>
/// <remarks>
/// It works from each selected query's described entry and not from a file's target: under a filter, a file with a
/// query that was left out has none.  What it compares is within what <c>describe</c> with the same filters would
/// change, so a difference is one that <c>describe</c> mends.
/// </remarks>
internal static class SidecarCheck
{
    private static readonly ContinuationLine Help = new(
        "help",
        "run 'dotnet sqlsource describe' and commit the sidecars"
    );

    /// <summary>
    /// Compares each file, in the files' order, and sets <see cref="QueryWork.Differs" /> on each query that a
    /// difference was reported at, or at the whole of whose sidecar one was.
    /// </summary>
    /// <param name="files">The files, with every selected query that needs an entry described or failed.</param>
    /// <param name="outcomes">What <see cref="FileOutcomes" /> decided for the same files, in the same order.</param>
    public static ImmutableArray<FileCheck> Compare(
        ImmutableArray<FileWork> files,
        ImmutableArray<FileOutcome> outcomes
    ) => [.. files.Select((file, index) => Compare(file, outcomes[index]))];

    private static FileCheck Compare(FileWork file, FileOutcome outcome)
    {
        var sql = file.File.Path;

        // The one difference this step does not decide: a sidecar is deleted only where FileOutcomes says so.
        if (outcome.Action == FileAction.Delete)
        {
            return Of(outcome, [AtSidecar(outcome.SidecarPath, sql, "no query of the file needs an entry")]);
        }

        var selected = file
            .Queries.Where(static query => query.Planned is { NeedsEntry: true, IsSelected: true })
            .ToList();
        var described = selected.Where(static query => query.State == QueryState.Described).ToList();

        // A file none of whose queries could be described is compared with nothing: its errors stand.
        if (file.OnDisk is not { } onDisk || described.Count == 0)
        {
            return new FileCheck(
                file.File,
                outcome.SidecarPath,
                WasCompared: false,
                EquatableArray<ToolDiagnostic>.Empty
            );
        }

        if (onDisk.Sidecar is not { } sidecar)
        {
            // Once for the file: its queries are not reported one by one, and each that was described differs.
            foreach (var query in described)
            {
                query.Differs = true;
            }

            return Of(outcome, [WholeSidecar(onDisk, selected[0], sql)]);
        }

        var differences = new List<ToolDiagnostic>();
        foreach (var query in described)
        {
            if (DifferenceOf(query, sidecar) is { } what)
            {
                query.Differs = true;
                differences.Add(
                    ToolDiagnostic
                        .At(
                            ToolDiagnostics.SidecarOutOfDate,
                            query.Planned.Query.NameLocation,
                            query.Planned.Query.Name,
                            what
                        )
                        .WithLines(Help)
                );
            }
        }

        differences.AddRange(EntriesWithoutAQuery(file, onDisk, sidecar));
        return Of(outcome, differences);
    }

    // No file where the sidecar would be, at the first selected query; or a file that is no sidecar of this format,
    // at the file.
    private static ToolDiagnostic WholeSidecar(SidecarOnDisk onDisk, QueryWork first, string sql)
    {
        if (!onDisk.Exists)
        {
            return ToolDiagnostic
                .At(
                    ToolDiagnostics.SidecarOutOfDate,
                    first.Planned.Query.NameLocation,
                    sql,
                    "the sidecar does not exist"
                )
                .WithLines(Help);
        }

        return AtSidecar(
            onDisk.Path,
            sql,
            onDisk.FormatVersion is { } version
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"the sidecar has format {version}, and this tool writes {SidecarFormat.Version}"
                )
                : "the sidecar cannot be read"
        );
    }

    // What differs for a query that was described, or null.
    private static string? DifferenceOf(QueryWork query, Sidecar sidecar)
    {
        if (sidecar.Find(query.Planned.Query.Name) is not { } committed)
        {
            return "the sidecar has no entry";
        }

        var described =
            query.Entry ?? throw new InvalidOperationException($"'{query.Planned.Query.Name}' has no entry.");
        return SidecarComparer.FindDifference(committed, described) is { } difference ? TextOf(difference) : null;
    }

    // The hash, the engine and the database are the project's: the database says nothing of them.  Two hashes of 64
    // characters tell nobody anything.
    private static string TextOf(SidecarDifference difference) =>
        difference.Path switch
        {
            SidecarKeys.Hash => "the query has changed since it was described",
            SidecarKeys.Engine or SidecarKeys.Database =>
                $"{difference.Path} is {difference.Committed}, and the project says {difference.Described}",
            _ => $"{difference.Path} is {difference.Committed}, and the database says {difference.Described}",
        };

    // An entry for a name that no query of the file has, or for a query that needs none: writing drops it.
    private static IEnumerable<ToolDiagnostic> EntriesWithoutAQuery(FileWork file, SidecarOnDisk onDisk, Sidecar sidecar)
    {
        SourceText? text = null;
        foreach (var entry in sidecar.Queries)
        {
            var query = file.Queries.FirstOrDefault(candidate =>
                string.Equals(candidate.Planned.Query.Name, entry.Name, StringComparison.Ordinal)
            );
            if (query is { Planned.NeedsEntry: true })
            {
                continue;
            }

            text ??= SourceText.From(onDisk.Text ?? "");
            yield return ToolDiagnostic
                .At(
                    ToolDiagnostics.SidecarOutOfDate,
                    onDisk.Path,
                    text.Lines.GetLinePosition(entry.NameSpan.Start),
                    entry.Name,
                    query is null ? "the query is not in the file" : "the query needs no entry"
                )
                .WithLines(Help);
        }
    }

    private static ToolDiagnostic AtSidecar(string sidecarPath, string sql, string what) =>
        ToolDiagnostic.ForFile(ToolDiagnostics.SidecarOutOfDate, sidecarPath, sql, what).WithLines(Help);

    private static FileCheck Of(FileOutcome outcome, IEnumerable<ToolDiagnostic> differences) =>
        new(
            outcome.File,
            outcome.SidecarPath,
            WasCompared: true,
            new EquatableArray<ToolDiagnostic>([.. differences])
        );
}
```

- [ ] **Step 7: Compare where the run writes**

In `src/SqlSource.Tool/Describing/DescribeRun.cs`, replace the `if (!options.Check)` of task 2:

```csharp
        if (options.Check)
        {
            foreach (var check in SidecarCheck.Compare(files, outcomes))
            {
                foreach (var difference in check.Differences)
                {
                    reporter.Report(difference);
                }
            }
        }
        else
        {
            SidecarStore.Apply(outcomes, reporter, cancellationToken);
        }
```

In `src/SqlSource.Tool/DescribeCommand.cs`, remove `Hidden = true,` and the comment above the option that explains it.

- [ ] **Step 8: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.CheckTests`
Expected: PASS.

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.  `DiagnosticsDocumentTests` holds the section of step 1 to the descriptor.

- [ ] **Step 9: Bring the documents this task makes wrong**

In `src/SqlSource.Tool/AGENTS.md`:

- In the bullet that starts **`Describing/` is a run after its plan**, replace its last sentence, the one that names sub-phase 2.6, with: `` `--check` shares the run and replaces only its last step: `Describing/SidecarCheck.cs` compares where `SidecarStore` writes, from each selected query's described entry, and returns its differences as data.  So the step that decides must never write, the step that writes must never decide, and the step that compares does neither: what it says of a sidecar that would be deleted it takes from `FileOutcomes`. ``
- Replace the bullet that starts **The exit code is `0`, `1` or `2`.** with: `` **The exit code is `0`, `1` or `2`.**  `1` when the reporter wrote any error but `SQLSRC219`, when the command line was wrong, and when the run was cancelled.  `2` when it wrote `SQLSRC219` and nothing else: a sidecar that `--check` found out of date.  `Cli.ExitCodeOf` takes both from `Reporter.Count` and `Reporter.DifferenceCount`, so a command reports and goes on, and never returns `1` itself.  A query that could not be described is "could not check" and never `SQLSRC219`. ``

In `src/SqlSource.Tool/README.md`, add `$ dotnet sqlsource describe --check` as the last line of the first `console` block, and after the paragraph that ends with the two summary lines and before `The exit code is`, add:

```markdown
`--check` is the command for a build server.  It describes every query of the run, as `--force` does, compares what the database says with the sidecars that are there, and changes no file.  A sidecar that is out of date is [`SQLSRC219`](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc219), which says what differs and where.  It compares what changes generated code and not the versions of the tool or of the server, so a sidecar that a developer wrote passes on a server of another patch.  Every database of the run needs a connection.
```

Replace the sentence that starts `The exit code is` with:

```markdown
The exit code is `0`; `1` when anything else was reported as an error, when the command line is wrong, or when the run was cancelled; and `2` when `--check` found a sidecar out of date and nothing else failed.
```

- [ ] **Step 10: Format, validate, commit**

Run `./format.sh`, then `./pre-commit-validation.sh` as its own command.

```bash
git add src tests docs
git commit -m "Compare the sidecars under --check: SQLSRC219 and the exit code 2

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: The comparison under filters, and the summary of a check

**Files:**
- Modify: `src/SqlSource.Tool/Describing/SidecarCheck.cs`, `RunSummary.cs`, `DescribeRun.cs`
- Modify: `src/SqlSource.Tool/README.md`
- Test: `tests/SqlSource.Tool.Tests/CheckFilterTests.cs` (create), `CheckTests.cs`

**Interfaces:**
- Consumes: `SidecarCheck.Compare`, `QueryWork.Differs`, `DescribeOptions.Databases`, `RunSummary.Lines`.
- Produces:
  - `SidecarCheck.Compare(ImmutableArray<FileWork> files, ImmutableArray<FileOutcome> outcomes, ImmutableArray<string> databases)`: `databases` are the names of `--database`; an entry without a query is reported only when its own database is one of them, or when there are none.
  - `RunSummary.Lines(planned, selected, files, connections, named, bool check)`: under `check` each line is `<name> (<engine>): <n> same, <n> differ, <n> failed`.

Rows one, three and four of the spec's table already hold for selected queries alone: task 3 compares only those.  Row six already holds only without a filter: `FileOutcomes` decides it.  What is left is row five's scope and the summary.

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tool.Tests/CheckFilterTests.cs`:

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using Xunit;

namespace SqlSource.Tool.Tests;

// "--check" compares within what "describe" with the same filters would change, and counts what it found.
public sealed class CheckFilterTests : IDisposable
{
    private const string Users =
        "-- name: GetUser\nSELECT 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n";

    private static readonly QueryDescription Rows = new(
        SidecarResultKind.Rows,
        EquatableArray<DescribedParameter>.Empty,
        EquatableArray<SidecarColumn>.Empty,
        null,
        "not-needed",
        "no-origin"
    );

    private readonly DescribeScene _scene = new();
    private readonly string _plain;
    private readonly string _users;

    public CheckFilterTests()
    {
        // "Invoices.sql" comes before "Users.sql", so the plan holds "billing" first.
        _ = _scene.Project.AddSql("Invoices.sql", "-- name: ListInvoices\n-- database: billing\nSELECT 3;\n");
        _plain = _scene.Project.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 4;\n");
        _users = _scene.Project.AddSql("Users.sql", Users);
        // Two units stand in the working directory for the test of "--project", so each run names its own.
        _ = _scene.Run.Folder.WriteFile("App.slnx", "<Solution><Project Path=\"App.csproj\" /></Solution>");
    }

    public void Dispose() => _scene.Dispose();

    private Task<CliResult> DescribeAsync(params string[] args) => _scene.DescribeAsync(["App.csproj", .. args]);

    private void AddASidecarToPlain() =>
        File.WriteAllText(
            _plain + ".json",
            SidecarWriter.Write(
                new Sidecar(SidecarFormat.Version, PackageVersion.Prefix, EquatableArray<SidecarEntry>.Empty)
            )
        );

    [Fact]
    public async Task Check_NoFilter_CountsEachQueryAsSameDifferOrFailed()
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        _scene.Describer.Descriptions["GetInvoice"] = Rows;
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        var result = await DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBe(
            "billing (postgres): 1 same, 1 differ, 0 failed\npostgres (postgres): 0 same, 0 differ, 1 failed\n"
        );
    }

    // Each query of a sidecar that is absent or cannot be read counts under "differ".
    [Fact]
    public async Task Check_NoSidecars_CountsEveryQueryUnderDiffer()
    {
        var result = await DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Out.ShouldBe(
            "billing (postgres): 0 same, 2 differ, 0 failed\npostgres (postgres): 0 same, 1 differ, 0 failed\n"
        );
    }

    [Fact]
    public async Task Check_DatabaseWithADifferenceInTheOthersEntry_ExitsZero()
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        _scene.Describer.Descriptions["GetUser"] = Rows;

        var result = await DescribeAsync("--check", "--database", "billing");

        result.ShouldBe(new CliResult(0, "billing (postgres): 2 same, 0 differ, 0 failed\n", ""));
    }

    [Fact]
    public async Task Check_FileFilterWithADifferenceInAnotherFile_ExitsZero()
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        _scene.Describer.Descriptions["GetUser"] = Rows;

        var result = await DescribeAsync("--check", "Invoices.sql");

        result.ShouldBe(new CliResult(0, "billing (postgres): 1 same, 0 differ, 0 failed\n", ""));
    }

    // An entry of a query that is gone is reported when its own database is named, and not when another is.
    [Theory]
    [InlineData("billing", true)]
    [InlineData("postgres", false)]
    public async Task Check_EntryOfAQueryThatIsGoneUnderDatabase_IsReportedForItsOwnDatabaseAlone(
        string named,
        bool reported
    )
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        var sidecar = TestSidecar.Read(_users);
        var gone = sidecar.Find("GetInvoice").ShouldNotBeNull() with { Name = "Gone" };
        await File.WriteAllTextAsync(
            _users + ".json",
            SidecarWriter.Write(sidecar with { Queries = new EquatableArray<SidecarEntry>([.. sidecar.Queries, gone]) }),
            TestContext.Current.CancellationToken
        );

        var result = await DescribeAsync("--check", "--database", named);

        result.ExitCode.ShouldBe(reported ? 2 : 0);
        result.Error.Contains("for 'Gone': the query is not in the file", StringComparison.Ordinal).ShouldBe(reported);
    }

    // "describe" deletes a sidecar only in a run with no filter, so only such a check says it would.
    [Theory]
    [InlineData(2, "App.csproj")]
    [InlineData(0, "App.csproj", "--database", "postgres", "--database", "billing")]
    [InlineData(0, "App.csproj", "Users.sql", "Invoices.sql")]
    [InlineData(0, "App.slnx", "--project", "App.csproj")]
    public async Task Check_SidecarOfAFileThatNeedsNone_IsADifferenceOnlyWithoutAFilter(
        int exitCode,
        params string[] args
    )
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        AddASidecarToPlain();

        var result = await _scene.DescribeAsync(["--check", .. args]);

        result.ExitCode.ShouldBe(exitCode);
        File.Exists(_plain + ".json").ShouldBeTrue();
    }

    // What "describe" with a filter wrote, "--check" with the same filter finds the same.
    [Theory]
    [InlineData("App.csproj", "--database", "billing")]
    [InlineData("App.csproj", "Invoices.sql")]
    [InlineData("App.slnx", "--project", "App.csproj")]
    public async Task Check_AfterADescribeWithTheSameFilterThatSucceeded_ExitsZero(params string[] args)
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        (await _scene.DescribeAsync(["--force", .. args])).ExitCode.ShouldBe(0);

        var result = await _scene.DescribeAsync(["--check", .. args]);

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public async Task Check_DatabaseNameThatNoQueryHas_HasThreeZerosInTheWordsOfACheck()
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);

        var result = await DescribeAsync("--check", "--database", "nowhere");

        result.ShouldBe(new CliResult(0, "nowhere: 0 same, 0 differ, 0 failed\n", ""));
    }
}
```

In `tests/SqlSource.Tool.Tests/CheckTests.cs`, in `Check_SidecarsThatDescribeWrote_DescribesEveryQueryAgainAndChangesNoFile`, add:

```csharp
        result.Out.ShouldBe("postgres (postgres): 2 same, 0 differ, 0 failed\n");
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.CheckFilterTests`
Expected: FAIL.  The summaries say `described` and `skipped`, and `Gone` is reported under `--database postgres`.

- [ ] **Step 3: Hold an entry without a query to the databases that were named**

In `src/SqlSource.Tool/Describing/SidecarCheck.cs`, give `Compare` its third parameter and pass it down to `EntriesWithoutAQuery`:

```csharp
    /// <param name="databases">
    /// The names of <c>--database</c>.  An entry for a query that is gone is reported when its own database is one
    /// of them, or when there are none: a run for one database says nothing of another's entries.
    /// </param>
    public static ImmutableArray<FileCheck> Compare(
        ImmutableArray<FileWork> files,
        ImmutableArray<FileOutcome> outcomes,
        ImmutableArray<string> databases
    ) => [.. files.Select((file, index) => Compare(file, outcomes[index], databases))];
```

`Compare(FileWork, FileOutcome, ImmutableArray<string> databases)` passes `databases` to `EntriesWithoutAQuery(file, onDisk, sidecar, databases)`, whose loop gains, after the `continue` for a query that needs an entry:

```csharp
            if (
                !databases.IsEmpty
                && (
                    entry.Database is not { } database
                    || !databases.Contains(database, StringComparer.OrdinalIgnoreCase)
                )
            )
            {
                continue;
            }
```

In `src/SqlSource.Tool/Describing/DescribeRun.cs`, pass `options.Databases` as the third argument of `SidecarCheck.Compare`.

- [ ] **Step 4: Count a check in its own words**

In `src/SqlSource.Tool/Describing/RunSummary.cs`:

- Give `Lines` a last parameter, `bool check`, documented as: `Whether the run is <c>--check</c>, whose counts are <c>same</c>, <c>differ</c> and <c>failed</c>.`
- Replace the constant `Zeros` with a local in `Lines`: `var zeros = check ? "0 same, 0 differ, 0 failed" : "0 described, 0 skipped, 0 failed";`, and use it in the second loop.
- Replace the body of the first loop:

```csharp
        foreach (var database in selected)
        {
            if (check)
            {
                // The "no connection" form never shows: a check describes every query, so a database with no
                // connection has an error and a failed query.
                var (same, differ, notDescribed) = CountChecked(files, database.Name);
                lines.Add(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{Label(database)}: {same} same, {differ} differ, {notDescribed} failed"
                    )
                );
                continue;
            }

            var (described, skipped, failed) = Count(files, database.Name);
            // The second form has no place for a failure: it is for a database with nothing to describe.
            lines.Add(
                connections.For(database.Name).Value is null && failed == 0
                    ? string.Create(
                        CultureInfo.InvariantCulture,
                        $"{Label(database)}: no connection, {skipped} skipped"
                    )
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $"{Label(database)}: {described} described, {skipped} skipped, {failed} failed"
                    )
            );
        }
```

Add:

```csharp
    // Under --check a selected query that needs an entry was described, and is the same or differs, or it failed.
    private static (int Same, int Differ, int Failed) CountChecked(ImmutableArray<FileWork> files, string database)
    {
        var queries = Selected(files, database).ToList();
        var described = queries.Where(static query => query.State == QueryState.Described).ToList();
        var differ = described.Count(static query => query.Differs);

        return (described.Count - differ, differ, queries.Count - described.Count);
    }
```

In `src/SqlSource.Tool/Describing/DescribeRun.cs`, pass `options.Check` as the last argument of `RunSummary.Lines`.  `SummaryTests` calls the command and not `Lines`, so no test changes for the parameter; if a test does call `Lines`, pass `check: false`.

- [ ] **Step 5: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.CheckFilterTests`
Expected: PASS.

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.

- [ ] **Step 6: Bring the document this task makes wrong**

In `src/SqlSource.Tool/README.md`, at the end of the paragraph on `--check` that task 3 added, add:

```markdown
With `--database`, `--project` or a `.sql` path it compares what `describe` with the same filters would change, and nothing else.  Its summary counts each query as the same, as one that differs, or as one that could not be described:
```

and under it:

````markdown
```console
billing (postgres): 15 same, 1 differ, 0 failed
```
````

- [ ] **Step 7: Format, validate, commit**

Run `./format.sh`, then `./pre-commit-validation.sh` as its own command.

```bash
git add src tests
git commit -m "Hold --check to the filters of the run, and count same, differ and failed

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: The clock, the log's file, `--log` and `SQLSOURCE_LOG`, with `run.start`, `run.end` and `diagnostic`

**Files:**
- Modify: `src/SqlSource.Tool/ToolHost.cs`
- Create: `src/SqlSource.Tool/Logging/LogValue.cs`, `LogMember.cs`, `IRunLog.cs`, `NullRunLog.cs`, `RunLogFile.cs`, `RunTrace.cs`, `LoggedArguments.cs`
- Modify: `src/SqlSource.Tool/Reporting/Reporter.cs`, `src/SqlSource.Tool/Cli.cs`, `src/SqlSource.Tool/DescribeCommand.cs`, `src/SqlSource.Tool/UsageCheck.cs`
- Modify: `src/SqlSource.Tool/README.md`
- Create: `tests/SqlSource.Tool.Tests/TestClock.cs`, `LogReader.cs`, `RunLogFileTests.cs`, `RunLogTests.cs`, `RedactionTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Hosts.cs`, `CliRun.cs`, `DescribeScene.cs`, `CliTests.cs`, `ToolHostTests.cs`, `UsageCheckTests.cs`, `DescribeTests.cs`

**Interfaces:**
- Consumes: `Reporter`, `Cli.ExitCodeOf`, `Usage.Values`, `UsageValue(Option? Option, string Text, int Position)`, `ConnectionArgument.TryParse`, `Redaction.Mark`, `ToolDiagnostics.FileCannotBeChanged`.
- Produces:
  - `ToolHost.Time`, a `TimeProvider`, the record's last parameter.
  - `LogValue`, a closed set: `LogValue.Null`; implicit conversions from `string?`, `bool` and `long`; `LogValue.Of(long?)`; `LogValue.ListOf(IEnumerable<LogValue>)`, `LogValue.ListOfTexts(IEnumerable<string>)`, `LogValue.MapOf(params LogMember[])`; the nested records `Nothing`, `Text(string Value)`, `Flag(bool Value)`, `Number(long Value)`, `Items(EquatableArray<LogValue> Values)`, `Members(EquatableArray<LogMember> Values)`, which compare by their values.
  - `LogMember(string Name, LogValue Value)`, a `readonly record struct`.
  - `IRunLog.Write(string name, params LogMember[] members)`; `NullRunLog.Instance`.
  - `RunLogFile : IRunLog, IDisposable` with `Path`, `Failure`, the constant `MaxStringBytes`, the constructor `RunLogFile(string path, Stream stream, TimeProvider time)` and `static RunLogFile? TryOpen(string given, string workingDirectory, TimeProvider time, out string path, out string reason)`.
  - `RunTrace(IRunLog log)` with `Log` and the settable `Databases`, a `LogValue`.
  - `Reporter.Trace`, a settable `RunTrace`; the reporter writes a `diagnostic` event for each error.
  - `DescribeCommand.LogOption`, `DescribeCommand.LogVariable`, `DescribeCommand.Runs(ParseResult)`, `DescribeCommand.LogPathOf(ParseResult, Func<string, string?>)`.
  - `LoggedArguments.Of(Usage usage, IReadOnlyList<string> args)` returning `LogValue`.
  - In the tests: `TestClock` with `Now` and `Advance(TimeSpan)`; `LogReader.Read(string path)` returning `JsonElement[]` and `LogReader.Names(string path)`; `CliRun.Time`; `DescribeScene.Clock`.

- [ ] **Step 1: Give the host a clock**

In `src/SqlSource.Tool/ToolHost.cs`, add the last parameter and its documentation, and pass `TimeProvider.System` last in `Create`:

```csharp
/// <param name="Time">The clock: the time of a log's events, and how long a step took.</param>
```

```csharp
    Func<string, IDescribeExchange> Exchange,
    TimeProvider Time
```

Create `tests/SqlSource.Tool.Tests/TestClock.cs`:

```csharp
using System;

namespace SqlSource.Tool.Tests;

// A clock that stands still until a test moves it, so that a time and a duration in a log are exact.
internal sealed class TestClock : TimeProvider
{
    private TimeSpan _elapsed;

    public DateTimeOffset Now { get; private set; } = new(2026, 10, 9, 14, 2, 11, 412, TimeSpan.Zero);

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow() => Now;

    public override long GetTimestamp() => _elapsed.Ticks;

    public void Advance(TimeSpan by)
    {
        Now += by;
        _elapsed += by;
    }
}
```

Give every host of the tests a clock:

- `tests/SqlSource.Tool.Tests/Hosts.cs`: pass `TimeProvider.System` last in both `new(...)`.
- `tests/SqlSource.Tool.Tests/CliTests.cs`, in `BuildCommands_EveryOption_TakesNoValueOrNeedsOne`: pass `TimeProvider.System` last.
- `tests/SqlSource.Tool.Tests/CliRun.cs`: add the property and pass `Time` last in `CreateHost`.

```csharp
    // The clock of the run.  The machine's, unless a test brings one it can move.
    public TimeProvider Time { get; set; } = TimeProvider.System;
```

- `tests/SqlSource.Tool.Tests/DescribeScene.cs`: add the property, and `Run.Time = Clock;` as the first line of the constructor.

```csharp
    public TestClock Clock { get; } = new();
```

- `tests/SqlSource.Tool.Tests/ToolHostTests.cs`: add `host.Time.ShouldBeSameAs(TimeProvider.System);`.

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.  Nothing reads the clock yet.

- [ ] **Step 2: Write the failing tests of the writer**

Create `tests/SqlSource.Tool.Tests/LogReader.cs`:

```csharp
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SqlSource.Tool.Tests;

// The lines of a log, each parsed: a line that is not one JSON object fails the test that reads it.
internal static class LogReader
{
    public static JsonElement[] Read(string path) => [.. File.ReadAllLines(path).Select(Parse)];

    public static string?[] Names(string path) => [.. Read(path).Select(line => line.GetProperty("event").GetString())];

    public static JsonElement Parse(string line)
    {
        using var document = JsonDocument.Parse(line);
        return document.RootElement.ValueKind == JsonValueKind.Object
            ? document.RootElement.Clone()
            : throw new InvalidDataException($"A line of the log is {document.RootElement.ValueKind}, not an object.");
    }
}
```

Create `tests/SqlSource.Tool.Tests/RunLogFileTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Text;
using Shouldly;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests;

// The writer of a log's file: one JSON object on a line for each event.
public sealed class RunLogFileTests : IDisposable
{
    private const string Head = "{\"event\":\"e\",\"time\":\"2026-10-09T14:02:11.412Z\"";

    private readonly TestClock _clock = new();
    private readonly TempFolder _folder = new();
    private readonly MemoryStream _stream = new();

    public void Dispose()
    {
        _stream.Dispose();
        _folder.Dispose();
    }

    private string Written() => Encoding.UTF8.GetString(_stream.ToArray());

    [Fact]
    public void Write_Event_IsOneObjectOnALineWithItsNameItsTimeAndItsMembersInOrder()
    {
        using var log = new RunLogFile("/log", _stream, _clock);

        log.Write(
            "e",
            new("text", "a"),
            new("number", 12),
            new("flag", true),
            new("nothing", LogValue.Null),
            new("list", LogValue.ListOfTexts(["b", "c"])),
            new("map", LogValue.MapOf(new("inner", LogValue.Of(null)), new("deep", LogValue.ListOf([LogValue.MapOf()]))))
        );

        Written()
            .ShouldBe(
                Head
                    + ",\"text\":\"a\",\"number\":12,\"flag\":true,\"nothing\":null,\"list\":[\"b\",\"c\"],"
                    + "\"map\":{\"inner\":null,\"deep\":[{}]}}\n"
            );
    }

    [Fact]
    public void Write_TwoEvents_AreTwoLinesEachWithTheTimeItWasWrittenAt()
    {
        using var log = new RunLogFile("/log", _stream, _clock);

        log.Write("e");
        _clock.Advance(TimeSpan.FromMilliseconds(1500));
        log.Write("e");

        Written().ShouldBe(Head + "}\n{\"event\":\"e\",\"time\":\"2026-10-09T14:02:12.912Z\"}\n");
    }

    // A log is for people: a letter outside ASCII is written as it is, and only what JSON must escape is escaped.
    [Fact]
    public void Write_TextOutsideAsciiAndAQuote_IsReadableAndStillOneLine()
    {
        using var log = new RunLogFile("/log", _stream, _clock);

        log.Write("e", new("text", "é <b> \"q\"\nnext"));

        Written().ShouldBe(Head + ",\"text\":\"é <b> \\\"q\\\"\\nnext\"}\n");
    }

    [Fact]
    public void Write_StringOverOneMebibyte_IsCutToThatSizeAndTheObjectIsMarked()
    {
        using var log = new RunLogFile("/log", _stream, _clock);

        log.Write("e", new("big", new string('a', 2 * RunLogFile.MaxStringBytes)), new("small", "b"));

        var line = LogReader.Parse(Written().TrimEnd('\n'));
        line.GetProperty("big").GetString().ShouldBe(new string('a', RunLogFile.MaxStringBytes));
        line.GetProperty("small").GetString().ShouldBe("b");
        line.GetProperty("truncated").GetBoolean().ShouldBeTrue();
    }

    // The size is counted in UTF-8, and a character of four bytes is never cut in two.
    [Fact]
    public void Write_LongStringOfCharactersOfFourBytes_IsCutBetweenTwoOfThem()
    {
        using var log = new RunLogFile("/log", _stream, _clock);

        log.Write("e", new("big", string.Concat(Enumerable.Repeat("😀", 300_000))));

        var line = LogReader.Parse(Written().TrimEnd('\n'));
        var kept = line.GetProperty("big").GetString().ShouldNotBeNull();
        Encoding.UTF8.GetByteCount(kept).ShouldBe(RunLogFile.MaxStringBytes);
        kept.ShouldBe(string.Concat(Enumerable.Repeat("😀", RunLogFile.MaxStringBytes / 4)));
        line.GetProperty("truncated").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void Write_ShortString_IsNotMarked()
    {
        using var log = new RunLogFile("/log", _stream, _clock);

        log.Write("e", new("text", "a"));

        Written().ShouldNotContain("truncated");
    }

    // A server's message may hold half a character.  The line must still be JSON.
    [Fact]
    public void Write_TextWithHalfACharacter_IsWrittenWithTheReplacementCharacter()
    {
        using var log = new RunLogFile("/log", _stream, _clock);

        log.Write("e", new("text", "a\uD83Db"));

        LogReader.Parse(Written().TrimEnd('\n')).GetProperty("text").GetString().ShouldBe("a\uFFFDb");
    }

    // A full disk must not end the run: the writer goes quiet and says why when it is asked.
    [Fact]
    public void Write_StreamThatFails_DoesNotThrowAndKeepsTheFailure()
    {
        using var broken = new BrokenStream();
        using var log = new RunLogFile("/log", broken, _clock);

        log.Write("e");
        log.Write("e");

        log.Failure.ShouldBeOfType<IOException>().Message.ShouldBe("disk full");
        broken.Writes.ShouldBe(1);
    }

    [Fact]
    public void Write_AfterDispose_WritesNothing()
    {
        var log = new RunLogFile("/log", _stream, _clock);
        log.Dispose();

        log.Write("e");

        Written().ShouldBeEmpty();
        log.Failure.ShouldBeNull();
    }

    [Fact]
    public void TryOpen_RelativePath_MakesTheFileInTheWorkingDirectoryAndEmptiesOneThatIsThere()
    {
        var expected = _folder.WriteFile("run.log", "of an earlier run\n");

        using var log = RunLogFile.TryOpen("run.log", _folder.Path, _clock, out var path, out _);

        var opened = log.ShouldNotBeNull();
        opened.Path.ShouldBe(expected);
        path.ShouldBe(expected);
        opened.Write("e");
        opened.Dispose();
        File.ReadAllText(expected).ShouldBe(Head + "}\n");
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-such-folder/run.log")]
    [InlineData("folder")]
    public void TryOpen_PathThatCannotBeWritten_GivesNoLogAndTheReason(string given)
    {
        _ = _folder.CreateFolder("folder");

        using var log = RunLogFile.TryOpen(given, _folder.Path, _clock, out _, out var reason);

        log.ShouldBeNull();
        reason.ShouldNotBeEmpty();
    }

    private sealed class BrokenStream : MemoryStream
    {
        public int Writes { get; private set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Writes++;
            throw new IOException("disk full");
        }

        public override void Write(ReadOnlySpan<byte> buffer) => Write([], 0, 0);
    }
}
```

Run: `dotnet build SqlSource.slnx`
Expected: FAIL, the namespace `SqlSource.Tool.Logging` does not exist.

- [ ] **Step 3: Write an event's values and the log's interface**

Create `src/SqlSource.Tool/Logging/LogValue.cs`:

```csharp
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SqlSource.Tool.Logging;

/// <summary>
/// A value of an event of the log: what JSON can hold, and nothing else.  The set is closed, so that the writer
/// writes every value and <see cref="SessionRunLog" /> reaches every string.
/// </summary>
internal abstract record LogValue
{
    // Only the records below derive from it.
    private LogValue() { }

    /// <summary>The value <c>null</c>.</summary>
    public static LogValue Null { get; } = new Nothing();

    public static implicit operator LogValue(string? text) => text is null ? Null : new Text(text);

    public static implicit operator LogValue(bool flag) => new Flag(flag);

    public static implicit operator LogValue(long number) => new Number(number);

    /// <summary>A number, or <c>null</c> when there is none.</summary>
    public static LogValue Of(long? number) => number is { } value ? new Number(value) : Null;

    public static LogValue ListOf(IEnumerable<LogValue> items) => new Items(new EquatableArray<LogValue>([.. items]));

    public static LogValue ListOfTexts(IEnumerable<string> texts) =>
        ListOf(texts.Select(static text => (LogValue)text));

    /// <summary>Names and values, in the order given.</summary>
    public static LogValue MapOf(params LogMember[] members) =>
        new Members(new EquatableArray<LogMember>([.. members]));

    public sealed record Nothing : LogValue;

    public sealed record Text(string Value) : LogValue;

    public sealed record Flag(bool Value) : LogValue;

    public sealed record Number(long Value) : LogValue;

    // Each holds the repository's array that compares by its items, so two values that hold the same are equal.
    public sealed record Items(EquatableArray<LogValue> Values) : LogValue;

    public sealed record Members(EquatableArray<LogMember> Values) : LogValue;
}
```

Create `src/SqlSource.Tool/Logging/LogMember.cs`:

```csharp
namespace SqlSource.Tool.Logging;

/// <summary>
/// One member of an event or of a map.  People and phase 10's bundle read a member by its name: one is added, and
/// never renamed.
/// </summary>
internal readonly record struct LogMember(string Name, LogValue Value);
```

Create `src/SqlSource.Tool/Logging/IRunLog.cs`:

```csharp
namespace SqlSource.Tool.Logging;

/// <summary>
/// Where the events of a run go.  <see cref="RunLogFile" /> writes them to the file of <c>--log</c>, and
/// <see cref="NullRunLog" /> is what a run without one has.
/// </summary>
/// <remarks>
/// Nothing hands a log a connection's value.  A describer is given a <see cref="SessionRunLog" />, which removes
/// the value all the same.
/// </remarks>
internal interface IRunLog
{
    /// <summary>Writes one event.</summary>
    /// <param name="name">The event's name: <c>run.start</c>, or <c>postgres.</c> and a describer's own.</param>
    /// <param name="members">Its members, in the order they are written.</param>
    void Write(string name, params LogMember[] members);
}
```

Create `src/SqlSource.Tool/Logging/NullRunLog.cs`:

```csharp
namespace SqlSource.Tool.Logging;

/// <summary>The log of a run that has none.</summary>
internal sealed class NullRunLog : IRunLog
{
    private NullRunLog() { }

    public static NullRunLog Instance { get; } = new();

    public void Write(string name, params LogMember[] members)
    {
        // Nothing is kept: the run has no --log.
    }
}
```

`SessionRunLog` comes in task 6; until then the two `<see cref="SessionRunLog" />` do not resolve.  Write them as `<c>SessionRunLog</c>` in this task and turn them into references in task 6.

- [ ] **Step 4: Write the file's writer**

Create `src/SqlSource.Tool/Logging/RunLogFile.cs`:

```csharp
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SqlSource.Tool.Logging;

/// <summary>
/// The file of <c>--log</c>: JSON Lines, UTF-8 with <c>\n</c>, one object for each event, written and flushed as
/// the run goes so that a run that is stopped leaves what it did.
/// </summary>
/// <remarks>
/// <para>
/// Every object starts with <c>event</c> and <c>time</c>, the moment in UTC with milliseconds.  A string over
/// <see cref="MaxStringBytes" />, counted in UTF-8, is cut to that size between two characters, and its object
/// gains <c>"truncated": true</c>.
/// </para>
/// <para>
/// Several threads may write: an event is one write, under a lock.  A write that the system refuses does not end
/// the run: the writer keeps the <see cref="Failure" /> and writes nothing more, and <c>Cli</c> reports it when
/// the run ends.
/// </para>
/// </remarks>
internal sealed class RunLogFile : IRunLog, IDisposable
{
    /// <summary>The longest string an event holds, in bytes of UTF-8: 1 MiB.</summary>
    public const int MaxStringBytes = 1024 * 1024;

    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    // The log is for people: only what JSON must escape is escaped.
    private static readonly JsonWriterOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly object _gate = new();
    private readonly MemoryStream _line = new();
    private readonly Stream _stream;
    private readonly TimeProvider _time;
    private bool _closed;

    /// <param name="path">The full path of the file, for the error of a write that fails.</param>
    /// <param name="stream">Where the lines go.  The log closes it.</param>
    /// <param name="time">The clock of the run.</param>
    internal RunLogFile(string path, Stream stream, TimeProvider time)
    {
        Path = path;
        _stream = stream;
        _time = time;
    }

    public string Path { get; }

    /// <summary>Why the log stopped being written, or null while it is.</summary>
    public Exception? Failure { get; private set; }

    /// <summary>
    /// Makes the file, or empties the one that is there.  Null, with the system's reason, when it cannot be.
    /// </summary>
    /// <param name="given">The path as the option or the variable gave it.</param>
    /// <param name="workingDirectory">The full path a relative path is resolved against.</param>
    /// <param name="time">The clock of the run.</param>
    /// <param name="path">The full path, or <paramref name="given" /> when it is no path at all.</param>
    /// <param name="reason">Why the file could not be made.  Empty when it was.</param>
    public static RunLogFile? TryOpen(
        string given,
        string workingDirectory,
        TimeProvider time,
        out string path,
        out string reason
    )
    {
        path = given;
        reason = "";
        try
        {
            path = System.IO.Path.GetFullPath(given, workingDirectory);
            return new RunLogFile(path, new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read), time);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // An empty path, a directory, a folder that is not there, or a file the system will not open.
            reason = exception.Message;
            return null;
        }
    }

    public void Write(string name, params LogMember[] members)
    {
        lock (_gate)
        {
            if (_closed || Failure is not null)
            {
                return;
            }

            try
            {
                _line.SetLength(0);
                using (var json = new Utf8JsonWriter(_line, Options))
                {
                    var cut = false;
                    json.WriteStartObject();
                    json.WriteString("event", Fit(name, ref cut));
                    json.WriteString(
                        "time",
                        _time.GetUtcNow().UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture)
                    );
                    foreach (var member in members)
                    {
                        json.WritePropertyName(Fit(member.Name, ref cut));
                        WriteValue(json, member.Value, ref cut);
                    }

                    if (cut)
                    {
                        json.WriteBoolean("truncated", true);
                    }

                    json.WriteEndObject();
                }

                _line.WriteByte((byte)'\n');
                _line.WriteTo(_stream);
                _stream.Flush();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Failure = exception;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            try
            {
                _stream.Dispose();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // What was buffered could not be written.
                Failure ??= exception;
            }

            _line.Dispose();
        }
    }

    private static void WriteValue(Utf8JsonWriter json, LogValue value, ref bool cut)
    {
        switch (value)
        {
            case LogValue.Text text:
                json.WriteStringValue(Fit(text.Value, ref cut));
                break;
            case LogValue.Flag flag:
                json.WriteBooleanValue(flag.Value);
                break;
            case LogValue.Number number:
                json.WriteNumberValue(number.Value);
                break;
            case LogValue.Items items:
                json.WriteStartArray();
                foreach (var item in items.Values)
                {
                    WriteValue(json, item, ref cut);
                }

                json.WriteEndArray();
                break;
            case LogValue.Members members:
                json.WriteStartObject();
                foreach (var member in members.Values)
                {
                    json.WritePropertyName(Fit(member.Name, ref cut));
                    WriteValue(json, member.Value, ref cut);
                }

                json.WriteEndObject();
                break;
            default:
                json.WriteNullValue();
                break;
        }
    }

    // A string as the file holds it: at most MaxStringBytes of UTF-8, cut between two characters, and with half a
    // character, which JSON cannot hold, written as the replacement character.
    private static string Fit(string text, ref bool cut)
    {
        // A char is at most three bytes of UTF-8, so a short text with no half characters is written as it is.
        if (text.Length <= MaxStringBytes / 3 && text.AsSpan().IndexOfAnyInRange('\uD800', '\uDFFF') < 0)
        {
            return text;
        }

        var kept = new StringBuilder(Math.Min(text.Length, MaxStringBytes));
        var bytes = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > MaxStringBytes)
            {
                cut = true;
                break;
            }

            bytes += rune.Utf8SequenceLength;
            _ = kept.Append(rune.ToString());
        }

        return kept.ToString();
    }
}
```

Create `src/SqlSource.Tool/Logging/RunTrace.cs`:

```csharp
using System;

namespace SqlSource.Tool.Logging;

/// <summary>
/// What a run says of itself beside its errors and its summary.  <c>Reporter</c> carries it, so every step that
/// can report can say what it did.
/// </summary>
/// <param name="log">The log of the run.</param>
internal sealed class RunTrace(IRunLog log)
{
    public IRunLog Log => log;

    /// <summary>
    /// The counts of each database, as <c>run.end</c> holds them.  <c>describe</c> sets it when it has them, and
    /// <c>Cli</c>, which knows the exit code, writes the event.
    /// </summary>
    public LogValue Databases { get; set; } = LogValue.ListOf(Array.Empty<LogValue>());
}
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.RunLogFileTests`
Expected: PASS, 13 tests.

- [ ] **Step 5: Write the failing tests of a run with a log**

Create `tests/SqlSource.Tool.Tests/RunLogTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tool.Tests;

// The log of a run: "--log" or SQLSOURCE_LOG, one JSON object on a line for each thing the run did.
public sealed class RunLogTests : IDisposable
{
    private const string Users = "-- name: GetUser\nSELECT 1;\n\n-- name: ListUsers\nSELECT 2;\n";

    private readonly DescribeScene _scene = new();
    private readonly string _users;

    public RunLogTests()
    {
        _users = _scene.Project.AddSql("Users.sql", Users);
        // Each describe takes 25 ms on the clock of the test.
        _scene.Describer.BeforeDescribe = _ =>
        {
            _scene.Clock.Advance(TimeSpan.FromMilliseconds(25));
            return Task.CompletedTask;
        };
    }

    private string Log => _scene.Run.Folder.PathOf("run.log");

    public void Dispose() => _scene.Dispose();

    private static string?[] Strings(JsonElement list) => [.. list.EnumerateArray().Select(item => item.GetString())];

    [Fact]
    public async Task Run_WithLog_StartsWithWhatRanAndEndsWithHowItEnded()
    {
        var result = await _scene.DescribeAsync("--log", "run.log");

        // The console is as without the option.
        result.ShouldBe(new CliResult(0, "postgres (postgres): 2 described, 0 skipped, 0 failed\n", ""));
        var events = LogReader.Read(Log);
        var start = events[0];
        start.GetProperty("event").GetString().ShouldBe("run.start");
        start.GetProperty("time").GetString().ShouldBe("2026-10-09T14:02:11.412Z");
        start.GetProperty("toolVersion").GetString().ShouldBe(PackageVersion.Prefix);
        start.GetProperty("formatVersion").GetInt32().ShouldBe(SidecarFormat.Version);
        start.GetProperty("runtime").GetString().ShouldBe(RuntimeInformation.FrameworkDescription);
        start.GetProperty("os").GetString().ShouldBe(RuntimeInformation.OSDescription);
        Strings(start.GetProperty("arguments")).ShouldBe(["describe", "--log", "run.log"]);
        var end = events[^1];
        end.GetProperty("event").GetString().ShouldBe("run.end");
        end.GetProperty("time").GetString().ShouldBe("2026-10-09T14:02:11.462Z");
        end.GetProperty("exitCode").GetInt32().ShouldBe(0);
        end.GetProperty("durationMs").GetInt64().ShouldBe(50);
    }

    [Fact]
    public async Task Run_ErrorWithLog_IsADiagnosticEventWithItsPlaceItsMessageAndItsLines()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser", "relation \"users\" does not exist");

        var result = await _scene.DescribeAsync("--log", "run.log");

        result.ExitCode.ShouldBe(1);
        var events = LogReader.Read(Log);
        var diagnostics = events.Where(line => line.GetProperty("event").GetString() == "diagnostic").ToArray();
        diagnostics.Select(line => line.GetProperty("id").GetString()).ShouldBe(["SQLSRC999", "SQLSRC217"]);
        var rejected = diagnostics[0];
        rejected.GetProperty("path").GetString().ShouldBe(_users);
        rejected.GetProperty("line").GetInt32().ShouldBe(1);
        rejected.GetProperty("column").GetInt32().ShouldBe(10);
        rejected.GetProperty("message").GetString().ShouldBe("The server rejected the query 'GetUser'");
        var lines = rejected.GetProperty("lines").EnumerateArray().ToArray();
        lines.Select(line => line.GetProperty("label").GetString()).ShouldBe(["query", "step", "server", "help"]);
        lines[2].GetProperty("text").GetString().ShouldBe("relation \"users\" does not exist");
        // An error at a file and at no place in it.
        diagnostics[1].GetProperty("line").ValueKind.ShouldBe(JsonValueKind.Null);
        diagnostics[1].GetProperty("column").ValueKind.ShouldBe(JsonValueKind.Null);
        events[^1].GetProperty("exitCode").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Run_ExitCodeTwo_IsTheExitCodeOfRunEnd()
    {
        var result = await _scene.DescribeAsync("--check", "--log", "run.log");

        result.ExitCode.ShouldBe(2);
        LogReader.Read(Log)[^1].GetProperty("exitCode").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Run_LogOptionAndVariable_WritesToTheOptionsFileAlone()
    {
        _scene.Run.Environment["SQLSOURCE_LOG"] = "variable.log";

        _ = await _scene.DescribeAsync("--log", "run.log");

        File.Exists(Log).ShouldBeTrue();
        File.Exists(_scene.Run.Folder.PathOf("variable.log")).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_LogVariableAlone_WritesToItsFile()
    {
        _scene.Run.Environment["SQLSOURCE_LOG"] = "run.log";

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(0);
        LogReader.Names(Log)[0].ShouldBe("run.start");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Run_NoLogOptionAndNoVariable_WritesNoFile(string? variable)
    {
        if (variable is not null)
        {
            _scene.Run.Environment["SQLSOURCE_LOG"] = variable;
        }

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(0);
        Directory.GetFiles(_scene.Run.Folder.Path, "*.log").ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_SecondRunWithTheSameLog_EmptiesTheFile()
    {
        _ = await _scene.DescribeAsync("--log", "run.log");

        _ = await _scene.DescribeAsync("--log", "run.log");

        LogReader.Names(Log).Count(name => name == "run.start").ShouldBe(1);
    }

    // "tmp" is the folder of the run's temporary files, so it is a directory.
    [Theory]
    [InlineData("tmp")]
    [InlineData("no-such-folder/run.log")]
    public async Task Run_LogThatCannotBeWritten_IsSqlsrc218AndTheRunDoesNotStart(string given)
    {
        var path = _scene.Run.Folder.PathOf(given);

        var result = await _scene.DescribeAsync("--log", given);

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith($"{path} : error SQLSRC218: '{path}' could not be written: ");
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_DescriberThatThrowsWithLog_IsADiagnosticOfSqlsrc200AndRunEndSaysOne()
    {
        _scene.Describer.Exceptions["GetUser"] = new InvalidOperationException("boom");

        var result = await _scene.DescribeAsync("--log", "run.log");

        result.ExitCode.ShouldBe(1);
        var events = LogReader.Read(Log);
        events[^2].GetProperty("id").GetString().ShouldBe("SQLSRC200");
        events[^1].GetProperty("event").GetString().ShouldBe("run.end");
        events[^1].GetProperty("exitCode").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Run_CancelledWithLog_EndsWithRunEndAndOne()
    {
        using var cancel = new CancellationTokenSource();
        _scene.Describer.BeforeDescribe = _ => cancel.CancelAsync();

        var result = await _scene.Run.RunAsync(cancel.Token, "describe", "--log", "run.log");

        result.ExitCode.ShouldBe(1);
        var end = LogReader.Read(Log)[^1];
        end.GetProperty("event").GetString().ShouldBe("run.end");
        end.GetProperty("exitCode").GetInt32().ShouldBe(1);
    }

    // A command line that was wrong is not logged at all: the run ended before the log was opened.
    [Theory]
    [InlineData("describe", "--log", "run.log", "--conection", "x")]
    [InlineData("describe", "--log", "run.log", "--database", "not a name")]
    public async Task Run_WrongCommandLineWithLog_WritesNoFile(params string[] args)
    {
        var result = await _scene.Run.RunAsync(args);

        result.ExitCode.ShouldBe(1);
        File.Exists(Log).ShouldBeFalse();
    }

    // A user's log of the last run must survive a look at the help.
    [Theory]
    [InlineData("describe", "--help")]
    [InlineData("--version")]
    [InlineData("--help")]
    public async Task Run_NoDescribeWithTheLogVariable_LeavesTheFileAsItWas(params string[] args)
    {
        _scene.Run.Environment["SQLSOURCE_LOG"] = "run.log";
        await File.WriteAllTextAsync(Log, "of the last run\n", TestContext.Current.CancellationToken);

        var result = await _scene.Run.RunAsync(args);

        result.ExitCode.ShouldBe(0);
        (await File.ReadAllTextAsync(Log, TestContext.Current.CancellationToken)).ShouldBe("of the last run\n");
    }

    [Fact]
    public async Task Run_LogWithAnEmptyValue_IsAWrongCommandLine()
    {
        var result = await _scene.DescribeAsync("--log", "");

        result.ShouldBe(
            new CliResult(1, "", "sqlsource: the value of option '--log' at position 3 is not a path\n")
        );
    }

    [Fact]
    public async Task Run_LogGivenTwice_IsAWrongCommandLine()
    {
        var result = await _scene.DescribeAsync("--log", "a.log", "--log=b.log");

        result.ShouldBe(new CliResult(1, "", "sqlsource: option '--log' is given more than once\n"));
        Directory.GetFiles(_scene.Run.Folder.Path, "*.log").ShouldBeEmpty();
    }
}
```

Create `tests/SqlSource.Tool.Tests/RedactionTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

// No value of a connection reaches the log: not from the command line, and not from a describer.
public sealed class RedactionTests : IDisposable
{
    private const string Marker = "s3cret-marker";

    private const string Value = "Host=db;Password=" + Marker;

    private readonly DescribeScene _scene = new();

    public RedactionTests()
    {
        _scene.Run.Environment.Clear();
        _ = _scene.Project.AddSql("Users.sql", "-- name: GetUser\nSELECT 1;\n");
    }

    private string Log => _scene.Run.Folder.PathOf("run.log");

    public void Dispose() => _scene.Dispose();

    // Whichever way the option was written, the token keeps the option and the name and loses the value.
    [Theory]
    [InlineData("--connection|postgres=***", "--connection", "postgres=" + Value)]
    [InlineData("--connection=postgres=***", "--connection=postgres=" + Value)]
    [InlineData("--connection:postgres=***", "--connection:postgres=" + Value)]
    public async Task Run_ConnectionOnTheCommandLine_IsLoggedWithItsNameAndNoValue(string logged, params string[] args)
    {
        var result = await _scene.DescribeAsync(["--log", "run.log", .. args]);

        result.ExitCode.ShouldBe(0);
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe(Value);
        LogReader
            .Read(Log)[0]
            .GetProperty("arguments")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ShouldBe(["describe", "--log", "run.log", .. logged.Split('|')]);
        (await File.ReadAllTextAsync(Log, TestContext.Current.CancellationToken)).ShouldNotContain(Marker);
    }

    // The value was typed where "name=value" stands and the shell split it: the line is wrong, and nothing is logged.
    [Fact]
    public async Task Run_WrongCommandLineThatHoldsAValue_WritesNoLog()
    {
        var result = await _scene.DescribeAsync("--log", "run.log", "--connection", "postgres=Host=db;", "Password=" + Marker);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldNotContain(Marker);
        File.Exists(Log).ShouldBeFalse();
    }
}
```

In `tests/SqlSource.Tool.Tests/UsageCheckTests.cs`:

- In the vocabulary of `Check_EveryAcceptedCommandLineOfTheTool_IsReadTheSameBySystemCommandLine`, add `"--log",` and `"--log=L",` after `"--check",`.
- Add:

```csharp
    // System.CommandLine rejects a second value for an option that takes one, with a message that repeats it.
    [Theory]
    [InlineData("--connection", "describe", "--connection", "a=b", "--connection=" + Secret)]
    [InlineData("-c", "describe", "-c", "a=b", "App.csproj", "-c:" + Secret)]
    public void Check_OptionThatTakesOneValueGivenTwice_IsNamedAsItWasTypedAndItsValueIsNot(
        string name,
        params string[] args
    ) => UsageCheck.Check(Root(), args).Messages.ShouldBe([$"sqlsource: option '{name}' is given more than once"]);
```

`Root()` of that class declares `--connection` with the alias `-c` and `ArgumentArity.ExactlyOne`.

In `tests/SqlSource.Tool.Tests/DescribeTests.cs`, in `Run_Help_ShowsTheProjectOption`, add `result.Out.ShouldContain("--log <path>");`.

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.RunLogTests`
Expected: FAIL.  Every run ends with `sqlsource: unknown option '--log'`.

- [ ] **Step 6: Reject a second value for an option that takes one**

In `src/SqlSource.Tool/UsageCheck.cs`, in `Check`, declare beside `values`:

```csharp
        // An option that takes one value takes it once: System.CommandLine rejects a second with the value's text.
        var givenOnce = new HashSet<Option>();
```

and replace each of the two `values.Add(new UsageValue(option, ...));` for an option's value with a call of a local function that adds the value or the message:

```csharp
        void AddValue(Option option, string name, string text, int position)
        {
            if (option.Arity.MaximumNumberOfValues == 1 && !givenOnce.Add(option))
            {
                messages.Add($"sqlsource: option '{name}' is given more than once");
                return;
            }

            values.Add(new UsageValue(option, text, position));
        }
```

The two calls are `AddValue(option, name, token[(name.Length + 1)..], index);` and `AddValue(option, name, args[index], index + 1);`, the second still followed by `index++;`.  Run `UsageCheckTests`: a test of that class whose line gives `--connection` twice now has this message too, and its expectation gains it.

- [ ] **Step 7: Give `describe` the option, and say when a run has a log**

In `src/SqlSource.Tool/DescribeCommand.cs`:

```csharp
    internal const string LogOption = "--log";

    /// <summary>The environment variable that gives the log's path when <c>--log</c> does not.</summary>
    internal const string LogVariable = "SQLSOURCE_LOG";
```

In `Create`, after `check`, and add `log,` to the command's initializer:

```csharp
        var log = new Option<string>(LogOption)
        {
            Description =
                "Write what the run did to this file, one JSON object on a line.  The file is overwritten.  "
                + $"{LogVariable} gives the path when the option does not.",
            HelpName = "path",
            Arity = ArgumentArity.ExactlyOne,
        };
```

In `CheckUsage`, add a branch to the chain, before the one for `ConnectionOption`:

```csharp
            else if (value.Option.Name == LogOption && string.IsNullOrWhiteSpace(value.Text))
            {
                // What --log "$UNSET" gives.
                messages.Add($"sqlsource: the value of option '{LogOption}' at position {value.Position} is not a path");
            }
```

Add the two members that `Cli` asks.  `ParseResult.Action` is the action that an invocation would run: the help's, when `--help` is on the line.  If System.CommandLine 2.0.12 names it otherwise, keep what `Runs` means and the test `Run_NoDescribeWithTheLogVariable_LeavesTheFileAsItWas` holds it.

```csharp
    /// <summary>
    /// Whether the command line runs <c>describe</c>: the command is named, and it is its own action that runs
    /// and not the help's.
    /// </summary>
    public static bool Runs(ParseResult parsed) =>
        parsed.CommandResult.Command.Name == Name
        && ReferenceEquals(parsed.Action, parsed.CommandResult.Command.Action);

    /// <summary>
    /// The path of the run's log as it was given, by <c>--log</c> or else by <c>SQLSOURCE_LOG</c>, or null when
    /// the run has none.  A command line that does not run <c>describe</c> has none: the help must not empty the
    /// log of the last run.  A variable that is empty or white space is not set.
    /// </summary>
    public static string? LogPathOf(ParseResult parsed, Func<string, string?> getEnvironmentVariable)
    {
        if (!Runs(parsed))
        {
            return null;
        }

        if (parsed.GetValue<string>(LogOption) is { } option)
        {
            return option;
        }

        var variable = getEnvironmentVariable(LogVariable);
        return string.IsNullOrWhiteSpace(variable) ? null : variable;
    }
```

- [ ] **Step 8: Write the command line as the log holds it**

Create `src/SqlSource.Tool/Logging/LoggedArguments.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using SqlSource.Tool.Describing;

namespace SqlSource.Tool.Logging;

/// <summary>
/// The command line as <c>run.start</c> holds it: every token, with the value of each <c>--connection</c>
/// replaced.
/// </summary>
/// <remarks>
/// It is built from what <see cref="UsageCheck" /> read and not from the text: a token is replaced because it was
/// bound to the option, whichever way the option was written, <c>--connection x</c>, <c>--connection=x</c> or
/// <c>--connection:x</c>.  A command line that was wrong never comes here: the run ended before the log was opened.
/// </remarks>
internal static class LoggedArguments
{
    public static LogValue Of(Usage usage, IReadOnlyList<string> args)
    {
        var logged = args.ToArray();
        foreach (
            var value in usage.Values.Where(static value => value.Option?.Name == DescribeCommand.ConnectionOption)
        )
        {
            // The token is the value, or the option, a separator and the value.
            var token = args[value.Position - 1];
            logged[value.Position - 1] = token[..(token.Length - value.Text.Length)] + WithoutItsValue(value.Text);
        }

        return LogValue.ListOfTexts(logged);
    }

    // The name stays, so that a reader sees which database was given a connection.
    private static string WithoutItsValue(string text) =>
        ConnectionArgument.TryParse(text, out var connection)
            ? connection.Name + "=" + Redaction.Mark
            : Redaction.Mark;
}
```

- [ ] **Step 9: Have the reporter write each error to the log**

In `src/SqlSource.Tool/Reporting/Reporter.cs`, add `using SqlSource.Tool.Logging;` and:

```csharp
    /// <summary>
    /// What the run says of itself beside its errors.  <c>Cli</c> sets it once, before any command runs, when the
    /// run has a log; every error is also an event of that log.
    /// </summary>
    public RunTrace Trace { get; set; } = new(NullRunLog.Instance);
```

Split `Format` so that the message is made once, and write the event inside the lock, after the console's write, so that the two keep one order:

```csharp
    public void Report(ToolDiagnostic diagnostic)
    {
        var message = MessageOf(diagnostic);
        var text = Format(diagnostic, message, error.NewLine);
        lock (_gate)
        {
            error.Write(text);
            _count++;
            if (ReferenceEquals(diagnostic.Descriptor, ToolDiagnostics.SidecarOutOfDate))
            {
                _differences++;
            }

            Trace.Log.Write(
                "diagnostic",
                new("id", diagnostic.Descriptor.Id),
                new("path", diagnostic.Path),
                new("line", LogValue.Of(diagnostic.Position?.Line + 1)),
                new("column", LogValue.Of(diagnostic.Position?.Character + 1)),
                new("message", message),
                new(
                    "lines",
                    LogValue.ListOf(
                        diagnostic.Lines.Select(static line =>
                            LogValue.MapOf(new("label", OneLine.Of(line.Label)), new("text", OneLine.Of(line.Text)))
                        )
                    )
                )
            );
        }
    }

    private static string MessageOf(ToolDiagnostic diagnostic)
    {
        object?[] arguments = [.. diagnostic.Arguments.Select(OneLine.Of)];
        return string.Format(
            CultureInfo.InvariantCulture,
            diagnostic.Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
            arguments
        );
    }
```

`Format(ToolDiagnostic diagnostic, string message, string newLine)` is the method that is there without the lines that make the message.

- [ ] **Step 10: Open the log in `Cli.RunAsync`, and end it there**

In `src/SqlSource.Tool/Cli.cs`, add `using System.Runtime.InteropServices;`, `using SqlSource.Diagnostics;`, `using SqlSource.Snapshot;` and `using SqlSource.Tool.Logging;`, and replace `RunAsync`:

```csharp
    /// <summary>
    /// Runs the command with a host that the caller gives.  The tests call this.
    /// </summary>
    /// <remarks>
    /// The log of the run is opened and closed here, though <c>--log</c> is an option of <c>describe</c>: the error
    /// of an exception is reported here and the exit code is decided here, and both belong in the log.
    /// </remarks>
    internal static async Task<int> RunAsync(string[] args, ToolHost host, CancellationToken cancellationToken)
    {
        var reporter = new Reporter(host.Error);
        var started = host.Time.GetTimestamp();
        RunLogFile? log = null;
        var exitCode = 0;
        try
        {
            // A run that was cancelled prints nothing more, whatever it was asked to do.
            cancellationToken.ThrowIfCancellationRequested();

            var root = BuildCommands(host, reporter);

            var usage = UsageCheck.Check(root, args);
            var messages = usage.Messages.Count > 0 ? usage.Messages : DescribeCommand.CheckUsage(usage);
            if (messages.Count > 0)
            {
                // These lines have no id and do not go through the reporter.
                foreach (var line in messages)
                {
                    await host.Error.WriteLineAsync(line);
                }

                return 1;
            }

            var parsed = root.Parse(args, Parser);
            if (parsed.Errors.Count > 0 || !usage.IsReadTheSameBy(parsed))
            {
                // UsageCheck lets nothing through that System.CommandLine rejects or reads another way, as far as
                // the tests know.  If something is, nothing runs, and System.CommandLine's message is not written:
                // it may repeat a token.
                await host.Error.WriteLineAsync("sqlsource: the command line is not valid");
                return 1;
            }

            // After the command line was read, so that a wrong one is never logged, and before anything runs.
            if (DescribeCommand.LogPathOf(parsed, host.GetEnvironmentVariable) is { } given)
            {
                log = RunLogFile.TryOpen(given, host.WorkingDirectory, host.Time, out var path, out var reason);
                if (log is null)
                {
                    // The run does not start.
                    reporter.Report(
                        ToolDiagnostic.ForFile(ToolDiagnostics.FileCannotBeChanged, path, path, "written", reason)
                    );
                    return 1;
                }

                reporter.Trace = new RunTrace(log);
                log.Write(
                    "run.start",
                    new("toolVersion", PackageVersion.Prefix),
                    new("formatVersion", SidecarFormat.Version),
                    new("runtime", RuntimeInformation.FrameworkDescription),
                    new("os", RuntimeInformation.OSDescription),
                    new("arguments", LoggedArguments.Of(usage, args))
                );
            }

            var invocation = new InvocationConfiguration
            {
                Output = host.Out,
                Error = host.Error,
                // An exception comes here, to the catch below.
                EnableDefaultExceptionHandler = false,
                // Cli.Run listens for Ctrl+C itself.
                ProcessTerminationTimeout = null,
            };
            exitCode = await parsed.InvokeAsync(invocation, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            exitCode = 1;
        }
        catch (DescriberFaultException fault)
        {
            // The error of an exception that a describer threw, already without the connection's value.
            reporter.Report(fault.Diagnostic);
        }
        catch (Exception exception)
        {
            reporter.Report(UnexpectedFailure.Of(exception, host.GetEnvironmentVariable));
        }

        using (log)
        {
            exitCode = ExitCodeOf(reporter, exitCode);
            return log is null ? exitCode : End(log, exitCode, started, host, reporter);
        }
    }

    // The last event, with the exit code of the process, and the file is closed.  A log that stopped being
    // writable while the run went on did not end the run: it is an error now, once, and the exit code is 1.
    private static int End(RunLogFile log, int exitCode, long started, ToolHost host, Reporter reporter)
    {
        log.Write(
            "run.end",
            new("exitCode", exitCode),
            new("durationMs", (long)host.Time.GetElapsedTime(started).TotalMilliseconds),
            new("databases", reporter.Trace.Databases)
        );
        log.Dispose();
        if (log.Failure is not { } failure)
        {
            return exitCode;
        }

        reporter.Report(
            ToolDiagnostic.ForFile(ToolDiagnostics.FileCannotBeChanged, log.Path, log.Path, "written", failure.Message)
        );
        return 1;
    }
```

A file that `RunLogFile.TryOpen` refused for a path that is no path keeps `path` as it was given; `OneLine` in the reporter keeps it on one line.

- [ ] **Step 11: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.RunLogTests`
Expected: PASS.

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.RedactionTests`
Expected: PASS, 4 tests.

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.

- [ ] **Step 12: Hold the writer's failure to a test of the whole run**

`RunLogFileTests` holds the writer.  That `Cli` goes on and reports the failure once is held here, where the system has a file that fails every write.  Add to `tests/SqlSource.Tool.Tests/RunLogTests.cs`:

```csharp
    // "/dev/full" takes no byte: every write fails as on a full disk.  Linux has it; the build server runs there.
    [Fact]
    public async Task Run_LogThatStopsBeingWritable_FinishesTheRunAndIsOneSqlsrc218()
    {
        Assert.SkipUnless(File.Exists("/dev/full"), "The system has no device that is always full.");

        var result = await _scene.DescribeAsync("--log", "/dev/full");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBe("postgres (postgres): 2 described, 0 skipped, 0 failed\n");
        result.Error.ShouldStartWith("/dev/full : error SQLSRC218: '/dev/full' could not be written: ");
        result.Error.Split("error SQLSRC").Length.ShouldBe(2);
        File.Exists(_users + ".json").ShouldBeTrue();
    }
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.RunLogTests`
Expected: PASS; on macOS and Windows this one test is skipped, and `RunLogFileTests.Write_StreamThatFails_DoesNotThrowAndKeepsTheFailure` holds the writer everywhere.

- [ ] **Step 13: Bring the document this task makes wrong**

In `src/SqlSource.Tool/README.md`, add `$ dotnet sqlsource describe --log sqlsource.log` as the last line of the first `console` block, and before the paragraph that starts `` `describe` takes a `.sln` `` add:

```markdown
`--log <path>` writes what the run did to a file, one JSON object on a line: what ran and on what, each project, file, database and query with what the run decided for it, each error, and how the run ended.  `SQLSOURCE_LOG` gives the path when the option does not.  The file is overwritten by each run, and the console stays as it is without the option.  A log holds no connection string: the value of each `--connection` is written as `***` with its name, and so is a value that a database's driver repeats in a message.  It does hold the paths of your files and the names of your queries and databases, so read it before you attach it to an issue.
```

The sentence on what a log holds is true once task 6 has added the events; this task and that one are in one pull request.

- [ ] **Step 14: Format, validate, commit**

Run `./format.sh`, then `./pre-commit-validation.sh` as its own command.

```bash
git add src tests
git commit -m "Write a run's log: --log and SQLSOURCE_LOG, with the run's start, its end and its errors

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: The other events, a describer's log, and `SessionRunLog`

**Files:**
- Create: `src/SqlSource.Tool/Logging/SessionRunLog.cs`, `src/SqlSource.Tool/Logging/RunEvents.cs`, `src/SqlSource.Tool/Planning/FileClaim.cs`
- Modify: `src/SqlSource.Tool/Logging/RunTrace.cs`, `IRunLog.cs`, `LogValue.cs` (the two `<c>SessionRunLog</c>` become references)
- Modify: `src/SqlSource.Tool/Describing/IQueryDescriber.cs`, `DatabaseRuns.cs`, `DescribeRun.cs`, `SidecarStore.cs`, `RunSummary.cs`
- Modify: `src/SqlSource.Tool/Planning/PlannedFile.cs`, `RunPlanner.cs`
- Modify: `src/SqlSource.Tool/Projects/RunProjects.cs`, `src/SqlSource.Tool/DescribeCommand.cs`
- Modify: `src/SqlSource.Tool/AGENTS.md`, `docs/tech-debt/TD-0034-redaction-of-a-describers-text-has-holes.md`
- Create: `tests/SqlSource.Tool.Tests/RecordingLog.cs`, `SessionRunLogTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/FakeDescriber.cs`, `DescriberTests.cs`, `RunLogTests.cs`, `RedactionTests.cs`, `SecretTests.cs`

**Interfaces:**
- Consumes: everything task 5 produces; `Redaction.Of(string text, string secret)`; `ServerInfo(Version, Driver, DriverVersion, Settings)`; `ServerSetting(Name, Value)`; `DatabaseConnection.Source`; `TypeClaim(SourcePath, Path, Output, Position)`.
- Produces:
  - `IQueryDescriber.OpenAsync(OpenRequest request, IRunLog log, CancellationToken cancellationToken)`.
  - `SessionRunLog(IRunLog inner, string secret, string? prefix = null) : IRunLog`.
  - `RunTrace.Without(string secret)`: the same trace with one connection's value removed from all it says.
  - `FileClaim(string ProjectPath, string SourcePath, LinePosition Position)`, and `PlannedFile.Claims`, an `EquatableArray<FileClaim>`, the record's last parameter.
  - `RunEvents`, static, each taking the `RunTrace` first: `Project(trace, string path, ProjectEvaluation? evaluation)`, `File(trace, PlannedFile file)`, `Database(trace, PlannedDatabase database, DatabaseConnection connection, bool hasDescriber)`, `Opened(trace, PlannedDatabase database, ServerInfo server)`, `NotOpened(trace, PlannedDatabase database, string error)`, `Query(trace, QueryWork query, long? durationMs = null, string? error = null)`, `Sidecar(trace, string path, string action)`.  Task 7 adds the lines of `--verbose` to the same functions.
  - `RunSummary.Counts(ImmutableArray<PlannedDatabase> selected, ImmutableArray<FileWork> files, bool check)` returning the `LogValue` of `run.end`'s `databases`.
  - In the tests: `FakeDescriber.OnOpen`, an `Action<IRunLog, OpenRequest>?`, and `FakeDescriber.OnDescribe`, an `Action<IRunLog, DescribeRequest>?`; `RecordingLog : IRunLog` with `Events`, a list of `(string Name, LogMember[] Members)`.

The order of a run's events, which the tests hold: `run.start`; a `project` for each project the run leaves out, then one for each it asked MSBuild about, each before its own error; a `file` for each claimed file; a `query` for each query the run decided without a database, skipped, left out or failed by the plan; then for each selected database a `database`, the describer's own events, `database.open`, and a `query` as each describe ends, after that query's error; a `sidecar` for each file the run looked at; `run.end`.

- [ ] **Step 1: Write the failing tests of the log that removes a value**

Create `tests/SqlSource.Tool.Tests/RecordingLog.cs`:

```csharp
using System.Collections.Generic;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Tests;

// A log that keeps what it was given.
internal sealed class RecordingLog : IRunLog
{
    public List<(string Name, LogMember[] Members)> Events { get; } = [];

    public void Write(string name, params LogMember[] members) => Events.Add((name, members));
}
```

Create `tests/SqlSource.Tool.Tests/SessionRunLogTests.cs`:

```csharp
using Shouldly;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests;

// The log a describer is given: a connection's value is removed from every string of an event, at any depth.
public sealed class SessionRunLogTests
{
    private const string Secret = "Host=db;Password=hunter2";

    private readonly RecordingLog _inner = new();

    [Fact]
    public void Write_ValueInEveryPlaceAStringCanStand_IsWrittenAsTheMarkInEach()
    {
        var log = new SessionRunLog(_inner, Secret);

        log.Write(
            "open " + Secret,
            new(Secret, Secret),
            new("inside", "cannot connect with '" + Secret + "' today"),
            new("list", LogValue.ListOf([Secret, LogValue.ListOfTexts([Secret])])),
            new("map", LogValue.MapOf(new(Secret + " key", LogValue.MapOf(new("deep", Secret)))))
        );

        var (name, members) = _inner.Events.ShouldHaveSingleItem();
        name.ShouldBe("open ***");
        members.ShouldBe([
            new("***", "***"),
            new("inside", "cannot connect with '***' today"),
            new("list", LogValue.ListOf(["***", LogValue.ListOfTexts(["***"])])),
            new("map", LogValue.MapOf(new("*** key", LogValue.MapOf(new("deep", "***"))))),
        ]);
    }

    [Fact]
    public void Write_ValuesThatAreNoStrings_AreAsTheyWere()
    {
        var log = new SessionRunLog(_inner, Secret);

        log.Write("e", new("number", 5), new("flag", true), new("nothing", LogValue.Null));

        _inner.Events[0].Members.ShouldBe([new("number", 5), new("flag", true), new("nothing", LogValue.Null)]);
    }

    // A describer gives the rest of the name, and cannot write an event of the run's.
    [Fact]
    public void Write_WithAPrefix_PutsItBeforeTheNameAfterTheValueWasRemoved()
    {
        var log = new SessionRunLog(_inner, "postgres.", "postgres.");

        log.Write("run.end postgres.");

        _inner.Events[0].Name.ShouldBe("postgres.run.end ***");
    }
}
```

Run: `dotnet build SqlSource.slnx`
Expected: FAIL, `SessionRunLog` does not exist.

- [ ] **Step 2: Write `SessionRunLog`, and let a trace remove a value**

Create `src/SqlSource.Tool/Logging/SessionRunLog.cs`:

```csharp
using System.Linq;
using SqlSource.Tool.Describing;

namespace SqlSource.Tool.Logging;

/// <summary>
/// A log over the run's that writes every occurrence of one database's connection value as <c>***</c>: in the
/// event's name and in every string of it at any depth, the names of members included.
/// </summary>
/// <remarks>
/// <para>
/// A describer is given one, with the engine as its prefix, so that a describer that logs the value by mistake, or
/// passes on a driver's message that holds it, leaks nothing, and so that it cannot write an event of the run's.
/// The run writes its own events of a database through one too, without a prefix: they hold what a describer gave.
/// </para>
/// <para>
/// The whole value is removed and nothing less: the tool does not read a connection string, so it knows no part
/// of one.  <c>docs/tech-debt/TD-0034</c> says what that leaves.
/// </para>
/// </remarks>
/// <param name="inner">The log of the run.</param>
/// <param name="secret">The connection's value.</param>
/// <param name="prefix">What stands before each event's name: the engine and a dot, for a describer.</param>
internal sealed class SessionRunLog(IRunLog inner, string secret, string? prefix = null) : IRunLog
{
    public void Write(string name, params LogMember[] members) =>
        inner.Write(prefix + Redaction.Of(name, secret), [.. members.Select(Clean)]);

    private LogMember Clean(LogMember member) => new(Redaction.Of(member.Name, secret), Clean(member.Value));

    private LogValue Clean(LogValue value) =>
        value switch
        {
            LogValue.Text text => Redaction.Of(text.Value, secret),
            LogValue.Items items => LogValue.ListOf(items.Values.Select(Clean)),
            LogValue.Members map => LogValue.MapOf([.. map.Values.Select(Clean)]),
            _ => value,
        };
}
```

In `src/SqlSource.Tool/Logging/RunTrace.cs`, add:

```csharp
    /// <summary>
    /// The same trace with one connection's value removed from everything said through it.  What the run says of
    /// a database once it has the connection goes through one: a server's version, a driver's name and a setting
    /// are a describer's text, as a failure's lines are.
    /// </summary>
    public RunTrace Without(string secret) => new(new SessionRunLog(log, secret));
```

Turn the `<c>SessionRunLog</c>` of `IRunLog.cs` and `LogValue.cs` into `<see cref="SessionRunLog" />`.

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.SessionRunLogTests`
Expected: PASS, 3 tests.

- [ ] **Step 3: Write the failing tests of the events**

In `tests/SqlSource.Tool.Tests/FakeDescriber.cs`, add `using SqlSource.Tool.Logging;`, the two properties, the parameter of `OpenAsync`, and the calls.  The session keeps the log it was opened with.

```csharp
    // Run with the log a describer is given: when a session is asked for, and before each query is described.
    public Action<IRunLog, OpenRequest>? OnOpen { get; set; }

    public Action<IRunLog, DescribeRequest>? OnDescribe { get; set; }
```

```csharp
    public Task<OpenResult> OpenAsync(OpenRequest request, IRunLog log, CancellationToken cancellationToken) =>
```

In the function it hands the exchange, after `Opened.Add(request);`, add `OnOpen?.Invoke(log, request);`, and make the session with `new Session(this, request.Exchange, log)`.  `Session` takes `IRunLog log` as its third parameter, and in `DescribeAsync`, after `owner.Described.Add(asked);`, add `owner.OnDescribe?.Invoke(log, asked);`.

In `tests/SqlSource.Tool.Tests/DescriberTests.cs`, pass `NullRunLog.Instance` as the second argument of the two calls of `OpenAsync`.

Add to `tests/SqlSource.Tool.Tests/RunLogTests.cs`, with `using SqlSource.Tool.Logging;`:

```csharp
    private static string? Text(JsonElement line, string name) => line.GetProperty(name).GetString();

    // A run that skips GetUser, describes ListUsers and fails Bad.
    private async Task<JsonElement[]> RunThatDescribesSkipsAndFailsAsync()
    {
        (await _scene.DescribeAsync()).ExitCode.ShouldBe(0);
        await File.WriteAllTextAsync(
            _users,
            Users.Replace("SELECT 2;", "SELECT 22;", StringComparison.Ordinal) + "\n-- name: Bad\nSELECT 4;\n",
            TestContext.Current.CancellationToken
        );
        _scene.Describer.Failures["Bad"] = FakeDescriber.Failure("Bad");

        (await _scene.DescribeAsync("--log", "run.log")).ExitCode.ShouldBe(1);
        return LogReader.Read(Log);
    }

    [Fact]
    public async Task Run_ThatDescribesSkipsAndFails_WritesItsEventsInTheOrderThingsHappened()
    {
        var events = await RunThatDescribesSkipsAndFailsAsync();

        events
            .Select(line => Text(line, "event"))
            .ShouldBe([
                "run.start",
                "project",
                "file",
                "query",
                "database",
                "database.open",
                "query",
                "diagnostic",
                "query",
                "diagnostic",
                "sidecar",
                "run.end",
            ]);
        // The second describe took 25 ms more on the clock than the first: every event has the time it was written.
        Text(events[6], "time").ShouldBe("2026-10-09T14:02:11.487Z");
    }

    [Fact]
    public async Task Run_ThatDescribesSkipsAndFails_SaysOfTheProjectAndTheFileWhatItRead()
    {
        var events = await RunThatDescribesSkipsAndFailsAsync();

        var project = events[1];
        Text(project, "path").ShouldBe(_scene.Project.ProjectPath);
        project.GetProperty("inRun").GetBoolean().ShouldBeTrue();
        project.GetProperty("usesSqlSource").GetBoolean().ShouldBeTrue();
        Text(project, "targetFramework").ShouldBe("net10.0");
        project.GetProperty("sqlFiles").GetInt32().ShouldBe(1);
        project.GetProperty("compileFiles").GetInt32().ShouldBe(1);
        var file = events[2];
        Text(file, "path").ShouldBe(_users);
        Text(file, "project").ShouldBe(_scene.Project.ProjectPath);
        Text(file, "dialect").ShouldBe("postgres");
        Text(file, "state").ShouldBe("ready");
        file.GetProperty("queries").GetInt32().ShouldBe(3);
        var claim = file.GetProperty("claims").EnumerateArray().ShouldHaveSingleItem();
        Text(claim, "project").ShouldBe(_scene.Project.ProjectPath);
        Text(claim, "source").ShouldBe(_scene.Project.PathOf("Queries.cs"));
        claim.GetProperty("line").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Run_ThatDescribesSkipsAndFails_SaysOfEachQueryWhatItDecided()
    {
        var events = await RunThatDescribesSkipsAndFailsAsync();

        var skipped = events[3];
        Text(skipped, "file").ShouldBe(_users);
        Text(skipped, "name").ShouldBe("GetUser");
        Text(skipped, "database").ShouldBe("postgres");
        Text(skipped, "hash").ShouldNotBeNull().Length.ShouldBe(64);
        skipped.GetProperty("selected").GetBoolean().ShouldBeTrue();
        Text(skipped, "decision").ShouldBe("skipped");
        skipped.TryGetProperty("durationMs", out _).ShouldBeFalse();
        skipped.TryGetProperty("error", out _).ShouldBeFalse();
        var described = events[6];
        Text(described, "name").ShouldBe("ListUsers");
        Text(described, "decision").ShouldBe("described");
        described.GetProperty("durationMs").GetInt64().ShouldBe(25);
        var failed = events[8];
        Text(failed, "name").ShouldBe("Bad");
        Text(failed, "decision").ShouldBe("failed");
        Text(failed, "error").ShouldBe("SQLSRC999");
        failed.TryGetProperty("durationMs", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_ThatDescribesSkipsAndFails_SaysOfTheDatabaseTheSidecarAndTheEnd()
    {
        var events = await RunThatDescribesSkipsAndFailsAsync();

        var database = events[4];
        Text(database, "name").ShouldBe("postgres");
        Text(database, "engine").ShouldBe("postgres");
        // Where the connection came from, and never what it is.
        Text(database, "connectionSource").ShouldBe("SQLSOURCE_CONNECTION_POSTGRES");
        database.GetProperty("describer").GetBoolean().ShouldBeTrue();
        var open = events[5];
        Text(open, "name").ShouldBe("postgres");
        Text(open, "serverVersion").ShouldBe("16.4");
        Text(open, "driver").ShouldBe("FakeDriver");
        Text(open, "driverVersion").ShouldBe("1.2.3");
        open.GetProperty("settings").ValueKind.ShouldBe(JsonValueKind.Object);
        var sidecar = events[10];
        Text(sidecar, "path").ShouldBe(_users + ".json");
        Text(sidecar, "action").ShouldBe("held-back");
        var counts = events[11].GetProperty("databases").EnumerateArray().ShouldHaveSingleItem();
        Text(counts, "name").ShouldBe("postgres");
        Text(counts, "engine").ShouldBe("postgres");
        counts.GetProperty("described").GetInt32().ShouldBe(1);
        counts.GetProperty("skipped").GetInt32().ShouldBe(1);
        counts.GetProperty("failed").GetInt32().ShouldBe(1);
    }

    private string? ActionOf(string sidecarPath) =>
        LogReader
            .Read(Log)
            .Where(line => Text(line, "event") == "sidecar" && Text(line, "path") == sidecarPath)
            .Select(line => Text(line, "action"))
            .ShouldHaveSingleItem();

    [Fact]
    public async Task Run_Sidecars_AreWrittenThenUnchangedThenDeleted()
    {
        _ = await _scene.DescribeAsync("--log", "run.log");
        ActionOf(_users + ".json").ShouldBe("written");

        _ = await _scene.DescribeAsync("--log", "run.log");
        ActionOf(_users + ".json").ShouldBe("unchanged");

        await File.WriteAllTextAsync(
            _users,
            "-- name: GetUser\n-- output: sql\nSELECT 1;\n",
            TestContext.Current.CancellationToken
        );
        _ = await _scene.DescribeAsync("--log", "run.log");
        ActionOf(_users + ".json").ShouldBe("deleted");
        File.Exists(_users + ".json").ShouldBeFalse();
    }

    // "written" would say what did not happen.
    [Fact]
    public async Task Run_SidecarThatCannotBeWritten_HasTheActionFailed()
    {
        _ = Directory.CreateDirectory(_users + ".json");

        var result = await _scene.DescribeAsync("--log", "run.log");

        result.Error.ShouldContain("error SQLSRC218: ");
        ActionOf(_users + ".json").ShouldBe("failed");
    }

    [Fact]
    public async Task Run_Check_SaysOfEachSidecarWhetherItIsTheSameAndCountsInItsOwnWords()
    {
        (await _scene.DescribeAsync()).ExitCode.ShouldBe(0);
        var other = _scene.Project.AddSql("Zebras.sql", "-- name: GetZebra\nSELECT 5;\n");

        var result = await _scene.DescribeAsync("--check", "--log", "run.log");

        result.ExitCode.ShouldBe(2);
        ActionOf(_users + ".json").ShouldBe("same");
        ActionOf(other + ".json").ShouldBe("differs");
        File.Exists(other + ".json").ShouldBeFalse();
        var counts = LogReader.Read(Log)[^1].GetProperty("databases").EnumerateArray().ShouldHaveSingleItem();
        counts.GetProperty("same").GetInt32().ShouldBe(2);
        counts.GetProperty("differ").GetInt32().ShouldBe(1);
        counts.GetProperty("failed").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Run_DescriberThatLogs_HasItsEventsUnderItsEngineBetweenTheRunsOwn()
    {
        _scene.Describer.OnOpen = (log, request) => log.Write("connect", new("database", request.Database));
        _scene.Describer.OnDescribe = (log, request) => log.Write("columns", new("query", request.Name));

        _ = await _scene.DescribeAsync("--log", "run.log");

        var events = LogReader.Read(Log);
        events
            .Select(line => Text(line, "event"))
            .Skip(3)
            .ShouldBe([
                "database",
                "postgres.connect",
                "database.open",
                "postgres.columns",
                "query",
                "postgres.columns",
                "query",
                "sidecar",
                "run.end",
            ]);
        Text(events[6], "query").ShouldBe("GetUser");
        Text(events[7], "name").ShouldBe("GetUser");
    }

    [Fact]
    public async Task Run_DatabaseWithoutADescriber_SaysSoAndFailsItsQueriesWithSqlsrc216()
    {
        _scene.Run.Describers.Clear();

        _ = await _scene.DescribeAsync("--log", "run.log");

        var events = LogReader.Read(Log);
        var database = events.Single(line => Text(line, "event") == "database");
        database.GetProperty("describer").GetBoolean().ShouldBeFalse();
        events.ShouldNotContain(line => Text(line, "event") == "database.open");
        events
            .Where(line => Text(line, "event") == "query")
            .Select(line => $"{Text(line, "decision")} {Text(line, "error")}")
            .ShouldBe(["failed SQLSRC216", "failed SQLSRC216"]);
    }

    [Fact]
    public async Task Run_DatabaseWithoutAConnection_HasTheSourceNoneAndFailsItsQueriesWithSqlsrc213()
    {
        _ = _scene.Run.Environment.Remove("SQLSOURCE_CONNECTION_POSTGRES");

        _ = await _scene.DescribeAsync("--log", "run.log");

        var events = LogReader.Read(Log);
        Text(events.Single(line => Text(line, "event") == "database"), "connectionSource").ShouldBe("none");
        events
            .Where(line => Text(line, "event") == "query")
            .Select(line => Text(line, "error"))
            .ShouldBe(["SQLSRC213", "SQLSRC213"]);
    }

    [Fact]
    public async Task Run_SessionThatDoesNotOpen_HasTheErrorsIdInDatabaseOpen()
    {
        _scene.Describer.OpenFailure = FakeDescriber.Failure("GetUser", "password authentication failed");

        _ = await _scene.DescribeAsync("--log", "run.log");

        var open = LogReader.Read(Log).Single(line => Text(line, "event") == "database.open");
        Text(open, "name").ShouldBe("postgres");
        Text(open, "error").ShouldBe("SQLSRC999");
        open.TryGetProperty("serverVersion", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_ProjectThatProjectOptionLeavesOut_IsInTheLogAndWasNotAsked()
    {
        var lib = Plans.Postgres(_scene.Run.Folder, "Lib", "Lib").AnsweredBy(_scene.Run.Processes);
        _ = _scene.Run.Folder.WriteFile(
            "App.slnx",
            "<Solution><Project Path=\"App.csproj\" /><Project Path=\"Lib/Lib.csproj\" /></Solution>"
        );

        _ = await _scene.DescribeAsync("App.slnx", "--project", "App.csproj", "--log", "run.log");

        var projects = LogReader.Read(Log).Where(line => Text(line, "event") == "project").ToArray();
        projects.Select(line => Text(line, "path")).ShouldBe([lib.ProjectPath, _scene.Project.ProjectPath]);
        projects[0].GetProperty("inRun").GetBoolean().ShouldBeFalse();
        projects[0].TryGetProperty("usesSqlSource", out _).ShouldBeFalse();
        projects[1].GetProperty("inRun").GetBoolean().ShouldBeTrue();
    }
```

Add to `tests/SqlSource.Tool.Tests/RedactionTests.cs`, with `using System.Text.Json;`, `using SqlSource.Tool.Describing;` and `using SqlSource.Tool.Logging;`:

```csharp
    // A value with the two characters that JSON writes another way.
    private const string Escaped = "Host=db;Password=\"s3cret\\marker\"";

    private async Task<string> LoggedWithAsync(string value)
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = value;
        _ = await _scene.DescribeAsync("--log", "run.log");
        return await File.ReadAllTextAsync(Log, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Run_DescriberThatLogsItsConnection_HasTheMarkInEveryPlaceAndTheValueNowhere()
    {
        _scene.Describer.OnOpen = (log, request) =>
            log.Write(
                "connect " + request.Connection,
                new(request.Connection, request.Connection),
                new("message", "cannot connect with '" + request.Connection + "'"),
                new("list", LogValue.ListOf([LogValue.MapOf(new("deep", request.Connection))]))
            );

        var text = await LoggedWithAsync(Value);

        text.ShouldNotContain(Marker);
        var line = LogReader.Read(Log).Single(e => e.GetProperty("event").GetString() == "postgres.connect ***");
        line.GetProperty("***").GetString().ShouldBe("***");
        line.GetProperty("message").GetString().ShouldBe("cannot connect with '***'");
        line.GetProperty("list")[0].GetProperty("deep").GetString().ShouldBe("***");
    }

    // The value is removed before the line is written, so the file holds it neither as it is nor as JSON escapes it.
    [Fact]
    public async Task Run_DescriberThatLogsAConnectionThatJsonEscapes_LeavesTheValueInNeitherForm()
    {
        _scene.Describer.OnOpen = (log, request) => log.Write("connect", new("with", "<" + request.Connection + ">"));

        var text = await LoggedWithAsync(Escaped);

        text.ShouldNotContain("s3cret");
        LogReader
            .Read(Log)
            .Single(e => e.GetProperty("event").GetString() == "postgres.connect")
            .GetProperty("with")
            .GetString()
            .ShouldBe("<***>");
    }

    // What the run itself writes of a database is a describer's text too.
    [Fact]
    public async Task Run_DescriberThatGivesItsConnectionAsItsServer_HasTheMarkInDatabaseOpen()
    {
        _scene.Describer.Server = new ServerInfo(
            Value,
            Value,
            Value,
            new EquatableArray<ServerSetting>([new ServerSetting(Value, Value)])
        );

        var text = await LoggedWithAsync(Value);

        text.ShouldNotContain(Marker);
        var open = LogReader.Read(Log).Single(e => e.GetProperty("event").GetString() == "database.open");
        open.GetProperty("serverVersion").GetString().ShouldBe("***");
        open.GetProperty("driver").GetString().ShouldBe("***");
        open.GetProperty("driverVersion").GetString().ShouldBe("***");
        open.GetProperty("settings").GetProperty("***").GetString().ShouldBe("***");
    }

    // Every text of a failure and of an exception is in a "diagnostic", and was cleaned before it was reported.
    [Fact]
    public async Task Run_DescriberThatReturnsAndThrowsItsConnection_LeavesNoValueInADiagnostic()
    {
        // After "Users.sql", so that GetUser fails before Throws ends the run.
        _ = _scene.Project.AddSql("Zebra.sql", "-- name: Throws\nSELECT 2;\n");
        _scene.Describer.Failures["GetUser"] = new DescribeFailure(
            FakeDescriber.Rejected,
            new EquatableArray<string>([Value]),
            DescribeStep.Explain,
            new EquatableArray<string>(["server said " + Value]),
            "try " + Value
        );
        _scene.Describer.Exceptions["Throws"] = new InvalidOperationException("bad " + Value);
        _scene.Run.Environment["SQLSOURCE_DEBUG"] = "1";

        var text = await LoggedWithAsync(Value);

        text.ShouldNotContain(Marker);
        LogReader
            .Read(Log)
            .Where(e => e.GetProperty("event").GetString() == "diagnostic")
            .Select(e => e.GetProperty("id").GetString())
            .ShouldBe(["SQLSRC999", "SQLSRC200"]);
    }
```

Add to `tests/SqlSource.Tool.Tests/SecretTests.cs`, beside `Run_ValueOnTheCommandLineAndEveryError_IsNeverPrinted`:

```csharp
    // The same run with a log: the file holds every error of it, and the marker word nowhere.
    [Fact]
    public async Task Run_ValueOnTheCommandLineAndEveryErrorWithLog_IsNeverLogged()
    {
        AddEveryError();

        var result = await _scene.DescribeAsync("--connection", "postgres=" + Value, "--log", "run.log");

        ShouldHold(result, "213,214,216,217,218,221,999");
        var log = await File.ReadAllTextAsync(
            _scene.Run.Folder.PathOf("run.log"),
            TestContext.Current.CancellationToken
        );
        log.ShouldNotContain(Marker);
        log.ShouldContain("\"id\":\"SQLSRC999\"");
    }
```

Run: `dotnet build SqlSource.slnx`
Expected: FAIL, `FakeDescriber.OpenAsync` no longer implements the interface.

- [ ] **Step 4: Give a describer its log**

In `src/SqlSource.Tool/Describing/IQueryDescriber.cs`, add `using SqlSource.Tool.Logging;`, and replace the member and the second paragraph of the remarks:

```csharp
/// <para>
/// Every call it makes to an engine goes through <see cref="OpenRequest.Exchange" />, so that a run can be recorded
/// and replayed.  It puts the connection's value, or a part of it, in nothing it returns and in nothing it logs.
/// The log it is given removes the whole value all the same, and puts the engine before each event's name.
/// </para>
```

```csharp
    /// <summary>Opens a session with one database.</summary>
    /// <param name="request">The database, its connection and the exchange.</param>
    /// <param name="log">
    /// Where the describer's own events go, for the session it opens too.  It is beside the request and not in
    /// it: the request holds the secret.
    /// </param>
    /// <param name="cancellationToken">Ends the run.</param>
    Task<OpenResult> OpenAsync(OpenRequest request, IRunLog log, CancellationToken cancellationToken);
```

- [ ] **Step 5: Keep who claims a file**

Create `src/SqlSource.Tool/Planning/FileClaim.cs`:

```csharp
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Tool.Planning;

/// <summary>
/// One <c>[SqlSourceGenerate]</c> that claims a file, by where it stands: the tool reads syntax and keeps no name
/// of a type.  The log and <c>--verbose</c> say it.
/// </summary>
/// <param name="ProjectPath">The full path of the project the attribute is in.</param>
/// <param name="SourcePath">The full path of the C# file that carries it.</param>
/// <param name="Position">Where the attribute starts in that file, counted from zero.</param>
internal sealed record FileClaim(string ProjectPath, string SourcePath, LinePosition Position);
```

In `src/SqlSource.Tool/Planning/PlannedFile.cs`, add the last parameter and its documentation:

```csharp
/// <param name="Claims">Every attribute that claims it, in the order of the projects and then of their claims.</param>
```

```csharp
    EquatableArray<PlannedQuery> Queries,
    EquatableArray<FileClaim> Claims
```

In `src/SqlSource.Tool/Planning/RunPlanner.cs`:

- `ClaimedFile` gains a last parameter, `List<TypeClaim> Types`.
- In `ReadProject`, beside `claimsOf`, declare `var typesOf = new Dictionary<string, List<TypeClaim>>(SqlPath.Comparer);`, and in the inner loop, after `levels.Add(level);`:

```csharp
                if (!typesOf.TryGetValue(path, out var types))
                {
                    types = [];
                    typesOf.Add(path, types);
                }

                types.Add(claim);
```

- Pass `typesOf[path]` as the last argument of `new ClaimedFile(...)`.
- Pass `ClaimsOf(work)` as the last argument of both `new PlannedFile(...)` in `PlanFile`, and add:

```csharp
    // Who claims the file, for the log: every claim of every project, in their order.
    private static EquatableArray<FileClaim> ClaimsOf(FileWork work) =>
        new([
            .. work.Claimants.SelectMany(claimant =>
                claimant.Types.Select(type => new FileClaim(claimant.ProjectPath, type.SourcePath, type.Position))
            ),
        ]);
```

Run: `dotnet build SqlSource.slnx`.  If a test builds a `PlannedFile` itself, give it `EquatableArray<FileClaim>.Empty`.

- [ ] **Step 6: Build each event in one place**

Create `src/SqlSource.Tool/Logging/RunEvents.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using SqlSource.Parsing;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Projects;

namespace SqlSource.Tool.Logging;

/// <summary>
/// The events of a run, each built in one place, so that an event has one set of members wherever it is written.
/// </summary>
/// <remarks>
/// A member is added and never renamed: people read the log, and so does phase 10's bundle.  None of these is
/// handed a connection's value.  The ones of a database after its connection is found are written through
/// <see cref="RunTrace.Without" />, since they hold what a describer gave.
/// </remarks>
internal static class RunEvents
{
    /// <summary>A project of the unit.</summary>
    /// <param name="trace">The run's.</param>
    /// <param name="path">The full path of the project file.</param>
    /// <param name="evaluation">What MSBuild said of it, or null for one that <c>--project</c> leaves out.</param>
    public static void Project(RunTrace trace, string path, ProjectEvaluation? evaluation)
    {
        var members = new List<LogMember> { new("path", path), new("inRun", evaluation is not null) };
        if (evaluation is not null)
        {
            // Nothing says whether a project that was not restored, or that MSBuild failed on, uses SqlSource.
            LogValue uses = evaluation.State switch
            {
                ProjectState.UsesSqlSource => true,
                ProjectState.DoesNotUseSqlSource => false,
                _ => LogValue.Null,
            };
            members.Add(new("usesSqlSource", uses));
        }

        if (evaluation?.Manifest is { } manifest)
        {
            members.Add(new("targetFramework", manifest.TargetFramework));
            members.Add(new("sqlFiles", manifest.Files.Length));
            members.Add(new("compileFiles", manifest.CompileFiles.Length));
        }

        trace.Log.Write("project", [.. members]);
    }

    /// <summary>A file that a type claims.</summary>
    public static void File(RunTrace trace, PlannedFile file) =>
        trace.Log.Write(
            "file",
            new("path", file.Path),
            new("project", file.ProjectPath),
            new("dialect", SqlDialectName.Canonical(file.Dialect)),
            new("state", StateOf(file.State)),
            new(
                "claims",
                LogValue.ListOf(
                    file.Claims.Select(static claim =>
                        LogValue.MapOf(
                            new("project", claim.ProjectPath),
                            new("source", claim.SourcePath),
                            new("line", claim.Position.Line + 1)
                        )
                    )
                )
            ),
            new("queries", file.Queries.Count)
        );

    /// <summary>A selected database: where its connection came from, never what it is.</summary>
    public static void Database(
        RunTrace trace,
        PlannedDatabase database,
        DatabaseConnection connection,
        bool hasDescriber
    ) =>
        trace.Log.Write(
            "database",
            new("name", database.Name),
            new("engine", SqlDialectName.Canonical(database.Dialect)),
            new("connectionSource", connection.Source ?? "none"),
            new("describer", hasDescriber)
        );

    /// <summary>A session that opened.  Every member but the name is the describer's text.</summary>
    public static void Opened(RunTrace trace, PlannedDatabase database, ServerInfo server) =>
        trace.Log.Write(
            "database.open",
            new("name", database.Name),
            new("serverVersion", server.Version),
            new("driver", server.Driver),
            new("driverVersion", server.DriverVersion),
            new(
                "settings",
                LogValue.MapOf([.. server.Settings.Select(static setting => new LogMember(setting.Name, setting.Value))])
            )
        );

    /// <summary>A session that did not open.</summary>
    /// <param name="trace">The run's, without the connection's value.</param>
    /// <param name="database">The database.</param>
    /// <param name="error">The id of the error that was reported.</param>
    public static void NotOpened(RunTrace trace, PlannedDatabase database, string error) =>
        trace.Log.Write("database.open", new("name", database.Name), new("error", error));

    /// <summary>What the run decided for a query that needs an entry.</summary>
    /// <param name="trace">The run's.</param>
    /// <param name="query">The query, in the state it ended in.</param>
    /// <param name="durationMs">How long its describe took, for one that was described.</param>
    /// <param name="error">The id of the error, for one that failed where a database was asked.</param>
    public static void Query(RunTrace trace, QueryWork query, long? durationMs = null, string? error = null)
    {
        var planned = query.Planned;
        var members = new List<LogMember>
        {
            new("file", planned.Query.NameLocation.Path),
            new("name", planned.Query.Name),
            new("database", planned.Database),
            new("hash", planned.Hash),
            new("selected", planned.IsSelected),
            new("decision", DecisionOf(query.State)),
        };
        if (durationMs is { } duration)
        {
            members.Add(new("durationMs", duration));
        }

        if (error is not null)
        {
            members.Add(new("error", error));
        }

        trace.Log.Write("query", [.. members]);
    }

    /// <summary>What the run did with a sidecar, or under <c>--check</c> what it found.</summary>
    /// <param name="trace">The run's.</param>
    /// <param name="path">The full path of the sidecar.</param>
    /// <param name="action">
    /// <c>written</c>, <c>unchanged</c>, <c>deleted</c>, <c>held-back</c> or <c>failed</c>; <c>same</c> or
    /// <c>differs</c> under <c>--check</c>.
    /// </param>
    public static void Sidecar(RunTrace trace, string path, string action) =>
        trace.Log.Write("sidecar", new("path", path), new("action", action));

    private static string StateOf(PlannedFileState state) =>
        state switch
        {
            PlannedFileState.Ready => "ready",
            PlannedFileState.HasParseErrors => "has-parse-errors",
            _ => "not-describable",
        };

    // A query that is still to be described has no event yet: it gets one when its describe ends.
    private static string DecisionOf(QueryState state) =>
        state switch
        {
            QueryState.Described => "described",
            QueryState.Skipped => "skipped",
            QueryState.LeftOut => "left-out",
            _ => "failed",
        };
}
```

- [ ] **Step 7: Write the events where each thing happens**

In `src/SqlSource.Tool/Projects/RunProjects.cs`, add `using SqlSource.Tool.Logging;`.  Keep the unit's projects before `--project` narrows them, and write the events: one for each project left out, then one for each evaluation as the loop reaches it, before its error.

```csharp
        var all = projects;
        var restricted = named.Count > 0;
        if (restricted)
        {
            projects = Restrict(projects, named, unit, host.WorkingDirectory, reporter);
        }

        // A project that --project leaves out is not asked about.
        foreach (var left in all.Except(projects, StringComparer.OrdinalIgnoreCase))
        {
            RunEvents.Project(reporter.Trace, left, evaluation: null);
        }
```

and as the first statement of the `foreach` over the evaluations: `RunEvents.Project(reporter.Trace, evaluation.ProjectPath, evaluation);`.

In `src/SqlSource.Tool/DescribeCommand.cs`, in `PlanAsync`, add `using SqlSource.Tool.Logging;` and, after `RunPlanner.Plan` and before the loop that reports its errors:

```csharp
        foreach (var file in result.Plan.Files)
        {
            RunEvents.File(reporter.Trace, file);
        }
```

In `src/SqlSource.Tool/Describing/SidecarStore.cs`, add `using SqlSource.Tool.Logging;`.  `Write` and `Delete` return whether the file was changed, `true` at the end of the `try` and `false` after the report in the `catch`, and `Apply` says what happened to each sidecar:

```csharp
            switch (outcome.Action)
            {
                case FileAction.Write:
                    var written = Write(
                        outcome.SidecarPath,
                        outcome.Text ?? throw new InvalidOperationException("A file to write has no text."),
                        reporter
                    );
                    RunEvents.Sidecar(reporter.Trace, outcome.SidecarPath, written ? "written" : "failed");
                    break;
                case FileAction.Delete:
                    var deleted = Delete(outcome.SidecarPath, reporter);
                    RunEvents.Sidecar(reporter.Trace, outcome.SidecarPath, deleted ? "deleted" : "failed");
                    break;
                case FileAction.HeldBack:
                    if (outcome.HasDescribed)
                    {
                        reporter.Report(NotWritten(outcome));
                    }

                    RunEvents.Sidecar(reporter.Trace, outcome.SidecarPath, "held-back");
                    break;
                case FileAction.Unchanged:
                    RunEvents.Sidecar(reporter.Trace, outcome.SidecarPath, "unchanged");
                    break;
                case FileAction.None:
                    // The run did not look at this sidecar: nothing is changed, and nothing is said.
                    break;
                default:
                    throw new InvalidOperationException($"Unknown action {outcome.Action}.");
            }
```

In `src/SqlSource.Tool/Describing/RunSummary.cs`, add `using SqlSource.Tool.Logging;` and:

```csharp
    /// <summary>
    /// The counts of each selected database as <c>run.end</c> holds them, in the plan's order: the numbers of the
    /// summary's lines, under the names of those lines.
    /// </summary>
    public static LogValue Counts(ImmutableArray<PlannedDatabase> selected, ImmutableArray<FileWork> files, bool check) =>
        LogValue.ListOf(selected.Select(database => Counts(database, files, check)));

    private static LogValue Counts(PlannedDatabase database, ImmutableArray<FileWork> files, bool check)
    {
        var name = new LogMember("name", database.Name);
        var engine = new LogMember("engine", SqlDialectName.Canonical(database.Dialect));
        if (check)
        {
            var (same, differ, notDescribed) = CountChecked(files, database.Name);
            return LogValue.MapOf(name, engine, new("same", same), new("differ", differ), new("failed", notDescribed));
        }

        var (described, skipped, failed) = Count(files, database.Name);
        return LogValue.MapOf(
            name,
            engine,
            new("described", described),
            new("skipped", skipped),
            new("failed", failed)
        );
    }
```

In `src/SqlSource.Tool/Describing/DescribeRun.cs`, add `using SqlSource.Tool.Logging;`:

- After the loop that reports the errors of `RunDecisions.Decide`:

```csharp
        // What the run decided without asking a database.  A query to describe gets its event when that ends.
        var trace = reporter.Trace;
        foreach (
            var query in files
                .SelectMany(static file => file.Queries)
                .Where(static query => query.Planned.NeedsEntry && query.State != QueryState.ToDescribe)
        )
        {
            RunEvents.Query(trace, query);
        }
```

- In the loop over `SidecarCheck.Compare`, after the inner loop that reports the differences of a file:

```csharp
                if (check.WasCompared)
                {
                    RunEvents.Sidecar(trace, check.SidecarPath, check.Differences.Count == 0 ? "same" : "differs");
                }
```

- Before the summary's lines are written: `trace.Databases = RunSummary.Counts(selected, files, options.Check);`

In `src/SqlSource.Tool/Describing/DatabaseRuns.cs`, add `using SqlSource.Parsing;` and `using SqlSource.Tool.Logging;`, and replace `RunAsync`, `RunDatabaseAsync`, `DescribeAllAsync` and `Fail`.  `Guard`, `NoSecretIn`, `NoConnection` and `ToDescribe` stay as they are.

```csharp
    public static async Task RunAsync(
        ImmutableArray<PlannedDatabase> selected,
        ImmutableArray<FileWork> files,
        Connections connections,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        foreach (var database in selected)
        {
            RunEvents.Database(
                reporter.Trace,
                database,
                connections.For(database.Name),
                host.Describers.Find(database.Dialect) is not null
            );

            var queries = ToDescribe(files, database.Name);
            if (queries.Count == 0)
            {
                // Nothing of it is asked of a database, so it needs neither a describer nor a connection.
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await RunDatabaseAsync(database, queries, connections, host, reporter, cancellationToken);
        }
    }

    // The first of the four that holds is reported, and the queries fail: no describer, at each query; a variable
    // that two databases read; no connection; a session that cannot be opened.  The last three once, at the first
    // query.
    private static async Task RunDatabaseAsync(
        PlannedDatabase database,
        List<QueryWork> queries,
        Connections connections,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        if (host.Describers.Find(database.Dialect) is not { } describer)
        {
            foreach (var query in queries)
            {
                reporter.Report(DescribeErrors.NoDescriber(query.Planned, database.Dialect));
            }

            Fail(queries, ToolDiagnostics.NoDescriberForDialect.Id, reporter.Trace);
            return;
        }

        var first = queries[0].Planned;
        var connection = connections.For(database.Name);
        if (connection.Value is not { } value)
        {
            var error = NoConnection(first, connection, connections);
            reporter.Report(error);
            Fail(queries, error.Descriptor.Id, reporter.Trace);
            return;
        }

        // From here on the run holds the connection's value, and what a describer gives may hold it: everything
        // said of this database goes through a trace that removes it.
        var trace = reporter.Trace.Without(value);
        var engine = SqlDialectName.Canonical(database.Dialect);
        var guard = new Guard(value, host.GetEnvironmentVariable, cancellationToken);
        var request = new OpenRequest(database.Name, value, host.Exchange(database.Name));
        var log = new SessionRunLog(reporter.Trace.Log, value, engine + ".");
        var opened = await guard.CallAsync(() => describer.OpenAsync(request, log, cancellationToken));
        if (opened.Session is not { } session)
        {
            var failure = guard.Call(() => opened.Failure ?? throw new InvalidOperationException(Guard.Nothing));
            reporter.Report(Redaction.Of(DescribeErrors.Failure(failure, first, database, server: null), value));
            RunEvents.NotOpened(trace, database, failure.Descriptor.Id);
            Fail(queries, failure.Descriptor.Id, trace);
            return;
        }

        try
        {
            await DescribeAllAsync(database, queries, session, guard, host.Time, reporter, trace, cancellationToken);
        }
        catch
        {
            // The first exception is the run's end: a session that fails to close after it adds nothing.
            await Guard.CloseQuietlyAsync(session);
            throw;
        }

        await guard.CallAsync(session.DisposeAsync);
    }

    private static async Task DescribeAllAsync(
        PlannedDatabase database,
        List<QueryWork> queries,
        IDescribeSession session,
        Guard guard,
        TimeProvider time,
        Reporter reporter,
        RunTrace trace,
        CancellationToken cancellationToken
    )
    {
        var value = guard.Secret;
        var server = guard.Call(() => session.Server);
        RunEvents.Opened(trace, database, server);
        foreach (var query in queries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var planned = query.Planned;
            var describe = new DescribeRequest(
                planned.Query.Name,
                SampleSql.Build(planned.Query),
                planned.Query.Parameters
            );
            var started = time.GetTimestamp();
            var result = await guard.CallAsync(() => session.DescribeAsync(describe, cancellationToken));
            var duration = (long)time.GetElapsedTime(started).TotalMilliseconds;
            if (result.Description is { } description)
            {
                query.Entry = guard.Call(() =>
                    NoSecretIn(EntryBuilder.Build(planned, database.Dialect, database.Name, server, description), value)
                );
                query.State = QueryState.Described;
                RunEvents.Query(trace, query, duration);
            }
            else
            {
                var failure = guard.Call(() => result.Failure ?? throw new InvalidOperationException(Guard.Nothing));
                reporter.Report(Redaction.Of(DescribeErrors.Failure(failure, planned, database, server), value));
                query.State = QueryState.Failed;
                RunEvents.Query(trace, query, error: failure.Descriptor.Id);
            }
        }
    }
```

```csharp
    // The queries fail with one error, which was reported: each has its event.
    private static void Fail(List<QueryWork> queries, string error, RunTrace trace)
    {
        foreach (var query in queries)
        {
            query.State = QueryState.Failed;
            RunEvents.Query(trace, query, error: error);
        }
    }
```

`RunEvents.Opened` reads a record that a describer made, outside the guard.  That is safe for `Settings`: an `EquatableArray` that is the default of its type enumerates as empty.

- [ ] **Step 8: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.RunLogTests`
Expected: PASS.

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.RedactionTests`
Expected: PASS, 8 tests.

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.  `DatabaseRunsTests` and `SidecarStoreTests` call the two units with a reporter whose trace has no log, and pass as they are.

- [ ] **Step 9: Bring the documents this task makes wrong**

In `src/SqlSource.Tool/AGENTS.md`:

- In the bullet **Everything from outside comes through `ToolHost`**, replace its last sentence with: `` What a later sub-phase takes from outside, a process or a database, is a new member of `ToolHost`, as the clock is: `ToolHost.Time` gives the time of a log's events and how long a step took, and a test brings `tests/SqlSource.Tool.Tests/TestClock.cs`. ``
- In the bullet **A connection's value goes to the describer and nowhere else**, after the sentence that ends `` which writes the whole value as `***`. ``, add: `` The log is held to the same: nothing hands a log a value, `Logging/LoggedArguments.cs` builds `arguments` from what `UsageCheck` read and not from the text, a describer's log is a `Logging/SessionRunLog.cs` that removes the value from every string of an event, and what the run itself writes of a database after its connection is found goes through `RunTrace.Without`.  `tests/SqlSource.Tool.Tests/RedactionTests.cs` holds the log to all of it; a new event that carries what a describer gave gets a case there. ``
- Add two bullets after the one that starts **A database needs a describer and a connection only when**:

```markdown
- **`Logging/` is what a run says of itself beside its errors and its summary.**  `Cli.RunAsync` opens the file of `--log`, or of `SQLSOURCE_LOG`, after the command line was read and only for a `describe` that runs, sets `Reporter.Trace`, and writes `run.start` and `run.end`, since the exit code and the error of an exception are known only there.  Every step that has the reporter has the trace.  `Logging/RunEvents.cs` builds each event in one place.  A step that reports nothing, the planner, `RunDecisions`, `FileOutcomes`, `SidecarCheck`, writes no event either: its caller does, from what it returned.
- **An event's name and its members are read by people and by phase 10's bundle.**  A member is added and never renamed, and a value is one of `Logging/LogValue.cs`'s closed set.  A describer's events have its engine before their names, which `SessionRunLog` puts there.  `Logging/RunLogFile.cs` cuts a string over 1 MiB, and never ends a run: a write that fails is kept, and `Cli` reports it once as `SQLSRC218` when the run ends.
```

In `docs/tech-debt/TD-0034-redaction-of-a-describers-text-has-holes.md`, at the end of the section **Problem**, add:

```markdown
Sub-phase 2.6 puts the same removal before two more places, with the same holes: the log of `--log`, through [`SessionRunLog`](../../src/SqlSource.Tool/Logging/SessionRunLog.cs), and the lines of `--verbose`, through [`RunTrace`](../../src/SqlSource.Tool/Logging/RunTrace.cs).  A describer that logs a value it changed, or one keyword of it, writes it to a file that a user may attach to an issue.
```

and in **Proposed fix**, add `Give \`SessionRunLog\` and \`RunTrace\` the same parts.` as its last sentence.  In `docs/tech-debt/README.md`, the row of `TD-0034` says what the removal covers: add `, in what is printed, said under \`--verbose\` and logged` where it names the text a describer gave.

- [ ] **Step 10: Format, validate, commit**

Run `./format.sh`, then `./pre-commit-validation.sh` as its own command.

```bash
git add src tests docs
git commit -m "Log each project, file, database, query and sidecar, and give a describer a log that removes its connection

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: `--verbose`

**Files:**
- Modify: `src/SqlSource.Tool/Logging/RunTrace.cs`, `RunEvents.cs`
- Modify: `src/SqlSource.Tool/DescribeCommand.cs`, `src/SqlSource.Tool/Cli.cs`
- Modify: `src/SqlSource.Tool/README.md`, `src/SqlSource.Tool/AGENTS.md`
- Test: `tests/SqlSource.Tool.Tests/VerboseTests.cs` (create), `UsageCheckTests.cs`, `DescribeTests.cs`, `RedactionTests.cs`

**Interfaces:**
- Consumes: `RunTrace`, `RunEvents`, `OneLine.Of`, `Redaction.Of`, `ListedFiles.Read(ProjectManifest).Paths`, `RunUnit(Kind, Path)`.
- Produces:
  - `RunTrace(IRunLog log, TextWriter? verbose = null, string? secret = null)` with `Say(string text)`, which writes `sqlsource: ` and the text on one line when the run is verbose, with the trace's secret removed.
  - `DescribeCommand.IsVerbose(ParseResult)`.
  - `RunEvents.Unit(RunTrace trace, RunUnit unit, bool wasGiven)` and `RunEvents.Unclaimed(RunTrace trace, string projectPath, int files)`, which say a line and write no event.

Every line starts with `sqlsource: ` and is on standard output.  The texts, which the tests hold:

| Thing | Line, after `sqlsource: ` |
|----|----|
| The unit | `unit '<path>', a solution, found in the working directory`, or `a project`, or `from the path given` |
| A project that was read | `project '<path>': <framework>, <n> .sql files` |
| A project left out | `project '<path>' is left out: it does not use SqlSource`, or `--project does not name it` |
| A project with an error | `project '<path>' could not be read` |
| Files no type claims | `project '<path>': <n> .sql files that no type claims`, when there are any |
| A claimed file | `file '<path>': <dialect>, ready, claimed by <source>(<line>)`, with `has parse errors` or `cannot be described` for the other states, and each claim after a comma |
| A database | `database '<name>' (<engine>): connection from <source>`, or `no connection` |
| A session | `database '<name>': server <version>`, or `database '<name>': no session` |
| A query | `query '<name>' in '<file>': described in <n> ms`, `skipped, its entry is current`, `failed` or `not selected` |
| A sidecar | `sidecar '<path>': written`, `unchanged`, `deleted`, `left as it was`, `could not be changed`, `the same` or `different` |

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tool.Tests/VerboseTests.cs`.  Read `DoesNotUse` in `RunProjectsTests.cs` first, and make the project that does not use SqlSource the way it does.

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

// "--verbose": prose on standard output for the person watching, one line for each thing the run decided.
public sealed class VerboseTests : IDisposable
{
    // The plan holds a database in the order it first holds a query of it: GetUser comes before GetInvoice.
    private const string Summary =
        "postgres (postgres): 2 described, 0 skipped, 0 failed\nbilling (postgres): 1 described, 0 skipped, 0 failed\n";

    private readonly DescribeScene _scene = new();
    private readonly TestProject _lib;
    private readonly string _items;
    private readonly string _solution;
    private readonly string _tools;
    private readonly string _users;

    public VerboseTests()
    {
        // Two projects that use SqlSource and one that does not, in a solution; two databases.
        _users = _scene.Project.AddSql(
            "Users.sql",
            "-- name: GetUser\nSELECT 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n"
        );
        _lib = Plans.Postgres(_scene.Run.Folder, "Lib", "Lib").AnsweredBy(_scene.Run.Processes);
        _items = _lib.AddSql("Items.sql", "-- name: GetItem\nSELECT 3;\n");
        // A file that the project lists and that stands outside the folder of its type: no type claims it.
        _lib.ListSql(_scene.Run.Folder.WriteFile("Elsewhere/Loose.sql", "-- name: Loose\nSELECT 9;\n"));
        _tools = _scene.Run.Folder.WriteFile("Tools/Tools.csproj", "<Project />");
        _scene.Run.Processes.Projects[_tools] = new FakeProject
        {
            Imported = "",
            ProjectAssetsFile = _scene.Run.Folder.WriteFile("Tools/obj/project.assets.json", "{}"),
        };
        _solution = _scene.Run.Folder.WriteFile(
            "All.slnx",
            "<Solution><Project Path=\"App.csproj\" /><Project Path=\"Lib/Lib.csproj\" />"
                + "<Project Path=\"Tools/Tools.csproj\" /></Solution>"
        );
        _scene.Describer.BeforeDescribe = _ =>
        {
            _scene.Clock.Advance(TimeSpan.FromMilliseconds(25));
            return Task.CompletedTask;
        };
    }

    public void Dispose() => _scene.Dispose();

    private static string Line(string text) => "sqlsource: " + text + "\n";

    [Fact]
    public async Task Run_Verbose_SaysOfEachProjectFileDatabaseQueryAndSidecarWhatItDecided()
    {
        var app = _scene.Project.ProjectPath;
        var queries = _scene.Project.PathOf("Queries.cs");

        var result = await _scene.DescribeAsync("All.slnx", "--verbose");

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBeEmpty();
        result.Out.ShouldBe(
            Line($"unit '{_solution}', a solution, from the path given")
                + Line($"project '{app}': net10.0, 1 .sql files")
                + Line($"project '{_lib.ProjectPath}': net10.0, 2 .sql files")
                + Line($"project '{_tools}' is left out: it does not use SqlSource")
                + Line($"project '{_lib.ProjectPath}': 1 .sql files that no type claims")
                + Line($"file '{_users}': postgres, ready, claimed by {queries}(1)")
                + Line($"file '{_items}': postgres, ready, claimed by {_lib.PathOf("Queries.cs")}(1)")
                + Line("database 'postgres' (postgres): connection from SQLSOURCE_CONNECTION_POSTGRES")
                + Line("database 'postgres': server 16.4")
                + Line($"query 'GetUser' in '{_users}': described in 25 ms")
                + Line($"query 'GetItem' in '{_items}': described in 25 ms")
                + Line("database 'billing' (postgres): connection from SQLSOURCE_CONNECTION_BILLING")
                + Line("database 'billing': server 16.4")
                + Line($"query 'GetInvoice' in '{_users}': described in 25 ms")
                + Line($"sidecar '{_users}.json': written")
                + Line($"sidecar '{_items}.json': written")
                + Summary
        );
    }

    [Fact]
    public async Task Run_SecondVerboseRun_SaysWhatWasSkippedAndWhatIsUnchanged()
    {
        _ = await _scene.DescribeAsync("All.slnx");

        var result = await _scene.DescribeAsync("All.slnx", "--verbose", "--database", "postgres", "--project", "App.csproj");

        result.Out.ShouldContain(Line($"project '{_lib.ProjectPath}' is left out: --project does not name it"));
        result.Out.ShouldContain(Line($"query 'GetUser' in '{_users}': skipped, its entry is current"));
        result.Out.ShouldContain(Line($"query 'GetInvoice' in '{_users}': not selected"));
        result.Out.ShouldContain(Line($"sidecar '{_users}.json': unchanged"));
        // Nothing of "postgres" is asked of a database, so no session is opened and none is spoken of.
        result.Out.ShouldNotContain("server 16.4");
    }

    [Fact]
    public async Task Run_VerboseRunThatFails_SaysWhatFailedAndWhatWasLeftAsItWas()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");
        _ = _scene.Run.Environment.Remove("SQLSOURCE_CONNECTION_BILLING");

        var result = await _scene.DescribeAsync("App.csproj", "--verbose");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldContain(Line("unit '" + _scene.Project.ProjectPath + "', a project, from the path given"));
        result.Out.ShouldContain(Line($"query 'GetUser' in '{_users}': failed"));
        result.Out.ShouldContain(Line("database 'billing' (postgres): no connection"));
        result.Out.ShouldContain(Line($"query 'GetInvoice' in '{_users}': failed"));
        result.Out.ShouldContain(Line($"sidecar '{_users}.json': left as it was"));
    }

    [Fact]
    public async Task Run_VerboseCheck_SaysOfEachSidecarWhetherItIsTheSame()
    {
        _ = await _scene.DescribeAsync("All.slnx");
        File.Delete(_items + ".json");

        var result = await _scene.DescribeAsync("All.slnx", "--verbose", "--check");

        result.ExitCode.ShouldBe(2);
        result.Out.ShouldContain(Line($"sidecar '{_users}.json': the same"));
        result.Out.ShouldContain(Line($"sidecar '{_items}.json': different"));
    }

    [Fact]
    public async Task Run_WithoutVerbose_PrintsTheSummaryAlone()
    {
        var result = await _scene.DescribeAsync("All.slnx");

        result.ShouldBe(new CliResult(0, Summary, ""));
    }

    // "--log" without "--verbose" keeps the console quiet, and "--verbose" without "--log" writes nothing to disk.
    [Fact]
    public async Task Run_LogWithoutVerbose_LeavesTheConsoleAsItIsWithoutEither()
    {
        var result = await _scene.DescribeAsync("All.slnx", "--log", "run.log");

        result.ShouldBe(new CliResult(0, Summary, ""));
        File.Exists(_scene.Run.Folder.PathOf("run.log")).ShouldBeTrue();
    }

    [Fact]
    public async Task Run_VerboseWithoutLog_WritesNoFileButTheSidecars()
    {
        _ = await _scene.DescribeAsync("All.slnx", "--verbose");

        Directory.GetFiles(_scene.Run.Folder.Path, "*.log", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_VerboseInTheWorkingDirectory_SaysTheUnitWasFoundThere()
    {
        File.Delete(_solution);

        var result = await _scene.DescribeAsync("--verbose");

        result.Out.ShouldStartWith(
            Line($"unit '{_scene.Project.ProjectPath}', a project, found in the working directory")
        );
    }
}
```

Add to `tests/SqlSource.Tool.Tests/RedactionTests.cs`:

```csharp
    // A line of "--verbose" that holds what a describer gave is written as an error is.
    [Fact]
    public async Task Run_VerboseWithADescriberThatGivesItsConnectionAsItsServersVersion_SaysTheMark()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = Value;
        _scene.Describer.Server = _scene.Describer.Server with { Version = Value };

        var result = await _scene.DescribeAsync("--verbose");

        result.Out.ShouldNotContain(Marker);
        result.Error.ShouldNotContain(Marker);
        result.Out.ShouldContain("sqlsource: database 'postgres': server ***\n");
    }
```

Add to `tests/SqlSource.Tool.Tests/SecretTests.cs`, beside the test task 6 added:

```csharp
    [Fact]
    public async Task Run_ValueOnTheCommandLineAndEveryErrorWithVerbose_IsNeverSaid()
    {
        AddEveryError();

        var result = await _scene.DescribeAsync("--connection", "postgres=" + Value, "--verbose");

        ShouldHold(result, "213,214,216,217,218,221,999");
        result.Out.ShouldContain("sqlsource: database 'postgres' (postgres): connection from --connection\n");
    }
```

In `tests/SqlSource.Tool.Tests/UsageCheckTests.cs`, add `"--verbose",` to the vocabulary of `Check_EveryAcceptedCommandLineOfTheTool_IsReadTheSameBySystemCommandLine`.  In `tests/SqlSource.Tool.Tests/DescribeTests.cs`, in `Run_Help_ShowsTheProjectOption`, add `result.Out.ShouldContain("--verbose");`.

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.VerboseTests`
Expected: FAIL, `sqlsource: unknown option '--verbose'`.

- [ ] **Step 2: Let a trace speak**

Replace `src/SqlSource.Tool/Logging/RunTrace.cs`:

```csharp
using System;
using System.IO;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Logging;

/// <summary>
/// What a run says of itself beside its errors and its summary: the events of its log, and under <c>--verbose</c>
/// a line of prose for the person watching.  <c>Reporter</c> carries it, so every step that can report can say
/// what it did.
/// </summary>
/// <param name="log">The log of the run.</param>
/// <param name="verbose">Standard output, when the run is verbose.</param>
/// <param name="secret">A connection's value that is removed from every line, or null.</param>
internal sealed class RunTrace(IRunLog log, TextWriter? verbose = null, string? secret = null)
{
    private const string Prefix = "sqlsource: ";

    public IRunLog Log => log;

    /// <summary>
    /// The counts of each database, as <c>run.end</c> holds them.  <c>describe</c> sets it when it has them, and
    /// <c>Cli</c>, which knows the exit code, writes the event.
    /// </summary>
    public LogValue Databases { get; set; } = LogValue.ListOf(Array.Empty<LogValue>());

    /// <summary>
    /// Says one line under <c>--verbose</c>, and nothing without it.  The text is kept on one line: it holds
    /// paths and names that the tool did not write.
    /// </summary>
    public void Say(string text) =>
        verbose?.WriteLine(Prefix + OneLine.Of(secret is null ? text : Redaction.Of(text, secret)));

    /// <summary>
    /// The same trace with one connection's value removed from everything said through it.  What the run says of
    /// a database once it has the connection goes through one: a server's version, a driver's name and a setting
    /// are a describer's text, as a failure's lines are.
    /// </summary>
    public RunTrace Without(string value) => new(new SessionRunLog(log, value), verbose, value);
}
```

- [ ] **Step 3: Say a line where each event is written**

In `src/SqlSource.Tool/Logging/RunEvents.cs`, add `using System.Globalization;`, `using System.IO;` if a name needs it, and a line to each function, after its event.  Each text is built with `string.Create(CultureInfo.InvariantCulture, ...)` where it holds a number.

`Project`, at its end:

```csharp
        trace.Say(
            evaluation switch
            {
                null => $"project '{path}' is left out: --project does not name it",
                { Manifest: { } read } => string.Create(
                    CultureInfo.InvariantCulture,
                    $"project '{path}': {read.TargetFramework}, {read.Files.Length} .sql files"
                ),
                { State: ProjectState.DoesNotUseSqlSource } => $"project '{path}' is left out: it does not use SqlSource",
                _ => $"project '{path}' could not be read",
            }
        );
```

`File` becomes a block body: the event as it is, then:

```csharp
        var state = file.State switch
        {
            PlannedFileState.Ready => "ready",
            PlannedFileState.HasParseErrors => "has parse errors",
            _ => "cannot be described",
        };
        var claims = file.Claims.Select(static claim =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"{claim.SourcePath}({claim.Position.Line + 1})"
            )
        );
        trace.Say(
            $"file '{file.Path}': {SqlDialectName.Canonical(file.Dialect)}, {state}, "
                + $"claimed by {string.Join(", ", claims)}"
        );
```

`Database`:

```csharp
        trace.Say(
            $"database '{database.Name}' ({SqlDialectName.Canonical(database.Dialect)}): "
                + (connection.Source is { } source ? $"connection from {source}" : "no connection")
        );
```

`Opened`: `trace.Say($"database '{database.Name}': server {server.Version}");`

`NotOpened`: `trace.Say($"database '{database.Name}': no session");`

`Query`, at its end:

```csharp
        string decision;
        if (!planned.IsSelected)
        {
            decision = "not selected";
        }
        else if (query.State == QueryState.Described)
        {
            decision = string.Create(CultureInfo.InvariantCulture, $"described in {durationMs ?? 0} ms");
        }
        else
        {
            decision = query.State == QueryState.Skipped ? "skipped, its entry is current" : "failed";
        }

        trace.Say($"query '{planned.Query.Name}' in '{planned.Query.NameLocation.Path}': {decision}");
```

`Sidecar`:

```csharp
        var said = action switch
        {
            "held-back" => "left as it was",
            "failed" => "could not be changed",
            "same" => "the same",
            "differs" => "different",
            _ => action,
        };
        trace.Say($"sidecar '{path}': {said}");
```

And the two that say a line and write no event:

```csharp
    /// <summary>The unit of the run and how it was found.  The log has it in <c>arguments</c> and its projects.</summary>
    public static void Unit(RunTrace trace, RunUnit unit, bool wasGiven) =>
        trace.Say(
            $"unit '{unit.Path}', a {(unit.Kind == RunUnitKind.Solution ? "solution" : "project")}, "
                + (wasGiven ? "from the path given" : "found in the working directory")
        );

    /// <summary>How many <c>.sql</c> files of a project no type claims: the tool reads none of them.</summary>
    public static void Unclaimed(RunTrace trace, string projectPath, int files)
    {
        if (files > 0)
        {
            trace.Say(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"project '{projectPath}': {files} .sql files that no type claims"
                )
            );
        }
    }
```

- [ ] **Step 4: Give `describe` the flag, and call the two new lines**

In `src/SqlSource.Tool/DescribeCommand.cs`:

```csharp
    private const string VerboseOption = "--verbose";
```

```csharp
        var verbose = new Option<bool>(VerboseOption)
        {
            Description = "Say what the run decides for each project, file, database and query, as it goes.",
            Arity = ArgumentArity.Zero,
        };
```

Add `verbose,` to the command's initializer, before `log,`, and:

```csharp
    /// <summary>Whether a <c>describe</c> that runs was given <c>--verbose</c>.</summary>
    public static bool IsVerbose(ParseResult parsed) => Runs(parsed) && parsed.GetValue<bool>(VerboseOption);
```

In `PlanAsync`, after the unit was found: `RunEvents.Unit(reporter.Trace, unit, wasGiven: unitPath is not null);`.  After the loop over the plan's files that task 6 added:

```csharp
        foreach (var manifest in manifests)
        {
            var claimed = result.Plan.Files.Count(file =>
                file.Claims.Any(claim => string.Equals(claim.ProjectPath, manifest.ProjectPath, StringComparison.Ordinal))
            );
            RunEvents.Unclaimed(reporter.Trace, manifest.ProjectPath, ListedFiles.Read(manifest).Paths.Count - claimed);
        }
```

The expected output of `Run_Verbose_SaysOfEachProjectFileDatabaseQueryAndSidecarWhatItDecided` has this line before the lines of the files: put this loop before the loop of `RunEvents.File`.

In `src/SqlSource.Tool/Cli.cs`, in `RunAsync`, the trace is set for every `describe` that runs, with or without a log.  Remove `reporter.Trace = new RunTrace(log);` from the block that opens the log, and declare the trace before that block, so that `run.start` is still the first event:

```csharp
            // What the run says of itself: to the log, when it has one, and to the person watching under --verbose.
            IRunLog events = NullRunLog.Instance;
```

Inside the block, after the check for `log is null`: `events = log;`.  After the block:

```csharp
            if (DescribeCommand.Runs(parsed))
            {
                reporter.Trace = new RunTrace(events, DescribeCommand.IsVerbose(parsed) ? host.Out : null);
            }
```

`run.start` is written with `log.Write` as before, ahead of this.

- [ ] **Step 5: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.VerboseTests`
Expected: PASS, 8 tests.  If the first test differs only in the order of two lines, the order of the code is the order things happen in: change the expectation, and say so in the commit message.

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.

- [ ] **Step 6: Bring the documents this task makes wrong**

In `src/SqlSource.Tool/README.md`, before the paragraph on `--log`:

```markdown
`--verbose` says what the run decides as it goes, a line for each project, file, database, query and sidecar, each starting with `sqlsource:`.  Without it the tool prints its errors and the lines of its summary, and nothing else.
```

In `src/SqlSource.Tool/AGENTS.md`, at the end of the bullet **`Logging/` is what a run says of itself**, add: `` `RunTrace.Say` is the one place that writes a line of `--verbose`, each to standard output and on one line, and `RunEvents` says it beside the event it belongs to, so the two never tell two stories. ``

- [ ] **Step 7: Format, validate, commit**

Run `./format.sh`, then `./pre-commit-validation.sh` as its own command.

```bash
git add src tests
git commit -m "Say what a run decides under --verbose

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: The line that ends a failed run

**Files:**
- Modify: `src/SqlSource.Tool/Cli.cs`
- Modify: `src/SqlSource.Tool/README.md`, `src/SqlSource.Tool/AGENTS.md`
- Test: `tests/SqlSource.Tool.Tests/ExitCodeTests.cs` (create), `DescribeScene.cs`, and every test that compares the whole of standard error of a `describe` that fails

**Interfaces:**
- Consumes: `Cli.ExitCodeOf`, `DescribeCommand.Runs`, `DescribeCommand.LogPathOf`, `Reporter.Count`, `Reporter.DifferenceCount`.
- Produces: `Cli.RunAgainLine`, the constant `sqlsource: run again with --log <file> for a record of what the run did`; in the tests, `DescribeScene.RunAgain`, that line with its line break.

The line is written on standard error when all of these hold: the command line ran `describe`; the reporter wrote an error other than `SQLSRC219`; the run was not cancelled; and no log was asked for, by `--log` or by `SQLSOURCE_LOG`.  A log that was asked for and could not be opened is asked for.

- [ ] **Step 1: Write the failing tests**

In `tests/SqlSource.Tool.Tests/DescribeScene.cs`, add:

```csharp
    // What ends a "describe" that reported an error and wrote no log.
    public const string RunAgain = "sqlsource: run again with --log <file> for a record of what the run did\n";
```

Create `tests/SqlSource.Tool.Tests/ExitCodeTests.cs`:

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

// The three exit codes, and the one line that tells a user of a failed run how to get a record of it.
public sealed class ExitCodeTests : IDisposable
{
    private readonly DescribeScene _scene = new();
    private readonly string _users;

    public ExitCodeTests()
    {
        _users = _scene.Project.AddSql("Users.sql", "-- name: GetUser\nSELECT 1;\n");
    }

    public void Dispose() => _scene.Dispose();

    [Fact]
    public async Task Run_NothingReported_ExitsZeroWithoutTheLine()
    {
        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): 1 described, 0 skipped, 0 failed\n", ""));
    }

    [Fact]
    public async Task Run_ErrorWithoutALog_ExitsOneAndEndsWithTheLine()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith($"{_users}(1,10): error SQLSRC999: ");
        result.Error.ShouldEndWith($"{DescribeScene.See}999\n{DescribeScene.RunAgain}");
        result.Error.Split(DescribeScene.RunAgain).Length.ShouldBe(2);
    }

    // An exception is an error of the run too, and a log is what a maintainer will ask for.
    [Fact]
    public async Task Run_DescriberThatThrows_ExitsOneAndEndsWithTheLine()
    {
        _scene.Describer.Exceptions["GetUser"] = new InvalidOperationException("boom");

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldEndWith($"{DescribeScene.See}200\n{DescribeScene.RunAgain}");
    }

    // The line is for a difference that could not be checked, not for one that was found.
    [Fact]
    public async Task Run_CheckThatFindsADifference_ExitsTwoWithoutTheLine()
    {
        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("error SQLSRC219: ");
        result.Error.ShouldNotContain("run again");
    }

    [Fact]
    public async Task Run_CheckThatCouldNotDescribe_ExitsOneWithTheLine()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldEndWith(DescribeScene.RunAgain);
    }

    [Theory]
    [InlineData("option")]
    [InlineData("variable")]
    [InlineData("a log that cannot be opened")]
    public async Task Run_ErrorWithALogAskedFor_ExitsOneWithoutTheLine(string kind)
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");
        if (kind == "variable")
        {
            _scene.Run.Environment["SQLSOURCE_LOG"] = "run.log";
        }

        var result = kind switch
        {
            "option" => await _scene.DescribeAsync("--log", "run.log"),
            "variable" => await _scene.DescribeAsync(),
            _ => await _scene.DescribeAsync("--log", "tmp"),
        };

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC");
        result.Error.ShouldNotContain("run again");
    }

    [Theory]
    [InlineData("describe", "--conection", "x")]
    [InlineData("describe", "--database", "not a name")]
    [InlineData("--oops")]
    public async Task Run_WrongCommandLine_ExitsOneWithoutTheLine(params string[] args)
    {
        var result = await _scene.Run.RunAsync(args);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith("sqlsource: ");
        result.Error.ShouldNotContain("run again");
    }

    // A run that was cancelled prints nothing more, whatever it had reported.
    [Fact]
    public async Task Run_CancelledAfterAnError_ExitsOneWithoutTheLine()
    {
        _ = _scene.Project.AddSql("More.sql", "-- name: First\nSELECT 0;\n");
        _scene.Describer.Failures["First"] = FakeDescriber.Failure("First");
        using var cancel = new CancellationTokenSource();
        _scene.Describer.BeforeDescribe = request =>
            request.Name == "GetUser" ? cancel.CancelAsync() : Task.CompletedTask;

        var result = await _scene.Run.RunAsync(cancel.Token, "describe");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC999: ");
        result.Error.ShouldNotContain("run again");
    }

    [Fact]
    public async Task Run_CancelledBeforeItStarted_ExitsOneAndPrintsNothing()
    {
        var result = await _scene.Run.RunCancelledAsync("describe");

        result.ShouldBe(new CliResult(1, "", ""));
    }

    // An error of a run that never reached a database is still an error of "describe".
    [Fact]
    public async Task Run_NoUnit_ExitsOneWithTheLine()
    {
        File.Delete(_scene.Project.ProjectPath);

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC201: ");
        result.Error.ShouldEndWith(DescribeScene.RunAgain);
    }
}
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.ExitCodeTests`
Expected: FAIL in the four tests that expect the line.

- [ ] **Step 2: Write the line**

In `src/SqlSource.Tool/Cli.cs`:

```csharp
    /// <summary>
    /// What ends a <c>describe</c> that reported an error and wrote no log: there is no hint under each error, and
    /// this one line says how to get a record of the run.
    /// </summary>
    internal const string RunAgainLine = "sqlsource: run again with --log <file> for a record of what the run did";
```

In `RunAsync`, declare beside `log`:

```csharp
        // Whether the line that ends a failed run has a reader: "describe" ran, and no log was asked for.
        var mayPoint = false;
        var cancelled = false;
```

Set `mayPoint` right after the command line was found valid, before the log is opened, so that a log that could not be opened counts as asked for:

```csharp
            mayPoint =
                DescribeCommand.Runs(parsed)
                && DescribeCommand.LogPathOf(parsed, host.GetEnvironmentVariable) is null;
```

In the `catch` of a cancelled run, add `cancelled = true;`.  Replace the block that ends the method:

```csharp
        using (log)
        {
            exitCode = ExitCodeOf(reporter, exitCode);
            if (mayPoint && !cancelled && reporter.Count > reporter.DifferenceCount)
            {
                await host.Error.WriteLineAsync(RunAgainLine);
            }

            return log is null ? exitCode : End(log, exitCode, started, host, reporter);
        }
```

- [ ] **Step 3: Give the line to every test that compares the whole of standard error**

Run: `dotnet test --solution SqlSource.slnx`
Expected: FAIL, in the tests that compare the whole `Error` of a `describe` that reported an error.  For each, append `DescribeScene.RunAgain` to the expected text, as its last line.  They are in `DescribeRunTests`, `DescribeTests`, `FilterRunTests`, `FilterTests`, `SummaryTests`, `SecretTests`, `ConnectionTests`, `CheckTests`, `RunUnitTests` and `CliTests`, where they go through `Cli.RunAsync`; a test of a unit that is given a `Reporter` of its own, `DatabaseRunsTests` for one, does not change.  Three rules:

- A test that expects exit `2` and the whole of `Error` gains nothing.
- A test of a wrong command line gains nothing.
- A test in `CheckTests` that expects exit `1` and uses `ShouldContain` gains nothing.

Change no assertion other than by adding the line.  If a test fails for another reason, stop and find out why.

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.

- [ ] **Step 4: Check that the scripts that run the packed tool still pass**

`tools/check-tool-install.sh` runs `describe` in an empty folder and looks for `SQLSRC201` with `grep`: the new last line does not fail it.  Read the script to see that nothing compares the whole of standard error, and run it:

Run: `tools/check-tool-install.sh`
Expected: its last line starts `check-tool-install: ` and says the tool runs.

- [ ] **Step 5: Bring the documents this task makes wrong**

In `src/SqlSource.Tool/README.md`, at the end of the paragraph on `--log`, add:

```markdown
A run that reported an error and wrote no log ends with one line that says so: `sqlsource: run again with --log <file> for a record of what the run did`.
```

In `src/SqlSource.Tool/AGENTS.md`, at the end of the bullet **The format of an error is a contract with MSBuild and with editors**, add: `` No error carries a hint to a log: a `describe` that reported an error other than `SQLSRC219`, was not cancelled, and was asked for no log ends with `Cli.RunAgainLine`, once, on standard error.  A test that compares the whole of standard error of such a run ends its text with `DescribeScene.RunAgain`. ``

- [ ] **Step 6: Format, validate, commit**

Run `./format.sh`, then `./pre-commit-validation.sh` as its own command.

```bash
git add src tests
git commit -m "End a failed describe with one line that says how to get a log

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: The documents, and the close of phase 2

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`
- Modify: `src/SqlSource.Tool/AGENTS.md`, `src/SqlSource.Tool/README.md`
- Modify, if the read-through finds something: `docs/deferred/README.md` and a new `docs/deferred/D-0002-<slug>.md`; `docs/tech-debt/README.md` and a new item

**Interfaces:**
- Consumes: the code as tasks 2 to 8 left it.
- Produces: nothing that code reads.

- [ ] **Step 1: Read `src/SqlSource.Tool/AGENTS.md` against the code**

Read the file from its first bullet to its last, and for each sentence that names a file, a type, a member, a test or a behaviour, open what it names and check that the sentence is true.  Tasks 3, 6, 7 and 8 changed five bullets; this step is for the others.  Expect to correct at least these:

- The bullet **An option is declared so that `UsageCheck` reads it right** says that an option which takes a value is `ExactlyOne` "when it is given once": add that `UsageCheck` rejects a second one itself, with `option '<name>' is given more than once`, since System.CommandLine's message repeats the value.
- The bullet **A command's own rules for its values are in the command**: add `--log`, whose value must not be empty.
- The bullet **`Describing/` is a run after its plan** names the steps in order: check that it names `SidecarCheck.cs` where task 3 put it and `RunSummary.cs` last.
- The bullet **A filter narrows what a run may read and change**: add one sentence, that `--check` reports within the same bounds, and that `tests/SqlSource.Tool.Tests/CheckFilterTests.cs` holds it.

The file ends with its maintenance line.  Keep it under 250 lines, as the repository's rule for such a file asks; it is a list of long bullets, so count lines, not words.

- [ ] **Step 2: Read the tool's readme as a user**

Read `src/SqlSource.Tool/README.md` from the top.  It must say, each once and in an order a user can follow: what `describe` does; the connections; `--force`, `--database` and a `.sql` path; the summary; `--check` with its summary and what it compares; the three exit codes; `--verbose`; `--log`, `SQLSOURCE_LOG` and what a log never holds; the line that ends a failed run; the unit and `--project`; what the tool needs.  Every link is an absolute URL.  Move a paragraph that tasks 3 to 8 left out of that order; rewrite none.

- [ ] **Step 3: Close the outline's rows**

In `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`:

- In the table under `## Phases`, the row of 2.6 says `Done`.  The rows of 2.1 to 2.5 already do; check them.
- Under `## Phase 2 - the tool and the snapshot`, in `### Settled`, add these bullets after the one that starts **The summary under `--check`**:

```markdown
- **What `--check` says of a sidecar that `describe` would delete** it takes from the step that decides the deletion: in a run with no filter, in which nothing was reported, for a sidecar that can be opened and is not of a higher format version.  A difference that `describe` does not mend would be a build that stays red.
- **A difference in a query's hash** is reported as `the query has changed since it was described`, with neither hash; one in its engine or its database says what the project says, and any other what the database says.
- **A file none of whose selected queries could be described is compared with nothing.**  "Could not check" is exit `1`, never `SQLSRC219`.
- **The log is opened by the command line's reader, not by `describe`**, and only for a `describe` that runs: the exit code and the error of an exception are known only there, and the help must not empty the log of the last run.  A log that stops being writable does not end the run; it is one `SQLSRC218` when the run ends.
- **What the run writes of what a describer gave**, a server's version, a driver's name, a setting, goes through the removal of the connection's value, in the log and under `--verbose`, as a describer's own events do.  A describer's event has its engine before its name, which the tool puts there.
- **A claim is named in the log by where its attribute stands**, a file and a line: the tool reads syntax and keeps no name of a type.
- **`sidecar.action` has `failed`**, for a sidecar that could not be written or deleted, beside the six values planned.
- **The line that ends a failed run** is written for a `describe` that reported an error other than `SQLSRC219`, was not cancelled and was asked for no log.  A wrong command line has none.
```

- Read the section's `### Scope`, `### Decided`, `### Testing` and `### Documentation` against what phase 2 delivered, sub-phase by sub-phase.  Where a sentence says something that the code does not do, and no bullet under `### Settled` already says how it ended, either correct the sentence or, when it is work that was not done, go to step 4.  Do not rewrite a sentence that is true.

- [ ] **Step 4: Record what phase 2 did not deliver**

For each thing step 3 found that phase 2's specs planned and the code does not do, add an item to `docs/deferred` by the rules of `docs/deferred/AGENTS.md`: take `D-0002` from `Next id` in `docs/deferred/README.md` and raise that line, write `docs/deferred/D-0002-<short-slug>.md` with its five sections, and add its row.  When this plan was written none was known: sub-phases 2.1 to 2.5 each closed with nothing deferred, and this plan delivers every row of the spec's tables.  If step 3 finds none, change nothing in `docs/deferred`, and say in the pull request that the read-through found none.

Do the same for `docs/tech-debt`, by `docs/tech-debt/AGENTS.md`, for a problem that tasks 2 to 8 met and left.  Two are known and belong in one new item, `TD-0036-two-small-gaps-of-check-and-the-log.md`; `TD-0036` was the next id of `docs/tech-debt/README.md` when this plan was written, so take the one that is there now and raise that line:

- **The log's `os` and `runtime` come from `RuntimeInformation`, not from `ToolHost`**, so a test compares them with the machine's own and cannot fix them.  The rule is that everything from outside comes through the host.
- **An entry of a query that is gone, of a database that `--database` does not name, is not reported by `--check`, though `describe` with the same filter drops it** when it writes the file for another reason.  The epic asks for the first; the claim that `--check` reports what `describe` would change does not hold for that one entry.

Give each its **Problem**, **Why it exists**, **Impact**, **Proposed fix** and **Trigger** as the neighbouring items do; the trigger of the first is phase 10's bundle, which reads the log, and of the second a user who reports it.

- [ ] **Step 5: Validate, and check the version**

Run `./format.sh`, then `./pre-commit-validation.sh` as its own command.
Expected: every step `passed`.

```bash
git fetch --tags origin
```

```bash
git tag --list 'v*' --sort=-v:refname | head -n 1
```

Expected: nothing.  If a tag is printed and `VersionPrefix` in `Directory.Build.props` is not greater than it, stop and ask the owner what the new version is.

- [ ] **Step 6: Commit**

```bash
git add docs src
git commit -m "Close phase 2: the tool's documents read against the code, and the outline says Done

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 7: Open the pull request**

Push the branch and open a pull request against `main` whose description lists the nine commits, the eight departures of this plan from the spec, and what step 4 recorded.  It ends with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.  Reply to every CodeRabbit finding in its own thread, as `AGENTS.md` at the root asks.
