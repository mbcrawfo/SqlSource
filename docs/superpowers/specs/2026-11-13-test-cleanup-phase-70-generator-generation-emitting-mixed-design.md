# Test cleanup phase 70: tests/SqlSource.Tests/Generation/Emitting, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Emitting`
- Findings: 11 (1 delete, 3 fold, 4 strengthen, 3 loosen)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 101
- Guard: mutation (generator-generation-emitting), with covers and run time for 1 finding

## Files

- `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/BatchMethodWriterTests.cs`
- `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/ExecutionCapabilityRulesTests.cs`
- `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/QueryMethodWriterTests.cs`
- `tests/SqlSource.Tests/Generation/Emitting/XmlDocWriterTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-07

- test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/ExecutionCapabilityRulesTests.cs` | `ExecutionCapabilityRulesTests.Of_QueryMethod_RestoresNoIsolation` | rows: all
- action: delete
- evidence: measured (2 rows, kills 2, alone 0, rows needed 1) and read: both rows run `ExecutionCapabilityRules.Of` on `QueryMethodWriterTests.GetUser with { Engine = ... }`, a one-optional list query method, and `Of_EachEngineShapeAndStreaming_GivesItsFlags` asserts the exact flags of that same method on each engine, 0 on PostgreSQL (line 25) and 23 on SQL Server (line 36), neither of which has `RestoresSessionIsolation` (128).  No document names the test or its class; its comment (line 157) states what those exact values prove.  blind: no
- cover: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/ExecutionCapabilityRulesTests.cs` | `ExecutionCapabilityRulesTests.Of_EachEngineShapeAndStreaming_GivesItsFlags`
- proposal: Delete it; the exact flags of every query method's shape on both engines already exclude the restore.
- verdict: confirmed
- verifier's reason: `QueryMethodWriterTests.GetUser` is a PostgreSQL `OneOptional` `List` query method with no `Batch` (QueryMethodWriterTests.cs:43-68), so both rows at ExecutionCapabilityRulesTests.cs:159-165 test that method on each engine.  `Of_EachEngineShapeAndStreaming_GivesItsFlags` asserts the exact flags of that same method on both engines: `0` at line 25 and `23` at line 36, neither holding `RestoresSessionIsolation` (128).  It also asserts every other query method's shape (lines 22-43), and every value lacks 128.  `kills` gives 2 kills, `ExecutionCapabilityRules.cs:159:9` and `159:10`, the mutants of `Has`, each killed by more than 80 other methods.  No document names the test or its class, and its comment (line 157) marks no record.

### F-SqlSource.Tests.Generation.Emitting-1-11

- test: `tests/SqlSource.Tests/Generation/Emitting/XmlDocWriterTests.cs` | `XmlDocWriterTests.Escape_TextWithNothingToEscape_IsTheSameString`; `XmlDocWriterTests.Escape_CharacterThatXmlCanHold_IsKept` | rows: all
- action: fold
- evidence: measured (`Escape_TextWithNothingToEscape_IsTheSameString`: kills 14, alone 0, timeouts under it 1; cover `MethodWriterTests.Append_SqlOverSeveralLinesWithCharactersXmlReserves_DocumentsItLineByLineAndEscaped`, `XmlDocWriterTests.Escape_CharacterThatXmlCanHold_IsKept`) and read (XmlDocWriterTests.cs:56-61 asserts `ShouldBeSameAs` of `Escape` on `"SELECT id FROM users"`, the assertion the theory at :48-53 makes of every row; no `AGENTS.md`, readme, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names either test or `XmlDocWriterTests`; the fold keeps the input, so the timeout under it stays reached); blind: no
- cover: `tests/SqlSource.Tests/Generation/Emitting/XmlDocWriterTests.cs` | `XmlDocWriterTests.Escape_CharacterThatXmlCanHold_IsKept`
- proposal: Move `"SELECT id FROM users"` into the theory as a row and delete the fact.
- verdict: confirmed
- verifier's reason: The fact (XmlDocWriterTests.cs:55-61) makes the theory's one assertion (:53), `ShouldBeSameAs`, on plain text.  `kills` gives it 14 kills, all also killed elsewhere, and one TIMEOUT at XmlDocWriter.cs:173:13 (`index++` removed), which `kills` also lists under the theory `Escape_CharacterThatXmlCanHold_IsKept`.  The one survivor under it, 182:74, is the theory's too.  The fold keeps the input as a row, so nothing it reaches is lost, and the doc search finds neither name.

### F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-01

- test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/BatchMethodWriterTests.cs` | `BatchMethodWriterTests.Append_SqlServerOwnTransaction_RestoresTheSessionsLevel` | rows: `(nameof(TransactionMode.ReadUncommitted), false)` line 330, `(nameof(TransactionMode.ReadCommitted), false)` line 331, `(nameof(TransactionMode.RepeatableRead), false)` line 332, `(nameof(TransactionMode.Serializable), false)` line 333, `(nameof(TransactionMode.Snapshot), false)` line 334, `(nameof(TransactionMode.ReadUncommitted), true)` line 336, `(nameof(TransactionMode.On), true)` line 338
- action: fold
- evidence: measured (9 rows, kills 132, alone 3, rows needed 2) and read: the writer decides the restore by the flag alone, `context.Capabilities.Has(ExecutionCapabilities.RestoresSessionIsolation)` at `src/SqlSource/Generation/Emitting/QueryMethods/CollectionMethods/BatchWriter.cs:72`, `CommandWriter.cs:81` and `CollectionMethods/BatchLocals.cs:27`, and the restore block every row asserts (BatchMethodWriterTests.cs:348-415) is the same text at every mode; which mode sets the flag is the rule's, `ExecutionCapabilityRules.cs:123-125`, whose theory has a row for each dropped row (ExecutionCapabilityRulesTests.cs:124-128, 130, 133).  No document names the test or its class; its comment (lines 324-328) states the rule the rule's theory states.  blind: no
- cover: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/ExecutionCapabilityRulesTests.cs` | `ExecutionCapabilityRulesTests.Of_CollectionMethodAtAMode_RestoresTheSessionsIsolationOnlyWithItsOwnTransactionOnSqlServer`
- proposal: Keep the rows `(nameof(TransactionMode.On), false)` (line 337) and `(nameof(TransactionMode.Serializable), true)` (line 335), one query method and one repository's method, and drop the seven others, whose mode only the rule's theory reads.
- verdict: confirmed
- verifier's reason: The restore is written under `RestoresSessionIsolation` alone (`src/SqlSource/Generation/Emitting/QueryMethods/CollectionMethods/BatchWriter.cs:72-87`, `BatchLocals.cs:27-30`), and nothing the test asserts (BatchMethodWriterTests.cs:348-415) depends on the mode.  The mode changes only the arguments of `BeginTransactionAsync` (`BatchWriter.cs:142-148`), and the order check at line 366-367 looks only for the name.  Which modes set the flag is `BeginsItsOwn`'s (`ExecutionCapabilityRules.cs:141-152`), and the rule's theory has a row for each dropped row: ExecutionCapabilityRulesTests.cs:124-128, 130 and 133.  `kills` on generator-generation-emitting gives 132 kills and 3 alone, `ExecutionCapabilityWriter.cs:289:53`, `289:98` and `307:21`.  All three are string mutations in the restore block, which every row writes, so the two kept rows keep them.  The kills at `ExecutionCapabilityRules.cs:145:13` that depend on the mode (the `or`→`and` mutations) are also killed by the rule's theory, which has a row for each mode.  The 3 timeouts (`LocalNames.cs:23:16`, `XmlDocWriter.cs:173:13`, `199:21`) are under nearly every writer test, the cover among them.  The search of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred names neither the test nor `BatchMethodWriterTests`.  The comment at lines 324-328 states the rule and marks no record.  The fold drops rows that share one name, so a filter cannot measure it: it is confirmed by reading, and the cleanup phase's own guard proves it.

### F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-02

- test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/BatchMethodWriterTests.cs` | `BatchMethodWriterTests.Append_OffOrPostgreSql_LeavesTheSessionsLevelAlone` | rows: `(nameof(DatabaseEngine.SqlServer), nameof(TransactionMode.Off), true)` line 422, `(nameof(DatabaseEngine.PostgreSql), nameof(TransactionMode.On), false)` line 423, `(nameof(DatabaseEngine.PostgreSql), nameof(TransactionMode.On), true)` line 424, `(nameof(DatabaseEngine.PostgreSql), nameof(TransactionMode.ReadCommitted), false)` line 425, `(nameof(DatabaseEngine.PostgreSql), nameof(TransactionMode.RepeatableRead), false)` line 426
- action: fold
- evidence: measured (8 rows, kills 43, alone 0, rows needed 2) and read: without `RestoresSessionIsolation` the writer writes none of the asserted text (BatchWriter.cs:72, CommandWriter.cs:81, BatchLocals.cs:27-30), and that no flag is given at these engines and modes is proved by the rule's theories: ExecutionCapabilityRulesTests.cs:134, 138, 135 and 136 for the dropped rows, and for PostgreSQL's repository under on the exact flags 32 at ExecutionCapabilityRulesTests.cs:100.  No document names the test or its class; its comment (lines 418-419) says nothing the rule's theory does not.  blind: no
- cover: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/ExecutionCapabilityRulesTests.cs` | `ExecutionCapabilityRulesTests.Of_CollectionMethodAtAMode_RestoresTheSessionsIsolationOnlyWithItsOwnTransactionOnSqlServer`; `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/ExecutionCapabilityRulesTests.cs` | `ExecutionCapabilityRulesTests.Of_CollectionMethod_GivesItsEnginesFlags`
- proposal: Keep `(nameof(DatabaseEngine.SqlServer), nameof(TransactionMode.Off), false)` (line 421) and `(nameof(DatabaseEngine.PostgreSql), nameof(TransactionMode.Serializable), true)` (line 428), the latter the row whose `System.Data.IsolationLevel` the case-sensitive check is about, and drop the rest.
- verdict: confirmed
- verifier's reason: Without the flag the writer declares, reads and restores nothing (`BatchWriter.cs:72-87`, `BatchLocals.cs:27-30`, `CommandWriter.cs:81`).  The rule's theory covers every dropped row: ExecutionCapabilityRulesTests.cs:134 (SQL Server, off, repository), 138 (PostgreSQL, on), 135 (PostgreSQL, read committed) and 136 (PostgreSQL, repeatable read).  PostgreSQL's repository under on is covered by the exact flags `32` at line 100 (`Batch = new BatchCall(TransactionMode.On)`, line 114).  `kills` gives 43 kills and 0 alone.  The two kept rows still kill each mutant that depends on a row.  `ExecutionCapabilityRules.cs:123:21` (Conditional true) and `144:36` (`true`) need SQL Server under off, which row 421 keeps.  `103:55` (`capabilities | ~TransactionFlags`) needs a repository, which row 428 keeps.  Row 428 is also the case-sensitive check's own row (BatchMethodWriterTests.cs:442), since it begins at `System.Data.IsolationLevel.Serializable`.  The 3 timeouts are the ones shared by every writer test.  No document names the test or its class, and the comment at lines 418-419 marks no record.  The fold drops theory rows, so it is confirmed by reading, and the cleanup phase's own guard proves it.

### F-SqlSource.Tests.Generation.Emitting-1-04

- test: `tests/SqlSource.Tests/Generation/Emitting/XmlDocWriterTests.cs` | `XmlDocWriterTests.Escape_CharacterThatXmlCanHold_IsKept` | rows: all
- action: strengthen
- evidence: measured (4 rows; kills 22, alone 1, timeouts under it 1; survivor src/SqlSource/Generation/Emitting/XmlDocWriter.cs:182:74 Equality mutation (`> '\uE000'`), reached by 55 methods) and read (the theory holds the upper edge of the range after the surrogates, `"a�b"` at XmlDocWriterTests.cs:51, but not its lower edge, so a writer that replaces U+E000, which XML holds, with U+FFFD passes); blind: no (line 182 also has mutants that do not compile, but this one ran)
- cover: none
- proposal: Add the row `[InlineData("a\uE000b")]`, the first character after the surrogates.
- verdict: confirmed
- verifier's reason: `IsAllowedInXml` (XmlDocWriter.cs:182) allows `>= '\uE000' and <= '�'`; the rows at XmlDocWriterTests.cs:49-52 hold `\t`, a surrogate pair, U+FFFD and U+0085/U+2028, none of them U+E000, and `kills` lists `SURVIVED src/SqlSource/Generation/Emitting/XmlDocWriter.cs:182:74 Equality mutation -> > '\uE000'` under the test.  A row of U+E000 is replaced under the mutant and fails `ShouldBeSameAs`.  The proposed row holds U+E000 as a literal character (bytes EE 80 80 in the findings file); the cleanup should write it `"a\uE000b"`, as the file's other rows escape theirs.
- survivors to kill: `src/SqlSource/Generation/Emitting/XmlDocWriter.cs:182:74`
- condition: Write the new row's character as its escape, `"a\uE000b"`, as the file's other rows write theirs; the finding's text holds it as a literal character.
- a cover: `XmlDocWriterTests.Escape_CharacterThatXmlCanHold_IsKept` is the cover of F-SqlSource.Tests.Generation.Emitting-1-11 (phase 70).  The change keeps every assertion and row that makes it so.

### F-SqlSource.Tests.Generation.Emitting-1-05

- test: `tests/SqlSource.Tests/Generation/Emitting/XmlDocWriterTests.cs` | `XmlDocWriterTests.AppendMember_TextEndingInALineTerminator_EndsWithAnEmptyCommentLine` | rows: all
- action: strengthen
- evidence: measured (kills 32, alone 0, timeouts under it 2; survivors src/SqlSource/Generation/Emitting/XmlDocWriter.cs:198:47 Equality mutation (`end + 1 <= text.Length`) and src/SqlSource/Generation/Emitting/XmlDocWriter.cs:198:47 Arithmetic mutation (`end - 1`), each reached by `AppendMember_AnyLineTerminatorOfCSharp_StartsANewCommentLine` alone, whose rows never end the text in a terminator) and read (the test at XmlDocWriterTests.cs:150 ends the text in `\n` only; both mutants read past the end of a text that ends in `\r`, which is a line terminator the name promises, and throw); blind: no
- cover: none
- proposal: Make it a theory over `"\n"`, `"\r"` and `"\r\n"` at the end of the text, each giving the same empty comment line.
- verdict: confirmed
- verifier's reason: The test (XmlDocWriterTests.cs:145-153) ends the text in `\n`, so `text[end] == '\r'` at XmlDocWriter.cs:198 is false and the right side of `&&` never runs.  For a text ending in `\r`, `end + 1 == text.Length`: under `end + 1 <= text.Length` or `end - 1 < text.Length` the code reads `text[end + 1]` and throws.  The evidence (lines 414-415) names `AppendMember_AnyLineTerminatorOfCSharp_StartsANewCommentLine` as the one test reaching both survivors, and its rows (:125-130) put the terminator in the middle of the text only.  Unmutated, `"SELECT 1\r"` and `"SELECT 1\r\n"` give the same empty last line as `\n`, so the proposed theory expects one text.
- survivors to kill: `src/SqlSource/Generation/Emitting/XmlDocWriter.cs:198:47`

### F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-03

- test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/BatchMethodWriterTests.cs` | `BatchMethodWriterTests.Append_SynchronousSqlServerReadingRows_ChecksAsTheAsynchronousMethodDoes` | rows: all
- action: strengthen
- evidence: read: the comment (lines 981-982) promises that the synchronous method checks each result set, refuses a completed transaction and reports a rollback, but the test asserts only `TransactionRolledBackException`, which the method's documentation holds whether or not a catch throws it, `reader.NextResult()`, which the drain of the database error's catch holds without any result-set check, and `reader.Dispose();` (BatchMethodWriterTests.cs:988-990); nothing checks the refusal at all, so the sync method could lose its refusal, its `FieldCount` check or its throw of `TransactionRolledBackException` and pass.  Measured: kills 61, alone 0.  blind: no
- cover: none
- proposal: Assert the synchronous refusal `if (transaction is not null && transaction.Connection is null)`, the result-set check `!reader.NextResult())` with `|| reader.FieldCount != 2)`, and the catch's `reader.Dispose();` followed by `throw new global::SqlSource.TransactionRolledBackException("GetUser", exception);`, as the asynchronous tests assert them.
- verdict: confirmed
- verifier's reason: The test (BatchMethodWriterTests.cs:984-992) asserts `TransactionRolledBackException`, `reader.NextResult()` and `reader.Dispose();\n` anywhere in the text.  The documentation's cref (`ExecutionCapabilityWriter.cs:23`) and the database error's catch (`ExecutionCapabilityWriter.cs:448-466`) both hold `TransactionRolledBackException` and `reader.Dispose();`.  That catch's drain (`AppendDrainLoop` with `NoCancellation`, line 434) also writes `reader.NextResult()`, and so does the plain move of `BatchRowsWriter.cs:245`.  So removing the rollback catch of `AppendRollbackCheck` (lines 359-397), or the `FieldCount` check of `AppendResultSetCheck` (lines 106-117), fails nothing.  Nothing in the test names the refusal of `AppendCompletedTransactionCheck` (lines 316-346).  `kills` gives 61 kills, 0 alone and 0 survivors named, which agrees.  The comment at lines 981-982 promises all three behaviours.
- survivors to kill: none named by place; the missing assertion is the proposal's

### F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-10

- test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/QueryMethodWriterTests.cs` | `QueryMethodWriterTests.Append_EachPooledCollectionTypeUnderArrayPoolOff_ReadsItsRowsAsBeforeThePool` | rows: all
- action: strengthen
- evidence: measured (8 rows, kills 60, alone 5; it is the only test under the survivors `src/SqlSource/Generation/Emitting/QueryMethods/RowsWriter.cs:373:72 String mutation` and `src/SqlSource/Generation/Emitting/QueryMethods/RowsWriter.cs:377:94 String mutation`) and read: it asserts `"        var rows = " + buffer` where `buffer` ends at the generic's `<` (QueryMethodWriterTests.cs:912-927, 943), so the buffer's row type and its `>()`, which those mutants remove, are never checked though the name promises how the rows are read.  blind: no
- cover: none
- proposal: Assert the whole declaration of the buffer, `"        var rows = " + buffer + "global::App.GetUserDto>();\n"`, which kills both survivors.
- verdict: confirmed
- verifier's reason: The theory asserts `"        var rows = " + buffer`, where `buffer` stops at the generic's `<` (QueryMethodWriterTests.cs:912-927, 943).  `kills` shows both survivors under this test alone.  `RowsWriter.cs:373:72` is the `">()"` of the `List` buffer for `List`/`IList`/`ICollection`, rows 920-922, and `377:94` is the `">()"` of the `ImmutableArray` builder, row 923-927.  The declaration is written as `var rows = <buffer>;\n` (`RowsWriter.cs:83`), so the proposed assertion `buffer + "global::App.GetUserDto>();\n"` kills both.  The name promises how the rows are read, and the survivors break exactly that.
- survivors to kill: `src/SqlSource/Generation/Emitting/QueryMethods/RowsWriter.cs:373:72`, `src/SqlSource/Generation/Emitting/QueryMethods/RowsWriter.cs:377:94`

### F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-08

- test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/ExecutionCapabilityRulesTests.cs` | `ExecutionCapabilityRulesTests.Flags_AsNumbers_HaveTheValuesOfTheTheory` | rows: all
- action: loosen
- evidence: measured (reaches no tested mutant) and read: three theories give their expected flags as numbers (`0`, `1`, `2`, `10`, `23`, `31`, `32`, `128`, `129`, `192`, `213`, ExecutionCapabilityRulesTests.cs:22-43, 64-69, 90-100), and this test pins the enum's values so a reader can decode them (lines 167-190); giving `ExecutionCapabilities` other values, which changes no behaviour, fails this test and every row of the three theories.  An attribute argument may be a constant expression such as `(int)(ExecutionCapabilities.RefusesCompletedTransaction | ExecutionCapabilities.DrainsLaterResults)` or `(int)SqlServerReadsRows`, so the numbers are not needed even though the theories are public and the enum internal.  blind: no
- cover: none
- proposal: Write each row's expected value as the cast of its flags, using the file's `SqlServerReadsRows` where it fits, and then delete this test, whose only purpose is to decode the numbers.
- verdict: confirmed
- verifier's reason: The test (ExecutionCapabilityRulesTests.cs:168-190) pins the enum's numeric values, and `kills` shows it reaches no tested mutant.  The three theories at lines 22-43, 64-69 and 90-100 give numbers.  The flags are computed in the output step and never cached (`src/SqlSource/Generation/Emitting/QueryMethods/AGENTS.md`), so renumbering them changes no behaviour, yet it fails this test and every row.  The theories take `int` because a public method cannot take the internal enum.  An attribute argument may still be a constant cast such as `(int)SqlServerReadsRows` (the private const at line 12) or `(int)(A | B)`, which accessibility does not restrict.  The loosen is feasible as proposed.

### F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-11

- test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/QueryMethodWriterTests.cs` | `QueryMethodWriterTests.Append_SqlServerEachShape_RefusesACompletedTransactionBeforeItOpensTheConnection` | rows: all
- action: loosen
- evidence: read: besides the refusal's place (lines 834-839, 843-844) it counts every `.Connection is null)\n` in the method and expects 1, or 3 for a shape that reads rows (lines 840-842); the two extra are the rollback checks of `TransactionCheck` and `DatabaseErrorCheck` (lines 2107, 2209), so a change to how those catches test the connection, which other tests own, fails this test though the refusal is unchanged.  blind: no
- cover: none
- proposal: Count only the refusal, the occurrences of `CompletedTransactionCheck("    ")` or of `transaction is not null && transaction.Connection is null`, and expect 1 for every shape.
- verdict: confirmed
- verifier's reason: The test counts every `.Connection is null)\n` and expects 3 for the shapes that read rows (QueryMethodWriterTests.cs:840-842).  For those shapes, two of the three come from other rules.  One is the rollback catch's `if (transaction.Connection is null)` (`ExecutionCapabilityWriter.cs:380-382`, the test's `TransactionCheck` at line 2107).  The other is the database error's `if (raisedLater && transaction.Connection is null)` (`ExecutionCapabilityWriter.cs:452-456`, `DatabaseErrorCheck` at line 2209).  Rewording either condition leaves the refusal unchanged and still fails this test.  The proposed needle, `transaction is not null && transaction.Connection is null` (`CompletedTransactionCheck`, line 2227), matches the refusal alone.  The absence of the rollback checks on `None`/`RowCount` is `Append_SqlServerShapeReadingNoRows_DoesNotCheckTheTransaction`'s rule (lines 800-806).

### F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-12

- test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/QueryMethodWriterTests.cs` | `QueryMethodWriterTests.Append_SqlServerStreams_DrainsInAFinallyWhenLeftAtARow` | rows: all
- action: loosen
- evidence: measured (kills 226, alone 0; cover includes `Append_SqlServerStreams_ChecksTheTransactionWhenARowFails` and `Append_SqlServerStreams_DrainsAfterTheReadLoop`) and read: its one expected text (lines 1579-1619) runs from the reader's scope to the outer finally, so it holds the whole of `Append_SqlServerStreams_ChecksTheTransactionWhenARowFails`'s expected text (lines 1629-1656, the same input) and of `Append_SqlServerStreams_DrainsAfterTheReadLoop`'s SQL Server text (lines 1559-1566); a change to the read's try, the row's catch or the drain after the loop, each another test's rule, fails it too though the drain in the finally is unchanged.  That the row is built before the flag is set, which its comment claims, is in `ChecksTheTransactionWhenARowFails`'s text, which ends at `atRow = true;`.  blind: no
- cover: none
- proposal: Assert only its rule: the lines `atRow = true;`, `yield return row;`, `atRow = false;` and the finally's `if (atRow)` with the drain given `CancellationToken.None`, leaving the read, the row's catch and the drain after the loop to the two focused tests.
- verdict: confirmed
- verifier's reason: The test's one expected text (QueryMethodWriterTests.cs:1579-1619) covers everything from `readerScope` to the outer `finally`.  It holds `CheckedRead` with its `DatabaseErrorCheck` (helper at line 2125-2147), the row's `try`, `TransactionCheck`, the drain after the loop and the `if (atRow)` drain.  That covers the whole of `Append_SqlServerStreams_ChecksTheTransactionWhenARowFails`'s text (lines 1629-1656, same input), which ends at `atRow = true;`.  It also covers `Append_SqlServerStreams_DrainsAfterTheReadLoop`'s text (lines 1559-1566).  A change to either of those rules fails this test with its own rule unchanged.  `kills` gives 226 kills and 0 alone.  `docs/architecture/generator.md:692` names `QueryMethodWriterTests` only for the four writings of each flag alone, not this test.  The comment at lines 1570-1574 marks no record.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-emitting`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-70/generator-generation-emitting/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Emitting.' 'Generation/Emitting/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-70/generator-generation-emitting/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Emitting.' 'Generation/Emitting/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-70/generator-generation-emitting/before/reports/mutation-report.json artifacts/test-cleanup/phase-70/generator-generation-emitting/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-70/generator-generation-emitting/before/reports/mutation-report.json XmlDocWriterTests.Escape_CharacterThatXmlCanHold_IsKept
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-70/generator-generation-emitting/before/reports/mutation-report.json XmlDocWriterTests.AppendMember_TextEndingInALineTerminator_EndsWithAnEmptyCommentLine
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-70/generator-generation-emitting/before/reports/mutation-report.json XmlDocWriterTests.Escape_TextWithNothingToEscape_IsTheSameString
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-70/generator-generation-emitting/before/reports/mutation-report.json BatchMethodWriterTests.Append_SqlServerOwnTransaction_RestoresTheSessionsLevel
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-70/generator-generation-emitting/before/reports/mutation-report.json BatchMethodWriterTests.Append_OffOrPostgreSql_LeavesTheSessionsLevelAlone
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-70/generator-generation-emitting/before/reports/mutation-report.json BatchMethodWriterTests.Append_SynchronousSqlServerReadingRows_ChecksAsTheAsynchronousMethodDoes
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-70/generator-generation-emitting/before/reports/mutation-report.json ExecutionCapabilityRulesTests.Of_QueryMethod_RestoresNoIsolation
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-70/generator-generation-emitting/before/reports/mutation-report.json ExecutionCapabilityRulesTests.Flags_AsNumbers_HaveTheValuesOfTheTheory
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-70/generator-generation-emitting/before/reports/mutation-report.json QueryMethodWriterTests.Append_EachPooledCollectionTypeUnderArrayPoolOff_ReadsItsRowsAsBeforeThePool
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-70/generator-generation-emitting/before/reports/mutation-report.json QueryMethodWriterTests.Append_SqlServerEachShape_RefusesACompletedTransactionBeforeItOpensTheConnection
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-70/generator-generation-emitting/before/reports/mutation-report.json QueryMethodWriterTests.Append_SqlServerStreams_DrainsInAFinallyWhenLeftAtARow
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Generation.Emitting-1-04: `src/SqlSource/Generation/Emitting/XmlDocWriter.cs:182:74`
- F-SqlSource.Tests.Generation.Emitting-1-05: `src/SqlSource/Generation/Emitting/XmlDocWriter.cs:198:47`
- F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-03: no survivor is named by place; the test fails when the assertion it adds is removed, checked once by hand
- F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-10: `src/SqlSource/Generation/Emitting/QueryMethods/RowsWriter.cs:373:72`, `src/SqlSource/Generation/Emitting/QueryMethods/RowsWriter.cs:377:94`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-emitting`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-70/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-70/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-70/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-70/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-70/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-70/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tests.Generation.Emitting.QueryMethods-1-08.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
