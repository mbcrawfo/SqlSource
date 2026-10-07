# Query models - epic outline

Date: 2026-10-07

This outline coordinates the phases of the epic.  It is kept current until the epic closes: a phase that changes the plan updates it in the same pull request.

## Goal

Give each query a typed input and a typed output, found from the database itself, in the manner of sqlc.

```sql
-- name: GetUser
-- summary: Loads one user by id.
SELECT id, name, created_at, deleted_at
FROM users
WHERE id = @id;
```

```csharp
[SqlQueries]
public partial class UserRepository(IDbConnection connection)
{
    // Generated beside Sql.GetUser:
    //   public sealed record GetUserParameters(int Id);
    //   public sealed record GetUserRow(int Id, string Name, DateTime CreatedAt, DateTime? DeletedAt);
    public Task<GetUserRow?> Get(int id) =>
        connection.QuerySingleOrDefaultAsync<GetUserRow>(Sql.GetUser, new GetUserParameters(id));
}
```

The types come from a new command-line tool, `sqlsource`, which asks a running database to describe each query and writes the answer into a file beside the `.sql` file.  The generator reads that file and never touches a database.  A build reports a file that is out of date with its SQL, so a wrong type never compiles silently.

## How to read this document

The epic was designed before its phases.  Each statement about a phase is one of three kinds:

- **Decided.**  Agreed with the project owner.  A phase spec may add detail and must not contradict it.  Changing one means asking the owner and updating this outline.
- **Recommended.**  The epic designer's proposal for a question that was raised and not settled.  A phase spec settles it, and this outline is updated to record what was settled.
- **Technical notes.**  Facts about the databases, their drivers, Roslyn, MSBuild and this repository that constrain the design.  Most were verified against the sources listed at the end; the ones that were not are marked, and the phase that depends on one verifies it first.

## Scope

Decided:

- One result set per query: a `SELECT`, or an `INSERT`, `UPDATE`, `DELETE` or `MERGE` with `RETURNING` or `OUTPUT`.  A statement without a result set gets an input type only.
- A select list that is visible in the query.  Stored procedures and table-valued functions are not called to find their shape.
- PostgreSQL and SQL Server.  The design leaves room for every dialect SqlSource lexes, and the parts that would differ for them are named.
- Extensive end-to-end tests, one project per supported database, that run the tool, build with the generator and execute the generated code against a real database in a container.

## Phases

| Phase | Status | Spec | Delivers |
|----|----|----|----|
| 1. Parameters | Not started | | The lexer finds `@name` parameters by the file's dialect; each query carries its ordered parameter list and the hash of its SQL.  Nothing a user sees changes. |
| 2. The tool and the snapshot | Not started | | The `SqlSource.Tool` package: the `sqlsource describe` command with `--check` and `--force`, project evaluation through MSBuild, the snapshot format with its shared reader and writer, and the PostgreSQL describer with nullability inference.  Publishing covers the second package. |
| 3. Models | Not started | | The generator reads snapshots, maps PostgreSQL types to C#, emits the input and output types with documentation, and reports stale, missing and mismatched snapshots.  The PostgreSQL end-to-end project.  The first usable release. |
| 4. SQL Server | Not started | | The SQL Server describer, its type map, and its end-to-end project. |
| 5. Build integration | Not started | | The online mode: an MSBuild target runs the tool before compile when a connection string is in the environment.  A watch mode.  The package-install check runs the tool. |

Each phase has its own spec, plan and pull request.  Phase 1 changes nothing a user can see.  Phase 2 ships a tool whose output nothing reads yet; its package is published so that phase 3 can be tried against it.  Phase 3 is the first release a user can use, for PostgreSQL.  Phase 4 adds SQL Server.  Phase 5 removes the need to run the tool by hand.

The order puts the parameter lexeme first because every later phase depends on it and it is small; the tool before the generator because the generator's models cannot be tested without a snapshot to read; PostgreSQL before SQL Server because its nullability inference is the hardest single piece and should be proven early; and the build integration last because it is convenience over a working whole.

## Decisions for the whole epic

### Approach

Decided: the types are found by asking the database, from a tool that runs outside the compiler, and are committed to the repository in a file beside each `.sql` file.  The alternative, parsing the schema and the query and inferring the types in SqlSource, was researched to the same depth and rejected.  The reasons, in order of weight:

1. Only the server implements its dialect completely.  Every function, operator, cast, domain, extension type and syntax the server knows is covered; a static binder covers what was written for it, and its failures are silent wrong types, not errors.
2. The static approach is out of proportion to the project.  SQL Server has a managed parser; PostgreSQL has none, and a native one cannot load inside a source generator.  Either engine's binder needs a catalog of thousands of functions regenerated for each release.  sqlc, after six years, added a database-backed analyser because "for more advanced queries sqlc might not have enough information to produce good code".
3. Every tool of this kind built in the last five years asks the database: SQLx, pgtyped, Prisma TypedSQL, SqlBound, and sqlc's analyser.
4. The static approach stays open.  Describing sits behind one interface and the snapshot is engine-neutral; a static describer for an engine that cannot answer, SQLite is the plausible one, is a later implementation of that interface.

Decided: the generator keeps the emission.  A tool that emitted C# itself was weighed and rejected: generated code would land in the repository and go stale silently, every change to naming, nullability policy or the type map would need a database run to apply, and the constants, token methods and diagnostics the generator already produces would be duplicated or moved.  With the split, stale types are a build error, the snapshot is a small diffable file, and the mapping evolves with the package.

### Packages and repository

Decided:

- The tool is a normal .NET console application in `src/SqlSource.Tool`, packed as a .NET tool with `PackAsTool` and the command name `sqlsource`, published as the package `SqlSource.Tool`.  It targets `net8.0` with `RollForward` set to `Major`.
- The tool references the generator project directly, and the generator adds `InternalsVisibleTo` for it, as it has for the test projects.  The lexer, the file parser, the dialect rules, the snapshot reader and writer, the hashing and the diagnostic descriptors are shared this way.  No library package is split out; the generator's package is unchanged.  A `SqlSource.Core` library is the refactor to make if a third consumer appears.
- Both packages carry the same `VersionPrefix` and are published together by the same workflow.
- The generator package stays a development dependency with one DLL under `analyzers/dotnet/cs` and nothing under `lib/`.

Technical notes:

- A tool package carries its whole dependency closure in its own folder, so the generator DLL and Roslyn travel with it without packaging work.  The tool gets `Microsoft.CodeAnalysis` transitively; the parser uses `TextSpan` from it.
- The generator cannot take a JSON library.  `System.Text.Json` in a `netstandard2.0` analyzer collides with the compiler's own copy, and Newtonsoft is a dependency the package cannot ship cleanly.  The snapshot reader is a small hand-written JSON parser, and the writer is hand-written too so that the two are tested together.  The format stays within what such a parser handles comfortably: objects, arrays, strings, integers, booleans and null.
- The tool evaluates the project by running `dotnet msbuild <project> -getItem:AdditionalFiles -getProperty:SqlSourceDialect` as a subprocess, which returns the compiler's view of the files with their metadata as JSON, after the package's own props and targets have run.  The switches exist from the .NET 8 SDK.  Hosting MSBuild in-process was rejected: it needs `MSBuildLocator` and matches SDK versions by hand.  Whether evaluation alone sees a file that a target adds, `TD-0016`, is for phase 2 to verify; a target in `SqlSource.targets` that writes a response file for the tool is the fallback.
- MSBuild in Visual Studio runs on .NET Framework; shipping the describe step as an MSBuild task instead of a tool would mean building it twice and loading Npgsql inside MSBuild.  A tool invoked by a target is the lower-risk shape.

### Workflow

Decided:

- `sqlsource describe <project>` describes every query of every `.sql` file the project claims and writes or updates the sidecars.  A query whose hash and versions match its entry is skipped.
- `sqlsource describe --force` describes every query again whether or not its entry is up to date.  It is also the command to run after a schema change, since the hash cannot see one.
- `sqlsource describe --check` describes into a temporary location and compares with the committed sidecars, and exits non-zero when they differ.  It is the CI step, as `cargo sqlx prepare --check` and `sqlc diff` are.
- The connection string comes from a command-line option or an environment variable, never from the project file, since it holds credentials.

Recommended:

- The environment variable is `SQLSOURCE_CONNECTION`.  Environment variables are not MSBuild properties and need not carry the `SqlSource` prefix, but the name should still make the owner obvious.
- The tool is installed through a local tool manifest, so its version is pinned per repository and `dotnet tool restore` gets it, the way this repository pins CSharpier.
- Phase 5's online mode: a target in `SqlSource.targets` runs `dotnet sqlsource describe` before `CoreCompile` when the variable is set and the build is not a design-time build.  The tool hashes before it connects, so a build with nothing changed costs nothing.  Without the variable the build is offline and the committed snapshot is used, which is what CI and a machine without a database get.  This is SQLx's online and offline split.
- Phase 5's `--watch` describes a file as it is saved, for people who want the IDE to update without a build, as pgtyped's watch mode does.
- Starting a throwaway database with Testcontainers and running the project's migration command, then describing against it, is a later addition to the tool and not in this epic.  DbUp, FluentMigrator and EF Core migrations all have a command-line form, so "run this command against this connection string" would cover them.

### Output of the tool

Decided: the tool reports errors in the compiler's format, `path(line,col): error SQLSRCnnn: message`, with the same ids as the generator where the condition is the same, so that the online mode's errors reach the IDE's error list and a terminal shows what the build would.

Recommended: exit code zero on success, one when a query could not be described, two when `--check` found a difference.  Command-line parsing with System.CommandLine, which gives help and exit codes for free; the commands are few enough that a hand-written parser would also do.

### The sidecar

Decided:

- One file beside each `.sql` file: `Users.sql` has `Users.sql.json`.  The package's props include `**/*.sql.json` as `AdditionalFiles` the way they include `.sql` files, and the generator pairs the two by path.
- The file carries two version tags: a format version, which is the compatibility contract between tool and generator, and the version of the tool that wrote it, so that a tool and a generator that are out of step are detected.
- Each query's entry carries a hash of the SQL it describes.  An entry whose hash does not match the query is stale.

Recommended:

- A per-file sidecar rather than one folder of hashed files as SQLx's `.sqlx/` is, because the diff is readable beside the SQL it describes, a deleted `.sql` file leaves an orphan that is obvious, and the generator already keys everything by file.  Not one file per project, because every query change would touch it and merge conflicts would be constant.
- The hash is SHA-256 over the engine name and the SQL the generator emits for the query, comments stripped whatever `keep-comments` says, with parameters as written.  A comment changes no type; a parameter's name is part of the input type.
- The format version is an integer.  The generator reads the versions it knows; a higher one is an error that says to update SqlSource, a lower one an error that says to run the tool.  A tool version that differs from the generator's while the format matches is a warning, since the two are released together and the snapshot is still readable.  The tool rewrites an entry whose tool version is not its own, so `describe` after an update refreshes everything without `--force`.
- Entries are keyed by query name, in the file's order, so a diff follows the `.sql` file.
- A schema fingerprint per origin table, to catch a schema change behind unchanged SQL without a database, is not in this epic.  `--check` in CI is the answer the epic gives.

What the generator does with a sidecar:

| State | Effect |
|----|----|
| No sidecar for a `.sql` file | No models for its queries, no diagnostic.  The feature is unused for that file. |
| Sidecar present, query missing from it | An error at the query: run the tool. |
| Entry present, hash differs | An error at the query: the snapshot is stale.  No model is emitted, so stale types never compile. |
| Entry present for a query that no longer exists | A warning at the sidecar: an orphan entry. |
| Sidecar's engine differs from the file's dialect | An error at the sidecar. |
| Format version unknown, or tool version differs | As above under Recommended. |

Recommended: a file opts in by having a sidecar.  No property or attribute setting is needed, and a project that never runs the tool builds exactly as today.  Whether a missing entry should be a warning instead of an error, so that a new query compiles as a constant until the tool runs, is for phase 3 to settle; the error is the safer default.

### Parameters

Decided: the lexer finds parameters, so that one dialect-aware rule gives the same list to the tool, the generator and the hash.  A `@` inside a string, a comment or a quoted identifier is not a parameter under any dialect, and the lexer already knows where those are.

Recommended, the rule for both dialects in scope: `@` followed by a letter, a digit or `_`, where the `@` is not preceded by `@` or by a character that can be part of an identifier.  The name runs over letters, digits and `_`.  Names are compared ignoring case, as both drivers compare them.  Ordinal is the order of first appearance.  `@name` only: Npgsql also takes `:name`, which SqlSource does not, because `::` casts and array slices `a[1:2]` make `:` ambiguous and Npgsql's own documentation says its rewriting "may not parse some forms of SQL correctly".  The operators that contain `@` are safe under the rule, `@>`, `<@`, `@@`, `@?`, as are T-SQL's `@@ROWCOUNT` and the other system functions.

Technical notes:

- At run time the SQL still holds `@name`.  On SQL Server that is native.  On PostgreSQL, Npgsql rewrites `@name` to `$n` when the command's parameters are named, which is what Dapper produces.  SqlSource's rule must therefore agree with Npgsql's for every query, and the tool checks it: for PostgreSQL it lets Npgsql derive the parameters of the original SQL and compares names and count with the lexer's, and reports a disagreement as an error on the query.
- A query with `{{tokens}}` cannot be described as written.  Recommended: such queries get no models in this epic and no diagnostic for it.  A marker that gives a sample value for each token, with the snapshot hashed over the sampled SQL, is the later path and is recorded under Out of scope.

### Nullability

Decided: a nullability the description leaves unknown is nullable.  A false nullable is a nuisance; a false non-nullable is a silent wrong value, because Dapper leaves a member at its default when the column is `NULL`.

Decided: parameters are non-nullable unless the user says otherwise.  The database cannot report it, and sqlc, SQLx and FSharp.Data.SqlClient all default this way.

Recommended, the overrides, each written so that the `.sql` file stays runnable as it is:

| What | How | Precedent |
|----|----|----|
| A column's nullability | An alias with a suffix: `AS "name!"` is not null, `AS "name?"` is nullable.  Legal quoted identifiers on both engines; `[name!]` also on SQL Server.  The database returns the name with the suffix and the generator strips it. | SQLx, pgtyped |
| A parameter's nullability | A marker in the query: `-- param: id nullable`. | sqlc's `narg`, pgtyped's `!` in the other direction |
| A parameter's type | The same marker: `-- param: id type=int`.  The tool declares it instead of asking, so `SELECT @p` can be described.  Every engine outside the two in scope except DuckDB needs this for every parameter. | Prisma's `-- @param {Int} $1:name`, sqlc's `sqlc.arg` |

A suffix on the parameter in the SQL, `@id?`, was rejected: it is valid on neither server, so the file could no longer be run by the user's tools or by the describer without rewriting.  The marker's exact grammar is phase 1's to settle, since the lexer reads markers.

### Types in C#

Recommended: the default C# type for a database type is **the type the driver boxes**, so that Dapper assigns the value without conversion.  The rows that need a decision:

| Database type | Default | Why, and the alternative |
|----|----|----|
| PostgreSQL `date`, `time` | `DateOnly`, `TimeOnly` | Npgsql 10 boxes these.  Npgsql 9 and earlier box `DateTime` and `TimeSpan`; a project option switches. |
| SQL Server `date`, `time` | `DateTime`, `TimeSpan` | What SqlClient boxes.  `DateOnly` and `TimeOnly` are reachable through Dapper 2.1.86 and later; a project option switches. |
| `timestamptz` | `DateTime` with `Kind.Utc` | What Npgsql 6 and later box.  `DateTimeOffset` is always offset zero; an option. |
| `timestamp`, `datetime2`, `datetime` | `DateTime` | Kind Unspecified on both. |
| `numeric`, `decimal`, `money` | `decimal` | Both drivers.  SQL precision 38 exceeds .NET's 28; the facets are kept in the description for a later diagnostic. |
| `json`, `jsonb`, `xml`, SQL Server `json` | `string` | Both drivers box `string`.  Npgsql's POCO mapping is opt-in and not AOT-safe. |
| `uuid`, `uniqueidentifier` | `Guid` | |
| `bytea`, `varbinary`, `rowversion` | `byte[]` | |
| PostgreSQL array | `T[]` of the mapped element | Npgsql's default.  An array that holds nulls needs `T?[]` for a value type and the server cannot say; a mapping override is the fix. |
| PostgreSQL domain | Its base type's mapping | The server reports the domain; the describer resolves it. |
| PostgreSQL enum | `string` | Requires `EnableUnmappedTypes()` on the data source in Npgsql 8 and later, which the README must say.  A project that calls `MapEnum<T>()` maps the enum's name to `T` with the override. |
| PostgreSQL range, geometric types, `interval` | `NpgsqlRange<T>`, `NpgsqlPoint` and so on, `TimeSpan` | The model references `NpgsqlTypes`, which a project that uses Npgsql has. |
| PostgreSQL composite, `record` | Unsupported in this epic | A diagnostic that names the column. |
| `sql_variant` | `object` | |
| `hierarchyid`, `geography`, `geometry` | Unsupported in this epic | Need `Microsoft.SqlServer.Types`; a diagnostic, and the override. |
| SQL Server `vector` | Unsupported in this epic | SqlClient 6.1 and later only. |
| Anything else | A diagnostic that names the type and the override | |

- A nullable column is the C# type with `?`: `Nullable<T>` for a value type, the annotation for a reference type.
- Facets, `varchar(50)` and `decimal(18,2)`, are dropped from the type, as every surveyed tool drops them, and kept in the description; the generator puts them in the member's documentation.
- The override is a project-level mapping from a database type name to a C# type, `timestamptz` to `DateTimeOffset`, `public.status` to `MyApp.Status`.  Its MSBuild form is phase 3's; its property starts with `SqlSource`, as every property of the package does.  A per-column C# type override, SQLx's `AS "created: DateTimeOffset"`, is not in this epic.
- The type map lives in the generator and never in the snapshot, so a better mapping needs no database, and one snapshot serves two projects that map differently.

### Generated types

Recommended, for phase 3 to settle:

- For a query `GetUser`: `GetUserParameters` when it has parameters, and `GetUserRow` when it has a result set.  Both `sealed record` types with positional parameters in the query's order, which gives value equality, `with`, and deconstruction, and which Dapper constructs when the constructor's parameters match the columns in order by name and type.
- They are nested in the attributed type in both modes, since in `Nested` mode the `Sql` class is private and a private nested type cannot appear in the signature of a method of the containing type.  Their accessibility is the type's own, which the phase spec confirms against how `Direct` mode exposes members.
- Column and parameter names become property names by PascalCasing: `created_at` to `CreatedAt`.  A name that is not an identifier after that, or two columns with one name, which `SELECT a.id, b.id` produces, is an error that asks for an alias.
- Dapper matches `created_at` to `CreatedAt` only when `DefaultTypeMap.MatchNamesWithUnderscores` is true.  The README says so.  A generated reader that maps by ordinal with the typed getters, which would make Dapper's conventions and version irrelevant, is recorded under Out of scope as the candidate for a later epic.
- Each generated type and member has XML documentation: the query's summary, the database type and facets of each member, and the origin table and column when there is one.

### Diagnostics

Recommended: a new range, `SQLSRC2xx`, for the epic: a stale, missing or orphaned snapshot entry; a sidecar whose engine is not the file's dialect; version mismatches; an unmapped type; an unsupported type; a parameter with no type and no marker; a column with no name; two columns with one name; a name that is not an identifier.  The tool uses the same ids for the same conditions and its own ids for the server's errors and the lexer-versus-Npgsql disagreement.  `docs/diagnostics.md` gains each one in the phase that adds it.

### Testing

Decided: one end-to-end test project per supported database, each with its own `.sql` files, a schema script, and **committed sidecars** so that the project compiles with the generator like any consumer.  Each runs against a Testcontainers instance with the schema applied and has three kinds of test:

1. The tool in `--check` mode against the container, asserting that the committed sidecars are what the tool produces today.  This proves the describer.
2. Every generated query executed through Dapper, asserting the typed results, nullability included.  This proves the type map at run time.
3. The error paths, a stale sidecar and an undescribable query, by running the tool against a scratch copy.

Recommended:

- The projects live under `tests/`, one per database, named for it.  They reference the generator the way `tests/SqlSource.Tests` does and the tool as a project.  Docker is already a required tool, so Testcontainers adds nothing to the setup; `CONTRIBUTING.md` says which images the tests pull.
- The tool's own unit tests, the snapshot round trip, the hash, the parameter lexeme and the type map need no database and live with the existing tests.
- The nullability inference for PostgreSQL gets a test matrix of its own: left, right and full joins, nested joins, joins inside subqueries and CTEs, lateral joins, a view over an outer join, aggregates, set operations and the planner's join reordering.
- `tools/check-package-install.sh` gains a run of the packed tool, so the packed tool and the packed generator are proven together once per build.  Whether that run needs a database, and so Docker in that script, is phase 5's to settle; a `--check` against a committed sidecar with no changes may be enough to prove the packaging.

### Documentation

Each phase keeps the documents current, by the rules in `AGENTS.md`:

- `README.md` gains a section on models: installing the tool, running `describe`, committing the sidecar, the overrides, the type map and its option, the Dapper settings a user needs, and what is unsupported.  Phase 3 writes it for PostgreSQL; phase 4 adds SQL Server; phase 5 adds the online mode.
- `CONTRIBUTING.md` gains the tool project, the end-to-end projects and their images, and the new package check.
- `docs/publishing.md` covers the second package from phase 2.
- `docs/diagnostics.md` gains each diagnostic in the phase that adds it.

## Phase 1 - parameters

### Scope

1. A `Parameter` lexeme kind in `SqlLexer`, found by the rule under Parameters, under every dialect.
2. `SqlBlock` and `SqlQuery` carry the ordered list of parameter names.  The segments are unchanged: a parameter stays in the SQL as written.
3. The hash of a query's SQL, computed where the generator builds the emitted text, so that the tool and the generator cannot disagree.
4. The `-- param:` marker: parsed, validated and carried on the block, with no effect yet.  Its grammar is settled here.
5. `InternalsVisibleTo` for `SqlSource.Tool`.

### Decided

- Parameters are found by the lexer, under the file's dialect.
- Nothing a user sees changes.  No member is generated from the parameter list yet, and no diagnostic is added unless the marker needs one.

### Recommended

- The marker is `-- param: <name> [nullable] [type=<database type>]`, one parameter per marker, inside a query, matched like the other markers.  A marker that names a parameter the query does not have is an error.
- The hash is SHA-256 of `engine + "\n" + SQL`, hex-encoded, where the SQL is the comment-stripped emitted text with `\n` line endings.  `System.Security.Cryptography.SHA256` is available to a `netstandard2.0` analyzer.

### Technical notes

- MySQL and MariaDB read `@name` as a user variable and their connector treats it as a parameter by default; the rule is the same for them.  Oracle uses `:name`.  The lexeme's prefix is a dialect rule, like the quote readers, even though every dialect in scope uses `@`.

### Testing

Theory tests over the rule under each dialect, including `@` inside strings, comments and quoted identifiers, the operators that contain `@`, `@@` functions, and a parameter at the start and end of the text.  Round-trip tests that the hash is stable across line endings and `keep-comments`.

## Phase 2 - the tool and the snapshot

### Scope

1. `src/SqlSource.Tool`: the console application, its package, its command line, its exit codes and its error output format.
2. Project evaluation through `dotnet msbuild -getItem`, giving the tool the compiler's view of the `.sql` files and their dialects.
3. The snapshot model, the shared reader and writer, and the format version.
4. `IQueryDescriber`, the engine-neutral `QueryDescription`, and the PostgreSQL describer over Npgsql, with nullability inference.
5. `sqlsource describe`, `--check`, `--force`, the connection option and the environment variable.
6. Publishing for the second package.

### Decided

- The tool is a console application packed as a .NET tool.
- `--check` and `--force` ship in this phase.
- The format carries the format version and the tool version.

### Recommended

The description, engine-neutral at the top and engine-specific in the type:

```
QueryDescription
    ResultKind        Rows | NoRows
    Parameters        [ { Name, Ordinal, Type: DbType?, Nullable: bool? } ]
    Columns           [ { Ordinal, Name, Type: DbType, Nullable: bool?, Origin: { Schema, Table, Column }?, IsIdentity, IsComputed } ]

DbType, PostgreSQL     { Schema, Name, Kind: Base | Array(Element) | Domain(Base) | Enum(Labels) | Range(Subtype) | Multirange(Subtype) | Composite, Facets }
DbType, SQL Server     { SystemTypeName with facets, MaxLength, Precision, Scale, UserType: { Schema, Name, AssemblyQualifiedName }? }
```

A parameter's type, a nullability and an origin are optional because the engines outside this epic need them to be: MySQL reports every parameter as `VARCHAR`, SQLite and Oracle report none, DuckDB reports no nullability, Oracle no origin.  `null` means unknown.  The description holds database types, never OIDs, which are per database, and never C# types.

The PostgreSQL describer:

1. **Parameters.**  `NpgsqlCommand.DeriveParameters()` on the original SQL.  It sends `Parse` with no types and `Describe`, and sets each parameter's `PostgresType`, a resolved object: a base type, a domain with its base, an array with its element, an enum with its labels, a range with its subtype, a composite with its fields.  Npgsql does the `@name` rewriting here, which is the lexer check under Parameters.  A `-- param:` type is declared instead.
2. **Columns.**  `ExecuteReader(CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo)` with the parameters typed from step 1, then `GetColumnSchema()`.  `SchemaOnly` sends `Parse` and `Describe` only.  `KeyInfo` makes Npgsql join `pg_attribute` for every column with a table origin and fill `AllowDBNull` from `attnotnull`, with `BaseSchemaName`, `BaseTableName` and `BaseColumnName`.  A column with no origin keeps `AllowDBNull` null.
3. **Nullability through outer joins.**  The protocol does not report it.  `EXPLAIN (VERBOSE, FORMAT JSON)` of the query, and a walk of the plan that marks an output nullable when it is produced on the inner side of a `Left` join, the outer side of a `Right` join, or either side of a `Full` join.  Match plan outputs to result columns by origin relation, which the `RowDescription` and the plan's `Relation Name` and `Alias` both give, rather than by the text of the output expression as SQLx does; SQLx's one open bug is a `LEFT JOIN` the planner ran as a `Hash Right Join`, which a walk that reads join sides correctly handles.  On PostgreSQL 16 and later, `EXPLAIN (GENERIC_PLAN)` takes the `$n` form directly.  Below 16, `PREPARE` the statement, `SET plan_cache_mode = force_generic_plan`, and `EXPLAIN EXECUTE stmt(NULL, ...)`, then `DEALLOCATE`, which is SQLx's path.  A column whose origin relation the walk cannot find in the plan, and every column of a query whose plan could not be obtained, falls back to a keyword heuristic: if the lexer sees `LEFT`, `RIGHT`, `FULL` or `OUTER` outside strings and comments, the column is nullable.  Then the override, then the policy.
4. CockroachDB is this describer with step 3 skipped: it rejects the `EXPLAIN` form and the `SET`, as SQLx found.

The server's errors and what the tool says:

| Server error | Cause | The tool reports |
|----|----|----|
| `42P18 could not determine data type of parameter $n` | Used nowhere that fixes its type: `SELECT @p`, `COALESCE(@a, @b)` | Add a cast, `@p::int`, or a `-- param:` type. |
| `42P08 inconsistent types deduced for parameter $n` | Used as two types | Cast one use, or use two parameters. |
| Any other | The query is not valid against this schema | The server's message, at the query. |

`record` and `unknown` as a column type are reported as unsupported with the column named.  A polymorphic function's result is already concrete at `Describe`.

### Technical notes

- `Parse` and `Describe` plan and execute nothing; `Describe` of a statement that returns no rows answers `NoData`, and `RETURNING` lists are treated as select lists.  Table origins survive subqueries in `FROM`, CTEs and joins; they are zero for expressions, function results, casts, aggregates, the merged column of a `USING` join, and the whole output of a `UNION`, `INTERSECT` or `EXCEPT`.  A view reports the view's OID and column, and a view's columns are never `NOT NULL` in `pg_attribute`, so they are nullable unless overridden.
- Versions: `Parse` and `Describe` are protocol 3.0; `EXPLAIN (FORMAT JSON)` is 9.0; `plan_cache_mode` is 12; `EXPLAIN (GENERIC_PLAN)` is 16.  The supported floor is for this phase to set; PostgreSQL 13 is the oldest version in support.
- Unverified, and the first thing this phase's spike settles: that `SchemaOnly | KeyInfo` together behave as described in Npgsql; that `EXPLAIN (GENERIC_PLAN)` accepts `$n` through the extended protocol, or whether the `PREPARE` form is needed on 16 too; what `Describe` reports for a domain-typed expression and for a `USING` column of a `FULL JOIN`; and whether `-getItem` evaluation sees a file that a target adds.
- The tool must leave the database as it found it: a `PREPARE` is deallocated, a `SET` is session-local, nothing is executed.

### Testing

The snapshot round trip and the command line without a database.  The describer against a PostgreSQL container, with the nullability matrix under Testing above.  The end-to-end project for PostgreSQL is phase 3's, since it needs the models; this phase's container tests assert descriptions.

### Documentation

`CONTRIBUTING.md` for the new project and its tests.  `docs/publishing.md` for the second package.  The README waits for phase 3, when a user can do something with the output; this phase's package is published so that phase 3 can be tried against it.

## Phase 3 - models

### Scope

1. The generator's pipeline reads `.sql.json` sidecars as a second file kind, paired by path, parsed with the shared reader into a value-equal record, cached like a parsed `.sql` file.
2. Hash and version comparison per query, and the diagnostics under The sidecar.
3. The PostgreSQL type map and the project-level mapping override.
4. Emission of the input and output types with documentation, and the override suffix stripped from names.
5. The PostgreSQL end-to-end project.
6. The README section on models.

### Decided

- The generator never connects to anything.  Its inputs are the project's files and MSBuild values.
- Stale snapshots never produce models.

### Recommended

The items under Generated types and Types in C#.  Two more:

- The props include `**/*.sql.json`.  The generator's file filter already keys on the `.sql` extension and will not mistake a sidecar for a query file.
- A query whose parameters are all absent from the snapshot entry, because the tool declared none, still gets a parameters type with no members only if the phase finds a use for it; otherwise no type.

### Technical notes

- `SqlPath.IsSqlFile` matches the `.sql` extension; a sidecar ends in `.json`.  Pairing is by the normalised path with `.json` removed.
- The reader must tolerate a hand-edited sidecar: an unknown field is ignored, a malformed file is one error at the file, not a crash of the generator.

### Testing

Generator tests over sidecars: every row of the state table, the type map, naming, the override suffix.  The PostgreSQL end-to-end project, with the three kinds of test under Testing.

## Phase 4 - SQL Server

### Scope

1. The SQL Server describer over Microsoft.Data.SqlClient.
2. The SQL Server type map.
3. The SQL Server end-to-end project.
4. The README's SQL Server additions.

### Recommended

The describer:

1. **Parameters.**  `sp_describe_undeclared_parameters @tsql` returns one row per undeclared `@name` with `suggested_system_type_name`, facets included, and `suggested_user_type_*` for alias and CLR types.  The engine takes the type from the innermost enclosing comparison, assignment, function argument, `INSERT` value or `CAST`.  A parameter used more than once is error 11508, which the common optional-filter pattern `WHERE (@name IS NULL OR name = @name)` triggers.  The tool renames each occurrence, `@name` to `@name_1`, `@name_2`, with the lexer, describes, and accepts the result when every copy came back the same type; copies that disagree are an error that names the parameter and the two types.  This is FSharp.Data.SqlClient's workaround without its T-SQL parser.  A `-- param:` type is declared in `@params` instead.
2. **Columns.**  `sys.dm_exec_describe_first_result_set(@tsql, @params, 1)`, the table-valued form that returns a failure as a row with `error_number`, `error_message` and `error_type` instead of raising it.  `@params` is a declaration string built from step 1; the procedure requires every parameter declared, which is why parameters come first.  Browse information fills `source_schema`, `source_table` and `source_column` and adds hidden key columns that the tool drops by `is_hidden = 1`.  A batch with no result set returns no rows.  A column with `name` null, `SELECT 1`, is an error that asks for an alias.
3. **Nullability** is the engine's: outer joins, `CASE`, `ISNULL` never null, `COALESCE` null unless every argument is non-null, and "1 if it can't be determined".  The tool takes it as given, then the override.

The errors to expect:

| `error_type` or number | Cause | The tool reports |
|----|----|----|
| 8, 11521 | A column's type depends on an undeclared parameter | Not reachable once step 1 declares every parameter; reported if it is. |
| 10, 11525 | A temporary table in a multi-statement batch | Supported in a single-statement batch only, which a query in scope is. |
| 4, 11513 | `EXEC(@sql)` | Dynamic SQL cannot be described. |
| 3, 11509 | Two code paths return different shapes | One result set per query. |
| 2, 11501 | Compile error | The server's message. |
| 11502, 11506, 11507 | A parameter's type cannot be deduced | Add a `CAST` or a `-- param:` type. |

### Technical notes

- Both procedures exist from SQL Server 2012, in Azure SQL, LocalDB and the Linux images, and are documented as static analysis: the batch is parsed and bound, never run.
- `json` on SQL Server 2025 and Azure SQL is reported as `varchar(max)` or `nvarchar(max)` by design; `rowversion` is reported as `timestamp`.  Table-valued parameters cannot be inferred and are out of this epic.
- Unverified, for this phase's spike: the exact `system_type_name` spelling for every type; behaviour with a CTE, an `OUTPUT` clause and `SELECT INTO`; whether `@params` accepts a `READONLY` table type; the inferred type of a parameter in `TOP (@n)`, `OFFSET @n ROWS`, `LIKE @p` and `IN (@a, @b)`, which may come back wider than expected, `varchar(8000)` or `numeric(38,19)`.
- `CommandBehavior.SchemaOnly` on SqlClient prepends `SET FMTONLY ON`, which is deprecated, runs the batch's control flow and is under-documented.  It is not used.

### Testing

The describer against a SQL Server container.  The SQL Server end-to-end project.

## Phase 5 - build integration

### Scope

1. The online-mode target in `SqlSource.targets`.
2. `sqlsource describe --watch`.
3. The packed tool in `tools/check-package-install.sh`.
4. The README's online-mode section and `CONTRIBUTING.md`.

### Recommended

The items under Workflow.  The target runs the tool through `dotnet sqlsource`, so a project without the tool manifest gets a clear error.  It must not run in a design-time build, and it must not run when the variable is unset.

### Technical notes

- The target runs before `CoreCompile`, after the package's own targets that collect `AdditionalFiles`, so the tool and the compiler see the same files.  A sidecar the tool writes during the build is read by `CoreCompile` in the same build; the IDE's generator run sees it afterwards.

## Out of scope for the epic

- Multiple result sets, stored procedures and table-valued functions.
- Databases other than PostgreSQL and SQL Server.  The description's optional fields and the `-- param:` type marker are the room left for them.  SQLite would need a static describer; MySQL, MariaDB and Oracle need declared parameter types.
- Queries with `{{tokens}}`.  A `-- sample:` marker giving a value per token, with the snapshot hashed over the sampled SQL, is the later path.
- Executing queries.  The generated code is types and SQL; Dapper or ADO.NET runs them.  A generated reader that maps by ordinal with the typed getters is the candidate for a later epic, and would remove the dependence on Dapper's conventions and versions.
- Sharing a type between queries that read the same columns, as sqlc does with table structs.  The origin fields are kept so that it can be added.
- Per-column C# type overrides.  Composite, `record`, CLR and `vector` types.  Table-valued parameters.
- A schema fingerprint in the snapshot.  Starting a database from the tool.

## Research

The epic was preceded by research into both approaches, which this outline condenses.  The facts that phase specs will lean on, with their sources:

- **Static analysis, prior art.**  sqlc parses with hand-written parsers it wrote in 2026 to escape native dependencies, builds its catalog from migration DDL and a 245 KB file of PostgreSQL functions generated from a live server, types unknown functions as `any`, and added a database-backed analyser in v1.19 and managed databases in v1.22.  SQLDelight's grammars are small because SQLite's type system is small.  Rezoom.SQL types SQL statically by owning its own dialect.  ScriptDom is MIT, `netstandard2.0`, 7 MB, and does no binding.  No managed PostgreSQL parser exists; the .NET bindings of libpg_query bundle a native library.  Nothing in .NET does schema-driven static inference of result types from SQL text.
- **Database-backed, prior art.**  SQLx's `.sqlx/query-<sha256>.json` holds `db_name`, `query`, `describe` with columns, parameters and a tri-state `nullable` list, and `hash`; `cargo sqlx prepare --check` for CI; `DATABASE_URL` for online and `SQLX_OFFLINE` for offline.  pgtyped speaks the protocol directly and does no outer-join analysis.  Prisma TypedSQL requires a database for `prisma generate --sql`.  FSharp.Data.SqlClient has used the two SQL Server procedures since 2014 and documents their limits.  SqlBound, a .NET package at release-candidate stage, does a prepare step with a committed JSON snapshot and a source generator.
- **Other engines** and what they can answer without executing: MySQL's `COM_STMT_PREPARE` gives column types, a `NOT_NULL` flag and origins but reports every parameter as `VARCHAR`; SQLite gives declared types for table columns only and nothing for parameters; Oracle's `OCI_DESCRIBE_ONLY` gives column types and nullability but the client declares bind types; DuckDB infers parameter types and gives column types but no nullability or origin.
- **Type mappings** of Npgsql 10 and Microsoft.Data.SqlClient, and Dapper's constructor and name matching, are as stated under Types in C# and Generated types.

### Sources

PostgreSQL: [protocol message formats](https://www.postgresql.org/docs/current/protocol-message-formats.html), [protocol flow](https://www.postgresql.org/docs/current/protocol-flow.html), [PREPARE](https://www.postgresql.org/docs/current/sql-prepare.html), [EXPLAIN](https://www.postgresql.org/docs/current/sql-explain.html), [error codes](https://www.postgresql.org/docs/current/errcodes-appendix.html), [pg_type](https://www.postgresql.org/docs/current/catalog-pg-type.html), [operator type resolution](https://www.postgresql.org/docs/current/typeconv-oper.html), [parse_target.c](https://github.com/postgres/postgres/blob/master/src/backend/parser/parse_target.c), [parse_param.c](https://github.com/postgres/postgres/blob/master/src/backend/parser/parse_param.c), [postgres.c](https://github.com/postgres/postgres/blob/master/src/backend/tcop/postgres.c).

Npgsql: [SqlQueryParser.cs](https://github.com/npgsql/npgsql/blob/main/src/Npgsql/SqlQueryParser.cs), [NpgsqlCommand.cs](https://github.com/npgsql/npgsql/blob/main/src/Npgsql/NpgsqlCommand.cs), [DbColumnSchemaGenerator.cs](https://github.com/npgsql/npgsql/blob/main/src/Npgsql/Schema/DbColumnSchemaGenerator.cs), [basic usage](https://www.npgsql.org/doc/basic-usage.html), [supported types](https://www.npgsql.org/doc/types/basic.html), [date and time](https://www.npgsql.org/doc/types/datetime.html), [enums and composites](https://www.npgsql.org/doc/types/enums_and_composites.html), release notes [6.0](https://www.npgsql.org/doc/release-notes/6.0.html), [8.0](https://www.npgsql.org/doc/release-notes/8.0.html), [10.0](https://www.npgsql.org/doc/release-notes/10.0.html).

SQL Server: [sp_describe_first_result_set](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-describe-first-result-set-transact-sql), [sys.dm_exec_describe_first_result_set](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-exec-describe-first-result-set-transact-sql), [sp_describe_undeclared_parameters](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-describe-undeclared-parameters-transact-sql), [errors 11000-12999](https://github.com/MicrosoftDocs/sql-docs/blob/live/docs/relational-databases/errors-events/includes/sql-server-2025-database-engine-events-and-errors-11000-12999.md), [SET FMTONLY](https://learn.microsoft.com/en-us/sql/t-sql/statements/set-fmtonly-transact-sql), [COALESCE](https://learn.microsoft.com/en-us/sql/t-sql/language-elements/coalesce-transact-sql), [json type](https://learn.microsoft.com/en-us/sql/t-sql/data-types/json-data-type), [data type mappings](https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql-server-data-type-mappings), [CommandBehavior](https://learn.microsoft.com/en-us/dotnet/api/system.data.commandbehavior), SqlClient release notes [5.1](https://github.com/dotnet/SqlClient/blob/main/release-notes/5.1/5.1.0.md), [6.0](https://github.com/dotnet/SqlClient/blob/main/release-notes/6.0/6.0.0.md), [6.1](https://github.com/dotnet/SqlClient/blob/main/release-notes/6.1/6.1.0.md).

Prior art, database-backed: SQLx [query! macro](https://docs.rs/sqlx/latest/sqlx/macro.query.html), [describe.rs](https://github.com/launchbadge/sqlx/blob/main/sqlx-postgres/src/connection/describe.rs), [PR 3541](https://github.com/launchbadge/sqlx/pull/3541), [issue 3202](https://github.com/launchbadge/sqlx/issues/3202), [sqlx-cli](https://github.com/launchbadge/sqlx/blob/main/sqlx-cli/README.md), [offline data format](https://github.com/launchbadge/sqlx/blob/main/sqlx-macros-core/src/query/data.rs); pgtyped [actions.ts](https://github.com/adelsz/pgtyped/blob/master/packages/query/src/actions.ts), [sql files](https://pgtyped.dev/docs/sql-file); [Prisma TypedSQL](https://www.prisma.io/docs/orm/prisma-client/using-raw-sql/typedsql); FSharp.Data.SqlClient [home](https://fsprojects.github.io/FSharp.Data.SqlClient/), [DesignTime.fs](https://github.com/fsprojects/FSharp.Data.SqlClient/blob/master/src/SqlClient.DesignTime/DesignTime.fs), issues [13](https://github.com/fsprojects/FSharp.Data.SqlClient/issues/13), [34](https://github.com/fsprojects/FSharp.Data.SqlClient/issues/34), [263](https://github.com/fsprojects/FSharp.Data.SqlClient/issues/263); [SqlBound](https://www.nuget.org/packages/SqlBound).

Prior art, static: sqlc [config](https://docs.sqlc.dev/en/latest/reference/config.html), [managed databases](https://docs.sqlc.dev/en/latest/howto/managed-databases.html), [vet](https://docs.sqlc.dev/en/latest/howto/vet.html), [named parameters](https://docs.sqlc.dev/en/latest/howto/named_parameters.html), [output_columns.go](https://github.com/sqlc-dev/sqlc/blob/main/internal/compiler/output_columns.go), [resolve.go](https://github.com/sqlc-dev/sqlc/blob/main/internal/compiler/resolve.go), [seed.go](https://github.com/sqlc-dev/sqlc/blob/main/internal/engine/postgresql/seed.go), releases [1.19.0](https://github.com/sqlc-dev/sqlc/releases/tag/v1.19.0), [1.22.0](https://github.com/sqlc-dev/sqlc/releases/tag/v1.22.0), [oliphant](https://github.com/sqlc-dev/oliphant), [sqlc-gen-csharp](https://github.com/DaredevilOSS/sqlc-gen-csharp); SQLDelight [JoinClauseMixin.kt](https://github.com/sqldelight/sql-psi/blob/master/core/src/main/kotlin/com/alecstrong/sql/psi/core/psi/mixins/JoinClauseMixin.kt); [Rezoom.SQL](https://github.com/rspeele/Rezoom.SQL); [ScriptDom on NuGet](https://www.nuget.org/packages/Microsoft.SqlServer.TransactSql.ScriptDom); [libpg_query](https://github.com/pganalyze/libpg_query).

Other engines: MySQL [COM_STMT_PREPARE](https://dev.mysql.com/doc/dev/mysql-server/latest/page_protocol_com_stmt_prepare.html), [bug 23385](https://bugs.mysql.com/bug.php?id=23385); SQLite [column_decltype](https://sqlite.org/c3ref/column_decltype.html), [table_column_metadata](https://sqlite.org/c3ref/table_column_metadata.html); SQLx [sqlx-sqlite explain.rs](https://github.com/launchbadge/sqlx/blob/main/sqlx-sqlite/src/connection/explain.rs); Oracle [OCI statement functions](https://docs.oracle.com/en/database/oracle/oracle-database/19/lnoci/statement-functions.html); [DuckDB prepared statements](https://duckdb.org/docs/current/clients/c/prepared.html).

Dapper, Roslyn and MSBuild: [DefaultTypeMap.cs](https://github.com/DapperLib/Dapper/blob/main/Dapper/DefaultTypeMap.cs), [PR 2228 DateOnly](https://github.com/DapperLib/Dapper/pull/2228), [incremental generators cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md), [analyzer banned symbols](https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/Core/AnalyzerBannedSymbols.txt), [Testcontainers modules](https://dotnet.testcontainers.org/modules/).
