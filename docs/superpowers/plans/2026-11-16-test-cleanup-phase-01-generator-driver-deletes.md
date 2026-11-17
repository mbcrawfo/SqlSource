# Test cleanup phase 01: generator driver deletes, implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the 13 tests of `tests/SqlSource.Tests/Generator` that the test review of 2026-11-13 found duplicated by a named cover, with the guard's before and after halves showing nothing lost.

**Architecture:** Deletes only, in ten test files of one folder, plus the helpers only those tests use.  No source changes.  The guard is the `generator-root` mutation scope, the line coverage of its files, the RoslynFloor project, and a list of covers with the run time for the ten findings mutation cannot see.

**Tech Stack:** C#, xUnit v3 on the Microsoft Testing Platform, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** [`docs/superpowers/specs/2026-11-13-test-cleanup-phase-01-generator-driver-deletes-design.md`](../specs/2026-11-13-test-cleanup-phase-01-generator-driver-deletes-design.md), under the epic's "How a phase is done" in [`2026-11-13-test-cleanup-epic-design.md`](../specs/2026-11-13-test-cleanup-epic-design.md).

## Global Constraints

- Every finding is checked against today's code first: none of the ten files under `tests/SqlSource.Tests/Generator` nor `tests/SqlSource.Testing/Generator` changed since the review's commit `36864a86`, and the one change under `src/SqlSource` is a comment of `Generation/Chains/SettingsFileSide.cs`, so every finding holds as written.
- A cover keeps every assertion and row that makes it one.
- No test outside the findings changes, and no new test is written.
- Every file of the folder keeps its first line, the `// Shared: tests/SqlSource.Tests.RoslynFloor ...` header.
- Nothing that builds runs in the checkout while a mutation run does.

## Review Focus

- A private helper, constant or `using` left with no reader after a delete: the build treats the analyzer warning as an error, so `./pre-commit-validation.sh` catches it.
- A comment that introduced a deleted test and now sits over the next one, or a class comment that names a deleted case.
- The `TableModelTests` cover lacking the one check of the deleted test it does not make: no second model file.  Task 1 adds it, as the finding's proposal offers.

---

### Task 1: Delete the 13 tests

**Files:**
- Modify: `tests/SqlSource.Tests/Generator/AttributeConflictTests.cs`, `CachingTypeTests.cs`, `LibraryDiagnosticsTests.cs`, `ParameterTypeTests.cs`, `RowBufferTests.cs`, `SettingsFileBuildFoldersTests.cs`, `SettingsFileTests.cs`, `SharedModelTests.cs`, `SidecarTests.cs`, `TableModelTests.cs`, all under `tests/SqlSource.Tests/Generator/`

- [ ] **Step 1: Remove each test with the comment above it**

| Finding | Test removed | Also removed |
|----|----|----|
| 1-01 | `AttributeConflictTests.Build_WarningsAsErrors_HasNoConflictForAGeneratedType` | |
| 2-03 | `CachingTypeTests.JsonLibraryTypesMarker_Edit_RetypesOnlyItsFile` | `UsersWithJsonMarker` |
| 4-01 | `LibraryDiagnosticsTests.Run_OnInSettingsFileWithoutPlugin_IsReportedAtTheMember` | |
| 5-06 | `ParameterTypeTests.Run_ParamMarker_GivesTheParameterItsType` | |
| 6-07 | `RowBufferTests.Run_ArrayMethodUnderArrayPoolOff_WritesTheMethodWithAListAndNoBuffer` | `ListUsersMethods` |
| 6-08 | `SettingsFileBuildFoldersTests.Run_CompileItemUnderTheOutputFolder_ReachesNoSettingsFile` | |
| 6-09 | `SettingsFileTests.Run_DialectInTheFolder_ParsesTheFileWithIt` | |
| 6-10 | `SettingsFileTests.Run_DialectInAFolderAbove_ParsesTheFileWithIt` | |
| 6-11 | `SettingsFileTests.Run_DialectMarker_OverridesTheFolder` | |
| 6-12 | `SettingsFileTests.Run_OutputInTheFolder_Applies` | |
| 7-01 | `SidecarTests.Run_MssqlFileWithoutASidecar_IsSqlsrc241AtEachQuery` | |
| 7-02 | `SharedModelTests.Run_InternalMappedTypeInAPublicModel_IsSqlsrc251AtTheQuery` | |
| 7-03 | `TableModelTests.Run_TwoTablesOfOneModelName_ShareOneModelThatNamesBothTables` | |

Then grep each file for every private member the removed tests used; remove any left with no reader (`SettingsFileTests.Postgres` is one: only 6-09 and 6-10 read it).

- [ ] **Step 2: Add to `TableModelTests.Run_ThreeTablesOfOneModelName_AreNamedOnceEach`, after `run.ShouldBeClean();`**

```csharp
ModelFiles(run).ShouldBe(["App.ABCDto-Model.g.cs"]);
```

- [ ] **Step 3: Run the folder's tests and RoslynFloor**

Run: `dotnet test --project tests/SqlSource.Tests -- --filter-query "/*/SqlSource.Tests.Generator/**"` (or the whole project), then `dotnet test --project tests/SqlSource.Tests.RoslynFloor`
Expected: both pass.

### Task 2: Guard, documents and the epic's row

- [ ] **Step 1: Run the guard's after half** as the spec's "The guard" gives it: the `generator-root` mutation run to `artifacts/test-cleanup/phase-01/generator-root/after`, `compare` against `before`, the coverage run to `coverage-after`, RoslynFloor and the run time.  Expected: `compare` lists nothing `LOST` and nothing `NOT COMPARABLE`, or only a kill that `kills` on the before report shows no removed or changed test made; no file under `*.cs` of `src/SqlSource` loses line coverage.
- [ ] **Step 2: Documents.**  The spec's list (`docs/architecture/generator.md` 268, 372, 793, 854, 858 and `tests/SqlSource.Tests/Generator/AGENTS.md` 17) names classes that stay and no removed test: no change.
- [ ] **Step 3: Set phase 01's row of the epic's table to `done`** (the pull request that merges sets it so), and record there any finding left out.
- [ ] **Step 4: `./format.sh`, `./pre-commit-validation.sh`, commit.**
