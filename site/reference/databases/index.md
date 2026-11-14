# Supported databases and versions

<a id="database-support"></a>

## Database support

| Database | Dialect | Can be described | Page |
|----|----|----|----|
| PostgreSQL | `postgres` | Yes | [PostgreSQL](../../postgresql.md) |
| SQL Server, Azure SQL | `mssql` | Yes | [SQL Server](../../sqlserver.md) |
| MySQL | `mysql` | No | None |
| MariaDB | `mariadb` | No | None |
| SQLite | `sqlite` | No | None |
| Oracle | `oracle` | No | None |
| CockroachDB | `cockroachdb` | No | None |
| Any other database | `ansi` | No | None |

Every dialect is read, and the queries of every `.sql` file whose output is `sql` or `models` get their constants and token methods whatever its database.  *Described* is something more: the `sqlsource` tool can ask the database about a query's parameters and columns and write the answer to a sidecar.  The tool decides by the dialect of the file, and asks only the databases marked Yes.
