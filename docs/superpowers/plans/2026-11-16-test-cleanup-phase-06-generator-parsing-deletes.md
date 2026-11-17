# Test cleanup phase 06: generator parsing deletes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Delete the eight tests of `tests/SqlSource.Tests/Parsing` that the test review found redundant, with the guard showing nothing lost.

**Architecture:** Eight test deletions, no source change.  The guard is the `generator-parsing` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource/Parsing/**/*.cs`, and, for the three findings mutation cannot guard (2-13, 2-14, 2-15), each removed test beside its named cover with the test project's run time before and after.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-06-generator-parsing-deletes-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but the eight the spec names.
- The covers keep every assertion and row: `SqlLexerTests.Lex_Construct_IsReadByTheRulesOfTheDialect`, `Lex_DollarTagWithANonAsciiCharacter_IsAQuote`, `Lex_CommentSyntaxInsideQuotedRegion_IsNotAComment`, `Lex_StringOfPostgreSqlThatIsNotContinued_IsReadByItsOwnPrefix`; the reader tests of `Parsing/Quoting/`; `SqlGeneratorParameterScopeTests.Read_OneOptionInTwoStates_ConflictsAtTheLater`; `TokenScannerTests.Scan_ReservedNameWithADefault_IsAnError`; `SqlQueryHashTests.Compute_SimpleQuery_IsTheSha256OfItsInput`, `Compute_ChangeToWhatIsDescribed_GivesAnotherHash`, `Compute_BareParamMarker_IsTheHashWithoutIt`.
- `SqlModelTests.SqlParseError_SameValuesInSeparateInstances_AreEqual` and `SqlBlock_SameValuesInSeparateInstances_AreEqual` stay (the spec's keeps, 2-18 and 2-19).
- The epic's phase table: change only row 06.

## Review Focus

None: the change removes tests and adds no behaviour.  Checked against the spec on `066dc5fb`: nothing under `src/SqlSource/Parsing` or `tests/SqlSource.Tests/Parsing` has changed since the review's commit, each finding reads as the review wrote it, and every cover exists.  No helper, `using` or data member is used only by a removed test.  The document lines the spec lists (TD-0004 line 25, `src/SqlSource/Parsing/AGENTS.md` line 11, `tools/README.md` lines 176 and 194) name only tests and classes that stay, so they stay as they are.

---

### Task 0: The guard's "before" half

- [x] The `generator-parsing` mutation run into `artifacts/test-cleanup/phase-06/generator-parsing/before`.
- [x] `kills` on the before report for each of the eight removed tests.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-06/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Parsing/**/*.cs`.
- [x] Run time of `dotnet test --project tests/SqlSource.Tests`, interleaved with the "after" runs (`--no-build`), with the load beside each figure.

### Task 1: Delete the tests

**Files:**
- Modify: `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` (remove `Lex_QuotedRegion_EndsAtTheUndoubledQuote`, `Lex_EscapeString_TreatsBackslashAsAnEscape`, `Lex_EscapeStringWithDoubledQuote_EndsAtTheUndoubledQuote`, `Lex_LetterEAtTheEndOfAnIdentifier_IsNotAnEscapeStringPrefix`, `Lex_DollarQuote_EndsAtTheSameTag`)
- Modify: `tests/SqlSource.Tests/Parsing/SqlModelTests.cs` (remove `SqlParseError_Create_HoldsItsArguments`)
- Modify: `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs` (remove `Compute_AnyQuery_IsSixtyFourLowerCaseHexCharacters`, `Compute_ParamMarkerWithAType_ChangesTheHash`)
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 06's state)

- [x] **Step 1:** Remove each test with its attributes and the comment above it, if it has one.
- [x] **Step 2:** Set row 06 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows no removed test made.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-06/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Parsing/**/*.cs` falls.
- [x] **Step 3:** The pull request lists each removed test beside its cover, the compare result, the coverage figures and the run times.
