# TD-0034 - What the removal of a connection's value from a describer's text does not cover

## Problem

Before a text that a describer gave is printed, [`Redaction`](../../src/SqlSource.Tool/Describing/Redaction.cs) writes every occurrence of the database's whole connection value as `***`.  [`DatabaseRuns`](../../src/SqlSource.Tool/Describing/DatabaseRuns.cs) applies it.  The log of `--log`, through [`SessionRunLog`](../../src/SqlSource.Tool/Logging/SessionRunLog.cs), and the lines of `--verbose`, through [`RunTrace`](../../src/SqlSource.Tool/Logging/RunTrace.cs), have the same removal, and [`EntrySecretCheck`](../../src/SqlSource.Tool/Describing/EntrySecretCheck.cs) ends the run when a describer put the whole value into an entry of a sidecar.  Four things are outside all of them:

- **A value that was changed.**  Only the whole value is found.  A driver's message that quotes the value changed, trimmed of a trailing space or line break for one, or quotes one keyword of it, is printed, said and logged, and an entry that holds it is written.  A log is a file that a user may attach to an issue, and a sidecar is committed.
- **A value that the parts of a message make together.**  The message format of a failure's descriptor and each of its arguments are searched alone, so a value that stands half in the format and half in an argument, or over two arguments, is in the message that is printed and logged.  The same holds for a value that a describer writes over two members of one event of its log.
- **A value that the sidecar holds without the describer.**  `EntrySecretCheck` finds a value only when the entry's text holds it and the same text without the describer's part does not.  A value that equals a word of the format, the engine, or the name of the query, of its database or of a parameter is so never found in what a describer gave.  No real connection string has that shape, and the file holds such a value whatever the describer does.
- **A value that equals a word of the describer's part of an entry.**  The text without the describer's part has no column and no type, so a value that equals a key that only those have, `origin` or `nullableSource` for two, ends the run with `SQLSRC200` when a query has a column.  That fails closed, and it is what is left of a wider case: a dummy value of a test or a replay such as `postgres` no longer ends the run.

## Why it exists

The tool does not read a connection string and so knows no part of one, and no describer exists yet whose texts could be studied.  What the tool could mend alone is mended: the descriptor of a failure is searched, a failure's server lines are searched joined, and a value that the sidecar holds without the describer is no fault.

## Impact

Low today: the released tool has no describer.  From phase 3 a describer's rule is to put the value, or a part of it, in nothing it returns, and this is the net under that rule, with these holes.

## Proposed fix

With the first real describer: have it hand the tool the parts of the connection that are secret, as its driver's connection-string builder knows them, and redact each in `Redaction`, `SessionRunLog` and `RunTrace`, and search for each in `EntrySecretCheck`.  That does not reach the case that matters most: a driver quotes a connection string mostly when it cannot read it, and then its builder gives no part either.  So study what the driver says of a string it cannot read, and either keep that text from the output whole or search it for the value as the driver changes it.  Search the message of a failure once it is made, and not its format and arguments alone.

## Trigger

Phase 3, the PostgreSQL describer.
