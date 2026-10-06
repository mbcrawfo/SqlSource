# TD-0004 - Some SQL is misread whatever the dialect

## Problem

A file is read by the rules of its dialect: [`SqlDialectRules`](../../src/SqlSource/Parsing/SqlDialectRules.cs) and the readers in [`Parsing/Quoting/`](../../src/SqlSource/Parsing/Quoting).  These constructs are still read differently from the database they are written for:

| Construct | Database | How SqlSource reads it |
|----|----|----|
| SQL written for the SQL mode `ANSI_QUOTES`, where `"a\"` is a complete identifier | MySQL, MariaDB | As in the default mode: the backslash escapes the quote, so the region is not closed |
| SQL written for the SQL mode `NO_BACKSLASH_ESCAPES`, where `'a\'` is a complete string | MySQL, MariaDB | As in the default mode |
| A versioned comment whose body holds a string that contains `*/`, such as `/*!50700 SELECT '*/' */` | MySQL, MariaDB | The comment ends at the first `*/`.  The server reads the body as SQL when its version is high enough, and as a comment when it is not, so where it ends depends on the server. |
| A comment between the parts of a continued `E'...'` string | PostgreSQL | The string ends before the comment, and the part after it is a plain string with no backslash escapes |
| A bytes literal with a backslash escape, `b'\''` | CockroachDB, under `postgres` | PostgreSQL's reading: `b'\'` is complete |
| A block comment that is still open at the end of the file | SQLite | `UnterminatedBlockComment` |

The default dialect, `ansi`, is one set of rules for every database.  By design it also misreads what the table in `README.md` says it does not read: backslash escapes in plain strings, `[...]` identifiers, `q'...'` strings, `#` comments, `--` that needs whitespace, and comments that do not nest.  A project that sets a dialect is not affected.

A misread has one of two outcomes:

- **An error.**  The quotes or comments no longer balance, and the file gets `UnterminatedQuote` or `UnterminatedBlockComment`.  The file produces nothing until the construct is rewritten.  The `keep-comments` directive does not help, because a lexer error ends the pass.
- **Silent removal.**  The misread quotes happen to balance, and SQL after them is read as a comment and stripped.  The `keep-comments` directive avoids the removal.

In [`SqlLexerTests`](../../tests/SqlSource.Tests/Parsing/SqlLexerTests.cs), the `Lex_KnownLimit_*` tests pin the readings of `ansi`, and the last rows of `Lex_UnterminatedQuoteOfADialect_ReportsTheOpeningDelimiter` pin the versioned comment and the bytes literal.  `FindEnd_EscapeStringNotFollowedByAContinuation_EndsAtItsOwnQuote` in [`EscapeStringReaderTests`](../../tests/SqlSource.Tests/Parsing/Quoting/EscapeStringReaderTests.cs) pins the comment in a continued string.

## Why it exists

- A dialect is one fixed set of rules.  The two SQL modes change a rule of MySQL at run time, and nothing in a `.sql` file or a project says which mode its SQL is for.
- The end of a versioned comment cannot be known without the version of the server.
- A quoted region is copied as written and is never searched for markers.  If a comment could sit inside a continued string, a `-- name:` line could too, and a marker must never be inside a quoted region.
- CockroachDB has no dialect of its own; `docs/deferred/D-0001-dialects-not-delivered.md` records it.
- SQLite's reading of an open comment would silently take every later query of the file with it.  An error is the safer reading, and it is deliberate.

## Impact

Low for most projects: the SQL modes are off by default, and the other constructs are rare.  A user of one gets either an error that names an unterminated quote or comment in valid SQL (`SQLSRC101`, `SQLSRC102`), or a generated constant that is missing part of the query.  The second is silent at build time, though the truncated SQL will almost always fail when it runs.  `README.md` lists the constructs.

## Proposed fix

1. Let a dialect take options that change one rule, such as `dialect=mysql` with `ansi-quotes` or `no-backslash-escapes`, in the directive, the metadata and the property.  `SqlDialectRules` already holds each rule as a value or a reader, so an option is another instance: `ANSI_QUOTES` is the Doubled reader in the `"` slot.
2. Allow a comment between the parts of a continued `E` string by returning the parts as separate quoted regions, with the lexer remembering that the next plain string continues an escape string.  The comment between them is then an ordinary lexeme, and a marker there is still seen.
3. Leave the versioned comment and SQLite's open comment as they are.

## Trigger

A user reports one of these constructs.
