# Dialect options, continued strings and CockroachDB - design

Date: 2026-10-06

Fixes what can be fixed of [`TD-0004`](../../tech-debt/TD-0004-lexer-misreads-dialect-specific-sql.md), and delivers the CockroachDB row of [`D-0001`](../../deferred/D-0001-dialects-not-delivered.md).  It builds on the [SQL dialects design](2026-10-06-sql-dialects-design.md), which put options that change one rule of a dialect out of scope.

## Goal

A file is read by the rules of its dialect, and six constructs are still read differently from the database they are written for.  No user has reported one; the owner asked for the item to be reviewed and fixed ahead of its trigger.

After this change four of the six are read as the database reads them:

```xml
<PropertyGroup>
    <SqlSourceDialect>mysql,ansi-quotes</SqlSourceDialect>
</PropertyGroup>
<ItemGroup>
    <AdditionalFiles Update="Legacy/**/*.sql" SqlSourceDialect="mariadb,no-backslash-escapes" />
    <AdditionalFiles Update="Ledger/**/*.sql" SqlSourceDialect="cockroachdb" />
</ItemGroup>
```

```sql
-- SqlSource: dialect=mysql,no-backslash-escapes

-- name: GetPath
SELECT 'C:\temp\' AS path;
```

```sql
-- SqlSource: dialect=postgres

-- name: GetNote
SELECT E'it\'s' -- the first part
       ' here\'';
```

Success is:

- SQL written for MySQL's and MariaDB's `ANSI_QUOTES` and `NO_BACKSLASH_ESCAPES` modes is read correctly when the option is set.
- A continued PostgreSQL `E'...'` string with `--` comments between its parts is read as one string, and the comments are stripped.
- CockroachDB's `b'\''` is read correctly under a dialect of its own.
- A project that sets no option and does not use `postgres` gets exactly the SQL it gets today.
- `TD-0004` describes only what still misreads, and `D-0001` no longer lists CockroachDB.

## Review of TD-0004

The item was checked against the code before the design.  Every row matches the lexer, and each test it names exists.  Two corrections came out of the primary sources:

- **The continued-string row is narrower than the item says.**  PostgreSQL allows only a `--` comment between the parts of a continued string.  `E'a' /* c */` followed by `'b'` on the next line is a syntax error there, so there is nothing to fix for a block comment.
- **The CockroachDB row has no fix in the item.**  It belongs to `D-0001` as a missing dialect, and this work delivers it.

## Decisions

Settled with the owner during the design, in this order.

| Decision | Choice | Why |
|----|----|----|
| Scope | Both fixes the item proposes, and a `cockroachdb` dialect | The owner's decision.  The versioned comment and SQLite's open comment stay as they are; the item calls both deliberate. |
| How an option is written | In the dialect value, after the name, separated by commas: `mysql,ansi-quotes` | One value in the directive, the metadata and the property.  Precedence stays directive, then metadata, then property, with nothing to merge and no new MSBuild name.  It mirrors the server's `sql_mode='ANSI_QUOTES,NO_BACKSLASH_ESCAPES'`.  A separate setting and a dialect name for each combination were considered. |
| How a continued string is read | Each part is its own quoted region, and the lexer carries the continuation from one part to the next | The gap is then ordinary text and comments: the comments are stripped, and a marker there is still seen.  Letting the reader skip comments inside one region was considered: the comments would never be stripped, and the reader would have to know marker syntax. |
| Option spellings | `ansi-quotes` and `no-backslash-escapes`, and the server's `ansi_quotes` and `no_backslash_escapes` as aliases | The directives of this project are written with hyphens.  People will copy the server's spelling from `sql_mode`. |
| A marker in the gap of a continued string | It is a marker | A `-- name:` line is a marker wherever a comment can be.  The part after it is still read with backslash escapes, so both halves are read correctly.  This is not listed as a misread. |
| A bare `\r` under `cockroachdb` | Left, and recorded in `TD-0004` | CockroachDB ends a `--` comment and counts a line break only at `\n`.  SqlSource takes `\r` as a line break in every dialect.  It matters only for a file with classic Mac line endings. |

## Out of scope

- The versioned comment `/*!50700 ... */` whose body holds `*/` in a string, and SQLite's block comment that is open at the end of the file.
- MySQL's combination modes, such as `ANSI`, which turns on `ANSI_QUOTES` among others.
- Options for any dialect but `mysql` and `mariadb`.
- The other engines of `D-0001`.
- Merging options across the directive, the metadata and the property.

## What a user sees

### Values

A value is a dialect name, then any options of that dialect, separated by commas.

| Value | Also accepted | Use it for |
|----|----|----|
| `cockroachdb` | `cockroach` | CockroachDB |

| Option | Also accepted | Dialects | What it changes |
|----|----|----|----|
| `ansi-quotes` | `ansi_quotes` | `mysql`, `mariadb` | `"..."` is a quoted identifier, and a backslash does not escape in it |
| `no-backslash-escapes` | `no_backslash_escapes` | `mysql`, `mariadb` | A backslash does not escape in `'...'` or in `"..."` |

- Each part is trimmed and compared ignoring case.  Options come in any order, and one that is repeated is accepted.
- The value is replaced whole.  A file's `dialect=mysql` under a project's `mysql,ansi-quotes` is plain `mysql`.
- In a directive the value is one word: `dialect=mysql,ansi-quotes`, with no space.  In MSBuild, whitespace around a part is ignored.

### Errors

No new diagnostic.  These are not valid values, and are reported as any other invalid value is: `SQLSRC011` from the property or the metadata, `SQLSRC111` from the directive.

- An option that does not exist: `mysql,ansi`.
- An option of another dialect: `postgres,ansi-quotes`.
- An empty part: `mysql,` or `mysql,,ansi-quotes`.
- An option with no dialect before it: `ansi-quotes`.

`dialect=mysql, ansi-quotes` in a directive is two words: `dialect=mysql,` is `SQLSRC111`, and `ansi-quotes` is `SQLSRC109`, a directive that is not known.

Two `dialect=` directives in a file conflict, `SQLSRC112`, when they differ in the dialect or in the options.

### What changes for a project that uses `postgres` today

- Blanks at the end of a line and blank lines between the parts of a continued `E` string are cleaned as in any other SQL.  They were copied as written.  The line break between the parts always stays, so PostgreSQL still reads one string.
- A continued string whose later part is not closed is reported, `SQLSRC101`, at the quote of that part.  It was reported at the quote of the first part.

## The rules

### Options of `mysql` and `mariadb`

| Options | `'` | `"` |
|----|----|----|
| None | Backslash | Backslash |
| `ansi-quotes` | Backslash | Doubled |
| `no-backslash-escapes` | Doubled | Doubled |
| Both | Doubled | Doubled |

Everything else about the two dialects is unchanged.

### Continued strings

| Dialect | The gap between two parts may hold |
|----|----|
| `postgres` | Whitespace and `--` comments, with at least one line break |
| `cockroachdb` | Whitespace, with at least one line break |
| Every other dialect, `ansi` included | Nothing: a string is not continued |

A continuation matters only after a part that takes backslash escapes, because only there does it move a boundary: the part that follows takes them too.  A plain string that is continued has the same boundaries either way.

Under `postgres`:

- `E'a' /* c */`, then `'b'` on the next line: the block comment ends the continuation, and `'b'` is a plain string.  PostgreSQL rejects the statement.
- `E'a' 'b'` on one line: no line break, no continuation.  PostgreSQL rejects the statement.
- A third part continues the second by the same rule.

### `cockroachdb`

The rules of `postgres`, with two differences:

| Rule | `postgres` | `cockroachdb` |
|----|----|----|
| Prefixes that give `'...'` backslash escapes | `E`, `e` | `E`, `e`, `b` |
| The gap of a continued string | Whitespace and `--` comments | Whitespace |

`B'...'`, a bit string, and `x'...'` take no escapes.  A part that continues a `b'...'` literal takes them, as one that continues `E'...'` does.  Comments nest, `"..."` doubles its quote, and `$tag$` strings are read, all as in `postgres`.

`postgres` still reads `b'\''` as PostgreSQL does.

## Structure

| Unit | Where | Change | What it does |
|----|----|----|----|
| `SqlDialectOptions` | `Parsing/` | New | A flags enum: `None`, `AnsiQuotes`, `NoBackslashEscapes`. |
| `SqlDialectChoice` | `Parsing/` | New | A `readonly record struct` of a `SqlDialect` and its `SqlDialectOptions`.  What a setting names. |
| `SqlDialect` | `Parsing/` | Changed | Gains `CockroachDb`, at the end. |
| `SqlDialectName` | `Parsing/` | Changed | `TryParse` gives a `SqlDialectChoice`.  It splits the value at commas, reads the name, then each option, and knows which dialects take which options.  The span overload still allocates nothing.  `Accepted` gains `cockroachdb`. |
| `SqlDialectRules` | `Parsing/` | Changed | `For` takes a `SqlDialectChoice` and gives one shared instance for each choice.  A new value, `StringContinuation`: `None`, `AcrossWhitespace` or `AcrossLineComments`.  A new static property, `CockroachDb`. |
| `QuoteReader` | `Parsing/Quoting/` | Changed | One new virtual member: the reader of a part that continues the region just read at an offset, or null.  Null by default. |
| `EscapeStringReader` | `Parsing/Quoting/` | Changed | Its prefixes are a constructor argument.  It loses `continues` and reads one part.  After a part with a prefix it names the Backslash reader as the reader of a continuation. |
| `SqlLexer` | `Parsing/` | Changed | Carries a pending continuation between lexemes, below. |
| `SqlDirectiveScope` | `Parsing/` | Changed | `Dialect` and `TryFindDialect` carry a `SqlDialectChoice`.  The conflict check compares the whole choice. |
| `SqlPreambleDialect`, `SqlFileParser` | `Parsing/` | Changed | Pass a `SqlDialectChoice` where they pass a `SqlDialect`. |
| `DialectSetting`, `FileDialect`, `SqlFileReader` | `Generation/` | Changed | Carry a `SqlDialectChoice`.  A value that is not valid is still read as `ansi` and reported. |

`SqlTextBuilder`, `SqlMarkerReader`, the pipeline of `SqlSourceGenerator` and the two files in `build/` do not change.  No MSBuild property or metadata is added.

### The lexer

The lexer still names no dialect and no quoting form.  It holds a reader, the quote that opened the last part, and whether the gap has held a line break.

1. After a quoted region is read, the lexer asks its reader for the reader of a continuation.  If there is one and the rules' `StringContinuation` is not `None`, it is pending.
2. It stays pending across whitespace, where a line break is noted, and across a line comment when the value is `AcrossLineComments`.  A line comment ends at a line break, so one in the gap counts as a line break.
3. Anything else clears it: text that is not whitespace, a block comment, a hint, a quoted region of another kind, and the same quote when the gap held no line break.
4. When the same quote opens after a gap with a line break, the region is read by the pending reader, and step 1 applies to it again.

A reader still holds no state.  Nothing is allocated for a continuation.  The file's header holds only comments, so nothing is pending when `SqlPreambleDialect` switches the rules.

The lexer does not know markers.  A `-- name:` line in the gap is a line comment to it, the continuation stays pending, and the parser sees the marker as it sees any other.

## Testing

Each change is written test first.

- **`SqlDialectNameTests`**: each option and its alias, case, whitespace, order, a repeated option, each invalid form above, and `cockroachdb` with its alias.  `Accepted` and the message of `SQLSRC011` are compared by the test that exists.
- **`SqlDialectRulesTests`**: one shared instance for each choice; the rows of the opener and comment-rule theories for `CockroachDb`; `StringContinuation` for each dialect.
- **`SqlLexerTests`**
  - `Lex_Construct_IsReadByTheRulesOfTheDialect` takes the value syntax in its dialect column, so a row can name `mysql,ansi-quotes`.  Rows for the four combinations of options on both engines, for `'...'` and `"..."`.  `CockroachDb` joins the rows it shares with `PostgreSql`.  The `postgres` row of a continued string changes to separate lexemes; the `ansi` row does not change.
  - A theory for the gap under `postgres`: a comment before the line break, comment lines after it, several comments, three parts, a `-- name:` line, and each thing that ends the continuation.
  - `cockroachdb`: `b'\''`, a `b` at the end of an identifier, `B'\'`, a continued `b` literal, and a comment in the gap ending the continuation.
  - `Lex_UnterminatedQuoteOfADialect_ReportsTheOpeningDelimiter`: the position of the continued-string row changes, and the comment on the `b'\''` row is corrected.
- **`EscapeStringReaderTests`**: the continuation cases move to the lexer tests.  The prefixes and the continuation reader are tested here.
- **`SqlFileParserTests`**: the built SQL of a continued string with its comments stripped and the line break kept; the same under `keep-comments`; a `-- name:` line in the gap giving two blocks; an option set by the directive; two directives that differ only in options.
- **`SqlDirectiveScopeTests`, `SqlPreambleDialectTests`**: a directive with options is found, applied and compared.
- **Generator tests**: an option through the property and through the metadata; an invalid option reported as `SQLSRC011` with the value as written.
- **`SqlFileParserAllocationTests`**: unchanged, and still passing.

## Documentation

All in the same pull request.

- **`README.md`**
  - `cockroachdb` has its own row in the dialect table, and leaves the row of `postgres`.
  - "What a dialect changes" gains a `cockroachdb` column.  Its details say that `b'...'` takes backslash escapes there, and how each of `postgres` and `cockroachdb` continues a string.
  - A section on options: the two options, what each changes, and the value in the three places.
  - "What is still read differently" loses the SQL modes, the continued string and the bytes literal, and gains the bare `\r`.
- **`docs/diagnostics.md`**: `SQLSRC011` and `SQLSRC111` name `cockroachdb`, its alias and the options, and say that an option of another dialect is not valid.  `SQLSRC112` says that two `dialect=` directives conflict when their options differ.
- **`docs/tech-debt/TD-0004`**: rewritten to what remains: the versioned comment, SQLite's open comment and the bare `\r` under `cockroachdb`.  It keeps its id.  Its row in `docs/tech-debt/README.md` changes to match.
- **`docs/deferred/D-0001`**: the CockroachDB row goes, and seven dialects become eight.  Its row in `docs/deferred/README.md` changes to match.
- **`src/SqlSource/AGENTS.md`**: the bullets of `Parsing/` on `SqlDialectRules`, on readers, on the continued string and on `SqlDialectName` are brought up to date.
- **The dialect design and its plan** are left as written.  They record that work.

## Research

### Sources

- PostgreSQL: [`scan.l`](https://github.com/postgres/postgres/blob/REL_18_STABLE/src/backend/parser/scan.l), read for this design.  `comment` is `("--"{non_newline}*)`.  `whitespace_with_newline` is `({non_newline_whitespace}*{newline}{special_whitespace}*)`, where `non_newline_whitespace` is `({non_newline_space}|{comment})` and `special_whitespace` is `({space}+|{comment}{newline})`.  `quotecontinue` is `{whitespace_with_newline}{quote}`.  A block comment is in none of them.
- CockroachDB: [`scan.go`](https://github.com/cockroachdb/cockroach/blob/master/pkg/sql/scanner/scan.go), read for this design.
  - `case 'b'` calls `scanString` with `allowEscapes` true; `case 'B'` calls `scanBitString`; `case 'x', 'X'` calls `scanHexString`; a plain `'` and `"` call `scanString` with `allowEscapes` false.
  - After a closing quote, `scanString` calls `skipWhitespace(lval, false)`, with comments not allowed, and continues only when a single quote follows and the whitespace held a `\n`.
  - `ScanComment` counts the depth of `/*`, and ends a `--` comment at `\n`.
- MySQL: [SQL modes](https://dev.mysql.com/doc/refman/8.4/en/sql-mode.html), read for this design.  `ANSI_QUOTES` treats `"` as an identifier quote, like the backtick.  `NO_BACKSLASH_ESCAPES` stops the backslash being an escape character in strings and in identifiers.
- MariaDB: [SQL mode](https://mariadb.com/kb/en/sql-mode/), read for this design.  It describes the two modes the same way.

## Verification

- `./pre-commit-validation.sh` passes.
- The tests that pin `ansi`, `mssql`, `sqlite`, `oracle` and plain `mysql` and `mariadb` pass unchanged.
- `grep -r "TD-0004" .` finds only text that is true of the rewritten item, outside the earlier design and plan.
- `grep -ri "cockroach" docs/deferred` finds CockroachDB only among the dialects that are delivered.
- Before the pull request is opened: `git fetch --tags origin`, and `VersionPrefix` is compared with the highest `v*` tag.
