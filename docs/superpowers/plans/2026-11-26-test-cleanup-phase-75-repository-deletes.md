# Test cleanup phase 75: repository deletes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the two tests of `tests/SqlSource.Repository.Tests` that the test review found covered by others.

**Architecture:** Test changes only.  No mutation scope runs these tests, so the guard is covers and run time: each removed test beside its named cover, read and confirmed, and the project's run time before and after on one machine.

**Tech Stack:** C#, xUnit v3, Shouldly.

**Spec:** `docs/superpowers/specs/2026-11-13-test-cleanup-phase-75-repository-deletes-design.md`, under the epic `docs/superpowers/specs/2026-11-13-test-cleanup-epic-design.md` ("How a phase is done").

## Global Constraints

- Change only the two tests the findings name; no helper is used by them alone.
- Every commit passes `./pre-commit-validation.sh`.
- The phase sets only its own row of the epic outline's table.

### Task 0: Check the findings and run the guard's "before" half

- [x] F-SqlSource.Repository.Tests-1-02 still holds.  `site/diagnostics.md` gives `sqlsrc001` and `sqlsrc236` only by heading slug (`## SQLSRC001`, `## SQLSRC236`); `site/reference/errors.md` links its anchors as `../diagnostics.md#sqlsrcNNN` and the page links its own as `#sqlsrcNNN`, so `RelativeLinks_OfTheUserDocs_ReachAFileAndAnchorThatExist` fails if the real page's anchors are not read.  `Links_OfTheUserDocs_AreFound` still requires links of `site/diagnostics.md`, and `UserDocsReadingTests.Anchors_RepeatedHeading_GetsASuffix` still proves on lines of its own that a heading becomes its slug.
- [x] F-SqlSource.Repository.Tests-2-01 still holds.  `Parse_ProjectLine_MapsTheProjectToItsEngine` proves the key is `TestScopes.AssemblyPrefix` plus the project file's name and that `ProjectFiles` keeps the path as written; `TestScopesFileTests.RepositoryFile_Projects_AreTheFourContainerProjects` parses `tools/test-scopes.txt`, whose line 14 is `project postgres tests/DatabaseTests/Postgres/Postgres.csproj`, and pins the literal key `SqlSource.DatabaseTests.Postgres`.
- [x] No document but the plans and specs of `docs/superpowers/` names either test; `tests/SqlSource.Repository.Tests/Documentation/AGENTS.md:3` names `UserDocsLinksTests` as the class that checks links and anchors, which stays true.
- [x] Run time of `dotnet test --project tests/SqlSource.Repository.Tests`: 645 tests, 2.68 s and 2.57 s on two warm runs (9.69 s on the first run after a build).

### Task 1: Apply the findings

**Files:**
- Modify: `tests/SqlSource.Repository.Tests/Documentation/UserDocsLinksTests.cs`
- Modify: `tests/SqlSource.Repository.Tests/Validation/TestScopesTests.cs`

- [x] **Step 1:** Delete `UserDocsLinksTests.Anchors_HeadingsOfAPage_AreTheirSlugs` (F-SqlSource.Repository.Tests-1-02).
- [x] **Step 2:** Delete `TestScopesTests.Parse_ProjectLine_IsKeyedByThePrefixedProjectName` and the comment above it (F-SqlSource.Repository.Tests-2-01).  The comment explained the naming for this test only; the rule itself is in `tests/DatabaseTests/AGENTS.md` and `tests/DatabaseTests/Directory.Build.props`, and `TestScopes.cs` says what checks it.
- [x] **Step 3:** Run time of `dotnet test --project tests/SqlSource.Repository.Tests`: 643 tests, 2.71 s and 2.62 s on two warm runs, on the same machine.  The two fewer tests take a few milliseconds, so the difference is noise.
- [x] **Step 4:** Set the phase's row of the epic outline to `done`.
- [ ] **Step 5:** The pull request gives the change, each removed test beside its cover and the run times.

## The guard's results

| Finding | Test removed | Named cover |
|----|----|----|
| F-SqlSource.Repository.Tests-1-02 | `UserDocsLinksTests.Anchors_HeadingsOfAPage_AreTheirSlugs` | `UserDocsReadingTests.Anchors_RepeatedHeading_GetsASuffix`; `UserDocsLinksTests.RelativeLinks_OfTheUserDocs_ReachAFileAndAnchorThatExist`; `UserDocsLinksTests.Links_OfTheUserDocs_AreFound` |
| F-SqlSource.Repository.Tests-2-01 | `TestScopesTests.Parse_ProjectLine_IsKeyedByThePrefixedProjectName` | `TestScopesTests.Parse_ProjectLine_MapsTheProjectToItsEngine`; `TestScopesFileTests.RepositoryFile_Projects_AreTheFourContainerProjects` |

| Run | Tests | Warm runs |
|----|----|----|
| Before | 645 | 2.68 s, 2.57 s |
| After | 643 | 2.71 s, 2.62 s |
