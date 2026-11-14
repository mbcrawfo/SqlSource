# Test cleanup phase 39: tests/SqlSource.Tests/Generation/Models, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Models`
- Findings: 1 (1 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 8
- Guard: mutation (generator-generation-models)

## Files

- `tests/SqlSource.Tests/Generation/Models/ModelsByFileTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation-2-06

- test: `tests/SqlSource.Tests/Generation/Models/ModelsByFileTests.cs` | `ModelsByFileTests.Arrange_ModelsOfOneQueryAndRole_KeepTheSetsOrderWhenTheirPlacesAreEqual` | rows: all
- action: strengthen
- evidence: measured (survivor src/SqlSource/Generation/Models/ModelsByFile.cs:186:20 Conditional (true) mutation `(true?byType :left.CompareTo(right))`, covered by this test and `Arrange_TwoTypesClaimOneQuery_PutsBothModelsInItsSlotByClaimingType` alone; timeouts under it 5); read (ModelsByFile.cs:186 breaks a tie of equal places by the index in the set, which is what the name promises; the test sorts two models, and `Array.Sort` sorts 16 or fewer items by insertion sort, which keeps equal items in their order, so the order holds with the tie-break removed); blind: no
- cover: none
- proposal: Give one place more than 16 models, listed in the set in an order that a sort without the tie-break would change, and assert the slot holds them in the set's order.
- verdict: confirmed
- verifier's reason: `kills` in generator-generation-models lists `SURVIVED ModelsByFile.cs:186:20 Conditional (true)` under the test.  The order comes from `Array.Sort(ordered, 0, count, new PlaceOrder(...))` (ModelsByFile.cs:58).  For 16 or fewer items the sort is an insertion sort, and two items are only swapped when one is greater, so equal items keep their order whether or not the tie-break exists.  The test sorts two models with one place (ModelsByFileTests.cs:54-63).  The name promises the set's order on equal places.  With more than 16 models the sort partitions and reorders equal items, so the proposal would kill the survivor.
- survivors to kill: `src/SqlSource/Generation/Models/ModelsByFile.cs:186:20`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-models`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-39/generator-generation-models/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Models.' 'Generation/Models/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-39/generator-generation-models/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Models.' 'Generation/Models/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-39/generator-generation-models/before/reports/mutation-report.json artifacts/test-cleanup/phase-39/generator-generation-models/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-39/generator-generation-models/before/reports/mutation-report.json ModelsByFileTests.Arrange_ModelsOfOneQueryAndRole_KeepTheSetsOrderWhenTheirPlacesAreEqual
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Generation-2-06: `src/SqlSource/Generation/Models/ModelsByFile.cs:186:20`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-models`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-39/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-39/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-39/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-39/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-39/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-39/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
