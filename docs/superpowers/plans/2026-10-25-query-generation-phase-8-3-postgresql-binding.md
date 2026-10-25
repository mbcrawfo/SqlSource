# Query generation 8.3: PostgreSQL binding and end-to-end - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bind each parameter of a `postgres` method as a new `NpgsqlParameter<T>` with `DataTypeName` set to the sidecar's `bindName`, and prove the methods of 8.1 and the converters of 8.2 against PostgreSQL 14 and 18 on Npgsql 10 and 8: every described query of the end-to-end project through its method, every type of the map written and read through generated code, and a method's allocations no more than hand-written ADO.NET's.

**Architecture:** The models stage already reads each entry's parameters; `QueryModels` gains the sidecar type of each parameter, `MethodPlanner` copies it and the property's C# type into `MethodParameter`, and `PostgresParameterBinder`, moved into a file of its own, writes `new global::Npgsql.NpgsqlParameter<T>()` with `ParameterName`, `DataTypeName` and `TypedValue`.  The SQL Server binder is untouched (8.4's).  The end-to-end project's default output becomes `CodeGen`; the Dapper tests that stay read their SQL through second, `Models` claims; new query files cover the shapes, collection types, locations, tokens, suffix, run-time arguments, converters and every type.

**Tech Stack:** C# (`netstandard2.0` generator, Roslyn 4.8.0 floor), generated code C# 12 on .NET 8+, Npgsql 8.0.3/9.0.3/10.0.3 in generator tests, Npgsql 10 in `tests/SqlSource.Tests.Postgres` and 8.0.9 in `tests/SqlSource.Tests.Postgres.Npgsql8`, xunit v3, Shouldly, Testcontainers on `postgres:18.6` (default) and `postgres:14.24` (floor).

**Spec:** [`docs/superpowers/specs/2026-10-24-query-generation-phase-8-3-postgresql-binding-design.md`](../specs/2026-10-24-query-generation-phase-8-3-postgresql-binding-design.md), with [8.0's spike](../specs/2026-10-24-query-generation-phase-8-spike.md) (sections 6, 7, 8, 11) and the epic's phase 8 section.

## Global Constraints

- Generated code never names an `NpgsqlDbType` member, compiles against Npgsql 8, 9 and 10 with no conditional, uses nothing newer than C# 12, writes every framework type from `global::`, and gives no compiler warning (`GeneratorRun.GeneratedCodeWarnings` empty).
- A parameter is `new global::Npgsql.NpgsqlParameter<T>()`, `T` the type of the value bound: the property's type, `T?` for a nullable value type, or the converter's database side; a null is `TypedValue = null`, never `DBNull.Value`.  `DataTypeName` is the entry's `bindName` exactly as written (`integer`, `public.account_status[]`, `public.email`); no modifier is added.
- A new local of a query's method takes its name from `LocalNames` (`src/SqlSource/AGENTS.md`); this plan adds none (the parameter's local is `Parameter`, already taken).
- Touch the engine-neutral code as little as possible: 8.4 changes the SQL Server binder beside this unit.  `ParameterBinder.cs` keeps the base class and `SqlServerParameterBinder`; nothing of SQL Server's tests or documents changes but where a shared sentence must say "on PostgreSQL".
- Every sidecar is written by `tools/describe-end-to-end.sh` (it needs Docker and also rewrites SQL Server's sidecars, which must come out unchanged; revert any SQL Server or example sidecar diff that is only provenance noise, and stop and report one that is not).  A new query describes the same on 14 and 18, or is left out of 14's `--check` with its reason beside the filter.
- The end-to-end project's queries write parameters in camelCase; a test that writes runs in a transaction that is rolled back; a database left changed is a bug.
- Every commit passes `./pre-commit-validation.sh` run after its last edit, and, since `tests/SqlSource.Tests.Postgres` changes, `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test tests/SqlSource.Tests.Postgres` before a push.  `tests/SqlSource.Tests.Postgres.Npgsql8` runs on the default image only.
- Tests: `Method_Scenario_Outcome`, Shouldly, `sealed` classes, a server test in `[Collection(UsesPostgres.Name)]`; a file in `tests/SqlSource.Tests/Generator/` starts with the shared line, ends in `Tests.cs`, uses Roslyn 4.8.0 API and C# 12, and passes an `OutputKind`.
- Shared files with 8.4 (`docs/manual/generator.md`, `docs/architecture/`, `tests/README.md`, `tests/AGENTS.md`, `tests/SqlSource.Testing/`, the epic outline): small, local edits; the outline's 8.3 row only.

## Where merged code differs from the spec

- 8.1's `MethodParameter` carries no C# type and no description of the parameter, and `QueryModels` keeps nothing of an entry's parameters but the properties.  Task 1 adds both, engine-neutral, so that 8.4 can read its facets from the same `Described`.
- `AllTypesRows` (`tests/SqlSource.Testing/EndToEnd/AllTypesRows.cs`) already checks Npgsql 8's and 10's types, so both projects share it for the methods' reads unchanged.

## Calls the spec left to the builder, settled

- **Classes and files.**  `Queries/Methods.sql`, claimed by `MethodQueries` (extension class, default settings): the shapes, tokens, suffix, rowcounts, sleeps.  `Queries/Collections.sql`, claimed by `CollectionQueries`: ten `many` queries, one per collection type by its `-- collection-type:` marker.  `Queries/Locations.sql`, one query, claimed by `InternalLocationQueries` (`MethodLocation.Internal`) and `PrivateLocationQueries` (`MethodLocation.Private`, with a hand-written `internal static` wrapper in its own partial declaration).  `Queries/Cube.sql`, claimed by `CubeQueries`, main project only.  The second, `Models` claims are `AllTypesSql` (`AllTypes.sql`, same namespace as `AllTypesQueries`, so they share models) in both projects.
- **The id type** is `NoteRef`, a `readonly record struct NoteRef(long Value)` with two converters, over a new domain `note_ref AS bigint`, the type of a new nullable column `notes.ref`, seeded with a value on two notes and `NULL` on the third; `Notes.sql` gains `GetNoteByRef`, whose parameter is declared `-- param: @ref public.note_ref`.
- **The allocation test's query** is `GetAccountName` of `Accounts.sql` (one parameter, one row of two columns) and `ListAccounts` (no parameter, three rows); see Task 7 for the counts and margin.

## Review Focus

1. **A null for a nullable value type where the server infers the type**, `@closedBefore IS NULL` of `ListClosedBefore` with `null`: a typed `NULL`, every account read (Task 2).
2. **A null in a property of a reference type that the model calls not null**, `FindAccountName` (`many` by default) with `Email = null!`: sent as `NULL`, so the method returns no row, and the connection is closed again (Task 4).
3. **A value longer than its `varchar(n)` parameter** is bound as `character varying` and refused by the server's own `22001` on insert, never cut (Task 6).
4. **A domain's `CHECK` does not apply to a parameter compared with it**: `GetNoteByRef`'s neighbour, a `FindNotesByEmail` with `"nobody"`, returns no row instead of failing (Task 5).
5. **A `DateTimeOffset` at a non-zero offset for `timestamptz`** throws Npgsql's `ArgumentException` before sending, and the connection the method opened is closed again (Task 4).

---

### Task 1: Binding by `bindName`; the outline's row In progress

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 8.3: `In progress`)
- Modify: `src/SqlSource/Generation/Models/QueryModels.cs`, `src/SqlSource/Generation/Models/ModelReader.cs`
- Modify: `src/SqlSource/Generation/Methods/QueryMethod.cs`, `src/SqlSource/Generation/Methods/MethodPlanner.cs`
- Modify: `src/SqlSource/Generation/Emitting/ParameterBinder.cs` (remove `PostgresParameterBinder`; make `AppendByElement` `protected static`)
- Create: `src/SqlSource/Generation/Emitting/PostgresParameterBinder.cs`
- Test: `tests/SqlSource.Tests/Generation/Emitting/PostgresParameterBinderTests.cs` (new), `ParameterBinderTests.cs` (SQL Server rows only), `QueryMethodWriterTests.cs`, `tests/SqlSource.Tests/Generator/MethodTests.cs`, `ConverterMethodTests.cs`, `MethodCompileTests.cs`, and any other test whose expected PostgreSQL binding text changes.

**Interfaces:**
- Produces: `QueryModels` gains a last member `EquatableArray<SidecarType>? ParameterTypes`, the type of each parameter of the entry in order (null when `Input` is).  `MethodParameter(string Name, string Property, string TypeText, bool IsValueType, bool IsNullable, Conversion? Write = null, SidecarType? Described = null)`: `TypeText` the property's type without `?`, as `MethodColumn.TypeText`; `Described` the sidecar's type of the parameter, whose `BindName` the PostgreSQL binder sets and whose facets 8.4 will read.  `PostgresParameterBinder` (internal sealed, in its own file) is still returned by `ParameterBinder.For(DatabaseEngine.PostgreSql)`.

- [ ] **Step 1: Set the outline's row of 8.3 to `In progress`.**

- [ ] **Step 2: Write the failing binder tests** in `PostgresParameterBinderTests` (helper `Write(MethodParameter, value = "value")` as `ParameterBinderTests.Write`, engine PostgreSql):

```csharp
// For_NotNullValueType_BindsATypedParameterByItsBindName
Write(new MethodParameter("user_id", "UserId", "int", true, false, Described: Integer("integer")))
    .ShouldBe(
        """
            var p0 = new global::Npgsql.NpgsqlParameter<int>();
            p0.ParameterName = "user_id";
            p0.DataTypeName = "integer";
            p0.TypedValue = parameters.UserId;
            command.Parameters.Add(p0);

        """);
```

and these, each by `ShouldContain` of the lines named:
- `Append_NullableValueType_IsAParameterOfTheNullableType`: `"int"`, nullable → `new global::Npgsql.NpgsqlParameter<int?>()` and `p0.TypedValue = parameters.UserId;`.
- `Append_ReferenceType_IsAParameterOfTheType`: `"string"`, `bindName` `public.email` → `NpgsqlParameter<string>()`, `p0.DataTypeName = "public.email";`.
- `Append_NoBindName_SetsNoDataTypeName`: `Described: null` → no line containing `DataTypeName`.
- `Append_BindNameWithAQuote_IsWrittenAsALiteral`: `bindName` `public."Odd ""name"""` → `p0.DataTypeName = "public.\"Odd \"\"name\"\"\"";` (by `SymbolDisplay.FormatLiteral`).
- `Append_NotNullValueTypeWithAConverter_BindsTheDatabaseType`: `ToLong` (see `ParameterBinderTests`) → `NpgsqlParameter<long>()` and `p0.TypedValue = global::App.Ids.ToLong(parameters.Id);`.
- `Append_NullableOrReferencePropertyWithAConverter_BindsNullForNull` (theory as the old one): `long` value side → `NpgsqlParameter<long?>()` and `p0.TypedValue = parameters.Id is { } v1 ? global::App.Ids.ToLong(v1) : null;`; a reference database side (`string`, `DatabaseIsValueType` false) → `NpgsqlParameter<string>()`.
- `Append_ArrayConvertedByElement_BindsTheDatabaseArray` (the three rows of the old theory): `T` is the `ConvertAll` result's array, `long[]`, `string?[]`, `long?[]`, and `p0.TypedValue = parameters.Ids is { } v1 ? <converted> : null;`.

Change `ParameterBinderTests` to SQL Server alone (its theory `For_EachEngine_...` becomes `For_SqlServer_WritesTheBindingByValue`, loops become a single engine).

- [ ] **Step 3: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~ParameterBinderTests"`; expected: compile failure (`MethodParameter` has no `TypeText`).

- [ ] **Step 4: Implement.**  `ModelReader.ReadQuery` fills `ParameterTypes` from `entry.Parameters[i].Type` (null when the entry has no parameters).  `MethodPlanner.TryReadInput` passes `property.Type.Text` and `models.ParameterTypes?[index]`.  `PostgresParameterBinder.Append` writes the five lines above; `T` is: no converter → `TypeText` plus `?` for a nullable value type; whole-value converter → `Write.DatabaseText` plus `?` when `Write.DatabaseIsValueType` and the property is nullable or a reference type; by element → the array `AppendByElement` gives (its database element text plus `[]`).  The value is the property, the converter's call, or `parameters.X is { } <names.Value> ? <call or ConvertAll> : null` (target-typed, C# 9+), reusing `AppendByElement`.  Update every test that pinned PostgreSQL's old binding text (`MethodTests`, `ConverterMethodTests`, `QueryMethodWriterTests`) to the new lines; a test that ran both engines over one expectation now expects each its own.

- [ ] **Step 5: The binding of every kind, compiled.**  In `MethodTests` add `Run_ParameterOfEachKind_BindsItsBindName` (a theory whose rows give the parameter's sidecar type and expected lines): `integer` → `NpgsqlParameter<int>` `"integer"`; `integer[]` → `NpgsqlParameter<int[]>` (as the map writes it) `"integer[]"`; an enum `public.mood` (string) → `NpgsqlParameter<string>` `"public.mood"`; a domain `public.email` over `text` → `"public.email"`; `int4range` → `NpgsqlParameter<global::NpgsqlTypes.NpgsqlRange<int>>` `"int4range"`; `int4multirange` → `NpgsqlParameter<global::NpgsqlTypes.NpgsqlRange<int>[]>` `"int4multirange"`; a converted parameter (an override with converters, as in `ConverterMethodTests`) → the database type.  In `MethodCompileTests` add `Run_ParameterOfEachKindOnNpgsql_Compiles(int major)` for 8, 9, 10 over one file holding those seven parameters: `ShouldBeClean()` and no `GeneratedCodeWarnings`.

- [ ] **Step 6: Run** the generator tests: `dotnet test tests/SqlSource.Tests` and `dotnet test tests/SqlSource.Tests.RoslynFloor`; expected PASS.  The end-to-end projects still build (their `CodeGen` queries are the examples' and none of the end-to-end's yet).

- [ ] **Step 7: Validate and commit** (`./pre-commit-validation.sh`, then `git commit -m "Bind each PostgreSQL parameter by its bindName"`).

---

### Task 2: The project's default `CodeGen`; the existing queries through their methods

**Files:**
- Modify: `tests/SqlSource.Tests.Postgres/SqlSource.Tests.Postgres.csproj` (`SqlSourceOutput` `CodeGen`, comment), `EndToEnd/AccountQueryTests.cs`, `InvoiceQueryTests.cs`, `ShapeQueryTests.cs`, `AllTypesQueryTests.cs`, `DapperRecipeTests.cs`, `ModelShapeTests.cs`
- Create: `EndToEnd/AllTypesSql.cs` (`[SqlSourceGenerate(Path = "Queries/AllTypes.sql", SqlLocation = SqlLocation.Direct, Output = GeneratorOutput.Models)] internal static partial class AllTypesSql;`)

**Interfaces:**
- Produces: `AllTypesSql.ListAllTypes`, `AllTypesSql.InsertAllTypes` (constants the Dapper tests run); the methods `AccountQueriesExtensions.GetAccount(...)` and friends, `Reporting.AccountReportsExtensions`, `InvoiceQueriesExtensions`, `ShapeQueries.SearchAccounts(connection, ...)` (Public), `AllTypesQueriesExtensions.ListAllTypes`.

- [ ] **Step 1: Switch the project to `CodeGen`** and see the build fail where tests use constants that are now methods.

- [ ] **Step 2: Rewrite each query class's Dapper test as a test of its method**, one test per query, keeping the asserted rows: `AccountQueryTests` (every query of `Accounts.sql` through `AccountQueries`' methods, and `GetAccount`/`ListAccounts` through `Reporting.AccountReports`' to prove the second namespace; `ListClosedBefore` with `null` and with a date), `InvoiceQueryTests`, `ShapeQueryTests`.  Names: `GetAccountAsync_...` is wrong; the method is what the test calls, so `GetAccount_OfBob_ReadsTheAccount`.  Tests take the connection from `EndToEndSources.App(postgres)` and pass `cancellationToken: TestContext.Current.CancellationToken`; half open the connection and half pass it closed, so both paths run (the connection rule is asserted in Task 4).
- [ ] **Step 3: Move the Dapper reads of `all_types` and `DapperRecipeTests` to `AllTypesSql`'s constants**, and add `ListAllTypes_BothRows_ReadsEveryColumn` calling the method (`AllTypesRows.ShouldBeTheFullRow` and `ShouldBeTheNullRow`).  `ModelShapeTests`: assertions of a constant's text that no longer exists move to a type that still has it (`AllTypesSql`, or drop the text check where `ConstantQueries` already proves constants), and add one that `AccountQueries`, `InvoiceQueries` and `AllTypesQueries` have no constant.
- [ ] **Step 4: Run** `dotnet test tests/SqlSource.Tests.Postgres`; expected PASS.
- [ ] **Step 5: Validate and commit** ("Run the end-to-end queries through their methods").

---

### Task 3: The shapes, collection types, locations, tokens and suffix

**Files:**
- Create: `tests/SqlSource.Tests.Postgres/EndToEnd/Queries/Methods.sql`, `Collections.sql`, `Locations.sql` (sidecars by the script), `MethodQueries.cs`, `CollectionQueries.cs`, `LocationQueries.cs` (both location classes and the private one's wrapper), `MethodQueryTests.cs`, `CollectionQueryTests.cs`, `LocationQueryTests.cs`
- Modify: `SqlSource.Tests.Postgres.csproj` (a project `SqlSourceCollectionType` and a folder's metadata, below)

**Interfaces:**
- Produces (in `Methods.sql`, all on `accounts`/`notes` of the seed): `TouchAccount` (`UPDATE accounts SET name = name WHERE id = @accountId;`, default `rowcount`), `TouchAccountQuietly -> none` (same SQL), `GetAccountInRange -> one` and `FindAccountInRange -> one-optional` (`SELECT id, name FROM accounts WHERE id BETWEEN @fromId AND @toId ORDER BY id;`), `CountNotesOf` with `-- generator: async-method-suffix` (method `CountNotesOfAsync`), `ListAccountsWhere` (`... WHERE {{filter:id = @accountId}} ORDER BY id`, `-- param: @accountId integer`), `AddNote -> rowcount` (`INSERT INTO notes (...) VALUES (...) RETURNING id`), `ListAccountsAsRowCount -> rowcount` (a `SELECT`), `RenameInCte -> rowcount` (`WITH u AS (UPDATE accounts SET name = name WHERE id = @accountId RETURNING id) SELECT id FROM u`).

- [ ] **Step 1: Write the SQL and the claims**, then `tools/describe-end-to-end.sh`, and read the diff (only the new sidecars; any other change is reverted or reported).
- [ ] **Step 2: Write the tests**, each asserting values:
  - `TouchAccount_OneRow_Returns1`, `TouchAccount_NoRow_Returns0`, `TouchAccountQuietly_Runs_ReturnsNothing` (the write's effect checked in the same rolled-back transaction).
  - `GetAccountInRange_OneRow_ReadsIt`, `..._NoRow_ThrowsUnexpectedRowCount`, `..._TwoRows_ThrowsUnexpectedRowCount`; `FindAccountInRange_OneRow_ReadsIt`, `..._NoRow_ReturnsNull`, `..._TwoRows_ThrowsUnexpectedRowCount` (the exception's message names the query).
  - `CountNotesOfAsync_Alice_Reads2` (reflection: no method `CountNotesOf`).
  - `ListAccountsWhere_DefaultFilter_ReadsTheAccount`, `ListAccountsWhere_AnotherFilterThatDropsTheParameter_ReadsItsRows` (`"status = 'active'"`, the declared `accountId` bound and unused).
  - `AddNote_InsertReturning_Returns1`, `ListAccountsAsRowCount_Select_ReturnsMinus1`, `RenameInCte_UpdateInsideAWith_ReturnsMinus1`.
  - Collections: one test per type of `CollectionKind` (`IEnumerable`, `ICollection`, `IReadOnlyCollection`, `IList`, `IReadOnlyList`, `Array`, `List`, `ImmutableArray`, `ImmutableList`, `IImmutableList`), each by its query's marker, asserting the method's declared return type by reflection and the three accounts' rows.  The other levels: set the project property `SqlSourceCollectionType` to `IReadOnlyList` (so `AccountQueries.ListAccounts` returns it), the metadata `SqlSourceCollectionType="IList"` on `EndToEnd/Queries/Billing/**/*.sql` (`ListInvoices`), and `ShapeQueries`' attribute `List`, over a query of `Shapes.sql` that its file marker does not override (check `docs/manual/settings.md#precedence`; move the marker into a query if the file header would win).  `CollectionQueryTests.ReturnType_OfEachLevel_IsThatLevels` asserts the four.
  - Locations: `GetAccountName` of `Locations.sql` through `AccountQueries`' extension (already), `ShapeQueries` (Public, already), `InternalLocationQueries.GetLocatedName(connection, args)`, and the private one through its wrapper; reflection asserts each method's accessibility and that `Internal` and `Private` give no extension class.
- [ ] **Step 3: Run** the project's tests on the default image and on `postgres:14.24`; expected PASS, `CheckTests` included.
- [ ] **Step 4: Validate and commit** ("Run every shape, collection type, location and token through a method").

---

### Task 4: The run-time arguments and the connection rule

**Files:**
- Modify: `Queries/Methods.sql` (`Sleep -> none`: `SELECT 1 AS slept FROM pg_sleep(@seconds);` and `ListThenSleep`: a `generate_series` of `@count` rows whose last row sleeps `@seconds`, in a form that sends the first rows before the sleep on 14 and 18, per spike section 11), its sidecar by the script
- Create: `EndToEnd/RunTimeTests.cs`

- [ ] **Step 1: Add the two queries and describe.**
- [ ] **Step 2: Write the tests** (connections from `EndToEndSources.App`):
  - `TouchAccount_InATransaction_IsUndoneByItsRollback` (count by a second method call inside the transaction, then rollback, then read again).
  - `Sleep_CommandTimeoutShorterThanTheSleep_Throws` (`commandTimeout: 1`, `seconds: 30`; Npgsql's `NpgsqlException` with an inner `TimeoutException`), and the closed connection is `Closed` after.
  - `Sleep_TokenCancelledBeforeTheCall_ThrowsOperationCanceled` (closed connection stays `Closed`).
  - `ListThenSleep_TokenCancelledDuringTheRead_ThrowsOperationCanceled` (`CancelAfter` once the first rows have been sent; closed connection `Closed` after).
  - `GetAccount_ClosedConnection_IsClosedAgain`, `GetAccountInRange_ClosedConnectionAndAnException_IsClosedAgain`, `GetAccount_OpenConnection_IsLeftOpen`, `GetAccount_OpenConnectionInATransaction_IsLeftOpenWithTheTransaction` (the transaction commits or rolls back after).
  - Review Focus 2: `FindAccountName_NullEmail_SendsNullAndFindsNoRow` (empty result, connection closed after).
  - Review Focus 5: `ListClosedBefore_OffsetNotZero_ThrowsBeforeSending` (`ArgumentException`, connection closed after).
- [ ] **Step 3: Run** on both images; expected PASS.
- [ ] **Step 4: Validate and commit** ("Prove the run-time arguments and the connection rule").

---

### Task 5: Notes through converters, and the id type

**Files:**
- Modify: `EndToEnd/app/schema.sql` (`CREATE DOMAIN note_ref AS bigint;`, `notes.ref note_ref` nullable), `EndToEnd/app/seed.sql` (values on notes 1 and 3, `NULL` on 2), `Queries/Notes.sql` (`-- type-override: public.note_ref = SqlSource.Tests.Postgres.EndToEnd.Types.NoteRef`; `GetNoteByRef` with `-- param: @ref public.note_ref`; `FindNotesByEmail` with `-- param: @email public.email`, reading `n.id, a.email`), `NoteQueries.cs` (output back to the project's `CodeGen`, comment updated), `Types/Email.cs` (converters `FromDatabase(string)`, `ToDatabase(Email)`), `Types/EmailHandler.cs` unchanged (Dapper still uses it in the recipe test, if any; delete it if nothing does and say so in its commit)
- Create: `Types/NoteRef.cs` (record struct with `[SqlSourceConverter] FromDatabase(long)` and `ToDatabase(NoteRef)`)
- Modify: `NoteQueryTests.cs` (every Notes query through its method; `ListMarkedNames` through its method too), sidecars by the script

- [ ] **Step 1: Schema, seed, SQL, converters; describe; read the diff** (only `Notes.sql.json` changes among existing sidecars, since no existing query reads `notes.*` whole: check).
- [ ] **Step 2: Tests:** each existing Notes query through its method with the rows the Dapper test asserted (`NoteKind` read through the `MapEnum` data source, `Email` through its converter); `GetNoteByRef_ARef_ReadsTheNote` (column and parameter converted, a value type), `ListNoteRefs`-style read of the `NULL` ref as `null` (add the column to `GetNoteByRef`'s neighbour if needed); `FindNotesByEmail_Alice_ReadsHerNotes` (parameter through `ToDatabase`), Review Focus 4: `FindNotesByEmail_ValueTheDomainRefuses_FindsNoRow`.
- [ ] **Step 3: Run** both images; **Step 4: validate and commit** ("Run the notes through converters, and an id type").

---

### Task 6: Every type written and read, on Npgsql 10 and 8

**Files:**
- Modify: `Queries/AllTypes.sql` (`InsertEveryType`: every column of `all_types` from a parameter, `RETURNING *`, `-- type-override: timestamptz = default`), its sidecar by the script; `EndToEnd/app/schema.sql` (`CREATE EXTENSION cube;`, comment updated)
- Create: `Queries/Cube.sql` (`EchoCube`: `SELECT CAST(@value AS cube) AS value;`), `CubeQueries.cs`, `EndToEnd/EveryTypeTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres.Npgsql8/AllTypesQueries.cs` (output `CodeGen`, which is the project's then: set the project's `SqlSourceOutput` to `CodeGen`), add `AllTypesSql.cs` there (the Dapper claim), `AllTypesQueryTests.cs` (Dapper read via `AllTypesSql`; method read; method write), `.csproj` comments

**Interfaces:**
- Consumes: `AllTypesRows.ShouldBeTheFullRow(row, id, nullProperties)`, `ShouldBeTheNullRow`.

- [ ] **Step 1: SQL and describe.**  The input model's values come from the method-read row 1 (as `ArgsFrom` builds them, `Bit1Value` a `BitArray`), and from row 2 for the row of `NULL`s.
- [ ] **Step 2: Tests in the main project:**
  - `InsertEveryType_FullRow_ReadsEveryValueBack` (id 3, `AllTypesRows.ShouldBeTheFullRow(row, 3)` with no null properties) and `InsertEveryType_NullRow_ReadsEveryNullBack` (id 4), both in one rolled-back transaction, and a second read through `ListAllTypes` inside it.
  - `EchoCube_AValue_ReadsItBack` (`new NpgsqlCube(...)`).
  - `Map_EveryKey_IsWrittenAndReadOrNamed`: a dictionary in the test from each `PostgresTypeMap.Keys` to the `InsertEveryTypeArgs` property that writes it or `"EchoCube"`, and a second dictionary `MapLeftOut` from the key to why (the registered types read only by a cast, `pg_snapshot`, `txid_snapshot`, `pg_node_tree`, `record`, `unknown`, `void`: take the reasons from `schema.sql`'s comment); the union equals the keys, they do not overlap, and each property exists.
  - Review Focus 3: `InsertEveryType_VarcharLongerThanItsLength_IsRefusedByTheServer` (`PostgresException` `22001`).
- [ ] **Step 3: Npgsql 8**: `ListAllTypes_BothRows_ReadsEveryColumn` and `InsertEveryType_FullRowAndNullRow_ReadEveryValueBack` through the methods; the Dapper read stays on `AllTypesSql`.  The data source maps nothing (no enum or plugin needed for `all_types`).
- [ ] **Step 4: Run** both projects (main on both images); **Step 5: validate and commit** ("Write and read every type through the methods, on Npgsql 10 and 8").

---

### Task 7: The allocation test

**Files:**
- Create: `tests/SqlSource.Tests.Postgres/EndToEnd/AllocationTests.cs` and its collection `MeasuresAllocations` (`[CollectionDefinition(Name, DisableParallelization = true)]`, `ICollectionFixture<PostgresContainer>`, so it gets a container of its own)

- [ ] **Step 1: Write the test** `GetAccountName_PerCall_AllocatesNoMoreThanHandWritten` and `ListAccounts_PerCall_AllocatesNoMoreThanHandWritten`: the hand-written method is in the test, as the manual's recipe writes ADO.NET (`CreateCommand`, `CreateParameter`, `Value`, `ExecuteReaderAsync`, `ReadAsync`, `GetFieldValue<T>` by ordinal, the model built, the array of rows for `ListAccounts`), on the same open connection from one data source.  Warm each up 200 calls; then, alternately, 2,000 calls of each, measured by `GC.GetTotalAllocatedBytes(true)` before and after; `generated / calls <= handWritten / calls + Margin`, `Margin` 64 bytes, stated in a comment: the per-call difference of Npgsql's own pooled buffers and state machine noise is below it on the spike's numbers (section 8: 24 bytes for a box, 296 bytes an execution), and an extra boxed value or array per call would exceed it only with several, so the margin catches a regression of a few allocations and not jitter.  Assert both numbers are greater than zero (it measured something).
- [ ] **Step 2: Run** it three times; if it flakes, raise the counts (never the margin above 128) and say so in the comment.
- [ ] **Step 3: Validate and commit** ("Measure a method's allocations against hand-written ADO.NET").

---

### Task 8: The documents; the row Done

**Files:**
- Modify: `docs/manual/postgresql.md` (Query methods: how a parameter is bound, from the user's side; `MapEnum` and plugins on the data source for a mapped type with no converter; what Npgsql 8 and 10 read; `rowcount` `-1` for a `SELECT` and a `WITH` ending in one; what the tests found: the `DateTimeOffset` offset, a `DateTime`'s `Kind` now refused by Npgsql instead of converted, the domain's `CHECK`, a value longer than `varchar(n)`; the known issue of binding by value removed)
- Modify: `docs/manual/generator.md` ("How a parameter is sent": PostgreSQL by its described type, SQL Server still by value, callout narrowed to SQL Server; the PostgreSQL known issue removed; a new known issue: a `DbConnection` whose commands keep parameters of their own type cannot take Npgsql's, and fails with the wrapper's `InvalidCastException`), `README.md` (the line on the common path where the FAQ answers `InvalidCastException` or the supported environments say what the methods need)
- Modify: `docs/manual/README.md` (What works today: the methods row for PostgreSQL), `docs/manual/recipes.md` (the Dapper recipe only if a test found otherwise)
- Modify: `docs/architecture/generator.md` (replace the `### Typed binding` placeholder with a `PostgreSQL` part, keeping a placeholder for SQL Server; the "Binding a parameter" paragraph and the `QueryModels`/`MethodParameter` facts)
- Modify: `tests/SqlSource.Tests.Postgres/README.md`, `tests/README.md`, `tests/AGENTS.md` (the claims, the methods' tests, the allocation collection, the Npgsql 8 project's methods; the rules that say `NoteQueries` is `models` and that the Npgsql 8 project is a read), `CONTRIBUTING.md` (what the Npgsql 8 project proves), the epic outline (row 8.3 `Done`)

- [ ] **Step 1: Write them**, then run the internals grep of `docs/manual/AGENTS.md` over the changed manual pages and the "When a phase lands" grep, and fix each hit.
- [ ] **Step 2: Validate and commit** ("Document PostgreSQL's binding; 8.3 done").
