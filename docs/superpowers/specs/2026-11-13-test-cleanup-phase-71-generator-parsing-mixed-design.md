# Test cleanup phase 71: tests/SqlSource.Tests/Parsing, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Parsing`
- Findings: 13 (4 delete, 3 fold, 5 strengthen, 1 loosen)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 126
- Guard: mutation (generator-parsing), with covers and run time for 5 findings

## Files

- `tests/SqlSource.Tests/Parsing/Markers/SqlGeneratorParameterScopeTests.cs`
- `tests/SqlSource.Tests/Parsing/Markers/SqlMarkerReaderTests.cs`
- `tests/SqlSource.Tests/Parsing/SqlFileParserParameterTests.cs`
- `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs`
- `tests/SqlSource.Tests/Parsing/SqlFileParserTokenTests.cs`
- `tests/SqlSource.Tests/Parsing/SqlListPlacesTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Parsing-1-08

- test: `tests/SqlSource.Tests/Parsing/SqlFileParserTokenTests.cs` | `SqlFileParserTokenTests.Parse_LineCommentThatHoldsTheClosingBraces_IsReported` | rows: all
- action: delete
- evidence: measured (1 row, kills 161, alone 0, timeouts under it 17; cover `Parse_CommentThatHoldsBracesInsideAnOpenDefault_IsReported`, which has 18 timeouts under it on the same code, so the timeouts are not lost); read (its text, a line comment holding the `}}` of `f`'s default and another token after it, SqlFileParserTokenTests.cs:150, is the theory's row at :159, `"SELECT 1 {{f:a -- note}}"`, joined with its row at :164, `"SELECT {{f:a -- }}\r\nORDER BY {{g:b}}\r"`, and it asserts the same error, kind, span and name; no AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names the test or its class, and its comment at :145-146 explains the rule rather than recording a regression); blind: yes (SqlFileParser.cs:452-458, the search for a hidden `}}`, partly does not compile)
- cover: `tests/SqlSource.Tests/Parsing/SqlFileParserTokenTests.cs` | `SqlFileParserTokenTests.Parse_CommentThatHoldsBracesInsideAnOpenDefault_IsReported`
- proposal: Delete the fact and move its comment above the theory, whose rows at :159 and :164 already prove it.
- verdict: confirmed
- verifier's reason: The fact (tests/SqlSource.Tests/Parsing/SqlFileParserTokenTests.cs:148-156) has a `-- note}}` comment inside f's default and a later token with a default.  It asserts one `TokenCrossesComment` at the comment, for f.  Theory row :159 (`-- note}}`) and row :164 (`-- }}` on the first line, then `ORDER BY {{g:b}}`) make the same assertion (:169-170).  Row :164's `\r\n` comes after the comment, so it does not move the comment's mapped offset.  `kills` shows that every kill and every one of the 17 timeouts of the fact is also under the theory.  The code is partly blind (SqlFileParser.cs:452-458), and the reading above shares its inputs.  The comment at :145-146 explains the rule and records nothing.  No AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred file names the test.  Its only mention is the TD-0021 plan, docs/superpowers/plans/2026-10-23-td-0021-comments-and-token-defaults.md:98, as a test to write.

### F-SqlSource.Tests.Parsing-1-09

- test: `tests/SqlSource.Tests/Parsing/SqlFileParserParameterTests.cs` | `SqlFileParserParameterTests.Parse_SqlServerNegatedListInAnAggregate_HasAMethodsError` | rows: all
- action: delete
- evidence: measured (1 row, kills 305, alone 0, timeouts under it 29; cover `Parse_SqlServerNegatedListInAnAggregateTwice_IsReportedAtEachUseAsWritten` alone, which has 38 timeouts under it on the same code); read (the cover's first use, `SUM(IIF(id NOT IN @ids, 1, 0))` at SqlFileParserParameterTests.cs:898, is the fact's `COUNT(IIF(id NOT IN @ids, 1, NULL))` at :869 under another aggregate, and it too parses through `Blocks`, so with no file error, and asserts the same `MethodErrors` entry, "inside an aggregate", at the list; no AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names the test, and its comment at :864-865 states the rule of the group rather than a record); blind: yes (SqlFileParser.cs:391-423, where the refused lists are found, does not compile under mutation)
- cover: `tests/SqlSource.Tests/Parsing/SqlFileParserParameterTests.cs` | `SqlFileParserParameterTests.Parse_SqlServerNegatedListInAnAggregateTwice_IsReportedAtEachUseAsWritten`
- proposal: Delete the fact and move its comment above the cover, or make its SQL a row of `Parse_SqlServerNegatedListWhereAnEmptyListIsRefused_NamesThePlace`, which has no plain aggregate row.
- verdict: confirmed
- verifier's reason: The fact (tests/SqlSource.Tests/Parsing/SqlFileParserParameterTests.cs:866-873) and the cover (:891-902) both parse through `Blocks` under SQL Server, which asserts no file errors.  Both assert the `MethodErrors` entry `NegatedListRefusedEmpty` "inside an aggregate" at the list.  The cover's first use, `SUM(IIF(id NOT IN @ids, 1, 0))`, differs from the fact's only in the aggregate word and the else value.  That COUNT is an aggregate is a row of the static `Words` table (src/SqlSource/Parsing/SqlListUses.Places.cs:196), which mutation cannot see.  It is still guarded by `SqlListPlacesTests` row `"SELECT COUNT(IIF(id NOT IN @a, 1, NULL)) FROM t"` → `UnderNot, InAggregate` (tests/SqlSource.Tests/Parsing/SqlListPlacesTests.cs:17).  `kills` shows that every kill and every one of the 29 timeouts of the fact is under the cover.  No required document names the test.  The TD-0106 plan names it only as a test to write.

### F-SqlSource.Tests.Parsing-2-11

- test: `tests/SqlSource.Tests/Parsing/Markers/SqlMarkerReaderTests.cs` | `SqlMarkerReaderTests.Read_MarkerWithoutAValue_HasAnEmptyValueSpan` | rows: all
- action: delete
- evidence: measured (1 row; kills 41, alone 0, timeouts under it 8; cover: `SqlGeneratorParameterScopeTests.Read_MarkerWithoutGeneratorParameters_IsAnError`); read (the row `("-- name:   ", "Name:")` of `Read_MarkerComment_ReturnsItsKindAndValue`, SqlMarkerReaderTests.cs:55, reads the text the value span covers, which is empty only when the span is, :190, for the same marker with one more blank; the row `("-- dialect:   ", "dialect:")` of `Describe_Marker_IsItsWordAndItsValue`, :170, branches on `ValueSpan.IsEmpty` itself; `Read_MarkerComment_ReturnsItsKindAndValue` has 9 timeouts under it; no document names the test); blind: no
- cover: `tests/SqlSource.Tests/Parsing/Markers/SqlMarkerReaderTests.cs` | `SqlMarkerReaderTests.Read_MarkerComment_ReturnsItsKindAndValue`; `tests/SqlSource.Tests/Parsing/Markers/SqlMarkerReaderTests.cs` | `SqlMarkerReaderTests.Describe_Marker_IsItsWordAndItsValue`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: `Markers` (SqlMarkerReaderTests.cs:181-192) prints the text that the value span covers.  `"Name:"` for `"-- name:   "` (:55) and for `"-- name:"` (:54) therefore holds only when the span has length 0, which is what `TextSpan.IsEmpty` is.  `Describe` branches on `ValueSpan.IsEmpty` itself (SqlMarkerReader.cs:98), and its row `"-- dialect:   "` (:170) expects the bare word.  The test's text `"-- name:  "` (:144) differs from the row at :55 by one blank.  Every kill and all 8 timeouts are among those of the two named covers.  No comment or document names the test.

### F-SqlSource.Tests.Parsing-2-12

- test: `tests/SqlSource.Tests/Parsing/Markers/SqlGeneratorParameterScopeTests.cs` | `SqlGeneratorParameterScopeTests.Parameters_OfANewScope_AreEmpty` | rows: all
- action: delete
- evidence: measured (1 row; kills 1, alone 0, timeouts under it 0; cover: `Read_DefaultWithAValue_IsInvalidWithNoValues`); read (`Read_ParameterWithAValueNotInItsList_IsInvalidAndNamesTheValues` and `Read_DefaultWithAValue_IsInvalidWithNoValues` read only words that apply nothing and assert `scope.Parameters.IsEmpty`, SqlGeneratorParameterScopeTests.cs:181, :196, which fails if a new scope is not empty; the one other mention of the test's name is a plan under docs/superpowers, outside the documents that record tests); blind: no
- cover: `tests/SqlSource.Tests/Parsing/Markers/SqlGeneratorParameterScopeTests.cs` | `SqlGeneratorParameterScopeTests.Read_ParameterWithAValueNotInItsList_IsInvalidAndNamesTheValues`; `tests/SqlSource.Tests/Parsing/Markers/SqlGeneratorParameterScopeTests.cs` | `SqlGeneratorParameterScopeTests.Read_DefaultWithAValue_IsInvalidWithNoValues`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: The test's one kill is SqlGeneratorParameterScope.cs:32:19 `(true?GeneratorParameterChoices.Reset :...)`, which makes a scope without `default` read as Reset.  `Read_ParameterWithAValueNotInItsList_IsInvalidAndNamesTheValues` and `Read_DefaultWithAValue_IsInvalidWithNoValues` read only words that apply nothing.  They assert `scope.Parameters.IsEmpty` (SqlGeneratorParameterScopeTests.cs:181, :196) and kill the same mutant, as the set comparison of their `kills` shows.  The only mention of the name is docs/superpowers/plans/2026-11-04-generator-parameters-merge.md:110, a plan and not a record document.

### F-SqlSource.Tests.Parsing-1-10

- test: `tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs` | `SqlFileParserTests.Parse_QueryTurningKeepCommentsOffUnderAChainThatKeepsThem_DropsTheComment`; `tests/SqlSource.Tests/Parsing/SqlFileParserTokenTests.cs` | `SqlFileParserTokenTests.Parse_KeptForm_IsBuiltOnlyWhenItMayBeWanted` | rows: all
- action: fold
- evidence: measured (2 rows, kills 235, alone 0, timeouts under it 14; cover `Parse_KeptForm_IsBuiltOnlyWhenItMayBeWanted` and `SqlFileParserAllocationTests.Parse_TypicalFile_AllocatesWithinItsBudget`); read (both parse with `commentsWanted: true` and assert that the SQL is `SELECT 1` and the kept form null; the row at SqlFileParserTests.cs:109, `keep-comments=off` on the query, is the theory's row at SqlFileParserTokenTests.cs:16, and only the row at :110, `-- generator: default` in the preamble, is a case the theory lacks, and the kill the allocation budget brings to the cover; no AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names either test); blind: no
- cover: `tests/SqlSource.Tests/Parsing/SqlFileParserTokenTests.cs` | `SqlFileParserTokenTests.Parse_KeptForm_IsBuiltOnlyWhenItMayBeWanted`
- proposal: Move the row at SqlFileParserTests.cs:110 into the theory as `("-- generator: default\n\n-- name: Q\nSELECT 1 -- c\n", true, null)` and delete `Parse_QueryTurningKeepCommentsOffUnderAChainThatKeepsThem_DropsTheComment`.
- verdict: confirmed
- verifier's reason: The fact (tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs:108-117) parses with `commentsWanted: true` and asserts `Sql == "SELECT 1"` and `Kept == null`, as the theory does through `Block` (SqlFileParserTokenTests.cs:33-39, 551-552).  Its row :109 is the theory's row :16.  `kills` shows two kills that the theory lacks, SqlLexer.cs:104:13 and SqlText.cs:28:16, both from row :110's blank line in the preamble.  The proposed theory row keeps that `\n\n`, so those kills move with it, and the allocation test kills them as well.  All 14 timeouts are under the theory.  The fact has no comment.  No required document names either test.  The TD-0109 plan only renamed the fact.

### F-SqlSource.Tests.Parsing-1-11

- test: `tests/SqlSource.Tests/Parsing/SqlFileParserParameterTests.cs` | `SqlFileParserParameterTests.Parse_DeclareUnderADialectWithoutDeclaredVariables_DeclaresNone`; `tests/SqlSource.Tests/Parsing/SqlFileParserParameterTests.cs` | `SqlFileParserParameterTests.Parse_DeclareUnderADialectWithoutDeclaredVariables_LeavesTheNameAParameter` | rows: all
- action: fold
- evidence: measured (`DeclaresNone`: 1 row, kills 124, alone 0, timeouts under it 13; cover `Parse_DeclareUnderADialectWithoutDeclaredVariables_LeavesTheNameAParameter` alone, which has 13 timeouts under it); read (both parse the same text, SqlFileParserParameterTests.cs:68 and :109, the fact under PostgreSQL alone and the theory under the seven dialects without declared variables; a block's `DeclaredVariables` and its parameter list come from the one `SqlDeclaredVariables.Find` result, SqlFileParser.cs:345-346 and :382, so the fact's assertion is the theory's on another property; no AGENTS.md, readme, CONTRIBUTING.md, docs/architecture, docs/tech-debt or docs/deferred names either test); blind: yes (the flag that decides, `DeclaredVariables` of SqlDialectRules.cs:80-82, is in a static initialiser)
- cover: `tests/SqlSource.Tests/Parsing/SqlFileParserParameterTests.cs` | `SqlFileParserParameterTests.Parse_DeclareUnderADialectWithoutDeclaredVariables_LeavesTheNameAParameter`
- proposal: Assert `DeclaredVariables.ShouldBeEmpty()` in the theory beside its parameters, for every dialect, and delete the fact.
- verdict: confirmed
- verifier's reason: Both tests parse `DECLARE @n int = @start;\nSELECT @n` (tests/SqlSource.Tests/Parsing/SqlFileParserParameterTests.cs:67 and :108).  The fact runs under PostgreSQL and the theory under all seven dialects without declared variables.  A block's `DeclaredVariables` (src/SqlSource/Parsing/SqlFileParser.cs:382) and its parameter list (:364-371) come from the one `variables` of :345-346.  `kills` shows that every kill and every one of the 13 timeouts of the fact is under the theory.  The fold adds `DeclaredVariables.ShouldBeEmpty()` to every row of the theory, so the fact's assertion is kept and widened.  No required document names either test.

### F-SqlSource.Tests.Parsing-2-16

- test: `tests/SqlSource.Tests/Parsing/SqlListPlacesTests.cs` | `SqlListPlacesTests.Find_SqlServerNotWithNoWordAfterIt_ReachesTheList`; `tests/SqlSource.Tests/Parsing/SqlListPlacesTests.cs` | `SqlListPlacesTests.Find_SqlServerWordThatStartsNoClauseAfterANot_EndsNothing` | rows: all
- action: fold
- evidence: measured (`Find_SqlServerNotWithNoWordAfterIt_ReachesTheList`: 1 row; kills 125, alone 0, timeouts under it 24; cover: `Find_SqlServer_ReadsWhereEachListStands`); read (the fact's body, SqlListPlacesTests.cs:416-425, is the theory's, :287-295, with the same expected `[SqlListPlace.UnderNot]`, and its text `WHERE NOT a = 1 b IN @a` is the theory's rows less the word between, :279-286; the theory has 24 timeouts under it too; no document names either test); blind: no
- cover: `tests/SqlSource.Tests/Parsing/SqlListPlacesTests.cs` | `SqlListPlacesTests.Find_SqlServerWordThatStartsNoClauseAfterANot_EndsNothing`
- proposal: Move `"WHERE NOT a = 1 b IN @a"` into the theory as its first row, with its comment, and delete the fact.
- verdict: confirmed
- verifier's reason: The fact's body (SqlListPlacesTests.cs:416-425) is the theory's (:287-295) line for line, with the same expected `[SqlListPlace.UnderNot]`.  Moving its text into the theory as a row keeps the same assertion on the same input, so the fold drops nothing.  Its 24 timeouts are all under the theory too.  One kill, SqlLexer.cs:73:16 `!(Position < text.Length && _error is null)`, is not credited to the theory, though the theory must fail on it: no lexemes means no uses, and places of `[]` against `[UnderNot]`.  I read this as an attribution gap of the report.  `Find_SqlServer_ReadsWhereEachListStands` and 283 other methods kill the same mutant.  No document names either test.

### F-SqlSource.Tests.Parsing-1-02

- test: `tests/SqlSource.Tests/Parsing/SqlFileParserTokenTests.cs` | `SqlFileParserTokenTests.Parse_TwoDefaultsForOneTokenThatDiffer_ConflictAtTheSecond` | rows: all
- action: strengthen
- evidence: measured (4 rows, kills 284, alone 11; survivors src/SqlSource/Parsing/Tokens/SqlTokenList.cs:128:17 Logical mutation and src/SqlSource/Parsing/Tokens/SqlTokenList.cs:125:56 PostIncrementExpression to PostDecrementExpression mutation, each covered by this test alone); read (every row has one token, so `FirstWithDefault` at SqlTokenList.cs:123-136 always finds its token at index 0: its name test can go, or its index run backwards, and the conflicts between a marker and an inline default, rows at SqlFileParserTokenTests.cs:430 and :432, are still placed right); blind: no
- cover: none
- proposal: Add a row whose SQL has another token with a default before the one in conflict, such as `"-- name: Q\n-- token: {{a:x}}\nSELECT {{b:z}} {{a:y}}\n", "{{a:y}}", "{{a:y}}"`, so that a conflict placed at the first token with a default, rather than at the first of its own name, fails.
- verdict: confirmed
- verifier's reason: `FirstWithDefault` at src/SqlSource/Parsing/Tokens/SqlTokenList.cs:123-137 is called only from the marker branch (:80), so only the rows at tests/SqlSource.Tests/Parsing/SqlFileParserTokenTests.cs:430 and :432 reach it, and each holds one occurrence, which is at index 0.  `kills` lists `SURVIVED` for SqlTokenList.cs:128:17 Logical (`||`) and :125:56 `index--`.  In the proposed row, `{{b:z}}` comes first and has a default, so the `||` mutant returns b and puts the conflict at `{{b:z}}`, and the `index--` mutant reads index -1 and throws.  Both fail the row.
- survivors to kill: `src/SqlSource/Parsing/Tokens/SqlTokenList.cs:125:56`, `src/SqlSource/Parsing/Tokens/SqlTokenList.cs:128:17`

### F-SqlSource.Tests.Parsing-1-03

- test: `tests/SqlSource.Tests/Parsing/SqlFileParserTokenTests.cs` | `SqlFileParserTokenTests.Parse_CommentInTheHeadOfAnIgnoredToken_IsAnErrorOfTheKeptForm` | rows: all
- action: strengthen
- evidence: measured (3 rows, kills 300, alone 6, rows needed 2; survivors src/SqlSource/Parsing/SqlFileParser.cs:588:25 Conditional (false) mutation and src/SqlSource/Parsing/SqlFileParser.cs:588:25 Equality mutation -> `next > sql.Text.Length`, each covered by 4 methods); read (the loop at SqlFileParser.cs:572-589 walks every `{{` of the SQL to find the ignored tokens, but in each row, SqlFileParserTokenTests.cs:335-337, the ignored `f` is the first token, so a loop that stops after the first token still finds it); blind: no
- cover: none
- proposal: Add a row where the ignored token with a comment in its head follows another token, such as `"SELECT {{h}}, {{f/* c */:x}}", new[] { "h" }, new[] { "h" }`, so that a scan that stops after the first token fails.
- verdict: confirmed
- verifier's reason: The loop at src/SqlSource/Parsing/SqlFileParser.cs:572-589 walks every `{{` to find ignored tokens.  In every row at tests/SqlSource.Tests/Parsing/SqlFileParserTokenTests.cs:335-337 the ignored `f` is the SQL's first `{{`, so a loop that stops after the first token loses nothing.  `kills` lists `SURVIVED` for 588:25 Conditional (false) and Equality (`next > sql.Text.Length`), both of which set index to -1 after the first token.  In the proposed row `SELECT {{h}}, {{f/* c */:x}}`, h is read first and is not ignored, so both mutants miss f and leave `KeptErrors` empty.  The row at :337 already shows that this shape parses with no other error.
- survivors to kill: `src/SqlSource/Parsing/SqlFileParser.cs:588:25`

### F-SqlSource.Tests.Parsing-2-02

- test: `tests/SqlSource.Tests/Parsing/Markers/SqlMarkerReaderTests.cs` | `SqlMarkerReaderTests.Read_AnythingElse_IsNotAMarker` | rows: all
- action: strengthen
- evidence: measured (survivor src/SqlSource/Parsing/Markers/SqlMarkerReader.cs:139:16 Equality mutation `index <= 0`, covered by 89 methods, none killing); read (`StartsLine`, SqlMarkerReader.cs:131-140: the mutant takes a comment as starting its line when one non-blank character stands before it at the start of the text; the rows that put SQL before the comment, SqlMarkerReaderTests.cs:70-73, 98-99, 102, all put two or more characters before it); blind: yes (SqlMarkerReader.cs:134 does not compile under some mutants)
- cover: none
- proposal: Add the row `"a-- name: A"`, a comment after one character at the start of the text, which is not a marker and kills the survivor.
- verdict: confirmed
- verifier's reason: `kills` lists `SURVIVED` SqlMarkerReader.cs:139:16 `index <= 0` under the test.  `StartsLine` (SqlMarkerReader.cs:131-140) ends at `index == 0` only when the text's first character is not a blank and only blanks stand between it and the comment.  The original then gives false, unless that character is a line terminator, and the mutant gives true.  The rows that put SQL before the comment put it further away: `SELECT 1 -- name: A` stops at 7, `/* x */ -- name: A` at 6, `'x' -- name: A` at 2 and `/*+ h */-- name: A` at 7 (SqlMarkerReaderTests.cs:70-73, 98-99, 102).  `"a-- name: A"` lexes as text `a` and a line comment at 1.  The mutant reads that comment as a marker, which fails `ShouldBeEmpty`.
- survivors to kill: `src/SqlSource/Parsing/Markers/SqlMarkerReader.cs:139:16`

### F-SqlSource.Tests.Parsing-2-03

- test: `tests/SqlSource.Tests/Parsing/Markers/SqlGeneratorParameterScopeTests.cs` | `SqlGeneratorParameterScopeTests.Read_OneStateTwice_IsNotAConflict` | rows: all
- action: strengthen
- evidence: measured (survivor src/SqlSource/Parsing/Markers/SqlGeneratorParameterScope.cs:98:13 OrAssignmentExpression to ExclusiveOrAssignmentExpression mutation `_defaulted ^= parameter`, covered only by `Read_OneOptionInTwoStates_ConflictsAtTheLater` and `Read_Words_NameTheirStates`); read (the test gives the state on three times, SqlGeneratorParameterScopeTests.cs:109, and never the state default twice; with the mutant a second `keep-comments=default` clears the default it set, so the scope ends with no state for the parameter and no error); blind: yes (SqlGeneratorParameterScope.cs:67-102 does not compile under some mutants)
- cover: none
- proposal: Make the test a theory and add the row `-- generator: keep-comments=default keep-comments=default`, expecting no error and `GeneratorParameterChoices.None.With(GeneratorParameters.KeepComments, GeneratorParameterState.Default)`.
- verdict: confirmed
- verifier's reason: SqlGeneratorParameterScope.cs:84-99.  A second `keep-comments=default` has no written value, and the conflict test `defaulted && value is not null` is false for it.  The mutant's `_defaulted ^= parameter` therefore clears the bit that the first word set, so `Parameters` (:29-32) lacks the default.  The test gives only the state `on` three times (SqlGeneratorParameterScopeTests.cs:109).  No row in the file gives `default` twice: `Read_OneOptionInTwoStates_ConflictsAtTheLater` pairs `default` only with another state (:87, :89, :91).  `kills` confirms the survivor is reached only by the two tests that the finding names.  `GeneratorParameterChoices.With(parameters, GeneratorParameterState)` handles `Default` (src/SqlSource/Settings/GeneratorParameterChoices.cs:34-48), so the proposed expectation can be written.
- survivors to kill: `src/SqlSource/Parsing/Markers/SqlGeneratorParameterScope.cs:98:13`

### F-SqlSource.Tests.Parsing-2-04

- test: `tests/SqlSource.Tests/Parsing/SqlListPlacesTests.cs` | `SqlListPlacesTests.Find_SqlServer_ReadsWhereEachListStands` | rows: the aggregate rows of `Queries`, SqlListPlacesTests.cs:15-37
- action: strengthen
- evidence: measured (survivors src/SqlSource/Parsing/SqlListUses.Places.cs:451:21 Statement mutation and src/SqlSource/Parsing/SqlListUses.Places.cs:451:21 PostIncrementExpression to PostDecrementExpression mutation, covered only by this test, `Find_WithPlaces_AllocatesThePlacesAlone` and `Find_WithPlaces_GivesTheUsesWithout`); read (`PassParentheses`, SqlListUses.Places.cs:444-460, counts the depth of the call's parentheses before `IsWindowed` looks for OVER; the group's comment promises "unless OVER follows the call", but no row that ends in OVER holds a parenthesis inside the call, :18, :20, :26, :112, :117, so the depth is never needed); blind: no
- cover: none
- proposal: Add the row `{ "SELECT SUM(CASE WHEN s IN @a THEN (1) END) OVER () FROM t", ["None"] }`, which the mutants read as `InAggregate`.
- verdict: confirmed
- verifier's reason: `PassParentheses` (SqlListUses.Places.cs:444-460) needs `depth` only when the call holds a nested `(`.  With either mutant at :451 it stops at the inner `)` or never reaches 0.  On a call with nested parentheses and no OVER, as at SqlListPlacesTests.cs:17 and :33, both answers are "not windowed", the same as the original.  The windowed rows (:18-20, :26, :112, :117, :180) hold no parenthesis inside the call or inside its WITHIN GROUP.  So no row tells the mutants apart, and `kills` lists both as `SURVIVED` under the theory.  On the proposed row `SUM(CASE WHEN s IN @a THEN (1) END) OVER ()`, both mutants answer "not windowed" and give `InAggregate` where the original gives `None`.
- survivors to kill: `src/SqlSource/Parsing/SqlListUses.Places.cs:451:21`

### F-SqlSource.Tests.Parsing-1-07

- test: `tests/SqlSource.Tests/Parsing/SqlFileParserTokenTests.cs` | `SqlFileParserTokenTests.Parse_CommentInTheGapOfAContinuedStringInADefault_IsReported` | rows: all
- action: loosen
- evidence: read (SqlFileParserTokenTests.cs:191-203 assert all eight lexemes of the text, with each text lexeme's exact extent, before the one assertion the name promises at :204-205; a change to how the lexer splits plain text, or to which lexeme holds the `E` prefix, breaks the test while the error is still reported where it is; what PostgreSQL's lexer makes of a continued string is pinned in `SqlLexerTests`, such as `Lex_EscapeStringOfPostgreSqlThatGoesOnAfterAGap_ReadsEachPartWithBackslashEscapes`); blind: no
- cover: none
- proposal: Drop the lexeme listing and keep the assertion on the error, or, to keep the premise in the comment, assert only that the `-- }}` is a line comment lexeme between two quoted ones.
- verdict: confirmed
- verifier's reason: tests/SqlSource.Tests/Parsing/SqlFileParserTokenTests.cs:191-203 pins all eight lexemes, with the extent of each text lexeme, before the one assertion the name promises (:204-205).  If the lexer swallowed the `-- }}` into a string, the default would close at it and that error assertion would fail anyway.  The listing therefore guards nothing that the error does not, and it breaks on any re-split of plain text.  The same split, a line comment in the gap between two quoted parts with `E` in the text before, is already pinned by `SqlLexerTests.Lex_EscapeStringOfPostgreSqlThatGoesOnAfterAGap_ReadsEachPartWithBackslashEscapes` (tests/SqlSource.Tests/Parsing/SqlLexerTests.cs:391-394, row `"E'a' -- c\n'b\\'d' x"`).  No required document names the test.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-parsing`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-71/generator-parsing/before --tests 'FullyQualifiedName~SqlSource.Tests.Parsing.' 'Parsing/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-71/generator-parsing/after --tests 'FullyQualifiedName~SqlSource.Tests.Parsing.' 'Parsing/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-71/generator-parsing/before/reports/mutation-report.json artifacts/test-cleanup/phase-71/generator-parsing/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-71/generator-parsing/before/reports/mutation-report.json SqlFileParserTokenTests.Parse_TwoDefaultsForOneTokenThatDiffer_ConflictAtTheSecond
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-71/generator-parsing/before/reports/mutation-report.json SqlFileParserTokenTests.Parse_CommentInTheHeadOfAnIgnoredToken_IsAnErrorOfTheKeptForm
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-71/generator-parsing/before/reports/mutation-report.json SqlFileParserTokenTests.Parse_CommentInTheGapOfAContinuedStringInADefault_IsReported
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-71/generator-parsing/before/reports/mutation-report.json SqlFileParserTokenTests.Parse_LineCommentThatHoldsTheClosingBraces_IsReported
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-71/generator-parsing/before/reports/mutation-report.json SqlFileParserParameterTests.Parse_SqlServerNegatedListInAnAggregate_HasAMethodsError
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-71/generator-parsing/before/reports/mutation-report.json SqlFileParserTests.Parse_QueryTurningKeepCommentsOffUnderAChainThatKeepsThem_DropsTheComment
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-71/generator-parsing/before/reports/mutation-report.json SqlFileParserTokenTests.Parse_KeptForm_IsBuiltOnlyWhenItMayBeWanted
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-71/generator-parsing/before/reports/mutation-report.json SqlFileParserParameterTests.Parse_DeclareUnderADialectWithoutDeclaredVariables_DeclaresNone
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-71/generator-parsing/before/reports/mutation-report.json SqlFileParserParameterTests.Parse_DeclareUnderADialectWithoutDeclaredVariables_LeavesTheNameAParameter
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-71/generator-parsing/before/reports/mutation-report.json SqlMarkerReaderTests.Read_AnythingElse_IsNotAMarker
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-71/generator-parsing/before/reports/mutation-report.json SqlGeneratorParameterScopeTests.Read_OneStateTwice_IsNotAConflict
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-71/generator-parsing/before/reports/mutation-report.json SqlListPlacesTests.Find_SqlServer_ReadsWhereEachListStands
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-71/generator-parsing/before/reports/mutation-report.json SqlMarkerReaderTests.Read_MarkerWithoutAValue_HasAnEmptyValueSpan
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-71/generator-parsing/before/reports/mutation-report.json SqlGeneratorParameterScopeTests.Parameters_OfANewScope_AreEmpty
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-71/generator-parsing/before/reports/mutation-report.json SqlListPlacesTests.Find_SqlServerNotWithNoWordAfterIt_ReachesTheList
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-71/generator-parsing/before/reports/mutation-report.json SqlListPlacesTests.Find_SqlServerWordThatStartsNoClauseAfterANot_EndsNothing
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Parsing-1-02: `src/SqlSource/Parsing/Tokens/SqlTokenList.cs:125:56`, `src/SqlSource/Parsing/Tokens/SqlTokenList.cs:128:17`
- F-SqlSource.Tests.Parsing-1-03: `src/SqlSource/Parsing/SqlFileParser.cs:588:25`
- F-SqlSource.Tests.Parsing-2-02: `src/SqlSource/Parsing/Markers/SqlMarkerReader.cs:139:16`
- F-SqlSource.Tests.Parsing-2-03: `src/SqlSource/Parsing/Markers/SqlGeneratorParameterScope.cs:98:13`
- F-SqlSource.Tests.Parsing-2-04: `src/SqlSource/Parsing/SqlListUses.Places.cs:451:21`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-parsing`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-71/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-71/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-71/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-71/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-71/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-71/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tests.Parsing-1-08, F-SqlSource.Tests.Parsing-1-09, F-SqlSource.Tests.Parsing-1-11, F-SqlSource.Tests.Parsing-2-02, F-SqlSource.Tests.Parsing-2-03.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tests.Parsing-1-08 | delete | `SqlFileParserTokenTests.Parse_LineCommentThatHoldsTheClosingBraces_IsReported` | `tests/SqlSource.Tests/Parsing/SqlFileParserTokenTests.cs`: `SqlFileParserTokenTests.Parse_CommentThatHoldsBracesInsideAnOpenDefault_IsReported` |
| F-SqlSource.Tests.Parsing-1-09 | delete | `SqlFileParserParameterTests.Parse_SqlServerNegatedListInAnAggregate_HasAMethodsError` | `tests/SqlSource.Tests/Parsing/SqlFileParserParameterTests.cs`: `SqlFileParserParameterTests.Parse_SqlServerNegatedListInAnAggregateTwice_IsReportedAtEachUseAsWritten` |
| F-SqlSource.Tests.Parsing-1-11 | fold | `SqlFileParserParameterTests.Parse_DeclareUnderADialectWithoutDeclaredVariables_DeclaresNone`, `SqlFileParserParameterTests.Parse_DeclareUnderADialectWithoutDeclaredVariables_LeavesTheNameAParameter` (rows: `SqlFileParserParameterTests.Parse_DeclareUnderADialectWithoutDeclaredVariables_LeavesTheNameAParameter` | rows: all) | `tests/SqlSource.Tests/Parsing/SqlFileParserParameterTests.cs`: `SqlFileParserParameterTests.Parse_DeclareUnderADialectWithoutDeclaredVariables_LeavesTheNameAParameter` |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
