# TD-0081 - A test starts mkfifo itself

## Problem

[`PostgresDescriberTests.SecretsOf_PasswordFileThatIsANamedPipe_IsNoneAndIsNotOpened`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresDescriberTests.cs) makes its named pipe with `Process.Start(new ProcessStartInfo("mkfifo", ...))`, found by the maintainability review of 2026-10-27.  `tests/AGENTS.md` says a test starts a program through `Programs.RunAsync`, never `Process.Start`, and skips with `Programs.SkipUnlessOnPath` where a program of the operating system is missing.  The test skips only on Windows, and waits for `mkfifo` on the test's token rather than on `Eventually.Patience`.

## Why it exists

It was written before, or apart from, the rule that every program goes through `Programs`.

## Impact

Low.  On a Unix machine without `mkfifo` on the path the test fails instead of skipping, and a `mkfifo` that hangs holds the run until the test's token ends it.

## Proposed fix

`Programs.SkipUnlessOnPath("mkfifo")`, then `Programs.RunAsync(start, Eventually.Patience)`.

## Trigger

The next change to that test, or a run on a machine without `mkfifo`.
