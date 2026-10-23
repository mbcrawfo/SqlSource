# TD-0074 - Duplicated tests across the engine projects

## Problem

A maintainability review of `tests/` on 2026-10-22 found these, besides what [TD-0039](TD-0039-duplicated-and-uneven-patterns-in-the-tests.md) holds.  Most come from the SQL Server test projects, which phase 6 built by copying PostgreSQL's.  No test is wrong; each copy is one more place a change has to reach.  The numbers are the review's: items 2 and 7 are fixed, and item 1 is reduced to what remains.

1. **`ShapeQueryTests` is written in both engine projects, and stays so.**  [`SqlSource.Tests.Postgres`](../../tests/SqlSource.Tests.Postgres/EndToEnd/ShapeQueryTests.cs) and [`SqlSource.Tests.SqlServer`](../../tests/SqlSource.Tests.SqlServer/EndToEnd/ShapeQueryTests.cs) each hold four tests that read the project's own generated models (`SearchAccountsView`, `ShapeQueries` and the rest), which are different types in each assembly, and that differ otherwise by the two lines that open a connection of each driver.  No helper can take them without reflection over every model, which would hide what each test reads, so this is a copy kept on purpose.
3. **The driver tests' file and type are written out each time.**  The text `-- name: GetUser\nSELECT 1;\n` has 44 copies in 17 files, ten classes of `tests/SqlSource.Tests/Generator/` declare the same `Users` file, and `[SqlSourceGenerate] public partial class Sample;` has 13 copies in 11 classes, two of them built by identical helpers in `GeneratorParameterLevelTests` and `SettingLevelTests`.  `SqlTexts.GetUser` exists, in `tests/SqlSource.Tool.Tests`, which `Generator/` cannot see.
4. **The caching tests repeat one assertion.**  "Only `Users.sql` is read again" is the same dictionary six times and the same `ParsedFile` block five times in [`CachingOptionsTests`](../../tests/SqlSource.Tests/Generator/CachingOptionsTests.cs) and [`CachingParseTests`](../../tests/SqlSource.Tests/Generator/CachingParseTests.cs).
5. **Test classes that cover several subjects.**  [`NullabilityWalkTests`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs), about 1,550 lines and 96 tests, covers tables and views, `NOT NULL`, system columns, aggregates, subplans, aliases, subqueries, partitions and expressions over joins, with its fixtures between the tests.  [`SqlServerSessionTests`](../../tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerSessionTests.cs), about 920 lines, and [`SqlServerEventsTests`](../../tests/SqlSource.Tool.Tests/Describing/SqlServer/SqlServerEventsTests.cs), about 880, are not split by call as `PostgresSession*Tests` and `PostgresEvents*Tests` are.  [`RedactionTests`](../../tests/SqlSource.Tool.Tests/Describing/RedactionTests.cs) puts thirteen tests of `Redaction.Of` among twenty of whole runs, and [`RunLogTests`](../../tests/SqlSource.Tool.Tests/Logging/RunLogTests.cs) mixes what the log says with where it goes.
6. **Fakes and helpers of the describers' tests are written per project.**  `IRunLog` has `RecordingLog` and the thread-safe `ConcurrentLog` of [`DriverLogTests`](../../tests/SqlSource.Tests.SqlServer/Describer/DriverLogTests.cs), and the lookup of a member is written three times; five fakes of `IDescribeExchange` call the live one and record or hook; `TextsOf(DescribeFailure)` is in both `Describer/Sessions.cs`, and a failure's formatted message is built three times.
8. **Small copies.**  [`RealisticCaseReaderTests`](../../tests/SqlSource.Tool.Tests/Realistic/RealisticCaseReaderTests.cs) arranges a case folder by hand fourteen times; the program that waits is chosen three times, in `ProcessRunnerTests`, `ProgramsTests` and `TerminationTests`; setting and restoring `CurrentCulture` is written three times; and 37 of about 3,600 test names do not have the three parts of `Method_Scenario_Outcome`.

## Why it exists

The SQL Server projects were made by copying PostgreSQL's, which kept each readable alone.  `tests/SqlSource.Tests.RoslynFloor` compiles `tests/SqlSource.Tests/Generator/` whole, so a helper those tests share has to live in `tests/SqlSource.Testing/Generator` within Roslyn 4.8, and items 3 and 4 were copied rather than moved there.

## Impact

Low.  A change to the four tests of `ShapeQueryTests` reaches two projects, and a large class makes a failure harder to place.

## Proposed fix

1. None for now: a helper over reflection would read every model for the tests and hide what each reads.  A third engine's project is the time to look again.
3. `GeneratorHarness.UsersFile` and `GeneratorHarness.SampleSource(arguments)` in `tests/SqlSource.Testing/Generator`, and `SqlTexts` in `tests/SqlSource.Testing`.
4. `CachingScene.ShouldHaveReadOnly(result, file)`.
5. Split each class by subject or by call, sharing through `PostgresWalkColumns` and `SqlServerScript`; move the whole-run tests of `RedactionTests` to `Logging/`, and the tests of the log's destination to a class of their own.
6. A thread-safe `RecordingLog` with `Member(name)`, a `HookedExchange(before, after)` and `Failures.TextsOf` and `MessageOf`, in `tests/SqlSource.Tool.Testing`.
8. A `CaseFolder` helper, `Programs.Waiter(seconds)`, a disposable scope of the culture, and the names renamed or allowed by `tests/AGENTS.md`.

## Trigger

A third engine's test project, or a change that has to reach both copies of an item.
