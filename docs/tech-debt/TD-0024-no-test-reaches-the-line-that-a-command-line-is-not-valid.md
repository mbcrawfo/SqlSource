# TD-0024 - No test reaches the line that a command line is not valid

## Problem

[`Cli`](../../src/SqlSource.Tool/Cli.cs) writes `sqlsource: the command line is not valid`, and not System.CommandLine's own message, which may repeat a token, when System.CommandLine rejects a command line or reads it another way than [`UsageCheck`](../../src/SqlSource.Tool/UsageCheck.cs) did.  It writes it too when System.CommandLine binds the values of `--connection` otherwise than `UsageCheck` did ([`DescribeCommand.ReadsConnectionsAs`](../../src/SqlSource.Tool/DescribeCommand.cs)), since the log's `arguments` is redacted from the one reading and the describer is handed the other.  No command line of the tool as it is does either, so no test reaches the line; `UsageCheckTests` holds `ReadsConnectionsAs` as a function, and no test runs it through `Cli`.

## Why it exists

It needs an option that System.CommandLine can reject after `UsageCheck` has passed it, such as one that may be given once and is given twice.  `--project`, `--database` and `--connection` may each be given several times, and `--force` takes no value.  Sub-phase 2.6 added `--log`, which is given once, but `UsageCheck` rejects a second one itself with its own line, `option '--log' is given more than once`, since System.CommandLine's message repeats the value: it does not reach the line either.

## Impact

Low.  The line is a guard for a disagreement that the tests of `UsageCheck` are there to rule out.

## Proposed fix

With the first option or argument that `UsageCheck` and System.CommandLine could read differently, or a way for a test to give `Cli` a parser of its own, add a test that makes them disagree and expects the tool's own line and nothing of the command line in the output.

## Trigger

A new kind of option or argument, or a seam in `Cli` for the parser.
