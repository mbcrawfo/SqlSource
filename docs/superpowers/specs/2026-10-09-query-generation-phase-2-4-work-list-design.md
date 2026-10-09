# Query generation, phase 2.4: the work list - design

Date: 2026-10-09

Sub-phase 2.4 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It delivers the plan of a run: from the manifests of sub-phase 2.3, which queries exist, which of them need an entry in a sidecar, which database each belongs to, and which of them this run was asked to describe.  Nothing is described yet.

It builds on sub-phase 2.3.  Sub-phase 2.5 builds on it.

## Goal

```csharp
[SqlSourceGenerate(Path = "Queries", Output = GeneratorOutput.Models)]
internal static partial class Queries;
```

```sql
-- Queries/Users.sql, in a project whose SqlSourceDialect is postgres
-- name: GetUser
SELECT id, name FROM users WHERE id = @id;

-- name: CountUsers
-- output: sql
SELECT count(*) FROM users;

-- name: GetInvoice
-- database: billing
SELECT id FROM invoices WHERE id = @id;
```

The plan of this project holds one file, `Queries/Users.sql`, with three queries: `GetUser` needs an entry, belongs to `postgres` and has a hash; `CountUsers` needs none; `GetInvoice` needs one and belongs to `billing`.  Under `--database billing` only `GetInvoice` is selected.

Success is:

- The plan agrees with the generator on which type claims which file with which output.  A test runs both over the same sources and compares.
- A file that no type claims is not read, and a query whose output is `Sql` for every type that claims its file needs no entry.
- The hash of a planned query is the hash phase 5's generator will compute for it.
- The tool reuses the generator's parser, path resolver, setting readers and hash, and copies none of them.

## Decisions

From the epic, unchanged: the tool reads the attributes with a syntax-level reader of its own, since the generator's works on symbols; the path resolver, the parser, the settings resolution and the hash are shared; the output resolves for each type and query, and the needs of a file are the union over the types that claim it; the database of a query is its marker's, else the file's, else the metadata's, else the property's, else the name of its dialect; a file with parse errors is skipped and its errors reported; a token without a default and an output that needs a describable dialect are errors in the tool as in the generator; two queries of one database with two dialects are an error; `--database` and a `.sql` file path restrict a run.

Changed in the epic by the owner, and recorded in the outline:

| Decision | Choice | Why |
|----|----|----|
| What the attribute reader reads | `Path` and `Output` | Nothing else on the attribute changes what is described.  Reading the other nine properties would report non-literals that the tool has no use for. |
| A `.sql` filter on the command line | Positional: `sqlsource describe Queries/Users.sql` | It is what a user types.  A path that ends in `.sql` is a filter, and any other is the unit. |

Settled in this spec:

| Decision | Choice | Why |
|----|----|----|
| Database names | Compared ignoring case; the first spelling in the order of the plan is the one shown and written | `billing` and `Billing` read their connection from one variable, so they cannot be two databases. |
| Where `SQLSRC209` is reported | At the name of the file's first query that needs an entry | The epic recommends the `-- output:` marker that set the output, but the parser keeps no position for a marker.  Phase 5 decides whether the generator gains one. |
| Where `SQLSRC210` is reported | At the query's name, once for each token | The parser keeps no position for a token either. |
| Which invalid settings the tool reports | The ones it reads: the dialect, the output and the database | The build reports the rest, and the tool is not the build. |
| A type the generator would reject | Planned as any other | A type that is not partial is the build's error.  The tool describes a query the generator will not use, which costs one description. |
| What filters do to the plan | They select queries.  The plan keeps every query of every claimed file | Sub-phase 2.5 writes a file only when every query that needs an entry has one, so it must know the queries that were not selected. |

## Out of scope

- Describing, connections, sidecars and the summary.  Sub-phase 2.5.
- `--check`, `--verbose` and `--log`.  Sub-phase 2.6.
- The diagnostics of a type: not partial, file-local, a `Path` that matches nothing, a case collision of two paths.  The build reports them.
- A `.sql` file that two projects list.  It is planned, and `docs/tech-debt` records what goes wrong.

## The command line

```
sqlsource describe [<path>...] [--project <path>]... [--database <name>]...
```

- Each `<path>` whose extension is `.sql`, ignoring case, is a file filter.  It is resolved against the working directory, put through `SqlPath.Normalize`, and compared with the plan's files by `SqlPath.Comparer`.
- A token that starts with `-` is never a `<path>`: sub-phase 2.2's rule for an unknown option holds for every position.  At most one other `<path>` may be given, and it is the unit, by the rules of sub-phase 2.2; a second is a wrong command line: a message on standard error and exit `1`.
- With file filters and no unit, the unit is found from the working directory, not from the files.
- `--database` takes a database name, by the rule of the `-- database:` marker, and may be given several times.  A value that is not a name is a wrong command line: a message on standard error and exit `1`.

## The attribute reader

`AttributeReader.Read(ProjectManifest)` gives the project's claims and the errors of reading them.  A `TypeClaim` is the path of the C# file, the `Path` as written or null, the `Output` or null, and the position of the attribute.

1. **Find the files.**  A `Compile` file is read only when its text holds `SqlSourceGenerate`, compared ordinally.  A file that cannot be read is skipped: the build reports it.
2. **Parse.**  With `CSharpParseOptions` built from the manifest: the language version that `LanguageVersionFacts.TryParse` reads from `LangVersion`, and the latest when it is empty or not read; the constants of `DefineConstants` as preprocessor symbols; no documentation comments.
3. **Find the attributes.**  An `AttributeSyntax` whose name ends in the identifier `SqlSourceGenerate` or `SqlSourceGenerateAttribute`, however it is qualified, in an attribute list with no target or the target `type`, on a class, a struct or a record: the declarations `TargetTypeReader.IsCandidate` takes.  The first such attribute of a declaration is its claim.
4. **Read the two arguments**, each a named argument with `=`.

| Argument | Read | Anything else |
|----|----|----|
| `Path` | A string literal: regular, verbatim or raw.  `null`, `default` and the empty string are not set.  A string of white space is a path, as in the generator, where it matches nothing | `SQLSRC208` at the expression: a constant, `nameof`, a concatenation, an interpolated string |
| `Output` | A member access whose name is `Sql`, `Models` or `CodeGen` and whose left side ends in the identifier `GeneratorOutput` | `SQLSRC208` at the expression: a cast of a number, a constant.  A member access with another name is not set; the compiler rejects it |

A claim whose argument is `SQLSRC208` is not planned: its files may be described for another type, and are not for this one.

What this reader cannot see is in `docs/tech-debt`: an alias for the attribute, a type of the same name in another namespace, and an attribute inside `#if` under a configuration other than the default.

## The plan

`RunPlanner.Plan(manifests, filters)` gives a `RunPlan` and the errors of planning.  For each project, in order:

1. **The list of `.sql` files** is the manifest's `Files`, normalised, sorted and made distinct ignoring case as the generator's is.  The private helper `ToSortedSet` of `SqlSourceGenerator` moves to `SqlPath`, and both call it.
2. **The claims** come from the attribute reader.
3. **A claim's files** come from `PathResolver.FindFiles(sourceFile, path, sqlPaths)`: the private method of that name, which today takes a `TargetType`, takes the two values it reads from one, and `PathResolver.Resolve` calls it.  `PathResolverAllocationTests` keeps its budget.
4. **The settings.**  The properties and each file's metadata are put into a dictionary under the keys the compiler would give them, `build_property.<Name>` and `build_metadata.SqlSourceSettingsFile.<Name>`, behind a small `AnalyzerConfigOptions`.  `MSBuildSettings.Read` and `DialectSetting.ReadProperty` and `ReadMetadata` then read them as they do in the generator.  `SqlSourceDatabase` is read by the tool, with `SettingValue.IsDatabaseName`.
5. **Each claimed file is parsed once**, with the text read as the compiler reads it, `SourceText.From` over the file's bytes, and through `SqlFileReader.Read`.  That method takes an `AdditionalText` today; the plan either hands it a small one over the file or gives it an overload that takes the path and the text, whichever changes the generator less.  Comments are never wanted: the hash comes from the stripped SQL.
6. **Each query** gets its values:

| Value | Rule |
|----|----|
| Needs an entry | For any claim of its file, `QuerySettings.Resolve(query.Markers, claim, metadata, property).Output` is `Models` or `CodeGen`.  The claim's level holds its `Output` and nothing else |
| Database | `query.Markers.Database`, else the file's `SqlSourceDatabase` metadata, else the property, else `SqlDialectName.Canonical` of the file's dialect.  Only for a query that needs an entry: a query that needs none belongs to no database |
| Hash | `SqlQueryHash.Compute` over the file's dialect and the query's segments, tokens and parameters.  Only for a query that needs an entry |
| Selected | The query's project is in the run, and its file is one of the file filters or there are none, and its database is one of `--database` or there are none |

A `.sql` file that two projects list is planned once, under the first project in order, and a query of it needs an entry when any claim of any project says so.

### The plan's records

| Type | Holds |
|----|----|
| `RunPlan` | The files, in order of project and then of path; the databases, which are those of the queries that need an entry, each with its name as first spelled and its dialect |
| `PlannedFile` | The `.sql` file's path; the project; the dialect; a state, `Ready`, `HasParseErrors` or `NotDescribable`; its queries in the file's order |
| `PlannedQuery` | The parsed `SqlQuery`; whether it needs an entry; its database; its hash; whether it is selected; a problem, none, `TokenWithoutDefault` or `DatabaseDialectConflict` |

A file that is `Ready` and has no query that needs an entry stays in the plan: sub-phase 2.5 deletes a sidecar it finds beside it.  The plan holds no path of a sidecar; sub-phase 2.5 works it out, so that this sub-phase needs nothing of sub-phase 2.1.

### What is wrong with a plan

| Condition | Report | Effect |
|----|----|----|
| A claimed file has parse errors | Each, with the id and position the generator gives it | The file is `HasParseErrors`; its queries are in no count |
| The dialect of a property or of a claimed file's metadata is not valid | `SQLSRC011`, at the project file, once for each value | As in the generator: the file is read as `ansi` |
| `SqlSourceOutput` or `SqlSourceDatabase` is not valid, as a property or as the metadata of a claimed file | `SQLSRC014`, at the project file, once for each setting and value | The value is not set |
| A file has a query that needs an entry, and its dialect is not `postgres` or `mssql` | `SQLSRC209`, once for the file | The file is `NotDescribable` |
| A query that needs an entry has a token with no default | `SQLSRC210`, once for each such token | The query's problem is `TokenWithoutDefault` |
| A database has two dialects, among the queries that need an entry | `SQLSRC211`, at the first such query of each file whose dialect is not the database's first | Every query of that database in that file that needs an entry has the problem `DatabaseDialectConflict` |
| A file filter names no claimed file of a project in the run | `SQLSRC212` | The run goes on |

The dialects that can be described are a list of two in the generator assembly, `SqlDescribableDialects` in `Parsing/`, beside `SqlDialectName`.  Phase 5 reads the same list.

Each error of the plan makes the exit code `1`.  A query with a problem, and every query of a file that is not `Ready`, cannot be described; sub-phase 2.5 counts the selected ones that need an entry as failed.

### Diagnostics

In `ToolDiagnostics`, with the four places each needs.  `SQLSRC209` and `SQLSRC210` are also the generator's, from phase 5.

| Id | Title | Message | Arguments |
|----|----|----|----|
| `SQLSRC208` | Attribute argument is not a literal | `'{0}' of [SqlSourceGenerate] is read from the source by 'sqlsource', which needs a literal here` | `Path` or `Output` |
| `SQLSRC209` | Output needs a dialect that can be described | `The output '{0}' needs a dialect that can be described, and the dialect of this file is '{1}'.  Set the dialect to 'postgres' or 'mssql', or the output to 'sql'.` | The output; the dialect's name |
| `SQLSRC210` | Token has no default | `The token '{0}' has no default.  A query whose output is '{1}' is described with a sample in its place.` | The token; the output |
| `SQLSRC211` | Database has two dialects | `The database '{0}' has the dialect '{1}' here and '{2}' in '{3}'` | The database; this file's dialect; the first dialect; the file that gave it |
| `SQLSRC212` | File is not in the run | `'{0}' is not a .sql file that a type of the run claims` | The path given |

`SQLSRC210` has a `help:` line that shows the token with a default, `{{name:default}}`, and names the `-- token:` marker.

An error that comes from the generator's code arrives as a `DiagnosticInfo`.  `ToolDiagnostic.From(DiagnosticInfo)` turns one into the tool's form: the descriptor, the path, the start of the line span, and the arguments.

### `describe` after this sub-phase

It finds the unit and the projects, builds the plan, reports what is wrong with it, and exits `0` or `1`.  It prints nothing for a plan with no error.

## Order of work

One pull request, one commit for each step.  Each leaves `./pre-commit-validation.sh` passing and brings its tests and the documents it makes wrong.

1. The epic outline: the row of 2.4 says In progress.
2. In the generator: `SqlPath.ToSortedSet`, `PathResolver.FindFiles`, `SqlDescribableDialects`, and the way into `SqlFileReader.Read` without the compiler's file.  No behaviour of the generator changes.  `PathResolver.Resolve` is a hot path, so the pull request states its time and allocation before and after, as `src/SqlSource/AGENTS.md` requires.
3. The attribute reader, with `SQLSRC208`, and the tech-debt item.
4. The test helper that builds a project in a temporary folder and answers MSBuild's two runs, below.
5. The plan for one project: claims, files, settings, parsing, and the values of a query but for `Selected`.  Parse errors, `SQLSRC011` and `SQLSRC014`.
6. `SQLSRC209` and `SQLSRC210`.
7. Several projects, the databases of a run, and `SQLSRC211`.
8. The filters: `--database`, the `.sql` paths, `Selected`, and `SQLSRC212`.
9. The parity test.
10. The documents, and the outline's row set to Done.

## Testing

A helper of the test project, `TestProject`, writes C# and `.sql` files to a temporary folder and gives an `IProcessRunner` that answers the two runs of sub-phase 2.3 with a manifest built from those files and from properties and metadata the test names.  With it a test runs `Cli.RunAsync` from the command line to the plan with no MSBuild.

| Where | Cases |
|----|----|
| `AttributeReaderTests` | The attribute by its short and long name, qualified and with `global::`; on a class, a struct, a record and a nested type; with `[type: ...]`; in a list with another attribute.  Not on a method or an assembly.  `Path` as each kind of literal, `null`, empty and white space.  `Output` as each member, qualified and not.  `SQLSRC208` for each form the table names, with its position.  A file that does not hold the word is not parsed.  An attribute inside `#if` for a constant of the manifest, and for one that is not; inside `#if NET8_0_OR_GREATER`, with the constants a manifest of sub-phase 2.3 holds.  C# of the newest version in the file |
| `RunPlannerTests` | The example of this spec.  No `Path`, a folder, a file.  Two types that claim one file with two outputs: the union.  A file no type claims is not read, though it does not parse.  The output from each of its five levels, and the database from each of its four and from the dialect.  Database names that differ in case are one.  The hash equals `SqlQueryHash.Compute` called by the test.  A file read with a byte order mark hashes as without.  A file with `\r\n` |
| `RunPlannerErrorTests` | Each row of the table of what is wrong, with the id, the position and the effect.  A file of another dialect whose queries are all `Sql` gives its database no second dialect.  `SQLSRC209` once for a file with three such queries, and not for a file whose queries are all `Sql`.  `SQLSRC210` for two tokens of one query.  `SQLSRC211` across two files and across two projects |
| `FilterTests` | Each filter alone and together; a filter that selects nothing; a `.sql` path with a unit and without; two units; `SQLSRC212`; a `--database` value that is not a name |
| `GeneratorParityTests` | The test project links `tests/SqlSource.Tests/Generator/GeneratorHarness.cs` and the helpers it uses, as `tests/SqlSource.Tests.RoslynFloor` does.  For each of a set of sources and file lists, the generator runs with step tracking, and the `TypeFiles` it gives are compared with the tool's claims and their files: the same types by file, the same `Path`, the same `Output`, the same files in the same order |
| `tests/SqlSource.Tests` | `SqlPath.ToSortedSet` and `PathResolver.FindFiles` are covered by the tests that covered them as private methods; `PathResolverAllocationTests` passes with its budget as it is; `SqlDescribableDialects` has a test of its two members |

## Documentation

- `src/SqlSource/AGENTS.md`: `SqlDescribableDialects`; that `PathResolver.FindFiles`, `SqlPath.ToSortedSet` and `MSBuildSettings.Read` are also the tool's, so a change to one changes what the tool describes.
- `src/SqlSource.Tool/AGENTS.md`: the attribute reader is syntax only and reads two arguments; the plan must agree with the generator, and `GeneratorParityTests` is what holds it to that; a rule that both need goes into the generator assembly.
- `docs/diagnostics.md`: `SQLSRC208` to `SQLSRC212`.  The section of `SQLSRC014` says today that the generator does not check `SqlSourceDatabase`; it gains that the tool does.  The part on the tool says that it also reports `SQLSRC011`, `SQLSRC014` and the errors of a `.sql` file, under the generator's ids.
- `docs/tech-debt`: two items, each with the next free id.  The attribute reader is syntax only: an alias, a type that shadows the attribute's name, and an attribute under `#if` in another configuration are misread.  And a `.sql` file that two projects list has one sidecar and no owner: in one run it is planned under the first project, so a second that gives it another dialect gets a sidecar its build calls stale; and a run that does not hold both projects, a project as the unit or `--project`, sees the needs of one alone and may write a sidecar without the entries the other needs.
- `README.md`, `CONTRIBUTING.md` and `docs/publishing.md`: nothing in them changes.
- The epic outline: in steps 1 and 10.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.  When this spec was written the repository had no `v*` tag.
