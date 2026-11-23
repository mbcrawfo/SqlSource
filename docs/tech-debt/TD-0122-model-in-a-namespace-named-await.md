# TD-0122: A model in a namespace with a part named await is written without its @

## Problem

[`ModelCollector.TypeTextOf`](../../src/SqlSource/Generation/Models/ModelCollector.cs) writes a model's name from `global::` with its namespace as it is.  A namespace part named `await` needs no `@` where it is declared, but an asynchronous method cannot use it as an identifier, so a method that names such a model fails to compile with CS4003.

## Why it exists

The text of a model's type is shared by every place that names the model, and only the body of an asynchronous method needs the `@`.  A review of the static connection access found it, and the owner chose to record it.

## Impact

Low.  A namespace needs a part named `await`, and the build fails at the method, with an error that names `await`.  Renaming the part is the workaround.

## Proposed fix

Write `@await` for each namespace part named `await` in `TypeTextOf`, as the access of a static connection source does, and add a compile test of a query method with a model in such a namespace.  Remove the limit "A model in a namespace with a part named `await`" from `site/reference/query-methods.md` with its row of `site/guides/troubleshooting.md`.

## Trigger

A user report of CS4003 in a generated query method, or the next change to how a model's type is written.
