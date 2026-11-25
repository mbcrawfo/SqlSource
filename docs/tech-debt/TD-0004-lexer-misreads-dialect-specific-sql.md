# TD-0004 - Some SQL is misread whatever the dialect

## Problem

A file is read by the rules of its dialect and of the dialect's options: [`SqlDialectRules`](../../src/SqlSource/Parsing/SqlDialectRules.cs) and the readers in [`Parsing/Quoting/`](../../src/SqlSource/Parsing/Quoting).  These constructs are still read differently from the database they are written for:

| Construct | Database | How SqlSource reads it |
|----|----|----|
| A versioned comment with a version, whose body holds a string that contains `*/`, such as `/*!50700 SELECT '*/' */`, when the dialect gives no `version=` | MySQL, MariaDB | The comment ends at the first `*/`.  The server reads the body as SQL when its version is high enough, and as a comment when it is not, so where it ends depends on the server. |
| A block comment that is still open at the end of the file | SQLite | `UnterminatedBlockComment` |
| `@x` written for the absolute value of `x`, with no space | PostgreSQL | As the parameter `x`.  Npgsql reads it the same way; write `abs(x)` or `@ x`. |
| A parameter after a comma, outside parentheses, in a statement that follows a `DECLARE` with no `;` and starts with a word that [`SqlDeclaredVariables`](../../src/SqlSource/Parsing/SqlDeclaredVariables.cs) does not know, such as `SEND` or a label | SQL Server | As a local variable, so not as a parameter.  A `-- param:` marker with the type makes it one again.  The server reads it as a parameter, so the tool stops at the query with `SQLSRC231` when it describes one. |
| A parameter inside a hint, `/*+ ... @p ... */` or `/*! ... @p ... */` | Every dialect | Not as a parameter: a hint is copied whole and is not searched, a versioned comment whose body the lexer reads as SQL included. |

A user variable of MySQL and MariaDB, and the parameter of a procedure that the SQL creates, are read as parameters too, unless a `-- variable:` marker names them, which [`SqlMarkerScope`](../../src/SqlSource/Parsing/Markers/SqlMarkerScope.cs) reads and `SqlParameterList` leaves out.  That reading is by design, since nothing in the SQL tells such a name from a parameter, and the marker is the way out.

The default dialect, `ansi`, is one set of rules for every database.  By design it also misreads what the table "What each dialect changes" in [`site/reference/dialects.md`](../../site/reference/dialects.md#what-a-dialect-changes) says it does not read: backslash escapes in plain strings, `[...]` identifiers, `q'...'` strings, `#` comments, `--` that needs whitespace, comments that do not nest, a carriage return alone that ends no line, and a local variable that T-SQL declares with `DECLARE`, which it reads as a parameter.  Setting the dialect fixes those, and only those: each construct in the table above is still misread under its own dialect.

A misread has one of two outcomes:

- **An error.**  The quotes or comments no longer balance, and the file gets `UnterminatedQuote` or `UnterminatedBlockComment`.  The file produces nothing until the construct is rewritten.  The `keep-comments` generator parameter does not help, because a lexer error ends the pass.
- **Silent removal.**  The misread quotes happen to balance, and SQL after them is read as a comment and stripped.  The `keep-comments` generator parameter avoids the removal.

In [`SqlLexerTests`](../../tests/SqlSource.Tests/Parsing/SqlLexerTests.cs), the `Lex_KnownLimit_*` tests pin the readings of `ansi`.  The `/*!50700` row of `Lex_UnterminatedQuoteOfADialect_ReportsTheOpeningDelimiter` pins the versioned comment without a version, and `Lex_UnterminatedBlockComment_IsAnErrorInEveryDialect` pins SQLite's comment.

## Why it exists

- The end of a versioned comment with a version cannot be known without the version of the server, which the `version=` option of `mysql` and `mariadb` gives and a project may leave out.
- Npgsql reads `@x` as a parameter too, so a query that writes it fails in Npgsql whatever SqlSource reads, and `abs(x)` is the fix in both.
- SQLite's reading of an open comment would silently take every later query of the file with it.  An error is the safer reading, and it is deliberate.
- Under `mssql` a local variable is found by its `DECLARE`, without a parser of T-SQL.  T-SQL needs no `;` between statements, so the end of a list of declarations is known only by the word that starts the next statement, from a fixed list: the reserved words that start one, and `THROW`.
- A hint is kept whole because it can hold SQL that must reach the server as written, and its parameters would have to be found without changing it.

## Impact

Low: the constructs are rare.  A user of one gets either an error that names an unterminated quote or comment in valid SQL (`SQLSRC101`, `SQLSRC102`), or a generated constant that is missing part of the query.  The second is silent at build time, though the truncated SQL will almost always fail when it runs.  [`site/reference/dialects.md`](../../site/reference/dialects.md#still-read-differently) lists the constructs, and [`site/reference/parameters.md`](../../site/reference/parameters.md#names-read-as-parameters) the parameter rows, in its section on names that are read as parameters and in its limits.  The SQL Server describer compares a query's list with the parameters SQL Server reports, both ways, and stops at the query with `SQLSRC231` when they differ, so the `DECLARE` row gives an error at the query, never a wrong description.

## Proposed fix

1. For the versioned comment without `version=`, a warning at a numbered versioned comment of `mysql` or `mariadb` whose body holds a quote, which names the option.
2. Leave SQLite's open comment as it is.
3. For a parameter inside a versioned comment that the server runs, read the parameters of its body, which the lexer already reads as SQL, into the query's list, and keep the hint whole in the SQL.
4. For the `DECLARE` row, end a list of declarations at a label, `name:`, as well as at a word that starts a statement.

## Trigger

A user reports one of these constructs.
