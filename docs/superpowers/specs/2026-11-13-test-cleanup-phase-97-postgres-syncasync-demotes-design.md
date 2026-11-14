# Test cleanup phase 97: tests/DatabaseTests/Postgres.SyncAsync, demotes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/DatabaseTests/Postgres.SyncAsync`
- Findings: 1 (1 demote)
- Kind: one kind, demote
- Changed lines: about 19
- Guard: covers and run time

## Files

- a new server-free class in `tests/DatabaseTests/Postgres.SyncAsync/`
- `tests/DatabaseTests/Postgres.SyncAsync/AGENTS.md`
- `tests/DatabaseTests/Postgres.SyncAsync/PairTests.cs`
- `tests/DatabaseTests/Postgres.SyncAsync/README.md`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-engines-11

- test: `tests/DatabaseTests/Postgres.SyncAsync/PairTests.cs` | `PairTests.Find_GeneratedPairs_EachHasItsScenarios` | rows: all
- action: demote
- evidence: read (twin fault of F-DatabaseTests.SqlServer.SyncAsync-1-01; the Postgres.SyncAsync-1 slice has no finding.  PairTests.cs:12-13 puts the class in `[Collection(UsesPostgis.Name)]` with a `PostgresDatabases` constructor, so running this fact, even alone by a filter, starts the PostGIS container, and builds its image when none exists (tests/DatabaseTests/AGENTS.md); the fact (:45-47) reads only the static `Pairs`, `MethodPairs.Find` over the assembly by reflection (:15), and the static `AllScenarios.List`, through `ScenarioCoverage.GapsOf`, pure LINQ, never `databases`.  The project's AGENTS.md keeps `KnownDifferencesTests` and `LibraryRenderingTests` outside `UsesPostgis` "so that they run without Docker", and README.md:20 and :32-35 list those two alone as the tests that need no server; this check, the one a developer runs after adding a query (AGENTS.md, README.md:64), needs none either.  0.003 s against the class's 35.3 s.  The searched documents name the test only for what it checks and that only the nightly runs it.  No mutation scope ran the slice.); blind: no
- cover: to be written: `ScenarioCoverageTests.Find_GeneratedPairs_EachHasItsScenarios` in `tests/DatabaseTests/Postgres.SyncAsync/ScenarioCoverageTests.cs`
- proposal: Move the fact, unchanged in what it asserts (`ScenarioCoverage.GapsOf(MethodPairs.Find(assembly), AllScenarios.List)` is empty), into a sealed class outside `UsesPostgis` with no fixture, and add it to the readme's command and the AGENTS.md rule for the tests that need no server, as F-DatabaseTests.SqlServer.SyncAsync-1-01 does for SQL Server.
- verdict: confirmed
- verifier's reason: tests/DatabaseTests/Postgres.SyncAsync/PairTests.cs:12-13 puts the class in `[Collection(UsesPostgis.Name)]` with a `PostgresDatabases` constructor; `UsesPostgis` is an `ICollectionFixture<PostgresDatabases>` (UsesPostgis.cs:8-9) and `PostgresDatabases` a `PostgisContainer` (PostgresDatabases.cs:15), so the fact run alone still starts, and may build, the PostGIS container.  The fact (:45-47) reads only the static `Pairs` (`MethodPairs.Find` over the assembly, :15), the static `AllScenarios.List` (Scenarios/AllScenarios.cs:11-31, plain lists) and `ScenarioCoverage.GapsOf` (TestHarness/SyncAsync/ScenarioCoverage.cs:18), never `databases`.  The project's AGENTS.md:17 keeps `KnownDifferencesTests` and `LibraryRenderingTests` outside `UsesPostgis` "so that they run without Docker", and README.md:20 and :32-36 list them as the tests needing no server; this one is of that kind.  The documents name it only for what it checks (AGENTS.md:14, README.md:64) and the plan docs/superpowers/plans/2026-11-12-sync-methods-part-4.md:214 places it in `PairTests` without a reason.  The proposed unit-level class is specified with its assertion unchanged.  Twin of F-DatabaseTests.SqlServer.SyncAsync-1-01, confirmed; the cleanup updates AGENTS.md:17 and the readme's command in the same change.
- condition: The verifier adds: update `tests/DatabaseTests/Postgres.SyncAsync/AGENTS.md:17` and the readme's command in the same change (`README.md:64` and `AGENTS.md:14` name the test).

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-engines-11 | demote | `PairTests.Find_GeneratedPairs_EachHasItsScenarios` | to be written: `ScenarioCoverageTests.Find_GeneratedPairs_EachHasItsScenarios` in `tests/DatabaseTests/Postgres.SyncAsync/ScenarioCoverageTests.cs` |

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/Postgres.SyncAsync`.
A `SyncAsync` project is a test project only under `-p:RunSyncAsync=true`, and runs no test under `--coverage`: run it by the command of its readme.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

Lines that name a test of the phase; the phase updates each line its change makes untrue, as the conditions below say:

- `tests/DatabaseTests/Postgres.SyncAsync/AGENTS.md:14` (F-engines-11)
- `tests/DatabaseTests/Postgres.SyncAsync/README.md:64` (F-engines-11)

Edits the findings and their verdicts ask for:

- F-engines-11: The verifier adds: update `tests/DatabaseTests/Postgres.SyncAsync/AGENTS.md:17` and the readme's command in the same change (`README.md:64` and `AGENTS.md:14` name the test).

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
