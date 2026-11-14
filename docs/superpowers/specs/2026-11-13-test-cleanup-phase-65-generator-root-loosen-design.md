# Test cleanup phase 65: tests/SqlSource.Tests (root files), loosening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests (root files)`
- Findings: 1 (1 loosen)
- Kind: one kind, loosen
- Changed lines: about 10
- Guard: mutation (generator-root), with covers and run time for 1 finding

## Files

- `tests/SqlSource.Tests/SqlServerNamesTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests-1-05

- test: `tests/SqlSource.Tests/SqlServerNamesTests.cs` | `SqlServerNamesTests.ClrSystemTypes_OfSys_AreTheThreeInOrder` | rows: all
- action: loosen
- evidence: read (SqlServerNamesTests.cs:35-37 holds `ClrSystemTypes` to `["hierarchyid", "geography", "geometry"]` in that order.  Its only readers are `SqlServerNames.IsClrSystemType`, a `Contains` (SqlServerNames.cs:39), and `SqlServerParameterBinder`, which adds each name to a table of rows (SqlServerParameterBinder.cs:364-367); the tool names it only in a doc comment.  No order of the list reaches any output); blind: yes
- cover: none
- proposal: Assert the three names with `ignoreOrder: true` and rename the test to drop `InOrder`; reordering the list, say alphabetically, breaks it today while nothing a user sees changes.
- verdict: confirmed
- verifier's reason: SqlServerNamesTests.cs:35-37 holds `ClrSystemTypes` to an exact order.  It has three readers.  SqlServerNames.cs:39 is a `Contains`.  SqlServerParameterBinder.cs:338-367 adds each name to a `Dictionary<string, Row>` with OrdinalIgnoreCase, where order means nothing.  src/SqlSource.Tool/Describing/SqlServer/Types/SqlServerTypes.cs:69 is a doc comment.  docs/architecture/sqlserver-describer.md:249 names the list but says nothing of its order.  So reordering it changes nothing a user sees, and only this test fails.  The property is a static initialiser, so the finding is blind and is judged by reading.  Holding the three names without order keeps the test's value.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-root`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-65/generator-root/before --tests 'FullyQualifiedName~SqlSource.Tests.Generator.|FullyQualifiedName~SqlSource.Tests.DatabaseNameTests.|FullyQualifiedName~SqlSource.Tests.EquatableArrayTests.|FullyQualifiedName~SqlSource.Tests.PackageVersionTests.|FullyQualifiedName~SqlSource.Tests.SortedSearchTests.|FullyQualifiedName~SqlSource.Tests.SqlIdentifierTests.|FullyQualifiedName~SqlSource.Tests.SqlServerNamesTests.|FullyQualifiedName~SqlSource.Tests.SystemSchemasTests.|FullyQualifiedName~SqlSource.Tests.TextLinesTests.|FullyQualifiedName~SqlSource.Tests.UnexpectedEnumValueExceptionTests.|FullyQualifiedName~SqlSource.Tests.VersionsTests.' '*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-65/generator-root/after --tests 'FullyQualifiedName~SqlSource.Tests.Generator.|FullyQualifiedName~SqlSource.Tests.DatabaseNameTests.|FullyQualifiedName~SqlSource.Tests.EquatableArrayTests.|FullyQualifiedName~SqlSource.Tests.PackageVersionTests.|FullyQualifiedName~SqlSource.Tests.SortedSearchTests.|FullyQualifiedName~SqlSource.Tests.SqlIdentifierTests.|FullyQualifiedName~SqlSource.Tests.SqlServerNamesTests.|FullyQualifiedName~SqlSource.Tests.SystemSchemasTests.|FullyQualifiedName~SqlSource.Tests.TextLinesTests.|FullyQualifiedName~SqlSource.Tests.UnexpectedEnumValueExceptionTests.|FullyQualifiedName~SqlSource.Tests.VersionsTests.' '*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-65/generator-root/before/reports/mutation-report.json artifacts/test-cleanup/phase-65/generator-root/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-65/generator-root/before/reports/mutation-report.json SqlServerNamesTests.ClrSystemTypes_OfSys_AreTheThreeInOrder
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-root`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-65/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-65/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-65/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-65/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-65/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-65/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tests-1-05.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
