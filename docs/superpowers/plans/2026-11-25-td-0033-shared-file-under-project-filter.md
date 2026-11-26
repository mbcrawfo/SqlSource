# TD-0033: a shared `.sql` file under `--project` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A solution run with `--project` never writes the sidecar of a `.sql` file without the entries that a project of the solution it leaves out needs.

**Architecture:** The owner chose option A (Q1, "A, solution runs only").  `RunProjects.FindAsync` evaluates the projects that `--project` leaves out too, reports nothing of them, and gives their manifests as `FoundProjects.Others`.  `RunPlanner.Plan` reads their claims and settings files only for the files a project of the run claims: they add to what each query needs, so the file's target holds their entries, but they select no query.  The existing rules do the rest: a query that only a left-out project needs is `Skipped` when its entry is current, and `LeftOut` otherwise, which holds the file back.

**Tech Stack:** C#, xUnit v3, Shouldly.

**Spec:** `docs/tech-debt/TD-0033-sql-file-that-two-projects-list-has-one-sidecar.md`, narrowed by the owner's Q1.

## Global Constraints

- Only a solution run with `--project` (describe, `--check`, a watch) reads the left-out projects.  `diagnose`, a run on a project alone and a run from `--manifest` are unchanged.
- A left-out project reports nothing: not `SQLSRC204`, `SQLSRC220`, `SQLSRC205`, its claim errors or its settings files' problems.  One that cannot be evaluated is passed over.
- The sidecar format does not change.  TD-0033 stays open, rewritten, for a run on a project alone, the build's run, a left-out project that cannot be evaluated, and `SQLSRC245`.

## Review Focus

- A query that only the left-out project needs and whose entry is stale: the file is held back, its sidecar left as it was (command test).
- A left-out project that is not restored: no error, the run goes on (RunProjects test).
- A file that only a left-out project claims: not planned, not written (planner test).
- `--check --project`: an entry that only the left-out project needs is no difference (command test).
- The left-out project's own errors (a bad `Source`) are not reported (planner test).

---

### Task 1: `RunProjects` evaluates the left-out projects

**Files:** `src/SqlSource.Tool/Projects/RunProjects.cs`, `FoundProjects.cs`, `src/SqlSource.Tool/Logging/RunEvents.cs`, `src/SqlSource.Tool/PlanSteps.cs`, `src/SqlSource.Tool/Bundles/DiagnoseRun.cs`, `src/SqlSource.Tool/Watching/WatchSession.cs`; tests `tests/SqlSource.Tool.Tests/Projects/RunProjectsTests.cs`.

**Interfaces:**
- `RunProjects.FindAsync(unit, named, configuration, context, bool readsOthers = false)`.
- `FoundProjects(Projects, Manifests, AssetsFiles, ImmutableArray<ProjectManifest> Others = default)`; `Others` is empty unless `readsOthers` and `--project` left a project of a solution out.
- `PlanSteps.FindManifestsAsync(unit, usage, context, bool readsOthers)`: describe's path and the watch pass `true`, `diagnose` `false`.
- `PlanSteps.PlanFromAsync(unit, manifests, others, usage, context)` and `PlanFromAsync(unit, manifests, others, filters, isLoggedOnly, context)`.

- [ ] Tests: `Find_OneProjectNamed_ReadsTheOthersForTheFilesTheyShare` (manifests are B's alone, `Others` are A's and C's, all asked, no error), `Find_OneProjectNamedAndAnotherNotRestored_SaysNothingOfIt`, `Find_OneProjectNamedWithoutReadingOthers_IsTheOnlyOneAskedAbout` (diagnose's way).
- [ ] Implement: evaluate the run's and the left-out projects in one `EvaluateAsync` call when `readsOthers`; split the evaluations; a left-out project's event is the left-out one, saying it is read for the files it shares when it uses SqlSource.
- [ ] Commit.

### Task 2: `RunPlanner` takes the left-out manifests' needs

**Files:** `src/SqlSource.Tool/Planning/RunPlanner.cs`; tests `tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs`.

**Interfaces:** `RunPlanner.Plan(manifests, filters, cancellationToken, ImmutableArray<ProjectManifest> others = default)`.

- [ ] Tests: `Plan_QueryThatOnlyALeftOutProjectNeeds_NeedsAnEntryAndIsNotSelected`, `Plan_FileThatOnlyALeftOutProjectClaims_IsNotPlanned`, `Plan_LeftOutProjectsErrors_AreNotReported`, `Plan_LeftOutProject_CountsNoUnclaimedFiles`.
- [ ] Implement: a `ClaimedFile` knows whether its project is in the run; the left-out manifests add claimants only to planned files; a query is selected only when a project of the run needs it; `Select` narrows that further.
- [ ] Commit.

### Task 3: A run keeps what the left-out project needs

**Files:** tests `tests/SqlSource.Tool.Tests/Commands/FilterRunTests.cs` and `CheckFilterTests.cs`.

- [ ] Tests: `Run_Project_KeepsTheCurrentEntryThatALeftOutProjectNeeds`, `Run_Project_HoldsBackAFileWhoseLeftOutEntryIsStale`, `Check_Project_FindsNoDifferenceInAnEntryThatALeftOutProjectNeeds`.
- [ ] Commit.

### Task 4: Documents

- [ ] Rewrite `docs/tech-debt/TD-0033-*.md` and its README row to what remains.
- [ ] `site/reference/sidecars.md#limit-file-in-two-projects`, `site/guides/several-projects.md`, `site/guides/troubleshooting.md`, `site/diagnostics.md` (`SQLSRC245`), `site/reference/cli.md` (`--project`).
- [ ] `docs/architecture/tool.md` (finding the projects, the plan, the watch's limits), `src/SqlSource.Tool/Projects/AGENTS.md`, `src/SqlSource.Tool/Planning/AGENTS.md`.
- [ ] Format, validate, commit.
