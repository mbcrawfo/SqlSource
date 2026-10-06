# TD-0005 - A marker written above a name marker applies to the previous block

## Problem

A block runs from its `-- name:` marker to the next one, and a `-- summary:` or `-- SqlSource:` marker applies to the block it is in, wherever it appears.  A marker written directly above a name marker is therefore part of the block before it:

```sql
-- name: A
SELECT {{x}} FROM a

-- SqlSource: no-token-validation token-ignore=x
-- summary: Loads B.
-- name: B
SELECT {{x}} FROM b
```

[`SqlFileParser`](../../src/SqlSource/Parsing/SqlFileParser.cs) gives block `A` the summary "Loads B.", turns validation off for `A`, and leaves `{{x}}` in `A` as literal text.  Block `B` gets none of it.  No error is reported.

Above the first name marker the same lines behave differently: the directives apply to every block, and the summary is the `SummaryBeforeFirstName` error.  The first block in a file therefore does not teach the rule.

## Why it exists

It is what the phase 1 spec defines, and the parser implements the spec.  The whole-branch review of phase 1 raised it.  Changing it changes rules the project owner approved, so it was recorded and not changed.

## Impact

Nothing reaches a user yet.  From phase 2 on, a developer who writes annotations above a declaration, as C# attributes and documentation comments are written, gets the wrong result silently: validation is off for the wrong query, a literal `{{x}}` is sent to the database, and the documentation lands on the wrong member.

## Proposed fix

Report an error for a `-- summary:` or `-- SqlSource:` marker that has no SQL between it and the next name marker or the end of the file.  The rule "a marker applies to its whole block wherever it appears" stays true for every marker that is accepted, and the mistake above becomes an error that points at the misplaced line.  It needs a new `SqlParseErrorKind`, a check in `SqlFileParser.ReadBlock`, and the project owner's agreement.

## Trigger

Before phase 2 documents the `.sql` file format in `README.md`.  After that, tightening the rule breaks files that rely on it.
