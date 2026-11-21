# Test cleanup phase 59: tool dotenv strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `DotEnvParserTests.Parse_Text_HoldsTheValuesOfTheToolsKeys` fail when the byte order mark is left in place, when an unquoted value's comment is found by the character after a `#` instead of the one before it, and when the last ` #` of a value is taken instead of the first.

**Architecture:** One test file, `tests/SqlSource.Tool.Tests/DotEnv/DotEnvParserTests.cs`.  No source or helper changes.  The guard is the `tool-dotenv` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource.Tool/DotEnv/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-59-tool-dotenv-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the theory of the finding changes, by the three rows its finding proposes.  Every existing row keeps its string.
- The epic's phase table: change only row 59.

Checked against the spec on `e3b723d2`: `DotEnvParser.cs` still strips the mark at line 46, and `ReadUnquoted` still ends a value at the first `#` whose preceding character is white space (lines 225-228).  The theory reads as the finding says: the only marked row sets its key again on the next line, the only "ends at a #" row has white space on both sides of its one `#`, and the `a#b` row has white space on neither.  So the finding holds.  No phase has run this scope before, so the "before" is a run of its own.  The spec names no document to update, and none names the changed test or its class.

## Review Focus

None beyond the finding: each new row is one the finding's verifier traced to fail on one named mutant.

---

### Task 0: The guard's "before" half

- [x] Mutation into `artifacts/test-cleanup/phase-59/tool-dotenv/before`: 143 mutants tested, 124 killed, 15 survived, 4 timeouts, 0 errors; the tested run took 2 min 14 s.  `kills`: `Parse_Text_HoldsTheValuesOfTheToolsKeys` 28 rows, 74 kills, 14 alone, 3 timeouts and 9 survivors under it, among them the three the finding names (`DotEnvParser.cs:46:9` Block removal, `225:79` Arithmetic `index + 1`, `228:17` Statement `;`).
- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-59/coverage-before/SqlSource.Tool.Tests` (7,089 tests, 3 min 10 s): 193 of 193 lines over the 5 files of `src/SqlSource.Tool/DotEnv/`.

### Task 1: Apply the finding

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/DotEnv/DotEnvParserTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 59's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests-2-01):** `Syntax` gains the rows "unquoted value ends at the first # that follows white space" (`Host=x # one # two` → `Host=x`), "unquoted value keeps a # with white space after it but none before it" (`Password=a# b` → `Password=a# b`) and "a byte order mark before the first key is dropped" (`﻿SQLSOURCE_CONNECTION_A=1` → `A`, `1`).  As the condition asks, the mark is written `﻿`; the existing row's literal mark is written the same way, which leaves its string as it was.
- [x] **Step 2:** Set row 59 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `DotEnvParserTests` passes (72 tests).
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-59/tool-dotenv/after`: 143 mutants tested, 127 killed, 12 survived, 4 timeouts, 0 errors; the tested run took 2 min 25 s.  `compare`: 128 detected before, 128 still detected, 0 lost, 0 not comparable.  The three survivors the finding names are now `Killed`, each by this test alone.  `kills` after: `Parse_Text_HoldsTheValuesOfTheToolsKeys` 31 rows, 77 kills, 17 alone, 3 timeouts and 6 survivors under it.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-59/coverage-after/SqlSource.Tool.Tests` (7,092 tests, 4 min 42 s): 193 of 193 lines, before and after, the same figure in each file.
- [x] **Step 3:** Run time of the coverage runs of `tests/SqlSource.Tool.Tests`: 3 min 10 s before (7,089 tests) and 4 min 42 s after (7,092 tests); the phase adds three rows and no test, and the difference is the machine's load.
- [x] **Step 4:** The pull request gives the change, the compare result, the coverage figures and the run times.
