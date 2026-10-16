# A suite of realistic queries for the PostgreSQL describer - design

Date: 2026-10-15

## Why

The tests of the PostgreSQL describer are deep on its mechanisms and thin on the SQL people write.  The 60 fixture cases under `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/` and the container tests under `tests/SqlSource.Tests.Postgres/Describer/` each probe one feature with a query of one to three tables against a four-table schema.  No test combines three or four features the way a hand-written application query does: a CTE with an aggregate joined back to its table, a top-1-per-group `LATERAL`, a `row_number()` dedupe, an upsert with `RETURNING`.

The users this matters to write their SQL by hand.  The user who asked for this cares about two kinds of surprise equally: the tool refusing or crashing on valid SQL, and the tool accepting it and getting the answer quietly wrong, above all a column recorded `nullable: false` that the server can return `NULL` for.  Dapper leaves a member at its default when a column is `NULL`, so that one is a silent wrong value in the user's program.

A throwaway spike on 2026-10-15 ran 44 such queries against the test container and executed each one over seeded data built to produce `NULL`s.  It found no crash and no wrong `false`.  It found three refusals.  One of them is confusing: `WHERE placed_at > now() - @age` fails with SQLSRC225 because the server types `@age` as `timestamptz` from its first use and then reports `operator does not exist: timestamp with time zone > interval`, an operator the user never wrote.  It also found about 40 of roughly 130 columns recorded nullable or unknown where the server can never return `NULL`.  The spike's seed, queries and output are in [its appendix](2026-10-15-postgresql-realistic-query-suite-spike.md) and are the starting point of this suite; its test class was not kept.

This suite is the safety net for the companion design, [the PostgreSQL describer's nullability noise](2026-10-15-postgresql-nullability-noise-design.md), which makes the describer answer `false` more often.  Every such change is the kind that could start giving a wrong `false`, and this suite is what proves it does not.

## Goals

- A corpus of at least 40 realistic, hand-written queries, each described against a real server on PostgreSQL 14 and 18 in CI, with its description pinned.
- A check that cannot be fooled by a recorded expectation: each query is executed over seeded data, and a column recorded `nullable: false` that returns `NULL` fails the test, whatever the expectation says.
- A hand-written truth for every column of every query, "can this column be `NULL`", against which the describer's answers are scored: a `false` where the truth is "can" fails the test, and a nullable or unknown where the truth is "cannot" counts as noise.
- One number, the noise budget: the count of noise columns over the whole corpus.  The companion design lowers it.
- A help line for SQLSRC225 that names the likely cause when the server rejects a query over a type it inferred for a parameter.

## Non-goals

- Lowering noise.  That is the companion design's.  This suite records the describer's noise as it is when the suite lands.

The one exception is a bug the suite finds: a column recorded `false` that can be `NULL`, or a describe that crashes.  It is fixed in the same work, with the case that found it as its test.  A fix that turns out large stops the work, and the builder asks the user before going on (the user's decision).
- Replaying the corpus without a server.  The cases are not recorded as captures under `Fixtures/Describers/`; they run only in `tests/SqlSource.Tests.Postgres`, which `nightly.yml` leaves out.  (My call: replay would add the corpus to the Windows and macOS runs at the cost of a second recording path, and the risk this suite covers is the server's behaviour, which replay cannot see.)
- Other engines.
- Performance of the describer.

## Anti-goals

These would make the work a failure even if every test passes:

- A safety check that trusts the describer's own recording.  The oracle and the truth must be independent of `expected` files: a recording that captures a wrong `false` must still fail.
- Truth that was copied from the describer's output.  The truth is written by reasoning about the SQL and the schema; where the person writing it is unsure, the column is "can be `NULL`", and a comment says why.
- A corpus of the existing fixtures' shape.  A query that probes one feature with two tables adds nothing; each query should be one that a person might plausibly have in an application.
- Changing the shared database `fixtures` or `schema.sql`.  Other container tests assert row counts and OIDs there; the corpus has a database of its own.
- A test that passes on one PostgreSQL version by accident of the other's plan.  An expectation that differs by version says so explicitly.

## The corpus

### Database

A second database in the container the collection `UsesPostgres` already starts, created once for the run, named `realistic` (my call), with its own schema file and seed file.  `tests/SqlSource.Tests.Postgres/PostgresContainer.cs` stays the one place that names the image; whether it also creates this database or a fixture of the suite does is the builder's call.

The schema is richer than `schema.sql` where the corpus needs it, and as small as it can be otherwise.  It needs at least:

- A few tables with foreign keys and a mix of `NOT NULL` and nullable columns, including a domain, an enum, an array, a `jsonb` and a range column.
- A self-referencing table, for trees and recursive CTEs.
- A partitioned table, for a partition on each side of an outer join.
- A view over an outer join.
- A second schema.

The seed data is built so that each nullable path is exercised: a parent with no children, a child whose optional reference is `NULL`, a nullable column holding `NULL` in some rows and a value in others, a partition with no matching rows, a `jsonb` document missing a key.

### Queries

One query per case.  Each case is its own folder in a new folder of `tests/SqlSource.Tests.Postgres` (the name is the builder's), holding the query, the sample values of its parameters, the truth of its columns, and the recorded expectation.  The exact file layout is the builder's; a person must be able to read one case in one place, and the expectation must be a separate file that a recording tool writes.

The corpus covers each of the following at least once, and most of them in combination with others.  Every query of the spike is in it, in some form.

| Area | Shapes |
|----|----|
| Unions | `UNION ALL`; `UNION` with `ORDER BY` and `LIMIT`; `INTERSECT`; `EXCEPT`; branches of different types (`integer` and `bigint`, `varchar(n)` and `text`); a union inside a CTE joined to a table; a branch that is a literal |
| CTEs | Recursive over the tree table; a CTE used twice (planned apart by the server); `MATERIALIZED` and `NOT MATERIALIZED`; a chain of CTEs; a CTE with `GROUP BY` joined back to its table, inner and left; a data-modifying CTE, `WITH d AS (DELETE ... RETURNING *) INSERT ... SELECT ... FROM d RETURNING ...` |
| Subqueries | A scalar subquery in the select list; `IN (subquery)`; `EXISTS` as a column; a derived table with `DISTINCT`; the `row_number()` dedupe, `WHERE rn = 1` |
| `LATERAL` | `CROSS JOIN LATERAL (... LIMIT 1)` for the top row per group; `LEFT JOIN LATERAL ... ON true`; `unnest(...) WITH ORDINALITY`; `jsonb_array_elements` |
| Joins | Three or more tables with a mix of inner and left; `RIGHT` and `FULL`; a self-join on the tree; a join to the view; the partitioned table on each side of a left join, with a parameter that prunes at run time |
| Grouping | `GROUP BY` with `HAVING`; `FILTER`; `ROLLUP`; `DISTINCT ON` |
| Windows | `sum() OVER`, `lag`, `rank`, a named `WINDOW` |
| Expressions | `count(*)`, `coalesce` with a constant last, `CASE` with an `ELSE`, `greatest`, literals, `IS NULL` as a column, `jsonb_build_object`, `json_agg` with `FILTER`, `array_agg`, `string_agg` |
| Writes | Upsert, `INSERT ... ON CONFLICT ... DO UPDATE ... RETURNING`; `INSERT ... SELECT ... RETURNING`; multi-row `VALUES`; bulk insert from `unnest(@a::int[], @b::numeric[])`; `UPDATE ... FROM (aggregate subquery) RETURNING` a column of the subquery; `DELETE ... USING ... RETURNING` a column of the other table; `MERGE ... RETURNING` on 17 and later |
| Parameters | `= ANY(@ids)` of an integer and of an enum; `LIMIT @n OFFSET @m`; `ILIKE '%' \|\| @q \|\| '%'`; the optional filter, `(@p::t IS NULL OR c = @p)`; a range built from two parameters; a parameter inside a CTE; `jsonb @> @filter` and `? @key` |
| Statements | `TABLE t`; `VALUES (...), (...)` |
| Refusals | `array_agg(ROW(...))`, which is SQLSRC229; `now() - @age` with no cast, which is SQLSRC225 with the new help; a parameter compared with two types, which is SQLSRC225 |

A case may name the oldest major version it runs on, for `MERGE ... RETURNING`, which PostgreSQL 17 added, and is skipped with that reason on an older server (my call).

A query that the describer refuses records the failure's id and asserts the refusal; it is part of the corpus so that a change that makes it pass, or fail differently, is seen.

Every query that writes runs in a transaction that is rolled back, for the oracle as the describer's own `EXPLAIN` already does.

## The checks

Each case runs these, on whatever server the run has, so on 14 and on 18 in CI.  They are listed in the order of how much they matter.

1. **Safety against the truth.**  No column whose truth is "can be `NULL`" is recorded `nullable: false`.  This reads the description and the truth, never the expectation.
2. **The oracle.**  The query is executed over the seed data, with each parameter replaced by its sample value, and every row is read.  No column recorded `nullable: false` holds a `NULL` in any row.  The column count and the column names of the result equal the description's.
3. **Seed coverage.**  Every column whose truth is "can be `NULL`" holds a `NULL` in at least one row of the oracle's run, so the truth is shown and not only claimed.  A column the seed cannot make `NULL`, or not on every version, is exempt by a line in the case that says why; the builder keeps exemptions rare.  Every column whose truth is "cannot be `NULL`" holds no `NULL`, which catches a truth written wrong.
4. **The expectation.**  The description equals the recorded one: result kind, parameters with their types, columns with their names, types, `nullable`, `nullableSource` and origin, `plan` and `tableMatch`.  Where 14 and 18 differ, the expectation records both, explicitly, by the server's major version.
5. **The noise budget.**  A column whose truth is "cannot be `NULL`" and which is recorded `true` or `null` is noise.  One test sums the noise over the corpus and compares it with a number held in the test.  The number is the corpus's count when this lands, and is lowered whenever the describer improves.  It is raised only in a change that adds cases, and the commit says by how much and why.  The test fails when the count is above the number, and also when it is below, with a message that says to lower the number, so that an improvement is locked in by the change that makes it.  Where the count differs between 14 and 18 the test holds one number per version.

The sample values replace each parameter as a literal cast to the type the describer recorded for it, `('10')::bigint`, so the oracle runs the same SQL the server described.  The spike did it this way and it worked for every case.

A case may hold more than one set of sample values, and the oracle runs once for each; check 3 is taken over all the runs together (my call).  The truth of a column treats a parameter as the run does: it can be `NULL` only when the case's `-- param:` marker says `null`, since saying nothing means `not null` (the epic's rule).  So `SELECT @x::int AS x` is "cannot be `NULL`" unless `@x` is declared `null`, and a case that declares one gives a set of sample values with it `NULL`.

Checks 4 and 5 compare against the records of 14 and of 18 alone.  On any other major version, which a developer can run through `SQLSOURCE_TESTS_POSTGRES_IMAGE`, they are skipped with a message that says so, and checks 1 to 3 run as everywhere (my call).  The recording tool writes the record of the image it runs on, so recording both is two runs.

### Recording

A tool in the shape of `tools/record-fixtures.sh` writes every case's expectation from a run against a container, for the image of the tests or the one the environment names, and a person reads the diff before committing it.  Recording never writes the truth.  Whether it is a second script or an option of the first is the builder's call.  `CONTRIBUTING.md` says how to record and when.

## The help of SQLSRC225 for an inferred parameter

When the server rejects a query at the step `describe parameters` with `42883` (no such operator or function), `42804` (a datatype mismatch) or `42725` (an ambiguous operator or function), and the query has at least one parameter without a declared type, the failure's help says that the server gave each such parameter the type of its first use, which may not be the one meant, and to cast it where it is used to the type meant, `@age::<type>`, or declare its type with `-- param: @age <type>`: the tool cannot know the type meant, so the help writes `<type>` and not a guess, which here would be `interval`.  It names the undeclared parameters, as written, in the order of the query's list.  It does not claim to know which one is at fault.

The id stays SQLSRC225 and the message stays the server's.  `docs/diagnostics.md` says it in SQLSRC225's section, and a test in `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs` and a case of the corpus pin it.

## Success

- The corpus runs in `tests/SqlSource.Tests.Postgres` on 14 and 18 in CI, and `./pre-commit-validation.sh` passes.
- Checks 1 to 3 pass for every case, with no exemption that lacks a reason.
- The noise budget holds the count of the corpus as it lands, about 40 if the spike is a guide, and the test's message tells the next person how to lower it.
- `now() - @age` fails with SQLSRC225 and the new help.
- `tests/AGENTS.md` and `CONTRIBUTING.md` say where the corpus is, how to record it, and that its truth is written by hand.

## Left to the builder

- The folder's name, the files of a case, and their formats.
- How the database `realistic` is created and seeded once for the run.
- The exact queries, within the table above and the anti-goals.
- Whether a case is one xunit theory row or a test of its own.
