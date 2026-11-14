# Test cleanup phase 100: tests/DatabaseTests/SqlServer.SyncAsync, demotes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/DatabaseTests/SqlServer.SyncAsync`
- Findings: 1 (1 demote)
- Kind: one kind, demote
- Changed lines: about 19
- Guard: covers and run time

## Files

- a new server-free class in `tests/DatabaseTests/SqlServer.SyncAsync/`
- `tests/DatabaseTests/SqlServer.SyncAsync/AGENTS.md`
- `tests/DatabaseTests/SqlServer.SyncAsync/PairTests.cs`
- `tests/DatabaseTests/SqlServer.SyncAsync/README.md`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-DatabaseTests.SqlServer.SyncAsync-1-01

- test: `tests/DatabaseTests/SqlServer.SyncAsync/PairTests.cs` | `PairTests.Find_GeneratedPairs_EachHasItsScenarios` | rows: all
- action: demote
- evidence: read (`PairTests.cs:12-13` puts the class in `[Collection(UsesSqlServer.Name)]` with a `SqlServerDatabases` constructor, so running this test, even alone by a filter, starts the SQL Server container; `PairTests.cs:45-47` reads only `Pairs`, which `MethodPairs.Find` takes from the assembly by reflection at `PairTests.cs:15`, and `AllScenarios.List`, never `databases`; `README.md:20` and `README.md:32-35` of the project list the tests that need no server, to run without Docker, as `KnownDifferencesTests` and `LibraryRenderingTests` alone, and `AGENTS.md:19` keeps those two outside `UsesSqlServer` "so that they run without Docker"; this one, the check that fails when a query is added without its scenarios (`AGENTS.md:14`, `README.md:64`), needs no server either.  Measured 0.012 s against the class's 510.4 s.  The search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` found the test only in the project's `AGENTS.md:14` and `README.md:64`, which say what it checks and that only the nightly runs it, and nothing that ties it to the server); blind: no
- cover: to be written: `ScenarioCoverageTests.Find_GeneratedPairs_EachHasItsScenarios` in `tests/DatabaseTests/SqlServer.SyncAsync/ScenarioCoverageTests.cs`
- proposal: Move the test, unchanged in what it asserts (`ScenarioCoverage.GapsOf(MethodPairs.Find(assembly), AllScenarios.List)` is empty), into a sealed class outside `UsesSqlServer` with no fixture, and add that class to the readme's command for the tests that need no server, so that the check a developer runs after adding a query no longer starts SQL Server under emulation.
- verdict: confirmed
- verifier's reason: Read `tests/DatabaseTests/SqlServer.SyncAsync/PairTests.cs:12-15,45-47`: the class is `[Collection(UsesSqlServer.Name)]` with a `SqlServerDatabases` primary constructor, and `UsesSqlServer.cs:7-8` makes `SqlServerDatabases` an `ICollectionFixture`, whose base `SqlServerContainer` is `IAsyncLifetime` and starts the container in `InitializeAsync` (`tests/DatabaseTests/TestHarness/SqlServerContainer.cs:15,77-83`), so selecting this fact alone still starts SQL Server.  The fact reads only the static `Pairs` (`MethodPairs.Find` over the assembly by reflection, `TestHarness/SyncAsync/MethodPair.cs:94-119`) and the static `AllScenarios.List` (`Scenarios/AllScenarios.cs:11-35`), and `ScenarioCoverage.GapsOf` (`TestHarness/SyncAsync/ScenarioCoverage.cs:18-52`) is pure LINQ over them; nothing in `Scenarios/` reads the fixture beyond its constants.  The project's own rule for tests that need no server (`AGENTS.md:19`, `README.md:20,32-36`) keeps such tests outside `UsesSqlServer` so they run without Docker, and this one is of that kind.  The search of the `AGENTS.md` files, readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` names the test only in `SqlServer.SyncAsync/AGENTS.md:14` and `README.md:64` (what it checks, and that only the nightly runs it), with nothing tying it to the server; the only other mention is the implementation plan `docs/superpowers/plans/2026-11-12-sync-methods-part-4.md:214`, which places it in `PairTests` without a reason.  The comment above the class (`PairTests.cs:11`) only describes it.  No mutation scope ran the slice, so the move is judged by reading: the assertion is unchanged and needs no engine.  The cleanup must also update `AGENTS.md:19`, which names the two server-free classes, in the same change as the readme's command.
- condition: The verifier adds: update `tests/DatabaseTests/SqlServer.SyncAsync/AGENTS.md:19`, which names the server-free classes, in the same change as the readme's command (`README.md:64` names the test).

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-DatabaseTests.SqlServer.SyncAsync-1-01 | demote | `PairTests.Find_GeneratedPairs_EachHasItsScenarios` | to be written: `ScenarioCoverageTests.Find_GeneratedPairs_EachHasItsScenarios` in `tests/DatabaseTests/SqlServer.SyncAsync/ScenarioCoverageTests.cs` |

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/SqlServer.SyncAsync`.
A `SyncAsync` project is a test project only under `-p:RunSyncAsync=true`, and runs no test under `--coverage`: run it by the command of its readme.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

Lines that name a test of the phase; the phase updates each line its change makes untrue, as the conditions below say:

- `tests/DatabaseTests/SqlServer.SyncAsync/AGENTS.md:14` (F-DatabaseTests.SqlServer.SyncAsync-1-01)
- `tests/DatabaseTests/SqlServer.SyncAsync/README.md:64` (F-DatabaseTests.SqlServer.SyncAsync-1-01)

Edits the findings and their verdicts ask for:

- F-DatabaseTests.SqlServer.SyncAsync-1-01: The verifier adds: update `tests/DatabaseTests/SqlServer.SyncAsync/AGENTS.md:19`, which names the server-free classes, in the same change as the readme's command (`README.md:64` names the test).

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
