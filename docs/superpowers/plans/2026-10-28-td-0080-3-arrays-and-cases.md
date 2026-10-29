# TD-0080 items 10 and 17: one builder of an EquatableArray, and the fixture cases listed whole, implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tests build an `EquatableArray` through `TestSidecars.Of` alone, and the two lists of describer fixture cases fail on a case they leave out.

**Architecture:** `TestModels.Array` of `tests/SqlSource.Tests/Generation/`, the wrapper `Of` in `Generator/QueryTypesTests.cs` and the private `Array` of `Settings/QuerySettingsTests.cs`, all `new(ImmutableArray.Create(items))`, go; their callers call `TestSidecars.Of` of `tests/SqlSource.Testing/Snapshot/`, which RoslynFloor can reach.  `Cases_OfPostgres_*` and `Cases_OfSqlServer_*` of `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs` compare the cases found for their engine with their list by `ShouldBe(..., ignoreOrder: true)`, in place of a `ShouldContain` per name.

**Tech Stack:** .NET 10 SDK, xunit v3, Shouldly.

**Spec:** `docs/tech-debt/TD-0080-duplicated-arrangements-and-uneven-conventions-in-tests.md`, items 10 and 17 of "Problem" and of "Proposed fix".

## Global Constraints

- No test checks anything different afterwards, but the two case lists, which now also fail on a case they do not name.
- The two tests are renamed `Cases_OfPostgres_AreTheListedCases` and `Cases_OfSqlServer_AreTheListedCases`, since they no longer only hold every case.
- The procedures of adding a describer fixture case, in `tests/DatabaseTests/Postgres/README.md` and `tests/DatabaseTests/SqlServer/README.md`, say to add its name to the list.
- Every commit passes `./pre-commit-validation.sh`; the push passes `--full`.  Only items 10 and 17 of TD-0080 are struck.

## Review Focus

- A build that copied no fixture: the comparison fails on the empty set, as the old test did.
- `ConversionPlannerTests.Of<T>()`, which gives `EquatableArray<T>.Empty` and builds nothing, is not a builder of items and stays; its `Of(params Converter[])` sorts before it builds and calls `TestSidecars.Of`.

---

### Task 1: Items 10 and 17

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/TestModels.cs`, its callers under `tests/SqlSource.Tests/`, `tests/SqlSource.Tests/Generator/QueryTypesTests.cs`, `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs`, `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs`, the two readmes above, TD-0080.

- [ ] **Step 1:** Delete the three builders and call `TestSidecars.Of`, adding `using SqlSource.Testing.Snapshot;` where a file lacks it.
- [ ] **Step 2:** Rewrite the two case tests as a whole comparison of `Found.Where(found => found.Engine == "<engine>").Select(found => found.ToString())` with the list.
- [ ] **Step 3:** Run `dotnet test` on `tests/SqlSource.Tests`, `tests/SqlSource.Tests.RoslynFloor` and `tests/SqlSource.Tool.Tests`; expected: all pass, with the same counts.
- [ ] **Step 4:** Update the two readmes and strike items 10 and 17; `./format.sh`, `./pre-commit-validation.sh`, commit.
