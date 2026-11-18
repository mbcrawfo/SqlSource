# Test cleanup phase 46: generator settings strengthen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Strengthen ten tests of `tests/SqlSource.Tests/Settings` so that each survivor its finding names is killed.

**Architecture:** Test-only change: rows added to theories, two facts given inputs of two libraries, one fact given an allocation measure, and the value of no member of `DatabaseEngine` moved to the boundary of its check.  The guard is the `generator-settings` mutation scope, the line coverage of `src/SqlSource/Settings/**/*.cs`, and, for the findings mutation cannot guard (1-04, 1-05, 1-16), the test project's run time.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-46-generator-settings-strengthen-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the ten tests the findings name change; no source change.
- `LibrarySettingsTests.Over_OneNull_IsTheOtherItself` keeps both `ShouldBe(entries)` assertions: it is the cover of F-SqlSource.Tests.Settings-1-14 (phase 47).
- None of the changed theories branches on its row, so TD-0089 does not change.
- The epic's phase table: change only row 46.

## Review Focus

- The allocation measure of 1-05 must not count the JIT or static initialisers: call both `Over`s once before measuring, and keep every assertion (which boxes) outside the measured window.
- Lone surrogates and U+FFFF as `char` arguments of `[InlineData]`: written as `\uXXXX` escapes, and the rows must still be discovered and run one by one.
- The two-library inputs must be sorted by `SqlLibrary` where `LibrarySettings.Over` requires it, and written out of that order where the reader must sort them.

Checked against the spec on `55220289`.  Every finding still reads as the review wrote it: `ClrTypeName.AsWritten`, `LibrarySettings.Over` (its second null check is now at lines 29-31), `PerEngine`'s two checks, `SettingsLevelReader.IndexOfLibrary`, `TypeOverrideGrammar.BracketHelp` and `UnicodeEscape` are unchanged in substance.  No merged phase changed `src/SqlSource/Settings` or `tests/SqlSource.Tests/Settings` since p27 (`git log 04af0b6f..origin/main` on both is empty), so p27's "after" report is this phase's "before".

`Over_OneNull_IsTheOtherItself` measures allocation to tell the level itself from a copy of it; it is no allocation budget, so `tests/AGENTS.md`'s list of budgets does not name it, and no document changes.

---

### Task 0: The guard's "before" half

- [x] Copy `reports/p27-generator-settings-after.json` (tree of `04af0b6f`) to `artifacts/test-cleanup/phase-46/generator-settings/before/reports/mutation-report.json`.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-46/coverage-before/SqlSource.Tests`, timed, with the load beside it: 9218 tests, 6 min 26 s, load 3 rising to 35.
- [x] `kills` on the before report for each of the ten tests; each named survivor is listed as surviving.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tests/Settings/ClrTypes/ClrTypeNameTests.cs`
- Modify: `tests/SqlSource.Tests/Settings/Libraries/LibrarySettingsTests.cs`
- Modify: `tests/SqlSource.Tests/Settings/PerEngineTests.cs`
- Modify: `tests/SqlSource.Tests/Settings/SettingsLevelReaderTests.cs`
- Modify: `tests/SqlSource.Tests/Settings/TypeOverrides/TypeOverrideGrammarTests.cs`
- Modify: `tests/SqlSource.Tests/Settings/TypeOverrides/UnicodeEscapeTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 46's state)

- [x] **Step 1 (1-01):** `AsWritten_Text_LeavesOutGlobalBeforeANameOfSeveralParts` gains the row `("List<global::App.UserId>", "List<App.UserId>")`.
- [x] **Step 2 (1-04):** `LibrarySettingsTests` gains `Entry(SqlLibrary library, LibraryState state, SettingPlace place)`, which the NodaTime `Entry(LibraryState, SettingPlace)` calls, so the tests outside the findings keep their text.  `Over_NearAndFarLevels_ReplacesOnlyTheLibrariesTheNearLevelNames` merges `near = [NodaTime Off Near, Json Off Near]` and `far = [NodaTime On SettingsFile, NetTopologySuite On SettingsFile]`: the two levels are `offAndJson` and `onAndNetTopologySuite` (S2234 rejects `near` and `far` passed in the other order); `Over(offAndJson, onAndNetTopologySuite)` (near over far) is `[NodaTime Off Near, NetTopologySuite On SettingsFile, Json Off Near]`, and `Over(onAndNetTopologySuite, offAndJson)` is `[NodaTime On SettingsFile, NetTopologySuite On SettingsFile, Json Off Near]`.
- [x] **Step 3 (1-05):** `Over_OneNull_IsTheOtherItself` keeps both `ShouldBe(entries)`, calls both `Over`s once to warm up, then measures `GC.GetAllocatedBytesForCurrentThread()` across one call of each and asserts `0`; a comment says that a copy of the other level would allocate.
- [x] **Step 4 (1-06, 1-07):** `With_ValueOfNoMember_Throws` and both containers of `Indexer_ValueOfNoMember_Throws` use `(DatabaseEngine)DatabaseEngines.All.Length` in place of `(DatabaseEngine)7`, held in one `private static readonly DatabaseEngine NoMember` with a comment that it is the first number past the last member, the boundary of the check.  `Only_ValueOfNoMember_Throws` is not a finding's test and stays.
- [x] **Step 5 (1-16):** `Read_LibraryTypesAsMembers_AreReadIntoTheLevel` reads `[new(" json ", " On "), new(" NodaTime ", " Default ")]`; `LibraryTypes` is `[NodaTime Default SettingsFile, Json On SettingsFile]` and `LibraryEntries` is `[(Json, 0), (NodaTime, 1)]`.
- [x] **Step 6 (1-21):** `BracketHelp_KeyInBrackets_ShowsTheFormsThatAreRead` gains the rows `[dbo].[Email]` and `dbo.[Email]`, each with the help of its `=X` row; its comment says a settings file's key comes alone.
- [x] **Step 7 (1-22):** `TryRead_AnythingElse_IsFalse` gains `\\DC00` and `\\DFFF`.
- [x] **Step 8 (1-23):** `TryRead_EscapeOfACharacterOfTheBmp_IsThatCharacterAlone` gains `("\\0001", '\u0001', 5)`, `("\\FFFF", '￿', 5)` and `("\\00ff", 'ÿ', 5)`.
- [x] **Step 9 (1-24):** `TryRead_EscapeOfACharacterOutsideTheBmp_IsItsSurrogates(string text, char high, char low, int next)`: the four rows of U+1F600 pass `'\uD83D', '\uDE00'`, and it gains `("\\+010000", '\uD800', '\uDC00', 8)`, `("\\D800\\DC00", '\uD800', '\uDC00', 10)`, `("\\+10FFFF", '\uDBFF', '\uDFFF', 8)`, `("\\DBFF\\DFFF", '\uDBFF', '\uDFFF', 10)`.
- [x] **Step 10:** Row 46 of the epic's table is `done`.
- [x] **Step 11:** `./format.sh`, then the Settings tests pass: `dotnet test --project tests/SqlSource.Tests -- --filter-class 'SqlSource.Tests.Settings.*'` (917 tests).  The "after" mutation run is the check that each changed test fails on its mutant.
- [x] **Step 12:** `./pre-commit-validation.sh`, then commit.
  Passed on `42c90223`'s tree (19020 tests).

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare`: nothing `LOST` and nothing `NOT COMPARABLE` but known flips (notes: `GeneratorParameterList.cs` 121:20, 122:13; Timeout flips of `ClrTypeName.cs:440:24`, `SortedKeyedList.cs:46:20`, `TypeOverrideGrammar.cs:254:17`, `TypeOverrideMap.cs:134:16`), each hand-checked with `kills`; each survivor the spec names is `Killed`.
  1075 killed, 113 survived, 28 timeouts, 11 min 49 s at load 10-71.  `compare`: 1077 detected before, 1070 still detected, 1 lost, 6 not comparable, every one a flip `notes.md` predicted: `GeneratorParameterList.cs:121:20` LOST, and `ClrTypeName.cs:440:24`, `SortedKeyedList.cs` 45:21 (x2) and 46:20, `TypeOverrideGrammar.cs:254:17` and `TypeOverrideMap.cs:134:16` Killed to Timeout.  `kills` shows no changed test killed any of them but `TypeOverrideGrammar.cs:254:17` `index--`, which `BracketHelp_KeyInBrackets_ShowsTheFormsThatAreRead` killed in p27's run: an infinite loop on its rows with a plain first part, a fast failure on those that start with a bracket, so Killed or Timeout is a race (Timeout in p07's run, Killed in p27's, Timeout here and in a narrow re-run); asked of the owner as Q1.  All ten named survivors are `Killed`, with 20 other survivors of the same lines.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-46/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Settings/**/*.cs` falls.
  1924/1948 lines over the scope's 31 files, before and after; 9218 tests before (6 min 26 s, load 3 to 35), 9230 after (12 min 47 s, load 51 to 73).
- [x] **Step 3:** The test project's run time before and after, interleaved with `--no-build`, the load beside each.
  Without coverage, `--no-build`, one after the other: after 4 min 38 s (load 11 to 5), before 4 min 49 s (load 5 to 6), after 4 min 51 s (load 7 to 10).
- [x] **Step 4:** Copy the after report to `reports/p46-generator-settings-after.json`; the pull request gives the changes, the compare result, the coverage figures, the run times and the covers of 1-04, 1-05, 1-16 (no test or row is removed).
  Saved at `reports/p46-generator-settings-after.json`.
