# Test cleanup phase 46: tests/SqlSource.Tests/Settings, strengthening

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Settings`
- Findings: 10 (10 strengthen)
- Kind: one kind, strengthen
- Changed lines: about 80
- Guard: mutation (generator-settings), with covers and run time for 3 findings

## Files

- `tests/SqlSource.Tests/Settings/ClrTypes/ClrTypeNameTests.cs`
- `tests/SqlSource.Tests/Settings/Libraries/LibrarySettingsTests.cs`
- `tests/SqlSource.Tests/Settings/PerEngineTests.cs`
- `tests/SqlSource.Tests/Settings/SettingsLevelReaderTests.cs`
- `tests/SqlSource.Tests/Settings/TypeOverrides/TypeOverrideGrammarTests.cs`
- `tests/SqlSource.Tests/Settings/TypeOverrides/UnicodeEscapeTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Settings-1-01

- test: `tests/SqlSource.Tests/Settings/ClrTypes/ClrTypeNameTests.cs` | `ClrTypeNameTests.AsWritten_Text_LeavesOutGlobalBeforeANameOfSeveralParts` | rows: all
- action: strengthen
- evidence: measured (survivor src/SqlSource/Settings/ClrTypes/ClrTypeName.cs:136:13 Equality mutation -> `at > 0`, covered by this test alone); read (every row of ClrTypeNameTests.cs:197-208 either has no `global::` or has it at index 0, so the mutant, which returns the text unchanged whenever the first `global::` is past the start, passes; a text that starts with a short name, as `Text` writes `List<App.UserId>`, is `List<global::App.UserId>` and keeps its `global::` under the mutant); blind: no
- cover: none
- proposal: Add a row whose first `global::` is not at the start, `("List<global::App.UserId>", "List<App.UserId>")`.
- verdict: confirmed
- verifier's reason: read src/SqlSource/Settings/ClrTypes/ClrTypeName.cs:133-158: under the survivor 136:13 `at > 0` a text whose first `global::` is past index 0 comes back unchanged, and a text with none goes through the loop and is appended whole, so only the first case differs; every row of ClrTypeNameTests.cs:197-208 has no `global::` or has it at index 0, so none reaches it.  The proposed row `List<global::App.UserId>` -> `List<App.UserId>` is past the start and loses its `global::` today, so it fails under the mutant.  The name promises the `global::` is left out wherever a name of several parts is written.
- survivors to kill: `src/SqlSource/Settings/ClrTypes/ClrTypeName.cs:136:13`

### F-SqlSource.Tests.Settings-1-04

- test: `tests/SqlSource.Tests/Settings/Libraries/LibrarySettingsTests.cs` | `LibrarySettingsTests.Over_NearAndFarLevels_ReplacesOnlyTheLibrariesTheNearLevelNames` | rows: all
- action: strengthen
- evidence: measured (survivors src/SqlSource/Settings/Libraries/LibrarySettings.cs:37:16 Logical mutation; 37:16 Equality mutation; 37:37 Equality mutation; 39:17 Equality mutation; 39:40 Logical mutation; 39:40 Equality mutation; 41:21 Logical mutation; 41:21 Equality mutation, each covered by 4 methods); read (LibrarySettingsTests.cs:27-34 merges two levels that both name NodaTime alone, so no library is named by one level only and the "only" of the name is never tested: under 37:16 `&&` the loop stops when the near list ends and drops the far level's other libraries; the rows of `QuerySettingsTests.Over_LibraryTypes_TakeTheNearerLevelsEntryOfALibrary`, QuerySettingsTests.cs:457-461, are NodaTime alone too, though its comment promises "the farther one's for any it does not"); blind: yes (lines 34, 37, 39 and 41 also hold mutants that do not compile)
- cover: none
- proposal: Give the far level a second library the near one does not name, NetTopologySuite or Json, and the near level one the far does not, and assert the merge holds each, sorted by `SqlLibrary`, with the near entry for NodaTime.
- verdict: confirmed
- verifier's reason: read src/SqlSource/Settings/Libraries/LibrarySettings.cs:24-48 and LibrarySettingsTests.cs:26-34: both levels name NodaTime alone, so with one entry each the loop under 37:16 `&&`, 39:17 `!=` or 41:21 `||` still gives the near entry; a far level with a second library would lose it, or the near level's own extra library would be dropped or misplaced.  `kills` lists those survivors as SURVIVED under the test.  The other three methods covering them, `Over_ANearDefaultEntry_IsKept`, `Over_LibraryTypes_TakeTheNearerLevelsEntryOfALibrary` (QuerySettingsTests.cs:456-476, whose `Libraries` helper at 502-506 writes NodaTime alone) and `ResolveLibraries_MarkersOverFile_GivesTheMarkersEntry`, name one library too.  The name's "only" is never tested.
- survivors to kill: `src/SqlSource/Settings/Libraries/LibrarySettings.cs:37:16`

### F-SqlSource.Tests.Settings-1-05

- test: `tests/SqlSource.Tests/Settings/Libraries/LibrarySettingsTests.cs` | `LibrarySettingsTests.Over_OneNull_IsTheOtherItself` | rows: all
- action: strengthen
- evidence: measured (covers 2 mutants and kills none; survivor src/SqlSource/Settings/Libraries/LibrarySettings.cs:30:9 Block removal mutation, covered by this test alone); read (LibrarySettingsTests.cs:18-24 compares with `ShouldBe`, which is `EquatableArray`'s value equality, so a merged copy passes; the name and the doc of `LibrarySettings.Over`, LibrarySettings.cs:17, promise "either one itself"); blind: yes (line 29 holds mutants that do not compile)
- cover: none
- proposal: Assert the "itself": that `Over(entries, null)` and `Over(null, entries)` allocate nothing, measured with `GC.GetAllocatedBytesForCurrentThread` as `PerEngineAllocationTests` does.
- verdict: confirmed
- verifier's reason: read LibrarySettingsTests.cs:17-24 and LibrarySettings.cs:15-32: the doc says "either one itself when the other is" and the name says "Itself", but `ShouldBe` on `EquatableArray` compares values, so the survivor 30:9 block removal, which merges with an empty list into a new array, passes; `kills` shows it SURVIVED and no kill.  Measuring with `GC.GetAllocatedBytesForCurrentThread` is how tests/SqlSource.Tests/Settings/PerEngineAllocationTests.cs:69-75 measures, and tests/AGENTS.md allows it for synchronous code.
- survivors to kill: `src/SqlSource/Settings/Libraries/LibrarySettings.cs:30:9`
- a cover: `LibrarySettingsTests.Over_OneNull_IsTheOtherItself` is the cover of F-SqlSource.Tests.Settings-1-14 (phase 47).  The change keeps every assertion and row that makes it so.

### F-SqlSource.Tests.Settings-1-06

- test: `tests/SqlSource.Tests/Settings/PerEngineTests.cs` | `PerEngineTests.Indexer_ValueOfNoMember_Throws` | rows: all
- action: strengthen
- evidence: measured (survivor src/SqlSource/Settings/PerEngine.cs:30:17 Equality mutation -> `(uint)(int)engine > (uint)DatabaseEngines.All.Length`, covered by 52 methods); read (PerEngineTests.cs:152-158 uses `(DatabaseEngine)7` alone; under the mutant `(DatabaseEngine)2`, the first number past the last member, reads `default(T)` from a container without an array and throws `IndexOutOfRangeException`, not `UnexpectedEnumValueException`, from one with an array); blind: no (line 30 holds other mutants that do not compile)
- cover: none
- proposal: Use `(DatabaseEngine)DatabaseEngines.All.Length` as the value of no member, beside or in place of 7, for both containers.
- verdict: confirmed
- verifier's reason: read src/SqlSource/Settings/PerEngine.cs:25-37 and PerEngineTests.cs:151-158: `DatabaseEngine` has two members (src/SqlSource/Settings/DatabaseEngine.cs:9-16), so under 30:17 `>` the value 7 still throws, while 2 passes the check and returns `default` from a container without an array or throws `IndexOutOfRangeException` from `_slots[2]`, neither of which is the `UnexpectedEnumValueException` the test expects.  The name promises the throw for any value of no member.
- survivors to kill: `src/SqlSource/Settings/PerEngine.cs:30:17`

### F-SqlSource.Tests.Settings-1-07

- test: `tests/SqlSource.Tests/Settings/PerEngineTests.cs` | `PerEngineTests.With_ValueOfNoMember_Throws` | rows: all
- action: strengthen
- evidence: measured (survivor src/SqlSource/Settings/PerEngine.cs:48:13 Equality mutation -> `(uint)(int)engine > (uint)DatabaseEngines.All.Length`, covered by 31 methods); read (PerEngineTests.cs:144-145 and `Only_ValueOfNoMember_Throws`, PerEngineTests.cs:148-149, which goes through `With`, use `(DatabaseEngine)7`; under the mutant `With((DatabaseEngine)2, "a")` passes the check, matches no engine and returns a copy without throwing); blind: no (line 48 holds other mutants that do not compile)
- cover: none
- proposal: Use `(DatabaseEngine)DatabaseEngines.All.Length` as the value of no member, so the boundary of the check is tested.
- verdict: confirmed
- verifier's reason: read PerEngine.cs:43-60 and PerEngineTests.cs:143-149: under 48:13 `>` the value 2 passes the check, the loop over `DatabaseEngines.All` never matches it, and `With` returns a copy without throwing; both `With_ValueOfNoMember_Throws` and `Only_ValueOfNoMember_Throws` (which calls `With`, PerEngine.cs:174) use 7, which the mutant still refuses.
- survivors to kill: `src/SqlSource/Settings/PerEngine.cs:48:13`

### F-SqlSource.Tests.Settings-1-16

- test: `tests/SqlSource.Tests/Settings/SettingsLevelReaderTests.cs` | `SettingsLevelReaderTests.Read_LibraryTypesAsMembers_AreReadIntoTheLevel` | rows: all
- action: strengthen
- evidence: measured (survivor src/SqlSource/Settings/SettingsLevelReader.cs:155:52 PostIncrementExpression to PostDecrementExpression mutation, covered by this test and `Read_LibraryTypesWithARepeatedLibrary_IsAReportedRepeatedKey`); read (SettingsLevelReaderTests.cs:389-408 reads one member, and no test of the file reads two different libraries: under the mutant a second library's search reads `entries[-1]` and throws, and the sort of SettingsLevelReader.cs:149 is never exercised); blind: yes (SettingsLevelReader.cs:115-151, the reading loop and the sort, hold mutants that do not compile)
- cover: none
- proposal: Read two members of different libraries written out of the order of `SqlLibrary`, as `json` then `NodaTime`, and assert both entries sorted by library and `LibraryEntries` holding each with its member's index.
- verdict: confirmed
- verifier's reason: read src/SqlSource/Settings/SettingsLevelReader.cs:112-161 and SettingsLevelReaderTests.cs:388-445: line 155 is the loop of `IndexOfLibrary`; with one entry or a repeat that matches at index 0 the `index--` mutant is never stepped, and no test of the file reads two valid members of different libraries (`Read_LibraryTypesWithAnInvalidMember_IsReportedByIndex` has two invalid ones), so the sort at line 149 never orders anything.  Under the mutant a second library reads `entries[-1]` and throws.  `Json` is a member of `SqlLibrary` (src/SqlSource/Settings/Libraries/SqlLibrary.cs:7-17), after `NodaTime`, so the proposed out-of-order pair works.
- survivors to kill: `src/SqlSource/Settings/SettingsLevelReader.cs:155:52`

### F-SqlSource.Tests.Settings-1-21

- test: `tests/SqlSource.Tests/Settings/TypeOverrides/TypeOverrideGrammarTests.cs` | `TypeOverrideGrammarTests.BracketHelp_KeyInBrackets_ShowsTheFormsThatAreRead` | rows: all
- action: strengthen
- evidence: measured (survivors src/SqlSource/Settings/TypeOverrides/TypeOverrideGrammar.cs:293:22 Equality mutation -> `at + 1 <= entry.Length` and 293:22 Arithmetic mutation -> `at - 1`, and 249:17 Equality mutation -> `index <= entry.Length`, each covered by the three BracketHelp tests); read (every row at TypeOverrideGrammarTests.cs:388-410 ends in `=X`, but `BracketHelp` also takes a settings file's key alone, TypeOverrideGrammar.cs:124, as `SettingsFileDiagnostics.HelpOf` passes it; with a key alone the closing `]` is the last character, and each mutant reads past the end and throws, which a key a user writes must never make the generator do); blind: no
- cover: none
- proposal: Add rows of a key alone, without `=`, such as `[dbo].[Email]` and `dbo.[Email]`, with the same help as their entries.
- verdict: confirmed
- verifier's reason: read src/SqlSource/Settings/TypeOverrides/TypeOverrideGrammar.cs:115-140 and 200-306: the doc takes "a settings file's key alone", and with `[dbo].[Email]` alone the last `]` is the last character, so 293:22 (`<=` and `at - 1`) and 249:17 `<=` index `entry[entry.Length]` and throw; every row of TypeOverrideGrammarTests.cs:386-411 ends in `=X`.  Outside the scope, `SettingsFileDiagnosticsTests.Create_TypeOverrideKeyInBrackets_GivesTheBracketHelp` (tests/SqlSource.Tests/Diagnostics/SettingsFileDiagnosticsTests.cs:107-119) sends a key alone through `HelpOf` to `BracketHelp` and would fail on the throw, so the gap is guarded at the diagnostic's level; the unit theory whose name promises the help for a key in brackets still lacks the form, and the finding reads it rightly.
- survivors to kill: `src/SqlSource/Settings/TypeOverrides/TypeOverrideGrammar.cs:293:22`

### F-SqlSource.Tests.Settings-1-22

- test: `tests/SqlSource.Tests/Settings/TypeOverrides/UnicodeEscapeTests.cs` | `UnicodeEscapeTests.TryRead_AnythingElse_IsFalse` | rows: all
- action: strengthen
- evidence: measured (survivors src/SqlSource/Settings/TypeOverrides/UnicodeEscape.cs:26:26 Equality mutation -> `> 0xDC00` and 26:40 Equality mutation -> `< 0xDFFF`, each covered by 20 methods); read (the only lone low surrogate among the rows at UnicodeEscapeTests.cs:50-61 is `\DE00`, inside the range, so a lone `\DC00` or `\DFFF`, the ends of the range, is read as a character under the mutants); blind: no
- cover: none
- proposal: Add the rows `\DC00` and `\DFFF`.
- verdict: confirmed
- verifier's reason: read src/SqlSource/Settings/TypeOverrides/UnicodeEscape.cs:17-53 and UnicodeEscapeTests.cs:49-63: line 26 refuses a lone low surrogate, and the only one among the rows is `\DE00` (line 59, inside the range), so `> 0xDC00` and `< 0xDFFF` let `\DC00` and `\DFFF` through as one character.  The name promises false for them.
- survivors to kill: `src/SqlSource/Settings/TypeOverrides/UnicodeEscape.cs:26:26`

### F-SqlSource.Tests.Settings-1-23

- test: `tests/SqlSource.Tests/Settings/TypeOverrides/UnicodeEscapeTests.cs` | `UnicodeEscapeTests.TryRead_EscapeOfACharacterOfTheBmp_IsThatCharacterAlone` | rows: all
- action: strengthen
- evidence: measured (survivors src/SqlSource/Settings/TypeOverrides/UnicodeEscape.cs:44:13 Equality mutation -> `codePoint >= 0xFFFF`, covered by 17 methods; 92:29 Equality mutation -> `> 1` and 99:24 Equality mutation -> `< 'f'`, covered by 20 and 21 methods); read (the rows at UnicodeEscapeTests.cs:11-15 read neither end of the BMP's range, `\0001` or `\FFFF`, nor the hex digit `f`: under the mutants `\FFFF` goes through the surrogates' formula and becomes two other characters, and `\0001` and `\00ff` are refused); blind: no (lines 98-100 hold other mutants that do not compile)
- cover: none
- proposal: Add the rows `("\\0001", '\u0001', 5)`, `("\\FFFF", '\uFFFF', 5)` and `("\\00ff", 'ÿ', 5)`.
- verdict: confirmed
- verifier's reason: read UnicodeEscape.cs:44-53, 91-104 and UnicodeEscapeTests.cs:10-23: under 44:13 `>= 0xFFFF` the escape `\FFFF` goes through the surrogate formula and gives `\uD7FF`, `\uDFFF`, which fails `second.ShouldBe('\0')`; under 92:29 `> 1` `\0001` is refused; under 99:24 `< 'f'` the digit `f` is no hex digit and `\00ff` is refused.  The rows read `\000A`, `\\`, `\00e9`, `\+000041` and `\0041b`, none of which reaches them, and each proposed row decodes as stated today.
- survivors to kill: `src/SqlSource/Settings/TypeOverrides/UnicodeEscape.cs:44:13`
- condition: The proposal holds U+FFFF as a literal character: write it as `\uFFFF` in the test.

### F-SqlSource.Tests.Settings-1-24

- test: `tests/SqlSource.Tests/Settings/TypeOverrides/UnicodeEscapeTests.cs` | `UnicodeEscapeTests.TryRead_EscapeOfACharacterOutsideTheBmp_IsItsSurrogates` | rows: all
- action: strengthen
- evidence: measured (survivors src/SqlSource/Settings/TypeOverrides/UnicodeEscape.cs:31:40 Equality mutation -> `< 0xDBFF` and 92:38 Equality mutation -> `< 0x10FFFF`, covered by 20 methods; 34:80 Equality mutation -> `<= 0xDC00` and 34:92 Equality mutation -> `>= 0xDFFF`, covered by 4 methods); read (UnicodeEscapeTests.cs:26-38 reads U+1F600 alone, whose surrogates `\D83D\DE00` are inside both ranges; the ends, U+10000 as `\D800\DC00` and U+10FFFF as `\+10FFFF` or `\DBFF\DFFF`, are refused or read as one character under the mutants); blind: no (line 34 holds other mutants that do not compile)
- cover: none
- proposal: Take the expected surrogates as arguments and add the rows `\+010000` and `\D800\DC00` for (`\uD800`, `\uDC00`), and `\+10FFFF` and `\DBFF\DFFF` for (`\uDBFF`, `\uDFFF`).
- verdict: confirmed
- verifier's reason: read UnicodeEscape.cs:31-50 and UnicodeEscapeTests.cs:25-38: every row is U+1F600, whose surrogates `D83D`/`DE00` sit inside both ranges; under 31:40 `< 0xDBFF` `\DBFF\DFFF` gives `DBFF` alone, under 34:80 `<= 0xDC00` and 34:92 `>= 0xDFFF` `\D800\DC00` and `\DBFF\DFFF` are refused, and under 92:38 `< 0x10FFFF` `\+10FFFF` is refused.  U+10000 is (`\uD800`, `\uDC00`) and U+10FFFF (`\uDBFF`, `\uDFFF`) by the formula of lines 46-47, as the proposal states.
- survivors to kill: `src/SqlSource/Settings/TypeOverrides/UnicodeEscape.cs:31:40`

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-settings`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-46/generator-settings/before --tests 'FullyQualifiedName~SqlSource.Tests.Settings.' 'Settings/**/*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-46/generator-settings/after --tests 'FullyQualifiedName~SqlSource.Tests.Settings.' 'Settings/**/*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-46/generator-settings/before/reports/mutation-report.json artifacts/test-cleanup/phase-46/generator-settings/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-46/generator-settings/before/reports/mutation-report.json ClrTypeNameTests.AsWritten_Text_LeavesOutGlobalBeforeANameOfSeveralParts
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-46/generator-settings/before/reports/mutation-report.json LibrarySettingsTests.Over_NearAndFarLevels_ReplacesOnlyTheLibrariesTheNearLevelNames
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-46/generator-settings/before/reports/mutation-report.json LibrarySettingsTests.Over_OneNull_IsTheOtherItself
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-46/generator-settings/before/reports/mutation-report.json PerEngineTests.Indexer_ValueOfNoMember_Throws
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-46/generator-settings/before/reports/mutation-report.json PerEngineTests.With_ValueOfNoMember_Throws
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-46/generator-settings/before/reports/mutation-report.json SettingsLevelReaderTests.Read_LibraryTypesAsMembers_AreReadIntoTheLevel
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-46/generator-settings/before/reports/mutation-report.json TypeOverrideGrammarTests.BracketHelp_KeyInBrackets_ShowsTheFormsThatAreRead
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-46/generator-settings/before/reports/mutation-report.json UnicodeEscapeTests.TryRead_AnythingElse_IsFalse
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-46/generator-settings/before/reports/mutation-report.json UnicodeEscapeTests.TryRead_EscapeOfACharacterOfTheBmp_IsThatCharacterAlone
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-46/generator-settings/before/reports/mutation-report.json UnicodeEscapeTests.TryRead_EscapeOfACharacterOutsideTheBmp_IsItsSurrogates
```

For each **strengthen**, every survivor its finding names is `Killed` in the "after" report:

- F-SqlSource.Tests.Settings-1-01: `src/SqlSource/Settings/ClrTypes/ClrTypeName.cs:136:13`
- F-SqlSource.Tests.Settings-1-04: `src/SqlSource/Settings/Libraries/LibrarySettings.cs:37:16`
- F-SqlSource.Tests.Settings-1-05: `src/SqlSource/Settings/Libraries/LibrarySettings.cs:30:9`
- F-SqlSource.Tests.Settings-1-06: `src/SqlSource/Settings/PerEngine.cs:30:17`
- F-SqlSource.Tests.Settings-1-07: `src/SqlSource/Settings/PerEngine.cs:48:13`
- F-SqlSource.Tests.Settings-1-16: `src/SqlSource/Settings/SettingsLevelReader.cs:155:52`
- F-SqlSource.Tests.Settings-1-21: `src/SqlSource/Settings/TypeOverrides/TypeOverrideGrammar.cs:293:22`
- F-SqlSource.Tests.Settings-1-22: `src/SqlSource/Settings/TypeOverrides/UnicodeEscape.cs:26:26`
- F-SqlSource.Tests.Settings-1-23: `src/SqlSource/Settings/TypeOverrides/UnicodeEscape.cs:44:13`
- F-SqlSource.Tests.Settings-1-24: `src/SqlSource/Settings/TypeOverrides/UnicodeEscape.cs:31:40`

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-settings`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-46/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-46/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-46/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-46/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-46/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-46/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tests.Settings-1-04, F-SqlSource.Tests.Settings-1-05, F-SqlSource.Tests.Settings-1-16.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

No document names a test of the phase.

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
