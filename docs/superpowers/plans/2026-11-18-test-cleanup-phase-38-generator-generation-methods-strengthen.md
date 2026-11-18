# Test cleanup phase 38: generator generation methods strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Write the other parameter of `ListNamesTests.PrefixOf_AnotherParameterMatchesTheSuffix_AddsAnUnderscore` as `ids_9`, so that the mutant `> '9'` to `>= '9'` of `ListNames.cs:144:41`, which survives today, is killed.

**Architecture:** One string in one test changes and there is no source change.  The guard is the `generator-generation-methods` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Methods/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-38-generator-generation-methods-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change only `Scalar("ids_0")` to `Scalar("ids_9")` in `PrefixOf_AnotherParameterMatchesTheSuffix_AddsAnUnderscore`, keeping the expected `ids__`.
- The epic's phase table: change only row 38.

## Review Focus

None: the change rewrites one string.  Checked against the spec on `5c748332`.  `ListNames.IsPrefixAndDigits` still reads each character after the prefix with `is < '0' or > '9'`, and every suffix the tests write is a digit below 9, so the finding holds.  The test is a `[Fact]`, so TD-0089 is unchanged.  No merged phase changed the scope's source or tests since `5ae9aa35`, so p22's "after" report is this phase's "before".  `kills` on it shows the test kills no mutant alone: the `_0` suffixes of `PrefixOf_ADeclaredVariableMatchesTheSuffix_AddsAnUnderscore`, `PrefixOf_EachMatchedInTurn_AddsAnUnderscoreForEach` and `PrefixOf_TooLongForSqlServer_UsesTheOrdinal` still read `144:32 <= '0'`.

---

### Task 0: The guard's "before" half

- [x] p22's "after" report copied to `artifacts/test-cleanup/phase-38/generator-generation-methods/before/reports/mutation-report.json`.
- [x] `kills` on the before report for `ListNamesTests.PrefixOf_AnotherParameterMatchesTheSuffix_AddsAnUnderscore`.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-38/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Generation/Methods/**/*.cs`.

### Task 1: Change the suffix

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Methods/ListNamesTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 38's state)

- [x] **Step 1:** Write `Scalar("ids_9")` in place of `Scalar("ids_0")` in `PrefixOf_AnotherParameterMatchesTheSuffix_AddsAnUnderscore`.
- [x] **Step 2:** Set row 38 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then the Methods tests pass.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST` and nothing `NOT COMPARABLE`, and `ListNames.cs:144:41 >= '9'` is `Killed` in the after report.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-38/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/Methods/**/*.cs` falls.
- [x] **Step 3:** The pull request gives the changed input, the compare result, the coverage figures and the run times.
