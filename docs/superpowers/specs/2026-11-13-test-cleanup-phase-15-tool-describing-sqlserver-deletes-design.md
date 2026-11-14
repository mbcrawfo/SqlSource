# Test cleanup phase 15: tests/SqlSource.Tool.Tests/Describing/SqlServer, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Describing/SqlServer`
- Findings: 5 (5 delete)
- Kind: one kind, delete
- Changed lines: about 77
- Guard: mutation (tool-describing-sqlserver), with covers and run time for 1 finding

## Files

- `tests/SqlSource.Tool.Tests/Describing/SqlServer/Columns/SqlServerColumnsTests.cs`
- `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ProofTests.cs`
- `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerEventsSessionTests.cs`
- `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSessionProofTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Describing.SqlServer-1-01

- test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Columns/SqlServerColumnsTests.cs` | `SqlServerColumnsTests.OriginOf_ExpressionWithNoType_IsAnExpression` | rows: all
- action: delete
- evidence: measured (kills 1, alone 0, timeouts under it 0; cover: `SqlServerColumnsTests.Build_ColumnOfAFunction_SaysWhy`) and read: the row `One` already has `SourceType = null` (SqlServerColumnsTests.cs:74), so `One with { SourceType = null }` (:240) is the same row that `OriginOf_Expression_IsNone` passes (:188), and both assert `(null, "expression")`.  No comment above the test; its name and its class's appear in no AGENTS.md, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/`; blind: yes (SqlServerColumns.cs 108-110, 112-115, 119-121 and 123 do not compile)
- cover: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Columns/SqlServerColumnsTests.cs` | `SqlServerColumnsTests.OriginOf_Expression_IsNone`
- proposal: Delete it: it calls `OriginOf` with the same arguments as `OriginOf_Expression_IsNone` and expects the same result.  An expression whose null type is answered "expression" and not "not a table or view" is already what that test proves.
- verdict: confirmed
- verifier's reason: `One` already sets `SourceType = null` (tests/SqlSource.Tool.Tests/Describing/SqlServer/Columns/SqlServerColumnsTests.cs:74), so `One with { SourceType = null }` (:240) is `One`, the very argument of `OriginOf_Expression_IsNone` (:188), with the same `Database` and the same expected `(null, "expression")`.  `kills` in tool-describing-sqlserver gives each test the one kill `SqlServerColumns.cs:107:13 Equality mutation -> row.SourceServer is null`, explained: the mutant answers `another server` for a row with no server.  No timeout or survivor under it.  `OriginOf` tries the parts before the type (SqlServerColumns.cs:112-125), so "expression before not a table or view" is decided by the row `One`, which the cover passes.  No comment above the test, the class comment (:11-12) names no record; `grep -rn` over every `*.md` finds the test's name nowhere and the class's only in historical plans and specs (docs/superpowers/plans/2026-10-21-...-6-2-...md:63, :78; 2026-11-07-td-0108-describers.md:62; specs/2026-10-20-...-6-2-...md:125), none of them an AGENTS.md, readme, architecture page, tech-debt or deferred item.

### F-SqlSource.Tool.Tests.Describing.SqlServer-1-04

- test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerEventsSessionTests.cs` | `SqlServerEventsSessionTests.DescribeAsync_BareNullableColumnOfOneTable_WritesNoOperands` | rows: all
- action: delete
- evidence: measured (kills 94, alone 0, timeouts under it 5; cover: `SqlServerSessionProofTests.DescribeAsync_BareNullableColumn_AsksTableAndNoOperands`, `SqlServerEventsSessionTests.DescribeAsync_Rows_WritesColumnsOperandsEachColumnAndTheMatchInOrder`) and read: it describes `SELECT id, nickname FROM dbo.users` over the same script as `SqlServerSessionProofTests.DescribeAsync_BareNullableColumn_AsksTableAndNoOperands`: the same `nickname` row built from `NameColumn` (SqlServerEventsSessionTests.cs:102-109 and SqlServerSessionProofTests.cs:25-32) and the same `table` answer, apart from the hidden row's ordinal.  That test asserts the calls `parameters`, `columns`, `table` with no `operands`, the nickname's `(true, server)` and `matched` (SqlServerSessionProofTests.cs:64-68).  The events follow the calls one for one.  The order `columns`, `column`, `table`, `table-match` is asserted by `DescribeAsync_OneTable_WritesTableBeforeTheMatch` (SqlServerEventsSessionTests.cs:90-93), together with the table's duration, which this test does not assert.  The 5 timeouts under it are reached by the proof test too, which runs the same text through the same proofs and shows 5 in the evidence, so the numbers are not the only ground for the delete.  The comment above the test restates the proof test's; the test's name and its class's appear in no AGENTS.md, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/`; blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSessionProofTests.cs` | `SqlServerSessionProofTests.DescribeAsync_BareNullableColumn_AsksTableAndNoOperands`; `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerEventsSessionTests.cs` | `SqlServerEventsSessionTests.DescribeAsync_OneTable_WritesTableBeforeTheMatch`
- proposal: Delete it.  The proof test already proves that a bare nullable column of one table asks no `operands` and is matched, and `DescribeAsync_OneTable_WritesTableBeforeTheMatch` already proves the order of the log's events for one table.
- verdict: confirmed
- verifier's reason: The test (SqlServerEventsSessionTests.cs:96-127) and `SqlServerSessionProofTests.DescribeAsync_BareNullableColumn_AsksTableAndNoOperands` (SqlServerSessionProofTests.cs:52-69) describe `SELECT id, nickname FROM dbo.users` over the same `columns` and `table` answers, with the nickname row built the same way (NameColumn with the same five fields, :102-109 and :25-32).  The proof test asserts the calls `parameters`, `columns`, `table` with no `operands`, `(true, server)` for the nickname and `matched`; the events follow from those calls and from the column loop before `MatchAsync` (SqlServerSession.cs:285-292), and `DescribeAsync_OneTable_WritesTableBeforeTheMatch` (:76-94) asserts `column` before `table` before `table-match`.  `kills`: of the test's 94 kills, all but SqlServerEvents.cs:156:13, 161:21, 173:19 and SqlServerSession.cs:289:13, 361:9 are the proof test's, and all but 161:21 are in the union with `OneTable`; 161:21 (the member name `nullableSource`) is killed by `SqlServerEventsColumnTests.Column_ColumnWithAnOrigin_WritesEveryMemberAndNoReason` and by `DescribeAsync_Rows_WritesColumnsOperandsEachColumnAndTheMatchInOrder` (:71), which the evidence names as cover, so nothing is lost though the finding's cover list leaves it out.  The 5 timeouts (SelectBody.cs:476:17, TsqlTokens.cs:78:17 twice, 95:21, 98:22) are exactly the proof test's and `OneTable`'s, endless loops of the tokenizer that any describe reaches.  The comment above (:96-97) restates the proof test's; no record in any AGENTS.md, readme, architecture page, tech-debt or deferred item (the class is named only in historical plans).

### F-SqlSource.Tool.Tests.Describing.SqlServer-1-05

- test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSessionProofTests.cs` | `SqlServerSessionProofTests.DescribeAsync_NothingToAsk_NoOperandsCall` | rows: all
- action: delete
- evidence: measured (kills 197, alone 0, timeouts under it 5; cover: `SqlServerSessionTableTests.DescribeAsync_NoOrigin_AsksNoTable`) and read: both tests describe `SELECT COUNT(*) AS n FROM dbo.users` over the same script.  `ExpressionRow(1, "n")` (SqlServerScript.cs:91-104) is field for field the `count` row of the other test (SqlServerSessionTableTests.cs:155-166).  This test asserts the calls `parameters`, `columns` and the column `(false, expression)` (SqlServerSessionProofTests.cs:109-110).  `DescribeAsync_NoOrigin_AsksNoTable` asserts the same calls and the whole column, `false` and `expression`, plus the match `no-origin` (SqlServerSessionTableTests.cs:174-180), and its comment states the proof from the text with nothing to ask.  The same input reaches the same code, and the evidence shows the cover with 5 timeouts under it too, so the numbers are not the only ground for the delete.  docs/architecture/sqlserver-describer.md:293 says that the order of the proofs' calls is tested in `SqlServerSessionProofTests`.  That stays true through the class's four other tests, which assert `operands` asked or not.  The test is named only in a historical plan, docs/superpowers/plans/2026-10-23-sql-server-nullability-noise.md:266, and there is no comment above it; blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSessionTableTests.cs` | `SqlServerSessionTableTests.DescribeAsync_NoOrigin_AsksNoTable`
- proposal: Delete it.  `DescribeAsync_NoOrigin_AsksNoTable` runs the same describe and asserts everything this test asserts, and more.
- verdict: confirmed
- verifier's reason: `ExpressionRow(1, "n")` (tests/SqlSource.Tool.Tests/SqlServerScript.cs:91-104) and the `count` row of `DescribeAsync_NoOrigin_AsksNoTable` (SqlServerSessionTableTests.cs:155-166) are `IdColumn` with the same fields set to the same values, and both scripts are `NoParametersThenColumns` of `SELECT COUNT(*) AS n FROM dbo.users`.  This test asserts the calls `[Parameters, Columns]` and `(false, expression)` (SqlServerSessionProofTests.cs:109-110); the cover asserts the same calls, the whole `SidecarColumn` with `false` and `expression`, no table and `no-origin` (:174-180), and its comment (:150) states the proof from the text with nothing to ask.  `kills`: this test's 197 kills are a strict subset of the cover's 199 (the cover adds SqlServerTypes.cs:90:24 twice), and the 5 timeouts under it are the same 5 under the cover.  No comment above the test; docs/architecture/sqlserver-describer.md:293 names the class for the order of the proofs' calls, which its four other tests keep; the test is named only in the historical plan docs/superpowers/plans/2026-10-23-sql-server-nullability-noise.md:266.

### F-SqlSource.Tool.Tests.Describing.SqlServer-2-07

- test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ProofTests.cs` | `ProofTests.Holds_NotClrBeyondTheAnswers_DoesNotHold` | rows: all
- action: delete
- evidence: measured (kills 3, alone 0; cover `ProofTests.Holds_OperandBeyondTheAnswers_DoesNotHold`, which has this test as its own cover, so only one of the two may go); read (an index past the answers falls to `default` in `Proof.Holds`, Proof.cs:29-30, one bounds check for every node, which `Holds_OperandBeyondTheAnswers_DoesNotHold`, ProofTests.cs:21-22, asserts; that the default's NotClr is false is asserted by `Holds_NotClrWithoutAnswers_DoesNotHold`, ProofTests.cs:42, through the same `default`.  No comment above it, and no document names it; the `ProofTests` named in docs/architecture/sqlserver-describer.md and TD-0071 is the database project's class of that name); blind: no (Proof.cs:30 is listed as blind, but the test's three kills are on it)
- cover: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ProofTests.cs` | `ProofTests.Holds_OperandBeyondTheAnswers_DoesNotHold`; `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ProofTests.cs` | `ProofTests.Holds_NotClrWithoutAnswers_DoesNotHold`
- proposal: Delete this test and keep `Holds_OperandBeyondTheAnswers_DoesNotHold`, which with `Holds_NotClrWithoutAnswers_DoesNotHold` proves the same.
- verdict: confirmed
- verifier's reason: `kills` for `Holds_NotClrBeyondTheAnswers_DoesNotHold` and for `Holds_OperandBeyondTheAnswers_DoesNotHold` gives the same three Proof.cs:30 mutants: 30:27 Conditional (true), 30:27 Logical and 30:50 Equality.  Each one indexes past the answers and throws, and neither test has a timeout.
  - The bounds check is in `Holds` and shared by every node (Proof.cs:29-30), so the Operand test (ProofTests.cs:21-22) guards it for NotClr too.
  - That the `default` beyond the answers has NotClr false is asserted by `Holds_NotClrWithoutAnswers_DoesNotHold` (ProofTests.cs:42), through the same `default`.
  - No comment stands above it.  The `ProofTests` that docs/architecture/sqlserver-describer.md:293, TD-0071:28 and tests/DatabaseTests/SqlServer/README.md:3 name is tests/DatabaseTests/SqlServer/Describer/ProofTests.cs, not this class.
  Only one of the pair goes, as the finding says.

### F-describers-01

- test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerEventsSessionTests.cs` | `SqlServerEventsSessionTests.DescribeAsync_ThroughTheLogOfASession_RemovesTheSecretFromTheSqlAndTheMessageOfAnError` | rows: all
- action: delete
- evidence: measured (`SqlServerEventsSessionTests.DescribeAsync_ThroughTheLogOfASession_RemovesTheSecretFromTheSqlAndTheMessageOfAnError`: 1 row, 0.003 s; kills 15, alone 0, timeouts under it 0; cover: `SqlServerEventsSessionTests.DescribeAsync_ParametersThatFail_WritesTheErrorAndNoProbe`, `SqlServerEventsColumnTests.Columns_ErrorOfTheServer_WritesItInThePlaceOfTheRows`) and read.  It is SQL Server's twin of the confirmed F-SqlSource.Tool.Tests.Describing.Postgres-1-07 (`PostgresEventsTests.DescribeAsync_ThroughTheLogOfASession_RemovesTheSecretFromTheMessageOfAnError`), and has its fault.  The test (SqlServerEventsSessionTests.cs:258-281) asserts `***` in `sql` and in the error row's `message` of `mssql.parameters`, and its comment states the generic rule, "the same inside a list of maps".  `SessionRunLog` cleans every string of an event at any depth: `SessionRunLogTests.Write_ValueInEveryPlaceAStringCanStand_IsWrittenAsTheMarkInEach` (SessionRunLogTests.cs:19-48) and `Write_ValueSplitOverATextOfAListAndATextOfAMapAfterIt_IsTheMarkInBoth` (:74-90), a map inside a list.  `DescribeAsync_ThroughTheLogOfASession_HasTheEngineBeforeEachNameAndNothingOfTheSecret` (SqlServerEventsSessionTests.cs:216-256) shows the session's `parameters` event goes through that log with the secret removed, inside `rows`, a list of maps, too.  `RunLogRedactionTests.Run_DescriberThatLogsAParametersEventWithItsConnection_HasTheMarkInEachString` (RunLogRedactionTests.cs:213-247), the case src/SqlSource.Tool/AGENTS.md:42 asks for each event, writes `SqlServerEvents.Parameters` with an error through a run's describer log and asserts `sql` as `SELECT '***'` and the error's `message` as `Incorrect syntax near '***'.`.  `SqlServerEventsTests.Parameters_ErrorOfTheServer_WritesEveryErrorInThePlaceOfTheRows` (SqlServerEventsTests.cs:71-93) asserts the error rows' members, `message` among them.  Records: no comment above the test records a case; grep over the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred finds neither the test's name nor `SqlServerEventsSessionTests`, which only historical plans name.  Every cover is of SQL Server's events or engine-agnostic; blind: no
- cover: `tests/SqlSource.Tool.Tests/Logging/RunLogRedactionTests.cs` | `RunLogRedactionTests.Run_DescriberThatLogsAParametersEventWithItsConnection_HasTheMarkInEachString`; `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerEventsSessionTests.cs` | `SqlServerEventsSessionTests.DescribeAsync_ThroughTheLogOfASession_HasTheEngineBeforeEachNameAndNothingOfTheSecret`; `tests/SqlSource.Tool.Tests/Logging/SessionRunLogTests.cs` | `SessionRunLogTests.Write_ValueInEveryPlaceAStringCanStand_IsWrittenAsTheMarkInEach`; `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerEventsTests.cs` | `SqlServerEventsTests.Parameters_ErrorOfTheServer_WritesEveryErrorInThePlaceOfTheRows`
- proposal: Delete the test, as its PostgreSQL twin is deleted.  The session's parameters event goes through the session's log, and that log and the run's describer log clean the SQL and the error's message.
- verdict: confirmed
- verifier's reason: The test (tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerEventsSessionTests.cs:258-281) sends a refused `parameters` call through `new SessionRunLog(inner, new ConnectionSecret("secret"), "mssql.")` and asserts `***` in `sql` and in the error row's `message`; its comment, "The same inside a list of maps", states the generic rule and records no case.  `kills` in tool-describing-sqlserver: 15 kills, none alone, no timeout; each kill is explicable from its inputs (the error map's keys at SqlServerEvents.cs:215:25-218:25, the `sql` key at :51:17, the session's common path) and each is also killed by `ParametersThatFail_WritesTheErrorAndNoProbe`, `SqlServerEventsTests.Parameters_ErrorOfTheServer_WritesEveryErrorInThePlaceOfTheRows` or the other error-event tests.  The redaction itself is outside the scope's source, so it is judged by reading the covers: `RunLogRedactionTests.Run_DescriberThatLogsAParametersEventWithItsConnection_HasTheMarkInEachString` (tests/SqlSource.Tool.Tests/Logging/RunLogRedactionTests.cs:213-247) writes `SqlServerEvents.Parameters` with an error through a run's describer log and asserts `sql` `SELECT '***'` and the error's `message` `Incorrect syntax near '***'.`, the very two strings this test asserts, and it is the case src/SqlSource.Tool/AGENTS.md:42 requires; `SessionRunLogTests.Write_ValueInEveryPlaceAStringCanStand_IsWrittenAsTheMarkInEach` (SessionRunLogTests.cs:19-48) and `Write_ValueSplitOverATextOfAListAndATextOfAMapAfterIt_IsTheMarkInBoth` (:74-90) clean a string inside lists and maps; `DescribeAsync_ThroughTheLogOfASession_HasTheEngineBeforeEachNameAndNothingOfTheSecret` (SqlServerEventsSessionTests.cs:216-256) shows the SQL Server session's `parameters` event goes through `SessionRunLog` with the secret removed inside `rows`, a list of maps.  All covers are SQL Server's own events or engine-agnostic.  grep of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred finds neither the test's name nor `SqlServerEventsSessionTests` (only historical plans docs/superpowers/plans/2026-10-23-td-0074-engine-test-duplicates.md and 2026-10-28-td-0080-4-scripts-and-log-events.md name the class).  It matches the confirmed F-SqlSource.Tool.Tests.Describing.Postgres-1-07 for the PostgreSQL twin.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-describing-sqlserver`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-15/tool-describing-sqlserver/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.SqlServer.' 'Describing/SqlServer/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-15/tool-describing-sqlserver/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.SqlServer.' 'Describing/SqlServer/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-15/tool-describing-sqlserver/before/reports/mutation-report.json artifacts/test-cleanup/phase-15/tool-describing-sqlserver/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-15/tool-describing-sqlserver/before/reports/mutation-report.json SqlServerColumnsTests.OriginOf_ExpressionWithNoType_IsAnExpression
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-15/tool-describing-sqlserver/before/reports/mutation-report.json SqlServerEventsSessionTests.DescribeAsync_BareNullableColumnOfOneTable_WritesNoOperands
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-15/tool-describing-sqlserver/before/reports/mutation-report.json SqlServerSessionProofTests.DescribeAsync_NothingToAsk_NoOperandsCall
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-15/tool-describing-sqlserver/before/reports/mutation-report.json ProofTests.Holds_NotClrBeyondTheAnswers_DoesNotHold
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-15/tool-describing-sqlserver/before/reports/mutation-report.json SqlServerEventsSessionTests.DescribeAsync_ThroughTheLogOfASession_RemovesTheSecretFromTheSqlAndTheMessageOfAnError
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-describing-sqlserver`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-15/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-15/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-15/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-15/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-15/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-15/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tool.Tests.Describing.SqlServer-1-01.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tool.Tests.Describing.SqlServer-1-01 | delete | `SqlServerColumnsTests.OriginOf_ExpressionWithNoType_IsAnExpression` | `tests/SqlSource.Tool.Tests/Describing/SqlServer/Columns/SqlServerColumnsTests.cs`: `SqlServerColumnsTests.OriginOf_Expression_IsNone` |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tool.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/architecture/sqlserver-describer.md`: 293
- `docs/tech-debt/TD-0071-sql-server-answers-nullable-for-columns-that-cannot-be-null.md`: 28

## Left out

- F-SqlSource.Tool.Tests.Describing.SqlServer-2-08 (keep, `ProofTests.Holds_NotClrOfAnOperandOfAClrType_IsFalse`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
