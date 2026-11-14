# Test cleanup phase 43: tests/SqlSource.Tests/Generation/Sidecars, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Sidecars`
- Findings: 3 (3 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 24
- Guard: mutation (generator-generation-sidecars), with covers and run time for 1 finding

## Files

- `tests/SqlSource.Tests/Generation/Sidecars/SidecarInputTests.cs`
- `tests/SqlSource.Tests/Generation/Sidecars/SidecarNeedsTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation-3-10

- test: `tests/SqlSource.Tests/Generation/Sidecars/SidecarInputTests.cs` | `SidecarInputTests.Create_SidecarOfAClaimedFile_HoldsTheFileAndThePathOfItsSqlFileAsClaimed` | rows: all
- action: strengthen
- evidence: measured (2 rows, kills 2, alone 1; survivor src/SqlSource/Generation/Sidecars/SidecarInput.cs:30:16 Equality mutation -> `index <= 0`, covered by this test and `Create_SidecarOfAFileNoTypeClaims_IsNull`).  Read: both rows (SidecarInputTests.cs:14-15) claim `app/Users.sql`, the second of the claimed paths (line 11), so the mutant, which makes the first claimed path's sidecar unclaimed, passes the test whose name promises a claimed file gives an input; blind: no
- cover: none
- proposal: Add a row `"/app/Orders.sql.json"` expecting `app/Orders.sql`, the claimed path at index 0, which kills the survivor (the assertion of the SQL path becomes the row's argument).
- verdict: confirmed
- verifier's reason: `kills` lists `SURVIVED SidecarInput.cs:30:16 Equality mutation -> index <= 0` under the test.  `Claimed` is `app/Orders.sql`, `app/Users.sql` (SidecarInputTests.cs:11), and both rows (14-15) resolve to `app/Users.sql`, index 1, so the mutant, which returns null for the claimed path at index 0, passes them; the null-expecting theory has index -1 in both rows.  A row of `/app/Orders.sql.json` expecting `app/Orders.sql` kills it.
- survivors to kill: `src/SqlSource/Generation/Sidecars/SidecarInput.cs:30:16`

### F-SqlSource.Tests.Generation-3-11

- test: `tests/SqlSource.Tests/Generation/Sidecars/SidecarNeedsTests.cs` | `SidecarNeedsTests.Merge_Files_AreSortedByPathIgnoringCase` | rows: all
- action: strengthen
- evidence: read (the paths `app/b.sql` and `app/A.sql` (SidecarNeedsTests.cs:52-53) sort as `app/A.sql`, `app/b.sql` both ordinally and ignoring case, so the test would pass if `Merge` sorted with `StringComparer.Ordinal` instead of `SqlPath.Comparer` (SidecarNeeds.cs:57, SqlPath.cs:38); the doc comment of `Merge` says the order is `SqlPath.Comparer`'s so that a file's needs are found with a search by it, and an ordinal order would make that search miss.  The comparer is in src/SqlSource/Generation/Paths, outside the scope, so mutation cannot see this); blind: no
- cover: none
- proposal: Use paths whose ordinal order differs from their order ignoring case, as `app/a.sql` and `app/B.sql`, expecting `["app/a.sql", "app/B.sql"]`.
- verdict: confirmed
- verifier's reason: `SqlPath.Comparer` is `StringComparer.OrdinalIgnoreCase` (src/SqlSource/Generation/Paths/SqlPath.cs:38).  `app/A.sql` and `app/b.sql` (SidecarNeedsTests.cs:52-53) sort the same ordinally ('A' 0x41 < 'b' 0x62) and ignoring case, so the test would pass with an ordinal sort at SidecarNeeds.cs:57, which would break the comparer's search that the doc comment (50) promises.  `kills` shows only the `OrderBy`→`OrderByDescending` kill; the comparer is outside the sidecars scope, so mutation cannot see it.  `app/a.sql` and `app/B.sql` differ in the two orders (ordinal `B`,`a`; ignoring case `a`,`B`) and would fix it.
- survivors to kill: none named by place; the missing assertion is the proposal's

### F-SqlSource.Tests.Generation-3-12

- test: `tests/SqlSource.Tests/Generation/Sidecars/SidecarNeedsTests.cs` | `SidecarNeedsTests.Merge_NeedsOfOneType_AreInOrdinalOrderOfTheirNames` | rows: all
- action: strengthen
- evidence: measured (kills 2, alone 0; cover `Merge_NeedsOfTwoTypes_AreTheUnionByNameWithTheGreatestOutput`).  Read: the names `ListUsers` and `GetUser` (SidecarNeedsTests.cs:19-20) sort the same under any comparer, so nothing in it is ordinal: it would pass with `StringComparer.OrdinalIgnoreCase` at SidecarNeeds.cs:63, which would also merge two queries whose names differ only in case, and as it stands it proves only the sorting that the union test already proves; blind: no
- cover: none
- proposal: Give the type queries whose ordinal order differs from their order ignoring case, as `a` and `B`, expecting `["B", "a"]`; that asserts the name's promise and makes the test no longer covered by the union test.
- verdict: confirmed
- verifier's reason: `GetUser` and `ListUsers` (SidecarNeedsTests.cs:19-20, 46) sort the same under any comparer, so the test would pass with `StringComparer.OrdinalIgnoreCase` at SidecarNeeds.cs:63, which would also merge two names that differ only in case, against the doc comment's "ordinal order of the queries' names" (49).  `kills` shows its two kills (68:21, 69:17) both also killed by `Merge_NeedsOfTwoTypes_AreTheUnionByNameWithTheGreatestOutput`, which already proves the sort (35-37).  Names `a` and `B`, expecting `["B", "a"]`, assert the name's promise.
- survivors to kill: none named by place; the missing assertion is the proposal's

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-sidecars`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-43/generator-generation-sidecars/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Sidecars.' 'Generation/Sidecars/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-43/generator-generation-sidecars/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Sidecars.' 'Generation/Sidecars/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-43/generator-generation-sidecars/before/reports/mutation-report.json artifacts/test-cleanup/phase-43/generator-generation-sidecars/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-43/generator-generation-sidecars/before/reports/mutation-report.json SidecarInputTests.Create_SidecarOfAClaimedFile_HoldsTheFileAndThePathOfItsSqlFileAsClaimed
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-43/generator-generation-sidecars/before/reports/mutation-report.json SidecarNeedsTests.Merge_Files_AreSortedByPathIgnoringCase
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-43/generator-generation-sidecars/before/reports/mutation-report.json SidecarNeedsTests.Merge_NeedsOfOneType_AreInOrdinalOrderOfTheirNames
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Generation-3-10: `src/SqlSource/Generation/Sidecars/SidecarInput.cs:30:16`
- F-SqlSource.Tests.Generation-3-11: no survivor is named by place; the test fails when the assertion it adds is removed, checked once by hand
- F-SqlSource.Tests.Generation-3-12: no survivor is named by place; the test fails when the assertion it adds is removed, checked once by hand

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-sidecars`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-43/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-43/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-43/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-43/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-43/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-43/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tests.Generation-3-11.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
