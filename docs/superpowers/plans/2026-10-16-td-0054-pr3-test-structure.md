# TD-0054, pull request 3: the structure of the PostgreSQL tests - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Items 5, 6 and 7 of TD-0054 are fixed and the item is closed: the three large classes are split by subject, every test's prefix names what it calls, every test class is `sealed`, and a theory row of a plan is named by its case.

**Architecture:** Test code alone moves.  The fixtures that split classes share go in three static helpers at the root of `tests/SqlSource.Tool.Tests` (no base class), each split class reading them with `using static`.  Renames and sealing are mechanical edits checked by comparing `--list-tests` before and after.  Nothing under `src/` changes.

**Tech Stack:** C# (`LangVersion` `preview`) on `net10.0`; xunit v3 on Microsoft.Testing.Platform with Shouldly.

**Spec:** `docs/superpowers/specs/2026-10-16-td-0054-duplicated-postgresql-test-patterns-design.md`, the section "Pull request 3" and the Decisions table.

**Branch:** this worktree's branch, `claude/happy-tesla-53ca00`, fast-forwarded to pull request 2's head (`claude/quirky-jones-193ea0`, `8f89d8c`).  Line numbers below are at `8f89d8c`.  No pull request is opened until https://github.com/mbcrawfo/SqlSource/pull/64 has merged, and none before 17:00 (the user's instruction); the branch is brought up to date with `main` before each push.

## Where the plan settles what the spec leaves open

- **The work is two pull requests, not one** (`AGENTS.md`, pull requests rule 3).  Sealing alone touches 118 files, and the whole of pull request 3 comes to 150 files with the rename counted once, at the limit with no room for a review's fix.  So:
  - **Pull request 3a** (Task 1), from `main` on a branch of its own, `claude/td-0054-sealed-generator-tests`: seals the 68 test classes of `tests/SqlSource.Tests` and the one of `tests/SqlSource.Tests.RoslynFloor`, and rewrites item 6 of TD-0054 to say so.  About 70 files.  It touches no file of pull request 2 or of 3b.
  - **Pull request 3b** (Tasks 2 to 7), this branch: everything else, about 81 files, and it closes TD-0054.  It is opened after 3a; when 3a merges, the update from `main` meets 3a's edit of the TD-0054 file this branch deletes, and the merge keeps the deletion.
- **The shared helpers** are `PostgresSessionCases` (the session's split classes), `PostgresEventCases` (the events') and `PostgresWalkColumns` (the walk's), each `internal static` at the root of `tests/SqlSource.Tool.Tests`, namespace `SqlSource.Tool.Tests`.  A fixture that only one split class uses stays in it, so `DescribeUsersIdAsync`, which the spec names among the shared ones, stays in `PostgresSessionPlanTests`, the only class that calls it.
- **The set operations' type constants become `Int4Oid`, `Int8Oid`, `TextOid` and `DomainOid`.**  Only `Text` hides something (`PostgresPlans.Text`, which the class calls), but renaming one of four would leave `TextOid` beside `Int4`.
- **`SetOperationTests`' query-only test is `ReturnsNullAsync_NestedSetOperationWithANullableLeftmostBranch_IsTrue`**, for the private `ReturnsNullAsync` it calls.
- **A labelled row's key is kebab-case and says what the plan is**, taken from the row's comment where it has one; keys are unique within their dictionary, which uses `StringComparer.Ordinal`.  `Unusable`'s keys are `no-output-of-an-append`, `fewer-outputs-than-columns`, `unknown-join-type`, `doubled-alias` and `grouping-sets`.

## Global Constraints

- Nothing under `src/` changes.
- No test is lost or weakened.  The cases of `--list-tests` before and after are the same, apart from moved and renamed classes, renamed methods and labelled row names; the count of cases is the same.
- A test moves with the comment above it, unchanged.  The line ranges of the tables below are a guide; the tests they name decide, and the counts of methods (73, 30 and 91) are checked after each move.  A moved test's body changes only where a name it reads moved to a helper or was renamed.
- `DescribeCommandTests` and `ToolProcessTests` keep `Describe_`; `PlanOutputTests` keeps `Explain_`; `SqlMarkerReaderTests` stays as it is.  The `Columns_`, `Spellings_` and `Relations_` tests of `PostgresEventsTests` call those methods of `PostgresEvents` and keep their names.
- The four `partial` test classes stay `partial` (`public sealed partial class`, the order `.editorconfig` sets).  An edit never touches a `class` line inside a raw string of generator input: those lines are indented, and the sealing edit matches only a declaration at the start of a line.
- Specs and plans under `docs/superpowers/` are history and are not edited for a rename.
- The build treats warnings as errors with `AnalysisLevel` `latest-all`.  Fix what it reports, an unused `using` included; never suppress.  `ImplicitUsings` is off.
- Comments say what a thing is for, in the plain declarative style of the files around them, with two spaces after a full stop.  `tests/AGENTS.md` keeps its maintenance footer and stays brief.
- Every commit passes `./pre-commit-validation.sh` (Docker running).  Run `./format.sh`, then the validation, then commit in a command of its own.  Before each push, `tests/SqlSource.Tests.Postgres` also passes with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; pull request bodies with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.

## Review Focus

- **A test dropped or duplicated by a move.**  A range copied twice compiles if the copies land in different classes.  Task 7 compares the sorted lists by method name with the class stripped, and the counts per project.
- **A static field read before it is set.**  A rows dictionary is a `static readonly` field, and static initializers run in textual order: one that reads a field declared below it gets `null`.  Each dictionary goes after every field it reads; a mistake fails every test of the class, which Task 5's run shows.
- **A `using static` that changes what a name binds to.**  `PostgresEventCases.Member(LogMember[], string)` beside `PostgresPlans.Member(...)`, and `PostgresWalkColumns.AnswersOf` beside a class's own: overloads from two `using static` merge.  Each split class's tests run, and a wrong binding is a build error or a failing assertion.
- **A rename that misses a mention.**  Task 6 greps the repository, outside `docs/superpowers/`, for every old class and method name.
- **A sealed class that something derives from.**  None does today; the build after Task 1 and Task 6 says so.

---

### Task 1: Pull request 3a, the sealed test classes of the generator's tests

**Files:**
- Modify: the 68 files of `tests/SqlSource.Tests` and the 1 of `tests/SqlSource.Tests.RoslynFloor` that declare a test class at the start of a line with no `sealed`
- Modify: `docs/tech-debt/TD-0054-duplicated-and-uneven-patterns-in-the-postgresql-tests.md`, item 6's first paragraph

- [ ] **Step 1: Make the worktree**

```bash
git worktree add -b claude/td-0054-sealed-generator-tests ../td-0054-sealed origin/main
```

Work in that folder for the rest of this task.

- [ ] **Step 2: Seal the classes**

In each file that `grep -rlE '^(public|internal) (partial )?class ' tests/SqlSource.Tests tests/SqlSource.Tests.RoslynFloor --include='*.cs'` lists, put `sealed` after the access modifier of a declaration that starts the line, before `partial` where there is one:

```bash
sed -i '' -E 's/^(public|internal) (partial )?class /\1 sealed \2class /' <file>
```

Then `grep -rnE '^(public|internal) (partial )?class ' tests/SqlSource.Tests tests/SqlSource.Tests.RoslynFloor --include='*.cs'` prints nothing, and `git diff --stat` shows 69 files with one line each.

- [ ] **Step 3: Rewrite item 6 of TD-0054**

Its sentence on sealing becomes: the test classes of `tests/SqlSource.Tests`, `tests/SqlSource.Tests.RoslynFloor` and `tests/SqlSource.Tests.Postgres` are `sealed`, and those of `tests/SqlSource.Tool.Tests` are not.  The rest of the item stays.

- [ ] **Step 4: Validate and commit**

Run `./format.sh`, then `./pre-commit-validation.sh`: every step passes.  Commit "Seal the test classes of the generator's tests (TD-0054)".  Push only when Task 7's rule for opening pull requests allows; the pull request is "TD-0054, 3 of 4: sealed test classes of the generator's tests", against `main`, with Auto-fix on.

### Task 2: Split `PostgresSessionTests` (item 5)

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`
- Create, beside it: `PostgresSessionStatementTests.cs`, `PostgresSessionParameterTests.cs`, `PostgresSessionColumnTests.cs`, `PostgresSessionPlanTests.cs`, `PostgresSessionRelationTests.cs`, `PostgresSessionCheckTests.cs`
- Create: `tests/SqlSource.Tool.Tests/PostgresSessionCases.cs`

**Interfaces:**
- Produces `internal static class PostgresSessionCases`, with the members moved from `PostgresSessionTests:1123-1129`:
  - `public const string UngatedUsersId`, `public const string GatedUsersId`, with their comment
  - `public static (bool? Nullable, string? Source)[] AnswersOf(QueryDescription description)`

- [ ] **Step 1: Save the lists of tests as they are**

```bash
dotnet build SqlSource.slnx
for p in Tool.Tests Tests.Postgres; do dotnet test --project tests/SqlSource.$p/SqlSource.$p.csproj --no-build --list-tests | grep '^  SqlSource' | sort > "${TMPDIR:-/tmp}/td-0054-pr3-$p-before.txt"; done
```

Expected: one line per case in each file.

- [ ] **Step 2: Move the tests**

Each class is `public sealed class`, in `SqlSource.Tool.Tests.Describing.Postgres`, with the class comment of `PostgresSessionTests` reworded to its subject and the `using`s it needs.  Ranges include the comment above each test.

| Class | Lines at `8f89d8c` | Tests |
|----|----|----|
| `PostgresSessionTests` | 23-157 | 9: `MajorVersionOf_`, the two `OpenAsync_`, and the six refused before anything is asked |
| `PostgresSessionStatementTests` | 158-398 | 9: from `Query_AsksPrepareThenDeallocateForTheStatement` to `UngatedQueryWithAnExpressionExplainAnswers26000_` |
| `PostgresSessionParameterTests` | 399-644, 1643-1702 | 10: from `DeclaredAndInferredParameters_` to `TypesThatFails_`, and `ColumnOfAPseudoType_` and `PseudoTypeOfAParameterAndOfAColumn_` |
| `PostgresSessionColumnTests` | 645-1019, 1950-2118 | 16: from `StatementWithColumns_` to `TypeOfAnOriginAttributeThatTypesLacks_`, and the four of spellings and modifiers |
| `PostgresSessionPlanTests` | 1020-1122, 1130-1176, 1420-1531 | 10: `DescribeUsersIdAsync` and from `QueryWithoutAGateWordWithAnExpression_` to `DescriberWithSettings_`, and from `GatedQueryWhoseJoinExtendsTheColumnsSide_` to `GatedStatementWithoutRows_` with `PlansThatCannotBeUsed` |
| `PostgresSessionRelationTests` | 1177-1419, 1532-1642 | 12: the two of partitions and their fixtures, the five of relations and the union's fixtures, and `Labels`, `DescribeLabelsAsync` and the five of the table match |
| `PostgresSessionCheckTests` | 1703-1949 | 7: from `CheckWhoseColumnsDiffer_` to `ParameterTypeWithADotInItsName_` |

Lines 1123-1129 go to `PostgresSessionCases`; the classes that read them add `using static SqlSource.Tool.Tests.PostgresSessionCases;`.  `PostgresSessionTests.cs` keeps only its range.

- [ ] **Step 3: Build and run the seven classes**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresSession*"`
Expected: the 73 methods pass, with as many cases as `PostgresSessionTests` had in Step 1's list.

- [ ] **Step 4: Validate and commit**

`./format.sh`, `./pre-commit-validation.sh`, then commit "Split PostgresSessionTests by subject (TD-0054)".

### Task 3: Split `PostgresEventsTests` and name its tests for `DescribeAsync` (items 5 and 6)

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs`
- Create, beside it: `PostgresEventsColumnTests.cs`, `PostgresEventsRelationTests.cs`, `PostgresEventsPlanTests.cs`
- Create: `tests/SqlSource.Tool.Tests/PostgresEventCases.cs`

**Interfaces:**
- Produces `internal static class PostgresEventCases`, with these members of `PostgresEventsTests` made `public` and their comments: `Query`, `PreparedSql`, `PreparedTypes`, `CheckedParameters`, `ColumnsOfQuery` (lines 16-30), `Member(LogMember[] members, string name)`, `NamesOf`, `DescribeAsync(PostgresScript, IRunLog, DescribeRequest? = null)` (37-46), `Listing`, `CountColumn`, `ListingScript()` (333-350)

- [ ] **Step 1: Move the tests**

| Class | Lines at `8f89d8c` | Tests |
|----|----|----|
| `PostgresEventsTests` | 48-331, 854-872 | 9: from `QueryWithAParameter_WritesPrepareTypesAndDeallocateInOrder` to the engine test of `ThroughTheLogOfASession_`, and `ThroughTheLogOfASession_RemovesTheSecretFromTheMessageOfAnError`, a prepare's error |
| `PostgresEventsColumnTests` | 32-35, 351-615 | 9: `Check`, and from `QueryWithColumns_` to `ThroughTheLogOfASession_RemovesTheSecretFromAColumnsNameAndItsOrigin` |
| `PostgresEventsRelationTests` | 616-853 | 7: the partitions, `Relations_`, `Ids`, `Whole`, `WholeScript` and the table match |
| `PostgresEventsPlanTests` | 873-1084 | 5: `Joined` and its fixtures, the gated walk, explain, the unusable plans and the secret of a plan |

Each class is `public sealed class` with `using static SqlSource.Tool.Tests.PostgresEventCases;`, and `using static SqlSource.Tool.Tests.PostgresPlans;` where it calls a plan builder.

- [ ] **Step 2: Rename the 25 `Describe_` tests to `DescribeAsync_`**

Every `public async Task Describe_` in the four files; `grep -c 'Task DescribeAsync_'` over them sums to 25 and `grep 'Task Describe_'` finds none.

- [ ] **Step 3: Build and run the four classes**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresEvents*"`
Expected: as many pass as `PostgresEventsTests` had in Task 2's list.

- [ ] **Step 4: Validate and commit**

`./format.sh`, `./pre-commit-validation.sh`, then commit "Split PostgresEventsTests by subject and name its tests for DescribeAsync (TD-0054)".

### Task 4: Move the set operations to `NullabilityWalkSetOperationTests` (item 5)

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs`
- Create: `Walk/NullabilityWalkSetOperationTests.cs`, `tests/SqlSource.Tool.Tests/PostgresWalkColumns.cs`

**Interfaces:**
- Produces `internal static class PostgresWalkColumns`, lines 16-74 and 100-101 of `NullabilityWalkTests` made `internal` or `public` as a static class needs: `Table`, `View`, `Foreign`, `TableA`, `TableB`, `Tables`, `internal sealed record Asked(PostgresTableRow? Of, int Attnum)`, `NotNull`, `Optional`, `OfView`, `OfForeign`, `Expression`, both `Built` overloads, and `AnswersOf(WalkOutcome outcome)`

- [ ] **Step 1: Move**

`NullabilityWalkSetOperationTests` (`public sealed class`, namespace `SqlSource.Tool.Tests.Describing.Postgres.Walk`) takes lines 1053-1673: its 25 tests, `SetU`, `SetO`, `SetTables`, `RunSet`, `AppendOf`, `Select`, `SetOp`, `CrossedExcept14`, `Flagged14`, `Inputs18`, `NestedExcept14`, `NestedExcept18` and the five `TheoryData` there.  Its constants become `Int4Oid`, `Int8Oid`, `TextOid` and `DomainOid`, every use with them; `Text(...)` there is `PostgresPlans.Text` again.  Both classes add `using static SqlSource.Tool.Tests.PostgresWalkColumns;`, and `NullabilityWalkTests` keeps `Run`, `Scope`, `RunAs`, `RunUngated`, `LeftJoin` and everything above line 1053 that is not in the helper.

- [ ] **Step 2: Build and run both classes**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Describing.Postgres.Walk.NullabilityWalk*"`
Expected: as many pass as `NullabilityWalkTests` had in Task 2's list, 25 methods of them in the new class.

- [ ] **Step 3: Validate and commit**

`./format.sh`, `./pre-commit-validation.sh`, then commit "Move the walk's set operations to NullabilityWalkSetOperationTests (TD-0054)".

### Task 5: Key the theory data of plans by case name (item 7)

**Files:**
- Modify: `Walk/NullabilityWalkTests.cs` (`SetOperations`, `Unusable`, `UnresolvableChildren`), `Walk/NullabilityWalkSetOperationTests.cs` (`BranchesThatMayBeNull`, `Appendrels`, `Intersects`, `Excepts`, `SetOpsOfAnotherShape`), `Walk/PlanPartitionsTests.cs` (`NoShape`, `NotOneTable`), `PostgresSessionPlanTests.cs` (`PlansThatCannotBeUsed`)

For each of the eleven, as `tests/SqlSource.Tool.Tests/Fixtures/WalkMatrixTests.cs:44-85` does:

- `private static readonly Dictionary<string, T> <Name>Rows = new(StringComparer.Ordinal) { ["key"] = row, ... };`, where `T` is the row's one value or a named tuple of its values: `(string Reason, string Plan)` for `Unusable`, `(string Plan, bool Answered)` for `Intersects` and `Excepts`, `(string? Plan, string? Error)` for `PlansThatCannotBeUsed`.  A row's comment stays above it.
- `public static TheoryData<string> <Name> => [.. <Name>Rows.Keys];`, the name the `[MemberData]` reads today.
- The theory takes `string name` and reads `<Name>Rows[name]` first, deconstructing a tuple into the locals its parameters were.

- [ ] **Step 1: Convert the eleven**, with the keys of the settlement above for `Unusable`.

- [ ] **Step 2: Run the four classes**

Run: `dotnet test --project tests/SqlSource.Tool.Tests/SqlSource.Tool.Tests.csproj --filter-class "SqlSource.Tool.Tests.Describing.Postgres.Walk.*" --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresSessionPlanTests"`
Expected: every test passes, with the same number of cases as before; `--list-tests` shows each row of the eleven as `(name: "<key>")`.

- [ ] **Step 3: Validate and commit**

`./format.sh`, `./pre-commit-validation.sh`, then commit "Key the theory data of plans by case name (TD-0054)".

### Task 6: Names and sealing (item 6)

**Files:**
- Rename: `tests/SqlSource.Tool.Tests/Describing/Postgres/CheckVerdictTests.cs` to `NpgsqlCheckTests.cs` (`git mv`), its class with it; modify `PostgresErrorsTests.cs:642`
- Modify: every file of `tests/SqlSource.Tool.Tests` that declares a test class with no `sealed`
- Modify: `tests/SqlSource.Tests.Postgres/Describer/{ColumnTests,ExpressionTests,NpgsqlCheckTests,ParameterTests,ScanTests,SessionTests,TableMatchTests,WalkTests,FixtureCasesTests,SetOperationTests}.cs`, `Realistic/Describer/{RealisticDescriptionTests,RealisticNullabilitySafetyTests}.cs`
- Modify: `tools/record-realistic.sh:16`, `tools/AGENTS.md:11`, `docs/tech-debt/TD-0050-a-catalog-types-name-depends-on-the-search-path.md:11`, `tests/AGENTS.md`

- [ ] **Step 1: Rename `CheckVerdictTests`**, and the comment of `PostgresErrorsTests` that names it.

- [ ] **Step 2: Seal the tool's test classes**, with the edit of Task 1 Step 2 over `tests/SqlSource.Tool.Tests`.  Then `grep -rnE '^(public|internal) (partial )?class ' tests --include='*.cs'` prints nothing on this branch except the 69 lines of Task 1, which reach it from `main` once 3a merges.

- [ ] **Step 3: Rename the container tests**

In the twelve files, `public async Task Describe_` and `public Task Describe_` become `DescribeAsync_` (148 tests, four of them `TableMatchTests`' expression-bodied ones), except `SetOperationTests`' `Describe_NestedSetOperationWithANullableLeftmostBranch_ReturnsNull`, which becomes `ReturnsNullAsync_NestedSetOperationWithANullableLeftmostBranch_IsTrue`.  `DescribeCommandTests` and `ToolProcessTests` keep theirs.  Then `record-realistic.sh`'s `TEST` and `tools/AGENTS.md` name `DescribeAsync_Case_IsItsRecordedDescription`, and TD-0050 names `DescribeAsync_BuiltInTypeHiddenOnTheSearchPath_IsStillTheCatalogsSpellingAndBindsByTheBareName`.

- [ ] **Step 4: Find every other mention**

For each old class name (`CheckVerdictTests`, and `PostgresSessionTests`, `PostgresEventsTests` or `NullabilityWalkTests` where a mention means a test that moved) and each renamed method, `git grep` outside `docs/superpowers/` finds none but the ones that still hold.

- [ ] **Step 5: Record the rules in `tests/AGENTS.md`**

- The last bullet says: test classes are `sealed`; a test's prefix names what it calls, the C# method in full (`DescribeAsync_`, never `Describe_`) or the command or statement it runs (`DescribeCommandTests`' `Describe_`, `PlanOutputTests`' `Explain_`); a unit-test class and a container class may share a name, as the two `NpgsqlCheckTests` do, told apart by their folders.
- The bullet of the tool's root helpers names `PostgresSessionCases.cs`, `PostgresEventCases.cs` and `PostgresWalkColumns.cs`, for the split classes of the session, its events and the walk.

- [ ] **Step 6: Run on both images, validate and commit**

Run `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj`: every test passes or skips.  Then `./format.sh`, `./pre-commit-validation.sh`, and commit "Name each test for what it calls and seal the tool's test classes (TD-0054)".

### Task 7: Close TD-0054, compare, review, open the pull request

**Files:**
- Delete: `docs/tech-debt/TD-0054-duplicated-and-uneven-patterns-in-the-postgresql-tests.md`
- Modify: `docs/tech-debt/README.md` (its row)
- Add: this plan

- [ ] **Step 1: Close the item**

Delete the file and its row; `git grep -n 'TD-0054' -- ':!docs/superpowers'` prints nothing.

- [ ] **Step 2: Compare the lists of tests**

Save the after lists as Task 2 Step 1 did.  For each project: the counts are equal, and the two lists, with the class stripped from each line, the renames of Tasks 3 and 6 undone and labelled rows reduced to their method, are the same.  Any difference is a lost or doubled test, fixed before going on.

- [ ] **Step 3: Validate and commit**

`./format.sh`, `./pre-commit-validation.sh`, then commit "Close TD-0054" with this plan.

- [ ] **Step 4: Review the whole branch**

A fresh reviewer reads `git diff main...HEAD` against this plan and the spec.  Every finding is fixed (`AGENTS.md`: minor feedback is never deferred), or, for a large one, the user is asked whether it becomes tech debt; after the fixes, validate and commit again.

- [ ] **Step 5: Open the pull request, when allowed**

Only once https://github.com/mbcrawfo/SqlSource/pull/64 has merged, and not before 17:00.  Then: `git fetch --tags origin` and check `VersionPrefix` in `Directory.Build.props` against `git tag --list 'v*' --sort=-v:refname | head -n 1`; bring the branch up to date with `main` (the app's sync, resolving TD-0054's file by keeping it deleted); run `./pre-commit-validation.sh` and the container tests on `postgres:14.24` on the merged tree; push; open "TD-0054, 4 of 4: the structure of the PostgreSQL tests" against `main`, saying it lands after 3a and naming the before and after counts; turn Auto-fix on, and reply to every CodeRabbit finding.
