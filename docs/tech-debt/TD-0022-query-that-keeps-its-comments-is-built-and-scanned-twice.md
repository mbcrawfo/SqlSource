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

The tokens, the parameters and the hash come from the SQL without comments, whatever the query keeps, so a query that keeps its comments needs both forms, and the parser builds them in two passes.  The parser knows which queries keep their comments: a query's own list or its file header's, else the list of the settings files of the file's folder, which reaches the parser as `FileParseInput.CommentsWanted`.  See "[The settings have two stages](../architecture/generator.md#the-settings-have-two-stages)" and "[A block has two forms of its SQL](../architecture/generator.md#a-block-has-two-forms-of-its-sql)" in [`docs/architecture/generator.md`](../architecture/generator.md).

## Impact

Low.  It is paid only by a query that keeps its comments, by its own list or its file's, or by the list of a settings file that reaches it.  A parse of such a file is still about a tenth of a millisecond for 19 KB.  The test holds a budget of its own for this path, so a further rise is seen.

## Proposed fix

Build the kept form from the stripped one's work: one pass of the text builder that writes both outputs, and one scan that finds the tokens of both, since the two differ only where a comment stands.  Or keep the comments as segments of their own, which the emitter writes or leaves out.

## Trigger

A project whose files mostly keep their comments reports slow builds or a slow editor, or a third form of the SQL is needed.
