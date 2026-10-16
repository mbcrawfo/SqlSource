# The PostgreSQL describer's nullability noise, part C: set operations - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A column of no origin whose value comes from a `UNION`, an `INTERSECT` or an `EXCEPT` at the top of the plan is answered `false` from its branches, by the spec's table, where every branch the rule needs proves it; nothing else changes.  The recursive CTE is left in TD-0051 (decision 1).

**Architecture:** A new part of the walk, `NullabilityWalk`'s `SetOperations`, answers ordinal `n` of a node: through nodes that pass their child's columns on, at a set operation by combining its inputs' answers, and at any other node by walking it as a plan of its own, by the walk's rule for a reference and part A's for an expression.  Each branch's value must also be of a type that reaches the result's type with no cast that could make it `NULL` (decision 3).  The session asks one more catalog call, `relations`, for the relations the branches scan, which no column's origin named.

**Tech Stack:** C# (`net8.0` tool), xunit v3 with Shouldly, Npgsql, Testcontainers PostgreSQL 14 and 18.

**Spec:** `docs/superpowers/specs/2026-10-15-postgresql-nullability-noise-design.md`, part C.

## Global Constraints

- **Never a wrong `false`.**  Anything the code does not recognise keeps the answer it has today.  Each shape is trusted only after a container test on 14 and 18 shows what the server prints for it.
- **Every diff of an expectation is one of:** a `true` or `null` turning into `false`; a changed `nullableSource` (`no-origin` to `set-operation`); `plan` going from `unavailable` to `walked` for a root `Append` that prints no `Output` (decision 7).  Anything else is a bug: stop and find it.
- **Expectations are recorded, never edited by hand:** `tools/record-fixtures.sh`, and `tools/record-realistic.sh` twice, plain and with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14`.  Read every diff before committing it.
- **Every commit passes `./pre-commit-validation.sh`**; a task that changes answers also runs `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres` first.
- **The pull request holds 150 files or fewer.**  The estimate is about 45.
- The new `nullableSource` value is `set-operation`, additive; `formatVersion` stays 1.
- `AGENTS.md`, `README.md` and `docs/tech-debt/` are kept true in the commit that changes what they say.

## Review Focus

1. **A branch's hidden implicit cast.**  `SELECT u.id FROM users u UNION ALL SELECT o.id FROM orders o` coerces `u.id` to `bigint` in the branch, and the branch prints `"*SELECT* 1".id` with no cast.  A user's implicit cast that returns `NULL` would be invisible: the type rule (decision 3) must refuse every pair outside the families.  Owned by Task 2 and Task 4.
2. **A user subquery that is a branch's top node**, `SELECT s.b, s.a FROM (SELECT ... OFFSET 0) s UNION ...`: its scan prints its columns in the order the branch needs, so only a set operation's own leaf scan, `*SELECT* <k>`, is mapped by position.  Owned by Task 4.
3. **`EXCEPT`'s left input.**  On 14 it is the member whose flag is `0`, on 18 the `Outer` child; `INTERSECT` may put either branch first on both.  A wrong pick turns `EXCEPT`'s right branch into the answer.  Owned by Tasks 1 and 4.
4. **A node above the set operation that computes a value** (`SELECT kind, count(*) FROM (... UNION ALL ...) s GROUP BY kind`, a `CASE` over a branch's column): only entries printed as the child's entry at the same ordinal are followed.  Owned by Task 4.
5. **A set operation under a join** (`accounts a LEFT JOIN (... UNION ALL ...) s`): stays unknown.  Owned by Task 4.

## What the servers print

Probed on 2026-10-16, `EXPLAIN (VERBOSE)` of `EXECUTE` under `force_generic_plan` with `enable_partition_pruning = off`, on the realistic schema.

1. **`UNION ALL`** is an `Append` whose children are `Member`s, on 14 and 18.  The `Append` prints no `Output`; a node above it prints the first member's entries, a value computed below in parentheses.  A partitioned table in a branch is an `Append` of its partitions nested as a member (`union-all-activity-feed`).
2. **`UNION`** is the same `Append` under a `HashAggregate` (14) or a `Unique` over a `Sort` (18).  Three branches are one `Append` of three members.
3. **`INTERSECT` and `EXCEPT` on 14** are a `SetOp` (JSON `Node Type` `SetOp`, `Command` `Intersect`, `Intersect All`, `Except`, `Except All`; the text says `HashSetOp Except`) whose one `Outer` child is an `Append` of `Member`s, each with a flag as its last entry: `0` for the left input, `1` for the right.  `INTERSECT` may put the right branch first (`intersect-accounts-with-orders-and-events`: `"*SELECT* 2"` is the first member, and its flag is `1`).  A nested set operation is a `Result` member over the inner `SetOp`, printing the inner's entries and its own flag.
4. **`INTERSECT` and `EXCEPT` on 18** are a `SetOp` with two children, `Outer` and `Inner`, and no flag.  `EXCEPT` keeps its left input `Outer` even where it is the smaller (`SELECT c.id FROM categories c WHERE c.id < 3 EXCEPT SELECT g FROM generate_series(1, 100000) g`); `INTERSECT` may swap.
5. **A branch whose output needs a cast or a flag is a `Subquery Scan` aliased `*SELECT* <k>`** over the branch's plan (`Parent Relationship` `Subquery`), on both.  Its entry is a reference `"*SELECT* <k>".<column>` to the branch's column at the same ordinal, or a constant the planner put in its place.  **An implicit cast to the set operation's type is not printed** at the scan: `"*SELECT* 1".id` is `int4` coerced to `int8`, and only a node above prints `(("*SELECT* 1".id)::bigint)`.
6. **A recursive CTE** is a `Recursive Union` in the CTE's `InitPlan`, and the query reads it through a `CTE Scan`, which prints its columns in the order the query needs them (part B, fact 1).

## Decisions (mine)

1. **The recursive CTE stays in TD-0051.**  Its columns reach the result only through a `CTE Scan`, whose columns part B maps by origin and never by position, and a recursive CTE's columns have no origin; mapping them needs the CTE's column names, which the plan does not print.  The induction itself is cheap; the scan is not.
2. **A `*SELECT* <k>` scan is mapped by position, and no other scan is.**  The planner builds the scan's target list one entry per column of the branch, in order (fact 5), so its entry `n` is the branch plan's entry `n`.  A user can write that alias in quotes, so a query whose SQL holds the text `*SELECT*` maps no scan (`WalkScope.SetOperationAliasMayBeWritten`).
3. **The type rule.**  A branch's value counts only when its type is the result column's (the row description's `TypeOid`), or both are in one of three families of built-in types in which every ordered pair has a built-in `pg_cast` entry: `int2 21, int4 23, int8 20, numeric 1700, float4 700, float8 701`; `text 25, varchar 1043, bpchar 1042`; `date 1082, timestamp 1114, timestamptz 1184`.  `pg_cast` holds one row per pair, so no user can add another cast between them, and none of these casts returns `NULL` for a value that is not.  `select_common_type` always picks one of its inputs' types, so a nested set operation's middle type is a branch's type too.  A value whose type is not known (a domain's column, a `COALESCE`, a constant of a type not in the families) does not count.
4. **`relations`**, a catalog call of its own, answers `tables`' row for each relation a branch scans, by schema and name; it is asked only when the root reaches a set operation and a column has no origin, for the relations `tables` did not answer.  Its rows join `WalkScope.Tables`.
5. **Pass-through nodes.**  `Sort`, `Incremental Sort`, `Limit`, `Unique` and `Materialize` never project, so ordinal `n` is the child's.  Any other node with one `Outer` child and no `Join Type` passes ordinal `n` on only when its entry `n` is the child's printed entry `n` as `E`, `(E)` or `((E)::T)`, and none of those three texts is the child's entry at another ordinal.  The printed entries of an `Append` or `Merge Append` without `Output` are its first member's.
6. **An `Append` or `Merge Append` reached this way is a union of its members**, whatever made it: a `UNION`, a partitioned table, inheritance, or an `Append` nested in another.  The members it does not print, pruned ones, return no row.
7. **A root `Append` that prints no `Output`** is answered this way when a column has no origin, and its plan is `walked`, not `unavailable` (`walk-union`).  The spec lists no such diff; it is a plan the walk can now use.
8. **What the walk does not take on:** a set operation under a join, an entry a node above the set operation computes, and a branch's entry through any scan of another plan but `*SELECT* <k>`.  Each stays as it is today and goes to TD-0051.

- Found while building Task 4: decision 2 and fact 5 do not hold everywhere, and a `*SELECT* <k>` scan is followed by position only when it is itself a flagged member of a 14 `SetOp`'s `Append`.  A `UNION ALL` that a subquery in `FROM`, a CTE or an inlined SQL function holds is pulled up as an appendrel, and a branch the planner keeps as a `*SELECT* <k>` scan prints the outer query's columns in its order (`SELECT s.b, s.a FROM (... UNION ALL (SELECT ..., NULLIF(...) ... LIMIT 5)) s`, on 14 and 18).  On 18 the planner drops its own leaf scan when it adds nothing, so a `SetOp`'s input may be a user's subquery named `*SELECT* <k>` through a view, an inlined function or a `U&"..."` identifier the SQL does not show; `SetOperationAliasMayBeWritten` is dropped, since the flag, not the SQL text, is the proof.  Also: a nested `INTERSECT` or `EXCEPT`, whose own type is not the row description's, is `false` only when every input is; and a node with grouping sets is never a pass-through.

## Files

| File | Change |
|----|----|
| `src/SqlSource.Tool/Describing/Postgres/PlanNode.cs` | `Command` |
| `src/SqlSource.Tool/Describing/Postgres/PlanExpression.cs` | `TypeOf` |
| `src/SqlSource.Tool/Describing/Postgres/SetOperationTypes.cs` | Create: the families of decision 3 |
| `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs` | `PostgresRelationsRequest` |
| `src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs` | `RelationsAsync`, sharing `TablesSql`'s reader |
| `src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs` | `postgres.relations` |
| `src/SqlSource.Tool/Describing/Postgres/PlanRelations.cs` | Create: the relations a plan scans |
| `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs` | Ask `relations`; `ColumnTypes` and `SetOperationAliasMayBeWritten` in the scope |
| `src/SqlSource.Tool/Describing/Postgres/NullabilityWalk.cs` | `static partial`; `WalkScope` members; `Run` takes the set-operation path; `Expressions` exposes a reference's answer and type |
| `src/SqlSource.Tool/Describing/Postgres/NullabilityWalk.SetOperations.cs` | Create: `Reaches`, `ColumnAt` |
| `src/SqlSource/Snapshot/SidecarValues.cs`, `schemas/sidecar-v1.schema.json` | `set-operation` |
| Tests: `PlanNodeTests` or the existing reader test, `PlanExpressionTests`, `SetOperationTypesTests` (new), `PlanRelationsTests` (new), `NullabilityWalkTests`, `PostgresSessionTests`, `PostgresEventsTests` | Unit |
| `tests/SqlSource.Tests.Postgres/Describer/PlanOutputTests.cs`, `SetOperationTests.cs` (new), `ExpressionTests.cs` | Container |
| `tests/SqlSource.Tests.Postgres/Realistic/Describer/RealisticNullabilityNoiseTests.cs` | Lower `Budgets` |
| Recorded | Realistic expectations; `walk-union` |
| Docs | Sidecar format and epic designs, `src/SqlSource.Tool/AGENTS.md`, `src/SqlSource.Tool/README.md`, TD-0051, `docs/tech-debt/README.md` |

---

### Task 1: Pin what the servers print

**Files:** `tests/SqlSource.Tests.Postgres/Describer/PlanOutputTests.cs`

- [ ] **Step 1:** Add container tests on the fixture schema, reading `EXPLAIN (VERBOSE, FORMAT JSON)` as the existing tests do, one for each fact the code rests on:
  - `Explain_SetOperationLeafScan_PrintsTheBranchInItsOrder`: a `UNION ALL` whose second branch is coerced to the first's types (`text` under `varchar`, which makes it a scan), with the second branch's two columns aliased each with the other's name, as in `SELECT a.name AS label, a.name AS note FROM accounts a UNION ALL SELECT o.coupon AS note, o.note AS label FROM orders o` on the realistic schema: the member is a `Subquery Scan` aliased `*SELECT* 2` whose `Output` is `"*SELECT* 2".note, "*SELECT* 2".label`, and its `Subquery` child's is `o.coupon, o.note`.  Names cross; positions do not.
  - `Explain_SetOperationLeafScan_HidesItsImplicitCast`: `SELECT id FROM users UNION ALL SELECT id FROM orders`: the scan prints `"*SELECT* 1".id`, which is `int4` under an `int8` result.
  - `Explain_SetOp_HasFlaggedMembersOn14AndTwoInputsOn18`, a theory over `INTERSECT`, `EXCEPT` and their `ALL`s: `Node Type` `SetOp` and its `Command`; on 14 one `Outer` `Append` whose members' last entries are `0` and `1`; on 18 `Outer` and `Inner` children.  Skip other majors.
  - `Explain_Except_KeepsItsLeftInputFirst`: `SELECT id FROM users WHERE id < 3 EXCEPT SELECT g FROM generate_series(1, 100000) g`: on 14 the member with flag `0` scans `users`, on 18 the `Outer` child does.
  - `Catalog_CastFamilies_AreBuiltInBothWays`: for every ordered pair of distinct types in each family of decision 3, `pg_cast` has a row whose `oid` is below 16384.
- [ ] **Step 2:** Run on 18 and 14; PASS on both.  Commit: "Pin what the plan prints of set operations".

### Task 2: The plan's `Command`, and the type of an entry

**Files:** `PlanNode.cs`, `PlanExpression.cs`, `SetOperationTypes.cs` (new); their tests.

**Interfaces:**

```csharp
// PlanNode gains string? Command, after CteName: the JSON "Command" of a SetOp, null elsewhere.
internal static class PlanExpression
{
    // The type OID of an entry that is a whole constant or a known form, or null.
    public static long? TypeOf(string entry, bool catalogFunctions);
}
internal static class SetOperationTypes
{
    // Whether a value of type `from` reaches a result of type `to` with no cast that can return NULL:
    // equal, or both in one family of decision 3.
    public static bool Carries(long from, long to);
}
```

- [ ] **Step 1: Failing tests.**  `PlanNode.Read` keeps `Command`.  `TypeOf`: `'x'::text` 25; `'x'::character varying` 1043; `'x'::bpchar` 1042; `0` and `12` 23; `1.5` 1700; `'5'::bigint` 20; `'2024-01-01'::date` 1082; `true` 16; `count(*)`, `count(o.id)`, `row_number() OVER w1` 20 with catalog functions, null without; `percent_rank() OVER (?)` 701; `(u.deleted_at IS NULL)` 16; `NULL::text`, `COALESCE(...)`, `'active'::user_status`, `u.id` and `(u.id)::bigint` null.  `Carries`: `(23, 20)`, `(20, 23)`, `(25, 1043)`, `(1082, 1184)`, `(16, 16)`, an unknown OID with itself true; `(23, 25)`, `(1043, 16)`, a domain's OID with its base false.
- [ ] **Step 2: Implement.**  `TypeOf` reads with the tokeniser `NullableOf` uses, as a whole entry only: a quoted literal's cast name by the families' names (`text`, `character varying`, `bpchar`, `character`, `smallint`, `integer`, `bigint`, `numeric`, `real`, `double precision`, `date`, `timestamp without time zone`, `timestamp with time zone`, `boolean`), a bare integer, a bare number with a point, `true`/`false`, and the forms of part A whose type is fixed.
- [ ] **Step 3:** Pass.  Commit: "Read a SetOp's command and the type of a constant".

### Task 3: The call `relations`

**Files:** `PostgresCalls.cs`, `PostgresLive.cs`, `PostgresEvents.cs`, `PlanRelations.cs` (new), `PostgresSession.cs`; tests of each.

**Interfaces:**

```csharp
internal sealed record PostgresRelationsRequest(IReadOnlyList<PostgresRelationName> Relations); // sorted, each once
// Answer: PostgresTablesAnswer, a row for each relation that exists, as "tables" answers.
// PostgresLive: Task<PostgresTablesAnswer> RelationsAsync(PostgresRelationsRequest asked, CancellationToken token)
internal static class PlanRelations
{
    // Every relation scan's schema and relation in the whole tree, through every relationship; sorted, each once.
    public static IReadOnlyList<PostgresRelationName> Of(PlanNode root);
}
```

- [ ] **Step 1: Failing tests.**  `PlanRelationsTests`: the scans of members, of a `Subquery` child and of an `InitPlan` are found, a scan without a schema is not, each once.  `PostgresEventsTests`: `postgres.relations` writes the relations, the rows or the error, and the duration.
- [ ] **Step 2: Implement.**  `RelationsSql` is `TablesSql` with the `relations` CTE's filter by `unnest` of a schema array and a name array joined to `pg_namespace` and `pg_class`; the reader of rows is shared.  The session does not ask it yet (Task 4).
- [ ] **Step 3:** Pass.  Commit: "A catalog call for the relations a plan scans".

### Task 4: Answer a set operation's column from its branches

**Files:** `NullabilityWalk.cs`, `NullabilityWalk.SetOperations.cs` (new), `PostgresSession.cs`, `SidecarValues.cs`, `schemas/sidecar-v1.schema.json`; `NullabilityWalkTests`, `PostgresSessionTests`.

**Interfaces:**

```csharp
internal sealed record WalkScope(..., IReadOnlyDictionary<PostgresRelationName, IReadOnlyList<long>>? Partitions = null,
    IReadOnlyList<long>? ColumnTypes = null,            // the row description's TypeOid of each column; null answers no set operation
    bool SetOperationAliasMayBeWritten = false);         // the SQL holds "*SELECT*"
internal static partial class NullabilityWalk
{
    // Whether the root reaches an Append, a Merge Append or a SetOp through pass-through nodes (decision 5).
    public static bool ReachesSetOperation(PlanNode root);
}
// SidecarValues.NullableSource.SetOperation = "set-operation"
```

`ColumnAt(node, n, type)` answers `false` or null, by the first rule that applies:

| Node | Answer |
|----|----|
| `Append`, `Merge Append` | `false` when it has a member and every member's `ColumnAt` is `false` |
| `SetOp`, `Command` `Intersect`/`Intersect All` | `false` when any input's is: on 14 the members of the one `Outer` `Append` (through pass-through nodes), on 18 the `Outer` and the `Inner` child |
| `SetOp`, `Command` `Except`/`Except All` | the left input's: on 14 the one member whose last entry is `0`, the rest `1`; on 18 the `Outer` child |
| `SetOp` of any other shape or command, `Recursive Union` | null |
| A pass-through node (decision 5) | the child's at the ordinal |
| A `Subquery Scan` aliased `*SELECT* <digits>` whose entry `n` is a reference to its own alias, unless `SetOperationAliasMayBeWritten` | its `Subquery` child's at `n` |
| Any other node with `Output` of more than `n` entries | the node walked as a plan of its own: its instances by `Plans.InstancesOf`; a `Reason` or a set operation inside is null; entry `n` as a reference is `false` when part A's `ProvedNotNull` proves it and its attribute's `TypeOid` carries to `type`; as an expression, when `Expressions.NullableOf` (root and instances of this plan) is `false` and `TypeOf` carries to `type` |

Depth is bounded as `Plans.MaxDepth` bounds a scan.  In `Run`, when a column has no origin, `ColumnTypes` is given, and either the root has no `Output` or `instances.HasSetOperation`, and `ReachesSetOperation(root)`: each column of no origin is `ColumnAt(root, n, ColumnTypes[n])`, `false` with `set-operation` or kept; a column with an origin goes as today (its `ColumnOf`, or kept for a root without `Output`); `plan` is `walked`.  Otherwise `Run` is unchanged.

- [ ] **Step 1: Failing tests**, `NullabilityWalkTests`, each a plan written as JSON as the file's rows are:
  - `UNION ALL` of two scans of `NOT NULL` columns of the result's type is `false set-operation`; one member a `NULL::integer`, a nullable column, or a column of the other side of a `Left` join inside the member stays `null no-origin`.
  - The hidden cast: a member's `int4` column under an `int8` result is `false`; an `int4` under `text`, and a domain's column under its base, stay unknown.
  - The `*SELECT* 1` scan maps by position: crossed names `"*SELECT* 1".name, "*SELECT* 1".email` over `u.email, u.name` answer column 0 by `u.email`; the same scan aliased `s` stays unknown; so does `*SELECT* 1` with `SetOperationAliasMayBeWritten`.
  - `UNION` under a 14 `HashAggregate` printing `(("*SELECT* 1".id)::bigint)` and under an 18 `Unique` over a `Sort` are answered; a `HashAggregate` printing `"*SELECT* 1".b, "*SELECT* 1".a` over members `a, b`, and one printing `count(*)`, stay unknown; a first member printing one text twice leaves those ordinals unknown under a projecting node.
  - `INTERSECT` on 14 with the right branch first and on 18 swapped: `false` when either is.  `EXCEPT` on 14 and 18: the left's answer, never the right's; a 14 `Append` with two `0` flags or none is unknown.
  - A nested `EXCEPT` on 14 (a `Result` member over a `SetOp`) and on 18 (a `SetOp` input).
  - A partitioned table's nested `Append` as a member is a union of its partitions; an `Append` with no member is unknown.
  - A root `Append` without `Output` is `walked` and answered; a set operation under a `Hash Left Join` stays unknown; a `Recursive Union` stays unknown.
  - `PostgresSessionTests`: a set operation with a column of no origin asks `relations` after `explain` (and `partitions`, when asked) for the relations not in `tables`' answer, and the walk sees their rows; none is asked when every relation was answered, when the root reaches no set operation, or when every column has an origin; an error is logged, never reported, and the walk goes on without the rows.
- [ ] **Step 2: Implement.**  `NullabilityWalk` becomes `static partial`; the set-operation path is a private nested class in the new file over `Plans` and `Expressions`, which gains what it needs: the answer and the attribute type of a reference.  The session asks `relations` when `ReachesSetOperation(root)` and a column has no origin, with `PlanRelations.Of(root)` less the relations `tables` answered, and passes `scope with { Tables = merged }`, each column's `TypeOid` as `ColumnTypes`, and `SetOperationAliasMayBeWritten` (ordinal `Contains("*SELECT*")` of the SQL).  `set-operation` joins `SidecarValues` and the schema's enum.
- [ ] **Step 3:** Pass.  Commit: "Answer a set operation's column from its branches".

### Task 5: Against the servers, and recorded

- [ ] **Step 1:** `SetOperationTests` (container, 14 and 18) on the fixture schema, each answered `false set-operation` or kept: `UNION ALL` and `UNION` of `NOT NULL` columns, one with `int4` and `int8`; a branch's `NULL`; a branch with a `LEFT JOIN` whose extended column is used, and whose preserved one is; `INTERSECT` with one branch's nullable column (`false`); `EXCEPT` with the left's nullable column (unknown) and the right's (`false`); `a EXCEPT b EXCEPT c`; a branch over a partitioned table; the crossed names of Task 1; a quoted alias `"*SELECT* 1"` written in a branch (unknown); `GROUP BY` over a `UNION ALL` subquery (the key unknown, as decision 8); a recursive CTE (unknown).  `ExpressionTests.Describe_ConstantFirstBranchOfASetOperation_StaysUnknown`: the `INTERSECT` and `EXCEPT` rows move to a test of their new answer, `false set-operation`.
- [ ] **Step 2:** Re-record the fixtures and the realistic expectations on 18 and 14, read every diff against the Global Constraints, and lower `Budgets`.  Expected: `union-all-activity-feed` kind, ref_id; `union-branch-literal` id, name, sort_group; `union-distinct-search-ordered-limited` id, kind; `union-mixed-types` id, since, kind; `intersect-accounts-with-orders-and-events` id, name; `except-accounts-without-reservations` id, name, created_at: 15 on each major.  `walk-union` becomes `walked` with both columns still unknown.
- [ ] **Step 3:** Docs: TD-0051's counts, its set-operation bullet rewritten to what remains (the recursive CTE, decision 1; a branch's cast of a reference, `at` of `union-all-activity-feed`; a set operation under a join or computed over, decision 8; a branch's value of a type outside the families), and the expression bullet's "(part C)" dropped; `docs/tech-debt/README.md`'s row; the sidecar format and epic designs gain `set-operation`; `src/SqlSource.Tool/AGENTS.md` (the call `relations` and when it is asked, the set-operation path); `src/SqlSource.Tool/README.md` where it says what the walk answers.
- [ ] **Step 4:** `./pre-commit-validation.sh` and the 14 run.  Commit: "Record the answers part C gives".

### Task 6: Check the branch

- [ ] `git diff --stat main...HEAD | tail -1` ≤ 150; every expectation diff read; open the pull request with autofix on.
