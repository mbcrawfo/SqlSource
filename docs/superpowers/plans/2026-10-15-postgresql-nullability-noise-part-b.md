# The PostgreSQL describer's nullability noise, part B: the walk's gaps - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The walk answers a column through a `Subquery Scan` or a `CTE Scan`, through a partitioned table's `Append`, through a `Foreign Scan`, and as a bare name at a root that is not the scan; a bare name at a `ModifyTable` keeps the heuristic only under a join that null-extends; and part A's expressions are answered in a plan with an outer join where the walk can follow the value to a node that no join extends.  Nothing else changes.

**Architecture:** `PlanInstances` learns three more kinds of instance: a relation scan as today (now with `Foreign Scan`), a partitioned table (an `Append` or `Merge Append` whose members are partitions of one table by `pg_partition_ancestors`), and a scan of another plan (a `Subquery Scan`'s `Subquery` child, a `CTE Scan`'s `InitPlan`).  `NullabilityWalk` resolves a reference to a scan of another plan by the column's origin, walking that plan as a plan of its own, and follows an expression of the root down through the `Output`s that print it to the node that computes it.  The session asks a new call, `partitions`, between `explain` and the walk, only for a plan with an `Append` of that shape, and `explain` runs with `enable_partition_pruning = off`.

**Tech Stack:** C# (`net8.0` tool), xunit v3 with Shouldly, Npgsql, Testcontainers PostgreSQL 14 and 18, `postgres_fdw`.

**Spec:** `docs/superpowers/specs/2026-10-15-postgresql-nullability-noise-design.md`, part B, and the step of part A that part B lifts.

## Global Constraints

- **Never a wrong `false`.**  Anything the code does not recognise keeps the answer it has today.  Each shape is trusted only after a container test on 14 and 18 shows what the server prints for it.
- **Every diff of an expectation is one of three:** a `true` or `null` turning into `false`; a changed `nullableSource` (`heuristic` to `outer-join`, `catalog`, or `foreign-table`); `plan` going from `not-needed` to `walked` or `unavailable`.  Anything else is a bug: stop and find it.
- **Expectations are recorded, never edited by hand:** `tools/record-fixtures.sh`, and `tools/record-realistic.sh` twice, plain and with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14`.  Read every diff before committing it.
- **Every commit passes `./pre-commit-validation.sh`**; a task that changes answers also runs `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres` first.
- **The pull request holds 150 files or fewer.**  The estimate is about 60: no fixture capture gains a member, since `partitions` is a call of its own that the fixture schema never needs.
- `AGENTS.md`, `README.md` and `docs/tech-debt/` are kept true in the commit that changes what they say.

## What the servers print

Probed on 2026-10-15, `EXPLAIN (VERBOSE)` of `EXECUTE` under `force_generic_plan`, the same on 14 and 18 but for the window's name.

1. **A `Subquery Scan` prints its columns in the order the outer query needs them, not in the subquery's.**  `parameter-in-cte`: the scan `ro` prints `ro.order_count, ro.last_placed_at, ro.account_id`, and its child `o.account_id, count(*), max(o.placed_at)`.  So the spec's "the scan's `n`th output maps to the `n`th of its child" holds for no shape that can be told from the text, and **no column is mapped by its position** (my call).  A column with an origin is mapped by that origin instead (Task 3); one without an origin through a scan stays unknown and goes to TD-0051.
2. **A `CTE Scan`** has `CTE Name`, and its plan is the one `InitPlan` of the whole tree whose `Subplan Name` is `CTE <name>` (`cte-used-twice`: two scans `r` and `n` of one `InitPlan`, `n` under a `Hash Left Join`).
3. **A partitioned table under a parameter's condition prints an `Append` with no members**, `Subplans Removed: 2`: the `NULL` of each parameter prunes every partition at executor start.  Under `enable_partition_pruning = off` the members show, `Seq Scan on public.events_2024 e_1` and `... events_2025 e_2`, with identical `Output`s but for the alias, and the nodes above print `e.id`.  A scan of no column prints no `Output`.
4. **A value computed below prints in one pair of parentheses at every node above it**, and as itself at the node that computes it: `('x'::text)` at a `Limit`, a `Sort` and a `Hash Right Join`, `'x'::text` at the `Seq Scan on orders o` under the join's extended side; `COALESCE(o.note, 'z'::text)` at the join that computes it and `(COALESCE(...))` above.  A constant the root computes prints bare at every node over it.  `Append`, `Merge Append` and `Recursive Union` print no `Output`.
5. **A plan of one range table entry prints bare names at every node:** `Limit`, `Sort`, `GroupAggregate` over `Seq Scan on public.users u`, `Output: id, email, count(*)`.
6. **A `Foreign Scan` of one foreign table** has `Relation Name` and `Alias`; one of a join pushed down has neither, and `Relations` instead.  `postgres_fdw` pushes down no join that needs a `PlaceHolderVar` computed inside it.
7. **A bare name at a `ModifyTable` with a join under it:** `UPDATE ... FROM users u LEFT JOIN users b ON false ... RETURNING o.id, b.id` prints `o.id, id`, with a `Result` under a `Nested Loop Left Join`.

## Decisions (mine)

- A column through a scan of another plan is mapped by its origin: the child plan's `Output` entries that are references to the origin's relation and column are its candidates, and it is answered only when every candidate agrees (Task 3).  It is sound because a column with an origin is a plain reference in the subquery, so its entry is among the candidates; it needs no position and no name of the subquery's.
- `partitions` is a call of its own, as the spec says, asked only when the plan has an `Append` whose members are all relation scans under aliases `<p>_<n>`.  It answers the ancestors of each member, by `pg_partition_ancestors`.
- `explain` sets `enable_partition_pruning = off` for its transaction, a fixed setting and no member of the request, so no capture's request changes.  The cost, a plan of every partition, is TD-0052.
- The expression trace answers only where today's rule does not: today's answers stay as they are.
- Found while building Task 4: the node that computes an entry may take a value in parentheses from a child under the extended side, `CASE ... THEN ('x'::text) ...` over an anti join, so a span in parentheses that a child computes where a join extends is read as unknown (`PlanExpression.NullableOf`'s `groupPreserved`); a `COALESCE` with a constant beside it is still `false`.

## Files

| File | Change |
|----|----|
| `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs` | `PostgresPartitionsRequest`, `PostgresPartitionsAnswer`, `PostgresRelationName`, `PostgresPartitionRow` |
| `src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs` | `PartitionsAsync`; `enable_partition_pruning = off` in `explain` |
| `src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs` | `postgres.partitions` |
| `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs` | Ask `partitions` between `explain` and the walk |
| `src/SqlSource.Tool/Describing/Postgres/PlanInstances.cs` | Instance kinds; partitioned `Append`; `Foreign Scan`; scans of other plans; the bare alias; the `ModifyTable` rule; each node's extension |
| `src/SqlSource.Tool/Describing/Postgres/PlanPartitions.cs` | Create: which `Append`s need `partitions`, and the members' ancestors |
| `src/SqlSource.Tool/Describing/Postgres/NullabilityWalk.cs` | Resolve through scans; the expression trace; `WalkScope.Partitions` |
| Tests: `PlanInstancesTests`, `NullabilityWalkTests`, `PlanPartitionsTests` (new), `PostgresSessionTests`, `PostgresEventsTests`, `PostgresLiveTests` or the existing test of `SetTexts` | Unit |
| `tests/SqlSource.Tests.Postgres/Describer/PlanOutputTests.cs`, `WalkTests.cs`, `ScanTests.cs` (new), `ExpressionTests.cs` | Container |
| `tests/SqlSource.Tests.Postgres/Realistic/Describer/RealisticNullabilityNoiseTests.cs` | Lower `Budgets` |
| Recorded | Realistic expectations and the fixtures that change |
| Docs | `src/SqlSource.Tool/AGENTS.md`, `src/SqlSource.Tool/README.md`, TD-0046, TD-0051, TD-0052 (new), `docs/tech-debt/README.md` |

---

### Task 1: Pin what the servers print

**Files:** `tests/SqlSource.Tests.Postgres/Describer/PlanOutputTests.cs`

- [ ] **Step 1:** Add container tests that read `EXPLAIN (VERBOSE, FORMAT JSON)` on the fixture schema, one for each fact above that code will rest on:
  - `Explain_SubqueryScan_PrintsItsColumnsInTheOrderTheyAreUsed`: `SELECT s.total, s.id FROM (SELECT o.id, o.total, row_number() OVER () AS n FROM orders o) s WHERE s.n > 1`: the scan prints `s.total, s.id` and its child `o.id, o.total, ...`.  This is the evidence for refusing positions.
  - `Explain_CteScan_NamesTheInitPlanOfItsCte`: a CTE used twice; both scans' `CteName` and one `InitPlan` whose `SubplanName` is `CTE <name>`.
  - `Explain_PartitionedTableWithoutPruning_PrintsEveryMember`: with `SET LOCAL enable_partition_pruning = off` and a parameter's condition on the key, an `Append` of `Member` scans aliased `<alias>_1`, `<alias>_2`, and `<alias>.id` above it.
  - `Explain_ValueComputedBelow_IsParenthesisedAboveIt`: the `LEFT JOIN` of a subquery with a constant, under `ORDER BY ... LIMIT`: the constant is `('x'::text)` at the `Limit`, the `Sort` and the join, and `'x'::text` at the scan.
- [ ] **Step 2:** Run on 18 and 14; PASS on both.  Commit: "Pin what the plan prints of scans, partitions and values computed below".

### Task 2: The call `partitions`, and `explain` without partition pruning

**Files:** `PostgresCalls.cs`, `PostgresLive.cs`, `PostgresEvents.cs`, `PlanPartitions.cs` (new); tests of each.

**Interfaces:**

```csharp
internal sealed record PostgresRelationName(string Schema, string Name);
internal sealed record PostgresPartitionsRequest(IReadOnlyList<PostgresRelationName> Relations); // sorted, each once
internal sealed record PostgresPartitionsAnswer(IReadOnlyList<PostgresPartitionRow>? Rows, PostgresServerError? Error);
// Ancestors: pg_partition_ancestors of the relation without itself, nearest first; empty for one that is no partition.
internal sealed record PostgresPartitionRow(string Schema, string Name, IReadOnlyList<long> Ancestors);

internal static class PlanPartitions
{
    // The relations of the members of every Append or Merge Append in the tree, under any relationship, whose
    // members are all relation scans with a schema, a relation and an alias <p>_<n> of one <p>; sorted, each once.
    public static IReadOnlyList<PostgresRelationName> MembersOf(PlanNode root);
}
```

- [ ] **Step 1: Failing tests.**  `PlanPartitionsTests`: an `Append` of two `Member` scans `e_1`, `e_2` gives both relations; a member that is a `Subquery Scan`, an alias without `_n`, or two prefixes give none; an `Append` inside a `Subquery` child counts.  `PostgresEventsTests`: `postgres.partitions` writes the relations, the rows or the error, and the duration.  The test of `SetTexts`: `force_generic_plan`, then `enable_partition_pruning = off`, then the request's settings.
- [ ] **Step 2: Implement.**  `PartitionsSql`: `unnest` of two text arrays joined to `pg_namespace` and `pg_class` by name, with `ARRAY(SELECT a.relid FROM pg_partition_ancestors(c.oid) WITH ORDINALITY AS a (relid, n) WHERE a.relid <> c.oid ORDER BY a.n)`, ordered by schema and name; a relation that no longer exists has no row.  `ExplainAsync` sets `SET LOCAL enable_partition_pruning = off` after `force_generic_plan`; the remark says why (fact 3).
- [ ] **Step 3:** The unit tests pass.  Commit: "Ask the ancestors of a plan's partition scans, and plan every partition".

### Task 3: Instances of three kinds, and the walk through them

**Files:** `PlanInstances.cs`, `NullabilityWalk.cs`, `PostgresSession.cs`; `PlanInstancesTests`, `NullabilityWalkTests`, `PostgresSessionTests`.

**Interfaces:**

```csharp
internal sealed record WalkScope(bool Gated, bool OldAndNewMayBeNamed, bool CatalogFunctionsFirst,
    IReadOnlyDictionary<long, PostgresTableRow> Tables,
    IReadOnlyDictionary<PostgresRelationName, IReadOnlyList<long>> Partitions);   // empty when not asked

internal enum InstanceKind { Relation, Partitioned, Plan }
internal sealed record PlanInstance(InstanceKind Kind, bool NullExtended, string? Schema, string? Relation,
    IReadOnlyList<long> Parents, PlanNode? Child);
// PlanInstances: Find(PlanNode root, PlanNode tree, partitions); TryFind(alias, out PlanInstance);
// BareAlias (replaces RootNamesAnInstance); IsExtended(PlanNode); HasSetOperation now false for a partitioned Append.
```

Rules, each with unit rows of `NullabilityWalkTests` or `PlanInstancesTests`:

- **Relation:** as today, with `Foreign Scan` among the relation scans.  A foreign table's column is `null foreign-table` when mapped and not extended, `true outer-join` when extended.
- **Partitioned:** an `Append` or `Merge Append` whose children are all `Member` relation scans aliased `<p>_<n>`, each in `scope.Partitions` with a non-empty intersection of ancestors, and whose members' `Output`s are equal once each member's alias is read as `<p>` (absent in all counts as equal).  Its alias is `<p>`, its `Parents` the common ancestors, nearest first; it is no set operation, and its members are not visited.  Anything else stays a set operation, as today.
- **Plan:** a `Subquery Scan` with its one `Subquery` child, or a `CTE Scan` with the one `InitPlan` of the whole tree named `CTE <CteName>` (none or two: no child, so the column stays unknown).
- **A reference to a plan instance:** extended → `true outer-join`.  Otherwise the child is walked as a plan of its own (its own `PlanInstances`, cached per node, to a depth of 16), and the column is resolved by its origin: for every entry of the child root's `Output` that parses as a reference, a bare name or an alias that is not the child's instance ends it unknown; an entry naming a relation instance of the origin's schema and name, or a partitioned instance whose `Parents` hold the origin's OID, with the origin's column name, is a candidate with that instance's extension; a plan instance is resolved the same way, recursively, extended if it or its result is.  A child with a `Reason` or a set operation is unknown.  All candidates preserved → the catalog's answer; all extended → `true outer-join`; none or mixed → the heuristic.  Unit rows: the row-number dedupe; `SELECT s.total, s.note FROM (SELECT o.note AS total, o.total AS note ...)` read uncrossed; a self-join `a1.id, a2.id` through a scan stays heuristic; a CTE used twice, one scan under a `Left` join; a nested scan; a child reference to an outer alias (`LATERAL`) stays heuristic.
- **The bare alias:** the root's own alias as today, or the one relation scan the root reaches through nodes with exactly one child, each `Outer`; a `ModifyTable` root's alias unless a visited node null-extends (was: unless any join).
- **`OutputFits`** compares only relation and partitioned instances; a plan instance prints the subquery's column names.
- **`ProvedNotNull`** of part A finds a partitioned instance's table by the first of its `Parents` that `scope.Tables` holds; a plan instance is never proved.

- [ ] **Step 1:** Failing unit tests for each rule above.
- [ ] **Step 2:** Implement.  The session asks `partitions` after a plan is read and before the walk, when `PlanPartitions.MembersOf` is not empty; an error or a missing row leaves `Partitions` without that relation.
- [ ] **Step 3:** Unit tests pass.  Commit: "Walk a column through a scan of another plan, a partitioned table and a foreign table".

### Task 4: Follow an expression to the node that computes it

**Files:** `NullabilityWalk.cs`, `NullabilityWalkTests`.

- [ ] **Step 1: Failing tests.**  `'k'::text` and `COALESCE(o.note, 'z'::text)` printed bare at a `Hash Right Join` root are `false expression`; `('x'::text)` at the same root, found as `'x'::text` in the extended side's scan, stays `null no-origin`; `(count(*))` at a `Nested Loop` `Left` over an `Aggregate` on the inner side stays unknown; `('x'::text)` at a `Sort` over a `Hash Right Join` that prints it, over the scan, follows through both; a child with no `Output` under a join that extends stays unknown; a constant at the root over an `Anti` join is answered.
- [ ] **Step 2: Implement** `Preserved(node, entry)`: for each `Outer` or `Inner` child, a child without an `Output` must be preserved with no node under it that null-extends; a child whose `Output` holds the entry, or, for an entry wrapped whole in one pair of parentheses, the entry without them, is followed; when no child holds it, the node computes it and must be preserved.  An entry is answered when today's rule answers it, or when `Preserved(root, entry)`; a set operation still answers none.
- [ ] **Step 3:** Pass.  Commit: "Answer an expression computed where no join null-extends".

### Task 5: Against the servers, and recorded

- [ ] **Step 1:** `ScanTests` (container, 14 and 18): the dedupe, the crossed names, the self-join through a scan, a CTE used twice, a partitioned table on each side of a `LEFT JOIN` under a parameter, a `UNION ALL` of two partitions with different columns that stays unknown, a `Foreign Scan` through `postgres_fdw` on each side, a bare root over one scan, and the expressions of Task 4.  `WalkTests`: the partition tests move to their new answers.
- [ ] **Step 2:** Re-record the fixtures and the realistic expectations on 18 and 14, read every diff against the Global Constraints, and lower `Budgets`.
- [ ] **Step 3:** Docs: TD-0046 holds only `Custom Scan` and `OLD`/`NEW`, its "not whole" bullets rewritten; TD-0051's counts and kinds (a column of no origin through a scan, fact 1); TD-0052 for planning every partition; the README of the tech debt; `src/SqlSource.Tool/AGENTS.md` (the calls, the order, the walk); `src/SqlSource.Tool/README.md` where it says what the walk answers.
- [ ] **Step 4:** `./pre-commit-validation.sh` and the 14 run.  Commit: "Record the answers part B gives".

### Task 6: Check the branch

- [ ] `git diff --stat main...HEAD | tail -1` ≤ 150; every expectation diff read; the version check of `AGENTS.md`; open the pull request with autofix on.
