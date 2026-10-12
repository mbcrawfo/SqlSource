# Query generation, phase 3.4: the table match - design

Date: 2026-10-12

Sub-phase 3.4 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It gives the PostgreSQL describer the table match: whether a query's result is exactly one table's column list, so that phase 5 can name the model after the table and share it between every query that matches the table.  The describer stays unregistered until sub-phase 3.6.

It builds on sub-phase 3.3, and is independent of sub-phase 3.5: the two can be built beside each other.

## Goal

```sql
-- name: GetUser
SELECT * FROM users WHERE id = @id;
-- name: ListUsers
SELECT id, name, email, status, tags, balance, created_at, deleted_at, name_upper FROM users;
-- name: GetUserNames
SELECT id, name AS user_name FROM users;
```

| Query | `matchesTable` | `tableMatch` |
|----|----|----|
| `GetUser` | `{ "schema": "public", "table": "users" }` | `matched` |
| `ListUsers` | the same | `matched` |
| `GetUserNames` | `null` | `names-differ` |

Success is:

- Two queries that read the whole of one table, by `*` or by its list in order, say the same table, so that phase 5 gives them one model.
- A result that differs from the table in any way phase 5's model would show, a name, a column, the order, a nullability, matches nothing, and `tableMatch` names the first check that failed.

## Decisions

From the epic, unchanged: the result is exactly a table when every column has an origin in that one table, the names are the table's column names unchanged, the columns are the table's full column list in the table's order, and each column's nullability is the table column's; `tableMatch` is the first failing check in the format's fixed order, `matched`, `no-origin`, `several-tables`, `names-differ`, `columns-differ`, `order-differs`, `nullability-differs`; the tool compares the result against the catalog after describing.

Settled in this spec:

| Decision | Choice | Why |
|----|----|----|
| What "the table column's nullability" is | `false` for a `NOT NULL` column of a table, `true` for a nullable one, and `null` for every column of a view | It is what the catalog says of the column, which is what sub-phase 3.3's `catalog` and `view` layers record.  A column the heuristic or the walk made `true` over a `NOT NULL` origin then differs, and the model is not the table's, which is right: its nullability is not the table's. |
| A view | May match, as a table may | The epic's rule is about a relation's column list.  A view's columns are relations' columns, and a `SELECT *` from a view is the view. |
| A column selected twice, `SELECT id, id, name FROM t` | `columns-differ` | The columns are not the table's list; the check compares the lists, not the sets. |
| A name with an override suffix, `AS "name!"` | `names-differ` | The name as the server returned it is not the table's, and the suffix changes the nullability the model will have. |
| Where the table's column list comes from | The `tables` answer of sub-phase 3.3, which already lists every attribute of every table the columns name | No new call. |

## Out of scope

- The model's name, the `no-table-models` parameter, and sharing the model.  Phase 5, which reads `matchesTable`.
- A match across several tables, or of a subset of a table.  Not a table model by the epic's definition.

## The match

`TableMatcher.Match(columns, tables)` in `src/SqlSource.Tool/Describing/Postgres/`, over the columns as sub-phase 3.3 built them and the `tables` answer, called once per query with rows after the columns are built and, when sub-phase 3.5 is in, after the walk, since the walk sets the nullability the last check reads.  It returns the `SidecarTable` or null, and the `tableMatch` value.  The checks, in order, the first that fails deciding:

| Check | Fails when | `tableMatch` |
|----|----|----|
| 1 | A column has no origin | `no-origin` |
| 2 | The origins name more than one table OID | `several-tables` |
| 3 | A column's name is not its origin attribute's name, compared ordinally | `names-differ` |
| 4 | The columns' origin attributes, as a list, are not the table's attribute list as a set: a missing one, an extra one, or one twice | `columns-differ` |
| 5 | The columns' attribute numbers are not ascending | `order-differs` |
| 6 | A column's `nullable` is not the origin attribute's: `!notNull` for a table, `null` for a view | `nullability-differs` |
| None | | `matched`, with the table's schema and name |

The table is one relation, so a query over a table and a view that selects everything from each cannot match: check 2.

A statement without rows has no match: `matchesTable` and `tableMatch` are absent, as the format says.

### Events

| Event | Members |
|----|----|
| `postgres.table-match` | `query`, `result`: the `tableMatch` value, `table`: `schema.name` or null |

### Diagnostics

None: the match is provenance, never an error.

## Fixtures

Added under `Fixtures/Describers/postgres/`, and the cases of sub-phase 3.3 with rows re-recorded, since each now has a real `tableMatch`:

| Case | Proves |
|----|----|
| `match-star` | `SELECT *`, `matched` |
| `match-explicit-list` | The full list in the table's order, `matched` |
| `match-view-star` | `SELECT *` from the view, `matched` |
| `match-alias` | One column aliased, `names-differ` |
| `match-suffix` | `AS "name!"`, `names-differ` |
| `match-missing-column` | One column left out, `columns-differ` |
| `match-twice` | One column twice, `columns-differ` |
| `match-reordered` | Two columns swapped, `order-differs` |
| `match-expression` | An added expression, `no-origin` |
| `match-two-tables` | A join selecting both tables whole, `several-tables` |
| `match-left-join-nullability` | A table's full list on the nullable side of a `LEFT JOIN` with the other table unselected, `nullability-differs` under the heuristic, and under the walk once sub-phase 3.5 is in |

## Order of work

One pull request, one commit for each step.  Each leaves `./pre-commit-validation.sh` passing and brings its tests and the documents it makes wrong.

1. The epic outline: the row of 3.4 says In progress.
2. `TableMatcher` with its checks, and `DescribeAsync` filling `matchesTable` and `tableMatch`.
3. The event.
4. The fixtures, and the re-recording.
5. The documents, and the outline's row set to Done.

## Testing

| Where | Cases |
|----|----|
| `TableMatcherTests` (tool tests) | Each row of the table of checks from columns and tables made by the test; two checks failing at once give the first; a view with every column `null` matches; a view with one column `true` from the walk is `nullability-differs`; a table with a `NOT NULL` column made `true` by the heuristic is `nullability-differs`; a nullable column that is `true` matches; a table of one column; a column name compared ordinally, `Name` and `name` differ |
| `TableMatchTests` (container) | Each fixture case's query live; `SELECT *` from `billing.invoices` gives its schema; a table with a dropped column: `SELECT *` matches, since the dropped attribute is not in the list |
| `FixtureCasesTests`, `DescriberFixtureTests` | The new cases, and the re-recorded ones |

## Documentation

- `src/SqlSource.Tool/AGENTS.md`: the match runs after the walk, since it reads the nullability the walk sets; a view may match; the checks' order is the format's and is not to be reordered.
- `docs/diagnostics.md`, `src/SqlSource.Tool/README.md`, the repository's `README.md`, `CONTRIBUTING.md`, `docs/publishing.md`: nothing changes.
- `docs/tech-debt` and `docs/deferred`: nothing is expected.
- The epic outline: in steps 1 and 5.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.  When this spec was written the repository had no `v*` tag.
