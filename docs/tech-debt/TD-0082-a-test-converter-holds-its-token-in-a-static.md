# TD-0082 - A test converter holds its token in a static

## Problem

[`CancelOnRead.Source`](../../tests/DatabaseTests/SqlServer/EndToEnd/Types/CancelOnRead.cs) is a `public static CancellationTokenSource?` with a setter, which [`RunTimeArgumentTests`](../../tests/DatabaseTests/SqlServer/EndToEnd/RunTimeArgumentTests.cs) sets before its call and resets after, found by the maintainability review of 2026-10-27.  The converter cancels whatever source the static holds when it reads a value.

## Why it exists

A converter is a static method that the generated code calls, so the test has no other way to hand it the token it should cancel.

## Impact

Low.  It is safe while no other test reads the query that uses `CancelOnRead` at the same time; a second such test, run in parallel, would cancel the first one's token or read none.

## Proposed fix

Hold the source in an `AsyncLocal<CancellationTokenSource?>`, so that each test's flow sees its own.

## Trigger

A second test that reads a query through `CancelOnRead`.
