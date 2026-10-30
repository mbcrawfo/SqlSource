# TD-0042 - Allocations a parse could avoid

## Problem

A review of `Parsing/` on 2026-10-11 found two allocations of a parse that it does not need.

- **A summary of one line makes a list, and on .NET Framework a copy.**  `SqlFileParser.Parser.ReadBlockMarkers` makes a `List<string>` for every block, adds a substring for each `-- summary:` line and joins them with `string.Join`.  For the usual summary of one line, the substring is already the result.  On .NET, `string.Join` returns that substring; on .NET Framework, where the compiler of Visual Studio runs the generator, it builds a new string.  The list is made on both.
- **A scope holds the name of its input model twice.**  `SqlMarkerScope.InputModel` is a tuple of the name and its marker, and `_inputModel` holds the same name.

## Why it exists

Both were there before the pull request of that review, which changed no behaviour and so left what a parse allocates as it was.

## Impact

Low.  A parse allocates about 4,500 bytes for a query of the typical file of [`SqlFileParserAllocationTests`](../../tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs), and a `-- param:` about 700 more, and each of these is tens of bytes of that.  The budgets of those tests see the list on .NET, and the size of every scope, but neither is large enough to fail them.

## Proposed fix

- In `ReadBlockMarkers`, keep the first summary line as the summary and make the list at the second.
- In `SqlMarkerScope`, hold the marker of the first `-- input-model:` alone, and give the name from `_inputModel`.

A pull request that makes either change states the time and the allocation of a parse before and after, as `src/SqlSource/AGENTS.md` asks.  The tests run on .NET, so the copy that the first change removes on .NET Framework is not in what they measure.

## Trigger

The next change to `ReadBlockMarkers` or to how `SqlMarkerScope` holds its input model, or the next change to a budget of `SqlFileParserAllocationTests`.
