# Test cleanup phase 03: tests/SqlSource.Tests/Generation, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation`
- Findings: 1 (1 delete)
- Kind: one kind, delete
- Changed lines: about 11
- Guard: mutation (generator-generation)

## Files

- `tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation-1-13

- test: `tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs` | `SqlFileReaderTests.Read_Dialect_IsTheDialectTheFileIsParsedWith` | rows: all
- action: delete
- evidence: measured (kills 5, alone 0, timeouts under it 0; cover: `SqlFileReaderTests.Read_ReservedTokenInAKeptComment_IsAnErrorOfTheQueryAndNotOfTheFile`); read (SqlFileReader's part is passing the dialect to the parser, SqlFileReader.cs:40; `Read_FileParseInput_ReadsTheFileWithItsDialect` reads `SELECT 1 # c` with MySQL through the `FileParseInput` overload, which calls the same `Read`, SqlFileReader.cs:20-21, and asserts the MySQL comment stripped, SqlFileReaderTests.cs:242-253, so a dialect that does not reach the parser fails it; this test's Ansi half reports an error, whose conversion to a diagnostic is `Read_FileWithErrors_GivesEachErrorWithItsLineAndColumnAndNoQueries`; no comment above the test, and no AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names it or SqlFileReaderTests); blind: no
- cover: `tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs` | `SqlFileReaderTests.Read_FileParseInput_ReadsTheFileWithItsDialect`; `tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs` | `SqlFileReaderTests.Read_FileWithErrors_GivesEachErrorWithItsLineAndColumnAndNoQueries`
- proposal: Delete it; the `FileParseInput` test proves the dialect reaches the parse through both overloads.
- verdict: confirmed
- verifier's reason: SqlFileReader's only part in the dialect is passing it to `SqlFileParser.Parse`, src/SqlSource/Generation/SqlFileReader.cs:40, and the `FileParseInput` overload calls the same `Read` (20-21).
  - `Read_FileParseInput_ReadsTheFileWithItsDialect` reads `SELECT 1 # c` as MySQL and asserts the comment removed, tests/SqlSource.Tests/Generation/SqlFileReaderTests.cs:241-253.  A dialect that does not reach the parser fails it.
  - `kills` (generator-generation): 5 kills, 0 timeouts.  The two named covers hold all of them: FileWithErrors kills 38:20, 88:13 and 96:13, and FileParseInput kills 38:20 and 142:9 ×2.
  - No comment above the test, and no listed document names it or the class.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-03/generator-generation/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.&FullyQualifiedName!~SqlSource.Tests.Generation.Chains.&FullyQualifiedName!~SqlSource.Tests.Generation.Converters.&FullyQualifiedName!~SqlSource.Tests.Generation.Emitting.&FullyQualifiedName!~SqlSource.Tests.Generation.Exceptions.&FullyQualifiedName!~SqlSource.Tests.Generation.Methods.&FullyQualifiedName!~SqlSource.Tests.Generation.Models.&FullyQualifiedName!~SqlSource.Tests.Generation.Paths.&FullyQualifiedName!~SqlSource.Tests.Generation.Placement.&FullyQualifiedName!~SqlSource.Tests.Generation.Repositories.&FullyQualifiedName!~SqlSource.Tests.Generation.Sidecars.&FullyQualifiedName!~SqlSource.Tests.Generation.Types.' 'Generation/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-03/generator-generation/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.&FullyQualifiedName!~SqlSource.Tests.Generation.Chains.&FullyQualifiedName!~SqlSource.Tests.Generation.Converters.&FullyQualifiedName!~SqlSource.Tests.Generation.Emitting.&FullyQualifiedName!~SqlSource.Tests.Generation.Exceptions.&FullyQualifiedName!~SqlSource.Tests.Generation.Methods.&FullyQualifiedName!~SqlSource.Tests.Generation.Models.&FullyQualifiedName!~SqlSource.Tests.Generation.Paths.&FullyQualifiedName!~SqlSource.Tests.Generation.Placement.&FullyQualifiedName!~SqlSource.Tests.Generation.Repositories.&FullyQualifiedName!~SqlSource.Tests.Generation.Sidecars.&FullyQualifiedName!~SqlSource.Tests.Generation.Types.' 'Generation/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-03/generator-generation/before/reports/mutation-report.json artifacts/test-cleanup/phase-03/generator-generation/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-03/generator-generation/before/reports/mutation-report.json SqlFileReaderTests.Read_Dialect_IsTheDialectTheFileIsParsedWith
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-03/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-03/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-03/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-03/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-03/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-03/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
