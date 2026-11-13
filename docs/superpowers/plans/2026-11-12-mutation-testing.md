# Mutation Testing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mutation testing with Stryker.NET for both source projects, run by one script, with a comparison of two
reports that a later cleanup phase uses as its guard.

**Architecture:** Stryker runs from the folder of the project it mutates, with a `stryker-config.json` beside each.
`tools/mutation-test.sh` wraps the run and its scope.  A small console project, `tools/MutationReport`, reads
Stryker's JSON report and compares two of them; the skill of pull request 2 will add its condensing to the same
project.

**Tech Stack:** `dotnet-stryker` 5.1.0 with its `mtp` runner, bash, .NET 10, `System.Text.Json`, xUnit v3, Shouldly.

**Spec:** `docs/superpowers/specs/2026-11-12-test-quality-review-design.md`, "Part 1: mutation testing".  This plan is
pull request 1 of the spec's three.  Pull request 2, the skill, gets its own plan once this one has merged, since it
builds on the run times this one measures and on the report reader this one writes.  Pull request 3 is a run of the
skill and has no plan.

## Global Constraints

- `dotnet-stryker` is pinned at `5.1.0` with `"rollForward": false`.  It is already in `dotnet-tools.json`,
  uncommitted, on this branch.
- Stryker runs with `"test-runner": "mtp"`, and never from the repository root, which it refuses.
- Two mutation runs never share a checkout at the same time: Stryker builds in the working tree.
- Reports go under `artifacts/`, which git ignores, and are never committed.
- No file of `src/` changes but the two `stryker-config.json` files.
- The script is not a check: it is added to neither `pre-commit-validation.sh` nor `.github/workflows/build.yml`.
- Stryker's limits are a note in `tools/README.md`, not an item of `docs/tech-debt`.
- A project's full run fits in about ten hours.  If tuning cannot get it there, stop and ask the owner.
- No mutation score threshold fails a run, and no CI or nightly job is added.
- Lines are at most 120 characters, JSON is indented by 2 spaces, shell by 4.
- `tests/AGENTS.md` is 8,186 of its 8,192 bytes: add nothing to it.  A bullet of any `AGENTS.md` is at most 80 words.
- Test classes are `sealed`, assert with Shouldly and name tests `Method_Scenario_Outcome`.
- `RestorePackagesWithLockFile` is on and CI restores in locked mode: commit every `packages.lock.json` that a
  restore writes or changes.
- Every commit passes `./pre-commit-validation.sh`, run after the last edit.  Before a push,
  `./pre-commit-validation.sh --full` passes on the tree as pushed.

## Review Focus

1. **Two runs in one checkout.**  A second `mutation-test.sh` run while one is going is refused with a message that
   names the lock, and does not start Stryker.  Pinned in Task 5, Step 4.
2. **Reports from two checkouts.**  A "before" report made in one worktree and an "after" made in another compare
   mutant for mutant, by the file's path from the project root.  Pinned in Task 2
   (`Read_FileUnderTheProjectRoot_IsKeyedByItsPathFromTheRoot`).
3. **An "after" run with a narrower scope.**  A mutant killed before and `Ignored` or absent after has not been
   shown to be still killed: the comparison fails and says so, and does not pass in silence.  Pinned in Task 3.
4. **A run that fails or is interrupted.**  Stryker's failure is the script's exit code, and the lock is released on
   any exit, Ctrl-C included.  Pinned in Task 5, Step 5.
5. **A report of hundreds of megabytes.**  The reader streams the file and never holds `coveredBy`, `killedBy` or
   `source`.  Pinned in Task 6, Step 3, on a full report.

---

### Task 1: Stryker runs in both source projects

**Files:**
- Modify: `dotnet-tools.json` (already changed; commit it)
- Modify: `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
- Create: `src/SqlSource/stryker-config.json`
- Create: `src/SqlSource.Tool/stryker-config.json`
- Modify: `.gitignore`
- Modify: `tests/README.md` (the graph of "How they reference each other" and the list under it)

**Interfaces:**
- Produces: a bare `dotnet stryker` works in `src/SqlSource` and in `src/SqlSource.Tool`.  Its JSON report is
  `<output>/reports/mutation-report.json`.

- [ ] **Step 1: Reference the tool from its test project**

In `tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`, before the reference to `SqlSource.csproj`:

```xml
<!-- Stryker finds the project it mutates among a test project's own references (tools/README.md). -->
<ProjectReference Include="../../src/SqlSource.Tool/SqlSource.Tool.csproj" />
```

- [ ] **Step 2: Write `src/SqlSource/stryker-config.json`**

```json
{
  "stryker-config": {
    "project": "SqlSource.csproj",
    "test-projects": ["../../tests/SqlSource.Tests/SqlSource.Tests.csproj"],
    "test-runner": "mtp",
    "coverage-analysis": "perTest",
    "reporters": ["json", "html", "progress"]
  }
}
```

- [ ] **Step 3: Write `src/SqlSource.Tool/stryker-config.json`**

The same, with `"project": "SqlSource.Tool.csproj"` and
`"test-projects": ["../../tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj"]`.

- [ ] **Step 4: Ignore a bare run's output**

Add `StrykerOutput/` to `.gitignore`, under a comment that says a `dotnet stryker` without `--output` writes it.

- [ ] **Step 5: Run each project on a small scope**

```bash
cd src/SqlSource && dotnet stryker --skip-version-check --output ../../artifacts/mutation/trial-generator \
    --mutate '**/Parsing/SqlDialectName.cs' --test-case-filter 'FullyQualifiedName~SqlSource.Tests.Parsing'
cd ../SqlSource.Tool && dotnet stryker --skip-version-check --output ../../artifacts/mutation/trial-tool \
    --mutate '**/DotEnv/DotEnvParser.cs' --test-case-filter 'FullyQualifiedName~SqlSource.Tool.Tests.DotEnv'
```

Expected: both exit 0.  The generator's run tests about 51 mutants and kills about 48; the tool's tests about 115 and
kills about 100.  Each writes `reports/mutation-report.json`.  `git status --short` shows no file that Stryker left.
Keep both reports: Task 4 reads them.

- [ ] **Step 6: Update `tests/README.md`**

Add `ToolTests --> Tool` to the graph, and to the `tests/SqlSource.Tool.Tests` line under it a sentence that it
references `src/SqlSource.Tool` itself because Stryker looks for the project it mutates among a test project's own
references.

- [ ] **Step 7: Validate and commit**

Run: `./pre-commit-validation.sh`
Expected: every step passes or is named as skipped.

```bash
git add dotnet-tools.json .gitignore src/SqlSource/stryker-config.json src/SqlSource.Tool/stryker-config.json \
    tests/SqlSource.Tool.Tests tests/README.md docs/superpowers
git commit -m "build: add Stryker.NET and its configuration for both source projects"
```

---

### Task 2: Read a mutation report

**Files:**
- Create: `tools/MutationReport/MutationReport.csproj`
- Create: `tools/MutationReport/Mutant.cs` (`MutantStatus`, `MutantKey`, `Mutant`)
- Create: `tools/MutationReport/ReportReader.cs`
- Create: `tools/MutationReport/Program.cs` (a `return 0;` until Task 4)
- Modify: `SqlSource.slnx` (a `/tools/` folder with the project)
- Modify: `tests/SqlSource.Repository.Tests/SqlSource.Repository.Tests.csproj` (reference the project)
- Create: `tests/SqlSource.Repository.Tests/MutationReport/Reports.cs` (test helper)
- Test: `tests/SqlSource.Repository.Tests/MutationReport/ReportReaderTests.cs`

**Interfaces:**
- Produces, in namespace `SqlSource.MutationReport`, all `internal`:

```csharp
internal enum MutantStatus { Killed, Timeout, Survived, NoCoverage, Ignored, CompileError, RuntimeError, Pending }

// File is the path from the report's projectRoot, with '/'.  Ordinal numbers mutants whose other fields are equal,
// from 0, in the order the report gives them.
internal sealed record MutantKey(
    string File, int StartLine, int StartColumn, int EndLine, int EndColumn,
    string Mutator, string Replacement, int Ordinal);

internal sealed record Mutant(MutantKey Key, MutantStatus Status);

internal static class ReportReader
{
    // Throws InvalidDataException for text that is no report or a status it does not know.
    public static ValueTask<IReadOnlyList<Mutant>> ReadAsync(Stream json, CancellationToken cancellation);
}
```

- Produces for the tests, in `Reports.cs`: `Reports.Json(string projectRoot, params (string File, int Line, string
  Mutator, string Replacement, string Status)[] mutants)`, which returns a report's JSON as a `Stream`, each mutant at
  columns 1 to 5 of its line, with a `coveredBy` and a `killedBy` list and a `source` on its file so that the reader
  is shown to skip them.

The project: `net10.0`, `OutputType` `Exe`, `IsPackable` `false`, `AssemblyName` and `RootNamespace`
`SqlSource.MutationReport`, `InternalsVisibleTo` `SqlSource.Repository.Tests`.  The report's shape is the
mutation-testing report schema: `projectRoot`, and `files`, an object from an absolute path to
`{ "source": …, "mutants": [{ "id", "mutatorName", "replacement", "location": { "start": { "line", "column" },
"end": {…} }, "status", "coveredBy", "killedBy" }] }`.

- [ ] **Step 1: Create the project, add it to the solution, reference it from the test project**

Run: `dotnet build SqlSource.slnx`
Expected: builds, and writes `tools/MutationReport/packages.lock.json`.

- [ ] **Step 2: Write the failing tests**

```csharp
public sealed class ReportReaderTests
{
    [Fact]
    public async Task ReadAsync_ReportOfOneFile_GivesEachMutantItsKeyAndStatus()
    {
        var mutants = await ReportReader.ReadAsync(
            Reports.Json("/repo/src/SqlSource", ("/repo/src/SqlSource/A.cs", 7, "Boolean mutation", "true", "Killed")),
            TestContext.Current.CancellationToken);

        mutants.ShouldBe([
            new Mutant(new MutantKey("A.cs", 7, 1, 7, 5, "Boolean mutation", "true", 0), MutantStatus.Killed),
        ]);
    }

    [Theory]
    [InlineData("/one/src/SqlSource", "/one/src/SqlSource/Parsing/A.cs")]
    [InlineData("/two/elsewhere", "/two/elsewhere/Parsing/A.cs")]
    [InlineData(@"C:\repo\src\SqlSource", @"C:\repo\src\SqlSource\Parsing\A.cs")]
    public async Task ReadAsync_FileUnderTheProjectRoot_IsKeyedByItsPathFromTheRoot(string root, string file) =>
        (await ReportReader.ReadAsync(Reports.Json(root, (file, 1, "m", "r", "Killed")), Token))
            .Single().Key.File.ShouldBe("Parsing/A.cs");

    [Fact]
    public async Task ReadAsync_MutantsWithOneKey_AreNumberedInReportOrder() =>
        (await ReportReader.ReadAsync(
            Reports.Json("/r", ("/r/A.cs", 1, "m", "r", "Killed"), ("/r/A.cs", 1, "m", "r", "Survived")), Token))
            .Select(mutant => (mutant.Key.Ordinal, mutant.Status))
            .ShouldBe([(0, MutantStatus.Killed), (1, MutantStatus.Survived)]);

    [Fact]
    public async Task ReadAsync_StatusItDoesNotKnow_ThrowsNamingIt() =>
        (await Should.ThrowAsync<InvalidDataException>(async () =>
            await ReportReader.ReadAsync(Reports.Json("/r", ("/r/A.cs", 1, "m", "r", "Zombie")), Token)))
            .Message.ShouldContain("Zombie");

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{\"schemaVersion\":\"2\"}")]
    public async Task ReadAsync_TextThatIsNoReport_Throws(string text) =>
        await Should.ThrowAsync<InvalidDataException>(async () =>
            await ReportReader.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(text)), Token));
}
```

`Token` is `TestContext.Current.CancellationToken`.

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Repository.Tests -- --filter-class '*ReportReaderTests'`
Expected: FAIL, since `ReadAsync` does not exist or throws `NotImplementedException`.

- [ ] **Step 4: Implement `ReportReader.ReadAsync`**

Deserialize the stream with `JsonSerializer.DeserializeAsync` into private types that declare only `projectRoot`,
`files`, `mutants`, `mutatorName`, `replacement`, `location` and `status`, so that `System.Text.Json` skips `source`,
`coveredBy`, `killedBy` and `testFiles` without holding them.  A `JsonException` becomes an `InvalidDataException`.

- [ ] **Step 5: Run them to see them pass**

Run: `dotnet test --project tests/SqlSource.Repository.Tests -- --filter-class '*ReportReaderTests'`
Expected: PASS.

- [ ] **Step 6: Validate and commit**

Run: `./pre-commit-validation.sh`, then:

```bash
git add SqlSource.slnx tools/MutationReport tests/SqlSource.Repository.Tests
git commit -m "feat: read a Stryker mutation report"
```

---

### Task 3: Compare two reports

**Files:**
- Create: `tools/MutationReport/ReportComparison.cs`
- Test: `tests/SqlSource.Repository.Tests/MutationReport/ReportComparisonTests.cs`

**Interfaces:**
- Consumes: `Mutant`, `MutantKey`, `MutantStatus` of Task 2.
- Produces:

```csharp
// A mutant that was detected before (Killed or Timeout) and is not shown to be detected after.
internal sealed record Change(MutantKey Key, MutantStatus Before, MutantStatus? After);

internal sealed record Comparison(int Detected, IReadOnlyList<Change> Lost, IReadOnlyList<Change> NotComparable)
{
    public bool Holds => Lost.Count == 0 && NotComparable.Count == 0;
}

internal static class ReportComparison
{
    public static Comparison Compare(IReadOnlyList<Mutant> before, IReadOnlyList<Mutant> after);
}
```

`Detected` counts the mutants that are `Killed` or `Timeout` before.  Each of them, by its status after, is still
detected (`Killed`, `Timeout`), lost (`Survived`, `NoCoverage`), or not comparable (`Ignored`, `CompileError`,
`RuntimeError`, `Pending`, or absent, with `After` null).  A mutant that was not detected before is left out, whatever
it is after.  `Lost` and `NotComparable` are ordered by file, line, column and ordinal.

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class ReportComparisonTests
{
    private static Mutant At(int line, MutantStatus status) =>
        new(new MutantKey("A.cs", line, 1, line, 5, "m", "r", 0), status);

    [Theory]
    [InlineData(MutantStatus.Killed, MutantStatus.Killed)]
    [InlineData(MutantStatus.Killed, MutantStatus.Timeout)]
    [InlineData(MutantStatus.Timeout, MutantStatus.Killed)]
    public void Compare_DetectedBeforeAndAfter_Holds(MutantStatus before, MutantStatus after)
    {
        var comparison = ReportComparison.Compare([At(1, before)], [At(1, after)]);

        comparison.Holds.ShouldBeTrue();
        comparison.Detected.ShouldBe(1);
    }

    [Theory]
    [InlineData(MutantStatus.Killed, MutantStatus.Survived)]
    [InlineData(MutantStatus.Killed, MutantStatus.NoCoverage)]
    [InlineData(MutantStatus.Timeout, MutantStatus.Survived)]
    public void Compare_DetectedBeforeAndNotAfter_IsLost(MutantStatus before, MutantStatus after) =>
        ReportComparison.Compare([At(1, before)], [At(1, after)])
            .Lost.ShouldBe([new Change(At(1, before).Key, before, after)]);

    [Theory]
    [InlineData(MutantStatus.Ignored)]
    [InlineData(MutantStatus.CompileError)]
    [InlineData(MutantStatus.RuntimeError)]
    [InlineData(MutantStatus.Pending)]
    public void Compare_DetectedBeforeAndNotTestedAfter_IsNotComparable(MutantStatus after)
    {
        var comparison = ReportComparison.Compare([At(1, MutantStatus.Killed)], [At(1, after)]);

        comparison.Lost.ShouldBeEmpty();
        comparison.NotComparable.ShouldBe([new Change(At(1, after).Key, MutantStatus.Killed, after)]);
    }

    [Fact]
    public void Compare_DetectedBeforeAndAbsentAfter_IsNotComparableWithNoStatus() =>
        ReportComparison.Compare([At(1, MutantStatus.Killed)], [])
            .NotComparable.ShouldBe([new Change(At(1, MutantStatus.Killed).Key, MutantStatus.Killed, null)]);

    [Theory]
    [InlineData(MutantStatus.Survived, MutantStatus.NoCoverage)]
    [InlineData(MutantStatus.Ignored, MutantStatus.Survived)]
    [InlineData(MutantStatus.NoCoverage, MutantStatus.Killed)]
    public void Compare_NotDetectedBefore_IsLeftOut(MutantStatus before, MutantStatus after)
    {
        var comparison = ReportComparison.Compare([At(1, before)], [At(1, after)]);

        comparison.Holds.ShouldBeTrue();
        comparison.Detected.ShouldBe(0);
    }

    [Fact]
    public void Compare_MutantOnlyInTheSecondReport_IsLeftOut() =>
        ReportComparison.Compare([], [At(1, MutantStatus.Survived)]).Holds.ShouldBeTrue();

    [Fact]
    public void Compare_SeveralLost_AreOrderedByPosition() =>
        ReportComparison.Compare(
            [At(9, MutantStatus.Killed), At(2, MutantStatus.Killed)],
            [At(9, MutantStatus.Survived), At(2, MutantStatus.Survived)])
            .Lost.Select(change => change.Key.StartLine).ShouldBe([2, 9]);
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Repository.Tests -- --filter-class '*ReportComparisonTests'`
Expected: FAIL.

- [ ] **Step 3: Implement `ReportComparison.Compare`** with a dictionary of the second report by key.

- [ ] **Step 4: Run them to see them pass**, with the same command.  Expected: PASS.

- [ ] **Step 5: Validate and commit**

Run: `./pre-commit-validation.sh`, then:

```bash
git add tools/MutationReport tests/SqlSource.Repository.Tests
git commit -m "feat: compare the killed mutants of two mutation reports"
```

---

### Task 4: The `compare` command

**Files:**
- Create: `tools/MutationReport/Cli.cs`
- Modify: `tools/MutationReport/Program.cs`
- Test: `tests/SqlSource.Repository.Tests/MutationReport/CliTests.cs`
- Modify: `CONTRIBUTING.md` (the list of projects), `tests/README.md` (the table's row of
  `tests/SqlSource.Repository.Tests`, the graph, the list under it)

**Interfaces:**
- Consumes: `ReportReader.ReadAsync`, `ReportComparison.Compare`.
- Produces:

```csharp
internal static class Cli
{
    // 0: every mutant detected before is detected after.  1: one is lost or not comparable.
    // 2: the arguments are wrong, or a report cannot be read.
    public static Task<int> RunAsync(string[] arguments, TextWriter output, TextWriter error);
}
```

`Program.cs` is `return await Cli.RunAsync(args, Console.Out, Console.Error);`.  The one command is
`compare <before.json> <after.json>`.  What it prints, exactly, to `output`:

```text
LOST A.cs:7:1 Boolean mutation -> true (was Killed, now Survived)
NOT COMPARABLE A.cs:9:1 Boolean mutation -> true (was Killed, now Ignored)
NOT COMPARABLE A.cs:12:1 Boolean mutation -> true (was Killed, now absent)
3 detected before, 0 still detected, 1 lost, 2 not comparable
```

The last line is always printed.  To `error`, with exit code 2: `Usage: compare <before.json> <after.json>` for any
other arguments, and `Cannot read <path>: <the exception's message>` for a file that is missing or no report.

- [ ] **Step 1: Write the failing tests**

Each test writes its reports with `Reports.Json` into a folder from `Directory.CreateTempSubdirectory()`, which it
deletes in a `finally`, and runs `Cli.RunAsync` with two `StringWriter`s.

```csharp
public sealed class CliTests
{
    [Fact] public async Task RunAsync_NothingLost_ExitsZeroWithTheSummaryAlone();
    //  before: line 7 Killed.  after: line 7 Killed.
    //  exit 0; output is "1 detected before, 1 still detected, 0 lost, 0 not comparable\n"; error is empty.

    [Fact] public async Task RunAsync_MutantLostAndMutantsNotComparable_ExitsOneAndNamesEach();
    //  before: lines 7, 9, 12 Killed.  after: line 7 Survived, line 9 Ignored, line 12 absent.
    //  exit 1; output is the four lines above, exactly.

    [Theory]
    [InlineData()]
    [InlineData("compare")]
    [InlineData("compare", "one.json")]
    [InlineData("compare", "one.json", "two.json", "three.json")]
    [InlineData("condense", "one.json", "two.json")]
    public async Task RunAsync_OtherArguments_ExitsTwoWithTheUsage(params string[] arguments);
    //  exit 2; error is "Usage: compare <before.json> <after.json>\n"; output is empty.

    [Fact] public async Task RunAsync_ReportThatDoesNotExist_ExitsTwoAndNamesTheFile();
    //  exit 2; error starts with "Cannot read " + the missing path; output is empty.

    [Fact] public async Task RunAsync_FileThatIsNoReport_ExitsTwoAndNamesTheFile();
    //  the second file holds "[]".  exit 2; error starts with "Cannot read " + its path.
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Repository.Tests -- --filter-class '*CliTests'`
Expected: FAIL.

- [ ] **Step 3: Implement `Cli.RunAsync` and `Program.cs`**

- [ ] **Step 4: Run them to see them pass**, with the same command.  Expected: PASS.

- [ ] **Step 5: Run it on the real reports of Task 1**

```bash
dotnet run --project tools/MutationReport -- compare \
    artifacts/mutation/trial-generator/reports/mutation-report.json \
    artifacts/mutation/trial-generator/reports/mutation-report.json; echo "exit $?"
```

Expected: one line, `N detected before, N still detected, 0 lost, 0 not comparable` with N about 48, and `exit 0`.
The same on the tool's report, with N about 104.

- [ ] **Step 6: Update the documents**

- `CONTRIBUTING.md`: `tools/MutationReport` in the list of projects, as the console program that compares two
  mutation reports, run by `tools/mutation-test.sh` and tested in `tests/SqlSource.Repository.Tests`; that project's
  line gains those tests.
- `tests/README.md`: the row of `tests/SqlSource.Repository.Tests` gains the tests of `tools/MutationReport`; the
  graph gains a node `MutationReport["tools/MutationReport"]` and `RepositoryTests --> MutationReport`; the line
  under it says why.

- [ ] **Step 7: Validate and commit**

Run: `./pre-commit-validation.sh`, then:

```bash
git add tools/MutationReport tests/SqlSource.Repository.Tests CONTRIBUTING.md tests/README.md
git commit -m "feat: add the compare command of the mutation report tool"
```

---

### Task 5: `tools/mutation-test.sh`

**Files:**
- Create: `tools/mutation-test.sh` (executable)

**Interfaces:**
- Consumes: the two `stryker-config.json` files of Task 1 and the `compare` command of Task 4.
- Produces:

```text
Usage: mutation-test.sh <generator|tool> [--output <folder>] [--tests <filter>] [<glob> ...]
       mutation-test.sh compare <before.json> <after.json>
```

- `generator` is `src/SqlSource` and `tool` is `src/SqlSource.Tool`.  The script finds the repository from its own
  path, so it runs from any folder, and goes to the project's folder before it starts Stryker.
- Each `<glob>` becomes a `--mutate`, read from the project's folder (`'**/Parsing/*.cs'`); with none the whole
  project is tested.  `--tests` becomes `--test-case-filter`.
- `--output` defaults to `artifacts/mutation/<generator|tool>` of the repository, and a relative one is read from the
  folder the script was called in.  The script ends by printing the report's path,
  `<output>/reports/mutation-report.json`.
- The lock is the folder `artifacts/mutation/.running`, made with `mkdir` before Stryker starts and removed by a
  `trap` on `EXIT`.  If it exists, the script prints to stderr that another run holds it, that two runs cannot share a
  checkout, and that the folder is removed by hand if no run is going, and exits 2 without starting Stryker.
- `compare` runs `dotnet run --project tools/MutationReport -- compare <before> <after>` with the two paths made
  absolute, and exits with its code.  It takes no lock.
- Any other arguments print the usage to stderr and exit 2.  Stryker's exit code is the script's.

Follow `tools/record-fixtures.sh` for the header comment, `set -euo pipefail` and the `usage` function.

- [ ] **Step 1: Write the script**

- [ ] **Step 2: Lint it**

Run: `tools/shellcheck.sh && tools/shfmt.sh --check`
Expected: both pass.

- [ ] **Step 3: Run a scope from another folder, then compare with a test class left out**

```bash
cd /tmp && "$OLDPWD"/tools/mutation-test.sh generator --output "$OLDPWD"/artifacts/mutation/before \
    --tests 'FullyQualifiedName~SqlSource.Tests.Parsing' '**/Parsing/SqlDialectName.cs'; cd "$OLDPWD"
tools/mutation-test.sh generator --output artifacts/mutation/after \
    --tests 'FullyQualifiedName~SqlSource.Tests.Parsing&FullyQualifiedName!~SqlDialectNameTests' \
    '**/Parsing/SqlDialectName.cs'
tools/mutation-test.sh compare artifacts/mutation/before/reports/mutation-report.json \
    artifacts/mutation/after/reports/mutation-report.json; echo "exit $?"
```

Expected: the first run kills about 48 of 51 and prints its report's path.  The comparison prints the summary line,
and exits 1 with a `LOST` line for each mutant that only `SqlDialectNameTests` killed, or exits 0 if the other tests
of the folder kill them all.  Record which in the pull request: it is the first real measurement of that class.

- [ ] **Step 4: See a second run refused**

While a run of Step 3 is going, in another terminal: `tools/mutation-test.sh tool; echo "exit $?"`
Expected: the message that names `artifacts/mutation/.running`, `exit 2`, and no Stryker banner.

- [ ] **Step 5: See the lock released**

Interrupt a run with Ctrl-C, and run one with `--tests 'FullyQualifiedName~NoSuchTest'`.
Expected: after each, `artifacts/mutation/.running` does not exist, and the second exits with Stryker's non-zero
code.

- [ ] **Step 6: Validate and commit**

Run: `./pre-commit-validation.sh`, then:

```bash
git add tools/mutation-test.sh
git commit -m "build: add tools/mutation-test.sh"
```

---

### Task 6: Full runs, and the documents

**Files:**
- Modify: `tools/README.md` (a new section, "Mutation testing", after "Recording scripts")
- Modify: `tools/AGENTS.md`
- Modify: `CONTRIBUTING.md`
- Modify: the two `stryker-config.json` files, only if tuning is needed

**Interfaces:**
- Consumes: `tools/mutation-test.sh`.
- Produces: the measured wall time and counts of a whole run of each project, which the plan of pull request 2 reads
  from `tools/README.md`.

- [ ] **Step 1: Run the generator whole, in the background**

Run: `time tools/mutation-test.sh generator`
Expected: exit 0 within ten hours.  Note the wall time and the counts Stryker prints: created, compile errors,
tested, killed, survived, timed out, not covered.

- [ ] **Step 2: Run the tool whole, in the background, after the first has ended**

Run: `time tools/mutation-test.sh tool`
Expected: the same.  Its tests start `dotnet msbuild` and other processes, so expect timeouts; if they pass a few
hundred, lower `concurrency` in its configuration and run again.

If either run does not fit in ten hours after `concurrency` is tuned and mutators that only make noise are named in
`ignore-mutations`, stop and ask the owner.  If a whole run of the tool cannot be made to work, stop and ask the
owner.

- [ ] **Step 3: Compare each full report with itself**

Run: `time tools/mutation-test.sh compare <report> <report>` for each.
Expected: `0 lost, 0 not comparable`, exit 0, in seconds, with the process's memory far below the report's size
(`/usr/bin/time -l` on macOS shows the maximum resident set size).  Note the report's size.

- [ ] **Step 4: Write "Mutation testing" in `tools/README.md`**

It says, in this order:

- what the script is, that it is no check and that a person runs it, with its two usages and one example of a scope
  and of a comparison;
- that a whole run takes hours, with the measured wall time and counts of each project and the date and machine they
  were measured on, and the size of a full report;
- what `reports/mutation-report.json` holds: each mutant's status, the tests that cover it and the tests that kill
  it, and each test's file, name and line;
- how `compare` matches two reports and what its three exit codes mean, and that the two runs must have the same
  scope and no change to `src/` between them;
- the limits:
  - the `mtp` runner is a preview, so a result that decides something is read by a person;
  - the mutants of a method are all untestable when one of them does not compile, with the measured share of each
    project;
  - the rows of a theory share one name in the report;
  - a mutant is tested only by its project's one test project, so a kill by another project is not seen;
  - Stryker makes every mutant of the project on every run, whatever the scope;
- that Stryker refuses the repository root, which is why the configuration is beside each project and why
  `tests/SqlSource.Tool.Tests` references the tool itself.

- [ ] **Step 5: Add the bullet to `tools/AGENTS.md`**, before the maintenance footer:

```markdown
- **`mutation-test.sh` is no check and stays out of `pre-commit-validation.sh` and `build.yml`**, since a whole run
  takes hours.  It runs Stryker from the folder of the project it mutates, with the `stryker-config.json` there, and
  `tools/MutationReport` compares two reports ([the script](README.md#mutation-testing)).
```

- [ ] **Step 6: Update `CONTRIBUTING.md`**

Stryker.NET joins CSharpier, ReportGenerator and docfx in the sentence on local tools.  After the coverage commands,
a short paragraph gives `tools/mutation-test.sh generator '**/Parsing/*.cs'` as the way to see which mutants of a
folder the tests miss, and links `tools/README.md#mutation-testing`.

- [ ] **Step 7: Validate and commit**

Run: `./pre-commit-validation.sh`, then:

```bash
git add tools/README.md tools/AGENTS.md CONTRIBUTING.md src/SqlSource/stryker-config.json \
    src/SqlSource.Tool/stryker-config.json
git commit -m "docs: document mutation testing and its measured runs"
```

---

### Task 7: Review and pull request

- [ ] **Step 1:** `git fetch --tags origin`, then compare `VersionPrefix` of `Directory.Build.props` with
  `git tag --list 'v*' --sort=-v:refname | head -n 1`.  On 2026-11-12 the repository had no `v*` tag, so there was
  nothing to compare.
- [ ] **Step 2:** Run `branch-review:review`, address every finding, the minor ones too, and run it again until it
  comes back clean.
- [ ] **Step 3:** Merge `origin/main` into the branch so that it is up to date.
- [ ] **Step 4:** Run `./pre-commit-validation.sh --full` on the tree as it will be pushed.  Expected: every step
  passes.
- [ ] **Step 5:** Offer the pull request to the owner.  When they agree, push, open it, and turn on Auto-fix.  Its
  description gives the measured times and counts, and the result of Task 5, Step 3.
