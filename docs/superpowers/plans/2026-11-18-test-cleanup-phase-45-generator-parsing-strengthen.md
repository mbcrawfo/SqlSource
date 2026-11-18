# Test cleanup phase 45: generator parsing strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add one row to each of six tests of `tests/SqlSource.Tests/Parsing`, so that each kills the survivor its finding names: an option given twice, a quoted column name of one character, a quoted name that ends in an undoubled quote, two invalid library entries, a bracketed identifier that ends the text, and a token name holding a zero-width space.

**Architecture:** Each finding adds one `InlineData` row to an existing theory; no assertion, helper or source changes.  The guard is the `generator-parsing` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Parsing/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-45-generator-parsing-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only rows are added; every existing row and assertion stays.  `SqlDialectNameTests.TryParse_NameWithOptions_GivesTheDialectAndItsOptions` is the cover of F-SqlSource.Tests.Generator-2-04 (phase 19).
- The zero-width space is written `​` in the C# source, never as a literal character.
- None of the five theories is in TD-0089's lists, and the new rows add no branch on the row, so TD-0089 is not touched.
- The epic's phase table: change only row 45.

Checked against the spec on `650518df`.  The survivors' lines are where the spec says: `SqlDialectName.cs:97` (`options |= option`), `SqlMarkerScope.cs:812` (`written.Length < 3`), `:826` (`index + 1 < inner.Length`), `:448` (the `return` after the first invalid library entry), `BracketReader.cs:23` (`close + 1 < text.Length`) and `OpenDefaultFinder.cs:80` (`SqlIdentifier.IsValid(name) ? name : null`); the six findings hold.  #344 (the sqlfluff templater) changed the scope's source and tests after p06's run on `6ad6c501`, so p06's "after" report is stale and this phase runs its own "before".

## Review Focus

Each row is a boundary the proposal names, and the guard's "after" run shows whether it kills its survivor; no input class is left that the rows do not reach.

---

### Task 0: The guard's "before" half

- [ ] Mutation run into `artifacts/test-cleanup/phase-45/generator-parsing/before`, with its time and figures.
- [ ] `kills` on the before report for each of the six tests: each survivor the spec names is `Survived`.
- [ ] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-45/coverage-before/SqlSource.Tests`, the figures of every file of `src/SqlSource/Parsing`.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/SqlFileParserMarkerTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/Quoting/BracketReaderTests.cs`
- Modify: `tests/SqlSource.Tests/Parsing/Tokens/OpenDefaultFinderTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 45's state)

- [ ] **Step 1 (F-SqlSource.Tests.Parsing-1-01):** in `TryParse_NameWithOptions_GivesTheDialectAndItsOptions`, under "In any order, and one that is repeated counts once.", add `[InlineData("mysql,ansi-quotes,ansi_quotes", "MySql", "AnsiQuotes")]`.
- [ ] **Step 2 (F-SqlSource.Tests.Parsing-1-05):** in `Parse_ColumnMarker_GivesItsColumnANullability`, among the quoted names, add `[InlineData("\"a\" null", "a:null")]`.
- [ ] **Step 3 (F-SqlSource.Tests.Parsing-1-04):** in `Parse_ColumnMarkerWithoutANameAndANullability_IsInvalid`, after `"\"total\"x null"`, add `[InlineData("\"total\"\" null")]`.
- [ ] **Step 4 (F-SqlSource.Tests.Parsing-1-06):** in `Parse_InvalidLibraryTypes_IsSQLSRC111`, add `[InlineData("nodatime=maybe json=maybe")]`.
- [ ] **Step 5 (F-SqlSource.Tests.Parsing-2-01):** in `FindEnd_ClosedIdentifier_EndsAfterItsBracket`, add `[InlineData("[a]", "[a]")]`.
- [ ] **Step 6 (F-SqlSource.Tests.Parsing-2-05):** in `At_Offset_GivesTheTokenWhoseDefaultIsOpenThereAndWhereItsOpenerStarts`, after `"{{1:a "`, add `[InlineData("{{a​b:x ", 8, null, -1)]`.
- [ ] **Step 7:** Set row 45 of the epic's table to `done`.
- [ ] **Step 8:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tests --filter 'FullyQualifiedName~SqlSource.Tests.Parsing.'` passes.
- [ ] **Step 9:** Hand checks: with each survivor's mutation made by hand, its new row fails; each change reverted.
- [ ] **Step 10:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [ ] **Step 1:** The mutation run with output `.../after`, then `compare`: nothing `LOST` or `NOT COMPARABLE` but an accepted flip; each of the six survivors `Killed`.
- [ ] **Step 2:** Coverage into `artifacts/test-cleanup/phase-45/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Parsing` loses a line.
- [ ] **Step 3:** Copy the "after" report to the run's `reports/p45-generator-parsing-after.json`.
- [ ] **Step 4:** The pull request gives the rows, the compare result, the coverage figures, the hand checks and the run times.
