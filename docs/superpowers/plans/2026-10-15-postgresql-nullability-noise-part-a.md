# The PostgreSQL describer's nullability noise, part A: expressions - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A column with no origin whose entry of the plan's root `Output` is, as a whole, a `count`, a window ranking, a constant, an `IS NULL` test, or a `COALESCE`, `GREATEST`, `LEAST` or `CASE` over such values is recorded `false` with `nullableSource: "expression"`, and nothing else changes but `plan` for the queries now planned.

**Architecture:** A recogniser, `PlanExpression`, reads one entry of `Output` with a small tokeniser and answers `false`, `true` (`NULL::type`) or unknown.  The session asks for the plan when any column has no origin, as well as for a gated query; `NullabilityWalk` answers a no-origin column from the recogniser only in a plan with no set operation and no join that null-extends, and leaves every column with an origin of a query that is not gated as it was built.  A check that the root's `Output` fits the described columns guards against a plan of another statement (TD-0049).

**Tech Stack:** C# (`net8.0` tool), xunit v3 with Shouldly, Npgsql, Testcontainers PostgreSQL 14 and 18.

**Spec:** `docs/superpowers/specs/2026-10-15-postgresql-nullability-noise-design.md` (part A, "When a plan is asked for", and "What the plan prints").

## Global Constraints

- **Never a wrong `false`.**  Anything the code does not recognise keeps the answer it has today.  A form is trusted only after a container test on 14 and 18 shows the text the server prints for it.
- **Every diff of an expectation is one of three:** a `true` or `null` turning into `false`; a changed `nullableSource`; `plan` going from `not-needed` to `walked` or `unavailable`.  A `NULL::type` column going from `null no-origin` to `true expression` counts as the second.  Anything else is a bug: stop and find it.
- **Expectations are recorded, never edited by hand:** `tools/record-fixtures.sh`, and `tools/record-realistic.sh` twice, once plain and once with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14`.  Read the diff of each recording before committing it.
- **No call or record of the exchange changes.**  `PostgresExplainRequest` stays as it is; only which queries ask `explain` changes.  `formatVersion` stays 1.
- **Every commit passes `./pre-commit-validation.sh`**, which runs the container tests on `postgres:18` alone.  A task that touches the describer's answers also runs `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres` before its commit.
- **An `AGENTS.md` that names a file, type or member is updated in the commit that changes it**; tech-debt items follow `docs/tech-debt/AGENTS.md`.
- **The pull request holds 150 files or fewer.**  The estimate is about 135 (83 realistic expectations, 13 fixture cases of two files, about 25 of code, tests and docs).  The planned cut, if Task 7 counts more, is after Task 5: tasks 1 to 5 are one pull request, Task 6 the next.
- Test names are `Method_Scenario_Outcome`; assertions are Shouldly's; comments and XML docs follow the prose of the files around them.

## Review Focus

1. **A set operation whose first branch is a constant and another branch `NULL`.**  The root prints the first branch, `('a'::text)`, for a `UNION` (an `Aggregate` over an `Append`), a `UNION ALL` under `ORDER BY` (a `Sort` over an `Append`, which has an `Output`), and an `INTERSECT` or `EXCEPT` (a `SetOp`, over an `Append` on 14 and over `Outer` and `Inner` scans on 18).  Each must stay `null no-origin`.  Tests: Task 6, `Run_ExpressionOverASetOperation_KeepsNoOrigin` and the container theory `Describe_ConstantFirstBranchOfASetOperation_StaysUnknown`.
2. **A function the session's search path finds before `pg_catalog`'s.**  With `Search Path=shadow,pg_catalog,public` a `shadow.count(*)` that returns `NULL` prints as `count(*)`.  It must stay unknown.  Tests: Task 2 unit rows, Task 6 `Describe_CountShadowedOnTheSearchPath_StaysUnknown`.
3. **A value computed on the side a join null-extends.**  A constant or `COALESCE` of a subquery pulled up from a `LEFT JOIN` prints `('x'::text)` under a `Right` join, and a `LEFT JOIN LATERAL` count prints `(count(*))` under a `Nested Loop` `Left`.  Both stay unknown.  A `NOT EXISTS` of a query with no gate word plans as `Anti` (14) or `Right Anti` (18) and is answered.  Tests: Task 6, unit and container.
4. **Text that starts or ends like a form.**  `(count(*) + NULL::integer)`, `(COALESCE(o.note, 'x'::text) || NULL::text)`, `((a IS NULL) AND b)`, `(name = ') IS NULL'::text)`, a `CASE` whose `ELSE` is `NULL::text`, `"count"(*)`, `shadow.count(*)`, an unbalanced quote or parenthesis.  Each is unknown.  Tests: Task 1.
5. **A plan of another statement.**  A root whose reference names another column than the described one's origin is `unavailable` with `output-differs`, every column as built; a view's column, which the plan prints under the base table's name, does not trip it.  Tests: Task 5.

---

## What the servers print

Probed on 2026-10-15 against the fixture schema, `EXPLAIN (VERBOSE, FORMAT JSON)`, the same on 14 and 18 unless two are given.  The tests below use these texts.

| Query fragment | Root `Output` entry |
|----|----|
| `count(*)`, `coalesce(max(email::text), 'x')` over `users` | `count(*)`, `COALESCE(max((email)::text), 'x'::text)` |
| `'lit'`, `NULL::text`, `1`, `1.5`, `-1`, `-1.5`, `true`, `5000000000`, `1.5::float8` | `'lit'::text`, `NULL::text`, `1`, `1.5`, `'-1'::integer`, `'-1.5'::numeric`, `true`, `'5000000000'::bigint`, `'1.5'::double precision` |
| `count(*) FILTER (WHERE status = 'active')` under a window | `(count(*) FILTER (WHERE (status = 'active'::user_status)))` |
| `count(DISTINCT name)` under a window | `(count(DISTINCT name))` |
| `count(*) OVER ()` | 14: `count(*) OVER (?)`, 18: `count(*) OVER w1` |
| `row_number() OVER (ORDER BY id)` under a second window | 14: `(row_number() OVER (?))`, 18: `(row_number() OVER w2)`; `rank`, `dense_rank`, `percent_rank`, `cume_dist` the same |
| `u.deleted_at IS NULL`, `IS NOT NULL` | `(u.deleted_at IS NULL)`, `(u.deleted_at IS NOT NULL)` |
| `u.email IS DISTINCT FROM u.name` | `((u.email)::text IS DISTINCT FROM (u.name)::text)` |
| `u.email IS NOT DISTINCT FROM u.name` | `(NOT ((u.email)::text IS DISTINCT FROM (u.name)::text))` |
| `CASE WHEN u.deleted_at IS NULL THEN 'open' ELSE 'closed' END` | `CASE WHEN (u.deleted_at IS NULL) THEN 'open'::text ELSE 'closed'::text END` |
| `CASE u.status WHEN 'active' THEN 'A' END` (no `ELSE`) | `CASE u.status WHEN 'active'::user_status THEN 'A'::text ELSE NULL::text END` |
| `greatest(u.created_at, u.deleted_at)`, `least(1, u.id)` | `GREATEST(u.created_at, u.deleted_at)`, `LEAST(1, u.id)` |
| One table, `FROM users a ... ORDER BY id`: `deleted_at IS NULL`, `greatest(...)`, `coalesce(email, 'x')` | `((deleted_at IS NULL))`, `(GREATEST(created_at, deleted_at))`, `(COALESCE((email)::character varying, 'x'::character varying))`: bare names when the plan has one range table entry |
| `'x'::email_t` (a domain) | `('x'::character varying(254))::email_t` |
| `upper(u.name)`, `u.email::text` | `upper((u.name)::text)`, `(u.email)::text` |
| `SELECT 'a'::text, 1 FROM users UNION SELECT NULL, NULL FROM orders` | `Aggregate` over `Append` (`Member` scans): `('a'::text)`, `(1)` |
| `... INTERSECT ...`, `... EXCEPT ...` | 14: `SetOp` over `Append`, `('a'::text)`, `(0)`; 18: `SetOp` over `Outer` and `Inner` scans, `('a'::text)` |
| `SELECT 'a'::text UNION ALL SELECT NULL ORDER BY 1` | `Sort` over `Append`: `('a'::text)` |
| `users u LEFT JOIN (SELECT o.user_id, 'x'::text AS k, coalesce(o.note, 'n') AS c FROM orders o) s` | `Hash Join` `Right`: `u.id`, `('x'::text)`, `(COALESCE(o.note, 'n'::text))` |
| `users u LEFT JOIN LATERAL (SELECT count(*) ...) c ON true` | `Nested Loop` `Left`: `u.id`, `(count(*))` |
| `'k'::text, count(*) OVER () ... WHERE NOT EXISTS (...)` | 14: `Hash Join` `Anti`; 18: `Right Anti`; `'k'::text`, `count(*) OVER ...` |
| `count(*), 'p'::text FROM` a partitioned table | `Aggregate` over `Append` of the partitions |
| `UPDATE orders ... RETURNING id, 'done', note IS NULL` | `ModifyTable`: `id`, `'done'::text`, `(note IS NULL)` |
| `SET search_path = shadow, pg_catalog, public` with `shadow.count(*)` | `count(*), pg_catalog.count(*)`: the shadow prints bare, and returns `NULL` |

## Files

| File | Change |
|----|----|
| `src/SqlSource.Tool/Describing/Postgres/PlanExpression.cs` | Create: the tokeniser and the recogniser of one `Output` entry |
| `src/SqlSource.Tool/Describing/Postgres/PostgresSearchPath.cs` | Create: whether an unqualified function name is `pg_catalog`'s |
| `src/SqlSource.Tool/Describing/Postgres/NullabilityWalk.cs` | `WalkScope`; the output-fit check; expression answers; an ungated query's columns with an origin kept |
| `src/SqlSource.Tool/Describing/Postgres/PlanInstances.cs` | `WalkReasons.OutputDiffers`; each instance's relation; the sole alias; whether a set operation was visited |
| `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs` | Plan a query with a no-origin column; build the `WalkScope` |
| `src/SqlSource/Snapshot/SidecarValues.cs`, `schemas/sidecar-v1.schema.json`, `tests/SqlSource.Tests/Snapshot/SidecarFaker.cs` | `expression` |
| `tests/SqlSource.Tool.Tests/Describing/Postgres/PlanExpressionTests.cs`, `PostgresSearchPathTests.cs` | Create |
| `tests/SqlSource.Tool.Tests/Describing/Postgres/NullabilityWalkTests.cs`, `PlanInstancesTests.cs`, `PostgresSessionTests.cs` | Extend |
| `tests/SqlSource.Tests.Postgres/Describer/PlanOutputTests.cs`, `ExpressionTests.cs` | Create: the printed forms on 14 and 18; the describer's answers against a server |
| `tests/SqlSource.Tests.Postgres/Describer/WalkTests.cs` | An ungated query planned for an expression |
| `tests/SqlSource.Tests.Postgres/Realistic/RealisticNoiseBudgetTests.cs` | Lower `Budgets` |
| `tests/SqlSource.Tool.Tests/Fixtures/WalkMatrixTests.cs` | Only a `walk-*` cell that turns `false expression` |
| Recorded: `tests/SqlSource.Tests.Postgres/Realistic/Cases/*/expected-pg{14,18}.txt`, `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/*/{capture,expected}.json` | Re-recorded |
| Docs: `src/SqlSource.Tool/README.md`, `src/SqlSource.Tool/AGENTS.md`, `docs/superpowers/specs/2026-10-07-sidecar-format-design.md`, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, `docs/tech-debt/TD-0046-*.md`, `TD-0047-*.md`, `TD-0049-*.md`, `TD-0051-*.md`, `docs/tech-debt/README.md` | Kept true |

---

### Task 1: The recogniser of an `Output` entry

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/PlanExpression.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PlanExpressionTests.cs`

**Interfaces:**
- Consumes: `OutputReference.Parse(string)` and `OutputReference(string? Alias, string Column)`.
- Produces:

```csharp
internal static class PlanExpression
{
    /// <returns>false when the server can never return NULL for the entry, true for a NULL constant, null when the
    /// entry as a whole is no form this class knows.</returns>
    public static bool? NullableOf(string entry, bool catalogFunctions, Func<OutputReference, bool> provedNotNull);
}
```

`catalogFunctions` says whether an unqualified function name is `pg_catalog`'s (Task 2 decides it).  `provedNotNull` answers a reference that is an argument of `COALESCE`, `GREATEST`, `LEAST` or a `CASE` result; a reference that is the whole entry is never asked and is `null`.

- [ ] **Step 1: Write the failing tests**

`NullableOf_Form_IsItsAnswer`, a theory of `(entry, catalogFunctions, expected)` with `provedNotNull` answering `false` for every reference:

| Entry | `catalogFunctions` | Expected |
|----|----|----|
| `count(*)`, `(count(*))`, `(count(DISTINCT name))`, `(count(*) FILTER (WHERE (status = 'active'::user_status)))`, `count(*) OVER (?)`, `count(*) OVER w1` | true | false |
| `(row_number() OVER (?))`, `row_number() OVER w1`, `(rank() OVER w2)`, `(dense_rank() OVER w2)`, `(percent_rank() OVER w2)`, `(cume_dist() OVER w2)` | true | false |
| `'lit'::text`, `('a'::text)`, `1`, `1.5`, `true`, `false`, `'-1'::integer`, `'5000000000'::bigint`, `'1.5'::double precision`, `'x'::character varying(254)`, `'{}'::text[]`, `'it''s'::text` | true | false |
| `NULL::text`, `NULL::character varying(254)` | true | true |
| `(u.deleted_at IS NULL)`, `(u.deleted_at IS NOT NULL)`, `((deleted_at IS NULL))`, `((u.email)::text IS DISTINCT FROM (u.name)::text)`, `(NOT ((u.email)::text IS DISTINCT FROM (u.name)::text))` | false | false |
| `COALESCE(max((email)::text), 'x'::text)`, `(COALESCE((email)::character varying, 'x'::character varying))`, `LEAST(1, u.id)` | false | false |
| `CASE WHEN (u.deleted_at IS NULL) THEN 'open'::text ELSE 'closed'::text END`, `CASE WHEN CASE WHEN (id > 1) THEN true ELSE false END THEN 'then'::text ELSE 'else END'::text END`, `CASE WHEN (name = ' END '::text) THEN 'a'::text ELSE 'b'::text END` | false | false |

`NullableOf_NearMiss_IsUnknown`, a theory of entries that answer `null` with `catalogFunctions` true and every reference proved: `(count(*) + NULL::integer)`, `(COALESCE(o.note, 'x'::text) \|\| NULL::text)`, `((a IS NULL) AND b)`, `(name = ') IS NULL'::text)`, `"count"(*)`, `shadow.count(*)`, `pg_catalog.count(*)`, `row_number()`, `max(x)`, `upper((u.name)::text)`, `(u.email)::text`, `('x'::character varying(254))::email_t`, `CASE u.status WHEN 'active'::user_status THEN 'A'::text ELSE NULL::text END`, `CASE WHEN x THEN 'a'::text END`, `COALESCE(NULL::text, NULL::text)`, `u.id`, `id`, `(hashed SubPlan 2)`, `(ANY (u.id = (hashed SubPlan 2).col1))`, `(InitPlan 1).col1`, `E'a\\b'::text`, `(-1)`, `'open`, `(count(*)`, `count(*))`, the empty string.

`NullableOf_FunctionWhenTheCatalogIsNotFirst_IsUnknown`: `count(*)`, `count(*) OVER w1` and `(row_number() OVER (?))` with `catalogFunctions` false are `null`; `'a'::text`, `(x IS NULL)` and `COALESCE(x, 'a'::text)` stay `false`, since they call no function.

`NullableOf_ReferenceArgument_IsAskedOfTheWalk`: with `provedNotNull` true for `u.created_at` and `created_at` alone, `GREATEST(u.created_at, u.deleted_at)`, `(GREATEST(created_at, deleted_at))`, `COALESCE(u.deleted_at, u.created_at)` and `CASE WHEN (x > 1) THEN u.created_at ELSE 'epoch'::timestamp with time zone END` are `false`; `GREATEST(u.deleted_at, u.closed_at)` is `null`; the whole entry `u.created_at` is `null` and `provedNotNull` is never called for it (assert with a flag the function sets).

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class '*PlanExpressionTests'`
Expected: does not build, `PlanExpression` is not defined.

- [ ] **Step 3: Implement `PlanExpression.NullableOf`**

A private tokeniser reads the entry into tokens and returns none when a quote or a parenthesis does not balance: a word (a bare name or keyword, `[a-z_][a-z0-9_$]*` in either case), a quoted name (`"..."` with `""`), a string (`'...'` with `''`), a number (`[0-9]+(\.[0-9]+)?`, and anything that runs on into letters is "other"), `(`, `)`, `[`, `]`, `,`, `.`, `::`, each a token of its own, and "other" for any other run of symbols (`*`, `+`, `||`, `=`, `?`).  Spaces separate tokens and are dropped.  The recogniser works on a span of tokens and needs the whole span to be one form:

- **A span wrapped whole in one pair of parentheses** is a test of the inner span (below) or else the form of the inner span.
- **A constant:** a string, `::`, then a type name; a number; `true`; `false`.  `NULL`, `::`, then a type name is `true`.  A type name is words, quoted names and `.`, with an optional `(` numbers and commas `)` and any number of `[` `]` pairs, and nothing else.
- **`count` and the rankings:** an unqualified word `count`, `row_number`, `rank`, `dense_rank`, `percent_rank` or `cume_dist`, then a parenthesised group, then for `count` an optional `FILTER` and one parenthesised group, then ` OVER ` and either one parenthesised group or one word, to the end; `OVER` is optional for `count` and required for the rankings.  Each is `false` only when `catalogFunctions`.
- **`COALESCE`, `GREATEST`, `LEAST`** (upper case, as printed: keywords and not functions, so no search path): the name, then one parenthesised group to the end, split at its commas of depth zero; `false` when any argument is `false` by these rules or is a reference (`OutputReference.Parse` of the argument's text) that `provedNotNull` answers true.
- **`CASE` ... `END`:** the first token `CASE`, the last `END`, with `CASE` and `END` words counted as a nesting at parenthesis depth zero; the results are the spans after each `THEN` and after the `ELSE` of the outermost level, each ending at the next `WHEN`, `ELSE` or the final `END` of that level.  `false` when there is an `ELSE` and every result is `false` or a reference `provedNotNull` answers true.
- **A test, only inside a pair of parentheses:** an operand then `IS NULL`, `IS NOT NULL`, or an operand, `IS DISTINCT FROM`, an operand; or `NOT` followed by one parenthesised `IS DISTINCT FROM` test.  An operand is one primary, a reference, a constant, a parenthesised group or a call `name(...)`, followed by any number of `::` and a type name.  Anything else between the parentheses, an `AND` or an operator at depth zero, is no test.

Anything else, a cast of anything but a constant included, is `null`: the rule for casts is to recognise none (my call; `(u.email)::text` stays in TD-0051).

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class '*PlanExpressionTests'`
Expected: PASS.

- [ ] **Step 5: Commit**

Run `./pre-commit-validation.sh`, then:

```bash
git add src/SqlSource.Tool/Describing/Postgres/PlanExpression.cs tests/SqlSource.Tool.Tests/Describing/Postgres/PlanExpressionTests.cs
git commit -m "Read an entry of a plan's Output as an expression that cannot be NULL"
```

---

### Task 2: Whether a bare function name is `pg_catalog`'s

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/PostgresSearchPath.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSearchPathTests.cs`

**Interfaces:**
- Produces: `internal static class PostgresSearchPath { public static bool PutsCatalogFirst(string? searchPath); }`, given the `search_path` that `open` answered (`ServerInfo.Settings`, name `search_path`).

PostgreSQL searches `pg_catalog` first for a function unless the path names it later; `pg_temp` is never searched for a function, but the rule does not lean on that.

- [ ] **Step 1: Write the failing test**

`PutsCatalogFirst_SearchPath_IsItsAnswer`, a theory:

| `searchPath` | Expected |
|----|----|
| `"$user", public` | true |
| `public` | true |
| empty | true |
| `pg_catalog, public` | true |
| `"pg_catalog", public` | true |
| `PG_CATALOG, public` | true |
| `"Pg_Catalog", public` (another schema) | true |
| `public, pg_catalog` | false |
| `"$user", public, pg_catalog` | false |
| `shadow, pg_catalog, public` | false |
| `pg_temp, pg_catalog` | false |
| null | false |
| `"unclosed, pg_catalog` | false |

- [ ] **Step 2: Run it to see it fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class '*PostgresSearchPathTests'`
Expected: does not build.

- [ ] **Step 3: Implement `PutsCatalogFirst`**

Split at commas outside double quotes; trim each element; a quoted element is its text with `""` read as `"`, a bare one is lower-cased.  True when no element is `pg_catalog`, or the first is; false for null and for a quote that does not close.

- [ ] **Step 4: Run it to see it pass**, the same command, PASS.

- [ ] **Step 5: Commit**, after `./pre-commit-validation.sh`:

```bash
git add src/SqlSource.Tool/Describing/Postgres/PostgresSearchPath.cs tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSearchPathTests.cs
git commit -m "Say whether the session finds pg_catalog's functions first"
```

---

### Task 3: The printed forms, on 14 and 18

**Files:**
- Create: `tests/SqlSource.Tests.Postgres/Describer/PlanOutputTests.cs`

**Interfaces:**
- Consumes: `PlanExpression.NullableOf` (Task 1), `PlanNode.Read`, `PostgresContainer.ConnectionString` and `.Major`.

This is the test the spec requires before any form is trusted: it fails when a server prints a form otherwise than the recogniser reads it.

- [ ] **Step 1: Write the tests**

A helper runs `EXPLAIN (VERBOSE, FORMAT JSON) <sql>` on a data source of the test's own and returns `PlanNode.Read(text)!`.

`Explain_Form_PrintsWhatTheRecogniserReads`, a theory of `(sql, ordinal, printedOn14, printedOn18, expected)`: the root's `Output[ordinal]` is `printedOn14` or `printedOn18` by `postgres.Major` (skip on any other major), and `PlanExpression.NullableOf(entry, catalogFunctions: true, _ => false)` is `expected`.  One row for each row of "What the servers print" above that is a form or a near miss of a form, with its texts and its answer: the constants, `NULL::text`, `count` with `FILTER`, `DISTINCT` and `OVER`, each ranking, the tests, `IS NOT DISTINCT FROM`, both `CASE`s, `GREATEST` and `LEAST` (answer `null`, since every reference answers false here), the one-table `ORDER BY` query's three entries, the domain cast, `upper` and the cast of a column.

`Explain_SetOperation_PrintsTheFirstBranchAtTheRoot`: for the `UNION`, the `INTERSECT`, the `EXCEPT` and the `UNION ALL ... ORDER BY` above, the root's first entry is `('a'::text)` and some node reached through `Outer` and `Inner` children is an `Append` or a `SetOp`.  This is what Task 6's guard rests on.

`Explain_ShadowedCount_PrintsUnqualified`: on a connection with `Search Path=shadow,pg_catalog,public`, after creating, idempotently, `CREATE SCHEMA IF NOT EXISTS shadow; CREATE OR REPLACE FUNCTION shadow.nulls(int8) RETURNS int8 LANGUAGE sql AS 'SELECT NULL::int8'; CREATE OR REPLACE AGGREGATE shadow.count(*) (sfunc = shadow.nulls, stype = int8);`, the root of `SELECT count(*) FROM users` prints `count(*)`, and `SELECT count(*) IS NULL FROM users` returns true.  This is what Task 2's rule rests on.

- [ ] **Step 2: Run them on 18 and on 14**

Run: `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class '*PlanOutputTests'`, then the same with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14`.
Expected: PASS on both.  A row that fails on either is a form the recogniser must not trust as written: fix Task 1's rule for it, or move the row to the near misses with `null`.

- [ ] **Step 3: Commit**, after `./pre-commit-validation.sh`:

```bash
git add tests/SqlSource.Tests.Postgres/Describer/PlanOutputTests.cs
git commit -m "Pin the plan's printed expressions on PostgreSQL 14 and 18"
```

---

### Task 4: The sidecar value `expression`

**Files:**
- Modify: `src/SqlSource/Snapshot/SidecarValues.cs` (`NullableSource`), `schemas/sidecar-v1.schema.json` (the `nullableSource` enum), `tests/SqlSource.Tests/Snapshot/SidecarFaker.cs` (`NullableSources`)
- Modify: `docs/superpowers/specs/2026-10-07-sidecar-format-design.md` (the `nullableSource` row near line 85 and the schema copy near line 1153), `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (the provenance list near line 367)

**Interfaces:**
- Produces: `SidecarValues.NullableSource.Expression = "expression"`, listed after `ForeignTable` and before `NoOrigin` everywhere the values are listed, as commit `b91384c` added `foreign-table`.

- [ ] **Step 1: Add the value** in the five places.  The format design's row says: `"expression"`, on PostgreSQL the plan prints the column as an expression that can never be `NULL`, or as a `NULL` constant.  The epic outline's list gains the word.
- [ ] **Step 2: Run** `dotnet test --project tests/SqlSource.Tests`.  Expected: PASS (the faker draws from the list; the schema test reads the file).
- [ ] **Step 3: Commit**, after `./pre-commit-validation.sh`: `git commit -m "Add the nullableSource expression to sidecar format 1"` with the five files.

---

### Task 5: Plan a query that has a column with no origin

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/NullabilityWalk.cs`, `PlanInstances.cs` (`WalkReasons`), `PostgresSession.cs:293-388`
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/NullabilityWalkTests.cs`, `PostgresSessionTests.cs`, `tests/SqlSource.Tests.Postgres/Describer/WalkTests.cs`
- Recorded: the fixture cases and realistic expectations that change
- Docs: `src/SqlSource.Tool/README.md`, `src/SqlSource.Tool/AGENTS.md`, `docs/superpowers/specs/2026-10-07-sidecar-format-design.md` (`plan` row, line 60), `docs/tech-debt/TD-0046-*.md` (line 17), `TD-0047-*.md`, `TD-0049-*.md`, `docs/tech-debt/README.md`

**Interfaces:**
- Produces:

```csharp
/// <param name="Gated">Whether OuterJoinGate found a word: only then does the plan answer a column with an origin.</param>
/// <param name="OldAndNewMayBeNamed">As today's parameter of Run.</param>
internal sealed record WalkScope(bool Gated, bool OldAndNewMayBeNamed);

public static WalkOutcome Run(PlanNode root, IReadOnlyList<SidecarColumn> columns, IReadOnlyList<ColumnOrigin?> origins, WalkScope scope);

// In WalkReasons:
public const string OutputDiffers = "output-differs";
```

This task changes no answer: it plans more queries, keeps their columns as built, and adds the check.  Task 6 gives the answers.

- [ ] **Step 1: Write the failing unit tests**

In `NullabilityWalkTests`, give the `Built` helper a `gated` argument (the session builds an ungated query's columns with `gated: false`) and the `Run` helper a `WalkScope`; the existing tests pass `new WalkScope(true, oldAndNewMayBeNamed)`.

- `Run_UngatedQueryWhoseEntryCannotBeMapped_KeepsTheCatalogsAnswer`: built ungated `NotNull, Expression`; root `Over("Subquery Scan", Scan("t"), "Subquery") with { Alias = "s", Output = ["s.id", "count(*)"] }`; plan `walked`, answers `[(false, "catalog"), (null, "no-origin")]`.
- `Run_UngatedQueryWithAPlanThatCannotBeUsed_KeepsTheCatalogsAnswer`: a doubled alias under an `Inner` join; `unavailable`, `doubled-alias`, `[(false, "catalog")]`.
- `Run_ReferenceThatNamesAnotherColumnThanTheOrigin_IsUnavailableWithOutputDiffers`: gated `NotNull`; root `Scan("t", "t.opt")`; `unavailable`, `output-differs`, `[(true, "heuristic")]`.  The same with a bare `opt` at a scan root.
- `Run_ViewsColumnPrintedUnderTheBaseTablesName_IsWalked`: gated `OfView` (attribute `id`); root `Scan("t", "t.opt")`; `walked`, `[(null, "view")]`: the check reads a table's origin alone.
- `Run_UnmappedReferenceAndExpression_PassTheCheck`: gated `NotNull, NotNull`; root `LeftJoin("x.other", "a.id + 1")`; `walked`.

In `PostgresSessionTests`:
- `DescribeAsync_QueryWithoutAGateWord_TakesNullabilityFromTheCatalogAndNeedsNoPlan` becomes `DescribeAsync_QueryWithoutAGateWordWithAnExpression_AsksForThePlanAndKeepsTheCatalogsAnswer`: `DescribeUsersIdAsync` with an `explain` answering `Text(Over("WindowAgg", Scan("users")) with { Output = ["users.id", "count(*) OVER w1"] })`; plan `walked`; `[(false, "catalog"), (null, "no-origin")]`; `exchange.Unused` empty.
- `DescribeAsync_QueryWhoseEveryColumnHasAnOrigin_NeedsNoPlan`: an ungated `SELECT id FROM users`, no `explain` in the script, plan `not-needed`.
- The test at `PostgresSessionTests.cs:745` (`SELECT 1, 'a'::varchar(100), 2::numeric(18,2)`) gains an `Explains` answering a `Result` with the three constants as `Output`, and expects plan `walked`.
- `DescribeAsync_UngatedQueryWithAnExpressionExplainAnswers26000_IsSqlsrc236AtExplain`, beside the gated one at line 439.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class '*NullabilityWalkTests' --filter-class '*PostgresSessionTests'`
Expected: does not build (`WalkScope`), then the new rows fail.

- [ ] **Step 3: Implement**

- `NullabilityWalk.Run` takes the scope.  After the `NoOutput` and `instances.Reason` checks, the output-fit check: for each column whose origin's `Table.Relkind` is `r` or `p` and whose entry parses as a reference that maps (a qualified alias `instances.TryFind` finds, or a bare name when `instances.RootNamesAnInstance`), the reference's `Column` equals `origin.Attribute.Name` ordinally; one that differs makes the plan `Unusable(WalkReasons.OutputDiffers, ...)`.  The entry count stays "at least as many as the columns": a sort prints its keys after the target list.
- `ColumnOf` keeps a column with an origin as it was built when `!scope.Gated`, as it keeps one without an origin.
- `PostgresSession.DescribePreparedAsync`: `var planned = gated || (columns.Count > 0 && origins.Any(static origin => origin is null));` and the walk runs when `planned`, with `new WalkScope(gated, OuterJoinGate.NamesOldOrNew(request.Sql))` built there and passed through `WalkAsync`, which no longer calls `NamesOldOrNew` itself.  The `26000` path is unchanged.  Rewrite the comment above the call ("for a gated query alone") and the class's remark.

- [ ] **Step 4: Run the unit tests to see them pass**, the command of Step 2.

- [ ] **Step 5: Add the container tests to `WalkTests`**

- `Describe_UngatedQueryWithAnExpression_IsWalkedAndKeepsTheCatalogsAnswer`: `SELECT u.id, count(*) OVER () AS n FROM users u`; plan `walked`; `[(false, "catalog"), (null, "no-origin")]`.
- `Describe_UngatedQueryWhoseEveryColumnHasAnOrigin_NeedsNoPlan`: `SELECT u.id, u.name FROM users u`; plan `not-needed`.

Run: `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class '*WalkTests'`, then with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14`.  Expected: PASS.

- [ ] **Step 6: Re-record and read the diffs**

Run `tools/record-fixtures.sh`, `tools/record-realistic.sh`, `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 tools/record-realistic.sh`.  Expected diff: `plan: not-needed` to `walked` or `unavailable` in the expectations of queries with a no-origin column, an `explain` call added to the captures of those fixtures, and no change to any `nullable` or `nullableSource`.  `git diff --stat | tail -1` gives the count of files so far.  The budget test passes unchanged.

- [ ] **Step 7: Update the docs**

- `src/SqlSource.Tool/README.md`: the plan is also asked for a query with rows and a column that comes from no table; the sentence on `IMMUTABLE` functions says "planning a query" with either reason; `not-needed` is "a query with rows, none of the words, and every column a table's or a view's".
- `src/SqlSource.Tool/AGENTS.md`: the order of calls (line 28, "for a gated query with rows, `explain`"), the `plan` sentence (line 30), when `explain` is asked (line 32, "for a gated query with rows alone"), the reasons a plan cannot be used (line 33, `output-differs`), and that an ungated query's columns with an origin keep the catalog's answer.
- The format design's `plan` row: `not-needed` is "no column the plan could answer" (the spec's wording).
- TD-0047: planning runs for a gated query and for one with a column of no origin, so most queries with an expression are planned.
- TD-0049: the check catches a stale plan whose references name other columns than this statement's origins; it does not catch one whose references agree, or one of expressions alone, which part A could read.
- TD-0046: rewrite the bullet at line 17: a query with a no-origin column is planned for the expression, so "a gated query none of whose columns has an origin" is no longer a plan for nothing; keep the `left(name, 3)` half.
- `docs/tech-debt/README.md`: the rows of TD-0046, TD-0047 and TD-0049 follow their items.

- [ ] **Step 8: Commit**, after `./pre-commit-validation.sh` and the 14 run of `tests/SqlSource.Tests.Postgres`:

```bash
git add -A src tests docs
git commit -m "Plan a query that has a column of no origin, and check that the plan is this statement's"
```

---

### Task 6: Answer an expression from the plan

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/NullabilityWalk.cs`, `PlanInstances.cs`, `PostgresSession.cs`
- Test: `NullabilityWalkTests.cs`, `PlanInstancesTests.cs`, `PostgresSessionTests.cs`; create `tests/SqlSource.Tests.Postgres/Describer/ExpressionTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Realistic/RealisticNoiseBudgetTests.cs`, and `tests/SqlSource.Tool.Tests/Fixtures/WalkMatrixTests.cs` only if a `walk-*` case changes
- Recorded: fixtures and realistic expectations
- Docs: `src/SqlSource.Tool/README.md`, `src/SqlSource.Tool/AGENTS.md`, `docs/tech-debt/TD-0051-*.md`, `docs/tech-debt/README.md`

**Interfaces:**
- Consumes: `PlanExpression.NullableOf` (Task 1), `PostgresSearchPath.PutsCatalogFirst` (Task 2), `SidecarValues.NullableSource.Expression` (Task 4), `WalkScope` (Task 5).
- Produces:

```csharp
internal sealed record WalkScope(
    bool Gated,
    bool OldAndNewMayBeNamed,
    bool CatalogFunctionsFirst,
    IReadOnlyDictionary<long, PostgresTableRow> Tables
);

// PlanInstances:
public bool HasSetOperation { get; }          // a visited node is "Append", "Merge Append", "SetOp" or "Recursive Union"
public string? SoleAlias { get; }             // the alias when the plan has exactly one relation instance
public bool TryFindRelation(string alias, out string schema, out string relation);
```

- [ ] **Step 1: Write the failing unit tests**

In `PlanInstancesTests`: `HasSetOperation` is true for an `Aggregate` over an `Append` (whose children are `Member`s), a `Sort` over an `Append`, and a `SetOp` over an `Outer` and an `Inner` scan, and false for a join; `SoleAlias` is the alias of a scan under a `Sort`, of a `ModifyTable` over a scan of its own alias, and null under a join of two; `TryFindRelation` gives `public` and the table's name for `Scan("a")` and false for an unknown alias.

The record's two new members change every `new WalkScope(...)` of Task 5: the helpers pass `CatalogFunctionsFirst: true` and their `Tables`, and the session passes Step 3's values.  Add tables `a` and `b` to the helper's `Tables` as `Table with { Oid = ..., Name = "a" }`, for the scans of `PostgresPlans.Join`.

In `NullabilityWalkTests`, with `CatalogFunctionsFirst` true unless a row says otherwise:

- `Run_ExpressionWithNoJoin_IsAnsweredFromItsEntry`: ungated `Expression` ×4; root `Over("Sort", Scan("t")) with { Output = ["(count(*))", "'a'::text", "NULL::text", "upper((t.opt)::text)"] }`; `[(false, "expression"), (false, "expression"), (true, "expression"), (null, "no-origin")]`.
- `Run_ExpressionUnderAnInnerOrSemiJoin_IsAnswered` and `..._UnderAJoinThatExtends_KeepsNoOrigin`: `'k'::text` at the root of a `Join("Inner", ...)` is `false expression`; of `Left`, `Right` and `Full`, `null no-origin`, gated or not.
- `Run_ExpressionUnderAnAntiJoin_IsAnsweredOnlyWhenNotGated`: `Anti` and `Right Anti`, ungated `false expression`, gated `null no-origin`.
- `Run_ExpressionOverASetOperation_KeepsNoOrigin`: `('a'::text)` at an `Aggregate` over an `Append` of two `Member` scans, at a `Sort` over the same, and at a `SetOp` over an `Outer` and an `Inner` scan: `null no-origin`, plan `walked`.
- `Run_FunctionWhenTheCatalogIsNotFirst_KeepsNoOrigin`: `count(*)` with `CatalogFunctionsFirst` false.
- `Run_ReferenceArgument_IsTheCatalogsAnswerOfItsInstance`: `GREATEST(a.id, a.opt)` under an `Inner` join of `Scan("a")` and `Scan("b")`, with `Tables` holding tables named `a` and `b` like `Table`, is `false`; `GREATEST(a.opt, b.opt)` stays `null`; a bare `GREATEST(id, opt)` at a `Sort` over the one `Scan("t")` is `false`; the same bare entry under a join of two is `null`; under `OldAndNewMayBeNamed` at a `ModifyTable` aliased `t`, `COALESCE(old.id, NULL::integer)` is `null`.

In `PostgresSessionTests`, the test renamed in Task 5 now expects `[(false, "catalog"), (false, "expression")]`, and `DescribeAsync_SearchPathThatPutsTheCatalogLast_LeavesAFunctionUnknown` opens with a script whose `open` answers `search_path` `public, pg_catalog` and expects `(null, "no-origin")` for the same `count(*) OVER w1`.

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class '*NullabilityWalkTests' --filter-class '*PlanInstancesTests' --filter-class '*PostgresSessionTests'`
Expected: does not build, then the new rows fail.

- [ ] **Step 3: Implement**

- `PlanInstances` keeps each instance's `Schema` and `RelationName` beside whether it is extended (a `ModifyTable` root's are its own), and sets `HasSetOperation` and `SoleAlias` while it visits.
- `NullabilityWalk`: expressions are answered when `!instances.HasSetOperation` and every visited node's `NullExtends` is `none`, or, in an ungated scope, its `JoinType` is `Anti` or `Right Anti`.  A column without an origin is then `PlanExpression.NullableOf(entry, scope.CatalogFunctionsFirst, ProvedNotNull)`, and an answer that is not null sets `Nullable` and `NullableSource = Expression`; otherwise it is kept.  `ProvedNotNull(reference)`: the alias is the reference's, or `instances.SoleAlias` for a bare name when `!scope.OldAndNewMayBeNamed`; at a `ModifyTable` under `OldAndNewMayBeNamed` only the target's alias counts, as for a column; the instance is not extended; `TryFindRelation` gives a schema and a name that a row of `scope.Tables` has, ordinally; the row has an attribute of the reference's name; and `ColumnNullability.Of(new ColumnOrigin(row, attribute), gated: false).Nullable` is `false`.  No new call is made: a relation that `tables` did not answer leaves the reference unproved (my call).
- `PostgresSession`: the scope takes `PostgresSearchPath.PutsCatalogFirst` of `Server.Settings`' `search_path` and `tables.Value`.
- Rewrite the remarks of `NullabilityWalk` ("Nothing of the plan can make a column without an origin non-nullable") and of `ColumnNullability` to match.

- [ ] **Step 4: Run the unit tests to see them pass**, the command of Step 2.

- [ ] **Step 5: Write the container tests, `ExpressionTests`**

In the collection `UsesPostgres`, describing through `Sessions.DescribeAsync` and reading `(Nullable, NullableSource)` of each column:

- `Describe_ExpressionsOfOneTable_AreAnsweredFromThePlan`: `SELECT a.id, coalesce(a.email, 'none') AS contact, CASE WHEN a.deleted_at IS NULL THEN 'open' ELSE 'closed' END AS state, CASE a.status WHEN 'active' THEN 'A' END AS label, greatest(a.created_at, a.deleted_at) AS changed, a.deleted_at IS NULL AS live, 'v1' AS version, NULL::text AS nothing, a.email::text AS raw FROM users a ORDER BY a.id`; `[(false, catalog), (false, expression), (false, expression), (null, no-origin), (false, expression), (false, expression), (false, expression), (true, expression), (null, no-origin)]`.
- `Describe_CountsAndRankings_AreAnsweredFromThePlan`: `count(*)`, `count(*) FILTER (...)`, `count(DISTINCT ...)`, `count(*) OVER ()`, `row_number() OVER (...)`, `rank() OVER (...)` over `users` with `GROUP BY id`: each `false expression`.
- `Describe_ConstantFirstBranchOfASetOperation_StaysUnknown`, a theory over the four set operations of Task 3 and `WITH RECURSIVE r(n, k) AS (SELECT 1, 'a'::text UNION ALL SELECT n + 1, NULL FROM r WHERE n < 3) SELECT n, k FROM r`: column 0 `(null, "no-origin")`.
- `Describe_ExpressionFromTheSideALeftJoinExtends_StaysUnknown`, a theory over the pulled-up subquery and the `LEFT JOIN LATERAL` count of "What the servers print": the expression columns `(null, "no-origin")`.
- `Describe_ExpressionBesideNotExists_IsAnswered`: `SELECT u.id, 'k' AS k FROM users u WHERE NOT EXISTS (SELECT 1 FROM orders o WHERE o.user_id = u.id)`; `k` is `(false, "expression")`.
- `Describe_PartitionedTableAggregate_StaysUnknown`: `count(*)` over `walk_parts` (create it as `WalkTests` does): `(null, "no-origin")`, for part B to lift.
- `Describe_CountShadowedOnTheSearchPath_StaysUnknown`: with the shadow aggregate of Task 3, `SELECT count(*) AS n FROM users` on `{connection};Search Path=shadow,pg_catalog,public` is `(null, "no-origin")`, and on `{connection};Search Path=pg_catalog,shadow,public` it is `(false, "expression")`.

Run on 18 and 14: `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class '*ExpressionTests'`.  Expected: PASS on both.

- [ ] **Step 6: Re-record, read the diffs, lower the budgets**

Run the three recordings of Task 5 Step 6.  Every changed line must be a `null no-origin` turning `false expression`, or a `NULL` constant turning `true expression`; check each against the case's `case.json`, whose truth is the judge.  A `walk-*` fixture that changes gets its cell in `WalkMatrixTests` decided by hand from the case's SQL first, with a constant `Expression = "false expression"`; a recording that disagrees with that is a bug.  Run `RealisticNoiseBudgetTests` on 18 and 14; each failure names the new count; set `Budgets[14]` and `Budgets[18]` to them.

- [ ] **Step 7: Update the docs**

- `src/SqlSource.Tool/README.md`: two rows in the table of `nullable`, `false` `expression` ("A column the plan shows to be a count, a window ranking, a constant, an `IS NULL` test, or a `COALESCE`, `GREATEST`, `LEAST` or `CASE` with an `ELSE` of values that cannot be `NULL`") and `true` `expression` ("A `NULL` constant"), and `no-origin` becomes "a column that comes from no table and that the plan does not answer".
- `src/SqlSource.Tool/AGENTS.md`: the nullability paragraph (line 30) and "The walk degrades a column alone" (line 36) say when an expression is answered: no set operation, no join that extends but an ungated anti join, `pg_catalog` first on the session's path, references only through a relation that `tables` answered; and name `PlanExpression` and `PostgresSearchPath`.
- TD-0051: the counts are the new budgets; the expression bullet keeps only what remains: strict functions and operators and casts (`(u.email)::text`), aggregates other than `count`, a `CASE` without `ELSE` where it is truly nullable is no noise; add an expression in a plan with an outer join or a partitioned table (lifted by part B), and a session whose search path puts `pg_catalog` after another schema.
- `docs/tech-debt/README.md`: TD-0051's row.

- [ ] **Step 8: Commit**, after `./pre-commit-validation.sh` and the 14 run:

```bash
git add -A src tests docs
git commit -m "Answer an expression of the plan that can never be NULL"
```

---

### Task 7: Check the whole branch

- [ ] **Step 1:** `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres`.  Expected: PASS, the safety checks of `RealisticQueryTests` among them.
- [ ] **Step 2:** `./pre-commit-validation.sh`.  Expected: every step `passed`.
- [ ] **Step 3:** `git diff --stat main...HEAD | tail -1`.  Expected: 150 files or fewer.  If more, stop and tell the user before opening anything: the cut is after Task 5's commit.
- [ ] **Step 4:** `git diff main...HEAD -- '*expected-pg*.txt' '*expected.json' | grep '^[-+]' | grep -v '^[-+][-+]'` and read every pair against the Global Constraints' three kinds of diff.
- [ ] **Step 5:** before the pull request, the version check of `AGENTS.md` ("Check the version before you open a pull request").
