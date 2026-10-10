# TD-0034 - What the removal of a connection's value from a describer's text does not cover

## Problem

Before a text that a describer gave is printed, [`Redaction`](../../src/SqlSource.Tool/Describing/Redaction.cs) writes every occurrence of the database's whole connection value as `***`.  [`DatabaseRuns`](../../src/SqlSource.Tool/Describing/DatabaseRuns.cs) applies it.  Three things are outside it:

- **The descriptor of a failure.**  The id, the message format and the help link of a `DescribeFailure` are printed as given.  A describer that built a descriptor at run time from a server's text would not be covered.
- **A value split over several server lines.**  Each line is searched alone, so a value that a describer breaks over two lines is not found.
- **A value that was changed.**  Only the whole value is replaced.  A driver's message that quotes the value changed, trimmed of a trailing space or line break for one, or quotes one keyword of it, is printed.  The same holds for what is written to a sidecar: [`EntrySecretCheck`](../../src/SqlSource.Tool/Describing/EntrySecretCheck.cs) ends the run when an entry holds the whole value as the writer would write it, and finds nothing of a value that was changed.  It also searches the whole text the writer would write for the entry, its fixed words included, so a connection value that equals one of them, or a short dummy value of a test or a replay such as `server`, `name` or `postgres`, would end the run with `SQLSRC200` for every query.  That fails closed, and no real connection string has that shape, since one holds `=` and no fixed text of the writer does.

## Why it exists

The spec of sub-phase 2.5 gives the rule for printed text and for the whole value, because the tool does not read a connection string and so knows no part of one.  No describer exists yet whose texts could be studied.

## Impact

Low today: the released tool has no describer.  From phase 3 a describer's rule is to put the value, or a part of it, in nothing it returns, and this is the net under that rule, with these holes.

## Proposed fix

With the first real describer: have it hand the tool the parts of the connection that are secret, as its driver's connection-string builder knows them, and redact each.  Search a failure's server lines joined as well as alone.  Give `EntrySecretCheck` the same parts.

## Trigger

Phase 3, the PostgreSQL describer.
