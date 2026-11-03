# SQLSRC275 Message Wording Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** SQLSRC275's message says a reserved token name is of an argument a query method *can* have, not one *every* query method has, matching the manual's wording.

**Architecture:** One descriptor's `messageFormat` changes.  The tests that assert the message, the manual's sample output and the two golden files of the query-method corpus that hold it change with it.

**Tech Stack:** C#, Roslyn source generator, the golden corpus of `tests/README.md`.

**Spec:** none.  The owner's decision on finding R1-F2 of the query-method refactor's PR 3 (#209, `docs/superpowers/plans/2026-11-02-query-method-argument-model.md`): that PR reworded the manual to "the arguments a query method can have", and this PR fixes the message.  A query without parameters has no `parameters` argument, yet the name stays reserved.

## Global Constraints

- The id `SQLSRC275`, its title, severity, `NotConfigurable` tag, help link and the five reserved names stay as they are.
- New message, exactly: `The token '{0}' of the query '{1}' has the name of an argument that a query method can have: connection, parameters, transaction, commandTimeout or cancellationToken.  Rename the token.`
- Lines stay within 120 columns.
- `AnalyzerReleases.Unshipped.md` records id, category, severity and title only, so it needs no change.

## Review Focus

- A golden file diff that shows anything but the SQLSRC275 line: the change is message-only, so any other line is a regression.
- RoslynFloor's run of the corpus reads the same golden files: it must pass without being regenerated.
- The manual's SQLSRC275 prose already says "can have"; only its console sample holds the old message.

---

### Task 1: Reword SQLSRC275

**Files:**
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs` (`TokenNamedAsAnArgument`, `messageFormat`)
- Modify: `tests/SqlSource.Tests/Generator/MethodDiagnosticsTests.cs` (the two expected SQLSRC275 strings)
- Modify: `docs/manual/diagnostics.md` (the `## SQLSRC275` console sample)
- Regenerate: `tests/SqlSource.Tests/Generator/Golden/postgres/diagnostics.txt`, `tests/SqlSource.Tests/Generator/Golden/sqlserver/diagnostics.txt`

- [ ] **Step 1: Update the two expected strings in `MethodDiagnosticsTests`** to the new message, `'transaction'` / `'SetName'` and `'transaction'` / `'List'` as now.
- [ ] **Step 2: Run them and see them fail**: `dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generator.MethodDiagnosticsTests"`; expected: the two SQLSRC275 tests fail on the message.
- [ ] **Step 3: Change the descriptor's `messageFormat`** to the new message, wrapped within 120 columns.
- [ ] **Step 4: Run step 2's command again**; expected: pass.
- [ ] **Step 5: Update the manual's console sample** to the new message with `'transaction'` / `'ListFrom'`.
- [ ] **Step 6: Regenerate the corpus**: `SQLSOURCE_UPDATE_GOLDEN=1 dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generator.QueryMethodCorpusTests"`, then the same without the variable; expected: pass.  `git diff tests/SqlSource.Tests/Generator/Golden` shows the line 4 SQLSRC275 message of the two `diagnostics.txt` files and nothing else.
- [ ] **Step 7: Run the diagnostics tests**: `dotnet test --project tests/SqlSource.Tests -- --filter-namespace "SqlSource.Tests.Diagnostics"`; expected: pass.
- [ ] **Step 8: Format, validate and commit**: `./pre-commit-validation.sh`, then commit `fix(diagnostics): SQLSRC275 names an argument a query method can have`.
