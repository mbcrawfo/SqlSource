# Test cleanup phase 21: tests/SqlSource.Tests/Generation/Emitting, folds

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Emitting`
- Findings: 1 (1 fold)
- Kind: one kind, fold
- Changed lines: about 45
- Guard: mutation (generator-generation-emitting)

## Files

- `tests/SqlSource.Tests/Generation/Emitting/TypeEmitterTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation.Emitting-1-10

- test: `tests/SqlSource.Tests/Generation/Emitting/TypeEmitterTests.cs` | `TypeEmitterTests.Emit_MethodsQuery_WritesNoConstant`; `TypeEmitterTests.Emit_MethodsTokenQuery_WritesNoTokenMethod`; `TypeEmitterTests.Emit_NestedTypeOfOnlyMethodsQueries_WritesNoNestedClass` | rows: all
- action: fold
- evidence: measured (each 1 row, kills 5, alone 0, timeouts under it 0; the covers are one another: `Emit_MethodsQuery_WritesNoConstant` for the other two, `Emit_MethodsTokenQuery_WritesNoTokenMethod` for it) and read (TypeEmitterTests.cs:526-587: the three expect the same text, the type's empty part; the two on the type differ only in a constant query or a token query, and `SelectMembers` skips a query whose output has a method at TypeEmitter.cs:144, before `AppendMember` asks whether it has a token at :208; the nested one holds both kinds of query; no `AGENTS.md`, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names them or `TypeEmitterTests`, and the comment at :524 states the rule); blind: no
- cover: `tests/SqlSource.Tests/Generation/Emitting/TypeEmitterTests.cs` | `TypeEmitterTests.Emit_NestedTypeOfOnlyMethodsQueries_WritesNoNestedClass`
- proposal: Make one theory over the placement, `Placement(direct, 1)` with `direct` true and false, of a file of a constant query and a token query under `OutputKind.Methods`, that expects the type's empty part.
- verdict: confirmed
- verifier's reason: The three tests (TypeEmitterTests.cs:524-587) expect the same text, the type's empty part, with no diagnostics, under `OutputKind.Methods`.  `EmitOnType` is `Placement(direct: true, 1)` (:719-724), and the nested test's `Emit` gives each file `SettingsLevel.None`, which is `Placement(false, 1)`.  `kills` gives each the same 5 kills (TypeEmitter.cs:45:13 twice, 144:17, 146:17, 309:86), with no timeout or survivor.  `SelectMembers` skips any query whose output has a method (:144-147) before `AppendMember` asks about tokens (:208).  The proposed theory keeps both kinds of query and both placements, and a regression that writes either kind on either placement still fails it.  The comment at :524 states the rule, not a record, and the doc search finds none of the names or `TypeEmitterTests`.  The fold merges facts and drops no row, so reading settles it.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-emitting`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-21/generator-generation-emitting/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Emitting.' 'Generation/Emitting/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-21/generator-generation-emitting/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Emitting.' 'Generation/Emitting/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-21/generator-generation-emitting/before/reports/mutation-report.json artifacts/test-cleanup/phase-21/generator-generation-emitting/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-21/generator-generation-emitting/before/reports/mutation-report.json TypeEmitterTests.Emit_MethodsQuery_WritesNoConstant
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-21/generator-generation-emitting/before/reports/mutation-report.json TypeEmitterTests.Emit_MethodsTokenQuery_WritesNoTokenMethod
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-21/generator-generation-emitting/before/reports/mutation-report.json TypeEmitterTests.Emit_NestedTypeOfOnlyMethodsQueries_WritesNoNestedClass
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-emitting`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-21/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-21/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-21/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-21/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-21/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-21/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
