# TD-0054 - Duplicated and uneven patterns in the tests of the PostgreSQL describer

## Problem

A maintainability review of phase 3 on 2026-10-16 found eight of these in `tests/SqlSource.Tool.Tests/Describing/Postgres/`, `tests/SqlSource.Tests.Postgres/` and the helpers they share.  Items 1 and 3, and the parts of items 2 and 8 in `tests/SqlSource.Tool.Tests`, are resolved; the numbers of the rest are the review's.  No test is wrong; each copy is one more place a change has to reach.

- **2. A column's nullability is a literal tuple in five container classes**, `(false, "catalog")`, each with its own names for the common ones (`ScanTests`' `Nullable`, `ExpressionTests`' `NotNull` for an expression, `SetOperationTests`' `Proven`), and the projection to `(Nullable, NullableSource)` written in `ColumnTests`, `WalkTests`, `ScanTests`, `ExpressionTests` and `SetOperationTests`, though `NullabilityAnswers` in `tests/SqlSource.Tool.Testing` names them.
- **4. The container tests repeat their helpers.**  Nine classes of [`Describer/`](../../tests/SqlSource.Tests.Postgres/Describer/) write their own `RunAsync`, `ExecuteAsync` or `ScalarAsync`, with `NpgsqlDataSource.Create(postgres.ConnectionString)` 27 times; `ShouldDescribeAsync` is in `ColumnTests`, `TableMatchTests`, `WalkTests` and `ParameterTests`; `ColumnTests` and `WalkTests` query the server's major version though `PostgresContainer.Major` holds it; the skips "before 18 has no old and new in RETURNING" and "pinned for PostgreSQL 14 and 18 alone" are copied five and four times; a capture of a live describe takes three lines in ten tests; a session is opened four ways; `SessionTests` holds three near-identical exchanges that run a hook after one live call; `SessionTests` redeclares `Secrets.Marker`, and the `PGPASSWORD` skip is copied into both projects.
- **5. Classes over six hundred lines that mix subjects.**  [`PostgresSessionTests`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs), about 2,100 lines, covers about ten; [`NullabilityWalkTests`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs), about 1,700, holds the set operations with fixtures of their own; [`PostgresEventsTests`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs), about 1,080.
- **6. Uneven names.**  The container tests of `DescribeAsync` start `Describe_` and the unit tests `DescribeAsync_`, and so do `PostgresEventsTests`'; `PlanOutputTests` uses `Explain_` and `ExplainAsync_` is used by `ExplainTests`; the test classes of `tests/SqlSource.Tests.Postgres` are `sealed`, and most of the others in the repository are not.  [`CheckVerdictTests`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/CheckVerdictTests.cs) tests `NpgsqlCheck`.
- **7. Theory rows named by their JSON.**  Eleven `TheoryData` of plans, eight in `NullabilityWalkTests`, so a failing row is named by its plan.
- **8. Small duplicates in the container tests.**  `new DescribeCapture("postgres")` in `ParameterTests`, `ColumnTests` and `SessionTests`, and `FixtureCasesTests`' own `Engine`, beside `SidecarValues.Engine.Postgres`; `FixtureCasesTests` writes a recording's files by hand though `FixtureCaseFiles` writes them.

## Why it exists

Each sub-phase of phase 3 wrote its tests beside the last's, and the container tests and the unit tests were written by different sub-phases from different helpers.

## Impact

Low.  A change to a nullability source or to how the container tests reach the server reaches several classes; 5 and 7 make a failure harder to place.

## Proposed fix

The design is `docs/superpowers/specs/2026-10-16-td-0054-duplicated-postgresql-test-patterns-design.md`, whose pull requests 2 and 3 are what remains.

- **2.** The five container classes use `NullabilityAnswers` and its `Of`.
- **4.** `ExecuteAsync`, `ScalarAsync`, `SkipBelow` and `SkipUnlessRecordedMajor` on `PostgresContainer`; `ShouldDescribeAsync`, a typed open and a captured describe on `Describer/Sessions.cs`, whose `OpenAsync` takes a database for `RealisticDescribe`; `Recording.IsAsked`; one `AfterLiveExchange`; `Secrets.Marker` and a shared `PGPASSWORD` skip in `SqlSource.Tool.Testing`.
- **5.** Split `PostgresSessionTests` and `PostgresEventsTests` by subject, and move the set operations to `NullabilityWalkSetOperationTests`.
- **6.** A test's prefix names what it calls, the C# method in full or the command; every test class is `sealed`; both recorded in `tests/AGENTS.md`.  `CheckVerdictTests` becomes `NpgsqlCheckTests`.
- **7.** Labelled rows, as `WalkMatrixTests` keys its cases.
- **8.** `SidecarValues.Engine.Postgres` and `FixtureCaseFiles` in the container tests.

## Trigger

A change to a nullability source or to the container tests' access to the server that has to touch more than three test classes; or the first test of a second engine's describer, which should start from shared helpers.
