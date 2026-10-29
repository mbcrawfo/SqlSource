# TD-0091 - `describe --check` does not compare a column's origin

## Problem

[`SidecarComparer.FindDifference`](../../src/SqlSource/Snapshot/SidecarComparer.cs), which `describe --check` compares each entry with, compares `hash`, `engine`, `database`, `resultKind`, `matchesTable` and each parameter's and column's `name`, `ordinal`, `type` and `nullable`, and never a column's `origin`.  The generator matches the keys of `columnTypes` against `origin`, in `QueryTyper.SourceTypeOf` through `ColumnTypeMap.Find`, so `origin` decides a column's C# type whenever a column type names it.

A table moved to another schema that the `search_path` still finds is one way it shows: the SQL, the hash and the types stay the same, so `--check` passes, but a `describe` writes a new `origin`, a `columnTypes` key qualified by the old schema stops matching, and the generated property's type changes.  [The sidecar format](../architecture/sidecar-format.md#what---check-compares) records that `origin` is not compared.

## Why it exists

The format design classed `origin` as informational and left it out of the comparison.  `columnTypes`, which reads it, came later, and the comparison was not revisited.  The review of TD-0084 found it, and the owner chose to record it and fix it in a unit of its own.

## Impact

Medium.  A build server's `--check` passes on a sidecar that a `describe` would change in a way that changes generated code, which is what `--check` exists to catch.  It needs a column type that names the column, and a database whose schema moved the column's table or renamed the table or the column without changing the query's result.

## Proposed fix

Compare each column's `origin` (`schema`, `table`, `column`) in `SidecarComparer.FindDifference`, after `nullable`, with a test in `SidecarComparerTests` for each part.  Update [what --check compares](../architecture/sidecar-format.md#what---check-compares), the remarks of `SidecarComparer`, `src/SqlSource/Snapshot/AGENTS.md`'s rule on `SidecarComparer`, and the bullet of the manual's [check in CI](../manual/tool.md#check-in-ci) on what it compares, and remove the known issue of `docs/manual/tool.md`.

## Trigger

The unit `check-origin` of the tech-debt run that recorded it, or a report of a `--check` that passed on a sidecar whose column types changed after a `describe`.
