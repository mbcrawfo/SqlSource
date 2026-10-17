# TD-0054, pull request 2: the helpers of the container tests - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Each helper of `tests/SqlSource.Tests.Postgres` that item 4 of TD-0054 names, and the nullability tuples and small duplicates of items 2 and 8 there, has one definition, and TD-0054 is rewritten to items 5, 6 and 7.

**Architecture:** `PostgresContainer` gains what reaches the server from a test (`ExecuteAsync`, `ScalarAsync`) and the skips by the server's major (`SkipBelow`, `SkipUnlessRecordedMajor`, `RecordedMajors`).  `Describer/Sessions.cs` gains the typed open, the asserted describe and the captured describe and open.  `Recording` is a small class at the project's root; the `PGPASSWORD` skip goes in `tests/SqlSource.Tool.Testing`, which both projects see.  No test moves and none is renamed; nothing under `src/` changes.

**Tech Stack:** C# (`LangVersion` `preview`) on `net10.0`; xunit v3 on Microsoft.Testing.Platform with Shouldly; Npgsql and Testcontainers.

**Spec:** `docs/superpowers/specs/2026-10-16-td-0054-duplicated-postgresql-test-patterns-design.md`, the section "Pull request 2".

**Branch:** this worktree's branch, which starts from pull request 1's head (`claude/postgresql-test-patterns-debt-e05c9f`, `e01d065`).  The pull request is opened only after pull request 1 (https://github.com/mbcrawfo/SqlSource/pull/63) has merged, from a branch brought up to date with `main`.  This file is untracked until Task 4 commits it.

## Where the plan settles what the spec leaves open

- **The skips decide in a static core that takes the server's major**, so a test covers both branches on any image: `PostgresContainer.SkipBelow(int server, int major, string reason)` and `SkipUnlessRecordedMajor(int server, string what)`.  The instance methods of the spec pass `Major`.
- **`SkipUnlessRecordedMajor(what)` says `"{what} are pinned for PostgreSQL 14 and 18 alone."`**, the majors read from `RecordedMajors`.  Its `what`s are `"The set operations"` (three skips), `"The printed forms"`, `"The expectations"` (`RealisticDescribe.SkipMessage`, which goes) and `"The budgets"`: `RealisticNullabilityNoiseTests` skips the same way, which the spec misses.
- **The RETURNING constant is `PostgresContainer.NoOldAndNewInReturning`**, beside `SkipBelow`.
- **The typed open is `Sessions.OpenSessionAsync`**, which returns the `PostgresSession` after `opened.Value.ShouldBeOfType<PostgresSession>()`.  Its sites that asserted `ShouldNotBeNull()` now assert the type, which is stronger.
- **The capture is two methods**, since four of the ten sites need the session itself: `DescribeCapturedAsync`, which describes in a session of its own, and `OpenCapturedAsync`, which opens one.
- **A data source of `ConnectionString` that runs one statement and is dropped becomes `ExecuteAsync` or `ScalarAsync`.**  One that runs several commands, holds a transaction, reads rows or binds a parameter keeps its own: `ColumnTests` at the domain `numeric`, the inheritance tables, `FormatTypesOfAsync` and the INSTEAD rule; `ParameterTests`' canary and schema `"user"`; `TableMatchTests`' dropped column; `PlanOutputTests`' `ExplainExecuteAsync`, the cast pairs and the two that take another connection; `SetOperationTests`' `ReturnsNullAsync`; `SessionTests`' `BackendsOfAsync` source; `Sessions.EndBackendAsync`.
- **`AfterLiveExchange` takes a hook of the call's method, request and token**, and a test that needs its session in the hook closes over a local it sets once the session is open.
- **The `PGPASSWORD` skip is `PasswordFiles.SkipUnlessRead()`**, in `tests/SqlSource.Tool.Testing/PasswordFiles.cs`.  It decides something, so `tests/SqlSource.Tool.Tests/PasswordFilesTests.cs` tests it in the collection `ChangesTheEnvironment`.
- **`RealisticCase.AsFixture` passes `SidecarValues.Engine.Postgres`** too: it is a `new FixtureCase(...)`, which pull request 1's rule covers.

## Global Constraints

- Nothing under `src/` changes.
- No test is lost or renamed.  `--list-tests` of `tests/SqlSource.Tests.Postgres` and `tests/SqlSource.Tool.Tests` after the work is the list before it plus the new tests of `PostgresContainerTests` and `PasswordFilesTests`.
- A test's expectation does not change in value; a skip's message may change its words.  A literal becomes a name only for the same value.
- A literal stays where it is what the test reads or checks: `ToolProcessTests`' `entry.Engine.ShouldBe("postgres")`, `UsesPostgres.Name`, `RepositoryPaths`' folder `"postgres"`.
- `ModifierCallTests`, `ExplainTests` and `RelationsCallTests` build a `PostgresConnection` and `PostgresLive` on purpose and stay as they are.  `Realistic/RealisticDatabase.ScalarAsync` takes another connection and stays.  `Describer/Requests.cs` stays.
- Helpers of the container tests are `internal`; `PostgresContainer` stays `public sealed`, since it is a collection fixture.  `tests/SqlSource.Tool.Testing`'s are `internal`, namespace `SqlSource.Tool.Testing`.
- The build treats warnings as errors with `AnalysisLevel` `latest-all`.  Fix what it reports, an unused `using` included; never suppress.  `ImplicitUsings` is off.
- Comments say what a thing is for, in the plain declarative style of the files around them, with two spaces after a full stop.
- Every commit passes `./pre-commit-validation.sh` (it needs Docker running).  Run `./format.sh`, then the validation, then commit in a command of its own.  The container tests also pass with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` before the push.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- **A skip that runs before a failure it used to let through.**  `RealisticDescriptionTests` fails a recording on a major that is not recorded (`Expectation.Refuse`); a `SkipUnlessRecordedMajor` placed before that check would skip it instead.  Task 1 keeps the refusal first and says so.
- **A version branch turned around.**  `ServerMajorAsync() < 18`, `>= 17` and `< 15` become `SkipBelow` or `postgres.Major`; a `SkipBelow(17, ...)` where `< 18` stood runs a test on 17 that cannot pass.  Task 1 lists each site's number, and the run on `postgres:14.24` catches a skip that went missing there.
- **A projection that drops an assertion.**  `ScanTests`, `SetOperationTests` and `WalkTests` check `description.Plan` before they project, and every copy of `ShouldDescribeAsync` checks the failure with its id.  Task 2 keeps each.
- **A hook that sees the session too early.**  `AfterLiveExchange` runs on `"open"` as well, before the test has its session; each hook acts only once its session is set or for its own method, as today.
- **A test dropped in a mass edit.**  Task 4 compares the lists of tests with those Task 1 saves.

---

### Task 1: What `PostgresContainer` gains (item 4)

**Files:**
- Modify: `tests/SqlSource.Tests.Postgres/PostgresContainer.cs`, `PostgresContainerTests.cs`
- Modify: `Realistic/RealisticCase.cs:32-37`, `Realistic/Describer/RealisticDescribe.cs:36,50`, `Realistic/Describer/RealisticDescriptionTests.cs:59-68`, `Realistic/Describer/RealisticNullabilityNoiseTests.cs:21,26-30`
- Modify the private helpers and data sources of `Describer/ColumnTests.cs`, `WalkTests.cs`, `ExpressionTests.cs`, `ScanTests.cs`, `SetOperationTests.cs`, `NpgsqlCheckTests.cs`, `PlanOutputTests.cs`, `SessionTests.cs`, `DescribeCommandTests.cs`

**Interfaces:**
- Produces, on `PostgresContainer`:
  - `public Task ExecuteAsync(string sql)`, which runs it on a data source of `ConnectionString` with the test's cancellation token
  - `public Task<object?> ScalarAsync(string sql)`, the same with `ExecuteScalarAsync`
  - `public static IReadOnlyList<int> RecordedMajors { get; } = [14, 18];`, with `RealisticCase`'s comment widened to the plan forms that are pinned
  - `public const string NoOldAndNewInReturning = "PostgreSQL before 18 has no old and new in RETURNING.";`
  - `public void SkipBelow(int major, string reason)` over `public static void SkipBelow(int server, int major, string reason)`, which is `Assert.SkipWhen(server < major, reason)`
  - `public void SkipUnlessRecordedMajor(string what)` over `public static void SkipUnlessRecordedMajor(int server, string what)`, which skips with `"{what} are pinned for PostgreSQL 14 and 18 alone."` unless `RecordedMajors` holds `server`; build the text invariantly from `RecordedMajors`

- [ ] **Step 1: Save the lists of tests as they are**

```bash
dotnet build tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj
dotnet build tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj
dotnet test --project tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj --no-build --list-tests | grep '^  SqlSource' | sort > "${TMPDIR:-/tmp}/td-0054-pr2-postgres-before.txt"
dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --no-build --list-tests | grep '^  SqlSource' | sort > "${TMPDIR:-/tmp}/td-0054-pr2-tool-before.txt"
```

Expected: each file has one line per test.  Then run `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj --no-build` and note its counts of passed and skipped tests, for Task 4.

- [ ] **Step 2: Write the failing tests in `PostgresContainerTests`**

```csharp
[Fact]
public void SkipBelow_ServerOfThatMajor_DoesNotSkip() =>
    Should.NotThrow(() => PostgresContainer.SkipBelow(18, 18, "a reason"));

[Fact]
public void SkipBelow_ServerBeforeThatMajor_SkipsWithTheReason() =>
    Should.Throw<SkipException>(() => PostgresContainer.SkipBelow(17, 18, "a reason")).Message.ShouldEndWith("a reason");

[Fact]
public void SkipBelow_OfTheContainer_AsksTheServersMajor() =>
    Should.NotThrow(() => postgres.SkipBelow(postgres.Major, "a reason"));

[Theory]
[InlineData(14)]
[InlineData(18)]
public void SkipUnlessRecordedMajor_RecordedMajor_DoesNotSkip(int server) =>
    Should.NotThrow(() => PostgresContainer.SkipUnlessRecordedMajor(server, "The things"));

[Fact]
public void SkipUnlessRecordedMajor_OtherMajor_SkipsNamingTheRecordedOnes() =>
    Should.Throw<SkipException>(() => PostgresContainer.SkipUnlessRecordedMajor(16, "The things"))
        .Message.ShouldEndWith("The things are pinned for PostgreSQL 14 and 18 alone.");
```

`SkipException` is `Xunit.Sdk.SkipException`, whose message ends with the reason.

- [ ] **Step 3: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj --filter-class "SqlSource.Tests.Postgres.PostgresContainerTests"`
Expected: FAIL to build, `SkipBelow` does not exist.

- [ ] **Step 4: Add the members of the Interfaces block, and run the tests to see them pass**

- [ ] **Step 5: Use them**

- The private `RunAsync`, `ExecuteAsync` and `ScalarAsync` of `ColumnTests`, `WalkTests`, `ExpressionTests`, `ScanTests`, `SetOperationTests`, `NpgsqlCheckTests`, `PlanOutputTests` (`RunAsync`), `SessionTests` and `DescribeCommandTests` go; callers use `postgres.ExecuteAsync` or `postgres.ScalarAsync` (`_postgres` in `DescribeCommandTests`, whose `ExecuteAsync` returns a scalar and becomes `ScalarAsync`).
- The single-statement data sources become the same calls: `ColumnTests`' `MakeOwnTablesAsync`, the NOT VALID table, the foreign partition, the foreign table and the materialized view; `WalkTests`' partitioned table.  The ones the settlements above list stay.
- `ColumnTests` and `WalkTests` lose `ServerMajorAsync` and their `System.Globalization` if unused.  Each site, as it stands:
  - `ColumnTests` `< 15` negative scale, `< 18` NOT VALID, `< 17` MERGE: `postgres.SkipBelow(15 | 18 | 17, <its message>)`
  - `ColumnTests` `< 18` RETURNING and `WalkTests`' four: `postgres.SkipBelow(18, PostgresContainer.NoOldAndNewInReturning)`
  - `ColumnTests`' inheritance `if (... < 18)` and `WalkTests`' `>= 17`: `postgres.Major`
- `RealisticCase.RecordedMajors` goes; `RealisticCase.Files`, `RealisticDescribe.ExpectationOn` and `RealisticNullabilityNoiseTests` read `PostgresContainer.RecordedMajors`.
- `SetOperationTests`' `WalkedAsync` and `PlanOutputTests`' two set-operation skips call `postgres.SkipUnlessRecordedMajor("The set operations")`.  `PlanOutputTests`' printed forms call `postgres.SkipUnlessRecordedMajor("The printed forms")` first and then pick `printedOn14` on 14 and `printedOn18` otherwise.
- `RealisticDescriptionTests.Describe_Case_IsItsRecordedDescription` fails on `Expectation.Refuse` first, then calls `postgres.SkipUnlessRecordedMajor("The expectations")`; `RealisticDescribe.SkipMessage` goes.
- `RealisticNullabilityNoiseTests.Noise_OverTheCorpus_IsTheBudget` calls `postgres.SkipUnlessRecordedMajor("The budgets")` and reads `Budgets[major]`.

- [ ] **Step 6: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj`
Expected: PASS, with Step 1's count plus the six new cases.

- [ ] **Step 7: Commit**

`./format.sh`, `./pre-commit-validation.sh`, then commit `tests/SqlSource.Tests.Postgres` as "Reach the server and skip by its major through PostgresContainer in the container tests (TD-0054)".

### Task 2: What `Describer/Sessions.cs` gains, and the nullability answers (items 4 and 2)

**Files:**
- Modify: `tests/SqlSource.Tests.Postgres/Describer/Sessions.cs`
- Modify: `Describer/ColumnTests.cs`, `WalkTests.cs`, `ScanTests.cs`, `ExpressionTests.cs`, `SetOperationTests.cs`, `TableMatchTests.cs`, `ParameterTests.cs`, `NpgsqlCheckTests.cs`, `SessionTests.cs`, `Realistic/Describer/RealisticDescribe.cs:141-170`

**Interfaces:**
- Consumes: `NullabilityAnswers` of `tests/SqlSource.Tool.Testing` (its names and `Of`).
- Produces, on `Sessions`:
  - `OpenAsync(string connection, IDescribeExchange? exchange = null, IRunLog? log = null, string database = PostgresContainer.Database)`
  - `Task<PostgresSession> OpenSessionAsync(string connection, IDescribeExchange? exchange = null, IRunLog? log = null)`, `OpenAsync` then `opened.Value.ShouldBeOfType<PostgresSession>()`
  - `Task<QueryDescription> ShouldDescribeAsync(string connection, DescribeRequest request, IDescribeExchange? exchange = null)`, `DescribeAsync` then `described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id)` and `described.Value.ShouldNotBeNull()`
  - `Task<QueryDescription> ShouldDescribeAsync(string connection, string sql, IDescribeExchange? exchange = null)`, over `Requests.Of(sql)`
  - `Task<(Outcome<QueryDescription, DescribeFailure> Described, DescribeCapture Capture)> DescribeCapturedAsync(string connection, DescribeRequest request, IDescribeExchange? exchange = null)`, `DescribeAsync` through `new CapturingExchange(exchange ?? LiveExchange.Instance, capture)`
  - `Task<(PostgresSession Session, DescribeCapture Capture)> OpenCapturedAsync(string connection, IDescribeExchange? exchange = null)`, the same over `OpenSessionAsync`
  - each `DescribeCapture` is `new DescribeCapture(SidecarValues.Engine.Postgres)`

- [ ] **Step 1: Add the members**

- [ ] **Step 2: The describes**

- `ColumnTests`, `TableMatchTests` and `WalkTests` lose their `ShouldDescribeAsync`; a call becomes `Sessions.ShouldDescribeAsync(postgres.ConnectionString, sql)`, or the class keeps a one-line private over it where it calls it often.  `ParameterTests`' `ShouldDescribeAsync` stays a one-line private, `(await Sessions.ShouldDescribeAsync(postgres.ConnectionString, request)).Parameters`.
- `WalkTests.WalkedAsync`, `ScanTests.DescribedAsync`, `ExpressionTests.AnswersAsync` and `SetOperationTests.WalkedAsync` describe through `ShouldDescribeAsync` and project with `NullabilityAnswers.Of(description.Columns.ShouldNotBeNull())`, keeping their checks of `Plan`.  `ExpressionTests`' optional connection stays.
- `ColumnTests.NullabilityOf` goes for `NullabilityAnswers.Of`.

- [ ] **Step 3: The nullability answers**

Each tuple of `ColumnTests`, `WalkTests`, `ScanTests`, `ExpressionTests` and `SetOperationTests` becomes its name, through `using static SqlSource.Tool.Testing.NullabilityAnswers;`.  The fields `ScanTests.Catalog`, `Nullable`, `OuterJoin`, `Heuristic`, `Expression`, `Unknown`, `ExpressionTests.NotNull`, `Unknown` and `SetOperationTests.Proven`, `Unknown` go: `Proven` is `NotNullSetOperation`, `Unknown` is `UnknownNoOrigin`, `ScanTests.Nullable` is `NullableCatalog`, `ExpressionTests.NotNull` is `NotNullExpression`.  Every pair these classes use has a name already.  Then:

Run: `grep -nE '\((true|false|null), "[a-z-]+"\)' tests/SqlSource.Tests.Postgres/Describer/*.cs`
Expected: no output.

- [ ] **Step 4: The opens and the captures**

- Each `Sessions.OpenAsync(...)` followed by `opened.Value.ShouldBeOfType<PostgresSession>()` or `ShouldNotBeNull()` is `await using var session = await Sessions.OpenSessionAsync(...)`: `ParameterTests` (two), `WalkTests`, `NpgsqlCheckTests` (four), `SessionTests` (`OpenAsync_Container_...`, `Describe_AfterTheServerEndedTheSession_...`, `Describe_StatementPrepared_...`).  `SessionTests.ShouldNotOpenAsync` and `OpenAsync_ExchangeThatThrows...` keep `OpenAsync`, since they open nothing.
- The describes of `ParameterTests` at its four captures and of `ColumnTests` at its two use `DescribeCapturedAsync`; `ColumnTests` then checks `described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id)`, as its `ShouldDescribeAsync` did.
- The opens of `ParameterTests` at line 220 and of `SessionTests` at `OpenAsync_ThroughACapturingExchange_...`, `Describe_ServerEndsTheSession...` and `Describe_TableDropped...` use `OpenCapturedAsync`, then `await using var session = opened.Session;`.
- `RealisticDescribe.DescribeAsync` opens through `Sessions.OpenAsync(connection, database: PostgresContainer.RealisticDatabase)` and keeps its `InvalidOperationException` for a session that did not open.

- [ ] **Step 5: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj`
Expected: PASS, with Task 1's count.

- [ ] **Step 6: Commit**

`./format.sh`, `./pre-commit-validation.sh`, then commit as "Open, describe and capture through Sessions, and name the nullability answers, in the container tests (TD-0054)".

### Task 3: The rest of item 4, and item 8

**Files:**
- Create: `tests/SqlSource.Tests.Postgres/Recording.cs`
- Create: `tests/SqlSource.Tool.Testing/PasswordFiles.cs`, `tests/SqlSource.Tool.Tests/PasswordFilesTests.cs`
- Modify: `Describer/SessionTests.cs`, `Describer/FixtureCasesTests.cs`, `Describer/FixtureVersionTests.cs:17`, `Describer/DescribeCommandTests.cs:45`, `Realistic/RealisticCase.cs:47`, `Realistic/Describer/RealisticDescriptionTests.cs:22`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresConnectionTests.cs:114-175`

**Interfaces:**
- Produces:
  - `Recording.IsAsked(string variable)`, `internal static`, namespace `SqlSource.Tests.Postgres`: whether the variable holds a value
  - `PasswordFiles.SkipUnlessRead()`, `internal static`, namespace `SqlSource.Tool.Testing`: skips with `"PGPASSWORD is set, and the driver takes it before a password file."` while `PGPASSWORD` is set, under today's comment
  - `SessionTests.AfterLiveExchange(Func<string, object?, CancellationToken, Task> after) : IDescribeExchange`, private sealed: asks `LiveExchange.Instance`, then awaits `after(method, request, cancellationToken)`, then returns the answer

- [ ] **Step 1: Write the failing test of the skip**

```csharp
[Collection(nameof(ChangesTheEnvironment))]
public sealed class PasswordFilesTests
{
    [Fact]
    public void SkipUnlessRead_PasswordVariable_SkipsWhileItIsSetAndNotOtherwise()
    {
        var was = Environment.GetEnvironmentVariable("PGPASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("PGPASSWORD", "x");
            Should.Throw<SkipException>(PasswordFiles.SkipUnlessRead).Message.ShouldEndWith("before a password file.");

            Environment.SetEnvironmentVariable("PGPASSWORD", null);
            Should.NotThrow(PasswordFiles.SkipUnlessRead);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PGPASSWORD", was);
        }
    }
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.PasswordFilesTests"`
Expected: FAIL to build, `PasswordFiles` does not exist.

- [ ] **Step 3: Write `PasswordFiles`, and run the test to see it pass**

- [ ] **Step 4: Use the skip and the marker**

`SessionTests` and `PostgresConnectionTests` lose `PasswordSetSkip` and its comment and call `PasswordFiles.SkipUnlessRead()`.  `SessionTests.Marker` goes for `Secrets.Marker`.  `PostgresConnectionTests`' four `"s3cret-marker"` are `Secrets.Marker`, the two file texts `Secrets.Marker + " is no certificate"` and `Secrets.Marker + " is no entry"`.

- [ ] **Step 5: One `AfterLiveExchange`**

`EndsTheBackendAfterPrepare`, `ReadsTheListedText` and `DropsTheTableAfterColumns` go.  Each test passes a hook that does what its class did after the live call: end the backend after `"prepare"` once its session is set; on a `PostgresPrepareRequest`, keep it and read the listed text on the session's connection; drop `session_gone` after the first `"columns"`, through `postgres.ExecuteAsync`.  `ThrowsAfterTheServerAnswered` stays.

- [ ] **Step 6: Recording, and the engine**

- `FixtureCasesTests` and `RealisticDescriptionTests` lose `Recording` and call `Recording.IsAsked(RecordVariable)`.
- `FixtureCasesTests` loses `Engine`; `FixtureCases.Of` and its capture take `SidecarValues.Engine.Postgres`, and `RecordAsync` writes through `FixtureCaseFiles.WriteRecordingAsync(fixture, capture, expected, token)`.
- `FixtureVersionTests`, `DescribeCommandTests` and `RealisticCase.AsFixture` pass `SidecarValues.Engine.Postgres`.

Run: `grep -rn '"postgres"' tests/SqlSource.Tests.Postgres --include='*.cs'`
Expected: only `UsesPostgres.Name`, `RepositoryPaths`' folder and `ToolProcessTests`' assertion.

- [ ] **Step 7: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj` and `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: PASS.

- [ ] **Step 8: Commit**

`./format.sh`, `./pre-commit-validation.sh`, then commit as "Share the recording switch, the hook after a live call, the secret marker and the PGPASSWORD skip, and name the postgres engine once, in the container tests (TD-0054)".

### Task 4: Documents, the lists of tests, the second image, and the plan

**Files:**
- Modify: `tests/AGENTS.md`, `docs/tech-debt/TD-0054-duplicated-and-uneven-patterns-in-the-postgresql-tests.md`, `docs/tech-debt/README.md:44`
- Add: this plan

- [ ] **Step 1: Compare the lists of tests**

Build both projects, list them into `td-0054-pr2-postgres-after.txt` and `td-0054-pr2-tool-after.txt` as in Task 1's Step 1, and `diff` each with its `before`.
Expected: the six new cases of `PostgresContainerTests` and the one of `PasswordFilesTests`.  Nothing else.

- [ ] **Step 2: Run the container tests on the oldest image**

Run: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj`
Expected: PASS, with Task 1's counts of passed and skipped tests on 14.24 plus the six new cases, which pass there.

- [ ] **Step 3: `tests/AGENTS.md`**

- The bullet on the two libraries names `PasswordFiles` among `SqlSource.Tool.Testing`'s helpers.
- The bullet on `tests/SqlSource.Tests.Postgres` says a test that needs the server reaches it through `PostgresContainer.ExecuteAsync` and `ScalarAsync` and skips by `SkipBelow` or `SkipUnlessRecordedMajor`, opens and describes through `Describer/Sessions.cs`, and reads a recording's variable through `Recording.cs`.
- The realistic bullet's `RealisticCase.RecordedMajors` becomes `PostgresContainer.RecordedMajors`.

- [ ] **Step 4: TD-0054**

Rewrite the item to what remains: the Problem says items 1, 2, 3, 4 and 8 are resolved and keeps the numbers of 5, 6 and 7; the Proposed fix points at the spec's pull request 3.  Keep the file's labelled bullets, `- **5. Title.** ...` for a finding and `- **5.** ...` for its fix, since a Markdown list of `5. 6. 7.` renders as 1, 2, 3.  The row in `docs/tech-debt/README.md` describes what remains: classes of over a thousand lines, uneven names, and theory rows named by their JSON.

- [ ] **Step 5: Commit**

`./format.sh`, `./pre-commit-validation.sh`, then commit the three documents and this plan as "Rewrite TD-0054 to what remains after the container tests' helpers".

- [ ] **Step 6: Review the whole branch, and fix every finding**

- [ ] **Step 7: Open the pull request**

Once pull request 1 has merged: bring the branch up to date with `main`, run `./pre-commit-validation.sh` and the run of Step 2 on the tree as it will be pushed, check `VersionPrefix` against the highest tag, push, and open it against `main` as "TD-0054, 2 of 3: the helpers of the container tests".  Turn on Auto-fix.
