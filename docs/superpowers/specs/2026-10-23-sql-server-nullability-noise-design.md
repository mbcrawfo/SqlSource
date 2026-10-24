# The SQL Server describer's nullability noise - design

Date: 2026-10-23

## Why

[TD-0071](../../tech-debt/TD-0071-sql-server-answers-nullable-for-columns-that-cannot-be-null.md) records that the SQL Server describer answers nullable for 39 columns of the realistic corpus on SQL Server 2017 and 44 on 2025 that can never be `NULL` (`RealisticNullabilityNoiseTests.Budgets`).  The describer keeps the server's `is_nullable` and never narrows it, and the server calls nullable every `COUNT`, every aggregate over a group, every window function, every `CAST` and every `COALESCE` whose first argument is nullable.  For a user each is a `?` on a property and a `!` added by hand.  The owner asked for the noise to be lowered.

The epic's rule holds without exception: a false nullable is a nuisance, a false non-nullable a silent wrong value.  No column that can be `NULL` is ever answered `false`.  The server's answer is corrected only where a proof shows it wrong, and anything the code does not recognise keeps the server's answer.

## What the server shows

These facts come from `sys.dm_exec_describe_first_result_set` and from running the queries, on `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04` and `2025-CU9-GDR1-ubuntu-24.04`, on 2026-10-23.  The two majors answer the same unless a line says otherwise.

1. **The server is not always wrong where it says nullable.**  With `ANSI_WARNINGS` and `ARITHABORT` both off, an arithmetic overflow, a conversion overflow and a divide by zero return `NULL` instead of raising an error: `CAST(300 AS tinyint)` is `NULL`, `SUM(a)` of two `int`s of `2147483647` over one group is `NULL`, and so is `a + 1`.  With `ANSI_WARNINGS` on, whatever `ARITHABORT` says, each raises error `220` or `8115`.  So `SUM`, a cast and arithmetic of values that are not null are not null only under `ANSI_WARNINGS` on.  SqlClient, like every driver of SQL Server, turns `ANSI_WARNINGS` on when it connects, and only a `SET` in the application's own code turns it off.
2. **`CONVERT` with a `NULL` style is `NULL`**: `CONVERT(varchar(30), SYSDATETIME(), NULL)`.
3. **No grouping returns a row for an empty input**: `GROUP BY ROLLUP (b)`, `GROUP BY CUBE (b)`, `GROUP BY GROUPING SETS ((b), ())` and `GROUP BY ()` over a table with no row return no row.  So every row of a query with a `GROUP BY` is of a group that has at least one row, the rows of a `ROLLUP`'s or a `CUBE`'s totals included.  A query with aggregates and no `GROUP BY` returns one row for an empty input, and its `SUM`, `MAX` and `MIN` are `NULL` there.
4. **A grouping column is `NULL` in the rows of a total**: under `ROLLUP`, `CUBE` and `GROUPING SETS`, a column that a row does not group by is `NULL` in that row, whatever it is in the table.
5. **A `PERSISTED NOT NULL` computed column read through an outer join is `NULL`, and keeps its origin**: in `SELECT t.c_one FROM dbo.u LEFT JOIN dbo.t ON t.id = u.t_id`, where `c_one AS (1) PERSISTED NOT NULL`, the server answers `is_nullable` 1 and `source_table` `t`, `source_column` `c_one`, as for any column of the outer side.  So the correction that TD-0071 proposes, keyed on the server's answer and the catalog alone, would answer `false` for a column that can be `NULL`.  Read from one table, the server answers it nullable whenever its expression could be (`CAST(a * 2 AS int) PERSISTED NOT NULL` is 1, `(1) PERSISTED NOT NULL` is 0).
6. **`JSON_OBJECT` and `JSON_ARRAY` are never `NULL`** (2025): `JSON_OBJECT('a': NULL)` is `{"a":null}`, `JSON_OBJECT()` and `JSON_OBJECT('a': NULL ABSENT ON NULL)` are `{}`, `JSON_ARRAY(NULL)` and `JSON_ARRAY()` are `[]`.  The server answers both nullable.
7. **`NTILE` takes only a positive integer**: `NTILE(@n)` with `@n` `NULL` is error `4116`.  `ROW_NUMBER`, `RANK` and `DENSE_RANK` take no argument.  The four are `bigint`, and the server answers each nullable.
8. **A built-in function is never a user's.**  T-SQL calls a user's scalar function and a CLR aggregate only by a name with its schema, `dbo.f(x)`; a bare `COUNT(x)` or `COALESCE(x, y)` is always the built-in.  This is what lets the text be read for a form, where PostgreSQL's search path needed a catalog call (TD-0051's part E).

## Goals

1. Prove not null, from the query's text and the server's answer about the pieces it names, the kinds the corpus over-reports most, and correct the server's answer for the columns proved:
   - `COUNT` and `COUNT_BIG`, as a result column, with or without `OVER`, and as the one column of a scalar subquery that has no `GROUP BY`, `HAVING`, `TOP`, `OFFSET` or set operator;
   - `SUM`, `MAX` and `MIN` of an operand that is not null, over a group of a `GROUP BY` (a plain one, `ROLLUP`, `CUBE` or `GROUPING SETS`), and as a window aggregate whose frame holds the current row;
   - `ROW_NUMBER`, `RANK`, `DENSE_RANK`, and `NTILE` of an integer literal;
   - `COALESCE` with an argument that is not null;
   - `CAST` and `CONVERT` of an operand that is not null, `CONVERT`'s style absent or an integer literal;
   - `JSON_OBJECT` and `JSON_ARRAY`;
   - and, as the owner decides (see "Decisions for the owner"), arithmetic, `+`, `-`, `*`, `/` and `%`, of operands that are not null, which the corpus's `SUM(ol.quantity * ol.unit_price)` and `COALESCE(...) + c.name` need, and `GREATEST` and `LEAST` with an argument that is not null.
2. The item's narrow correction, made sound: a `PERSISTED NOT NULL` computed column, read by a query whose `FROM` is that one table, is not null, with a fixture and the `nullableSource` `catalog`.
3. Each proof tested against attempts to break it, on 2017 and 2025: a query the proof must leave nullable, run over rows that give it `NULL`.
4. The budgets lowered with each correction, in its commit; the sidecars, fixtures and captures recorded again.
5. TD-0071 rewritten to what remains.

## Non-goals

- **The kinds the text cannot prove without a model of the query's rows**: a recursive CTE's columns, a `COALESCE` over both sides of a `FULL JOIN`, a `CASE` whose `ELSE` is the nullable column it guards, a nullable column that a filter, an `UPDATE`'s `SET` or a branch of a `UNION` makes not null.  They stay in TD-0071.
- **Other functions of operands that are not null**, `EOMONTH`, `AT TIME ZONE`, `LAG` and `LEAD` with a default, `DATEADD`, and the many more: each needs its own reading of what it returns, and none is in the owner's list.  They stay in TD-0071, by name.
- **A statement other than one `SELECT`**: an `INSERT`, `UPDATE`, `DELETE` or `MERGE` with `OUTPUT`, a `UNION`, `INTERSECT` or `EXCEPT`, a batch of several statements, and `FOR XML` or `FOR JSON` keep the server's answer.
- **A computed column that is not persisted, or persisted and read through a join**: its expression is the server's to judge, and the instance of its table the text would have to find.

## Anti-goals

- **No plan.**  Showplan is not read (below).
- **No parser of T-SQL.**  The reader knows the outline of one `SELECT` and the forms above, and gives up on anything else, which keeps the server's answer.
- **No new error.**  A proof that cannot be made, and a call that fails, leave the column as the server answered it; nothing is reported to the user.

## The sources of a proof

Three sources were weighed.

**Showplan XML** (`SET SHOWPLAN_XML ON`) holds every operator's output columns and their expressions as trees: an `Aggregate` of `countstar`, `SUM` as `CASE WHEN COUNT_BIG(x) = 0 THEN NULL ELSE SUM(x) END`, each `Convert`.  It is rejected:

1. **It weakens the describer's security invariant.**  Today the user's text reaches the server only as the value of `@tsql` of two procedures that analyse a text and run nothing.  Showplan needs `SET SHOWPLAN_XML ON` as a batch of its own, then the user's text as a batch, or as the argument of `sp_executesql`, run while the setting holds.  The guarantee would rest on a session setting that stays on.
2. **It names no result column.**  The root's `OutputList` holds each distinct column or expression once, so `SELECT id, id` and a constant folded away do not map one to one onto the result.
3. **It does not say a column's nullability.**  `SUM` of a column that is not null needs to know that no outer join reaches the column: a walk of `Nested Loops`, `Hash Match`, `Merge Join`, `Apply`, `Concatenation`, spools and segments, the size of PostgreSQL's plan walk, for a second engine.
4. **It changes with the data.**  The optimiser picks a plan by statistics, so a capture would differ between the fixtures' database and the corpus's, and between 2017 and 2025; the describe procedure's answer does not.

**The catalog alone** sees no expression: `COUNT(*)` has no origin.  It gives one fact this design needs, whether a computed column is `PERSISTED NOT NULL`.

**The query's text** shows each result column's expression, and so its outer form: `COUNT(...)`, `COALESCE(a, b)`.  What it cannot show is whether a column it names is null, through joins, `APPLY`, CTEs, views and synonyms.

**The server can.**  `sys.dm_exec_describe_first_result_set` already judges a column's nullability through every join, and the spike found it right everywhere a `NULL` can come out.  So the describer asks it about the operands: a second describe of a text built from the query's own tokens, `SELECT <operand 1> AS [1], <operand 2> AS [2], ...` followed by the query's own `FROM` to the end of its `WHERE`, behind the query's own `WITH`.  Each operand is evaluated over the same rows of the same `FROM` and `WHERE` as it is in the query, so the server's answer about it holds there.

So the choice is **the text, read with the T-SQL lexer, for the form of each result column; the server, asked in one new call, for whether an operand is not null; and the catalog, read by the tool's own text of `columns`, for a `PERSISTED NOT NULL` computed column.**  The new call is sent like `columns`: the text as `@tsql` and the same declarations as `@params` of `sys.dm_exec_describe_first_result_set`, which analyses and runs nothing.

## Decisions

### 1. The reader of one `SELECT`

The proofs read the query only when its text is, after the lexer's comments, an optional `;`, an optional `WITH` clause, and one `SELECT` that ends the text or is followed by `;` alone.  The `SELECT` may have `DISTINCT`, `TOP (...)` with `PERCENT` and `WITH TIES`, `FROM`, `WHERE`, `GROUP BY`, `HAVING`, `WINDOW`, `ORDER BY` with `OFFSET` and `FETCH`, and `OPTION`; it may not have `INTO`, `FOR XML`, `FOR JSON`, `FOR BROWSE`, or a `UNION`, `INTERSECT` or `EXCEPT` at its level.  Anything else, and any text the reader cannot follow, proves nothing.

The lexer, `DescriberSql.Lex` with the rules of `mssql`, gives comments, quoted regions and parameters, and leaves the rest as text.  A small tokenizer splits that text into words, numbers, operators and punctuation; a quoted region is a string when it opens with `'` (an `N` before it joins it) and a name otherwise.  Depth is counted by parentheses, so a subquery's clauses are never the outer query's.

The select list is split into items at its commas of depth 0.  An item is mapped to the visible column of the same place, and only when the counts agree, no item is a `*` or `q.*` (but for the computed column of decision 6), and each item's name is the server's: its alias, `expr AS name`, `expr name` or `name = expr`, a bracketed, double-quoted or string alias read without its quotes; or, for an item that is a column alone, the column's last part.  One item that does not agree, in count or in name, proves nothing for the whole query, since the reader has misread its select list.

### 2. What proves an expression not null

An expression is proved not null by the first of these that applies, read from its tokens:

| Form | Proved when |
|----|----|
| A string, `N` string, number or binary literal | Always.  `NULL` is not a literal here |
| `( e )` | `e` is proved |
| `COUNT( ... )`, `COUNT_BIG( ... )`, with `ALL`, `DISTINCT` or `*`, with or without `OVER (...)` | Always |
| `( SELECT COUNT(...) [AS a] [FROM ...] [WHERE ...] )`, and `COUNT_BIG` | No `GROUP BY`, `HAVING`, `TOP`, `OFFSET`, `FOR` or set operator at the subquery's level: it returns one row |
| `ROW_NUMBER()`, `RANK()`, `DENSE_RANK()` with `OVER (...)`; `NTILE(n) OVER (...)` | `n`, if any, is an integer literal |
| `SUM`, `MAX`, `MIN` of `[ALL | DISTINCT] e`, no `OVER` | The `SELECT` has a `GROUP BY` that is not `GROUP BY ALL`, and `e` is proved in the rows' context (below) |
| `SUM`, `MAX`, `MIN` of `e` with `OVER (...)` | The frame holds the current row, and `e` is proved in the select's context.  A frame holds it when it is absent, or is `ROWS` or `RANGE` with a start of `UNBOUNDED PRECEDING`, `n PRECEDING` or `CURRENT ROW` and an end, if any, of `CURRENT ROW`, `n FOLLOWING` or `UNBOUNDED FOLLOWING`, `n` an integer literal.  `OVER w`, a named window, proves nothing |
| `COALESCE(e1, ..., en)` | One `ei` is proved |
| `CAST(e AS type)`, `CONVERT(type, e)`, `CONVERT(type, e, style)` | `e` is proved, and `style` is an integer literal.  `TRY_CAST`, `TRY_CONVERT`, `PARSE` and `TRY_PARSE` prove nothing |
| `JSON_OBJECT( ... )`, `JSON_ARRAY( ... )` | Always |
| `e1 op e2 op ...`, `op` among `+`, `-`, `*`, `/`, `%` at depth 0, and a leading unary `-`, `+` or `~` (if the owner takes arithmetic) | Every operand is proved |
| `GREATEST(e1, ...)`, `LEAST(e1, ...)` (if the owner takes them) | One `ei` is proved |
| Anything else, a parameter and `NULL` excepted | It may be asked (decision 3) |

A name is matched without its case, and only when it is bare: `dbo.COUNT(x)` is a user's function.

The **context** says whether an expression may be asked.  The rows' context is the operand of an aggregate without `OVER`: its value is computed over the rows of the `FROM` and the `WHERE`, so it may be asked.  The select's context is everything else in an item.  It may be asked too, unless the `SELECT`'s `GROUP BY` has `ROLLUP`, `CUBE` or `GROUPING SETS` (fact 4), where a grouping column is `NULL` in a total's row whatever the server says of it over the rows.  Under a plain `GROUP BY`, an expression outside an aggregate can only be of the grouping columns, whose value in a group's row is a value of its rows.  An expression that holds an aggregate's name or `OVER` at its own level, outside a subquery, and is not one of the forms above is never asked, since outside its `GROUP BY` it means something else.  Nor is a schema-qualified call, which may be a CLR aggregate, nor anything that holds one at its own level.

### 3. The call `operands`

When the proof of some item needs the server, the describer asks one more call after `columns`, before `table`: `operands`, whose request and answer are `columns`' records, with the text `SqlServerLive` already sends.  Its `sql` is the query's `WITH` clause, as written, then `SELECT`, then each operand to ask, its tokens' text as written followed by a line break and `AS [<n>]`, the operands separated by commas, then the query's text from its `FROM` to the end of its `WHERE`, or nothing for a `SELECT` without `FROM`.  `GROUP BY`, `HAVING`, `WINDOW`, `ORDER BY`, `OFFSET` and `OPTION` are left out.  Its `params` are those of `columns`.  The line break ends any line comment inside an operand.

An operand is not null when its row is visible, at its place, with the name `<n>`, and `is_nullable` false.  An answer that holds an `error` or error rows, or whose visible rows are not the operands one for one, proves no operand, and the proofs that need none still stand.  Nothing about it is reported: it is logged as `mssql.operands`, as each call is.

The text is the query's own tokens, rearranged, and is sent only as `@tsql`, so [why a query cannot run a statement](../../architecture/sqlserver-describer.md#why-a-query-cannot-run-a-statement) still holds and gains a row.

### 4. The answer of a proved column

A column that a form proves is `nullable` false with the `nullableSource` `expression`, which the sidecar format already defines for PostgreSQL's plan: the describer proved it from the column's expression.  Every other column keeps `server`.  The `mssql.column` event holds the source as it does today.

### 5. The setting the proofs assume

`COUNT` (an `int`), `SUM`, `CAST`, `CONVERT`, `COALESCE` (which converts to its type) and arithmetic are not null only under `ANSI_WARNINGS` on (fact 1).  The describer assumes it, as every driver sets it, and the manual's SQL Server page says, as a known issue, that a connection which turns `ANSI_WARNINGS` and `ARITHABORT` off can read `NULL` from such a column.  `MAX`, `MIN`, `COUNT_BIG`, the ranking functions, `JSON_OBJECT`, `JSON_ARRAY` and a computed column hold whatever the settings.

### 6. A `PERSISTED NOT NULL` computed column

The tool's text of `columns` and `table` reads one more column of its own, `persisted_not_null`, beside `source_type`: true for a row whose source is a table of the session's database (`source_type` `U`) and whose source column `sys.computed_columns` marks `is_persisted` and not `is_nullable`.  The row's record gains `PersistedNotNull`.

A column whose row says so is `nullable` false, with the `nullableSource` `catalog`, when the query's `FROM` is one table alone, `FROM name [[AS] alias] [WITH (hints)]` with nothing after it but its `WHERE` and the clauses that follow, the name not one of the `WITH` clause's, the `SELECT` has no `ROLLUP`, `CUBE` or `GROUPING SETS`, and the column's item is a column alone, or the item is a `*` or `q.*` and the only one.  Then every row of the result is a row of that table, which the constraint keeps not null.  A query that reads it through a join keeps the server's answer (fact 5).

The rows of `table` are `SELECT *` from the table alone, so each such column of them is corrected the same way, and the table match compares a corrected query with a corrected table: `select-computed-not-null` stays `matched`.  A query that reads a table with such a column through a join, and so cannot prove it, no longer matches that table on `nullability-differs`; no case of the corpus, the end-to-end project or the examples reads one.

## The corpus after this design

| Kind | Columns, 2017 | More on 2025 |
|----|----|----|
| `COUNT`: `order_count` of `customer-order-counts`, `big-customers-having`, `orders-grouping-sets`, `customers-last-payment-subquery` (a scalar subquery) and `customers-first-order-value` (`OVER`); `line_count`, `product_count`, `shipped_count` | 8 | |
| Ranking: `rank_in_category`, `sales_tier` | 2 | |
| `SUM`, `MAX` of a column: `big-customers-having` `last_ordered_at`, `sales-by-category-rollup` `units`, `top-with-ties-best-sellers` `units_sold`, `orders-grouping-sets` `last_placed_at`, `payments-running-total` `paid_to_date` (a frame of `ROWS UNBOUNDED PRECEDING`) | 5 | |
| `COALESCE`: `display_name`, `due_on`, `effective_price` | 3 | |
| `CAST`: `is_open`, `has_open_cart`, each of a `CASE` the server answers not null | 2 | |
| `JSON_OBJECT`, `JSON_ARRAY`: `contact`, `reachable_by`, `channels` | | 3 |
| Arithmetic: `total_spent` and both `revenue`s (`SUM(ol.quantity * ol.unit_price)`), and `breadcrumb` (`COALESCE(...) + c.name`) | 4 | |
| `GREATEST`, `LEAST`: `lowest_price`, `highest_price` | | 2 |

With every kind, the budgets go from 39 to 15 on 2017 and from 44 to 15 on 2025; without arithmetic and `GREATEST`/`LEAST`, to 19 and 21.  What remains, 15 columns: the four of the recursive CTE, the `FULL JOIN`'s `invoice_id`, `EOMONTH`'s `statement_on`, `AT TIME ZONE`'s `placed_utc`, `LAG`'s `previous_status`, the guarded `CASE`'s `ship_to`, the computed `sale_price`, and the filters and `SET`s of `cte-feeding-update` (`outstanding`, `note`, `due_at`), `update-orders-from-join-output` (`shipped_at`) and `contacts-union` (`name`).

## Tests

- **The reader, without a server** (`tests/SqlSource.Tool.Tests`): each form proved and not proved from text; the select list's split, the names' check and every reason the reader gives up (`*` among items, a set operator, a second statement, `INTO`, `FOR JSON`, an item it misreads); the text of `operands`, a line comment inside an operand included; an `operands` answer that is short, misnamed or an error.
- **The proofs, on a server** (`tests/SqlSource.Tests.SqlServer/Describer/`, on 2017 and 2025): for each form, a query the describer proves, and beside it the queries that must stay nullable, each run over rows that make it `NULL` to show the server was right: `MAX` with no `GROUP BY` over no row; `MAX` under `GROUP BY ALL`; `SUM` of a column of the outer side of a `LEFT JOIN`; a scalar subquery of `COUNT(*)` with a `GROUP BY` or a `HAVING`; `SUM() OVER (ROWS BETWEEN 1 FOLLOWING AND 2 FOLLOWING)`; `COALESCE` of a grouping column under `ROLLUP`; `CAST` of a grouping column under `ROLLUP`; `CONVERT` with a style that is a parameter; `TRY_CAST`; `NTILE(@n)`; `dbo.COUNT`-like user functions; `COALESCE` of nullable arguments alone; `JSON_OBJECT` in a scalar subquery with `TOP (1)` and no row; a `PERSISTED NOT NULL` column through a `LEFT JOIN`, and under `ROLLUP`.  And the facts above that the proofs stand on: no row for an empty input under each grouping.
- **Fixtures**: `select-computed-not-null` recorded again, its `name_len_p` now `false`, `catalog`; a new `select-computed-outer-join`, nullable; a new `select-proved-forms`, whose capture holds an `operands`.
- **The corpus**: `RealisticNullabilitySafetyTests` and its oracle, unchanged, hold no wrong `false`; `RealisticNullabilityNoiseTests`' budgets fall with each commit that proves a kind.

## Where the code differs from the item

- The item proposes the computed column's correction "without an analysis of the query's text".  Fact 5 shows that would be unsound through an outer join, so decision 6 reads the text for a single table.
- The item says the other kinds need the query's text.  They need the text for their form, and the server for their operands (decision 3); no kind needs a parser of T-SQL.

## Decisions taken

The owner, 2026-10-23: "Approve, extras + (b) (Recommended)".

1. The kinds: the owner's list, as goals 1 and 2 hold, with arithmetic of operands that are not null and `GREATEST`/`LEAST` with an argument that is not null.
2. The sources: the text for the form, one new call `operands` for the operands, the catalog through `columns`' own text for the computed column, which is proved only when the `FROM` is that one table; showplan rejected.
3. `ANSI_WARNINGS`: the proofs of `COUNT`, `SUM`, a cast, `COALESCE` and arithmetic assume it on (decision 5), with a known issue on the manual's SQL Server page.

## Left to the builder

- The names and files of the reader, its tokens and the proofs, in a folder of their own under `src/SqlSource.Tool/Describing/SqlServer/`, which holds 17 files today.
- The order of commits: each kind its commit, with its budgets and recordings, so a commit never leaves a budget wrong.
