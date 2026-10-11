# TD-0038, pull request 2: results and errors of the tool - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Items 5 and 6 of TD-0038, and the tool's part of items 10 and 11, each get one shape: one record for a value or why not, `ToolDiagnostic.ForFile` taking its path once, a `RunMode` in place of two booleans, one answer in place of two for whether a run may delete, a `Reporter` that is made with its trace, and one read of a file's text.

**Architecture:** `Outcome<TValue, TFailure>` at the root of the tool replaces `DescribeResult`, `OpenResult` and `SolutionProjects`, and is what four readers return in place of a null with an `out` or a report of their own.  The rest removes a repeated argument, a pair of booleans or a settable property in favour of one value that is decided in one place.  Nothing a user sees changes.

**Tech Stack:** C# (`LangVersion` `preview`) on `net8.0` for `src/SqlSource.Tool`, with System.CommandLine 2.0.12; xunit v3 on Microsoft.Testing.Platform with Shouldly.

**Spec:** `docs/superpowers/specs/2026-10-10-td-0038-duplicated-patterns-design.md`, the section "Pull request 2".

## Where the plan departs from the spec

- **`DescribeRun.RunAsync` hands the counts to a function it is given, and does not return them.**  Today `RunTrace.Databases` is set before the summary is written, so a summary that cannot be written (standard output is closed) still ends the log with the counts.  A returned value would lose them on that road.  Task 6 pins it with a test.
- **`RunLogFile.TryOpen` is renamed `Open`**, since it no longer has the shape of a `TryX`.

## Global Constraints

- Nothing a user sees changes: no message, id, exit code, sidecar, log member or line of `--verbose`.  `README.md`, `docs/diagnostics.md`, `docs/publishing.md` and `CONTRIBUTING.md` are not touched.
- `src/SqlSource` is not touched: every change is in `src/SqlSource.Tool` and `tests/SqlSource.Tool.Tests`.
- The existing tests pass unchanged, apart from tests of a member that is renamed, moved or removed, which follow it.
- The build treats warnings as errors with `AnalysisLevel` `latest-all`.  Fix what it reports in the code, a `using` that nothing needs any more included; never suppress.
- `ImplicitUsings` is off: a file names every namespace it uses.  `Outcome<,>` is in `SqlSource.Tool`, which every namespace of the tool and of its tests is under, so it needs no `using`.
- Every commit passes `./pre-commit-validation.sh` (it needs Docker running).  Run `./format.sh`, then the validation, then commit in a command of its own.  Never work around the hook.
- An `AGENTS.md` that names a member changes in the commit that renames or removes it.
- Prose in comments and documents follows the repository's style: plain sentences, two spaces after a full stop.
- End each commit message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

Run one test class with:

```bash
dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.OutcomeTests"
```

Run the tool's tests with `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`, and everything with `dotnet test --solution SqlSource.slnx`.

## Review Focus

1. **A describer that says it failed and gives no failure** (`Failed(null!)`), or says it succeeded and gives no value.  It must still be `SQLSRC200` with "A describer returned nothing.", so `DatabaseRuns` reads `Value` and `Failure` and not `Succeeded`.  Test in Task 2.
2. **A run that reported an error, with a file that needs no entry and has a sidecar.**  The sidecar must stay.  `FileOutcomes` no longer sees why a run may not delete, so a test of the whole command holds it.  Test in Task 4.
3. **Standard output that fails while the summary is written, in a run with a log.**  `run.end` must still hold the counts of each database.  Test in Task 6.
4. **A log whose path cannot be opened, or is a file of the run.**  The error must be at the full path, quote that path first, and say `written`.  Tests in Task 3.
5. **`describe --help` with `SQLSOURCE_LOG` set.**  `describe` has no action until `Cli` binds one, and `DescribeCommand.Runs` must still tell a run from the help, so the help does not empty the log.  The existing tests of `RunLogTests` and `DescribeTests` hold it; Task 6 runs them and changes neither.

## File Structure

| File | Change |
|----|----|
| `src/SqlSource.Tool/Reporting/ToolDiagnostic.cs` | `ForFile` puts the path first among the arguments; gains `AtFile` |
| `src/SqlSource.Tool/Outcome.cs` | New: a value, or why there is none |
| `src/SqlSource.Tool/Describing/Describers/DescribeResult.cs`, `OpenResult.cs` | Deleted |
| `src/SqlSource.Tool/Describing/Describers/IQueryDescriber.cs`, `IDescribeSession.cs` | Return an `Outcome` |
| `src/SqlSource.Tool/Describing/DatabaseRuns.cs` | Reads `Value` |
| `src/SqlSource.Tool/Projects/ManifestReader.cs`, `SolutionReader.cs`, `RunUnitFinder.cs` | Return an `Outcome`; `RunUnitFinder` takes no reporter |
| `src/SqlSource.Tool/Projects/SolutionProjects.cs` | Deleted |
| `src/SqlSource.Tool/Projects/ProjectEvaluator.cs`, `RunProjects.cs` | Read the outcomes |
| `src/SqlSource.Tool/Logging/RunLogFile.cs` | `TryOpen` becomes `Open`, which returns an `Outcome` whose failure is the `SQLSRC218` |
| `src/SqlSource.Tool/Describing/RunMode.cs` | New: `Describe`, `Force`, `Check` |
| `src/SqlSource.Tool/Describing/DescribeOptions.cs`, `RunDecisions.cs`, `RunSummary.cs`, `FileOutcomes.cs`, `FileCheck.cs`, `SidecarCheck.cs`, `SidecarStore.cs` | Take the mode, or `mayDelete`, or hold the `FileOutcome` |
| `src/SqlSource.Tool/Planning/FileText.cs` | New: the one read of a file's text |
| `src/SqlSource.Tool/Planning/SqlFileText.cs`, `AttributeReader.cs` | Read through `FileText` |
| `src/SqlSource.Tool/Reporting/Reporter.cs`, `Logging/RunTrace.cs` | The trace is given to the constructor; `RunTrace.Databases` goes |
| `src/SqlSource.Tool/Describing/DescribeRun.cs`, `DescribeCommand.cs`, `Cli.cs` | The counts go to a function; `describe` is bound when the reporter exists |
| `src/SqlSource.Tool/AGENTS.md` | The rule for a result that may fail, and every member named above |
| `docs/tech-debt/TD-0038-*.md`, `docs/tech-debt/README.md` | Rewritten to what remains |

No task measures time or allocation: nothing here runs in the generator.

---

### Task 1: `ForFile` takes the path once (item 6)

**Files:**
- Create: `tests/SqlSource.Tool.Tests/Reporting/ToolDiagnosticTests.cs`
- Modify: `src/SqlSource.Tool/Reporting/ToolDiagnostic.cs`
- Modify the thirteen calls: `src/SqlSource.Tool/Cli.cs`, `Projects/ProjectEvaluator.cs`, `Projects/RunProjects.cs`, `Planning/RunPlanner.cs`, `Describing/RunDecisions.cs`, `Describing/SidecarStore.cs`, `Describing/SidecarCheck.cs`
- Modify: `tests/SqlSource.Tool.Tests/Planning/RunPlannerErrorTests.cs`, `tests/SqlSource.Tool.Tests/Describing/RunDecisionsTests.cs`

**Interfaces:**
- Produces: `ToolDiagnostic.ForFile(DiagnosticDescriptor descriptor, string path, params string[] rest)`, whose arguments are `[path, .. rest]`; `ToolDiagnostic.AtFile(DiagnosticDescriptor descriptor, string path, params string[] arguments)`, whose arguments are as given.

- [ ] **Step 1: Write the failing tests**

`tests/SqlSource.Tool.Tests/Reporting/ToolDiagnosticTests.cs`:

```csharp
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests.Reporting;

public sealed class ToolDiagnosticTests
{
    [Fact]
    public void ForFile_PathAndTheRest_IsAtThePathAndQuotesItFirst()
    {
        var diagnostic = ToolDiagnostic.ForFile(ToolDiagnostics.FileCannotBeChanged, "/work/a.json", "written", "no");

        diagnostic.Path.ShouldBe("/work/a.json");
        diagnostic.Position.ShouldBeNull();
        diagnostic.Arguments.ToArray().ShouldBe(["/work/a.json", "written", "no"]);
    }

    [Fact]
    public void ForFile_PathAlone_QuotesThePath()
    {
        var diagnostic = ToolDiagnostic.ForFile(ToolDiagnostics.ProjectNotRestored, "/work/App.csproj");

        diagnostic.Arguments.ToArray().ShouldBe(["/work/App.csproj"]);
    }

    [Fact]
    public void AtFile_Arguments_IsAtThePathAndQuotesOnlyWhatItWasGiven()
    {
        var diagnostic = ToolDiagnostic.AtFile(SqlDiagnostics.InvalidDialect, "/work/App.csproj", "nosuch");

        diagnostic.Path.ShouldBe("/work/App.csproj");
        diagnostic.Position.ShouldBeNull();
        diagnostic.Arguments.ToArray().ShouldBe(["nosuch"]);
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Reporting.ToolDiagnosticTests"`
Expected: the build fails, `'ToolDiagnostic' does not contain a definition for 'AtFile'`.

- [ ] **Step 3: Change `ForFile` and add `AtFile`**

In `src/SqlSource.Tool/Reporting/ToolDiagnostic.cs`, replace `ForFile` and its documentation with:

```csharp
    /// <summary>
    /// An error about a file, at no position in it, whose message quotes the file first, as <c>{0}</c>.
    /// </summary>
    /// <param name="descriptor">What is wrong.</param>
    /// <param name="path">The full path of the file: where the error is, and the first text the message quotes.</param>
    /// <param name="rest">The text the message quotes after the path.</param>
    public static ToolDiagnostic ForFile(DiagnosticDescriptor descriptor, string path, params string[] rest) =>
        AtFile(descriptor, path, [path, .. rest]);

    /// <summary>
    /// An error at a file, at no position in it, whose message does not quote that file first: a descriptor of the
    /// generator for a setting, which quotes no path, or an error at a sidecar that quotes its <c>.sql</c> file.
    /// </summary>
    /// <param name="descriptor">What is wrong.</param>
    /// <param name="path">The full path of the file.</param>
    /// <param name="arguments">The text the descriptor's message quotes.</param>
    public static ToolDiagnostic AtFile(DiagnosticDescriptor descriptor, string path, params string[] arguments) =>
        Create(descriptor, arguments) with
        {
            Path = path,
        };
```

- [ ] **Step 4: Change the thirteen calls**

Ten lose the argument that repeats the path:

| Place | Becomes |
|----|----|
| `Cli.cs`, the log that cannot be opened | `ToolDiagnostic.ForFile(ToolDiagnostics.FileCannotBeChanged, path, "written", reason)` |
| `Cli.cs`, `End` | `ToolDiagnostic.ForFile(ToolDiagnostics.FileCannotBeChanged, log.Path, "written", failure.Message)` |
| `ProjectEvaluator.cs`, the manifest | `ToolDiagnostic.ForFile(ToolDiagnostics.ManifestCannotBeRead, project, reason)` |
| `ProjectEvaluator.cs`, `Failed` | `.ForFile(ToolDiagnostics.ProjectCannotBeEvaluated, project)` |
| `RunProjects.cs`, the solution | `ToolDiagnostic.ForFile(ToolDiagnostics.SolutionCannotBeRead, unit.Path, reason)` |
| `RunProjects.cs`, `DoesNotUseSqlSource` | `.ForFile(ToolDiagnostics.ProjectDoesNotUseSqlSource, project)` |
| `RunProjects.cs`, `NotRestored` | `ToolDiagnostic.ForFile(ToolDiagnostics.ProjectNotRestored, project).WithLines(Restore)` |
| `RunDecisions.cs`, `SQLSRC221` | `.ForFile(ToolDiagnostics.SidecarOfNewerTool, onDisk.Path, newer.ToString(CultureInfo.InvariantCulture), SidecarFormat.Version.ToString(CultureInfo.InvariantCulture))` |
| `SidecarStore.cs`, `Failure` | `ToolDiagnostic.ForFile(ToolDiagnostics.FileCannotBeChanged, path, tried, exception.Message)` |
| `SidecarStore.cs`, `NotWritten` | `.ForFile(ToolDiagnostics.SidecarNotWritten, outcome.File.Path)` |

Three keep their arguments and call `AtFile`: the two in `RunPlanner.ReadProject` (`SqlDiagnostics.InvalidDialect` and `SqlDiagnostics.InvalidSettingValue`, at `manifest.ProjectPath`), and `SidecarCheck.AtSidecar`.  In each, only the name `ForFile` becomes `AtFile`.

In the tests: the five `ToolDiagnostic.ForFile(` of `RunPlannerErrorTests.cs` become `ToolDiagnostic.AtFile(`, and in `RunDecisionsTests.cs` `.ForFile(ToolDiagnostics.SidecarOfNewerTool, path, path, "2", "1")` becomes `.ForFile(ToolDiagnostics.SidecarOfNewerTool, path, "2", "1")`.

- [ ] **Step 5: Check that no call repeats its path**

```bash
grep -rnE 'ForFile\(' src tests --include='*.cs'
```

Expected: the definition, and calls in which no expression follows itself.  Read each line.

- [ ] **Step 6: Run the tool's tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: all pass.  The tests of `Commands/` compare whole errors, so a call that lost the wrong argument fails one of them.

- [ ] **Step 7: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A src tests && git commit -m "Give ToolDiagnostic.ForFile its path once

ForFile puts the path first among the arguments, for a message that quotes
its file as {0}.  AtFile takes the arguments as given, for the three errors
at a file that quote something else.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `Outcome`, and the describer's two results (item 5)

**Files:**
- Create: `src/SqlSource.Tool/Outcome.cs`
- Create: `tests/SqlSource.Tool.Tests/OutcomeTests.cs`
- Delete: `src/SqlSource.Tool/Describing/Describers/DescribeResult.cs`, `src/SqlSource.Tool/Describing/Describers/OpenResult.cs`
- Modify: `src/SqlSource.Tool/Describing/Describers/IQueryDescriber.cs`, `IDescribeSession.cs`, `src/SqlSource.Tool/Describing/DatabaseRuns.cs`
- Modify: `tests/SqlSource.Tool.Tests/FakeDescriber.cs`, `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsTests.cs`
- Modify: `src/SqlSource.Tool/AGENTS.md`

**Interfaces:**
- Produces:

```csharp
internal sealed record Outcome<TValue, TFailure>
{
    public TValue? Value { get; }
    public TFailure? Failure { get; }
    public bool Succeeded { get; }   // true: Value is not null; false: Failure is not null
    public static Outcome<TValue, TFailure> Of(TValue value);
    public static Outcome<TValue, TFailure> Failed(TFailure failure);
}
```

- `IQueryDescriber.OpenAsync` returns `Task<Outcome<IDescribeSession, DescribeFailure>>`; `IDescribeSession.DescribeAsync` returns `Task<Outcome<QueryDescription, DescribeFailure>>`.

- [ ] **Step 1: Write the failing tests of `Outcome`**

`tests/SqlSource.Tool.Tests/OutcomeTests.cs`:

```csharp
using System.Collections.Immutable;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

public sealed class OutcomeTests
{
    [Fact]
    public void Of_Value_SucceededWithTheValueAndNoFailure()
    {
        var outcome = Outcome<string, string>.Of("value");

        outcome.Succeeded.ShouldBeTrue();
        outcome.Value.ShouldBe("value");
        outcome.Failure.ShouldBeNull();
    }

    [Fact]
    public void Failed_Reason_DidNotSucceedAndHasTheReasonAndNoValue()
    {
        var outcome = Outcome<string, string>.Failed("why");

        outcome.Succeeded.ShouldBeFalse();
        outcome.Failure.ShouldBe("why");
        outcome.Value.ShouldBeNull();
    }

    // A value that is a struct has no null: a caller reads Succeeded first.
    [Fact]
    public void Failed_OutcomeOfAStruct_HasTheDefaultOfTheStructAsItsValue()
    {
        var outcome = Outcome<ImmutableArray<string>, string>.Failed("why");

        outcome.Succeeded.ShouldBeFalse();
        outcome.Value.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void Equals_TwoOutcomes_AreEqualWhenTheyHoldTheSame()
    {
        Outcome<string, string>.Of("a").ShouldBe(Outcome<string, string>.Of("a"));
        Outcome<string, string>.Of("a").ShouldNotBe(Outcome<string, string>.Of("b"));
        Outcome<string, string>.Of("a").ShouldNotBe(Outcome<string, string>.Failed("a"));
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.OutcomeTests"`
Expected: the build fails, `The type or namespace name 'Outcome<,>' could not be found`.

- [ ] **Step 3: Write `Outcome`**

`src/SqlSource.Tool/Outcome.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;

namespace SqlSource.Tool;

/// <summary>
/// A value, or why there is none.  Exactly one of the two is set.
/// </summary>
/// <remarks>
/// What a method returns that another file calls and that gives one value or one reason.  Such a method reports
/// nothing: its caller does.  <c>src/SqlSource.Tool/AGENTS.md</c> has the rule, and what is no <c>Outcome</c>.
/// </remarks>
/// <typeparam name="TValue">What there is when it succeeded.</typeparam>
/// <typeparam name="TFailure">Why there is no value: a reason as text, an error, a describer's failure.</typeparam>
internal sealed record Outcome<TValue, TFailure>
{
    private Outcome(TValue? value, TFailure? failure, bool succeeded)
    {
        Value = value;
        Failure = failure;
        Succeeded = succeeded;
    }

    public TValue? Value { get; }

    public TFailure? Failure { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool Succeeded { get; }

    public static Outcome<TValue, TFailure> Of(TValue value) => new(value, default, succeeded: true);

    public static Outcome<TValue, TFailure> Failed(TFailure failure) => new(default, failure, succeeded: false);
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.OutcomeTests"`
Expected: 4 pass.

- [ ] **Step 5: Write the test of a describer that fails with no failure**

It cannot be written before the contract changes, since the fake names the new type.  In `tests/SqlSource.Tool.Tests/FakeDescriber.cs`, under `OpensNothing`:

```csharp
    // Returned in place of a failure, for a describer that says it failed and gives no reason.
    public bool OpenFailsWithNothing { get; set; }
```

and in `OpenAsync`, under the `if (OpensNothing)` block:

```csharp
                if (OpenFailsWithNothing)
                {
                    return Task.FromResult(Outcome<IDescribeSession, DescribeFailure>.Failed(null!));
                }
```

In `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsTests.cs`, under `Run_DescriberThatReturnsNothing_IsAFault`:

```csharp
    [Fact]
    public async Task Run_DescriberThatFailsWithNoFailure_IsAFault()
    {
        _describer.OpenFailsWithNothing = true;

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault
            .Diagnostic.Arguments.ToArray()
            .ShouldBe(["System.InvalidOperationException", "A describer returned nothing."]);
    }
```

- [ ] **Step 6: Change the contract to `Outcome`**

Delete `DescribeResult.cs` and `OpenResult.cs`.

`IQueryDescriber.cs`:

```csharp
    Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
        IRunLog log,
        CancellationToken cancellationToken
    );
```

`IDescribeSession.cs`:

```csharp
    Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        DescribeRequest request,
        CancellationToken cancellationToken
    );
```

`DatabaseRuns.cs`, in `RunDatabaseAsync`:

```csharp
        // The value and not Succeeded, here and where a query is described: a describer may have given null for
        // either, and an outcome that holds neither is a fault.
        if (opened.Value is not { } session)
```

and in `DescribeAllAsync`, `if (result.Description is { } description)` becomes `if (result.Value is { } description)`.  The two `?? throw new InvalidOperationException(Guard.Nothing)` stay as they are.

`FakeDescriber.cs`: `Task<OpenResult>` becomes `Task<Outcome<IDescribeSession, DescribeFailure>>`, in the signature and in `Task.FromResult<...>(null!)`; `OpenResult.Failed(failure)` and `OpenResult.Opened(new Session(...))` become `Outcome<IDescribeSession, DescribeFailure>.Failed(failure)` and `.Of(new Session(...))`; `Task<DescribeResult>` becomes `Task<Outcome<QueryDescription, DescribeFailure>>`; `DescribeResult.Failed(failure)` and `DescribeResult.Described(...)` become `Outcome<QueryDescription, DescribeFailure>.Failed(failure)` and `.Of(...)`.

- [ ] **Step 7: Say it in `src/SqlSource.Tool/AGENTS.md`**

In the bullet that starts "**`Describing/Describers/` is the contract a describer implements**", "It uses nothing of the run's steps, only `Reporting/` and `Logging/`" becomes "It uses nothing of the run's steps, only `Outcome`, `Reporting/` and `Logging/`", and "the requests, results, failures and server information they exchange" becomes "the requests, failures and server information they exchange, with each result an `Outcome` of a `DescribeFailure`".

- [ ] **Step 8: Run the tool's tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: all pass, the new `Run_DescriberThatFailsWithNoFailure_IsAFault` among them.

```bash
grep -rnE 'DescribeResult|OpenResult' src tests --include='*.cs' --include='*.md'
```

Expected: no line.

- [ ] **Step 9: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A src tests && git commit -m "Give a value or why not one record, and a describer's two results with it

Outcome<TValue, TFailure> replaces DescribeResult and OpenResult, which were
the same record under two names.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Four readers return an `Outcome` (item 5)

**Files:**
- Modify: `src/SqlSource.Tool/Projects/ManifestReader.cs`, `SolutionReader.cs`, `RunUnitFinder.cs`, `ProjectEvaluator.cs`, `RunProjects.cs`
- Delete: `src/SqlSource.Tool/Projects/SolutionProjects.cs`
- Modify: `src/SqlSource.Tool/Logging/RunLogFile.cs`, `src/SqlSource.Tool/DescribeCommand.cs`, `src/SqlSource.Tool/Cli.cs`
- Modify: `tests/SqlSource.Tool.Tests/TestProject.cs`, `Projects/ManifestReaderTests.cs`, `Projects/SolutionReaderTests.cs`, `Projects/RunUnitTests.cs`, `Logging/RunLogFileTests.cs`
- Modify: `src/SqlSource.Tool/AGENTS.md`

**Interfaces:**
- Consumes: `Outcome<TValue, TFailure>` of Task 2; `ToolDiagnostic.ForFile` of Task 1.
- Produces:
  - `ManifestReader.Read(string text)` returns `Outcome<ProjectManifest, string>`
  - `SolutionReader.ReadAsync(string solutionPath, CancellationToken cancellationToken)` returns `Task<Outcome<ImmutableArray<string>, string>>`
  - `RunUnitFinder.Find(string? argument, string workingDirectory)` returns `Outcome<RunUnit, ToolDiagnostic>`
  - `RunLogFile.Open(string given, string workingDirectory, TimeProvider time)` returns `Outcome<RunLogFile, ToolDiagnostic>`

Each of the four is changed with its tests, the tests first.  The four are independent; do them in this order and run the tool's tests after each.

- [ ] **Step 1: `ManifestReader.Read`**

Tests first.  In `Projects/ManifestReaderTests.cs`, the two helpers become:

```csharp
    private static ProjectManifest Read(string text)
    {
        var read = ManifestReader.Read(text);
        read.Succeeded.ShouldBeTrue(read.Failure);
        return read.Value.ShouldNotBeNull();
    }

    private static string ReasonOf(string text)
    {
        var read = ManifestReader.Read(text);
        read.Succeeded.ShouldBeFalse();
        return read.Failure.ShouldNotBeNull();
    }
```

In `TestProject.cs`:

```csharp
    public ProjectManifest Manifest()
    {
        var read = ManifestReader.Read(ManifestText());
        return read.Succeeded ? read.Value : throw new InvalidOperationException(read.Failure);
    }
```

Run `dotnet build tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`; expected: it fails, `No overload for method 'Read' takes 1 arguments`.

Then in `ManifestReader.cs`, the signature and its documentation become:

```csharp
    /// <summary>
    /// The manifest, or why the text is not one, as the end of a sentence.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    public static Outcome<ProjectManifest, string> Read(string text)
```

Add beside the other private helpers:

```csharp
    private static Outcome<ProjectManifest, string> No(string reason) =>
        Outcome<ProjectManifest, string>.Failed(reason);
```

In the body: `if (!IsVersionLine(line, out reason)) { return null; }` becomes `if (!IsVersionLine(line, out var reason)) { return No(reason); }`; each of the four `reason = <text>; return null;` becomes `return No(<text>);` with the same text; the final `reason = "";` goes, and `return new ProjectManifest { ... };` becomes `return Outcome<ProjectManifest, string>.Of(new ProjectManifest { ... });`.  `IsVersionLine` keeps its `out string reason`: it is a private helper.

In `ProjectEvaluator.cs`, the end of the method that reads the manifest file becomes:

```csharp
        var read = ManifestReader.Read(text);
        return read.Succeeded
            ? new ProjectEvaluation(project, ProjectState.UsesSqlSource, read.Value)
            : new ProjectEvaluation(
                project,
                ProjectState.Failed,
                Failure: ToolDiagnostic.ForFile(ToolDiagnostics.ManifestCannotBeRead, project, read.Failure)
            );
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`.  Expected: all pass.

- [ ] **Step 2: `SolutionReader.ReadAsync`**

Tests first.  In `Projects/SolutionReaderTests.cs`, the helper's return type becomes `Task<Outcome<ImmutableArray<string>, string>>` (add `using System.Collections.Immutable;`).  In each test:

- `read.Failure.ShouldBeNull();` followed by `read.Projects.ShouldBe(Expected());` becomes `read.Succeeded.ShouldBeTrue(read.Failure);` and `read.Value.ShouldBe(Expected());`
- `read.Failure.ShouldBeNull();` followed by `read.Projects.ShouldBeEmpty();` becomes `read.Succeeded.ShouldBeTrue(read.Failure);` and `read.Value.ShouldBeEmpty();`
- `read.Failure.ShouldNotBeNullOrWhiteSpace();` followed by `read.Projects.ShouldBeEmpty();` becomes `read.Succeeded.ShouldBeFalse();` and `read.Failure.ShouldNotBeNullOrWhiteSpace();`: a reader that failed has no list
- in `Read_FileOfAnotherKind_IsNotRead`, add `read.Succeeded.ShouldBeFalse();` above the comparison of `read.Failure`

Then in `SolutionReader.cs` (add `using System.Collections.Immutable;`), the summary gains "The full paths of the <c>.csproj</c> files, in ordinal order, or what the reader said when it could not read the file.", the return type becomes `Task<Outcome<ImmutableArray<string>, string>>`, and the three returns become:

```csharp
            return Outcome<ImmutableArray<string>, string>.Failed("it is neither a .sln nor a .slnx file");
```

```csharp
            return Outcome<ImmutableArray<string>, string>.Failed(exception.Message);
```

```csharp
        return Outcome<ImmutableArray<string>, string>.Of(
            [
                .. solution
                    .SolutionProjects.Select(project => project.FilePath)
                    // A project of another language is not the generator's.
                    .Where(static path => Path.GetExtension(path).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
                    // A .sln writes a path with "\" on every system.  The library gives it with the separator of this
                    // one today, and nothing promises that.
                    .Select(path => Path.GetFullPath(path.Replace('\\', Path.DirectorySeparatorChar), directory))
                    // The library refuses a solution that lists one project twice, so each is here once.
                    .Order(StringComparer.Ordinal),
            ]
        );
```

Delete `Projects/SolutionProjects.cs`.  In `RunProjects.FindAsync`:

```csharp
            var read = await SolutionReader.ReadAsync(unit.Path, cancellationToken);
            if (!read.Succeeded)
            {
                reporter.Report(
                    ToolDiagnostic.ForFile(ToolDiagnostics.SolutionCannotBeRead, unit.Path, read.Failure)
                );
                return [];
            }

            projects = read.Value;
            solution = unit.Path;
```

Run the tool's tests.  Expected: all pass.

- [ ] **Step 3: `RunUnitFinder.Find`**

Tests first.  In `Projects/RunUnitTests.cs`, the helper becomes the one place that reports, so that every assertion on `_error` stays as it is:

```csharp
    // The finder reports nothing: its caller does, as DescribeCommand does here.
    private RunUnit? Find(string? argument)
    {
        var found = RunUnitFinder.Find(argument, _folder.Path);
        if (found.Succeeded)
        {
            return found.Value;
        }

        new Reporter(_error).Report(found.Failure);
        return null;
    }
```

Then `RunUnitFinder.cs` (add `using Microsoft.CodeAnalysis;` for `DiagnosticDescriptor`):

```csharp
    /// <summary>
    /// The unit, or the error that says why there is none.
    /// </summary>
    /// <param name="argument">The path as given, or null when none was.</param>
    /// <param name="workingDirectory">The full path a relative path is resolved against.</param>
    public static Outcome<RunUnit, ToolDiagnostic> Find(string? argument, string workingDirectory)
    {
        // Path.GetFullPath throws for an empty path, which is what an unset variable of a shell gives.
        if (argument is { Length: 0 })
        {
            return No(ToolDiagnostics.NotARunUnit, argument);
        }

        var path = Path.TrimEndingDirectorySeparator(
            argument is null ? workingDirectory : Path.GetFullPath(argument, workingDirectory)
        );

        if (Directory.Exists(path))
        {
            return FindIn(path);
        }

        if (File.Exists(path) && KindOf(path) is { } kind)
        {
            return Outcome<RunUnit, ToolDiagnostic>.Of(new RunUnit(kind, path));
        }

        return No(ToolDiagnostics.NotARunUnit, path);
    }
```

`FindIn(string directory)` returns `Outcome<RunUnit, ToolDiagnostic>` and takes no reporter.  Its `catch` returns `No(ToolDiagnostics.DirectoryCannotBeRead, directory, exception.Message)` under the comment it has, and its `switch` becomes:

```csharp
        return units switch
        {
            [{ Kind: { } kind } unit] => Outcome<RunUnit, ToolDiagnostic>.Of(new RunUnit(kind, unit.File)),
            [] => No(ToolDiagnostics.NoRunUnit, directory),
            _ => No(ToolDiagnostics.SeveralRunUnits, directory),
        };
```

Add:

```csharp
    private static Outcome<RunUnit, ToolDiagnostic> No(DiagnosticDescriptor descriptor, params string[] arguments) =>
        Outcome<RunUnit, ToolDiagnostic>.Failed(ToolDiagnostic.Create(descriptor, arguments));
```

In `DescribeCommand.PlanAsync`:

```csharp
        var found = RunUnitFinder.Find(unitPath, host.WorkingDirectory);
        if (!found.Succeeded)
        {
            reporter.Report(found.Failure);
            return null;
        }

        var unit = found.Value;
        RunEvents.Unit(reporter.Trace, unit, wasGiven: unitPath is not null);
```

Run the tool's tests.  Expected: all pass.

- [ ] **Step 4: `RunLogFile.Open`**

Tests first.  In `Logging/RunLogFileTests.cs` the five tests of `TryOpen` become these, with `using SqlSource.Diagnostics;` and `using SqlSource.Tool.Reporting;` added if the file does not have them:

```csharp
    [Fact]
    public void Open_RelativePath_MakesTheFileInTheWorkingDirectoryAndEmptiesOneThatIsThere()
    {
        var expected = _folder.WriteFile("run.log", "of an earlier run\n");

        using var log = RunLogFile.Open("run.log", _folder.Path, _clock).Value.ShouldNotBeNull();

        log.Path.ShouldBe(expected);
        log.Write("e");
        log.Dispose();
        File.ReadAllText(expected).ShouldBe(Head + "}\n");
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-such-folder/run.log")]
    [InlineData("folder")]
    public void Open_PathThatCannotBeWritten_GivesNoLogAndTheErrorWithTheReason(string given)
    {
        _ = _folder.CreateFolder("folder");

        var opened = RunLogFile.Open(given, _folder.Path, _clock);
        opened.Value?.Dispose();

        opened.Succeeded.ShouldBeFalse();
        var failure = opened.Failure.ShouldNotBeNull();
        failure.Descriptor.ShouldBeSameAs(ToolDiagnostics.FileCannotBeChanged);
        // The error is at the path, the full one when the system could make one, and quotes it first.
        failure.Arguments[0].ShouldBe(failure.Path);
        failure.Arguments[1].ShouldBe("written");
        failure.Arguments[2].ShouldNotBeEmpty();
    }

    // A slip of the command line, "--log Users.sql", must not empty a file of the run: nothing is opened or made.
    [Theory]
    [InlineData("Users.sql")]
    [InlineData("Users.sql.json")]
    [InlineData("App.csproj")]
    [InlineData("App.sln")]
    [InlineData("App.slnx")]
    [InlineData("USERS.SQL")]
    [InlineData("sub/App.CsProj")]
    public void Open_PathOfAFileOfTheRun_GivesNoLogAndTheReasonAndLeavesTheFileAsItWas(string given)
    {
        var existing = _folder.WriteFile(given, "of the run\n");

        var opened = RunLogFile.Open(given, _folder.Path, _clock);
        opened.Value?.Dispose();

        opened.Failure.ShouldBe(
            ToolDiagnostic.ForFile(
                ToolDiagnostics.FileCannotBeChanged,
                existing,
                "written",
                "a log is not written over a .sql file, a sidecar, a project or a solution"
            )
        );
        File.ReadAllText(existing).ShouldBe("of the run\n");
    }

    [Fact]
    public void Open_PathOfAFileOfTheRunThatIsNotThere_MakesNothing()
    {
        var opened = RunLogFile.Open("New.sql", _folder.Path, _clock);
        opened.Value?.Dispose();

        opened.Succeeded.ShouldBeFalse();
        File.Exists(_folder.PathOf("New.sql")).ShouldBeFalse();
    }

    [Fact]
    public void Open_PathThatOnlyEndsLikeAFileOfTheRun_IsALog()
    {
        var opened = RunLogFile.Open("run.sql.log", _folder.Path, _clock);
        using var log = opened.Value;

        opened.Succeeded.ShouldBeTrue();
    }
```

Then in `RunLogFile.cs` (add `using SqlSource.Diagnostics;` and `using SqlSource.Tool.Reporting;` if the file does not have them), `TryOpen` and its documentation become:

```csharp
    /// <summary>
    /// Makes the file, or empties the one that is there.  When it cannot be, the <c>SQLSRC218</c> that says why:
    /// the system's reason, or that the name is a file of the run (a <c>.sql</c> file, a sidecar, a project or a
    /// solution, whatever the case), which is refused before anything is opened, made or emptied.  The error is
    /// at the full path, or at <paramref name="given" /> when that is no path at all.
    /// </summary>
    /// <param name="given">The path as the option or the variable gave it.</param>
    /// <param name="workingDirectory">The full path a relative path is resolved against.</param>
    /// <param name="time">The clock of the run.</param>
    public static Outcome<RunLogFile, ToolDiagnostic> Open(string given, string workingDirectory, TimeProvider time)
    {
        var path = given;
        try
        {
            var full = System.IO.Path.GetFullPath(given, workingDirectory);
            path = full;
            if (FilesOfTheRun.Any(ending => full.EndsWith(ending, StringComparison.OrdinalIgnoreCase)))
            {
                return NotOpened(path, "a log is not written over a .sql file, a sidecar, a project or a solution");
            }

            return Outcome<RunLogFile, ToolDiagnostic>.Of(
                new RunLogFile(path, new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read), time)
            );
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // An empty path, a directory, a folder that is not there, or a file the system will not open.
            return NotOpened(path, exception.Message);
        }
    }

    private static Outcome<RunLogFile, ToolDiagnostic> NotOpened(string path, string reason) =>
        Outcome<RunLogFile, ToolDiagnostic>.Failed(
            ToolDiagnostic.ForFile(ToolDiagnostics.FileCannotBeChanged, path, "written", reason)
        );
```

If the build reports `CA2000` at `new RunLogFile`, stop and tell the user: the log is closed by the `finally` of `Cli.RunAsync`, and how to show the analyzer that is theirs to choose.

In `Cli.RunAsync`, the block that opens the log becomes (Task 6 rewrites the method around it and keeps this):

```csharp
                if (logPath is { } given)
                {
                    var opened = RunLogFile.Open(given, host.WorkingDirectory, host.Time);
                    if (!opened.Succeeded)
                    {
                        // The run does not start.
                        reporter.Report(opened.Failure);
                        return 1;
                    }

                    log = opened.Value;
                    events = log;
                    log.Write(
```

with the members of `run.start` as they are.

Run the tool's tests.  Expected: all pass; `RunLogTests` holds the error of a log that cannot be opened as the command prints it.

- [ ] **Step 5: Write the rule in `src/SqlSource.Tool/AGENTS.md`**

Add a bullet after the one that starts "**An evaluation reports nothing.**":

```markdown
- **A result that may fail has one shape where files meet.**  A method that another file calls and that gives one value or one reason returns an `Outcome<TValue, TFailure>` (`Outcome.cs`) and reports nothing: its caller does, as `DescribeCommand.PlanAsync` reports what `Projects/RunUnitFinder.cs` found and `Cli.RunAsync` what `RunLogFile.Open` did.  A private helper may be a `TryX`, and so may a method that gives a value or nothing and no reason, as `ConnectionArgument.TryParse`.  A result with more than two states is a record with a state, as `ProjectEvaluation`.  A step that reports several errors as it goes takes the reporter, or returns its errors as data, and is no `Outcome`.
```

- [ ] **Step 6: Check that nothing names what is gone**

```bash
grep -rnE 'SolutionProjects\b|TryOpen|Read\([a-zA-Z]+, out ' src tests --include='*.cs' --include='*.md'
```

Expected: only `solution.SolutionProjects`, the library's member, in `SolutionReader.cs`.

- [ ] **Step 7: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A src tests && git commit -m "Return an Outcome from the four readers that gave a null and a reason

ManifestReader.Read, SolutionReader.ReadAsync, RunUnitFinder.Find and
RunLogFile.Open each give one value or one reason and report nothing; the
caller reports.  SolutionProjects goes.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: A mode, one answer for deleting, and a check that holds its outcome (items 10 and 5)

**Files:**
- Create: `src/SqlSource.Tool/Describing/RunMode.cs`
- Modify: `src/SqlSource.Tool/Describing/DescribeOptions.cs`, `RunDecisions.cs`, `RunSummary.cs`, `FileOutcomes.cs`, `FileCheck.cs`, `SidecarCheck.cs`, `DescribeRun.cs`, `src/SqlSource.Tool/DescribeCommand.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/RunDecisionsTests.cs`, `FileOutcomesTests.cs`, `DatabaseRunsTests.cs`, `tests/SqlSource.Tool.Tests/Commands/FilterRunTests.cs`
- Modify: `src/SqlSource.Tool/AGENTS.md`

**Interfaces:**
- Produces:
  - `internal enum RunMode { Describe, Force, Check }`
  - `DescribeOptions(RunMode Mode, bool HasFilter, ImmutableArray<ConnectionArgument> Connections, ImmutableArray<string> Databases)`
  - `RunDecisions.Decide(RunPlan plan, RunMode mode, ICollection<ToolDiagnostic> errors)`
  - `RunSummary.Lines(planned, selected, files, connections, named, RunMode mode)` and `RunSummary.Counts(selected, files, RunMode mode)`
  - `FileOutcomes.Decide(ImmutableArray<FileWork> files, bool mayDelete, Func<string, SidecarOnDisk> read)`
  - `FileCheck(FileOutcome Outcome, bool WasCompared, EquatableArray<ToolDiagnostic> Differences)`

- [ ] **Step 1: Pin what `FileOutcomes` will no longer see**

In `tests/SqlSource.Tool.Tests/Commands/FilterRunTests.cs`, under `Run_FileThatNeedsNoEntry_LosesItsSidecarInARunWithNoFilter`:

```csharp
    // The plan drops a setting or a claim it could not read, so after an error a file can look as if it needs no
    // entry.  A run that reported anything deletes nothing.
    [Fact]
    public async Task Run_FileThatNeedsNoEntry_KeepsItsSidecarInARunThatReportedAnError()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser", "relation \"users\" does not exist");
        await File.WriteAllTextAsync(
            _plain + ".json",
            "of a query that needed an entry once",
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("SQLSRC999");
        (await File.ReadAllTextAsync(_plain + ".json", TestContext.Current.CancellationToken)).ShouldBe(
            "of a query that needed an entry once"
        );
    }
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Commands.FilterRunTests"`
Expected: all pass.  It is a pin: it passes before the change and must pass after it.

- [ ] **Step 2: Change the tests of the three signatures**

`Describing/RunDecisionsTests.cs`:

- the helper becomes `private QueryState[] States(RunPlan plan, RunMode mode = RunMode.Describe) => [.. RunDecisions.Decide(plan, mode, _errors)[0].Queries.Select(query => query.State)];`
- each `States(<plan>, force: true)` becomes `States(<plan>, RunMode.Force)` (three)
- each `RunDecisions.Decide(plan, force: false, _errors)` becomes `RunDecisions.Decide(plan, RunMode.Describe, _errors)` (four)
- `.Decide(plan, force: true, _errors)` becomes `.Decide(plan, RunMode.Force, _errors)`
- `RunDecisions.Decide(plan, force: true, _errors, check: true)` becomes `RunDecisions.Decide(plan, RunMode.Check, _errors)`

`Describing/DatabaseRunsTests.cs`: `RunDecisions.Decide(plan, force: false, [])` becomes `RunDecisions.Decide(plan, RunMode.Describe, [])`.

`Describing/FileOutcomesTests.cs`:

- in `Described`, `RunDecisions.Decide(plan, force, [])` becomes `RunDecisions.Decide(plan, force ? RunMode.Force : RunMode.Describe, [])`
- the helper becomes

```csharp
    private static FileOutcome Outcome(ImmutableArray<FileWork> files, bool mayDelete = true) =>
        FileOutcomes.Decide(files, mayDelete, SidecarOnDisk.Read)[0];
```

- each `runHasFilter: true` becomes `mayDelete: false` (three)
- the theory `Decide_FileThatNeedsNoEntry_LosesItsSidecarOnlyInARunWithNoFilterAndNothingReported` and the comment above it become

```csharp
    // Which run may delete is DescribeRun's to say: one with no filter that reported nothing.  FilterRunTests holds
    // that.
    [Theory]
    [InlineData(true, true, nameof(FileAction.Delete))]
    [InlineData(false, true, nameof(FileAction.None))]
    [InlineData(true, false, nameof(FileAction.None))]
    public void Decide_FileThatNeedsNoEntry_LosesItsSidecarOnlyInARunThatMayDeleteAndWhenThereIsOne(
        bool mayDelete,
        bool sidecarExists,
        string action
    )
    {
        var project = Plans.Postgres(_folder, "Other");
        var sql = project.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 3;\n");
        if (sidecarExists)
        {
            File.WriteAllText(sql + ".json", "anything");
        }

        Outcome(Described(Plans.Of(project)), mayDelete).Action.ShouldBe(Enum.Parse<FileAction>(action));
    }
```

Run `dotnet build tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`; expected: it fails, `The name 'RunMode' does not exist in the current context`.

- [ ] **Step 3: Add `RunMode`**

`src/SqlSource.Tool/Describing/RunMode.cs`:

```csharp
namespace SqlSource.Tool.Describing;

/// <summary>
/// What a run of <c>describe</c> does with the queries it selected and with the sidecars on the disk.
/// </summary>
internal enum RunMode
{
    /// <summary>A selected query whose entry is current is skipped, and the sidecars are written.</summary>
    Describe,

    /// <summary><c>--force</c>: a selected query is described though its entry is current.</summary>
    Force,

    /// <summary>
    /// <c>--check</c>: every selected query is described, as under <c>--force</c>, the sidecars on the disk are
    /// compared with what was described, and no file is changed.
    /// </summary>
    Check,
}
```

- [ ] **Step 4: `DescribeOptions` and `DescribeCommand.OptionsOf`**

In `DescribeOptions.cs`, the parameters `Force` and `Check` and their two `<param>` elements become one:

```csharp
/// <param name="Mode">Whether the run skips what is current, describes it all the same, or only compares.</param>
```

```csharp
internal sealed record DescribeOptions(
    RunMode Mode,
    bool HasFilter,
    ImmutableArray<ConnectionArgument> Connections,
    ImmutableArray<string> Databases
);
```

In `DescribeCommand.OptionsOf`, the two `parsed.GetValue<bool>(...)` arguments become `ModeOf(parsed)`, and under `OptionsOf`:

```csharp
    // --check beside --force is --check: it describes every query, as --force does, and writes nothing.
    private static RunMode ModeOf(ParseResult parsed) =>
        (parsed.GetValue<bool>(CheckOption), parsed.GetValue<bool>(ForceOption)) switch
        {
            (true, _) => RunMode.Check,
            (false, true) => RunMode.Force,
            _ => RunMode.Describe,
        };
```

- [ ] **Step 5: `RunDecisions.Decide`**

The `<param>` elements of `force` and `check` become:

```csharp
    /// <param name="mode">
    /// Under <see cref="RunMode.Force" /> and <see cref="RunMode.Check" /> a selected query is described though its
    /// entry is current.  Under <see cref="RunMode.Check" />, which writes nothing, a sidecar of a newer tool is no
    /// error, and the queries of its file are described as any other: the comparison reports the file as a
    /// difference.
    /// </param>
    /// <param name="errors">Gains <c>SQLSRC221</c> for each sidecar of a newer tool, in a run that writes.</param>
```

and the signature and the first lines of the body:

```csharp
    public static ImmutableArray<FileWork> Decide(RunPlan plan, RunMode mode, ICollection<ToolDiagnostic> errors)
    {
        var force = mode != RunMode.Describe;
        var check = mode == RunMode.Check;
        var files = ImmutableArray.CreateBuilder<FileWork>(plan.Files.Count);
```

The rest of the body, and the private `Decide` of one query, stay.

- [ ] **Step 6: `RunSummary`**

In `Lines`, the parameter `bool check` becomes `RunMode mode`, its `<param>` becomes `/// <param name="mode">The run's mode: the counts of <c>--check</c> are <c>same</c>, <c>differ</c> and <c>failed</c>.</param>`, and the body starts with `var check = mode == RunMode.Check;`.

The public `Counts` becomes:

```csharp
    /// <param name="mode">The run's mode: <c>--check</c> counts in its own words.</param>
    public static LogValue Counts(
        ImmutableArray<PlannedDatabase> selected,
        ImmutableArray<FileWork> files,
        RunMode mode
    )
    {
        var check = mode == RunMode.Check;
        return LogValue.ListOf(selected.Select(database => Counts(database, files, check)));
    }
```

The private `Counts` of one database keeps its `bool check`.

- [ ] **Step 7: `FileOutcomes.Decide`**

The two `<param>` elements of `runHasFilter` and `anythingWasReported` become one, and both signatures take `bool mayDelete` in their place:

```csharp
    /// <param name="mayDelete">
    /// Whether the run may delete a sidecar, which <see cref="DescribeRun" /> decides.  A run with
    /// <c>--project</c>, <c>--database</c> or a <c>.sql</c> path may not: a run on a part does not know what the
    /// rest needs of a file.  Nor may a run that has reported an error before this step: the plan drops a setting
    /// it cannot read and a claim it cannot read, so a file can look as if it needs no entry only because of the
    /// error.
    /// </param>
```

```csharp
    public static ImmutableArray<FileOutcome> Decide(
        ImmutableArray<FileWork> files,
        bool mayDelete,
        Func<string, SidecarOnDisk> read
    ) => [.. files.Select(file => Decide(file, mayDelete, read))];

    private static FileOutcome Decide(FileWork file, bool mayDelete, Func<string, SidecarOnDisk> read)
```

and in the body:

```csharp
            // A sidecar that would be empty.
            var deletes = mayDelete && read(file.File.Path) is { Text: not null, NewerFormat: null };
```

- [ ] **Step 8: `FileCheck` holds its `FileOutcome`**

`FileCheck.cs`: the `<param>` elements of `File` and `SidecarPath` become one, and the record loses `using SqlSource.Tool.Planning;` if nothing else there needs it:

```csharp
/// <param name="Outcome">
/// What <see cref="FileOutcomes" /> decided for the file, with the file of the plan and the full path of its
/// sidecar.
/// </param>
```

```csharp
internal sealed record FileCheck(FileOutcome Outcome, bool WasCompared, EquatableArray<ToolDiagnostic> Differences);
```

`SidecarCheck.cs`: the `new FileCheck(file.File, outcome.SidecarPath, WasCompared: false, EquatableArray<ToolDiagnostic>.Empty)` becomes `new FileCheck(outcome, WasCompared: false, EquatableArray<ToolDiagnostic>.Empty)`, and `Of` becomes:

```csharp
    private static FileCheck Of(FileOutcome outcome, IEnumerable<ToolDiagnostic> differences) =>
        new(outcome, WasCompared: true, new EquatableArray<ToolDiagnostic>([.. differences]));
```

- [ ] **Step 9: `DescribeRun`**

```csharp
        // 1 and 2: what is there, and what each query needs.
        var errors = new List<ToolDiagnostic>();
        var files = RunDecisions.Decide(plan, options.Mode, errors);
```

(the comment "--check describes every selected query, as --force does." goes: the mode's documentation says it), and step 4:

```csharp
        // 4: a run that was cancelled changes no file.
        cancellationToken.ThrowIfCancellationRequested();
        // A sidecar is deleted only by a run on the whole, which knows what every file needs, and never after an
        // error: the plan drops a setting or a claim it could not read, and the file then looks as if it needs no
        // entry.
        var mayDelete = !options.HasFilter && reporter.Count == 0;
        var outcomes = FileOutcomes.Decide(files, mayDelete, SidecarOnDisk.Read);
        if (options.Mode == RunMode.Check)
```

Inside that block `check.SidecarPath` becomes `check.Outcome.SidecarPath`.  Below it, the two `options.Check` that are passed to `RunSummary.Counts` and `RunSummary.Lines` become `options.Mode`.

- [ ] **Step 10: `src/SqlSource.Tool/AGENTS.md`**

In the bullet that starts "**A filter narrows what a run may read and change**", "`FileOutcomes.Decide` takes both as parameters, and a function that reads the sidecar (`SidecarOnDisk.Read`)" becomes "`Describing/DescribeRun.cs` decides both and gives `FileOutcomes.Decide` the one answer, with a function that reads the sidecar (`SidecarOnDisk.Read`)".

- [ ] **Step 11: Run the tool's tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: all pass.  `CheckTests` holds `--check` beside `--force`; `FilterRunTests` and `CheckFilterTests` hold what a run may delete and what a check reports.

```bash
grep -rnE 'runHasFilter|anythingWasReported|options\.(Force|Check)\b|check: true' src tests --include='*.cs' --include='*.md'
```

Expected: no line.

- [ ] **Step 12: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A src tests && git commit -m "Give a run its mode, and FileOutcomes one answer for whether it may delete

RunMode replaces the two booleans of --force and --check that were passed
on as three.  DescribeRun decides whether a run may delete a sidecar, and
FileCheck holds the FileOutcome it was built from.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: One read of a file's text (item 11)

**Files:**
- Create: `src/SqlSource.Tool/Planning/FileText.cs`
- Create: `tests/SqlSource.Tool.Tests/Planning/FileTextTests.cs`
- Modify: `src/SqlSource.Tool/Planning/SqlFileText.cs`, `src/SqlSource.Tool/Planning/AttributeReader.cs`

**Interfaces:**
- Produces: `FileText.Read(string path)` returns `SourceText?`, null when the system does not let the file be read.

- [ ] **Step 1: Write the failing tests**

`tests/SqlSource.Tool.Tests/Planning/FileTextTests.cs`:

```csharp
using System;
using Shouldly;
using SqlSource.Tool.Planning;
using Xunit;

namespace SqlSource.Tool.Tests.Planning;

public sealed class FileTextTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Read_FileThatIsThere_GivesItsText()
    {
        var path = _folder.WriteFile("Q.sql", "SELECT 1;\n");

        FileText.Read(path).ShouldNotBeNull().ToString().ShouldBe("SELECT 1;\n");
    }

    [Fact]
    public void Read_FileThatIsGone_GivesNull() => FileText.Read(_folder.PathOf("Gone.sql")).ShouldBeNull();

    [Fact]
    public void Read_Folder_GivesNull() => FileText.Read(_folder.CreateFolder("Q.sql")).ShouldBeNull();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Planning.FileTextTests"`
Expected: the build fails, `The name 'FileText' does not exist in the current context`.

- [ ] **Step 3: Write `FileText` and read through it**

`src/SqlSource.Tool/Planning/FileText.cs`:

```csharp
using System;
using System.IO;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Tool.Planning;

/// <summary>
/// Reads a file of a project as the text the generator's code takes: a <c>.sql</c> file for the parser, a source
/// file for <see cref="AttributeReader" />.
/// </summary>
internal static class FileText
{
    /// <summary>
    /// The text of the file, or null when the system does not let it be read.  The caller says what a file with no
    /// text means.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    public static SourceText? Read(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return SourceText.From(stream);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
```

In `SqlFileText.cs`, `GetText` becomes:

```csharp
    public override SourceText? GetText(CancellationToken cancellationToken = default) => FileText.Read(path);
```

In `AttributeReader.cs`, `ReadText(path)` becomes `FileText.Read(path)` and the private `ReadText` goes.  In both files remove each `using` that nothing needs any more; the build names them.

- [ ] **Step 4: Run the tool's tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: all pass.

- [ ] **Step 5: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A src tests && git commit -m "Read a file's text in one place, for the parser and for the reader of attributes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: A reporter made with its trace, and no order to remember (item 11)

**Files:**
- Modify: `src/SqlSource.Tool/Reporting/Reporter.cs`, `src/SqlSource.Tool/Logging/RunTrace.cs`
- Modify: `src/SqlSource.Tool/Describing/DescribeRun.cs`, `src/SqlSource.Tool/DescribeCommand.cs`, `src/SqlSource.Tool/Cli.cs`
- Modify: `tests/SqlSource.Tool.Tests/Reporting/ReporterTests.cs`, `Commands/CliTests.cs`, `Commands/UsageCheckTests.cs`, `Describing/ConnectionTests.cs`, `CliRun.cs`
- Modify: `src/SqlSource.Tool/AGENTS.md`

**Interfaces:**
- Consumes: `RunLogFile.Open` of Task 3; `DescribeOptions.Mode` of Task 4.
- Produces:
  - `Reporter(TextWriter error, RunTrace? trace = null)`, with `RunTrace Trace { get; }`
  - `DescribeRun.RunAsync(RunPlan plan, DescribeOptions options, ToolHost host, Reporter reporter, Action<LogValue> counted, CancellationToken cancellationToken)`
  - `DescribeCommand.Create()`, and `DescribeCommand.Bind(ParseResult parsed, ToolHost host, Reporter reporter, Action<LogValue> counted)`
  - `Cli.BuildCommands()`

System.CommandLine takes a command's action when it invokes and not when it parses: an action that is set on `parsed.CommandResult.Command` after `Parse` is the one that runs.  This was checked against 2.0.12 before the plan was written.

- [ ] **Step 1: Pin the counts of a run whose summary cannot be written**

In `Commands/CliTests.cs`, which has `FailingWriter`, under `Run_ExceptionWhoseMessageHasLineBreaks_KeepsTheErrorOnOneLine` (add the `using` directives the build asks for):

```csharp
    // The counts are the run's, and the files were written: a summary that cannot be printed does not lose them.
    [Fact]
    public async Task Run_SummaryThatCannotBeWritten_StillEndsTheLogWithTheCounts()
    {
        using var run = new CliRun { Out = new FailingWriter("the pipe is closed") };
        run.Describers.Add(new FakeDescriber());
        var project = Plans.Postgres(run.Folder, directory: "").AnsweredBy(run.Processes);
        run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = DescribeScene.Connection;
        _ = project.AddSql("Users.sql", "-- name: GetUser\nSELECT 1;\n");

        var result = await run.RunAsync("describe", "--log", "run.log");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith("sqlsource : error SQLSRC200: ");
        var end = LogReader.Read(run.Folder.PathOf("run.log"))[^1];
        end.GetProperty("event").GetString().ShouldBe("run.end");
        var database = end.GetProperty("databases").EnumerateArray().ShouldHaveSingleItem();
        database.GetProperty("described").GetInt32().ShouldBe(1);
    }
```

It is a pin: it passes before this task changes anything and must pass after it.

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Commands.CliTests"`
Expected: all pass.

- [ ] **Step 2: Write the failing test of the reporter**

In `Reporting/ReporterTests.cs` (add `using SqlSource.Tool.Logging;`):

```csharp
    [Fact]
    public void Report_ReporterMadeWithATrace_WritesTheErrorAsAnEventOfItsLog()
    {
        var log = new RecordingLog();
        var reporter = new Reporter(_error, new RunTrace(log));

        reporter.Report(Failure);

        log.Events.ShouldHaveSingleItem().Name.ShouldBe("diagnostic");
        reporter.Trace.Log.ShouldBeSameAs(log);
    }
```

Run: `dotnet build tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: it fails, `'Reporter' does not contain a constructor that takes 2 arguments`.

- [ ] **Step 3: `Reporter` and `RunTrace`**

`Reporter.cs`: the declaration becomes

```csharp
/// <param name="error">Where the errors are written.</param>
/// <param name="trace">
/// What the run says of itself beside its errors, when it has one: every error is then also an event of its log.
/// </param>
internal sealed class Reporter(TextWriter error, RunTrace? trace = null)
```

and the property with its documentation becomes

```csharp
    /// <summary>
    /// What the run says of itself beside its errors.  <c>Cli</c> makes the reporter of a <c>describe</c> that runs
    /// with the run's, which has its log when there is one.  A reporter that was given none says nothing.
    /// </summary>
    public RunTrace Trace { get; } = trace ?? new RunTrace(NullRunLog.Instance);
```

`RunTrace.cs`: the property `Databases` and its documentation go.

- [ ] **Step 4: `DescribeRun` hands over the counts**

The signature gains `Action<LogValue> counted` before the token (add `using System;`), and the method gains documentation:

```csharp
    /// <param name="plan">The plan of the run.</param>
    /// <param name="options">What the command line says of the run, beside its plan.</param>
    /// <param name="host">The describers, the environment and standard output.</param>
    /// <param name="reporter">Where the errors go, with the run's trace.</param>
    /// <param name="counted">
    /// Given the counts of each selected database, as <c>run.end</c> holds them, before the summary is written: a
    /// summary that cannot be written does not lose them.
    /// </param>
    /// <param name="cancellationToken">Ends the run.</param>
    public static async Task RunAsync(
        RunPlan plan,
        DescribeOptions options,
        ToolHost host,
        Reporter reporter,
        Action<LogValue> counted,
        CancellationToken cancellationToken
    )
```

and `trace.Databases = RunSummary.Counts(selected, files, options.Mode);` becomes `counted(RunSummary.Counts(selected, files, options.Mode));`.

- [ ] **Step 5: `DescribeCommand.Create` and `Bind`**

`Create` takes no parameter, and its `describe.SetAction(...)` call goes, so that it ends:

```csharp
        return new Command(Name, "Asks the databases of the projects to describe their queries")
        {
            path,
            project,
            database,
            connection,
            force,
            check,
            verbose,
            log,
        };
    }

    /// <summary>
    /// Gives the <c>describe</c> that a command line runs its action, once the run has its reporter.
    /// System.CommandLine takes a command's action when it invokes, not when it parses.
    /// </summary>
    /// <param name="parsed">A command line for which <see cref="Runs" /> is true.</param>
    /// <param name="host">What the run takes from outside.</param>
    /// <param name="reporter">Where the errors go, with the run's trace.</param>
    /// <param name="counted">Given the counts of each selected database, as <c>run.end</c> holds them.</param>
    public static void Bind(ParseResult parsed, ToolHost host, Reporter reporter, Action<LogValue> counted) =>
        parsed.CommandResult.Command.SetAction(
            async (command, cancellationToken) =>
            {
                // An error is in the reporter, where the exit code is taken.
                if (await PlanAsync(command, host, reporter, cancellationToken) is { } plan)
                {
                    await DescribeRun.RunAsync(plan, OptionsOf(command), host, reporter, counted, cancellationToken);
                }

                return 0;
            }
        );
```

Add `using SqlSource.Tool.Logging;` if the file does not have it.  The summary of `Runs` gains a sentence: "Until <see cref="Bind" /> gives the command its action it has none, and neither has a command line that runs it, so the answer is the same before and after."

- [ ] **Step 6: `Cli`**

`BuildCommands` takes no parameter and adds `DescribeCommand.Create()`.

`RunAsync` becomes, with everything from `var usage` to the `return 1;` of "the command line is not valid" as it is:

```csharp
    internal static async Task<int> RunAsync(string[] args, ToolHost host, CancellationToken cancellationToken)
    {
        // Until the run has its trace an error has the console alone: a log that cannot be opened, or an
        // exception.  Neither is followed by an error of the run, so no count is split between two reporters.
        var reporter = new Reporter(host.Error);
        var started = host.Time.GetTimestamp();
        RunLogFile? log = null;
        var databases = LogValue.ListOf([]);
        var exitCode = 0;

        // Whether the line that ends a failed run has a reader: "describe" runs, and no log was asked for.
        var mayPoint = false;
        var cancelled = false;
        try
        {
            try
            {
                // A run that was cancelled prints nothing more, whatever it was asked to do.
                cancellationToken.ThrowIfCancellationRequested();

                var root = BuildCommands();

                // ... the usage check and the parse, unchanged ...

                // Whether the command line runs "describe", and the log it was given, once.  A log that cannot be
                // opened returns below, before the line that ends a failed run is ever considered.
                var runs = DescribeCommand.Runs(parsed);
                var logPath = DescribeCommand.LogPathOf(parsed, host.GetEnvironmentVariable);
                mayPoint = runs && logPath is null;

                // What the run says of itself: to the log, when it has one, and to the person watching under
                // --verbose.
                IRunLog events = NullRunLog.Instance;

                // After the command line was read, so that a wrong one is never logged, and before anything runs.
                if (logPath is { } given)
                {
                    var opened = RunLogFile.Open(given, host.WorkingDirectory, host.Time);
                    if (!opened.Succeeded)
                    {
                        // The run does not start.
                        reporter.Report(opened.Failure);
                        return 1;
                    }

                    log = opened.Value;
                    events = log;
                    log.Write(
                        "run.start",
                        new("toolVersion", PackageVersion.Prefix),
                        new("formatVersion", SidecarFormat.Version),
                        new("runtime", RuntimeInformation.FrameworkDescription),
                        new("os", RuntimeInformation.OSDescription),
                        new("arguments", LoggedArguments.Of(usage, args))
                    );
                }

                if (runs)
                {
                    // The reporter of the run: from here on every error is also an event of the log.  The command
                    // gets its action only now, so that it cannot run with another.
                    reporter = new Reporter(
                        host.Error,
                        new RunTrace(events, DescribeCommand.IsVerbose(parsed) ? host.Out : null)
                    );
                    DescribeCommand.Bind(parsed, host, reporter, counts => databases = counts);
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
```

The three `catch` blocks and what follows them stay, but for the last line of the outer `try`:

```csharp
            return log is null ? exitCode : End(log, exitCode, started, databases, host, reporter);
```

and `End`:

```csharp
    private static int End(
        RunLogFile log,
        int exitCode,
        long started,
        LogValue databases,
        ToolHost host,
        Reporter reporter
    )
    {
        log.Write(
            "run.end",
            new("exitCode", exitCode),
            new("durationMs", (long)host.Time.GetElapsedTime(started).TotalMilliseconds),
            new("databases", databases)
        );
```

with the rest of `End` as it is.

- [ ] **Step 7: The tests that build the commands**

`Cli.BuildCommands(<host>, <reporter>)` becomes `Cli.BuildCommands()` in `CliRun.cs` (the reporter of `PlanAsync` stays: `DescribeCommand.PlanAsync` takes it), `Commands/UsageCheckTests.cs` (two), `Commands/CliTests.cs` and `Describing/ConnectionTests.cs`.  Remove each local, such as a host or a temporary folder, and each `using` that only the old arguments needed; the build names them.

- [ ] **Step 8: `src/SqlSource.Tool/AGENTS.md`**

In the bullet that starts "**`Logging/` is what a run says of itself**":

- "It sets `Reporter.Trace` for every `describe` that runs, with the log when there is one and with standard output when `--verbose` was given, and writes `run.start` and `run.end` there, since the exit code and the error of an exception are known only there; `Reporter` writes a `diagnostic` event for each error, under the same lock as the console, through `Reporter.Trace`." becomes "For every `describe` that runs it makes the run's `Reporter` with its `RunTrace`, which has the log when there is one and standard output when `--verbose` was given, and only then gives the command its action (`DescribeCommand.Bind`), so that the command cannot run with another reporter; an error before that has a reporter with no trace.  It writes `run.start` and `run.end`, since the exit code and the error of an exception are known only there; `Reporter` writes a `diagnostic` event for each error, under the same lock as the console, to the trace it was made with."
- "`describe` sets `RunTrace.Databases` from `RunSummary.Counts`, the counts that `run.end` ends with." becomes "`Describing/DescribeRun.cs` hands `Cli` the counts that `run.end` ends with, from `RunSummary.Counts`, before it writes the summary, so that a summary that cannot be written does not lose them."

- [ ] **Step 9: Run everything**

Run: `dotnet test --solution SqlSource.slnx`
Expected: all pass.  `RunLogTests`, `VerboseTests`, `RedactionTests` and `SecretTests` hold the log and the lines; `DescribeTests` and `CliTests` hold the help and the version, which run with no action bound.

```bash
grep -rnE 'Trace = |\.Databases = |RunTrace\.Databases|BuildCommands\([a-zA-Z]' src tests --include='*.cs' --include='*.md'
```

Expected: no line.

- [ ] **Step 10: Format, validate, commit**

```bash
./format.sh
```

```bash
./pre-commit-validation.sh
```

```bash
git add -A src tests && git commit -m "Make a run's reporter with its trace, and bind describe when it exists

Reporter.Trace and RunTrace.Databases were settable properties that Cli had
to assign in order.  The trace is now given to the reporter's constructor,
describe gets its action once the run's reporter exists, and DescribeRun
hands over the counts that run.end ends with.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Rewrite TD-0038, open the pull request

**Files:**
- Modify: `docs/tech-debt/TD-0038-duplicated-and-uneven-patterns-in-the-generator-and-the-tool.md`
- Modify: `docs/tech-debt/README.md` (the row of TD-0038)
- Add: `docs/superpowers/plans/2026-10-11-td-0038-pr2-tool-results.md` (this file), and the spec as it was amended for this pull request

- [ ] **Step 1: Rewrite TD-0038 to what remains**

In the item's file, keeping the numbers of the items that stay:

- Under **Problem**, the second paragraph becomes: `Items 1 to 6 and 8 of the review are resolved, and so is the tool's part of 10 and 11; the numbers of the others are kept, written out so that a renderer does not count them again, since the [design](../superpowers/specs/2026-10-10-td-0038-duplicated-patterns-design.md) names them.`
- Delete the items **5** and **6**.
- Item **10** becomes: `- **10. A boolean cascade.**  `SqlMarkerScope.Read(marker, inQuery, inPreamble)`, where both true is a third state.`
- Item **11** becomes: `- **11. Small leftovers.**  `SqlSourceGenerator.ToSortedFiles` and `ToSortedSettings` are one method in two element types.  `SidecarComparer.Json` re-implements the scalar formatting of `SidecarJsonBuilder`.  `SettingValue.IsTypeName` and `SqlDialectMarker.Describe` forward to one other method.`
- Under **Why it exists**, the last sentence becomes: `Item 7 came from readers that were written to allocate nothing and copied the one way to do that.`
- Under **Impact**, the text becomes: `Low.  Nothing here is wrong today, and the tests hold each pair together where they could drift.  Each item makes a change touch more places than a reader would expect.`
- Under **Proposed fix**, the first line becomes `As the [design](../superpowers/specs/2026-10-10-td-0038-duplicated-patterns-design.md) says, in its pull requests 3 and 4, each held by the existing tests:`; delete the entries **5** and **6**; entry **10** becomes `- **10.** A scope kind of `Preamble`, `Query`, `WholeFile`.`; entry **11** becomes `- **11.** Delete, inline or merge each.`
- Under **Trigger**: `Before the next phase that changes `Parsing/`, which adds to 7 and 10, or one of the methods of 9.`

Item 9 stays as it is: `Cli.RunAsync` and the others are pull request 4's.

In `docs/tech-debt/README.md`, the description of the row becomes:

> Duplicated and uneven patterns in `src/`: duplicated text helpers in `Parsing/`, methods of a hundred lines, a boolean cascade in `SqlMarkerScope.Read`, and forwarding or duplicated members of the generator

The status stays `Open`.

- [ ] **Step 2: Validate and commit the documents**

```bash
./pre-commit-validation.sh
```

```bash
git add -A docs && git commit -m "Rewrite TD-0038 to what remains after the tool's results and errors, and add the plan

The design says that a describer names Outcome in full, how Cli holds a
reporter before the run has its trace, and which TryX is no Outcome.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 3: Check the version**

```bash
git fetch --tags origin
```

```bash
git tag --list 'v*' --sort=-v:refname | head -n 1
```

Expected: no output, as on 2026-10-11: the repository has no release tag, so there is nothing to compare.  If a tag is printed, compare it with `VersionPrefix` in `Directory.Build.props`, and ask the user for the new version if `VersionPrefix` is not greater.

- [ ] **Step 4: Ask the user, then push and open the pull request**

Pushing and opening a pull request are seen outside this machine: ask the user first.  Then:

```bash
git push -u origin claude/td-0038-tool-results
```

Open the pull request against `main` with `gh pr create`.  Its description says: what TD-0038 is and that this is the second of four pull requests of the design; the items resolved, one line each (5, 6, and the tool's part of 10 and 11); that nothing a user sees changes; the two places where the plan departs from the design, from the top of this file; and that TD-0038 is rewritten, not deleted.  End it with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.

Then follow the repository's rules for a pull request: reply in its thread to every CodeRabbit finding, with what changed or with why not.
