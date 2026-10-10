# TD-0034 - What the removal of a connection's value from a describer's text does not cover

## Problem

Before a text that a describer gave is printed, [`Redaction`](../../src/SqlSource.Tool/Describing/Redaction.cs) writes every occurrence of the database's whole connection value as `***`.  [`DatabaseRuns`](../../src/SqlSource.Tool/Describing/DatabaseRuns.cs) applies it.  Four things are outside it:

- **The descriptor of a failure.**  The id, the message format and the help link of a `DescribeFailure` are printed as given.  A describer that built a descriptor at run time from a server's text would not be covered.
- **A value split over several server lines.**  Each line is searched alone, so a value that a describer breaks over two lines is not found.
- **A value that was changed.**  Only the whole value is replaced.  A driver's message that quotes the value changed, trimmed of a trailing space or line break for one, or quotes one keyword of it, is printed.
- **A sidecar.**  What a describer returns is also written into the sidecar: the server's version, the names and types of columns.  That text is not searched at all, and a sidecar is a file that is committed.

## Why it exists

The spec of sub-phase 2.5 gives the rule for printed text and for the whole value, because the tool does not read a connection string and so knows no part of one.  No describer exists yet whose texts could be studied.

## Impact

Low today: the released tool has no describer.  From phase 3 a describer's rule is to put the value, or a part of it, in nothing it returns, and this is the net under that rule, with these holes.

## Proposed fix

With the first real describer: have it hand the tool the parts of the connection that are secret, as its driver's connection-string builder knows them, and redact each.  Search a failure's server lines joined as well as alone.  And either redact the session's server version before it reaches an entry, or check an entry's text for the value before it is written, failing the query.

## Trigger

Phase 3, the PostgreSQL describer.
