# The PostgreSQL describer's nullability noise, part E - design

Date: 2026-10-23

## Why

[TD-0051](../../tech-debt/TD-0051-columns-that-cannot-be-null-are-answered-nullable.md) is the one Medium item of the tech debt: after parts A to D ([the noise design](2026-10-15-postgresql-nullability-noise-design.md) and [part D](2026-10-20-postgresql-nullability-noise-part-d-design.md)), the describer still answers `true` or `null` for 42 columns of the realistic corpus on PostgreSQL 14 and 40 on 18 that can never be `NULL` (`RealisticNullabilityNoiseTests.Budgets`).  For a user each is a `?` on a property and a `!` they add by hand.

The largest kind left is `count(x)` and the forms whose signature in `pg_catalog` is polymorphic: `count("any")`, `array_agg(anynonarray)`, `jsonb_build_object(VARIADIC "any")` and the range intersection `*(anyrange, anyrange)`.  Part D left them because a function or an operator of another schema, whose argument types are the call's exactly, beats a polymorphic one whatever the order of the search path and prints bare, so the plan's text cannot tell the catalog's from a user's.  `count(x)` and an aggregate in a `WITH` query are among the commonest forms of SQL.  This part takes that kind up with one new server call.  It also takes the two other kinds that are cheap and safe: a `COALESCE` over both sides of a `FULL JOIN`, and the type of a `COALESCE`, `GREATEST`, `LEAST` or `CASE`.

The epic's rule holds without exception: a false nullable is a nuisance, a false non-nullable a silent wrong value.  No column the truth says can be `NULL` is ever answered `false`.  Every answer below is proved from the catalog and the source of PostgreSQL 14 and 18 and pinned by a container test on both.  Anything the code does not recognise keeps the answer it has today.

## What the plan and the catalog show

These facts come from `EXPLAIN (VERBOSE, FORMAT JSON)` of `EXECUTE` under `force_generic_plan`, on the realistic schema, and from the catalogs of `postgres:14.24` and `postgres:18.6`, on 2026-10-23.  The two majors give the same text unless a line says otherwise.

1. **A polymorphic call prints bare, with its arguments as they are**: `count(o.id)`, `count(DISTINCT o.id)`, `count(email) FILTER (WHERE (id > 1))`, `count(email) OVER (?)` on 14 and `OVER w1` on 18, `array_agg(DISTINCT p.id ORDER BY p.id)`, `jsonb_build_object('id', a.id, 'name', a.name, 'email', a.email)`, `(during * tstzrange($1, $2))`.  A call written with `VARIADIC` prints it, `jsonb_build_object(VARIADIC $1)`.  As for any call, the plan prints a schema only when the search path would not find the same function under the bare name.
2. **`pg_catalog`'s overloads of these names**, on both majors, are exactly these:

   | Name | Overloads in `pg_catalog`: `prosrc`, or `aggtransfn` / `aggfinalfn` / `agginitval` |
   |----|----|
   | `count` | `count()`: `int8inc` / none / `0`; `count("any")`: `int8inc_any` / none / `0` |
   | `array_agg` | `array_agg(anynonarray)`: `array_agg_transfn` / `array_agg_finalfn`; `array_agg(anyarray)`: `array_agg_array_transfn` / `array_agg_array_finalfn` |
   | `jsonb_build_object` | `jsonb_build_object(VARIADIC "any")`: `jsonb_build_object`; `jsonb_build_object()`: `jsonb_build_object_noargs` |
   | `json_build_object` | `json_build_object(VARIADIC "any")`: `json_build_object`; `json_build_object()`: `json_build_object_noargs` |
   | `int4range`, `int8range`, `numrange`, `tsrange`, `tstzrange`, `daterange` | two of their bound type: `range_constructor2`; with a third `text`: `range_constructor3` |

   Of the operators `*`, the only one that takes two ranges is `*(anyrange, anyrange)`, `range_intersect`.  `*(anymultirange, anymultirange)` would need a cast from a range to a multirange, and `pg_cast` holds those casts as explicit only (`castcontext` `e`).
3. **What each overload returns**, from its C function in the source of 14 and 18, and checked on both servers (`jsonb_build_object('a', NULL)` is `{"a": null}`, `jsonb_build_object(VARIADIC NULL::text[])` is `NULL`, `tstzrange(NULL, NULL)` is `(,)`, and `count(x)` of no row is `0`):
   - `count` starts from `0`, a strict transition skips a `NULL` input, and it has no final function.  So it is a `bigint` that is never `NULL`: over no row and in an empty window frame it is `0`, and with a `FILTER` too.
   - `array_agg`'s transition is not strict and makes its state at the first row, a `NULL` input included.  `array_agg_array_transfn` raises an error for a `NULL` or an empty array.  The final function is `NULL` only for a state that saw no row.  So over a group that has a row it is not `NULL`, whatever its inputs, with a `DISTINCT` or an `ORDER BY` too.
   - `jsonb_build_object` and `json_build_object` return `NULL` only when called with `VARIADIC` and a `NULL` array (`extract_variadic_args` gives `-1`).  A `NULL` key raises an error, and so does an odd number of arguments.  The forms with no arguments return `{}`.
   - `range_constructor2` reads a `NULL` bound as infinite.  `range_constructor3` raises an error for a `NULL` third argument.  Both raise an error for a lower bound above the upper, and never return `NULL`.
   - `range_intersect` is strict, and returns the empty range for two ranges that do not overlap.
4. **`COALESCE`, `GREATEST`, `LEAST` and a `CASE`'s results print with their implicit casts to the common type**: `COALESCE(p.price, '0'::numeric)`, `max(COALESCE(o.id, (l.product_id)::bigint))`, `CASE WHEN (l.quantity > 1) THEN '1'::numeric ELSE 2.5 END`, `CASE ... THEN (l.quantity)::bigint ELSE o.id END`, and `CASE ... THEN p.name ELSE 'x'::text END`.
5. **A `FULL JOIN`'s `COALESCE` is computed at the join**: `Hash Join` `Full` prints `COALESCE(accounts.id, r.account_id)` in its own `Output`, and the `Sort` above it prints `(COALESCE(accounts.id, r.account_id))`.  A `USING` or a `NATURAL` full join prints the same for its merged column (`walk-using`, `walk-natural`).
6. **`WITH ORDINALITY` prints nothing of itself.**  A `Function Scan` prints `"Function Call": "unnest(a.tags)"` and an `Output` of the columns the query above needs, by the names the alias gives (`t.tag`, `t.ordinal`).  Nothing says which column, if any, is the ordinal.

## Goals

- These kinds answer `false`, with `nullableSource` `expression`, where the walk proves them, on 14 and 18:
  1. **`count(x)`**, with or without `DISTINCT`, `FILTER` and `OVER`.
  2. **`array_agg(...)` over a group that has a row**, without `FILTER`, `OVER` or `WITHIN GROUP`.
  3. **`jsonb_build_object(...)` and `json_build_object(...)`** called without `VARIADIC`.
  4. **A constructor of a built-in range type**, and **the intersection `*` of two values of one built-in range type** that are not `NULL`.
  5. **`COALESCE`, `GREATEST` or `LEAST` computed at a `FULL JOIN`** over a column on each of its sides that is not `NULL` but for that join.
- **`COALESCE`, `GREATEST`, `LEAST` and `CASE` get a type** where they are not `NULL`, so arithmetic, a comparison, a cast, an aggregate and a scalar subquery's value over them are read: `sum(coalesce(x, 0))` under `GROUP BY`.
- The noise budget lowered on both majors, by the corpus as it stands, from 42 to 32 on 14 and from 40 to 30 on 18 (below).
- TD-0051 rewritten to describe only what remains.  `docs/architecture/postgres-nullability.md` and `postgres-describer.md` describe what was built, and the manual's nullability section says what a user now gets.

## Non-goals

- **`WITH ORDINALITY`** (fact 6).  The ordinal could be told only from the SQL's column list or from the server's names for the scan's columns, which no call asks for.
- **Any other polymorphic function or operator**: `upper(anyrange)` and `lower(anyrange)` return `NULL` for an infinite or an empty range, `json_agg` and `jsonb_agg` are left until a case needs them, and a multirange's operators likewise.
- **A name whose `pg_catalog` overloads are not all in the table.**  A name is read only when every overload `pg_catalog` holds under it is one the table pins (fact 2).
- **A session whose search path puts another schema before `pg_catalog`.**  Every form here is read only when the session finds `pg_catalog` first, as part D's are, though a name no other schema holds would not need it.  That costs nothing in the corpus and keeps one gate for every catalog form.
- **A `FULL JOIN`'s `COALESCE` computed above the join**, or over a cast of a side's column (`COALESCE(o.id, (a.id)::bigint)`), or with a join under either side that null-extends.
- The kinds that stay in TD-0051 as they are: `EXISTS` on 14, `InitPlan`s, windows and `FILTER` of other aggregates, `VALUES`, columns through a `Subquery Scan` or a `CTE Scan`, recursive CTEs, `*SELECT* <k>` branches, grouping sets, views, written values of `RETURNING`, and a partitioned `ModifyTable` target.
- Any change to the sidecar format: every answer here is `expression`.

## Anti-goals

These would make the work a failure even if every test passes:

- **A wrong `false`.**  The safety tests (`RealisticNullabilitySafetyTests`) and the oracle hold every column the truth says can be `NULL` on 14 and 18.
- **A name read when another schema holds one of it**, when the call failed, or when the call was not asked.  An unknown answer is "held elsewhere".
- **An overload of a listed name that the table does not pin.**  The container test fails when `pg_catalog` holds an overload the table does not list, or lacks one it lists.
- **A change to a capture other than an added `overloads`**, and only for a case whose plan prints a name of the table.  No fixture's plan does today (`count(*)` asks nothing), so only the new cases have one.
- **A change to an answer that is not a lowering of noise.**  Every diff of a recorded expectation turns a `true` or `null` into `false` with `expression`.

## Decisions

### 1. The call `overloads`

A new call, `overloads`, asks which of a list of names some schema other than `pg_catalog` holds a function of (`pg_proc.proname`, every `prokind`) or an operator of (`pg_operator.oprname`), or `pg_catalog` holds as an object that is not built in: one whose OID is `16384` or more, `FirstNormalObjectId`, which every object `initdb` creates is below.  A superuser, or an extension made in `pg_catalog`, can add such an overload, and the table pins only the stock ones.

- Request: `PostgresOverloadsRequest(IReadOnlyList<string> Functions, IReadOnlyList<string> Operators)`, each sorted and without duplicates.
- Answer: `PostgresOverloadsAnswer(IReadOnlyList<string>? Functions, IReadOnlyList<string>? Operators, PostgresServerError? Error)`: the names of the request that another schema holds, or `pg_catalog` holds as an object that is not built in.  An empty list means `pg_catalog` alone holds every name of it, and only its built-in overloads.

It is asked after `relations`, and only when all of these hold:
- a column has no origin;
- the session finds `pg_catalog` first;
- the plan prints a name of the table of decision 2: a function's name followed by its parenthesis, `count` only when the parenthesis holds more than `*`, or an operator of the table between two operands.

It is found by one scan of the tokens of every `Output` entry of the plan.  A name the scan finds and the reader does not need costs a name in the request, and is never wrong.  Of the 65 fixture cases, none prints such a name today.

Every schema counts, `pg_temp` and the schemas the path does not name included, whether or not the session can see them: that costs an answer and is never wrong.  An error of the call is logged and never reported, and every name is then "held elsewhere".  Another session may create or drop a function between `explain` and `overloads`.  That is the server's change, as a table dropped between `columns` and `tables` is.

Without the call, the text cannot tell the catalog's call from a user's, and with it a bare name that only `pg_catalog` holds is the catalog's whatever the argument types and the order of the path.  The call is the item's proposed fix.  The alternative, a list of fixed names asked once in `open`, would change the answer of every capture and ask for names no query prints.

### 2. Forms of `pg_catalog` by their name

A new closed table, beside `TotalFunctions`, holds the names whose every overload in `pg_catalog` is pinned (fact 2), each with how it is read.  A call of such a name is read only when the session finds `pg_catalog` first and `overloads` answered that no other schema holds the name:

| Name | Read as | Not `NULL` when | Type |
|----|----|----|----|
| `count` | `count(...)` with any argument, with an optional `FILTER (...)` and an optional `OVER` | always | `bigint` |
| `array_agg` | an aggregate with nothing after its parenthesis | the node that computes it is a grouped `Aggregate` (`GroupsHaveRows`), as part D's aggregates are | none known |
| `jsonb_build_object`, `json_build_object` | a call whose first argument is not `VARIADIC` | always | `jsonb`, `json` |
| `int4range`, `int8range`, `numrange`, `tsrange`, `tstzrange`, `daterange` | a call of two or three arguments | always | that range type |
| `*` | `(A * B)` with both operands proved of one of those range types | `A` and `B` are proved | that range type |

The arguments of the rows that are "always" are read for their balance only, since none can make the call `NULL`.  `count(*)` and the rankings stay as they are, read without the call, since `pg_catalog` holds their exact signature first.  The table holds the implementation of each overload (fact 2), and `Catalog_CatalogOverloads_ArePinned` holds it to the catalog of 14 and 18:

- `pg_catalog` holds exactly those overloads of each name;
- each is implemented as the table says;
- `count`'s initial value is `0`;
- for `*`, `pg_typeof(r * r)` is the range type for each of the six, and `pg_cast` holds no implicit or assignment cast from one.

A row goes in this table only with that test, and only for a name whose every overload is never `NULL` under its row's condition.

### 3. The type of `COALESCE`, `GREATEST`, `LEAST` and `CASE`

A `COALESCE`, a `GREATEST` or a `LEAST` that part A proves not `NULL` is of type `T` when one of its arguments is proved of `T`, and every argument proved of a known type is of `T`.  A `CASE` whose every result part A proves not `NULL` gets its type the same way, from its results.  The parser casts every argument and every result to the common type, and the plan prints those casts (fact 4), so a proved argument's type is the form's.  A form with no argument of a known type stays not `NULL` of no known type, as today.  A domain's column is proved of the domain, which no row of the tables takes, so a form over one feeds nothing.

### 4. `COALESCE`, `GREATEST` and `LEAST` at a `FULL JOIN`

Each row of a full join has a row of one side at least.  So a `COALESCE`, a `GREATEST` or a `LEAST` is not `NULL` when all of these hold:
- it is computed at a node whose join type is `Full`, read where the walk follows the entry (`ComputedAt`), and no join above extends that node;
- among its arguments is a plain reference to an instance under the join's `Outer` child and one to an instance under its `Inner` child;
- each reference would be proved by `ProvedTypeOf` but for the extension of that one join;
- no join under that child null-extends (`PlanInstances.ExtendsUnder`).

`GREATEST` and `LEAST` ignore a `NULL` argument and are `NULL` only when every argument is.  The form is of the references' type when both are of one type (decision 3), and of no known type otherwise.  Both 14 and 18 compute it at the join, the hash and the merge joins alike (fact 5).  A form above the join, or with a cast of a side's column, is not read.

## The corpus after this part

| Case | Column | 14 | 18 | Decision |
|----|----|----|----|----|
| `any-ids-and-enum-array` | `order_count` | 1 | 1 | 2 |
| `category-parent-self-join` | `product_count` | 1 | 1 | 2 |
| `count-filter-statuses` | `with_email` | 1 | 1 | 2 |
| `group-by-having` | `order_count` | 1 | 1 | 2 |
| `json-build-and-agg` | `order_count`, `account` | 2 | 2 | 2 |
| `array-and-string-agg` | `product_ids` | 1 | 1 | 2 |
| `reservations-in-range` | `overlap` | 1 | 1 | 2 |
| `recursive-category-tree` | `product_count` | 1 | 1 | 2 |
| `full-join-accounts-reservations` | `account_id` | 1 | 1 | 4 |
| | | 10 | 10 | |

The budgets become 32 and 30.  The build found `recursive-category-tree` `product_count` as well: it is a `count(p.id)` at the outer grouped query, outside the CTE scan, so the walk reads it.  Decision 3 lowers no column of the corpus: its one case, `sum(coalesce(...))` in `cte-chain` `lifetime`, is behind a `CTE Scan`.  It is taken because it is small and `sum(coalesce(x, 0))` is common.  The fixture cases `walk-using` and `walk-natural` change by decision 4: their merged columns become `Expression` in `WalkMatrixTests`, by hand.  If the build finds a column of the table unproved, the column stays, and the budget is set to what the corpus gives, with the reason in the plan.

## What a user sees

- The manual's nullability list ([which columns can be null](../../manual/postgresql.md#nullability)) gains `count(x)`, `array_agg` under `GROUP BY`, `jsonb_build_object` and `json_build_object`, a range's constructor and intersection, a `COALESCE` over both sides of a `FULL JOIN`, and arithmetic or an aggregate over a `COALESCE`.  Its list of what stays possibly null, and its known issue, lose them.
- The manual, the examples and the end-to-end queries use `count(note)` to show a column's `!` mark (`docs/manual/postgresql.md`, `generator.md`, `recipes.md`, `examples/PostgresExamples/Models/Orders.sql`, `tests/SqlSource.Tests.Postgres/EndToEnd/Queries/Notes.sql`).  Each moves to an expression this part still does not prove, as `Notes.sql`'s `initcap(name)` already is, and their sidecars are recorded again.

## Where the code differs from the earlier documents

- `PlanExpression` is split since TD-0073: the forms of `pg_catalog` are in `PlanExpression.Forms.cs`, the forms of nullability in `PlanExpression.Nullability.cs`, and the proofs of types in `PlanExpression.Proofs.cs`.  The new forms go in those parts, not in `PlanExpression.cs`.
- The item says that where no other schema holds a name, a bare name is `pg_catalog`'s whatever the argument types.  That is so, but it is not enough: not every overload of a name is total (`upper(anyrange)` is `NULL` for an infinite bound).  So the table is by name, with every overload pinned.
- The item proposes a reading of a `Function Scan` that knows which column is the ordinal.  The plan prints nothing that tells it (fact 6), so `WITH ORDINALITY` stays.
- The architecture page says that `COALESCE`, `GREATEST`, `LEAST` and `CASE` are of no known type, and the code gives them none.  Decision 3 changes both.

## Shape of the work

One pull request with the design, the plan, the code and the recorded files.  The recorded files are the sidecars of the realistic cases whose answer changes (8), the end-to-end project's and the examples' sidecars where a query changes, the fixture cases `walk-using` and `walk-natural`, and the new fixture cases that hold an `overloads` call.  The estimate is under 60 files.  Each kind is its own commit, which lowers the budgets of `RealisticNullabilityNoiseTests` and records its sidecars on 18, the default image, and runs the suite on 14.

## Success

- `RealisticNullabilityNoiseTests` passes with the lowered budgets, and `RealisticNullabilitySafetyTests` and the oracle pass on 14 and 18.
- Every diff of a recorded file is one the anti-goals allow.
- TD-0051 holds what remains, and `./pre-commit-validation.sh` passes, with the PostgreSQL project run on `postgres:14.24` too.

## Decisions taken

| Decision | Whose |
|----|----|
| The kinds in (count, polymorphic forms, `FULL JOIN`'s `COALESCE`, the type of `COALESCE`) and out (`WITH ORDINALITY`) | The owner's, 2026-10-23: "Approve" |
| A new call, `overloads`, asked per query for the names the plan prints | The owner's, 2026-10-23: "Approve" |
| The samples of a `!` mark moved from `count(note)` to `initcap` | The owner's, 2026-10-23: "Approve" |
| A closed table by name, every overload pinned by a container test | Mine |
| The session must still find `pg_catalog` first | Mine |
| A `FULL JOIN`'s form only where the join computes it, over plain references | Mine |
| One pull request | Mine |

## Left to the builder

- Where the table of decision 2 lives (a new `CatalogOverloads` beside `TotalFunctions`, or rows of `TotalFunctions`), as long as the container test reads it from there.
- How `WalkScope` and `ExpressionContext` carry the answer of `overloads`, and how the walk tells the side of a `Full` join an instance is under.
- The names of the new fixture cases, one for `count(x)` and one for a range intersection.  Each holds the call, and a container test of `WalkTests` holds a schema whose `count(integer)` leaves `count(x)` unknown.
