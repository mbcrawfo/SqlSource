# TD-0063 - The lower bound of the type override map is a search of its own

## Problem

[`SortedSearch`](../../src/SqlSource/SortedSearch.cs) is the one exact binary search of the generator's sorted lists: `SqlPath.IndexOf` and `MappedTypeResolver.Find` search through it.  [`TypeOverrideMap`](../../src/SqlSource/Settings/TypeOverrideMap.cs) is the third search that was written out beside them, and it stays apart: its `LowerBound` finds the first entry that is not less than a key, which `SortedSearch` does not offer, over a `KeyParts`, a `ref struct` that holds spans of the key's text.  On netstandard2.0 a generic helper cannot take a `ref struct` as its key, for that needs `allows ref struct`.  So a change to how the map's entries are searched is made in `LowerBound` and not in `SortedSearch`.

## Why it exists

The generator targets netstandard2.0, which has no `allows ref struct`, and the key of the search is a `ref struct` of spans.

## Impact

Low.  `TypeOverrideMapTests` pins the map's order and matches.  The cost is one more binary search to keep right when the map's order changes.

## Proposed fix

Give `SortedSearch` a lower-bound entry, and search the map through it, once either of these holds: the generator can use `allows ref struct` (a newer target), or `KeyParts` holds offsets into the key's text instead of spans, so that it is an ordinary struct a generic helper can take.

## Trigger

When the generator moves off netstandard2.0, or the next time `KeyParts` or `TypeOverrideMap`'s search changes.
