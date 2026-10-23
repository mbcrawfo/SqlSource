# D-0005 - Bracketed type override keys

## Planned

Phase 6 of the [query generation epic](../superpowers/specs/2026-10-07-query-generation-epic-design.md) was to decide whether a key of the type override may be written in T-SQL's bracket form, `[dbo].[OrderStatus]=MyApp.OrderStatus`, as a SQL Server user writes a name everywhere else, beside the plain form, `dbo.OrderStatus`, and the double-quoted one, `"dbo"."OrderStatus"`, which [sub-phase 5.2](../superpowers/specs/2026-10-18-query-generation-phase-5-2-type-map-design.md) settled for every engine.

## Delivered instead

[Sub-phase 6.4](../superpowers/specs/2026-10-20-query-generation-phase-6-4-type-map-design.md) refuses a bracketed key, as the grammar of `Settings/TypeOverrides/TypeKeyText.cs` and `Settings/TypeOverrides/TypeOverrideGrammar.cs` already does, and the help of the error shows the plain and the quoted forms, which mean what they mean for PostgreSQL: a plain part compared ignoring case, a quoted part compared exactly.

## Why deferred

The owner chose not to take brackets for now.  `[]` after a key already means an array, and `TypeOverrideGrammar` reads `[`…`]` as one run when it splits an MSBuild list, so brackets would make the grammar harder to read and to explain for a form that only one engine uses and that two other forms already cover.

## Remaining work

A bracketed part, `]]` for a bracket inside it, in `TypeKeyText.TryReadPart` and the list splitter, without changing what a plain, a quoted or an array key means: decide whether a bracketed part is compared exactly, as a double-quoted one is, or ignoring case, as SQL Server compares names by default; tests for each form at each level of the override, and a key such as `[int[]]` that mixes the two meanings of brackets; the manual's override section and its known issue.

## Trigger

Users of SQL Server asking to write override keys as they write names in T-SQL.
