# TD-0074 - Duplicated tests across the engine projects

## Problem

A maintainability review of `tests/` on 2026-10-22 found these, besides what [TD-0039](TD-0039-duplicated-and-uneven-patterns-in-the-tests.md) holds.  Most come from the SQL Server test projects, which phase 6 built by copying PostgreSQL's.  No test is wrong; each copy is one more place a change has to reach.

1. **The realistic corpus's harness is copied per engine,** about 720 lines on each side under `tests/SqlSource.Tests.Postgres/Realistic/` and `tests/SqlSource.Tests.SqlServer/Realistic/`.  `Generation/RealisticClaims.cs`, `RealisticClaimsTests.cs` and `RealisticModelNullabilityTests.cs` differ by their namespace, `RealisticModels.cs` by one token, `Describer/RealisticDescribe.cs` by five lines, and `ModelOf` and `ArgumentsOf` of `RealisticParameters.cs` are the same; the unit tests of `RealisticClaims.Parse` run in both container projects.  `Describer/FixtureCasesTests.cs`, `Describer/ToolProcessTests.cs`, `EndToEnd/ErrorPathTests.cs` and `EndToEnd/ShapeQueryTests.cs` have the same shape on both sides.
2. **Reading a model by reflection is written in five projects.**  `Shape`, `ModelNamed`, `ShouldBeModel`, `ShapeOf` and `PropertiesOf` are in both `EndToEnd/ModelShapeTests.cs`; `PropertiesOf` again in both `AllTypesQueryTests.cs` of `SqlSource.Tests.Postgres.Npgsql8` and `SqlSource.Tests.SqlServer.SqlClient61` and both `RealisticModels.cs`, and `NamesOf` and `ColumnOf` in both end-to-end `AllTypesQueryTests.cs`.  The 32 properties of SQL Server's `all_types` are listed in [`ModelShapeTests`](../../tests/SqlSource.Tests.SqlServer/EndToEnd/ModelShapeTests.cs) and in [`AllTypesQueryTests`](../../tests/SqlSource.Tests.SqlServer.SqlClient61/AllTypesQueryTests.cs).
3. **The driver tests' file and type are written out each time.**  The text `-- name: GetUser\nSELECT 1;\n` has 44 copies in 17 files, ten classes of `tests/SqlSource.Tests/Generator/` declare the same `Users` file, and `[SqlSourceGenerate] public partial class Sample;` has 13 copies in 11 classes, two of them built by identical helpers in `GeneratorParameterLevelTests` and `SettingLevelTests`.  `SqlTexts.GetUser` exists, in `tests/SqlSource.Tool.Tests`, which `Generator/` cannot see.
4. **The caching tests repeat one assertion.**  "Only `Users.sql` is read again" is the same dictionary six times and the same `ParsedFile` block five times in [`CachingOptionsTests`](../../tests/SqlSource.Tests/Generator/CachingOptionsTests.cs) and [`CachingParseTests`](../../tests/SqlSource.Tests/Generator/CachingParseTests.cs).
5. **Test classes that cover several subjects.**  [`NullabilityWalkTests`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs), about 1,550 lines and 96 tests, covers tables and views, `NOT NULL`, system columns, aggregates, subplans, aliases, subqueries, partitions and expressions over joins, with its fixtures between the tests.  [`SqlServerSessionTests`](../../tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSessionTests.cs), about 920 lines, and [`SqlServerEventsTests`](../../tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerEventsTests.cs), about 880, are not split by call as `PostgresSession*Tests` and `PostgresEvents*Tests` are.  [`RedactionTests`](../../tests/SqlSource.Tool.Tests/Describing/RedactionTests.cs) puts thirteen tests of `Redaction.Of` among twenty of whole runs, and [`RunLogTests`](../../tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs) mixes what the log says with where it goes.
6. **Fakes and helpers of the describers' tests are written per project.**  `IRunLog` has `RecordingLog` and the thread-safe `ConcurrentLog` of [`DriverLogTests`](../../tests/SqlSource.Tests.SqlServer/Describer/DriverLogTests.cs), and the lookup of a member is written three times; five fakes of `IDescribeExchange` call the live one and record or hook; `TextsOf(DescribeFailure)` is in both `Describer/Sessions.cs`, and a failure's formatted message is built three times.
7. **The containers' gates on a version are written twice.**  `TagOf`, the image from the environment, `SkipBelow` and `SkipUnlessRecordedMajor` are in [`PostgresContainer`](../../tests/SqlSource.Tests.Postgres/PostgresContainer.cs) and [`SqlServerContainer`](../../tests/SqlSource.Tests.SqlServer/SqlServerContainer.cs), with their five tests twice, and each engine's recorded majors are literals both there and in [`RealisticTestCases`](../../tests/SqlSource.Tool.Tests/RealisticTestCases.cs).
8. **Small copies.**  [`RealisticCaseReaderTests`](../../tests/SqlSource.Tool.Tests/Realistic/RealisticCaseReaderTests.cs) arranges a case folder by hand fourteen times; the program that waits is chosen three times, in `ProcessRunnerTests`, `ProgramsTests` and `TerminationTests`; setting and restoring `CurrentCulture` is written three times; and 37 of about 3,600 test names do not have the three parts of `Method_Scenario_Outcome`.

## Why it exists

The SQL Server projects were made by copying PostgreSQL's, which kept each readable alone.  `tests/SqlSource.Tests.RoslynFloor` compiles `tests/SqlSource.Tests/Generator/` whole, so a helper those tests share has to live in `tests/SqlSource.Testing/Generator` within Roslyn 4.8, and items 3 and 4 were copied rather than moved there.

## Impact

Low.  A change to the realistic harness, a model's shape or the containers' gates reaches two projects, and a large class makes a failure harder to place.

## Proposed fix

1. Move `RealisticClaims`, `RealisticModels` (given an assembly, a namespace and an engine), `ModelOf` and the realistic paths to `tests/SqlSource.Tool.Testing/Realistic/`, with their unit tests in `tests/SqlSource.Tool.Tests/Realistic/`; give the second-tier classes helpers there that take the engine.  `ArgumentsOf` needs Dapper, which neither library references.
2. A `ModelReflection` in `tests/SqlSource.Testing/EndToEnd/`, and the list of `all_types` in `SqlServerAllTypesRows`.
3. `GeneratorHarness.UsersFile` and `GeneratorHarness.SampleSource(arguments)` in `tests/SqlSource.Testing/Generator`, and `SqlTexts` in `tests/SqlSource.Testing`.
4. `CachingScene.ShouldHaveReadOnly(result, file)`.
5. Split each class by subject or by call, sharing through `PostgresWalkColumns` and `SqlServerScript`; move the whole-run tests of `RedactionTests` to `Logging/`, and the tests of the log's destination to a class of their own.
6. A thread-safe `RecordingLog` with `Member(name)`, a `HookedExchange(before, after)` and `Failures.TextsOf` and `MessageOf`, in `tests/SqlSource.Tool.Testing`.
7. An `EngineImage` in `tests/SqlSource.Testing` beside `DefaultImages`, holding the variable, the default, the recorded majors and the gates, with one test class.
8. A `CaseFolder` helper, `Programs.Waiter(seconds)`, a disposable scope of the culture, and the names renamed or allowed by `tests/AGENTS.md`.

## Trigger

A third engine's test project, or a change that has to reach both copies of an item.
