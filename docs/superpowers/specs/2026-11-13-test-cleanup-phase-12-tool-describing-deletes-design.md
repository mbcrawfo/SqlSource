# Test cleanup phase 12: tests/SqlSource.Tool.Tests/Describing, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Describing`
- Findings: 1 (1 delete)
- Kind: one kind, delete
- Changed lines: about 18
- Guard: mutation (tool-describing)

## Files

- `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsFaultTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Describing-1-03

- test: `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsFaultTests.cs` | `DatabaseRunsFaultTests.Run_DescriptionWithoutTheValue_IsDescribedAsBefore` | rows: all
- action: delete
- evidence: measured (1 row, 0.004 s; kills 57, alone 0, timeouts under it 0; cover `DatabaseRunsFailureTests.Run_QueryThatFailsWithAPasswordThatIsTheDatabasesName_PrintsTheQueryLineAsItIs`).  Read: it gives `GetUser` a description with one column `id` and a server of `ServerWith()`, and asserts both queries `Described` and an empty error (DatabaseRunsFaultTests.cs:340-356).  `Run_DescriptionWithAPasswordTheDescriberGaveAsAColumnsName_IsDescribed` (DatabaseRunsFaultTests.cs:311-328) runs the same entry check over a described column, one whose name is a password the describer gave, and asserts `Described`, an empty error and the entry's column; `DatabaseRunsTests.Run_Describer_DescribesEachQueryInOneSessionForItsDatabase` asserts both queries described with an empty error under the default server.  No comment is above the test; `grep -rn` of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ for the test's name and `DatabaseRunsFaultTests` finds nothing; blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsFaultTests.cs` | `DatabaseRunsFaultTests.Run_DescriptionWithAPasswordTheDescriberGaveAsAColumnsName_IsDescribed`; `tests/SqlSource.Tool.Tests/Describing/DatabaseRunsTests.cs` | `DatabaseRunsTests.Run_Describer_DescribesEachQueryInOneSessionForItsDatabase`
- proposal: Delete the test: a description without the value is described in both covers, one of them with a column that the check reads.
- verdict: confirmed
- verifier's reason: The test (DatabaseRunsFaultTests.cs:340-356) sets `ServerWith()` and one column `id`, then asserts both queries `Described` and an empty error.  `DatabaseRunsTests.Run_Describer_DescribesEachQueryInOneSessionForItsDatabase` (DatabaseRunsTests.cs:39-63) runs the same scene with the default connection, which holds `Secrets.Word`, and a description without it, and asserts the same two states and empty error, and more.  `Run_DescriptionWithAPasswordTheDescriberGaveAsAColumnsName_IsDescribed` (DatabaseRunsFaultTests.cs:311-328) runs the entry check over a column name that does not match, and asserts `Described`, an empty error and the column.  The only other difference, a server with a null driver version, is held by `DatabaseRunsFailureTests.Run_DriverWithoutAVersion_IsNamedAlone`.  `kills` gives 57 kills, 0 alone and 0 timeouts; each kill makes the run throw or fail a query, which the test sees.  There is no comment above the test.  The search of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred finds the name nowhere, and finds the class only in docs/tech-debt/TD-0089 about `Run_DescriberThatThrows_IsAFaultWithoutTheConnection`, another test.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-describing`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-12/tool-describing/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.&FullyQualifiedName!~SqlSource.Tool.Tests.Describing.Capture.&FullyQualifiedName!~SqlSource.Tool.Tests.Describing.Describers.&FullyQualifiedName!~SqlSource.Tool.Tests.Describing.Postgres.&FullyQualifiedName!~SqlSource.Tool.Tests.Describing.SqlServer.' 'Describing/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-12/tool-describing/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.&FullyQualifiedName!~SqlSource.Tool.Tests.Describing.Capture.&FullyQualifiedName!~SqlSource.Tool.Tests.Describing.Describers.&FullyQualifiedName!~SqlSource.Tool.Tests.Describing.Postgres.&FullyQualifiedName!~SqlSource.Tool.Tests.Describing.SqlServer.' 'Describing/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-12/tool-describing/before/reports/mutation-report.json artifacts/test-cleanup/phase-12/tool-describing/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-12/tool-describing/before/reports/mutation-report.json DatabaseRunsFaultTests.Run_DescriptionWithoutTheValue_IsDescribedAsBefore
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-describing`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-12/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-12/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-12/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-12/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-12/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-12/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md`: 20

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
