# Connection Sources Beyond the Repository Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A repository gets its connection from a marked instance member of a type it holds (a held factory) or from a marked static member anywhere in the project (a global), resolved in three tiers where the most specific wins.

**Architecture:** Tier 1 is today's reading of the repository's own members, unchanged.  `ConnectionCandidateReader` gains a walk through each held member into its type for tier 2.  A new `ForAttributeWithMetadataName` stage finds every `[SqlSourceConnection]` and keeps the static members and stray parameters as `GlobalMark`s.  A step over the compilation checks them once, and `RepositoryShapeReader` turns them into tier-3 candidates for each repository.  `ConnectionResolver` decides each database from the first tier that has a candidate for it.  The emitter reaches a held member through its holder and a global through its fully qualified type.

**Tech Stack:** C# Roslyn incremental source generator (`netstandard2.0`, floor Roslyn 4.14), xUnit v3 with Shouldly, Npgsql and Microsoft.Data.SqlClient end-to-end tests in Docker.

**Spec:** [`docs/superpowers/specs/2026-11-16-connection-sources-beyond-the-repository-design.md`](../specs/2026-11-16-connection-sources-beyond-the-repository-design.md).  Read it before any task: it states every rule the tests below pin.

## Global Constraints

- Root `AGENTS.md` rules apply: every commit passes `./pre-commit-validation.sh`, run in a login shell (`zsh -lc`), after the last edit; nothing is pushed before `./pre-commit-validation.sh --full` passes on the exact tree.
- Tier 1 behaviour is unchanged: no existing test of `ConnectionCandidateReaderTests`, `ConnectionResolverTests`, `RepositoryDiagnosticsTests` or `RepositoryMethodTests` changes its expected result.  Only test helpers that construct records may change.
- No `ISymbol` or syntax node leaves `RepositoryShapeReader` or the new global steps: every value between steps is an equatable record (`src/SqlSource/Generation/Repositories/AGENTS.md`).
- A global is only a mark in the project's own source.  Marks in referenced assemblies are read only through a holder.
- No new diagnostic id.  Diagnostics reuse `SQLSRC028`–`SQLSRC033`, `SQLSRC034` and `SQLSRC036`.
- Compare database names with `DatabaseName.Comparer`.
- Non-goals are not deferred work: no `docs/deferred` entry for a database-name argument, unmarkable factory types, referenced-assembly globals or deeper holder chains.
- Code shared with `tests/SqlSource.Tests.RoslynFloor` compiles on Roslyn 4.14.0: use no Roslyn API newer than that.

## Review Focus

1. **A mark on a base interface of the held interface** (`IDatabaseFactory : IConnectionOpener`, mark on `IConnectionOpener.Open`): it is found.  Test in Task 2.
2. **A nullable holder** (`IDatabaseFactory? dbf`) under `<Nullable>enable</Nullable>` with warnings as errors: the generated code compiles with no warning.  Test in Task 5.
3. **A struct repository whose streaming method uses a held factory with a null holder**: it throws `InvalidOperationException` naming the holder, not `NullReferenceException`.  Test in Task 5.
4. **A factory parameter stored in a field** (`UserRepository(IDatabaseFactory dbf) { private readonly IDatabaseFactory _dbf = dbf; }`): one candidate, `_dbf.Open`, no `SQLSRC029` and no `CS9124`.  Test in Task 2.
5. **A global in a nested static class** (`App.Outer.Db.ConnectionString`): reached as `global::App.Outer.Db.ConnectionString`.  Test in Task 5.

---

### Task 1: The candidate records carry a tier and a holder

**Files:**
- Create: `src/SqlSource/Generation/Repositories/CandidateTier.cs`
- Create: `src/SqlSource/Generation/Repositories/CandidateHolder.cs`
- Modify: `src/SqlSource/Generation/Repositories/ConnectionCandidate.cs`
- Modify: `src/SqlSource/Generation/Repositories/RepositoryShape.cs`
- Modify: `src/SqlSource/Generation/Repositories/ConnectionSource.cs`
- Modify: `src/SqlSource/Generation/Repositories/RepositoryShapeReader.cs` (pass empty arrays for the new shape members)
- Test: `tests/SqlSource.Tests/Generation/Repositories/ConnectionCandidateTests.cs` (create)
- Modify: the `Shape(...)` helper of `tests/SqlSource.Tests/Generation/Repositories/ConnectionResolverTests.cs` and any other test that constructs a `RepositoryShape` or `ConnectionSource` positionally

**Interfaces:**
- Produces:
  - `internal enum CandidateTier { Own, Held, Global }`
  - `internal sealed record CandidateHolder(string Name, CandidateMember Member, bool IsStatic, string TypeName, LocationInfo Location)`: `Member` is `Field`, `Property` or `Parameter`, and `TypeName` is the holder type's simple name, `IDatabaseFactory`, for messages.
  - `ConnectionCandidate` gains trailing parameters `CandidateTier Tier = CandidateTier.Own`, `CandidateHolder? Holder = null` and `string? GlobalTypeText = null`, the fully qualified type of a global, `global::App.Outer.Db`.
  - `ConnectionCandidate.DisplayName`, a computed `string`: `Name` for `Own`, `Holder.Name + "." + Name` for `Held`, and the last dotted segment of `GlobalTypeText`, then `"." + Name`, for `Global`.
  - `RepositoryShape` gains `EquatableArray<ConnectionCandidate> HeldCandidates` and `EquatableArray<ConnectionCandidate> GlobalCandidates`, after `Candidates`.
  - `ConnectionSource` gains trailing parameters `string? HolderAccessText = null` and `string? HolderName = null`.  `MemberName` now holds the candidate's `DisplayName`.

- [ ] **Step 1: Write the failing tests**

`ConnectionCandidateTests.DisplayName_OwnMember_IsItsName` asserts `"_connectionString"`.  `DisplayName_HeldMember_IsHolderDotName` asserts `"dbf.Open"` for `Holder = new("dbf", CandidateMember.Parameter, false, "IDatabaseFactory", location)` and `Name = "Open"`.  `DisplayName_Global_IsTypeDotName` asserts `"Db.Reports"` for `GlobalTypeText = "global::App.Outer.Db"` and `Name = "Reports"`.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generation.Repositories.ConnectionCandidateTests"`
Expected: build FAIL, `CandidateHolder` not defined.

- [ ] **Step 3: Add the records and members listed under Interfaces**

`RepositoryShapeReader` passes `EquatableArray<ConnectionCandidate>.Empty` for both new arrays until Tasks 2 and 3 fill them.  The resolver keeps setting `MemberName` from `candidate.DisplayName`.

- [ ] **Step 4: Run the whole unit project**

Run: `dotnet test --project tests/SqlSource.Tests`
Expected: PASS, with no other test changed in what it expects.

- [ ] **Step 5: Validate and commit**

Run `zsh -lc ./pre-commit-validation.sh`, then:

```bash
git add src/SqlSource/Generation/Repositories tests/SqlSource.Tests
git commit -m "refactor: give connection candidates a tier and a holder"
```

### Task 2: Read held factories (tier 2)

**Files:**
- Modify: `src/SqlSource/Generation/Repositories/ConnectionCandidateReader.cs`
- Modify: `src/SqlSource/Generation/Repositories/RepositoryShapeReader.cs`
- Test: `tests/SqlSource.Tests/Generation/Repositories/HeldFactoryReaderTests.cs` (create, using `RepositoryShapes.Of` and `RepositoryShapes.Library`)

**Interfaces:**
- Consumes: Task 1's records.
- Produces:
  - `ConnectionCandidateReader.ReadHeld(INamedTypeSymbol type, Compilation compilation, LocationInfo fallback, CancellationToken cancellationToken) -> EquatableArray<ConnectionCandidate>`, the tier-2 candidates, each with `Tier = Held` and its `Holder`, in the order the holders are declared: own members, then base types' members, then parameters.
  - `ConnectionCandidateReader.ReadMarkedMember(ISymbol member, bool isInherited, LocationInfo fallback) -> ConnectionCandidate?`, the existing private `ReadMember` made `internal` and documented as the one rule of what a marked member gives.  Task 3 uses it.
  - `RepositoryShape.HeldCandidates` is filled by `RepositoryShapeReader`.

- [ ] **Step 1: Write the failing tests**

Each test reads `RepositoryShapes.Of(source).HeldCandidates`:
- `ReadHeld_InterfaceMethodMarked_IsAHeldFactory`: `IDatabaseFactory` marks `Task<DbConnection> Open(CancellationToken ct)`, and `UserRepository(IDatabaseFactory dbf)`.  One candidate: `Name "Open"`, `Type Factory`, `IsAsync true`, `TakesCancellationToken true`, `Holder.Name "dbf"`, `Holder.Member Parameter`, `Tier Held`.
- `ReadHeld_EachKindOfMember_IsRead`, a theory: a marked `string` property gives `ConnectionString`, an `NpgsqlDataSource` property `DataSource`, a `DbConnection` property `Connection`, and a method returning `DbConnection` `Factory`.
- `ReadHeld_MarkOnBaseInterface_IsFound` (Review Focus 1): `IDatabaseFactory : IConnectionOpener`, mark on `IConnectionOpener.Open`.
- `ReadHeld_ClassImplementingMarkedInterface_IsMarked`: the holder is typed by `DatabaseFactory : IDatabaseFactory`, and only the interface member carries the mark with `Database = "audit"`.  The candidate's `Database` is `"audit"`.
- `ReadHeld_ParameterStoredInField_HoldsTheFieldAlone` (Review Focus 4): one candidate whose `Holder.Name` is `"_dbf"`.
- `ReadHeld_ParameterPassedToBase_HoldsTheBaseMember`: `UserRepository(IDatabaseFactory dbf) : RepositoryBase(dbf)`, where the base has `protected IDatabaseFactory Factory { get; }`.  One candidate whose holder is `Factory`.
- `ReadHeld_FieldOfATypeWithoutMarks_DoesNotHideTheParameter`: `UserRepository(IDatabaseFactory dbf, ILogger logger) { private readonly ILogger _logger = logger; }`.  One candidate, holder `dbf`.
- `ReadHeld_TwoHolders_AreTwoCandidates`: `_main` and `_audit` of one factory type give `DisplayName`s `_main.Open` and `_audit.Open`.
- `ReadHeld_StaticFactoryMember_IsNotHeld`: a marked static member of the factory type gives no held candidate.
- `ReadHeld_MarkedHolder_IsNotAHolder`: a marked `NpgsqlDataSource` field stays in `Candidates` and gives nothing in `HeldCandidates`.
- `ReadHeld_FactoryInReferencedAssembly_IsFound`: the factory is in `RepositoryShapes.Library(...)`.
- `ReadHeld_UnreachableMember_IsNotACandidate`: a marked `private` member of a factory class gives nothing.
- `ReadHeld_UnsupportedMember_IsAnUnsupportedCandidate`: a marked factory method `Open(int retries)` gives `Type Unsupported`.
- `ReadHeld_RepositoryImplementingMarkedInterface_GainsNoMark`: a repository with `string connectionString` that implements an interface with a marked member.  `Candidates` is unchanged: one convention candidate, `IsMarked false`.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generation.Repositories.HeldFactoryReaderTests"`
Expected: FAIL, `HeldCandidates` is empty.

- [ ] **Step 3: Implement `ReadHeld` and fill `HeldCandidates`**

- Holders are found with the reachability and base-type walk that `Read` uses today.  A holder's type is not a source type, it has at least one candidate, and it is not marked.
- Parameters are holders only when no field or property is a holder, and not when `IsReadBesides` holds.
- A factory type's members are those of the type, its base types and `AllInterfaces`, instance members only, reachable from the repository.  An implementation's own symbol and the interface member it implements are one candidate.
- Extend `FindMarking` with a flag that also follows `FindImplementationForInterfaceMember` back to the marked interface member.  Only `ReadHeld` sets it, so tier 1 is unchanged.

- [ ] **Step 4: Run the reader tests and the whole unit project**

Run: `dotnet test --project tests/SqlSource.Tests`
Expected: PASS.

- [ ] **Step 5: Validate and commit**

```bash
git add src/SqlSource/Generation/Repositories tests/SqlSource.Tests
git commit -m "feat: read the marked members of factories a repository holds"
```

### Task 3: Find and check global marks (tier 3)

**Files:**
- Create: `src/SqlSource/Generation/Repositories/GlobalMark.cs`
- Create: `src/SqlSource/Generation/Repositories/GlobalMarkReader.cs`
- Create: `src/SqlSource/Generation/Repositories/GlobalConnections.cs`
- Modify: `src/SqlSource/SqlSourceGenerator.cs` (`ReadRepositories`, and a new `ReportGlobalConnections` stage wired in `Initialize`)
- Modify: `src/SqlSource/Generation/TrackingNames.cs`
- Modify: `src/SqlSource/Generation/Repositories/RepositoryShapeReader.cs`
- Test: `tests/SqlSource.Tests/Generation/Repositories/GlobalConnectionsTests.cs` (create)
- Test: `tests/SqlSource.Tests/Generator/GlobalConnectionDiagnosticsTests.cs` (create)

**Interfaces:**
- Consumes: `ConnectionCandidateReader.ReadMarkedMember` (Task 2).
- Produces:
  - `internal sealed record GlobalMark(string DeclarationId, int? ParameterOrdinal, LocationInfo Location)`.  `DeclarationId` is `ISymbol.GetDocumentationCommentId()` of the member, or of the method or type that declares the parameter, and `ParameterOrdinal` is set for a parameter.
  - `GlobalMarkReader.IsCandidate(SyntaxNode node) -> bool`, for `VariableDeclaratorSyntax`, `PropertyDeclarationSyntax`, `MethodDeclarationSyntax` and `ParameterSyntax`.
  - `GlobalMarkReader.Read(GeneratorAttributeSyntaxContext context, CancellationToken token) -> GlobalMark?`: a mark for a static member or a parameter, and null for an instance member.
  - `GlobalConnections.Check(EquatableArray<GlobalMark> marks, Compilation compilation, CancellationToken token) -> EquatableArray<DiagnosticInfo>`: `SQLSRC032` at the mark for each broken global.
  - `GlobalConnections.CandidatesFor(EquatableArray<GlobalMark> marks, INamedTypeSymbol repository, Compilation compilation) -> EquatableArray<ConnectionCandidate>`: the valid globals the repository can reach, each `Tier = Global` with its `GlobalTypeText`.
  - `RepositoryShapeReader.Read(EquatableArray<TargetType> types, Compilation compilation, bool interfacesOn, EquatableArray<GlobalMark> globalMarks, CancellationToken token)` fills `GlobalCandidates`.
  - `TrackingNames.GlobalConnectionMarks` and `TrackingNames.GlobalConnectionChecks`.

- [ ] **Step 1: Write the failing unit tests**

`GlobalConnectionsTests`, reading `RepositoryShapes.Of(source).GlobalCandidates`:
- `Global_StaticPropertyInStaticClass_IsACandidate`: `public static class Db { [SqlSourceConnection] public static NpgsqlDataSource Main { get; set; } = null!; }` gives `Type DataSource`, `Tier Global`, `GlobalTypeText "global::App.Db"`, `DisplayName "Db.Main"`.
- `Global_EachKind_IsRead`, a theory: a static `string` field, a `const string`, a static `DbConnection` property, and a static method returning `Task<DbConnection>`.
- `Global_StaticMemberOfARepository_IsNotGlobal`: a marked static member of another `[SqlSourceGenerate]` type is not in `GlobalCandidates`.
- `Global_Unreachable_IsNotACandidate`: a `private static` member of a static class.
- `Global_InReferencedAssembly_IsNotACandidate`: a public marked static in `RepositoryShapes.Library(...)`.
- `Global_NestedType_IsReachedThroughItsFullName` (Review Focus 5): `GlobalTypeText "global::App.Outer.Db"`.

`GlobalConnectionDiagnosticsTests`, generator runs through `RepositoryRuns.Run`:
- `Run_GlobalInGenericType_ReportsSqlsrc032OnceAtTheMark`: two repositories, one `SQLSRC032` at the mark's location.
- `Run_StaticAbstractInterfaceMember_ReportsSqlsrc032`.
- `Run_MarkedMethodParameter_ReportsSqlsrc032`, a parameter of an ordinary method of a non-repository class.
- `Run_MarkedPrimaryConstructorParameterOfNonRepository_ReportsSqlsrc032`.
- `Run_UnsupportedGlobal_ReportsSqlsrc032OnceAtTheMark`: a static method with an `int` parameter, and two repositories.  Exactly one diagnostic.
- `Run_BrokenGlobalWithoutRepositories_StillReported`.
- `Run_MarkedInstanceMemberOutsideRepository_ReportsNothing`.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/SqlSource.Tests -- --filter-class "*GlobalConnection*"`
Expected: FAIL.

- [ ] **Step 3: Implement the stage**

- `ForAttributeWithMetadataName(AttributeSource.ConnectionAttributeMetadataName, GlobalMarkReader.IsCandidate, GlobalMarkReader.Read)` `.WhereNotNull().Collect()` is sorted by `DeclarationId` and `ParameterOrdinal` into `EquatableArray<GlobalMark>`, tracked as `GlobalConnectionMarks`.
- The transform reads only the mark's own symbol.  Whether the containing type is a repository, what the member's type derives from and whether it is reachable are decided by `GlobalConnections` over `CompilationProvider`, since those may change in another file.
- `ReportGlobalConnections` combines the marks with `CompilationProvider`, selects `GlobalConnections.Check`, is tracked as `GlobalConnectionChecks`, and reports through `ReportAll`.
- `ReadRepositories` adds the marks to its `Combine`.
- A global is broken when `ReadMarkedMember` gives `Unsupported`, when its containing type or any type around it is generic, or when it is `static abstract` or `static virtual`.  A parameter is broken unless it belongs to the primary constructor of a `[SqlSourceGenerate]` type, which tier 1 reads.
- Static members of `[SqlSourceGenerate]` types are skipped by both methods.

- [ ] **Step 4: Run the unit project**

Run: `dotnet test --project tests/SqlSource.Tests`
Expected: PASS.

- [ ] **Step 5: Validate and commit**

```bash
git add src/SqlSource tests/SqlSource.Tests
git commit -m "feat: find the project's global connection marks and check them once"
```

### Task 4: Resolve each database from the first tier that has a candidate

**Files:**
- Modify: `src/SqlSource/Generation/Repositories/ConnectionResolver.cs`
- Test: `tests/SqlSource.Tests/Generation/Repositories/ConnectionResolverTierTests.cs` (create, with the helpers of `ConnectionResolverTests` extended by `Held(...)` and `Global(...)` builders)

**Interfaces:**
- Consumes: `RepositoryShape.Candidates`, `HeldCandidates` and `GlobalCandidates`.
- Produces: `ConnectionResolver.Resolve(TargetType, RepositoryShape, IReadOnlyCollection<NeededDatabase>)`, same signature.  A held or global source's `ConnectionSource` has `AccessText`, `LeadingName`, `HolderAccessText`, `HolderName` and `MemberName` as Task 5 emits them:

| Source | `AccessText` | `LeadingName` | `HolderAccessText` | `HolderName` | `MemberName` |
|----|----|----|----|----|----|
| held through parameter `dbf` | `dbf.Open` | `dbf` | `dbf` | `dbf` | `dbf.Open` |
| held through field `_dbf` | `this._dbf.Open` | null | `this._dbf` | `_dbf` | `_dbf.Open` |
| held through static field `F` of `UserRepository` | `UserRepository.F.Open` | `UserRepository` | `UserRepository.F` | `F` | `F.Open` |
| global `Db.Reports` | `global::App.Db.Reports` | null | null | null | `Db.Reports` |

- [ ] **Step 1: Write the failing tests**

- `Resolve_OwnConventionBeatsHeldFactory`: tier 1 `connectionString` and a held `dbf.Open`.  The source is `connectionString`, with no diagnostic.
- `Resolve_OwnMarkBeatsGlobal`.
- `Resolve_HeldBeatsGlobal`.
- `Resolve_GlobalAlone_IsTheSource`, with `AccessText "global::App.Db.Reports"`.
- `Resolve_EachDatabaseFromItsOwnTier`: `main` from a tier 1 mark with `Database = "main"`, and `reports` from a global.
- `Resolve_NamedSharedMarkBeatsDefault`: globals `Db.Main` without `Database` and `Db.Reports` with `"reports"`, on `[Main, Reports]`.  `main` gets `Db.Main`, `reports` gets `Db.Reports`, and there are no diagnostics.
- `Resolve_SharedDefaultOnSeveralDatabases_ServesEach`: a held `Open()` without `Database` on `[Main, Reports]` resolves both, with no `SQLSRC030`.
- `Resolve_TwoSharedDefaults_ReportsSqlsrc029`, listing `'_main.Open', '_audit.Open'`.
- `Resolve_SharedPairWithinATier_IsAPair`: a held async and sync factory form a pair.
- `Resolve_PairAcrossTiers_IsNotFormed`: a tier 1 synchronous connection string plus a global async factory.  The synchronous source is the tier 1 member, `Synchronous` and `Asynchronous` both come from tier 1, and the global is not used.
- `Resolve_NamedAndDefaultInOneTier_DoNotPair`: a global async `Database = "main"` and a global sync default resolve `main` to the named one alone, with `Synchronous` null.
- `Resolve_SharedMarkForUnusedDatabase_IsIgnoredSilently`: no `SQLSRC031`.
- `Resolve_SharedDefaultOfOtherDriver_IsIgnored`: a global `Npgsql.NpgsqlDataSource` without `Database` for `Sales`.  It reports `SQLSRC028`, not `SQLSRC033`.
- `Resolve_SharedNamedOfOtherDriver_ReportsSqlsrc033`: the same with `Database = "sales"` reports `SQLSRC033`.  For a global it is at the repository's attribute location, and for a held member at `Holder.Location`.
- `Resolve_HeldUnsupported_ReportsSqlsrc032AtTheHolder`: the message's member text is `'IDatabaseFactory.Open' reached through 'dbf'`.
- `Resolve_FailedOwnTierBlocksLowerTiers`, a theory over three tier 1 failures: an unsupported mark without `Database`, a mark without `Database` on two databases, and `HeldParameters` non-empty with no candidates.  Each with a valid global present still fails the database, with today's diagnostic.
- `Resolve_HolderParameterNamedCancellationToken_ReportsSqlsrc036`.
- `Resolve_NoTierHasACandidate_ReportsSqlsrc028WithTheNewAdvice`: the advice of the convention case also says a marked member of a held type or a marked static member of the project gives the connection.
- `Resolve_SharedDatabaseNameComparedIgnoringCase`: a global `Database = "Reports"` serves `reports`.
- `Resolve_StaticTypeWithAGlobal_ReportsSqlsrc034`: a static repository stays `SQLSRC034`, with a global present.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/SqlSource.Tests -- --filter-class "*ConnectionResolver*"`
Expected: the new tests FAIL and `ConnectionResolverTests` passes.

- [ ] **Step 3: Implement the tiers in `Resolve`**

- Tier 1 runs today's `Assign` or `Conventions`, unchanged.
- A database counts as decided by tier 1 when tier 1 has a candidate for it or fails it.  It also counts as decided when the repository has no tier-1 candidate and `HeldParameters` is non-empty, which keeps `SQLSRC028` with today's advice.
- Undecided databases go through tier 2, then tier 3, with one shared-tier assignment:
  - A mark naming the database, by `DatabaseName.Comparer`, beats the tier's defaults.
  - A default of a driver type of another engine is skipped.
  - A mark for a database not needed is skipped silently.
  - A `Type == Unsupported` held candidate reports `SQLSRC032` at its holder and fails the databases it would serve.
- `SourceOf` builds the table above.
- `NoConnectionAdvice` gains the new text in its convention and marked branches.

- [ ] **Step 4: Run the unit project**

Run: `dotnet test --project tests/SqlSource.Tests`
Expected: PASS.

- [ ] **Step 5: Validate and commit**

```bash
git add src/SqlSource/Generation/Repositories tests/SqlSource.Tests
git commit -m "feat: resolve a repository's connection through three tiers"
```

### Task 5: Emit calls through a holder and to a global

**Files:**
- Modify: `src/SqlSource/Generation/Emitting/QueryMethods/CommandWriter.cs` (`AppendSourceOrThrow`)
- Modify: `src/SqlSource/Generation/Emitting/QueryMethods/QueryMethodLocals.cs` (`SourceOf`, `Seed`)
- Modify: `src/SqlSource/Generation/Methods/MethodPlanner.cs` only if `LeadingName` handling needs it
- Test: `tests/SqlSource.Tests/Generator/SharedConnectionMethodTests.cs` (create)
- Test: `tests/SqlSource.Tests/Generator/MethodCompileTests.cs` (add cases)
- Test: `tests/SqlSource.Tests/Generator/CachingRepositoryTests.cs` (add cases)

**Interfaces:**
- Consumes: `ConnectionSource.HolderAccessText`, `HolderName` and `MemberName` (Task 4).
- Produces: generated code only.

- [ ] **Step 1: Write the failing tests**

`SharedConnectionMethodTests`, a run under postgres:
- `Run_HeldAsyncFactory_CallsThroughTheHolder`: the methods file contains `(dbf ?? throw new global::System.InvalidOperationException("The connection source 'dbf' of 'App.Sample' is null.")).Open(cancellationToken)` and `"The connection source 'dbf.Open' of 'App.Sample' is null."`.
- `Run_HeldDataSourceProperty_OpensThroughTheHolder`: contains `(this._dbf ?? throw` and `.DataSource ?? throw`.
- `Run_GlobalConnectionString_UsesTheFullName`: contains `new global::Npgsql.NpgsqlConnection(global::App.Db.ConnectionString ?? throw` and `'Db.ConnectionString'`.
- `Run_GlobalInNestedType_UsesTheFullName` (Review Focus 5): `global::App.Outer.Db.ConnectionString`.

`MethodCompileTests`, generated code compiled with warnings as errors and nullable enabled:
- `Compile_HeldFactoryOfEachKind_Compiles`, a theory over the four kinds through a parameter holder and a field holder.
- `Compile_NullableHolder_CompilesWithoutWarnings` (Review Focus 2).
- `Compile_GlobalOfEachKind_Compiles`.
- `Compile_StructRepositoryStreamingThroughHolder_Compiles`.

Review Focus 3 is in `SharedConnectionMethodTests` too.  `Run_StructStreamingThroughHolder_ChecksTheHolderBeforeTheIterator` uses `partial struct Sample(IDatabaseFactory dbf)` with a `many` query whose collection type is `IAsyncEnumerable`.  The methods file assigns the holder's local from `dbf ?? throw new global::System.InvalidOperationException("The connection source 'dbf' of 'App.Sample' is null.")` before the local function that iterates, and calls `.Open(` on that local inside it.

`CachingRepositoryTests`:
- `Run_EditUnrelatedToAGlobalMark_LeavesTheMarksEqual`: the `GlobalConnectionMarks` and `GlobalConnectionChecks` steps report `Unchanged` or `Cached`.
- `Run_GlobalMarkAdded_ResolvesTheRepositoryAgain`: a repository with no source gets a method once a global is marked.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Generator.SharedConnectionMethodTests"`
Expected: FAIL.

- [ ] **Step 3: Implement the emission**

- `AppendSourceOrThrow` wraps the holder as `(<HolderAccessText> ?? throw new global::System.InvalidOperationException("The connection source '<HolderName>' of '<TypeName>' is null."))` and appends `.<member>` when `HolderAccessText` is set.  The rest is unchanged.
- In a struct's streaming method, `QueryMethodLocals.SourceOf` copies the holder into the local, not the member's value, so that the holder's null check runs first.
- A global's `LeadingName` is null, so `Seed` adds nothing for it.

- [ ] **Step 4: Run the unit project**

Run: `dotnet test --project tests/SqlSource.Tests`
Expected: PASS.

- [ ] **Step 5: Validate and commit**

```bash
git add src/SqlSource tests/SqlSource.Tests
git commit -m "feat: generate calls through a held factory and to a global"
```

### Task 6: End-to-end through each engine

**Files:**
- Modify: `tests/DatabaseTests/Postgres/EndToEnd/RepositoryQueries.cs`, and the test class that runs its repositories
- Modify: `tests/DatabaseTests/SqlServer/EndToEnd/RepositoryQueries.cs`, and the test class that runs its repositories
- Modify: `tests/DatabaseTests/SqlServer/EndToEnd/AGENTS.md` if it lists the repositories

**Interfaces:**
- Consumes: Tasks 1–5.

- [ ] **Step 1: Write the failing tests**

- Add `HeldFactoryRepository(IConnectionFactory factory)` over `Queries/Repository.sql`, with an `IConnectionFactory` whose marked async and sync methods form a pair.
- Add `GlobalRepository` with no members, served by a marked static in `EndToEndConnections`, with `Database` set to the logical database the file uses, so that no other repository of the project is affected.
- Each repository gets the project's existing per-repository assertions: a query returns rows, and the connection is disposed.
- In each project, add the test `HeldFactory_NullFactory_ThrowsNamingTheHolder`.

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test --project tests/DatabaseTests/Postgres` and `dotnet test --project tests/DatabaseTests/SqlServer` (Docker running)
Expected: FAIL to build before the repositories exist, then PASS once added; no sidecar changes, since they reuse `Queries/Repository.sql`.

- [ ] **Step 3: Run each project on its floor image**

```bash
SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/DatabaseTests/Postgres
```

```bash
SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/DatabaseTests/SqlServer
```

Expected: PASS.

- [ ] **Step 4: Validate and commit**

```bash
git add tests/DatabaseTests
git commit -m "test: run repositories through a held factory and a global on each engine"
```

### Task 7: Documentation and examples

**Files:**
- Modify: `site/reference/repository-connections.md`: the three tiers, held factories, globals, defaults, the engine filter and the null messages, as the spec's Behaviour section states them.
- Modify: `site/reference/attributes.md`: where `[SqlSourceConnection]` can go.
- Modify: `site/guides/repository-connection.md` and `site/guides/several-databases.md`: a shared factory and a global as the way to share a connection, and a default with named marks.
- Modify: `site/diagnostics.md`: `SQLSRC028`, `SQLSRC029`, `SQLSRC030` (tier 1 only), `SQLSRC031` (a repository's own marks only), `SQLSRC032` (held and global cases, reported once at a global), `SQLSRC033` (named shared marks) and `SQLSRC036` (holder parameters).
- Modify: `src/SqlSource/Generation/AttributeSource.cs`: the XML documentation of `SqlSourceConnectionAttribute`.
- Modify: `docs/architecture/generator.md`: the new stages in the pipeline list and in "Records between the steps", the count of steps that look at the compilation, the holder walk, and "The repository's connection" with the tiers.
- Modify: `src/SqlSource/Generation/Repositories/AGENTS.md`: the `ForAttributeWithMetadataName` rule now says the global stage reads only the mark's own symbol and decides everything else over the compilation, and the reader-filter rule covers `HeldCandidates`.  Keep the maintenance footer.
- Create: `examples/PostgresExamples/Repository/IConnectionFactory.cs`, `OrderRepository.cs`, `OrderRepository.sql`, and its sidecar via `tools/describe-end-to-end.sh`: a held factory.
- Create: `examples/SqlServerExamples/Repository/Connections.cs`, `OrderRepository.cs`, `OrderRepository.sql`, and its sidecar: a global connection string without `Database`.
- Modify: `examples/PostgresExamples/README.md` and `examples/SqlServerExamples/README.md`: the `Repository/` row.
- Test: the existing documentation, link, readme-size and diagnostics-section tests in `tests/SqlSource.Repository.Tests`.

- [ ] **Step 1: Write the pages and examples**

Describe effects, never internals: no "tier", "candidate" or stage names on `site/`, only what a user writes and what they get.  Each `sql` sample's info string names its dialect.

- [ ] **Step 2: Write the examples' sidecars**

Run: `zsh -lc tools/describe-end-to-end.sh` (Docker running) and read its diff.  Only the new `OrderRepository.sql.json` files appear.

- [ ] **Step 3: Run the repository tests and the build**

Run: `dotnet test --project tests/SqlSource.Repository.Tests` and `dotnet build SqlSource.slnx`
Expected: PASS, and the examples compile.

- [ ] **Step 4: Validate and commit**

```bash
git add site docs src/SqlSource examples
git commit -m "docs: document held factories and global connection sources"
```

### Task 8: Branch checks

- [ ] **Step 1: Check the version.** Run `git fetch --tags origin` and `git tag --list 'v*' --sort=-v:refname | head -n 1`.  With no tag, nothing to compare; otherwise follow root `AGENTS.md` (ask the user if `VersionPrefix` is not greater).
- [ ] **Step 2: Run the full validation.** Run `zsh -lc './pre-commit-validation.sh --full'`.  Expected: every check passes.
- [ ] **Step 3: Run `branch-review:review`** and address its findings until it comes back clean, then offer the pull request.
