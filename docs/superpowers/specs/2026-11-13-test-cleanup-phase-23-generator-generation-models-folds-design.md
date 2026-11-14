# Test cleanup phase 23: tests/SqlSource.Tests/Generation/Models, folds

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Models`
- Findings: 1 (1 fold)
- Kind: one kind, fold
- Changed lines: about 11
- Guard: mutation (generator-generation-models), with covers and run time for 1 finding

## Files

- `tests/SqlSource.Tests/Generation/Models/ModelCollectorTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation-2-05

- test: `tests/SqlSource.Tests/Generation/Models/ModelCollectorTests.cs` | `ModelCollectorTests.Resolve_NoGroups_GivesNoPlaces`; `ModelCollectorTests.Resolve_NoGroups_GivesNoAssignments` | rows: all
- action: fold
- evidence: measured (each 1 row, reaches no tested mutant); read (ModelCollectorTests.cs:165-175: the two tests make the same call, `ModelCollector.Resolve(EquatableArray<ModelGroup>.Empty, EquatableArray<TakenName>.Empty)`, and each asserts one member of the one result empty, `Places` and `Assignments`; no comment above either, and no AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names them or ModelCollectorTests); blind: no
- cover: `tests/SqlSource.Tests/Generation/Models/ModelCollectorTests.cs` | `ModelCollectorTests.Resolve_NoGroups_GivesNoPlaces`; `tests/SqlSource.Tests/Generation/Models/ModelCollectorTests.cs` | `ModelCollectorTests.Resolve_NoGroups_GivesNoAssignments`
- proposal: Make them one test, `Resolve_NoGroups_GivesAnEmptySet`, that resolves no groups once and asserts its models, places, assignments and diagnostics empty.
- verdict: confirmed
- verifier's reason: Both tests make the same call, `ModelCollector.Resolve(EquatableArray<ModelGroup>.Empty, EquatableArray<TakenName>.Empty)` (ModelCollectorTests.cs:165-175).  Each asserts one member of the result empty.  That path is the early return at ModelCollector.cs:82-90, on lines listed blind (81-154), so `kills` gives 0 kills for each.  `ModelSet` has Models, Places, Diagnostics and Assignments (ModelSet.cs:14-18), so the one proposed test can assert all four empty.  No comment stands above either test, and no record document names them; only plans name ModelCollectorTests.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-models`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-23/generator-generation-models/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Models.' 'Generation/Models/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-23/generator-generation-models/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Models.' 'Generation/Models/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-23/generator-generation-models/before/reports/mutation-report.json artifacts/test-cleanup/phase-23/generator-generation-models/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-23/generator-generation-models/before/reports/mutation-report.json ModelCollectorTests.Resolve_NoGroups_GivesNoPlaces
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-23/generator-generation-models/before/reports/mutation-report.json ModelCollectorTests.Resolve_NoGroups_GivesNoAssignments
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-models`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-23/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-23/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-23/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-23/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-23/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-23/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tests.Generation-2-05.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tests.Generation-2-05 | fold | `ModelCollectorTests.Resolve_NoGroups_GivesNoPlaces`, `ModelCollectorTests.Resolve_NoGroups_GivesNoAssignments` (rows: all) | `tests/SqlSource.Tests/Generation/Models/ModelCollectorTests.cs`: `ModelCollectorTests.Resolve_NoGroups_GivesNoPlaces`; `tests/SqlSource.Tests/Generation/Models/ModelCollectorTests.cs`: `ModelCollectorTests.Resolve_NoGroups_GivesNoAssignments` |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
