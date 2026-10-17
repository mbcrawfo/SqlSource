# SqlSource.Tests.Postgres

This project runs the PostgreSQL describer and the `describe` command against a real server, which Testcontainers starts in Docker.  It proves four things: each step of the describer, from its session to the walk, with what only a server answers; every fixture case of the describer, described live and compared with its `expected.json` by `FixtureCasesTests`, which `tools/record-fixtures.sh` also runs to record them; the whole `describe` command with the real describer, in process and as the built tool; and the realistic corpus, where each case is a query with a truth of which of its columns can be NULL, written by hand.

## Running

The project needs Docker running, and its first run pulls the image.  It starts one container for the run, with the database `fixtures` and the schema `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/schema.sql`, and a second database, `realistic`, with the schema and seed of `tests/SqlSource.Tests.Postgres/Realistic/` for the realistic corpus.

```bash
dotnet test --project tests/SqlSource.Tests.Postgres
```

`dotnet test --solution SqlSource.slnx`, and so `pre-commit-validation.sh`, runs it on the default image.

## The image

The image is `postgres:18.6`, named in one place, `PostgresContainer.DefaultImage`; the variable `SQLSOURCE_TESTS_POSTGRES_IMAGE` names another, and PostgreSQL 14 is the oldest that is supported, which CI runs as `postgres:14.24`:

```bash
SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test --project tests/SqlSource.Tests.Postgres
```

Every database image the tests run is pinned to a major and a minor, such as `postgres:18.6`, and never to a bare major such as `postgres:18`.  The recorded fixtures carry the version of the server they were recorded against, and a bare major moves to each new minor without a word, so the tests would run a server the recordings never saw.  This holds for the default image, for the image CI runs on the oldest supported version, and for the image of any engine added later.  `PostgresContainerTests` fails on an image whose tag is not a major and a minor, or whose server reports another version, and `FixtureVersionTests` fails on a fixture case recorded against any version but the default image's.

Moving to a new minor is a change of its own: bump the tag, record the fixtures and the realistic corpus again on the new image, as in the next two sections, and read the diff, all in the same pull request.

## The describer's fixtures

The describer's fixtures are in `tests/SqlSource.Tool.Tests/Fixtures/Describers/postgres/`: a folder for each case with its `query.sql`, the `capture.json` of describing it and the `expected.json` of its sidecar.  `tests/SqlSource.Tool.Tests` replays each from its capture with no database, and this project describes each against the container and compares the entry, so a describer that asks something else fails the first and one that answers something else fails the second.  After a change to what the describer asks or answers, or to `schema.sql`, record them again and read the diff before it is committed:

```bash
tools/record-fixtures.sh
```

To add a case, add its folder with a `query.sql` and run the script.  A recorded file is never edited by hand.

## The realistic corpus

`tests/SqlSource.Tests.Postgres/Realistic/` holds the corpus and what reads it: the cases in `Realistic/Cases/`, each a folder with its `query.sql`, the `case.json` that holds its truth and the expectations recorded of it, and the `schema.sql` and `seed.sql` of the `realistic` database.  `Realistic/Describer/` holds the describer's checks over it:

- `RealisticDescriptionTests` compares each description with the `expected-pg14.txt` and `expected-pg18.txt` recorded of it;
- `RealisticNullabilitySafetyTests` checks its nullability against the truth and against the oracle that executes the query over the seed;
- `RealisticNullabilityNoiseTests` holds the count of columns that are not null and that the describer answers as nullable or unknown to a budget for each recorded major, 14 and 18, in its `Budgets`.

The corpus is recorded by its own script, `tools/record-realistic.sh`, which writes the `expected-pg{major}.txt` of each case under `tests/SqlSource.Tests.Postgres/Realistic/Cases/` for the major version of the server it ran against, and never a `case.json`.  The expectations are those of PostgreSQL 18 and 14, so recording is two runs, the second on the older image; run it after a change to what the describer asks or answers, or to the schema, the seed or a case, and read the diff before it is committed:

```bash
tools/record-realistic.sh
SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 tools/record-realistic.sh
```

To add a case, add its folder with a `query.sql` and a `case.json`, which holds the truth, written from the SQL and `schema.sql` and never from what the describer answers, and run both.  When a column of the truth can be `NULL` and no row of `seed.sql` makes it so, add such a row to `seed.sql`, which never removes one; `exempt` is for a `NULL` no seed can show.  The noise budget is lowered in the change that lowers the noise, the test says to, and raised only in a change that adds cases.
