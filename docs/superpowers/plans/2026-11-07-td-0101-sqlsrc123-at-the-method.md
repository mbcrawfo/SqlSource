# TD-0101 SQLSRC123 at the method Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `codegen` query method is refused for its models' accessibility only when it takes or returns a model that is internal; the method planner reports that `SQLSRC123` and leaves out the method, not the file.

**Architecture:** `PlacementChecks.VisibilityReason` keeps only what C# forbids whatever a method takes (an extension method that is private or protected, protected on a static class or a struct).  The two models rules move to a new `PlacementChecks.MethodModelsReason`, which `MethodPlanner.Plan` asks once it has the query's models, and refuses the method only when one of its models' `ModelAssignment` is internal.  `TypePlacement`'s check of a query method in a nested class whose visibility another query wrote (step 3) is all about models, so it goes too; the planner finds that writer itself when it reports.

**Tech Stack:** C# (`netstandard2.0` generator), Roslyn incremental generator, xUnit + Shouldly.

**Spec:** `docs/tech-debt/TD-0101-a-method-with-no-models-is-refused-as-one-with-models.md`

## Global Constraints

- The generator's rules of `src/SqlSource/AGENTS.md` hold: no Roslyn object between steps, value equality for cached records, nothing a user writes makes the generator throw, `TypeOutput`'s inputs are never widened for the methods.
- `src/SqlSource/AGENTS.md` is at 7,833 of 8,192 bytes: add nothing there; detail goes to `docs/architecture/generator.md`.
- `SQLSRC123`'s message text and arguments do not change; its place does not change: the query's `-- visibility:` marker, else `-- member-location:`, else its name when the query writes its own visibility, and `-- member-location:`, else its name, naming "`<place>` for the query '`<writer>`'" when a nested class's visibility comes from another query.
- Only a written visibility is refused; `PlacementChecks.DefaultVisibility` never gives what either reason refuses.
- Tests in `tests/SqlSource.Tests/Generator/` start with the RoslynFloor line, use Roslyn 4.14.0 API only, pass an `OutputKind` where the harness asks, and are named `Method_Scenario_Outcome`.
- Resolving TD-0101 follows `docs/tech-debt/AGENTS.md` "Resolving an item": delete the file, its README row, every `TD-0101` reference, and the settings known issue.

## Where the code differs from the spec

- The spec says "a model that is internal".  A model is public when any type that shares it is public (`docs/manual/generator.md#model-accessibility`), so a non-public type's method can take a public model.  The planner reads that from the model's assignment: `ModelAssignment` gains `IsPublic`, set from the model `ModelCollector` emits.
- The spec names the two rules of `VisibilityReason`; `TypePlacement.CheckMethodsOfClassVisibilities` (architecture step 3) applies the second rule to a nested class's visibility written by another query, so it moves to the planner too.

## Review Focus

1. A type that is not public sharing a file, and so its models, with a public type: its public extension method takes public models and compiles, with no `SQLSRC123` (Task 2, `Run_PublicMethodOfAnInternalTypeWithSharedPublicModels_Compiles`).
2. A refused method's file keeps everything else: its other queries' constants and methods, and the refused query's models; the compilation has no error but `SQLSRC123` (Task 2, the reworked driver tests).
3. A query with models but no sidecar entry yet gets its missing-entry error and no `SQLSRC123` until it is described (Task 2, `Run_MethodOfATypeReachableAsProtectedWithoutAnEntry_ReportsNoSqlsrc123`).
4. Two types that claim one file, both not public, each get their own `SQLSRC123` at the one place (Task 2, `Run_TwoInternalTypesClaimingAFileWithAPublicMethod_NamesEachType`).
5. A nested class made public by another file's query: a method in it with models is refused, naming that query, both files kept; one with no models compiles (Task 2).

---

### Task 1: A model's assignment says whether the model is public

**Files:**
- Modify: `src/SqlSource/Generation/Models/ModelAssignment.cs`, `src/SqlSource/Generation/Models/ModelCollector.cs:~197`
- Test: `tests/SqlSource.Tests/Generation/Models/ModelCollectorTests.cs`, `tests/SqlSource.Tests/Generation/Models/ModelAssignmentTests.cs`

**Interfaces:**
- Produces: `ModelAssignment(string ClaimingType, string NormalizedPath, int QueryIndex, ModelRole Role, string TypeText, ModelKind Kind, bool IsPublic)`, `IsPublic` documented "Whether the generated model is public, as `Model.IsPublic` is; otherwise it is internal."

- [ ] **Step 1: Update the existing constructions and add the failing test** `Resolve_ModelSharedByAPublicAndAnInternalType_AssignsItPublicToBoth` in `ModelCollectorTests`: two requests of one model, one from a public type (`IsPublic: true`) and one not; both assignments have `IsPublic` true.  Add `Resolve_ModelOfAnInternalType_AssignsItInternal`: one request with `IsPublic: false` gives `IsPublic` false.  Existing `new ModelAssignment(...)` calls in the tests pass the value their requests give.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~ModelCollectorTests|FullyQualifiedName~ModelAssignmentTests"`; expect a build failure (no seventh parameter).
- [ ] **Step 3: Add the parameter** and pass the emitted model's `isPublic` in `ModelCollector.Models`.
- [ ] **Step 4: Run** the same filter; expect PASS.  Then `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Caching"` passes too.
- [ ] **Step 5: Commit** `feat(generator): a model's assignment says whether the model is public`.

### Task 2: SQLSRC123 for a method's models is the planner's

**Files:**
- Modify: `src/SqlSource/Generation/Placement/PlacementChecks.cs`, `src/SqlSource/Generation/Placement/TypePlacement.cs`, `src/SqlSource/Generation/Methods/MethodPlanner.cs`
- Test: `tests/SqlSource.Tests/Generation/Placement/TypePlacementTests.cs`, `tests/SqlSource.Tests/Generator/PlacementDiagnosticsTests.cs`, `tests/SqlSource.Tests/Generator/PlacementTests.cs`

**Interfaces:**
- Consumes: `ModelAssignment.IsPublic` (Task 1); `ModelAssignment.IndexOf` / the planner's `FindAssignment`.
- Produces:
  - `PlacementChecks.VisibilityReason(TargetType type, MemberLocation location, Visibility visibility)`: the `output` parameter is gone; it returns "an extension method is public or internal" and the static and struct reasons, and nothing about models.
  - `PlacementChecks.MethodModelsReason(TargetType type, MemberLocation location, Visibility visibility) : string?`: why a query method there could not take or return the type's internal models, with today's two texts exactly: `ExtensionClass` + `Public` + `!type.IsPublic` → "'<type>' is not public, so its models are internal, which a public extension method cannot take or return"; `Class` or `NestedClass` + `Public` or `Protected` + `type.IsReachableAsProtected` → "'<type>' is reachable as protected, so its models are internal, which a method reachable from another assembly cannot take or return"; else null.  Its summary says the planner asks it only for a method that takes or returns an internal model.
  - `TypePlacement`: `CheckMethodsOfClassVisibilities` removed; `Of`'s summary lists the checks without it ("Each of the last three leaves out every file it names").

- [ ] **Step 1: Rewrite the placement unit tests.**  In `TypePlacementTests`: `Of_PublicExtensionMethodOfAnInternalType_ReportsSqlsrc123AtTheVisibilityMarker` becomes `Of_PublicExtensionMethodOfAnInternalType_IsUsable` (no diagnostics, `Usable` `[true]`); the two `Of_NestedClassMadePublicBy…` tests become `…_IsUsable` (no diagnostics, every file usable, the class keeps the written visibility); `VisibilityReason_OfATypeReachableAsProtected_RefusesOnlyAPublicOrProtectedMethod` becomes `MethodModelsReason_OfEachLocationAndVisibility_RefusesOnlyWhereModelsWouldNotCompile`, with today's rows minus the output column plus `ExtensionClass`/`Public` on a non-public type (refused, extension text), `ExtensionClass`/`Public` on a public type and `ExtensionClass`/`Internal` (not refused).  `DefaultVisibility_OfEachLocation_NeverFails` also asserts `MethodModelsReason` is null for each default.
- [ ] **Step 2: Rewrite the driver tests** in `PlacementDiagnosticsTests` (the class comment says that a models refusal leaves out the method alone):
  - `Run_MethodOfATypeReachableAsProtected_ReportsSqlsrc123`: same rows and message; the file also holds `-- name: GetName\n-- output: sql\nSELECT name FROM users;\n`, and the run asserts the type's file has `GetName`'s constant and the `GetUser` models are generated (assert by the model's name in `run.Sources`), the methods file absent, and no compile error.
  - `Run_MethodWithNoModelsOfATypeReachableAsProtected_ReportsSqlsrc123` becomes `Run_MethodWithNoModelsOfATypeReachableAsProtected_Compiles`: no problems, the methods file holds `public` `GetUser`, and the run compiles without errors; add rows for `nested-class`/`public` and `class`/`protected`.
  - New `Run_PublicExtensionMethodWithNoModelsOfAnInternalType_Compiles`: `-- visibility: public`, `DELETE FROM users;`, entry with no parameters and no rows, declaration `internal partial class Sample;`; no problems, the methods file declares `public static partial class SampleExtensions` with the method.
  - The `Run_VisibilityThatDoesNotFit_ReportsSqlsrc123` row for a public extension method of a type that is not public keeps its message; the same-file constant assertion as above is added for that row's file (or a new test `Run_PublicExtensionMethodWithModelsOfAnInternalType_ReportsSqlsrc123AndKeepsTheFile`).
  - `Run_NestedClassMadePublicByAnotherFileOfATypeReachableAsProtected_ReportsSqlsrc123AtTheMethod`: same message and place; now also the method's file is kept (its models generated) and the constant file as today.  Add `…WithNoModels_Compiles`.
  - New `Run_MethodOfATypeReachableAsProtectedWithoutAnEntry_ReportsNoSqlsrc123`: the query with parameters, `-- visibility: public`, no entry: the problems are exactly the missing-entry error the harness gives today for a `codegen` query without an entry, and no `SQLSRC123`.
  - New `Run_TwoInternalTypesClaimingAFileWithAPublicMethod_NamesEachType`, after `Run_TwoTypesClaimingAFileWithSqlsrc123_NamesEachType`: two `internal` types, `-- visibility: public`, `UserEntry`: two distinct `SQLSRC123`, one naming each type, with the extension text.
  - New `Run_PublicMethodOfAnInternalTypeWithSharedPublicModels_Compiles` (in `PlacementTests`, the allowed combinations): a `public` and an `internal` type claim one file whose query writes `-- visibility: public`; no problems, both methods files written, no compile error.
- [ ] **Step 3: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Placement"`; expect the new and reworked tests to fail.
- [ ] **Step 4: Implement.**
  - `PlacementChecks`: split as the Interfaces say; drop `using SqlSource.Generation.Models` only if unused.  `TypePlacement.CheckQuery` calls the three-argument `VisibilityReason`; remove `CheckMethodsOfClassVisibilities` and its call; update the summary and the `TD-0101` comments go.
  - `MethodPlanner.Plan`: once `models` is found, when `PlacementChecks.MethodModelsReason(type, placement.Location, placement.Visibility)` is not null and the query's input model (when `models.Input` is not null) or output model (when `models.Output` is not null) has an assignment with `IsPublic` false, add `SQLSRC123` (`SqlDiagnostics.VisibilityNotFit`) to `problems` with the arguments `query.Name`, `PlacementChecks.NameOf(type)`, `PlacementChecks.KeywordOf(placement.Visibility)`, the place, `PlacementChecks.TextOf(placement.Location)`, the reason.  The place and location: when `settings.WrittenVisibility` is set, `PlaceText.Of(written.Place)` at `query.VisibilityMarker ?? query.MemberLocationMarker ?? query.NameLocation`; otherwise (a nested class whose visibility another query wrote) the first query of the usable queries, in their order, in the same nested class with a written visibility: `PlaceText.Of(its place) + " for the query '" + its name + "'"`, at `query.MemberLocationMarker ?? query.NameLocation`.  Search for that writer only when reporting, so a method that is not refused costs one switch.  A missing assignment is not a refusal: that query already has its own error.  Add `SQLSRC123` to the class summary's list.
- [ ] **Step 5: Run** `dotnet test tests/SqlSource.Tests` and `dotnet test tests/SqlSource.Tests.RoslynFloor`; expect PASS.
- [ ] **Step 6: Commit** `fix(generator): SQLSRC123 refuses a method only for an internal model, and leaves the file in (TD-0101)`.

### Task 3: Documents, and TD-0101 resolved

**Files:**
- Modify: `docs/manual/diagnostics.md` (SQLSRC123 section only), `docs/manual/settings.md` (visibility section, known issue), `docs/manual/generator.md` (lines ~112, ~116, ~573), `docs/architecture/generator.md` (placement section, the query methods' problems and the `MethodDiagnostics` node), `src/SqlSource/Generation/Placement/AGENTS.md`, `docs/tech-debt/README.md` (TD-0101 row only)
- Delete: `docs/tech-debt/TD-0101-a-method-with-no-models-is-refused-as-one-with-models.md`

- [ ] **Step 1: Manual.**  SQLSRC123: the two models bullets say the method is refused when it takes or returns a model that is internal (a query with parameters or rows; a model shared with a public type is public), and drop "is refused too: write `-- visibility: internal`"; the paragraph on what is left out says a refusal for models leaves out that method alone, its file keeping its other members and its models, and comes once the query has its entry, while the other reasons leave out the file; the nested-class paragraph says the same.  `settings.md`: the "What fits" bullet adds a query method with no parameters and no rows, the nested-class bullet's "leaves its own file…" becomes the method alone, and the known issue "A query method that takes no models can be refused…" is deleted.  `generator.md`: line ~112 says the method is refused when the models it takes or returns are internal; ~116 and ~573 say a visibility refused for the models leaves out the method alone.  No id, no internals.
- [ ] **Step 2: Architecture.**  Placement: step 1's text drops "for its output"; step 3 is removed and the list renumbered; the `IsReachableAsProtected` paragraph says the planner refuses such a method, through `MethodModelsReason`, only when an assignment of its models is internal, with no `TD-0101`.  Query methods, "Its problems": add `SQLSRC123` with its place rules and the writer search; the mermaid node reads "SQLSRC123, SQLSRC272, SQLSRC274 and SQLSRC275".  Placement `AGENTS.md`: "Only a written visibility is checked" names both reasons, and a rule that a refusal for models is the planner's, through `PlacementChecks.MethodModelsReason`, keeping the file at 8 KB or less.
- [ ] **Step 3: Resolve TD-0101.**  Delete the item file and its README row; `git grep -n "TD-0101"` shows only this plan.
- [ ] **Step 4: Run** `dotnet test tests/SqlSource.Repository.Tests`; expect PASS (links, diagnostics sections, AGENTS limits).
- [ ] **Step 5: Commit** `docs: SQLSRC123 for a method's models leaves out the method; resolve TD-0101`.
