# TD-0109 - Duplicated arrangements and uneven conventions in tests

## Problem

The maintainability review of 2026-11-06, after the bug-report bundles of phase 10, found these in `tests/`.  Items 1 to 41 come from the first sweep of `tests/`, items 42 to 54 from a sweep of the folders it left out; each location was confirmed by a checker that did not find it.  The tests that depend on a timer or on the machine are TD-0111 to TD-0113.  Paths are under `tests/`.

### Duplicated helpers and arrangements

1. **`CorpusWordsTests` copies two helpers of `BundleLeaks`**, which its project references: `DatabaseTests/SqlServer/Renaming/CorpusWordsTests.cs:164` (`PlainWordsOf`) is `SqlSource.Tool.Testing/Bundles/BundleLeaks.cs:441` (`EngineWordsOf`) but for keeping `@@` words, and `CorpusWordsTests.cs:208` (`NameOf`) is `BundleLeaks.cs:486` (`QuotedOf`) without its literal arm.  The two walks around them differ on purpose.
2. **The rename of a real engine's bundle is arranged by hand in the container projects**, which cannot reach `SqlSource.Tool.Tests`: a key of one repeated byte is written at `SqlSource.Tool.Tests/RenamingBundles.cs:107` and again at `DatabaseTests/Postgres/Renaming/RealisticRenamingTests.cs:143`, `DatabaseTests/Postgres/Renaming/PartitionRenamingTests.cs:70` and `DatabaseTests/SqlServer/Renaming/RealisticRenamingTests.cs:48`; `const byte Key = 7` at `SqlSource.Tool.Tests/PostgresRenamings.cs:21` and the two PostgreSQL classes (`:40`, `:31`); and `BundleRenamer.RenameAsync` is called at seven sites (`RenamingBundles.cs:115`, `PostgresRenamings.cs:42`, `SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerFixtureRenamingTests.cs:110`, `SqlServerErrorRenamingTests.cs:609`, and the three container classes at `:139`, `:66`, `:44`).
3. **SQL Server's renaming tests have no counterpart of `PostgresRenamings`**, so three classes of `SqlSource.Tool.Tests` (`BundleLeaksTests`, `Bundles/Renaming/SqlServer/SqlServerFixtureRenamingTests`, `SqlServerErrorRenamingTests`) each declare `Engine = "mssql"` (`:19`, `:21`, `:31`) and the engine's `Part` (`:27`, `:23`, `:47`), look a fixture up by hand four times (`BundleLeaksTests.cs:33`, `SqlServerFixtureRenamingTests.cs:105`, `SqlServerErrorRenamingTests.cs:491`, `:547`), read `schema.sql` three times (`SqlServerFixtureRenamingTests.cs:59`, `SqlServerErrorRenamingTests.cs:174`, `:493`) and write their own `RenamedAsync`, `RefusedAsync` and `RenameAsync` (`SqlServerFixtureRenamingTests.cs:108`; `SqlServerErrorRenamingTests.cs:594`, `:601`, `:608`).
4. **`SqlSource.Tool.Testing/Bundles/BundleLeaks.cs`, 683 lines, holds two searches that share no helper**: PostgreSQL's `Of` (`:52`, seven helpers) and SQL Server's `Find` (`:307`) and `EngineRowNames` (`:357`, eleven helpers).  Every SQL Server call passes `BundleLeaks.EngineRowNames(raw, Part)` by hand, 10 times (`SqlSource.Tool.Tests/BundleLeaksTests.cs:160`, `:173`, `:183`, `:194`, `:205`, `:215`, `:225`; `DatabaseTests/SqlServer/Describer/DiagnoseCommandTests.cs:83`; `DatabaseTests/SqlServer/Renaming/RealisticRenamingTests.cs:66`; `SqlServerFixtureRenamingTests.cs:66`), and `BundleLeaksTests.cs:266` tests `Provenance`.
5. **`RenamingBundles.RawAsync` maps a failure to an outcome as `RawBundles.OutcomeOf` does**: `SqlSource.Tool.Tests/RenamingBundles.cs:84-87` and `SqlSource.Tool.Testing/Bundles/RawBundles.cs:93-94`.
6. **`DiagnoseCommandTests` is copied per engine**: `DatabaseTests/Postgres/Describer/DiagnoseCommandTests.cs` and `DatabaseTests/SqlServer/Describer/DiagnoseCommandTests.cs` hold the same two tests (`:80`/`:87`, `:107`/`:114`) and helpers `DiagnoseAsync` (`:52`/`:56`) and `ReadBundle` (`:63`/`:67`), which is also `SqlSource.Tool.Tests/BundleReplays.cs:26`; the "reproduced" line is written in four classes (the two, `SqlSource.Tool.Tests/DiagnoseScene.cs:34` and `Commands/ReplayTests.cs:22`).
7. **Three realistic classes are copied per engine outside the bodies of `SqlSource.Tool.Testing/Realistic/`**, and one copy has drifted: `Realistic/Generation/RealisticDapperTests.cs`, `Realistic/Generation/RealisticMethodTests.cs` and `Realistic/Describer/RealisticNullabilityNoiseTests.cs` of `DatabaseTests/Postgres/` and `DatabaseTests/SqlServer/`, where only SQL Server's noise test lists the noisy columns when it fails.
8. **The watch tests share their `FileChange` factories through the test class `WatchTests`** (`SqlSource.Tool.Tests/Commands/WatchTests.cs:33`, `:35`), which five classes forward to or reach into 14 times (`WatchFolderTests.cs:85`, `:139`, `:144`, `:157`, `:161`, `:197`, `:249`, `:255`, `:287`, `:293`; `WatchEndingTests.cs:87`; `WatchDuringAPassTests.cs:40`; `WatchFailureTests.cs:120`; `WatchChangeTests.cs:59`), while `Watching/WatchSetTests.cs:28-34` and `Commands/WatchFolderTests.cs:39-41` define their own and 30 sites write `new FileChange(...)` inline.
9. **38 sites write the pair `CompilationErrors.ShouldBeEmpty(); GeneratedCodeWarnings.ShouldBeEmpty();`** that `GeneratorRun.Problems()` (`SqlSource.Testing/Generator/GeneratorHarness.cs:532`) asserts: 37 in `SqlSource.Tests/Generator/` (`ModelNameTests` 2, `ModelTests` 4, `MethodTests` 2, `PlacementTests` 1, `GeneratedSourceWarningTests` 2, `SharedModelTests` 15, `TableModelTests` 2, `SqlClientProbeTests` 1, `ColumnMarkerTests` 1, `SettingLevelTests` 1, `ModelWarningTests` 2, `MethodDriverTests` 4) and `SqlSource.Tests/Generation/Converters/ConverterReaderTests.cs:61`.
10. **The same run wrapper is written in pairs of generator classes**, which may share through `SqlSource.Testing/Generator/`: `ModelNameTests.cs:521` and `ColumnMarkerTests.cs:284`; `NpgsqlProbeTests.cs:426`, `:429` and `SqlClientProbeTests.cs:390`, `:393`, each with a private `Source` equal to `GeneratorHarness.SampleSource(null)`; `ListPaddingParameterTests.cs:38` and `ArrayPoolParameterTests.cs:154`; `DialectTests.cs:155` and `SettingsFileTests.cs:702`, `:704`; and `new SourceFile(GeneratorHarness.SourcePath, GeneratorHarness.SampleSource(null))` 13 more times in five classes.
11. **A describer fixture is looked up by name with the same lambda 10 times**: `SqlSource.Tool.Tests/BundleReplays.cs:34`, `BundleLeaksTests.cs:34`, `PostgresRenamings.cs:29`, `DiagnoseScene.cs:58`, `Commands/ReplayTests.cs:64`, `Bundles/Renaming/SqlServer/SqlServerFixtureRenamingTests.cs:106`, `SqlServerErrorRenamingTests.cs:491`, `:547`, `Bundles/BundleReplayTests.cs:235`, `Bundles/BundleReaderTests.cs:41`.
12. **PostgreSQL's catalog words are read by two copies of one query and loop**: `DatabaseTests/Postgres/Renaming/VocabularyTests.cs:32-56` and `:132`, and `DatabaseTests/Postgres/Renaming/RealisticRenamingTests.cs:44-57` and `:178`.
13. **The `AnalyzerConfigOptions` fake is written three times**: `SqlSource.Testing/Generator/TestOptionsProvider.cs:38`, `SqlSource.Tests/Generation/Types/MappedTypes/TransitivePackageAssembliesTests.cs:100` and `SqlSource.Tests/Generation/Chains/BuildFoldersTests.cs:142`.
14. **Helpers that need no driver are copied across the end-to-end projects**: `CollectionOf` and `IdsOf` in the four `ListQueryTests.cs` (`DatabaseTests/Postgres/EndToEnd:475`, `DatabaseTests/SqlServer/EndToEnd:428`, `DatabaseTests/Postgres.Npgsql8:476`, `DatabaseTests/SqlServer.SqlClient61:427`); `ElementOf` and `ShouldBeSame` in the four `JsonQueryTests.cs` (`:291`, `:378`, `:291`, `:378`); `CountingSequence` at `DatabaseTests/Postgres/EndToEnd/AllTypesQueryTests.cs:364` and `DatabaseTests/Postgres.Npgsql8/AllTypesQueryTests.cs:256`; and `PropertiesOf` at `DatabaseTests/TestHarness/EndToEnd/AllTypesRows.cs:202` and `SqlServerAllTypesRows.cs:191`, both beside `ModelReflection.PropertiesOf`.  `DatabaseTests/Postgres.Npgsql8/AllTypesQueryTests.cs:121`, `:123` call `GetProperties()` where `tests/AGENTS.md` sends model reflection through `ModelReflection`.
15. **The bundle tests spell one path and one sample `BundleInfo` again and again**: `"/work/report.zip"` 11 times in `SqlSource.Tool.Tests` (`RenamingBundles.cs:31`, `PostgresRenamings.cs:24`, and `Bundles/` `BundleErrorsTests.cs:10`, `DiagnoseBundlesTests.cs:23`, `BundleJsonTests.cs:11`, `BundleReplayTests.cs:25`, `BundleQueryFileTests.cs:17`, `BundleDescribersTests.cs:15`, `EntryDifferencesTests.cs:13`, `BundleReaderTests.cs:186`, `Renaming/Postgres/PostgresRefusalTests.cs:234`), and the sample bundle's versions five times (`Bundles/BundleJsonTests.cs:12`, `BundleWriterTests.cs:15`, `BundleReaderTests.cs:18`, `BundleReplayTests.cs:410`, `Commands/ReplayTests.cs:47`).
16. **Two private `IQueryDescriber` fakes differ only in the exception they throw**: `SqlSource.Tool.Tests/Bundles/BundleReplayTests.cs:137` (`MissingDescriber`) and `SqlSource.Tool.Tests/Describing/DatabaseRunsFaultTests.cs:370` (`ThrowsBeforeItReturns`).

### Uneven conventions

17. **The SQL Server renaming tests assert with xunit**, where `tests/AGENTS.md` says assertions are Shouldly's: `DatabaseTests/SqlServer/Renaming/VocabularyTests.cs:48`, `:73`, `:74` and `DatabaseTests/SqlServer/Renaming/CorpusWordsTests.cs:37`, `:45`, `:54`, `:65`, `:76`, `:77`, the only `Assert.*` calls of `tests/`.
18. **The PostgreSQL floor image is a literal that rule 5 of the root `AGENTS.md` does not list**: `DatabaseTests/Postgres/Renaming/VocabularyTests.cs:24` (`"postgres:14.24"`) and the header at `:65`.  Moving the floor by that rule and `DatabaseTests/Postgres/README.md` leaves the vocabulary's check skipping on the new floor, with nothing failing.
19. **117 test names are not `Method_Scenario_Outcome`**: 102 have two parts and 15 four, out of 7,105 tests.  Every `[Fact]` or `[Theory]` whose method name does not hold exactly two underscores is one; most are in the new bundle tests (`Bundles/Renaming/` 21), the generator's `LibraryTypeTests` (9) and the settings tests (11).  Four prefixes of `SqlSource.Tests/Generator/LibraryTypeTests.cs`, `Detected_`, `NotDetected_`, `Default_` and `CodeGen_`, name a scenario where the method goes.

### Comments that said more than their test checks

The review corrected each comment to say what its test asserts; what is not tested remains.

20. `DatabaseTests/SqlServer/Describer/ParameterTests.cs`, `DescribeAsync_DeclaredVariable_IsNoParameter`: builds its request with an empty list, so the plan's handling of a `DECLARE`d variable is never run.
21. `SqlSource.Tests/Generator/CachingConverterTests.cs`, `Run_NodaTimeReferenceAdded_RunsLibraryConvertersAndConverters`: no step of the files' types (`FileTypesInput`, `FileTypes`, `TypedFile`) is checked to stay cached.
22. `SqlSource.Tests/Generator/CachingSettingsFileTests.cs`, `Run_BuildFolderChanged_ReachesTheSameSettingsFiles`: nothing checks that no diagnostic is reported again.
23. `SqlSource.Tests/Generator/LibraryConversionTests.cs`, `Run_OverrideWithTheSwitchOff_GetsTheConversion`: no row runs with the library's switch on.
24. `SqlSource.Tests/Generator/MethodTests.cs`, `Run_MixedType_HasConstantsMethodsAndModels`: nothing checks that the method is public or on the type.
25. `SqlSource.Tests/Generator/ParameterTypeTests.cs`, `Run_ParamMarker_WinsOverTheTypeOverride`: wins over two levels, the query's marker and the project's settings file, not over a file header or a folder's settings file.
26. `SqlSource.Tests/Generator/SqlClientProbeTests.cs` (around `:110`): nothing checks whether a model file is written below 6.1 or on it.
27. `SqlSource.Tests/Generator/TypeOverrideLevelTests.cs` (around `:309`): one pairing of spellings is run, `_int4` in the project's file under `int4[]` in the query.
28. `SqlSource.Tests/Parsing/Quoting/QuoteReaderTests.cs`: no row holds a circled digit, which `QuoteReader` refuses for SQL Server.
29. `SqlSource.Tests/Parsing/Markers/SqlMarkerScopeTests.cs`: the spans of the placement markers are checked in a query's scope alone, not in the preamble's or the whole file's.
30. `SqlSource.Tests/SettingsFiles/SettingsFileSchemaTests.cs`, `Schema_AnyOf_IsOnTheChoicesTheDialectAndTheParametersAlone`: looks at the top-level properties and their `items` only, so the `anyOf` of `libraryTypes.propertyNames`, `libraryTypes.additionalProperties` and `columnTypes.additionalProperties` are outside it, and no branch is checked to be an enum or a pattern.
31. `SqlSource.Tool.Tests/Watching/WatchSetTests.cs`, `Classify_NewContentForAListedFileOutsideTheProject_IsEvaluate`: two rows are in the project's own folder, against the name; `src/App/New.sql` repeats a case of `Classify_CreatedOrRenamedUnlistedFileUnderTheProject_IsEvaluate`.
32. `SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerFieldsTests.cs`: no test walks the records of `src/SqlSource.Tool/Describing/SqlServer/SqlServerCalls.cs`, so a string member that no fixture fills and the hand-written table leaves out has no check (TD-0108 item 8).
33. `SqlSource.Tool.Tests/Commands/DiagnoseScrubTests.cs`, `Diagnose_ScrubOfTheRawBundle_GivesWhatDiagnoseGives`: the zips' entry lists and the contents of `query.sql` and `capture.json` are not compared.
34. `SqlSource.Tool.Tests/Commands/CheckFilterTests.cs`, `Check_NoSidecars_CountsEveryQueryUnderDiffer`: no test checks the count of `differ` for a sidecar that cannot be read.
35. `SqlSource.Tool.Tests/Commands/SecretTests.cs`, `Run_ValueOnTheCommandLineAndEveryErrorWithLog_IsNeverLogged`: the log is checked for `SQLSRC999` alone; the other errors are checked on standard error only.
36. `SqlSource.Tool.Tests/Commands/WatchTests.cs`, `Watch_Log_IsOneFileForTheSession`: an event logged between two passes would go unseen.
37. `SqlSource.Tool.Tests/Describing/ConnectionTests.cs`, `UnusedNames_NameCloseToAnUnselectedDatabaseOfThePlanIgnoringCase_IsKept`: its name is near a selected database, not an unselected one, against the test's name.
38. `SqlSource.Tool.Tests/Describing/SqlServer/Proofs/OperandsQueryTests.cs` (around `:80`): every row is of an operand answered not null, so a nullable operand's CLR check is untested.
39. `SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs`, `Run_ReferenceThatNamesAnotherColumnUnderALeftJoin_KeepsTheJoinsAnswer`: the walk's reason, `output-differs`, is never checked.
40. `SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanInstancesTests.cs`, `HasSetOperation_AppendOrSetOpUnderTheRoot_IsTrue`: PostgreSQL 14's `SetOp` over an `Append` is never built.
41. `SqlSource.Tool.Tests/Logging/RunLogFileTests.cs`, `Open_PathThatCannotBeWritten_GivesNoLogAndTheErrorWithTheReason`: nothing compares the error's path with the full path where one can be made.

### Found in the folders the first sweep left out

42. **`ToolDiagnosticsTests` formats a descriptor's message by hand 41 times** (`SqlSource.Tests/Diagnostics/ToolDiagnosticsTests.cs`, `:65` to `:529`), where `SqlDiagnosticsTests.cs:305` has a private `Format` used 18 times and `SqlSource.Tool.Tests/Reported.cs:15` a third copy; the help-link and id-shape checks are written in both classes (`ToolDiagnosticsTests.cs:48`, `:31-32`; `SqlDiagnosticsTests.cs:64`, `:59-60`), and four `ForParseError_` facts differ only in kind and id (`SqlDiagnosticsTests.cs:96`, `:100`, `:112`, `:125`).
43. **The `Commands` classes rebuild the `.env` and manifest arrange that `DescribeScene` lacks**: `DotEnvPath` four times (`SqlSource.Tool.Tests/Commands/DescribeRunTests.cs:30`, `DotEnvRunTests.cs:29`, `OnlineRunTests.cs:37`, `SecretTests.cs:33`), the `.env` written inline eight times (`DotEnvRunTests.cs:39`, `:51`, `:64`, `:127`, `:227`; `ManifestRunTests.cs:63`; `OnlineRunTests.cs:191`; `VerboseTests.cs:62`), the line `SQLSOURCE_CONNECTION_BILLING=Host=from-the-file` four times, `obj/App.csproj.manifest` written six times (`ManifestRunTests.cs:30`; `OnlineRunTests.cs:41`, `:338`, `:372`, `:468`, `:585`), and one `Invoices` constant and constructor in `DotEnvRunTests.cs:14-25` and `ManifestRunTests.cs:16-25`.
44. **`VariantCopyTests` is one body in both engines**: `DatabaseTests/Postgres/EndToEnd/VariantCopyTests.cs:29` and `DatabaseTests/SqlServer/EndToEnd/VariantCopyTests.cs:36`.
45. **The test settings file's place, `SettingPlace.OfSettingsFile("/app/sqlsource.json", 1)`, is a private field of 12 classes of `SqlSource.Tests/Settings/` and `SqlSource.Tests/SettingsFiles/` under four names**, and the root file's, `("/sqlsource.json", 0)`, of three (`Settings/SettingsLevelTests.cs:12`, `Settings/TypeOverrides/TypeOverrideMapTests.cs:15`, `Settings/ColumnTypes/ColumnTypeMapTests.cs:15`).
46. **SQL Server's `StreamingQueryTests`, 928 lines, ends with 12 tests of queries of several statements** (`DatabaseTests/SqlServer/EndToEnd/StreamingQueryTests.cs:636` to `:904`), a second subject beside the 28 of the stream's lifecycle that PostgreSQL's twin holds alone.
47. **`SettingsFileReaderTests.DesignsExample` is shared by reaching into a test class**: defined at `SqlSource.Tests/SettingsFiles/SettingsFileReaderTests.cs:21` and read by `SettingsFileReaderRobustnessTests.cs:20`, `:36` and `SettingsFileSchemaTests.cs:336`.
48. **SQL Server's end-to-end tests open their rolled-back transaction through copies of one helper**: `EndToEndSources.RolledBackAsync` (`DatabaseTests/SqlServer/EndToEnd/EndToEndSources.cs:50`, 36 calls) beside a private `BeginAsync` in `Json/JsonQueryTests.cs:372` (and its copy) and three copies in `DatabaseTests/SqlServer.SqlClient61/` (`NodaTimeQueryTests.cs:200`, `NetTopologySuiteQueryTests.cs:290`, `MethodAllTypesTests.cs:178`).
49. **`BuildFileTests` writes one helper twice across its partial files**: `AttributesOf` (`SqlSource.Tests/Package/BuildFileTests.cs:512`) and `Attributes` (`BuildFileTests.Online.cs:740`), 10 callers between them; `Outer` (`BuildFileTests.Online.cs:49-50`) loads its file outside `Load` (`BuildFileTests.cs:806`); and its theories over the build files are `BuildFile_` at `BuildFileTests.cs:760` and `BuildFiles_` elsewhere.
50. **`TypeOverrideGrammarTests` writes out its `Read` helper (`SqlSource.Tests/Settings/TypeOverrides/TypeOverrideGrammarTests.cs:484`) four times** (`:84`, `:94`, `:109`, `:124`), three of them facts (`:91`, `:106`, `:121`) with the body of the theory at `:62`.
51. **Builders that `TestSidecars` already has are written again**: PostgreSQL's `integer` and a column at `SqlSource.Tool.Tests/Realistic/RealisticNoiseTests.cs:80`, `:94` and `Realistic/RealisticDescriptionsTests.cs:59`, `:62`, and `integer` at `Describing/EntryBuilderTests.cs:19`.
52. **`QuerySettingsTests.Turn` (`SqlSource.Tests/Settings/QuerySettingsTests.cs:511`, six calls) is `TestModels.ParametersOn` and `ParametersOff`.**
53. **The attribute's metadata name is a literal** at `SqlSource.Tests/EndToEnd/EndToEndTests.cs:178` and the two engines' `EndToEnd/ModelShapeTests.cs` (`:43`, `:37`), where `AttributeSource.AttributeMetadataName` is visible to all three.
54. **58 test names of three parts name something other than the method they call**: 23 in `DatabaseTests/Postgres/EndToEnd/ListQueryTests.cs` and 22 in `DatabaseTests/SqlServer/EndToEnd/ListQueryTests.cs`, which name the query in short form (`Ids_` for `ListAccountsByIds`), with their copies in the driver floor projects; 12 of `SqlSource.Tests/EndToEnd/EndToEndTests.cs` whose method part is a scenario (`ProjectWithValidationOff_`, `ProjectWithADialect_`, `ProjectWithParameters_`, `Method_`); and `SqlSource.Tests/Settings/SettingsLevelReaderTests.cs:448`, with the three `LibraryTypes_` names of item 19 beside it.

## Why it exists

The bundle and renaming tests were written by sessions working in parallel, each with the helpers of its own engine; the older items grew one class at a time.  The review records them and does not refactor, so that its pull request stays a review.

## Impact

Low each, Medium together: a fix to one copy leaves the other wrong, as the noise tests of item 7 already show, item 18 lets a check stop running without a failure, and items 20 to 41 are cases a reader may think are covered.

## Proposed fix

One item, or a few related ones, at a time:

1. Expose the plain-word scan, with a switch for `@@`, and the quoted-name reader from `SqlSource.Tool.Testing/Bundles/`.
2. and 3. A `Key(byte)` and a real engine's `RenameAsync` beside `RawBundles`, and a `SqlServerRenamings` at the root of `SqlSource.Tool.Tests` mirroring `PostgresRenamings`.
4. Split `BundleLeaks` by engine, give SQL Server's search an overload that finds the engine's rows itself, and move the `Provenance` test to a class of its own.
5. Make `RawBundles.OutcomeOf` visible and call it.
6. A `DiagnoseCommands` body in `SqlSource.Tool.Testing`, with `ReadBundle` and the "reproduced" line, and one-line tests per engine, as `ErrorPaths` is.
7. `RealisticDapperChecks`, `RealisticMethodChecks` and `RealisticNoiseChecks` in `SqlSource.Tool.Testing/Realistic/`, with PostgreSQL's noise test listing its columns.
8. One static `FileChanges` with `Changed`, `Created`, `Deleted`, `CreatedFolder`, `Renamed` and `PassStarts`.
9. `run.Problems()` or `run.ShouldBeClean()` at each site.
10. `ModelRuns.CleanRun`, `GeneratorHarness.RunWithReferences`, `MethodRuns.ListUsers` and `GeneratorHarness.SampleSourceFile`.
11. `FixtureCases.Named(engine, name)` and `FixtureCases.SchemaOf(engine)`.
12. One `PostgresCatalogWords.ReadAsync`, with a comment on why the realistic check reads a subset.
13. One internal `TestAnalyzerConfigOptions` in `SqlSource.Testing/Generator/`.
14. Move the helpers into `DatabaseTests/TestHarness/EndToEnd/`, make `PropertiesOf` one over `ModelReflection`, and use `ModelReflection.NamesOf` in the Npgsql 8 test.
15. One bundle path constant and a `BundleInfos.Sample(...)`.
16. One `ThrowingDescriber(Func<OpenRequest, Exception>)` at the root of `SqlSource.Tool.Tests`.
17. Shouldly's assertions, as PostgreSQL's `VocabularyTests` writes them.
18. A `DefaultImages.PostgresFloor` used by `VocabularyTests`, named in rule 5 and in `DatabaseTests/Postgres/README.md`.
19. Give each two-part name a scenario and merge each four-part one into three.
20. to 41. Add the missing case or assertion, or narrow the test's name where it says too much.
42. One `Format(DiagnosticDescriptor, params object[])` in `SqlSource.Testing`, the help-link and id-shape checks as one theory over both `All`s, and the four facts as rows.
43. `DotEnvPath`, `WriteDotEnv` and `WriteManifest` on `DescribeScene`, and `SqlTexts.GetInvoice`.
44. A `VariantCopies.ShouldShareHash` in `DatabaseTests/TestHarness/EndToEnd/`.
45. One `TestPlaces` with `SettingsFile` and `RootSettingsFile` in `SqlSource.Tests/Settings/`.
46. Move the 12 batch tests into a `StreamingBatchTests`, with the constants only they use.
47. A static `SettingsFileExamples.Design` in `SqlSource.Tests/SettingsFiles/`.
48. `RolledBackAsync` in the JSON tests, and one project-local helper in `SqlServer.SqlClient61`.
49. One `AttributesOf`, a `Load` that takes the folder, and one prefix.
50. Make the three facts rows of the theory, and call `Read`.
51. `TestSidecars.Postgres()` and `TestSidecars.Column(...)`.
52. Call `TestModels.ParametersOn` and `ParametersOff`.
53. `AttributeSource.AttributeMetadataName`.
54. Name each test after the method it calls, in the originals and their copies together.

## Trigger

A change to the tests of an item, or the next maintainability review.
