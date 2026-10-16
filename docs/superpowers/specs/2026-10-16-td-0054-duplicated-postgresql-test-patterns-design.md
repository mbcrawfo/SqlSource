# TD-0054, duplicated and uneven patterns in the PostgreSQL tests - design

Date: 2026-10-16

Resolves [TD-0054](../../tech-debt/TD-0054-duplicated-and-uneven-patterns-in-the-postgresql-tests.md).  The item lists eight findings of the maintainability review of phase 3 in `tests/SqlSource.Tool.Tests/Describing/Postgres/`, `tests/SqlSource.Tests.Postgres/` and the helpers they share.  This document says what each becomes, in which pull request, and records the decisions the item left open.  The numbers 1 to 8 below are the item's own.

The work starts from `c26bd5f`, which closes TD-0055 and adds `tests/SqlSource.Tests.Postgres/Describer/ScratchSchema.cs`.  That commit is on `claude/two-container-test-classes-debt-290ba2` and its own pull request; this work takes `ScratchSchema` as it is.

## Goal

Each helper, constant and fixture text the item names has one definition, each large class covers one subject, and a failing theory row is named by its case.  A change to the statement name, to a nullability source or to the users table then touches one place in the tests, and the first test of a second engine's describer finds shared helpers to start from.

Success is:

- Nothing under `src/` changes.
- No test is lost or weakened: the test cases before and after are the same, apart from the class a test is in, its name (item 6), and theory rows that gain a label (item 7).
- The container tests pass on the default image and on `postgres:14.24` before each push, as `tests/AGENTS.md` asks.
- Every test class of the repository is `sealed`.
- When the last pull request merges, `docs/tech-debt/TD-0054-*.md` and its row are gone, and no file outside `docs/superpowers/` names the id.

Not in scope: a missing test; a helper for a duplicate the item does not name; `WalkMatrixTests`' string-keyed nullability (`"false catalog"`), which is the matrix's own case format.

## What the review found

All eight findings hold at `c26bd5f`.  Counts that differ from the item, none in a way that changes the fix:

| Item | The item says | Found |
|----|----|----|
| 1 | `"sqlsource_1"` 23 times in `PostgresEventsTests` | 22 to 24 by how it is counted; one is inside an error text.  `PostgresErrorsTests` writes `"sqlsource_"` 27 more times through its own `Prepared(...)`, which the item misses |
| 2 | About two hundred tuples | About 224, counting named fields; 121 of them in `NullabilityWalkTests`.  The tuple is `(bool?, string?)`: `(null, "no-origin")` appears 33 times |
| 3 | `users.id` as `ColumnOf(0, "id", 23, Users, 1)` 23 times | 23 matches of `ColumnOf(..., "id", 23, ...)`, of which 17 are `Users` at ordinal 0 |
| 3 | `Append` and `Member` in three classes | `Append` is the same in all three; `Member` has three incompatible signatures |
| 4 | `NpgsqlDataSource.Create(postgres.ConnectionString)` 27 times | 27, 10 of them in `ColumnTests` |
| 4 | `ShouldDescribeAsync` in three classes | Four: `ParameterTests` has one that takes a request and returns the parameters |
| 6 | Container tests start `Describe_` | 177 of them, but 7 test the `describe` command, 11 `Explain_` tests read what the server prints, and one `Describe_` of `SetOperationTests` describes nothing |

Where a proposed fix does not hold as written:

- **`PostgresRows.EmailT254` and a `Typmods` class (3).**  `tests/SqlSource.Tests.Postgres/Describer/ModifierCallTests.cs` writes 258 too and cannot see the tool's tests.  `Typmods` goes in `tests/SqlSource.Tool.Testing`; `PostgresRows.EmailT254` stays in the tool's tests and uses it.
- **`PostgresPlans.Member` (3).**  There is no one `Member` today.  It becomes two overloads, and the schema of `PlanRelationsTests`' third form is set with `with`.
- **`Recording()` on `Describer/Sessions.cs` (4).**  It reads an environment variable and has nothing to do with a session, and one copy is in `Realistic/Describer/`.  It goes in its own small class instead.
- **Rename `CheckVerdictTests` once the container class is told apart (6).**  The two `NpgsqlCheckTests` would be in different projects and namespaces, which nothing confuses.  The rename happens with no change to the container class.
- **Start every container test `DescribeAsync_` / `ExplainAsync_` (6).**  The prefix names what a test calls; some call the `describe` command or the server's `EXPLAIN`.  See item 6.

## Decisions

| Question | Choice | Whose |
|----|----|----|
| Scope | All eight items; TD-0054 is closed by the last pull request | The user's |
| Test names | The full method name: `DescribeAsync_`, `ExplainAsync_`, as `tests/AGENTS.md`'s `Method_Scenario_Outcome` says | The user's |
| Sealing | Every test class in the repository is `sealed`, not only those of the item, and `tests/AGENTS.md` says so | The user's |
| TD-0055 | Already fixed on its own branch; this work builds on it | The user's |
| TD-0053, the describer's own duplicates | Lands after this work.  The helpers here hold the call names (`"prepare"`, `"columns"`) and plan nodes (`"Append"`, `"Member"`) as literals in one place each, which TD-0053 swaps for its constants; if TD-0053 splits `PostgresSession`, it re-splits the session's test classes to mirror it | The user's |
| Shape of the work | One spec and three pull requests, merged in order: the tool's tests (1, 2, 3, 8 in `SqlSource.Tool.Tests` and `SqlSource.Tool.Testing`), the container tests (4, and 2 and 8 in `SqlSource.Tests.Postgres`), the structure (5, 6, 7).  Each rewrites TD-0054 to what remains, keeping the item's numbers; the last deletes it | My call, as TD-0039 did it |
| A prefix that is not a C# method | A test of the `describe` command keeps `Describe_`, and one of the server's `EXPLAIN` keeps `Explain_`: the prefix names what is called | My call |
| Two test classes of one name in two projects | Allowed; the folder and namespace tell them apart | My call |
| How split classes share helpers | Static helper classes at the root of `tests/SqlSource.Tool.Tests`, as `tests/AGENTS.md` asks of the tool's helpers.  No base class | My call |
| How small a split class may be | About six tests; a smaller subject joins a neighbour | My call |

## Pull request 1: the helpers of the tool's tests (items 1, 2, 3 and 8, in `SqlSource.Tool.Tests` and `SqlSource.Tool.Testing`)

Every helper of the tool's tests stays at the root of `tests/SqlSource.Tool.Tests`, in the namespace `SqlSource.Tool.Tests`.  A helper that the container tests will need in pull request 2 goes in `tests/SqlSource.Tool.Testing`, which both projects see.  No test moves and none is renamed.

### The session's script once (1)

- `PostgresScript` gains fluent instance methods that add the call and return `this`, replacing the private static builders of `PostgresSessionTests`: `Prepares`, `Columns`, `Frees`, `Checks`, `Explains`, `PlansNothing`, `Resolves`, `Tables` and `Spells`, with the parameters they take today minus the script.  `PlanOf(PlanNode)` becomes a static on `PostgresScript` or `PostgresPlans`.
- Today's `Prepares` also answers an empty `columns` when the prepare's parameter types cover the request.  It keeps that behaviour under a name that says so, and `PreparesOnly` becomes `Prepares`.  The builder picks the name.
- `PostgresScript.Statement(int number)` is `$"sqlsource_{number}"`.  The fluent methods take the number, as the builders do today.
- `Partitions` and `Relations` are added, since `PostgresEventsTests` and `PostgresSessionTests` answer those calls with a raw `Answer` today.
- `PostgresEventsTests` uses the fluent methods in place of its inline `.Answer(...)` calls and its fields `Prepare`, `Deallocate`, `Columns`, `Check`, `Agrees`, `Sees` and `NoPlan`, where they give the same calls.  `PostgresErrorsTests`' `Prepared(...)` builds its statement names with `Statement(n)`.
- The literal `"sqlsource_1"` stays where it is what a test checks: the statement-name tests of `PostgresSessionTests`, and error texts.

### One `NullabilityAnswers` (2)

`tests/SqlSource.Tool.Testing/NullabilityAnswers.cs` holds named `(bool? Nullable, string? Source)` tuples built from `SidecarValues.NullableSource`, and `Of(IEnumerable<SidecarColumn>)`, the projection that is written seven times.

- The names are `{NotNull|Nullable|Unknown}{Source}`, for example `NotNullCatalog`, `NullableOuterJoin`, `NullableHeuristic`, `UnknownNoOrigin`, `UnknownView`, `NotNullSetOperation`.  One name for each pair the tests use.  This rules out the clashes the classes have today: `ScanTests`' `Nullable`, `ExpressionTests`' `NotNull` for an expression, and `Unknown` beside `NoOrigin` for the same tuple.
- This pull request moves `NullabilityWalkTests` and `PostgresSessionTests` onto it, and pull request 2 moves the five container classes.  Their local names and projections go.
- `(false, "record")` in `PostgresSessionTests` is an `InlineData` of a pseudo-type, not a nullability, and stays.

### Rows, plans and requests (3)

- `PostgresTables.ExpressionOf(ordinal, name, typeOid, length = null, precision = null, scale = null)` is `PostgresSessionTests`' `Expression` and the 16 inline expression rows.
- `PostgresTables.UsersId` is the 17 `ColumnOf(0, "id", 23, Users, 1)` of `PostgresTables.Users`.  The other six `ColumnOf(..., "id", 23, ...)` are other tables or ordinals and stay.  `PostgresEventsTests`' `IdColumn` and `UserId` go.
- A raw type OID in a row the tests build, an expression row or an argument of `ColumnOf`, becomes `PostgresRows.X.Oid`; each one used there has a row today.  `CheckVerdictTests`' table OID 16396 becomes `PostgresTables.Users.Oid`.  An OID in an attribute such as `[InlineData]` has to be a constant and stays one; an OID with no row, such as a domain's or one a test of types is about, stays a literal or a local constant.  No new class of OIDs is added (my call).
- `Typmods`, in `tests/SqlSource.Tool.Testing`, names the modifiers: 258 (`varchar(254)`), 14 (`varchar(10)`) and 393222 (`numeric(6,2)`).  `PostgresRows.EmailT254` is the one row of `PostgresTypesTests` and `PostgresSessionTests`.
- `PostgresPlans.Append(params PlanNode[])`, which does not mark its children, since `PlanPartitionsTests` has a case with an `Outer` child; `PostgresPlans.Member(PlanNode)`, so `AppendOf(a, b)` becomes `Append(Member(a), Member(b))`; and `PostgresPlans.Member(alias, relation, params string[] output)`.  `PlanPartitionsTests`, `PlanInstancesTests`, `PlanRelationsTests`, `NullabilityWalkTests`' `AppendOf` and the inline `Over("Append", Scan("x")) with` of `PostgresSessionTests` and `PostgresEventsTests` use them.  `PlanRelationsTests`' one non-default schema is set with `with`.
- `PostgresRequests`, at the root, holds `Of(sql, params SqlQueryParameter[])`, `Inferred(name)` and `Declared(name, type)`, which are copied in `PostgresSessionTests`, `PostgresErrorsTests` and `ParameterPlaceholdersTests`.  `PostgresErrorsTests`' `Query(...)` keeps its fixed SQL as a one-line local over `PostgresRequests.Of`.  `tests/SqlSource.Tests.Postgres/Describer/Requests.cs` lexes its SQL, has another shape, and stays.

### Small duplicates (8, in the tool's tests)

- `GetUser` and `Awkward` of `ReplayingExchangeTests`, `CapturingExchangeTests`, `CaptureJsonTests` and `DescriberTests` go in one helper at the root, under a name other than `GetUser`, which `SqlTexts.GetUser` already has.  `DatabaseRunsTests`' `Awkward` is a different text and stays.
- `PostgresScript.Connection` is `FixtureReplay.Connection`, and `ReplayingExchangeTests`' bare `"replay"` uses it.  `FixtureReplayTests`' assertion of the literal stays.
- The engine `"postgres"` becomes `SidecarValues.Engine.Postgres` in `new DescribeCapture(...)`, `new FixtureCase(...)` and `FixtureCases.Of(...)`.  A literal stays where it is what the test reads or writes: JSON that is parsed, the log's `engine`, a command line, a database name, a dialect, a folder name.
- `FixtureReplayTests`' fact about `FixtureCase` moves to `Fixtures/FixtureCaseTests.cs`, and its two facts about `FixtureCases.Of` to a new `Fixtures/FixtureCasesTests.cs`.
- One writer of a fixture case's files, beside `FixtureCase` in `tests/SqlSource.Tool.Testing/Fixtures/`, replaces the copies in `FixtureReplayTests`, `DescriberFixtureTests` and, in pull request 2, `tests/SqlSource.Tests.Postgres/Describer/FixtureCasesTests.cs`.

## Pull request 2: the container tests (item 4, and items 2 and 8 in `SqlSource.Tests.Postgres`)

### What `PostgresContainer` gains (4)

- `ExecuteAsync(sql)` and `ScalarAsync(sql)` are the nine private `RunAsync`, `ExecuteAsync` and `ScalarAsync`, which differ only in what they return.  They open a data source of `ConnectionString` and use the test's cancellation token.  `DescribeCommandTests`' `ExecuteAsync`, which returns a scalar, becomes `ScalarAsync`.  `Realistic/RealisticDatabase.ScalarAsync` takes another connection and stays.
- The other `NpgsqlDataSource.Create(postgres.ConnectionString)` that run one statement use them.  Those that hold a transaction or several commands keep their data source.
- `ColumnTests` and `WalkTests` lose `ServerMajorAsync` and read `postgres.Major`.
- `SkipBelow(int major, string reason)` is the `Assert.SkipWhen(... < major, ...)` of those two classes.  Its five uses of "PostgreSQL before 18 has no old and new in RETURNING." share one constant.
- `RecordedMajors = [14, 18]` moves from `Realistic/RealisticCase.cs` to `PostgresContainer`, and `RealisticCase` reads it from there.  `SkipUnlessRecordedMajor(string what)` is the four skips of "... pinned for PostgreSQL 14 and 18 alone" in `SetOperationTests` and `PlanOutputTests`, and `RealisticDescribe`'s `SkipMessage`.
- `PostgresContainerTests` covers `SkipBelow` and `SkipUnlessRecordedMajor`, which decide something.

### What `Describer/Sessions.cs` gains (4)

- `ShouldDescribeAsync(connection, sql, exchange = null)` and an overload that takes a `DescribeRequest`: the four copies, and `WalkedAsync`, `DescribedAsync` and `AnswersAsync`, which do the same before they project.  The projection is `NullabilityAnswers.Of`, which also moves `ColumnTests`, `WalkTests`, `ScanTests`, `ExpressionTests` and `SetOperationTests` off their tuples.
- `OpenAsync` takes a database, with `PostgresContainer.Database` as the default, so `RealisticDescribe` opens through it.  A second method returns the `PostgresSession` and asserts its type, replacing the `opened.Value.ShouldBeOfType<PostgresSession>()` and `ShouldNotBeNull()` that follow `OpenAsync` today.  `ModifierCallTests`, `ExplainTests` and `RelationsCallTests` test the live calls below a session, build a `PostgresConnection` and `PostgresLive` on purpose, and stay as they are.
- One method describes through a `CapturingExchange` and returns the result with its capture: the three lines of the ten capture sites in `ParameterTests`, `ColumnTests` and `SessionTests`.  It takes the exchange to wrap, `LiveExchange.Instance` by default.

### The rest of item 4

- `Recording.IsAsked(variable)`, in `tests/SqlSource.Tests.Postgres/Recording.cs`, is the `Recording` property of `FixtureCasesTests` and `RealisticDescriptionTests`, which read different variables.
- `SessionTests`' three exchanges that run a hook after one live call share one private `AfterLiveExchange`, which asks `LiveExchange.Instance` and then calls the hook.  `ThrowsAfterTheServerAnswered` differs and stays.
- `SessionTests` uses `Secrets.Marker`, and `PostgresConnectionTests` replaces its four `"s3cret-marker"` with it.
- The `PGPASSWORD` skip, its comment and its message are one method of `tests/SqlSource.Tool.Testing`, used by `SessionTests` and `PostgresConnectionTests`.

### Items 2 and 8 in the container tests

`ColumnTests`, `WalkTests`, `ScanTests`, `ExpressionTests` and `SetOperationTests` use `NullabilityAnswers`.  `ParameterTests`, `ColumnTests` and `SessionTests` write `SidecarValues.Engine.Postgres`, `FixtureCasesTests` loses its `Engine` constant, and `FixtureVersionTests` and `DescribeCommandTests` pass the constant to `FixtureCases.Of`.  `FixtureCasesTests` writes its case through the shared writer.

## Pull request 3: the structure (items 5, 6 and 7)

### Classes by subject (5)

`PostgresSessionTests`, after pull request 1 has moved its builders:

| Class | Subject |
|----|----|
| `PostgresSessionTests` | The major version, the version floor, and what is refused before anything is asked |
| `PostgresSessionStatementTests` | Statement names, prepare and deallocate, and `26000` and `42P05` as `SQLSRC236` |
| `PostgresSessionParameterTests` | Parameter types, OIDs asked once, pseudo-type parameters and the pseudo-type column (`SQLSRC229`) |
| `PostgresSessionColumnTests` | Columns, facets, tables and origins, a missing catalog or type, and spellings and modifiers |
| `PostgresSessionPlanTests` | The gate, explain, the planner's settings and the gated walk |
| `PostgresSessionRelationTests` | Partitions, the relations of set operations, and the table match |
| `PostgresSessionCheckTests` | The Npgsql check and bind names |

The fixtures that several of these share, `UngatedUsersId`, `GatedUsersId`, `DescribeUsersIdAsync` and the like, go in one static helper at the root.

`PostgresEventsTests`:

| Class | Subject |
|----|----|
| `PostgresEventsTests` | The events of prepare, types and deallocate, and their shapes |
| `PostgresEventsColumnTests` | The events of columns, tables and the check |
| `PostgresEventsRelationTests` | The events of partitions and relations, and of the table match |
| `PostgresEventsPlanTests` | The events of explain, of a node and of the walk |

Each `Describe_ThroughTheLogOfASession_*` test of removed secrets goes with its subject.  `Listing`, `ListingScript`, `DescribeAsync`, `NamesOf`, `Member` and the other fixtures they share go in one static helper at the root.

`NullabilityWalkTests` gives `NullabilityWalkSetOperationTests` its 25 tests of set operations, lines 1079 to 1699 at `c26bd5f`, with the fixtures only they use.  The constants `Int4`, `Int8`, `Text` and `Domain` there stay constants, since `[InlineData]` uses them, and `Text`, which hides `PostgresPlans.Text`, is renamed so it no longer does.  `Built` and `Expression`, which both classes use, go in a static helper at the root.  The set-operation cases that use the general fixtures stay.

### Names and modifiers (6)

- A test's prefix is what it calls.  The container tests that call `DescribeAsync` through `Sessions` or a session start `DescribeAsync_`: `ColumnTests`, `ExpressionTests`, `NpgsqlCheckTests`, `ParameterTests`, `ScanTests`, `SessionTests`, `TableMatchTests`, `WalkTests`, `FixtureCasesTests`, `SetOperationTests`, `RealisticDescriptionTests` and `RealisticNullabilitySafetyTests`.  The 25 `Describe_` of `PostgresEventsTests` become `DescribeAsync_` too.
- `DescribeCommandTests` and `ToolProcessTests` run the `describe` command and keep `Describe_`.  `PlanOutputTests` runs the server's `EXPLAIN` and keeps `Explain_`.  `SetOperationTests`' `Describe_NestedSetOperationWithANullableLeftmostBranch_ReturnsNull` only runs the query, and is named for what it calls; the builder picks the name.  `SqlMarkerReaderTests`' `Describe_` tests `SqlMarkerReader.Describe` and stays.
- `tools/record-realistic.sh` and `tools/AGENTS.md` name `Describe_Case_IsItsRecordedDescription`, and change with it.  So does TD-0050's mention of a `ColumnTests` method.  Specs and plans under `docs/superpowers/` are history and stay.
- Every test class is `sealed`: 68 in `SqlSource.Tests`, 1 in `SqlSource.Tests.RoslynFloor`, 49 in `SqlSource.Tool.Tests`.  The four `partial` classes stay `partial`.  Raw strings in `Generator/`, `TypeEmitterTests` and `AttributeReaderTests` hold `class` and `partial class` lines of generator input, which an edit must not touch.  No test class is a base class or has `protected` or `virtual` members.
- `CheckVerdictTests` becomes `NpgsqlCheckTests`, and the comment in `PostgresErrorsTests` that names it changes.
- No test checks that a test class is sealed: `TestLayoutTests` reads files, and a check by text would have to step over the raw strings of generator input (my call).  `tests/AGENTS.md` says: test classes are `sealed`; the prefix names what the test calls, the C# method in full or the command; a unit-test class and a container class may share a name.

### Labelled rows (7)

The eleven `TheoryData` of plans, eight in `NullabilityWalkTests` (or `NullabilityWalkSetOperationTests` after the split), two in `PlanPartitionsTests` and `PostgresSessionTests`' `PlansThatCannotBeUsed`, are keyed by a case name as `tests/SqlSource.Tool.Tests/Fixtures/WalkMatrixTests.cs` keys its cases: a dictionary of rows by name, a `TheoryData<string>` of its keys, and a theory that takes the name and reads its row.  `Unusable` repeats the reason `no-output`, so its keys say more than the reason.

## Testing

The work changes tests and nothing they test, so what holds it is that the same cases run and pass.

- Each pull request records `dotnet test --list-tests` for each project it touches, before and after.  Pull requests 1 and 2 leave the list the same, apart from the fixture facts that move in 1.  Pull request 3 changes the class and name of the tests it moves or renames, and the display names of labelled rows; the count of cases is the same.
- Every test passes, and `./pre-commit-validation.sh` passes, before each commit.  The container tests also run on `postgres:14.24` before each push.
- A helper that decides something gets a test of its own: `SkipBelow` and `SkipUnlessRecordedMajor` in `PostgresContainerTests`, `NullabilityAnswers.Of` and the case writer in the tool's tests.  A helper that only names a value, or adds a call to a script that a replay checks, gets none.

## Order and the TD-0055 branch

The branch of this work holds `c26bd5f`.  Pull request 1 touches no file of TD-0055's change, and can be opened with that branch as its base or after TD-0055 merges; pull request 2 touches the same `Describer/` classes and is opened only after TD-0055 has merged and the branch is brought up to date with `main`.  If that branch changes `ScratchSchema` in review, pull request 2 takes the change.

## Documents

- `tests/AGENTS.md` changes in pull request 1, which adds helpers at the root of the tool's tests and in `tests/SqlSource.Tool.Testing`; in pull request 2, for `Recording.cs`; and in pull request 3, for the rules of item 6 and the files it moves.
- `docs/tech-debt/TD-0054-*.md` and its row in `docs/tech-debt/README.md` are rewritten by pull requests 1 and 2 to what remains, and deleted by pull request 3.
- No new tech debt is expected.  What a pull request finds and does not fix gets its own item, as the core rules say.

## Left to the builder

- The names of the fluent methods beyond those above, and of the static helpers at the root.
- Which single-statement uses of `NpgsqlDataSource.Create` move to `ExecuteAsync` or `ScalarAsync`.
- The exact shape of the capture method of `Sessions`, and of `AfterLiveExchange`.
- The name of the `SetOperationTests` test that describes nothing.
