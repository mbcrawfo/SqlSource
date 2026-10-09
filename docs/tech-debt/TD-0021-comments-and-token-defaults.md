# TD-0021 - Comments interact with the defaults of tokens in three ways that are not reported

## Problem

The default of a token, `{{name:default}}`, is read from the SQL of a query, and comments take part in what that is.  Three behaviours follow, each confirmed by a probe, and none of them is reported:

1. **A `--` comment inside an inline default swallows the closing braces.**  In `{{f:AND a = 1 -- note}}` followed by a line `ORDER BY {{o:name}}`, the comment runs to the end of its line and takes the `}}` with it.  The SQL without comments has no `}}` on that line, so the default of `f` runs to the next `}}`, the one of `{{o:name}}`, and the token `o` is gone.  With `keep-comments` the same text is read as two tokens.  [`SqlTextBuilder`](../../src/SqlSource/Parsing/SqlTextBuilder.cs) builds the SQL that the tokens are found in, and the lexer that decides what a comment is does not know that a token has begun.
2. **A `-- token:` marker's default is taken as written, and an inline default is taken from the SQL without comments.**  So `-- token: {{f:a /* c */ b}}` beside an identical inline `{{f:a /* c */ b}}` is `SQLSRC112`, a conflict between two defaults, and editing a comment inside a marker's default changes the query's hash.
3. **The SQL with comments is built and scanned wherever comments may be wanted for the file.**  [`FileParseInput.CommentsWanted`](../../src/SqlSource/Generation/FileParseInput.cs) is true when the file's metadata says `keep-comments`, and a type's attribute (`Parameters`) can override it, so it can be true where no type ends up keeping comments.  A `{{class}}` inside a comment is then `SQLSRC114` for SQL that nothing emits.

The README tells users not to put a `--` comment inside a default (Defaults, under Tokens).  The other two are not documented.

## Why it exists

- The stripped SQL is the one definition of a query's text, from which the tokens, the parameters and the hash come ([`SqlQueryHash`](../../src/SqlSource/Parsing/SqlQueryHash.cs)), so comments are gone before a token is found.
- A marker's text is a separate piece of the file that is not stripped.
- The parser cannot see the attribute, so it builds the kept form whenever any level it can see may want it; a parse that does not know a level of the settings cannot do better without making the settings an input of the parse, which the design rules out (see [`src/SqlSource/AGENTS.md`](../../src/SqlSource/AGENTS.md), "The settings have two stages").

## Impact

Low.  The first needs a `--` comment at the end of a line that holds a default's closing braces, and the result is a missing token, which is noticed when the method lacks a parameter or the build breaks.  The second shows as an error that names the two defaults.  The third is an error for SQL that is not emitted, and it is removed by taking the word out of the comment.

## Proposed fix

1. Report a `--` comment that begins inside a default and ends after it, or make the lexer read to the closing braces of a token before it reads a comment.
2. Strip comments from a marker's default by the same rules as an inline one, before the two are compared and before the hash is taken.
3. Report the third where the type's settings are resolved, and not in the parse, or build the kept form's tokens only for a query that keeps its comments.

[TD-0004](TD-0004-lexer-misreads-dialect-specific-sql.md) is about SQL the lexer misreads by dialect and is not the home of these.

## Trigger

A report of a token that disappears, of an `SQLSRC112` for two defaults that look equal, or of an `SQLSRC114` for a comment.
