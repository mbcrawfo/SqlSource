# Test cleanup phase 80: tests/SqlSource.Tests/TestHarness, loosening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/TestHarness`
- Findings: 1 (1 loosen)
- Kind: one kind, loosen
- Changed lines: about 10
- Guard: covers and run time

## Files

- `tests/SqlSource.Tests/TestHarness/PostgisContainerTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.TestHarness-1-02

- test: `tests/SqlSource.Tests/TestHarness/PostgisContainerTests.cs` | `PostgisContainerTests.ImageOf_PostgresImage_NamesItsTagPostgisVersionAndDockerfileHash` | rows: all (`"postgres:18.6", @"^sqlsource-postgis:18\.6-3\.6\.4-[0-9a-f]{12}$"` line 23; `"postgres:14.24", @"^sqlsource-postgis:14\.24-3\.6\.4-[0-9a-f]{12}$"` line 24)
- action: loosen
- evidence: read (both patterns spell the PostGIS version `3\.6\.4` literally, while `PostgisContainer.ImageOf` takes it from `PostgisContainer.Package` at `tests/DatabaseTests/TestHarness/PostgisContainer.cs:25,30`.  Moving PostGIS, which root `AGENTS.md` rule 5 and `tests/DatabaseTests/Postgres/README.md:31` plan for as a change of `Package` and the two Dockerfiles' defaults, "since `PostgisContainerTests` keeps the three equal", breaks this test though the form of the name, tag then upstream version then hash, is unchanged; neither document lists this test among the places that name the version.  The test has no comment marking the literal as a record, and the plan that wrote it, `docs/superpowers/plans/2026-11-12-postgis-image-prebuild.md:48-49`, gives no reason for it.  No mutation scope ran the slice); blind: no
- cover: none
- proposal: Build the version part of each pattern from `PostgisContainer.Package`, its text before the first `+` escaped with `Regex.Escape`, so the test still proves the name's form, the Debian suffix dropped, and a PostGIS move changes only the places the readme lists.
- verdict: confirmed
- verifier's reason: The finding reads the test rightly.  Both rows, `tests/SqlSource.Tests/TestHarness/PostgisContainerTests.cs:23-24`, spell `3\.6\.4`, while `ImageOf` takes it from `Package.Split('+')[0]` (`tests/DatabaseTests/TestHarness/PostgisContainer.cs:25,30`).  Root `AGENTS.md:36` and `tests/DatabaseTests/Postgres/README.md:31` plan a PostGIS move as a change of `Package` and the two Dockerfiles' `POSTGIS` defaults, kept equal by `PostgisContainerTests`, and list no other place; `grep -rn` for the version over `tests`, `src`, `tools`, `docs`, `examples` and the root documents finds it in no other test.  So a planned move breaks this test while the name's form, tag then upstream version then hash, is unchanged.  No comment above the test or the class (lines 12-15) calls the literal a record, and the plan that wrote it, `docs/superpowers/plans/2026-11-12-postgis-image-prebuild.md:44-51`, gives no reason for it.  The proposal still proves the Debian suffix is dropped for today's `Package`.  No mutation scope ran the slice.

## The guard

### Covers and run time

No mutation scope runs these tests.  The pull request lists each removed test or row beside its named cover, read and confirmed, and each touched test project's run time before and after the change, on one machine.

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
