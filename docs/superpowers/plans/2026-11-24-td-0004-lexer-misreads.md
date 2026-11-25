# TD-0004 lexer misreads Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix three of TD-0004's misreads: a dialect rule for what ends a line, an option of `mysql` and `mariadb` that gives the server's version so a versioned comment is read as the server reads it, and a `-- variable:` marker that leaves a name out of a query's parameters.

**Architecture:** Each fix is a rule of `SqlDialectRules` or a marker, read by the lexer, the marker reader, the text builder and `SqlParameterList`, and mirrored by the sqlfluff templater in `src/sqlfluff-templater-sqlsource/`, whose corpus `TemplatingCorpusTests` regenerates.

**Tech Stack:** C# (netstandard2.0 generator, xUnit with Shouldly), Python 3 templater (pytest, ruff), docfx manual in `site/`.

**Spec:** `docs/tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md`, proposed fixes 1, 3 and 4.  Fix 2 (SQLite's open comment) stays as it is.

## Global Constraints

- Root `AGENTS.md`, `src/SqlSource/Parsing/AGENTS.md`, `src/sqlfluff-templater-sqlsource/AGENTS.md`, `site/AGENTS.md` and `docs/tech-debt/AGENTS.md` are binding.
- Validate with `./pre-commit-validation.sh` after `./format.sh`, through a login shell (`zsh -lc`).
- A plan change regenerates the corpus with `SQLSOURCE_UPDATE_TEMPLATER_CORPUS=1`; never edit `tests/corpus/` by hand.
- User docs state effects only, and never name a TD id.

## Facts measured on real servers (2026-11-24, `mysql:8.4`, `mariadb:11.4`, Python's SQLite)

These decide the rules; the code comments cite them, not this plan.

- MySQL, MariaDB and SQLite end a `--` comment (and `#` in MySQL and MariaDB) only at `\n`: `SELECT 1 -- c\r, 2\n` returns one column on all three.  A `\r\n` pair ends the line at the `\r` as today.
- MySQL `/*!`: six digits followed by white space are a version `MMmmrr`; otherwise five or more digits give a version of the first five, `Mmmrr`, and the rest of the digits are body.  Fewer than five digits, or none, is no version: the body, digits included, is always run.
- MariaDB `/*!`: five digits, or six when a sixth digit follows, whatever comes next, are the version; a version from 50700 to 99999 is never run.  `/*M!` reads the same digits with no such exclusion.  Fewer than five digits is no version, always run.  Under `mysql`, `/*M!` is a plain comment.
- A body that is run is read as SQL: a string, a `--` or `#` comment to `\n`, and a `/* ... */` comment inside it do not end it, so `/*!50600 , 2 # x */\n , 3 */` is `, 2 , 3`.  A body that is not run ends at the first `*/`.

## Review Focus

1. A CRLF file under `mysql` must lex, build and hash exactly as today: pin `a -- c\r\nb` and a CRLF block's built SQL under `mysql`.
2. A lone `\r` inside a kept comment (`keep-comments`) under `mysql` must not become a line break in the built SQL, or SQL the server reads as a comment would run.
3. A versioned comment with no version option keeps today's reading, `Lex_UnterminatedQuoteOfADialect_ReportsTheOpeningDelimiter`'s `/*!50700` row included.
4. A versioned comment that runs and is left open (`/*!50600 , 2`) is `UnterminatedBlockComment` at its opener; an open string in its body is `UnterminatedQuote` at that quote.
5. A `-- variable:` name in a token default, and in a second spelling, is still left out.

---

### Task 1: What ends a line (fix 3), C#

**Files:**
- Modify: `src/SqlSource/Parsing/SqlDialectRules.cs`, `SqlLexer.cs`, `SqlTextBuilder.cs`, `Markers/SqlMarkerReader.cs`, and every caller of `SqlMarkerReader.Read` and `SqlTextBuilder.Build` (`SqlFileParser.cs`, `SqlDialectMarker.cs`, `Markers/SqlMarkerScope.cs`, `src/SqlSource.Tool/Bundles/BundleQueryFile.cs`, `Renaming/SqlRenamer.QueryFile.cs`, `Renaming/TokenCheck.Sql.cs`)
- Modify: `src/SqlSource/Parsing/AGENTS.md` (the `SqlText` rule)
- Test: `tests/SqlSource.Tests/Parsing/SqlDialectRulesTests.cs`, `SqlLexerTests.cs`, `SqlTextBuilderTests.cs`, `SqlFileParserMarkerTests.cs`

**Interfaces:**
- Produces: `SqlDialectRules.CarriageReturnEndsLine` (bool, true but for `MySql`, `MariaDb`, `Sqlite`, `CockroachDb` under every option set); `SqlDialectRules.IsLineTerminator(char)`; `SqlDialectRules.FindLineEnd(string text, int start, int end)`, which under `false` finds the first `\n` and stops before a `\r` just before it.  `SqlMarkerReader.Read(string, SqlLexeme, SqlDialectRules)` and `SqlTextBuilder.Build(string, SqlDialectRules, lexemes, start, end, keepComments)`.  `SqlText.FindLineEnd` and `IsLineTerminator` stay for the tool's bundles and Oracle's quote reader, whose dialects end a line at both.

- [ ] Tests first: a `Lex_LineEnd_IsReadByTheRulesOfTheDialect` theory (`a -- c\rb` is one comment to the end under `mysql`, `mariadb`, `sqlite`, `cockroachdb`, `#` too under `mysql`; `a -- c\r\nb` gives `Text:\r\nb` in every dialect); move the CockroachDB `\r` row of `Lex_StringInCockroachDb_IsReadByItsRules` out of the TD-0004 comment as a row of the dialect's reading (no continuation); a `SqlTextBuilder` test that a lone `\r` in text and in a kept comment stays a `\r` under `mysql` and a CRLF block builds the same SQL as under `ansi`; a marker test that `x\r-- name: A` is no marker under `mysql`.  Run, see them fail.
- [ ] Implement; the lexer's gap check uses `Rules.IsLineTerminator`.
- [ ] Run `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Parsing"`, then commit.

### Task 2: Server version option and versioned comments (fix 1), C#

Shape per the owner's answer to Q2 (recommended: `mysql,version=8.0.36`, the full `major.minor.patch`).

**Files:**
- Modify: `src/SqlSource/Parsing/SqlDialectChoice.cs` (add `int ServerVersion`, 0 for none, as `major * 10000 + minor * 100 + patch`), `SqlDialectName.cs` (parse and name the option; `Accepted` is unchanged), `SqlDialectRules.cs` (`For` caches the rules of a version; `VersionedComments` rule: `None`, `MySql`, `MariaDb`; `ServerVersion`), `SqlLexer.cs` (`ReadBlockComment` of a `/*!` or `/*M!` that runs reads its body as SQL)
- Modify: `schemas/settings-v1.schema.json` and its test, `SettingsFileSchemaTests`
- Test: `SqlDialectNameTests.cs`, `SqlLexerTests.cs`, `SqlDialectRulesTests.cs`, `SqlFileParserDialectTests.cs`

**Interfaces:**
- Produces: `SqlDialectChoice(SqlDialect Dialect, SqlDialectOptions Options, int ServerVersion = 0)`; `SqlDialectRules.RunsVersionedComment(string text, int start, out int bodyStart)`, true for a hint whose body the server runs, by the facts above.

- [ ] Tests first: `TryParse` of the option, alone and with others, any order and case, and its refusals (another dialect, a part missing or not a number, twice); a `Lex_VersionedComment_IsReadAsTheServerReadsIt` theory over the measured cases, for `mysql` and `mariadb` with and without a version; `/*! '*/' */` reads as one hint with no version (owner choice (a)); errors at the opener and at an open quote of the body.  Run, see them fail.
- [ ] Implement.  The body reader skips a quoted region by the dialect's reader, a `--` or `#` comment to `FindLineEnd`, and a `/* ... */` to its first `*/`; the first `*/` outside them ends the hint.
- [ ] Run the parsing and settings tests, commit.

### Task 3: The `-- variable:` marker (fix 4), C#

Shape per the owner's answer to Q1 (recommended: `-- variable: @n`, one name, inside a query only).

**Files:**
- Modify: `Markers/SqlMarkerKind.cs` (`Variable`), `Markers/SqlMarkerReader.cs` (`variable:`), `Markers/SqlMarkerScope.cs` (read it, `IsAllowedInQuery`), `SqlParameterList.cs` (leave the names out where `variables` are), `SqlFileParser.cs`, `src/SqlSource/Diagnostics/SqlDiagnostics.cs` and `site/diagnostics.md` for any new diagnostic
- Test: `SqlFileParserParameterTests.cs`, `SqlFileParserMarkerTests.cs`, `Marker_OfEveryKind_HasAKeywordAndAScope`

- [ ] Tests first: the name is left out of the list under `mysql`, `ansi` and `mssql`, in every spelling and in a token default; a malformed value is `SQLSRC111`; the other diagnostics the owner accepts; the marker in a header is `SQLSRC116`.  Run, see them fail.
- [ ] Implement, commit.

### Task 4: The templater

**Files:** `src/sqlfluff-templater-sqlsource/sqlfluff_templater_sqlsource/` `rules.py`, `text.py`, `lexer.py`, `text_builder.py`, `markers.py`, `dialects.py`, `settings.py` as each mirrors the C# changed; its tests; `tests/corpus/` regenerated by `TemplatingCorpusTests` with `SQLSOURCE_UPDATE_TEMPLATER_CORPUS=1`, with corpus cases for a lone `\r` under `mysql`, a versioned comment with a version, and a `-- variable:` marker.

- [ ] Port, regenerate, run `uv run --locked pytest` and the floor command of the templater's `AGENTS.md`, commit.

### Task 5: Documents

- `site/reference/dialects.md`: the version option in Options, and the rows of "What is still read differently" rewritten to what remains (no carriage-return row; the versioned-comment row only without the option).
- `site/reference/parameters.md`: the marker section, and the rows of names read as parameters.
- `site/reference/sql-files.md` marker list, `site/guides/sqlfluff.md`, `site/guides/troubleshooting.md` rows if any limit changes, `site/diagnostics.md`.
- `docs/architecture/generator.md` (lexemes and the rules of a dialect, markers), `docs/architecture/templater.md` if it lists mirrored modules, `docs/architecture/sqlserver-describer.md` line on TD-0004.
- `docs/tech-debt/TD-0004-…` rewritten to what remains, its row in `docs/tech-debt/README.md`; the comments in `SqlLexerTests.cs` and `SqlDeclaredVariables.cs` that cite it.
- `examples/`: a MySQL example only if an example project reads MySQL; otherwise none applies.
- Run `./format.sh`, `./pre-commit-validation.sh`, commit.
