# Query-method identity by type and name Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A generated method is known by its type and its name, not by the place of its query's name, so that a clash that drops one method can never drop another method of the same query.

**Architecture:** `DroppedMethod` becomes `(TypeKey, Name)`; `MethodConflicts.DroppedOf` gives the names to drop and `MethodEmitter.Keep` filters by name.  `MethodPlanner`'s private `Planned` holds an `ImmutableArray<QueryMethod>`, one method today, and `AddPlanned` walks it.  `NameLocation` stays on `QueryMethod` and `MethodSignature` only to place `SQLSRC274`.

**Tech Stack:** C# (`netstandard2.0` generator), xUnit v3, Shouldly.

**Spec:** `docs/superpowers/specs/2026-10-30-query-method-emission-refactor-design.md`, section "PR 5: a method's identity is its type and name".

## Global Constraints

- Every generated file and diagnostic stays byte-identical: `QueryMethodCorpusTests` passes with no golden file changed (never run it with `SQLSOURCE_UPDATE_GOLDEN=1`).
- `CachingMethodTests` stay true unchanged.
- No switch expression over an enum gets a `_ =>` arm (`src/SqlSource/AGENTS.md`).
- No new allocation in the planner or the emitter beyond the one-element array that holds a planned query's method; the success path of `MethodPlanner.Plan` drops today's `with` copy and empty list so the net count does not rise.
- No `docs/tech-debt` or `docs/deferred` item (spec, "Decisions").
- Out of scope: the context struct, phase files and `MethodHost` (PR 6).

## What the code is today (checked at `6d67d35e`)

Matches the spec, with one detail it does not mention: `Planned` also carries `Name`, which `AddPlanned` reads only for a plan with a method, so it moves onto each method (`QueryMethod.Name`).

## Review Focus

1. Two methods of one type, one query, distinct names, sharing a `NameLocation`, one clashing with another type: only the clashing one is dropped.  Pinned by tests in Task 1 (conflicts and emitter).
2. Two types that claim one file share each query's place: a clash between two of them leaves the third its method (`DroppedOf_QueryOfThreeTypesWithAClashBetweenTwo_LeavesTheThirdItsMethod`, kept).
3. The order of `Dropped` stays independent of the signatures' order (`Find_SignaturesInAnotherOrder_GiveAnEqualValue`, kept).
4. A clash inside one type (two queries, one name) still reports `SQLSRC274` from `AddPlanned` in the same order: the corpus's `diagnostics` cases.
5. An edit that moves a clashing query's name: `MethodOutput`'s input is now its names, equal across the move, so the type's file is not written again; no existing caching test asserts otherwise.

---

### Task 1: identity by type and name

**Files:**
- Modify: `src/SqlSource/Generation/Methods/MethodConflicts.cs`
- Modify: `src/SqlSource/Generation/Emitting/MethodEmitter.cs`
- Modify: `src/SqlSource/Generation/Methods/MethodPlanner.cs` (`Plan`, `AddPlanned`, `Planned`)
- Modify: `tests/SqlSource.Tests/Generation/Methods/MethodConflictsTests.cs`
- Create: `tests/SqlSource.Tests/Generation/Emitting/MethodEmitterTests.cs`

**Interfaces:**
- Produces: `internal sealed record DroppedMethod(string TypeKey, string Name)`; `public EquatableArray<string> MethodConflicts.DroppedOf(string typeKey)` (names, ordinal order); `MethodEmitter.Emit(TypeMethods methods, EquatableArray<string> dropped, bool extensionClassBlocked)`; private `Planned(List<DiagnosticInfo> Problems, string QueryName, LocationInfo NameLocation, ImmutableArray<QueryMethod> Methods, bool IsDriverUsable, List<string> WarningIds)`.
- `SqlSourceGenerator`'s `MethodOutput` step compiles unchanged (it passes `DroppedOf`'s value through).

- [ ] **Step 1: Write the failing tests**

In `MethodConflictsTests`: every `new DroppedMethod(key, x.NameLocation)` becomes `new DroppedMethod(key, "Get")`; `DroppedOf(...)` expectations become `["Get"]`.  Add:

```csharp
// Two methods of one type share their query's place, as a query that gives two methods would: the one that clashes
// is dropped by its name, and the other is kept.
[Fact]
public void DroppedOf_TwoMethodsOfOnePlaceOneClashing_DropsOnlyTheClashingName()
{
    var get = Signature("App.First", "FirstExtensions", "Get");
    var list = Signature("App.First", "FirstExtensions", "List") with { NameLocation = get.NameLocation };
    var other = Signature("App.Second", "FirstExtensions", "Get", path: "b.sql");

    var conflicts = Find([get, list, other]);

    conflicts.DroppedOf("App.First").ShouldBe(["Get"]);
    conflicts.Dropped.ShouldBe([new DroppedMethod("App.First", "Get"), new DroppedMethod("App.Second", "Get")]);
}
```

New `MethodEmitterTests` (namespace `SqlSource.Tests.Generation.Emitting`), building a `TypeMethods` as `MethodSignatureTests.KeyOf` does, from `QueryMethodWriterTests.GetUser` and `GetUser with { Name = "ListUsers" }` (same `NameLocation`):

- `Emit_DroppedNameOfOneOfTwoMethodsOfOnePlace_KeepsTheOther`: `Emit(methods, ["GetUser"], false)` gives a source that contains `" ListUsers(\n"` and does not contain `" GetUser(\n"`.
- `Emit_NothingDropped_KeepsBoth`: `Emit(methods, EquatableArray<string>.Empty, false)` contains both.
- `Emit_EveryMethodDropped_GivesNoSource`: `Emit(methods, ["GetUser", "ListUsers"], false).Source` is null.

- [ ] **Step 2: Run them to see them fail to compile**

`dotnet build tests/SqlSource.Tests` — fails: `DroppedMethod` takes a `LocationInfo`; `Emit` takes `EquatableArray<LocationInfo>`.

- [ ] **Step 3: Implement**

- `DroppedMethod(string TypeKey, string Name)`; its doc says the name is unique in the type once `MethodPlanner.AddPlanned` has run.
- `FindClashes` adds `new DroppedMethod(method.TypeKey, method.Name)`; `Find` orders `Dropped` by `TypeKey`, then `Name`, both ordinal.  `SQLSRC274` still uses `method.NameLocation`.
- `DroppedOf` selects `method.Name`; summary: the names of the type's methods to leave out.
- `MethodEmitter.Emit`/`Keep` take `EquatableArray<string> dropped` and keep a method whose `Name` it does not contain; the `<param>` says names.
- `MethodPlanner.Plan` returns `new Planned(problems, query.Name, query.NameLocation, [], false, [])` on the paths that give no method, and on success one `new Planned(..., [method], isUsable, WarningIdsOf(...))`, with no `with` copy.  The local `name` stays for the method and `SQLSRC274`.
- `AddPlanned` counts `byName` over every method of every plan (`method.Name` → the plan's `QueryName`), then for each plan adds its problems and, for each of its methods in order, either `SQLSRC274` at `plan.NameLocation` with `method.Name`, or keeps the method when `plan.IsDriverUsable` (warning ids and conversion libraries as today).  The comment on `Planned` says it holds the query's methods, one today.

- [ ] **Step 4: Run the tests**

```bash
dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generation.Methods.MethodConflictsTests" --filter-class "SqlSource.Tests.Generation.Emitting.MethodEmitterTests" --filter-class "SqlSource.Tests.Generator.CachingMethodTests" --filter-class "SqlSource.Tests.Generator.QueryMethodCorpusTests"
dotnet test --project tests/SqlSource.Tests.RoslynFloor -- --filter-class "SqlSource.Tests.Generator.QueryMethodCorpusTests"
```

Expected: all pass, `git status` shows no golden file changed.

- [ ] **Step 5: Commit** — `refactor(generator): a query method is known by its type and name`

### Task 2: documentation

**Files:** `docs/architecture/generator.md`, `### Query methods`.

- After the list of the plan's steps, the `AddPlanned` paragraph: each query's plan holds its methods, a list of one today, and `AddPlanned` names them unique within the type.
- **Across types.**: a method is known by its type's key and its name; `DroppedMethod` holds both; the place of the query's name, which every method of one query would share, only places `SQLSRC274`.  `DroppedOf` gives the names of the type's dropped methods, so a clashing query whose name moves writes nothing again.
- **The file.**: `MethodEmitter.Emit` keeps the methods whose names are not dropped.

No AGENTS.md names `DroppedMethod`, `DroppedOf`, `Keep` or `Planned` (checked), so none changes.  Commit: `docs(architecture): query methods are known by their type and name`.
