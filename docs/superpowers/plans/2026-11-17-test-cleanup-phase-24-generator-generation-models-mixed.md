# Test cleanup phase 24: generator generation models mixed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the `PropertyNamesTests` test whose behaviour cannot be observed and drop two rows of its schema theory that other rows already cover, with the guard showing nothing lost.

**Architecture:** One test file changes, `tests/SqlSource.Tests/Generation/Models/PropertyNamesTests.cs`; no source change.  The guard is the `generator-generation-models` mutation scope and the coverage of the files under its `mutate` glob, `src/SqlSource/Generation/Models/**/*.cs`.

**Tech Stack:** C#, xUnit v3, Shouldly, Stryker.NET through `tools/mutation-test.sh`.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-24-generator-generation-models-mixed-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change no test but `PropertyNamesTests.FromTable_PublicSchema_NeedsNothingOfTheSchema`, deleted with its comment, and `PropertyNamesTests.FromTable_Table_IsTheSchemaUnlessItIsPublicThenTheTable`, which loses its rows `("public", "users", "Users")` and `("billing", "invoices", "BillingInvoices")`.
- The covers keep every row: `FromTable_TableOfAnEngine_DropsTheSchemaOfThatEngineAlone` rows `("public", "orders", "PostgreSql", "Orders")` and `("billing", "invoices", "PostgreSql", "BillingInvoices")`, and the schema theory's rows `("public", "order_lines", "OrderLines")` and `("Billing_Admin", "audit_log", "BillingAdminAuditLog")`.
- The epic's phase table: change only row 24.

## Review Focus

None: two rows and one fact go, each with a cover the spec names.  Checked against the spec on `7b272da0`: `PropertyNames.FromTable` still returns the table's name when the schema is the engine's default, compared ordinally, before it reads the schema, so `public` is never PascalCased; the covers' rows are as the spec quotes them; no document names either test or their class.  Nothing changed `src/SqlSource/Generation/Models` or `tests/SqlSource.Tests/Generation/Models` since p23's "after" run (`a716ef0c`), so that report is this phase's "before".

---

### Task 0: The guard's "before" half

- [x] p23's "after" report copied to `artifacts/test-cleanup/phase-24/generator-generation-models/before/reports/mutation-report.json`.
- [x] `kills` on the before report for both changed tests.
- [x] Coverage of `tests/SqlSource.Tests` into `artifacts/test-cleanup/phase-24/coverage-before/SqlSource.Tests`, with the line figures of each file of `src/SqlSource/Generation/Models/**/*.cs`.

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Tests/Generation/Models/PropertyNamesTests.cs`
- Modify: `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` (row 24's state)

- [x] **Step 1:** Delete `FromTable_PublicSchema_NeedsNothingOfTheSchema` and the comment above it (F-SqlSource.Tests.Generation-2-07).
- [x] **Step 2:** Drop the rows `("public", "users", "Users")` and `("billing", "invoices", "BillingInvoices")` of `FromTable_Table_IsTheSchemaUnlessItIsPublicThenTheTable` (F-SqlSource.Tests.Generation-2-08).
- [x] **Step 3:** Set row 24 of the epic's table to `done`.
- [x] **Step 4:** `./format.sh`, then the `PropertyNamesTests` tests of `tests/SqlSource.Tests`: every test passes.
- [x] **Step 5:** `./pre-commit-validation.sh`, then commit.

### Task 2: The guard's "after" half

- [x] **Step 1:** The mutation run with output `.../after`, then `tools/mutation-test.sh compare` of the two reports: nothing `LOST`, nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` shows the changed tests did not make.
- [x] **Step 2:** Coverage into `artifacts/test-cleanup/phase-24/coverage-after/SqlSource.Tests`: no file of `src/SqlSource/Generation/Models/**/*.cs` falls.
- [x] **Step 3:** The pull request lists the removed test and rows beside their covers, the compare result, the coverage figures and the run times.
