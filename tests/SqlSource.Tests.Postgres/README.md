# SqlSource.Tests.Postgres

This project runs the PostgreSQL describer and the `describe` command against a real server, which Testcontainers starts in Docker.  It is also the end-to-end project of the generator, which it runs when it builds.  It proves five things: each step of the describer, from its session to the walk, with what only a server answers; every fixture case of the describer, described live and compared with its `expected.json` by `FixtureCasesTests`, which `tools/record-fixtures.sh` also runs to record them; the whole `describe` command with the real describer, in process and as the built tool; the realistic corpus, where each case is a query with a truth of which of its columns can be NULL, written by hand; and [the end-to-end project](#the-end-to-end-project), where the generator builds models from the project's own queries and committed sidecars, the tool's `--check` finds those sidecars current, and Dapper reads real rows into the models.

## Running

The project needs Docker running, and its first run pulls the image.  It starts one container for the run, with the database `fixtures` and the schema `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/schema.sql`, and a second database, `realistic`, for the realistic corpus below.  It also has the databases `app` and `billing` for the end-to-end tests, each built from the `schema.sql` and `seed.sql` in its folder of `tests/SqlSource.Tests.Postgres/EndToEnd/`.

```bash
dotnet test --project tests/SqlSource.Tests.Postgres
```

`dotnet test --solution SqlSource.slnx`, and so `pre-commit-validation.sh`, runs it on the default image.

The project copies two fixture projects to its output, with the package's MSBuild files and `global.json`: `Single`, from `tests/SqlSource.Tool.Tests/Fixtures/Projects/`, and `ErrorPaths`, from `EndToEnd/ErrorPaths/`.  `DescribeCommandTests` runs the whole `describe` command in process against the container, over the fixture cases, `--check`, `--force` and `--log`, and `ToolProcessTests` starts the tool that was built in a process of its own and describes a copy of `Single`.  `ErrorPathTests` describes copies of `ErrorPaths`, [below](#the-end-to-end-project).

## The image

The image is `postgres:18.6`, named in one place, `PostgresContainer.DefaultImage`; the variable `SQLSOURCE_TESTS_POSTGRES_IMAGE` names another, and PostgreSQL 14 is the oldest that is supported, which CI runs as `postgres:14.24`:

```bash
SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres
```

Every database image the tests run is pinned to a major and a minor, such as `postgres:18.6`, and never to a bare major such as `postgres:18`.  The recorded fixtures carry the version of the server they were recorded against, and a bare major moves to each new minor without a word, so the tests would run a server the recordings never saw.  This holds for the default image, for the image CI runs on the oldest supported version, and for the image of any engine added later.  `PostgresContainerTests` fails on an image whose tag is not a major and a minor, or whose server reports another version, and `FixtureVersionTests` fails on a fixture case recorded against any version but the default image's.

Moving to a new minor is a change of its own: bump the tag, and the default image's in `examples/PostgresExamples/docker-compose.yml`, record the fixtures and the realistic corpus again on the new image, as in the next two sections, write the end-to-end sidecars again, as in [the end-to-end project](#the-end-to-end-project), since each entry holds the server's version, and read the diff, all in the same pull request.

## The describer's fixtures

The describer's fixtures are in `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/`: a folder for each case with its `query.sql`, the `capture.json` of describing it and the `expected.json` of its sidecar, whose `toolVersion` is `fixture` so that a new version of the tool touches no case.  A case may also hold an `issue.md`, which says where the case came from, and an `explain-settings.txt`, the settings of the planner it is described with.  `tests/SqlSource.Tool.Tests` replays each from its capture with no database, and this project describes each against the container and compares the entry, so a describer that asks something else fails the first and one that answers something else fails the second.  After a change to what the describer asks or answers, or to `schema.sql`, record them again and read the diff before it is committed:

```bash
tools/record-fixtures.sh
```

To add a case, add its folder with a `query.sql` and run the script.  A recorded file is never edited by hand.

A case of the walk is named `walk-` and its shape, and `tests/SqlSource.Tool.Tests/Fixtures/WalkMatrixTests.cs` holds a row for each, written by hand, which fails for a `walk-` case it does not list.  To add a case that forces a join method:

1. Name it for the method, with `-hash`, `-merge` or `-nestloop` at the end.
2. Add an `explain-settings.txt` that turns the other two methods off, one `name=value` on a line, from the closed list of `PlannerSettings`: `enable_hashjoin=off` and `enable_nestloop=off` for a merge join.
3. Add its row to `Rows` of `WalkMatrixTests`, and an `InlineData` with the join's node type to `Capture_CaseNamedForAJoinMethod_WasPlannedWithIt` beside it.
4. Run the script.

`FixtureCasesTests` describes such a case with its settings, and `DescribeCommandTests` leaves it out, since a run of the command cannot turn a join method off.

## The realistic corpus

`tests/SqlSource.Tests.Postgres/Realistic/` holds the corpus and what reads it: the cases in `Realistic/Cases/`, each a folder with `query.sql`, `case.json` (the truth), and the recorded expectations, `expected-pg14.txt` and `expected-pg18.txt`, of each major the case runs on.  The `realistic` database is built from `tests/SqlSource.Tests.Postgres/Realistic/schema.sql` and `tests/SqlSource.Tests.Postgres/Realistic/seed.sql`.  The root of `Realistic/` holds what every layer shares: `RealisticCase`, `RealisticCases`, `RealisticSamples` and `RealisticDatabase`.  `Realistic/Describer/` holds the describer's checks over it, the description whole in `RealisticDescriptionTests` and its nullability in the classes named `RealisticNullability*`:

- `RealisticDescriptionTests` compares each description with the `expected-pg14.txt` and `expected-pg18.txt` recorded of it;
- `RealisticNullabilitySafetyTests` checks its nullability against the truth and against the oracle that executes the query over the seed;
- `RealisticNullabilityNoiseTests` holds the count of columns that are not null and that the describer answers as nullable or unknown to a budget for each recorded major, 14 and 18, in its `Budgets`.

The corpus is recorded by its own script, `tools/record-realistic.sh`, which writes the `expected-pg{major}.txt` of each case under `tests/SqlSource.Tests.Postgres/Realistic/Cases/` for the major version of the server it ran against, and never a `case.json`.  The expectations are those of PostgreSQL 18 and 14, so recording is two runs, the second on the older image; run it after a change to what the describer asks or answers, or to the schema, the seed or a case, and read the diff before it is committed:

```bash
tools/record-realistic.sh
SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 tools/record-realistic.sh
```

A `case.json` can also say:

- `minimumMajor`, when the case needs a later server than 14.  The case then has no expectation for an earlier major.
- `samples`, a SQL literal for each parameter.  A check that executes the query runs it over the seed with them.
- `refusal`, the id of the failure, when the describer refuses the case.  The case then has no columns.

A column that can be `NULL` says why, and an `exempt` says why the seed cannot show it, so it needs a reason and belongs only on a column that can be `NULL`.  `RealisticCases` refuses a `case.json` that breaks either.  Every folder of `Cases/` is a case.

To add a case, add its folder with a `query.sql` and a `case.json`, which holds the truth, written from the SQL and `schema.sql` and never from what the describer answers, and run both.  When a column of the truth can be `NULL` and no row of `seed.sql` makes it so, add such a row to `seed.sql`, which never removes one; `exempt` is for a `NULL` no seed can show.  The noise budget is lowered in the change that lowers the noise, the test says to, and raised only in a change that adds cases.

## The end-to-end project

`EndToEnd/` is where the project uses the generator as a consumer does.  The project imports `src/SqlSource/build/SqlSource.props` and `SqlSource.targets` by path, references the generator as an analyzer, and sets the dialect `postgres`, the output `Models`, the logical database `app` and the type override `timestamptz=System.DateTimeOffset` as properties.  It lists `SQLSRC240` in `WarningsNotAsErrors`, so that a pull request that raises `VersionPrefix` warns until the sidecars are written again.

### The databases

The container has two logical databases for it, each built from the `schema.sql` and `seed.sql` of its folder:

- `app`, in `EndToEnd/app/`: the tables `accounts` and `notes`, which a `LEFT JOIN` reads, the enums `account_status`, read as a `string`, and `note_kind`, read as a C# enum, the domain `email`, the extensions `citext`, `hstore` and `ltree`, and `all_types`, with a column of every type that the type map gives a C# type for.  Its seed has two rows of `all_types`: row 1 with a value in every column and row 2 with `NULL` in every column but `id`.
- `billing`, in `EndToEnd/billing/`: the table `billing.invoices`, in a schema of its own.

### The queries

The queries are in `EndToEnd/Queries/`, each file claimed by a type under `EndToEnd/` and chosen to cover a combination of settings:

| File | Claimed by | Covers |
|----|----|----|
| `Accounts.sql` | `AccountQueries`, and `Reporting.AccountReports` in a second namespace | The project's settings by property, `Nested`, table models by `SELECT *` and by the table's full list, `no-table-models`, two queries that share a model by `-- output-model:`, and a parameter declared `null` |
| `AllTypes.sql` | `AllTypesQueries` | Every type of the type map, read by `SELECT *` and written by an insert, class models by the file header, and `timestamptz` given back to the map by a query's marker |
| `Constants.sql` | `ConstantQueries` | The output `sql` by the file header, with no models and no sidecar |
| `Billing/Invoices.sql` | `InvoiceQueries` | The database `billing` by `-- database:`, the model namespace by the file header, and the folder's metadata in the project: `sealed class` output models, the suffix `Row` and `timestamptz` as a `DateTime` |
| `Notes.sql` | `NoteQueries` | The output `codegen` by the attribute, the type override of the enum to a C# enum and of the domain to a record struct, a `LEFT JOIN`, `-- column:`, and the marks `!` and `?` |
| `Shapes.sql` | `ShapeQueries` | The shapes, a suffix and the model namespace by the attribute and by markers, `sort-input` and `sort-output`, a model of another namespace, and the settings of query methods, which are accepted and change nothing |

Every query writes its parameters in camelCase, `@accountId`, so that its input model binds through Dapper as the manual's recipe says.

### The tests

- `ModelShapeTests` reads the models by reflection: the name, the shape and the properties that the settings give each.  It needs no server.
- `CheckTests` runs `describe --check` over the real project, with a connection to each database, and finds every committed entry the same.
- `ErrorPathTests` describes a copy of `ErrorPaths`, a small project that is never compiled, and makes its sidecar stale, or its query one that the server refuses.
- `AccountQueryTests`, `NoteQueryTests`, `ShapeQueryTests`, `InvoiceQueryTests` and `AllTypesQueryTests` read every query through Dapper into its output model, with its input model as the parameters, and assert the values of the rows, `NULL`s included.  The exception is `ListMarkedNames`, whose marks are for a reader by ordinal, which `NoteQueryTests` reads with ADO.NET into its model.  A test that writes runs in a transaction that is rolled back.  `EndToEndSources` sets up a data source as the manual's recipe says.
- `DapperRecipeTests` proves what the manual's Dapper recipe says beyond the queries: the parameters it sends by its sample, `Types/TypedParameter.cs`, and by a cast in the SQL, each column of `WriteSideLeftOut` written so in a transaction that is rolled back, a C# enum sent as its number, a `DateTimeOffset` at offset 0, and which records Dapper fills.

`AllTypesQueryTests` reads both rows of `all_types`, every column of each.  It writes a row with a parameter for each type that Dapper can send and reads it back, and lists the other types in `WriteSideLeftOut`, each with what Dapper or Npgsql said, and a test fails when one of them comes to bind.  The expectations of both rows are in `tests/SqlSource.Testing/EndToEnd/AllTypesRows.cs`, which `tests/SqlSource.Tests.Postgres.Npgsql8` shares to read the same rows on Npgsql 8.  It fails on a property with no expectation, so a column added to `all_types` adds its value in the seed, its expectation there, and its parameter to the insert or its row to `WriteSideLeftOut`.

### Writing the sidecars

Every sidecar under `EndToEnd/Queries/` is written by one script, never by hand:

```bash
tools/describe-end-to-end.sh
```

It starts a container of the default image and applies the schemas and seeds of `app` and `billing`, then deletes the sidecars and runs `describe --force` on the project.  The project does not build while a sidecar is missing or stale, so the script runs the tool from its source, not a test.  Run it after a change to a query, a schema or a seed of `EndToEnd/`, after a move to a new minor of the default image, and in the pull request that raises `VersionPrefix` ([`docs/publishing.md`](../../docs/publishing.md)), and read the diff before it is committed.
