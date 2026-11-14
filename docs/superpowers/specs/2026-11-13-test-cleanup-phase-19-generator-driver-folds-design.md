# Test cleanup phase 19: tests/SqlSource.Tests/Generator, folds

Part of the test cleanup epic, [2026-11-13-test-cleanup-epic-design.md](2026-11-13-test-cleanup-epic-design.md), from the test review of 2026-11-13; the appendix is [2026-11-13-test-cleanup-findings.md](2026-11-13-test-cleanup-findings.md).  The review read commit `36864a86587181e09dcf183d25f4ffa50d22fc94`: a line number below is of that commit, and the phase finds each test by its name.

## Scope

- Folder: `tests/SqlSource.Tests/Generator`
- Findings: 8 (8 fold)
- Kind: one kind, fold
- Changed lines: about 106
- Guard: mutation (generator-root), with covers and run time for 6 findings

## Files

- `tests/SqlSource.Tests/Generator/ArrayPoolParameterTests.cs`
- `tests/SqlSource.Tests/Generator/DialectTests.cs`
- `tests/SqlSource.Tests/Generator/ModelNameTests.cs`
- `tests/SqlSource.Tests/Generator/NestedClassTests.cs`
- `tests/SqlSource.Tests/Generator/PlacementDiagnosticsTests.cs`

## Findings

Each finding as the review wrote it, with its verifier's reason.  A **condition** is a correction or an addition that a verdict, the run notes or the writer carries into the change.

### F-SqlSource.Tests.Generator-1-02

- test: `tests/SqlSource.Tests/Generator/ArrayPoolParameterTests.cs` | `ArrayPoolParameterTests.Run_ArrayPool_ComesFromTheMostSpecificLevelThatNamesIt` | rows: `(Unpooled, Other, null, null, false)` at line 29, `(Default, Other, Unpooled, null, false)` at line 32, `(null, null, Unpooled, Other, false)` at line 34
- action: fold
- evidence: measured (13 rows, kills 57, alone 0, timeouts under it 12, rows needed 1, cover `SettingLevelTests.Run_CollectionTypeAtEachLevel_IsTheMethodsReturnType`) and read: the merge of the levels is in src/SqlSource/Settings/, outside the generator-root scope, so the cover says nothing of it.  `QuerySettings.UseArrayPool` is `Parameters.IsOn(GeneratorParameters.ArrayPool)` (src/SqlSource/Settings/QuerySettings.cs:77), over the same per-option merge as every generator parameter.  The three rows prove only that a level naming another option, `sort-input`, leaves array-pool as the level above gave it, which `GeneratorParameterLevelTests` proves with the same `Other = "sort-input"` in its rows `(Default, Other, On, null)`, `(On, Other, null, null)` and `(On, On, On, Other)` (GeneratorParameterLevelTests.cs:40-43).  The rows that stay keep what is array-pool's own: on by default, off from each level, `default` giving it back on at each level, and an explicit on or off at the query.  The only document naming the theory is docs/tech-debt/TD-0089, for its `if` on `folderFile`, not for its rows; the class comment names no row.  The remaining rows reach the same code, so its timeouts stay under the test.; blind: no
- cover: `tests/SqlSource.Tests/Generator/GeneratorParameterLevelTests.cs` | `GeneratorParameterLevelTests.Run_GeneratorParametersWhoseNearestStateIsOn_KeepTheComment`
- proposal: Drop the three rows whose only point is that `sort-input` at a level leaves array-pool alone; keep the other ten.
- verdict: confirmed
- verifier's reason: The three rows, tests/SqlSource.Tests/Generator/ArrayPoolParameterTests.cs:29, :32 and :34, each have a level that names only `sort-input` and assert array-pool keeps what the level above gave.  The merge is per parameter by slot mask (src/SqlSource/Settings/GeneratorParameterValues.cs:41-45 `Over`, `SlotsOf` at :61-73) with nothing particular to array-pool, whose definition is an ordinary on-by-default switch (src/SqlSource/Settings/GeneratorParameterList.cs:72), and `UseArrayPool` is `Parameters.IsOn(GeneratorParameters.ArrayPool)` (src/SqlSource/Settings/QuerySettings.cs:77).  The cover, `GeneratorParameterLevelTests.Run_GeneratorParametersWhoseNearestStateIsOn_KeepTheComment`, has the same shapes with `Other = "sort-input"`: `(On, Other, null, null)`, `(Default, Other, On, null)` and `(On, On, On, Other)` (tests/SqlSource.Tests/Generator/GeneratorParameterLevelTests.cs:40-43), built by `ListFromAt` (:227-254) the same way as the theory builds its levels (folder file through `LevelSettings`, header and query as `-- generator:` markers, project file as `generatorParameters`), and its `On` includes `token-validation=off`, a parameter on by default as array-pool is.  The ten rows that stay still cover array-pool from each level, `default` at each level, and an explicit on or off at the query.  `kills` in generator-root (the only scope that ran the test; generator-settings holds no such test) shows 57 kills of the whole theory, all in the root files, none in Settings; the rows share one name, so no filter can measure the fold: it is confirmed by reading, and the phase's own guard proves it.  The test stays, so its 12 timeouts stay under it.  The only document naming the theory is docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md:19, for its branch on `folderFile`, not for a row; the class comment (ArrayPoolParameterTests.cs:10-13) names no row.
- condition: `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md:19` names this theory for its `if` on `folderFile`, not for a row; rows with a folder's file stay, so that line stays true and this phase leaves TD-0089 alone (phase 99 changes it).

### F-SqlSource.Tests.Generator-2-04

- test: `tests/SqlSource.Tests/Generator/DialectTests.cs` | `DialectTests.Run_OptionOfADialect_IsReadFromTheMarkerTheFoldersFileAndTheProjectsFile` | rows: `(null, "mariadb , ansi_quotes , no_backslash_escapes", null)` at line 95, `("mysql, no-backslash-escapes", null, null)` at line 97, `("MySQL ,  NO_BACKSLASH_ESCAPES", null, null)` at line 98
- action: fold
- evidence: read (the three rows prove the spelling of a value, not a level: line 95 a folder's file with spaces, underscores and `mariadb`, lines 97-98 a marker with a space, case and underscores; the levels and the winner are proved by the rows at lines 91-93 and 100-101.  `SqlDialectMarkerTests.Apply_MarkerWithAnOption_SwitchesTheLexerToTheRulesOfThatOption` has the marker rows `mysql, no-backslash-escapes` and `MySQL ,\tNO_BACKSLASH_ESCAPES` with this query's `'C:\temp\'`, and the folder's value goes through `SqlDialectName.TryParse` (src/SqlSource/SettingsFiles/SettingsFileReader.cs:183), whose `TryParse_NameWithOptions_GivesTheDialectAndItsOptions` has the spaces around commas, the server's underscores and `MariaDB` with an option.  Measured: 8 rows, kills 37, alone 0, timeouts under it 10, rows needed 1; the dialect's reading is outside the generator-root scope.  No governing document names `DialectTests`; the comments above the rows, lines 94 and 96, say what each spells and record nothing more); blind: no
- cover: `tests/SqlSource.Tests/Parsing/SqlDialectMarkerTests.cs` | `SqlDialectMarkerTests.Apply_MarkerWithAnOption_SwitchesTheLexerToTheRulesOfThatOption`; `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs` | `SqlDialectNameTests.TryParse_NameWithOptions_GivesTheDialectAndItsOptions`
- proposal: Drop the rows at lines 95, 97 and 98, keeping one row for each level and the two of the winner.
- verdict: confirmed
- verifier's reason: The theory (tests/SqlSource.Tests/Generator/DialectTests.cs:89-115) is about the level an option is read from.  Rows 91-93 and 100-101 prove the levels and the winner.  Rows 95, 97 and 98 prove only how a value is spelled.
  - Row 97 (`mysql, no-backslash-escapes`) is a row of `SqlDialectMarkerTests.Apply_MarkerWithAnOption_SwitchesTheLexerToTheRulesOfThatOption` word for word (tests/SqlSource.Tests/Parsing/SqlDialectMarkerTests.cs:30).
  - Row 98 (`MySQL ,  NO_BACKSLASH_ESCAPES`) matches that test's row at line 31 (`MySQL ,\tNO_BACKSLASH_ESCAPES`) in case, the space before the comma, whitespace after it and underscores.  That unit test checks the lexer's rules on the same `'C:\temp\'`.
  - The generator reads the marker through that same `SqlDialectMarker.Apply` (src/SqlSource/Parsing/SqlFileParser.cs:36).
  - The folder file's value goes through `SettingsFileReader.ReadDialect` to `SqlDialectName.TryParse` (src/SqlSource/SettingsFiles/SettingsFileReader.cs:183).  `TryParse_NameWithOptions_GivesTheDialectAndItsOptions` (tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs:41-55) has spaces around the commas (line 53), the server's underscores (line 48) and MariaDB with its options (lines 44, 46, 49).  So row 95 adds nothing.
  - Row 100 keeps `mariadb,no-backslash-escapes` running through the generator.
  The docs search finds no mention of `DialectTests`.  The comments above the rows (lines 94 and 96) say what each one spells and record nothing more.  `git log -S` traces the rows to feature commits 37b472e1 and 7b501f57, not to a fix.  The subject is outside generator-root, so I confirmed this by reading.  The rows share one name, so no filter can measure the fold, and the cleanup phase's own guard proves it.

### F-SqlSource.Tests.Generator-2-05

- test: `tests/SqlSource.Tests/Generator/DialectTests.cs` | `DialectTests.Run_File_IsReadByItsMarkerThenItsFoldersFileThenTheProjectsFile` | rows: `(null, null, "MySQL", MySql)` at line 39, `(null, null, "tsql", SqlServer)` at line 40
- action: fold
- evidence: read (the test is of the order of the levels, its comment at DialectTests.cs:11-13; these two rows prove only that a name is read in any case and that `tsql` is SQL Server, which the row at line 38 and the folder rows already give at the same level.  `SqlDialectNameTests.TryParse_NameOrAlias_GivesItsDialect` has `tsql`, `MSSQL` and `MySql`, and `FileParseInputTests.Resolve_Dialect_IsTheChainsOrElseAnsi` resolves a chain's `MySQL`.  Measured: 11 rows, kills 50, alone 0, timeouts under it 10, rows needed 1; the subject is outside the generator-root scope.  No governing document names `DialectTests`; the comment above the rows, "The project's file alone, in any case", records nothing more); blind: no
- cover: `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs` | `SqlDialectNameTests.TryParse_NameOrAlias_GivesItsDialect`; `tests/SqlSource.Tests/Generation/FileParseInputTests.cs` | `FileParseInputTests.Resolve_Dialect_IsTheChainsOrElseAnsi`
- proposal: Drop the rows at lines 39 and 40.
- verdict: confirmed
- verifier's reason: The theory (tests/SqlSource.Tests/Generator/DialectTests.cs:34-65) is about the order of the levels (comment at lines 11-13).  Row 39 (`"MySQL"` in the project's file) proves only that a name is read in any case.  Row 40 (`"tsql"`) proves only an alias.  Row 38 (`mysql`) already proves the project's level alone.  The project's file is read by `SettingsFileReader.ReadDialect`, which calls `SqlDialectName.TryParse` (src/SqlSource/SettingsFiles/SettingsFileReader.cs:183).  `SqlDialectNameTests.TryParse_NameOrAlias_GivesItsDialect` has `tsql` (tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs:16), `MSSQL` and `MySql` (lines 27, 29).  `FileParseInputTests.Resolve_Dialect_IsTheChainsOrElseAnsi` resolves a chain's `"MySQL"` (tests/SqlSource.Tests/Generation/FileParseInputTests.cs:19-22).  The order of levels does not depend on which dialect wins, so losing the one row where the project's file gives SQL Server loses nothing.  The docs search finds no mention of `DialectTests`.  The comment "The project's file alone, in any case" (line 37) records nothing more, and the `tsql` row came with feature commit 1a894627.  The subject is outside generator-root, so I confirmed this by reading.  The rows share one name, so no filter can measure the fold, and the cleanup phase's own guard proves it.
- a cover: `DialectTests.Run_File_IsReadByItsMarkerThenItsFoldersFileThenTheProjectsFile` is the cover of F-SqlSource.Tests.Generator-6-09 (phase 01), F-SqlSource.Tests.Generator-6-10 (phase 01), F-SqlSource.Tests.Generator-6-11 (phase 01).  The change keeps every assertion and row that makes it so.

### F-SqlSource.Tests.Generator-2-06

- test: `tests/SqlSource.Tests/Generator/DialectTests.cs` | `DialectTests.Run_BytesLiteralWithAnEscapedQuoteUnderCockroachDb_IsRead` | rows: `("Cockroach")` at line 152
- action: fold
- evidence: read (the test is of CockroachDB's bytes literal; the row `Cockroach` proves only the alias and its case, which `SqlDialectNameTests.TryParse_NameOrAlias_GivesItsDialect` proves with its rows `cockroach` and `CockroachDB`.  Measured: 2 rows, kills 32, alone 0, timeouts under it 10, rows needed 1; the lexer is outside the generator-root scope.  No governing document names `DialectTests`; the comment, "under either of its names", records nothing more); blind: no
- cover: `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs` | `SqlDialectNameTests.TryParse_NameOrAlias_GivesItsDialect`
- proposal: Drop the row at line 152, leaving a `[Fact]` under `cockroachdb`.
- verdict: confirmed
- verifier's reason: `Run_BytesLiteralWithAnEscapedQuoteUnderCockroachDb_IsRead` (tests/SqlSource.Tests/Generator/DialectTests.cs:149-163) is about CockroachDB's bytes literal.  Its comment says the theory runs "under either of its names".  The row `Cockroach` (line 152) differs from `cockroachdb` (line 151) only in the name the project's settings file gives, and `SqlDialectName.TryParse` reads that name (SettingsFileReader.cs:183).  `TryParse_NameOrAlias_GivesItsDialect` has `cockroach` and `CockroachDB` (tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs:23-24), which cover both the alias and the case.  The docs search finds CockroachDB in docs/tech-debt/TD-0004 and docs/deferred/D-0001, about the lexer's limits and the choice of dialects, and neither names this test or its alias row.  Commit 8ee9a2ce made this theory uniform under TD-0089, and TD-0089 does not ask for both names.  The subject is outside generator-root, so I confirmed this by reading.  The rows share one name, so no filter can measure the fold, and the cleanup phase's own guard proves it.

### F-SqlSource.Tests.Generator-5-03

- test: `tests/SqlSource.Tests/Generator/ModelNameTests.cs` | `ModelNameTests.Run_ReservedName_IsSqlsrc256` | rows: all but `"EqualityContract"` (line 168): `"Equals"` (169), `"GetHashCode"` (170), `"ToString"` (171), `"GetType"` (172), `"MemberwiseClone"` (173), `"Finalize"` (174), `"ReferenceEquals"` (175), `"PrintMembers"` (176), `"Deconstruct"` (177), `"Clone"` (178)
- action: fold
- evidence: read (the generator's one check is `PropertyNames.IsReserved(name)` at `src/SqlSource/Generation/Models/ModelReader.cs:388`, and `PropertyNamesTests.IsReserved_EachName_IsTrue`, `tests/SqlSource.Tests/Generation/Models/PropertyNamesTests.cs:46-58`, has the same eleven names as rows; each row here differs only in the name it passes to that predicate and the name the message repeats.  No document names the test or `ModelNameTests`, and no comment marks it as a record.  Measured: 11 rows, kills 46, alone 0, timeouts under it 12, rows needed 1, but `ModelReader` and `PropertyNames` are outside the root scope, so the rows are judged by reading); blind: no
- cover: `tests/SqlSource.Tests/Generation/Models/PropertyNamesTests.cs` | `PropertyNamesTests.IsReserved_EachName_IsTrue`
- proposal: Keep one row, which proves that a reserved name is SQLSRC256 with its message and drops the query's models, and leave each name to the unit theory; `Run_ReservedParameterName_IsSqlsrc256WithRename` keeps the parameter's case.
- verdict: confirmed
- verifier's reason: The theory's rows (ModelNameTests.cs:167-191) differ only in the column name. Every name is already PascalCase, so `PropertyNames.PascalCase` returns it unchanged. Each row then reaches the same single check, `PropertyNames.IsReserved(name)` at `src/SqlSource/Generation/Models/ModelReader.cs:388`, which raises the same SQLSRC256 with the name put into the message. `PropertyNamesTests.IsReserved_EachName_IsTrue` (`tests/SqlSource.Tests/Generation/Models/PropertyNamesTests.cs:46-58`) has the same eleven names, and `IsReserved_Name_IsFalse` has the negatives. The rows have no comment above them. A grep of the documents finds neither the test nor its class. In generator-root the theory has 46 kills, 0 alone, 12 timeouts and rows needed 1. None of its kills is in `PropertyNames` (it is outside the scope); they are pipeline mutants and `SqlIdentifier.cs:18,30,40,52`, which every generator run kills. The 12 timeouts are the pipeline's own and stay reached by the kept row. `Run_ReservedParameterName_IsSqlsrc256WithRename` (:193-205) keeps the parameter's wording.

### F-SqlSource.Tests.Generator-5-04

- test: `tests/SqlSource.Tests/Generator/ModelNameTests.cs` | `ModelNameTests.Run_ModelNameWithAContextualKeywordThatCSharpRefusesAsATypeName_IsAnError` | rows: all but one keyword for each place, for instance keeping `("input-model", "file")` (499), `("output-model", "scoped")` (505) and `("modelNamespace", "extension")` (510), and dropping `("input-model", "required")` (500), `("input-model", "scoped")` (501), `("input-model", "extension")` (502), `("output-model", "file")` (503), `("output-model", "required")` (504), `("output-model", "extension")` (506), `("modelNamespace", "file")` (507), `("modelNamespace", "required")` (508) and `("modelNamespace", "scoped")` (509)
- action: fold
- evidence: measured and read (every place reads its value through `SettingValue.IsQualifiedName`, `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs:186,211,219` and `src/SqlSource/Settings/SettingsLevelReader.cs:36`, which refuses each keyword through `SqlIdentifier.IsUsableTypeName`; `SettingValueTests.IsQualifiedName_IdentifiersJoinedByPeriods_IsAName`, `tests/SqlSource.Tests/Settings/SettingValueTests.cs:180-187`, has a keyword alone, first, in the middle and last, and `SqlIdentifierTests.IsUsableTypeName_NameThatCSharpTakesForAType_IsUsable`, `tests/SqlSource.Tests/SqlIdentifierTests.cs:40-52`, has each of the four.  What a row adds is the place's diagnostic and its fallback, which one keyword per place proves; the comment's "a part alone, in the middle and at the end" is the place's, and stays.  No document names the test.  Measured: 12 rows, kills 61, alone 0, timeouts under it 12, rows needed 4, the four being the four keywords of `SqlIdentifier.IsUsableTypeName`, which the unit test kills too, since none is alone); blind: no
- cover: `tests/SqlSource.Tests/Settings/SettingValueTests.cs` | `SettingValueTests.IsQualifiedName_IdentifiersJoinedByPeriods_IsAName`; `tests/SqlSource.Tests/SqlIdentifierTests.cs` | `SqlIdentifierTests.IsUsableTypeName_NameThatCSharpTakesForAType_IsUsable`
- proposal: Keep one row for each place, three in all, with a different keyword in each, and leave the list of keywords to the unit theories.
- verdict: confirmed
- verifier's reason: Every place in ModelNameTests.cs:498-550 reads its value through `SettingValue.IsQualifiedName`: the markers at `SqlMarkerScope.cs:186,211,219` and the settings file at `SettingsLevelReader.cs:36`. That check refuses the four keywords through `SqlIdentifier.IsUsableTypeName` (`src/SqlSource/SqlIdentifier.cs:48-49`). Where the keyword sits in the value (alone, middle, last) depends on the place, not on the keyword (:514-519), so one row per place keeps each position. `SettingValueTests.IsQualifiedName_IdentifiersJoinedByPeriods_IsAName` (SettingValueTests.cs:180-185) and `SqlIdentifierTests.IsUsableTypeName_NameThatCSharpTakesForAType_IsUsable` (SqlIdentifierTests.cs:40-52) between them have every keyword in every position. `SqlIdentifier.cs` is a root file, so generator-root measured it. Every mutant of line 49 the theory kills, each keyword's string mutant (`:49:46`, `:49:56`, `:49:70`, `:49:82`) and each logical mutant, is also killed by `SqlIdentifierTests.IsUsableTypeName_NameThatCSharpTakesForAType_IsUsable`. That is what "rows needed 4" means, and the unit test keeps it. The comment (:495-497) explains the C# rule rather than marking the rows as a record, and the covers carry the same rule in their own comments (SettingValueTests.cs:180). No document names the test. The timeouts are the pipeline's 12, which the kept rows still reach.

### F-SqlSource.Tests.Generator-5-05

- test: `tests/SqlSource.Tests/Generator/NestedClassTests.cs` | `NestedClassTests.Run_ContextualKeywordThatCannotNameTheNestedClass_IsAnError` | rows: all but one for each place, for instance keeping `("file", false)` (241) and `("var", true)` (250), and dropping `("required", false)` (242), `("scoped", false)` (243), `("extension", false)` (244), `("var", false)` (245), `("file", true)` (246), `("required", true)` (247), `("scoped", true)` (248) and `("extension", true)` (249)
- action: fold
- evidence: measured and read (the marker and the settings file both read the value through `SettingValue.IsNestedClassName`, `src/SqlSource/Parsing/Markers/SqlMarkerScope.cs:207` and `src/SqlSource/Settings/SettingsLevelReader.cs:43`, and `SettingValueTests.IsNestedClassName_OneIdentifierThatCanNameANestedClass_IsAName`, `tests/SqlSource.Tests/Settings/SettingValueTests.cs:150-161`, has each of `file`, `required`, `scoped`, `extension` and `var` as a row.  A row here adds only the place's diagnostic, SQLSRC111 and the file left out or SQLSRC019 and the default class, which one row of each place proves.  No document names the test.  Measured: 10 rows, kills 63, alone 0, timeouts under it 10, rows needed 4); blind: no
- cover: `tests/SqlSource.Tests/Settings/SettingValueTests.cs` | `SettingValueTests.IsNestedClassName_OneIdentifierThatCanNameANestedClass_IsAName`
- proposal: Keep one row for each place, one of a contextual keyword and one of `var` so that the comment's two reasons each keep a case, and leave the list to the unit theory.
- verdict: confirmed
- verifier's reason: The marker (`SqlMarkerScope.cs:203-207`) and the settings file (`SettingsLevelReader.cs:43`) both read the value through `SettingValue.IsNestedClassName` (`src/SqlSource/Settings/SettingValue.cs:86-87`). That check refuses `var` and then defers to `SqlIdentifier.IsUsableTypeName`. `SettingValueTests.IsNestedClassName_OneIdentifierThatCanNameANestedClass_IsAName` (SettingValueTests.cs:150-161) has rows for `file`, `required`, `scoped`, `extension` and `var`, and its comments carry both reasons from the theory's comment (NestedClassTests.cs:236-239). Each row of the theory (NestedClassTests.cs:240-282) adds only its place's outcome: SQLSRC111 with the file left out, or SQLSRC019 with the constant kept. Keeping one keyword row and one `var` row, one per place, keeps both outcomes and both reasons. In generator-root, every keyword mutant of `SqlIdentifier.cs:49` the theory kills is also killed by `SqlIdentifierTests.IsUsableTypeName_NameThatCSharpTakesForAType_IsUsable`. It has 10 timeouts, all pipeline mutants that any kept row still reaches. No document names the test.

### F-SqlSource.Tests.Generator-5-07

- test: `tests/SqlSource.Tests/Generator/PlacementDiagnosticsTests.cs` | `PlacementDiagnosticsTests.Run_NestedClassMadePublicByAnotherFileOfATypeReachableAsProtected_ReportsSqlsrc123AtTheMethod`; `PlacementDiagnosticsTests.Run_NestedClassMadePublicByAMarkerOfATypeReachableAsProtected_ReportsSqlsrc123AtTheMarker` | rows: all
- action: fold
- evidence: read (the two tests, :427-460 and :464-493, have the same source, `NestedSampleSource("public", "", "protected")`, the same `A.sql` whose header makes `Sql` public, and the same entry; they differ only in where `GetUser`'s `-- member-location: nested-class` is written, the header or the query, and so in the span, `(3,10)-(3,17)` or `(2,1)-(2,33)`.  The second asserts the diagnostic alone, and not that the methods file is left out, the model kept and `GetName` kept, which the first does.  The comments above them say the error is at the method or at its marker and that it names the query that wrote the visibility, which both assert; the class comment requires every refusal to leave out the method alone.  No document names either test.  Measured: kills 59 and 53, alone 0 each, timeouts under each 12, from one input, so the theory reaches the same; each is the other's cover, so neither is deleted, and the subject is outside the root scope); blind: no
- cover: `tests/SqlSource.Tests/Generator/PlacementDiagnosticsTests.cs` | `PlacementDiagnosticsTests.Run_NestedClassMadePublicByAnotherFileOfATypeReachableAsProtected_ReportsSqlsrc123AtTheMethod`
- proposal: Make them one theory whose rows are the place of `-- member-location:` and the span, each asserting the first test's whole check, so the marker's case also proves that only the method is left out.
- verdict: confirmed
- verifier's reason: The two facts (PlacementDiagnosticsTests.cs:427-460 and :464-493) use the same `NestedSampleSource("public", "", "protected")`, the same `A.sql` and the same `UserEntry`. They differ only in whether `GetUser`'s `-- member-location: nested-class` is in the header (:432) or on the query (:469), and so in the span (`(3,10)-(3,17)` or `(2,1)-(2,33)`). The second checks only `Problems()`. The first also checks that the methods file is left out, `GetUserDtoFile` kept and the `Sql` class with `GetName` kept (:456-459), which the class comment (:16-18) requires of every refusal. In generator-root, the first kills six mutants the second does not (`SqlSourceGenerator.cs:64:9`, `:65:9`, `:823:9`, `:837:9`, `:843:21`, `:845:21`), all from those extra reads. The second kills nothing the first does not, and both have the same 12 timeouts. A theory that gives every row the first test's checks loses nothing and adds those checks to the marker case. Neither comment marks a test as a record, and no document names either test.

## The guard

### Mutation

Run each scope of the phase, from `.claude/skills/reviewing-test-quality/scopes.txt`, before the change and after it, from the repository root.  A phase of the same scope done just before this one may give its "after" report as this one's "before".

Scope `generator-root`:

```bash
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-19/generator-root/before --tests 'FullyQualifiedName~SqlSource.Tests.Generator.|FullyQualifiedName~SqlSource.Tests.DatabaseNameTests.|FullyQualifiedName~SqlSource.Tests.EquatableArrayTests.|FullyQualifiedName~SqlSource.Tests.PackageVersionTests.|FullyQualifiedName~SqlSource.Tests.SortedSearchTests.|FullyQualifiedName~SqlSource.Tests.SqlIdentifierTests.|FullyQualifiedName~SqlSource.Tests.SqlServerNamesTests.|FullyQualifiedName~SqlSource.Tests.SystemSchemasTests.|FullyQualifiedName~SqlSource.Tests.TextLinesTests.|FullyQualifiedName~SqlSource.Tests.UnexpectedEnumValueExceptionTests.|FullyQualifiedName~SqlSource.Tests.VersionsTests.' '*.cs'
# the change
tools/mutation-test.sh generator --output artifacts/test-cleanup/phase-19/generator-root/after --tests 'FullyQualifiedName~SqlSource.Tests.Generator.|FullyQualifiedName~SqlSource.Tests.DatabaseNameTests.|FullyQualifiedName~SqlSource.Tests.EquatableArrayTests.|FullyQualifiedName~SqlSource.Tests.PackageVersionTests.|FullyQualifiedName~SqlSource.Tests.SortedSearchTests.|FullyQualifiedName~SqlSource.Tests.SqlIdentifierTests.|FullyQualifiedName~SqlSource.Tests.SqlServerNamesTests.|FullyQualifiedName~SqlSource.Tests.SystemSchemasTests.|FullyQualifiedName~SqlSource.Tests.TextLinesTests.|FullyQualifiedName~SqlSource.Tests.UnexpectedEnumValueExceptionTests.|FullyQualifiedName~SqlSource.Tests.VersionsTests.' '*.cs'
tools/mutation-test.sh compare artifacts/test-cleanup/phase-19/generator-root/before/reports/mutation-report.json artifacts/test-cleanup/phase-19/generator-root/after/reports/mutation-report.json
```

`compare` must list nothing `LOST` and nothing `NOT COMPARABLE`.  The one exception is a mutant that no test this phase removes or changes killed, or had as a timeout under it, in the "before" report, which is a kill that did not repeat: the pull request names it by file, place and mutator.  `kills` shows it for each test the phase removes or changes:

```bash
dotnet build tools/MutationReport --nologo --verbosity quiet
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-19/generator-root/before/reports/mutation-report.json ArrayPoolParameterTests.Run_ArrayPool_ComesFromTheMostSpecificLevelThatNamesIt
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-19/generator-root/before/reports/mutation-report.json DialectTests.Run_OptionOfADialect_IsReadFromTheMarkerTheFoldersFileAndTheProjectsFile
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-19/generator-root/before/reports/mutation-report.json DialectTests.Run_File_IsReadByItsMarkerThenItsFoldersFileThenTheProjectsFile
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-19/generator-root/before/reports/mutation-report.json DialectTests.Run_BytesLiteralWithAnEscapedQuoteUnderCockroachDb_IsRead
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-19/generator-root/before/reports/mutation-report.json ModelNameTests.Run_ReservedName_IsSqlsrc256
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-19/generator-root/before/reports/mutation-report.json ModelNameTests.Run_ModelNameWithAContextualKeywordThatCSharpRefusesAsATypeName_IsAnError
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-19/generator-root/before/reports/mutation-report.json NestedClassTests.Run_ContextualKeywordThatCannotNameTheNestedClass_IsAnError
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-19/generator-root/before/reports/mutation-report.json PlacementDiagnosticsTests.Run_NestedClassMadePublicByAnotherFileOfATypeReachableAsProtected_ReportsSqlsrc123AtTheMethod
dotnet tools/MutationReport/bin/Debug/net10.0/SqlSource.MutationReport.dll kills artifacts/test-cleanup/phase-19/generator-root/before/reports/mutation-report.json PlacementDiagnosticsTests.Run_NestedClassMadePublicByAMarkerOfATypeReachableAsProtected_ReportsSqlsrc123AtTheMarker
```

### Coverage

The line coverage of the source the phase's tests reach must not fall: every file under the `mutate` globs of `generator-root`.  Run the test project with coverage before the change and after it, as `CONTRIBUTING.md` does for the solution, and compare those files' figures in the two reports:

```bash
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-19/coverage-before/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-19/coverage-before/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-19/coverage-before/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
dotnet test --project tests/SqlSource.Tests --results-directory artifacts/test-cleanup/phase-19/coverage-after/SqlSource.Tests --coverage --coverage-output-format cobertura
dotnet reportgenerator '-reports:artifacts/test-cleanup/phase-19/coverage-after/SqlSource.Tests/*.cobertura.xml' -targetdir:artifacts/test-cleanup/phase-19/coverage-after/SqlSource.Tests/report -reporttypes:Html '-assemblyfilters:+SqlSource'
```

`tests/SqlSource.Tests.RoslynFloor` compiles `tests/SqlSource.Tests/Generator/` a second time: run it too, before the change and after, and it passes after.

### Covers and run time

Mutation cannot guard these findings, whose test is blind, reaches no tested mutant, or has its subject outside the scope: F-SqlSource.Tests.Generator-1-02, F-SqlSource.Tests.Generator-2-04, F-SqlSource.Tests.Generator-2-05, F-SqlSource.Tests.Generator-2-06, F-SqlSource.Tests.Generator-5-03, F-SqlSource.Tests.Generator-5-07.  For them, and for the database tests of the phase if it has any, the pull request lists each removed test or row beside its named cover, and the test project's run time before and after the change, on one machine.

| Finding | Action | Test removed, or rows dropped | Named cover |
|----|----|----|----|
| F-SqlSource.Tests.Generator-1-02 | fold | `ArrayPoolParameterTests.Run_ArrayPool_ComesFromTheMostSpecificLevelThatNamesIt` (rows: `(Unpooled, Other, null, null, false)` at line 29, `(Default, Other, Unpooled, null, false)` at line 32, `(null, null, Unpooled, Other, false)` at line 34) | `tests/SqlSource.Tests/Generator/GeneratorParameterLevelTests.cs`: `GeneratorParameterLevelTests.Run_GeneratorParametersWhoseNearestStateIsOn_KeepTheComment` |
| F-SqlSource.Tests.Generator-2-04 | fold | `DialectTests.Run_OptionOfADialect_IsReadFromTheMarkerTheFoldersFileAndTheProjectsFile` (rows: `(null, "mariadb , ansi_quotes , no_backslash_escapes", null)` at line 95, `("mysql, no-backslash-escapes", null, null)` at line 97, `("MySQL ,  NO_BACKSLASH_ESCAPES", null, null)` at line 98) | `tests/SqlSource.Tests/Parsing/SqlDialectMarkerTests.cs`: `SqlDialectMarkerTests.Apply_MarkerWithAnOption_SwitchesTheLexerToTheRulesOfThatOption`; `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs`: `SqlDialectNameTests.TryParse_NameWithOptions_GivesTheDialectAndItsOptions` |
| F-SqlSource.Tests.Generator-2-05 | fold | `DialectTests.Run_File_IsReadByItsMarkerThenItsFoldersFileThenTheProjectsFile` (rows: `(null, null, "MySQL", MySql)` at line 39, `(null, null, "tsql", SqlServer)` at line 40) | `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs`: `SqlDialectNameTests.TryParse_NameOrAlias_GivesItsDialect`; `tests/SqlSource.Tests/Generation/FileParseInputTests.cs`: `FileParseInputTests.Resolve_Dialect_IsTheChainsOrElseAnsi` |
| F-SqlSource.Tests.Generator-2-06 | fold | `DialectTests.Run_BytesLiteralWithAnEscapedQuoteUnderCockroachDb_IsRead` (rows: `("Cockroach")` at line 152) | `tests/SqlSource.Tests/Parsing/SqlDialectNameTests.cs`: `SqlDialectNameTests.TryParse_NameOrAlias_GivesItsDialect` |
| F-SqlSource.Tests.Generator-5-03 | fold | `ModelNameTests.Run_ReservedName_IsSqlsrc256` (rows: all but `"EqualityContract"` (line 168): `"Equals"` (169), `"GetHashCode"` (170), `"ToString"` (171), `"GetType"` (172), `"MemberwiseClone"` (173), `"Finalize"` (174), `"ReferenceEquals"` (175), `"PrintMembers"` (176), `"Deconstruct"` (177), `"Clone"` (178)) | `tests/SqlSource.Tests/Generation/Models/PropertyNamesTests.cs`: `PropertyNamesTests.IsReserved_EachName_IsTrue` |
| F-SqlSource.Tests.Generator-5-07 | fold | `PlacementDiagnosticsTests.Run_NestedClassMadePublicByAnotherFileOfATypeReachableAsProtected_ReportsSqlsrc123AtTheMethod`, `PlacementDiagnosticsTests.Run_NestedClassMadePublicByAMarkerOfATypeReachableAsProtected_ReportsSqlsrc123AtTheMarker` (rows: all) | `tests/SqlSource.Tests/Generator/PlacementDiagnosticsTests.cs`: `PlacementDiagnosticsTests.Run_NestedClassMadePublicByAnotherFileOfATypeReachableAsProtected_ReportsSqlsrc123AtTheMethod` |

Run time, on one machine, before and after: `dotnet test --project tests/SqlSource.Tests`.

## Documents

A search of the `AGENTS.md` files, the readmes, `CONTRIBUTING.md`, `docs/architecture/`, `docs/tech-debt/` and `docs/deferred/` for the name of each test this phase removes, renames or takes rows from, and for its class's name, found the following.  The plans and specs of `docs/superpowers/` are records of their date and stay as they are.

Lines that name a test of the phase; the phase updates each line its change makes untrue, as the conditions below say:

- `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md:19` (F-SqlSource.Tests.Generator-1-02)

Edits the findings and their verdicts ask for:

- F-SqlSource.Tests.Generator-1-02: `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md:19` names this theory for its `if` on `folderFile`, not for a row; rows with a folder's file stay, so that line stays true and this phase leaves TD-0089 alone (phase 99 changes it).

Lines that name a class of the phase, which stays, so they stay true unless they describe what the phase removes; read each:

- `docs/tech-debt/TD-0089-theories-that-branch-on-their-row.md`: 19

## Left out

- Every other finding of these folders is in the phase that holds its file; no test outside the findings is changed, and no new test is written but the unit tests a **demote** names.
