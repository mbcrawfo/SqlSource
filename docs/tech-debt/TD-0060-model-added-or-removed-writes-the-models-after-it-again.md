# TD-0060 - A model added or removed writes again every model sorted after it

## Problem

In [`SqlSourceGenerator`](../../src/SqlSource/SqlSourceGenerator.cs), the step `Models` gives one [`ModelSet`](../../src/SqlSource/Generation/Models/ModelSet.cs) for the project, its models in the ordinal order of their full names, and `ModelOutput` takes it apart with a `SelectMany` into one node for each model, which `ModelEmitter.Emit` writes.  The compiler matches the nodes a `SelectMany` gives by their place in the list, not by the model's full name.  When a model is added, every model sorted after it moves to the next place, and its node's input is another model's record, so `ModelEmitter.Emit` runs again for it and its source is added again, though nothing about it changed.  When a model is removed, every model sorted after it moves back a place in the same way.  A model sorted before the change keeps its place and is taken from the last run.

A model is added or removed when a query gains or loses its parameters or its rows, when a query, a file or a type that claims a file is added or removed, when a model's name changes by a marker, a suffix or the model namespace, and when a query starts or stops matching a table.  [`CachingTests`](../../tests/SqlSource.Tests/Generator/CachingTests.cs) holds the cost in `Run_ModelAdded_EmitsTheModelsAfterItAgain` and `Run_ModelRemoved_EmitsTheModelsAfterItAgain`.

This is the same cost that [TD-0019](TD-0019-change-to-claimed-files-reads-every-claimed-file-again.md) records for the checks of the sidecars, for the same reason, in another step.

## Why it exists

A model is emitted once for its full name across the project, so its record can only be decided after every type's requests are collected, in one step: that is where two requests of one name are compared, `SQLSRC257`, and where the names are checked against the project and against each other, `SQLSRC258`.  The epic's technical note and sub-phase 5.3's spec chose a `SelectMany` back to one node for each model after that step, so that an edit that changes one model writes that model alone, and accepted that a model added or removed writes again those after it.

## Impact

Low.  Each model sorted after the change is written again and its source added again: on average half of the project's models for each model added or removed.  Writing a model is a small string build, and an edit to a query that keeps its models does not pay it.  The compiler then sees new source for those models, which costs it more than the writing, in a project with many models.

## Proposed fix

Give each model a node that stays with its full name.  One way is to emit from a step whose nodes are keyed by name: for example, a step for each type's requests, `TypeModels` taken apart by a `SelectMany`, whose output is the model record when the type's request is the first of its group and nothing otherwise, read from the collected decision by a search on the full name.  The nodes then follow the types and their requests, which a model added elsewhere does not move.  Check it with `CachingTests`: change `Run_ModelAdded_EmitsTheModelsAfterItAgain` and `Run_ModelRemoved_EmitsTheModelsAfterItAgain` to expect only the model added or removed to be written.

## Trigger

A report of a slow compiler or IDE when queries are added to a project with many models, or the fix of TD-0019, whose approach would serve here too.
