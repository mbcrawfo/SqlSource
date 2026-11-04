# Closed sets across the generator and the tool

## Intent

When a new database, a new setting value or any other new enum member is added, no code may quietly take a default branch and do the wrong thing, where only a test would notice.  The build fails at each decision the new member needs, and a value that is no member at all fails loudly at run time.

The earlier [query-method emission refactor](2026-10-30-query-method-emission-refactor-design.md), PR 2, made `DatabaseEngine` a closed set in the generator by forbidding discard arms.  This work replaces that rule with one that the built-in analyzers enforce, resolves [TD-0096](../../tech-debt/TD-0096-no-check-keeps-a-catch-all-out-of-an-enum-switch.md) that way, and closes the places no switch over an enum reaches: per-engine fields, the sidecar's type records, the tool's describer registry, and `if` checks on an enum.

What we found in the tool: no tool code names an engine outside `Describing/Postgres/` and `Describing/SqlServer/`, except the registration in `ToolHost.Create`.  Its gaps are the registry, filled at run time, and its discard arms.

## Goals

- **One rule for every switch expression over an enum**, in `src/SqlSource` and `src/SqlSource.Tool`: it names every member and ends in `_ => throw UnexpectedEnumValueException.Of(value)`.  IDE0072 fails the build when a member is not named, whether or not there is a discard; a probe build confirmed this.
- **The same rule for switch statements** (my call, for one rule throughout): IDE0010 already fails the build for an unnamed member even with a `default:` label, and the `default:` label stands alone and throws.  A `default:` stacked onto named cases, as in `SqlServerParameterBinder`'s `case Facet.Max: default: return true;`, is split so that the named cases keep their behaviour and `default:` throws.  That is 22 labels in `src/SqlSource` and 8 in the tool.
- **Every engine decision reaches a switch over `DatabaseEngine`**, including the ones held in per-engine fields, the sidecar's type records and the tool's describer registry.
- **Adding an engine fails the build of both projects** at each place that has to decide something for it.
- **TD-0096 is resolved** in this work, by this rule and not by its proposed analyzer.

## Non-goals

- A repository analyzer.  The owner decided that enforcing the naming is enough.  That the discard throws, and throws this exception, is a convention in `AGENTS.md`.
- Adding a third engine, or changing anything a user can see.
- The test projects.  Their `dialect == "mssql" ? … : …` choices pick fixtures, and a new engine adds its own tests.
- Switches over strings, types or numbers that cannot name every value, other than the sidecar's type records below.

## Anti-goals

- **Any change to generated code, sidecars, diagnostics or golden files.**  This is a refactor: the whole suite passes with no golden file updated.
- **A helper that gives a default for an engine it does not know.**  That is the silent fallback this work removes, moved somewhere else.
- **A bool that names an engine**, such as `isSqlServer`, which collapses the closed set to two answers.

## Constraints

- `src/SqlSource` targets netstandard2.0, runs on the Roslyn 4.14 floor, and caches the incremental pipeline by value equality.  So anything added to a pipeline value must compare by value, and nothing may add work for each line of a query.
- `src/SqlSource/AGENTS.md` (8183 bytes) and `src/SqlSource.Tool/AGENTS.md` (8172 bytes) are at their 8 KB limit, which `AgentsFileTests` checks.  New rules replace or tighten existing ones.
- The repository's rules apply in full: validate before each commit, run `--full` before each push, and run branch-review.  A change to a describer or its tests also runs that describer's tests on its floor image.  Docs change in the same PR as the code they describe.

## Approaches

### The rule for a discard

1. **Forbid discards, enforced by a repository analyzer** (TD-0096's proposal).  A new member is CS8509, and a discard written back is an analyzer error.  It costs an analyzer project referenced by both projects, with its own tests.
2. **Require a throwing discard, with the naming enforced by IDE0072 (chosen).**  IDE0072 already fails the build for an unnamed member even when a discard is there, so a discard written to cover "the rest" can no longer hide a new member.  The discard only catches values that are no member, such as a cast from a number, and the throw makes that loud.  It needs no new project.  Only the throw is a convention.  The owner chose this.

### Per-engine structures

- **A. A per-engine container, `PerEngine<T>`**: one slot per engine, indexed by engine, built through a switch.  No engine's value is read by its field name, and a new engine gets a slot automatically.  It cannot hold a different type for each engine.
- **B. Keep the named fields and add `For(engine)` and `With(engine, value)` switches**, with every place that handles both fields by name rewritten as a loop over the engines.  It changes the fewest types and works for any type, but a field can still be read by name, so a new side-by-side pair is held off only by convention.
- **C. A for values of one type for every engine, and B for the rest (chosen).**  Most structures are closed structurally, and only the driver probes and the typed override delegates rely on the convention.  The owner chose this.  It reverses PR 2's choice to keep `MethodDrivers`, `CodeGenEngines` and their kind in their two-engine shape.

## The design

### 1. The rule for enums

- **`UnexpectedEnumValueException`**: an `internal sealed` exception derived from `InvalidOperationException`, in the root of `src/SqlSource`, which the tool reaches through its `InternalsVisibleTo`.  `UnexpectedEnumValueException.Of<T>(T value) where T : struct, Enum` gives one whose message names the enum's type and the value.  It is not one of the exceptions `Generation/Exceptions/` writes into a user's project.
- **`.editorconfig`**: the `[src/SqlSource/**.cs]` section drops `CS8524 = none` and `IDE0072 = none`, so both projects follow the defaults, which are errors under `TreatWarningsAsErrors`.  Its comment is replaced by one stating the rule.
- **The generator**: each switch expression over an enum that CS8524 and IDE0072 then report gets the throwing discard.  The build lists every one.
- **The tool**: each switch expression over an enum whose discard gives a value gets the throw instead.  To find them, remove every `_ =>` arm in the tool on a scratch branch and build: CS8524 marks the switches over enums (IDE0072 already makes them name each member), and CS8509 marks the switches over strings and types, which keep their discards.
- **`[Flags]` enums** hold combinations and are tested by bit, as `CodeGenEngines` is.  The bit of each engine comes from a switch over `DatabaseEngine`, as `DatabaseEngineExtensions` already gives it.  Where a flags value is known to hold a single member, and code picks behaviour by which one, it is a switch like any other.  That switch names the combined members in one throwing arm: `ConverterTargets.None or ConverterTargets.PostgreSql or ConverterTargets.All => throw …` (my call).
- **`DatabaseEngines.All`**: the engines in declaration order, built once from `Enum.GetValues`, so the list needs no upkeep.  It is what every loop over the engines reads.

### 2. Per-engine structures

**The container.**  `PerEngine<T>` is a readonly struct in `src/SqlSource` with one slot per `DatabaseEngine`.  It compares by value and is sound in a pipeline value when `T` is.

- `PerEngine.Of(Func<DatabaseEngine, T>)` builds it by calling the function once for each member of `DatabaseEngines.All`.  Its callers pass a switch expression over the engine, so a new engine is IDE0072 there.
- It has an indexer by engine, and `Select` and `Zip` for merging slot by slot.
- How it stores its slots is the builder's call.  If it stores them by the enum's number, a test asserts that the members of `DatabaseEngine` run from 0 with no gaps.

**Which code changes.**  The gap is code that handles engines by naming each one's field side by side.  A field read only by its own engine's code is not a gap, because a new engine's code never reads it.  `TypeKeyNames`' dictionaries, `SidecarTypeReader`'s key arrays and `ParameterBinder`'s two binders, which `ParameterBinder.For` already picks with a switch, stay as they are.  The known sites:

| Site | Today | After |
|---|---|---|
| `SettingsLevel.TypeOverrides`, `.SqlServerTypeOverrides`, and its merge in `Over` | Two named lists; PostgreSQL's is unnamed | One `PerEngine<EquatableArray<TypeOverride>?>`, merged slot by slot |
| `SqlMarkerScope.Build` | `isSqlServer` bool, then two ternaries | Fills only its file's engine's slot through `PerEngine.Of` |
| `SettingsLevelReader.Read` / `ReadTypeOverrides` | Out parameter for SQL Server's list | Gives the container |
| `TypeOverrideLists` | Record with `PostgreSql`, `SqlServer` | The container, or a record that holds one |
| `QuerySettings` type override resolution, `FileTypesInput.TypeSettingsOf` | Switch arms or null checks that name each field | Index or loop |
| `MethodDrivers.Postgres`, `.SqlServer` | Two bools, read by `Allows`'s switch | `PerEngine<bool>`; `PostgresAtLeast10` and `SqlServerSpatial` stay, since they name capabilities |
| `ReportMissingDrivers` | Two `if`s on `CodeGenEngines` bits | A loop over `DatabaseEngines.All`, with each engine's driver name and whether its probe is referenced given by switches (approach B) |
| `TypeKeys.TryNormalizeEach`, `TypeOverrideGrammar` | Each engine's normalizer by name | A loop over the engines, with the normalizer picked by a switch |
| `MethodsInput.AllUses`, `MappedTypeNames` | Engine rows by name | The builder classifies each: A where every engine has a value of the same type, B otherwise |
| `NpgsqlProbe` / `SqlClientProbe` pairs, `QueryTyper.OverrideLookup.OfPostgres` / `.OfSqlServer` | Values of a different type for each engine | Approach B: a switch for each question asked of them, and no code that reads both fields side by side |

The table names the sites found so far.  The builder sweeps both projects for any other pair of per-engine fields, or a bool named for an engine, and treats each the same way.  The engine guide's step 2 in `docs/architecture/generator.md`, "the structures that hold one entry per engine, which no switch guards", ends up either empty or shrunk to the approach-B fields, each with the switch that guards it.

### 3. The sidecar's type records

- **`SidecarType` gets an abstract `Kind`**, of a new enum `SidecarTypeKind` with `PostgreSql`, `SqlServer` and `OtherEngine`, which each sealed record sets.
- **Each match on the record's type becomes a switch over `type.Kind`**, followed by a cast to the record, which fails loudly if a record's `Kind` is wrong.  The known matches:
  - `SidecarComparer.Type`, the tuple match whose `_ => null` reports no difference;
  - `SidecarWriter.WriteType`;
  - `QueryTyper`'s three, at the engine's choice, the override key and the mapping;
  - `QueryTyper.DatabaseTypeOf`'s `if (type is SqlServerType)`;
  - `ModelReader.IsArrayType`, which asks Npgsql's question of any engine's type.
- **`OtherEngine`'s arm keeps today's behaviour** at each site: a type of an engine this version does not know.
- **`SidecarTypeKind` and `DatabaseEngine` are related by a switch** from one to the other, so a new engine is IDE0072 at that switch, and then at every switch over the kind once it gets a member.
- **A type test inside one engine's own code stays**, such as `PostgresErrors`'s `is PostgresType { Element: … }` in the PostgreSQL describer.
- **The engine's name in a sidecar**: `SidecarValues.Engine`'s constants are removed and replaced by a switch from `DatabaseEngine` to its name, and the 34 test files that use them call the switch instead (my call).  `SidecarTypeReader` turns the name into an engine by looping over `DatabaseEngines.All`, then switches over the engine; a name no engine has stays `OtherEngineType`.  The tool's `EntryBuilder` writes the engine's name through that switch, not `SqlDialectName.Canonical(dialect)`.  The two give the same text today, so no sidecar changes.

### 4. The tool's describers

- **`IQueryDescriber.Dialect` becomes `Engine`**, a `DatabaseEngine`, and `DescriberRegistry` is keyed by engine.
- **`DescriberRegistry.Find(dialect)`** gives null for a dialect that `SqlDescribableDialects.Contains` rejects, and otherwise looks up `SqlDescribableDialects.EngineOf(dialect)`.  It must check `Contains` first, because `EngineOf` gives PostgreSQL's engine to dialects that cannot be described.
- **`ToolHost.Create` builds the released registry** from `DatabaseEngines.All` through `DescriberFor(DatabaseEngine)`, a switch, so a new engine fails the tool's build until it has a describer.  Tests keep handing the host registries of their own, with fakes.
- **`SQLSRC216`** stays for a test's host that lacks a describer.  The diagnostic and its text are unchanged.
- **`ToolHostTests`** asserts the released host has a describer for every member of `DatabaseEngines.All`, not the two it names today.

### 5. Equality checks on an enum

- **The rule**, in both `AGENTS.md` files: a check of one value, such as `state == PlannedFileState.Ready`, is fine; a decision that gives members their own behaviour is a switch expression.
- **Every `== X.Member`, `!=`, or `is X.A or X.B` on `DatabaseEngine` or `SqlDialect` becomes a switch.**  The known ones:
  - `SidecarChecker.HasTypeMap`, `dialect is PostgreSql or SqlServer`, which would silently give a new dialect no type map;
  - `SqlParameterList`'s two `rules.Dialect == SqlDialect.SqlServer`;
  - `SqlDialectName`'s `is MySql or MariaDb`;
  - `MethodPlanner`'s converter cache, `target == ConverterTargets.SqlServer ? … : target == NpgsqlBefore10 ? … : _npgsql10`, which silently gives any other target Npgsql 10's converters.  It becomes a switch over the single target, as section 1 says for flags, and the three cache fields stay, one for each arm.

  A switch over `SqlDialect` names all of its eight members.
- **The builder sweeps both projects for the same kind of check on any other enum** and converts the ones that pick behaviour by member.

### 6. Other shapes

- **`NullabilityWalk.Combine`**, a tuple switch over `Resolution`, becomes nested switches, so a new `Resolution` is IDE0072 there and doesn't quietly become `Unknown`.
- **The other four tuple switches** are over characters, records and nullables, and stay as they are.
- **A property pattern or `when` clause that picks an enum member** follows the same rule as a tuple switch.  The builder converts any found to a switch over the enum.

### 7. Documents

- **A new `src/AGENTS.md`** holds the rules both projects share (my call).  The root `AGENTS.md` says a rule goes at the deepest level it applies to, and `src/SqlSource.Tool/AGENTS.md` has 20 bytes to spare.  The shared rules are:
  - the throwing discard and `default:`;
  - the equality rule;
  - no bool names an engine;
  - per-engine values go in `PerEngine<T>`.

  The `_ =>` rule leaves `src/SqlSource/AGENTS.md`, and its link to the engine guide moves with it.
- **`src/SqlSource/Settings/AGENTS.md`**: the rule that a level holds two arrays, one for each engine, now names the container.
- **`docs/architecture/generator.md`**:
  - "Adding a database engine": steps 1 and 2 merge, since the build now lists nearly every site;
  - the approach-B fields that remain are listed;
  - `PerEngine<T>`, the type records' `Kind` and the sidecar engine name are described where those components are.
- **`docs/architecture/tool.md`**: the describer registry by engine, and `DescriberFor`.
- **TD-0096**: the item file is deleted, its row removed from `docs/tech-debt/README.md`, and every reference removed: the `.editorconfig` comment and `src/SqlSource/AGENTS.md`.  The 2026-11-02 plan is a dated record and keeps its mention.
- **User docs** do not change.

## How the work is split (my call)

Three PRs, each one green and reviewable alone:

1. **The rule.** `UnexpectedEnumValueException`, `DatabaseEngines.All`, the `.editorconfig` change, the throwing discards in both projects, the `AGENTS.md` rules, and the resolution of TD-0096.
2. **Per-engine structures.** `PerEngine<T>`, every site of section 2, and the engine guide's rewrite.
3. **Records, describers and checks.** Sections 3 to 6, and `tool.md`.

The owner may prefer one PR; the order holds either way.

**Sequencing (the owner's call):** this work lands after the file-owned settings work, the `claude/file-settings-*` branches, which change `QuerySettings`, `SettingsLevelReader`, `Settings/AGENTS.md` and `SqlSourceGenerator.cs`.  PR 2 is planned against `main` once that work has merged, and the plan's sites are re-checked there.  PR 1 touches those files only to add discards, and may start sooner, rebasing onto that work as it merges.

## Left to the builder

- `PerEngine<T>`'s storage, members beyond the indexer, `Of`, `Select` and `Zip`, and where it lives in `src/SqlSource`.
- The exact message of `UnexpectedEnumValueException`, and whether `Of` is generic or takes `Enum`.
- Classifying `MethodsInput.AllUses`, `MappedTypeNames` and any further pair found by the sweep as A or B.
- Whether `TypeOverrideLists` becomes the container itself or a record that holds one.
- Tests: `PerEngine<T>`'s own tests, the contiguity test if its storage needs one, and the `ToolHostTests` change.
- **What may change in a test** (my call): its source may change wherever it compiles against an API this work changes, such as `SettingsLevel`'s lists, the `MethodDrivers` constructor, a fake describer's `Engine`, `OverrideLookup`'s fields or the sidecar engine names.  No old member is kept for a test's sake.  What a test asserts, its fixtures, and every golden file stay as they are.
