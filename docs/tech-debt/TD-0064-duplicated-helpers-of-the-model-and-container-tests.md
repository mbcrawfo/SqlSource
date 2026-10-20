# TD-0064 - Duplicated helpers of the model and container tests

## Problem

A maintainability review of `tests/` on 2026-10-19 found these, beyond what [TD-0039](TD-0039-duplicated-and-uneven-patterns-in-the-tests.md) records.  No test is wrong; each copy is one more place a change has to reach.

1. **The model tests' harness is copied into seven classes of `Generator/`.**  [`ColumnMarkerTests`](../../tests/SqlSource.Tests/Generator/ColumnMarkerTests.cs), [`ModelNameTests`](../../tests/SqlSource.Tests/Generator/ModelNameTests.cs), [`ModelTests`](../../tests/SqlSource.Tests/Generator/ModelTests.cs), [`QueryTypesTests`](../../tests/SqlSource.Tests/Generator/QueryTypesTests.cs), [`SharedModelTests`](../../tests/SqlSource.Tests/Generator/SharedModelTests.cs), [`TableModelTests`](../../tests/SqlSource.Tests/Generator/TableModelTests.cs) and [`TypeOverrideLevelTests`](../../tests/SqlSource.Tests/Generator/TypeOverrideLevelTests.cs) each have a `Run` at Models under postgres with Npgsql 10, an `Entry` that writes a sidecar entry for the query's hash, and most of `Column`, `Parameter`, `Int4` and the same `Source`.  `ModelFiles` is in three and `Catalog` in four.  `TestLayoutTests` keeps `Generator/` to test classes, since `tests/SqlSource.Tests.RoslynFloor` compiles it whole, so a shared helper has to go to `tests/SqlSource.Testing`, and each class wrote its own instead.
2. **Copied parser facts.**  In [`SqlFileParserTests`](../../tests/SqlSource.Tests/Parsing/SqlFileParserTests.cs), the theory of a marker without a value covers output, database and type-override, while the facts for token, param and column, and dialect in the preamble, have the same body; the facts of a marker that is not allowed outside a query are four copies of one; and the names give one outcome as `IsAnErrorAtTheMarker`, `IsInvalidAtTheMarker` and `IsInvalid`.
3. **A describer is opened the same way at eight places**, across three projects: [`Sessions`](../../tests/SqlSource.Tests.Postgres/Describer/Sessions.cs), [`PostgresScript`](../../tests/SqlSource.Tool.Tests/PostgresScript.cs), [`FixtureReplay`](../../tests/SqlSource.Tool.Testing/Fixtures/FixtureReplay.cs), and inline in `ReplayingExchangeTests`, `CapturingExchangeTests` and four times in `DescriberTests`.
4. **The whole command is run in process twice**: [`CliRun`](../../tests/SqlSource.Tool.Testing/CliRun.cs) and [`EndToEndTool`](../../tests/SqlSource.Tests.Postgres/EndToEnd/EndToEndTool.cs), whose writers are not of the invariant culture.
5. **A database is created and given its schema and seed by three loops**: two in [`PostgresContainer`](../../tests/SqlSource.Tests.Postgres/PostgresContainer.cs) and one in [`AppContainer`](../../tests/SqlSource.Tests.Postgres.Npgsql8/AppContainer.cs); `RepositoryPaths.EndToEndSchema` and `Seed` only pass `EndToEndFiles` through.  `PostgresContainer` cannot be shared, since its project brings Npgsql 10.
6. **The realistic corpus writes its skips more than once**: "needs a later major" three times with three messages (`RealisticDescribe`, `RealisticDapperTests`, `RealisticModelRefusalTests`), "refused" twice (`RealisticDescribe`, `RealisticModels`), and the committed sidecar is read twice (`RealisticModels`, `RealisticDescriptionTests`), all in [`Realistic/`](../../tests/SqlSource.Tests.Postgres/Realistic).
7. **Two large classes mix subjects.**  [`CachingTests`](../../tests/SqlSource.Tests/Generator/CachingTests.cs), about 1,840 lines, covers the caching of every step with about twenty helpers at its end; `ModelTests`, about 1,180 lines, holds a group about the warnings of obsolete and experimental types.
8. **Magic strings.**  `/app/Repo/Users.sql` is declared eleven times under two names and used about seventy times in twenty-two files, and its normalised form nine more; two `DescribeRequest` builders, [`PostgresRequests`](../../tests/SqlSource.Tool.Tests/PostgresRequests.cs) and [`Requests`](../../tests/SqlSource.Tests.Postgres/Describer/Requests.cs), both name the query `Q`.
9. **Names that break `Method_Scenario_Outcome`**: the tests of [`ModelShapeTests`](../../tests/SqlSource.Tests.Postgres/EndToEnd/ModelShapeTests.cs), which start with a type and not a method, and four-part names in `AccountQueryTests`, `NoteQueryTests`, `CliTests` and `NullabilityWalkTests`.

## Why it exists

Each sub-phase of phase 5 added its model tests beside the last's and copied the nearest helper, which the rule that `Generator/` holds only test classes made the easy choice.

## Impact

Low.  A change to how a model test writes its sidecar reaches seven classes, and a change to how a database is seeded reaches three loops.

## Proposed fix

1. `tests/SqlSource.Testing/Generator/ModelRuns.cs`, within Roslyn 4.8.0 and C# 12, with `Run`, `Entry`, `Column`, `Parameter`, `Int4`, `Catalog` and `ModelFiles`; or let `TestLayoutTests` allow a support folder of `Generator/` that both projects compile.  Decide which before starting.
2. Rows of the existing theory, and one theory of a marker outside a query, with one name for the outcome.
3. An `OpenAsync` extension of `IQueryDescriber` in `tests/SqlSource.Tool.Testing`.
4. A `CliRun` over `Hosts.Real`, and delete `EndToEndTool`.
5. `EndToEndFiles.CreateAsync` over `DbDataSource` in `tests/SqlSource.Testing`, which needs no Npgsql; drop the pass-throughs.
6. `RealisticCase.SkipUnlessRunsOn(major)` and `SkipIfRefused()`, and `RealisticModels.EntryOf` in `RealisticDescriptionTests`.
7. Split `CachingTests` by step with its helpers in `tests/SqlSource.Testing/Generator`, updating `tests/AGENTS.md`, and move the warnings group into a `ModelWarningTests`.
8. `GeneratorHarness.UsersPath` and its normalised form; one `DescribeRequests` in `tests/SqlSource.Tool.Testing`.
9. Rename them, folding the query or the time into the scenario.

## Trigger

The next change to how a model test writes its sidecar, or to how the container seeds a database; 7 before `CachingTests` gains another step.
