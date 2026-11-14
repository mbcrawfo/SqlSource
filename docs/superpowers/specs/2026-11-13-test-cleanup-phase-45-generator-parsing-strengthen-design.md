# Test cleanup phase 45: tests/SqlSource.Tests/Parsing, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Parsing`
- Findings: 6 (6 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 48
- Guard: mutation (generator-parsing)

## Files

- `tests/SqlSource.Tests/Parsing/Quoting/BracketReaderTests.cs`
- `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs`
- `tests/SqlSource.Tests/Parsing/SqlFileParserMarkerTests.cs`
- `tests/SqlSource.Tests/Parsing/Tokens/OpenDefaultFinderTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Parsing-1-01

- test: `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs` | `SqlDialectNameTests.TryParse_NameWithOptions_GivesTheDialectAndItsOptions` | rows: all
- action: strengthen
- evidence: measured (10 rows, kills 21, alone 0, rows needed 1; survivor src/SqlSource/Parsing/SqlDialectName.cs:97:13 OrAssignmentExpression to ExclusiveOrAssignmentExpression mutation, covered by 8 methods); read (the comment at SqlDialectNameTests.cs:46 promises "one that is repeated counts once", but the only repeating row, `"mysql,ansi-quotes,ansi_quotes,ANSI-QUOTES"` at :48, gives the option three times, an odd count, so `options ^= option` at SqlDialectName.cs:97 still leaves it set and the row passes); blind: no
- cover: none
- proposal: Add a row that gives one option exactly twice, such as `("mysql,ansi-quotes,ansi_quotes", "MySql", "AnsiQuotes")`, so that toggling instead of setting the flag fails.
- verdict: confirmed
- verifier's reason: `kills` in generator-parsing lists `SURVIVED src/SqlSource/Parsing/SqlDialectName.cs:97:13 ... options ^= option` under the test.  The one row that repeats an option, `"mysql,ansi-quotes,ansi_quotes,ANSI-QUOTES"` at tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs:48, gives `AnsiQuotes` three times, so XOR leaves it set and `ToString()` is still `AnsiQuotes`; the other rows (:42-54) give no option twice.  The comment at :46 promises "one that is repeated counts once".  The proposed row, `ansi-quotes,ansi_quotes`, gives the option twice, which leaves `None` under the mutant and fails at :60.
- survivors to kill: `src/SqlSource/Parsing/SqlDialectName.cs:97:13`
- a cover: `SqlDialectNameTests.TryParse_NameWithOptions_GivesTheDialectAndItsOptions` is the cover of F-SqlSource.Tests.Generator-2-04 (phase 19).  The change keeps every assertion and row that makes it so.

### F-SqlSource.Tests.Parsing-1-04

- test: `tests/SqlSource.Tests/Parsing/SqlFileParserMarkerTests.cs` | `SqlFileParserMarkerTests.Parse_ColumnMarkerWithoutANameAndANullability_IsInvalid` | rows: all
- action: strengthen
- evidence: measured (21 rows, kills 166, alone 7, rows needed 9; survivors src/SqlSource/Parsing/Markers/SqlMarkerScope.cs:826:22 Equality mutation -> `index + 1 <= inner.Length` and src/SqlSource/Parsing/Markers/SqlMarkerScope.cs:826:22 Arithmetic mutation -> `index - 1`, each covered by this test and `Parse_ColumnMarker_GivesItsColumnANullability`); read (the rows with an undoubled quote in a quoted name, SqlFileParserMarkerTests.cs:731-734, never put it at the last character inside the quotes, where SqlMarkerScope.cs:826 is the bound that turns it into SQLSRC111 instead of an index out of range); blind: no
- cover: none
- proposal: Add a row whose quoted name ends in an undoubled quote just before its closing one, such as `"\"total\"\" null"`, which is SQLSRC111 today and throws under either mutant.
- verdict: confirmed
- verifier's reason: In src/SqlSource/Parsing/Markers/SqlMarkerScope.cs:826, `index + 1 < inner.Length` guards `inner[index + 1]`.  The quote rows at tests/SqlSource.Tests/Parsing/SqlFileParserMarkerTests.cs:730-734 never put an undoubled quote at the last index of `inner`.  For example, in `"to"tal"` the quote sits at index 2 of 6, and both mutants still read a `t` there and return null.  For `"total""`, `inner` is `total"`: the original returns null (SQLSRC111), and both survivors (`<=`, and `index - 1` at col 22, with `inner[index + 1]` unchanged) read `inner[6]` and throw.  `kills` confirms both survive under the test.
- survivors to kill: `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs:826:22`

### F-SqlSource.Tests.Parsing-1-05

- test: `tests/SqlSource.Tests/Parsing/SqlFileParserMarkerTests.cs` | `SqlFileParserMarkerTests.Parse_ColumnMarker_GivesItsColumnANullability` | rows: all
- action: strengthen
- evidence: measured (11 rows, kills 180, alone 7, rows needed 3; survivor src/SqlSource/Parsing/Markers/SqlMarkerScope.cs:812:13 Equality mutation -> `written.Length <= 3`, covered by 4 methods); read (the quoted names of the rows, SqlFileParserMarkerTests.cs:669-672, all hold more than one character, so a bound at SqlMarkerScope.cs:812 that refuses a quoted name of one character, `"a"`, passes); blind: no
- cover: none
- proposal: Add a row with a quoted name of one character, such as `("\"a\" null", "a:null")`.
- verdict: confirmed
- verifier's reason: src/SqlSource/Parsing/Markers/SqlMarkerScope.cs:812 refuses a quoted name shorter than 3 characters.  The quoted rows at tests/SqlSource.Tests/Parsing/SqlFileParserMarkerTests.cs:669-672 are all 5 characters or more, so `<= 3` survives (`kills`: SURVIVED 812:13).  `"a"` is exactly 3 characters: the original gives `a`, and the mutant returns null, which makes the marker invalid and fails the row.
- survivors to kill: `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs:812:13`

### F-SqlSource.Tests.Parsing-1-06

- test: `tests/SqlSource.Tests/Parsing/SqlFileParserMarkerTests.cs` | `SqlFileParserMarkerTests.Parse_InvalidLibraryTypes_IsSQLSRC111` | rows: all
- action: strengthen
- evidence: measured (7 rows, kills 126, alone 2, rows needed 2; survivor src/SqlSource/Parsing/Markers/SqlMarkerScope.cs:448:17 Statement mutation, covered by this test alone); read (the test asserts a single error, and SqlMarkerScope.cs:422-424 says an invalid entry is reported once for the marker and the entries after it are not read, but every row, SqlFileParserMarkerTests.cs:353-359, holds one entry, so removing the `return` after the first invalid entry changes nothing a row can see; the survivors at :497-500 are the help's wording and are not part of this finding); blind: no
- cover: none
- proposal: Add a row with two invalid entries, such as `"nodatime=maybe json=maybe"`, which is one SQLSRC111 today and two without the `return`.
- verdict: confirmed
- verifier's reason: The test checks `Errors(text).ShouldHaveSingleItem()` (tests/SqlSource.Tests/Parsing/SqlFileParserMarkerTests.cs:363), and each row at :353-359 holds at most one entry.  Without the `return` at src/SqlSource/Parsing/Markers/SqlMarkerScope.cs:448, the loop slices past the bad entry and adds a default library entry, which no one sees because a file with errors has no blocks.  So the mutant survives (`kills`: SURVIVED 448:17).  `AddInvalid` (:894) adds an error on every call, so `nodatime=maybe json=maybe` gives two errors under the mutant and one today, and the test's other assertions hold for that value.
- survivors to kill: `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs:448:17`

### F-SqlSource.Tests.Parsing-2-01

- test: `tests/SqlSource.Tests/Parsing/Quoting/BracketReaderTests.cs` | `BracketReaderTests.FindEnd_ClosedIdentifier_EndsAfterItsBracket` | rows: all
- action: strengthen
- evidence: measured (survivors src/SqlSource/Parsing/Quoting/BracketReader.cs:23:41 Equality mutation `close + 1 <= text.Length` and src/SqlSource/Parsing/Quoting/BracketReader.cs:23:41 Arithmetic mutation `close - 1`, covered by 9 methods, none killing); read (every row of the test, BracketReaderTests.cs:14-21, and of `FindEnd_DoubledCloserInSqlServer_StandsForOneBracket`, :29-32, has text after the closing bracket, so no row has a closed identifier that ends the text; both mutants index `text[text.Length]` there and throw, as a query ending in `[name]` would); blind: no
- cover: none
- proposal: Add a row whose identifier ends the text, `("[a]", "[a]")`, which reads it with the SQL Server reader as well as SQLite's and kills both survivors.
- verdict: confirmed
- verifier's reason: `kills` in generator-parsing lists `SURVIVED` BracketReader.cs:23:41 (both `close + 1 <= text.Length` and `close - 1`) under the test.  Every row of `FindEnd_ClosedIdentifier_EndsAfterItsBracket` and of `FindEnd_DoubledCloserInSqlServer_StandsForOneBracket` has text after the last `]` (BracketReaderTests.cs:14-21, :29-32).  Both rows of `FindEnd_IdentifierThatEndsInAnEscapedBracketInSqlServer_IsUnterminated` (:52-53) run out on `IndexOf` before they reach line 23 at the end of the text.  So for the SQL Server reader on `[a]`, the original returns 3 at BracketReader.cs:23-29.  Both mutants pass the bounds check, read `text[3]` and throw.  The proposed row kills both.
- survivors to kill: `src/SqlSource/Parsing/Quoting/BracketReader.cs:23:41`

### F-SqlSource.Tests.Parsing-2-05

- test: `tests/SqlSource.Tests/Parsing/Tokens/OpenDefaultFinderTests.cs` | `OpenDefaultFinderTests.At_Offset_GivesTheTokenWhoseDefaultIsOpenThereAndWhereItsOpenerStarts` | rows: all
- action: strengthen
- evidence: measured (survivor src/SqlSource/Parsing/Tokens/OpenDefaultFinder.cs:80:16 Conditional (true) mutation `(true?name :null)`, covered by 7 methods, none killing); read (`ReadOpener`, OpenDefaultFinder.cs:63-81, rejects a name that `SqlIdentifier.IsValid` refuses after its first character passes, which is a name holding a format character; the rows, OpenDefaultFinderTests.cs:51-62, have no such name, and the rule of `Expected`, :95-118, is written for ASCII names alone; `TokenScannerTests.Scan_TextThatIsNotExactlyAToken_IsLiteral` reads `{{a\u200B}}` as no token, TokenScannerTests.cs:56, so the finder must open no default for it); blind: no
- cover: none
- proposal: Add the row `("{{a\u200Bb:x ", 8, null, -1)`, a name with a zero-width space, which the mutant gives as an open default.
- verdict: confirmed
- verifier's reason: `TryReadOpener` takes any `SyntaxFacts.IsIdentifierPartCharacter` (TokenScanner.cs:162-181), which admits format characters.  `SqlIdentifier.IsValid` (src/SqlSource/SqlIdentifier.cs) refuses them.  OpenDefaultFinder.cs:80 is therefore the only place that refuses `a\u200Bb`, and `kills` lists `SURVIVED` OpenDefaultFinder.cs:80:16 `(true?name :null)` under the test.  The rows (OpenDefaultFinderTests.cs:51-62) and the regex rule of `Expected` (:120) are ASCII only.  The proposed row, which holds U+200B in the findings file, is 8 characters long.  The original answers `(null, -1)` and the mutant `("a\u200Bb", 0)`.  The test's own row `{{1:a ` (:53) already promises that an invalid name opens no default.
- survivors to kill: `src/SqlSource/Parsing/Tokens/OpenDefaultFinder.cs:80:16`
- condition: The proposal holds U+200B as a literal character: write it as `\u200B` in the test.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-parsing`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-45/generator-parsing/before --tests 'FullyQualifiedName~SqlSource.Tests.Parsing.' 'Parsing/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-45/generator-parsing/after --tests 'FullyQualifiedName~SqlSource.Tests.Parsing.' 'Parsing/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-45/generator-parsing/before/reports/mutation-report.json artifacts/test-cleanup/phase-45/generator-parsing/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-45/generator-parsing/before/reports/mutation-report.json SqlDialectNameTests.TryParse_NameWithOptions_GivesTheDialectAndItsOptions
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-45/generator-parsing/before/reports/mutation-report.json SqlFileParserMarkerTests.Parse_ColumnMarkerWithoutANameAndANullability_IsInvalid
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-45/generator-parsing/before/reports/mutation-report.json SqlFileParserMarkerTests.Parse_ColumnMarker_GivesItsColumnANullability
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-45/generator-parsing/before/reports/mutation-report.json SqlFileParserMarkerTests.Parse_InvalidLibraryTypes_IsSQLSRC111
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-45/generator-parsing/before/reports/mutation-report.json BracketReaderTests.FindEnd_ClosedIdentifier_EndsAfterItsBracket
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-45/generator-parsing/before/reports/mutation-report.json OpenDefaultFinderTests.At_Offset_GivesTheTokenWhoseDefaultIsOpenThereAndWhereItsOpenerStarts
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Parsing-1-01: `src/SqlSource/Parsing/SqlDialectName.cs:97:13`
- F-SqlSource.Tests.Parsing-1-04: `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs:826:22`
- F-SqlSource.Tests.Parsing-1-05: `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs:812:13`
- F-SqlSource.Tests.Parsing-1-06: `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs:448:17`
- F-SqlSource.Tests.Parsing-2-01: `src/SqlSource/Parsing/Quoting/BracketReader.cs:23:41`
- F-SqlSource.Tests.Parsing-2-05: `src/SqlSource/Parsing/Tokens/OpenDefaultFinder.cs:80:16`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-parsing`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-45/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-45/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-45/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-45/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-45/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-45/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- F-SqlSource.Tests.Parsing-1-13 (keep, `SqlDialectNameTests.TryParse_NameOrAlias_GivesItsDialect`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
