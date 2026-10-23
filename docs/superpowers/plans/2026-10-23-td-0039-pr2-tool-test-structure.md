# TD-0039, pull request 2: the structure of the tool's tests - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every theory of `tests/SqlSource.Tool.Tests` whose row is a kind that the test turns into an arrangement becomes one fact for each kind, `DatabaseRunsTests` is split by subject over a `DatabaseRunsScene`, and TD-0039 is rewritten to what remains for pull request 3.

**Architecture:** A split theory keeps its comment and its assertions in a private method of its class, and each row becomes a fact that arranges its own case and calls that method.  The four classes split from `DatabaseRunsTests` compose a `DatabaseRunsScene` at the root of the project, as the command tests compose `DescribeScene`; no base class.  Nothing under `src/` changes.

**Tech Stack:** C# (`LangVersion` `preview`) on `net8.0`/`net10.0`; xunit v3 on Microsoft.Testing.Platform with Shouldly.

**Spec:** `docs/superpowers/specs/2026-10-11-td-0039-duplicated-test-patterns-design.md`, the section "Pull request 2", and `docs/tech-debt/TD-0039-duplicated-and-uneven-patterns-in-the-tests.md`, items 5 and 6.

**Branch:** `claude/td-0039-test-patterns-6c394b`, synced with `main` at `677d626`.

## Where the code differs from the design, and what the plan settles

The design was written on 2026-10-11; the item was recounted on 2026-10-22, and again for this plan on 2026-10-23.

- **The fact about `Redaction.Of` has already moved.**  `Of_TextThatHoldsTheSecretTwice_HasTheMarkInBothPlaces` is no longer in `DatabaseRunsTests` (td-0034 and td-0074 rewrote the redaction tests).  Nothing to move; the rewritten item drops it.
- **`RunLogTests` is now `RunLogDestinationTests`** (td-0074), in `tests/SqlSource.Tool.Tests/Logging/`.
- **The third row of `DescribeRunTests`' theory is `"not a sidecar"`**, a merge conflict, not `"a merge conflict"`.
- **Item 6 now also names three theories of `Describing/Postgres/Walk/`** (review of 2026-10-22): `NullabilityWalkSystemColumnTests` (3 rows), `NullabilityWalkSubPlanTests` (10 rows) and `NullabilityWalkPartitionTests` (4 rows).  They are tool tests, so they are in this pull request.  With the design's eleven rows, that is twenty-eight facts.
- **The level theories of `ModelTests`, `TableModelTests` and `TypeOverrideLevelTests` stay for pull request 3**, which owns `tests/SqlSource.Tests/Generator/`.
- **A sweep of every theory of the tool's tests for a `switch`, an `if` or a `==` on a row found no other kind theory.**  `ProjectEvaluatorTests.Evaluate_Project_IsReadByTheFirstRowOfTheTableThatHolds` reads its `assets` column with a `switch`, but its rows are the decision table of the phase 2.3 design, row for row, and splitting them would hide the table; it stays a theory.  The others (`Run_DescriberThatThrows_IsAFaultWithoutTheConnection`, `DescribeAsync_PlanThatCannotBeUsed_WritesItsReason`, `DescribeAsync_CheckThatTheServerCancels_IsSqlsrc225AtCheckParameters`, the `PlanExpressionTests` theories) pass their row on as data.
- **`DatabaseRunsCancellationTests` has two tests**, as the design's table gives it.  The fault class has twenty-three; the design's four classes stand.
- **One commit for the four tasks.**  Each task's "Format, validate, commit" step became one validation of the whole tree and one commit, since the tasks were built side by side and an intermediate tree was never validated.
- **`DatabaseRunsScene` lives at the project's root, namespace `SqlSource.Tool.Tests`**, beside `DescribeScene`, as `tests/AGENTS.md` places helpers that only the tool's tests use.  `tests/README.md` and `tests/AGENTS.md` name it with the other split classes' helpers.

## Global Constraints

- Nothing under `src/` changes.  `README.md`, `CONTRIBUTING.md`, `docs/manual/` and `docs/publishing.md` are not touched.
- No test changes what it checks.  A moved test keeps its name, body and comment; a fact made from a row keeps that row's arrangement and the theory's assertions exactly.
- Test names are `Method_Scenario_Outcome`; every test class is `public sealed class`.
- The build treats warnings as errors with `AnalysisLevel` `latest-all`; fix in code, never suppress.  `ImplicitUsings` is off: each split file keeps only the `using`s it needs.
- Comments: plain declarative style of the files around them, two spaces after a full stop.
- Every commit passes `./pre-commit-validation.sh` (Docker running).  Run `./format.sh`, then the validation, then commit.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- **A row whose arrangement is lost in the split.**  `ExitCodeTests`' variable row sets `SQLSOURCE_LOG` *and* runs `describe` with no option; `NullabilityWalkSubPlanTests`' `"max"` row changes the column's type to `timestamptz`; `NullabilityWalkPartitionTests`' `"missing"` row passes one partition, not two.  Each task's diff is read row by row against the theory it replaces.
- **A test dropped or duplicated in a move.**  Task 4 compares `--list-tests` with the list saved before Task 1: the only differences are the 28 new fact names replacing 7 theory names, and the 43 tests of `DatabaseRunsTests` under their new classes.
- **Shared state between tests through the scene.**  xunit makes a class instance per test; the scene is a field, built in the constructor and disposed with the class, so each test keeps its own folder.
- **`_files` read after a run.**  `RunExpectingTheValueInTheDescriptionAsync` and two tests read the files of the last run; the scene keeps them as `Files`.

---

### Task 1: One fact for each kind in the command and log tests (item 6)

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Commands/DescribeRunTests.cs:108-131`
- Modify: `tests/SqlSource.Tool.Tests/Commands/CheckTests.cs:146-166`
- Modify: `tests/SqlSource.Tool.Tests/Commands/ExitCodeTests.cs:80-102`
- Modify: `tests/SqlSource.Tool.Tests/Logging/RunLogDestinationTests.cs:107-133`

| Theory (removed) | Facts | Private method |
|----|----|----|
| `Run_SidecarThatIsNotThisTools_IsWrittenAgainWithEveryQueryDescribed` | `Run_SidecarOfAnotherToolVersion_IsWrittenAgainWithEveryQueryDescribed`, `Run_SidecarOfALowerFormatVersion_IsWrittenAgainWithEveryQueryDescribed`, `Run_SidecarWithAMergeConflict_IsWrittenAgainWithEveryQueryDescribed` | `ShouldBeWrittenAgainAsync(Func<string, SidecarFile, string> replace)`: describes, reads the written text and the sidecar, writes `replace(written, sidecar)`, describes again and asserts as the theory did |
| `Check_SidecarThatCannotBeRead_IsOneDifferenceAtTheSidecar` | `Check_SidecarWithAMergeConflict_IsOneDifferenceAtTheSidecar`, `Check_SidecarThatIsADirectory_IsOneDifferenceAtTheSidecar` | `ShouldBeOneDifferenceAtTheSidecarAsync()`: runs `--check` and asserts as the theory did; each fact describes first and spoils the sidecar itself |
| `Run_ErrorWithALogAskedFor_ExitsOneWithoutTheLine` | `Run_ErrorWithTheLogOption_ExitsOneWithoutTheLine` (`--log run.log`, SQLSRC999), `Run_ErrorWithTheLogVariable_ExitsOneWithoutTheLine` (`SQLSOURCE_LOG=run.log`, no option, SQLSRC999), `Run_LogThatCannotBeOpened_ExitsOneWithoutTheLine` (`--log tmp`, SQLSRC218), `Run_LogThatIsAFileOfTheRun_ExitsOneWithoutTheLine` (`--log Users.sql`, SQLSRC218) | `ShouldExitOneWithoutTheLine(CliResult result, string id)`; the failure of `GetUser` stays in each fact's arrangement |
| `Run_LogThatIsAFileOfTheRun_IsSqlsrc218AndTheFileIsAsItWas` | `Run_LogOptionThatIsAFileOfTheRun_IsSqlsrc218AndTheFileIsAsItWas`, `Run_LogVariableThatIsAFileOfTheRun_IsSqlsrc218AndTheFileIsAsItWas` | `ShouldLeaveTheFileAsItWasAsync(Func<Task<CliResult>> run)`: reads the bytes before, runs, asserts as the theory did |

The theory's comment goes above the private method, or above the first fact when it says why the case exists.

- [ ] **Step 1:** Replace each theory as the table says.
- [ ] **Step 2:** Build and run the four classes: `dotnet build tests/SqlSource.Tool.Tests && dotnet test --project tests/SqlSource.Tool.Tests --no-build -- --filter-class '*DescribeRunTests' --filter-class '*CheckTests' --filter-class '*ExitCodeTests' --filter-class '*RunLogDestinationTests'`.  Expected: all pass.
- [ ] **Step 3:** Format, validate, commit: `Split the kind theories of the command and log tests into facts (TD-0039 item 6)`.

### Task 2: One fact for each reason or shape in the walk's tests (item 6)

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkSystemColumnTests.cs:75-93`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkSubPlanTests.cs:70-104`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkPartitionTests.cs:85-117`

| Theory (removed) | Facts | Private method |
|----|----|----|
| `Run_SystemColumnOfATableThatMayReadRowsFromElsewhere_KeepsNoOrigin` | `Run_SystemColumnOfATableWithChildren_KeepsNoOrigin`, `Run_SystemColumnOfATableWithAForeignPartition_KeepsNoOrigin`, `Run_SystemColumnOfATableWithAnInsteadRule_KeepsNoOrigin` | `static void ShouldKeepNoOrigin(PostgresTableRow table)` |
| `Run_ScalarSubPlanThatMayReturnNoRow_KeepsNoOrigin` | one per shape: `Run_ScalarSubPlanSortedByAGroupKey_KeepsNoOrigin`, `Run_ScalarSubPlanSortedWithoutAKey_KeepsNoOrigin`, `Run_ScalarSubPlanWithGroupingSets_KeepsNoOrigin`, `Run_ScalarSubPlanWithAFilter_KeepsNoOrigin`, `Run_ScalarSubPlanUnderALimit_KeepsNoOrigin`, `Run_ScalarSubPlanOfMax_KeepsNoOrigin` (type `Timestamptz`), `Run_ScalarSubPlanWithTwoOutputs_KeepsNoOrigin`, `Run_ScalarSubPlanFoundTwice_KeepsNoOrigin`, `Run_ScalarSubPlanOfAnotherName_KeepsNoOrigin`, `Run_ScalarSubPlanThatIsAnInitPlan_KeepsNoOrigin` | `static void ShouldKeepNoOrigin(long type, params PlanNode[] subPlans)` |
| `Run_ExpressionOverPartitionsThatDoNotAllProveIt_KeepsNoOrigin` | `Run_ExpressionOverAPartitionWithoutARow_KeepsNoOrigin`, `Run_ExpressionOverAForeignPartition_KeepsNoOrigin`, `Run_ExpressionOverAPartitionWhoseColumnIsNullable_KeepsNoOrigin`, `Run_ExpressionOverPartitionsOfTwoTypes_KeepsNoOrigin` | none needed beyond `RunOverPartitions`; each fact asserts its own answers, as the row did |

- [ ] **Step 1:** Replace each theory as the table says; every fact keeps its row's plan, table and answers.
- [ ] **Step 2:** Build and run `--filter-class '*NullabilityWalk*'`.  Expected: all pass.
- [ ] **Step 3:** Format, validate, commit: `Split the reason and shape theories of the walk's tests into facts (TD-0039 item 6)`.

### Task 3: `DatabaseRunsTests` by subject (item 5)

**Files:**
- Create: `tests/SqlSource.Tool.Tests/DatabaseRunsScene.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsTests.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsFailureTests.cs`, `DatabaseRunsFaultTests.cs`, `DatabaseRunsCancellationTests.cs`
- Modify: `tests/README.md` (the paragraph of the helpers of `Describing/`), `tests/AGENTS.md` (the sentence on split classes)

**Interfaces:**
- Produces: `internal sealed class DatabaseRunsScene : IDisposable` with `const string Users` (the text of the class today), `TempFolder Folder`, `FakeDescriber Describer`, `DescribeCapture Capture`, `CapturingExchange Exchange`, `Dictionary<string, string> Environment`, `StringWriter Error`, `TestProject Project`, `string UsersPath`, `ImmutableArray<FileWork> Files` (the files of the last run), `Task<ImmutableArray<FileWork>> RunAsync(RunPlan? plan = null, bool registered = true, string[]? given = null, CancellationToken? cancellationToken = null, IQueryDescriber? describer = null)` with today's body and comment, and `static QueryState[] States(ImmutableArray<FileWork> files)`.  `Dispose` disposes `Error` and `Folder`.

Each class holds `private readonly DatabaseRunsScene _scene = new();` and `public void Dispose() => _scene.Dispose();`, and its tests read `_scene.X` where they read `_x`.

| Class | Tests |
|----|----|
| `DatabaseRunsTests` (sessions and connections) | `Run_NoDescriberForTheDialect_IsSqlsrc216AtEachQuery`, `Run_Describer_DescribesEachQueryInOneSessionForItsDatabase`, `Run_DatabaseWithoutAConnection_IsSqlsrc213OnceAndItsQueriesFail`, `Run_ConnectionForANameCloseToADatabase_IsListedInTheHelpOfSqlsrc213`, `Run_ConnectionForANameFarFromEveryDatabase_IsCountedByItsPositionInTheHelpOfSqlsrc213`, `Run_ConnectionsForNamesFarFromEveryDatabase_AreCountedInTheHelpOfSqlsrc213`, `Run_TwoDatabasesWithOneVariable_IsSqlsrc214AtEachThatHasAQueryToDescribe`, `Run_VariableWithNoNameAndTwoDatabases_IsSqlsrc215ForTheOneWithoutAConnection`, `Run_VariableWithNoNameAndADatabaseThatCannotBeDescribed_IsSqlsrc215ThatLeavesItOut`, `Run_DatabaseWithNothingToDescribe_NeedsNoConnectionAndIsNotOpened` |
| `DatabaseRunsFailureTests` (a session that cannot be opened and a query that fails) | `Run_SessionThatCannotBeOpened_IsReportedOnceAndTheDatabasesQueriesFail`, `Run_SessionThatCannotBeOpenedAndHoldsTheConnection_IsPrintedWithoutIt`, `Run_SessionThatCannotBeOpenedAndHoldsAPasswordTheDescriberGave_IsPrintedWithoutIt`, `Run_QueryThatFailsWithAPasswordThatIsTheDatabasesNameAndTheEngines_PrintsTheQueryLineAsItIs`, `Run_QueryThatFailsWithAPasswordThatIsTheDatabasesName_PrintsTheQueryLineAsItIs`, `Run_QueryThatFails_IsReportedInTheFormatOfADescribersErrorAndTheSessionGoesOn`, `Run_DriverWithoutAVersion_IsNamedAlone`, `Run_FailureThatHoldsTheConnection_IsPrintedWithoutIt` |
| `DatabaseRunsFaultTests` (a describer that breaks its contract) | the other 23, from `Run_DescriberThatThrowsWhenAskedForTheSecrets_IsOneFaultWithoutTheValue` to `Run_DescriberThatThrowsAndTheSessionThrowsWhenItIsClosed_IsAFaultOfTheFirstException`, with `ShouldBeTheFaultOfNothingAsync`, `RunExpectingTheValueInTheDescriptionAsync` and the nested `ThrowsBeforeItReturns` and `ThrowsWhenRead` |
| `DatabaseRunsCancellationTests` (a run that is cancelled) | `Run_CancelledWhileAQueryIsDescribed_EndsAsCancelledAndNotAsAFault`, `Run_CancelledAndTheSessionThrowsWhenItIsClosed_EndsAsCancelled` |

Each class opens with a one-line comment of its subject, as `DatabaseRunsTests` does today.

- [ ] **Step 1:** Create the scene and move the tests as the table says, in the order they are in today.
- [ ] **Step 2:** `tests/README.md`: after the paragraph of the helpers of `Describing/SqlServer/`, say that `DatabaseRunsScene.cs` holds what the classes split from `DatabaseRunsTests`, those named `DatabaseRuns*`, share: the folder, the describer, the exchange, the environment, the writer of errors, the project and the run.  `tests/AGENTS.md`: the sentence "The split classes `PostgresSession*` ... share their cases through a static helper and no base class" adds "and `DatabaseRuns*` through `DatabaseRunsScene`, composed as `DescribeScene` is".
- [ ] **Step 3:** Build and run `--filter-class '*DatabaseRuns*'`.  Expected: 43 tests pass (the theory's three rows count as one name).
- [ ] **Step 4:** Format, validate, commit: `Split DatabaseRunsTests by subject over DatabaseRunsScene (TD-0039 item 5)`.

### Task 4: The list of tests, and TD-0039 to what remains

**Files:**
- Modify: `docs/tech-debt/TD-0039-duplicated-and-uneven-patterns-in-the-tests.md`
- Modify: `docs/tech-debt/README.md` (the row of TD-0039, if its text names what is resolved)

- [ ] **Step 1:** `list.sh tests/SqlSource.Tool.Tests after.txt` (the scratchpad script that saved the list before Task 1), then `diff before.txt after.txt`.  Expected: 7 theory names out and 28 fact names in; 43 names of `Describing.DatabaseRunsTests` out and the same 43 method names in under `DatabaseRunsTests`, `DatabaseRunsFailureTests`, `DatabaseRunsFaultTests` and `DatabaseRunsCancellationTests`.  Nothing else.
- [ ] **Step 2:** Rewrite TD-0039 to what remains: items 5 (`SqlFileParserTests`, `GeneratedSourceTests`, the header and the triple, as counted on 2026-10-22, which still hold), 6 (the level theories of `ModelTests`, `TableModelTests` and `TypeOverrideLevelTests`, and the two theories of `ModelTests` that build their text with a `switch` and `Replace`) and 8; say that the design's pull request 3 is what remains, and drop `DatabaseRunsTests` from the trigger.
- [ ] **Step 3:** Validate and commit: `Rewrite TD-0039 to what pull request 3 leaves`.
