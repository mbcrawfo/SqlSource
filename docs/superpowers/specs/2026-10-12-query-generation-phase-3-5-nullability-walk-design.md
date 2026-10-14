# Query generation, phase 3.5: the nullability walk - design

Date: 2026-10-12

Sub-phase 3.5 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It gives the PostgreSQL describer the third layer of nullability: for a query with an outer join, a generic plan from the server, walked to find which relation instances an outer join null-extends, so that a `NOT NULL` column on the inner side of a `LEFT JOIN` stays non-nullable and one on the outer side becomes nullable, with the planner's join strength reduction applied for free.  It replaces the heuristic of sub-phase 3.3 wherever the plan answers, and keeps it wherever the plan cannot.  The describer stays unregistered until sub-phase 3.6.

It builds on sub-phase 3.3, and is independent of sub-phase 3.4.

## Goal

```sql
-- name: GetUserWithLastOrder
SELECT u.id, u.name, o.id AS order_id, o.total
FROM users u LEFT JOIN orders o ON o.user_id = u.id
WHERE u.id = @id;
```

| Column | `nullable` | `nullableSource` |
|----|----|----|
| `id` | `false` | `catalog` |
| `name` | `false` | `catalog` |
| `order_id` | `true` | `outer-join` |
| `total` | `true` | `outer-join` |

With `AND o.total > @min` in the `WHERE`, the planner makes the join inner and every column is `false` with `catalog`.  `plan` is `walked`.

Success is:

- A column is `false` only when its origin is `NOT NULL` and the plan proves no outer join null-extends its relation instance.
- Every shape of the matrix, under every join method the planner can choose, gives the right answer, recorded as a fixture that runs without Docker.
- Anything the walk does not understand degrades to the heuristic, never to a wrong `false`.

## Decisions

From the epic, unchanged: the plan is a generic one, so that `WHERE x = $1` is not folded for the placeholder; the root node's `Output` is the result's target list in order; a plain reference prints as `alias.column`, which names the relation instance and so handles a self-join; a relation instance is nullable when a join on the path from the root to its scan null-extends its side, `Left` the `Inner` child, `Right` the `Outer`, `Full` both, `Inner` and `Semi` nothing; an entry that is not a plain reference stays unknown; a join type the walk does not know, an `Output` it cannot map, or a plan it cannot obtain drops to the heuristic; the plan beats the text; the sidecar records the outcome with the layer that decided it; the matrix under the epic's Testing with the planner's join methods toggled; the tool leaves the database as it found it.

Changed from the epic by the spike, and recorded in the outline:

| Decision | Choice | Why |
|----|----|----|
| How the plan is obtained, on every version | `SET LOCAL plan_cache_mode = force_generic_plan` and `EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE <name>(NULL, ...)` on the statement sub-phase 3.2 prepared, in a transaction that is rolled back | `EXPLAIN (GENERIC_PLAN)` of PostgreSQL 16 was planned for 16 and later.  Through Npgsql, with the parameters bound as nulls, 18.4 folded `u.id = $1` to a `Result` node with `One-Time Filter: false`: the hazard the epic named.  The `PREPARE` route gives a true generic plan on 14 and 18 alike, so the version boundary is gone. |
| A `Subquery Scan` or `CTE Scan` | A reference to one is unmappable: the column is `heuristic` | The epic said to map its outputs to the child plan's by position.  The scan's `Output` lists the subquery's columns by name, `c.total`, and the child plan's `Output` is positional expressions, `o.total`; the names of the subquery's columns are nowhere in the plan.  Matching by name is unsafe when a column is renamed to another column's name, so the reference stays unknown.  A subquery the planner flattens has no such scan and is walked exactly. |
| An unmappable `Output` entry | Degrades that column alone, to `heuristic` with an origin and `no-origin` without | The epic's safety net dropped the whole query.  A column's answer does not depend on another's, and a plan that cannot be used at all still drops every column. |
| A bare column name in `Output` | Resolves only when the root node is itself a scan of a relation, or a `ModifyTable` with no join under it | A single-relation query prints bare names, and so does a `Result` node that replaced a scan the planner proved empty, while another scan remains.  The one safe reading is the one with nothing to confuse it with. |

Settled in this spec:

| Decision | Choice | Why |
|----|----|----|
| Join types the walk knows | `Inner`, `Left`, `Right`, `Full`, `Semi`, `Anti`, `Right Semi`, `Right Anti` | `Right Anti` is PostgreSQL 16's and `Right Semi` 18's.  A semi join shows nothing of its hidden side above it and null-extends nothing. |
| An `Anti` join | Null-extends its `Inner` child, and a `Right Anti` its `Outer` | The epic said an `Anti` join extends nothing, which is true of a `NOT EXISTS`, whose inner side nothing above refers to.  The planner also makes an anti join of `a LEFT JOIN b ... WHERE b.x IS NULL`, keeps `b.id` in the join's `Output`, and returns it `NULL` in every row: found by the review of the walk and seen on 14, as `Anti`, and on 18, as `Right Anti`. |
| Which children are a join's sides | Children whose `Parent Relationship` is `Outer` or `Inner` | `InitPlan`, `SubPlan`, `Member` and `Subquery` children are other plans: a CTE's plan hangs under whichever node first needs it, and its scans are not instances of this plan's joins. |
| Where a relation instance may be found | The root and the nodes reached from it through `Outer` and `Inner` children | The same reason: an alias under an `InitPlan` is the CTE's, reached from here only through a `CTE Scan`. |
| What names a relation instance | `Alias` on a scan of a relation: `Seq Scan`, `Index Scan`, `Index Only Scan`, `Bitmap Heap Scan`, `Tid Scan`, `Tid Range Scan`, `Sample Scan`, and `ModifyTable` at the root, whose `Alias` is its target | A `RETURNING` list refers to the target by its alias, and the row it returns is the one the statement acted on, which no join null-extends. |
| The target's own scan | A scan under a root `ModifyTable` with the `ModifyTable`'s `Alias` is the target itself: the same instance, with the root's empty path, and no doubled alias | An `UPDATE` or a `DELETE` scans its target under the `ModifyTable` with the same alias, on 14 and 18 alike, so every `RETURNING` of one would be unusable otherwise.  A `MERGE` whose target is under a `Full` join still returns the row it inserted or deleted. |
| A reference at a root `ModifyTable` in a statement that names `OLD` or `NEW`, bare or as a quoted identifier (`"old"`, `"new"` in any case, or any `U&"..."` identifier, whose escapes the tool does not decode) | Maps only to the target's own alias; any other qualified reference keeps the heuristic | PostgreSQL 18 prints its old and new row under a name that an alias of an inner scope can share in the plan, and the plan does not tell the two apart.  It resolves `"old"` and `"new"` as it does `old` and `new`.  The target's alias is safe: the server refuses an old or new name that clashes with it, and where a table of the statement is named `old` the special name is not available. |
| When the plan is asked for | For a gated query with rows, after the `check` call and before `deallocate` | A query that is not gated pays nothing, as the epic wants, and the order keeps the captures of sub-phase 3.3 valid for every query that is not gated. |
| When the table match is made | After the `check` call and the walk, where sub-phase 3.4 made it before `check` | Its last check reads the nullability the walk set.  It asks the server nothing, so no capture changes; a query whose `check` fails has no `postgres.tableMatch` event. |
| Planner settings | The `explain` request carries a list of `enable_*` settings for the tests; the tool itself passes none | The matrix forces each join method.  A setting's name is one of a fixed list the describer holds, so nothing else reaches a `SET LOCAL`. |
| An `EXPLAIN` that fails | Logged, not reported: `plan` is `unavailable` and every origin column is `heuristic` | A plan the server will not give is not a fault of the query, and the heuristic is safe. |
| A plan that was obtained and cannot be used | `plan` is `unavailable` too, and the `postgres.walk` event's `reason` tells the two apart | The sidecar format wrote `unavailable` for a failed `EXPLAIN` alone; its meaning is widened, additively, to "the walk had no plan it could use", and the outline and the tool's readme say so.  The log is where "a walk bug" is told from "`EXPLAIN` failed". |
| Grouping sets | A node with a `Grouping Sets` key, or of `Node Type` `MixedAggregate`, among the nodes of step 1 makes the plan unusable | `ROLLUP`, `CUBE` and `GROUPING SETS` put `NULL` into a grouping column on a subtotal row, and the column keeps its origin and prints as a plain reference.  The gate of sub-phase 3.3 lets such a query in; the walk must not answer `false` for it. |
| Planner settings | `PostgresDescriber` takes them at construction, and `ToolHost.Create` constructs one with none; the harness constructs one for each case from its `explain-settings.txt` through the factory of sub-phase 3.1 | One instance in the real host, one per case in the tests. |

## Out of scope

- A test of the walk against PostgreSQL's own planner for every shape SQL can take.  The matrix is the set the epic names, with the shapes the spike found, and a user's bug bundle adds to it.
- Mapping a reference through a `Subquery Scan` or `CTE Scan`.  `docs/tech-debt` records what it would take and why name matching was refused.
- CockroachDB, which rejects the `EXPLAIN` form.  Out of the epic.

## The call

| Method | Request | Answer |
|----|----|----|
| `explain` | `statement`, `parameters`: the count, `settings`: `name=value` pairs, usually empty | `plan`: the JSON text the server returned; or `error` |

The `live` function: `BEGIN`; `SET LOCAL plan_cache_mode = force_generic_plan`; one `SET LOCAL` for each setting; `EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE <name>(NULL, ...)`, or `EXECUTE <name>` for a statement without parameters; `ROLLBACK`.  A failure at any step rolls back and is the answer's `error`.  The rollback is on the same connection, outside the exchange's view: it is part of the one call.  Nothing is executed but the `EXPLAIN` itself, which plans and does not run the statement.

The settings a request may carry: `enable_hashjoin`, `enable_mergejoin`, `enable_nestloop`, `enable_memoize`, `enable_material`, each `on` or `off`.  `PostgresDescriber` takes them at construction, for the tests; a name outside the list throws.

## The plan

`PlanNode`, read from the answer's text with System.Text.Json, every other key ignored:

| Key | Member |
|----|----|
| `Node Type` | `NodeType` |
| `Join Type` | `JoinType`, or null |
| `Parent Relationship` | `ParentRelationship`, or null on the root |
| `Output` | `Output`, a list of strings, or null |
| `Alias` | `Alias`, or null |
| `Relation Name`, `Schema` | `RelationName`, `Schema`, or null |
| `Subplan Name`, `CTE Name` | `SubplanName`, `CteName`, or null |
| `Grouping Sets` | `HasGroupingSets`, whether the key is present |
| `Plans` | `Plans`, the children, empty when none |

The text is an array of one object whose `Plan` is the root.  Anything else is a plan that cannot be used.

## The walk

`NullabilityWalk.Run(plan, columns)` in `src/SqlSource.Tool/Describing/Postgres/`, over the root node and the columns as sub-phase 3.3 built them, with their origins and the catalog's answer for each.  It gives each column its `nullable` and `nullableSource`, and the `plan` value.

**1. The instances.**  From the root, following children whose `ParentRelationship` is `Outer` or `Inner`, collect every node that names a relation instance, with the path of join nodes above it and the relationship under which each was passed.  A join node is one with a `JoinType`.  A root `ModifyTable` with an `Alias` is an instance with an empty path, and a scan below it with that `Alias` is the same instance and is not collected again.  A `JoinType` that is not one of the eight, an `Alias` found twice otherwise, or a node with `HasGroupingSets` or of `Node Type` `MixedAggregate` makes the plan unusable.

**2. Null-extension.**  An instance is null-extended when a join on its path is `Left` or `Anti` and the instance is under its `Inner` child, `Right` or `Right Anti` and under its `Outer` child, or `Full`.

**3. Each column**, from `Output[i]` of the root, where `i` is the column's ordinal:

| The entry | Reads as | The column |
|----|----|----|
| `alias.column`, either part a bare identifier or a quoted one with `""` for a quote | A reference to the instance named `alias` | Mappable when the alias is an instance of step 1 |
| A bare identifier | A reference to the root itself | Mappable when the root is a scan of a relation with an `Alias`, or a `ModifyTable` with an `Alias` and no join among the nodes of step 1 |
| Anything else: an operator, a parenthesis, a space, `$1`, `NULL::numeric`, `(SubPlan 1)`, `COALESCE(u.id, p.id)` | An expression | Unmappable |
| An alias that names a `Subquery Scan`, `CTE Scan`, `Function Scan`, `Values Scan`, `WorkTable Scan` or any other node that is no scan of a relation | | Unmappable |

| Column | Mappable, not null-extended | Mappable, null-extended | Unmappable |
|----|----|----|----|
| Origin in a table, `NOT NULL` | `false`, `catalog` | `true`, `outer-join` | `true`, `heuristic` |
| Origin in a table, nullable | `true`, `catalog` | `true`, `outer-join` | `true`, `heuristic` |
| Origin in a view or a foreign table | `null`, `view` | `true`, `outer-join` | `null`, `view` |
| No origin | `null`, `no-origin` | `null`, `no-origin` | `null`, `no-origin` |

A column without an origin is not looked up at all: nothing of the plan can make it non-nullable.

**4. The plan's value.**  `walked` when the plan was used; `unavailable` when `explain` answered an error, the text could not be read, the root has no `Output` or fewer entries than columns, a `JoinType` is unknown, an alias is doubled or a grouping set is in the plan: then every column with an origin in a table is `true` with `heuristic`, and one in a view `null` with `view`.  `Output[i]` is the column's entry because PostgreSQL prints the target list first and any entry it added for a sort or a group after it.

Sub-phase 3.4's match runs after the walk, on the nullability it set.

### What the spike showed the walk must handle

- A `LEFT JOIN` the planner swapped is a `Hash Join` with `Join Type` `Right` and the sides swapped; the rule gives the same answer.
- A join the planner removed leaves a scan at the root with qualified names, `u.id`.
- A `LEFT JOIN` with a `WHERE` on the nullable side, or followed by an inner join on it, plans as `Inner`.
- `ModifyTable`'s `Output` is the `RETURNING` list; an `INSERT` has a `Result` child with `$1`; an `UPDATE ... FROM` has a join child.
- An `UPDATE` or a `DELETE` has a scan of its target under the `ModifyTable`, with the `ModifyTable`'s own `Alias`, on 14 and 18.  A `DELETE` of one table prints bare names, `id`, and an `UPDATE ... FROM` qualified ones, `u.id`.  A `RETURNING` of a relation the planner proved empty under an outer join prints a bare name too, `id`, from a `Result` under the join, which the review of the walk found on 17 and 18: so a bare name at a `ModifyTable` is its target's only when no join is under it.
- The `OLD` and `NEW` of a `RETURNING` list of PostgreSQL 18 print as `old.id` and `new.name`, or under the names `WITH (OLD AS o, NEW AS n)` gives them, and never under the target's alias.  The server refuses an old or new name that a table of the statement's top level has, but an alias of an inner scope reaches the plan under its own name: the `SELECT` of an `INSERT ... SELECT`, a flattened subquery in `FROM` or `USING`, an inlined CTE.  The final review found `INSERT INTO ord2 (id, user_id) SELECT old.id + 100, old.id FROM users old RETURNING old.id, new.id` on 18, whose `Output` is `old.id, new.id` over a `Seq Scan on public.users old`, with `old.id` `NULL` in every row.  So in a statement that names `OLD` or `NEW` a qualified reference at a root `ModifyTable` maps only to the target's own alias, and any other stays `heuristic`.  PostgreSQL 18 resolves the quoted `"old".id` and `"new".id` too, so `OuterJoinGate` counts a quoted identifier named `old` or `new` in any case, and any Unicode-escaped identifier `U&"..."`, whose escapes the tool does not decode, for the gate and for this rule alike.
- A `MERGE` with `WHEN NOT MATCHED BY SOURCE` and `WHEN NOT MATCHED BY TARGET` plans as a `Full` join under the `ModifyTable`: the source's columns are `outer-join`, and the target's are the row acted on.
- A `UNION` root is an `Append` with no `Output`: unusable, every origin column `heuristic`, which the set operation deserves since its columns have no origin anyway.
- A `LEFT JOIN ... ON ... AND o.id IS NULL` the planner proved empty prints bare names, `total`, from a `Result` node beside a real scan: unmappable, `heuristic`.
- A view is flattened; its scans get unique aliases, `u_1`, and the view's columns are walked as its own joins dictate, with `view` where nothing null-extends them.
- Aliases are unique in a plan; a quoted one prints as `"Order".total`.
- A view's columns have the view as their origin while the plan prints its base tables' aliases, `u_1.id`: the one place where the origin and the mapped instance name different relations, which is why a view's mapped column that nothing extends stays `view`.
- A partitioned table or an inheritance parent scans as an `Append` of `Member` children, which step 1 does not collect, so its columns are `heuristic` unless the planner pruned to one partition; `docs/tech-debt` records it beside the subquery scans.

### Events

| Event | Members |
|----|----|
| `postgres.explain` | `query`, `statement`, `settings`, `sql`: the `EXPLAIN` statement as sent, `plan`: the text, which the log's writer cuts at 1 MiB, or `error`, `durationMs` |
| `postgres.walk.node` | One for each node visited in step 1: `depth`, `nodeType`, `joinType`, `parentRelationship`, `alias`, `relation`: `schema.name`, `nullExtends`: `inner`, `outer`, `both` or `none` |
| `postgres.walk.column` | One for each column: `ordinal`, `name`, `output`: the entry, `alias`, `column`, `mapped`, `nullExtended`, `nullable`, `nullableSource` |
| `postgres.walk` | `query`, `plan`: the value, `reason` when `unavailable`: `explain-failed`, `unreadable`, `no-output`, `unknown-join-type`, `doubled-alias` or `grouping-sets` |

`postgres.column` of sub-phase 3.3 is logged when a column is built, before the plan is asked for: for a gated query it holds the heuristic's answer, and `postgres.walk.column` holds the one the sidecar records.

### Diagnostics

None: the walk degrades and never reports.

## The matrix

Each a fixture case under `Fixtures/Describers/postgres/`, with its expected nullability for every column, recorded with `tools/record-fixtures.sh`.  A case whose name ends in `-hash`, `-merge` or `-nestloop` has an `explain-settings.txt` beside its `query.sql`, one `name=value` on a line, turning the other two methods off; `FixtureReplay` and `FixtureCasesTests` read it and construct the describer with the settings.

| Case | Shape |
|----|----|
| `walk-left`, `-hash`, `-merge`, `-nestloop` | `a LEFT JOIN b`: `a` catalog, `b` outer-join |
| `walk-right`, with the three methods | `a RIGHT JOIN b`: `a` outer-join, `b` catalog |
| `walk-full`, `-hash`, `-merge` | Both outer-join; no nested loop, which a `FULL JOIN` cannot use |
| `walk-nested` | `a LEFT JOIN (b JOIN c)`: `b` and `c` outer-join |
| `walk-chain` | `a LEFT JOIN b LEFT JOIN c`: `b` and `c` outer-join |
| `walk-self-join` | `users a LEFT JOIN users b`: `a` catalog, `b` outer-join |
| `walk-reduced-by-where` | `LEFT JOIN` with `WHERE b.x > @p`: all catalog |
| `walk-reduced-by-join` | `LEFT JOIN b` then `JOIN c ON c.id = b.id`: all catalog |
| `walk-removed` | `LEFT JOIN` to an unreferenced unique table: catalog |
| `walk-subquery-flattened` | An outer join inside a subquery in `FROM` that the planner flattens: exact |
| `walk-subquery-kept` | The same with a window function, so a `Subquery Scan` stays: heuristic |
| `walk-cte-inlined` | A CTE referenced once: exact |
| `walk-cte-materialized` | `MATERIALIZED`, or referenced twice: heuristic |
| `walk-lateral` | `LEFT JOIN LATERAL`: the lateral side outer-join |
| `walk-view` | A view over an outer join: each column as the view's own join dictates, `view` where nothing extends it |
| `walk-view-nullable-side` | A view on the nullable side: outer-join |
| `walk-using` and `walk-natural` | The merged column no-origin, the rest as the join dictates |
| `walk-aggregate` | `GROUP BY` plain keys over an outer join: keys as the join dictates, aggregates no-origin |
| `walk-union` | Two outer-join queries: unusable, heuristic |
| `walk-returning` | `UPDATE ... FROM ... RETURNING`: the target catalog, the joined table as the join dictates |
| `walk-proved-empty` | `ON ... AND o.id IS NULL`: heuristic |
| `walk-quoted-alias` | `orders "Order"`: mapped |
| `walk-semi-anti` | `EXISTS` and `NOT EXISTS`: catalog |
| `walk-anti` | `a LEFT JOIN b ... WHERE b.x IS NULL`, which plans as an anti join: `a` catalog, `b` outer-join |
| `walk-rollup`, `walk-cube`, `walk-grouping-sets` | A `NOT NULL` grouping column: `heuristic`, `plan` `unavailable` with `grouping-sets` |
| `walk-order-by-unselected` | `ORDER BY` a column not in the select list over an outer join: the selected columns as the join dictates |

Sub-phase 3.2's `FixtureCasesTests` describes every case live on 14 and on 18 and compares each with the one `expected.json`, so a case must give the same nullability on both.  The cases are described against `postgres:14` before what was recorded is committed.

## Order of work

One pull request, one commit for each step.  Each leaves `./pre-commit-validation.sh` passing and brings its tests and the documents it makes wrong.

1. The epic outline: the row of 3.5 says In progress.
2. `explain` and `PlanNode`.
3. `NullabilityWalk` steps 1 and 2, with the known join types.
4. Step 3, the references, and step 4, the plan's value, replacing the heuristic in `DescribeAsync`.
5. The settings for the tests, and `explain-settings.txt` in the harness.
6. The events.
7. The matrix, recorded, and every gated fixture that a sub-phase merged before this one recorded, 3.3's and, when it came first, 3.4's, re-recorded.
8. The documents, and the outline's row set to Done.

## Testing

| Where | Cases |
|----|----|
| `PlanNodeTests` (tool tests) | Each key read, `Grouping Sets` as presence; unknown keys ignored; no `Plans`; a text that is not an array of one plan is unusable |
| `OutputReferenceTests` (tool tests) | `u.id`, `users.id`, `"Order".total`, `"a""b".c`, `id`; each of the expression forms is none |
| `NullabilityWalkTests` (tool tests) | Over plans written by the test: each join type and each side; a `Right Semi` extends nothing, an `Anti` its inner side and a `Right Anti` its outer; a chain of two joins; a swapped `Right`; an `InitPlan` child not traversed and its alias not found; a `Subquery Scan` alias unmappable; a bare name at a scan root and under a join; a doubled alias is unusable; a root `ModifyTable` over a scan of its own alias is usable, its target not null-extended under a `Full` join, and bare names resolve at it; an `old.id` that names no instance keeps the heuristic; at a root `ModifyTable` whose statement may name `OLD` or `NEW` an alias other than the target's keeps the heuristic, the same plan maps it when neither is named, and a root that is no `ModifyTable` is not affected; an unknown join type is unusable; a `Grouping Sets` key and a `MixedAggregate` are unusable; a root without `Output`; fewer entries than columns; each row of the column table; a view origin null-extended and not; each `reason` |
| `WalkTests` (container) | A settings name outside the list throws; `pg_prepared_statements` is empty and `plan_cache_mode` is what `open` read after each describe; on 17 and later, a `MERGE ... RETURNING` with the source's columns `outer-join` and the target's `catalog`; on 18, a `RETURNING` with `OLD` and `NEW`, `heuristic`.  The last two are no fixtures: 14 has neither |
| `FixtureCasesTests`, `DescriberFixtureTests` | The matrix, live on 14 and 18 through CI's two runs and replayed without Docker, and the re-recorded cases |

## Documentation

- `src/SqlSource.Tool/AGENTS.md`: the `PREPARE` route and why `GENERIC_PLAN` is not used; which children are sides and which are other plans; a `Subquery Scan` or `CTE Scan` is unmappable and name matching is refused; a bare name resolves at a scan root, or at a `ModifyTable` root with no join under it, alone; the walk degrades a column alone, and the plan whole; the settings are for the tests and the list is closed.
- `docs/tech-debt`: references through a `Subquery Scan` or `CTE Scan` are unknown, what mapping them would need, and why name matching is unsafe; the columns of a partitioned or inherited table under an `Append`.
- `docs/diagnostics.md`, `src/SqlSource.Tool/README.md`, the repository's `README.md`, `CONTRIBUTING.md`, `docs/publishing.md`: nothing changes.
- `docs/deferred`: nothing is expected.
- The epic outline: in steps 1 and 8, and its Nullability notes corrected for the route, the subquery mapping and the widened `unavailable`.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.  When this spec was written the repository had no `v*` tag.
