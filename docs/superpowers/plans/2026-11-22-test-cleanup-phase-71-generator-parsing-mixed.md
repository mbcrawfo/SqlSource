# Test cleanup phase 71: generator parsing mixed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the phase's thirteen findings to six test classes of `tests/SqlSource.Tests/Parsing`: four deletes, three folds, five strengthens and one loosen.

**Architecture:** Only tests change, with no source or helper changes.  The guard is the `generator-parsing` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource/Parsing/**/*.cs`, and, for the five findings mutation cannot guard, their covers and the test project's run time.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-71-generator-parsing-mixed-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only `SqlGeneratorParameterScopeTests.cs`, `SqlMarkerReaderTests.cs`, `SqlFileParserParameterTests.cs`, `SqlFileParserTests.cs`, `SqlFileParserTokenTests.cs` and `SqlListPlacesTests.cs` change among the tests.
- The epic's phase table: change only row 71.

Checked against the spec on `4531df23`: each test the findings name still reads as the findings say, so all thirteen hold.  Two places have moved since the review: `SqlFileParser.cs:588:25` is now `658:25`, and `SqlMarkerReader.cs:139:16` is now `156:16`.  The spec names no document to update, and none names a test of the phase.

Where the change goes beyond the spec's words:

- **F-SqlSource.Tests.Parsing-1-08.**  The moved comment named the `f` and `o` of the deleted fact, so it now says "the default" and "any token".
- **F-SqlSource.Tests.Parsing-1-09.**  The cover moves up to the deleted fact's place, so the fact's comment still opens the group of tests it explains.
- **F-SqlSource.Tests.Parsing-1-07.**  Of the proposal's two options this takes the first, dropping the lexeme listing.  The sentence of the comment that described that listing goes with it.
- **F-SqlSource.Tests.Parsing-2-16.**  The fact's comment said "such a word" of the theory before it, which would be wrong in its new place, so it is reworded.  The theory's own comment moves onto its rows, so that each comment covers only the rows it describes.
- **F-SqlSource.Tests.Parsing-2-03.**  The theory names the state with `nameof` and `Enum.Parse`, since an internal enum cannot be the parameter of a public test method.

## Review Focus

- Each removed test or row's cover still holds what the finding says it does.
- The loosened test still fails when its own rule breaks.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-71/generator-parsing/before` (Windows, on `4531df23`): 2,040 killed, 173 survived, 84 timeouts, 0 errors; 24 min 54 s.
- [x] `kills` on the "before" report:

| Test | Rows | Kills | Alone | Timeouts under it | Survivors under it |
|----|----|----|----|----|----|
| `Parse_LineCommentThatHoldsTheClosingBraces_IsReported` | 1 | 166 | 0 | 17 | 21 |
| `Parse_SqlServerNegatedListInAnAggregate_HasAMethodsError` | 1 | 308 | 0 | 29 | 52 |
| `Read_MarkerWithoutAValue_HasAnEmptyValueSpan` | 1 | 45 | 0 | 7 | 2 |
| `Parameters_OfANewScope_AreEmpty` | 1 | 1 | 0 | 0 | 0 |
| `Parse_QueryTurningKeepCommentsOffUnderAChainThatKeepsThem_DropsTheComment` | 2 | 237 | 0 | 14 | 13 |
| `Parse_KeptForm_IsBuiltOnlyWhenItMayBeWanted` | 11 | 254 | 1 | 14 | 16 |
| `Parse_DeclareUnderADialectWithoutDeclaredVariables_DeclaresNone` | 1 | 127 | 0 | 13 | 14 |
| `Parse_DeclareUnderADialectWithoutDeclaredVariables_LeavesTheNameAParameter` | 7 | 177 | 0 | 13 | 14 |
| `Find_SqlServerNotWithNoWordAfterIt_ReachesTheList` | 1 | 128 | 0 | 22 | 31 |
| `Find_SqlServerWordThatStartsNoClauseAfterANot_EndsNothing` | 8 | 138 | 0 | 22 | 35 |
| `Parse_TwoDefaultsForOneTokenThatDiffer_ConflictAtTheSecond` | 4 | 292 | 11 | 15 | 26 |
| `Parse_CommentInTheHeadOfAnIgnoredToken_IsAnErrorOfTheKeptForm` | 3 | 309 | 6 | 18 | 35 |
| `Read_AnythingElse_IsNotAMarker` | 33 | 78 | 1 | 12 | 6 |
| `Read_OneStateTwice_IsNotAConflict` | 1 | 53 | 0 | 7 | 2 |
| `Find_SqlServer_ReadsWhereEachListStands` | 121 | 324 | 36 | 36 | 75 |
| `Parse_CommentInTheGapOfAContinuedStringInADefault_IsReported` | 1 | 177 | 0 | 22 | 21 |

- [x] Coverage into `artifacts/test-cleanup/phase-71/coverage-before/SqlSource.Tests`: 9,328 tests, 0 skipped, 7 min 1 s; 3,872 of 3,911 lines over the 44 files of `src/SqlSource/Parsing/`.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tests/Parsing/Markers/SqlGeneratorParameterScopeTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/Markers/SqlMarkerReaderTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlFileParserParameterTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlFileParserTokenTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlListPlacesTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 71's state)

- [x] **Step 1 (F-SqlSource.Tests.Parsing-1-08):** Delete `Parse_LineCommentThatHoldsTheClosingBraces_IsReported` and move its comment above the theory.
- [x] **Step 2 (F-SqlSource.Tests.Parsing-1-09):** Delete `Parse_SqlServerNegatedListInAnAggregate_HasAMethodsError`, with its comment above the cover.
- [x] **Step 3 (F-SqlSource.Tests.Parsing-2-11):** Delete `Read_MarkerWithoutAValue_HasAnEmptyValueSpan`.
- [x] **Step 4 (F-SqlSource.Tests.Parsing-2-12):** Delete `Parameters_OfANewScope_AreEmpty`.
- [x] **Step 5 (F-SqlSource.Tests.Parsing-1-10):** Move the row with `-- generator: default` in the preamble into `Parse_KeptForm_IsBuiltOnlyWhenItMayBeWanted` and delete `Parse_QueryTurningKeepCommentsOffUnderAChainThatKeepsThem_DropsTheComment`.
- [x] **Step 6 (F-SqlSource.Tests.Parsing-1-11):** Assert `DeclaredVariables.ShouldBeEmpty()` in `Parse_DeclareUnderADialectWithoutDeclaredVariables_LeavesTheNameAParameter` and delete `Parse_DeclareUnderADialectWithoutDeclaredVariables_DeclaresNone`.
- [x] **Step 7 (F-SqlSource.Tests.Parsing-2-16):** Move `"WHERE NOT a = 1 b IN @a"` into `Find_SqlServerWordThatStartsNoClauseAfterANot_EndsNothing` as its first row and delete `Find_SqlServerNotWithNoWordAfterIt_ReachesTheList`.
- [x] **Step 8 (F-SqlSource.Tests.Parsing-1-02):** Add the row with `{{b:z}}` before the token in conflict.
- [x] **Step 9 (F-SqlSource.Tests.Parsing-1-03):** Add the row `"SELECT {{h}}, {{f/* c */:x}}"`.
- [x] **Step 10 (F-SqlSource.Tests.Parsing-2-02):** Add the row `"a-- name: A"`.
- [x] **Step 11 (F-SqlSource.Tests.Parsing-2-03):** Make `Read_OneStateTwice_IsNotAConflict` a theory with a row that gives `keep-comments=default` twice.
- [x] **Step 12 (F-SqlSource.Tests.Parsing-2-04):** Add the row `"SELECT SUM(CASE WHEN s IN @a THEN (1) END) OVER () FROM t"` to `Queries`.
- [x] **Step 13 (F-SqlSource.Tests.Parsing-1-07):** Drop the lexeme listing of `Parse_CommentInTheGapOfAContinuedStringInADefault_IsReported`.
- [x] **Step 14:** Set row 71 of the epic's table to `done`.
- [x] **Step 15:** `./format.sh`.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-71/generator-parsing/after` (Windows): 2,050 killed, 165 survived, 82 timeouts, 0 errors; 25 min 9 s.  `compare`: 2,124 detected before, 2,122 still detected, 1 lost, 1 not comparable, both kills that did not repeat:
  - `LOST Parsing/SqlParameterList.cs:130:16` Conditional (false), Killed to Survived.  Its only killer in the "before" report is `SqlFileParserAllocationTests.Parse_QueriesWithAParamEach_AllocateWithinTheirBudgetForTheParam`, which this phase does not touch.  With the mutation written into the source by hand that test passed 3 of 3 runs on a build confirmed to hold it: an empty builder's `ToImmutable()` allocates nothing, so the mutant allocates what the original does.
  - `NOT COMPARABLE Parsing/SqlListUses.Conditions.cs:473:17` Equality `_top + 1 != _frames.Length`, Killed to Timeout, so still detected.  Its only killer in the "before" report is `Corpus_EveryCase_MatchesTheParser`, which this phase does not touch.
  - `kills` on the "before" report lists neither mutant under any test the phase removes or changes.
- [x] **Step 2:** Every survivor a strengthen names is `Killed`:
  - 1-02: `SqlTokenList.cs:125:56` (`index--`) and `SqlTokenList.cs:128:17` (Logical).
  - 1-03: `SqlFileParser.cs:658:25` (was `588:25`), Conditional (false) and Equality `next > sql.Text.Length`.  Two other mutants at this place, Conditional (true) and Equality `next <= sql.Text.Length`, which the finding does not name, still survive and are equivalent: `next` never passes the text's length, and `IndexOf` from the length itself gives -1.
  - 2-02: `SqlMarkerReader.cs:156:16` (was `139:16`), `index <= 0`.
  - 2-03: `SqlGeneratorParameterScope.cs:98:13`, `_defaulted ^= parameter`.
  - 2-04: `SqlListUses.Places.cs:451:21`, Statement and `depth--`.
- [x] **Step 3:** `kills` on the "after" report, for the tests that remain and the named covers:

| Test | Rows | Kills | Alone | Timeouts under it | Survivors under it |
|----|----|----|----|----|----|
| `Parse_KeptForm_IsBuiltOnlyWhenItMayBeWanted` | 12 | 255 | 1 | 14 | 12 |
| `Parse_DeclareUnderADialectWithoutDeclaredVariables_LeavesTheNameAParameter` | 7 | 177 | 0 | 13 | 13 |
| `Find_SqlServerWordThatStartsNoClauseAfterANot_EndsNothing` | 9 | 138 | 0 | 22 | 36 |
| `Parse_TwoDefaultsForOneTokenThatDiffer_ConflictAtTheSecond` | 5 | 298 | 13 | 15 | 17 |
| `Parse_CommentInTheHeadOfAnIgnoredToken_IsAnErrorOfTheKeptForm` | 4 | 310 | 8 | 17 | 25 |
| `Read_AnythingElse_IsNotAMarker` | 34 | 80 | 2 | 11 | 5 |
| `Read_OneStateTwice_IsNotAConflict` | 2 | 56 | 1 | 7 | 0 |
| `Find_SqlServer_ReadsWhereEachListStands` | 122 | 326 | 38 | 36 | 72 |
| `Parse_CommentInTheGapOfAContinuedStringInADefault_IsReported` | 1 | 171 | 0 | 22 | 14 |
| `Parse_CommentThatHoldsBracesInsideAnOpenDefault_IsReported` | 6 | 243 | 0 | 18 | 18 |
| `Parse_SqlServerNegatedListInAnAggregateTwice_IsReportedAtEachUseAsWritten` | 1 | 314 | 0 | 39 | 63 |
| `Read_MarkerComment_ReturnsItsKindAndValue` | 42 | 55 | 0 | 8 | 0 |
| `Describe_Marker_IsItsWordAndItsValue` | 7 | 59 | 0 | 8 | 0 |
| `Read_ParameterWithAValueNotInItsList_IsInvalidAndNamesTheValues` | 4 | 50 | 1 | 7 | 0 |
| `Read_DefaultWithAValue_IsInvalidWithNoValues` | 2 | 50 | 1 | 7 | 0 |

- [x] **Step 4:** Coverage into `artifacts/test-cleanup/phase-71/coverage-after/SqlSource.Tests`: 9,328 tests, 0 skipped, 6 min 3 s; 3,873 of 3,911 lines over the same 44 files.  No file falls, and `Tokens/SqlTokenList.cs` gains one line, 91 to 92.
- [x] **Step 5:** Run time of `tests/SqlSource.Tests`, both with coverage on this machine: 7 min 1 s before, 6 min 3 s after.  The count stays 9,328: eight tests or rows go and eight rows come.
