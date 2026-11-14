# Test cleanup phase 55: tests/SqlSource.Tool.Tests/Describing/Postgres, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tool.Tests/Describing/Postgres`
- Findings: 4 (4 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 32
- Guard: mutation (tool-describing-postgres)

## Files

- `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionParameterTests.cs`
- `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/Expressions/PlanExpressionTests.cs`
- `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanPartitionsTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tool.Tests.Describing.Postgres-2-01

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionParameterTests.cs` | `PostgresSessionParameterTests.DescribeAsync_FewerTypesThanParameters_ThrowsAndFreesTheStatement` | rows: all
- action: strengthen
- evidence: measured (1 row; kills 32, alone 0, timeouts under it 5; survivors src/SqlSource.Tool/Describing/Postgres/PostgresSession.Builders.cs:38:16 Conditional (false) mutation, which removes the throw, and src/SqlSource.Tool/Describing/Postgres/PostgresSession.Builders.cs:40:17 String mutation, under this test alone) and read: the test asserts only `Should.ThrowAsync<InvalidOperationException>` (PostgresSessionParameterTests.cs:187).  With the guard of PostgresSession.Builders.cs:38-42 removed, the session goes on to ask "types", which the script does not hold, and the replay throws `CaptureMissException`, which derives from `InvalidOperationException` (src/SqlSource.Tool/Describing/Capture/CaptureMissException.cs:20), so the test still passes; blind: no
- cover: none
- proposal: Assert that the exception is the describer's own and not the replay's miss: `ShouldBeOfType<InvalidOperationException>()` on the thrown exception, or that its message holds the counts ("1 types for the 2 parameters").
- verdict: confirmed
- verifier's reason: The test asserts only `Should.ThrowAsync<InvalidOperationException>` and `exchange.Unused.ShouldBeEmpty()` (tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionParameterTests.cs:187-194).  `kills` in tool-describing-postgres lists `SURVIVED src/SqlSource.Tool/Describing/Postgres/PostgresSession.Builders.cs:38:16 Conditional (false) mutation` and `SURVIVED ...PostgresSession.Builders.cs:40:17 String mutation` under it.  With the guard of PostgresSession.Builders.cs:38-42 gone, `DescribePreparedAsync` goes on to ask "columns" (src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs:178-183), not "types" as the finding says: `PostgresScript.PreparesWithNoColumns` scripts no "columns" answer when fewer types are resolved than asked (tests/SqlSource.Tool.Tests/PostgresScript.cs:84-86).  Either way the replay throws `CaptureMissException.NoAnswer` (src/SqlSource.Tool/Describing/Capture/ReplayingExchange.cs:55), which derives from `InvalidOperationException` (src/SqlSource.Tool/Describing/Capture/CaptureMissException.cs:20), and the `finally` still frees the statement (PostgresSession.cs:150-159), so the test passes on the mutant.  The proposal, to assert the exact type or the counts in the message, would kill both survivors; the cleanup should name "columns" as the call the replay misses.
- survivors to kill: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.Builders.cs:38:16`, `src/SqlSource.Tool/Describing/Postgres/PostgresSession.Builders.cs:40:17`
- condition: The verifier adds: name "columns" as the call the replay misses.

### F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-04

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanPartitionsTests.cs` | `PlanPartitionsTests.MembersOf_AppendOfAnotherShape_IsEmpty` | rows: `"member-without-a-number"` (line 43), `"member-with-a-word-for-a-number"` (line 44)
- action: strengthen
- evidence: measured (survivors src/SqlSource.Tool/Describing/Postgres/Walk/PlanPartitions.cs:119:16 Logical mutation -> `cut > 0 || cut < alias.Length - 1`; PlanPartitions.cs:119:27 Equality mutation -> `cut <= alias.Length - 1`; PlanPartitions.cs:119:33 Arithmetic mutation -> `alias.Length + 1`; PlanPartitions.cs:119:53 Linq method mutation (All() to Any()) -> `alias[(cut + 1)..].Any`) and read (`PrefixOf`, PlanPartitions.cs:116-120, takes a member's alias as a prefix and a number only when everything after its last `_` is a digit.  The row "without a number" uses `e`, which has no `_`, and the row "with a word for a number" uses `e_x`, which has no digit, so neither row can tell `All` from `Any`, or `<` from `<=`.  `e_` (no number after the `_`) and `e_1x` (a word that starts with a digit) are the cases the rows' names promise, and each kills those mutants); blind: no
- cover: none
- proposal: Give the two rows the aliases `e_` and `e_1x` in place of `e` and `e_x`, or add them as rows beside those, so that a suffix with no number, or a suffix that is not all digits, fails the shape.
- verdict: confirmed
- verifier's reason: The rows are at PlanPartitionsTests.cs:43, with alias `e`, and :44, with `e_x`. I ran `kills` on `PlanPartitionsTests.MembersOf_AppendOfAnotherShape_IsEmpty`. It lists 119:16 Logical (`cut > 0 || cut < alias.Length - 1`), 119:27 Equality, 119:33 Arithmetic and 119:53 All-to-Any as SURVIVED under the test. Worked through `PrefixOf` (PlanPartitions.cs:116-120):
  - `e` has cut -1, and every mutant still returns null, since `e` is not a digit.
  - `e_x` fails `All` and `Any` alike.
  - `e_` has cut 1 and length 2. The original is false at `1 < 1`. The `||`, `<=` and `+1` mutants pass and take an empty suffix's `All` as true, so the prefix is `e` and matches `e_1`, which kills them.
  - `e_1x` passes `Any` and fails `All`, which kills the Linq mutant.
  The rows' names promise exactly these cases.
- survivors to kill: `src/SqlSource.Tool/Describing/Postgres/Walk/PlanPartitions.cs:119:16`

### F-SqlSource.Tool.Tests.Describing.Postgres.Walk.Expressions-1-01

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/Expressions/PlanExpressionTests.cs` | `PlanExpressionTests.MayNameColumn_Entry_IsWhetherItNamesTheColumnOrHoldsAParameter` | rows: all
- action: strengthen
- evidence: measured (kills 66, alone 2, timeouts under it 8; survivor src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/PlanExpression.cs:197:21 Logical mutation -> `token.Kind == Kind.Other || text.Contains('$', ...)`, covered by this test alone).  Read: the name promises that only a parameter, a symbol token holding `$`, counts beside the column's name (PlanExpression.cs:194-198, and docs/architecture/postgres-nullability.md:114 says "holds a `$` parameter").  No row has a symbol token that is not a parameter: the rows that expect false, `'id'::text` and `row_number() OVER w1` (PlanExpressionTests.cs:784-785), tokenize to strings, casts, words and parentheses only, so a reader that counts any operator as a parameter passes every row.  blind: no
- cover: none
- proposal: Add a row with an operator and no parameter that names another column, such as `[InlineData("(u.name || 'x'::text)", "id", false)]`, which the mutant answers true.
- verdict: confirmed
- verifier's reason: `kills` on the report of tool-describing-postgres gives 6 rows, 66 kills, 2 alone, 8 timeouts, and lists `SURVIVED src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/PlanExpression.cs:197:21 Logical mutation -> token.Kind == Kind.Other || text.Contains('$', ...)`; the evidence (line 204) has no other test of the scope under it.  PlanTokens.Read (src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/PlanTokens.cs:61-102) gives `'id'::text` a String, a Cast and a Word, and `row_number() OVER w1` Words, an Open and a Close: no Other token, so the two rows that expect false (tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/Expressions/PlanExpressionTests.cs:784-785) pass under the mutant, and the true rows are true either way.  The proposed row `(u.name || 'x'::text)` with "id" tokenizes `||` as Other with no `$` (PlanTokens.cs:93-101), so the code answers false (PlanExpression.cs:195-204) and the mutant true.  The name and the doc comment (PlanExpression.cs:180-182) promise only a parameter counts.
- survivors to kill: `src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/PlanExpression.cs:197:21`

### F-SqlSource.Tool.Tests.Describing.Postgres.Walk.Expressions-1-02

- test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/Expressions/PlanExpressionTests.cs` | `PlanExpressionTests.NullableOf_CoalesceOverBothSidesOfAFullJoin_IsNotNull` | rows: all
- action: strengthen
- evidence: measured (kills 145, alone 4, timeouts under it 11; survivor src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/PlanExpression.Nullability.cs:109:21 OrAssignmentExpression to ExclusiveOrAssignmentExpression mutation -> `inner ^= !side.OuterSide`, covered by this test alone).  Read: the comment above the theory (PlanExpressionTests.cs:716-719) promises the form is not NULL over a reference to each side, and FullJoinSides (PlanExpression.Nullability.cs:89-113) ORs each argument's side.  Every row (PlanExpressionTests.cs:721-731) has at most one reference to the inner side `r`, so a reader that toggles the inner flag instead of setting it passes; with two inner references and one outer it would answer unknown where the rule says not NULL.  blind: no (the line is in the blind range 91-115 of the evidence, but this mutant compiled and survived)
- cover: none
- proposal: Add a row with one outer and two inner references, such as `[InlineData("COALESCE(a.id, r.account_id, r.account_id)", false, BuiltInTypes.Int4)]`, which the mutant answers null.
- verdict: confirmed
- verifier's reason: `kills` gives 11 rows, 145 kills, 4 alone, 11 timeouts, and lists `SURVIVED .../PlanExpression.Nullability.cs:109:21 OrAssignmentExpression to ExclusiveOrAssignmentExpression mutation -> inner ^= !side.OuterSide`, which the evidence (line 180) says no other test of the scope fails on.  Every row (PlanExpressionTests.cs:721-731) has at most one argument that SideOf (PlanExpressionTests.cs:704-711) maps to the inner side; a parenthesised or cast reference is not parsed as a reference, so `^=` and `|=` agree on every row.  With `COALESCE(a.id, r.account_id, r.account_id)` the code's FullJoinSides (PlanExpression.Nullability.cs:90-115) returns three Int4 types, so NullableOf is false (line 60) and ProvedTypeOf Int4 (PlanExpression.Proofs.cs:139-144); under the mutant inner toggles back to false, FullJoinSides is null, and AnyArgumentIsNotNull is false since AtAFullJoin proves no reference (PlanExpressionTests.cs:713-714), so both answers are null.  The comment above the theory (lines 716-719) promises the rule for a reference to each side, which this breaks.
- survivors to kill: `src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/PlanExpression.Nullability.cs:109:21`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-describing-postgres`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-55/tool-describing-postgres/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.Postgres.' 'Describing/Postgres/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-55/tool-describing-postgres/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.Postgres.' 'Describing/Postgres/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-55/tool-describing-postgres/before/reports/mutation-report.json artifacts/test-cleanup/phase-55/tool-describing-postgres/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-55/tool-describing-postgres/before/reports/mutation-report.json PostgresSessionParameterTests.DescribeAsync_FewerTypesThanParameters_ThrowsAndFreesTheStatement
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-55/tool-describing-postgres/before/reports/mutation-report.json PlanPartitionsTests.MembersOf_AppendOfAnotherShape_IsEmpty
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-55/tool-describing-postgres/before/reports/mutation-report.json PlanExpressionTests.MayNameColumn_Entry_IsWhetherItNamesTheColumnOrHoldsAParameter
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-55/tool-describing-postgres/before/reports/mutation-report.json PlanExpressionTests.NullableOf_CoalesceOverBothSidesOfAFullJoin_IsNotNull
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tool.Tests.Describing.Postgres-2-01: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.Builders.cs:38:16`, `src/SqlSource.Tool/Describing/Postgres/PostgresSession.Builders.cs:40:17`
- F-SqlSource.Tool.Tests.Describing.Postgres.Walk-1-04: `src/SqlSource.Tool/Describing/Postgres/Walk/PlanPartitions.cs:119:16`
- F-SqlSource.Tool.Tests.Describing.Postgres.Walk.Expressions-1-01: `src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/PlanExpression.cs:197:21`
- F-SqlSource.Tool.Tests.Describing.Postgres.Walk.Expressions-1-02: `src/SqlSource.Tool/Describing/Postgres/Walk/Expressions/PlanExpression.Nullability.cs:109:21`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-describing-postgres`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-55/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-55/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-55/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-55/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-55/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-55/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- F-SqlSource.Tool.Tests.Describing.Postgres.Walk.Expressions-1-05 (keep, `PlanExpressionTests.NullableOf_CoalesceOverBothSidesWithoutTheFullJoin_IsUnknown`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
