# Query generation 10.3, renaming for SQL Server - implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** SQL Server's part of the bundle renaming, `IEngineRenaming` for `DatabaseEngine.SqlServer`, so that `BundleRenamer.RenameAsync` renames every SQL Server fixture and realistic case into a bundle that passes the token check, replays to its renamed entry or failure, keeps the original's provenance and holds no name or literal of the user's.  No command: `diagnose` is 10.4's, and a refusal is the `RenamingRefusal` value 10.4 reports.

**Architecture:** One folder, `src/SqlSource.Tool/Bundles/Renaming/SqlServer/`, holds the engine part: the committed vocabulary, the table of every member of the seven calls and of the entry's type record, and the renaming of an error's message.  The engine-neutral steps of 10.1 do everything else; `EngineRenamings.For(DatabaseEngine.SqlServer)` gives the part.  Tests rename every fixture with no database, rename hand-built error bundles, classify every word of both SQL Server corpora, and, in `tests/DatabaseTests/SqlServer`, capture each realistic case on the server, rename it and replay it.

**Tech Stack:** C# / .NET 10, xUnit v3, Shouldly, Testcontainers SQL Server (`tests/DatabaseTests/TestHarness`).

**Spec:** `docs/superpowers/specs/2026-11-04-query-generation-phase-10-bug-reports-design.md` ("Renaming", "10.3 Renaming for SQL Server", "Testing"), its spike `docs/superpowers/specs/2026-11-05-query-generation-phase-10-spike.md` (findings 1, 3, 4, 5), and the contract `docs/architecture/bundles.md` ("The engine's contract", "Rules found while building").

## Where merged code differs from the spec

- **Case folding.**  The design compares SQL Server names ignoring case, and by ordinal in a case-sensitive database.  `IEngineRenaming.Fold` is given no bundle, so it cannot read the collation, and the pipeline compares the user's names both ordinally (`SqlServerTableMatcher`, `ColumnProofs.AreTheColumns`) and ignoring case (CTE names, parameters).  The plan folds ignoring case always, as the spike did; a query whose names differ from the catalog's only by case where the tool compares them ordinally is refused by the proof.  Sent to the owner as Q1; Task 4's test pins the refusal.
- **A copy's separator.**  `ParameterCopies` makes `<name><one or more _><n>`, as many `_` as keep it apart from the names the text holds.  A renamed name is a prefix and hex digits, so in the renamed query one `_` always suffices: `CopyOf` gives the suffix `_<n>`, which is what the pipeline makes from the renamed query.
- **Errors.**  No SQL Server text is compared (spike finding 5), so every message is replaced by the `@name`s it names, renamed, a copy written as its parameter, which `SqlServerErrors.NamedIn` reads the same way; a `ConnectionErrors` text becomes empty.  There are no positions.
- **Derive and Register do nothing** for SQL Server: it has no plan and asks no list in name order, `AS [n]` is kept by `IsEngineWord`, and the copies come through `CopyOf`.

## Global Constraints

- Every member the pipeline reads is kept (spike finding 1: for SQL Server every member is kept); a string member with no rule is a refusal, never kept by default.
- A user's name spelled as a keyword or a word of the engine's vocabulary stays verbatim; the vocabulary is the union of the floor image `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04` and the default image `DefaultImages.SqlServer`.
- Vocabulary sources (design step 2): the reserved (185), ODBC (235) and future (273, with `REGR_R2`) keywords of Microsoft's list (`docs/superpowers/specs/2026-11-05-query-generation-phase-10-spike/mssql-keywords.tsv`); the non-reserved words `APPLY`, `INSERTED`, `DELETED`, `MATCHED`, `SOURCE`, `TARGET`, `OFFSET`, `PRECEDING`, `UNBOUNDED`, `TIES`, `RECOMPILE`, `SYSTEM_TIME`, `ABSENT`, `OPENJSON`; the pseudo-columns `$action`, `$identity`, `$rowguid`, `$node_id`, `$edge_id`, `$from_id`, `$to_id` and `$partition`; the documented functions either image knows (`SELECT F()` gives no `195`, or `SELECT * FROM F()` no `208`); `sys.types` with `is_user_defined = 0` of either image; the schemas `dbo`, `sys`, `INFORMATION_SCHEMA`; the system databases `master`, `msdb`, `model`, `tempdb`, `mssqlsystemresource`; the global variables `@@ROWCOUNT` and the like; and browse mode 2's hidden row name `ROWSTAT\u0000`.
- The word rule is `QuoteReader.IsNameCharacterOfSqlServer`; names fold ignoring case (`ToUpperInvariant`, character by character).
- No interface change: `IEngineRenaming`, `IRenamingScope` and the neutral steps stay as 10.1 merged them.  Touch nothing of PostgreSQL's part; `EngineRenamings.For` changes its SQL Server line alone.
- Rules of a folder go in `src/SqlSource.Tool/Bundles/Renaming/SqlServer/AGENTS.md`; `src/SqlSource.Tool/AGENTS.md` and `src/SqlSource.Tool/Describing/AGENTS.md` are full.
- Every implementer runs the tests of what they changed in the foreground; the full `./pre-commit-validation.sh` runs once the change is complete.

## Review Focus

1. **A parameter spelled in two cases, `@Id` and `@id`**, is one parameter to SQL Server: its copies must be found through `CopyOf` ignoring case (Task 2's `CopyOf` tests).
2. **An error that names a copy or a global variable**, `'@label_1'` or `@@ROWCOUNT`: the copy is written as its parameter, the variable dropped, and `SQLSRC226` still names the same parameter (Task 4).
3. **A system CLR type's assembly-qualified name of another version**, `Version=10.0.0.0`, is the engine's; a user's CLR type's is renamed whole (Task 2).
4. **A bracketed name of digits, `[1]`, is the tool's alias; `[1a]` and `[2 x]` are the user's** (Task 2).
5. **A temporary table `#t`, a global one `##t`, and `$action` in a `MERGE`'s `OUTPUT`**: the name after `#` is renamed, `$action` kept (Task 2's word tests, Task 3's `merge-output` fixture).

---

### Task 1: The vocabulary

**Files:**
- Create: `src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerVocabulary.cs`
- Create: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerVocabularyTests.cs`
- Create: `tests/DatabaseTests/SqlServer/Renaming/VocabularyTests.cs`
- Create: `tests/DatabaseTests/SqlServer/Renaming/DocumentedFunctions.cs`

**Interfaces:**
- Produces: `internal static class SqlServerVocabulary` with `public static bool Contains(string spelling)` (folds as Task 2's `Fold` does, upper-invariant by character, then looks up an ordinal set of the folded words), and the lists it is made of, each `public static IReadOnlyList<string>`: `Reserved`, `Odbc`, `Future`, `NonReserved`, `PseudoColumns`, `GlobalVariables`, `Functions`, `Types`, `Schemas`, `SystemDatabases`, and `public const string HiddenRow = "ROWSTAT\u0000"`.
- Produces (test project): `internal static class DocumentedFunctions { public static IReadOnlyList<string> Names }`, the 318 names of `mssql-functions.tsv`.

- [ ] **Step 1: Write the container test** `VocabularyTests` in `[Collection(UsesSqlServer.Name)]`:
  - `SystemTypes_OfTheImage_AreAllInTheVocabulary`: every `name` of `SELECT name FROM sys.types WHERE is_user_defined = 0` satisfies `SqlServerVocabulary.Contains`; the assertion message lists the missing ones.
  - `DocumentedFunctions_TheImageKnows_AreAllInTheVocabulary`: each name of `DocumentedFunctions.Names` probed with `SELECT TOP (1) error_number FROM sys.dm_exec_describe_first_result_set(@tsql, NULL, 0)`, `@tsql` = `SELECT F()`; `195` is unknown unless `SELECT * FROM F()` answers other than `208`; every known name is in the vocabulary, the message listing the missing ones.
- [ ] **Step 2: Write `SqlServerVocabulary`** with the lists of the Global Constraints, the keywords copied from `mssql-keywords.tsv` (plus `REGR_R2` in `Future`), `Functions` and `Types` left empty for now.
- [ ] **Step 3: Run the container test on the default image and on the floor image** (`SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`): `dotnet test --project tests/DatabaseTests/SqlServer -- --filter-class "SqlSource.DatabaseTests.SqlServer.Renaming.VocabularyTests"`.  Expected: FAIL, listing the missing types and functions.  Fill `Types` and `Functions` with the union of both runs' lists, sorted; run both again: PASS.  The spike counted 34 + 2 types and 297 functions on the default image.
- [ ] **Step 4: Write the unit tests** `SqlServerVocabularyTests`: `Reserved.Count == 185`, `Odbc.Count == 235`, `Future.Count == 273` and holds `REGR_R2`; `Contains("select")`, `Contains("SeLeCt")`, `Contains("$ACTION")`, `Contains("@@rowcount")`, `Contains("Msdb")`, `Contains(SqlServerVocabulary.HiddenRow)`, `Contains("geography")`; `!Contains("users")`, `!Contains("ROWSTAT")`; no list holds a word twice, folded.
- [ ] **Step 5: Run** `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Bundles.Renaming.SqlServer.SqlServerVocabularyTests"`: PASS.
- [ ] **Step 6: Commit** `feat(tool): SQL Server's vocabulary for the renaming, checked on each image`.

### Task 2: The engine part

**Files:**
- Create: `src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerRenaming.cs`
- Create: `src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerFields.cs`
- Create: `src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerMessages.cs`
- Modify: `src/SqlSource.Tool/Bundles/Renaming/EngineRenamings.cs` (the SQL Server line alone)
- Modify: `tests/SqlSource.Tool.Tests/Bundles/Renaming/EngineRenamingsTests.cs`
- Test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerRenamingTests.cs`, `SqlServerFieldsTests.cs`, `SqlServerMessagesTests.cs`

**Interfaces:**
- Consumes: `SqlServerVocabulary.Contains` (Task 1).
- Produces: `internal sealed class SqlServerRenaming : IEngineRenaming` with `public static SqlServerRenaming Instance`; `internal static class SqlServerFields { public static FieldRule? RuleOf(string method, string path) }`; `internal static class SqlServerMessages { public static string Rename(string message, IRenamingScope scope, Func<string, bool> isEngineWord); public static IEnumerable<string> WordsOf(string renamed) }`.  `EngineRenamings.For(DatabaseEngine.SqlServer)` returns `SqlServerRenaming.Instance`.

The members of `SqlServerRenaming`:

| Member | Answer |
|----|----|
| `Dialect` | `SqlDialect.SqlServer` |
| `IsWordStart`, `IsWordPart` | `QuoteReader.IsNameCharacterOfSqlServer` |
| `Fold` | upper-invariant by character, `quoted` ignored |
| `IsEngineWord` | `SqlServerVocabulary.Contains`; a spelling of ASCII digits alone (the tool's `AS [n]`); a system CLR type's assembly-qualified name, `Microsoft.SqlServer.Types.Sql(Geometry\|Geography\|HierarchyId), Microsoft.SqlServer.Types, Version=<digits and dots>, Culture=neutral, PublicKeyToken=89845dcd8080cc91`, compared folded |
| `RuleOf` | `SqlServerFields.RuleOf` |
| `IsEngineRow` | a row at `answer.rows[]` of `columns`, `operands` or `table` whose `sourceSchema` folds to `SYS` or `INFORMATION_SCHEMA`, or whose `sourceDatabase` is a system database; `table`'s `request` whose `schema` is `sys` or `INFORMATION_SCHEMA`; nothing else |
| `CopyOf` | the name as `<prefix><one or more _><ASCII digits>` with `<prefix>` in `queryParameters` (whose comparer ignores case): `(prefix, "_" + digits)`; otherwise null |
| `Register`, `Derive` | nothing |
| `RenameOwn` | `SqlServerMessages.Rename(field.Value, scope, s => IsEngineWord(s, false))` |
| `WordsOf` | `SqlServerMessages.WordsOf(field.Value)` |

`SqlServerFields.RuleOf`, by `"<method> <path>"`; every other path is null:

| Paths | Rule |
|----|----|
| `open answer.productVersion`, `answer.driverVersion`, `answer.settings[].name`, `answer.settings[].value` | `Fixed` |
| `open answer.database`; `answer.rows[].sourceServer`, `answer.rows[].sourceDatabase` of `columns`, `operands`, `table` | `Name(Database)` |
| `request.sql` of `parameters`, `probe`, `declared`, `columns`, `operands` | `Sql` |
| `request.params` of the same five | `TypeSpelling` |
| `table request.schema`, `table request.table` | `Name(Schema)`, `Name(Table)` |
| `parameters answer.rows[].name` | `Parameter` |
| `parameters answer.rows[].suggestedSystemTypeName`; `answer.rows[].systemTypeName` of `declared`, `columns`, `operands`, `table` | `TypeSpelling` |
| `…UserTypeSchema` / `userTypeSchema`, `…UserTypeName` / `userTypeName` of the same rows | `Name(Schema)`, `Name(Type)` |
| `…AssemblyQualifiedTypeName` / `assemblyQualifiedTypeName` of the same rows | `WholeName(Type)` |
| `answer.rows[].name`, `sourceSchema`, `sourceTable`, `sourceColumn` of `columns`, `operands`, `table` | `Name(Column)`, `Name(Schema)`, `Name(Table)`, `Name(Column)` |
| `answer.rows[].sourceType` of the same three | `Closed` |
| `answer.error.errors[].message` of every method; `answer.errors[].message` of `declared`, `columns`, `operands`, `table` | `Own` |
| `expected.json` paths under `parameters[].type.` and `columns[].type.`: `name`, `bindName` | `TypeSpelling` |
| the same: `userType.schema`, `userType.name`, `userType.assemblyQualifiedName` | `Name(Schema)`, `Name(Type)`, `WholeName(Type)` |

`SqlServerMessages.Rename` reads each `@name` of the message as `SqlServerErrors.NamedIn` does (the `@`, then `QuoteReader.IsNameCharacterOfSqlServer` characters), and writes, joined by one space: nothing for a name that starts with `@` (a global variable); `"@" + scope.Parameter(name)` when `scope.IsMapped(name, false)` or `isEngineWord(name)`; when the name is `<stem><_…><digits>` and the stem is mapped or an engine word, `"@" + scope.Parameter(stem)`; else `"@" + scope.Parameter(name)`.  `WordsOf` gives `"@"` and the name of each space-separated piece.

- [ ] **Step 1: Write the failing tests.**  `SqlServerRenamingTests`: each row of the members' table, among them `Fold("Users", false) == Fold("USERS", true)`; `IsEngineWord("1", true)`, `!IsEngineWord("1a", true)`, `!IsEngineWord("2 x", true)`; the hierarchyid name of `select-alias-and-clr` with `Version=10.0.0.0` is the engine's, `MyCompany.Money, MyCompany, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null` is not; `IsEngineRow` true for `sourceDatabase` `msdb` and `sourceSchema` `INFORMATION_SCHEMA`, false for `dbo` in `fixtures` and for a `parameters` row; `CopyOf("before_1", {before}) == ("before", "_1")`, `CopyOf("Before__12", {before}) == ("Before", "_12")`, `CopyOf("before_x", …)`, `CopyOf("other_1", {before})` and `CopyOf("before_", …)` null.  `SqlServerFieldsTests`: a theory of every path of the table and its rule, and `RuleOf("columns", "answer.rows[].unknown")` null.  `SqlServerMessagesTests` with a `IRenamingScope` stub that maps `label`, `other`: `"The undeclared parameter '@label_1' is used more than once"` gives `"@<label>"`; `"@other and @@ROWCOUNT"` gives `"@<other>"`; `ConnectionErrors.LoginRefused` gives `""`; `WordsOf("@p_1 @p_2")` is `["@", "p_1", "@", "p_2"]`.  `EngineRenamingsTests`: `For(SqlServer)` is a `SqlServerRenaming`; `For(PostgreSql)` stays null (its own fact).
- [ ] **Step 2: Run them**: `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-namespace "SqlSource.Tool.Tests.Bundles.Renaming*"`.  Expected: FAIL to compile.
- [ ] **Step 3: Implement** the three classes and the `EngineRenamings` line, `// Built by 10.3.` removed.
- [ ] **Step 4: Run them**: PASS, and the rest of `Bundles.Renaming` still passes.
- [ ] **Step 5: Commit** `feat(tool): SQL Server's part of the renaming`.

### Task 3: Every fixture renamed

**Files:**
- Create: `tests/SqlSource.Tool.Testing/Bundles/RawBundles.cs`
- Create: `tests/SqlSource.Tool.Testing/Bundles/BundleLeaks.cs`
- Create: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerFixtureRenamingTests.cs`

**Interfaces:**
- Consumes: `EngineRenamings.For` (Task 2), `BundleRenamer.RenameAsync`, `FixtureCases.Of`, `FixtureReplay`.
- Produces: `internal static class RawBundles` with `public static Bundle OfFixture(FixtureCase fixture)` (the fixture's files and a `BundleInfo` with `Renamed: false`, `ToolVersion`, `Runtime` and `OperatingSystem` `fixture`, the engine and server version of its `expected.json`, the driver version of its `open`, outcome `Entry`) and `public static Task<Bundle> CaptureAsync(FixtureCase fixture, Func<FixtureCase, IQueryDescriber> describer, string connection, IDescribeExchange live)` (describes through a `CapturingExchange` as `FixtureReplay.DescribeThroughAsync` does, but a refused query gives a bundle whose outcome is the failure's id and `DescribeStepName.Of` its step, with no `expected.json`); `internal static class BundleLeaks { public static IReadOnlyList<string> Find(Bundle renamed, IEngineRenaming engine, IReadOnlySet<string> keptFromEngineRows, params string[] sources); public static IReadOnlySet<string> EngineRowNames(Bundle raw, IEngineRenaming engine); public static IReadOnlyList<(string Path, string Value)> ProvenanceOf(string expected) }`.
  - `Find` lexes each source (`schema.sql`, the case's `query.sql`) with the engine's dialect and word rule, takes each word, quoted name and literal content outside comments that is no engine word, no number and not in `keptFromEngineRows`, and reports each found, ignoring case, as a token of a string that the renamed bundle holds under a rule other than `Fixed` or `Closed` (the capture's by `RuleOf`, the entry's by `EntryRenamer.RuleOf`, its key included), or as a token of its `query.sql`.
  - `EngineRowNames` gives every `Name` member of a row `IsEngineRow` says is the engine's.
  - `ProvenanceOf` gives `plan`, `tableMatch`, each parameter's `typeSource` and each column's `nullableSource`, with their paths.

- [ ] **Step 1: Write the tests**, each a theory over the names of `FixtureCases.Of("mssql")`, with `new SqlServerDescriber()` and `RenamingKey.Of`:
  - `Rename_Fixture_GivesABundleThatReplaysToItsRenamedEntry`: `RenameAsync` succeeds (the proof is inside it).
  - `Rename_Fixture_KeepsTheOriginalsProvenance`: `ProvenanceOf(renamed.Expected) == ProvenanceOf(raw.Expected)`.
  - `Rename_Fixture_LeaksNoNameOrLiteralOfTheUsers`: `Find(...)` over `schema.sql` of the engine folder and the case's `query.sql` is empty.
  - `Rename_FixtureWithTwoKeys_GivesOtherNamesAndTheSameProvenance`: keys of fills 1 and 2 give different `query.sql` and equal provenance.
- [ ] **Step 2: Run** `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Bundles.Renaming.SqlServer.SqlServerFixtureRenamingTests"`.  Expected: PASS for all 26 cases.  A case that fails is investigated (systematic debugging) and fixed in Task 2's code; a case the renaming must refuse is reported to the controller before anything is changed.
- [ ] **Step 3: Commit** `test(tool): every SQL Server fixture renamed, replayed and searched for leaks`.

### Task 4: Errors and refusals, by hand

**Files:**
- Create: `tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerErrorRenamingTests.cs`

**Interfaces:**
- Consumes: `RawBundles.OfFixture` (Task 3); `BundleReplay` to give an edited raw bundle its outcome.

Each case is a fixture's raw bundle with one answer replaced, its outcome made by replaying it raw, then renamed.

- [ ] **Step 1: Write the tests:**
  - `Rename_UndeclaredParameterError_NamesTheRenamedParameter`: `update-copies-agree` with `parameters`' answer an `error` of rows `{11502, 16, 1, "The undeclared parameter '@<a copy>' is used more than once in the batch being analyzed."}`; the raw replay is `SQLSRC226` at `describe parameters`; renamed, it succeeds, the message is `@<renamed parameter>`, and the renamed failure's id and step are the raw's.
  - `Rename_ColumnsErrorRows_KeepTheirNumbersAndLoseTheirText`: `select-left-join` with `columns`' `errors` `[{ number: 208, type: 7, message: "Invalid object name 'dbo.users'." }]`; renamed, the message is `""`, no `users` is in the bundle, the outcome is the raw's.
  - `Rename_OpenError_KeepsItsNumbers`: `open`'s answer an `error` of `{18456, 14, 1, ConnectionErrors.LoginRefused}`; renamed, the message is `""` and the outcome is the raw's.
  - `Rename_MemberWithNoRule_IsRefusedAtItsPlace`: a string `extra` added to the first row of `columns`; the refusal is `RefusalStep.Token` at `capture.json call <n> 'columns' answer.rows[].extra` with the value as its token.
  - `Rename_ANameThatDiffersFromTheCatalogsByCaseAlone_IsRefusedByTheProof`: a fixture whose `tableMatch` is `matched`, with one selected column written in upper case in `query.sql`, in each request's `sql` and in `columns`' `name` (its `sourceColumn` as it was), and its expected entry made by replaying it raw; renamed, the refusal is `RefusalStep.Replay` (Q1).
- [ ] **Step 2: Run** `dotnet test --project tests/SqlSource.Tool.Tests -- --filter-class "SqlSource.Tool.Tests.Bundles.Renaming.SqlServer.SqlServerErrorRenamingTests"`: PASS.
- [ ] **Step 3: Commit** `test(tool): SQL Server's errors and refusals of the renaming, built by hand`.

### Task 5: Every word of the corpora classified

**Files:**
- Create: `tests/DatabaseTests/SqlServer/Renaming/CorpusWordsTests.cs`
- Create: `tests/DatabaseTests/SqlServer/Renaming/CorpusUserWords.cs`

**Interfaces:**
- Consumes: `SqlServerRenaming.Instance` (Task 2); the fixtures through the harness's `DescriberFixtures`, the realistic cases through `RealisticCases.Corpus.All`.
- Produces: `internal static class CorpusUserWords { public static IReadOnlySet<string> Words }`, folded as `SqlServerRenaming.Fold` folds: every word of the corpora that is the user's.

A word of a case is each word of a `Text` lexeme, by the engine's word rule with `@` and `#` trimmed and `@@` words whole, and each bracketed or double-quoted name, of the `query.sql` of each SQL Server fixture and realistic case, comments, literals and parameters left out.  No container: the class is not in `UsesSqlServer`.

- [ ] **Step 1: Write the tests**: `EveryWord_OfTheCorpora_IsTheEnginesOrTheUsers` (each word is `IsEngineWord` or in `Words`, the message listing every word that is neither with its case); `EveryUserWord_IsNoEngineWord`; `EveryUserWord_IsInSomeCase`.
- [ ] **Step 2: Run** `dotnet test --project tests/DatabaseTests/SqlServer -- --filter-class "SqlSource.DatabaseTests.SqlServer.Renaming.CorpusWordsTests"`.  Expected: the first FAILS, listing the words.  Classify each: an engine word missing from Task 1's lists goes there, with its source; the user's go in `CorpusUserWords`.  The spike found about 224 of the user's.  Run again: PASS, and Task 1's and Task 3's tests still pass.
- [ ] **Step 3: Commit** `test: every word of SQL Server's corpora is the engine's or the user's`.

### Task 6: The realistic corpus on the server

**Files:**
- Create: `tests/DatabaseTests/SqlServer/Renaming/RealisticRenamingTests.cs`

**Interfaces:**
- Consumes: `RawBundles.CaptureAsync`, `BundleLeaks` (Task 3), `EngineRenamings.For` (Task 2), `RealisticCases.Corpus`, `SqlServerDatabases.RealisticConnectionString`.

- [ ] **Step 1: Write the test** in `[Collection(UsesSqlServer.Name)]`, a theory over `RealisticCasesTests.Cases`, each case skipped by `SkipUnlessRunsOn`: `Rename_RealisticCase_ReplaysToItsRenamedAnswerAndLeaksNothing`.  The case is captured on the server with `new SqlServerDescriber()` over `LiveExchange.Instance`, renamed with a fixed key, and must succeed; a case with an entry keeps `ProvenanceOf`, and a refused case's renamed outcome is its raw one; `BundleLeaks.Find` over `Realistic/schema.sql` and the case's `query.sql` is empty.
- [ ] **Step 2: Run on the default image and the floor image**: `dotnet test --project tests/DatabaseTests/SqlServer -- --filter-class "SqlSource.DatabaseTests.SqlServer.Renaming.RealisticRenamingTests"`, then with `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.  Expected: PASS for every case that runs on the major, the three refused ones included.
- [ ] **Step 3: Commit** `test: SQL Server's realistic corpus captured, renamed and replayed`.

### Task 7: Documentation

**Files:**
- Modify: `docs/architecture/bundles.md` (the SQL Server placeholder becomes `### Renaming for SQL Server` under `## Renaming`'s engine sections, out of `## Not built yet`; the `EngineRenamings.For` paragraph)
- Modify: `docs/architecture/sqlserver-describer.md` (the calls' records: every member is kept by the renaming while the pipeline reads it, and a new one gets its rule in `SqlServerFields`)
- Modify: `docs/architecture/AGENTS.md` (the row of `src/SqlSource.Tool/Bundles/Renaming/SqlServer`)
- Create: `src/SqlSource.Tool/Bundles/Renaming/SqlServer/AGENTS.md`
- Modify: `src/SqlSource.Tool/Describing/SqlServer/AGENTS.md` (one bullet: a member added to a record of `SqlServerCalls.cs` gets its rule in `Bundles/Renaming/SqlServer/SqlServerFields.cs`)
- Modify: `tests/DatabaseTests/SqlServer/README.md` (the `Renaming/` tests; when an image moves, `VocabularyTests` and `CorpusWordsTests` say which words to add)
- Modify: `tests/README.md` if it lists the renaming's tests
- Create (only if Q1's answer is a TD item): `docs/tech-debt/TD-<next>-…md`, its id the next after 10.2's

- [ ] **Step 1: Write the section of `bundles.md`**: how SQL Server answers each member of the contract, the field table by reference to `SqlServerFields`, the vocabulary and how it is checked, the fold and Q1's consequence, the copy's suffix, the messages, and the rules found while building.
- [ ] **Step 2: Run** `dotnet test --project tests/SqlSource.Tests -- --filter-namespace "SqlSource.Tests.Documentation*"` (links and AGENTS.md sizes): PASS.
- [ ] **Step 3: Commit** `docs: SQL Server's part of the renaming`.
