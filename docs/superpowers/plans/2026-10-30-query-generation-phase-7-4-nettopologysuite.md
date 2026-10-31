# Query generation, phase 7.4: NetTopologySuite - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `netTopologySuite` in the library switch; PostGIS's `geometry` and `geography` read and sent through `Npgsql.NetTopologySuite` on PostgreSQL; SQL Server's `geography` and `geometry` read and sent through `NetTopologySuite.IO.SqlServerBytes` by a read path of their own; the tool's check of a PostGIS parameter; a pinned PostGIS image; `SQLSRC282` for NetTopologySuite below 2.5.0 and `NetTopologySuite.IO.SqlServerBytes` below 2.1.0; all of it end to end on both engines and their floors.

**Architecture:** 7.2's shape ("Shape for 7.3 and 7.4" of `7-2-notes.md`): a new `SqlLibrary` member with its `LibraryProbe` flags; library rows for both engines; on SQL Server a spatial branch of `ConversionPlanner.Plan` that sets a new field of `Conversion`, which `RowReaderWriter` and `SqlServerParameterBinder` write, and a generated class `SqlSource.NetTopologySuiteConversions` that holds only the four shared readers and writers.  PostgreSQL needs no generated code: the plugin reads the rows.

**Tech Stack:** C# (netstandard2.0 analyzer, Roslyn 4.8 floor), xunit v3, Shouldly, Testcontainers, Npgsql 8/10, Microsoft.Data.SqlClient 6.1/7, NetTopologySuite 2.5.0/2.6.0, `NetTopologySuite.IO.SqlServerBytes` 2.1.0, PostGIS 3.6.4.

**Spec:** `docs/superpowers/specs/2026-10-26-query-generation-phase-7-library-maps-design.md` (sections "7.4 NetTopologySuite", "NetTopologySuite", "Generated conversions", "The libraries' floors", "The probe"), with the spike `docs/superpowers/specs/2026-10-26-query-generation-phase-7-spike.md` (questions 4, 5, 6), and the hand-over notes `7-1-notes.md`, `7-2-notes.md` in the shared notes folder.

## Where merged code differs from the spec

- **A library row whose type has no C# type without the library.**  7.1's `QueryTyper.TypeOf` drops a library row when the type's mapping without the library fails, so the property never needs a type it cannot have.  PostGIS's types and SQL Server's spatial types fail without the library (`SQLSRC246`), so as merged the row would never apply.  This plan keeps the row and defers the failure to the claims whose output is not `codegen`: the property has `Library` set and `WithoutLibrary` null, the query keeps the failure's diagnostic, and a `models` claim of the query gets no models and that diagnostic (`SQLSRC246`, as today).  That is the spec's rule ("a `models` query keeps today's types"), built where 7.1 did not need it.
- **SQL Server's spatial types are CLR user types of `sys`.**  `SqlServerTypeMap.Map` asks the library rows only for a type without a user type ("A CLR user type never takes a row", 7.2).  This plan has it ask them for a CLR type of `sys` too, before the map's row.
- **`SQLSRC282` is per library, and NetTopologySuite is two packages on SQL Server.**  `NetTopologySuite.IO.SqlServerBytes` is used only by a SQL Server query, so a used library and a library below its floor are recorded with their engine (`LibraryUse`), and a PostgreSQL query is never stopped by an old `SqlServerBytes`.
- **The spatial read goes through `DbDataReader.GetStream(i)`**, with no cast to `SqlDataReader`, if a test through a connection that wraps SqlClient's reads it (Task 8).  If it does not, Task 8 changes the read to the cast and adds the known issue.

## Global Constraints

- The analyzer names no library type in its own code: every NetTopologySuite type is text it emits, `global::`-qualified.
- Floors: NetTopologySuite 2.5.0 (told by `NetTopologySuite.Algorithm.Hull.ConcaveHull`), `NetTopologySuite.IO.SqlServerBytes` 2.1.0 (told by its informational version, read as `SqlClientProbe` reads SqlClient's), one floor for both engines.
- Markers: `NetTopologySuite.Geometries.Geometry`, `Npgsql.NpgsqlNetTopologySuiteExtensions`, `NetTopologySuite.IO.SqlServerBytesReader`.
- Packages: main projects NetTopologySuite 2.6.0, `Npgsql.NetTopologySuite` 10.0.3, `NetTopologySuite.IO.SqlServerBytes` 2.1.0; floor projects `Npgsql.NetTopologySuite` 8.0.9 and NetTopologySuite 2.5.0 by `VersionOverride` (central transitive pinning), `SqlServerBytes` 2.1.0.
- PostGIS `3.6.4+dfsg-2.pgdg13+1`, built on the run's pinned `postgres` image by the spike's Dockerfile, which reads `apt-archive.postgresql.org` after `ca-certificates`.  `DefaultImages.Postgres` does not change, so no fixture or existing sidecar is recorded again.
- Defaults: PostgreSQL on when `Npgsql.NetTopologySuite` is referenced; SQL Server off; available on SQL Server when NetTopologySuite and `SqlServerBytes` are referenced.
- Anti-goal: the existing end-to-end tests, the realistic corpora and the examples keep their generated code byte for byte.
- Diagnostic ids: none expected; any new one takes `SQLSRC290` and up.  `SQLSRC246`'s help is this unit's to change.
- 7.3 (JSON) is built beside this unit: touch `ConversionPlanner`, `QueryMethodWriter`/`RowReaderWriter`, `ConverterSet`/`LibraryConverters`, the binders and `LibraryRows.Of` only with additions for the spatial kind; do not restructure.  Do not add `json` anything.
- Every commit passes `./pre-commit-validation.sh`; the repository's formatter (`dotnet csharpier format .`, as `CONTRIBUTING.md` names it) runs before validating.

## Review Focus

1. **A `models` claim of a query that has a PostGIS or spatial column while the library is on** must still give `SQLSRC246` and no models, not a `Geometry` property and not silence (Task 2 test `Run_ModelsClaimOfALibraryOnlyColumn_Reports246AndNoModels`).
2. **A query claimed `codegen` by one type and `models` by another** gets `Geometry` in the first and `SQLSRC246` for the second (Task 2 test `Run_CodegenAndModelsClaims_EachGetsItsOwn`).
3. **A SQL Server override to `Microsoft.SqlServer.Types.SqlGeography`** keeps 8.4's `Udt` binding and `GetFieldValue<SqlGeography>` read, with or without NetTopologySuite referenced (Task 4 test `Plan_OverrideToSqlGeography_IsUnchanged`).
4. **A project's converter between `Geometry` and some type** does not replace or block the spatial path, and no `SQLSRC279` comes from the spatial path (Task 4 test `Plan_ProjectConverterOfGeometry_DoesNotReplaceTheSpatialPath`).
5. **A PostgreSQL-only project with an old `SqlServerBytes`** gets no `SQLSRC282` and keeps its methods (Task 5 test `Run_PostgresUseWithOldSqlServerBytes_ReportsNothing`).

---

### Task 1: `netTopologySuite` in the switch, and the probe

**Files:**
- Modify: `src/SqlSource/Settings/Libraries/SqlLibrary.cs`, `src/SqlSource/Generation/Types/Drivers/LibraryProbe.cs`, `src/SqlSource/SqlSourceGenerator.cs` (the typing probe's `Select`), `src/SqlSource/Generation/Methods/MethodPlanner.cs` (`LibraryName`), `src/SqlSource/Generation/Types/Maps/LibraryRows.cs` (`PlaceOf` arm only), `schemas/settings-v1.schema.json` (if the pattern of names is not derived)
- Modify: `tests/SqlSource.Testing/Generator/LibraryReferences.cs`, `tests/SqlSource.Testing/SqlSource.Testing.csproj` (`PackageDownload` NetTopologySuite `[2.5.0];[2.6.0]`, `Npgsql.NetTopologySuite` `[8.0.9];[10.0.3]`, `NetTopologySuite.IO.SqlServerBytes` `[2.0.0];[2.1.0]`)
- Test: the existing `LibraryProbeTests`, `LibrarySettingsTests`/schema tests, `LibraryDiagnosticsTests` (SQLSRC281) in `tests/SqlSource.Tests/`

**Interfaces:**
- Produces: `SqlLibrary.NetTopologySuite` (after `NodaTime`).  `LibraryProbe(bool NodaTime, bool NpgsqlNodaTime, Version? NodaTimeVersion, bool NetTopologySuite, bool NetTopologySuiteAtFloor, string? NetTopologySuiteVersion, bool NpgsqlNetTopologySuite, bool SqlServerBytes, string? SqlServerBytesVersion)`; new members default so existing `new LibraryProbe(a, b, c)` calls in tests can stay or be updated.  `IsAvailable(NetTopologySuite, PostgreSql)` = `NetTopologySuite && NpgsqlNetTopologySuite`; on SQL Server `NetTopologySuite && SqlServerBytes`.  `MissingAssembliesOf(NetTopologySuite, PostgreSql)` = `"Npgsql.NetTopologySuite"`, on SQL Server `"NetTopologySuite.IO.SqlServerBytes"`.  `LibraryRows.PlaceOf(NetTopologySuite)` = `"the NetTopologySuite library"`; `MethodPlanner.LibraryName(NetTopologySuite)` = `"NetTopologySuite"`.
- `NetTopologySuiteAtFloor` is `DriverProbes.Has(compilation, "NetTopologySuite.Algorithm.Hull.ConcaveHull")`.  The two versions are the informational versions of the assemblies that hold `Geometry` and `SqlServerBytesReader` (package version, `+commit` dropped), null when absent; the typing probe nulls both, beside `NodaTimeVersion`.

- [ ] **Step 1: Tests.**  `Read_NetTopologySuite260WithPlugin_SetsEveryFlag` (real 2.6.0 + `Npgsql.NetTopologySuite` 10.0.3: `NetTopologySuite`, `NetTopologySuiteAtFloor`, `NpgsqlNetTopologySuite` true, `NetTopologySuiteVersion` `"2.6.0"`); `Read_SqlServerBytes210_SetsItsVersion` (`"2.1.0"`); `Read_NetTopologySuiteSynthetic240_IsNotAtTheFloor` (a synthetic assembly with `Geometry` and no `ConcaveHull`); `IsAvailable_NetTopologySuite_ByEngine` (four rows); `DefaultOf_NetTopologySuite_OnOnlyOnPostgresWithThePlugin`; a settings-file and a marker test that `netTopologySuite=on` reads and `netTopologySuites=on` is the grammar diagnostic; `Run_NetTopologySuiteOnWithoutSqlServerBytes_Reports281` naming `NetTopologySuite.IO.SqlServerBytes`.  Extend `LibraryReferences` with `NetTopologySuite(version)`, `NpgsqlNetTopologySuite(version)`, `SqlServerBytes(version)` and a synthetic of NetTopologySuite's types (`Geometry`, `Point`, `LineString`, `Polygon`, `MultiPoint`, `MultiLineString`, `MultiPolygon`, `GeometryCollection`, `LinearRing` as empty classes deriving from `Geometry`, optionally `ConcaveHull`).
- [ ] **Step 2:** run them, see them fail.
- [ ] **Step 3:** implement; the schema's pattern of names and `SQLSRC111`'s help follow from the enum (7.1 notes), check `SettingsFileSchemaTests`.
- [ ] **Step 4:** `dotnet test --project tests/SqlSource.Tests` passes.
- [ ] **Step 5:** commit `feat(libraries): netTopologySuite in the switch, and its probe`.

### Task 2: PostGIS rows, and a library row whose type has no other

**Files:**
- Modify: `src/SqlSource/Generation/Types/Maps/LibraryRows.cs`, `src/SqlSource/Generation/Types/QueryTyper.cs` (`TypeOf`, `Diagnose`), `src/SqlSource/Generation/Types/QueryTypes.cs` (or wherever `QueryTypes` is), `src/SqlSource/Generation/Models/QueryModels.cs`, `src/SqlSource/Generation/Models/ModelReader.cs`, `src/SqlSource/Generation/Models/TypeModels.cs`, `src/SqlSource/SqlSourceGenerator.cs` (a reporting step), `src/SqlSource/TrackingNames.cs` (if each step has a name)
- Test: `tests/SqlSource.Tests/` beside 7.1's library typing tests and `LibraryRowsTests`

**Interfaces:**
- Consumes: Task 1's `SqlLibrary.NetTopologySuite`, `PlaceOf`.
- Produces:
  - `LibraryRows.Of(IReadOnlyList<SqlLibrary>)` returns one cached instance for each set of the libraries (NodaTime, NetTopologySuite, both): the smallest change, a small array indexed by a bit per library.  If 7.3's commit that does this exists unmerged, cherry-pick it with `-x` instead and add NetTopologySuite's rows to it.
  - `LibraryRows.Find(PostgresType)` also matches a type of `kind` `base` whose `InternalName` is `geometry` or `geography`, ignoring case, in any schema, giving `global::NetTopologySuite.Geometries.Geometry`, a reference type, with `Detail` `PlaceOf(NetTopologySuite)` and `Library` NetTopologySuite.  Arrays and domains follow from `PostgresTypeMap.Map`'s walk (`Geometry[]`, the base's).
  - `QueryTypes` and `QueryModels` gain `DiagnosticInfo? WithoutLibraryFailure`: the first failure the query has without its libraries, set when a library row kept a property whose type without the library failed.  Such a property has `Library` set and `WithoutLibrary` null.  The typing diagnostics of the file do not hold it.
  - `TypeModels.Of` asks no models of such a query for a claim whose output is not `codegen`; a new `TypeModels.LibraryFailuresOf(TypeQueries, EquatableArray<FileModels>) : EquatableArray<DiagnosticInfo>` gives each such query's failure once per claim, reported by its own source output (`ReportAll`).
  - `SQLSRC246`'s help for a PostGIS type (`Unreadable`, `kind` `base`, `InternalName` `geometry`/`geography`): `Reference Npgsql.NetTopologySuite to read it as NetTopologySuite's Geometry in a codegen query, or map it with the type override, as <key>=<a C# type>`.

- [ ] **Step 1: Tests.**  `Find_PostgisTypeInAnySchema_IsGeometry` (`public.geometry`, `gis.geography`); `Find_PostgisNameOfAnotherKind_IsNull` (a domain named `geometry`); `Of_EachSetOfLibraries_IsTheSameInstance`; generator runs with `Npgsql.NetTopologySuite` referenced: `Run_PostgisColumnsInCodegen_AreGeometry` (`geometry`, `geography`, `geometry[]` as `Geometry[]`, a domain over `geometry`); `Run_PostgisWithLibraryOff_Reports246WithTheNewHelp`; `Run_ModelsClaimOfALibraryOnlyColumn_Reports246AndNoModels`; `Run_CodegenAndModelsClaims_EachGetsItsOwn`; `Run_OverrideOfPublicGeometryToPoint_WinsOverTheRow` (`public.geometry=NetTopologySuite.Geometries.Point`).  7.1's NodaTime typing tests pass unchanged.
- [ ] **Step 2:** run, fail.
- [ ] **Step 3:** implement.  In `TypeOf`, when the mapping without the library fails, keep the library mapping, set `WithoutLibrary` null and record the failure's `Diagnose(...)` for the query (`TypeQuery` collects it, the first one, into `QueryTypes`).  Keep NodaTime's behaviour: its rows always have a base type, so nothing of NodaTime changes.
- [ ] **Step 4:** `dotnet test --project tests/SqlSource.Tests` passes, the caching tests included (add the new tracking name where the tests list every step).
- [ ] **Step 5:** commit `feat(types): PostGIS's rows, with the failure of a library-only type kept for models claims`.

### Task 3: SQL Server's spatial rows

**Files:**
- Modify: `src/SqlSource/Generation/Types/Maps/LibraryRows.cs`, `src/SqlSource/Generation/Types/Maps/SqlServerTypeMap.cs` (`Map`'s user-type branch), `src/SqlSource/Generation/Types/QueryTyper.cs` (`Diagnose`, `NeedsSqlServerTypes` arm)
- Test: `LibraryRowsTests`, `SqlServerTypeMapTests`, the SQL Server library typing tests

**Interfaces:**
- Consumes: Task 2's `LibraryRows`, `WithoutLibraryFailure`.
- Produces: `LibraryRows.Find(SqlServerType)` also gives `global::NetTopologySuite.Geometries.Geometry` for a CLR type of `sys` named `geography` or `geometry`, ignoring case; `SqlServerTypeMap.Map` asks it after the override and before the CLR row.  `PropertyType.Key` of such a column is `geography` or `geometry`, which Task 4 reads.  `SQLSRC246`'s help for `geography`/`geometry` (and not `hierarchyid`): `Reference NetTopologySuite.IO.SqlServerBytes and turn the netTopologySuite library on, to read it as NetTopologySuite's Geometry in a codegen query, or reference the package Microsoft.SqlServer.Types and map it with the type override, as geography=Microsoft.SqlServer.Types.SqlGeography`.

- [ ] **Step 1: Tests.**  `Run_SqlServerSpatialWithLibraryOn_IsGeometry`; `Run_SqlServerSpatialWithLibraryOff_Reports246WithTheNewHelp`; `Run_HierarchyId_KeepsItsHelp`; `Run_ModelsClaimOfSqlServerSpatial_Reports246`.
- [ ] **Step 2–4:** fail, implement, pass.
- [ ] **Step 5:** commit `feat(types): SQL Server's geography and geometry as NetTopologySuite's Geometry`.

### Task 4: The spatial read path and its write

**Files:**
- Create: `src/SqlSource/Generation/Converters/NetTopologySuiteConversions.cs`
- Modify: `src/SqlSource/Generation/Converters/LibraryConverters.cs` (`SourceOf`, `ClassMetadataNames`), `src/SqlSource/Generation/Methods/Conversion.cs`, `ConversionContext.cs`, `ConversionPlanner.cs`, `MethodDrivers.cs`, `MethodPlanner.cs` (passes the flag), `src/SqlSource/SqlSourceGenerator.cs` (drivers from the typing library probe too), `src/SqlSource/Generation/Emitting/RowReaderWriter.cs`, `src/SqlSource/Generation/Emitting/SqlServerParameterBinder.cs`
- Test: `ConversionPlannerTests`, `LibraryConvertersTests`, emitted-text tests of the SQL Server methods, a compile test

**Interfaces:**
- Consumes: Task 3's rows; Task 1's probe.
- Produces:
  - `MethodDrivers` gains `bool SqlServerSpatial` (`probe.NetTopologySuite && probe.SqlServerBytes`, from the typing probe); `ConversionContext` gains `bool SqlServerSpatial`.
  - `Conversion` gains `bool IsSpatial = false`.  For a spatial conversion, `Method` is the instance it calls, `global::SqlSource.NetTopologySuiteConversions.GeographyReader` / `GeographyWriter` / `GeometryReader` / `GeometryWriter`, `DatabaseText` `global::System.Data.SqlTypes.SqlBytes`, `DatabaseIsValueType` false, `IsByElement` false, `Library` NetTopologySuite.
  - `ConversionPlanner.Plan`: before any edge, when `context.Engine` is SQL Server, `context.SqlServerSpatial`, `type.Key` is `geography` or `geometry` ignoring case, and the property's text is one of NetTopologySuite's geometry types (`Geometry`, `Point`, `LineString`, `LinearRing`, `Polygon`, `MultiPoint`, `MultiLineString`, `MultiPolygon`, `GeometryCollection` of `global::NetTopologySuite.Geometries.`), the spatial conversion; nothing reported.  Any other type, `SqlGeography` included, goes on as today.
  - `NetTopologySuiteConversions`: `ClassName` `global::SqlSource.NetTopologySuiteConversions`, `MetadataName` `SqlSource.NetTopologySuiteConversions`, `Source`: `GeneratedText.Header`, `#nullable enable`, `internal static class NetTopologySuiteConversions` in namespace `SqlSource` holding only the four `internal static readonly` fields (`new global::NetTopologySuite.IO.SqlServerBytesReader { IsGeography = true }` and so on; geometry's with no initializer) and an empty `static NetTopologySuiteConversions() { }` with a comment that it makes the fields at the first spatial query, after the application has set `NtsGeometryServices.Instance`.  No ordinates set.
  - Read text: `(T)<Reader>.Read(<reader>.GetStream(i))`, the cast left out when `T` is `global::NetTopologySuite.Geometries.Geometry`; behind `IsDBNull` for a nullable column, as today.
  - Write text: `new global::System.Data.SqlTypes.SqlBytes(<Writer>.Write(v))`, a null as `global::System.Data.SqlTypes.SqlBytes.Null`, with the existing `Udt` and `UdtTypeName` lines of `geography`/`geometry`.

- [ ] **Step 1: Tests.**  `Plan_GeographyAsGeometry_IsTheSpatialPath`; `Plan_GeometryAsPointByOverride_IsTheSpatialPath` (`Method` the geometry reader); `Plan_SpatialWithoutSqlServerBytes_IsTheDriver` (null, as today); `Plan_OverrideToSqlGeography_IsUnchanged`; `Plan_ProjectConverterOfGeometry_DoesNotReplaceTheSpatialPath`; `Plan_PostgresGeometry_IsTheDriver`; emitted text of a method that reads a nullable `geography` as `Geometry`, a not-null `geometry` as `Point`, and binds both (exact lines); `Run_SpatialMethod_EmitsTheClassOnce` and `Run_NoSpatialUse_EmitsNoClass`; `ClassMetadataNames_ComparedWithTheSources_AreEveryClassTheyDeclare` passes with the new class; a compile test of the generated methods and class against SqlClient 6.1 and 7 metadata with NetTopologySuite 2.5.0 and 2.6.0 and `SqlServerBytes` 2.1.0, with no warning.
- [ ] **Step 2–4:** fail, implement, pass `dotnet test --project tests/SqlSource.Tests`.
- [ ] **Step 5:** commit `feat(methods): SQL Server's spatial read path and its write`.

### Task 5: `SQLSRC282` for NetTopologySuite and `SqlServerBytes`

**Files:**
- Create: `src/SqlSource/Generation/Methods/LibraryUse.cs`
- Modify: `LibraryProbe.cs` (`IsBelowTheFloor`, `FloorOf`, `VersionTextOf` per package), `MethodsInput.cs` (`BelowTheFloorOf`, `IsBelowTheFloor`), `MethodPlanner.cs` (`AddUsedLibraries` with the query's engine), `TypeMethods.cs` (`UsedLibraries`), `SqlSourceGenerator.cs` (`ReportLibrariesBelowTheFloor`)
- Test: `LibraryDiagnosticsTests`

**Interfaces:**
- Produces: `readonly record struct LibraryUse(SqlLibrary Library, DatabaseEngine Engine)`.  `MethodsInput.LibrariesBelowTheFloor` and `TypeMethods.UsedLibraries` become `EquatableArray<LibraryUse>`; `ConversionLibraries` stays `LibraryValue`.  `LibraryProbe.IsBelowTheFloor(SqlLibrary, DatabaseEngine)`: NodaTime as today on both; NetTopologySuite when `NetTopologySuite && !NetTopologySuiteAtFloor`, and on SQL Server also when `SqlServerBytes` is below 2.1.0 by its version.  `LibraryProbe.PackagesBelowTheFloor(SqlLibrary, DatabaseEngine) : IEnumerable<(string Name, string Version, string Floor)>`, which the report turns into one `SQLSRC282` each, distinct: `("NetTopologySuite", "2.4.0", "2.5.0")`, `("NetTopologySuite.IO.SqlServerBytes", "2.0.0", "2.1.0")`.

- [ ] **Step 1: Tests.**  `Run_NetTopologySuite240OnPostgres_Reports282` and no method; `Run_SqlServerBytes200OnSqlServer_Reports282` (synthetic NetTopologySuite at its floor, `SqlServerBytes` with informational version `2.0.0`); `Run_PostgresUseWithOldSqlServerBytes_ReportsNothing`; `Run_BothBelowOnSqlServer_ReportsTwo`; `Run_NodaTimeBelowTheFloor_StillReports282` (existing tests unchanged).
- [ ] **Step 2–4:** fail, implement, pass.
- [ ] **Step 5:** commit `feat(diagnostics): SQLSRC282 for NetTopologySuite and its SQL Server package`.

### Task 6: The tool's check of a PostGIS parameter

**Files:**
- Modify: `Directory.Packages.props` (`NetTopologySuite` 2.6.0, `Npgsql.NetTopologySuite` 10.0.3, `NetTopologySuite.IO.SqlServerBytes` 2.1.0, each with a comment as its neighbours have), `src/SqlSource.Tool/SqlSource.Tool.csproj`, `src/SqlSource.Tool/packages.lock.json`, `src/SqlSource.Tool/Describing/Postgres/PostgresConnection.cs` (`BuildSource` calls `builder.UseNetTopologySuite()`), every `packages.lock.json` that changes
- Test: proven by Task 7's describe and check against PostGIS; a describer test on the plain image that a parameter of `seg`-like unknown extension is still `SQLSRC232` stays as it is.

- [ ] **Step 1:** add the reference and the call, with a comment saying why (so a PostGIS parameter is described and checked).
- [ ] **Step 2:** `dotnet test --project tests/DatabaseTests/Postgres` passes on the default image (nothing else changes, as the spike found), and the floor image `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`.
- [ ] **Step 3:** commit `feat(tool): the PostgreSQL check binds PostGIS parameters through Npgsql.NetTopologySuite`.

### Task 7: The PostGIS image, its container, and PostgreSQL end to end

**Files:**
- Create: `tests/DatabaseTests/TestHarness/Postgis/Dockerfile` (the spike's, `ARG PG` the run's tag, `ARG POSTGIS=3.6.4+dfsg-2.pgdg13+1`), a harness helper that builds it through Testcontainers' `ImageFromDockerfileBuilder` as `sqlsource-postgis:<postgres tag>-3.6.4`, kept for reuse, and a way for `PostgresContainer` to run on it (a subclass or a hook; the harness stays driver-free)
- Create: `tests/DatabaseTests/Postgres/PostgisDatabases.cs` (database `gis` from `EndToEnd/gis/schema.sql` and `seed.sql`, on the PostGIS image of `EngineImage.Postgres.Image`), `UsesPostgis.cs`, `EndToEnd/gis/schema.sql` (`CREATE EXTENSION postgis;` and table `spatial_values`: `id`, `geometry_value geometry`, `geography_value geography`, `point_value geometry(Point,4326)`, `polygon_value geometry(Polygon,4326)`, `geometry_array_value geometry[]`, a domain column), `EndToEnd/gis/seed.sql` (empty or a row), `EndToEnd/NetTopologySuite/sqlsource.json` (`"database": "gis"`, `"libraryTypes": {"netTopologySuite": "default"}`), `EndToEnd/NetTopologySuite/Queries/NetTopologySuite.sql` (insert and read of every column, a `Point` and a `Polygon` by a header `-- type-override:`), `NetTopologySuiteQueries.cs`, `NetTopologySuiteQueryTests.cs`, a check test of the `gis` database in the PostGIS collection
- Modify: `tests/DatabaseTests/Postgres/sqlsource.json` (`"netTopologySuite": "off"`), `Postgres.csproj` (`Npgsql.NetTopologySuite`), `EndToEndSources.cs` (`GisWithNetTopologySuite`), `CheckTests.cs` (names `--database` app, billing, realistic, since `gis` is checked on its own container), `tools/describe-end-to-end.sh` (builds the image from the default tag, starts it, creates `gis`, deletes and rewrites `EndToEnd/NetTopologySuite/Queries` sidecars, passes `--connection gis=...`), `tests/DatabaseTests/Postgres.Npgsql8/` (links the queries and sidecar, its own copy of the tests, its own PostGIS container on `EngineImage.Postgres.Default`, `Npgsql.NetTopologySuite` 8.0.9 and NetTopologySuite 2.5.0 by `VersionOverride`), `tools/test-scopes.txt` if a scope line is needed, the AGENTS.md of each folder touched (Postgres, EndToEnd, Npgsql8, DatabaseTests)
- Test: the new tests on `postgres:18.6` and `postgres:14.24`

**Interfaces:**
- Consumes: Tasks 1, 2 and 6.

- [ ] **Step 1: Tests**, each through the generated methods on a data source with `UseNetTopologySuite()`, in a rolled-back transaction: `geometry` and `geography` written and read equal, SRID 4326 and 3857 kept on `geometry`, 4326 on `geography`; a `Point` and a `Polygon` by override read back as those types; nulls of every column; `geometry[]` with a null element; a ZM point keeps Z and M.  The check test: `describe --check` of the project's `gis` entries is all same.
- [ ] **Step 2:** run `tools/describe-end-to-end.sh` and read its diff: only the new folder's sidecars appear; no existing sidecar changes.
- [ ] **Step 3:** make the tests pass on the default image and on `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`, and the floor project's.
- [ ] **Step 4:** commit `test(e2e): NetTopologySuite on PostgreSQL through Npgsql.NetTopologySuite, on a pinned PostGIS image`.

### Task 8: SQL Server end to end

**Files:**
- Create: `tests/DatabaseTests/SqlServer/EndToEnd/NetTopologySuite/sqlsource.json` (`"libraryTypes": {"netTopologySuite": "on"}`), `Queries/NetTopologySuite.sql` (insert and read of `dbo.spatial_values`), `Queries/NetTopologySuiteOverrides.sql` (a header override to `Point` and `Polygon`), `NetTopologySuiteQueries.cs`, `NetTopologySuiteQueryTests.cs`, a test-only wrapping `DbConnection`/`DbCommand`/`DbDataReader` that forwards to SqlClient's
- Modify: `EndToEnd/app/schema.sql` (`dbo.spatial_values`: `id`, `geography_value geography`, `geometry_value geometry`, `point_value geography`, `polygon_value geometry`, no seed), `SqlServer.csproj` (NetTopologySuite, `SqlServerBytes`), `RepositoryPaths.cs` and `CheckTests.cs` (its folder in the list), `tools/describe-end-to-end.sh` (deletes and rewrites its sidecars), `tests/DatabaseTests/SqlServer.SqlClient61/` (links, own copy of the tests, NetTopologySuite 2.5.0 by `VersionOverride`, `SqlServerBytes` 2.1.0), the AGENTS.md of each folder touched

- [ ] **Step 1: Tests** through the methods in a rolled-back transaction: `geography` (SRID 4326) and `geometry` (SRID 0 and 3857) written and read equal, SRID kept; a `Point` and a `Polygon` by override; nulls; a ZM point keeps Z and M; a clockwise `geography` polygon's shell throws `ArgumentException` from the write; `geography` of SRID 0 fails at the server (assert the `SqlException`, the manual's case).  The wrapping connection: a query with no parameters reads a `geography` through the method on the wrapper.
- [ ] **Step 2:** run them on 2025 and on `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`, and the floor project.  If the wrapper's read fails, change Task 4's read to `((global::Microsoft.Data.SqlClient.SqlDataReader)reader).GetSqlBytes(i).Value` with `Read(byte[])`, flip the test to assert the cast's `InvalidCastException`, and say so in the hand-back.
- [ ] **Step 3:** run `tools/describe-end-to-end.sh`; only the new sidecars appear.
- [ ] **Step 4:** commit `test(e2e): NetTopologySuite on SQL Server through the spatial read path`.

### Task 9: Documentation, and the rule

**Files:**
- Modify: root `AGENTS.md` rule 5 (one sentence: the PostGIS image is built by `tests/DatabaseTests/TestHarness/Postgis/Dockerfile` from the run's pinned `postgres` image with PostGIS `3.6.4+dfsg-2.pgdg13+1`, the version named in that file alone, and moving it runs the PostGIS tests on each image CI and the nightly run; nothing is recorded on it but the sidecars of `EndToEnd/NetTopologySuite/`, written by `tools/describe-end-to-end.sh`), `README.md` (NetTopologySuite out of the library callout, with `UseNetTopologySuite()`; FAQ on `InvalidCastException`), `docs/manual/README.md` (What works today), `docs/manual/settings.md` (library types: `netTopologySuite`, defaults by engine), `docs/manual/postgresql.md` (Types row for PostGIS, its subsection, `SQLSRC232` row without PostGIS), `docs/manual/sqlserver.md` (Types row, a NetTopologySuite subsection: static instances, `NtsGeometryServices.Instance` at startup, EF Core's defaults, SRID 0 on `geography`, ring orientation, subtype cast, SRID from the bytes; the wrapping connection's known issue if Task 8 kept the cast), `docs/manual/diagnostics.md` (`SQLSRC246` help, `SQLSRC232` list, `SQLSRC281` and `SQLSRC282` mentions), `docs/manual/parameters-and-tokens.md` and `docs/tech-debt/TD-0093-*.md` (write `geometry` without its modifier), `docs/manual/generator.md` if a known issue changes, `docs/architecture/generator.md` (the PostGIS rows, the deferred failure, the spatial path, the class, `LibraryUse`; the placeholder names System.Text.Json alone), `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (7.4 row Done), `docs/manual/AGENTS.md` phase map if it changes, `tests/README.md`, `tests/DatabaseTests/README.md`, `tests/DatabaseTests/Postgres/README.md` (the PostGIS image, its container, the `gis` database), `tests/DatabaseTests/SqlServer/README.md`, `.github/workflows/README.md` if a workflow changes, `CONTRIBUTING.md` if building the image needs anything of a developer.
- Test: `tests/SqlSource.Tests/Documentation/` (links, diagnostics, AGENTS sizes) pass.

- [ ] **Step 1:** write them by root `AGENTS.md` rule 4: user pages say effects, never the describer or a TD id in the packed readmes.
- [ ] **Step 2:** `dotnet test --project tests/SqlSource.Tests` passes.
- [ ] **Step 3:** commit `docs: NetTopologySuite on both engines, and the PostGIS image`.
