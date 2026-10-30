# TD-0079 item 2: one sorted search — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every hand-written binary search of `src/` but `TypeOverrideMap.LowerBound` goes through `SortedSearch`, which gains a probe-based exact search and a lower bound, and `ColumnTypeMap` shares `TypeOverrideMap`'s order and part comparison, with no change to what is generated.

**Architecture:** `SortedSearch` exposes its probe interface (`SortedSearch.IProbe`, "how the item at an index sorts against what is looked for") and two generic entries over a `struct` probe, so a search allocates nothing and boxes nothing.  Each caller keeps its own semantics in a small private probe struct beside it.  The two settings maps build their order from one `SortedKeyedList.OrderOf` and compare a part's text with one `TypeKeyText.ComparePart`.

**Tech Stack:** C# (LangVersion preview), `netstandard2.0` generator, xUnit v3 + Shouldly tests.

**Spec:** `docs/tech-debt/TD-0079-duplicated-logic-and-uneven-conventions-in-src.md`, item 2 and Proposed fix 2.

## Global Constraints

- `src/SqlSource` targets `netstandard2.0` only; no new package references; no `allows ref struct`.
- No change to generated code, diagnostics or any behaviour a user can see.
- Searches allocate nothing: probes are `readonly struct`s passed as a generic `TProbe : struct, SortedSearch.IProbe`, never boxed, never a captured lambda per call.
- `TypeOverrideMap.LowerBound` stays as it is (TD-0063: its key is a `ref struct`).
- `MethodPlanner.FindModels` and `FindAssignment` may be moved by `td-0079-planning`: change only the search inside each, wherever the code is when the task runs; keep each edit small.
- Repository rules: root `AGENTS.md`, `src/SqlSource/AGENTS.md` (performance), `tests/AGENTS.md` (Shouldly, `Method_Scenario_Outcome`, `sealed` classes).  Run `./format.sh -- <changed files>` before committing.  Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. A key before every item, after every item, or in an empty list: each search gives what it gave (`-1`, `~0`, `~count`, `count`, `0`).
2. A run of equal keys: a lower bound gives the first of the run (`ModelAssignment.FirstOf`, `ConverterSet.Touching`, the maps' `Find`).
3. `SqlPath.Seek` from a `start` above 0 and with `past` set: steps over a whole subfolder.
4. `SqlBlockText.FindRun` for an offset before the first run gives `-1`, and for equal offsets the last run at or before it.
5. A column-type entry whose key is not well formed sorts as "not before" the origin, as today.

---

### Task 1: `SortedSearch` offers a probe search and a lower bound

**Files:**
- Modify: `src/SqlSource/SortedSearch.cs`
- Test: `tests/SqlSource.Tests/SortedSearchTests.cs`

**Interfaces:**
- Produces:
  - `public interface IProbe { int CompareAt(int index); }`, nested in `SortedSearch`, made public (it is private today).  Negative when the item at the index sorts before what is looked for, zero when equal, positive after.
  - `public static int IndexOf<TProbe>(int count, TProbe probe) where TProbe : struct, IProbe` — an index in `[0, count)` whose `CompareAt` is zero, or `-1`.  It is today's private `Search`, renamed; the two string overloads call it.
  - `public static int LowerBound<TProbe>(int count, TProbe probe) where TProbe : struct, IProbe` — the first index in `[0, count)` whose `CompareAt` is not negative, or `count`.
  - `public static int LowerBound<TProbe>(int start, int count, TProbe probe) where TProbe : struct, IProbe` — the same over `[start, count)`, `start` when `start == count`.
- Update the class summary: the one binary search of the generator's sorted lists, exact or a lower bound; a caller with its own order gives a probe.

- [ ] **Step 1: Write the failing tests** in `SortedSearchTests`, over a private `readonly struct` probe of an `int[]` and a key (`CompareAt(i) => items[i].CompareTo(key)`):
  - `IndexOf_ProbeOfAnItemInTheList_IsItsIndex` — `[1, 3, 5]`, key 3 → 1.
  - `IndexOf_ProbeOfNoItem_IsMinusOne` — `[1, 3, 5]`, keys 0, 4, 6, and an empty list → -1.
  - `LowerBound_KeyInARunOfEqualItems_IsTheFirstOfTheRun` — `[1, 3, 3, 3, 5]`, key 3 → 1.
  - `LowerBound_KeyBetweenItems_IsWhereItWouldGo` — theory: `[1, 3, 5]`, key 0 → 0, 2 → 1, 6 → 3; empty list, key 1 → 0.
  - `LowerBound_FromAStart_SearchesFromThere` — `[1, 3, 3, 5]`, start 2, key 3 → 2; start 3, key 1 → 3; start 4 (the count) → 4.
- [ ] **Step 2: Run them and see them fail** (no such members): `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.SortedSearchTests`.
- [ ] **Step 3: Implement** the members above in `SortedSearch.cs`; the lower bound is the `while (low < high)` halving that `SortedKeyedList.IndexOf` has today.
- [ ] **Step 4: Run the class again and see it pass.**
- [ ] **Step 5: Commit** `SortedSearch offers a probe search and a lower bound`.

### Task 2: The settings maps search through `SortedSearch` and share their order

**Files:**
- Modify: `src/SqlSource/Settings/SortedKeyedList.cs`, `src/SqlSource/Settings/ColumnTypes/ColumnTypeMap.cs`, `src/SqlSource/Settings/TypeOverrides/TypeOverrideMap.cs`, `src/SqlSource/Settings/TypeOverrides/TypeKeyText.cs`
- Test: existing `tests/SqlSource.Tests/Settings/ColumnTypes/ColumnTypeMapTests.cs`, `tests/SqlSource.Tests/Settings/TypeOverrides/TypeOverrideMapTests.cs`, grammar tests; add `tests/SqlSource.Tests/Settings/SortedKeyedListTests.cs` only if no test pins `IndexOf`'s complement (check `TypeOverrideGrammar`/`ColumnTypeGrammar` tests first).

**Interfaces:**
- Consumes: Task 1's `SortedSearch.IProbe`, `LowerBound<TProbe>(int, TProbe)`.
- Produces:
  - `SortedKeyedList.IndexOf` keeps its signature and result (`~where` when absent); its body is a `SortedSearch.LowerBound` over a private probe struct of the list, key, order and key-of, then one `CompareAt` at the result.
  - `public static Comparison<string> SortedKeyedList.OrderOf(Func<string, string, StringComparison, int> compare)` — the order of a map whose keys compare by their parts: `compare` ignoring case, then ordinally to break a tie.  Each map's `KeyOrder` becomes `SortedKeyedList.OrderOf(static (a, b, comparison) => Compare(new KeyParts(a), new KeyParts(b), comparison))`, and each map's private `Order` is deleted.  (A shared `Order` over the parts cannot be generic: both `KeyParts` are `ref struct`s, as TD-0063 explains.)
  - `public static int TypeKeyText.ComparePart(ReadOnlySpan<char> a, ReadOnlySpan<char> b, StringComparison comparison)` — `TextOf(a).CompareTo(TextOf(b), comparison)`; both maps' private `ComparePart` are deleted and call it.
  - `ColumnTypeMap.LowerBound` is replaced by `SortedSearch.LowerBound(map.Count, new OriginProbe(map, origin))`, a private `readonly struct` whose `CompareAt` splits the entry's key and gives `CompareColumn(entry, origin, OrdinalIgnoreCase)` when well formed, else 0.
  - `TypeOverrideMap.LowerBound` is untouched; its comment still names TD-0063.

- [ ] **Step 1: Run the settings tests before the change** and note they pass: `dotnet test --project tests/SqlSource.Tests --filter-namespace "SqlSource.Tests.Settings*"`.
- [ ] **Step 2: Make the changes above.**  Comments say why in one line where the code does not (the probe is a struct so the map is not boxed).
- [ ] **Step 3: Run the settings tests and the generator's type-override and column-type tests** (`--filter-class` `SqlSource.Tests.Generator.ColumnTypeTests`, `SqlSource.Tests.Generator.ColumnMarkerTests`, `SqlSource.Tests.Generator.TypeOverrideLevelTests`) and see them pass.
- [ ] **Step 4: Commit** `The settings maps search through SortedSearch and share their order`.

### Task 3: The parser's and the generation's searches go through `SortedSearch`

**Files:**
- Modify: `src/SqlSource/Parsing/SqlBlockText.cs`, `src/SqlSource/Generation/Paths/SqlPath.cs`, `src/SqlSource/Generation/Converters/ConverterSet.cs`, `src/SqlSource/Generation/Models/ModelAssignment.cs`, `src/SqlSource/Generation/Methods/MethodPlanner.cs` (or wherever `FindModels` now is)
- Test: `tests/SqlSource.Tests/Generation/Models/ModelAssignmentTests.cs`; existing `SqlPathTests`, `ConverterSetTests`, `SqlTextBuilderTests`, `Generator/MethodTests`, `Generator/SharedModelTests`, `Parsing/*`

**Interfaces:**
- Consumes: Task 1's `IProbe`, `IndexOf<TProbe>`, both `LowerBound<TProbe>`.
- Produces:
  - `SqlBlockText.FindRun(int offset, bool bySource)` keeps its contract (last run starting at or before `offset`, or -1): `SortedSearch.LowerBound(runs.Length, probe) - 1`, the probe giving -1 when the run's offset is `<= offset`, else 1.
  - `SqlPath.Seek` keeps its signature: `SortedSearch.LowerBound(start, sortedPaths.Count, probe)`, the probe giving `CompareToFolder(...)`, with 0 turned to -1 when `past` is set.
  - `ConverterSet.LowerBound` and its `Func<int, string>` lambdas (which capture `this` and allocate on each `Touching`) are replaced by two private probe structs: by argument over `Converters`, by return over `Converters` and `_byReturn`, each `string.CompareOrdinal(text at index, typeText)`.
  - `ModelAssignment.FirstOf` keeps its contract: a `SortedSearch.LowerBound` by `ClaimingType` ordinally, then the equality check it has.
  - `public static int ModelAssignment.IndexOf(EquatableArray<ModelAssignment> sorted, string claimingType, string normalizedPath, int queryIndex, ModelRole role)` — the index of the assignment of that query and role in a list ordered by `Order`, or -1.  `Order` and the probe share one private `Compare(ModelAssignment item, string claimingType, string normalizedPath, int queryIndex, ModelRole role)`, so the order is written once.
  - `MethodPlanner.FindAssignment` keeps its signature; its body calls `ModelAssignment.IndexOf` and no longer builds a probe `ModelAssignment`.
  - `MethodPlanner.FindModels` keeps its signature: `SortedSearch.IndexOf(file.Queries.Count, probe)` over a private probe struct comparing `Queries[i].Index` with `queryIndex`, then the name check it has.

- [ ] **Step 1: Write the failing tests** in `ModelAssignmentTests`:
  - `IndexOf_AssignmentInTheList_IsItsIndex` — a list sorted by `ModelAssignment.Order` of two types, two paths and both roles; each assignment's own fields find its index.
  - `IndexOf_NoSuchAssignment_IsMinusOne` — another role, another index, another path and an empty list each give -1.
- [ ] **Step 2: Run them and see them fail**: `dotnet test --project tests/SqlSource.Tests --filter-class SqlSource.Tests.Generation.Models.ModelAssignmentTests`.
- [ ] **Step 3: Make the changes above.**
- [ ] **Step 4: Run the covering tests and see them pass**: the classes listed under Test, plus `--filter-namespace "SqlSource.Tests.Parsing*"` and the allocation budgets (`SqlFileParserAllocationTests`, `PathResolverAllocationTests`, `SettingsFileSetTests`).
- [ ] **Step 5: Commit** `The parser's and the generation's searches go through SortedSearch`.

### Task 4: Documents (done by the controller)

**Files:** `docs/tech-debt/TD-0079-duplicated-logic-and-uneven-conventions-in-src.md`, `docs/tech-debt/README.md` (TD-0079's row only), `docs/tech-debt/TD-0063-lower-bound-of-the-type-override-map-is-a-search-of-its-own.md`, `docs/architecture/generator.md` (paths and column types paragraphs), `src/SqlSource/Settings/AGENTS.md` (the shared-list rule).

- [ ] Strike item 2 and fix 2 through in place, as TD-0080's fixed items are ("Fixed: …", "Done."), numbers unchanged; drop "binary searches" from the row.
- [ ] TD-0063: `SortedSearch` now has a lower bound and every other search goes through it; the map's stays apart only for its `ref struct` key.
- [ ] `generator.md`: every search of a sorted list goes through `SortedSearch`, save the type override map's; the two maps share `SortedKeyedList.OrderOf` and `TypeKeyText.ComparePart`.
- [ ] Measure with the scratch harness (Release, three runs) and keep the figures for the PR description.
- [ ] `./pre-commit-validation.sh --full`, then commit.
