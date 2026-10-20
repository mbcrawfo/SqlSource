# The PostgreSQL describer's nullability noise, part D - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A column of no origin is answered `false` (`nullableSource` `expression`) for a total operator, function or cast of `pg_catalog` over proved values, an aggregate other than `count` over a group that has a row, a scalar subquery of a plain aggregate, a system column, and on 18 `EXISTS(SubPlan n)`, a proved `ANY` and `MERGE_ACTION()`; nothing else changes.

**Architecture:** `PlanExpression`'s reader gains a typed proof (a span is a value that is not `NULL`, of a known OID) and the closed tables of total operators, functions and aggregates; the walk tells it, at the node that computes an entry, whether that node's groups have a row and which spans a child printed, and resolves `SubPlan <n>` by walking the subplan as a plan of its own.  The session asks `relations` whenever a column has no origin.

**Tech Stack:** C# (`net8.0` tool), xunit v3 with Shouldly, Npgsql, Testcontainers PostgreSQL 14.24 and 18.6.

**Spec:** `docs/superpowers/specs/2026-10-20-postgresql-nullability-noise-part-d-design.md`.  Read it first; decisions are cited as D1 to D7 and facts as F1 to F7.

## Global Constraints

- **Never a wrong `false`.**  A form is read only as a whole span, never a prefix, and only when every value it is computed from is proved.  When in doubt the column keeps the answer it has today.
- **Every diff of a recorded file is one of:** a `true` or `null` turning into `false` with `nullableSource` `expression`; a fixture capture gaining the call `relations` (D7).  Anything else is a bug: stop and find it.
- **Every commit passes `./pre-commit-validation.sh`** (root `AGENTS.md`, "Commits"); after any change to the describer also run `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres` and see it pass.
- **Recorded files are recorded, never edited by hand:** `tools/record-fixtures.sh` (fixture captures and `expected.json`) and `tools/describe-end-to-end.sh` (sidecars of the realistic cases, the end-to-end project and the examples, on the default image).  Read every diff before committing it.
- **The pull request holds 150 files or fewer.**  Estimate: under 80.  If recordings push it over, they go in a second PR, `(part 2 of 2)`.
- An operator, function or literal type is read only when `WalkScope.CatalogFunctionsFirst` is true.
- No new server call, no new record member, no new `nullableSource` value; `formatVersion` stays 1.
- `src/SqlSource.Tool/AGENTS.md`, `docs/architecture/postgres-nullability.md`, `docs/manual/postgresql.md` and `docs/tech-debt/` are kept true in the commit that changes what they say (Task 8 holds the bulk).
- Comments and remarks follow the surrounding style: plain sentences, two spaces after a full stop, no sub-phase or part letters in code comments.

## Review Focus

1. **An aggregate inside a span a child printed**, at an `Aggregate` node with a `Group Key` (the child computed it under its own grouping, possibly a plain one): must be unknown.  Owned by Task 3 (`NullableOf_AggregateInASpanAChildPrinted_IsUnknown`, `Run_AggregateTakenFromAChild_KeepsNoOrigin`).
2. **`a.id IN (SELECT count(*) ...)` on 14**, which prints `(SubPlan n)` over a plain `count(*)` aggregate: the column is `boolean`, so it must stay unknown.  Owned by Task 4 (`Run_ScalarSubPlanOfABooleanColumn_KeepsNoOrigin`) and Task 6 (`DescribeAsync_InOverAPlainCount_KeepsNoOrigin`).
3. **A user's operator or aggregate on a path that puts its schema first** (`SET search_path = shadow, pg_catalog` with `shadow.*(numeric, numeric)` or `shadow.max(timestamptz)`): unknown.  Owned by Task 6 (`DescribeAsync_OperatorOfASchemaBeforeTheCatalog_KeepsNoOrigin`).
4. **An aggregate with no `GROUP BY`, with `GROUP BY ()`, with `FILTER`, or as a window aggregate**: unknown even over a `NOT NULL` column.  Owned by Task 3 (unit) and Task 6 (container).
5. **A domain's column or a value of a type outside the tables** (`sum(amount)` of a domain, `max(status)` of an enum, `upper(r.during)`): unknown.  Owned by Task 2 (unit) and Task 6 (container).

## Files

| File | Change |
|----|----|
| `src/SqlSource.Tool/Describing/Postgres/BuiltInTypes.cs` | `Oid = 26`, `Tid = 27`, `Xid = 28`, `Cid = 29` |
| `src/SqlSource.Tool/Describing/Postgres/Walk/PlanNode.cs` | `Strategy`, `HasGroupKey`, `HasFilter` |
| `src/SqlSource.Tool/Describing/Postgres/Walk/TotalFunctions.cs` | Create: the tables of D2 and D4 |
| `src/SqlSource.Tool/Describing/Postgres/Walk/ExpressionContext.cs` | Create: what the reader is told beside the entry |
| `src/SqlSource.Tool/Describing/Postgres/Walk/PlanExpression.cs` | Typed proof; operators, functions, casts, aggregates, subplans, `EXISTS`, `ANY`, `MERGE_ACTION()` |
| `src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.cs` | `Expressions`: context per node, follow after the direct reading, subplans, system columns |
| `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs` | `RelationsOfAsync` asked whenever a column has no origin |
| `tests/SqlSource.Tool.Tests/PostgresPlans.cs` | Helpers for an `Aggregate` and a `SubPlan` child |
| Unit tests | `PlanNodeTests`, `TotalFunctionsTests` (new), `PlanExpressionTests`, `NullabilityWalkTests`, `PostgresSessionRelationTests` |
| Container tests | `PlanOutputTests`, `ExpressionTests` |
| `tests/SqlSource.Tests.Postgres/Realistic/Describer/RealisticNullabilityNoiseTests.cs` | `Budgets` |
| Recorded | Realistic, end-to-end and example sidecars; fixture cases |
| Docs | `docs/architecture/postgres-nullability.md`, `src/SqlSource.Tool/AGENTS.md`, `docs/manual/postgresql.md`, TD-0051, `docs/tech-debt/README.md` |

---

### Task 1: Pin what the servers print

**Files:** Modify `tests/SqlSource.Tests.Postgres/Describer/PlanOutputTests.cs`.

**Interfaces:** Produces the exact texts later tasks' unit tests use.  Consumes nothing.

Container tests, run on 18 by default and on 14 with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`.  Follow the file's existing `Explain_*` helpers (they explain a query on the fixture schema and read the root's or a node's `Output`).

- [ ] **Step 1: Add `Explain_TotalForm_PrintsWhatTheReaderReads`**, a theory of `(sql, expected root entry)` with the same text on both majors, on `users u` of the fixture schema (`id integer`, `name varchar(100) NOT NULL`, `balance numeric(18,2) NOT NULL`, `created_at timestamptz NOT NULL`):
  - `u.balance * 2` → `(balance * '2'::numeric)` or what the server prints; pin what it prints.
  - `u.id::bigint * 2` → `((id)::bigint * 2)`
  - `lower(u.name) || '!'` → `(lower((name)::text) || '!'::text)`
  - `length(u.name)` → `length((name)::text)`
  - `date_trunc('day', u.created_at)` → `date_trunc('day'::text, created_at)`
  - `u.created_at::date` → `(created_at)::date`
  - `u.name = 'x'` → `((name)::text = 'x'::text)`
  Pin each text exactly as the server prints it; a text that differs between 14 and 18 is a separate `InlineData` per major (use the file's pattern for per-major expectations if it has one, else skip that row and note it).
- [ ] **Step 2: Add `Explain_GroupedAggregate_IsComputedAtANodeWithAGroupKey`**: `SELECT u.id, max(u.name), sum(u.balance) FROM users u JOIN orders o ON o.user_id = u.id GROUP BY u.id ORDER BY 3`.  Assert the root `Sort` prints `(max((u.name)::text))` and `(sum(u.balance))`, and the `Aggregate` under it has `HasGroupKey` true, `HasGroupingSets` false and prints them bare.  (Needs Task 2's `PlanNode` members: write this step's assertions on `HasGroupKey` after Task 2, or read the JSON key `Group Key` directly here.)
- [ ] **Step 3: Add `Explain_ScalarSubquery_IsASubPlanOfAPlainAggregate`**: `SELECT u.id, (SELECT count(*) FROM orders o WHERE o.user_id = u.id) AS n FROM users u`.  Assert the root entry is `(SubPlan 1)` on both, and the node with `Subplan Name` `SubPlan 1` is an `Aggregate`, `Strategy` `Plain`, no `Group Key`, `Output` `["count(*)"]`.
- [ ] **Step 4: Add `Explain_Sublinks_PrintEachKindOnEighteen`**, a theory over `EXISTS (SELECT 1 FROM orders o WHERE o.user_id > u.id)`, `u.id IN (SELECT o.user_id FROM orders o WHERE o.placed_at > u.created_at)`, the hashed `u.id IN (SELECT o.user_id FROM orders o)` and `ARRAY(SELECT o.id FROM orders o WHERE o.user_id = u.id)`: on 18 the entries are `EXISTS(SubPlan 1)`, `(ANY (u.id = (SubPlan 1).col1))`, `(ANY (u.id = (hashed SubPlan 1).col1))`, `ARRAY(SubPlan 1)`; on 14 `(SubPlan 1)`, `(SubPlan 1)`, `(hashed SubPlan 1)`, `(SubPlan 1)` (F4).
- [ ] **Step 5: Add `Explain_SystemColumn_PrintsAsAColumn`**: `INSERT INTO users (name) VALUES ('x') ON CONFLICT DO NOTHING RETURNING id, (xmax = 0) AS inserted` prints `(users.xmax = 0)` or `(xmax = 0)`; pin the text.
- [ ] **Step 6: Add `Explain_MergeAction_PrintsUpperCase`**, skipped below 17 the way the file skips by major: `MERGE INTO users u USING (SELECT 1 AS id) s ON s.id = u.id WHEN MATCHED THEN DO NOTHING RETURNING merge_action(), u.id` prints `MERGE_ACTION()` at the root.
- [ ] **Step 7: Run** `dotnet test --project tests/SqlSource.Tests.Postgres --filter "FullyQualifiedName~PlanOutputTests"` and the same with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`.  Expected: PASS on both.  Correct a pinned text to what the server prints; if a fact of the spec's F1 to F6 does not hold, stop and report it.
- [ ] **Step 8:** `./pre-commit-validation.sh`, then commit `Pin what the plan prints of operators, aggregates and subplans`.

### Task 2: Typed proof, total operators, functions and casts, system columns

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/BuiltInTypes.cs`, `Walk/PlanNode.cs`, `Walk/PlanExpression.cs`, `Walk/NullabilityWalk.cs`
- Create: `Walk/TotalFunctions.cs`, `Walk/ExpressionContext.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/TotalFunctionsTests.cs` (new), `PlanExpressionTests.cs`, `PlanNodeTests.cs`, `NullabilityWalkTests.cs`; container `PlanOutputTests.cs`

**Interfaces:**
- Produces:
  - `BuiltInTypes.Oid = 26`, `Tid = 27`, `Xid = 28`, `Cid = 29` (the catalog test `Catalog_BuiltInTypes_AreNamedSo` covers them by name).
  - `PlanNode` gains `string? Strategy` (`Strategy`), `bool HasGroupKey` (a `Group Key` array with at least one entry), `bool HasFilter` (a `Filter` key), read in `NodeOf`; `PostgresPlans` helpers pass defaults.
  - `internal sealed record ExpressionContext(bool CatalogFunctions, Func<OutputReference, long?> ProvedTypeOf, Func<string, bool>? GroupPreserved = null, Func<string, bool>? GroupFromChild = null, bool GroupsHaveRows = false, Func<string, long?>? ScalarSubPlanTypeOf = null, Func<string, int, long?>? SubPlanColumnTypeOf = null)`.  `ProvedTypeOf` returns the OID of a proved reference, `TotalFunctions.UnknownType` (0) for one proved of no known type, or null.  Tasks 3 and 4 use the last four members; Task 2 only reads `CatalogFunctions`, `ProvedTypeOf` and `GroupPreserved`.
  - `PlanExpression.NullableOf(string entry, ExpressionContext context)`; the existing `NullableOf(entry, catalogFunctions, provedNotNull, groupPreserved)` stays and maps `provedNotNull` to `UnknownType`, so every existing test and caller is unchanged.
  - `PlanExpression.ProvedTypeOf(string entry, ExpressionContext context) : long?`, the typed proof of the whole entry (null when not proved).
  - `TotalFunctions`: `const long UnknownType = 0`; `long? OperatorResult(string name, long left, long right)`; `long? FunctionResult(string name, IReadOnlyList<long> arguments)`; `long? AggregateResult(string name, IReadOnlyList<long> arguments)` (filled in Task 3); and, for the catalog test, `IEnumerable<(string Name, long Left, long Right, long Result)> Operators`, `IEnumerable<(string Name, long[] Arguments, long Result)> Functions`, `IEnumerable<(string Name, long[] Arguments, long Result)> Aggregates`.

- [ ] **Step 1: Write `TotalFunctionsTests`** (unit): `OperatorResult("*", Numeric, Numeric) == Numeric`; `OperatorResult("*", Int8, Int4) == Int8`; `OperatorResult("/", Float4, Float8) == Float8`; `OperatorResult("=", Xid, Int4) == Bool`; `OperatorResult("||", Text, Text) == Text`; `OperatorResult("=", Varchar, Varchar)` is null; `OperatorResult("*", Numeric, Int4)` is null; `FunctionResult("upper", [Text]) == Text`; `FunctionResult("date_trunc", [Text, Timestamptz]) == Timestamptz`; `FunctionResult("upper", [UnknownType])` is null; `FunctionResult("array_agg", [Int4])` is null.
- [ ] **Step 2: Implement `TotalFunctions`** with the rows of D2's table exactly (operators: `+ - * /` same-type over the six number types, mixed over `smallint`/`integer`/`bigint` with the wider result, `real`/`double precision` either way to `double precision`; `= <> < <= > >=` same-type over the twelve types D2 lists, mixed integers, `real`/`double precision`; `=(xid, integer)`; `||(text, text)`; functions `lower`, `upper` (`text`→`text`), `length` (`text`→`integer`), `date_trunc` (`text`, `timestamp`→`timestamp`; `text`, `timestamptz`→`timestamptz`)).  Remarks: the rule of D2 (exact signature, `pg_catalog` first, total not strict), with the C functions named (`int4mul`, `numeric_mul`, `float8div`, `int4eq`, `textcat`, `lower`, `textlen`, `timestamptz_trunc`, ... as `oprcode`/`prosrc` give them) and the sentence that each errors where it cannot compute and returns a value otherwise.  Run the test: PASS.
- [ ] **Step 3: Add the container test `Catalog_TotalFunctions_ArePgCatalogsOwn`** to `PlanOutputTests`: for every row of `TotalFunctions.Operators`, `pg_operator` of `pg_catalog` has `oprname`, `oprleft`, `oprright` exactly, `oprresult` equal to the row's result, and its `oprcode`'s function is in `pg_catalog`; for every row of `Functions`, `pg_proc` of `pg_catalog` with `proname` and `proargtypes` exactly (as an `oidvector`), `prorettype` equal, `prokind = 'f'`, `proretset` false.  Collect every mismatch and fail with the list.  Run on 18 and 14.24: PASS (drop a row that fails on either, and say so in the commit).
- [ ] **Step 4: Write the failing `PlanExpressionTests`**, each through the new overload with `catalogFunctions: true` and a `ProvedTypeOf` that answers `u.balance`/`l.unit_price` → `Numeric`, `l.quantity`/`u.id`/`id` → `Int4`, `u.name`/`name` → `Varchar`, `created_at` → `Timestamptz`, `products.xmax` → `Xid`, anything else null:
  - `NullableOf_TotalOperatorOverProvedValues_IsNotNull`: `((l.quantity)::numeric * l.unit_price)`, `((id)::bigint * 2)`, `((id + 1) * 3)`, `(lower((name)::text) || '!'::text)`, `length((name)::text)`, `date_trunc('day'::text, created_at)`, `(created_at)::date`, `((name)::text = 'x'::text)`, `(products.xmax = 0)`, `(((l.quantity)::numeric * l.unit_price))` → `false`.
  - `NullableOf_OperatorOrFunctionNotInTheTable_IsUnknown`: `(l.quantity * l.unit_price)` (no such `integer * numeric`), `(u.note * 2)` (unproved), `upper(r.during)`, `(r.during * tstzrange($1, $2))`, `OPERATOR(public.*)(...)`-shaped text `(l.quantity OPERATOR(public.*) l.unit_price)`, `public.lower((name)::text)`, `(- id)`, `((id)::bigint * 2) * NULL::bigint`, a binary operator not wrapped in its own parentheses → null.
  - `NullableOf_TotalFormWhenTheCatalogIsNotFirst_IsUnknown`: the first theory's texts with `catalogFunctions: false` → null (casts and literals need it too).
  - `NullableOf_CastOutsideAFamily_IsUnknown`: `(status)::text` with `status` → an enum OID, `(id)::text`, `(created_at)::text` → null.
  - `ProvedTypeOf_TotalOperator_IsItsResult`: `((id)::bigint * 2)` → `Int8`; `((name)::text = 'x'::text)` → `Bool`; `COALESCE(id, 0)` → null (not null, no type).
  Run: FAIL.
- [ ] **Step 5: Implement the typed proof in `PlanExpression.Reader`**: `long? Proved(int from, int to)` per D1 (constants and tests as `TypeOid` types them, a reference through `ProvedTypeOf`, a cast `(E)::T` by D3 with `SetOperationTypes.Carries(S, T)` and `T` read by the existing `BuiltInType`, a binary operator inside a group, `(A op B)` with one `Kind.Other` token at depth zero between two operands, a function call `name(a, ...)` with a lower-case bare name), each looked up in `TotalFunctions`; `UnknownType` never matches a row.  `Form` answers `false` when `Proved` is not null, after its existing checks; `IsNotNull` uses it too, so `COALESCE` and `CASE` take the new forms as arguments.  A group that `GroupPreserved` says no to is unknown in `Proved` as in `Form`.  Run Step 4: PASS; the whole `PlanExpressionTests` class: PASS unchanged.
- [ ] **Step 6: System columns in `NullabilityWalk.Expressions.ProvedTypeOf`**: when no attribute has the reference's name and the name is one of `tableoid`, `ctid`, `xmin`, `xmax`, `cmin`, `cmax`, and the table's `Relkind` is `r` or `p` and `ColumnNullability.Of` would answer a validated `NOT NULL` column of it `false` (no `Relhassubclass` for `r`, no `HasForeignPartition`, no `HasInsteadRule`; add a small `ColumnNullability.KeepsNotNull(PostgresTableRow)` used by both `Of` and this, so the rule stays in one place), answer `Oid`, `Tid`, `Xid` or `Cid`.  Change `Expressions.NullableOf` to build an `ExpressionContext` (`ProvedTypeOf` is the method itself).  Test `Run_SystemColumnOfATable_IsAnsweredInAnExpression` (root `ModifyTable` printing `(products.xmax = 0)` → `false` `expression`) and `Run_SystemColumnOfAForeignTable_KeepsNoOrigin`.  Run `NullabilityWalkTests`: PASS.
- [ ] **Step 7: `PlanNode` members** of the interface, with `PlanNodeTests.Read_AggregateKeys_AreRead` (`"Strategy": "Plain"`, `"Group Key": ["a.id"]`, `"Filter": "..."` read; `"Group Key": []` is `HasGroupKey` false).  PASS.
- [ ] **Step 8:** `./pre-commit-validation.sh`; the Postgres tests on 18 and 14.24; commit `Answer total operators, functions and casts over proved values`.

### Task 3: Aggregates over a group that has a row

**Files:** Modify `Walk/TotalFunctions.cs`, `Walk/PlanExpression.cs`, `Walk/NullabilityWalk.cs`, `tests/SqlSource.Tool.Tests/PostgresPlans.cs`.  Test: `TotalFunctionsTests`, `PlanExpressionTests`, `NullabilityWalkTests`, container `PlanOutputTests`.

**Interfaces:**
- Consumes: Task 2's `ExpressionContext`, `TotalFunctions`, `PlanNode.HasGroupKey`.
- Produces: `TotalFunctions.AggregateResult` and `Aggregates` filled; the walk's `Expressions` reads an entry at the node that computes it with `GroupsHaveRows = node.NodeType == "Aggregate" && node.HasGroupKey && !node.HasGroupingSets` (use the `PlanVocabulary` constant for `Aggregate`) and `GroupFromChild = group => Printing(children, group).Any()`.

- [ ] **Step 1: Tests.** `TotalFunctionsTests.AggregateResult_*`: `sum(Int4) == Int8`, `sum(Int8) == Numeric`, `avg(Float4) == Float8`, `max(Timestamptz) == Timestamptz`, `max(Varchar)` null, `string_agg(Text, Text) == Text`, `bool_and(Bool) == Bool`, `array_agg(Int4)` null.  `PlanExpressionTests` with `GroupsHaveRows: true` and Task 2's `ProvedTypeOf`, plus `p.name` → `Text`, `p.id` → `Int4`:
  - `NullableOf_AggregateOverAGroupWithARow_IsNotNull`: `sum(((l.quantity)::numeric * l.unit_price))`, `max(created_at)`, `string_agg(DISTINCT p.name, ', '::text ORDER BY p.name)`, `sum(l.quantity)`, `avg(id)`, `bool_and((id > 0))`, `COALESCE(max(created_at), now())` → `false`.
  - `NullableOf_AggregateThatMayBeNull_IsUnknown`: the same texts with `GroupsHaveRows: false`; and with `true`: `max(created_at) FILTER (WHERE (id > 1))`, `sum(id) OVER w1`, `sum(id) OVER (?)`, `max(o.note)` (unproved), `array_agg(DISTINCT p.id ORDER BY p.id)`, `percentile_cont('0.5'::double precision) WITHIN GROUP (ORDER BY id)` → null.
  - `NullableOf_AggregateInASpanAChildPrinted_IsUnknown`: `((sum(id)) * 2)` with `GroupsHaveRows: true` and `GroupFromChild` true for `sum(id)` → null; with `GroupFromChild` false → `false`.
  - `TotalFunctionsTests` and container `Catalog_TotalAggregates_ArePgCatalogsOwn`: for every row of `Aggregates`, `pg_proc` of `pg_catalog` with that name and exact `proargtypes`, `prokind = 'a'`, `prorettype` equal.
  Run: FAIL.
- [ ] **Step 2: Implement.**  `AggregateResult` with D4's table.  In the reader, an aggregate span is `name ( [DISTINCT] a1, ..., an [ORDER BY ...] )` to the end of the span, with nothing after its parenthesis (a `FILTER`, an `OVER` or a `WITHIN GROUP` makes it no aggregate form); each argument up to a depth-zero `ORDER BY` is proved; the result comes from `AggregateResult`; and it is read only when the reader's current `GroupsHaveRows` is true.  Descending into a group for which `GroupFromChild` is true sets it false for everything inside (save and restore a field, or thread a flag); the direct reading passes `false`.
- [ ] **Step 3: The walk.**  `Expressions.NullableOf(entry)`: when nothing extends, the direct reading first, and when it gives null, `NullableAt(root, entry, 0)`; otherwise `NullableAt` as today.  At the node where `NullableAt` reads the entry, the context carries `GroupsHaveRows` of that node and `GroupFromChild` over its followed children.  Add `PostgresPlans.Aggregate(string strategy, bool groupKey, PlanNode child, params string[] output)` and tests: `Run_AggregateOfAGroupedNode_IsAnswered` (Sort over grouped `Aggregate` printing `(max(o.placed_at))` above, `max(o.placed_at)` at the aggregate, over a join of `a` and `o` → `false` `expression`), `Run_AggregateOfAPlainNode_KeepsNoOrigin`, `Run_AggregateOfAnExtendedSide_KeepsNoOrigin` (a `Left` join extending `o`), `Run_AggregateTakenFromAChild_KeepsNoOrigin` (a grouped `Aggregate` whose entry `((sum(x)) * 2)` takes `sum(x)` from a child plain `Aggregate`).  Run the class: PASS, every existing test unchanged.
- [ ] **Step 4:** `./pre-commit-validation.sh`; Postgres tests on 18 and 14.24; commit `Answer aggregates over a group that has a row`.

### Task 4: Subplans, `EXISTS`, `ANY` and `MERGE_ACTION()`

**Files:** Modify `Walk/PlanExpression.cs`, `Walk/NullabilityWalk.cs`, `tests/SqlSource.Tool.Tests/PostgresPlans.cs`.  Test: `PlanExpressionTests`, `NullabilityWalkTests`.

**Interfaces:**
- Consumes: Tasks 2 and 3.
- Produces: the reader reads `SubPlan <n>` inside a group through `ScalarSubPlanTypeOf("SubPlan <n>")`, `EXISTS(SubPlan <n>)` as `Bool`, `(ANY (L op (SubPlan <n>).col<k>))` and its `hashed` form through `SubPlanColumnTypeOf("SubPlan <n>", k)` and `TotalFunctions.OperatorResult` (only a result of `Bool` counts), and `MERGE_ACTION()` as `Text`.  `Expressions.NullableOf(string entry, long columnType)` takes the column's row-description type (`WalkScope.ColumnTypes[index]`, or `UnknownType` when the list's length differs).

- [ ] **Step 1: Reader tests** (`PlanExpressionTests`): `NullableOf_SubPlanForms_AreReadThroughTheirCallbacks`: `(SubPlan 3)` with `ScalarSubPlanTypeOf` → `Int8` is `false`, → null is null; `EXISTS(SubPlan 1)` is `false` with no callback; `(ANY (a.id = (hashed SubPlan 2).col1))` and `(ANY (a.id = (SubPlan 2).col1))` with `a.id` → `Int4` and `SubPlanColumnTypeOf("SubPlan 2", 1)` → `Int4` are `false`, with the column → null or → `Text` are null, with `a.email` unproved are null; `(ANY ((a.id = (SubPlan 2).col1) AND (a.x = (SubPlan 2).col2)))` is null; `(ALL (a.id = (SubPlan 2).col1))` is null; `MERGE_ACTION()` is `false`; `merge_action()`, `"MERGE_ACTION"()`, `EXISTS(SubPlan 1) AND true`, `ARRAY(SubPlan 1)`, `(rescan SubPlan 1)` are null.
- [ ] **Step 2: Implement the reader forms.**  `SubPlan <n>` is the two tokens `SubPlan` and a number inside a group, and `hashed SubPlan <n>` three; `.col<k>` follows a group of either as `Dot` and a word `col<digits>`.
- [ ] **Step 3: Walk tests** (`NullabilityWalkTests`), with `PostgresPlans.SubPlan(string name, PlanNode root)` setting `ParentRelationship` `SubPlan` and `SubplanName`:
  - `Run_ScalarSubPlanOfAPlainCount_IsAnswered`: root scan of `a` printing `a.id, (SubPlan 1)`, child `SubPlan 1` a `Plain` `Aggregate` printing `count(*)`, column types `[Int4, Int8]` → second column `false` `expression`.
  - `Run_ScalarSubPlanOfABooleanColumn_KeepsNoOrigin`: the same with column types `[Int4, Bool]`.
  - `Run_ScalarSubPlanThatMayReturnNoRow_KeepsNoOrigin`: the subplan's `Aggregate` with a `Group Key`; with a `Filter`; a `Limit` root; an `Output` of `max(o.placed_at)`; a name found twice.
  - `Run_ScalarSubPlanUnderAnExtendedSide_KeepsNoOrigin`: `((SubPlan 1))` printed by the inner side of a `Left` join.
  - `Run_AnyOverAProvedSubPlanColumn_IsAnswered`: `(ANY (a.id = (hashed SubPlan 2).col1))` with the subplan a scan of `o` printing `o.user_id` (a `NOT NULL` column of a table in `Tables`) → `false`; with `o.note` (nullable) → no origin; with the subplan a `Left` join extending `o` → no origin.
  - `Run_MergeActionAtARootModifyTable_IsAnswered`.
- [ ] **Step 4: Implement in `Expressions`.**  A subplan is found by `SubplanName` among every node of `cache.Tree` with `ParentRelationship` `SubPlan`, exactly once.  `ScalarSubPlanTypeOf`: the subplan's root is an `Aggregate`, `Strategy` `Plain`, no `Group Key`, no `Grouping Sets`, no `Filter`, one `Output` entry; that entry is proved by an `Expressions` of the subplan (`cache.Of(subRoot)`, no set operation, no `Reason`, its own `OnlyAliasOf`), and its type is in a family of `SetOperationTypes`; and it is read only when the entry is the column's whole entry (after the wrapping parentheses) and the type equals the column's type (D5).  `SubPlanColumnTypeOf(name, k)`: the subplan's root `Output[k - 1]`, a reference proved by the subplan's `ProvedTypeOf` or an expression by its typed proof.  Depth: count subplans followed against `Plans.MaxDepth`.  Run both test classes: PASS.
- [ ] **Step 5:** `./pre-commit-validation.sh`; Postgres tests on 18 and 14.24; commit `Answer scalar subqueries, EXISTS, ANY and MERGE_ACTION from their subplans`.

### Task 5: Ask `relations` whenever a column has no origin

**Files:** Modify `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs` (`RelationsOfAsync` and its comment).  Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionRelationTests.cs`.

- [ ] **Step 1: Test** `DescribeAsync_ExpressionOverARelationNoColumnNames_AsksRelations`: a scripted session (follow the file's existing pattern) whose query has a column of no origin and whose plan, with no set operation, scans `orders`, which `tables` did not answer; it asks `relations` for `public.orders` after `explain`, and the expression `max(o.placed_at)` at a grouped `Aggregate` is `false`.  And keep the existing test that a query whose every column has an origin asks nothing more.  Run: FAIL.
- [ ] **Step 2: Implement**: drop the `NullabilityWalk.MayAnswerFromBranches(cache)` condition from `RelationsOfAsync`; keep "a column has no origin" and "a relation is left to ask for".  If `MayAnswerFromBranches` has no caller left, delete it and its tests.  Update the comment and `docs/architecture/postgres-nullability.md`'s sentence on when `relations` is asked, and `src/SqlSource.Tool/AGENTS.md`'s, in this commit.
- [ ] **Step 3: Run** the session tests: PASS.  `./pre-commit-validation.sh`; commit `Ask relations for every query with a column of no origin`.

### Task 6: Container tests of the new answers

**Files:** Modify `tests/SqlSource.Tests.Postgres/Describer/ExpressionTests.cs`.

Use the file's `AnswersAsync` and `NullabilityAnswers` (`NotNullExpression`, `UnknownNoOrigin`, `NotNullCatalog`).  Each test runs on 18 and on 14.24; a test of an 18-only form skips below the major that has it, as the suite skips elsewhere.

- [ ] **Step 1: Write the tests**, on the fixture schema:
  - `DescribeAsync_TotalOperatorsAndFunctions_AreAnsweredFromThePlan`: `u.balance * 2`, `u.id::bigint * 2`, `lower(u.name) || '!'`, `length(u.name)`, `date_trunc('day', u.created_at)`, `u.created_at::date`, `u.name = 'x'`, `u.email || '!'` (nullable → unknown), `upper(b.during)` from `bookings b` (unknown).
  - `DescribeAsync_AggregatesOverAGroup_AreAnswered`: `SELECT u.id, max(o.placed_at), sum(o.total), string_agg(u.name, ',') , max(o.note), max(o.placed_at) FILTER (WHERE o.total > 0), array_agg(o.id) FROM users u JOIN orders o ON o.user_id = u.id GROUP BY u.id` → catalog, expression, expression, expression, unknown, unknown, unknown.
  - `DescribeAsync_AggregatesThatMayBeNull_KeepNoOrigin`: `SELECT max(created_at), sum(balance) FROM users`; `... GROUP BY ()`; `SELECT u.id, max(o.placed_at) FROM users u LEFT JOIN orders o ON o.user_id = u.id GROUP BY u.id`; `SELECT sum(balance) OVER (ORDER BY id ROWS BETWEEN 1 FOLLOWING AND 2 FOLLOWING) FROM users` → unknown each.
  - `DescribeAsync_ScalarSubqueryOfACount_IsAnswered` and `DescribeAsync_InOverAPlainCount_KeepsNoOrigin` (`u.id IN (SELECT count(*) FROM orders o WHERE o.user_id = u.id)`), `DescribeAsync_ScalarSubqueryOfAMax_KeepsNoOrigin`.
  - `DescribeAsync_ExistsAndAny_AreAnsweredOnEighteen`: `EXISTS (SELECT 1 FROM orders o WHERE o.user_id = u.id)` and `u.id IN (SELECT o.user_id FROM orders o)` → expression on 18, unknown on 14; `u.email IN (SELECT o.note FROM orders o)` → unknown on both.
  - `DescribeAsync_SystemColumn_IsAnswered`: the upsert of Task 1 Step 5 → `inserted` expression.
  - `DescribeAsync_MergeAction_IsAnswered` (17 and later).
  - `DescribeAsync_OperatorOfASchemaBeforeTheCatalog_KeepsNoOrigin`: in a scratch schema (`ScratchSchema`, as `Explain_CountShadowedOnTheSearchPath_PrintsUnqualified` does) create `shadow.max(timestamptz)` as an aggregate and an operator `shadow.*` of `(numeric, numeric)`, connect with `search_path=shadow,pg_catalog`, and `u.balance * 2`, `max(o.placed_at)` of a grouped join stay unknown.
- [ ] **Step 2: Run** on 18 and 14.24: PASS.  A test that fails is a finding: fix the code (Tasks 2 to 5), never the expectation, unless the expectation contradicts the spec.
- [ ] **Step 3:** `./pre-commit-validation.sh`; commit `Pin the new answers against the servers`.

### Task 7: Record, and lower the budgets

**Files:** `RealisticNullabilityNoiseTests.cs`; recorded files.

- [ ] **Step 1:** `tools/record-fixtures.sh`, then `git diff --stat` and read every changed capture and `expected.json`: a capture may only gain a `relations` call and its answer; an `expected.json` may only turn `nullable` `null`/`true` into `false` with `expression`.  `WalkMatrixTests` must still pass unchanged.
- [ ] **Step 2:** `tools/describe-end-to-end.sh`, then read every changed sidecar under `tests/SqlSource.Tests.Postgres/Realistic/Cases/`, `tests/SqlSource.Tests.Postgres/EndToEnd/` and `examples/PostgresExamples/`: only the changes the constraints allow.  The realistic cases expected to change are the spec's table (`group-by-having`, `array-and-string-agg`, `order-report-four-tables`, `insert-multirow-values`, `scalar-subquery-last-order`, `lateral-latest-order`, `upsert-product-returning`, `exists-as-column`, `merge-returning`).  A generated model whose property loses its `?` follows from it; build the examples.
- [ ] **Step 3:** Run `RealisticNullabilityNoiseTests` on 18 and on 14.24; set `Budgets` to `[14] = 44, [18] = 42`, or to what the failure message gives, which must be lower than 53 and 54; a column of the spec's table that stays unproved is named in the commit with the reason.  Run the whole `tests/SqlSource.Tests.Postgres` on both: PASS, the safety tests and the oracle included.
- [ ] **Step 4:** Count the PR's files (`git diff --stat origin/main | tail -1`): over 150 means the recordings move to a second PR (Global Constraints).  `./pre-commit-validation.sh`; commit `Record the answers of part D and lower the noise budgets`.

### Task 8: Documents

**Files:** `docs/architecture/postgres-nullability.md`, `src/SqlSource.Tool/AGENTS.md`, `docs/manual/postgresql.md`, `docs/tech-debt/TD-0051-columns-that-cannot-be-null-are-answered-nullable.md`, `docs/tech-debt/README.md`.

- [ ] **Step 1: Architecture.**  In "Expressions": the typed proof, the tables of `TotalFunctions` and their rule (exact signature, `pg_catalog` first, total), casts within the families, aggregates read only at a grouped `Aggregate` that computes them and never in a span a child printed, the direct reading then the following, subplans walked as plans of their own (`(SubPlan n)` as a whole column of a type in a family; `EXISTS`, `ANY`, `MERGE_ACTION()` of 18), system columns.  In "The plan": `relations` asked whenever a column has no origin.  Replace the sentence "A column of no origin through a scan is never resolved" only if it changed (it did not).
- [ ] **Step 2: `src/SqlSource.Tool/AGENTS.md`**, the rule beginning "A column of no origin is answered from its entry": add, one sentence each, that an operator, a function or an aggregate is read only by `TotalFunctions`' exact signatures with `pg_catalog` first, that an aggregate is read only at the grouped `Aggregate` that computes it, and that `(SubPlan n)` is read only as a whole column of a type in a family; keep `count(x)` never read.
- [ ] **Step 3: Manual**, `docs/manual/postgresql.md`'s nullability section: add to "never null when it is" arithmetic, comparisons, `||`, `lower`, `upper`, `length`, `date_trunc` and casts between numbers, between texts and between dates and times, over values that are never null; `max`, `min`, `sum`, `avg`, `string_agg`, `bool_and`, `bool_or` of values that are never null in a `GROUP BY` query, without `FILTER`; a subquery that returns `count(*)`; and, "on PostgreSQL 18", `EXISTS (...)`, `x IN (...)` where both sides are never null, and `merge_action()`.  Remove those from the "possibly null" list (its examples become `count(note)`, `array_agg(...)`, `upper(range)`) and from the known issue's list; the known issue keeps `count(x)`, `array_agg`, `EXISTS` on older versions, `VALUES`, a recursive `WITH` and a view.  The `"total!"` example of line 117 uses a `sum` that is now answered: change it to a column that is still possibly null in truth never null (`count(note)`, whose property is `long`), keeping the Dapper sentence true.  No internals: no plan, no `pg_catalog`, no ids.
- [ ] **Step 4: TD-0051** rewritten to what remains, with the counts after part D in its first paragraph: `count(x)` and polymorphic signatures (with the proposed fix: a call that tells whether any schema but `pg_catalog` holds a function or an operator of a name); functions and operators outside the tables; `EXISTS` and `IN` on 14 and an uncorrelated one (`InitPlan`); window aggregates and `FILTER`; `VALUES`; columns through scans; set operations as they are; the recursive CTE; `COALESCE` over both sides of a `FULL JOIN` (`full-join-accounts-reservations`); the ordinal of `WITH ORDINALITY` (`unnest-tags-with-ordinality`); grouping sets (`rollup-by-status`); a view; a `RETURNING` of a written value.  Its "Proposed fix" names this design too.  Update its row in `docs/tech-debt/README.md` if the summary changed.
- [ ] **Step 5:** `./pre-commit-validation.sh` (its documentation tests check links and `sql` samples); commit `Describe part D of the nullability noise and narrow TD-0051`.
