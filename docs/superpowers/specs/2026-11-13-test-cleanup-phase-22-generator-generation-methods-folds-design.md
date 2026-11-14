# Test cleanup phase 22: tests/SqlSource.Tests/Generation/Methods, folds

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Methods`
- Findings: 1 (1 fold)
- Kind: one kind, fold
- Changed lines: about 4
- Guard: mutation (generator-generation-methods)

## Files

- `tests/SqlSource.Tests/Generation/Methods/MethodArgumentsTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation-2-03

- test: `tests/SqlSource.Tests/Generation/Methods/MethodArgumentsTests.cs` | `MethodArgumentsTests.AppendDeclaration_TokenNamedAwait_IsVerbatim` | rows: `(nameof(MemberLocation.NestedClass), "")` at line 236
- action: fold
- evidence: measured (3 rows, kills 51, alone 0, rows needed 1, timeouts under it 3); read (`AppendDeclaration` passes `host.IsExtension` and nothing else of the host, MethodArguments.cs:213; the Class row, line 235, already proves that a host that is not the extension class writes no `this `, and that `IsExtension` is false for NestedClass is `MethodHostTests.IsExtension_EachLocation_IsTheExtensionClassAlone` row `(nameof(MemberLocation.NestedClass), false)` at MethodHostTests.cs:14; no comment above the test or its group, and no AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names the test or MethodArgumentsTests but for the allocation test; the timeouts are on MethodArguments.cs:321 and 324, which the two kept rows still run); blind: no
- cover: `tests/SqlSource.Tests/Generation/Methods/MethodHostTests.cs` | `MethodHostTests.IsExtension_EachLocation_IsTheExtensionClassAlone`; `tests/SqlSource.Tests/Generation/Methods/MethodArgumentsTests.cs` | `MethodArgumentsTests.AppendDeclaration_TokenNamedAwait_IsVerbatim` row `(nameof(MemberLocation.Class), "")`
- proposal: Drop the NestedClass row; the ExtensionClass and Class rows keep the declaration's two forms.
- verdict: confirmed
- verifier's reason: `AppendDeclaration` passes only `host.IsExtension` on (MethodArguments.cs:212-213).  The Class row (MethodArgumentsTests.cs:235) already asserts the declaration without `this `.  `MethodHostTests.IsExtension_EachLocation_IsTheExtensionClassAlone` row `(nameof(MemberLocation.NestedClass), false)` (MethodHostTests.cs:14) asserts that NestedClass is no extension.  The 3 timeouts are on MethodArguments.cs:321 and :324, in the enumerator that every row runs alike.  No comment stands above the theory (:233).  Of the documents, docs/architecture/generator.md:665 and tests/AGENTS.md:44 name MethodArgumentsTests only for the allocation test.  A row of a theory cannot be measured by a filter, so this is confirmed by reading, and the methods phase's own guard proves it.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-methods`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-22/generator-generation-methods/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Methods.' 'Generation/Methods/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-22/generator-generation-methods/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Methods.' 'Generation/Methods/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-22/generator-generation-methods/before/reports/mutation-report.json artifacts/test-cleanup/phase-22/generator-generation-methods/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-22/generator-generation-methods/before/reports/mutation-report.json MethodArgumentsTests.AppendDeclaration_TokenNamedAwait_IsVerbatim
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-methods`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-22/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-22/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-22/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-22/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-22/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-22/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/architecture/generator.md`: 665
- `tests/AGENTS.md`: 44

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
