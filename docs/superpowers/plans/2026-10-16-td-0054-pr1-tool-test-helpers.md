# TD-0054, pull request 1: the helpers of the tool's tests - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Each script builder, nullability tuple, row, plan, request and small duplicate that items 1, 2, 3 and 8 of TD-0054 name in `tests/SqlSource.Tool.Tests` has one definition, and TD-0054 is rewritten to what remains.

**Architecture:** The private builders of `PostgresSessionTests` become fluent instance methods of `PostgresScript`; `PostgresTables`, `PostgresRows` and `PostgresPlans` gain the rows and plans that classes copy; a new `PostgresRequests` holds the request builders.  Helpers that the container tests will need in pull request 2 (`NullabilityAnswers`, `Typmods`, `FixtureCaseFiles`) go in `tests/SqlSource.Tool.Testing`.  No test moves and none is renamed; nothing under `src/` changes.

**Tech Stack:** C# (`LangVersion` `preview`) on `net10.0`; xunit v3 on Microsoft.Testing.Platform with Shouldly.

**Spec:** `docs/superpowers/specs/2026-10-16-td-0054-duplicated-postgresql-test-patterns-design.md`, the section "Pull request 1".

**Branch:** `claude/postgresql-test-patterns-debt-e05c9f`, which holds `c26bd5f` (TD-0055, its own pull request) and the spec as `d02a288`.  This file is untracked until Task 5 commits it.

## Where the plan settles what the spec leaves open

- **The `Prepares` that also answers an empty `columns` is `PreparesWithNoColumns`.**  Today's `PreparesOnly` becomes `Prepares`.
- **Answers a builder makes are static members of `PostgresScript`**: `Freed`, `NoPlan`, `PlanOf(PlanNode)` and `Agreeing(params PostgresColumnRow[])`, the answer of `check` that `Checks` builds and `PostgresEventsTests`' `Sees` copies.
- **`Partitions` and `Relations` take the request and the answer**, since the tests keep their requests in fields and assert on them.  `relations` answers a `PostgresTablesAnswer`.
- **`NullabilityAnswers` names only the pairs the tests use**: the ten below.  A pair a later edit finds gets a name by the same rule.
- **`NullabilityWalkTests` and `PostgresSessionTests` keep a one-line `AnswersOf`** that passes their columns to `NullabilityAnswers.Of`, since one holds a `WalkOutcome` and the other a `QueryDescription`.
- **The request and SQL that the capture tests share are `CaptureInputs.GetUser` and `CaptureInputs.AwkwardSql`.**  `SqlTexts.GetUser` is another value.
- **The case writer is `FixtureCaseFiles`**, with `Create` for a case's folder and `query.sql`, and `WriteRecordingAsync` for its `capture.json` and `expected.json`.  `DescriberFixtureTests`' case without an `expected.json` uses `Create` and `WriteCaptureAsync`.
- **The new tests are `NullabilityAnswersTests` at the root and `FixtureCaseFilesTests` and `FixtureCasesTests` in `Fixtures/`.**  The last holds the two facts that move from `FixtureReplayTests`.

## Global Constraints

- Nothing under `src/` changes.  No file of `tests/SqlSource.Tests.Postgres` changes in this pull request.
- No test is lost or renamed.  The list of tests of `tests/SqlSource.Tool.Tests` after the work is the list before it, with `FixtureReplayTests.Case_ItsThreeFiles_AreReadFromItsFolder` in `FixtureCaseTests` and `FixtureReplayTests.Of_EngineWithNoFolder_IsEmpty` and `Of_EngineWithCases_GivesThemInNameOrder` in `FixtureCasesTests`, plus the tests of `NullabilityAnswersTests` and `FixtureCaseFilesTests`.
- A test's expectation does not change in value.  A literal becomes a name only for the same value; if two copies differ, they are not the same and both stay.
- A literal stays where it is what the test checks: `"sqlsource_1"` in the statement-name tests of `PostgresSessionTests` and in error texts; `"replay"` in `FixtureReplayTests`' assertion; `"postgres"` read from JSON, in the log's `engine`, in a command line, a database name, a dialect or a folder path.
- Helpers of the tool's tests are `internal`, at the root of `tests/SqlSource.Tool.Tests`, namespace `SqlSource.Tool.Tests`.  Helpers in `tests/SqlSource.Tool.Testing` are `internal`, namespace `SqlSource.Tool.Testing` (`SqlSource.Tool.Testing.Fixtures` under `Fixtures/`).
- An OID in an attribute such as `[InlineData]` stays a constant; an OID with no row in `PostgresRows` stays a literal.  No class of OIDs is added.
- The build treats warnings as errors with `AnalysisLevel` `latest-all`.  Fix what it reports, an unused `using` included; never suppress.  If a check is wrong, stop and tell the user.
- `ImplicitUsings` is off: a file names every namespace it uses.
- Comments say what a thing is for, in the plain declarative style of the files around them, with two spaces after a full stop.
- Every commit passes `./pre-commit-validation.sh` (it needs Docker running).  Run `./format.sh`, then the validation, then commit in a command of its own.  Never work around the hook.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- **`PreparesWithNoColumns` adds a call only when the answer types every parameter.**  Moved to `PostgresEventsTests`, it can add a `columns` call a test did not expect; a replay then leaves it in `exchange.Unused` or misses.  Task 1 runs `PostgresEventsTests` and asserts nothing new is unused.
- **Static initialisation order in `PostgresTables`.**  `UsersId` read before `Users` is initialised is a `NullReferenceException` in a type initialiser, which fails every test that touches the class.  Task 3 declares it after `Users` and runs the whole project.
- **A statement name the product sends that no test still spells.**  If every `"sqlsource_1"` became `Statement(1)`, a change of the product's prefix would pass.  Task 1 keeps the literal in the statement-name tests and checks it is there.
- **Two copies that look alike and differ.**  `DatabaseRunsTests`' `Awkward` is another text; `PostgresRelationsTests`' `Member` sets a schema; `PlanPartitionsTests`' `Append` has an `Outer` child.  Tasks 3 and 4 list what stays.
- **A test dropped in a mass edit.**  Task 5 compares the list of tests with the one Task 1 saves.

---

### Task 1: The session's script once (item 1)

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/PostgresScript.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs:176-260, 543-548, 2074-2085` and every call of those builders
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs` (the callers of `Prepared`)

**Interfaces:**
- Produces, on `PostgresScript`:
  - `public static string Statement(int number)`, which is `$"sqlsource_{number}"`
  - `public PostgresScript Prepares(int number, string sql, string[] types, PostgresPrepareAnswer answer)`, `prepare` alone
  - `public PostgresScript PreparesWithNoColumns(int number, string sql, string[] types, PostgresPrepareAnswer answer)`, today's `Prepares`
  - `public PostgresScript Columns(int number, int parameters, params PostgresColumnRow[] columns)`
  - `public PostgresScript Frees(int number, PostgresDeallocateAnswer? answer = null)`
  - `public PostgresScript Checks(string sql, (string Name, string Type)[] parameters, params PostgresColumnRow[] columns)`
  - `public PostgresScript Explains(int number, int parameters, PostgresExplainAnswer answer)`
  - `public PostgresScript PlansNothing(int number, int parameters)`
  - `public PostgresScript Resolves(long[] oids, params PostgresTypeRow[] rows)`
  - `public PostgresScript Tables(long[] oids, params PostgresTableRow[] tables)`
  - `public PostgresScript Spells(params (long Oid, int Modifier, string FormatType)[] rows)`
  - `public PostgresScript Partitions(PostgresPartitionsRequest request, PostgresPartitionsAnswer answer)`
  - `public PostgresScript Relations(PostgresRelationsRequest request, PostgresTablesAnswer answer)`
  - `public static PostgresDeallocateAnswer Freed { get; }`, `public static PostgresExplainAnswer NoPlan { get; }` (`XX000`, `"no plan in this test"`), `public static PostgresExplainAnswer PlanOf(PlanNode root)`, `public static PostgresCheckAnswer Agreeing(params PostgresColumnRow[] columns)`

- [ ] **Step 1: Save the list of tests as it is**

```bash
dotnet build tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj
dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --no-build --list-tests | grep '^  SqlSource' | sort > "${TMPDIR:-/tmp}/td-0054-pr1-before.txt"
```

Expected: the file has one line per test, about the count `dotnet test` reports.

- [ ] **Step 2: Move the builders onto `PostgresScript`**

Each method is the body of today's private static builder with `script` replaced by `this`, and every `$"sqlsource_{number}"` written as `Statement(number)`.  Keep their comments.  `PlanOf` uses `PostgresPlans.Text`.  `Agreeing` is the `PostgresCheckAnswer` that `Checks` builds today, each column with its `TypeOid` and `"a type"`.

- [ ] **Step 3: Use them in `PostgresSessionTests`**

Each `_ = Prepares(script, ...)` becomes `_ = script.PreparesWithNoColumns(...)`, each `PreparesOnly(script, ...)` becomes `script.Prepares(...)`, and the rest drop their first argument; a chain may replace a run of `_ =` lines where it reads better.  The raw `script.Answer("partitions", ...)` and `"relations"` become `Partitions` and `Relations`.  Delete the private builders and `Freed`.

- [ ] **Step 4: Use them in `PostgresEventsTests` and `PostgresErrorsTests`**

In `PostgresEventsTests`, each `.Answer("prepare" | "columns" | "deallocate" | "check" | "explain" | "types" | "tables" | "partitions" | "relations", ...)` becomes the fluent method that gives the same request and answer.  Its fields `Prepare`, `Deallocate`, `Columns`, `Check`, `Agrees`, its `Sees` and `NoPlan` go where every use is replaced; a field that a test asserts on (an event's `query` member, say) stays.  `new PostgresDeallocateAnswer(null)` is `PostgresScript.Freed`.  In `PostgresErrorsTests`, a caller of `Prepared("sqlsource_N", ...)` passes `PostgresScript.Statement(N)`; a `"sqlsource_N"` inside an expected error text stays.

- [ ] **Step 5: Check that the product's statement name is still spelled**

Run: `grep -n '"sqlsource_1"' tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`
Expected: at least one line inside a test of the statement name (`DescribeAsync_Query_AsksPrepareThenDeallocateForTheStatement` or its neighbours), so a change of the prefix fails a test.  If none is left, put the literal back in that test's script.

- [ ] **Step 6: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: PASS, with the same count as Step 1.

- [ ] **Step 7: Commit**

```bash
./format.sh
./pre-commit-validation.sh
```
```bash
git add tests/SqlSource.Tool.Tests && git commit -m "Write the session's script through PostgresScript in the tests of the session and of its events (TD-0054)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: One `NullabilityAnswers` (item 2, in the tool's tests)

**Files:**
- Create: `tests/SqlSource.Tool.Testing/NullabilityAnswers.cs`
- Create: `tests/SqlSource.Tool.Tests/NullabilityAnswersTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs` (99-100, 1120-1122, and the tuples)
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs` (1231-1232, and the tuples)

**Interfaces:**
- Produces, in `SqlSource.Tool.Testing.NullabilityAnswers`, each a `public static (bool? Nullable, string? Source) X { get; }` from `SidecarValues.NullableSource`:

| Name | Value |
|----|----|
| `NotNullCatalog` | `(false, Catalog)` |
| `NullableCatalog` | `(true, Catalog)` |
| `NullableOuterJoin` | `(true, OuterJoin)` |
| `NullableHeuristic` | `(true, Heuristic)` |
| `NotNullExpression` | `(false, Expression)` |
| `NullableExpression` | `(true, Expression)` |
| `NotNullSetOperation` | `(false, SetOperation)` |
| `UnknownNoOrigin` | `(null, NoOrigin)` |
| `UnknownView` | `(null, View)` |
| `UnknownForeignTable` | `(null, ForeignTable)` |

- and `public static (bool? Nullable, string? Source)[] Of(IEnumerable<SidecarColumn> columns)`, each column's `(Nullable, NullableSource)` in order.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Of_Columns_AreTheirNullabilityAndSourceInOrder() =>
    NullabilityAnswers
        .Of([ColumnWith(false, SidecarValues.NullableSource.Catalog), ColumnWith(null, SidecarValues.NullableSource.NoOrigin)])
        .ShouldBe([NullabilityAnswers.NotNullCatalog, NullabilityAnswers.UnknownNoOrigin]);
```

`ColumnWith(bool? nullable, string? source)` is a private helper of the test that builds a `SidecarColumn` with those two and any type.

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.NullabilityAnswersTests"`
Expected: FAIL to build, `NullabilityAnswers` does not exist.

- [ ] **Step 3: Write `NullabilityAnswers`**

- [ ] **Step 4: Run it to see it pass**

Same command.  Expected: PASS.

- [ ] **Step 5: Move the two classes onto it**

Each literal tuple of nullability in `NullabilityWalkTests` and `PostgresSessionTests` becomes its name, through `using static SqlSource.Tool.Testing.NullabilityAnswers;`.  `NullabilityWalkTests`' `FromBranches` and `NoOrigin` go.  Each class's `AnswersOf` stays as one line over `NullabilityAnswers.Of`: `outcome.Columns.Select(static walked => walked.Column)` and `description.Columns.ShouldNotBeNull()`.  `PostgresSessionTests`' `[InlineData(..., false, "record")]` is a pseudo-type and stays.

- [ ] **Step 6: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: PASS.

- [ ] **Step 7: Commit**

`./format.sh`, `./pre-commit-validation.sh`, then commit `tests/SqlSource.Tool.Testing` and `tests/SqlSource.Tool.Tests` as "Name the nullability answers of the walk's and the session's tests once (TD-0054)".

### Task 3: Rows, plans and requests (item 3)

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/PostgresTables.cs`, `PostgresRows.cs`, `PostgresPlans.cs`
- Create: `tests/SqlSource.Tool.Tests/PostgresRequests.cs`
- Create: `tests/SqlSource.Tool.Testing/Typmods.cs`
- Modify the callers: `Describing/Postgres/PostgresSessionTests.cs`, `PostgresEventsTests.cs`, `PostgresErrorsTests.cs`, `ParameterPlaceholdersTests.cs`, `PostgresColumnsTests.cs`, `PostgresTypesTests.cs`, `CheckVerdictTests.cs`, `ColumnOriginTests.cs`, `ColumnFacetsTests.cs`, `ColumnTypesTests.cs`, `Walk/NullabilityWalkTests.cs`, `Walk/PlanPartitionsTests.cs`, `Walk/PlanInstancesTests.cs`, `Walk/PlanRelationsTests.cs`

**Interfaces:**
- Produces:
  - `PostgresTables.ExpressionOf(int ordinal, string name, long typeOid, int? length = null, int? precision = null, int? scale = null)`, a `PostgresColumnRow` with table 0 and attnum 0
  - `PostgresTables.UsersId { get; }`, `ColumnOf(0, "id", PostgresRows.Int4.Oid, Users, 1)`, declared after `Users`
  - `Typmods.Varchar254 = 258`, `Typmods.Varchar10 = 14`, `Typmods.Numeric6Scale2 = 393222`, `public const int`, with a comment that a length's modifier is the length plus 4 and a numeric's is `(precision << 16 | scale) + 4`
  - `PostgresRows.EmailT254 { get; }`, `EmailT with { Typtypmod = Typmods.Varchar254 }`
  - `PostgresPlans.Append(params PlanNode[] members)`, `Over("Append", Scan("x")) with { Plans = members }`, which does not mark its children
  - `PostgresPlans.Member(PlanNode scan)`, the scan with `ParentRelationship = "Member"`
  - `PostgresPlans.Member(string alias, string relation, params string[] output)`, `Member(Scan(alias, output) with { RelationName = relation })`
  - `PostgresRequests.Of(string sql, params SqlQueryParameter[] parameters)`, a `DescribeRequest` named `"Q"`; `PostgresRequests.Inferred(string name)`; `PostgresRequests.Declared(string name, string type)`

- [ ] **Step 1: Add the helpers**

- [ ] **Step 2: Rows**

`PostgresSessionTests`' `Expression` goes and its 52 calls use `ExpressionOf`; so do the inline expression rows `new(ordinal, name, oid, 0, 0, ...)` of `ColumnOriginTests`, `PostgresColumnsTests`, `ColumnFacetsTests`, `ColumnTypesTests`, `PostgresEventsTests` and `NullabilityWalkTests`.  The 17 `ColumnOf(0, "id", 23, <Users>, 1)`, through `PostgresTables.Users` or a local `users` of it, become `PostgresTables.UsersId`; `PostgresEventsTests`' `IdColumn` and `UserId` go.  The other `ColumnOf(..., "id", 23, ...)` name another table or ordinal and stay.  In these rows and in `ColumnOf` and `Attribute` arguments, a type OID that has a row becomes `PostgresRows.X.Oid`; `CheckVerdictTests`' 16396 becomes `PostgresTables.Users.Oid`.

- [ ] **Step 3: Modifiers**

`PostgresSessionTests`' and `PostgresTypesTests`' `EmailT254` and `PostgresTypesTests`' `Varchar254` go for `PostgresRows.EmailT254` and `Typmods`.  The bare 258, 14 and 393222 that are modifiers, in `PostgresSessionTests` and `PostgresColumnsTests`, become `Typmods`.

- [ ] **Step 4: Plans**

`PlanPartitionsTests`, `PlanInstancesTests` and `PlanRelationsTests` lose `Append` and `Member`; `PlanInstancesTests`' `Partition` becomes `Member(alias, relation, output)`.  `PlanRelationsTests`' members are `Member(Scan(alias) with { RelationName = relation })`, and its one schema other than `public` is set with `with`, as in `Member(Scan("i") with { RelationName = "invoices", Schema = "billing" })`.  `NullabilityWalkTests`' `AppendOf(a, b)` becomes `Append(Member(a), Member(b))`.  The inline `Over("Append", Scan("x")) with { Plans = ... }` of `PostgresSessionTests` and `PostgresEventsTests` become `Append(...)`.  `PostgresEventsTests` has a private `Member(LogMember[], string)`, which hides the plan's: call the plan's as `PostgresPlans.Member`.

- [ ] **Step 5: Requests**

`PostgresSessionTests`' `Request`, `Inferred` and `Declared`, the same of `ParameterPlaceholdersTests`, and `PostgresErrorsTests`' `QueryOf`, `Inferred` and `Declared` go for `PostgresRequests`, through `using static SqlSource.Tool.Tests.PostgresRequests;` where a class calls them often.  `PostgresErrorsTests`' `Query(params SqlQueryParameter[])` stays as `=> PostgresRequests.Of("SELECT 1", parameters)`.  `ParameterPlaceholdersTests`' `List` is no request and stays.

- [ ] **Step 6: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: PASS, with the count of Task 1's Step 1 plus Task 2's test.

- [ ] **Step 7: Commit**

`./format.sh`, `./pre-commit-validation.sh`, then commit as "Build the rows, plans and requests of the PostgreSQL tests in one place each (TD-0054)".

### Task 4: Small duplicates (item 8, in the tool's tests)

**Files:**
- Create: `tests/SqlSource.Tool.Tests/CaptureInputs.cs`
- Create: `tests/SqlSource.Tool.Testing/Fixtures/FixtureCaseFiles.cs`
- Create: `tests/SqlSource.Tool.Tests/Fixtures/FixtureCaseFilesTests.cs`, `tests/SqlSource.Tool.Tests/Fixtures/FixtureCasesTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/PostgresScript.cs:22`
- Modify: `Describing/Capture/ReplayingExchangeTests.cs`, `CapturingExchangeTests.cs`, `CaptureJsonTests.cs`, `Describing/Describers/DescriberTests.cs`, `Describing/DatabaseRunsTests.cs:31`, `Fixtures/FixtureReplayTests.cs`, `Fixtures/FixtureCaseTests.cs`, `Fixtures/DescriberFixtureTests.cs`, `Fixtures/WalkMatrixTests.cs:41`, `tests/SqlSource.Tool.Testing/CliRun.cs:35`

**Interfaces:**
- Produces:
  - `CaptureInputs.GetUser`, `new DescribeRequest("GetUser", "SELECT 1;", EquatableArray<SqlQueryParameter>.Empty)`; `CaptureInputs.AwkwardSql`, `"SELECT '<é>' AS \"a\"\r\nFROM t"`
  - `FixtureCaseFiles.Create(string root, string engine, string name, string query)`, which writes `<root>/<engine>/<name>/query.sql` and returns the `FixtureCase`
  - `FixtureCaseFiles.WriteCaptureAsync(FixtureCase fixture, DescribeCapture capture, CancellationToken cancellationToken)`, which writes `capture.json` by `CaptureJson.Write`
  - `FixtureCaseFiles.WriteRecordingAsync(FixtureCase fixture, DescribeCapture capture, string expected, CancellationToken cancellationToken)`, which writes `capture.json` and `expected.json`

- [ ] **Step 1: Write the failing test of the writer**

```csharp
[Fact]
public async Task WriteRecordingAsync_Case_IsReadBackAsItsThreeFiles()
{
    using var folder = new TempFolder();
    var token = TestContext.Current.CancellationToken;
    var fixture = FixtureCaseFiles.Create(folder.Path, SidecarValues.Engine.Postgres, "typed", "SELECT 1;");

    await FixtureCaseFiles.WriteRecordingAsync(fixture, new DescribeCapture(SidecarValues.Engine.Postgres), "{}", token);

    var read = FixtureCases.Of(SidecarValues.Engine.Postgres, folder.Path).ShouldHaveSingleItem();
    read.Name.ShouldBe(fixture.Name);
    read.Query.ShouldBe("SELECT 1;");
    read.Expected.ShouldBe("{}");
    CaptureJson.Read(read.Capture).Calls.ShouldBeEmpty();
}
```

Use what `TempFolder` and `CaptureJson` name the folder's path and the read; the assertions stay.

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Fixtures.FixtureCaseFilesTests"`
Expected: FAIL to build, `FixtureCaseFiles` does not exist.

- [ ] **Step 3: Write `FixtureCaseFiles`, and run the test to see it pass**

- [ ] **Step 4: Use the writer**

`FixtureReplayTests`' `Case`, `Write` and the writing half of `RecordAsync`, and `DescriberFixtureTests`' three lines at 164-166, use `FixtureCaseFiles`.

- [ ] **Step 5: Move the fixture facts**

`FixtureReplayTests.Case_ItsThreeFiles_AreReadFromItsFolder` moves to `Fixtures/FixtureCaseTests.cs`; `Of_EngineWithNoFolder_IsEmpty` and `Of_EngineWithCases_GivesThemInNameOrder` move to a new `Fixtures/FixtureCasesTests.cs`, a `public sealed class` that disposes its `TempFolder`.  The names stay.

- [ ] **Step 6: The rest of the duplicates**

- `CaptureInputs` replaces `GetUser` in `ReplayingExchangeTests`, `CapturingExchangeTests` and `DescriberTests`, and `Awkward` in `CaptureJsonTests` and `ReplayingExchangeTests`.  `DatabaseRunsTests`' `Awkward` is another text and stays.
- `PostgresScript.Connection` is `FixtureReplay.Connection`; `ReplayingExchangeTests`' bare `"replay"` is `FixtureReplay.Connection`.
- `SidecarValues.Engine.Postgres` replaces `"postgres"` in `new DescribeCapture(...)`, `new FixtureCase(...)` and `FixtureCases.Of(...)` of the files above.  The literals of the Global Constraints stay.

- [ ] **Step 7: Run the tests**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj`
Expected: PASS.

- [ ] **Step 8: Commit**

`./format.sh`, `./pre-commit-validation.sh`, then commit as "Share the capture tests' inputs and the fixture case writer, and name the postgres engine once (TD-0054)".

### Task 5: Documents, the list of tests, and the plan

**Files:**
- Modify: `tests/AGENTS.md`, `docs/tech-debt/TD-0054-duplicated-and-uneven-patterns-in-the-postgresql-tests.md`, `docs/tech-debt/README.md:44`
- Add: this plan

- [ ] **Step 1: Compare the list of tests**

```bash
dotnet build tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj
dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --no-build --list-tests | grep '^  SqlSource' | sort > "${TMPDIR:-/tmp}/td-0054-pr1-after.txt"
diff "${TMPDIR:-/tmp}/td-0054-pr1-before.txt" "${TMPDIR:-/tmp}/td-0054-pr1-after.txt"
```

Expected: the three moved facts under their new classes, and the new tests of `NullabilityAnswersTests` and `FixtureCaseFilesTests`.  Nothing else.

- [ ] **Step 2: `tests/AGENTS.md`**

The bullet on `tests/SqlSource.Tool.Testing` names `NullabilityAnswers`, `Typmods` and `FixtureCaseFiles` (under `Fixtures/`).  The bullet "Tests mirror the source folders" names `PostgresRequests.cs` and `CaptureInputs.cs` among the helpers at the root.

- [ ] **Step 3: TD-0054**

Rewrite the item to what remains, as TD-0039 was after its first pull request: the Problem says items 1 and 3, and the parts of 2 and 8 in `tests/SqlSource.Tool.Tests`, are resolved, and keeps the item's numbers for the rest (2 and 8 in `tests/SqlSource.Tests.Postgres`, 4, 5, 6, 7).  The Proposed fix points at the spec for pull requests 2 and 3.  The row in `docs/tech-debt/README.md` describes what remains.

- [ ] **Step 4: Commit**

`./format.sh`, `./pre-commit-validation.sh`, then commit the three documents and this plan as "Rewrite TD-0054 to what remains after the tool's test helpers".

- [ ] **Step 5: Open the pull request**

Ask the user first.  Then bring the branch up to date with `main`, push, and open it against `main` if TD-0055's pull request has merged; otherwise against `claude/two-container-test-classes-debt-290ba2`.  Title "TD-0054, 1 of 3: the helpers of the tool's tests".  Turn on Auto-fix.
