# Query generation, phase 6.2: the SQL Server columns and table match - design

Date: 2026-10-20

Sub-phase 6.2 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It completes what the SQL Server describer says of a query: its columns with their types, nullability, origins, identity and computed flags; the table match, against the server's own description of each origin's table or view; and SqlClient's own log in the run's log.  The describer is still registered nowhere; sub-phase 6.3 registers it once the realistic corpus has checked it.

It builds on sub-phase 6.1.  Sub-phase 6.3 builds on it.  What the servers answered is in [the spike of phase 6](2026-10-20-query-generation-phase-6-sql-server-spike.md), "the spike" below.

## Intent

PostgreSQL needed a plan walk because its protocol says nothing of a column's nullability through a join.  SQL Server says it, and the spike found it right everywhere a `NULL` can come out.  The owner's rule is to rely on what the server says when it is accurate and to correct it in the describer only where a test proves it wrong, so this sub-phase records the server's answer as given and builds no walk.  What the table match needs, a table's full column list and the nullability its columns have when read whole, is asked of the server the same way.

## Goals

1. Each column of a query's first result set, from `sys.dm_exec_describe_first_result_set` in browse mode 2, with its type, its nullability as the server gives it, `nullableSource` `server`, its origin, and its identity and computed flags.
2. `matchesTable` and `tableMatch`, by the six checks of the format, against the server's description of `SELECT *` from the origin's table or view.
3. Each failure the procedure reports is a failure at the query with one id for each cause a user mends differently.
4. SqlClient's event source into the run's log when a log is written, with nothing of the connection in it.
5. The fixtures re-recorded with columns, and the cases of this sub-phase.

## Non-goals

- Correcting the server's nullability.  Sub-phase 6.3's oracle decides whether any correction is needed; this sub-phase has no layer but the server.
- Reducing the server's over-reporting, `COUNT(*)` and the computed columns the spike found nullable.  It is noise, safe, and measured by 6.3's budget; reducing it is later work, if the owner wants it.
- A query's second result set.  The owner deferred it as `D-0006`; this sub-phase describes the first, as the procedure does, and the manual says so.
- Registering the describer.  Sub-phase 6.3.

## Anti-goals

- **A column recorded not null that the server called nullable.**  The describer never narrows the server's answer.
- **A hidden column in a model.**  Browse mode's hidden columns are dropped before anything is built.
- **A driver message that names the host, the user or the password** in the log.

## What exists today

As of the merge of 6.1: the describer types parameters and answers `ResultKind.None` with no columns; the fixtures under `Describers/mssql/` are statements without rows.  PostgreSQL's `TableMatcher` holds the six checks in the format's order, with the nullability check asking PostgreSQL's `ColumnNullability`; `NpgsqlRunLogger` writes `postgres.driver` events through the describer's `SessionRunLog`, and only when `IRunLog.Writes`.

## Design

### The calls of this sub-phase

| Method | Request | Answer |
|----|----|----|
| `columns` | `sql`, the sample SQL as written; `params`, every parameter of the query's list declared at its described type | `rows`, every row of `sys.dm_exec_describe_first_result_set(@sql, @params, 2)` with `is_hidden`, `column_ordinal`, `name`, `is_nullable`, `system_type_name`, `max_length`, `precision`, `scale`, the user type's schema, name and assembly-qualified name, `source_schema`, `source_table`, `source_column`, `is_identity_column`, `is_computed_column`, `error_number`, `error_type` and `error_message`; or `error` |
| `table` | `schema` and `table` of one origin | The rows of `sys.dm_exec_describe_first_result_set` for `SELECT * FROM <schema>.<table>`, in mode 2, as `columns` reads them; or `error` |

`columns` is asked for every query, after the parameters are typed, since the procedure needs every parameter declared.  `table` is asked once for each distinct origin, sorted, when the query has rows.  Its text is the tool's, each name a catalog name the server gave, put in brackets with `]` doubled, and never the user's text.  A query whose `parameters` failed with 11501 asks `columns` too, with the declared parameters alone, before its failure is reported, below.

### Columns

1. Rows with `is_hidden = 1` are dropped: browse mode 2 adds one, `ROWSTAT`.
2. A row that holds an error makes the query fail, below.  No row is `ResultKind.None`, the answer for a statement with no result set: an `UPDATE` with no `OUTPUT`, a `SELECT ... INTO`, an `OUTPUT ... INTO`.
3. Each other row is a column: `name` as the server returned it, a `!` or `?` suffix kept; `ordinal`; `type` by `SqlServerTypes`; `nullable` the server's `is_nullable`, `nullableSource` `server`; `origin` the source's schema, table and column when the three are given, `source_server` is null and `source_database` is the session's database, which mode 2 gives as the view for a view's column and gives for no column of a `FOR SYSTEM_TIME` query; `identity` and `computed` the server's flags, null where it gives none.  A column of another database, a linked server or the system resource, `master.sys.databases`, has no origin: the sidecar's `origin` has no database, and the table match must not find a table of that name in the session's database.
4. A column whose `name` is null, `SELECT 1`, is `SQLSRC266`.  Two columns of one name are recorded, and the generator's `SQLSRC255` reports them, as for PostgreSQL.

Browse mode 2, not the outline's 1: mode 1 names a view's base table as the origin and adds hidden key and `rowversion` columns of every table the query reads, which differ between versions on a system view; mode 2 names the view, as PostgreSQL's origin does, and adds one hidden column.  Nullability, identity and computed are the same under both.

### The table match

`SqlServerTableMatcher` runs the format's six checks in order, the first failing check being `tableMatch`, as PostgreSQL's does: `no-origin`, `several-tables`, `names-differ`, `columns-differ`, `order-differs`, `nullability-differs`.  The table's column list, and each column's nullability, are the server's description of `SELECT *` from it, hidden rows dropped: so a period column declared `HIDDEN` is out of the list as `SELECT *` leaves it out, a computed column is in it, and a column is the table's in nullability when `nullable` equals what the server says of it read whole.  That is the rule PostgreSQL's match follows, what a plain query records for the column, and it is what the spike requires: the server judges a computed column by its expression, so a `PERSISTED NOT NULL` one may be nullable in every query, and against the catalog's `is_nullable` no table with one would ever match.  Names are compared ordinally.  A view may match, as on PostgreSQL.  `matchesTable` is the origin's schema and table.  `plan` is `skipped` on every entry with rows, as the format says for an engine with no walk.

### Failures

By `error_type`, never by message:

| `error_type` | Cause | The tool reports |
|----|----|----|
| 3, `CONFLICTING_RESULTS` | Two code paths return different shapes | `SQLSRC263` |
| 4, `DYNAMIC_SQL` | `EXEC` of a string the server cannot read | `SQLSRC264` |
| 10, `TEMPORARY_TABLE` | A temporary table in a batch of several statements | `SQLSRC265`, with `help:` to make the query one statement or use a table variable |
| 8, `UNDECLARED_PARAMETER` | Unreachable: every parameter is declared | `SQLSRC225`, so that it is seen and reported |
| 3, 4 or 10 in the answer of the `columns` asked after `parameters` failed with 11501 | `sp_describe_undeclared_parameters` reports a temporary table of a batch of several statements as 208 and 11501, where this procedure gives its cause | That row's id; any other answer leaves the failure `SQLSRC225` with `parameters`' lines |
| Any other | The query is not valid against this schema: 334, an `OUTPUT` on a table with a trigger; 207, an unknown column; 102, a `GO` inside the query; 2812, a word read as a procedure | `SQLSRC225` with every error row as a `server:` line, in the server's order |

The step is `describe columns`, or `table match` for `table`.

### Driver logging

When the run's log writes, the session enables an `EventListener` on SqlClient's event source, `Microsoft.Data.SqlClient.EventSource`, for its lifetime alone: the run describes one database at a time.  What is written is an allowlist, not a filter: an event of a name on the list is written as `mssql.driver` through the describer's `SessionRunLog`, with `category`, the event's name; `level`; `eventId`; and only the payload members the list names for it, those that cannot name the server, the database, the user or a password, the command's text and an error's number among them; and `exception`, null.  An event not on the list, and every free-form trace string, is not written, so a message SqlClient adds in a later release is dropped until someone lists it.  The builder makes the list from the events of the keywords that show the commands sent and which failed, as the outline decides, on a run of the container tests.  Every string still passes the session's redaction, which is the connection's value and its password.  A run without a log enables nothing.

### Events

`mssql.columns` and `mssql.table` with their requests and answers whole; one `mssql.column` for each column with its origin, or why it has none; `mssql.table-match` with `table`, a map of `schema` and `table`, or null, and `tableMatch`; `mssql.driver`.

### Diagnostics

In `ToolDiagnostics`, at the step `describe columns`:

| Id | Message |
|----|----|
| `SQLSRC263` | "The query '{0}' returns rows of different shapes on different paths" |
| `SQLSRC264` | "The query '{0}' runs SQL that the server cannot read until it runs" |
| `SQLSRC265` | "The query '{0}' uses a temporary table in a batch of several statements" |
| `SQLSRC266` | "Column {0} of the query '{1}' has no name" with `help:` to give it an alias |

### The fixtures

Every case of 6.1 is recorded again, since every query now asks `columns`.  New cases:

| Case | Proves |
|----|----|
| `select-left-join` | Nullability through an outer join, origins, identity |
| `select-view-star` | A view as origin, and `matched` to the view |
| `select-table-star` | `matched` to a table with a computed column and a `rowversion` |
| `select-hidden-period` | A system-versioned table whose period columns are `HIDDEN`: `matched` without them |
| `select-temporal-all` | `FOR SYSTEM_TIME ALL`, `no-origin` |
| `select-aggregates` | `COUNT(*)`, `MAX`, a `GROUP BY` key |
| `select-alias-and-clr` | An alias type column and a `hierarchyid` column |
| `select-computed-not-null` | A table with a `PERSISTED NOT NULL` computed column that the server calls nullable, `matched` |
| `select-other-database` | A column of `msdb` and one of `master.sys.databases`: no origin, `no-origin` |
| `insert-output` | `OUTPUT inserted.*`, `matched` to its table |
| `merge-output` | `MERGE` with `WHEN NOT MATCHED BY SOURCE` and `OUTPUT` of `$action`, `inserted` and `deleted` |
| `select-first-result-set` | `SELECT ...; SELECT ...`: the first result set alone |
| `select-names-differ`, `select-columns-differ`, `select-order-differs`, `select-nullability-differs`, `select-several-tables` | Each failing check of the match |

`json` and `vector` need 2025: their case is a container test that skips below 17, not a fixture, since every fixture is described on both servers; it checks that the server gives `json` and `vector(n)` to SqlClient, as the spike's probe found.

## Testing

| Where | Cases |
|----|----|
| `SqlServerColumnsTests` (tool tests) | From rows made by the test: hidden rows dropped; no rows is `None`; each field; a null `name` is `SQLSRC266`; an origin of another database or server dropped |
| `SqlServerTableMatcherTests` (tool tests) | Each check in order from columns and tables made by the test; two failing give the first; a view matches; a computed column nullable in both matches; names compared ordinally |
| `SqlServerErrorsTests` | Each row of the failure table; the `server:` lines in order; an 11501 of `parameters` classified by `columns` |
| `ColumnTests` (container) | Every row of the spike's column and DML tables, under mode 2 and through SqlClient, on both servers, as the spike found; a row that answers otherwise is recorded as it answers and the spike's appendix corrected, and a row that fails under mode 2 where mode 1 answered is brought to the owner before the build goes on; `json` and `vector` on 2025 |
| `FailureTests` (container) | A temporary table in two statements is `SQLSRC265`; `EXEC(@s)` is `SQLSRC264`; an `IF` of two shapes is `SQLSRC263` |
| `TableMatchTests` (container) | Each check against the schema |
| `SafetyTests` (container) | `columns` of a text that drops a table, of one that waits, and of one that shuts the server down: the table is there, the call returned at once, the server answers |
| `DriverLogTests` (container) | With a log, `mssql.driver` events are written and none names the host, the user, the database or the password; a refused connection and a rejected login write none of them; without a log, no listener is enabled |
| `FixtureCasesTests`, `DescriberFixtureTests` | Every case |
| `SecretTests`, `RedactionTests`, `EntrySecretCheckTests` | The new events and the driver's |

## Documentation

- `docs/architecture/` SQL Server describer page: columns, browse mode, origins, the table match, driver logging.
- `src/SqlSource.Tool/AGENTS.md`: the hidden rows, browse mode 2, origins of another database dropped, the `table` call's text built from catalog names alone, no walk and `plan: skipped`, and the listener's allowlist and scope.
- `docs/manual/diagnostics.md`: `SQLSRC263` to `SQLSRC266`, and `SQLSRC225`'s SQL Server causes.
- The sidecar format design's `name` row of SQL Server: SqlClient 6.1 and later receive `json` and `vector(n)`, so the tool records them so; `varchar(max)` is what an older client sees.
- `docs/deferred/D-0006`'s "Delivered instead" names the code that describes the first result set.
- `docs/tech-debt`: `TD-0034` if a driver event on the list could still name something.
- The epic outline: the row of 6.2.

## Order of work

One pull request, one commit for each step.

1. The outline's row of 6.2 says In progress.
2. `columns`, the columns, origins, the failures and `SQLSRC263` to `SQLSRC266`; the fixtures recorded again.
3. `table`, `SqlServerTableMatcher`, `matchesTable`, `tableMatch` and `plan`.
4. Driver logging.
5. The events, the new cases and the safety tests.
6. The documents, and the outline's row set to Done.

The plan counts the files against the limit of 150: about twenty cases of three files and the fixtures recorded again.  If the count goes over, the cases go in a second pull request after the code.

## Left to the builder

- The SqlClient keywords, level and the allowlist, by the rule above.
- The cases' SQL, within the table.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- Browse mode 2, not 1, so that a view is its columns' origin as on PostgreSQL.
- The table match reads a table's column list and nullability from the server's description of `SELECT *`, not from `sys.columns`.
- An origin outside the session's database is dropped.
- Driver logging is an allowlist of events and members.
- A column without a name is the tool's error, `SQLSRC266`, since the sidecar's `name` is a string, where PostgreSQL's `?column?` reaches the generator.
- A dynamic `EXEC` the server reads, `EXEC(N'...')` of a literal, and a procedure's first result set, `EXEC dbo.proc`, are recorded as the server describes them and are neither refused nor tested; the outline's "stored procedures are not called to find their shape" holds, since nothing is called.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.
