# TD-0069 - A change to any model runs every file's model node again

## Problem

Each model is emitted from its file's `ParsedFile` node, as [the fan-out by file](../architecture/generator.md#the-fan-out-by-file) describes.  In `RegisterModelSources` of [`SqlSourceGenerator`](../../src/SqlSource/SqlSourceGenerator.cs), the path of each file is combined with `ModelsByFile`, the arrangement of every model of the project, and the step `FileModelSlots` finds the file's slots with `ModelsByFile.SlotsOf`.  So when any model changes, `ModelsByFile` changes and `FileModelSlots` runs again for every claimed file: a search, and a compare of the file's slots with the last run's.  For a file whose slots are the same, nothing after it runs, so the edit still writes one model.  But the driver's tables for those nodes cost more.  At 500 files of one model each, in a `Release` build, an edit to one model's column allocates about 2920 KB per run and takes about 3.3 ms, against 2660 KB and 2.8 ms before the fan-out by file, which emitted the models from a `SelectMany` of the sorted `Models`.  Of the 260 KB more, `ModelsByFile.Arrange` allocates about 57 KB, and the per-file nodes the rest.

## Why it exists

A node of the pipeline can only see a collected value whole.  Which file emits a model, and which models there are, is decided once for the project, in `Models`, since a model is shared across files and types by its full name.  A file's node therefore depends on the whole decision, and the driver runs every such node again when it changes.  The per-file nodes are what keep a model added or removed from writing the models of other files, which a `SelectMany` of the sorted models did.

## Impact

Low.  The cost grows with the number of claimed files and is paid on each edit that changes a model, in the compiler and the IDE.  At 500 files it is about 260 KB and half a millisecond per run.  No model is written again for it.

## Proposed fix

Roslyn has no way to make an item of a values provider depend on part of a collected value, so the per-file run cannot be avoided while the decision is collected.  First measure, with a memory profiler on a project of 500 files of one model each, which of the tables between `ParsedFile` and `ModelOutput` cost the most.  Then try fewer nodes on that path: for example, select the path inside the step that combines it with `ModelsByFile`, rather than in a step of its own, keeping the `SelectMany` of the slots and then of each slot's models, which keep a model's node in place.  Keep a change only if every case of `CachingModelTests` holds.  If no change pays, accept the cost and close this item.

## Trigger

A project with thousands of claimed files where the models' steps show in the IDE's allocations, or a Roslyn version that can key a node on part of a collected value.
