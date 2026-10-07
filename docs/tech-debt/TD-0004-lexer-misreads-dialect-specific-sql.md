# TD-0004 - Some SQL is misread whatever the dialect

## Problem

A file is read by the rules of its dialect and of the dialect's options: [`SqlDialectRules`](../../src/SqlSource/Parsing/SqlDialectRules.cs) and the readers in [`Parsing/Quoting/`](../../src/SqlSource/Parsing/Quoting).  These constructs are still read differently from the database they are written for:

| Construct | Database | How SqlSource reads it |
|----|----|----|
| A versioned comment whose body holds a string that contains `*/`, such as `/*!50700 SELECT '*/' */` | MySQL, MariaDB | The comment ends at the first `*/`.  The server reads the body as SQL when its version is high enough, and as a comment when it is not, so where it ends depends on the server. |
| A block comment that is still open at the end of the file | SQLite | `UnterminatedBlockComment` |
| A carriage return with no line feed after it | MySQL, SQLite, CockroachDB | As the end of a line, as in every dialect.  These databases end a `--` comment only at a line feed, and CockroachDB counts a line break between the parts of a continued string only there too. |

The default dialect, `ansi`, is one set of rules for every database.  By design it also misreads what the table in `README.md` says it does not read: backslash escapes in plain strings, `[...]` identifiers, `q'...'` strings, `#` comments, `--` that needs whitespace, and comments that do not nest.  Setting the dialect fixes those, and only those: each construct in the table above is still misread under its own dialect.

A misread has one of two outcomes:

- **An error.**  The quotes or comments no longer balance, and the file gets `UnterminatedQuote` or `UnterminatedBlockComment`.  The file produces nothing until the construct is rewritten.  The `keep-comments` directive does not help, because a lexer error ends the pass.
- **Silent removal.**  The misread quotes happen to balance, and SQL after them is read as a comment and stripped.  The `keep-comments` directive avoids the removal.

In [`SqlLexerTests`](../../tests/SqlSource.Tests/Parsing/SqlLexerTests.cs), the `Lex_KnownLimit_*` tests pin the readings of `ansi`.  The `/*!50700` row of `Lex_UnterminatedQuoteOfADialect_ReportsTheOpeningDelimiter` pins the versioned comment, `Lex_UnterminatedBlockComment_IsAnErrorInEveryDialect` pins SQLite's comment, and the last row of `Lex_StringInCockroachDb_IsReadByItsRules` pins the carriage return in a continued string.

## Why it exists

- The end of a versioned comment cannot be known without the version of the server.
- SQLite's reading of an open comment would silently take every later query of the file with it.  An error is the safer reading, and it is deliberate.
- What ends a line is the same for every dialect, in `SqlLexer`, in `SqlMarkerReader` and in `SqlTextBuilder`.  A rule for some dialects alone would reach all three, for files with the line endings of classic Mac OS.

## Impact

Low: the constructs are rare.  A user of one gets either an error that names an unterminated quote or comment in valid SQL (`SQLSRC101`, `SQLSRC102`), or a generated constant that is missing part of the query.  The second is silent at build time, though the truncated SQL will almost always fail when it runs.  `README.md` lists the constructs.

## Proposed fix

1. For the versioned comment, an option of `mysql` and `mariadb` that gives the version of the server.  The lexer then knows whether the body is SQL, and reads it as SQL or as a comment.  `SqlDialectName` already reads options after a dialect's name.
2. Leave SQLite's open comment as it is.
3. For the carriage return, a value in `SqlDialectRules` for what ends a line, read by the three places that look for one.

## Trigger

A user reports one of these constructs.
