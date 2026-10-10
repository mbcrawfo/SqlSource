# TD-0024 - No test reaches the line that a command line is not valid

## Problem

[`Cli`](../../src/SqlSource.Tool/Cli.cs) writes `sqlsource: the command line is not valid`, and not System.CommandLine's own message, which may repeat a token, when System.CommandLine rejects a command line or reads it another way than [`UsageCheck`](../../src/SqlSource.Tool/UsageCheck.cs) did.  No command line of the tool as it is does either, so no test reaches the line.

## Why it exists

It needs an option that System.CommandLine can reject after `UsageCheck` has passed it, such as one that may be given once and is given twice.  `--project`, `--database` and `--connection` may each be given several times, and `--force` takes no value.

## Impact

Low.  The line is a guard for a disagreement that the tests of `UsageCheck` are there to rule out.

## Proposed fix

With the first option that may be given only once, add a test that gives it twice and expects the tool's own line and nothing of the command line in the output.

## Trigger

Sub-phase 2.6, which adds `--log`, an option that is given once.
