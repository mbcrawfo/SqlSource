# Query generation, phase 6.4: the SQL Server type map - design

Date: 2026-10-20

Sub-phase 6.4 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It gives the generator SQL Server: a type map of every type Microsoft.Data.SqlClient reads, a probe of the driver the project references with its floor, the type override for SQL Server's names, table models named without `dbo`, and the end of `SQLSRC243` for `mssql`.  After it, an `mssql` file with a current sidecar gets its models.

It depends on nothing of phase 6 but the sidecar format, which already holds SQL Server's types, and can be built beside sub-phases 6.1 to 6.3.  Sub-phase 6.5 builds on it and on 6.3.  It follows [sub-phase 5.2](2026-10-18-query-generation-phase-5-2-type-map-design.md), PostgreSQL's map, in its method.

## Intent

The owner's measure of phase 5 was every built-in type mapped accurately to its .NET type; SQL Server is held to the same.  The map follows the outline's policies, the type the driver boxes, facets dropped from the type and kept in the documentation, a type the driver cannot read refused with the override as its help, and the owner's two decisions of this phase: the driver floor is SqlClient 6.1, the oldest line Microsoft still services, and the override's keys stay engine-neutral.

## Goals

1. `SqlServerTypeMap`: for every type SqlClient 6.1 and 7 read, the C# type each boxes, researched against both, with a column per driver where they differ, and a test for each row under each column.
2. The typing of a query dispatches on the engine of its entry; PostgreSQL's map, its tests and its behaviour are unchanged.
3. `SqlClientProbe`: whether the project references Microsoft.Data.SqlClient, which line, by symbol; the floor and newer-than-known diagnostics, once per project, when an `mssql` file has needs.
4. The type override: SQL Server's type names are known keys; a name the two engines share means, for each file, the type of that name in the file's engine; `sys.` is SQL Server's system schema as `pg_catalog.` is PostgreSQL's; an alias type is keyed `schema.name`; a bracketed key is refused with a help that shows the forms that work.
5. A table model of `dbo` is named without its schema, as one of `public` is.
6. `SQLSRC243` is reported for no dialect of this release.

## Non-goals

- The library maps: NodaTime, NetTopologySuite, `JsonDocument`, `SqlJson`, `SqlVector<T>` and `Microsoft.SqlServer.Types`.  Phase 7, and later.
- The bracket form of an override key.  Deferred by the owner as `D-0005`.
- Binding a parameter, `SqlDbType` and its facets.  Phase 8.
- Any proof against a server.  Sub-phase 6.5 reads every row of the map through Dapper on SqlClient 7 and on 6.1.

## Anti-goals

- **A PostgreSQL project that sees a difference.**  Every existing test of the map, the override and the models passes unchanged.
- **An override entry that silently changes meaning by engine** in a way the manual does not say: the manual's override section says that a shared name applies to each engine's own type of that name.
- **A version number read as a feature.**  SqlClient's assembly version is its major alone, `6.0.0.0` for 6.0 and 6.1; the probe keys on symbols, as the outline decides.

## What exists today

From the survey of the generator at the time of writing:

- `Generation/Types/PostgresTypeMap.cs` is the only map; `QueryTyper.TypeOf` sends any type that is not a `PostgresType` to `SQLSRC246` with "Run 'sqlsource describe'"; `QueryTyper.Typing` carries Npgsql's `DriverColumn`; `OverrideLookup` and the `OverrideOf` delegate take a `PostgresType`; `QueryTyper.Diagnose`'s help texts name Npgsql; `AsAKey` puts `pg_catalog.` before a bare key.
- `NpgsqlProbe`, its step, its tracking name, `UsesPostgresEntries`, and the project-level output of `SQLSRC247` and `SQLSRC248`, in `SqlSourceGenerator.cs`.
- `SidecarChecker.HasTypeMap` is `dialect == SqlDialect.PostgreSql`, "Phase 6 adds mssql"; `tests/SqlSource.Tests/Generator/SidecarTests.cs` asserts `SQLSRC243` for `mssql`.
- `Settings/TypeKeys.cs`: `Names`, PostgreSQL's 84, and `Aliases`, `int` and `integer` to `int4` and the like, applied when a key is read, so an `int` entry is stored as `int4`; `SystemSchema` is `pg_catalog`; a part is plain or double-quoted, and `[` is no part.  `TypeOverrideMap` merges per key and ranks the matches.  `Keys_OfTheMap_AreTheKnownNamesOfTheSettings` ties the map's rows to `Names`.
- `Generation/Models/PropertyNames.FromTable` drops a schema ordinally equal to `public`.
- `Snapshot/SqlServerType`: `Name`, the system type name with its facets, `MaxLength` in bytes, `Precision`, `Scale`, `UserType`.

## Design

### The engine split

`QueryTyper.TypeOf` sends a `PostgresType` to `PostgresTypeMap.Map` with Npgsql's column, and a `SqlServerType` to `SqlServerTypeMap.Map` with SqlClient's.  `QueryTyper.Typing` carries both columns.  The override is asked through one lookup that takes the engine and the type's keys, below, and each map gives `TypeMapping` as today.  `Diagnose`'s help texts and `AsAKey` depend on the engine of the entry: `sys.` for SQL Server, and SqlClient where Npgsql is named.  `FileTypesInput` carries both probes.  `TypeMapping.cs` and `TypeMappingKind.cs` say "a database type" where they say PostgreSQL.  `src/SqlSource/AGENTS.md`'s list of the files that may touch symbols gains `SqlClientProbe.cs`.

### The base map

A type's map key is its base name, `Name` cut before its first `(`: `nvarchar(50)` is `nvarchar`, `datetime2(7)` is `datetime2`.  A type with a `userType` is first asked of the override by the user type's key, then mapped by its base name, as a PostgreSQL domain maps as its base; `sysname` is `sys.sysname` over `nvarchar(128)` and is `string` so.  A CLR type, a user type with an assembly-qualified name, is not mapped by its base name, which is its own: it is a row of the map like any other.

The rows, as the outline's policy gives them and the research confirms or changes:

| Types | C# |
|----|----|
| `bigint`, `int`, `smallint`, `tinyint` | `long`, `int`, `short`, `byte` |
| `bit` | `bool` |
| `decimal`, `numeric`, `money`, `smallmoney` | `decimal` |
| `float`, `real` | `double`, `float` |
| `date`, `datetime`, `datetime2`, `smalldatetime` | `System.DateTime` |
| `time` | `System.TimeSpan` |
| `datetimeoffset` | `System.DateTimeOffset` |
| `char`, `varchar`, `text`, `nchar`, `nvarchar`, `ntext`, `xml` | `string` |
| `binary`, `varbinary`, `image`, `timestamp` | `byte[]` |
| `uniqueidentifier` | `System.Guid` |
| `sql_variant` | `object` |
| `json` | `string`, as the outline decides; the spike's probe found SqlClient 6.1.7 and 7.1.1 box it so |
| `vector` | `Microsoft.Data.SqlTypes.SqlVector<float>`, which the spike's probe found both drivers box: a type of SqlClient's, so the row needs the driver, `SQLSRC269` without it |
| `hierarchyid`, `geography`, `geometry` | Refused, `SQLSRC246`, with the override and the package `Microsoft.SqlServer.Types` in the help: the outline leaves them to a library map |

The research reads, for every row, what `SqlDataReader.GetValue` boxes and `GetFieldType` says on SqlClient 6.1's last patch and 7's, against SQL Server 2025, `json` and `vector` included, with the probes of the driver each needs, and records it in this spec's appendix, as 5.2 did for Npgsql.  A row whose type differs by driver has a cell per driver; under no driver, such a row is `DiffersByDriver` with the entry to write for each, and a row whose C# type is SqlClient's is the driver-needed failure, as PostgreSQL's rows do.

### The probe

`SqlClientProbe(Referenced, AtLeast61, AtLeast7, Version)`, a value-equal record from one `Select` over the compilation, combined after the parse as `NpgsqlProbe` is:

- `Referenced`: `Microsoft.Data.SqlClient.SqlConnection` is found.  `System.Data.SqlClient` is not Microsoft.Data.SqlClient and counts as no driver.
- `AtLeast61`: `Microsoft.Data.SqlTypes.SqlVector`1` is found, the type the outline names for 6.1.
- `AtLeast7`: a type or member that 7.0 first ships, which the research chooses and verifies in every release's metadata from 6.0 to the newest 7, as 5.2 verified `NpgsqlCube`.
- `Version`: the newest `Microsoft.Data.SqlClient` among the references, for the diagnostics alone: its `AssemblyInformationalVersionAttribute` cut before a `+`, `6.1.4`, read from the referenced assembly's symbol, and its assembly version's major when the attribute is absent, since the assembly version is the major alone.

The driver column is 7 when `AtLeast7`, 6.1 when `AtLeast61`, none when not `Referenced`.  Referenced and not `AtLeast61` is below the floor: `SQLSRC267`, an error once per project, naming the version found and the floor, with models still emitted by the 6.1 column.  A major above `NewestKnownMajor`, 7, is `SQLSRC268`, information, as `SQLSRC248` is.  Both are reported with no position from a project-level output that runs when an `mssql` file of the project has needs, as PostgreSQL's are, with a step and a tracking name of their own and a caching test.

The owner chose 6.1 over 5.1 knowing that EF Core 8 and 9 reference SqlClient 5.1: such a project gets `SQLSRC267`, and the manual's known issue says to reference Microsoft.Data.SqlClient 6.1 or later directly.

### The type override

The keys stay engine-neutral, as the owner decided in this phase and in 5.2.

- **Names.**  `TypeKeys.Names` gains SQL Server's names: every row of the map, `json`, `vector`, `hierarchyid`, `geography`, `geometry`, `sysname`, and `rowversion` as SQL Server's other name of `timestamp`.  A name both engines have is one key.
- **A key is read into one canonical key for each engine.**  Today `int` is read as `int4`.  After this sub-phase a key is read into a pair, PostgreSQL's canonical key and SQL Server's, either of which may be none: `int` and `integer` are `int4` and `int`; `int4` is `int4` and none; `bit` and `timestamp` are themselves in both; `rowversion` is none and `timestamp`; `nvarchar` is none and `nvarchar`; `pg_catalog.text` is `text` and none; `sys.text` is none and `text`; `_int4` is `int4[]` and none; a key of a user's schema, `dbo.Email`, is the same in both.  PostgreSQL's canonical key is exactly what a key reads as today.  Every rule that today compares keys compares, for each engine, that engine's canonical keys: the last entry of a key in an MSBuild value wins, two values of one key in one marker scope are `SQLSRC112` by the canonical key of the scope's dialect, the merge across levels replaces per key, and the ranking is by level, then by an exact spelling, then by order, as today.  So every existing test of the grammar, the keys and the map keeps its answer for PostgreSQL, and `int=X` applies to an `int` column of both engines while `int4=X` applies to PostgreSQL's alone.
- **The system schema.**  `sys.` before a SQL Server name is the system type, as `pg_catalog.` is PostgreSQL's: `sys.int` is `int`'s key in SQL Server alone, and `pg_catalog.int4` PostgreSQL's alone.
- **SQL Server's synonyms.**  T-SQL's one-word synonyms are aliases in SQL Server, as PostgreSQL's are in PostgreSQL: `integer` is `int`, `character` is `char`, `dec` is `decimal`, `rowversion` is `timestamp`.  So `integer=X` reaches both engines' integer, as it names one in each.  A name of two words, `double precision`, is no key, as on PostgreSQL.
- **User types.**  An alias type or a table type of a user's schema is keyed `schema.name`, `dbo.Email`, plain or quoted by part, as a PostgreSQL type outside `pg_catalog` is.  An unqualified name that no engine knows is the error it is today.
- **Brackets.**  A key with a `[` part, `[dbo].[Email]`, is refused by the grammar as today, `SQLSRC014` or `SQLSRC111`, and the help of either, when the key holds a `[`, shows the plain and the quoted forms: `dbo.Email` and `"dbo"."Email"`.  `D-0005` records the bracket form.
- **The meaning of a shared name.**  `bit` is a bit string on PostgreSQL and a boolean on SQL Server; `timestamp` is a time on PostgreSQL and a row version on SQL Server.  A project of both engines that wants an entry for one of them sets it by `AdditionalFiles` metadata on that engine's files, which the manual's override section says, as 5.2 decided.

`TypeKeys` stays in `Settings/`, which may not reference `Generation/`: the keys a type is matched by are computed where the type is, and `Settings/` keeps the grammar and the names.

### Table models

`PropertyNames.FromTable` takes the engine: the schema is dropped when it is ordinally `public` on PostgreSQL or `dbo` on SQL Server, and prefixed otherwise, so `billing.invoices` is `BillingInvoicesDto` on both and `dbo.Orders` is `OrdersDto`.

### The sidecar check

`SidecarChecker.HasTypeMap` is true for `mssql`.  `SQLSRC243` stays, for an engine whose describer lands before its map, and is reported by no dialect of this release; its section in `docs/manual/diagnostics.md` says so.  `SidecarTests` assert that an `mssql` file is checked against its sidecar.

### Diagnostics

| Id | Is |
|----|----|
| `SQLSRC267` | New, in `SqlDiagnostics`, error: "The project references Microsoft.Data.SqlClient {0}, and SqlSource needs 6.1 or later" |
| `SQLSRC268` | New, information: "The project references Microsoft.Data.SqlClient {0}, which is newer than this version of SqlSource knows" |
| `SQLSRC269` | New, error at the column: a type whose C# type is SqlClient's, `vector`, in a project that does not reference Microsoft.Data.SqlClient, as `SQLSRC249` is for Npgsql |
| `SQLSRC246`, `SQLSRC250` to `SQLSRC252` | Reused, with SQL Server's help texts |
| `SQLSRC014`, `SQLSRC111` | The help for a bracketed key |

## Testing

| Where | Cases |
|----|----|
| `SqlServerTypeMapTests` | Every row under each driver column and under none; a faceted name cut at `(`; an alias type mapped by its base and asked of the override first; `sysname`; a CLR type; the refused rows; `Keys_OfTheMap_AreTheKnownNamesOfTheSettings` for both engines' rows |
| `SqlClientProbeTests` | No reference, `System.Data.SqlClient` alone, 6.0, 6.1, 7 and a newer major, each by its symbols; `SQLSRC267` and `SQLSRC268` once per project, only with an `mssql` file that has needs |
| `TypeKeysTests`, `TypeOverrideMapTests` | SQL Server's names read; `int`, `bit` and `timestamp` matched in each engine; `int4` in PostgreSQL alone; `rowversion` in SQL Server alone; `sys.int`; `dbo.Email`, plain and quoted; a bracketed key refused with its help; the ranking across levels within an engine; every existing case unchanged |
| `QueryTyperTests` | A query of each engine; the help texts per engine |
| `PropertyNamesTests`, model tests | `dbo` dropped on SQL Server and kept on PostgreSQL, `public` the other way round |
| `SidecarTests` | `mssql` checked; `SQLSRC243` for no dialect |
| `Caching*Tests` | The probe's step; an edit to a `.sql` file of either engine re-runs only its file's typing |

## Documentation

- `docs/manual/sqlserver.md`: its Types section, the map's table, the floor, the driver it needs and what happens without it or below it, the refused types and their override, and its known issue: EF Core 8 and 9's SqlClient 5.1, with the fix.
- `docs/manual/settings.md`: the type override's keys for SQL Server, the shared names, `sys.`, and the bracket form's known issue for `D-0005`.  The model settings' Status cells say SQL Server.
- `docs/manual/README.md`'s "What works today": models for SQL Server.  6.4 may merge before 6.3, when the released tool cannot yet describe SQL Server: the statements that a user gets SQL Server models, in "What works today", the README and `generator.md`, are written by whichever of 6.3 and 6.4 merges second, and the first keeps the "Coming in a later release" callouts; `sqlserver.md`'s type map may be written by 6.4 either way, beneath the callout.  `docs/manual/generator.md` where it names PostgreSQL alone for models.  The README's "Only want to generate models?" and supported environments: SqlClient 6.1.
- `docs/manual/diagnostics.md`: `SQLSRC243` reported by no dialect, `SQLSRC267` to `SQLSRC269`, the help of `SQLSRC014` and `SQLSRC111`.
- `docs/architecture/generator.md`: the engine split, the probe in the pipeline's diagram, the override's keys per engine.
- `src/SqlSource/AGENTS.md`: the map's rows and the names, the probe file, `NewestKnownMajor` per driver.
- The epic outline: the row of 6.4.

## Order of work

One pull request, one commit for each step:

1. The outline's row says In progress.  The research appendix.
2. The engine split of the typing, with PostgreSQL's tests unchanged.
3. `SqlServerTypeMap` and its tests.
4. `SqlClientProbe`, its step and `SQLSRC267` to `SQLSRC269`.
5. The override's keys.
6. Table models of `dbo`; `HasTypeMap` and `SQLSRC243`.
7. The documents, and the row set to Done.

## Left to the builder

- The research's answers for every row, and therefore the per-driver cells; `json` and `vector` are settled above and the research confirms them.
- The symbol that marks 7.0.
- Where the canonical keys are computed and stored, within the rule that `Settings/` references nothing of `Generation/`.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- `SQLSRC243` is kept for a later engine rather than removed.
- An alias type is asked of the override by its own key and then mapped by its base, as a PostgreSQL domain is.
- `hierarchyid`, `geography` and `geometry` are refused by the base map, not mapped to `object` or `byte[]`.
- `System.Data.SqlClient` counts as no driver.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.
