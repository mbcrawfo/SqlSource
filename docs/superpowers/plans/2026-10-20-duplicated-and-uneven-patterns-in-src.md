# Duplicated and Uneven Patterns in `src/` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve TD-0063: give each rule that the tool and the generator, or several files, write out by hand one home, with no change a user can see.

**Architecture:** A refactor in ten items.  Rules both sides need move into the generator's project (`InvalidValues`, `QuerySettings.ResolveDatabase`, `DatabaseName`, `SqlPath`'s extensions); hand-kept lists get a `switch` that throws and a theory; `SqlSourceGenerator.Initialize` is split by stage; small duplicates get one helper each; two log events get their missing parts.

**Tech Stack:** C# (`netstandard2.0` generator on Roslyn 4.8.0, `net8.0` tool), xUnit v3, Shouldly.

**Spec:** [`docs/tech-debt/TD-0063-duplicated-and-uneven-patterns-in-src.md`](../../tech-debt/TD-0063-duplicated-and-uneven-patterns-in-src.md), Problem and Proposed fix.

## Global Constraints

- No behaviour a user can see changes: messages, the order of errors, exit codes, generated code, sidecars and `--verbose` lines stay byte for byte.  The one deliberate change is item 9's added `query` member of four log events, which the TD asks for and the log's rule allows ("a member is added and never renamed").
- No name in `src/SqlSource/Generation/TrackingNames.cs` changes, and no named step moves to another input: `tests/SqlSource.Tests/Generator/CachingTests.cs` and the tool's parity tests must pass unchanged.
- The generator's `RegisterSourceOutput` calls keep today's order (the attribute, case collisions, parse errors and dialects, invalid settings, the Npgsql check, sidecar checks, file types, file models, model diagnostics, model sources, type outputs), so the diagnostics and sources of a run keep their order.
- Database names stay compared ignoring case (`StringComparer.OrdinalIgnoreCase`) everywhere they are today.
- Generator code: `netstandard2.0`, Roslyn 4.8.0 API only, no collection expression targeting `ImmutableArray<T>`, nothing allocated per character or line (`src/SqlSource/AGENTS.md`).
- `Settings/` uses nothing of `Parsing/`, `Generation/` or `Diagnostics/`; `Snapshot/` nothing of `Parsing/`, `Generation/`, `Settings/` or `Diagnostics/`.  A rule shared across them goes in the root namespace, as `SqlIdentifier` does.
- Every commit passes `./pre-commit-validation.sh` (root `AGENTS.md`); a task that changes `src/SqlSource.Tool/Describing/Postgres/` also runs `tests/SqlSource.Tests.Postgres` with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24` before the push.
- An `AGENTS.md` or `docs/architecture/` page that names a moved, renamed or removed member is updated in the same commit.
- Another session may be editing `src/SqlSource/Settings/TypeKeys.cs` (line-break-keys) and `tests/` shared helpers (test-helpers): do not touch `TypeKeys.cs`, and keep changes to `tests/SqlSource.Tool.Testing/` and `tests/SqlSource.Testing/` to what a signature change forces.

## Where the merged code differs from the TD

- Item 4: `Snapshot/` may not use `Settings/`, and `SidecarEntry` and `SidecarComparer` compare database names, so `DatabaseName` goes in the root namespace (`src/SqlSource/DatabaseName.cs`), not beside `SettingValue.IsDatabaseName`.  `IsDatabaseName` stays where it is.  The ordinal compare of project paths in `DescribeCommand` disappears with item 5, so the project-path comparer serves `RunProjects` alone.
- Item 3: `Settings/` cannot name `SqlDialectName`, so `QuerySettings.ResolveDatabase` takes the dialect's name as its last argument.  Like `ResolveTypeOverrides` it has no attribute parameter: the attribute has no level for the database.
- Item 8 (binary search): `SqlPath.IndexOf` and `MappedTypeResolver.Find` are one exact search and share `SortedSearch.IndexOf`.  `TypeOverrideMap`'s `LowerBound` is a lower bound over a `ref struct` key (`KeyParts`), which no generic helper can hold on `netstandard2.0` (no `allows ref struct`) without splitting the key again at every step.  It stays, and TD-0063 is narrowed to it (Task 11).
- Item 8 (line terminators): `SqlTextBuilder` names `'\r'` and `'\n'` only to measure a `\r\n` pair, which is not a test for a line break; it keeps them.
- Item 9: `RunEvents.Sidecar`'s values are `written`, `unchanged`, `deleted`, `held-back`, `failed`, `same` and `differs`, and `FileAction` has no failed, same or differs.  So `Sidecar` takes a `FileAction` and whether the change failed, and `--check`'s two values get a sibling, `RunEvents.SidecarChecked`.

## Review Focus

1. A `--database` given twice in different case (`--database Billing --database billing`): one filter, the first spelling, in the plan, the summary and `SQLSRC219`'s scope, as today.  Covered by the existing filter tests; Task 4 keeps them green.
2. A file claimed by two projects: each project's `unclaimed` count in the log is unchanged.  Task 4 adds a test.
3. A project whose dialect property and several files' metadata dialects are invalid, listed in an order that is not sorted: SQLSRC011 and SQLSRC014 come in the generator's order from both sides.  Task 2 adds the parity test.
4. A marker of every kind in a whole-file scope: none throws, none is silently ignored.  Task 6's theory.
5. A sidecar whose name ends in `.SQL.JSON`: still found by the tool's leftover sweep (`SqlPath.IsSidecarFile` ignores case, as `IsSidecarName` did).  Task 5 adds a test.

---

### Task 1: One comparer for database names and one for project paths (item 4)

**Files:**
- Create: `src/SqlSource/DatabaseName.cs`
- Modify: `src/SqlSource.Tool/Planning/RunPlanner.cs`, `src/SqlSource.Tool/Planning/RunFilters.cs`, `src/SqlSource.Tool/DescribeCommand.cs`, `src/SqlSource.Tool/Describing/Connections.cs`, `src/SqlSource.Tool/Describing/RunDecisions.cs`, `src/SqlSource.Tool/Describing/SidecarCheck.cs`, `src/SqlSource.Tool/Describing/DatabaseRuns.cs`, `src/SqlSource.Tool/Describing/RunSummary.cs`, `src/SqlSource/Snapshot/SidecarEntry.cs`, `src/SqlSource/Snapshot/SidecarComparer.cs`, `src/SqlSource.Tool/Projects/RunProjects.cs`
- Test: `tests/SqlSource.Tests/DatabaseNameTests.cs`

**Interfaces:**
- Produces: `internal static class DatabaseName` (namespace `SqlSource`) with `public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;`, documented as the one comparer of database names (two names that differ only in case read one connection variable).  `RunProjects.PathComparer` (`public static StringComparer PathComparer => StringComparer.OrdinalIgnoreCase;`), documented as the one comparer of project paths.

- [ ] **Step 1: Write the test** `DatabaseNameTests.Comparer_NamesThatDifferInCase_AreEqual`: `DatabaseName.Comparer.Equals("Billing", "billing").ShouldBeTrue()`, `DatabaseName.Comparer.Equals("billing", "billing2").ShouldBeFalse()`, and `DatabaseName.Comparer.GetHashCode("BILLING").ShouldBe(DatabaseName.Comparer.GetHashCode("billing"))`.
- [ ] **Step 2: Run it and see it fail to compile.**  `dotnet test --project tests/SqlSource.Tests --filter-class "*DatabaseNameTests"` (or the repository's usual filter form).
- [ ] **Step 3: Add `DatabaseName`, and replace every database-name `StringComparer.OrdinalIgnoreCase`/`StringComparison.OrdinalIgnoreCase` at the listed sites** with `DatabaseName.Comparer` (or `DatabaseName.Comparer.Equals(a, b)`).  `SidecarComparer.Text` takes a `StringComparison`; give it a `StringComparer` parameter instead (default `StringComparer.Ordinal`) and pass `DatabaseName.Comparer` for the database.  `DescribeCommand.CheckUsage`'s set of `--connection` names is a set of database names.  Replace the four project-path comparers of `RunProjects` with `RunProjects.PathComparer`.  Grep `OrdinalIgnoreCase` under `src/SqlSource.Tool/` and `src/SqlSource/Snapshot/` afterwards: none of the remaining ones may compare a database name.
- [ ] **Step 4: Run the tool's and generator's tests; they pass.**
- [ ] **Step 5: Update `src/SqlSource.Tool/AGENTS.md`'s rule "A database belongs to a query that needs an entry" ("Names are compared ignoring case") to name `DatabaseName.Comparer`, and `docs/architecture/tool.md` / `generator.md` where they say how database names are compared.  Run `./pre-commit-validation.sh`, then commit.**

### Task 2: One order for the invalid values, and a parity test (item 1)

**Files:**
- Create: `src/SqlSource/Generation/MSBuild/InvalidValues.cs`
- Modify: `src/SqlSource/SqlSourceGenerator.cs` (`FindInvalidDialects`, `FindInvalidSettings`), `src/SqlSource.Tool/Planning/RunPlanner.cs` (delete the private `InvalidValues`)
- Test: `tests/SqlSource.Tests/Generation/MSBuild/InvalidValuesTests.cs`, `tests/SqlSource.Tool.Tests/Planning/GeneratorParityTests.cs`

**Interfaces:**
- Produces, in `SqlSource.Generation.MSBuild`:
  ```csharp
  internal sealed class InvalidValues
  {
      public void AddDialect(string? value);                    // null is ignored
      public void AddSettings(IEnumerable<InvalidSetting> settings);
      public IReadOnlyCollection<string> Dialects { get; }      // each once, StringComparer.Ordinal order
      public IEnumerable<InvalidSetting> Settings { get; }      // each once, by Name then Value, both ordinal
  }
  ```
  The generator's two finders and the tool's planner each fill one and read it; the tool keeps its own filter (only `MSBuildSettings.OutputName` and `MSBuildSettings.DatabaseName`) before `AddSettings`.

- [ ] **Step 1: Write `InvalidValuesTests`**: `Dialects_AddedOutOfOrderAndTwice_AreEachOnceInOrdinalOrder` (add `"zeta"`, `null`, `"Beta"`, `"beta"`, `"zeta"` → `["Beta", "beta", "zeta"]`); `Settings_AddedOutOfOrderAndTwice_AreEachOnceByNameThenValue` (add `(SqlSourceOutput, "x")`, `(SqlSourceDialect, "b")`, `(SqlSourceOutput, "a")`, `(SqlSourceOutput, "x")` → `[(SqlSourceDialect, b), (SqlSourceOutput, a), (SqlSourceOutput, x)]`).
- [ ] **Step 2: Write the parity test** `GeneratorParityTests.Errors_OfInvalidValues_AreInTheGeneratorsOrder`: one project whose `SqlSourceDialect` property is `"zeta"` and `SqlSourceOutput` is `"nothing"`, with three claimed files listed in this order whose `SqlSourceDialect` metadata is `"beta"`, `"alpha"`, `"beta"` and one whose `SqlSourceOutput` metadata is `"bad"`.  Run the generator over it (the harness's options provider gives the property and each file's metadata) and `RunPlanner.Plan` over its manifest.  Compare, as the existing error test does, the ordered sequence of `(Id, Message)` of SQLSRC011 and of SQLSRC014 whose setting is `SqlSourceOutput` from each side: the two sequences are equal, `SQLSRC011` is `[alpha, beta, zeta]`, and each side has at least two SQLSRC014.  Do not re-sort either side before comparing: the order is what is pinned.
- [ ] **Step 3: Run both; `InvalidValuesTests` fails to compile, the parity test passes already** (it pins today's agreement).
- [ ] **Step 4: Add `InvalidValues` and use it in both places.**  `FindInvalidDialects` returns `invalid.Dialects`, `FindInvalidSettings` returns `invalid.Settings`; `RunPlanner.ReadProject` fills one per project and a private `RunPlanner.AddInvalid(errors, projectPath, invalid)` reports SQLSRC011 then SQLSRC014 from it.
- [ ] **Step 5: Run the generator's and tool's tests; all pass.**
- [ ] **Step 6: Update the remarks of `RunPlanner`, `src/SqlSource.Tool/AGENTS.md` ("Of the settings that are not valid…" may name `InvalidValues`) and `src/SqlSource/AGENTS.md`'s `SQLSRC011`/`SQLSRC014` rule to say both sides take their order from `InvalidValues`, and the `docs/architecture/generator.md` section on the tool running the same code (its list of shared members).  Validate, commit.**

### Task 3: One resolver of the database (item 3)

**Files:**
- Modify: `src/SqlSource/Settings/QuerySettings.cs`, `src/SqlSource.Tool/Planning/RunPlanner.cs` (`PlanQuery`)
- Test: `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs` (create the file if it does not exist)

**Interfaces:**
- Produces: `public static string QuerySettings.ResolveDatabase(SettingsLevel markers, SettingsLevel metadata, SettingsLevel property, string dialectName)`: the markers', then the metadata's, then the property's `Database`, else `dialectName`.  Its doc says the attribute has no level for it.

- [ ] **Step 1: Write `ResolveDatabase_EachLevel_WinsOverTheLevelsBelowIt`** as a theory: markers `a`, metadata `b`, property `c` → `a`; markers none → `b`; markers and metadata none → `c`; none → the dialect name `postgres`.
- [ ] **Step 2: See it fail; add the method; see it pass.**
- [ ] **Step 3: `RunPlanner.PlanQuery` calls it** with `query.Markers`, `owner.Metadata.Settings?.Level ?? SettingsLevel.None`, `owner.Property`, `SqlDialectName.Canonical(dialect)`.
- [ ] **Step 4: Update `src/SqlSource/AGENTS.md`'s `Settings/` rule ("`QuerySettings.Resolve` is the one rule that puts the levels together") to name `ResolveDatabase` with `ResolveTypeOverrides`, and `docs/architecture/generator.md` / `tool.md` where they say how a query's database is found.  Validate, commit.**

### Task 4: The filters on the plan, and the unclaimed count from the planner (item 5)

**Files:**
- Modify: `src/SqlSource.Tool/Planning/RunFilters.cs`, `src/SqlSource.Tool/Planning/RunPlan.cs`, `src/SqlSource.Tool/Planning/RunPlanResult.cs`, `src/SqlSource.Tool/Planning/RunPlanner.cs`, `src/SqlSource.Tool/DescribeCommand.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/FileOutcomesTests.cs`, `tests/SqlSource.Tool.Tests/Describing/RunDecisionsTests.cs` (their `new RunFilters([], ["billing"])`), `tests/SqlSource.Tool.Tests/Planning/RunPlannerTests.cs`

**Interfaces:**
- Consumes: `DatabaseName.Comparer` (Task 1).
- Produces:
  - `RunFilters(ImmutableArray<FileFilter> Files, ImmutableArray<UsageValue> Databases)`; `Create(IEnumerable<UsageValue> sqlPaths, IEnumerable<UsageValue> databases, string workingDirectory)` keeps the first value of each name by `DatabaseName.Comparer`, in the order given; `public bool NamesDatabase(string name)`.
  - `RunPlan(EquatableArray<PlannedFile> Files, EquatableArray<PlannedDatabase> Databases, RunFilters Filters)`: the filters the plan was made with.
  - `RunPlanResult(RunPlan Plan, EquatableArray<ToolDiagnostic> Errors, EquatableArray<int> Unclaimed)`: for each manifest, in the order given, the number of its listed `.sql` files that no type of it claims.
  - `DescribeOptions` keeps its shape.  `DescribeCommand.OptionsOf(ParseResult parsed, Usage usage, RunPlan plan)` sets `HasFilter` to `--project` given `|| !plan.Filters.IsEmpty`, and `Databases` to `plan.Filters.Databases`.

- [ ] **Step 1: Write `RunPlannerTests.Plan_FileThatTwoProjectsClaim_CountsUnclaimedForEach`**: project A lists `X.sql` and `Only.sql` and claims both with a root type; project B, in a sibling folder, lists `X.sql` (A's file, by an `AdditionalFiles`/`Compile`-style include of the path) and `Lone.sql` with a type that claims only `X.sql` (an attribute `Path` naming it).  `result.Unclaimed` is `[0, 1]`.  Also add `Plan_Filters_AreKeptOnThePlan`: the plan's `Filters` is the instance given.
- [ ] **Step 2: See them fail to compile.**
- [ ] **Step 3: Change the records and the planner.**  `ReadProject` returns the count beside its claimed files (`listed.Paths.Count - claimed.Count`); `Plan` gathers them in manifest order.  `RunPlanner.IsNamed` uses `filters.NamesDatabase`.  `DescribeCommand.PlanAsync` builds the filters from `ValuesOf(usage, DatabaseOption)` (the values, not their texts), writes each `RunEvents.Unclaimed` from `result.Unclaimed[i]` and no longer reads `ListedFiles`; `Bind` passes the plan to `OptionsOf`, which no longer de-duplicates the databases itself.  Update the two tests' `new RunFilters([], ["billing"])` to `new([], [new UsageValue(null, "billing", 1)])`.
- [ ] **Step 4: Run the tool's tests (filters, check filters, verbose, log, describe); all pass.**
- [ ] **Step 5: Update `docs/architecture/tool.md`'s plan section (the filters live on the plan; the planner counts the unclaimed files) and any `src/SqlSource.Tool/AGENTS.md` sentence that says where `HasFilter` or the `--database` list is built.  Validate, commit.**

### Task 5: One spelling of the sidecar's name (item 2)

**Files:**
- Modify: `src/SqlSource/Snapshot/SidecarFormat.cs`, `src/SqlSource/Generation/Paths/SqlPath.cs`, `src/SqlSource.Tool/Describing/SidecarStore.cs`, `src/SqlSource.Tool/Logging/RunLogFile.cs`
- Test: `tests/SqlSource.Tool.Tests/Describing/SidecarStoreTests.cs` (or the file that tests `RemoveLeftovers`), `tests/SqlSource.Tests/Snapshot/` (a fact on `SidecarFormat.PathFor`)

**Interfaces:**
- Produces: `public const string SidecarFormat.Extension = ".json";` (what a sidecar's path has after its `.sql` file's), `PathFor(sqlPath) => sqlPath + Extension`.  `SqlPath.SqlExtension` (`".sql"`) and `SqlPath.SidecarExtension` (`SqlExtension + SidecarFormat.Extension`) become `public const`; `SqlPath`'s private `JsonExtension` goes, its uses read `SidecarFormat.Extension`.  `SidecarStore` drops `SidecarSuffix` and `IsSidecarName` and calls `SqlPath.IsSidecarFile`.  `RunLogFile.FilesOfTheRun` is `[SqlPath.SqlExtension, SqlPath.SidecarExtension, ".csproj", ".sln", ".slnx"]`.

- [ ] **Step 1: Write `SidecarFormatTests.PathFor_SqlPath_IsTheSidecarPathThatSqlPathRecognises`**: `SqlPath.IsSidecarFile(SidecarFormat.PathFor("a/Q.sql")).ShouldBeTrue()` and `SqlPath.SqlPathOfSidecar(SidecarFormat.PathFor("a/Q.sql")).ShouldBe("a/Q.sql")`; and a `SidecarStore` test that a leftover temporary file of a sidecar spelled `Q.SQL.JSON` is removed as one of `Q.sql.json` is (Review Focus 5).
- [ ] **Step 2: Run them (they pass today: they pin the behaviour across the change).**
- [ ] **Step 3: Make the change.**  Keep the comment on the remaining `SidecarFormat` constant that `SqlPath` builds on it.
- [ ] **Step 4: Run the generator's and tool's tests; all pass.  Update `docs/architecture/` where it names `SidecarSuffix` or `IsSidecarName`.  Validate, commit.**

### Task 6: The markers' lists held together (item 6)

**Files:**
- Modify: `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs`
- Test: `tests/SqlSource.Tests/Parsing/Markers/SqlMarkerScopeTests.cs`

**Interfaces:**
- Produces: `SqlMarkerScope.Read` is one `switch (marker.Kind)` with a `case` for every kind `IsAllowed` can let through (the bodies of `ReadModelSetting` move into it, and `ReadModelSetting` goes) and a `default` that throws `InvalidOperationException($"No reading of the marker {marker.Kind}.")`.  `IsAllowedInQuery` and `IsAllowedInPreamble` become `internal static` for the test.  The kinds `SqlFileParser` reads itself are `Name`, `Summary` and `Dialect`.

- [ ] **Step 1: Write the theory** `Marker_OfEveryKind_HasAKeywordAndAScope(SqlMarkerKind kind)`, with `MemberData` over `Enum.GetValues<SqlMarkerKind>()`:
  - `SqlMarkerReader.WordOf(kind)` is not empty, and `SqlMarkerReader.Read` of `"-- " + word + ": x"` (its one lexeme) gives a marker of `kind`;
  - `kind is Name or Summary or Dialect` exactly when neither `IsAllowedInQuery(kind)` nor `IsAllowedInPreamble(kind)`;
  - for an allowed kind, a `SqlScopeKind.WholeFile` scope over that text reads the marker without throwing (an invalid value is an error in the list, never an exception).
- [ ] **Step 2: Run it; it passes for every kind today** (it pins the lists).  Temporarily drop one `case` after Step 3 to see the theory fail for that kind, then restore it.
- [ ] **Step 3: Rewrite `Read` as the `switch`.**  Order of the cases is free; each body is today's branch unchanged.
- [ ] **Step 4: Run the parsing tests; all pass.  Update `src/SqlSource/AGENTS.md`'s marker rule ("A new marker is a `SqlMarkerKind`, a keyword…, and a branch in `SqlMarkerScope.Read`") to say "a `case`", and that the theory fails until all four are there; and `docs/architecture/generator.md`'s markers section if it describes the chain.  Validate, commit.**

### Task 7: `Initialize` by stage (item 7)

**Files:**
- Create: `src/SqlSource/Generation/IncrementalValuesProviderExtensions.cs`
- Modify: `src/SqlSource/SqlSourceGenerator.cs`
- Test: the existing `tests/SqlSource.Tests/Generator/` suite (CachingTests above all)

**Interfaces:**
- Produces: `internal static class IncrementalValuesProviderExtensions { public static IncrementalValuesProvider<T> WhereNotNull<T>(this IncrementalValuesProvider<T?> source) where T : class; }` (a `Where(x is not null)` and a `Select(x!)`, both static lambdas), used at the five sites: target types, listed paths, parsed files, sidecar inputs, file settings.  A `WithTrackingName` that follows one of them today follows the extension's result.
- `private static void ReportAll(SourceProductionContext output, EquatableArray<DiagnosticInfo> diagnostics)`, used by the four outputs that report an array (sidecar checks, file types, file models, the models' set) and by the type output.
- `Initialize` becomes calls to one private static method per stage, in this order, which keeps the order of every `RegisterSourceOutput`:
  1. `RegisterAttribute(context)`
  2. `ClaimFiles(context)`: target types, `.sql` files, listed paths, sorted paths, case collisions, the framework and language-version checks, type files, claimed and comment paths; registers the case-collision output.
  3. `ReadSettings(context)`: project dialect, project settings and level, file metadata, file settings; registers nothing.
  4. `ParseFiles(context, …)`: parse inputs and parsed files; registers the parse-error and dialect output.
  5. `ReportInvalidSettings(context, …)`: registers SQLSRC014's output.
  6. `SelectTypeQueries(…)`: ambiguous hint names and type queries.
  7. `CheckSidecars(context, …)`: sidecars, file needs, sidecar checks.
  8. `TypeFiles(context, …)`: the Npgsql probe, mapped type names and types, typed files and file types.
  9. `CollectModels(context, …)`: file models through the resolved models.
  10. `ReportDriver(context, …)`: whether postgres entries are used; registers SQLSRC247/248's output.
  11. `ReportProblems(context, …)`: the four `ReportAll` outputs, in today's order.
  12. `RegisterModelSources(context, models)`: the `ModelOutput` step and its `AddSource`, alone in its method (TD-0060 will rewire it).
  13. `RegisterTypeSources(context, typeQueries, projectLevel)`: the `TypeOutput` step and its output.
  A stage that hands several providers on returns a `private readonly record struct` of them, named for what it holds.  Every comment of today's body moves with the step it describes.

- [ ] **Step 1: Run `tests/SqlSource.Tests` and note it is green.**
- [ ] **Step 2: Add the extension and `ReportAll`, and split `Initialize`.**  No step is renamed, and none takes another input.
- [ ] **Step 3: Run `tests/SqlSource.Tests` (both Roslyn versions, as the solution's test run does) and `tests/SqlSource.Tool.Tests/Planning/GeneratorParityTests.cs`; all pass unchanged.**
- [ ] **Step 4: Update `src/SqlSource/AGENTS.md` ("`SqlSourceGenerator` wires the steps, and its only logic is the helpers at its end") and `docs/architecture/generator.md`'s pipeline section to name the stage methods where they describe `Initialize`.  Validate, commit.**

### Task 8: Small duplicates of the generator (item 8)

**Files:**
- Create: `src/SqlSource/Generation/Models/ModelKinds.cs`, `src/SqlSource/Generation/Types/MessageSubject.cs`, `src/SqlSource/SortedSearch.cs`
- Modify: `src/SqlSource/Generation/Models/ModelProperty.cs`, `ModelCollector.cs`, `ModelReader.cs`, `TypeModels.cs`, `src/SqlSource/Generation/Emitting/ModelEmitter.cs`, `HintName.cs`, `src/SqlSource/Generation/Types/QueryTyper.cs`, `MappedTypeResolver.cs`, `src/SqlSource/Generation/Paths/SqlPath.cs`, `src/SqlSource/Parsing/SqlText.cs`, `SqlLexer.cs`, `Markers/SqlMarkerReader.cs`, `Quoting/QuoteOperatorReader.cs`, `src/SqlSource/Settings/TypeOverrideMap.cs` (a comment only)
- Test: `tests/SqlSource.Tests/SortedSearchTests.cs`, `tests/SqlSource.Tests/Parsing/SqlTextTests.cs`, `tests/SqlSource.Tests/Generation/Models/ModelKindsTests.cs`

**Interfaces:**
- `ModelProperty.IsSameShape` is deleted (no caller; `FindDifference` needs which member differs), and the `IsArray` doc stops naming it ("`ModelCollector.FindDifference` does not compare it").
- `internal static class ModelKinds { public static string Keyword(ModelKind kind); }`: `record`, `sealed record`, `class`, `sealed class` (anything else is `sealed class`, as today).  `ModelEmitter` appends `Keyword(kind) + " "`; `ModelCollector.KindOf` goes.
- `internal static class MessageSubject { public const string Parameter = "parameter"; public const string Column = "column"; public static string Of(string owner, string name); }` → `column 'id'`.  `QueryTyper` and `ModelReader` lose their constants and `Subject`.
- `HintName.NameOf(TargetType type)`: today's `Create` without `.g.cs`; `Create(type) => NameOf(type) + Extension`.  `TypeModels.ClaimingTypeOf` takes its key from `HintName.NameOf` and builds only the display name itself.
- `internal static class SortedSearch { public static int IndexOf<T>(EquatableArray<T> sorted, string key, Func<T, string> keyOf, StringComparer comparer) where T : IEquatable<T>; }`: the index of the item whose key equals `key` by `comparer`, or `-1`.  `SqlPath.IndexOf` calls it with `Comparer`; `MappedTypeResolver.Find` with `static type => type.Name` and `StringComparer.Ordinal`.  `TypeOverrideMap.LowerBound` gets one sentence on why it is a search of its own (a lower bound over a `ref struct` key).
- `public static bool SqlText.IsLineTerminator(char character) => character is '\r' or '\n';` beside `LineTerminators`, marked `[MethodImpl(MethodImplOptions.AggressiveInlining)]`; `SqlLexer`, `SqlMarkerReader.StartsLine` and `QuoteOperatorReader` call it.

- [ ] **Step 1: Write the tests**: `SortedSearchTests.IndexOf_KeyInTheList_IsItsIndex` (keys `["a", "B", "c"]` with `OrdinalIgnoreCase`: `"b"` → 1, `"d"` → -1, empty list → -1); `SqlTextTests.IsLineTerminator_Character_IsTrueForCarriageReturnAndLineFeedOnly` (theory: `'\r'`, `'\n'` true; `' '`, `'\t'`, `' '`, `'\f'` false); `ModelKindsTests.Keyword_EachKind_IsItsCSharpKeyword` (four rows).
- [ ] **Step 2: See them fail to compile.**
- [ ] **Step 3: Make the changes listed above.**  `SqlText.IsLineTerminator` stays allocation-free; nothing else moves.
- [ ] **Step 4: Run `tests/SqlSource.Tests`, including the allocation budgets; all pass.**
- [ ] **Step 5: Update `src/SqlSource/AGENTS.md`: the `Parsing/` rule that "code that tests one character for a line break names both terminators of SQL" now says it calls `SqlText.IsLineTerminator`; the paths rule ("find one with `SqlPath.IndexOf`…") is unchanged in substance.  Update `docs/architecture/generator.md` where it names `ClaimingTypeOf`'s tie to `HintName`, `IsSameShape` or the subject helpers.  Validate, commit.**

### Task 9: Even events of the log (item 9)

**Files:**
- Modify: `src/SqlSource.Tool/Logging/RunEvents.cs`, `src/SqlSource.Tool/Describing/SidecarStore.cs`, `src/SqlSource.Tool/Describing/DescribeRun.cs`, `src/SqlSource.Tool/Describing/Postgres/PostgresEvents.cs`, `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`, `src/SqlSource.Tool/Describing/Postgres/PostgresSession.Calls.cs`
- Test: `tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs`, and wherever a test holds the members of `postgres.types`, `postgres.spellings`, `postgres.tables` or `postgres.deallocate`

**Interfaces:**
- `RunEvents.Sidecar(RunTrace trace, string path, FileAction action, bool failed)`: `failed` gives `failed`; otherwise `Write` → `written`, `Delete` → `deleted`, `HeldBack` → `held-back`, `Unchanged` → `unchanged`, `Unreadable` → `failed`; `None` and anything else throw `InvalidOperationException`.  The `--verbose` words are today's.
- `RunEvents.SidecarChecked(RunTrace trace, string path, bool differs)`: `differs` or `same`, with today's words.  `DescribeRun.CheckedAs` returns `bool?` (null: no event).
- `PostgresEvents.Types`, `Spellings`, `Tables` and `Deallocate` take `string query` after `log` and write `new("query", query)` as their first member, as `Columns` does.  `PostgresSession.TypesAsync`, `SpellingsAsync`, `TablesAsync` and `DeallocateAsync` take the query's name first, as `ColumnsAsync` does, and `RowsOfAsync`, `TablesOfAsync` and `SpellingsOfAsync` pass `request.Name` down.

- [ ] **Step 1: Write the tests**: `RunLogTests.Log_DescribeOfAQuery_NamesTheQueryInEveryPostgresEventOfIt`, from a scripted describe (`tests/SqlSource.Tool.Tests/PostgresScript.cs`) that asks `tables`, `types` and `spellings`: every `postgres.*` event other than `postgres.walk.node` and `postgres.walk.column` has `query` equal to the query's name; and `RunEvents_Sidecar_EachActionIsItsWord` (theory over `(FileAction, failed)` → the log's `action` and the `--verbose` line: `(Write,false)→written`, `(Write,true)→failed`, `(Delete,false)→deleted`, `(Unreadable,true)→failed`, `(HeldBack,false)→held-back "left as it was"`, `(Unchanged,false)→unchanged`).
- [ ] **Step 2: See them fail.**
- [ ] **Step 3: Make the change.**  Update tests that compare the four events whole, and the remarks of `PostgresEvents` if they list members.
- [ ] **Step 4: Run the tool's tests, and `tests/SqlSource.Tests.Postgres` on the default image and with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`; all pass.  The fixtures' captures do not change (the log is not in them).**
- [ ] **Step 5: Update `docs/architecture/tool.md`'s logging section and `postgres-describer.md` where they list an event's members or `RunEvents.Sidecar`'s string.  Validate, commit.**

### Task 10: Repeated blocks of the tool (item 10)

**Files:**
- Modify: `src/SqlSource.Tool/Describing/DatabaseRuns.cs`, `src/SqlSource.Tool/Describing/Postgres/PostgresSession.cs`
- Test: existing `tests/SqlSource.Tool.Tests/Describing/` and `Commands/` suites

**Interfaces:**
- `DatabaseRuns.ReportFailure(Guard guard, Func<DescribeFailure?> failureOf, PlannedQuery planned, PlannedDatabase database, ServerInfo? server, Reporter reporter)` (use the actual type of `server` in `DescribeErrors.Failure`): reads the failure under the guard (a null is `Guard.Nothing`'s fault), redacts it, reports `DescribeErrors.Failure` redacted, and returns the redacted failure.  The open-failure and describe-failure blocks call it and keep writing their own events (`NotOpened` + `Fail`, and `Query` with `error`).
- `PostgresSession.Answered<TRow, TValue>(PostgresServerError? error, IReadOnlyList<TRow>? rows, string call, string member, Func<IReadOnlyList<TRow>, TValue> read)` → `Outcome<TValue, PostgresServerError>`: `Failed(error)` when there is one, otherwise `Of(read(rows ?? throw new InvalidOperationException($"The answer to '{call}' holds no {member} and no error.")))`.  The three `…OfAsync` methods call it; each exception message stays word for word.

- [ ] **Step 1: Run the tool's tests and note they are green; these blocks are covered by `Describing/DatabaseRunsTests`, `Commands/SecretTests` and the `PostgresScript` session tests.**
- [ ] **Step 2: Make both changes.**
- [ ] **Step 3: Run the tool's tests and `tests/SqlSource.Tests.Postgres` on both images; all pass.**
- [ ] **Step 4: Update `docs/architecture/tool.md`'s describing section if it walks through the two blocks.  Validate, commit.**

### Task 11: Close the item

**Files:**
- Modify: `docs/tech-debt/TD-0063-duplicated-and-uneven-patterns-in-src.md`, `docs/tech-debt/README.md`, `src/SqlSource/AGENTS.md`, `src/SqlSource.Tool/AGENTS.md`, `docs/architecture/*.md`

- [ ] **Step 1: Narrow TD-0063** to what is left, `TypeOverrideMap`'s lower bound beside `SortedSearch`: Problem (the one search left of item 8, with the `ref struct` reason), Why it exists, Impact (Low), Proposed fix (when the generator may use `allows ref struct`, or if `KeyParts` holds offsets instead of spans, search through `SortedSearch`), Trigger.  Rename the file to `TD-0063-lower-bound-of-the-type-override-map-is-a-search-of-its-own.md` and rewrite its README row's title.
- [ ] **Step 2: `grep -rn "TD-0063" .` outside `bin/` and `obj/`: only the narrowed item, its row and its comment in `TypeOverrideMap` remain.**
- [ ] **Step 3: Read `src/SqlSource/AGENTS.md`, `src/SqlSource.Tool/AGENTS.md`, `docs/architecture/generator.md` and `tool.md` once more for any name this branch moved, renamed or removed (`InvalidValues`, `ReadModelSetting`, `IsSidecarName`, `SidecarSuffix`, `IsSameShape`, `KindOf`, `Subject`, `ClaimingTypeOf`, `DescribeOptions`, `RunFilters`, `ResolveDatabase`, `DatabaseName`, `Initialize`), and fix each.**
- [ ] **Step 4: Validate, commit.**
