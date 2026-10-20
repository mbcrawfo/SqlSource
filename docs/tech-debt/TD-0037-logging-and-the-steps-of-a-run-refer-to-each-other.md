# TD-0037 - Logging and the steps of a run refer to each other

## Problem

`Logging/` references `Describing`, `Planning` and `Projects`, which call back into it.  [`RunEvents`](../../src/SqlSource.Tool/Logging/RunEvents.cs) takes `QueryWork`, `PlannedFile`, `ProjectEvaluation` and the like, and those steps call `RunEvents`: the namespaces depend on each other in a circle.

## Why it exists

It is the shape the events took when `RunEvents` became the one place that builds them.  Building each event in one place gave it one set of members wherever it is written, and the types it reads were already in the other namespaces.

## Impact

Low.  The circle costs nothing to run, but it makes a change to either side harder to place: an event cannot be read or moved without the types of three other namespaces, and none of them can be taken out of the tool without it.

## Proposed fix

Give `RunEvents` the plain data it needs, or move the events that take a type of another namespace to the namespace that owns the type.

## Trigger

A change that moves an event or a record across these folders, or the next maintainability review.
