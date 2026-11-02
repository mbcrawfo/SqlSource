# Closed engine set Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every branch on `DatabaseEngine` under `src/SqlSource` names every engine in a switch expression, so adding an engine fails the build at each place that must decide something for it, with every generated file and diagnostic unchanged.

**Architecture:** Each `engine == SqlServer ? … : …` (or its negation, or an `if` on it) becomes a switch expression with one arm per member and no `_ =>` arm.  CS8524 is turned off for `src/SqlSource/**.cs`, so a missing member is CS8509, an error under `TreatWarningsAsErrors`.  `SqlDescribableDialects.EngineOf` becomes a switch over every `SqlDialect`.  The architecture page gains `### Extending query methods` with `#### Adding a database engine`.

**Tech Stack:** C# (`netstandard2.0` generator, `LangVersion` preview), xUnit v3 with Shouldly, the golden corpus of PR 1.

**Spec:** `docs/superpowers/specs/2026-10-30-query-method-emission-refactor-design.md`, section "PR 2: a closed set of engines, across the generator", and "Documentation".

## Global Constraints

- Byte identity: `tests/SqlSource.Tests/Generator/QueryMethodCorpusTests.cs` passes unchanged in `tests/SqlSource.Tests` and `tests/SqlSource.Tests.RoslynFloor`.  A changed golden file means the code is wrong; never run with `SQLSOURCE_UPDATE_GOLDEN`.
- No `_ =>` arm over `DatabaseEngine`.  Arms list `PostgreSql` before `SqlServer`, the enum's order.
- A decision written as a statement (`if`, `return`, `yield`) takes its value from a switch expression first.
- `MethodDrivers`, `CodeGenEngines`, `DatabaseEngineExtensions`, `TypeOverrideLists` and `MethodsInput.AllUses` keep their two-engine shape.
- No other unit's scope: no argument model, no execution capabilities or `ConversionKind`, no method identity, no writer split.  The writer's five engine branches stay where they are, each a switch.
- The `SqlDialect.SqlServer` checks of the parsing code and `SidecarChecker.HasTypeMap` test the dialect, not the engine, and are not changed.
- No user documentation, manual, example, `docs/deferred` or `docs/tech-debt` change.

## Where the code differs from the spec

- **The count holds:** 28 branches in 13 files, as the spec says, counting `SqlDescribableDialects.EngineOf` and `QueryTyper`'s engine from a `SidecarType`.
- **`QueryTyper.Chosen` takes its engine from the type's class, not from an engine** (`type is SqlServerType ? SqlServer : PostgreSql`, after a guard that returns for any other class).  A class hierarchy cannot be switched exhaustively, so the guard and the engine become one `switch` statement over the type, whose `default` is the guard's return.  A new `SidecarType` then falls to the unknown-engine path, never to PostgreSQL.
- **The `_ =>` rule goes in `src/SqlSource/AGENTS.md`**, by the owner's choice on review finding R3-F1, since it covers every folder of the generator; some of that file's bullets are tightened to keep it within 8 KB.  Enforcing the rule with an analyzer of the repository's own, for the generator and the tool, is TD-0096, by the owner's choice.
- **IDE0072 is off beside CS8524:** with `EnforceCodeStyleInBuild` and `latest-all` it asks for the same discard arm.  It also made every enum switch with a discard name each member, so, by the owner's choice on review finding R1-F1, every enum switch expression under `src/SqlSource` drops its discard too and CS8509 guards them all.  Five tests that passed an unnamed value (`(Enum)99`) to pin the old fallbacks go with the fallbacks; no enum the generator switches on is ever cast from a number read from input.

## Review Focus

- A type override marker in a `mysql`, `sqlite`, `ansi`, `oracle`, `mariadb` or `cockroachdb` file must still be read by PostgreSQL's key rules: pinned by `EngineOf_EveryDialect_IsSqlServerForMssqlAndPostgresOtherwise`.
- A future describable dialect left on PostgreSQL's rules must fail a test: `EngineOf_DescribableDialects_EachHasAnEngineOfItsOwn`.
- A SQL Server `NeedsDriver` type keeps the message that names SqlClient, and a PostgreSQL one the message that names Npgsql, after the two arms of `QueryTyper.Diagnose` merge: covered by the existing typer tests and the corpus `diagnostics` cases.
- A type of an engine this version does not know (`OtherEngineType`) chosen by a marker keeps the map's error: covered by the existing `QueryTyper` tests of `OtherEngineType`; the implementer confirms one exists and names it in the hand-back.
- Nothing a user writes makes a switch throw: every switch is over a value the code produced from a closed set, and every `SqlDialect` member is named.

---

### Task 1: Exhaustive engine switches

**Files:**
- Modify: `.editorconfig` (section `[src/SqlSource/**.cs]`)
- Modify: `src/SqlSource/Settings/DatabaseEngine.cs`, `Settings/QuerySettings.cs`, `Settings/TypeOverrides/TypeOverrideGrammar.cs`
- Modify: `src/SqlSource/Parsing/SqlDescribableDialects.cs`, `Parsing/Markers/SqlMarkerScope.cs`
- Modify: `src/SqlSource/Generation/Emitting/ParameterBinder.cs`, `Generation/Emitting/QueryMethodWriter.cs`
- Modify: `src/SqlSource/Generation/Types/QueryTyper.cs`, `Generation/Types/Drivers/LibraryProbe.cs`
- Modify: `src/SqlSource/Generation/Methods/DatabaseEngineExtensions.cs`, `MethodDrivers.cs`, `ConversionPlanner.cs`
- Modify: `src/SqlSource/Generation/Converters/ConverterTargets.cs`, `Generation/Models/PropertyNames.cs`
- Test: `tests/SqlSource.Tests/Parsing/SqlDescribableDialectsTests.cs`

**Interfaces:**
- Produces: no signature changes.  `SqlDescribableDialects.EngineOf(SqlDialect) : DatabaseEngine` keeps its signature and values.

- [ ] **Step 1: Write the failing test of the dialect rule** in `SqlDescribableDialectsTests`:

```csharp
// A dialect that becomes describable needs an engine of its own, or its type override is read by another's rules.
[Fact]
public void EngineOf_DescribableDialects_EachHasAnEngineOfItsOwn() =>
    SqlDescribableDialects.All.Select(SqlDescribableDialects.EngineOf).Distinct().Count()
        .ShouldBe(SqlDescribableDialects.All.Length);

[Fact]
public void EngineOf_EveryDialect_IsSqlServerForMssqlAndPostgresOtherwise()
{
    foreach (var dialect in Enum.GetValues<SqlDialect>())
    {
        SqlDescribableDialects.EngineOf(dialect)
            .ShouldBe(dialect == SqlDialect.SqlServer ? DatabaseEngine.SqlServer : DatabaseEngine.PostgreSql, dialect.ToString());
    }
}
```

Both pass today (they pin behaviour); run them to confirm, since the refactor must keep them green: `dotnet test --project tests/SqlSource.Tests -- --filter-class "SqlSource.Tests.Parsing.SqlDescribableDialectsTests"`.

- [ ] **Step 2: Turn CS8524 off** in `.editorconfig`'s `[src/SqlSource/**.cs]` section, with a comment: a switch expression over `DatabaseEngine` or `SqlDialect` names every member and has no discard, so that adding a member fails the build with CS8509 at every switch; CS8524 warns of an unnamed value, which the code never makes.  Link `docs/architecture/generator.md#adding-a-database-engine`.  Line: `dotnet_diagnostic.CS8524.severity = none`.

- [ ] **Step 3: Rewrite each branch as a switch expression.**  Behaviour, including short-circuit order where it matters, is unchanged.  The sites, by file:
  - `QuerySettings.ResolveTypeOverrides`: the `Over(...)` call per engine.
  - `TypeOverrideGrammar.TryNormalize`: `TryNormalizePostgres` / `TryNormalizeSqlServer` per engine.
  - `SqlDescribableDialects.EngineOf`: over `SqlDialect`, arms `SqlServer => SqlServer`, `PostgreSql => PostgreSql`, and `Ansi or MySql or MariaDb or Sqlite or Oracle or CockroachDb => PostgreSql` with a comment that these cannot be described, so only a marker's type override reads their keys, by PostgreSQL's rules, as before; a dialect that becomes describable gets an engine of its own (see the guide).  Its doc comment keeps saying what it returns.
  - `SqlMarkerScope.Build...`: `isSqlServer` from a switch expression.
  - `ParameterBinder.For`.
  - `QueryMethodWriter`: `DrainsWhenLeftAtARow`, `ChecksTransaction`, `AppendCompletedTransactionCheck`, the `<exception>` of `CompletedTransactionType` in the docs, and `AppendDrain`; each `if` reads a local bool from a switch expression.  Keep the comments.
  - `QueryTyper.Chosen`: replace the guard `if (type is not (PostgresType or SqlServerType))` and the later `var engine = type is SqlServerType ? …` with one `switch (type)` statement before the first use that sets `engine` for `PostgresType` and `SqlServerType` and, in `default`, runs the guard's body; keep the guard's comment on the `default`.
  - `QueryTyper.Diagnose`: merge `TypeMappingKind.NeedsDriver when engine == SqlServer` and the later `TypeMappingKind.NeedsDriver` into one arm `NeedsDriver => engine switch { PostgreSql => <Npgsql's>, SqlServer => <SqlClient's> }`, at the place of the first, after checking no arm between them matches `NeedsDriver`.
  - `QueryTyper.AsAKey`: the tail returns a switch expression.
  - `QueryTyper.OverrideLookup`'s constructor: `OfPostgres` and `OfSqlServer` each from a switch expression of `Find` or `null`, so one delegate is made, as now.
  - `LibraryProbe`: the seven in `IsAvailable`, `DefaultOf`, `MissingAssembliesOf`, `IsBelowTheFloor` and `PackagesBelowTheFloor`.  The last two may share a private `bool SqlServerBytesBelowTheFloorOn(DatabaseEngine engine)`.  The `_ =>` arms over `SqlLibrary` stay.
  - `DatabaseEngineExtensions.ToCodeGenEngines` and `DatabaseName`; `MethodDrivers.Allows`.
  - `ConversionPlanner.SpatialOf` (engine first, parenthesised, then the other conditions in their order) and `IsForeign`.
  - `ConverterTargetsExtensions.Of`; `PropertyNames`' default schema.

- [ ] **Step 4: Fix `DatabaseEngine`'s comments.**  `PostgreSql`'s summary: `PostgreSQL.`  The enum's summary keeps its text and adds that every decision on an engine is a switch expression that names each member, and that adding one follows `docs/architecture/generator.md`, "Adding a database engine".  `ParameterBinder`'s summary adds that a new engine adds a subclass and its arm in `For`, per the same guide.

- [ ] **Step 5: Confirm nothing is left.**  `grep -rnE 'DatabaseEngine\.(SqlServer|PostgreSql)' src/SqlSource --include='*.cs'` shows only switch arms, the enum's own uses in `MethodsInput.AllUses`, and `QueryTyper`'s type switch assignments.  `grep -rnE '_ =>' ` near any engine switch shows none.

- [ ] **Step 6: Build and test.**  `dotnet build` (no warnings), then `dotnet test --project tests/SqlSource.Tests` and `dotnet test --project tests/SqlSource.Tests.RoslynFloor`.  Expected: all pass, the corpus unchanged (`git status` shows no file under `Golden/`).

- [ ] **Step 7: Probe the guard.**  Temporarily add a member `MySql` to `DatabaseEngine`, build `src/SqlSource`, and count the CS8509 errors: one for each switch rewritten in Step 3 (28 sites, fewer where two sites share a helper).  Remove the member.  Report the count in the hand-back; commit nothing of the probe.

- [ ] **Step 8: Commit.**  `refactor: decide every engine branch with an exhaustive switch`

### Task 2: The engine guide and its pointers

**Files:**
- Modify: `docs/architecture/generator.md` (new `### Extending query methods` after `### Query methods`; the `### The type override` marker bullet)
- Modify: `src/SqlSource/Generation/AGENTS.md`, `src/SqlSource/Settings/AGENTS.md`

- [ ] **Step 1: Write `### Extending query methods`** with a one-paragraph lead (the generator's engines are a closed set: each decision is a switch expression that names every member, and the compiler lists the places a new one must decide) and `#### Adding a database engine`, an ordered list for the code as it is:
  1. The `DatabaseEngine` member, then each switch CS8509 lists, and the two-engine structures no switch guards: `MethodDrivers` (a field), `CodeGenEngines` (a bit) and `ReportMissingDrivers` in `SqlSourceGenerator` (SQLSRC273), `TypeOverrideLists` and `TypeKeys.TryNormalizeEach`, `MethodsInput.AllUses`.
  2. The dialect: its `SqlDialect` rules, its place in `SqlDescribableDialects.All`, its arm in `EngineOf` (the test fails until it has an engine of its own), `SidecarChecker.HasTypeMap`.
  3. The sidecar: its name in `SidecarValues.Engine`, a `SidecarType` record and its reader in `SidecarTypeReader`, a case in `QueryTyper`'s type switches; the describer, linking `tool.md`.
  4. The type map in `Generation/Types/Maps/` and the driver probe in `Generation/Types/Drivers/`, with `LibraryProbe`'s rows.
  5. A `ParameterBinder` subclass and its arm in `ParameterBinder.For`.
  6. The writer's switches decide SQL Server's transaction and drain statements for the new engine; name the five.
  7. Golden-corpus rows (a `CorpusEngine` row, its `OwnScenarios` and golden files, `tests/README.md`), database tests and image pins (root `AGENTS.md` rule 5), and the manual.
  Every name is checked against the code before it is written.
- [ ] **Step 2: Fix the marker bullet of `### The type override`**: it names `EngineOf` as SQL Server's rules for `mssql` and PostgreSQL's for every other dialect; add that the other dialects cannot be described, so only their markers reach it.  Fix any other line the change makes wrong (`grep -n 'EngineOf\|every other dialect\|every dialect' docs/architecture/*.md`).
- [ ] **Step 3: Add to `src/SqlSource/Generation/AGENTS.md`** a section `## Engines` with two bullets: a decision on a `DatabaseEngine` is a switch expression that names every member, with no `_ =>` arm (CS8524 and IDE0072 are off for that); adding an engine follows [the guide](../../docs/architecture/generator.md#adding-a-database-engine).  Add one bullet to `src/SqlSource/Settings/AGENTS.md`, where the enum is.  Keep each file under 8 KB.
- [ ] **Step 4: Validate** `dotnet test --project tests/SqlSource.Tests -- --filter-namespace "SqlSource.Tests.Documentation"` (link and AGENTS size tests).
- [ ] **Step 5: Commit.**  `docs: add the guide to adding a database engine`

### Task 3: Measure and validate (manager)

- [ ] Time and allocation of a generator run over the corpus, before (`origin/main`) and after, from a `Stopwatch` loop over a `Release` build, for the PR description.
- [ ] `./pre-commit-validation.sh --full`; then each describer project is untouched, so no floor-image run is needed (state it).
