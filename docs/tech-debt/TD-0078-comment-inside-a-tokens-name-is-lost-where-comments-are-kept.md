# TD-0078 - A token with a comment inside its name is lost where comments are kept

## Problem

A comment between the braces of a token and its name, or between its name and the colon or closing braces, makes the two forms of a query's SQL disagree.  In `{{f/* c */}}`, `{{/* c */f}}` and `{{f/* c */:a}}`, the SQL without comments has a stripped block comment's one space in its place, `{{f }}`, which [`TokenScanner`](../../src/SqlSource/Parsing/Tokens/TokenScanner.cs) reads as the token `f`, since blanks are allowed around a name.  The kept form, built by [`SqlFileParser.BuildKept`](../../src/SqlSource/Parsing/SqlFileParser.cs) for a query that may keep its comments, has the comment there, and is not a token: it stays literal text.  Nothing is reported.

So a type that keeps the query's comments emits a constant, or a method without the parameter `f`, holding `{{f/* c */}}` as SQL, while the tool describes, and the hash covers, the query with the token `f`.

`SQLSRC120` covers a comment that crosses the edge of a token's default, and this is the same kind of disagreement in the token's head, which it does not look at.

## Why it exists

It was found by the review of the change that added `SQLSRC120`, and is older than that change: the kept form was always scanned for tokens on its own.  Reporting it adds a case to a diagnostic, which is the owner's decision, and the change it was found in was about defaults.

## Impact

Low.  It needs a comment written inside a token's braces before the default, and `keep-comments`.  The result is a query whose method lacks a parameter or whose SQL holds the braces, which the build or the first run shows.

## Proposed fix

In `BuildKept`, beside the check of a default opened in a comment, report a token of the SQL without comments whose head, the text from `{{` to the colon or to `}}`, holds a comment in the file: map the occurrence's span back with `SqlBlockText.ToSourceSpan` and look for a comment lexeme inside it before the colon.  Report it as a kept error, as the other disagreements of the two forms are, under `SQLSRC120` with a message that covers a head, or a diagnostic of its own.  Alternatively, read the token's head in the kept form with comments skipped, so that the two forms agree.

## Trigger

A report of a token that a type which keeps comments does not emit, or a change to how a token's head is read.
