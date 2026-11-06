# TD-0113 `.env` search tests inside a git work tree Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The two `DotEnvSearchTests` that assume no `.git` above the temporary folder skip, with a reason, when the temporary folder is inside a git work tree, and TD-0113 is resolved.

**Architecture:** A test-only change.  Each of the two tests starts with `Assert.SkipWhen(DotEnvSearch.IsInRepository(_folder.Path), "The temporary folder is inside a git work tree.")`, so on such a machine it reports a skip instead of a false failure; elsewhere it runs as before.

**Tech Stack:** C#, xunit v3 (`Assert.SkipWhen`, already in `xunit.v3` 4.0.1), Shouldly.

**Spec:** `docs/tech-debt/TD-0113-two-env-search-tests-assume-no-repository-above-the-temporary-folder.md`

## Global Constraints

- Skip reason, word for word: `The temporary folder is inside a git work tree.`
- Condition: `DotEnvSearch.IsInRepository(_folder.Path)` (the spec's proposed fix; `TempFolder.Path` is the folder's full name).
- Resolving follows `docs/tech-debt/AGENTS.md` "Resolving an item": delete the item file, remove its row from `docs/tech-debt/README.md` (only that row; td-0111 and td-0112 edit their own beside it), and remove every reference to `TD-0113`.
- The one other reference is the sentence of `docs/tech-debt/TD-0109-…md` line 5, "The tests that depend on a timer or on the machine are TD-0111 to TD-0113."  It becomes "… are TD-0111 and TD-0112."  td-0111 and td-0112 resolve those two in parallel; whichever merges later rewrites the sentence again on its sync (recorded in the shared notes as `td-0113-notes.md`).
- No user-visible behavior changes, so no manual, README or example changes; there is no known issue for this item in `docs/manual/`.

## Review Focus

- Temporary folder inside a git work tree with a `.env` above it: `Read_OutsideARepository_ReadsOnlyTheUnitFolder` skips, never fails.  Verified by running the class with `TMPDIR` pointed inside this checkout.
- Temporary folder inside a git work tree with no `.env` above it: `Read_NoEnvAnywhere_GivesNoFile` skips too (the spec skips it whenever in a repository, not only when a `.env` is found).
- Usual machine (temporary folder outside any work tree): both tests still run and pass; no other test of the class skips.
- The other tests of the class make their own `.git` and must not gain a skip.

---

### Task 1: Skip the two tests in a work tree and resolve TD-0113

**Files:**
- Modify: `tests/SqlSource.Tool.Tests/DotEnv/DotEnvSearchTests.cs` (`Read_NoEnvAnywhere_GivesNoFile`, `Read_OutsideARepository_ReadsOnlyTheUnitFolder`)
- Delete: `docs/tech-debt/TD-0113-two-env-search-tests-assume-no-repository-above-the-temporary-folder.md`
- Modify: `docs/tech-debt/README.md` (TD-0113's row only)
- Modify: `docs/tech-debt/TD-0109-duplicated-arrangements-and-uneven-conventions-in-tests.md` (line 5's sentence)

**Interfaces:**
- Consumes: `DotEnvSearch.IsInRepository(string folder) : bool` (`src/SqlSource.Tool/DotEnv/DotEnvSearch.cs`), `TempFolder.Path : string`.

- [ ] **Step 1: Show the failure.**  Run the class with the temporary folder inside this checkout:
  `mkdir -p .td0113tmp && TMPDIR=$PWD/.td0113tmp dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.DotEnv.DotEnvSearchTests"` (a `.env` at `.td0113tmp/` level is written by the test itself).  Expected: `Read_OutsideARepository_ReadsOnlyTheUnitFolder` fails.
- [ ] **Step 2: Add the skip** as the first statement of both tests, with the condition and reason above.
- [ ] **Step 3: Run Step 1's command again.**  Expected: both tests reported skipped with the reason, the rest pass.  Remove `.td0113tmp`.
- [ ] **Step 4: Run the class with the usual temporary folder.**  Expected: every test passes, none skipped.
- [ ] **Step 5: Resolve the item** (delete file, remove row, rewrite TD-0109's sentence); `grep -rn "TD-0113" . --exclude-dir=bin --exclude-dir=obj` finds only this plan.
- [ ] **Step 6: `./format.sh`, then `./pre-commit-validation.sh`, then commit.**

### Task 2: The other tests outside a repository (owner's answer to review finding R1-F1)

The review found 13 more tests of `tests/SqlSource.Tool.Tests` that fail the same way, since `WatchSet` climbs to a `.git` through the same `DotEnvSearch`: 8 cases of `Watching/WatchSetTests.cs` and 7 tests of `Commands/Watch/` (`WatchTests`, `WatchFolderTests`, `WatchDuringAPassTests`, `WatchEndingTests`).  The owner chose to guard them all.

- [ ] **Step 1:** Add `AboveTheTempFolder` at the project's root: `RepositoryReason` (the reason above) and `SkipWhenInARepository(string folder)`, which skips when `DotEnvSearch.IsInRepository(folder)`.  The two `DotEnvSearchTests` call it too.
- [ ] **Step 2:** Call it at the start of each affected test on its `TempFolder` (`watch.Run.Folder.Path` for a `WatchScene`), and in a theory only on its `repository: false` rows.
- [ ] **Step 3:** Run `tests/SqlSource.Tool.Tests` with `TMPDIR` below a folder that holds an empty `.git`: no failure, the guarded tests skipped.  Run it with the usual temporary folder: no new skip.
- [ ] **Step 4:** Add the rule to `tests/SqlSource.Tool.Tests/AGENTS.md`; `./format.sh`, `./pre-commit-validation.sh`, commit.

### Task 3: The online build's `auto` test under a `.env` above (owner's answer to review finding R2-F1)

`OnlineBuildTests.Build_AutoWithoutAConnectionSource_RunsNothingAndWritesNoFileOfIt` fails when any folder above the temporary folder holds a `.env`, since the build's `GetDirectoryNameOfFileAbove` looks up to the root of the file system.  The owner chose a skip with its own reason; the build's lookup stays as it is.

- [ ] **Step 1:** Add `EnvReason` = `A folder above the temporary folder holds a .env.` and `SkipWhenAnEnvIsAbove(string folder)` to `AboveTheTempFolder`, which skips when a folder above `folder` holds a `.env` file; call it with `_fixtures.Root` at the start of that test.
- [ ] **Step 2:** Run `tests/SqlSource.Tool.Tests` with `TMPDIR` below a folder that holds only a `.env`, and below one that holds only an empty `.git`: no failure.  With the usual temporary folder: no new skip.
- [ ] **Step 3:** Update the rule of `tests/SqlSource.Tool.Tests/AGENTS.md`; `./format.sh`, `./pre-commit-validation.sh`, commit.
