# TD-0080 items 4 and 8: the generator tests' shared helpers, implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The helpers that the generator's tests copy from class to class live once, in `tests/SqlSource.Testing/Generator/`, and every copy goes.

**Architecture:** Each helper moves to the library type that already holds its kind of code: the reasons of a step to `StepReasons`, the names and texts of the caching scene's methods to `CachingScene`, the check of a run's compilation to `GeneratorRun`, the column types a step gave to `StepOutputs`, the joining of a continued line to `GeneratedFile`, and the type of `pg_catalog` to `ModelRuns.Catalog`, widened to the copies' parameters.  The library stays on Roslyn 4.8.0, since RoslynFloor compiles `tests/SqlSource.Tests/Generator/` again against it.

**Tech Stack:** .NET 10 SDK, xunit v3 on Microsoft.Testing.Platform, Shouldly, Roslyn 4.8.0 in the library.

**Spec:** `docs/tech-debt/TD-0080-duplicated-arrangements-and-uneven-conventions-in-tests.md`, items 4 and 8 of "Problem" and "4. and 8." of "Proposed fix".

## Global Constraints

- No test checks anything different afterwards, but that a helper which asserted "every reason is X" now also asserts there is at least one, as `ShouldAllBeCached` of the caching classes already did.
- The helpers stay `internal`, use only API of Roslyn 4.8.0 and C# 12, and only the packages `SqlSource.Testing` references.
- Every commit passes `./pre-commit-validation.sh` after `./format.sh`; the push passes `./pre-commit-validation.sh --full`.
- Only items 4 and 8 of TD-0080 are struck, as `~~item~~  Done.` in "Problem" and in "Proposed fix".
- `tests/README.md` names the library's types: a new member that changes what a type is for updates its clause there.

## Review Focus

- A step whose reasons are empty: `ShouldAllBe(reasons, reason)` fails on it, where Shouldly's `ShouldAllBe` passed.  A test that relied on the empty pass is a weak test and gets an explicit `ShouldBeEmpty` or another step, decided case by case when the suite runs.
- `ColumnsOf` over a run of several queries: without a name it fails on more than one query, as the copies did; with a name it picks that query.
- `Joined` in `QueryMethodWriterTests` uses its own `Continued` constant elsewhere; it keeps the constant or takes `GeneratedFile.Continued`, never two marks.
- RoslynFloor compiles every changed file of `Generator/` again: it must build and pass.

---

### Task 1: Item 4, the caching tests' tail

**Files:**
- Modify: `tests/SqlSource.Testing/Generator/StepReasons.cs`, `tests/SqlSource.Testing/Generator/CachingScene.cs`, every `tests/SqlSource.Tests/Generator/Caching*Tests.cs`

**Interfaces:**
- Produces, in `StepReasons`: `public static void ShouldAllBe(IncrementalStepRunReason[] reasons, IncrementalStepRunReason reason)` (at least one, and every one is `reason`) and `public static void ShouldAllBeCached(IncrementalStepRunReason[] reasons)`.
- Produces, in `CachingScene`: `public const string UserMethods = "App.UserQueries-Methods.g.cs"`, `public const string OrderMethods = "App.OrderQueries-Methods.g.cs"`, `public static Dictionary<string, IncrementalStepRunReason> UsersAndOrders(IncrementalStepRunReason users, IncrementalStepRunReason orders)` (the copies' `Expect`) and `public static string MethodsOf(GeneratorDriverRunResult result, string file)`.

- [ ] **Step 1:** Add the members; delete the copies from `CachingConverterTests` and `CachingMethodTests`, and rename their `Expect` calls to `UsersAndOrders`.
- [ ] **Step 2:** Replace every `X.ShouldAllBe(reason => reason == IncrementalStepRunReason.Cached)` of the `Caching*Tests` classes with `ShouldAllBeCached(X)`, and the same form with another reason with `ShouldAllBe(X, IncrementalStepRunReason.<Reason>)`.
- [ ] **Step 3:** Run `dotnet test --project tests/SqlSource.Tests --filter-class '*Caching*'` and the same on `tests/SqlSource.Tests.RoslynFloor`; expected: all pass.  A failure on an empty set of reasons is handled as Review Focus says.
- [ ] **Step 4:** `./format.sh`, `./pre-commit-validation.sh`, commit.

### Task 2: Item 8, the small helpers

**Files:**
- Modify: `tests/SqlSource.Testing/Generator/GeneratorHarness.cs` (`GeneratorRun`, `GeneratedFile`), `StepOutputs.cs`, `ModelRuns.cs`; the copies in `Generator/ConverterMethodTests.cs`, `MethodConflictTests.cs`, `MethodDiagnosticsTests.cs`, `MethodTests.cs`, `RowBufferTests.cs`, `ColumnTypeTests.cs`, `QueryTypesTests.cs`, `TypeOverrideLevelTests.cs`, `SettingsFileTests.cs`, `NpgsqlProbeTests.cs` of `tests/SqlSource.Tests`, and `Generation/Emitting/QueryMethodWriterTests.cs`, `Generation/Types/Maps/PostgresTypeMapTests.cs`

**Interfaces:**
- `GeneratorRun.Problems()`: `IReadOnlyList<string>`, after checking that the compilation has no error and the generated code no warning.
- `GeneratedFile.Continued = "↩"` and `GeneratedFile.Joined(string text)`: each line that ends in the mark joined to the next, whose indentation goes.
- `StepOutputs.ColumnsOf(GeneratorRun run, string? queryName = null)`: `PropertyType[]`, the column types of the run's one query, of the one value `TrackingNames.FileTypes` gave, or of the query of that name among its values.  `SettingsFileTests.ColumnOf` becomes `ColumnsOf(run).ShouldHaveSingleItem()`.
- `ModelRuns.Catalog(string internalName, string kind = SidecarValues.Kind.Base, PostgresType? subtype = null)`.

- [ ] **Step 1:** Add the members and delete each copy, calling the shared one.
- [ ] **Step 2:** Run `dotnet test --project tests/SqlSource.Tests` and `--project tests/SqlSource.Tests.RoslynFloor`; expected: all pass, with the same count as before.
- [ ] **Step 3:** Update `tests/README.md` where it says what `GeneratorRun`, `GeneratedFile`, `StepOutputs`, `CachingScene` and `StepReasons` hold; strike items 4 and 8 of TD-0080.
- [ ] **Step 4:** `./format.sh`, `./pre-commit-validation.sh`, commit.
