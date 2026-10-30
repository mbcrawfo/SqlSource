# Query generation, phase 7: library maps - design

Date: 2026-10-26

Phase 7 of the [query generation epic](2026-10-07-query-generation-epic-design.md), specified again after phase 8 closed, as the owner decided on 2026-10-24.  It gives the generated query methods the types of the common .NET libraries, NodaTime, NetTopologySuite and System.Text.Json, out of the box: through the driver's plugin where the driver has one, and through conversions the generator writes where it does not.  It is six sub-phases, 7.0 to 7.5, each one pull request, run by an implementation manager from this document; each sub-phase's plan is written when the sub-phases it builds on have merged.

Written against `main` at `0b1a85ab`, after phase 8, sub-phase 8.7 included, and after [the settings-files design](2026-10-25-settings-files-design.md), whose integration branch `integration/settings-files` holds no layer yet.

Amended on 2026-10-30 by sub-phase 7.0, whose [spike](2026-10-26-query-generation-phase-7-spike.md) answered every "Verify first" item against the containers and drivers.  An amended detail cites the spike's question; a finding that bears on a Decided item went to the owner, and the owner's answer is written here with its date: the NodaTime range defaults, changed on 2026-10-30.

Amended again on 2026-10-30 by sub-phase 7.1, with three decisions the owner made that day on questions its build raised: `default` in the switch hides the farther levels and gives the engine's default, as a type override's `default` does; the switch knows `nodaTime` alone in 7.1, and 7.3 adds `json` and 7.4 `netTopologySuite`, each with its probe's markers; and `SQLSRC282` is delivered by 7.2 for NodaTime and by 7.4 for NetTopologySuite and `NetTopologySuite.IO.SqlServerBytes`.  Each amended statement says so with the date.

## How to read this document

Each statement is one of three kinds, as in the settings-files design:

- **Decided.**  Agreed with the owner on 2026-10-26.  A sub-phase must not contradict it without asking the owner and updating this document and the epic outline.
- **Designer's call.**  A proposal made while writing this design, which the owner has seen and not changed.  A sub-phase may change one when the code shows a better way, and says so in its pull request.
- **Verify first.**  A fact about a driver, a library, a server or an image that the design depends on and that was found by reading documentation or source, not by running it.  Sub-phase 7.0 confirms each before any generator code depends on it.

## Intent

The epic planned phase 7 before the methods existed: library maps for every output, switched on by detection from the compilation, with a converted row shape of its own.  Phase 8 then gave the methods converters (8.2), column types (8.7) and short C# names, and the owner moved this phase after it.  The goal, in the owner's words: support the types of the most common libraries in the .NET ecosystem; lean on the driver where it has native support, as Npgsql has for NodaTime; and where it has none, have the generator emit the mapping, so long as the conversion is simple.

JSON is the case that needs most care.  The owner's view: a JSON column is a `string` by default, and a project tells SqlSource when it wants the extra complexity of a JSON type.  The owner also wants JSON columns read into a project's own classes with System.Text.Json, a part that had no concrete shape before this design.

Decided, the owner's answers that shaped the design:

- **Phase 7 is for the generated methods.**  A library map changes a column's or parameter's C# type only in a query whose output is `codegen`.  A `models` query keeps today's types; a Dapper user who wants a library's type writes a type override and a handler, as today.
- **Detection only on a very clear signal.**  A library is on by default only where the driver's plugin assembly is referenced: `Npgsql.NodaTime` and `Npgsql.NetTopologySuite` on PostgreSQL.  SQL Server has no plugin, so a library is on there only when the project asks.  PostgreSQL needs a way to turn a detected library off.  The switch has the standard levels: the settings files, a file's header and a query.
- **JSON's own classes use System.Text.Json's default options.**  A project that needs options of its own writes a `[SqlSourceConverter]` from `string` to its type that calls the serializer with them.
- **A string column that holds JSON is covered by an explicit opt-in** on its mapping, so that SQL Server before 2025, which stores JSON in `nvarchar(max)`, can use its classes too.  A class mapped to a string column without the opt-in is never serialized.
- **NetTopologySuite on SQL Server is in**, with the generated reader and writer shared as static instances, and no setup needed from the user beyond NetTopologySuite's own global default.
- **The default types** in [the libraries](#the-libraries), approved as proposed, but for PostgreSQL's NodaTime ranges, which the owner changed on 2026-10-30 after 7.0's spike: `NpgsqlRange<Instant>` and `NpgsqlRange<LocalDate>` ("B: NpgsqlRange").
- **Approach A**, below: the generator's conversions go through phase 8's converter pipeline.

## Goals

1. A library switch, `libraryTypes` in a `sqlsource.json` and `-- library-types:` in a file's header and in a query, that turns each library on or off for `codegen` queries, merged library by library.
2. NodaTime: on PostgreSQL through `Npgsql.NodaTime`, detected; on SQL Server through generated conversions, on request.  Every NodaTime type the [table](#nodatime) names read and sent through the methods.
3. NetTopologySuite: on PostgreSQL through `Npgsql.NetTopologySuite`, detected, for PostGIS's `geometry` and `geography`; on SQL Server through `NetTopologySuite.IO.SqlServerBytes`, on request.
4. JSON: `string` by default on both engines, as today, with a string column that holds JSON covered by an explicit opt-in.  The `json` library makes an unmapped JSON column a `JsonElement`.  `JsonElement`, `JsonDocument` and `JsonNode` given by a type override, a column type or a marker are read and sent on both engines, through the driver where it can and through a generated parse where it cannot.  A project's own class given to a JSON column is read and sent through System.Text.Json's default options, unless the project has a converter for it.
5. The generator's conversions supplied through the converter pipeline of sub-phase 8.2, so that a project's own converter replaces any of them, null never reaches one, and a PostgreSQL array is converted element by element.
6. Each library proven end to end on both engines, on the newest driver and the floor's, and shown in each example project.

## Non-goals

- **Library maps for `models` queries.**  Decided: out of scope, not deferred.  A project that reads its models through Dapper gives them a library's types with the type override and the column types, which exist today, and writes its own handlers; SqlSource will not detect libraries for `models` queries.
- **Libraries beyond the three.**  Newtonsoft through `Npgsql.Json.NET`, `Npgsql.GeoJSON`, Pgvector, Ulid, and the strongly-typed-id generators, which are `D-0007`.  A project maps them with a type override and, where the driver does not read the type, a converter, as today.
- **`SqlJson` and `SqlDbTypeExtensions.Json`.**  The epic planned a `SqlJson` map gated on SqlClient 6.0.  The 6.1 floor always has it, so a flag would be constant; `SqlJson` reads as a `string` and adds nothing a project wants; and binding as `SqlDbTypeExtensions.Json` fails against SQL Server 2017, as [8.0's spike](2026-10-24-query-generation-phase-8-spike.md) found.  `json` stays sent as `nvarchar(max)`.
- **Detecting a call to `UseNodaTime()`, `UseNetTopologySuite()` or `EnableDynamicJson()`.**  These are run-time calls, possibly in another assembly; the plugin assembly is the only signal.
- **Serializer options other than the defaults**, a `JsonSerializerContext`, or a setting naming either.  Decided: a converter is the way.
- **Generated Dapper type handlers.**  Nothing generated depends on Dapper.
- **NodaTime types with no single-column form**, `ZonedDateTime` with a zone, `Period` on SQL Server, `Interval` and `DateInterval` on SQL Server.
- **`xml` as `XDocument` or `XElement`.**  It stays `string`.

## Anti-goals

- **A change to a project that turns nothing on.**  A SQL Server project, or a PostgreSQL project without either plugin, that sets no `libraryTypes` gets the same models and methods, byte for byte, as before this phase, unless it maps a type to a library type with an override, where it now gets the conversion it lacked.  Two exceptions: a PostgreSQL project that references a plugin, whose `codegen` queries change, which is the detection the owner asked for; and a project that gives a JSON column a class of its own with no converter, which on PostgreSQL went to the driver, through `EnableDynamicJson()` and whatever options the data source set, and now goes through the serializer's default options.  No release has shipped (`VersionPrefix` 0.1.0, no `v*` tag), so no user's project changes under them.
- **Generated code that names a library the project does not reference.**  A conversion class is emitted only for a library whose assemblies the compilation has, and only when some `codegen` query uses it.
- **A conversion that guesses.**  Every default conversion is lossless when it reads.  The two that need a convention, `datetime2` as an `Instant` and `time` as a `Duration`, are reached only by a type override that names the type, and the manual states the convention.
- **A warning hidden in generated code.**  The reflection-based serializer warns in a trimmed or AOT project (`IL2026`, `IL3050`); the warning is left where it falls, and the manual says to write a converter with a source-generated context there.
- **A query diagnostic in a project that does not generate methods**, as 8.2's anti-goal says for converters.

## Constraints

- **The settings files come first.**  Phase 7's switch is a key of `sqlsource.json` and a marker, and there is no MSBuild form of it.  Sub-phase 7.1 builds on `main` after the settings-files integration branch has merged into it; 7.0, which writes no product code, may run before.
- **The analyzer** targets `netstandard2.0` with no dependencies and Roslyn 4.8 as its floor.  It names no library type in its own code; library types are text it emits.
- **Generated code** needs .NET 8 and C# 12 (`SQLSRC003`, `SQLSRC012`), so it may use `DateOnly`, `TimeOnly`, the in-box System.Text.Json, `static` lambdas and patterns.
- **The post-initialization text** compiles in every project that references the package, netstandard and C# 7.3 included, and must name no library type.  Library conversions are emitted after the probe, by a source output.
- **The drivers' floors** are Npgsql 8.0 and Microsoft.Data.SqlClient 6.1, tested with Npgsql 8, 10 and SqlClient 6.1, 7 as today.  Each plugin's floor is the version of the plugin that matches its Npgsql.
- **Root `AGENTS.md`**: every test image pinned (rule 5), which a PostGIS image must meet; deferred work and tech debt recorded in the pull request that causes them; documentation, examples and architecture kept current by each sub-phase; `./pre-commit-validation.sh` before every push.

## What exists today

As of `0b1a85ab`.  Paths are under `src/SqlSource/` unless they say otherwise.

- **The base maps.**  `Generation/Types/Maps/PostgresTypeMap.cs` gives each PostgreSQL type the type Npgsql 8 and Npgsql 10 box; `json`, `jsonb`, `jsonpath` and `xml` are `string`; PostGIS's `geometry` and `geography` have no row and are `SQLSRC246`, whose help names the type override.  `Generation/Types/Maps/SqlServerTypeMap.cs` gives SqlClient's; `xml` and `json` are `string`; `hierarchyid`, `geography` and `geometry` are `SQLSRC246` with help naming Microsoft.SqlServer.Types, and bound as `Udt` when an override maps them.  `TypeKeyNames.Extensions` (`Settings/TypeOverrides/TypeKeyNames.cs`) lists the extension types the base map knows: `citext`, `hstore`, `ltree`, `lquery`, `ltxtquery`, `cube`.
- **The probes.**  `NpgsqlProbe` and `SqlClientProbe`, each a `CompilationProvider.Select` to a value-equal record of flags found by marker symbols, combined into `FileTypesInput` and read by `QueryTyper` and `MethodDrivers`.  `IDriverProbe` and `DriverProbes.Has` are shared.
- **Typing.**  `QueryTyper` gives a parameter its `-- param: -> Type`, else `TypeOf`; a column its query's `-- column:`, then the column types, then `TypeOf`; `TypeOf` asks the merged type override at every level, then the base map.  The result is `PropertyType`, whose `Place` is set when an override, a column type or a marker chose the type, and whose `Database` is the base map's type with no override applied.  The 5.2 design planned the order "override, then a library map, then the base map".
- **Converters.**  `ConverterReader` finds `[SqlSourceConverter]` methods in the project; `MappedTypeResolver.AddConverters` finds them on a mapped type in a referenced assembly; `ConverterSet.Of` joins the two, the project's replacing the mapped type's for one edge.  `Methods/ConversionPlanner.cs` converts only where `Place` is set, reads by the edge from `Database` to the property's type and writes by the reverse, converts arrays by element, and raises `SQLSRC279` and `SQLSRC280`.  `MethodsInput.ConvertersOf` collects converters only for properties with `Place` set.  `QueryMethodWriter` writes a converted read as `GetFieldValue<B>` then the call, behind `IsDBNull` for a nullable column; `ParameterBinder` and `PostgresParameterBinder` write the reverse.
- **Binding.**  PostgreSQL: an `NpgsqlParameter<T>` with `DataTypeName` set to the sidecar's `bindName`.  SQL Server: the command's `SqlParameter` with `SqlDbType` and facets, a CLR type as `Udt` with its `UdtTypeName`, `json` as `NVarChar` at `max`.
- **Generated support types.**  `AttributeSource` holds the attribute, the enums, `SqlSourceConverterAttribute` and `UnexpectedRowCountException`, listed in `GeneratedTypes` for the suppressor of `TD-0006`.
- **The tool's check of a PostgreSQL parameter** binds it through a plain `NpgsqlDataSourceBuilder` with no plugin (`src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs`), and refuses a type Npgsql cannot write as `SQLSRC232`.
- **The end-to-end projects.**  `tests/DatabaseTests/Postgres` and `tests/DatabaseTests/SqlServer`, with their floor projects `tests/DatabaseTests/Postgres.Npgsql8` and `tests/DatabaseTests/SqlServer.SqlClient61`, write and read every row of each map through the methods (`all_types`, `AllTypesRows`, `ModelShapeTests`), keep Dapper for the recipe's tests and the realistic corpus, which is claimed under `models` and `codegen` with shared models, and set `timestamptz=System.DateTimeOffset` and `date=System.DateOnly` respectively.  The PostgreSQL image, `postgres:18.6`, has no PostGIS.
- **Shared models.**  Two queries that give one model name different properties are `SQLSRC257`.
- **The free diagnostic ids**: from `SQLSRC281` for the snapshot and the generator's typing; the settings-files work takes ids below 100 for a settings file's faults, and a marker's grammar error takes the next free id from 101.

## Approaches

Three ways were considered of supplying the conversions the generator writes.

**A. Generated converters through phase 8's converter pipeline.**  When a library is in use, the generator emits its conversions as static methods of an internal generated class, and they join the project's converters as a third origin beside the project and the mapped type.  The rule that the project's converter replaces another of the same edge then gives "bring your own options" with nothing new; null handling, arrays and `SQLSRC279` and `SQLSRC280` come with it.  It needs two extensions: a read path of its own for SQL Server's spatial types, which SqlClient gives only through `GetSqlBytes`, and a serializer conversion for a project's own JSON classes, since a converter is one fixed pair of types.

**B. A converted row in the type map, written into each method.**  The epic's original third shape: each library row carries a read and a write expression, inlined.  No helper class, but a second conversion mechanism beside converters, with null handling, arrays and replacement by the project's converter built twice.

**C. Companion packages**, `SqlSource.NodaTime` and the like, shipping marked converter methods.  Dropped: a converter is found only in the project and on the mapped type, and `Instant` is not SqlSource's to mark, so discovery would change; and each package adds a release to version and test against every driver.

**Decided: A**, because it reuses what phase 8 proved, and its two extensions are contained additions, not a parallel system.

## Design

### The libraries

How each library's types are read, by engine.  "Plugin" is the driver reading the type itself, `GetFieldValue<T>` and an `NpgsqlParameter<T>` with `DataTypeName`, as 8.1 does for an override with no converter.  "Generated" is a conversion of this phase, written as a converter.

| Library | Name in the switch | PostgreSQL | SQL Server |
|----|----|----|----|
| NodaTime | `nodaTime` | Plugin, `Npgsql.NodaTime`; on by default when it is referenced | Generated; off by default; needs `NodaTime` |
| NetTopologySuite | `netTopologySuite` | Plugin, `Npgsql.NetTopologySuite`; on by default when it is referenced | Generated, with a read path of its own; off by default; needs `NetTopologySuite` and `NetTopologySuite.IO.SqlServerBytes` |
| System.Text.Json | `json` | `JsonElement` and `JsonDocument` by the driver; `JsonNode` by the driver on Npgsql 10 and generated on 8 and 9; a project's class generated; off by default | Generated; off by default |

Designer's call: the names in the switch, `nodaTime`, `netTopologySuite` and `json`, matched as a choice is, ignoring case, hyphens and spaces.  Decided by the owner on 2026-10-30: the switch knows `nodaTime` alone in 7.1; 7.3 adds `json`, and 7.4 `netTopologySuite`, each with the probe's markers of its library.

### The library switch

Decided: one setting, at the standard levels, with each library's default by engine as the table above gives it.  Designer's call, its form:

```jsonc
// sqlsource.json
{
    "libraryTypes": {
        "nodaTime": "on",
        "netTopologySuite": "off",
    },
}
```

```sql
-- library-types: nodatime=on json=on
```

- **The value** of a library is `on`, `off` or `default`.  `default` hides the library's entries of the farther levels, so the engine's default applies, as `default` does for a type override, which hides the type's farther entries and gives the type to the base map.  Amended on 2026-10-30 by the owner, where this said that a farther level or the engine's default applies.
- **The levels**, most specific first: a `-- library-types:` marker inside the query; one in the file header; the `.sql` file's chain of settings files, nearest first.  No attribute level, since the owner named the three; and no MSBuild level, since none exists after the settings-files work.  Merged per library, as the type override is per key: a level replaces only the libraries it names.
- **The default** when no level names a library: on PostgreSQL, `on` when the plugin assembly is referenced and `off` otherwise; on SQL Server, `off`; for `json` on both, `off`.
- **One value for both engines.**  A setting names a library, not an engine; a file's dialect decides which engine's rules apply to its queries.
- **It takes effect only in a `codegen` query.**  In a `models` or `sql` query the switch is read, its grammar checked, and nothing else; no diagnostic says it does nothing there.
- **The marker** takes entries of `name=value` separated by white space, several per marker, and is allowed in the file header and in a query, as `-- type-override:` is.  It is not part of the query's hash (`SqlQueryHash`), so changing it leaves the sidecar current.  The tool ignores it, as it ignores `-- type-override:`.

What a library being on does:

- **It gives the defaults of its rows** to the columns and parameters whose database types it maps, below a type override, a column type and a marker's C# type, and above the base map.
- **It does not gate conversions.**  The generator's conversions between a library type and the type the driver reads are offered wherever the library's assemblies are referenced and the driver of the query's engine cannot read the type, whether the switch is on or not, so a SQL Server project that wants one column as an `Instant` maps it with a type override and gets its conversion without turning NodaTime on.  The switch decides only what an unmapped column becomes.  Designer's call; see [generated conversions](#generated-conversions).

Errors, designer's call for their ids:

- **A name or a value the switch does not know**, in a settings file: the settings-files work's diagnostic for a value that is not valid, at the member.  In a marker: `SQLSRC111`, a marker value that is not valid, at the line, as for `-- type-override:`.  Reported in any project, like a type override's grammar.
- **`on` for a library whose assemblies the project does not reference**: `SQLSRC281`, an error, once for each place that says `on` and each engine of a `codegen` query that place reaches, naming the library and the assemblies that are missing.  On PostgreSQL that is the plugin, since without it the driver cannot read the type; on SQL Server the library's own assemblies.  The queries it reaches keep the base map's types.

### Where the library map stands

The order a column's type is chosen in, from 8.7, with the library map added:

1. The query's `-- column: name -> Type`.
2. The column types, by the column's source.
3. The type override, at every level, for the column's type, its domain's base and its array's element.
4. **The library map**, for the query's engine, of each library that is on for the query.
5. The base map, under the driver's probe.

A parameter: its `-- param: -> Type`, else 3 to 5.  A library row matches a type as the base map does: by its name in `pg_catalog`, or for PostGIS by `geometry` and `geography` in whatever schema holds them.  7.0 found the describer gives a PostGIS type `kind` `base` and `internalName` `geometry` or `geography`, with its schema in `schema` and in `name`, and its modifier in `name` (`public.geometry(Point,4326)`), so a PostGIS row matches by `internalName` with `kind` `base`, as `TypeKeyNames.IsExtension` matches `citext` ([spike, question 4](2026-10-26-query-generation-phase-7-spike.md#4-postgis)); through a domain to its base, and through an array to its element, giving `T[]`.  A domain mapped by an override keeps the override's type.

**A query is `codegen` per claiming type.**  A query's output is resolved for each type that claims its file, by the markers, the attribute and the settings files, so one query can be `models` for one type and `codegen` for another, as the realistic corpora are.  Typing runs once per file, before the claiming types.  Designer's call: typing gives each column and parameter whose type a library row chose both types, the library's and the one it would have without the library, and the step that builds a type's models picks by that type's output.  So typing stays once per file, and the switch's merge, which needs only the file's and query's levels, stays there too.  `SQLSRC281` is reported where a library row is used for a `codegen` claim.

**The model types differ by output.**  A table model, or a model named by a marker, shared by a `codegen` query with a library on and a `models` query reading the same columns, has two sets of property types, and that is `SQLSRC257`, as for any two models of one name that differ.  So do two `codegen` queries, in places where the library is on and off, that share a model.  The manual says so with the fix: name the models apart, or turn the library on or off alike for both.

**The PostgreSQL driver must be taught at run time.**  A detected library is read by its plugin, so the application must call `UseNodaTime()` or `UseNetTopologySuite()` on its data source.  If it does not, the method fails with the driver's `InvalidCastException`, and the README's FAQ on that exception names this case.  Two traps the manual names:

- **Entity Framework Core.**  `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime` references `Npgsql.NodaTime`, so the library is detected, while EF's `o.UseNodaTime()` teaches only EF's own data source.  A data source built for the methods needs its own call; a project that does not want NodaTime in the methods turns it off.
- **Dapper on the same data source.**  After `UseNodaTime()`, Npgsql boxes `timestamptz` as an `Instant`, so a `models` query read through Dapper into a `DateTime` property fails.  That is the driver's change, not SqlSource's, and the manual points to the type override.

### NodaTime

Decided, the default types:

| Database type | PostgreSQL, through the plugin | SQL Server, generated |
|----|----|----|
| `timestamptz` / `datetimeoffset` | `Instant` | `OffsetDateTime` |
| `timestamp` / `datetime2`, `datetime`, `smalldatetime` | `LocalDateTime` | `LocalDateTime` |
| `date` | `LocalDate` | `LocalDate` |
| `time` | `LocalTime` | `LocalTime` |
| `timetz` | `OffsetTime` | |
| `interval` | `Period` | |
| `tstzrange`, `tsrange`, `daterange` | `NpgsqlRange<Instant>`, `NpgsqlRange<LocalDateTime>`, `NpgsqlRange<LocalDate>` | |
| `tstzmultirange`, `tsmultirange`, `datemultirange` | `NpgsqlRange<Instant>[]`, `NpgsqlRange<LocalDateTime>[]`, `NpgsqlRange<LocalDate>[]` | |

**PostgreSQL.**  These are the types the plugin boxes, on Npgsql 8, 9 and 10 alike, but for `tstzrange` and `daterange` and their multiranges, which it boxes as `Interval` and `DateInterval`.  The owner chose `NpgsqlRange<Instant>` and `NpgsqlRange<LocalDate>` for those on 2026-10-30, since 7.0 found that `Interval` and `DateInterval` cannot read an empty range (`ArgumentOutOfRangeException`, `ArgumentException`) and that `Interval` reads a `(a,b]` `tstzrange` as `[a+1ns, b+1ns)`, which a write stores as `[a,b)`.  The plugin reads and writes `NpgsqlRange<Instant>` and `NpgsqlRange<LocalDate>`, their arrays included, with neither fault.  `Interval` and `DateInterval` stay reachable by a type override, and the manual names both faults there.  It also says that a reader of the boxed value, Dapper's, still gets `Interval` and `DateInterval` for these columns, and so fails on an empty range once the plugin is on.  An array of each is `T[]`.  A type override still chooses any type the plugin reads, `timestamptz=NodaTime.ZonedDateTime` or `interval=NodaTime.Duration`, and the method reads it through the plugin; the manual names the plugin's accepted alternatives and that `ZonedDateTime` and `OffsetDateTime` are read and must be written in UTC.  7.0 confirmed each row, its arrays and its `NULL`, and each alternative, on Npgsql 8 and 10 and on a full and a slim data source, and the types the plugin boxes on Npgsql 9 too.  A value not in UTC fails before the query is sent with `ArgumentException`, not `InvalidCastException`.  An `interval` with months read as `Duration` throws `InvalidCastException` naming `Period` ([spike, question 1](2026-10-26-query-generation-phase-7-spike.md#1-nodatime-on-postgresql)).  No generated conversion is used on PostgreSQL: a project that references NodaTime but not `Npgsql.NodaTime` and maps `timestamptz=NodaTime.Instant` keeps today's behaviour, the type trusted to the driver, which fails at run time until the plugin is registered; the manual says to add the plugin.

**SQL Server.**  The generated conversions, each one line, on the type SqlClient boxes:

| Edge | Read, from the database | Write, to the database | Note |
|----|----|----|----|
| `DateTime` ↔ `LocalDate` | `LocalDate.FromDateTime(v)` | `v.ToDateTimeUnspecified()` | Default for `date` |
| `TimeSpan` ↔ `LocalTime` | `LocalTime.FromTicksSinceMidnight(v.Ticks)` | `new TimeSpan(v.TickOfDay)` | Default for `time` |
| `DateTime` ↔ `LocalDateTime` | `LocalDateTime.FromDateTime(v)` | `v.ToDateTimeUnspecified()` | Default for `datetime2`, `datetime`, `smalldatetime` |
| `DateTimeOffset` ↔ `OffsetDateTime` | `OffsetDateTime.FromDateTimeOffset(v)` | `v.ToDateTimeOffset()` | Default for `datetimeoffset` |
| `DateTime` ↔ `Instant` | `Instant.FromDateTimeUtc(DateTime.SpecifyKind(v, DateTimeKind.Utc))` | `v.ToDateTimeUtc()` | By override only; the stored value is taken as UTC, as the EF Core provider and the Dapper packages take it |
| `DateTimeOffset` ↔ `Instant` | `Instant.FromDateTimeOffset(v)` | `v.ToDateTimeOffset()` | By override only; the offset is dropped on read and is zero on write |
| `TimeSpan` ↔ `Duration` | `Duration.FromTimeSpan(v)` | `v.ToTimeSpan()` | By override only; `time` holds 0 to 24 hours, and a value outside fails as the driver fails |

These follow the EF Core provider for SQL Server, `SimplerSoftware.EntityFrameworkCore.SqlServer.NodaTime`.  Every default reads without loss: `datetime2(7)`, `time(7)` and `datetimeoffset(7)` hold ticks, and NodaTime's nanoseconds below a tick are truncated on write, as the provider does.  The manual says that `datetime` and `smalldatetime` round on write, as they do for a `DateTime`.

7.0 ran each edge both ways at the limits of each type ([spike, question 2](2026-10-26-query-generation-phase-7-spike.md#2-nodatime-on-sql-server)).  Every default reads without loss.  Where a write fails, the failure is the conversion's or SqlClient's, before the server, and the method lets it through, as for any value the driver refuses.  The manual says so, with these cases:

- An `OffsetDateTime` whose offset has seconds, which NodaTime allows and `DateTimeOffset` does not, is truncated to the minute by `ToDateTimeOffset()`, with no error: the stored instant moves by those seconds.  An offset beyond ±14 hours, or a local time whose instant `DateTimeOffset` cannot hold, throws.
- A NodaTime value outside `DateTime`'s range throws in the conversion: `ArgumentOutOfRangeException` for a `LocalDate`, `InvalidOperationException` for a `LocalDateTime` or an `Instant`.
- SqlClient rounds `datetime` and `smalldatetime` before it sends, and refuses a value outside their ranges, or rounding past them, with `SqlTypeException` or `OverflowException`.  At `datetime`'s top it is uneven: `9999-12-31 23:59:59.999` overflows while `DateTime.MaxValue` is stored as `.997`.
- A `Duration` outside `time`, 24 hours or more or below zero, throws `OverflowException: SqlDbType.Time overflow` in SqlClient.

The parameter of a converted type is bound by its described `SqlDbType` and facets, as 8.2 binds every converted parameter.

### NetTopologySuite

**PostgreSQL.**  `geometry` and `geography`, PostGIS's types, are `Geometry`, read and sent through the plugin.  A type override may name a subtype, `Point` or `Polygon`, which the plugin reads.  An array is `Geometry[]`.  PostGIS is an extension and its types are not in the base map, so with the library off they stay `SQLSRC246`, whose help gains the library.

**The tool's check.**  The tool's own data source has no plugin, so its check of a PostGIS parameter is refused as `SQLSRC232` today.  7.0 confirmed it, in `public` and in another schema, and found that `Npgsql.NetTopologySuite` with `UseNetTopologySuite()` in `PostgresConnection.BuildSource` makes every PostGIS parameter described and checked.  It leaves every column's entry as it was, refuses another extension's type as before, and describes as before against a database without PostGIS ([spike, question 4](2026-10-26-query-generation-phase-7-spike.md#4-postgis)).  Designer's call: the tool's PostgreSQL connection registers `Npgsql.NetTopologySuite`, which the tool then ships, so a PostGIS parameter is described and checked; if the owner prefers not to grow the tool, the check skips a type of a known extension instead, and 7.4's pull request says which.  Either way 7.4 takes PostGIS out of the manual's list under `SQLSRC232` and out of `docs/manual/postgresql.md`'s row for it, and changes the known issue of [TD-0093](../../tech-debt/TD-0093-a-param-marker-takes-a-modifier-of-numbers-alone.md) to write the type without its modifier, since 7.0 left both saying that a PostGIS parameter is declared `text` and cast.

**SQL Server.**  `geography` and `geometry` are `Geometry`, with the library on.  SqlClient reads a spatial column's bytes only through `GetSqlBytes`; `GetFieldValue<SqlBytes>`, `GetFieldValue<byte[]>` and `GetValue` go through Microsoft.SqlServer.Types.  So the read is a path of its own, not a converter after `GetFieldValue<B>`:

```csharp
// A generated internal class, emitted when a codegen query reads or sends a spatial NetTopologySuite type.
private static readonly SqlServerBytesReader GeographyReader = new() { IsGeography = true };
private static readonly SqlServerBytesWriter GeographyWriter = new() { IsGeography = true };
// and the two of geometry

// The read of a nullable column, in the method:
reader.IsDBNull(i) ? null : (Geometry)GeographyReader.Read(((SqlDataReader)reader).GetSqlBytes(i).Value)
```

- **The cast to `SqlDataReader`**: the methods' reader is a `DbDataReader`, and `GetSqlBytes` is SqlClient's.  A connection that wraps SqlClient's gives a reader of its own, so the cast fails there, as binding already fails for such a connection, the known issue of 8.4; the manual's known issue gains the spatial read.  Designer's call: the cast is written once per method that reads a spatial column.  7.0 found that `DbDataReader.GetStream(i)` and `GetBytes` give the same bytes with no cast, and `SqlServerBytesReader.Read(Stream)` exists ([spike, question 5](2026-10-26-query-generation-phase-7-spike.md#5-nettopologysuite-on-sql-server)).  So 7.4 reads through `GetStream` with no cast if it works through a connection that wraps SqlClient's, and keeps the cast and its known issue otherwise; its pull request says which.

- **The write** is a `SqlBytes` of `Writer.Write(value)`, bound as `Udt` with `UdtTypeName` `geography` or `geometry`, a null as `SqlBytes.Null`, as EF Core's provider binds it.  It needs no Microsoft.SqlServer.Types.
- **A subtype** given by a type override, `geography=NetTopologySuite.Geometries.Point`, is the read cast to it, which throws `InvalidCastException` for another shape.
- **The four instances** are `static readonly` fields, created on first use with NetTopologySuite's `NtsGeometryServices.Instance`.  A reader takes `Instance` when it is constructed, not when it reads (7.0), so "first use" must be the first spatial query: the class that holds them has a static constructor, which keeps the runtime from creating them earlier, and holds nothing else.  A project with other services sets `NtsGeometryServices.Instance` at startup, before the first query; one that configures NetTopologySuite only through EF Core's `UseNetTopologySuite(...)` gets the defaults, and the manual says so.  The SRID comes from the bytes.
- **Safe to share, and Z and M kept.**  7.0 found that `Read` and `Write` write no instance state in 2.1.0's source, and that shared instances used from 16 threads gave the single-threaded results.  The reader's and the writer's `HandleOrdinates` are `XYZM` by default, and a ZM point round-trips through both servers.  So the generated instances set no ordinates.
- **SRID 0.**  SQL Server refuses a `geography` of SRID 0, NetTopologySuite's default, with TDS error 8023, `... not a valid instance of data type geography`; `geometry` takes it.  The manual says to give a `geography` its SRID.
- **A type override** to `Microsoft.SqlServer.Types.SqlGeography` keeps 8.4's `Udt` binding, being an override.
- **Ring orientation.**  The writer throws `ArgumentException` for a `geography` polygon whose shell is clockwise; the manual says so.

### JSON

**The default stays `string`** for `json` and `jsonb` on PostgreSQL and `json` on SQL Server.  `jsonpath` and `xml` stay `string` with any library on.

**The `json` library**, when on, makes an unmapped JSON column or parameter a `JsonElement`: immutable, and nothing to dispose, where a `JsonDocument` must be disposed and so makes a poor property.  An array of `jsonb` is `JsonElement[]`.

**The JSON types**, given by an override, a column type or a marker, with the library on or off:

| Type | PostgreSQL | SQL Server |
|----|----|----|
| `JsonElement` | The driver, Npgsql 8 to 10 | Generated: read by `JsonDocument.Parse(v)` and the root's `Clone()`, the document disposed; written by `GetRawText()` |
| `JsonDocument` | The driver | Generated: `JsonDocument.Parse(v)`; written by `RootElement.GetRawText()`.  The caller disposes it, as the manual says |
| `JsonNode`, `JsonObject`, `JsonArray` | The driver on Npgsql 10; generated on 8 and 9, which read them only after `EnableDynamicJson()` | Generated: `JsonNode.Parse(v)`, cast to the subtype; written by `ToJsonString()` |

Designer's call: a `JsonElement` of kind `Null` is a JSON `null`, not a SQL `NULL`; a SQL `NULL` is a null property, as for every type.  7.0 confirmed each driver read named "the driver" above, on Npgsql 8, 9 and 10, with `DataTypeName` `json` and `jsonb` on the write.  `JsonNode` and its subtypes are read by Npgsql 10 alone; 8 and 9 throw `InvalidCastException` naming `EnableDynamicJson` ([spike, question 3](2026-10-26-query-generation-phase-7-spike.md#3-json)).  It also found:

- **SQL Server 2025's `json` refuses a top-level scalar**, a JSON `null` included, with error 13609 `JSON text is not properly formatted`, where `nvarchar(max)` takes it.  So a `JsonElement` of kind `Null`, or a class whose JSON is a scalar, written to a `json` column fails at the server, and the method lets the error through.  The manual says so.
- **A `JsonNode` cannot hold a JSON `null`.**  `JsonNode.Parse("null")` and Npgsql's read both give a C# `null`.  So a JSON `null` read into a `JsonNode`, `JsonObject` or `JsonArray` property is `null` when the property is nullable, and throws `InvalidCastException("Column '<name>' holds a JSON null.")` when it is not, as for a project's class below.  The method checks the value it read, whether the driver or a generated parse read it, as 8.1 checks a `vector`.
- **2025's `json` normalizes what it stores**, and PostgreSQL's `jsonb` does too.  White space goes, escapes are unescaped, and of duplicate keys SQL Server keeps the first.  So a value reads back equal in structure, not in text.

**A project's own class.**  Decided: a JSON column or parameter given a type that is no JSON type and no type the base map gives, `OrderMetadata`, by an override, a column type or a marker, is read and sent through System.Text.Json with its default options, unless the project has a converter of that edge.  Designer's call: this needs no switch, since mapping a JSON column to a class is itself the opt-in, and it applies with the `json` library off.  The generated code:

```csharp
// read
reader.IsDBNull(i) ? null : JsonSerializer.Deserialize<OrderMetadata>(reader.GetFieldValue<string>(i))
// write
value is { } v ? JsonSerializer.Serialize(v) : DBNull.Value
```

- **On PostgreSQL** this replaces the driver's route, which needs `EnableDynamicJson()`, an opt-in nothing can detect that is neither trim nor AOT safe.  Npgsql reads any JSON column as a `string` and binds a `string` with `DataTypeName` `jsonb`, so the generated code needs no opt-in.  A project that wants the driver's route writes no class mapping; a project with options of its own writes a converter, which the converter pipeline already prefers.
- **On SQL Server**, the class is read from the `string` SqlClient gives and sent as `nvarchar(max)`, as `json` is sent today.
- **The database types** it applies to without more: `json` and `jsonb` on PostgreSQL, `json` on SQL Server.  A column or parameter of any other type the base map reads as `string` takes it only by [the JSON opt-in](#the-json-opt-in).
- **A JSON `null`** read into a property that is not nullable throws `InvalidCastException("Column '<name>' holds a JSON null.")`; into a nullable one, it is null.  A SQL `NULL` never reaches the serializer.
- **An array** of a class, `jsonb[]` mapped to `OrderMetadata[]`, is converted element by element, as 8.2's arrays are.
- **A collection or a record** is a class for this rule: `List<Tag>` given to a `jsonb` column is serialized whole.
- **The serializer's default options** are case-sensitive and use the property names as written, so JSON written as camelCase reads as empty properties with no error.  The manual says so, and that a converter with options of the project's own is the fix.  The reflection-based serializer warns in a trimmed or AOT project, and the manual says to write a converter with a source-generated `JsonSerializerContext` there.

### The JSON opt-in

Decided: a column of a string type that holds JSON, SQL Server's `nvarchar(max)` before 2025 above all, gets a project's class through the serializer only when its mapping says so; a class mapped to a string column without it keeps 8.2's rules, so an `Email` mapped to `nvarchar` is never written as `{"Value":"..."}`.  Designer's call, its form:

```jsonc
// sqlsource.json: a column type's value as an object
{
    "columnTypes": {
        "orders.metadata": { "type": "OrderMetadata", "json": true },
        "orders.created_by": "UserId",
    },
}
```

```sql
-- column: orders.metadata -> json OrderMetadata
-- name: SaveOrder
-- param: @metadata -> json OrderMetadata
-- column: metadata -> json OrderMetadata
```

- **Where it is written**: the column types, in a settings file as the object form the settings-files design keeps free for a value, and in a file header's `-- column:`; a query's `-- column: name -> json Type`; and `-- param: @name -> json Type`.  Not in the type override, which maps a database type, not a column that holds JSON; a domain over `text` that holds JSON is mapped by its columns.
- **The word `json`** before the C# type in a marker, separated by white space; a C# type holds no white space, so the two never meet.  In the object form, `"json": true`, and `false` or no member is the plain mapping.
- **What it applies to**: a column or parameter whose base-map type is `string`, PostgreSQL's `text`, `varchar` and `char` and SQL Server's `char`, `varchar`, `nchar`, `nvarchar` and their `max`, read and sent as the serializer does for a JSON type, and bound as the column's type.  On a JSON database type it is allowed and changes nothing.
- **`SQLSRC283`**, an error at the place that says it: the opt-in on a column or parameter whose base-map type is not `string`, or with a C# type that the serializer rule does not take, a JSON type or a type the base map gives.  Reported for `codegen` claims only, as the converter diagnostics are; in a `models` query the opt-in is read, its grammar checked, and the type applied, as 8.7's column types are.
- **A project's converter** of the edge from `string` to the class replaces the serializer here too.
- **Not in the query's hash**, as no `-- column:` or `->` is, so adding it leaves the sidecar current.

### Generated conversions

Every conversion of this phase but the spatial read path is a converter, in the sense of 8.2: a static method of one argument, an edge between a library type and the type the base map reads, with no direction of its own.

- **Emitted** as `internal static` methods of an internal generated class per library, in the namespace `SqlSource` or one of its own, the builder's call, by a source output after the probe, only for the libraries whose assemblies the compilation references and only when some `codegen` query's plan uses an edge of it.  Not post-initialization text, and not listed in `GeneratedTypes` unless the suppressor needs it, which the builder checks.  The class's file holds `#nullable enable` and compiles warning-free on .NET 8 and 10.
- **A third origin**, `Library`, beside `Project` and `MappedType`.  The project's converter of an edge replaces a library's as it replaces a mapped type's, with no diagnostic; a mapped type's converter also replaces a library's.  So a project's `string → OrderMetadata` converter replaces the serializer for that class, and its `DateTime → Instant` replaces NodaTime's.
- **Built by the generator**, not found by the attribute step: generated source does not reach a syntax provider.  The generator adds the library's edges to `ConverterSet` as records it builds itself, with the method names it emits.
- **Applied where the type was chosen**: by an override, a column type, a marker, or now a library row.  A library row is a place of its own for `PropertyType.Place`, the builder's call how, so `ConversionPlanner` converts it and `MethodsInput.ConvertersOf` collects it; `SQLSRC250` to `SQLSRC252` word it as "by the NodaTime library".  On PostgreSQL a plugin's row is a place that has no edge, so the planner leaves it to the driver, as it leaves an override with no converter.
- **The serializer** is a kind of conversion, not an edge: a generic read and write between `string` and the class.  It applies where the property's type is a class as defined above, on a JSON database type, with no edge from the project or a mapped type.  It never makes `SQLSRC279`: a class with an edge to or from some other type and none of this pair keeps 8.2's rule.
- **The spatial read path** is a kind of conversion too, whose read is `GetSqlBytes` and whose write is the `SqlBytes` binding.  A project's converter between `SqlBytes` and `Geometry` is not a thing a user can write, since `B` for `geography` has no base-map type; the path is not replaceable, and a project that wants other behaviour maps the column to `Microsoft.SqlServer.Types.SqlGeography` and converts from there.
- **Scoped by engine and driver.**  `ConverterSet` knows no engine today, and one type's queries can span both.  A library edge carries the engines, and for JSON the drivers, it serves: NodaTime's and NetTopologySuite's on SQL Server only; `JsonElement`'s and `JsonDocument`'s on SQL Server only; `JsonNode`'s on SQL Server and on Npgsql 8 and 9; the serializer on both.  The planner uses a library edge only for a query of its engine and driver, so a PostgreSQL `timestamp` read as `LocalDateTime` goes to the plugin, never to SQL Server's `DateTime` edge.  How the edge carries this is the builder's call.
- **The foreign-type rule** (`ConversionPlanner`'s `Gives`) counts a library type as foreign, so a converter between `Instant` and another type still never touches a plain `DateTime` column.
- **A library row is exempt from `SQLSRC279` and `SQLSRC280`.**  Where a library row chose the type and no edge fits, the type goes to the driver, as the plugin reads it on PostgreSQL; a project's converter between `Instant` and `DateTimeOffset` must not turn every `timestamptz` the plugin reads into an error.  A type an override, a column type or a marker chose keeps 8.2's rules.

### The libraries' floors

Designer's call: each library has a floor, one for both engines, found by 7.0 in each package's metadata ([spike, question 6](2026-10-26-query-generation-phase-7-spike.md#6-the-probes-markers-and-the-libraries-floors)).  Every member the generated code calls exists from 2.0.0 of each library, so the floors are where the plugins or a behaviour put them:

| Library | Floor | Why | How the probe tells it |
|----|----|----|----|
| NodaTime | 3.1.9 | `Npgsql.NodaTime` 8.0.x needs it | The identity version of the assembly, `3.1.9.0` or later: no public symbol is new in it |
| NetTopologySuite | 2.5.0 | `Npgsql.NetTopologySuite` 8.0.x needs it | `NetTopologySuite.Algorithm.Hull.ConcaveHull`, first in 2.5.0; every 2.x is `2.0.0.0` |
| `NetTopologySuite.IO.SqlServerBytes` | 2.1.0 | It writes M values from the coordinates, and checks a `geography` polygon for emptiness before its ring orientation | The informational version, read as `SqlClientProbe` reads SqlClient's: 2.0.0 and 2.1.0 have the same public API and identity version |

A library below its floor that a `codegen` query uses is `SQLSRC282`, an error once per project naming the library, the version found and the floor; its queries keep their types and get no method, as below a driver's floor.  Decided by the owner on 2026-10-30: 7.2 delivers it for NodaTime, and 7.4 for NetTopologySuite and `NetTopologySuite.IO.SqlServerBytes`.  Each end-to-end floor project references each library at its floor; the main projects reference the newest.

### The probe

Designer's call: one new probe, `LibraryProbe`, a value-equal record of flags beside `NpgsqlProbe` and `SqlClientProbe`, built by 7.1 with NodaTime's markers and grown by 7.3 and 7.4 with their libraries', as the owner decided on 2026-10-30, read by marker symbols with `DriverProbes.Has` and combined into `FileTypesInput` and `MethodsInput`.  The markers, which 7.0 found public and present in every version of each package from its floor to its newest: for `NodaTime`, `NodaTime.Instant`; for `Npgsql.NodaTime`, `Npgsql.NpgsqlNodaTimeExtensions`; for `NetTopologySuite`, `NetTopologySuite.Geometries.Geometry`; for `Npgsql.NetTopologySuite`, `Npgsql.NpgsqlNetTopologySuiteExtensions`; for `NetTopologySuite.IO.SqlServerBytes`, `NetTopologySuite.IO.SqlServerBytesReader`; and for Npgsql 10's `JsonNode` support, the `NpgsqlProbe`'s existing Npgsql 10 flag, `AtLeast10`, whose `NpgsqlTypes.NpgsqlCube` is absent from 8.0.9 and 9.0.5.  `Geometry` and `SqlServerBytesReader` are also in the 1.x packages, over GeoAPI's types; the floors' versions above tell those apart.  System.Text.Json is always present on .NET 8.

## Diagnostics

| Id | Severity | Where | About |
|----|----|----|----|
| `SQLSRC281` | Error | The place that says `on`: the settings file's member or the marker's line | A library is on for a `codegen` query of an engine whose assemblies for it the project does not reference; names the library and the assemblies |
| `SQLSRC282` | Error | The project | A library that a `codegen` query uses is older than its floor; names the library, the version found and the floor |
| `SQLSRC283` | Error | The place that says `json` | The JSON opt-in on a column or parameter whose database type is not read as `string`, or with a C# type the serializer does not take |
| A settings file's value diagnostic | Error | The `libraryTypes` member | A library name or value that is not known; the settings-files work's id |
| `SQLSRC111`, existing | Error | The marker's line | A `-- library-types:` entry that is not `name=value`, or names an unknown library or value |

Changed: `SQLSRC246`'s help, for PostGIS and SQL Server's spatial types, names the library beside the type override; `SQLSRC250` to `SQLSRC252` word a library's place.  If a sub-phase needs another id, it takes the next free id and says so in its pull request.

## Sub-phases

| Sub-phase | Builds on | Delivers |
|----|----|----|
| 7.0 Before the maps | Nothing | The spike: each Verify first of this document answered against the containers and drivers, as a document beside the outline; this design amended where a finding changes a detail |
| 7.1 The switch, and NodaTime on PostgreSQL | 7.0, and the settings files on `main` | `libraryTypes` and `-- library-types:`, the probe, the library map's place in typing, `SQLSRC281`, and NodaTime on PostgreSQL through the plugin, end to end |
| 7.2 Generated conversions, and NodaTime on SQL Server | 7.1 | The `Library` origin, emission, the place of a library row in the planner, NodaTime on SQL Server, end to end, and `SQLSRC282` for NodaTime |
| 7.3 JSON | 7.2 | The `json` library and its name in the switch, the JSON types on both engines, and a project's own class through the serializer, end to end |
| 7.4 NetTopologySuite | 7.2 | `netTopologySuite` in the switch, PostGIS through the plugin, the tool's check, a pinned PostGIS image, SQL Server's spatial read path, end to end, and `SQLSRC282` for NetTopologySuite and `NetTopologySuite.IO.SqlServerBytes` |
| 7.5 The examples and close | 7.3 and 7.4 | Each library in each example project, the user documentation of the phase read whole, and the close |

7.3 and 7.4 can be built beside each other on 7.2.  Both touch `ConversionPlanner`, `QueryMethodWriter` and the binders, each adding a kind of conversion; each keeps its change to its own kind, and the later to merge reconciles.

### 7.0 Before the maps

A spike, as [8.0's](2026-10-24-query-generation-phase-8-0-before-the-methods-design.md) was: throwaway code against PostgreSQL 14 and 18 with Npgsql 8 and 10, and SQL Server 2017 and 2025 with SqlClient 6.1 and 7, its findings written as `docs/superpowers/specs/2026-10-26-query-generation-phase-7-spike.md`.  Each answer is what the code did; a question it cannot reach names the sub-phase that must.  A finding that contradicts a Decided item stops at the document and asks the owner.

1. **NodaTime on PostgreSQL**: each default of the table read by `GetFieldValue<T>` and written by `NpgsqlParameter<T>` with `DataTypeName` after `UseNodaTime()`, on a full and a slim builder, arrays, ranges and multiranges included; the plugin's version that pairs with Npgsql 8.
2. **NodaTime on SQL Server**: each conversion of the table, both ways, at the limits of each type.
3. **JSON**: `JsonElement`, `JsonDocument` and `JsonNode` read and written on each Npgsql with `DataTypeName` `json` and `jsonb`; a `string` with `DataTypeName` `jsonb` for a class's serialized text; `json` on SQL Server 2025 and `nvarchar(max)` on 2017 read as `string`.
4. **PostGIS**: an image that meets root `AGENTS.md` rule 5, named by PostgreSQL's major and minor, for each PostgreSQL image the end-to-end tests run on, 14 and 18 in CI and 15 to 17 at night; designer's call, built from the pinned `postgres` image with PostGIS installed at a pinned version, since the `postgis/postgis` tags name no minor.  If none can meet the rule, the spike stops and asks the owner; the type names and schema the describer reports for `geometry` and `geography`, and how a library row should match them; whether the tool's check refuses a PostGIS parameter, and whether registering `Npgsql.NetTopologySuite` in the tool's connection makes it pass.
5. **NetTopologySuite on SQL Server**: `GetSqlBytes` and `SqlServerBytesReader` on each server; binding `SqlBytes` as `Udt`; the default ordinates of the reader and the writer; whether they are safe across threads.
6. **The probe's markers** in each package's metadata, and each library's floor: the oldest version with every API the generated code calls, and a symbol that marks it.

### 7.1 The switch, and NodaTime on PostgreSQL

- `libraryTypes` in the settings file's reader, schema and agreement test; `-- library-types:` in the marker grammar, header and query; merged per library by the levels above; `SQLSRC281` and the grammar diagnostics.
- `LibraryProbe` and its markers.
- The library map in `QueryTyper`, step 4 of [where the library map stands](#where-the-library-map-stands), only for a `codegen` query; a library row's place in `PropertyType`.
- NodaTime's PostgreSQL rows, read and bound through the plugin with no generated code.
- **End to end.**  Designer's call: a folder of its own in each PostgreSQL end-to-end project, with a `sqlsource.json` that leaves the library on, while the project's root settings file turns NodaTime off, so that its Dapper tests, its `all_types`, and its realistic corpus, claimed under `models` and `codegen` with shared models, are untouched.  The folder's methods run on a data source that calls `UseNodaTime()`, beside the existing one.  Every row of NodaTime's PostgreSQL table written and read, nulls and arrays included, on 14 and 18, and in `Postgres.Npgsql8` with the plugin of Npgsql 8.  The opt-out is shown by the root `off`, and the detection by the folder.
- Generator tests: each level and its merge, the defaults by engine and by reference, `SQLSRC281`, a `models` query unchanged, the order of step 4 against an override and a column type, `SQLSRC257` for a model shared across outputs.  Caching tests: an edit to a `-- library-types:` marker re-types only its file's queries.

### 7.2 Generated conversions, and NodaTime on SQL Server

- The `Library` origin in `ConverterSet`, the emitted class, the place of a library row in `ConversionPlanner` and `MethodsInput.ConvertersOf`, and replacement by a project's and a mapped type's converter.
- NodaTime's SQL Server rows, its conversions of the table both defaults and by-override, and their emission only when a `codegen` query uses them.
- `SQLSRC282` for NodaTime below 3.1.9, read from `LibraryProbe`'s version of NodaTime, as the owner decided on 2026-10-30.
- **End to end**: a folder of its own in the SQL Server end-to-end project with NodaTime on, every row written and read through the methods on 2017 and 2025, a column as an `Instant` and a `Duration` by override, and the same in `SqlServer.SqlClient61`.  The allocation test covers one converted read and one write.
- Generator tests: the emitted class and its absence without a `codegen` use or without the assembly; a project's converter replacing a library's; the conversions offered with the switch off for an override; the generated code compiled against both drivers' metadata with no warning.

### 7.3 JSON

- The `json` library and `JsonElement` as its default, with `json` added to the switch's names, as the owner decided on 2026-10-30; the JSON types of the table on both engines; a project's own class through the serializer; the null rules; arrays.
- The JSON opt-in for string columns, in the settings file's `columnTypes`, the header's and a query's `-- column:` and `-- param:`, with `SQLSRC283`.
- **End to end**, on both engines and their floors: each JSON type read and written; a class and an array of a class; a JSON `null` into a nullable and a not-null property; a converter with its own options replacing the serializer for one class.  On PostgreSQL, the class with no `EnableDynamicJson()` on the data source.  On SQL Server 2017 and 2025, a class in an `nvarchar(max)` column by the opt-in, and a class in an `nvarchar` column without it left to 8.2's rules.
- Generator tests for each of those, and for a class that is not on a JSON type, which keeps 8.2's rules.

### 7.4 NetTopologySuite

- `netTopologySuite` added to the switch's names, with the probe's markers of NetTopologySuite, `Npgsql.NetTopologySuite` and `NetTopologySuite.IO.SqlServerBytes`, as the owner decided on 2026-10-30.
- The PostgreSQL rows through the plugin; the tool's check, as 7.0 found it must be.
- `SQLSRC282` for NetTopologySuite below 2.5.0 and `NetTopologySuite.IO.SqlServerBytes` below 2.1.0, as the owner decided on 2026-10-30.
- **The PostGIS image.**  Designer's call: a container of its own, started only by the NetTopologySuite tests, beside the end-to-end project's existing one, on the same PostgreSQL version as each run, CI's and the nightly's.  `DefaultImages.Postgres` does not change, so no fixture or sidecar is recorded again; the sidecars of the PostGIS queries are written against the PostGIS container by `tools/describe-end-to-end.sh`, which gains it.  The image and its versions are named in the places rule 5 lists, and rule 5 gains the PostGIS image's sentence.  7.0 built it from each pinned `postgres` image with PostGIS `3.6.4+dfsg-2.pgdg13+1` from the image's own pgdg source, on arm64 and amd64, by the Dockerfile in [the spike](2026-10-26-query-generation-phase-7-spike.md#4-postgis).  GEOS, PROJ and GDAL come with it unpinned, from pgdg and Debian, as the base image's own packages do.  7.4 takes that Dockerfile, which also reads pgdg's archive, `apt-archive.postgresql.org`, after installing `ca-certificates`, so the pinned version still builds once it leaves `apt.postgresql.org`.
- SQL Server's spatial read path and its write, the four static instances, the subtype cast.
- **End to end**, on both engines and their floors: `geometry` and `geography` written and read through the methods, a `Point` and a `Polygon` by override, SRIDs kept, nulls, an array on PostgreSQL; on SQL Server a clockwise `geography` shell's `ArgumentException`.
- Generator tests: the emitted fields and their absence without a use; the read and write text; an override to `SqlGeography` unchanged.

### 7.5 The examples and close

- Each example project's `CodeGen` folder gains a file that shows each library its database supports, by the rules of `examples/AGENTS.md`: on PostgreSQL NodaTime and NetTopologySuite detected and a class in `jsonb`, which needs PostGIS in the example's Compose image; on SQL Server NodaTime and the `json` library turned on, and a class in `json`.  If PostGIS in the example's image is not wanted, the PostgreSQL example shows NodaTime and JSON only, and the pull request says so.
- The user documentation of the phase read whole, by the phase map of `docs/manual/AGENTS.md`, and fixed.  The README's "NodaTime and other libraries" section, its callout replaced, with the call each plugin needs on the data source and the two traps; the manual's "What works today" row flipped.
- The close: the outline's rows set to Done, its phase 7 section's Settled list written, `docs/architecture/generator.md`'s placeholder replaced.

## Testing

Each sub-phase's testing is listed with it.  Across the phase:

- Every library row and every generated conversion written as a parameter and read as a column through generated code, on the newest driver and on the floor's.
- The existing end-to-end tests, the realistic corpora and the examples unchanged, byte for byte in their generated code, by the anti-goal.
- The allocation test of each end-to-end project extended to one converted read and write per engine; a library conversion allocates nothing a hand-written conversion would not, the serializer and the parse excepted.
- Caching tests for the switch's markers and settings, and for the probe: an edit to a `-- library-types:` marker re-types only its file; a library reference added or removed re-runs typing, models and methods, and nothing that does not depend on types.

## Documentation

Each sub-phase updates what it changes, by root `AGENTS.md` rule 4 and the phase map of `docs/manual/AGENTS.md`:

- `README.md`: "NodaTime and other libraries", with each plugin's `Use...()` call, the EF Core and Dapper traps, and a link to the manual; the FAQ on `InvalidCastException`, which gains a library whose plugin was not registered.
- `docs/manual/settings.md`: `libraryTypes` and `-- library-types:`, their levels and defaults; the converters section, where a library's conversions are replaced by the project's.
- `docs/manual/postgresql.md` and `docs/manual/sqlserver.md`: the Types sections, each library's rows; the Query methods sections, how its types are read and sent.
- `docs/manual/generator.md`: the order of a column's type with the library map; a model shared across outputs.
- `docs/manual/recipes.md`: a class in a JSON column, with and without options of its own.
- `docs/manual/diagnostics.md`: the new ids, and the changed help of `SQLSRC246`.
- `docs/architecture/generator.md`: the library map, the probe, the `Library` origin, the serializer and the spatial path.
- `src/SqlSource/AGENTS.md`: the ids taken.
- `docs/manual/AGENTS.md`: the phase 7 row of its phase map, updated by this design's pull request for the switch.
- The epic outline: the rows and the phase 7 section, updated by this design's pull request, and by each sub-phase for its row.

## Left to the builder

- The records and steps, within the caching rules; where the library rows live beside the base maps.
- The emitted class's name, namespace and layout, and the method names.
- How a library row is a place in `PropertyType`, and how the diagnostics word it.
- The text of each message.
- The ids, by the rule above.
- The folder names of the end-to-end tests and the example files.

## Calls made in this design

Not decided with the owner, and open to the review of this design:

- The switch's name, `libraryTypes` and `-- library-types:`, its values `on`, `off` and `default`, its merge per library, and its one value for both engines.
- The generator's conversions offered whenever a library's assemblies are referenced, with the switch deciding only the defaults.
- A project's own class in a JSON column needs no switch, and the serializer replaces the driver's route on PostgreSQL.
- `JsonElement` as the `json` library's default, and `JsonDocument` and `JsonNode` reached by override only.
- A JSON `null` into a not-null property throws.
- On SQL Server, `datetime` and `smalldatetime` are `LocalDateTime` like `datetime2`.
- NodaTime on PostgreSQL uses no generated conversion, even for an override the plugin does not read.
- The spatial read path is not replaceable by a project's converter.
- The tool registers `Npgsql.NetTopologySuite` for its check, if 7.0 finds it needed.
- The end-to-end libraries live in folders of their own, turned on there and off at the project's root.
- Typing gives a library-chosen property both types, and each claiming type picks by its output.
- A library edge is scoped to the engines and drivers it serves; a library row is exempt from `SQLSRC279` and `SQLSRC280`.
- A project that maps a type to NodaTime on PostgreSQL without the plugin keeps today's behaviour.
- The libraries' floors, below which `SQLSRC282` stops the methods.
- The JSON opt-in's form: the object form in `columnTypes`, the word `json` before the type in `-- column:` and `-- param:`, and no form in the type override.
- `SQLSRC111` for the marker's grammar, as for every marker.
- The spatial read goes through `GetStream` with no cast if that works through a wrapping connection, and otherwise casts the reader to `SqlDataReader`, a known issue for a wrapping connection.
- The class that holds the spatial instances has a static constructor, so that they are created at the first spatial query.
- One floor per library for both engines.
- A JSON `null` read into a not-null `JsonNode`, `JsonObject` or `JsonArray` throws, as into a not-null class.
- PostGIS in a container of its own, built from the pinned `postgres` image.
