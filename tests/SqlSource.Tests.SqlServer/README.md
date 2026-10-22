# SqlSource.Tests.SqlServer

This project runs the tests of SQL Server against a real server, which Testcontainers starts in Docker.  It proves the container itself: that the image the run names is pinned to its update and runs the build that is recorded for it, and that the fixture schema applies.  `Describer/` holds the tests of the SQL Server describer that only a server can answer: opening a session and what each refusal of it reports (`SessionTests`), the types the server gives a query's parameters, its errors and the canaries that prove a text runs nothing (`ParameterTests`), the columns it describes under browse mode 2, as the spike of phase 6 found them (`ColumnTests`), the failures of a query whose columns cannot be recorded (`FailureTests`), the table match against the schema (`TableMatchTests`), the canaries that prove `columns` runs nothing, waits for nothing and shuts nothing down (`SafetyTests`), SqlClient's events in the run's log, with nothing of the connection in them (`DriverLogTests`), and the describer's fixture cases, described against the server and held to the build they were recorded on (`FixtureCasesTests`, `FixtureVersionTests`).  `Realistic/` holds [the realistic corpus](#the-realistic-corpus): each case is a query with a truth, written by hand, of which of its columns can be `NULL`, and the describer's nullability is checked against the truth and against the query run over a seed.

## Running

The project needs Docker running, and its first run pulls the image.  It starts one container for the run and creates two databases on it: `fixtures`, with `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/schema.sql`, and `realistic`, for [the realistic corpus](#the-realistic-corpus), with its `schema.sql` and then its `seed.sql`.  It applies each file through `SqlServerScripts`, which splits the file at each line that holds `GO` alone, ignoring case and the white space around it, and runs each batch as one command of SqlClient.  SqlClient runs with `QUOTED_IDENTIFIER` on, where `sqlcmd` has it off and refuses the persisted computed columns of both schemas.  A `GO` with a count is refused.

```bash
dotnet test --project tests/SqlSource.Tests.SqlServer
```

`dotnet test --solution SqlSource.slnx`, and so `pre-commit-validation.sh`, runs it on the default image.  SQL Server 2017 is the oldest that is supported, which CI runs as well:

```bash
SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/SqlSource.Tests.SqlServer
```

A change to these tests, or to the SQL Server describer, is run on both images before it is pushed.

SQL Server's images are built for x64 alone.  On Apple silicon Docker runs them under emulation: the first run takes minutes while the image is pulled, and a start with the image already pulled takes seconds.

## The images

The default image is `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04`, named for the tests and the scripts in one place, `DefaultImages.SqlServer` in `tests/SqlSource.Testing/DefaultImages.cs`.  The variable `SQLSOURCE_TESTS_SQLSERVER_IMAGE` names another, and the oldest that is supported is `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`, the floor, whose tag is also in `.github/workflows/build.yml`, `CONTRIBUTING.md` and this readme.

Every SQL Server image the tests run names its update: `<year>-CU<n>`, with `-GDR<n>` when the build has one, then `-ubuntu-<version>`.  It never names `-latest`, a bare year or an on-demand hotfix, which move to a new build without a word, so the tests would run a server the recordings never saw.  `SqlServerContainer.ImageBuilds` maps each of the two images to the build, `SERVERPROPERTY('ProductVersion')`, that its server reports, recorded when the tag was chosen.  `SqlServerContainerTests` fails on a tag of another form, on an image that is not in the map, and on a server whose build differs from the map's.

Moving a tag is a change of its own: change the tag and its row of `ImageBuilds`, with the build that the new image reports, in the same pull request.  Moving the default image also records again, on the new image, everything that was recorded on it, and the diff is read before it is committed.  Nothing is recorded on the floor: moving it changes its tag in the places above and runs the tests on it.

## The fixtures

The fixture schema is `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/schema.sql`: tables of `dbo` and of a schema `billing`, an identity, a default, a `rowversion`, a computed and a persisted computed column, an alias type, one named by a keyword, `dbo.[User]`, with the table `dbo.accounts` of a column of it, and a table type, a view over an outer join, an inline and a multi-statement table-valued function, a table with an `AFTER` trigger, a system-versioned table whose period columns are hidden, the table `dbo.victim`, which a test that proves a text runs nothing looks for afterwards, and `dbo.all_types`, with one `NOT NULL` column of every type that SQL Server 2017 and 2025 share.  The describer's fixture cases are in the folders beside it.

### The describer's fixtures

The describer's fixtures are in `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/`: a folder for each case with its `query.sql`, the `capture.json` of describing it and the `expected.json` of its sidecar, whose `toolVersion` is `fixture` so that a new version of the tool touches no case.  `tests/SqlSource.Tool.Tests` replays each from its capture with no database, and this project describes each against the container and compares the entry, so a describer that asks something else fails the first and one that answers something else fails the second.  Both the capture and the sidecar carry the build of the server they were recorded on, which `FixtureVersionTests` holds to the build of the default image in `ImageBuilds`; `FixtureCasesTests` compares an entry whatever the build, so CI describes the cases on the floor image as well, where the answers are the same.  After a change to what the describer asks or answers, or to `schema.sql`, record them again and read the diff before it is committed:

```bash
tools/record-fixtures.sh mssql
```

To add a case, add its folder with a `query.sql` and run the script.  A recorded file is never edited by hand.  Nothing is recorded on the floor image.

## The realistic corpus

`tests/SqlSource.Tests.SqlServer/Realistic/` holds the corpus and what reads it: the cases in `Realistic/Cases/`, each a folder with `query.sql` and `case.json` (the truth) and no other file.  No case's description is recorded yet: a later change adds each case's sidecar beside its query, and the models built from it.  The `realistic` database is built from `Realistic/schema.sql` and `Realistic/seed.sql`.  The root of `Realistic/` also holds `RealisticCases`, which reads the cases through `RealisticCaseReader` of `tests/SqlSource.Tool.Testing/Realistic/`, each a `RealisticCase` of that folder, whose `SkipUnlessRunsOn` and `SkipIfRefused` are how a test skips a case, `RealisticSamples`, which builds the batch that runs a case's query with a sample, and `RealisticDatabase`.  What both engines' corpora share, the case and its reader, the checks of a sample, the columns a description must have and the arithmetic of the noise budget, is in that folder of `tests/SqlSource.Tool.Testing`, and its tests are in `tests/SqlSource.Tool.Tests/Realistic/`.

### The database

`SqlServerContainer` creates `realistic` beside `fixtures` and applies `schema.sql`, then `seed.sql`, through `SqlServerScripts`, once for the run.  It stands apart from `fixtures`, so a change to it moves no capture of the describer's fixtures.  Its tables are of a shop: customers, a tree of categories, products, stock, orders and their lines, carts, prices with their history, and in a schema `billing` the invoices and their payments, with an alias type, a sequence, computed columns, a system-versioned table and a view over an outer join.

Every batch of `schema.sql` runs on SQL Server 2017.  What a later major brings is made by a batch that runs its text through `EXEC` only when `SERVERPROPERTY('ProductMajorVersion')` is high enough, so that 2017 never parses it: `dbo.product_specs`, with a column of 2025's `json`, is made on 17 alone.  The seed's values are fixed and its ids are given, so that a truth and a sample can name a row; its header says which row holds each nullable path of the schema.  The seed only grows: a case may add rows, and none is removed, since another case's truth or sample may stand on it.  `RealisticDatabaseTests` checks that every table has rows, that `dbo.product_specs` is there on 2025 alone, and that `fixtures` was left as it was.

### The checks

`Realistic/Describer/` holds the describer's checks over the corpus.  `RealisticDescribe` describes each case as a run does, in a session of its own on `realistic`, once in a run, and every check reads that one description.  A check runs a case on each server that its `minimumMajor` allows.

- `RealisticDescriptionTests` fails on a case that is refused otherwise than its `refusal` says: described when it says a refusal, refused by another id, or refused when it says none.  It also fails on a description whose columns differ from the truth's in number, or in the name at a position.
- `RealisticNullabilitySafetyTests` fails on a column that the describer calls not null and the truth says can be `NULL`.  It then runs the query over the seed once for each sample through `RealisticNullabilityOracle`, and fails on a `NULL` in a column that the describer or the truth calls not null, on a column that can be `NULL`, with no `exempt`, that held none for any sample, on a column the server names otherwise than the description, on a sample that names no parameter of the query, and on a sample that gives `NULL` to a parameter whose `-- param:` marker does not say `null`.
- `RealisticNullabilityNoiseTests` holds the count of columns that are not null and that the describer answers as nullable or unknown to a budget for each recorded major, 14 and 17, in its `Budgets`, as [the budget](#the-noise-budget) says.
- `RealisticNullabilityOracleTests` checks the oracle itself: that a query that writes leaves the seed as it was, that a `NULL` in a row is seen, that a sample is bound, an expression that ends in a comment too, that a parameter of an alias type is declared as that type and bound, and that a query that fails throws its own error.

The oracle runs a query in a transaction that it rolls back, and reads every row of its first result set, as the describer describes the first.  So a case may write: an `INSERT`, `UPDATE`, `DELETE` or `MERGE` with an `OUTPUT` is read as a `SELECT` is, and the seed stays as it was.

`RealisticCasesTests`, which fails on a folder of `Cases/` that cannot be read as a case or holds another file, `RealisticSamplesTests` and the tests of `tests/SqlSource.Tool.Tests/Realistic/` need no server.

### A case

A `case.json` holds its `columns`, in the order of the query's, each with its `name`, whether it is `nullable`, a `why` when it is, and an `exempt` when no seed can show its `NULL`.  It can also say:

- `samples`, one or more sets of a T-SQL expression for each parameter, by its name without `@`, compared ignoring case.  The oracle declares a variable of each parameter at the type the describer gave it, set to the expression, and passes it to the query through `sp_executesql`, so a sample is bound as a caller's value is and is never put into the query's text.  An expression is allowed where a literal is, `DATEADD(day, -30, '2024-11-01')` as well as `N'%a%'`, and it may end in a comment.  Every parameter has a value in each sample.  `"NULL"` is the one way to write a null, and only for a parameter whose `-- param:` marker says `null`, since generated code sends no null to any other.  A case with no parameters has no samples and runs once.
- `minimumMajor`, when the case needs a later server than SQL Server 2017, in SQL Server's major versions: 14 for 2017, 16 for 2022 and 17 for 2025.  A case of 2022's syntax, such as `GREATEST` or `IS DISTINCT FROM`, says 16, and so runs on 2025 alone of the two recorded majors.  The checks leave it out on an earlier server.
- `refusal`, the id of the failure, when the describer refuses the case.  The case then has no columns, and the checks of its nullability skip it.

`models`, `checkedFrom` and `dapper` are of a case's models and its sidecar, which no case here has yet.

A column that can be `NULL` says why, and an `exempt` says why the seed cannot show it, so it needs a reason and belongs only on a column that can be `NULL`.  `RealisticCaseReader`, through which `RealisticCases` reads each case, refuses a `case.json` that breaks either, that has both a `refusal` and `columns`, or that has a member it does not know.  Every folder of `Cases/` is a case.

### Adding a case

To add a case, add its folder with a `query.sql`, a SqlSource file of one query, and a `case.json`, which holds the truth, written from the SQL and `schema.sql` and never from what the describer answers.  A `why` says how the `NULL` comes, and names the rows of the seed that show it.  When a column of the truth can be `NULL` and no sample shows it over the seed, add a row to `seed.sql` that makes it so, or a sample that does; `exempt` is for a `NULL` no seed can show.  Run the project on both images, since `pre-commit-validation.sh` runs the default image alone.

SQL Server returns no row for a grouping set `()` over empty input, `ROLLUP`'s grand total among them: only an aggregate with no `GROUP BY` returns its one row over no rows.  So every row of a `ROLLUP` or of `GROUPING SETS` aggregates at least one row, and a `SUM` or `MAX` of a column that is not null is not null in it.

### The noise budget

Noise is a column that the truth calls not null and the describer answers as nullable or unknown.  It is safe, since the generated type is wider than it need be, and the budget keeps its cost in view.  `Budgets` of `RealisticNullabilityNoiseTests` holds one budget for each of `SqlServerContainer.RecordedMajors` and no other, which a test of its own checks, and the count over the cases that run on a major must be that major's budget exactly.  On a major that is not recorded the test skips.

A count under the budget fails, and the budget is lowered in that change, so the improvement is kept.  A count over it fails too, and is fixed in the describer, unless the change adds cases: then the budget is raised to the count, and the commit says by how much and why.  No description of a case is committed to diff, so the failure lists the noisy columns, each by its case, its position and its name, to read against the truths.
