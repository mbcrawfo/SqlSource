# TD-0042 - Allocations of a parse that no budget sees

## Problem

A review of `Parsing/` on 2026-10-11 found three things about what a parse allocates.  None is new but the field, and none is under a budget.

- **A query's scope is under no budget.**  The file of [`SqlFileParserAllocationTests`](../../tests/SqlSource.Tests/Parsing/SqlFileParserAllocationTests.cs) has no query with a marker of its own: its queries have `-- name:` and `-- summary:` alone, which the parser reads itself.  So a parse of it makes one [`SqlMarkerScope`](../../src/SqlSource/Parsing/Markers/SqlMarkerScope.cs), the preamble's, and what a scope costs for a query with a `-- param:`, a `-- token:` or a setting is not measured.  The change under review added the scope's `SqlScopeKind`, a field of 4 bytes that made every scope 8 bytes larger as measured on a 64-bit runtime, and its measurement showed it once for a file.
- **A summary of one line makes a list, and on .NET Framework a copy.**  `SqlFileParser.Parser.ReadBlockMarkers` makes a `List<string>` for every block, adds a substring for each `-- summary:` line and joins them with `string.Join`.  For the usual summary of one line, the substring is already the result.  On .NET, `string.Join` returns that substring; on .NET Framework, where the compiler of Visual Studio runs the generator, it builds a new string.  The list is made on both.
- **A scope holds the name of its input model twice.**  `SqlMarkerScope.InputModel` is a tuple of the name and its marker, and `_inputModel` holds the same name.

## Why it exists

The first is how the test's file was written, before a query had markers of its own.  The other two were there before the pull request, which changed no behaviour and so left what a parse allocates as it was, apart from the field its design asks for.

## Impact

Low.  A parse allocates about 4,400 bytes for a query of the test's file, and each of these is tens of bytes of that.  The risk is the first: a change that makes a scope dearer passes the allocation tests.

## Proposed fix

- Give `SqlFileParserAllocationTests` a file whose queries each have a marker of their own, a `-- param:` for one, with a budget of its own, set from what it measures.
- In `ReadBlockMarkers`, keep the first summary line as the summary and make the list at the second.
- In `SqlMarkerScope`, hold the marker of the first `-- input-model:` alone, and give the name from `_inputModel`.

A pull request that makes either of the last two changes states the time and the allocation of a parse before and after, as `src/SqlSource/AGENTS.md` asks.  The tests run on .NET, so the copy that the second change removes on .NET Framework is not in what they measure.

## Trigger

The next change to the fields of `SqlMarkerScope` or to `ReadBlockMarkers`, or the next time a budget of the parse is set again.
