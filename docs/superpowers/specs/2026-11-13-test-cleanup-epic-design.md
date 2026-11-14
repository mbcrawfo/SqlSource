# Test cleanup epic of 2026-11-13

The outline of the cleanup that the test review of 2026-11-13 proposes, kept current until the epic closes: each phase's pull request sets its row's state, and a phase that changes the plan changes this outline in the same pull request.  The review is the skill `.claude/skills/reviewing-test-quality/`; its findings, verdicts and evidence are in `artifacts/test-review/2026-11-13/` of the machine that ran it, and what the epic needs of them is in the phase specs and the appendix, [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).

## The run

- **Date**: 2026-11-13.
- **Commit read**: `36864a86587181e09dcf183d25f4ffa50d22fc94`.  A line number in a phase spec or the appendix is of that commit; a phase finds each test by its name, since earlier phases move the lines.
- **Folders**: every folder of `projects.txt`: `tests/SqlSource.Tests`, `tests/SqlSource.Tests.RoslynFloor`, `tests/SqlSource.Repository.Tests`, `tests/SqlSource.Tool.Tests`, `tests/DatabaseTests/Postgres`, `tests/DatabaseTests/Postgres.Npgsql8`, `tests/DatabaseTests/Postgres.SyncAsync`, `tests/DatabaseTests/SqlServer`, `tests/DatabaseTests/SqlServer.SqlClient61`, `tests/DatabaseTests/SqlServer.SyncAsync` and `tests/DatabaseTests/QueryMethodAllocation`, which is reviewed by reading and never run.
- **Slices**: 60, of 1,026 files and 198,317 lines, each read in full by its reviewer; 55 had findings and one verifier each.
- **Mirrors**: the three groups `engines`, `generator` and `describers`, 14 findings, all confirmed.
- **Previous review**: none.

### Scopes

All 33 scopes of `scopes.txt` ran and passed on the first run, from 15:47:32Z to 23:46:25Z, 7 h 59 min.  A scope sees only its own source and its own tests.

| Scope | Tests run | Of | Mutants tested | Of | Time | Findings on its tests |
|----|----|----|----|----|----|----|
| `generator-root` | 2,213 | `tests/SqlSource.Tests` | 236 | `src/SqlSource` | 1 h 23 min (5026 s) | 54 |
| `generator-diagnostics` | 107 | `tests/SqlSource.Tests/Diagnostics` | 19 | `src/SqlSource/Diagnostics` | 1 min 36 s (96 s) | 5 |
| `generator-generation` | 66 | `tests/SqlSource.Tests/Generation` | 113 | `src/SqlSource/Generation` | 2 min 21 s (141 s) | 4 |
| `generator-generation-chains` | 96 | `tests/SqlSource.Tests/Generation/Chains` | 145 | `src/SqlSource/Generation/Chains` | 2 min 45 s (165 s) | 12 |
| `generator-generation-converters` | 59 | `tests/SqlSource.Tests/Generation/Converters` | 165 | `src/SqlSource/Generation/Converters` | 2 min 48 s (168 s) | 9 |
| `generator-generation-emitting` | 732 | `tests/SqlSource.Tests/Generation/Emitting` | 2,330 | `src/SqlSource/Generation/Emitting` | 38 min 7 s (2287 s) | 27 |
| `generator-generation-exceptions` | 11 | `tests/SqlSource.Tests/Generation/Exceptions` | 99 | `src/SqlSource/Generation/Exceptions` | 1 min 23 s (83 s) | 0 |
| `generator-generation-methods` | 158 | `tests/SqlSource.Tests/Generation/Methods` | 932 | `src/SqlSource/Generation/Methods` | 3 min 42 s (222 s) | 4 |
| `generator-generation-models` | 105 | `tests/SqlSource.Tests/Generation/Models` | 577 | `src/SqlSource/Generation/Models` | 3 min 24 s (204 s) | 4 |
| `generator-generation-paths` | 166 | `tests/SqlSource.Tests/Generation/Paths` | 149 | `src/SqlSource/Generation/Paths` | 2 min 36 s (156 s) | 3 |
| `generator-generation-placement` | 55 | `tests/SqlSource.Tests/Generation/Placement` | 192 | `src/SqlSource/Generation/Placement` | 1 min 20 s (80 s) | 2 |
| `generator-generation-repositories` | 125 | `tests/SqlSource.Tests/Generation/Repositories` | 291 | `src/SqlSource/Generation/Repositories` | 1 min 45 s (105 s) | 9 |
| `generator-generation-sidecars` | 27 | `tests/SqlSource.Tests/Generation/Sidecars` | 113 | `src/SqlSource/Generation/Sidecars` | 1 min 8 s (68 s) | 8 |
| `generator-generation-types` | 523 | `tests/SqlSource.Tests/Generation/Types` | 883 | `src/SqlSource/Generation/Types` | 3 min 38 s (218 s) | 10 |
| `generator-parsing` | 2,284 | `tests/SqlSource.Tests/Parsing` | 2,275 | `src/SqlSource/Parsing` | 34 min 29 s (2069 s) | 34 |
| `generator-settings` | 909 | `tests/SqlSource.Tests/Settings` | 1,225 | `src/SqlSource/Settings` | 11 min 17 s (677 s) | 24 |
| `generator-settings-files` | 201 | `tests/SqlSource.Tests/SettingsFiles` | 117 | `src/SqlSource/SettingsFiles` | 2 min 48 s (168 s) | 6 |
| `generator-snapshot` | 479 | `tests/SqlSource.Tests/Snapshot` | 934 | `src/SqlSource/Snapshot` | 58 min 55 s (3535 s) | 10 |
| `tool-root` | 823 | `tests/SqlSource.Tool.Tests` | 571 | `src/SqlSource.Tool` | 1 h 54 min (6861 s) | 31 |
| `tool-bundles` | 244 | `tests/SqlSource.Tool.Tests/Bundles` | 438 | `src/SqlSource.Tool/Bundles` | 3 min 7 s (187 s) | 7 |
| `tool-bundles-renaming` | 1,149 | `tests/SqlSource.Tool.Tests/Bundles/Renaming` | 1,226 | `src/SqlSource.Tool/Bundles/Renaming` | 13 min 12 s (792 s) | 16 |
| `tool-describing` | 313 | `tests/SqlSource.Tool.Tests/Describing` | 696 | `src/SqlSource.Tool/Describing` | 4 min 28 s (268 s) | 7 |
| `tool-describing-capture` | 149 | `tests/SqlSource.Tool.Tests/Describing/Capture` | 89 | `src/SqlSource.Tool/Describing/Capture` | 1 min 39 s (99 s) | 1 |
| `tool-describing-describers` | 77 | `tests/SqlSource.Tool.Tests/Describing/Describers` | 83 | `src/SqlSource.Tool/Describing/Describers` | 1 min 26 s (86 s) | 11 |
| `tool-describing-postgres` | 2,047 | `tests/SqlSource.Tool.Tests/Describing/Postgres` | 2,909 | `src/SqlSource.Tool/Describing/Postgres` | 30 min 23 s (1823 s) | 37 |
| `tool-describing-sqlserver` | 1,002 | `tests/SqlSource.Tool.Tests/Describing/SqlServer` | 1,728 | `src/SqlSource.Tool/Describing/SqlServer` | 9 min 50 s (590 s) | 14 |
| `tool-dotenv` | 93 | `tests/SqlSource.Tool.Tests/DotEnv` | 143 | `src/SqlSource.Tool/DotEnv` | 2 min 46 s (166 s) | 4 |
| `tool-logging` | 145 | `tests/SqlSource.Tool.Tests/Logging` | 248 | `src/SqlSource.Tool/Logging` | 3 min 50 s (230 s) | 7 |
| `tool-planning` | 204 | `tests/SqlSource.Tool.Tests/Planning` | 145 | `src/SqlSource.Tool/Planning` | 3 min 22 s (202 s) | 6 |
| `tool-processes` | 13 | `tests/SqlSource.Tool.Tests/Processes` | 26 | `src/SqlSource.Tool/Processes` | 4 min 3 s (243 s) | 3 |
| `tool-projects` | 316 | `tests/SqlSource.Tool.Tests/Projects` | 241 | `src/SqlSource.Tool/Projects` | 23 min 39 s (1419 s) | 8 |
| `tool-reporting` | 36 | `tests/SqlSource.Tool.Tests/Reporting` | 107 | `src/SqlSource.Tool/Reporting` | 2 min 42 s (162 s) | 5 |
| `tool-watching` | 144 | `tests/SqlSource.Tool.Tests/Watching` | 398 | `src/SqlSource.Tool/Watching` | 2 min 17 s (137 s) | 2 |

Findings on tests that no scope runs: 71, judged by reading under the named-cover rule.

### What it could not see

- **The database projects**, `tests/SqlSource.Repository.Tests`, the tests of the test libraries (`tests/SqlSource.Tests/TestHarness/` and `TestProjectTests` of `tests/SqlSource.Tool.Tests`) and `tests/SqlSource.Tests/Package`: no scope runs them.  Their findings rest on a named cover that a verifier read, and their phases are guarded by that list and the project's run time.
- **A test whose subject is outside its scope's source**, as most of `tests/SqlSource.Tests/Generator/` are, since `generator-root` mutates only the project's root files: unmeasured for that subject, and judged by reading.
- **What mutation cannot see**: a diagnostic's wording, an allocation budget, a wait or an order of events, an engine's behaviour, a build or a process, and blind code (a mutant that does not compile or is in a static initialiser).
- **`tests/DatabaseTests/Postgres.SyncAsync` and `tests/DatabaseTests/SqlServer.SyncAsync`**: `measure.sh` ran them without `-p:RunSyncAsync=true`, so neither was a test project, and with `--coverage`, under which neither runs a test, since neither references `Microsoft.Testing.Extensions.CodeCoverage`.  The lead ran them by hand with the property and without coverage: their tests have times and no coverage.  The review's pull request corrects `measure.sh` and `projects.txt` to run them so.
- **The broad tests**: `broad-tests.txt` named the whole namespaces `SqlSource.DatabaseTests.Postgres.Realistic.` and `SqlSource.DatabaseTests.SqlServer.Realistic.` as broad, which hold focused tests of one case or of the harness beside the classes that run the corpus.  The review's pull request narrows them to those classes; it changes no number, since no scope runs the database projects, and three findings of phases 84 and 87 rest on covers of those namespaces.
- **`tests/DatabaseTests/QueryMethodAllocation`**: never run, since its tests skip under coverage; reviewed by reading.
- **Coverage gaps**: none.  Every file of every slice was read in full by its reviewer.  No scope failed, and no test project failed.

### Findings

455 findings: 441 of the slices and 14 of the mirrors.  By action: 158 delete, 58 fold, 92 strengthen, 31 loosen, 10 demote, 106 keep.  By verdict: 447 confirmed, 7 refuted, 1 needs measurement, which was measured again and confirmed.

- **In a phase**: 342: 152 delete, 57 fold, 92 strengthen, 31 loosen, 10 demote.
- **In the appendix**: 105 confirmed **keep**, 7 refuted (one of them a **keep**), and 1 withdrawn, F-engines-09: F-DatabaseTests.SqlServer.EndToEnd-2-06 names the theory it would empty as its cover, so it is kept, and the appendix says why.
- **Unsettled**: none.

## How a phase is done

1. **Read the phase's spec** whole: its files, its findings with their verdicts and conditions, its guard, its documents and what it leaves out.  Find each test by its name; the line numbers are of the commit the review read.
2. **Check the finding still holds.**  If the code under a finding has changed since the review so that it no longer reads as the finding says, the phase leaves it out, says so in its pull request and notes it in its row here; it never applies a finding the code no longer supports.
3. **Run the guard's "before" half**: each scope's mutation run with its `before` output, the test project's coverage run, and, for findings mutation cannot guard, the project's run time.  Nothing else that builds runs in the checkout while a mutation run does.  A phase of the same scope done just before may hand its "after" report on as this phase's "before", when nothing else has changed that scope's source or tests in between.
4. **Apply the findings**, each as its proposal says, with its conditions.  A **delete** removes the test and any helper only it used; a **fold** drops the named rows or merges the named tests into one theory; a **strengthen** adds the assertion or row its finding names; a **loosen** asserts the behaviour and not its incidental form; a **demote** writes or changes the unit test it names, then removes the database test.  A test that another finding names as cover keeps every assertion and row that makes it so.
5. **Update the documents** the spec lists, in the same pull request, as `AGENTS.md` asks.
6. **Run the guard's "after" half** and compare: `compare` lists nothing `LOST` and nothing `NOT COMPARABLE` but a kill that did not repeat, which `kills` on the "before" report shows no removed or changed test made; each survivor a **strengthen** names is `Killed`; the touched source's line coverage does not fall; and the pull request lists each removed test or row beside its cover, with the run times.
7. **Validate and review** as `AGENTS.md` says: `./pre-commit-validation.sh` before each commit, `./pre-commit-validation.sh --full` before each push, the floor images after a change to a describer's tests, and `branch-review:review` until clean.
8. **Open the pull request** with the guard's results in its description, and set the phase's row to `in progress`; the pull request that merges sets it to `done`.

Phases are done in their order: deletions with measured backing first, since they shrink what every later phase reads, then folds, then strengthening and loosening, then the folders no scope runs, and the database projects last.  A phase of mixed kinds stands where the last kind it holds does.  No two phases touch one file, so phases of different folders may also run side by side, as long as no two mutation runs share the checkout.

## The phases

Counts are of findings.  The guard is **mutation** (the scope's run before and after, `compare`, and coverage), with **covers and run time** added for the findings mutation cannot guard, or **covers and run time** alone where no scope runs the tests.

| # | Phase | Scope | Delete | Fold | Strengthen | Loosen | Demote | Guard | State |
|----|----|----|----|----|----|----|----|----|----|
| 01 | [generator-driver-deletes](2026-11-13-test-cleanup-phase-01-generator-driver-deletes-design.md) | `tests/SqlSource.Tests/Generator` | 13 |  |  |  |  | mutation: `generator-root`; covers and run time for 10 | not started |
| 02 | [generator-diagnostics-deletes](2026-11-13-test-cleanup-phase-02-generator-diagnostics-deletes-design.md) | `tests/SqlSource.Tests/Diagnostics` | 2 |  |  |  |  | mutation: `generator-diagnostics`; covers and run time for 2 | not started |
| 03 | [generator-generation-deletes](2026-11-13-test-cleanup-phase-03-generator-generation-deletes-design.md) | `tests/SqlSource.Tests/Generation` | 1 |  |  |  |  | mutation: `generator-generation` | not started |
| 04 | [generator-generation-emitting-deletes](2026-11-13-test-cleanup-phase-04-generator-generation-emitting-deletes-design.md) | `tests/SqlSource.Tests/Generation/Emitting` | 6 |  |  |  |  | mutation: `generator-generation-emitting`; covers and run time for 1 | not started |
| 05 | [generator-generation-types-deletes](2026-11-13-test-cleanup-phase-05-generator-generation-types-deletes-design.md) | `tests/SqlSource.Tests/Generation/Types` | 7 |  |  |  |  | mutation: `generator-generation-types`; covers and run time for 4 | not started |
| 06 | [generator-parsing-deletes](2026-11-13-test-cleanup-phase-06-generator-parsing-deletes-design.md) | `tests/SqlSource.Tests/Parsing` | 8 |  |  |  |  | mutation: `generator-parsing`; covers and run time for 3 | not started |
| 07 | [generator-settings-deletes](2026-11-13-test-cleanup-phase-07-generator-settings-deletes-design.md) | `tests/SqlSource.Tests/Settings` | 1 |  |  |  |  | mutation: `generator-settings` | not started |
| 08 | [generator-snapshot-deletes](2026-11-13-test-cleanup-phase-08-generator-snapshot-deletes-design.md) | `tests/SqlSource.Tests/Snapshot` | 2 |  |  |  |  | mutation: `generator-snapshot` | not started |
| 09 | [tool-root-deletes](2026-11-13-test-cleanup-phase-09-tool-root-deletes-design.md) | `tests/SqlSource.Tool.Tests (root files)` | 2 |  |  |  |  | mutation: `tool-root`; covers and run time for 1 | not started |
| 10 | [tool-commands-deletes](2026-11-13-test-cleanup-phase-10-tool-commands-deletes-design.md) | `tests/SqlSource.Tool.Tests/Commands` | 6 |  |  |  |  | mutation: `tool-root` | not started |
| 11 | [tool-bundles-renaming-deletes](2026-11-13-test-cleanup-phase-11-tool-bundles-renaming-deletes-design.md) | `tests/SqlSource.Tool.Tests/Bundles/Renaming` | 2 |  |  |  |  | mutation: `tool-bundles-renaming` | not started |
| 12 | [tool-describing-deletes](2026-11-13-test-cleanup-phase-12-tool-describing-deletes-design.md) | `tests/SqlSource.Tool.Tests/Describing` | 1 |  |  |  |  | mutation: `tool-describing` | not started |
| 13 | [tool-describing-describers-deletes](2026-11-13-test-cleanup-phase-13-tool-describing-describers-deletes-design.md) | `tests/SqlSource.Tool.Tests/Describing/Describers` | 2 |  |  |  |  | mutation: `tool-describing-describers` | not started |
| 14 | [tool-describing-postgres-deletes](2026-11-13-test-cleanup-phase-14-tool-describing-postgres-deletes-design.md) | `tests/SqlSource.Tool.Tests/Describing/Postgres` | 16 |  |  |  |  | mutation: `tool-describing-postgres`; covers and run time for 7 | not started |
| 15 | [tool-describing-sqlserver-deletes](2026-11-13-test-cleanup-phase-15-tool-describing-sqlserver-deletes-design.md) | `tests/SqlSource.Tool.Tests/Describing/SqlServer` | 5 |  |  |  |  | mutation: `tool-describing-sqlserver`; covers and run time for 1 | not started |
| 16 | [tool-logging-deletes](2026-11-13-test-cleanup-phase-16-tool-logging-deletes-design.md) | `tests/SqlSource.Tool.Tests/Logging` | 2 |  |  |  |  | mutation: `tool-logging` | not started |
| 17 | [tool-planning-deletes](2026-11-13-test-cleanup-phase-17-tool-planning-deletes-design.md) | `tests/SqlSource.Tool.Tests/Planning` | 3 |  |  |  |  | mutation: `tool-planning` | not started |
| 18 | [tool-projects-deletes](2026-11-13-test-cleanup-phase-18-tool-projects-deletes-design.md) | `tests/SqlSource.Tool.Tests/Projects` | 5 |  |  |  |  | mutation: `tool-projects` | not started |
| 19 | [generator-driver-folds](2026-11-13-test-cleanup-phase-19-generator-driver-folds-design.md) | `tests/SqlSource.Tests/Generator` |  | 8 |  |  |  | mutation: `generator-root`; covers and run time for 6 | not started |
| 20 | [generator-generation-folds](2026-11-13-test-cleanup-phase-20-generator-generation-folds-design.md) | `tests/SqlSource.Tests/Generation` |  | 1 |  |  |  | mutation: `generator-generation`; covers and run time for 1 | not started |
| 21 | [generator-generation-emitting-folds](2026-11-13-test-cleanup-phase-21-generator-generation-emitting-folds-design.md) | `tests/SqlSource.Tests/Generation/Emitting` |  | 1 |  |  |  | mutation: `generator-generation-emitting` | not started |
| 22 | [generator-generation-methods-folds](2026-11-13-test-cleanup-phase-22-generator-generation-methods-folds-design.md) | `tests/SqlSource.Tests/Generation/Methods` |  | 1 |  |  |  | mutation: `generator-generation-methods` | not started |
| 23 | [generator-generation-models-folds](2026-11-13-test-cleanup-phase-23-generator-generation-models-folds-design.md) | `tests/SqlSource.Tests/Generation/Models` |  | 1 |  |  |  | mutation: `generator-generation-models`; covers and run time for 1 | not started |
| 24 | [generator-generation-models-mixed](2026-11-13-test-cleanup-phase-24-generator-generation-models-mixed-design.md) | `tests/SqlSource.Tests/Generation/Models` | 1 | 1 |  |  |  | mutation: `generator-generation-models` | not started |
| 25 | [generator-generation-paths-folds](2026-11-13-test-cleanup-phase-25-generator-generation-paths-folds-design.md) | `tests/SqlSource.Tests/Generation/Paths` |  | 1 |  |  |  | mutation: `generator-generation-paths` | not started |
| 26 | [generator-generation-types-folds](2026-11-13-test-cleanup-phase-26-generator-generation-types-folds-design.md) | `tests/SqlSource.Tests/Generation/Types` |  | 1 |  |  |  | mutation: `generator-generation-types` | not started |
| 27 | [generator-settings-folds](2026-11-13-test-cleanup-phase-27-generator-settings-folds-design.md) | `tests/SqlSource.Tests/Settings` |  | 1 |  |  |  | mutation: `generator-settings` | not started |
| 28 | [tool-commands-folds](2026-11-13-test-cleanup-phase-28-tool-commands-folds-design.md) | `tests/SqlSource.Tool.Tests/Commands` |  | 2 |  |  |  | mutation: `tool-root`; covers and run time for 2 | not started |
| 29 | [tool-bundles-folds](2026-11-13-test-cleanup-phase-29-tool-bundles-folds-design.md) | `tests/SqlSource.Tool.Tests/Bundles` |  | 2 |  |  |  | mutation: `tool-bundles` | not started |
| 30 | [tool-describing-postgres-folds](2026-11-13-test-cleanup-phase-30-tool-describing-postgres-folds-design.md) | `tests/SqlSource.Tool.Tests/Describing/Postgres` |  | 2 |  |  |  | mutation: `tool-describing-postgres` | not started |
| 31 | [tool-logging-folds](2026-11-13-test-cleanup-phase-31-tool-logging-folds-design.md) | `tests/SqlSource.Tool.Tests/Logging` |  | 1 |  |  |  | mutation: `tool-logging` | not started |
| 32 | [tool-logging-mixed](2026-11-13-test-cleanup-phase-32-tool-logging-mixed-design.md) | `tests/SqlSource.Tool.Tests/Logging` | 1 | 1 |  |  |  | mutation: `tool-logging`; covers and run time for 1 | not started |
| 33 | [generator-root-strengthen](2026-11-13-test-cleanup-phase-33-generator-root-strengthen-design.md) | `tests/SqlSource.Tests (root files)` |  |  | 2 |  |  | mutation: `generator-root` | not started |
| 34 | [generator-driver-strengthen](2026-11-13-test-cleanup-phase-34-generator-driver-strengthen-design.md) | `tests/SqlSource.Tests/Generator` |  |  | 2 |  |  | mutation: `generator-root`; covers and run time for 2 | not started |
| 35 | [generator-generation-chains-strengthen](2026-11-13-test-cleanup-phase-35-generator-generation-chains-strengthen-design.md) | `tests/SqlSource.Tests/Generation/Chains` |  |  | 1 |  |  | mutation: `generator-generation-chains` | not started |
| 36 | [generator-generation-chains-mixed](2026-11-13-test-cleanup-phase-36-generator-generation-chains-mixed-design.md) | `tests/SqlSource.Tests/Generation/Chains` | 3 |  | 2 |  |  | mutation: `generator-generation-chains` | not started |
| 37 | [generator-generation-emitting-strengthen](2026-11-13-test-cleanup-phase-37-generator-generation-emitting-strengthen-design.md) | `tests/SqlSource.Tests/Generation/Emitting` |  |  | 3 |  |  | mutation: `generator-generation-emitting` | not started |
| 38 | [generator-generation-methods-strengthen](2026-11-13-test-cleanup-phase-38-generator-generation-methods-strengthen-design.md) | `tests/SqlSource.Tests/Generation/Methods` |  |  | 1 |  |  | mutation: `generator-generation-methods` | not started |
| 39 | [generator-generation-models-strengthen](2026-11-13-test-cleanup-phase-39-generator-generation-models-strengthen-design.md) | `tests/SqlSource.Tests/Generation/Models` |  |  | 1 |  |  | mutation: `generator-generation-models` | not started |
| 40 | [generator-generation-paths-strengthen](2026-11-13-test-cleanup-phase-40-generator-generation-paths-strengthen-design.md) | `tests/SqlSource.Tests/Generation/Paths` |  |  | 2 |  |  | mutation: `generator-generation-paths` | not started |
| 41 | [generator-generation-placement-strengthen](2026-11-13-test-cleanup-phase-41-generator-generation-placement-strengthen-design.md) | `tests/SqlSource.Tests/Generation/Placement` |  |  | 2 |  |  | mutation: `generator-generation-placement` | not started |
| 42 | [generator-generation-repositories-mixed](2026-11-13-test-cleanup-phase-42-generator-generation-repositories-mixed-design.md) | `tests/SqlSource.Tests/Generation/Repositories` |  | 2 | 3 |  |  | mutation: `generator-generation-repositories` | not started |
| 43 | [generator-generation-sidecars-strengthen](2026-11-13-test-cleanup-phase-43-generator-generation-sidecars-strengthen-design.md) | `tests/SqlSource.Tests/Generation/Sidecars` |  |  | 3 |  |  | mutation: `generator-generation-sidecars`; covers and run time for 1 | not started |
| 44 | [generator-generation-types-strengthen](2026-11-13-test-cleanup-phase-44-generator-generation-types-strengthen-design.md) | `tests/SqlSource.Tests/Generation/Types` |  |  | 1 |  |  | mutation: `generator-generation-types` | not started |
| 45 | [generator-parsing-strengthen](2026-11-13-test-cleanup-phase-45-generator-parsing-strengthen-design.md) | `tests/SqlSource.Tests/Parsing` |  |  | 6 |  |  | mutation: `generator-parsing` | not started |
| 46 | [generator-settings-strengthen](2026-11-13-test-cleanup-phase-46-generator-settings-strengthen-design.md) | `tests/SqlSource.Tests/Settings` |  |  | 10 |  |  | mutation: `generator-settings`; covers and run time for 3 | not started |
| 47 | [generator-settings-mixed](2026-11-13-test-cleanup-phase-47-generator-settings-mixed-design.md) | `tests/SqlSource.Tests/Settings` | 3 | 1 | 3 |  |  | mutation: `generator-settings` | not started |
| 48 | [generator-snapshot-mixed](2026-11-13-test-cleanup-phase-48-generator-snapshot-mixed-design.md) | `tests/SqlSource.Tests/Snapshot` | 2 |  | 4 |  |  | mutation: `generator-snapshot` | not started |
| 49 | [tool-commands-strengthen](2026-11-13-test-cleanup-phase-49-tool-commands-strengthen-design.md) | `tests/SqlSource.Tool.Tests/Commands` |  |  | 1 |  |  | mutation: `tool-root` | not started |
| 50 | [tool-commands-mixed](2026-11-13-test-cleanup-phase-50-tool-commands-mixed-design.md) | `tests/SqlSource.Tool.Tests/Commands` | 2 | 3 | 1 |  |  | mutation: `tool-root`; covers and run time for 4 | not started |
| 51 | [tool-bundles-renaming-strengthen](2026-11-13-test-cleanup-phase-51-tool-bundles-renaming-strengthen-design.md) | `tests/SqlSource.Tool.Tests/Bundles/Renaming` |  |  | 2 |  |  | mutation: `tool-bundles-renaming` | not started |
| 52 | [tool-bundles-renaming-mixed](2026-11-13-test-cleanup-phase-52-tool-bundles-renaming-mixed-design.md) | `tests/SqlSource.Tool.Tests/Bundles/Renaming` | 1 |  | 1 |  |  | mutation: `tool-bundles-renaming` | not started |
| 53 | [tool-describing-capture-strengthen](2026-11-13-test-cleanup-phase-53-tool-describing-capture-strengthen-design.md) | `tests/SqlSource.Tool.Tests/Describing/Capture` |  |  | 1 |  |  | mutation: `tool-describing-capture`; covers and run time for 1 | not started |
| 54 | [tool-describing-describers-mixed](2026-11-13-test-cleanup-phase-54-tool-describing-describers-mixed-design.md) | `tests/SqlSource.Tool.Tests/Describing/Describers` | 1 |  | 1 |  |  | mutation: `tool-describing-describers`; covers and run time for 1 | not started |
| 55 | [tool-describing-postgres-strengthen](2026-11-13-test-cleanup-phase-55-tool-describing-postgres-strengthen-design.md) | `tests/SqlSource.Tool.Tests/Describing/Postgres` |  |  | 4 |  |  | mutation: `tool-describing-postgres` | not started |
| 56 | [tool-describing-postgres-mixed](2026-11-13-test-cleanup-phase-56-tool-describing-postgres-mixed-design.md) | `tests/SqlSource.Tool.Tests/Describing/Postgres` |  | 1 | 3 |  |  | mutation: `tool-describing-postgres`; covers and run time for 3 | not started |
| 57 | [tool-describing-sqlserver-strengthen](2026-11-13-test-cleanup-phase-57-tool-describing-sqlserver-strengthen-design.md) | `tests/SqlSource.Tool.Tests/Describing/SqlServer` |  |  | 3 |  |  | mutation: `tool-describing-sqlserver` | not started |
| 58 | [tool-describing-sqlserver-mixed](2026-11-13-test-cleanup-phase-58-tool-describing-sqlserver-mixed-design.md) | `tests/SqlSource.Tool.Tests/Describing/SqlServer` | 3 |  | 2 |  |  | mutation: `tool-describing-sqlserver`; covers and run time for 1 | not started |
| 59 | [tool-dotenv-strengthen](2026-11-13-test-cleanup-phase-59-tool-dotenv-strengthen-design.md) | `tests/SqlSource.Tool.Tests/DotEnv` |  |  | 1 |  |  | mutation: `tool-dotenv` | not started |
| 60 | [tool-processes-strengthen](2026-11-13-test-cleanup-phase-60-tool-processes-strengthen-design.md) | `tests/SqlSource.Tool.Tests/Processes` |  |  | 2 |  |  | mutation: `tool-processes` | not started |
| 61 | [tool-projects-strengthen](2026-11-13-test-cleanup-phase-61-tool-projects-strengthen-design.md) | `tests/SqlSource.Tool.Tests/Projects` |  |  | 3 |  |  | mutation: `tool-projects` | not started |
| 62 | [tool-reporting-strengthen](2026-11-13-test-cleanup-phase-62-tool-reporting-strengthen-design.md) | `tests/SqlSource.Tool.Tests/Reporting` |  |  | 3 |  |  | mutation: `tool-reporting` | not started |
| 63 | [tool-reporting-mixed](2026-11-13-test-cleanup-phase-63-tool-reporting-mixed-design.md) | `tests/SqlSource.Tool.Tests/Reporting` | 1 |  | 1 |  |  | mutation: `tool-reporting` | not started |
| 64 | [tool-watching-strengthen](2026-11-13-test-cleanup-phase-64-tool-watching-strengthen-design.md) | `tests/SqlSource.Tool.Tests/Watching` |  |  | 1 |  |  | mutation: `tool-watching`; covers and run time for 1 | not started |
| 65 | [generator-root-loosen](2026-11-13-test-cleanup-phase-65-generator-root-loosen-design.md) | `tests/SqlSource.Tests (root files)` |  |  |  | 1 |  | mutation: `generator-root`; covers and run time for 1 | not started |
| 66 | [generator-driver-loosen](2026-11-13-test-cleanup-phase-66-generator-driver-loosen-design.md) | `tests/SqlSource.Tests/Generator` |  |  |  | 9 |  | mutation: `generator-root`; covers and run time for 3 | not started |
| 67 | [generator-driver-mixed](2026-11-13-test-cleanup-phase-67-generator-driver-mixed-design.md) | `tests/SqlSource.Tests/Generator` | 2 | 5 |  | 3 |  | mutation: `generator-root`; covers and run time for 5 | not started |
| 68 | [generator-generation-loosen](2026-11-13-test-cleanup-phase-68-generator-generation-loosen-design.md) | `tests/SqlSource.Tests/Generation` |  |  |  | 1 |  | mutation: `generator-generation` | not started |
| 69 | [generator-generation-converters-mixed](2026-11-13-test-cleanup-phase-69-generator-generation-converters-mixed-design.md) | `tests/SqlSource.Tests/Generation/Converters` | 3 |  | 1 | 1 |  | mutation: `generator-generation-converters`; covers and run time for 2 | not started |
| 70 | [generator-generation-emitting-mixed](2026-11-13-test-cleanup-phase-70-generator-generation-emitting-mixed-design.md) | `tests/SqlSource.Tests/Generation/Emitting` | 1 | 3 | 4 | 3 |  | mutation: `generator-generation-emitting`; covers and run time for 1 | not started |
| 71 | [generator-parsing-mixed](2026-11-13-test-cleanup-phase-71-generator-parsing-mixed-design.md) | `tests/SqlSource.Tests/Parsing` | 4 | 3 | 5 | 1 |  | mutation: `generator-parsing`; covers and run time for 5 | not started |
| 72 | [tool-commands-loosen](2026-11-13-test-cleanup-phase-72-tool-commands-loosen-design.md) | `tests/SqlSource.Tool.Tests/Commands` |  |  |  | 6 |  | mutation: `tool-root`; covers and run time for 6 | not started |
| 73 | [tool-bundles-loosen](2026-11-13-test-cleanup-phase-73-tool-bundles-loosen-design.md) | `tests/SqlSource.Tool.Tests/Bundles` |  |  |  | 1 |  | mutation: `tool-bundles` | not started |
| 74 | [tool-describing-mixed](2026-11-13-test-cleanup-phase-74-tool-describing-mixed-design.md) | `tests/SqlSource.Tool.Tests/Describing` | 1 | 2 |  | 2 |  | mutation: `tool-describing`; covers and run time for 1 | not started |
| 75 | [repository-deletes](2026-11-13-test-cleanup-phase-75-repository-deletes-design.md) | `tests/SqlSource.Repository.Tests` | 2 |  |  |  |  | covers and run time | not started |
| 76 | [generator-testharness-syncasync-deletes](2026-11-13-test-cleanup-phase-76-generator-testharness-syncasync-deletes-design.md) | `tests/SqlSource.Tests/TestHarness/SyncAsync` | 1 |  |  |  |  | covers and run time | not started |
| 77 | [repository-strengthen](2026-11-13-test-cleanup-phase-77-repository-strengthen-design.md) | `tests/SqlSource.Repository.Tests` |  |  | 1 |  |  | covers and run time | not started |
| 78 | [generator-package-mixed](2026-11-13-test-cleanup-phase-78-generator-package-mixed-design.md) | `tests/SqlSource.Tests/Package` | 2 |  | 1 |  |  | covers and run time | not started |
| 79 | [generator-testharness-strengthen](2026-11-13-test-cleanup-phase-79-generator-testharness-strengthen-design.md) | `tests/SqlSource.Tests/TestHarness` |  |  | 1 |  |  | covers and run time | not started |
| 80 | [generator-testharness-loosen](2026-11-13-test-cleanup-phase-80-generator-testharness-loosen-design.md) | `tests/SqlSource.Tests/TestHarness` |  |  |  | 1 |  | covers and run time | not started |
| 81 | [postgres-deletes](2026-11-13-test-cleanup-phase-81-postgres-deletes-design.md) | `tests/DatabaseTests/Postgres` | 2 |  |  |  |  | covers and run time | not started |
| 82 | [postgres-describer-deletes](2026-11-13-test-cleanup-phase-82-postgres-describer-deletes-design.md) | `tests/DatabaseTests/Postgres/Describer` | 2 |  |  |  |  | covers and run time | not started |
| 83 | [postgres-endtoend-deletes](2026-11-13-test-cleanup-phase-83-postgres-endtoend-deletes-design.md) | `tests/DatabaseTests/Postgres/EndToEnd` | 4 |  |  |  |  | covers and run time | not started |
| 84 | [postgres-realistic-deletes](2026-11-13-test-cleanup-phase-84-postgres-realistic-deletes-design.md) | `tests/DatabaseTests/Postgres/Realistic` | 1 |  |  |  |  | covers and run time | not started |
| 85 | [sqlserver-deletes](2026-11-13-test-cleanup-phase-85-sqlserver-deletes-design.md) | `tests/DatabaseTests/SqlServer` | 3 |  |  |  |  | covers and run time | not started |
| 86 | [sqlserver-endtoend-deletes](2026-11-13-test-cleanup-phase-86-sqlserver-endtoend-deletes-design.md) | `tests/DatabaseTests/SqlServer/EndToEnd` | 3 |  |  |  |  | covers and run time | not started |
| 87 | [sqlserver-realistic-deletes](2026-11-13-test-cleanup-phase-87-sqlserver-realistic-deletes-design.md) | `tests/DatabaseTests/SqlServer/Realistic` | 2 |  |  |  |  | covers and run time | not started |
| 88 | [postgres-npgsql8-folds](2026-11-13-test-cleanup-phase-88-postgres-npgsql8-folds-design.md) | `tests/DatabaseTests/Postgres.Npgsql8` |  | 1 |  |  |  | covers and run time | not started |
| 89 | [postgres-npgsql8-and-endtoend-folds](2026-11-13-test-cleanup-phase-89-postgres-npgsql8-and-endtoend-folds-design.md) | `tests/DatabaseTests/Postgres.Npgsql8`, `tests/DatabaseTests/Postgres/EndToEnd` |  | 2 |  |  |  | covers and run time | not started |
| 90 | [postgres-describer-folds](2026-11-13-test-cleanup-phase-90-postgres-describer-folds-design.md) | `tests/DatabaseTests/Postgres/Describer` |  | 2 |  |  |  | covers and run time | not started |
| 91 | [postgres-endtoend-mixed](2026-11-13-test-cleanup-phase-91-postgres-endtoend-mixed-design.md) | `tests/DatabaseTests/Postgres/EndToEnd` | 12 | 1 |  |  |  | covers and run time | not started |
| 92 | [sqlserver-sqlclient61-and-endtoend-folds](2026-11-13-test-cleanup-phase-92-sqlserver-sqlclient61-and-endtoend-folds-design.md) | `tests/DatabaseTests/SqlServer.SqlClient61`, `tests/DatabaseTests/SqlServer/EndToEnd` |  | 2 |  |  |  | covers and run time | not started |
| 93 | [sqlserver-endtoend-folds](2026-11-13-test-cleanup-phase-93-sqlserver-endtoend-folds-design.md) | `tests/DatabaseTests/SqlServer/EndToEnd` |  | 3 |  |  |  | covers and run time | not started |
| 94 | [sqlserver-endtoend-mixed](2026-11-13-test-cleanup-phase-94-sqlserver-endtoend-mixed-design.md) | `tests/DatabaseTests/SqlServer/EndToEnd` | 3 | 1 |  |  |  | covers and run time | not started |
| 95 | [postgres-npgsql8-strengthen](2026-11-13-test-cleanup-phase-95-postgres-npgsql8-strengthen-design.md) | `tests/DatabaseTests/Postgres.Npgsql8` |  |  | 2 |  |  | covers and run time | not started |
| 96 | [sqlserver-sqlclient61-strengthen](2026-11-13-test-cleanup-phase-96-sqlserver-sqlclient61-strengthen-design.md) | `tests/DatabaseTests/SqlServer.SqlClient61` |  |  | 1 |  |  | covers and run time | not started |
| 97 | [postgres-syncasync-demotes](2026-11-13-test-cleanup-phase-97-postgres-syncasync-demotes-design.md) | `tests/DatabaseTests/Postgres.SyncAsync` |  |  |  |  | 1 | covers and run time | not started |
| 98 | [postgres-describer-mixed](2026-11-13-test-cleanup-phase-98-postgres-describer-mixed-design.md) | `tests/DatabaseTests/Postgres/Describer` | 1 |  |  | 1 | 5 | mutation: `tool-describing-postgres`; covers and run time for 7 | not started |
| 99 | [describers-and-commands-mixed](2026-11-13-test-cleanup-phase-99-describers-and-commands-mixed-design.md) | `tests/DatabaseTests/Postgres/Describer`, `tests/DatabaseTests/SqlServer/Describer`, `tests/SqlSource.Tool.Tests/Commands` | 1 | 1 |  | 1 | 3 | mutation: `tool-root`, `tool-describing-sqlserver`; covers and run time for 5 | not started |
| 100 | [sqlserver-syncasync-demotes](2026-11-13-test-cleanup-phase-100-sqlserver-syncasync-demotes-design.md) | `tests/DatabaseTests/SqlServer.SyncAsync` |  |  |  |  | 1 | covers and run time | not started |
| | **Total** | | **152** | **57** | **92** | **31** | **10** | | |

The rules give no least size.  76 of the 100 phases change fewer than about 50 lines each, about 1426 in all; they stay cut, since each holds one folder and one kind, or the files that tie kinds together.  Line counts are the writer's estimate from the tests' spans.

## The tech-debt items

Both source projects had every scope run, so each has an item for the code its own tests do not check: [TD-0114](../../tech-debt/TD-0114-generator-code-its-own-tests-do-not-check.md) for `src/SqlSource` and [TD-0115](../../tech-debt/TD-0115-tool-code-its-own-tests-do-not-check.md) for `src/SqlSource.Tool`.  The appendix counts those gaps by file.

