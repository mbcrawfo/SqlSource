# TD-0005 - Markers at the end of a block are accepted

## Goal

A `-- summary:` or `-- SqlSource:` marker at the end of a block is an error.  A marker is at the end of a block when no SQL follows it before the next `-- name:` marker or the end of the file.  The project owner set this goal on 2026-10-05, to avoid confusion about which block such a marker belongs to.

The rule covers every block: a named block followed by another, the last block in a file, and the single block of a file that has no name marker.

## Problem

Today the parser accepts these markers.  A block runs from its `-- name:` marker to the next one, and a marker applies to the block it is in wherever it appears, so a marker at the end of a block applies to that block.  A reader sees it differently when the marker sits directly above the next name marker:

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

A marker after the last SQL of the last block, or of a file with no name marker, is not misapplied.  It is still an error under the goal, so that one rule holds everywhere: a marker comes before the SQL it describes.

## Why it exists

Accepting a marker anywhere in its block is what the phase 1 spec defines, and the parser implements the spec.  The whole-branch review of phase 1 raised the case above.  The goal was set after the parser was written.

## Impact

Nothing reaches a user yet.  From phase 2 on, a developer who writes annotations above a declaration, as C# attributes and documentation comments are written, gets the wrong result silently: validation is off for the wrong query, a literal `{{x}}` is sent to the database, and the documentation lands on the wrong member.

## Proposed fix

In the parser, not in the generator:

1. Add a `SqlParseErrorKind` for a marker at the end of a block, with the marker as its span and no argument.
2. In `SqlFileParser.ReadBlock`, report it for each `-- summary:` or `-- SqlSource:` marker that comes after the block's last lexeme with content (`SqlLexeme.GetContentSpan`).  A block with no content at all stays `EmptyBlock` only.
3. Do not apply a reported marker's summary or directives.
4. Add parser tests for a named block followed by another, the last block in a file, a file with no name marker, a marker followed by a comment only, and a marker between two pieces of SQL, which stays valid.

Phase 2 then maps the new kind to a diagnostic like every other kind.

## Trigger

Before phase 2 documents the `.sql` file format in `README.md`.  After that, tightening the rule breaks files that rely on it.
