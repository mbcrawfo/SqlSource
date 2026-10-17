# TD-0053 - Duplicated and uneven patterns in the PostgreSQL describer

## Problem

A maintainability review of phase 3 on 2026-10-16 found these in `src/SqlSource.Tool/Describing/Postgres/` and `Describing/Capture/`.  No answer is wrong; each copy is one more place a change has to reach.  The items keep the review's numbers, which the designs of the fix in `docs/superpowers/specs/` use.

4. **A plan's node types and relationships are literals in four files.**  `"Append"`, `"Merge Append"`, `"SetOp"`, `"Recursive Union"`, `"Subquery Scan"`, `"ModifyTable"`, `"Subquery"`, `"InitPlan"`, `"Member"`, `"Outer"` and `"Inner"` are spelled in [`PlanInstances`](../../src/SqlSource.Tool/Describing/Postgres/Walk/PlanInstances.cs), `PlanPartitions`, [`NullabilityWalk`](../../src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.cs) and [`NullabilityWalk.SetOperations`](../../src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.SetOperations.cs), some as private constants of one file and literals of another.  `VisitedNode.NullExtends` is a string, `"none"` compared as a constant in `PlanInstances` and as a literal in `NullabilityWalk`.
5. **Tree helpers written three times.**  `All(PlanNode)` is in `PlanInstances`, `PlanPartitions` and [`PlanRelations`](../../src/SqlSource.Tool/Describing/Postgres/Walk/PlanRelations.cs); the `Subquery` child of a `Subquery Scan` is found in `PlanInstances.ChildOf` and `SetOperations.LeafBranchOf`; `PlanRelations.Of` and `PlanPartitions.MembersOf` end in the same distinct sort by schema and name.  `PlanInstances` is built by `MayAnswerFromBranches` and again by `NullabilityWalk.Run`, and the empty partitions dictionary is made three times.  `NullabilityWalk.Expressions.NullableAt` and `Preserved` open with the same depth guard and child filter.
6. **Hand-kept type tables.**  The ten types that carry a modifier are listed in [`ColumnFacets`](../../src/SqlSource.Tool/Describing/Postgres/ColumnFacets.cs), [`TypeModifiers`](../../src/SqlSource.Tool/Describing/Postgres/TypeModifiers.cs) and `PostgresTypeNames.InCatalog`; built-in OIDs are bare numbers in `PlanExpression.TypesByName` and [`SetOperationTypes.FamilyOf`](../../src/SqlSource.Tool/Describing/Postgres/Walk/SetOperationTypes.cs), which no test holds to `pg_type`.  [`PostgresTypes`](../../src/SqlSource.Tool/Describing/Postgres/PostgresTypes.cs) switches on `typtype` three times, in both `Build`s and in `PairsOf`, which must ask for exactly the pairs `Build` will look up; the "no row for type" lookup is `PostgresTypes.Row`, [`ColumnTypes.RowOf`](../../src/SqlSource.Tool/Describing/Postgres/ColumnTypes.cs) and [`PostgresColumns.FacetsOf`](../../src/SqlSource.Tool/Describing/Postgres/PostgresColumns.cs).
7. **`PostgresSession` mixes the flow of a describe with its call wrappers and builders**, about 870 lines.  The time-ask-log pattern of a call is written ten times, six as wrappers and four inline, with the call names, which are a contract with the captures, as bare literals.  `ColumnOrigin.MissingFrom` must run before `ColumnOrigin.Find`, and `MissingType` before `PostgresTypes.Build`, by statement order alone.  `WalkAsync` and `RunSetOperation` take seven parameters.  `VersionOf` is used only by `PostgresLive`.
8. **Small repeats.**  "The answer to 'prepare' holds no types and no error." thrown twice; the `bool?` to `LogValue` conversion six times in [`PostgresEvents`](../../src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs), for want of `LogValue.Of(bool?)`.  In [`PostgresLive`](../../src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs) the three catches of a driver's refusal are pasted in `CheckAsync` and `RefusalOfAsync`; the `oid[]` and the schema-and-name parameters are built twice each; `tables` and `types` bind by name and `relations`, `partitions` and `spellings` by position; and `OpenSql` and `SettingNames` are matched by index.
9. **Members only tests use.**  The one-argument `PlanInstances.Find`, `RowDescriptions.Resolve` and `BatchRewrites.Resolve`, and `WalkScope` without `ColumnTypes`.
10. **[`CaptureJson.Options`](../../src/SqlSource.Tool/Describing/Capture/CaptureJson.cs) can be changed by any caller** before its first use, though a change to it changes every fixture.

## Why it exists

The describer grew over six sub-phases and four follow-ups, each adding the reader or the call it needed beside the last; nothing was wrong enough to stop for.

## Impact

Low.  4 is where a fix to one copy leaves another behind: a node type changed in one file and not the others gives a different answer in one step of the describe.

## Proposed fix

4. `PlanNodeTypes` and `PlanRelationships` constants, or predicates on `PlanNode`, and a `NullExtension` enum written as text only by `PostgresEvents`.
5. `PlanNode.Descendants()` and `PlanNode.SubqueryChild`, one sort of relation names, one cache of `PlanInstances` per walk, and a shared guard for `NullableAt` and `Preserved`.
6. A `BuiltInTypes` class with named OIDs and the set of types with a modifier, driving `PlanOutputTests`' check of the cast families; `PairsOf` derived from `Build`; `PostgresTypes.Row` everywhere.
7. One generic `AskAsync(method, request, live, logEvent)` and constants for the call names; `ColumnOrigin.FindAll` returning the origins or the missing message; `VersionOf` in `PostgresLive`.
8. `LogValue.Of(bool?)`, a `when (IsDriverRefusal(e))` filter, `OidArray` and `AddRelations` helpers, one parameter style, and `OpenSql` built from `SettingNames`.
9. Remove them, or say in each why a test needs it.
10. `MakeReadOnly()` in its initializer.

## Trigger

A new node type the walk follows (4), or a new call of the describer (7); otherwise before phase 6 adds a second engine's describer beside this one.
