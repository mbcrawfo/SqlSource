# TD-0063 - The lower bound of the type override map is a search of its own

## Problem

[`TypeOverrideMap`](../../src/SqlSource/Settings/TypeOverrideMap.cs) finds the first entry that is not less than a key with its own `LowerBound`, a binary search written out again beside [`SortedSearch`](../../src/SqlSource/SortedSearch.cs), which every other sorted list of the generator is searched through.  `LowerBound` cannot use it: the key is a `KeyParts`, a `ref struct` that holds spans of the key's text, and on netstandard2.0 a generic helper cannot take a `ref struct` as its key, for that needs `allows ref struct`.  A change to how a sorted list is searched has to reach both.

## Why it exists

The generator targets netstandard2.0, where `allows ref struct` is not available, and a key of spans keeps the search from allocating a string per probe.  A second short loop was the smaller cost.

## Impact

Low.  The two searches agree today and `TypeOverrideMapTests` pins the map's order and matches.  The risk is only that a change to one is not made in the other.

## Proposed fix

Search through `SortedSearch` once either of these holds: the generator can use `allows ref struct` (a newer target), or `KeyParts` holds offsets into the key's text instead of spans, so that it is an ordinary struct a generic helper can take.

## Trigger

When the generator moves off netstandard2.0, or the next time `KeyParts` or `TypeOverrideMap`'s search changes.
