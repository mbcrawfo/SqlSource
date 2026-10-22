# SqlSource.Tests.SqlServer

This project runs the tests of SQL Server against a real server, which Testcontainers starts in Docker.  It proves the container itself: that the image the run names is pinned to its update and runs the build that is recorded for it, and that the fixture schema applies.  `Describer/` holds the tests of the SQL Server describer that only a server can answer: opening a session and what each refusal of it reports (`SessionTests`), the types the server gives a query's parameters, its errors and the canaries that prove a text runs nothing (`ParameterTests`), and the describer's fixture cases, described against the server and held to the build they were recorded on (`FixtureCasesTests`, `FixtureVersionTests`).

## Running

The project needs Docker running, and its first run pulls the image.  It starts one container for the run, creates the database `fixtures` and applies `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/schema.sql` to it through `SqlServerScripts`, which splits the file at each line that holds `GO` alone, ignoring case and the white space around it, and runs each batch as one command of SqlClient.  SqlClient runs with `QUOTED_IDENTIFIER` on, where `sqlcmd` has it off and refuses the schema's persisted computed column.  A `GO` with a count is refused.

```bash
dotnet test --project tests/SqlSource.Tests.SqlServer
```

`dotnet test --solution SqlSource.slnx`, and so `pre-commit-validation.sh`, runs it on the default image.  SQL Server 2017 is the oldest that is supported, which CI runs as well:

```bash
SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test --project tests/SqlSource.Tests.SqlServer
```

A change to these tests, or to the SQL Server describer, is run on both images before it is pushed.

SQL Server's images are built for x64 alone.  On Apple silicon Docker runs them under emulation: the first run takes minutes while the image is pulled, and a start with the image already pulled takes seconds.

## The images

The default image is `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04`, named for the tests and the scripts in one place, `DefaultImages.SqlServer` in `tests/SqlSource.Testing/DefaultImages.cs`.  The variable `SQLSOURCE_TESTS_SQLSERVER_IMAGE` names another, and the oldest that is supported is `mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`, the floor, whose tag is also in `.github/workflows/build.yml`, `CONTRIBUTING.md` and this readme.

Every SQL Server image the tests run names its update: `<year>-CU<n>`, with `-GDR<n>` when the build has one, then `-ubuntu-<version>`.  It never names `-latest`, a bare year or an on-demand hotfix, which move to a new build without a word, so the tests would run a server the recordings never saw.  `SqlServerContainer.ImageBuilds` maps each of the two images to the build, `SERVERPROPERTY('ProductVersion')`, that its server reports, recorded when the tag was chosen.  `SqlServerContainerTests` fails on a tag of another form, on an image that is not in the map, and on a server whose build differs from the map's.

Moving a tag is a change of its own: change the tag and its row of `ImageBuilds`, with the build that the new image reports, in the same pull request.  Moving the default image also records again, on the new image, everything that was recorded on it, and the diff is read before it is committed.  Nothing is recorded on the floor: moving it changes its tag in the places above and runs the tests on it.

## The fixtures

The fixture schema is `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/schema.sql`: tables of `dbo` and of a schema `billing`, an identity, a default, a `rowversion`, a computed and a persisted computed column, an alias type and a table type, a view over an outer join, a table with an `AFTER` trigger, a system-versioned table whose period columns are hidden, the table `dbo.victim`, which a test that proves a text runs nothing looks for afterwards, and `dbo.all_types`, with one `NOT NULL` column of every type that SQL Server 2017 and 2025 share.  The describer's fixture cases are in the folders beside it.

### The describer's fixtures

The describer's fixtures are in `tests/SqlSource.Tool.Tests/Fixtures/Describers/mssql/`: a folder for each case with its `query.sql`, the `capture.json` of describing it and the `expected.json` of its sidecar, whose `toolVersion` is `fixture` so that a new version of the tool touches no case.  `tests/SqlSource.Tool.Tests` replays each from its capture with no database, and this project describes each against the container and compares the entry, so a describer that asks something else fails the first and one that answers something else fails the second.  Both the capture and the sidecar carry the build of the server they were recorded on, which `FixtureVersionTests` holds to the build of the default image in `ImageBuilds`; `FixtureCasesTests` compares an entry whatever the build, so CI describes the cases on the floor image as well, where the answers are the same.  After a change to what the describer asks or answers, or to `schema.sql`, record them again and read the diff before it is committed:

```bash
tools/record-fixtures.sh mssql
```

To add a case, add its folder with a `query.sql` and run the script.  A recorded file is never edited by hand.  Nothing is recorded on the floor image.
