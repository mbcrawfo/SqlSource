# Test cleanup phase 05: tests/SqlSource.Tests/Generation/Types, deletes

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generation/Types`
- Findings: 7 (7 delete)
- Kind: one kind, delete
- Changed lines: about 76
- Guard: mutation (generator-generation-types), with covers and run time for 4 findings

## Files

- `tests/SqlSource.Tests/Generation/Types/MappedTypes/MappedTypeNamesTests.cs`
- `tests/SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs`
- `tests/SqlSource.Tests/Generation/Types/Maps/SqlServerTypeMapTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generation.Types-1-02

- test: `tests/SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs` | `PostgresTypeMapTests.Map_NodaTimeOff_IsTheBaseMap` | rows: all
- action: delete
- evidence: measured (12 rows, kills 3, alone 0, timeouts under it 0, rows needed 1; cover `PostgresTypeMapTests.Map_ArrayOfATypeOfNpgsqlTypes_NeedsTheDriverWithoutIt`) and read: `mapping.ShouldBe(PostgresTypeMap.Map(type, column))` (PostgresTypeMapTests.cs:573) compares a call with itself, since `libraries: null` is the parameter's default (src/SqlSource/Generation/Types/Maps/PostgresTypeMap.cs:45-50).  What is left, the text is not NodaTime's and `Library` is null, follows from `Map_EveryRowOfTheTable_GivesItsType` (PostgresTypeMapTests.cs:263), which maps every one of the twelve NodaTime names (each is a row of `Rows`, lines 124-228) under every column with no libraries and asserts its exact base-map text or failure; without libraries no row with a `Library` can be reached (PostgresTypeMap.cs:57-60).  No comment above the test; no AGENTS.md, readme, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names it or its NodaTime-off case (the class is named only for `Keys_` and, in TD-0089, for `Map_EveryRowOfTheTable_GivesItsType`); blind: no
- cover: `tests/SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs` | `PostgresTypeMapTests.Map_EveryRowOfTheTable_GivesItsType`
- proposal: Delete the theory; its `NodaTimeRows` data stays for `Map_NodaTimeOn_GivesEachRow`.
- verdict: confirmed
- verifier's reason: Read PostgresTypeMapTests.cs:562-577: line 573 compares `Map(type, column, libraries: null)` with `Map(type, column)`, the same call, since `libraries` defaults to null (src/SqlSource/Generation/Types/Maps/PostgresTypeMap.cs:45-50).  Each of the twelve `NodaTimeRows` names (lines 469-480) is a row of `Rows` (lines 124-228), and `Map_EveryRowOfTheTable_GivesItsType` (lines 261-287) maps it as a `pg_catalog` type under all three `NpgsqlColumn` values (Npgsql8, Npgsql10, None: src/SqlSource/Generation/Types/Drivers/NpgsqlColumn.cs) with no libraries and asserts the exact base-map text or failure, which is never NodaTime's; the row is found by `InternalName` before the kind is looked at (PostgresTypeMap.cs:63-80), so `ModelRuns.Catalog`'s kind does not change the path.  Only `libraries?.Find` (line 57) can give a mapping with `Library` set, so null `Library` follows.  `kills`: its 3 kills (PostgresTypeMap.cs:289) are each also killed by `Map_EveryRowOfTheTable_GivesItsType`; its survivors at 319:24 are under the cover too; no timeouts.  No comment above it; `grep -rn` of the AGENTS.md files, readmes, CONTRIBUTING.md, docs/architecture, docs/tech-debt and docs/deferred finds neither the test nor a NodaTime-off record (TD-0089 names only `Map_EveryRowOfTheTable_GivesItsType`; only a plan in docs/superpowers/plans names it, which is no record).

### F-SqlSource.Tests.Generation.Types-1-03

- test: `tests/SqlSource.Tests/Generation/Types/Maps/SqlServerTypeMapTests.cs` | `SqlServerTypeMapTests.Map_NodaTimeOff_IsTheBaseMap` | rows: all
- action: delete
- evidence: measured (6 rows, kills 2, alone 0, timeouts under it 0, rows needed 1; cover `SqlServerTypeMapTests.Map_AliasTypeOverDate_GivesLocalDate`) and read: it maps each of `date`, `time`, `datetime2`, `datetime`, `smalldatetime` and `datetimeoffset` under `SqlClient7` with no libraries and asserts only that the text is not NodaTime's and `Library` is null (SqlServerTypeMapTests.cs:361-367).  `Map_EveryRowOfTheTable_GivesItsType` (SqlServerTypeMapTests.cs:93) makes the same call for the same six names under every column, `SqlClient7` among them, and asserts the exact base-map text (`DateTime`, `TimeSpan`, `DateTimeOffset`, lines 43-62); with no libraries nothing can set `Library` (src/SqlSource/Generation/Types/Maps/SqlServerTypeMap.cs:39-87).  No comment above the test; no AGENTS.md, readme, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names it (the class is named only for `Keys_`); blind: no
- cover: `tests/SqlSource.Tests/Generation/Types/Maps/SqlServerTypeMapTests.cs` | `SqlServerTypeMapTests.Map_EveryRowOfTheTable_GivesItsType`
- proposal: Delete the theory; `NodaTimeRows` stays for `Map_NodaTimeOn_GivesEachRow`.
- verdict: confirmed
- verifier's reason: Read SqlServerTypeMapTests.cs:359-367: it maps the six `NodaTimeRows` names (lines 331-336) under `SqlClient7` with no libraries and asserts only text not NodaTime's and `Library` null.  `Map_EveryRowOfTheTable_GivesItsType` (lines 91-110) makes the same call, `TestSidecars.SqlServer(name: name)` under `SqlClient7` among its four columns, and asserts the exact text (`DateTime`, `TimeSpan`, `DateTimeOffset`, lines 43-62).  In src/SqlSource/Generation/Types/Maps/SqlServerTypeMap.cs:39-87 only `libraries?.Find` (lines 59, 81) can return a mapping with a library, so null `Library` follows when there are none.  `kills`: 2 kills at SqlServerTypeMap.cs:131:16, each also killed by 16 and 27 other methods, the cover among them; the survivor at 131:16 `parenthesis <= 0` is under the cover too; no timeouts.  No comment above it; no record document names it (the class is named in docs/architecture/generator.md and src/SqlSource/Generation/Types/AGENTS.md only for `Keys_`).

### F-SqlSource.Tests.Generation.Types-1-04

- test: `tests/SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs` | `PostgresTypeMapTests.Of_ALibrary_IsTheSameInstanceEachTime` | rows: all
- action: delete
- evidence: measured (kills 2, alone 0, timeouts under it 0; cover `PostgresTypeMapTests.Find_PostgisNameOfAnotherKind_IsNull`) and read: `LibraryRows.Of` finds a set by the mask of its libraries' bits in a table built once (src/SqlSource/Generation/Types/Maps/LibraryRows.cs:50-59, 137-166), so one library asked twice is the same path as any set asked twice.  `Of_EachSetOfLibraries_IsTheSameInstance` (PostgresTypeMapTests.cs:703) asserts a one-library set, NetTopologySuite's, is the same instance when asked again (with a repeat), besides several sets and their distinctness.  No comment above the test; no AGENTS.md, readme, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names it; blind: yes (LibraryRows.cs 41-44, 138-166 are blind, static)
- cover: `tests/SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs` | `PostgresTypeMapTests.Of_EachSetOfLibraries_IsTheSameInstance`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: Read PostgresTypeMapTests.cs:696-698 and src/SqlSource/Generation/Types/Maps/LibraryRows.cs:50-59, 137-166: `Of` returns `Sets[mask]`, built once for every mask, so one library asked twice takes the same path as any set.  `Of_EachSetOfLibraries_IsTheSameInstance` (lines 702-714) asserts the one-library set of NetTopologySuite is the same instance as `Of([NetTopologySuite, NetTopologySuite])` (line 709).  `kills`: its 2 kills are `LibraryRows.cs:53:25 i <= on.Count` and `53:39 i--`, each an out-of-range index that throws, both also killed by the cover; no timeouts, no survivors.  The rest is blind (static), so the named-cover rule holds by reading.  No comment above it; no record document names it.

### F-SqlSource.Tests.Generation.Types-1-05

- test: `tests/SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs` | `PostgresTypeMapTests.Of_SameSet_IsTheSameInstance` | rows: all
- action: delete
- evidence: measured (kills 5, alone 0, timeouts under it 0; cover `PostgresTypeMapTests.Of_EachSetOfLibraries_IsTheSameInstance`) and read: it asserts a two-library set is one instance whatever the order and repeats, and two one-library sets are different instances (PostgresTypeMapTests.cs:622-628).  `Of_EachSetOfLibraries_IsTheSameInstance` (PostgresTypeMapTests.cs:703-714) asserts exactly that on other libraries: `both` against the reversed list with a repeat, and `nts`, `noda` and `both` each distinct; the lookup is by mask (src/SqlSource/Generation/Types/Maps/LibraryRows.cs:50-59) and every mask's set is built by the same loop (LibraryRows.cs:141-163), so Json's bit is no other path.  No comment above the test; no AGENTS.md, readme, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names it; blind: yes (LibraryRows.cs 138-166 are blind, static)
- cover: `tests/SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs` | `PostgresTypeMapTests.Of_EachSetOfLibraries_IsTheSameInstance`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: Read PostgresTypeMapTests.cs:621-628 against 702-714: both assert a two-library set is one instance whatever the order with a repeat (`[Json, NodaTime]` against `[NodaTime, Json, Json]`; `[NodaTime, NetTopologySuite]` against `[NetTopologySuite, NodaTime, NodaTime]`) and that one-library sets differ (`Json` against `NodaTime`; `nts`, `noda`, `both` pairwise).  The lookup is by mask (LibraryRows.cs:50-59), every set built by one loop (141-163), and `Of([Json])` is non-null elsewhere (`Of_JsonOnly_FindsNoNodaTimeRow`, line 635), so Json's bit is no other path.  `kills`: its 5 kills (53:25 twice, 53:39, 55:13 `&=`, 55:13 `^=`, the last needing the repeat) are each also killed by the cover, which kills 7; no timeouts.  No comment above it; no record document names it (only a plan in docs/superpowers/plans).

### F-SqlSource.Tests.Generation.Types-1-06

- test: `tests/SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs` | `PostgresTypeMapTests.PlaceOf_Json_IsTheSystemTextJsonLibrary` | rows: all
- action: delete
- evidence: measured (reaches no tested mutant) and read: `LibraryRows.PlaceOf` is called only inside `LibraryRows`, to set each row's `Detail` (src/SqlSource/Generation/Types/Maps/LibraryRows.cs:224, 249, 269, 298); Json's rows take `PlaceOf(SqlLibrary.Json)` (LibraryRows.cs:249-257), and `Map_JsonOn_GivesJsonElement` (PostgresTypeMapTests.cs:649-663) asserts `mapping.Detail.ShouldBe("the System.Text.Json library")` for `json` and `jsonb` under every column, the same text.  No comment above the test; no AGENTS.md, readme, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names it; blind: yes (LibraryRows.cs 80-82 are blind, static)
- cover: `tests/SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs` | `PostgresTypeMapTests.Map_JsonOn_GivesJsonElement`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: Read PostgresTypeMapTests.cs:692-694 and 646-663, and src/SqlSource/Generation/Types/Maps/LibraryRows.cs:77-84, 246-262.  `grep -rn "PlaceOf("` over src and tests: `LibraryRows.PlaceOf` is called only at LibraryRows.cs:224, 249, 269, 298, to set each row's `Detail`; Json's rows take `PlaceOf(SqlLibrary.Json)` at 249-257, and `Map_JsonOn_GivesJsonElement` asserts `Detail.ShouldBe("the System.Text.Json library")` for `json` and `jsonb` under every column (line 661), the same exact wording, so the wording keeps its guard.  `kills`: reaches no tested mutant (LibraryRows.cs 80-82 blind, static), so judged by the named-cover rule, which the cover meets.  No comment above it; no record document names it.

### F-SqlSource.Tests.Generation.Types-1-07

- test: `tests/SqlSource.Tests/Generation/Types/Maps/SqlServerTypeMapTests.cs` | `SqlServerTypeMapTests.Map_Override_AnswersBeforeTheRow` | rows: all
- action: delete
- evidence: measured (kills 2, alone 0, timeouts under it 0; cover `SqlServerTypeMapTests.Map_AliasTypeOverDate_GivesLocalDate`) and read: it asserts an entry for `int` answers a plain `int` before its row (SqlServerTypeMapTests.cs:317-323).  A type without a user type goes through one path, the override by its key before the library rows and the row (src/SqlSource/Generation/Types/Maps/SqlServerTypeMap.cs:69-87).  `Map_OverrideAndLibrary_OverrideWins` (SqlServerTypeMapTests.cs:410) shows an entry answering a plain `date` before both the library row and the row, and `Map_AliasType_IsAskedOfTheOverrideFirstThenMappedByItsBase` (SqlServerTypeMapTests.cs:184-185) shows an entry for `nvarchar` answering at the base level of that same path.  No comment above the test; no AGENTS.md, readme, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names it; blind: no
- cover: `tests/SqlSource.Tests/Generation/Types/Maps/SqlServerTypeMapTests.cs` | `SqlServerTypeMapTests.Map_OverrideAndLibrary_OverrideWins`; `tests/SqlSource.Tests/Generation/Types/Maps/SqlServerTypeMapTests.cs` | `SqlServerTypeMapTests.Map_AliasType_IsAskedOfTheOverrideFirstThenMappedByItsBase`
- proposal: Delete the test.
- verdict: confirmed
- verifier's reason: Read SqlServerTypeMapTests.cs:316-323, 409-421, 165-186 and src/SqlSource/Generation/Types/Maps/SqlServerTypeMap.cs:39-87.  A type with no user type takes one path: `KeyOf`, the override by that key (lines 69-72), then libraries and the row; the column is read only after the override.  `Map_OverrideAndLibrary_OverrideWins` shows an entry for a plain `date` answering before both the library row and the row, and `Map_AliasType_IsAskedOfTheOverrideFirstThenMappedByItsBase` (line 184-185) an entry for `nvarchar` answering on the same path; `Map_Name_IsMatchedIgnoringCase` (line 294) asserts the key asked.  `kills`: 2 kills at SqlServerTypeMap.cs:131:16, each a `Substring(0, -1)` that throws for `int`, also killed by 16 and 27 methods including the covers; one survivor shared with them; no timeouts.  No comment above it; no record document names it (only a plan in docs/superpowers/plans).

### F-SqlSource.Tests.Generation.Types-1-08

- test: `tests/SqlSource.Tests/Generation/Types/MappedTypes/MappedTypeNamesTests.cs` | `MappedTypeNamesTests.Collect_FilesOfOneChain_GiveItsTypesOnce` | rows: all
- action: delete
- evidence: measured (kills 9, alone 0, timeouts under it 0; cover `MappedTypeNamesTests.Collect_ColumnTypes_GiveTheirTypes`) and read: its output, `["global::MyApp.Name", "long"]`, is the same whether the shared level is read once or for each file, since the names go into a `SortedSet` (src/SqlSource/Generation/Types/MappedTypes/MappedTypeNames.cs:27); the skip of a level equal by reference to the one before (MappedTypeNames.cs:32-37) is invisible to it.  What it does observe, every file's level collected and a type met twice given once, is what `Collect_EveryLevel_GivesEachTypeOnceInOrdinalOrder` (MappedTypeNamesTests.cs:33-56) asserts with two files' levels and a header type met through two queries; B's merged level is built by `SettingsLevel.Over` before `Collect` sees it.  The comment above it (lines 91-92) states behaviour, not a record; no AGENTS.md, readme, `docs/architecture/`, `docs/tech-debt/` or `docs/deferred/` names it or its class; blind: yes (MappedTypeNames.cs 26-64 do not compile under mutation)
- cover: `tests/SqlSource.Tests/Generation/Types/MappedTypes/MappedTypeNamesTests.cs` | `MappedTypeNamesTests.Collect_EveryLevel_GivesEachTypeOnceInOrdinalOrder`
- proposal: Delete the test; the read-once skip is an optimisation no output shows, and needs no test of its own.
- verdict: confirmed
- verifier's reason: Read MappedTypeNamesTests.cs:91-112 and 30-56, and src/SqlSource/Generation/Types/MappedTypes/MappedTypeNames.cs:25-64.  The names go into one `SortedSet` (line 27), so a level read once or again gives the same output; and with files A, B, C whose levels are chain, merged, chain, no two neighbours are the same object, so the skip at lines 32-37 never fires in this test at all.  What it does observe, each file's level collected and a type met twice given once, `Collect_EveryLevel_GivesEachTypeOnceInOrdinalOrder` asserts.  `kills`: its 9 kills (29:25 twice, 29:42, 32:17, 34:17, 63:16 twice, 71:13, 98:13) are each also killed by `Collect_EveryLevel_GivesEachTypeOnceInOrdinalOrder`; the 32:17 kill is explained by no level being added at all; no timeouts.  The comment at lines 91-92 states behaviour, not a record; no record document names the test or its class (only plans in docs/superpowers/plans).

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-generation-types`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-05/generator-generation-types/before --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Types.' 'Generation/Types/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-05/generator-generation-types/after --tests 'FullyQualifiedName~SqlSource.Tests.Generation.Types.' 'Generation/Types/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-05/generator-generation-types/before/reports/mutation-report.json artifacts/test-cleanup/phase-05/generator-generation-types/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-05/generator-generation-types/before/reports/mutation-report.json PostgresTypeMapTests.Map_NodaTimeOff_IsTheBaseMap
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-05/generator-generation-types/before/reports/mutation-report.json SqlServerTypeMapTests.Map_NodaTimeOff_IsTheBaseMap
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-05/generator-generation-types/before/reports/mutation-report.json PostgresTypeMapTests.Of_ALibrary_IsTheSameInstanceEachTime
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-05/generator-generation-types/before/reports/mutation-report.json PostgresTypeMapTests.Of_SameSet_IsTheSameInstance
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-05/generator-generation-types/before/reports/mutation-report.json PostgresTypeMapTests.PlaceOf_Json_IsTheSystemTextJsonLibrary
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-05/generator-generation-types/before/reports/mutation-report.json SqlServerTypeMapTests.Map_Override_AnswersBeforeTheRow
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-05/generator-generation-types/before/reports/mutation-report.json MappedTypeNamesTests.Collect_FilesOfOneChain_GiveItsTypesOnce
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-generation-types`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-05/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-05/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-05/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-05/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-05/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-05/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tests.Generation.Types-1-04, F-SqlSource.Tests.Generation.Types-1-05, F-SqlSource.Tests.Generation.Types-1-06, F-SqlSource.Tests.Generation.Types-1-08.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tests.Generation.Types-1-04 | delete | `PostgresTypeMapTests.Of_ALibrary_IsTheSameInstanceEachTime` | `tests/SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs`: `PostgresTypeMapTests.Of_EachSetOfLibraries_IsTheSameInstance` |
| F-SqlSource.Tests.Generation.Types-1-05 | delete | `PostgresTypeMapTests.Of_SameSet_IsTheSameInstance` | `tests/SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs`: `PostgresTypeMapTests.Of_EachSetOfLibraries_IsTheSameInstance` |
| F-SqlSource.Tests.Generation.Types-1-06 | delete | `PostgresTypeMapTests.PlaceOf_Json_IsTheSystemTextJsonLibrary` | `tests/SqlSource.Tests/Generation/Types/Maps/PostgresTypeMapTests.cs`: `PostgresTypeMapTests.Map_JsonOn_GivesJsonElement` |
| F-SqlSource.Tests.Generation.Types-1-08 | delete | `MappedTypeNamesTests.Collect_FilesOfOneChain_GiveItsTypesOnce` | `tests/SqlSource.Tests/Generation/Types/MappedTypes/MappedTypeNamesTests.cs`: `MappedTypeNamesTests.Collect_EveryLevel_GivesEachTypeOnceInOrdinalOrder` |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/architecture/generator.md`: 282, 284
- `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md`: 12
- `src/SqlSource/Generation/Types/AGENTS.md`: 3

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
