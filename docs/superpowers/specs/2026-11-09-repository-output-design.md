# Repository output design

## Intent

Today a described query gets, by default, an `internal` extension method of `DbConnection` (the output `codegen`).  A user who wants a repository still writes one by hand: a class that holds a connection and forwards each call, as the README's getting-started shows.  This change makes that class the generated default: `[SqlSourceGenerate]` on a partial class with a constructor gives it an instance method for each query, which finds its own connection, so a caller writes `await users.GetUser(...)` and gets data, with no wire-up.  It also gives the class an interface by default, so code that uses the repository can mock it.

SqlSource is unreleased, so breaking the current default is accepted.

## Goals

- Rename the output `codegen` to `methods`, unchanged in behaviour.
- Add the output `repository`, and make it the default.
- A repository query becomes an instance method of the type, which gets a connection from a member of the instance, runs the query and disposes what it created.
- The connection source is found by convention in the common case, and marked with a new attribute otherwise, with a database name for a type that uses several databases.
- Each repository type gets a generated `partial interface` of its public repository methods, on by default, off for a project by an MSBuild property and per type by the attribute.

## Non-goals

- **Transactions.**  A repository method takes no transaction.  A query that must run in a transaction has the output `methods`, and the user's own code on the repository manages the transaction and calls the generated `methods` method.  Mixing outputs in one type and one file already works.
- **Command timeout.**  A repository method takes no `commandTimeout`.  A query that needs one uses `methods`.
- **Hand-written members in the interface.**  The interface holds only the methods SqlSource generates; a user adds others by declaring another part of the partial interface.
- **Detecting a `DbConnection` member by convention.**  Its lifetime cannot be guessed; it is used only when marked.
- Synchronous methods.

## Anti-goals

- A connection source chosen by a guess when more than one could apply.  Ambiguity is a build error, never a pick.
- A silent fallback from `repository` to `methods` when no source is found.  That is a build error naming what to add.

## Constraints

- Projects target .NET 8+, C# 12+: `DbDataSource` is in the BCL, primary constructors exist.  A primary-constructor parameter of the type is usable from the generator's own partial part (checked: it compiles).
- Microsoft.Data.SqlClient has no `DbDataSource` (none in 7.1), so a SQL Server repository uses a connection string, a factory method or a `DbConnection`.
- Npgsql's `NpgsqlDataSource` derives from `DbDataSource`; match the BCL base type, not Npgsql's.
- The repository rules of `AGENTS.md` apply: user docs, manual, diagnostics page, examples, architecture pages, the settings schema and `AnalyzerReleases.Unshipped.md` change in the same PR.

## Approaches

**Connection source.**  Three were considered:

1. **A marker attribute on a member, plus convention detection.**  Chosen.  The common case needs no code; anything else is one marked member, such as a property over an `IConnectionStringProvider`; SqlSource reports its own errors, naming the database that lacks a source.  Costs more rules to document.
2. **A partial hook method the generator declares and the user implements**, e.g. `private partial ValueTask<DbConnection> OpenConnectionAsync(string database, CancellationToken ct)`.  Uniform and compiler-enforced, but boilerplate on every repository, a `switch` over a string for several databases, and the error for a missing body is the compiler's, not SqlSource's.
3. **The type's attribute names the member**, `[SqlSourceGenerate(Connection = nameof(_dataSource))]`.  One place, but a member named by string, and several databases need a map in an attribute argument.

**Interface.**  With the interface on by default, one way fits: the generator declares `partial interface I{Type}` and adds it to the type's base list in its own part.  A key of `sqlsource.json` was rejected: settings files apply to the folders of `.sql` files, and one type can claim files of several folders that could disagree.  The project-wide switch follows the precedent of `SqlSourcePublicExceptions`, an MSBuild property.

**Output vs member location.**  The user chose a new output.  `repository` sits after `methods` in the progression `sql`, `models`, `methods`, `repository`.

## Design

### Outputs

| Output | A query gets |
|----|----|
| `sql` | Its SQL (unchanged) |
| `models` | SQL and models (unchanged) |
| `methods` | Today's `codegen`, unchanged: models and a static or extension method that takes the connection |
| `repository` (default) | Models and an instance method of the type that gets its own connection |

`codegen` is removed, not kept as an alias: it becomes an unknown value, reported as any unknown output is.  Everything that needs a sidecar entry for `codegen` today (the generator, the tool's planning in `src/SqlSource.Tool/Planning/PlannedQuery.cs`) needs one for `repository` too; every check a `codegen` method passes (`SQLSRC209`, `SQLSRC272` to `SQLSRC282`, `SqlBlock.CodeGenErrors`, driver probes) applies to a `repository` method as well.

### The repository method

```csharp
// Generated, for a public type App.UserRepository
public async Task<GetUserDto?> GetUser(GetUserParams parameters, CancellationToken cancellationToken = default)
```

- Arguments: the input model when the query has parameters, a `string` for each token, then `cancellationToken`.  No connection, transaction or command timeout.  The user chose the input model over spelling out each parameter as an argument: it matches `methods`, keeps a signature stable when a query gains a parameter, and cannot clash with a token or `cancellationToken`.
- Return types by result shape and collection type as for `methods`, `IAsyncEnumerable<T>` included.
- Name: the query's name, with `Async` under `async-method-suffix`.  It must not collide with a member of the type, the type, its type parameters, a constant or token method or nested class of the type's queries (the `class`-location rules of `SQLSRC274`).  Tokens keep today's reserved names (`SQLSRC275`).
- Behaviour: get a connection from the source (below), open it if the source gave a closed one, run the query exactly as the `methods` method does, and dispose the connection if the method created it.  A streaming method disposes when the loop ends.

### Connection source

**The attribute.**  `SqlSourceConnectionAttribute`, generated beside the existing attributes, with targets field, property, method and parameter, and an optional `Database` string property naming a database as the `database` setting does.

**Accepted members and their lifetimes:**

| Member | Gives | Method's connection |
|----|----|----|
| `string` field, property or primary-constructor parameter, or a method returning `string` | A connection string | Creates the dialect's driver connection (`NpgsqlConnection`, `Microsoft.Data.SqlClient.SqlConnection`), opens it, disposes it |
| `DbDataSource` (or derived) field, property or parameter | A data source | `OpenConnectionAsync(cancellationToken)`, disposes it |
| Method returning `DbConnection`, `Task<T>` or `ValueTask<T>` with `T` deriving from `DbConnection`, taking nothing or one `CancellationToken` | A factory | Calls it each time, opens if closed, disposes it |
| `DbConnection` (or derived) field, property or parameter | A borrowed connection | Opens it if closed and closes it again, as `methods` does today; never disposes it |

The rule a user learns: a method is a factory, so what it returns is owned; a field, property or parameter is held state, so a connection there is borrowed.

Members of the type, members inherited from base types that the type can access, and the type's primary-constructor parameters are candidates.  Instance and static members both count.

**Members before parameters** (designer's call, from the builder check).  A primary-constructor parameter is a convention candidate only when no field, property or method of the type or its bases is.  So `partial class UserRepository(NpgsqlDataSource dataSource) { private readonly NpgsqlDataSource _dataSource = dataSource; }` uses `_dataSource`, a record's positional parameter yields to its generated property, and `Repo(string connectionString) : RepoBase(connectionString)` uses the base's accessible member.  The generator never captures a parameter the user's code also stores or passes to a base constructor unless the user marks that parameter, in which case the compiler's own warning (CS9124, CS9107) is theirs to see.

**Which databases a type needs.**  The resolved database (`QuerySettings.ResolveDatabase`) of each query the type claims whose output is `repository`.  Queries of other outputs need no source.

**Resolution, per type:**

1. If any member is marked: each needed database must have exactly one marked member, by its `Database`.  A marked member with no `Database` stands for the type's only needed database, and is an error when the type needs more than one.  Conventions are off.
2. If none is marked and the type needs exactly one database: candidates by convention are a `string` named `connectionString`, `_connectionString` or `ConnectionString`, and any member of a type deriving from `DbDataSource`.  Exactly one candidate is the source; more than one is an error listing them.
3. If none is marked and the type needs more than one database: an error asking for a marked member with `Database` for each.

**Known limitation: a PostgreSQL connection string.**  A `string` source makes a bare `NpgsqlConnection`, on which no plugin (NodaTime, NetTopologySuite) and none of the user's own Npgsql mappings are registered, so a query that reads such a type compiles and throws `InvalidCastException` at run time.  The user chose to document this, not to diagnose or work around it: Npgsql itself recommends a `DbDataSource`, and users on that path are not affected.  It goes in the "Known issues" of `docs/manual/generator.md` and in the PostgreSQL page, pointing to a `DbDataSource` source.  It is a limit of the driver, not tech debt.

### Diagnostics (new; ids are the builder's, type-level ones below 100)

- A type with repository queries has no connection source for a database (says what to add, and that `output: methods` is the alternative).
- More than one source for one database (convention or marked), listing the members.
- A marked member without `Database` on a type that needs several databases.
- A marked member whose `Database` the type's repository queries do not use (warning).
- A marked member of a type that is not accepted, or a method with parameters other than one `CancellationToken`.
- A marked member whose declared type is a driver's own type of the other engine (e.g. `NpgsqlDataSource` for an `mssql` database).
- Repository queries on a `static` type (suggests `output: methods`).
- The generated interface's name is taken by a type that is not a partial interface.

Each one leaves the type's repository methods out, as a `methods` method's errors leave its method out; models stay.

### Placement settings under `repository`

| Setting | Under `repository` |
|----|----|
| `visibility` | Each instance method's accessibility.  Default `public`, whatever the type's (a public member of an internal type is effectively internal, and only public methods fill the interface).  `protected` supports an abstract base repository |
| `member-location` | Repository methods always go on the type.  `class` fits; `extension-class` or `nested-class` reaching a repository query is `SQLSRC122` |
| `nested-class` | Not used by repository methods; still places the constants of `sql` and `models` queries |

`async-method-suffix`, `collection-type` and streaming, `array-pool`, `list-padding`, `token-validation` and every model setting apply as they do to `methods`.

### Interface

- The generator writes `partial interface I{TypeName}`, in the type's namespace and containing types, with the type's type parameters and declared accessibility, and adds it to the base list of its own part of the type.
- Members: the type's generated repository methods whose visibility is `public`, with the same signatures and documentation.
- Generated when the type claims at least one query whose output is `repository`, even when every such method has an error, so code that names the interface does not cascade errors.
- A user adds members by declaring another `partial interface I{TypeName}`.
- **Project switch:** MSBuild property `SqlSourceRepositoryInterfaces`, default on; `false` (ignoring case) turns it off, passed to the generator as `SqlSourcePublicExceptions` is.
- **Per type:** properties of `[SqlSourceGenerate]`: `GenerateInterface` (`bool`, overrides the project switch when written) and `InterfaceName` (`string`, replaces `I{TypeName}`).

### Documentation and examples

- `README.md` getting-started: step 4 shows a repository with a primary-constructor `NpgsqlDataSource` and a call on it; the FAQ and "Only want models" sections name `methods` where they say `codegen`; a section or FAQ on mocking via the interface and on transactions via `methods`.
- `docs/manual/generator.md`: a "Repository methods" section (signature, connection sources and lifetimes, interface); "Query methods" renamed to the `methods` output.
- `docs/manual/settings.md`: output table and default, member-location and visibility defaults, the new MSBuild property.
- `docs/manual/recipes.md`: transactions with `methods` inside a repository; a connection from a provider interface; a SQL Server repository from a connection string.
- `docs/manual/diagnostics.md`, `AnalyzerReleases.Unshipped.md`, `schemas/settings-v1.schema.json`.
- `docs/architecture/generator.md` and `docs/architecture/tool.md` where they describe `codegen`.
- Both example projects: the current `CodeGen` folders become `Methods`, and each gets a `Repository` example, sidecars written by `tools/describe-end-to-end.sh`.

## Left to the builder

- Diagnostic ids and wording.
- Whether the repository method's body reuses the `methods` emission (acquire a connection, call the shared body) or is emitted on its own, so long as behaviour matches.
- Internal renames of `CodeGen` identifiers (`CodeGenEngines`, `CodeGenErrors`, `OutputKind.CodeGen`), which now cover two outputs.
- The exact runtime exception when a source member is null at call time.
- Whether `SqlSourceConnectionAttribute` is conditional; it must stay readable on a base type from a referenced project, as `SqlSourceConverterAttribute` is.
- XML documentation of interface members (`inheritdoc` or copied).
