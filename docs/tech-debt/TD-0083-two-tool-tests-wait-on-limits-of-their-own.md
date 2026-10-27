# TD-0083 - Two tool tests wait on limits of their own

## Problem

`tests/AGENTS.md` makes `Eventually.Patience` the one limit a test waits on, and two tests keep limits of their own, found by the maintainability review of 2026-10-27:

- [`ProgramsTests`](../../tests/SqlSource.Tool.Tests/ProgramsTests.cs) gives `RunAsync` 20 seconds (`WithinThePatience`) to give up a program whose patience is 200 ms.
- [`TerminationTests`](../../tests/SqlSource.Tool.Tests/Commands/TerminationTests.cs) gives the run 60 seconds (`Soon`) to end once its console is closed.

## Why it exists

Each limit must be shorter than something else the test waits for: `ProgramsTests` must tell `RunAsync`'s patience from the test's own, and `TerminationTests` must end before the two minutes its MSBuild target waits.  Each comment gives that reason, but the rule of `tests/AGENTS.md` does not name them as exceptions.

## Impact

Low.  On a slow runner either can fail when nothing is wrong, and a reader of `tests/AGENTS.md` does not expect them.

## Proposed fix

Derive both from `Eventually.Patience` (for `Soon`, with the MSBuild target's wait made longer than it), or name the two in `tests/AGENTS.md` as the exceptions.

## Trigger

A failure of either on a slow runner, or a change to `Eventually.Patience`.
