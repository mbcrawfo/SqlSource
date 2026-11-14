# Test cleanup phase 98: tests/DatabaseTests/Postgres/Describer, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/DatabaseTests/Postgres/Describer`
- Findings: 7 (1 delete, 1 loosen, 5 demote)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 177
- Guard: mutation (tool-describing-postgres), with covers and run time for 7 findings

## Files

- `tests/DatabaseTests/Postgres/Describer/ParameterTests.cs`
- `tests/DatabaseTests/Postgres/Describer/TransactionTests.cs`
- `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresLiveTests.cs`
- `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-DatabaseTests.Postgres.Describer-1-01

- test: `tests/DatabaseTests/Postgres/Describer/ParameterTests.cs` | `ParameterTests.DescribeAsync_OptionalFilterWithTheTypingUseFirstOrADeclaration_IsDescribed` | rows: all
- action: delete
- evidence: read (ParameterTests.cs:170-181 asserts only that a value is described.  Its row at line 172, `(@name IS NULL OR name = @name)` with `varchar` declared, is the same SQL and declaration as `DescribeAsync_OptionalFilterWithADeclaration_IsTypedAsDeclared` at ParameterTests.cs:474-480, which also asserts the declared type.  Its row at line 171, `(name = @name OR @name IS NULL)`, is the row at ParameterTests.cs:427 under another parameter name.  The comment at ParameterTests.cs:422-423 calls that row "the optional filter with the use that fixes the type first", and the test asserts that the parameter is inferred as text.  The test has no comment.  A search of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred found neither the test nor its class.  The name appears only in an old plan, docs/superpowers/plans/2026-10-13-query-generation-phase-3-2-session-and-parameters.md:3661); blind: no
- cover: `tests/DatabaseTests/Postgres/Describer/ParameterTests.cs` | `ParameterTests.DescribeAsync_OptionalFilterWithADeclaration_IsTypedAsDeclared`; `tests/DatabaseTests/Postgres/Describer/ParameterTests.cs` | `ParameterTests.DescribeAsync_ParameterComparedWithATextualColumn_IsText` (row `"SELECT 1 FROM users WHERE (name = @p OR @p IS NULL)"`, line 427)
- proposal: Delete the theory.  Each of its rows is described, and its parameter's type asserted, by the two named tests.
- verdict: confirmed
- verifier's reason: No mutation scope ran the slice (evidence: `## Mutation scopes` None), so this was judged by reading and the named-cover rule.  ParameterTests.cs:170-181 asserts only `Value.ShouldNotBeNull()`.  The row at :172, `(@name IS NULL OR name = @name)` with `("name", "varchar")`, has the same SQL and declaration as `DescribeAsync_OptionalFilterWithADeclaration_IsTypedAsDeclared` (ParameterTests.cs:474-480).  That test describes through `Sessions.ShouldDescribeAsync`, which fails on any failure (Sessions.cs:71-81), and it also asserts `Declared("name", CharacterVarying)`.  The row at :171, `(name = @name OR @name IS NULL)`, matches the row at ParameterTests.cs:427 apart from the parameter's name.  The name changes nothing: `ParameterPlaceholders.Rewrite` turns either into `$1`, and the column `name` has no `@`.  The comment at :422-423 calls that row "the optional filter with the use that fixes the type first", and the test asserts `text`.  The SQLSRC227 limit itself is recorded by `DescribeAsync_FirstUseThatDoesNotFixTheType_IsSqlsrc227AtTheUse` (:158-168), which stays.  The test has no comment.  My own grep found its name only in docs/superpowers/plans/2026-10-13-query-generation-phase-3-2-session-and-parameters.md:3661, an old plan.  It is not in any AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred, and none of those names the class `ParameterTests`.  Both covers are on Postgres in the same class, and I read and confirmed them.

### F-DatabaseTests.Postgres.Describer-2-01

- test: `tests/DatabaseTests/Postgres/Describer/TransactionTests.cs` | `TransactionTests.DescribeAsync_GatedQuery_RunsEveryCallFromPrepareToDeallocateInOneTransaction` | rows: all
- action: loosen
- evidence: read (TransactionTests.cs:49-51 asserts the exact list of calls `["prepare", "columns", "tables", "types", "spellings", "check", "explain", "deallocate"]`, while the rule the name and the comment at :22-23 promise is held by :52-54: one transaction start from the first call to the one before the last, and none after `deallocate`.  `src/SqlSource.Tool/Describing/Postgres/AGENTS.md` says "a call that has nothing to ask is not made" and that a new call after `prepare` goes through `PostgresLive.InDescribeAsync`: a describe that gains, drops or reorders a call that stays in the transaction breaks this test though the behaviour holds, and the loop at :53 already checks whatever calls are made.  The order of the calls is what `PostgresSession.DescribeAsync` holds, and its unit tests through `PostgresScript` hold what is asked; no mutation scope ran this slice); blind: no
- cover: none
- proposal: Replace the exact list with what the rule needs: the first call is `prepare`, the last is `deallocate`, and `explain` is among them (the gated query's reason, whose rollback to the savepoint must not end the transaction); keep the three assertions on the starts.
- verdict: confirmed
- verifier's reason: The finding reads the test rightly.  TransactionTests.cs:49-51 asserts the exact list of calls, and :52-54 hold the rule that the name and the comments at :15-16 and :22-23 promise: one `xact_start` from `prepare` to the call before `deallocate`, and none after it.  The loop at :53 checks every call the hook sees, and `HookedExchange` (tests/SqlSource.Tool.Testing/HookedExchange.cs:33-51) hooks every call that goes through the exchange, so a call that is added or dropped is still held to the rule.  The exact list adds only the order and the set of calls, which `src/SqlSource.Tool/Describing/Postgres/AGENTS.md` gives to `PostgresSession.DescribeAsync` ("a call that has nothing to ask is not made"), and which the unit tests already pin through `PostgresScript`: PostgresEventsTests.cs:34, PostgresEventsRelationTests.cs:220 and :247, PostgresEventsColumnTests.cs:267-314.  I tried to refute it on the grounds that the list is a record: no AGENTS.md, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names the test.  The plan that wrote it, docs/superpowers/plans/2026-10-23-td-0049-describe-in-one-transaction.md:60, wrote the list as `prepare, columns, tables, types, spellings, check, explain, …, deallocate`, with an ellipsis, so the exact list was never the decision.  The proposal keeps `explain` among the calls, which is what keeps the gated query's reason (docs/architecture/postgres-describer.md:131: `explain` rolls back to the savepoint, and that rollback must not end the transaction).  No mutation scope ran the slice (evidence:21).

### F-DatabaseTests.Postgres.Describer-1-04

- test: `tests/DatabaseTests/Postgres/Describer/ParameterTests.cs` | `ParameterTests.DescribeAsync_TwoStatements_IsSqlsrc230AndSendsNothing` | rows: all
- action: demote
- evidence: read (ParameterTests.cs:32-42 asserts SQLSRC230 and a capture of `["open"]` alone.  `PostgresSession.DescribeAsync` refuses the text in `ParameterPlaceholders.Rewrite` before any call (src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs:111-114), so the server plays no part beyond opening.  PostgresSessionTests.cs:57-68 describes the same text over a script that holds `open` alone, where any further call fails the test, and asserts SQLSRC230.  src/SqlSource.Tool/Describing/Postgres/AGENTS.md says these four checks "use no server" and that a change to one "gets a case in tests/SqlSource.Tool.Tests/Describing/Postgres/".  It also says that PostgresScript's tests hold what is asked, and that only "what only a server can show" is tested in tests/DatabaseTests/Postgres/Describer/.  The test has no comment.  The only document that names it is an old plan, docs/superpowers/plans/2026-10-13-query-generation-phase-3-2-session-and-parameters.md:2667); blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs` | `PostgresSessionTests.DescribeAsync_SeveralStatements_IsSqlsrc230AndAsksNothing`
- proposal: Delete the container test.  The existing unit test over PostgresScript proves the same refusal and that nothing beyond `open` is asked.
- verdict: confirmed
- verifier's reason: The slice is unmeasured, so this was judged by reading.  ParameterTests.cs:32-42 asserts SQLSRC230 and a capture of `["open"]`.  `PostgresSession.DescribeAsync` returns SQLSRC230 from `ParameterPlaceholders.Rewrite` before any call (src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs:108-114), so the server plays no part.  `PostgresSessionTests.DescribeAsync_SeveralStatements_IsSqlsrc230AndAsksNothing` (tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs:56-68) runs over a script that holds `open` alone, where any further call fails (class comment :12-14).  Its row at :58, `DELETE FROM users WHERE id = @id; DELETE FROM orders`, is the same shape, and it asserts SQLSRC230 and no value.  src/SqlSource.Tool/Describing/Postgres/AGENTS.md says the four checks "use no server" and a change to one gets a case in tests/SqlSource.Tool.Tests/Describing/Postgres/.  The same file names `PrepareAsync_TextOfTwoStatements_AnswersTheServersErrorAndRunsNothing` (ParameterTests.cs:84-119) as the server-side canary, and that test stays.  This test has no comment.  My own grep found its name only in the old plan docs/superpowers/plans/2026-10-13-query-generation-phase-3-2-session-and-parameters.md:2667.

### F-DatabaseTests.Postgres.Describer-1-05

- test: `tests/DatabaseTests/Postgres/Describer/ParameterTests.cs` | `ParameterTests.DescribeAsync_StatementPrepareDoesNotTake_IsSqlsrc233AndSendsNothing` | rows: all
- action: demote
- evidence: read (ParameterTests.cs:44-63, 5 rows, asserts SQLSRC233 with the kind, and a capture of `["open"]` alone.  The refusal is `StatementShape.RefusedKind`, before any call and with no gate on the server's version (PostgresSession.cs:116-119).  So the comment's "on every server" holds whatever the server is.  StatementShapeTests.cs:57-58 gives the kind for the first words CALL and CREATE.  Its rows at lines 108, 112 and 115 (`SELECT @a::int AS a INTO TEMP t`, `SELECT 1. INTO t`, `(SELECT 1 INTO t)`) give SELECT INTO for the three INTO rows.  PostgresSessionTests.cs:71-85 proves that the session turns a refused kind into SQLSRC233 with the arguments `[query, kind]` over a script of `open` alone.  The AGENTS.md rule is the one quoted in F-04.  The test's comment records why each statement is refused, not a known limit or a regression); blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs` | `PostgresSessionTests.DescribeAsync_CallStatement_IsSqlsrc233AndAsksNothing`; `tests/SqlSource.Tool.Tests/Describing/Postgres/Statements/StatementShapeTests.cs` | `StatementShapeTests.RefusedKind_RefusedFirstWord_IsThatWord`; `tests/SqlSource.Tool.Tests/Describing/Postgres/Statements/StatementShapeTests.cs` | `StatementShapeTests.RefusedKind_SelectInto_IsSelectInto`
- proposal: Delete the container theory.  The unit tests already read each row's kind and prove that the session refuses a refused kind before asking anything.
- verdict: confirmed
- verifier's reason: The slice is unmeasured, so this was judged by reading.  ParameterTests.cs:44-63 asserts SQLSRC233, `[Query, kind]` and `["open"]` for 5 rows.  `StatementShape.RefusedKind` refuses before any call, with no version gate (PostgresSession.cs:116-119).  StatementShapeTests.cs:57-58 covers the first words CALL and CREATE.  StatementShapeTests.cs:108, :112 and :115 hold the three INTO rows' texts (`TEMP t` against `t` changes nothing the reader reads) and assert `SELECT INTO`.  `PostgresSessionTests.DescribeAsync_CallStatement_IsSqlsrc233AndAsksNothing` (PostgresSessionTests.cs:70-85) proves the session maps a refused kind to SQLSRC233 with `["Q", kind]` over a script of `open` alone.  The comment at ParameterTests.cs:44-45 says the tool refuses SELECT INTO on every server, though 15+ prepares it.  My own search found that decision recorded in site/diagnostics.md:2410, which names no test.  The test is not that record, and since the refusal comes before any call, the server's version cannot change the result.  docs/deferred/D-0002-describing-call-and-data-definition.md does not name the test either.  My own grep found the test's name nowhere outside its file.

### F-DatabaseTests.Postgres.Describer-1-06

- test: `tests/DatabaseTests/Postgres/Describer/ParameterTests.cs` | `ParameterTests.DescribeAsync_DeclaredTypeThatIsNoTypeName_IsSqlsrc228AndSendsNothing` | rows: all
- action: demote
- evidence: read (ParameterTests.cs:65-82 describes `SELECT 1 FROM users WHERE id = @id` with the type `int) AS SELECT 1; DROP TABLE users; --` and asserts SQLSRC228, all four arguments and a capture of `["open"]`.  PostgresSessionTests.cs:118-132 describes the same SQL and type over a script of `open` alone.  It asserts SQLSRC228, `Arguments[1]` and `Arguments[3]`, but not `Arguments[0]`, the type's text, or `Arguments[2]`, the query.  `DeclaredTypeText` refuses the type before anything is sent (PostgresSession.cs:126-130).  The AGENTS.md rule is the one quoted in F-04.  The test has no comment.  The only document that names it is an old plan, docs/superpowers/plans/2026-10-13-query-generation-phase-3-2-session-and-parameters.md:2681); blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs` | `PostgresSessionTests.DescribeAsync_DeclaredTypeThatIsNoTypeName_IsSqlsrc228AndAsksNothing`
- proposal: Make the unit test assert the whole argument list, `["int) AS SELECT 1; DROP TABLE users; --", "@id", query, NotATypeName + ": " + Shape]`, then delete the container test.
- verdict: confirmed
- verifier's reason: The slice is unmeasured, so this was judged by reading.  ParameterTests.cs:65-82 asserts SQLSRC228, all four arguments and `["open"]`.  `PostgresSessionTests.DescribeAsync_DeclaredTypeThatIsNoTypeName_IsSqlsrc228AndAsksNothing` (PostgresSessionTests.cs:118-132) uses the same SQL and type over an `open`-only script, but asserts only `Arguments[1]` and `Arguments[3]` (:130-131).  The finding is right that `Arguments[0]` (the type's text) and `Arguments[2]` (the query) are asserted only by the container test.  So the demotion must first strengthen the unit test, as proposed.  The refusal comes from `ParameterPlaceholders.TypeList` before any call (PostgresSession.cs:126-130).  The container test does not check the server afterwards: `users` is never queried, so it is no canary of the injection guarantee.  The server-side canary named by src/SqlSource.Tool/Describing/Postgres/AGENTS.md is `PrepareAsync_TextOfTwoStatements_...`, which stays.  The test has no comment.  My own grep found its name only in the old plan (phase-3-2 plan :2681).
- condition: Make the unit test assert the whole argument list before deleting the container test, as the proposal says.

### F-DatabaseTests.Postgres.Describer-1-07

- test: `tests/DatabaseTests/Postgres/Describer/ParameterTests.cs` | `ParameterTests.DescribeAsync_SqlWithAPlaceholderOfItsOwn_IsSqlsrc234AndSendsNothing` | rows: all
- action: demote
- evidence: read (ParameterTests.cs:569-583 describes `SELECT 1 FROM users WHERE id = @id AND name = $2` and asserts SQLSRC234, the arguments `[query, "$2"]` and a capture of `["open"]`.  PostgresSessionTests.cs:88-102 describes the same SQL over a script of `open` alone and asserts SQLSRC234 with the arguments `["Q", "$2"]`.  `StatementShape.PositionalParameter` refuses it before any call (PostgresSession.cs:121-124), and StatementShapeTests.cs:137-148 holds its readings.  The AGENTS.md rule is the one quoted in F-04.  The test's comment gives why the text is refused, not a known limit or a regression); blind: no
- cover: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs` | `PostgresSessionTests.DescribeAsync_PositionalParameterInTheSql_IsSqlsrc234AndAsksNothing`
- proposal: Delete the container test.  The unit test proves the same refusal, arguments and absence of any call with no server.
- verdict: confirmed
- verifier's reason: The slice is unmeasured, so this was judged by reading.  ParameterTests.cs:569-583 asserts SQLSRC234, `[Query, "$2"]` and `["open"]`.  `PostgresSessionTests.DescribeAsync_PositionalParameterInTheSql_IsSqlsrc234AndAsksNothing` (PostgresSessionTests.cs:87-102) uses the same SQL over an `open`-only script and asserts SQLSRC234 and `["Q", "$2"]`.  `StatementShape.PositionalParameter` refuses it before any call (PostgresSession.cs:121-124), and StatementShapeTests.cs:137-148 has the same text at :138.  The comment at ParameterTests.cs:569-570 says why the text is refused and records no limit or regression.  My own grep found the current name nowhere outside its file.  The old plans name earlier, different tests (`..._PassesTheCheck`, `..._TypesTheListedParametersAlone`), not this one.

### F-DatabaseTests.Postgres.Describer-2-02

- test: `tests/DatabaseTests/Postgres/Describer/TransactionTests.cs` | `TransactionTests.ExplainAsync_WithoutPrepare_Throws` | rows: all
- action: demote
- evidence: read (TransactionTests.cs:232-241 builds a `PostgresConnection` and a `PostgresLive` and calls `ExplainAsync` with no describe begun; `src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs:821-824` throws the `InvalidOperationException` on `connection.InDescribe` before any command is made, and `PostgresConnection` (PostgresConnection.cs:37) connects only when asked, so the server is never reached: the 0.000 s of the evidence.  `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresConnectionTests.cs:30` already builds a `PostgresConnection` on `Host=127.0.0.1;Port=1` without a server, and `PostgresLiveTests.cs` holds no case of this guard.  `src/SqlSource.Tool/Describing/Postgres/AGENTS.md` keeps for `tests/DatabaseTests/Postgres/Describer/` "what only a server can show".  No AGENTS.md, readme, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names the test); blind: no
- cover: to be written: `PostgresLiveTests.ExplainAsync_OutsideADescribe_Throws` in `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresLiveTests.cs`
- proposal: Write the unit test on a `PostgresConnection` of `Host=127.0.0.1;Port=1` that nothing opens, asserting that `ExplainAsync` throws `InvalidOperationException`, so the guard runs without Docker and on every platform of the nightly, and delete the container test.
- verdict: confirmed
- verifier's reason: TransactionTests.cs:232-241 calls `ExplainAsync` on a fresh `PostgresLive`.  PostgresLive.cs:819-824 throws `InvalidOperationException` on `!connection.InDescribe` before any command is made.  `InDescribe` is `_describe is not null` (PostgresConnection.cs:366), and `_describe` is set only by `BeginDescribeAsync` (:389), so the guard never reaches a server, which is why the evidence shows 0.000 s (evidence:138).  The unit test is feasible: `PostgresLive` and `PostgresConnection` are internal and visible to `SqlSource.Tool.Tests` (src/SqlSource.Tool/SqlSource.Tool.csproj:37), and PostgresConnectionTests.cs:30 already builds a `PostgresConnection` on `Host=127.0.0.1;Port=1` without a server.  `PostgresLiveTests.cs` holds no case of this guard.  `src/SqlSource.Tool/Describing/Postgres/AGENTS.md` keeps only "what only a server can show" in `tests/DatabaseTests/Postgres/Describer/`.  The repository's documents do not name the test.  Only the plan docs/superpowers/plans/2026-10-23-td-0049-describe-in-one-transaction.md:64 names it, as a step that specifies the behaviour, and the demoted test keeps that behaviour.  It is not the record of a limit or a regression.  Nothing is lost on the engine, since the cover is the to-be-written unit test.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `tool-describing-postgres`:

```bash
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-98/tool-describing-postgres/before --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.Postgres.' 'Describing/Postgres/**/*.cs'
# the change
tools/mutation-test.sh tool --output artifacts/test-cleanup/phase-98/tool-describing-postgres/after --tests 'FullyQualifiedName~SqlSource.Tool.Tests.Describing.Postgres.' 'Describing/Postgres/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-98/tool-describing-postgres/before/reports/mutation-report.json artifacts/test-cleanup/phase-98/tool-describing-postgres/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-98/tool-describing-postgres/before/reports/mutation-report.json PostgresSessionTests.DescribeAsync_DeclaredTypeThatIsNoTypeName_IsSqlsrc228AndAsksNothing
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `tool-describing-postgres`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-98/coverage-before/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-98/coverage-before/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-98/coverage-before/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
dotnet test --project tests/SqlSource.Tool.Tests --results-directory artifacts/test-cleanup/phase-98/coverage-after/SqlSource.Tool.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-98/coverage-after/SqlSource.Tool.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-98/coverage-after/SqlSource.Tool.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource.Tool'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-DatabaseTests.Postgres.Describer-1-01, F-DatabaseTests.Postgres.Describer-1-04, F-DatabaseTests.Postgres.Describer-1-05, F-DatabaseTests.Postgres.Describer-1-06, F-DatabaseTests.Postgres.Describer-1-07, F-DatabaseTests.Postgres.Describer-2-01, F-DatabaseTests.Postgres.Describer-2-02.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-DatabaseTests.Postgres.Describer-1-01 | delete | `ParameterTests.DescribeAsync_OptionalFilterWithTheTypingUseFirstOrADeclaration_IsDescribed` | `tests/DatabaseTests/Postgres/Describer/ParameterTests.cs`: `ParameterTests.DescribeAsync_OptionalFilterWithADeclaration_IsTypedAsDeclared`; `tests/DatabaseTests/Postgres/Describer/ParameterTests.cs`: `ParameterTests.DescribeAsync_ParameterComparedWithATextualColumn_IsText` (row `"SELECT 1 FROM users WHERE (name = @p OR @p IS NULL)"`, line 427) |
| F-DatabaseTests.Postgres.Describer-1-04 | demote | `ParameterTests.DescribeAsync_TwoStatements_IsSqlsrc230AndSendsNothing` | `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`: `PostgresSessionTests.DescribeAsync_SeveralStatements_IsSqlsrc230AndAsksNothing` |
| F-DatabaseTests.Postgres.Describer-1-05 | demote | `ParameterTests.DescribeAsync_StatementPrepareDoesNotTake_IsSqlsrc233AndSendsNothing` | `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`: `PostgresSessionTests.DescribeAsync_CallStatement_IsSqlsrc233AndAsksNothing`; `tests/SqlSource.Tool.Tests/Describing/Postgres/Statements/StatementShapeTests.cs`: `StatementShapeTests.RefusedKind_RefusedFirstWord_IsThatWord`; `tests/SqlSource.Tool.Tests/Describing/Postgres/Statements/StatementShapeTests.cs`: `StatementShapeTests.RefusedKind_SelectInto_IsSelectInto` |
| F-DatabaseTests.Postgres.Describer-1-06 | demote | `ParameterTests.DescribeAsync_DeclaredTypeThatIsNoTypeName_IsSqlsrc228AndSendsNothing` | `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`: `PostgresSessionTests.DescribeAsync_DeclaredTypeThatIsNoTypeName_IsSqlsrc228AndAsksNothing` |
| F-DatabaseTests.Postgres.Describer-1-07 | demote | `ParameterTests.DescribeAsync_SqlWithAPlaceholderOfItsOwn_IsSqlsrc234AndSendsNothing` | `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`: `PostgresSessionTests.DescribeAsync_PositionalParameterInTheSql_IsSqlsrc234AndAsksNothing` |
| F-DatabaseTests.Postgres.Describer-2-02 | demote | `TransactionTests.ExplainAsync_WithoutPrepare_Throws` | to be written: `PostgresLiveTests.ExplainAsync_OutsideADescribe_Throws` in `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresLiveTests.cs` |

Run time, on one machine, before and after: `dotnet test --project tests/DatabaseTests/Postgres`, `dotnet test --project tests/SqlSource.Tool.Tests`.
This phase changes describer tests: run `tests/DatabaseTests/Postgres` on `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` and `tests/DatabaseTests/SqlServer` on `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04` too, for the projects it touches, as `AGENTS.md` asks before a push.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/architecture/sqlserver-describer.md`: 343
- `src/SqlSource.Tool/Describing/Postgres/AGENTS.md`: 41
- `src/SqlSource.Tool/Describing/SqlServer/AGENTS.md`: 10
- `tests/DatabaseTests/Postgres/AGENTS.md`: 27

## Left out

- F-SqlSource.Tool.Tests.Describing.Postgres-1-12 (keep, `PostgresLiveTests.UnusableException_InnerException_IsDropped`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
