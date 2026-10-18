# Query generation, phase 5.4: the PostgreSQL end-to-end project - design

Date: 2026-10-18

Sub-phase 5.4 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It makes `tests/SqlSource.Tests.Postgres` the end-to-end project that the epic's Testing section decides on: queries in `.sql` files, schemas for two logical databases, committed sidecars, and tests that run the tool against the container, build with the generator, and read real rows into the generated models through Dapper.

It builds on sub-phase 5.3.  Sub-phases 5.5 and 5.6 build on it and can be built beside each other.

## Intent

The generator tests of 5.1 to 5.3 prove what the generator writes.  They cannot prove that what it writes is right against a real server: that the type the map chose is the one Npgsql gives, that a column the model calls not null never holds `NULL`, that a model built for Dapper is one Dapper fills.  The owner's first measure of phase 5 is that every built-in PostgreSQL type maps accurately to its .NET type, and this is where that is proven.

## Goals

1. `tests/SqlSource.Tests.Postgres` runs the generator at build time, as a consumer does, over its own `.sql` files and committed sidecars.
2. Two logical databases, `app` and `billing`, on the project's container, each with a schema and a seed.
3. Queries that cover the combinations of settings: each output, each `SqlLocation`, each model type, the suffixes, the namespace, the sorting, table models, shared names, the type override at each level, and the phase 8 settings accepted with no effect.
4. A table of every type the base map has, and a query that reads each, so that the Dapper run proves every row of the map.
5. `tools/describe-end-to-end.sh`, which writes every committed sidecar of the project.
6. The epic's three kinds of test: the tool's `--check` against the container, every `Models` query run through Dapper into its model, and the error paths through the tool on a scratch copy.
7. CI runs them on PostgreSQL 18 and 14, as it runs the project today, and a second, small project reads every mapped type on the oldest supported Npgsql.

## Non-goals

- The realistic corpus as models.  Sub-phase 5.6.
- Retiring `expected-pg14.txt` and `expected-pg18.txt`.  Sub-phase 5.7.
- Execution through generated methods.  Phase 8, which replaces the Dapper run as the main proof and keeps a Dapper test.
- A second test project beyond the one for the oldest Npgsql below.  The epic decides one project per database, and that project is the one exception, which the owner approved.

## Anti-goals

- **A test that passes by reading nothing.**  Every Dapper test asserts on the values of at least one row, and the type-map test on a row of values and a row of `NULL`s, so that a query that returns no rows cannot pass.
- **A committed sidecar written by hand.**  Every sidecar the project commits is written by `tools/describe-end-to-end.sh`, and `--check` proves it is what the tool writes today.
- **A database left as the tests found it changed.**  The Dapper tests read; a test that writes runs in a transaction that is rolled back.

## What exists today

As of the merge of 5.3:

- `tests/SqlSource.Tests.Postgres` is an `Exe` on `net10.0` with xunit v3 and Testcontainers.  It has a plain `ProjectReference` to `src/SqlSource` for the tool's code, and does not reference it as an analyzer or import the props and targets.  `PostgresContainer` starts one container for the run, with the databases `fixtures` and `realistic`, on `PostgresContainer.DefaultImage` (`postgres:18.6`) or `SQLSOURCE_TESTS_POSTGRES_IMAGE`.  CI runs the project on 18.6 and again on 14.24.
- `tests/SqlSource.Tests/SqlSource.Tests.csproj` is the pattern for a project that runs the generator: it imports `src/SqlSource/build/SqlSource.props` and `SqlSource.targets` by path and references the generator with `OutputItemType="Analyzer"`.
- `DescribeCommandTests` runs `Cli.RunAsync` in process against the container, and `ToolProcessTests` runs the built tool, over a copy of the fixture project `Single`.
- `Directory.Packages.props` pins Npgsql 10.0.3 and Dapper 2.1.89; no project references Dapper.
- `TreatWarningsAsErrors` is on for every project.

## Design

### The project

`tests/SqlSource.Tests.Postgres.csproj` imports the props and targets by path and references the generator as an analyzer, as `tests/SqlSource.Tests` does, beside the reference it already has for the tool's code; the builder finds the form of the two references that MSBuild accepts for one project.  It sets `SqlSourceDialect` to `postgres` and lists `SQLSRC240`, the tool-version warning, in `WarningsNotAsErrors`, as the epic decides, so that a pull request that raises `VersionPrefix` warns until it runs the script.  It references Dapper.

Its `.sql` files outside `EndToEnd/` and `Realistic/` are not claimed by any type: the describer's fixture `query.sql` files are under `tests/SqlSource.Tool.Tests`, and the realistic cases are claimed by sub-phase 5.6.  The project's existing tests are unchanged.

### The databases

`PostgresContainer` creates two more databases, `app` and `billing`, and applies each one's `schema.sql` and `seed.sql` with plain ADO.NET, as it applies the realistic schema today.  They live under `EndToEnd/app/` and `EndToEnd/billing/`.  `billing` has a schema of its own, `billing`, so that the table model's schema prefix is proven, and `app` the `public` tables.  Both run on PostgreSQL 14: nothing in them needs a later major.

`app` holds, among the tables its queries need:

- **`all_types`**, a table with a column of every type the base map of sub-phase 5.2 gives a C# type for on both Npgsql 8 and 10 and that PostgreSQL 14 has, arrays, ranges and multiranges of a representative few included, the extension types of the map with their extensions created by the schema, a domain and an enum of the schema's own, and two rows in the seed: one with a value in every column, chosen to exercise the type's range, and one with `NULL` in every column but the key.  `cube`, which Npgsql 8 cannot read and the build on 8 would refuse, is left out; its row is proven by 5.2's generator tests alone.  A type whose C# type differs between 8 and 10, every row of sub-phase 5.2's table with a cell in its 10 column and their arrays, ranges and multiranges, is asserted by the type its model was given, so that one test serves both runs.  The types the map refuses are not in the table.
- A table whose columns cover nullable and not-null, an identity, a generated column, a default.
- Two tables that a `LEFT JOIN` joins, so that a column of the nullable side is proven nullable.

### The queries

Under `EndToEnd/Queries/`, in files claimed by classes under `EndToEnd/`, each class and file chosen to cover one combination.  Together they cover:

| What | How |
|----|----|
| Each output | One file at each of `Sql`, `Models` and `CodeGen`; the `Sql` file has no sidecar, which proves the tool's filtering |
| Each `SqlLocation` | A class at `Nested` and one at `Direct` |
| Each model type | `sealed record`, `record`, `sealed class`, `class`, by property, metadata, attribute and marker |
| Suffixes and names | Each level of the suffixes, and both model markers with and without a period |
| Namespace | The default, `SqlSourceModelNamespace` at a level, and a type in a second namespace claiming a file |
| Sorting | `sort-input` and `sort-output` on one query each |
| Table models | `SELECT *` from `app` and `billing` tables, an explicit full list, `no-table-models`, and two queries that share one table's model |
| Shared names | Two queries that name one model with `-- output-model:` |
| Nullability | The `!` and `?` suffixes, a `LEFT JOIN`, a parameter declared `null` |
| The type override | `timestamptz` to `System.DateTimeOffset` at the property level, overridden back to `default` by a query marker and to `System.DateTime` for a folder by metadata; a domain to a user's record struct; the schema's enum to a user's C# enum |
| Parameter names | Every query of `EndToEnd/` writes its parameters in camelCase, `@accountId`, so that its input model binds through Dapper as the manual's recipe says; the realistic corpus of 5.6 proves the snake_case way, parameters built by hand |
| Two databases | Queries of `billing` name it with `-- database: billing`; the rest are `app` by the project's `SqlSourceDatabase` |
| The phase 8 settings | A result shape, a collection type and `MethodLocation` on one file, accepted with no effect |
| The type map | `SELECT * FROM all_types` and a query with a parameter of each mapped type |

The project's own code that uses a model, the Dapper tests, compiles against the generated types, so a model that is not generated, or has another shape, fails the build of the tests.

### The script

`tools/describe-end-to-end.sh` writes every committed sidecar of the project, from nothing, by `describe --force`, against a container started for it with the same image, schemas and seeds as the tests, so that what it writes is what `--check` compares with.  It cannot run a test of the project, as `tools/record-fixtures.sh` and `tools/record-realistic.sh` do: a query that has no entry, or a stale one, is a build error, and the tests compile against the models, so after a query is added or edited the project does not build until the script has run.  The tool needs no build of the project it describes.  So the script starts the container itself with `docker run`, on the default image it reads from `PostgresContainer.DefaultImage` as the other two scripts do, creates the databases and applies their schemas and seeds with `psql` in the container, runs the tool as a project, `dotnet run --project src/SqlSource.Tool -- describe --force`, on the test project with a `--connection` for each logical database, removes the container on every exit, and fails when the tool reports an error.  The schemas and seeds are the files `PostgresContainer` applies, named once.  The sidecars are recorded on the default image; how a case that needs a later major is kept out of the run on 14 is sub-phase 5.6's, since only the corpus has one.

The epic says that the pull request that raises `VersionPrefix` runs this script, since every committed sidecar's `toolVersion` changes; `docs/publishing.md` does not say so yet, and gains the step (below).

### The three kinds of test

Under `EndToEnd/`, in the project's collection that shares the container.

1. **`--check`.**  `Cli.RunAsync` runs `describe --check` on the project with a connection for each database, and the test asserts exit `0`.  A schema change that forgets the script fails CI with the tool's diff; a query change that forgets it fails the build before, with the stale entry's error.
2. **Dapper.**  Every `Models` and `CodeGen` query of `EndToEnd/` is run through Dapper into its generated output model, with its generated input model as the parameters as the manual's recipe says to pass them, over the seed, and the test asserts the rows: every property of the rows it names, `NULL`s included.  `SELECT * FROM all_types` asserts every column of both rows, so that each row of the map is read through Dapper into the mapped type: a value Dapper cannot assign to the property's type throws.  The query with a parameter of each mapped type proves the write side for the types Dapper can bind, which are those it has a `DbType` for and a `string` beside a column Npgsql compares with `text`; for the others, the types of `NpgsqlTypes`, `IPAddress`, `PhysicalAddress`, `BitArray`, an enum's `string`, `jsonb`'s `string`, and a C# enum, which Dapper sends as its number, the read side is the proof in phase 5, the manual's recipe says what a Dapper user does, and phase 8's methods, which bind by the sidecar's `bindName`, prove the write side.  The test lists the types it leaves out of the write side, so that a type is left out on purpose and not by omission.  Dapper's `DefaultTypeMap.MatchNamesWithUnderscores` is set, as the manual's recipe tells a user to.  The data source enables what the manual says a project needs for the types it has, and nothing more: `MapEnum<T>()` for the enum mapped to a C# enum, and a Dapper type handler for the user's record struct; an enum read as `string` needs nothing, which sub-phase 5.2's research found.
3. **Error paths.**  A scratch copy of a small project under `EndToEnd/ErrorPaths/`, made per test in a temporary folder, with the package's MSBuild files.  The folder's `.sql` and `.cs` files are removed from the test project's own `AdditionalFiles` and `Compile`, as `tests/SqlSource.Tool.Tests` removes `Fixtures/Projects/**`, so that neither the build, the script nor `--check` of the test project sees them, and it commits no sidecar.  Each test makes its state in the copy: it describes the copy, then edits the schema in the container or the query to make the sidecar stale, for which `--check` exits `2` with `SQLSRC219`; and a query the server refuses, for which `describe` reports the error and writes no sidecar.  The build's own error for a stale sidecar, `SQLSRC244`, is a generator test of sub-phase 5.1 and is not repeated.

### The oldest Npgsql

The owner approved a second project for it, after the review found that the end-to-end project cannot run on Npgsql 8: it references the tool, which pins Npgsql 10 and reads Npgsql 10's internals by reflection, and a direct reference to 8 under that is a package downgrade, `NU1605`, an error.

The epic decides that the map is tested against Npgsql 8, 9 and 10, and the type map of sub-phase 5.2 differs on 8 and 9 from 10 in every row with a cell in its 10 column.  The generator tests of 5.2 prove the probe and the map under each; 8 and 9 box every type alike, which 5.2's research found, so 8 at run time stands for both.

`tests/SqlSource.Tests.Postgres.Npgsql8` is that project.  It references the generator as an analyzer with the props and targets imported by path, Npgsql pinned to the floor's latest 8.0 release by `VersionOverride`, Dapper, xunit v3 and Testcontainers, and nothing of the tool, so that no reference pulls Npgsql 10 in and its lock file is its own.  It links the `all_types` query's `.sql` file and its committed sidecar from the end-to-end project, so the type map's query is described once, by the script, and its own `[SqlSourceGenerate]` type claims the linked file.  Its one kind of test is the `all_types` read through Dapper into the model the generator gave under Npgsql 8, both rows, with the same assertions as the end-to-end project's, from a helper the two share; it runs on PostgreSQL 18 with the `app` schema and seed applied, by a container fixture of its own.  It has no `--check` and no error paths: the sidecar it reads is checked by the end-to-end project.

`pre-commit-validation.sh` and CI run it with the solution, as every test project; CI runs it on the default image alone.  `CONTRIBUTING.md` gains its sentence, `tests/README.md` its place, and the epic outline's Testing section the exception.

## Testing

What this sub-phase adds is tests; they are listed above.  Besides them:

- `./pre-commit-validation.sh` builds the project with the generator, and runs the three kinds of test on the default image.
- CI runs them on 18.6 and on 14.24 through the steps that exist, and the Npgsql 8 project with the solution's tests.

## Documentation

- `tests/SqlSource.Tests.Postgres/README.md`: the end-to-end part of the project, the two databases, the script, and when to run it.  The Npgsql 8 project gets a short README of its own, or a section of `tests/README.md`, as that file's rules say.
- `tools/README.md`: the script.
- `CONTRIBUTING.md`: the project's sentence, and the script beside the other two recording scripts.
- `docs/publishing.md`: the step that the epic and this spec assume and the file does not hold yet: the pull request that raises `VersionPrefix` runs `tools/describe-end-to-end.sh` and commits the sidecars it rewrites.
- `AGENTS.md`, rule 5: the end-to-end sidecars are among what a new minor of the default image records again, by this script.
- `docs/manual/recipes.md`, where sub-phase 5.3's Dapper recipe is checked against what the tests had to do: every setting the tests needed is in it.
- The epic outline: the 5.4 row's status, and any change to the project decision above.

## Order of work

One pull request, one commit for each step:

1. The epic's 5.4 row says In progress.  The project runs the generator, with no claimed file yet.
2. The two databases, their schemas and seeds.
3. The queries and the classes, with sidecars written by the script.
4. `--check` and the error paths.
5. The Dapper tests.
6. `tests/SqlSource.Tests.Postgres.Npgsql8`.
7. The documents, and the epic's row set to Done.

The plan counts the pull request's files against the limit of 150 before it starts; the queries and their sidecars are the bulk.

## Left to the builder

- Which queries and how many, within the table above; the names of the classes and files.
- The seed's values.
- The form of the two references to the generator in one project.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- The script starts its own container and runs the tool as a project, since the project cannot build until its sidecars are current.
- The error paths make their stale state in a scratch copy at test time; the test project commits no stale sidecar.
- The write side of the type map is proven through Dapper for the types Dapper binds, and the read side for every type.
- A seed per database beside its schema, where the epic named a schema alone: the Dapper tests read rows.
- The Npgsql 8 project holds the `all_types` read alone, links its query and sidecar from the end-to-end project, and runs on the default image.
- The error paths run the tool alone; the build's error for a stale sidecar stays a generator test.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.
