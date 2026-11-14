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
