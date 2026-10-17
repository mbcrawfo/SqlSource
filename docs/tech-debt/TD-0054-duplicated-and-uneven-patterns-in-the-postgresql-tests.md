# TD-0054 - Duplicated and uneven patterns in the tests of the PostgreSQL describer

## Problem

A maintainability review of phase 3 on 2026-10-16 found eight of these in `tests/SqlSource.Tool.Tests/Describing/Postgres/`, `tests/SqlSource.Tests.Postgres/` and the helpers they share.  Items 1, 2, 3, 4 and 8 are resolved; the numbers of the rest are the review's.  No test is wrong; each makes a test or its failure harder to find.

- **5. Classes over six hundred lines that mix subjects.**  [`PostgresSessionTests`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresSessionTests.cs), about 2,100 lines, covers about ten; [`NullabilityWalkTests`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/Walk/NullabilityWalkTests.cs), about 1,700, holds the set operations with fixtures of their own; [`PostgresEventsTests`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/PostgresEventsTests.cs), about 1,080.
- **6. Uneven names.**  The container tests of `DescribeAsync` start `Describe_` and the unit tests `DescribeAsync_`, and so do `PostgresEventsTests`'; `PlanOutputTests` uses `Explain_` and `ExplainAsync_` is used by `ExplainTests`; the test classes of `tests/SqlSource.Tests.Postgres` are `sealed`, and most of the others in the repository are not.  [`CheckVerdictTests`](../../tests/SqlSource.Tool.Tests/Describing/Postgres/CheckVerdictTests.cs) tests `NpgsqlCheck`.
- **7. Theory rows named by their JSON.**  Eleven `TheoryData` of plans, eight in `NullabilityWalkTests`, so a failing row is named by its plan.

## Why it exists

Each sub-phase of phase 3 wrote its tests beside the last's, and the container tests and the unit tests were written by different sub-phases from different helpers.

## Impact

Low.  5 and 7 make a failure harder to place, and 6 makes a test harder to find by what it calls.

## Proposed fix

The design is `docs/superpowers/specs/2026-10-16-td-0054-duplicated-postgresql-test-patterns-design.md`, whose pull request 3 is what remains.

- **5.** Split `PostgresSessionTests` and `PostgresEventsTests` by subject, and move the set operations to `NullabilityWalkSetOperationTests`.
- **6.** A test's prefix names what it calls, the C# method in full or the command; every test class is `sealed`; both recorded in `tests/AGENTS.md`.  `CheckVerdictTests` becomes `NpgsqlCheckTests`.
- **7.** Labelled rows, as `WalkMatrixTests` keys its cases.

## Trigger

A change to one subject of `PostgresSessionTests`, `PostgresEventsTests` or `NullabilityWalkTests` that has to find its tests among the others; or the first test of a second engine's describer, which should start from classes of one subject each.
