# Query generation - epic outline

Date: 2026-10-07

This outline coordinates the phases of the epic.  It is kept current until the epic closes: a phase that changes the plan updates it in the same pull request.

## Goal

At the end of this epic a project can do three things with SqlSource, together or any subset of them:

1. **Use queries written in `.sql` files**, as it can today: a constant, or a method where the query has tokens.
2. **Get strongly typed input and output models for its queries**, found from the database itself, in the manner of sqlc.
3. **Execute its queries through generated methods**: ADO.NET code that SqlSource writes, as a replacement for Dapper, not a wrapper around it.

A project that only wants the constants is unchanged.  A project that wants the models and runs them through Dapper or its own ADO.NET code can.  A project that wants the generated methods gets them from the same sidecar.

```sql
-- name: GetUser
-- summary: Loads one user by id.
SELECT id, name, created_at, deleted_at
FROM users
WHERE id = @id;
```

```csharp
[SqlSourceGenerate]
public partial class UserRepository(IDbConnection connection)
{
    // Generated beside Sql.GetUser:
    //   public sealed record GetUserParameters(int Id);
    //   public sealed record GetUserRow(int Id, string Name, DateTime CreatedAt, DateTime? DeletedAt);
    //   public static Task<GetUserRow?> GetUserAsync(DbConnection connection, GetUserParameters parameters, CancellationToken ct);
    public Task<GetUserRow?> Get(int id, CancellationToken ct) => Sql.GetUserAsync(connection, new GetUserParameters(id), ct);
}
```

The types, and everything the methods need to run the query, come from a new command-line tool, `sqlsource`, which asks a running database to describe each query and writes the answer into a file beside the `.sql` file.  The generator reads that file and never touches a database.  A build reports a file that is out of date with its SQL, so a wrong type never compiles silently.

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
- Generated execution methods are plain ADO.NET over `DbConnection`, with the provider's own types only where ADO.NET's abstractions do not reach.  Dapper is not a dependency of anything generated.
- Extensive end-to-end tests, one project per supported database, that run the tool, build with the generator and execute the generated code against a real database in a container.

## Phases

| Phase | Status | Spec | Delivers |
|----|----|----|----|
| 0. Names | Not started | | `[SqlQueries]` becomes `[SqlSourceGenerate]`, `SqlQueriesMode` becomes `GeneratorTarget` and `Mode` becomes `Target`; `-- SqlSource:` becomes `-- generator:` and `dialect=` becomes the marker `-- dialect:`.  Through the generator, the parser, the README, the diagnostics, the tests and the package-install project.  Nothing else changes. |
| 1. Parameters | Not started | | The lexer finds `@name` parameters by the file's dialect; each query carries its ordered parameter list and the hash of its SQL.  Nothing a user sees changes. |
| 2. The tool and the snapshot | Not started | | The `SqlSource.Tool` package: the `sqlsource describe` command with `--check` and `--force`, project evaluation through MSBuild, the snapshot format with its shared reader and writer, and the PostgreSQL describer with nullability inference.  Publishing covers the second package. |
| 3. Models | Not started | | The generator reads snapshots, maps PostgreSQL types to C#, emits the input and output types with documentation, and reports stale, missing and mismatched snapshots.  The PostgreSQL end-to-end project.  The first usable release. |
| 4. SQL Server | Not started | | The SQL Server describer, its type map, and its end-to-end project. |
| 5. Execution methods | Not started | | A generated method per query that opens a command, binds the parameters, runs it and reads the rows into the output type by ordinal with typed getters, for both engines.  The end-to-end projects execute through them. |
| 6. Build integration | Not started | | The online mode: an MSBuild target runs the tool before compile when a connection string is in the environment.  A watch mode.  The package-install check runs the tool. |
| 7. Bug reports | Not started | | `sqlsource diagnose`, the obfuscated bundle, and `sqlsource replay`, which runs the pipeline from a bundle's capture without a database. |

Each phase has its own spec, plan and pull request.  Phase 0 is a rename and nothing else, kept apart because it touches every file that names the attribute.  Phase 1 changes nothing a user can see.  Phase 2 ships a tool whose output nothing reads yet; its package is published so that phase 3 can be tried against it.  Phase 3 is the first release a user can use, for PostgreSQL with Dapper or their own ADO.NET code.  Phase 4 adds SQL Server.  Phase 5 delivers the third goal.  Phase 6 removes the need to run the tool by hand.  Phase 7 makes a bug report reproducible without the user's schema; the log and the replay seam from phase 2 serve maintainers until then.

The order puts the parameter lexeme first because every later phase depends on it and it is small; the tool before the generator because the generator's models cannot be tested without a snapshot to read; PostgreSQL before SQL Server because its nullability inference is the hardest single piece and should be proven early; the execution methods after both describers so that their abstraction over the two providers is designed with both in hand; and the build integration last because it is convenience over a working whole.

## Decisions for the whole epic

### Approach

Decided: the types are found by asking the database, from a tool that runs outside the compiler, and are committed to the repository in a file beside each `.sql` file.  The alternative, parsing the schema and the query and inferring the types in SqlSource, was researched to the same depth and rejected.  The reasons, in order of weight:

1. Only the server implements its dialect completely.  Every function, operator, cast, domain, extension type and syntax the server knows is covered; a static binder covers what was written for it, and its failures are silent wrong types, not errors.
2. The static approach is out of proportion to the project.  SQL Server has a managed parser; PostgreSQL has none, and a native one cannot load inside a source generator.  Either engine's binder needs a catalog of thousands of functions regenerated for each release.  sqlc, after six years, added a database-backed analyser because "for more advanced queries sqlc might not have enough information to produce good code".
3. Every tool of this kind built in the last five years asks the database: SQLx, pgtyped, Prisma TypedSQL, SqlBound, and sqlc's analyser.
4. The static approach stays open.  Describing sits behind one interface and the snapshot is engine-neutral; a static describer for an engine that cannot answer, SQLite is the plausible one, is a later implementation of that interface.

Decided: the generator keeps the emission, of the models and of the execution methods.  A tool that emitted C# itself was weighed and rejected: generated code would land in the repository and go stale silently, every change to naming, nullability policy, the type map or the generated methods would need a database run to apply, and the constants, token methods and diagnostics the generator already produces would be duplicated or moved.  With the split, stale types are a build error, the snapshot is a small diffable file, and the mapping evolves with the package.

### Packages and repository

Decided:

- The tool is a normal .NET console application in `src/SqlSource.Tool`, packed as a .NET tool with `PackAsTool` and the command name `sqlsource`, published as the package `SqlSource.Tool`.  It targets `net8.0` with `RollForward` set to `Major`.
- `SqlSourceDatabase` is the first MSBuild value that the tool reads and the generator does not.  It still goes through the package's props and targets, so its metadata is trimmed and collected the way `SqlSourceDialect`'s is, and it obeys the prefix rule.  The `-- database:` marker is parsed by the shared parser, so the generator accepts it and ignores it.
- The tool references the generator project directly, and the generator adds `InternalsVisibleTo` for it, as it has for the test projects.  The lexer, the file parser, the dialect rules, the snapshot reader and writer, the hashing and the diagnostic descriptors are shared this way.  No library package is split out; the generator's package is unchanged.  A `SqlSource.Core` library is the refactor to make if a third consumer appears.
- Both packages carry the same `VersionPrefix` and are published together by the same workflow.
- The generator package stays a development dependency with one DLL under `analyzers/dotnet/cs` and nothing under `lib/`.

Technical notes:

- A tool package carries its whole dependency closure in its own folder, so the generator DLL and Roslyn travel with it without packaging work.  The tool gets `Microsoft.CodeAnalysis` transitively; the parser uses `TextSpan` from it.
- The generator cannot take a JSON library.  `System.Text.Json` in a `netstandard2.0` analyzer collides with the compiler's own copy, and Newtonsoft is a dependency the package cannot ship cleanly.  The snapshot reader is a small hand-written JSON parser, and the writer is hand-written too so that the two are tested together.  The format stays within what such a parser handles comfortably: objects, arrays, strings, integers, booleans and null.
- The tool learns what the compiler sees from a **manifest** that a target in `SqlSource.targets` writes: the project's `.sql` files with their metadata and the project's `SqlSource` properties, to a file under `obj`, at the point where the compiler's own config file is written and after the package's trim and collect targets.  From the command line the tool runs `dotnet msbuild -t:SqlSourceWriteManifest` on each project in the run and reads the files; in phase 6's online mode the target has already run as part of the build, so no second MSBuild process is spawned.  The manifest is a contract between package and tool, as the sidecar is between tool and generator, and carries a format version.
- The alternatives and why not: `dotnet msbuild -getItem:AdditionalFiles` evaluates without running targets, so it misses a file a target adds and the package's own trimming, and inside a build it would mean a second evaluation of the project being built; hosting MSBuild in-process needs `MSBuildLocator` and matches SDK versions by hand; reading `obj/*.GeneratedMSBuildEditorConfig.editorconfig` needs a prior build and holds no file list.  Globbing `**/*.sql` in the tool was never an option, since `Remove`, `Update`, `DefaultItemExcludes`, `SqlSourceIncludeFiles=false` and `Directory.Build.props` all change what the compiler sees.
- For a solution, phase 2 chooses between one process per project and a generated traversal project that runs the target over every project in parallel with `SkipNonexistentTargets`, which also skips projects without SqlSource; `-getProperty:SqlSourceImported` is the cheap evaluation-only check if the first route is taken.  A multi-targeting project writes one manifest per target framework and the tool reads the first, since `AdditionalFiles` almost never depend on the framework.
- MSBuild in Visual Studio runs on .NET Framework; shipping the describe step as an MSBuild task instead of a tool would mean building it twice and loading Npgsql inside MSBuild.  A tool invoked by a target is the lower-risk shape.

### Workflow

Decided, the unit of a run:

- The tool mirrors the SDK's commands.  With no argument it looks in the current directory for exactly one `.sln`, `.slnx` or `.csproj` and errors otherwise; an explicit path to either is an argument.
- In solution mode the tool lists the solution's projects and keeps the ones that use SqlSource.  In project mode a project that does not use SqlSource is an error.
- The tool's configuration is the project file: which `.sql` files the project claims, their dialects, and their databases.  There is no configuration file of the tool's own.

Decided, databases:

- A project can use several databases, of one engine or of several, and one run refreshes every file the run has a connection for.
- Each `.sql` file belongs to one logical database, named the way its dialect is: a `-- database: billing` marker in the file, as metadata `SqlSourceDatabase` on the file's `AdditionalFiles` item, or as the property `SqlSourceDatabase` for the project.  The marker is allowed in the preamble, for every query of the file, and inside a query, for that query alone; the query's marker wins over the file's, the file's over the metadata, the metadata over the property.  When nothing is set, the name is the dialect's name.
- A name is one database across the whole run: a `billing` database used by three projects is one connection.  Every query that names a database has the dialect of its file, and the tool errors when two queries of one name have two dialects, since the dialect picks the driver for that connection.
- Connections are supplied per name, on the command line or in the environment, never in the project file, since they hold credentials.  A name with no connection is an error for its queries; the rest of the run continues.

Decided, commands:

- `sqlsource describe` describes every query of every `.sql` file in the run and writes or updates the sidecars.  A query whose hash and versions match its entry is skipped.
- `sqlsource describe --force` describes every query again whether or not its entry is up to date.  It is also the command to run after a schema change, since the hash cannot see one.
- `sqlsource describe --check` describes into a temporary location and compares with the committed sidecars, and exits non-zero when they differ.  It is the CI step, as `cargo sqlx prepare --check` and `sqlc diff` are.

Recommended:

- `--connection billing=Host=...` names a connection; `SQLSOURCE_CONNECTION_BILLING` is the same in the environment, with the name upper-cased.  A bare `--connection ...` or `SQLSOURCE_CONNECTION` is accepted when exactly one database is in the run, which is most projects.  Environment variables are not MSBuild properties and need not carry the `SqlSource` prefix, but the name should still make the owner obvious.
- `--database billing` restricts a run, including `--check`, to the named databases, so refreshing one after a migration neither needs nor complains about the others' connections.  `--project` and a file path restrict a run the same way.
- A project uses SqlSource when the package's props set a marker property, `SqlSourceImported`, which evaluation returns whether SqlSource came as a package or, as in this repository's tests, as a project reference with the props imported by path.  Looking for a `PackageReference` would miss the second.
- A `.sql` file with lexer errors is skipped and its errors reported, since the generator rejects it anyway.
- Every run ends with one summary line per database, with the counts of queries described, skipped and failed, so a filter that matched nothing is visible.
- The sidecar records each query's database name, so a reader knows where its types came from.  The generator does not read it.
- The tool is installed through a local tool manifest, so its version is pinned per repository and `dotnet tool restore` gets it, the way this repository pins CSharpier.
- Phase 6's online mode: a target in `SqlSource.targets` runs `dotnet sqlsource describe` before `CoreCompile` when the variable is set and the build is not a design-time build.  The tool hashes before it connects, so a build with nothing changed costs nothing.  Without the variable the build is offline and the committed snapshot is used, which is what CI and a machine without a database get.  This is SQLx's online and offline split.
- Phase 6's `--watch` describes a file as it is saved, for people who want the IDE to update without a build, as pgtyped's watch mode does.
- Starting a throwaway database with Testcontainers and running the project's migration command, then describing against it, is a later addition to the tool and not in this epic.  DbUp, FluentMigrator and EF Core migrations all have a command-line form, so "run this command against this connection string" would cover them.

### Output of the tool

Decided: the tool reports errors in the compiler's format, `path(line,col): error SQLSRCnnn: message`, with the same ids as the generator where the condition is the same, so that the online mode's errors reach the IDE's error list and a terminal shows what the build would.

Recommended: exit code zero on success, one when a query could not be described, two when `--check` found a difference.  Command-line parsing with System.CommandLine, which gives help and exit codes for free; the commands are few enough that a hand-written parser would also do.

### What is generated

Decided:

- The attribute is renamed `SqlSourceGenerate`, `[SqlSourceGenerate]` on the type, since it now drives three kinds of generation and not only queries.  Its `Mode` property is renamed `Target` and the `SqlQueriesMode` enum `GeneratorTarget`; its values, `Nested` and `Direct`, say where the members go, which is what a target is.  Nothing has been published, so the rename breaks nobody.  It is phase 0.
- A new setting, `SqlSourceOutput`, says what SqlSource generates for a query.  Its values are the members of a new enum, `GeneratorOutput`, emitted beside `GeneratorTarget`:

| Value | Generates |
|----|----|
| `Sql` | The constant or token method, as today |
| `Models` | `Sql`, plus the input and output types |
| `CodeGen` | `Models`, plus the execution methods |

- The default is `CodeGen`.
- It is set the way the dialect and the database are, plus one place of its own: the property `SqlSourceOutput` for the project, metadata `SqlSourceOutput` on a file's `AdditionalFiles` item, an optional `Output` property on the attribute, `[SqlSourceGenerate(Output = GeneratorOutput.Models)]`, for the files a type claims, and an `-- output: models` marker in a file's preamble or inside a query.
- The tool reads the setting too, to leave out what needs no types: a project whose queries all resolve to `Sql` is skipped in solution mode, and so is a file or a query that resolves to `Sql`.  A sidecar holds entries only for the queries that need them, and the tool deletes a sidecar that would be empty.
- `Models` and `CodeGen` need a dialect that has a describer: `postgres` or `mssql` in this epic.  A query that resolves to either output under any other dialect is an error, in the generator and in the tool, that says to set the dialect or to set the output to `Sql`.  Whether `cockroachdb` joins the two, through the PostgreSQL describer, is for phase 2 to settle.
- The default dialect stays `ansi`.  Only a project that generates `Sql` alone can use it, and that is who it is for.  A project that uses nothing but the defaults therefore gets the error above on every file, and its message is the instruction: set `SqlSourceDialect`.  The README's installation section says so before anything else.

Recommended:

- Precedence, most specific first: the query's marker, the file's marker, the attribute's `Output`, the file's metadata, the project's property, then the default.  The attribute sits above the metadata because it is set on one type on purpose, while metadata is usually a glob; and below the markers because what a file says about itself wins everywhere else in SqlSource.  Since two types may claim one file, the output is resolved per type and query, not per file.
- The tool therefore has to know which types claim which files with which `Output`, which it needs anyway: a `.sql` file no type claims is ignored by the generator today and gets no sidecar.  The manifest carries the project's `Compile` items, and the tool reads the attributes from them with the generator's own reader over syntax trees, matching the attribute by name.  A semantic match, which the generator has, needs a compilation with references the tool does not want; the syntax match is an approximation only for a project that aliases or shadows the attribute's name, which the phase 2 spec records.
- Until phase 5 ships, `CodeGen` behaves as `Models`.
- A value that is not one of the three is an error with no position, once per distinct value, as an invalid dialect is; the files it covers are generated as `CodeGen`.
- The output-needs-a-dialect error is reported once per file, at the marker that set the output when there is one and at the start of the file otherwise, rather than at every query, since the fix is one setting.  The file still gets its constants and methods for the `Sql` part of its output.
- The three per-file settings, dialect, database and output, resolve by the same rule from the same four sources, so the generator's dialect resolution generalises to a per-file settings record rather than growing two siblings.  The dialect marker alone keeps its preamble-only restriction, since it changes how the lines after it are lexed.

Technical notes:

- Each setting the generator reads needs a `CompilerVisibleProperty` and a `CompilerVisibleItemMetadata` in the props, and the trim and collect targets in `SqlSource.targets` that the dialect has.  `SqlSourceDatabase` needs the trim and collect so that the tool sees a clean value, even though the generator never reads it.

### The sidecar

Decided:

- One file beside each `.sql` file: `Users.sql` has `Users.sql.json`.  The package's props include `**/*.sql.json` as `AdditionalFiles` the way they include `.sql` files, and the generator pairs the two by path.
- The sidecar nests under its `.sql` file in Visual Studio and Rider with no user action: `SqlSource.targets` sets `DependentUpon="%(Filename)"` on every `**/*.sql.json` item, and `%(Filename)` of `Users.sql.json` is `Users.sql`.  In the targets and not the props, so that it also reaches a sidecar a project lists itself when `SqlSourceIncludeFiles` is off.  VS Code has no project-file nesting; the README gives its `explorer.fileNesting.patterns` line.
- The file's first property is `_WARNING`, one sentence saying the tool wrote it and regenerates it.
- The file carries two version tags: a format version, which is the compatibility contract between tool and generator, and the version of the tool that wrote it, so that a tool and a generator that are out of step are detected.
- Each query's entry carries a hash of the SQL it describes.  An entry whose hash does not match the query is stale.
- The format is a public contract and is specified in the [sidecar format design](2026-10-07-sidecar-format-design.md): the fields, the reader's rules, the engine-specific type objects, the compatibility rules, and a JSON Schema that phase 2 ships under `schemas/`.  In outline: a top level of `$schema`, `formatVersion`, `toolVersion` and `queries`; per query `hash`, `engine`, `database`, `serverVersion`, `resultKind`, `parameters` and `columns`; a flat type object per engine with a required `name` spelled as the engine spells it; tri-state `nullable`, `identity` and `computed`; an `origin` or null.  Only objects, arrays, strings, integers, booleans and null.
- An entry with rows records `matchesTable`, the table whose column list the result is exactly, or null; the tool compares the result against the catalog after describing.  See Models.
- The sidecar records the description, not the decision: a column's `nullable` is what the server and the tool's inference established, and a column's `name` is as the server returned it, `!` or `?` suffix included.  The generator applies the suffix and the policy.  So the file is a faithful record and the policy can change without a database.
- A change to the format is additive, with no version bump, when a reader of the previous format that ignores unknown keys still generates correct code from the new file: a new key, a new engine, a new type kind.  A rename, a retyping, a hash change, a new `resultKind` or a new required key bumps `formatVersion`.

Recommended:

- A per-file sidecar rather than one folder of hashed files as SQLx's `.sqlx/` is, because the diff is readable beside the SQL it describes, a deleted `.sql` file leaves an orphan that is obvious, and the generator already keys everything by file.  Not one file per project, because every query change would touch it and merge conflicts would be constant.
- The hash is SHA-256 over the engine name and the SQL the generator emits for the query, comments stripped whatever `keep-comments` says, with parameters as written.  A comment changes no type; a parameter's name is part of the input type.
- The format version is an integer.  The generator reads the versions it knows; a higher one is an error that says to update SqlSource, a lower one an error that says to run the tool.  A tool version that differs from the generator's while the format matches is a warning, since the two are released together and the snapshot is still readable.  The tool rewrites an entry whose tool version is not its own, so `describe` after an update refreshes everything without `--force`.
- Entries are keyed by query name, in the file's order, so a diff follows the `.sql` file.
- A schema fingerprint per origin table, to catch a schema change behind unchanged SQL without a database, is not in this epic.  `--check` in CI is the answer the epic gives.
- A file with a query whose database has no connection in the run is not written, rather than mixing old and new entries under one `toolVersion`.

What the generator does with a sidecar:

| State | Effect |
|----|----|
| A query whose output is `Sql` | Its sidecar entry, if any, is ignored for generation; an entry that exists is an orphan, below. |
| A query whose output needs types and has no sidecar or no entry | An error at the query: run the tool. |
| Entry present, hash differs | An error at the query: the snapshot is stale.  No model is emitted, so stale types never compile. |
| Entry present for a query that no longer exists | A warning at the sidecar: an orphan entry. |
| Sidecar's engine differs from the file's dialect | An error at the sidecar. |
| Format version unknown, or tool version differs | As above under Recommended. |

Recommended: a missing entry is an error, not a warning, so that a new query never silently compiles as a constant alone.  A project that wants only the constants says `SqlSourceOutput=Sql` and never runs the tool.

### Parameters

Decided: the lexer finds parameters, so that one dialect-aware rule gives the same list to the tool, the generator and the hash.  A `@` inside a string, a comment or a quoted identifier is not a parameter under any dialect, and the lexer already knows where those are.

Recommended, the rule for both dialects in scope: `@` followed by a letter, a digit or `_`, where the `@` is not preceded by `@` or by a character that can be part of an identifier.  The name runs over letters, digits and `_`.  Names are compared ignoring case, as both drivers compare them.  Ordinal is the order of first appearance.  `@name` only: Npgsql also takes `:name`, which SqlSource does not, because `::` casts and array slices `a[1:2]` make `:` ambiguous and Npgsql's own documentation says its rewriting "may not parse some forms of SQL correctly".  The operators that contain `@` are safe under the rule, `@>`, `<@`, `@@`, `@?`, as are T-SQL's `@@ROWCOUNT` and the other system functions.

Technical notes:

- At run time the SQL still holds `@name`.  On SQL Server that is native.  On PostgreSQL, Npgsql rewrites `@name` to `$n` when the command's parameters are named, which is what Dapper produces.  SqlSource's rule must therefore agree with Npgsql's for every query, and the tool checks it: for PostgreSQL it lets Npgsql derive the parameters of the original SQL and compares names and count with the lexer's, and reports a disagreement as an error on the query.

Decided, the parameter list of a query:

- It is the parameters the lexer finds in the static SQL, in order of first appearance, followed by the parameters that `-- param:` markers declare and the static SQL does not hold, in marker order.
- A `-- param:` marker for a parameter that is not in the static SQL must give a type.  Such a parameter reaches the query only through a token's fragment at run time, so nothing else can type it.  A marker without a type for such a parameter is an error.
- A parameter that appears in a token's default and in neither the static SQL nor a marker is an error that says to declare it.  A default is a sample, and a type taken from a sample alone would be a guess.
- The marker writes the parameter with the dialect's prefix, as the SQL does: `@name` under `postgres` and `mssql`, and `:name` under `oracle` if that dialect ever describes.  One parameter per marker, allowed inside a query and in the preamble for a name every query of the file shares, the query's winning.  A marker in a query that names a parameter the query neither holds nor could receive is not an error, since the receiving case cannot be told apart.

Technical notes:

- The tool asks the server to resolve a declared type name, so that every parameter in the sidecar carries a resolved type and the generator never parses a type name.  On PostgreSQL that is a `pg_type` lookup through Npgsql's type catalog; on SQL Server the type goes into `@params` where the procedure resolves it, or through `sys.types` for a parameter the sample does not use.
- A declared parameter that a run-time fragment does not use is bound anyway by the generated method.  SQL Server accepts a parameter the batch does not reference; Npgsql in named mode sends only the parameters whose placeholders appear in the text.  Phase 5 verifies both.

### Tokens

A query with `{{tokens}}` cannot be described as written, so each token gets a default: a sample fragment that the tool substitutes before describing.

Decided:

- A token may carry its default inline: `{{name:default}}`.  The name is as today; the default is everything after the first `:` up to the closing `}}`, trimmed, so `{{cast:x::int}}` is the token `cast` with the default `x::int`.  A default cannot contain `}}`.  An empty default, `{{extraWhere:}}`, is allowed and means the query is described with nothing there.
- A default is a sample for describing and nothing else.  The generated method still takes the token as a `string`, the emitted SQL still holds the placeholder, and token validation applies to the run-time argument as today.
- Under `Sql` output a default is allowed and ignored.  Under `Models` and `CodeGen` a token without a default is an error at the query, in the generator and in the tool.
- A default can also be given once for a token that appears several times, with a marker that holds the inline form: `-- token: {{where:AND deleted_at IS NULL}}`.  The marker's body is exactly what would be written in the SQL, parsed by the same scanner, so there is no second grammar to learn; one token per marker, and anything outside the braces is an error.  It is allowed inside a query and in the preamble, where it covers every query of the file; the query's wins.
- The README states the contract: a fragment passed at run time must keep the sample's shape, the same result columns and the same parameters, because nothing can check it.  An `ORDER BY` fragment or a table name keeps it; a column list does not.

Recommended:

- The marker form was chosen over a `-- generator: token-default=` parameter because generator parameters are a space-separated list and most defaults are SQL fragments with spaces, which would need a quoting rule; and over `-- token: where AND ...`, a bare name and a space, because that reads as SQL to anyone who does not know the rule.
- An inline default and a marker default for one token in one query, or two inline occurrences with different defaults, is an error, not a precedence.  One occurrence with a default and others without is fine.
- The hash covers the sample and the declarations: the SQL with each token rendered as `{{name:default}}` using the resolved default, followed by the `-- param:` declarations in effect, so a changed default, a renamed token or a changed declaration re-describes.
- A parameter that appears only inside a token's default, or only in the fragment a caller will pass at run time, is not in the static SQL, so the lexer cannot find it; it must be declared with a `-- param:` marker that gives its type.  See Parameters.
- `{{a:b}}` was literal text under the rule that braces around anything but a name are not a token; it is now a token.  Nothing is published, so the change costs nothing, and the README's rule is updated.

### Nullability

Decided: a nullability the description leaves unknown is nullable.  A false nullable is a nuisance; a false non-nullable is a silent wrong value, because Dapper leaves a member at its default when the column is `NULL`.

Decided: parameters are non-nullable unless the user says otherwise.  The database cannot report it, and sqlc, SQLx and FSharp.Data.SqlClient all default this way.

Recommended, the overrides, each written so that the `.sql` file stays runnable as it is:

| What | How | Precedent |
|----|----|----|
| A column's nullability | An alias with a suffix: `AS "name!"` is not null, `AS "name?"` is nullable.  Legal quoted identifiers on both engines; `[name!]` also on SQL Server.  The database returns the name with the suffix and the generator strips it. | SQLx, pgtyped |
| A parameter's nullability | A marker in the query: `-- param: @deletedBefore null`.  The parameter as it is written in the SQL, with the dialect's prefix, then `null`, the word a column definition uses. | sqlc's `narg`, pgtyped's `!` in the other direction |
| A parameter's type | The same marker with a type in the database's own vocabulary: `-- param: @page int`, `-- param: @since timestamptz null`.  The tool hands the type to the server as the parameter's type, so the server still checks it against every use.  It fixes a type the server cannot infer, `SELECT @p`, or infers too wide, `varchar(8000)` in `TOP (@n)`; and it is how a parameter that is not in the static SQL gets a type at all.  Every engine outside the two in scope except DuckDB needs it for every parameter. | Prisma's `-- @param {Int} $1:name`, sqlc's `sqlc.arg`, T-SQL's own `@name type` declarations |

A suffix on the parameter in the SQL, `@id?`, was rejected: it is valid on neither server, so the file could no longer be run by the user's tools or by the describer without rewriting.  The marker's exact grammar is phase 1's to settle, since the lexer reads markers.

### Types in C#

Decided:

- The type map covers **every type the driver can read**.  This outline does not enumerate them; each phase that adds an engine researches the driver's full list and populates the map, with a test per row.
- Major libraries that extend the drivers' types get maps out of the box.  In the first version: NodaTime, through `Npgsql.NodaTime` on PostgreSQL and by conversion on SQL Server, where no plugin exists and the EF Core provider's conventions are followed; NetTopologySuite, through `Npgsql.NetTopologySuite` and `NetTopologySuite.IO.SqlServerBytes`; `JsonDocument` and `JsonElement`, built into Npgsql and by parsing on SQL Server; `SqlJson` on SqlClient 6.0 and later; and `BigInteger` for an unbounded `numeric`.  Left to the override or a later phase: Newtonsoft through `Npgsql.Json.NET`, `Npgsql.GeoJSON`, Pgvector, `SqlVector`, `Microsoft.SqlServer.Types` for `hierarchyid` and spatial, and the strongly-typed-id generators, which wrap a primitive the driver already reads.
- A library's map is switched on by detection from the compilation, keyed on the plugin assembly where one exists, since a reference to `Npgsql.NodaTime` is the signal that the data source calls `UseNodaTime()`; and a property per library, `SqlSourceNodaTime`, overrides detection either way, since a project can reference NodaTime for other reasons and keep `DateTime` at the database layer.  The README says which `Use...()` call each plugin needs on the data source.
- A map row has one of three shapes: a driver type read with `GetFieldValue<T>`; a plugin type read the same way once the plugin is registered; or a converted type, with a read and a write expression the generator inlines.  The third is what NodaTime on SQL Server needs, and it is the mechanism the override uses to map a column to a strongly-typed id.
- The driver floor is **Npgsql 8.0 and Microsoft.Data.SqlClient 5.1**, tested against Npgsql 8, 9 and 10 and SqlClient 5.1, 6.1 and 7.  Npgsql 8 is the oldest line still patched and the first with the opt-in model the enum policy relies on.  SqlClient 5.1 is the first that reads `DateOnly` and `TimeOnly`; its mappings for every type in scope equal 7.1's.
- The map tracks the driver by **feature, detected from the compilation by symbol**, never by version number.  SqlClient's assembly version is the major alone, `6.0.0.0` for both 6.0 and 6.1, so a version cannot tell the json release from the vector release; a symbol can.  The markers are the features themselves: `NpgsqlDataSourceBuilder.EnableUnmappedTypes` for Npgsql 8, `NpgsqlTypes.NpgsqlCube` for Npgsql 10, `Microsoft.Data.SqlTypes.SqlJson` for SqlClient 6.0, `SqlVector<T>` for 6.1.  The probe is one `Select` over the compilation that returns a small value-equal record of flags and assembly versions, as the .NET floor probe is today, combined after the parse so that typing never re-reads a file.  The versions serve diagnostics only.

Recommended, the policies the maps follow:

- The default C# type for a database type is **the type the driver boxes**.  A generated method reads with `GetFieldValue<T>` and could ask for any type the driver converts to, but the models also serve a project that runs them through Dapper or its own code, where the boxed type is the one that arrives without conversion.  One default serves both.
- Where the two drivers disagree, each follows its own driver: PostgreSQL `date` and `time` are `DateOnly` and `TimeOnly`, which Npgsql 10 boxes; SQL Server's are `DateTime` and `TimeSpan`, which SqlClient boxes.  A project option switches either.  `timestamptz` is `DateTime` with `Kind.Utc`, with `DateTimeOffset` as an option.
- `json`, `jsonb`, `xml` and SQL Server's `json` are `string` by default; a library map can change that.
- A PostgreSQL array is `T[]` of the mapped element.  An array that holds nulls needs `T?[]` for a value type and the server cannot say; the override is the fix.
- A PostgreSQL domain maps as its base type.  A PostgreSQL enum is `string` by default, which needs `EnableUnmappedTypes()` on the data source in Npgsql 8 and later, and the override maps it to a C# enum for a project that calls `MapEnum<T>()`.
- A type the driver reads only with an extra package, `hierarchyid` and `geography` through `Microsoft.SqlServer.Types`, PostGIS through NetTopologySuite, is covered by that library's map, not by the base map.  A type the driver cannot read at all is a diagnostic that names the type and the override.
- A nullable column is the C# type with `?`: `Nullable<T>` for a value type, the annotation for a reference type.
- Facets, `varchar(50)` and `decimal(18,2)`, are dropped from the type, as every surveyed tool drops them, and kept in the description; the generator puts them in the member's documentation.
- The override is a project-level mapping from a database type name to a C# type, `timestamptz` to `DateTimeOffset`, `public.status` to `MyApp.Status`.  Its MSBuild form is phase 3's; its property starts with `SqlSource`, as every property of the package does.  A per-column C# type override, SQLx's `AS "created: DateTimeOffset"`, is not in this epic.
- The type map lives in the generator and never in the snapshot, so a better mapping needs no database, and one snapshot serves two projects that map differently.
- The map yields two things for a parameter: the C# type, and what the generated method sets on the `DbParameter`: `DbType` where it is enough; on PostgreSQL, `NpgsqlParameter.DataTypeName` with the type's name where it is not, `jsonb`, `int[]`, `public.user_status`, which the sidecar already holds, which Npgsql has accepted since 4.0 and which from 10.0 takes precedence; on SQL Server, `SqlDbType` with the facets for size, precision and scale, which the description carries.  Generated code never names an `NpgsqlDbType` member, so it compiles against every supported Npgsql without a conditional.
- Below the floor: one error per project naming the version found and the floor; models are still emitted, execution methods are not.  Driver absent: models are emitted, since a models-only project is legitimate, with an error only at a column whose type lives in `NpgsqlTypes`.  Newer than the generator knows: emit for the newest known flags and report an informational diagnostic, not a warning, since `TreatWarningsAsErrors` is common.  The README states the floor per dialect, as Kiota documents the runtime versions its output needs.
- Npgsql 10's change of `date` and `time` to `DateOnly` and `TimeOnly` affects only non-generic reads.  `GetFieldValue<DateOnly>` works from Npgsql 6 and `GetFieldValue<DateTime>` still works on 10, so the generated methods are unaffected and the type map's default need not vary by driver version; the probe can at most warn when a project option disagrees with what the referenced driver boxes.

### Models

Decided:

- **Names.**  A query `GetUser` gets `GetUserParams` when it has parameters and `GetUserDto` when it has a result set.  The suffixes are settings: `SqlSourceInputModelSuffix` and `SqlSourceOutputModelSuffix` as project properties, as `AdditionalFiles` metadata, as `InputModelSuffix` and `OutputModelSuffix` on the attribute, and as the preamble-only markers `-- input-model-suffix:` and `-- output-model-suffix:`.  One query's names are set whole with the markers `-- input-model:` and `-- output-model:`, which take the full name; a name that is not an identifier, or an input-model marker on a query with no parameters, is an error.
- **Namespace.**  Models are namespace-level types, not nested, in the namespace of the attributed type by default.  `SqlSourceModelNamespace` as a property, as metadata, as `ModelNamespace` on the attribute, and as the preamble-only marker `-- model-namespace:` change it.  A `-- input-model:` or `-- output-model:` name that contains a period is fully qualified and ignores all of those.
- **Accessibility** follows the attributed type: a `public` type gets public models, an `internal` one internal.  An override is not in this epic.
- **Shape.**  `SqlSourceInputModelType` and `SqlSourceOutputModelType` take `record`, `sealed record`, `class` or `sealed class`, default `sealed record`, as a property, as metadata, as `InputModelType` and `OutputModelType` on the attribute with a `GeneratorModelType` enum of the four values, and as the markers `-- input-model-type:` and `-- output-model-type:` in the preamble or a query.  A record is positional, in the model's property order, which gives value equality, `with` and deconstruction.  A class has public properties with get and set and no constructor of its own.
- **Order.**  Properties are in the query's order: the parameters as the parameter list rule orders them, declared-only ones last, and the columns as the select list has them.  No custom ordering.
- **Sorting** is two generator parameters, `sort-input` and `sort-output`, sorting the properties by name, ordinal comparison.
- **Generator parameters get every level.**  `SqlSourceGeneratorParameters` as a project property, as `AdditionalFiles` metadata and as `Parameters` on the attribute holds the same space-separated list that the `-- generator:` marker holds.  Every switch, present and future, has every level at once.  `SqlSourceTokenValidation` is folded into it: `SqlSourceGeneratorParameters=no-token-validation` says the same thing, and the package has one mechanism for switches rather than one property per switch.
- **Each level sets the list; none adds to it.**  The list in effect for a query is the one from the most specific level that gives one, whole: the query's marker, else the file's, else the attribute's, else the metadata's, else the property's, else empty.  A level that omits a parameter gets that parameter's default, so the vocabulary is only departures from the default, `keep-comments`, `no-token-validation`, `sort-input`, `sort-output`, `no-table-models` and `token-ignore=name`, and `token-validation` is dropped, since omitting `no-token-validation` says the same.  A level that wants to keep a parameter from above restates it.  The word `default`, alone, sets the empty list: `-- generator: default`, or `default` as the property's, the metadata's or the attribute's value.  It is a no-op when the levels above are at their defaults, which is the point: a reader sees that the query means the defaults.  A `-- generator:` with nothing after it is an error, since an empty marker reads as a mistake, and `default` beside another parameter is an error too.  This is the rule the dialect's options already follow, where a value replaces the one it wins over whole, and the README states it in one sentence beside the list.
- **Table models.**  A query whose result is exactly a table, as the sidecar's `matchesTable` records, gets a model named after the table instead of after the query, shared by every query that matches that table.  The result is exactly a table when every column has an origin in that one table, the names are the table's column names unchanged, the columns are the table's full column list in the table's order, and each column's nullability is the table column's.  `SELECT *` and an explicit full list in table order qualify; an alias, a dropped column, a reordering, an added expression or an outer join does not.  Requiring the order keeps positional records right for Dapper users and makes the rule one sentence: the select list is the table's column list.  A query's own `-- output-model:` wins over the table's name.
- **A shared name is a shared model.**  Two queries whose output models resolve to one full name get one model, emitted once, when their columns' names, types and nullability are identical, and an error at the second query's marker when they are not.  The same holds for input models and parameters.  This is how a project shares a model between queries that read the same shape: under its control, by one marker per query.
- **Property names** come from column and parameter names by PascalCasing: `created_at` to `CreatedAt`.  A name that is not an identifier after that, or two columns with one name, which `SELECT a.id, b.id` produces, is an error that asks for an alias.
- **Documentation.**  Each generated type and member has XML documentation: the query's summary, the database type and facets of each member, and the origin table and column when there is one.

Recommended:

- **No deduplication by shape.**  Two queries with identical shapes have two names, and a model shared by inference has to take one of them, which is arbitrary, and unstable: a query added earlier in the file, or a column added to one of the two, renames or splits the shared model and breaks code far from the edit.  Table models share under a name that comes from the schema and cannot drift; the shared-name rule shares under a name of the user's choosing.  Between them, shape-matching heuristics have no place.
- **Table model names and default.**  PascalCase of the table name plus the output suffix: `users` gives `UsersDto`.  No singularising, since inflection is wrong often enough in English and always elsewhere.  The schema is dropped when it is the engine's default, `public` or `dbo`, and prefixed otherwise, so `billing.invoices` gives `BillingInvoicesDto` and cannot collide with `public.invoices`.  Table models are on by default and `no-table-models` turns them off.  The README states the one consequence worth knowing: a query that stops matching, because the table gained a column and the query lists its columns, gets a model of its own name again, which is a compile error wherever the table model was used; `SELECT *` queries never pay it.
- **Dapper and sorting.**  Dapper matches a constructor's parameters to the columns in order, so a sorted positional record no longer maps through Dapper; the generated methods call the constructor themselves and are unaffected.  The README says that a project that runs sorted models through Dapper uses `class` models, and that snake_case columns need `DefaultTypeMap.MatchNamesWithUnderscores` there.
- The attribute ends with `Path`, `Target`, `Output`, `InputModelSuffix`, `OutputModelSuffix`, `ModelNamespace`, `InputModelType`, `OutputModelType` and `Parameters`.  Every one is optional and follows one rule.  The expected use is project-level defaults for most projects, `AdditionalFiles` metadata or file and query markers for the rest, and the attribute rarely; it is there so that the chain has no gap.

### Diagnostics

A user who reports "the model for this query is wrong" will usually share neither the schema nor the SQL.  The describer is a pipeline, and the step that produced the wrong answer has to be found from what the user will share.  Four layers, each cheaper to share than the next.

Decided:

- **Provenance in the sidecar.**  Informational fields that say which step decided a value; the generator never branches on them, and a reader ignores an unknown value.  `nullableSource` on every column: `server`, `catalog`, `outer-join`, `view`, `no-origin`, `heuristic`, with `outer-join` beating `view` beating `catalog` inside the walk.  `plan` on every entry with rows: `not-needed`, `walked`, `unavailable`, `skipped`, which tells a `heuristic` apart between "the walk could not match this origin", a walk bug, and "`EXPLAIN` failed".  `tableMatch` on every entry with rows: the first failing check in a fixed order, `matched`, `no-origin`, `several-tables`, `names-differ`, `columns-differ`, `order-differs`, `nullability-differs`.  `typeSource` gains `inferred-from-copies` for SQL Server's rename-and-unify.  The [sidecar format design](2026-10-07-sidecar-format-design.md) has the fields.  Nothing else goes into the sidecar: no plans, no timings, no catalog rows.
- **A log file.**  `--verbose` writes prose progress to the console for the person watching; `--log <path>`, or `SQLSOURCE_LOG`, writes JSON Lines for the run, one object per event, overwritten each run.  Per run: tool, format, runtime and OS versions, the command line with connection values replaced.  Per database: logical name, engine, driver assembly version, server version, and the session settings that change a description, `search_path`, `plan_cache_mode` and `standard_conforming_strings` on PostgreSQL, `compatibility_level`, `ANSI_NULLS` and `QUOTED_IDENTIFIER` on SQL Server; never host, user or database name.  Per query, in pipeline order with timings: the SQL as sent, every description as received verbatim, the lexer-versus-Npgsql check, every catalog query and its rows, the exact `EXPLAIN` statement and its JSON, one event per plan node with its join type, parent relationship and the nullable side set after it, one event per column with its origin, the node it matched and its source, the table match, and the result or the server's error verbatim.  A payload over 1 MiB is cut and marked.  Never written: a connection string or any keyword of it, a password, token or certificate, the environment.  Row data cannot exist, since nothing executes; literals do reach the log through the SQL and the plan's filter strings, and the log is the user's own file.
- **Driver logging** is wired into the same file when `--log` is given: Npgsql through its logger factory at Debug with parameter logging off, SqlClient through its event source at Informational with execution and trace keywords.  Enough to see the wire-level order and which statement failed without a second tool.
- **The seam.**  Each describer depends on one interface whose every method is a request and an answer: server info, derive parameters, describe columns, explain, attributes, types, table columns, and the SQL Server equivalents.  Three implementations: live over the driver; recording, which wraps live and appends every call and answer to a capture; replaying, which answers from a capture keyed by method and a hash of the arguments, and fails loudly on a call the capture lacks, so that a pipeline change that asks a new question is a visible fixture update.  The nullability matrix is recorded once against a container and then runs on every CI machine without Docker; a user's bundle becomes a regression test by copying one file.
- **Error messages.**  The first line is the compiler's form; continuation lines carry `query:` with the query name, logical database, engine and server version, driver and version; `step:` from a fixed list, `describe parameters`, `check parameters`, `describe columns`, `catalog`, `explain`, `walk`, `table match`, `resolve type`, `write sidecar`; `server:` with the SQLSTATE or error number and message verbatim, never paraphrased; `help:` with the fix; `see:` with the link.  One id per actionable cause, not per SQLSTATE: PostgreSQL's `42P18` and `42P08` get their own, every other server rejection shares one.  The text states the problem and only `help:` suggests the fix.  No per-error verbose hint; a failed run ends with one line that says how to get a log or a bundle.
- **A bug-report bundle and replay**, phase 7.  `sqlsource diagnose --query Users.sql:GetUser` re-describes one query and writes a zip holding the entry, the capture, the walk events and the log, with identifiers obfuscated by keyed hashing per class, schema, table, column, alias, function, type and literal, and the mapping written beside the zip, never inside it.  Obfuscation is dictionary-based from the vocabulary the tool already holds, so the tree, the join types, the origins and the `NOT NULL` flags survive and the walk gives the same answer on the bundle.  The engine's own names, `pg_catalog`, `public`, `sys`, `dbo` and built-in functions and types, stay verbatim.  `sqlsource replay <bundle>` runs the pipeline from the capture with no database and diffs its entry against the bundle's: a reproduction means the bug is in the pipeline and the bundle is a fixture; no reproduction means the live step diverged, and the driver lines are the next read.

Recommended:

- The diagnostic ids are a new range, `SQLSRC2xx`, for the generator's and the tool's conditions alike: a stale, missing or orphaned snapshot entry; a sidecar whose engine is not the file's dialect; version mismatches; an unmapped type; an unsupported type; a parameter with no type and no marker; a column with no name; two columns with one name; a name that is not an identifier; the server's errors; the lexer-versus-Npgsql disagreement.  The tool uses the generator's ids where the condition is shared.  `docs/diagnostics.md` gains each one in the phase that adds it.
- Fixtures live under `tests/SqlSource.Tool.Tests/Fixtures/<engine>/<case>/` as `query.sql`, `capture.json`, `expected.json` and an optional `issue.md`, with `capture.json` byte for byte the bundle's.  A `captureVersion` and a script that re-records the unobfuscated fixtures against the containers mitigate the coupling of captures to the interface's shape; an obfuscated fixture that outlives its interface version is deleted and its case re-tested by hand.
- `--log` without `--verbose` keeps the console quiet; `--verbose` without `--log` writes nothing to disk.  There is no `--diagnostics <dir>`; the bundle is the directory-shaped output.

### Testing

Decided: one end-to-end test project per supported database, each with its own `.sql` files, a schema script, and **committed sidecars** so that the project compiles with the generator like any consumer.  Each runs against a Testcontainers instance with the schema applied and has three kinds of test:

1. The tool in `--check` mode against the container, asserting that the committed sidecars are what the tool produces today.  This proves the describer.
2. Every query executed through its generated method, asserting the typed results, nullability included.  This proves the type map and the generated ADO.NET code at run time.  Until phase 5, the projects execute through Dapper, and a Dapper test stays afterwards for the subset of users who run the models that way.
3. The error paths, a stale sidecar and an undescribable query, by running the tool against a scratch copy.

Recommended:

- The projects live under `tests/`, one per database, named for it.  They reference the generator the way `tests/SqlSource.Tests` does and the tool as a project.  Docker is already a required tool, so Testcontainers adds nothing to the setup; `CONTRIBUTING.md` says which images the tests pull.
- The tool's own unit tests, the snapshot round trip, the hash, the parameter lexeme and the type map need no database and live with the existing tests.  The describer pipelines are tested from recorded fixtures through the replay seam, without a database; the container tests prove the live implementations and record the fixtures.
- The nullability inference for PostgreSQL gets a test matrix of its own: left, right and full joins; nested joins; a self-join on both sides; a `LEFT JOIN` reduced to inner by a `WHERE` and by a later inner join; an outer join inside a subquery and inside a CTE; `LEFT JOIN LATERAL`; a view over an outer join; `USING` and `NATURAL`; aggregates over an outer join with a plain group key; `UNION` of two outer-join queries; and each plan shape, hash, merge and nested loop, by toggling `enable_hashjoin`, `enable_mergejoin` and `enable_nestloop`.
- `tools/check-package-install.sh` gains a run of the packed tool, so the packed tool and the packed generator are proven together once per build.  Whether that run needs a database, and so Docker in that script, is phase 6's to settle; a `--check` against a committed sidecar with no changes may be enough to prove the packaging.

### Documentation

Each phase keeps the documents current, by the rules in `AGENTS.md`:

- `README.md`'s installation section changes with phase 3: add the package and the tool, set the dialect, run `describe`, commit the sidecar; and a one-line note that a project wanting only the constants sets `SqlSourceOutput=Sql` and can keep the default dialect.  It gains a section on models: running `describe`, committing the sidecar, the overrides, the type map and its option, the Dapper settings a user who stays with Dapper needs, and what is unsupported.  Phase 3 writes it for PostgreSQL; phase 4 adds SQL Server; phase 5 adds the generated methods; phase 6 adds the online mode.
- `CONTRIBUTING.md` gains the tool project, the end-to-end projects and their images, and the new package check.
- `docs/publishing.md` covers the second package from phase 2.
- `docs/diagnostics.md` gains each diagnostic in the phase that adds it.

## Phase 0 - names

### Scope

1. `SqlQueriesAttribute` becomes `SqlSourceGenerateAttribute`, used as `[SqlSourceGenerate]`; `SqlQueriesMode` becomes `GeneratorTarget`; the attribute's `Mode` property becomes `Target`.  The values `Nested` and `Direct` and the `Path` property are unchanged.
2. `-- SqlSource:` becomes `-- generator:`, and its `dialect=` directive becomes the marker `-- dialect: postgres`.  What a `-- generator:` line holds is called a generator parameter, not a directive, in the code and the documents.
3. Every place that names them: the generator's emitted source, the suppressor, the parser, the README, `docs/diagnostics.md`, the tests, and `tools/package-install`.

### Decided

- Renames and nothing else, in one pull request with one commit per rename, so that each diff is mechanical and the later phases start from the new names.
- The rule the README states for `.sql` files: a line comment of the form `-- word: rest` whose word SqlSource knows is a marker; nothing else in a comment is read.  Each marker has its own grammar for the rest.  `-- generator:` is the marker whose rest is a list of generator parameters: `keep-comments`, `token-validation`, `no-token-validation` and `token-ignore=name`, the switches that have no value of their own.
- A setting with one value, and an MSBuild property and metadata beside it, is a marker: `-- dialect:` now, `-- database:` and `-- output:` in phase 1.  The dialect marker keeps the placement rule the directive had, before the first `-- name:` line and before any SQL, since it changes how the lines after it are lexed.
- This is the last phase that can rename what a user writes at no cost, so it ships before anything else does.

### Technical notes

- The diagnostics that mention the directive form, the invalid dialect in a file and the misplaced dialect, are reworded, keep their ids, and their sections in `docs/diagnostics.md` follow.
- The parser's names for the directive scope and the preamble dialect follow the rename, so that later phases do not read "directive" in code and "marker" in the documents.

### Testing

The existing tests, under the new names and markers.  The package-install check proves the attribute reaches a consumer under its new name and that a `.sql` file with the new markers builds.

## Phase 1 - parameters and settings

### Scope

1. A `Parameter` lexeme kind in `SqlLexer`, found by the rule under Parameters, under every dialect.
2. `SqlBlock` and `SqlQuery` carry the ordered list of parameter names.  The segments are unchanged: a parameter stays in the SQL as written.
3. The hash of a query's SQL, computed where the generator builds the emitted text, so that the tool and the generator cannot disagree.
4. The `-- param:` marker: parsed, validated and carried on the block, with no effect yet, and the parameter list rule under Parameters.
5. Token defaults: the inline `{{name:default}}` form in the token scanner, the once-only marker, the conflict errors, and each token's resolved default carried on the block.  The hash and the parameter list are computed over the sample SQL.
6. The `-- database:` and `-- output:` markers, in the preamble and inside a query, carried on the block; the `SqlSourceDatabase` and `SqlSourceOutput` property and metadata in the props and targets; the `GeneratorOutput` enum and the attribute's `Output` property; the settings record that resolves all three settings by one rule, per type and query for the output.  `output` is validated and has no effect yet; `database` is never read by the generator.
7. The model settings under Models, each at every level: the suffixes, the namespace, the model types with the `GeneratorModelType` enum, the per-query names, and `SqlSourceGeneratorParameters` with its metadata and attribute property, into which `SqlSourceTokenValidation` is folded.  The `sort-input`, `sort-output` and `no-table-models` parameters are accepted and `token-validation` is dropped; each level sets the list whole.  All validated, none with an effect before phase 3.
8. `InternalsVisibleTo` for `SqlSource.Tool`.

### Decided

- Parameters are found by the lexer, under the file's dialect.
- Nothing a user sees changes beyond the attribute's new properties, which are accepted and have no effect yet, and `SqlSourceTokenValidation` giving way to `SqlSourceGeneratorParameters`.  No member is generated from the parameter list, and the only diagnostics added are invalid setting values and whatever the new markers need.

### Recommended

- The marker is `-- param: <prefix><name> [<database type>] [null]`, as under Overrides, matched like the other markers.  The type is the rest of the line before an optional trailing `null`, so a type with facets, `decimal(18,2)`, or with spaces, `double precision`, needs no quoting.
- The hash is SHA-256 of `engine + "\n" + SQL`, hex-encoded, where the SQL is the comment-stripped emitted text with `\n` line endings.  `System.Security.Cryptography.SHA256` is available to a `netstandard2.0` analyzer.

### Technical notes

- MySQL and MariaDB read `@name` as a user variable and their connector treats it as a parameter by default; the rule is the same for them.  Oracle uses `:name`.  The lexeme's prefix is a dialect rule, like the quote readers, even though every dialect in scope uses `@`.

### Testing

Theory tests over the rule under each dialect, including `@` inside strings, comments and quoted identifiers, the operators that contain `@`, `@@` functions, and a parameter at the start and end of the text.  Round-trip tests that the hash is stable across line endings and `keep-comments`.

## Phase 2 - the tool and the snapshot

### Scope

1. `src/SqlSource.Tool`: the console application, its package, its command line, its exit codes and its error output format.
2. Finding the unit of a run, listing a solution's projects, the `SqlSourceImported` marker, the manifest target in `SqlSource.targets` and its format, and running it to get the compiler's view of the `.sql` files with their dialects, databases and outputs, and of the `Compile` items.  Reading the `[SqlSourceGenerate]` attributes from the C# files with the generator's reader over syntax trees, resolving each type's files with the generator's path resolver, resolving the three settings per type and query with the shared code, and leaving out every unclaimed file and every project, type, file and query whose output is `Sql`.
3. The snapshot model, the shared reader and writer, and the format version, as the [sidecar format design](2026-10-07-sidecar-format-design.md) specifies; the JSON Schema under `schemas/`.
4. `IQueryDescriber`, the engine-neutral `QueryDescription`, and the PostgreSQL describer over Npgsql, with nullability inference, behind the describe-session seam with its live, recording and replaying implementations.
5. The provenance fields, the `--verbose` console and `--log` file with the events under Diagnostics, driver logging wired in, the error message format, and the fixture layout, with the nullability matrix recorded as fixtures.
6. `sqlsource describe`, `--check`, `--force`, `--database`, the connection options and the environment variables.  Token defaults substituted before describing; a token without one is an error.  After describing, the table match: for a result whose columns all originate in one table, the table's column list from `pg_attribute` or `sys.columns`, compared as Models defines, recorded as `matchesTable`.
7. Publishing for the second package.

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
3. **Nullability.**  The protocol does not report it, and the one way the origin's `attnotnull` is wrong is the unsafe one: a `NOT NULL` column from the nullable side of an outer join.  Three layers, decided:
   - *The origin.*  `attnotnull` of the origin column from step 2; unknown when there is no origin.  A view's columns are unknown by construction, since their `pg_attribute` rows never say `NOT NULL`, so a view that hides an outer join is already safe.
   - *The gate.*  The lexer looks for `LEFT`, `RIGHT`, `FULL` or `OUTER` as a word outside strings, comments and quoted identifiers.  A query without one stops at the origin: no `EXPLAIN`, no dependence on a plan.  The gate is safe because the one way an outer join hides from the text, a view, is already unknown.
   - *The plan walk*, for gated queries.  The plan comes from `EXPLAIN (GENERIC_PLAN, VERBOSE, FORMAT JSON)` on 16 and later, and below that from `PREPARE`, `SET LOCAL plan_cache_mode = force_generic_plan` and `EXPLAIN EXECUTE` with nulls, in a transaction that is rolled back; a generic plan, so that `WHERE x = $1` is not folded for the null placeholder.  The root node's `Output` is the result's target list in order, so column *i* is `Output[i]`; a plain column reference prints as `alias.column`, which names the relation instance and so handles a self-join, where the origin's table OID cannot say which side a column came from.  A relation instance is nullable when any join on the path from the root to its scan null-extends its side: for `Join Type` `Left` the child whose `Parent Relationship` is `Inner`, for `Right` the child that is `Outer`, for `Full` both; `Inner`, `Semi` and `Anti` null-extend nothing.  A `Subquery Scan` or `CTE Scan` maps its outputs to its child plan's by position and recurses, the CTE's plan found by its `Subplan Name`.  An `Output` entry that is not a plain reference, an expression or an aggregate, stays unknown; a `GROUP BY` key that is a plain reference keeps its origin's answer.  A column is non-null only when its origin says `NOT NULL` and the walk found its relation instance under no null-extending side.
   - *The safety net.*  A join type the walk does not know, an `Output` entry it cannot map, a plan it cannot obtain, or CockroachDB, drops the query to the heuristic: every column of a gated query is nullable.  So the walk is wrong only where its join-side rule is wrong, and the matrix under Testing exists to prove that rule; SQLx's one open bug is a `Hash Right Join` whose sides it read backwards, and the matrix forces that shape by toggling the planner's join methods.
   - The plan beats the text because the planner has applied join strength reduction: `A LEFT JOIN B ... WHERE b.x IS NOT NULL`, or a `LEFT JOIN` followed by an inner join on the joined table, is planned as an inner join, and the walk says non-null, which is correct and which no text rule gets right.
   - The outcome, `true`, `false` or `null`, is what the sidecar records, with the layer that decided it as provenance (see Diagnostics).  The override suffix and the policy that unknown means nullable are the generator's, applied when it reads.
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
- Unverified, and the first thing this phase's spike settles: that `SchemaOnly | KeyInfo` together behave as described in Npgsql; that `EXPLAIN (GENERIC_PLAN)` accepts `$n` through the extended protocol, or whether the `PREPARE` form is needed on 16 too; and what `Describe` reports for a domain-typed expression and for a `USING` column of a `FULL JOIN`.
- The tool must leave the database as it found it: a `PREPARE` is deallocated, a `SET` is session-local, nothing is executed.

### Testing

The snapshot round trip and the command line without a database.  The describer against a PostgreSQL container, with the nullability matrix under Testing above.  The end-to-end project for PostgreSQL is phase 3's, since it needs the models; this phase's container tests assert descriptions.

### Documentation

`CONTRIBUTING.md` for the new project and its tests.  `docs/publishing.md` for the second package.  The README waits for phase 3, when a user can do something with the output; this phase's package is published so that phase 3 can be tried against it.

## Phase 3 - models

### Scope

1. The generator's pipeline reads `.sql.json` sidecars as a second file kind, paired by path, parsed with the shared reader into a value-equal record, cached like a parsed `.sql` file.  `SqlSourceOutput` takes effect: `Models` and `CodeGen` queries need an entry, `Sql` queries do not, and a `Models` or `CodeGen` query with a token that has no default is an error.
2. Hash and version comparison per query, and the diagnostics under The sidecar.
3. The PostgreSQL type map, every type Npgsql reads; the driver probe with its feature flags and floor diagnostics; the library maps for NodaTime, NetTopologySuite, `JsonDocument` and `BigInteger` with their detection and properties; and the project-level mapping override.
4. Emission of the input and output types with documentation, and the override suffix stripped from names.
5. The PostgreSQL end-to-end project.
6. The README section on models.

### Decided

- The generator never connects to anything.  Its inputs are the project's files and MSBuild values.
- Stale snapshots never produce models.

### Recommended

The items under Models and Types in C#.  Two more:

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
2. The SQL Server type map, every type SqlClient reads, its driver probe, and the library maps for NodaTime by conversion, NetTopologySuite through the bytes reader, `JsonDocument` and `SqlJson`.
3. The SQL Server end-to-end project.
4. The README's SQL Server additions.

### Recommended

The describer:

1. **Parameters.**  `sp_describe_undeclared_parameters @tsql` returns one row per undeclared `@name` with `suggested_system_type_name`, facets included, and `suggested_user_type_*` for alias and CLR types.  The engine takes the type from the innermost enclosing comparison, assignment, function argument, `INSERT` value or `CAST`.  A parameter used more than once is error 11508, which the common optional-filter pattern `WHERE (@name IS NULL OR name = @name)` triggers.  The tool renames each occurrence, `@name` to `@name_1`, `@name_2`, with the lexer, describes, and accepts the result when every copy came back the same type; copies that disagree are an error that names the parameter and the two types.  This is FSharp.Data.SqlClient's workaround without its T-SQL parser.  A `-- param:` type is declared in `@params` instead.
2. **Columns.**  `sys.dm_exec_describe_first_result_set(@tsql, @params, 1)`, the table-valued form that returns a failure as a row with `error_number`, `error_message` and `error_type` instead of raising it.  `@params` is a declaration string built from step 1; the procedure requires every parameter declared, which is why parameters come first.  Browse information fills `source_schema`, `source_table` and `source_column` and adds hidden key columns that the tool drops by `is_hidden = 1`.  A batch with no result set returns no rows.  A column with `name` null, `SELECT 1`, is an error that asks for an alias.
3. **Nullability** is the engine's: outer joins, `CASE`, `ISNULL` never null, `COALESCE` null unless every argument is non-null, and "1 if it can't be determined".  The tool records it as given; the override is the generator's.

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

## Phase 5 - execution methods

### Scope

1. For each query whose output is `CodeGen`, a generated method that takes a `DbConnection`, the parameters type when there is one, and a `CancellationToken`; creates the command; binds each parameter with its mapped `DbType` or provider type and facets; and executes it.
2. For a query with a result set, reading each row into the output type by ordinal with `GetFieldValue<T>`, with `IsDBNull` for nullable members.
3. The method's shape per query: how many rows it returns.
4. Both engines, in the end-to-end projects, executing through the generated methods.

### Decided

- Plain ADO.NET over `DbConnection`, `DbCommand`, `DbParameter` and `DbDataReader`.  The provider's own types only where ADO.NET's abstractions do not reach.  Nothing generated depends on Dapper.
- A project that does not want the methods still gets the models.

### Recommended

- Asynchronous methods only, named after the query with an `Async` suffix, static, placed where the query's constant is.
- The result shape is the user's to say, since the description cannot: sqlc uses `:one`, `:many`, `:exec` and `:execrows` on its name marker.  A marker or generator parameter, settled in this phase's spec with its grammar added to the lexer, chooses between one row or null, a list of rows, and the affected-row count for a query with no result set.  The default when nothing is said: a list for a query with rows, the count for one without.
- An open transaction is passed as an optional `DbTransaction`.  Opening the connection is the caller's business.
- Reading by ordinal, not by name: the snapshot fixes the ordinal of every column, and the hash guarantees the SQL is the one described.  A type the provider cannot convert to the mapped C# type is a bug in the type map, caught by the end-to-end tests.

### Technical notes

- `GetFieldValue<DateOnly>` and `GetFieldValue<TimeOnly>` work on Microsoft.Data.SqlClient 5.1 and later and on Npgsql 6 and later; the type map's options for those types are honoured by asking for the chosen type.
- A parameter whose PostgreSQL type `DbType` cannot name, an array, `jsonb`, an enum, a range, is bound by setting `NpgsqlParameter.DataTypeName` to the type's name from the sidecar.  Generated code therefore casts to or constructs an `NpgsqlParameter`, which it does for the `postgres` dialect anyway, and never names an `NpgsqlDbType` member.  SQL Server's `json` binds as `nvarchar` on every version; `SqlDbTypeExtensions.Json`, in the `Microsoft.Data` namespace, waits for a phase that maps `SqlJson` and is gated on its flag.
- A `SqlParameter` for `decimal` needs precision and scale set, or the server rounds; for `nvarchar(n)` a size, or the plan cache fragments.  The description's facets give both.

### Testing

The end-to-end projects run every query through its generated method and assert rows, counts, nulls and cancellation.  Generator tests cover the method shapes and the parameter binding code for every row of the type map.

## Phase 6 - build integration

### Scope

1. The online-mode target in `SqlSource.targets`.
2. `sqlsource describe --watch`.
3. The packed tool in `tools/check-package-install.sh`.
4. The README's online-mode section and `CONTRIBUTING.md`.

### Recommended

The items under Workflow.  The target runs the tool through `dotnet sqlsource`, so a project without the tool manifest gets a clear error.  It must not run in a design-time build, and it must not run when the variable is unset.

### Technical notes

- The target runs before `CoreCompile`, after the package's own targets that collect `AdditionalFiles`, so the tool and the compiler see the same files.  A sidecar the tool writes during the build is read by `CoreCompile` in the same build; the IDE's generator run sees it afterwards.

## Phase 7 - bug reports

### Scope

1. `sqlsource diagnose --query <file>:<name>`: re-describe one query with logging, obfuscate, write the bundle and the mapping file.
2. The obfuscation: keyed hashing per identifier class, dictionary-based replacement over the structured plan fields, the plan's expression strings, the SQL and the catalog rows, and a `--scrub` that obfuscates a bundle made with `--no-obfuscate`.
3. `sqlsource replay <bundle>`: run the pipeline from the capture, print every walk and table-match decision, diff the entry.
4. The README's bug-report section and the issue template that asks for a bundle.

### Decided

- The items under Diagnostics.  The mapping file is never inside the bundle.

### Testing

Obfuscate the fixtures and assert that replay gives the same provenance on the obfuscated capture as on the original, for both engines.

## Out of scope for the epic

- Multiple result sets, stored procedures and table-valued functions.
- Databases other than PostgreSQL and SQL Server.  The description's optional fields and the `-- param:` type marker are the room left for them.  SQLite would need a static describer; MySQL, MariaDB and Oracle need declared parameter types.
- Using a token's default as the C# default value of the generated method's parameter.  A default is a sample for describing; whether it should also be a run-time default is a separate question.
- Synchronous execution methods, streaming results as `IAsyncEnumerable<T>`, and bulk operations.  The method shapes in this epic are one row, a list and a count.
- Per-column C# type overrides.  Table-valued parameters.
- A schema fingerprint in the snapshot.  Starting a database from the tool.

## Research

The epic was preceded by research into both approaches, which this outline condenses.  The facts that phase specs will lean on, with their sources:

- **Static analysis, prior art.**  sqlc parses with hand-written parsers it wrote in 2026 to escape native dependencies, builds its catalog from migration DDL and a 245 KB file of PostgreSQL functions generated from a live server, types unknown functions as `any`, and added a database-backed analyser in v1.19 and managed databases in v1.22.  SQLDelight's grammars are small because SQLite's type system is small.  Rezoom.SQL types SQL statically by owning its own dialect.  ScriptDom is MIT, `netstandard2.0`, 7 MB, and does no binding.  No managed PostgreSQL parser exists; the .NET bindings of libpg_query bundle a native library.  Nothing in .NET does schema-driven static inference of result types from SQL text.
- **Database-backed, prior art.**  SQLx's `.sqlx/query-<sha256>.json` holds `db_name`, `query`, `describe` with columns, parameters and a tri-state `nullable` list, and `hash`; `cargo sqlx prepare --check` for CI; `DATABASE_URL` for online and `SQLX_OFFLINE` for offline.  pgtyped speaks the protocol directly and does no outer-join analysis.  Prisma TypedSQL requires a database for `prisma generate --sql`.  FSharp.Data.SqlClient has used the two SQL Server procedures since 2014 and documents their limits.  SqlBound, a .NET package at release-candidate stage, does a prepare step with a committed JSON snapshot and a source generator.
- **Other engines** and what they can answer without executing: MySQL's `COM_STMT_PREPARE` gives column types, a `NOT_NULL` flag and origins but reports every parameter as `VARCHAR`; SQLite gives declared types for table columns only and nothing for parameters; Oracle's `OCI_DESCRIBE_ONLY` gives column types and nullability but the client declares bind types; DuckDB infers parameter types and gives column types but no nullability or origin.
- **Type mappings** of Npgsql 10 and Microsoft.Data.SqlClient, and Dapper's constructor and name matching, are as stated under Types in C# and Models.
- **Driver versions.**  Npgsql's assembly version is its package version; SqlClient's is the major alone, verified from the packages' metadata.  Npgsql 8, 9 and 10 and SqlClient 6.1 and 7 are the serviced lines; SqlClient 6.1 is the LTS until 2028.  `NpgsqlParameter.DataTypeName` has existed since Npgsql 4.0.  Dapper.AOT checks no driver version and stays on the ADO.NET surface; the EF Core Npgsql provider pins the driver major through its package graph; Kiota documents the exact runtime versions its output needs.
- **Type libraries.**  NodaTime has 338 million downloads and a first-party Npgsql plugin with 26 million; NetTopologySuite 249 million, with 36 million for the Npgsql plugin and 92 million for the SQL Server bytes reader.  `Microsoft.SqlServer.Types` is under a proprietary licence with redistribution limits.  Npgsql's own type plugins are exactly four: NodaTime, NetTopologySuite, GeoJSON and Json.NET; Pgvector is the only third-party one with comparable use.

### Sources

PostgreSQL: [protocol message formats](https://www.postgresql.org/docs/current/protocol-message-formats.html), [protocol flow](https://www.postgresql.org/docs/current/protocol-flow.html), [PREPARE](https://www.postgresql.org/docs/current/sql-prepare.html), [EXPLAIN](https://www.postgresql.org/docs/current/sql-explain.html), [error codes](https://www.postgresql.org/docs/current/errcodes-appendix.html), [pg_type](https://www.postgresql.org/docs/current/catalog-pg-type.html), [operator type resolution](https://www.postgresql.org/docs/current/typeconv-oper.html), [parse_target.c](https://github.com/postgres/postgres/blob/master/src/backend/parser/parse_target.c), [parse_param.c](https://github.com/postgres/postgres/blob/master/src/backend/parser/parse_param.c), [postgres.c](https://github.com/postgres/postgres/blob/master/src/backend/tcop/postgres.c).

Npgsql: [SqlQueryParser.cs](https://github.com/npgsql/npgsql/blob/main/src/Npgsql/SqlQueryParser.cs), [NpgsqlCommand.cs](https://github.com/npgsql/npgsql/blob/main/src/Npgsql/NpgsqlCommand.cs), [DbColumnSchemaGenerator.cs](https://github.com/npgsql/npgsql/blob/main/src/Npgsql/Schema/DbColumnSchemaGenerator.cs), [basic usage](https://www.npgsql.org/doc/basic-usage.html), [supported types](https://www.npgsql.org/doc/types/basic.html), [date and time](https://www.npgsql.org/doc/types/datetime.html), [enums and composites](https://www.npgsql.org/doc/types/enums_and_composites.html), release notes [6.0](https://www.npgsql.org/doc/release-notes/6.0.html), [8.0](https://www.npgsql.org/doc/release-notes/8.0.html), [10.0](https://www.npgsql.org/doc/release-notes/10.0.html).

SQL Server: [sp_describe_first_result_set](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-describe-first-result-set-transact-sql), [sys.dm_exec_describe_first_result_set](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-exec-describe-first-result-set-transact-sql), [sp_describe_undeclared_parameters](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-describe-undeclared-parameters-transact-sql), [errors 11000-12999](https://github.com/MicrosoftDocs/sql-docs/blob/live/docs/relational-databases/errors-events/includes/sql-server-2025-database-engine-events-and-errors-11000-12999.md), [SET FMTONLY](https://learn.microsoft.com/en-us/sql/t-sql/statements/set-fmtonly-transact-sql), [COALESCE](https://learn.microsoft.com/en-us/sql/t-sql/language-elements/coalesce-transact-sql), [json type](https://learn.microsoft.com/en-us/sql/t-sql/data-types/json-data-type), [data type mappings](https://learn.microsoft.com/en-us/dotnet/framework/data/adonet/sql-server-data-type-mappings), [CommandBehavior](https://learn.microsoft.com/en-us/dotnet/api/system.data.commandbehavior), SqlClient release notes [5.1](https://github.com/dotnet/SqlClient/blob/main/release-notes/5.1/5.1.0.md), [6.0](https://github.com/dotnet/SqlClient/blob/main/release-notes/6.0/6.0.0.md), [6.1](https://github.com/dotnet/SqlClient/blob/main/release-notes/6.1/6.1.0.md).

Prior art, database-backed: SQLx [query! macro](https://docs.rs/sqlx/latest/sqlx/macro.query.html), [describe.rs](https://github.com/launchbadge/sqlx/blob/main/sqlx-postgres/src/connection/describe.rs), [PR 3541](https://github.com/launchbadge/sqlx/pull/3541), [issue 3202](https://github.com/launchbadge/sqlx/issues/3202), [sqlx-cli](https://github.com/launchbadge/sqlx/blob/main/sqlx-cli/README.md), [offline data format](https://github.com/launchbadge/sqlx/blob/main/sqlx-macros-core/src/query/data.rs); pgtyped [actions.ts](https://github.com/adelsz/pgtyped/blob/master/packages/query/src/actions.ts), [sql files](https://pgtyped.dev/docs/sql-file); [Prisma TypedSQL](https://www.prisma.io/docs/orm/prisma-client/using-raw-sql/typedsql); FSharp.Data.SqlClient [home](https://fsprojects.github.io/FSharp.Data.SqlClient/), [DesignTime.fs](https://github.com/fsprojects/FSharp.Data.SqlClient/blob/master/src/SqlClient.DesignTime/DesignTime.fs), issues [13](https://github.com/fsprojects/FSharp.Data.SqlClient/issues/13), [34](https://github.com/fsprojects/FSharp.Data.SqlClient/issues/34), [263](https://github.com/fsprojects/FSharp.Data.SqlClient/issues/263); [SqlBound](https://www.nuget.org/packages/SqlBound).

Prior art, static: sqlc [config](https://docs.sqlc.dev/en/latest/reference/config.html), [managed databases](https://docs.sqlc.dev/en/latest/howto/managed-databases.html), [vet](https://docs.sqlc.dev/en/latest/howto/vet.html), [named parameters](https://docs.sqlc.dev/en/latest/howto/named_parameters.html), [output_columns.go](https://github.com/sqlc-dev/sqlc/blob/main/internal/compiler/output_columns.go), [resolve.go](https://github.com/sqlc-dev/sqlc/blob/main/internal/compiler/resolve.go), [seed.go](https://github.com/sqlc-dev/sqlc/blob/main/internal/engine/postgresql/seed.go), releases [1.19.0](https://github.com/sqlc-dev/sqlc/releases/tag/v1.19.0), [1.22.0](https://github.com/sqlc-dev/sqlc/releases/tag/v1.22.0), [oliphant](https://github.com/sqlc-dev/oliphant), [sqlc-gen-csharp](https://github.com/DaredevilOSS/sqlc-gen-csharp); SQLDelight [JoinClauseMixin.kt](https://github.com/sqldelight/sql-psi/blob/master/core/src/main/kotlin/com/alecstrong/sql/psi/core/psi/mixins/JoinClauseMixin.kt); [Rezoom.SQL](https://github.com/rspeele/Rezoom.SQL); [ScriptDom on NuGet](https://www.nuget.org/packages/Microsoft.SqlServer.TransactSql.ScriptDom); [libpg_query](https://github.com/pganalyze/libpg_query).

Other engines: MySQL [COM_STMT_PREPARE](https://dev.mysql.com/doc/dev/mysql-server/latest/page_protocol_com_stmt_prepare.html), [bug 23385](https://bugs.mysql.com/bug.php?id=23385); SQLite [column_decltype](https://sqlite.org/c3ref/column_decltype.html), [table_column_metadata](https://sqlite.org/c3ref/table_column_metadata.html); SQLx [sqlx-sqlite explain.rs](https://github.com/launchbadge/sqlx/blob/main/sqlx-sqlite/src/connection/explain.rs); Oracle [OCI statement functions](https://docs.oracle.com/en/database/oracle/oracle-database/19/lnoci/statement-functions.html); [DuckDB prepared statements](https://duckdb.org/docs/current/clients/c/prepared.html).

Drivers and libraries: [Npgsql release notes](https://www.npgsql.org/doc/release-notes/10.0.html), [NodaTime plugin](https://www.npgsql.org/doc/types/nodatime.html), [NetTopologySuite plugin](https://www.npgsql.org/doc/types/nts.html), [DataTypeName.cs](https://github.com/npgsql/npgsql/blob/main/src/Npgsql/Internal/Postgres/DataTypeName.cs), [SqlClient support lifecycle](https://learn.microsoft.com/en-us/sql/connect/ado-net/sqlclient-driver-support-lifecycle), SqlClient release notes [7.1](https://github.com/dotnet/SqlClient/blob/main/release-notes/7.1/7.1.0.md), [SqlDbTypeExtensions](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqldbtypeextensions), [NetTopologySuite.IO.SqlServerBytes](https://github.com/NetTopologySuite/NetTopologySuite.IO.SqlServerBytes), [EFCore.SqlServer.NodaTime](https://github.com/StevenRasmussen/EFCore.SqlServer.NodaTime), [Compilation.ReferencedAssemblyNames](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.compilation.referencedassemblynames), [Kiota](https://learn.microsoft.com/en-us/openapi/kiota/using), [Dapper.AOT](https://aot.dapperlib.dev/gettingstarted).

Dapper, Roslyn and MSBuild: [DefaultTypeMap.cs](https://github.com/DapperLib/Dapper/blob/main/Dapper/DefaultTypeMap.cs), [PR 2051 DateOnly](https://github.com/DapperLib/Dapper/pull/2051), [incremental generators cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md), [analyzer banned symbols](https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/Core/AnalyzerBannedSymbols.txt), [Testcontainers modules](https://dotnet.testcontainers.org/modules/).
