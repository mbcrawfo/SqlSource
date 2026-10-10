# Query generation, phase 2.5: describe - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver `sqlsource describe` whole but for an engine: the interfaces a describer implements, connections, the rules for what is described, skipped and written, and the sidecars on disk.  No describer is registered, so the released command reports every query that must be described as `SQLSRC216`; a describer of the tests proves the rest.

**Architecture:** A new folder, `src/SqlSource.Tool/Describing/`.  The run is five units that each do one step and hand data to the next: `RunDecisions` reads the sidecars that are there and gives each query a state; `Connections` finds each selected database's connection; `DatabaseRuns` opens one session for each database and describes its queries one at a time, reporting as it goes; `FileOutcomes` gives each file its outcome and its target sidecar, and `SidecarStore` applies the outcomes to the disk; `RunSummary` gives the lines of the summary.  `DescribeRun` calls them in order, and `DescribeCommand` reads the command line into its options.  The first seven tasks build the units with tests of their own and leave the command as it is; task 9 puts the command on them.

**Tech Stack:** C# on `net8.0` in `src/SqlSource.Tool` with System.CommandLine 2.0.12; the sidecar model, reader and writer of `src/SqlSource/Snapshot/`; xunit v3 on Microsoft.Testing.Platform and Shouldly in `tests/SqlSource.Tool.Tests` on `net10.0`.

**Spec:** [`docs/superpowers/specs/2026-10-09-query-generation-phase-2-5-describe-design.md`](../specs/2026-10-09-query-generation-phase-2-5-describe-design.md).  Read it first; this plan argues from it.  The [epic outline](../specs/2026-10-07-query-generation-epic-design.md) holds what the spec builds on: its sections "Workflow", "Diagnostics" and "Phase 2".  The [spec of sub-phase 2.6](../specs/2026-10-09-query-generation-phase-2-6-check-and-logging-design.md) says what builds on this one: it replaces `SidecarStore` with a comparison and gives `OpenAsync` a log.

## Global Constraints

- Branch: `claude/query-generation-phase-2-5-describe`, which exists and holds the reviewed spec.  One commit per task, in task order.
- Every commit passes `./pre-commit-validation.sh`, which needs Docker running.  **The closing steps of every task** are: run `./format.sh`; run `./pre-commit-validation.sh` as its own command and fix what it reports; then commit in a separate command.  A hook runs the validation again before any Bash command that contains the words of the commit command; never work around it, and keep those words out of any other command.
- **The code of this plan was written against the repository as it stood and was not compiled.**  Where the compiler, the formatter or an analyzer objects, change the code to satisfy it and keep the names and the behaviour that the **Interfaces** blocks and the tests give.  Never suppress a rule, and never edit `.editorconfig`.
- `src/SqlSource` stays on `netstandard2.0` and Roslyn 4.8.0 and takes no new package reference.  This plan adds seven descriptors to it and changes nothing else there.  The tool targets `net8.0`: do not raise it, and do not use an API that .NET 8 lacks.  No project takes a new package reference.
- **No value of a connection is ever printed.**  The tool hands a value to `OpenRequest` and to nothing else.  A type that holds one overrides `ToString` to leave it out.  Every text that a describer gave is printed through `Redaction`, which writes the whole value as `***`.
- Nothing in the tool reads `Console`, `Environment` or the current directory except `ToolHost.Create` and `Cli.Run`.  The tool reads and writes files directly, as `Planning/SqlFileText.cs` does.
- Every error goes through `Reporter` with a descriptor.  A wrong command line is the exception: its lines have no id, do not go through the reporter, and **never repeat a token's text**; a line names an option the tool has, or a position counted from one.
- The new diagnostics are `SQLSRC213` to `SQLSRC218` and `SQLSRC221`, with the titles and messages of the spec's table.  Each touches four places: the descriptor and `All` in `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md` (the table row and the section), and a test in `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`.  `All`, the release file and the document are in the order of the ids: `SQLSRC213` to `SQLSRC218` go between `SQLSRC212` and `SQLSRC220`, and `SQLSRC221` between `SQLSRC220` and `SQLSRC222`.
- A path in a message is a full path.  Database names are compared with `StringComparer.OrdinalIgnoreCase`, query names and the names of variables with `StringComparer.Ordinal`.
- Exit codes: `0` success; `1` an error was reported, a wrong command line, or a cancelled run.  `Cli.RunAsync` takes `1` from `Reporter.Count`: a command reports and goes on, and never returns `1` itself.
- A file is at most 120 characters wide, apart from Markdown: `tools/editorconfig-checker.sh` rejects a longer line.  `./format.sh` breaks the code of this plan where it is wider, but never a string: split a string literal that is too wide with `+`, at a word.
- A file this plan gives in full ends with one line break, which the block that shows it does not.
- Types under `src/` carry XML documentation as their neighbours do; test files use `//` comments.
- Prose uses two spaces after a full stop, as the repository does.  Files under `docs/superpowers/` are not rewritten, apart from the epic outline in tasks 1 and 11.
- `README.md` at the root, `CONTRIBUTING.md` and `docs/publishing.md` do not change.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Before the pull request: `git fetch --tags origin`, then `git tag --list 'v*' --sort=-v:refname | head -n 1`.  When this plan was written there was no `v*` tag and nothing to compare.  If there is one now and `VersionPrefix` in `Directory.Build.props` is not greater, ask the owner for the new version.

Commands used throughout:

```bash
dotnet build SqlSource.slnx
```

```bash
dotnet test --solution SqlSource.slnx
```

One class of the tool's tests:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.DescribeRunTests
```

## Where this plan departs from the spec

1. **Eleven commits, not nine.**  The spec's step 2 is tasks 2 and 3 here, and its step 8 is tasks 9 and 10.  Tasks 2 to 8 build units that the command does not call yet; task 9 puts `describe` on them, so the run-level classes of the spec's test table, `DescribeRunTests`, `SummaryTests`, `FilterRunTests` and `SecretTests`, arrive in tasks 9 and 10.  The units have classes of their own that the spec does not name: `RunDecisionsTests`, `DatabaseRunsTests`, `FileOutcomesTests`.
2. **A describer's exception is caught where the describer is called**, in `DatabaseRuns`, and thrown again as a `DescriberFaultException` that holds the `SQLSRC200` with the connection's value removed.  `Cli.RunAsync` reports it.  The tool's `AGENTS.md` says an exception is caught in `Cli` and nowhere else; this is the one other place, and task 6 says so there.
3. **`OpenResult` and `DescribeResult` are made only through two factories each**, so that neither can hold both a value and a failure, or neither.
4. **`DescribeFailure.Step` is an enum, `DescribeStep`, and may be null**: a failure to open a session has none of the epic's steps.
5. **A failure to open a session is reported in the format of a describer's error, with the `query:` line of the database's first query to describe** and no server version.
6. **The line of a wrong `--connection`** is `sqlsource: the value of option '--connection' at position <n> is not <name>=<connection string>`, and of a second value for one name `sqlsource: option '--connection' at position <n> names a database that an earlier one names`.
7. **`SQLSRC217` names each query that held the file back on a line labelled `query`**: `GetUser: failed`, or `GetUser: not in this run`.
8. **A sidecar that the system will not let the tool read is treated as absent**, as one that cannot be parsed is.  Writing it then fails with `SQLSRC218`.
9. **Ctrl+C a second time ends the process**, and `docs/tech-debt/TD-0024` is rewritten to hold the one gap that is left.  That item names this sub-phase as its trigger: its first gap is what the spec's rule on a describer's text settles, and its second is a run that waits on a database.
10. **The epic outline gains one line under "Settled"**, for the rule that a describer's text is printed with the connection's value removed.

## Review Focus

What the spec implies and its own test table does not name.  Each has a test in the task that owns the code.

1. **A sidecar path that is a directory, or a file the system will not open.**  Nothing throws: the sidecar is treated as absent, and writing it is `SQLSRC218` with no temporary file left.  Tasks 5 and 8.
2. **A describer that returns nothing, or throws when its session is closed.**  `SQLSRC200`, without the connection's value, and no sidecar is written.  Task 6.
3. **A run that is cancelled while a query is described.**  Exit `1`, nothing printed after, no sidecar written and no temporary file.  Task 9.
4. **A sidecar that an editor or git saved with a byte order mark and `\r\n`.**  With the same content it is not written again.  Task 7.
5. **A database with no connection whose only queries that are not current failed in the plan.**  No `SQLSRC213`: nothing of it is to be described.  Its summary line has the three counts, since one failed.  Tasks 6 and 9.

---

### Task 1: The outline says In progress

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`
- Create: `docs/superpowers/plans/2026-10-10-query-generation-phase-2-5-describe.md` (this file, already written)

- [ ] **Step 1: Set the row of 2.5**

In the table under `## Phases`, in the row that starts `| 2.5 Describe |`, replace `| Not started |` with `| In progress |`.

- [ ] **Step 2: Closing steps, and commit**

```bash
git add docs/superpowers/specs/2026-10-07-query-generation-epic-design.md docs/superpowers/plans/2026-10-10-query-generation-phase-2-5-describe.md
git commit -m "Start phase 2.5: describe

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The describer's interfaces, the registry and the exchange

**Files:**
- Create in `src/SqlSource.Tool/Describing/`: `IQueryDescriber.cs`, `IDescribeSession.cs`, `OpenRequest.cs`, `OpenResult.cs`, `ServerInfo.cs`, `ServerSetting.cs`, `DescribeRequest.cs`, `DescribeResult.cs`, `QueryDescription.cs`, `DescribedParameter.cs`, `DescribeFailure.cs`, `DescribeStep.cs`, `DescribeStepName.cs`, `DescriberRegistry.cs`, `IDescribeExchange.cs`, `LiveExchange.cs`
- Modify: `src/SqlSource.Tool/ToolHost.cs`
- Create: `tests/SqlSource.Tool.Tests/FakeDescriber.cs`, `tests/SqlSource.Tool.Tests/RecordingExchange.cs`, `tests/SqlSource.Tool.Tests/DescriberTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Hosts.cs`, `tests/SqlSource.Tool.Tests/CliRun.cs`, `tests/SqlSource.Tool.Tests/CliTests.cs`, `tests/SqlSource.Tool.Tests/ToolHostTests.cs`

**Interfaces:**
- Produces, all `internal` in `SqlSource.Tool.Describing`:
  - `interface IQueryDescriber { SqlDialect Dialect { get; } Task<OpenResult> OpenAsync(OpenRequest request, CancellationToken cancellationToken); }`
  - `interface IDescribeSession : IAsyncDisposable { ServerInfo Server { get; } Task<DescribeResult> DescribeAsync(DescribeRequest request, CancellationToken cancellationToken); }`
  - `record OpenRequest(string Database, string Connection, IDescribeExchange Exchange)`, whose `ToString` leaves the connection out
  - `OpenResult.Opened(IDescribeSession)`, `OpenResult.Failed(DescribeFailure)`, with `Session` and `Failure`
  - `record ServerInfo(string Version, string? Driver, string? DriverVersion, EquatableArray<ServerSetting> Settings)` and `record ServerSetting(string Name, string Value)`
  - `record DescribeRequest(string Name, string Sql, EquatableArray<SqlQueryParameter> Parameters)`
  - `DescribeResult.Described(QueryDescription)`, `DescribeResult.Failed(DescribeFailure)`, with `Description` and `Failure`
  - `record QueryDescription(SidecarResultKind ResultKind, EquatableArray<DescribedParameter> Parameters, EquatableArray<SidecarColumn>? Columns, SidecarTable? MatchesTable, string? Plan, string? TableMatch)`
  - `record DescribedParameter(string Name, SidecarType? Type, string? TypeSource)`
  - `record DescribeFailure(DiagnosticDescriptor Descriptor, EquatableArray<string> Arguments, DescribeStep? Step, EquatableArray<string> ServerLines, string? Help)`
  - `enum DescribeStep` and `DescribeStepName.Of(DescribeStep) : string`
  - `class DescriberRegistry(IEnumerable<IQueryDescriber>)` with `Empty` and `Find(SqlDialect) : IQueryDescriber?`
  - `interface IDescribeExchange` with `AskAsync`, and `LiveExchange.Instance`
  - `ToolHost` gains two members at the end: `DescriberRegistry Describers` and `Func<string, IDescribeExchange> Exchange`
- Produces for the tests: `FakeDescriber`, `RecordingExchange`, `CliRun.Describers` and `CliRun.Exchange`

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tool.Tests/DescriberTests.cs`:

```csharp
using System;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing;
using Xunit;

namespace SqlSource.Tool.Tests;

// The seam a describer is written against: the registry, the exchange, and the describer of the tests itself.
public class DescriberTests
{
    [Fact]
    public void Find_DialectWithADescriber_GivesIt()
    {
        var postgres = new FakeDescriber();
        var mssql = new FakeDescriber(SqlDialect.SqlServer);

        var registry = new DescriberRegistry([postgres, mssql]);

        registry.Find(SqlDialect.PostgreSql).ShouldBeSameAs(postgres);
        registry.Find(SqlDialect.SqlServer).ShouldBeSameAs(mssql);
    }

    [Fact]
    public void Find_DialectWithoutADescriber_GivesNull() =>
        new DescriberRegistry([new FakeDescriber()]).Find(SqlDialect.SqlServer).ShouldBeNull();

    [Fact]
    public void Constructor_TwoDescribersOfOneDialect_Throws() =>
        Should.Throw<ArgumentException>(() => new DescriberRegistry([new FakeDescriber(), new FakeDescriber()]));

    // The released tool has no describer in this sub-phase: phase 3 registers the first.
    [Fact]
    public void Empty_EveryDescribableDialect_HasNoDescriber()
    {
        foreach (var dialect in SqlDescribableDialects.All)
        {
            DescriberRegistry.Empty.Find(dialect).ShouldBeNull();
        }
    }

    [Fact]
    public async Task AskAsync_LiveExchange_CallsLiveWithTheRequest()
    {
        var answer = await LiveExchange.Instance.AskAsync(
            "double",
            21,
            static (request, _) => Task.FromResult(request * 2),
            TestContext.Current.CancellationToken
        );

        answer.ShouldBe(42);
    }

    [Fact]
    public async Task OpenAsync_DescriberOfTheTests_MakesEveryCallThroughTheExchange()
    {
        var describer = new FakeDescriber();
        var exchange = new RecordingExchange();
        var token = TestContext.Current.CancellationToken;

        var opened = await describer.OpenAsync(new OpenRequest("billing", "Host=db", exchange), token);
        await using var session = opened.Session.ShouldNotBeNull();
        var described = await session.DescribeAsync(
            new DescribeRequest("GetUser", "SELECT 1;", EquatableArray<SqlQueryParameter>.Empty),
            token
        );

        exchange.Methods.ShouldBe(["open", "describe"]);
        described.Description.ShouldBe(FakeDescriber.NoRows);
        describer.Opened.ShouldHaveSingleItem().Database.ShouldBe("billing");
        describer.Described.ShouldHaveSingleItem().Name.ShouldBe("GetUser");
    }

    [Fact]
    public async Task DescribeAsync_QueryGivenAFailure_ReturnsItAndDoesNotThrow()
    {
        var describer = new FakeDescriber();
        describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser", "42P01: relation \"users\" does not exist");
        var token = TestContext.Current.CancellationToken;

        var opened = await describer.OpenAsync(new OpenRequest("billing", "Host=db", new RecordingExchange()), token);
        var described = await opened
            .Session.ShouldNotBeNull()
            .DescribeAsync(new DescribeRequest("GetUser", "SELECT 1;", EquatableArray<SqlQueryParameter>.Empty), token);

        described.Description.ShouldBeNull();
        described.Failure.ShouldNotBeNull().Step.ShouldBe(DescribeStep.DescribeColumns);
    }

    // A record prints its members, and one of these is a secret.
    [Fact]
    public void ToString_OpenRequest_LeavesTheConnectionOut()
    {
        var request = new OpenRequest("billing", "Host=db;Password=s3cret", LiveExchange.Instance);

        request.ToString().ShouldBe("OpenRequest { Database = billing }");
    }

    [Theory]
    [InlineData(DescribeStep.DescribeParameters, "describe parameters")]
    [InlineData(DescribeStep.CheckParameters, "check parameters")]
    [InlineData(DescribeStep.DescribeColumns, "describe columns")]
    [InlineData(DescribeStep.Catalog, "catalog")]
    [InlineData(DescribeStep.Explain, "explain")]
    [InlineData(DescribeStep.Walk, "walk")]
    [InlineData(DescribeStep.TableMatch, "table match")]
    [InlineData(DescribeStep.ResolveType, "resolve type")]
    [InlineData(DescribeStep.WriteSidecar, "write sidecar")]
    public void Of_Step_IsTheNameTheEpicGivesIt(DescribeStep step, string name) =>
        DescribeStepName.Of(step).ShouldBe(name);
}
```

In `tests/SqlSource.Tool.Tests/ToolHostTests.cs`, add `using SqlSource.Tool.Describing;` and, at the end of `Create_RealHost_IsTheConsoleTheCurrentDirectoryAndTheEnvironment`:

```csharp
        host.Describers.ShouldBeSameAs(DescriberRegistry.Empty);
        host.Exchange("billing").ShouldBeSameAs(LiveExchange.Instance);
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL to compile: `The type or namespace name 'Describing' does not exist in the namespace 'SqlSource.Tool'`.

- [ ] **Step 3: Write the records and the interfaces**

Each file below is in `src/SqlSource.Tool/Describing/` and in the namespace `SqlSource.Tool.Describing`.

`IQueryDescriber.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Parsing;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Asks the databases of one dialect to describe queries.  <see cref="DescriberRegistry" /> holds one for a dialect.
/// </summary>
/// <remarks>
/// <para>
/// A describer never throws for what a server or a user can cause: it returns a <see cref="DescribeFailure" />.  An
/// exception from one is a bug, and <c>SQLSRC200</c>.
/// </para>
/// <para>
/// Every call it makes to an engine goes through <see cref="OpenRequest.Exchange" />, so that a run can be recorded
/// and replayed.  It puts the connection's value, or a part of it, in nothing it returns.
/// </para>
/// </remarks>
internal interface IQueryDescriber
{
    /// <summary>The dialect whose databases it describes.</summary>
    SqlDialect Dialect { get; }

    /// <summary>Opens a session with one database.</summary>
    Task<OpenResult> OpenAsync(OpenRequest request, CancellationToken cancellationToken);
}
```

`IDescribeSession.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tool.Describing;

/// <summary>
/// An open session with one database.  It is used by one thread, one query at a time, and closed when the run is
/// done with the database.
/// </summary>
internal interface IDescribeSession : IAsyncDisposable
{
    /// <summary>What the session knows of its server.</summary>
    ServerInfo Server { get; }

    /// <summary>Describes one query.  The SQL is text: a describer that needs its lexemes lexes it itself.</summary>
    Task<DescribeResult> DescribeAsync(DescribeRequest request, CancellationToken cancellationToken);
}
```

`OpenRequest.cs`:

```csharp
namespace SqlSource.Tool.Describing;

/// <summary>
/// What a describer needs to open a session.  It is the one thing that holds a connection's value.
/// </summary>
/// <param name="Database">The logical name of the database, as the plan spells it.</param>
/// <param name="Connection">The connection's value.  A describer hands it to its driver and to nothing else.</param>
/// <param name="Exchange">What every call to the engine goes through.</param>
internal sealed record OpenRequest(string Database, string Connection, IDescribeExchange Exchange)
{
    /// <summary>
    /// The request without its connection: a record prints its members, and that one is a secret.
    /// </summary>
    public override string ToString() => $"OpenRequest {{ Database = {Database} }}";
}
```

`OpenResult.cs`:

```csharp
namespace SqlSource.Tool.Describing;

/// <summary>
/// A session, or why there is none.  Exactly one of the two is set.
/// </summary>
internal sealed record OpenResult
{
    private OpenResult(IDescribeSession? session, DescribeFailure? failure)
    {
        Session = session;
        Failure = failure;
    }

    public IDescribeSession? Session { get; }

    public DescribeFailure? Failure { get; }

    public static OpenResult Opened(IDescribeSession session) => new(session, null);

    public static OpenResult Failed(DescribeFailure failure) => new(null, failure);
}
```

`ServerInfo.cs`:

```csharp
namespace SqlSource.Tool.Describing;

/// <summary>
/// What a session knows of its server.
/// </summary>
/// <param name="Version">The server's version, as a sidecar records it.</param>
/// <param name="Driver">The name of the driver, or null.</param>
/// <param name="DriverVersion">The version of the driver, or null.</param>
/// <param name="Settings">The settings of the session that change a description.  Sub-phase 2.6 logs them.</param>
internal sealed record ServerInfo(
    string Version,
    string? Driver,
    string? DriverVersion,
    EquatableArray<ServerSetting> Settings
);
```

`ServerSetting.cs`:

```csharp
namespace SqlSource.Tool.Describing;

/// <summary>A setting of a session that changes a description, <c>search_path</c> for one.</summary>
internal sealed record ServerSetting(string Name, string Value);
```

`DescribeRequest.cs`:

```csharp
using SqlSource.Parsing;

namespace SqlSource.Tool.Describing;

/// <summary>
/// One query to describe.
/// </summary>
/// <param name="Name">The query's name.</param>
/// <param name="Sql">The sample SQL: the query with each token's default in the token's place.</param>
/// <param name="Parameters">The query's parameter list, each with the type and the nullability it declares.</param>
internal sealed record DescribeRequest(string Name, string Sql, EquatableArray<SqlQueryParameter> Parameters);
```

`DescribeResult.cs`:

```csharp
namespace SqlSource.Tool.Describing;

/// <summary>
/// A description, or why there is none.  Exactly one of the two is set.
/// </summary>
internal sealed record DescribeResult
{
    private DescribeResult(QueryDescription? description, DescribeFailure? failure)
    {
        Description = description;
        Failure = failure;
    }

    public QueryDescription? Description { get; }

    public DescribeFailure? Failure { get; }

    public static DescribeResult Described(QueryDescription description) => new(description, null);

    public static DescribeResult Failed(DescribeFailure failure) => new(null, failure);
}
```

`QueryDescription.cs`:

```csharp
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing;

/// <summary>
/// What a database said of one query.  It is engine-neutral but for its types, which are the sidecar's own records,
/// so that a describer's answer and an entry say a type one way.
/// </summary>
/// <param name="ResultKind">Whether the statement produces rows.</param>
/// <param name="Parameters">
/// The parameters the describer could type, in any order.  A parameter's ordinal and nullability are not the
/// describer's: the run takes them from the query's list.
/// </param>
/// <param name="Columns">The columns, in order.  Null when there are no rows.</param>
/// <param name="MatchesTable">The table whose column list the result is, or null.</param>
/// <param name="Plan">Whether the nullability plan walk ran.  Provenance.</param>
/// <param name="TableMatch">The first failing check of the table match.  Provenance.</param>
internal sealed record QueryDescription(
    SidecarResultKind ResultKind,
    EquatableArray<DescribedParameter> Parameters,
    EquatableArray<SidecarColumn>? Columns,
    SidecarTable? MatchesTable,
    string? Plan,
    string? TableMatch
);
```

`DescribedParameter.cs`:

```csharp
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing;

/// <summary>
/// A parameter as a describer typed it.
/// </summary>
/// <param name="Name">The bare name, in any case.</param>
/// <param name="Type">The engine's type, or null when the engine gave none.</param>
/// <param name="TypeSource">Where the type came from.  Provenance.</param>
internal sealed record DescribedParameter(string Name, SidecarType? Type, string? TypeSource);
```

`DescribeFailure.cs`:

```csharp
using Microsoft.CodeAnalysis;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Why a session could not be opened or a query described.  The run reports it at the query.
/// </summary>
/// <param name="Descriptor">What is wrong.</param>
/// <param name="Arguments">The text the descriptor's message quotes.</param>
/// <param name="Step">The step that failed, or null for a session that could not be opened.</param>
/// <param name="ServerLines">What the server said, verbatim, a line each.</param>
/// <param name="Help">How to mend it, or null.</param>
internal sealed record DescribeFailure(
    DiagnosticDescriptor Descriptor,
    EquatableArray<string> Arguments,
    DescribeStep? Step,
    EquatableArray<string> ServerLines,
    string? Help
);
```

`DescribeStep.cs`:

```csharp
namespace SqlSource.Tool.Describing;

/// <summary>
/// The steps of describing a query, which the <c>step:</c> line of an error names.  The epic outline fixes the list.
/// </summary>
internal enum DescribeStep
{
    DescribeParameters,
    CheckParameters,
    DescribeColumns,
    Catalog,
    Explain,
    Walk,
    TableMatch,
    ResolveType,
    WriteSidecar,
}
```

`DescribeStepName.cs`:

```csharp
using System;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The name of a step as an error prints it.
/// </summary>
internal static class DescribeStepName
{
    public static string Of(DescribeStep step) =>
        step switch
        {
            DescribeStep.DescribeParameters => "describe parameters",
            DescribeStep.CheckParameters => "check parameters",
            DescribeStep.DescribeColumns => "describe columns",
            DescribeStep.Catalog => "catalog",
            DescribeStep.Explain => "explain",
            DescribeStep.Walk => "walk",
            DescribeStep.TableMatch => "table match",
            DescribeStep.ResolveType => "resolve type",
            DescribeStep.WriteSidecar => "write sidecar",
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, null),
        };
}
```

`DescriberRegistry.cs`:

```csharp
using System;
using System.Collections.Generic;
using SqlSource.Parsing;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The describers of a run, one for a dialect.  The real host's has none until phase 3 registers PostgreSQL's.
/// </summary>
internal sealed class DescriberRegistry
{
    private readonly Dictionary<SqlDialect, IQueryDescriber> _describers = [];

    public DescriberRegistry(IEnumerable<IQueryDescriber> describers)
    {
        foreach (var describer in describers)
        {
            if (!_describers.TryAdd(describer.Dialect, describer))
            {
                throw new ArgumentException(
                    $"Two describers are given for the dialect {describer.Dialect}.",
                    nameof(describers)
                );
            }
        }
    }

    /// <summary>A registry with no describer.</summary>
    public static DescriberRegistry Empty { get; } = new([]);

    /// <summary>The describer of a dialect, or null when this version has none for it.</summary>
    public IQueryDescriber? Find(SqlDialect dialect) => _describers.GetValueOrDefault(dialect);
}
```

`IDescribeExchange.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tool.Describing;

/// <summary>
/// What every call a describer makes to an engine goes through, so that phase 3 can record a run and replay it.
/// </summary>
/// <remarks>
/// A request and an answer of a real describer are plain data that can be written as JSON, and a call that can fail
/// on the server returns the failure as its answer.
/// </remarks>
internal interface IDescribeExchange
{
    /// <summary>Asks the engine one question.</summary>
    /// <param name="method">The name of the question, which with the request keys a recorded answer.</param>
    /// <param name="request">What is asked.</param>
    /// <param name="live">Asks the engine itself.</param>
    /// <param name="cancellationToken">Ends the call.</param>
    Task<TAnswer> AskAsync<TRequest, TAnswer>(
        string method,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TAnswer>> live,
        CancellationToken cancellationToken
    );
}
```

`LiveExchange.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The exchange of a real run: it asks the engine and keeps nothing.
/// </summary>
internal sealed class LiveExchange : IDescribeExchange
{
    private LiveExchange() { }

    public static LiveExchange Instance { get; } = new();

    public Task<TAnswer> AskAsync<TRequest, TAnswer>(
        string method,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TAnswer>> live,
        CancellationToken cancellationToken
    ) => live(request, cancellationToken);
}
```

- [ ] **Step 4: Give the host its two members**

In `src/SqlSource.Tool/ToolHost.cs`, add `using SqlSource.Tool.Describing;`, add two `<param>` lines after the one of `ProcessorCount`, add the two parameters, and give them in `Create`:

```csharp
/// <param name="Describers">The describers of the run, by dialect.</param>
/// <param name="Exchange">Gives the exchange that the describer of a database, by its name, calls through.</param>
internal sealed record ToolHost(
    TextWriter Out,
    TextWriter Error,
    string WorkingDirectory,
    Func<string, string?> GetEnvironmentVariable,
    IProcessRunner Processes,
    string TempDirectory,
    int ProcessorCount,
    DescriberRegistry Describers,
    Func<string, IDescribeExchange> Exchange
)
{
    /// <summary>
    /// The host of a real run: the console, the current directory, the environment and the processes of the
    /// machine.  It has no describer yet: phase 3 registers the first.
    /// </summary>
    public static ToolHost Create() =>
        new(
            Console.Out,
            Console.Error,
            Directory.GetCurrentDirectory(),
            Environment.GetEnvironmentVariable,
            new ProcessRunner(),
            Path.GetTempPath(),
            Environment.ProcessorCount,
            DescriberRegistry.Empty,
            static _ => LiveExchange.Instance
        );
}
```

- [ ] **Step 5: Write the describer of the tests**

Create `tests/SqlSource.Tool.Tests/RecordingExchange.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Describing;

namespace SqlSource.Tool.Tests;

// An exchange that asks the engine, as the live one does, and keeps the name of each question.
internal sealed class RecordingExchange : IDescribeExchange
{
    public List<string> Methods { get; } = [];

    public Task<TAnswer> AskAsync<TRequest, TAnswer>(
        string method,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TAnswer>> live,
        CancellationToken cancellationToken
    )
    {
        Methods.Add(method);
        return live(request, cancellationToken);
    }
}
```

Create `tests/SqlSource.Tool.Tests/FakeDescriber.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;

namespace SqlSource.Tool.Tests;

// The describer of the tests.  It is given a description, a failure or an exception for a query's name, makes every
// call through the exchange as a real one must, and keeps what it was asked.  A query it was told nothing about is
// a statement without rows and without typed parameters.
internal sealed class FakeDescriber(SqlDialect dialect = SqlDialect.PostgreSql) : IQueryDescriber
{
    // No describer of this sub-phase has an error of a server, so the tests bring one.
    public static readonly DiagnosticDescriptor Rejected = new(
        id: "SQLSRC999",
        title: "Query was rejected",
        messageFormat: "The server rejected the query '{0}'",
        category: "SqlSource",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://example.test/sqlsrc999"
    );

    public static QueryDescription NoRows { get; } =
        new(SidecarResultKind.None, EquatableArray<DescribedParameter>.Empty, null, null, null, null);

    public SqlDialect Dialect => dialect;

    public ServerInfo Server { get; set; } = new("16.4", "FakeDriver", "1.2.3", EquatableArray<ServerSetting>.Empty);

    public Dictionary<string, QueryDescription> Descriptions { get; } = [with(StringComparer.Ordinal)];

    public Dictionary<string, DescribeFailure> Failures { get; } = [with(StringComparer.Ordinal)];

    public Dictionary<string, Exception> Exceptions { get; } = [with(StringComparer.Ordinal)];

    public DescribeFailure? OpenFailure { get; set; }

    public Exception? OpenException { get; set; }

    // Returned in place of a result, for a describer that returns nothing.
    public bool OpensNothing { get; set; }

    public Exception? CloseException { get; set; }

    // Runs before each query is described, for a test that cancels the run there.
    public Func<DescribeRequest, Task>? BeforeDescribe { get; set; }

    public List<OpenRequest> Opened { get; } = [];

    public List<DescribeRequest> Described { get; } = [];

    public int Closed { get; private set; }

    public static DescribeFailure Failure(string query, params string[] serverLines) =>
        new(
            Rejected,
            new EquatableArray<string>([query]),
            DescribeStep.DescribeColumns,
            new EquatableArray<string>([.. serverLines]),
            "check the SQL"
        );

    public Task<OpenResult> OpenAsync(OpenRequest request, CancellationToken cancellationToken) =>
        request.Exchange.AskAsync(
            "open",
            request.Database,
            (_, _) =>
            {
                Opened.Add(request);
                if (OpenException is { } exception)
                {
                    throw exception;
                }

                if (OpensNothing)
                {
                    return Task.FromResult<OpenResult>(null!);
                }

                return Task.FromResult(
                    OpenFailure is { } failure
                        ? OpenResult.Failed(failure)
                        : OpenResult.Opened(new Session(this, request.Exchange))
                );
            },
            cancellationToken
        );

    private sealed class Session(FakeDescriber owner, IDescribeExchange exchange) : IDescribeSession
    {
        public ServerInfo Server => owner.Server;

        public Task<DescribeResult> DescribeAsync(DescribeRequest request, CancellationToken cancellationToken) =>
            exchange.AskAsync(
                "describe",
                request,
                async (asked, token) =>
                {
                    owner.Described.Add(asked);
                    if (owner.BeforeDescribe is { } before)
                    {
                        await before(asked);
                        token.ThrowIfCancellationRequested();
                    }

                    if (owner.Exceptions.TryGetValue(asked.Name, out var exception))
                    {
                        throw exception;
                    }

                    return owner.Failures.TryGetValue(asked.Name, out var failure)
                        ? DescribeResult.Failed(failure)
                        : DescribeResult.Described(owner.Descriptions.GetValueOrDefault(asked.Name, NoRows));
                },
                cancellationToken
            );

        public ValueTask DisposeAsync()
        {
            owner.Closed++;
            return owner.CloseException is { } exception ? throw exception : ValueTask.CompletedTask;
        }
    }
}
```

- [ ] **Step 6: Give every host of the tests the two members**

In `tests/SqlSource.Tool.Tests/Hosts.cs`, add `using SqlSource.Tool.Describing;`, and end the argument list of both `new(...)` with:

```csharp
            processorCount,
            DescriberRegistry.Empty,
            static _ => LiveExchange.Instance
```

in `Create`, and in `Real`:

```csharp
            Environment.ProcessorCount,
            DescriberRegistry.Empty,
            static _ => LiveExchange.Instance
```

In `tests/SqlSource.Tool.Tests/CliTests.cs`, in `BuildCommands_EveryOption_TakesNoValueOrNeedsOne`, add `using SqlSource.Tool.Describing;` and end the `new ToolHost(...)` with:

```csharp
            ProcessorCount: 1,
            DescriberRegistry.Empty,
            static _ => LiveExchange.Instance
```

In `tests/SqlSource.Tool.Tests/CliRun.cs`, add `using SqlSource.Tool.Describing;`, add two members after `Processes`:

```csharp
    // The describers of the run.  None, as in the released tool, unless a test adds one.
    public List<IQueryDescriber> Describers { get; } = [];

    // Every database of the run calls through this one.
    public RecordingExchange Exchange { get; } = new();
```

and end the `new(...)` of `CreateHost` with:

```csharp
            ProcessorCount: 4,
            new DescriberRegistry(Describers),
            _ => Exchange
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.DescriberTests`
Expected: PASS, 17 tests.

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.

- [ ] **Step 8: Closing steps, and commit**

```bash
git add src/SqlSource.Tool tests/SqlSource.Tool.Tests
git commit -m "Give a describer its interfaces, a registry and an exchange

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The sample SQL, and an entry from a description

**Files:**
- Create: `src/SqlSource.Tool/Describing/SampleSql.cs`, `src/SqlSource.Tool/Describing/EntryBuilder.cs`
- Create: `tests/SqlSource.Tool.Tests/Plans.cs`, `tests/SqlSource.Tool.Tests/SampleSqlTests.cs`, `tests/SqlSource.Tool.Tests/EntryBuilderTests.cs`

**Interfaces:**
- Consumes: `PlannedQuery`, `SqlQuery.Segments`, `SqlQuery.Tokens`, `SqlQuery.Parameters`, `ServerInfo`, `QueryDescription`, `DescribedParameter`
- Produces: `SampleSql.Build(SqlQuery query) : string`
- Produces: `EntryBuilder.Build(PlannedQuery query, SqlDialect dialect, string database, ServerInfo server, QueryDescription description) : SidecarEntry`, which throws `InvalidOperationException` for a parameter the query's list lacks
- Produces for the tests: `Plans.Type`, `Plans.Postgres(TempFolder folder, string name = "App", string? directory = null) : TestProject`, `Plans.Of(params TestProject[] projects) : RunPlan`, `Plans.Of(RunFilters filters, params TestProject[] projects) : RunPlan`, `Plans.Query(TempFolder folder, string sql) : PlannedQuery`

- [ ] **Step 1: Write the helper that plans a project of a test**

Create `tests/SqlSource.Tool.Tests/Plans.cs`:

```csharp
using System.Linq;
using SqlSource.Tool.Planning;
using Xunit;

namespace SqlSource.Tool.Tests;

// The plan of a project that a test builds, for a test of what comes after the plan.
internal static class Plans
{
    // A type that claims every .sql file in the folder of its own source file.
    public const string Type = "[SqlSource.SqlSourceGenerate]\ninternal static partial class Queries;\n";

    // A project whose dialect is postgres, with that type at its root: a .sql file added at the root is claimed.
    public static TestProject Postgres(TempFolder folder, string name = "App", string? directory = null)
    {
        var project = new TestProject(folder, name, directory);
        project.Properties["SqlSourceDialect"] = "postgres";
        _ = project.AddSource("Queries.cs", Type);
        return project;
    }

    public static RunPlan Of(params TestProject[] projects) => Of(RunFilters.None, projects);

    // The plan, whatever is wrong with it: a test of a query with a problem wants the plan all the same.
    public static RunPlan Of(RunFilters filters, params TestProject[] projects) =>
        RunPlanner
            .Plan([.. projects.Select(project => project.Manifest())], filters, TestContext.Current.CancellationToken)
            .Plan;

    // The first query of a file that holds this SQL, in a postgres project of its own.
    public static PlannedQuery Query(TempFolder folder, string sql)
    {
        var project = Postgres(folder);
        _ = project.AddSql("Q.sql", sql);
        return Of(project).Files[0].Queries[0];
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/SqlSource.Tool.Tests/SampleSqlTests.cs`:

```csharp
using System;
using Shouldly;
using SqlSource.Tool.Describing;
using Xunit;

namespace SqlSource.Tool.Tests;

// The SQL a database is asked to describe: the query with each token's default in the token's place.
public sealed class SampleSqlTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private string Sample(string sql) => SampleSql.Build(Plans.Query(_folder, sql).Query);

    [Fact]
    public void Build_QueryWithoutTokens_IsItsSql() =>
        Sample("-- name: A\nSELECT 1 FROM t;\n").ShouldBe("SELECT 1 FROM t;");

    [Fact]
    public void Build_TokenWithADefault_HasTheDefaultInItsPlace() =>
        Sample("-- name: A\nSELECT 1 FROM t {{where:WHERE x = 1}};\n").ShouldBe("SELECT 1 FROM t WHERE x = 1;");

    [Fact]
    public void Build_TokenWithAnEmptyDefault_HasNothingInItsPlace() =>
        Sample("-- name: A\nSELECT 1 FROM t {{where:}};\n").ShouldBe("SELECT 1 FROM t ;");

    [Fact]
    public void Build_OneTokenTwice_HasItsDefaultInBothPlaces() =>
        Sample("-- name: A\nSELECT {{col:id}} FROM t ORDER BY {{col}};\n").ShouldBe("SELECT id FROM t ORDER BY id;");

    [Fact]
    public void Build_DefaultFromAMarker_HasTheDefaultInTheTokensPlace() =>
        Sample("-- name: A\n-- token: {{where:WHERE x = 1}}\nSELECT 1 FROM t {{where}};\n")
            .ShouldBe("SELECT 1 FROM t WHERE x = 1;");

    // The plan gives such a query a problem and the run never builds its sample.
    [Fact]
    public void Build_TokenWithoutADefault_Throws() =>
        Should
            .Throw<InvalidOperationException>(() => Sample("-- name: A\nSELECT 1 FROM t {{where}};\n"))
            .Message.ShouldBe("The token 'where' of the query 'A' has no default.");
}
```

Create `tests/SqlSource.Tool.Tests/EntryBuilderTests.cs`:

```csharp
using System;
using System.Linq;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Planning;
using Xunit;

namespace SqlSource.Tool.Tests;

// An entry of a sidecar, from the planned query, the session and the description.
public sealed class EntryBuilderTests : IDisposable
{
    private static readonly ServerInfo Server = new("16.4", "Npgsql", "8.0.0", EquatableArray<ServerSetting>.Empty);

    private static readonly PostgresType Integer = new(
        "integer",
        "base",
        "pg_catalog",
        "int4",
        null,
        null,
        null,
        null,
        null,
        null,
        null
    );

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private static QueryDescription With(params DescribedParameter[] parameters) =>
        FakeDescriber.NoRows with
        {
            Parameters = new EquatableArray<DescribedParameter>([.. parameters]),
        };

    private static SidecarEntry Build(PlannedQuery query, QueryDescription description) =>
        EntryBuilder.Build(query, SqlDialect.PostgreSql, "postgres", Server, description);

    [Fact]
    public void Build_Description_TakesEachMemberFromItsSource()
    {
        var query = Plans.Query(_folder, "-- name: GetUser\nSELECT id FROM users WHERE id = @id;\n");
        var column = new SidecarColumn(0, "id", Integer, false, "catalog", null, true, false);
        var description = new QueryDescription(
            SidecarResultKind.Rows,
            new EquatableArray<DescribedParameter>([new DescribedParameter("id", Integer, "inferred")]),
            new EquatableArray<SidecarColumn>([column]),
            new SidecarTable("public", "users"),
            "not-needed",
            "matched"
        );

        var entry = EntryBuilder.Build(query, SqlDialect.PostgreSql, "Billing", Server, description);

        entry.ShouldBe(
            new SidecarEntry(
                "GetUser",
                default,
                query.Hash.ShouldNotBeNull(),
                "postgres",
                "Billing",
                "16.4",
                SidecarResultKind.Rows,
                new SidecarTable("public", "users"),
                "not-needed",
                "matched",
                new EquatableArray<SidecarParameter>([new SidecarParameter("id", 0, Integer, null, "inferred")]),
                new EquatableArray<SidecarColumn>([column])
            )
        );
    }

    [Fact]
    public void Build_ParameterTheDescriptionLacks_HasNoTypeAndNoSource()
    {
        var query = Plans.Query(_folder, "-- name: A\nSELECT @a, @b;\n");

        var entry = Build(query, With(new DescribedParameter("b", Integer, "inferred")));

        entry
            .Parameters.ToArray()
            .ShouldBe([new SidecarParameter("a", 0, null, null, null), new SidecarParameter("b", 1, Integer, null, "inferred")]);
    }

    [Fact]
    public void Build_ParameterTheDescriptionGivesInAnotherCase_IsFoundAndKeepsTheNameOfTheList()
    {
        var query = Plans.Query(_folder, "-- name: A\nSELECT @userId;\n");

        var entry = Build(query, With(new DescribedParameter("USERID", Integer, "inferred")));

        entry.Parameters.ToArray().ShouldBe([new SidecarParameter("userId", 0, Integer, null, "inferred")]);
    }

    // A parameter that only a marker declares comes last in the query's list, and so in the entry.
    [Fact]
    public void Build_ParameterThatOnlyAMarkerDeclares_ComesAfterTheOnesOfTheSql()
    {
        var query = Plans.Query(_folder, "-- name: A\n-- param: @extra int\nSELECT @a;\n");

        var entry = Build(query, With(new DescribedParameter("extra", Integer, "declared")));

        entry
            .Parameters.ToArray()
            .ShouldBe([new SidecarParameter("a", 0, null, null, null), new SidecarParameter("extra", 1, Integer, null, "declared")]);
    }

    [Theory]
    [InlineData("-- param: @a null\n", true)]
    [InlineData("-- param: @a not null\n", false)]
    [InlineData("", null)]
    public void Build_Nullable_IsWhatTheMarkerSays(string marker, bool? nullable)
    {
        var query = Plans.Query(_folder, $"-- name: A\n{marker}SELECT @a;\n");

        Build(query, FakeDescriber.NoRows).Parameters.Single().Nullable.ShouldBe(nullable);
    }

    // A bug of the describer, and SQLSRC200.
    [Fact]
    public void Build_ParameterTheQueryDoesNotHave_Throws()
    {
        var query = Plans.Query(_folder, "-- name: A\nSELECT @a;\n");

        Should
            .Throw<InvalidOperationException>(() => Build(query, With(new DescribedParameter("b", Integer, null))))
            .Message.ShouldBe("The describer gave the parameter 'b', which the query 'A' does not have.");
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL to compile: `The name 'SampleSql' does not exist in the current context`.

- [ ] **Step 4: Write `SampleSql`**

Create `src/SqlSource.Tool/Describing/SampleSql.cs`:

```csharp
using System;
using System.Text;
using SqlSource.Generation;
using SqlSource.Parsing;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The SQL a database is asked to describe.
/// </summary>
internal static class SampleSql
{
    /// <summary>
    /// The query's SQL without comments, with each token's resolved default in the token's place: nothing, for an
    /// empty one.  This is the text the query's hash is made from, with the defaults written out.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A token has no default.  The plan gives such a query a problem, and the run does not describe it.
    /// </exception>
    public static string Build(SqlQuery query)
    {
        var sql = new StringBuilder();
        foreach (var segment in query.Segments)
        {
            _ = sql.Append(segment.Kind == SqlSegmentKind.Literal ? segment.Text : DefaultOf(query, segment.Text));
        }

        return sql.ToString();
    }

    // Names are compared as SqlQueryHash compares them.
    private static string DefaultOf(SqlQuery query, string name)
    {
        foreach (var token in query.Tokens)
        {
            if (token.Name == name && token.Default is { } text)
            {
                return text;
            }
        }

        throw new InvalidOperationException($"The token '{name}' of the query '{query.Name}' has no default.");
    }
}
```

- [ ] **Step 5: Write `EntryBuilder`**

Create `src/SqlSource.Tool/Describing/EntryBuilder.cs`:

```csharp
using System;
using System.Collections.Immutable;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Planning;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Makes the entry of a sidecar from what the plan, the session and the description each know of a query.
/// </summary>
internal static class EntryBuilder
{
    /// <summary>
    /// The entry.  Its parameters are the query's list in the list's order: the name and the nullability are the
    /// list's, the ordinal is the index, and the type with its source is what the description gives for that name,
    /// compared ignoring case, or null.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The description holds a parameter that the query's list does not: a bug of the describer.
    /// </exception>
    public static SidecarEntry Build(
        PlannedQuery query,
        SqlDialect dialect,
        string database,
        ServerInfo server,
        QueryDescription description
    )
    {
        var listed = query.Query.Parameters;
        foreach (var described in description.Parameters)
        {
            if (Find(listed, described.Name) < 0)
            {
                throw new InvalidOperationException(
                    $"The describer gave the parameter '{described.Name}', which the query '{query.Query.Name}' "
                        + "does not have."
                );
            }
        }

        var parameters = ImmutableArray.CreateBuilder<SidecarParameter>(listed.Count);
        for (var ordinal = 0; ordinal < listed.Count; ordinal++)
        {
            var parameter = listed[ordinal];
            var described = Find(description.Parameters, parameter.Name);
            parameters.Add(
                new SidecarParameter(
                    parameter.Name,
                    ordinal,
                    described?.Type,
                    parameter.Nullable,
                    described?.TypeSource
                )
            );
        }

        return new SidecarEntry(
            query.Query.Name,
            default,
            query.Hash ?? throw new InvalidOperationException($"The query '{query.Query.Name}' needs no entry."),
            SqlDialectName.Canonical(dialect),
            database,
            server.Version,
            description.ResultKind,
            description.MatchesTable,
            description.Plan,
            description.TableMatch,
            new EquatableArray<SidecarParameter>(parameters.MoveToImmutable()),
            description.Columns
        );
    }

    private static int Find(EquatableArray<SqlQueryParameter> listed, string name)
    {
        for (var index = 0; index < listed.Count; index++)
        {
            if (string.Equals(listed[index].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static DescribedParameter? Find(EquatableArray<DescribedParameter> described, string name)
    {
        foreach (var parameter in described)
        {
            if (string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return parameter;
            }
        }

        return null;
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.SampleSqlTests`
Expected: PASS, 6 tests.

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.EntryBuilderTests`
Expected: PASS, 8 tests.

If a `SampleSqlTests` case fails on white space alone, the parser's stripped segments are the truth: `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs` shows what a segment holds.  Change the expected text, not `SampleSql`.

- [ ] **Step 7: Closing steps, and commit**

```bash
git add src/SqlSource.Tool tests/SqlSource.Tool.Tests
git commit -m "Build the sample SQL of a query and the entry of its description

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Connections, with `SQLSRC213` to `SQLSRC215`

**Files:**
- Create in `src/SqlSource.Tool/Describing/`: `ConnectionArgument.cs`, `ConnectionVariables.cs`, `DatabaseConnection.cs`, `Connections.cs`
- Modify: `src/SqlSource.Tool/DescribeCommand.cs`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`
- Create: `tests/SqlSource.Tool.Tests/ConnectionTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/UsageCheckTests.cs`, `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`

**Interfaces:**
- Produces: `record ConnectionArgument(string Name, string Value)` with `static bool TryParse(string text, [NotNullWhen(true)] out ConnectionArgument? argument)`; its `ToString` leaves the value out
- Produces: `ConnectionVariables.Unnamed` (`"SQLSOURCE_CONNECTION"`) and `ConnectionVariables.For(string database) : string`
- Produces: `record DatabaseConnection(string Database, string Variable, string? Value, string? Source, string? SharesVariableWith)`; its `ToString` leaves the value out
- Produces: `Connections.Resolve(IReadOnlyList<string> selected, IReadOnlyList<ConnectionArgument> given, Func<string, string?> getEnvironmentVariable) : Connections`, with `For(string database) : DatabaseConnection`, `Databases : ImmutableArray<string>`, `UnusedNames : ImmutableArray<string>` and `UnnamedIsSetAndNotUsed : bool`
- Produces: `ToolDiagnostics.DatabaseHasNoConnection` (213), `ToolDiagnostics.DatabasesShareConnectionVariable` (214), `ToolDiagnostics.UnnamedConnectionNotUsed` (215).  Task 6 reports them.
- Produces: the option `--connection` of `describe`, checked by `DescribeCommand.CheckUsage`, and the constant `DescribeCommand.ConnectionOption`

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tool.Tests/ConnectionTests.cs`:

```csharp
using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

// Where the connection of a database comes from: the command line, its own variable, or the variable with no name.
public class ConnectionTests
{
    private const string Secret = "s3cret";

    private static ConnectionArgument Parse(string text)
    {
        ConnectionArgument.TryParse(text, out var argument).ShouldBeTrue();
        return argument.ShouldNotBeNull();
    }

    private static Connections Resolve(
        string[] selected,
        string[] given,
        params (string Name, string Value)[] environment
    )
    {
        var variables = environment.ToDictionary(variable => variable.Name, variable => variable.Value);
        return Connections.Resolve(selected, [.. given.Select(Parse)], name => variables.GetValueOrDefault(name));
    }

    private static Command ToolRoot() =>
        Cli.BuildCommands(Hosts.Create("/work", new FakeProcessRunner(), "/tmp"), new Reporter(TextWriter.Null));

    [Theory]
    [InlineData("billing", "SQLSOURCE_CONNECTION_BILLING")]
    [InlineData("billing-v2", "SQLSOURCE_CONNECTION_BILLING_V2")]
    [InlineData("app.main", "SQLSOURCE_CONNECTION_APP_MAIN")]
    [InlineData("a_b", "SQLSOURCE_CONNECTION_A_B")]
    [InlineData("café", "SQLSOURCE_CONNECTION_CAFÉ")]
    public void For_DatabaseName_IsUpperCaseWithAnythingElseAsAnUnderscore(string database, string variable) =>
        ConnectionVariables.For(database).ShouldBe(variable);

    [Fact]
    public void TryParse_ValueThatHoldsEqualsSigns_IsCutAtTheFirst() =>
        Parse("billing=Host=db;Port=5432").ShouldBe(new ConnectionArgument("billing", "Host=db;Port=5432"));

    [Fact]
    public void ToString_ConnectionArgument_LeavesTheValueOut() =>
        Parse("billing=" + Secret).ToString().ShouldNotContain(Secret);

    [Fact]
    public void For_ValueOnTheCommandLine_IsUsedAndSaysWhereItCameFrom() =>
        Resolve(["billing"], ["billing=A"])
            .For("billing")
            .ShouldBe(new DatabaseConnection("billing", "SQLSOURCE_CONNECTION_BILLING", "A", "--connection", null));

    [Fact]
    public void For_NameOnTheCommandLineInAnotherCase_IsThatDatabases() =>
        Resolve(["billing"], ["BILLING=A"]).For("Billing").Value.ShouldBe("A");

    [Fact]
    public void For_VariableOfTheName_IsUsedAndSaysWhereItCameFrom()
    {
        var connection = Resolve(["billing"], [], ("SQLSOURCE_CONNECTION_BILLING", "B")).For("billing");

        connection.Value.ShouldBe("B");
        connection.Source.ShouldBe("SQLSOURCE_CONNECTION_BILLING");
    }

    [Fact]
    public void For_VariableWithNoNameAndOneSelectedDatabase_IsUsed()
    {
        var connection = Resolve(["billing"], [], ("SQLSOURCE_CONNECTION", "C")).For("billing");

        connection.Value.ShouldBe("C");
        connection.Source.ShouldBe("SQLSOURCE_CONNECTION");
    }

    [Fact]
    public void For_EverySource_TheCommandLineWinsThenTheVariableOfTheName()
    {
        (string, string)[] both = [("SQLSOURCE_CONNECTION_BILLING", "B"), ("SQLSOURCE_CONNECTION", "C")];

        Resolve(["billing"], ["billing=A"], both).For("billing").Value.ShouldBe("A");
        Resolve(["billing"], [], both).For("billing").Value.ShouldBe("B");
    }

    [Fact]
    public void For_VariableThatIsEmpty_IsNotSet()
    {
        var connections = Resolve(["billing"], [], ("SQLSOURCE_CONNECTION_BILLING", ""), ("SQLSOURCE_CONNECTION", ""));

        connections.For("billing").Value.ShouldBeNull();
        connections.UnnamedIsSetAndNotUsed.ShouldBeFalse();
    }

    [Fact]
    public void For_VariableWithNoNameAndTwoSelectedDatabases_IsNotUsed()
    {
        var connections = Resolve(["billing", "reports"], ["billing=A"], ("SQLSOURCE_CONNECTION", "C"));

        connections.For("billing").Value.ShouldBe("A");
        connections.For("reports").Value.ShouldBeNull();
        connections.UnnamedIsSetAndNotUsed.ShouldBeTrue();
    }

    [Fact]
    public void UnusedNames_NameThatIsNoSelectedDatabase_IsNoErrorAndIsKept()
    {
        var connections = Resolve(["billing"], ["biling=A", "billing=B", "reports=C"]);

        connections.For("billing").Value.ShouldBe("B");
        connections.UnusedNames.ShouldBe(["biling", "reports"]);
        connections.Databases.ShouldBe(["billing"]);
    }

    [Fact]
    public void For_TwoNamesWithOneVariableBothFromTheEnvironment_NeitherHasAConnection()
    {
        var connections = Resolve(["app-v2", "app_v2"], [], ("SQLSOURCE_CONNECTION_APP_V2", "B"));

        connections
            .For("app-v2")
            .ShouldBe(new DatabaseConnection("app-v2", "SQLSOURCE_CONNECTION_APP_V2", null, null, "app_v2"));
        connections
            .For("app_v2")
            .ShouldBe(new DatabaseConnection("app_v2", "SQLSOURCE_CONNECTION_APP_V2", null, null, "app-v2"));
    }

    // The one on the command line too: the spec gives neither a connection.
    [Fact]
    public void For_TwoNamesWithOneVariableAndOneOnTheCommandLine_NeitherHasAConnection()
    {
        var connections = Resolve(["app-v2", "app_v2"], ["app-v2=A"], ("SQLSOURCE_CONNECTION_APP_V2", "B"));

        connections.For("app-v2").Value.ShouldBeNull();
        connections.For("app-v2").SharesVariableWith.ShouldBe("app_v2");
        connections.For("app_v2").SharesVariableWith.ShouldBe("app-v2");
    }

    [Fact]
    public void For_TwoNamesWithOneVariableBothOnTheCommandLine_EachHasItsOwn()
    {
        var connections = Resolve(["app-v2", "app_v2"], ["app-v2=A", "app_v2=B"]);

        connections.For("app-v2").Value.ShouldBe("A");
        connections.For("app_v2").Value.ShouldBe("B");
        connections.For("app_v2").SharesVariableWith.ShouldBeNull();
    }

    [Theory]
    [InlineData("describe", "--connection", "billing=Host=db")]
    [InlineData("describe", "--connection=billing=Host=db", "--connection:reports=Host=db:5432")]
    public void CheckUsage_ConnectionsWithANameAndAValue_GiveNoLine(params string[] args) =>
        DescribeCommand.CheckUsage(UsageCheck.Check(ToolRoot(), args)).ShouldBeEmpty();

    [Theory]
    [InlineData(3, "describe", "--connection", Secret)]
    [InlineData(3, "describe", "--connection", "=" + Secret)]
    [InlineData(3, "describe", "--connection", "not a name=" + Secret)]
    [InlineData(3, "describe", "--connection", "billing=")]
    [InlineData(2, "describe", "--connection=" + Secret)]
    [InlineData(2, "describe", "--connection:" + Secret)]
    public void CheckUsage_ConnectionThatIsNoNameAndValue_IsReportedByItsPositionAndNotItsText(
        int position,
        params string[] args
    ) =>
        DescribeCommand
            .CheckUsage(UsageCheck.Check(ToolRoot(), args))
            .ShouldBe([
                $"sqlsource: the value of option '--connection' at position {position} is not "
                    + "<name>=<connection string>",
            ]);

    [Fact]
    public void CheckUsage_SecondConnectionForOneNameIgnoringCase_IsReportedByItsPosition() =>
        DescribeCommand
            .CheckUsage(
                UsageCheck.Check(ToolRoot(), ["describe", "--connection", "billing=A", "--connection=BILLING=" + Secret])
            )
            .ShouldBe(["sqlsource: option '--connection' at position 4 names a database that an earlier one names"]);
}
```

In `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`, in `All_Messages_FormatWithTheirArguments`, the loop takes out the one descriptor whose first argument is not quoted:

```csharp
        foreach (
            var descriptor in ToolDiagnostics
                .All.Remove(ToolDiagnostics.UnexpectedFailure)
                .Remove(ToolDiagnostics.UnnamedConnectionNotUsed)
        )
```

and add three tests to the class:

```csharp
    [Fact]
    public void DatabaseHasNoConnection_Message_HoldsTheDatabase() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.DatabaseHasNoConnection.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "billing"
            )
            .ShouldBe("No connection is given for the database 'billing'");

    [Fact]
    public void DatabasesShareConnectionVariable_Message_HoldsBothDatabasesAndTheVariable() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.DatabasesShareConnectionVariable.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "app-v2",
                "app_v2",
                "SQLSOURCE_CONNECTION_APP_V2"
            )
            .ShouldBe("The databases 'app-v2' and 'app_v2' both read their connection from SQLSOURCE_CONNECTION_APP_V2");

    [Fact]
    public void UnnamedConnectionNotUsed_Message_HoldsTheCountAndTheNames() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.UnnamedConnectionNotUsed.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "2",
                "billing, reports"
            )
            .ShouldBe("SQLSOURCE_CONNECTION is for a run with one database, and this run has 2: billing, reports");
```

In `tests/SqlSource.Tool.Tests/UsageCheckTests.cs`, in `Check_EveryAcceptedCommandLineOfTheTool_IsReadTheSameBySystemCommandLine`, add five tokens to `vocabulary` after `"--database=",`:

```csharp
            "--connection",
            "--connection=N=V",
            "--connection:N=V",
            "--connection=",
            "N=V",
```

and compare the values of the new option too:

```csharp
            if (
                !usage.IsReadTheSameBy(parsed)
                || !HasTheSameValues(usage, parsed, "--project", "--database", "--connection")
            )
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL to compile: `The name 'ConnectionArgument' does not exist in the current context`.

- [ ] **Step 3: Add the three descriptors**

In `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, after `FileNotInRun`:

```csharp
    public static readonly DiagnosticDescriptor DatabaseHasNoConnection = new(
        id: "SQLSRC213",
        title: "Database has no connection",
        messageFormat: "No connection is given for the database '{0}'",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc213",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor DatabasesShareConnectionVariable = new(
        id: "SQLSRC214",
        title: "Two databases share a connection variable",
        messageFormat: "The databases '{0}' and '{1}' both read their connection from {2}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc214",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor UnnamedConnectionNotUsed = new(
        id: "SQLSRC215",
        title: "Connection names no database",
        messageFormat: "SQLSOURCE_CONNECTION is for a run with one database, and this run has {0}: {1}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc215",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

In `All`, add `DatabaseHasNoConnection, DatabasesShareConnectionVariable, UnnamedConnectionNotUsed,` after `FileNotInRun,`.

In `src/SqlSource/AnalyzerReleases.Unshipped.md`, after the row of `SQLSRC212`:

```
SQLSRC213 | SqlSource | Error | Database has no connection
SQLSRC214 | SqlSource | Error | Two databases share a connection variable
SQLSRC215 | SqlSource | Error | Connection names no database
```

- [ ] **Step 4: Add the three sections to `docs/diagnostics.md`**

In the table at the top, after the row of `SQLSRC212`:

```markdown
| [SQLSRC213](#sqlsrc213) | Database has no connection |
| [SQLSRC214](#sqlsrc214) | Two databases share a connection variable |
| [SQLSRC215](#sqlsrc215) | Connection names no database |
```

Between the sections `## SQLSRC212` and `## SQLSRC220`:

````markdown
## SQLSRC213

**Database has no connection**

A query that must be described belongs to a database that the run has no connection for.  A connection is given for the name of a database, and never in the project, since it holds credentials.  On the command line it is `--connection billing=<connection string>`.  In the environment it is the variable of the name: `SQLSOURCE_CONNECTION_`, then the name in upper case with every character that is not a letter or a digit written as `_`, so `billing-v2` reads `SQLSOURCE_CONNECTION_BILLING_V2`.  `SQLSOURCE_CONNECTION`, with no name, serves a run that has exactly one database.  The command line wins over the variable of the name, and that over `SQLSOURCE_CONNECTION`.  A variable that is empty is not set.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC213: No connection is given for the database 'postgres'
    help: pass --connection postgres=<connection string>, or set SQLSOURCE_CONNECTION_POSTGRES
```

Give the connection in one of the two ways the `help:` line names, or leave the database out of the run with `--database`.  When `--connection` was given for a name that no database of the run has, the `help:` line lists those names, so that a misspelt one is seen.  The error is reported once for a database, at its first query that must be described, and those queries count as failed.  A database whose queries all have a current entry needs no connection, and its summary line says `no connection`; under `--force` every query is described, so every database of the run needs one.

## SQLSRC214

**Two databases share a connection variable**

The variable of a database's name is the name in upper case with every character that is not a letter or a digit written as `_`.  Two databases of this run have names that give one variable, as `app-v2` and `app_v2` do, so the variable cannot say which of them it is for.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC214: The databases 'app-v2' and 'app_v2' both read their connection from SQLSOURCE_CONNECTION_APP_V2
```

Give both databases their connection on the command line, `--connection app-v2=<connection string> --connection app_v2=<connection string>`, or rename one of them.  A connection on the command line for one of the two is not enough: the variable would then be read for the other alone, and a reader of the command could not tell.  The error is reported once for each of the two databases that has a query to describe, at its first such query, and those queries count as failed.

## SQLSRC215

**Connection names no database**

`SQLSOURCE_CONNECTION` is set, and it is the connection of a run with exactly one database.  This run has more, and the database of this query has no connection of its own, so nothing says that the variable is for it.

```console
/work/App/Queries/Reports.sql(1,10): error SQLSRC215: SQLSOURCE_CONNECTION is for a run with one database, and this run has 2: postgres, reports
    help: set SQLSOURCE_CONNECTION_POSTGRES for 'postgres' and SQLSOURCE_CONNECTION_REPORTS for 'reports', or pass --connection <name>=<connection string> for each
```

Set the variable of each database, as the `help:` line names them, or give each with `--connection`, or restrict the run to one database with `--database`.  The error stands in the place of [SQLSRC213](#sqlsrc213) and is reported as that one is: once for a database, at its first query that must be described.
````

- [ ] **Step 5: Write the four types**

Create `src/SqlSource.Tool/Describing/ConnectionArgument.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using SqlSource.Settings;

namespace SqlSource.Tool.Describing;

/// <summary>
/// One value of <c>--connection</c>: the name of a database, <c>=</c>, and its connection.
/// </summary>
/// <param name="Name">The database.</param>
/// <param name="Value">The connection.  It may hold <c>=</c> itself, and is never printed.</param>
internal sealed record ConnectionArgument(string Name, string Value)
{
    /// <summary>
    /// Reads <c>name=value</c>.  The name is the text before the first <c>=</c> and must be a database name by the
    /// rule of the <c>-- database:</c> marker; the value is the rest and must not be empty.
    /// </summary>
    /// <remarks>
    /// The option always has a name: a connection string holds <c>=</c> itself and a shell removes quotes, so the
    /// tool could not tell a name from the start of a value by looking.
    /// </remarks>
    public static bool TryParse(string text, [NotNullWhen(true)] out ConnectionArgument? argument)
    {
        argument = null;
        var separator = text.IndexOf('=');
        if (
            separator <= 0
            || separator == text.Length - 1
            || !SettingValue.IsDatabaseName(text.AsSpan(0, separator))
        )
        {
            return false;
        }

        argument = new ConnectionArgument(text[..separator], text[(separator + 1)..]);
        return true;
    }

    /// <summary>The argument without its value, which is a secret.</summary>
    public override string ToString() => $"ConnectionArgument {{ Name = {Name} }}";
}
```

Create `src/SqlSource.Tool/Describing/ConnectionVariables.cs`:

```csharp
using System.Text;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The environment variables that hold a connection.
/// </summary>
internal static class ConnectionVariables
{
    /// <summary>The variable with no name, for a run that has exactly one selected database.</summary>
    public const string Unnamed = "SQLSOURCE_CONNECTION";

    /// <summary>
    /// The variable of a database: its name in upper case, in the invariant culture, with every character that is
    /// not a letter or a digit written as <c>_</c>.  Two names can give one variable, which is <c>SQLSRC214</c>.
    /// </summary>
    public static string For(string database)
    {
        var variable = new StringBuilder(Unnamed).Append('_');
        foreach (var character in database.ToUpperInvariant())
        {
            _ = variable.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return variable.ToString();
    }
}
```

Create `src/SqlSource.Tool/Describing/DatabaseConnection.cs`:

```csharp
namespace SqlSource.Tool.Describing;

/// <summary>
/// The connection of one selected database, or that it has none.
/// </summary>
/// <param name="Database">The database, as the plan spells it.</param>
/// <param name="Variable">The environment variable of its name.</param>
/// <param name="Value">The connection, or null.  It goes to the describer and to nothing else.</param>
/// <param name="Source">
/// Where the value came from, which is all the tool may say of it: <c>--connection</c>, or a variable's name.
/// </param>
/// <param name="SharesVariableWith">
/// Another selected database whose name gives the same variable, when either of the two has no value on the command
/// line (<c>SQLSRC214</c>).  Such a database has no connection.
/// </param>
internal sealed record DatabaseConnection(
    string Database,
    string Variable,
    string? Value,
    string? Source,
    string? SharesVariableWith
)
{
    /// <summary>The connection without its value, which is a secret.</summary>
    public override string ToString() =>
        $"DatabaseConnection {{ Database = {Database}, Source = {Source ?? "none"} }}";
}
```

Create `src/SqlSource.Tool/Describing/Connections.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The connections of a run's selected databases.  It reports nothing: a database with no connection is an error
/// only when a query of it must be described, which <see cref="DatabaseRuns" /> knows.
/// </summary>
internal sealed class Connections
{
    public const string CommandLineSource = "--connection";

    private readonly Dictionary<string, DatabaseConnection> _byDatabase;

    private Connections(
        Dictionary<string, DatabaseConnection> byDatabase,
        ImmutableArray<string> databases,
        ImmutableArray<string> unusedNames,
        bool unnamedIsSetAndNotUsed
    )
    {
        _byDatabase = byDatabase;
        Databases = databases;
        UnusedNames = unusedNames;
        UnnamedIsSetAndNotUsed = unnamedIsSetAndNotUsed;
    }

    /// <summary>The selected databases, in the plan's order.</summary>
    public ImmutableArray<string> Databases { get; }

    /// <summary>
    /// The names <c>--connection</c> gave that are no selected database, in the order given.  Not an error: a
    /// script may give every connection it has.  The help of <c>SQLSRC213</c> lists them.
    /// </summary>
    public ImmutableArray<string> UnusedNames { get; }

    /// <summary>
    /// Whether <c>SQLSOURCE_CONNECTION</c> is set and the run has more than one selected database, so that it is
    /// used for none (<c>SQLSRC215</c>).
    /// </summary>
    public bool UnnamedIsSetAndNotUsed { get; }

    /// <summary>The connection of a selected database, by its name in any case.</summary>
    public DatabaseConnection For(string database) => _byDatabase[database];

    /// <summary>
    /// Finds the connection of each selected database: the command line's value for its name, ignoring case; else
    /// the variable of its name; else <c>SQLSOURCE_CONNECTION</c>, when exactly one database is selected.  A
    /// variable that is empty is not set.
    /// </summary>
    /// <param name="selected">The selected databases, in the plan's order, each once ignoring case.</param>
    /// <param name="given">The values of <c>--connection</c>, each name once ignoring case.</param>
    /// <param name="getEnvironmentVariable">The host's.</param>
    public static Connections Resolve(
        IReadOnlyList<string> selected,
        IReadOnlyList<ConnectionArgument> given,
        Func<string, string?> getEnvironmentVariable
    )
    {
        var onCommandLine = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var argument in given)
        {
            onCommandLine[argument.Name] = argument.Value;
        }

        var unnamed = NullIfEmpty(getEnvironmentVariable(ConnectionVariables.Unnamed));
        var variables = selected.Select(ConnectionVariables.For).ToArray();
        var byDatabase = new Dictionary<string, DatabaseConnection>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < selected.Count; index++)
        {
            var name = selected[index];
            var variable = variables[index];
            if (SharerOf(index, selected, variables, onCommandLine) is { } other)
            {
                byDatabase[name] = new DatabaseConnection(name, variable, null, null, other);
            }
            else if (onCommandLine.TryGetValue(name, out var value))
            {
                byDatabase[name] = new DatabaseConnection(name, variable, value, CommandLineSource, null);
            }
            else if (NullIfEmpty(getEnvironmentVariable(variable)) is { } ofName)
            {
                byDatabase[name] = new DatabaseConnection(name, variable, ofName, variable, null);
            }
            else if (selected.Count == 1 && unnamed is not null)
            {
                byDatabase[name] = new DatabaseConnection(name, variable, unnamed, ConnectionVariables.Unnamed, null);
            }
            else
            {
                byDatabase[name] = new DatabaseConnection(name, variable, null, null, null);
            }
        }

        return new Connections(
            byDatabase,
            [.. selected],
            [.. given.Select(static argument => argument.Name).Where(name => !byDatabase.ContainsKey(name))],
            unnamed is not null && selected.Count > 1
        );
    }

    // The first other selected database with the same variable, when the variable would be read for either.
    private static string? SharerOf(
        int index,
        IReadOnlyList<string> selected,
        string[] variables,
        Dictionary<string, string> onCommandLine
    )
    {
        for (var other = 0; other < selected.Count; other++)
        {
            if (
                other != index
                && string.Equals(variables[other], variables[index], StringComparison.Ordinal)
                && !(onCommandLine.ContainsKey(selected[index]) && onCommandLine.ContainsKey(selected[other]))
            )
            {
                return selected[other];
            }
        }

        return null;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
```

- [ ] **Step 6: Give `describe` the option and its rule**

In `src/SqlSource.Tool/DescribeCommand.cs`, add `using System;` and `using SqlSource.Tool.Describing;`, and the constant beside the others:

```csharp
    internal const string ConnectionOption = "--connection";
```

In `Create`, after the `database` option:

```csharp
        // Always a name, "=" and a value.  CheckUsage holds each to that, and a message never repeats one.
        var connection = new Option<string[]>(ConnectionOption)
        {
            Description =
                "The connection string of a database, as <name>=<connection string>.  May be given several times.",
            HelpName = "name=value",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = false,
        };
```

and add `connection,` to the command's initializer after `database,`.

In `CheckUsage`, add to the `<remarks>`: `A value of <c>--connection</c> is a name, <c>=</c> and a value, and a name is given once.`  Declare the set before the loop and add a branch at its end:

```csharp
        var connections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
```

```csharp
            else if (value.Option.Name == ConnectionOption)
            {
                if (!ConnectionArgument.TryParse(value.Text, out var connection))
                {
                    messages.Add(
                        $"sqlsource: the value of option '{ConnectionOption}' at position {value.Position} is not "
                            + "<name>=<connection string>"
                    );
                }
                else if (!connections.Add(connection.Name))
                {
                    messages.Add(
                        $"sqlsource: option '{ConnectionOption}' at position {value.Position} names a database that "
                            + "an earlier one names"
                    );
                }
            }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.ConnectionTests`
Expected: PASS, 27 tests.

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.  `Check_EveryAcceptedCommandLineOfTheTool_IsReadTheSameBySystemCommandLine` holds `UsageCheck` and System.CommandLine together over the new tokens.  If it names a command line, the two read `--connection` differently: mend `UsageCheck`, by reading as System.CommandLine does or by rejecting more, as the tool's `AGENTS.md` says.

- [ ] **Step 8: Closing steps, and commit**

```bash
git add src tests docs/diagnostics.md
git commit -m "Find the connection of each database

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: What is there, and what each query needs, with `SQLSRC221`

**Files:**
- Create in `src/SqlSource.Tool/Describing/`: `SidecarOnDisk.cs`, `QueryState.cs`, `QueryWork.cs`, `FileWork.cs`, `RunDecisions.cs`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`
- Create: `tests/SqlSource.Tool.Tests/TestSidecar.cs`, `tests/SqlSource.Tool.Tests/RunDecisionsTests.cs`
- Modify: `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`

**Interfaces:**
- Consumes: `RunPlan`, `PlannedFile`, `PlannedQuery`, `Sidecar.IsWrittenBy`, `SidecarEntry.IsCurrentFor`, `SidecarReader.Read`, `SidecarFormat.PathFor`, `PackageVersion.Prefix`
- Produces: `record SidecarOnDisk(string Path, string? Text, Sidecar? Usable, int? NewerFormat)` with `static SidecarOnDisk Read(string sqlPath)`
- Produces: `enum QueryState { NotNeeded, Failed, ToDescribe, Described, Skipped, LeftOut }`
- Produces: `class QueryWork(PlannedQuery planned, QueryState state, SidecarEntry? entry = null)` with `Planned`, and settable `State` and `Entry`
- Produces: `record FileWork(PlannedFile File, SidecarOnDisk? OnDisk, ImmutableArray<QueryWork> Queries)`
- Produces: `RunDecisions.Decide(RunPlan plan, bool force, ICollection<ToolDiagnostic> errors) : ImmutableArray<FileWork>` and `RunDecisions.SelectedDatabases(RunPlan plan) : ImmutableArray<PlannedDatabase>`
- Produces: `ToolDiagnostics.SidecarOfNewerTool` (221)
- Produces for the tests: `TestSidecar.EntryFor(PlannedQuery query) : SidecarEntry`, `TestSidecar.Write(PlannedFile file, IEnumerable<SidecarEntry> entries, string? toolVersion = null, int formatVersion = SidecarFormat.Version) : string`, `TestSidecar.WriteCurrent(PlannedFile file) : string`, `TestSidecar.Read(string sqlPath) : Sidecar`

- [ ] **Step 1: Write the helper that writes a sidecar of a test**

Create `tests/SqlSource.Tool.Tests/TestSidecar.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Planning;

namespace SqlSource.Tool.Tests;

// A sidecar that a test puts beside a .sql file, or reads from there.
internal static class TestSidecar
{
    // An entry that is current for the query: its hash and its database, and a statement without rows.
    public static SidecarEntry EntryFor(PlannedQuery query) =>
        new(
            query.Query.Name,
            default,
            query.Hash.ShouldNotBeNull(),
            "postgres",
            query.Database,
            "16.4",
            SidecarResultKind.None,
            null,
            null,
            null,
            EquatableArray<SidecarParameter>.Empty,
            null
        );

    // Writes a sidecar beside the file and gives its path.  The versions are the tool's unless given.
    public static string Write(
        PlannedFile file,
        IEnumerable<SidecarEntry> entries,
        string? toolVersion = null,
        int formatVersion = SidecarFormat.Version
    )
    {
        var path = SidecarFormat.PathFor(file.Path);
        var sidecar = new Sidecar(
            formatVersion,
            toolVersion ?? PackageVersion.Prefix,
            new EquatableArray<SidecarEntry>([.. entries])
        );
        File.WriteAllText(path, SidecarWriter.Write(sidecar));
        return path;
    }

    // A sidecar with a current entry for every query of the file that needs one.
    public static string WriteCurrent(PlannedFile file) =>
        Write(file, file.Queries.Where(query => query.NeedsEntry).Select(EntryFor));

    public static Sidecar Read(string sqlPath) =>
        SidecarReader.Read(File.ReadAllText(SidecarFormat.PathFor(sqlPath))).Sidecar.ShouldNotBeNull();
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/SqlSource.Tool.Tests/RunDecisionsTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

// Steps 1 and 2 of the run: the sidecars that are there, and what each query needs of the run.
public sealed class RunDecisionsTests : IDisposable
{
    private const string Users =
        "-- name: GetUser\nSELECT 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n\n"
        + "-- name: Plain\n-- output: sql\nSELECT 3;\n";

    private static readonly RunFilters OnlyBilling = new([], ["billing"]);

    private readonly TempFolder _folder = new();
    private readonly TestProject _project;
    private readonly List<ToolDiagnostic> _errors = [];

    public RunDecisionsTests()
    {
        _project = Plans.Postgres(_folder);
        _ = _project.AddSql("Users.sql", Users);
    }

    public void Dispose() => _folder.Dispose();

    // The states of the file's queries, in the file's order.
    private QueryState[] States(RunPlan plan, bool force = false) =>
        [.. RunDecisions.Decide(plan, force, _errors)[0].Queries.Select(query => query.State)];

    [Fact]
    public void Decide_NoSidecar_DescribesWhatNeedsAnEntry()
    {
        States(Plans.Of(_project)).ShouldBe([QueryState.ToDescribe, QueryState.ToDescribe, QueryState.NotNeeded]);

        _errors.ShouldBeEmpty();
    }

    [Fact]
    public void Decide_CurrentEntries_AreSkippedAndKept()
    {
        var plan = Plans.Of(_project);
        _ = TestSidecar.WriteCurrent(plan.Files[0]);

        var file = RunDecisions.Decide(plan, force: false, _errors)[0];

        file.Queries.Select(query => query.State)
            .ShouldBe([QueryState.Skipped, QueryState.Skipped, QueryState.NotNeeded]);
        file.Queries[0].Entry.ShouldNotBeNull().Name.ShouldBe("GetUser");
        file.Queries[1].Entry.ShouldNotBeNull().Database.ShouldBe("billing");
        file.OnDisk.ShouldNotBeNull().Usable.ShouldNotBeNull();
    }

    [Fact]
    public void Decide_CurrentEntriesUnderForce_AreDescribedAgain()
    {
        var plan = Plans.Of(_project);
        _ = TestSidecar.WriteCurrent(plan.Files[0]);

        States(plan, force: true).ShouldBe([QueryState.ToDescribe, QueryState.ToDescribe, QueryState.NotNeeded]);
    }

    [Fact]
    public void Decide_EntryWithAnotherHash_IsDescribed()
    {
        var plan = Plans.Of(_project);
        var file = plan.Files[0];
        _ = TestSidecar.Write(
            file,
            [TestSidecar.EntryFor(file.Queries[0]) with { Hash = "0000" }, TestSidecar.EntryFor(file.Queries[1])]
        );

        States(plan).ShouldBe([QueryState.ToDescribe, QueryState.Skipped, QueryState.NotNeeded]);
    }

    [Fact]
    public void Decide_EntryOfAnotherDatabase_IsDescribed()
    {
        var plan = Plans.Of(_project);
        var file = plan.Files[0];
        _ = TestSidecar.Write(
            file,
            [TestSidecar.EntryFor(file.Queries[0]) with { Database = "elsewhere" }, TestSidecar.EntryFor(file.Queries[1])]
        );

        States(plan).ShouldBe([QueryState.ToDescribe, QueryState.Skipped, QueryState.NotNeeded]);
    }

    // The name of a database is compared ignoring case, as its connection variable is.
    [Fact]
    public void Decide_EntryWhoseDatabaseDiffersInCase_IsCurrent()
    {
        var plan = Plans.Of(_project);
        var file = plan.Files[0];
        _ = TestSidecar.Write(
            file,
            [TestSidecar.EntryFor(file.Queries[0]), TestSidecar.EntryFor(file.Queries[1]) with { Database = "BILLING" }]
        );

        States(plan).ShouldBe([QueryState.Skipped, QueryState.Skipped, QueryState.NotNeeded]);
    }

    [Fact]
    public void Decide_SidecarOfAnotherToolVersion_IsAbsent()
    {
        var plan = Plans.Of(_project);
        var file = plan.Files[0];
        _ = TestSidecar.Write(file, file.Queries.Take(2).Select(TestSidecar.EntryFor), toolVersion: "0.0.1");

        States(plan).ShouldBe([QueryState.ToDescribe, QueryState.ToDescribe, QueryState.NotNeeded]);
        _errors.ShouldBeEmpty();
    }

    [Fact]
    public void Decide_SidecarOfALowerFormatVersion_IsAbsent()
    {
        var plan = Plans.Of(_project);
        var file = plan.Files[0];
        _ = TestSidecar.Write(file, file.Queries.Take(2).Select(TestSidecar.EntryFor), formatVersion: 0);

        States(plan).ShouldBe([QueryState.ToDescribe, QueryState.ToDescribe, QueryState.NotNeeded]);
        _errors.ShouldBeEmpty();
    }

    // The usual cause is a merge conflict, and "describe" is the fix.
    [Fact]
    public void Decide_SidecarThatCannotBeParsed_IsAbsent()
    {
        var plan = Plans.Of(_project);
        File.WriteAllText(SidecarFormat.PathFor(plan.Files[0].Path), "<<<<<<< HEAD\n{");

        var file = RunDecisions.Decide(plan, force: false, _errors)[0];

        file.Queries.Select(query => query.State)
            .ShouldBe([QueryState.ToDescribe, QueryState.ToDescribe, QueryState.NotNeeded]);
        file.OnDisk.ShouldNotBeNull().Text.ShouldBe("<<<<<<< HEAD\n{");
        _errors.ShouldBeEmpty();
    }

    // Review focus 1.
    [Fact]
    public void Decide_SidecarPathThatIsADirectory_IsAbsentAndNothingThrows()
    {
        var plan = Plans.Of(_project);
        _ = Directory.CreateDirectory(SidecarFormat.PathFor(plan.Files[0].Path));

        var file = RunDecisions.Decide(plan, force: false, _errors)[0];

        file.Queries[0].State.ShouldBe(QueryState.ToDescribe);
        file.OnDisk.ShouldNotBeNull().Text.ShouldBeNull();
        _errors.ShouldBeEmpty();
    }

    [Fact]
    public void Decide_SidecarOfAHigherFormatVersion_IsSqlsrc221AndItsSelectedQueriesFail()
    {
        var plan = Plans.Of(_project);
        var file = plan.Files[0];
        var path = TestSidecar.Write(file, file.Queries.Take(2).Select(TestSidecar.EntryFor), formatVersion: 2);

        States(plan, force: true).ShouldBe([QueryState.Failed, QueryState.Failed, QueryState.NotNeeded]);

        _errors
            .ShouldHaveSingleItem()
            .ShouldBe(
                ToolDiagnostic
                    .ForFile(ToolDiagnostics.SidecarOfNewerTool, path, path, "2", "1")
                    .WithLines(new ContinuationLine("help", "update the SqlSource.Tool package"))
            );
    }

    [Fact]
    public void Decide_QueryWithAProblemOfThePlan_FailsEvenUnderForce()
    {
        _ = _project.AddSql("Tokens.sql", "-- name: Find\nSELECT 1 {{where}};\n");
        var plan = Plans.Of(_project);

        var tokens = RunDecisions.Decide(plan, force: true, _errors).Single(file => file.File.Path.EndsWith("Tokens.sql"));

        tokens.Queries.ShouldHaveSingleItem().State.ShouldBe(QueryState.Failed);
    }

    // The plan reported SQLSRC209 for the file.  Its queries fail, and its sidecar is not read.
    [Fact]
    public void Decide_FileThatIsNotReady_FailsItsQueriesAndReadsNoSidecar()
    {
        _project.Properties["SqlSourceDialect"] = "ansi";
        var plan = Plans.Of(_project);
        plan.Files[0].State.ShouldBe(PlannedFileState.NotDescribable);
        _ = TestSidecar.Write(plan.Files[0], [], formatVersion: 2);

        var file = RunDecisions.Decide(plan, force: false, _errors)[0];

        file.Queries.Select(query => query.State)
            .ShouldBe([QueryState.Failed, QueryState.Failed, QueryState.NotNeeded]);
        file.OnDisk.ShouldBeNull();
        _errors.ShouldBeEmpty();
    }

    [Fact]
    public void Decide_QueryThatIsNotSelected_IsKeptWhenCurrentAndLeftOutOtherwise()
    {
        var all = Plans.Of(_project);
        _ = TestSidecar.Write(all.Files[0], [TestSidecar.EntryFor(all.Files[0].Queries[0])]);

        States(Plans.Of(OnlyBilling, _project))
            .ShouldBe([QueryState.Skipped, QueryState.ToDescribe, QueryState.NotNeeded]);

        File.Delete(SidecarFormat.PathFor(all.Files[0].Path));
        States(Plans.Of(OnlyBilling, _project))
            .ShouldBe([QueryState.LeftOut, QueryState.ToDescribe, QueryState.NotNeeded]);
    }

    // "--force" describes what is selected, and nothing else.
    [Fact]
    public void Decide_QueryThatIsNotSelectedUnderForce_IsStillKept()
    {
        var all = Plans.Of(_project);
        _ = TestSidecar.WriteCurrent(all.Files[0]);

        States(Plans.Of(OnlyBilling, _project), force: true)
            .ShouldBe([QueryState.Skipped, QueryState.ToDescribe, QueryState.NotNeeded]);
    }

    // A filter narrows what a run reads as well as what it changes.
    [Fact]
    public void Decide_FileWithNoSelectedQuery_IsNotRead()
    {
        _ = _project.AddSql("Other.sql", "-- name: Other\nSELECT 4;\n");
        var all = Plans.Of(_project);
        var other = all.Files.Single(file => file.Path.EndsWith("Other.sql"));
        _ = TestSidecar.Write(other, [], formatVersion: 2);

        var file = RunDecisions
            .Decide(Plans.Of(OnlyBilling, _project), force: false, _errors)
            .Single(work => work.File.Path == other.Path);

        file.OnDisk.ShouldBeNull();
        file.Queries.ShouldHaveSingleItem().State.ShouldBe(QueryState.LeftOut);
        _errors.ShouldBeEmpty();
    }

    [Fact]
    public void SelectedDatabases_Filter_GivesTheDatabasesOfSelectedQueriesInThePlansOrder()
    {
        RunDecisions.SelectedDatabases(Plans.Of(_project)).Select(database => database.Name)
            .ShouldBe(["postgres", "billing"]);
        RunDecisions.SelectedDatabases(Plans.Of(OnlyBilling, _project)).Select(database => database.Name)
            .ShouldBe(["billing"]);
    }
}
```

In `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`, add:

```csharp
    [Fact]
    public void SidecarOfNewerTool_Message_HoldsTheSidecarAndBothVersions() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.SidecarOfNewerTool.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "/work/Users.sql.json",
                "2",
                "1"
            )
            .ShouldBe("'/work/Users.sql.json' has format 2, and this tool writes format 1");
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL to compile: `The name 'RunDecisions' does not exist in the current context`.

- [ ] **Step 4: Add the descriptor, its release row and its section**

In `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, after `ProjectNotRestored`:

```csharp
    public static readonly DiagnosticDescriptor SidecarOfNewerTool = new(
        id: "SQLSRC221",
        title: "Sidecar was written by a newer tool",
        messageFormat: "'{0}' has format {1}, and this tool writes format {2}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc221",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

In `All`, add `SidecarOfNewerTool,` after `ProjectNotRestored,`.

In `src/SqlSource/AnalyzerReleases.Unshipped.md`, after the row of `SQLSRC220`:

```
SQLSRC221 | SqlSource | Error | Sidecar was written by a newer tool
```

In `docs/diagnostics.md`, in the table after the row of `SQLSRC220`:

```markdown
| [SQLSRC221](#sqlsrc221) | Sidecar was written by a newer tool |
```

and between the sections `## SQLSRC220` and `## SQLSRC222`:

````markdown
## SQLSRC221

**Sidecar was written by a newer tool**

A sidecar, the `.sql.json` file beside a `.sql` file, says which version of the format it has.  This one has a higher version than this tool writes, so a newer `sqlsource` wrote it, and this one must not write it back down: the generator of that newer version would no longer read it.

```console
/work/App/Queries/Users.sql.json : error SQLSRC221: '/work/App/Queries/Users.sql.json' has format 2, and this tool writes format 1
    help: update the SqlSource.Tool package
```

Update the tool, with `dotnet tool update SqlSource.Tool`, to the version of the SqlSource package the project uses.  The file is left as it is, and every query of its `.sql` file that the run was asked to describe counts as failed.  A sidecar of a lower format version, of another version of the tool, or one that cannot be read, a merge conflict for one, is no error: the tool describes its queries and writes it again.
````

- [ ] **Step 5: Write the five types**

Create `src/SqlSource.Tool/Describing/SidecarOnDisk.cs`:

```csharp
using System;
using System.IO;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The sidecar beside a <c>.sql</c> file as a run found it.
/// </summary>
/// <param name="Path">The full path of the sidecar.</param>
/// <param name="Text">The text of the file, or null when there is none that can be opened.</param>
/// <param name="Usable">
/// The sidecar, when it was read and this version of the tool wrote it in this version of the format.  Only such a
/// one has entries a run keeps: an entry from an older tool never stands beside a new one.
/// </param>
/// <param name="NewerFormat">The format version of the file, when it is higher than the tool's.</param>
internal sealed record SidecarOnDisk(string Path, string? Text, Sidecar? Usable, int? NewerFormat)
{
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
            return new SidecarOnDisk(path, null, null, null);
        }

        var read = SidecarReader.Read(text);
        if (read.Sidecar is { } sidecar)
        {
            return new SidecarOnDisk(path, text, sidecar.IsWrittenBy(PackageVersion.Prefix) ? sidecar : null, null);
        }

        return new SidecarOnDisk(
            path,
            text,
            null,
            read.FormatVersion is { } version && version > SidecarFormat.Version ? version : null
        );
    }
}
```

Create `src/SqlSource.Tool/Describing/QueryState.cs`:

```csharp
namespace SqlSource.Tool.Describing;

/// <summary>
/// What a run does, or did, with one query.
/// </summary>
internal enum QueryState
{
    /// <summary>The query needs no entry: its output is <c>sql</c>.</summary>
    NotNeeded,

    /// <summary>
    /// It cannot be described, or could not: a problem of the plan, a file that is not ready, a sidecar of a newer
    /// tool, no describer, no connection, or a failure of the describer.
    /// </summary>
    Failed,

    /// <summary>The run will describe it.  No query is left in this state when the databases are done.</summary>
    ToDescribe,

    /// <summary>The run described it, and <see cref="QueryWork.Entry" /> is the new entry.</summary>
    Described,

    /// <summary>Its entry is current, and <see cref="QueryWork.Entry" /> is that entry, which is kept.</summary>
    Skipped,

    /// <summary>It is not selected and has no current entry, so its file cannot be written.</summary>
    LeftOut,
}
```

Create `src/SqlSource.Tool/Describing/QueryWork.cs`:

```csharp
using SqlSource.Snapshot;
using SqlSource.Tool.Planning;

namespace SqlSource.Tool.Describing;

/// <summary>
/// One query of a run, and what the run has done with it so far.  <see cref="RunDecisions" /> makes it and
/// <see cref="DatabaseRuns" /> moves it from <see cref="QueryState.ToDescribe" /> to its end.
/// </summary>
internal sealed class QueryWork(PlannedQuery planned, QueryState state, SidecarEntry? entry = null)
{
    public PlannedQuery Planned { get; } = planned;

    public QueryState State { get; set; } = state;

    /// <summary>The entry the file's target holds for the query: the kept one, or the new one.</summary>
    public SidecarEntry? Entry { get; set; } = entry;
}
```

Create `src/SqlSource.Tool/Describing/FileWork.cs`:

```csharp
using System.Collections.Immutable;
using SqlSource.Tool.Planning;

namespace SqlSource.Tool.Describing;

/// <summary>
/// One claimed file of a run.
/// </summary>
/// <param name="File">The file of the plan.</param>
/// <param name="OnDisk">
/// The sidecar beside it.  Null when the run did not read it: the file is not ready, or no query of it that needs
/// an entry is selected.
/// </param>
/// <param name="Queries">Every query of the file, in the file's order.</param>
internal sealed record FileWork(PlannedFile File, SidecarOnDisk? OnDisk, ImmutableArray<QueryWork> Queries);
```

Create `src/SqlSource.Tool/Describing/RunDecisions.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Snapshot;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Steps 1 and 2 of a run: reads the sidecars that are there, and says of each query what the run does with it.
/// </summary>
internal static class RunDecisions
{
    /// <summary>
    /// Decides for every query of the plan.  A sidecar is read only for a file that is ready and has a selected
    /// query that needs an entry: a filter narrows what a run reads as well as what it changes.
    /// </summary>
    /// <param name="plan">The plan of the run.</param>
    /// <param name="force">Whether a selected query is described though its entry is current.</param>
    /// <param name="errors">Gains <c>SQLSRC221</c> for each sidecar of a newer tool.</param>
    public static ImmutableArray<FileWork> Decide(RunPlan plan, bool force, ICollection<ToolDiagnostic> errors)
    {
        var files = ImmutableArray.CreateBuilder<FileWork>(plan.Files.Count);
        foreach (var file in plan.Files)
        {
            var ready = file.State == PlannedFileState.Ready;
            var onDisk =
                ready && file.Queries.Any(static query => query.NeedsEntry && query.IsSelected)
                    ? SidecarOnDisk.Read(file.Path)
                    : null;
            if (onDisk?.NewerFormat is { } newer)
            {
                errors.Add(
                    ToolDiagnostic
                        .ForFile(
                            ToolDiagnostics.SidecarOfNewerTool,
                            onDisk.Path,
                            onDisk.Path,
                            newer.ToString(CultureInfo.InvariantCulture),
                            SidecarFormat.Version.ToString(CultureInfo.InvariantCulture)
                        )
                        .WithLines(new ContinuationLine("help", "update the SqlSource.Tool package"))
                );
            }

            files.Add(new FileWork(file, onDisk, [.. file.Queries.Select(query => Decide(query, ready, onDisk, force))]));
        }

        return files.MoveToImmutable();
    }

    /// <summary>
    /// The selected databases: those of the plan that have a selected query that needs an entry, in the plan's
    /// order.  A query that the plan could not describe counts, as it does in the summary.
    /// </summary>
    public static ImmutableArray<PlannedDatabase> SelectedDatabases(RunPlan plan)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in plan.Files)
        {
            foreach (var query in file.Queries)
            {
                if (query is { NeedsEntry: true, IsSelected: true, Database: { } database })
                {
                    _ = names.Add(database);
                }
            }
        }

        return [.. plan.Databases.Where(database => names.Contains(database.Name))];
    }

    // The rows of the spec's table, in its order.
    private static QueryWork Decide(PlannedQuery query, bool ready, SidecarOnDisk? onDisk, bool force)
    {
        if (!query.NeedsEntry)
        {
            return new QueryWork(query, QueryState.NotNeeded);
        }

        // A file that is not ready has the plan's error, and a sidecar of a newer tool has SQLSRC221.
        if (
            !ready
            || query.Problems != QueryProblems.None
            || (query.IsSelected && onDisk?.NewerFormat is not null)
        )
        {
            return new QueryWork(query, QueryState.Failed);
        }

        if (query.IsSelected && force)
        {
            return new QueryWork(query, QueryState.ToDescribe);
        }

        if (
            query is { Hash: { } hash, Database: { } database }
            && onDisk?.Usable?.Find(query.Query.Name) is { } entry
            && entry.IsCurrentFor(hash, database)
        )
        {
            return new QueryWork(query, QueryState.Skipped, entry);
        }

        return new QueryWork(query, query.IsSelected ? QueryState.ToDescribe : QueryState.LeftOut);
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.RunDecisionsTests`
Expected: PASS, 17 tests.

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.

- [ ] **Step 7: Closing steps, and commit**

```bash
git add src tests docs/diagnostics.md
git commit -m "Read the sidecars that are there and decide for each query

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Describing, with `SQLSRC216` and the format of a describer's error

**Files:**
- Create: `src/SqlSource.Tool/Reporting/UnexpectedFailure.cs`
- Create in `src/SqlSource.Tool/Describing/`: `Redaction.cs`, `DescriberFaultException.cs`, `DescribeErrors.cs`, `DatabaseRuns.cs`
- Modify: `src/SqlSource.Tool/Cli.cs`, `src/SqlSource.Tool/AGENTS.md`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`
- Create: `tests/SqlSource.Tool.Tests/DatabaseRunsTests.cs`
- Modify: `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`

**Interfaces:**
- Consumes: `FileWork`, `QueryWork`, `QueryState`, `Connections`, `DatabaseConnection`, `ToolHost.Describers`, `ToolHost.Exchange`, `SampleSql.Build`, `EntryBuilder.Build`, `DescribeFailure`, `ServerInfo`, the descriptors of task 4
- Produces: `UnexpectedFailure.Of(Exception exception, Func<string, string?> getEnvironmentVariable) : ToolDiagnostic`, moved out of `Cli`
- Produces: `Redaction.Mark` (`"***"`), `Redaction.Of(string text, string secret) : string`, `Redaction.Of(ToolDiagnostic diagnostic, string secret) : ToolDiagnostic`
- Produces: `class DescriberFaultException(ToolDiagnostic diagnostic) : Exception` with `Diagnostic`; `Cli.RunAsync` reports it and returns `1`
- Produces: `DatabaseRuns.RunAsync(ImmutableArray<PlannedDatabase> selected, ImmutableArray<FileWork> files, Connections connections, ToolHost host, Reporter reporter, CancellationToken cancellationToken) : Task`.  After it no query is `ToDescribe`: each is `Described`, with its entry, or `Failed`.
- Produces: `ToolDiagnostics.NoDescriberForDialect` (216)

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tool.Tests/DatabaseRunsTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

// Step 3 of the run: one session for each database, its queries one at a time, and what is reported on the way.
public sealed class DatabaseRunsTests : IDisposable
{
    private const string Secret = "Host=db;Password=s3cret";

    private const string See = "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc";

    private const string Users =
        "-- name: GetUser\nSELECT 1 WHERE @id = 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n";

    private readonly TempFolder _folder = new();
    private readonly FakeDescriber _describer = new();
    private readonly RecordingExchange _exchange = new();
    private readonly Dictionary<string, string> _environment = [];
    private readonly StringWriter _error = new(CultureInfo.InvariantCulture) { NewLine = "\n" };
    private readonly TestProject _project;
    private readonly string _users;

    public DatabaseRunsTests()
    {
        _project = Plans.Postgres(_folder);
        _users = _project.AddSql("Users.sql", Users);
    }

    public void Dispose()
    {
        _error.Dispose();
        _folder.Dispose();
    }

    private static ConnectionArgument Parse(string text)
    {
        ConnectionArgument.TryParse(text, out var argument).ShouldBeTrue();
        return argument.ShouldNotBeNull();
    }

    // Decides for the plan and runs its databases.  Both databases have a connection unless "given" says otherwise.
    private async Task<ImmutableArray<FileWork>> RunAsync(
        RunPlan? plan = null,
        bool registered = true,
        string[]? given = null,
        CancellationToken cancellationToken = default
    )
    {
        plan ??= Plans.Of(_project);
        var files = RunDecisions.Decide(plan, force: false, []);
        var selected = RunDecisions.SelectedDatabases(plan);
        var connections = Connections.Resolve(
            [.. selected.Select(database => database.Name)],
            [.. (given ?? ["postgres=" + Secret, "billing=" + Secret]).Select(Parse)],
            name => _environment.GetValueOrDefault(name)
        );
        var host = Hosts.Create(_folder.Path, new FakeProcessRunner(), _folder.Path, environment: _environment) with
        {
            Describers = new DescriberRegistry(registered ? [_describer] : []),
            Exchange = _ => _exchange,
        };

        await DatabaseRuns.RunAsync(selected, files, connections, host, new Reporter(_error), cancellationToken);
        return files;
    }

    private static QueryState[] States(ImmutableArray<FileWork> files) =>
        [.. files.SelectMany(file => file.Queries).Select(query => query.State)];

    [Fact]
    public async Task Run_NoDescriberForTheDialect_IsSqlsrc216AtEachQuery()
    {
        var files = await RunAsync(registered: false);

        States(files).ShouldBe([QueryState.Failed, QueryState.Failed]);
        _error
            .ToString()
            .ShouldBe(
                $"{_users}(1,10): error SQLSRC216: This version of sqlsource cannot describe 'postgres'\n{See}216\n"
                    + $"{_users}(4,10): error SQLSRC216: This version of sqlsource cannot describe 'postgres'\n{See}216\n"
            );
    }

    [Fact]
    public async Task Run_Describer_DescribesEachQueryInOneSessionForItsDatabase()
    {
        var files = await RunAsync();

        _error.ToString().ShouldBeEmpty();
        States(files).ShouldBe([QueryState.Described, QueryState.Described]);
        _describer.Opened.Select(request => request.Database).ShouldBe(["postgres", "billing"]);
        _describer.Opened.ShouldAllBe(request => request.Connection == Secret && request.Exchange == _exchange);
        _describer.Closed.ShouldBe(2);
        _exchange.Methods.ShouldBe(["open", "describe", "open", "describe"]);

        var getUser = _describer.Described[0];
        getUser.Name.ShouldBe("GetUser");
        getUser.Sql.ShouldBe("SELECT 1 WHERE @id = 1;");
        getUser.Parameters.ShouldHaveSingleItem().Name.ShouldBe("id");

        var entry = files[0].Queries[1].Entry.ShouldNotBeNull();
        entry.Name.ShouldBe("GetInvoice");
        entry.Hash.ShouldBe(files[0].Queries[1].Planned.Hash);
        entry.Engine.ShouldBe("postgres");
        entry.Database.ShouldBe("billing");
        entry.ServerVersion.ShouldBe("16.4");
    }

    [Fact]
    public async Task Run_DatabaseWithoutAConnection_IsSqlsrc213OnceAndItsQueriesFail()
    {
        _ = _project.AddSql("More.sql", "-- name: More\nSELECT 3;\n");

        var files = await RunAsync(given: ["billing=" + Secret]);

        States(files).ShouldBe([QueryState.Failed, QueryState.Failed, QueryState.Described]);
        _describer.Opened.ShouldHaveSingleItem().Database.ShouldBe("billing");
        var more = _project.PathOf("More.sql");
        _error
            .ToString()
            .ShouldBe(
                $"{more}(1,10): error SQLSRC213: No connection is given for the database 'postgres'\n"
                    + "    help: pass --connection postgres=<connection string>, or set SQLSOURCE_CONNECTION_POSTGRES\n"
                    + $"{See}213\n"
            );
    }

    [Fact]
    public async Task Run_ConnectionForANameThatIsNoDatabase_IsListedInTheHelpOfSqlsrc213()
    {
        _ = await RunAsync(given: ["billing=" + Secret, "postgress=" + Secret, "reports=" + Secret]);

        _error
            .ToString()
            .ShouldContain(
                "    help: pass --connection postgres=<connection string>, or set SQLSOURCE_CONNECTION_POSTGRES.  "
                    + "--connection was given for postgress, reports, which are no databases of this run\n"
            );
    }

    [Fact]
    public async Task Run_TwoDatabasesWithOneVariable_IsSqlsrc214AtEachThatHasAQueryToDescribe()
    {
        var project = Plans.Postgres(_folder, "Other");
        var sql = project.AddSql(
            "Q.sql",
            "-- name: A\n-- database: app-v2\nSELECT 1;\n\n-- name: B\n-- database: app_v2\nSELECT 2;\n"
        );
        _environment["SQLSOURCE_CONNECTION_APP_V2"] = Secret;

        var files = await RunAsync(Plans.Of(project), given: []);

        States(files).ShouldBe([QueryState.Failed, QueryState.Failed]);
        _describer.Opened.ShouldBeEmpty();
        _error
            .ToString()
            .ShouldBe(
                $"{sql}(1,10): error SQLSRC214: The databases 'app-v2' and 'app_v2' both read their connection from "
                    + $"SQLSOURCE_CONNECTION_APP_V2\n{See}214\n"
                    + $"{sql}(5,10): error SQLSRC214: The databases 'app_v2' and 'app-v2' both read their connection "
                    + $"from SQLSOURCE_CONNECTION_APP_V2\n{See}214\n"
            );
    }

    [Fact]
    public async Task Run_VariableWithNoNameAndTwoDatabases_IsSqlsrc215ForTheOneWithoutAConnection()
    {
        _environment["SQLSOURCE_CONNECTION"] = Secret;

        var files = await RunAsync(given: ["postgres=" + Secret]);

        States(files).ShouldBe([QueryState.Described, QueryState.Failed]);
        _error
            .ToString()
            .ShouldBe(
                $"{_users}(4,10): error SQLSRC215: SQLSOURCE_CONNECTION is for a run with one database, and this "
                    + "run has 2: postgres, billing\n"
                    + "    help: set SQLSOURCE_CONNECTION_POSTGRES for 'postgres' and SQLSOURCE_CONNECTION_BILLING for "
                    + "'billing', or pass --connection <name>=<connection string> for each\n"
                    + $"{See}215\n"
            );
    }

    [Fact]
    public async Task Run_SessionThatCannotBeOpened_IsReportedOnceAndTheDatabasesQueriesFail()
    {
        _ = _project.AddSql("More.sql", "-- name: More\nSELECT 3;\n");
        _describer.OpenFailure = new DescribeFailure(
            FakeDescriber.Rejected,
            new EquatableArray<string>(["(open)"]),
            Step: null,
            new EquatableArray<string>(["28P01: password authentication failed"]),
            Help: null
        );

        var files = await RunAsync(given: ["postgres=" + Secret]);

        States(files).ShouldAllBe(state => state == QueryState.Failed);
        _describer.Described.ShouldBeEmpty();
        var more = _project.PathOf("More.sql");
        // SQLSRC213 for billing comes after, at its own query.
        _error
            .ToString()
            .ShouldStartWith(
                $"{more}(1,10): error SQLSRC999: The server rejected the query '(open)'\n"
                    + "    query: More, database postgres, postgres\n"
                    + "    server: 28P01: password authentication failed\n"
                    + "    see: https://example.test/sqlsrc999\n"
            );
    }

    [Fact]
    public async Task Run_QueryThatFails_IsReportedInTheFormatOfADescribersErrorAndTheSessionGoesOn()
    {
        _ = _project.AddSql("More.sql", "-- name: Bad\nSELECT 3;\n\n-- name: Good\nSELECT 4;\n");
        _describer.Failures["Bad"] = FakeDescriber.Failure("Bad", "42P01: relation \"users\" does not exist", "LINE 1");

        var files = await RunAsync();

        States(files)
            .ShouldBe([QueryState.Failed, QueryState.Described, QueryState.Described, QueryState.Described]);
        _describer.Closed.ShouldBe(2);
        _error
            .ToString()
            .ShouldBe(
                $"{_project.PathOf("More.sql")}(1,10): error SQLSRC999: The server rejected the query 'Bad'\n"
                    + "    query: Bad, database postgres, postgres 16.4, FakeDriver 1.2.3\n"
                    + "    step: describe columns\n"
                    + "    server: 42P01: relation \"users\" does not exist\n"
                    + "    server: LINE 1\n"
                    + "    help: check the SQL\n"
                    + "    see: https://example.test/sqlsrc999\n"
            );
    }

    [Fact]
    public async Task Run_DriverWithoutAVersion_IsNamedAlone()
    {
        _describer.Server = _describer.Server with { DriverVersion = null };
        _describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        _ = await RunAsync();

        _error.ToString().ShouldContain("    query: GetUser, database postgres, postgres 16.4, FakeDriver\n");
    }

    // The describer's rule is to put the value in nothing it returns.  The run does not rely on it.
    [Fact]
    public async Task Run_FailureThatHoldsTheConnection_IsPrintedWithoutIt()
    {
        _describer.Server = new ServerInfo($"16.4 at {Secret}", Secret, Secret, EquatableArray<ServerSetting>.Empty);
        _describer.Failures["GetUser"] = new DescribeFailure(
            FakeDescriber.Rejected,
            new EquatableArray<string>([$"GetUser on {Secret}"]),
            DescribeStep.Catalog,
            new EquatableArray<string>([$"could not connect to {Secret}"]),
            $"check {Secret}"
        );

        _ = await RunAsync();

        _error.ToString().ShouldNotContain("s3cret");
        _error
            .ToString()
            .ShouldBe(
                $"{_users}(1,10): error SQLSRC999: The server rejected the query 'GetUser on ***'\n"
                    + "    query: GetUser, database postgres, postgres 16.4 at ***, *** ***\n"
                    + "    step: catalog\n"
                    + "    server: could not connect to ***\n"
                    + "    help: check ***\n"
                    + "    see: https://example.test/sqlsrc999\n"
            );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    public async Task Run_DescriberThatThrows_IsAFaultWithoutTheConnection(string? debug)
    {
        if (debug is not null)
        {
            _environment[Cli.DebugVariable] = debug;
        }

        _describer.Exceptions["GetUser"] = new ArgumentException($"Keyword not supported in '{Secret}'");

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault.InnerException.ShouldBeNull();
        fault.Message.ShouldNotContain("s3cret");
        fault.Diagnostic.Descriptor.Id.ShouldBe("SQLSRC200");
        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.ArgumentException", "Keyword not supported in '***'"]);
        (fault.Diagnostic.Lines.Count > 0).ShouldBe(debug is not null);
        fault.Diagnostic.Lines.ShouldAllBe(line => line.Label == "trace" && !line.Text.Contains("s3cret"));
        // The session is closed all the same.
        _describer.Closed.ShouldBe(1);
    }

    [Fact]
    public async Task Run_DescriberThatThrowsOnOpen_IsAFaultWithoutTheConnection()
    {
        _describer.OpenException = new InvalidOperationException($"cannot parse {Secret}");

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.InvalidOperationException", "cannot parse ***"]);
    }

    // Review focus 2.
    [Fact]
    public async Task Run_DescriberThatReturnsNothing_IsAFault()
    {
        _describer.OpensNothing = true;

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.InvalidOperationException", "A describer returned nothing."]);
    }

    // Review focus 2.
    [Fact]
    public async Task Run_SessionThatThrowsWhenItIsClosed_IsAFaultWithoutTheConnection()
    {
        _describer.CloseException = new IOException($"lost {Secret}");

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault.Diagnostic.Arguments.ToArray().ShouldBe(["System.IO.IOException", "lost ***"]);
    }

    [Fact]
    public async Task Run_DescriptionWithAParameterTheQueryLacks_IsAFault()
    {
        _describer.Descriptions["GetUser"] = FakeDescriber.NoRows with
        {
            Parameters = new EquatableArray<DescribedParameter>([new DescribedParameter("nope", null, null)]),
        };

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync());

        fault
            .Diagnostic.Arguments[1]
            .ShouldBe("The describer gave the parameter 'nope', which the query 'GetUser' does not have.");
    }

    // Review focus 5: a database is opened, and needs a connection, only for a query to describe.
    [Fact]
    public async Task Run_DatabaseWithNothingToDescribe_NeedsNoConnectionAndIsNotOpened()
    {
        _ = _project.AddSql("Tokens.sql", "-- name: Find\n-- database: billing\nSELECT 1 {{where}};\n");
        var plan = Plans.Of(_project);
        _ = TestSidecar.WriteCurrent(plan.Files.Single(file => file.Path == _users));

        var files = await RunAsync(plan, given: []);

        _error.ToString().ShouldBeEmpty();
        _describer.Opened.ShouldBeEmpty();
        States(files).ShouldBe([QueryState.Failed, QueryState.Skipped, QueryState.Skipped]);
    }

    [Fact]
    public async Task Run_CancelledWhileAQueryIsDescribed_EndsAsCancelledAndNotAsAFault()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _describer.BeforeDescribe = _ => cancel.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => RunAsync(cancellationToken: cancel.Token));

        _describer.Described.ShouldHaveSingleItem();
        _describer.Closed.ShouldBe(1);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Of_TextThatHoldsTheSecretTwice_HasTheMarkInBothPlaces() =>
        Redaction.Of("a s3cret and a s3cret", "s3cret").ShouldBe("a *** and a ***");
}
```

In `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`, add:

```csharp
    [Fact]
    public void NoDescriberForDialect_Message_HoldsTheDialect() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.NoDescriberForDialect.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "postgres"
            )
            .ShouldBe("This version of sqlsource cannot describe 'postgres'");
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL to compile: `The name 'DatabaseRuns' does not exist in the current context`.

- [ ] **Step 3: Add the descriptor, its release row and its section**

In `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, after `UnnamedConnectionNotUsed`:

```csharp
    public static readonly DiagnosticDescriptor NoDescriberForDialect = new(
        id: "SQLSRC216",
        title: "No describer for the dialect",
        messageFormat: "This version of sqlsource cannot describe '{0}'",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc216",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

In `All`, add `NoDescriberForDialect,` after `UnnamedConnectionNotUsed,`.

In `src/SqlSource/AnalyzerReleases.Unshipped.md`, after the row of `SQLSRC215`:

```
SQLSRC216 | SqlSource | Error | No describer for the dialect
```

In `docs/diagnostics.md`, in the table after the row of `SQLSRC215`:

```markdown
| [SQLSRC216](#sqlsrc216) | No describer for the dialect |
```

and after the section `## SQLSRC215`:

````markdown
## SQLSRC216

**No describer for the dialect**

The query must be described, its dialect is one whose databases can be asked, and this version of the `sqlsource` tool has nothing that asks them.  It is not [SQLSRC209](#sqlsrc209), which is about a dialect that no version can describe.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC216: This version of sqlsource cannot describe 'postgres'
```

Update the tool, with `dotnet tool update SqlSource.Tool`, to a version that describes the dialect; this version describes none.  Until then, set the output of the queries to `sql`, with `SqlSourceOutput` or an `-- output: sql` marker, to use their constants and methods without types.  The error is reported at each query that the run would have described, and each counts as failed.
````

- [ ] **Step 4: Move the making of `SQLSRC200` out of `Cli`**

Create `src/SqlSource.Tool/Reporting/UnexpectedFailure.cs`:

```csharp
using System;
using System.Linq;
using SqlSource.Diagnostics;

namespace SqlSource.Tool.Reporting;

/// <summary>
/// An exception as <c>SQLSRC200</c>.
/// </summary>
internal static class UnexpectedFailure
{
    /// <summary>
    /// The error of an exception: its type and its message, and its stack trace as lines labelled <c>trace</c>
    /// when <c>SQLSOURCE_DEBUG</c> is set and not empty.
    /// </summary>
    public static ToolDiagnostic Of(Exception exception, Func<string, string?> getEnvironmentVariable)
    {
        var failure = ToolDiagnostic.Create(
            ToolDiagnostics.UnexpectedFailure,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message
        );
        if (string.IsNullOrEmpty(getEnvironmentVariable(Cli.DebugVariable)))
        {
            return failure;
        }

        var trace = exception
            .ToString()
            .Split('\n')
            .Select(static line => new ContinuationLine("trace", line.TrimEnd('\r')));
        return failure.WithLines([.. trace]);
    }
}
```

In `src/SqlSource.Tool/Cli.cs`: delete the private method `Failure`; replace its two calls, `Failure(exception, Environment.GetEnvironmentVariable)` and `Failure(exception, host.GetEnvironmentVariable)`, with `UnexpectedFailure.Of(exception, Environment.GetEnvironmentVariable)` and `UnexpectedFailure.Of(exception, host.GetEnvironmentVariable)`; add `using SqlSource.Tool.Describing;` and remove the usings that nothing needs any more; and in `RunAsync` add a catch between the one of `OperationCanceledException` and the one of `Exception`:

```csharp
        catch (DescriberFaultException fault)
        {
            // The error of an exception that a describer threw, already without the connection's value.
            reporter.Report(fault.Diagnostic);
            return 1;
        }
```

- [ ] **Step 5: Write `Redaction` and `DescriberFaultException`**

Create `src/SqlSource.Tool/Describing/Redaction.cs`:

```csharp
using System;
using System.Linq;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Takes a connection's value out of a text that a describer gave, before the text is printed.
/// </summary>
/// <remarks>
/// A describer's rule is to put the value in nothing it returns.  The run does not rely on it: a driver's message
/// for a connection string that is wrong can quote the string.  The whole value is replaced and nothing less: the
/// tool does not read a connection string, so it knows no part of one.
/// </remarks>
internal static class Redaction
{
    /// <summary>What stands where the value was.</summary>
    public const string Mark = "***";

    public static string Of(string text, string secret) =>
        secret.Length == 0 ? text : text.Replace(secret, Mark, StringComparison.Ordinal);

    /// <summary>The error with the value taken out of its arguments and of the text of each line under it.</summary>
    public static ToolDiagnostic Of(ToolDiagnostic diagnostic, string secret) =>
        diagnostic with
        {
            Arguments = new EquatableArray<string>([.. diagnostic.Arguments.Select(argument => Of(argument, secret))]),
            Lines = new EquatableArray<ContinuationLine>([
                .. diagnostic.Lines.Select(line => line with { Text = Of(line.Text, secret) }),
            ]),
        };
}
```

Create `src/SqlSource.Tool/Describing/DescriberFaultException.cs`:

```csharp
using System;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// An exception that a describer threw, which is a bug of the describer, as the <c>SQLSRC200</c> to report: with
/// the connection's value already taken out.  It holds no inner exception, whose message may hold the value.
/// <c>Cli.RunAsync</c> reports <see cref="Diagnostic" /> and the run ends.
/// </summary>
internal sealed class DescriberFaultException(ToolDiagnostic diagnostic) : Exception("A describer failed.")
{
    public ToolDiagnostic Diagnostic { get; } = diagnostic;
}
```

- [ ] **Step 6: Write `DescribeErrors`**

Create `src/SqlSource.Tool/Describing/DescribeErrors.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// The errors of describing, each at the name of a query.  None holds a connection's value: what a describer gave
/// goes through <see cref="Redaction" /> where it is reported.
/// </summary>
internal static class DescribeErrors
{
    private const string Help = "help";

    /// <summary><c>SQLSRC216</c>: this version has no describer for the dialect of the query's database.</summary>
    public static ToolDiagnostic NoDescriber(PlannedQuery query, SqlDialect dialect) =>
        ToolDiagnostic.At(
            ToolDiagnostics.NoDescriberForDialect,
            query.Query.NameLocation,
            SqlDialectName.Canonical(dialect)
        );

    /// <summary><c>SQLSRC213</c>, with the option, the variable, and the names given for no database.</summary>
    public static ToolDiagnostic NoConnection(PlannedQuery query, DatabaseConnection connection, Connections all)
    {
        var help = new StringBuilder("pass --connection ")
            .Append(connection.Database)
            .Append("=<connection string>, or set ")
            .Append(connection.Variable);
        if (!all.UnusedNames.IsEmpty)
        {
            _ = help.Append(".  --connection was given for ")
                .AppendJoin(", ", all.UnusedNames)
                .Append(all.UnusedNames.Length == 1 ? ", which is no database" : ", which are no databases")
                .Append(" of this run");
        }

        return ToolDiagnostic
            .At(ToolDiagnostics.DatabaseHasNoConnection, query.Query.NameLocation, connection.Database)
            .WithLines(new ContinuationLine(Help, help.ToString()));
    }

    /// <summary><c>SQLSRC214</c>: the database's variable is another selected database's too.</summary>
    public static ToolDiagnostic SharedVariable(PlannedQuery query, DatabaseConnection connection, string other) =>
        ToolDiagnostic.At(
            ToolDiagnostics.DatabasesShareConnectionVariable,
            query.Query.NameLocation,
            connection.Database,
            other,
            connection.Variable
        );

    /// <summary><c>SQLSRC215</c>, in the place of <c>SQLSRC213</c>, with the variable of each database.</summary>
    public static ToolDiagnostic UnnamedNotUsed(PlannedQuery query, Connections all)
    {
        var variables = all.Databases.Select(name => $"{ConnectionVariables.For(name)} for '{name}'");
        return ToolDiagnostic
            .At(
                ToolDiagnostics.UnnamedConnectionNotUsed,
                query.Query.NameLocation,
                all.Databases.Length.ToString(CultureInfo.InvariantCulture),
                string.Join(", ", all.Databases)
            )
            .WithLines(
                new ContinuationLine(
                    Help,
                    $"set {string.Join(" and ", variables)}, or pass --connection <name>=<connection string> for each"
                )
            );
    }

    /// <summary>
    /// A describer's failure, in the format the epic gives it: <c>query:</c>, <c>step:</c>, a <c>server:</c> line
    /// for each line of the server, and <c>help:</c>.  A part the tool does not have is left out.
    /// </summary>
    /// <param name="failure">What the describer returned.</param>
    /// <param name="query">The query it is reported at.</param>
    /// <param name="database">The query's database.</param>
    /// <param name="server">The session's server, or null when no session opened.</param>
    public static ToolDiagnostic Failure(
        DescribeFailure failure,
        PlannedQuery query,
        PlannedDatabase database,
        ServerInfo? server
    )
    {
        var lines = new List<ContinuationLine> { new("query", QueryLine(query, database, server)) };
        if (failure.Step is { } step)
        {
            lines.Add(new ContinuationLine("step", DescribeStepName.Of(step)));
        }

        lines.AddRange(failure.ServerLines.Select(static line => new ContinuationLine("server", line)));
        if (failure.Help is { } help)
        {
            lines.Add(new ContinuationLine(Help, help));
        }

        return ToolDiagnostic
            .At(failure.Descriptor, query.Query.NameLocation, [.. failure.Arguments])
            .WithLines([.. lines]);
    }

    // <name>, database <database>, <engine> <server version>, <driver> <version>
    private static string QueryLine(PlannedQuery query, PlannedDatabase database, ServerInfo? server)
    {
        var line = new StringBuilder(query.Query.Name)
            .Append(", database ")
            .Append(database.Name)
            .Append(", ")
            .Append(SqlDialectName.Canonical(database.Dialect));
        if (server is null)
        {
            return line.ToString();
        }

        _ = line.Append(' ').Append(server.Version);
        if (server.Driver is { } driver)
        {
            _ = line.Append(", ").Append(driver);
            if (server.DriverVersion is { } version)
            {
                _ = line.Append(' ').Append(version);
            }
        }

        return line.ToString();
    }
}
```

- [ ] **Step 7: Write `DatabaseRuns`**

Create `src/SqlSource.Tool/Describing/DatabaseRuns.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Step 3 of a run: for each database that has a query to describe, one session, and its queries one at a time.
/// A connection is not for several threads, and a describe takes milliseconds.
/// </summary>
/// <remarks>
/// <para>
/// It reports as it goes, so that a run on a slow database shows what it found.  When it returns, no query is
/// <see cref="QueryState.ToDescribe" />: each was described or failed.
/// </para>
/// <para>
/// This is the one place outside <c>Cli</c> that catches every exception: a describer's, which is a bug of the
/// describer and may hold the connection's value.  It is thrown again as a <see cref="DescriberFaultException" />
/// without the value, and the run ends.  A run that was cancelled ends as one.
/// </para>
/// </remarks>
internal static class DatabaseRuns
{
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

            Fail(queries);
            return;
        }

        var first = queries[0].Planned;
        var connection = connections.For(database.Name);
        if (connection.Value is not { } value)
        {
            reporter.Report(
                connection.SharesVariableWith is { } other ? DescribeErrors.SharedVariable(first, connection, other)
                : connections.UnnamedIsSetAndNotUsed ? DescribeErrors.UnnamedNotUsed(first, connections)
                : DescribeErrors.NoConnection(first, connection, connections)
            );
            Fail(queries);
            return;
        }

        var guard = new Guard(value, host.GetEnvironmentVariable, cancellationToken);
        var request = new OpenRequest(database.Name, value, host.Exchange(database.Name));
        var opened = await guard.RunAsync(() => describer.OpenAsync(request, cancellationToken));
        if (opened.Session is not { } session)
        {
            var failure = guard.Run(() => opened.Failure ?? throw new InvalidOperationException(Guard.Nothing));
            reporter.Report(Redaction.Of(DescribeErrors.Failure(failure, first, database, server: null), value));
            Fail(queries);
            return;
        }

        try
        {
            var server = guard.Run(() => session.Server);
            foreach (var query in queries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var planned = query.Planned;
                var describe = new DescribeRequest(
                    planned.Query.Name,
                    SampleSql.Build(planned.Query),
                    planned.Query.Parameters
                );
                var result = await guard.RunAsync(() => session.DescribeAsync(describe, cancellationToken));
                if (result.Description is { } description)
                {
                    query.Entry = guard.Run(() =>
                        EntryBuilder.Build(planned, database.Dialect, database.Name, server, description)
                    );
                    query.State = QueryState.Described;
                }
                else
                {
                    var failure = guard.Run(() => result.Failure ?? throw new InvalidOperationException(Guard.Nothing));
                    reporter.Report(Redaction.Of(DescribeErrors.Failure(failure, planned, database, server), value));
                    query.State = QueryState.Failed;
                }
            }
        }
        finally
        {
            await guard.RunAsync(session.DisposeAsync);
        }
    }

    // The queries of a database that the run decided to describe, in the plan's order.
    private static List<QueryWork> ToDescribe(ImmutableArray<FileWork> files, string database) =>
        [
            .. files
                .SelectMany(static file => file.Queries)
                .Where(query =>
                    query.State == QueryState.ToDescribe
                    && string.Equals(query.Planned.Database, database, StringComparison.OrdinalIgnoreCase)
                ),
        ];

    private static void Fail(List<QueryWork> queries)
    {
        foreach (var query in queries)
        {
            query.State = QueryState.Failed;
        }
    }

    // Runs what a describer gives or does.  Whatever it throws is a bug of the describer and is thrown again as
    // the SQLSRC200 to report, without the connection's value.
    private sealed class Guard(
        string secret,
        Func<string, string?> getEnvironmentVariable,
        CancellationToken cancellationToken
    )
    {
        public const string Nothing = "A describer returned nothing.";

        public async Task<T> RunAsync<T>(Func<Task<T>> call)
            where T : class
        {
            try
            {
                return await call() ?? throw new InvalidOperationException(Nothing);
            }
            catch (Exception exception) when (!IsCancellation(exception))
            {
                throw Fault(exception);
            }
        }

        public async Task RunAsync(Func<ValueTask> call)
        {
            try
            {
                await call();
            }
            catch (Exception exception) when (!IsCancellation(exception))
            {
                throw Fault(exception);
            }
        }

        public T Run<T>(Func<T> call)
            where T : class
        {
            try
            {
                return call() ?? throw new InvalidOperationException(Nothing);
            }
            catch (Exception exception) when (!IsCancellation(exception))
            {
                throw Fault(exception);
            }
        }

        private bool IsCancellation(Exception exception) =>
            exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

        private DescriberFaultException Fault(Exception exception) =>
            new(Redaction.Of(UnexpectedFailure.Of(exception, getEnvironmentVariable), secret));
    }
}
```

A `DescriberFaultException` that the `try` throws is thrown again by the `finally` only when closing the session fails too, and then the second one replaces the first.  Both are bugs of one describer, and one `SQLSRC200` is enough.

- [ ] **Step 8: Say in the tool's `AGENTS.md` where else an exception is caught**

In `src/SqlSource.Tool/AGENTS.md`, replace the bullet that starts `- **An exception is `SQLSRC200`**` with:

```markdown
- **An exception is `SQLSRC200`**, caught in `Cli.RunAsync`, and in `Cli.Run` when the host itself cannot be made, as when the working directory was deleted, and in one other place: `Describing/DatabaseRuns.cs` catches what a describer throws, since its message may hold a connection's value, and throws a `DescriberFaultException` that holds the same error with the value taken out, which `Cli.RunAsync` reports.  `Reporting/UnexpectedFailure.cs` makes the error.  The stack trace is continuation lines labelled `trace`, written only when `SQLSOURCE_DEBUG` is set and not empty.
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.DatabaseRunsTests`
Expected: PASS, 19 tests.

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.  The tests of `Cli` that expect `SQLSRC200` pass as before: the error is made by the same code in its new place.

- [ ] **Step 10: Closing steps, and commit**

```bash
git add src tests docs/diagnostics.md
git commit -m "Describe the queries of each database in one session

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: A file's outcome and its target

**Files:**
- Create in `src/SqlSource.Tool/Describing/`: `FileAction.cs`, `HeldBackQuery.cs`, `FileOutcome.cs`, `FileOutcomes.cs`
- Create: `tests/SqlSource.Tool.Tests/FileOutcomesTests.cs`

**Interfaces:**
- Consumes: `FileWork`, `QueryWork`, `QueryState`, `SidecarOnDisk.Text`, `SidecarWriter.Write`, `SidecarFormat.Version`, `PackageVersion.Prefix`
- Produces: `enum FileAction { None, Unchanged, Write, Delete, HeldBack }`
- Produces: `record HeldBackQuery(string Name, bool Failed)`: `Failed` is false for a query that is not in the run
- Produces: `record FileOutcome(PlannedFile File, string SidecarPath, FileAction Action, Sidecar? Target, string? Text, EquatableArray<HeldBackQuery> HeldBackBy, bool HasDescribed)`
- Produces: `FileOutcomes.Decide(ImmutableArray<FileWork> files, bool runHasFilter, Func<string, bool> exists) : ImmutableArray<FileOutcome>`, one outcome for each file in the files' order.  It changes nothing on the disk: sub-phase 2.6 compares where task 8 writes.

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tool.Tests/FileOutcomesTests.cs`:

```csharp
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Planning;
using Xunit;

namespace SqlSource.Tool.Tests;

// Step 4 of the run: what happens to each file's sidecar, and what the sidecar is to hold.  Nothing is written here.
public sealed class FileOutcomesTests : IDisposable
{
    private const string Users =
        "-- name: GetUser\nSELECT 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n\n"
        + "-- name: Plain\n-- output: sql\nSELECT 3;\n";

    private static readonly RunFilters OnlyBilling = new([], ["billing"]);

    private readonly TempFolder _folder = new();
    private readonly TestProject _project;

    public FileOutcomesTests()
    {
        _project = Plans.Postgres(_folder);
        _ = _project.AddSql("Users.sql", Users);
    }

    public void Dispose() => _folder.Dispose();

    // Decides for the plan, and describes what the run would: each such query gets an entry of a newer server.
    private static ImmutableArray<FileWork> Described(RunPlan plan, bool force = false, params string[] failing)
    {
        var files = RunDecisions.Decide(plan, force, []);
        foreach (var query in files.SelectMany(file => file.Queries).Where(q => q.State == QueryState.ToDescribe))
        {
            if (failing.Contains(query.Planned.Query.Name))
            {
                query.State = QueryState.Failed;
                continue;
            }

            query.State = QueryState.Described;
            query.Entry = TestSidecar.EntryFor(query.Planned) with { ServerVersion = "17.0" };
        }

        return files;
    }

    private static FileOutcome Outcome(ImmutableArray<FileWork> files, bool runHasFilter = false) =>
        FileOutcomes.Decide(files, runHasFilter, File.Exists)[0];

    [Fact]
    public void Decide_EveryQueryDescribed_WritesATargetOfTheToolsVersionsInTheFilesOrder()
    {
        var plan = Plans.Of(_project);

        var outcome = Outcome(Described(plan));

        outcome.Action.ShouldBe(FileAction.Write);
        outcome.File.ShouldBe(plan.Files[0]);
        outcome.SidecarPath.ShouldBe(plan.Files[0].Path + ".json");
        var target = outcome.Target.ShouldNotBeNull();
        target.FormatVersion.ShouldBe(SidecarFormat.Version);
        target.ToolVersion.ShouldBe(PackageVersion.Prefix);
        target.Queries.Select(entry => (entry.Name, entry.ServerVersion))
            .ShouldBe([("GetUser", "17.0"), ("GetInvoice", "17.0")]);
        outcome.Text.ShouldBe(SidecarWriter.Write(target));
    }

    [Fact]
    public void Decide_EveryEntryCurrent_IsUnchanged()
    {
        var plan = Plans.Of(_project);
        _ = TestSidecar.WriteCurrent(plan.Files[0]);

        Outcome(Described(plan)).Action.ShouldBe(FileAction.Unchanged);
    }

    // Review focus 4: what git or an editor did to the file's bytes is no change.
    [Fact]
    public void Decide_SameContentWithAByteOrderMarkAndOtherLineEndings_IsUnchanged()
    {
        var plan = Plans.Of(_project);
        var path = TestSidecar.WriteCurrent(plan.Files[0]);
        var text = File.ReadAllText(path).Replace("\n", "\r\n", StringComparison.Ordinal);
        File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Outcome(Described(plan)).Action.ShouldBe(FileAction.Unchanged);
    }

    [Fact]
    public void Decide_OneQueryDescribedAndOneKept_WritesBoth()
    {
        var plan = Plans.Of(_project);
        _ = TestSidecar.Write(plan.Files[0], [TestSidecar.EntryFor(plan.Files[0].Queries[1])]);

        var outcome = Outcome(Described(plan));

        outcome.Action.ShouldBe(FileAction.Write);
        outcome.Target.ShouldNotBeNull().Queries.Select(entry => (entry.Name, entry.ServerVersion))
            .ShouldBe([("GetUser", "17.0"), ("GetInvoice", "16.4")]);
    }

    [Fact]
    public void Decide_EntryOfAQueryThatIsGone_IsDroppedAndTheFileIsWritten()
    {
        var plan = Plans.Of(_project);
        var file = plan.Files[0];
        var gone = TestSidecar.EntryFor(file.Queries[0]) with { Name = "Gone" };
        _ = TestSidecar.Write(file, [.. file.Queries.Take(2).Select(TestSidecar.EntryFor), gone]);

        var outcome = Outcome(Described(plan));

        outcome.Action.ShouldBe(FileAction.Write);
        outcome.Target.ShouldNotBeNull().Queries.Select(entry => entry.Name).ShouldBe(["GetUser", "GetInvoice"]);
    }

    [Fact]
    public void Decide_QueryThatFailed_HoldsTheFileBackAndSaysThatOneWasDescribed()
    {
        var outcome = Outcome(Described(Plans.Of(_project), failing: "GetUser"));

        outcome.Action.ShouldBe(FileAction.HeldBack);
        outcome.Target.ShouldBeNull();
        outcome.Text.ShouldBeNull();
        outcome.HeldBackBy.ToArray().ShouldBe([new HeldBackQuery("GetUser", Failed: true)]);
        outcome.HasDescribed.ShouldBeTrue();
    }

    [Fact]
    public void Decide_EveryQueryFailed_HoldsTheFileBackAndSaysThatNoneWasDescribed()
    {
        var outcome = Outcome(Described(Plans.Of(_project), failing: ["GetUser", "GetInvoice"]));

        outcome.Action.ShouldBe(FileAction.HeldBack);
        outcome.HeldBackBy.Count.ShouldBe(2);
        outcome.HasDescribed.ShouldBeFalse();
    }

    [Fact]
    public void Decide_QueryThatIsNotInTheRunAndHasNoCurrentEntry_HoldsTheFileBack()
    {
        var outcome = Outcome(Described(Plans.Of(OnlyBilling, _project)), runHasFilter: true);

        outcome.Action.ShouldBe(FileAction.HeldBack);
        outcome.HeldBackBy.ToArray().ShouldBe([new HeldBackQuery("GetUser", Failed: false)]);
        outcome.HasDescribed.ShouldBeTrue();
    }

    [Fact]
    public void Decide_QueryThatIsNotInTheRunAndHasACurrentEntry_IsWrittenBesideTheNewOne()
    {
        var all = Plans.Of(_project);
        _ = TestSidecar.Write(all.Files[0], [TestSidecar.EntryFor(all.Files[0].Queries[0])]);

        var outcome = Outcome(Described(Plans.Of(OnlyBilling, _project)), runHasFilter: true);

        outcome.Action.ShouldBe(FileAction.Write);
        outcome.Target.ShouldNotBeNull().Queries.Select(entry => (entry.Name, entry.ServerVersion))
            .ShouldBe([("GetUser", "16.4"), ("GetInvoice", "17.0")]);
    }

    [Fact]
    public void Decide_FileWithNoSelectedQuery_IsNotTouched()
    {
        var project = Plans.Postgres(_folder, "Other");
        _ = project.AddSql("Q.sql", "-- name: A\nSELECT 1;\n");
        var plan = Plans.Of(OnlyBilling, project);
        File.WriteAllText(plan.Files[0].Path + ".json", "anything");

        var outcome = Outcome(Described(plan), runHasFilter: true);

        outcome.Action.ShouldBe(FileAction.None);
        outcome.Target.ShouldBeNull();
    }

    [Theory]
    [InlineData(false, true, FileAction.Delete)]
    [InlineData(true, true, FileAction.None)]
    [InlineData(false, false, FileAction.None)]
    public void Decide_FileThatNeedsNoEntry_LosesItsSidecarOnlyInARunWithNoFilter(
        bool runHasFilter,
        bool sidecarExists,
        FileAction action
    )
    {
        var project = Plans.Postgres(_folder, "Other");
        var sql = project.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 3;\n");
        if (sidecarExists)
        {
            File.WriteAllText(sql + ".json", "anything");
        }

        Outcome(Described(Plans.Of(project)), runHasFilter).Action.ShouldBe(action);
    }

    [Fact]
    public void Decide_FileThatIsNotReady_IsNotTouched()
    {
        _project.Properties["SqlSourceDialect"] = "ansi";
        var plan = Plans.Of(_project);
        File.WriteAllText(plan.Files[0].Path + ".json", "anything");

        Outcome(Described(plan)).Action.ShouldBe(FileAction.None);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL to compile: `The name 'FileOutcomes' does not exist in the current context`.

- [ ] **Step 3: Write the four types**

Create `src/SqlSource.Tool/Describing/FileAction.cs`:

```csharp
namespace SqlSource.Tool.Describing;

/// <summary>
/// What a run does with the sidecar of one file.
/// </summary>
internal enum FileAction
{
    /// <summary>
    /// Nothing, and nothing is said: the file is not ready, has no selected query that needs an entry, or needs no
    /// sidecar and has none to delete or the run has a filter.
    /// </summary>
    None,

    /// <summary>The target is what the file on the disk holds.</summary>
    Unchanged,

    /// <summary>The target is written.</summary>
    Write,

    /// <summary>The sidecar that is there is deleted: no query of the file needs an entry.</summary>
    Delete,

    /// <summary>
    /// The sidecar is left as it was, because a query that needs an entry failed or was left out.  When a query of
    /// the file was described in this run, that is <c>SQLSRC217</c>.
    /// </summary>
    HeldBack,
}
```

Create `src/SqlSource.Tool/Describing/HeldBackQuery.cs`:

```csharp
namespace SqlSource.Tool.Describing;

/// <summary>
/// A query that kept its file from being written.
/// </summary>
/// <param name="Name">The query.</param>
/// <param name="Failed">Whether it failed.  False for one that is not in the run and has no current entry.</param>
internal sealed record HeldBackQuery(string Name, bool Failed);
```

Create `src/SqlSource.Tool/Describing/FileOutcome.cs`:

```csharp
using SqlSource.Snapshot;
using SqlSource.Tool.Planning;

namespace SqlSource.Tool.Describing;

/// <summary>
/// What a run does with the sidecar of one file, as data.  <see cref="SidecarStore" /> applies it to the disk, and
/// sub-phase 2.6 compares in its place.
/// </summary>
/// <param name="File">The file of the plan.</param>
/// <param name="SidecarPath">The full path of its sidecar.</param>
/// <param name="Action">What is done.</param>
/// <param name="Target">
/// What the sidecar is to hold, for <see cref="FileAction.Write" /> and <see cref="FileAction.Unchanged" />.
/// </param>
/// <param name="Text">The target as the writer gives it.  Set with <paramref name="Target" />.</param>
/// <param name="HeldBackBy">The queries that held the file back, in the file's order.</param>
/// <param name="HasDescribed">Whether a query of the file was described in this run.</param>
internal sealed record FileOutcome(
    PlannedFile File,
    string SidecarPath,
    FileAction Action,
    Sidecar? Target,
    string? Text,
    EquatableArray<HeldBackQuery> HeldBackBy,
    bool HasDescribed
);
```

Create `src/SqlSource.Tool/Describing/FileOutcomes.cs`:

```csharp
using System;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Snapshot;
using SqlSource.Tool.Planning;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Step 4 of a run: gives each file its outcome, with its target.  It changes nothing on the disk.
/// </summary>
internal static class FileOutcomes
{
    /// <summary>
    /// Decides for each file, in the files' order.
    /// </summary>
    /// <param name="files">The files, with every query described, kept, failed or left out.</param>
    /// <param name="runHasFilter">
    /// Whether the run has <c>--project</c>, <c>--database</c> or a <c>.sql</c> path.  A sidecar is deleted only in
    /// a run with none: a run on a part does not know what the rest needs of a file.
    /// </param>
    /// <param name="exists">Whether a file is on the disk.</param>
    public static ImmutableArray<FileOutcome> Decide(
        ImmutableArray<FileWork> files,
        bool runHasFilter,
        Func<string, bool> exists
    ) => [.. files.Select(file => Decide(file, runHasFilter, exists))];

    private static FileOutcome Decide(FileWork file, bool runHasFilter, Func<string, bool> exists)
    {
        var path = SidecarFormat.PathFor(file.File.Path);
        if (file.File.State != PlannedFileState.Ready)
        {
            return Of(file, path, FileAction.None);
        }

        var needing = file.Queries.Where(static query => query.Planned.NeedsEntry).ToList();
        if (needing.Count == 0)
        {
            // A sidecar that would be empty.
            return Of(file, path, !runHasFilter && exists(path) ? FileAction.Delete : FileAction.None);
        }

        if (!needing.Exists(static query => query.Planned.IsSelected))
        {
            return Of(file, path, FileAction.None);
        }

        if (needing.Exists(static query => query.State == QueryState.ToDescribe))
        {
            throw new InvalidOperationException($"A query of '{file.File.Path}' is still to be described.");
        }

        var heldBackBy = needing
            .Where(static query => query.State is QueryState.Failed or QueryState.LeftOut)
            .Select(static query => new HeldBackQuery(query.Planned.Query.Name, query.State == QueryState.Failed))
            .ToImmutableArray();
        if (!heldBackBy.IsEmpty)
        {
            return new FileOutcome(
                file.File,
                path,
                FileAction.HeldBack,
                Target: null,
                Text: null,
                new EquatableArray<HeldBackQuery>(heldBackBy),
                needing.Exists(static query => query.State == QueryState.Described)
            );
        }

        // Every query that needs an entry was described or has a current one.  An entry on the disk for a query
        // that is gone, or that needs none, is in no target, so writing drops it.
        var target = new Sidecar(
            SidecarFormat.Version,
            PackageVersion.Prefix,
            new EquatableArray<SidecarEntry>([
                .. needing.Select(static query =>
                    query.Entry ?? throw new InvalidOperationException($"'{query.Planned.Query.Name}' has no entry.")
                ),
            ])
        );
        var text = SidecarWriter.Write(target);
        return new FileOutcome(
            file.File,
            path,
            file.OnDisk?.Text is { } onDisk && SameText(onDisk, text) ? FileAction.Unchanged : FileAction.Write,
            target,
            text,
            EquatableArray<HeldBackQuery>.Empty,
            needing.Exists(static query => query.State == QueryState.Described)
        );
    }

    private static FileOutcome Of(FileWork file, string path, FileAction action) =>
        new(file.File, path, action, Target: null, Text: null, EquatableArray<HeldBackQuery>.Empty, HasDescribed: false);

    // A checkout may give the file other line endings than the writer's.
    private static bool SameText(string onDisk, string written) =>
        string.Equals(onDisk.Replace("\r\n", "\n", StringComparison.Ordinal), written, StringComparison.Ordinal);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.FileOutcomesTests`
Expected: PASS, 14 tests.

- [ ] **Step 5: Closing steps, and commit**

```bash
git add src/SqlSource.Tool tests/SqlSource.Tool.Tests
git commit -m "Give each file its outcome and its target

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Applying the outcomes, with `SQLSRC217` and `SQLSRC218`

**Files:**
- Create: `src/SqlSource.Tool/Describing/SidecarStore.cs`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`
- Create: `tests/SqlSource.Tool.Tests/SidecarStoreTests.cs`
- Modify: `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`

**Interfaces:**
- Consumes: `FileOutcome`, `FileAction`, `HeldBackQuery`, `Reporter`
- Produces: `SidecarStore.Apply(ImmutableArray<FileOutcome> outcomes, Reporter reporter, CancellationToken cancellationToken)`
- Produces: `ToolDiagnostics.SidecarNotWritten` (217) and `ToolDiagnostics.FileCannotBeChanged` (218)

- [ ] **Step 1: Write the failing tests**

Create `tests/SqlSource.Tool.Tests/SidecarStoreTests.cs`:

```csharp
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Shouldly;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

// The one place that changes a sidecar on the disk: a file is written whole or not at all.
public sealed class SidecarStoreTests : IDisposable
{
    private const string See = "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc";

    private static readonly DateTime Old = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);

    private readonly TempFolder _folder = new();
    private readonly StringWriter _error = new(CultureInfo.InvariantCulture) { NewLine = "\n" };
    private readonly PlannedFile _file;
    private readonly string _sidecar;

    public SidecarStoreTests()
    {
        var project = Plans.Postgres(_folder);
        _ = project.AddSql("Users.sql", "-- name: GetUser\nSELECT 1;\n");
        _file = Plans.Of(project).Files[0];
        _sidecar = _file.Path + ".json";
    }

    public void Dispose()
    {
        _error.Dispose();
        _folder.Dispose();
    }

    private FileOutcome Outcome(FileAction action, string? text = null, params HeldBackQuery[] heldBackBy) =>
        new(
            _file,
            _sidecar,
            action,
            Target: null,
            text,
            new EquatableArray<HeldBackQuery>([.. heldBackBy]),
            HasDescribed: false
        );

    private void Apply(params FileOutcome[] outcomes) =>
        SidecarStore.Apply([.. outcomes], new Reporter(_error), TestContext.Current.CancellationToken);

    // The names of what is in the project's folder, to see that nothing else was left there.
    private string[] Left() =>
        [
            .. Directory
                .EnumerateFileSystemEntries(Path.GetDirectoryName(_sidecar)!)
                .Select(entry => Path.GetFileName(entry))
                .Order(StringComparer.Ordinal),
        ];

    [Fact]
    public void Apply_Write_MakesTheFileWithTheTextAndLeavesNoTemporaryFile()
    {
        Apply(Outcome(FileAction.Write, "{\n}\n"));

        File.ReadAllBytes(_sidecar).ShouldBe("{\n}\n"u8.ToArray());
        Left().ShouldBe(["App.csproj", "Queries.cs", "Users.sql", "Users.sql.json"]);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Apply_WriteOverAFile_ReplacesIt()
    {
        File.WriteAllText(_sidecar, "old and longer than the new text");

        Apply(Outcome(FileAction.Write, "new"));

        File.ReadAllText(_sidecar).ShouldBe("new");
        Left().Length.ShouldBe(4);
    }

    [Theory]
    [InlineData(FileAction.Unchanged)]
    [InlineData(FileAction.None)]
    [InlineData(FileAction.HeldBack)]
    public void Apply_ActionThatChangesNothing_LeavesTheFileAsItWas(FileAction action)
    {
        File.WriteAllText(_sidecar, "as it was");
        File.SetLastWriteTimeUtc(_sidecar, Old);

        Apply(Outcome(action, action == FileAction.Unchanged ? "as it was" : null));

        File.ReadAllText(_sidecar).ShouldBe("as it was");
        File.GetLastWriteTimeUtc(_sidecar).ShouldBe(Old);
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Apply_Delete_RemovesTheFile()
    {
        File.WriteAllText(_sidecar, "of a file that needs none");

        Apply(Outcome(FileAction.Delete));

        File.Exists(_sidecar).ShouldBeFalse();
        _error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Apply_HeldBackWithAQueryDescribed_IsSqlsrc217WithTheQueriesThatHeldItBack()
    {
        var outcome = Outcome(
            FileAction.HeldBack,
            text: null,
            new HeldBackQuery("GetUser", Failed: true),
            new HeldBackQuery("GetInvoice", Failed: false)
        ) with
        {
            HasDescribed = true,
        };

        Apply(outcome);

        _error
            .ToString()
            .ShouldBe(
                $"{_file.Path} : error SQLSRC217: The sidecar of '{_file.Path}' was not written, because not every "
                    + "query of the file has an entry\n"
                    + "    query: GetUser: failed\n"
                    + "    query: GetInvoice: not in this run\n"
                    + "    help: describe the whole file, or fix the queries that failed\n"
                    + $"{See}217\n"
            );
        File.Exists(_sidecar).ShouldBeFalse();
    }

    // Review focus 1: a path that cannot be written, on every operating system and for every user.
    [Fact]
    public void Apply_WriteWhereADirectoryStands_IsSqlsrc218AndLeavesNoTemporaryFile()
    {
        _ = Directory.CreateDirectory(_sidecar);

        Apply(Outcome(FileAction.Write, "{}"));

        _error.ToString().ShouldStartWith($"{_sidecar} : error SQLSRC218: '{_sidecar}' could not be written: ");
        _error.ToString().ShouldEndWith($"{See}218\n");
        Directory.Exists(_sidecar).ShouldBeTrue();
        Left().ShouldBe(["App.csproj", "Queries.cs", "Users.sql", "Users.sql.json"]);
    }

    [Fact]
    public void Apply_DeleteOfWhatIsNoFile_IsSqlsrc218()
    {
        _ = Directory.CreateDirectory(_sidecar);

        Apply(Outcome(FileAction.Delete));

        _error.ToString().ShouldStartWith($"{_sidecar} : error SQLSRC218: '{_sidecar}' could not be deleted: ");
    }

    [Fact]
    public void Apply_FailureOfOneFile_DoesNotKeepTheNextFromBeingWritten()
    {
        _ = Directory.CreateDirectory(_sidecar);
        var other = _file.Path + ".other.json";

        Apply(Outcome(FileAction.Write, "{}"), Outcome(FileAction.Write, "{}") with { SidecarPath = other });

        File.ReadAllText(other).ShouldBe("{}");
        _error.ToString().Split("error SQLSRC218").Length.ShouldBe(2);
    }

    [Fact]
    public void Apply_RunThatWasCancelled_WritesNothing()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        _ = Should.Throw<OperationCanceledException>(() =>
            SidecarStore.Apply([Outcome(FileAction.Write, "{}")], new Reporter(_error), cancelled.Token)
        );

        Left().ShouldBe(["App.csproj", "Queries.cs", "Users.sql"]);
    }
}
```

In `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`, add:

```csharp
    [Fact]
    public void SidecarNotWritten_Message_HoldsTheSqlFile() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.SidecarNotWritten.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "/work/Users.sql"
            )
            .ShouldBe(
                "The sidecar of '/work/Users.sql' was not written, because not every query of the file has an entry"
            );

    [Theory]
    [InlineData("written")]
    [InlineData("deleted")]
    public void FileCannotBeChanged_Message_HoldsTheFileWhatWasTriedAndTheReason(string tried) =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.FileCannotBeChanged.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "/work/Users.sql.json",
                tried,
                "Access is denied."
            )
            .ShouldBe($"'/work/Users.sql.json' could not be {tried}: Access is denied.");
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build SqlSource.slnx`
Expected: FAIL to compile: `The name 'SidecarStore' does not exist in the current context`.

- [ ] **Step 3: Add the two descriptors, their release rows and their sections**

In `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, after `NoDescriberForDialect`:

```csharp
    public static readonly DiagnosticDescriptor SidecarNotWritten = new(
        id: "SQLSRC217",
        title: "Sidecar was not written",
        messageFormat: "The sidecar of '{0}' was not written, because not every query of the file has an entry",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc217",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor FileCannotBeChanged = new(
        id: "SQLSRC218",
        title: "File could not be changed",
        messageFormat: "'{0}' could not be {1}: {2}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc218",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

In `All`, add `SidecarNotWritten, FileCannotBeChanged,` after `NoDescriberForDialect,`.

In `src/SqlSource/AnalyzerReleases.Unshipped.md`, after the row of `SQLSRC216`:

```
SQLSRC217 | SqlSource | Error | Sidecar was not written
SQLSRC218 | SqlSource | Error | File could not be changed
```

In `docs/diagnostics.md`, in the table after the row of `SQLSRC216`:

```markdown
| [SQLSRC217](#sqlsrc217) | Sidecar was not written |
| [SQLSRC218](#sqlsrc218) | File could not be changed |
```

and after the section `## SQLSRC216`:

````markdown
## SQLSRC217

**Sidecar was not written**

A sidecar is written whole or not at all: when every query of its `.sql` file that needs an entry was described in this run or already has a current one.  Here the run described a query of the file and could not write the result, because another query of the file has neither.  The lines under the message name each such query and say why: it `failed`, with an error of its own above, or it is `not in this run`, because `--database` or a `.sql` path left it out and its entry is missing or out of date.

```console
/work/App/Queries/Users.sql : error SQLSRC217: The sidecar of '/work/App/Queries/Users.sql' was not written, because not every query of the file has an entry
    query: GetInvoice: not in this run
    help: describe the whole file, or fix the queries that failed
```

Mend the queries that failed, or run `describe` without the filter that left a query out, so that every query of the file is described in one run.  The sidecar on the disk is as it was before the run.  The error is there so that a description that was not saved does not look like a success; a file that was held back with none of its queries described has only the errors of those queries.

## SQLSRC218

**File could not be changed**

The tool could not write a sidecar, or could not delete one that its `.sql` file no longer needs.  The message says which, and ends with the reason the operating system gave.

```console
/work/App/Queries/Users.sql.json : error SQLSRC218: '/work/App/Queries/Users.sql.json' could not be written: Access to the path '/work/App/Queries/Users.sql.json' is denied.
```

Give yourself the right to write in the folder of the `.sql` file, or close the program that holds the sidecar open, and run the command again.  A sidecar is written to a temporary file beside it and then moved over the old one, so a run that fails or is stopped leaves the old file or the new one and never a part of either.  The rest of the run goes on.
````

- [ ] **Step 4: Write `SidecarStore`**

Create `src/SqlSource.Tool/Describing/SidecarStore.cs`:

```csharp
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using SqlSource.Diagnostics;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Applies the outcomes of a run to the disk.  It is the one place that writes or deletes a sidecar, and the one
/// thing the tool writes into a project.
/// </summary>
/// <remarks>
/// A sidecar is written whole or not at all: to a temporary file in the same folder, which is then moved over the
/// old one, so that a run that is stopped leaves the old file or the new one.
/// </remarks>
internal static class SidecarStore
{
    private const string Written = "written";

    private const string Deleted = "deleted";

    /// <summary>
    /// Writes and deletes what the outcomes say, and reports <c>SQLSRC217</c> for a file that was held back though
    /// a query of it was described, and <c>SQLSRC218</c> for a file the system would not let it change.  A run
    /// that was cancelled changes no further file.
    /// </summary>
    public static void Apply(ImmutableArray<FileOutcome> outcomes, Reporter reporter, CancellationToken cancellationToken)
    {
        foreach (var outcome in outcomes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (outcome.Action)
            {
                case FileAction.Write:
                    Write(
                        outcome.SidecarPath,
                        outcome.Text ?? throw new InvalidOperationException("A file to write has no text."),
                        reporter
                    );
                    break;
                case FileAction.Delete:
                    Delete(outcome.SidecarPath, reporter);
                    break;
                case FileAction.HeldBack when outcome.HasDescribed:
                    reporter.Report(NotWritten(outcome));
                    break;
                default:
                    break;
            }
        }
    }

    private static void Write(string path, string text, Reporter reporter)
    {
        // Beside the file, so that the move is within one volume.  The name ends in neither ".sql" nor ".json":
        // nothing takes a file that a stopped run left for a query file or a sidecar.
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            // UTF-8 without a byte order mark, and the line endings of the writer.
            File.WriteAllText(temporary, text);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDelete(temporary);
            reporter.Report(Failure(path, Written, exception));
        }
    }

    private static void Delete(string path, Reporter reporter)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            reporter.Report(Failure(path, Deleted, exception));
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The error of the write is the one to report.
        }
    }

    private static ToolDiagnostic Failure(string path, string tried, Exception exception) =>
        ToolDiagnostic.ForFile(ToolDiagnostics.FileCannotBeChanged, path, path, tried, exception.Message);

    private static ToolDiagnostic NotWritten(FileOutcome outcome) =>
        ToolDiagnostic
            .ForFile(ToolDiagnostics.SidecarNotWritten, outcome.File.Path, outcome.File.Path)
            .WithLines([
                .. outcome.HeldBackBy.Select(static query => new ContinuationLine(
                    "query",
                    $"{query.Name}: {(query.Failed ? "failed" : "not in this run")}"
                )),
                new ContinuationLine("help", "describe the whole file, or fix the queries that failed"),
            ]);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.SidecarStoreTests`
Expected: PASS, 11 tests.

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.

- [ ] **Step 6: Closing steps, and commit**

```bash
git add src tests docs/diagnostics.md
git commit -m "Write a sidecar whole or not at all

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: `describe` on its units: `--force`, the summary and the exit code

**Files:**
- Create in `src/SqlSource.Tool/Describing/`: `DescribeOptions.cs`, `RunSummary.cs`, `DescribeRun.cs`
- Modify: `src/SqlSource.Tool/DescribeCommand.cs`, `src/SqlSource.Tool/AGENTS.md`
- Create: `tests/SqlSource.Tool.Tests/DescribeScene.cs`, `tests/SqlSource.Tool.Tests/DescribeRunTests.cs`, `tests/SqlSource.Tool.Tests/SummaryTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/DescribeTests.cs`, `tests/SqlSource.Tool.Tests/FilterTests.cs`, `tests/SqlSource.Tool.Tests/UsageCheckTests.cs`

**Interfaces:**
- Consumes: `RunDecisions.Decide`, `RunDecisions.SelectedDatabases`, `Connections.Resolve`, `DatabaseRuns.RunAsync`, `FileOutcomes.Decide`, `SidecarStore.Apply`, `ConnectionArgument.TryParse`, `DescribeCommand.PlanAsync`
- Produces: `record DescribeOptions(bool Force, bool HasFilter, ImmutableArray<ConnectionArgument> Connections, ImmutableArray<string> Databases)`
- Produces: `RunSummary.Lines(ImmutableArray<PlannedDatabase> planned, ImmutableArray<PlannedDatabase> selected, ImmutableArray<FileWork> files, Connections connections, ImmutableArray<string> named) : ImmutableArray<string>`
- Produces: `DescribeRun.RunAsync(RunPlan plan, DescribeOptions options, ToolHost host, Reporter reporter, CancellationToken cancellationToken) : Task`
- Produces: the option `--force` of `describe`; `--database` is shown in `--help`
- Produces for the tests: `DescribeScene`, with `Run`, `Describer`, `Project`, `DescribeAsync(params string[] args)`, and the constants `Connection` and `See`

- [ ] **Step 1: Write the scene of a run-level test**

Create `tests/SqlSource.Tool.Tests/DescribeScene.cs`:

```csharp
using System;
using System.Threading.Tasks;

namespace SqlSource.Tool.Tests;

// A run of "describe" from the command line to the disk: one postgres project in the working directory, whose type
// claims the .sql files beside it, and the describer of the tests.  The databases "postgres" and "billing" have a
// connection, each in its own variable, until a test takes one away.
internal sealed class DescribeScene : IDisposable
{
    public const string Connection = "Host=db;Password=hunter2";

    public const string See = "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc";

    public DescribeScene()
    {
        Run.Describers.Add(Describer);
        Project = Plans.Postgres(Run.Folder, directory: "").AnsweredBy(Run.Processes);
        Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = Connection;
        Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = Connection;
    }

    public CliRun Run { get; } = new();

    public FakeDescriber Describer { get; } = new();

    public TestProject Project { get; }

    public Task<CliResult> DescribeAsync(params string[] args) => Run.RunAsync(["describe", .. args]);

    public void Dispose() => Run.Dispose();
}
```

- [ ] **Step 2: Write the failing tests of the run**

Create `tests/SqlSource.Tool.Tests/DescribeRunTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tool.Tests;

// "describe" from the command line to the sidecars on the disk, with the describer of the tests.
public sealed class DescribeRunTests : IDisposable
{
    private const string Users = "-- name: GetUser\nSELECT 1;\n\n-- name: ListUsers\nSELECT 2;\n";

    private const string See = DescribeScene.See;

    private static readonly DateTime Old = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);

    private readonly DescribeScene _scene = new();
    private readonly string _users;

    public DescribeRunTests() => _users = _scene.Project.AddSql("Users.sql", Users);

    private string Sidecar => _users + ".json";

    public void Dispose() => _scene.Dispose();

    [Fact]
    public async Task Run_QueriesWithoutASidecar_AreDescribedAndTheSidecarIsWritten()
    {
        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): 2 described, 0 skipped, 0 failed\n", ""));
        var sidecar = TestSidecar.Read(_users);
        sidecar.FormatVersion.ShouldBe(SidecarFormat.Version);
        sidecar.ToolVersion.ShouldBe(PackageVersion.Prefix);
        sidecar
            .Queries.Select(entry => $"{entry.Name} {entry.Engine} {entry.Database} {entry.ServerVersion}")
            .ShouldBe(["GetUser postgres postgres 16.4", "ListUsers postgres postgres 16.4"]);
        sidecar.Queries.ShouldAllBe(entry => entry.Hash.Length == 64);
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe(DescribeScene.Connection);
        _scene.Describer.Closed.ShouldBe(1);
    }

    [Fact]
    public async Task Run_SecondRunWithNothingChanged_DescribesNothingAndWritesNothing()
    {
        _ = await _scene.DescribeAsync();
        File.SetLastWriteTimeUtc(Sidecar, Old);

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): 0 described, 2 skipped, 0 failed\n", ""));
        _scene.Describer.Described.Count.ShouldBe(2);
        _scene.Describer.Opened.Count.ShouldBe(1);
        File.GetLastWriteTimeUtc(Sidecar).ShouldBe(Old);
    }

    // The SQL, a declaration, a default and the database are each part of what was described.
    [Theory]
    [InlineData("-- name: GetUser\nSELECT 1;\n", "-- name: GetUser\nSELECT 11;\n")]
    [InlineData(
        "-- name: GetUser\n-- param: @id int\nSELECT @id;\n",
        "-- name: GetUser\n-- param: @id bigint\nSELECT @id;\n"
    )]
    [InlineData("-- name: GetUser\nSELECT 1 {{w:WHERE 1 = 1}};\n", "-- name: GetUser\nSELECT 1 {{w:WHERE 2 = 2}};\n")]
    [InlineData("-- name: GetUser\nSELECT 1;\n", "-- name: GetUser\n-- database: billing\nSELECT 1;\n")]
    public async Task Run_QueryThatChanged_IsDescribedAgain(string before, string after)
    {
        File.WriteAllText(_users, before);
        (await _scene.DescribeAsync()).Out.ShouldEndWith(": 1 described, 0 skipped, 0 failed\n");
        File.WriteAllText(_users, after);

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldEndWith(": 1 described, 0 skipped, 0 failed\n");
        _scene.Describer.Described.Count.ShouldBe(2);
    }

    // A comment changes no type, and is not part of the hash.
    [Fact]
    public async Task Run_QueryWhoseCommentChanged_IsSkipped()
    {
        _ = await _scene.DescribeAsync();
        File.WriteAllText(_users, Users.Replace("SELECT 1;", "/* the first */\nSELECT 1;", StringComparison.Ordinal));

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): 0 described, 2 skipped, 0 failed\n", ""));
    }

    [Theory]
    [InlineData("another tool version")]
    [InlineData("a lower format version")]
    [InlineData("not a sidecar")]
    public async Task Run_SidecarThatIsNotThisTools_IsWrittenAgainWithEveryQueryDescribed(string kind)
    {
        _ = await _scene.DescribeAsync();
        var written = File.ReadAllText(Sidecar);
        var sidecar = TestSidecar.Read(_users);
        File.WriteAllText(
            Sidecar,
            kind switch
            {
                "another tool version" => SidecarWriter.Write(sidecar with { ToolVersion = "0.0.1" }),
                "a lower format version" => SidecarWriter.Write(sidecar with { FormatVersion = 0 }),
                _ => "<<<<<<< HEAD\n" + written,
            }
        );

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): 2 described, 0 skipped, 0 failed\n", ""));
        File.ReadAllText(Sidecar).ShouldBe(written);
    }

    [Fact]
    public async Task Run_SidecarOfAHigherFormatVersion_IsSqlsrc221AndTheFileIsLeftAlone()
    {
        _ = await _scene.DescribeAsync();
        var newer = SidecarWriter.Write(TestSidecar.Read(_users) with { FormatVersion = 2 });
        File.WriteAllText(Sidecar, newer);

        var result = await _scene.DescribeAsync("--force");

        result.ShouldBe(
            new CliResult(
                1,
                "postgres (postgres): 0 described, 0 skipped, 2 failed\n",
                $"{Sidecar} : error SQLSRC221: '{Sidecar}' has format 2, and this tool writes format 1\n"
                    + "    help: update the SqlSource.Tool package\n"
                    + $"{See}221\n"
            )
        );
        File.ReadAllText(Sidecar).ShouldBe(newer);
        _scene.Describer.Described.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Run_Force_DescribesWhatIsCurrent()
    {
        _ = await _scene.DescribeAsync();
        _scene.Describer.Server = _scene.Describer.Server with { Version = "17.0" };

        var result = await _scene.DescribeAsync("--force");

        result.ShouldBe(new CliResult(0, "postgres (postgres): 2 described, 0 skipped, 0 failed\n", ""));
        TestSidecar.Read(_users).Queries.ShouldAllBe(entry => entry.ServerVersion == "17.0");
    }

    [Fact]
    public async Task Run_OneQueryFails_TheOthersAreDescribedAndTheFileIsNotWritten()
    {
        _scene.Describer.Failures["ListUsers"] = FakeDescriber.Failure("ListUsers", "42601: syntax error");

        var result = await _scene.DescribeAsync();

        result.ShouldBe(
            new CliResult(
                1,
                "postgres (postgres): 1 described, 0 skipped, 1 failed\n",
                $"{_users}(4,10): error SQLSRC999: The server rejected the query 'ListUsers'\n"
                    + "    query: ListUsers, database postgres, postgres 16.4, FakeDriver 1.2.3\n"
                    + "    step: describe columns\n"
                    + "    server: 42601: syntax error\n"
                    + "    help: check the SQL\n"
                    + "    see: https://example.test/sqlsrc999\n"
                    + $"{_users} : error SQLSRC217: The sidecar of '{_users}' was not written, because not every "
                    + "query of the file has an entry\n"
                    + "    query: ListUsers: failed\n"
                    + "    help: describe the whole file, or fix the queries that failed\n"
                    + $"{See}217\n"
            )
        );
        File.Exists(Sidecar).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_SessionCannotBeOpened_IsReportedOnceForTheDatabase()
    {
        _scene.Describer.OpenFailure = FakeDescriber.Failure("(open)", "28P01: password authentication failed");

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBe("postgres (postgres): 0 described, 0 skipped, 2 failed\n");
        result.Error.Split("error SQLSRC").Length.ShouldBe(2);
        result.Error.ShouldStartWith($"{_users}(1,10): error SQLSRC999: ");
        File.Exists(Sidecar).ShouldBeFalse();
    }

    // A developer who has one of a project's two databases is not stopped by the other.
    [Fact]
    public async Task Run_NoConnectionAndEveryQueryCurrent_IsNoErrorAndTheSummarySaysSo()
    {
        _ = await _scene.DescribeAsync();
        _scene.Run.Environment.Clear();

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): no connection, 2 skipped\n", ""));
    }

    [Fact]
    public async Task Run_NoConnectionAndOneQueryStale_IsSqlsrc213Once()
    {
        _ = await _scene.DescribeAsync();
        File.WriteAllText(_users, Users.Replace("SELECT 2;", "SELECT 22;", StringComparison.Ordinal));
        _scene.Run.Environment.Clear();

        var result = await _scene.DescribeAsync();

        result.ShouldBe(
            new CliResult(
                1,
                "postgres (postgres): 0 described, 1 skipped, 1 failed\n",
                $"{_users}(4,10): error SQLSRC213: No connection is given for the database 'postgres'\n"
                    + "    help: pass --connection postgres=<connection string>, or set SQLSOURCE_CONNECTION_POSTGRES\n"
                    + $"{See}213\n"
            )
        );
    }

    // Under "--force" every query is described, so a database of the run always needs its connection.
    [Fact]
    public async Task Run_NoConnectionUnderForce_IsSqlsrc213ThoughEveryQueryIsCurrent()
    {
        _ = await _scene.DescribeAsync();
        _scene.Run.Environment.Clear();

        var result = await _scene.DescribeAsync("--force");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith($"{_users}(1,10): error SQLSRC213: ");
        result.Out.ShouldBe("postgres (postgres): 0 described, 0 skipped, 2 failed\n");
    }

    [Fact]
    public async Task Run_ConnectionOnTheCommandLine_IsTheOneTheDescriberIsGiven()
    {
        var result = await _scene.DescribeAsync("--connection", "POSTGRES=Host=other;Port=5433");

        result.ExitCode.ShouldBe(0);
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe("Host=other;Port=5433");
    }

    // The plan reported the file.  Its queries count as failed and get no error of this sub-phase.
    [Fact]
    public async Task Run_FileThatIsNotReady_FailsItsQueriesWithThePlansErrorAlone()
    {
        _scene.Project.Properties["SqlSourceDialect"] = "ansi";

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBe("ansi (ansi): 0 described, 0 skipped, 2 failed\n");
        result.Error.Split("error SQLSRC").Length.ShouldBe(2);
        result.Error.ShouldContain("error SQLSRC209: ");
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    // The released tool: no describer is registered until phase 3.
    [Fact]
    public async Task Run_NoDescriberRegistered_IsSqlsrc216AtEachQueryAndNothingIsWritten()
    {
        _scene.Run.Describers.Clear();

        var result = await _scene.DescribeAsync();

        result.ShouldBe(
            new CliResult(
                1,
                "postgres (postgres): 0 described, 0 skipped, 2 failed\n",
                $"{_users}(1,10): error SQLSRC216: This version of sqlsource cannot describe 'postgres'\n{See}216\n"
                    + $"{_users}(4,10): error SQLSRC216: This version of sqlsource cannot describe 'postgres'\n{See}216\n"
            )
        );
        File.Exists(Sidecar).ShouldBeFalse();
    }

    // Review focus 3.
    [Fact]
    public async Task Run_CancelledWhileAQueryIsDescribed_WritesNothingAndPrintsNothing()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _scene.Describer.BeforeDescribe = _ => cancel.CancelAsync();

        var result = await _scene.Run.RunAsync(cancel.Token, "describe");

        result.ShouldBe(new CliResult(1, "", ""));
        File.Exists(Sidecar).ShouldBeFalse();
        Directory.EnumerateFiles(_scene.Run.Folder.Path, "*.tmp").ShouldBeEmpty();
    }

    // Review focus 5.
    [Fact]
    public async Task Run_NoConnectionAndOnlyAQueryThatFailedInThePlan_IsNoSqlsrc213AndTheSummaryCountsIt()
    {
        _ = await _scene.DescribeAsync();
        _ = _scene.Project.AddSql("Tokens.sql", "-- name: Find\nSELECT 1 {{where}};\n");
        _scene.Run.Environment.Clear();

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC210: ");
        result.Error.ShouldNotContain("SQLSRC213");
        result.Out.ShouldBe("postgres (postgres): 0 described, 2 skipped, 1 failed\n");
    }

    [Fact]
    public async Task Run_DescriberThatThrows_IsSqlsrc200WithoutTheConnectionAndNothingIsWritten()
    {
        _scene.Describer.Exceptions["ListUsers"] = new InvalidOperationException($"bad {DescribeScene.Connection}");

        var result = await _scene.DescribeAsync();

        result.ShouldBe(
            new CliResult(
                1,
                "",
                "sqlsource : error SQLSRC200: sqlsource failed unexpectedly: System.InvalidOperationException: bad ***\n"
                    + $"{See}200\n"
            )
        );
        File.Exists(Sidecar).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_QueryThatWasDeleted_LosesItsEntry()
    {
        _ = await _scene.DescribeAsync();
        File.WriteAllText(_users, "-- name: GetUser\nSELECT 1;\n");

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): 0 described, 1 skipped, 0 failed\n", ""));
        TestSidecar.Read(_users).Queries.ShouldHaveSingleItem().Name.ShouldBe("GetUser");
    }

    [Fact]
    public async Task Run_FileThatNeedsNoEntryAnyMore_LosesItsSidecar()
    {
        _ = await _scene.DescribeAsync();
        File.WriteAllText(_users, "-- output: sql\n" + Users);

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "", ""));
        File.Exists(Sidecar).ShouldBeFalse();
    }
}
```

Create `tests/SqlSource.Tool.Tests/SummaryTests.cs`:

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

// The lines that end a run: one for each selected database, and one for each name of "--database".
public sealed class SummaryTests : IDisposable
{
    private const string Users = "-- name: GetUser\nSELECT 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n";

    private readonly DescribeScene _scene = new();
    private readonly string _users;

    public SummaryTests()
    {
        // "Invoices.sql" comes before "Users.sql", so the plan holds "billing" first.
        _ = _scene.Project.AddSql("Invoices.sql", "-- name: ListInvoices\n-- database: billing\nSELECT 3;\n");
        _users = _scene.Project.AddSql("Users.sql", Users);
    }

    public void Dispose() => _scene.Dispose();

    [Fact]
    public async Task Run_TwoDatabases_HaveALineEachInThePlansOrder()
    {
        var result = await _scene.DescribeAsync();

        result.ShouldBe(
            new CliResult(
                0,
                "billing (postgres): 2 described, 0 skipped, 0 failed\n"
                    + "postgres (postgres): 1 described, 0 skipped, 0 failed\n",
                ""
            )
        );
    }

    [Fact]
    public async Task Run_DescribedSkippedAndFailed_AreEachCounted()
    {
        _ = await _scene.DescribeAsync();
        File.WriteAllText(_users, Users.Replace("SELECT 2;", "SELECT 22;", StringComparison.Ordinal) + "\n-- name: Bad\nSELECT 4;\n");
        _scene.Describer.Failures["Bad"] = FakeDescriber.Failure("Bad");

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBe(
            "billing (postgres): 1 described, 1 skipped, 0 failed\n"
                + "postgres (postgres): 0 described, 1 skipped, 1 failed\n"
        );
    }

    [Fact]
    public async Task Run_DatabaseWithoutAConnectionAndNothingToDescribe_SaysNoConnection()
    {
        _ = await _scene.DescribeAsync();
        _ = _scene.Run.Environment.Remove("SQLSOURCE_CONNECTION_BILLING");

        var result = await _scene.DescribeAsync();

        result.ShouldBe(
            new CliResult(
                0,
                "billing (postgres): no connection, 2 skipped\npostgres (postgres): 0 described, 1 skipped, 0 failed\n",
                ""
            )
        );
    }

    // The "no connection" form has no place for a failure, so a database with one has the three counts.
    [Fact]
    public async Task Run_DatabaseWithoutAConnectionAndAFailedQuery_HasTheThreeCounts()
    {
        _ = await _scene.DescribeAsync();
        _ = _scene.Project.AddSql("Tokens.sql", "-- name: Find\n-- database: billing\nSELECT 1 {{where}};\n");
        _ = _scene.Run.Environment.Remove("SQLSOURCE_CONNECTION_BILLING");

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBe(
            "billing (postgres): 0 described, 2 skipped, 1 failed\n"
                + "postgres (postgres): 0 described, 1 skipped, 0 failed\n"
        );
    }

    [Fact]
    public async Task Run_Database_CountsOnlyTheSelectedQueriesAndSpellsTheNameAsThePlanDoes()
    {
        var result = await _scene.DescribeAsync("--database", "BILLING");

        result.Out.ShouldBe("billing (postgres): 2 described, 0 skipped, 0 failed\n");
        // "Users.sql" was held back by GetUser, which is not in the run and has no entry.
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("    query: GetUser: not in this run\n");
    }

    // A filter that matched nothing is visible.
    [Fact]
    public async Task Run_DatabaseNameThatNoQueryHas_HasThreeZerosAndNoEngine()
    {
        var result = await _scene.DescribeAsync("--database", "postgres", "--database", "nowhere");

        result.ShouldBe(
            new CliResult(
                1,
                "postgres (postgres): 1 described, 0 skipped, 0 failed\nnowhere: 0 described, 0 skipped, 0 failed\n",
                result.Error
            )
        );
        result.Error.ShouldContain("    query: GetInvoice: not in this run\n");
    }

    [Fact]
    public async Task Run_DatabaseOfThePlanThatNoSelectedQueryHas_HasThreeZerosAndItsEngine()
    {
        var result = await _scene.DescribeAsync("Invoices.sql", "--database", "Postgres");

        result.ShouldBe(new CliResult(0, "postgres (postgres): 0 described, 0 skipped, 0 failed\n", ""));
        _scene.Describer.Opened.ShouldBeEmpty();
    }
}
```

- [ ] **Step 3: Change the tests that the new behaviour makes wrong**

In `tests/SqlSource.Tool.Tests/DescribeTests.cs`, replace `Run_ProjectWhosePlanHasNoError_PrintsNothing` with:

```csharp
    // The released tool has no describer, so a query that must be described is SQLSRC216.
    [Fact]
    public async Task Run_ProjectWithAQueryToDescribe_IsSqlsrc216AndASummaryLine()
    {
        using var run = new CliRun();
        var project = new TestProject(run.Folder).AnsweredBy(run.Processes);
        project.Properties["SqlSourceDialect"] = "postgres";
        _ = project.AddSource("Queries.cs", Queries);
        var sql = project.AddSql("One.sql", "-- name: One\nSELECT 1;\n");

        var result = await run.RunAsync("describe", project.ProjectPath);

        result.ShouldBe(
            new CliResult(
                1,
                "postgres (postgres): 0 described, 0 skipped, 1 failed\n",
                $"{sql}(1,10): error SQLSRC216: This version of sqlsource cannot describe 'postgres'\n"
                    + "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc216\n"
            )
        );
    }

    [Fact]
    public async Task Run_ProjectWhoseQueriesNeedNoEntry_PrintsNothing()
    {
        using var run = new CliRun();
        var project = new TestProject(run.Folder).AnsweredBy(run.Processes);
        project.Properties["SqlSourceDialect"] = "postgres";
        project.Properties["SqlSourceOutput"] = "sql";
        _ = project.AddSource("Queries.cs", Queries);
        _ = project.AddSql("One.sql", "-- name: One\nSELECT 1;\n");

        var result = await run.RunAsync("describe", project.ProjectPath);

        result.ShouldBe(new CliResult(0, "", ""));
    }
```

In `tests/SqlSource.Tool.Tests/FilterTests.cs`, replace `Run_Help_ShowsThePathsAndNotTheDatabaseOption` with:

```csharp
    [Fact]
    public async Task Run_Help_ShowsThePathsAndTheOptionsOfDescribe()
    {
        var result = await _run.RunAsync("describe", "--help");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldContain("sqlsource describe [<path>...]");
        result.Out.ShouldContain(".sql");
        result.Out.ShouldContain("--database <name>");
        result.Out.ShouldContain("--connection <name=value>");
        result.Out.ShouldContain("--force");
    }
```

In `tests/SqlSource.Tool.Tests/UsageCheckTests.cs`, in `Check_EveryAcceptedCommandLineOfTheTool_IsReadTheSameBySystemCommandLine`, add `"--force",` and `"--force=V",` to `vocabulary` after `"N=V",`.

Any other test that runs the whole command over a project with a query that needs an entry, and expects it to print nothing, changes as the first of these did: the released tool now reports `SQLSRC216` for the query and ends with a summary line.  The full run of step 8 finds them.

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.DescribeRunTests`
Expected: FAIL.  `Run_QueriesWithoutASidecar_AreDescribedAndTheSidecarIsWritten` fails with an empty `Out`: the command builds the plan and stops.

- [ ] **Step 5: Write `DescribeOptions`, `RunSummary` and `DescribeRun`**

Create `src/SqlSource.Tool/Describing/DescribeOptions.cs`:

```csharp
using System.Collections.Immutable;

namespace SqlSource.Tool.Describing;

/// <summary>
/// What the command line says of a run, beside what the plan already holds.
/// </summary>
/// <param name="Force">Whether a selected query is described though its entry is current.</param>
/// <param name="HasFilter">
/// Whether the run has <c>--project</c>, <c>--database</c> or a <c>.sql</c> path.  The plan's filters do not hold
/// <c>--project</c>, and a sidecar is deleted only in a run with none of the three.
/// </param>
/// <param name="Connections">The values of <c>--connection</c>.</param>
/// <param name="Databases">The names of <c>--database</c>, each once ignoring case, in the order given.</param>
internal sealed record DescribeOptions(
    bool Force,
    bool HasFilter,
    ImmutableArray<ConnectionArgument> Connections,
    ImmutableArray<string> Databases
);
```

Create `src/SqlSource.Tool/Describing/RunSummary.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using SqlSource.Parsing;
using SqlSource.Tool.Planning;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Step 5 of a run: the lines that end it, so that a filter that matched nothing is seen.
/// </summary>
internal static class RunSummary
{
    private const string Zeros = "0 described, 0 skipped, 0 failed";

    /// <summary>
    /// A line for each selected database, in the plan's order, then one for each name of <c>--database</c> that
    /// is no selected database, in the order given.  The counts are of selected queries that need an entry.
    /// </summary>
    /// <param name="planned">The databases of the plan.</param>
    /// <param name="selected">The selected ones of them.</param>
    /// <param name="files">The files, with every query described, kept, failed or left out.</param>
    /// <param name="connections">The connections of the selected databases.</param>
    /// <param name="named">The names of <c>--database</c>.</param>
    public static ImmutableArray<string> Lines(
        ImmutableArray<PlannedDatabase> planned,
        ImmutableArray<PlannedDatabase> selected,
        ImmutableArray<FileWork> files,
        Connections connections,
        ImmutableArray<string> named
    )
    {
        var lines = ImmutableArray.CreateBuilder<string>();
        foreach (var database in selected)
        {
            var (described, skipped, failed) = Count(files, database.Name);
            // The second form has no place for a failure: it is for a database with nothing to describe.
            lines.Add(
                connections.For(database.Name).Value is null && failed == 0
                    ? string.Create(CultureInfo.InvariantCulture, $"{Label(database)}: no connection, {skipped} skipped")
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $"{Label(database)}: {described} described, {skipped} skipped, {failed} failed"
                    )
            );
        }

        foreach (var name in named.Where(name => !selected.Any(database => Same(database.Name, name))))
        {
            // With its engine when the plan knows the database, though no query of it is selected.
            lines.Add(
                planned.FirstOrDefault(database => Same(database.Name, name)) is { } known
                    ? $"{Label(known)}: {Zeros}"
                    : $"{name}: {Zeros}"
            );
        }

        return lines.ToImmutable();
    }

    private static string Label(PlannedDatabase database) =>
        $"{database.Name} ({SqlDialectName.Canonical(database.Dialect)})";

    private static (int Described, int Skipped, int Failed) Count(ImmutableArray<FileWork> files, string database)
    {
        var (described, skipped, failed) = (0, 0, 0);
        foreach (var query in Selected(files, database))
        {
            switch (query.State)
            {
                case QueryState.Described:
                    described++;
                    break;
                case QueryState.Skipped:
                    skipped++;
                    break;
                default:
                    failed++;
                    break;
            }
        }

        return (described, skipped, failed);
    }

    // A selected query that needs an entry was described, was skipped, or failed.
    private static IEnumerable<QueryWork> Selected(ImmutableArray<FileWork> files, string database) =>
        files
            .SelectMany(static file => file.Queries)
            .Where(query =>
                query.Planned is { NeedsEntry: true, IsSelected: true, Database: { } name } && Same(name, database)
            );

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
```

Create `src/SqlSource.Tool/Describing/DescribeRun.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// A run of <c>describe</c> after its plan: the steps of the spec, in order, each a unit of its own.
/// </summary>
/// <remarks>
/// The step that gives each file's outcome, <see cref="FileOutcomes" />, and the one that applies the outcomes to
/// the disk, <see cref="SidecarStore" />, stay apart: sub-phase 2.6 compares where this one writes.
/// </remarks>
internal static class DescribeRun
{
    public static async Task RunAsync(
        RunPlan plan,
        DescribeOptions options,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        // 1 and 2: what is there, and what each query needs.
        var errors = new List<ToolDiagnostic>();
        var files = RunDecisions.Decide(plan, options.Force, errors);
        foreach (var error in errors)
        {
            reporter.Report(error);
        }

        // 3: the connection of every selected database is looked up, since the summary says which have none.
        var selected = RunDecisions.SelectedDatabases(plan);
        var connections = Connections.Resolve(
            [.. selected.Select(static database => database.Name)],
            options.Connections,
            host.GetEnvironmentVariable
        );
        await DatabaseRuns.RunAsync(selected, files, connections, host, reporter, cancellationToken);

        // 4: a run that was cancelled changes no file.
        cancellationToken.ThrowIfCancellationRequested();
        var outcomes = FileOutcomes.Decide(files, options.HasFilter, File.Exists);
        SidecarStore.Apply(outcomes, reporter, cancellationToken);

        // 5.  The exit code is the reporter's: 1 when anything was reported.
        var lines = RunSummary.Lines([.. plan.Databases], selected, files, connections, options.Databases);
        foreach (var line in lines)
        {
            await host.Out.WriteLineAsync(line);
        }
    }
}
```

- [ ] **Step 6: Put the command on the run**

In `src/SqlSource.Tool/DescribeCommand.cs`:

Replace the summary of the class with:

```csharp
/// <summary>
/// The <c>describe</c> command.  It finds the unit and the projects, builds the plan of the run and reports what is
/// wrong with it, and hands the plan to <see cref="DescribeRun" />, which describes and writes.
/// </summary>
```

Add `using System.Collections.Immutable;` and the constant `private const string ForceOption = "--force";`.

In `Create`: remove `Hidden = true,` from the `database` option and replace the comment above it with `// It selects queries: what is described, what is written, and what the summary counts.`; add the option

```csharp
        var force = new Option<bool>(ForceOption)
        {
            Description =
                "Describe every query of the run again, whether or not its entry is current.  The command to run "
                + "after a change to the schema.",
            Arity = ArgumentArity.Zero,
        };
```

and make the command and its action:

```csharp
        var describe = new Command(Name, "Asks the databases of the projects to describe their queries")
        {
            path,
            project,
            database,
            connection,
            force,
        };
        describe.SetAction(
            async (parsed, cancellationToken) =>
            {
                // An error is in the reporter, where the exit code is taken.
                if (await PlanAsync(parsed, host, reporter, cancellationToken) is { } plan)
                {
                    await DescribeRun.RunAsync(plan, OptionsOf(parsed), host, reporter, cancellationToken);
                }

                return 0;
            }
        );
        return describe;
```

Add the method after `CheckUsage`:

```csharp
    // What the command line says of the run, beside its plan.  CheckUsage has held each --connection to its form.
    private static DescribeOptions OptionsOf(ParseResult parsed)
    {
        var paths = parsed.GetValue<string[]>(PathArgument) ?? [];
        var projects = parsed.GetValue<string[]>(ProjectOption) ?? [];
        var databases = parsed.GetValue<string[]>(DatabaseOption) ?? [];
        var connections = ImmutableArray.CreateBuilder<ConnectionArgument>();
        foreach (var text in parsed.GetValue<string[]>(ConnectionOption) ?? [])
        {
            connections.Add(
                ConnectionArgument.TryParse(text, out var connection)
                    ? connection
                    : throw new InvalidOperationException("A --connection that is no name and value was let through.")
            );
        }

        return new DescribeOptions(
            parsed.GetValue<bool>(ForceOption),
            projects.Length > 0 || databases.Length > 0 || paths.Any(SqlPath.IsSqlFile),
            connections.ToImmutable(),
            [.. databases.Distinct(StringComparer.OrdinalIgnoreCase)]
        );
    }
```

In the `<remarks>` of `PlanAsync`, replace `and sub-phase 2.5 goes on from it` with `and the run goes on from it`.

- [ ] **Step 7: Mend the two sentences of the tool's `AGENTS.md` that are now wrong**

In `src/SqlSource.Tool/AGENTS.md`:

In the bullet that starts `- **The tool never restores and never builds a project, and writes nothing into one.**`, replace that bold sentence with `**The tool never restores and never builds a project, and writes nothing into one but its sidecars.**`, and add at the end of the bullet: `  A sidecar is written or deleted by `Describing/SidecarStore.cs` and by nothing else.`

In the bullet that starts `- **An option is declared so that `UsageCheck` reads it right.**`, replace its last sentence, from `An option is added to` to the end, with:

```markdown
An option is added to `--help` in the sub-phase that gives it an effect, not before: one that is read and checked earlier is `Hidden` until then, as `--database` was in sub-phase 2.4.
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.DescribeRunTests`
Expected: PASS, 25 tests.

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.SummaryTests`
Expected: PASS, 7 tests.

If `Run_QueryWhoseCommentChanged_IsSkipped` describes the query again, the comment the test adds left white space in the stripped SQL, and the hash is right to differ: put the comment where the parser strips it whole, as `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs` does for the same claim.  Change the test's edit, not the hash.

Run: `dotnet test --solution SqlSource.slnx`
Expected: PASS.

- [ ] **Step 9: Closing steps, and commit**

`tools/check-tool-install.sh`, which the validation runs, calls `describe` in an empty folder and expects `SQLSRC201`: that is unchanged.

```bash
git add src/SqlSource.Tool tests/SqlSource.Tool.Tests
git commit -m "Describe, write the sidecars and summarise the run

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Filters and secrets held by tests, and a second Ctrl+C

**Files:**
- Create: `tests/SqlSource.Tool.Tests/FilterRunTests.cs`, `tests/SqlSource.Tool.Tests/SecretTests.cs`
- Modify: `src/SqlSource.Tool/Cli.cs`, `tests/SqlSource.Tool.Tests/CliTests.cs`
- Modify: `docs/tech-debt/TD-0024-gaps-of-the-tools-shell.md`, `docs/tech-debt/README.md`

**Interfaces:**
- Consumes: `DescribeScene`, `TestSidecar`, `FakeDescriber`, `CliRun`
- Produces: `Cli.Interrupt(CancellationTokenSource interrupt) : bool`

These two classes hold the tool to two of the spec's four measures of success: a filter narrows what a run changes, and no value of a connection is ever printed.  Task 9 built the behaviour, so most of these tests pass as they are written.  A test that fails has found a defect in an earlier task: mend the code there, not the test.

- [ ] **Step 1: Write the tests of the filters**

Create `tests/SqlSource.Tool.Tests/FilterRunTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tool.Tests;

// A filter narrows what a run changes as well as what it describes: a run for one database, one file or one
// project leaves every other sidecar as it was.
public sealed class FilterRunTests : IDisposable
{
    private const string Users = "-- name: GetUser\nSELECT 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n";

    private static readonly DateTime Old = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);

    private readonly DescribeScene _scene = new();
    private readonly string _invoices;
    private readonly string _plain;
    private readonly string _users;

    public FilterRunTests()
    {
        _invoices = _scene.Project.AddSql("Invoices.sql", "-- name: ListInvoices\n-- database: billing\nSELECT 3;\n");
        _plain = _scene.Project.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 4;\n");
        _users = _scene.Project.AddSql("Users.sql", Users);
    }

    public void Dispose() => _scene.Dispose();

    private static string HashOf(string sqlPath, string query) =>
        TestSidecar.Read(sqlPath).Find(query).ShouldNotBeNull().Hash;

    [Fact]
    public async Task Run_DatabaseWithTheOthersEntriesCurrent_WritesTheMixedFile()
    {
        _ = await _scene.DescribeAsync();
        var getUser = HashOf(_users, "GetUser");
        var getInvoice = HashOf(_users, "GetInvoice");
        File.WriteAllText(_users, Users.Replace("SELECT 2;", "SELECT 22;", StringComparison.Ordinal));

        var result = await _scene.DescribeAsync("--database", "billing");

        result.ShouldBe(new CliResult(0, "billing (postgres): 1 described, 1 skipped, 0 failed\n", ""));
        HashOf(_users, "GetUser").ShouldBe(getUser);
        HashOf(_users, "GetInvoice").ShouldNotBe(getInvoice);
    }

    [Fact]
    public async Task Run_DatabaseWithOneOfTheOthersEntriesStale_DoesNotWriteAndSaysWhichQueryIsNotInTheRun()
    {
        _ = await _scene.DescribeAsync();
        var before = File.ReadAllBytes(_users + ".json");
        File.WriteAllText(
            _users,
            Users.Replace("SELECT 1;", "SELECT 11;", StringComparison.Ordinal)
                .Replace("SELECT 2;", "SELECT 22;", StringComparison.Ordinal)
        );

        var result = await _scene.DescribeAsync("--database", "billing");

        result.ShouldBe(
            new CliResult(
                1,
                "billing (postgres): 1 described, 1 skipped, 0 failed\n",
                $"{_users} : error SQLSRC217: The sidecar of '{_users}' was not written, because not every query of "
                    + "the file has an entry\n"
                    + "    query: GetUser: not in this run\n"
                    + "    help: describe the whole file, or fix the queries that failed\n"
                    + $"{DescribeScene.See}217\n"
            )
        );
        File.ReadAllBytes(_users + ".json").ShouldBe(before);
    }

    // A sidecar of a newer tool is an error where it is read.  This one is not read.
    [Fact]
    public async Task Run_FileWhoseQueriesAllBelongToAnotherDatabase_IsNotReadWrittenOrDeleted()
    {
        var newer = SidecarWriter.Write(new Sidecar(2, "9.9.9", EquatableArray<SidecarEntry>.Empty));
        File.WriteAllText(_invoices + ".json", newer);
        File.SetLastWriteTimeUtc(_invoices + ".json", Old);

        var result = await _scene.DescribeAsync("--database", "postgres", "--force");

        result.Error.ShouldNotContain("SQLSRC221");
        File.ReadAllText(_invoices + ".json").ShouldBe(newer);
        File.GetLastWriteTimeUtc(_invoices + ".json").ShouldBe(Old);
    }

    [Theory]
    [InlineData("App.csproj", "--database", "postgres")]
    [InlineData("App.csproj", "Users.sql")]
    [InlineData("App.slnx", "--project", "App.csproj")]
    public async Task Run_FileThatNeedsNoEntry_KeepsItsSidecarUnderEachFilter(params string[] args)
    {
        // Two units stand in the working directory, so each run names its own.
        _ = _scene.Run.Folder.WriteFile("App.slnx", "<Solution><Project Path=\"App.csproj\" /></Solution>");
        File.WriteAllText(_plain + ".json", "of a query that needed an entry once");

        var result = await _scene.DescribeAsync(args);

        result.Error.ShouldNotContain("SQLSRC218");
        File.ReadAllText(_plain + ".json").ShouldBe("of a query that needed an entry once");
    }

    [Fact]
    public async Task Run_FileThatNeedsNoEntry_LosesItsSidecarInARunWithNoFilter()
    {
        File.WriteAllText(_plain + ".json", "of a query that needed an entry once");

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(0);
        File.Exists(_plain + ".json").ShouldBeFalse();
    }

    [Fact]
    public async Task Run_SqlPath_DescribesAndWritesThatFileAlone()
    {
        var result = await _scene.DescribeAsync("Users.sql");

        result.ShouldBe(
            new CliResult(
                0,
                "billing (postgres): 1 described, 0 skipped, 0 failed\n"
                    + "postgres (postgres): 1 described, 0 skipped, 0 failed\n",
                ""
            )
        );
        _scene.Describer.Described.Select(request => request.Name).ShouldBe(["GetInvoice", "GetUser"]);
        File.Exists(_users + ".json").ShouldBeTrue();
        File.Exists(_invoices + ".json").ShouldBeFalse();
    }

    [Fact]
    public async Task Run_Project_DescribesAndWritesTheFilesOfThatProjectAlone()
    {
        using var run = new CliRun();
        var describer = new FakeDescriber();
        run.Describers.Add(describer);
        run.Environment["SQLSOURCE_CONNECTION"] = DescribeScene.Connection;
        var a = Plans.Postgres(run.Folder, "A").AnsweredBy(run.Processes);
        var b = Plans.Postgres(run.Folder, "B").AnsweredBy(run.Processes);
        var inA = a.AddSql("A.sql", "-- name: InA\nSELECT 1;\n");
        var inB = b.AddSql("B.sql", "-- name: InB\nSELECT 2;\n");
        var plainOfA = a.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 3;\n");
        File.WriteAllText(plainOfA + ".json", "kept");
        _ = run.Folder.WriteFile(
            "App.slnx",
            "<Solution><Project Path=\"A/A.csproj\" /><Project Path=\"B/B.csproj\" /></Solution>"
        );

        var result = await run.RunAsync("describe", "--project", a.ProjectPath);

        result.ShouldBe(new CliResult(0, "postgres (postgres): 1 described, 0 skipped, 0 failed\n", ""));
        describer.Described.ShouldHaveSingleItem().Name.ShouldBe("InA");
        File.Exists(inA + ".json").ShouldBeTrue();
        File.Exists(inB + ".json").ShouldBeFalse();
        File.ReadAllText(plainOfA + ".json").ShouldBe("kept");
    }
}
```

- [ ] **Step 2: Write the tests of the secret**

Create `tests/SqlSource.Tool.Tests/SecretTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using Xunit;

namespace SqlSource.Tool.Tests;

// No value of a connection is ever printed: not by an error of this sub-phase, not for a command line that is
// wrong, and not when a describer breaks its own rule.
public sealed class SecretTests : IDisposable
{
    private const string Marker = "s3cret-marker";

    private const string Value = "Host=db;Password=" + Marker;

    private readonly DescribeScene _scene = new();
    private readonly string _users;

    public SecretTests()
    {
        _scene.Run.Environment.Clear();
        _users = _scene.Project.AddSql("Users.sql", "-- name: Bad\nSELECT 1;\n\n-- name: Good\nSELECT 2;\n");
    }

    public void Dispose() => _scene.Dispose();

    // A project with an error of every kind of this sub-phase but SQLSRC215, when "postgres" has a connection:
    // a failure of the describer and SQLSRC217 in Users.sql, SQLSRC213 for "reports", SQLSRC214 for the two "app"
    // databases, SQLSRC216 for "mssql", SQLSRC218 for a sidecar that cannot be written, and SQLSRC221.
    private void AddEveryError()
    {
        _scene.Describer.Failures["Bad"] = new DescribeFailure(
            FakeDescriber.Rejected,
            new EquatableArray<string>(["Bad"]),
            DescribeStep.Explain,
            new EquatableArray<string>(["could not explain"]),
            "check the SQL"
        );
        _ = _scene.Project.AddSql("Reports.sql", "-- name: Report\n-- database: reports\nSELECT 3;\n");
        _ = _scene.Project.AddSql(
            "Shared.sql",
            "-- name: One\n-- database: app-v2\nSELECT 4;\n\n-- name: Two\n-- database: app_v2\nSELECT 5;\n"
        );
        _ = _scene.Project.AddSql("Other.sql", "-- dialect: mssql\n-- name: Other\nSELECT 6;\n");
        var locked = _scene.Project.AddSql("Locked.sql", "-- name: Locked\nSELECT 7;\n");
        _ = Directory.CreateDirectory(locked + ".json");
        var newer = _scene.Project.AddSql("Newer.sql", "-- name: Newer\nSELECT 8;\n");
        File.WriteAllText(
            newer + ".json",
            SidecarWriter.Write(new Sidecar(2, "9.9.9", EquatableArray<SidecarEntry>.Empty))
        );
    }

    private static void ShouldHold(CliResult result, string ids)
    {
        result.ExitCode.ShouldBe(1);
        result.Out.ShouldNotContain(Marker);
        result.Error.ShouldNotContain(Marker);
        foreach (var id in ids.Split(','))
        {
            result.Error.ShouldContain($"error SQLSRC{id}: ");
        }
    }

    [Theory]
    [InlineData("--connection", "postgres=" + Value)]
    [InlineData("--connection=postgres=" + Value)]
    [InlineData("--connection:postgres=" + Value)]
    public async Task Run_ValueOnTheCommandLineAndEveryError_IsNeverPrinted(params string[] args)
    {
        AddEveryError();

        var result = await _scene.DescribeAsync(args);

        ShouldHold(result, "213,214,216,217,218,221,999");
        _scene.Describer.Opened.ShouldAllBe(request => request.Connection == Value);
    }

    [Fact]
    public async Task Run_ValueInTheVariableOfTheNameAndEveryError_IsNeverPrinted()
    {
        AddEveryError();
        _scene.Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = Value;
        _scene.Run.Environment["SQLSOURCE_CONNECTION_APP_V2"] = Value;

        var result = await _scene.DescribeAsync();

        ShouldHold(result, "213,214,216,217,218,221,999");
    }

    // With several databases the variable with no name is used for none, which is SQLSRC215.
    [Fact]
    public async Task Run_ValueInTheVariableWithNoNameAndEveryError_IsNeverPrinted()
    {
        AddEveryError();
        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Value;

        var result = await _scene.DescribeAsync();

        ShouldHold(result, "214,215,216,221");
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_ValueInTheVariableWithNoNameAndOneDatabase_IsUsedAndNeverPrinted()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Value;
        _scene.Describer.Failures["Bad"] = FakeDescriber.Failure("Bad");

        var result = await _scene.DescribeAsync();

        ShouldHold(result, "217,999");
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe(Value);
    }

    // The value was typed where "name=value" stands.  Its first keyword is taken for a name, and only that is said.
    [Fact]
    public async Task Run_ConnectionStringGivenWithoutAName_IsNeverPrinted()
    {
        var result = await _scene.DescribeAsync("--connection", Value);

        ShouldHold(result, "213");
        result.Error.ShouldContain("--connection was given for Host, which is no database of this run");
    }

    [Theory]
    [InlineData("--connection", Marker)]
    [InlineData("--connection=" + Marker)]
    [InlineData("--connection:" + Marker)]
    [InlineData("--connection", "postgres=a", "--connection", "POSTGRES=" + Marker)]
    public async Task Run_ConnectionThatIsWrong_IsAWrongCommandLineThatDoesNotRepeatIt(params string[] args)
    {
        var result = await _scene.DescribeAsync(args);

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith("sqlsource: ");
        result.Error.ShouldNotContain(Marker);
        _scene.Run.Processes.Requests.ShouldBeEmpty();
    }

    // The rule of sub-phase 2.2 for an unknown option: nothing after it is read.
    [Fact]
    public async Task Run_MisspeltConnectionOptionFollowedByTheValue_NamesTheOptionAlone()
    {
        var result = await _scene.DescribeAsync("--conection", "postgres=" + Value);

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option '--conection'\n"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    public async Task Run_DescriberThatThrowsWithTheValue_PrintsTheMarkInItsPlace(string? debug)
    {
        if (debug is not null)
        {
            _scene.Run.Environment[Cli.DebugVariable] = debug;
        }

        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Value;
        _scene.Describer.OpenException = new ArgumentException($"Format of the initialization string: {Value}");

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldNotContain(Marker);
        result.Error.ShouldStartWith(
            "sqlsource : error SQLSRC200: sqlsource failed unexpectedly: System.ArgumentException: Format of the "
                + "initialization string: ***\n"
        );
        result.Error.Contains("    trace: ", StringComparison.Ordinal).ShouldBe(debug is not null);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    public async Task Run_DescriberThatReturnsTheValue_PrintsTheMarkInEachPlace(string? debug)
    {
        if (debug is not null)
        {
            _scene.Run.Environment[Cli.DebugVariable] = debug;
        }

        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Value;
        _scene.Describer.Server = new ServerInfo($"16.4 ({Value})", "FakeDriver", null, EquatableArray<ServerSetting>.Empty);
        _scene.Describer.Failures["Bad"] = new DescribeFailure(
            FakeDescriber.Rejected,
            new EquatableArray<string>([$"Bad at {Value}"]),
            DescribeStep.DescribeColumns,
            new EquatableArray<string>([$"connection to {Value} was lost"]),
            $"check {Value}"
        );

        var result = await _scene.DescribeAsync();

        result.Out.ShouldNotContain(Marker);
        result.Error.ShouldNotContain(Marker);
        result.Error.ShouldStartWith(
            $"{_users}(1,10): error SQLSRC999: The server rejected the query 'Bad at ***'\n"
                + "    query: Bad, database postgres, postgres 16.4 (***), FakeDriver\n"
                + "    step: describe columns\n"
                + "    server: connection to *** was lost\n"
                + "    help: check ***\n"
        );
    }

    // What is written to a sidecar is not printed, and is the server's own word.
    [Fact]
    public async Task Run_QueriesDescribed_PrintNothingOfTheConnection()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = Value;

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): 2 described, 0 skipped, 0 failed\n", ""));
        File.ReadAllText(_users + ".json").ShouldNotContain(Marker);
    }
}
```

- [ ] **Step 3: Run both classes**

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.FilterRunTests`
Expected: PASS, 9 tests.

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.SecretTests`
Expected: PASS, 17 tests.

Two things to know when one fails.  In `Run_FileThatNeedsNoEntry_KeepsItsSidecarUnderEachFilter`, the third row runs on a solution and names its one project: if `RunProjects` reports `SQLSRC207`, give the project's full path, `_scene.Project.ProjectPath`, in the place of `"App.csproj"`.  In `AddEveryError`, `Other.sql` takes its dialect from a `-- dialect:` marker in its preamble: if the plan reports the file, read `tests/SqlSource.Tests/Parsing/SqlDialectMarkerTests.cs` for the marker's form.

- [ ] **Step 4: Write the failing test of the second Ctrl+C**

In `tests/SqlSource.Tool.Tests/CliTests.cs`, add `using System.Threading;` if it is not there, and:

```csharp
    // A run that waits on a database and does not look at its token can still be ended from the keyboard.
    [Fact]
    public void Interrupt_FirstTime_CancelsTheRunAndKeepsTheProcess_SecondTime_LetsTheProcessEnd()
    {
        using var interrupt = new CancellationTokenSource();

        Cli.Interrupt(interrupt).ShouldBeTrue();
        interrupt.IsCancellationRequested.ShouldBeTrue();
        Cli.Interrupt(interrupt).ShouldBeFalse();
    }
```

Run: `dotnet build SqlSource.slnx`
Expected: FAIL to compile: `'Cli' does not contain a definition for 'Interrupt'`.

- [ ] **Step 5: Let a second Ctrl+C end the process**

In `src/SqlSource.Tool/Cli.cs`, in `Run`, replace the local function `Cancel` with:

```csharp
        void Cancel(object? sender, ConsoleCancelEventArgs e) => e.Cancel = Interrupt(interrupt);
```

and add after `Run`:

```csharp
    /// <summary>
    /// What Ctrl+C does.  The first time, the run is cancelled and ends by its own road, with its exit code, and
    /// the process is kept.  A second time, while the run has not ended, the process is left to be killed: a run
    /// that waits on a database may not look at its token.
    /// </summary>
    /// <returns>Whether the process is kept.</returns>
    internal static bool Interrupt(CancellationTokenSource interrupt)
    {
        if (interrupt.IsCancellationRequested)
        {
            return false;
        }

        interrupt.Cancel();
        return true;
    }
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests --filter-class SqlSource.Tool.Tests.CliTests`
Expected: PASS.

- [ ] **Step 6: Rewrite `TD-0024` to hold the one gap that is left**

This sub-phase is the item's trigger.  Its first gap is settled by task 6 and its second by step 5.  Replace the whole of `docs/tech-debt/TD-0024-gaps-of-the-tools-shell.md` with:

```markdown
# TD-0024 - No test reaches the line that a command line is not valid

## Problem

[`Cli`](../../src/SqlSource.Tool/Cli.cs) writes `sqlsource: the command line is not valid`, and not System.CommandLine's own message, which may repeat a token, when System.CommandLine rejects a command line or reads it another way than [`UsageCheck`](../../src/SqlSource.Tool/UsageCheck.cs) did.  No command line of the tool as it is does either, so no test reaches the line.

## Why it exists

It needs an option that System.CommandLine can reject after `UsageCheck` has passed it, such as one that may be given once and is given twice.  `--project`, `--database` and `--connection` may each be given several times, and `--force` takes no value.

## Impact

Low.  The line is a guard for a disagreement that the tests of `UsageCheck` are there to rule out.

## Proposed fix

With the first option that may be given only once, add a test that gives it twice and expects the tool's own line and nothing of the command line in the output.

## Trigger

Sub-phase 2.6, which adds `--log`, an option that is given once.
```

In `docs/tech-debt/README.md`, replace the description in the row of `TD-0024`, its last column, with:

```
No test reaches the line `sqlsource: the command line is not valid`, which the `sqlsource` tool writes when System.CommandLine and its own check of the command line disagree; it waits for an option that may be given only once
```

Run: `grep -rn "TD-0024" --include='*.md' --include='*.cs' .`
Expected: the item's file and its row in `README.md`, and no other line that speaks of three gaps, of `SQLSRC200` or of Ctrl+C.  Mend one that does.

- [ ] **Step 7: Closing steps, and commit**

```bash
git add src/SqlSource.Tool tests/SqlSource.Tool.Tests docs/tech-debt
git commit -m "Hold the filters and the secret, and let a second Ctrl+C end the tool

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: The documents, and the outline says Done

**Files:**
- Modify: `src/SqlSource.Tool/AGENTS.md`, `src/SqlSource.Tool/README.md`
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`

- [ ] **Step 1: Add the guidance of `Describing/` to the tool's `AGENTS.md`**

In `src/SqlSource.Tool/AGENTS.md`, after the bullet that starts `- **Of the settings that are not valid, the tool reports the ones it reads**`, add:

```markdown
- **`Describing/` is a run after its plan, in steps that stay apart.**  `Describing/DescribeRun.cs` calls them in order: `RunDecisions.cs` reads the sidecars that are there and gives each query a state; `Connections.cs` finds the connection of each selected database; `DatabaseRuns.cs` opens one session for each database and describes its queries one at a time; `FileOutcomes.cs` gives each file its outcome and its target and changes nothing; `SidecarStore.cs` applies the outcomes to the disk; `RunSummary.cs` gives the lines that end the run.  Sub-phase 2.6 compares where `SidecarStore` writes, so the step that decides must never write and the step that writes must never decide.
- **A describer returns a failure and never throws.**  What a server or a user can cause is a `DescribeFailure`, which the run reports at the query in the format of `Describing/DescribeErrors.cs`.  An exception from one is a bug of the describer and ends the run with `SQLSRC200`.  The released tool has no describer: `ToolHost.Create` gives `DescriberRegistry.Empty`, and every query that must be described is `SQLSRC216`, until phase 3 registers PostgreSQL's.
- **Every call a describer makes to an engine goes through `IDescribeExchange`**, the one it is given in its `OpenRequest`, so that phase 3 can record a run and replay it.  A request and an answer are plain data that can be written as JSON, and a call that can fail on the server returns the failure as its answer.  `tests/SqlSource.Tool.Tests/FakeDescriber.cs` is a describer written that way.
- **A connection's value goes to the describer and nowhere else.**  `OpenRequest` is the one thing that holds it for a describer, and `ConnectionArgument`, `DatabaseConnection` and `OpenRequest` each override `ToString` to leave it out: a record prints its members.  What the tool may say of a connection is where it came from, `--connection` or the name of a variable.  A describer puts the value in nothing it returns, and the run does not rely on that: every text of a describer that is printed, an exception's included, goes through `Describing/Redaction.cs`, which writes the whole value as `***`.  `tests/SqlSource.Tool.Tests/SecretTests.cs` holds the tool to all of it; a new place that prints what a describer gave gets a case there.
- **A sidecar is written whole or not at all, through `SidecarStore`**: to a temporary file beside it that is then moved over the old one.  The file's target holds an entry for every query that needs one, each described in this run or current, where current means that the hash and the database match and that this version of the tool wrote the file: an entry from an older tool never stands beside a new one.  A file with a query that has neither is left as it was.
- **A filter narrows what a run may read and change, as well as what it describes.**  A sidecar is read and written only for a file with a selected query that needs an entry, and deleted only in a run with no `--project`, no `--database` and no `.sql` path: a run on a part does not know what the rest needs of a file.  `tests/SqlSource.Tool.Tests/FilterRunTests.cs` holds it.
- **A database needs a describer and a connection only when a query of it must be described.**  The connection of every selected database is looked up, since the summary says which have none, and a missing one is an error only in `DatabaseRuns`.  A query that the plan could not describe, and every query of a file that is not `Ready`, counts as failed and gets no further error.
```

In the bullet that starts `- **`Planning/` makes the plan of a run and reports nothing.**`, replace `because sub-phase 2.5 writes a sidecar only when every query of its file that needs an entry has one` with `because a sidecar is written only when every query of its file that needs an entry has one`.

- [ ] **Step 2: Read the tool's `AGENTS.md` against the code**

Every file, type and member the file names must exist under that name.  Run:

```bash
grep -o '`[A-Za-z/]*\.cs`' src/SqlSource.Tool/AGENTS.md | sort -u | tr -d '`'
```

and check that each path exists under `src/SqlSource.Tool/` or, for one that starts with `tests/`, at the root.  Mend a name that does not.

- [ ] **Step 3: Say in the tool's readme what `describe` does now**

In `src/SqlSource.Tool/README.md`, replace the second paragraph, the one that starts `This version finds the projects to run on`, and the `console` block after it, with:

````markdown
`describe` finds the projects to run on, reads what the compiler is given for each, and works out which queries a database must describe: the ones in a `.sql` file that a type with `[SqlSourceGenerate]` claims, whose output is `models` or `codegen`.  It asks the database of each such query for its parameters and columns and writes what it learns into a sidecar, a `.sql.json` file beside the `.sql` file, which is committed with it.

**This version has no describer for any database yet.**  It reports each query that must be described as [`SQLSRC216`](https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc216), and everything else of the command is in place.

```console
$ dotnet sqlsource describe --connection billing="Host=localhost;Database=billing"
$ dotnet sqlsource describe App.slnx --project src/App/App.csproj
$ dotnet sqlsource describe src/App/Queries/Users.sql
$ dotnet sqlsource describe --database billing --force
```

A query belongs to a logical database: the one its `-- database:` marker names, or the `SqlSourceDatabase` metadata of its file, or the property of that name, or else the name of its dialect.  A connection is given for that name, on the command line or in the environment, and never in the project file, since it holds credentials:

| Source | For the database `billing` |
|----|----|
| `--connection <name>=<connection string>` | `--connection billing="Host=localhost;Database=billing"` |
| The variable of the name | `SQLSOURCE_CONNECTION_BILLING`: the name in upper case, with every character that is not a letter or a digit written as `_` |
| `SQLSOURCE_CONNECTION` | Used when the run has exactly one database |

The command line wins over the variable of the name, and that over `SQLSOURCE_CONNECTION`.  The tool never prints a connection string.

A query whose entry in the sidecar is current, because neither its SQL nor its database changed since this version of the tool described it, is skipped, and a database whose queries are all current needs no connection.  `--force` describes every query of the run again: it is the command to run after a change to the schema, which the tool cannot see.  `--database`, which may be given several times, restricts the run to the queries of the databases it names, and a path that ends in `.sql` to that file; a restricted run leaves every other sidecar as it was.  A sidecar is written only when every query of its file has an entry, and a run ends with one line for each database:

```console
billing (postgres): 4 described, 12 skipped, 0 failed
reports (postgres): no connection, 5 skipped
```

The exit code is `0`, or `1` when anything was reported as an error.
````

In the paragraph that starts `` `describe` takes a `.sln`, `.slnx` or `.csproj` file ``, remove the last sentence, `A path that ends in `.sql` restricts the run to that file, and may be given several times.`: the new text says it.

This file is the package's readme on nuget.org, so every link in it is an absolute URL.

- [ ] **Step 4: Bring the epic outline up to date**

In `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`:

In the table under `## Phases`, in the row that starts `| 2.5 Describe |`, replace `| In progress |` with `| Done |`.

Under `## Phase 2 - the tool and the snapshot`, in the list under `### Settled`, add after the bullet that starts `- **A sidecar of a higher format version**`:

```markdown
- **A text of a describer** is printed with the connection's value written as `***`, the message and the trace of an exception it throws included: a driver's message for a connection string that is wrong can quote the string.  The whole value is replaced and nothing less, since the tool does not read a connection string.
- **A query that the plan could not describe**, and every query of a file that is not ready, counts as failed in the summary and gets no error of sub-phase 2.5.  A sidecar is read only for a file with a selected query that needs an entry.  The summary's `no connection` form is for a database with no failed query.
```

- [ ] **Step 5: Check that nothing was deferred and no debt was left out**

The spec expects nothing in `docs/deferred` and nothing new in `docs/tech-debt`.  Read the spec's sections once more against the code, and its test table against the test classes.  For anything of the spec that was not delivered, add an item to `docs/deferred` as its `AGENTS.md` says; for any problem found and not mended, an item to `docs/tech-debt`.

- [ ] **Step 6: Closing steps, and commit**

```bash
git add src/SqlSource.Tool/AGENTS.md src/SqlSource.Tool/README.md docs
git commit -m "Document describe and close phase 2.5

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 7: Check the version before the pull request**

```bash
git fetch --tags origin
```

```bash
git tag --list 'v*' --sort=-v:refname | head -n 1
```

Expected: no output, as when this plan was written.  If a tag is printed and `VersionPrefix` in `Directory.Build.props` is not greater than it, stop and ask the owner what the new version should be.
