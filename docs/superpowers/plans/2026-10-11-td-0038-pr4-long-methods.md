# TD-0038, pull request 4: long methods - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The six methods of item 9 of TD-0038 each do one thing: `SidecarReader.ReadEntry`, `DatabaseRuns.DescribeAllAsync`, `ProjectEvaluator.EvaluateOneAsync`, `RunPlanner.PlanFile`, `RunPlanner.ReadProject` and `Cli.RunAsync` are split by the responsibilities the design names, and TD-0038 is deleted.

**Architecture:** Every change is a refactoring of a private method, or of `Cli.RunAsync`, whose signature stays, into private steps of the same class.  Nothing new is visible outside its file, so the existing tests hold each change, and five tests are added first to pin what no test holds today.  A sidecar read is a hot path, so its time and allocation are measured before the first change and after it.

**Tech Stack:** C# (`LangVersion` `preview`) on `netstandard2.0` with Roslyn 4.8.0 for `src/SqlSource`, and on `net8.0` for `src/SqlSource.Tool`; System.CommandLine; xunit v3 on Microsoft.Testing.Platform with Shouldly.

**Spec:** `docs/superpowers/specs/2026-10-10-td-0038-duplicated-patterns-design.md`, the section "Pull request 4".

**Branch:** `claude/td-0038-long-methods`, made from `main` in Task 1.  This file is untracked until Task 7 commits it.

## Where the plan settles what the spec leaves open

The user approved the first six on 2026-10-11.

- **`Cli.RunAsync` keeps two `try` blocks, in two methods.**  `run.end` must be written before the log is closed, so the `try`/`finally` that closes the log stays in `RunAsync`, with the end of the run.  The three catches move with the steps into `RunCommandAsync`.  No method nests a `try`.
- **`RunPlanner.ReadProject` merges its two parallel dictionaries**, `claimsOf` and `typesOf`, into one map of a path to a `FileClaims`: they are always filled together.
- **The invalid values of a project go through a private collector**, `InvalidValues`, which reports them in today's order: `SQLSRC011` in ordinal order, then `SQLSRC014` by name and by value.
- **A step of `ProjectEvaluator.EvaluateOneAsync` that may fail returns `Outcome<T, ProjectEvaluation>`**, the failure being the evaluation the method returns.
- **`SidecarReader.ReadEntry` keeps what it read in one private value type**, `EntryMembers`, handed on by `ref`, so an entry allocates what it does today.
- **The spec and the three earlier plans keep naming TD-0038.**  `docs/superpowers` still names earlier resolved items, and its `AGENTS.md` says a spec is not rewritten.  The item, its row and the mention in TD-0042 go.
- **The class of the steps of `Cli.RunAsync` is `RunState`, not `Run`.**  `Cli` has a method `Run`, and a type may not share a member's name.
- **A wrong command line and a log that cannot be opened end as exit code `1` from `RunCommandAsync`, and go through the end of the run like any other.**  Today they return `1` from inside the `try`.  The end of the run does nothing more for either: no log is open, and `MayPoint` is false, since it is false until the command line was read and false when a log was asked for.
- **The last step of `EvaluateOneAsync`, `ReadManifestAsync`, returns the `ProjectEvaluation` itself**: what it gives is what the method returns, success or failure.
- **`DescribeAllAsync` is not split.**  The spec asks for its record alone.

## Global Constraints

- Nothing a user sees changes: no message, id, exit code, generated file, sidecar, log member or line of `--verbose`.  `README.md`, `CONTRIBUTING.md`, `docs/diagnostics.md` and `docs/publishing.md` are not touched.
- The existing tests pass unchanged.  No test is edited; five are added.
- The budget of `SidecarReaderAllocationTests`, 1.7 bytes for a character, does not rise, and neither do those of `SqlFileParserAllocationTests` and `PathResolverAllocationTests`.
- No step changes the order in which errors are added or events are written.  Move code; do not reorder it.
- `src/SqlSource` targets `netstandard2.0` with Roslyn 4.8.0: no API newer than that, and no collection expression that targets `ImmutableArray<T>`.  `src/SqlSource.Tool` targets `net8.0`; do not raise it.
- A method of the tool that another file calls and that gives one value or one reason returns an `Outcome`; a private helper may be a `TryX`.  Every new method here is private.
- The build treats warnings as errors with `AnalysisLevel` `latest-all`.  Fix what it reports in the code, a `using` that nothing needs any more included; never suppress.  If a check is wrong, stop and tell the user.
- `ImplicitUsings` is off: a file names every namespace it uses.
- Every commit passes `./pre-commit-validation.sh` (it needs Docker running).  Run `./format.sh`, then the validation, then commit in a command of its own.  Never work around the hook.
- An `AGENTS.md` or a tech-debt item that names a member changes in the commit that moves what it says to another member.
- Prose in comments and documents follows the repository's style: plain sentences, two spaces after a full stop.
- End each commit message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

Run one test class with:

```bash
dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Planning.RunPlannerErrorTests"
```

The generator's tests are in `tests/SqlSource.Tests/SqlSource.Tests.csproj`.  Run everything with `dotnet test --solution SqlSource.slnx`.

A test this plan adds pins what the code does today, so it must pass before the change it guards.  If one fails before the change, the test has misread today's behaviour: correct its expectation to what the code does, never the code, and say so in the commit message.

## Review Focus

1. **An exception of the run that is not a describer's, with a log.**  Its `SQLSRC200` must be a `diagnostic` event of the log, before `run.end`: it is reported by the run's reporter, which `InvokeAsync` makes, and not by the one the run started with.  Test in Task 6.
2. **A project with an attribute that cannot be read, a dialect that is not valid and a setting that is not valid, the last two given by the property and by a file's metadata.**  The errors are `SQLSRC208`, then `SQLSRC011`, then `SQLSRC014`, each value once, and before any error of a file.  Test in Task 5.
3. **A file that two types of one project claim.**  Its claims are both there, in the order of their source files: the merged map must keep, for each path, the order in which the claims came.  Test in Task 5.
4. **A project of several frameworks whose second evaluation fails.**  It is `SQLSRC205` with that run's output, and no target is run.  Test in Task 3.
5. **An entry of a sidecar with two mistakes**, no `hash` and `columns` beside a result of no rows.  The first found is the one reported, the missing key: the checks keep their order.  Test in Task 1.

## File Structure

| File | Change |
|----|----|
| `src/SqlSource/Snapshot/Reading/SidecarReader.cs` | `ReadEntry` in four steps over a private `EntryMembers` |
| `src/SqlSource.Tool/Describing/DatabaseRuns.cs` | A private `SessionRun`; `DescribeAllAsync` takes it |
| `src/SqlSource.Tool/Projects/ProjectEvaluator.cs` | `EvaluateOneAsync` calls `AskAsync`, `RunManifestTargetAsync` and `ReadManifestAsync` |
| `src/SqlSource.Tool/Planning/RunPlanner.cs` | `PlanFile` calls `Parse`, `PlanQuery` and `FileOf`; `ReadProject` calls `ClaimsByFile` and `ReadMetadata` and reports through `InvalidValues` |
| `src/SqlSource.Tool/Cli.cs` | `RunAsync` calls `RunCommandAsync`, which calls `ReadCommandLineAsync`, `OpenLog` and `InvokeAsync`; a private `RunState` and `ParsedCommandLine` |
| `src/SqlSource.Tool/AGENTS.md`, `docs/tech-debt/TD-0036-two-small-gaps-of-check-and-the-log.md` | Name the step of `Cli` that now does what they say |
| `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs`, `tests/SqlSource.Tool.Tests/Projects/ProjectEvaluatorTests.cs`, `tests/SqlSource.Tool.Tests/Planning/RunPlannerErrorTests.cs`, `tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs`, `tests/SqlSource.Tool.Tests/Commands/CliTests.cs` | One test each, of the review focus |
| `docs/tech-debt/TD-0038-duplicated-and-uneven-patterns-in-the-generator-and-the-tool.md` | Deleted |
| `docs/tech-debt/README.md`, `docs/tech-debt/TD-0042-allocations-of-a-parse-that-no-budget-sees.md` | The row goes; the mention is reworded |

---

### Task 1: Measure a sidecar read, then `ReadEntry` in four steps

**Files:**
- Modify: `src/SqlSource/Snapshot/Reading/SidecarReader.cs` (from the comment above `ReadEntry` to the end of `RequireEntryKeys`)
- Test: `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs`
- Modify: this file, the table under "Measurements"

**Interfaces:**
- Consumes: nothing of another task.
- Produces: nothing another task uses.  `SidecarReader.Read(string) : SidecarReadResult` keeps its signature.

- [ ] **Step 1: Make the branch**

```bash
git switch -c claude/td-0038-long-methods
```

- [ ] **Step 2: Measure a read before any change**

`src/SqlSource/AGENTS.md` asks a change to a hot path for its time and allocation, before and after.  Add this method to `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderAllocationTests.cs`, with `using System.Diagnostics;` at the top.  It is never committed.

```csharp
    [Fact]
    public void Measure()
    {
        const int Reads = 20_000;
        var text = SidecarExamples.Read(SidecarExamples.Users);
        for (var read = 0; read < 10_000; read++)
        {
            _ = SidecarReader.Read(text);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (var read = 0; read < Reads; read++)
        {
            _ = SidecarReader.Read(text);
        }

        watch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Fail(
            $"{watch.Elapsed.TotalMilliseconds * 1000 / Reads:F2} us for a read, "
                + $"{allocated / (double)(Reads * text.Length):F3} bytes for a character, "
                + $"{allocated / Reads} bytes for a read of {text.Length} characters"
        );
    }
```

Run it three times:

```bash
dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj -c Release --filter-method "*SidecarReaderAllocationTests.Measure"
```

Expected: FAIL each time, with the numbers in the message.  Write the run with the middle time into the row "before" of the table under "Measurements" at the end of this file.  Leave the method in place until Step 8.

- [ ] **Step 3: Pin the order of an entry's checks**

Add to `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs`, after `Read_ColumnsWithoutRows_IsMalformedAtTheColumnsKey`:

```csharp
    // The first mistake found is the one reported: a key that is missing, before columns that have no rows.
    [Fact]
    public void Read_EntryWithoutAHashAndWithColumnsWithoutRows_IsMissingKeyOfTheHash()
    {
        var text = Q(
            Valid
                .Replace("'hash': 'h', ", string.Empty, StringComparison.Ordinal)
                .Replace("'resultKind': 'rows'", "'resultKind': 'none'", StringComparison.Ordinal)
        );

        ShouldBeMalformed(text, SidecarErrorKind.MissingKey, "{'engine'", 1, "hash");
    }
```

- [ ] **Step 4: Run it, and see it pass before the change**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-method "*Read_EntryWithoutAHashAndWithColumnsWithoutRows_IsMissingKeyOfTheHash"`
Expected: PASS.  It pins today's behaviour.

- [ ] **Step 5: Split `ReadEntry`**

In `src/SqlSource/Snapshot/Reading/SidecarReader.cs`, replace everything from the comment above `ReadEntry` through the closing brace of `RequireEntryKeys` (the methods `ReadEntry`, `ReadParametersOrDefer`, `ReadColumnsOrDefer`, `ReadDeferred` and `RequireEntryKeys`) with:

```csharp
    // The types of an entry's parameters and columns have the shape its engine picks.  The tool writes the engine
    // before them, and then they are read where they stand.  Key order is not significant to a reader, though: an
    // array that comes before the engine is passed over, and read once the object has ended.
    private static SidecarEntry? ReadEntry(SidecarCursor cursor, string name, TextSpan nameSpan)
    {
        if (!cursor.BeginObject(name, orNull: false, out var brace))
        {
            return null;
        }

        var members = new EntryMembers
        {
            Parameters = EquatableArray<SidecarParameter>.Empty,
            ParametersAt = -1,
            ColumnsAt = -1,
        };
        ReadMembers(cursor, ref members);
        CheckMembers(cursor, in members, brace);
        if (
            cursor.Error is not null
            || members.Hash is not { } hash
            || members.Engine is not { } engine
            || members.ResultKind is not { } kind
        )
        {
            return null;
        }

        ReadDeferred(cursor, engine, ref members);
        if (cursor.Error is not null)
        {
            return null;
        }

        return new SidecarEntry(
            name,
            nameSpan,
            hash,
            engine,
            members.Database,
            members.ServerVersion,
            kind,
            members.MatchesTable,
            members.Plan,
            members.TableMatch,
            members.Parameters,
            members.Columns
        );
    }

    // Reads every member of the entry, to its closing brace.
    private static void ReadMembers(SidecarCursor cursor, ref EntryMembers members)
    {
        var seen = 0;
        for (var key = cursor.NextKey(EntryKeys, ref seen); key is not null; key = cursor.NextKey(EntryKeys, ref seen))
        {
            switch (key)
            {
                case SidecarKeys.Hash:
                    members.Hash = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.Engine:
                    members.Engine = cursor.ReadString(key, orNull: false);
                    break;
                case SidecarKeys.Database:
                    members.Database = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.ServerVersion:
                    members.ServerVersion = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.ResultKind:
                    members.ResultKind = ReadResultKind(cursor);
                    break;
                case SidecarKeys.MatchesTable:
                    members.MatchesTable = ReadTable(cursor);
                    break;
                case SidecarKeys.Plan:
                    members.Plan = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.TableMatch:
                    members.TableMatch = cursor.ReadString(key, orNull: true);
                    break;
                case SidecarKeys.Parameters:
                    ReadParametersOrDefer(cursor, ref members);
                    break;
                default:
                    members.ColumnsKey = cursor.KeySpan;
                    ReadColumnsOrDefer(cursor, ref members);
                    break;
            }
        }

        members.Seen = seen;
    }

    // Reads the parameters where they stand when the entry's engine is known.  Otherwise passes over them and keeps
    // the offset to read them from, which is -1 when there is nothing left to read.
    private static void ReadParametersOrDefer(SidecarCursor cursor, ref EntryMembers members)
    {
        if (members.Engine is { } engine)
        {
            members.Parameters = ReadParameters(cursor, engine);
        }
        else
        {
            members.ParametersAt = cursor.DeferArray(SidecarKeys.Parameters);
        }
    }

    private static void ReadColumnsOrDefer(SidecarCursor cursor, ref EntryMembers members)
    {
        if (members.Engine is { } engine)
        {
            members.Columns = ReadColumns(cursor, engine);
        }
        else
        {
            members.ColumnsAt = cursor.DeferArray(SidecarKeys.Columns);
        }
    }

    // What an entry must hold, and what it must not: columns beside a result that has no rows.  The first mistake
    // found is the entry's error, so the order of the checks is what a user reads.
    private static void CheckMembers(SidecarCursor cursor, in EntryMembers members, TextSpan brace)
    {
        var seen = members.Seen;
        cursor.Require(EntryKeys, seen, brace, SidecarKeys.Hash);
        cursor.Require(EntryKeys, seen, brace, SidecarKeys.Engine);
        cursor.Require(EntryKeys, seen, brace, SidecarKeys.ResultKind);
        cursor.Require(EntryKeys, seen, brace, SidecarKeys.Parameters);
        if (members.ResultKind == SidecarResultKind.Rows)
        {
            cursor.Require(EntryKeys, seen, brace, SidecarKeys.Columns);
        }

        if (members.ResultKind == SidecarResultKind.None && SidecarCursor.Has(EntryKeys, seen, SidecarKeys.Columns))
        {
            cursor.Fail(SidecarErrorKind.ColumnsWithoutRows, members.ColumnsKey);
        }
    }

    // Reads the arrays that were passed over, and comes back to the end of the entry.
    private static void ReadDeferred(SidecarCursor cursor, string engine, ref EntryMembers members)
    {
        var end = cursor.Position;
        if (members.ParametersAt >= 0)
        {
            cursor.Position = members.ParametersAt;
            members.Parameters = ReadParameters(cursor, engine);
        }

        if (members.ColumnsAt >= 0)
        {
            cursor.Position = members.ColumnsAt;
            members.Columns = ReadColumns(cursor, engine);
        }

        cursor.Position = end;
    }

    // What was read of an entry's members.  A value, handed on by reference: an entry costs no object but itself.
    private record struct EntryMembers
    {
        /// <summary>The keys that were found, by the bits <see cref="SidecarCursor.NextKey" /> keeps.</summary>
        public int Seen { get; set; }

        public string? Hash { get; set; }

        public string? Engine { get; set; }

        public string? Database { get; set; }

        public string? ServerVersion { get; set; }

        public SidecarResultKind? ResultKind { get; set; }

        public SidecarTable? MatchesTable { get; set; }

        public string? Plan { get; set; }

        public string? TableMatch { get; set; }

        public EquatableArray<SidecarParameter> Parameters { get; set; }

        public EquatableArray<SidecarColumn>? Columns { get; set; }

        /// <summary>Where the parameters that were passed over start, or -1.</summary>
        public int ParametersAt { get; set; }

        /// <summary>Where the columns that were passed over start, or -1.</summary>
        public int ColumnsAt { get; set; }

        /// <summary>The span of the key of the columns, for the error of columns without rows.</summary>
        public TextSpan ColumnsKey { get; set; }
    }
```

`EntryMembers` must stay a value type that is passed by `ref` or `in` and never boxed: do not make it a class, do not put it in a collection, and do not call a method of `object` on it.  If an analyzer objects to its shape, change the shape and keep those three.

- [ ] **Step 6: Build and run the reader's tests**

Run: `dotnet test --project tests/SqlSource.Tests/SqlSource.Tests.csproj --filter-namespace "SqlSource.Tests.Snapshot.Reading"`
Expected: every test passes but `Measure`, which fails by design.  `SidecarReaderAllocationTests.Read_UsersExample_AllocatesWithinItsBudget`, `SidecarReaderRobustnessTests` and the new test are among those that pass.

- [ ] **Step 7: Measure a read after the change**

Run the command of Step 2 three times.  Write the run with the middle time into the row "after" of the table under "Measurements".  The bytes for a read must not be more than before.  If they are, the value is being copied to the heap somewhere: find it and remove it before going on.

- [ ] **Step 8: Remove the measuring method**

Delete `Measure` and `using System.Diagnostics;` from `tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderAllocationTests.cs`.

Run: `git status --short tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderAllocationTests.cs`
Expected: no output.

- [ ] **Step 9: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

Expected: every step of the summary `passed`.

```bash
git add src/SqlSource/Snapshot/Reading/SidecarReader.cs tests/SqlSource.Tests/Snapshot/Reading/SidecarReaderTests.cs
```

```bash
git commit -m "Read an entry of a sidecar in four steps

The members, the checks, the arrays that were passed over, the entry.  What
was read travels in one value by reference, so a read allocates what it did.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: One record for what belongs to a session

**Files:**
- Modify: `src/SqlSource.Tool/Describing/DatabaseRuns.cs` (the end of `RunDatabaseAsync`, `DescribeAllAsync`, and a new record before `Guard`)
- Test: `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsTests.cs` (unchanged; it holds the change)

**Interfaces:**
- Consumes: nothing of another task.
- Produces: nothing another task uses.  `DatabaseRuns.RunAsync` keeps its signature.

- [ ] **Step 1: Run the tests that hold the method, before the change**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Describing.DatabaseRunsTests"`
Expected: PASS.

- [ ] **Step 2: Add the record**

In `src/SqlSource.Tool/Describing/DatabaseRuns.cs`, directly above the comment of the class `Guard`, add:

```csharp
    // What belongs to one open session: everything a query of its database is described with.  It holds the session
    // and does not own it: RunDatabaseAsync closes it.  The trace is the one without the connection's value.
    private sealed record SessionRun(
        IDescribeSession Session,
        Guard Guard,
        TimeProvider Time,
        Reporter Reporter,
        RunTrace Trace
    );
```

- [ ] **Step 3: Hand it to `DescribeAllAsync`**

In `RunDatabaseAsync`, replace the `try` statement with:

```csharp
        var run = new SessionRun(session, guard, host.Time, reporter, trace);
        try
        {
            await DescribeAllAsync(database, queries, run, cancellationToken);
        }
        catch
        {
            // The first exception is the run's end: a session that fails to close after it adds nothing.
            await Guard.CloseQuietlyAsync(session);
            throw;
        }
```

Replace `DescribeAllAsync` with:

```csharp
    private static async Task DescribeAllAsync(
        PlannedDatabase database,
        List<QueryWork> queries,
        SessionRun run,
        CancellationToken cancellationToken
    )
    {
        var guard = run.Guard;
        var value = guard.Secret;
        var server = guard.Call(() => run.Session.Server);
        RunEvents.Opened(run.Trace, database, server);
        foreach (var query in queries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var planned = query.Planned;
            var describe = new DescribeRequest(
                planned.Query.Name,
                SampleSql.Build(planned.Query),
                planned.Query.Parameters
            );
            var started = run.Time.GetTimestamp();
            var result = await guard.CallAsync(() => run.Session.DescribeAsync(describe, cancellationToken));
            var duration = (long)run.Time.GetElapsedTime(started).TotalMilliseconds;
            if (result.Value is { } description)
            {
                query.Entry = guard.Call(() =>
                    NoSecretIn(EntryBuilder.Build(planned, database.Dialect, database.Name, server, description), value)
                );
                query.State = QueryState.Described;
                RunEvents.Query(run.Trace, query, duration);
            }
            else
            {
                var failure = guard.Call(() => result.Failure ?? throw new InvalidOperationException(Guard.Nothing));
                run.Reporter.Report(Redaction.Of(DescribeErrors.Failure(failure, planned, database, server), value));
                query.State = QueryState.Failed;
                RunEvents.Query(run.Trace, query, error: failure.Descriptor.Id);
            }
        }
    }
```

- [ ] **Step 4: Run the tests that hold it**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Describing.DatabaseRunsTests"`
Expected: PASS.

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Commands.SecretTests"`
Expected: PASS.

- [ ] **Step 5: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add src/SqlSource.Tool/Describing/DatabaseRuns.cs
```

```bash
git commit -m "Give DescribeAllAsync one record for what belongs to a session

The session, its guard, the clock, the reporter and the trace travel together,
and the method takes four parameters in place of eight.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `EvaluateOneAsync` in three steps

**Files:**
- Modify: `src/SqlSource.Tool/Projects/ProjectEvaluator.cs` (`EvaluateOneAsync`, three new methods after it, a record beside `Answer`)
- Test: `tests/SqlSource.Tool.Tests/Projects/ProjectEvaluatorTests.cs`

**Interfaces:**
- Consumes: `Outcome<TValue, TFailure>.Of(TValue)` and `.Failed(TFailure)` of `src/SqlSource.Tool/Outcome.cs`, with `Succeeded`, `Value` and `Failure`.
- Produces: nothing another task uses.  `ProjectEvaluator.EvaluateAsync` keeps its signature.

- [ ] **Step 1: Pin the second evaluation that fails**

Add to `tests/SqlSource.Tool.Tests/Projects/ProjectEvaluatorTests.cs`, after `Evaluate_EvaluationThatFails_IsSqlsrc205WithMSBuildsErrorOutputFirst`:

```csharp
    // The second evaluation of a project with several frameworks fails as the first does, and no target is run.
    [Fact]
    public async Task Evaluate_ProjectWithSeveralFrameworksWhoseSecondEvaluationFails_IsSqlsrc205AndRunsNoTarget()
    {
        var project = new FakeProject
        {
            TargetFrameworks = "net8.0;net10.0",
            Error = "App.csproj(3,5): error MSB4019: no such import",
        };
        _runner.Default = project;
        _runner.BeforeAnswer = (request, _) =>
        {
            if (FakeProcessRunner.PropertyOf(request, "TargetFramework") is not null)
            {
                project.EvaluationExitCode = 1;
            }

            return Task.CompletedTask;
        };

        var evaluation = await EvaluateAsync();

        ShouldBeSqlsrc205(
            evaluation,
            ("reason", "dotnet msbuild ended with the exit code 1"),
            ("msbuild", "App.csproj(3,5): error MSB4019: no such import")
        );
        _runner.Requests.Count.ShouldBe(2);
    }
```

- [ ] **Step 2: Run it, and see it pass before the change**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-method "*Evaluate_ProjectWithSeveralFrameworksWhoseSecondEvaluationFails_IsSqlsrc205AndRunsNoTarget"`
Expected: PASS.

- [ ] **Step 3: Split the method**

In `src/SqlSource.Tool/Projects/ProjectEvaluator.cs`, replace `EvaluateOneAsync` with:

```csharp
    private static async Task<ProjectEvaluation> EvaluateOneAsync(
        ToolHost host,
        string project,
        ImmutableArray<string> solutionSwitches,
        string manifestFile,
        CancellationToken cancellationToken
    )
    {
        // A solution may list a project that is not on the disk.  MSBuild is started in the project's folder, and
        // with the folder gone too nothing could be started, which would read as a machine without the SDK.
        if (!File.Exists(project))
        {
            return Failed(project, "the project file does not exist", new ProcessResult(0, "", ""));
        }

        var asked = await AskAsync(host, project, solutionSwitches, cancellationToken);
        if (!asked.Succeeded)
        {
            return asked.Failure;
        }

        var state = StateOf(asked.Value.Answer, project);
        if (state != ProjectState.UsesSqlSource)
        {
            return new ProjectEvaluation(project, state);
        }

        var target = await RunManifestTargetAsync(
            host,
            project,
            manifestFile,
            [.. asked.Value.FrameworkSwitch, .. solutionSwitches],
            cancellationToken
        );
        return target.Succeeded
            ? await ReadManifestAsync(project, manifestFile, target.Value, cancellationToken)
            : target.Failure;
    }

    // Evaluates the project and runs no target; a project of several frameworks is evaluated again under its first.
    private static async Task<Outcome<Evaluated, ProjectEvaluation>> AskAsync(
        ToolHost host,
        string project,
        ImmutableArray<string> solutionSwitches,
        CancellationToken cancellationToken
    )
    {
        ImmutableArray<string> evaluate =
        [
            "msbuild",
            project,
            "-nologo",
            "-getProperty:SqlSourceImported",
            "-getProperty:TargetFramework",
            "-getProperty:TargetFrameworks",
            "-getProperty:ProjectAssetsFile",
            "-getItem:PackageReference",
            .. solutionSwitches,
        ];

        var result = await RunAsync(host, project, evaluate, cancellationToken);
        if (!TryReadAnswer(result, out var answer, out var reason))
        {
            return Outcome<Evaluated, ProjectEvaluation>.Failed(Failed(project, reason, result));
        }

        // NuGet imports a package's props into a project with several frameworks only under a condition on
        // TargetFramework, so this answer says nothing but which framework is the first.  TD-0026.
        if (answer.TargetFramework.Length != 0 || FirstFramework(answer.TargetFrameworks) is not { } first)
        {
            return Outcome<Evaluated, ProjectEvaluation>.Of(new Evaluated(answer, []));
        }

        ImmutableArray<string> frameworkSwitch = [MSBuildProperty.Switch("TargetFramework", first)];
        result = await RunAsync(host, project, [.. evaluate, .. frameworkSwitch], cancellationToken);
        return TryReadAnswer(result, out answer, out reason)
            ? Outcome<Evaluated, ProjectEvaluation>.Of(new Evaluated(answer, frameworkSwitch))
            : Outcome<Evaluated, ProjectEvaluation>.Failed(Failed(project, reason, result));
    }

    // Runs the target of the package that writes the manifest, to the file the tool named.
    private static async Task<Outcome<ProcessResult, ProjectEvaluation>> RunManifestTargetAsync(
        ToolHost host,
        string project,
        string manifestFile,
        ImmutableArray<string> switches,
        CancellationToken cancellationToken
    )
    {
        var result = await RunAsync(
            host,
            project,
            [
                "msbuild",
                project,
                "-nologo",
                "-t:" + ManifestTarget,
                MSBuildProperty.Switch("SqlSourceManifestFile", manifestFile),
                .. switches,
            ],
            cancellationToken
        );
        return result.ExitCode == 0
            ? Outcome<ProcessResult, ProjectEvaluation>.Of(result)
            : Outcome<ProcessResult, ProjectEvaluation>.Failed(Failed(project, ExitCodeReason(result), result));
    }

    // Reads the manifest the target wrote.  A file that is not there shows what the target printed.
    private static async Task<ProjectEvaluation> ReadManifestAsync(
        string project,
        string manifestFile,
        ProcessResult target,
        CancellationToken cancellationToken
    )
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(manifestFile, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failed(project, $"the target {ManifestTarget} left no file that can be read", target);
        }

        var read = ManifestReader.Read(text);
        return read.Succeeded
            ? new ProjectEvaluation(project, ProjectState.UsesSqlSource, read.Value)
            : new ProjectEvaluation(
                project,
                ProjectState.Failed,
                Failure: ToolDiagnostic.ForFile(ToolDiagnostics.ManifestCannotBeRead, project, read.Failure)
            );
    }
```

At the end of the class, after the record `Answer`, add:

```csharp

    // What the evaluation of a project gave, and the switch of the framework it was evaluated under, if any.
    private readonly record struct Evaluated(Answer Answer, ImmutableArray<string> FrameworkSwitch);
```

The switches of the manifest run keep their order: the framework's, then the solution's.

- [ ] **Step 4: Run the tests that hold it**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Projects.ProjectEvaluatorTests"`
Expected: PASS, the new test among them.

- [ ] **Step 5: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add src/SqlSource.Tool/Projects/ProjectEvaluator.cs tests/SqlSource.Tool.Tests/Projects/ProjectEvaluatorTests.cs
```

```bash
git commit -m "Evaluate a project in three steps

Ask MSBuild about it, run the manifest target, read the manifest.  A step that
may fail gives the evaluation to return as its failure.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `PlanFile` in three steps

**Files:**
- Modify: `src/SqlSource.Tool/Planning/RunPlanner.cs` (`PlanFile`, and three new methods after it)
- Test: `tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs`, `RunPlannerErrorTests.cs` (unchanged; they hold the change)

**Interfaces:**
- Consumes: nothing of another task.
- Produces: nothing another task uses.  Task 5 changes `ReadProject` in the same file and touches none of these methods.

- [ ] **Step 1: Run the tests that hold the method, before the change**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-namespace "SqlSource.Tool.Tests.Planning"`
Expected: PASS.

- [ ] **Step 2: Split the method**

In `src/SqlSource.Tool/Planning/RunPlanner.cs`, replace the comment above `PlanFile` and the method with:

```csharp
    // Plans a claimed file once, under the first project that claims it.  A file with a parse error has no query.
    private static PlannedFile PlanFile(
        FileWork work,
        ImmutableArray<ToolDiagnostic>.Builder errors,
        CancellationToken cancellationToken
    )
    {
        // A run that was cancelled reads no further file, and reports nothing of what it read.
        cancellationToken.ThrowIfCancellationRequested();

        var parsed = Parse(work.Owner, cancellationToken);
        if (parsed.Errors.Count > 0)
        {
            errors.AddRange(parsed.Errors.Select(ToolDiagnostic.From));
            return FileOf(work, parsed.Dialect, PlannedFileState.HasParseErrors, EquatableArray<PlannedQuery>.Empty);
        }

        var state = PlannedFileState.Ready;
        var queries = ImmutableArray.CreateBuilder<PlannedQuery>(parsed.Queries.Count);
        foreach (var query in parsed.Queries)
        {
            queries.Add(PlanQuery(query, work, parsed.Dialect, ref state, errors));
        }

        return FileOf(work, parsed.Dialect, state, new EquatableArray<PlannedQuery>(queries.MoveToImmutable()));
    }

    // Parses a claimed file with the dialect of the project it is planned under.  Comments are never wanted: the
    // hash comes from the SQL without them.
    private static ParsedSqlFile Parse(ClaimedFile owner, CancellationToken cancellationToken)
    {
        var input = FileParseInput.Resolve(
            owner.Metadata,
            owner.ProjectDialect,
            projectKeepsComments: false,
            EquatableArray<string>.Empty
        );
        return SqlFileReader.Read(
            owner.Metadata.File,
            owner.NormalizedPath,
            input.Dialect,
            input.InvalidDialect,
            commentsWanted: false,
            cancellationToken
        );
    }

    // One query with its values, and its errors.  The first query of a file that needs an entry under a dialect
    // that cannot be described changes the state of the file.
    private static PlannedQuery PlanQuery(
        SqlQuery query,
        FileWork work,
        SqlDialect dialect,
        ref PlannedFileState state,
        ImmutableArray<ToolDiagnostic>.Builder errors
    )
    {
        var output = GreatestOutput(query, work);
        if (output == OutputKind.Sql)
        {
            // It needs no entry, and so belongs to no database and has no hash.
            return new PlannedQuery(query, NeedsEntry: false, Database: null, Hash: null);
        }

        // Once for the file, at its first query that needs an entry: one setting mends it.  The parser keeps no
        // position for the marker that set the output.
        if (state == PlannedFileState.Ready && !SqlDescribableDialects.Contains(dialect))
        {
            state = PlannedFileState.NotDescribable;
            errors.Add(
                ToolDiagnostic.At(
                    ToolDiagnostics.OutputNeedsDescribableDialect,
                    query.NameLocation,
                    SettingValue.NameOf(output),
                    SqlDialectName.Canonical(dialect)
                )
            );
        }

        var problems = QueryProblems.None;
        // An empty default is one: the query is described with nothing in the token's place.
        foreach (
            var token in query.Tokens.Where(static token => token.Default is null).Select(static token => token.Name)
        )
        {
            problems |= QueryProblems.TokenWithoutDefault;
            errors.Add(
                ToolDiagnostic
                    .At(ToolDiagnostics.TokenHasNoDefault, query.NameLocation, token, SettingValue.NameOf(output))
                    .WithLines(HelpForToken(token))
            );
        }

        var owner = work.Owner;
        return new PlannedQuery(
            query,
            NeedsEntry: true,
            query.Markers.Database
                ?? owner.Metadata.Settings?.Level.Database
                ?? owner.Property.Database
                ?? SqlDialectName.Canonical(dialect),
            SqlQueryHash.Compute(dialect, query.Segments, query.Tokens, query.Parameters),
            problems
        );
    }

    // The planned file of a work, under its owner.
    private static PlannedFile FileOf(
        FileWork work,
        SqlDialect dialect,
        PlannedFileState state,
        EquatableArray<PlannedQuery> queries
    ) =>
        new(
            work.Owner.Metadata.File.Path,
            work.Owner.NormalizedPath,
            work.Owner.ProjectPath,
            dialect,
            state,
            queries,
            ClaimsOf(work)
        );
```

`./format.sh` decides how the `foreach` is wrapped; take what it gives.

- [ ] **Step 3: Run the tests that hold it**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-namespace "SqlSource.Tool.Tests.Planning"`
Expected: PASS.  `GeneratorParityTests`, `Plan_QueryThatNeedsAnEntryUnderADialectThatCannotBeDescribed_IsSqlsrc209OnceForItsFile` and `Plan_TokenWithoutADefaultInAFileThatCannotBeDescribed_IsReportedBesideSqlsrc209` are among them.

- [ ] **Step 4: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add src/SqlSource.Tool/Planning/RunPlanner.cs
```

```bash
git commit -m "Plan a file in three steps

Parse it, plan each query, build the planned file, which both ends of the
method now make in one place.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `ReadProject` in four steps

**Files:**
- Modify: `src/SqlSource.Tool/Planning/RunPlanner.cs` (`ReadProject`, two new methods after `IsRead`, two new classes before `ClaimedFile`)
- Test: `tests/SqlSource.Tool.Tests/Planning/RunPlannerErrorTests.cs`, `tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs`

**Interfaces:**
- Consumes: nothing of another task.  `ClaimedFile` keeps its members: `PlanFile` and `GreatestOutput` read them.
- Produces: nothing another task uses.

- [ ] **Step 1: Pin the order of a project's errors**

Add to `tests/SqlSource.Tool.Tests/Planning/RunPlannerErrorTests.cs`, after `Plan_AttributeArgumentThatIsNoLiteral_IsSqlsrc208AndItsTypeClaimsNothing`:

```csharp
    // The errors of a project come in one order, each value once though the property and a file's metadata both
    // give it, and before the errors of its files: the file is read as ansi, which cannot be described.
    [Fact]
    public void Plan_ProjectWithEveryKindOfError_ReportsThemInOrderAndEachValueOnce()
    {
        var project = Project(dialect: "nosuch");
        project.Properties["SqlSourceOutput"] = "everything";
        _ = project.AddSource("Other.cs", Type("Other", "Path = Folder"));
        _ = project.AddSql("Q/One.sql", "SELECT 1;", ("SqlSourceDialect", "nosuch"), ("SqlSourceOutput", "everything"));

        var result = Plan(project);

        result
            .Errors.Select(error => error.Descriptor.Id)
            .ShouldBe(["SQLSRC208", "SQLSRC011", "SQLSRC014", "SQLSRC209"]);
    }
```

- [ ] **Step 2: Pin the order of a file's claims**

Add to `tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs`, after `Plan_TwoTypesThatClaimOneFile_NeedWhatEitherNeeds`:

```csharp
    [Fact]
    public void Plan_TwoTypesThatClaimOneFile_HasBothClaimsInTheOrderOfTheirSourceFiles()
    {
        var project = Postgres();
        var first = project.AddSource("A.cs", Type("A", "Path = \"Q\""));
        var second = project.AddSource("B.cs", Type("B", "Path = \"Q/One.sql\""));
        _ = project.AddSql("Q/One.sql", "SELECT 1;");

        var file = Plan(project).Plan.Files.ShouldHaveSingleItem();

        file.Claims.Select(claim => claim.SourcePath).ShouldBe([first, second]);
    }
```

- [ ] **Step 3: Run both, and see them pass before the change**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-method "*Plan_ProjectWithEveryKindOfError_ReportsThemInOrderAndEachValueOnce"`
Expected: PASS.

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-method "*Plan_TwoTypesThatClaimOneFile_HasBothClaimsInTheOrderOfTheirSourceFiles"`
Expected: PASS.

- [ ] **Step 4: Split the method**

In `src/SqlSource.Tool/Planning/RunPlanner.cs`, replace the comment above `ReadProject` and the method with:

```csharp
    // The claimed files of one project, in the order of their paths, each with the settings of this project.  The
    // errors of the project are added: SQLSRC208, then SQLSRC011, then SQLSRC014.
    private static List<ClaimedFile> ReadProject(
        ProjectManifest manifest,
        ImmutableArray<ToolDiagnostic>.Builder errors,
        CancellationToken cancellationToken
    )
    {
        var claims = AttributeReader.Read(manifest, cancellationToken);
        errors.AddRange(claims.Errors);

        var options = ManifestOptions.ForProject(manifest);
        var dialect = DialectSetting.ReadProperty(options);
        // The keys of a manifest: they have the database, which the keys of the compiler do not.
        var settings = ProjectSettings.Read(options, MSBuildSettings.ManifestProperty);
        var invalid = new InvalidValues();
        invalid.Add(dialect);
        invalid.Add(settings.Invalid);

        var listed = ListedFiles.Read(manifest);
        var claimsOf = ClaimsByFile(claims.Claims, listed);

        // The metadata of a file that no type claims is not read, as nothing else of such a file is.
        var claimed = new List<ClaimedFile>(claimsOf.Count);
        foreach (var path in listed.Paths)
        {
            if (!claimsOf.TryGetValue(path, out var fileClaims))
            {
                continue;
            }

            claimed.Add(
                new ClaimedFile(
                    manifest.ProjectPath,
                    path,
                    ReadMetadata(listed[path], invalid),
                    dialect,
                    settings.Level,
                    fileClaims.Levels,
                    fileClaims.Types
                )
            );
        }

        invalid.AddTo(errors, manifest.ProjectPath);
        return claimed;
    }
```

Directly after `IsRead`, add:

```csharp

    // The claims of each claimed file, by its path.  What a claim of a file says is its Output, and nothing else.
    private static Dictionary<string, FileClaims> ClaimsByFile(ImmutableArray<TypeClaim> claims, ListedFiles listed)
    {
        var byFile = new Dictionary<string, FileClaims>(SqlPath.Comparer);
        foreach (var claim in claims)
        {
            var level = claim.Output is { } output ? new SettingsLevel { Output = output } : SettingsLevel.None;
            foreach (var path in listed.ClaimedBy(claim))
            {
                if (!byFile.TryGetValue(path, out var fileClaims))
                {
                    fileClaims = new FileClaims();
                    byFile.Add(path, fileClaims);
                }

                fileClaims.Levels.Add(level);
                fileClaims.Types.Add(claim);
            }
        }

        return byFile;
    }

    // The metadata of a claimed file's item.  What is not valid in it is kept for the errors of the project.
    private static FileMetadata ReadMetadata(ManifestFile file, InvalidValues invalid)
    {
        var metadata = FileMetadata.Read(
            new SqlFileText(file.Path),
            ManifestOptions.ForFile(file),
            MSBuildSettings.ManifestMetadata
        );
        invalid.Add(metadata.Dialect);
        if (metadata.Settings is { } settings)
        {
            invalid.Add(settings.Invalid);
        }

        return metadata;
    }
```

Directly above the comment `// One claimed file as one project sees it.`, add:

```csharp
    // The claims of one file in one project: what each says, and the type that says it, in the order they came.
    private sealed class FileClaims
    {
        public List<SettingsLevel> Levels { get; } = [];

        public List<TypeClaim> Types { get; } = [];
    }

    // What is not valid in one project, each value once, as the generator reports them: the dialects in ordinal
    // order, then the settings the tool reads, by name and then by value.
    private sealed class InvalidValues
    {
        private readonly SortedSet<string> _dialects = new(StringComparer.Ordinal);

        private readonly HashSet<InvalidSetting> _settings = [];

        public void Add(DialectSetting dialect)
        {
            if (dialect.InvalidValue is { } value)
            {
                _ = _dialects.Add(value);
            }
        }

        public void Add(IEnumerable<InvalidSetting> settings) => _settings.UnionWith(settings.Where(IsRead));

        public void AddTo(ImmutableArray<ToolDiagnostic>.Builder errors, string projectPath)
        {
            foreach (var value in _dialects)
            {
                errors.Add(ToolDiagnostic.AtFile(SqlDiagnostics.InvalidDialect, projectPath, value));
            }

            foreach (
                var setting in _settings
                    .OrderBy(static setting => setting.Name, StringComparer.Ordinal)
                    .ThenBy(static setting => setting.Value, StringComparer.Ordinal)
            )
            {
                errors.Add(
                    ToolDiagnostic.AtFile(SqlDiagnostics.InvalidSettingValue, projectPath, setting.Value, setting.Name)
                );
            }
        }
    }

```

No `using` is added: `ManifestFile` is of `SqlSource.Tool.Projects`, and `FileMetadata`, `DialectSetting` and `InvalidSetting` of `SqlSource.Generation.MSBuild`, which the file names already.

- [ ] **Step 5: Run the tests that hold it**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-namespace "SqlSource.Tool.Tests.Planning"`
Expected: PASS, the two new tests among them, with `Plan_MetadataThatIsNoDialect_IsSqlsrc011OnceForEachValueOfAClaimedFile`, `Plan_MetadataThatIsNotValid_IsSqlsrc014ForAClaimedFileAlone` and `GeneratorParityTests`.

- [ ] **Step 6: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add src/SqlSource.Tool/Planning/RunPlanner.cs tests/SqlSource.Tool.Tests/Planning/RunPlannerErrorTests.cs tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs
```

```bash
git commit -m "Read a project in four steps

Its own settings, the claims of each file in one map, the metadata of each
claimed file, and what is not valid, which one collector reports in order.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `Cli.RunAsync` in four steps

**Files:**
- Modify: `src/SqlSource.Tool/Cli.cs` (`RunAsync`, `End`, four new methods between them, two new types at the end of the class)
- Modify: `src/SqlSource.Tool/AGENTS.md`
- Modify: `docs/tech-debt/TD-0036-two-small-gaps-of-check-and-the-log.md`
- Test: `tests/SqlSource.Tool.Tests/Commands/CliTests.cs`

**Interfaces:**
- Consumes: `DescribeCommand.CheckUsage(Usage)`, `DescribeCommand.ReadsConnectionsAs(Usage, ParseResult)`, `DescribeCommand.Runs(ParseResult)`, `DescribeCommand.LogPathOf(ParseResult, Func<string, string?>)`, `DescribeCommand.IsVerbose(ParseResult)`, `DescribeCommand.Bind(ParseResult, ToolHost, Reporter, Action<LogValue>)`, `RunLogFile.Open(string, string, TimeProvider) : Outcome<RunLogFile, ToolDiagnostic>`, all as they are.
- Produces: `Cli.RunAsync(string[] args, ToolHost host, CancellationToken cancellationToken) : Task<int>`, unchanged.

- [ ] **Step 1: Pin which reporter an exception goes to**

Add to `tests/SqlSource.Tool.Tests/Commands/CliTests.cs`, after `Run_SummaryThatCannotBeWritten_StillEndsTheLogWithTheCounts`:

```csharp
    // The error of an exception belongs in the log: the run's reporter, which has the trace, reports it, and not
    // the one the run started with.
    [Fact]
    public async Task Run_SummaryThatCannotBeWrittenWithALog_HasTheErrorAsAnEventBeforeRunEnd()
    {
        using var run = new CliRun { Out = new FailingWriter("the pipe is closed") };
        run.Describers.Add(new FakeDescriber());
        var project = Plans.Postgres(run.Folder, directory: "").AnsweredBy(run.Processes);
        run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = DescribeScene.Connection;
        _ = project.AddSql("Users.sql", "-- name: GetUser\nSELECT 1;\n");

        var result = await run.RunAsync("describe", "--log", "run.log");

        result.ExitCode.ShouldBe(1);
        var events = LogReader.Read(run.Folder.PathOf("run.log"));
        events[^2].GetProperty("event").GetString().ShouldBe("diagnostic");
        events[^2].GetProperty("id").GetString().ShouldBe("SQLSRC200");
        events[^1].GetProperty("event").GetString().ShouldBe("run.end");
    }
```

- [ ] **Step 2: Run it, and see it pass before the change**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-method "*Run_SummaryThatCannotBeWrittenWithALog_HasTheErrorAsAnEventBeforeRunEnd"`
Expected: PASS.

- [ ] **Step 3: Split `RunAsync`**

In `src/SqlSource.Tool/Cli.cs`, replace `RunAsync`, with its documentation comment, and `End`, with its comment, by:

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
        var state = new RunState(host);
        try
        {
            // The command first, and the reporter only after it: the command replaces the reporter.
            var commandExitCode = await RunCommandAsync(args, state, cancellationToken);
            var exitCode = ExitCodeOf(state.Reporter, commandExitCode);
            if (state.MayPoint && !state.Cancelled && state.Reporter.Count > state.Reporter.DifferenceCount)
            {
                await host.Error.WriteLineAsync(RunAgainLine);
            }

            return state.Log is null ? exitCode : End(state.Log, exitCode, state);
        }
        finally
        {
            // A path that leaves the method by an exception of its own still closes the file.
            state.Log?.Dispose();
        }
    }

    // The steps of a run, to the end of its command, and the three things that end one early.  It gives what the
    // command gave, 1 for a run that did not start or was cancelled, and 0 for one an exception ended: the exit
    // code of that one comes from the reporter.
    private static async Task<int> RunCommandAsync(string[] args, RunState state, CancellationToken cancellationToken)
    {
        try
        {
            // A run that was cancelled prints nothing more, whatever it was asked to do.
            cancellationToken.ThrowIfCancellationRequested();

            if (await ReadCommandLineAsync(args, state.Host) is not { } commandLine || !OpenLog(commandLine, args, state))
            {
                return 1;
            }

            return await InvokeAsync(commandLine, state, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            state.Cancelled = true;
            return 1;
        }
        catch (DescriberFaultException fault)
        {
            // The error of an exception that a describer threw, already without the connection's value.
            state.Reporter.Report(fault.Diagnostic);
            return 0;
        }
        catch (Exception exception)
        {
            state.Reporter.Report(UnexpectedFailure.Of(exception, state.Host.GetEnvironmentVariable));
            return 0;
        }
    }

    // Reads the command line with both readers.  Null when it is wrong, with its lines written: they have no id
    // and do not go through the reporter.
    private static async Task<ParsedCommandLine?> ReadCommandLineAsync(string[] args, ToolHost host)
    {
        var root = BuildCommands();

        var usage = UsageCheck.Check(root, args);
        var messages = usage.Messages.Count > 0 ? usage.Messages : DescribeCommand.CheckUsage(usage);
        if (messages.Count > 0)
        {
            foreach (var line in messages)
            {
                await host.Error.WriteLineAsync(line);
            }

            return null;
        }

        var parsed = root.Parse(args, Parser);
        if (
            parsed.Errors.Count > 0
            || !usage.IsReadTheSameBy(parsed)
            || !DescribeCommand.ReadsConnectionsAs(usage, parsed)
        )
        {
            // UsageCheck lets nothing through that System.CommandLine rejects or reads another way, as far as
            // the tests know.  If something is, nothing runs, and System.CommandLine's message is not written:
            // it may repeat a token.  The values of --connection are held too: the log's arguments is
            // redacted from one reading and the describer is handed the other.
            await host.Error.WriteLineAsync("sqlsource: the command line is not valid");
            return null;
        }

        // Whether the command line runs "describe", once.
        return new ParsedCommandLine(usage, parsed, DescribeCommand.Runs(parsed));
    }

    // Opens the log the run was given, and writes run.start: after the command line was read, so that a wrong one
    // is never logged, and before anything runs.  False when the log cannot be opened, which is reported: the run
    // does not start.
    private static bool OpenLog(ParsedCommandLine commandLine, string[] args, RunState state)
    {
        var host = state.Host;
        var logPath = DescribeCommand.LogPathOf(commandLine.Parsed, host.GetEnvironmentVariable);
        state.MayPoint = commandLine.RunsDescribe && logPath is null;
        if (logPath is null)
        {
            return true;
        }

        var opened = RunLogFile.Open(logPath, host.WorkingDirectory, host.Time);
        if (!opened.Succeeded)
        {
            state.Reporter.Report(opened.Failure);
            return false;
        }

        state.Log = opened.Value;
        state.Log.Write(
            "run.start",
            new("toolVersion", PackageVersion.Prefix),
            new("formatVersion", SidecarFormat.Version),
            new("runtime", RuntimeInformation.FrameworkDescription),
            new("os", RuntimeInformation.OSDescription),
            new("arguments", LoggedArguments.Of(commandLine.Usage, args))
        );
        return true;
    }

    // Gives "describe" its action with the reporter of the run, and invokes the command.
    private static async Task<int> InvokeAsync(
        ParsedCommandLine commandLine,
        RunState state,
        CancellationToken cancellationToken
    )
    {
        var host = state.Host;
        var parsed = commandLine.Parsed;
        if (commandLine.RunsDescribe)
        {
            // What the run says of itself: to the log, when it has one, and to the person watching under
            // --verbose.
            IRunLog events = state.Log is { } log ? log : NullRunLog.Instance;

            // The reporter of the run: from here on every error is also an event of the log.  The command
            // gets its action only now, so that it cannot run with another.
            state.Reporter = new Reporter(
                host.Error,
                new RunTrace(events, DescribeCommand.IsVerbose(parsed) ? host.Out : null)
            );
            DescribeCommand.Bind(parsed, host, state.Reporter, counts => state.Databases = counts);
        }

        var invocation = new InvocationConfiguration
        {
            Output = host.Out,
            Error = host.Error,
            // An exception comes to the catch of RunCommandAsync.
            EnableDefaultExceptionHandler = false,
            // Cli.Run listens for Ctrl+C itself.
            ProcessTerminationTimeout = null,
        };
        return await parsed.InvokeAsync(invocation, cancellationToken);
    }

    // The last event, with the exit code of the process, and the file is closed.  A log that stopped being
    // writable while the run went on did not end the run: it is an error now, once, and the exit code is 1.
    private static int End(RunLogFile log, int exitCode, RunState state)
    {
        log.Write(
            "run.end",
            new("exitCode", exitCode),
            new("durationMs", (long)state.Host.Time.GetElapsedTime(state.Started).TotalMilliseconds),
            new("databases", state.Databases)
        );
        log.Dispose();
        if (log.Failure is not { } failure)
        {
            return exitCode;
        }

        state.Reporter.Report(
            ToolDiagnostic.ForFile(ToolDiagnostics.FileCannotBeChanged, log.Path, "written", failure.Message)
        );
        return 1;
    }
```

At the end of the class, after `VersionAction`, add:

```csharp

    // A command line that both readers accept, as each read it.
    private sealed record ParsedCommandLine(Usage Usage, ParseResult Parsed, bool RunsDescribe);

    // What the steps of one run share.
    private sealed class RunState(ToolHost host)
    {
        public ToolHost Host => host;

        public long Started { get; } = host.Time.GetTimestamp();

        // Until the run has its trace an error has the console alone: a log that cannot be opened, or an
        // exception.  Neither is followed by an error of the run, so no count is split between two reporters.
        // InvokeAsync replaces it, so a step reads it here when it reports, and keeps it in no local.
        public Reporter Reporter { get; set; } = new(host.Error);

        // The log of the run, once it is open.  RunAsync closes it.
        public RunLogFile? Log { get; set; }

        // The counts of each selected database, as run.end holds them.
        public LogValue Databases { get; set; } = LogValue.ListOf([]);

        // Whether the line that ends a failed run has a reader: "describe" runs, and no log was asked for.
        public bool MayPoint { get; set; }

        public bool Cancelled { get; set; }
    }
```

Three things to hold to while doing it:

- `ExitCodeOf(state.Reporter, await RunCommandAsync(...))` is wrong: C# reads `state.Reporter` before the `await`, and gets the reporter the run started with.  Keep the two statements.
- The log is written to through `state.Log` only after `OpenLog` set it, and closed in `RunAsync` only.
- If an analyzer wants `RunState` to be disposable because it holds the log, do not suppress it and do not dispose the log twice: stop and tell the user.

- [ ] **Step 4: Run the tests that hold it**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-namespace "SqlSource.Tool.Tests.Commands"`
Expected: PASS, the new test among them.

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-namespace "SqlSource.Tool.Tests.Logging"`
Expected: PASS.  `Run_LogThatCannotBeWritten_IsSqlsrc218AndTheRunDoesNotStart`, `Run_WrongCommandLineWithLog_WritesNoFile`, `Run_CancelledWithLog_EndsWithRunEndAndOne` and `Run_LogThatStopsBeingWritable_FinishesTheRunAndIsOneSqlsrc218` are among them.

- [ ] **Step 5: Name the steps where a document says what one of them does**

In `src/SqlSource.Tool/AGENTS.md`:

- In the bullet **Everything from outside comes through `ToolHost`**, ``which `Cli.RunAsync` reads from `RuntimeInformation` `` becomes ``which `Cli.OpenLog` reads from `RuntimeInformation` ``.
- In the bullet **A result that may fail has one shape where files meet**, ``and `Cli.RunAsync` what `RunLogFile.Open` did`` becomes ``and `Cli.OpenLog` what `RunLogFile.Open` did``.
- In the bullet **`Logging/` is what a run says of itself**, after the sentence that ends ``an error before that has a reporter with no trace.``, add: ``Its steps, `ReadCommandLineAsync`, `OpenLog` and `InvokeAsync`, run in that order inside the one `try` of `RunCommandAsync` and share a `RunState`.  `InvokeAsync` replaces `RunState.Reporter`, so whatever reports after a step reads the reporter from the state then, and never from a local made before the step.``
- In the bullet **An exception is `SQLSRC200`**, ``caught in `Cli.RunAsync`,`` becomes ``caught in `Cli.RunCommandAsync`, the one `try` around the steps of `Cli.RunAsync`,``; ``which `Cli.RunAsync` reports`` becomes ``which `Cli.RunCommandAsync` reports``; and ``between `Describing/DatabaseRuns.cs` and `Cli.RunAsync` `` becomes ``between `Describing/DatabaseRuns.cs` and `Cli.RunCommandAsync` ``.
- In the bullet **A command's own rules for its values are in the command**, `` `Cli.RunAsync` calls it when `UsageCheck` found nothing`` becomes `` `Cli.ReadCommandLineAsync` calls it when `UsageCheck` found nothing``.

In `docs/tech-debt/TD-0036-two-small-gaps-of-check-and-the-log.md`, in item 1, ``[`Cli.RunAsync`](../../src/SqlSource.Tool/Cli.cs) writes `run.start` `` becomes ``[`Cli.OpenLog`](../../src/SqlSource.Tool/Cli.cs) writes `run.start` ``.

Then check that nothing else was made untrue:

```bash
grep -rn "Cli\.RunAsync" --include='*.md' src docs/tech-debt
```

Expected: every line that is left says something `RunAsync` still does itself or through its steps: it takes a test's host, it opens and closes the log, it makes the run's reporter, it writes `run.start` and `run.end`.  The line of TD-0038 is deleted in Task 7.

- [ ] **Step 6: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add src/SqlSource.Tool/Cli.cs src/SqlSource.Tool/AGENTS.md docs/tech-debt/TD-0036-two-small-gaps-of-check-and-the-log.md tests/SqlSource.Tool.Tests/Commands/CliTests.cs
```

```bash
git commit -m "Run the command in four steps, with no try inside another

Read the command line, open the log, invoke, end the run.  The steps share
one state, and the catches stand around the first three alone.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Delete TD-0038, open the pull request

**Files:**
- Delete: `docs/tech-debt/TD-0038-duplicated-and-uneven-patterns-in-the-generator-and-the-tool.md`
- Modify: `docs/tech-debt/README.md` (the row of TD-0038 goes)
- Modify: `docs/tech-debt/TD-0042-allocations-of-a-parse-that-no-budget-sees.md`
- Add: `docs/superpowers/plans/2026-10-11-td-0038-pr4-long-methods.md` (this file)

**Interfaces:**
- Consumes: the table under "Measurements", filled in Task 1.
- Produces: the pull request.

- [ ] **Step 1: Run everything**

Run: `dotnet test --solution SqlSource.slnx`
Expected: all pass.

- [ ] **Step 2: Read the six methods once more**

Open each of `SidecarReader.ReadEntry`, `DatabaseRuns.DescribeAllAsync`, `ProjectEvaluator.EvaluateOneAsync`, `RunPlanner.PlanFile`, `RunPlanner.ReadProject` and `Cli.RunAsync`.  Each must now do one thing and call its steps, and no method may hold a `try` inside another.  If one still does several things, it is not resolved: stop and tell the user before deleting the item.

- [ ] **Step 3: Delete the item and its row**

```bash
git rm docs/tech-debt/TD-0038-duplicated-and-uneven-patterns-in-the-generator-and-the-tool.md
```

In `docs/tech-debt/README.md`, delete the row of the table that starts `| [TD-0038](`.  `Next id` does not change: an id is never reused.

- [ ] **Step 4: Reword the mention in TD-0042**

In `docs/tech-debt/TD-0042-allocations-of-a-parse-that-no-budget-sees.md`, under **Problem**:

- `The review of the third pull request of TD-0038 found three things about what a parse allocates.` becomes `A review of `Parsing/` on 2026-10-11 found three things about what a parse allocates.`
- `That pull request added the scope's `SqlScopeKind`, a field of 8 bytes in every scope, and the measurement showed it once for a file.` becomes `The change under review added the scope's `SqlScopeKind`, a field of 8 bytes in every scope, and its measurement showed it once for a file.`

- [ ] **Step 5: Check that the id is gone from all but the record of the work**

```bash
grep -rn "TD-0038" . --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj --exclude-dir=.superpowers | grep -v "docs/superpowers/"
```

Expected: no output.  The spec and the four plans under `docs/superpowers` keep naming the id, as that folder's `AGENTS.md` says: they are the intent at the time, and are not rewritten.

- [ ] **Step 6: Validate and commit the documents**

```bash
./pre-commit-validation.sh
```

```bash
git add -A docs
```

```bash
git commit -m "Delete TD-0038, which is resolved, and add the plan

The long methods of the tool and of the sidecar reader were its last part.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 7: Check the version**

```bash
git fetch --tags origin
```

```bash
git tag --list 'v*' --sort=-v:refname | head -n 1
```

Expected: no output, as on 2026-10-11: the repository has no release tag, so there is nothing to compare.  If a tag is printed, compare it with `VersionPrefix` in `Directory.Build.props`, and ask the user for the new version if `VersionPrefix` is not greater.

- [ ] **Step 8: Ask the user, then push and open the pull request**

Pushing and opening a pull request are seen outside this machine: ask the user first.  Then:

```bash
git push -u origin claude/td-0038-long-methods
```

Open the pull request against `main` with `gh pr create`.  Its description says: what TD-0038 is and that this is the last of four pull requests of the design; the six methods, one line each, with the steps each now has; that nothing a user sees changes and no existing test was edited; the five tests added and what each pins; the time and the allocation of a sidecar read before and after, from the table under "Measurements", with how they were measured (a `Stopwatch` loop over a `Release` build, 10,000 reads to warm up and 20,000 timed, the middle of three runs); the points the plan settled beyond the design, from the top of this file; and that TD-0038 is deleted.  End it with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.

Then follow the repository's rules for a pull request: reply in its thread to every CodeRabbit finding, with what changed or with why not.

---

## Measurements

A read of the first example of the sidecar format, the file of `SidecarReaderAllocationTests`, `Release`, the middle of three runs, 10,000 reads to warm up and 20,000 timed.  Task 1 fills both rows.

| | Time for a read | Bytes for a character | Bytes for a read |
|----|----|----|----|
| before | 32.31 us | 1.385 | 15,648 |
| after | 33.56 us | 1.385 | 15,648 |

A read of this file, 11,301 characters, allocates the same bytes as before, to the byte.  The times differ by less than the machine drifts between two sets of runs: timed in turn, the old reader and the new, the old gave 35.28 and 34.84 us and the new 34.64 and 35.00 us.
