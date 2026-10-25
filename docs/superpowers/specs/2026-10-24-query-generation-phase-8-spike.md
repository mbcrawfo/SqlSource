# The spike of phase 8, the drivers - appendix

Date: 2026-10-24

Appendix to phase 8 of the [query generation epic](2026-10-07-query-generation-epic-design.md) and its sub-phases' specs, written by [sub-phase 8.0](2026-10-24-query-generation-phase-8-0-before-the-methods-design.md).  Before any generator code of the methods, two throwaway console programs ran the calls the generated methods will make against both engines, on each engine's floor and default image, with each engine's floor and newest driver, to settle the questions 8.0's spec lists.  The programs were not kept; what they ran and what they answered are here.  Each answer is what the code did; where the combinations differ, the section says which does what.

## What ran

| | Version |
|----|----|
| PostgreSQL, default | `postgres:18.6`, `SHOW server_version` = `18.6 (Debian 18.6-1.pgdg13+2)` |
| PostgreSQL, floor | `postgres:14.24`, `14.24 (Debian 14.24-1.pgdg13+2)` |
| SQL Server, default | `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04`, `ProductVersion` `17.0.5015.4` (RTM-CU9-GDR) |
| SQL Server, floor | `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`, `14.0.3560.2` (RTM-CU31-GDR) |
| Npgsql | `10.0.3` and `8.0.9` |
| Microsoft.Data.SqlClient | `7.1.1` (assembly `7.0.0.0`) and `6.1.7` (assembly `6.0.0.0`) |
| Host | .NET 10.0.12, `net10.0`, Apple silicon, Docker 29.4; SQL Server under emulation |

Each engine's probes were one source file linked into two console projects, one per driver version, outside the repository, so that no central package version applied; each project ran against both of its engine's containers, eight runs in all.  Every probe caught its own exception and printed the call, the value with its runtime type, or the exception's type and message.  The connections were `NpgsqlDataSource.Create(...).CreateConnection()` and `new SqlConnection(...)`, each used as a `DbConnection`; commands came from `DbConnection.CreateCommand()` and parameters from `DbCommand.CreateParameter()`, as generated code will make them, unless a question names the provider's own type.  The `MapEnum` probes used a data source built by `NpgsqlDataSourceBuilder.MapEnum<T>()`.

What a parameter was sent as was read on the server: on PostgreSQL by `pg_typeof(@p)` and `current_query()`, and by `pg_prepared_statements` after `PrepareAsync`; on SQL Server by the declaration `sp_executesql` carried, read from `sys.dm_exec_cached_plans` joined to `sys.dm_exec_sql_text`, and by `SQL_VARIANT_PROPERTY(CAST(@p AS sql_variant), 'BaseType' | 'MaxLength' | 'Precision' | 'Scale')`.

**The two versions of each driver agreed on every probe but three.**  Npgsql 8 cannot bind an `IPNetwork` by value (Q7), has no `NpgsqlCube` (Q7), and words one error otherwise (Q7); its allocations differ by a few bytes too (Q8).  The two SqlClients gave identical results throughout.  The servers differ only where a type or statement is missing from the older one: `MERGE` on PostgreSQL 14, `json` and `vector` on SQL Server 2017, and the wording of SQL Server's truncation error.  So each answer below holds on every combination of its engine unless it says otherwise.

## 1. `GetFieldValue<T>` on a `NULL`, with no `IsDBNull`

The call: a query returning `NULL` of a type, `ReadAsync`, then `reader.GetFieldValue<T>(0)`; a second run of the same text with `IsDBNull(0)` showed the column held `NULL`, and the same `T` on a row with a value returned it.

**PostgreSQL: every `T` throws** `System.InvalidCastException: Column '<name>' is null.`, where the name is the column's, its alias when it has one.  Tried: `int`, `long`, `short`, `bool`, `decimal`, `float`, `double`, `string` (of `text` and of `jsonb`), `byte[]`, `int[]`, `string[]`, `Guid`, `DateTime` (`timestamptz` and `timestamp`), `DateOnly`, `TimeOnly`, `TimeSpan`, `DateTimeOffset` (`timestamptz`, `timetz`), `char`, `IPAddress`, `JsonDocument`, `Dictionary<string, string?>` (`hstore`), `NpgsqlRange<int>`, `NpgsqlRange<int>[]` (a multirange), a C# enum after `MapEnum<T>()`, a `string` of an unmapped enum, and `int` of a domain over `integer`.  The reader stays usable on the row after the throw.

**SQL Server: every `T` but one throws** `System.Data.SqlTypes.SqlNullValueException: Data is Null. This method or property cannot be called on Null values.`, from `GetFieldValue<T>` and `GetFieldValueAsync<T>` alike.  Tried: `int`, `long`, `short`, `byte`, `bool`, `decimal`, `float`, `double`, `string` (of `nvarchar`, `xml` and, on 2025, `json`), `byte[]`, `Guid`, `DateTime` (`datetime2`, `datetime`, `date`), `DateOnly`, `TimeOnly`, `TimeSpan`, `DateTimeOffset`, and a C# enum over `int`.

**The exception is `vector`.**  On 2025, `GetFieldValue<SqlVector<float>>` on a `NULL` `vector(3)` returns `SqlVector<float>.Null`, `IsNull` true and `Length` 3, with no throw, sync and async, on 6.1.7 and 7.1.1.  So the driver's throw that the epic counts on as an oracle does not reach a `vector` column the sidecar wrongly calls not null.  How a not-null `vector` column is read is a question to the owner, since it touches the outline's Decided read.

On both engines `GetFieldValue<object>` on a `NULL` returns `DBNull.Value`; no generated read asks for `object`.

## 2. `GetFieldValue<T?>` of a nullable value type

**PostgreSQL accepts it:** `int?`, `long?`, `bool?`, `Guid?`, `DateTime?`, `DateOnly?`, `NpgsqlRange<int>?` and a mapped enum's `T?` return `null` on a `NULL` and the value otherwise.

**SQL Server refuses it.**  Every `T?` throws `SqlNullValueException` on a `NULL`, as `T` does.  On a value, `int?`, `long?`, `bool?`, `decimal?`, `Guid?`, `DateTime?`, `TimeSpan?` and `DateTimeOffset?` return it, but `DateOnly?`, `TimeOnly?` and an enum's `T?` throw `InvalidCastException: Unable to cast object of type 'System.DateTime' to type 'System.Nullable`1[System.DateOnly]'.` and the like: SqlClient's conversions to `DateOnly`, `TimeOnly` and an enum apply to `T`, not to `T?`.

So a nullable column is read as `reader.IsDBNull(i) ? null : reader.GetFieldValue<T>(i)` on both engines; `GetFieldValue<T?>` is not used.

## 3. An array that holds a `NULL`

PostgreSQL alone.  `ArrayNullabilityMode` defaults to `Never` on both drivers.

| Call, on `ARRAY[1,NULL,3]` | Result |
|----|----|
| `GetFieldValue<int?[]>` | `[1, null, 3]` |
| `GetFieldValue<int[]>` | throws `System.InvalidCastException: Cannot read a non-nullable collection of elements because the returned array contains nulls. Call GetFieldValue with a nullable collection type instead.` |
| `GetFieldValue<string[]>`, on `ARRAY['a',NULL]` | `["a", null]` |
| `GetFieldValue<List<int?>>` | `[1, null, 3]` |

The throw names no column, unlike question 1's.

## 4. Parameter names

The call: the text `SELECT @name`, one parameter with `ParameterName` `name`, `@name`, `NAME` and `@NAME` in turn.

- **Both drivers match every form**, with or without the `@` and ignoring case, on a plain connection and on a command with `Transaction` set.  `ParameterName` reads back as it was set.
- **Npgsql rewrites `@name` to `$1` by default** on a command made by `CreateCommand`, with or without a transaction: `current_query()` returned `SELECT $1::int AS v, ...`.  Npgsql also runs a command whose transaction is open but not set on it.
- **A placeholder with no parameter** is sent as written by Npgsql, and the server reads `@name` as the prefix operator `@` on a column: `Npgsql.PostgresException: 42703: column "name" does not exist`.  SqlClient's server says `Must declare the scalar variable "@name".` [137].
- **One parameter binds every placeholder its name matches ignoring case**: a parameter `id`, `Id` or `@ID` with the text `SELECT @Id::int AS a, @id::int AS b` gave 7 to both, and the server received `SELECT $1::int AS a, $1::int AS b`.
- **Two parameters whose names differ only in case**: Npgsql binds the placeholder to the first added and reports nothing; SQL Server refuses the batch, `The variable name '@A' has already been declared.` [134].  SqlSource's lexer makes `@Id` and `@id` one parameter, so a generated method never adds two.
- **SQL Server matches the text's names ignoring case** under the instance's collation, also in a database created `Latin1_General_CS_AS`; `SELECT @Name + @NAME` with one parameter `@name` returns 2.  `SqlParameterCollection` itself is exact: `Contains("name")` is false for a parameter added as `@name`.
- **SqlClient requires the transaction on the command.**  `CreateCommand()` never sets `Transaction`; a command on a connection with a pending local transaction and no `Transaction` throws `InvalidOperationException: BeginExecuteReader requires the command to have a transaction when the connection assigned to the command is in a pending local transaction.  The Transaction property of the command has not been initialized.` before anything is sent (`BeginExecuteNonQuery` for `ExecuteNonQueryAsync`).  Another connection's transaction throws `The transaction is either not associated with the current connection or has been completed.`

## 5. A declared parameter that the text does not use

**Both accept it.**  Npgsql does not send it: the server received only `$1` for `SELECT @a` with `a` and an unused `b`, `pg_prepared_statements` listed one parameter type, and an unused parameter that Npgsql could not have written (`new object()`, an untyped `DBNull.Value`) was never bound.  SqlClient sends it: `SELECT @a` with an unused `@b` was declared `(@a int,@b int)`, and accepted.  On SQL Server a parameter whose `Value` is a C# `null`, used or not, is not sent at all, and the server refuses the batch: `The parameterized query '(@a int,@b nvarchar(4000))SELECT @a' expects the parameter '@b', which was not supplied.` [8178].  A `NULL` is bound as `DBNull.Value` on both.

## 6. `ExecuteNonQuery`'s count

| Text | PostgreSQL | SQL Server |
|----|----|----|
| `SELECT` of 3 rows | `-1` | `-1` |
| `INSERT ... RETURNING` / `OUTPUT inserted.id` of 2 rows | `2` | `2` |
| `UPDATE` of no rows | `0` | `0` |
| Two `UPDATE`s of 2 and 3 rows in one text | `5`, the sum | `5`, the sum |
| `UPDATE` of 2 rows and a `SELECT`, in either order | `2` | `2` |
| `WITH u AS (UPDATE ... RETURNING id) SELECT id FROM u` | `-1`, counted as a `SELECT` | |
| `SET NOCOUNT ON; UPDATE` of 2 rows | | `-1` |
| `SET NOCOUNT ON; UPDATE (2); SET NOCOUNT OFF; UPDATE (3)` | | `3` |
| `MERGE` of 2 rows, with or without `OUTPUT` | `2` on 18; a syntax error on 14 | `2` |

**`SET NOCOUNT ON` outlives its command.**  It is session state: on the same open connection, later commands of SqlSource or any other code return `-1` until the connection is closed and the pool resets it.  So a `rowcount` method called on a connection the caller opened returns `-1` after anything on it has set `NOCOUNT`.

## 7. Binding by value alone

The call: `CreateParameter()` with `Value` and no `DbType`, in `INSERT INTO t (c) VALUES (@p)`, in `SELECT count(*) FROM t WHERE c = @p` and, to see what was sent, in `pg_typeof(@p)` or the SQL Server declaration; for each C# type of each engine's base map, both drivers' types where the map's differ.

### PostgreSQL

| Outcome | Types |
|----|----|
| Insert and `WHERE` both work | `bool`, `short`, `int`, `long`, `float`, `double`, `decimal` (`numeric`), `string` into `text`, `varchar`, `bpchar`, `name` and `citext`, `char` (`"char"`, sent as `text`), `byte[]`, `Guid`, `DateOnly`, `TimeOnly`, `TimeSpan` (`time`, `interval`), `DateTime` (`timestamp`, `timestamptz`, and `date`, sent as `timestamp`), `IPAddress`, `NpgsqlCidr`, `IPNetwork` on Npgsql 10, a 6-byte `PhysicalAddress` into `macaddr`, `NpgsqlLine`, `NpgsqlLSeg`, `NpgsqlBox`, `NpgsqlPath`, `NpgsqlCircle`, `NpgsqlTsVector`, `NpgsqlTsQuery`, `hstore`, `NpgsqlCube` on Npgsql 10, every range and multirange whose subtype matches, `BitArray` into `bit(n)` and `varbit`, an `int` into a domain over `integer`, and arrays of these (`int[]`, `int?[]`, `string[]` into `text[]`, `Guid[]`, `DateTime[]` into `timestamptz[]`, `DateOnly[]` into `date[]`); a C# enum on a `MapEnum` data source |
| Insert works, `WHERE` fails | `decimal` into `money` (`42883: operator does not exist: money = numeric`), `DateTimeOffset` into `timetz` (sent as `timestamptz`), `DateTime[]` into `date[]` (sent as `timestamp[]`) |
| The server refuses both, the parameter sent as `text` or another type | `string` into `json`, `jsonb`, `jsonpath`, `xml`, `refcursor`, `ltree`, `lquery`, `ltxtquery`, an enum, an enum's array and `jsonb[]` (`42804: column "c" is of type jsonb but expression is of type text`); `bool` into `bit(1)`; `short[]` into `int2vector`; `NpgsqlRange<DateTime>` into `daterange` and its multirange, Npgsql 8's types for them |
| Npgsql refuses before sending | `uint` (`oid`, `xid`, `cid`, `regtype`, `regconfig`), `ulong` (`xid8`), `uint[]` (`oidvector`), `NpgsqlTid`, `NpgsqlLogSequenceNumber`: `InvalidCastException: Writing values of 'System.UInt32' is not supported for parameters having no NpgsqlDbType or DataTypeName.`; `IPNetwork` on Npgsql 8, the same; an 8-byte `PhysicalAddress` into `macaddr8`: `ArgumentException: A macaddr value must be 6 bytes long.`; a `DateTimeOffset` whose offset is not zero: `ArgumentException: Cannot write DateTimeOffset with Offset=02:00:00 to PostgreSQL type 'timestamp with time zone', only offset 0 (UTC) is supported.` |

**Setting `DataTypeName` mends every row of the last three**, as question 8 shows.  `point`, `polygon`, `json`, `jsonpath`, `xml`, `refcursor`, `lquery` and `ltxtquery` have no `=` in PostgreSQL, so a `WHERE c = @p` on them fails however they are bound.

Two more by value:

- **A `DateTime` is sent by its `Kind`**: `timestamptz` for `Utc`, `timestamp` otherwise, and the server converts between the two in the session's `TimeZone`.  Under `Timezone=America/New_York`, a `Utc` `DateTime` of `03:00` into a `date` column was stored as the day before and its `WHERE` found no row; an `Unspecified` one into `timestamptz` was read as local time.  Named, Npgsql refuses the mismatch instead: `Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time zone', only UTC is supported.` and `Cannot write DateTime with Kind=UTC to PostgreSQL type 'timestamp without time zone', consider using 'timestamp with time zone'.`
- **An untyped `DBNull.Value`** is accepted by an `INSERT` and by `WHERE c = @p`, where it matches nothing, but where the server must infer its type, `@p IS NULL` or `pg_typeof(@p)`, it fails: `42P18: could not determine data type of parameter $1`.  Named, it works.  A C# `null` `Value` throws before sending: `InvalidOperationException: Parameter 'p' must have either its DbType, NpgsqlDbType, DataTypeName or its Value set.` on Npgsql 10, `... either its NpgsqlDbType or its DataTypeName or its Value set.` on 8.

### SQL Server

| Column | Sent | Insert | `WHERE c = @p` |
|----|----|----|----|
| `bigint`, `int`, `smallint`, `tinyint`, `bit`, `float`, `real`, `uniqueidentifier` | as the column | works | works |
| `decimal(12,4)` ← `1234.5678m` | `decimal(8,4)`, from the value | works | works |
| `money`, `smallmoney`, `numeric` | `decimal(p,s)` from the value | works | works |
| `date`, `datetime`, `smalldatetime` ← `DateTime` | `datetime` | works | works |
| `datetime2(7)` ← `DateTime` `.1234567` | **`datetime`** | stored `.1233333` | **no row** |
| `time` ← `TimeSpan`; `datetimeoffset` | `time(7)`; `datetimeoffset(7)` | exact | works |
| `char`, `varchar`, `nchar`, `nvarchar`, `sysname` ← `string` | `nvarchar(<length of the value>)` | works | works |
| `text`, `ntext`, `xml`; `json` on 2025 | `nvarchar(<length>)` | works | refused by the server for any binding (402) |
| `binary`, `varbinary` ← `byte[]` | `varbinary(<length>)` | works | works |
| `image` | `varbinary(<length>)` | works | refused for any binding (402) |
| `rowversion` ← its own `byte[8]` | `varbinary(8)` | refused for any binding (273) | works |
| `sql_variant` ← `(object)42` | `int` | works | works |
| `vector(3)` ← `SqlVector<float>` on 2025 | `vector(3)`, `SqlDbType` inferred as the vector type | works | refused for any binding (8117) |

Also by value: `DateOnly` is sent as `date`, `TimeOnly` as `time(7)`, an enum as its underlying type, `char` as `nvarchar(1)`; `float[]` throws `ArgumentException: No mapping exists from object type System.Single[] to a known managed provider native type.`

- **A string, a `byte[]` and a `decimal` are declared by the value**: `nvarchar(2)` for `"ab"`, `nvarchar(4000)` for 4,000 characters, `nvarchar(max)` for 4,001; `varbinary(n)` up to 8,000 bytes; `decimal(2,1)` for `1.5m` and `decimal(20,6)` for `12345678901234.123456m`.  A `DateTime` is always `datetime`.
- **`DBNull.Value` is declared `nvarchar(4000)`**, whatever the column.  Its insert works into every column above but `binary`, `varbinary` (`Implicit conversion from data type nvarchar to binary is not allowed.` [257]) and `image` (`Operand type clash: nvarchar is incompatible with image` [206]).

## 8. Npgsql's `DataTypeName`

The call: `(NpgsqlParameter)command.CreateParameter()` with `DataTypeName` and `Value`, and `new NpgsqlParameter<T> { DataTypeName = ..., TypedValue = ... }`, each in `SELECT pg_typeof(@p), @p::text` and in an `INSERT` into a column of the type.

**Both forms are accepted for every case on every combination**, and `DataTypeName` reads back as set: `integer[]` with an `int[]`; an enum's name, plain or schema-qualified, with a `string`, on a plain data source and on a `MapEnum` one, and the enum's array with a `string[]`; `jsonb` with a `string`; `int4range` with an `NpgsqlRange<int>`; and a domain over `integer`, plain or schema-qualified.  Every type question 7 found refused by value was accepted named: `money`, `timetz` at any offset, `jsonb`, `macaddr8`, `oid`, `xid`, `cid`, `regtype`, `regconfig`, `xid8`, `tid`, `pg_lsn`, `int2vector`, `oidvector`, `ltree`, `daterange` and `datemultirange` of `DateTime`, `bit`, `date[]` of `DateTime`, `jsonb[]` and an enum's array.

- **A domain's name is accepted, and Npgsql sends its base type**: `pg_typeof(@p)` is `integer`, and a value the domain's `CHECK` refuses is accepted until it lands in a column of the domain.
- **`NpgsqlParameter<T>` with `DataTypeName` does not box a value type.**  Measured on one thread with `GC.GetAllocatedBytesForCurrentThread()`, after a warm-up, as the median of seven runs of 20,000: setting `TypedValue` of an `NpgsqlParameter<int>` allocates 0 bytes, setting `Value` of an `NpgsqlParameter` 24, one boxed `int`; an execution of `SELECT @p` costs 296 bytes with the generic parameter and 320 with `Value`, with or without `DataTypeName`.  A new parameter for each call costs 600 bytes generic and 624 not.  `NpgsqlParameter<int?>` does not box either: 0 bytes to set, 296 to execute.
- **A `NULL` through `NpgsqlParameter<T>` is `TypedValue = null`**, of an `NpgsqlParameter<int?>` or an `NpgsqlParameter<string>`: an insert stores `NULL` and `@p IS NULL` is true, with or without `DataTypeName`, since the type comes from `T`.  **`Value = DBNull.Value` throws in the setter**, before any command runs, on `NpgsqlParameter<int>`, `<int?>` and `<string>`: `InvalidCastException: Unable to cast object of type 'System.DBNull' to type 'System.Int32'.`  So a nullable value type is an `NpgsqlParameter<T?>`, and a reference type's `null` is its own `TypedValue`.

## 9. SqlClient's facets

The call: the `SqlParameter` that `CreateParameter()` returns, with `SqlDbType` and its facets set.

- **`Size` of `NVarChar` and `NChar` is in characters**: `Size` 50 is declared `nvarchar(50)`, 100 bytes, so the description's `maxLength` in bytes is halved; `-1` is `nvarchar(max)`.  `VarChar`, `Char`, `VarBinary` and `Binary` take bytes, `-1` for `max`.
- **A value longer than `Size` is cut by SqlClient, with no error.**  `NVarChar` `Size` 5 with `"abcdefghij"` inserts `"abcde"`, and `WHERE c = @p` with that value matches a row holding `"abcde"`; `VarChar` and `VarBinary` do the same.  By value, the whole value is sent and the server refuses an insert that would truncate: `String or binary data would be truncated in table ..., column 'c'.` [2628] on 2025, `String or binary data would be truncated.` [8152] on 2017.
- **`Decimal` takes both `Precision` and `Scale`.**  `Precision` 12 and `Scale` 4 are declared `decimal(12,4)` and round `1.23456m` to `1.2346`, half away from zero; with only one set, the other comes from the value (`decimal(12,5)`, `decimal(6,4)`).  A value outside the precision throws before sending: `ArgumentException: Parameter value '123456789.1000' is out of range.`  A `DbParameter` with `DbType.Decimal`, `Precision` and `Scale` behaves the same.
- **`DateTime2` with `Scale` 7 round-trips a `DateTime` exactly.**  Without a `Scale` it is `datetime2(7)`.  **`Scale` 0 counts as not set** and is sent as 7, for `DateTime2` and `Time` alike, so a `datetime2(0)` or `time(0)` parameter is declared at 7 and the server rounds on assignment.  A `Scale` between 1 and 6 is declared so, and SqlClient **truncates** the value to it (`.1239999` at 3 is sent as `.123`), where the server's own conversion of a 7-digit value rounds (`.124`).  `Time` with a `TimeOnly` and `Date` with a `DateOnly` are accepted; `Date` with a `DateTime` drops its time.
- **`SqlDbType.Timestamp`** with a row's `rowversion` is declared `timestamp` and matches in a `WHERE`, also in an `UPDATE ... WHERE rv = @p`; an insert of it is refused (273), as by value.
- **`json` as `NVarChar` `Size` -1** is declared `nvarchar(max)` and inserts into a `json` column on 2025, where the server validates it (`JSON text is not properly formatted.` [13609]).  `SqlDbTypeExtensions.Json` exists on 6.1.7 and 7.1.1 and declares `json` on 2025; on 2017 it is a protocol error, `The incoming tabular data stream (TDS) remote procedure call (RPC) protocol stream is incorrect. Parameter 3 ("@p"): Data type 0xF4 is unknown.` [8009].
- **`vector`.**  `SqlDbTypeExtensions.Vector` and `SqlVector<T>` exist on 6.1.7 and 7.1.1.  The value is a `SqlVector<float>`, from `new SqlVector<float>(ReadOnlyMemory<float>)`; **its length gives the dimension, and `Size` is ignored**: `Size` 3, 12 or 20 is declared `vector(3)` for a three-element value and reads back 20 afterwards.  A value of another length is refused by the server, `The vector dimensions 4 and 3 do not match.` [42204]; a `float[]` or a `string` throws `InvalidCastException` before sending.  `SqlDbType = SqlDbTypeExtensions.Vector` does not hold without an `ISqlVector` value: it reads back `NVarChar`, and **`DBNull.Value` is sent as `nvarchar(4000)`**, which an insert into `vector(3)` accepts; `SqlVector<float>.CreateNull(3)` is declared `vector(3)` and inserts a `NULL`.  On 2017 every vector binding is protocol error 8009, `Data type 0xF5 is unknown.`  Read back, `GetFieldValue<SqlVector<float>>` and `GetValue` give the vector, `GetFieldValue<string>` gives `"[1,2,3]"`, `GetFieldValue<float[]>` throws `InvalidCastException`.
- **An enum is read only from its own underlying type.**  `GetFieldValue<MyEnum>` with `MyEnum : int` reads an `int` column, and throws `InvalidCastException: Unable to cast object of type 'System.Byte' to type 'Spike.MyEnum'.` on a `tinyint`, and the like on `smallint` and `bigint`; a `byte` enum reads `tinyint` and not `int`.  There is no widening or narrowing.

## 10. The reader

The call: `ExecuteReaderAsync(CommandBehavior.Default)` on a row with small columns, 1 MB of text and 1 MB of bytes, then `ReadAsync`, then the synchronous `GetFieldValue<T>` of each column, in order and out of it.

- **`ReadAsync` under `Default` buffers the whole row on both drivers**, and the synchronous `GetFieldValue<T>` that follows does no I/O.  Every column read in order, out of order and a second time returned its value.  On SQL Server this was proven by pausing the server's container after `ReadAsync` on a 32 MB row: the synchronous reads completed in 0 ms while the server was paused, where under `SequentialAccess` they blocked until it resumed.  On PostgreSQL the reads took 0 ms; `SequentialAccess` refuses a read out of order on both.
- **`CommandBehavior.SingleRow` makes the second `ReadAsync` return `false`** on both drivers, so `one` and `one-optional`, which read past the first row, must not use it.  On PostgreSQL, an `INSERT ... RETURNING` of two rows under `SingleRow` still inserted both.
- **`CommandBehavior.SingleResult` reads every row of the first result set** and hides the later ones (`NextResultAsync` is `false`).  A statement after the first result set still ran when the reader was disposed without `NextResult`, on both drivers, under `Default` and `SingleResult` (and `SingleRow` on SQL Server): disposing the reader drains the batch.  With an `INSERT` first and a `SELECT` after it, Npgsql's reader stood on the `SELECT`'s rows.  A batch's first result set is the first statement's that has rows: `UPDATE ...; SELECT ...` gives the `SELECT`'s rows.

## 11. The connection rule

- **`OpenAsync` on an open connection throws** `InvalidOperationException`: `Connection already open` on Npgsql, `The connection was not closed. The connection's current state is open.` on SqlClient, with or without a transaction.  `CloseAsync` on a closed connection, or twice, returns.  So the method opens only a connection whose `State` is `Closed`, as decided.
- **`CloseAsync` with an open transaction rolls the transaction back** on both, pooled or not: another connection did not see its write, the transaction's `CommitAsync` then throws `This NpgsqlTransaction has completed; it is no longer usable.` or `This SqlTransaction has completed; it is no longer usable.`, and the connection reopens with no transaction.  The method never closes a connection it did not open, so this reaches no caller's transaction.
- **A cancelled token leaves the connection `Open`**, not `Broken`, in the `catch`, on both, and a `finally` that closes it leaves it `Closed`, after which it reopens and runs.  A caller's open connection stays open and usable, and on SQL Server a caller's transaction survives a cancelled command (`@@TRANCOUNT` 1, rollback works).
- **The exception of a cancellation differs by driver and by moment:**

| Cancelled | Npgsql | SqlClient |
|----|----|----|
| During `ExecuteReaderAsync` or `ExecuteNonQueryAsync` | `OperationCanceledException: Query was cancelled`, inner `PostgresException: 57014: canceling statement due to user request` | **`SqlException: A severe error occurred on the current command.  The results, if any, should be discarded. Operation cancelled by user.`** (Number 0, Class 11), not an `OperationCanceledException` |
| During `ReadAsync` | the same `OperationCanceledException` | `TaskCanceledException: A task was canceled.` |
| Before the call | `OperationCanceledException` | `TaskCanceledException` |

- **Npgsql returns the rows it already has before it throws.**  A token cancelled while `ReadAsync` waited returned the rows the server flushed in answer to the cancel as `true`, and only the `ReadAsync` that reached the server's error threw.  On PostgreSQL 14 a `UNION ALL` whose second arm slept sent no row until the sleep ended, so the cancel found nothing to stop; 18 sent the first row at once.  A test of cancellation during the read uses a query that sends rows before it waits, as `generate_series` with a `pg_sleep` on its last row did on both.
- **`commandTimeout`** on SQL Server throws `SqlException: Execution Timeout Expired.` (Number -2), the connection open in the `catch`.

## What the later sub-phases take from this

- **8.1, reading a row.**  A not-null column is `GetFieldValue<T>(i)` with no `IsDBNull`, and the driver's throw is the oracle on every type of both maps but `vector` (question 1), whose read waits on the owner.  A nullable column is `IsDBNull(i) ? null : GetFieldValue<T>(i)`; `GetFieldValue<T?>` is not used, since SqlClient refuses it (question 2).  The synchronous `GetFieldValue<T>` after `ReadAsync` reads the buffered row on both (question 10).
- **8.1, the reader's behaviour.**  `CommandBehavior.Default`: `SingleRow` would hide the second row `one` must see, and `SingleResult` gains nothing a method that reads only the first result set needs (question 10).
- **8.1, the parameter's name.**  Both drivers match the name with or without `@`, ignoring case; the method sets it without the `@` (question 4).
- **8.1, binding by value.**  The known issues of each engine are the rows of question 7 that fail: on PostgreSQL, every `string`-mapped type that is not text, `bit(1)`, `int2vector`, Npgsql 8's `daterange`, the unsigned and Npgsql-specific types, `macaddr8`, a `DateTimeOffset` with an offset, the `WHERE` of `money`, `timetz` and Npgsql 8's `date[]`, a `DateTime` sent by its `Kind` and converted in the session's `TimeZone` (into `date` on Npgsql 8, `timestamp` and `timestamptz`), and an untyped `NULL` where the server infers the type; on SQL Server, a `datetime2` sent as `datetime`, a `NULL` into a binary column, and the declaration that changes with the value.
- **8.1, the transaction.**  SqlClient refuses a command on a connection with a transaction when `Transaction` is not set; Npgsql runs it (question 4).
- **8.2, arrays.**  A `NULL` element read as `T[]` of a value type throws Npgsql's `InvalidCastException`, which names no column (question 3).
- **8.3, binding.**  `DataTypeName` set to the type's name mends every row of question 7 and works on Npgsql 8 and 10; an `NpgsqlParameter<T>` with `TypedValue` allocates nothing for a value type, nullable or not, and the parameter the command creates boxes it; its `NULL` is `TypedValue = null` of an `NpgsqlParameter<T?>` or of a reference type's, never `DBNull.Value`, which its setter refuses (question 8).  A domain is sent as its base type.  `rowcount` of an `UPDATE ... RETURNING` inside a `WITH` that ends in a `SELECT` is `-1` (question 6).
- **8.4, binding.**  The facet rules of 8.4's table hold, with four details: a value longer than `Size` is cut by SqlClient where the server would refuse it, `Scale` 0 is sent as 7, a `Scale` below 7 truncates where the server rounds, and `vector`'s dimension is the value's, with `DBNull.Value` sent as `nvarchar(4000)` unless the `NULL` is a `SqlVector<float>.CreateNull(n)` (question 9).  `SET NOCOUNT ON` outlives its command (question 6).  A cancelled execution is a `SqlException` (question 11).  An enum is read only from its own underlying type (question 9).
- **8.3 and 8.4, the run-time tests.**  The connection rule holds on both drivers, and a cancelled method leaves the connection it opened closed (question 11).
