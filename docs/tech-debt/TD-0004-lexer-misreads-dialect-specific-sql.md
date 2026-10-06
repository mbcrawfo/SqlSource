# TD-0004 - The lexer misreads SQL that is specific to some dialects

## Problem

[`SqlLexer`](../../src/SqlSource/Parsing/SqlLexer.cs) follows ANSI rules, plus the PostgreSQL and MySQL quoting forms that cannot be mistaken for anything else.  It reads these constructs differently from the database they are written for:

| Construct | Dialect | How the lexer reads it |
|----|----|----|
| A backslash escape in a plain string, `'a\'b'` or `"a\"b"` | MySQL | The backslash is an ordinary character, so the string ends at the escaped quote |
| A quote-operator literal, `q'[it's]'` | Oracle | An ordinary `'...'` string that ends at the first quote inside it |
| A bracketed identifier that contains a quote, `--` or `/*`, such as `[a'b]` | SQL Server | Plain text, so the quote opens a string and `--` starts a comment |
| `--` with no whitespace after it, `5--3` | MySQL | A comment |
| A `#` comment | MySQL | Plain text, so the comment stays in the SQL |
| A `/*` inside a block comment | MySQL, Oracle, SQLite | A nested comment that needs its own `*/` |

A misread has one of two outcomes:

- **An error.**  The quotes or comments no longer balance, and the file gets `UnterminatedQuote` or `UnterminatedBlockComment`.  The file produces nothing until the construct is rewritten.  The `preserve-comments` directive does not help, because a lexer error ends the pass.
- **Silent removal.**  The misread quotes happen to balance, and SQL after them is read as a comment and stripped.  `SELECT 'a\'b -- c', 2` becomes `SELECT 'a\'b`.  The `preserve-comments` directive avoids the removal.

The `Lex_KnownLimit_*` tests in [`SqlLexerTests`](../../tests/SqlSource.Tests/Parsing/SqlLexerTests.cs) pin each reading.

## Why it exists

The epic decided on one conservative lexer with no dialect setting.  Each of these constructs conflicts with another dialect's reading of the same characters, so none can be supported without knowing the dialect.

## Impact

Nothing reaches a user yet: the parser is not wired into the generator.  From phase 2 on, a user of one of these constructs gets either an error that names an unterminated quote in valid SQL, or a generated constant that is missing part of the query.  The second is silent at build time, though the truncated SQL will almost always fail when it runs.

## Proposed fix

1. Phase 2 lists these limits in `README.md`, and says which outcome `preserve-comments` helps with.
2. Add a dialect setting.  The dialect-sensitive choices are already together in `SqlLexer`: comment nesting, backslash escapes, bracketed identifiers, `#` comments and the whitespace rule for `--` become flags chosen by the setting.
3. Decide whether Oracle's `q'...'` form can be recognised without a setting, as `E'...'` is.  A `q` directly before a quote has no other meaning in the supported dialects, but that needs checking before it is relied on.

## Trigger

Phase 2 writes the README section on the `.sql` file format.  A user reports one of these constructs.  A dialect setting is designed.
