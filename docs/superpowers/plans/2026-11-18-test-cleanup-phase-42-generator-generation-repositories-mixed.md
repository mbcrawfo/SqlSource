# Test cleanup phase 42: generator generation repositories mixed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Fold `ConnectionCandidateReaderTests.Read_MarkedMethodWithTwoParameters_IsUnsupported` into `Read_MarkedMemberOfAnotherShape_IsUnsupported`, drop the six rows of `ConnectionResolverTests.Resolve_MarkedCandidate_HasTheKindOfWhatItGives` that vary only the member, and strengthen `Read_MarkedStringMethod_IsAConnectionString`, `Resolve_NoCandidate_ReportsSqlsrc028AndFailsTheDatabase` and `Resolve_FieldNamedAsAnArgument_IsTheSource` so that each proves what its name says.

**Architecture:** Test-only change in `tests/SqlSource.Tests/Generation/Repositories`; there is no source change.  The guard is the `generator-generation-repositories` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Repositories/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-42-generator-generation-repositories-mixed-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- F-3-02: delete the fact and add its member, `private NpgsqlConnection Open(string name, CancellationToken cancellationToken) => null!;`, as a fifth row of `Read_MarkedMemberOfAnotherShape_IsUnsupported`.
- F-3-08: drop the rows `(ConnectionString, Property)`, `(ConnectionString, Parameter)`, `(ConnectionString, Method)`, `(DataSource, Parameter)`, `(Connection, Property)` and `(Connection, Parameter)`; keep `(ConnectionString, Field)`, `(DataSource, Field)`, `(Factory, Method)` and `(Connection, Field)`, one for each type.
- F-3-01: add `candidate.IsAsync.ShouldBeFalse();`.
- F-3-06: assert the whole advice, as `Resolve_HeldParameters_ReportsSqlsrc028NamingThem` does.
- F-3-07: resolve the field for `Main with { TakesParameters = true }`, so that `parameters` is an argument's name.
- The epic's phase table: change only row 42.

## Review Focus

Checked against the spec on `f3d58b43`.  The source moved since the review read it, but each finding still reads as it says: `ReturnOf` gives a string return `IsAsync` false at `ConnectionCandidateReader.cs:451` and `Method` passes `isAsync && isCallable` on (436); the held-parameters advice is chosen by `heldParameters.Count > 0` at `ConnectionResolver.cs:259`; the check that an argument hides a member, `member == CandidateMember.Parameter && IsAnArgumentsName(name, database)`, is now in the partial `ConnectionResolver.Resolving.cs:130`, with `IsAnArgumentsName` at 274-276, and `Take`'s `source.IsAsync ? null : source` at 98.  Neither theory the phase touches branches on its row, so TD-0089 is unchanged.  No document names a test of the phase.  No earlier phase ran this scope, so the "before" report is this phase's own.  F-3-06's survivor, `ConnectionResolver.cs:259:13` `heldParameters.Count >= 0`, is already `Killed` in the "before" report, by `ConnectionResolverTierTests.Resolve_NoTierHasACandidate_ReportsSqlsrc028WithTheNewAdvice` (#331); the finding's read of the test still holds, since the test alone would pass on that mutant, so it is applied as written.

---

### Task 0: The guard's "before" half

- [x] Mutation run of `generator-generation-repositories` into `artifacts/test-cleanup/phase-42/generator-generation-repositories/before`.
- [x] `kills` on the before report for the six tests the spec names: `ConnectionCandidateReader.cs:451:53` survives under `Read_MarkedStringMethod_IsAConnectionString`; `ConnectionResolver.cs:259:13` (`>= 0`) is killed by another class's test.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-42/coverage-before/SqlSource.Tests`.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Repositories/ConnectionCandidateReaderTests.cs`
- Modify: `tests/SqlSource.Tests/Generation/Repositories/ConnectionResolverTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 42's state)

- [x] **Step 1:** F-3-02 fold, F-3-01 strengthen in `ConnectionCandidateReaderTests.cs`.
- [x] **Step 2:** F-3-08 fold, F-3-06 and F-3-07 strengthen in `ConnectionResolverTests.cs`.
- [x] **Step 3:** Set row 42 of the epic's table to `done`.
- [x] **Step 4:** `./format.sh`, then the Repositories tests pass.
- [x] **Step 5:** F-3-07 hand check: with `IsAnArgumentsName`'s member check turned to `||` the strengthened test fails, and passes again on the real source.
- [x] **Step 6:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare`: nothing `LOST` and nothing `NOT COMPARABLE`, and both named survivors `Killed`.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-42/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/Repositories/**/*.cs` falls.
- [x] **Step 3:** The pull request gives the changes, the compare result, the coverage figures and the run times.
