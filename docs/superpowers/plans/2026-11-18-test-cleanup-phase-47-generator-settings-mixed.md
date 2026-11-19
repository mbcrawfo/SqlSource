# Test cleanup phase 47: generator settings mixed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Apply the seven findings of `tests/SqlSource.Tests/Settings` in `QuerySettingsTests.cs` and `TypeOverrides/TypeKeysTests.cs`: delete three tests, fold two facts into the theory that holds their cases, and strengthen three tests so that each survivor its finding names is killed.

**Architecture:** Test-only change.  The guard is the `generator-settings` mutation scope and the line coverage of `src/SqlSource/Settings/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-47-generator-settings-mixed-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the tests the findings name change; no source change.
- `Over_EachMember_ComesFromThisLevelWhenItHasOne` keeps its two `ShouldBeSameAs` assertions on `SettingsLevel.None`: it is the cover of 1-14.
- `TryNormalizeEach_Key_IsWhatEachEnginesReadingGives` keeps every row: it is the cover of 1-19 and 1-20.  Its `?:` only derives the value one assertion compares, so it is on neither list of TD-0089, and the fold adds no branch: TD-0089 does not change.
- The epic's phase table: change only row 47.

Checked against the spec on `5a89e46d`.  Every finding still reads as the review wrote it: `SettingsLevel.Over`'s `??` lines are 119-132, `QuerySettings.Resolve`'s model lines 106-111, and `TypeKeyText.TrySplit`'s `return false` after a `.` is line 49.  The last change to `src/SqlSource/Settings` or `tests/SqlSource.Tests/Settings` is p46's `42c90223` (`git diff 42c90223 HEAD` on both is empty), so p46's "after" report is this phase's "before".  No document names a test of the phase.

---

### Task 0: The guard's "before" half

- [x] Copy `reports/p46-generator-settings-after.json` to `artifacts/test-cleanup/phase-47/generator-settings/before/reports/mutation-report.json`.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-47/coverage-before/SqlSource.Tests`, timed, with the load beside it.
  9230 tests, 9 min 2 s, load 8 rising to 57.
- [x] `kills` on the before report for each of the nine tests the spec names; each named survivor is listed as surviving.
  `SettingsLevel.cs:119:22` (remove right), `QuerySettings.cs:106:13` and `TypeKeyText.cs:49:24` survive in it.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs`
- Modify: `tests/SqlSource.Tests/Settings/TypeOverrides/TypeKeysTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 47)

- [x] **1-08:** delete `QuerySettingsTests.Resolve_WithoutListPadding_PadsLists`.
- [x] **1-14:** delete `QuerySettingsTests.Over_LibraryTypesOfOnlyOneLevel_AreThatLevelsItself`; the `Libraries` helper stays, used by other tests.
- [x] **1-19:** delete `TypeKeysTests.TryNormalizePostgres_SysSchema_IsAUsersSchema`.
- [x] **1-20:** delete `TryNormalizeEach_ArrayOfAUsersSchema_IsPostgreSqlsAlone` and `TryNormalizeEach_EscapedKeyOfAUsersSchema_IsRewrittenForEachEngine`; their comments go above the theory's rows `dbo.U&"Em\0061il"` and `dbo.Email[]`.
- [x] **1-09:** `Over_EachMember_ComesFromThisLevelWhenItHasOne` builds two levels, `near` and `far`, that set each member that resolves whole (`Output`, `Database`, the two suffixes, `ModelNamespace`, the two model types, `CollectionType`, `NestedClass`, `MemberLocation`, `Visibility`) to different values, and asserts `near.Over(far)` is `near` and `Sort.Over(far)` is `far` with `Sort`'s parameters.  Its three assertions stay.
- [x] **1-11:** `Resolve_EachModelSetting_ComesFromTheMostSpecificLevelThatHasIt` resolves markers that set all six over the file, asserting the markers' six, and `SettingsLevel.None` over the file, asserting the file's six.
- [x] **1-18:** add the row `int4.` to `TryNormalizePostgres_AnythingElse_IsNotAKey`.
- [x] Row 47 of the epic's table: `done`, since this is the pull request that merges.
- [x] `./format.sh`, then the Settings tests pass: `dotnet test --project tests/SqlSource.Tests -- --filter-class 'SqlSource.Tests.Settings.*'`.
  913 tests pass (917 before: five removed, one row added).

### Task 2: The guard's "after" half

- [x] The mutation run with output `.../after`, then `tools/mutation-test.sh compare`: nothing `LOST` and nothing `NOT COMPARABLE` but known flips (notes: `GeneratorParameterList.cs` 121:20, 122:13; `TypeOverrideGrammar.cs:254:17` and the other Timeout flips), each hand-checked with `kills`; each survivor the spec names (`SettingsLevel.cs:119:22`, `QuerySettings.cs:106:13`, `TypeKeyText.cs:49:24`) is `Killed`.
  10 min 12 s (Stryker), load 21 falling to 14.  1103 detected before, 1101 still detected, 0 lost, 2 not comparable, both Killed to Timeout and still detected: `GeneratorParameterList.cs:262:17` (post-increment to post-decrement), killed before only by `GeneratorParameterListTests.Words_AnyText_GivesEachWordWithWhereItStarts`, and `SortedKeyedList.cs:46:20` (conditional false, a known flip), killed before only by `QuerySettingsTests.Over_TypeOverrides_MergePerKey`; no test this phase removes or changes killed either.  The three named survivors are `Killed`.
- [x] Coverage into `artifacts/test-cleanup/phase-47/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Settings/**/*.cs` falls.
  1924/1948 lines over the scope's 31 files, before and after, no file changed; 9226 tests after, 6 min 35 s, load 11 to 10.
- [x] Copy the after report to `reports/p47-generator-settings-after.json`; the pull request gives the changes, the compare result, the coverage figures, the run times and each removed test beside its cover.
  Saved at `reports/p47-generator-settings-after.json`.

### Task 3: Validate and review

- [ ] `./pre-commit-validation.sh` before the commit, `--full` before the push.
- [ ] `branch-review:review --base origin/main` until clean.
