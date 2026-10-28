# AGENTS.md - src/SqlSource.Tool/Describing/Postgres/Walk

How the walk reads a plan is [the plan walk](../../../../../docs/architecture/postgres-nullability.md#the-plan-walk).  Every rule here guards against a wrong `false`.

## The plan

- **The walk never answers `false` for a column with an origin on its own**: when in doubt it is the heuristic ([how the walk degrades](../../../../../docs/architecture/postgres-nullability.md#how-the-walk-degrades)).  It degrades a column alone, and the plan whole.  A column without an origin is never mapped.
- **A query that is not gated is planned for its columns of no origin alone**: its columns with an origin keep the catalog's answer whatever the plan says (`WalkScope.Gated`).
- **Every node type, relationship, join type and set-operation command the walk compares is a constant of `PlanVocabulary.cs`**, and the sets of node types named for their purposes are built from them.  They differ on purpose, `PlanInstances`' set operations holding `Recursive Union` and `NullabilityWalk.ReachesSetOperation` not, so never merge them.
- **The walk follows a join's sides and nothing else** (`PlanInstances`, [relation instances](../../../../../docs/architecture/postgres-nullability.md#relation-instances)).  Only scans reached through `Outer` or `Inner` children are instances of the plan's joins; an `InitPlan`, `SubPlan`, `Member` or `Subquery` child is another plan.
- **A plan with a reason of the page's table is unusable**, and leaves every column to the heuristic.  A gated query's plan that fails `output-differs` is read for no expression.
- **`PostgresSession.WalkAsync` builds one `PlanInstancesCache` a walk**, after `partitions`, for `NullabilityWalk.Run` to find the instances of each plan once.
- **A `ModifyTable` at the root is its target, and the scan of the target under it is that same instance**, which is never null-extended ([a root `ModifyTable`](../../../../../docs/architecture/postgres-nullability.md#a-root-modifytable)).  When `OuterJoinGate.NamesOldOrNew` finds `OLD` or `NEW`, a qualified reference at a root `ModifyTable` maps only to the target's own alias.
- **An entry of `Output` is a reference or it is nothing** (`OutputReference`, [output entries](../../../../../docs/architecture/postgres-nullability.md#output-entries)).  A reference to a `Subquery Scan` or a `CTE Scan` is resolved in the scanned plan by the column's origin, and never by its place or its name.  A bare name is only ever the plan's one relation's (`PlanInstances.BareAlias`).

## Expressions

- **A column of no origin is answered from its entry of the root's `Output` as an expression, or not at all** ([expressions](../../../../../docs/architecture/postgres-nullability.md#expressions)).  An entry is read as a whole, never as a prefix, and only in a plan with no set operation.
- **A form is pinned in `tests/DatabaseTests/Postgres/Describer/PlanOutputTests.cs`, as 14 and 18 print it, before it is read.**
- **Where a join null-extends, an entry is read only at the node that computes it and only when that node is preserved.**  An aggregate is read only at the grouped `Aggregate` that computes it, never inside a value a child printed.
- **An operator, a function or an aggregate other than `count(*)` and the rankings is read only by an exact signature of `TotalFunctions` or by a name of `CatalogOverloads`**, a cast only within a family of `SetOperationTypes`.  Any of them, a ranking and a literal's type name are read only when the session finds `pg_catalog` first (`PostgresSearchPath`).  What a row of either table needs is in the table's remarks.

## Set operations

- **A set operation's column of no origin is answered from its inputs** (`NullabilityWalk.SetOperations.cs`, [set operations](../../../../../docs/architecture/postgres-nullability.md#set-operations)).  A `*SELECT* <k>` alias alone proves nothing.  A branch's value counts only when its type carries to the column's (`SetOperationTypes.Carries`).

## Fixtures

- **Every fixture case gives one answer on every supported major, 14 to 18** ([the walk in the fixtures](../../../../../docs/architecture/postgres-nullability.md#the-walk-in-the-fixtures)).  A shape that two majors plan differently is a test of `tests/DatabaseTests/Postgres/Describer/WalkTests.cs` with each version's answer, and not a fixture.
- **`tests/SqlSource.Tool.Tests/Fixtures/WalkMatrixTests.cs` holds what each `walk-*` case must give, by hand**: a recording that disagrees with it is a bug to find, and the table is not changed to fit one.

> Maintenance: this file names specific files, folders, types and members.  If you rename, move, or remove them, update this file in the same commit.
