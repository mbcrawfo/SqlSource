# TD-0024 - Three gaps of the `sqlsource` tool's shell that wait for a later sub-phase

## Problem

The review of sub-phase 2.2 of query generation found these in [`src/SqlSource.Tool`](../../src/SqlSource.Tool/AGENTS.md).  None can be reached, or tested, in the tool as that sub-phase leaves it.

1. **`SQLSRC200` prints the message of any exception.**  [`Cli`](../../src/SqlSource.Tool/Cli.cs) reports whatever reaches its catch with the exception's own message.  A driver's exception, or one of a parser of connection strings, can quote a part of a connection string.
2. **Ctrl+C is always swallowed**, in `Cli.Run`, and the run is cancelled through its token.  A run that does not look at its token, one that waits on a database for one, cannot be stopped from the keyboard.
3. **No test reaches the line `sqlsource: the command line is not valid`.**  `Cli` writes it, and not System.CommandLine's own message, which may repeat a token, when System.CommandLine rejects a command line or reads it another way than [`UsageCheck`](../../src/SqlSource.Tool/UsageCheck.cs) did.  No command line of the tool as it is does either.

## Why it exists

The tool takes no connection, ends at once, and has no option that takes a value.  The first two need a driver and a run that can wait, and the third needs an option that System.CommandLine can reject after `UsageCheck` has passed it, such as one that may be given once and is given twice.

## Impact

Low today.  The first is a secret in the tool's output from sub-phase 2.5 on, and the second a tool that must be killed, unless that sub-phase settles them.

## Proposed fix

1. Catch the exceptions of a driver where a connection is opened and a query described, and report each with an id of its own and without the connection's text, so that none reaches the general catch.
2. Let a second Ctrl+C end the process: leave `ConsoleCancelEventArgs.Cancel` false once the token is cancelled.
3. With the first option that may be given only once, add a test that gives it twice and expects the tool's own line and nothing of the command line in the output.  `--project`, the first option that takes a value, may be given several times, so it cannot serve.

## Trigger

Sub-phase 2.5, which adds `--connection` and the first connection.
