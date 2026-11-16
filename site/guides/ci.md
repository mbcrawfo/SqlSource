# Check the sidecars in CI

Make a build server or pipeline fail when a sidecar is missing or out of date, with GitHub Actions jobs for PostgreSQL and SQL Server.

**Use this when** your sidecars are in git, and you want CI to fail when one is out of date.  Use it too when your schema changes, since a build cannot see a sidecar that a schema change made wrong.

<a id="check-sidecars-in-ci"></a>

## Check the sidecars in CI

<a id="ci-postgresql"></a>

### PostgreSQL

In a PostgreSQL project, which sets `"dialect": "postgres"` in its `sqlsource.json`, add this GitHub Actions job:

```yaml
jobs:
  sidecars:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:18.6
        env:
          POSTGRES_DB: app
          POSTGRES_PASSWORD: ci
        ports:
          - 5432:5432
        options: --health-cmd pg_isready --health-interval 5s --health-timeout 5s --health-retries 10
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: dotnet tool restore
      - run: dotnet restore
      - run: ./scripts/migrate.sh # creates your schema; replace with your own step
      - run: dotnet sqlsource describe --check
        env:
          SQLSOURCE_CONNECTION: Host=localhost;Database=app;Username=postgres;Password=ci
```

**What you get.**  The job starts PostgreSQL and creates your schema.  Then `describe --check` describes every query of the run.  The job fails when a sidecar that is checked in does not match what the database says.

**The rules you need.**

- **A build in CI never describes, so this step is how the job describes.**  The build reads the sidecars that are checked in.  See [running in the build](../reference/msbuild.md#running-in-the-build) and [Check in CI](../reference/describing.md#check-in-ci).
- **`--check` needs a database with your schema, and a connection for every database of the run.**  Replace `./scripts/migrate.sh` with your own step that creates the schema.  See [Check in CI](../reference/describing.md#check-in-ci).
- **`SQLSOURCE_CONNECTION` gives the connection when the run has one database.**  A run with several databases sets a variable for each one.  See [connections](../reference/connections.md#connections).
- **A sidecar that is missing or out of date is [SQLSRC219](../diagnostics.md#sqlsrc219).**  The exit code of `--check` tells it from a check that could not be made.  See [Read the exit code](#exit-code) and [exit codes](../reference/cli.md#exit-codes).
- **Name the exact version of your server in the image, a PostgreSQL minor such as `postgres:18.6`, and move it when you move your database.**  A tag such as `-latest` moves without a word, and a new server can describe a query otherwise than your sidecars say.

<a id="ci-sql-server"></a>

### SQL Server

A SQL Server project sets `"dialect": "mssql"` in its `sqlsource.json`.  Its job is the job for PostgreSQL with two changes.  Replace the service with this one:

```yaml
    services:
      mssql:
        image: mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04
        env:
          ACCEPT_EULA: Y
          MSSQL_SA_PASSWORD: Ci-Passw0rd
        ports:
          - 1433:1433
        options: >-
          --health-cmd "/opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P Ci-Passw0rd -Q 'SELECT 1'"
          --health-interval 5s --health-timeout 5s --health-retries 20
```

Replace the `describe --check` step with this one, which changes the connection:

```yaml
      - run: dotnet sqlsource describe --check
        env:
          SQLSOURCE_CONNECTION: Server=localhost,1433;Database=app;User ID=sa;Password=Ci-Passw0rd;TrustServerCertificate=True
```

**What you get.**  The steps are the same as in the job for PostgreSQL.  The job starts SQL Server, and the step that creates your schema creates the database too.  The job fails when a sidecar that is checked in does not match what the database says.

**The rules you need.**

- **`TrustServerCertificate=True` is for the job's own server, whose certificate nothing trusts.**  See [connecting to SQL Server](../reference/databases/sqlserver/index.md#connecting).
- **Name the exact version of your server in the image, a SQL Server cumulative update, and move it when you move your database.**  The same rule of the job for [PostgreSQL](#ci-postgresql) gives the reason.
- The other rules of the job for [PostgreSQL](#ci-postgresql) hold too.

<a id="exit-code"></a>

## Read the exit code

If your pipeline must tell a forgotten sidecar from a check that could not run, read the exit code of `describe --check`.  This step takes the place of the `describe --check` step of your job.  Keep that step's `env`:

```yaml
      - run: |
          status=0
          dotnet sqlsource describe --check || status=$?
          if [ "$status" -eq 2 ]; then
            echo "::error::A sidecar is out of date. Run 'dotnet sqlsource describe' and commit the sidecars."
          elif [ "$status" -ne 0 ]; then
            echo "::error::The check could not be made. Read the errors above."
          fi
          exit "$status"
```

**What you get.**  The step fails in both cases, and GitHub Actions shows which one as an error of the run.  `|| status=$?` keeps the code, since GitHub Actions stops a step at the first command that fails.

**The rules you need.**

- `0` means that the run reported no error: each sidecar matches what the database says.  See [Exit codes](../reference/cli.md#exit-codes).
- `2` means that a sidecar is out of date, and nothing else went wrong.  Someone forgot to describe, or the schema changed.  Run `dotnet sqlsource describe`, or `dotnet sqlsource describe --force` after a schema change, and commit the sidecars.  See [Check in CI](../reference/describing.md#check-in-ci).
- `1` means that the check could not be made, such as when the database could not be opened.  Read the errors above the summary.  See [Check in CI](../reference/describing.md#check-in-ci).
- `--check` changes no file, so the job has nothing to commit.  See [Check in CI](../reference/describing.md#check-in-ci).

## If it does not work

- **The job fails with SQLSRC219.**  Run the command that the `help:` line of the error names, and commit the sidecars.  See [SQLSRC219](../diagnostics.md#sqlsrc219).
