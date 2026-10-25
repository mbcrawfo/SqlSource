# Query generation 8.4: SQL Server binding and end-to-end - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bind each parameter of an `mssql` method by the `SqlDbType` and facets of its described type, and prove 8.1's methods and 8.2's converters against SQL Server 2017 and 2025 on SqlClient 7 and 6.1, every query of the end-to-end project run through its method.

**Architecture:** The sidecar's `SqlServerType` of each parameter travels from `ModelReader` on `ModelProperty.Described` to `MethodParameter.Described`; a new `SqlServerBinding` maps its base name to a `SqlDbType` and its facets and writes the binding, called from `SqlServerParameterBinder`.  `PostgresParameterBinder` is untouched, so 8.3 can be built beside this.  The end-to-end project's default output becomes `CodeGen`; Dapper keeps its tests through second, `Models` claims.

**Tech Stack:** C# (`netstandard2.0` generator, Roslyn 4.8.0 floor), generated code C# 12 on .NET 8+, Microsoft.Data.SqlClient 6.1.7 and 7.1.1, xunit v3, Shouldly, Testcontainers.MsSql, SQL Server 2017 (`2017-CU31-GDR19-ubuntu-18.04`, the floor) and 2025 (the default image).

**Spec:** [`docs/superpowers/specs/2026-10-24-query-generation-phase-8-4-sql-server-binding-design.md`](../specs/2026-10-24-query-generation-phase-8-4-sql-server-binding-design.md), with [8.3's spec](../specs/2026-10-24-query-generation-phase-8-3-postgresql-binding-design.md), which it mirrors, [8.0's spike](../specs/2026-10-24-query-generation-phase-8-spike.md) (sections 4, 6, 9, 10, 11) and the epic's phase 8 section.

## Global Constraints

- Generated code uses nothing newer than C# 12, writes every framework type from `global::`, and names a local of a query's method only through `LocalNames` (`src/SqlSource/AGENTS.md`).
- The parameter is the one the command creates, cast: `(global::Microsoft.Data.SqlClient.SqlParameter)command.CreateParameter()`; `ParameterName` without the `@`; a NULL is `DBNull.Value`, but for `vector`.
- The binding is the described type's, never the value's, but for one exception: a string or `byte[]` longer than the described length is bound at `max` of its variable-length type.  A value that fits never changes the binding.
- `datetime2`, `time`, `datetimeoffset` at `Scale` 7 whatever the described scale; `decimal`/`numeric` with both `Precision` and `Scale`; `json` as `NVarChar` `Size` -1; `vector` with no `SqlDbType`, its NULL `global::Microsoft.Data.SqlTypes.SqlVector<float>.CreateNull(n)`.
- Owner decisions (2026-10-24): a `decimal`/`numeric`/`money` value with more scale than its type is rounded by SqlClient, and the tests pin it; a not-null `SqlVector<float>` column holding `NULL` throws `InvalidCastException("Column '<name>' is null.")`, which 8.4 tests.
- Keep shared engine-neutral code as small as possible (8.3 runs beside this): `ParameterBinder.cs` changes only by the extraction in Task 1; the epic outline's row 8.4 only; `docs/manual/generator.md`, `docs/architecture/`, `tests/README.md`, `tests/AGENTS.md`, `tests/SqlSource.Testing/` edited locally.
- No entry of `EndToEnd/Queries/` differs between 2017 and 2025: a parameter typed differently declares `-- param:`, and a 2025-only query goes in `NewTypes.sql`, left out of 2017's `--check` and skipped by `SkipBelow(17, ...)`.  Sidecars are written by `tools/describe-end-to-end.sh` alone, and their diff read before commit.
- A test that writes runs in a transaction that is rolled back and gives it to every call; a test asserts at least one row's values, NULLs included.
- Every commit passes `./pre-commit-validation.sh` after its last edit.  Every task that changes `tests/SqlSource.Tests.SqlServer` also runs that project on the floor, `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/SqlSource.Tests.SqlServer`, before its commit.
- Tests: `Method_Scenario_Outcome`, Shouldly, `sealed` classes; a file in `tests/SqlSource.Tests/Generator/` starts with the shared line, ends in `Tests.cs`, uses C# 12 and Roslyn 4.8.0 API, and states its `OutputKind`.

## Where merged code differs from the spec

- **No `MethodParameter` carries the described type.**  `ModelProperty` gains `SidecarType? Described = null` (set for a parameter by `ModelReader`, not compared by `ModelCollector.FindDifference`), and `MethodParameter` gains the same member, copied by `MethodPlanner.TryReadInput`.  It is the whole `SidecarType`, so 8.3 reads `BindName` from it too; the shared notes folder says so.
- **`AppendByValue` writes `DBNull.Value` for a null**, and a nullable `vector` needs `CreateNull(n)`.  Its value expression is extracted to `protected static void AppendValue(StringBuilder builder, string parameters, MethodParameter bound, ConversionNames names, string nullText)`; `AppendByValue` calls it with `global::System.DBNull.Value`, and its output is unchanged.
- **`sysname` is a row of the map and a described name.**  It binds as `NVarChar` with `Size` 128, as `nvarchar(128)`.
- **The cancellation during the read** is made deterministic by a column converter that cancels the token on the first row (Task 4), not by timing.

## Review Focus

1. **A converted string parameter longer than its column** (`dbo.Email` over `nvarchar(320)`, a 321-character `Email`): the length rule must test the converted value, so the server refuses the insert whole.  Task 5 pins it.
2. **An over-long value for a fixed type** (`char(5)` given `"abcdef"`): bound as `varchar(max)`, not cut to `"abcde"`; in a `WHERE` it matches no `"abcde"` row.  Task 6 pins it.
3. **A C# enum override of an integer type** bound with that type's `SqlDbType` and read back (`tinyint` as `enum : byte`).  Task 6 pins it.
4. **A NULL for every nullable parameter type**, binary included (by value a NULL was `nvarchar(4000)`, which `varbinary` refuses).  Task 6's null row writes every column NULL.
5. **A `datetime2(3)` parameter in a `WHERE`** is declared at 7, so a seven-digit value matches only a row holding exactly it; the manual says so.  Task 6 pins the insert's rounding and the `WHERE`.

---

### Task 1: The SQL Server binding in the generator

**Files:**
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (row 8.4: `In progress`); add this plan
- Modify: `src/SqlSource/Generation/Models/ModelProperty.cs`, `src/SqlSource/Generation/Models/ModelReader.cs` (`ReadParameters`)
- Modify: `src/SqlSource/Generation/Methods/QueryMethod.cs` (`MethodParameter`), `src/SqlSource/Generation/Methods/MethodPlanner.cs` (`TryReadInput`)
- Modify: `src/SqlSource/Generation/Emitting/ParameterBinder.cs` (extract `AppendValue`; `SqlServerParameterBinder.Append` calls `SqlServerBinding.Append`)
- Create: `src/SqlSource/Generation/Emitting/SqlServerBinding.cs`
- Test: `tests/SqlSource.Tests/Generation/Emitting/SqlServerBindingTests.cs` (new), `tests/SqlSource.Tests/Generation/Emitting/ParameterBinderTests.cs`, `tests/SqlSource.Tests/Generator/MethodCompileTests.cs`, `tests/SqlSource.Tests/Generator/CachingMethodTests.cs` / `CachingModelTests.cs` if a caching case breaks

**Interfaces:**
- Produces: `ModelProperty(..., bool IsArray, SidecarType? Described = null)`; `MethodParameter(string Name, string Property, bool IsValueType, bool IsNullable, Conversion? Write = null, SidecarType? Described = null)`.
- Produces: `internal static class SqlServerBinding` with `public static void Append(StringBuilder builder, string indent, string command, string parameter, string parameters, MethodParameter bound, ConversionNames names)`, and `internal static string? SqlDbTypeOf(string baseName)` (the member name of `SqlDbType`, or null for `vector` and any name the map lacks).

The binding of each base name (case-insensitive), from the spec's table:

| Base name | `SqlDbType` | Facets | Over-long |
|----|----|----|----|
| `char`, `varchar` | `Char`, `VarChar` | `Size` = `maxLength`, `-1` for `max` | `VarChar`, `Size` -1 |
| `nchar`, `nvarchar`, `sysname` | `NChar`, `NVarChar`, `NVarChar` | `Size` = `maxLength / 2`, `-1` for `max` | `NVarChar`, `Size` -1 |
| `binary`, `varbinary` | `Binary`, `VarBinary` | `Size` = `maxLength`, `-1` for `max` | `VarBinary`, `Size` -1 |
| `decimal`, `numeric` | `Decimal` | `Precision`, `Scale` (cast to `byte`) | |
| `datetime2`, `time`, `datetimeoffset` | `DateTime2`, `Time`, `DateTimeOffset` | `Scale` = 7 | |
| `json` | `NVarChar` | `Size` = -1 | |
| `timestamp`, `rowversion` | `Timestamp` | | |
| `vector` | none | value is the `SqlVector<float>`; NULL is `CreateNull(n)`, `n` from `vector(n)` | |
| `bigint`, `int`, `smallint`, `tinyint`, `bit`, `float`, `real`, `money`, `smallmoney`, `date`, `datetime`, `smalldatetime`, `uniqueidentifier`, `text`, `ntext`, `xml`, `image`, `sql_variant` | `BigInt`, `Int`, `SmallInt`, `TinyInt`, `Bit`, `Float`, `Real`, `Money`, `SmallMoney`, `Date`, `DateTime`, `SmallDateTime`, `UniqueIdentifier`, `Text`, `NText`, `Xml`, `Image`, `Variant` | none | |

The base name is `SqlServerTypeMap.BaseName(type.Name)`; an alias type's `Name` is already its base's.  A `MethodParameter` whose `Described` is not a `SqlServerType`, or whose base name is not in the table, is bound by `AppendByValue` as today.

- [ ] **Step 1: Write the failing emitter tests** in `SqlServerBindingTests`, through `ParameterBinder.For(DatabaseEngine.SqlServer)` with the `Write` helper of `ParameterBinderTests` (copy it).  One exact-text test, `Append_NullableNVarChar_WritesTypeSizeAndTheOverLongRule`, for `MethodParameter("name", "Name", false, true, Described: TestSidecars.SqlServer("nvarchar(50)", 100, 0, 0))`:

```text
    var p0 = (global::Microsoft.Data.SqlClient.SqlParameter)command.CreateParameter();
    p0.ParameterName = "name";
    p0.SqlDbType = global::System.Data.SqlDbType.NVarChar;
    p0.Size = 50;
    p0.Value = (object?)parameters.Name ?? global::System.DBNull.Value;
    if (p0.Value is string { Length: > 50 })
    {
        p0.Size = -1;
    }
    command.Parameters.Add(p0);
```

and `ShouldContain` theories, one row for each row of the table above: `Append_EachBaseType_SetsItsSqlDbType` (every base name, `SqlDbType` line); `Append_Decimal_SetsPrecisionAndScale` (`decimal(12,4)` → `p0.Precision = 12;` and `p0.Scale = 4;`); `Append_DateTime2OfAnyScale_SetsScale7` (`datetime2(0)`, `datetime2(3)`, `time(0)`, `datetimeoffset(2)`); `Append_VarcharMax_SetsSizeMinusOneAndNoOverLongRule` (`varchar(max)`, `maxLength` -1); `Append_FixedChar_RebindsAnOverLongValueAsVarCharMax` (`char(5)`: the `if` sets `SqlDbType.VarChar` and `Size = -1`; `nchar(5)` with `maxLength` 10 → `Size = 5` and `NVarChar`; `binary(4)` → `is byte[] { Length: > 4 }`, `VarBinary`); `Append_Json_BindsNVarCharMax`; `Append_Timestamp_SetsNoSize`; `Append_NullableVector_SendsCreateNullOfItsDimension` (`vector(3)` → `p0.Value = (object?)parameters.Embedding ?? global::Microsoft.Data.SqlTypes.SqlVector<float>.CreateNull(3);` and no `SqlDbType` line); `Append_AliasType_BindsAsItsBase` (`nvarchar(320)`, `maxLength` 640, `userType` `dbo.Email` → `NVarChar`, `Size = 320`); `Append_ConvertedParameter_BindsTheDatabaseTypeAndChecksTheConvertedValue` (a `Conversion` to `string` on `nvarchar(320)`: the `Value` line is 8.2's converted one and the `if` tests `p0.Value`); `Append_NoDescribedType_BindsByValue` (8.1's four lines exactly).

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test --project tests/SqlSource.Tests --filter-class '*SqlServerBindingTests'`
Expected: FAIL (`MethodParameter` has no `Described`).

- [ ] **Step 3: Carry the type and write the binding.**  `ModelReader.ReadParameters` sets `Described` to the entry parameter's `Type`; `MethodPlanner.TryReadInput` copies `property.Described`.  Extract `AppendValue` from `AppendByValue`.  `SqlServerBinding.Append` writes the cast, the name, the facets from the table, the value through `AppendValue` (null text `CreateNull(n)` for `vector`, else `DBNull.Value`), the over-long `if` for a bounded variable or fixed length type, and `Parameters.Add`.  Update `ParameterBinderTests.For_EachEngine_WritesTheBindingByValue` and the other two-engine loops so the SQL Server rows give no `Described` (still by value), and keep the PostgreSQL rows as they are.

- [ ] **Step 4: Compile against SqlClient 6.1 and 7.**  Extend `MethodCompileTests.Run_QueriesOfEveryShapeOnSqlClient_Compile` with an insert query `SaveEverything` whose parameters are one of each base name of the table (not null and nullable alternating, `vector(3)` both, an alias `dbo.Email` over `nvarchar(320)`), described by `TestSidecars.SqlServer(...)`, and assert `run.ShouldBeClean()` and that the file contains `SqlDbType.DateTime2`, `CreateNull(3)` and `p.Precision`-style lines (use the local's real name).

- [ ] **Step 5: Run the generator tests, the RoslynFloor project and the caching tests**

Run: `dotnet test --project tests/SqlSource.Tests` and `dotnet test --project tests/SqlSource.Tests.RoslynFloor`
Expected: PASS.  A caching test that counts the models step's reasons may now see `Modified` on an edit of a parameter's facets only: that is correct (the binding depends on it); change its expectation and say why in a comment.

- [ ] **Step 6: Validate and commit**

Run `./pre-commit-validation.sh` (expected: all steps passed; the end-to-end projects still bind by `Models`, so nothing there changes yet).

```bash
git add -A && git commit -m "Bind each SQL Server parameter by its described type"
```

### Task 2: The end-to-end project under CodeGen, its existing queries through their methods

**Files:**
- Modify: `tests/SqlSource.Tests.SqlServer/SqlSource.Tests.SqlServer.csproj` (`SqlSourceOutput` → `CodeGen`; its comment)
- Create: `tests/SqlSource.Tests.SqlServer/EndToEnd/DapperQueries.cs` (the second, `Output = GeneratorOutput.Models` claims of `Queries/AllTypes.sql`, `Queries/NewTypes.sql` and every file `DapperRecipeTests` reads, each in its own namespace or a class whose models do not collide, `SqlLocation.Direct`)
- Modify: `EndToEnd/AllTypesQueryTests.cs`, `NewTypesQueryTests.cs`, `DapperRecipeTests.cs` (read the `Models` claims' constants); `AccountQueryTests.cs`, `InvoiceQueryTests.cs`, `ShapeQueryTests.cs`, `WriteQueryTests.cs` (call the methods instead of Dapper); `ModelShapeTests.cs` if a claim's model moves
- Leave: `NoteQueries.cs` at `Models` (Task 5); `ConstantQueries` at `Sql`; `Realistic/` classes at `Models` (8.5)

**Interfaces:**
- Produces: the generated methods `AccountQueries.GetAccount(...)` etc. as `DbConnection` extension methods on `AccountQueriesExtensions` (the default location), `ShapeQueries.SearchAccounts(connection, ...)` (the class sets `MethodLocation.Public`), and the `Models` claim classes later tasks' Dapper tests read.

- [ ] **Step 1:** Switch the default; build; list every error (constants that no longer exist) and give each Dapper test the `Models` claim it needs or turn it into a method test.  A method test asserts the same rows and values as the Dapper test it replaces, `NULL`s included, and each write gives its rolled-back transaction as `transaction:`.
- [ ] **Step 2:** Run `dotnet test --project tests/SqlSource.Tests.SqlServer` on the default image and on the floor image (Global Constraints).  Expected: PASS on both; `NewTypes` skips on 2017.
- [ ] **Step 3:** `./pre-commit-validation.sh`, then commit `Run the SQL Server end-to-end queries through their methods`.

### Task 3: The new queries of the shapes, collections, locations, tokens and counts

**Files:**
- Create: `tests/SqlSource.Tests.SqlServer/EndToEnd/Queries/Methods.sql` (and more files as the claims need: a file is claimed by one class per setting under test), their sidecars from `tools/describe-end-to-end.sh`
- Create: the claiming classes and `EndToEnd/MethodShapeTests.cs`, `EndToEnd/MethodCollectionTests.cs`, `EndToEnd/MethodLocationTests.cs`, `EndToEnd/RowCountTests.cs` (names are the builder's)
- Modify: `tests/SqlSource.Tests.SqlServer/SqlSource.Tests.SqlServer.csproj` (metadata for the collection type by metadata)

Queries and the test each needs (all over the seed of `app`):

- A query with no rows (`UPDATE dbo.accounts SET status = status WHERE id = @accountId`): its default `rowcount` returns 1; `-> none` runs it.
- `-> one` with one row, no row (`UnexpectedRowCountException`) and two rows (the same); `-> one-optional` with one, none (`null`) and two (throws).
- `many` read into each of the ten `CollectionKind`s, the collection type set by the attribute, by the project property, by item metadata and by the `-- collection-type:` marker, each level at least once; each test asserts the runtime collection type (`ImmutableArray<T>`, `List<T>` behind `IList`, the array behind `IReadOnlyList`, ...) and the rows.
- Each `MethodLocation`, `ExtensionClass`, `Public`, `Internal` and `Private` (called through a public wrapper on the type), and `async-method-suffix` (`ListAccountsAsync`).
- A `CodeGen` query with a token and its default, called with the default and with another fragment, and with a declared parameter the other fragment does not use (SQL Server accepts it; the spike, question 5).
- `INSERT ... OUTPUT INSERTED.id` as `rowcount` (2 rows → 2); `rowcount` of a `SELECT` (`-1`); `SET NOCOUNT ON; UPDATE ...` as `rowcount` (`-1`), and a `rowcount` method called after it on the same open connection (`-1`), then on a new connection (the count); a batch of two statements (`UPDATE ...; SELECT ...`) as `rowcount` (the update's count) and as `one` (the `SELECT`'s row, the first result set); `MERGE ... OUTPUT` through `WriteQueries.MergeAccountStatus` (Task 2 already) and as `rowcount`.
- `WAITFOR DELAY '00:00:05'; SELECT 1 AS n` (or the builder's equivalent), used by Task 4.

- [ ] **Step 1:** Write the queries and the classes; run `tools/describe-end-to-end.sh`; read the diff (only new entries; no existing entry changed; PostgreSQL sidecars byte-identical).  A parameter that 2017 and 2025 type differently gets `-- param:`.
- [ ] **Step 2:** Write the tests; run on the default and the floor image; `CheckTests` passes on both.
- [ ] **Step 3:** `./pre-commit-validation.sh`, commit `Prove the shapes, collections and locations on SQL Server`.

### Task 4: The run-time arguments and the connection rule

**Files:**
- Create: `tests/SqlSource.Tests.SqlServer/EndToEnd/RunTimeArgumentTests.cs`, and `EndToEnd/Types/CancelOnRead.cs` (a test type with a `[SqlSourceConverter]` pair whose `FromDatabase` cancels a static `CancellationTokenSource` the test sets), with the query that reads it by a `-- type-override:`

Tests, each through a generated method:

- A method given a transaction: its write is undone by the rollback (read on another connection).
- A method called on a connection in a transaction and not given it: `InvalidOperationException` whose message starts `ExecuteReader requires the command to have a transaction` or `BeginExecute...` as SqlClient words it (assert a stable substring, `requires the command to have a transaction`), and the `ExecuteNonQuery` form for a `rowcount` method.
- `commandTimeout: 1` on the `WAITFOR` query: `SqlException` with `Number` -2.
- A token cancelled while the command runs (`CancelAfter` during the `WAITFOR`): `SqlException` whose message contains `Operation cancelled by user`.
- A token cancelled before the call: `TaskCanceledException` (`Should.ThrowAsync<TaskCanceledException>`).
- A token cancelled during the read, by `CancelOnRead` on the first row of a `many` query of several rows: `TaskCanceledException`.
- The connection rule: a closed connection is closed after the method, after its exception (the `one` of two rows) and after its cancellation; an open connection is left open after each; an open connection with a transaction is left open, the transaction still usable (`@@TRANCOUNT` 1).

- [ ] **Step 1:** Write the tests and the type; run on both images (the exceptions are the same on both, the spike found).
- [ ] **Step 2:** `./pre-commit-validation.sh`, commit `Prove the run-time arguments and the connection rule on SQL Server`.

### Task 5: Notes through converters, and the id type

**Files:**
- Modify: `EndToEnd/NoteQueries.cs` (`CodeGen`; drop the comment about 8.3 and 8.4), `EndToEnd/NoteQueryTests.cs` (method tests), `EndToEnd/Types/Email.cs` (`[SqlSourceConverter]` `FromDatabase(string)` and `ToDatabase(Email)`), `EndToEnd/Queries/Notes.sql` (a query with an `Email` parameter, e.g. `FindAccountByEmail ... WHERE email = @email`, which SQL Server describes as `dbo.Email` already)
- Modify: `EndToEnd/app/schema.sql`, `EndToEnd/app/seed.sql`: `CREATE TYPE dbo.ExternalId FROM bigint NOT NULL;` and a new table `dbo.external_refs (id int PRIMARY KEY, account_id int NOT NULL REFERENCES dbo.accounts (id), external_id dbo.ExternalId NOT NULL)`, two seeded rows; no existing table changes
- Create: `EndToEnd/Types/ExternalId.cs` (`readonly record struct ExternalId(long Value)` with converters), `EndToEnd/Queries/ExternalRefs.sql` (`-- type-override: dbo.ExternalId = SqlSource.Tests.SqlServer.EndToEnd.Types.ExternalId`; a query reading `external_id` and filtering by `@externalId`, and an insert of one), its claim and `EndToEnd/ExternalRefQueryTests.cs`
- Keep: `EmailHandler` (Dapper's recipe still uses it)

Tests: every `Notes.sql` query through its method with 6.5's assertions; `Email` read through `FromDatabase` and bound through `ToDatabase`; an `Email` of 321 characters inserted into `accounts` through a method is refused by the server (2628 on 2025, 8152 on 2017), not cut; `ExternalId` read and matched as a column and a parameter, its insert rolled back.

- [ ] **Step 1:** Schema, types, queries; `tools/describe-end-to-end.sh`; read the diff (existing entries unchanged).
- [ ] **Step 2:** Tests; both images.
- [ ] **Step 3:** `./pre-commit-validation.sh`, commit `Read and write notes and ids through converters on SQL Server`.

### Task 6: Every type written and read, on SqlClient 7 and 6.1

**Files:**
- Modify: `EndToEnd/Queries/AllTypes.sql`: `WriteAllTypes` (`INSERT INTO dbo.all_types (...every column but rowversion_value...) OUTPUT INSERTED.* VALUES (...)`), `FindByRowversion` (`SELECT id FROM dbo.all_types WHERE rowversion_value = @rowversion`), `FindByDatetime2` (`WHERE datetime2_value = @value`), `FindByChar` (`WHERE char_value = @value`), `CountTinyintLevels` (`-- type-override: tinyint = SqlSource.Tests.SqlServer.EndToEnd.Types.Level`, `SELECT tinyint_value FROM dbo.all_types WHERE tinyint_value = @level`), and the column-specific inserts the facet tests need (a `datetime2(3)` and a `decimal(12,4)` column already exist or come from a temp table in the query); leave `InsertAllTypes`, its Dapper test and `WriteSideLeftOut` as they are
- Modify: `EndToEnd/Queries/NewTypes.sql`: `WriteNewTypes` (insert `json_value`, `vector_value`, `OUTPUT INSERTED.*`) and `GetNewType` reading one row, for the vector null check
- Create: `EndToEnd/MethodAllTypesTests.cs`, `EndToEnd/Types/Level.cs` (`enum Level : byte`)
- Modify: `tests/SqlSource.Testing/EndToEnd/SqlServerAllTypesRows.cs` only if a shared assertion is needed (keep the change local)
- Modify: `tests/SqlSource.Tests.SqlServer.SqlClient61/AllTypesQueries.cs` (a `CodeGen` claim of `AllTypes.sql` and `NewTypes.sql` beside the `Models` ones), `AllTypesQueryTests.cs` or a new `MethodAllTypesTests.cs` there (write and read every type through the methods on 6.1)

Tests (main project; the floor project repeats the first three):

- `WriteAllTypes` with row 1's values, and with every column NULL: the row read back through the method equals `SqlServerAllTypesRows`'s full row and null row (rowversion compared by length), in a rolled-back transaction.  The values include a `decimal` that a value-inferred binding would declare with less precision and `datetime2` `.1234567`, which `datetime` would lose.
- `FindByRowversion` with row 1's rowversion finds row 1.
- `WriteNewTypes` on 2025 (`SkipBelow(17, ...)`) with the vector `[1.5,-2.25,0.5]` and its NULL; a not-null vector read of a NULL (`SELECT CAST(NULL AS vector(3)) AS v` described not null by a `-- column: v not null` marker, or the builder's equivalent) throws `InvalidCastException` with message `Column 'v' is null.`
- Facets: a string and a `byte[]` one longer than their column (`varchar(20)`, `varbinary(8)`), inserted through the method, are refused by the server (2628 / 8152); `"abcdef"` for `char(5)` in `FindByChar` matches no row holding `"abcde"`; a `datetime2(3)` written with `.1234567` reads back `.1230000`, as the server rounds; a `decimal(12,4)` given `1.23456m` reads back `1.2346m` and a `WHERE` with `1.23456m` matches it (owner's decision); a `decimal(12,4)` given `123456789.1m` throws `ArgumentException` before sending (spike: `out of range`).
- `CountTinyintLevels` binds `Level.High` (`255`) and reads it back as `Level`.

- [ ] **Step 1:** Queries; `tools/describe-end-to-end.sh` (diff read; `NewTypes.sql` 2025 alone).
- [ ] **Step 2:** Tests in both projects; main project on both images; `dotnet test --project tests/SqlSource.Tests.SqlServer.SqlClient61` on the default image.
- [ ] **Step 3:** `./pre-commit-validation.sh`, commit `Write and read every SQL Server type through the methods`.

### Task 7: The allocation test

**Files:**
- Create: `tests/SqlSource.Tests.SqlServer/EndToEnd/AllocationTests.cs`, with a collection of its own, `[CollectionDefinition(..., DisableParallelization = true)]`, whose fixture starts its own `SqlServerContainer` (as `UsesSqlServer` does) and builds `app`
- Uses: a query with two parameters and three rows (e.g. `ShapeQueries.SearchAccounts` or a new one), and a hand-written method in the test: `CreateCommand`, `CommandText`, `Transaction`, `CommandTimeout`, `(SqlParameter)CreateParameter()` with the same `SqlDbType`, `Size` and `Value`, `ExecuteReaderAsync(CommandBehavior.Default, token)`, the same `GetFieldValue<T>` reads into the same model, the open/close rule.

- [ ] **Step 1:** Warm both up (e.g. 50 calls), then measure `GC.GetTotalAllocatedBytes(true)` over N calls of each (e.g. 500), alternating blocks; assert generated bytes per call `<=` hand-written bytes per call plus a margin (e.g. 5% or 256 bytes, whichever is larger), each written in the test with why.  It must fail if the generated method is replaced by one that, say, builds a `List<T>` and copies it: check by a temporary local edit, not committed.
- [ ] **Step 2:** Run on both images; `./pre-commit-validation.sh`; commit `Measure the SQL Server methods' allocations against hand-written ADO.NET`.

### Task 8: The documents, and the row set to Done

**Files:**
- `docs/manual/sqlserver.md`: Query methods section from what was built (binding by type and facets, the over-long and scale-7 rules, the `decimal` rounding and range error, `json` as `nvarchar(max)`, `vector`; the transaction SqlClient requires; `rowcount` `-1` and `NOCOUNT` for the session; a batch's first result set; the exceptions of a cancelled method; an enum read only from its own underlying type; the `datetime2(n)` `WHERE` note); remove the "Each parameter is sent by its value" bullet and its callout; fix line 207's sentence.
- `docs/manual/generator.md`: remove the SQL Server known issue of binding by value; add the wrapping connection's known issue for SqlClient's `SqlParameter` (or extend 8.3's if it merged first); the "Nothing but the value is set" bullet and callout: SQL Server's part only, PostgreSQL's left for 8.3.
- `docs/manual/README.md`: the row of query methods, SQL Server delivered (PostgreSQL left as 8.3 leaves it).
- `docs/manual/recipes.md`: unchanged unless a test found otherwise.
- `docs/architecture/generator.md`: the SQL Server binding step, in the "Binding a parameter" paragraph, plus `ModelProperty.Described`/`MethodParameter.Described`.
- `tests/SqlSource.Tests.SqlServer/README.md`, `tests/README.md`, `tests/AGENTS.md`: the project's claims (default `CodeGen`, the `Models` claims), the method tests, the allocation collection, what the SqlClient 6.1 project now proves; the `NoteQueries` rule.
- `CONTRIBUTING.md`: what the SqlClient 6.1 project proves.
- `docs/deferred/D-0006-*`: only if the batch tests change what it says.
- The epic outline: row 8.4 `Done`, with what was built.

- [ ] **Step 1:** Write them; `UserDocsLinksTests`/`UserDocsSamplesTests` pass.
- [ ] **Step 2:** `./pre-commit-validation.sh`, commit `Document the SQL Server methods; 8.4 done`.
