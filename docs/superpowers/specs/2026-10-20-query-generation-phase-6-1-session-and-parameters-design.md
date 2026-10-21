# Query generation, phase 6.1: the SQL Server session and parameters - design

Date: 2026-10-20

Sub-phase 6.1 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It delivers the second engine as far as opening a session and typing a query's parameters: the SQL Server describer over Microsoft.Data.SqlClient, the project that tests it against SQL Server in a container, the images pinned to a cumulative update, CI that pulls its images in a step of their own and runs the floor, the fixture recording for two engines, and the first SQL Server fixtures.  The describer is registered nowhere yet: the released tool keeps saying `SQLSRC216` for `mssql` until sub-phase 6.3, and until then the describer is exercised by its tests alone.

It builds on nothing of phase 6.  Sub-phase 6.2 builds on it.  Sub-phase 6.4 can be built beside it.  What the servers answered is in [the spike of phase 6](2026-10-20-query-generation-phase-6-sql-server-spike.md), which this spec cites as "the spike".

## Intent

The owner holds SQL Server to the bar PostgreSQL met: every type exact, the server's answers trusted where they are right and corrected only where a test proves them wrong, and everything proven on the oldest and the newest server.  This sub-phase lays the ground the rest of the phase stands on, the container, the images and the capture of every call, and does the part of describing that the spike found most surprising: the parameters, where the outline's workaround for a reused parameter does not work as written.

## Goals

1. `tests/SqlSource.Tests.SqlServer`, a container test project like `tests/SqlSource.Tests.Postgres`, on SQL Server 2025 by default and 2017 as the floor, each image pinned to a tag that names its cumulative update, with a test that the server is the build the tag promises.
2. CI pulls every image the tests run, both engines', in a step of its own before the tests, so that the time of a pull is never read as the time of the tests; then runs the new project on the default image with the solution, and again on 2017.  `pre-commit-validation.sh` runs it on the default image with every other test.
3. `tools/record-fixtures.sh` records the fixtures of either engine; `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/` holds its schema and the first cases, replayed with no Docker.
4. `SqlServerDescriber`, behind the seam of sub-phase 2.5, opens a session, reads the server's version and the settings that change a description, and refuses a server below 2017.
5. Every parameter of a query gets the type the server gives it, a declared one resolved by the server; a parameter used more than once is typed by its copies, with the probe of the spike deciding which copies count; and the server's list of undeclared parameters is checked against the lexer's.
6. Every call goes through the exchange; a server's error is a failure at the query with one id for each cause a user mends differently.

## Non-goals

- Columns, origins, the table match and driver logging.  Sub-phase 6.2.
- Registering the describer in `ToolHost.Create`.  Sub-phase 6.3, once the realistic corpus has checked the describer whole.
- The type map and the generator's side.  Sub-phase 6.4.
- The end-to-end databases `app` and `billing`, and `EndToEndFiles` for SQL Server.  Sub-phase 6.5.
- A table-valued parameter.  Out of the epic, though the spike found the server accepts one declared `READONLY`.

## Anti-goals

- **A statement of the user's that the tool runs.**  Nothing a `.sql` file holds may make the describer run a statement.  The user's text reaches the server only as the value of a parameter of `sp_describe_undeclared_parameters` or `sys.dm_exec_describe_first_result_set`, which are static analysis; a declaration reaches it only as the `@params` value of the same.  Never concatenated into a batch the tool sends.
- **A type the tool guessed.**  Every type in the sidecar is one the server gave.  A copy of a reused parameter is ignored only on the server's evidence, never on the lexer's reading of what surrounds it.
- **An image that moves.**  No test, script or workflow names `-latest` or a bare year.

## What exists today

- `tests/SqlSource.Tests.Postgres` is the pattern: `PostgresContainer`, whose default image is `DefaultImages.Postgres` (`postgres:18.6`) in `tests/SqlSource.Testing/DefaultImages.cs`, read by its line shape in `tools/record-fixtures.sh`, `tools/describe-end-to-end.sh` and the Npgsql 8 project; `ImageVariable` `SQLSOURCE_TESTS_POSTGRES_IMAGE`; `RecordedMajors`; `PostgresContainerTests`, which fails on an image tag that is not a major and a minor and on a server of another version; `FixtureVersionTests`; `Describer/FixtureCasesTests`, which records with `SQLSOURCE_RECORD_FIXTURES` set.
- `tools/record-fixtures.sh` is PostgreSQL's alone: its project, case folder, test class and image are fixed.
- `build.yml`'s `build` job runs `dotnet test --solution`, then the PostgreSQL project again with `SQLSOURCE_TESTS_POSTGRES_IMAGE: postgres:14.24`, within `timeout-minutes: 15`.  Testcontainers pulls each image inside the test step.  `nightly.yml` names its projects and so leaves every container project out.
- The describer seam is engine-neutral: `IQueryDescriber`, `IDescribeSession`, `DescriberRegistry`, the three exchanges, the capture, `SessionRunLog`, which prefixes every event with the engine's canonical name, here `mssql.`, and `EntryBuilder`.  `src/SqlSource.Tool/AGENTS.md` holds the rules a describer follows.  `CallRecordTests.Under` lists the namespaces whose records are checked, and `DescriberFixtureTests.DescriberOf` knows `postgres` alone.
- The sidecar's `SqlServerType` and `SqlServerUserType`, the reader, writer and comparer, and the `typeSource` value `inferred-from-copies` exist.
- `SQLSRC235`'s message names PostgreSQL.  No project references Microsoft.Data.SqlClient or Testcontainers.MsSql.
- Root `AGENTS.md` rule 5 pins every test database to a major and a minor, with PostgreSQL's places named.

## Design

### The images

| Image | Tag | Where it is named |
|----|----|----|
| The default | The newest tag of SQL Server 2025 that names a cumulative update, and a GDR when the newest build has one, on the newest Ubuntu MCR publishes for it: `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04` or later when the builder starts | `DefaultImages.SqlServer`, in `tests/SqlSource.Testing/DefaultImages.cs` beside `DefaultImages.Postgres` |
| The floor | The newest such tag of SQL Server 2017: `2017-CU31-GDR19-ubuntu-18.04` or later | `build.yml`, `CONTRIBUTING.md`, `tests/SqlSource.Tests.SqlServer/README.md` |

A tag names its update: `<year>-CU<n>` with an optional `-GDR<n>`, then `-ubuntu-<version>`, and never `-latest`, a bare year or an on-demand hotfix tag.  `SqlServerContainer.ImageBuilds` maps each of the two tags to the `SERVERPROPERTY('ProductVersion')` its server reports, recorded when the tag is chosen.  `SqlServerContainerTests` fails on a tag of another form, on a tag not in the map, and on a server whose build differs from the map's.  Moving a tag changes the map in the same pull request, with everything rule 5 records again.

Root `AGENTS.md` rule 5 becomes the rule for both engines: a PostgreSQL image names a major and a minor; a SQL Server image names its update as above; moving the default image of either engine records again what was recorded on it, `tools/record-fixtures.sh` for that engine's fixtures, and from sub-phases 6.5 and 6.7 its end-to-end sidecars and its example's; moving a floor image changes the places that name it and runs the tests on it.

### The test project

`tests/SqlSource.Tests.SqlServer`, in the solution's `tests` folder, built as `tests/SqlSource.Tests.Postgres` is: `net10.0`, xunit v3, Shouldly, the same references to the tool, the generator and the shared test libraries, and the packages `Testcontainers.MsSql`, at the version of `Testcontainers.PostgreSql`, and `Microsoft.Data.SqlClient`, at the tool's version.

| Part | Is |
|----|----|
| `SqlServerContainer` | A collection fixture, `UsesSqlServer`: starts `Image`, creates the database `fixtures`, applies the fixture schema to it, and gives the connection string of that database.  Its default image is `DefaultImages.SqlServer`, a `public const string` on one line of the shape `DefaultImages.Postgres` has, since the scripts read it so; `ImageVariable` is `SQLSOURCE_TESTS_SQLSERVER_IMAGE`; `RecordedMajors` is `[14, 17]`; `Major`, `SkipBelow`, `ConnectionStringOf`, `ExecuteAsync` and `ScalarAsync` as PostgreSQL's have them |
| Applying a script | One file is split into batches at each line that holds `GO` alone, ignoring case and the white space around it, and each batch is run as one command through SqlClient, which has `QUOTED_IDENTIFIER` on where `sqlcmd` has it off.  `GO` with a count is refused |
| The schema | `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/schema.sql`: tables of the `dbo` schema and of a `billing` schema, an identity, a `rowversion`, a computed and a persisted computed column, an alias type, a view over an outer join, a system-versioned table, and a column of every type of the spike's table but `json` and `vector`, which 2017 lacks; a table with an `AFTER` trigger for sub-phase 6.2 |
| `SqlServerContainerTests` | The tag's form and build, above |
| `Describer/` | The container tests of the describer, one class for each subject |
| `Describer/FixtureCasesTests`, `FixtureVersionTests` | As PostgreSQL's: each case described live and compared with `expected.json`, `serverVersion` set to one value on both; recorded with `SQLSOURCE_RECORD_FIXTURES` set; a fixture recorded on another build than the default image's fails |
| `README.md` | Running the project and the floor run, the images and their pinning, the fixtures, and that SQL Server's images are x64 only and run under emulation on Apple silicon, slowly the first time while the image is pulled |

`RepositoryPaths` gains the SQL Server schema.  `tests/AGENTS.md` names the project and the files it compiles by exact path, as it does PostgreSQL's.

### CI and the scripts

- `tools/pull-test-images.sh` pulls the default image of each engine, read from `tests/SqlSource.Testing/DefaultImages.cs` by their line shape, and every image given as an argument, one `docker pull` each, and prints the time each took.  It is not a check; it is a step.
- `build.yml`'s `build` job names the two floor images once, as `env` of the job, `POSTGRES_FLOOR_IMAGE: postgres:14.24` and `SQLSERVER_FLOOR_IMAGE: mcr.microsoft.com/mssql/server:<the floor tag>`.  A step "Pull test images" runs `tools/pull-test-images.sh "$POSTGRES_FLOOR_IMAGE" "$SQLSERVER_FLOOR_IMAGE"` before the first step that runs a test.  "Test" runs the solution as today, the new project with it.  "Test on PostgreSQL 14" reads its image from the variable, and "Test on SQL Server 2017" follows it, `dotnet test --project tests/SqlSource.Tests.SqlServer --no-build` with `SQLSOURCE_TESTS_SQLSERVER_IMAGE: ${{ env.SQLSERVER_FLOOR_IMAGE }}`.  The builder measures the job and raises `timeout-minutes` to what it needs with room; the pull step makes the measure readable.
- `nightly.yml` needs no change to leave the project out; its comment and `.github/workflows/README.md` name both container projects.
- `pre-commit-validation.sh` is unchanged: `dotnet test --solution` runs the new project on the default image.  Root `AGENTS.md`'s rule 2 on the floor becomes both engines': after a change to a describer or its tests, run its project on its floor image too.
- `tools/record-fixtures.sh [postgres|mssql]` records one engine's fixtures, or both when given none; each engine's project, case folder, test class, image variable and container class are a row of a table in the script.  `tools/AGENTS.md` and `tools/README.md` say so.
- `tools/check-package.sh` wants the SqlClient assemblies in the tool's package, and `tools/check-tool-install.sh` proves that the closure loads, as it does Npgsql's.

### The describer

In `src/SqlSource.Tool/Describing/SqlServer/`, by the pattern and the rules of PostgreSQL's:

| Type | Is |
|----|----|
| `SqlServerDescriber` | `IQueryDescriber` for `SqlDialect.SqlServer`.  `SecretsOf` gives the `Password` of `SqlConnectionStringBuilder`, and nothing for a value it cannot read, never throwing |
| `SqlServerSession` | `IDescribeSession`: the exchange, the log, the connection holder, and `DescribeAsync`.  Its `Outcome` is built with the constructor, as `PostgresSession.OpenAsync`'s is |
| `SqlServerConnection` | Holds the connection's value and makes the one `SqlConnection` of the session on the first `live` call that needs it.  Disposed with the session; `ToString` leaves the value out |
| `SqlServerLive` | The `live` functions and every text the tool sends |
| `SqlServerCalls.cs`, `SqlServerCallNames` | The request and answer records, and the names of the calls |
| `DeclaredTypeText` (SQL Server's) | Reads a `-- param:` type against T-SQL's grammar of a type name |
| `ParameterCopies` | The rename of a reused parameter's occurrences and the rule that unifies its copies |
| `SqlServerTypes` | Builds a `SqlServerType` from a row of either procedure |
| `SqlServerErrors`, `ConnectionErrors` | A server's or the driver's error to a `DescribeFailure` |
| `SqlServerEvents` | Builds each event of the describer's log |

`CallRecordTests.Under` gains the namespace, and `DescriberFixtureTests.DescriberOf` the engine `mssql`.

### The calls of this sub-phase

| Method | Request | Answer |
|----|----|----|
| `open` | Nothing | `productVersion`, `SERVERPROPERTY('ProductVersion')`; `engineEdition`, `SERVERPROPERTY('EngineEdition')`; `driverVersion`, SqlClient's assembly informational version; `settings`: the database's `compatibility_level`, `ANSI_NULLS` and `QUOTED_IDENTIFIER` of the session, and the database's collation, as name and value; or `error` |
| `parameters` | `sql`, the text with every reused parameter's occurrences renamed; `params`, the declarations of the declared parameters, or null | `rows`, one for each parameter the server typed, with `name`, `suggested_system_type_name`, `suggested_max_length`, `suggested_precision`, `suggested_scale`, `suggested_user_type_schema`, `suggested_user_type_name` and `suggested_assembly_qualified_type_name`; or `error` |
| `probe` | `sql` and `params`, every copy and parameter declared | `binds`, true when `sp_describe_undeclared_parameters(@sql, @params)` raises nothing; or `error` for a failure that is not the batch's, a connection lost |
| `declared` | `params`, every declared parameter's declaration | `rows`, one for each, from describing `SELECT @a AS [1], @b AS [2], ...` under the declarations: the type and facets, and the user type's schema, name and assembly-qualified name; or `errors`, the error rows of the describe as number, type and message |

Each call is a `SqlCommand` of `CommandType.StoredProcedure` for the procedure, or of text that names the function with the user's text as an `nvarchar(max)` parameter.  `ServerError` is the `SqlException`'s errors in order, each number, class, state and message; a failure of the driver's, a connection refused, is a number and a fixed text from `ConnectionErrors`, never the driver's message, which can name the host.  Every `live` function catches `SqlException` and returns it as the answer's `error`; a cancellation passes through; anything else is a bug and `SQLSRC200`.

`ServerInfo`: `Version` is `productVersion`, which the sidecar's `serverVersion` holds; `Driver` is `Microsoft.Data.SqlClient`; `Settings` the four.  A server whose major is below 14 is `SQLSRC235` at `open`, unless `engineEdition` is 5, Azure SQL Database, or 8, Azure SQL Managed Instance, which report a version of their own and have both procedures.

### `DescribeAsync`

1. **The declarations.**  Each declared type is read against T-SQL's grammar of a type name before anything is sent: a name of one or two parts, each a plain identifier, a bracketed one with `]]` for a bracket, or a double-quoted one with `""` for a quote; then an optional `(` with `max`, or one or two integers separated by a comma, and `)`; and nothing else, no comment, no `;`, no second `(`.  So `int`, `nvarchar(50)`, `varchar(max)`, `decimal(18,2)`, `dbo.Email` and `[billing].[Amount]` are read, and `int; DROP TABLE x` and `int) --` are refused.  A refused text is `SQLSRC228` with its reason and nothing is sent.  The grammar is a first refusal; the spike shows that `@params` is a declaration list to the server, and that a text that is not one is an error, `SQLSRC228` too.
2. **The copies.**  The parameters of the query's list that are not declared and that the lexer finds more than once are reused.  `ParameterCopies` renames each occurrence of a reused parameter's `Parameter` lexemes to a copy name, the parameter's name and a suffix, numbered in order of appearance, chosen so that it is no name the text already holds, compared ignoring case.  `DECLARE`d variables, which are in no list, are left as written.
3. **`parameters`**, with the renamed text and the declarations.  An `error` ends the describe with the failure `SqlServerErrors` gives for it.
4. **The unify**, for each reused parameter, over the rows of its copies:
   - every copy typed and every type the same, by name, facets and user type: that type;
   - otherwise, for each copy, a `probe` with that copy declared `xml` and every other copy and parameter declared at the type `parameters` gave it, the declared ones as declared; and when it binds, a second `probe` with the copy declared `int`.  A copy that binds both ways constrains nothing and is ignored, which the spike found for `@p IS NULL`, `@p IS NOT NULL` and `CASE WHEN @p IS NULL`; a copy in a context of `xml` fails as `int`, and one in any other typed context fails as `xml`.  The probe asks `sp_describe_undeclared_parameters`, which does not look at the shape of a result, so a query whose paths return two shapes is still probed right, and 6.2 reports the shapes.  Of the copies left, every type the same: that type; none left: `SQLSRC226`; two types: `SQLSRC262`, naming the parameter and the first two types in order of appearance.
   The type's `typeSource` is `inferred-from-copies`.
5. **`declared`**, when a parameter is declared.  Its `errors` are `SQLSRC228`: 2715 and 2724 that the database lacks the type, any other that the text is not a type.
6. **The lists agree.**  The server's undeclared parameters, each copy taken back to its parameter, are the query's parameters that are not declared, compared ignoring case.  A parameter of the list the server did not report, a `CREATE PROCEDURE p @id int` that the lexer reads as a parameter, or one the server reported that the list lacks, a name the lexer took for a `DECLARE`d variable, is `SQLSRC231` with each name and which side lacks it, at the step `check parameters`; `docs/tech-debt/TD-0004`'s last row says the tool now catches it.
7. Each parameter of the list gets its type: declared, from `declared`, `typeSource` `declared`; reused, from the unify; otherwise from its row, `inferred`.

The description is the parameters, `ResultKind.None`, and null for everything else, until sub-phase 6.2.

A parameter used once, alone in a context that types nothing, `WHERE @p IS NULL`, keeps the server's answer, `int`: the server's answer is recorded, and the `xml` probe is the tool's way to choose among copies, not a second opinion on the server.  The manual says so beside the optional filter.

### Types

`SqlServerTypes` builds a `SqlServerType` from a row: `name` is the system type name verbatim, `maxLength`, `precision` and `scale` verbatim, and `userType` when the row names a user type, its schema, name and, for a CLR type, assembly-qualified name.  `sysname` is a user type of `sys` over `nvarchar(128)`, `rowversion` is `timestamp`, as the spike found.  `bindName` is never written for SQL Server: the sidecar format left it to this phase, and phase 8 binds a type `SqlDbType` names by `SqlDbType` with its facets, and a CLR type by the `userType` the entry already holds; a table type is out of the epic.  The sidecar format design's row for `bindName` is updated to say so.

### Errors

| Server error | Cause | The tool reports |
|----|----|----|
| 11502, 11503, 11506, 11507, from `parameters` | The server cannot deduce a parameter's type: no typing context, two untyped parameters in one expression, two possible types, or no type that makes the query valid | `SQLSRC226`; `help:` add a `CAST` around a use, or declare the type with `-- param:` |
| 2715, 2724, from `declared` or `parameters` | A declared type the database lacks | `SQLSRC228` |
| 102 inside the declarations | A declaration that is not a type | `SQLSRC228` |
| 11508 | A parameter used twice, which the rename prevents | A bug: `SQLSRC225` with the server's line, so that it is seen and reported |
| Any other, 11501 and the error before it among them | The query is not valid against this schema, a use of a declared type included | `SQLSRC225` with every error the server gave as a `server:` line, the cause first |
| A connection or login failure at `open` | The database cannot be opened | `SQLSRC224` with a fixed line, never the driver's text |

The numbers are from Microsoft's list of errors 11000 to 12999, among the outline's sources; 11504 and 11505, a discovered type that is not valid, are `SQLSRC225`.  The describer maps by number and never by message.  The step of each failure is `describe parameters`, or `resolve type` for `declared`.

### Events

`mssql.parameters`, `mssql.probe` and `mssql.declared`, each with its request and its answer whole, as the outline's Diagnostics decides; the run's `database.open` holds the four settings.  Every string goes through the session's redaction, as every PostgreSQL event's does, and the new events get their cases in `RedactionTests`.

### Diagnostics

| Id | Is |
|----|----|
| `SQLSRC262` | New, in `ToolDiagnostics`: "The copies of the parameter '{0}' of the query '{1}' have the types '{2}' and '{3}'", at the step `describe parameters`, with `help:` to declare the parameter with `-- param:` |
| `SQLSRC235` | Its message names the engine and the floor from the describer: "The database '{0}' runs {1} {2}, and the tool supports {3} and later", PostgreSQL's arguments supplied as before |
| `SQLSRC231` | Its title and message name the reader instead of Npgsql: "{0} reads the parameters of the query '{1}' differently from SqlSource: {2}", `Npgsql` for PostgreSQL and `SQL Server` here |
| `SQLSRC224`, `SQLSRC225`, `SQLSRC226`, `SQLSRC228` | Reused, their sections in `docs/manual/diagnostics.md` gaining SQL Server's causes |

### The first fixtures

Under `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/`, recorded on the default image, each a statement without rows so that sub-phase 6.2 changes none of their entries:

| Case | Proves |
|----|----|
| `update-inferred` | Two inferred parameters, one of an alias type |
| `insert-declared` | A declared `nvarchar(50)`, a declared alias type and an inferred `decimal` |
| `delete-declared-only` | A parameter that only a `-- param:` declares, and a token with a default |
| `update-optional-filter` | `(@name IS NULL OR name = @name)`: the copies, the probe, `inferred-from-copies` |
| `update-copies-agree` | A parameter compared to two columns of one type |
| `update-wide-types` | `TOP (@n)` and a `LIKE`, recorded as the server widens them |

The refusals, `SQLSRC226`, `SQLSRC228`, `SQLSRC262` and `SQLSRC225`, are container tests and not cases, as PostgreSQL's are.

## Testing

| Where | Cases |
|----|----|
| `ParameterCopiesTests` (tool tests) | A parameter once is not renamed; twice and three times are; two reused parameters; a name in another case; a copy name that the text already holds is skipped; a `DECLARE`d variable, a string, a comment and a bracketed identifier left alone; the unify's every branch from answers made by the test: all agree, one ignored by both probes, one that binds as `xml` and not as `int`, all ignored, two left that differ, a copy untyped; the lists' check both ways |
| `DeclaredTypeTextTests` (SQL Server's) | Each accepted form; `;`, `--`, `/*`, a quote or bracket left open, `int) --`, `int foo`, `nvarchar(`, `nvarchar(max, 2)`, a third part, a line break, refused with its reason |
| `SqlServerTypesTests` | Each row of the spike's type table built from rows made by the test, `sysname`, an alias type and a CLR type among them |
| `SqlServerErrorsTests` | Each row of the error table; each `server:` line in the server's order; a login failure's text nowhere |
| `SqlServerContainerTests` (container) | The tag's form; the server's build equals the map's |
| `SessionTests` (container) | `open` gives the version, the edition's acceptance, SqlClient as the driver and the four settings; a wrong password, a missing database and an unreachable host are `SQLSRC224` with no part of the value in any text |
| `ParameterTests` (container) | Every row of the spike's parameter table that types, inferred; a declared base type, alias type and `varchar(max)`; a declared type checked against a use that rejects it is `SQLSRC225`; `SELECT @p` and `COALESCE(@p, name)` are `SQLSRC226`; `-- param: @p dbo.Nope` is `SQLSRC228`; the optional filter typed through its copies, in a `SELECT`, an `UPDATE` with no `OUTPUT`, and an `IF` whose paths return two shapes; a reused parameter of an `xml` column typed `xml`; `@p IS NULL OR @p IS NOT NULL` is `SQLSRC226`; `CREATE PROCEDURE p @id int AS ...` is `SQLSRC231`; `id = @p AND name = @p` is `SQLSRC262`; a `DECLARE`d variable is no parameter; a text of two statements with a `DROP` of a table of the schema leaves the table there, through `parameters` and through `probe`, and a declaration with a `DROP` in it is `SQLSRC228` and leaves it too |
| `FixtureCasesTests` (container), `DescriberFixtureTests` (tool tests) | Every case live and replayed equals `expected.json`; a capture with a call removed fails with `CaptureMissException` |
| `SecretTests` | A `SqlServerDescriber` given a value it cannot open, with a marker word in its password: `SQLSRC224` printed and the word nowhere |
| `ToolDiagnosticsTests`, the diagnostics document test | `SQLSRC262` and the new message of `SQLSRC235` |

## Documentation

- `src/SqlSource.Tool/AGENTS.md`: `Describing/SqlServer/`; the user's text and the declarations reach the server only as parameter values of the two procedures, and the canary tests that prove it; the copies and the `xml` probe; the calls and their records are a contract with the captures.
- `docs/architecture/`: a page for the SQL Server describer, as `postgres-describer.md` is PostgreSQL's, with the parameters' part; `docs/architecture/README.md` lists it.
- `CONTRIBUTING.md`: the third container project, its images and its variable, the emulation on Apple silicon and the first pull's minutes, the pull step of CI, and `tools/record-fixtures.sh`'s argument.
- `tests/README.md`, `tests/AGENTS.md`, `tests/SqlSource.Tests.SqlServer/README.md`, `tools/README.md`, `tools/AGENTS.md` and `.github/workflows/README.md` for what each holds of the above.
- Root `AGENTS.md`: rules 2 and 5.
- `docs/manual/diagnostics.md`: `SQLSRC262`, and `SQLSRC224` to `SQLSRC228`, `SQLSRC231` and `SQLSRC235` for SQL Server.
- `docs/tech-debt/TD-0004`: its last row is caught by the lists' check.  No other user page changes: the tool still cannot describe SQL Server.
- The sidecar format design's `bindName` row for SQL Server.
- `docs/tech-debt`: `TD-0034` if a SqlClient message that reaches a failure or the log can name the host or the user.
- The epic outline: the row of 6.1.

## Order of work

One pull request, one commit for each step.  Each leaves `./pre-commit-validation.sh` passing.

1. The epic outline's row of 6.1 says In progress.
2. The test project, `SqlServerContainer` with the pinned images and their builds, the fixture schema, the packages pinned in `Directory.Packages.props`, `SqlServerContainerTests`, the solution, root `AGENTS.md` rule 5, `tests/` documents and `CONTRIBUTING.md`.
3. `tools/pull-test-images.sh`, and `build.yml`'s pull step, floor variables and floor run, with its timeout measured.
4. The tool's reference to SqlClient and the package checks; `SqlServerDescriber`, `SqlServerSession`, `SqlServerConnection`, `open`, `SQLSRC224` and `SQLSRC235`'s message.
5. `DeclaredTypeText`, `ParameterCopies`, `parameters`, `probe`, `declared`, the lists' check, `SqlServerTypes`, `SqlServerErrors`, `SQLSRC262` and `SQLSRC231`'s message.
6. The events.
7. `FixtureCasesTests`, `FixtureVersionTests`, `tools/record-fixtures.sh`, the cases and `DescriberFixtureTests`.
8. The documents, and the outline's row set to Done.

The plan counts the files against the limit of 150; six cases of three files are a small part of it.

## Left to the builder

- The exact tags, chosen by the rule above on the day the builder starts, and their builds.
- The suffix of a copy name, within the rule that it cannot collide.
- Whether `declared` describes all declarations in one call, as above, or one each, if one call cannot tell which declaration a 2715 belongs to.
- The names of classes and tests.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- The rename-and-unify of the outline is amended by the spike: a copy is ignored when it binds declared both `xml` and `int`, the probe runs only when copies disagree, and a parameter used once is never probed.
- The lexer's list and the server's are checked against each other, both ways, with `SQLSRC231` generalized, where a crash or a silent omission would follow otherwise.
- A declared parameter's type is resolved by describing `SELECT` of the parameters under the declarations, where the outline named `sys.types`: the server parses the declaration as it will at run time, an alias type included.
- `bindName` is never written for SQL Server.
- An Azure SQL Database or Managed Instance is accepted whatever its version.  Neither is tested; the manual says that describing is tested on SQL Server 2017 and 2025.
- CI's pull step is a script, `tools/pull-test-images.sh`, that reads the default images where the scripts read them and takes the floors from the workflow, so that no tag is written twice more.
- `tools/record-fixtures.sh` with no argument records both engines.
- `SQLSRC235`'s message is generalized rather than given a SQL Server twin.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.  When this spec was written the repository had no `v*` tag.
