# TD-0054 - Duplicated and uneven patterns in the tests of the PostgreSQL describer

## Problem

A maintainability review of phase 3 on 2026-10-16 found these in `tests/SqlSource.Tool.Tests/Describing/Postgres/`, `tests/SqlSource.Tests.Postgres/` and the helpers they share.  No test is wrong; the helpers at the root of `tests/SqlSource.Tool.Tests` exist, and many classes write the same arrangement beside them.

1. **The session's script is built by hand twice.**  [`PostgresSessionTests`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs) has private builders, `Prepares`, `Columns`, `Frees`, `Checks`, `Explains`, `PlanOf`, `Resolves`, `Tables`, `Spells`, and [`PostgresEventsTests`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs) writes the same calls inline, with the statement name `"sqlsource_1"` 23 times.
2. **A column's nullability is a literal tuple about two hundred times in seven classes**, `(false, "catalog")`, each class with its own names for the common ones, and the projection to `(Nullable, NullableSource)` written seven times.
3. **Rows and plans built by hand.**  An expression's `PostgresColumnRow` with a raw OID about twenty times in eight classes; `users.id` as `ColumnOf(0, "id", 23, Users, 1)` 23 times, twice under two names in `PostgresEventsTests`; `EmailT254` defined twice and the modifier 258 bare in three files; `Append` and `Member` plan helpers declared in [`PlanPartitionsTests`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanPartitionsTests.cs), [`PlanInstancesTests`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanInstancesTests.cs) and [`PlanRelationsTests`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/PlanRelationsTests.cs); `Inferred`, `Declared` and the request builder copied in `ParameterPlaceholdersTests`, `PostgresErrorsTests` and `PostgresSessionTests`.
4. **The container tests repeat their helpers.**  Nine classes of [`Describer/`](../../tests/SqlSource.Tests.Postgres/Describer/) write their own `RunAsync`, `ExecuteAsync` or `ScalarAsync`, with `NpgsqlDataSource.Create(postgres.ConnectionString)` 27 times; `ShouldDescribeAsync` is in `ColumnTests`, `TableMatchTests` and `WalkTests`; `ColumnTests` and `WalkTests` query the server's major version though `PostgresContainer.Major` holds it; the skips "before 18 has no old and new in RETURNING" and "pinned for PostgreSQL 14 and 18 alone" are copied five and four times; a capture of a live describe takes three lines in ten tests; a session is opened four ways; `SessionTests` holds three near-identical exchanges that run a hook after one live call; `SessionTests` redeclares `Secrets.Marker`, and the `PGPASSWORD` skip is copied into both projects.
5. **Classes over six hundred lines that mix subjects.**  `PostgresSessionTests`, about 2,250 lines, covers about ten; [`NullabilityWalkTests`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs), about 1,700, holds the set operations with fixtures of their own; `PostgresEventsTests`, about 1,190.
6. **Uneven names.**  The container tests of `DescribeAsync` start `Describe_` and the unit tests `DescribeAsync_`; `PlanOutputTests` uses `Explain_` and `ExplainTests` `ExplainAsync_`; the 26 classes of `tests/SqlSource.Tests.Postgres` are `sealed` and 33 of the 34 in `Describing/Postgres/` are not.  [`CheckVerdictTests`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/CheckVerdictTests.cs) tests `NpgsqlCheck`.
7. **Theory rows named by their JSON.**  Eleven `TheoryData` of plans as `Text(...)`, eight in `NullabilityWalkTests`, so a failing row is named by its plan.
8. **Small duplicates.**  `GetUser` and `Awkward` copied between the capture tests; the replay connection written as `PostgresScript.Connection`, `FixtureReplay.Connection` and `"replay"`; the engine as `"postgres"` in about twenty places beside `SidecarValues.Engine.Postgres`; `FixtureCases.Of("postgres", ...)` three times; facts of `FixtureCase` and `FixtureCases` in `FixtureReplayTests`, and the case writer copied three times.

## Why it exists

Each sub-phase of phase 3 wrote its tests beside the last's, and the container tests and the unit tests were written by different sub-phases from different helpers.

## Impact

Low.  A change to the statement name, a nullability source or the users table reaches dozens of lines in several classes; 5 and 7 make a failure harder to place.

## Proposed fix

1. Fluent `Prepares`, `Frees` and the rest on `PostgresScript`, used by both classes.
2. One `NullabilityAnswers` in `tests/SqlSource.Tool.Testing` of named tuples from `SidecarValues.NullableSource` and an `Of(...)` projection.
3. `PostgresTables.ExpressionOf` and `PostgresTables.UsersId`, `PostgresRows.X.Oid` for every OID, `PostgresRows.EmailT254` and a `Typmods` class, `PostgresPlans.Append` and `PostgresPlans.Member`, one `PostgresRequests`.
4. `ExecuteAsync`, `ScalarAsync`, `SkipBelow` and `SkipUnlessRecordedMajor` on `PostgresContainer`; `ShouldDescribeAsync` and `Recording()` on `Describer/Sessions.cs`, whose `OpenAsync` takes a database for `RealisticDescribe`; one `AfterLiveExchange`; `Secrets.Marker` and a shared `PGPASSWORD` skip in `SqlSource.Tool.Testing`.
5. After 1, split `PostgresSessionTests` and `PostgresEventsTests` by subject, and move the set operations to `NullabilityWalkSetOperationTests`.
6. Choose one convention for each and record it in `tests/AGENTS.md`; rename `CheckVerdictTests` to `NpgsqlCheckTests` once the container class of that name is told apart.
7. Labelled rows, as `WalkMatrixTests` keys its cases.
8. Share the capture tests' arrangement, point `PostgresScript.Connection` at `FixtureReplay.Connection`, use `SidecarValues.Engine.Postgres`, and move the fixture facts to the classes that mirror them.

## Trigger

A change to the statement name, to a nullability source or to the fixture schema that has to touch more than three test classes; or the first test of a second engine's describer, which should start from shared helpers.
