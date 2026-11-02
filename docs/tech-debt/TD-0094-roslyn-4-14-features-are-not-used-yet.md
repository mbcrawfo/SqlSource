# TD-0094 - The Roslyn 4.14 features are not used yet

## Problem

Raising the floor of [`src/SqlSource`](../../src/SqlSource) from Roslyn 4.8.0 to 4.14.0 adopted only the compiler's handling of `Microsoft.CodeAnalysis.EmbeddedAttribute`, whose definition the generator writes itself, since `AddEmbeddedAttributeDefinition` writes it on 4.14 without the generated-code header.  The public API of `Microsoft.CodeAnalysis` and `Microsoft.CodeAnalysis.CSharp` that 4.9 to 4.14 added for incremental pipelines, caching and allocation was not looked at, so the pipeline's steps may cost more time or allocations than 4.14 allows.

The floor raise also lets a collection expression target `ImmutableArray<T>`, and the style rules IDE0301, IDE0303, IDE0304 and IDE0305 now flag the generator's existing `ImmutableArray.Create`, `Empty`, builder and `ToImmutableArray()` calls.  The `[src/SqlSource/**.cs]` section of [`.editorconfig`](../../.editorconfig) lowers those four rules to suggestions, so the calls stay as they are.

## Why it exists

The floor raise was scoped to the workarounds that 4.8.0 forced.  Finding what else 4.14 offers, and measuring each candidate against the allocation budgets, is work of its own.

## Impact

Low.  Nothing is wrong; the pipeline may cost more than it has to, in the compiler and the IDE.  The override keeps IDE0301 to IDE0305 silent in `src/SqlSource`, so new `ImmutableArray` code is not nudged toward collection expressions.

## Proposed fix

List the public API of `Microsoft.CodeAnalysis` and `Microsoft.CodeAnalysis.CSharp` that 4.9 to 4.14 added and that bears on incremental pipelines, caching and allocation.  Check [TD-0013](TD-0013-sql-edit-reads-every-attributed-type-again.md) and [TD-0069](TD-0069-change-to-any-model-runs-every-files-model-node-again.md) against that list.  Adopt a candidate only with a before-and-after measurement against the allocation budgets.  Convert the `ImmutableArray` calls that the four style rules flag only with the same measurement, call by call on the hot paths, and when every flagged call is converted or kept on purpose, delete the override from `.editorconfig`.

## Trigger

The next performance work on the generator, or the next floor raise.
