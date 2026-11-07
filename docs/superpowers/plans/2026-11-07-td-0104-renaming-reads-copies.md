# TD-0104: SQL Server's renaming reads copies through the capture

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** SQL Server's capture records each reused parameter's copies beside the `parameters` request, and the renaming reads a copy through that record instead of by its spelling, so a cut copy and a copy of `@x_` beside `@x` are renamed right and the proof passes.

**Architecture:** The describer adds a nullable `reused` member to `SqlServerParametersRequest`, the `Reused` list of `CopiedSql`.  The engine's contract replaces `CopyOf(name, queryParameters)` with `CopiesIn(method, call)`, which reads a call's recorded copies; the neutral renaming builds one `RenamingCopies` from the raw calls for the SQL renamer and the scope, and one from the renamed calls for the token check.  SQL Server's messages read a copy's parameter through the scope, so both loops of TD-0108 item 2 go.

**Tech Stack:** C# / .NET, xUnit + Shouldly, Testcontainers SQL Server.

**Spec:** `docs/tech-debt/TD-0104-sql-server-renaming-cannot-read-back-some-copies.md`

## Global Constraints

- Root `AGENTS.md` rules: `./format.sh` then `./pre-commit-validation.sh` before every commit; recorded fixture files are never edited by hand (`tools/record-fixtures.sh mssql`, default image `DefaultImages.SqlServer`); docs updated in the same change.
- A capture record's member is added, nullable, and none is renamed (`SqlServerCalls.cs` header).  The new member is null when no parameter is reused, so the request of every other query keys as before (`CaptureJson.KeyOf` leaves out null members).
- Resolving TD-0104 follows `docs/tech-debt/AGENTS.md` "Resolving an item": delete the file, remove its README row (own row only), remove every `TD-0104` reference.
- TD-0108 item 2 is struck in place ("~~…~~ Fixed: …"; its proposed fix "~~…~~ Done."), numbers unchanged.
- Leave `NameMapping` alone.  The engine's contract changes only as below, for both engines.

**Deviation from the spec, by the code:** the spec says "have `CopyOf` read a copy through that map".  `CopyOf` is a member of a stateless engine given only a name and the query's parameters, so it cannot see the capture: the contract member becomes `CopiesIn`, and "the two tests above" become tests of `CopiesIn` that expect the parameter.  The record holds the `Reused` list (parameter and copies in order of use) rather than the `ParameterOfCopy` map, since the renamed copy's number is its position: a cut copy whose number was passed over (`…_2`, `…_3`) is the renamed query's `_1`, `_2`.

**Owner question pending (Q1):** an older capture of a query with a reused parameter no longer replays (SQLSRC290).  Build assumes "accept it"; if the owner chooses otherwise, Task 1 changes where the member lives.

## Review Focus

1. A cut copy whose number was passed over: `CopiesIn` gives the suffix by position (`_1`, `_2`), not the raw number — test in Task 2 (`SqlServerRenamingTests`).
2. A parameter spelled in two cases: `RenamingCopies` finds `ID_1` as the copy `Id_1` (`SqlParameterName.Comparer`) — test in Task 2 (`RenamingCopiesTests`).
3. A message naming `@x__1`, a copy of `@x_` beside `@x`: written as `@<x_>` — test in Task 2 (`SqlServerMessagesTests`).
4. A renamed `reused` entry whose copy is not its parameter and suffix (a leak) is refused by the token check — test in Task 2 (`TokenCheckTests`).
5. A query with no reused parameter records nothing new: the re-recording of Task 1 changes only the captures of `update-copies-agree` and `update-optional-filter` — checked by reading the diff in Task 1.

---

### Task 1: The describer records the copies

**Files:**
- Modify: `src/SqlSource.Tool/Describing/SqlServer/SqlServerCalls.cs` (new record, new member)
- Modify: `src/SqlSource.Tool/Describing/SqlServer/Statements/ParameterCopies.cs` (`ReusedParameter` replaced by the call record)
- Modify: `src/SqlSource.Tool/Describing/SqlServer/SqlServerSession.cs:177-181`, and any other user of `ReusedParameter` (`SqlServerSession.Builders.cs`, `SqlServerErrors.cs`)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerFields.cs` (two rules)
- Modify: `tests/SqlSource.Tool.Tests/Describing/Capture/CallRecordTests.cs` only if its list must change (it lists records ending in Request/Answer; the new record is a member, so it should not)
- Modify: tests that script a `parameters` request of SQL with copies (`SqlServerSessionParameterTests`, `SqlServerEventsTests` `SELECT @p, @p`, others the run finds), `SqlServerFieldsTests`
- Re-record: `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/*/capture.json` with `tools/record-fixtures.sh mssql`
- Docs: `docs/architecture/sqlserver-describer.md` (the calls table's `parameters` request; "The copies and the probe": the request records `reused`; the paragraph on members added after captures: an older capture of a reused parameter misses)

**Interfaces:**
- Produces: `internal sealed record SqlServerReusedParameter(string Name, IReadOnlyList<string> Copies);` in `SqlServerCalls.cs` — the parameter's name as the list spells it, and its copies without `@`, in order of use.  JSON: `{"name": "before", "copies": ["before_1", "before_2"]}`.
- Produces: `SqlServerParametersRequest(string Sql, string? Params, IReadOnlyList<SqlServerReusedParameter>? Reused)` — `Reused` null when no parameter is reused (never an empty list).  JSON member `reused`.
- Produces: `CopiedSql.Reused` is `IReadOnlyList<SqlServerReusedParameter>`; `ReusedParameter` is gone.
- Produces: rules `parameters request.reused[].name` → `FieldRule.Parameter` and the rule of each element of `request.reused[].copies` → `FieldRule.Parameter` (spell the array-element path as `RuleOf` is asked it; `SqlServerFieldsTests` shows the form).

- [ ] **Step 1: Failing tests.**  In `SqlServerSessionParameterTests`, a reused-parameter test asserts the asked request is `new SqlServerParametersRequest(Renamed, null, [new SqlServerReusedParameter("<p>", ["<p>_1", "<p>_2"])])` (change the scripted request of each existing test whose SQL holds copies); a test of SQL with no reused parameter keeps `Reused` null.  In `SqlServerFieldsTests`, the two new paths give `FieldRule.Parameter`.
- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~SqlServer"` — expect the new assertions to fail.
- [ ] **Step 3: Implement** the record, the member, `ParameterCopies` on the new record, the session's request and the two rules.  Update each test that scripts a copies request.
- [ ] **Step 4: Run** the same filter, then all of `tests/SqlSource.Tool.Tests` — PASS except fixture replays whose committed captures lack `reused`.
- [ ] **Step 5: Re-record** `tools/record-fixtures.sh mssql`.  Read `git diff --stat`: only `update-copies-agree` and `update-optional-filter` captures change (Review Focus 5); any other changed file is reported, not committed blindly.
- [ ] **Step 6: Run** `tests/SqlSource.Tool.Tests` whole (fixture replays, `SqlServerFixtureRenamingTests` — the renaming still reads copies by spelling, and `reused` strings are `Parameter` members, so it passes) and `dotnet test tests/DatabaseTests/SqlServer --filter "FullyQualifiedName~FixtureCasesTests|FullyQualifiedName~ParameterTests"`.
- [ ] **Step 7: Docs** of `sqlserver-describer.md` as listed above.
- [ ] **Step 8: Commit** `feat(describer): SQL Server's capture records each reused parameter's copies (TD-0104)` after `./format.sh` and `./pre-commit-validation.sh`.

### Task 2: The renaming reads copies through the capture; TD-0104 resolved

**Files:**
- Create: `src/SqlSource.Tool/Bundles/Renaming/RenamingCopies.cs`
- Modify: `src/SqlSource.Tool/Bundles/Renaming/IEngineRenaming.cs` (replace `CopyOf`), `IRenamingScope.cs` (add `ParameterOf`)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/BundleRenamer.cs`, `RenamingRun.cs`, `SqlRenamer.cs` (ctor and `ParameterName`), `TokenCheck.cs` (`Check`, `IsParameter`)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerRenaming.cs` (`CopiesIn`; `CopyOf` and its loop gone), `SqlServerMessages.cs` (`Named` through `scope.ParameterOf`; its loop gone, and the `isEngineWord` parameter if nothing else uses it)
- Modify: `src/SqlSource.Tool/Bundles/Renaming/Postgres/PostgresRenaming.cs` (`CopiesIn` gives none)
- Modify: `tests/SqlSource.Tool.Tests/FakeEngineRenaming.cs` and the neutral tests that build a `SqlRenamer` or a scope (`SqlRenamerTests`, `TokenCheckTests`, `RenamingBundles` users)
- Test: `tests/SqlSource.Tool.Tests/Bundles/Renaming/RenamingCopiesTests.cs` (new), `SqlServer/SqlServerRenamingTests.cs`, `SqlServer/SqlServerMessagesTests.cs`, `Postgres/PostgresRenamingTests.cs`, `TokenCheckTests.cs`, `SqlServer/SqlServerErrorRenamingTests.cs` (its hand edit of `update-copies-agree` must rewrite `reused` too, if it does not already)
- Add fixture cases: `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/update-copies-of-two-parameters/query.sql`, `.../update-copies-cut/query.sql`; list both in `Cases_OfSqlServer_AreTheListedCases` of `tests/SqlSource.Tool.Tests/Fixtures/DescriberFixtureTests.cs`; record with `tools/record-fixtures.sh mssql`; add the new words `CorpusWordsTests` names to `tests/DatabaseTests/SqlServer/Renaming/CorpusUserWords.cs`
- Docs: `docs/architecture/bundles.md` (contract table's `CopyOf` row, PostgreSQL's and SQL Server's rows, `Register`/`Derive` row's "a copy comes through `CopyOf`", "Why step 8…", "The copies of a reused parameter", "SQL Server's messages"); `docs/architecture/sqlserver-describer.md` "Fixtures" (eight statements without rows, naming the two cases); `src/SqlSource.Tool/Bundles/Renaming/SqlServer/AGENTS.md` (the `CopyOf` rule becomes one on `CopiesIn`: suffix `_<position>`, read from `reused`); delete `docs/tech-debt/TD-0104-…md` and its README row; strike TD-0108 item 2 and its proposed fix.

**Interfaces:**
- Consumes: Task 1's `reused` member (`request.reused[].name`, `request.reused[].copies[]`).
- Produces: `internal sealed record ParameterCopy(string Name, string Parameter, string Suffix);` (in `RenamingCopies.cs`) — a copy's name without prefix, the query's parameter it copies, and the suffix that the copy of the renamed parameter has.
- Produces: `IEngineRenaming.CopiesIn(string method, JsonObject call) : IEnumerable<ParameterCopy>` — the copies one call records (`call` holds `request` and `answer`), read from the call alone; replaces `CopyOf`.  Docs: the suffix holds nothing of the user's; asked of every call, raw and renamed.
- Produces: `internal sealed class RenamingCopies` with `static RenamingCopies Of(IEngineRenaming engine, IEnumerable<(string Method, JsonObject Call)> calls)`, `static RenamingCopies None`, and `ParameterCopy? Find(string name)` — keyed by `SqlParameterName.Comparer`, the first record of a name kept.
- Produces: `IRenamingScope.ParameterOf(string name) : string` — the parameter of a copy the pipeline made, else the name.
- Produces: `SqlRenamer(IEngineRenaming engine, NameMapping mapping, IReadOnlySet<string> queryParameters, RenamingCopies copies)`; `ParameterName(name)`: a name that is no query parameter and that `copies.Find` finds is `NameOf(copy.Parameter, quoted: false, NameClass.Parameter) + copy.Suffix`.
- Produces: `RenamingRun.For(engine, mapping, query, copies)`; `BundleRenamer` builds the raw `RenamingCopies` from each call's `Original` after `Drop` and gives it to the run.
- Produces: `TokenCheck` builds `RenamingCopies` from each call's `Renamed`; `IsParameter(value)` passes a name that `IsSafe`, or that `Find` finds with `IsSafe(copy.Parameter)` and the name equal, by `SqlParameterName.Comparer`, to `copy.Parameter + copy.Suffix`.
- `SqlServerRenaming.CopiesIn`: for method `parameters` only, each `request.reused[]` entry's `copies[i]` is `new ParameterCopy(copies[i], name, "_" + (i + 1))` (invariant culture); anything else, or a missing/null `reused`, gives none; a member of the wrong JSON kind gives none for that entry.
- `PostgresRenaming.CopiesIn` gives none.  `FakeEngineRenaming.CopiesIn` reads whatever the neutral tests need (e.g. a `request.copies` array of `{name, parameter, suffix}`), documented in the fake.

- [ ] **Step 1: Add the two fixture cases** (statements without rows, over `dbo.users`):
  - `update-copies-of-two-parameters`, `-- name: RetireBetween`: `UPDATE dbo.users SET deleted_at = SYSUTCDATETIME() WHERE created_at < @since OR deleted_at < @since OR created_at > @since_ OR deleted_at > @since_;` (copies `since_1`, `since_2`, `since__1`, `since__2`).
  - `update-copies-cut`, `-- name: RetireCut`: the same shape with one parameter whose name is 127 characters, `retire_before_` followed by 113 `t`s, used twice (its copies are cut to 125 characters and `_1`, `_2`).
  List them in `DescriberFixtureTests`, run `tools/record-fixtures.sh mssql`, and read the diff (only the two new folders).
- [ ] **Step 2: Failing tests.**
  - `SqlServerFixtureRenamingTests` for the two new cases fails with `RequestSql` (the TD's two cases, end to end).
  - `SqlServerRenamingTests`: replace the four `CopyOf_*` tests by `CopiesIn_*`: `CopiesIn_ACopyOfTwoParametersOfTheQuery_IsItsOwnParameter` (`reused` `[{x, [x_1, x_2]}, {x_, [x__1, x__2]}]` gives `x__1` → `("x_", "_1")`); `CopiesIn_ACopyCutToFit_IsItsParameter` (the 127-`a` name with copies cut to 125 `a`s and `_1` gives that name and `_1`); `CopiesIn_ANumberPassedOver_IsSuffixedByPosition` (copies `[…_2, …_3]` give `_1`, `_2`); `CopiesIn_AnotherMethodOrNoReused_IsEmpty` (`probe`, `columns`, `parameters` with `reused` null or absent).
  - `RenamingCopiesTests`: `Find_ACopySpelledInAnotherCase_IsFound` (`ID_1` finds `Id_1`); `Find_NoCopy_IsNull`; `Of_TwoRecordsOfOneName_KeepsTheFirst`.
  - `SqlServerMessagesTests`: a message naming `@x__1` with the scope's copies `x__1` → `x_` is `@<x_>`; `Rename_AMappedNameThatLooksLikeACopy_IsWrittenWhole` keeps `@<label_9>` (no copy recorded); a copy with a longer separator now needs its copy recorded.
  - `TokenCheckTests`: `Check_ARenamedCopyThatIsNotItsParameterAndSuffix_IsRefused` (a renamed `reused` entry whose copy is a user's spelling); `Check_CopyOfAParameterThatIsNoReplacement_IsRefused` kept, through recorded copies.
  - `PostgresRenamingTests:55`: `CopiesIn` of any call is empty.
- [ ] **Step 3: Run** `dotnet test tests/SqlSource.Tool.Tests --filter "FullyQualifiedName~Renaming|FullyQualifiedName~DescriberFixture"` — the new tests fail (compile errors count as failing).
- [ ] **Step 4: Implement** the interfaces above; remove `CopyOf` from the contract, both engines and the fake; `SqlServerMessages.Named` becomes `scope.ParameterOf(name)`.
- [ ] **Step 5: Run** all of `tests/SqlSource.Tool.Tests`, then `dotnet test tests/DatabaseTests/SqlServer --filter "FullyQualifiedName~Renaming|FullyQualifiedName~FixtureCasesTests"` (adds the new words to `CorpusUserWords` as `CorpusWordsTests` names them).
- [ ] **Step 6: Docs and tech debt** as listed under Files.  `git grep -n "TD-0104\|CopyOf" -- src tests docs/architecture docs/tech-debt docs/manual` finds nothing.
- [ ] **Step 7: Commit** `fix(renaming): SQL Server's copies are read through the capture's record (TD-0104)` after `./format.sh` and `./pre-commit-validation.sh`.
