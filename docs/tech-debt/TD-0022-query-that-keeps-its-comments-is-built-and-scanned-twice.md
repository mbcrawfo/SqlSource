# TD-0022 - A query that keeps its comments is built and scanned twice

## Problem

A block has two forms of its SQL: `Segments`, without comments, from which the tokens, the parameters and the hash come, and `KeptSegments`, with them.  [`SqlFileParser`](../../src/SqlSource/Parsing/SqlFileParser.cs) builds the second whenever a query may keep its comments, with a second pass of [`SqlTextBuilder`](../../src/SqlSource/Parsing/SqlTextBuilder.cs) over the same lexemes and a second scan for tokens.  So a parse of a query that keeps its comments costs about twice a parse of one that does not.

Measured with a `Stopwatch` loop over a `Release` build, on the file of [`SqlFileParserAllocationTests`](../../tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs) with a preamble that says `keep-comments`:

| | Before the two forms | With them |
|----|----|----|
| Allocated, in bytes for each character | 10.3 | 20.3 |
| Time of a parse of 19 KB | 68 µs | 118 µs |

The same file without `keep-comments` went from 9.3 to 11.3 bytes and from 61 to 69 µs over the same change, for the parameters and the tokens a query now carries.

## Why it exists

`keep-comments` can come from a level the parser cannot see: a property, the metadata of the file's item, or the attribute of a type that claims the file, and two types that claim one file can ask for different things.  The parser therefore keeps both forms where both may be wanted, and the emitter picks one for each type and query.  The hash needs the form without comments in any case.  See "The settings have two stages" and "A block has two forms of its SQL" in [`src/SqlSource/AGENTS.md`](../../src/SqlSource/AGENTS.md).

## Impact

Low.  It is paid only by a query that keeps its comments, or by every query without a list of its own in a file for which a level outside it asks for comments.  A parse of such a file is still about a tenth of a millisecond for 19 KB.  The test holds a budget of its own for this path, so a further rise is seen.

## Proposed fix

Build the kept form from the stripped one's work: one pass of the text builder that writes both outputs, and one scan that finds the tokens of both, since the two differ only where a comment stands.  Or keep the comments as segments of their own, which the emitter writes or leaves out.

## Trigger

A project whose files mostly keep their comments reports slow builds or a slow editor, or a third form of the SQL is needed.
