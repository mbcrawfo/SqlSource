# Query generation, phase 6.4: the SQL Server type map - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An `mssql` file with a current sidecar gets its models: every type Microsoft.Data.SqlClient 6.1 and 7 read has its C# type, the project's SqlClient is probed with its floor, the type override reads SQL Server's names per engine, a table model of `dbo` drops its schema, and `SQLSRC243` is reported for no dialect.

**Architecture:** A pure `SqlServerTypeMap` beside `PostgresTypeMap` turns a `SqlServerType` into the same `TypeMapping` under a `SqlClientColumn` that a new `SqlClientProbe` step chooses.  `QueryTyper` dispatches on the class of the sidecar type.  The type override keeps one setting with one grammar, but a level now holds two sorted arrays, PostgreSQL's (exactly today's) and SQL Server's, each keyed by that engine's canonical key, so `TypeOverrideMap` is unchanged and every PostgreSQL answer stays as it is.

**Tech Stack:** C# on `netstandard2.0`, Roslyn incremental generators pinned to 4.8.0, xunit v3 with Shouldly, `PackageDownload` for the SqlClient assemblies the probe tests read as metadata.

**Spec:** [`docs/superpowers/specs/2026-10-20-query-generation-phase-6-4-type-map-design.md`](../specs/2026-10-20-query-generation-phase-6-4-type-map-design.md).  Read it with this plan; its base map table, its override rules and its "Calls made" bind every task.  The map's rows come from the spec's research appendix (Task 1), never from memory.

## Where this plan departs from the spec, and why

The merged code is as the spec's "What exists today" says.  The plan follows it, with these choices the spec left to the builder or that the code forces:

- **Where the canonical keys are stored.**  A level holds two arrays: `SettingsLevel.TypeOverrides`, PostgreSQL's, exactly what it holds today, and a new `SettingsLevel.SqlServerTypeOverrides`, the same entries read by SQL Server's rules.  An entry with no key in one engine is absent from that engine's array.  `TypeOverrideMap` (merge, last-wins, ranking) runs on each array unchanged, and no existing test of the PostgreSQL array sees an extra entry.  A single array of entries tagged by engine was weighed and dropped: every existing test of a read level would gain SQL Server's entries.
- **A marker's key is read by its file's engine alone.**  A `-- type-override:` marker applies only to its file's queries, whose engine is the file's dialect (`mssql` is SQL Server, every other dialect PostgreSQL, as today).  So a marker fills one array, conflicts are `SQLSRC112` by that engine's key, and a key with no key in that engine (`nvarchar=X` in a `postgres` file, `int4=X` in an `mssql` file) is `SQLSRC111`, as `nvarchar` is in a `postgres` file today.  An MSBuild value, which serves both engines, accepts a key that either engine reads.
- **`sys.` on PostgreSQL.**  The spec says both that PostgreSQL's canonical key is "exactly what a key reads as today" and that `sys.int` is SQL Server's alone.  Today `sys.int` is a PostgreSQL key of a user's schema named `sys`; the plan keeps that, per the anti-goal "A PostgreSQL project that sees a difference".  In practice it is SQL Server's alone, since no PostgreSQL type lives in `sys` unless a user makes one.  `pg_catalog.` keys have no SQL Server key, as the spec says.
- **The order of the commits.**  The spec's order of work is followed in substance, with the map before the split, since the split dispatches to it: Tasks 1 to 7 in order, one commit each.  The research appendix lands with the map (Task 2), whose rows it decides.
- **The help of a bracketed key.**  `SQLSRC014`'s and `SQLSRC111`'s messages gain a trailing help argument, empty unless the key holds a `[`.  `SQLSRC111`'s parse error therefore carries two arguments for every `InvalidMarkerValue`.
- **Rows that differ by driver.**  The research found none: every type boxes the same on SqlClient 6.1.7 and 7.1.1, on `net8.0` and `net10.0`.  A row has one C# type and the column only tells a referenced driver from none; `DiffersByDriver` stays PostgreSQL's.

## Global Constraints

- Every commit passes `./pre-commit-validation.sh` on the exact tree committed.  One commit per task, in order; the plan goes in Task 1's commit.
- Root `AGENTS.md`, `src/SqlSource/AGENTS.md`, `tests/AGENTS.md`, `tests/SqlSource.Tests/Generator/AGENTS.md`, `docs/manual/AGENTS.md`, `docs/architecture/AGENTS.md` win over this plan and any skill's default.
- Generator code: `netstandard2.0`, Roslyn 4.8.0 API only, no collection expression targeting `ImmutableArray<T>`, records with `EquatableArray<T>` between steps, no `ISymbol`, `Compilation` or `Location` past the step that reads them, nothing that can throw on a user's value.  `Settings/` uses nothing of `Parsing/`, `Generation/` or `Diagnostics/`.
- Tests in `tests/SqlSource.Tests/Generator/` start with the `// Shared:` line, use Roslyn 4.8.0 API and C# 12 at most, and state every run's output; helpers go in `tests/SqlSource.Testing/Generator/`.
- Ids, exactly: `SQLSRC267` SqlClient below 6.1 (error, no position, once per project); `SQLSRC268` SqlClient newer than 7 (Information, configurable, no position, once per project); `SQLSRC269` a type whose C# type is SqlClient's without SqlClient (error, at the query like `SQLSRC249`).  `SQLSRC246`, `SQLSRC250` to `SQLSRC252` reused.  No other new id.
- Every C# type text is a keyword or `global::`-qualified.
- Shared files with the 6.1-6.3 sessions (`docs/manual/diagnostics.md`, `docs/manual/sqlserver.md`, the epic outline, `SqlDiagnostics.cs`, `AnalyzerReleases.Unshipped.md`, `docs/architecture/`, `Directory.Packages.props`): touch only this sub-phase's rows, sections and ids.
- User documents name no internals; the statements that a user gets SQL Server models (`docs/manual/README.md`'s "What works today", `README.md`, `docs/manual/generator.md`, `settings.md`'s Status cells) are written only if 6.3 has fully merged when the PR is pushed; otherwise the callouts stay.

## Review Focus

- **An alias type over a faceted base**, `dbo.Email` over `nvarchar(320)`, maps to `string` with the key `nvarchar`, and an entry `dbo.Email=MyApp.Email` wins over `nvarchar=...`.  Test in Task 2.
- **A user's key in another case**: an entry `DBO.EMAIL=MyApp.Email` matches the type `dbo.Email` (plain parts compare ignoring case), `"dbo"."email"` does not.  Test in Task 5.
- **`int=long` in the project property with a `postgres` and an `mssql` file** gives `long` to both `int4` and `int` columns; `int4=long` reaches only the `postgres` file.  Test in Task 5.
- **A sidecar of an `mssql` file whose column type is null or has an unknown name** (hand edited, or a newer server type) is `SQLSRC246` with a `sys.`-qualified entry in its help, never an exception.  Test in Task 5.
- **A project that references both Npgsql 7 and SqlClient 5.1** with files of both engines reports `SQLSRC247` and `SQLSRC267`, once each; with only `postgres` files, only `SQLSRC247`.  Test in Task 3.

---

### Task 1: The plan and the outline's row

**Files:**
- Create: this plan
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md` (the 6.4 row of the Phases table: `Not started` to `In progress`)

- [ ] **Step 1:** Set the 6.4 row to `In progress`.  Touch no other row.
- [ ] **Step 2: Validate and commit**: `git commit -m "Plan phase 6.4 of query generation"`.

### Task 2: The research appendix and the SQL Server base map

**Files:**
- Create: `src/SqlSource/Generation/Types/SqlServerTypeMap.cs`, `SqlClientColumn.cs`; `tests/SqlSource.Tests/Generation/Types/SqlServerTypeMapTests.cs`
- Modify: `docs/superpowers/specs/2026-10-20-query-generation-phase-6-4-type-map-design.md` (a `## Research` section before `## Version`); `Generation/Types/TypeMappingKind.cs`, `TypeMapping.cs` (doc wording: "a database type", not PostgreSQL), `DriverColumn.cs` (doc: Npgsql's column); `Settings/TypeKeys.cs` (SQL Server's names, list only); `tests/SqlSource.Tests/Generation/Types/PostgresTypeMapTests.cs` (the keys test, both engines)

- [ ] **Step 0:** Write the spec's `## Research` section from the research findings (the controller hands them over): the server image and version, the two driver versions and the target framework; a table of every type with `GetFieldType` and `GetValue`'s type on each driver; which rows differ (if any); the probe's markers checked in the metadata of every release from 6.0.0 to the newest 7 on every target framework, with the 7.0 marker named; the assembly and informational versions.  Where a finding changes a row of the spec's table, change the row and say so in one sentence.  Style: as 5.2's `## Research`, prose and one table.

**Interfaces:**
- Produces, in `SqlSource.Generation.Types`:
  - `enum SqlClientColumn { SqlClient61, SqlClient7, None }`.
  - `delegate TypeMapping? SqlServerOverrideOf(string key)`: the type override's answer for a SQL Server key, or null.
  - `static class SqlServerTypeMap`: `TypeMapping Map(SqlServerType type, SqlClientColumn column, SqlServerOverrideOf? overrideOf = null)`; `string KeyOf(SqlServerType type)`; `string BaseName(string name)` (the name cut before its first `(`, `nvarchar(50)` to `nvarchar`); `IEnumerable<string> Keys` (every row's name).
  - `TypeMappingKind.NeedsSqlServerTypes`, added at the end: SqlClient reads the type only with the package Microsoft.SqlServer.Types (`hierarchyid`, `geography`, `geometry`); `Detail` is that package's C# type for it.  `NeedsDriver`'s doc names `Microsoft.Data.SqlTypes` beside `NpgsqlTypes`.
- Produces, in `SqlSource.Settings`:
  - `TypeKeys.KnownSqlServerNames` (`IReadOnlyCollection<string>`), from a private `SqlServerNames` list: `bigint`, `int`, `smallint`, `tinyint`, `bit`, `decimal`, `numeric`, `money`, `smallmoney`, `float`, `real`, `date`, `datetime`, `datetime2`, `smalldatetime`, `time`, `datetimeoffset`, `char`, `varchar`, `text`, `nchar`, `nvarchar`, `ntext`, `xml`, `json`, `binary`, `varbinary`, `image`, `timestamp`, `uniqueidentifier`, `sql_variant`, `vector`, `hierarchyid`, `geography`, `geometry`, `sysname` (`rowversion` is an alias).  `KnownNames` stays PostgreSQL's.  The map is held equal to this list; Task 4 reads keys by it.

`KeyOf`: for a type whose `UserType` has a `Name`, the user type's key: its bare name (`TypeKeys.WritePart(name)`) when its schema is `sys`, else `WritePart(schema) + "." + WritePart(name)`, so `dbo.Email` gives `dbo."Email"`; for any other type, `WritePart(BaseName(Name))`.

`Map`, at each level asking `overrideOf` first, as `PostgresTypeMap.Map` does:
1. A type with a user type: `overrideOf(KeyOf(type))`.  Then an alias type (no `AssemblyQualifiedName`) maps as its base: `Map(type with { UserType = null }, column, overrideOf)`, which asks the override for the base's key; `sysname` is `string` so.  A CLR type of `sys` is a row of the map by its name (`hierarchyid`, `geography`, `geometry`); a CLR type of any other schema is `Unreadable` with its own key.
2. A type without one: `overrideOf(key)`, then the row of `BaseName(Name)`, ignoring case, else `Unreadable` with the key.  An empty `Name` is `NotWrittenByTheTool`.

The rows (from the spec's table as the research confirms or corrects it; text, value type):

| Names | C# | Value type |
|----|----|----|
| `bigint`, `int`, `smallint`, `tinyint` | `long`, `int`, `short`, `byte` | yes |
| `bit` | `bool` | yes |
| `decimal`, `numeric`, `money`, `smallmoney` | `decimal` | yes |
| `float`, `real` | `double`, `float` | yes |
| `date`, `datetime`, `datetime2`, `smalldatetime` | `global::System.DateTime` | yes |
| `time` | `global::System.TimeSpan` | yes |
| `datetimeoffset` | `global::System.DateTimeOffset` | yes |
| `char`, `varchar`, `text`, `nchar`, `nvarchar`, `ntext`, `xml`, `json` | `string` | no |
| `binary`, `varbinary`, `image`, `timestamp` | `byte[]` | no |
| `uniqueidentifier` | `global::System.Guid` | yes |
| `sql_variant` | `object` | no |
| `vector` | `global::Microsoft.Data.SqlTypes.SqlVector<float>`, of SqlClient: `NeedsDriver` under `None` | yes (a struct) |
| `hierarchyid`, `geography`, `geometry` | `NeedsSqlServerTypes` under every column, `Detail` `Microsoft.SqlServer.Types.SqlHierarchyId`, `...SqlGeography`, `...SqlGeometry` | - |

No row differs between 6.1 and 7, so `Row` has one cell and the column decides only `None`.

- [ ] **Step 1: Write `SqlServerTypeMapTests`**, building types with `TestSidecars.SqlServer`:
  - `Map_EveryRowOfTheTable_GivesItsType`: `TheoryData<string name, string under61, string under7, string underNone, bool isValueType>`, one row per name of the table; a failing cell is `!` and the kind's name (`!NeedsDriver` for `vector` under `None`; `!NeedsSqlServerTypes` for the three CLR rows under all three, with the user type `sys.<name>` and an assembly-qualified name).  A mapped row's `Key` is its name and `IsPublic` is true.
  - `Map_FacetedName_IsCutAtItsParenthesis`: `nvarchar(50)`, `datetime2(7)`, `decimal(18,2)`, `varchar(max)`, `vector(3)` map as their base names, with the base name as `Key`.
  - `Map_AliasType_IsAskedOfTheOverrideFirstThenMappedByItsBase`: `nvarchar(320)` with user type `dbo.Email` is `string`, `Key` `nvarchar`; an `overrideOf` answering `dbo."Email"` wins; one answering only `nvarchar` gives its text; the keys it is asked, in order, are `dbo."Email"` then `nvarchar`.
  - `Map_Sysname_IsString`: `nvarchar(128)` with user type `sys.sysname` is `string`, and the override is asked `sysname` first.
  - `Map_ClrType_IsARowByItsName`: `geography` with user type `sys.geography` and an assembly-qualified name is `NeedsSqlServerTypes`; an `overrideOf` answering `geography` wins; a CLR type `dbo.Money` (`Name` `Money`, an assembly-qualified name) is `Unreadable` with key `dbo."Money"`.
  - `Map_UnknownSystemName_IsUnreadable`: `newtype` is `Unreadable`, key `newtype`; an empty name is `NotWrittenByTheTool`.
  - `Map_Override_AnswersBeforeTheRow`: an `overrideOf` answering `int` gives its mapping for `int`.
- [ ] **Step 2: Extend `PostgresTypeMapTests.Keys_OfTheMap_AreTheKnownNamesOfTheSettings`**, or add a second case beside it, so that `SqlServerTypeMap.Keys` equals `TypeKeys.KnownSqlServerNames` ignoring case and order.  The PostgreSQL case is unchanged.
- [ ] **Step 3: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SqlServerTypeMapTests|FullyQualifiedName~PostgresTypeMapTests"`.  Expected: does not compile.
- [ ] **Step 4: Implement** the types above.  Rows are a static `Dictionary<string, Row>` ignoring case.  `Map` never throws: a user type with a null `Name` is treated as no user type.
- [ ] **Step 5: Run** the filter.  Expected: PASS.
- [ ] **Step 6: Update `src/SqlSource/AGENTS.md`**: the rule on a new row of `PostgresTypeMap.cs` covers `SqlServerTypeMap.cs` and `TypeKeys.KnownSqlServerNames`, held by the keys test.
- [ ] **Step 7: Validate and commit**: `git commit -m "Map every type SqlClient reads"`.

### Task 3: The probe of SqlClient, and SQLSRC267 and SQLSRC268

**Files:**
- Create: `src/SqlSource/Generation/Types/SqlClientProbe.cs`; `tests/SqlSource.Testing/Generator/SqlClientReferences.cs`; `tests/SqlSource.Tests/Generator/SqlClientProbeTests.cs`
- Modify: `src/SqlSource/SqlSourceGenerator.cs`, `Generation/TrackingNames.cs`, `Diagnostics/SqlDiagnostics.cs`, `AnalyzerReleases.Unshipped.md`; `tests/SqlSource.Testing/SqlSource.Testing.csproj` and any `packages.lock.json` that restore changes; `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs` (`SQLSRC268` configurable); the `Caching*Tests` class of the probe's step; `docs/manual/diagnostics.md` (sections `SQLSRC267`, `SQLSRC268`, in id order, and its index); `src/SqlSource/AGENTS.md`

**Interfaces:**
- Produces: `sealed record SqlClientProbe(bool Referenced, bool AtLeast61, bool AtLeast7, string? Version)` with:
  - `static SqlClientProbe Read(Compilation compilation)`.  `Referenced`: `Microsoft.Data.SqlClient.SqlConnection` found; `AtLeast61`: ``Microsoft.Data.SqlTypes.SqlVector`1`` found; `AtLeast7`: `Microsoft.Data.SqlClient.SspiContextProvider`, first in 7.0.0 on every target framework, in no 6.x (the research).  `Version`: of the referenced assemblies named `Microsoft.Data.SqlClient` (ignoring case), the newest by identity version; its `AssemblyInformationalVersionAttribute` read from the assembly's symbol (`compilation.GetAssemblyOrModuleSymbol(reference)`), cut before a `+`, or, without the attribute, the identity version's major alone (`"6"`); null when there is none.  Never throws.
  - `SqlClientColumn Column`: `SqlClient7` when `AtLeast7`, `SqlClient61` when `Referenced`, else `None`.
  - `bool IsBelowTheFloor` (`Referenced && !AtLeast61`); `bool IsNewerThanKnown` (`Referenced` and `Version`'s leading integer above `NewestKnownMajor`, a private `const int` of 7); `string VersionText` (`Version`, or "of an unknown version").
- Produces: steps `TrackingNames.SqlClientProbe` (a `Select` over `CompilationProvider`) and `TrackingNames.UsesSqlServerEntries` (as `UsesPostgresEntries`, for `SqlDialect.SqlServer`), and one output over them.  `TypedFiles` carries the probe beside `NpgsqlProbe`.
- Produces, in `SqlSource.Testing.Generator`: `static class SqlClientReferences` with `MetadataReference SqlClient60`, `SqlClient61`, `SqlClient7` (`ref/net8.0/Microsoft.Data.SqlClient.dll` of 6.0.5, 6.1.7 and 7.1.1 from the NuGet folder, never loaded; for 7.1.1 also the `lib/netstandard2.0` assemblies of its dependencies `Microsoft.Data.SqlClient.Extensions.Abstractions` and `Microsoft.Data.SqlClient.Internal.Logging` at the versions its nuspec names, if the reference does not resolve without them) and `MetadataReference Synthetic(string version, bool connection, bool vector, bool seven, string? informationalVersion = null, string assemblyName = "Microsoft.Data.SqlClient")`, built as `NpgsqlReferences.Synthetic` is; and `MetadataReference SystemDataSqlClient`, a synthetic assembly `System.Data.SqlClient` with `System.Data.SqlClient.SqlConnection`.
- Produces: `SqlDiagnostics.SqlClientTooOld` (`SQLSRC267`, error, `NotConfigurable`, title "Microsoft.Data.SqlClient is older than 6.1", message "This project references Microsoft.Data.SqlClient {0}, and SqlSource needs Microsoft.Data.SqlClient 6.1 or later.  Update the package.") and `SqlDiagnostics.SqlClientNewerThanKnown` (`SQLSRC268`, `Info`, configurable with the same comment as `SQLSRC248`, title "Microsoft.Data.SqlClient is newer than SqlSource knows", message "This project references Microsoft.Data.SqlClient {0}, which is newer than this version of SqlSource knows.  Its models use the types of Microsoft.Data.SqlClient 7.").

- [ ] **Step 1: Download the drivers.**  Add the three SqlClient versions to `SqlSource.Testing.csproj`'s `PackageDownload`, with a comment like Npgsql's.  Restore without locked mode; commit any changed lock file.
- [ ] **Step 2: Write `SqlClientProbeTests`** (shared file), each over a `Models` query of an `mssql` file whose entry is current (the probe and the driver report do not depend on `HasTypeMap`; until Task 6 the file also reports `SQLSRC243`, which these tests ignore by filtering on the ids they test):
  - `Read_EachDriver_GivesItsFlags`: no reference gives `(false, false, false, null)` and `None`; `SystemDataSqlClient` alone the same; `SqlClient60` `(true, false, false, "6.0.5")` and `SqlClient61`; `SqlClient61` `(true, true, false, "6.1.7")`; `SqlClient7` `(true, true, true, "7.1.1")` and `SqlClient7`.  Read from the step's output by `TrackingNames.SqlClientProbe`.
  - `Run_SqlClientBelowTheFloor_IsSqlsrc267Once`: `SqlClient60` gives one `SQLSRC267` naming its version and `6.1`; two `mssql` files still give one.
  - `Run_SqlClientNewerThanKnown_IsSqlsrc268`: `Synthetic("8.0.0.0", true, true, true, "8.0.1")` gives one `SQLSRC268`, severity `Info`, naming `8.0.1`.
  - `Run_NoQueryNeedsAnSqlServerEntry_ReportsNothingOfTheDriver`: `SqlClient60` with every query at `Sql`, or with only `postgres` files, gives neither.
  - `Run_BothDriversBelowTheirFloors_ReportsEachForItsEngine` (Review Focus): `NpgsqlReferences.Synthetic("7.0.0", false, false)` and `SqlClient60`, one file of each engine, give one `SQLSRC247` and one `SQLSRC267`; with only the `postgres` file, only `SQLSRC247`.
  - `Read_InformationalVersionMissing_GivesTheMajor`: a synthetic `6.0.0.0` without the attribute gives `Version` `"6"`.
- [ ] **Step 3: Caching.**  In the `Caching*Tests` class that holds `NpgsqlProbe`'s case, the same case for `SqlClientProbe` and `UsesSqlServerEntries`.
- [ ] **Step 4: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~SqlClientProbeTests|FullyQualifiedName~Caching"`.  Expected: does not compile.
- [ ] **Step 5: Implement** the probe, the steps, the output (`Location.None`, once per project), the descriptors in `All`, `AnalyzerReleases.Unshipped.md` (`Info` for 268), `SqlDiagnosticsTests`' configurable list, and the two sections of `docs/manual/diagnostics.md`: what each means and the fix (reference Microsoft.Data.SqlClient 6.1 or later directly; EF Core 8 and 9 bring 5.1, so a project of theirs references the package itself).
- [ ] **Step 6: Update `src/SqlSource/AGENTS.md`**: `SqlClientProbe.cs` in the list of the files that touch symbols; `SQLSRC267` and `SQLSRC268` in the no-position rule; `NewestKnownMajor` per driver in the map rule.
- [ ] **Step 7: Run** the filter, then `tests/SqlSource.Tests` and `tests/SqlSource.Tests.RoslynFloor`.  Expected: PASS.
- [ ] **Step 8: Validate and commit**: `git commit -m "Probe the SqlClient a project references"`.

### Task 4: The type override's keys per engine

**Files:**
- Create: `src/SqlSource/Settings/DatabaseEngine.cs`
- Modify: `Settings/TypeKeys.cs`, `TypeOverrideGrammar.cs`, `SettingsLevel.cs`, `SettingsLevelReader.cs`, `QuerySettings.cs`; `Parsing/Markers/SqlMarkerScope.cs`, `Parsing/SqlDescribableDialects.cs`, `Parsing/SqlParseErrorKind.cs` and every site that adds `InvalidMarkerValue`; `Generation/MSBuild/InvalidSetting.cs` (or `InvalidValues.cs`, wherever the record is); `Generation/Types/MappedTypeNames.cs`, `FileTypesInput.cs`; `SqlSourceGenerator.cs` (`SQLSRC014`'s report); `src/SqlSource.Tool/Planning/RunPlanner.cs` (the same); `Diagnostics/SqlDiagnostics.cs` (`SQLSRC014`, `SQLSRC111` formats); tests below

**Interfaces:**
- Produces, in `SqlSource.Settings`:
  - `enum DatabaseEngine { PostgreSql, SqlServer }`.
  - `TypeKeys.TryNormalizeSqlServer(ReadOnlySpan<char> key, out string normalized)`: by `TrySplit`; an array is false; an unqualified plain part is a SQL Server name or alias, ignoring case, given as the name (aliases: `integer` to `int`, `character` to `char`, `dec` to `decimal`, `rowversion` to `timestamp`); an unqualified quoted part must hold a name exactly, in lower case, with no alias; `sys.` (plain in any case, or `"sys"`) is taken off and the rest given as `TryNormalize` does after `pg_catalog.` (a known name in lower case, any other as written, the quoted forms likewise, no alias); `pg_catalog.` is false; any other qualified key is as `TryNormalize` gives it.  `TryNormalize` is unchanged.
  - Consumes `TypeKeys.KnownSqlServerNames`' list (Task 2).
  - `TypeKeys.IsKnownSqlServerName(string)` if `AsAKey` (Task 5) needs it, or `TryNormalizeSqlServer` reused there.
  - `TypeOverrideGrammar.TryReadEntry(ReadOnlySpan<char> text, bool spacesAroundEquals, SettingPlace place, DatabaseEngine engine, out TypeOverride? entry)`: the entry keyed by that engine's reading; false for text that is no entry or a key that engine does not read.
  - `TypeOverrideGrammar.ReadList(string text, SettingPlace place, List<string> invalid)` returns `TypeOverrideLists`, `readonly record struct TypeOverrideLists(EquatableArray<TypeOverride> PostgreSql, EquatableArray<TypeOverride> SqlServer)`: each entry goes in each list whose engine reads its key, the last of a key in a list winning; an entry that neither reads is invalid.
  - `TypeOverrideGrammar.BracketHelp(ReadOnlySpan<char> entry)`: `""` unless the key, the text before the first `=` outside quotes, holds `[`; then `".  A key is not written in brackets: write " + plain + ", or " + quoted + " to match its case exactly"`, each `[x]` part (with `]]` read as `]`) written plain when every character is plain (`dbo.Email`) and quoted (`"dbo"."Email"`), a plain part kept, a quoted part kept; when some part cannot be plain, `".  A key is not written in brackets: write " + quoted`.
  - `SettingsLevel.SqlServerTypeOverrides` (`EquatableArray<TypeOverride>?`), merged in `Over` by `TypeOverrideMap.Over` beside `TypeOverrides`.
  - `QuerySettings.ResolveTypeOverrides(SettingsLevel markers, SettingsLevel metadata, SettingsLevel property, DatabaseEngine engine)`: the engine's arrays merged as today.
- Produces, in `SqlSource.Parsing`: `SqlDescribableDialects.EngineOf(SqlDialect dialect)`: `SqlServer` for `SqlDialect.SqlServer`, `PostgreSql` for every other.
- Produces: `SQLSRC014` message `"'{0}' is not a valid value of {1}{2}"` and `SQLSRC111` message `"'{0}' lacks a value it needs, has one it does not take, or has one that is not valid{1}"`; every report passes the help (empty for every value but a bracketed type override key).  `InvalidSetting` gets `string Help` (`BracketHelp(Value)` when `Name` is `MSBuildSettings.TypeOverridesName`, else `""`), used by both the generator and the tool.

- [ ] **Step 1: Write the tests**, each existing case keeping its answer (a call site gains `DatabaseEngine.PostgreSql` or `.PostgreSql` and nothing else):
  - `TypeKeysTests`: `TryNormalizeSqlServer` of `int`, `INT`, `integer` gives `int`; `rowversion` and `timestamp` give `timestamp`; `dec` gives `decimal`; `nvarchar`, `sysname`, `hierarchyid`, `json`, `vector` themselves; `"int"` gives `int`, `"Int"` and `"integer"` are false; `sys.int` and `sys."int"` give `int`; `sys.newtype` gives `newtype`; `dbo.Email`, `"dbo"."Email"` and `dbo.U&"Em\0061il"` as `TryNormalize` gives them; `int4`, `_int4`, `int[]`, `pg_catalog.text`, `[dbo].[Email]` are false.  `TryNormalize` of `nvarchar` and `rowversion` stays false; of `sys.int` stays `sys.int`.
  - `TypeOverrideGrammarTests`: `TryReadEntry` per engine: `int=long` is `int4` and `int`; `int4=long` is `int4` and false; `bit=bool` and `timestamp=byte[]` themselves in both; `rowversion=byte[]` false and `timestamp`; `nvarchar=string` false and `nvarchar`; `pg_catalog.text=string` `text` and false; `sys.text=string` `sys.text` and `text`; `_int4=int[]` `int4[]` and false; `dbo.Email=MyApp.Email` the same in both.  `ReadList("int=X int4=Y")` gives PostgreSQL `int4=Y`, SQL Server `int=X`; `ReadList("nvarchar=string")` records nothing invalid; `ReadList("[dbo].[Email]=MyApp.Email")` records it invalid.  `BracketHelp` of `[dbo].[Email]=X`, `dbo.[Email]=X`, `[my schema].[Email]=X`, `[a]]b].c=X` and of `dbo.Email=X` (`""`).
  - `SqlMarkerScopeTests` / `SqlFileParserTests`: in an `mssql` file, `-- type-override: int=long` and `-- type-override: integer=short` in one scope are `SQLSRC112`; `int4=long` is `SQLSRC111`; `nvarchar=string` fills `SqlServerTypeOverrides` and leaves `TypeOverrides` null; in a `postgres` file `nvarchar=string` is `SQLSRC111` as today; `-- type-override: [dbo].[Email]=MyApp.Email` is `SQLSRC111` whose message ends with the bracket help.
  - `SettingsLevelReaderTests`, `QuerySettingsTests`: the property `int=long nvarchar=string` gives both arrays; `ResolveTypeOverrides(..., SqlServer)` merges the SQL Server arrays by place.
  - The generator's and the tool's `SQLSRC014` for `SqlSourceTypeOverrides` `[dbo].[Email]=MyApp.Email` carry the help; `GeneratorParityTests` still agree.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tests` and `tests/SqlSource.Tool.Tests`.  Expected: does not compile or the new cases fail.
- [ ] **Step 3: Implement.**  `SettingsLevelReader` fills both members from one `ReadList`.  `SqlMarkerScope` reads with `SqlDescribableDialects.EngineOf(rules.Dialect)` and fills that engine's member.  `MappedTypeNames.Collect` collects the C# types of both members.  `FileTypesInput.TypeOverridesOf` keeps both members.  `QueryTyper.LookupOf` passes `DatabaseEngine.PostgreSql` until Task 5.  Every `InvalidMarkerValue` error carries two arguments; `SqlParseErrorKind`'s doc says so.
- [ ] **Step 4: Run** both projects and `tests/SqlSource.Tests.RoslynFloor`.  Expected: PASS.
- [ ] **Step 5: Update** `src/SqlSource/AGENTS.md`'s `Settings/` rule on the type override (two arrays per level, one per engine; a marker fills its file's engine's) and `docs/architecture/generator.md`'s type override section.  `docs/manual/diagnostics.md`: `SQLSRC014` and `SQLSRC111` say a bracketed key is not read and show the forms that are.
- [ ] **Step 6: Validate and commit**: `git commit -m "Read the type override's keys for each engine"`.

### Task 5: The typing split by engine

**Files:**
- Modify: `Generation/Types/QueryTyper.cs`, `FileTypesInput.cs`; `Diagnostics/SqlDiagnostics.cs`, `AnalyzerReleases.Unshipped.md` (`SQLSRC269`); `SqlSourceGenerator.cs` (the probe into `FileTypesInput.Gather`); `tests/SqlSource.Tests/Generation/Types/QueryTyperTests.cs`; `docs/manual/diagnostics.md` (`SQLSRC269`, and `SQLSRC246`'s help for SQL Server)

**Interfaces:**
- Consumes: `SqlServerTypeMap` (Task 2), `SqlClientProbe` (Task 3), `ResolveTypeOverrides(..., engine)` (Task 4).
- Produces: `FileTypesInput(..., NpgsqlProbe Probe, SqlClientProbe SqlClientProbe, EquatableArray<MappedType> MappedTypes)` and `Gather(..., NpgsqlProbe probe, SqlClientProbe sqlClientProbe, ...)`.  `SqlDiagnostics.TypeNeedsSqlClient` (`SQLSRC269`, error, `NotConfigurable`, title "Type needs Microsoft.Data.SqlClient", message "The {0} has the type '{1}', whose C# type depends on Microsoft.Data.SqlClient, and this project does not reference it.  {2}.").

The split, in `QueryTyper`:
- `Typing` carries `DriverColumn Column`, `SqlClientColumn SqlClientColumn`, the lookup and the place.  The lookup is made per distinct markers level, for the engine of the file (`SqlDescribableDialects.EngineOf(file.Dialect)`), and offers `Find(PostgresType)` as today and `Find(string sqlServerKey)` (`TypeOverrideMap.Find` and `MappingOf`, no array or extension rule).
- `TypeOf`: a `PostgresType` to `PostgresTypeMap.Map` as today; a `SqlServerType` to `SqlServerTypeMap.Map(type, column, lookup.Find)`; anything else, or null, `SQLSRC246` with "Run 'sqlsource describe'".
- `Diagnose` takes the engine.  SQL Server's helps: `NeedsDriver` is `SQLSRC269` with "Reference Microsoft.Data.SqlClient"; `NeedsSqlServerTypes` is `SQLSRC246` with "Reference the package Microsoft.SqlServer.Types and map it with the type override, as " + key + "=" + detail; `Unreadable` is `SQLSRC246` with "Map it with the type override, as " + `AsAKey(key, engine)` + "=<a C# type>"; `NotWrittenByTheTool`, and the mapped-type failures, as PostgreSQL's.  `AsAKey` for SQL Server puts `sys.` before an unqualified key that `TryNormalizeSqlServer` does not read.  Every PostgreSQL help is unchanged.

- [ ] **Step 1: Write the tests** in `QueryTyperTests` (and generator runs where the existing class uses them):
  - `Type_SqlServerEntry_IsTypedByItsMap`: an `mssql` file's entry with parameters `int` and `nvarchar(50)` and columns `datetime2(7)`, `uniqueidentifier`, `dbo.Email` gives `int`, `string`, `global::System.DateTime`, `global::System.Guid`, `string`.
  - `Type_SqlServerTypeOfSqlClientWithoutIt_IsSqlsrc269`: `vector(3)` under `None` is `SQLSRC269`, help "Reference Microsoft.Data.SqlClient."; under `SqlClient61` it is the `SqlVector<float>` type.
  - `Type_SqlServerHelps`: `geography` gives `SQLSRC246` "... as geography=Microsoft.SqlServer.Types.SqlGeography."; `newtype` gives "... as sys.newtype=<a C# type>."; a null type and an empty name give "Run 'sqlsource describe'." (Review Focus); `dbo.Money` (a CLR type) gives "... as dbo.\"Money\"=<a C# type>.".
  - `Type_SharedNameInTheProperty_ReachesEachEngine` (Review Focus): the property `int=long` gives `long` to a `postgres` file's `int4` column and an `mssql` file's `int` column; `int4=long` only to the first; `rowversion=MyApp.Version` only to an `mssql` `timestamp` column.
  - `Type_UserKeyInAnotherCase_Matches` (Review Focus): `DBO.EMAIL=MyApp.Email` matches `dbo.Email`; `"dbo"."email"=MyApp.Email` does not, and the column stays `string`.
  - Every existing PostgreSQL case unchanged.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~QueryTyperTests"`.  Expected: fail.
- [ ] **Step 3: Implement** the split, `SQLSRC269` in `All`, `AnalyzerReleases.Unshipped.md`, and its section in `docs/manual/diagnostics.md`; `SQLSRC246`'s section gains SQL Server's two cases.
- [ ] **Step 4: Run** `tests/SqlSource.Tests` and `tests/SqlSource.Tests.RoslynFloor`.  Expected: PASS.
- [ ] **Step 5: Update** `src/SqlSource/AGENTS.md` and `docs/architecture/generator.md` (the engine split, the probe in the pipeline's diagram).
- [ ] **Step 6: Validate and commit**: `git commit -m "Type each query by the map of its engine"`.

### Task 6: Table models of `dbo`, and `mssql` checked against its sidecar

**Files:**
- Modify: `Generation/Models/PropertyNames.cs`, `TypeModels.cs`; `Generation/Sidecars/SidecarChecker.cs`; `tests/SqlSource.Tests/Generation/Models/PropertyNamesTests.cs`; `tests/SqlSource.Tests/Generator/SidecarTests.cs`; `tests/SqlSource.Tests.Postgres/Realistic/Generation/RealisticModels.cs` (the new argument); a model test class and the `Caching*Tests` of the typed file; `docs/manual/diagnostics.md` (`SQLSRC243`)

**Interfaces:**
- Produces: `PropertyNames.FromTable(SidecarTable table, DatabaseEngine engine)`: the schema dropped when it is ordinally `public` on PostgreSQL or `dbo` on SQL Server.  `TypeModels.Request` passes `SqlDescribableDialects.EngineOf(file.Dialect)`.
- Produces: `SidecarChecker.HasTypeMap` true for `PostgreSql` and `SqlServer`.

- [ ] **Step 1: Write the tests**:
  - `PropertyNamesTests`: `dbo.orders` is `Orders` on SQL Server and `DboOrders` on PostgreSQL; `public.orders` is `PublicOrders` on SQL Server and `Orders` on PostgreSQL; `billing.invoices` is `BillingInvoices` on both; `DBO.orders` keeps its schema on SQL Server (ordinal).  Existing cases pass `DatabaseEngine.PostgreSql`.
  - `SidecarTests`: the case that asserts `SQLSRC243` for `mssql` becomes `Run_MssqlFile_IsCheckedAgainstItsSidecar` (a current entry is usable, a stale one `SQLSRC244`); a test that no dialect of `SqlDescribableDialects` reports `SQLSRC243`.
  - A generator run of an `mssql` file with a current sidecar at `Models`, a column of `dbo.orders` matched as its table: the model `OrdersDto` with the C# types of Task 5, no warning in generated code.  Put it beside the existing model runs (`ModelRuns` helpers).
  - `Caching*Tests`: an edit to an `mssql` `.sql` file re-runs only its file's typing, as the existing case does for `postgres`.
- [ ] **Step 2: Run** the filters.  Expected: fail.
- [ ] **Step 3: Implement.**  The comment of `HasTypeMap` says `SQLSRC243` waits for an engine whose describer lands before its map.
- [ ] **Step 4: Run** `tests/SqlSource.Tests`, `tests/SqlSource.Tests.RoslynFloor`, and `tests/SqlSource.Tests.Postgres` (it compiles `RealisticModels.cs`).  Expected: PASS.
- [ ] **Step 5:** `docs/manual/diagnostics.md`'s `SQLSRC243` says no dialect of this release reports it.
- [ ] **Step 6: Validate and commit**: `git commit -m "Give mssql files their models"`.

### Task 7: The documents, and the row set to Done

**Files:**
- Modify: `docs/manual/sqlserver.md`, `docs/manual/settings.md`, `docs/architecture/generator.md`, `src/SqlSource/AGENTS.md` (read whole), the epic outline's 6.4 row; and only if 6.3 has fully merged into `main` (both PRs; `gh pr list -R mbcrawfo/SqlSource --state merged` and `/Users/michael/.claude/implementation-manager/mbcrawfo-SqlSource/query-gen-phase-6/notes.md`): `docs/manual/README.md` ("What works today"), `README.md` ("Only want to generate models?", supported environments with SqlClient 6.1), `docs/manual/generator.md`, `settings.md`'s Status cells

- [ ] **Step 1: `sqlserver.md`**, beneath the existing callout: `<a id="types"></a>` `## Types`: the table of the map (database type, C# type), facets dropped, a nullable column the type with `?`; which Microsoft.Data.SqlClient it needs (6.1 or later) and what happens without it (models of every type but `vector`, which needs it) or below it (the build error, models still made); `hierarchyid`, `geography`, `geometry` and their override with Microsoft.SqlServer.Types; alias types map as their base and can be overridden by `schema.name`.  `<a id="known-issues"></a>` `## Known issues`: EF Core 8 and 9 bring Microsoft.Data.SqlClient 5.1; reference 6.1 or later directly.  Effects only, no internals; run the grep of `docs/manual/AGENTS.md`.
- [ ] **Step 2: `settings.md`'s type override section**: SQL Server's keys; a name both engines have applies to each engine's own type of that name (`bit`, `timestamp`, `int`), and a project of both engines that wants one for one engine sets it by metadata on that engine's files; `sys.` as SQL Server's system schema; `rowversion` and the other synonyms; a known issue that a bracketed key is not read, with the forms that are.
- [ ] **Step 3:** `docs/architecture/generator.md` read whole for this sub-phase's changes; `src/SqlSource/AGENTS.md` read whole.
- [ ] **Step 4:** If 6.3 has merged, write the statements that a user gets SQL Server models in the four places above and remove their callouts; if not, leave them and note it in the PR body.
- [ ] **Step 5:** The 6.4 row `Done`.
- [ ] **Step 6: Run** `tests/SqlSource.Tests --filter "FullyQualifiedName~Documentation|FullyQualifiedName~Diagnostics"`.  Expected: PASS.
- [ ] **Step 7: Validate and commit**: `git commit -m "Document the SQL Server type map"`.

### Task 8: Review and the pull request (controller)

- [ ] Whole-branch review (superpowers:requesting-code-review); fix every finding.
- [ ] `git fetch --tags origin`; the version check of root `AGENTS.md` (no `v*` tag: nothing to compare).
- [ ] Bring the branch up to date with `main`, validate, get CodeRabbit's clearance, push, open the PR titled "Query generation phase 6.4: the SQL Server type map", turn on Auto-fix.
