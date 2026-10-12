# Query generation, phase 3.3: columns, the catalog and the gate - design

Date: 2026-10-12

Sub-phase 3.3 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It gives the PostgreSQL describer its columns: their types with facets, their origins from the catalog, the nullability the catalog can answer, the outer-join gate with the heuristic as its only answer until the walk arrives, and the check that Npgsql reads the query's parameters as the lexer does.  After it, a description is complete but for `matchesTable` and the walk, and still reaches no sidecar: the describer is registered in sub-phase 3.6.

It builds on sub-phase 3.2.  Sub-phases 3.4 and 3.5 build on it, independently of each other.

## Goal

```sql
-- name: GetUserOrders
SELECT u.id, u.email, o.total, count(*) OVER () AS n
FROM users u LEFT JOIN orders o ON o.user_id = u.id
WHERE u.id = @id;
```

| Column | `type.name` | `nullable` | `nullableSource` | `origin` |
|----|----|----|----|----|
| `id` | `integer` | `true` | `heuristic` | `public.users.id` |
| `email` | `public.email_t` | `true` | `heuristic` | `public.users.email` |
| `total` | `numeric(18,2)` | `true` | `heuristic` | `public.orders.total` |
| `n` | `bigint` | `null` | `no-origin` | `null` |

The same query without the join: `id` is `false` with `catalog`, `email` `true` with `catalog`, and `plan` is `not-needed`.

Success is:

- Every column has the type the server reports, spelled as PostgreSQL spells it, and a column that comes from a domain says so.
- A column's nullability is never `false` unless the catalog says `NOT NULL` and no outer join can have touched it.
- A query whose parameters Npgsql would read differently from the lexer is an error now, not a wrong result at run time.

## Decisions

From the epic, unchanged: three layers of nullability, the origin, the gate and the walk, with the safety net that a gated query the walk cannot answer makes every column nullable; the provenance values of the sidecar format; `record` and `unknown` as a column type are unsupported with the column named; a column's name is recorded as the server returned it, suffix included; the tool checks the lexer against Npgsql and reports a disagreement at the query.

Changed from the epic by the spike, and recorded in the outline:

| Decision | Choice | Why |
|----|----|----|
| How columns are described | `EXECUTE <name>(NULL, ...)` under `CommandBehavior.SchemaOnly`, on the statement sub-phase 3.2 prepared | One prepared statement serves the parameters, the columns and, in sub-phase 3.5, the plan.  The epic planned a `SchemaOnly` describe of the original SQL with parameters typed from `DeriveParameters`. |
| `CommandBehavior.KeyInfo` | Not used | It makes Npgsql join `pg_attribute` for each column.  The tool runs one catalog query of its own over every table the columns name, which the table match needs anyway and which gives `attidentity`, `attgenerated` and `relkind` too. |
| A domain column | The origin's domain, when the column has an origin whose attribute type is a domain whose base chain reaches the type the protocol reported; else the protocol's type | The row description flattens a domain to its base type with the domain's modifier, so only the catalog knows a column is a domain.  Both answers are the server's; the one that names the domain is the more useful, and an expression over a domain says the base type, which is also true. |

Settled in this spec:

| Decision | Choice | Why |
|----|----|----|
| Where facets come from | `NpgsqlDbColumn.ColumnSize`, `NumericPrecision` and `NumericScale`, taken only for the internal types that carry each | Npgsql does not expose a column's raw type modifier.  Its three numbers are the modifier decoded, and the container tests compare the spelled name of every column with an origin against `format_type` from the catalog. |
| A `NumericScale` of 1024 or more | The scale less 2048 | PostgreSQL 15 allows a negative scale and stores it in eleven bits; Npgsql reads sixteen, so `-3` arrives as `2045`. |
| A `NumericPrecision` of 65535 on `interval` | No precision | An interval without a precision and with field restrictions, `interval year to month`, has that modifier.  The restriction itself is lost, `docs/tech-debt` records it. |
| `plan` for a gated query in this sub-phase | `unavailable` | The walk does not exist yet, which is what the value says.  Sub-phase 3.5 makes it `walked`. |
| The gate's words | `LEFT`, `RIGHT`, `FULL`, `OUTER`, as whole words ignoring case, in `Text` lexemes | The lexer already knows what is a string, a comment or a quoted identifier.  `left(name, 3)` gates a query that has no join, and the walk then finds nothing to null-extend, which costs one `EXPLAIN` and nothing else. |
| What the Npgsql check compares | The count, the names and the type OIDs of the columns Npgsql's describe of the original SQL gives, against the `columns` answer | If Npgsql finds a placeholder the lexer did not, the server sees `@x` and rejects it or types it otherwise; if it finds fewer, it leaves a parameter unreferenced, which is harmless.  Equal columns prove the SQL the server saw was the same. |
| A type name Npgsql will not bind | Its own id, `SQLSRC232` | Phase 8's generated code binds by `DataTypeName` with the sidecar's spelling; a name Npgsql rejects now would fail there.  It is not a disagreement about the parameters. |

## Out of scope

- The table match: `matchesTable` is `null` and `tableMatch` is `no-origin` in this sub-phase.  Sub-phase 3.4.  The fixtures recorded here that have rows are re-recorded there.
- The walk.  Sub-phase 3.5.
- The modifier of a type outside `pg_catalog`, PostGIS's `geometry(Point,4326)` for one: recorded without it, in `docs/tech-debt`.
- The facets of an array's element, `character varying(10)[]`: the element is recorded without them, in `docs/tech-debt`.

## The calls of this sub-phase

| Method | Request | Answer |
|----|----|----|
| `columns` | `statement`, `parameters`: the count | `columns`: for each, `ordinal`, `name`, `typeOid`, `tableOid`, `attributeNumber`, `length`, `precision`, `scale`, the last three as Npgsql gives them or null; or `error` |
| `tables` | `oids`: the distinct table OIDs of the columns, sorted | `tables`: for each, `oid`, `relkind`, `schema`, `name`, and `columns`: every attribute with `attnum > 0` that is not dropped, in `attnum` order, each with `attnum`, `name`, `notNull`, `identity`, `generated`, `typeOid` |
| `check` | `sql`: the sample SQL with the trailing `;` dropped and `@name` as written; `parameters`: for each of the query's list, `name` and `dataTypeName` | `columns`: `name` and `typeOid` of each; or `error`; or `driverError`: Npgsql's own message for a name it cannot bind |

`types` of sub-phase 3.2 is asked once per query, after `tables`, for the OIDs of the parameters, of the columns and of the origin attributes together.

## `DescribeAsync`

After step 3 of sub-phase 3.2, `prepare`:

4. **`columns`.**  Zero columns is `ResultKind.None`: the description is the parameters alone, as before, and steps 5 to 8 are skipped.
5. **`tables`**, for the distinct `tableOid` values that are not zero.  A column whose `tableOid` is not zero and whose table or attribute the answer lacks is a bug, and throws.
6. **`types`**, for every OID of the parameters, the columns and the origin attributes.
7. **The columns**, each in order:
   - *Type.*  The origin attribute's type when the column has an origin, the attribute's type is a domain, and following `typbasetype` from it reaches the column's `typeOid`; else the column's `typeOid`.  Built by `PostgresTypes.Build` with the column's facets, which land on a base type or on a domain's `base`.
   - *Facets*, from the answer: `length` for `bpchar`, `varchar`, `bit`, `varbit`; `precision` for `numeric`, `time`, `timetz`, `timestamp`, `timestamptz`, `interval`, with 65535 on `interval` read as none; `scale` for `numeric`, with 1024 or more read as negative.  Any other type: none.
   - *A pseudo-type*, `typtype` `p`: `SQLSRC229` for the first such column, and the describe ends.
   - *Origin.*  `schema`, `table`, `column` from the tables answer; `null` when `tableOid` is zero.  `identity` is whether `attidentity` is not empty, `computed` whether `attgenerated` is not empty; both `null` with no origin.
   - *Nullability*, by the layers below.
8. **`check`**, as under The Npgsql check.

`ResultKind.Rows`, the columns, `matchesTable` `null`, `tableMatch` `no-origin`, and `plan` as the gate says.

### Nullability in this sub-phase

| The column | `nullable` | `nullableSource` |
|----|----|----|
| No origin | `null` | `no-origin` |
| Origin in a view, `relkind` `v` or `m` | `null` | `view` |
| Gated query, origin in a table | `true` | `heuristic` |
| Origin in a table, `notNull` | `false` | `catalog` |
| Origin in a table, not `notNull` | `true` | `catalog` |

`OuterJoinGate.IsGated(lexemes)` is the gate: a `Text` lexeme holds one of the four words, bounded by characters that are not identifier characters by the lexer's rule.  `plan` is `not-needed` for a query that is not gated and `unavailable` for one that is; sub-phase 3.5 gives the gated rows their real answer.

### The Npgsql check

`check` describes the original SQL as a consumer's code will send it: a `NpgsqlCommand` with the sample SQL, one `NpgsqlParameter` for each parameter of the query's list, named as the list names it, `Value` `DBNull.Value`, and `DataTypeName` the `name` of the type the parameter got in step 6; `ExecuteReaderAsync(CommandBehavior.SchemaOnly)`; the columns' names and OIDs.  Npgsql rewrites `@name` to `$n` itself here, and ignores a parameter the SQL does not reference, which a declared-only parameter is.

| The answer | Is |
|----|----|
| Columns equal in count, names and OIDs to the `columns` answer | The check passes |
| Columns that differ, or `error` | `SQLSRC231`, with what differs: `Npgsql finds N parameters and the lexer M`, or `column 3 is text for Npgsql and integer for the lexer`, or the server's lines |
| `driverError` | `SQLSRC232`, naming the type and the parameter |

A query without parameters is checked too: it costs one `Parse`, and the comparison still proves the two describes saw one query.

### Events

| Event | Members |
|----|----|
| `postgres.columns` | `query`, `statement`, `columns`: the count, or `error`, `durationMs` |
| `postgres.tables` | `oids`, `tables`: the count, `durationMs` |
| `postgres.check` | `query`, `parameters`: name and type name of each, `result`: `same`, `differ` or `unbound`, and what differs, `durationMs` |
| `postgres.column` | One for each column: `ordinal`, `name`, `type`: the spelled name, `origin`, `nullable`, `nullableSource`, `gated` |

### Diagnostics

| Id | Title | Message | Arguments |
|----|----|----|----|
| `SQLSRC229` | Column type is not supported | `The column '{0}' of the query '{1}' has the type '{2}', which cannot be described` | The column; the query; the type's `format_type` |
| `SQLSRC231` | Parameters as Npgsql reads them differ | `Npgsql reads the parameters of the query '{0}' differently from SqlSource: {1}` | The query; what differs |
| `SQLSRC232` | Type cannot be bound by Npgsql | `Npgsql cannot bind the type '{0}' of the parameter '{1}' of the query '{2}'` | The type's name; the parameter; the query |

`SQLSRC229` has a `help:` line: cast the column to a concrete type, or give the expression an alias and a cast.  `SQLSRC231` has one: a `@` that one of the two reads as a parameter and the other as text; the server's lines say where.  `SQLSRC232` has one: report it, since the sidecar's spelling is what generated code will use.  The `step:` is `describe columns` for `columns`, `catalog` for `tables`, `resolve type` for a pseudo-type, and `check parameters` for `check`.

## Fixtures

Added under `Fixtures/Describers/postgres/`, each with rows, and the four of sub-phase 3.2 re-recorded:

| Case | Proves |
|----|----|
| `select-plain` | Every type of `schema.sql` as a column, facets included; `catalog` for `NOT NULL` and nullable; a domain column; identity and generated columns |
| `select-expressions` | An aggregate, a function, a cast, a literal, `NULL`, `?column?`, a `USING` column of an inner join with its origin: `no-origin` where there is none |
| `select-view` | Every column `view` |
| `select-left-join` | Every origin column `heuristic`, `plan` `unavailable`, `n` from an aggregate `no-origin` |
| `select-suffix-names` | `AS "deleted_at!"` and `AS "name?"` recorded as returned |
| `select-other-schema` | `billing.invoices`, the origin's schema |
| `insert-returning` | `RETURNING` as a select list |
| `select-declared-domain` | A `-- param:` of a domain, the Npgsql check passing with `public.email_t` |

## Order of work

One pull request, one commit for each step.  Each leaves `./pre-commit-validation.sh` passing and brings its tests and the documents it makes wrong.

1. The epic outline: the row of 3.3 says In progress.
2. `columns`, `ResultKind`, and the types of the columns with their facets and spelled names.
3. `tables`: origins, identity, computed, and the domain through the origin.
4. Nullability by the catalog, `OuterJoinGate`, and the heuristic.
5. `SQLSRC229`.
6. `check`, `SQLSRC231` and `SQLSRC232`.
7. The events.
8. The fixtures, recorded, and the four of sub-phase 3.2 re-recorded.
9. The documents, and the outline's row set to Done.

## Testing

| Where | Cases |
|----|----|
| `OuterJoinGateTests` (tool tests) | Each word, in each case; inside a string, a comment, a quoted identifier and a dollar-quoted string: not gated; `LEFTOVER` and `x_left`: not gated; `left(` : gated |
| `ColumnFacetsTests` (tool tests) | Each type with each facet from an answer made by the test; 2045 to -3; 65535 on interval to none; a facet on a type that does not carry it is dropped |
| `ColumnTypeTests` (tool tests) | The domain through the origin; a domain over a domain; an origin whose type is not a domain; a domain whose base does not reach the column's type: the column's own |
| `ColumnNullabilityTests` (tool tests) | Each row of the table |
| `ColumnTests` (container) | Every type of `schema.sql`, the spelled name of each column with an origin equal to `format_type(atttypid, atttypmod)` from the catalog; `numeric(2,-3)` and `interval day to second(3)` and `interval year to month` as columns, on 15 and later for the first; a domain column, `COALESCE` of it, `NULL::email_t` and a cast; `SELECT 'x'`, `NULL`, `ROW(1,2)` is `SQLSRC229`; `?column?`; two columns with one name; a `USING` column of a `FULL JOIN` has no origin and of an inner join has the left one; a `UNION`; a subquery and a CTE keep origins; a view; identity and generated; the suffix names; `INSERT ... RETURNING`; `UPDATE` without `RETURNING` is `None` |
| `NpgsqlCheckTests` (container) | Every case of `ParameterTests` passes the check; a declared-only parameter passes; a `@` in a dollar-quoted string passes; a query without parameters passes; `DataTypeName` accepted for each type of the schema, arrays and the domain included |
| `FixtureCasesTests`, `DescriberFixtureTests` | The new cases, and the old ones re-recorded |

`SQLSRC231` and `SQLSRC232` cannot be made to happen from a `.sql` file with the lexer and Npgsql as they are; their paths are held by tool tests over a replaying exchange whose `check` answer the test writes.

## Documentation

- `src/SqlSource.Tool/AGENTS.md`: the order of the calls in a describe and why `types` comes after `tables`; the domain through the origin; where facets come from and the two decodings; the gate's words and that `plan` says `unavailable` until the walk; the Npgsql check and what each of its ids means.
- `docs/diagnostics.md`: `SQLSRC229`, `SQLSRC231` and `SQLSRC232`.
- `docs/tech-debt`: the modifier of a type outside `pg_catalog`; the facets of an array's element; the field restriction of an interval.
- `src/SqlSource.Tool/README.md`, the repository's `README.md`, `CONTRIBUTING.md`, `docs/publishing.md`: nothing changes.
- `docs/deferred`: nothing is expected.
- The epic outline: in steps 1 and 9.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.  When this spec was written the repository had no `v*` tag.
