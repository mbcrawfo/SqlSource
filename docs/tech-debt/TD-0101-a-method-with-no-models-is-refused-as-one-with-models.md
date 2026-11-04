# TD-0101 - A query method with no models is refused as one with models

## Problem

Two rules of SQLSRC123 refuse a `codegen` query method because the type's models are internal, which such a method could not take or return (CS0050, CS0051), in [`PlacementChecks.VisibilityReason`](../../src/SqlSource/Generation/Placement/PlacementChecks.cs):

- a `public` method in the extension class of a type that is not public, or inside a type that is not;
- a `public` or `protected` method on the type or in its nested class when the type, or a type around it, is `protected` or `protected internal` and none of them is `internal`, `private` or `private protected`.

Both refuse a query whose method takes no model, one with no parameters and no rows such as `DELETE FROM users;`, whose method would compile.  The file then loses its members and models for the type.

## Why it exists

The rules run in the per-type placement analysis ([`TypePlacement`](../../src/SqlSource/Generation/Placement/TypePlacement.cs)), which works from a type's parsed files and their settings, before the models are known: a query's models come from its sidecar entry, and `src/SqlSource/AGENTS.md` keeps the sidecars out of `TypeOutput`'s inputs.  The owner chose to record the over-refusal rather than move the models part of both rules into the method planner, which sees the models.

## Impact

Low.  It needs a `codegen` query with neither parameters nor rows on a type that is not public, given a public or protected visibility.  Writing `-- visibility: internal` on the query gets past it, and the manual says so.

## Proposed fix

Keep the placement rule for the visibility alone, and move the models part into [`MethodPlanner`](../../src/SqlSource/Generation/Methods/MethodPlanner.cs), which has each query's models: refuse a method only when it takes or returns a model that is internal, reporting SQLSRC123 there and leaving the method out, not the file.  Update the SQLSRC123 section of `docs/manual/diagnostics.md`, the visibility section of `docs/manual/settings.md` and its known issue, the placement section of `docs/architecture/generator.md`, and the test `Run_MethodWithNoModelsOfATypeReachableAsProtected_ReportsSqlsrc123`, then resolve this item.

## Trigger

A report from a user whose query method with no models is refused, or the next change to where SQLSRC123 is decided.
