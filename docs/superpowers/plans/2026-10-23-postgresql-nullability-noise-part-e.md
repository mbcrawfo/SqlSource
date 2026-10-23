# The PostgreSQL describer's nullability noise, part E - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A column of no origin is answered `false` (`nullableSource` `expression`) in these cases, and nothing else changes:
- `count(x)`, an `array_agg` over a group, a `jsonb_build_object` or `json_build_object` without `VARIADIC`, a range constructor and a range intersection, each where no schema but `pg_catalog` holds the name;
- a `COALESCE`, `GREATEST` or `LEAST` computed at a `FULL JOIN` over a column of each side.

A `COALESCE`, `GREATEST`, `LEAST` or `CASE` also gets a type, so that forms over it are read.

**Architecture:** A new call, `overloads`, asks which of the names the plan prints a schema other than `pg_catalog` holds.  The walk hands the names that only `pg_catalog` holds to `PlanExpression`'s reader, which reads the rows of a new closed table, `CatalogOverloads`, by name.  The reader also types the variadic forms and `CASE` from their proved arguments.  At a `Full` join that computes an entry, the walk tells the reader which side a reference is proved on.

**Tech Stack:** C# (`net8.0` tool), xunit v3 with Shouldly, Npgsql, Testcontainers PostgreSQL 14.24 and 18.6.

**Spec:** `docs/superpowers/specs/2026-10-23-postgresql-nullability-noise-part-e-design.md`.  Read it first; its decisions are cited as D1 to D4 and its facts as F1 to F6.

## Global Constraints

- **Never a wrong `false`.**  No column the truth says can be `NULL` is answered `false`; `RealisticNullabilitySafetyTests` and the oracle hold it on 14 and 18.  When in doubt the column keeps the answer it has today.
- **Every diff of a recorded file is one of:**
  - a `true` or `null` turning into `false` with `nullableSource` `expression`;
  - a capture gaining the call `overloads`, only in a case whose plan prints a name of `CatalogOverloads`;
  - a query of the samples of Task 6 that changes.

  Anything else is a bug: stop and find it.
- **Every commit passes `./pre-commit-validation.sh`** (root `AGENTS.md`, "Commits") on the exact tree.  After any change to the describer or its tests, also run `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres` and see it pass.
- **Recorded files are recorded, never edited by hand.**
  - `tools/record-fixtures.sh postgres` writes the fixture captures and their `expected.json`.
  - `tools/describe-end-to-end.sh` writes the sidecars of the realistic cases, the end-to-end project and the examples, on the default image.

  Read every diff before committing it.
- **Lower the budgets in the commit that lowers the noise.**  `RealisticNullabilityNoiseTests.Budgets` starts at `{ [14] = 42, [18] = 40 }` and ends at `{ [14] = 33, [18] = 31 }`.  A task that lowers the noise lowers both budgets in its own commit, to what its run on each major counts.
- Every form of this part is read only when `WalkScope.CatalogFunctionsFirst` is true (the design's non-goal).
- A name is "only the catalog's" only when `overloads` was asked for it and answered it absent.  A call that failed or was not made leaves every name held elsewhere.
- No new `nullableSource` value, no change to the sidecar format; `formatVersion` stays 1.  A call is added and never renamed (`src/SqlSource.Tool/AGENTS.md`).
- The PR holds 150 files or fewer; the estimate is under 60.
- Docs are kept true in the commit that changes what they say: `src/SqlSource.Tool/AGENTS.md`, `docs/architecture/postgres-nullability.md`, `docs/architecture/postgres-describer.md`, the remarks of each class changed.  The manual, the examples' README, TD-0051 and `docs/tech-debt/README.md` are Task 6's.
- Comments and remarks follow the surrounding style: plain sentences, two spaces after a full stop, no part letters or task numbers in code.

## Review Focus

1. **Another schema holds the name, whatever the path's order**: `shadow.count(integer)` with `Search Path=pg_catalog,shadow,public`, and `shadow.*(numeric, numeric)` beside a range intersection.  The column stays unknown.  Owned by Task 4 (`DescribeAsync_CountOfAnArgumentThatAnotherSchemaTakes_StaysUnknown` kept; `DescribeAsync_RangeIntersectionWhenAnotherSchemaHoldsTheOperator_KeepsNoOrigin` added).
2. **`overloads` fails, or the answer lists a name**: unknown.  Owned by Task 4 (`DescribeAsync_OverloadsFails_KeepsNoOrigin`, `DescribeAsync_NameAnotherSchemaHolds_KeepsNoOrigin`).
3. **`jsonb_build_object(VARIADIC $1)`**, which is `NULL` for a `NULL` array: unknown.  Owned by Task 2 (unit) and Task 4 (container).
4. **`array_agg` with no `GROUP BY`, with `FILTER`, or as a window aggregate**: unknown even over a `NOT NULL` column.  Owned by Task 2 (unit) and Task 4 (container).
5. **A `FULL JOIN` whose `COALESCE` is not over one column of each side**: both arguments of one side; a side with a `LEFT JOIN` under it; the full join on the side a `LEFT JOIN` extends; a cast of a side's column.  Each stays unknown.  Owned by Task 5.

## Files

| File | Change |
|----|----|
| `src/SqlSource.Tool/Describing/Postgres/BuiltInTypes.cs` | `Json = 114`, `Jsonb = 3802`, `Int4range = 3904`, `Numrange = 3906`, `Tsrange = 3908`, `Tstzrange = 3910`, `Daterange = 3912`, `Int8range = 3926` |
| `src/SqlSource.Tool/Describing/Postgres/Walk/CatalogOverloads.cs` | Create: the table of D2 and `CatalogNames` |
| `src/SqlSource.Tool/Describing/Postgres/Walk/ExpressionContext.cs` | `CatalogAlone`, `ProvedOnFullJoinSide` |
| `Walk/PlanExpression.Forms.cs`, `.Proofs.cs`, `.Nullability.cs`, `PlanExpression.cs` | The forms of D2, D3 and D4; the scan of names |
| `Walk/NullabilityWalk.cs`, `Walk/NullabilityWalk.Expressions.cs`, `Walk/PlanInstances.cs` | `WalkScope.CatalogAlone`; the context at a `Full` join |
| `PostgresCallNames.cs`, `PostgresCalls.cs`, `PostgresLive.cs`, `PostgresEvents.cs`, `PostgresSession.Calls.cs`, `PostgresSession.cs` | The call `overloads` |
| `tests/SqlSource.Tool.Tests/PostgresScript.cs`, `PostgresPlans.cs` | `Overloads(...)`; a `Full` join helper if missing |
| Unit tests | `CatalogOverloadsTests` (new), `PlanExpressionTests`, `NullabilityWalkTests`, `PlanInstancesTests`, `PostgresEventsRelationTests` or a new `PostgresEventsOverloadsTests`, `PostgresSessionRelationTests` |
| Container tests | `PlanOutputTests`, `OverloadsCallTests` (new), `ExpressionTests`, `WalkTests`, `ScratchSchema` |
| `tests/SqlSource.Tests.Postgres/Realistic/Describer/RealisticNullabilityNoiseTests.cs` | `Budgets` |
| `tests/SqlSource.Tool.Tests/Fixtures/WalkMatrixTests.cs` | `walk-using`, `walk-natural`, the new `walk-count-argument` |
| Fixture cases | New `walk-count-argument`, `select-range-intersection`; re-recorded `walk-using`, `walk-natural` |
| Recorded | Realistic sidecars (8 cases), end-to-end and example sidecars |
| Docs | `docs/architecture/postgres-nullability.md`, `postgres-describer.md`, `src/SqlSource.Tool/AGENTS.md`, `docs/manual/postgresql.md`, `generator.md`, `recipes.md`, `examples/PostgresExamples/README.md`, TD-0051, `docs/tech-debt/README.md` |

---

### Task 1: Pin what the servers print

**Files:** Modify `tests/SqlSource.Tests.Postgres/Describer/PlanOutputTests.cs`.

**Interfaces:** Produces the exact texts the later unit tests use.  Consumes nothing.

Container tests, run on 18 by default and on 14 with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`.  Follow the file's `ExplainAsync` helper and `postgres.SkipUnlessRecordedMajor("The printed forms")`.  The fixture schema has `users u` (`id`, `name varchar(100) NOT NULL`, `email`, `status`, `created_at timestamptz NOT NULL`), `orders o` (`id bigint`, `user_id integer NOT NULL`, `total numeric NOT NULL`, `note`) and `bookings b` (`id`, `user_id integer NOT NULL`, `during tstzrange NOT NULL`).

- [ ] **Step 1: `Explain_PolymorphicForm_PrintsWhatTheReaderReads`**, a theory of `(select list, from, expected root entry)` with one text on both majors (F1):
  - `count(o.note)` over `orders o` → `count(note)`;
  - `count(DISTINCT u.status)` → `count(DISTINCT status)`;
  - `count(u.email) FILTER (WHERE u.id > 1)` → `count(email) FILTER (WHERE (id > 1))`;
  - `jsonb_build_object('id', u.id, 'email', u.email)` → `jsonb_build_object('id', id, 'email', email)`;
  - `json_build_object()` → `json_build_object()`;
  - `b.during * tstzrange(now(), now())` → the server's text; pin it.

  A row whose text a server prints otherwise is pinned as it prints.
- [ ] **Step 2: `Explain_PolymorphicFormsOfTheMajor_PrintAsEach`**, per major, the way the file splits a text by major:
  - `count(u.email) OVER (PARTITION BY u.status)` → `count(email) OVER (?)` on 14 and `count(email) OVER w1` on 18;
  - `jsonb_build_object(VARIADIC $1)` with a `text[]` parameter → `jsonb_build_object(VARIADIC $1)`;
  - `array_agg(DISTINCT o.id ORDER BY o.id)` under `GROUP BY o.user_id` → computed at the `Aggregate` with a `Group Key`.
- [ ] **Step 3: `Explain_CommonType_PrintsTheCastsOfTheArguments`** (F4): `sum(coalesce(o.total, 0))`, `max(coalesce(o.id, o.user_id))` and `sum(CASE WHEN o.id > 1 THEN 1 ELSE 2.5 END)` over `orders o GROUP BY o.user_id` print at the `Aggregate`:
  - `sum(COALESCE(total, '0'::numeric))`;
  - `max(COALESCE(id, (user_id)::bigint))`;
  - `sum(CASE WHEN (id > 1) THEN '1'::numeric ELSE 2.5 END)`.

  Pin the alias qualification as printed.
- [ ] **Step 4: `Explain_FullJoinCoalesce_IsComputedAtTheJoin`** (F5): a theory over no planner settings and over `enable_hashjoin=off`, `enable_nestloop=off`, on `SELECT coalesce(u.id, o.user_id) AS k, o.total FROM users u FULL JOIN orders o ON o.user_id = u.id`.  The node whose `Join Type` is `Full` prints `COALESCE(u.id, o.user_id)` in its `Output`, and no node under it prints it.  Use the file's way of passing planner settings to `ExplainAsync`, or `SET LOCAL` in its transaction.
- [ ] **Step 5: Run** `dotnet test --project tests/SqlSource.Tests.Postgres --filter "FullyQualifiedName~PlanOutputTests"`, then again with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`.  Expected: PASS on both.  Correct a pinned text to what the server prints.  If F1, F4 or F5 does not hold, stop and report it.
- [ ] **Step 6:** `./pre-commit-validation.sh`, then commit `Pin what the plan prints of polymorphic forms, common types and a full join's COALESCE`.

### Task 2: The table of names, and the reader's forms of it

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/Walk/CatalogOverloads.cs`, `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/CatalogOverloadsTests.cs`
- Modify: `BuiltInTypes.cs`, `Walk/ExpressionContext.cs`, `Walk/PlanExpression.Forms.cs`, `Walk/PlanExpression.Proofs.cs`, `Walk/PlanExpression.Nullability.cs`, `Walk/PlanExpression.cs`
- Test: `PlanExpressionTests.cs`, `BuiltInTypesTests.cs` if it lists names; container `PlanOutputTests.cs`

**Interfaces:**
- Produces:
  - `BuiltInTypes` constants of the Files table.
  - `internal sealed record CatalogNames(IReadOnlySet<string> Functions, IReadOnlySet<string> Operators)` with `static CatalogNames None { get; }`, both sets empty.  This is the names that only `pg_catalog` holds.
  - `internal static class CatalogOverloads`:
    - `IReadOnlyList<string> FunctionNames`: `count`, `array_agg`, `jsonb_build_object`, `json_build_object`, `int4range`, `int8range`, `numrange`, `tsrange`, `tstzrange`, `daterange`.
    - `IReadOnlyList<string> OperatorNames`: `*`.
    - `IEnumerable<(string Name, string Arguments, string Implementation)> Overloads`: F2's table row by row.  `Arguments` is `pg_get_function_identity_arguments` (`"any"`, `VARIADIC "any"`, `""`, `timestamp with time zone, timestamp with time zone, text`).  `Implementation` is `prosrc` for a function and `aggtransfn/aggfinalfn/agginitval` for an aggregate, `-` for none and `` for no initial value: `int8inc_any/-/0`, `array_agg_transfn/array_agg_finalfn/`.
    - `long? RangeTypeOf(string constructor)`: the OID of a range constructor's type, else null.
    - `bool IsRange(long type)`: one of the six.
  - `ExpressionContext` gains `CatalogNames? CatalogAlone = null`, appended after the existing members.  Null means none.
  - `PlanExpression.CatalogNamesIn(string entry) : (IReadOnlyList<string> Functions, IReadOnlyList<string> Operators)`, the names of `CatalogOverloads` the entry prints:
    - a lower-case bare word followed by `(`, except `count` whose parenthesis holds only `*`;
    - an operator token whose previous token ends an operand (`Close`, `Word`, `Number`, `String`, `QuotedName`, `CloseBracket`) and whose next starts one.

    An entry whose quotes or parentheses do not balance gives none.

- [ ] **Step 1: Write `CatalogOverloadsTests`** (unit):
  - `RangeTypeOf("tstzrange") == BuiltInTypes.Tstzrange`, and `RangeTypeOf("upper")` is null;
  - `IsRange(BuiltInTypes.Int4range)`, and not `IsRange(BuiltInTypes.Int4)`;
  - every name of `Overloads` is in `FunctionNames`, and every name of `FunctionNames` has a row;
  - `Overloads` has 20 rows.

  Run: FAIL (no type).
- [ ] **Step 2: Implement `CatalogOverloads`, `CatalogNames` and the constants.**  The remarks say:
  - D2's rule: read by name, only when no other schema holds it and `pg_catalog` is first;
  - what each row reads (D2's table);
  - why each overload is never `NULL` under its condition (F3, with the C functions named);
  - that a row goes in only with `Catalog_CatalogOverloads_ArePinned`.

  Run: PASS.
- [ ] **Step 3: Container `Catalog_CatalogOverloads_ArePinned`** in `PlanOutputTests`, after the file's `Catalog_TotalAggregates_ArePgCatalogsOwn`.  It collects every mismatch and fails with the list.
  - For each name of `FunctionNames`, the set of `(pg_get_function_identity_arguments(p.oid), implementation)` over `pg_catalog`'s `pg_proc` rows of that name equals the table's rows of the name.  The implementation is built from `prosrc`, or for `prokind = 'a'` from `pg_aggregate`'s `aggtransfn::text`, `aggfinalfn::text` (`-` for 0) and `coalesce(agginitval, '')`.
  - For each of the six range types, `SELECT pg_typeof(r * r)` over `'empty'::<type>` is that type, and `pg_cast` holds no row from it with `castcontext` `i` or `a`.
  - The OIDs of the new `BuiltInTypes` constants are named so (extend `Catalog_BuiltInTypes_AreNamedSo` if it lists them).

  Run on 18 and 14.24: PASS.  A row that fails on either is dropped from the table, and the commit says so.
- [ ] **Step 4: Write the failing `PlanExpressionTests`.**  The context has `catalogFunctions: true` and a `ProvedTypeOf` answering `o.id` → `Int8`, `u.id` → `Int4`, `during` and `b.during` → `Tstzrange`, `u.name` → `Varchar`, else null.  `All` is `CatalogNames` with every name of the table; `None` is `CatalogNames.None`.
  - `NullableOf_CatalogsAloneCount_IsNotNull`: with `All`, these are `false`: `count(o.id)`, `count(DISTINCT status)`, `count(email) FILTER (WHERE (id > 1))`, `count(email) OVER (?)`, `count(email) OVER w1`, `(count(o.id))`.
  - With `None`, and with `All` but `catalogFunctions: false`, the same texts are null; `count(*)` stays `false` with `None`.
  - `NullableOf_ArrayAggOverAGroupWithARow_IsNotNull`: with `All` and `GroupsHaveRows: true`, `array_agg(DISTINCT p.id ORDER BY p.id)` and `array_agg(o.note)` are `false`.  With `GroupsHaveRows: false`, and with a `FILTER (WHERE ...)` or an `OVER w1` after it, they are null.
  - `NullableOf_JsonBuildObject_IsNotNullButForVariadic`: with `All`, `jsonb_build_object('id', id, 'email', email)` and `json_build_object()` are `false`, and `jsonb_build_object(VARIADIC $1)` is null.  `ProvedTypeOf` of the first is `Jsonb` and of the second `Json`.
  - `NullableOf_RangeConstructorAndIntersection_AreNotNull`: with `All`, `tstzrange($1, $2)` and `int4range(1, 2, '[]'::text)` are `false`, of `Tstzrange` and `Int4range`.  `(during * tstzrange($1, $2))` is `false` of `Tstzrange`.  `tstzrange($1)` is null (one argument), and so is `(during * (u.id)::numeric)`.  With `All` minus the operator `*`, the intersection is null.
  - `NullableOf_PolymorphicNameNotInTheTable_IsUnknown`: `upper(during)`, `lower(during)`, `json_agg(o.note)` and `jsonb_agg(o.note)` under `All` and `GroupsHaveRows: true` are null.
  - `CatalogNamesIn_Entry_GivesTheNamesItCalls`:
    - `count(*)` → none;
    - `(count(o.id) + count(*))` → functions `[count]`;
    - `(during * tstzrange($1, $2))` → functions `[tstzrange]`, operators `[*]`;
    - `jsonb_build_object('a'::text, (x * 2))` → functions `[jsonb_build_object]`, operators `[*]`;
    - `upper(during)` → none;
    - `'count(x)'::text` → none (a string).

  Run: FAIL.
- [ ] **Step 5: Implement the reader.**
  - `count`: where `Form` and `Proved` accept `count(*)`, accept `IsCountOrRanking(from, to, anyArgument: true)` when `CatalogFunctions` and `CatalogAlone` holds `count`, of `Int8`.
  - `array_agg`: in `Aggregate`, a name `array_agg` held by `CatalogAlone` gives `TotalFunctions.UnknownType` without proving its arguments, under the same `_groupsHaveRows` and nothing-after-the-parenthesis rules.
  - `jsonb_build_object` and `json_build_object`: in `Function`, when held, give `Jsonb` or `Json` when the argument list is empty or its first token is not the word `VARIADIC`.
  - The range constructors: in `Function`, when held, give `RangeTypeOf(name)` for two or three arguments, without proving them.
  - The intersection: in `Operator`, `*` with both operands proved of one type for which `IsRange` holds, and `*` held by `CatalogAlone.Operators`, gives that type.  This comes after `TotalFunctions.OperatorResult`, which holds no range.
  - `CatalogNamesIn` walks the tokens of `PlanTokens.Read`.

  Run Step 4: PASS.  The whole of `PlanExpressionTests`: PASS unchanged.
- [ ] **Step 6:** `./pre-commit-validation.sh`; commit `Read count, array_agg, the JSON builders and ranges of pg_catalog by their name`.  No answer changes yet: nothing sets `CatalogAlone`.

### Task 3: The call `overloads`

**Files:**
- Modify: `PostgresCallNames.cs`, `PostgresCalls.cs`, `PostgresLive.cs`, `PostgresEvents.cs`, `PostgresSession.Calls.cs`, `tests/SqlSource.Tool.Tests/PostgresScript.cs`
- Create: `tests/SqlSource.Tests.Postgres/Describer/OverloadsCallTests.cs`
- Test: the events test class of the relations call (`PostgresEventsRelationTests`), or a new `PostgresEventsOverloadsTests` beside it; `ReplayingExchangeTests` only if it lists the calls

**Interfaces:**
- Produces:
  - `PostgresCallNames.Overloads = "overloads"`.
  - `internal sealed record PostgresOverloadsRequest(IReadOnlyList<string> Functions, IReadOnlyList<string> Operators)`, each sorted ordinally, each name once.
  - `internal sealed record PostgresOverloadsAnswer(IReadOnlyList<string>? Functions, IReadOnlyList<string>? Operators, PostgresServerError? Error)`: the names of the request that a schema other than `pg_catalog` holds, sorted; null lists with an error.
  - `PostgresLive.OverloadsAsync(PostgresOverloadsRequest asked, CancellationToken token) : Task<PostgresOverloadsAnswer>`.
  - `PostgresEvents.Overloads(IRunLog log, string query, PostgresOverloadsRequest request, PostgresOverloadsAnswer answer, long durationMs)`, shaped as `PostgresEvents.Relations`.  The members are `functions` and `operators` asked, and the answer's two lists, or its error as the other calls write one.
  - `PostgresSession.OverloadsAsync(string query, PostgresOverloadsRequest request, CancellationToken token)` in `PostgresSession.Calls.cs`, as `RelationsAsync` is.
  - `PostgresScript.Overloads(PostgresOverloadsRequest request, PostgresOverloadsAnswer answer)`, and `PostgresScript.Overloads(IReadOnlyList<string> functions, IReadOnlyList<string> operators)` that answers none held elsewhere.

- [ ] **Step 1: Container tests `OverloadsCallTests`**, in the shape of `RelationsCallTests`:
  - `OverloadsAsync_NamesOnlyTheCatalogHolds_AnswersNone`: functions `[count, jsonb_build_object, tstzrange]`, operators `[*]` → both lists empty, no error.  This runs without the shadow schema; see Task 4, Step 1.
  - `OverloadsAsync_NamesAnotherSchemaHolds_AnswersThem`: run `ScratchSchema.ShadowCount` and `ShadowOperators` and drop the schema in a `finally` (Task 4, Step 1 makes that the rule).  Asked functions `[array_agg, count, max]` and operators `[*, +]`, the answer is functions `[count, max]` and operators `[*]`.
  - `OverloadsAsync_NoNames_AnswersNone`: empty request → empty lists.

  Run: FAIL.
- [ ] **Step 2: Implement the records, the name and `PostgresLive.OverloadsAsync`.**  One command of two result sets, or two commands in one batch, as `PostgresLive` does elsewhere:

  ```sql
  SELECT DISTINCT p.proname::text FROM pg_catalog.pg_proc AS p
  WHERE p.proname = ANY ($1::text[]) AND p.pronamespace <> 'pg_catalog'::pg_catalog.regnamespace ORDER BY 1;
  SELECT DISTINCT o.oprname::text FROM pg_catalog.pg_operator AS o
  WHERE o.oprname = ANY ($2::text[]) AND o.oprnamespace <> 'pg_catalog'::pg_catalog.regnamespace ORDER BY 1;
  ```

  A `NpgsqlException` goes through `PostgresLive.AnswerAsync`, as every call's does.  Add the event, the session's call method and the script helpers.  Write the events test in the shape of the relations' (a logged request and answer; an error).  Run: PASS.
- [ ] **Step 3: `docs/architecture/postgres-describer.md`**: add `overloads` to the calls and their records, the order of a describe (after `relations`) and the table of members that came later, as `relations` is there.  In `src/SqlSource.Tool/AGENTS.md`, extend the rule of the order of a describe: `explain`, then `partitions`, then `relations`, then `overloads`.
- [ ] **Step 4:** `./pre-commit-validation.sh`; commit `Add the call overloads`.

### Task 4: Ask `overloads` and answer by name

**Files:**
- Modify: `PostgresSession.cs`, `Walk/NullabilityWalk.cs` (`WalkScope`), `Walk/NullabilityWalk.Expressions.cs`, `Walk/NullabilityWalk.SetOperations.cs` if it builds an `ExpressionContext`, `tests/SqlSource.Tests.Postgres/Describer/ScratchSchema.cs`, and the tests that run `ShadowCount` or `ShadowOperators`
- Create: fixture cases `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/walk-count-argument/query.sql` and `select-range-intersection/query.sql`
- Modify: `tests/SqlSource.Tool.Tests/Fixtures/WalkMatrixTests.cs`, `RealisticNullabilityNoiseTests.cs`, `examples/PostgresExamples/Models/Orders.sql` and `StoreQueries.cs`, `tests/SqlSource.Tests.Postgres/EndToEnd/Queries/Notes.sql` and `ModelShapeTests.cs`
- Test: `PostgresSessionRelationTests.cs` (or a new `PostgresSessionOverloadsTests.cs`), `NullabilityWalkTests.cs`, container `ExpressionTests.cs`

**Interfaces:**
- Consumes: Task 2's `CatalogNames`, `CatalogOverloads`, `PlanExpression.CatalogNamesIn`, `ExpressionContext.CatalogAlone`; Task 3's call.
- Produces:
  - `WalkScope` gains a last member `CatalogNames? CatalogAlone = null`.
  - `Expressions.ContextOf` passes `_scope.CatalogAlone` into every context it builds.
  - `CatalogOverloads.RequestOf(PlanNode root) : PostgresOverloadsRequest?`: the names `CatalogNamesIn` finds over every `Output` entry of every node of the tree, `SubPlan`, `InitPlan` and `Subquery` children included; null when none.

- [ ] **Step 1: Make the shadow schema a test's own.**  `ScratchSchema.ShadowCount` and `ShadowOperators` now change what other tests see: a `count(x)` or a range intersection in the `fixtures` database is unknown while `shadow` holds the name.  Move them to the list in the class comment of objects "made by its test and dropped in a finally".  Give `ScratchSchema` a `DropShadow = "DROP SCHEMA IF EXISTS shadow CASCADE"`.  Wrap every test that runs either in `try`/`finally` with it: `PlanOutputTests.Explain_CountShadowedOnTheSearchPath_PrintsUnqualified`, the `ExpressionTests` that use them, `OverloadsCallTests`, and any other the grep finds.
- [ ] **Step 2: Session tests** (scripted, in the shape of `DescribeAsync_ExpressionOverARelationNoColumnNames_AsksRelations`).  The plan is a grouped `Aggregate` over a `Left` join of `u` and `o` printing `u.id, count(o.id)`, with column types `[Int4, Int8]`:
  - `DescribeAsync_CountOfAnArgument_AsksOverloadsAndIsAnswered`: the script answers `overloads` (`[count]`, `[]`) with none held.  The second column is `false` with `expression`, and `exchange.Unused` is empty.
  - `DescribeAsync_NameAnotherSchemaHolds_KeepsNoOrigin`: the answer lists `count` → `null` with `no-origin`.
  - `DescribeAsync_OverloadsFails_KeepsNoOrigin`: an answer with an error → `null` `no-origin`, nothing reported.
  - `DescribeAsync_PlanOfCountStar_AsksNoOverloads`: `count(*)` → no call (`CaptureMissException` would fail it otherwise).
  - `DescribeAsync_CatalogNotFirst_AsksNoOverloads`: a `search_path` setting of `open` of `shadow, pg_catalog` → no call.

  Run: FAIL.
- [ ] **Step 3: Implement.**  In `PostgresSession.WalkAsync`, after `RelationsOfAsync`, add `OverloadsOfAsync(request, root, origins, scope, cancellationToken) : Task<CatalogNames>`.  It asks only when all of these hold, and returns `CatalogNames.None` otherwise:
  - a column has no origin;
  - `scope.CatalogFunctionsFirst`;
  - `CatalogOverloads.RequestOf(root)` is not null.

  Its result is the asked names minus the answered ones.  An answer with an error gives `None`.  Pass it as `scope with { Tables = relations, CatalogAlone = names }`.  Update the comment of `DescribePreparedAsync` that lists the calls.  Run Step 2: PASS; `PostgresSession*Tests` unchanged.
- [ ] **Step 4: Walk test** `Run_CountOfAnArgumentWithTheCatalogAlone_IsAnswered`: `NullabilityWalkTests`, the plan of Step 2, `WalkScope` with `CatalogAlone` holding `count` → `false` `expression`; without → `null` `no-origin`.  PASS.
- [ ] **Step 5: Container tests** in `ExpressionTests`:
  - `DescribeAsync_CountsAndRankings_AreAnsweredFromThePlan`: its `count(DISTINCT name)` becomes `NotNullExpression`.
  - Add `DescribeAsync_FormsOfTheCatalogByName_AreAnswered`, over `users u LEFT JOIN orders o ... GROUP BY u.id`:
    - `count(o.id)`, `count(o.note) FILTER (WHERE o.id > 1)`, `array_agg(o.id)` and `jsonb_build_object('id', u.id, 'email', u.email)` → `NotNullExpression`;
    - `array_agg(o.id) FILTER (WHERE o.id > 1)` and `jsonb_build_object(VARIADIC ARRAY['k', u.name])` → `UnknownNoOrigin`.
  - Add `DescribeAsync_ArrayAggWithoutAGroup_KeepsNoOrigin`: `SELECT array_agg(id) FROM users` → `UnknownNoOrigin`.
  - Add `DescribeAsync_RangeIntersection_IsAnswered`: `SELECT b.during * tstzrange(@from, @to) AS overlap FROM bookings b` → `NotNullExpression`.
  - Add `DescribeAsync_RangeIntersectionWhenAnotherSchemaHoldsTheOperator_KeepsNoOrigin`: with `ShadowOperators` and `Search Path=pg_catalog,shadow,public`, then the drop → `UnknownNoOrigin`.
  - `DescribeAsync_CountOfAnArgumentThatAnotherSchemaTakes_StaysUnknown` keeps passing, now through `overloads`.
- [ ] **Step 6: Fixtures.**  Add `walk-count-argument` (`SELECT u.id, count(o.id) AS n FROM users u LEFT JOIN orders o ON o.user_id = u.id GROUP BY u.id`) with the row `(Walked, [Kept, Expression])` in `WalkMatrixTests`.  Add `select-range-intersection` (`SELECT b.id, b.during * tstzrange(@from, @to) AS overlap FROM bookings b WHERE b.id = @id`).  Each has the fixture header comment the other cases have.  Run `tools/record-fixtures.sh postgres`.  The diff must be only the two new folders, each capture holding `overloads`.  If another capture changes, it is a bug.
- [ ] **Step 7: The samples of a mark** (the owner's decision).
  - In `examples/PostgresExamples/Models/Orders.sql`, `CountShippedOrders` keeps its query and drops `-- column: shipped not null`, since the count is now proved.  A new `ListUserDisplayNames` (`SELECT u.id, initcap(u.name) AS display_name FROM users AS u ORDER BY u.id`, with `-- column: display_name not null` and a summary) carries the marker, with its method in `StoreQueries.cs`.
  - In `tests/SqlSource.Tests.Postgres/EndToEnd/Queries/Notes.sql`, `CountAccountNotes` drops its `-- column:` line.  A new `ListDisplayNames` over `accounts` carries the marker on `initcap(a.name)`, with its row in `ModelShapeTests` (`DisplayName`, `string`, not null) and a read in `NoteQueryTests` if the file reads every query.
  - Check that no SQL Server file changes.
- [ ] **Step 8: Record and lower.**  Run `tools/describe-end-to-end.sh`.  The realistic sidecars change only for the 8 columns of the design's table but `full-join-accounts-reservations`.  Lower `Budgets` to `{ [14] = 34, [18] = 32 }`, or to what the noise test counts on each major; a difference from the design is reported in the commit.  Run the Postgres project on 18 and on 14.24: the noise, safety and oracle tests PASS.
- [ ] **Step 9: Docs** made wrong by this task:
  - `docs/architecture/postgres-nullability.md`, "The plan" and "Expressions": `overloads` after `relations`, and the forms by name with the rule of the call.
  - `src/SqlSource.Tool/AGENTS.md`: the rule "`count(x)` is never read" becomes "`count(x)` and the other names of `CatalogOverloads` are read only when `overloads` answered that no other schema holds them".
- [ ] **Step 10:** `./pre-commit-validation.sh`; commit `Ask which names another schema holds, and answer count(x), array_agg, the JSON builders and ranges`.

### Task 5: The type of `COALESCE`, `GREATEST`, `LEAST` and `CASE`, and a full join's `COALESCE`

**Files:** Modify `Walk/PlanExpression.Nullability.cs`, `Walk/PlanExpression.Proofs.cs`, `Walk/ExpressionContext.cs`, `Walk/NullabilityWalk.Expressions.cs`, `Walk/PlanInstances.cs`, `WalkMatrixTests.cs`, `RealisticNullabilityNoiseTests.cs`.  Test: `PlanExpressionTests`, `PlanInstancesTests`, `NullabilityWalkTests`, container `ExpressionTests` and `WalkTests`.

**Interfaces:**
- Produces:
  - `ExpressionContext` gains `Func<OutputReference, (bool OuterSide, long Type)?>? ProvedOnFullJoinSide = null`.  It gives the side of the `Full` join that computes the entry a reference is under, and its type, when the reference would be proved but for that join's extension.  It is null at any other node.
  - `PlanInstances.IsUnder(string alias, PlanNode node) : bool`: the instance of the alias is reached from `node` through `Outer` and `Inner` children.

**Commit A, D3:**
- [ ] **Step 1: Tests** `ProvedTypeOf_CommonTypeForm_IsItsArgumentsType` (`PlanExpressionTests`, with Task 2's `ProvedTypeOf`, plus `total` → `Numeric`, `quantity` → `Int4`):
  - `COALESCE(total, '0'::numeric)` → `Numeric`;
  - `COALESCE(o.note, 'x'::text)` → `Text`;
  - `GREATEST(quantity, 1)` → `Int4`;
  - `CASE WHEN (quantity > 1) THEN '1'::numeric ELSE 2.5 END` → `Numeric`;
  - `COALESCE(o.id, (u.id)::bigint)` → `Int8`;
  - `COALESCE(COALESCE(o.note, 'x'::text), o.note)` → `Text`;
  - `COALESCE(o.note, o.note)` → null.

  Also `NullableOf_FormsOverACommonType_AreRead`: with `GroupsHaveRows: true`, `sum(COALESCE(total, '0'::numeric))` and `(COALESCE(quantity, 0) + 1)` → `false`.  A domain's column, `COALESCE(amount, '1'::numeric)` with `amount` → a domain OID, gives no type.  Run: FAIL.
- [ ] **Step 2: Implement** in `Proved`.
  - A variadic call that part A proves not `NULL` is of the one type every argument proved of a known type has, when there is one.
  - A `CASE` takes its type from its results the same way.
  - No such type keeps today's null.
  - `UnknownType` arguments are skipped.

  Run: PASS; the class unchanged.
- [ ] **Step 3: Container** `DescribeAsync_FormsOverCommonTypes_AreAnswered` in `ExpressionTests`: `sum(coalesce(o.total, 0))` and `max(coalesce(o.id, o.user_id))` over `orders o GROUP BY o.user_id` → `NotNullExpression`.  `sum(coalesce(o.total, 0))` without a `GROUP BY` → `UnknownNoOrigin`.
- [ ] **Step 4:** Docs: in `postgres-nullability.md` "Expressions" and `PlanExpression`'s remarks, replace "of no known type" for these forms with D3's rule.  Run `tools/describe-end-to-end.sh` and the noise test on 18 and 14.24.  The design expects no change; any lowering lowers the budgets in this commit.  `./pre-commit-validation.sh`; commit `Type COALESCE, GREATEST, LEAST and CASE from their proved arguments`.

**Commit B, D4:**
- [ ] **Step 5: Tests.**
  - `PlanInstancesTests.IsUnder_InstanceOfASide_IsUnderThatChild`.
  - `PlanExpressionTests.NullableOf_CoalesceOverBothSidesOfAFullJoin_IsNotNull`, with a `ProvedOnFullJoinSide` answering `a.id` → `(true, Int4)` and `r.account_id` → `(false, Int4)`:
    - `COALESCE(a.id, r.account_id)`, `GREATEST(a.id, r.account_id)` and `LEAST(r.account_id, a.id)` → `false`, of `Int4`;
    - `COALESCE(a.id, a.id)` and `COALESCE(a.id, (r.account_id)::bigint)` → null;
    - without the callback → null.
  - `NullabilityWalkTests` over a `Hash Join` `Full` of `r` and `a` printing `COALESCE(a.id, r.account_id)` under a `Sort` printing it in parentheses: `Run_CoalesceAtAFullJoin_IsAnswered` → `false` `expression`.
  - Each of these keeps `no-origin`:
    - `Run_CoalesceAtAFullJoinUnderALeftJoin_KeepsNoOrigin`: the full join under the extended side of a `Left` join;
    - `Run_CoalesceAtAFullJoinOverAnExtendedSide_KeepsNoOrigin`: a `Left` join under one side;
    - `Run_CoalesceAboveAFullJoin_KeepsNoOrigin`: computed at a node above the join.

  Run: FAIL.
- [ ] **Step 6: Implement.**  In `ComputedAt`, at a node whose join type is `PlanJoinTypes.Full` and that computes the entry, set `ProvedOnFullJoinSide`.  For a reference, find the child (`Outer` or `Inner`) whose subtree the instance is under (`IsUnder`), require `!PlanInstances.ExtendsUnder(child)`, and prove it as `ProvedTypeOf` does, the instance's `NullExtended` aside.  In the reader, a variadic call whose arguments include a plain reference proved on each side is not `NULL`, of the two references' type when they agree.  Run Step 5: PASS.
- [ ] **Step 7: Container** in `WalkTests`, with the planner settings of a hash and of a merge join: `SELECT coalesce(u.id, o.user_id) AS k FROM users u FULL JOIN orders o ON o.user_id = u.id` → `NotNullExpression` on both.  The shapes of Review Focus 5 → `UnknownNoOrigin`.
- [ ] **Step 8: Record and lower.**  Change `WalkMatrixTests`: `walk-using` becomes `[Expression, Joined, Joined]` and `walk-natural` `[Expression, Expression, Joined, Joined]`.  Run `tools/record-fixtures.sh postgres`: only those two `expected.json` change.  Run `tools/describe-end-to-end.sh`: only `full-join-accounts-reservations` changes.  Lower `Budgets` to `{ [14] = 33, [18] = 31 }`.  Run the Postgres project on 18 and 14.24: PASS.
- [ ] **Step 9:** Docs: `postgres-nullability.md` "Expressions" gains D4's rule, and `src/SqlSource.Tool/AGENTS.md` the guard (a full join's form only at the join, over plain references, nothing extending under a side).  `./pre-commit-validation.sh`; commit `Answer COALESCE, GREATEST and LEAST over both sides of a full join`.

### Task 6: What a user reads, and the item

**Files:** `docs/manual/postgresql.md`, `docs/manual/generator.md`, `docs/manual/recipes.md`, `examples/PostgresExamples/README.md`, `docs/tech-debt/TD-0051-columns-that-cannot-be-null-are-answered-nullable.md`, `docs/tech-debt/README.md`, `docs/architecture/postgres-nullability.md` (the TD-0051 sentences).

- [ ] **Step 1: The manual's nullability** (`postgresql.md#nullability`):
  - The list of never-null forms gains: `count(x)`; `array_agg(...)` under `GROUP BY` without `FILTER`; `jsonb_build_object` and `json_build_object` not called with `VARIADIC`; a range's constructor and the intersection `*` of two ranges; `COALESCE`, `GREATEST` or `LEAST` over a column of each side of a `FULL JOIN`, as `USING` writes it.  Each is for a name that no schema of the database but `pg_catalog` holds.
  - The `COALESCE` line's "Each counts only on its own" becomes the rule that arithmetic, a comparison, a cast and an aggregate over one are read.
  - The list of what stays possibly null loses `count(note)`, `array_agg(...)` and `sum(coalesce(x, 0))`, and gains "a function or an operator of that name in another schema, such as one an extension installs".
  - The known issue at "Many columns that are never null" loses them.
- [ ] **Step 2: The samples of a mark** in `postgresql.md`, `generator.md#model-nullability`, `generator.md#column-marker` and `recipes.md` move from `count(note)` to `initcap(name)`, with the sentence "`initcap` is never null for a value that is not, though SqlSource treats it as possibly null".  The property becomes `string`, not `string?`.  Each `sql postgres` sample stays valid SQL for the test that parses it.  `examples/PostgresExamples/README.md` names `ListUserDisplayNames` for the marker and says `CountShippedOrders`'s count is shown never null.
- [ ] **Step 3: Rewrite TD-0051** to describe only what remains (`docs/tech-debt/AGENTS.md`, "Changing an item").
  - In "Problem": the counts become 33 and 31, with "after parts A to E" and the history sentence extended; the bullets of `count(x)` and polymorphic signatures and of `COALESCE` over a `FULL JOIN` go.
  - The `COALESCE` bullet is limited to what remains: a form over a type of no family.
  - The ordinality bullet says the plan prints nothing of `WITH ORDINALITY` (F6).
  - The bullet of functions outside the tables gains a polymorphic name another schema holds, and an overload `pg_catalog` holds that no row pins.
  - "Proposed fix" links this design and drops the two kinds done; the ordinality one becomes "the scan's column names from the server, which no call asks for".
  - The row in `docs/tech-debt/README.md` is kept true; the impact stays Medium.
- [ ] **Step 4:** `./pre-commit-validation.sh`; commit `Say what part E proves in the manual, and narrow TD-0051`.

### Task 7: Check the whole

- [ ] **Step 1:** `git diff --stat origin/main...HEAD | tail -1`: under 150 files.
- [ ] **Step 2:** `grep -rn "count(x)\|count(note)\|count(o.note)" docs src/SqlSource.Tool/AGENTS.md` finds no sentence that says `count(x)` is never read or possibly null, but in TD-0051 history and the design documents.
- [ ] **Step 3:** The Postgres project on 18 and on `postgres:14.24`, and `./pre-commit-validation.sh`: PASS.
