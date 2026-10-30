# TD-0063 - The lower bound of the type override map is a search of its own

## Problem

[`SortedSearch`](../../src/SqlSource/SortedSearch.cs) is the one binary search of the generator's sorted lists, exact or a lower bound, and every other search goes through it, each giving it a probe of its own order.  [`TypeOverrideMap`](../../src/SqlSource/Settings/TypeOverrides/TypeOverrideMap.cs) is the one search written out beside it: its `LowerBound` finds the first entry that is not less than a key, as `SortedSearch.LowerBound` does, but its probe would hold a `KeyParts`, a `ref struct` that holds spans of the key's text.  On netstandard2.0 a struct cannot hold a `ref struct`, and a generic helper cannot take one, for that needs `allows ref struct`.  So a change to how the map's entries are searched is made in `LowerBound` and not in `SortedSearch`.

## Why it exists

The generator targets netstandard2.0, which has no `allows ref struct`, and the key of the search is a `ref struct` of spans.

## Impact

Low.  `TypeOverrideMapTests` pins the map's order and matches.  The cost is one more binary search to keep right when the map's order changes.

## Proposed fix

Search the map through `SortedSearch.LowerBound`, with a probe of its own, once either of these holds: the generator can use `allows ref struct` (a newer target), or `KeyParts` holds offsets into the key's text instead of spans, so that it is an ordinary struct a probe can hold.

## Trigger

When the generator moves off netstandard2.0, or the next time `KeyParts` or `TypeOverrideMap`'s search changes.
