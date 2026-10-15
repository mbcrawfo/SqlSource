# The PostgreSQL describer's nullability noise - design

Date: 2026-10-15

## Why

A column whose nullability the describer leaves unknown is nullable in generated code (the epic's rule: "a false nullable is a nuisance; a false non-nullable is a silent wrong value").  A throwaway spike on 2026-10-15 described 44 realistic, hand-written queries against PostgreSQL and found no wrong `false`, but about 40 of roughly 130 columns recorded `true` or `null` where the server can never return `NULL`.  For a user that is a model full of `?` properties, each one a `!` or a `"name!"` alias they have to add by hand.  On realistic SQL it is the most visible way the describer disappoints.

The noise has three sources:

1. **Expressions.**  A column that is no plain reference to a table's column has no origin in the row description, and is `null` with `no-origin`: `count(*)`, `row_number()`, `'user' AS kind`, `coalesce(email, 'none')`, `CASE ... ELSE 'gone' END`, `deleted_at IS NULL`.
2. **The walk's gaps.**  [TD-0046](../../tech-debt/TD-0046-the-walk-leaves-some-columns-to-the-heuristic.md) lists the plan shapes the nullability walk cannot map.  On realistic SQL the ones that bite are a subquery the planner keeps apart (the `row_number()` dedupe, `WHERE rn = 1`), a CTE used twice, and a partitioned table: the spike's dedupe query recorded `u.id` and `u.name` nullable, and so did its partitioned table on the side a left join keeps.
3. **Set operations.**  A column of a `UNION`, an `INTERSECT`, an `EXCEPT` or a recursive CTE has no origin, so it is `null` with `no-origin` even where every branch's column is `NOT NULL`.

This design makes the describer answer `false` for each of these where it can prove it, and leaves every other answer as it is.

It depends on [the suite of realistic queries](2026-10-15-postgresql-realistic-query-suite-design.md), which must land first.  That suite's noise budget is how this work is measured, and its safety checks and oracle are what show it never gives a wrong `false`.  The two are built by different people; this document is written for a builder who has not read the conversation that produced it.

## Goals

- A column recorded `false` for each shape in parts A, B and C below that the server can never return `NULL` for, on PostgreSQL 14 and 18, the versions CI runs.  On 15 to 17 a form that prints differently is not recognised and stays as it is; nothing is claimed for them beyond that (my call, after the builder check).
- The suite's noise budget lowered by each part, and every one of its safety checks passing on 14 and 18 after each part.
- TD-0046 closed for what part B covers, and a new tech-debt item for what this design leaves.

## Non-goals

- A parser of SQL.  Every answer here is read from what the server already gives: the row description, the catalog, and the generic plan of `EXPLAIN (VERBOSE, FORMAT JSON)`, as the walk reads it today.  A parser such as libpg_query was weighed and not chosen: it is a native dependency for one platform each, and the plan already holds the planner's view of each output, after the rewrites a parser would have to imitate.
- Strict functions and operators over non-null arguments, `upper(name)`, `a + b`, `name || '!'`.  The deparsed text does not say which overload was chosen, so whether it is strict needs the function's OID, which `EXPLAIN` does not print.  It goes into the new tech-debt item.
- `EXISTS (...)` as a column.  On 14 the planner prints both an `EXISTS` and an `x IN (subquery)` as `(hashed SubPlan 2)`, and on 18 it can print an `EXISTS` as `(ANY (u.id = (hashed SubPlan 2).col1))`.  The text cannot tell them apart, and `IN` can return `NULL`.  It goes into the new tech-debt item.
- Aggregates other than `count`: `sum`, `max`, `array_agg`, `json_agg`, `string_agg` return `NULL` over no rows, and the plan does not say whether a group can be empty or a `FILTER` excludes every row.
- `VALUES`, whose plan does not print its rows.
- The `OLD` and `NEW` rows of PostgreSQL 18's `RETURNING`, and an extension's `Custom Scan`, TD-0046's seventh and fourth bullets, which stay in TD-0046.
- Any change to how the generator reads a sidecar beyond the new values of `nullableSource`, which a reader that does not know them already ignores.

## Anti-goals

These would make the work a failure even if every test passes:

- **A wrong `false`.**  Anything this design's code does not recognise keeps the answer it has today.  A form is trusted only after a test on 14 and on 18 shows the text the server prints for it, and that the text cannot also come from a form that can be `NULL`.
- A recogniser that matches a prefix of an entry.  `count(*) + NULL`, `COALESCE(a, 'x') || NULL` and `(a IS NULL) AND b` must not be read as their first part: an entry is recognised only as a whole, with quotes and parentheses balanced.
- Reading a set operation's root as the first branch.  The root of a `UNION` prints the first branch's references (below), so mapping them as instances would apply the first branch's answer to every branch.
- An unqualified function name in the plan that is read as `pg_catalog`'s when the session's search path finds another one first.
- A change to an answer the suite records that is not a lowering of noise.  Every diff of the suite's expectations in this work turns a `true` or a `null` into a `false`, changes a `nullableSource`, or changes `plan` from `not-needed` to `walked` or `unavailable` for a query that is now planned (below); anything else is a bug.

## What the plan prints

These are from `EXPLAIN (VERBOSE, COSTS OFF)` against the fixture schema, `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/schema.sql`, on 2026-10-15.  The JSON format holds the same text in each node's `Output`.

`SELECT count(*), coalesce(max(email::text), 'x'), 'lit' AS l, NULL::text AS n FROM users`, the same on 14 and 18:

```
Aggregate
  Output: count(*), COALESCE(max((email)::text), 'x'::text), 'lit'::text, NULL::text
```

`SELECT u.id, EXISTS (SELECT 1 FROM orders o WHERE o.user_id = u.id) AS e, row_number() OVER () AS rn, u.deleted_at IS NULL AS live, upper(u.name) AS up, coalesce(u.email, 'none') AS em FROM users u`:

```
18: Output: u.id, (ANY (u.id = (hashed SubPlan 2).col1)), row_number() OVER w1, (u.deleted_at IS NULL), upper((u.name)::text), COALESCE((u.email)::character varying, 'none'::character varying)
14: Output: u.id, (hashed SubPlan 2), row_number() OVER (?), (u.deleted_at IS NULL), upper((u.name)::text), COALESCE((u.email)::character varying, 'none'::character varying)
```

`SELECT id, name FROM users UNION SELECT user_id, note FROM orders`, the same on 14 and 18.  The root prints the first branch's names, and `orders.note` is nullable:

```
HashAggregate
  Output: users.id, users.name
  ->  Append
        ->  Seq Scan on public.users
              Output: users.id, users.name
        ->  Subquery Scan on "*SELECT* 2"
              Output: "*SELECT* 2".user_id, "*SELECT* 2".note
              ->  Seq Scan on public.orders
                    Output: orders.user_id, orders.note
```

The row-number dedupe, `SELECT s.id, s.name FROM (SELECT u.id, u.name, row_number() OVER (PARTITION BY u.id) rn FROM users u LEFT JOIN orders o ON o.user_id = u.id) s WHERE rn = 1`, on 18:

```
Subquery Scan on s
  Output: s.id, s.name
  ->  WindowAgg
        Output: u.id, u.name, row_number() OVER w1
        ->  Sort
              ->  Hash Right Join
                    Output: u.id, u.name
                    ->  Seq Scan on public.orders o
                    ->  Hash
                          ->  Seq Scan on public.users u
```

Planning folds an `IMMUTABLE` function over constants into its value, so `upper('x')` prints as `'X'::text`, and a strict one over a `NULL` constant prints as `NULL::type`.  The recogniser reads the folded value as the constant it is.

## When a plan is asked for

Today the walk runs for a gated query alone ([`OuterJoinGate`](../../src/SqlSource.Tool/Describing/Postgres/OuterJoinGate.cs)).  After this work a plan is also asked for when any column has no origin, since parts A and C answer such columns from it.  A query whose every column has an origin and which is not gated still gets `not-needed`.  A query planned for this reason records `plan` as a gated one does, `walked` or `unavailable`; no new value is added, and the sidecar format design's description of `not-needed` is reworded to "no column the plan could answer" (my call, after the builder check).  A plan that cannot be used, or an `EXPLAIN` that fails with an error of the query, leaves every column as it is today, as now.  An `EXPLAIN` that fails because the session lost its prepared statement, `26000`, stays the describe failure SQLSRC236 that it is for a gated query today: it means the session is not kept, which no answer can survive, and this design makes it reachable for more queries.

The same widening applies to [TD-0049](../../tech-debt/TD-0049-describe-behind-a-pooler-can-reach-another-backend.md): behind a pooler in transaction mode, which the README already says is not supported, `EXPLAIN` can return the plan of another backend's statement of the same name, and part A would read a `count(*)` of that plan as this query's.  Before any part trusts a plan for an expression, the builder adds a cheap check that the root's `Output` fits the described columns: as many entries, and each entry that is a plain reference names a column of the same name as the described one's origin.  A plan that fails it is `unavailable` with a new reason.  TD-0049 is updated to say what the check catches and what it does not (my call, after the builder check).

This widens [TD-0047](../../tech-debt/TD-0047-explain-evaluates-immutable-functions.md): planning runs a function that is wrongly marked `IMMUTABLE`, and after this work most queries with an expression are planned, not only gated ones.  TD-0047's text is updated to say so in the first part's change.

## Part A: expressions

A column with no origin, `null` with `no-origin`, is given an answer from its entry of the root's `Output` when the entry, as a whole, is one of these forms, and only in a plan where no join null-extends anything.

That condition is what keeps part A safe.  PostgreSQL prints a value computed below a join by its expression and not by a name: a constant or a `COALESCE(...)` of a subquery the planner pulled up from the nullable side of a `LEFT JOIN`, or the `(count(*))` of an aggregate subquery under a `LEFT JOIN LATERAL` whose `Subquery Scan` the planner removed, reaches the root as the same text and is `NULL` for a row the join did not match.  The text cannot tell such a value from one the root computes.  So part A answers in a query that has no outer join, and in a gated one whose every join the plan shows as `Inner`, `Semi` or `Right Semi`, and leaves every expression of any other plan as it is (my call, after the builder check).  Part B lifts the condition where it can: once the walk can follow a value down to the node that computes it, an expression computed on a side that no join extends is answered as in a plan with no such join.  That step is part of part B, after its subquery and CTE scans, and is pinned by cases of both shapes above.  Each form is pinned by a container test on 14 and 18 and by unit tests over the text, including the near misses of the anti-goals.

| Form, as the plan prints it | Answer |
|----|----|
| `count(...)`, with or without ` OVER ...` | `false` |
| `row_number()`, `rank()`, `dense_rank()`, `percent_rank()`, `cume_dist()`, each with ` OVER ...`; the window prints as `(?)` on 14 and as a name on 18 | `false` |
| A constant: a quoted literal with a cast, `'lit'::text`; a number; `true`; `false` | `false` |
| `NULL::type` | `true` |
| `(x IS NULL)`, `(x IS NOT NULL)`, `(x IS DISTINCT FROM y)`, `(x IS NOT DISTINCT FROM y)` | `false` |
| `COALESCE(a, ..., z)`, `GREATEST(...)`, `LEAST(...)` | `false` when any argument is `false` by this table or is a reference the walk answers `false`; otherwise unchanged |
| `CASE ... END` with an `ELSE` | `false` when every `THEN` result and the `ELSE` result is `false` by this table or is a reference the walk answers `false`; otherwise unchanged |
| A cast, `(x)::type` | the answer of `x`, where the builder shows that the cast cannot return `NULL` for a value that is not `NULL`; otherwise unchanged |

The new answers have `nullableSource: "expression"`, a new value of the sidecar format.  It is additive and `formatVersion` stays 1, as `foreign-table` was added in TD-0044.  The sidecar format design, [`2026-10-07-sidecar-format-design.md`](2026-10-07-sidecar-format-design.md), the epic outline's list of values in [`2026-10-07-query-generation-epic-design.md`](2026-10-07-query-generation-epic-design.md), which is kept current until its epic closes, and `SidecarValues.NullableSource` gain it.

An argument that is a reference is answered by the walk as it answers a column: an alias of an instance that no join extends, and a `NOT NULL` origin.  A reference inside an expression has no origin in the row description, so the builder resolves its column from the instance's relation and the column's name, with one catalog lookup, or leaves it unchanged.  Which is the builder's call; `COALESCE(t.spent, 0)`, with a constant last, needs no lookup at all.

The recogniser reads the text with a small tokeniser that knows the plan's quoting: `'...'` with `''`, `"..."` with `""`, and parentheses.  It is no parser of SQL, and anything it does not recognise is unknown.

A function name is read as `pg_catalog`'s only when the plan prints it unqualified and the describer's session puts `pg_catalog` first on its search path, which PostgreSQL does unless the path names it later.  The builder checks how the session sets its search path ([`PostgresSession`](../../src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs), and [TD-0050](../../tech-debt/TD-0050-a-catalog-types-name-depends-on-the-search-path.md) for a related trap) and leaves every function unrecognised when it cannot be sure.

## Part B: the walk's gaps

Part B is TD-0046's own proposed fix for its first, second, third, fifth and sixth bullets.  The item says what each needs; this design adds the order and the rule that matters most.

1. **A `Subquery Scan` or a `CTE Scan`.**  The scan's `n`th output maps to the `n`th of its child: the `Subquery` child of a `Subquery Scan`, and for a `CTE Scan` the `InitPlan` whose `Subplan Name` is `CTE <name>`.  The child is walked with the joins above the scan added to its path.  A CTE used twice has one `InitPlan` and two scans, each with its own path.  The order is trusted only where a test establishes it for that shape, on 14 and 18; elsewhere the column stays unknown.  Matching by name is never done: `SELECT o.note AS total, o.total AS note` would be read crossed.
2. **A partitioned table.**  An `Append` or `MergeAppend` whose every `Member` scans a relation that `pg_inherits` says is a partition of one parent is one instance of that parent, under the members' alias with its `_n` cut.  That needs one more catalog call.  It must not apply to the `Append` of a set operation, whose members are other relations or subquery scans; the `pg_inherits` test is what tells them apart.
3. **A `Foreign Scan`** joins the relation scans, with a container test against `postgres_fdw`.  Its columns stay `null` with `foreign-table` unless a join extends them.
4. **A root that is not the scan.**  A bare name resolves when the root reaches exactly one relation scan through single `Outer` children, with no join and no other instance.
5. **A bare name at a `ModifyTable` with a join under it** keeps the heuristic only when a join under it null-extends.

TD-0046 is updated to hold what remains, the bullets of an extension's `Custom Scan` and of `OLD` and `NEW`, and its row in `docs/tech-debt/README.md` follows.  The item's second "not whole" bullet, a plan asked for when it cannot change anything, changes meaning with this design's rule for when a plan is asked for, and is rewritten or dropped to match.

## Part C: set operations

The root of a set operation prints the first branch's references (above), and today the walk never maps them, because a column of a set operation has no origin and a `Member`'s scans are not instances.  Both stay true.

A column of a set operation is answered from its branches instead.  Each member of the `Append`, `MergeAppend`, `SetOp` or `HashSetOp` under the root is walked as a plan of its own, with its own instances and joins.  Its `n`th output gets an answer by the walk's rule, by part A for an expression, or by part B for a scan of a subquery; a reference to a table's column inside a member is answered by its alias and, since it has no origin in the row description, by the same catalog lookup that part A's arguments need.  Then:

| Operation | Column `n` is `false` when |
|----|----|
| `UNION`, `UNION ALL` | every member's output `n` is `false` |
| `INTERSECT`, `INTERSECT ALL` | any member's output `n` is `false` |
| `EXCEPT`, `EXCEPT ALL` | the first member's output `n` is `false` |

Otherwise the column stays unknown.  The answers have `nullableSource: "set-operation"`, another new value, with the same changes as part A's.  How each node and its members print, the flag column a `SetOp`'s members add, and whether the members' outputs are in the order of the result are established by tests on 14 and 18 before anything trusts them.

A recursive CTE, a `Recursive Union` over its two terms with a `WorkTable Scan` in the second, is answered by induction: column `n` is `false` when it is `false` in the first term, and in the second term when the `WorkTable Scan`'s column `n` is taken as `false`.  This is the last step of part C and may be split off into the new tech-debt item if it proves large; the builder says so in the plan.

## Shape of the work

Three pull requests, in the order A, B, C, each with its own plan, after the suite has landed.  A part's plan is written against the suite as it landed, its folder, its files and its budget test, and not against the suite's design (my call, after the builder check).  Each one:

- lowers the suite's noise budget, on 14 and on 18, and updates the expectations whose noise it removes, each diff a `true` or `null` turning into `false` or a changed `nullableSource`;
- passes every safety check of the suite on 14 and 18;
- re-records the fixture cases with `tools/record-fixtures.sh` where an answer of theirs changes;
- updates `docs/tech-debt/` for what it closes and what it finds.

The new tech-debt item, opened by part A and added to by B and C, lists what stays noise: strict functions and operators, `EXISTS` as a column, aggregates other than `count`, `VALUES`, and anything a part leaves unproved.  It names the spike's queries that show each.

## Success

- The suite's noise budget is lower after each part, and every remaining noise column of the corpus is of a kind the new tech-debt item names.
- No safety check of the suite fails, on 14 or 18, after any part.
- TD-0046 holds only its bullets of `Custom Scan` and of `OLD` and `NEW`.
- `./pre-commit-validation.sh` passes.

## Decisions

| Decision | Whose |
|----|----|
| Read answers from the plan's text, no parser of SQL | Mine |
| Plan a query when any column has no origin, widening TD-0047 | Mine |
| Parts A, B and C in scope; strict functions, `EXISTS`, other aggregates and `VALUES` left as tech debt | Mine |
| New `nullableSource` values `expression` and `set-operation` | Mine |
| Three pull requests, A then B then C, each planned after the suite has landed | Mine |
| Part A only in a plan where no join null-extends; part B lifts that where it can trace a value to where it is computed | Mine, after the builder check |
| `plan` of a query planned for an expression is `walked` or `unavailable`, no new value | Mine, after the builder check |
| A check that the plan's `Output` fits the described columns, against TD-0049's stale plan | Mine, after the builder check |
| The goal claims 14 and 18, the versions CI runs | Mine, after the builder check |
| The recursive CTE as the last step of C, which may be split off | Mine |

## Left to the builder

- The recogniser's and the tokeniser's shape, and where they live under `src/SqlSource.Tool/Describing/Postgres/`.
- How a reference inside an expression or a member is resolved to its column's `NOT NULL`, or whether it is left unknown.
- The rule for casts in part A.
- How the walk's code is split to walk a member, a subquery's child or a CTE's `InitPlan` as a plan of its own.
