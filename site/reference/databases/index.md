# Supported databases and versions

<a id="supported-versions"></a>

## Supported versions

| Database | Server versions supported | Server versions tested | Driver package | Driver versions supported | Driver versions tested | `dialect` | Pages |
|----|----|----|----|----|----|----|----|
| PostgreSQL | 14 or later | 14, 15, 16, 17, 18 | `Npgsql` | 8 or later | 8, 10 | `postgres` | [PostgreSQL](postgresql/index.md) |
| SQL Server | 2017 or later | 2017, 2019, 2022, 2025 | `Microsoft.Data.SqlClient` | 6.1 or later | 6.1, 7 | `mssql` | [SQL Server](sqlserver/index.md) |

The tool describes the queries of these two databases, and of Azure SQL, and of no other.  [Database support](#database-support) lists the other databases.

- **A server older than its row supports is [SQLSRC235](../../diagnostics.md#sqlsrc235).**  The tool reports it.
- **Azure SQL is accepted, and not tested.**  See [Azure SQL](#azure-sql).
- **A query method needs the driver package of its database.**  The project must reference the package, or the query is [SQLSRC273](../../diagnostics.md#sqlsrc273).  See [which queries get a method](../query-methods.md#which-methods).
- **A driver older than its row supports is an error.**  It is [SQLSRC247](../../diagnostics.md#sqlsrc247) for Npgsql, and [SQLSRC267](../../diagnostics.md#sqlsrc267) for Microsoft.Data.SqlClient.
- **What each version of a driver changes**, and what a newer driver gets, is on [which version of Npgsql](postgresql/types.md#which-npgsql) and [which version of Microsoft.Data.SqlClient](sqlserver/types.md#which-sqlclient).
- **A SQL Server project that uses EF Core can get an older Microsoft.Data.SqlClient from it.**  Reference a supported version from the project itself.  See [SQLSRC267 in a project that uses EF Core](sqlserver/types.md#limit-ef-core-sqlclient).
- **The tool has requirements of its own**, such as the .NET SDK.  See [the tool's requirements](../cli.md#requirements).

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

SqlSource reads the queries of every dialect.  Whatever the database, the queries of every `.sql` file whose output is `models` or `sql` get their constants and token methods.  *Described* is more: the `sqlsource` tool asks the database about the parameters and columns of a query, and writes the answer to a sidecar.  The tool goes by the dialect of the file, and asks only the databases marked Yes.  Azure SQL can be described, and is accepted, and not tested: see [Azure SQL](#azure-sql).

<a id="azure-sql"></a>

## Azure SQL

Azure SQL Database and Azure SQL Managed Instance are accepted, and not tested.  The tool accepts each of them whatever version it reports, and never reports [SQLSRC235](../../diagnostics.md#sqlsrc235) for either of them.
