# Repository Output Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename the output `codegen` to `methods`, and add a default output `repository` that gives a partial type an instance method per query, a connection found on the instance, and a generated interface.

**Architecture:** `repository` is a fourth `OutputKind` that behaves like `methods` everywhere a query "has a method", except that its method is an instance method on the type whose connection comes from a resolved `ConnectionSource`. Connection candidates and the interface's name check are read once per compilation into equatable records (no `ISymbol` in the pipeline), resolved per type in `MethodPlanner`, and emitted inline by the existing query-method writers with an ownership strategy in `CommandWriter`. The interface is written in the type's `-Methods.g.cs` file.

**Tech Stack:** C# Roslyn incremental generator (`netstandard2.0`, Roslyn floor 4.14), xUnit v3 + Shouldly, Testcontainers database tests, Npgsql and Microsoft.Data.SqlClient.

**Spec:** `docs/superpowers/specs/2026-11-09-repository-output-design.md`

## Global Constraints

- Output values are `sql`, `models`, `methods`, `repository`; default `repository`.  `codegen` is removed, not aliased.
- Enum order: `Sql = 0`, `Models = 1`, `Methods = 2`, `Repository = 3` (the "greater output" comparisons in `SidecarNeeds.Union` and `RunPlanner.GreatestOutput` rely on order; both outputs need entries).
- A repository method takes `(InputModel parameters?, string token..., CancellationToken cancellationToken = default)`: no connection, no transaction, no `commandTimeout`.
- Repository method default visibility is `public` whatever the type's.  Default member location for `repository` is `class`; a written `extension-class` or `nested-class` reaching a repository query is `SQLSRC122`.
- Connection attribute: `SqlSourceConnectionAttribute` in namespace `SqlSource`, `[Embedded]`, **not** `[Conditional]`, targets `Field | Property | Method | Parameter`, one property `public string Database { get; set; }`.  The attribute text compiles as C# 7.3 (no `string?`).
- `[SqlSourceGenerate]` gains `public bool GenerateInterface { get; set; }` and `public string InterfaceName { get; set; }`; only a *written* named argument overrides.
- MSBuild property `SqlSourceRepositoryInterfaces`: on unless its value is `false` (ordinal ignore case).
- Convention names for a `string` source: exactly `connectionString`, `_connectionString`, `ConnectionString` (ordinal).  Any field, property or parameter whose type derives from `System.Data.Common.DbDataSource` is a convention candidate.  `DbConnection` is never a convention candidate.
- Members before parameters: a primary-constructor parameter is a convention candidate only when no field, property or method candidate exists.  Once any candidate is marked, conventions are off.
- Lifetimes: string → new driver connection, disposed; `DbDataSource` → `OpenConnectionAsync(cancellationToken)`, disposed; factory method → called each time, disposed; `DbConnection` field/property/parameter → borrowed, opened if closed and closed again, never disposed.
- Driver connection types: `global::Npgsql.NpgsqlConnection` (postgres), `global::Microsoft.Data.SqlClient.SqlConnection` (mssql).
- New type-level diagnostics take `SQLSRC028`–`SQLSRC035` (next free below 100); each descriptor written out in full with a literal id, added to `SqlDiagnostics.All`, `AnalyzerReleases.Unshipped.md` and `docs/manual/diagnostics.md` in the same task.
- Test names follow `Method_Scenario_Outcome`.  Every commit passes `./pre-commit-validation.sh`; the branch passes `./pre-commit-validation.sh --full` and `branch-review:review` before any push (root `AGENTS.md`).
- A PostgreSQL connection-string source does not register Npgsql plugins: documented as a known limitation, not diagnosed, not tech debt.

## Review Focus

1. **A streamed repository method whose loop is left early or throws** must dispose the connection it created, exactly once, when the loop ends.  Test: Task 8 `ListNotes_BreakAfterFirstRow_DisposesTheFactoryConnection`.
2. **The common DI shape** `partial class R(NpgsqlDataSource dataSource) { readonly NpgsqlDataSource _dataSource = dataSource; }` must use `_dataSource` and compile with no CS9124/CS9107 warning.  Test: Task 4 `Run_ParameterStoredInAField_UsesTheFieldAndCompilesWithoutWarnings`.
3. **Repository on a generic, nested, struct or record type** must produce an interface and methods that compile.  Test: Task 6 `Run_RepositoryOnAwkwardTypes_Compiles` (theory).
4. **A marked member on a base class in another file**, edited after the first run, must be seen without editing the derived type's file.  Test: Task 3 `Run_BaseMemberMarkedLater_ResolvesTheDerivedRepository` (caching).
5. **A connection source that is null at run time** must throw `InvalidOperationException` naming the member and type, before any database work.  Test: Task 8 `GetNote_NullConnectionString_ThrowsNamingTheMember`.

---

### Task 1: Rename `codegen` to `methods`

Pure rename; the default stays `Methods` in this task.

**Files:**
- Modify: `src/SqlSource/Settings/OutputKind.cs` (`CodeGen` → `Methods`), `src/SqlSource/Settings/QuerySettings.cs:61,86`, `src/SqlSource/Settings/SettingValue.cs:13` (doc example)
- Modify (internal renames): `Generation/Methods/CodeGenEngines.cs` → `MethodEngines.cs` (type `MethodEngines`), `DatabaseEngineExtensions.ToCodeGenEngines` → `ToMethodEngines`, `TypeMethods.CodeGenEngines` → `MethodEngines`, `TrackingNames.CodeGenEngines` → `MethodEngines`, `SqlQuery.CodeGenErrors` and `SqlBlock.CodeGenErrors` → `MethodErrors`, and every reference
- Modify: `TypePlacement.OutputText` (l.482) → delete; use `SettingValue.NameOf(output)`
- Modify (hard-coded text): `SqlDiagnostics.cs:1036` (SQLSRC273 "has the output methods … or set the output to models"), `Generation/Types/TypeDiagnostics.cs:117,179` ("in a methods query"), comments that say codegen across `src/`
- Modify: `schemas/settings-v1.schema.json:35-46` (description, enum `["sql","models","methods"]`, pattern regenerated in enum order)
- Modify: `examples/*/CodeGen/` → `git mv` to `Methods/` (namespaces `*.Methods`), examples READMEs, `examples/AGENTS.md`, comments in `schema.sql`, `DapperSetup.cs`, `Postgis/Dockerfile`, `SqlServerExamples/sqlsource.json`
- Modify: `tests/DatabaseTests/{Postgres,SqlServer}/sqlsource.json` (`"output": "Methods"`), the DatabaseTests AGENTS.md files and READMEs that say CodeGen, `src/SqlSource/{Diagnostics,Parsing,Generation}/AGENTS.md`, `docs/tech-debt/TD-0089-*.md` test name
- Modify: every user doc and architecture page that says `codegen` (list in the Explore report: `README.md`, `docs/manual/*.md`, `docs/architecture/{generator,README,sqlserver-describer}.md`); `codegen` → `methods`, "the default" stays true in this task
- Test: rename-only edits across the ~25 test files using `OutputKind.CodeGen` and the spelling rows below

**Interfaces:**
- Produces: `OutputKind.Methods`; `MethodEngines`; `SqlQuery.MethodErrors`.

- [ ] **Step 1: Update the spelling tests first**
  - `SettingValueTests.TryReadChoice_Output_IsRead`: rows `"methods"`, `"Methods"`, `"me-thods"` → `OutputKind.Methods`; add row `"codegen"` to a new `TryReadChoice_Codegen_IsNotRead` asserting `false`.
  - `SettingsFileSchemaTests` rows l.342/354: `" Me-thods "`, `"\tme-thods\n"` valid; add `{"output": "codegen"}` to the invalid rows beside `bogus`.
  - `SqlFileParserMarkerTests` l.99-116: `-- output: me-thods` → `"Methods"`.
  - `RunPlannerErrorTests` l.143/266/300 and `ToolDiagnosticsTests:214`: `"codegen"` → `"methods"`.
- [ ] **Step 2: Run `dotnet test tests/SqlSource.Tests` — expect failures on the renamed rows**
- [ ] **Step 3: Rename the enum, internal identifiers, schema, messages, fixtures, examples and docs as listed**
- [ ] **Step 4: Run `./pre-commit-validation.sh` — expect pass.**  `grep -rniI codegen src tests examples docs/manual docs/architecture README.md schemas` returns only the new negative tests.
- [ ] **Step 5: Commit** `refactor: rename the output codegen to methods`

---

### Task 2: Add `OutputKind.Repository` (parsed, placed, gated; no method yet)

Repository queries need entries, get models, follow repository placement rules, and are collected for planning, but `MethodPlanner` gives them no method until Task 5.  The default is still `Methods`.

**Files:**
- Modify: `src/SqlSource/Settings/OutputKind.cs` (add `Repository = 3`, doc "Models, and an instance method of the type that gets its own connection")
- Create: `src/SqlSource/Settings/OutputKindExtensions.cs`
- Modify: `QuerySettings.MemberLocation` (Repository → `MemberLocation.Class`)
- Modify: `TypeEmitter.SelectMembers` (l.144), `MethodPlanner.Collect` (l.163) and `WrittenNames` (l.197), `TypeModels.Of` (l.51-52) and `Request` (l.195), `TypePlacement` l.283, 457, 465, 471 — replace `== OutputKind.Methods` with `.HasMethod()`
- Modify: `TypePlacement.CheckQuery` (l.405-424) SQLSRC122 rule; `TypePlacement.PlacementOf` (l.70-83) default visibility
- Modify: `schemas/settings-v1.schema.json` (enum and pattern add `repository`)
- Modify: `tests/SqlSource.Testing/Generator/GeneratorHarness.cs` `Spell` (add `Repository => "Repository"`)
- Test: `tests/SqlSource.Tests/Settings/{SettingValueTests,QuerySettingsTests}.cs`, `Generator/PlacementDiagnosticsTests.cs`, `Generation/Placement/TypePlacementTests.cs`, `tests/SqlSource.Tool.Tests/Planning/RunPlannerErrorTests.cs`

**Interfaces:**
- Produces: `internal static bool HasMethod(this OutputKind output) => output is OutputKind.Methods or OutputKind.Repository;`

- [ ] **Step 1: Write the failing tests**
  - `SettingValueTests.TryReadChoice_Output_IsRead` rows `"repository"`, `"Re-pository"` → `OutputKind.Repository`.
  - `QuerySettingsTests.MemberLocation_NoLevelWritesOne_IsTheDefaultOfTheOutput` row `(Repository, Class)`.
  - `PlacementDiagnosticsTests.Run_NestedClassMarkerOnARepositoryQuery_ReportsSqlsrc122AtTheMarker` and `Run_ExtensionClassMarkerOnARepositoryQuery_ReportsSqlsrc122AtTheMarker`: message ends "… because its output is repository" (the existing `Sqlsrc122` helper's shape).
  - `PlacementDiagnosticsTests.Run_ClassMarkerOnARepositoryQuery_ReportsNothing`.
  - `TypePlacementTests.Of_RepositoryQueryOnAnInternalType_IsPublicOnTheType` (placement `Class`, accessibility `Public`).
  - `RunPlannerErrorTests.Plan_TwoClaimsWithTwoOutputs_NameTheGreaterInAMessage` row `("methods","repository","repository")`.
  - `SidecarCheck` behaviour: `SettingsFileTests.Run_RepositoryQueryWithoutAnEntry_ReportsSqlsrc241` with "The output 'repository' needs each query described…".
- [ ] **Step 2: Run `dotnet test tests/SqlSource.Tests tests/SqlSource.Tool.Tests` — expect those to fail**
- [ ] **Step 3: Implement**
  - SQLSRC122 when `WrittenMemberLocation is {}` and either (`location == ExtensionClass && output != Methods`) or (`output == Repository && location == NestedClass`).
  - Default visibility: `settings.WrittenVisibility?.Value ?? (settings.Output == Repository ? Visibility.Public : PlacementChecks.DefaultVisibility(...))`.
  - `MethodPlanner.Plan` (per query, l.223+): a `Repository` query returns no method and reports nothing yet; Task 5 replaces that branch.
- [ ] **Step 4: Run the two test projects — expect pass**
- [ ] **Step 5: `./pre-commit-validation.sh`, commit** `feat: add the output repository to settings and placement`

---

### Task 3: The connection attribute, the interface properties and candidate reading

**Files:**
- Modify: `src/SqlSource/Generation/AttributeSource.cs` (add `ConnectionAttributeMetadataName = "SqlSource.SqlSourceConnectionAttribute"`, `GenerateInterfaceProperty`, `InterfaceNameProperty`; add the attribute and two properties to `Text`)
- Modify: `src/SqlSource/Generation/TargetType.cs` (add `string MetadataName`, `bool? GenerateInterface`, `string? InterfaceName`), `TargetTypeReader.Read` (fill them; `MetadataName` = namespace + containing types joined by `+`, each its `MetadataName`, so `Compilation.GetTypeByMetadataName` finds it)
- Create: `src/SqlSource/Generation/Repositories/ConnectionCandidate.cs`, `CandidateMember.cs`, `CandidateType.cs`, `ConnectionCandidateReader.cs`, `RepositoryShape.cs`, `RepositoryShapeReader.cs`, `RepositoryInterfaces.cs`, `AGENTS.md` (rules: no `ISymbol` leaves `RepositoryShapeReader`; why it runs per compilation; maintenance footer)
- Modify: `src/SqlSource/SqlSourceGenerator.cs` (new step after `ClaimFiles`), `Generation/TrackingNames.cs` (`RepositoryInterfaces`, `RepositoryShapes`), `src/SqlSource/build/SqlSource.props:49-53` (`<CompilerVisibleProperty Include="SqlSourceRepositoryInterfaces" />`)
- Test: `tests/SqlSource.Tests/Generation/AttributeSourceTests.cs`, create `tests/SqlSource.Tests/Generation/Repositories/ConnectionCandidateReaderTests.cs` and `RepositoryInterfacesTests.cs`, `tests/SqlSource.Tests/Package/BuildFileTests.cs`, `Generator/CachingSettingsFileTests.cs`, create `Generator/CachingRepositoryTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  internal enum CandidateMember { Field, Property, Method, Parameter }
  internal enum CandidateType { ConnectionString, DataSource, Connection, Factory, Unsupported }
  internal sealed record ConnectionCandidate(
      string Name, CandidateMember Member, CandidateType Type,
      bool IsAsync, bool TakesCancellationToken, bool IsStatic, bool IsInherited,
      bool IsMarked, string? Database, string DeclaredTypeName, LocationInfo Location);
  internal sealed record RepositoryShape(
      string TypeKey, EquatableArray<ConnectionCandidate> Candidates,
      bool InterfaceOn, string InterfaceName, string? InterfaceTakenBy);
  internal static class RepositoryInterfaces {
      public const string PropertyName = "SqlSourceRepositoryInterfaces";
      public static bool Read(AnalyzerConfigOptions globalOptions); }
  internal static class RepositoryShapeReader {
      public static EquatableArray<RepositoryShape> Read(
          ImmutableArray<TargetType> types, Compilation compilation, bool interfacesOn, CancellationToken token); }
  ```
  `TypeKey` is the key `TypeQueries`/`MethodsInput` already join on.  `DeclaredTypeName` is the fully qualified metadata name of the member's type (or a factory's `T`), used by Task 4's engine check.

- [ ] **Step 1: Write the failing tests**
  - `AttributeSourceTests.Text_EveryType_IsEmbedded`: count 3.  New `Text_ConnectionAttribute_TargetsMembersAndParametersAndIsNotConditional`.  `CompileText` still compiles at C# 7.3.
  - `RepositoryInterfacesTests.Read_Value_IsOnUnlessFalse`: `null`, `""`, `"true"`, `"yes"` → `true`; `"false"`, `"FALSE"` → `false`.
  - `BuildFileTests.Props_PropertiesTheCompilerIsShown_...`: fourth item `RepositoryInterfaces.PropertyName`.
  - `ConnectionCandidateReaderTests` (compile a snippet, run the reader; one fact each):
    - `Read_PrimaryConstructorDataSource_IsAParameterCandidate`
    - `Read_FieldNamedConnectionString_IsAConnectionStringCandidate` (theory over the three names; `connstr` gives none)
    - `Read_ParameterStoredInAField_KeepsTheFieldAlone` (members before parameters)
    - `Read_RecordPositionalString_IsItsPropertyAlone`
    - `Read_MarkedFactory_IsAsyncAndTakesTheToken` (`ValueTask<NpgsqlConnection> Open(CancellationToken)`)
    - `Read_MarkedDbConnectionProperty_IsAConnection`
    - `Read_UnmarkedDbConnection_IsNotACandidate`
    - `Read_MarkedMethodWithTwoParameters_IsUnsupported`
    - `Read_MarkedMemberWithDatabase_CarriesIt`
    - `Read_ProtectedBaseMember_IsInherited` and `Read_PrivateBaseMember_IsNotACandidate`
    - `Read_AnyMarked_DropsConventionCandidates`
    - `Read_InterfaceNameTakenByAClass_CarriesTheTakenBy` and `Read_InterfaceNameIsAPartialInterface_IsNotTaken`
  - `CachingRepositoryTests.Run_BaseMemberMarkedLater_ResolvesTheDerivedRepository` (Review Focus 4): base in another file; second run edits only the base; assert `RepositoryShapes` output `Modified` and (after Task 5 lands, extend) the derived type's methods appear.  In this task assert the shape's candidates change.
- [ ] **Step 2: Run `dotnet test tests/SqlSource.Tests` — expect failures**
- [ ] **Step 3: Implement**
  - Step wiring: `TargetTypes.Collect().Combine(CompilationProvider).Combine(repositoryInterfaces).Select(RepositoryShapeReader.Read).WithTrackingName(RepositoryShapes)`.  It reruns each compilation by design (base types live in other files; see `ModelNameChecker.FindTaken` precedent) and returns an equatable array so later steps stay cached.
  - Only types whose `IsStatic` is false are read; a static type gets an empty candidate list.
  - Candidates: the type's members, accessible members of each base type (`compilation.IsSymbolAccessibleWithin`), and the primary constructor's parameters (`InstanceConstructors` whose `DeclaringSyntaxReferences` point at a `TypeDeclarationSyntax`).  Unmarked members of unaccepted types are dropped; marked ones become `Unsupported`.
  - Interface name: `InterfaceName ?? "I" + type name`; `InterfaceOn = GenerateInterface ?? interfacesOn`; `InterfaceTakenBy` = a description ("the class App.IUserRepository") when a type of that full name exists that is not an interface declared `partial` in source.
- [ ] **Step 4: Run tests — expect pass; update the "three properties" texts** (`src/SqlSource/build/AGENTS.md:8`, `src/SqlSource/AGENTS.md:38`, `docs/architecture/msbuild-and-manifest.md:14`, `docs/architecture/README.md:32`, `docs/architecture/generator.md:27-29,753`, comments in `TestOptionsProvider.cs:11`, `GeneratorHarness.cs:174`); regenerate golden files with `SQLSOURCE_UPDATE_GOLDEN=1 dotnet test tests/SqlSource.Tests` (attribute text changed) and review the diff is the attribute file alone
- [ ] **Step 5: `./pre-commit-validation.sh`, commit** `feat: read repository connection candidates and interface settings`

---

### Task 4: Resolve each repository's connection sources, with diagnostics

**Files:**
- Create: `src/SqlSource/Generation/Repositories/ConnectionSource.cs`, `ConnectionSourceKind.cs`, `ConnectionResolver.cs`, `ConnectionResolution.cs`
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs` (028–035 + `All`), `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md` (a new table "Repositories" after the one ending at SQLSRC027, and one section each)
- Modify: `Generation/Methods/MethodsInput.cs` (add `RepositoryShape? Repository`; `Gather` joins on `TypeKey`), `MethodPlanner.Plan` (resolve once per type before planning queries)
- Test: create `tests/SqlSource.Tests/Generation/Repositories/ConnectionResolverTests.cs`, create `tests/SqlSource.Tests/Generator/RepositoryDiagnosticsTests.cs`, `tests/SqlSource.Tests/Diagnostics/SqlDiagnosticsTests.cs` (`Configurable` lists 031)

**Interfaces:**
- Consumes: `ConnectionCandidate`, `RepositoryShape` (Task 3).
- Produces:
  ```csharp
  internal enum ConnectionSourceKind { ConnectionString, DataSource, Factory, Borrowed }
  internal sealed record ConnectionSource(
      ConnectionSourceKind Kind, string MemberName, string AccessText,
      bool IsAsync, bool TakesCancellationToken);   // AccessText: "this.X", "X" (parameter), or "global::Ns.T.X" (static); escaped with @ where needed
  internal readonly record struct NeededDatabase(string Name, DatabaseEngine Engine);
  internal sealed record ConnectionResolution(
      EquatableArray<(string Database, ConnectionSource Source)> Sources,
      EquatableArray<string> FailedDatabases,
      EquatableArray<DiagnosticInfo> Diagnostics);
  internal static class ConnectionResolver {
      public static ConnectionResolution Resolve(
          TargetType type, RepositoryShape shape, IReadOnlyCollection<NeededDatabase> needed); }
  ```
  Needed databases are the distinct `(QuerySettings.ResolveDatabase(...), engine)` of the type's `Repository` queries.  A query whose database is in `FailedDatabases` gets no method; every type with a static repository fails all.

**Diagnostics** (titles are the `**Title**` line of their manual section; messages name the type, database and members):

| Id | Severity | Title | Reported at |
|----|----|----|----|
| SQLSRC028 | Error | Repository has no connection | attribute; says to add a `connectionString` or `DbDataSource` member, or `[SqlSourceConnection]`, or set the output to `methods` |
| SQLSRC029 | Error | Repository has more than one connection | attribute; lists the members |
| SQLSRC030 | Error | Connection needs a database | the marked member; the type uses several databases |
| SQLSRC031 | Warning (configurable) | Connection's database is not used | the marked member |
| SQLSRC032 | Error | Connection member is not supported | the marked member; lists accepted shapes |
| SQLSRC033 | Error | Connection is of another database engine | the member (`Npgsql.*` type for mssql, `Microsoft.Data.SqlClient.*` for postgres) |
| SQLSRC034 | Error | Repository is static | attribute; suggests the output `methods` |
| SQLSRC035 | Error | Repository interface name is taken | attribute (reported in Task 6) |

- [ ] **Step 1: Write the failing tests**
  - `ConnectionResolverTests` (pure, hand-built candidates): `Resolve_OneConventionCandidate_IsTheSource`, `Resolve_TwoConventionCandidates_ReportsSqlsrc029`, `Resolve_NoCandidate_ReportsSqlsrc028AndFailsTheDatabase`, `Resolve_TwoDatabasesWithoutMarks_ReportsSqlsrc028ForEach`, `Resolve_MarkedWithoutDatabaseOnTwoDatabases_ReportsSqlsrc030`, `Resolve_MarkedPerDatabase_ResolvesEach`, `Resolve_MarkedForAnUnusedDatabase_WarnsSqlsrc031AndResolvesTheRest`, `Resolve_UnsupportedMarked_ReportsSqlsrc032`, `Resolve_NpgsqlDataSourceForMssql_ReportsSqlsrc033`, `Resolve_StaticType_ReportsSqlsrc034`, and kinds: string → `ConnectionString`, `DataSource`, method → `Factory`, field/property/parameter `Connection` → `Borrowed`.
  - `RepositoryDiagnosticsTests` (generator runs at `OutputKind.Repository`, sidecar entries via `MethodRuns`-style helper — add `RepositoryRuns` in `tests/SqlSource.Testing/Generator/` mirroring `MethodRuns` at `OutputKind.Repository`): `Run_RepositoryWithoutAConnection_ReportsSqlsrc028AtTheAttribute`, `Run_StaticRepository_ReportsSqlsrc034`, `Run_ParameterStoredInAField_UsesTheFieldAndCompilesWithoutWarnings` (Review Focus 2; extend in Task 5 to assert the method uses `this._dataSource`; here assert no diagnostics).
- [ ] **Step 2: Run — expect failures**
- [ ] **Step 3: Implement the resolver, descriptors, release rows and manual sections**; diagnostics go into `TypeMethods.Diagnostics` (not `TargetType.Diagnostics`, which would drop the type's other files)
- [ ] **Step 4: Run `dotnet test tests/SqlSource.Tests tests/SqlSource.Repository.Tests` — expect pass** (the repository tests check the diagnostics page)
- [ ] **Step 5: `./pre-commit-validation.sh`, commit** `feat: resolve repository connections`

---

### Task 5: Emit repository methods

Inline emission, the path `docs/architecture/generator.md` "Adding a kind of method" prescribes: the receiver and ownership become strategies.

**Files:**
- Modify: `Generation/Methods/QueryMethod.cs` (add `public ConnectionSource? Source { get; init; }`), `MethodPlanner.Plan` (repository query → method with `Host = new MethodHost(visibility, MemberLocation.Class, null)` and `Source` from the resolution; name checks are the `class` ones)
- Modify: `Generation/Methods/MethodArguments.cs` (when `Source` is set: no receiver, no `transaction`, no `commandTimeout`; rewrite `Enumerator` to count positions instead of hard-coding 2 leading / 3 trailing)
- Modify: `Generation/Emitting/QueryMethods/HeaderWriter.cs` (`ModifiersOf`: no `static` when `Source` set; `AppendChecks`: no receiver check), `AsyncSpelling.IteratorModifiersOf` (no `static` when `Source` set), `QueryMethodLocals.cs` (connection is a local name from `LocalNames` when `Source` set), `CommandWriter.cs` (acquire step; guard the `commandTimeout` line on the argument existing), `ExecutionCapabilityRules.Of` (no `RefusesCompletedTransaction`, `ChecksRollbackAtRead`, `ChecksRollbackAfterDatabaseError` when the method has no transaction argument)
- Modify: `Generation/Methods/DatabaseEngineExtensions.cs` (add `ConnectionTypeText`)
- Modify: `tests/SqlSource.Testing/Generator/Corpus/` (add a repository dimension: one scenario per `ConnectionSourceKind` × {one-optional, many, streaming}, both engines)
- Test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/QueryMethodWriterTests.cs`, `Generation/Methods/{MethodArgumentsTests,DatabaseEngineExtensionsTests}.cs`, `Generation/Emitting/QueryMethods/ExecutionCapabilityRulesTests.cs`, create `Generator/RepositoryMethodTests.cs`, `Generator/MethodCompileTests.cs`, golden files

**Interfaces:**
- Consumes: `ConnectionSource`, `ConnectionResolution` (Task 4).
- Produces: `public static string ConnectionTypeText(this DatabaseEngine engine)` → `"global::Npgsql.NpgsqlConnection"` / `"global::Microsoft.Data.SqlClient.SqlConnection"`.

**Acquire statements** (then the existing `wasClosed` open/close code runs unchanged; `connection` is the local's name):

| Kind | Statement |
|----|----|
| `ConnectionString` | `await using var connection = new {ConnectionTypeText}({Access} ?? throw Null());` |
| `DataSource` | `await using var connection = await ({Access} ?? throw Null()).OpenConnectionAsync(cancellationToken).ConfigureAwait(false);` |
| `Factory` | `await using var connection = [await ]{Access}({cancellationToken?})[.ConfigureAwait(false)] ?? throw Null();` |
| `Borrowed` | `var connection = {Access} ?? throw Null();` |

`Null()` stands for `new global::System.InvalidOperationException("The connection source '{MemberName}' of '{TypeDisplayName}' is null.")`, written inline.  In a streaming method the acquire statement is inside the iterator, so the connection lives until the loop ends.

- [ ] **Step 1: Write the failing tests**
  - `QueryMethodWriterTests.Append_RepositoryDataSource_IsTheWholeMethod`: exact text via `GeneratedFile.Joined` of a `public async … GetUser(global::App.GetUserParams parameters, global::System.Threading.CancellationToken cancellationToken = default)` instance method, acquire from `this._dataSource`, no `transaction`, no `CommandTimeout`.
  - `Append_RepositoryKind_AcquiresAsTheTableSays` (theory over the four kinds, asserting the table's statement).
  - `Append_RepositoryStream_AcquiresInsideANonStaticIterator`.
  - `Append_RepositoryOnSqlServer_WritesNoTransactionCheck` (no `transaction.Connection`, still drains later results).
  - `MethodArgumentsTests.Of_Repository_IsInputTokensAndCancellation`.
  - `DatabaseEngineExtensionsTests.ConnectionTypeText_Engine_IsTheDriversConnection`.
  - `RepositoryMethodTests.Run_DataSourceParameter_WritesAnInstanceMethod`, `Run_TwoDatabases_UsesEachMarkedSource`, `Run_RepositoryMethodNamedLikeAMember_ReportsSqlsrc274`, `Run_ProtectedVisibility_WritesAProtectedInstanceMethod`, and extend `Run_ParameterStoredInAField_...` to assert `this._dataSource`.
  - `MethodCompileTests.Run_RepositoryOfEachKind_CompilesWithoutWarnings` (both drivers referenced).
  - Extend `CachingRepositoryTests.Run_BaseMemberMarkedLater_ResolvesTheDerivedRepository` to assert the derived method appears.
- [ ] **Step 2: Run — expect failures**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Regenerate golden files (`SQLSOURCE_UPDATE_GOLDEN=1`), review each new repository golden reads as the table says; run `dotnet test tests/SqlSource.Tests tests/SqlSource.Tests.RoslynFloor` — expect pass**
- [ ] **Step 5: `./pre-commit-validation.sh`, commit** `feat: emit repository methods`

---

### Task 6: Generate the repository interface

**Files:**
- Modify: `Generation/Methods/TypeMethods.cs` (add `string? InterfaceName`, `bool InterfaceIsPublic`), `MethodPlanner.Plan` (set them when the type claims any `Repository` query and `shape.InterfaceOn`; report SQLSRC035 and set `InterfaceName = null` when `InterfaceTakenBy` is set)
- Modify: `Generation/Emitting/MethodEmitter.cs` (`Emit` writes the file when `InterfaceName` is set even with no methods; `AppendOnType` adds ` : global::{ns}.{containers}.{InterfaceName}{TypeParameters}` to the innermost declaration and writes the interface after it, inside the same containers)
- Modify: `Generation/Emitting/QueryMethods/QueryMethodWriter.cs` (add `AppendInterfaceMember(StringBuilder, int indent, QueryMethod)`: documentation + signature + `;`, no modifiers, no defaults dropped)
- Test: `Generation/Emitting/MethodEmitterTests.cs`, create `Generator/RepositoryInterfaceTests.cs`, `Generator/MethodCompileTests.cs`

**Interfaces:**
- Consumes: `RepositoryShape.InterfaceOn/InterfaceName/InterfaceTakenBy` (Task 3), repository `QueryMethod`s (Task 5).

Rules (from the spec): members are the repository methods whose visibility is `public`; the interface is `public` when `TargetType.IsPublic`, else `internal`; it carries the type's type parameters and no constraints; it is written even when every repository method has an error.

- [ ] **Step 1: Write the failing tests**
  - `RepositoryInterfaceTests.Run_RepositoryType_ImplementsItsGeneratedInterface` (exact interface text and `partial class UserRepository : global::App.IUserRepository`).
  - `Run_ProtectedAndInternalMethods_AreNotInTheInterface`.
  - `Run_EveryMethodFails_StillWritesTheEmptyInterface`.
  - `Run_GenerateInterfaceFalse_WritesNone`; `Run_PropertyFalseAndAttributeTrue_WritesOne`; `Run_InterfaceName_RenamesIt`.
  - `Run_InterfaceNameTakenByAClass_ReportsSqlsrc035AndWritesNone`.
  - `Run_UserPartialInterface_AddsItsOwnMembers` (user declares `partial interface IUserRepository { void Extra(); }` and implements `Extra`; compiles).
  - `Run_TypeWithOnlySqlQueries_WritesNoInterface`.
  - `MethodCompileTests.Run_RepositoryOnAwkwardTypes_Compiles` (Review Focus 3; theory: generic `Repo<T>`, nested in a partial class, `partial struct`, `partial record`, `partial record struct`).
- [ ] **Step 2: Run — expect failures**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Golden regen and review; run `tests/SqlSource.Tests` and RoslynFloor — expect pass**
- [ ] **Step 5: `./pre-commit-validation.sh`, commit** `feat: generate the repository interface`

---

### Task 7: Make `repository` the default

**Files:**
- Modify: `QuerySettings.Resolve` (l.86) default `OutputKind.Repository`; `schemas/settings-v1.schema.json` description "Default `repository`"
- Modify (tests and fixtures that rely on the default, per the Explore report): `Settings/QuerySettingsTests.cs:120` (rename to `Resolve_Output_IsRepositoryUnlessALevelSaysOtherwise`), `Generator/{GeneratorHarnessTests,TypeOverrideLevelTests,SettingsFileTests,SettingLevelTests,SettingsFileBuildFoldersTests}.cs` default-output call sites, `tests/SqlSource.Tool.Tests/Planning/GeneratorParityTests.cs:401-520`, `tests/SqlSource.Tool.Testing/OnlineBuilds.cs:77`, `tests/SqlSource.Testing/Generator/GeneratorHarness.cs:141-145` comment, tool fixture projects under `tests/SqlSource.Tool.Tests/Fixtures/Projects/*/sqlsource.json` (add `"output": "methods"` where claims are `static`; `Plans.cs` writes static claims — give its settings `methods`)
- Modify: `examples/*/Methods/sqlsource.json` (create, `"output": "methods"`), `examples/AGENTS.md` rule 4 (four folders; `Repository/` uses the default)
- Test: those above

- [ ] **Step 1: Change `QuerySettingsTests` default assertion; run — expect failure**
- [ ] **Step 2: Flip the default; run the whole `tests/SqlSource.Tests` and `tests/SqlSource.Tool.Tests`; for each failure decide:** a test about the default's own behaviour asserts the repository behaviour (e.g. `SettingsFileTests` message now names `repository`); a test that only needed *a* method output pins `OutputKind.Methods` explicitly
- [ ] **Step 3: Run until green; build both example projects (`dotnet build SqlSource.slnx`)**
- [ ] **Step 4: `./pre-commit-validation.sh`, commit** `feat: make repository the default output`

---

### Task 8: End-to-end database tests and examples

**Files:**
- Create: `tests/DatabaseTests/Postgres/EndToEnd/Queries/Repository.sql` (header `-- output: repository`; `GetNote -> one-optional`, `ListNotes` with `-- collection-type: IAsyncEnumerable`, `TouchNote -> rowcount`, against existing `app` tables), its sidecar via `tools/describe-end-to-end.sh`
- Create: `tests/DatabaseTests/Postgres/EndToEnd/RepositoryQueries.cs`: four claims of the file — `DataSourceRepository(NpgsqlDataSource dataSource)`, `ConnectionStringRepository(string connectionString)`, `FactoryRepository` with `[SqlSourceConnection] ValueTask<NpgsqlConnection> OpenAsync(CancellationToken)` that records each connection it makes, `BorrowedRepository` with `[SqlSourceConnection] DbConnection Connection { get; }`
- Create: `tests/DatabaseTests/Postgres/EndToEnd/RepositoryQueryTests.cs`
- Create the SQL Server mirror (`ConnectionString`, `Factory`, `Borrowed`; no data source) under `tests/DatabaseTests/SqlServer/EndToEnd/`
- Modify: the "The queries" tables in `tests/DatabaseTests/Postgres/README.md` and `tests/DatabaseTests/SqlServer/README.md`
- Create: `examples/{PostgresExamples,SqlServerExamples}/Repository/` (one `UserRepository.cs`/`.sql`, Postgres from a primary-constructor `NpgsqlDataSource`, SQL Server from `connectionString`; a short consumer that mocks nothing but takes `IUserRepository`), sidecars via the script; examples READMEs' folder table

- [ ] **Step 1: Write the tests** (`[Collection(UsesPostgres.Name)]`, sources from `EndToEndSources`)
  - `GetNote_EachSource_ReadsTheRow` (theory over the four claims).
  - `ListNotes_BreakAfterFirstRow_DisposesTheFactoryConnection` (Review Focus 1): the factory's recorded connection is `Closed` and disposed after `break`.
  - `ListNotes_BorrowedOpenConnection_IsLeftOpen` and `GetNote_BorrowedClosedConnection_IsClosedAgain`.
  - `GetNote_NullConnectionString_ThrowsNamingTheMember` (Review Focus 5): `InvalidOperationException` "The connection source 'connectionString' of '…ConnectionStringRepository' is null."
  - `TouchNote_ThroughTheInterface_CountsTheRow` (calls via `IDataSourceRepository`).
  - SQL Server: the same minus the data source.
- [ ] **Step 2: Run `tools/describe-end-to-end.sh` (Docker) to write sidecars; never hand-edit**
- [ ] **Step 3: Run `dotnet test tests/DatabaseTests/Postgres` and `tests/DatabaseTests/SqlServer` — expect pass; build the examples**
- [ ] **Step 4: `./pre-commit-validation.sh`, commit** `test: run repository methods against both databases`

---

### Task 9: User and architecture documentation

**Files:**
- Modify: `README.md` — step 2/4 of getting started show `public partial class UserRepository(NpgsqlDataSource dataSource);` and `await users.GetUser(new GetUserParams(42))`; the "Only want models" and FAQ sections name `methods`; a FAQ "How do I mock a repository?" (the generated interface, `SqlSourceRepositoryInterfaces`) and "How do I run queries in a transaction?" (`-- output: methods` and hand-written code); `--prerelease` and absolute links kept
- Modify: `docs/manual/generator.md` — new "Repository methods" section (signature, connection sources table with lifetimes, convention and attribute, members before parameters, interface); "Query methods" retitled to the `methods` output; "Known issues": the PostgreSQL connection-string limitation
- Modify: `docs/manual/settings.md` — output table and default, member-location and visibility tables gain the `repository` rows, new section "Repository interfaces" for `SqlSourceRepositoryInterfaces` and the two attribute properties, the "Settings that work today" table
- Modify: `docs/manual/postgresql.md` (connection-string limitation, pointing to a `DbDataSource`), `docs/manual/recipes.md` (transactions with `methods` inside a repository; a connection from a provider interface; a SQL Server repository from a connection string), `docs/manual/README.md` "What works today" row, `docs/manual/concepts.md`, `sql-files.md` output row
- Modify: `docs/architecture/generator.md` (repository shapes step, resolution, the ownership strategy in "Adding a kind of method", the interface), `docs/architecture/tool.md` where outputs are listed
- Modify: `CONTRIBUTING.md` only if a command changed (none expected)

- [ ] **Step 1: Write the docs; every `sql` sample carries its dialect info string**
- [ ] **Step 2: Run `dotnet test tests/SqlSource.Repository.Tests` — expect pass (links, samples, diagnostics page, AGENTS.md sizes)**
- [ ] **Step 3: `./pre-commit-validation.sh`, commit** `docs: document the repository output`

---

### Task 10: Finish

- [ ] **Step 1:** `git fetch --tags origin`; compare `VersionPrefix` in `Directory.Build.props` with `git tag --list 'v*' --sort=-v:refname | head -n 1`; if not greater, ask the user for the new version
- [ ] **Step 2:** `./pre-commit-validation.sh --full` — expect pass
- [ ] **Step 3:** Run `branch-review:review`; fix findings (minor ones too) and rerun until clean
- [ ] **Step 4:** Offer to open the pull request
