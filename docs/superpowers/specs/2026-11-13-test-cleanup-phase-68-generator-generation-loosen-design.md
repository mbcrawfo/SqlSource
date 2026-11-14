# Test cleanup phase 68: tests/SqlSource.Tests/Generation, loosening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation`
- Findings: 1 (1 loosen)
- Kind: one kind, loosen
- Changed lines: about 10
- Guard: mutation (generator-generation)

## Files

- `tests/SqlSource.Tests/Generation/AttributeSourceTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation-1-06

- test: `tests/SqlSource.Tests/Generation/AttributeSourceTests.cs` | `AttributeSourceTests.Text_GenerateAttribute_HasTheInterfaceProperties` | rows: all
- action: loosen
- evidence: read (AttributeSourceTests.cs:113-121 compares the attribute's properties with `ShouldBe` on a list, which holds them in their order of declaration in `AttributeSource.Text`; a user sets them as named arguments, whose order is not behaviour, so moving one declaration in the text fails the test while the attribute is the same); blind: no
- cover: none
- proposal: Compare the three properties with `ignoreOrder: true`, keeping the exact set so that a property added or removed still fails.
- verdict: confirmed
- verifier's reason: tests/SqlSource.Tests/Generation/AttributeSourceTests.cs:113-121 compares the properties from `GetMembers()`, in their order of declaration, with an ordered `ShouldBe`.  A user sets the three properties as named arguments, so their order of declaration is not behaviour.  `ignoreOrder: true` keeps the exact set.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-68/generator-generation/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.&FullyQualifiedName!~SqlSource.Tests.Generation.Chains.&FullyQualifiedName!~SqlSource.Tests.Generation.Converters.&FullyQualifiedName!~SqlSource.Tests.Generation.Emitting.&FullyQualifiedName!~SqlSource.Tests.Generation.Exceptions.&FullyQualifiedName!~SqlSource.Tests.Generation.Methods.&FullyQualifiedName!~SqlSource.Tests.Generation.Models.&FullyQualifiedName!~SqlSource.Tests.Generation.Paths.&FullyQualifiedName!~SqlSource.Tests.Generation.Placement.&FullyQualifiedName!~SqlSource.Tests.Generation.Repositories.&FullyQualifiedName!~SqlSource.Tests.Generation.Sidecars.&FullyQualifiedName!~SqlSource.Tests.Generation.Types.' 'Generation/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-68/generator-generation/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.&FullyQualifiedName!~SqlSource.Tests.Generation.Chains.&FullyQualifiedName!~SqlSource.Tests.Generation.Converters.&FullyQualifiedName!~SqlSource.Tests.Generation.Emitting.&FullyQualifiedName!~SqlSource.Tests.Generation.Exceptions.&FullyQualifiedName!~SqlSource.Tests.Generation.Methods.&FullyQualifiedName!~SqlSource.Tests.Generation.Models.&FullyQualifiedName!~SqlSource.Tests.Generation.Paths.&FullyQualifiedName!~SqlSource.Tests.Generation.Placement.&FullyQualifiedName!~SqlSource.Tests.Generation.Repositories.&FullyQualifiedName!~SqlSource.Tests.Generation.Sidecars.&FullyQualifiedName!~SqlSource.Tests.Generation.Types.' 'Generation/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-68/generator-generation/before/reports/mutation-report.json artifacts/test-cleanup/phase-68/generator-generation/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-68/generator-generation/before/reports/mutation-report.json AttributeSourceTests.Text_GenerateAttribute_HasTheInterfaceProperties
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-68/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-68/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-68/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-68/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-68/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-68/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
