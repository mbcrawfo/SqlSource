# Supported databases and versions

<a id="supported-versions"></a>

## Supported versions

| Database | Server versions supported | Server versions tested | Driver package | Driver versions supported | Driver versions tested | `dialect` | Pages |
|----|----|----|----|----|----|----|----|
| PostgreSQL | 14 or later | 14, 15, 16, 17, 18 | `Npgsql` | 8 or later | 8, 10 | `postgres` | [PostgreSQL](postgresql/index.md) |
| SQL Server | 2017 or later | 2017, 2019, 2022, 2025 | `Microsoft.Data.SqlClient` | 6.1 or later | 6.1, 7 | `mssql` | [SQL Server](sqlserver/index.md) |

- **PostgreSQL 14 and later are supported**, and SqlSource is tested on every major from 14 to 18.  An older server is [SQLSRC235](../../diagnostics.md#sqlsrc235).
- **SQL Server 2017 and later are supported**, and SqlSource is tested on 2017, 2019, 2022 and 2025.  An older server is [SQLSRC235](../../diagnostics.md#sqlsrc235).

<a id="database-support"></a>

## Database support

| Database | Dialect | Can be described | Page |
|----|----|----|----|
| PostgreSQL | `postgres` | Yes | [PostgreSQL](postgresql/index.md) |
| SQL Server, Azure SQL | `mssql` | Yes | [SQL Server](sqlserver/index.md) |
| MySQL | `mysql` | No | None |
| MariaDB | `mariadb` | No | None |
| SQLite | `sqlite` | No | None |
| Oracle | `oracle` | No | None |
| CockroachDB | `cockroachdb` | No | None |
| Any other database | `ansi` | No | None |

Every dialect is read, and the queries of every `.sql` file whose output is `sql` or `models` get their constants and token methods whatever its database.  *Described* is something more: the `sqlsource` tool can ask the database about a query's parameters and columns and write the answer to a sidecar.  The tool decides by the dialect of the file, and asks only the databases marked Yes.

<a id="azure-sql"></a>

## Azure SQL

Azure SQL Database and Azure SQL Managed Instance are accepted whatever version they report, and are not tested.
