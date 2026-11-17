# Test cleanup phase 21: generator generation emitting folds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fold the three tests of `TypeEmitterTests` that expect the type's empty part for methods queries into one theory over the placement, with the guard showing nothing lost.

**Architecture:** Three facts become one theory, no source change.  The guard is the `generator-generation-emitting` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Emitting/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-21-generator-generation-emitting-folds-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but `TypeEmitterTests.Emit_MethodsQuery_WritesNoConstant`, `Emit_MethodsTokenQuery_WritesNoTokenMethod` and `Emit_NestedTypeOfOnlyMethodsQueries_WritesNoNestedClass`.
- The theory takes `bool direct`, rows `true` and `false`, emits under `OutputKind.Methods` with `Placement(direct, 1)` one file holding a constant query and a token query, and expects no diagnostics and the type's empty part, as the three did.
- The comment that states the rule stays, above the theory.
- The epic's phase table: change only row 21.

## Review Focus

None: the change merges facts and drops no row.  Checked against the spec on `721cfb41`: `TypeEmitter.SelectMembers` still skips a query whose output has a method before `AppendMember` asks about tokens, `EmitOnType` is still `Placement(direct: true, 1)`, and the nested test's `Emit` still gives each file `SettingsLevel.None`, which is `Placement(false, 1)`.  #331 changed `Generation/Emitting` since p04's "after" report, so the phase runs its own "before".

---

### Task 0: The guard's "before" half

- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-21/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Generation/Emitting/**/*.cs`.
- [x] The `generator-generation-emitting` mutation run into `artifacts/test-cleanup/phase-21/generator-generation-emitting/before`.
- [x] `kills` on the before report for each of the three tests.

### Task 1: Fold the tests

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Emitting/TypeEmitterTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 21's state)

- [x] **Step 1:** Replace the three tests with `Emit_OnlyMethodsQueries_WritesTheTypesEmptyPart(bool direct)`, rows `true` and `false`, as the constraints say.
- [x] **Step 2:** Set row 21 of the epic's table to `done`.
- [x] **Step 3:** `./format.sh`, then `dotnet test --project tests/SqlSource.Tests`: every test passes.
- [x] **Step 4:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows the folded tests did not make.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-21/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/Emitting/**/*.cs` falls.
- [x] **Step 3:** The pull request lists the folded tests beside their cover, the compare result, the coverage figures and the run times.
