# Query type discovery - design

Date: 2026-10-07

Research for the query models feature: generated input and output types for each query, in the manner of sqlc.  This document answers one question of that feature, and is the input to its full design: given a query, how does SqlSource find the types of its parameters and of the columns of its result set, reliably, for PostgreSQL and SQL Server first and other databases later?

It compares the two ways this is done, recommends one, and describes the recommended one in enough detail for the feature design to build on: what is asked of each database, what comes back, what cannot come back and what to do about it, the shape of the result, and how it reaches the generator.

## Goal

A query such as

```sql
-- name: GetUser
SELECT id, name, created_at, deleted_at
FROM users
WHERE id = @id;
```

yields enough to generate

```csharp
public sealed record GetUserParameters(int Id);
public sealed record GetUserRow(int Id, string Name, DateTime CreatedAt, DateTime? DeletedAt);
```

For that, each parameter needs a name, a database type, a C# type and whether it may be null; each result column needs the same, plus its ordinal and, when it is a column of a table, which table and column, which the feature design can use to share a type between queries that read the same columns.

Success is:

- The types are the database's own answer, not an approximation of it: a query that the database accepts is described, and the described types are the ones the driver hands back at run time.
- A column that can be null in a result is nullable in the model.  A false nullable is a nuisance; a false non-nullable is a silent wrong value, because Dapper leaves a member at its default when the column is `NULL`.
- A project that does not use the feature builds exactly as it does today, with no database and no new tool.
- A second database engine is a new implementation of one interface and one table of type mappings, not a change to the generator's pipeline.

## Scope

In scope, from the owner:

- One result set per query: a `SELECT`, or an `INSERT`, `UPDATE`, `DELETE` or `MERGE` with `RETURNING` or `OUTPUT`.  A statement without a result set yields parameters only.
- A select list that is visible in the query.  Stored procedures and table-valued functions are not called to find their shape.
- PostgreSQL and SQL Server.  The design must leave room for the other dialects SqlSource already lexes.

Out of scope for this document, left to the feature design:

- The C# that is generated: the shape of the models, naming, how a query's method takes and returns them, Dapper or raw ADO.NET.
- Whether models are generated for every query or only on request.
- Queries with `{{tokens}}`.  See Open questions.

## The two approaches

Every tool that types SQL at build time does one of two things.  It either parses the SQL and the schema itself and reasons about types with its own engine, or it hands the SQL to a running database and asks it.

### A: static analysis, in SqlSource

Parse the schema's DDL into a catalog, parse the query, resolve every name and every expression against the catalog and against a table of the engine's functions, operators and casts, and derive nullability from the schema and the query's joins.  This is what sqlc, SQLDelight and Rezoom.SQL do.  The research looked at each.

**What it takes.**  Four parts, for each engine.

1. *A parser for the dialect.*  SQL Server has one: `Microsoft.SqlServer.TransactSql.ScriptDom`, MIT since 2023, a 7 MB `netstandard2.0` assembly with no dependencies, and no name or type binding.  PostgreSQL has none that is managed.  The bindings of libpg_query for .NET (Npgquery, pgsqlparser, PostgresQuery) all bundle a native library and none targets `netstandard2.0`; the ANTLR grammar in grammars-v4 is 136 KB, derived from `gram.y`, and its README says it is ambiguous.  A native library cannot be loaded by a source generator, which runs inside the compiler and the IDE, on .NET Framework in Visual Studio, from a package folder that has no convention for native assets.  sqlc's own history is the measure of the alternative: in 2026 its maintainers replaced every parser they depended on with hand-written ones to escape cgo, and the PostgreSQL one, oliphant, is 1.2 MB of Go validated against 308,561 cases from a C oracle.
2. *A catalog.*  Tables and columns from the project's migrations, which means parsing DDL too, including `ALTER`.  Views need their defining query bound.  sqlc reads a directory of migration files for this.
3. *A binder.*  Name resolution through aliases, joins, subqueries, CTEs, views and `SELECT *`; the type of every expression; and the type of every parameter from the context it is used in.  The expression typing is the open-ended part.  PostgreSQL's catalog has 3,439 functions, 102 of them polymorphic, 805 operators and 249 casts, and resolves an operator by a six-step algorithm over implicit casts, preferred types and type categories; aggregates change type, `sum(integer)` is `bigint`.  sqlc ships a 245 KB file of PostgreSQL functions generated from a live server and still types a function it does not know as `any`; that literal appears eight times in its output-column code.  T-SQL has a 31-entry precedence list that decides the type of every mixed expression.
4. *Nullability.*  The schema's `NOT NULL`, outer joins, `COALESCE`, `CASE`, aggregates over no rows, set operations.  Each is a rule to write and keep right.  The open sqlc issues on `LEFT JOIN` with subqueries and user-defined types are this part.

**What it gives.**  No database at any point.  Types for a query the moment it is written, in the IDE.  And an engine that SqlSource owns, so a wrong answer is SqlSource's to fix.

**Where it fails.**  A static binder's failure is not an error.  It is a type that is wrong, or `any`, or a column that is non-nullable when the database would return `NULL`, and nothing reports it.  sqlc, after six years and a company, added a database-backed analyser in 2023 with this stated reason: "Without a database connection, sqlc does its best to parse, analyze and compile your queries just using the schema you pass it and what it knows about the various database engines it supports.  In many cases this works just fine, but for more advanced queries sqlc might not have enough information to produce good code."  Rezoom.SQL types SQL statically and completely only because it invented its own dialect and compiles it to each backend.  SQLDelight's base grammar is 22 KB because SQLite's type system is four affinities.

**Effort.**  SQL Server: the parser is free; a binder for single-table and simply joined `SELECT`, `INSERT`, `UPDATE` and `DELETE` with a few hundred hand-curated functions is a few thousand lines and would be a plausible first version, with a permanent tail of functions it does not know.  PostgreSQL: the parser alone blocks the generator; in a separate tool a native binding would serve, and the binder needs a function catalog generated from a server and regenerated for each major version.

### B: ask the database, outside the compiler

Hand the query to a running database and ask it what it would return, without executing it.  Both engines have a first-class way to do this, and it is the engine's own parser, binder and type system that answers.  A separate step runs it and writes the answer into a file that is committed beside the SQL; the generator reads the file and never touches a database.  This is what SQLx, pgtyped, Prisma TypedSQL and FSharp.Data.SqlClient do, what sqlc added, and what one .NET package, SqlBound, does.

**What it takes.**

1. A `dotnet` tool that opens a connection, describes each query and writes the snapshot.  Two engine-specific describers, each a few hundred lines over Npgsql and Microsoft.Data.SqlClient.
2. A snapshot format, and a reader for it in the generator.
3. The parts the database does not answer, which are the same for both approaches: which `@name`s are parameters, whether a parameter may be null, and a way for the user to override a nullability the database gets wrong.
4. For PostgreSQL only, result nullability, which the protocol does not report.  See Nullability, below.

**What it gives.**  The database's answer.  Every function, operator, cast, collation, domain and extension type the server knows, including the user's own.  Every syntax the server accepts.  SQL Server reports nullability itself, outer joins included.

**Where it fails.**  A database must exist when a query changes, with the schema applied.  The committed snapshot can go stale when the schema changes and the SQL does not; a check in CI catches it.  PostgreSQL's nullability needs inference.  And a query that the server cannot type on its own, `SELECT @p` with nothing to compare it with, is an error the user fixes with a cast; the static approach has the same case and answers `any`.

### Side by side

| | A: static | B: ask the database |
|----|----|----|
| Needs a database | Never | When a query or the schema changes, and in CI to check the snapshot |
| Correct for | What the binder covers | What the server accepts |
| Failure mode | Silently wrong type or nullability; `any` | An error from the server, or a stale snapshot that a CI check reports |
| PostgreSQL parser | None managed; native cannot run in the generator | The server's |
| Function and operator coverage | A generated catalog, per version, with gaps | Complete, including extensions and user functions |
| User-defined types, domains, enums, composites | Only if the DDL that defines them is parsed | Reported by the catalog |
| Nullability, SQL Server | Own rules | The engine's, outer joins included |
| Nullability, PostgreSQL | Own rules | `NOT NULL` of the origin column plus inference over the plan, as SQLx does |
| Types available in the IDE as you type | Yes | After the tool runs |
| Effort, first version | High for SQL Server, very high for PostgreSQL | Moderate for both |
| Effort, ongoing | Tracks each engine's releases | Tracks two drivers |
| Second engine | A parser, a catalog and a binder | A describer |
| Precedent | sqlc (now also B), SQLDelight, Rezoom.SQL (own dialect) | SQLx, pgtyped, Prisma, FSharp.Data.SqlClient, sqlc's analyser, SqlBound |

### Recommendation

B.  The reasons, in order of weight:

1. Correctness is the point of the feature, and only the server implements its dialect completely.  Everything else is an approximation whose errors are silent.
2. A is out of proportion to the project.  SQL Server's half is tractable; PostgreSQL's half is a parser SqlSource cannot host plus a type system it would have to regenerate for each release, and the projects that did it are companies or have narrowed the language.
3. The industry converged.  Of the tools surveyed, every one built in the last five years asks the database, and the one that started static added a database path.
4. A stays open.  The design puts the describing behind one interface and keeps the snapshot engine-neutral.  A static describer for an engine, SQLite is the plausible one, is a later implementation of that interface and changes nothing else.

The cost that B puts on the user, a database when a query changes, is one .NET projects already carry: a project with migrations has a database to run them against, and the tool can take a connection string, or start a container and run the project's migration command.  The design keeps the first in the first version and sketches the second.

## The design

### Pieces

```
.sql files ──lexer──► queries, parameters ──┐
                                            ├──► generator ──► models
<file>.sql.json (snapshot) ◄──tool───────────┘
          ▲
          └── describer (Npgsql | SqlClient) ◄── a database with the schema applied
```

| Piece | Lives in | Does |
|----|----|----|
| Parameter lexing | `SqlSource` (the generator) and the tool, shared source | Finds `@name` placeholders outside strings, comments and quoted identifiers, by the file's dialect.  Gives each query its ordered parameter list. |
| Describer | The tool, one per engine | Asks the database for parameter types, column types, nullability and column origins.  Returns a `QueryDescription`. |
| Snapshot | The repository, one file beside each `.sql` file | The descriptions of the file's queries, keyed by name, each with a hash of the SQL it describes. |
| Snapshot reader | The generator | Reads the sidecar as an `AdditionalFiles` item, matches each query by name and hash, reports stale or missing entries. |
| Type map | The generator, one per engine | Turns a database type into a C# type.  Lives in the generator, not the snapshot, so a better mapping needs no database. |

The generator stays what it is today: a pure function of the project's files.  The tool is the only thing that connects anywhere.

### Parameters: finding them

The database can type a parameter only after SqlSource has decided which `@name`s are parameters, and the generator needs the same list to build the input model, so one piece of code decides, and both the generator and the tool use it.  It is a new lexeme kind in `SqlLexer`, so it is already dialect-aware: a `@` inside a string, a comment or a quoted identifier is not a parameter under any dialect, and the lexer already knows where those are.

The rule, for both dialects in scope: `@` followed by a letter, a digit or `_`, where the `@` is not itself preceded by `@` or by a character that can be part of an identifier.  The name runs over letters, digits and `_`.  Names are compared case-insensitively, as both drivers compare them.  Ordinal is the order of first appearance.

This is Npgsql's rule for `@` with two deliberate differences.  Npgsql also takes `:name`, which SqlSource does not: `::` casts and array slices `a[1:2]` make `:` ambiguous in PostgreSQL, and Npgsql's own documentation says its rewriting "may not parse some forms of SQL correctly".  And Npgsql allows `.` in a name; SqlSource does not, since the name becomes a C# identifier.  The operators that contain `@` are safe under the rule: in `@>`, `<@`, `@@` and `@?` the `@` is followed by a symbol or another `@`.  In T-SQL, `@@ROWCOUNT` and the other system functions start with `@@` and are excluded by the same clause.

At run time the user's SQL still holds `@name`.  On SQL Server that is the native form.  On PostgreSQL, Npgsql rewrites `@name` to `$n` when the command's parameters are named, which is what Dapper produces; SqlSource's rule must therefore agree with Npgsql's for every query, or run time and describe time would see different parameters.  The tool checks this: for PostgreSQL it also lets Npgsql derive the parameters of the original SQL and compares the names and count with the lexer's, and reports a disagreement as an error on the query rather than describing it.

The parameter list is also what the generator hashes with the SQL, so a snapshot made for `@id` is stale for `@userId`.

### Describing a query on PostgreSQL

The protocol's extended query flow does this directly.  A `Parse` message with no parameter types followed by `Describe` of the statement returns, without planning or executing anything, a `ParameterDescription` with the type OID the server inferred for each `$n`, and a `RowDescription` with, for each column, its name, type OID, type modifier and, when the column is a column of a table, that table's OID and the column's number.  A statement that returns no rows answers `NoData`.  `RETURNING` lists get the same treatment as a select list.  Table origins survive subqueries in `FROM`, CTEs and joins; they are zero for expressions, function results, casts, aggregates, the merged column of a `USING` join, and the whole output of a `UNION`, `INTERSECT` or `EXCEPT`.  A view reports the view's OID and column, not the base table's.

The tool reaches this through Npgsql:

1. **Parameters.**  `NpgsqlCommand.DeriveParameters()` on the original SQL.  It sends `Parse` with no types and `Describe`, and sets each parameter's `PostgresType`, which is a resolved object: a base type, or a domain with its base, an array with its element, an enum with its labels, a range with its subtype, a composite with its fields.  Npgsql does the `@name` rewriting here, which is the comparison described above.
2. **Columns.**  `ExecuteReader(CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo)` with the parameters typed from step 1, then `GetColumnSchema()`.  `SchemaOnly` sends `Parse` and `Describe` only.  `KeyInfo` makes Npgsql join `pg_attribute` for every column that has a table origin and fill `AllowDBNull` from `attnotnull`, with `BaseSchemaName`, `BaseTableName` and `BaseColumnName`; a column with no origin keeps `AllowDBNull` null.  Each column also carries its `PostgresType`.
3. **Nullability through outer joins.**  See Nullability.

The errors to expect and what to tell the user:

| Server error | Cause | The tool reports |
|----|----|----|
| `42P18 could not determine data type of parameter $n` | The parameter is used nowhere that fixes its type: `SELECT @p`, `COALESCE(@a, @b)` | Add a cast: `@p::int`.  Or declare the type with a marker (see Overrides). |
| `42P08 inconsistent types deduced for parameter $n` | Used as two types: `int_col = @p AND text_col = @p` | Cast one use, or use two parameters. |
| Any other | The query is not valid against this schema | The server's message, at the query. |

The server resolves a polymorphic function's result to a concrete type before `Describe`, so `anyelement` never reaches the tool.  `record` and `unknown` can, from `ROW(...)` and from a bare literal in some positions; the tool reports them as unsupported with the column named.  Type OIDs are per database; the snapshot stores schema-qualified type names and the resolved structure, never an OID.

Versions: `Parse`/`Describe` is protocol 3.0, PostgreSQL 7.4.  The nullability step needs `EXPLAIN (FORMAT JSON)`, 9.0, and `plan_cache_mode`, 12, or `EXPLAIN (GENERIC_PLAN)`, 16.  CockroachDB speaks the same protocol but rejects the `EXPLAIN` form and the `SET`; its describer is the PostgreSQL one with the nullability step skipped, which SQLx also does.

### Describing a query on SQL Server

SQL Server has two system procedures for exactly this, both since SQL Server 2012 and present in Azure SQL, LocalDB and the Linux images, and documented as static analysis: the batch is parsed and bound, never run.

1. **Parameters.**  `sp_describe_undeclared_parameters @tsql` returns one row per undeclared `@name` with `suggested_system_type_name`, which includes the facets, `nvarchar(50)`, `decimal(18,2)`, `varchar(max)`, plus `suggested_max_length`, `suggested_precision`, `suggested_scale`, and `suggested_user_type_*` for alias and CLR types.  The engine takes the type from the innermost enclosing comparison, assignment, function argument, `INSERT` value or `CAST`.  It does not report nullability, and it has two limits that matter:
   - **A parameter used more than once is error 11508.**  This is common: `WHERE (@name IS NULL OR name = @name)` is the standard optional filter.  FSharp.Data.SqlClient works around it by renaming each occurrence, `@name` to `@name_1`, `@name_2`, describing, and accepting the result when every copy came back the same type.  SqlSource does the same, and needs no T-SQL parser for it: the lexer already knows every occurrence.  Copies that disagree are an error that names the parameter and the two types; a marker fixes it.
   - **A parameter nothing fixes a type for** is error 11502, 11506 or 11507.  The user adds a `CAST` or a marker.
2. **Columns.**  `sys.dm_exec_describe_first_result_set(@tsql, @params, 1)`, the table-valued function that shares the procedure's algorithm but returns a failure as a row with `error_number`, `error_message` and an `error_type` code instead of raising it.  `@params` is a declaration string built from step 1, `N'@id int, @name nvarchar(50)'`; the procedure requires every parameter declared, which is why the parameters come first.  The third argument asks for browse information, which fills `source_schema`, `source_table` and `source_column` and adds hidden key columns that the tool drops by `is_hidden = 1`.  Each column has `name`, `is_nullable`, `system_type_name` with facets, `user_type_*` for alias and CLR types, `is_identity_column` and `is_computed_column`.  A batch with no result set returns no rows.  A column with no name, `SELECT 1`, returns `NULL` for `name`; the model needs a name, so that is an error asking for an alias.

The engine's nullability is its own: an outer join's columns, `CASE`, `ISNULL` (never null), `COALESCE` (null unless every argument is non-null), and "1 if it can't be determined", which is the safe direction.

The errors to expect:

| `error_type` or number | Cause | The tool reports |
|----|----|----|
| 8, 11521 | A column's type depends on an undeclared parameter | Not reachable once step 1 declares every parameter; reported if it is. |
| 10, 11525 | A temporary table in a multi-statement batch | Temporary tables are supported in a single-statement batch only.  A query in scope is one statement. |
| 4, 11513 | `EXEC(@sql)` | Dynamic SQL cannot be described. |
| 3, 11509 | Two code paths return different shapes | One result set per query; restructure. |
| 2, 11501 | Compile error | The server's message. |

Two engine facts shape the type map: `json` (SQL Server 2025, Azure SQL) is reported as `varchar(max)` or `nvarchar(max)` by design, and `rowversion` is reported as `timestamp`.  Table-valued parameters cannot be inferred; the procedure requires scalar parameters.  They are out of the first version, and a marker would be the way in.

### Nullability

The two engines differ here, and it is the one place where the recommended approach is not simply the server's answer.

**SQL Server** reports `is_nullable` per column from its own binder, with outer joins and expressions handled, and reports nullable when it cannot tell.  The tool takes it as given.

**PostgreSQL** does not report it.  The protocol has no nullability field.  What exists:

1. For a column with a table origin, `pg_attribute.attnotnull` says whether the column itself is `NOT NULL`.  Npgsql's `KeyInfo` fetches it.  A view's columns are never `NOT NULL` in `pg_attribute`.
2. For a column with no origin, an expression, aggregate, function or set operation, nothing.  It is nullable unless the user says otherwise.
3. A `NOT NULL` column from the nullable side of an outer join is nullable in the result, and nothing in 1 says so.

Without 3, every `LEFT JOIN` produces a wrong non-nullable, which is the failure the Goal rules out.  Two ways to get 3:

- **Walk the plan.**  SQLx's approach since 2021: `EXPLAIN (VERBOSE, FORMAT JSON)` of the query, and a walk of the plan tree that marks an output as nullable when it is produced on the inner side of a `Left` join, the outer side of a `Right` join, or either side of a `Full` join.  The plan needs parameter values; SQLx prepares the statement and explains `EXECUTE stmt(NULL, NULL, ...)` under `SET plan_cache_mode = force_generic_plan` so the plan is not specialised for the nulls.  PostgreSQL 16 adds `EXPLAIN (GENERIC_PLAN)`, which takes the `$n` form directly and needs neither.  SQLx matches plan outputs to result columns by the text of the output expression, and has one open bug where the planner turned a `LEFT JOIN` into a `Hash Right Join` and the walk marked the wrong side.  Matching by origin relation instead of output text, which the `RowDescription` and the plan's `Relation Name` and `Alias` both give, is the more robust form of the same idea, and handles the right-join case when the walk reads join sides correctly.
- **A keyword heuristic.**  If the lexer sees `LEFT`, `RIGHT`, `FULL` or `OUTER` as a keyword outside strings and comments, every column of the query is nullable unless overridden.  Always safe, often too conservative: the driving table's columns become nullable too.

The recommendation is both, layered: the plan walk marks what it can account for; any column whose origin relation it cannot find in the plan, and every column of a query whose plan could not be obtained, falls back to the heuristic; and the override below is the user's last word.  The plan walk is the one piece of this design with real algorithmic content, and the feature design should treat it as its own unit with its own test matrix: left, right and full joins, nested joins, joins inside subqueries and CTEs, lateral joins, a view over an outer join, and the planner's join reordering.

For both engines the generator applies the override, then the policy: a nullability the description leaves unknown is nullable.

### Overrides

The database cannot answer three things, so the user can.  Every override is written in a way that keeps the `.sql` file runnable as it is, which is SqlSource's rule for everything in a query.

| What | How | Precedent |
|----|----|----|
| A column's nullability | An alias with a suffix: `AS "name!"` is not null, `AS "name?"` is nullable.  Legal quoted identifiers on both engines, `[name!]` also on SQL Server.  The database returns the name with the suffix; the generator strips it. | SQLx, pgtyped |
| A parameter's nullability | A marker in the query: `-- param: id nullable`.  Parameters are non-nullable unless marked. | sqlc's `narg`, pgtyped's `!` in the other direction, FSharp.Data.SqlClient's all-or-nothing option |
| A parameter's type | The same marker: `-- param: id type=int`.  Passed to the describer, which declares it instead of asking, so `SELECT @p` can be described.  Needed on every engine that cannot infer parameter types, which is every engine in Other engines except DuckDB. | Prisma's `-- @param {Int} $1:name`, sqlc's `sqlc.arg` |

The marker reuses the existing `-- marker:` syntax of `name` and `summary`.  Its exact grammar is the feature design's.  Why a comment and not a suffix in the SQL for parameters: `@id?` is not valid on either server, so the file could no longer be run by the user's tools or by the describer without rewriting.

A per-column C# type override, `AS "created: DateTimeOffset"` in SQLx, is not in the first version; a project-level mapping override, below, covers the common need.

### The description

What a describer returns, and what the snapshot holds, is engine-neutral at the top and engine-specific in the type:

```
QueryDescription
    ResultKind        Rows | NoRows
    Parameters        [ { Name, Ordinal, Type: DbType?, Nullable: bool? } ]
    Columns           [ { Ordinal, Name, Type: DbType, Nullable: bool?, Origin: { Schema, Table, Column }?, IsIdentity, IsComputed } ]

DbType, PostgreSQL     { Schema, Name, Kind: Base | Array(Element) | Domain(Base) | Enum(Labels) | Range(Subtype) | Multirange(Subtype) | Composite, Facets: typmod-derived precision, scale, length }
DbType, SQL Server     { SystemTypeName with facets, MaxLength, Precision, Scale, UserType: { Schema, Name, AssemblyQualifiedName }? }
```

Three things are deliberately optional, because the engines in Other engines need them to be: a parameter's type, a nullability, an origin.  `null` means unknown.  The generator turns unknown nullability into nullable and an unknown parameter type into an error that asks for a marker.

The description holds database types and never C# types.  Mapping is the generator's, so a project upgrades SqlSource and gets a better mapping without a database, and the same snapshot serves two projects that map differently.

### The snapshot

One file beside each `.sql` file: `Users.sql` has `Users.sql.json`.  The package's props already hand every `.sql` file to the compiler; they add `**/*.sql.json` the same way, and the generator pairs the two by path.

Why a sidecar per file and not one folder of hashed files as SQLx's `.sqlx/` is: the diff of a sidecar is readable and sits next to the SQL it describes in a review; a deleted `.sql` file leaves an orphan that is obvious; and the generator already keys everything by file.  Why not one file per project: every query change would touch it, and merge conflicts would be constant.

Contents, per query, keyed by the query's name:

| Field | Holds | Why |
|----|----|----|
| `hash` | SHA-256 of the engine's name and the SQL the generator emits for the query, comments stripped, with parameters as written | The snapshot is for this text and no other.  Comments are stripped before hashing whatever `keep-comments` says, since a comment changes no type. |
| `engine` | `postgres`, `mssql`, the dialect name | A sidecar describes one engine; a file lexed as `mssql` with a `postgres` sidecar is an error. |
| `server` | The server's version string | Informational; helps when a snapshot and a server disagree. |
| `description` | The `QueryDescription` | |

Staleness is by hash, as in SQLx: an entry whose hash does not match the query's SQL is stale, and the generator reports it with the message to run the tool.  A schema change that leaves the SQL unchanged is invisible to the hash; the tool's `--check` mode, which describes into a temporary location and compares, is the CI answer, as `cargo sqlx prepare --check` and `sqlc diff` are.  Storing a fingerprint of each origin table's definition in the entry would narrow that gap further and is noted for the feature design.

What the generator does with the file:

| State | Effect |
|----|----|
| No sidecar for a `.sql` file | No models for its queries, no diagnostic.  The feature is unused. |
| Sidecar present, query missing from it | A diagnostic at the query: run the tool. |
| Entry present, hash differs | A diagnostic at the query: the snapshot is stale.  No model, so stale types never compile. |
| Entry present for a query that no longer exists | A diagnostic at the sidecar: an orphan entry. |
| Sidecar's engine differs from the file's dialect | A diagnostic at the sidecar. |

Whether a missing entry is an error or a warning, and whether models are opt-in per file or per query, are the feature design's.

### Mapping to C#

The generator holds one table per engine from the description's type to a C# type.  The rule for the defaults: **the type the driver boxes**, so that Dapper's reader assigns the value without conversion.  The research's full tables are in the Sources; the rows that need a decision:

| Database type | Default | Why, and the alternative |
|----|----|----|
| PostgreSQL `date`, `time` | `DateOnly`, `TimeOnly` | Npgsql 10 boxes these.  Npgsql 9 and earlier box `DateTime` and `TimeSpan`; a project option switches. |
| SQL Server `date`, `time` | `DateTime`, `TimeSpan` | What SqlClient boxes.  `DateOnly`/`TimeOnly` are reachable through `GetFieldValue<T>` and through Dapper 2.1.86 and later; a project option switches. |
| `timestamptz` | `DateTime` with `Kind.Utc` | What Npgsql 6 and later box.  `DateTimeOffset` is always offset zero; an option. |
| `timestamp`, `datetime2`, `datetime` | `DateTime` | Kind Unspecified on both. |
| `numeric`, `decimal`, `money` | `decimal` | Both drivers.  SQL precision 38 exceeds .NET's 28; the facets are in the description for a diagnostic later. |
| `json`, `jsonb`, `xml`, SQL Server `json` | `string` | Both drivers box `string`.  Npgsql's POCO mapping is opt-in and not AOT-safe. |
| `uuid`, `uniqueidentifier` | `Guid` | |
| `bytea`, `varbinary`, `rowversion` | `byte[]` | |
| PostgreSQL array | `T[]` of the mapped element | Npgsql's default.  An array that holds nulls needs `T?[]` for a value type and the server cannot say; a mapping override is the fix. |
| PostgreSQL domain | Its base type's mapping | The server reports the domain; the describer resolves it. |
| PostgreSQL enum | `string` | Requires `EnableUnmappedTypes()` on the data source in Npgsql 8 and later, which the documentation must say.  A project that calls `MapEnum<T>()` maps the enum's name to `T` with the override. |
| PostgreSQL range, geometric types, `interval` | `NpgsqlRange<T>`, `NpgsqlPoint` and so on, `TimeSpan` | The model references `NpgsqlTypes`, which a project that uses Npgsql has. |
| PostgreSQL composite, `record` | Unsupported in the first version | A diagnostic that names the column. |
| `sql_variant` | `object` | |
| `hierarchyid`, `geography`, `geometry` | Unsupported in the first version | Need `Microsoft.SqlServer.Types`; a diagnostic, and the override. |
| SQL Server `vector` | Unsupported in the first version | SqlClient 6.1 and later only. |
| Anything else | A diagnostic that names the type and the override | |

A nullable column is the C# type with `?`; for a value type that is `Nullable<T>`, for a reference type the annotation.  Facets, `varchar(50)`, `decimal(18,2)`, are dropped from the type, as every surveyed tool drops them, and kept in the description; the generator can put them in the member's documentation for free.

The override is a project-level mapping from a database type name to a C# type, `timestamptz` to `DateTimeOffset`, `public.status` to `MyApp.Status`.  Its MSBuild form is the feature design's; its property starts with `SqlSource`, as every property of the package does.

### Diagnostics

The feature needs a new range, `SQLSRC2xx` is free, for: a stale, missing or orphaned snapshot entry; a sidecar whose engine is not the file's dialect; an unmapped type; an unsupported type; a parameter with no type and no marker; a column with no name; two columns with the same name, which a joined `SELECT a.id, b.id` produces and a model cannot hold; a column name that is not a C# identifier after the feature design's naming transform.  The tool reports its own errors, the server's and the lexer-versus-Npgsql disagreement, at the file and line of the query, in the compiler's `path(line,col): error SQLSRCnnn:` form so that an IDE and CI both read them, with the same ids where the condition is the same.

### Other engines

The interface is sized by what the engines SqlSource already lexes can answer without executing a statement:

| Engine | Parameter types | Column types | Nullability | Origin table and column | How |
|----|----|----|----|----|----|
| PostgreSQL | Inferred by the server | Yes | `attnotnull` plus a plan walk | Yes | Above |
| SQL Server | Inferred by the server | Yes | Yes, by the engine | Yes, in browse mode | Above |
| CockroachDB | Inferred | Yes | `attnotnull`, no plan walk | Yes | The PostgreSQL describer with `EXPLAIN` off |
| MySQL, MariaDB | No: every parameter is reported as `VARCHAR` | Yes, with length and decimals | Yes, the `NOT_NULL` flag | Yes | `COM_STMT_PREPARE`, through MySqlConnector's schema API.  Parameters need markers. |
| SQLite | No | The declared type of a table column only; nothing for an expression | Only by looking the origin column up in `PRAGMA table_info` | Yes, with a build flag Microsoft.Data.Sqlite has | `sqlite3_prepare`.  SQLx simulates the bytecode to type expressions; a static describer is the realistic path here. |
| Oracle | No: the client declares | Yes, with precision and scale | Yes | No | `OCI_DESCRIBE_ONLY`, through ODP.NET.  Parameters need markers. |
| DuckDB | Inferred | Yes | No | No | `duckdb_prepare`. |

Hence the optional parameter type, the three-state nullability and the optional origin in the description, and the marker that declares a parameter's type.  A new engine is a describer, a type map, and a dialect name the snapshot's `engine` field accepts.

### The tool

A `dotnet` tool, published as its own package, the natural name is `SqlSource.Tool` with the command `sqlsource`, that references Npgsql and Microsoft.Data.SqlClient and the generator's parsing code as shared source.  The generator package stays a development dependency with nothing added.

What it needs to know is exactly what the compiler knows: which `.sql` files belong to the project and the dialect of each.  The cleanest way to get that is to ask MSBuild for the compiler's view, `dotnet msbuild <project> -getItem:AdditionalFiles -getProperty:SqlSourceDialect`, which evaluates the project with the package's own props and returns the items with their metadata as JSON; the .NET 8 SDK and later have the switches.  The alternative, a target in `SqlSource.targets` that writes a response file and runs the tool, is the fallback if evaluation alone proves not to be enough, for instance for a file that a target adds, `TD-0016`.

Commands, for the feature design to settle:

| Command | Does |
|----|----|
| `sqlsource describe <project> --connection <string>` | Describes every query of every `.sql` file the project claims, and writes or updates the sidecars.  The connection string can also come from an environment variable, never from the project file, since it holds credentials. |
| `sqlsource describe --check` | Describes into a temporary location and compares with the committed sidecars; non-zero when they differ.  For CI. |
| Later: `--container` | Starts a throwaway database with Testcontainers, `Testcontainers.PostgreSql` or `Testcontainers.MsSql`, runs a migration command the project names, describes, and stops it.  DbUp, FluentMigrator and EF Core migrations all have a command-line form, so "run this command against this connection string" covers them. |

The tool describes every query of a file in one connection, and `PREPARE`s nothing it does not clean up, so that a dev database is unchanged by a run.

## Decisions

Proposed here, for the feature design to confirm or overturn.  None was put to the owner except the first.

| Decision | Choice | Reason |
|----|----|----|
| Static or database | Ask the database, from a tool, with a committed snapshot | The owner asked for both to be researched and a recommendation made.  See Recommendation. |
| Where parameters are found | In the lexer, shared by generator and tool | One rule, dialect-aware, and the same list at describe time, at generate time and at run time; the tool checks the last against Npgsql. |
| Parameter syntax | `@name` only | Native to SQL Server; unambiguous in PostgreSQL where `:` is not. |
| Parameter nullability | Non-nullable unless marked | What sqlc, SQLx and FSharp.Data.SqlClient do.  The engine cannot say, and an optional parameter is the rarer case. |
| Column nullability override | Alias suffix `!` and `?` | Keeps the SQL runnable; two tools already use it; works on both engines. |
| Unknown nullability | Nullable | The safe direction.  Dapper leaves a non-nullable member at its default on `NULL`. |
| PostgreSQL outer joins | Plan walk by origin relation, keyword heuristic as fallback, override as last word | Without it every `LEFT JOIN` is wrong in the unsafe direction.  SQLx has run the plan walk for four years; matching by relation avoids its known bug. |
| SQL Server parameter reuse | Rename each occurrence, describe, unify | The common optional-filter pattern otherwise fails.  FSharp.Data.SqlClient's workaround, done with the lexer instead of a parser. |
| Snapshot | One `.sql.json` beside each `.sql` file, keyed by query name, hashed by SQL | Readable diffs beside the SQL; no orphan folder; the generator already keys by file. |
| Hash | Engine name plus comment-stripped SQL with parameters as written | A comment changes no type; a parameter's name is part of the model. |
| Types in the snapshot | Database types, structured, never OIDs and never C# types | OIDs are per database; the C# mapping evolves without a database. |
| Default C# types | What the driver boxes | Dapper assigns the boxed value; any other default converts. |
| Facets | In the description, not in the type | Every surveyed tool drops them from the type; the description keeps them for documentation and diagnostics. |
| Project with no sidecars | Builds as today | The feature is opt-in by the presence of a sidecar. |

## Open questions for the feature design

- **Queries with tokens.**  `SELECT * FROM {{table}}` cannot be described as written.  Options: exclude them from models in the first version; or a marker that gives a sample value for each token, `-- sample: table=users`, with the snapshot hashed over the sampled SQL.  The second is cheap once the marker syntax exists.
- **Opt-in.**  By the presence of a sidecar, by a property, by an attribute property, or per query.
- **Naming.**  `created_at` to `CreatedAt`; what to do with a name that is not an identifier, and with two columns of one name.
- **Sharing models.**  The origin fields let a query that reads exactly a table's columns use one type for that table, as sqlc does.  Whether to.
- **Which queries get a parameter model.**  One with no parameters has none; one with one parameter might take it directly.
- **The runtime side.**  Whether the generated method executes the query, with Dapper or ADO.NET, or only returns the SQL and the types.
- **A schema fingerprint in the snapshot**, to catch a schema change behind unchanged SQL without a database.

## Verification

The research verified the facts above against the sources below, with these exceptions that the feature's implementation should settle with a spike against a real server before the design relies on them:

- PostgreSQL: that `SchemaOnly | KeyInfo` together behave as described in Npgsql; that `EXPLAIN (GENERIC_PLAN)` accepts `$n` through the extended protocol, or whether the `PREPARE` and `EXECUTE (NULL, ...)` form is needed; what `Describe` reports for a domain-typed expression and for a `USING` column of a `FULL JOIN`.
- SQL Server: the exact `system_type_name` spelling for every type; behaviour with a CTE, an `OUTPUT` clause and `SELECT INTO`; whether `@params` accepts a `READONLY` table type; the inferred type of a parameter in `TOP (@n)`, `OFFSET @n ROWS`, `LIKE @p` and `IN (@a, @b)`, which the documentation does not cover and which may come back wider than expected, `varchar(8000)` or `numeric(38,19)`.
- Both: that the generated model's types match what Dapper assigns for every row of the type map, on the driver versions the project supports.

These spikes are also the seed of the feature's integration tests, which need both servers; the project already requires Docker.

## Sources

PostgreSQL: [protocol message formats](https://www.postgresql.org/docs/current/protocol-message-formats.html), [protocol flow](https://www.postgresql.org/docs/current/protocol-flow.html), [PREPARE](https://www.postgresql.org/docs/current/sql-prepare.html), [EXPLAIN](https://www.postgresql.org/docs/current/sql-explain.html), [error codes](https://www.postgresql.org/docs/current/errcodes-appendix.html), [pg_type](https://www.postgresql.org/docs/current/catalog-pg-type.html), [operator type resolution](https://www.postgresql.org/docs/current/typeconv-oper.html), [function type resolution](https://www.postgresql.org/docs/current/typeconv-func.html), [parse_target.c](https://github.com/postgres/postgres/blob/master/src/backend/parser/parse_target.c), [parse_param.c](https://github.com/postgres/postgres/blob/master/src/backend/parser/parse_param.c), [postgres.c](https://github.com/postgres/postgres/blob/master/src/backend/tcop/postgres.c).

Npgsql: [SqlQueryParser.cs](https://github.com/npgsql/npgsql/blob/main/src/Npgsql/SqlQueryParser.cs), [NpgsqlCommand.cs](https://github.com/npgsql/npgsql/blob/main/src/Npgsql/NpgsqlCommand.cs), [DbColumnSchemaGenerator.cs](https://github.com/npgsql/npgsql/blob/main/src/Npgsql/Schema/DbColumnSchemaGenerator.cs), [basic usage](https://www.npgsql.org/doc/basic-usage.html), [supported types](https://www.npgsql.org/doc/types/basic.html), [date and time](https://www.npgsql.org/doc/types/datetime.html), [enums and composites](https://www.npgsql.org/doc/types/enums_and_composites.html), release notes [6.0](https://www.npgsql.org/doc/release-notes/6.0.html), [8.0](https://www.npgsql.org/doc/release-notes/8.0.html), [10.0](https://www.npgsql.org/doc/release-notes/10.0.html).

SQL Server: [sp_describe_first_result_set](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-describe-first-result-set-transact-sql), [sys.dm_exec_describe_first_result_set](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-exec-describe-first-result-set-transact-sql), [sp_describe_undeclared_parameters](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-describe-undeclared-parameters-transact-sql), [errors 11000-12999](https://github.com/MicrosoftDocs/sql-docs/blob/live/docs/relational-databases/errors-events/includes/sql-server-2025-database-engine-events-and-errors-11000-12999.md), [SET FMTONLY](https://learn.microsoft.com/en-us/sql/t-sql/statements/set-fmtonly-transact-sql), [COALESCE](https://learn.microsoft.com/en-us/sql/t-sql/language-elements/coalesce-transact-sql), [json type](https://learn.microsoft.com/en-us/sql/t-sql/data-types/json-data-type), [data type precedence](https://learn.microsoft.com/en-us/sql/t-sql/data-types/data-type-precedence-transact-sql), [data type mappings](https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql-server-data-type-mappings), [CommandBehavior](https://learn.microsoft.com/en-us/dotnet/api/system.data.commandbehavior), [SqlCommandBuilder.DeriveParameters](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlcommandbuilder.deriveparameters), SqlClient release notes [5.1](https://github.com/dotnet/SqlClient/blob/main/release-notes/5.1/5.1.0.md), [6.0](https://github.com/dotnet/SqlClient/blob/main/release-notes/6.0/6.0.0.md), [6.1](https://github.com/dotnet/SqlClient/blob/main/release-notes/6.1/6.1.0.md).

Prior art, database-backed: SQLx [query! macro](https://docs.rs/sqlx/latest/sqlx/macro.query.html), [describe.rs](https://github.com/launchbadge/sqlx/blob/main/sqlx-postgres/src/connection/describe.rs), [PR 3541 plan_cache_mode](https://github.com/launchbadge/sqlx/pull/3541), [issue 3202](https://github.com/launchbadge/sqlx/issues/3202), [sqlx-cli](https://github.com/launchbadge/sqlx/blob/main/sqlx-cli/README.md), [offline data format](https://github.com/launchbadge/sqlx/blob/main/sqlx-macros-core/src/query/data.rs); pgtyped [actions.ts](https://github.com/adelsz/pgtyped/blob/master/packages/query/src/actions.ts), [sql files](https://pgtyped.dev/docs/sql-file); [Prisma TypedSQL](https://www.prisma.io/docs/orm/prisma-client/using-raw-sql/typedsql); FSharp.Data.SqlClient [home](https://fsprojects.github.io/FSharp.Data.SqlClient/), [DesignTime.fs](https://github.com/fsprojects/FSharp.Data.SqlClient/blob/master/src/SqlClient.DesignTime/DesignTime.fs), issues [13](https://github.com/fsprojects/FSharp.Data.SqlClient/issues/13), [34](https://github.com/fsprojects/FSharp.Data.SqlClient/issues/34), [263](https://github.com/fsprojects/FSharp.Data.SqlClient/issues/263); [SqlBound](https://www.nuget.org/packages/SqlBound).

Prior art, static: sqlc [config](https://docs.sqlc.dev/en/latest/reference/config.html), [managed databases](https://docs.sqlc.dev/en/latest/howto/managed-databases.html), [vet](https://docs.sqlc.dev/en/latest/howto/vet.html), [named parameters](https://docs.sqlc.dev/en/latest/howto/named_parameters.html), [output_columns.go](https://github.com/sqlc-dev/sqlc/blob/main/internal/compiler/output_columns.go), [resolve.go](https://github.com/sqlc-dev/sqlc/blob/main/internal/compiler/resolve.go), [seed.go](https://github.com/sqlc-dev/sqlc/blob/main/internal/engine/postgresql/seed.go), releases [1.19.0](https://github.com/sqlc-dev/sqlc/releases/tag/v1.19.0), [1.22.0](https://github.com/sqlc-dev/sqlc/releases/tag/v1.22.0), [oliphant](https://github.com/sqlc-dev/oliphant), [teesql](https://github.com/sqlc-dev/teesql), [sqlc-gen-csharp](https://github.com/DaredevilOSS/sqlc-gen-csharp); SQLDelight [JoinClauseMixin.kt](https://github.com/sqldelight/sql-psi/blob/master/core/src/main/kotlin/com/alecstrong/sql/psi/core/psi/mixins/JoinClauseMixin.kt); [Rezoom.SQL](https://github.com/rspeele/Rezoom.SQL); [ScriptDom on NuGet](https://www.nuget.org/packages/Microsoft.SqlServer.TransactSql.ScriptDom), [SqlScriptDOM](https://github.com/microsoft/SqlScriptDOM); [libpg_query](https://github.com/pganalyze/libpg_query); [grammars-v4 postgresql](https://github.com/antlr/grammars-v4/tree/master/sql/postgresql).

Other engines: MySQL [COM_STMT_PREPARE](https://dev.mysql.com/doc/dev/mysql-server/latest/page_protocol_com_stmt_prepare.html), [bug 23385](https://bugs.mysql.com/bug.php?id=23385), [mysql_stmt_param_metadata](https://dev.mysql.com/doc/c-api/8.0/en/mysql-stmt-param-metadata.html); MariaDB [result set packets](https://mariadb.com/docs/server/reference/clientserver-protocol/4-server-response-packets/result-set-packets); SQLite [column_decltype](https://sqlite.org/c3ref/column_decltype.html), [column metadata](https://sqlite.org/c3ref/column_database_name.html), [table_column_metadata](https://sqlite.org/c3ref/table_column_metadata.html); SQLx [sqlx-sqlite explain.rs](https://github.com/launchbadge/sqlx/blob/main/sqlx-sqlite/src/connection/explain.rs), [sqlx-mysql executor.rs](https://github.com/launchbadge/sqlx/blob/main/sqlx-mysql/src/connection/executor.rs); Oracle [OCI statement functions](https://docs.oracle.com/en/database/oracle/oracle-database/19/lnoci/statement-functions.html), [DBMS_SQL](https://docs.oracle.com/en/database/oracle/oracle-database/19/arpls/DBMS_SQL.html); [DuckDB prepared statements](https://duckdb.org/docs/current/clients/c/prepared.html).

Dapper and Roslyn: [DefaultTypeMap.cs](https://github.com/DapperLib/Dapper/blob/main/Dapper/DefaultTypeMap.cs), [PR 2228 DateOnly](https://github.com/DapperLib/Dapper/pull/2228), [incremental generators cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md), [analyzer banned symbols](https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/Core/AnalyzerBannedSymbols.txt), [Testcontainers modules](https://dotnet.testcontainers.org/modules/).
