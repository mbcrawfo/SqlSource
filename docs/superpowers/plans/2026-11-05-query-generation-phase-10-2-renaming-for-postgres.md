# Phase 10.2, renaming for PostgreSQL - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** PostgreSQL's part of the renaming, an `IEngineRenaming` that `EngineRenamings.For(DatabaseEngine.PostgreSql)` gives, with no command: every PostgreSQL fixture and every realistic case renamed passes the token check, replays to its renamed entry with the original's provenance, and leaks no name or literal of its schema or query; its refusals are `RenamingRefusal` values that 10.4 reports.

**Architecture:** A new folder `src/SqlSource.Tool/Bundles/Renaming/Postgres/` holds the engine's part, split by concern: the words (word rule, fold, committed vocabulary, the planner's names), the field rules of every call and of the entry, the engine's own strings (settings and errors), the plan, and the derivations (lists in name order, `prepare`'s error position).  `PostgresRenaming` implements `IEngineRenaming` by delegating to them, and is the `PostgreSql` arm of `EngineRenamings.For`.  The interface of 10.1 is not changed.

**Tech Stack:** C# on `net8.0`, `System.Text.Json` (`JsonNode`), the generator's lexer, the walk's `PlanTokens`, xUnit v3 + Shouldly, Testcontainers PostgreSQL in `tests/DatabaseTests/Postgres`.

**Spec:** `docs/superpowers/specs/2026-11-04-query-generation-phase-10-bug-reports-design.md` ("Renaming" steps 1 to 13, "10.2 Renaming for PostgreSQL", "Testing", "Documentation"), as amended by `docs/superpowers/specs/2026-11-05-query-generation-phase-10-spike.md` (findings 1 to 5, the six rules), and the contract of `docs/architecture/bundles.md` ("The engine's contract", "Rules found while building").

## Global Constraints

- Root `AGENTS.md` rules apply: `./pre-commit-validation.sh` before each commit, `--full` before each push; docs in the same PR; no deferred minor feedback; after a change to the describer or its tests, `tests/DatabaseTests/Postgres` also on `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`.
- `IEngineRenaming`, `IRenamingScope`, `FieldRule`, `NameClass` and the neutral steps of `Bundles/Renaming/` keep their shape; a change to them is an owner question sent to the manager first.  Nothing of SQL Server's part is touched; `EngineRenamings.For`'s `SqlServer` arm stays `null, // Built by 10.3.`.
- Every member of the engine's part never reads a database, the host or a file (the vocabulary is an embedded resource, read once), holds nothing of one bundle once that bundle is renamed (the part is stateless: `PostgresRenaming.Instance`), and writes no name of its own making: every replacement comes from the scope.
- Owner decisions (2026-11-04): no raw log in a bundle; a server error keeps only its SQLSTATE and the parameter it names, in a text the tool writes; never blank out a part that cannot be made safe, refuse instead; a user's name spelled as a keyword or a word of the engine's vocabulary stays verbatim.
- A switch over an enum names each member and ends in `_ => throw UnexpectedEnumValueException.Of(value)`.
- `captureVersion` stays `1`; no capture record changes.
- AGENTS.md: each bullet ≤ 80 words, each file ≤ 8 KB (root ≤ 16 KB), a maintenance footer where files are named.  `src/SqlSource.Tool/AGENTS.md`, `src/SqlSource.Tool/Describing/AGENTS.md` and `tests/AGENTS.md` are full: put rules in `src/SqlSource.Tool/Bundles/Renaming/Postgres/AGENTS.md` (new), `src/SqlSource.Tool/Describing/Postgres/AGENTS.md` (7.6 KB: one short bullet at most) or `Walk/AGENTS.md`.
- User documentation does not change: this sub-phase has no command.
- No `Spike10` code is copied whole; `CrudeRenamerPostgres.cs`, `CrudeRenamingTests.cs`, `RealisticCaptureTests.cs` and `VocabularyTests.cs` at `f51ac107` are references (`git show f51ac107:<path>`).

## Where merged code differs from the spec's assumptions

- **Step 8 is 10.1's proof**: a request the pipeline does not make from the renamed query is the replay's capture miss, `RefusalStep.RequestSql`.  PostgreSQL's part derives only what its pipeline makes by its own rule: the order of `relations` and `partitions` (Task 6).  PostgreSQL makes no copy of a parameter, so `CopyOf` is always null.
- **Step 1 for the plan is an allowlist**: the keys `PlanNode.Read` reads, which Task 1 names in one place, are kept and every other key is dropped, rather than the spike's 28 keys, so that a key no corpus held is dropped too.  `Grouping Sets`, read for its presence alone, keeps its elements with every member of each dropped.
- **`<alias>_<n>` is decided when the plan is renamed, not registered**: `Register` cannot see whether `<alias>_<n>` occurs in the query's SQL, but by `RenameOwn` the query is renamed, so a spelling the SQL or a catalog holds is mapped.  Its token check goes through `WordsOf`, which gives the stem of `<stem>_<n>` when the stem is shaped as a replacement: the check then asks that the stem be a replacement, and the planner's `_<n>` is its fixed text.
- **The vocabulary is a committed, embedded list**, made by an explicit container test on the floor and default images and merged as a union (finding 3), with an always-run test on those two images that the list holds every word of the image.  Root `AGENTS.md` rule 5 says "Nothing is recorded on a floor image": it gains the vocabulary as the one exception.
- **A column of a `pg_catalog` relation is renamed**: `IsEngineRow` is given the column's row, which does not name its table's schema.  The name is not the user's and is renamed alike everywhere, so no decision changes; it is recorded in the architecture page.
- **`Fold` lower-cases ASCII alone**, as PostgreSQL does for a UTF-8 database, unlike `FakeEngineRenaming`'s `char.ToLowerInvariant`.
- **The realistic test renames all 59 cases**, the three the describer refuses included, as bundles of a failure: they are the first captures that hold a server's error.

## Review Focus

1. **A user's table named as an engine word that is partitioned or inherited and scanned without an alias** (`<engineword>_<n>` in the plan): expected a refusal of step 12 naming the token, never a leak and never a replay that differs silently.  Test in Task 5.
2. **A `search_path` with a quoted element holding a comma or a space** (`"my, schema", public`): each element read as `PostgresSearchPath` reads it and renamed whole, the comma kept inside the quotes.  Test in Task 4.
3. **An error's message that holds the user's text** (`relation "secret_table" does not exist`, a `22P02` with a literal): the bundle holds only the tool's text; nothing of the message survives in any file.  Test in Task 4.
4. **A `prepare` error whose position is inside the type list** of a renamed declared type (`-- param: @x my_domain`, `42704`): the renamed position falls in the same item, so the replay gives `SQLSRC228` naming the same parameter.  Test in Task 6.
5. **A plan holding a key no corpus has** (a future PostgreSQL's) whose value holds a name: the key is dropped, and the renamed bundle still replays.  Test in Task 5.

---

### Task 1: The plan's keys in one place

**Files:**
- Create: `src/SqlSource.Tool/Describing/Postgres/Walk/PlanKeys.cs`
- Modify: `src/SqlSource.Tool/Describing/Postgres/Walk/PlanNode.cs` (every key literal → a `PlanKeys` constant)
- Modify: `src/SqlSource.Tool/Describing/Postgres/Walk/AGENTS.md` (one bullet: a key the walk reads is a constant of `PlanKeys`, and gets its kind in `Bundles/Renaming/Postgres/PostgresPlanRenamer.cs`, or a bundle drops it)
- Test: `tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanKeysTests.cs`

**Interfaces:**
- Produces: `internal static class PlanKeys` with `const string` members `Plan`, `Plans`, `NodeType` ("Node Type"), `JoinType`, `ParentRelationship`, `Output`, `Alias`, `RelationName`, `Schema`, `SubplanName`, `CteName` ("CTE Name"), `Command`, `Strategy`, `SubplansRemoved`, `GroupingSets`, `GroupKey`, `Filter`; and `public static IReadOnlyList<string> All` (every constant, by reflection or listed).

- [ ] **Step 1:** Test `All_IsEveryConstant` (reflection over the `const` fields equals `All`) and `PlanNode_ReadsNoKeyLiteral` (the source text of `PlanNode.cs`, found through the repository root as other source tests do, holds no `"` + one of `All`'s values + `"`). Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "*PlanKeysTests"`; expected FAIL (no type).
- [ ] **Step 2:** Add `PlanKeys`, replace the literals of `PlanNode.cs`; behaviour unchanged.
- [ ] **Step 3:** Run the test class and `--filter-namespace "*Postgres*"` of the tool's tests; expected PASS.
- [ ] **Step 4:** Commit `refactor(tool): the keys the walk reads of a plan, in PlanKeys`.

### Task 2: The words: word rule, fold, vocabulary and the planner's names

**Files:**
- Create: `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresWords.cs`, `PostgresPlannerNames.cs`, `postgres-vocabulary.txt` (embedded)
- Modify: `src/SqlSource.Tool/SqlSource.Tool.csproj` (`<EmbeddedResource Include="Bundles/Renaming/Postgres/postgres-vocabulary.txt" LogicalName="SqlSource.Tool.Bundles.Renaming.Postgres.postgres-vocabulary.txt" />`)
- Create: `tests/DatabaseTests/Postgres/Renaming/VocabularyTests.cs`
- Modify: `tests/DatabaseTests/Postgres/README.md` (a section "The renaming's vocabulary": what it holds, the check, and the two commands that make it again), root `AGENTS.md` rule 5 (the PostgreSQL default bullet names the vocabulary, made by `VocabularyTests` on the floor and default images; the floor bullet's "Nothing is recorded on a floor image" gains "but its words in the renaming's vocabulary", written for both engines so 10.3 changes only its own bullet)
- Test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresWordsTests.cs`, `PostgresPlannerNamesTests.cs`

**Interfaces:**
- Produces:
  - `internal static class PostgresWords { bool IsWordStart(char); bool IsWordPart(char); string Fold(string spelling, bool quoted); bool IsEngineWord(string spelling, bool quoted); IReadOnlySet<string> Vocabulary { get; } IReadOnlySet<string> EngineSchemas { get; } }` — the word rule is `PostgresIdentifiers.IsWordStart`/`IsWordPart`; `Fold` keeps a quoted spelling and lower-cases `A`-`Z` alone in an unquoted one; `IsEngineWord` folds, then is true when the text is in `Vocabulary`, in `EngineSchemas` (`pg_catalog`, `information_schema`, `pg_toast`, `public`) or `PostgresPlannerNames.Is` says so.
  - `internal static class PostgresPlannerNames { bool Is(string folded); }` — true for `*SELECT*`, `*SELECT* <k>`, `*VALUES*`, `*VALUES*_<n>`, `*RESULT*`, `*ANTI*`, `*MERGE*`, `*GROUP*`, `column<n>`, `col<n>`, `w<n>`, `subplan`, `initplan`, `hashed`, `cte`, `unnamed_subquery`, `any_subquery`, `unnamed_join`, `excluded`, `worktable`, `old`, `new` (`<k>`, `<n>`: ASCII digits, one or more).  A form whose letters are upper case in the plan (`*SELECT* 1`, `*VALUES*`) is compared as the plan prints it, since such a name is quoted.
  - The vocabulary file: one folded word a line, sorted by ordinal, each once, after a header of `#` lines saying what it holds and how it is made; `PostgresWords` skips `#` lines and blank ones.  It holds `pg_get_keywords()`' words, `pg_catalog`'s `proname`s, `oprname`s and `typname`s, `information_schema`'s `typname`s, and the system columns of `pg_class` (`attnum < 0`), each folded.

- [ ] **Step 1:** Tests in `PostgresWordsTests`: `Fold("Users", false) == "users"`, `Fold("Users", true) == "Users"`, `Fold("ÉTÉ", false) == "ÉTÉ"` (non-ASCII kept); `IsEngineWord` true for `SELECT`, `name`, `count`, `int4`, `_text`, `ctid`, `xmax`, `pg_catalog`, `PUBLIC`, `EXCLUDED`, `json_build_object`, `"count"` quoted, `*SELECT* 2` quoted, `*VALUES*_2` quoted, `column3`, `w1`; false for `users`, `"Count"` quoted, `account_status`, `*SELECT*x`; `Vocabulary.Count > 3000`.  `PostgresPlannerNamesTests` one `[Theory]` of each form above and of near misses (`*SELECT* `, `col`, `w`, `subplans`).  Run: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-namespace "*Renaming.Postgres"`; expected FAIL.
- [ ] **Step 2:** `VocabularyTests` in collection `UsesPostgres`: `Vocabulary_OfThisImage_IsInTheCommittedList` — skips unless the image is the floor's or the default's (`postgres:14.24` and `DefaultImages.Postgres`), reads the image's words with the five queries above and asserts every one, folded, is in `PostgresWords.Vocabulary`, naming the missing ones and the command that adds them; and `[Fact(Explicit = true)] Record_TheImagesWords_MergesThemIntoTheList` — reads the committed file through `RepositoryRoot.Path`, adds the image's words, writes it back sorted with its header.  The queries qualify every catalog with `pg_catalog.`.
- [ ] **Step 3:** Add the code and the csproj line; make the list: run the explicit test with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`, then on the default image (`dotnet test --project tests/DatabaseTests/Postgres -- --filter-class "SqlSource.DatabaseTests.Postgres.Renaming.VocabularyTests" --explicit only`).  Expected: about 3,826 words plus the 74 operators and the system columns.
- [ ] **Step 4:** Run the tool's tests of the namespace and `VocabularyTests` (not explicit) on both images; expected PASS.
- [ ] **Step 5:** Write the README section and the two bullets of rule 5; run `dotnet test --project tests/SqlSource.Tests -- --filter-namespace "*Documentation*"` (AGENTS size and link tests); expected PASS.
- [ ] **Step 6:** Commit `feat(tool): PostgreSQL's words for the renaming, with its committed vocabulary`.

### Task 3: The rule of every member, and the engine's rows

**Files:**
- Create: `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresFieldRules.cs`
- Test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresFieldRulesTests.cs`

**Interfaces:**
- Consumes: `PostgresCallNames`, the records of `PostgresCalls.cs`, `BundleFiles.Expected`.
- Produces: `internal static class PostgresFieldRules { FieldRule? RuleOf(string method, string path); bool IsEngineRow(string method, string path, JsonObject row); }`.

The table, by `"<method> <path>"` (an `error` record's rows apply under every method's `answer.error.`; `relations`' answer is `tables`' record):

| Rule | Paths |
|----|----|
| `Fixed` | `open answer.serverVersion`, `open answer.driverVersion`, `open answer.settings[].name`, `prepare request.statement`, `columns request.statement`, `explain request.statement`, `explain request.settings[]`, `deallocate request.statement` |
| `Closed` | `answer.error.sqlState`, `tables`/`relations` `answer.tables[].relkind`, `…columns[].identity`, `…columns[].generated`, `types answer.rows[].typtype`, `types answer.rows[].typcategory` |
| `Sql` | `prepare request.sql`, `check request.sql`, `check answer.sent.text`, `overloads request.operators[]`, `overloads answer.operators[]` |
| `TypeSpelling` | `prepare request.types[]`, `types answer.rows[].formatType`, `spellings answer.rows[].formatType`, `check request.parameters[].dataTypeName`, `check answer.columns[].dataTypeName` |
| `Label` | `types answer.rows[].enumLabels[]` |
| `Parameter` | `check request.parameters[].name`, `check answer.sent.parameters[]`, `check answer.driverError.parameter` |
| `Name(Column)` | `columns answer.columns[].name`, `tables`/`relations` `answer.tables[].columns[].name`, `check answer.columns[].name` |
| `Name(Table)` | `tables`/`relations` `answer.tables[].name`, `relations`/`partitions` `request.relations[].name`, `partitions answer.rows[].name` |
| `Name(Schema)` | `tables`/`relations` `answer.tables[].schema`, `relations`/`partitions` `request.relations[].schema`, `partitions answer.rows[].schema`, `types answer.rows[].nspname` |
| `Name(Type)` | `types answer.rows[].typname` |
| `Name(Function)` | `overloads request.functions[]`, `overloads answer.functions[]` |
| `Own` | `open answer.settings[].value`, `answer.error.message`, `check answer.driverError.message`, `explain answer.plan` |
| `Drop` | `answer.error.detail`, `answer.error.hint`, `answer.error.position` of every method but `prepare`, `deallocate answer.error`, `partitions answer.error`, `relations answer.error` |
| Entry (`expected.json`), under `parameters[].type.` and `columns[].type.` after any run of `element.`, `base.`, `subtype.` | `name`, `bindName` `TypeSpelling`; `kind` `Closed`; `schema` `Name(Schema)`; `internalName` `Name(Type)`; `labels[]` `Label` |

Every other path is null (a refusal of a string, nothing for a number).  `IsEngineRow` is true when the row's `nspname`, else its `schema`, is a string that is `pg_catalog` or `information_schema`.

- [ ] **Step 1:** Tests: `RuleOf_EveryStringMemberOfEveryCall_HasARule` — the test maps each `PostgresCallNames` constant to its request and answer record types (asserting the map's keys are all the constants, by reflection, so a new call fails here), walks each record's properties as `CaptureJson.Options` names them (camelCase, `[]` for a list, into nested records), and asserts `RuleOf` is not null for every string leaf; `RuleOf_EveryRuleOfTheTable_IsOfAMemberThatExists` (no stale path); `RuleOf_ErrorDetail_IsDrop` for each method; `RuleOf_PrepareErrorPosition_IsNotDrop` and `RuleOf_CheckErrorPosition_IsDrop`; `RuleOf_EntryTypeAtAnyDepth` for `columns[].type.element.base.internalName` → `Name(Type)`; `RuleOf_UnknownMember_IsNull`; `IsEngineRow` true for `{"nspname":"pg_catalog"}` and `{"schema":"information_schema"}`, false for `{"schema":"public"}` and for `{"schema":null}`.  Run the class; expected FAIL.
- [ ] **Step 2:** Implement the table as one dictionary built once; the entry rule by stripping the record prefix and the nested type members.
- [ ] **Step 3:** Run the class; expected PASS.
- [ ] **Step 4:** Commit `feat(tool): the rule of every member of PostgreSQL's calls for the renaming`.

### Task 4: The engine's own strings: settings and errors

**Files:**
- Create: `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresOwnStrings.cs`
- Test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresOwnStringsTests.cs` (with a small fake `IRenamingScope` at `tests/SqlSource.Tool.Tests/FakeRenamingScope.cs`, test root, that maps a name to `<class prefix><spelling>` and records refusals)

**Interfaces:**
- Consumes: `PostgresSearchPath` (its reading of an element), `PostgresLive`'s setting names, `ConnectionErrors.MultiplexingRefused`, `ConnectionErrors.UnreadableTypes`, `PostgresLive.StatementNotListed`, `PostgresLive.TransactionRefused`, `PostgresIdentifiers.ParameterNumberAt`.
- Produces: `internal static class PostgresOwnStrings { string Setting(RenamingField field, IRenamingScope scope); IEnumerable<string> WordsOfSetting(RenamingField field); string Message(RenamingField field, IRenamingScope scope); IEnumerable<string> WordsOfMessage(RenamingField field); }`.

Rules: a setting's name is read from `field.Row["name"]`.  `search_path`'s value: each element as `PostgresSearchPath` reads it, `$user`, `public` and `pg_catalog` kept, any other through `scope.Name(element, quoted, NameClass.Schema)`, a quoted element written back quoted with `"` doubled, joined by `", "`.  `plan_cache_mode` and `standard_conforming_strings` keep their value.  Any other setting's name is refused (`RefusalStep.Token`, at the place, the token the name), since only those three are read.  A message: one of the four constants is kept; with `sqlState` `42P18` or `42P08` and a `$n` that `ParameterNumberAt` reads, it is `parameter $<n>`; any other is the empty string; `check`'s `driverError.message` is always the empty string.  `WordsOfMessage` gives nothing for a value that is one of these forms, and the value whole for any other, so that a message the renaming did not write fails the check.  `WordsOfSetting` gives each renamed `search_path` element (a quoted one as `"..."`) and nothing for the other two settings' values.

- [ ] **Step 1:** Tests: `Setting_SearchPath_RenamesTheUsersSchemas` (`"$user", public, app, "My Schema"` → `"$user", public, s_app, "s_My Schema"` with the fake scope); `Setting_SearchPathWithAQuotedComma_IsOneElement` (Review Focus 2); `Setting_PlanCacheMode_IsKept`; `Setting_OtherName_IsRefused`; `Message_42P18_IsTheParameterItNames` (`could not determine data type of parameter $2` → `parameter $2`); `Message_42P18WithoutDollar_IsEmpty`; `Message_UsersText_IsEmpty` (`42P01`, `relation "secret_table" does not exist` → empty; Review Focus 3); `Message_StatementNotListed_IsKept` (each of the four constants, any SQLSTATE); `WordsOfMessage_OfAnUnwrittenText_IsTheText`; `WordsOfSetting_OfSearchPath_IsEachElement`.  Run; expected FAIL.
- [ ] **Step 2:** Implement.
- [ ] **Step 3:** Run; expected PASS.
- [ ] **Step 4:** Commit `feat(tool): PostgreSQL's settings and errors renamed to the tool's own text`.

### Task 5: The plan

**Files:**
- Create: `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresPlanRenamer.cs`
- Test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresPlanRenamerTests.cs` (plans built with `tests/SqlSource.Tool.Tests/PostgresPlans.cs` where it serves)

**Interfaces:**
- Consumes: `PlanKeys` (Task 1), `PlanTokens`, `PostgresWords`.
- Produces: `internal static class PostgresPlanRenamer { void Register(string plan, IRenamingScope scope); string Rename(string plan, IRenamingScope scope); IEnumerable<string> WordsOf(string renamed); }`.

The kind of each key of `PlanKeys.All`, a table in this file: **structure** `Plan`, `Plans`; **fixed** `Node Type`, `Join Type`, `Parent Relationship`, `Strategy`, `Command`; **count** `Subplans Removed`; **presence** `Grouping Sets` (kept, each element's members dropped); **name** `Alias` (`NameClass.Alias`), `Relation Name` (`Table`), `Schema` (`Schema`), `CTE Name` (`Alias`), each one raw name, `quoted: true`; **subplan** `Subplan Name`: `CTE <name>` keeps `CTE ` and renames the name as `CTE Name` is, any other through the expression rule; **expression** `Output`, `Filter`, `Group Key`: through `PlanTokens`, a `Word` by `scope.Name(word, false, Function if "(" follows else Alias)`, a `QuotedName` by its unquoted spelling, `quoted: true`, written back quoted, a `String` as `'` + `scope.Literal(content)` + `'`, a `Number` and punctuation kept, an `Other` kept when it holds no letter, digit or `_` and refused otherwise.  A key of no kind is dropped, at any depth, and so is any member of the root array's element but `Plan`.

`<alias>_<n>`: when renaming (not registering) a name or word `<a>_<n>` (`n` ASCII digits) that `scope.IsMapped` does not hold: when `<a>` is mapped or an engine word, the result is `scope.Name(<a>, quoted, cls)` + `_<n>`; otherwise it is a name of its own.  `Register` registers every name key's value but an `Alias` of the form `<a>_<n>`.  A string of an expression key that `PlanTokens.Read` cannot read, and a plan that is no JSON array of objects with `Plan`, are refused with `RefusalStep.PlanString` at the place, the token the string.  `Rename` writes the JSON indented, with `\n` line ends, through `CaptureJson.Options`' encoder.

`WordsOf`: for each kept string by its key's kind: fixed and count nothing; a name as `"<name>"` (quotes doubled); `CTE <name>` as `"<name>"` and nothing for `CTE`; an expression's tokens, a word as itself, a quoted name and a string as written, and every other token one character at a time.  A word or quoted name `<stem>_<n>` whose stem is shaped as a replacement (a lower-case letter, `_`, eight or more lower-case hex digits) gives the stem alone (the planner's `_<n>` being its text); any other is given whole.

- [ ] **Step 1:** Tests: `Rename_DropsEveryKeyNotRead` (a plan with `Total Cost`, `Index Cond`, `Sort Key` and a key `Future Key` holding `secret_name`: none in the result; Review Focus 5); `Rename_NameKeys_AreOneRawName` (`Alias` `*SELECT* 1` kept, `Relation Name` `Order Items` → one table name); `Rename_OutputQuotedAndBare_ShareOneName` (`"Order".user_id` and Relation Name `Order`); `Rename_Literal_IsTheMappingsLiteral` (`('Active'::text)`); `Rename_CteSubplanName_KeepsCte`; `Rename_SubPlanAndInitPlanNames_AreKept` (`SubPlan 1`, `InitPlan 1 (returns $1)`, `(InitPlan 1).col1`, `hashed SubPlan 2`); `Rename_ChildAlias_IsRenamedThroughItsAlias` (`orders_1` with `orders` mapped → `t_orders_1` with the fake scope); `Rename_ChildAliasHeldBySql_IsItsOwnName`; `Rename_GroupingSets_KeepsItsPresence`; `Rename_UnreadableOutput_IsRefusedAtThePlan` (`(a.b` → `RefusalStep.PlanString`, token `(a.b`); `Rename_OtherTokenWithALetter_IsRefused`; `WordsOf_GivesEveryNameWordAndLiteral`; `WordsOf_ChildOfAReplacement_IsTheStem`; `WordsOf_ChildOfAnEngineWord_IsWhole` (`name_1` given whole, which the check refuses: Review Focus 1).  Run; expected FAIL.
- [ ] **Step 2:** Implement.
- [ ] **Step 3:** Run; expected PASS.
- [ ] **Step 4:** Commit `feat(tool): PostgreSQL's plan renamed key by key through PlanTokens`.

### Task 6: What the pipeline derives

**Files:**
- Create: `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresDerivations.cs`
- Test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresDerivationsTests.cs`

**Interfaces:**
- Consumes: `PlanRelations.InOrder`, `ParameterPlaceholders.HeadLength`, `ParameterPlaceholders.TypeAt`, `RenamedText.MapPosition`, `IRenamingScope.Sql`.
- Produces: `internal static class PostgresDerivations { void Derive(RenamingCall call, IRenamingScope scope); }`.

Rules: `relations` and `partitions`: `Renamed.request.relations` is put in the order of `PlanRelations.InOrder` of its renamed schemas and names.  `prepare` with an `answer.error.position` `p` (code points from one in `PrepareText(statement, types, sql)` of `Original.request`): when `p` is after the original head, the renamed position is the renamed head's length plus `MapPosition(p - head)` of `scope.Sql(original sql, NameClass.Alias)`; when it is inside item `k` of the type list, it is the renamed item `k`'s start plus `MapPosition` of the offset within the original item through `scope.Sql(original type, NameClass.Type)`, clamped to the renamed item; before the list, it is kept.  A position the mapping cannot place is refused (`RefusalStep.Token`, at `answer.error.position`, no token).  Every other call is left alone.

- [ ] **Step 1:** Tests: `Derive_Relations_AreInTheirRenamedOrder` (two relations whose renamed names swap their order); `Derive_Partitions_AreInTheirRenamedOrder`; `Derive_PrepareErrorInTheSql_MapsThePosition` (`p` at a name after a renamed longer name); `Derive_PrepareErrorInTheTypeList_StaysInTheSameItem` (Review Focus 4: two declared types, `p` inside the second, the first renamed longer; `TypeAt` of the renamed position is 1); `Derive_PrepareErrorWithoutPosition_IsLeftAlone`.  Run; expected FAIL.
- [ ] **Step 2:** Implement.
- [ ] **Step 3:** Run; expected PASS.
- [ ] **Step 4:** Commit `feat(tool): the order of relations and a prepare error's position derived after renaming`.

### Task 7: PostgreSQL's part, wired in

**Files:**
- Create: `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresRenaming.cs`, `src/SqlSource.Tool/Bundles/Renaming/Postgres/AGENTS.md`
- Modify: `src/SqlSource.Tool/Bundles/Renaming/EngineRenamings.cs` (the `PostgreSql` arm only), `tests/SqlSource.Tool.Tests/Bundles/Renaming/EngineRenamingsTests.cs`
- Create: `tests/SqlSource.Tool.Testing/Bundles/RawBundles.cs`, `BundleLeaks.cs`, `Provenance.cs` (shared by the tool's tests and `tests/DatabaseTests/Postgres`)
- Test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresRenamingTests.cs`

**Interfaces:**
- Consumes: Tasks 2 to 6.
- Produces:
  - `internal sealed class PostgresRenaming : IEngineRenaming { static PostgresRenaming Instance; }` — `Dialect` `SqlDialect.PostgreSql`; word rule, `Fold`, `IsEngineWord` of `PostgresWords`; `RuleOf`, `IsEngineRow` of `PostgresFieldRules`; `CopyOf` null; `Register` calls `PostgresPlanRenamer.Register` for an `explain` call with a string plan; `RenameOwn` and `WordsOf` dispatch on `field.Method` and `field.Path` (`open` settings, `answer.error.message`, `check` driver error, `explain` plan) and throw `InvalidOperationException` for any other `Own` path; `Derive` is `PostgresDerivations.Derive`.
  - `RawBundles.OfFixture(FixtureCase fixture, string? failureId = null, string? failureStep = null) -> Bundle`: its files with a `bundle.json` of `Renamed: false`, the engine's and server's versions from the capture's `open`, `fixture` for tool, runtime and system, and an `entry` outcome, or the failure given; `RawBundles.Of(string query, DescribeCapture capture, string? expected, BundleOutcome outcome, IReadOnlyList<string> explainSettings) -> Bundle`.
  - `BundleLeaks.Of(Bundle renamed, IEnumerable<string> sources, Func<string, bool> isEngineWord) -> IReadOnlyList<string>`: every name (word of plain text, quoted name, parameter, `-- name:` value) and literal content of the sources, lexed as PostgreSQL, that `isEngineWord` does not keep, found in any file of the bundle, a name as a whole word compared case-insensitively, a literal of four characters or more as a substring; empty when none (after `f51ac107`'s `LeaksOf`).
  - `Provenance.Of(string expected) -> string`: the entry's `plan` and `tableMatch`, each column's `nullableSource` and each parameter's `typeSource`, one line each, for comparing two entries whose names differ.
  - `AGENTS.md` of the folder: rules only, each linking `bundles.md#renaming-for-postgresql` — a member the describer reads gets its rule in `PostgresFieldRules.cs` and a plan key its kind in `PostgresPlanRenamer.cs`; nothing here keeps a word through `IRenamingScope.Keep`; a message is only ever the tool's text; the vocabulary is made by `VocabularyTests`, never by hand; and the maintenance footer.

- [ ] **Step 1:** Tests: `EngineRenamings.For(PostgreSql)` is `PostgresRenaming.Instance` and `For(SqlServer)` null (the old test split in two); `RenameAsync_SelectDeclaredDomain_RenamesAndReplays` (the fixture `select-declared-domain` through `RawBundles.OfFixture`, `BundleRenamer.RenameAsync` with the describer `BundleDescribers.For` gives: success; `users`, `email_t`, `id`, `email` and `FindUserByEmail` in no file; `name` and `pg_catalog` kept).  Run; expected FAIL.
- [ ] **Step 2:** Implement `PostgresRenaming`, the arm, the helpers and the folder's `AGENTS.md`.
- [ ] **Step 3:** Run the tool's tests of `*Bundles*`; expected PASS.
- [ ] **Step 4:** Commit `feat(tool): PostgreSQL's part of the renaming, given by EngineRenamings`.

### Task 8: Fixture cases of a `VALUES` list and a scalar subquery

**Files:**
- Create: `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/select-values-list/query.sql`, `select-scalar-subquery/query.sql`, and their `capture.json` and `expected.json`, recorded
- Modify: any count or list of the PostgreSQL fixtures a test or a document holds (find them with `grep -rn "65" tests docs --include=*.md --include=*.cs` and the fixture names' lists)

`select-values-list`: a `SELECT` joining the fixtures' `users` to `(VALUES (1, 'a'), (2, 'b')) AS v(id, label)` so the plan prints `*VALUES*` and `column<n>`; `select-scalar-subquery`: a column `(SELECT max(...) FROM ... WHERE ... = u.id)` so the plan prints `SubPlan <k>` (or `InitPlan` for an uncorrelated one, a second column).  Each over `schema.sql`'s tables, with a parameter where natural.

- [ ] **Step 1:** Write the two `query.sql`; run `tools/record-fixtures.sh postgres` (Docker); read the diff: only the two new folders change.  Expected: each `capture.json`'s plan holds `*VALUES*` and `SubPlan`/`InitPlan`.
- [ ] **Step 2:** Run `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-namespace "*Fixtures*"` and `tests/DatabaseTests/Postgres`'s `FixtureCasesTests` on the default and the floor image; expected PASS (on the floor, as the recorded-major gate allows).
- [ ] **Step 3:** Commit `test(tool): fixture cases of a VALUES list and a scalar subquery`.

### Task 9: Every fixture renamed

**Files:**
- Test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/Postgres/PostgresFixtureRenamingTests.cs`, `PostgresRefusalTests.cs`, `PostgresClassificationTests.cs`

**Interfaces:**
- Consumes: Task 7's helpers, `FixtureCases`, `BundleDescribers.For`, `RenamingKey.Of`.

- [ ] **Step 1:** `PostgresFixtureRenamingTests`, a `[Theory]` over every PostgreSQL fixture: `Rename_Fixture_ReplaysWithTheOriginalsProvenance` (success; `Provenance.Of` of the renamed `expected.json` equals the original's); `Rename_Fixture_LeaksNothing` (`BundleLeaks.Of` with the fixtures' `schema.sql` and the case's `query.sql` is empty); `Rename_TwiceWithTwoKeys_GivesOtherNamesAndTheSameProvenance` (two keys: the mappings' replacements share none, provenance equal).  A fixture the renaming must refuse is listed in the test with why, and the list is expected empty; the PR description says what is on it.
- [ ] **Step 2:** `PostgresRefusalTests`, bundles built by hand from fixtures: `Rename_PlanStringThatDoesNotRead_IsRefusedAtThePlan` (a fixture's `Output` entry made `(u.id`: `RefusalStep.PlanString`, place `capture.json call <n> 'explain' answer.plan`); `Rename_UnknownStringMember_IsRefusedAtIt` (a member added to a `tables` row); `Rename_UnknownSetting_IsRefused`; `Rename_ErrorOfPrepare_ReplaysToTheSameFailure` (`BundleReplays.PrepareTypeNotDetermined()` made a raw bundle of a failure: success, `SQLSRC226` at `describe parameters`); `Rename_ErrorWithTheUsersText_KeepsNoneOfIt` (a `42P01` whose message names a table: the message is empty in the renamed capture, Review Focus 3); `Rename_PrepareErrorInTheTypeList_ReplaysToSQLSRC228` (Review Focus 4); `Rename_PartitionChildOfAnEngineWord_IsRefusedAtItsToken` (Review Focus 1: a plan alias `name_1` of a relation `name`).
- [ ] **Step 3:** `PostgresClassificationTests`, over every fixture: `PlanWord_HeldByNeitherQueryNorCatalog_IsKept` — every word of the raw plan's kept keys, by `PlanTokens`, that is no word of `query.sql` and no name of a catalog answer of the capture is kept verbatim in the renamed plan (the planner's names are in the list).
- [ ] **Step 4:** Run the three classes; expected PASS.  A failure is a bug of Tasks 2 to 7, fixed there with a test of its own.
- [ ] **Step 5:** Commit `test(tool): every PostgreSQL fixture renamed, replayed and searched`.

### Task 10: The realistic corpus renamed on a container

**Files:**
- Create: `tests/DatabaseTests/Postgres/Renaming/RealisticRenamingTests.cs`
- Modify: `tests/DatabaseTests/Postgres/README.md` (the test's line in its list of what the project tests), `tests/DatabaseTests/Postgres/AGENTS.md` only if a rule is needed

**Interfaces:**
- Consumes: `RealisticCases.Corpus.All`, `FixtureReplay.DescribeThroughAsync`, `CapturingExchange`, Task 7's helpers.

- [ ] **Step 1:** In collection `UsesPostgres`, a `[Theory]` over every realistic case that runs on the image's major: describe it through a `CapturingExchange` against the database `realistic`; build the raw bundle (`RawBundles.Of`, an entry, or for a refused case the failure's id and step); rename with `PostgresRenaming.Instance`; assert success, `Provenance.Of` equal for an entry, and `BundleLeaks.Of` empty over the realistic `schema.sql` and the case's `query.sql`.  `Rename_Intersect_KeepsThePlannersSetName`: for `intersect-accounts-with-orders-and-events`, on majors 14 to 17 the raw plan holds `*SELECT* ` and the renamed plan holds it verbatim.  `RenamedWords_AreNoWordOfTheServer`: every original of the mappings of the corpus is neither a word of `pg_get_keywords()` nor a `pg_catalog` function or type name of the running image.
- [ ] **Step 2:** Run `dotnet test --project tests/DatabaseTests/Postgres -- --filter-namespace "*Renaming*"` on the default image and with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`; expected PASS (59 cases on 18; the cases a major cannot run skip).
- [ ] **Step 3:** Commit `test(tool): the realistic corpus renamed and replayed on a container`.

### Task 11: The architecture

**Files:**
- Modify: `docs/architecture/bundles.md` (`## Not built yet` › `### Renaming for PostgreSQL` replaced by a section `### Renaming for PostgreSQL` under `## The engine's contract`, or a `##` of its own; SQL Server's placeholder untouched), `docs/architecture/postgres-describer.md` ("The calls and their records": the members each call must keep while the pipeline reads them, and that a new member or plan key gets its rule), `src/SqlSource.Tool/Describing/Postgres/AGENTS.md` (one short bullet: a member the describer starts to read gets its rule in `Bundles/Renaming/Postgres/PostgresFieldRules.cs`, linking the page), `tests/README.md` if it lists the bundle or renaming tests

The section says, member by member of `IEngineRenaming`, what PostgreSQL's part answers and why: the word rule and the ASCII fold; the vocabulary, its sources, the images and `VocabularyTests`; the planner's names; the field table (pointing at `PostgresFieldRules.cs`, not repeating it); the engine's rows and the limit of a `pg_catalog` relation's column; the settings and the messages; the plan's kinds, `CTE <name>`, `<alias>_<n>` and its check; the two derivations; `CopyOf` null; and the tests that prove it.

- [ ] **Step 1:** Write the docs; run `dotnet test --project tests/SqlSource.Tests -- --filter-namespace "*Documentation*"`; expected PASS.
- [ ] **Step 2:** Commit `docs: renaming for PostgreSQL in bundles.md and the describer's page`.

### After the tasks

- [ ] Run the formatter (`dotnet csharpier format .` or what `CONTRIBUTING.md` names) and `./pre-commit-validation.sh --full`; then `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/DatabaseTests/Postgres`.
- [ ] Write `10-2-notes.md` in the shared notes folder: the folder and types, the vocabulary's test and root rule 5's wording (10.3 adds its own bullet), `FakeRenamingScope`, the shared helpers in `tests/SqlSource.Tool.Testing/Bundles/`, the `<stem>_<n>` rule of `WordsOf`, any refusal list.
