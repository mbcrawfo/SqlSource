# Test cleanup phase 57: tests/SqlSource.Tool.Tests/Describing/SqlServer, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Describing/SqlServer`
- Findings: 3 (3 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 24
- Guard: mutation (tool-describing-sqlserver)

## Files

- `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/SelectStatementTests.cs`
- `tests/SqlSource.Tool.Tests/Describing/SqlServer/Statements/DeclaredTypeTextTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Describing.SqlServer-2-01

- test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/SelectStatementTests.cs` | `SelectStatementTests.Read_TopDistinct_SkipsThem` | rows: all
- action: strengthen
- evidence: measured (survivors only under this test: src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:264:13 Statement mutation, src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:264:13 PostIncrementExpression to PostDecrementExpression mutation, src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:273:13 Statement mutation, src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:276:61 Arithmetic mutation, src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:276:72 String mutation); read (SelectStatementTests.cs:74-80 asserts only the item's Name and HasTop.  With `TopEnd` leaving `10`, `PERCENT` or `WITH TIES` unskipped (SelectBody.cs:262-279), the item reads as `10 a` or `PERCENT WITH TIES a`, which SelectItem.cs:51-53 still names `a` from its last token, so the test passes while the item's expression becomes the literal `10`, which ExpressionProofs would prove not null); blind: no
- cover: none
- proposal: Assert in each row that the item's expression is the one token `a`, for example `Text(tokens, item.Expression).ShouldBe("a")` with the class's `Text` helper, so that a TOP count, PERCENT or WITH TIES left in the select list fails the test.
- verdict: confirmed
- verifier's reason: `kills` on tool-describing-sqlserver for `SelectStatementTests.Read_TopDistinct_SkipsThem` lists as SURVIVED SelectBody.cs:264:13 (Statement and PostDecrement), 273:13 Statement, 276:61 Arithmetic and 276:72 String, each reached by no other method.  Traced by hand: with 264's `at++` gone, `SELECT TOP 10 a FROM t` leaves `10 a` as the item, and SelectItem.cs:51-53 still names it `a` because the token before it, a Number, ends an expression.  With 273 or 276 broken, `PERCENT WITH TIES a` and `WITH TIES a` are named `a` in the same way, since `TIES` is a word outside `OperandWords` (SelectItem.cs:16-23, 74).  The test asserts only Name and HasTop (SelectStatementTests.cs:76-79).  The proposed `Text(tokens, item.Expression).ShouldBe("a")` uses the class's `Text` helper (SelectStatementTests.cs:403).  It would fail on all five mutants, and also on the 271:39 and 276:43 String mutants that survive under four methods.
- survivors to kill: `src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:264:13`, `src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:273:13`, `src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:276:61`, `src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:276:72`

### F-SqlSource.Tool.Tests.Describing.SqlServer-2-02

- test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Proofs/SelectStatementTests.cs` | `SelectStatementTests.Read_Groupings_AreRead` | rows: `"GROUP BY GROUPING SETS ((a), ())"` (line 117)
- action: strengthen
- evidence: measured (survivors src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:450:99 String mutation, under `ColumnProofsTests.Apply_PersistedNotNull_StaysNullable` and this test alone, and src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:450:30 String mutation, under 11 methods); read (the only GROUPING SETS row, line 117, also holds `()`, which SelectBody.cs:451 reads as Sets on its own, so the `GROUPING SETS` words of line 450 are never what makes the row Sets; ColumnProofsTests.cs:248 has the same `()`.  A query of `GROUPING SETS ((a), (b))` misread as Plain would let an operand be asked in the select's context, which a grouping set can turn `NULL`); blind: no (lines 445-456 are blind for other mutants, but both survivors were tested)
- cover: none
- proposal: Add the row `[InlineData("GROUP BY GROUPING SETS ((a), (b))", nameof(SelectGrouping.Sets))]`, whose only sign of a grouping set is the two words.
- verdict: confirmed
- verifier's reason: `kills` for `SelectStatementTests.Read_Groupings_AreRead` lists SelectBody.cs:450:30 and 450:99 String mutations as SURVIVED.  In GroupingOf (SelectBody.cs:444-456), the row `GROUPING SETS ((a), ())` (SelectStatementTests.cs:117) still becomes Sets under either mutant, because the loop reaches the `( )` pair that line 451 matches.  ColumnProofsTests.cs:248, the only other GROUPING SETS text in the folder, also ends in `()`.  The proposed row `GROUPING SETS ((a), (b))` has no `(` directly followed by `)` and no ROLLUP or CUBE, so either mutant reads it as Plain, and the row would fail on both.
- survivors to kill: `src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:450:30`, `src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:450:99`

### F-SqlSource.Tool.Tests.Describing.SqlServer-2-04

- test: `tests/SqlSource.Tool.Tests/Describing/SqlServer/Statements/DeclaredTypeTextTests.cs` | `DeclaredTypeTextTests.Read_AnythingElse_IsRefusedWithWhatWasFound` | rows: the `TypeNameFound.Parts` rows, lines 177-188
- action: strengthen
- evidence: measured (survivor src/SqlSource.Tool/Describing/SqlServer/Statements/DeclaredTypeText.cs:252:16 Conditional (false) mutation -> `(false?TypeNameFound.Parts :Found(text, position))`, under this test and `Read_Semicolon_SaysItIsNotWrittenAsATypeName`); read (`ReadEnd` gives Parts for a dot only after a modifier, since `ReadName` (DeclaredTypeText.cs:62-84) catches every dot before one.  No row has a dot after the parentheses, so the mutant's Shape in place of Parts for a text such as `nvarchar(50).x` passes, though the test's name promises the reason found); blind: no
- cover: none
- proposal: Add the row `[InlineData("nvarchar(50).x", TypeNameFound.Parts)]`.
- verdict: confirmed
- verifier's reason: `kills` for `DeclaredTypeTextTests.Read_AnythingElse_IsRefusedWithWhatWasFound` lists DeclaredTypeText.cs:252:16 Conditional (false) as SURVIVED.  ReadName (DeclaredTypeText.cs:62-84) returns Parts for every dot before or between the parts, via SkipToDot, and ReadCollection (line 218-220) handles a dot inside the parentheses.  So ReadEnd (line 244-253) sees a dot only after a closed modifier.  No row of DeclaredTypeTextTests.cs:177-188 has one.  Under the mutant, `nvarchar(50).x` reaches `Found`, which gives Shape for `.` through `TypeNameFound.At`'s default arm (DescribeFailures.cs), instead of Parts.  The proposed row would therefore fail on the mutant.
- survivors to kill: `src/SqlSource.Tool/Describing/SqlServer/Statements/DeclaredTypeText.cs:252:16`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-describing-sqlserver`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-57/tool-describing-sqlserver/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.SqlServer.' 'Describing/SqlServer/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-57/tool-describing-sqlserver/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.SqlServer.' 'Describing/SqlServer/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-57/tool-describing-sqlserver/before/reports/mutation-report.json artifacts/test-cleanup/phase-57/tool-describing-sqlserver/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-57/tool-describing-sqlserver/before/reports/mutation-report.json SelectStatementTests.Read_TopDistinct_SkipsThem
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-57/tool-describing-sqlserver/before/reports/mutation-report.json SelectStatementTests.Read_Groupings_AreRead
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-57/tool-describing-sqlserver/before/reports/mutation-report.json DeclaredTypeTextTests.Read_AnythingElse_IsRefusedWithWhatWasFound
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tool.Tests.Describing.SqlServer-2-01: `src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:264:13`, `src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:273:13`, `src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:276:61`, `src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:276:72`
- F-SqlSource.Tool.Tests.Describing.SqlServer-2-02: `src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:450:30`, `src/SqlSource.Tool/Describing/SqlServer/Proofs/SelectBody.cs:450:99`
- F-SqlSource.Tool.Tests.Describing.SqlServer-2-04: `src/SqlSource.Tool/Describing/SqlServer/Statements/DeclaredTypeText.cs:252:16`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-describing-sqlserver`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-57/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-57/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-57/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-57/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-57/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-57/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
