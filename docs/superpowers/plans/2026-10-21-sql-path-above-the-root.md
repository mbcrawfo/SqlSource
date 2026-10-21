# A `.sql` Path Above the Root Never Selects Everything Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `.sql` path of `sqlsource describe` that `SqlPath.Normalize` cannot place is reported as `SQLSRC212` and selects no query, so a run with no other filter no longer describes every query.

**Architecture:** `RunFilters.Create` keeps such a path as a `FileFilter` whose `NormalizedPath` is null, instead of dropping it.  `RunPlanner.Select` then sees a file filter, selects no file for it, and reports it with the other paths that name no planned file.  `RunFilters.NamesFiles` becomes redundant (`!Files.IsEmpty` says the same) and is removed.  `SqlPath` is not touched.

**Tech Stack:** C# (.NET 8 tool), xUnit v3, Shouldly.

**Spec:** [`docs/tech-debt/TD-0068-sql-path-that-climbs-above-the-root-is-ignored.md`](../../tech-debt/TD-0068-sql-path-that-climbs-above-the-root-is-ignored.md) (deleted by Task 2; read it from `main` if gone).

## Decisions

- **Error, not a silent no-op.**  The path is reported as the existing `SQLSRC212` ("is not a .sql file that a type of the run claims"): it is a `.sql` path that names no file of the run, which is exactly what `SQLSRC212` documents.  No new diagnostic id.  The run goes on, as for any `SQLSRC212`, and exits `1`.
- **How it is named.**  Through `UsageValue.NamedAsPath(fullPath)`, as every `SQLSRC212` is: the full path in quotes, or `The path at position <n>` when the token holds `=` or `;`.  The brief asked for "naming the path by its position"; the merged rule of cli-secrets (#87, TD-0023) prints a path with neither character and names one with either by position, and this follows the merged code.  A path such as `a\..\..\q.sql` is printed as `'<working directory>/a\..\..\q.sql'`.
- **Deduplication.**  A placed path is keyed by its normalised path, as today.  An unplaced one is keyed by its full path, compared with `SqlPath.Comparer`: it holds a backslash, which a normalised path never does, so the two keys never collide, and the same unplaced path given twice is reported once.
- **`SqlPath` unchanged**, so the claimed-files unit (TD-0019) running beside this one has nothing to merge here.

## Global Constraints

- Every commit passes `./pre-commit-validation.sh` (root AGENTS.md, Commits).
- A message never prints a token that holds `=` or `;`; it names it by position (`UsageValue.NamedAsPath`).
- `Planning/` reports nothing: `RunPlanner` returns errors as data (src/SqlSource.Tool/AGENTS.md).
- A sidecar is deleted only in a run with no `--project`, no `--database` and no `.sql` path; this must still hold for the unplaced path.

## Review Focus

- The unplaced path with no other filter: exit `1`, `SQLSRC212`, nothing described, no sidecar deleted (Task 1, `FilterRunTests`).
- The unplaced path beside a `.sql` path that is claimed: the claimed file's queries are selected, the unplaced one is reported (Task 1, `FilterTests`).
- The unplaced path beside `--database`: still selects nothing (covered by `isNamed` being false for every file; Task 1 `RunFiltersTests` pins that the filter is kept).
- The unplaced path given twice: reported once (Task 1, `RunFiltersTests`).
- An unplaced path whose token holds `=` or `;` (`a=b\..\..\q.sql`): named by its position (Task 1, `FilterTests`).

All four command-line cases skip on Windows, where `Path.GetFullPath` resolves a backslash's `..` itself, as the existing cases do.

---

### Task 1: Keep, report and select nothing for an unplaced `.sql` path

**Files:**
- Modify: `src/SqlSource.Tool/Planning/FileFilter.cs`
- Modify: `src/SqlSource.Tool/Planning/RunFilters.cs`
- Modify: `src/SqlSource.Tool/Planning/RunPlanner.cs` (`Select`, ~line 420)
- Modify: `src/SqlSource.Tool/DescribeCommand.cs:336`
- Test: `tests/SqlSource.Tool.Tests/Planning/RunFiltersTests.cs`
- Test: `tests/SqlSource.Tool.Tests/Commands/FilterRunTests.cs`
- Test: `tests/SqlSource.Tool.Tests/Commands/FilterTests.cs`

**Interfaces:**
- Produces: `internal sealed record FileFilter(string? NormalizedPath, string Named)`, null when `SqlPath.Normalize` cannot place the path.  `RunFilters.NamesFiles` is removed; callers use `!Files.IsEmpty`.

- [ ] **Step 1: Write the failing tests**

`RunFiltersTests`: replace `Create_SqlPathThatClimbsAboveTheRoot_PlacesNothingAndNamesFiles` with `Create_SqlPathThatClimbsAboveTheRoot_KeepsItWithNoNormalizedPath` (skip on Windows; `climbing` as today): `Files.ShouldHaveSingleItem()` has `NormalizedPath` null and `Named` equal to `$"'{Path.GetFullPath(climbing, WorkingDirectory)}'"`; `IsEmpty` is false.  Add `Create_SqlPathThatClimbsAboveTheRootGivenTwice_KeepsItOnce`: the same token twice, positions 1 and 2, gives one file.  Replace the two `NamesFiles` assertions with `Files.ShouldHaveSingleItem()` / `Files.ShouldBeEmpty()`.

`FilterRunTests.Run_SqlPathThatNamesNoFile_KeepsTheSidecarOfAFileThatNeedsNoEntry`: rename to `Run_SqlPathThatClimbsAboveTheRoot_IsSqlsrc212AndDescribesNothing`, update the comment, and assert `ExitCode` `1`, `Error` contains `error SQLSRC212: '` + the full path (`Path.GetFullPath(climbing, _scene.Run.Folder.Path)` or the scene's equivalent), `Out` does not contain `1 described`, and the `_plain` sidecar is unchanged.

`FilterTests`: add, skipping on Windows,
- `Plan_SqlPathThatClimbsAboveTheRootBesideOneThatIsClaimed_ReportsTheOneAndSelectsTheOther`, as `Plan_SqlPathThatNoTypeClaimsBesideOneThatIsClaimed_…` with the climbing path in place of `Q/Missing.sql`: exit `1`, `SQLSRC212`, selected `["GetUser", "GetInvoice", "Plain"]`.
- `Plan_SqlPathThatClimbsAboveTheRootWithAnEqualsSign_IsNamedByItsPosition`: `"a=b" + climbing-tail` gives exactly the `CliResult` of `Plan_SqlPathWithAnEqualsSign_IsNamedByItsPosition`.

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~RunFiltersTests|FullyQualifiedName~FilterRunTests|FullyQualifiedName~FilterTests"`
Expected: the new and changed cases fail (no `SQLSRC212`, `Files` empty); it does not compile until `NamesFiles` is gone from the tests.

- [ ] **Step 3: Implement**

- `FileFilter.NormalizedPath` becomes `string?`, documented as null for a path whose `..` climbs above the root.
- `RunFilters.Create` adds every path: key `normalized ?? path` in `seen`, `new FileFilter(normalized, given.NamedAsPath(path))`.  Remove `NamesFiles`, its `init` and the TD-0068 sentence; the summary says such a path is kept with no normalised path, selects nothing and is reported by the planner.
- `RunPlanner.Select`: `named` takes the non-null paths; the error filter is `filter.NormalizedPath is not { } path || !planned.Contains(path)`.  Update the comment above `Select` to say a path that names no planned file, or none at all, is `SQLSRC212`.
- `DescribeCommand.OptionsOf`: `plan.Filters.NamesFiles` becomes `!plan.Filters.Files.IsEmpty`.

- [ ] **Step 4: Run the tests and see them pass**

Same command as Step 2.  Expected: PASS.

- [ ] **Step 5: Validate and commit**

Run `./pre-commit-validation.sh`; expected exit 0.  Commit `Report a .sql path above the root as SQLSRC212 and select nothing (TD-0068)`.

### Task 2: Docs and tech-debt

**Files:**
- Modify: `docs/manual/tool.md` (Known issues: remove the bullet that starts "On Linux and macOS, a `.sql` path written with backslashes")
- Modify: `docs/manual/diagnostics.md` (`## SQLSRC212`)
- Modify: `docs/architecture/tool.md:121`
- Delete: `docs/tech-debt/TD-0068-sql-path-that-climbs-above-the-root-is-ignored.md` and its row in `docs/tech-debt/README.md` (`Next id` stays `TD-0070`)

- [ ] **Step 1: Edit**
- `diagnostics.md`, SQLSRC212: after the sentence on relative paths, add that on Linux and macOS a backslash is part of a name, so a path written with backslashes whose `..` go above the root of the file system names no file and is reported here; suggest writing it with `/`.  Effects only, no internals.
- `architecture/tool.md`: replace the last sentence ("A path whose `..` climbs above the root … deletes no sidecar") with: such a path is kept in `RunFilters.Files` with no normalised path, selects no file and is `SQLSRC212`; any file filter keeps the run from deleting a sidecar.
- Check `grep -rn "TD-0068\|NamesFiles" docs src tests --include='*.md' --include='*.cs'` finds nothing outside `docs/superpowers/`.

- [ ] **Step 2: Validate and commit**

Run `./pre-commit-validation.sh` (the diagnostics test checks `diagnostics.md`); expected exit 0.  Commit `Document that a .sql path above the root is SQLSRC212; resolve TD-0068`.
