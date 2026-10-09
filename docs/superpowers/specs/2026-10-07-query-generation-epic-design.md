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
-- name: GetUser -> one-optional
-- summary: Loads one user by id.
SELECT id, name, created_at, deleted_at
FROM users
WHERE id = @id;
```

```csharp
[SqlSourceGenerate]
public partial class UserRepository(DbConnection connection)
{
    // Generated beside Sql.GetUser, in the namespace of this type:
    //   public sealed record GetUserParams(int Id);
    //   public sealed record GetUserDto(int Id, string Name, DateTime CreatedAt, DateTime? DeletedAt);
    // and on DbConnection, in a generated UserRepositoryExtensions class:
    //   public static Task<GetUserDto?> GetUser(this DbConnection connection, GetUserParams parameters,
    //       DbTransaction? transaction = null, int? commandTimeout = null, CancellationToken cancellationToken = default);
    public Task<GetUserDto?> Get(int id, CancellationToken ct) => connection.GetUser(new GetUserParams(id), cancellationToken: ct);
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
| 0. Names | Done | [query-generation-phase-0-names-design](2026-10-08-query-generation-phase-0-names-design.md) | `[SqlQueries]` becomes `[SqlSourceGenerate]`, `SqlQueriesMode` becomes `SqlLocation` and `Mode` becomes `SqlLocation`; `-- SqlSource:` becomes `-- generator:` and `dialect=` becomes the marker `-- dialect:`.  Through the generator, the parser, the README, the diagnostics, the tests and the package-install project.  Nothing else changes. |
| 1. Parameters and settings | Done | [query-generation-phase-1-parameters-and-settings-design](2026-10-08-query-generation-phase-1-parameters-and-settings-design.md) | The lexer finds `@name` parameters by the file's dialect; each query carries its ordered parameter list and the hash of its SQL; every new marker, setting, enum and generator parameter of the epic is parsed and validated with no effect yet.  A user sees four changes: `SqlSourceTokenValidation` gives way to `SqlSourceGeneratorParameters`; `token-validation` and `token-ignore=` are no longer generator parameters, and `-- token-ignore:` is a marker; a query's generator parameters replace the preamble's; and `{{a:b}}` is a token with a default, where it was text. |
| 2.1 The snapshot format | Done | [query-generation-phase-2-1-snapshot-format-design](2026-10-09-query-generation-phase-2-1-snapshot-format-design.md) | In the generator assembly: the model of a sidecar, the hand-written reader and writer, the two comparisons the format design defines, and the JSON Schema under `schemas/`.  No tool, and no step of the generator reads a sidecar yet. |
| 2.2 The tool's shell and its package | Not started | [query-generation-phase-2-2-tool-shell-design](2026-10-09-query-generation-phase-2-2-tool-shell-design.md) | `src/SqlSource.Tool` and `tests/SqlSource.Tool.Tests`: the command line, `Cli.Run(args)`, the exit codes, the error format, finding the unit of a run.  The `SqlSource.Tool` package: packed, checked, installed by a script, and published beside the generator. |
| 2.3 The project manifest and discovery | Not started | [query-generation-phase-2-3-project-manifest-design](2026-10-09-query-generation-phase-2-3-project-manifest-design.md) | `SqlSourceImported` and the `SqlSourceWriteManifest` target with its format and version; the tool lists a solution's projects, has MSBuild write each manifest and reads it; several target frameworks; a project that was never restored; `--project`. |
| 2.4 The work list | Not started | [query-generation-phase-2-4-work-list-design](2026-10-09-query-generation-phase-2-4-work-list-design.md) | The syntax-level attribute reader; the plan of a run from the shared path resolver, parser, settings and hash: which queries need an entry, their databases, and which are selected by `--database` and a `.sql` path. |
| 2.5 Describe | Not started | [query-generation-phase-2-5-describe-design](2026-10-09-query-generation-phase-2-5-describe-design.md) | The describer's interfaces and registry with no engine registered; the exchange's interface with its live shape; connections; the skip rule, the sidecar-written rule, `--force`, and the summary. |
| 2.6 `--check` and logging | Not started | [query-generation-phase-2-6-check-and-logging-design](2026-10-09-query-generation-phase-2-6-check-and-logging-design.md) | `describe --check` with its comparison and its exit code; `--verbose`; `--log` and `SQLSOURCE_LOG` with the run's events.  Closes phase 2. |
| 3. The PostgreSQL describer | Not started | | The describer over Npgsql behind the replay seam: parameters, columns, the nullability walk, the table match, provenance, the describer's log events, driver logging, and the recorded fixtures with the nullability matrix. |
| 4. Defaults | Not started | | The repository's own test projects, the Roslyn-floor project and the package-install project set `SqlSourceOutput` and dialects explicitly, so that the next phase's defaults, `CodeGen` under `ansi`, break nothing that exists. |
| 5. Models | Not started | | The generator reads sidecars, maps PostgreSQL types to C#, emits the input and output types with documentation, once per full name, and reports stale, missing and mismatched sidecars.  The PostgreSQL end-to-end project.  The first usable release. |
| 6. SQL Server | Not started | | The SQL Server describer, its type map, and its end-to-end project. |
| 7. Library maps | Not started | | The out-of-the-box maps for NodaTime, NetTopologySuite, `JsonDocument` and `SqlJson` on both engines, their detection from the compilation and their properties. |
| 8. Execution methods | Not started | | A generated asynchronous method per query, an extension method on `DbConnection` by default, that binds the parameters, runs the command and reads the rows into the output type by ordinal with typed getters, in the shape and collection type the query asks for, for both engines.  The end-to-end projects execute through them. |
| 9. Build integration | Not started | | The online mode: an MSBuild target runs the tool before compile when a connection is configured.  The `.env` file.  A watch mode.  The package-install check runs the tool. |
| 10. Bug reports | Not started | | `sqlsource diagnose`, the obfuscated bundle, and `sqlsource replay`, which runs the pipeline from a bundle's capture without a database. |

Each phase has its own spec, plan and pull request, and phase 2 has one of each for each of its six sub-phases: 2.1 can be built beside 2.2 to 2.4, which follow one another, and 2.5 and 2.6 need all four.  Phase 0 is a rename and nothing else, kept apart because it touches every file that names the attribute.  Phase 1 generates nothing new: what a user sees of it is the four changes its row lists, and the settings that are accepted and have no effect yet.  Phases 2 and 3 ship a tool whose output nothing reads yet; the package is published so that phase 5 can be tried against it.  Phase 4 is a repository-only change.  Phase 5 is the first release a user can use, for PostgreSQL with Dapper or their own ADO.NET code.  Phase 6 adds SQL Server, phase 7 the library maps on both.  Phase 8 delivers the third goal.  Phase 9 removes the need to run the tool by hand.  Phase 10 makes a bug report reproducible without the user's schema; the log and the replay seam from phases 2 and 3 serve maintainers until then.

The order puts the parameter lexeme first because every later phase depends on it and it is small; the tool before the generator because the generator's models cannot be tested without a snapshot to read; PostgreSQL before SQL Server because its nullability inference is the hardest single piece and should be proven early; the library maps after both engines so that each map is written against both at once; the execution methods after the describers so that their abstraction over the two providers is designed with both in hand; and the build integration last because it is convenience over a working whole.

Two terms, used throughout: the **sidecar** is the `.sql.json` file beside a `.sql` file, and **snapshot** is the same thing seen as a whole, the committed description of a project's queries.  The **project manifest** is the file the package's targets write under `obj` for the tool; the **tool manifest** is `dotnet`'s `dotnet-tools.json`, which pins the tool's version in a repository that uses it.  This repository's own does not pin `sqlsource`: its end-to-end projects use the tool as a project.

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

- A tool package carries its whole dependency closure in its own folder, so the generator DLL and Roslyn travel with it without packaging work.  The generator's Roslyn reference is `PrivateAssets="all"`, so it does not flow through the project reference; the tool references `Microsoft.CodeAnalysis.CSharp` itself, since the parser uses `TextSpan` and `SyntaxFacts` from it, and at a current version, not the generator's pin: the tool parses a consumer's C# to find attributes, and the pinned version reads C# 12 at most.  The generator's own reference is untouched.
- The generator cannot take a JSON library.  `System.Text.Json` in a `netstandard2.0` analyzer collides with the compiler's own copy, and Newtonsoft is a dependency the package cannot ship cleanly.  The snapshot reader is a small hand-written JSON parser, and the writer is hand-written too so that the two are tested together.  The format stays within what such a parser handles comfortably: objects, arrays, strings, integers, booleans and null.
- The tool learns what the compiler sees from a **project manifest** that a target in `SqlSource.targets` writes: the project's `.sql` files with their metadata and the project's `SqlSource` properties, to a file under `obj`, or to the file the property `SqlSourceManifestFile` names.  The target hooks nothing and depends on the package's two trim targets.  From the command line the tool runs `dotnet msbuild -t:SqlSourceWriteManifest -p:SqlSourceManifestFile=<file>` on each project in the run, with a file in a temporary folder of its own, and reads the files, so it parses nothing MSBuild prints for them and writes nothing into a project; in phase 9's online mode the target has already run as part of the build, so no second MSBuild process is spawned.  The project manifest is a contract between package and tool, as the sidecar is between tool and generator, and carries a format version.
- The alternatives and why not: `dotnet msbuild -getItem:AdditionalFiles` with no target evaluates without running any, so it misses a file a target adds and the package's own trimming, and with or without a target it would mean a second evaluation of the project inside a build, where the manifest file is already written; hosting MSBuild in-process needs `MSBuildLocator` and matches SDK versions by hand; reading `obj/*.GeneratedMSBuildEditorConfig.editorconfig` needs a prior build and holds no file list.  Globbing `**/*.sql` in the tool was never an option, since `Remove`, `Update`, `DefaultItemExcludes`, `SqlSourceIncludeFiles=false` and `Directory.Build.props` all change what the compiler sees.
- For a solution, the tool reads the solution's projects with `Microsoft.VisualStudio.SolutionPersistence`, asks each for `-getProperty:SqlSourceImported`, and runs the target on the ones that use SqlSource, one MSBuild process for each project and several at once, each given the `SolutionDir` and the like that a build of the solution gives it.  Sub-phase 2.3's spike ruled out the solution itself as the target: `-t:` on a solution fails for a project that lacks the target, and MSBuild refuses its `-get` switches for a solution.  The tool never restores.  A package's props reach a project only after a restore, so a project without the marker whose `ProjectAssetsFile` does not exist, or which has a `PackageReference` to SqlSource all the same, was not restored, and is an error in a solution as well as alone: a run on a fresh checkout must not pass by finding nothing.
- Multi-targeting, settled by the same spike: a target invoked by name on a `TargetFrameworks` project runs in the outer build with an empty `TargetFramework`, and a target invoked alone does not run the hooks on `GenerateMSBuildEditorConfigFileCore`.  So the manifest target has explicit `DependsOnTargets` on the two trim targets, and the tool passes `-p:TargetFramework=<first>` from `-getProperty:TargetFrameworks`, to the target and to the evaluation that asks whether the project uses SqlSource: NuGet imports a package's props into such a project only under a condition on `TargetFramework`, so with no framework the marker is never set.  `AdditionalFiles` almost never depend on the framework, so there is one manifest per project, the first framework's.  A file or an attribute that only another framework has is not described, the build of that framework says so from phase 5, and `docs/tech-debt` records it.
- A target invoked by name also lacks the constants of the framework, `NET10_0`, `NET8_0_OR_GREATER`, which the SDK adds in its target `AddImplicitDefineConstants` before the compile.  The manifest target depends on that target when the project has a `TargetFramework`, so that the tool's attribute reader sees `#if` as the compiler does; the name is the SDK's own, and `docs/tech-debt` records the dependency.
- A target invoked by name runs no hook of a build either.  A `.sql` file that a project's target adds is in the manifest only when that target hooks `SqlSourceTrimMetadataOfFiles`, the hook the package names; one that hooks `BeforeBuild` is not, and from phase 5 its queries need an entry the tool cannot write.  Depending on `BeforeBuild` was weighed and rejected: it would run whatever a project hangs on it, on every `describe`.  `docs/tech-debt` records the limit, and phase 9's run inside a build does not have it.
- MSBuild in Visual Studio runs on .NET Framework; shipping the describe step as an MSBuild task instead of a tool would mean building it twice and loading Npgsql inside MSBuild.  A tool invoked by a target is the lower-risk shape.

### Workflow

Decided, the unit of a run:

- The tool mirrors the SDK's commands.  With no argument it looks in the current directory for exactly one `.sln`, `.slnx` or `.csproj` and errors otherwise; an explicit path to either is an argument.
- In solution mode the tool lists the solution's projects and keeps the ones that use SqlSource.  In project mode a project that does not use SqlSource is an error.
- The tool's configuration is the project file: which `.sql` files the project claims, their dialects, and their databases.  There is no configuration file of the tool's own.

Decided, databases:

- A project can use several databases, of one engine or of several, and one run refreshes every file the run has a connection for.
- Each query belongs to one logical database, named the way its dialect is: a `-- database: billing` marker in the file, as metadata `SqlSourceDatabase` on the file's `AdditionalFiles` item, or as the property `SqlSourceDatabase` for the project.  The marker is allowed in the preamble, for every query of the file, and inside a query, for that query alone; the query's marker wins over the file's, the file's over the metadata, the metadata over the property.  When nothing is set, the name is the dialect's name.  Names are compared ignoring case: `billing` and `Billing` read one connection variable, so they are one database.
- A name is one database across the whole run: a `billing` database used by three projects is one connection.  Every query that names a database has the dialect of its file, and the tool errors when two queries of one name have two dialects, since the dialect picks the driver for that connection.
- Connections are supplied per name, on the command line, in the environment or in a `.env` file, never in the project file, since they hold credentials.  A name with no connection, and no `--database` filter that excludes it, is an error for its queries that must be described; the rest of the run continues.  A database whose queries all have a current entry needs no connection, and its summary line says it has none: a developer who has one of a project's two databases is not stopped by the other.  Under `--force` and `--check` every query is described, so there it is always an error.
- A sidecar is written when every query of its file that needs an entry was described in this run or already has a current entry, where current means the hash and the database match and the file's versions are the tool's.  Otherwise the file is left as it was and the run says which queries kept it from being written; when a query of the file was described in that run, that is an error, since a description that was not saved must not look like a success.  So a `--database billing` run writes a mixed file when the other queries' entries are current, and never mixes an entry from an older tool with a new one.

Decided, commands:

- `sqlsource describe` describes every query of every `.sql` file in the run and writes or updates the sidecars.  A query whose entry is current, by the rule above, is skipped.  A query needs an entry when its output resolves to `Models` or `CodeGen`, whatever its shape: one with neither parameters nor rows still gets an entry, since the methods phase needs its `resultKind`.
- `sqlsource describe --force` describes every query again whether or not its entry is up to date.  It is also the command to run after a schema change, since the hash cannot see one.
- `sqlsource describe --check` describes every query, as `--force` does, in memory and compares with the committed sidecars, and exits non-zero when they differ.  It is the CI step, as `cargo sqlx prepare --check` and `sqlc diff` are.  "Differ" means a difference in the data that changes a generated type or method: the set of entries, and per entry `hash`, `engine`, `database`, `resultKind`, `matchesTable`, and the parameters' and columns' names, ordinals, types and nullability.  It ignores `_WARNING`, `$schema`, `formatVersion`, `toolVersion` and `serverVersion`, which differ between a developer's machine and CI by design, every provenance field, and `origin`, `identity` and `computed`, which reach only documentation.  Under `--database` the comparison covers the entries of the named databases alone, and a committed file whose `formatVersion` is not the tool's is reported as a difference rather than compared.  The [sidecar format design](2026-10-07-sidecar-format-design.md) lists the two sets.

Decided, connections and the build:

- `--connection billing=Host=...` names a connection, and the option is always a name, `=` and a value: a connection string holds `=` itself and a shell removes quotes before the tool sees them, so nothing else tells a name from the start of a value.  `SQLSOURCE_CONNECTION_BILLING` is the same in the environment and in a `.env` file, with the name upper-cased and every character that is not a letter or a digit replaced by `_`, so `billing-v2` is `SQLSOURCE_CONNECTION_BILLING_V2`; two database names that map to one variable are an error.  `SQLSOURCE_CONNECTION`, with no name, is accepted when exactly one database is in the run, which is most projects.  Environment variables are not MSBuild properties and need not carry the `SqlSource` prefix, but the name should still make the owner obvious.
- The `.env` file is the primary source: standard dotenv syntax, found by walking up from the project directory to the repository root, git-ignored by convention with a committed `.env.example` if a team wants to document the names.  Precedence: the command line, then the process environment, then the file.  A file is what works in every IDE, since Visual Studio, Rider and VS Code inherit their process environment and set variables per run configuration, not per build, and an OS-wide variable is wrong for anyone who works on several projects.  It is what SQLx does with `DATABASE_URL`.  `dotnet user-secrets` was considered and not taken: it is keyed per project, connections are per logical database across a solution, and two mechanisms is two pages of README.
- `--database billing` restricts a run, including `--check`, to the named databases, so refreshing one after a migration neither needs nor complains about the others' connections.  `--project <path>` and a `.sql` file path restrict a run the same way; all three are phase 2's.  The `.sql` path is positional, `sqlsource describe Queries/Users.sql`: an argument that ends in `.sql` is a filter and any other is the unit.
- A project uses SqlSource when the package's props set a marker property, `SqlSourceImported`, which evaluation returns whether SqlSource came as a package or, as in this repository's tests, as a project reference with the props imported by path.  Looking for a `PackageReference` would miss the second.
- A `.sql` file with parse errors is skipped and its errors reported, since the generator produces nothing for it either.
- Every run ends with one summary line per database, with the counts of queries described, skipped and failed, so a filter that matched nothing is visible.
- The sidecar records each query's database name, so a reader knows where its types came from.  The generator does not read it.
- The tool is installed through a local tool manifest, so its version is pinned per repository and `dotnet tool restore` gets it, the way this repository pins CSharpier.
- Phase 9's online mode: a target in `SqlSource.targets` runs `dotnet sqlsource describe` on the project before `CoreCompile`.  `SqlSourceOnline` is `auto`, `true` or `false`: `auto`, the default, runs the tool when `SQLSOURCE_CONNECTION` is in the environment or a `.env` exists in the project directory or in `$(SolutionDir)` when that is defined, which is what an MSBuild condition can test; a project that uses only named variables sets `true`.  `true` always runs it, and under `true` a missing connection is an error; under `auto` the tool exits zero with one line saying it is offline when no connection resolves for any database in the run, and a connection for some databases and not others is an error for the unconnected ones that have a query to describe, in every mode, as under Workflow.  A bare `SQLSOURCE_CONNECTION` with two databases in the run is that error, not offline.  `false` never runs it, for CI or a machine that has the variable for other reasons.  Offline, the committed snapshot is used.  This is SQLx's online and offline split.
- Phase 9's `--watch`: `sqlsource describe --watch` on a unit watches its `.sql` files and project files, debounces saves, re-describes the changed file, and re-runs the manifest target when a project file changes.  The IDE's generator run picks up the written sidecar.

### Output of the tool

Decided: the tool reports errors in the compiler's format, `path(line,col): error SQLSRCnnn: message`, with the same ids as the generator where the condition is the same, so that the online mode's errors reach the IDE's error list and a terminal shows what the build would.

Settled in phase 2: exit code zero on success, one when anything was reported as an error, two when `--check` found a difference and nothing else failed.  An exception is caught and reported as an error of its own.  Command-line parsing with System.CommandLine.

### What is generated

Decided:

- The attribute is renamed `SqlSourceGenerate`, `[SqlSourceGenerate]` on the type, since it now drives three kinds of generation and not only queries.  Its `Mode` property and the `SqlQueriesMode` enum are both renamed `SqlLocation`; its values, `Nested` and `Direct`, say where the SQL constants and token methods go, and nothing else, which the name now says.  Nothing has been published, so the rename breaks nobody.  It is phase 0.
- A new setting, `SqlSourceOutput`, says what SqlSource generates for a query.  Its values are the members of a new enum, `GeneratorOutput`, emitted beside `SqlLocation`:

| Value | Generates |
|----|----|
| `Sql` | The constant or token method, as today |
| `Models` | `Sql`, plus the input and output types |
| `CodeGen` | `Models`, plus the execution methods |

- The default is `CodeGen`.
- It is set the way the dialect and the database are, plus one place of its own: the property `SqlSourceOutput` for the project, metadata `SqlSourceOutput` on a file's `AdditionalFiles` item, an optional `Output` property on the attribute, `[SqlSourceGenerate(Output = GeneratorOutput.Models)]`, for the files a type claims, and an `-- output: models` marker in a file's preamble or inside a query.
- The tool reads the setting too, to leave out what needs no types: a project whose queries all resolve to `Sql` is skipped in solution mode, and so is a file or a query that resolves to `Sql`.  A sidecar holds entries only for the queries that need them, and the tool deletes a sidecar that would be empty.
- `Models` and `CodeGen` need a dialect that has a describer: `postgres` or `mssql` in this epic.  A query that resolves to either output under any other dialect is an error, in the generator and in the tool, that says to set the dialect or to set the output to `Sql`.  CockroachDB is out of the epic: it speaks the protocol but rejects the `EXPLAIN` form the walk needs, and a dialect that is supported but untested would be worse than one that is absent.  It is the first candidate after the epic, as the PostgreSQL describer with the walk off.
- The default dialect stays `ansi`.  Only a project that generates `Sql` alone can use it, and that is who it is for.  A project that uses nothing but the defaults therefore gets the error above on every file, and its message is the instruction: set `SqlSourceDialect`.  The README's installation section says so before anything else.

Recommended:

- Precedence, most specific first: the query's marker, the file's marker, the attribute's `Output`, the file's metadata, the project's property, then the default.  The attribute sits above the metadata because it is set on one type on purpose, while metadata is usually a glob; and below the markers because what a file says about itself wins everywhere else in SqlSource.  Since two types may claim one file, the output is resolved per type and query, not per file.
- The tool therefore has to know which types claim which files with which `Output`, which it needs anyway: a `.sql` file no type claims is ignored by the generator today and gets no sidecar.  The project manifest carries the project's `Compile` items, with its `LangVersion` and `DefineConstants`, and the tool reads the attributes from them with a syntax-level reader of its own: the generator's reader is semantic, it works on symbols and `AttributeData`, so there is nothing of it to share.  The tool's reader matches the attribute by name and reads `Path` and `Output` alone, since nothing else on the attribute changes what is described: a string literal for `Path` and an enum member for `Output`.  Any other form of those two, a constant, `nameof`, concatenation, is an error asking for a literal; a project that aliases or shadows the attribute's name is misread, which `docs/tech-debt` records.  The path resolver is shared.
- Until phase 8 ships, `CodeGen` behaves as `Models`.
- A value that is not one of the three is an error with no position, once per distinct value, as an invalid dialect is; the files it covers are generated as `CodeGen`.
- The output-needs-a-dialect error is reported once per file, in the generator and in the tool alike, at the marker that set the output when there is one and at the start of the file otherwise, rather than at every query, since the fix is one setting.  The parser keeps no position for a marker, so the tool reports it at the file's first query that needs an entry, and phase 5 decides whether the generator gains the position.  The file still gets its constants and methods for the `Sql` part of its output.
- Every setting resolves by one rule, most specific source first, and the generator's dialect resolution generalises to a settings record rather than growing siblings.  The record is two-stage, because the generator's caching rule in `src/SqlSource/AGENTS.md` forbids a setting that is not a parse input from reaching the parse: the dialect stays the per-file input it is, resolved before the parse; the markers are carried on the block by the parser; and the property, metadata and attribute levels join after the type's queries are selected, as token validation does today, and resolve there with the markers, per type and query.  An attribute edit therefore re-parses nothing.  Not every setting has every source; the table says which:

| Setting | Property | Metadata | Attribute | Preamble marker | Query marker |
|----|----|----|----|----|----|
| Dialect | `SqlSourceDialect` | yes | no | `-- dialect:` | no |
| Database | `SqlSourceDatabase` | yes | no | `-- database:` | `-- database:` |
| Output | `SqlSourceOutput` | yes | `Output` | `-- output:` | `-- output:` |
| Model suffixes, namespace | yes | yes | yes | yes | no |
| Model types, collection type | yes | yes | yes | yes | yes |
| `SqlLocation`, method location | no | no | `SqlLocation`, `MethodLocation` | no | no |
| Generator parameters | `SqlSourceGeneratorParameters` | yes | `Parameters` | `-- generator:` | `-- generator:` |
| Project-only: `SqlSourceDateTimeTypes`, `SqlSourceTimestampTzType`, the type override, the library switches | yes | no | no | no | no |
| Query-only markers: `-- param:`, `-- token:`, `-- token-ignore:`, the `-> shape` suffix, `-- input-model:`, `-- output-model:` | no | no | no | no | yes |

  The dialect marker is preamble-only because it changes how the lines after it are lexed; the model suffix and namespace markers are preamble-only because a name is a query-level detail that `-- input-model:` and `-- output-model:` set whole.

Technical notes:

- Each setting the generator reads with a metadata level needs a `CompilerVisibleProperty` and a `CompilerVisibleItemMetadata` in the props, and the trim and collect targets in `SqlSource.targets` that the dialect has; a project-only setting needs the property alone.  `SqlSourceOnline` and `SqlSourceLog` are read by the build, never by the generator, and need neither.  `SqlSourceDatabase` needs the trim and collect so that the tool sees a clean value, even though the generator never reads it.

### The sidecar

Decided:

- One file beside each `.sql` file: `Users.sql` has `Users.sql.json`.  The package's props include `**/*.sql.json` as `AdditionalFiles` the way they include `.sql` files, and the generator pairs the two by path.
- The sidecar nests under its `.sql` file in Visual Studio and Rider with no user action: `SqlSource.targets` sets `DependentUpon="%(Filename)"` on every `**/*.sql.json` item, and `%(Filename)` of `Users.sql.json` is `Users.sql`.  As an evaluation-time `ItemGroup` with `Update` in the `.targets` file, not inside a target, since the IDEs nest from evaluated items; in the `.targets` file and not the props so that it also reaches a sidecar a project lists itself when `SqlSourceIncludeFiles` is off; and on the SDK's `None` item for the file as well as the `AdditionalFiles` item, since the IDE may nest on either.  Whether the IDEs honour the metadata on these item types is phase 5's spike.  `tests/SqlSource.Tests/Package/BuildFileTests.cs` asserts today that every element of `SqlSource.targets` is an unconditional target hooking `GenerateMSBuildEditorConfigFileCore`; phase 5 extends it for this item group, and phase 9 for the online target's condition and for the SDK properties it reads, `DesignTimeBuild` and `SolutionDir`, in the same pull requests, as `AGENTS.md` requires.  A project with `SqlSourceIncludeFiles` off lists `**/*.sql.json` itself, which the README says.  VS Code has no project-file nesting; the README gives its `explorer.fileNesting.patterns` line.
- The file's first property is `_WARNING`, a short sentence or two saying the tool wrote it and regenerates it.
- The file carries two version tags: a format version, which is the compatibility contract between tool and generator, and the version of the tool that wrote it, so that a tool and a generator that are out of step are detected.
- Each query's entry carries a hash of the SQL it describes.  An entry whose hash does not match the query is stale.  The hash has one definition, under Tokens, which every other mention points at.
- The format is a public contract and is specified in the [sidecar format design](2026-10-07-sidecar-format-design.md): the fields, the reader's rules, the engine-specific type objects, the compatibility rules, and a JSON Schema that phase 2 ships under `schemas/`.  In outline: a top level of `$schema`, `formatVersion`, `toolVersion` and `queries`; per query `hash`, `engine`, `database`, `serverVersion`, `resultKind`, `parameters` and `columns`; a flat type object per engine with a required `name` spelled as the engine spells it; tri-state `nullable`, `identity` and `computed`; an `origin` or null.  Only objects, arrays, strings, integers, booleans and null.
- An entry with rows records `matchesTable`, the table whose column list the result is exactly, or null; the tool compares the result against the catalog after describing.  See Models.
- The sidecar is read once per file, and the orphan, stale and missing checks run per sidecar over the union of what every type that claims the file needs, not per type: an entry that one claiming type needs is not an orphan for another whose output is `Sql`.
- The sidecar records the description, not the decision: a column's `nullable` is what the server and the tool's inference established, and a column's `name` is as the server returned it, `!` or `?` suffix included.  The generator applies the suffix and the policy.  So the file is a faithful record and the policy can change without a database.
- A change to the format is additive, with no version bump, when a reader of the previous format that ignores unknown keys still generates correct code from the new file: a new key, a new engine, a new type kind.  A rename, a retyping, a hash change, a new `resultKind` or a new required key bumps `formatVersion`.

Recommended:

- A per-file sidecar rather than one folder of hashed files as SQLx's `.sqlx/` is, because the diff is readable beside the SQL it describes, a deleted `.sql` file leaves an orphan that is obvious, and the generator already keys everything by file.  Not one file per project, because every query change would touch it and merge conflicts would be constant.
- The format version is an integer.  The generator reads the versions it knows; a higher one is an error that says to update SqlSource, a lower one an error that says to run the tool.  A tool version that differs from the generator's while the format matches is a warning, since the two are released together and the snapshot is still readable.  Every version comparison, in the generator and in the tool, uses `VersionPrefix` alone, with any suffix stripped: a sidecar written by `0.4.0-dev` on a developer's machine equals a generator built as `0.4.0-pr-42` in CI.  The pull request that raises `VersionPrefix` makes every committed sidecar's `toolVersion` differ, which `docs/publishing.md` answers: that pull request runs `tools/describe-end-to-end.sh`, and the end-to-end projects list the warning's id in `WarningsNotAsErrors` so that the step is a reminder and not a broken build.  The tool rewrites a file whose `toolVersion` is not its own, so `describe` after an update refreshes everything without `--force`.
- Entries are keyed by query name, in the file's order, so a diff follows the `.sql` file.
- A schema fingerprint per origin table, to catch a schema change behind unchanged SQL without a database, is not in this epic.  `--check` in CI is the answer the epic gives.

What the generator does with a sidecar:

| State | Effect |
|----|----|
| A query whose output is `Sql` | Its sidecar entry, if any, is ignored for generation; an entry that exists is an orphan, below. |
| A query whose output needs types and has no sidecar or no entry | An error at the query: run the tool. |
| Entry present, hash differs | An error at the query: the snapshot is stale.  No model is emitted, so stale types never compile. |
| Entry present for a query that no longer exists | A warning at the sidecar: an orphan entry.  A diagnostic "at the sidecar" is at the entry's line, which the hand-written reader keeps. |
| An entry's `engine` differs from the file's dialect | An error at the query. |
| Format version unknown, or tool version differs | As above under Recommended. |

Recommended: a missing entry is an error, not a warning, so that a new query never silently compiles as a constant alone.  A project that wants only the constants says `SqlSourceOutput=Sql` and never runs the tool.

### Parameters

Decided: the lexer finds parameters, so that one dialect-aware rule gives the same list to the tool, the generator and the hash.  A `@` inside a string, a comment or a quoted identifier is not a parameter under any dialect, and the lexer already knows where those are.

Recommended, the rule for both dialects in scope: `@` followed by a letter, a digit or `_`, where the `@` is not preceded by `@` or by a character that can be part of an identifier.  The name runs over letters, digits and `_`, where a letter is any Unicode letter, as both engines accept non-ASCII identifiers and the lexer's identifier rule already does.  Names are compared ignoring case, as both drivers compare them.  Ordinal is the order of first appearance.  `@name` only: Npgsql also takes `:name`, which SqlSource does not, because `::` casts and array slices `a[1:2]` make `:` ambiguous and Npgsql's own documentation says its rewriting "may not parse some forms of SQL correctly".  The operators that contain `@` are safe under the rule, `@>`, `<@`, `@@`, `@?`, as are T-SQL's `@@ROWCOUNT` and the other system functions.

Technical notes:

- At run time the SQL still holds `@name`.  On SQL Server that is native.  On PostgreSQL, Npgsql rewrites `@name` to `$n` when the command's parameters are named, which is what Dapper produces.  SqlSource's rule must therefore agree with Npgsql's for every query, and the tool checks it: for PostgreSQL it lets Npgsql derive the parameters of the original SQL and compares names and count with the lexer's, and reports a disagreement as an error on the query.

Decided, the parameter list of a query:

- It is the parameters the lexer finds in the static SQL, in order of first appearance, followed by the parameters that `-- param:` markers declare and the static SQL does not hold, in marker order.
- Under `mssql`, a name that the query declares with `DECLARE` is a local variable and is in no list, at any place it is written; the lexer still gives it as a parameter lexeme.  A step that walks those lexemes, such as the tool's renaming of each occurrence for `sp_describe_undeclared_parameters`, takes only the names of the parameter list.  What the rule can still get wrong is in `docs/tech-debt/TD-0004`.
- A `-- param:` marker for a parameter that is not in the static SQL must give a type.  Such a parameter reaches the query only through a token's fragment at run time, so nothing else can type it.  A marker without a type for such a parameter is an error.
- A parameter that appears in a token's default and in neither the static SQL nor a marker is an error that says to declare it.  A default is a sample, and a type taken from a sample alone would be a guess.
- The marker writes the parameter with the dialect's prefix, as the SQL does.  In this epic that is `@` under every dialect; the prefix is a field of the dialect's rules, like the quote readers, so that an engine with another prefix, Oracle's `:name`, is a rule and not a rewrite.  One parameter per marker, inside a query only: a parameter is a query-level detail, and a preamble declaration would either add a parameter to every query of the file or silently apply nowhere.  A marker that names a parameter the query neither holds nor could receive is not an error, since the receiving case cannot be told apart.

Technical notes:

- The tool asks the server to resolve a declared type name, so that every parameter in the sidecar carries a resolved type and the generator never parses a type name.  On SQL Server the type goes into `@params` where the procedure resolves it, or through `sys.types` for a parameter the sample does not use.  On PostgreSQL, how a declared type reaches the `Parse` message is phase 3's spike: `NpgsqlCommandBuilder.DeriveParameters` sends no types and replaces the collection, so the candidates are a cast written into the sample SQL, `(@page)::int`, or typed `NpgsqlParameter`s with `DataTypeName` set for the declared ones and the rest left for the server, if Npgsql's `SchemaOnly` path allows an untyped parameter.
- A declared parameter that a run-time fragment does not use is bound anyway by the generated method.  SQL Server accepts a parameter the batch does not reference; Npgsql in named mode sends only the parameters whose placeholders appear in the text.  Phase 8 verifies both.

### Tokens

A query with `{{tokens}}` cannot be described as written, so each token gets a default: a sample fragment that the tool substitutes before describing.

Decided:

- A token may carry its default inline: `{{name:default}}`.  The name is as today; the default is everything after the first `:` up to the closing `}}`, trimmed, so `{{cast:x::int}}` is the token `cast` with the default `x::int`.  A default cannot contain `}}`.  An empty default, `{{extraWhere:}}`, is allowed and means the query is described with nothing there.
- A default is a sample for describing and nothing else.  The generated method still takes the token as a `string`, the emitted SQL still holds the placeholder, and token validation applies to the run-time argument as today.
- Under `Sql` output a default is allowed and ignored.  Under `Models` and `CodeGen` a token without a default is an error at the query, in the generator and in the tool.
- A default can also be given once for a token that appears several times, with a marker that holds the inline form: `-- token: {{where:AND deleted_at IS NULL}}`.  The marker's body is exactly what would be written in the SQL, parsed by the same scanner, so there is no second grammar to learn; one token per marker, and anything outside the braces is an error.  It is allowed inside a query only: a default is a sample for one query's SQL, and a token of the same name in another query stands in other SQL.
- The README states the contract: a fragment passed at run time must keep the sample's shape, the same result columns and the same parameters, because nothing can check it.  An `ORDER BY` fragment or a table name keeps it; a column list does not.

Recommended:

- The marker form was chosen over a `-- generator: token-default=` parameter because generator parameters are a space-separated list and most defaults are SQL fragments with spaces, which would need a quoting rule; and over `-- token: where AND ...`, a bare name and a space, because that reads as SQL to anyone who does not know the rule.
- Two defaults for one token of a query, an inline one beside a query marker or two inline occurrences that differ, are an error, not a precedence.  One occurrence with a default and others without is fine.
- **The hash**, the one definition: SHA-256, lower-case hex, of the UTF-8 bytes of `engine`, a line feed, the SQL the generator emits for the query with comments stripped whatever `keep-comments` says, `\n` line endings and parameters as written, with each token rendered as `{{name:default}}` using its resolved default, a line feed, and the query's `-- param:` declarations as `name type null` with one space between parts, the type as written and `null`, `not null` or neither as the marker has it, one per line in the order of the query's parameter list.  A query with no tokens renders none; a query with no declarations has an empty last part, so the suffix always applies.  `engine` is the dialect's canonical name, the first alias in `SqlDialectName`, for every dialect, and the hash is computed lazily, on first use, so that a parse under `ansi` pays nothing for it and the parser's allocation tests stay as they are.  A comment changes no type, a parameter's name is part of the input type, a default is what was described, and a declaration is what was declared.
- A parameter that appears only inside a token's default, or only in the fragment a caller will pass at run time, is not in the static SQL, so the lexer cannot find it; it must be declared with a `-- param:` marker that gives its type.  See Parameters.
- `{{a:b}}` was literal text under the rule that braces around anything but a name are not a token; it is now a token.  Nothing is published, so the change costs nothing, and the README's rule is updated.
- An empty default describes the query with nothing in the token's place, but token validation still rejects an empty run-time argument unless `no-token-validation` is set; the README says so beside the empty-default example.
- `token-ignore` becomes a marker, `-- token-ignore: name`, inside a query only, one name per marker, several markers accumulating.  It is almost always set per query, and it names text rather than switching a behaviour, which is what the generator parameters are for.  The `token-ignore=name` parameter is dropped in phase 1.

### Nullability

Decided: a nullability the description leaves unknown is nullable.  A false nullable is a nuisance; a false non-nullable is a silent wrong value, because Dapper leaves a member at its default when the column is `NULL`.

Decided: parameters are non-nullable unless the user says otherwise.  The database cannot report it, and sqlc, SQLx and FSharp.Data.SqlClient all default this way.

Recommended, the overrides, each written so that the `.sql` file stays runnable as it is:

| What | How | Precedent |
|----|----|----|
| A column's nullability | An alias with a suffix: `AS "name!"` is not null, `AS "name?"` is nullable.  Legal quoted identifiers on both engines; `[name!]` also on SQL Server.  The database returns the name with the suffix and the generator strips it. | SQLx, pgtyped |
| A parameter's nullability | A marker in the query: `-- param: @deletedBefore null`.  The parameter as it is written in the SQL, with the dialect's prefix, then `null`, the word a column definition uses.  `not null` is accepted too: it says what saying nothing says, and is what a user who writes column definitions will write. | sqlc's `narg`, pgtyped's `!` in the other direction |
| A parameter's type | The same marker with a type in the database's own vocabulary: `-- param: @page int`, `-- param: @since timestamptz null`.  The tool hands the type to the server as the parameter's type, so the server still checks it against every use.  It fixes a type the server cannot infer, `SELECT @p`, or infers too wide, `varchar(8000)` in `TOP (@n)`; and it is how a parameter that is not in the static SQL gets a type at all.  Every engine outside the two in scope except DuckDB needs it for every parameter. | Prisma's `-- @param {Int} $1:name`, sqlc's `sqlc.arg`, T-SQL's own `@name type` declarations |

A suffix on the parameter in the SQL, `@id?`, was rejected: it is valid on neither server, so the file could no longer be run by the user's tools or by the describer without rewriting.  The marker's exact grammar is phase 1's to settle, since the lexer reads markers.

### Types in C#

Decided:

- The type map covers **every type the driver can read**.  This outline does not enumerate them; each phase that adds an engine researches the driver's full list and populates the map, with a test per row.
- Major libraries that extend the drivers' types get maps out of the box.  In phase 7: NodaTime, through `Npgsql.NodaTime` on PostgreSQL and by conversion on SQL Server, where no plugin exists and the EF Core provider's conventions are followed; NetTopologySuite, through `Npgsql.NetTopologySuite` and `NetTopologySuite.IO.SqlServerBytes`; `JsonDocument` and `JsonElement`, built into Npgsql and by parsing on SQL Server; and `SqlJson` on SqlClient 6.0 and later.  Left to the override or a later phase: `BigInteger` for a `numeric` beyond `decimal`'s range, since an unbounded `numeric` holds a scale that `BigInteger` drops, so `decimal` stays the default and `BigInteger` is an override; Newtonsoft through `Npgsql.Json.NET`, `Npgsql.GeoJSON`, Pgvector, `SqlVector`, `Microsoft.SqlServer.Types` for `hierarchyid` and spatial, and the strongly-typed-id generators, which wrap a primitive the driver already reads.
- A library's map is switched on by detection from the compilation, keyed on the plugin assembly where one exists, since a reference to `Npgsql.NodaTime` is the signal that the data source calls `UseNodaTime()`; and a property per library, `SqlSourceNodaTime`, overrides detection either way, since a project can reference NodaTime for other reasons and keep `DateTime` at the database layer.  The README says which `Use...()` call each plugin needs on the data source.
- A map row has one of three shapes: a driver type read with `GetFieldValue<T>`; a plugin type read the same way once the plugin is registered; or a converted type, with a read and a write expression the generator inlines.  The third is what NodaTime on SQL Server needs, and it is the mechanism the override uses to map a column to a strongly-typed id.
- The driver floor is **the oldest line each maintainer still services**: Npgsql 8.0 and Microsoft.Data.SqlClient 6.1, tested against Npgsql 8, 9 and 10 and SqlClient 6.1 and 7.  Npgsql 8 is also the first with the opt-in model the enum policy relies on; SqlClient 6.1 is the long-term-support line until 2028, and every mapping the epic needs, `DateOnly` and `TimeOnly` included, exists from 5.1.
- The map tracks the driver by **feature, detected from the compilation by symbol**, never by version number.  SqlClient's assembly version is the major alone, `6.0.0.0` for both 6.0 and 6.1, so a version cannot tell the json release from the vector release; a symbol can.  The markers are the features themselves: `NpgsqlDataSourceBuilder.EnableUnmappedTypes` for Npgsql 8, `NpgsqlTypes.NpgsqlCube` for Npgsql 10, unverified and phase 5's probe confirms it, `Microsoft.Data.SqlTypes.SqlJson` for SqlClient 6.0, `SqlVector<T>` for 6.1.  The probe is one `Select` over the compilation that returns a small value-equal record of flags and assembly versions, as the .NET floor probe is today, combined after the parse so that typing never re-reads a file.  The versions serve diagnostics only.

Recommended, the policies the maps follow:

- The default C# type for a database type is **the type the driver boxes**.  A generated method reads with `GetFieldValue<T>` and could ask for any type the driver converts to, but the models also serve a project that runs them through Dapper or its own code, where the boxed type is the one that arrives without conversion.  One default serves both.
- Where the two drivers disagree, each follows its own driver, and on PostgreSQL the referenced one: `date` and `time` are `DateOnly` and `TimeOnly` when the probe finds Npgsql 10 or later, which boxes them, and `DateTime` and `TimeSpan` on Npgsql 8 and 9, which box those; SQL Server's are `DateTime` and `TimeSpan`, which SqlClient boxes.  That is the boxed-type principle applied per project, so there is nothing to warn about; the README notes that upgrading Npgsql from 9 to 10 changes the models' `date` and `time` types, which is the driver's own change surfacing.  `SqlSourceDateTimeTypes`, `Modern` or `Legacy`, overrides the probe; `timestamptz` is `DateTime` with `Kind.Utc`, and `SqlSourceTimestampTzType`, `DateTime` or `DateTimeOffset`, switches that.  Both options are phase 5's.
- `json`, `jsonb`, `xml` and SQL Server's `json` are `string` by default; a library map can change that.
- A PostgreSQL array is `T[]` of the mapped element.  An array that holds nulls needs `T?[]` for a value type and the server cannot say; the override is the fix.
- A PostgreSQL domain maps as its base type.  A PostgreSQL enum is `string` by default, which needs `EnableUnmappedTypes()` on the data source in Npgsql 8 and later, and the override maps it to a C# enum for a project that calls `MapEnum<T>()`.
- A type the driver reads only with an extra package, `hierarchyid` and `geography` through `Microsoft.SqlServer.Types`, PostGIS through NetTopologySuite, is covered by that library's map, not by the base map.  A type the driver cannot read at all is a diagnostic that names the type and the override.
- A nullable column is the C# type with `?`: `Nullable<T>` for a value type, the annotation for a reference type.
- Facets, `varchar(50)` and `decimal(18,2)`, are dropped from the type, as every surveyed tool drops them, and kept in the description; the generator puts them in the member's documentation.
- The override is a project-level mapping from a database type name to a C# type, `timestamptz` to `DateTimeOffset`, `public.status` to `MyApp.Status`.  Its MSBuild form is phase 5's; its property starts with `SqlSource`, as every property of the package does.  A per-column C# type override, SQLx's `AS "created: DateTimeOffset"`, is not in this epic.
- The type map lives in the generator and never in the snapshot, so a better mapping needs no database, and one snapshot serves two projects that map differently.
- The map yields two things for a parameter: the C# type, and what the generated method sets on the `DbParameter`: `DbType` where it is enough; on PostgreSQL, `NpgsqlParameter.DataTypeName` with the type's name where it is not, `jsonb`, `int[]`, `public.user_status`, which the sidecar already holds, which Npgsql has accepted since 4.0 and which from 10.0 takes precedence; on SQL Server, `SqlDbType` with the facets for size, precision and scale, which the description carries.  Generated code never names an `NpgsqlDbType` member, so it compiles against every supported Npgsql without a conditional.
- `CodeGen` requires the dialect's driver: Npgsql for `postgres`, Microsoft.Data.SqlClient for `mssql`.  The methods take a `DbConnection` and set provider-specific members on the parameters the command creates, by casting them, which fails for a wrapping connection that creates its own parameters; the README says so.  Below the floor: one error per project naming the version found and the floor; models are still emitted, execution methods are not.  Driver absent: models are emitted, since a models-only project is legitimate, with an error only at a column whose type lives in `NpgsqlTypes`, and under `CodeGen` one error per project saying which driver the dialect needs.  Newer than the generator knows: emit for the newest known flags and report an informational diagnostic, not a warning, since `TreatWarningsAsErrors` is common.  The README states the floor per dialect, as Kiota documents the runtime versions its output needs.
- Npgsql 10's change of `date` and `time` to `DateOnly` and `TimeOnly` affects only non-generic reads.  `GetFieldValue<DateOnly>` works from Npgsql 6 and `GetFieldValue<DateTime>` still works on 10, so the generated methods read whichever type the map chose, on any supported driver.

### Models

Decided:

- **Names.**  A query `GetUser` gets `GetUserParams` when it has parameters and `GetUserDto` when it has a result set.  The suffixes are settings: `SqlSourceInputModelSuffix` and `SqlSourceOutputModelSuffix` as project properties, as `AdditionalFiles` metadata, as `InputModelSuffix` and `OutputModelSuffix` on the attribute, and as the preamble-only markers `-- input-model-suffix:` and `-- output-model-suffix:`.  One query's names are set whole with the markers `-- input-model:` and `-- output-model:`, which take the full name; a name that is not an identifier, or an input-model marker on a query with no parameters, is an error.
- **Where models are emitted.**  Once per full name, project-wide: the generator collects every model every claiming type needs, keyed by full name, and emits each once in its own file, so two types that claim one file in one namespace produce one `GetUserDto`, not two.  A table model's namespace is the claiming type's unless `SqlSourceModelNamespace` is set, so queries in two namespaces that match one table get two table models unless the project sets the namespace; the README says so.
- **Namespace.**  Models are namespace-level types, not nested, in the namespace of the attributed type by default.  `SqlSourceModelNamespace` as a property, as metadata, as `ModelNamespace` on the attribute, and as the preamble-only marker `-- model-namespace:` change it.  A `-- input-model:` or `-- output-model:` name that contains a period is fully qualified and ignores all of those.
- **Accessibility** follows the attributed type's effective accessibility, at least `internal`: a `public` type gets public models, an `internal` one, or a `public` one nested in an `internal` one, internal models.  An override is not in this epic.
- **Shape.**  `SqlSourceInputModelType` and `SqlSourceOutputModelType` take `record`, `sealed record`, `class` or `sealed class`, default `sealed record`, as a property, as metadata, as `InputModelType` and `OutputModelType` on the attribute with a `GeneratorModelType` enum of the four values, and as the markers `-- input-model-type:` and `-- output-model-type:` in the preamble or a query.  A record is positional, in the model's property order, which gives value equality, `with` and deconstruction.  A class has public properties with get and set and no constructor of its own.
- **Order.**  Properties are in the query's order: the parameters as the parameter list rule orders them, declared-only ones last, and the columns as the select list has them.  No custom ordering.
- **Sorting** is two generator parameters, `sort-input` and `sort-output`, sorting the properties by name, ordinal comparison.
- **Generator parameters get every level.**  `SqlSourceGeneratorParameters` as a project property, as `AdditionalFiles` metadata and as `Parameters` on the attribute holds the same space-separated list that the `-- generator:` marker holds.  Every switch, present and future, has every level at once.  `SqlSourceTokenValidation` is folded into it: `SqlSourceGeneratorParameters=no-token-validation` says the same thing, and the package has one mechanism for switches rather than one property per switch.
- **Each level sets the list; none adds to it.**  The list in effect for a query is the one from the most specific level that gives one, whole: the query's marker, else the file's, else the attribute's, else the metadata's, else the property's, else empty.  A level that omits a parameter gets that parameter's default, so the vocabulary is only departures from the default, `keep-comments`, `no-token-validation`, `sort-input`, `sort-output`, `no-table-models` and `async-method-suffix`, and `token-validation` is dropped, since omitting `no-token-validation` says the same.  Today's parser unions `token-ignore` and ORs `keep-comments` across the preamble and the query; after phase 1 a query's list replaces the preamble's, and the README records the change.  A level that wants to keep a parameter from above restates it.  The word `default`, alone, sets the empty list: `-- generator: default`, or `default` as the property's, the metadata's or the attribute's value.  It is a no-op when the levels above are at their defaults, which is the point: a reader sees that the query means the defaults.  A `-- generator:` with nothing after it is an error, since an empty marker reads as a mistake, and `default` beside another parameter is an error too.  This is the rule the dialect's options already follow, where a value replaces the one it wins over whole, and the README states it in one sentence beside the list.
- **Table models.**  A query whose result is exactly a table, as the sidecar's `matchesTable` records, gets a model named after the table instead of after the query, shared by every query that matches that table.  The result is exactly a table when every column has an origin in that one table, the names are the table's column names unchanged, the columns are the table's full column list in the table's order, and each column's nullability is the table column's.  `SELECT *` and an explicit full list in table order qualify; an alias, a dropped column, a reordering, an added expression or an outer join does not.  Requiring the order keeps positional records right for Dapper users and makes the rule one sentence: the select list is the table's column list.  A query's own `-- output-model:` wins over the table's name.
- **A shared name is a shared model.**  Two queries whose output models resolve to one full name get one model, emitted once, when their columns' names, types and nullability are identical and their effective model type and sorting are the same, and an error at the second query's marker when they are not.  The same holds for input models and parameters.  This is how a project shares a model between queries that read the same shape: under its control, by one marker per query.
- **Property names** come from column and parameter names by PascalCasing: `created_at` to `CreatedAt`.  A name that is not an identifier after that, or two columns with one name, which `SELECT a.id, b.id` produces, is an error that asks for an alias.
- **Documentation.**  Each generated type and member has XML documentation: the query's summary, the database type and facets of each member, and the origin table and column when there is one.

Recommended:

- **No deduplication by shape.**  Two queries with identical shapes have two names, and a model shared by inference has to take one of them, which is arbitrary, and unstable: a query added earlier in the file, or a column added to one of the two, renames or splits the shared model and breaks code far from the edit.  Table models share under a name that comes from the schema and cannot drift; the shared-name rule shares under a name of the user's choosing.  Between them, shape-matching heuristics have no place.
- **Table model names and default.**  PascalCase of the table name plus the output suffix: `users` gives `UsersDto`.  No singularising, since inflection is wrong often enough in English and always elsewhere.  The schema is dropped when it is the engine's default, `public` or `dbo`, and prefixed otherwise, so `billing.invoices` gives `BillingInvoicesDto` and cannot collide with `public.invoices`.  Table models are on by default and `no-table-models` turns them off.  The README states the one consequence worth knowing: a query that stops matching, because the table gained a column and the query lists its columns, gets a model of its own name again, which is a compile error wherever the table model was used; `SELECT *` queries never pay it.
- **Dapper and sorting.**  Dapper matches a constructor's parameters to the columns in order, so a sorted positional record no longer maps through Dapper; the generated methods call the constructor themselves and are unaffected.  The README says that a project that runs sorted models through Dapper uses `class` models, and that snake_case columns need `DefaultTypeMap.MatchNamesWithUnderscores` there.
- The attribute ends with `Path`, `SqlLocation`, `Output`, `InputModelSuffix`, `OutputModelSuffix`, `ModelNamespace`, `InputModelType`, `OutputModelType`, `MethodLocation`, `CollectionType` and `Parameters`.  Every one is optional and follows one rule.  The expected use is project-level defaults for most projects, `AdditionalFiles` metadata or file and query markers for the rest, and the attribute rarely; it is there so that the chain has no gap.

### Diagnostics

A user who reports "the model for this query is wrong" will usually share neither the schema nor the SQL.  The describer is a pipeline, and the step that produced the wrong answer has to be found from what the user will share.  Four layers, each cheaper to share than the next.

Decided:

- **Provenance in the sidecar.**  Informational fields that say which step decided a value; the generator never branches on them, and a reader ignores an unknown value.  `nullableSource` on every column: `server`, `catalog`, `outer-join`, `view`, `no-origin`, `heuristic`, with `outer-join` beating `view` beating `catalog` inside the walk.  `plan` on every entry with rows: `not-needed`, `walked`, `unavailable`, `skipped`, which tells a `heuristic` apart between "the walk could not match this origin", a walk bug, and "`EXPLAIN` failed".  `tableMatch` on every entry with rows: the first failing check in a fixed order, `matched`, `no-origin`, `several-tables`, `names-differ`, `columns-differ`, `order-differs`, `nullability-differs`.  `typeSource` gains `inferred-from-copies` for SQL Server's rename-and-unify.  The [sidecar format design](2026-10-07-sidecar-format-design.md) has the fields.  Nothing else goes into the sidecar: no plans, no timings, no catalog rows.
- **A log file.**  `--verbose` writes prose progress to the console for the person watching; `--log <path>`, or `SQLSOURCE_LOG`, writes JSON Lines for the run, one object per event, overwritten each run.  Per run: tool, format, runtime and OS versions, the command line with connection values replaced.  Per database: logical name, engine, driver assembly version, server version, and the session settings that change a description, `search_path`, `plan_cache_mode` and `standard_conforming_strings` on PostgreSQL, `compatibility_level`, `ANSI_NULLS` and `QUOTED_IDENTIFIER` on SQL Server; never host, user or database name.  Per query, in pipeline order with timings: the SQL as sent, every description as received verbatim, the lexer-versus-Npgsql check, every catalog query and its rows, the exact `EXPLAIN` statement and its JSON, one event per plan node with its join type, parent relationship and the nullable side set after it, one event per column with its origin, the node it matched and its source, the table match, and the result or the server's error verbatim.  A payload over 1 MiB is cut and marked.  Never written: a connection string or any keyword of it, a password, token or certificate, the environment.  Row data cannot exist, since nothing executes; literals do reach the log through the SQL and the plan's filter strings, and the log is the user's own file.
- **Driver logging** is wired into the same file when `--log` is given: Npgsql through its logger factory at Debug with parameter logging off, SqlClient through its event source at Informational with execution and trace keywords.  Enough to see the wire-level order and which statement failed without a second tool.
- **The seam.**  Each describer depends on one interface whose every method is a request and an answer: server info, derive parameters, describe columns, explain, attributes, types, table columns, and the SQL Server equivalents.  Three implementations: live over the driver; recording, which wraps live and appends every call and answer to a capture; replaying, which answers from a capture keyed by method and a hash of the arguments, and fails loudly on a call the capture lacks, so that a pipeline change that asks a new question is a visible fixture update.  The three are engine-neutral, one exchange that every engine's interface calls through.  Sub-phase 2.5 delivers its interface and the live shape; phase 3 delivers recording, replaying and the capture, where a real describer's requests and answers say what a capture must hold.  The nullability matrix is recorded once against a container and then runs on every CI machine without Docker; a user's bundle becomes a regression test by copying one file.
- **Error messages.**  The first line is the compiler's form; continuation lines carry `query:` with the query name, logical database, engine and server version, driver and version; `step:` from a fixed list, `describe parameters`, `check parameters`, `describe columns`, `catalog`, `explain`, `walk`, `table match`, `resolve type`, `write sidecar`; `server:` with the SQLSTATE or error number and message verbatim, never paraphrased; `help:` with the fix; `see:` with the link.  One id per actionable cause, not per SQLSTATE: PostgreSQL's `42P18` and `42P08` get their own, every other server rejection shares one.  The text states the problem and only `help:` suggests the fix.  No per-error verbose hint; a failed run ends with one line that says how to get a log or a bundle.
- **A bug-report bundle and replay**, phase 10.  `sqlsource diagnose --query Users.sql:GetUser` re-describes one query and writes a zip holding the entry, the capture, the walk events and the log, with identifiers obfuscated by keyed hashing per class, schema, table, column, alias, function, type and literal, and the mapping written beside the zip, never inside it.  Obfuscation is dictionary-based from the vocabulary the tool already holds, so the tree, the join types, the origins and the `NOT NULL` flags survive and the walk gives the same answer on the bundle.  The engine's own names, `pg_catalog`, `public`, `sys`, `dbo` and built-in functions and types, stay verbatim.  `sqlsource replay <bundle>` runs the pipeline from the capture with no database and diffs its entry against the bundle's: a reproduction means the bug is in the pipeline and the bundle is a fixture; no reproduction means the live step diverged, and the driver lines are the next read.

Recommended:

- The diagnostic ids are a new range, `SQLSRC2xx`, for the generator's and the tool's conditions alike: a stale, missing or orphaned snapshot entry; a sidecar whose engine is not the file's dialect; version mismatches; an unmapped type; an unsupported type; a parameter with no type and no marker; a column with no name; two columns with one name; a name that is not an identifier; the server's errors; the lexer-versus-Npgsql disagreement.  The tool uses the generator's ids where the condition is shared.  `docs/diagnostics.md` gains each one in the phase that adds it.  The tool's descriptors live in the generator assembly beside the generator's, so the diagnostics document test and `AnalyzerReleases` cover both; a parse error phase 1 adds takes the next `SQLSRC1xx` id, as the parser's errors do today.  Phase 2 uses `SQLSRC200` to `SQLSRC221`, assigned to its sub-phases in its section below, and the later phases take ids from `SQLSRC222`.
- Fixtures live under `tests/SqlSource.Tool.Tests/Fixtures/<engine>/<case>/` as `query.sql`, `capture.json`, `expected.json` and an optional `issue.md`, with `capture.json` byte for byte the bundle's.  A `captureVersion` and `tools/record-fixtures.sh`, phase 3's, which re-records the unobfuscated fixtures against the containers, mitigate the coupling of captures to the interface's shape; an obfuscated fixture that outlives its interface version is deleted and its case re-tested by hand.
- `--log` without `--verbose` keeps the console quiet; `--verbose` without `--log` writes nothing to disk.  There is no `--diagnostics <dir>`; the bundle is the directory-shaped output.

### Testing

Decided: one end-to-end test project per supported database, each with its own `.sql` files, a schema script, and **committed sidecars** so that the project compiles with the generator like any consumer.  Each runs against a Testcontainers instance with the schema applied and has three kinds of test:

1. The tool in `--check` mode against the container, asserting that the committed sidecars are what the tool produces today.  This proves the describer.
2. Every query executed through its generated method, asserting the typed results, nullability included.  This proves the type map and the generated ADO.NET code at run time.  Until phase 8, the projects execute through Dapper, and a Dapper test stays afterwards for the subset of users who run the models that way.
3. The error paths, a stale sidecar and an undescribable query, by running the tool against a scratch copy.

Decided, the projects:

- `tests/SqlSource.Tool.Tests` for the tool: the command line, the settings resolution and the describer pipelines through the replay seam from recorded fixtures, with no database.  The snapshot's reader and writer are generator code, and their round trip is in `tests/SqlSource.Tests`.  `tests/SqlSource.Tests.Postgres` and `tests/SqlSource.Tests.SqlServer` for the end-to-end projects, following the `SqlSource.Tests.<Variant>` naming.  Each references the generator the way `tests/SqlSource.Tests` does, a project reference as an analyzer with the props and targets imported by path, and the tool as a project.  Docker is already a required tool, so Testcontainers adds nothing to the setup; `CONTRIBUTING.md` says which images the tests pull.
- One project per database, not per output mode.  A project holds files with `output` set to each of `Sql`, `Models` and `CodeGen`, classes with each `SqlLocation` and `MethodLocation`, each collection type and each shape, so one project covers the combinations and proves the tool's filtering.  Each project uses two logical databases on one container, `app` and `billing`, so the database names and the per-name connections are exercised.
- A `schema.sql` per logical database in the project, applied by the test fixture after the container starts with plain ADO.NET, split on `GO` for SQL Server.  No migration library: it would add a dependency to test a feature that does not depend on one.
- The sidecars are committed, produced by `tools/describe-end-to-end.sh`, phase 5's, which starts the containers, applies the schemas and runs the tool.  The `--check` test asserts they match what the tool produces today, so a schema or query change that forgets the script fails CI with a diff, the same discipline the epic asks of users.
- Images are pinned by tag in one place per project, with an environment variable to override, defaulting to the newest release: PostgreSQL 18 and SQL Server 2025.  CI runs each project twice, newest and oldest supported: PostgreSQL 14, the oldest line in support, and SQL Server 2017, the oldest image Microsoft publishes.  For PostgreSQL that covers both sides of the `EXPLAIN (GENERIC_PLAN)` boundary at 16, so the `PREPARE` path runs on 14 and the direct path on 18.  SQL Server's image is x64 only; on Apple silicon it runs under Docker's emulation, slowly, which `CONTRIBUTING.md` says rather than the tests skipping themselves.
- The tool runs in process through a public `Cli.Run(args)` entry, for speed and debuggability, with one test per project that runs the packed command as a subprocess to prove the command line.  The container is a collection fixture shared by the project's tests and cleaned up by Testcontainers' reaper.
- No Roslyn-floor variant: the existing floor project recompiles the generator tests, which is where model and method emission are unit-tested.

Recommended:

- The hash, the parameter lexeme, the markers and the type map are generator code and are tested in `tests/SqlSource.Tests`.  The container tests prove the live describer implementations and record the fixtures that `SqlSource.Tool.Tests` replays.
- The nullability inference for PostgreSQL gets a test matrix of its own: left, right and full joins; nested joins; a self-join on both sides; a `LEFT JOIN` reduced to inner by a `WHERE` and by a later inner join; an outer join inside a subquery and inside a CTE; `LEFT JOIN LATERAL`; a view over an outer join; `USING` and `NATURAL`; aggregates over an outer join with a plain group key; `UNION` of two outer-join queries; and each plan shape, hash, merge and nested loop, by toggling `enable_hashjoin`, `enable_mergejoin` and `enable_nestloop`.
- `tools/check-package-install.sh` gains a run of the packed tool, so the packed tool and the packed generator are proven together once per build.  It proves packaging, not behaviour: the packed tool's `--help`, and a `describe` with no connection asserting the expected error, with no database.  Behaviour is the end-to-end projects' job, and this keeps Docker out of that script.  That is phase 9's; from sub-phase 2.2 a script of its own, `tools/check-tool-install.sh`, installs the packed tool alone and runs it, which proves that its dependency closure loads.

### Documentation

Each phase keeps the documents current, by the rules in `AGENTS.md`:

- `README.md`'s installation section changes with phase 5: add the package and the tool, set the dialect, run `describe`, commit the sidecar; and a one-line note that a project wanting only the constants sets `SqlSourceOutput=Sql` and can keep the default dialect.  It gains a section on models: running `describe`, committing the sidecar, the overrides, the type map and its option, the Dapper settings a user who stays with Dapper needs, and what is unsupported.  Phase 5 writes it for PostgreSQL; phase 6 adds SQL Server; phase 7 the library maps; phase 8 the generated methods; phase 9 the online mode.
- `CONTRIBUTING.md` gains the tool project, the end-to-end projects and their images, and the new package check.
- `docs/publishing.md` covers the second package from phase 2.
- `docs/tech-debt` gains the tool's syntax-level attribute reader in phase 2.
- `docs/diagnostics.md` gains each diagnostic in the phase that adds it.

## Phase 0 - names

### Scope

1. `SqlQueriesAttribute` becomes `SqlSourceGenerateAttribute`, used as `[SqlSourceGenerate]`; `SqlQueriesMode` becomes `SqlLocation`, and so does the attribute's `Mode` property.  The values `Nested` and `Direct` and the `Path` property are unchanged.
2. `-- SqlSource:` becomes `-- generator:`, and its `dialect=` directive becomes the marker `-- dialect: postgres`.  What a `-- generator:` line holds is called a generator parameter, not a directive, in the code and the documents.
3. Every place that names them: the generator's emitted source, the suppressor, the parser, the README, `docs/diagnostics.md`, the tests, and `tools/package-install`.

### Decided

- Renames and nothing else, in one pull request with one commit per rename, so that each diff is mechanical and the later phases start from the new names.
- The rule the README states for `.sql` files: a line comment of the form `-- word: rest` whose word SqlSource knows is a marker; nothing else in a comment is read.  Each marker has its own grammar for the rest.  `-- generator:` is the marker whose rest is a list of generator parameters: `keep-comments`, `token-validation`, `no-token-validation` and `token-ignore=name`, the switches that have no value of their own.
- A setting with one value, and an MSBuild property and metadata beside it, is a marker: `-- dialect:` now, `-- database:` and `-- output:` in phase 1.  The dialect marker keeps the placement rule the directive had, before the first `-- name:` line and before any SQL, since it changes how the lines after it are lexed.
- This is the last phase that can rename what a user writes at no cost, so it ships before anything else does.
- The dialect marker's value is the rest of its line, read as the `SqlSourceDialect` property is: `-- dialect: mysql, ansi-quotes` is accepted, where the directive's value was one word.  The old spellings are not kept: `-- SqlSource:` is an ordinary comment, and `dialect=` in a `-- generator:` line is an unknown generator parameter.

### Technical notes

- The diagnostics that mention the directive form, the invalid dialect in a file and the misplaced dialect, are reworded, keep their ids, and their sections in `docs/diagnostics.md` follow.
- The parser's names for the directive scope and the preamble dialect follow the rename, so that later phases do not read "directive" in code and "marker" in the documents.

### Testing

The existing tests, under the new names and markers.  The package-install check proves the attribute reaches a consumer under its new name and that a `.sql` file with the new markers builds.

## Phase 1 - parameters and settings

### Scope

1. A `Parameter` lexeme kind in `SqlLexer`, found by the rule under Parameters, with the prefix a field of the dialect's rules.
2. `SqlBlock` and `SqlQuery` carry the ordered list of parameter names.  The segments are unchanged: a parameter stays in the SQL as written.
3. The hash of a query, by the one definition under Tokens, computed where the generator builds the emitted text, so that the tool and the generator cannot disagree.
4. The `-- param:` marker, inside a query only: parsed, validated and carried on the block, with no effect yet, and the parameter list rule under Parameters.
5. Token defaults: the inline `{{name:default}}` form in the token scanner, the `-- token:` marker, the conflict errors, and each token's resolved default carried on the block.  `-- token-ignore:` as a marker, and the `token-ignore=` parameter dropped.
6. The `-- database:` and `-- output:` markers, in the preamble and inside a query, carried on the block; the `SqlSourceDatabase` and `SqlSourceOutput` property and metadata in the props and targets; the `GeneratorOutput` enum and the attribute's `Output` property; the two-stage settings record that resolves every setting by the table under What is generated.  `output` is validated and has no effect yet; `database` is never read by the generator.
7. The `-> shape` suffix on the name marker, parsed and validated, with no effect yet.
8. The model settings under Models, each at every level the table gives: the suffixes, the namespace, the model types with the `GeneratorModelType` enum, the per-query names, the collection type with `GeneratorCollectionType`, the method location with `MethodLocation`, on the attribute alone, and `SqlSourceGeneratorParameters` with its metadata and attribute property, into which `SqlSourceTokenValidation` is folded.  The `sort-input`, `sort-output`, `no-table-models` and `async-method-suffix` parameters are accepted, `token-validation` is dropped, and each level sets the list whole.  All validated, none with an effect before phase 5.
9. The new enums join the attribute in `AttributeSource`, in the suppressor's list and in the README's note on friend assemblies, as `TD-0006` requires of every generated type.
10. `InternalsVisibleTo` for `SqlSource.Tool`.

### Decided

- Parameters are found by the lexer, under the file's dialect.
- `keep-comments` is the one generator parameter that changes what the parser builds, and the levels above the markers resolve after the parse.  The parser therefore always builds the comment-stripped SQL, and builds the SQL with comments beside it when a marker asks for it or when the file's input says it may be wanted: the property, the file's metadata, or the `Parameters` of a type that claims the file.  That flag is an input of the parse, as the dialect is, and is the one way an edit outside a file parses it again.
- A file with no `-- name:` marker is preamble and query at once: every marker is allowed in it.
- The parameter list's errors are parse errors whatever the output resolves to.
- `-- param:` also accepts `not null`, which says what saying nothing says and is kept apart from saying nothing.
- The word `default` in a list of generator parameters is the empty list, at every level, and written twice it is still the empty list; beside another word it is a conflict, or an invalid value outside a marker.
- A `-- token:` or `-- token-ignore:` marker for a token that its query does not hold is not an error.
- A value from a fixed list is matched ignoring case and ignoring hyphens and spaces inside it, in a marker, a property, the metadata and the attribute alike.
- `SQLSRC006` covers every invalid attribute value and `SQLSRC014` every invalid MSBuild value, property or metadata, with no position.
- The metadata that the compiler reads is on items of the type `SqlSourceSettingsFile`, and one target trims every metadata.
- Nothing a user sees changes beyond the attribute's new properties, which are accepted and have no effect yet, `SqlSourceTokenValidation` giving way to `SqlSourceGeneratorParameters`, `token-ignore` becoming a marker, and a query's generator parameters replacing the preamble's instead of merging.  No member is generated from the parameter list, and the only diagnostics added are invalid setting values and whatever the new markers need, with the next `SQLSRC1xx` ids for parse errors and `SQLSRC0xx` for setting values, as today; `SQLSRC010` is removed with `SqlSourceTokenValidation`, and `docs/diagnostics.md` and `AnalyzerReleases.Unshipped.md` follow.

### Recommended

- The marker is `-- param: <prefix><name> [<database type>] [null | not null]`, as under Overrides, matched like the other markers.  The type is the rest of the line before an optional trailing `null` or `not null`, so a type with facets, `decimal(18,2)`, or with spaces, `double precision`, needs no quoting.
- `System.Security.Cryptography.SHA256` is available to a `netstandard2.0` analyzer.
- This phase is the largest parser change of the epic, and its spec should order the work so that each marker and setting lands with its tests before the next.

### Technical notes

- MySQL and MariaDB read `@name` as a user variable and their connector treats it as a parameter by default; the rule is the same for them.  Oracle uses `:name`.
- PostgreSQL's `@` is also the absolute-value operator, and `@x` written without a space reads as a parameter under the rule; Npgsql reads it the same way, so the cross-check cannot catch it, and the README says to write `abs(x)`.

### Testing

Theory tests over the rule under each dialect, including `@` inside strings, comments and quoted identifiers, the operators that contain `@`, `@@` functions, and a parameter at the start and end of the text.  Round-trip tests that the hash is stable across line endings and `keep-comments`, and changes with a default, a declaration and a token name.

## Phase 2 - the tool and the snapshot

Phase 2 is six sub-phases, each with a spec, a plan and a pull request of its own.

| Sub-phase | Spec | Builds on |
|----|----|----|
| 2.1 The snapshot format | [phase-2-1-snapshot-format](2026-10-09-query-generation-phase-2-1-snapshot-format-design.md) | Nothing |
| 2.2 The tool's shell and its package | [phase-2-2-tool-shell](2026-10-09-query-generation-phase-2-2-tool-shell-design.md) | Nothing |
| 2.3 The project manifest and discovery | [phase-2-3-project-manifest](2026-10-09-query-generation-phase-2-3-project-manifest-design.md) | 2.2 |
| 2.4 The work list | [phase-2-4-work-list](2026-10-09-query-generation-phase-2-4-work-list-design.md) | 2.3 |
| 2.5 Describe | [phase-2-5-describe](2026-10-09-query-generation-phase-2-5-describe-design.md) | 2.1 and 2.4 |
| 2.6 `--check` and logging | [phase-2-6-check-and-logging](2026-10-09-query-generation-phase-2-6-check-and-logging-design.md) | 2.5 |

### Scope

1. **2.1.**  The snapshot model, the shared reader and writer, and the format version, as the [sidecar format design](2026-10-07-sidecar-format-design.md) specifies; the JSON Schema under `schemas/`; the `_WARNING` property; the comparison `--check` makes and the test of a current entry.  `PackageVersion` is in the root namespace, not in `Snapshot/`.
2. **2.2.**  `src/SqlSource.Tool`: the console application, its package, its command line through System.CommandLine, its exit codes, its error output format under Diagnostics, the public `Cli.Run(args)` entry the tests call, and finding the unit of a run.  Publishing for the second package, and `tests/SqlSource.Tool.Tests`.
3. **2.3.**  Listing a solution's projects, the `SqlSourceImported` marker, the project manifest target in `SqlSource.targets` and its format with its version, and running it to get the compiler's view of the `.sql` files with their dialects, databases and outputs, and of the `Compile` items.  Several target frameworks.  `--project`.
4. **2.4.**  The tool's syntax-level attribute reader, the shared path resolver, and the settings resolution per type and query with the shared code, leaving out every unclaimed file and every project, type, file and query whose output is `Sql`.  `--database` and a `.sql` file path.  A token without a default reported.
5. **2.5.**  `IQueryDescriber`, the engine-neutral `QueryDescription`, the exchange's interface with its live shape, and a describer registry keyed by dialect with no engine registered yet.  A run whose dialect has no describer reports it per query and exits one, with a message that tells "this version has no describer for the dialect" apart from "the dialect cannot be described".  `sqlsource describe`, `--force`, the connection options and the environment variables, the sidecar-written rule, the skip rule, and token defaults substituted into the sample SQL before describing.
6. **2.6.**  `--check` with its comparison rule.  `--verbose`, `--log` and `SQLSOURCE_LOG`, with the run and database events; the describer's events come with phase 3.

### Decided

- The tool is a console application packed as a .NET tool.
- `--check`, `--force` and `--database` ship in this phase.
- The format carries the format version and the tool version, compared by prefix.
- The tool references a current Roslyn; its attribute reader reads `Path` and `Output` alone; a solution is one MSBuild process for each project; the tool never restores.  Each changes what this outline said before the sub-phases were specified, and is recorded where it stood.
- A `.sql` filter is positional; a file held back from being written, though a query of it was described, is an error; `--connection` is always `name=value`; a database with no connection and nothing to describe is not an error; a file that a target adds is in the manifest only through the package's own hook.
- Left out of the scope this outline first gave phase 2: a pin of `sqlsource` in this repository's tool manifest, since the end-to-end projects use the tool as a project.

### Settled

What the outline recommended or left open, and each sub-phase's spec settled:

- **The description.**  Engine-neutral at the top and engine-specific in the type, and the type is the sidecar's own type record from 2.1, so that a describer's answer and an entry say a type one way:

```
QueryDescription
    ResultKind        Rows | None
    Parameters        [ { Name, Type: SidecarType?, TypeSource } ]
    Columns           [ { Ordinal, Name, Type: SidecarType, Nullable: bool?, NullableSource, Origin: { Schema, Table, Column }?, Identity, Computed } ]
    MatchesTable      { Schema, Table }?
    Plan, TableMatch  provenance

SidecarType, PostgreSQL     { Name, Kind: base | array | domain | enum | range | multirange | composite, Schema, InternalName, facets, Element | Base | Labels | Subtype }
SidecarType, SQL Server     { Name, MaxLength, Precision, Scale, UserType: { Schema, Name, AssemblyQualifiedName }? }
```

  A parameter's type, a nullability and an origin are optional because the engines outside this epic need them to be: MySQL reports every parameter as `VARCHAR`, SQLite and Oracle report none, DuckDB reports no nullability, Oracle no origin.  `null` means unknown.  The description holds database types, never OIDs, which are per database, and never C# types.  A parameter's ordinal and nullability are not the describer's: the run takes them from the query's parameter list and its `-- param:` markers.
- **The project manifest** is lines of `key=value`, not JSON: MSBuild cannot write a Windows path into JSON without escaping it in a target for each item.
- **The diagnostics**, all in `Diagnostics/ToolDiagnostics.cs` of the generator assembly:

| Ids | Sub-phase | About |
|----|----|----|
| `SQLSRC200` to `SQLSRC203` | 2.2 | An exception; no unit, several units, a path that is no unit |
| `SQLSRC204` to `SQLSRC207`, `SQLSRC220` | 2.3 | A project that does not use SqlSource; MSBuild failed; a manifest that cannot be read; a `--project` outside the run; a project that was never restored |
| `SQLSRC208` to `SQLSRC212` | 2.4 | An attribute argument that is no literal; an output that needs a describable dialect; a token with no default; a database with two dialects; a file filter outside the run |
| `SQLSRC213` to `SQLSRC218`, `SQLSRC221` | 2.5 | No connection; two databases with one variable; `SQLSOURCE_CONNECTION` and several databases; no describer for the dialect; a sidecar not written; a file that could not be written or deleted; a sidecar of a newer tool |
| `SQLSRC219` | 2.6 | A sidecar that `--check` found out of date |

  `SQLSRC209` and `SQLSRC210` are also the generator's, from phase 5.
- **What a filter lets a run change.**  A sidecar is written only for a file with a selected query, and deleted only in a run with no filter.  `--check` compares within the same bounds, so it reports what `describe` with the same filters would change.
- **A wrong command line** is reported by the tool, and never repeats the text of a token it did not expect: a misspelt `--connection` must not print the connection string after it.
- **The summary under `--check`** counts `same`, `differ` and `failed`.
- **A sidecar of a higher format version** is an error for `describe`, which leaves it alone; a lower one is written again.

### Testing

The snapshot round trip in `tests/SqlSource.Tests`.  The command line, the manifest reader, the attribute reader, the settings resolution and the whole of `describe` over a describer of the tests, all without a database, in `tests/SqlSource.Tool.Tests`.  The project manifest target against fixture projects and against this repository's own test project, with the real `dotnet msbuild`.

### Documentation

`CONTRIBUTING.md` for the new projects and their tests.  `docs/publishing.md` for the second package.  `docs/tech-debt` for the syntax-level attribute reader.  The README waits for phase 5, when a user can do something with the output; this phase's package is published so that phase 5 can be tried against it.

## Phase 3 - the PostgreSQL describer

### Scope

1. The PostgreSQL describer over Npgsql, registered for `postgres`, behind the seam.  Sub-phase 2.5 left the seam's interface and its live shape; this phase adds the recording and replaying shapes and the capture file, engine-neutral, with requests and answers that are plain data a capture can hold: a call that can fail on the server returns the failure as its answer.
2. Parameters, columns, the nullability walk, the table match from `pg_attribute`, type resolution through Npgsql's type catalog, the declared-type route the spike settles, and the lexer-versus-Npgsql check.
3. The provenance fields, the describer's log events, driver logging wired in, and the fixture layout under Diagnostics, with the nullability matrix recorded as fixtures.

### Decided

- The describer records; the generator decides.  Every `nullable` in the sidecar is the server's or the walk's answer, with its source.

### Recommended

The description is phase 2's; this describer fills it as follows.

The PostgreSQL describer:

1. **Parameters.**  `NpgsqlCommandBuilder.DeriveParameters(command)` on the original SQL.  It sends `Parse` with no types and `Describe`, and sets each parameter's `PostgresType`, a resolved object: a base type, a domain with its base, an array with its element, an enum with its labels, a range with its subtype, a composite with its fields.  Npgsql does the `@name` rewriting here, which is the lexer check under Parameters.  A `-- param:` type is declared instead, by the route the spike under Parameters settles.
2. **Columns.**  `ExecuteReader(CommandBehavior.SchemaOnly | CommandBehavior.KeyInfo)` with the parameters typed from step 1, then `GetColumnSchema()`.  `SchemaOnly` sends `Parse` and `Describe` only.  `KeyInfo` makes Npgsql join `pg_attribute` for every column with a table origin and fill `AllowDBNull` from `attnotnull`, with `BaseSchemaName`, `BaseTableName` and `BaseColumnName`.  A column with no origin keeps `AllowDBNull` null.
3. **Nullability.**  The protocol does not report it, and the one way the origin's `attnotnull` is wrong is the unsafe one: a `NOT NULL` column from the nullable side of an outer join.  Three layers, decided under Nullability:
   - *The origin.*  `attnotnull` of the origin column from step 2; unknown when there is no origin.  A view's columns are unknown by construction, since their `pg_attribute` rows never say `NOT NULL`, so a view that hides an outer join is already safe.
   - *The gate.*  The lexer looks for `LEFT`, `RIGHT`, `FULL` or `OUTER` as a word outside strings, comments and quoted identifiers.  A query without one stops at the origin: no `EXPLAIN`, no dependence on a plan.  The gate is safe because the one way an outer join hides from the text, a view, is already unknown.
   - *The plan walk*, for gated queries.  The plan comes from `EXPLAIN (GENERIC_PLAN, VERBOSE, FORMAT JSON)` on 16 and later, and below that from `PREPARE`, `SET LOCAL plan_cache_mode = force_generic_plan` and `EXPLAIN EXECUTE` with nulls, in a transaction that is rolled back; a generic plan, so that `WHERE x = $1` is not folded for the null placeholder.  The root node's `Output` is the result's target list in order, so column *i* is `Output[i]`; a plain column reference prints as `alias.column`, which names the relation instance and so handles a self-join, where the origin's table OID cannot say which side a column came from.  A relation instance is nullable when any join on the path from the root to its scan null-extends its side: for `Join Type` `Left` the child whose `Parent Relationship` is `Inner`, for `Right` the child that is `Outer`, for `Full` both; `Inner`, `Semi` and `Anti` null-extend nothing.  A `Subquery Scan` or `CTE Scan` maps its outputs to its child plan's by position and recurses, the CTE's plan found by its `Subplan Name`.  An `Output` entry that is not a plain reference, an expression or an aggregate, stays unknown; a `GROUP BY` key that is a plain reference keeps its origin's answer.  A column is non-null only when its origin says `NOT NULL` and the walk found its relation instance under no null-extending side.
   - *The safety net.*  A join type the walk does not know, an `Output` entry it cannot map, or a plan it cannot obtain drops the query to the heuristic: every column of a gated query is nullable.  So the walk is wrong only where its join-side rule is wrong, and the matrix under Testing exists to prove that rule; SQLx's one open bug is a `Hash Right Join` whose sides it read backwards, and the matrix forces that shape by toggling the planner's join methods.
   - The plan beats the text because the planner has applied join strength reduction: `A LEFT JOIN B ... WHERE b.x IS NOT NULL`, or a `LEFT JOIN` followed by an inner join on the joined table, is planned as an inner join, and the walk says non-null, which is correct and which no text rule gets right.
   - The outcome, `true`, `false` or `null`, is what the sidecar records, with the layer that decided it as provenance (see Diagnostics).  The override suffix and the policy that unknown means nullable are the generator's, applied when it reads.

The server's errors and what the tool says:

| Server error | Cause | The tool reports |
|----|----|----|
| `42P18 could not determine data type of parameter $n` | Used nowhere that fixes its type: `SELECT @p`, `COALESCE(@a, @b)` | Add a cast, `@p::int`, or a `-- param:` type. |
| `42P08 inconsistent types deduced for parameter $n` | Used as two types | Cast one use, or use two parameters. |
| Any other | The query is not valid against this schema | The server's message, at the query. |

`record` and `unknown` as a column type are reported as unsupported with the column named.  A polymorphic function's result is already concrete at `Describe`.

### Technical notes

- `Parse` and `Describe` plan and execute nothing; `Describe` of a statement that returns no rows answers `NoData`, and `RETURNING` lists are treated as select lists.  Table origins survive subqueries in `FROM`, CTEs and joins; they are zero for expressions, function results, casts, aggregates, the merged column of a `USING` join, and the whole output of a `UNION`, `INTERSECT` or `EXCEPT`.  A view reports the view's OID and column, and a view's columns are never `NOT NULL` in `pg_attribute`, so they are nullable unless overridden.
- Versions: `Parse` and `Describe` are protocol 3.0; `EXPLAIN (FORMAT JSON)` is 9.0; `plan_cache_mode` is 12; `EXPLAIN (GENERIC_PLAN)` is 16.  The supported floor is PostgreSQL 14, the oldest line in support.
- Unverified, and the first thing this phase's spike settles: that `SchemaOnly | KeyInfo` together behave as described in Npgsql; that `EXPLAIN (GENERIC_PLAN)` accepts `$n` through the extended protocol, or whether the `PREPARE` form is needed on 16 too; and what `Describe` reports for a domain-typed expression and for a `USING` column of a `FULL JOIN`.
- The tool must leave the database as it found it: a `PREPARE` is deallocated, a `SET` is session-local, nothing is executed.

### Testing

The describer against a PostgreSQL container, with the nullability matrix under Testing above, recorded as fixtures that `SqlSource.Tool.Tests` replays.  The end-to-end project for PostgreSQL is phase 5's, since it needs the models; this phase's container tests assert descriptions.

## Phase 4 - defaults

### Scope

1. Every `.sql` file in `tests/SqlSource.Tests`, `tests/SqlSource.Tests.RoslynFloor` and `tools/package-install`, and every test that hands SQL to the generator, gets an explicit `SqlSourceOutput=Sql`, so that phase 5's defaults, `CodeGen` under `ansi`, break none of it.  A dialect alone protects nothing: a `postgres` file at the default output still needs a sidecar.  Fixtures that want models arrive in phases 5 and 6 together with their committed sidecars.
2. The package-install project's expected output, if it changes.

### Decided

- A repository-only change in its own pull request, so that phase 5's diff is the feature and not the migration.

### Testing

The existing suites pass unchanged in behaviour; the only edits are settings.

## Phase 5 - models

### Scope

1. The generator's pipeline reads `.sql.json` sidecars as a second file kind, paired by path, parsed with the shared reader into a value-equal record, cached like a parsed `.sql` file.  `SqlSourceOutput` takes effect: `Models` and `CodeGen` queries need an entry, `Sql` queries do not, and a `Models` or `CodeGen` query with a token that has no default is an error.  An entry whose `engine` this version of the generator has no type map for, `mssql` until phase 6, is one error at the query naming the engine, the same path as an unknown engine.
2. Hash and version comparison per query, the checks per sidecar over the union of the claiming types' needs, and the diagnostics under The sidecar.
3. The PostgreSQL type map, every type Npgsql reads; the driver probe with its feature flags and floor diagnostics; the project-level mapping override; and the two date and time options.
4. Emission of the input and output types with documentation, once per full name from the collected set; table models; the override suffix stripped from names.
5. The `DependentUpon` nesting in the targets, with its spike.
6. The PostgreSQL end-to-end project and `tools/describe-end-to-end.sh`.
7. The README's installation section and its section on models.

### Decided

- The generator never connects to anything.  Its inputs are the project's files and MSBuild values.
- Stale snapshots never produce models.

### Recommended

The items under Models and Types in C#.  Two more:

- The props include `**/*.sql.json`.  The generator's file filter already keys on the `.sql` extension and will not mistake a sidecar for a query file.

### Technical notes

- `SqlPath.IsSqlFile` matches the `.sql` extension; a sidecar ends in `.json`.  Pairing is by the normalised path with `.json` removed.
- The reader must tolerate a hand-edited sidecar: an unknown field is ignored, a malformed file is one error at the file, not a crash of the generator.
- Emission once per full name is a project-wide step after the per-type steps: `Collect()` the models every type needs, dedupe them by full name in a `Select`, which is also where the shared-name conflict diagnostics come from, then `SelectMany` back to one node per model, so that an edit to one file re-emits only the models whose shape changed; one `RegisterSourceOutput` over the collected set would re-add every model on every edit.  Each model's hint name goes through `HintName.FindAmbiguous` together with the types' hint names, since the compiler compares hint names ignoring case and throws on a clash.

### Testing

Generator tests over sidecars: every row of the state table, the type map, naming, the override suffix, two types claiming one file, two namespaces matching one table.  The PostgreSQL end-to-end project, with the three kinds of test under Testing.

## Phase 6 - SQL Server

### Scope

1. The SQL Server describer over Microsoft.Data.SqlClient.
2. The SQL Server type map, every type SqlClient reads, and its driver probe.  The library maps are phase 7's.
3. The SQL Server end-to-end project, with `tools/describe-end-to-end.sh` and the CI matrix extended for it.
4. The README's SQL Server additions.

### Decided

The describer, behind the seam of phase 3, with the same provenance, log events and fixtures:

1. **Parameters.**  `sp_describe_undeclared_parameters @tsql` returns one row per undeclared `@name` with `suggested_system_type_name`, facets included, and `suggested_user_type_*` for alias and CLR types.  The engine takes the type from the innermost enclosing comparison, assignment, function argument, `INSERT` value or `CAST`.  A parameter used more than once is error 11508, which the common optional-filter pattern `WHERE (@name IS NULL OR name = @name)` triggers.  The tool renames each occurrence, `@name` to `@name_1`, `@name_2`, with the lexer, describes, and accepts the result when at least one copy came back typed and every typed copy agrees; a copy in a context that types nothing, `@name_1 IS NULL`, is ignored, which is the motivating pattern.  Typed copies that disagree are an error that names the parameter and the two types.  This is FSharp.Data.SqlClient's workaround without its T-SQL parser.  A `-- param:` type is declared in `@params` instead.
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

The describer against a SQL Server container, recorded as fixtures for `SqlSource.Tool.Tests`.  The SQL Server end-to-end project.

## Phase 7 - library maps

### Scope

1. NodaTime: through `Npgsql.NodaTime` on PostgreSQL and by conversion on SQL Server.
2. NetTopologySuite: through `Npgsql.NetTopologySuite` and `NetTopologySuite.IO.SqlServerBytes`.
3. `JsonDocument` and `JsonElement`: built into Npgsql, by parsing on SQL Server.  `SqlJson` on SqlClient 6.0 and later, gated on its flag.
4. Detection from the compilation keyed on the plugin assembly, a property per library, and the three map-row shapes under Types in C#.
5. The README's section on libraries, with the `Use...()` call each plugin needs.

### Decided

- The items under Types in C#.

### Testing

Each map in both end-to-end projects with the library referenced, and the detection and the properties in generator tests.

## Phase 8 - execution methods

### Scope

1. For each query whose output is `CodeGen`, a generated method that binds the parameters, runs the command and, for a query with rows, reads them into the output type by ordinal with `GetFieldValue<T>` and `IsDBNull`.
2. The result shape on the name marker, the collection type setting, the method location setting, the `async-method-suffix` generator parameter, and the exception type.
3. Both engines, in the end-to-end projects, executing through the generated methods.

### Decided

- **Plain ADO.NET** over `DbConnection`, `DbCommand`, `DbParameter` and `DbDataReader`.  The provider's own types only where ADO.NET's abstractions do not reach.  Nothing generated depends on Dapper.  A project that does not want the methods still gets the models.
- **Asynchronous only.**  The method is named after the query; the generator parameter `async-method-suffix` appends `Async`.
- **Signature**: the parameters type when the query has parameters, then the query's tokens as `string` arguments in order of first appearance, then `DbTransaction? transaction = null`, `int? commandTimeout = null` and `CancellationToken cancellationToken = default`.  The parameters object comes first because it is the what and a token is the how, and a query without parameters has only tokens either way.  `DbTransaction` and `DbConnection`, not the interfaces: the interfaces have no asynchronous surface, and `DbCommand.Transaction` is a `DbTransaction`.  The README's example injects a `DbConnection`.
- **The connection** is opened if it is closed and closed again on the way out, and left alone if it was open, which is Dapper's rule: a transaction needs an open connection before it begins, so a method called inside one finds it open and never touches it.
- **No `CommandType` or `CommandFlags`.**  `CommandType` exists for stored procedures and `TableDirect`, both out of scope; the text is always `Text`.  Dapper's flags are `Buffered`, which is the collection type here, `Pipelined`, a Dapper internal, and `NoCache`, meaningless for generated code.  `commandTimeout` is the one knob people reach for.
- **Shapes** are written on the name marker: `-- name: GetUsers -> many`.  `many` returns the collection type; `one` returns one row and throws when there are none or more than one; `one-optional` returns one row or null and throws on more than one; `none` returns nothing; `rowcount` returns `ExecuteNonQuery`'s count.  Defaults: `many` for a query with rows, `rowcount` for one without, which matches `ExecuteNonQuery` and Dapper's `Execute` at no cost.  `one`, `one-optional` and `many` are errors on a query with no result set; `none` and `rowcount` are allowed on any, since an `INSERT ... RETURNING` whose rows nobody wants is a real case.  `one` and `one-optional` read one row past the expected to detect the second.
- **The exception** is `UnexpectedRowCountException`, deriving from `InvalidOperationException` so that code written for LINQ's `Single` still catches it, emitted into the consumer's project as internal source like the attribute, since the package has no runtime assembly.
- **Collection types**: `SqlSourceCollectionType` as a property, as metadata, as `CollectionType` on the attribute with a `GeneratorCollectionType` enum, and as the marker `-- collection-type:` in the preamble or a query.  Values `IEnumerable`, `ICollection`, `IReadOnlyCollection`, `IList`, `IReadOnlyList`, `Array`, `List`, `ImmutableArray`, `ImmutableList` and `IImmutableList`; default `Array`, giving `Task<GetUserDto[]>`.  The read-only interfaces are backed by the array; `IList`, `ICollection` and `IImmutableList` by a `List<T>` or an `ImmutableList<T>`.
- **Every collection is materialised before the method returns**, `IEnumerable` included, which is the array under that interface.  A lazy `IEnumerable<T>` from an asynchronous method is Dapper's unbuffered mode and a known trap: the task completes when the reader opens, the rows are then read synchronously in `MoveNext`, the command, reader and connection must outlive the call, an error surfaces in a `foreach` far from it, and a second enumeration cannot re-read a reader.  Streaming is what `IAsyncEnumerable<T>` is for, and it is the later feature with a shape of its own.  Structurally, one reading loop feeds every materialiser.
- **Method location**: `MethodLocation` on the attribute, with a `MethodLocation` enum, and nowhere else: it is a property of the type, as `SqlLocation` is, so it has no MSBuild property, no metadata and no marker.  `ExtensionClass`, the default, puts the methods as extension methods on `DbConnection` in a generated top-level `static partial class <Type>Extensions` in the attributed type's namespace with the type's accessibility; extension methods can live only in a top-level non-generic static class, so neither the nested `Sql` class nor a non-static attributed type can hold them.  `Public`, `Internal` and `Private` put them in the attributed type as static methods of that accessibility, taking the `DbConnection` as their first argument.  `SqlLocation` does not affect methods.
- A method in the attributed type with `SqlLocation` `Direct` and no `async-method-suffix` has the same name as the query's constant, which C# does not allow in one type; that is an error that names the three settings, not a silent rename.
- **Reading by ordinal**, not by name: the sidecar fixes the ordinal of every column, and the hash guarantees the SQL is the one described.  A type the provider cannot convert to the mapped C# type is a bug in the type map, caught by the end-to-end tests.
- `IAsyncEnumerable<T>`, synchronous methods and bulk operations are out of scope.

### Technical notes

- `GetFieldValue<DateOnly>` and `GetFieldValue<TimeOnly>` work on Microsoft.Data.SqlClient 5.1 and later and on Npgsql 6 and later; the type map's options for those types are honoured by asking for the chosen type.
- A parameter whose PostgreSQL type `DbType` cannot name, an array, `jsonb`, an enum, a range, is bound by setting `NpgsqlParameter.DataTypeName` to the type's name from the sidecar.  Generated code therefore casts the parameter the command created to `NpgsqlParameter`, which it does for the `postgres` dialect anyway, and never names an `NpgsqlDbType` member.  SQL Server's `json` binds as `nvarchar` on every version; `SqlDbTypeExtensions.Json`, in the `Microsoft.Data` namespace, waits for phase 7's `SqlJson` map and is gated on its flag.
- A `SqlParameter` for `decimal` needs precision and scale set, or the server rounds; for `nvarchar(n)` a size, or the plan cache fragments.  The description's facets give both.
- The `-> shape` suffix changes the name marker's grammar, which phase 1 owns; phase 1 parses and validates it and this phase gives it effect.
- `rowcount` on a statement with a result set returns whatever `ExecuteNonQuery` returns for it, `-1` on SQL Server for a `SELECT`; the README says so.
- `UnexpectedRowCountException` joins the attribute, the enums and the suppressor's list of generated types, as every generated type must.

### Testing

The end-to-end projects run every query through its generated method and assert rows, counts, nulls, each shape's success and failure, each collection type, the transaction and timeout arguments, cancellation, and that a closed connection is closed again and an open one left open.  Generator tests cover the method shapes, the locations and the parameter binding code for every row of the type map.

## Phase 9 - build integration

### Scope

1. The online-mode target in `SqlSource.targets`, the `SqlSourceOnline` property, the tool's `--manifest` and `--online` options, and the `BuildFileTests` extensions.
2. The `.env` file as a connection source in the tool.
3. `sqlsource describe --watch`.
4. The packed tool in `tools/check-package-install.sh`.
5. The README's online-mode section, `.env` and `.gitignore` guidance, and `CONTRIBUTING.md`.

### Decided

- The items under Workflow: `.env`, `SqlSourceOnline` with `auto`, `true` and `false`, and `--watch`.
- The target runs before `GenerateMSBuildEditorConfigFileCore`, after the project manifest target and before `SqlSourceTrackAdditionalFiles`, which depends on it so that the hash of the file list includes a sidecar written in this build.  It is skipped in a design-time build, and has MSBuild `Inputs` of the project's `.sql` files and the project manifest, written with `WriteOnlyWhenDifferent`, and `Outputs` of one stamp file under `obj`, so that a build where nothing changed skips it without launching the tool, in any IDE, and a project whose files all resolve to `Sql`, which has no sidecar by design, does not run it every build.  A schema change does not touch the inputs; that is the known gap `--force` and `--check` cover.
- A sidecar the tool creates during a build is not in that build's `AdditionalFiles`, since the props' glob expanded at evaluation; after the tool runs, the target globs `**/*.sql.json` again and adds what `@(AdditionalFiles)` lacks, and it runs before the compiler's config file is written, so the same build compiles with it.  The tool takes two options for the build's use: `--manifest <path>`, the project manifest the build already wrote, so no second MSBuild process is spawned, and `--online auto`, under which no connection for any database is one line and exit zero rather than an error.  A multi-targeting project runs the target once per framework; the second run skips everything, which the README says so nobody reports it.
- An unreachable database is a build error, not a silent fall back to offline, since a passing build would hide a stale sidecar.  The message says to start the database, remove the connection or set `SqlSourceOnline=false`.
- A failed description is a build error in the compiler's format, which the tool already produces.  The generator then also reports every query of that file that needed a new entry, since one failing query holds back its whole file: one cause, one error from the tool and one from the generator per held-back query, accepted, since the tool's names the server's reason and the generator's the consequence, and suppressing the generator's would need it to know the tool ran.  The README says that one failing query holds back its file.
- The unit is the project being built, through its own manifest, never the solution.  Connections come from the three sources as in a manual run.
- The tool is found through the local tool manifest, `dotnet sqlsource`; no manifest is an error that says to install it.  Bundling the tool in the generator package stays rejected: the package is a development dependency with one DLL.
- `--check` stays a CI step outside the build.  A build mode that fails on drift would duplicate it and slow every build.
- `SqlSourceLog` is the property the target passes through as `--log`.

### Technical notes

- `Exists` in the target's condition can test the project directory and `$(SolutionDir)`, which is `*Undefined*` outside a solution build and is tested only when defined; the tool's own search walks further, to the repository root, so the two can disagree for a `.env` placed between the two.  The README says where to put the file: the solution directory.
- The target runs the tool with an `Exec` task that does not read the tool's output by MSBuild's own rule for an error: that rule allows white space before one, so a continuation line such as `    server: ERROR: relation "users" does not exist` would be listed as a second error.  The task sets `IgnoreStandardErrorWarningFormat` and gives an expression of its own, which matches only a line that starts in the first column, as every first line of the tool's errors does and no continuation line does.

## Phase 10 - bug reports

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
- Databases other than PostgreSQL and SQL Server.  The description's optional fields and the `-- param:` type marker are the room left for them.  CockroachDB is the PostgreSQL describer with the walk off, and the first candidate; SQLite would need a static describer; MySQL, MariaDB and Oracle need declared parameter types.
- Using a token's default as the C# default value of the generated method's parameter.  A default is a sample for describing; whether it should also be a run-time default is a separate question.
- Synchronous execution methods, streaming results as `IAsyncEnumerable<T>`, and bulk operations.  The method shapes in this epic are one row, a list and a count.
- Per-column C# type overrides.  Table-valued parameters.
- A schema fingerprint in the snapshot.
- Running migrations or starting a database from the tool.  The tool describes against a database the user has; how the schema got there is not its business.

## Research

The epic was preceded by research into both approaches, which this outline condenses.  The facts that phase specs will lean on, with their sources:

- **Static analysis, prior art.**  sqlc parses with hand-written parsers it wrote to escape native dependencies, builds its catalog from migration DDL and a 245 KB file of PostgreSQL functions generated from a live server, types unknown functions as `any`, and added a database-backed analyser in v1.19 and managed databases in v1.22.  SQLDelight's grammars are small because SQLite's type system is small.  Rezoom.SQL types SQL statically by owning its own dialect.  ScriptDom is MIT, `netstandard2.0`, 7 MB, and does no binding.  No managed PostgreSQL parser exists; the .NET bindings of libpg_query bundle a native library.  Nothing in .NET does schema-driven static inference of result types from SQL text.
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
