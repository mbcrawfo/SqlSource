# TD-0108 renaming tables Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Items 7, 8, 9, 10, 11, 15 and 18 of TD-0108 (the renaming's rule tables, the entry's paths, the JSON writers and the split of `PostgresPlanRenamer`), and item 32 of TD-0109, which item 8's reflection walk resolves.  A refactor: no behaviour change.

**Architecture:** Each copy moves to one owner and every site calls it: the one-entry `expected.json` to a new `Bundles/ExpectedFile`, the JSON file writing to a new `Describing/Capture/JsonFile`, the entry's type-record paths to `EntryRenamer.TypeRecords`, SQL Server's column methods and row members to `SqlServerFields`, PostgreSQL's `Own` paths to `PostgresFieldRules`.  SQL Server's field test walks `SqlServerCalls.cs` by reflection, as PostgreSQL's walks `PostgresCalls.cs`, through one walk shared by both.  `PostgresPlanRenamer` is split by `partial` into its three walks.

**Tech Stack:** C# (.NET 10 SDK, tool targets `net8.0`), System.Text.Json, xunit v3, Shouldly.

**Spec:** `docs/tech-debt/TD-0108-duplicated-and-uneven-patterns-in-src.md` (items 7, 8, 9, 10, 11, 15, 18: Problem and Proposed fix), `docs/tech-debt/TD-0109-duplicated-arrangements-and-uneven-conventions-in-tests.md` (item 32).

## Where the merged code differs from the spec

The line numbers of the spec predate td-0104, td-0102, td-0103 and 108-renaming-reads.  The code is followed:

- `SqlServerFields` already lists its methods through `SqlServerCallNames` in its loops; the literals left are the keys of its dictionary (`"open answer.productVersion"` and the rest, now with td-0104's `parameters request.reused[]...`) and of the entry's type members.
- `SqlServerRenaming.CopiesIn` (td-0104) reads the members `reused`, `name` and `copies` of the `parameters` request, which `SqlServerFields` rules too.  Item 9's fix covers them with the six row members, since they are the same copy.
- `PostgresPlanRenamer` is 800 lines; its rename writer is at `Renaming.Run` (`:472-504`), with no final line break, unlike the three files.
- `BundleReplay` declares the capture's `captureVersion` and `engine` keys again (`:49-50`); item 15's "`CaptureJson`'s keys made internal" covers them.

## Global Constraints

- No behaviour change: every renamed bundle, mapping file, capture and `bundle.json` is byte-for-byte what it was; every existing test passes unchanged but for the names it must follow.
- The leak checks and the proof keep passing: `BundleLeaksTests`, `*FixtureRenamingTests`, `*ErrorRenamingTests`, `RenamingProof`.
- Folder dependencies of `docs/architecture/tool.md#folders` hold: `Describing/Capture/` and `Bundles/Renaming/` use nothing at the tool's root.
- Assertions are Shouldly's; test names are `Method_Scenario_Outcome`.
- `src/SqlSource.Tool/AGENTS.md` is at 8,173 of 8,192 bytes: add nothing to it.  An AGENTS.md rule is at most 80 words.
- Every AGENTS.md line and `docs/architecture/` page that names a moved member is updated in the task that moves it.
- Before every commit: `./format.sh`, then `./pre-commit-validation.sh` passes.

## Review Focus

1. A JSON file whose text holds a line break inside a string: it stays the escape `\n`, and only the writer's own line ends become `\n` (`JsonFile`); `MappingFileTests`, `BundleJsonTests` and the capture tests already pin the files' text.
2. A plan that is refused mid-write: the renamer still gives null, never a partial text (Task 2's `Run`); `PostgresPlanRenamerTests` holds the refusals.
3. A member added to a SQL Server record that no fixture fills: Task 3's walk fails for it.
4. A rule of the SQL Server table whose member no record has: Task 3's second walk test fails for it.
5. A member ruled `Own` in PostgreSQL's table with no arm in `OwnOf`: Task 4's test fails for it instead of a bundle throwing at run time.

---

### Task 1: One writer of the one-entry `expected.json` (item 7)

**Files:**
- Create: `src/SqlSource.Tool/Bundles/ExpectedFile.cs`
- Modify: `src/SqlSource.Tool/Bundles/DiagnoseBundles.cs` (`ExpectedOf`), `src/SqlSource.Tool/Bundles/Renaming/EntryRenamer.cs` (`Rename`), `src/SqlSource.Tool/Bundles/BundleReplay.cs` (add `NoPath`), `src/SqlSource.Tool/Bundles/Renaming/RenamingProof.cs` (remove `NoPath`, use `BundleReplay.NoPath`)
- Modify (forced): `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerErrorRenamingTests.cs:657`, `:773`
- Docs: `docs/architecture/bundles.md` (the raw bundle's paragraph, line ~35, and step 5 of the renaming, line ~134: name `ExpectedFile.Of`)

**Interfaces:**
- Produces: `BundleReplay.NoPath` (`public const string NoPath = "";`, the path of a bundle in no file, whose steps' errors name no path).
- Produces: `ExpectedFile.Of(string query, string engine, SidecarEntry entry, int formatVersion, string toolVersion, CancellationToken token) : Outcome<string, ToolDiagnostic>` — plans `query` through `BundleReplay.Plan` of a `Bundle(null, query, string.Empty, null, [])` at `BundleReplay.NoPath`, and gives `SidecarWriter.Write` of a sidecar of `formatVersion`, `toolVersion` and the one entry with the planned hash; the plan's failure as it is.  A planned query with no hash throws `InvalidOperationException`.

- [ ] **Step 1:** Add `NoPath` to `BundleReplay` and replace `RenamingProof.NoPath` everywhere (`RenamingProof`, `EntryRenamer`, the two test sites).
- [ ] **Step 2:** Write `ExpectedFile.Of`.  `DiagnoseBundles.ExpectedOf` calls it with `bundle.Query`, `bundle.Info!.Engine`, the entry, `SidecarFormat.Version`, `PackageVersion.Prefix`, and keeps its own `InvalidOperationException` naming the entry for a failure; `EntryRenamer.Rename` calls it with the renamed query, `SqlDialectName.Canonical(engine.Dialect)`, the renamed entry, `raw.FormatVersion`, `raw.ToolVersion`, and keeps its `run.Refuse(RenamingProof.Unreadable(BundleFiles.Query, failure))`.
- [ ] **Step 3:** Run `dotnet test --project tests/SqlSource.Tool.Tests --filter-class "*DiagnoseBundlesTests" --filter-class "*EntryRenamerTests" --filter-class "*BundleRenamerTests" --filter-class "*SqlServerErrorRenamingTests" --filter-class "*PostgresRefusalTests"` (or the project whole); all pass.
- [ ] **Step 4:** Update the two lines of `bundles.md`; `./format.sh`; `./pre-commit-validation.sh`; commit `refactor(bundles): one writer of the one-entry expected.json (TD-0108 item 7)`.

### Task 2: One JSON file writer, and the capture's keys shared (item 15)

**Files:**
- Create: `src/SqlSource.Tool/Describing/Capture/JsonFile.cs`
- Modify: `src/SqlSource.Tool/Describing/Capture/CaptureJson.cs` (`FileOptions`, `Write`, its six keys), `src/SqlSource.Tool/Bundles/BundleJson.cs` (`FileOptions`, `Write`), `src/SqlSource.Tool/Bundles/Renaming/MappingFile.cs` (`Options`, `Write`, literal keys), `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresPlanRenamer.cs` (`WriteOptions`, `Renaming.Run`), `src/SqlSource.Tool/Bundles/Renaming/CaptureRenamer.cs` (remove `Request` and `Answer`) and their users, `src/SqlSource.Tool/Bundles/BundleReplay.cs` (`VersionKey`, `EngineKey`)
- Modify (forced): `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresClassificationTests.cs:86`
- Docs: `docs/architecture/tool.md` folder row of `Describing/Capture/` (add "and `JsonFile`, how the tool writes a JSON file"); `docs/architecture/bundles.md:23` (bundle.json written "through `JsonFile`, as a capture is"), and any line naming `CaptureRenamer.Request`.

**Interfaces:**
- Produces: `JsonFile.Options : JsonWriterOptions` (indented, `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`); `JsonFile.Write(Action<Utf8JsonWriter> write) : string` — the file: UTF-8 text with the writer's `\r\n` replaced by `\n`, and a final `\n`; `JsonFile.Text(Action<Utf8JsonWriter> write) : string` — the same with no final line break, for a value held inside another file (the plan).  The remark of why only the writer's own line ends are replaced moves to `JsonFile`.
- Produces: `CaptureJson.VersionKey`, `EngineKey`, `CallsKey`, `MethodKey`, `RequestKey`, `AnswerKey` as `public const` (the class is internal).  `CaptureRenamer.Request` / `.Answer` are gone; every use is `CaptureJson.RequestKey` / `.AnswerKey`; `BundleReplay` uses `CaptureJson.VersionKey` / `.EngineKey`.
- Produces: `MappingFile`'s keys as private constants (`VersionKey = "mappingVersion"`, `NamesKey`, `ReplacementKey`, `ClassKey`, `OriginalKey`), as `BundleJson` declares its own.

- [ ] **Step 1:** Write `JsonFile` and move `CaptureJson.Write(DescribeCapture)`, `BundleJson.Write` and `MappingFile.Write` onto `JsonFile.Write`.
- [ ] **Step 2:** `PostgresPlanRenamer.Renaming.Run` writes through `JsonFile.Text`; a refusal inside the write still returns null (capture a `bool` from the write callback).  `WriteOptions` is gone.
- [ ] **Step 3:** The keys: `CaptureJson`'s public, `CaptureRenamer`'s gone, `BundleReplay`'s and `MappingFile`'s as above.
- [ ] **Step 4:** Run `dotnet test --project tests/SqlSource.Tool.Tests`; all pass, `MappingFileTests`, `BundleJsonTests`, `PostgresPlanRenamerTests` and the capture tests among them.
- [ ] **Step 5:** Docs; `./format.sh`; `./pre-commit-validation.sh`; commit `refactor(tool): one JSON file writer and the capture's keys shared (TD-0108 item 15)`.

### Task 3: SQL Server's table owns its names and is walked by reflection (items 8, 9, the SQL Server half of 11; TD-0109 item 32)

**Files:**
- Modify: `src/SqlSource.Tool/Bundles/Renaming/EntryRenamer.cs` (`TypeRecords` exposed), `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresFieldRules.cs` (`TypeRecords` built from it), `src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerFields.cs`, `src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerRenaming.cs`
- Create: `tests/SqlSource.Tool.Tests/CallMembers.cs`, at the project's root with the other helpers, (the walk of `PostgresFieldRulesTests`, moved)
- Modify: `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresFieldRulesTests.cs` (calls `CallMembers`), `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerFieldsTests.cs`
- Docs: `docs/architecture/bundles.md` (SQL Server's fields, ~`:325`-`:335`, and its tests, ~`:377`; PostgreSQL's members, ~`:252`), `docs/architecture/sqlserver-describer.md:98`, `src/SqlSource.Tool/Bundles/Renaming/SqlServer/AGENTS.md` if a line names a moved member.

**Interfaces:**
- Produces: `EntryRenamer.TypeRecords : IReadOnlyList<string>` (`public static`), the paths from the entry of its type records, `"parameters[].type."` and `"columns[].type."`, as today.  `PostgresFieldRules.TypeRecords` is each prefixed with `BundleFiles.Expected + " "`; `SqlServerFields` loops over it.
- Produces in `SqlServerFields` (`public` on the internal class): `ColumnMethods : IReadOnlyList<string>` (columns, operands, table); `const string Rows = "answer.rows[]"`; the member names `const string Name = "name"`, `SourceServer`, `SourceDatabase`, `SourceSchema`, `SourceColumn`, `Schema = "schema"` (table request), `Reused = "reused"`, `Copies = "copies"` (the `parameters` request's); `Members : IEnumerable<string>`, the `"<method> <path>"` keys of the calls, the entry's left out.  The dictionary is built with a local `Add(method, path, rule)` as `PostgresFieldRules` does, each method a `SqlServerCallNames` constant, each path built from the constants above where it names one; the entry's members from `SidecarKeys` (`Name`, `BindName`, `UserType` + `Schema` / `Name` / `AssemblyQualifiedName`, through `JsonStrings.MemberOf`).  No rule changes.
- `SqlServerRenaming` drops its `ColumnMethods` and its literals and uses `SqlServerFields`'; its `SystemSchemas` field is renamed `EngineSchemas` ("the schemas whose relations are the server's own, folded") and built as `[SqlServerVocabulary.Fold(SystemSchemas.SqlServer), "INFORMATION_SCHEMA"]`.
- Produces in tests: `CallMembers.Of(IReadOnlyDictionary<string, (Type Request, Type Answer)> calls) : IEnumerable<(string Method, string Path, bool IsString)>` and `CallMembers.IsUnderADrop(Func<string, string, FieldRule?> ruleOf, string method, string path) : bool`, the bodies of `PostgresFieldRulesTests.AllMembers`, `MembersOf`, `ValuesOf`, `IsUnderADrop` and `Parent`, moved unchanged.

- [ ] **Step 1: Write the failing tests** in `SqlServerFieldsTests`, with a `Calls` map as PostgreSQL's: open → (`SqlServerOpenRequest`, `SqlServerOpenAnswer`), parameters → (`SqlServerParametersRequest`, `SqlServerParametersAnswer`), probe → (`SqlServerProbeRequest`, `SqlServerProbeAnswer`), declared → (`SqlServerDeclaredRequest`, `SqlServerDeclaredAnswer`), columns and operands → (`SqlServerColumnsRequest`, `SqlServerColumnsAnswer`), table → (`SqlServerTableRequest`, `SqlServerColumnsAnswer`):
  - `Calls_AreEveryCallOfTheDescriber`: the map's keys, ordered, are the literal fields of `SqlServerCallNames`, ordered.
  - `RuleOf_EveryStringMemberOfEveryCall_HasARule`: `CallMembers.Of(Calls)` strings not under a drop with `SqlServerFields.RuleOf(...) is null` → `ShouldBeEmpty()`.
  - `RuleOf_EveryRuleOfTheTable_IsOfAMemberThatExists`: `SqlServerFields.Members` not among the walked members → `ShouldBeEmpty()`.
  The hand-written table, `RuleOf_AMemberOfTheRecords_IsItsRule`, the fixture test and `RuleOf_AnyOtherMember_IsNull` stay; the class comment says what each check holds.  The test's own `Methods`, `SqlMethods`, `DescribeMethods` and `ColumnMethods` lists stay literal: the test pins the table, so it does not read the table's lists.
- [ ] **Step 2:** Run the three; the third fails to compile until `Members` exists, the others pass or fail on a member the table lacks.  A member the walk finds with no rule is reported to the controller, not given a rule.
- [ ] **Step 3:** `CallMembers` from `PostgresFieldRulesTests`; `EntryRenamer.TypeRecords`; `SqlServerFields` and `SqlServerRenaming` as above.
- [ ] **Step 4:** Run `dotnet test --project tests/SqlSource.Tool.Tests` (the `Bundles.Renaming` tests at least); all pass.
- [ ] **Step 5:** Docs (SQL Server's tests now fail for a string member of a record with no rule and for a rule of no member, as PostgreSQL's); `./format.sh`; `./pre-commit-validation.sh`; commit `refactor(renaming): SQL Server's table owns its names and is walked by reflection (TD-0108 items 8, 9, 11; TD-0109 item 32)`.

### Task 4: PostgreSQL's `Own` paths and schema sets in one place (item 10, the PostgreSQL half of 11)

**Files:**
- Modify: `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresFieldRules.cs`, `PostgresRenaming.cs`, `PostgresOwnStrings.cs`, `PostgresWords.cs`
- Test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresRenamingTests.cs`

**Interfaces:**
- Produces in `PostgresFieldRules`: `public const string SettingValue = "answer.settings[].value"`, `ErrorMessage = "answer.error.message"`, `DriverErrorMessage = "answer.driverError.message"`, `Plan = "answer.plan"`, used by `Build`, by `PostgresRenaming.OwnOf` and by `PostgresOwnStrings` (its private `DriverErrorMessage` goes).  `PostgresRenaming`'s four private constants go.
- Produces: `PostgresRenaming.OwnOf(string method, string path) : PostgresRenaming.Own` (`internal static`; the enum `internal`), same arms and same `InvalidOperationException`; `RenameOwn` and `WordsOf` call it with the field's method and path.
- `PostgresFieldRules.EngineSchemas` is `[SystemSchemas.Postgres, "information_schema"]`, `PostgresWords.EngineSchemas` `[SystemSchemas.Postgres, "information_schema", "pg_toast", "public"]`.

- [ ] **Step 1: Write the failing test** `OwnOf_EveryMemberTheTableRulesOwn_IsOneOfPostgresqlsOwnStrings`: for each of `PostgresFieldRules.Members` whose `RuleOf` is `{ Kind: FieldRuleKind.Own }`, split at the first space, `Should.NotThrow(() => PostgresRenaming.OwnOf(method, path))`; and the members found are not empty.
- [ ] **Step 2:** Run it; it fails to compile.
- [ ] **Step 3:** The constants, `OwnOf`, and the two sets.
- [ ] **Step 4:** Run `dotnet test --project tests/SqlSource.Tool.Tests` (the `Bundles.Renaming.Postgres` tests at least); all pass.
- [ ] **Step 5:** `./format.sh`; `./pre-commit-validation.sh`; commit `refactor(renaming): PostgreSQL's Own paths and schemas in one place (TD-0108 items 10, 11)`.

### Task 5: Split `PostgresPlanRenamer` by its walks (item 18)

**Files:**
- Modify: `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresPlanRenamer.cs` (keeps the class remarks, the constants, `ReadOptions`, `Kinds`, `NameKeyClasses`, `KeyKind`, `KeysRead` and the helpers more than one walk calls: `Parse`, `IsPlanRoot`, `ChildOf`, `StemOfChild`, and any other the walks share)
- Create: `PostgresPlanRenamer.Register.cs` (`Register`, `RegisterNode`), `PostgresPlanRenamer.Words.cs` (`WordsOf` through `AllStrings`, `CheckedSpelling` if only they use it), `PostgresPlanRenamer.Renaming.cs` (`Rename` and the nested `Renaming`)
- Docs: `docs/architecture/bundles.md` (~`:267`, the plan: say which file holds each walk), `src/SqlSource.Tool/Bundles/Renaming/Postgres/AGENTS.md` (its line names `PostgresPlanRenamer.cs` for the kinds, which stay there: keep it true), `src/SqlSource.Tool/Describing/Postgres/Walk/AGENTS.md:11` (same).

**Interfaces:** none new: `internal static partial class PostgresPlanRenamer`, every member's signature unchanged.  Each new file has a one-line comment above its `partial` saying which walk it holds.

- [ ] **Step 1:** Move the members, unchanged, into the three files.
- [ ] **Step 2:** Run `dotnet test --project tests/SqlSource.Tool.Tests` (`PostgresPlanRenamerTests` and the PostgreSQL renaming tests); all pass.
- [ ] **Step 3:** Docs; `./format.sh`; `./pre-commit-validation.sh`; commit `refactor(renaming): split PostgresPlanRenamer by its three walks (TD-0108 item 18)`.

### Task 6: The tech-debt records

**Files:**
- Delete: `docs/tech-debt/TD-0108-duplicated-and-uneven-patterns-in-src.md` (items 7, 8, 9, 10, 11, 15 and 18 are its last open items)
- Modify: `docs/tech-debt/README.md` (remove TD-0108's row), `docs/tech-debt/TD-0109-duplicated-arrangements-and-uneven-conventions-in-tests.md` (item 32 struck with "Fixed: …", without its "(TD-0108 item 8)"; the "20. to 41." fix struck with "Done."; the Impact without "and item 32 is a case a reader may think is covered"; its README row kept current)

- [ ] **Step 1:** `grep -rn "TD-0108" . --exclude-dir=.git` finds nothing outside `docs/superpowers/` (whose plans keep resolved ids, as for TD-0104).
- [ ] **Step 2:** `./pre-commit-validation.sh`; commit `docs(tech-debt): TD-0108 resolved, TD-0109 item 32 fixed`.
