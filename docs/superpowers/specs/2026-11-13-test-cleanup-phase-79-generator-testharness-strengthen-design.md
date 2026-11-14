# Test cleanup phase 79: tests/SqlSource.Tests/TestHarness, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/TestHarness`
- Findings: 1 (1 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 8
- Guard: covers and run time

## Files

- `tests/SqlSource.Tests/TestHarness/TestDatabasesTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.TestHarness-1-01

- test: `tests/SqlSource.Tests/TestHarness/TestDatabasesTests.cs` | `TestDatabasesTests.ConnectionStringOf_TemplateWithADatabase_ReplacesItAndKeepsTheRest` | rows: all
- action: strengthen
- evidence: read (`tests/DatabaseTests/TestHarness/TestDatabases.cs:14-21` replaces the database by removing SqlClient's `Initial Catalog` and setting `Database`, and its comment names that removal as the reason the string keeps one database entry; neither row of the test, `TestDatabasesTests.cs:13-14`, has an `Initial Catalog`, so removing `builder.Remove("Initial Catalog")` leaves the test passing.  The assertion of `TestDatabasesTests.cs:25`, one key equal to `Database` ignoring case, cannot fail, since `DbConnectionStringBuilder`'s keys are a case-insensitive dictionary that holds `Database` once after line 20 sets it.  "Keeps the rest" is checked only by the count of keys at line 24, so a change that altered another entry's value, as the password's or the host's, would pass.  No mutation scope ran the slice); blind: no
- cover: none
- proposal: Add a row whose template names its database as `Initial Catalog=master`, assert that no `Initial Catalog` key is left, and assert each key of the template other than the database entry has the template's value in the result, in place of the count and the tautological key count.
- verdict: confirmed
- verifier's reason: The finding reads the test rightly.  `tests/DatabaseTests/TestHarness/TestDatabases.cs:14-21` removes `Initial Catalog` and sets `Database`, and its comment names the removal as what keeps one database entry; neither row at `tests/SqlSource.Tests/TestHarness/TestDatabasesTests.cs:13-14` has an `Initial Catalog`, and no other test reaches `ConnectionStringOf` with one (`grep -rn "Initial Catalog"` over `tests/` finds only `TestDatabases.cs:15,19`), so line 19 can go unnoticed.  Line 25 cannot fail: `DbConnectionStringBuilder` keeps its keys in a case-insensitive dictionary, so a key equal to `Database` ignoring case appears once at most, and line 23 already shows it is there.  It also misses the purpose the plan gave it (`docs/superpowers/plans/2026-10-28-database-tests-2-harness.md:36`, "not add a second key"): an implementation that appended `;Database=x` would re-parse to one key with the last value and pass lines 23-25.  "Keeps the rest" rests on the count of line 24 alone, so a changed value of another key passes.  No mutation scope ran the slice; the strengthen is judged by reading.
- survivors to kill: none named by place; the missing assertion is the proposal's

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
