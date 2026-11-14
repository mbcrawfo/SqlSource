# Check the sidecars in CI

<a id="check-sidecars-in-ci"></a>

## Check the sidecars in CI

<a id="ci-postgresql"></a>

### PostgreSQL

In a PostgreSQL project (`"dialect": "postgres"` in its `sqlsource.json`), this GitHub Actions job starts PostgreSQL, creates the schema, and fails when a sidecar that is checked in does not match:

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

<a id="ci-sql-server"></a>

### SQL Server

In a SQL Server project (`"dialect": "mssql"` in its `sqlsource.json`), the service and the connection change; the steps are the same, and the step that creates your schema creates the database too:

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

```yaml
      - run: dotnet sqlsource describe --check
        env:
          SQLSOURCE_CONNECTION: Server=localhost,1433;Database=app;User ID=sa;Password=Ci-Passw0rd;TrustServerCertificate=True
```

`TrustServerCertificate=True` is for the job's own server, whose certificate nothing trusts: see [connecting to SQL Server](../reference/databases/sqlserver/index.md#connecting).

Name the server's exact version in the image, a PostgreSQL minor or a SQL Server cumulative update, and move it when you move your database: a tag such as `-latest` moves to a new server without a word, and a new server can describe a query otherwise than the one your sidecars were written on.

Rules: [check in CI](../reference/describing.md#check-in-ci), [connections](../reference/connections.md#connections) for the variable, [exit codes](../reference/cli.md#exit-codes), [SQLSRC219](../diagnostics.md#sqlsrc219).

<a id="exit-code"></a>

## Read the exit code

If your pipeline must tell a forgotten sidecar from a check that could not run, read the exit code of `describe --check`.  This step takes the place of the `describe --check` step of the PostgreSQL job:

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
        env:
          SQLSOURCE_CONNECTION: Host=localhost;Database=app;Username=postgres;Password=ci
```

**What you get.**  The step fails in both cases, and GitHub Actions shows which one as an error of the run.  `|| status=$?` keeps the code, since GitHub Actions stops a step at the first command that fails.

**The rules you need.**

- `0` means that the run reported no error: each sidecar matches what the database says.  See [Exit codes](../reference/cli.md#exit-codes).
- `2` means that a sidecar is out of date, and nothing else went wrong.  Someone forgot to describe.  Run `dotnet sqlsource describe` and commit the sidecars.  See [Check in CI](../reference/describing.md#check-in-ci).
- `1` means that the check could not be made, such as when the database could not be opened.  Read the errors above the summary.  See [Check in CI](../reference/describing.md#check-in-ci).
- `--check` changes no file, so the job has nothing to commit.  See [Check in CI](../reference/describing.md#check-in-ci).
