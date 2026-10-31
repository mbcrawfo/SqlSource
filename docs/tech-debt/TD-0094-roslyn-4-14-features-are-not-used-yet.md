# TD-0094 - The Roslyn 4.14 features are not used yet

## Problem

Raising the floor of [`src/SqlSource`](../../src/SqlSource) from Roslyn 4.8.0 to 4.14.0 adopted only `AddEmbeddedAttributeDefinition`.  The public API of `Microsoft.CodeAnalysis` and `Microsoft.CodeAnalysis.CSharp` that 4.9 to 4.14 added for incremental pipelines, caching and allocation was not looked at, so the pipeline's steps may cost more time or allocations than 4.14 allows.

## Why it exists

The floor raise was scoped to the workarounds that 4.8.0 forced.  Finding what else 4.14 offers, and measuring each candidate against the allocation budgets, is work of its own.

## Impact

Low.  Nothing is wrong; the pipeline may cost more than it has to, in the compiler and the IDE.

## Proposed fix

List the public API of `Microsoft.CodeAnalysis` and `Microsoft.CodeAnalysis.CSharp` that 4.9 to 4.14 added and that bears on incremental pipelines, caching and allocation.  Check [TD-0013](TD-0013-sql-edit-reads-every-attributed-type-again.md) and [TD-0069](TD-0069-change-to-any-model-runs-every-files-model-node-again.md) against that list.  Adopt a candidate only with a before-and-after measurement against the allocation budgets.

## Trigger

The next performance work on the generator, or the next floor raise.
