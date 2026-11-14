# Test cleanup phase 44: tests/SqlSource.Tests/Generation/Types, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Types`
- Findings: 1 (1 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 8
- Guard: mutation (generator-generation-types)

## Files

- `tests/SqlSource.Tests/Generation/Types/MappedTypes/ShortNameIndexTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation.Types-1-01

- test: `tests/SqlSource.Tests/Generation/Types/MappedTypes/ShortNameIndexTests.cs` | `ShortNameIndexTests.MetadataNamesOf_ReferenceUnchanged_IndexesOnce` | rows: all
- action: strengthen
- evidence: measured (kills 17, alone 0, timeouts under it 0; cover `ShortNameIndexTests.MetadataNamesOf_Arity_GivesTheTypesOfThatArityAlone`; survivor `src/SqlSource/Generation/Types/MappedTypes/ShortNameIndex.cs:80:9 Block removal mutation`, covered by 14 methods, this one among them) and read: the survivor empties the block `{ return index; }` of `if (Indexes.TryGetValue(reference, out var index))` (ShortNameIndex.cs:79-82), so the second compilation walks the reference and builds a new index; `Indexes.GetValue(reference, _ => built)` (ShortNameIndex.cs:96) then hands back the instance already in the table, so `ShortNameIndex.IndexOf(library).ShouldBeSameAs(index)` (ShortNameIndexTests.cs:68) still passes.  The test cannot tell an index read again from one built again, which is what its name and its comment (ShortNameIndexTests.cs:49-50) promise; blind: no
- cover: none
- proposal: Make the build observable to the test, for instance an internal count of `ReferenceIndex.Build` calls beside the existing `IndexOf` hook "for tests", and assert that the second compilation's ask builds none; keep the `ShouldBeSameAs` check.
- verdict: confirmed
- verifier's reason: Read tests/SqlSource.Tests/Generation/Types/MappedTypes/ShortNameIndexTests.cs:49-69 and src/SqlSource/Generation/Types/MappedTypes/ShortNameIndex.cs:71-97.  With the block at ShortNameIndex.cs:80 emptied, the second `MetadataNamesOf` walks the reference again in `ReferenceIndex.Build` (line 95), and `Indexes.GetValue(reference, _ => built)` (line 96) hands back the instance already in the table, so `IndexOf(library).ShouldBeSameAs(index)` (test line 68) and both `ShouldBe(["Lib.UserId"])` still pass.  `kills` in generator-generation-types lists `SURVIVED src/SqlSource/Generation/Types/MappedTypes/ShortNameIndex.cs:80:9 Block removal mutation` under this test; its 17 kills (alias test at line 74, `root is null` at 90-91, the walk at 135-178) are each explained by the first ask returning no or a wrong index.  The test cannot tell an index read again from one rebuilt, which is what its name and comment (lines 49-50) promise; the strengthen reads it rightly.
- survivors to kill: `src/SqlSource/Generation/Types/MappedTypes/ShortNameIndex.cs:80:9`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-types`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-44/generator-generation-types/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Types.' 'Generation/Types/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-44/generator-generation-types/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Types.' 'Generation/Types/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-44/generator-generation-types/before/reports/mutation-report.json artifacts/test-cleanup/phase-44/generator-generation-types/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-44/generator-generation-types/before/reports/mutation-report.json ShortNameIndexTests.MetadataNamesOf_ReferenceUnchanged_IndexesOnce
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Generation.Types-1-01: `src/SqlSource/Generation/Types/MappedTypes/ShortNameIndex.cs:80:9`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-types`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-44/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-44/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-44/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-44/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-44/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-44/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
