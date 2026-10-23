# SqlSource.Tests.SqlServer

This project runs the tests of SQL Server against a real server, which Testcontainers starts in Docker.  It proves the container itself: that the image the run names is pinned to its update and runs the build that is recorded for it, and that the fixture schema applies.  `Describer/` holds the tests of the SQL Server describer that only a server can answer: opening a session and what each refusal of it reports (`SessionTests`), the types the server gives a query's parameters, its errors and the canaries that prove a text runs nothing (`ParameterTests`), the columns it describes under browse mode 2, as the spike of phase 6 found them (`ColumnTests`), the failures of a query whose columns cannot be recorded (`FailureTests`), the table match against the schema (`TableMatchTests`), the proofs that a column cannot be `NULL`, each held over rows that tempt a `NULL`, beside the queries that try to break them, each run to show the `NULL` the server foresaw, and the facts of the server they stand on (`ProofTests`), the canaries that prove `columns` runs nothing, waits for nothing and shuts nothing down, and that `operands` runs nothing (`SafetyTests`), SqlClient's events in the run's log, with nothing of the connection in them (`DriverLogTests`), the whole `describe` command with the describers of the released tool, in process (`DescribeCommandTests`) and as the built tool in a process of its own (`ToolProcessTests`), and the describer's fixture cases, described against the server and held to the build they were recorded on (`FixtureCasesTests`, `FixtureVersionTests`).  It is also the end-to-end project of SQL Server, which runs the generator when it builds: in `EndToEnd/`, [the end-to-end project](#the-end-to-end-project), the generator builds models from the project's own queries with committed sidecars, the tool's `--check` finds those sidecars current, and Dapper reads real rows into the models.  `Realistic/` holds [the realistic corpus](#the-realistic-corpus): each case is a query with a truth, written by hand, of which of its columns can be `NULL`, and the describer's nullability is checked against the truth and against the query run over a seed; each case that is not refused is also a `Models` query with a committed sidecar, whose generated model is held to the truth and read through Dapper ([the models](#the-models)).

## Running

The project needs Docker running, and its first run pulls the image.  It starts one container for the run and creates four databases on it: `fixtures`, with `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/schema.sql`, `realistic`, for [the realistic corpus](#the-realistic-corpus), with its `schema.sql` and then its `seed.sql`, and `app` and `billing`, the end-to-end databases, with the `schema.sql` and then the `seed.sql` of `EndToEnd/app/` and `EndToEnd/billing/`, and on SQL Server 2025 or later `EndToEnd/app/new-types.sql`, the table of `json` and `vector`, after them.  It applies each file through `EndToEndFiles` of `tests/SqlSource.Testing`, which splits the file at each line that holds `GO` alone, ignoring case and the white space around it, and runs each batch as one command of SqlClient, on one connection.  SqlClient runs with `QUOTED_IDENTIFIER` on, where `sqlcmd` has it off and refuses the persisted computed columns of the schemas of `fixtures` and `realistic`.  A `GO` with a count is refused.

The project copies two fixture projects to its output, with the package's MSBuild files and `global.json`: `SqlServer`, from `tests/SqlSource.Tool.Tests/Fixtures/Projects/`, and `ErrorPaths`, from `EndToEnd/ErrorPaths/`.  The one file of `SqlServer` is of the dialect `mssql` and names the database `fixtures`.  `DescribeCommandTests` runs the whole `describe` command in process over a copy of it, on the real MSBuild, and `--check` after it, and `ToolProcessTests` starts the tool that was built in a process of its own and describes a copy of it.  `ErrorPathTests` describes copies of `ErrorPaths`, [below](#the-end-to-end-project).

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

The default image is `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04`, named for the tests and the scripts in one place, `DefaultImages.SqlServer` in `tests/SqlSource.Testing/DefaultImages.cs`, and repeated in `examples/SqlServerExamples/docker-compose.yml`.  The variable `SQLSOURCE_TESTS_SQLSERVER_IMAGE`, `EngineImage.SqlServer.Variable`, names another, which `EngineImage.SqlServer.Image` gives the tests, and the oldest that is supported is `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`, the floor, whose tag is also in `.github/workflows/build.yml`, `CONTRIBUTING.md` and this readme.

Every SQL Server image the tests run names its update: `<year>-CU<n>`, with `-GDR<n>` when the build has one, then `-ubuntu-<version>`.  It never names `-latest`, a bare year or an on-demand hotfix, which move to a new build without a word, so the tests would run a server the recordings never saw.  `SqlServerContainer.ImageBuilds` maps each of the two images to the build, `SERVERPROPERTY('ProductVersion')`, that its server reports, recorded when the tag was chosen.  `SqlServerContainerTests` fails, on `EngineImage.SqlServer.Image`, on a tag of another form, on an image that is not in the map, and on a server whose build differs from the map's.

Moving a tag is a change of its own: change the tag and its row of `ImageBuilds`, and for the default image its tag in `examples/SqlServerExamples/docker-compose.yml` too, with the build that the new image reports, in the same pull request.  Moving the default image also records again, on the new image, everything that was recorded on it: [the describer's fixtures](#the-describers-fixtures), with `tools/record-fixtures.sh mssql`, and the sidecars of the end-to-end project, of the realistic cases and of `examples/SqlServerExamples`, with `tools/describe-end-to-end.sh`, as in [writing the sidecars](#writing-the-sidecars).  The diff is read before it is committed.  Nothing is recorded on the floor: moving it changes its tag in the places above and runs the tests on it.

## The fixtures

The fixture schema is `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/schema.sql`: tables of `dbo` and of a schema `billing`, an identity, a default, a `rowversion`, a computed and a persisted computed column, an alias type, one named by a keyword, `dbo.[User]`, with the table `dbo.accounts` of a column of it, and a table type, a view over an outer join, an inline and a multi-statement table-valued function, a table with an `AFTER` trigger, a system-versioned table whose period columns are hidden, the table `dbo.victim`, which a test that proves a text runs nothing looks for afterwards, and `dbo.all_types`, with one `NOT NULL` column of every type that SQL Server 2017 and 2025 share.  The describer's fixture cases are in the folders beside it.

### The describer's fixtures

The describer's fixtures are in `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/`: a folder for each case with its `query.sql`, the `capture.json` of describing it and the `expected.json` of its sidecar, whose `toolVersion` is `fixture` so that a new version of the tool touches no case.  `tests/SqlSource.Tool.Tests` replays each from its capture with no database, and this project describes each against the container and compares the entry, so a describer that asks something else fails the first and one that answers something else fails the second.  Both the capture and the sidecar carry the build of the server they were recorded on, which `FixtureVersionTests` holds to the build of the default image in `ImageBuilds`; `FixtureCasesTests` compares an entry whatever the build, so CI describes the cases on the floor image as well, where the answers are the same.  After a change to what the describer asks or answers, or to `schema.sql`, record them again and read the diff before it is committed:

```bash
tools/record-fixtures.sh mssql
```

To add a case, add its folder with a `query.sql` and run the script.  A recorded file is never edited by hand.  Nothing is recorded on the floor image.

## The realistic corpus

`tests/SqlSource.Tests.SqlServer/Realistic/` holds the corpus and what reads it: the cases in `Realistic/Cases/`, each a folder with `query.sql`, `case.json` (the truth) and, when the case has models, its sidecar `query.sql.json`.  The `realistic` database is built from `Realistic/schema.sql` and `Realistic/seed.sql`.  The root of `Realistic/` also holds `RealisticCases`, which binds the project to its corpus, `RealisticSamples`, which builds the batch that runs a case's query with a sample, and `RealisticDatabase`.  `RealisticCases.Corpus` is `RealisticCorpus.SqlServer` of `tests/SqlSource.Tool.Testing/Realistic/`, which names the corpus's files and reads its cases through `RealisticCaseReader`, each a `RealisticCase` of that folder, whose `SkipUnlessRunsOn` and `SkipIfRefused` are how a test skips a case; `RealisticCases.Models` finds a case's models in this assembly, and `RealisticCases.Describe` describes a case on `realistic`.  A test reads a case's committed entry through `RealisticModels.EntryOf`.  What both engines' corpora share, the corpus and its cases, the checks of a sample, the claims of `RealisticQueries.cs`, the finding of a case's models, the input model of a sample, the description of a case, the columns a description must have and the arithmetic of the noise budget, is in that folder of `tests/SqlSource.Tool.Testing`, and its tests are in `tests/SqlSource.Tool.Tests/Realistic/`.

### The database

`SqlServerContainer` creates `realistic` beside `fixtures` and applies `schema.sql`, then `seed.sql`, through `EndToEndFiles`, once for the run.  It stands apart from `fixtures`, so a change to it moves no capture of the describer's fixtures.  Its tables are of a shop: customers, a tree of categories, products, stock, orders and their lines, carts, prices with their history, and in a schema `billing` the invoices and their payments, with an alias type, a sequence, computed columns, a system-versioned table and a view over an outer join.

Every batch of `schema.sql` runs on SQL Server 2017.  What a later major brings is made by a batch that runs its text through `EXEC` only when `SERVERPROPERTY('ProductMajorVersion')` is high enough, so that 2017 never parses it: `dbo.product_specs`, with a column of 2025's `json`, is made on 17 alone.  The seed's values are fixed and its ids are given, so that a truth and a sample can name a row; its header says which row holds each nullable path of the schema.  The seed only grows: a case may add rows, and none is removed, since another case's truth or sample may stand on it.  `RealisticDatabaseTests` checks that every table has rows, that `dbo.product_specs` is there on 2025 alone, and that `fixtures` was left as it was.

### The checks

`Realistic/Describer/` holds the describer's checks over the corpus.  `RealisticCases.Describe`, a `RealisticDescribe` of `tests/SqlSource.Tool.Testing/Realistic/`, describes each case as a run does, in a session of its own on `realistic`, once in a run, and every check reads that one description.  A check runs a case on each server that its `minimumMajor` allows.

- `RealisticDescriptionTests` fails on a case that is refused otherwise than its `refusal` says: described when it says a refusal, refused by another id, or refused when it says none.  It also fails on a description whose columns differ from the truth's in number, or in the name at a position.  And it fails on a case with models that is described otherwise than its committed sidecar's entry holds, every field of it but the server's version, the provenance and the origins included, which `--check` ignores, on each recorded major that runs the case and that its `checkedFrom` does not leave out.
- `RealisticNullabilitySafetyTests` fails on a column that the describer calls not null and the truth says can be `NULL`.  It then runs the query over the seed once for each sample through `RealisticNullabilityOracle`, and fails on a `NULL` in a column that the describer or the truth calls not null, on a column that can be `NULL`, with no `exempt`, that held none for any sample, on a column the server names otherwise than the description, on a sample that names no parameter of the query, and on a sample that gives `NULL` to a parameter whose `-- param:` marker does not say `null`.
- `RealisticNullabilityNoiseTests` holds the count of columns that are not null and that the describer answers as nullable or unknown to a budget for each recorded major, 14 and 17, in its `Budgets`, as [the budget](#the-noise-budget) says.
- `RealisticNullabilityOracleTests` checks the oracle itself: that a query that writes leaves the seed as it was, that a `NULL` in a row is seen, that a sample is bound, an expression that ends in a comment too, that a parameter of an alias type is declared as that type and bound, and that a query that fails throws its own error.

The oracle runs a query in a transaction that it rolls back, and reads every row of its first result set, as the describer describes the first.  So a case may write: an `INSERT`, `UPDATE`, `DELETE` or `MERGE` with an `OUTPUT` is read as a `SELECT` is, and the seed stays as it was.

`RealisticCasesTests`, which fails on a folder of `Cases/` that cannot be read as a case or holds another file, `RealisticSamplesTests` and the tests of `tests/SqlSource.Tool.Tests/Realistic/` need no server.

### The models

A case has models when neither the describer nor the generator refuses it.  Each such case is a `Models` query of the project, claimed by a class of its own in `Realistic/Generation/RealisticQueries.cs`, since no one class can claim them all: a `Path` that names a folder searches none of the folders below it, and the attribute is not `AllowMultiple`.  The project's metadata on `Realistic/Cases/**/*.sql` gives those queries the logical database `realistic` and the type override `date=default`, which takes out the project's `date=System.DateOnly`, so that the corpus is read by the base type map.  Each case's sidecar is written by `tools/describe-end-to-end.sh`, as [the end-to-end project's](#writing-the-sidecars) are, on SQL Server 2025, and `CheckTests` finds it current on each recorded major that runs the case.  `Realistic/Generation/` holds the checks of the generated models:

- `RealisticClaimsTests` reads the classes of `RealisticQueries.cs` from its source, through `RealisticClaims`, and fails on a case with models that no class claims or that two claim, on a class of a case without models, and on a claim of a folder that is not a case.
- `RealisticModelNullabilityTests` reads each case's compiled output model, pairs each column of the truth with the property at its position, which must be named after it, and fails on a column that can be `NULL` whose property cannot hold null.  It reads the truth and never the sidecar's nullability, so it can fail where `--check` passes.
- `RealisticDapperTests` runs each case's query over the seed through Dapper into its output model, once for each sample, in a transaction that is rolled back and that every call is given.  It builds the input model from the sample's expressions as the server reads them, each cast to its parameter's type by a `SELECT` on the same connection before the transaction begins, and passes its values under the SQL's snake_case names in a `DynamicParameters`, since Dapper does not bind them to the model's PascalCase properties ([D-0004](../../docs/deferred/D-0004-input-models-bound-by-the-names-of-the-sql.md)).  It fails on an error, on a row of another type and on a case that reads no row for any sample, and asserts no value, which is the oracle's business.
- `RealisticModelRefusalTests` describes each case with `models` against `realistic` into a sidecar held in memory, runs the generator over its query and that sidecar at `Models` under `mssql`, and fails unless the generator gives exactly one error, of the id that `models` names.  No case has `models` today, so every case skips it.
- `RealisticModelsTests` and `RealisticParametersTests` test how those find this project's models and read a case's sample on SqlClient.  The reading of a claim, the building of an input model from its values and of Dapper's arguments from the model are tested in `tests/SqlSource.Tool.Tests/Realistic/`.

`RealisticClaimsTests`, `RealisticModelNullabilityTests` and the two tests of the helpers need no server.

### A case

A `case.json` holds its `columns`, in the order of the query's, each with its `name`, whether it is `nullable`, a `why` when it is, and an `exempt` when no seed can show its `NULL`.  It can also say:

- `samples`, one or more sets of a T-SQL expression for each parameter, by its name without `@`, compared ignoring case.  The oracle declares a variable of each parameter at the type the describer gave it, set to the expression, and passes it to the query through `sp_executesql`, so a sample is bound as a caller's value is and is never put into the query's text.  An expression is allowed where a literal is, `DATEADD(day, -30, '2024-11-01')` as well as `N'%a%'`, and it may end in a comment.  Every parameter has a value in each sample.  `"NULL"` is the one way to write a null, and only for a parameter whose `-- param:` marker says `null`, since generated code sends no null to any other.  A case with no parameters has no samples and runs once.
- `minimumMajor`, when the case needs a later server than SQL Server 2017, in SQL Server's major versions: 14 for 2017, 16 for 2022 and 17 for 2025.  A case of 2022's syntax, such as `GREATEST` or `IS DISTINCT FROM`, says 16, and so runs on 2025 alone of the two recorded majors.  The checks leave it out on an earlier server.
- `refusal`, the id of the failure, when the describer refuses the case.  The case then has no columns, no class and no sidecar, and the checks of its nullability skip it.
- `models`, a `refusal` and a `why`, when the generator refuses the models of a case that the describer accepts, such as two columns of one name.  The case keeps its SQL and has no class and no sidecar, and the describer's checks still read it.  No case has it today.
- `checkedFrom`, a `major` and a `why`, when SQL Server 2017 describes the case otherwise than its sidecar, which is written on 2025, by a parameter's or a column's type.  `--check` and the comparison with the sidecar on 2017 then leave the case out; the checks of its nullability still run there.  It is never for a difference of nullability, and its `why` says which type differs.  The `major` is 17, the one recorded major above 14.  No case has it today: 2017 describes every case that it runs as 2025 does.
- `dapper`, a `why`, when Dapper cannot send a parameter of the case.  The member leaves the case out of the run through Dapper alone: phase 8's generated methods, which will bind each parameter by its type, are to run every case, those with `dapper` too.  One case has it today, `insert-invoice-next-value`, whose null `@terms_days` Dapper sends as `nvarchar`, which `DATEADD` refuses.

A column that can be `NULL` says why, and an `exempt` says why the seed cannot show it, so it needs a reason and belongs only on a column that can be `NULL`.  `RealisticCaseReader`, through which `RealisticCorpus` reads each case, refuses a `case.json` that breaks either, that has both a `refusal` and `columns`, or that has a member it does not know.  Every folder of `Cases/` is a case.

### Adding a case

To add a case, add its folder with a `query.sql`, a SqlSource file of one query, and a `case.json`, which holds the truth, written from the SQL and `schema.sql` and never from what the describer answers.  Add its class to `Realistic/Generation/RealisticQueries.cs`, unless the describer refuses it, which its `refusal` says, or the generator does, which its `models` says, and run `tools/describe-end-to-end.sh`, which writes its sidecar: the project does not build while a claimed query has none.  A `why` says how the `NULL` comes, and names the rows of the seed that show it.  When a column of the truth can be `NULL` and no sample shows it over the seed, add a row to `seed.sql` that makes it so, or a sample that does; `exempt` is for a `NULL` no seed can show.  Run the project on both images, since `pre-commit-validation.sh` runs the default image alone.

SQL Server returns no row for a grouping set `()` over empty input, `ROLLUP`'s grand total among them: only an aggregate with no `GROUP BY` returns its one row over no rows.  So every row of a `ROLLUP` or of `GROUPING SETS` aggregates at least one row, and a `SUM` or `MAX` of a column that is not null is not null in it.

### The noise budget

Noise is a column that the truth calls not null and the describer answers as nullable or unknown.  It is safe, since the generated type is wider than it need be, and the budget keeps its cost in view.  `Budgets` of `RealisticNullabilityNoiseTests` holds one budget for each of `EngineImage.SqlServer.RecordedMajors` and no other, which a test of its own checks, and the count over the cases that run on a major must be that major's budget exactly.  On a major that is not recorded the test skips.

A count under the budget fails, and the budget is lowered in that change, so the improvement is kept.  A count over it fails too, and is fixed in the describer, unless the change adds cases: then the budget is raised to the count, and the commit says by how much and why.  The failure lists the noisy columns, each by its case, its position and its name, to read against the truths, and the cases that changed also fail `RealisticDescriptionTests` and show in the diff of their sidecars.  A proof that answers a new kind of column not null lowers the budget of each major in its own commit, with the sidecars it changes written again by `tools/describe-end-to-end.sh`.  The noise that remains is [TD-0071](../../docs/tech-debt/TD-0071-sql-server-answers-nullable-for-columns-that-cannot-be-null.md), by kind.

## The end-to-end project

`EndToEnd/` is where the project uses the generator as a consumer does.  The project imports `src/SqlSource/build/SqlSource.props` and `SqlSource.targets` by path, references the generator as an analyzer, and sets the dialect `mssql`, the output `Models`, the logical database `app`, the input model's suffix `Args` and shape `record`, and the type override `date=System.DateOnly` as properties.  The folder `EndToEnd/Queries/Billing/` has metadata of its own in the project: `sealed class` output models, the suffix `Row` and `date=default`.  It lists `SQLSRC240` in `WarningsNotAsErrors`, so that a pull request that raises `VersionPrefix` warns until the sidecars are written again.

### The databases

The container has two logical databases for `EndToEnd/`, each built from the `schema.sql` and `seed.sql` of its folder, as [running](#running) says:

- `app`, in `EndToEnd/app/`: the tables `dbo.accounts`, with an identity, the alias type `dbo.Email`, a computed column and defaults, and `dbo.notes`, which a `LEFT JOIN` reads, and `dbo.all_types`, with a nullable column of every type that the type map gives a C# type and that SQL Server 2017 has.  Its seed has two rows of `all_types`: row 1 with a value in every column, and row 2 with `NULL` in every column but `id` and `rowversion_value`, which the server fills.  On SQL Server 2025 and later, `EndToEnd/app/new-types.sql` adds `dbo.new_types`, with a column of `json` and one of `vector`, a row of values and a row of `NULL`s.
- `billing`, in `EndToEnd/billing/`: the table `billing.invoices`, in a schema of its own.

Every batch of the two schemas runs on SQL Server 2017, and a type that 2017 lacks goes into `new-types.sql`.

### The queries

The queries are in `EndToEnd/Queries/`, each file claimed by a type under `EndToEnd/` and chosen to cover a combination of settings:

| File | Claimed by | Covers |
|----|----|----|
| `Accounts.sql` | `AccountQueries`, and `Reporting.AccountReports` in a second namespace | The project's settings by property, `Nested`, table models of `dbo` by `SELECT *` and by the table's full list, with no `Dbo` in their names, `no-table-models`, two queries that share a model by `-- output-model:`, a parameter of the alias type, a parameter declared `null`, and two parameters whose types `-- param:` declares |
| `AllTypes.sql` | `AllTypesQueries` | Every type of the type map that SQL Server 2017 has, read by `SELECT *` into the table's model and written by an `INSERT` whose `OUTPUT INSERTED.*` reads into the same model, and `date` given back to the map by a query's marker |
| `NewTypes.sql` | `NewTypesQueries` | `json` and `vector`, described on SQL Server 2025 alone |
| `Constants.sql` | `ConstantQueries` | The output `sql` by the file header, with no models and no sidecar |
| `Billing/Invoices.sql` | `InvoiceQueries` | The database `billing` by `-- database:`, the model namespace by the file header, the table model `BillingInvoicesRow`, and the folder's metadata in the project |
| `Notes.sql` | `NoteQueries` | The output `codegen` by the attribute, the type override of the alias type `dbo.Email` to a record struct and of `time` to `TimeOnly` by the file header, and of `sys.time` back to the map by a query's marker, a `LEFT JOIN`, `-- column:`, and the marks `!` and `?` in brackets and in double quotes |
| `Shapes.sql` | `ShapeQueries` | The shapes, a suffix and the model namespace by the attribute and by markers, `sort-input` and `sort-output`, a model of another namespace, and the settings of query methods, which are accepted and change nothing |
| `Writes.sql` | `WriteQueries` | An `INSERT`, an `UPDATE` and a `MERGE`, each described by the rows of its `OUTPUT` |

Every query writes its parameters in camelCase, `@accountId`, so that its input model binds through Dapper as the manual's recipe says.  No entry differs between SQL Server 2017 and 2025: a parameter that the two would type differently declares its type with `-- param:`, as `ListOpenedWithin` does.

### The tests

- `ModelShapeTests` reads the models by reflection: the name, the shape and the properties that the settings give each.  It needs no server.
- `CheckTests` runs `describe --check`, through `CliRun.Real`, over the real project, with a connection to `app`, to `billing` and to `realistic`, and finds every committed entry the same, the realistic cases' among them.  Below SQL Server 2025 it names each file of `EndToEnd/Queries/` but `NewTypes.sql`, which that server cannot describe, and the `query.sql` of each realistic case that `minimumMajor` and `checkedFrom` do not leave out.
- `ErrorPathTests` describes a copy of `ErrorPaths`, a small project that is never compiled, against `fixtures`, and makes its sidecar stale, by an edit to its query or a column added to its table, or its query one that the server refuses.
- `AccountQueryTests`, `NoteQueryTests`, `ShapeQueryTests`, `InvoiceQueryTests`, `WriteQueryTests`, `AllTypesQueryTests` and `NewTypesQueryTests` read every query through Dapper into its output model, with its input model as the parameters, and assert the values of the rows, `NULL`s included.  The exception is `ListMarkedNames`, whose marks are for a reader by ordinal, which `NoteQueryTests` reads with ADO.NET into its model.  A test that writes runs in a transaction that is rolled back, and gives it to every Dapper call, since SqlClient refuses a command without the transaction of its connection.  `EndToEndSources` gives each a connection set up as the manual's recipe says, with the handlers of `Types/`.  `NewTypesQueryTests` skips below SQL Server 2025.
- `DapperRecipeTests` proves what the manual's Dapper recipe says for SQL Server beyond the queries: the type Dapper sends each C# type as, a `datetime2` that loses its digits as a `datetime` and is sent whole by the recipe's sample, `Types/TypedParameter.cs`, and by its `DbType`, a `vector` that Dapper refuses and the sample sends, a `string` written to `json`, the handlers of `Types/`, a mark that Dapper matches neither in a record nor in a class, and the transaction that a call must be given.  Its tests of `json` and `vector` skip below SQL Server 2025.

`AllTypesQueryTests` reads both rows of `all_types`, every column of each.  It writes a row with a parameter for each type that Dapper sends exactly and reads it back, and lists the other columns in `WriteSideLeftOut`, each with why, and a test fails when one of them comes to be written exactly.  The expectations of both rows, and of `new_types`, are in `tests/SqlSource.Testing/EndToEnd/SqlServerAllTypesRows.cs`, which [the SqlClient 6.1 project](../README.md#the-sqlclient-61-project) shares to read the same rows on Microsoft.Data.SqlClient 6.1, and so are the properties of `AllTypesDto` that `ModelShapeTests` and that project compare their model with.  It fails on a property with no expectation, so a column added to `all_types` adds its value in the seed, its expectation there, and its parameter to the insert or its row to `WriteSideLeftOut`.

A change to these tests is run on both images, as [running](#running) says.

### Writing the sidecars

Every sidecar under `EndToEnd/Queries/`, of the realistic cases under `Realistic/Cases/` and of `examples/SqlServerExamples` is written by one script, never by hand, which writes PostgreSQL's too ([its part](../SqlSource.Tests.Postgres/README.md#writing-the-sidecars)):

```bash
tools/describe-end-to-end.sh
```

For SQL Server it starts a container of the default image and applies the schema and seed of `realistic`, from `Realistic/`, then those of `app` and `billing`, with `new-types.sql` after the seed of `app`, through the image's `sqlcmd`, then deletes the sidecars of the end-to-end queries and of the realistic cases and runs `describe --force` on the project.  It then does the same for the examples, on a database `examples` made from their `schema.sql`.  The project does not build while a sidecar is missing or stale, so the script runs the tool from its source, not a test.  The sidecars are written on SQL Server 2025, and `CheckTests` finds them the same on 2017, but for the realistic cases that need a later server.  Run it after a change to what the describer asks or answers, to a query, a schema or a seed of `EndToEnd/`, of the realistic corpus or of the examples, after a case is added to the corpus, after a move of the default image, and in the pull request that raises `VersionPrefix` ([`docs/publishing.md`](../../docs/publishing.md)), and read the diff before it is committed.
