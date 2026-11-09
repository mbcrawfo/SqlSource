# TD-0109 bundle, renaming and diagnose tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve items 1–6, 11, 12, 15, 16 and 17 of TD-0109: one copy of each duplicated helper of the bundle, renaming and diagnose tests, and Shouldly's assertions in SQL Server's renaming tests, with no test losing what it checks.

**Architecture:** Test-only.  Helpers that a container project needs too go in `tests/SqlSource.Tool.Testing` (`Bundles/`, `Fixtures/`, its root); helpers only the tool's tests use go at the root of `tests/SqlSource.Tool.Tests`; PostgreSQL's catalog read goes in `tests/DatabaseTests/Postgres/Renaming/`.  Each task moves a body to one place and points every site at it.

**Tech Stack:** C# / .NET 10, xunit v3, Shouldly; Docker for `tests/DatabaseTests/`.

**Spec:** `docs/tech-debt/TD-0109-duplicated-arrangements-and-uneven-conventions-in-tests.md` (items 1–6, 11, 12, 15, 16, 17: Problem and Proposed fix).

## Global Constraints

- Test-only: no file under `src/` changes.  No test is removed, weakened or renamed (names are 109-names' unit, items 19 and 54), but a test moved to a new class keeps its method name.
- Assertions are Shouldly's (`tests/AGENTS.md`); every test class `sealed`; the libraries' types `internal`.
- A helper one test project uses stays in that project; one a second project needs is in a library (`tests/AGENTS.md`).  Fakes and helpers only the tool's tests use are at the root of `tests/SqlSource.Tool.Tests`, namespace `SqlSource.Tool.Tests` (its `AGENTS.md`).
- A new number reader goes through `SqlRenamer.NumberAt`; a SQL Server reader of spaces uses `SqlRenamer.IsSpace` / `SqlDialectRules.IsSpace`, never `char.IsWhiteSpace` (run notes).
- Docs move with the code in the same commit: `tests/README.md` ("The projects", the `tests/SqlSource.Tool.Testing` list), `tests/DatabaseTests/Postgres/README.md`, `tests/DatabaseTests/SqlServer/README.md`, `docs/architecture/bundles.md` (lines ~296–303 and ~376–380), and any `AGENTS.md` that names a moved type.  `tests/AGENTS.md` is at its 8 KB limit: add nothing there.  An `AGENTS.md` that names a file keeps its maintenance footer.
- Every commit passes `./format.sh` then `./pre-commit-validation.sh`; implementers run the tests that cover their change (container projects need Docker).
- TD-0109's file: strike only items 1–6, 11, 12, 15, 16, 17 ("~~…~~  Fixed: …" for the problem, "~~…~~  Done." for the proposed fix); numbers never change; leave the others alone.  "2. and 3." is one proposed-fix line: strike it when both are done.

## Where the merged code differs from the spec

- Line numbers in the spec predate the renaming fixes; the sites below are today's.
- New sites of the same duplications since the review, folded into their items: `DatabaseTests/SqlServer/Renaming/TypedXmlRenamingTests.cs` (key by hand, `BundleRenamer.RenameAsync`, `EngineRowNames` by hand: items 2 and 4), `Commands/DiagnoseScrubTests.cs:281` (fixture lookup, item 11; sample `BundleInfo`, item 15), `Bundles/Renaming/Postgres/PostgresRenamingTests.cs:256` (bundle path, item 15).
- `SqlServerErrorRenamingTests.Fallback.cs` calls `BundleRenamer.RenameAsync` with a key source (`Func<RenamingKey>`) four times, testing the retry; those calls stay as they are.
- `docs/architecture/bundles.md` says `BundleLeaks.Find` "names no engine": it asks the part it is given.  The split keeps that parameter, so the search stays engine-neutral though it lives in `SqlServerBundleLeaks`, the class of SQL Server's search.

## Rulings

- **One key for tests that compare no two runs.**  `BundleRenamings.AnyKey = 1`, the key most sites use today (every SQL Server site and the fake engine's).  PostgreSQL's three `const byte Key = 7` go, and its sites rename with `AnyKey`.  Tests that compare two keys keep `1` and `2` explicitly, and the fallback tests keep their own keys.  If any test fails under `AnyKey` where it passed under 7, stop and report it: that would be a renaming that depends on its key.
- **PostgreSQL's realistic word check reads the vocabulary's own queries** for keywords, functions and types, so its types now include `information_schema`'s; every such word is in the committed vocabulary, so the renaming keeps it and the check finds no more on the floor and default images, and on a nightly image it finds the new ones it should.  It still reads no operator and no system column (the comment says why: an operator is no name, and a table's column may not take a system column's name).
- **`BundleReplays.Read` and both `DiagnoseCommandTests.ReadBundle`** become one `WrittenBundles.Read(path)` in `tests/SqlSource.Tool.Testing/Bundles/`; `BundleReplays.Read`'s 14 call sites call it.

## Review Focus

1. A word of plain text that is an `@@` function (`@@ROWCOUNT`): the corpus check reads it whole, the leak search skips it — test both arms of the switch (Task 3).
2. A word that is `@` or `#` alone: neither mode yields an empty word — test (Task 3).
3. A bracketed or double-quoted name with a nullability mark (`[Full Name?]`) and a doubled closer (`[a]]b]`): the shared quoted-name reader gives `Full Name` and `a]b`, and null for a `'literal'` — test (Task 3).
4. `FixtureCases.Named` for a name no case has: fails naming the engine and the name, not `Single`'s bare "Sequence contains no matching element" — test (Task 1).
5. The PostgreSQL realistic word check run on the floor image (`postgres:14.24`) as well as the default, since its types source widened (Task 6).

---

### Task 1: Fixture lookups (item 11)

**Files:**
- Modify: `tests/SqlSource.Tool.Testing/Fixtures/FixtureCases.cs`
- Test: `tests/SqlSource.Tool.Tests/Fixtures/FixtureCasesTests.cs`
- Modify (sites): `tests/SqlSource.Tool.Tests/BundleReplays.cs` (`OfCase`), `BundleLeaksTests.cs` (`RawOf`), `PostgresRenamings.cs` (`Schema`, `Fixture`), `DiagnoseScene.cs` (`Case`), `Commands/ReplayTests.cs` (`CopyCase`), `Commands/DiagnoseScrubTests.cs:281`, `Bundles/Renaming/SqlServer/SqlServerFixtureRenamingTests.cs` (`RawOf`, schema read), `Bundles/Renaming/SqlServer/SqlServerErrorRenamingTests.cs` (`:174`, `:314`, `FixtureOf`, `SchemaAsync`, `EditedAsync`), `Bundles/BundleReplayTests.cs:239`, `Bundles/BundleReaderTests.cs:41`

**Interfaces:**
- Produces: `FixtureCases.Named(string engine, string name) : FixtureCase`; `FixtureCases.SchemaOf(string engine) : string` (the path of `<Root>/<engine>/schema.sql`).

- [ ] **Step 1: Tests** in `FixtureCasesTests`: `Named_ACaseOfTheEngine_IsThatCase` (`FixtureCases.Named("postgres", "select-plain").Name.ShouldBe("select-plain")`, `.Engine.ShouldBe("postgres")`); `Named_NoCaseOfTheName_FailsNamingTheEngineAndTheName` (`Should.Throw<InvalidOperationException>(() => FixtureCases.Named("mssql", "no-such-case")).Message` contains `mssql` and `no-such-case`); `SchemaOf_Engine_IsItsSchemaFile` (`File.Exists(FixtureCases.SchemaOf("mssql")).ShouldBeTrue()`, `Path.GetFileName(...)` is `schema.sql`).
- [ ] **Step 2:** Run them; they fail to compile.
- [ ] **Step 3:** Implement both members; `Named` uses `SingleOrDefault` and throws `InvalidOperationException($"The fixtures of {engine} have no case {name}.")`.
- [ ] **Step 4:** Point every site above at them (`FixtureCases.Of(e).Single(found => found.Name == n)` → `FixtureCases.Named(e, n)`, `Path.Combine(FixtureCases.Root, Engine, "schema.sql")` → `FixtureCases.SchemaOf(Engine)`).  `grep -rn 'found.Name == ' tests/SqlSource.Tool.Tests` leaves only `WalkMatrixTests` and `BundleCasesTests` (other lists).
- [ ] **Step 5:** `dotnet test --project tests/SqlSource.Tool.Tests` passes.  Update the `tests/README.md` entry of `FixtureCases` if it lists members.  Format, validate, commit `test: one lookup of a describer fixture and its schema (TD-0109 item 11)`.

### Task 2: Renaming helpers (items 2, 3, 5)

**Files:**
- Create: `tests/SqlSource.Tool.Testing/Bundles/BundleRenamings.cs`
- Create: `tests/SqlSource.Tool.Tests/SqlServerRenamings.cs`
- Modify: `tests/SqlSource.Tool.Testing/Bundles/RawBundles.cs` (`OutcomeOf` public)
- Modify: `tests/SqlSource.Tool.Tests/RenamingBundles.cs` (`Key`, `RenameAsync`, the failure arm of `RawAsync`), `PostgresRenamings.cs`
- Modify (sites): `BundleLeaksTests.cs` (`Engine`, `Part`, `RawOf`), `Bundles/Renaming/SqlServer/SqlServerFixtureRenamingTests.cs`, `SqlServerErrorRenamingTests.cs` (+ `.Fallback.cs` for `Engine`/`Part`/`RenamedAsync`/`RefusedAsync` names; its four key-source calls stay), every `RenamingBundles.Key(` caller (→ `BundleRenamings.Key(`), `DatabaseTests/Postgres/Renaming/RealisticRenamingTests.cs`, `PartitionRenamingTests.cs`, `DatabaseTests/SqlServer/Renaming/RealisticRenamingTests.cs`, `TypedXmlRenamingTests.cs`

**Interfaces:**
- Consumes: `FixtureCases.Named`, `FixtureCases.SchemaOf` (Task 1).
- Produces:
  - `BundleRenamings.AnyKey : byte = 1`; `BundleRenamings.Key(byte fill) : RenamingKey`; `BundleRenamings.RenameAsync(Bundle raw, IEngineRenaming engine, IQueryDescriber describer, RenamingKey key) : Task<Outcome<RenamedBundle, RenamingRefusal>>` (token from `TestContext.Current`); `BundleRenamings.RenamedAsync(same) : Task<RenamedBundle>` (fails with the refusal's text); `BundleRenamings.RefusedAsync(same) : Task<RenamingRefusal>`.
  - `SqlServerRenamings` (root of Tool.Tests, mirrors `PostgresRenamings`): `Engine = "mssql"`, `Part : IEngineRenaming` (`EngineRenamings.For(DatabaseEngine.SqlServer)`), `SchemaAsync() : Task<string>` (reads `FixtureCases.SchemaOf(Engine)`), `Fixture(string name) : FixtureCase`, `Raw(string name) : Bundle` (`RawBundles.OfFixture`), `RenameAsync(Bundle raw, RenamingKey? key = null)`, `RenamedAsync(Bundle raw, RenamingKey? key = null)`, `RefusedAsync(Bundle raw, RenamingKey? key = null)`, each with `new SqlServerDescriber()` and `key ?? BundleRenamings.Key(BundleRenamings.AnyKey)`.
  - `PostgresRenamings.RenameAsync/RenamedAsync(Bundle raw, byte key = BundleRenamings.AnyKey)`; its `Key` constant goes.
  - `RawBundles.OutcomeOf(DescribeFailure failure) : BundleOutcome` (public).

- [ ] **Step 1:** Write `BundleRenamings` and `SqlServerRenamings`; make `OutcomeOf` public.
- [ ] **Step 2:** Move every site: the three SQL Server classes of Tool.Tests lose their `Engine`, `Part`, fixture lookups, schema reads and private `RenamedAsync`/`RefusedAsync`/`RenameAsync`; `RenamingBundles.RenameAsync` calls `BundleRenamings.RenameAsync`, its `Key` goes, and `RawAsync`'s failure arm calls `RawBundles.OutcomeOf`; the four container classes call `BundleRenamings.RenamedAsync` (PostgreSQL's two with `AnyKey` and no `const byte Key`).  `grep -rn 'BundleRenamer.RenameAsync\|Enumerable.Repeat' tests --include='*.cs'` leaves only `BundleRenamings.cs`, `RenamingBundles`' fake overload if it keeps one (it should not), and `SqlServerErrorRenamingTests.Fallback.cs`'s key-source calls.
- [ ] **Step 3:** Run `tests/SqlSource.Tool.Tests`, then `tests/DatabaseTests/Postgres` and `tests/DatabaseTests/SqlServer` with `--filter-namespace "*.Renaming"` (or the class filters) on the default images.  Any failure under `AnyKey` that passed under 7: stop and report (Rulings).
- [ ] **Step 4:** Update `tests/README.md` (`Bundles/` list: `BundleRenamings`; the root helpers if listed), `docs/architecture/bundles.md` (~296: "through `PostgresRenamings.cs`, `SqlServerRenamings.cs` and…").  Format, validate, commit `test: one renaming of a real engine's bundle and SQL Server's renamings helper (TD-0109 items 2, 3, 5)`.

### Task 3: Leak searches split by engine (items 1, 4, 17)

**Files:**
- Delete: `tests/SqlSource.Tool.Testing/Bundles/BundleLeaks.cs`
- Create: `tests/SqlSource.Tool.Testing/Bundles/PostgresBundleLeaks.cs` (`Of` and its helpers), `tests/SqlSource.Tool.Testing/Bundles/SqlServerBundleLeaks.cs` (`Find`, `EngineRowNames` and their helpers)
- Delete: `tests/SqlSource.Tool.Tests/BundleLeaksTests.cs`; Create: `PostgresBundleLeaksTests.cs`, `SqlServerBundleLeaksTests.cs`, `ProvenanceTests.cs` at the root of Tool.Tests
- Modify: `tests/DatabaseTests/SqlServer/Renaming/CorpusWordsTests.cs`, `VocabularyTests.cs`; every `BundleLeaks.` caller (`grep -rn 'BundleLeaks\.' tests`)

**Interfaces:**
- Consumes: `SqlServerRenamings` (Task 2).
- Produces:
  - `PostgresBundleLeaks.Of(Bundle renamed, IEnumerable<string> sources, Func<string, bool, bool> isEngineWord)` — today's `BundleLeaks.Of`, unchanged.
  - `SqlServerBundleLeaks.Find(Bundle renamed, IEngineRenaming engine, IReadOnlySet<string> keptFromEngineRows, params string[] sources)` — today's; `SqlServerBundleLeaks.Find(Bundle renamed, IEngineRenaming engine, Bundle raw, params string[] sources)` = the first with `EngineRowNames(raw, engine)`; `SqlServerBundleLeaks.EngineRowNames(Bundle raw, IEngineRenaming engine)`.
  - `SqlServerBundleLeaks.PlainWordsOf(string text, int start, int end, bool quoteFollows, IEngineRenaming engine, bool keepsSystemFunctions) : IEnumerable<string>` — today's `EngineWordsOf`; `keepsSystemFunctions` true yields an `@@` word whole (the corpus check), false skips it (the search); a word empty after trimming `@`/`#` is never yielded.
  - `SqlServerBundleLeaks.QuotedNameOf(string quoted) : string?` — a `"…"` or `[…]` name with its doubled closer undone and a trailing `!`/`?` after a longer stem left out; null for any other quoted region.  The private literal reader keeps the `'…'` arm and calls it for names.
  - Use `CaptureJson.RequestKey`/`AnswerKey` in place of the private `Request`/`Answer` constants.

- [ ] **Step 1: Tests** in `SqlServerBundleLeaksTests` (new, beside the moved `Find_`/`EngineRowNames_` tests): `PlainWordsOf_SystemFunction_IsWholeWhenKeptAndNoneWhenNot` (text `SELECT @@ROWCOUNT, @x, #t` over its whole length, `quoteFollows: false`, `SqlServerRenamings.Part`: with `true` gives `["SELECT", "@@ROWCOUNT", "x", "t"]`, with `false` gives `["SELECT", "x", "t"]`); `PlainWordsOf_AtOrHashAlone_YieldsNoWord` (`"@ # a"` gives `["a"]` in both modes); `QuotedNameOf_Region_IsItsName` theory: `"[Full Name?]"`→`"Full Name"`, `"[a]]b]"`→`"a]b"`, `"\"x!\""`→`"x"`, `"[?]"`→`"?"`, `"'lit'"`→null.
- [ ] **Step 2:** Run; they fail to compile.
- [ ] **Step 3:** Split the file by engine; move the tests: `Of_` → `PostgresBundleLeaksTests`, `Find_`/`EngineRowNames_` → `SqlServerBundleLeaksTests` (using `SqlServerRenamings.Raw`/`Part` and the new `Find` overload), `Provenance_Entry_IsItsPlanTableMatchAndSources` → `ProvenanceTests`; names unchanged.
- [ ] **Step 4:** Every caller that passed `EngineRowNames(raw, Part)` uses the overload (`SqlServerFixtureRenamingTests`, the two container `RealisticRenamingTests`-likes on SQL Server, `TypedXmlRenamingTests`, SQL Server's `DiagnoseCommandTests`); callers with another kept set keep the first overload.  `CorpusWordsTests` drops `PlainWordsOf` and `NameOf` and calls the two shared members (`keepsSystemFunctions: true`).
- [ ] **Step 5 (item 17):** `CorpusWordsTests` and SQL Server's `VocabularyTests` assert with Shouldly, as PostgreSQL's `VocabularyTests` does: `unclassified.ShouldBeEmpty("Neither the engine's nor the user's: " + …)` and so for each `Assert.True(x.Count == 0, msg)`; `Assert.True(known > 0)` → `known.ShouldBeGreaterThan(0)`; `Assert.Equal(sorted, spelled)` → `spelled.ShouldBe(sorted)`; `Assert.Equal(spelled.Count, CorpusUserWords.Words.Count)` → `CorpusUserWords.Words.Count.ShouldBe(spelled.Count)`.  `grep -rn 'Assert\.\(True\|Equal\|False\)' tests --include='*.cs'` finds none.
- [ ] **Step 6:** Run Tool.Tests, then SQL Server's `Renaming` classes and `DiagnoseCommandTests` on the default image and on `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`, and PostgreSQL's `Renaming` classes on the default.
- [ ] **Step 7:** Docs: `tests/README.md` (`Bundles/`: "`PostgresBundleLeaks`, … `Of`, … and `SqlServerBundleLeaks`, … `Find`, … gives the names of the rows that are the engine's, and holds the reading of plain words and quoted names that `CorpusWordsTests` shares"), `docs/architecture/bundles.md` (~298, ~303, ~378–380: new names; `BundleLeaksTests` → `SqlServerBundleLeaksTests`), `tests/DatabaseTests/SqlServer/README.md` ~118 if it says how words are read, `tests/DatabaseTests/Postgres/README.md` ~65 (`BundleLeaks` → `PostgresBundleLeaks`).  Format, validate, commit `test: split the leak searches by engine and share SQL Server's word reading (TD-0109 items 1, 4, 17)`.

### Task 4: Diagnose command body (item 6)

**Files:**
- Create: `tests/SqlSource.Tool.Testing/DiagnoseCommands.cs`, `tests/SqlSource.Tool.Testing/Bundles/WrittenBundles.cs`
- Modify: `tests/DatabaseTests/Postgres/Describer/DiagnoseCommandTests.cs`, `tests/DatabaseTests/SqlServer/Describer/DiagnoseCommandTests.cs`, `tests/SqlSource.Tool.Tests/BundleReplays.cs` (`Read` goes) and its 14 callers, `DiagnoseScene.cs` and `Commands/ReplayTests.cs` (`Reproduced`)

**Interfaces:**
- Consumes: `SqlServerBundleLeaks.Find(renamed, engine, raw, sources)`, `PostgresBundleLeaks.Of` (Task 3).
- Produces:
  - `WrittenBundles.Read(string path) : Bundle` — `BundleReader.Read(path, ToolHost.OpenRead, ToolHost.ReadText)`, failure null, value not null.
  - `DiagnoseEngine(string Dialect, Func<IQueryDescriber> Describer, string Connection, string Sql, Func<DiagnoseCommands, Bundle, Task> ShouldLeakNothingAsync)` record, in `DiagnoseCommands.cs`, as `ErrorPathEngine` is beside `ErrorPaths`.
  - `DiagnoseCommands(DiagnoseEngine engine) : IDisposable` with `const string Reproduced = "sqlsource: reproduced: the pipeline gives this answer\n"`, `const string Query = "FindQuokkaOrders"`, `DiagnoseAsync(params string[] args) : Task<CliResult>`, `ReadBundle(string relativePath) : Bundle`, `ShouldLeakNothingAndReproduceAsync()` (today's `Diagnose_AQueryOfApp_…` body) and `ShouldReproduceAfterNoObfuscateThenScrubAsync()` (today's `Diagnose_NoObfuscateThenScrub_…` body).  The project is `Plans.Postgres(folder, directory: "")` with `Settings["database"] = "app"` and `Settings["dialect"] = engine.Dialect`, its query file `Orders.sql`.

- [ ] **Step 1:** Write both files; the bodies are the two tests' as they are, the leak check the engine's delegate: PostgreSQL's reads `DescriberFixtures.Postgres.Schema` and calls `PostgresBundleLeaks.Of(renamed, [schema, Sql], PostgresWords.IsEngineWord)`; SQL Server's makes `raw-for-leaks.zip` with `--no-obfuscate` through `DiagnoseAsync`, reads it, and calls `SqlServerBundleLeaks.Find(renamed, SqlServerRenamings-equivalent part, raw, schema, Sql)` (`EngineRenamings.For(DatabaseEngine.SqlServer)`; the container project cannot see Tool.Tests).
- [ ] **Step 2:** Each engine's `DiagnoseCommandTests` keeps its collection, comment, SQL and two test names, and each test is one line calling the body, as `EndToEnd/ErrorPathTests.cs` is.
- [ ] **Step 3:** `BundleReplays.Read` goes: its callers call `WrittenBundles.Read`; `DiagnoseScene.Reproduced` and `ReplayTests`' private `Reproduced` go, their users read `DiagnoseCommands.Reproduced`.  `grep -rn 'reproduced: the pipeline' tests --include='*.cs'` finds one line.
- [ ] **Step 4:** Run Tool.Tests and both `DiagnoseCommandTests` (default images; SQL Server's also on its floor image).
- [ ] **Step 5:** Docs: `tests/README.md` (Tool.Testing list: `DiagnoseCommands` beside `ErrorPaths`, `WrittenBundles` in `Bundles/`), `tests/DatabaseTests/Postgres/AGENTS.md` last bullet ("the two of `DiagnoseCommandTests` … `DiagnoseCommands`"), and the matching sentence of `tests/DatabaseTests/SqlServer/AGENTS.md` if it has one (add one only if PostgreSQL's has it; keep the footer).  Format, validate, commit `test: one diagnose command body for both engines (TD-0109 item 6)`.

### Task 5: Sample bundle and throwing describer (items 15, 16)

**Files:**
- Create: `tests/SqlSource.Tool.Tests/BundleInfos.cs`, `tests/SqlSource.Tool.Tests/ThrowingDescriber.cs`
- Modify: path sites `RenamingBundles.cs` (`Path` goes), `PostgresRenamings.cs`, `Bundles/BundleErrorsTests.cs`, `DiagnoseBundlesTests.cs`, `BundleJsonTests.cs`, `BundleReplayTests.cs`, `BundleQueryFileTests.cs`, `BundleDescribersTests.cs`, `EntryDifferencesTests.cs`, `BundleReaderTests.cs:186`, `Renaming/Postgres/PostgresRefusalTests.cs:234`, `Renaming/Postgres/PostgresRenamingTests.cs:256`, and `RenamingBundles.Path`'s users; info sites `BundleJsonTests`, `BundleWriterTests`, `BundleReaderTests`, `BundleReplayTests.Info`, `Commands/ReplayTests.InfoOf`, `Commands/DiagnoseScrubTests.cs:286`; describer sites `Bundles/BundleReplayTests` (`MissingDescriber`), `Describing/DatabaseRunsFaultTests` (`ThrowsBeforeItReturns`)

**Interfaces:**
- Produces:
  - `BundleInfos.Path = "/work/report.zip"` ("where a bundle's errors would be; nothing is read there"); `BundleInfos.Sample(string engine = "postgres", bool renamed = false, string? driverVersion = null, BundleOutcome? outcome = null) : BundleInfo` with `BundleVersion: 1`, `ToolVersion: "1.4.0"`, `FormatVersion: SidecarFormat.Version`, `ServerVersion: "18.6"`, `Runtime: ".NET 8.0.11"`, `OperatingSystem: "Linux 6.8"`, `Outcome: outcome ?? BundleOutcome.Entry`.  A site that differs (`DiagnoseScrubTests`' `ServerVersion: "17.0.1000.7"`) uses `with`.
  - `ThrowingDescriber(Func<OpenRequest, Exception> throws) : IQueryDescriber` — `Dialect` PostgreSQL, no secrets, no rewrite, `OpenAsync` throws `throws(request)`.

- [ ] **Step 1:** Write both; point every site at them; each value a site asserts stays the same.  `grep -rn '"/work/report.zip"\|ToolVersion: "1.4.0"' tests/SqlSource.Tool.Tests` finds only `BundleInfos.cs`.
- [ ] **Step 2:** Run Tool.Tests; format, validate, commit `test: one bundle path, one sample bundle info and one throwing describer (TD-0109 items 15, 16)`.

### Task 6: PostgreSQL's catalog words (item 12)

**Files:**
- Create: `tests/DatabaseTests/Postgres/Renaming/PostgresCatalogWords.cs`
- Modify: `tests/DatabaseTests/Postgres/Renaming/VocabularyTests.cs`, `RealisticRenamingTests.cs`

**Interfaces:**
- Produces: `PostgresCatalogWords.All`, `PostgresCatalogWords.Names` (`IReadOnlyList<(string Sql, bool Quoted)>`: `All` the five sources of `VocabularyTests` as today, `Names` its keywords, functions and types); `PostgresCatalogWords.ReadAsync(DbDataSource source, IEnumerable<(string Sql, bool Quoted)> sources, CancellationToken token) : Task<SortedSet<string>>` — each read whole and folded with `PostgresWords.Fold(word, quoted)`; the `CA2100` suppression moves with it.

- [ ] **Step 1:** Write it; `VocabularyTests.ImageWordsAsync` reads `All` on `PostgresDatabases.Database`, `RealisticRenamingTests.ServerWordsAsync` reads `Names` on `PostgresDatabases.RealisticDatabase`, with the comment of the Rulings on why it reads a subset.  Both classes lose their query arrays and loops.
- [ ] **Step 2:** Run both classes on the default image and on `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`.
- [ ] **Step 3:** `tests/DatabaseTests/Postgres/README.md` ~65 (`RenamedWords_AreNoWordOfTheServer` now names `information_schema`'s types too).  Format, validate, commit `test: one read of PostgreSQL's catalog words (TD-0109 item 12)`.

### Task 7: TD-0109 bookkeeping

**Files:**
- Modify: `docs/tech-debt/TD-0109-duplicated-arrangements-and-uneven-conventions-in-tests.md`; `docs/tech-debt/README.md` (TD-0109's row only if its description stops being true)

- [ ] **Step 1:** Strike items 1–6, 11, 12, 15, 16, 17 in place, each "Fixed: <what is there now, by name>"; strike proposed fixes 1, "2. and 3.", 4, 5, 6, 11, 12, 15, 16, 17 with "Done.".  Items 19 and 54 stay open, so the file and its row stay; the row still describes what remains (rewrite it to the open items if it no longer does).
- [ ] **Step 2:** Format, `./pre-commit-validation.sh`, commit `docs: strike TD-0109 items 1-6, 11, 12, 15, 16, 17`.
