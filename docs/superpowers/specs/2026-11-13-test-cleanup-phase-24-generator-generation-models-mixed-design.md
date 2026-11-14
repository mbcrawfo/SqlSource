# Test cleanup phase 24: tests/SqlSource.Tests/Generation/Models, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Models`
- Findings: 2 (1 delete, 1 fold)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 10
- Guard: mutation (generator-generation-models)

## Files

- `tests/SqlSource.Tests/Generation/Models/PropertyNamesTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation-2-07

- test: `tests/SqlSource.Tests/Generation/Models/PropertyNamesTests.cs` | `PropertyNamesTests.FromTable_PublicSchema_NeedsNothingOfTheSchema` | rows: all
- action: delete
- evidence: measured (kills 19, alone 0, timeouts under it 0; cover: `PropertyNamesTests.FromTable_TableOfAnEngine_DropsTheSchemaOfThatEngineAlone`); read (its comment, PropertyNamesTests.cs:111, says the public schema is not checked, but `public` PascalCases to `Public`, an identifier, so FromTable, PropertyNames.cs:92-98, gives `T` whether or not the schema is checked; what remains is a table under `public` on PostgreSQL, which row `("public", "orders", "PostgreSql", "Orders")` at line 80 asserts; no AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names the test or PropertyNamesTests); blind: no
- cover: `tests/SqlSource.Tests/Generation/Models/PropertyNamesTests.cs` | `PropertyNamesTests.FromTable_TableOfAnEngine_DropsTheSchemaOfThatEngineAlone`
- proposal: Delete the test and its comment; the default schema of either engine is an identifier, so the behaviour it names cannot be observed.
- verdict: confirmed
- verifier's reason: FromTable (PropertyNames.cs:73-99) returns the table's name when the schema is the engine's default (:92-95).  `public` PascalCases to the identifier `Public`, so checking the schema first could never change the result.  The behaviour that the comment at PropertyNamesTests.cs:111 names cannot be observed.  What remains is row `("public", "orders", "PostgreSql", "Orders")` at :80 of the engine theory.  `kills` shows 19 kills, 0 alone and 0 timeouts.  The test came in with FromTable itself (ddc2c819), and is not the record of a regression.  No AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names the test or the class.

### F-SqlSource.Tests.Generation-2-08

- test: `tests/SqlSource.Tests/Generation/Models/PropertyNamesTests.cs` | `PropertyNamesTests.FromTable_Table_IsTheSchemaUnlessItIsPublicThenTheTable` | rows: `("public", "users", "Users")` at line 66, `("billing", "invoices", "BillingInvoices")` at line 68
- action: fold
- evidence: measured (6 rows, kills 25, alone 0, rows needed 3; cover: `PropertyNamesTests.FromTable_TableOfAnEngine_DropsTheSchemaOfThatEngineAlone`, `PropertyNamesTests.PascalCase_Name_IsPascalCase`); read (the theory runs on PostgreSQL; row line 68 is word for word row `("billing", "invoices", "PostgreSql", "BillingInvoices")` at line 82 of the engine theory, and row line 66 is the case of row `("public", "orders", "PostgreSql", "Orders")` at line 80 and of its own row `("public", "order_lines", "OrderLines")` at line 67; row line 69 `("Billing_Admin", "audit_log", ...)` keeps a schema put before the table; no comment above the test, and no record document names it); blind: no
- cover: `tests/SqlSource.Tests/Generation/Models/PropertyNamesTests.cs` | `PropertyNamesTests.FromTable_TableOfAnEngine_DropsTheSchemaOfThatEngineAlone` rows at lines 80 and 82; `tests/SqlSource.Tests/Generation/Models/PropertyNamesTests.cs` | `PropertyNamesTests.FromTable_Table_IsTheSchemaUnlessItIsPublicThenTheTable` rows at lines 67 and 69
- proposal: Drop the rows at lines 66 and 68.
- verdict: confirmed
- verifier's reason: Row :68 `("billing", "invoices", "BillingInvoices")` runs on PostgreSql (:73), as engine row :82 does with the same text.  Row :66 `("public", "users", "Users")` is the case of row :80 and of its own row :67.  The rows left, :67 and :69-71, still show both halves of the name and the case of `public`.  There are 0 timeouts, no comment above the theory and no record document.  A row of a theory cannot be measured by a filter, so this is confirmed by reading, and the models phase's own guard proves it.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-models`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-24/generator-generation-models/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Models.' 'Generation/Models/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-24/generator-generation-models/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Models.' 'Generation/Models/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-24/generator-generation-models/before/reports/mutation-report.json artifacts/test-cleanup/phase-24/generator-generation-models/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-24/generator-generation-models/before/reports/mutation-report.json PropertyNamesTests.FromTable_PublicSchema_NeedsNothingOfTheSchema
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-24/generator-generation-models/before/reports/mutation-report.json PropertyNamesTests.FromTable_Table_IsTheSchemaUnlessItIsPublicThenTheTable
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-models`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-24/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-24/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-24/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-24/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-24/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-24/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
