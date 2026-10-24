# Query generation, phase 8.1: the methods - design

Date: 2026-10-24

Sub-phase 8.1 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It generates, for each query whose output is `CodeGen`, an asynchronous ADO.NET method that binds the query's parameters, runs it and reads its rows into the output model by ordinal, in the shape and collection type the query asks for.  The method takes the place of the query's constant or token method.  It binds each parameter by its value alone; the typed binding of each engine is [8.3](2026-10-24-query-generation-phase-8-3-postgresql-binding-design.md)'s and [8.4](2026-10-24-query-generation-phase-8-4-sql-server-binding-design.md)'s, and conversion is [8.2](2026-10-24-query-generation-phase-8-2-converters-design.md)'s.

It builds on 8.0.  Sub-phase 8.2 builds on it.

## Intent

The third goal of the epic, as a generator feature: a project that writes a query gets a method that runs it, with no Dapper and no hand-written ADO.NET.  The owner's measure for phase 8 is that every query runs through its method, every type is written and read through generated code, and the methods cost no more than hand-written ADO.NET.  This sub-phase writes the code that measure is taken on; 8.3 and 8.4 take it against the servers.

The decisions of the epic's phase 8 section are the authority, and this spec adds detail without contradicting them.  The owner's decisions of 2026-10-24 that this sub-phase carries: a `CodeGen` query's method replaces its constant; the methods take a `DbConnection` only, opened when closed and closed again only when the method opened it; and a type override with no converter is trusted to the driver.

## Goals

1. For each query that needs an entry, has a usable one, has the models it needs, and resolves its output to `CodeGen` for a type that claims its file: a method on that type's placement, by the epic's signature, returning the shape's type.
2. No constant and no token method for such a query, under either `SqlLocation`; a type that has no constant and no token method left gets no nested class.
3. The connection rule, the command, the transaction, the timeout and the cancellation token as decided.
4. The five shapes, their defaults, and the ten collection types, every collection materialised.
5. `async-method-suffix`, `MethodLocation` and `SqlSourceCollectionType` flip from "accepted; no effect yet" to their effect, in the generator and in the manual; so does the `-> shape` suffix.
6. `UnexpectedRowCountException`, emitted with the attribute.
7. Each column read by ordinal with `GetFieldValue<T>` into a record by its constructor or a class by an initializer, `IsDBNull` only where the model's property is nullable.
8. Each parameter bound by value, in a parameter the command creates, through a binding step that 8.3 and 8.4 extend per engine without changing the method's shape.
9. The driver of the dialect required under `CodeGen`, and the drivers' floor diagnostics stopping the methods; models are emitted in both cases.
10. The diagnostics of the methods' names and shapes, so that no conflict the generator can know of is a compiler error in generated code.

## Non-goals

- Converters.  Sub-phase 8.2.  A type override with no converter is read with `GetFieldValue<T>` and bound as a `T`, which is what this sub-phase does for every override.
- Setting `NpgsqlParameter.DataTypeName`, `SqlDbType`, size, precision or scale.  Sub-phases 8.3 and 8.4.  This sub-phase binds by value and sets nothing else on the parameter.
- Running the methods against a server.  Sub-phases 8.3 and 8.4; this sub-phase's proof is the generator tests, which compile what it emits against each supported driver's metadata.
- `IAsyncEnumerable<T>`, synchronous methods, bulk operations, an overload on `DbDataSource`, and a method for a query of several result sets (`D-0006`) or a procedure (`D-0002`).  Out of the epic, or deferred.
- A token's default as the C# default of its argument.  Out of the epic.

## Anti-goals

- **A warning or an error in generated code.**  Every conflict of names the generator can know of is its own diagnostic at the query, as sub-phase 5.3 made it for the models.
- **A method that reads a `NULL` into a property the model calls not null without failing.**  A column the model calls not null is read with no `IsDBNull`, so the driver's throw on a `NULL` is the oracle the epic counts on; the method never defaults it.
- **A lazy collection.**  Every collection is complete when the task completes, and the command, the reader and the connection are done with.
- **A method of one query rewriting another's.**  An edit to one query re-emits the methods of the types that claim its file, and nothing else, which the caching tests prove; an edit that makes or removes a clash between two types' methods re-emits both, as Diagnostics says.
- **Reflection or a per-call allocation a hand-written method would not make.**

## What exists today

As of the merge of 8.0, which this spec assumes; the findings of 8.0's spike amend this spec where they differ.

- Phase 1 parses every setting of the methods: `ResultShape` from the name marker's `-> shape`, carried on `SqlBlock.Shape` and `SqlQuery.Shape` and null when the marker has none; `CollectionKind` at every level, resolved per query into `QuerySettings.CollectionType` with the default `Array`; `MethodPlacement` from the attribute alone, on `TargetType.MethodPlacement`, default `ExtensionClass`; `GeneratorParameters.AsyncMethodSuffix` in `QuerySettings.Parameters`, with no accessor.  Nothing consumes them.
- `OutputKind`'s default is `CodeGen`, and `CodeGen` behaves as `Models` at two places: `TypeModels` and `SidecarNeeds`.
- A type's file is `TypeQueries` with the project level, emitted by `TypeEmitter`, which sees no sidecar, type or model.  Under `Nested` it writes `private static class <Name>`; under `Direct`, `public const string` or the token method that `MethodWriter` writes.  `TargetTypeReader.GetTakenNames` collects the type's member names under `Nested` alone.
- `QueryTypes` gives each parameter's and column's `PropertyType`: its C# text, whether it is a value type, its accessibility, its warning ids and the place of the override that chose it.  No binding.
- `ModelSet` holds the models and their places and diagnostics, with no record of which model each query got.  A query's own `QueryModels` keeps its output in column order.  Records are positional in the model's property order, and a record with an array column has a private parameterless constructor for Dapper; classes have public `get; set;` properties and no constructor.  Sorting reorders the model's properties, not the query's columns.
- `NpgsqlProbe` and `SqlClientProbe` give `Referenced`, the floor and the newest known version; `SQLSRC247` and `SQLSRC267` are reported below the floor when a query of their engine needs an entry, whether its output is `Models` or `CodeGen`, and models are still emitted.
- `AttributeSource` emits the attribute and the enums in a post-initialization file, internal, C# 7.3, with `#pragma warning disable CS0436`; `AttributeSource.GeneratedTypes` lists them for `AttributeConflictSuppressor`.
- The diagnostics end at `SQLSRC271`.
- In the repository, `CodeGen` is set by `NoteQueries` in both end-to-end projects, read through Dapper by the constant, and by the default output of both example projects' `CodeGen/` folders, whose classes call Dapper with the constant.  `ShapeQueries` in both end-to-end projects sets `SqlLocation.Direct` and `MethodLocation.Public` under the project's `Models`.

## Design

### Which queries get a method

A query gets a method for a type when, for that type, its output resolves to `CodeGen`, its file is a usable file of the type (`SQLSRC008` and `SQLSRC009` leave none out), its entry passed every check of 5.1, its parameters and columns have C# types, and it has every model its entry needs: an input model when it has parameters, an output model when it has rows.  A query with neither needs no model and gets a method.  A query that fails any of these has its diagnostic already, gets no method, and gets no constant either; its type still builds.

The models stage records, per type and query, the full names of the models the query got, since a shared model, a table model or a sorted model is not named after the query; the method reads by that record.

### What the type emits

For a `CodeGen` query, `TypeEmitter` writes no constant and no token method.  The nested class of `Nested` holds only the queries whose output is `Sql` or `Models`, and is not written when it holds none.  `SqlLocation` changes nothing about a method.

### Where the method goes

| `MethodLocation` | The method |
|----|----|
| `ExtensionClass`, the default | `public` or `internal` `static` extension method on `DbConnection`, in `static partial class <Type>Extensions`, top-level in the attributed type's namespace, with the attributed type's effective accessibility, at least `internal` |
| `Public`, `Internal`, `Private` | A `static` method of that accessibility on the attributed type, its first argument `DbConnection connection` |

`<Type>` is the attributed type's own name, without its containing types or type parameters.  The extension class's file is its own, with a hint name from `HintName` that cannot clash with a type's or a model's.  Two attributed types that give one extension class name in one namespace share the class, as partial declarations, when their accessibilities are the same; when they differ, it is `SQLSRC276`.

### The signature

```csharp
public static async Task<GetUserDto?> GetUser(
    this DbConnection connection,
    GetUserParams parameters,
    string orderBy,
    DbTransaction? transaction = null,
    int? commandTimeout = null,
    CancellationToken cancellationToken = default)
```

- The name is the query's, with `Async` appended under `async-method-suffix`.
- `parameters` is the input model, present when the query has parameters, and checked with `ArgumentNullException.ThrowIfNull`.
- Each token is a `string` argument, in order of first appearance, named as the token method names it today, and validated as the token method validates it unless `no-token-validation` is in effect.
- The argument names `connection`, `parameters`, `transaction`, `commandTimeout` and `cancellationToken` are fixed; a token that has one of them is `SQLSRC275`.  The names of the method's locals are chosen so that no token's name hides them, as `MethodWriter` chooses its own today.
- The return type, by the shape:

| Shape | Returns |
|----|----|
| `many` | `Task<C>`, where `C` is the collection type of the output model |
| `one` | `Task<TDto>` |
| `one-optional` | `Task<TDto?>` |
| `none` | `Task` |
| `rowcount` | `Task<int>` |

- XML documentation: the query's summary, each argument, the return, and `<exception cref>` for `UnexpectedRowCountException` on `one` and `one-optional`.

### The shapes

The shape is the name marker's, or the default: `many` when the entry has rows, `rowcount` when it has none.  `one`, `one-optional` or `many` on an entry with no rows is `SQLSRC272` at the query, and the query gets no method; the parser cannot see it, since it needs the entry.  It is reported only for a type whose output for the query is `CodeGen`, the only output a shape changes anything for, so a `Models` project gets no error for a setting that has no effect on it.  `none` and `rowcount` run `ExecuteNonQueryAsync` whatever the entry, so an `INSERT ... RETURNING` whose rows nobody wants is a `rowcount` or a `none`.

### The body

In order:

1. The SQL: a `const string` local holding the query's SQL as the constant would have held it, comments kept or stripped by `keep-comments`; for a query with tokens, the string the token method would have returned, built by the same code from the token arguments.
2. The connection rule: `connection.State == ConnectionState.Closed` is noted; when it is, `await connection.OpenAsync(cancellationToken)`, and in a `finally`, `await connection.CloseAsync()` when the method opened it.  An open connection is never touched.
3. The command: `await using var command = connection.CreateCommand()`; `CommandText`; `Transaction = transaction`; `CommandTimeout = commandTimeout.Value` when it has one.
4. The parameters, by the binding step below, in the order of the query's parameter list.
5. The execution, by the shape: `ExecuteNonQueryAsync` for `none` and `rowcount`; for the others `ExecuteReaderAsync`, `await using`, with the behaviour 8.0's spike settles, and `ReadAsync(cancellationToken)` for each row.
6. For `one`: no row is `UnexpectedRowCountException` with no rows; a row is read; a second row is `UnexpectedRowCountException` with more than one.  For `one-optional`: no row is `null`, and a second row throws as for `one`.  For `many`: every row, into the collection.

Every `await` has `.ConfigureAwait(false)`.  `cancellationToken` reaches every asynchronous call that takes one.

### Reading a row

One reading step per query, from its own `QueryModels.Output`, which is in column order: column `i` is read at ordinal `i` into the property that column `i` gives.

- A property the model calls not null: `reader.GetFieldValue<T>(i)`, with no `IsDBNull`.
- A property the model calls nullable: `reader.IsDBNull(i) ? null : reader.GetFieldValue<T>(i)`, where `T` is the property's type without `?`; or `GetFieldValue<T?>` where 8.0's spike finds both drivers accept a nullable value type.
- A record is built by its positional constructor, its arguments in the model's property order, which is the column order unless the model is sorted; a class by an object initializer.
- The synchronous `GetFieldValue<T>` after `ReadAsync`, which reads from the buffered row, unless 8.0's spike finds a driver that does not buffer under the behaviour chosen.

### Binding a parameter

The binding step writes, for each parameter in the list, in a block of the method:

```csharp
var p0 = command.CreateParameter();
p0.ParameterName = "user_id";
p0.Value = parameters.UserId;
command.Parameters.Add(p0);
```

- `ParameterName` is the parameter's name as the SQL writes it, with or without the `@` as 8.0's spike settles for both drivers.
- A nullable property sends `(object?)value ?? DBNull.Value`; a non-null reference property sends `DBNull.Value` when it is null all the same, since a caller can pass one.
- Nothing else is set.  The step is one emitter per engine, chosen by the file's dialect, whose engine-neutral form is the above; 8.3 and 8.4 give each engine's form its typed members, and 8.2 the converter's call on the value, without changing the method around it.

### The driver required

A project with a `CodeGen` query of a dialect whose driver the compilation does not reference gets `SQLSRC273`, once per project and dialect, `Location.None`, naming the package, as the probes report today; its models are emitted and its `CodeGen` queries get no method and no constant.  A driver below the floor, `SQLSRC247` or `SQLSRC267`, also stops the methods of its dialect, and its message and manual section say so.  Below the floor or absent, the generated code never names a type of the driver.

### `UnexpectedRowCountException`

Emitted in the post-initialization file beside the attribute, `internal sealed class UnexpectedRowCountException : InvalidOperationException` in the namespace `SqlSource`, C# 7.3, with the query's name as a property and a message that names the query and whether there were no rows or more than one.  It joins `AttributeSource.GeneratedTypes`.  A method with public accessibility may throw it though it is internal: a caller in another assembly catches `InvalidOperationException`, which the manual says.

### The pipeline

A new per-type step, beside the type's file, combines the type's queries with what the methods need: the usable entries, the C# types and the decided models of the type's claimed files, and the probes' findings.  It runs again only when one of those changes for the type's files, and gives one value-equal record per type, from which one source is added per type and placement.  Each step whose re-runs matter has a name in `TrackingNames` and a case in the caching tests, and `TypeOutput`'s inputs are not widened, so a sidecar edit does not re-emit a type's constants.

## Diagnostics

A clash between two generated methods is reported at both queries, and neither gets its method.  Only what SqlSource writes is compared across types: an extension method a project writes by hand, or one from a reference, which the compiler finds only where it is called, is not looked for.  Finding a clash across types is a project-wide step, so an edit that makes or removes one re-emits the methods of every type it touches, which the caching tests allow for.

| Id | Severity | Where | About |
|----|----|----|----|
| `SQLSRC272` | Error | The query | `one`, `one-optional` or `many` on a query whose entry has no rows |
| `SQLSRC273` | Error | No position, once per project and dialect | A `CodeGen` query of a dialect whose driver the project does not reference |
| `SQLSRC274` | Error | The query, each of the queries for a clash between two | A method's name that another member takes: a member of the attributed type under `Public`, `Internal` or `Private`, the type's own name, a constant or token method or nested class SqlSource writes in the type, another query's method of one name in the same class, or, under `ExtensionClass`, a member of `DbConnection` or of `object`, which an instance call would always pick over the extension, or an extension method of one name and one parameter list that SqlSource writes in another extension class of the same namespace |
| `SQLSRC275` | Error | The query | A token whose name is one of the method's fixed argument names |
| `SQLSRC276` | Error | The attributed type | An extension class whose name a type of the project that SqlSource did not write takes, or that two attributed types give with two accessibilities |

`SQLSRC247` and `SQLSRC267` change their message: below the floor, no method is generated.  The ids follow `SQLSRC271`; if another change takes ids first, this sub-phase takes the next free ones in this order and the plan says so.

## Testing

| Where | Cases |
|----|----|
| Generator tests, `tests/SqlSource.Tests/Generator/` | The text of a method for each shape, each collection type, each placement, with and without parameters, with tokens, with `async-method-suffix`, `keep-comments`, `no-token-validation`, a record and a class output, a sorted model, a shared and a table model, a nullable and a not-null column of a value and a reference type, an override's type; no constant and no nested class for a `CodeGen` query; a mixed type of `Sql`, `Models` and `CodeGen` queries; every diagnostic above, and none in the generated code at the highest warning level; the generated code compiled against Npgsql 8, 9 and 10 and SqlClient 6.1 and 7 from `NpgsqlReferences` and `SqlClientReferences` |
| Driver tests | `SQLSRC273` per dialect; the floor's diagnostics with no method |
| Caching tests | An edit to a query re-emits only the methods of its claiming types; a sidecar edit re-emits the methods and not the type's file; an attribute edit of `MethodLocation`; an edit to a model's settings |
| `AttributeConflictTests` | `UnexpectedRowCountException` among the suppressed types |
| `SettingLevelTests` | The rows that pin "changes no generated code" for `CollectionType`, `MethodLocation` and `CodeGen` now pin their effect |
| End-to-end projects | They still build: `NoteQueries` set to `Models` in both, so that their Dapper tests run as they do, until 8.3 and 8.4 set them back to `CodeGen`; `ShapeQueries` unchanged |
| Examples | Both `CodeGen/` folders build, their classes calling the generated methods in place of Dapper |

The tests that compile generated code obey the Roslyn floor's rules in `tests/SqlSource.Tests/Generator/AGENTS.md`.

## Documentation

- `docs/manual/generator.md`: the Query methods section, written from what was built: placement, signature, the connection rule, shapes, collection types, the exception, what replaces the constant; the known issue that a method's exception cannot be caught by its type from another assembly; the known issue for each engine of a parameter whose type the driver cannot infer from its value, until 8.3 and 8.4 remove it.
- `docs/manual/settings.md`: the statuses of Output's `codegen`, the collection type, `MethodLocation` and `async-method-suffix`, which move to the first table as working.
- `docs/manual/sql-files.md`: result shapes, and the marker index's callouts.
- `docs/manual/parameters-and-tokens.md`: the query's parameters and tokens as a method's arguments.
- `docs/manual/README.md`: the rows of "What works today" for Output, the settings for query methods, result shapes and generated query methods; and "Accepted; no effect yet" removed if nothing is left in that state.
- `docs/manual/concepts.md`, `docs/manual/tool.md`, `src/SqlSource.Tool/README.md`: their callouts.
- `docs/manual/diagnostics.md`: a section for each new id; `SQLSRC247` and `SQLSRC267`; the wording of `SQLSRC231`, `SQLSRC232` and `SQLSRC234` that names a later release of query methods.
- `README.md`: the introduction, the status line, step 4 with a method in place of Dapper, and the FAQ on `Async` names.
- `src/SqlSource/Generation/AttributeSource.cs`: the documentation comments that say "in a later release".
- `docs/architecture/generator.md`: the "Execution methods" placeholder replaced with what was built.
- `src/SqlSource/AGENTS.md`: the ids taken, and any rule the new step needs.
- `docs/tech-debt/TD-0006`: `UnexpectedRowCountException` among the types, and the two copies' consequence for a `catch`.
- `docs/deferred/D-0006`: a SQL Server batch's method reads its first result set.  `D-0002`: no method calls a procedure.
- `examples/` and their READMEs: the `CodeGen/` folders' classes and comments; `examples/AGENTS.md` if a rule changes.
- `tests/AGENTS.md`, `tests/SqlSource.Tests.Postgres/README.md`, `tests/SqlSource.Tests.SqlServer/README.md`: the `NoteQueries` change.
- The epic outline: the row of 8.1.

## Order of work

One pull request, one commit for each step:

1. The outline's row says In progress.  The models stage records which models each query got.
2. No constant, token method or empty nested class for a `CodeGen` query.
3. The per-type step and its record; the placement and the signature, with `SQLSRC274` to `SQLSRC276`.
4. The body: the connection rule, the command, binding by value, the shapes with `SQLSRC272`, reading a row, the collection types.
5. `UnexpectedRowCountException`.
6. `SQLSRC273` and the floor's diagnostics stopping the methods.
7. Caching tests.
8. The end-to-end projects and the examples kept building.
9. The documents, and the row set to Done.

## Left to the builder

- The steps and records, within the caching rules of `src/SqlSource/AGENTS.md`.
- The hint names, the locals' names and the layout of the emitted code.
- How each collection type is built from the reading loop, within the measure: the array from a growable buffer, `ImmutableArray` by a builder, and so on.
- The text of each message and documentation sentence.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- A query that fails a check gets neither its method nor its constant.
- The extension class is named by the attributed type's own name; two attributed types that give one name share it when their accessibilities agree, and conflict otherwise.
- A token named as a fixed argument is an error, not a rename of either.
- `SQLSRC272` only under `CodeGen`.
- A clash of two generated methods is reported at both, and only generated methods are compared across types; a query named as a member of `DbConnection` or `object` is an error under `ExtensionClass`.
- A non-null reference property that holds null at run time is sent as `DBNull.Value`, not refused with an exception.
- The exception is internal, in the namespace `SqlSource`, emitted in every project with the attribute, and its two copies across projects that share internals are a known issue, not a design of their own.
- `ConfigureAwait(false)` on every `await`.
- Until 8.3 and 8.4, the end-to-end projects' `NoteQueries` are `Models`.
- The manual documents the methods from 8.1, with a known issue per engine for the types binding by value cannot send, which 8.3 and 8.4 remove.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.
