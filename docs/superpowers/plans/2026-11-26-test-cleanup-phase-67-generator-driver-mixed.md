# Test cleanup phase 67: generator driver mixed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove two driver tests of `tests/SqlSource.Tests/Generator` that others already prove, fold one fact and seven rows into what stays, and make three tests assert the rule each is about, and not a whole generated file or an order no rule states.

**Architecture:** Two tests deleted, one fact folded into its theory, seven rows dropped from four theories, three tests loosened.  No source or helper changes.  The guard is the `generator-root` mutation scope, the coverage of the files under its `mutate` glob, `src/SqlSource/*.cs`, RoslynFloor, and, for the five findings mutation cannot guard, their covers and the test project's run time.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-67-generator-driver-mixed-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Only the tests the spec names change.  `Users` and `UserEntry` of `PlacementTests` have other readers and stay.
- `MethodTests.Run_MethodsQuery_IsAnExtensionMethodOfTheTypesExtensionClass` is the cover of 6-01, so its loosened form keeps the internal extension class, the internal method and its `this global::System.Data.Common.DbConnection connection` parameter.
- The epic's phase table: change only row 67.
- Every changed file is compiled again by `tests/SqlSource.Tests.RoslynFloor`, so it uses only what Roslyn 4.14.0 has.

Checked against the spec on `e2e419b7`: each of the ten tests still reads as its finding says, and each named cover is there (`MethodTests`' extension-method fact and its placement theory, `SettingLevelTests.Run_MemberLocationInTheHeader_PlacesTheMethod` row `""`, `QueryTypesTests.Run_Int4_IsIntUnderEachDriver` row `"10"`, whose `Int` leaves `PropertyType.Database` at null and is compared by the record's equality, `PostgresTypeMapTests`' table rows and cast test, `LanguageSupportTests` and `GeneratedSourceWarningTests` rows 8 and 9, and `PathResolverTests.Resolve_TypeWithProblems_KeepsThemBeforeItsOwn`).  No document names a test of the phase; `TD-0089` names another theory of `MethodTests`, and the folder's `AGENTS.md` names `QueryTypesTests` as a class that stays here, both still true.  p66's "after" report cannot be this phase's "before", since `main` has added tests to the scope's folder since it ran (#405-#423), so the "before" is a run of its own.

## Review Focus

Each kept test still fails when the rule it names breaks; each removed test or row has its cover; the loosened tests no longer fail on a change to what their findings call incidental.

---

### Task 0: The guard's "before" half

- [x] Run time of `dotnet test --project tests/SqlSource.Tests`: 9,829 tests in 7 min 16 s and, run again at once, 6 min 10 s, on `e2e419b7` with other work running on the machine.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-67/coverage-before/SqlSource.Tests` (9,829 tests, 6 min 13 s): 963 of 971 lines over the 11 files of `src/SqlSource/*.cs`.
- [x] `tests/SqlSource.Tests.RoslynFloor`: 2,251 tests pass, in 5 min 29 s.
- [x] Mutation into `artifacts/test-cleanup/phase-67/generator-root/before` (Windows, on `e2e419b7`): 238 mutants tested, 209 killed, 6 survived, 23 timeouts; the run took 2 h 21 min.
- [x] `kills` on that report: no test of the phase kills a mutant alone.

  | Test | Rows | Kills | Alone | Timeouts under it |
  |----|----|----|----|----|
  | `MethodTests.Run_MethodsQuery_IsAnExtensionMethodOfTheTypesExtensionClass` | 1 | 58 | 0 | 12 |
  | `MethodTests.Run_MethodOnTheType_TakesThePlaceOfTheConstant` | 1 | 61 | 0 | 12 |
  | `MethodTests.Run_EachTypePlacement_PutsTheMethodOnTheType` | 3 | 57 | 0 | 12 |
  | `PlacementTests.Run_MethodsQueryWithNoLocation_IsAnInternalMethodOfTheExtensionClass` | 1 | 58 | 0 | 12 |
  | `PlacementTests.Run_FileOfSeveralLocations_PutsEachQueryWhereItSays` | 1 | 43 | 0 | 8 |
  | `QueryTypesTests.Run_ColumnOfTheBaseMap_HasNoBaseMapTypeOfItsOwn` | 1 | 34 | 0 | 12 |
  | `QueryTypesTests.Run_TypeNoNpgsqlReads_IsSqlsrc246WithTheCastToText` | 5 | 44 | 0 | 12 |
  | `QueryTypesTests.Run_TypeTheToolNeverWrites_IsSqlsrc246WithDescribe` | 3 | 44 | 0 | 12 |
  | `TypeDiagnosticsTests.Run_ProjectOnALanguageVersionOlderThanCSharp12_IsAnErrorAtEachAttribute` | 4 | 43 | 0 | 7 |
  | `TypeDiagnosticsTests.Run_NestedLocationAndAMemberNamedSql_IsAnErrorAtTheAttribute` | 4 | 45 | 0 | 7 |
  | `TypeDiagnosticsTests.Run_TypeWithSeveralProblems_ReportsThemAll` | 1 | 31 | 0 | 7 |

  The fact `Run_MethodOnTheType_TakesThePlaceOfTheConstant` kills four mutants its theory does not, `SqlSourceGenerator.cs:66:9`, `:893:9`, `:899:21` and `:901:21`, the registration and `AddSource` of the type's file, as the verifier found at older lines; the fold gives the theory the read of that file that kills them.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tests/Generator/MethodTests.cs`
- Modify: `tests/SqlSource.Tests/Generator/PlacementTests.cs`
- Modify: `tests/SqlSource.Tests/Generator/QueryTypesTests.cs`
- Modify: `tests/SqlSource.Tests/Generator/TypeDiagnosticsTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 67's state)

- [x] **Step 1 (F-SqlSource.Tests.Generator-6-01, delete):** remove `PlacementTests.Run_MethodsQueryWithNoLocation_IsAnInternalMethodOfTheExtensionClass`.
- [x] **Step 2 (6-03, delete):** remove `QueryTypesTests.Run_ColumnOfTheBaseMap_HasNoBaseMapTypeOfItsOwn`.
- [x] **Step 3 (5-02, fold):** add `run.Sources[GeneratorHarness.SampleFile].ShouldNotContain("GetUser")` to `MethodTests.Run_EachTypePlacement_PutsTheMethodOnTheType`, with the fact's comment above the theory, and remove `Run_MethodOnTheType_TakesThePlaceOfTheConstant`.
- [x] **Step 4 (6-04, fold):** drop rows `pg_snapshot`, `txid_snapshot` and `pg_node_tree` of `QueryTypesTests.Run_TypeNoNpgsqlReads_IsSqlsrc246WithTheCastToText`; keep the two `aclitem` rows.
- [x] **Step 5 (6-05, fold):** drop row `("void", "base")` of `QueryTypesTests.Run_TypeTheToolNeverWrites_IsSqlsrc246WithDescribe`.
- [x] **Step 6 (8-01, fold):** drop rows 8.0 and 9.0 of `TypeDiagnosticsTests.Run_ProjectOnALanguageVersionOlderThanCSharp12_IsAnErrorAtEachAttribute`.
- [x] **Step 7 (8-02, fold):** drop the constant and method rows of `TypeDiagnosticsTests.Run_NestedLocationAndAMemberNamedSql_IsAnErrorAtTheAttribute`.
- [x] **Step 8 (5-01, loosen):** `MethodTests.Run_MethodsQuery_IsAnExtensionMethodOfTheTypesExtensionClass` asserts the extension class's summary line and declaration, and the method's `internal static` declaration with its `this DbConnection connection` parameter; the whole-file `ShouldBe` and the row buffer's line go.
- [x] **Step 9 (6-02, loosen):** `PlacementTests.Run_FileOfSeveralLocations_PutsEachQueryWhereItSays` asserts each constant inside its class (`OnType` on the type, `GetUser` in `Users`, `InSql` in `Sql`, `GetOrder` in `Orders`) and the order of the three class declarations by their indexes.  A local function takes a class's text from its declaration to its closing brace.
- [x] **Step 10 (8-03, loosen):** `TypeDiagnosticsTests.Run_TypeWithSeveralProblems_ReportsThemAll` asserts the count of two and each prefix by `ShouldContain`, without an order.
- [x] **Step 11:** Set row 67 of the epic's table to `done`.
- [ ] **Step 12:** `./format.sh`, `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [ ] **Step 1:** The mutation run with output `artifacts/test-cleanup/phase-67/generator-root/after`, and `compare`.
- [ ] **Step 2:** Coverage into `artifacts/test-cleanup/phase-67/coverage-after/SqlSource.Tests`, compared over the 11 files of `src/SqlSource/*.cs`.
- [ ] **Step 3:** `tests/SqlSource.Tests.RoslynFloor` passes.
- [ ] **Step 4:** Covers for 5-01, 5-02, 6-01, 8-01 and 8-02, and the run time after.
- [ ] **Step 5:** The pull request gives the change, the compare result, the coverage figures, the covers and the run times.
