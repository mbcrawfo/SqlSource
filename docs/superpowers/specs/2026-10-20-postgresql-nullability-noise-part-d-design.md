# The PostgreSQL describer's nullability noise, part D - design

Date: 2026-10-20

## Why

[TD-0051](../../tech-debt/TD-0051-columns-that-cannot-be-null-are-answered-nullable.md) is the one Medium item of the tech debt: after parts A, B and C of [the noise design](2026-10-15-postgresql-nullability-noise-design.md), the describer still answers `true` or `null` for 53 columns of the realistic corpus on PostgreSQL 14 and 54 on 18 that can never be `NULL` (`RealisticNullabilityNoiseTests.Budgets`).  For a user each is a `?` on a property and a `!` they add by hand.

That design left four kinds to TD-0051 because the plan's text seemed not to settle them: strict functions and operators, `EXISTS` as a column, aggregates other than `count`, and `VALUES`.  This part takes up the ones the describer can prove from what it already reads, the plan's `Output`, the catalog rows of `tables` and `relations`, and the row description, and leaves the rest in TD-0051 with what each would need.

The epic's rule holds without exception: a false nullable is a nuisance, a false non-nullable a silent wrong value.  Every answer below is proved from the source of PostgreSQL 14 and 18 and pinned by a container test on both; anything the code does not recognise keeps the answer it has today.

## What the plan prints

From `EXPLAIN (VERBOSE, FORMAT JSON)` of `EXECUTE` under `force_generic_plan`, on the realistic schema, 2026-10-20.  The text is the same on 14 and 18 unless the line says otherwise.

1. **A function's or an operator's arguments print with their implicit casts.**  `ruleutils.c` deparses an argument with `showimplicit` true (`get_oper_expr`, `get_func_expr`, `get_agg_expr`), so `l.quantity * l.unit_price` prints as `((l.quantity)::numeric * l.unit_price)`, `max(a.name)` of a `varchar` as `max((name)::text)`, and `a.id::bigint * 2` as `((id)::bigint * 2)`, the `bigint * integer` operator.  The type of every argument is therefore what the text shows: a reference's column type, a cast's target, a literal's cast or a bare number's `integer` or `numeric`.
2. **A function or an operator prints bare only when the session would resolve the same one from its name and those argument types** (`generate_function_name`, `generate_operator_name`), and `OPERATOR(schema.op)` or `schema.f(...)` otherwise.
3. **An aggregate under `GROUP BY` is computed at an `Aggregate` node with a `Group Key`**: `"Group Key": ["a.id"]`, `Strategy` `Sorted` or `Hashed`.  A node above prints it in parentheses, `(max(o.placed_at))`.  An aggregate with no `GROUP BY` is computed at an `Aggregate` whose `Strategy` is `Plain` and that has no `Group Key`; one with grouping sets has a `Grouping Sets` key.  `HAVING` is the node's `Filter`.
4. **A subquery in an expression is a `SubPlan`**, a child of the node that evaluates it with `Parent Relationship` `SubPlan` and `Subplan Name` `SubPlan <n>`, unique in the plan.  On 14 every kind prints `(SubPlan <n>)` or `(hashed SubPlan <n>)`: a scalar subquery, `EXISTS`, `IN`, `ANY`, `ALL` and `ARRAY` alike.  On 18 each kind prints its own way (`ruleutils.c`, `T_SubPlan`): a scalar subquery `(SubPlan <n>)`, `EXISTS(SubPlan <n>)`, `ARRAY(SubPlan <n>)`, and `IN` or `ANY` as `(ANY (<left> <op> (SubPlan <n>).col<k>))` or with `hashed`.  The planner turns a correlated `EXISTS` into a hashed `ANY` where it can, so `exists-as-column` prints `(ANY (a.id = (hashed SubPlan 2).col1))` on 18 and `(hashed SubPlan 2)` on 14.
5. **`merge_action()` prints `MERGE_ACTION()`** on 18 (17 and later have it), in upper case: it is the grammar's `MergeSupportFunc`, not a function, and a user's function of that name would print quoted.
6. **A system column prints as a column**, `(products.xmax = 0)`, the operator `=(xid, integer)`.  `tables` reads only `attnum > 0`, so the walk has no row for it.
7. **`VALUES` prints no rows**: a `Values Scan` on `"*VALUES*"` prints `"*VALUES*".column1` and nothing of what the rows hold.

## Goals

- These kinds answer `false`, with `nullableSource` `expression`, where the walk proves it, on 14 and 18:
  1. **A total operator, function or cast over values that are not `NULL`.**
  2. **An aggregate other than `count` over a group that has a row.**
  3. **A scalar subquery whose plan always returns one row that is not `NULL`.**
  4. **A system column of a table.**
  5. **On 18, `EXISTS(SubPlan <n>)`, an `ANY` whose both sides are not `NULL`, and `MERGE_ACTION()`.**
- A reference inside an expression is proved through any relation the plan scans, not only one that a plain column names: `relations` is asked for them.
- The noise budget lowered on both majors; by the corpus as it stands, from 53 to 44 on 14 and from 54 to 42 on 18 (below).
- TD-0051 narrowed to what remains, `docs/architecture/postgres-nullability.md` describing what was built, and the manual's nullability section saying what a user now gets.

## Non-goals

- **`count(x)` and any form whose function's signature in `pg_catalog` is polymorphic** (`anyelement`, `anyarray`, `anyrange`, `"any"`): `array_agg`, `json_agg`, `jsonb_build_object`, the range intersection `*`, `upper(anyrange)`.  A function of another schema whose signature is the argument's exact type beats a polymorphic one whatever the order of the path, and prints bare (fact 2).  Telling them apart needs to know that no other schema holds a function of that name, a new server call; it stays in TD-0051 with that as its proposed fix.
- **`EXISTS` on 14**, whose text cannot tell it from `IN` (fact 4), and an uncorrelated `EXISTS` or scalar subquery, an `InitPlan` that prints `$<n>` on 14 and `(InitPlan <n>).col1` on 18.
- **A window aggregate** (`sum(x) OVER w`): its frame may be empty, and the plan prints the window by name.
- **An aggregate with `FILTER`**, which may exclude every row of a group, and an ordered-set aggregate (`WITHIN GROUP`).
- **`VALUES`** (fact 7).
- **A column of no origin through a `Subquery Scan` or a `CTE Scan`**, a recursive CTE, a column of a view and a `RETURNING` column of a written value, which stay in TD-0051 as they are.
- Any change to the sidecar format: every answer here is an expression's, `expression`.

## Anti-goals

These would make the work a failure even if every test passes:

- **A wrong `false`.**  A form is answered only as a whole span, never as a prefix, and only when every value it is computed from is proved.
- **An operator or a function that is not `pg_catalog`'s.**  Each is read only when the session finds `pg_catalog` first (`PostgresSearchPath`), and only for a signature that `pg_catalog` holds exactly, with the argument types the text shows (decision 2).
- **An aggregate read in a node that did not compute it.**  An aggregate is proved by a `Group Key` only at the node that computes it, never inside a value the node takes from a child.
- **A subquery read as a scalar where it may be `IN`.**  On 14 `(SubPlan <n>)` is any kind; it is read as a scalar subquery only as a whole column whose type is the subplan's value's (decision 5).
- **A change to an answer that is not a lowering of noise.**  Every diff of a recorded expectation turns a `true` or `null` into `false` with `nullableSource` `expression`, and every diff of a capture adds the call `relations` (decision 7).  Anything else is a bug.

## Decisions

### 1. Values are proved with their type

Part A's reader answers whether a span can be `NULL`.  To read an operator it must also know the span's type, since the operator is known only by its argument types (decision 2).  The reader gains a typed proof: a span is a value that is not `NULL`, of a known type, when it is

- a literal with its cast to a built-in type, a number (`integer`, or `numeric` with a point), `true` or `false`; a test (`boolean`); `count(*)` or a ranking (`bigint`, `double precision`), as `PlanExpression.TypeOf` types them today;
- a reference the walk proves, of its column's declared type (`Expressions.ProvedTypeOf`, which a domain's column fails, since no signature below takes a domain);
- one of the forms of decisions 2 to 6, of the type the decision gives.

`COALESCE`, `GREATEST`, `LEAST` and `CASE` stay as part A reads them: not `NULL`, of no known type, so they never feed an operator.  A literal's type, a cast's and a function's are read only when the session finds `pg_catalog` first, as part A's literals are.

### 2. Total operators and functions of `pg_catalog`

An operator `(A op B)` or a function `f(a, ...)` that the plan prints bare is the one the session resolves from its name and the argument types it prints (facts 1 and 2).  When the session finds `pg_catalog` first and `pg_catalog` holds that name with exactly those argument types, the resolution finds it: an operator or a function whose argument types are the call's exactly is chosen before any that needs a coercion or is polymorphic, and of several with the same argument types the one earliest on the path is; a variadic or defaulted function of the same effective argument types loses to it by the same rule ([the manual's type conversion](https://www.postgresql.org/docs/current/typeconv-func.html), steps 2 and 3).  `pg_temp` is never searched for a function or an operator.

Such a call is not `NULL` when its arguments are not and it is **total**: it never returns `NULL` for arguments that are not.  Strictness is no proof (a strict function returns `NULL` for a `NULL` argument, and may for others, `array_upper` of an empty array).  The table is closed, and each row is read from the C function in the source of 14 and 18; every one errors where it cannot compute, and returns a value otherwise:

| Name | Argument types | Result |
|----|----|----|
| `+`, `-`, `*`, `/` | `smallint`, `integer`, `bigint`, `numeric`, `real`, `double precision`, both the same | that type |
| `+`, `-`, `*`, `/` | two of `smallint`, `integer`, `bigint`, mixed | the wider |
| `+`, `-`, `*`, `/` | `real` with `double precision`, either way | `double precision` |
| `=`, `<>`, `<`, `<=`, `>`, `>=` | two of one of `smallint`, `integer`, `bigint`, `numeric`, `real`, `double precision`, `text`, `bpchar`, `date`, `timestamp`, `timestamptz`, `boolean`; two of `smallint`, `integer`, `bigint` mixed; `real` with `double precision` | `boolean` |
| `=` | `xid`, `integer` | `boolean` |
| `\|\|` | `text`, `text` | `text` |
| `lower`, `upper` | `text` | `text` |
| `length` | `text` | `integer` |
| `date_trunc` | `text`, `timestamp without time zone` | `timestamp without time zone` |
| `date_trunc` | `text`, `timestamp with time zone` | `timestamp with time zone` |

A container test holds each row to the catalog of 14 and 18: an operator or a function of `pg_catalog` of exactly that name and those argument types exists, returns that type, and is implemented by the C function the remarks name.  A row that is not there on either major is dropped, never kept.

### 3. Casts within a family

A cast the plan prints, `(E)::T`, is not `NULL` when `E` is proved of type `S` and `S` and `T` are equal or in one family of [`SetOperationTypes`](../../src/SqlSource.Tool/Describing/Postgres/Walk/SetOperationTypes.cs).  Part C established why: `pg_cast` holds one row for a pair, the server ships a cast for every ordered pair of a family and pins it, so no user can replace it, and none of them returns `NULL` for a value that is not.  `T` is read as a built-in type name, with a modifier allowed (`numeric(10,2)`, a length coercion that errors or rounds).  Any other cast is unknown, as today.

### 4. Aggregates over a group that has a row

An aggregate other than `count`, read at the node that computes it, is not `NULL` when that node is an `Aggregate` with a `Group Key` and no `Grouping Sets` (fact 3), the aggregate has no `FILTER`, no `OVER` and no `WITHIN GROUP`, and every argument is proved.  Each group of such a node is made from the rows that share its key, so it has one row at least, and the aggregate sees every one of them.  Of these aggregates the final value is `NULL` only when no input was not `NULL` (`numeric_sum`, `numeric_avg`, `numeric_poly_sum`, `string_agg_finalfn`, a strict transition with no initial value for `max`, `min`, `bool_and`); a `DISTINCT` and an `ORDER BY` inside it change neither.  The table, by decision 2's rule of exact signatures:

| Aggregate | Argument types | Result |
|----|----|----|
| `max`, `min` | one of `smallint`, `integer`, `bigint`, `numeric`, `real`, `double precision`, `text`, `bpchar`, `date`, `timestamp`, `timestamptz` | that type |
| `sum` | `smallint`, `integer` / `bigint` / `numeric` / `real` / `double precision` | `bigint` / `numeric` / `numeric` / `real` / `double precision` |
| `avg` | `smallint`, `integer`, `bigint`, `numeric` / `real`, `double precision` | `numeric` / `double precision` |
| `string_agg` | `text`, `text` | `text` |
| `bool_and`, `bool_or`, `every` | `boolean` | `boolean` |

"Read at the node that computes it" is part B's rule: the walk follows the entry down while a child prints it, and reads it where none does.  The reader is told whether that node's groups have a row, and an aggregate inside a span that a child prints is read without it, since the child computed it under its own grouping.  So an aggregate is read only through the walk's following, and `PlanExpression` called on the root's entry directly answers no aggregate: the walk tries the direct reading first, as today, and follows the entry when that gives nothing.

### 5. A scalar subquery that always returns a row

A column whose whole entry is `(SubPlan <n>)` is not `NULL` when

- the `SubPlan` named so is found once in the plan, and its root is an `Aggregate` with `Strategy` `Plain`, no `Group Key`, no `Grouping Sets` and no `Filter`, with one `Output` entry: such a node returns exactly one row (fact 3);
- that entry is proved by the walk of the subplan as a plan of its own, by decisions 1 to 6 and part A, with a known type;
- the column's type, from the row description, is that type, and is in a family of `SetOperationTypes`.

The last condition is what tells a scalar subquery from the other kinds on 14 (fact 4): `EXISTS`, `IN`, `ANY` and `ALL` are `boolean`, and `ARRAY` an array of the value's type, none in a family.  On 18 the text already tells them apart; the rule is the same on both.  `lateral-latest-order` and `scalar-subquery-last-order` each have a `(SELECT count(*) ...)` column.

### 6. System columns, and three forms of 18

- **A system column** (`tableoid`, `ctid`, `xmin`, `xmax`, `cmin`, `cmax`) of a relation instance no join extends, of a table or a partitioned table that `ColumnNullability.Of` would answer a `NOT NULL` column of with `false` (no inheritance child, no foreign partition, no `INSTEAD` rule), is not `NULL`, of its type (`oid`, `tid`, `xid`, `cid`).  No user column may take those names.  A foreign table's are not read.  `upsert-product-returning` returns `(xmax = 0)`.
- **`EXISTS(SubPlan <n>)`** is never `NULL`: the executor starts it `false` and not null and sets it `true` at the first row (`ExecScanSubPlan`).
- **`(ANY (L op (SubPlan <n>).col<k>))`**, hashed or not, is not `NULL` when `L` is proved, column `k` of the subplan's root is proved by the walk of the subplan as a plan of its own, and `op` with their types is a comparison of decision 2.  `ExecScanSubPlan` combines the comparisons with `OR` and is `NULL` only when one is; `ExecHashSubPlan` is `NULL` only for a left side with a `NULL` or a subplan row with one (`havenullrows`).  An `EXISTS` that the planner hashed reads this way on 18, whatever the subquery's `WHERE`, since it is the comparison the planner made of it that is read.  Only one comparison is read; a row comparison, `((a, b) = ...)`, is unknown.
- **`MERGE_ACTION()`** is never `NULL`: `ExecEvalMergeSupportFunc` returns `INSERT`, `UPDATE` or `DELETE` or raises an error.

### 7. When `relations` is asked

A reference inside an expression is proved through the table row of its relation, and `tables` answers only the relations whose columns the row description names.  So `max(o.placed_at)` of `group-by-having`, whose plain columns are all of `accounts`, could not be proved.  `relations`, the catalog call part C added, is now asked whenever a column has no origin, for the relations the plan scans anywhere that `tables` did not answer, and not only when the root reaches a set operation.  It costs one round trip for a query with an expression over such a relation, and changes the capture of every fixture case of that shape; it adds no call and no member.  An error of it is logged and never reported, as today.

## The corpus after this part

| Case | Column | 14 | 18 | Decision |
|----|----|----|----|----|
| `group-by-having` | `revenue`, `last_placed_at` | 2 | 2 | 2, 3, 4 |
| `array-and-string-agg` | `product_names`, `units` | 2 | 2 | 4 |
| `order-report-four-tables` | `line_total` | 1 | 1 | 2, 3 |
| `insert-multirow-values` | `line_total` | 1 | 1 | 2, 3 |
| `scalar-subquery-last-order` | `order_count` | 1 | 1 | 5 |
| `lateral-latest-order` | `line_count` | 1 | 1 | 5 |
| `upsert-product-returning` | `inserted` | 1 | 1 | 2, 6 |
| `exists-as-column` | `has_orders`, `has_uninvoiced_orders` | | 2 | 6 |
| `merge-returning` | `action` | | 1 | 6 |
| | | 9 | 12 | |

The budgets become 44 and 42.  If the build finds one of these unproved, the column stays and the budget is set to what the corpus gives, with the reason in the plan.

## Where the code differs from the earlier documents

- The realistic cases record their answers in their committed sidecars, `query.sql.json`, written on the default image by `tools/describe-end-to-end.sh`; part C's plan names `tools/record-realistic.sh` and `expected-pg<n>.txt` files that no longer exist.
- The walk's files are under `Describing/Postgres/Walk/`, not beside the describer.
- Part A's design says the text cannot tell `EXISTS` from `IN`, which holds for 14; 18 prints each kind of subplan its own way (fact 4).
- Part A's design says a strict function needs its OID; for a signature `pg_catalog` holds exactly, the session's search path and the printed argument types settle it (decision 2).

## Shape of the work

One pull request, the design, the plan and the code, with the recorded files: the sidecars of the realistic cases, the end-to-end project and the examples where an answer changes, and the fixture cases whose capture gains `relations` or whose answer changes.  The estimate is under 80 files.  If the recorded files would take it over 150, the recordings go in a second pull request after the code, `(part 2 of 2)`.

## Success

- `RealisticNullabilityNoiseTests` passes with the lowered budgets, and `RealisticNullabilitySafetyTests` and the oracle pass on 14 and 18.
- Every diff of a recorded file is one the anti-goals allow.
- TD-0051 holds what remains, and `./pre-commit-validation.sh` passes.

## Decisions taken

| Decision | Whose |
|----|----|
| The kinds of goal 1, and none that needs a new server call | Mine |
| Exact signatures of `pg_catalog` with the session finding it first, polymorphic ones left | Mine |
| The closed tables of decisions 2 and 4, each held to the catalog by a container test | Mine |
| Casts within `SetOperationTypes`' families only | Mine |
| A scalar subquery only as a whole column of a type in a family | Mine |
| `relations` asked whenever a column has no origin | Mine |
| One pull request unless the recordings take it over 150 files | Mine |

## Left to the builder

- How the typed proof is threaded through `PlanExpression`'s reader, and how the reader is told that a span is a value a child printed.
- How the walk finds a `SubPlan` by name and walks it as a plan of its own, sharing `PlanInstancesCache`.
- Where the tables of decisions 2 and 4 live, as long as the container test reads them from there.
