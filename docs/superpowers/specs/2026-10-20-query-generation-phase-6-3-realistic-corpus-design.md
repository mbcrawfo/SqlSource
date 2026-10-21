# Query generation, phase 6.3: the realistic T-SQL corpus and the describer's close - design

Date: 2026-10-20

Sub-phase 6.3 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It writes a corpus of realistic T-SQL queries, fresh for SQL Server, each with a hand-written truth of which columns can be `NULL`, over a schema and seed of its own, and checks the describer against it on SQL Server 2017 and 2025: no column the truth or the data shows can be `NULL` is recorded not null, and the over-reporting is counted against a budget.  Then it registers the describer in `ToolHost.Create`, and the released tool describes SQL Server.

It builds on sub-phase 6.2.  Sub-phase 6.5 builds on it and on 6.4.  It follows [the suite of realistic queries](2026-10-15-postgresql-realistic-query-suite-design.md), PostgreSQL's, in its method, and not in its queries.

## Intent

The owner's bar for SQL Server is PostgreSQL's, and the owner's rule for nullability is to rely on the server where it is accurate and to correct it in the describer only where it is proven wrong.  The spike found SQL Server right on every `NULL` it tried; the corpus is the proof at scale, on real application queries written the way T-SQL developers write them, and it is what would show a wrong `false` if there is one.  The owner chose a fresh corpus over a translation of PostgreSQL's 59 cases, so that it exercises T-SQL's own idioms.

## Goals

1. `tests/SqlSource.Tests.SqlServer/Realistic/`: 50 to 60 cases, each one `query.sql` and one `case.json`, over `Realistic/schema.sql` and `Realistic/seed.sql`, in the container's database `realistic`.
2. Each case's truth, written by hand from the SQL and the schema: for every column, whether it can be `NULL`, and why when it can.
3. The checks of PostgreSQL's suite, but its expectation: the truth against the description; the oracle, which runs each query over the seed and fails on a `NULL` in a column recorded not null; the seed's coverage of every nullable column; and the noise budget per major, 14 and 17.
4. A case the describer refuses names its id, and is checked to be refused with it.
5. Any wrong `false` the checks find is corrected in the describer, with a fixture of its own, in this sub-phase.
6. The describer registered in `ToolHost.Create`; the tool's readme and the manual say that SQL Server is described.

## Non-goals

- The cases as `Models` queries with committed sidecars, the comparison of each committed entry whole, the models' nullability against the truth, and the run through Dapper.  Sub-phase 6.6, which is where the description of each case is first recorded, so that no expectation file is written here to be retired later, as PostgreSQL's `expected-pg14.txt` and `expected-pg18.txt` were.
- Lowering the noise.  The budget measures it; reducing it is later work if the owner asks for it.
- A translation of PostgreSQL's cases.

## Anti-goals

From PostgreSQL's suite, and kept: a truth copied from the describer's output; a safety check that trusts a recording; toy queries that each exercise one feature; a case that passes on one version by accident.  And for this corpus: **a query a T-SQL developer would not write** to make a point of PostgreSQL's.

## What exists today

`tests/SqlSource.Tests.Postgres/Realistic/`: `RealisticCase`, `RealisticCases`, `RealisticSamples`, `RealisticDatabase` and their tests at its root; `Describer/` with `RealisticDescribe`, `RealisticNullabilityOracle`, `RealisticNullabilitySafetyTests`, `RealisticNullabilityNoiseTests` and `RealisticDescriptionTests`; `Generation/` with what reads the models.  `case.json`'s fields are `columns` (`name`, `nullable`, `why`, `exempt`), `samples`, `minimumMajor`, `refusal`, `checkedFrom`, `models` and `dapper`.  The noise budget is `{ 14: 42, 18: 40 }` and fails when the count is above or below it.

## Design

### The shared parts

The case's record and the rules every case follows, a nullable column has a `why`, `exempt` only on a nullable column, a refused case has no columns, a folder holds nothing else, and the arithmetic of the noise budget, are the same for both engines.  The builder moves what is engine-neutral of them into `tests/SqlSource.Testing`, where both projects compile it, rather than copying it, as `TD-0039` asks of the test projects; what reads a database stays in each project.  `minimumMajor` and `checkedFrom` take the engine's own majors: 14 and 17 for SQL Server.

### The schema and the seed

`Realistic/schema.sql`, applied with `GO` batches as sub-phase 6.1 applies a script, and `Realistic/seed.sql`, which only grows.  A small retail and billing application in the `dbo` and `billing` schemas: customers with an alias type for an email and nullable contact fields; a self-referencing category tree; products with a computed and a persisted computed column; orders with an identity key, a `rowversion` for optimistic concurrency, and nullable shipping fields; order lines; invoices and payments in `billing`, with `money` and `decimal`; a system-versioned price table; a sequence; a view over an outer join; and a table with a `uniqueidentifier` key and a `NEWSEQUENTIALID()` default.  The seed has, for every nullable column, a row that makes it `NULL`, and rows that make every outer join and empty group produce one.  Everything runs on 2017.

### The cases

Written fresh, as a T-SQL developer writes them.  The corpus covers, among what the builder chooses:

| Area | For instance |
|----|----|
| Paging and top | `OFFSET ... FETCH`, `TOP (@n) WITH TIES`, keyset paging on a `rowversion` or a key |
| Joins | `LEFT`, `RIGHT` and `FULL JOIN`; `CROSS APPLY` and `OUTER APPLY` over a `TOP 1`; a join to a view over an outer join; a self-join of the category tree |
| Aggregates | `GROUP BY` with `HAVING`; `COUNT(*)` with no group; `STRING_AGG`; `ROLLUP` and `GROUPING SETS` with `GROUPING()`; a scalar subquery of an aggregate |
| Windows | `ROW_NUMBER`, `RANK`, `LAG` and `LEAD`, a running `SUM`, `FIRST_VALUE` |
| CTEs | A recursive CTE over the tree; a CTE feeding an `UPDATE` |
| Writes | `INSERT ... OUTPUT inserted.*`; an upsert by `MERGE ... OUTPUT $action, inserted.*, deleted.*`; `UPDATE ... FROM ... JOIN ... OUTPUT`; `DELETE ... OUTPUT deleted.*`; `NEXT VALUE FOR` |
| Expressions | `ISNULL`, `COALESCE`, `NULLIF`, `IIF`, `CASE` with and without `ELSE`; `TRY_CONVERT`; `CONCAT` and `+` over nullable strings; date functions, `EOMONTH`, `DATEFROMPARTS`, `AT TIME ZONE` |
| Set operations | `UNION`, `UNION ALL`, `INTERSECT`, `EXCEPT` |
| JSON and XML | `OPENJSON ... WITH`, `JSON_VALUE`, `FOR JSON PATH` |
| Temporal | `FOR SYSTEM_TIME AS OF @at` and `ALL` |
| Filters | The optional filter `(@x IS NULL OR col = @x)`, `IN` lists, `EXISTS` and `NOT EXISTS`, `LIKE` with `ESCAPE` |
| Newer servers | A few cases of 2022 and 2025 syntax, `GREATEST`, `IS DISTINCT FROM`, `JSON_OBJECT`, a `json` column, with `minimumMajor` |

Two to four cases are refused, each with its id: a dynamic `EXEC(@s)`, `SQLSRC264`; a temporary table in two statements, `SQLSRC265`, which 6.2 reaches through `columns` after `parameters` fails; a parameter whose type cannot be deduced, `SQLSRC226`.  The ids are the describer's to give: a case that the describer refuses with another id records the id it gets only after the owner agrees that the describer is right.  A case whose model the generator will refuse, two columns of one name, is kept for 6.6, which marks it as PostgreSQL's 5.6 marks `two-columns-named-id`.

A sample is a set of T-SQL literals by parameter name, `{ "status": "N'active'" }`.

### The oracle

For each case and each of its samples, the oracle runs the query through `sp_executesql`, with the parameters declared at the types the describer gave them and the sample literals as their values, in a transaction it rolls back, and records which columns held a `NULL` in any row.  A write runs too, and its `OUTPUT` is read; the rollback leaves the seed as it was.  A case with no parameters runs once.

### The checks

| Check | Fails when |
|----|----|
| Safety against the truth | A column the truth calls nullable is described not null |
| The oracle | A column described not null held a `NULL`; or a column the truth calls not null held one, which is a wrong truth |
| The seed's coverage | A column the truth calls nullable, and not `exempt`, held no `NULL` in any run |
| The refusal | A case with `refusal` is not refused with that id, or one without is |
| The noise budget | The count of columns the truth calls not null and the describer calls nullable, over the cases that run on the major, differs from the budget of that major, `{ 14: n, 17: m }`, set by this sub-phase from the first full run |

Each runs on each major of `RecordedMajors` that the case's `minimumMajor` allows.  A description that differs between 2017 and 2025 is allowed here, since no expectation is recorded: the parameter types the spike found differ, `DATEADD`'s count among them, change no nullability.

### A wrong `false`

If a check finds a column described not null that can be `NULL`, the describer is corrected in this sub-phase, before registration: the narrowest rule that makes the column nullable, keyed on what the server's answer and the catalog hold, with a fixture case of its own and a `nullableSource` value that says which rule decided.  A value that is not in the format's list is added by the format's rules for an additive change.  The owner is told of each in the pull request.  A correction that is not small, one that needs an analysis of the query such as PostgreSQL's walk, stops the sub-phase and goes to the owner, as PostgreSQL's suite said.  A case of the first pull request that finds one is held back to the second, with its fix, so that the first passes.  The spike gives no reason to expect one.

### Registration

`ToolHost.Create` registers `SqlServerDescriber` beside PostgreSQL's.  `ToolHostTests` expects it.  `DescribeCommandTests` gains SQL Server's: the whole command through `Cli.RunAsync` against the container over a copy of a fixture project with an `mssql` file, and `ToolProcessTests` the built tool once.

## Testing

The checks above, as container tests in `Realistic/Describer/`; the shared case rules' tests where the rules now live; `ToolHostTests`; the command's tests.  The suite stays out of `nightly.yml` with its project.

## Documentation

- `tests/SqlSource.Tests.SqlServer/README.md`: adding a case, writing a truth, the samples, and setting the budget, as PostgreSQL's README says them.
- `src/SqlSource.Tool/README.md`'s first paragraph and `docs/manual/tool.md`'s "Which queries are described": SQL Server is described.  `docs/manual/sqlserver.md` gains what describing needs of the database and the connection: the versions tested, `TrustServerCertificate` for a local container, the first result set of a batch, the parameter types the server widens and the `-- param:` that narrows them, and that a parameter used once alone in `@p IS NULL` is typed `int`.  `docs/manual/dialects.md`'s support table and its callout, the README's status line and every sentence of the phase map that says PostgreSQL alone is described, to the extent that describing is what they speak of; the models are 6.4's.  `docs/manual/diagnostics.md`: `SQLSRC216` no longer lists `mssql`.
- `docs/architecture/`: the corpus as the describer's proof, and any correction.
- The epic outline: the row of 6.3.

## Order of work

Two pull requests, each one commit per step, since the cases and their truths are the bulk and are reviewed apart from the code:

1. The outline's row says In progress.  The shared parts moved; the schema, the seed, the oracle, the checks, and the first half of the cases.
2. The second half of the cases, the budget, any correction, the registration, the documents, and the row set to Done.

Each counts its files against the limit of 150 before it starts.

## Left to the builder

- The cases, the schema's details and the seed's values, within the areas above.
- What of PostgreSQL's realistic code moves to `tests/SqlSource.Testing`.
- The budget's numbers, from the first full run.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- No expectation of the description is recorded in this sub-phase; 6.6 compares each committed sidecar entry whole, as 5.7 does for PostgreSQL.
- The oracle binds samples through `sp_executesql` with the described types, where PostgreSQL's substitutes cast literals into the text.
- The describer is registered at the end of this sub-phase, after the corpus, where PostgreSQL's was registered before its corpus existed.
- The noise is measured and not reduced.

## Version

The pull requests check `VersionPrefix` against the release tags, as `AGENTS.md` requires.
