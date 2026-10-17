# TD-0049 - A describe behind a pooler can ask another backend

## Problem

The PostgreSQL describer prepares a statement and asks about it in later calls, each a transaction of its own ([`PostgresSession.DescribePreparedAsync`](../../src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs)).  Behind a pooler in transaction mode, such as PgBouncer, each call can reach another backend.  The read of `prepare` matches the statement by its name and the text of the `PREPARE`, so a backend that lacks it, or holds a statement of the same name that an earlier run left there, is [`SQLSRC236`](../manual/diagnostics.md#sqlsrc236) at `describe parameters`.  What remains is `columns` and `explain`:

- `columns` describes `EXECUTE sqlsource_<n>`.  On a backend that lacks the statement the server answers no row description and no error, so the describe goes on with no columns, and `check`, which describes the SQL itself, reports [`SQLSRC231`](../manual/diagnostics.md#sqlsrc231): `the result has 1 columns for Npgsql and 0 for SqlSource`.  On a backend that holds a statement of the same name from an earlier run, it describes that statement's columns.  `check` compares their count, names and types, so a different statement is `SQLSRC231` too, but one with the same names and types and other origins would give the columns the other statement's nullability.
- `explain` plans `EXECUTE sqlsource_<n>` and can reach the same kind of backend.  `26000` is `SQLSRC236`; a stale statement of the same name gives the plan of that statement, which the walk reads for this one.

A try against PgBouncer 1.26 in transaction mode, with eight clients of `pgbench` sharing the pool, saw both cases of `columns` in about one of four failing runs, beside `SQLSRC236` for the other queries of the run, and never saw `26000` at `columns`.

## Why it exists

The describer needs one session, and [`docs/manual/postgresql.md`](../manual/postgresql.md#connecting) and `SQLSRC236` say to connect directly.  `columns` cannot tell an empty answer from a statement without rows, and nothing short of asking the backend again before each call says which backend a call reached.  What can be told is refused as `SQLSRC236`: `Multiplexing=true` at `open`, a statement that is not listed, `42P05` and `26000`.

## Impact

A stale plan matters to more queries since part A of the [nullability noise design](../superpowers/specs/2026-10-15-postgresql-nullability-noise-design.md): a query with a column of no origin is planned too, and the walk reads an expression of the plan's `Output`, a `count(*)` for one, as this query's.  So before it reads a plan the walk checks that the root's `Output` fits the described columns ([`NullabilityWalk`](../../src/SqlSource.Tool/Describing/Postgres/Walk/NullabilityWalk.cs)): each entry that is a reference the walk maps, for a column whose origin is a table, names a column of the origin's name.  A plan that fails is read for no expression, with the reason `output-differs`: a query that is not gated is `unavailable` and keeps every column as it was built, and a gated one's columns with an origin are walked as before, since column aliases, `users u(a, b)`, print a column under another name and fail the check too.  The check catches a stale plan whose references name other columns.  It does not catch one whose references agree, one of expressions alone, `SELECT count(*), 'x'`, whose `count(*)` the walk would read for this query's, or one whose references are to a `Subquery Scan` or a `CTE Scan`, which print the subquery's names for its columns and are left out of the check since part B; the walk resolves such a column by its origin in the scanned plan, so a stale plan there must also hold a reference of the origin's table and column.  Since part C a root that reaches a set operation and prints no `Output`, a `UNION ALL`'s `Append`, has no entry to check: a stale plan of that shape answers this query's columns of no origin from its branches, where only the row description's types must agree, and `relations` is asked for the relations that stale plan scans.

Low.  It needs a pooler in transaction mode, which is unsupported and documented as such, and other queries of the same run then fail as `SQLSRC236`.  An empty answer is always caught by `check`, with a message that names the wrong cause.  A wrong nullability needs a statement of the same name left by an earlier run, with the same column names and types and other origins, on the backend the call reached.

## Proposed fix

Run the calls of one describe in one transaction, which a pooler in transaction mode keeps on one backend: `PREPARE`, the read, the describe of `EXECUTE`, the `EXPLAIN` under a savepoint with its `SET LOCAL`s, and `DEALLOCATE` before the end, rolled back whatever happened.  Or, smaller: when `columns` answers no columns and `check` finds some, report `SQLSRC236` and not `SQLSRC231`; that leaves the stale statement.

## Trigger

A user's report of `SQLSRC231` or a wrong nullability behind a pooler, or a decision to support poolers in transaction mode.
