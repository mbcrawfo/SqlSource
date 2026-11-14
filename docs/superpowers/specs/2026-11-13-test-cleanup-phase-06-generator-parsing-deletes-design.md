# Test cleanup phase 06: tests/SqlSource.Tests/Parsing, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Parsing`
- Findings: 8 (8 delete)
- Kind: one kind, delete
- Changed lines: about 47
- Guard: mutation (generator-parsing), with covers and run time for 3 findings

## Files

- `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs`
- `tests/SqlSource.Tests/Parsing/SqlModelTests.cs`
- `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Parsing-2-06

- test: `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` | `SqlLexerTests.Lex_DollarQuote_EndsAtTheSameTag` | rows: all
- action: delete
- evidence: measured (5 rows; kills 48, alone 0, timeouts under it 9; rows needed 3; cover: `Lex_DollarTagWithANonAsciiCharacter_IsAQuote`, `Lex_Construct_IsReadByTheRulesOfTheDialect`, `DollarQuoteReaderTests.FindEnd_DollarThatOpensNoQuote_IsNotAQuote`); read (its five texts, SqlLexerTests.cs:64-68, are the first five rows of `DollarQuoteReaderTests.FindEnd_OpenerWithItsCloser_EndsAfterTheSameTag` word for word, DollarQuoteReaderTests.cs:12, :14-17; the lexer names no quoting form and hands a `$` to the reader its rules name, SqlLexer.cs:126-129, src/SqlSource/Parsing/AGENTS.md; that ANSI and three other dialects hand `$$ -- x $$ y` to the dollar reader is `Lex_Construct_IsReadByTheRulesOfTheDialect`'s row at SqlLexerTests.cs:304, and a non-ASCII tag through the lexer is `Lex_DollarTagWithANonAsciiCharacter_IsAQuote`, :98-109; the 9 timeouts are in the lexer's loop, which the covers run on the same constructs, `Lex_Construct_IsReadByTheRulesOfTheDialect` with 18 timeouts under it; no AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names the test, and only TD-0004 names its class, for the `Lex_KnownLimit_*` tests and three other rows); blind: no
- cover: `tests/SqlSource.Tests/Parsing/Quoting/DollarQuoteReaderTests.cs` | `DollarQuoteReaderTests.FindEnd_OpenerWithItsCloser_EndsAfterTheSameTag`; `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` | `SqlLexerTests.Lex_Construct_IsReadByTheRulesOfTheDialect`; `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` | `SqlLexerTests.Lex_DollarTagWithANonAsciiCharacter_IsAQuote`
- proposal: Delete the test; the reader's theory holds the tags and the dialect table holds the lexer's use of the reader.
- verdict: confirmed
- verifier's reason: The texts at SqlLexerTests.cs:64-68 are DollarQuoteReaderTests.cs:12 and :14-17 word for word.  The lexer adds nothing for them beyond handing the `$` to the reader and adding a Quoted lexeme (SqlLexer.cs:126-129, :254-283).  `Lex_Construct_IsReadByTheRulesOfTheDialect` makes that hand-off for ANSI and three other dialects, with the same expected lexemes (:304).  The non-ASCII tag through the lexer is `Lex_DollarTagWithANonAsciiCharacter_IsAQuote` (:98-109).  Every kill of the test and all 9 of its timeouts are among the kills and timeouts of the three named covers, checked set against set from their `kills` output.  No comment stands above the test.  The file's header comment and the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred do not name the test.  TD-0004 names only the `Lex_KnownLimit_*` tests and three other rows.

### F-SqlSource.Tests.Parsing-2-07

- test: `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` | `SqlLexerTests.Lex_QuotedRegion_EndsAtTheUndoubledQuote` | rows: all
- action: delete
- evidence: measured (4 rows; kills 31, alone 0, timeouts under it 8; rows needed 1; cover: `Lex_CommentSyntaxInsideQuotedRegion_IsNotAComment`, `DoubledQuoteReaderTests.FindEnd_ClosedRegion_EndsAfterTheQuoteThatIsNotDoubled`); read (its rows, SqlLexerTests.cs:34-37, are rows of the readers ANSI uses, `SqlDialectRules.Ansi` at src/SqlSource/Parsing/SqlDialectRules.cs:73-74: `'it''s' x` at EscapeStringReaderTests.cs:14, `"a""b" x` and `` `a``b` x `` at DoubledQuoteReaderTests.cs:18-19, and `''` at :14, whose doubled end the two readers share in `QuoteReader.FindDoubledEnd`; that the lexer hands each of the three quotes of ANSI to a reader and goes on after it is `Lex_CommentSyntaxInsideQuotedRegion_IsNotAComment`, :42-47, with the same `[quoted, "Text: ..."]` shape; the 8 timeouts are reached by that cover, 8 under it; no document names the test); blind: no
- cover: `tests/SqlSource.Tests/Parsing/Quoting/DoubledQuoteReaderTests.cs` | `DoubledQuoteReaderTests.FindEnd_ClosedRegion_EndsAfterTheQuoteThatIsNotDoubled`; `tests/SqlSource.Tests/Parsing/Quoting/EscapeStringReaderTests.cs` | `EscapeStringReaderTests.FindEnd_String_TakesBackslashEscapesOnlyAfterThePrefix`; `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` | `SqlLexerTests.Lex_CommentSyntaxInsideQuotedRegion_IsNotAComment`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: ANSI reads `'` with `EscapeString` and reads `"` and `` ` `` with `Doubled` (SqlDialectRules.cs:73-74).  Without a prefix both readers go through `QuoteReader.FindDoubledEnd` (QuoteReader.cs:79-99; EscapeStringReader.cs FindEnd).  Each row of the test is a reader row: `'it''s' x` at EscapeStringReaderTests.cs:14, and `''`, `"a""b" x` and `` `a``b` x `` at DoubledQuoteReaderTests.cs:14, :18-19.  `Lex_CommentSyntaxInsideQuotedRegion_IsNotAComment` (SqlLexerTests.cs:42-47) puts each of the three quotes of ANSI through the lexer, with the same `[quoted, "Text: ..."]` assertion.  Every kill and all 8 timeouts are among the named covers' kills and timeouts.  No comment and no document names the test.

### F-SqlSource.Tests.Parsing-2-08

- test: `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` | `SqlLexerTests.Lex_EscapeString_TreatsBackslashAsAnEscape` | rows: all
- action: delete
- evidence: measured (2 rows; kills 43, alone 0, timeouts under it 9; rows needed 1; cover: `Lex_Construct_IsReadByTheRulesOfTheDialect`); read (its texts, SqlLexerTests.cs:50-51, are `EscapeStringReaderTests.FindEnd_String_TakesBackslashEscapesOnlyAfterThePrefix`'s rows at EscapeStringReaderTests.cs:17-18 with a comment after them; that ANSI reads `E'a\'b' -- c'` with the escape, the prefix as text and the comment after it is `Lex_Construct_IsReadByTheRulesOfTheDialect`'s row at SqlLexerTests.cs:260-264, and the lower-case prefix through the lexer is the row at :385 of `Lex_EscapeStringOfPostgreSqlThatGoesOnAfterAGap_ReadsEachPartWithBackslashEscapes`, which uses the same reader object, SqlDialectRules.cs:31, :85-86; the 9 timeouts are reached by `Lex_Construct_IsReadByTheRulesOfTheDialect`, 18 under it; no document names the test); blind: no
- cover: `tests/SqlSource.Tests/Parsing/Quoting/EscapeStringReaderTests.cs` | `EscapeStringReaderTests.FindEnd_String_TakesBackslashEscapesOnlyAfterThePrefix`; `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` | `SqlLexerTests.Lex_Construct_IsReadByTheRulesOfTheDialect`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: The rows (SqlLexerTests.cs:50-51) are EscapeStringReaderTests.cs:17-18 with a comment after them.  The lexer's own part, the prefix kept as text with the escaped region and a comment after it under ANSI, is the row at SqlLexerTests.cs:260-264.  The lexer does not look at the prefix's case.  The reader does, with one `EscapeString` object for ANSI and PostgreSQL (SqlDialectRules.cs:31, :74, :86), and its rows hold both cases.  Every kill and all 9 timeouts are among those of the two named covers.  No comment or document names the test.

### F-SqlSource.Tests.Parsing-2-09

- test: `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` | `SqlLexerTests.Lex_EscapeStringWithDoubledQuote_EndsAtTheUndoubledQuote` | rows: all
- action: delete
- evidence: measured (1 row; kills 33, alone 0, timeouts under it 9; cover: `Lex_Construct_IsReadByTheRulesOfTheDialect`, `BackslashQuoteReaderTests.FindEnd_ClosedRegion_EndsAfterTheQuoteThatIsNeitherEscapedNorDoubled`); read (its text, `E'a''b\\' x` at SqlLexerTests.cs:57, is the row of `EscapeStringReaderTests.FindEnd_String_TakesBackslashEscapesOnlyAfterThePrefix` at EscapeStringReaderTests.cs:20 word for word, and a doubled quote beside escapes is `BackslashQuoteReaderTests`'s rows at BackslashQuoteReaderTests.cs:16-17; the lexer's part, the prefix as text before the region, is the row at SqlLexerTests.cs:260-264; the 9 timeouts are reached by that cover; no document names the test); blind: no
- cover: `tests/SqlSource.Tests/Parsing/Quoting/EscapeStringReaderTests.cs` | `EscapeStringReaderTests.FindEnd_String_TakesBackslashEscapesOnlyAfterThePrefix`; `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` | `SqlLexerTests.Lex_Construct_IsReadByTheRulesOfTheDialect`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: The text `E'a''b\\' x` (SqlLexerTests.cs:57) is EscapeStringReaderTests.cs:20 word for word.  The lexer's part, the prefix kept as text before the region, is SqlLexerTests.cs:260-264.  Every kill (33) and all 9 timeouts are among those of `FindEnd_String_TakesBackslashEscapesOnlyAfterThePrefix` and `Lex_Construct_IsReadByTheRulesOfTheDialect`.  No comment or document names the test.

### F-SqlSource.Tests.Parsing-2-10

- test: `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` | `SqlLexerTests.Lex_LetterEAtTheEndOfAnIdentifier_IsNotAnEscapeStringPrefix` | rows: all
- action: delete
- evidence: measured (1 row; kills 42, alone 0, timeouts under it 8; cover: `Lex_StringOfPostgreSqlThatIsNotContinued_IsReadByItsOwnPrefix`); read (its text, `typeE'a\' -- c` at SqlLexerTests.cs:61, is the row `typeE'a\' x` of `EscapeStringReaderTests.FindEnd_LetterEAtTheEndOfAnIdentifier_IsNotAPrefix`, EscapeStringReaderTests.cs:27, with a comment after it, and the lexer's reading of `typeE` before a string, prefix kept as text, is the row at SqlLexerTests.cs:442 of `Lex_StringOfPostgreSqlThatIsNotContinued_IsReadByItsOwnPrefix`, whose rules hand `'` to the same reader object as ANSI's, SqlDialectRules.cs:74, :86; that cover has 11 timeouts under it to this test's 8; no document names the test); blind: no
- cover: `tests/SqlSource.Tests/Parsing/Quoting/EscapeStringReaderTests.cs` | `EscapeStringReaderTests.FindEnd_LetterEAtTheEndOfAnIdentifier_IsNotAPrefix`; `tests/SqlSource.Tests/Parsing/SqlLexerTests.cs` | `SqlLexerTests.Lex_StringOfPostgreSqlThatIsNotContinued_IsReadByItsOwnPrefix`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: The reader's answer for `typeE'a\'` is EscapeStringReaderTests.cs:27.  Through the lexer, `HasPrefix` (EscapeStringReader.cs) is false for `typeE`, and both `FindEnd` and `FindContinuation` use it.  The row at SqlLexerTests.cs:442 shows this: `typeE` stays text and the next part is not continued.  It runs under PostgreSQL, whose `'` reader is the same object as ANSI's (SqlDialectRules.cs:74, :86).  Every kill and all 8 timeouts are among those of the two named covers, `Lex_StringOfPostgreSqlThatIsNotContinued_IsReadByItsOwnPrefix` with 11 timeouts under it.  No comment or document names the test.

### F-SqlSource.Tests.Parsing-2-13

- test: `tests/SqlSource.Tests/Parsing/SqlModelTests.cs` | `SqlModelTests.SqlParseError_Create_HoldsItsArguments` | rows: all
- action: delete
- evidence: measured (1 row; reaches no tested mutant); read (`SqlParseError.Create`, src/SqlSource/Parsing/SqlParseError.cs:17-18, is the one constructor the parser uses, as `SqlGeneratorParameterScope.Read` does, SqlGeneratorParameterScope.cs:42, :59-60, and `TokenScanner.Scan`; `Read_OneOptionInTwoStates_ConflictsAtTheLater` asserts the kind, the span and the arguments of such an error one by one against literal values, SqlGeneratorParameterScopeTests.cs:99-102, and `Scan_ReservedNameWithADefault_IsAnError` does the same, TokenScannerTests.cs:147-151; no AGENTS.md, readme, docs/architecture, docs/tech-debt or docs/deferred names the test or its class); blind: no
- cover: `tests/SqlSource.Tests/Parsing/Markers/SqlGeneratorParameterScopeTests.cs` | `SqlGeneratorParameterScopeTests.Read_OneOptionInTwoStates_ConflictsAtTheLater`; `tests/SqlSource.Tests/Parsing/Tokens/TokenScannerTests.cs` | `TokenScannerTests.Scan_ReservedNameWithADefault_IsAnError`
- proposal: Delete the test, and keep `SqlParseError_SameValuesInSeparateInstances_AreEqual` (F-SqlSource.Tests.Parsing-2-18).
- verdict: confirmed
- verifier's reason: `SqlParseError.Create` (SqlParseError.cs:17-18) is the record's only factory.  `Read_OneOptionInTwoStates_ConflictsAtTheLater` (SqlGeneratorParameterScopeTests.cs:99-102) asserts the kind, span and arguments of an error made by it one by one against literal values.  `Scan_ReservedNameWithADefault_IsAnError` (TokenScannerTests.cs:147-151) does the same.  Those direct assertions, not the comparisons of one `Create` with another, are what would catch a `Create` that dropped or moved an argument, and they make this test redundant.  `kills` shows it reaches no mutant.  No comment or record document names the test or its class.

### F-SqlSource.Tests.Parsing-2-14

- test: `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs` | `SqlQueryHashTests.Compute_AnyQuery_IsSixtyFourLowerCaseHexCharacters` | rows: all
- action: delete
- evidence: measured (1 row; kills 98, alone 0, timeouts under it 10; cover: `Compute_AnotherEngine_StartsFromItsCanonicalName`); read (it hashes `Simple`, SqlQueryHashTests.cs:147, the same input as `Compute_SimpleQuery_IsTheSha256OfItsInput`, :17-18, which asserts the exact digest, 64 lower-case hex characters, and so runs the same code with the same 10 timeouts under it; the one other mention of its name is a plan under docs/superpowers); blind: yes (SqlQueryHash.cs:35-94 does not compile under some mutants)
- cover: `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs` | `SqlQueryHashTests.Compute_SimpleQuery_IsTheSha256OfItsInput`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: Both tests hash `Simple` (SqlQueryHashTests.cs:147, :17-18).  The exact 64-character lower-case digest that the cover asserts implies the regex `^[0-9a-f]{64}$`, so the cover catches any change the regex would catch, on the blind lines of SqlQueryHash.cs too.  Every kill (98) and all 10 timeouts are among those of `Compute_SimpleQuery_IsTheSha256OfItsInput`.  The only other mention is a plan under docs/superpowers.

### F-SqlSource.Tests.Parsing-2-15

- test: `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs` | `SqlQueryHashTests.Compute_ParamMarkerWithAType_ChangesTheHash` | rows: all
- action: delete
- evidence: measured (1 row; kills 208, alone 0, timeouts under it 13; cover: `Compute_ChangeToWhatIsDescribed_GivesAnotherHash`, `SqlMarkerScopeTests.Read_TwoTokenDefaultsThatDifferOnlyInAComment_AreNoConflict`); read (the row `("-- param: @a\nSELECT @a", "-- param: @a int\nSELECT @a")` of `Compute_ChangeToWhatIsDescribed_GivesAnotherHash`, SqlQueryHashTests.cs:57, holds that a type changes the hash of a bare marker, and `Compute_BareParamMarker_IsTheHashWithoutIt`, :79-80, that a bare marker hashes as no marker, which together are this test's claim, :83-84; those covers have 16 and 13 timeouts under them; no document names the test); blind: yes (SqlQueryHash.cs:35-94 does not compile under some mutants)
- cover: `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs` | `SqlQueryHashTests.Compute_ChangeToWhatIsDescribed_GivesAnotherHash`; `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs` | `SqlQueryHashTests.Compute_BareParamMarker_IsTheHashWithoutIt`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: Two tests together hold this test's claim (SqlQueryHashTests.cs:83-84).  The row at :57 of `Compute_ChangeToWhatIsDescribed_GivesAnotherHash` shows that a type changes the hash of a bare marker.  `Compute_BareParamMarker_IsTheHashWithoutIt` (:79-80) shows that a bare marker hashes as no marker.  The exact digest of `Compute_QueryWithTokensAndDeclarations_RendersEachAsTheDefinitionSays` (:28-35) also pins that typed declarations add lines.  Every kill (208) and all 13 timeouts are among those of the two named covers.  No comment or document names the test.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-parsing`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-06/generator-parsing/before --tests 'FullyQualifiedName~SqlSource.Tests.Parsing.' 'Parsing/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-06/generator-parsing/after --tests 'FullyQualifiedName~SqlSource.Tests.Parsing.' 'Parsing/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-06/generator-parsing/before/reports/mutation-report.json artifacts/test-cleanup/phase-06/generator-parsing/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-06/generator-parsing/before/reports/mutation-report.json SqlLexerTests.Lex_DollarQuote_EndsAtTheSameTag
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-06/generator-parsing/before/reports/mutation-report.json SqlLexerTests.Lex_QuotedRegion_EndsAtTheUndoubledQuote
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-06/generator-parsing/before/reports/mutation-report.json SqlLexerTests.Lex_EscapeString_TreatsBackslashAsAnEscape
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-06/generator-parsing/before/reports/mutation-report.json SqlLexerTests.Lex_EscapeStringWithDoubledQuote_EndsAtTheUndoubledQuote
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-06/generator-parsing/before/reports/mutation-report.json SqlLexerTests.Lex_LetterEAtTheEndOfAnIdentifier_IsNotAnEscapeStringPrefix
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-06/generator-parsing/before/reports/mutation-report.json SqlModelTests.SqlParseError_Create_HoldsItsArguments
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-06/generator-parsing/before/reports/mutation-report.json SqlQueryHashTests.Compute_AnyQuery_IsSixtyFourLowerCaseHexCharacters
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-06/generator-parsing/before/reports/mutation-report.json SqlQueryHashTests.Compute_ParamMarkerWithAType_ChangesTheHash
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-parsing`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-06/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-06/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-06/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-06/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-06/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-06/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tests.Parsing-2-13, F-SqlSource.Tests.Parsing-2-14, F-SqlSource.Tests.Parsing-2-15.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tests.Parsing-2-13 | delete | `SqlModelTests.SqlParseError_Create_HoldsItsArguments` | `tests/SqlSource.Tests/Parsing/Markers/SqlGeneratorParameterScopeTests.cs`: `SqlGeneratorParameterScopeTests.Read_OneOptionInTwoStates_ConflictsAtTheLater`; `tests/SqlSource.Tests/Parsing/Tokens/TokenScannerTests.cs`: `TokenScannerTests.Scan_ReservedNameWithADefault_IsAnError` |
| F-SqlSource.Tests.Parsing-2-14 | delete | `SqlQueryHashTests.Compute_AnyQuery_IsSixtyFourLowerCaseHexCharacters` | `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs`: `SqlQueryHashTests.Compute_SimpleQuery_IsTheSha256OfItsInput` |
| F-SqlSource.Tests.Parsing-2-15 | delete | `SqlQueryHashTests.Compute_ParamMarkerWithAType_ChangesTheHash` | `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs`: `SqlQueryHashTests.Compute_ChangeToWhatIsDescribed_GivesAnotherHash`; `tests/SqlSource.Tests/Parsing/SqlQueryHashTests.cs`: `SqlQueryHashTests.Compute_BareParamMarker_IsTheHashWithoutIt` |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md`: 25
- `src/SqlSource/Parsing/AGENTS.md`: 11
- `tools/README.md`: 176, 194

## Left out

- F-SqlSource.Tests.Parsing-2-18 (keep, `SqlModelTests.SqlParseError_SameValuesInSeparateInstances_AreEqual`): a **keep**; the test stays as it is.
- F-SqlSource.Tests.Parsing-2-19 (keep, `SqlModelTests.SqlBlock_SameValuesInSeparateInstances_AreEqual`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
