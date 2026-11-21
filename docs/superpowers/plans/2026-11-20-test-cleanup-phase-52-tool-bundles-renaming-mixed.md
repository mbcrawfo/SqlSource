# Test cleanup phase 52: tool bundles renaming mixed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply the two findings of phase 52 to `tests/SqlSource.Tool.Tests/Bundles/Renaming/NameMappingTests.cs`: delete `IsMapped_UnknownSpelling_IsFalse`, which `IsMapped_AnotherCaseOfAMappedName_IsTrue` covers, and make `NameClasses_EveryMember_HasAPrefixAndAWord` assert each member's prefix and word, not only that they are distinct.

**Architecture:** Test-only change in `NameMappingTests.cs`.  No source, helper or document change but the epic's row 52.  The guard is the `tool-bundles-renaming` mutation scope and the coverage of `src/SqlSource.Tool/Bundles/Renaming/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-52-tool-bundles-renaming-mixed-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the two named tests change; the cover, `IsMapped_AnotherCaseOfAMappedName_IsTrue`, keeps its `IsMapped("x", false).ShouldBeFalse()`.  The epic's phase table: change only row 52.
- No document names a test of the phase (the spec's search); no document changes.

Checked against the spec on `ec4053be`: each test reads as the findings say (`IsMapped_UnknownSpelling_IsFalse` is the one line `Mapping().IsMapped("x", false).ShouldBeFalse()`; the cover asserts the same on a mapping that holds a name; `NameClasses_EveryMember_HasAPrefixAndAWord` asserts only the distinct counts and the throws for 99), and `NameClass.cs` is unchanged: every prefix is a lower-case letter and `_`, and every word is its member's name in lower case.  Phase 51, of the same scope, merged just before; its "after" report was written before its last commit to the scope's tests, so this phase runs its own "before".

Where the code settles what the spec leaves open:

- **F-…-1-01:** of the proposal's two forms of the word, the stronger, the member's name in lower case, holds for every member today, so the test asserts it.  The proposal's shape of the prefix, a lower-case letter and `_`, does not make a swapped prefix fail, as its last clause asks, so the test also pins each member's prefix (the review's R1-F1); it cannot be the member's first letter, since `Type` is `y_` and `Token` is `k_`.

## Review Focus

The strengthened test fails on an emptied or swapped prefix or word, and still asserts the distinct counts and the throws.

---

### Task 0: The guard's "before" half

- [x] Coverage of `tests/SqlSource.Tool.Tests` into `artifacts/test-cleanup/phase-52/coverage-before/SqlSource.Tool.Tests` (7,085 tests, 3 min 26 s).
- [x] Mutation: `tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-52/tool-bundles-renaming/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Bundles.Renaming.' 'Bundles/Renaming/**/*.cs'`: 1,010 killed, 154 survived, 19 timeouts, 43 no coverage, 10 min 22 s.  `kills` for the two tests saved beside it: `NameClasses_EveryMember_HasAPrefixAndAWord` 0 kills, with the ten words of `NameClass.cs` 48:33 and 50:33-58:35 surviving under it; `IsMapped_UnknownSpelling_IsFalse` 2 kills, 0 alone, both `RenamingKey.cs:29:9`, killed by 134 and 135 other tests.  Both as the spec measured.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/Bundles/Renaming/NameMappingTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 52's state)

- [x] **Step 1 (F-SqlSource.Tool.Tests.Bundles.Renaming-1-05):** delete `IsMapped_UnknownSpelling_IsFalse`.
- [x] **Step 2 (F-SqlSource.Tool.Tests.Bundles.Renaming-1-01):** in `NameClasses_EveryMember_HasAPrefixAndAWord`, assert of each member that `PrefixOf` matches `^[a-z]_$` and is its own of the twelve prefixes, pinned in the test, and `WordOf` matches `^[a-z]+$` and is the member's name ignoring case, beside the distinct counts and the throws.  `ToLowerInvariant` is refused by CA1308, so the lower case and the name are asserted apart.
- [x] **Step 3:** Set row 52 of the epic's table to `done`.
- [x] **Step 4:** `./format.sh`, then `NameMappingTests` pass (35 tests).
- [x] **Step 5:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-52/tool-bundles-renaming/after`: 1,020 killed, 144 survived, 19 timeouts, 43 no coverage, 10 min 4 s.  `compare`: 1,029 detected before, 1,029 still detected, 0 lost, 0 not comparable.  `NameClass.cs:48:33` is `Killed`, and so are the other nine words of 50:33-58:35.  The run was made again, 12 min 0 s, once review R1-F1 pinned the prefixes, with the same figures.  `kills` after: `NameClasses_EveryMember_HasAPrefixAndAWord` 24 kills, 10 alone, no survivor under it.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-52/coverage-after/SqlSource.Tool.Tests` (7,084 tests, 3 min 27 s): 3,611 of 3,714 lines over the 38 files of `src/SqlSource.Tool/Bundles/Renaming/`, before and after, the same lines in each file.
- [ ] **Step 3:** The pull request gives the changes, the compare result and the coverage figures.
