# Test cleanup phase 70: generator generation emitting mixed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the phase's eleven findings to four test classes of `Generation/Emitting`: one delete, three folds, four strengthens and three loosens.

**Architecture:** Only tests change, with no source or helper changes.  The guard is the `generator-generation-emitting` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Emitting/**/*.cs`, and, for the one finding mutation cannot guard, its cover and the test project's run time.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-70-generator-generation-emitting-mixed-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only `BatchMethodWriterTests.cs`, `ExecutionCapabilityRulesTests.cs`, `QueryMethodWriterTests.cs` and `XmlDocWriterTests.cs` change among the tests.
- The epic's phase table: change only row 70.

Checked against the spec on `cd00d105`: each of the twelve tests the findings name still reads as the findings say, so all eleven hold.  The spec names no document to update, and none names a test of the phase.

Both halves of the guard ran on `cd00d105`.  `main` then moved to `4ab5c120`, whose PR #395 changed `CallSpelling.cs`, `SyncCallSpelling.cs` and `RepositoryMethodWriterTests.cs` in this scope.  None is a file of this phase, and the validation runs on the merge.

Where the change goes beyond the spec's words:

- **F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-08.**  Two private constants, `SqlServerBatchReadsNoRows` and `SqlServerBatchReadsRows`, name the flags of the collection method's rows, as `SqlServerReadsRows` names the query method's.
- **F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-02.**  The finding's proposal keeps two rows and drops "the rest", but its list of rows names five of the six others and leaves out `(PostgreSql, Serializable, false)`, which no row of the rule's theory covers.  The epic's fold drops the named rows, so this row stays and the theory has three.  The guard's "after" half ran with the row gone: putting it back can only add kills, so its `compare` stands, and the test project has one test more than the 9,220 below.
- **F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-03.**  The new assertions replace the three old ones, each of which is part of a new one's text.
- **F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-12.**  The test now writes the plain `SqlServerStream`, since the vector column only served the text it no longer asserts.  The sentence of its comment on the row being built before the flag is set moves to `Append_SqlServerStreams_ChecksTheTransactionWhenARowFails`, whose text holds that order.

## Review Focus

- Each dropped row or removed test's cover still holds what the finding says it does.
- The loosened tests still fail when their own rule breaks.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-70/generator-generation-emitting/before` (Windows): 2,206 mutants tested, 1,993 killed, 204 survived, 9 timeouts, 0 errors, 141 without coverage; 39 min 35 s.
- [x] `kills` on the "before" report:

| Test | Rows | Kills | Alone | Survivors under it |
|----|----|----|----|----|
| `Escape_CharacterThatXmlCanHold_IsKept` | 4 | 22 | 1 | 1 |
| `AppendMember_TextEndingInALineTerminator_EndsWithAnEmptyCommentLine` | 1 | 32 | 0 | 2 |
| `Escape_TextWithNothingToEscape_IsTheSameString` | 1 | 14 | 0 | 1 |
| `Append_SqlServerOwnTransaction_RestoresTheSessionsLevel` | 9 | 133 | 3 | 41 |
| `Append_OffOrPostgreSql_LeavesTheSessionsLevelAlone` | 8 | 44 | 0 | 45 |
| `Append_SynchronousSqlServerReadingRows_ChecksAsTheAsynchronousMethodDoes` | 1 | 61 | 0 | 36 |
| `Of_QueryMethod_RestoresNoIsolation` | 2 | 2 | 0 | 0 |
| `Flags_AsNumbers_HaveTheValuesOfTheTheory` | 1 | 0 | 0 | 0 |
| `Append_EachPooledCollectionTypeUnderArrayPoolOff_ReadsItsRowsAsBeforeThePool` | 8 | 60 | 5 | 33 |
| `Append_SqlServerEachShape_RefusesACompletedTransactionBeforeItOpensTheConnection` | 5 | 105 | 0 | 27 |
| `Append_SqlServerStreams_DrainsInAFinallyWhenLeftAtARow` | 1 | 226 | 0 | 31 |

- [x] Run time of `tests/SqlSource.Tests`: 9,233 tests, 0 skipped, 6 min 0 s.
- [x] Coverage into `artifacts/test-cleanup/phase-70/coverage-before/SqlSource.Tests` (9,233 tests, 5 min 59 s): 4,387 of 4,416 lines over the 36 files of `src/SqlSource/Generation/Emitting/`.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Emitting/XmlDocWriterTests.cs`
- Modify: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/BatchMethodWriterTests.cs`
- Modify: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/ExecutionCapabilityRulesTests.cs`
- Modify: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/QueryMethodWriterTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 70's state)

- [x] **Step 1 (F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-07):** Delete `Of_QueryMethod_RestoresNoIsolation`.
- [x] **Step 2 (F-SqlSource.Tests.Generation.Emitting-1-11):** Move `"SELECT id FROM users"` into `Escape_CharacterThatXmlCanHold_IsKept` and delete the fact.
- [x] **Step 3 (F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-01):** Keep the rows `(On, false)` and `(Serializable, true)` of `Append_SqlServerOwnTransaction_RestoresTheSessionsLevel`.
- [x] **Step 4 (F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-02):** Drop the five named rows of `Append_OffOrPostgreSql_LeavesTheSessionsLevelAlone`, keeping `(SqlServer, Off, false)`, `(PostgreSql, Serializable, false)` and `(PostgreSql, Serializable, true)`.
- [x] **Step 5 (F-SqlSource.Tests.Generation.Emitting-1-04):** Add the row `"a\uE000b"`.
- [x] **Step 6 (F-SqlSource.Tests.Generation.Emitting-1-05):** Make `AppendMember_TextEndingInALineTerminator_EndsWithAnEmptyCommentLine` a theory over `\n`, `\r` and `\r\n`.
- [x] **Step 7 (F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-03):** Assert the synchronous refusal, the result-set check with its `FieldCount`, and the rollback catch's `reader.Dispose();` with its throw.
- [x] **Step 8 (F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-10):** Assert the buffer's whole declaration.
- [x] **Step 9 (F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-08):** Write each expected value as the cast of its flags and delete `Flags_AsNumbers_HaveTheValuesOfTheTheory`.
- [x] **Step 10 (F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-11):** Count only the refusal, once for every shape.
- [x] **Step 11 (F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-12):** Assert the three lines around the `yield return` and the finally's `if (atRow)` with its drain given `CancellationToken.None`.
- [x] **Step 12:** Set row 70 of the epic's table to `done`.
- [x] **Step 13:** `./format.sh`.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-70/generator-generation-emitting/after` (Windows): 2,206 mutants tested, 1,998 killed, 199 survived, 9 timeouts, 0 errors; 39 min 21 s.  `compare`: 2,002 detected before, 2,002 still detected, 0 lost, 0 not comparable.
- [x] **Step 2:** Every survivor a strengthen names is `Killed`: `XmlDocWriter.cs:182:74`, `XmlDocWriter.cs:198:47`, `RowsWriter.cs:373:72` and `RowsWriter.cs:377:94`.
- [x] **Step 3:** Hand check of F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-03, which names no survivor: with `AppendCompletedTransactionCheck`, `AppendResultSetCheck` or `AppendRollbackCheck` made to write nothing, the test fails each time.
- [x] **Step 4:** `kills` on the "after" report, in which `Append_OffOrPostgreSql_LeavesTheSessionsLevelAlone` had two rows, before its third was put back:

| Test | Rows | Kills | Alone | Survivors under it |
|----|----|----|----|----|
| `Escape_CharacterThatXmlCanHold_IsKept` | 6 | 24 | 3 | 0 |
| `AppendMember_TextEndingInALineTerminator_EndsWithAnEmptyCommentLine` | 3 | 40 | 2 | 1 |
| `Append_SqlServerOwnTransaction_RestoresTheSessionsLevel` | 2 | 133 | 3 | 40 |
| `Append_OffOrPostgreSql_LeavesTheSessionsLevelAlone` | 2 | 44 | 0 | 44 |
| `Append_SynchronousSqlServerReadingRows_ChecksAsTheAsynchronousMethodDoes` | 1 | 93 | 0 | 35 |
| `Append_EachPooledCollectionTypeUnderArrayPoolOff_ReadsItsRowsAsBeforeThePool` | 8 | 64 | 7 | 30 |
| `Append_SqlServerEachShape_RefusesACompletedTransactionBeforeItOpensTheConnection` | 5 | 91 | 0 | 26 |
| `Append_SqlServerStreams_DrainsInAFinallyWhenLeftAtARow` | 1 | 133 | 0 | 29 |
| Cover `Of_EachEngineShapeAndStreaming_GivesItsFlags` | 12 | 13 | 0 | 0 |
| Cover `Of_CollectionMethodAtAMode_RestoresTheSessionsIsolationOnlyWithItsOwnTransactionOnSqlServer` | 15 | 13 | 0 | 0 |
| Cover `Of_CollectionMethod_GivesItsEnginesFlags` | 11 | 20 | 0 | 0 |
| `Append_SqlServerStreams_ChecksTheTransactionWhenARowFails` | 1 | 176 | 0 | 21 |
| `Append_SqlServerStreams_DrainsAfterTheReadLoop` | 1 | 128 | 0 | 29 |

- [x] **Step 5:** Run time of `tests/SqlSource.Tests`: 9,220 tests, 0 skipped, 5 min 57 s, against 6 min 0 s before.
- [x] **Step 6:** Coverage into `artifacts/test-cleanup/phase-70/coverage-after/SqlSource.Tests` (9,220 tests, 6 min 3 s, against 5 min 59 s before): 4,387 of 4,416 lines, before and after, the same figure in each of the 36 files.
- [ ] **Step 7:** The pull request gives the change, the compare result, the coverage figures, the run times, and each removed test or row beside its cover.
