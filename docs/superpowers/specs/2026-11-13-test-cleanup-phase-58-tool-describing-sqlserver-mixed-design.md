# Test cleanup phase 58: tests/SqlSource.Tool.Tests/Describing/SqlServer, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Describing/SqlServer`
- Findings: 5 (3 delete, 2 strengthen)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 49
- Guard: mutation (tool-describing-sqlserver), with covers and run time for 1 finding

## Files

- `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ExpressionProofsTests.cs`
- `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerErrorsTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Describing.SqlServer-1-03

- test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerErrorsTests.cs` | `SqlServerErrorsTests.Columns_TableStep_IsTableMatch` | rows: all
- action: delete
- evidence: measured (kills 3, alone 0, timeouts under it 0; cover: `SqlServerErrorsTests.Columns_OtherType_Is225WithEveryLineInOrder`) and read: the test asserts only that `SqlServerErrors.Columns` reports the step it was given (SqlServerErrorsTests.cs:527-535).  `DescribeAsync_TableFails_Is225AtTableMatch` sends `table`'s error rows through the same call, `SqlServerErrors.Columns(request, errors, step)` at SqlServerSession.Builders.cs:34, with the step `TableMatch`, and asserts `SQLSRC225`, `DescribeStep.TableMatch` and the line (SqlServerSessionTableTests.cs:212-226).  `Columns_OtherType_Is225WithEveryLineInOrder` asserts the same pass-through at `DescribeColumns`.  No comment above the test; it is named only in a historical plan, docs/superpowers/plans/2026-10-21-query-generation-phase-6-2-columns-and-table-match.md:114, and in none of the AGENTS.md files, readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/`; blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSessionTableTests.cs` | `SqlServerSessionTableTests.DescribeAsync_TableFails_Is225AtTableMatch`; `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerErrorsTests.cs` | `SqlServerErrorsTests.Columns_OtherType_Is225WithEveryLineInOrder`
- proposal: Delete it.  The session test already proves that `table`'s error rows are reported at the table match, and the theory already proves that the step is passed through.
- verdict: confirmed
- verifier's reason: The test asserts only that `SqlServerErrors.Columns` returns the step it is given (SqlServerErrorsTests.cs:527-535; the source passes `step` through at SqlServerErrors.cs:233-257).  `SqlServerSessionTableTests.DescribeAsync_TableFails_Is225AtTableMatch` (SqlServerSessionTableTests.cs:207-227) sends `table`'s error rows (208, as this test's) through `RowsOf` -> `SqlServerErrors.Columns(request, errors, step)` (SqlServerSession.Builders.cs:31-35) at the table match, and asserts `SQLSRC225`, `DescribeStep.TableMatch` and the line, so it proves the same pass-through on the real path and more.  `kills` gives this test 3 kills (SqlServerErrors.cs:229:21, 230:13, 247:13), each also killed by `DescribeAsync_TableFails_Is225AtTableMatch`, and its 2 survivors (248:17) are under that test too (evidence:365-366); no timeout.  No comment above the test; the name appears only in the historical plan docs/superpowers/plans/2026-10-21-query-generation-phase-6-2-columns-and-table-match.md:114, and the class only in historical plans and specs, none an AGENTS.md, readme, architecture page, tech-debt or deferred item.

### F-SqlSource.Tool.Tests.Describing.SqlServer-2-05

- test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ExpressionProofsTests.cs` | `ExpressionProofsTests.Of_ProvedFromTheTextAlone_AsksNothing` | rows: all
- action: delete
- evidence: measured (kills 154, alone 0, timeouts under it 4; cover `ColumnProofsTests.Read_NothingToAsk_OperandsSqlIsNull`); read (it reads `SELECT COUNT(*) AS c FROM t AS t` and asserts no operand and `Holds(null)` true, ExpressionProofsTests.cs:457-465.  The row `("COUNT(*)", "", true, true, "")` of `Of_Form_IsProvedAsExpected`, line 17, reads the same item and asserts it proved with no operand asked; `ColumnProofsTests.Read_NothingToAsk_OperandsSqlIsNull`, ColumnProofsTests.cs:17-25, proves the same COUNT(*) asks nothing and is proved under `Apply(null)`, the null answers, and `ProofTests.Holds_YesWithoutAnswers_IsTrue` that a text proof holds without answers.  No comment above it, and no AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names it or its class); blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ExpressionProofsTests.cs` | `ExpressionProofsTests.Of_Form_IsProvedAsExpected` (row line 17); `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ColumnProofsTests.cs` | `ColumnProofsTests.Read_NothingToAsk_OperandsSqlIsNull`
- proposal: Delete the test; the COUNT(*) row of the theory and the column-level test already prove that a text proof asks nothing and holds without answers.
- verdict: confirmed
- verifier's reason: The test (ExpressionProofsTests.cs:456-465) reads `SELECT COUNT(*) AS c FROM t AS t` and asserts no operands and `Holds(null)` true.
  - Row line 17 of the theory runs the same item through `Prove` (ExpressionProofsTests.cs:467-480) and asserts operands `""` and a proof with `Answers()` of zero operands.  For a proof with no operands, `Holds(null)` and `Holds([])` give every index the same `default` through Proof.cs:30, so the row's assertion is the test's.
  - `ColumnProofsTests.Read_NothingToAsk_OperandsSqlIsNull` (ColumnProofsTests.cs:17-25) shows COUNT(*) is proved under `Apply(null)`, which calls `proof.Holds(answers)` (ColumnProofs.cs:171).
  - The test's 4 timeouts (TsqlTokens.cs:78:17 twice, 95:21, 98:22) are also under both covers.  Its 154 kills are the lexer, the reader and the COUNT form, which these inputs reach and its assertions would notice.
  - No comment stands above it, and the class comment (lines 10-12) names no record.  grep over the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred finds neither the test nor `ExpressionProofsTests`.

### F-SqlSource.Tool.Tests.Describing.SqlServer-2-06

- test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ExpressionProofsTests.cs` | `ExpressionProofsTests.Of_FormOfOperands_AsksEachOperand` | rows: all
- action: delete
- evidence: measured (kills 196, alone 0, timeouts under it 4; cover `ExpressionProofsTests.Operands_TwoItems_OneListInOrder`, `ColumnProofsTests.Apply_OperandNotNull_ColumnProved`); read (it asserts, for `COALESCE(t.a, t.b)`, the operands `t.a|t.b`, `Holds(null)` false and `Holds(Answers(false, true))` true, ExpressionProofsTests.cs:444-454.  The row `("COALESCE(t.a, t.b)", "", true, false, "t.a|t.b")` of `Of_Form_IsProvedAsExpected`, line 149, asserts the same operands; `Operands_TwoItems_OneListInOrder`, line 441, that one of COALESCE's two operands answered not null proves it; and `ColumnProofsTests.Apply_OperandNotNull_ColumnProved`, ColumnProofsTests.cs:27-37, the same COALESCE of two columns asking both, proved by `Answers(false, true)` and not by `Apply(null)`.  No comment above it, and no document names it or its class); blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ExpressionProofsTests.cs` | `ExpressionProofsTests.Of_Form_IsProvedAsExpected` (row line 149); `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ExpressionProofsTests.cs` | `ExpressionProofsTests.Operands_TwoItems_OneListInOrder`; `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ColumnProofsTests.cs` | `ColumnProofsTests.Apply_OperandNotNull_ColumnProved`
- proposal: Delete the test; each of its three assertions is made by a named cover.
- verdict: confirmed
- verifier's reason: The test (ExpressionProofsTests.cs:444-454) asserts three things for `COALESCE(t.a, t.b)`, and a cover makes each one:
  - The operands `t.a|t.b`: row line 149 asserts the same.
  - `Holds(null)` false: row line 149 asserts that the proof fails with every operand answered nullable but of no CLR type.  Proofs have no negation (Proof.cs:12-14, 46-75), and `default` facts are lower in both fields, so `Holds(null)` is false as well.  `ColumnProofsTests.Apply_OperandNotNull_ColumnProved` (ColumnProofsTests.cs:36) also asserts `Apply(null)` unproved.
  - `Holds(Answers(false, true))` true: `Operands_TwoItems_OneListInOrder` (ExpressionProofsTests.cs:433, 441) asserts that a two-argument COALESCE is proved by its second operand alone, and `Apply_OperandNotNull_ColumnProved` (ColumnProofsTests.cs:30-34) asserts the same for COALESCE of two columns.
  Its 4 timeouts are the same TsqlTokens ones that are under `Operands_TwoItems_OneListInOrder` and `Apply_OperandNotNull_ColumnProved`, and its kills are reader and COALESCE mutants its assertions would notice.  No comment stands above it, and no document names it or its class.

### F-SqlSource.Tool.Tests.Describing.SqlServer-1-02

- test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerErrorsTests.cs` | `SqlServerErrorsTests.Failed_ErrorAtDeclared_IsAtResolveType` | rows: all
- action: strengthen
- evidence: measured (covers 6 mutants and kills none) and read: the test hands `DescribeStep.ResolveType` to `SqlServerErrors.Failed` and asserts the same step back (SqlServerErrorsTests.cs:362-370), and `Failed` only passes its argument through (SqlServerErrors.cs:368-375).  The decision the name promises, that a `declared` call that fails on its connection is reported at resolve type, is `SqlServerErrors.Failed(request, error, DescribeStep.ResolveType)` at SqlServerSession.cs:384, which this test never runs.  No other test reaches it: the session's parameter tests answer `declared` with rows or error rows only, and `ParameterTests` of tests/DatabaseTests/SqlServer asserts `ResolveType` only for error rows (ParameterTests.cs:291, :489).  Removing the `ResolveType` argument at SqlServerSession.cs:384 would leave this test passing; blind: yes (SqlServerSession.cs 381-386 do not compile)
- cover: none
- proposal: Make it assert what its name says: run a describe of a declared parameter through `SqlServerScript` whose `declared` answers `Lost`, and assert `SQLSRC225` at `DescribeStep.ResolveType` with the fixed line and no `parameters` call.  It then belongs in `SqlServerSessionParameterTests`.  `Failed_ConnectionLostAtProbe_IsSqlsrc225WithTheFixedLine` keeps proving that `Failed` passes its step through.
- verdict: confirmed
- verifier's reason: The test passes `DescribeStep.ResolveType` to `SqlServerErrors.Failed` and asserts that step back (SqlServerErrorsTests.cs:362-370); `Failed` copies its `step` argument into the failure (src/SqlSource.Tool/Describing/SqlServer/SqlServerErrors.cs:368-375), so the test proves no decision.  The decision its name promises is the literal at src/SqlSource.Tool/Describing/SqlServer/SqlServerSession.cs:384, on lines the evidence marks blind (381-386).  `kills` shows 0 kills and only the survivors at SqlServerErrors.cs:371:53 (the message), nothing about the step.  No session test answers `declared` with an `Error`: every `SqlServerDeclaredAnswer` of SqlServerSessionParameterTests.cs (:128, :220, :459, `DeclaredAs` :483), SqlServerSessionColumnTests.cs (:123, :210) and SqlServerEventsSessionTests.cs (:147, :223) carries rows or error rows with a null error; the only one with `Lost` is an event test (SqlServerEventsTests.cs:259) that runs no session.  tests/DatabaseTests/SqlServer/Describer/ParameterTests.cs:291 and :489 assert `ResolveType` for error rows 2715 and 103, which go through `SqlServerErrors.Declared`, not `Failed`.  The finding reads the test rightly and its proposal names the missing assertion.
- survivors to kill: none named by place; the missing assertion is the proposal's

### F-SqlSource.Tool.Tests.Describing.SqlServer-2-03

- test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/ExpressionProofsTests.cs` | `ExpressionProofsTests.Of_Form_IsProvedAsExpected` | rows: the CONVERT style rows, lines 184-189
- action: strengthen
- evidence: measured (survivor src/SqlSource.Tool/Describing/SqlServer/Proofs/ExpressionProofs.Calls.cs:248:34 Logical mutation -> `parts.Count == 3 || parts[2].Count == 1`, under this test alone); read (the rows' comment, line 170, promises CONVERT's style "an integer literal".  The refused styles, `@style`, `NULL`, `-1` and `(23)`, each start with a token that is no integer, so `IsInteger` of the first token refuses them on its own and the check that the style is one token, ExpressionProofs.Calls.cs:248, is never what decides.  A style such as `23 + @p` starts with an integer, is NULL when `@p` is, and under the mutant is proved); blind: no (line 248 is listed as blind, but this mutant was tested and survived)
- cover: none
- proposal: Add the row `[InlineData("CONVERT(varchar(10), t.d, 23 + @p)", "", false, false, "")]`, a style that starts with an integer literal but is not one.
- verdict: confirmed
- verifier's reason: `kills` for `ExpressionProofsTests.Of_Form_IsProvedAsExpected` lists ExpressionProofs.Calls.cs:248:34 `parts.Count == 3 || parts[2].Count == 1` as SURVIVED.  `IsInteger` (TsqlToken.cs:56) needs a Number token of digits only, and the lexer never puts a sign into a number (TsqlTokens.cs NumberEnd).  So the first tokens of the refused styles (ExpressionProofsTests.cs:186-189), `@style`, `NULL`, `-` and `(`, each fail IsInteger on their own, and the one-token check never decides.  For `23 + @p`, the original refuses because parts[2].Count is 3, while the mutant accepts because Count == 3 and `23` is an integer, so the proposed row, expecting `false, false, ""` as the `@style` row does, would fail on it.
- survivors to kill: `src/SqlSource.Tool/Describing/SqlServer/Proofs/ExpressionProofs.Calls.cs:248:34`
- a cover: `ExpressionProofsTests.Of_Form_IsProvedAsExpected` is the cover of F-SqlSource.Tool.Tests.Describing.SqlServer-2-05 (phase 58), F-SqlSource.Tool.Tests.Describing.SqlServer-2-06 (phase 58).  The change keeps every assertion and row that makes it so.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-describing-sqlserver`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-58/tool-describing-sqlserver/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.SqlServer.' 'Describing/SqlServer/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-58/tool-describing-sqlserver/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.SqlServer.' 'Describing/SqlServer/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-58/tool-describing-sqlserver/before/reports/mutation-report.json artifacts/test-cleanup/phase-58/tool-describing-sqlserver/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-58/tool-describing-sqlserver/before/reports/mutation-report.json SqlServerErrorsTests.Failed_ErrorAtDeclared_IsAtResolveType
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-58/tool-describing-sqlserver/before/reports/mutation-report.json SqlServerErrorsTests.Columns_TableStep_IsTableMatch
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-58/tool-describing-sqlserver/before/reports/mutation-report.json ExpressionProofsTests.Of_Form_IsProvedAsExpected
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-58/tool-describing-sqlserver/before/reports/mutation-report.json ExpressionProofsTests.Of_ProvedFromTheTextAlone_AsksNothing
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-58/tool-describing-sqlserver/before/reports/mutation-report.json ExpressionProofsTests.Of_FormOfOperands_AsksEachOperand
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tool.Tests.Describing.SqlServer-1-02: no survivor is named by place; the test fails when the assertion it adds is removed, checked once by hand
- F-SqlSource.Tool.Tests.Describing.SqlServer-2-03: `src/SqlSource.Tool/Describing/SqlServer/Proofs/ExpressionProofs.Calls.cs:248:34`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-describing-sqlserver`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-58/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-58/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-58/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-58/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-58/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-58/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tool.Tests.Describing.SqlServer-1-02.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tool.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
