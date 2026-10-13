# Query generation, phase 3.3: columns, the catalog and the gate - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The PostgreSQL describer gives a query its columns: their types with facets, their origins from the catalog, the nullability the catalog can answer, the outer-join gate with the heuristic as its answer, and the check that Npgsql reads the query as the lexer does; registered nowhere.

**Architecture:** Three calls join `open`, `prepare`, `types` and `deallocate` behind the `IDescribeExchange`: `columns` describes the prepared statement through `EXECUTE` under `CommandBehavior.SchemaOnly`, `tables` reads `pg_class` and `pg_attribute` once for every table the columns name, and `check` describes the original SQL through Npgsql's own parser.  `types` moves after `tables` and is asked once, for the parameters, the columns and the origin attributes together.  The parts that use no driver, the facets, the choice of a column's type, its origin, its nullability and the gate, are static classes of their own, tested in `tests/SqlSource.Tool.Tests`; the session is tested there over a replay the test writes, and against a real server in `tests/SqlSource.Tests.Postgres`.

**Tech Stack:** C# (`LangVersion` `preview`); the tool on `net8.0`, the tests on `net10.0`; Npgsql 10.0.3; Testcontainers.PostgreSql 4.16.0; xunit v3 on Microsoft.Testing.Platform with Shouldly; Docker.

**Spec:** `docs/superpowers/specs/2026-10-12-query-generation-phase-3-3-columns-catalog-and-gate-design.md`.  Read it with this plan.  The epic outline, `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, is kept current and wins where the two disagree.

**Branch:** `claude/query-generation-phase-3-3-columns-catalog-and-gate`, made from `main` in Task 1.  This file is untracked until Task 1 commits it.

## Where the plan settles what the spec leaves open

A spike against PostgreSQL 14.24 and 18.4 through Npgsql 10.0.3 was run for this plan; the two servers answered alike but for `numeric(2,-3)`, which 14 refuses.  The first five items are the readings the user approved before the plan was written.  The rest are the spike's findings and the choices the spec leaves.

Approved readings:

- **`check` is sent as an `NpgsqlBatch` of one command, not as an `NpgsqlCommand`.**  The epic outline records why: Npgsql splits the text of an `NpgsqlCommand` at each `;` its own lexer finds, and nothing but `SchemaOnly` would stand between a text the tool's lexer read as one statement and its second part.  The SQL is cut at its trailing `;` first, as `ParameterPlaceholders.Rewrite` cuts it.
- **A `$n` the SQL writes itself is tried and recorded, with no new diagnostic.**  The spike's answer: `SELECT 1 FROM users WHERE id = @id AND name = $2` passes the check, since Npgsql sends `$1` for `@id`, leaves `$2` alone, and the server types it.  The first item of `docs/tech-debt/TD-0043` stays open and gains that finding.
- **The `select-view` fixture selects from `active_users`.**
- **`schema.sql` is not changed.**  `ColumnTests` makes the one table it needs for types the fixture schema lacks, and a cast in a select list keeps its modifier in the row description, so most of those types need no table at all.  A materialized view and a foreign table are held by `ColumnNullabilityTests` over answers the test writes; the container tests make a materialized view too, since it costs one statement.
- **The spec's "Out of scope" lists the table match twice.**  The spec stays as written.

What the spike found:

- **`GetColumnSchema` without `KeyInfo` gives everything `columns` needs, and asks the server nothing more.**  `TypeOID`, `TableOID`, `ColumnAttributeNumber`, `ColumnSize`, `NumericPrecision` and `NumericScale` are filled from the row description.  The server's statement log shows no statement between the `PREPARE` and the `DEALLOCATE`.  `NpgsqlDataReader.GetColumnSchemaAsync` returns `DbColumn`, whose items are `NpgsqlDbColumn`: the live function casts them.
- **Describing `EXECUTE` runs nothing.**  An `INSERT ... RETURNING` described so, and through the check, left its table empty.
- **`EXECUTE` is described whatever the number of its arguments.**  A statement with a `$2` of its own is described with one `NULL`.  The request holds the count of the query's list, as the spec says.
- **Npgsql gives a `numeric` no scale when the scale is zero.**  `numeric(18,0)` and `numeric(5)` arrive with a precision and a null scale, and `format_type` spells both with `,0`.  So a `numeric` with a precision and no scale has the scale 0.  This is a third decoding beside the spec's two.
- **`numeric(2,-3)` arrives with the scale 2045, and `interval year to month` with the precision 65535**, as the spec says.  `interval day to second(3)` arrives with the precision 3 and nothing of its fields: it is recorded as `interval(3)`.
- **Npgsql gives an array the facets of its element**: `varchar(10)[]` has the size 10.  `ColumnFacets` takes facets by the internal name of the column's own type, so an array gets none, which is the spec's choice.
- **An array of a domain is not flattened.**  A column of `email_t[]` has the OID of `_email_t`, whose element is the domain, so it is recorded as `public.email_t[]` with no help from its origin.  A column of a domain is flattened to its base with the domain's modifier, as the spec says, and so is a domain over a domain, to the base at the end of the chain.
- **A system column has its table's OID and an attribute number below zero**; a column of an expression, of a `UNION` and the `USING` column of a `FULL JOIN` have the table OID 0; the `USING` column of an inner or a left join has the left table's; a subquery and a CTE keep origins; a column of a view has the view's OID and `attnotnull` false.
- **`'x'` and `NULL` in a select list are `text`**, and `ROW(1,2)` is `record`.  So `unknown` is no column type a current server gives, and `SQLSRC229` is tried with `ROW(1,2)`.
- **A pseudo-type as a parameter's type does not fail `columns`.**  `PREPARE s (record) AS SELECT $1` is described, with a column of `record`.  The parameters are typed before the columns are built, so such a query stays `SQLSRC228` or `SQLSRC226` and is not `SQLSRC229`.
- **Npgsql refuses a type name before it sends anything, with one of two exceptions.**  `NotSupportedException` for a name it does not know, `nope`, `"char"`, and any name written with quotes, `"My Schema"."Status"`; `InvalidCastException` for a pseudo-type and for a domain over a domain.  It resolves the types before it reads the SQL, and it refuses a parameter the SQL does not reference too.
- **Npgsql refuses a text with a `;` that something follows with a `NotSupportedException` as well**, when the command has a named parameter, having sent nothing; without a parameter the server refuses it, `42601`.  So the exception's type does not say which of the two it is.  The live function asks again for each parameter alone, with a text of the tool's own, `SELECT $1`: the first parameter that is refused is the one that cannot be bound, and when none is, the exception was about the text.
- **`SQLSRC232` is reachable from a `.sql` file**, by three roads: a type in a schema or with a name that needs quotes, since Npgsql takes `My Schema.Status` and not `"My Schema"."Status"`; a parameter the server types as `"char"`, which a comparison with `pg_class.relkind` gives; and a domain over a domain.  Its help says to declare the parameter as a type Npgsql binds and to cast it where it is used, and names the quotes when the name has them.  `docs/tech-debt` records all three for phase 8.
- **Npgsql flattens the type of a parameter that is an array of a domain**, to an array of the base, where the server does not.  So `SELECT @p AS p` with `-- param: @p email_t[]` is `SQLSRC231`: the column is `character varying[]` for Npgsql and `email_t[]` for SqlSource.  That is a true difference in what the query returns, reported as the check reports any other; `docs/tech-debt` records that the help line speaks of a `@` and not of this.
- **Npgsql reads a parameter's name ignoring case**, as the tool does, and reads `@` inside a dollar-quoted string, an `E''` string, `@>` and `::` as the tool does.

Choices:

- **The live functions move out of `PostgresSession` first, in a commit of their own (Task 2).**  The session is 430 lines and would pass 800 with three more calls.  `PostgresLive` holds the SQL the tool sends and the seven functions that run on the connection; the session keeps what is asked, in which order, and what is made of the answers.  The pull request therefore has ten commits, one more than the spec's nine steps.
- **The captures of the four fixtures of sub-phase 3.2 are recorded again in Task 3 and in Task 7**, and not only in the fixtures' task: a describe asks `columns` from Task 3 and `check` from Task 7, and a replay of an older capture is a `CaptureMissException`.  Their `expected.json` does not change.
- **The describer is whole only after Task 7.**  It is registered nowhere, so each commit may describe less than the next: after Task 3 a column has its type and says `no-origin`; after Task 4 it has its origin and still says `no-origin`; Task 5 gives it its nullability; until Task 6 a column of a pseudo-type throws.
- **The events come in Task 8**, after every call exists, as they did in sub-phase 3.2.  Until then the three new calls write no event.
- **`types` is not asked when there is no OID to ask for**: a statement with no parameter and no column.
- **`tables` is not asked when no column names a table.**
- **`tables` answers a table that has no column**, through a left join, so that `SELECT ctid FROM t` on a table without columns finds its table.  Its answer has an `error`, as every call that can fail on the server has.
- **`driverError` is a record, `parameter` and `message`**, and not a text: `SQLSRC232` names the parameter, and Npgsql's message does not.
- **A column of `check` holds `dataTypeName` beside `name` and `typeOid`**: Npgsql's own name for the type, which the message of `SQLSRC231` quotes.
- **What differs is said in one of four forms**, and none of them counts parameters: nothing gives the number of placeholders Npgsql found.  `the result has 2 columns for Npgsql and 1 for SqlSource`; `column 3 is named 'x' for Npgsql and 'y' for SqlSource`; `column 3 is text for Npgsql and integer for SqlSource`; or the message of the error.  The message says `SqlSource` where the spec's examples say `the lexer`, as the descriptor's does.
- **An error of `columns`, of `tables` and of `check` has no `at character` line.**  The server counts in a text the user did not write: the tool's `EXECUTE`, its query of the catalog, and Npgsql's own rewrite of the SQL.
- **The type compared in the check is the one the protocol reported**, and its name in the message is `format_type` of it: for a domain column that is the base, which is what both describes see.
- **`OuterJoinGate.IsGated` takes the SQL and lexes it**, as `ParameterPlaceholders.Rewrite` does.  A lexeme has a span and no text, so the spec's `IsGated(lexemes)` would need the SQL as well.
- **`ParameterPlaceholders.AsWritten` is the SQL of the check**: cut at its trailing `;` as `Rewrite` cuts it, with each `@name` left as written.
- **A relation that is no view, materialized view or foreign table is a table**: `r` and `p`, and anything a later server adds.
- **`postgres.column` has a `query` member first.**  The spec lists none, and a line of the log should say whose column it is.
- **The tool's tests of the comparison are `CheckVerdictTests`**; `NpgsqlCheckTests` is the container's, as the spec names it.
- **`ParameterTests` is the proof that every case of it passes the check.**  From Task 7 each of its describes runs the check, so a case that Npgsql reads otherwise fails there.  If one does, stop and tell the user: it is a finding, and the test is not to be weakened.
- **`numeric(2,-3)` is tried on PostgreSQL 15 and later**, through `Assert.SkipWhen` on 14.

## Global Constraints

- The supported floor is PostgreSQL 14.  The default image is `postgres:18`; `SQLSOURCE_TESTS_POSTGRES_IMAGE` overrides it.
- The describer is registered nowhere: `ToolHost.Create` is unchanged, and the released tool says `SQLSRC216` for `postgres`.
- A describer returns a `DescribeFailure` for what a server or a user can cause and never throws for it.  A throw is a bug, `SQLSRC200`.
- Every call to the server goes through `OpenRequest.Exchange`.  The connection is made inside a `live` function and nowhere else: under a replay nothing of Npgsql runs.
- `PostgresConnection` makes every command and every batch.  A text that holds a user's SQL is sent as a batch of one command and never as an `NpgsqlCommand`.
- A request and an answer are sealed records of `string`, `int`, `long`, `bool`, nullable ones, `IReadOnlyList<T>` of them and records of the same kind.  A request holds nothing of a connection.  A call that can fail has an answer with a nullable `Error`.  An OID is a `long`.
- The methods are `open`, `prepare`, `columns`, `tables`, `types`, `check` and `deallocate`.  A method's name and a record's member names are a contract with the captures: none is renamed once a task has recorded with it.
- The order of a describe is: the statement check and the placeholders; `prepare`; `columns`; `tables`; `types`; the parameters typed; the columns built; `check`; `deallocate` in the `finally`.
- The facets: `length` for `bpchar`, `varchar`, `bit`, `varbit`; `precision` for `numeric`, `time`, `timetz`, `timestamp`, `timestamptz`, `interval`, with 65535 on `interval` read as none; `scale` for `numeric`, with 1024 or more read as the scale less 2048, and with none read as 0 when there is a precision.  Any other type: none.
- The gate's words are `LEFT`, `RIGHT`, `FULL`, `OUTER`, `ROLLUP`, `CUBE` and `GROUPING`, as whole words ignoring case, in `Text` lexemes.
- `plan` is `not-needed` for a query with rows that is not gated and `unavailable` for one that is; `matchesTable` is `null` and `tableMatch` is `no-origin`.  A query without rows has none of the three.
- A column's nullability is never `false` unless the catalog says `NOT NULL`, the origin is a table, and the query is not gated.
- The ids are `SQLSRC229`, `SQLSRC231` and `SQLSRC232`, with the titles and messages of the spec.
- A new descriptor touches four places in one commit: the descriptor and `ToolDiagnostics.All`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/diagnostics.md`, and the tests of the descriptors.
- Nothing the describer returns or logs holds the connection's value or a part of it.
- Nothing in the tool reads `DateTime` or `Stopwatch`: the clock is `OpenRequest.Time`.
- `README.md`, `CONTRIBUTING.md`, `docs/publishing.md` and `src/SqlSource.Tool/README.md` are not touched.  Nothing is expected for `docs/deferred`.
- `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/schema.sql` is not changed.
- The build treats warnings as errors with `AnalysisLevel` `latest-all`, Roslynator and Sonar.  Fix what it reports in the code; never suppress.  If a check is wrong, stop and tell the user.
- `ImplicitUsings` is off: a file names every namespace it uses.
- Comments say what a thing is for, in the plain declarative style of the files around them, with two spaces after a full stop.  Types under `src/` have XML documentation; test files use `//` comments.
- Test names are `Method_Scenario_Outcome`; assertions are Shouldly's.  A test takes its token from `TestContext.Current.CancellationToken`.
- Every commit passes `./pre-commit-validation.sh`, which needs Docker running and starts a PostgreSQL container.  Run `./format.sh`, then the validation, then commit in a command of its own.  Never work around the hook.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

How a test class is run by itself, here and in every task:

```bash
dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.ColumnFacetsTests"
dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.ColumnTests"
```

## Review Focus

- **A text that Npgsql would split, in the check.**  `SELECT @id AS §$x$; DROP TABLE t; --$x$` is one statement to the tool's lexer and two to Npgsql.  A user expects an error at the query and the table still there; not a second statement run, and not `SQLSRC200`.  Task 7, the container's `CheckAsync_TextNpgsqlWouldSplit_AnswersAnErrorAndRunsNothing`.
- **A `numeric` with a scale of zero.**  `numeric(18,0)` is the commonest money and id type after `numeric(18,2)`.  A user expects `numeric(18,0)`, as the database says, not `numeric(18)` with no scale.  Task 3, `Of_NumericWithAPrecisionAndNoScale_HasTheScaleZero` and the container's `Describe_CastWithAModifier_IsSpelledAsFormatTypeSpellsIt`.
- **A system column, and a table with no column of its own.**  `SELECT ctid, id FROM users` and `SELECT ctid FROM t` on an empty `CREATE TABLE t ()`.  A user expects a column without an origin, not a throw that the catalog's answer lacks an attribute.  Task 4, `Find_SystemColumn_IsNoOrigin` and the container's `Describe_SystemColumns_HaveNoOrigin`.
- **A parameter Npgsql cannot bind.**  A type in a schema whose name needs quotes, a comparison with `pg_class.relkind`, a domain over a domain, and such a type on a declared parameter the SQL never uses.  A user expects `SQLSRC232` that names the parameter and says what to write; not a `NotSupportedException` through the run.  Task 7, the container's `Describe_ParameterNpgsqlCannotBind_IsSqlsrc232NamingIt`.
- **A connection that is lost after `prepare`.**  The first call after it is now `columns`.  A user expects `SQLSRC225` at the query and a run that goes on to the next database; the `deallocate` of the `finally` must replace nothing.  Task 3, the container's `Describe_ServerEndsTheSessionBetweenPrepareAndColumns_IsSqlsrc225AndNoThrow`.

## File structure

Created under `src/SqlSource.Tool/Describing/Postgres/`:

| File | Holds |
|----|----|
| `PostgresLive.cs` | The SQL the tool sends and the function that runs each call on the connection.  Task 2 moves four in; Tasks 3, 4 and 7 add `columns`, `tables` and `check`. |
| `ColumnFacets.cs` | A column's facets from what Npgsql gives, by the internal name of its type. |
| `PostgresColumns.cs` | One `SidecarColumn` from a row of `columns`, its origin and the types' rows. |
| `ColumnOrigin.cs` | The table and the attribute a column comes from, and the search for them in the answer of `tables`. |
| `ColumnTypes.cs` | Which type a column has: the origin's domain or the protocol's. |
| `ColumnNullability.cs` | The five rows of the spec's table. |
| `OuterJoinGate.cs` | Whether a query holds one of the seven words. |
| `NpgsqlCheck.cs` | What the answer of `check` says against the answer of `columns`. |

Modified there: `PostgresCalls.cs` (the records of three calls), `PostgresSession.cs`, `PostgresConnection.cs` (`BatchOfOneAsync`), `PostgresErrors.cs`, `PostgresEvents.cs`, `ParameterPlaceholders.cs` (`AsWritten`).

Modified elsewhere under `src/`: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `src/SqlSource.Tool/AGENTS.md`.

Created under `tests/SqlSource.Tool.Tests/Describing/Postgres/`: `ColumnFacetsTests.cs`, `PostgresColumnsTests.cs`, `ColumnOriginTests.cs`, `ColumnTypeTests.cs`, `ColumnNullabilityTests.cs`, `OuterJoinGateTests.cs`, `CheckVerdictTests.cs`.  Modified there: `PostgresSessionTests.cs`, `PostgresEventsTests.cs`, `PostgresErrorsTests.cs`, `ParameterPlaceholdersTests.cs`.  Modified at the root of the tool's tests: `PostgresScript.cs`, `PostgresRows.cs`.  Modified in `tests/SqlSource.Tool.Tests/Fixtures/`: `DescriberFixtureTests.cs`, the four captures, and eight new cases.

Created under `tests/SqlSource.Tests.Postgres/Describer/`: `ColumnTests.cs`, `NpgsqlCheckTests.cs`.  Modified there: `Sessions.cs`, `ParameterTests.cs`, `SessionTests.cs`.

Modified in `tests/SqlSource.Tests/Diagnostics/`: `ToolDiagnosticsTests.cs`.

Documents: `docs/diagnostics.md`, `docs/tech-debt/README.md`, `docs/tech-debt/TD-0043-small-gaps-of-the-postgresql-describers-parameters.md`, two new items there, and the epic outline.

---

### Task 1: The branch, the plan and the outline

**Files:**
- Create: `docs/superpowers/plans/2026-10-13-query-generation-phase-3-3-columns-catalog-and-gate.md` (this file, already written)
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`

**Interfaces:**
- Consumes: nothing.
- Produces: the branch every later task commits to.

- [ ] **Step 1: Make the branch**

```bash
git switch -c claude/query-generation-phase-3-3-columns-catalog-and-gate main
```

- [ ] **Step 2: Set the row of 3.3 to In progress**

In `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`, in the table of sub-phases, the row that begins `| 3.3 Columns, the catalog and the gate | Not started |` becomes `| 3.3 Columns, the catalog and the gate | In progress |`.  Nothing else of the row changes.

- [ ] **Step 3: Validate**

Run: `./pre-commit-validation.sh`
Expected: every step `passed`.

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/plans/2026-10-13-query-generation-phase-3-3-columns-catalog-and-gate.md docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
```

```bash
git commit -m "Plan sub-phase 3.3, the columns, the catalog and the gate

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The live functions in a file of their own

A move and nothing else: no test changes, and every test passes as it did.

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`
- Modify: `src/SqlSource.Tool/AGENTS.md`

**Interfaces:**
- Consumes: `PostgresConnection`, the records of `PostgresCalls.cs`, `PostgresSession.VersionOf`.
- Produces: `internal sealed class PostgresLive(PostgresConnection connection)` with `Task<PostgresOpenAnswer> OpenAsync(CancellationToken)`, `Task<PostgresPrepareAnswer> PrepareAsync(PostgresPrepareRequest, CancellationToken)`, `Task<PostgresTypesAnswer> TypesAsync(PostgresTypesRequest, CancellationToken)`, `Task<PostgresDeallocateAnswer> DeallocateAsync(PostgresDeallocateRequest, CancellationToken)`.  `PostgresSession` holds one in the field `_live`.  Later tasks add `ColumnsAsync`, `TablesAsync` and `CheckAsync` to it.

- [ ] **Step 1: Create `PostgresLive.cs`**

Move these members of `PostgresSession` into it, bodies unchanged but for the receiver, `Connection.` becoming `connection.`: the field `SettingNames`; the constants `OpenSql`, `ParameterTypesSql` and `TypesSql`, each with its comment; and the four functions, renamed: `OpenLiveAsync` to `OpenAsync`, which loses its `connection` parameter and is no longer static; `PrepareLiveAsync` to `PrepareAsync`; `TypesLiveAsync` to `TypesAsync`; `DeallocateLiveAsync` to `DeallocateAsync`.  `OpenAsync` calls `PostgresSession.VersionOf`, which stays where the tests name it.  The comment `// In each "live" function, what the server can cause is the answer's error. ...` becomes the remarks of the class.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// What runs on a session's connection: the SQL the tool sends, and the function that asks the server each call of
/// the describer.  A function of this class is the <c>live</c> one of its call, and under a replay none runs.
/// </summary>
/// <remarks>
/// In each function, what the server can cause is the answer's error.  A cancellation passes through, and
/// anything else is a bug.
/// </remarks>
internal sealed class PostgresLive(PostgresConnection connection)
{
    // SettingNames, OpenSql, ParameterTypesSql and TypesSql, moved as they are.

    /// <summary>Asks <c>open</c>.</summary>
    public async Task<PostgresOpenAnswer> OpenAsync(CancellationToken token)
    {
        // The body of PostgresSession.OpenLiveAsync.
    }

    /// <summary>Asks <c>prepare</c>.</summary>
    public async Task<PostgresPrepareAnswer> PrepareAsync(PostgresPrepareRequest asked, CancellationToken token)
    {
        // The body of PostgresSession.PrepareLiveAsync.
    }

    /// <summary>Asks <c>types</c>.</summary>
    public async Task<PostgresTypesAnswer> TypesAsync(PostgresTypesRequest asked, CancellationToken token)
    {
        // The body of PostgresSession.TypesLiveAsync.
    }

    /// <summary>Asks <c>deallocate</c>.</summary>
    public async Task<PostgresDeallocateAnswer> DeallocateAsync(
        PostgresDeallocateRequest asked,
        CancellationToken token
    )
    {
        // The body of PostgresSession.DeallocateLiveAsync.
    }
}
```

The four comments in the bodies above stand for code that exists: after the move, `PostgresLive.cs` holds no such comment and `PostgresSession.cs` holds none of the moved members.

- [ ] **Step 2: Give the session its `PostgresLive`**

In `PostgresSession.cs`: add the field `private readonly PostgresLive _live;`; the constructor takes it after `connection` and sets it; `OpenAsync` makes it and hands it on; each wrapper names its function.

```csharp
    private PostgresSession(
        OpenRequest request,
        IRunLog log,
        PostgresConnection connection,
        PostgresLive live,
        ServerInfo server
    )
    {
        _exchange = request.Exchange;
        _time = request.Time;
        _log = log;
        _live = live;
        Connection = connection;
        Server = server;
    }
```

In `OpenAsync`, after `var connection = new PostgresConnection(request.Connection);`:

```csharp
        var live = new PostgresLive(connection);
```

the `live` argument of `open` becomes `(_, token) => live.OpenAsync(token)`, and the session is made with `new PostgresSession(request, log, connection, live, ServerOf(answer))`.

In the wrappers, `PrepareLiveAsync` becomes `_live.PrepareAsync`, `TypesLiveAsync` becomes `_live.TypesAsync`, and `DeallocateLiveAsync` becomes `_live.DeallocateAsync`.  Remove the `using` lines the session no longer needs; the build names them.

- [ ] **Step 3: Say where the functions are, in `src/SqlSource.Tool/AGENTS.md`**

In the bullet that begins `**The connection is made inside a `live` function and nowhere else.**`, after its first sentence, add:

```markdown
The `live` functions are `Describing/Postgres/PostgresLive.cs`, with the SQL the tool sends; `PostgresSession` holds what is asked, in which order, and what is made of each answer.
```

- [ ] **Step 4: Build and run the describer's tests**

Run: `dotnet build SqlSource.slnx`
Expected: no warning and no error.

Run: `dotnet test --project tests/SqlSource.Tool.Tests` and `dotnet test --project tests/SqlSource.Tests.Postgres`
Expected: every test passes, and none was changed.

- [ ] **Step 5: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: every step `passed`.

```bash
git add src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs src/SqlSource.Tool/AGENTS.md
```

```bash
git commit -m "Move the live functions of the PostgreSQL describer out of its session

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 3: `columns`, the result kind, and the types of the columns with their facets

After this task a statement with rows is `ResultKind.Rows`, and each column has its ordinal, its name as the server returned it, and the type the protocol reported with its facets.  A column has no origin yet and says `no-origin`; `plan` is `not-needed` for every query with rows.

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs`
- Create: `src/SqlSource.Tool/Describing/Postgres/ColumnFacets.cs`
- Create: `src/SqlSource.Tool/Describing/Postgres/PostgresColumns.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`
- Modify: `src/SqlSource.Tool/AGENTS.md`
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/ColumnFacetsTests.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresColumnsTests.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresLiveTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/PostgresScript.cs`
- Modify: `tests/SqlSource.Tool.Tests/PostgresRows.cs`
- Modify: `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs`
- Modify: the `capture.json` of the four cases under `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/`
- Create: `tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Describer/Sessions.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Describer/ParameterTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Describer/SessionTests.cs`

**Interfaces:**
- Consumes: `PostgresLive` (Task 2); `PostgresTypes.Build(long, IReadOnlyDictionary<long, PostgresTypeRow>, PostgresFacets)`, `PostgresTypes.IsPseudo`, `PostgresFacets`, `PostgresErrors.ServerLines`, `PostgresErrors.Types`, `PostgresErrors.PseudoType`.
- Produces:
  - `PostgresColumnsRequest(string Statement, int Parameters)`, `PostgresColumnsAnswer(IReadOnlyList<PostgresColumnRow>? Columns, PostgresServerError? Error)`, `PostgresColumnRow(int Ordinal, string Name, long TypeOid, long TableOid, int AttributeNumber, int? Length, int? Precision, int? Scale)`.
  - `ColumnFacets.Of(string typname, PostgresColumnRow column)` returning `PostgresFacets`.
  - `PostgresColumns.Build(PostgresColumnRow column, IReadOnlyDictionary<long, PostgresTypeRow> rows)` returning `SidecarColumn`.  Tasks 4 and 5 each give it one more parameter.
  - `PostgresLive.ColumnsAsync(PostgresColumnsRequest, CancellationToken)` and `PostgresLive.ExecuteText(PostgresColumnsRequest)`.
  - `PostgresErrors.Columns(PostgresServerError, DescribeRequest)`.
  - In `PostgresSession`: `DescribePreparedAsync`, `ParameterOids`, `RowsOfAsync`, `TypeParameters`, `Described`, which Tasks 4 to 7 grow.
  - In the tests: `PostgresScript.NoColumns`; `PostgresSessionTests.PreparesOnly`, `Columns` and `Expression`; `Sessions.DescribeAsync(string connection, DescribeRequest request, IDescribeExchange? exchange = null)`.

- [ ] **Step 1: Write the failing tests of the facets**

Create `tests/SqlSource.Tool.Tests/Describing/Postgres/ColumnFacetsTests.cs`:

```csharp
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// A column's facets from the three numbers Npgsql gives, which are the type's modifier decoded.  A type takes the
// numbers it carries and no other, whatever the answer holds.
public class ColumnFacetsTests
{
    private static PostgresColumnRow Column(int? length = null, int? precision = null, int? scale = null) =>
        new(0, "c", 0, 0, 0, length, precision, scale);

    [Theory]
    [InlineData("bpchar")]
    [InlineData("varchar")]
    [InlineData("bit")]
    [InlineData("varbit")]
    public void Of_TypeWithALength_TakesTheLengthAlone(string typname) =>
        ColumnFacets
            .Of(typname, Column(length: 10, precision: 5, scale: 2))
            .ShouldBe(new PostgresFacets(10, null, null));

    [Theory]
    [InlineData("time")]
    [InlineData("timetz")]
    [InlineData("timestamp")]
    [InlineData("timestamptz")]
    [InlineData("interval")]
    public void Of_TypeWithAPrecision_TakesThePrecisionAlone(string typname) =>
        ColumnFacets
            .Of(typname, Column(length: 10, precision: 3, scale: 2))
            .ShouldBe(new PostgresFacets(null, 3, null));

    [Fact]
    public void Of_Numeric_TakesThePrecisionAndTheScale() =>
        ColumnFacets
            .Of("numeric", Column(length: 10, precision: 18, scale: 2))
            .ShouldBe(new PostgresFacets(null, 18, 2));

    // Npgsql gives no scale for a scale of zero, and format_type spells numeric(18,0).
    [Fact]
    public void Of_NumericWithAPrecisionAndNoScale_HasTheScaleZero() =>
        ColumnFacets.Of("numeric", Column(precision: 18)).ShouldBe(new PostgresFacets(null, 18, 0));

    [Fact]
    public void Of_NumericWithoutAModifier_HasNone() =>
        ColumnFacets.Of("numeric", Column()).ShouldBe(PostgresFacets.None);

    // PostgreSQL 15 stores a scale in eleven bits, and Npgsql reads sixteen: -3 arrives as 2045.
    [Theory]
    [InlineData(2045, -3)]
    [InlineData(2047, -1)]
    [InlineData(1024, -1024)]
    [InlineData(1023, 1023)]
    [InlineData(0, 0)]
    public void Of_NumericScale_IsNegativeFrom1024(int given, int expected) =>
        ColumnFacets.Of("numeric", Column(precision: 2, scale: given)).ShouldBe(new PostgresFacets(null, 2, expected));

    // An interval with fields and no precision, "interval year to month", has this modifier.
    [Fact]
    public void Of_IntervalWithThePrecision65535_HasNone() =>
        ColumnFacets.Of("interval", Column(precision: 65535)).ShouldBe(PostgresFacets.None);

    // Npgsql gives an array the numbers of its element: they are dropped.
    [Theory]
    [InlineData("text")]
    [InlineData("int4")]
    [InlineData("date")]
    [InlineData("uuid")]
    [InlineData("_varchar")]
    [InlineData("_numeric")]
    [InlineData("email_t")]
    public void Of_TypeThatCarriesNoFacet_DropsWhatItWasGiven(string typname) =>
        ColumnFacets.Of(typname, Column(length: 10, precision: 6, scale: 2)).ShouldBe(PostgresFacets.None);

    [Theory]
    [InlineData("varchar")]
    [InlineData("timestamptz")]
    [InlineData("interval")]
    public void Of_TypeThatCarriesAFacetWithoutAModifier_HasNone(string typname) =>
        ColumnFacets.Of(typname, Column()).ShouldBe(PostgresFacets.None);
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.ColumnFacetsTests"`
Expected: the build fails: `PostgresColumnRow` and `ColumnFacets` do not exist.

- [ ] **Step 3: Add the records of `columns`**

Append to `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs`:

```csharp
/// <summary>Everything that decides the answer of <c>columns</c>.</summary>
/// <param name="Statement">The prepared statement to describe.</param>
/// <param name="Parameters">How many parameters the query's list has: the statement is given a <c>NULL</c> for each.</param>
internal sealed record PostgresColumnsRequest(string Statement, int Parameters);

/// <summary>The columns of a prepared statement, or why it could not be described.</summary>
/// <param name="Columns">The columns in order.  A statement without rows has none.</param>
/// <param name="Error">What the server said, or null.</param>
internal sealed record PostgresColumnsAnswer(IReadOnlyList<PostgresColumnRow>? Columns, PostgresServerError? Error);

/// <summary>One column, as the row description of the protocol and Npgsql's reading of it hold it.</summary>
/// <param name="Ordinal">The column's place, from zero.</param>
/// <param name="Name">The name as the server returned it.</param>
/// <param name="TypeOid">The type.  The protocol reports the base of a domain, with the domain's modifier.</param>
/// <param name="TableOid">The table the column comes from, or zero.</param>
/// <param name="AttributeNumber">The column of that table, zero for none and below zero for a system column.</param>
/// <param name="Length">Npgsql's <c>ColumnSize</c>, or null.</param>
/// <param name="Precision">Npgsql's <c>NumericPrecision</c>, or null.</param>
/// <param name="Scale">Npgsql's <c>NumericScale</c>, or null.</param>
internal sealed record PostgresColumnRow(
    int Ordinal,
    string Name,
    long TypeOid,
    long TableOid,
    int AttributeNumber,
    int? Length,
    int? Precision,
    int? Scale
);
```

- [ ] **Step 4: Write `ColumnFacets`**

Create `src/SqlSource.Tool/Describing/Postgres/ColumnFacets.cs`:

```csharp
namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// A column's facets.  Npgsql does not give a column's type modifier: it gives three numbers that are the modifier
/// decoded, and each is taken only for the types that carry it.
/// </summary>
/// <remarks>
/// Three of Npgsql's answers are not the server's.  It gives no scale for a <c>numeric</c> whose scale is zero.  It
/// reads the scale as sixteen bits where PostgreSQL 15 stores eleven, so a negative one arrives 2048 too high.  And
/// an <c>interval</c> with fields and no precision, <c>interval year to month</c>, has the precision 65535.  An
/// array is given the numbers of its element, which are dropped: the internal name of an array carries none.
/// </remarks>
internal static class ColumnFacets
{
    private const int NoIntervalPrecision = 65535;
    private const int FirstNegativeScale = 1024;
    private const int ScaleRange = 2048;

    /// <summary>The facets of a column.</summary>
    /// <param name="typname">The internal name of the type the protocol reported: <c>varchar</c>, <c>numeric</c>.</param>
    /// <param name="column">The column, with Npgsql's three numbers.</param>
    public static PostgresFacets Of(string typname, PostgresColumnRow column) =>
        typname switch
        {
            "bpchar" or "varchar" or "bit" or "varbit" => new PostgresFacets(column.Length, null, null),
            "numeric" when column.Precision is { } precision => new PostgresFacets(
                null,
                precision,
                ScaleOf(column.Scale)
            ),
            "time" or "timetz" or "timestamp" or "timestamptz" => new PostgresFacets(null, column.Precision, null),
            "interval" when column.Precision != NoIntervalPrecision => new PostgresFacets(null, column.Precision, null),
            _ => PostgresFacets.None,
        };

    private static int ScaleOf(int? scale) =>
        scale switch
        {
            null => 0,
            >= FirstNegativeScale => scale.Value - ScaleRange,
            _ => scale.Value,
        };
}
```

- [ ] **Step 5: Run the tests of the facets**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.ColumnFacetsTests"`
Expected: PASS.

- [ ] **Step 6: Write the failing tests of a column and of the `EXECUTE` text**

Add to `tests/SqlSource.Tool.Tests/PostgresRows.cs`, after `TextArray`:

```csharp
    public static PostgresTypeRow VarcharArray { get; } = ArrayOf(1015, "_varchar", Varchar);
```

Create `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresColumnsTests.cs`:

```csharp
using System;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tests.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// One column of a sidecar from one row of "columns".
public class PostgresColumnsTests
{
    [Fact]
    public void Build_Column_HasItsOrdinalItsNameAndItsTypeWithFacets() =>
        PostgresColumns
            .Build(
                new PostgresColumnRow(2, "balance", 1700, 0, 0, null, 18, 2),
                PostgresRows.Of(PostgresRows.Numeric)
            )
            .ShouldBe(
                new SidecarColumn(
                    2,
                    "balance",
                    TestSidecars.Postgres("numeric(18,2)", internalName: "numeric", precision: 18, scale: 2),
                    null,
                    SidecarValues.NullableSource.NoOrigin,
                    null,
                    null,
                    null
                )
            );

    // Npgsql gives varchar(10)[] the size 10, and the element is recorded without it.
    [Fact]
    public void Build_ArrayColumn_DropsTheFacetsOfItsElement()
    {
        var column = PostgresColumns.Build(
            new PostgresColumnRow(0, "names", 1015, 0, 0, 10, null, null),
            PostgresRows.Of(PostgresRows.VarcharArray, PostgresRows.Varchar)
        );

        column.Type.ShouldBe(
            TestSidecars.Postgres(
                "character varying[]",
                SidecarValues.Kind.Array,
                internalName: "_varchar",
                element: TestSidecars.Postgres("character varying", internalName: "varchar")
            )
        );
    }

    [Fact]
    public void Build_TypeTheRowsLack_ThrowsNamingIt() =>
        Should
            .Throw<InvalidOperationException>(() =>
                PostgresColumns.Build(new PostgresColumnRow(0, "c", 23, 0, 0, null, null, null), PostgresRows.Of())
            )
            .Message.ShouldContain("23");
}
```

Create `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresLiveTests.cs`:

```csharp
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The texts the live functions make themselves.  What they do with a server is in tests/SqlSource.Tests.Postgres.
public class PostgresLiveTests
{
    [Fact]
    public void ExecuteText_StatementWithoutParameters_HasNoList() =>
        PostgresLive.ExecuteText(new PostgresColumnsRequest("sqlsource_1", 0)).ShouldBe("EXECUTE sqlsource_1");

    [Fact]
    public void ExecuteText_StatementWithParameters_HasANullForEach() =>
        PostgresLive
            .ExecuteText(new PostgresColumnsRequest("sqlsource_7", 3))
            .ShouldBe("EXECUTE sqlsource_7(NULL, NULL, NULL)");
}
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresColumnsTests"`
Expected: the build fails: `PostgresColumns` and `PostgresLive.ExecuteText` do not exist.

- [ ] **Step 7: Write `PostgresColumns` and the live function**

Create `src/SqlSource.Tool/Describing/Postgres/PostgresColumns.cs`:

```csharp
using System;
using System.Collections.Generic;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// A column of a sidecar, from what <c>columns</c> answered for it and the rows of <c>types</c>.
/// </summary>
internal static class PostgresColumns
{
    /// <summary>The column, with the type the protocol reported and its facets.</summary>
    /// <param name="column">The row of <c>columns</c>.</param>
    /// <param name="rows">The rows of the closure, by OID.</param>
    /// <exception cref="InvalidOperationException">
    /// The rows lack the column's type, or the type is a pseudo-type.
    /// </exception>
    public static SidecarColumn Build(PostgresColumnRow column, IReadOnlyDictionary<long, PostgresTypeRow> rows) =>
        new(
            column.Ordinal,
            column.Name,
            PostgresTypes.Build(column.TypeOid, rows, FacetsOf(column, rows)),
            null,
            SidecarValues.NullableSource.NoOrigin,
            null,
            null,
            null
        );

    // The facets are read by the type the protocol reported, which is the one whose modifier they decode.
    private static PostgresFacets FacetsOf(PostgresColumnRow column, IReadOnlyDictionary<long, PostgresTypeRow> rows) =>
        rows.TryGetValue(column.TypeOid, out var row)
            ? ColumnFacets.Of(row.Typname, column)
            : throw new InvalidOperationException(
                $"The answer to 'types' holds no row for the type {column.TypeOid} of the column '{column.Name}'."
            );
}
```

In `src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs`, add `using System.Data;` and `using Npgsql.Schema;`, and after `PrepareAsync`:

```csharp
    /// <summary>
    /// Asks <c>columns</c>: describes the prepared statement through an <c>EXECUTE</c> that is parsed and never run.
    /// </summary>
    /// <remarks>
    /// Under <see cref="CommandBehavior.SchemaOnly" /> the driver sends no <c>Execute</c>.  Without
    /// <see cref="CommandBehavior.KeyInfo" /> it fills the schema from the row description alone and asks the
    /// server nothing more.  The text holds a name the tool made and nothing of a user's.
    /// </remarks>
    public async Task<PostgresColumnsAnswer> ColumnsAsync(PostgresColumnsRequest asked, CancellationToken token)
    {
        try
        {
            await using var command = await connection.CommandAsync(ExecuteText(asked), token);
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SchemaOnly, token);
            var columns = new List<PostgresColumnRow>(reader.FieldCount);

            // The async method is the base class's and gives the base type; each item is the driver's own.
            foreach (var column in (await reader.GetColumnSchemaAsync(token)).Cast<NpgsqlDbColumn>())
            {
                columns.Add(
                    new PostgresColumnRow(
                        columns.Count,
                        column.ColumnName,
                        column.TypeOID,
                        column.TableOID,
                        column.ColumnAttributeNumber ?? 0,
                        column.ColumnSize,
                        column.NumericPrecision,
                        column.NumericScale
                    )
                );
            }

            return new PostgresColumnsAnswer(columns, null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresColumnsAnswer(null, PostgresConnection.ErrorOf(exception));
        }
    }

    /// <summary>
    /// The text <c>columns</c> sends: the statement with a <c>NULL</c> for each parameter.  The server describes an
    /// <c>EXECUTE</c> whatever the number of its arguments, so a statement with a <c>$n</c> of its own is described
    /// too.
    /// </summary>
    public static string ExecuteText(PostgresColumnsRequest asked) =>
        asked.Parameters == 0
            ? $"EXECUTE {asked.Statement}"
            : $"EXECUTE {asked.Statement}({string.Join(", ", Enumerable.Repeat("NULL", asked.Parameters))})";
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresColumnsTests"` and the same for `PostgresLiveTests`.
Expected: PASS.

- [ ] **Step 8: Write the failing tests of the session**

In `tests/SqlSource.Tool.Tests/PostgresScript.cs`, after `Opened`:

```csharp
    // What "columns" answers for a statement without rows.
    public static PostgresColumnsAnswer NoColumns { get; } = new([], null);
```

In `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`, replace the helper `Prepares` with these three, and add `Expression` beside `Inferred` and `Declared`:

```csharp
    // "prepare" alone: for a describe that ends there, and for one whose columns the test writes.
    private static PostgresScript PreparesOnly(
        PostgresScript script,
        int number,
        string sql,
        string[] types,
        PostgresPrepareAnswer answer
    ) => script.Answer("prepare", new PostgresPrepareRequest($"sqlsource_{number}", sql, types), answer);

    // "prepare", and "columns" answering no column when the statement was prepared with a type for each parameter
    // of the list: most of these tests are about the parameters.
    private static PostgresScript Prepares(
        PostgresScript script,
        int number,
        string sql,
        string[] types,
        PostgresPrepareAnswer answer
    )
    {
        _ = PreparesOnly(script, number, sql, types, answer);
        return answer.ParameterTypes is { } resolved && resolved.Count >= types.Length
            ? Columns(script, number, types.Length)
            : script;
    }

    private static PostgresScript Columns(
        PostgresScript script,
        int number,
        int parameters,
        params PostgresColumnRow[] columns
    ) =>
        script.Answer(
            "columns",
            new PostgresColumnsRequest($"sqlsource_{number}", parameters),
            new PostgresColumnsAnswer(columns, null)
        );

    // A column of an expression: it names no table.
    private static PostgresColumnRow Expression(
        int ordinal,
        string name,
        long typeOid,
        int? length = null,
        int? precision = null,
        int? scale = null
    ) => new(ordinal, name, typeOid, 0, 0, length, precision, scale);
```

No existing test of the file changes: each asks through `Prepares`, which now answers `columns` too.  `DescribeAsync_FewerTypesThanParameters_ThrowsAndFreesTheStatement` gets no `columns` answer, since the describe throws before it asks, and its `exchange.Unused.ShouldBeEmpty()` holds that.

Add these tests at the end of the class:

```csharp
    [Fact]
    public async Task DescribeAsync_StatementWithColumns_IsRowsWithEachColumnsTypeAndFacets()
    {
        var script = new PostgresScript().Open();
        _ = PreparesOnly(script, 1, "SELECT 1, 'a'::varchar(100), 2::numeric(18,2)", [], new([], null));
        _ = Columns(
            script,
            1,
            0,
            Expression(0, "?column?", 23),
            Expression(1, "varchar", 1043, length: 100),
            Expression(2, "numeric", 1700, precision: 18, scale: 2)
        );
        _ = Resolves(script, [23, 1043, 1700], PostgresRows.Int4, PostgresRows.Varchar, PostgresRows.Numeric);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT 1, 'a'::varchar(100), 2::numeric(18,2)"),
            TestContext.Current.CancellationToken
        );

        var description = described.Value.ShouldNotBeNull();
        description.ResultKind.ShouldBe(SidecarResultKind.Rows);
        description.MatchesTable.ShouldBeNull();
        description.Plan.ShouldBe(SidecarValues.Plan.NotNeeded);
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NoOrigin);
        description
            .Columns.ShouldNotBeNull()
            .Select(column => (column.Ordinal, column.Name, column.Type.Name))
            .ShouldBe([(0, "?column?", "integer"), (1, "varchar", "character varying(100)"), (2, "numeric", "numeric(18,2)")]);
        exchange.Unused.ShouldBeEmpty();
    }

    // A statement without rows has no columns, no table and no provenance.
    [Fact]
    public async Task DescribeAsync_StatementWithoutColumns_IsNoneWithNoProvenance()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(script, 1, "DELETE FROM samples WHERE id = $1", ["unknown"], new([23], null));
        _ = Resolves(script, [23], PostgresRows.Int4);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("DELETE FROM samples WHERE id = @id", Inferred("id")),
            TestContext.Current.CancellationToken
        );

        var description = described.Value.ShouldNotBeNull();
        description.ResultKind.ShouldBe(SidecarResultKind.None);
        description.Columns.ShouldBeNull();
        description.Plan.ShouldBeNull();
        description.TableMatch.ShouldBeNull();
        exchange.Unused.ShouldBeEmpty();
    }

    // "types" is asked once, for the parameters and the columns together, sorted and each OID once.
    [Fact]
    public async Task DescribeAsync_ParametersAndColumns_AskTypesOnceForBoth()
    {
        var script = new PostgresScript().Open();
        _ = PreparesOnly(script, 1, "SELECT $1::text AS a, 1 AS b, 2 AS c", ["unknown"], new([25], null));
        _ = Columns(script, 1, 1, Expression(0, "a", 25), Expression(1, "b", 23), Expression(2, "c", 23));
        _ = Resolves(script, [23, 25], PostgresRows.Int4, PostgresRows.Text);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT @p::text AS a, 1 AS b, 2 AS c", Inferred("p")),
            TestContext.Current.CancellationToken
        );

        var description = described.Value.ShouldNotBeNull();
        description.Parameters.ShouldHaveSingleItem().Type.ShouldBe(TestSidecars.Postgres("text", internalName: "text"));
        description.Columns.ShouldNotBeNull().Select(column => column.Type.Name).ShouldBe(["text", "integer", "integer"]);
        exchange.Unused.ShouldBeEmpty();
    }

    // The position the server gives is in the tool's own EXECUTE, so there is no "at character" line.
    [Fact]
    public async Task DescribeAsync_ColumnsThatFails_IsSqlsrc225AtDescribeColumnsAndFreesTheStatement()
    {
        var dropped = new PostgresServerError("57P01", "terminating connection due to administrator command", null, null, 9);
        var script = new PostgresScript().Open();
        _ = PreparesOnly(script, 1, "SELECT 1", [], new([], null));
        _ = script.Answer(
            "columns",
            new PostgresColumnsRequest("sqlsource_1", 0),
            new PostgresColumnsAnswer(null, dropped)
        );
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Request("SELECT 1"), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        failure.ServerLines.ShouldBe(["57P01: terminating connection due to administrator command"]);
        exchange.Unused.ShouldBeEmpty();
    }
```

In `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs`, add beside `Deallocate`:

```csharp
    private static readonly PostgresColumnsRequest Columns = new("sqlsource_1", 1);
```

and in each of the four tests whose script answers `prepare` with types, `Describe_QueryWithAParameter_WritesPrepareTypesAndDeallocateInOrder`, `Describe_TypesThatFails_WritesItsErrorInThePlaceOfTheRows`, `Describe_DeallocateThatFails_WritesTheErrorThatIsNeverReported` and `Describe_ThroughTheLogOfASession_HasTheEngineBeforeEachNameAndNothingOfTheSecret`, add this line after the `.Answer("prepare", ...)` line:

```csharp
            .Answer("columns", Columns, PostgresScript.NoColumns)
```

The events those tests expect do not change: `columns` writes none until Task 8.

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresSessionTests"`
Expected: FAIL: the four new tests, and each old test that holds that its script was used whole, since the session does not ask `columns` yet.

- [ ] **Step 9: Ask `columns` in the session**

In `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`, after `Types`:

```csharp
    /// <summary>
    /// <c>SQLSRC225</c> at the step <c>describe columns</c>: the prepared statement could not be described.  There
    /// is no position line: a position the server gives is in the tool's own <c>EXECUTE</c>.
    /// </summary>
    public static DescribeFailure Columns(PostgresServerError error, DescribeRequest query) =>
        Rejected(error, query, DescribeStep.DescribeColumns, ServerLines(error with { Position = null }, 0));
```

In `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`, the `try` of `DescribeAsync` becomes:

```csharp
        try
        {
            return await DescribePreparedAsync(request, prepare.Statement, prepared, cancellationToken);
        }
```

and `TypeParametersAsync` and `Described` are replaced by these:

```csharp
    // What is asked of a statement that was prepared, in this order: its columns, and every type that the
    // parameters and the columns have, once.
    private async Task<Outcome<QueryDescription, DescribeFailure>> DescribePreparedAsync(
        DescribeRequest request,
        string statement,
        PostgresPrepareAnswer prepared,
        CancellationToken cancellationToken
    )
    {
        var parameterOids = ParameterOids(request, prepared);

        var answer = await ColumnsAsync(
            new PostgresColumnsRequest(statement, request.Parameters.Count),
            cancellationToken
        );
        if (answer.Error is { } refused)
        {
            return Failed(PostgresErrors.Columns(refused, request));
        }

        var columns =
            answer.Columns
            ?? throw new InvalidOperationException("The answer to 'columns' holds no columns and no error.");

        var rows = await RowsOfAsync(
            [.. parameterOids, .. columns.Select(static column => column.TypeOid)],
            cancellationToken
        );
        if (!rows.Succeeded)
        {
            return Failed(PostgresErrors.Types(rows.Failure, request));
        }

        var parameters = TypeParameters(request, parameterOids, rows.Value);
        if (!parameters.Succeeded)
        {
            return Failed(parameters.Failure);
        }

        return Described(parameters.Value, [.. columns.Select(column => PostgresColumns.Build(column, rows.Value))]);
    }

    // The types the server resolved for the parameters of the query's list.  It lists a type for a "$n" that the SQL
    // writes itself, after the query's own: that is no parameter of the query, and the list's are the first.
    private static IReadOnlyList<long> ParameterOids(DescribeRequest request, PostgresPrepareAnswer prepared)
    {
        var count = request.Parameters.Count;
        var resolved =
            prepared.ParameterTypes
            ?? throw new InvalidOperationException("The answer to 'prepare' holds no types and no error.");
        return resolved.Count < count
            ? throw new InvalidOperationException(
                $"The server resolved {resolved.Count} types for the {count} parameters of '{request.Name}'."
            )
            : [.. resolved.Take(count)];
    }

    // The rows of every type asked for, by OID.  The request is sorted and holds each OID once, so that it is the
    // same however a query orders what it names.  Nothing is asked when there is no type to ask for.
    private async Task<Outcome<IReadOnlyDictionary<long, PostgresTypeRow>, PostgresServerError>> RowsOfAsync(
        IReadOnlyList<long> oids,
        CancellationToken cancellationToken
    )
    {
        if (oids.Count == 0)
        {
            return Outcome<IReadOnlyDictionary<long, PostgresTypeRow>, PostgresServerError>.Of(
                new Dictionary<long, PostgresTypeRow>()
            );
        }

        var answer = await TypesAsync(new PostgresTypesRequest([.. oids.Distinct().Order()]), cancellationToken);
        return answer.Error is { } error
            ? Outcome<IReadOnlyDictionary<long, PostgresTypeRow>, PostgresServerError>.Failed(error)
            : Outcome<IReadOnlyDictionary<long, PostgresTypeRow>, PostgresServerError>.Of(
                (
                    answer.Rows
                    ?? throw new InvalidOperationException("The answer to 'types' holds no rows and no error.")
                ).ToDictionary(static row => row.Oid)
            );
    }

    // Each parameter of the query's list gets the type the server resolved for it.
    private static Outcome<IReadOnlyList<DescribedParameter>, DescribeFailure> TypeParameters(
        DescribeRequest request,
        IReadOnlyList<long> oids,
        IReadOnlyDictionary<long, PostgresTypeRow> rows
    )
    {
        var parameters = new List<DescribedParameter>(oids.Count);
        for (var index = 0; index < oids.Count; index++)
        {
            var parameter = request.Parameters[index];
            if (PostgresTypes.IsPseudo(oids[index], rows))
            {
                // The server accepts a pseudo-type, declared or inferred, and no value has one.
                return Outcome<IReadOnlyList<DescribedParameter>, DescribeFailure>.Failed(
                    PostgresErrors.PseudoType(request, parameter, rows[oids[index]].FormatType)
                );
            }

            // A parameter has no facets: the protocol gives it an OID and no modifier.
            parameters.Add(
                new DescribedParameter(
                    parameter.Name,
                    PostgresTypes.Build(oids[index], rows, PostgresFacets.None),
                    parameter.Type is null ? SidecarValues.TypeSource.Inferred : SidecarValues.TypeSource.Declared
                )
            );
        }

        return Outcome<IReadOnlyList<DescribedParameter>, DescribeFailure>.Of(parameters);
    }

    // A statement without rows has its parameters and nothing else: no columns, no table and no provenance.
    private static Outcome<QueryDescription, DescribeFailure> Described(
        IReadOnlyList<DescribedParameter> parameters,
        IReadOnlyList<SidecarColumn> columns
    )
    {
        var typed = new EquatableArray<DescribedParameter>([.. parameters]);
        return Outcome<QueryDescription, DescribeFailure>.Of(
            columns.Count == 0
                ? new QueryDescription(SidecarResultKind.None, typed, null, null, null, null)
                : new QueryDescription(
                    SidecarResultKind.Rows,
                    typed,
                    new EquatableArray<SidecarColumn>([.. columns]),
                    null,
                    SidecarValues.Plan.NotNeeded,
                    SidecarValues.TableMatch.NoOrigin
                )
        );
    }
```

Beside the wrapper `TypesAsync`, add the wrapper of the new call.  It writes no event yet:

```csharp
    private Task<PostgresColumnsAnswer> ColumnsAsync(
        PostgresColumnsRequest request,
        CancellationToken cancellationToken
    ) => _exchange.AskAsync("columns", request, _live.ColumnsAsync, cancellationToken);
```

Update the remarks of the class: after the sentence that ends `and infers the rest.` add `The statement is then described through an <c>EXECUTE</c> that is never run, which gives its columns.`

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresSessionTests"` and the same for `PostgresEventsTests`.
Expected: PASS.

- [ ] **Step 10: Record the four captures again**

`tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs` now fails: the captures hold no `columns` call.  Docker must be running.

Run: `tools/record-fixtures.sh`
Expected: `Recording 4 cases of postgres against postgres:18 ... done`.

Run: `git status --short tests/SqlSource.Tool.Tests/Fixtures/Describers`
Expected: the four `capture.json` files are modified and no `expected.json` is.  Read the diff: each capture gains one `columns` call with `"columns": []`, and the OIDs of the other calls are as they were.  If an `expected.json` changed, stop: the description of a statement without rows must not have.

In `Replay_CaptureWithACallRemoved_ThrowsACaptureMiss` of `DescriberFixtureTests.cs`, add a line to the theory's data:

```csharp
    [InlineData("columns")]
```

- [ ] **Step 11: Write the container tests**

In `tests/SqlSource.Tests.Postgres/Describer/Sessions.cs`, after `OpenAsync`:

```csharp
    // Describes one query in a session of its own, which must end with no prepared statement left in it.
    public static async Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        string connection,
        DescribeRequest request,
        IDescribeExchange? exchange = null
    )
    {
        var opened = await OpenAsync(connection, exchange);
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();

        var described = await session.DescribeAsync(request, TestContext.Current.CancellationToken);

        (await PreparedStatementsAsync(session)).ShouldBe(0);
        return described;
    }
```

In `tests/SqlSource.Tests.Postgres/Describer/ParameterTests.cs`, the private `DescribeAsync` becomes a call of it, and `ShouldDescribeAsync` no longer says what the result is, since a `SELECT` now has rows:

```csharp
    private Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        DescribeRequest request,
        IDescribeExchange? exchange = null
    ) => Sessions.DescribeAsync(postgres.ConnectionString, request, exchange);
```

```csharp
    // The parameters of a query that is described.  Its columns are ColumnTests' business.
    private async Task<EquatableArray<DescribedParameter>> ShouldDescribeAsync(DescribeRequest request)
    {
        var described = await DescribeAsync(request);

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        return described.Value.ShouldNotBeNull().Parameters;
    }
```

In `tests/SqlSource.Tests.Postgres/Describer/SessionTests.cs`, the call after `prepare` is now `columns`.  Replace the test `Describe_ServerEndsTheSessionBetweenPrepareAndTypes_IsSqlsrc225AndNoThrow` and the comment above it:

```csharp
    // The statement is prepared and the session ends before its columns are read.  "columns" answers the error that
    // is reported, and "deallocate", which is asked whatever became of the describe, answers one too and replaces
    // nothing.
    [Fact]
    public async Task Describe_ServerEndsTheSessionBetweenPrepareAndColumns_IsSqlsrc225AndNoThrow()
    {
        var capture = new DescribeCapture("postgres");
        var exchange = new EndsTheBackendAfterPrepare(postgres.ConnectionString);
        var opened = await Sessions.OpenAsync(postgres.ConnectionString, new CapturingExchange(exchange, capture));
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();
        exchange.Session = session;

        var described = await session.DescribeAsync(Requests.Of(Sql), TestContext.Current.CancellationToken);

        _ = ShouldBeRejected(described, DescribeStep.DescribeColumns);
        capture.Calls.Select(call => call.Method).ShouldBe(["open", "prepare", "columns", "deallocate"]);
        CaptureJson.TextOf(capture.Calls[3].Answer).ShouldContain(PostgresConnection.LostConnection);
    }
```

Create `tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// A query's columns against a real server: what only a server can show, the row description of a statement and
// what Npgsql makes of it.
[Collection(UsesPostgres.Name)]
public sealed class ColumnTests(PostgresContainer postgres)
{
    private async Task<QueryDescription> ShouldDescribeAsync(string sql, IDescribeExchange? exchange = null)
    {
        var described = await Sessions.DescribeAsync(postgres.ConnectionString, Requests.Of(sql), exchange);

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        return described.Value.ShouldNotBeNull();
    }

    private async Task<SidecarColumn[]> ColumnsOfAsync(string sql) =>
        [.. (await ShouldDescribeAsync(sql)).Columns.ShouldNotBeNull()];

    private static string[] TypeNamesOf(IEnumerable<SidecarColumn> columns) =>
        [.. columns.Select(static column => column.Type.Name)];

    // One value from the server, on a connection of the test's own.
    private async Task<object?> ScalarAsync(string sql)
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(sql);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> ServerMajorAsync() =>
        int.Parse((string)(await ScalarAsync("SHOW server_version_num"))!, CultureInfo.InvariantCulture) / 10000;

    [Fact]
    public async Task Describe_Select_IsRowsWithItsColumnsInOrder()
    {
        var description = await ShouldDescribeAsync("SELECT code, at_local, span, label FROM samples WHERE id = @id");

        description.ResultKind.ShouldBe(SidecarResultKind.Rows);
        var columns = description.Columns.ShouldNotBeNull();
        columns.Select(column => column.Ordinal).ShouldBe([0, 1, 2, 3]);
        columns.Select(column => column.Name).ShouldBe(["code", "at_local", "span", "label"]);
        TypeNamesOf(columns).ShouldBe(["character(2)", "timestamp(3) without time zone", "interval", "text"]);
        description.Parameters.ShouldHaveSingleItem().Name.ShouldBe("id");
    }

    // A cast in a select list keeps its modifier in the row description.  Each name is what format_type gives the
    // type, which Describe_EveryColumnOfATable_IsSpelledAsFormatTypeSpellsIt holds against the server for a column.
    [Theory]
    [InlineData("numeric(18,2)", "numeric(18,2)")]
    [InlineData("numeric(18,0)", "numeric(18,0)")]
    [InlineData("numeric(5)", "numeric(5,0)")]
    [InlineData("numeric", "numeric")]
    [InlineData("varchar(100)", "character varying(100)")]
    [InlineData("varchar", "character varying")]
    [InlineData("char(2)", "character(2)")]
    [InlineData("char", "character(1)")]
    [InlineData("bpchar", "bpchar")]
    [InlineData("bit(3)", "bit(3)")]
    [InlineData("bit", "bit(1)")]
    [InlineData("varbit(5)", "bit varying(5)")]
    [InlineData("varbit", "bit varying")]
    [InlineData("time(4)", "time(4) without time zone")]
    [InlineData("timetz(2)", "time(2) with time zone")]
    [InlineData("timestamp(3)", "timestamp(3) without time zone")]
    [InlineData("timestamptz(0)", "timestamp(0) with time zone")]
    [InlineData("timestamptz", "timestamp with time zone")]
    [InlineData("interval(3)", "interval(3)")]
    [InlineData("interval", "interval")]
    public async Task Describe_CastWithAModifier_IsSpelledAsFormatTypeSpellsIt(string type, string expected) =>
        (await ColumnsOfAsync($"SELECT NULL::{type} AS c")).ShouldHaveSingleItem().Type.Name.ShouldBe(expected);

    // What the describer does not keep, and docs/tech-debt records: the fields of an interval, and the modifier of
    // an array's element.
    [Theory]
    [InlineData("interval year to month", "interval")]
    [InlineData("interval day to second(3)", "interval(3)")]
    [InlineData("varchar(10)[]", "character varying[]")]
    [InlineData("numeric(6,2)[]", "numeric[]")]
    public async Task Describe_CastWhoseModifierIsNotKept_IsSpelledWithoutIt(string type, string expected) =>
        (await ColumnsOfAsync($"SELECT NULL::{type} AS c")).ShouldHaveSingleItem().Type.Name.ShouldBe(expected);

    [Fact]
    public async Task Describe_NumericWithANegativeScale_HasIt()
    {
        Assert.SkipWhen(await ServerMajorAsync() < 15, "PostgreSQL 14 has no negative scale.");

        var column = (await ColumnsOfAsync("SELECT NULL::numeric(2,-3) AS c")).ShouldHaveSingleItem();

        var type = column.Type.ShouldBeOfType<PostgresType>();
        type.Name.ShouldBe("numeric(2,-3)");
        type.Precision.ShouldBe(2);
        type.Scale.ShouldBe(-3);
    }

    [Fact]
    public async Task Describe_Literals_AreTextAndNamedByTheServer()
    {
        var columns = await ColumnsOfAsync("SELECT 'x', NULL, 1");

        columns.Select(column => column.Name).ShouldBe(["?column?", "?column?", "?column?"]);
        TypeNamesOf(columns).ShouldBe(["text", "text", "integer"]);
    }

    [Fact]
    public async Task Describe_TwoColumnsWithOneName_KeepsBoth()
    {
        var columns = await ColumnsOfAsync("SELECT 1 AS id, 'a'::text AS id");

        columns.Select(column => (column.Ordinal, column.Name)).ShouldBe([(0, "id"), (1, "id")]);
        TypeNamesOf(columns).ShouldBe(["integer", "text"]);
    }

    // A suffix is the generator's to read: the describer records the name the server returned.
    [Fact]
    public async Task Describe_SuffixNames_AreRecordedAsReturned() =>
        (await ColumnsOfAsync("SELECT deleted_at AS \"deleted_at!\", name AS \"name?\" FROM users"))
            .Select(column => column.Name)
            .ShouldBe(["deleted_at!", "name?"]);

    // The statement is described and never run.
    [Fact]
    public async Task Describe_InsertReturning_IsRowsAndInsertsNothing()
    {
        var description = await ShouldDescribeAsync(
            "INSERT INTO orders (user_id, total) VALUES (@user_id, @total) RETURNING id, placed_at"
        );

        description.ResultKind.ShouldBe(SidecarResultKind.Rows);
        description.Columns.ShouldNotBeNull().Select(column => column.Name).ShouldBe(["id", "placed_at"]);
        (await ScalarAsync("SELECT count(*) FROM orders")).ShouldBe(0L);
    }

    [Fact]
    public async Task Describe_UpdateWithoutReturning_IsNone()
    {
        var description = await ShouldDescribeAsync("UPDATE users SET name = @name WHERE id = @id");

        description.ResultKind.ShouldBe(SidecarResultKind.None);
        description.Columns.ShouldBeNull();
        description.Plan.ShouldBeNull();
        description.TableMatch.ShouldBeNull();
        description.Parameters.Select(parameter => parameter.Name).ShouldBe(["name", "id"]);
    }

    [Fact]
    public async Task Describe_QueryWithoutATable_AsksColumnsAfterPrepareAndTypesOnce()
    {
        var capture = new DescribeCapture("postgres");

        _ = await ShouldDescribeAsync(
            "SELECT @p::int AS a, 'x' AS b",
            new CapturingExchange(LiveExchange.Instance, capture)
        );

        capture.Calls.Select(call => call.Method).ShouldBe(["open", "prepare", "columns", "types", "deallocate"]);
    }
}
```

Run: `dotnet test --project tests/SqlSource.Tests.Postgres`
Expected: PASS, with `Describe_NumericWithANegativeScale_HasIt` run on the default image.  A `ParameterTests` case that fails here asserts something of the result kind that this step did not find: read it and mend the assertion, not the describer.

Run: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres`
Expected: PASS, with `Describe_NumericWithANegativeScale_HasIt` skipped.

- [ ] **Step 12: Keep `src/SqlSource.Tool/AGENTS.md` true**

In the bullet that begins `**`Describing/Postgres/` is the PostgreSQL describer, and it is registered nowhere.**`:

- `, asks `types` for the closure of those OIDs, and asks `deallocate`.` becomes `, asks `columns`, which describes the statement through `EXECUTE <name>(NULL, ...)` under `CommandBehavior.SchemaOnly` and runs nothing, asks `types` once for the closure of the OIDs of the parameters and the columns, and asks `deallocate`.`
- its last sentence, `A description has its parameters, `SidecarResultKind.None` and nothing else: the columns are sub-phase 3.3's.`, becomes `A statement without rows has its parameters and `SidecarResultKind.None`; one with rows has its columns too.`

In the bullet that begins `**The calls and their records are a contract with the captures.**`, `The methods are `open`, `prepare`, `types` and `deallocate`` becomes `The methods are `open`, `prepare`, `columns`, `types` and `deallocate``.

- [ ] **Step 13: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: every step `passed`.

```bash
git add src/SqlSource.Tool tests/SqlSource.Tool.Tests tests/SqlSource.Tests.Postgres
```

```bash
git commit -m "Describe the columns of a query through its prepared statement

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 4: `tables`: origins, identity, computed, and the domain through the origin

After this task a column that comes from a table or a view has its origin, says whether it is an identity or a generated column, and is recorded as its domain when its attribute is one.  It still says `no-origin` for its nullability, which is Task 5's.

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs`
- Create: `src/SqlSource.Tool/Describing/Postgres/ColumnOrigin.cs`
- Create: `src/SqlSource.Tool/Describing/Postgres/ColumnTypes.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresColumns.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`
- Modify: `src/SqlSource.Tool/AGENTS.md`
- Create: `tests/SqlSource.Tool.Tests/PostgresTables.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/ColumnOriginTests.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/ColumnTypeTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresColumnsTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/PostgresRows.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs`

**Interfaces:**
- Consumes: `PostgresColumnRow`, `PostgresColumns.Build`, `ColumnFacets`, `DescribePreparedAsync` and `RowsOfAsync` of Task 3.
- Produces:
  - `PostgresTablesRequest(IReadOnlyList<long> Oids)`, `PostgresTablesAnswer(IReadOnlyList<PostgresTableRow>? Tables, PostgresServerError? Error)`, `PostgresTableRow(long Oid, string Relkind, string Schema, string Name, IReadOnlyList<PostgresAttributeRow> Columns)`, `PostgresAttributeRow(int Attnum, string Name, bool NotNull, string Identity, string Generated, long TypeOid)`.
  - `ColumnOrigin(PostgresTableRow Table, PostgresAttributeRow Attribute)` with `static ColumnOrigin? Find(PostgresColumnRow column, IReadOnlyDictionary<long, PostgresTableRow> tables)`.
  - `ColumnTypes.OidOf(PostgresColumnRow column, ColumnOrigin? origin, IReadOnlyDictionary<long, PostgresTypeRow> rows)` returning `long`.
  - `PostgresColumns.Build(PostgresColumnRow column, ColumnOrigin? origin, IReadOnlyDictionary<long, PostgresTypeRow> rows)`.  Task 5 gives it `bool gated`.
  - `PostgresLive.TablesAsync(PostgresTablesRequest, CancellationToken)`; `PostgresErrors.Tables(PostgresServerError, DescribeRequest)`.
  - In the tests: `PostgresTables` with `Users`, `Orders`, `TableOf`, `Attribute`, `Of` and `ColumnOf`; `PostgresRows.Tid` and `PostgresRows.D2`; `PostgresSessionTests.Tables`.

- [ ] **Step 1: Write the failing tests of the origin and of the type**

Add to `tests/SqlSource.Tool.Tests/PostgresRows.cs`, after `EmailT`:

```csharp
    // A domain over a domain.
    public static PostgresTypeRow D2 { get; } = DomainOf(16500, "d2", EmailT);

    public static PostgresTypeRow Tid { get; } = BaseOf(27, "tid", "tid", "U");
```

Create `tests/SqlSource.Tool.Tests/PostgresTables.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using SqlSource.Tool.Describing.Postgres;

namespace SqlSource.Tool.Tests;

// Tables of "tables" as a server answers them, for a test that has no server, and columns of "columns" that name
// them.
internal static class PostgresTables
{
    public static PostgresTableRow Users { get; } =
        TableOf(
            16396,
            "users",
            Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true, identity: "a"),
            Attribute(2, "name", PostgresRows.Varchar.Oid, notNull: true),
            Attribute(3, "email", PostgresRows.EmailT.Oid),
            Attribute(9, "name_upper", PostgresRows.Text.Oid, generated: "s")
        );

    public static PostgresTableRow Orders { get; } =
        TableOf(
            16415,
            "orders",
            Attribute(2, "user_id", PostgresRows.Int4.Oid, notNull: true),
            Attribute(3, "total", PostgresRows.Numeric.Oid, notNull: true)
        );

    public static PostgresTableRow TableOf(long oid, string name, params PostgresAttributeRow[] columns) =>
        new(oid, "r", "public", name, columns);

    public static PostgresAttributeRow Attribute(
        int attnum,
        string name,
        long typeOid,
        bool notNull = false,
        string identity = "",
        string generated = ""
    ) => new(attnum, name, notNull, identity, generated, typeOid);

    public static IReadOnlyDictionary<long, PostgresTableRow> Of(params PostgresTableRow[] tables) =>
        tables.ToDictionary(static table => table.Oid);

    // A column of "columns" that comes from an attribute of a table.
    public static PostgresColumnRow ColumnOf(
        int ordinal,
        string name,
        long typeOid,
        PostgresTableRow table,
        int attnum,
        int? length = null,
        int? precision = null,
        int? scale = null
    ) => new(ordinal, name, typeOid, table.Oid, attnum, length, precision, scale);
}
```

Create `tests/SqlSource.Tool.Tests/Describing/Postgres/ColumnOriginTests.cs`:

```csharp
using System;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// Where a column comes from, by the table OID and the attribute number of its row description.
public class ColumnOriginTests
{
    private static readonly PostgresTableRow Users = PostgresTables.Users;

    [Fact]
    public void Find_ColumnOfATable_IsItsTableAndItsAttribute()
    {
        var origin = ColumnOrigin
            .Find(PostgresTables.ColumnOf(0, "mail", 1043, Users, 3), PostgresTables.Of(Users))
            .ShouldNotBeNull();

        origin.Table.ShouldBeSameAs(Users);
        origin.Attribute.Name.ShouldBe("email");
    }

    [Fact]
    public void Find_ColumnOfNoTable_IsNoOrigin() =>
        ColumnOrigin
            .Find(new PostgresColumnRow(0, "n", 20, 0, 0, null, null, null), PostgresTables.Of(Users))
            .ShouldBeNull();

    // ctid and xmin report their table and an attribute number below zero.  The table may have no column at all,
    // and may be missing from an answer the test writes: neither is looked at.
    [Theory]
    [InlineData(-1)]
    [InlineData(-6)]
    [InlineData(0)]
    public void Find_SystemColumn_IsNoOrigin(int attributeNumber)
    {
        var column = new PostgresColumnRow(0, "ctid", 27, Users.Oid, attributeNumber, null, null, null);

        ColumnOrigin.Find(column, PostgresTables.Of(Users)).ShouldBeNull();
        ColumnOrigin.Find(column, PostgresTables.Of()).ShouldBeNull();
        ColumnOrigin.Find(column, PostgresTables.Of(PostgresTables.TableOf(Users.Oid, "users"))).ShouldBeNull();
    }

    [Fact]
    public void Find_TableTheAnswerLacks_ThrowsNamingTheColumn() =>
        Should
            .Throw<InvalidOperationException>(() =>
                ColumnOrigin.Find(PostgresTables.ColumnOf(0, "mail", 1043, Users, 3), PostgresTables.Of())
            )
            .Message.ShouldContain("'mail'");

    [Fact]
    public void Find_AttributeTheAnswerLacks_ThrowsNamingTheTable() =>
        Should
            .Throw<InvalidOperationException>(() =>
                ColumnOrigin.Find(PostgresTables.ColumnOf(0, "x", 23, Users, 42), PostgresTables.Of(Users))
            )
            .Message.ShouldContain("'users'");
}
```

Create `tests/SqlSource.Tool.Tests/Describing/Postgres/ColumnTypeTests.cs`:

```csharp
using System;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The protocol reports a domain column as its base type, so only the catalog knows that the column is a domain.
public class ColumnTypeTests
{
    private static readonly PostgresTableRow Table = PostgresTables.TableOf(
        7,
        "t",
        PostgresTables.Attribute(1, "plain", PostgresRows.Int4.Oid),
        PostgresTables.Attribute(2, "mail", PostgresRows.EmailT.Oid),
        PostgresTables.Attribute(3, "mail2", PostgresRows.D2.Oid),
        PostgresTables.Attribute(4, "tags", PostgresRows.TextArray.Oid)
    );

    private static long OidOf(long reported, int attnum, params PostgresTypeRow[] rows) =>
        ColumnTypes.OidOf(
            PostgresTables.ColumnOf(0, "c", reported, Table, attnum),
            ColumnOrigin.Find(PostgresTables.ColumnOf(0, "c", reported, Table, attnum), PostgresTables.Of(Table)),
            PostgresRows.Of(rows)
        );

    [Fact]
    public void OidOf_ColumnWhoseOriginIsADomain_IsTheDomain() =>
        OidOf(PostgresRows.Varchar.Oid, 2, PostgresRows.EmailT, PostgresRows.Varchar).ShouldBe(PostgresRows.EmailT.Oid);

    // The protocol reports the base at the end of the chain, and the column is the outermost domain.
    [Fact]
    public void OidOf_ColumnWhoseOriginIsADomainOverADomain_IsTheOutermost() =>
        OidOf(PostgresRows.Varchar.Oid, 3, PostgresRows.D2, PostgresRows.EmailT, PostgresRows.Varchar)
            .ShouldBe(PostgresRows.D2.Oid);

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void OidOf_ColumnWhoseOriginIsNoDomain_IsTheReportedType(int attnum)
    {
        var reported = Table.Columns[attnum == 1 ? 0 : 3].TypeOid;

        OidOf(reported, attnum, PostgresRows.Int4, PostgresRows.TextArray, PostgresRows.Text).ShouldBe(reported);
    }

    // The two answers disagree: the column keeps the type the protocol reported, which is what a reader gets.
    [Fact]
    public void OidOf_DomainWhoseBaseDoesNotReachTheReportedType_IsTheReportedType() =>
        OidOf(PostgresRows.Text.Oid, 2, PostgresRows.EmailT, PostgresRows.Varchar, PostgresRows.Text)
            .ShouldBe(PostgresRows.Text.Oid);

    [Fact]
    public void OidOf_ColumnWithoutAnOrigin_IsTheReportedType() =>
        ColumnTypes
            .OidOf(new PostgresColumnRow(0, "c", 1043, 0, 0, 254, null, null), null, PostgresRows.Of())
            .ShouldBe(1043);

    [Fact]
    public void OidOf_OriginTypeTheRowsLack_ThrowsNamingIt() =>
        Should
            .Throw<InvalidOperationException>(() => OidOf(PostgresRows.Varchar.Oid, 2, PostgresRows.Varchar))
            .Message.ShouldContain(PostgresRows.EmailT.Oid.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.ColumnOriginTests"`
Expected: the build fails: the records of `tables`, `ColumnOrigin` and `ColumnTypes` do not exist.

- [ ] **Step 2: Add the records of `tables`**

Append to `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs`:

```csharp
/// <summary>The request of <c>tables</c>.</summary>
/// <param name="Oids">The tables the columns of a statement name, sorted and each once.</param>
internal sealed record PostgresTablesRequest(IReadOnlyList<long> Oids);

/// <summary>The tables asked for, or why the catalog could not be read.</summary>
/// <param name="Tables">One for each table that exists, in <c>oid</c> order.</param>
/// <param name="Error">What the server said, or null.</param>
internal sealed record PostgresTablesAnswer(IReadOnlyList<PostgresTableRow>? Tables, PostgresServerError? Error);

/// <summary>What the catalog says of one relation.</summary>
/// <param name="Oid">The relation.</param>
/// <param name="Relkind"><c>r</c> for a table, <c>v</c> for a view, <c>m</c>, <c>f</c>, <c>p</c>.</param>
/// <param name="Schema">Its schema.</param>
/// <param name="Name">Its name.</param>
/// <param name="Columns">
/// Every attribute with <c>attnum</c> above zero that is not dropped, in <c>attnum</c> order.  A table without a
/// column of its own has none.
/// </param>
internal sealed record PostgresTableRow(
    long Oid,
    string Relkind,
    string Schema,
    string Name,
    IReadOnlyList<PostgresAttributeRow> Columns
);

/// <summary>What the catalog says of one column of a relation.</summary>
/// <param name="Attnum">The column's number in its relation.</param>
/// <param name="Name">Its name.</param>
/// <param name="NotNull"><c>attnotnull</c>.</param>
/// <param name="Identity"><c>attidentity</c>: empty, <c>a</c> or <c>d</c>.</param>
/// <param name="Generated"><c>attgenerated</c>: empty, <c>s</c> or <c>v</c>.</param>
/// <param name="TypeOid"><c>atttypid</c>: the declared type, a domain where the column is one.</param>
internal sealed record PostgresAttributeRow(
    int Attnum,
    string Name,
    bool NotNull,
    string Identity,
    string Generated,
    long TypeOid
);
```

- [ ] **Step 3: Write `ColumnOrigin` and `ColumnTypes`**

Create `src/SqlSource.Tool/Describing/Postgres/ColumnOrigin.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The column of a table or a view that a column of a result comes from, as the catalog holds both.
/// </summary>
/// <param name="Table">The relation.</param>
/// <param name="Attribute">Its column.</param>
internal sealed record ColumnOrigin(PostgresTableRow Table, PostgresAttributeRow Attribute)
{
    /// <summary>
    /// The origin of a column, or null: for a column that names no table, an expression for one, and for a system
    /// column, <c>ctid</c> or <c>xmin</c>, which names its table with an attribute number below zero.
    /// </summary>
    /// <param name="column">The row of <c>columns</c>.</param>
    /// <param name="tables">The answer of <c>tables</c>, by OID.</param>
    /// <exception cref="InvalidOperationException">
    /// The answer lacks the table or the attribute that the column names: a bug of the describer.
    /// </exception>
    public static ColumnOrigin? Find(PostgresColumnRow column, IReadOnlyDictionary<long, PostgresTableRow> tables)
    {
        if (column.TableOid == 0 || column.AttributeNumber <= 0)
        {
            return null;
        }

        if (!tables.TryGetValue(column.TableOid, out var table))
        {
            throw new InvalidOperationException(
                $"The answer to 'tables' holds no table {column.TableOid} for the column '{column.Name}'."
            );
        }

        return table.Columns.FirstOrDefault(attribute => attribute.Attnum == column.AttributeNumber) is { } found
            ? new ColumnOrigin(table, found)
            : throw new InvalidOperationException(
                $"The answer to 'tables' holds no attribute {column.AttributeNumber} of the table '{table.Name}' "
                    + $"for the column '{column.Name}'."
            );
    }
}
```

Create `src/SqlSource.Tool/Describing/Postgres/ColumnTypes.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Which type a column has.  The row description flattens a domain to its base type with the domain's modifier,
/// so only the catalog knows that a column is a domain.
/// </summary>
internal static class ColumnTypes
{
    /// <summary>
    /// The origin attribute's type when it is a domain whose chain of bases reaches the type the protocol
    /// reported; else the reported type.  Both are the server's answers: the one that names the domain says more,
    /// and an expression over a domain has no origin and says the base type, which is also true.
    /// </summary>
    /// <param name="column">The row of <c>columns</c>.</param>
    /// <param name="origin">Where the column comes from, or null.</param>
    /// <param name="rows">The rows of the closure, by OID.</param>
    /// <exception cref="InvalidOperationException">The rows lack a type of the origin's chain.</exception>
    public static long OidOf(
        PostgresColumnRow column,
        ColumnOrigin? origin,
        IReadOnlyDictionary<long, PostgresTypeRow> rows
    )
    {
        if (origin is null)
        {
            return column.TypeOid;
        }

        var oid = origin.Attribute.TypeOid;
        while (RowOf(oid, rows) is { Typtype: "d" } domain)
        {
            oid = domain.Typbasetype;
            if (oid == column.TypeOid)
            {
                return origin.Attribute.TypeOid;
            }
        }

        return column.TypeOid;
    }

    private static PostgresTypeRow RowOf(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows) =>
        rows.TryGetValue(oid, out var row)
            ? row
            : throw new InvalidOperationException($"The answer to 'types' holds no row for the type {oid}.");
}
```

Run: the two test classes of Step 1.
Expected: PASS.

- [ ] **Step 4: Write the failing tests of a column with an origin**

In `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresColumnsTests.cs`, each of the three calls of `PostgresColumns.Build` gains `null` as its second argument, the origin, and these tests are added:

```csharp
    [Fact]
    public void Build_ColumnWithAnOrigin_HasItAndSaysWhetherItIsIdentityOrComputed()
    {
        var users = PostgresTables.Users;
        var rows = PostgresRows.Of(PostgresRows.Int4, PostgresRows.Text);

        var id = Build(PostgresTables.ColumnOf(0, "user_id", 23, users, 1), users, rows);
        var upper = Build(PostgresTables.ColumnOf(1, "u", 25, users, 9), users, rows);

        id.Origin.ShouldBe(new SidecarOrigin("public", "users", "id"));
        (id.Identity, id.Computed).ShouldBe((true, false));
        upper.Origin.ShouldBe(new SidecarOrigin("public", "users", "name_upper"));
        (upper.Identity, upper.Computed).ShouldBe((false, true));
    }

    [Fact]
    public void Build_ColumnWithoutAnOrigin_SaysNothingOfIdentityOrComputed()
    {
        var column = PostgresColumns.Build(
            new PostgresColumnRow(0, "n", 23, 0, 0, null, null, null),
            null,
            PostgresRows.Of(PostgresRows.Int4)
        );

        column.Origin.ShouldBeNull();
        column.Identity.ShouldBeNull();
        column.Computed.ShouldBeNull();
    }

    // The domain is the column's type, and the modifier the protocol reported lands on its base.
    [Fact]
    public void Build_ColumnWhoseOriginIsADomain_IsTheDomainWithTheFacetsOnItsBase()
    {
        var users = PostgresTables.Users;

        var column = Build(
            PostgresTables.ColumnOf(0, "email", 1043, users, 3, length: 254),
            users,
            PostgresRows.Of(PostgresRows.EmailT, PostgresRows.Varchar)
        );

        column.Type.ShouldBe(
            TestSidecars.Postgres(
                "public.email_t",
                SidecarValues.Kind.Domain,
                "public",
                "email_t",
                baseType: TestSidecars.Postgres("character varying(254)", internalName: "varchar", length: 254)
            )
        );
    }

    private static SidecarColumn Build(
        PostgresColumnRow column,
        PostgresTableRow table,
        System.Collections.Generic.IReadOnlyDictionary<long, PostgresTypeRow> rows
    ) => PostgresColumns.Build(column, ColumnOrigin.Find(column, PostgresTables.Of(table)), rows);
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresColumnsTests"`
Expected: the build fails: `Build` takes two arguments.

- [ ] **Step 5: Give `PostgresColumns.Build` the origin**

In `src/SqlSource.Tool/Describing/Postgres/PostgresColumns.cs`, `Build` becomes:

```csharp
    /// <summary>The column, with its type, its facets and its origin.</summary>
    /// <param name="column">The row of <c>columns</c>.</param>
    /// <param name="origin">Where the column comes from, or null.</param>
    /// <param name="rows">The rows of the closure, by OID.</param>
    /// <exception cref="InvalidOperationException">
    /// The rows lack a type of the column, or the type is a pseudo-type.
    /// </exception>
    public static SidecarColumn Build(
        PostgresColumnRow column,
        ColumnOrigin? origin,
        IReadOnlyDictionary<long, PostgresTypeRow> rows
    ) =>
        new(
            column.Ordinal,
            column.Name,
            PostgresTypes.Build(ColumnTypes.OidOf(column, origin, rows), rows, FacetsOf(column, rows)),
            null,
            SidecarValues.NullableSource.NoOrigin,
            origin is null ? null : new SidecarOrigin(origin.Table.Schema, origin.Table.Name, origin.Attribute.Name),
            origin is null ? null : origin.Attribute.Identity.Length > 0,
            origin is null ? null : origin.Attribute.Generated.Length > 0
        );
```

Run: the test class of Step 4.
Expected: PASS.

- [ ] **Step 6: Write the failing tests of the session**

In `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`, beside `Resolves`:

```csharp
    private static PostgresScript Tables(PostgresScript script, long[] oids, params PostgresTableRow[] tables) =>
        script.Answer("tables", new PostgresTablesRequest(oids), new PostgresTablesAnswer(tables, null));
```

and at the end of the class:

```csharp
    // "tables" is asked once, for each table once and sorted, whatever the order of the columns.  "types" is then
    // asked for the types of the origin attributes too, which is where a domain is found.
    [Fact]
    public async Task DescribeAsync_ColumnsOfTables_AskTablesOnceAndTypesForTheOriginsToo()
    {
        var users = PostgresTables.Users;
        var orders = PostgresTables.Orders;
        var script = new PostgresScript().Open();
        _ = PreparesOnly(script, 1, "SELECT o.total, u.email, u.id FROM orders o, users u", [], new([], null));
        _ = Columns(
            script,
            1,
            0,
            PostgresTables.ColumnOf(0, "total", 1700, orders, 3, precision: 18, scale: 2),
            PostgresTables.ColumnOf(1, "email", 1043, users, 3, length: 254),
            PostgresTables.ColumnOf(2, "id", 23, users, 1)
        );
        _ = Tables(script, [users.Oid, orders.Oid], users, orders);
        _ = Resolves(
            script,
            [23, 1043, 1700, PostgresRows.EmailT.Oid],
            PostgresRows.Int4,
            PostgresRows.Varchar,
            PostgresRows.Numeric,
            PostgresRows.EmailT
        );
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT o.total, u.email, u.id FROM orders o, users u"),
            TestContext.Current.CancellationToken
        );

        var columns = described.Value.ShouldNotBeNull().Columns.ShouldNotBeNull();
        columns.Select(column => column.Type.Name).ShouldBe(["numeric(18,2)", "public.email_t", "integer"]);
        columns
            .Select(column => column.Origin)
            .ShouldBe([
                new SidecarOrigin("public", "orders", "total"),
                new SidecarOrigin("public", "users", "email"),
                new SidecarOrigin("public", "users", "id"),
            ]);
        columns.Select(column => column.Identity).ShouldBe([false, false, true]);
        exchange.Unused.ShouldBeEmpty();
    }

    // A system column names its table, which is asked for, and has no origin.
    [Fact]
    public async Task DescribeAsync_SystemColumn_HasNoOrigin()
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = PreparesOnly(script, 1, "SELECT ctid FROM users", [], new([], null));
        _ = Columns(script, 1, 0, new PostgresColumnRow(0, "ctid", 27, users.Oid, -1, null, null, null));
        _ = Tables(script, [users.Oid], users);
        _ = Resolves(script, [27], PostgresRows.Tid);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT ctid FROM users"),
            TestContext.Current.CancellationToken
        );

        var column = described.Value.ShouldNotBeNull().Columns.ShouldNotBeNull().ShouldHaveSingleItem();
        column.Origin.ShouldBeNull();
        column.Type.Name.ShouldBe("tid");
        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task DescribeAsync_TablesThatFails_IsSqlsrc225AtCatalog()
    {
        var refused = new PostgresServerError("42501", "permission denied for table pg_attribute", null, null, 300);
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = PreparesOnly(script, 1, "SELECT id FROM users", [], new([], null));
        _ = Columns(script, 1, 0, PostgresTables.ColumnOf(0, "id", 23, users, 1));
        _ = script.Answer(
            "tables",
            new PostgresTablesRequest([users.Oid]),
            new PostgresTablesAnswer(null, refused)
        );
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Request("SELECT id FROM users"), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Step.ShouldBe(DescribeStep.Catalog);
        failure.ServerLines.ShouldBe(["42501: permission denied for table pg_attribute"]);
        exchange.Unused.ShouldBeEmpty();
    }
```

`DescribeAsync_StatementWithColumns_IsRowsWithEachColumnsTypeAndFacets` of Task 3 already holds that `tables` is not asked when no column names a table: its script has no answer for it.

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresSessionTests"`
Expected: FAIL: the three new tests, each for a call the session does not make.

- [ ] **Step 7: Ask `tables` in the session, and read the catalog**

In `src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs`, beside `TypesSql`:

```csharp
    // Every relation asked for with its columns, in one query.  The join is a left one, so that a table without a
    // column of its own is answered too, with one row that holds no attribute.
    private const string TablesSql = """
        SELECT
            c.oid,
            c.relkind::text,
            n.nspname::text,
            c.relname::text,
            a.attnum,
            a.attname::text,
            a.attnotnull,
            a.attidentity::text,
            a.attgenerated::text,
            a.atttypid
        FROM pg_catalog.pg_class AS c
        JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
        LEFT JOIN pg_catalog.pg_attribute AS a ON a.attrelid = c.oid AND a.attnum > 0 AND NOT a.attisdropped
        WHERE c.oid = ANY (@oids)
        ORDER BY c.oid, a.attnum
        """;
```

and after `ColumnsAsync`:

```csharp
    /// <summary>Asks <c>tables</c>: one query of the catalog for every table the columns of a statement name.</summary>
    public async Task<PostgresTablesAnswer> TablesAsync(PostgresTablesRequest asked, CancellationToken token)
    {
        try
        {
            await using var command = await connection.CommandAsync(TablesSql, token);
            _ = command.Parameters.Add(
                new NpgsqlParameter("oids", asked.Oids.Select(static oid => (uint)oid).ToArray())
                {
                    DataTypeName = "oid[]",
                }
            );
            await using var reader = await command.ExecuteReaderAsync(token);
            var tables = new List<PostgresTableRow>();
            List<PostgresAttributeRow> columns = [];
            while (await reader.ReadAsync(token))
            {
                long oid = await reader.GetFieldValueAsync<uint>(0, token);
                if (tables.Count == 0 || tables[^1].Oid != oid)
                {
                    columns = [];
                    tables.Add(
                        new PostgresTableRow(oid, reader.GetString(1), reader.GetString(2), reader.GetString(3), columns)
                    );
                }

                // The one row of a table without a column of its own holds no attribute.
                if (!await reader.IsDBNullAsync(4, token))
                {
                    columns.Add(
                        new PostgresAttributeRow(
                            reader.GetInt16(4),
                            reader.GetString(5),
                            reader.GetBoolean(6),
                            reader.GetString(7),
                            reader.GetString(8),
                            await reader.GetFieldValueAsync<uint>(9, token)
                        )
                    );
                }
            }

            return new PostgresTablesAnswer(tables, null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresTablesAnswer(null, PostgresConnection.ErrorOf(exception));
        }
    }
```

In `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`, after `Columns`:

```csharp
    /// <summary>
    /// <c>SQLSRC225</c> at the step <c>catalog</c>: the tables of the columns could not be read.  There is no
    /// position line: a position the server gives is in the tool's own query of the catalog.
    /// </summary>
    public static DescribeFailure Tables(PostgresServerError error, DescribeRequest query) =>
        Rejected(error, query, DescribeStep.Catalog, ServerLines(error with { Position = null }, 0));
```

In `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`, in `DescribePreparedAsync`, everything from `var rows = await RowsOfAsync(` to the end of the method becomes:

```csharp
        var tables = await TablesOfAsync(columns, cancellationToken);
        if (!tables.Succeeded)
        {
            return Failed(PostgresErrors.Tables(tables.Failure, request));
        }

        ColumnOrigin?[] origins = [.. columns.Select(column => ColumnOrigin.Find(column, tables.Value))];

        // After "tables": the type of an origin attribute is where a domain is found, so the one request of "types"
        // needs the catalog's answer.
        var rows = await RowsOfAsync(
            [
                .. parameterOids,
                .. columns.Select(static column => column.TypeOid),
                .. origins.OfType<ColumnOrigin>().Select(static origin => origin.Attribute.TypeOid),
            ],
            cancellationToken
        );
        if (!rows.Succeeded)
        {
            return Failed(PostgresErrors.Types(rows.Failure, request));
        }

        var parameters = TypeParameters(request, parameterOids, rows.Value);
        if (!parameters.Succeeded)
        {
            return Failed(parameters.Failure);
        }

        return Described(
            parameters.Value,
            [.. columns.Select((column, index) => PostgresColumns.Build(column, origins[index], rows.Value))]
        );
```

The comment above `DescribePreparedAsync` becomes `// What is asked of a statement that was prepared, in this order: its columns, the tables they name, and every type that the parameters, the columns and their origins have, once.`

After `RowsOfAsync`:

```csharp
    // The tables the columns name, by OID.  The request is sorted and holds each table once.  Nothing is asked when
    // no column names a table.
    private async Task<Outcome<IReadOnlyDictionary<long, PostgresTableRow>, PostgresServerError>> TablesOfAsync(
        IReadOnlyList<PostgresColumnRow> columns,
        CancellationToken cancellationToken
    )
    {
        long[] oids =
        [
            .. columns.Select(static column => column.TableOid).Where(static oid => oid != 0).Distinct().Order(),
        ];
        if (oids.Length == 0)
        {
            return Outcome<IReadOnlyDictionary<long, PostgresTableRow>, PostgresServerError>.Of(
                new Dictionary<long, PostgresTableRow>()
            );
        }

        var answer = await TablesAsync(new PostgresTablesRequest(oids), cancellationToken);
        return answer.Error is { } error
            ? Outcome<IReadOnlyDictionary<long, PostgresTableRow>, PostgresServerError>.Failed(error)
            : Outcome<IReadOnlyDictionary<long, PostgresTableRow>, PostgresServerError>.Of(
                (
                    answer.Tables
                    ?? throw new InvalidOperationException("The answer to 'tables' holds no tables and no error.")
                ).ToDictionary(static table => table.Oid)
            );
    }
```

Beside the wrapper `ColumnsAsync`:

```csharp
    private Task<PostgresTablesAnswer> TablesAsync(PostgresTablesRequest request, CancellationToken cancellationToken) =>
        _exchange.AskAsync("tables", request, _live.TablesAsync, cancellationToken);
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS, the fixtures included: a statement without rows asks no `tables`, so no capture changes.

- [ ] **Step 8: Write the container tests**

In `tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs`, update the test of the calls' order and add the rest.  `Describe_QueryWithoutATable_AsksColumnsAfterPrepareAndTypesOnce` stays as it is.  Add:

```csharp
    // Two tables of these tests alone: one with the modifiers the fixture schema lacks, and one with no column.
    // They are made once and left: the container is thrown away, and the fixtures are recorded in one of their own.
    private const string OwnTables = """
        CREATE TABLE IF NOT EXISTS column_types (
            n0 numeric(18, 0),
            n1 numeric(5),
            n numeric,
            ts3 timestamp(3),
            tstz0 timestamptz(0),
            ttz2 timetz(2),
            t4 time(4),
            i3 interval(3),
            b3 bit(3),
            b1 bit,
            vb5 varbit(5),
            vb varbit,
            c1 char,
            bp bpchar,
            vc varchar,
            em email_t NOT NULL,
            ems email_t[],
            statuses user_status[]
        );
        CREATE TABLE IF NOT EXISTS column_empty ();
        """;

    // The name of each column's type as the server spells it with its modifier, with the schema before a type that
    // is not of pg_catalog: format_type leaves the schema out of a type on the search path.
    private const string FormatTypes = """
        SELECT CASE WHEN n.nspname = 'pg_catalog' THEN '' ELSE n.nspname || '.' END
            || pg_catalog.format_type(a.atttypid, a.atttypmod)
        FROM pg_catalog.pg_attribute AS a
        JOIN pg_catalog.pg_type AS t ON t.oid = a.atttypid
        JOIN pg_catalog.pg_namespace AS n ON n.oid = t.typnamespace
        WHERE a.attrelid = CAST(@table AS regclass) AND a.attnum > 0 AND NOT a.attisdropped
        ORDER BY a.attnum
        """;

    private async Task MakeOwnTablesAsync()
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(OwnTables);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<string>> FormatTypesOfAsync(string table)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(FormatTypes);
        _ = command.Parameters.AddWithValue("table", table);
        await using var reader = await command.ExecuteReaderAsync(token);
        var names = new List<string>();
        while (await reader.ReadAsync(token))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static SidecarOrigin Origin(string table, string column, string schema = "public") =>
        new(schema, table, column);

    // Every column of every table and view of the fixture schema, and of the table of these tests: the name the
    // describer spells is the one format_type gives the column, domains, enums, arrays and ranges included.
    [Theory]
    [InlineData("users")]
    [InlineData("orders")]
    [InlineData("bookings")]
    [InlineData("samples")]
    [InlineData("billing.invoices")]
    [InlineData("active_users")]
    [InlineData("user_orders")]
    [InlineData("column_types")]
    public async Task Describe_EveryColumnOfATable_IsSpelledAsFormatTypeSpellsIt(string table)
    {
        await MakeOwnTablesAsync();

        var columns = await ColumnsOfAsync($"SELECT * FROM {table}");

        TypeNamesOf(columns).ShouldBe(await FormatTypesOfAsync(table));
    }

    [Fact]
    public async Task Describe_ColumnsOfATable_HaveTheirOrigin()
    {
        var columns = await ColumnsOfAsync("SELECT u.id AS user_id, email, amount FROM users u, billing.invoices");

        columns
            .Select(column => column.Origin)
            .ShouldBe([Origin("users", "id"), Origin("users", "email"), Origin("invoices", "amount", "billing")]);
    }

    // ctid and xmin name their table with an attribute number below zero, and a table may have no column at all.
    [Fact]
    public async Task Describe_SystemColumns_HaveNoOrigin()
    {
        await MakeOwnTablesAsync();

        var ofUsers = await ColumnsOfAsync("SELECT ctid, xmin, id FROM users");
        var ofEmpty = await ColumnsOfAsync("SELECT ctid FROM column_empty");

        ofUsers.Select(column => column.Origin).ShouldBe([null, null, Origin("users", "id")]);
        TypeNamesOf(ofUsers).ShouldBe(["tid", "xid", "integer"]);
        ofEmpty.ShouldHaveSingleItem().Origin.ShouldBeNull();
    }

    [Fact]
    public async Task Describe_IdentityAndGeneratedColumns_SayWhatTheyAre()
    {
        var columns = await ColumnsOfAsync(
            "SELECT u.id, u.name, u.name_upper, o.id AS order_id FROM users u, orders o"
        );

        columns.Select(column => column.Identity).ShouldBe([true, false, false, true]);
        columns.Select(column => column.Computed).ShouldBe([false, false, true, false]);
    }

    [Fact]
    public async Task Describe_ExpressionColumns_HaveNoOriginAndSayNothingOfIdentity()
    {
        var columns = await ColumnsOfAsync("SELECT count(*) AS n, upper(name) AS u FROM users GROUP BY name");

        columns.ShouldAllBe(column => column.Origin == null && column.Identity == null && column.Computed == null);
        TypeNamesOf(columns).ShouldBe(["bigint", "text"]);
    }

    // The column is its domain.  An expression over it has no origin and says the base type; a cast to the domain
    // says the base type with the domain's modifier, as the protocol reports it.
    [Fact]
    public async Task Describe_DomainColumnAndExpressionsOverIt_SayTheDomainOrItsBase()
    {
        var columns = await ColumnsOfAsync(
            "SELECT email, COALESCE(email, 'x@y') AS c, NULL::email_t AS n, email::text AS t FROM users"
        );

        TypeNamesOf(columns).ShouldBe(["public.email_t", "character varying", "character varying(254)", "text"]);
        var domain = columns[0].Type.ShouldBeOfType<PostgresType>();
        domain.Kind.ShouldBe(SidecarValues.Kind.Domain);
        domain.Base.ShouldNotBeNull().Name.ShouldBe("character varying(254)");
        columns.Skip(1).ShouldAllBe(column => column.Origin == null);
    }

    // The USING column of an inner join is the left table's; of a full join it is a COALESCE of both, and no
    // table's.
    [Fact]
    public async Task Describe_UsingColumn_HasTheLeftOriginOfAnInnerJoinAndNoneOfAFullJoin()
    {
        var inner = await ColumnsOfAsync("SELECT user_id FROM orders JOIN bookings USING (user_id)");
        var full = await ColumnsOfAsync("SELECT user_id FROM orders FULL JOIN bookings USING (user_id)");

        inner.ShouldHaveSingleItem().Origin.ShouldBe(Origin("orders", "user_id"));
        full.ShouldHaveSingleItem().Origin.ShouldBeNull();
    }

    [Fact]
    public async Task Describe_Union_HasNoOrigin() =>
        (await ColumnsOfAsync("SELECT id FROM users UNION ALL SELECT user_id FROM orders"))
            .ShouldHaveSingleItem()
            .Origin.ShouldBeNull();

    [Fact]
    public async Task Describe_SubqueryAndCte_KeepOrigins()
    {
        var columns = await ColumnsOfAsync(
            "WITH c AS (SELECT id, name FROM users) "
                + "SELECT c.id, s.total FROM c JOIN (SELECT user_id, total FROM orders) AS s ON s.user_id = c.id"
        );

        columns.Select(column => column.Origin).ShouldBe([Origin("users", "id"), Origin("orders", "total")]);
    }

    // The origin of a view's column is the view: what stands behind it is the view's own business.
    [Fact]
    public async Task Describe_ColumnOfAView_HasTheViewAsItsOrigin() =>
        (await ColumnsOfAsync("SELECT name FROM active_users"))
            .ShouldHaveSingleItem()
            .Origin.ShouldBe(Origin("active_users", "name"));

    [Fact]
    public async Task Describe_QueryOfATable_AsksTablesBetweenColumnsAndTypes()
    {
        var capture = new DescribeCapture("postgres");

        _ = await ShouldDescribeAsync("SELECT id FROM users", new CapturingExchange(LiveExchange.Instance, capture));

        capture
            .Calls.Select(call => call.Method)
            .ShouldBe(["open", "prepare", "columns", "tables", "types", "deallocate"]);
    }
```

Run: `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.ColumnTests"`
Expected: PASS.

Run: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.ColumnTests"`
Expected: PASS.

- [ ] **Step 9: Keep `src/SqlSource.Tool/AGENTS.md` true**

In the bullet that begins `**The calls and their records are a contract with the captures.**`, the list of methods becomes `` `open`, `prepare`, `columns`, `tables`, `types` and `deallocate` ``.

In the bullet that begins `**`Describing/Postgres/` is the PostgreSQL describer, and it is registered nowhere.**`, `asks `types` once for the closure of the OIDs of the parameters and the columns` becomes `asks `tables` for the tables the columns name, asks `types` once for the closure of the OIDs of the parameters, the columns and their origin attributes`.

- [ ] **Step 10: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: every step `passed`.

```bash
git add src/SqlSource.Tool tests/SqlSource.Tool.Tests tests/SqlSource.Tests.Postgres
```

```bash
git commit -m "Give a column its origin, and its domain through it

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Nullability by the catalog, the gate, and the heuristic

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/ColumnNullability.cs`
- Create: `src/SqlSource.Tool/Describing/Postgres/OuterJoinGate.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresColumns.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/ColumnNullabilityTests.cs`
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/OuterJoinGateTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresColumnsTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs`

**Interfaces:**
- Consumes: `ColumnOrigin`, `PostgresColumns.Build`, `Described` of the session.
- Produces:
  - `ColumnNullability.Of(ColumnOrigin? origin, bool gated)` returning `(bool? Nullable, string Source)`.
  - `OuterJoinGate.IsGated(string sql)` returning `bool`.
  - `PostgresColumns.Build(PostgresColumnRow column, ColumnOrigin? origin, IReadOnlyDictionary<long, PostgresTypeRow> rows, bool gated)`, its final shape.
  - `PostgresSession.Described(IReadOnlyList<DescribedParameter> parameters, IReadOnlyList<SidecarColumn> columns, bool gated)`.

- [ ] **Step 1: Write the failing tests of the gate**

Create `tests/SqlSource.Tool.Tests/Describing/Postgres/OuterJoinGateTests.cs`:

```csharp
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The gate: whether a query holds a word that can make a NOT NULL column of a table come back NULL.  It reads
// words and no structure, so it may say yes where there is no join, and never says no where there is one.
public class OuterJoinGateTests
{
    [Theory]
    [InlineData("LEFT")]
    [InlineData("RIGHT")]
    [InlineData("FULL")]
    [InlineData("OUTER")]
    [InlineData("ROLLUP")]
    [InlineData("CUBE")]
    [InlineData("GROUPING")]
    public void IsGated_EachWordInAnyCase_IsGated(string word)
    {
        var mixed = char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();

        OuterJoinGate.IsGated($"SELECT 1 FROM a {word} b").ShouldBeTrue();
        OuterJoinGate.IsGated($"SELECT 1 FROM a {word.ToLowerInvariant()} b").ShouldBeTrue();
        OuterJoinGate.IsGated($"SELECT 1 FROM a {mixed} b").ShouldBeTrue();
    }

    [Theory]
    [InlineData("SELECT u.id FROM users u LEFT JOIN orders o ON o.user_id = u.id")]
    [InlineData("SELECT status, count(*) FROM users GROUP BY ROLLUP (status)")]
    [InlineData("SELECT status, count(*) FROM users GROUP BY GROUPING SETS ((status), ())")]
    [InlineData("SELECT status, grouping(status) FROM users GROUP BY CUBE(status)")]
    // A function, and no join: the gate cannot tell, and the walk finds nothing to null-extend.
    [InlineData("SELECT left(name, 3) FROM users")]
    [InlineData("left")]
    [InlineData("SELECT 1 FROM a\nLEFT\nJOIN b ON true")]
    [InlineData("SELECT 1 FROM a NATURAL FULL JOIN b")]
    [InlineData("SELECT 1 FROM \"a\"LEFT JOIN b ON true")]
    public void IsGated_QueryThatHoldsAWord_IsGated(string sql) => OuterJoinGate.IsGated(sql).ShouldBeTrue();

    [Theory]
    [InlineData("SELECT id FROM users")]
    [InlineData("SELECT 1 FROM a JOIN b ON true")]
    [InlineData("SELECT status, count(*) FROM users GROUP BY status")]
    // Inside a string, a comment, a hint, a quoted identifier and a dollar-quoted string.
    [InlineData("SELECT 'left join' FROM users")]
    [InlineData("SELECT id FROM users -- left join")]
    [InlineData("SELECT id /* FULL OUTER */ FROM users")]
    [InlineData("SELECT /*+ rollup */ id FROM users")]
    [InlineData("SELECT \"left\", \"FULL\" FROM users")]
    [InlineData("SELECT $$ left $$ FROM users")]
    [InlineData("SELECT $tag$ cube $tag$ FROM users")]
    // A longer word that holds one.
    [InlineData("SELECT leftover FROM users")]
    [InlineData("SELECT x_left, left_x, left1, a.fullname FROM users")]
    [InlineData("SELECT groupings, cubed, outermost, rights FROM users")]
    // A parameter is no word of the SQL.
    [InlineData("SELECT id FROM users WHERE id = @left")]
    [InlineData("")]
    public void IsGated_QueryThatHoldsNoWord_IsNotGated(string sql) => OuterJoinGate.IsGated(sql).ShouldBeFalse();
}
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.OuterJoinGateTests"`
Expected: the build fails: `OuterJoinGate` does not exist.

- [ ] **Step 2: Write `OuterJoinGate`**

Create `src/SqlSource.Tool/Describing/Postgres/OuterJoinGate.cs`:

```csharp
using System;
using SqlSource.Parsing;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The outer-join gate: whether a query holds a word that can make a <c>NOT NULL</c> column of a table come back
/// <c>NULL</c>.  For a query that is gated the catalog's <c>NOT NULL</c> is not believed.
/// </summary>
/// <remarks>
/// The words are the four of an outer join and the three of the grouping sets, which put <c>NULL</c> into a
/// grouping column on a subtotal row with no join in sight; <c>GROUPING</c> covers <c>GROUPING SETS</c> and the
/// function.  The lexer of the generator says what is a string, a comment or a quoted identifier, and only what
/// is left is searched.  The gate reads words and no structure: <c>left(name, 3)</c> gates a query that has no
/// join, which costs one walk that finds nothing and never a wrong answer.
/// </remarks>
internal static class OuterJoinGate
{
    private static readonly string[] Words = ["LEFT", "RIGHT", "FULL", "OUTER", "ROLLUP", "CUBE", "GROUPING"];

    /// <summary>Whether the SQL holds one of the words, as a whole word and in any case.</summary>
    /// <exception cref="InvalidOperationException">
    /// The SQL does not lex: a bug of the plan, whose parser read the same text.
    /// </exception>
    public static bool IsGated(string sql)
    {
        var lexed = SqlLexer.Lex(sql, SqlDialectRules.PostgreSql);
        if (lexed.Error is not null)
        {
            throw new InvalidOperationException(
                "The sample SQL of a query does not lex, and the plan's parser read it."
            );
        }

        foreach (var lexeme in lexed.Lexemes)
        {
            if (lexeme.Kind == SqlLexemeKind.Text && HoldsAWord(sql.AsSpan(lexeme.Span.Start, lexeme.Span.Length)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HoldsAWord(ReadOnlySpan<char> text)
    {
        foreach (var word in Words)
        {
            var from = 0;
            while (text[from..].IndexOf(word, StringComparison.OrdinalIgnoreCase) is >= 0 and var found)
            {
                var start = from + found;
                var end = start + word.Length;
                if (!IsOfAName(text, start - 1) && !IsOfAName(text, end))
                {
                    return true;
                }

                from = start + 1;
            }
        }

        return false;
    }

    // Whether the character there continues an identifier, by the lexer's rule.  The edge of a lexeme does not.
    private static bool IsOfAName(ReadOnlySpan<char> text, int index) =>
        index >= 0 && index < text.Length && SqlLexer.IsParameterNameCharacter(text[index]);
}
```

Run: the test class of Step 1.
Expected: PASS.  If the case `SELECT /*+ rollup */ id FROM users` fails, the lexer reads a block hint of PostgreSQL as something other than a `Hint` or a `BlockComment` lexeme: read `SqlLexer` before changing anything, and tell the user if a hint is a `Text` lexeme.

- [ ] **Step 3: Write the failing tests of the nullability**

Create `tests/SqlSource.Tool.Tests/Describing/Postgres/ColumnNullabilityTests.cs`:

```csharp
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The five rows of the spec's table, in its order: the first that fits a column is its answer.
public class ColumnNullabilityTests
{
    private static ColumnOrigin Origin(string relkind, bool notNull) =>
        new(
            PostgresTables.TableOf(7, "t") with { Relkind = relkind },
            PostgresTables.Attribute(1, "c", PostgresRows.Int4.Oid, notNull)
        );

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Of_ColumnWithoutAnOrigin_IsUnknownAndSaysNoOrigin(bool gated) =>
        ColumnNullability.Of(null, gated).ShouldBe((null, SidecarValues.NullableSource.NoOrigin));

    // PostgreSQL does not record NOT NULL for a view's column, and does not enforce a foreign table's.  The answer
    // is the same whether the query is gated or not, and whatever the catalog says.
    [Theory]
    [InlineData("v")]
    [InlineData("m")]
    [InlineData("f")]
    public void Of_OriginInAViewAMaterializedViewOrAForeignTable_IsUnknownAndSaysView(string relkind)
    {
        foreach (var gated in new[] { false, true })
        {
            foreach (var notNull in new[] { false, true })
            {
                ColumnNullability
                    .Of(Origin(relkind, notNull), gated)
                    .ShouldBe((null, SidecarValues.NullableSource.View));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Of_GatedQueryAndOriginInATable_IsNullableByTheHeuristic(bool notNull) =>
        ColumnNullability.Of(Origin("r", notNull), gated: true).ShouldBe((true, SidecarValues.NullableSource.Heuristic));

    [Fact]
    public void Of_NotNullColumnOfATable_IsNotNullableByTheCatalog() =>
        ColumnNullability.Of(Origin("r", notNull: true), gated: false).ShouldBe((false, SidecarValues.NullableSource.Catalog));

    [Fact]
    public void Of_NullableColumnOfATable_IsNullableByTheCatalog() =>
        ColumnNullability.Of(Origin("r", notNull: false), gated: false).ShouldBe((true, SidecarValues.NullableSource.Catalog));

    // A partitioned table enforces its constraints as a table does.
    [Fact]
    public void Of_NotNullColumnOfAPartitionedTable_IsNotNullableByTheCatalog() =>
        ColumnNullability.Of(Origin("p", notNull: true), gated: false).ShouldBe((false, SidecarValues.NullableSource.Catalog));
}
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.ColumnNullabilityTests"`
Expected: the build fails: `ColumnNullability` does not exist.

- [ ] **Step 4: Write `ColumnNullability`, and give `Build` the gate**

Create `src/SqlSource.Tool/Describing/Postgres/ColumnNullability.cs`:

```csharp
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Whether a column can be <c>NULL</c>, as far as the catalog can answer, and where the answer came from.  The
/// describer records; the generator decides what an unknown means.
/// </summary>
/// <remarks>
/// An answer is never "not nullable" unless the catalog says <c>NOT NULL</c>, the origin is a table, and no outer
/// join can have touched the column.  Until the walk exists, a gated query makes every column of a table
/// nullable.
/// </remarks>
internal static class ColumnNullability
{
    /// <summary>The first row of the table that fits.</summary>
    /// <param name="origin">Where the column comes from, or null.</param>
    /// <param name="gated">Whether <see cref="OuterJoinGate" /> found a word in the query.</param>
    public static (bool? Nullable, string Source) Of(ColumnOrigin? origin, bool gated) =>
        origin switch
        {
            null => (null, SidecarValues.NullableSource.NoOrigin),

            // PostgreSQL records no NOT NULL for a view's column and does not enforce a foreign table's.
            { Table.Relkind: "v" or "m" or "f" } => (null, SidecarValues.NullableSource.View),
            _ when gated => (true, SidecarValues.NullableSource.Heuristic),
            { } found => (!found.Attribute.NotNull, SidecarValues.NullableSource.Catalog),
        };
}
```

If the compiler finds no type for the switch, write the first two arms as `((bool?)null, ...)`.

In `src/SqlSource.Tool/Describing/Postgres/PostgresColumns.cs`, `Build` becomes:

```csharp
    /// <summary>The column, with its type, its facets, its origin and its nullability.</summary>
    /// <param name="column">The row of <c>columns</c>.</param>
    /// <param name="origin">Where the column comes from, or null.</param>
    /// <param name="rows">The rows of the closure, by OID.</param>
    /// <param name="gated">Whether the query is gated.</param>
    /// <exception cref="InvalidOperationException">
    /// The rows lack a type of the column, or the type is a pseudo-type.
    /// </exception>
    public static SidecarColumn Build(
        PostgresColumnRow column,
        ColumnOrigin? origin,
        IReadOnlyDictionary<long, PostgresTypeRow> rows,
        bool gated
    )
    {
        var (nullable, source) = ColumnNullability.Of(origin, gated);
        return new SidecarColumn(
            column.Ordinal,
            column.Name,
            PostgresTypes.Build(ColumnTypes.OidOf(column, origin, rows), rows, FacetsOf(column, rows)),
            nullable,
            source,
            origin is null ? null : new SidecarOrigin(origin.Table.Schema, origin.Table.Name, origin.Attribute.Name),
            origin is null ? null : origin.Attribute.Identity.Length > 0,
            origin is null ? null : origin.Attribute.Generated.Length > 0
        );
    }
```

In `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresColumnsTests.cs`, every call of `PostgresColumns.Build` gains `false` as its last argument, the private `Build` helper gains a `bool gated = false` parameter that it passes on, and this test is added:

```csharp
    [Fact]
    public void Build_NotNullColumnOfATable_IsNotNullableUnlessTheQueryIsGated()
    {
        var users = PostgresTables.Users;
        var rows = PostgresRows.Of(PostgresRows.Int4);
        var column = PostgresTables.ColumnOf(0, "id", 23, users, 1);

        var plain = Build(column, users, rows);
        var gated = Build(column, users, rows, gated: true);

        (plain.Nullable, plain.NullableSource).ShouldBe((false, SidecarValues.NullableSource.Catalog));
        (gated.Nullable, gated.NullableSource).ShouldBe((true, SidecarValues.NullableSource.Heuristic));
    }
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.ColumnNullabilityTests"` and the same for `PostgresColumnsTests`.
Expected: the build fails in `PostgresSession.cs`, which calls `Build` with three arguments.  Step 6 mends it.

- [ ] **Step 5: Write the failing tests of the session**

Add to `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`:

```csharp
    // One script for a query of users.id and an aggregate, whatever its SQL says of a join.
    private static async Task<QueryDescription> DescribeUsersIdAsync(string sql)
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = PreparesOnly(script, 1, sql, [], new([], null));
        _ = Columns(script, 1, 0, PostgresTables.ColumnOf(0, "id", 23, users, 1), Expression(1, "n", 20));
        _ = Tables(script, [users.Oid], users);
        _ = Resolves(script, [20, 23], PostgresRows.Int8, PostgresRows.Int4);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Request(sql), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        return described.Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task DescribeAsync_QueryWithoutAGateWord_TakesNullabilityFromTheCatalogAndNeedsNoPlan()
    {
        var description = await DescribeUsersIdAsync("SELECT id, count(*) OVER () AS n FROM users");

        description.Plan.ShouldBe(SidecarValues.Plan.NotNeeded);
        description
            .Columns.ShouldNotBeNull()
            .Select(column => (column.Nullable, column.NullableSource))
            .ShouldBe([(false, "catalog"), (null, "no-origin")]);
    }

    // The walk does not exist yet, which is what "unavailable" says.
    [Fact]
    public async Task DescribeAsync_GatedQuery_MakesEveryOriginColumnNullableAndSaysThePlanIsUnavailable()
    {
        var description = await DescribeUsersIdAsync(
            "SELECT u.id, count(*) OVER () AS n FROM users u LEFT JOIN orders o ON o.user_id = u.id"
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NoOrigin);
        description.MatchesTable.ShouldBeNull();
        description
            .Columns.ShouldNotBeNull()
            .Select(column => (column.Nullable, column.NullableSource))
            .ShouldBe([(true, "heuristic"), (null, "no-origin")]);
    }

    // A statement without rows has no provenance, gate word or not.
    [Fact]
    public async Task DescribeAsync_GatedStatementWithoutRows_HasNoPlan()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(script, 1, "DELETE FROM users WHERE left(name, 1) = 'a'", [], new([], null));
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("DELETE FROM users WHERE left(name, 1) = 'a'"),
            TestContext.Current.CancellationToken
        );

        described.Value.ShouldNotBeNull().Plan.ShouldBeNull();
    }
```

Add to `tests/SqlSource.Tool.Tests/PostgresRows.cs`, after `Int4`:

```csharp
    public static PostgresTypeRow Int8 { get; } = BaseOf(20, "int8", "bigint", "N");
```

- [ ] **Step 6: Gate the query in the session**

In `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`, the end of `DescribePreparedAsync`, from `return Described(`, becomes:

```csharp
        // A statement without rows has nothing a join could null-extend.
        var gated = columns.Count > 0 && OuterJoinGate.IsGated(request.Sql);
        return Described(
            parameters.Value,
            [.. columns.Select((column, index) => PostgresColumns.Build(column, origins[index], rows.Value, gated))],
            gated
        );
```

and `Described` becomes:

```csharp
    // A statement without rows has its parameters and nothing else: no columns, no table and no provenance.  One
    // with rows says whether it needed a plan: "unavailable" for a gated query, until the walk exists.
    private static Outcome<QueryDescription, DescribeFailure> Described(
        IReadOnlyList<DescribedParameter> parameters,
        IReadOnlyList<SidecarColumn> columns,
        bool gated
    )
    {
        var typed = new EquatableArray<DescribedParameter>([.. parameters]);
        return Outcome<QueryDescription, DescribeFailure>.Of(
            columns.Count == 0
                ? new QueryDescription(SidecarResultKind.None, typed, null, null, null, null)
                : new QueryDescription(
                    SidecarResultKind.Rows,
                    typed,
                    new EquatableArray<SidecarColumn>([.. columns]),
                    null,
                    gated ? SidecarValues.Plan.Unavailable : SidecarValues.Plan.NotNeeded,
                    SidecarValues.TableMatch.NoOrigin
                )
        );
    }
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS.

- [ ] **Step 7: Write the container tests**

Add to `tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs`:

```csharp
    private static (bool? Nullable, string? Source)[] NullabilityOf(IEnumerable<SidecarColumn> columns) =>
        [.. columns.Select(static column => (column.Nullable, column.NullableSource))];

    // The query of the spec's Goal, without its join.
    [Fact]
    public async Task Describe_QueryWithoutAGateWord_TakesNullabilityFromTheCatalog()
    {
        var description = await ShouldDescribeAsync(
            "SELECT u.id, u.email, count(*) OVER () AS n FROM users u WHERE u.id = @id"
        );

        description.Plan.ShouldBe(SidecarValues.Plan.NotNeeded);
        NullabilityOf(description.Columns.ShouldNotBeNull())
            .ShouldBe([(false, "catalog"), (true, "catalog"), (null, "no-origin")]);
    }

    // The query of the spec's Goal: each row of its table.
    [Fact]
    public async Task Describe_LeftJoin_IsGatedAndEveryOriginColumnIsNullableByTheHeuristic()
    {
        var description = await ShouldDescribeAsync(
            "SELECT u.id, u.email, o.total, count(*) OVER () AS n "
                + "FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = @id"
        );

        var columns = description.Columns.ShouldNotBeNull();
        description.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NoOrigin);
        TypeNamesOf(columns).ShouldBe(["integer", "public.email_t", "numeric(18,2)", "bigint"]);
        NullabilityOf(columns)
            .ShouldBe([(true, "heuristic"), (true, "heuristic"), (true, "heuristic"), (null, "no-origin")]);
        columns
            .Select(column => column.Origin)
            .ShouldBe([Origin("users", "id"), Origin("users", "email"), Origin("orders", "total"), null]);
    }

    // A subtotal row holds NULL in a NOT NULL grouping column, with no outer join in sight, and the column keeps
    // its origin.
    [Theory]
    [InlineData("SELECT status, count(*) FROM users GROUP BY ROLLUP (status)")]
    [InlineData("SELECT status, count(*) FROM users GROUP BY CUBE (status)")]
    [InlineData("SELECT status, count(*) FROM users GROUP BY GROUPING SETS ((status), ())")]
    public async Task Describe_GroupingSetsOverANotNullColumn_IsGated(string sql)
    {
        var description = await ShouldDescribeAsync(sql);

        description.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        var status = description.Columns.ShouldNotBeNull()[0];
        status.Origin.ShouldBe(Origin("users", "status"));
        (status.Nullable, status.NullableSource).ShouldBe((true, "heuristic"));
    }

    // The gate reads words: a function named as one gates a query that has no join.
    [Fact]
    public async Task Describe_FunctionNamedAsAGateWord_IsGated()
    {
        var description = await ShouldDescribeAsync("SELECT id, left(name, 3) AS l FROM users");

        description.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        NullabilityOf(description.Columns.ShouldNotBeNull()).ShouldBe([(true, "heuristic"), (null, "no-origin")]);
    }

    [Fact]
    public async Task Describe_GateWordInAStringOrAComment_IsNotGated()
    {
        var description = await ShouldDescribeAsync("SELECT id, 'left join' AS s FROM users -- outer");

        description.Plan.ShouldBe(SidecarValues.Plan.NotNeeded);
        NullabilityOf(description.Columns.ShouldNotBeNull()).ShouldBe([(false, "catalog"), (null, "no-origin")]);
    }

    // PostgreSQL records no NOT NULL for a column of a view, plain or materialized.  A view's column says "view"
    // in a gated query too.
    [Fact]
    public async Task Describe_ColumnsOfAViewAndOfAMaterializedView_SayView()
    {
        await using (var source = NpgsqlDataSource.Create(postgres.ConnectionString))
        await using (
            var create = source.CreateCommand(
                "CREATE MATERIALIZED VIEW IF NOT EXISTS column_users AS SELECT id, name FROM users"
            )
        )
        {
            _ = await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var view = await ColumnsOfAsync("SELECT id, name, email, status FROM active_users");
        var materialized = await ColumnsOfAsync("SELECT id FROM column_users");
        var gated = await ColumnsOfAsync(
            "SELECT v.id, o.total FROM active_users v LEFT JOIN orders o ON o.user_id = v.id"
        );

        NullabilityOf(view).ShouldAllBe(found => found == ((bool?)null, "view"));
        NullabilityOf(materialized).ShouldBe([(null, "view")]);
        NullabilityOf(gated).ShouldBe([(null, "view"), (true, "heuristic")]);
    }
```

Run: `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.ColumnTests"`
Expected: PASS.

- [ ] **Step 8: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: every step `passed`.  The fixtures do not change: a statement without rows has no plan.

```bash
git add src/SqlSource.Tool tests/SqlSource.Tool.Tests tests/SqlSource.Tests.Postgres
```

```bash
git commit -m "Answer a column's nullability from the catalog, behind the outer-join gate

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 6: `SQLSRC229`, a column of a pseudo-type

**Files:**
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`
- Modify: `src/SqlSource/AnalyzerReleases.Unshipped.md`
- Modify: `docs/diagnostics.md`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`
- Modify: `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/PostgresRows.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs`

**Interfaces:**
- Consumes: `PostgresTypes.IsPseudo`, `PostgresColumns.Build` in its final shape, `Described(parameters, columns, gated)`.
- Produces: `ToolDiagnostics.ColumnTypeNotSupported`; `PostgresErrors.ColumnTypeHelp` and `PostgresErrors.ColumnPseudoType(DescribeRequest query, string column, string typeName)`; `PostgresSession.BuildColumns(DescribeRequest, IReadOnlyList<PostgresColumnRow>, IReadOnlyList<ColumnOrigin?>, IReadOnlyDictionary<long, PostgresTypeRow>, bool)` returning `Outcome<IReadOnlyList<SidecarColumn>, DescribeFailure>`, static until Task 8 gives it the log.

- [ ] **Step 1: Write the failing tests**

In `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`, beside the test of `QueryHasSeveralStatements`:

```csharp
    [Fact]
    public void ColumnTypeNotSupported_Message_HoldsTheColumnTheQueryAndTheType() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.ColumnTypeNotSupported.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "row",
                "ListPairs",
                "record"
            )
            .ShouldBe("The column 'row' of the query 'ListPairs' has the type 'record', which cannot be described");
```

Add to `tests/SqlSource.Tool.Tests/PostgresRows.cs`, after `Record`:

```csharp
    public static PostgresTypeRow RecordArray { get; } = ArrayOf(2287, "_record", Record);
```

Add to `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`:

```csharp
    // No value has a pseudo-type, so the query is not described.  The script answers no call after "types".
    [Theory]
    [InlineData(false, "record")]
    [InlineData(true, "record[]")]
    public async Task DescribeAsync_ColumnOfAPseudoType_IsSqlsrc229NamingTheColumnAndTheType(bool array, string type)
    {
        var pseudo = array ? PostgresRows.RecordArray : PostgresRows.Record;
        var script = new PostgresScript().Open();
        _ = PreparesOnly(script, 1, "SELECT 1 AS one, ROW(1, 2) AS pair, ROW(3) AS other", [], new([], null));
        _ = Columns(
            script,
            1,
            0,
            Expression(0, "one", 23),
            Expression(1, "pair", pseudo.Oid),
            Expression(2, "other", pseudo.Oid)
        );

        // The request names the types of the columns; the closure the server answers holds an array's element too.
        PostgresTypeRow[] closure = array
            ? [PostgresRows.Int4, PostgresRows.Record, pseudo]
            : [PostgresRows.Int4, pseudo];
        _ = Resolves(script, [23, pseudo.Oid], closure);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT 1 AS one, ROW(1, 2) AS pair, ROW(3) AS other"),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC229");
        failure.Arguments.ShouldBe(["pair", "Q", type]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.ColumnTypeHelp);
        exchange.Unused.ShouldBeEmpty();
    }

    // The parameters are typed before the columns are built: a pseudo-type that is a parameter's and so a
    // column's is the parameter's failure, whose help says what to declare.
    [Fact]
    public async Task DescribeAsync_PseudoTypeOfAParameterAndOfAColumn_IsTheParametersFailure()
    {
        var script = new PostgresScript().Open();
        _ = PreparesOnly(script, 1, "SELECT $1", ["record"], new([2249], null));
        _ = Columns(script, 1, 1, Expression(0, "?column?", 2249));
        _ = Resolves(script, [2249], PostgresRows.Record);
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT @p", Declared("p", "record")),
            TestContext.Current.CancellationToken
        );

        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC228");
        exchange.Unused.ShouldBeEmpty();
    }
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresSessionTests"`
Expected: the build fails: `PostgresErrors.ColumnTypeHelp` does not exist.

- [ ] **Step 2: Add the descriptor, in its four places**

In `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, between `DeclaredTypeNotValid` and `QueryHasSeveralStatements`:

```csharp
    public static readonly DiagnosticDescriptor ColumnTypeNotSupported = new(
        id: "SQLSRC229",
        title: "Column type is not supported",
        messageFormat: "The column '{0}' of the query '{1}' has the type '{2}', which cannot be described",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc229",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

and in `All`, `ColumnTypeNotSupported,` between `DeclaredTypeNotValid,` and `QueryHasSeveralStatements`.

In `src/SqlSource/AnalyzerReleases.Unshipped.md`, between the rows of `SQLSRC228` and `SQLSRC230`:

```markdown
SQLSRC229 | SqlSource | Error | Column type is not supported
```

In `docs/diagnostics.md`, in the table, between the rows of `SQLSRC228` and `SQLSRC230`:

```markdown
| [SQLSRC229](#sqlsrc229) | Column type is not supported |
```

and between the sections `## SQLSRC228` and `## SQLSRC230`:

````markdown
## SQLSRC229

**Column type is not supported**

A column of the query has a pseudo-type, a type that the server knows by name and that no value read from a column can be given.  `record` is the one a query meets: a row that is built in the select list and not taken apart again, `ROW(a, b)`, or the alias of a subquery selected whole.  An array of one, `record[]`, is another.  The tool cannot say what such a column holds, so the query is not described.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC229: The column 'row' of the query 'ListPairs' has the type 'record', which cannot be described
    query: ListPairs, database app, postgres 18.4, Npgsql 10.0.3
    step: resolve type
    help: cast the column to a concrete type, or give the expression an alias and a cast
```

Select the fields of the row as columns of their own, or cast the expression to a type the database has: `ROW(a, b)::pair`, with `pair` a composite type, or `to_jsonb(s)` for a whole row.  A parameter of a pseudo-type that the query returns is reported for the parameter, as [SQLSRC226](#sqlsrc226) or [SQLSRC228](#sqlsrc228).
````

- [ ] **Step 3: Report the column**

In `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`, beside the other help texts:

```csharp
    public const string ColumnTypeHelp =
        "cast the column to a concrete type, or give the expression an alias and a cast";
```

and after `PseudoType`:

```csharp
    /// <summary>
    /// <c>SQLSRC229</c>: a column whose type is a pseudo-type.  The server describes such a column and says nothing,
    /// so there is no server line.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="column">The column's name, as the server returned it.</param>
    /// <param name="typeName">The pseudo-type, as <c>format_type</c> spells it.</param>
    public static DescribeFailure ColumnPseudoType(DescribeRequest query, string column, string typeName) =>
        new(
            ToolDiagnostics.ColumnTypeNotSupported,
            new EquatableArray<string>([column, query.Name, typeName]),
            DescribeStep.ResolveType,
            EquatableArray<string>.Empty,
            ColumnTypeHelp
        );
```

In `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`, the end of `DescribePreparedAsync`, from `// A statement without rows has nothing a join could null-extend.`, becomes:

```csharp
        // A statement without rows has nothing a join could null-extend.
        var gated = columns.Count > 0 && OuterJoinGate.IsGated(request.Sql);
        var built = BuildColumns(request, columns, origins, rows.Value, gated);
        return built.Succeeded ? Described(parameters.Value, built.Value, gated) : Failed(built.Failure);
```

and after `TypeParameters`:

```csharp
    // The columns in order.  The first of a pseudo-type ends the describe: no value has such a type.  A domain is
    // never over one, so the type the protocol reported is the one to ask.
    private static Outcome<IReadOnlyList<SidecarColumn>, DescribeFailure> BuildColumns(
        DescribeRequest request,
        IReadOnlyList<PostgresColumnRow> columns,
        IReadOnlyList<ColumnOrigin?> origins,
        IReadOnlyDictionary<long, PostgresTypeRow> rows,
        bool gated
    )
    {
        var built = new List<SidecarColumn>(columns.Count);
        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            if (PostgresTypes.IsPseudo(column.TypeOid, rows))
            {
                return Outcome<IReadOnlyList<SidecarColumn>, DescribeFailure>.Failed(
                    PostgresErrors.ColumnPseudoType(request, column.Name, rows[column.TypeOid].FormatType)
                );
            }

            built.Add(PostgresColumns.Build(column, origins[index], rows, gated));
        }

        return Outcome<IReadOnlyList<SidecarColumn>, DescribeFailure>.Of(built);
    }
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests` and `dotnet test --project tests/SqlSource.Tests`
Expected: PASS, with the tests of `DiagnosticsDocumentTests` among them: the document's sections and the descriptors agree.

- [ ] **Step 4: Write the container test**

Add to `tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs`:

```csharp
    // A row built in the select list, and the alias of a subquery selected whole, are "record".
    [Theory]
    [InlineData("SELECT id, ROW(1, 2) FROM users", "row")]
    [InlineData("SELECT s FROM (SELECT 1 AS a, 2 AS b) AS s", "s")]
    public async Task Describe_ColumnOfAPseudoType_IsSqlsrc229NamingItAndItsType(string sql, string column)
    {
        var described = await Sessions.DescribeAsync(postgres.ConnectionString, Requests.Of(sql));

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC229");
        failure.Arguments.ShouldBe([column, Requests.Query, "record"]);
        failure.Step.ShouldBe(DescribeStep.ResolveType);
        failure.ServerLines.ShouldBeEmpty();
    }
```

Run: `dotnet test --project tests/SqlSource.Tests.Postgres`
Expected: PASS.  `ParameterTests.Describe_ParameterOfAPseudoType_IsAFailureAndNoThrow` passes as before: `SELECT @p` with a declared `record` is the parameter's `SQLSRC228`.

- [ ] **Step 5: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: every step `passed`.

```bash
git add src/SqlSource src/SqlSource.Tool docs/diagnostics.md tests/SqlSource.Tests tests/SqlSource.Tool.Tests tests/SqlSource.Tests.Postgres
```

```bash
git commit -m "Report a column of a pseudo-type as SQLSRC229

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: `check`, `SQLSRC231` and `SQLSRC232`

The describer asks Npgsql to describe the original SQL as a consumer's code will send it, `@name` as written and each parameter bound by the name of its type, and compares the result with what `columns` answered.

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/ParameterPlaceholders.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs`
- Modify: `.editorconfig`
- Create: `src/SqlSource.Tool/Describing/Postgres/NpgsqlCheck.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`
- Modify: `src/SqlSource/Diagnostics/ToolDiagnostics.cs`
- Modify: `src/SqlSource/AnalyzerReleases.Unshipped.md`
- Modify: `docs/diagnostics.md`
- Modify: `src/SqlSource.Tool/AGENTS.md`
- Create: `tests/SqlSource.Tool.Tests/Describing/Postgres/CheckVerdictTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/ParameterPlaceholdersTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs`
- Modify: `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs`
- Modify: the `capture.json` of the four cases
- Modify: `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`
- Create: `tests/SqlSource.Tests.Postgres/Describer/NpgsqlCheckTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs`

**Interfaces:**
- Consumes: `DescribePreparedAsync` with `parameters`, `columns`, `rows` and `built` in scope; `PostgresErrors.ServerLines`, `PostgresErrors.Written`; `PostgresLive`.
- Produces:
  - `PostgresCheckRequest(string Sql, IReadOnlyList<PostgresCheckParameter> Parameters)`, `PostgresCheckParameter(string Name, string DataTypeName)`, `PostgresCheckAnswer(IReadOnlyList<PostgresCheckColumn>? Columns, PostgresServerError? Error, PostgresBindError? DriverError)`, `PostgresCheckColumn(string Name, long TypeOid, string DataTypeName)`, `PostgresBindError(string Parameter, string Message)`.
  - `ParameterPlaceholders.AsWritten(string sql)` returning `string?`.
  - `PostgresConnection.BatchOfOneAsync(string sql, CancellationToken)` returning `Task<NpgsqlBatch>`.
  - `NpgsqlCheck.Same`, `NpgsqlCheck.Differ`, `NpgsqlCheck.Unbound`, and `NpgsqlCheck.Read(PostgresCheckAnswer answer, IReadOnlyList<PostgresColumnRow> described, IReadOnlyDictionary<long, PostgresTypeRow> rows)` returning `CheckVerdict(string Result, string? Differs)`.
  - `PostgresLive.CheckAsync(PostgresCheckRequest, CancellationToken)`.
  - `PostgresErrors.CheckHelp`, `BindHelp`, `BindQuotedHelp`, and `PostgresErrors.Check(DescribeRequest query, PostgresCheckRequest asked, PostgresCheckAnswer answer, CheckVerdict verdict)` returning `DescribeFailure?`.
  - `ToolDiagnostics.ParametersReadDifferently` and `ToolDiagnostics.TypeCannotBeBound`.
  - In the tests: `PostgresSessionTests.Checks`.

- [ ] **Step 1: Write the failing tests of the SQL as written**

Add to `tests/SqlSource.Tool.Tests/Describing/Postgres/ParameterPlaceholdersTests.cs`:

```csharp
    // The SQL of the check: cut where Rewrite cuts it, with each parameter as the user wrote it.
    [Theory]
    [InlineData("SELECT @id, @Name", "SELECT @id, @Name")]
    [InlineData("SELECT @id;", "SELECT @id")]
    [InlineData("SELECT @id ; -- the end\n/* and more */", "SELECT @id ")]
    [InlineData("SELECT ';' || @a", "SELECT ';' || @a")]
    [InlineData("SELECT $$ @x; $$, @a;", "SELECT $$ @x; $$, @a")]
    [InlineData("DELETE FROM samples", "DELETE FROM samples")]
    public void AsWritten_OneStatement_IsCutAtItsTrailingSemicolonWithItsParametersAsWritten(
        string sql,
        string expected
    ) => ParameterPlaceholders.AsWritten(sql).ShouldBe(expected);

    [Theory]
    [InlineData("SELECT 1; SELECT 2")]
    [InlineData("SELECT @a;;")]
    public void AsWritten_SecondStatement_IsNull(string sql) => ParameterPlaceholders.AsWritten(sql).ShouldBeNull();
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.ParameterPlaceholdersTests"`
Expected: the build fails: `AsWritten` does not exist.

- [ ] **Step 2: Write `AsWritten`**

In `src/SqlSource.Tool/Describing/Postgres/ParameterPlaceholders.cs`, the body of `Rewrite` moves into a private `Statement` that takes the list or nothing, and both public methods call it.  `Rewrite` keeps its documentation.

```csharp
    public static string? Rewrite(string sql, EquatableArray<SqlQueryParameter> parameters) =>
        Statement(sql, parameters);

    /// <summary>
    /// The SQL with each parameter as it was written, and without its trailing <c>;</c> and what follows it: what
    /// Npgsql is given to read in the check.  Its parser may refuse any text after a <c>;</c>, a comment included.
    /// Null when the query holds more than one statement, as for <see cref="Rewrite" />.
    /// </summary>
    /// <exception cref="InvalidOperationException">The SQL does not lex.</exception>
    public static string? AsWritten(string sql) => Statement(sql, null);

    // The one statement of the SQL, with "$n" for each parameter when it is given the list.
    private static string? Statement(string sql, EquatableArray<SqlQueryParameter>? parameters)
    {
        var lexed = SqlLexer.Lex(sql, SqlDialectRules.PostgreSql);
        if (lexed.Error is not null)
        {
            throw new InvalidOperationException(
                "The sample SQL of a query does not lex, and the plan's parser read it."
            );
        }

        var rewritten = new StringBuilder(sql.Length);
        for (var index = 0; index < lexed.Lexemes.Count; index++)
        {
            var lexeme = lexed.Lexemes[index];
            var text = sql.AsSpan(lexeme.Span.Start, lexeme.Span.Length);
            if (lexeme.Kind == SqlLexemeKind.Parameter && parameters is { } list)
            {
                _ = rewritten.Append('$').Append(OrdinalOf(text[1..], list));
                continue;
            }

            var end = lexeme.Kind == SqlLexemeKind.Text ? text.IndexOf(';') : -1;
            if (end < 0)
            {
                _ = rewritten.Append(text);
                continue;
            }

            // The first ";" of the SQL.  It is the trailing one when nothing but white space and comments follows.
            return text[(end + 1)..].IsWhiteSpace() && OnlyCommentsFollow(sql, lexed.Lexemes, index + 1)
                ? rewritten.Append(text[..end]).ToString()
                : null;
        }

        return rewritten.ToString();
    }
```

Run: the test class of Step 1.
Expected: PASS, the tests of `Rewrite` included.

- [ ] **Step 3: Write the failing tests of the comparison**

Create `tests/SqlSource.Tool.Tests/Describing/Postgres/CheckVerdictTests.cs`:

```csharp
using System;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What the answer of "check" says against the answer of "columns": the two describes saw one query when the
// columns are equal in count, names and types.
public class CheckVerdictTests
{
    private static readonly PostgresColumnRow[] Described =
    [
        new(0, "id", 23, 16396, 1, null, null, null),
        new(1, "name", 1043, 16396, 2, 100, null, null),
    ];

    private static CheckVerdict Read(PostgresCheckAnswer answer, params PostgresColumnRow[] described) =>
        NpgsqlCheck.Read(answer, described, PostgresRows.Of(PostgresRows.Int4, PostgresRows.Varchar));

    private static PostgresCheckAnswer Columns(params PostgresCheckColumn[] columns) => new(columns, null, null);

    [Fact]
    public void Read_ColumnsEqualInCountNamesAndTypes_IsSame() =>
        Read(
                Columns(new("id", 23, "integer"), new("name", 1043, "character varying")),
                Described
            )
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Same, null));

    // A statement without rows: both describes found none.
    [Fact]
    public void Read_NoColumnsOnEitherSide_IsSame() =>
        Read(Columns()).ShouldBe(new CheckVerdict(NpgsqlCheck.Same, null));

    [Fact]
    public void Read_AnotherNumberOfColumns_DiffersWithBothCounts() =>
        Read(Columns(new("id", 23, "integer")), Described)
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Differ, "the result has 1 columns for Npgsql and 2 for SqlSource"));

    // Names are compared as they are: the server folded both.
    [Fact]
    public void Read_ColumnWithAnotherName_DiffersNamingBoth() =>
        Read(Columns(new("id", 23, "integer"), new("Name", 1043, "character varying")), Described)
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Differ, "column 2 is named 'Name' for Npgsql and 'name' for SqlSource"));

    [Fact]
    public void Read_ColumnWithAnotherType_DiffersNamingBothTypes() =>
        Read(Columns(new("id", 25, "text"), new("name", 1043, "character varying")), Described)
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Differ, "column 1 is text for Npgsql and integer for SqlSource"));

    // The first difference is the one reported.
    [Fact]
    public void Read_SeveralDifferences_ReportsTheFirst() =>
        Read(Columns(new("x", 25, "text"), new("y", 25, "text")), Described)
            .Differs.ShouldBe("column 1 is named 'x' for Npgsql and 'id' for SqlSource");

    [Fact]
    public void Read_Error_DiffersWithItsMessage() =>
        Read(
                new PostgresCheckAnswer(
                    null,
                    new PostgresServerError("42601", "syntax error at or near \"@\"", null, null, 8),
                    null
                ),
                Described
            )
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Differ, "syntax error at or near \"@\""));

    [Fact]
    public void Read_DriverError_IsUnboundWithItsMessage() =>
        Read(new PostgresCheckAnswer(null, null, new PostgresBindError("p", "no such type")), Described)
            .ShouldBe(new CheckVerdict(NpgsqlCheck.Unbound, "no such type"));

    [Fact]
    public void Read_AnswerWithNothing_Throws() =>
        Should.Throw<InvalidOperationException>(() => Read(new PostgresCheckAnswer(null, null, null), Described));
}
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.CheckVerdictTests"`
Expected: the build fails: the records of `check` and `NpgsqlCheck` do not exist.

- [ ] **Step 4: Add the records of `check`, and `NpgsqlCheck`**

Append to `src/SqlSource.Tool/Describing/Postgres/PostgresCalls.cs`:

```csharp
/// <summary>Everything that decides the answer of <c>check</c>.</summary>
/// <param name="Sql">
/// The query's one statement as it was written, <c>@name</c> for each parameter, without its trailing <c>;</c>.
/// </param>
/// <param name="Parameters">The parameters of the query's list, in order.</param>
internal sealed record PostgresCheckRequest(string Sql, IReadOnlyList<PostgresCheckParameter> Parameters);

/// <summary>A parameter as generated code will bind it.</summary>
/// <param name="Name">The name, as the query's list has it.</param>
/// <param name="DataTypeName">The <c>name</c> of the type the describer gave the parameter.</param>
internal sealed record PostgresCheckParameter(string Name, string DataTypeName);

/// <summary>What Npgsql's own describe of the SQL gave.  One of the three members is not null.</summary>
/// <param name="Columns">The columns of the result, in order.</param>
/// <param name="Error">
/// What the server said, or what Npgsql said of a text it would not send: it has no SQLSTATE then.
/// </param>
/// <param name="DriverError">The parameter whose type Npgsql cannot bind.</param>
internal sealed record PostgresCheckAnswer(
    IReadOnlyList<PostgresCheckColumn>? Columns,
    PostgresServerError? Error,
    PostgresBindError? DriverError
);

/// <summary>A column as Npgsql's describe of the original SQL reports it.</summary>
/// <param name="Name">The name.</param>
/// <param name="TypeOid">The type.</param>
/// <param name="DataTypeName">Npgsql's own name for the type.</param>
internal sealed record PostgresCheckColumn(string Name, long TypeOid, string DataTypeName);

/// <summary>A type name that Npgsql refused before it sent anything.</summary>
/// <param name="Parameter">The first parameter of the list whose type is refused.</param>
/// <param name="Message">Npgsql's own message.</param>
internal sealed record PostgresBindError(string Parameter, string Message);
```

Create `src/SqlSource.Tool/Describing/Postgres/NpgsqlCheck.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>What <c>check</c> found.</summary>
/// <param name="Result">
/// <see cref="NpgsqlCheck.Same" />, <see cref="NpgsqlCheck.Differ" /> or <see cref="NpgsqlCheck.Unbound" />.
/// </param>
/// <param name="Differs">What differs, or Npgsql's message of a type it cannot bind.  Null when nothing does.</param>
internal sealed record CheckVerdict(string Result, string? Differs);

/// <summary>
/// The check of the tool's lexer against Npgsql.  The describer prepares a query with <c>$n</c> where its lexer
/// found a parameter; generated code will send the SQL as written and let Npgsql find them.  Both describes give
/// columns, and the two saw one query when the columns are equal in count, in names and in types.
/// </summary>
/// <remarks>
/// The check is best effort.  A disagreement that leaves the columns alone, in a predicate for one, passes, and so
/// does a <c>$n</c> that the SQL writes itself.  <c>docs/tech-debt</c> records what it does not see.
/// </remarks>
internal static class NpgsqlCheck
{
    /// <summary>The values of the <c>result</c> member of the <c>check</c> event.</summary>
    public const string Same = "same";
    public const string Differ = "differ";
    public const string Unbound = "unbound";

    /// <summary>Reads the answer of <c>check</c> against the answer of <c>columns</c>.</summary>
    /// <param name="answer">What Npgsql's describe of the original SQL gave.</param>
    /// <param name="described">The columns of the prepared statement.</param>
    /// <param name="rows">The rows of the closure, by OID, for the name of a type that differs.</param>
    /// <exception cref="InvalidOperationException">The answer holds nothing.</exception>
    public static CheckVerdict Read(
        PostgresCheckAnswer answer,
        IReadOnlyList<PostgresColumnRow> described,
        IReadOnlyDictionary<long, PostgresTypeRow> rows
    )
    {
        if (answer.DriverError is { } unbound)
        {
            return new CheckVerdict(Unbound, unbound.Message);
        }

        if (answer.Error is { } error)
        {
            return new CheckVerdict(Differ, error.Message);
        }

        var seen =
            answer.Columns
            ?? throw new InvalidOperationException("The answer to 'check' holds no columns and no error.");
        return Difference(seen, described, rows) is { } differs
            ? new CheckVerdict(Differ, differs)
            : new CheckVerdict(Same, null);
    }

    // The first thing that differs.  A type is compared as the protocol reported it, which both describes see: for
    // a column of a domain that is the base.
    private static string? Difference(
        IReadOnlyList<PostgresCheckColumn> seen,
        IReadOnlyList<PostgresColumnRow> described,
        IReadOnlyDictionary<long, PostgresTypeRow> rows
    )
    {
        if (seen.Count != described.Count)
        {
            return Invariant($"the result has {seen.Count} columns for Npgsql and {described.Count} for SqlSource");
        }

        for (var index = 0; index < seen.Count; index++)
        {
            var (theirs, ours) = (seen[index], described[index]);
            if (!string.Equals(theirs.Name, ours.Name, StringComparison.Ordinal))
            {
                return Invariant(
                    $"column {index + 1} is named '{theirs.Name}' for Npgsql and '{ours.Name}' for SqlSource"
                );
            }

            if (theirs.TypeOid != ours.TypeOid)
            {
                return Invariant(
                    $"column {index + 1} is {theirs.DataTypeName} for Npgsql and {NameOf(ours.TypeOid, rows)} for SqlSource"
                );
            }
        }

        return null;
    }

    private static string NameOf(long oid, IReadOnlyDictionary<long, PostgresTypeRow> rows) =>
        rows.TryGetValue(oid, out var row) ? row.FormatType : oid.ToString(CultureInfo.InvariantCulture);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
```

Run: the test class of Step 3.
Expected: PASS.

- [ ] **Step 5: Add the two descriptors, in their four places**

In `tests/SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`:

```csharp
    [Fact]
    public void ParametersReadDifferently_Message_HoldsTheQueryAndWhatDiffers() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.ParametersReadDifferently.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "ListUsers",
                "column 1 is text for Npgsql and integer for SqlSource"
            )
            .ShouldBe(
                "Npgsql reads the parameters of the query 'ListUsers' differently from SqlSource: "
                    + "column 1 is text for Npgsql and integer for SqlSource"
            );

    [Fact]
    public void TypeCannotBeBound_Message_HoldsTheTypeTheParameterAndTheQuery() =>
        string.Format(
                CultureInfo.InvariantCulture,
                ToolDiagnostics.TypeCannotBeBound.MessageFormat.ToString(CultureInfo.InvariantCulture),
                "public.d2",
                "@p",
                "ListUsers"
            )
            .ShouldBe("Npgsql cannot bind the type 'public.d2' of the parameter '@p' of the query 'ListUsers'");
```

In `src/SqlSource/Diagnostics/ToolDiagnostics.cs`, after `QueryHasSeveralStatements`:

```csharp
    public static readonly DiagnosticDescriptor ParametersReadDifferently = new(
        id: "SQLSRC231",
        title: "Parameters as Npgsql reads them differ",
        messageFormat: "Npgsql reads the parameters of the query '{0}' differently from SqlSource: {1}",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc231",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    public static readonly DiagnosticDescriptor TypeCannotBeBound = new(
        id: "SQLSRC232",
        title: "Type cannot be bound by Npgsql",
        messageFormat: "Npgsql cannot bind the type '{0}' of the parameter '{1}' of the query '{2}'",
        category: SqlDiagnostics.Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: SqlDiagnostics.HelpLinkBase + "sqlsrc232",
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );
```

and in `All`, after `QueryHasSeveralStatements`: `ParametersReadDifferently, TypeCannotBeBound`.

In `src/SqlSource/AnalyzerReleases.Unshipped.md`, after the row of `SQLSRC230`:

```markdown
SQLSRC231 | SqlSource | Error | Parameters as Npgsql reads them differ
SQLSRC232 | SqlSource | Error | Type cannot be bound by Npgsql
```

In `docs/diagnostics.md`, in the table, after the row of `SQLSRC230`:

```markdown
| [SQLSRC231](#sqlsrc231) | Parameters as Npgsql reads them differ |
| [SQLSRC232](#sqlsrc232) | Type cannot be bound by Npgsql |
```

and after the section `## SQLSRC230`:

````markdown
## SQLSRC231

**Parameters as Npgsql reads them differ**

SqlSource finds the parameters of a query with its own reading of the SQL, and describes the query with `$1`, `$2` in their places.  The code it generates sends the SQL as you wrote it, and Npgsql finds the `@name` parameters itself.  The tool asks Npgsql to describe the SQL that way too, with each parameter bound by its type's name, and compares the columns of the two results.  They are not the same, so what was described is not what would run.  The message ends with what differs: the number of columns, the name or the type of one, or what the server said of the SQL as Npgsql sent it.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC231: Npgsql reads the parameters of the query 'ListUsers' differently from SqlSource: syntax error at or near "@"
    query: ListUsers, database app, postgres 18.4, Npgsql 10.0.3
    step: check parameters
    server: 42601: syntax error at or near "@"
    help: a @ that one of the two reads as a parameter and the other as text; the server's lines say where
```

Look for a `@` that stands where only one of the two would read a parameter: inside an unusual string or an identifier, or directly after another character of a name.  Put a space before the parameter, or write the text so that the `@` is plainly inside a string.  One difference has no `@` in it: a parameter that is an array of a domain, `-- param: @p email_t[]`, and is returned as a column.  Npgsql sends it as an array of the domain's base type, so the column is `character varying[]` for Npgsql and `email_t[]` for SqlSource; cast the column to the type you want, `@p::varchar[]`.  If the SQL is plain and the two still differ, report it.

## SQLSRC232

**Type cannot be bound by Npgsql**

The code SqlSource generates binds a parameter by the name of its type, as the sidecar records it.  Npgsql refused that name before it sent anything, so a query with this parameter could not be run.  The `server:` line is Npgsql's own message.  Three kinds of type are known to be refused: a type whose name or whose schema's name needs quotes, `"My Schema"."Status"`; `"char"`, the one-byte type of the system catalogs, which a comparison with a column such as `pg_class.relkind` gives a parameter; and a domain over another domain.

```console
/work/App/Queries/Users.sql(1,10): error SQLSRC232: Npgsql cannot bind the type '"My Schema"."Status"' of the parameter '@status' of the query 'ListUsers'
    query: ListUsers, database app, postgres 18.4, Npgsql 10.0.3
    step: check parameters
    server: The data type name '"My Schema"."Status"' could not be found in the types that were loaded by Npgsql. Your database details or Npgsql type loading configuration may be incorrect. Alternatively your PostgreSQL installation might need to be upgraded, or an extension adding the missing data type might not have been installed.
    help: Npgsql takes no type name that is written with quotes; declare the parameter as text with -- param: and cast it to the type where it is used
```

Declare the parameter as a type Npgsql binds and cast it where it is used: `-- param: @status text` with `@status::"My Schema"."Status"` in the SQL.  The error is also given for a parameter that only a `-- param:` marker declares: Npgsql resolves the type of every parameter it is handed.
````

Run: `dotnet test --project tests/SqlSource.Tests`
Expected: PASS.

- [ ] **Step 6: Write the failing tests of the failures**

Add to `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresErrorsTests.cs`:

```csharp
    private static readonly DescribeRequest Checked = new(
        "ListUsers",
        "SELECT id FROM users WHERE status = @status",
        new EquatableArray<SqlQueryParameter>([new SqlQueryParameter("status", null, null, false)])
    );

    private static PostgresCheckRequest AskedWith(string type) =>
        new("SELECT id FROM users WHERE status = @status", [new PostgresCheckParameter("status", type)]);

    [Fact]
    public void Check_VerdictSame_IsNoFailure() =>
        PostgresErrors
            .Check(
                Checked,
                AskedWith("text"),
                new PostgresCheckAnswer([], null, null),
                new CheckVerdict(NpgsqlCheck.Same, null)
            )
            .ShouldBeNull();

    [Fact]
    public void Check_ColumnsThatDiffer_IsSqlsrc231WithWhatDiffersAndNoServerLine()
    {
        var failure = PostgresErrors
            .Check(
                Checked,
                AskedWith("text"),
                new PostgresCheckAnswer([], null, null),
                new CheckVerdict(NpgsqlCheck.Differ, "the result has 0 columns for Npgsql and 1 for SqlSource")
            )
            .ShouldNotBeNull();

        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe(["ListUsers", "the result has 0 columns for Npgsql and 1 for SqlSource"]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
        failure.ServerLines.ShouldBeEmpty();
        failure.Help.ShouldBe(PostgresErrors.CheckHelp);
    }

    // The position is in Npgsql's own rewrite of the SQL, which the user never sees, so it has no line.
    [Fact]
    public void Check_ErrorOfTheServer_IsSqlsrc231WithItsLinesAndNoPosition()
    {
        var error = new PostgresServerError("42601", "syntax error at or near \"@\"", null, "a hint", 31);

        var failure = PostgresErrors
            .Check(
                Checked,
                AskedWith("text"),
                new PostgresCheckAnswer(null, error, null),
                new CheckVerdict(NpgsqlCheck.Differ, error.Message)
            )
            .ShouldNotBeNull();

        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe(["ListUsers", "syntax error at or near \"@\""]);
        failure.ServerLines.ShouldBe(["42601: syntax error at or near \"@\"", "hint: a hint"]);
    }

    [Theory]
    [InlineData("public.d2", false)]
    [InlineData("\"My Schema\".\"Status\"", true)]
    [InlineData("\"char\"", true)]
    public void Check_DriverError_IsSqlsrc232NamingTheTypeAndTheParameterAsWritten(string type, bool quoted)
    {
        var failure = PostgresErrors
            .Check(
                Checked,
                AskedWith(type),
                new PostgresCheckAnswer(null, null, new PostgresBindError("status", "Npgsql's own words")),
                new CheckVerdict(NpgsqlCheck.Unbound, "Npgsql's own words")
            )
            .ShouldNotBeNull();

        failure.Descriptor.Id.ShouldBe("SQLSRC232");
        failure.Arguments.ShouldBe([type, "@status", "ListUsers"]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
        failure.ServerLines.ShouldBe(["Npgsql's own words"]);
        failure.Help.ShouldBe(quoted ? PostgresErrors.BindQuotedHelp : PostgresErrors.BindHelp);
    }
```

Add `using SqlSource.Tool.Describing.Describers;` and `using SqlSource.Parsing;` if the file lacks them.

In `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs`, beside `Frees`:

```csharp
    // "check" answering the columns the test gives, each with the type "columns" answered: the two describes agree.
    private static PostgresScript Checks(
        PostgresScript script,
        string sql,
        (string Name, string Type)[] parameters,
        params PostgresColumnRow[] columns
    ) =>
        script.Answer(
            "check",
            new PostgresCheckRequest(
                sql,
                [.. parameters.Select(static parameter => new PostgresCheckParameter(parameter.Name, parameter.Type))]
            ),
            new PostgresCheckAnswer(
                [.. columns.Select(static column => new PostgresCheckColumn(column.Name, column.TypeOid, "a type"))],
                null,
                null
            )
        );
```

Every test of the class whose describe ends in a description now needs the answer of `check`.  Add one line to each, before its `Frees` line or, in `DescribeUsersIdAsync`, before `var exchange`.  The SQL is the request's, with its trailing `;` dropped and `@name` as written; the columns are the ones the test gave `Columns`:

| Test | Line to add |
|----|----|
| `DescribeAsync_Query_AsksPrepareThenDeallocateForTheStatement` | `_ = Checks(script, "DELETE FROM samples WHERE flag", []);` |
| `DescribeAsync_SecondQuery_TakesTheNextName` | `_ = Checks(script, "DELETE FROM samples", []);` and `_ = Checks(script, "DELETE FROM orders", []);` |
| `DescribeAsync_PrepareThatFails_IsTheFailureFreesNothingAndTheNextTakesTheNextName` | `_ = Checks(script, "DELETE FROM orders", []);` |
| `DescribeAsync_DeallocateThatFails_IsStillDescribed` | `_ = Checks(script, "DELETE FROM samples", []);` |
| `DescribeAsync_InferredAndDeclaredParameters_AreTypedInTheListsOrder` | `_ = Checks(script, "SELECT id FROM users WHERE id = @id AND name = @name", [("id", "integer"), ("name", "character varying")]);` |
| `DescribeAsync_ParametersOfOneTypeAndADeclaredOnlyOne_AskForEachOidOnce` | `_ = Checks(script, "UPDATE users SET name = @name WHERE id = @id", [("name", "character varying"), ("id", "integer"), ("tenant", "integer")]);` |
| `DescribeAsync_MoreTypesThanParameters_TypesTheListAlone` | `_ = Checks(script, "SELECT 1 FROM users WHERE id = @id AND name = $2", [("id", "integer")]);` |
| `DescribeAsync_StatementWithColumns_IsRowsWithEachColumnsTypeAndFacets` | `_ = Checks(script, "SELECT 1, 'a'::varchar(100), 2::numeric(18,2)", [], Expression(0, "?column?", 23), Expression(1, "varchar", 1043), Expression(2, "numeric", 1700));` |
| `DescribeAsync_StatementWithoutColumns_IsNoneWithNoProvenance` | `_ = Checks(script, "DELETE FROM samples WHERE id = @id", [("id", "integer")]);` |
| `DescribeAsync_ParametersAndColumns_AskTypesOnceForBoth` | `_ = Checks(script, "SELECT @p::text AS a, 1 AS b, 2 AS c", [("p", "text")], Expression(0, "a", 25), Expression(1, "b", 23), Expression(2, "c", 23));` |
| `DescribeAsync_ColumnsOfTables_AskTablesOnceAndTypesForTheOriginsToo` | `_ = Checks(script, "SELECT o.total, u.email, u.id FROM orders o, users u", [], Expression(0, "total", 1700), Expression(1, "email", 1043), Expression(2, "id", 23));` |
| `DescribeAsync_SystemColumn_HasNoOrigin` | `_ = Checks(script, "SELECT ctid FROM users", [], Expression(0, "ctid", 27));` |
| `DescribeUsersIdAsync` | `_ = Checks(script, sql, [], Expression(0, "id", 23), Expression(1, "n", 20));` |
| `DescribeAsync_GatedStatementWithoutRows_HasNoPlan` | `_ = Checks(script, "DELETE FROM users WHERE left(name, 1) = 'a'", []);` |

A test that ends in a failure before the check gets no line: its `exchange.Unused.ShouldBeEmpty()` holds that the check is not asked.

Add these tests at the end of the class:

```csharp
    // The second describe saw another query: a column fewer.  The statement is freed all the same.
    [Fact]
    public async Task DescribeAsync_CheckWhoseColumnsDiffer_IsSqlsrc231SayingWhatDiffers()
    {
        var script = new PostgresScript().Open();
        _ = PreparesOnly(script, 1, "SELECT $1::int AS a, 'x' AS b", ["unknown"], new([23], null));
        _ = Columns(script, 1, 1, Expression(0, "a", 23), Expression(1, "b", 25));
        _ = Resolves(script, [23, 25], PostgresRows.Int4, PostgresRows.Text);
        _ = Checks(script, "SELECT @p::int AS a, 'x' AS b", [("p", "integer")], Expression(0, "a", 23));
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("SELECT @p::int AS a, 'x' AS b;", Inferred("p")),
            TestContext.Current.CancellationToken
        );

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe(["Q", "the result has 1 columns for Npgsql and 2 for SqlSource"]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
        exchange.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task DescribeAsync_CheckThatIsRefused_IsSqlsrc231WithTheLinesOfTheAnswer()
    {
        var refused = new PostgresServerError(
            null,
            "Specifying multiple SQL statements in a single NpgsqlBatchCommand isn't supported, please remove all semicolons.",
            null,
            null,
            null
        );
        var script = new PostgresScript().Open();
        _ = Prepares(script, 1, "DELETE FROM samples WHERE id = $1", ["unknown"], new([23], null));
        _ = Resolves(script, [23], PostgresRows.Int4);
        _ = script.Answer(
            "check",
            new PostgresCheckRequest("DELETE FROM samples WHERE id = @id", [new("id", "integer")]),
            new PostgresCheckAnswer(null, refused, null)
        );
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("DELETE FROM samples WHERE id = @id", Inferred("id")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe(["Q", refused.Message]);
        failure.ServerLines.ShouldBe([refused.Message]);
        exchange.Unused.ShouldBeEmpty();
    }

    // The parameter is the second of the list, and the type is the name the describer spelled for it.
    [Fact]
    public async Task DescribeAsync_CheckWithAParameterNpgsqlCannotBind_IsSqlsrc232NamingIt()
    {
        var script = new PostgresScript().Open();
        _ = Prepares(
            script,
            1,
            "DELETE FROM t WHERE a = $1 AND b = $2",
            ["unknown", "d2"],
            new([23, PostgresRows.D2.Oid], null)
        );

        // The request names the two types of the parameters; the closure holds the bases of the domain too.
        _ = Resolves(
            script,
            [23, PostgresRows.D2.Oid],
            PostgresRows.Int4,
            PostgresRows.Varchar,
            PostgresRows.EmailT,
            PostgresRows.D2
        );
        _ = script.Answer(
            "check",
            new PostgresCheckRequest(
                "DELETE FROM t WHERE a = @a AND b = @b",
                [new("a", "integer"), new("b", "public.d2")]
            ),
            new PostgresCheckAnswer(null, null, new PostgresBindError("b", "Writing is not supported"))
        );
        var exchange = Frees(script, 1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Request("DELETE FROM t WHERE a = @a AND b = @b", Inferred("a"), Declared("b", "d2")),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC232");
        failure.Arguments.ShouldBe(["public.d2", "@b", "Q"]);
        failure.ServerLines.ShouldBe(["Writing is not supported"]);
        failure.Help.ShouldBe(PostgresErrors.BindHelp);
        exchange.Unused.ShouldBeEmpty();
    }
```

In `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs`, add beside `Columns`:

```csharp
    private static readonly PostgresCheckRequest Check = new(
        "SELECT 1 FROM users WHERE id = @id",
        [new PostgresCheckParameter("id", "integer")]
    );

    private static readonly PostgresCheckAnswer Agrees = new([], null, null);
```

and in the three tests whose describe ends in a description, `Describe_QueryWithAParameter_WritesPrepareTypesAndDeallocateInOrder`, `Describe_DeallocateThatFails_WritesTheErrorThatIsNeverReported` and `Describe_ThroughTheLogOfASession_HasTheEngineBeforeEachNameAndNothingOfTheSecret`, add after the `.Answer("types", ...)` line:

```csharp
            .Answer("check", Check, Agrees)
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: the build fails: `PostgresErrors.Check` does not exist.

- [ ] **Step 7: Make the batch in `PostgresConnection`**

In `src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs`, before `ExecuteAsOneStatementAsync`:

```csharp
    /// <summary>
    /// A batch of one command that holds a user's SQL.  Its caller adds parameters to the command, runs or describes
    /// it, and disposes the batch.
    /// </summary>
    /// <remarks>
    /// The driver does not read the command of a batch that has no parameters and never splits it: it sends the
    /// text as one statement to parse, and the server refuses a text that holds a second one, <c>42601</c>, before
    /// it runs anything.  With a named parameter the driver reads the text to place the parameter, and refuses a
    /// text in which it finds a <c>;</c> that something follows: it throws a <see cref="NotSupportedException" />
    /// and sends nothing.  So whatever the text holds, and however the tool's lexer read it, one statement at most
    /// reaches the server.
    /// </remarks>
    public async Task<NpgsqlBatch> BatchOfOneAsync(string sql, CancellationToken cancellationToken)
    {
        var batch = new NpgsqlBatch(await OpenAsync(cancellationToken));
        try
        {
            batch.BatchCommands.Add(new NpgsqlBatchCommand(sql));
            return batch;
        }
        catch
        {
            await batch.DisposeAsync();
            throw;
        }
    }
```

and the body of `ExecuteAsOneStatementAsync` becomes:

```csharp
        await using var batch = await BatchOfOneAsync(sql, cancellationToken);
        _ = await batch.ExecuteNonQueryAsync(cancellationToken);
```

Its summary keeps its first sentence and its last, and says `see <see cref="BatchOfOneAsync" />` for the rest.

In `.editorconfig`, the comment above the `CA2100` line of `PostgresConnection.cs` says `its one batch, which carries the PREPARE`.  It becomes `its batches, which carry the PREPARE and the SQL of the check`, and the sentence that follows gains `; the check describes the user's own SQL and runs nothing` before its full stop.

- [ ] **Step 8: Write the live function**

In `src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs`, beside the other SQL:

```csharp
    // One parameter and nothing of a user's: what Npgsql says of a type's name alone.
    private const string ProbeSql = "SELECT $1";
```

and after `TypesAsync`:

```csharp
    /// <summary>
    /// Asks <c>check</c>: describes the SQL as generated code will send it, each parameter named as the query's
    /// list names it and bound by the name of its type, and runs nothing.
    /// </summary>
    /// <remarks>
    /// The text is a user's, so it goes as a batch of one command.  Npgsql refuses two things before it sends
    /// anything, with one of the same two exceptions for both: a type name it cannot bind, and a text in which it
    /// finds a <c>;</c> that something follows.  Which of the two it was is found by asking for each parameter
    /// alone, with a text of the tool's own.
    /// </remarks>
    public async Task<PostgresCheckAnswer> CheckAsync(PostgresCheckRequest asked, CancellationToken token)
    {
        try
        {
            await using var batch = await connection.BatchOfOneAsync(asked.Sql, token);
            foreach (var parameter in asked.Parameters)
            {
                _ = batch.BatchCommands[0].Parameters.Add(Unvalued(parameter.Name, parameter.DataTypeName));
            }

            await using var reader = await batch.ExecuteReaderAsync(CommandBehavior.SchemaOnly, token);
            var columns = new List<PostgresCheckColumn>(reader.FieldCount);
            for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
            {
                columns.Add(
                    new PostgresCheckColumn(
                        reader.GetName(ordinal),
                        reader.GetDataTypeOID(ordinal),
                        reader.GetDataTypeName(ordinal)
                    )
                );
            }

            return new PostgresCheckAnswer(columns, null, null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresCheckAnswer(null, PostgresConnection.ErrorOf(exception), null);
        }
        catch (NotSupportedException refused)
        {
            return await RefusedAsync(asked, refused.Message, token);
        }
        catch (InvalidCastException refused)
        {
            return await RefusedAsync(asked, refused.Message, token);
        }
    }

    // The driver refused and sent nothing.  The first parameter whose type it refuses alone is the one that cannot
    // be bound; when it refuses none, it refused the text, and its message is the answer's error.
    private async Task<PostgresCheckAnswer> RefusedAsync(PostgresCheckRequest asked, string message, CancellationToken token)
    {
        try
        {
            foreach (var parameter in asked.Parameters)
            {
                if (await RefusalOfAsync(parameter.DataTypeName, token) is { } refusal)
                {
                    return new PostgresCheckAnswer(null, null, new PostgresBindError(parameter.Name, refusal));
                }
            }

            return new PostgresCheckAnswer(null, new PostgresServerError(null, message, null, null, null), null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresCheckAnswer(null, PostgresConnection.ErrorOf(exception), null);
        }
    }

    // What Npgsql says of a parameter of this type and no other, or null when it binds it.
    private async Task<string?> RefusalOfAsync(string dataTypeName, CancellationToken token)
    {
        try
        {
            await using var command = await connection.CommandAsync(ProbeSql, token);
            _ = command.Parameters.Add(Unvalued(string.Empty, dataTypeName));
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SchemaOnly, token);
            return null;
        }
        catch (NotSupportedException refused)
        {
            return refused.Message;
        }
        catch (InvalidCastException refused)
        {
            return refused.Message;
        }
    }

    // A parameter with a type's name and no value, as a describe needs one.
    private static NpgsqlParameter Unvalued(string name, string dataTypeName) =>
        new()
        {
            ParameterName = name,
            Value = DBNull.Value,
            DataTypeName = dataTypeName,
        };
```

- [ ] **Step 9: Make the failures, and ask `check` in the session**

In `src/SqlSource.Tool/Describing/Postgres/PostgresErrors.cs`, beside the other help texts:

```csharp
    public const string CheckHelp =
        "a @ that one of the two reads as a parameter and the other as text; the server's lines say where";

    public const string BindHelp =
        "declare the parameter as a type Npgsql binds, text for one, with -- param: and cast it where it is used; "
        + "and report it: the sidecar's spelling of a type is what generated code binds with";

    public const string BindQuotedHelp =
        "Npgsql takes no type name that is written with quotes; declare the parameter as text with -- param: and "
        + "cast it to the type where it is used";
```

and after `ColumnPseudoType`:

```csharp
    /// <summary>
    /// What <c>check</c> is reported as, or null when the two describes agree.  A type Npgsql cannot bind is
    /// <c>SQLSRC232</c>, with Npgsql's message as its one line; anything else that differs is <c>SQLSRC231</c>,
    /// with the lines of the error when there is one.  There is no position line: a position the server gives
    /// is in Npgsql's own rewrite of the SQL.
    /// </summary>
    public static DescribeFailure? Check(
        DescribeRequest query,
        PostgresCheckRequest asked,
        PostgresCheckAnswer answer,
        CheckVerdict verdict
    )
    {
        if (verdict.Result == NpgsqlCheck.Same)
        {
            return null;
        }

        if (answer.DriverError is { } unbound)
        {
            return CannotBind(query, asked, unbound);
        }

        return new DescribeFailure(
            ToolDiagnostics.ParametersReadDifferently,
            new EquatableArray<string>([query.Name, verdict.Differs ?? string.Empty]),
            DescribeStep.CheckParameters,
            answer.Error is { } error ? ServerLines(error with { Position = null }, 0) : EquatableArray<string>.Empty,
            CheckHelp
        );
    }

    // SQLSRC232.  The type is the name that was asked with, and the parameter is written as the SQL writes one.
    private static DescribeFailure CannotBind(
        DescribeRequest query,
        PostgresCheckRequest asked,
        PostgresBindError unbound
    )
    {
        var index = 0;
        while (
            index < asked.Parameters.Count
            && !string.Equals(asked.Parameters[index].Name, unbound.Parameter, StringComparison.Ordinal)
        )
        {
            index++;
        }

        if (index == asked.Parameters.Count)
        {
            throw new InvalidOperationException(
                $"The answer to 'check' names the parameter '{unbound.Parameter}', which was not asked with."
            );
        }

        var type = asked.Parameters[index].DataTypeName;
        return new DescribeFailure(
            ToolDiagnostics.TypeCannotBeBound,
            new EquatableArray<string>([type, Written(query.Parameters[index]), query.Name]),
            DescribeStep.CheckParameters,
            new EquatableArray<string>([unbound.Message]),
            type.Contains('"', StringComparison.Ordinal) ? BindQuotedHelp : BindHelp
        );
    }
```

In `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`, the end of `DescribePreparedAsync`, from `var built = BuildColumns(`, becomes:

```csharp
        var built = BuildColumns(request, columns, origins, rows.Value, gated);
        if (!built.Succeeded)
        {
            return Failed(built.Failure);
        }

        return await CheckAsync(request, parameters.Value, columns, rows.Value, cancellationToken) is { } differs
            ? Failed(differs)
            : Described(parameters.Value, built.Value, gated);
```

and after `BuildColumns`:

```csharp
    // Asks Npgsql to describe the SQL as generated code will send it, and says what it saw otherwise than the
    // prepared statement's columns, or null.  It is asked for a query without parameters too: one Parse, which
    // still proves the two describes saw one query.
    private async Task<DescribeFailure?> CheckAsync(
        DescribeRequest request,
        IReadOnlyList<DescribedParameter> parameters,
        IReadOnlyList<PostgresColumnRow> columns,
        IReadOnlyDictionary<long, PostgresTypeRow> rows,
        CancellationToken cancellationToken
    )
    {
        var asked = new PostgresCheckRequest(
            ParameterPlaceholders.AsWritten(request.Sql)
                ?? throw new InvalidOperationException(
                    $"The SQL of '{request.Name}' was prepared and holds more than one statement."
                ),
            [.. parameters.Select(CheckParameterOf)]
        );
        var answer = await _exchange.AskAsync("check", asked, _live.CheckAsync, cancellationToken);
        return PostgresErrors.Check(request, asked, answer, NpgsqlCheck.Read(answer, columns, rows));
    }

    // A parameter as generated code will bind it: by the name of its type, as the sidecar will record it.
    private static PostgresCheckParameter CheckParameterOf(DescribedParameter parameter) =>
        new(
            parameter.Name,
            parameter.Type?.Name
                ?? throw new InvalidOperationException($"The parameter '{parameter.Name}' was given no type.")
        );
```

The comment above `DescribePreparedAsync` gains `, and Npgsql's own reading of the SQL` before its full stop.

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: every test passes but the four of `DescriberFixtureTests.Replay_Case_GivesTheExpectedEntry`, whose captures hold no `check` call.

- [ ] **Step 10: Record the four captures again**

Run: `tools/record-fixtures.sh`
Expected: `Recording 4 cases of postgres against postgres:18 ... done`.

Run: `git status --short tests/SqlSource.Tool.Tests/Fixtures/Describers`
Expected: the four `capture.json` files are modified and no `expected.json` is.  Read the diff: each capture gains one `check` call, whose request holds the SQL with `@name` and each parameter's type name, and whose answer has `"columns": []` and null for both errors.  If a case is no longer described, `SQLSRC231` or `SQLSRC232`, stop and tell the user what its check answered.

In `Replay_CaptureWithACallRemoved_ThrowsACaptureMiss` of `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs`, add:

```csharp
    [InlineData("check")]
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS.

- [ ] **Step 11: Write the container tests**

In `tests/SqlSource.Tests.Postgres/Describer/ColumnTests.cs`, the two tests of the calls' order gain `"check"` before `"deallocate"`: `["open", "prepare", "columns", "types", "check", "deallocate"]` and `["open", "prepare", "columns", "tables", "types", "check", "deallocate"]`.

Create `tests/SqlSource.Tests.Postgres/Describer/NpgsqlCheckTests.cs`:

```csharp
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Tool;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// The check of the tool's lexer against Npgsql, with a real driver and a real server.  Every describe of
// ParameterTests and of ColumnTests runs the check too, so each of their cases is one that passes it.
[Collection(UsesPostgres.Name)]
public sealed class NpgsqlCheckTests(PostgresContainer postgres)
{
    // What these tests need and the fixture schema lacks: a type in a schema whose name needs quotes, a domain
    // over a domain, and a table that a describe must leave as it is.  Each statement can be run again.
    private const string OwnObjects = """
        CREATE SCHEMA IF NOT EXISTS "My Schema";
        DO $do$
        BEGIN
            CREATE TYPE "My Schema"."Status" AS ENUM ('a', 'b');
        EXCEPTION WHEN duplicate_object THEN NULL;
        END
        $do$;
        DO $do$
        BEGIN
            CREATE DOMAIN check_d2 AS email_t;
        EXCEPTION WHEN duplicate_object THEN NULL;
        END
        $do$;
        CREATE TABLE IF NOT EXISTS check_canary (id int);
        """;

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(sql);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        string sql,
        params (string Name, string Type)[] declared
    ) => Sessions.DescribeAsync(postgres.ConnectionString, Requests.Of(sql, declared));

    private async Task ShouldPassAsync(string sql, params (string Name, string Type)[] declared)
    {
        var described = await DescribeAsync(sql, declared);

        described.Failure.ShouldBeNull(
            described.Failure is { } failure ? $"{failure.Descriptor.Id}: {string.Join("; ", failure.Arguments)}" : null
        );
    }

    [Theory]
    [InlineData("SELECT id, name FROM users WHERE id = @id")]
    [InlineData("SELECT id FROM users WHERE id = @ID OR id = @id")]
    [InlineData("SELECT $$a@b @c$$ AS x, id FROM users WHERE id = @id")]
    [InlineData("SELECT id FROM users WHERE tags @> @tags")]
    [InlineData("SELECT @id::int AS x")]
    [InlineData("SELECT tags[1:2] FROM users WHERE id = @id")]
    [InlineData("SELECT id, name FROM users")]
    [InlineData("UPDATE users SET name = @name WHERE id = @id")]
    [InlineData("INSERT INTO orders (user_id, total) VALUES (@user_id, @total) RETURNING id")]
    [InlineData("SELECT id FROM users WHERE id = @id; -- the end")]
    public async Task Describe_QueryNpgsqlReadsAsTheLexerDoes_PassesTheCheck(string sql) => await ShouldPassAsync(sql);

    // Npgsql ignores a parameter that the SQL does not reference, and still resolves its type.
    [Theory]
    [InlineData("DELETE FROM samples WHERE flag")]
    [InlineData("SELECT id FROM users WHERE id = @id")]
    public async Task Describe_DeclaredOnlyParameter_PassesTheCheck(string sql) =>
        await ShouldPassAsync(sql, ("tenant", "int"));

    // The name the describer spells for each kind of type is one Npgsql binds: an alias, a modifier that is
    // dropped, a domain, an enum, arrays, a range and a multirange.
    [Theory]
    [InlineData("int")]
    [InlineData("bigint")]
    [InlineData("smallint")]
    [InlineData("boolean")]
    [InlineData("real")]
    [InlineData("double precision")]
    [InlineData("numeric(18,2)")]
    [InlineData("varchar(50)")]
    [InlineData("char(2)")]
    [InlineData("text")]
    [InlineData("bytea")]
    [InlineData("uuid")]
    [InlineData("jsonb")]
    [InlineData("date")]
    [InlineData("time")]
    [InlineData("timestamp(3)")]
    [InlineData("timestamptz")]
    [InlineData("interval")]
    [InlineData("email_t")]
    [InlineData("user_status")]
    [InlineData("text[]")]
    [InlineData("user_status[]")]
    [InlineData("email_t[]")]
    [InlineData("tstzrange")]
    [InlineData("int4multirange")]
    public async Task Describe_ParameterOfEachKindOfType_IsBoundByItsSpelledName(string type) =>
        await ShouldPassAsync("SELECT 1 AS one FROM users WHERE @p IS NOT NULL", ("p", type));

    // What the check does not see, which docs/tech-debt/TD-0043 records: Npgsql sends $1 for @id and leaves the
    // "$2" of the SQL alone, the server types it, and the two describes agree.  Nothing would bind it at run time.
    [Fact]
    public async Task Describe_SqlWithAPlaceholderOfItsOwn_PassesTheCheck() =>
        await ShouldPassAsync("SELECT 1 FROM users WHERE id = @id AND name = $2");

    // Three types whose spelled names Npgsql refuses: one in a schema whose name needs quotes, the "char" of the
    // system catalogs, and a domain over a domain.
    [Theory]
    [InlineData("SELECT 1 AS one FROM users WHERE @p IS NOT NULL", "\"My Schema\".\"Status\"", "\"My Schema\".\"Status\"", true)]
    [InlineData("SELECT relname FROM pg_catalog.pg_class WHERE relkind = @p", null, "\"char\"", true)]
    [InlineData("SELECT 1 AS one FROM users WHERE @p IS NOT NULL", "check_d2", "public.check_d2", false)]
    public async Task Describe_ParameterNpgsqlCannotBind_IsSqlsrc232NamingIt(
        string sql,
        string? declared,
        string spelled,
        bool quoted
    )
    {
        _ = await ScalarAsync(OwnObjects);

        var described = declared is null ? await DescribeAsync(sql) : await DescribeAsync(sql, ("p", declared));

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC232");
        failure.Arguments.ShouldBe([spelled, "@p", Requests.Query]);
        failure.Step.ShouldBe(DescribeStep.CheckParameters);
        failure.ServerLines.ShouldHaveSingleItem().ShouldNotBeNullOrWhiteSpace();
        failure.Help.ShouldBe(quoted ? PostgresErrors.BindQuotedHelp : PostgresErrors.BindHelp);
    }

    // Npgsql resolves the type of a parameter it will not send.
    [Fact]
    public async Task Describe_DeclaredOnlyParameterNpgsqlCannotBind_IsSqlsrc232NamingIt()
    {
        _ = await ScalarAsync(OwnObjects);

        var described = await DescribeAsync("SELECT id FROM users WHERE id = @id", ("unused", "check_d2"));

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC232");
        failure.Arguments.ShouldBe(["public.check_d2", "@unused", Requests.Query]);
    }

    // Npgsql sends an array of a domain as an array of its base, and the server keeps the domain: the column the
    // parameter is returned as has two types.  docs/tech-debt records that the help speaks of a @.
    [Fact]
    public async Task Describe_ArrayOfADomainReturnedAsAColumn_IsSqlsrc231NamingBothTypes()
    {
        var described = await DescribeAsync("SELECT @p AS p", ("p", "email_t[]"));

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC231");
        failure.Arguments.ShouldBe([
            Requests.Query,
            "column 1 is character varying[] for Npgsql and email_t[] for SqlSource",
        ]);
        failure.ServerLines.ShouldBeEmpty();
    }

    // The live function itself, with a text no describe can bring it: "prepare" refuses such a text first.  The
    // tool's lexer reads "$x$" after a "§" as the start of a dollar-quoted string; Npgsql and the server read two
    // statements.  With a named parameter Npgsql refuses and sends nothing; without one the server refuses the
    // whole text.  Either way the answer is an error and the table of the second statement is still there.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CheckAsync_TextNpgsqlWouldSplit_AnswersAnErrorAndRunsNothing(bool withAParameter)
    {
        _ = await ScalarAsync(OwnObjects);
        var opened = await Sessions.OpenAsync(postgres.ConnectionString);
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();
        var live = new PostgresLive(session.Connection);

        var answer = await live.CheckAsync(
            withAParameter
                ? new PostgresCheckRequest(
                    "SELECT @id AS §$x$; DROP TABLE check_canary; --$x$",
                    [new PostgresCheckParameter("id", "integer")]
                )
                : new PostgresCheckRequest("SELECT 1 AS §$x$; DROP TABLE check_canary; --$x$", []),
            TestContext.Current.CancellationToken
        );

        answer.Columns.ShouldBeNull();
        answer.DriverError.ShouldBeNull();
        var error = answer.Error.ShouldNotBeNull();
        error.SqlState.ShouldBe(withAParameter ? null : "42601");
        (await ScalarAsync("SELECT to_regclass('check_canary') IS NOT NULL")).ShouldBe(true);
    }

    // A type that cannot be bound is found before the text is read, and is named with its parameter whichever
    // place of the list it has.
    [Fact]
    public async Task CheckAsync_SecondParameterCannotBeBound_AnswersItAsTheDriverError()
    {
        var opened = await Sessions.OpenAsync(postgres.ConnectionString);
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();
        var live = new PostgresLive(session.Connection);

        var answer = await live.CheckAsync(
            new PostgresCheckRequest(
                "SELECT @a, @b; SELECT 2",
                [new PostgresCheckParameter("a", "integer"), new PostgresCheckParameter("b", "no_such_type")]
            ),
            TestContext.Current.CancellationToken
        );

        answer.Error.ShouldBeNull();
        var unbound = answer.DriverError.ShouldNotBeNull();
        unbound.Parameter.ShouldBe("b");
        unbound.Message.ShouldContain("no_such_type");
    }

    [Fact]
    public async Task CheckAsync_SqlTheServerRejects_AnswersItsError()
    {
        var opened = await Sessions.OpenAsync(postgres.ConnectionString);
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();
        var live = new PostgresLive(session.Connection);

        var answer = await live.CheckAsync(
            new PostgresCheckRequest(
                "SELECT nope FROM users WHERE id = @id",
                [new PostgresCheckParameter("id", "integer")]
            ),
            TestContext.Current.CancellationToken
        );

        answer.Error.ShouldNotBeNull().SqlState.ShouldBe("42703");
    }
}
```

Run: `dotnet test --project tests/SqlSource.Tests.Postgres`
Expected: PASS, `ParameterTests` and `ColumnTests` included: each of their describes now runs the check.  If a case of `ParameterTests` or `ColumnTests` that passed before this step is now `SQLSRC231` or `SQLSRC232`, do not change the test: stop and tell the user the query and what the check answered.

Run: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres`
Expected: PASS.

- [ ] **Step 12: Keep `src/SqlSource.Tool/AGENTS.md` true**

In the bullet that begins `**The calls and their records are a contract with the captures.**`, the list of methods becomes `` `open`, `prepare`, `columns`, `tables`, `types`, `check` and `deallocate` ``.

In the bullet that begins `**Nothing a `.sql` file holds can make the describer run a statement.**`, after the sentence that ends `` and the `PREPARE` never goes through it. ``, add:

```markdown
`check` is the second text that holds a user's SQL, and goes the same way, through `PostgresConnection.BatchOfOneAsync`, under `CommandBehavior.SchemaOnly`: with a named parameter Npgsql reads the text to place it, and refuses one with a `;` that something follows by a `NotSupportedException`, having sent nothing, which `PostgresLive.CheckAsync` answers as an error and never lets through.
```

- [ ] **Step 13: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: every step `passed`.

```bash
git add .editorconfig src/SqlSource src/SqlSource.Tool docs/diagnostics.md tests/SqlSource.Tests tests/SqlSource.Tool.Tests tests/SqlSource.Tests.Postgres
```

```bash
git commit -m "Check that Npgsql reads a query's parameters as the lexer does

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 8: The events

Each of the three new calls says in the run's log what it asked and what it was answered, and each column that was built says what it is.  The order of a describe's events is `prepare`, `columns`, `tables`, `types`, one `column` for each column, `check`, `deallocate`.

**Files:**
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`
- Modify: `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs`

**Interfaces:**
- Consumes: the records of Tasks 3, 4 and 7; `CheckVerdict`; the wrappers `ColumnsAsync`, `TablesAsync` and `CheckAsync` and `BuildColumns` of the session.
- Produces: `PostgresEvents.Columns(IRunLog log, string query, PostgresColumnsRequest request, PostgresColumnsAnswer answer, long durationMs)`, `PostgresEvents.Tables(IRunLog log, PostgresTablesRequest request, PostgresTablesAnswer answer, long durationMs)`, `PostgresEvents.Check(IRunLog log, string query, PostgresCheckRequest request, CheckVerdict verdict, long durationMs)`, `PostgresEvents.Column(IRunLog log, string query, SidecarColumn column, bool gated)`.

- [ ] **Step 1: Write the failing tests**

In `tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs`, add `using SqlSource.Snapshot;` if the file lacks it, and change the tests that exist:

- `DescribeAsync(PostgresScript script, IRunLog log)` gains a third parameter, `DescribeRequest? query = null`, and describes `query ?? Query`.
- In `Describe_QueryWithAParameter_WritesPrepareTypesAndDeallocateInOrder`, the names become `["prepare", "columns", "types", "check", "deallocate"]`, `log.Events[1]` becomes `log.Events[2]` for `types`, and `log.Events[2]` becomes `log.Events[4]` for `deallocate`.
- In `Describe_TypesThatFails_WritesItsErrorInThePlaceOfTheRows`, the names become `["prepare", "columns", "types", "deallocate"]` and `log.Events[1]` becomes `log.Events[2]`.
- In `Describe_DeallocateThatFails_WritesTheErrorThatIsNeverReported`, `log.Events[2]` becomes `log.Events[^1]`.
- In `Describe_ThroughTheLogOfASession_HasTheEngineBeforeEachNameAndNothingOfTheSecret`, the names become `["postgres.prepare", "postgres.columns", "postgres.types", "postgres.check", "postgres.deallocate"]`.

Add:

```csharp
    // A query of users.id and an aggregate: one column with an origin and one without.
    private static readonly DescribeRequest Listing = new(
        "ListUsers",
        "SELECT id, count(*) OVER () AS n FROM users",
        EquatableArray<SqlQueryParameter>.Empty
    );

    private static readonly PostgresColumnRow IdColumn = PostgresTables.ColumnOf(0, "id", 23, PostgresTables.Users, 1);

    private static readonly PostgresColumnRow CountColumn = new(1, "n", 20, 0, 0, null, null, null);

    // The script of Listing up to "types", which a test ends as it needs.
    private static PostgresScript ListingScript() =>
        new PostgresScript()
            .Open()
            .Answer(
                "prepare",
                new PostgresPrepareRequest("sqlsource_1", Listing.Sql, []),
                new PostgresPrepareAnswer([], null)
            )
            .Answer(
                "columns",
                new PostgresColumnsRequest("sqlsource_1", 0),
                new PostgresColumnsAnswer([IdColumn, CountColumn], null)
            )
            .Answer(
                "tables",
                new PostgresTablesRequest([PostgresTables.Users.Oid]),
                new PostgresTablesAnswer([PostgresTables.Users], null)
            )
            .Answer(
                "types",
                new PostgresTypesRequest([20, 23]),
                new PostgresTypesAnswer([PostgresRows.Int8, PostgresRows.Int4], null)
            )
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

    private static PostgresCheckAnswer Sees(params PostgresColumnRow[] columns) =>
        new([.. columns.Select(static column => new PostgresCheckColumn(column.Name, column.TypeOid, "a type"))], null, null);

    [Fact]
    public async Task Describe_QueryWithColumns_WritesColumnsTablesAColumnForEachAndCheck()
    {
        var log = new RecordingLog();
        var script = ListingScript()
            .Answer("check", new PostgresCheckRequest(Listing.Sql, []), Sees(IdColumn, CountColumn));

        await DescribeAsync(script, log, Listing);

        log.Events.Select(written => written.Name)
            .ShouldBe(["prepare", "columns", "tables", "types", "column", "column", "check", "deallocate"]);

        var columns = log.Events[1].Members;
        NamesOf(columns).ShouldBe(["query", "statement", "columns", "durationMs"]);
        Member(columns, "query").ShouldBe((LogValue)"ListUsers");
        Member(columns, "statement").ShouldBe((LogValue)"sqlsource_1");
        Member(columns, "columns")
            .ShouldBeOfType<LogValue.Items>()
            .Values[0]
            .ShouldBe(
                LogValue.MapOf(
                    new("ordinal", 0L),
                    new("name", "id"),
                    new("typeOid", 23L),
                    new("tableOid", PostgresTables.Users.Oid),
                    new("attributeNumber", 1L),
                    new("length", LogValue.Null),
                    new("precision", LogValue.Null),
                    new("scale", LogValue.Null)
                )
            );

        var tables = log.Events[2].Members;
        NamesOf(tables).ShouldBe(["oids", "tables", "durationMs"]);
        Member(tables, "oids").ShouldBe(LogValue.ListOf([PostgresTables.Users.Oid]));
        var users = Member(tables, "tables").ShouldBeOfType<LogValue.Items>().Values[0].ShouldBeOfType<LogValue.Members>();
        users.Values.Select(value => value.Name).ShouldBe(["oid", "relkind", "schema", "name", "columns"]);
        users
            .Values[4]
            .Value.ShouldBeOfType<LogValue.Items>()
            .Values[0]
            .ShouldBe(
                LogValue.MapOf(
                    new("attnum", 1L),
                    new("name", "id"),
                    new("notNull", true),
                    new("identity", "a"),
                    new("generated", string.Empty),
                    new("typeOid", 23L)
                )
            );

        var id = log.Events[4].Members;
        NamesOf(id).ShouldBe(["query", "ordinal", "name", "type", "origin", "nullable", "nullableSource", "gated"]);
        Member(id, "query").ShouldBe((LogValue)"ListUsers");
        Member(id, "ordinal").ShouldBe((LogValue)0L);
        Member(id, "name").ShouldBe((LogValue)"id");
        Member(id, "type").ShouldBe((LogValue)"integer");
        Member(id, "origin")
            .ShouldBe(LogValue.MapOf(new("schema", "public"), new("table", "users"), new("column", "id")));
        Member(id, "nullable").ShouldBe((LogValue)false);
        Member(id, "nullableSource").ShouldBe((LogValue)"catalog");
        Member(id, "gated").ShouldBe((LogValue)false);

        var count = log.Events[5].Members;
        Member(count, "origin").ShouldBe(LogValue.Null);
        Member(count, "nullable").ShouldBe(LogValue.Null);
        Member(count, "nullableSource").ShouldBe((LogValue)"no-origin");

        var check = log.Events[6].Members;
        NamesOf(check).ShouldBe(["query", "parameters", "result", "differs", "durationMs"]);
        Member(check, "result").ShouldBe((LogValue)"same");
        Member(check, "differs").ShouldBe(LogValue.Null);
    }

    [Fact]
    public async Task Describe_QueryWithAParameter_WritesItsNameAndTypeNameInCheck()
    {
        var log = new RecordingLog();
        var script = new PostgresScript()
            .Open()
            .Answer("prepare", Prepare, new PostgresPrepareAnswer([23], null))
            .Answer("columns", Columns, PostgresScript.NoColumns)
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer([PostgresRows.Int4], null))
            .Answer("check", Check, Agrees)
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

        await DescribeAsync(script, log);

        Member(log.Events.Single(written => written.Name == "check").Members, "parameters")
            .ShouldBe(LogValue.ListOf([LogValue.MapOf(new("name", "id"), new("dataTypeName", "integer"))]));
    }

    [Fact]
    public async Task Describe_CheckThatDiffers_WritesWhatDiffers()
    {
        var log = new RecordingLog();
        var script = ListingScript().Answer("check", new PostgresCheckRequest(Listing.Sql, []), Sees(IdColumn));

        await DescribeAsync(script, log, Listing);

        var check = log.Events.Single(written => written.Name == "check").Members;
        Member(check, "result").ShouldBe((LogValue)"differ");
        Member(check, "differs").ShouldBe((LogValue)"the result has 1 columns for Npgsql and 2 for SqlSource");
    }

    [Fact]
    public async Task Describe_CheckWithATypeThatCannotBeBound_SaysUnboundWithTheDriversMessage()
    {
        var log = new RecordingLog();
        var script = new PostgresScript()
            .Open()
            .Answer("prepare", Prepare, new PostgresPrepareAnswer([23], null))
            .Answer("columns", Columns, PostgresScript.NoColumns)
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer([PostgresRows.Int4], null))
            .Answer(
                "check",
                Check,
                new PostgresCheckAnswer(null, null, new PostgresBindError("id", "no such type"))
            )
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

        await DescribeAsync(script, log);

        var check = log.Events.Single(written => written.Name == "check").Members;
        Member(check, "result").ShouldBe((LogValue)"unbound");
        Member(check, "differs").ShouldBe((LogValue)"no such type");
    }

    // A columns call that failed has its error in the place of the columns.
    [Fact]
    public async Task Describe_ColumnsThatFails_WritesItsErrorInThePlaceOfTheColumns()
    {
        var log = new RecordingLog();
        var lost = new PostgresServerError(null, "the connection to the server was lost", null, null, null);
        var script = new PostgresScript()
            .Open()
            .Answer("prepare", Prepare, new PostgresPrepareAnswer([23], null))
            .Answer("columns", Columns, new PostgresColumnsAnswer(null, lost))
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

        await DescribeAsync(script, log);

        log.Events.Select(written => written.Name).ShouldBe(["prepare", "columns", "deallocate"]);
        NamesOf(log.Events[1].Members).ShouldBe(["query", "statement", "error", "durationMs"]);
    }

    [Fact]
    public async Task Describe_TablesThatFails_WritesItsErrorInThePlaceOfTheTables()
    {
        var log = new RecordingLog();
        var refused = new PostgresServerError("42501", "permission denied for table pg_attribute", null, null, null);
        var script = new PostgresScript()
            .Open()
            .Answer(
                "prepare",
                new PostgresPrepareRequest("sqlsource_1", Listing.Sql, []),
                new PostgresPrepareAnswer([], null)
            )
            .Answer(
                "columns",
                new PostgresColumnsRequest("sqlsource_1", 0),
                new PostgresColumnsAnswer([IdColumn], null)
            )
            .Answer(
                "tables",
                new PostgresTablesRequest([PostgresTables.Users.Oid]),
                new PostgresTablesAnswer(null, refused)
            )
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

        await DescribeAsync(script, log, Listing);

        log.Events.Select(written => written.Name).ShouldBe(["prepare", "columns", "tables", "deallocate"]);
        NamesOf(log.Events[2].Members).ShouldBe(["oids", "error", "durationMs"]);
    }

    // The describe ends at the first column of a pseudo-type: the columns before it have their events, and it and
    // the ones after it have none.
    [Fact]
    public async Task Describe_ColumnOfAPseudoType_WritesAColumnForTheColumnsBeforeItAlone()
    {
        var log = new RecordingLog();
        var script = new PostgresScript()
            .Open()
            .Answer(
                "prepare",
                new PostgresPrepareRequest("sqlsource_1", Listing.Sql, []),
                new PostgresPrepareAnswer([], null)
            )
            .Answer(
                "columns",
                new PostgresColumnsRequest("sqlsource_1", 0),
                new PostgresColumnsAnswer(
                    [
                        new(0, "one", 23, 0, 0, null, null, null),
                        new(1, "pair", 2249, 0, 0, null, null, null),
                        new(2, "two", 23, 0, 0, null, null, null),
                    ],
                    null
                )
            )
            .Answer(
                "types",
                new PostgresTypesRequest([23, 2249]),
                new PostgresTypesAnswer([PostgresRows.Int4, PostgresRows.Record], null)
            )
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

        await DescribeAsync(script, log, Listing);

        log.Events.Select(written => written.Name).ShouldBe(["prepare", "columns", "types", "column", "deallocate"]);
        Member(log.Events[3].Members, "name").ShouldBe((LogValue)"one");
    }

    // A column's name is a user's text, and the connection's value is taken out of it as out of any string.
    [Fact]
    public async Task Describe_ThroughTheLogOfASession_RemovesTheSecretFromAColumnsNameAndItsOrigin()
    {
        var log = new RecordingLog();
        var script = ListingScript()
            .Answer("check", new PostgresCheckRequest(Listing.Sql, []), Sees(IdColumn, CountColumn));

        await DescribeAsync(script, new SessionRunLog(log, "users", "postgres."), Listing);

        var id = log.Events.First(written => written.Name == "postgres.column").Members;
        Member(id, "origin")
            .ShouldBe(LogValue.MapOf(new("schema", "public"), new("table", "***"), new("column", "id")));
    }
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Describing.Postgres.PostgresEventsTests"`
Expected: FAIL: no `columns`, `tables`, `column` or `check` event is written.

- [ ] **Step 2: Write the events**

In `src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs`, add `using SqlSource.Snapshot;`, update the remarks of the class with the sentence `A describe writes <c>prepare</c>, <c>columns</c>, <c>tables</c>, <c>types</c>, one <c>column</c> for each column, <c>check</c> and <c>deallocate</c>, in that order, each only when its call was made.`, and after `Types`:

```csharp
    /// <summary><c>postgres.columns</c>: each column as the answer holds it, or the error.</summary>
    public static void Columns(
        IRunLog log,
        string query,
        PostgresColumnsRequest request,
        PostgresColumnsAnswer answer,
        long durationMs
    ) =>
        log.Write(
            "columns",
            new("query", query),
            new("statement", request.Statement),
            answer.Error is { } error
                ? new LogMember(Error, ErrorOf(error))
                : new LogMember("columns", LogValue.ListOf((answer.Columns ?? []).Select(ColumnOf))),
            new(Duration, durationMs)
        );

    /// <summary><c>postgres.tables</c>: each table with its columns as the answer holds them, or the error.</summary>
    public static void Tables(
        IRunLog log,
        PostgresTablesRequest request,
        PostgresTablesAnswer answer,
        long durationMs
    ) =>
        log.Write(
            "tables",
            new("oids", LogValue.ListOf(request.Oids.Select(static oid => (LogValue)oid))),
            answer.Error is { } error
                ? new LogMember(Error, ErrorOf(error))
                : new LogMember("tables", LogValue.ListOf((answer.Tables ?? []).Select(TableOf))),
            new(Duration, durationMs)
        );

    /// <summary>
    /// <c>postgres.check</c>: the name and the type name of each parameter, whether the two describes agree, and
    /// what differs.  The SQL is in <c>postgres.prepare</c>.
    /// </summary>
    public static void Check(
        IRunLog log,
        string query,
        PostgresCheckRequest request,
        CheckVerdict verdict,
        long durationMs
    ) =>
        log.Write(
            "check",
            new("query", query),
            new(
                "parameters",
                LogValue.ListOf(
                    request.Parameters.Select(static parameter =>
                        LogValue.MapOf(new("name", parameter.Name), new("dataTypeName", parameter.DataTypeName))
                    )
                )
            ),
            new("result", verdict.Result),
            new("differs", verdict.Differs),
            new(Duration, durationMs)
        );

    /// <summary>
    /// <c>postgres.column</c>: one column as it will be recorded, and whether its query was gated.  It is the one
    /// place that says why a column is nullable beside what was asked to find out.
    /// </summary>
    public static void Column(IRunLog log, string query, SidecarColumn column, bool gated) =>
        log.Write(
            "column",
            new("query", query),
            new("ordinal", (long)column.Ordinal),
            new("name", column.Name),
            new("type", column.Type.Name),
            new(
                "origin",
                column.Origin is { } origin
                    ? LogValue.MapOf(
                        new("schema", origin.Schema),
                        new("table", origin.Table),
                        new("column", origin.Column)
                    )
                    : LogValue.Null
            ),
            new("nullable", column.Nullable is { } nullable ? (LogValue)nullable : LogValue.Null),
            new("nullableSource", column.NullableSource),
            new("gated", gated)
        );
```

and beside `RowOf`:

```csharp
    private static LogValue ColumnOf(PostgresColumnRow column) =>
        LogValue.MapOf(
            new("ordinal", (long)column.Ordinal),
            new("name", column.Name),
            new("typeOid", column.TypeOid),
            new("tableOid", column.TableOid),
            new("attributeNumber", (long)column.AttributeNumber),
            new("length", LogValue.Of(column.Length)),
            new("precision", LogValue.Of(column.Precision)),
            new("scale", LogValue.Of(column.Scale))
        );

    private static LogValue TableOf(PostgresTableRow table) =>
        LogValue.MapOf(
            new("oid", table.Oid),
            new("relkind", table.Relkind),
            new("schema", table.Schema),
            new("name", table.Name),
            new("columns", LogValue.ListOf(table.Columns.Select(AttributeOf)))
        );

    private static LogValue AttributeOf(PostgresAttributeRow attribute) =>
        LogValue.MapOf(
            new("attnum", (long)attribute.Attnum),
            new("name", attribute.Name),
            new("notNull", attribute.NotNull),
            new("identity", attribute.Identity),
            new("generated", attribute.Generated),
            new("typeOid", attribute.TypeOid)
        );
```

- [ ] **Step 3: Write them where each call is asked**

In `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`:

The wrappers of `columns` and `tables` become, as `PrepareAsync` and `TypesAsync` are:

```csharp
    private async Task<PostgresColumnsAnswer> ColumnsAsync(
        string query,
        PostgresColumnsRequest request,
        CancellationToken cancellationToken
    )
    {
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("columns", request, _live.ColumnsAsync, cancellationToken);
        PostgresEvents.Columns(_log, query, request, answer, ElapsedSince(started));
        return answer;
    }

    private async Task<PostgresTablesAnswer> TablesAsync(
        PostgresTablesRequest request,
        CancellationToken cancellationToken
    )
    {
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("tables", request, _live.TablesAsync, cancellationToken);
        PostgresEvents.Tables(_log, request, answer, ElapsedSince(started));
        return answer;
    }
```

and `DescribePreparedAsync` calls `ColumnsAsync(request.Name, new PostgresColumnsRequest(statement, request.Parameters.Count), cancellationToken)`.

In `CheckAsync`, the two lines from `var answer = await _exchange.AskAsync("check"` become:

```csharp
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("check", asked, _live.CheckAsync, cancellationToken);
        var verdict = NpgsqlCheck.Read(answer, columns, rows);
        PostgresEvents.Check(_log, request.Name, asked, verdict, ElapsedSince(started));
        return PostgresErrors.Check(request, asked, answer, verdict);
```

`BuildColumns` loses `static`, and its line `built.Add(PostgresColumns.Build(column, origins[index], rows, gated));` becomes:

```csharp
            var recorded = PostgresColumns.Build(column, origins[index], rows, gated);
            PostgresEvents.Column(_log, request.Name, recorded, gated);
            built.Add(recorded);
```

Run: `dotnet test --project tests/SqlSource.Tool.Tests`
Expected: PASS.

- [ ] **Step 4: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: every step `passed`.  No capture changes: an event is no call.

```bash
git add src/SqlSource.Tool tests/SqlSource.Tool.Tests
```

```bash
git commit -m "Log the columns, the tables, each column and the check of a describe

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: The fixtures

Eight cases with rows are recorded, and the four of sub-phase 3.2 are recorded with them once more.  Each new `expected.json` has `matchesTable` `null` and `tableMatch` `no-origin`: a placeholder that sub-phase 3.4 records again.

**Files:**
- Create: `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/<case>/query.sql` for the eight cases below, and the `capture.json` and `expected.json` that the recording writes beside each
- Modify: `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs`

**Interfaces:**
- Consumes: the describer as Tasks 2 to 8 left it; `tools/record-fixtures.sh`; `FixtureCasesTests` and `DescriberFixtureTests`.
- Produces: twelve cases under `postgres/`, which sub-phases 3.4 and 3.5 record again.

- [ ] **Step 1: Write the eight queries**

Each file ends with one line break and holds exactly one query.

`select-plain/query.sql`:

```sql
-- name: GetEverything
SELECT
    u.id, u.name, u.email, u.status, u.tags, u.balance, u.created_at, u.deleted_at, u.name_upper,
    s.flag, s.small, s.big, s.ratio, s.amount, s.code, s.label, s.born, s.at_time, s.at_local, s.span, s.token,
    s.payload, s.document,
    b.during, b.slots
FROM users AS u
CROSS JOIN samples AS s
CROSS JOIN bookings AS b
WHERE u.id = @id;
```

`select-expressions/query.sql`:

```sql
-- name: CountOrders
SELECT
    user_id,
    count(*) AS orders,
    upper(max(o.note)) AS loudest,
    sum(o.total)::numeric(20, 4) AS spent,
    'x' AS mark,
    NULL AS nothing,
    1
FROM orders AS o
JOIN bookings AS b USING (user_id)
GROUP BY user_id;
```

`select-view/query.sql`:

```sql
-- name: ListActiveUsers
SELECT id, name, email, status FROM active_users;
```

`select-left-join/query.sql`:

```sql
-- name: GetUserOrders
SELECT u.id, u.email, o.total, count(*) OVER () AS n
FROM users u LEFT JOIN orders o ON o.user_id = u.id
WHERE u.id = @id;
```

`select-suffix-names/query.sql`:

```sql
-- name: GetUserFlags
SELECT deleted_at AS "deleted_at!", name AS "name?" FROM users WHERE id = @id;
```

`select-other-schema/query.sql`:

```sql
-- name: ListInvoices
SELECT id, order_id, amount, issued_on FROM billing.invoices WHERE order_id = @order_id;
```

`insert-returning/query.sql`:

```sql
-- name: PlaceOrder
INSERT INTO orders (user_id, total, note) VALUES (@user_id, @total, @note) RETURNING id, placed_at;
```

`select-declared-domain/query.sql`:

```sql
-- name: FindUserByEmail
-- param: @email email_t
SELECT id, name FROM users WHERE email = @email;
```

- [ ] **Step 2: Record**

Run: `tools/record-fixtures.sh`
Expected: `Recording 12 cases of postgres against postgres:18 ... done`.

Run: `git status --short tests/SqlSource.Tool.Tests/Fixtures/Describers`
Expected: sixteen new files, a `capture.json` and an `expected.json` for each new case, beside the eight `query.sql`.  The four captures of sub-phase 3.2 are unchanged if Task 7 recorded them, and no older `expected.json` is modified.

- [ ] **Step 3: Read what was recorded**

A recording compares nothing, so read each new `expected.json` against this table before it is committed.  Anything else is a finding: stop and tell the user.

| Case | What its `expected.json` must say |
|----|----|
| `select-plain` | `resultKind` `rows`, `plan` `not-needed`, `matchesTable` `null`, `tableMatch` `no-origin`; 25 columns; `id` is `integer`, `nullable` `false` with `catalog`, `identity` `true`; `name` is `character varying(100)` with `length` 100; `email` is `public.email_t`, `kind` `domain`, with a `base` named `character varying(254)`, `nullable` `true` with `catalog`; `status` is `public.user_status` with its two labels; `tags` is `text[]`; `balance` is `numeric(18,2)`; `name_upper` has `computed` `true` and `identity` `false`; `code` is `character(2)`; `at_local` is `timestamp(3) without time zone`; `during` is `tstzrange` and `slots` `int4multirange`; every column has its `origin` |
| `select-expressions` | `plan` `not-needed`; `user_id` has the origin `public.orders.user_id`, `nullable` `false` with `catalog`; `orders`, `loudest`, `spent`, `mark`, `nothing` and `?column?` have `origin` `null`, `nullable` `null` with `no-origin`, and `identity` and `computed` `null`; `spent` is `numeric(20,4)`; `mark` and `nothing` are `text`; the last column is named `?column?` |
| `select-view` | `plan` `not-needed`; four columns, each with the origin `public.active_users.<name>`, `nullable` `null` with `view`; `email` is `public.email_t` |
| `select-left-join` | `plan` `unavailable`; `id`, `email` and `total` are `nullable` `true` with `heuristic`, with their origins in `users` and `orders`; `n` is `bigint`, `nullable` `null` with `no-origin`, `origin` `null` |
| `select-suffix-names` | The columns are named `deleted_at!` and `name?`; `deleted_at!` is `nullable` `true` and `name?` `false`, both with `catalog` |
| `select-other-schema` | Each origin has `schema` `billing` and `table` `invoices`; `amount` is `numeric(18,2)` |
| `insert-returning` | `resultKind` `rows`; `id` is `bigint` with `identity` `true`; `placed_at` is `timestamp with time zone`; three parameters, `user_id` `integer`, `total` `numeric`, `note` `text`, each `inferred` |
| `select-declared-domain` | The parameter `email` is `public.email_t` with `typeSource` `declared`; in `capture.json` the `check` request binds it as `public.email_t` and its answer has no error |

In each new `capture.json`, the calls are `open`, `prepare`, `columns`, `tables`, `types`, `check`, `deallocate`, in that order, and no value of it holds anything of a connection.

- [ ] **Step 4: Hold the cases in `DescriberFixtureTests`**

In `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs`, replace `Cases_OfPostgres_HoldTheFirstFour`:

```csharp
    // The cases of the PostgreSQL describer.  A build that copied none would leave the theory with nothing to run.
    [Fact]
    public void Cases_OfPostgres_HoldEveryCase()
    {
        var names = Found.Select(static found => found.ToString()).ToList();

        foreach (
            var name in new[]
            {
                "postgres/delete-declared-only",
                "postgres/insert-declared",
                "postgres/insert-returning",
                "postgres/select-declared-domain",
                "postgres/select-expressions",
                "postgres/select-left-join",
                "postgres/select-other-schema",
                "postgres/select-plain",
                "postgres/select-suffix-names",
                "postgres/select-view",
                "postgres/update-array-and-range",
                "postgres/update-inferred",
            }
        )
        {
            names.ShouldContain(name);
        }
    }
```

`Replay_CaptureWithACallRemoved_ThrowsACaptureMiss` cuts a call from `update-inferred`, which has no rows and so no `tables` call.  Give the theory the case too, so that `tables` is cut from one that has it: its signature becomes `(string method, string from = "update-inferred")`, the line `var source = Found.Single(static found => found.ToString() == "postgres/update-inferred");` becomes `var source = Found.Single(found => found.ToString() == "postgres/" + from);`, and this line is added to its data:

```csharp
    [InlineData("tables", "select-plain")]
```

- [ ] **Step 5: Replay, and describe live on both servers**

Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Fixtures.DescriberFixtureTests"`
Expected: PASS: twelve cases replayed with no database, byte for byte.

Run: `dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.FixtureCasesTests"`
Expected: PASS on the default image.

Run: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14 dotnet test --project tests/SqlSource.Tests.Postgres -- --filter-class "SqlSource.Tests.Postgres.Describer.FixtureCasesTests"`
Expected: PASS: PostgreSQL 14 describes each case as 18 does, but for the version.  A case that differs there is a finding: stop and tell the user.

- [ ] **Step 6: Format, validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: every step `passed`.

```bash
git add tests/SqlSource.Tool.Tests/Fixtures
```

```bash
git commit -m "Record the fixtures of the PostgreSQL describer's columns

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: The documents, and the outline

**Files:**
- Modify: `src/SqlSource.Tool/AGENTS.md`
- Create: `docs/tech-debt/TD-0044-small-gaps-of-the-postgresql-describers-columns.md`
- Create: `docs/tech-debt/TD-0045-the-check-against-npgsql-is-best-effort.md`
- Modify: `docs/tech-debt/TD-0043-small-gaps-of-the-postgresql-describers-parameters.md`
- Modify: `docs/tech-debt/README.md`
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`

**Interfaces:**
- Consumes: everything Tasks 2 to 9 built, by name.
- Produces: nothing a later task uses.

- [ ] **Step 1: Check the ids of the tech debt**

Run: `grep -n 'Next id' docs/tech-debt/README.md`
Expected: `Next id: \`TD-0044\``.  If `main` has taken `TD-0044` or `TD-0045` since, use the next two free ids here and in every text below.

- [ ] **Step 2: Rewrite the describer's bullets of `src/SqlSource.Tool/AGENTS.md`**

Replace the whole bullet that begins `**`Describing/Postgres/` is the PostgreSQL describer, and it is registered nowhere.**` with:

```markdown
- **`Describing/Postgres/` is the PostgreSQL describer, and it is registered nowhere.**  `ToolHost.Create` still gives `DescriberRegistry.Empty`, and its tests alone reach it, until the describer is whole: a description lacks `matchesTable`, which is always `null` with `tableMatch` `no-origin`, and the walk.  It describes a query through one prepared statement, and `PostgresSession.DescribePreparedAsync` holds the order: `prepare`, which runs `PREPARE <name> (<declared type or unknown>, ...) AS <sql>` on the SQL that `ParameterPlaceholders.Rewrite` gave `$n` and reads `parameter_types` of `pg_prepared_statements`; `columns`, which describes `EXECUTE <name>(NULL, ...)` under `CommandBehavior.SchemaOnly` and runs nothing; `tables`, one query of `pg_class` and `pg_attribute` for every table the columns name; `types`, once, for the closure of the OIDs of the parameters, the columns and the origin attributes; the parameters typed; the columns built; `check`; and `deallocate` in the `finally`.  `PREPARE` is the route because it takes a declared type as written, `int`, `varchar(50)`, a domain, resolves it and checks it against every use of the parameter; Npgsql's own describe sends no types.  `types` comes after `tables` because the type of an origin attribute is where a domain is found, and the request is one.  A call is not made when it has nothing to ask: `tables` when no column names a table, `types` when there is no OID.  A statement without rows is `SidecarResultKind.None` with no columns and no provenance.
- **A column is built from three answers, each by a class of its own.**  `ColumnOrigin.Find` gives the table and the attribute of a column's `tableOid` and `attributeNumber`; a system column, `ctid`, has an attribute number below zero and no origin, and a table or an attribute that `tables` did not answer is a throw.  `ColumnTypes.OidOf` chooses the type: the row description flattens a domain to its base with the domain's modifier, so the column is the origin attribute's type when that is a domain whose chain of bases reaches the reported type, and the reported type otherwise; an expression over a domain says the base.  An array of a domain is not flattened and needs no origin.  `ColumnFacets.Of` reads the facets from Npgsql's `ColumnSize`, `NumericPrecision` and `NumericScale`, since Npgsql gives no modifier, by the internal name of the reported type and for no other type, so an array, which Npgsql gives its element's numbers, gets none.  Three of Npgsql's numbers are decoded: a `numeric` with a precision and no scale has the scale 0; a scale of 1024 or more is the scale less 2048, a negative one of PostgreSQL 15; and the precision 65535 of an `interval` is none.  `PostgresColumns.Build` puts them together.  A column of a pseudo-type, `record`, is `SQLSRC229` and ends the describe; a parameter of one is found first and stays `SQLSRC226` or `SQLSRC228`.
- **A column is `nullable` `false` only when the catalog says `NOT NULL`, its origin is a table, and the query is not gated** (`ColumnNullability`).  No origin is `null` with `no-origin`; an origin in a view, a materialized view or a foreign table is `null` with `view`; a gated query makes every column of a table `true` with `heuristic`.  `OuterJoinGate.IsGated` is the gate: one of `LEFT`, `RIGHT`, `FULL`, `OUTER`, `ROLLUP`, `CUBE` and `GROUPING` as a whole word, in any case, in a `Text` lexeme, so a string, a comment and a quoted identifier do not count and `left(name, 3)` does.  The last three are the grouping sets, which put `NULL` into a `NOT NULL` grouping column with no join.  `plan` is `not-needed` for a query with rows that is not gated and `unavailable` for one that is, until sub-phase 3.5's walk makes it `walked`.
- **`check` is the lexer against Npgsql** (`NpgsqlCheck`).  The describer prepares the SQL with `$n` where the tool's lexer found a parameter; generated code will send the SQL as written, and Npgsql will find the parameters.  So `PostgresLive.CheckAsync` describes `ParameterPlaceholders.AsWritten`, the SQL cut at its trailing `;`, with a parameter for each of the query's list, bound by `DataTypeName` with the type's spelled `name` and no value, and `NpgsqlCheck.Read` compares the columns it gets with the answer of `columns`, in count, names and type OIDs.  A difference, or an error, is `SQLSRC231`.  Npgsql refuses a type name it cannot bind before it sends anything, with a `NotSupportedException` or an `InvalidCastException`, and refuses a text with a `;` that something follows the same way; the live function then asks for each parameter alone with `SELECT $1`, and the first that is refused is the answer's `driverError`, `SQLSRC232`, while a refusal that is no parameter's is the answer's `error`.  `SQLSRC232` is reachable from a `.sql` file: a name that needs quotes, `"char"`, and a domain over a domain.  The check is best effort, and `docs/tech-debt/TD-0045-the-check-against-npgsql-is-best-effort.md` says what it does not see.
```

In the bullet that begins `**The calls and their records are a contract with the captures.**`, after the list of the methods, the sentence `An OID is a `long`.` is followed by: `A row of `tables` names its members as the catalog does where the catalog has one word, `attnum`, `relkind`, and a column of `columns` holds Npgsql's three numbers as it gave them, undecoded.`

In the bullet that begins `**A server's error gets its id from four things, in this order**`, add at its end: `An error of `columns` is `SQLSRC225` at the step `describe columns`, of `tables` at `catalog`, and of `check` it is `SQLSRC231` at `check parameters`; none of the three has an `at character` line, since the server counts in a text the user did not write.`

In the bullet that begins `**A type's `name` is the tool's own spelling**`, the sentence `A parameter has no facets, since the protocol gives it an OID and no modifier; `PostgresFacets` is there for the columns.` becomes `A parameter has no facets, since the protocol gives it an OID and no modifier; a column has them, from `ColumnFacets`.`, and after `docs/tech-debt/TD-0043-small-gaps-of-the-postgresql-describers-parameters.md` add ` and `docs/tech-debt/TD-0044-small-gaps-of-the-postgresql-describers-columns.md``.

In the bullet that begins `**`tests/SqlSource.Tool.Tests/PostgresScript.cs` answers the session with no server.**`, after the sentence about `PostgresRows.cs`, add: `` `tests/SqlSource.Tool.Tests/PostgresTables.cs` has tables of `tables` and columns that name them. ``

Run: `grep -c 'sub-phase 3.3' src/SqlSource.Tool/AGENTS.md`
Expected: `0`.

- [ ] **Step 3: Write the two items of tech debt**

Create `docs/tech-debt/TD-0044-small-gaps-of-the-postgresql-describers-columns.md`:

```markdown
# TD-0044 - Small gaps of the PostgreSQL describer's columns

## Problem

Five things that the PostgreSQL describer records of a column are less than the server knows, or more than it promises:

- **The modifier of a type outside `pg_catalog`.**  [`ColumnFacets`](../../src/SqlSource.Tool/Describing/Postgres/ColumnFacets.cs) reads a modifier for the ten built-in types that carry one, by internal name.  A type of an extension has its own: PostGIS's `geometry(Point,4326)` is recorded as `public.geometry`, with no facet.
- **The facets of an array's element.**  A column of `character varying(10)[]` is recorded as `character varying[]`, and its `element` has no `length`.  Npgsql gives the array the numbers of its element, and the describer drops them.
- **The fields of an interval.**  `interval year to month` is recorded as `interval`, and `interval day to second(3)` as `interval(3)`.  Npgsql gives an interval's precision and nothing of its fields.
- **A foreign table says `view`.**  PostgreSQL does not enforce the constraints of a foreign table, whose `attnotnull` is a declaration about the remote, so [`ColumnNullability`](../../src/SqlSource.Tool/Describing/Postgres/ColumnNullability.cs) answers `null` for its columns, with the source `view` for want of a word of its own.
- **A `NOT NULL` that is `NOT VALID`.**  PostgreSQL 18 lets a `NOT NULL` constraint be added `NOT VALID`.  It sets `attnotnull`, and rows that were there before may hold `NULL`.  The describer says `nullable` `false` with `catalog`.

## Why it exists

The first three: Npgsql does not expose a column's raw type modifier, only `ColumnSize`, `NumericPrecision` and `NumericScale`, which are the modifier decoded for the types Npgsql knows.  The spec of sub-phase 3.3 chose them over a second describe with `KeyInfo`, which costs a query of `pg_attribute` for each query described.  The fourth: the sidecar format's provenance values have none for a foreign table, and a new value is a change of the format.  The fifth was found while the catalog query was written, on a server that few projects run yet.

## Impact

Low.  The describer is registered nowhere until sub-phase 3.6.  The first three lose detail that the generated code does not need to read a value: a `geometry` is read as one whatever its modifier, and a C# type has no length.  They matter to a tool that would compare a sidecar with a schema.  The fourth gives a foreign table's column the right answer, unknown, under a word that is not quite true.  The fifth is the one that can be wrong: a column recorded as not nullable that holds `NULL` gives a generated model that throws when it reads the row.

## Proposed fix

For the first three, read each column's modifier from the server: `tables` already reads `pg_attribute`, and `atttypmod` with `format_type(atttypid, atttypmod)` gives the modifier and its spelling for every column that has an origin; a column without one keeps Npgsql's numbers.  For the fourth, add a provenance value, `foreign-table`, to the format's list, which a reader keeps when it does not know it.  For the fifth, read `pg_constraint.convalidated` for the `NOT NULL` constraints of the tables asked for, on PostgreSQL 18 and later, and answer a column whose constraint is not validated as `true` with `catalog`.

## Trigger

A user who needs the modifier of an extension's type or an array's element in the sidecar; phase 5, whose models read `nullable`, for the fifth.
```

Create `docs/tech-debt/TD-0045-the-check-against-npgsql-is-best-effort.md`:

```markdown
# TD-0045 - The check against Npgsql is best effort, and some types cannot be bound

## Problem

The PostgreSQL describer prepares a query with `$n` where SqlSource's lexer found a parameter, and then asks Npgsql to describe the SQL as written, to prove that Npgsql finds the same parameters ([`NpgsqlCheck`](../../src/SqlSource.Tool/Describing/Postgres/NpgsqlCheck.cs), [`PostgresLive.CheckAsync`](../../src/SqlSource.Tool/Describing/Postgres/PostgresLive.cs)).  It compares the columns of the two results.  Six things about it are not whole:

- **A disagreement that leaves the columns alone passes.**  If the two read a `@` differently inside a predicate, and the server accepts both readings, the result has the same columns and the check says `same`.
- **A `$n` that the SQL writes itself passes.**  `SELECT 1 FROM users WHERE id = @id AND name = $2` is described by both: Npgsql sends `$1` for `@id`, leaves `$2`, and the server types it.  At run time nothing binds it.  This is the first item of [TD-0043](TD-0043-small-gaps-of-the-postgresql-describers-parameters.md).
- **Npgsql's `:name` form.**  Npgsql reads `:id` as a parameter when the command has one of that name, and SqlSource does not read the form.  A query that writes both `@id` and `:id` for one parameter is one SqlSource prepares as a syntax error, `SQLSRC225`, so the check never sees it; it is listed because the two readers differ there and nothing but the server's refusal covers it.
- **An array of a domain that is returned.**  Npgsql sends a parameter of `email_t[]` as `character varying[]`, and the server keeps the domain.  `SELECT @p AS p` is `SQLSRC231`, rightly, with a help line that speaks of a `@`.
- **Three kinds of type cannot be bound by their spelled name**, and a query with such a parameter is `SQLSRC232`: a type whose name or schema needs quotes, since Npgsql takes `My Schema.Status` and not `"My Schema"."Status"`; `"char"`, which the server gives a parameter that is compared with `pg_class.relkind`; and a domain over a domain, for which Npgsql says `Writing is not supported`.
- **A refusal costs a round trip for each parameter.**  Npgsql throws the same two exceptions for a type it cannot bind and for a text it will not send, so the live function asks again for each parameter alone to tell which.

## Why it exists

The check was specified as a comparison of columns because that is what a describe gives: Npgsql does not say which placeholders it found.  The spelled name is the sidecar's, chosen in sub-phase 3.2 to be what PostgreSQL writes, and Npgsql's `DataTypeName` has a syntax of its own, without quotes.  The round trips are on the path of a failure alone.

## Impact

Low today, since the describer is registered nowhere until sub-phase 3.6 and nothing binds by `DataTypeName` until phase 8.  The first two let a query through that fails when it runs, with the driver's error and no word of SqlSource's.  The fifth refuses queries that are valid: a project whose schema has a quoted name cannot declare a parameter of one of its types, and must cast a `text` parameter in the SQL.

## Proposed fix

For the first two, compare the parameters and not only the columns: after the check's describe, read the parameter types of the statement Npgsql prepared, which the protocol's `ParameterDescription` holds, and compare their count with the query's list; or refuse a `$` followed by a digit in a `Text` lexeme before anything is sent.  For the fourth, give `SQLSRC231` a second help line when what differs is a type and a parameter is an array of a domain.  For the fifth, decide in phase 8 how generated code names a type to Npgsql, and let the sidecar hold that: a second member beside `name`, the unquoted `schema.name` form that Npgsql takes, and for `"char"` and a domain over a domain the base type's name with a cast in the SQL the generator emits.  The sixth needs none.

## Trigger

Phase 8, whose generated code binds a parameter by the sidecar's `name`; sub-phase 3.6, which registers the describer and gives `SQLSRC232` to users.
```

- [ ] **Step 4: Update `TD-0043` and the table**

In `docs/tech-debt/TD-0043-small-gaps-of-the-postgresql-describers-parameters.md`:

- In **Problem**, the first bullet gains a sentence at its end: `Sub-phase 3.3's check of the lexer against Npgsql does not see it either: Npgsql sends `$1` for `@id` and leaves the `$2`, the server types it, and the two describes agree.`
- In **Why it exists**, `, and sub-phase 3.3's check of the lexer against Npgsql is the place that sees a query whose parameters the two read differently` becomes `; the check of the lexer against Npgsql was hoped to see it and does not, since it compares the columns of two describes ([TD-0045](TD-0045-the-check-against-npgsql-is-best-effort.md))`.
- In **Proposed fix**, `, or let sub-phase 3.3's check report it` is removed.
- In **Trigger**, `Sub-phase 3.3, whose check of the lexer against Npgsql meets the first; phase 8, whose generated code binds a parameter by the sidecar's `name`, for the second;` becomes `Phase 8, whose generated code sends the SQL as written, for the first, and binds a parameter by the sidecar's `name`, for the second;`.

In `docs/tech-debt/README.md`, `Next id: \`TD-0044\`` becomes `Next id: \`TD-0046\``, and two rows are added at the end of the table of active items:

```markdown
| [TD-0044](TD-0044-small-gaps-of-the-postgresql-describers-columns.md) | Open | 2026-10-13 | Low | The PostgreSQL describer records no modifier for a type of an extension, for an array's element or for the fields of an interval, says `view` for a foreign table, and believes a `NOT NULL` that is `NOT VALID` |
| [TD-0045](TD-0045-the-check-against-npgsql-is-best-effort.md) | Open | 2026-10-13 | Low | The check of the lexer against Npgsql compares columns alone, so a `$n` of the SQL's own and a disagreement in a predicate pass, and a type with a quoted name, `"char"` and a domain over a domain cannot be bound, `SQLSRC232` |
```

If the row of `TD-0043` in the table says that sub-phase 3.3 may report the `$n`, bring it in line with the item.

- [ ] **Step 5: Close the row of the outline, and record what the spike found**

In `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md`:

- The row that begins `| 3.3 Columns, the catalog and the gate | In progress |` becomes `| 3.3 Columns, the catalog and the gate | Done |`.
- In the list under `### Settled`, after the bullet that begins `**`format_type(oid, -1)` of `char` is `bpchar`.**`, add:

```markdown
- **The columns come from Npgsql's `GetColumnSchema` without `KeyInfo`, with three decodings.**  Sub-phase 3.3's spike found that the schema is filled from the row description alone and that the server is asked nothing more.  Npgsql gives no scale for a `numeric` whose scale is zero, a negative scale 2048 too high, and the precision 65535 for an interval with fields and no precision; and it gives an array the numbers of its element, which are dropped.  `EXECUTE` is described whatever the number of its arguments, and a statement described so, an `INSERT ... RETURNING` for one, is not run.
- **An array of a domain is not flattened by the protocol**, and a domain is, to the base at the end of its chain.  So a column of `email_t[]` is recorded as it is with no help from its origin, and a column of `email_t` is the domain only through its origin.
- **The check tells a type Npgsql cannot bind from a text it will not send by asking again.**  Npgsql throws a `NotSupportedException` or an `InvalidCastException` for both and sends nothing, so 3.3 asks for each parameter alone with a text of its own.  Three kinds of type are refused by their spelled name: one that needs quotes, `"char"`, and a domain over a domain; `SQLSRC232` is therefore reachable from a `.sql` file, and phase 8 decides how generated code names a type to Npgsql.
- **The check does not see a `$n` that the SQL writes itself**: both describes accept it.  It compares columns, and nothing gives the placeholders Npgsql found.
```

- [ ] **Step 6: Validate and commit**

Run: `./format.sh`, then `./pre-commit-validation.sh`.
Expected: every step `passed`.

```bash
git add src/SqlSource.Tool/AGENTS.md docs/tech-debt docs/superpowers/specs/2026-10-07-query-generation-epic-design.md
```

```bash
git commit -m "Document the PostgreSQL describer's columns, and close sub-phase 3.3

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 7: Check the version before the pull request**

Run: `git fetch --tags origin`, then `git tag --list 'v*' --sort=-v:refname | head -n 1`
Expected: no output.  The repository had no `v*` tag when this plan was written, so there is nothing to compare `VersionPrefix` with.  If a tag is printed, compare it with `VersionPrefix` in `Directory.Build.props`, and if `VersionPrefix` is not greater, ask the user what the new version should be and set it in this pull request.

The pull request's description ends with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.  Reply to every CodeRabbit finding in its thread, as `AGENTS.md` requires.
