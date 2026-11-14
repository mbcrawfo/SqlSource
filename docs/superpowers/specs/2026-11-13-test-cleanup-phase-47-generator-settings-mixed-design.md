# Test cleanup phase 47: tests/SqlSource.Tests/Settings, mixed actions

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Settings`
- Findings: 7 (3 delete, 1 fold, 3 strengthen)
- Kind: mixed: its files hold findings of several kinds, or a finding ties them to such a file
- Changed lines: about 64
- Guard: mutation (generator-settings)

## Files

- `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs`
- `tests/SqlSource.Tests/Settings/TypeOverrides/TypeKeysTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Settings-1-08

- test: `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs` | `QuerySettingsTests.Resolve_WithoutListPadding_PadsLists` | rows: all
- action: delete
- evidence: measured (kills 3, alone 0, cover `GeneratorParameterChoicesTests.Over_OptionNotNamedAbove_KeepsTheLowerState`); read (QuerySettingsTests.cs:90-92 resolves an empty level over one naming keep-comments and asserts `PadsLists`; QuerySettingsTests.cs:38 asserts `PadsLists` is on when no level says anything, and QuerySettingsTests.cs:51-57 asserts that levels naming other parameters leave list-padding at its default, on, in the resolved values; no comment above the test, and the class comment is the resolution rule; `grep -rn` of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ names neither the test nor `QuerySettingsTests`); blind: no
- cover: `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs` | `QuerySettingsTests.Resolve_NoLevelSaysAnything_GivesTheDefaults`; `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs` | `QuerySettingsTests.Resolve_Parameters_MergePerOption`
- proposal: Delete it.
- verdict: confirmed
- verifier's reason: read QuerySettingsTests.cs:90-92: `new SettingsLevel()` has the default parameters, as `SettingsLevel.None` has (QuerySettingsTests.cs:122-127), and `QuerySettings.Resolve` reads only `markers.Parameters.Over(file.Parameters)` for them (src/SqlSource/Settings/QuerySettings.cs:102-104), so it is `Resolve(None, Keep)`, which line 50 resolves and lines 51-57 check gives `Defaults | KeepComments | SortInput`, list-padding on; line 38 checks the `PadsLists` property itself.  Ran `kills`: its 3 kills, GeneratorParameterValues.cs:26:60, 36:56 and 44:40, each explained by reading a parameter after a merge, are all in the union of the two named covers' kills, and it has no timeout.  No comment above it, the class comment is the resolution rule, and the grep of the record documents names neither the test nor `QuerySettingsTests`.

### F-SqlSource.Tests.Settings-1-14

- test: `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs` | `QuerySettingsTests.Over_LibraryTypesOfOnlyOneLevel_AreThatLevelsItself` | rows: all
- action: delete
- evidence: measured (kills 1, alone 0, cover `QuerySettingsTests.Over_ColumnTypes_MergePerKey`); read (QuerySettingsTests.cs:479-485 merges a level with `SettingsLevel.None`, so `SettingsLevel.Over` returns a level itself at SettingsLevel.cs:106-114 and never reaches `LibrarySettings.Over` at line 129; that shortcut is asserted, by reference, at QuerySettingsTests.cs:106-107, and `LibrarySettings.Over` with one side null at LibrarySettingsTests.cs:18-24; no comment above the test; `grep -rn` of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ names neither the test nor `QuerySettingsTests`); blind: no
- cover: `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs` | `QuerySettingsTests.Over_EachMember_ComesFromThisLevelWhenItHasOne`; `tests/SqlSource.Tests/Settings/Libraries/LibrarySettingsTests.cs` | `LibrarySettingsTests.Over_OneNull_IsTheOtherItself`
- proposal: Delete it: its library types never reach the merge it is named for.
- verdict: confirmed
- verifier's reason: read QuerySettingsTests.cs:478-485 and SettingsLevel.cs:104-114: both calls merge with `SettingsLevel.None`, so `Over` returns a level itself before line 129 and `LibrarySettings.Over` is never called.  Ran `kills`: its one kill, SettingsLevel.cs:106:13 negate, is killed by `Over_EachMember_ComesFromThisLevelWhenItHasOne`, whose lines 106-107 assert the same shortcut by reference, which implies the equal `LibraryTypes`; `LibrarySettingsTests.Over_OneNull_IsTheOtherItself` (LibrarySettingsTests.cs:17-24) holds `LibrarySettings.Over` with one side null.  No comment above it, and the grep of the record documents names neither the test nor `QuerySettingsTests`.

### F-SqlSource.Tests.Settings-1-19

- test: `tests/SqlSource.Tests/Settings/TypeOverrides/TypeKeysTests.cs` | `TypeKeysTests.TryNormalizePostgres_SysSchema_IsAUsersSchema` | rows: all
- action: delete
- evidence: measured (kills 25, alone 0, cover `TypeKeysTests.TryNormalizeEach_Key_IsWhatEachEnginesReadingGives`); read (TypeKeysTests.cs:250 asserts that PostgreSQL reads `sys.int` as `sys.int`; the row `("sys.int", "sys.int", "int")` at TypeKeysTests.cs:257 asserts the same through `TryNormalizePostgres` itself at line 270, and SQL Server's reading beside it; no comment above the test; `grep -rn` of the AGENTS.md files, the readmes, CONTRIBUTING.md, docs/architecture/, docs/tech-debt/ and docs/deferred/ names neither the test nor `TypeKeysTests`); blind: no
- cover: `tests/SqlSource.Tests/Settings/TypeOverrides/TypeKeysTests.cs` | `TypeKeysTests.TryNormalizeEach_Key_IsWhatEachEnginesReadingGives`
- proposal: Delete it.
- verdict: confirmed
- verifier's reason: read TypeKeysTests.cs:249-250 and 252-274: the row `("sys.int", "sys.int", "int")` at line 257 asserts `TryNormalizePostgres("sys.int")` gives `sys.int` at line 270, the same input and the same call.  Ran `kills`: its 25 kills are all among the theory's 192, and its one timeout, TypeKeyText.cs:202:17, is under the theory too.  No comment above it (the comment at line 242 is the previous test's), and the grep of the record documents names neither the test nor `TypeKeysTests`.

### F-SqlSource.Tests.Settings-1-20

- test: `tests/SqlSource.Tests/Settings/TypeOverrides/TypeKeysTests.cs` | `TypeKeysTests.TryNormalizeEach_ArrayOfAUsersSchema_IsPostgreSqlsAlone`; `TypeKeysTests.TryNormalizeEach_EscapedKeyOfAUsersSchema_IsRewrittenForEachEngine`; `TypeKeysTests.TryNormalizeEach_Key_IsWhatEachEnginesReadingGives` | rows: all
- action: fold
- evidence: measured (the two facts: kills 52 and 147, alone 0, each with the cover `TypeKeysTests.TryNormalizeEach_Key_IsWhatEachEnginesReadingGives`); read (TypeKeysTests.cs:290-296, `public.tag[]` giving PostgreSQL's key alone, is the theory's row `("dbo.Email[]", "dbo.Email[]", null)` at line 262, and TypeKeysTests.cs:300-306, an escaped part of a user's schema rewritten for each engine, is its row `("dbo.U&\"Em\\0061il\"", "dbo.\"Email\"", "dbo.\"Email\"")` at line 261, which also check each engine's own reading; the facts' comments state the rules, and `grep -rn` of the record documents names no test of `TypeKeysTests`); blind: no
- cover: `tests/SqlSource.Tests/Settings/TypeOverrides/TypeKeysTests.cs` | `TypeKeysTests.TryNormalizeEach_Key_IsWhatEachEnginesReadingGives`
- proposal: Delete the two facts and move their comments onto rows 261 and 262 of the theory, which already hold their cases.
- verdict: confirmed
- verifier's reason: read TypeKeysTests.cs:288-306 and 252-274: `public.tag[]` giving PostgreSQL's key and null for SQL Server is the case of row 262 `dbo.Email[]`, and the escaped `public.U&"st\0061tus"` rewritten to the quoted form for both is the case of row 261 `dbo.U&"Em\0061il"`; only the schema's name differs, and SQL Server treats every schema but `sys` as a user's.  The theory also checks each engine's own reading.  Ran `kills`: the facts' 52 and 147 kills are all among the theory's, and their timeouts, TypeKeyText.cs:202:17 and 221:17, are under the theory too.  Their comments state rules, not a limit, regression or decision, and the proposal keeps them on the rows; the grep of the record documents names no test of `TypeKeysTests` (only a plan of docs/superpowers/plans does).
- a cover: `TypeKeysTests.TryNormalizeEach_Key_IsWhatEachEnginesReadingGives` is the cover of F-SqlSource.Tests.Settings-1-19 (phase 47).  The change keeps every assertion and row that makes it so.

### F-SqlSource.Tests.Settings-1-09

- test: `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs` | `QuerySettingsTests.Over_EachMember_ComesFromThisLevelWhenItHasOne` | rows: all
- action: strengthen
- evidence: measured (survivors, each covered by 11 methods: src/SqlSource/Settings/SettingsLevel.cs:119:22 Null coalescing mutation (remove right); 120:24 (left to right) and (remove left); 121:32, 122:33, 123:30, 124:30, 125:31 and 126:30, each (left to right), (remove left) and (remove right); 131:30 (remove right); 132:26 (left to right) and (remove left)); read (QuerySettingsTests.cs:101-108 sets `Output` and the parameters alone, then checks the two shortcuts for `SettingsLevel.None`, so no member but `Output` is ever taken from this level or from the other); blind: no
- cover: none
- proposal: Build two levels that each set every member that resolves whole, with different values, and assert that the first over the second has the first's value of each, and that a level that sets only an unrelated member, not `None`, over the full one has the full one's value of each.
- verdict: confirmed
- verifier's reason: read QuerySettingsTests.cs:100-108 and src/SqlSource/Settings/SettingsLevel.cs:104-134: the full merge is reached only by `output.Over(Sort)`, where `output` sets `Output` and `Sort` the parameters, so no member but those two is set on either side; the two other lines take the `None` shortcuts.  The survivors on 119-132 listed in the evidence are each a member whose `??` is never exercised with a value on one side, which the name, "each member", promises.
- survivors to kill: `src/SqlSource/Settings/SettingsLevel.cs:119:22`
- a cover: `QuerySettingsTests.Over_EachMember_ComesFromThisLevelWhenItHasOne` is the cover of F-SqlSource.Tests.Settings-1-14 (phase 47).  The change keeps every assertion and row that makes it so.

### F-SqlSource.Tests.Settings-1-11

- test: `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs` | `QuerySettingsTests.Resolve_EachModelSetting_ComesFromTheMostSpecificLevelThatHasIt` | rows: all
- action: strengthen
- evidence: measured (survivors, each covered by 15 or 16 methods: src/SqlSource/Settings/QuerySettings.cs:106:13 Null coalescing mutation (remove left); 107:13 (remove left); 108:13 (remove right); 109:39 (remove left); 110:13 (remove left); 111:39 (remove left)); read (QuerySettingsTests.cs:163-189 has the markers set `ModelNamespace`, `InputModelType` and `CollectionType` alone, so the file's value of those three is never needed and the markers' value of the other three is never given: dropping the markers' suffixes and output model type, or the file's namespace, input model type and collection type, passes); blind: no
- cover: none
- proposal: Resolve twice: once with markers that set all six over the file, asserting the markers' six, and once with `SettingsLevel.None` over the file, asserting the file's six.
- verdict: confirmed
- verifier's reason: read QuerySettingsTests.cs:162-189 against QuerySettings.cs:106-111: the markers set the namespace, the input model type and the collection type and the file sets all six, so the file's value of those three is never needed (108:13 remove right, 109:39 and 111:39 remove left survive) and the markers' suffixes and output model type are never given (106:13, 107:13 and 110:13 remove left survive).  The name promises each from the most specific level that has it.
- survivors to kill: `src/SqlSource/Settings/QuerySettings.cs:106:13`

### F-SqlSource.Tests.Settings-1-18

- test: `tests/SqlSource.Tests/Settings/TypeOverrides/TypeKeysTests.cs` | `TypeKeysTests.TryNormalizePostgres_AnythingElse_IsNotAKey` | rows: all
- action: strengthen
- evidence: measured (survivor src/SqlSource/Settings/TypeOverrides/TypeKeyText.cs:49:24 Boolean mutation -> `true`, covered by this test and `TryNormalizePostgres_EscapedPartThatIsNotValid_IsNotAKey`); read (when the part after a `.` cannot be read the mutant keeps the first part as the name; the rows `public.` and `pg_catalog.` at TypeKeysTests.cs:88-89 name no known type, so they stay refused, but `int4.` would be read as `int4`); blind: no
- cover: none
- proposal: Add the row `int4.`, a known name followed by a `.` and nothing; a row `a.` in `TypeKeyTextTests.TrySplit_AnythingElse_IsFalse` would prove the same at `TrySplit` itself.
- verdict: confirmed
- verifier's reason: read src/SqlSource/Settings/TypeOverrides/TypeKeyText.cs:26-57: line 49 is the `return false` after a `.` whose second part cannot be read; under `true` the key keeps its first part as an unqualified name, since `schema = name` comes after.  `TryNormalizePostgres` reads through `TrySplit` (TypeKeys.cs:60, 82, 94), so `public.` and `pg_catalog.` (TypeKeysTests.cs:88-89) stay refused as unknown names, while `int4.` would be read as `int4`.  `TrySplit_AnythingElse_IsFalse` (TypeKeyTextTests.cs:50-56) has no `a.` row.
- survivors to kill: `src/SqlSource/Settings/TypeOverrides/TypeKeyText.cs:49:24`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-settings`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-47/generator-settings/before --tests 'FullyQualifiedName~SqlSource.Tests.Settings.' 'Settings/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-47/generator-settings/after --tests 'FullyQualifiedName~SqlSource.Tests.Settings.' 'Settings/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-47/generator-settings/before/reports/mutation-report.json artifacts/test-cleanup/phase-47/generator-settings/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-47/generator-settings/before/reports/mutation-report.json QuerySettingsTests.Resolve_WithoutListPadding_PadsLists
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-47/generator-settings/before/reports/mutation-report.json QuerySettingsTests.Over_EachMember_ComesFromThisLevelWhenItHasOne
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-47/generator-settings/before/reports/mutation-report.json QuerySettingsTests.Resolve_EachModelSetting_ComesFromTheMostSpecificLevelThatHasIt
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-47/generator-settings/before/reports/mutation-report.json QuerySettingsTests.Over_LibraryTypesOfOnlyOneLevel_AreThatLevelsItself
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-47/generator-settings/before/reports/mutation-report.json TypeKeysTests.TryNormalizePostgres_AnythingElse_IsNotAKey
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-47/generator-settings/before/reports/mutation-report.json TypeKeysTests.TryNormalizePostgres_SysSchema_IsAUsersSchema
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-47/generator-settings/before/reports/mutation-report.json TypeKeysTests.TryNormalizeEach_ArrayOfAUsersSchema_IsPostgreSqlsAlone
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-47/generator-settings/before/reports/mutation-report.json TypeKeysTests.TryNormalizeEach_EscapedKeyOfAUsersSchema_IsRewrittenForEachEngine
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-47/generator-settings/before/reports/mutation-report.json TypeKeysTests.TryNormalizeEach_Key_IsWhatEachEnginesReadingGives
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Settings-1-09: `src/SqlSource/Settings/SettingsLevel.cs:119:22`
- F-SqlSource.Tests.Settings-1-11: `src/SqlSource/Settings/QuerySettings.cs:106:13`
- F-SqlSource.Tests.Settings-1-18: `src/SqlSource/Settings/TypeOverrides/TypeKeyText.cs:49:24`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-settings`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-47/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-47/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-47/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-47/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-47/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-47/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- F-SqlSource.Tests.Settings-1-10 (keep, `QuerySettingsTests.Resolve_ModelSettings_HaveTheirDefaults`): a **keep**; the test stays as it is.
- F-SqlSource.Tests.Settings-1-12 (keep, `QuerySettingsTests.Resolve_PlacementSettings_HaveTheirDefaults`): a **keep**; the test stays as it is.
- F-SqlSource.Tests.Settings-1-13 (keep, `QuerySettingsTests.ResolveColumnTypes_NoLevelSetsOne_IsEmpty`): a **keep**; the test stays as it is.
- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
