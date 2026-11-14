# Troubleshooting: it built, but something is wrong

Use this page when the build succeeds but something is wrong: an exception at run time, a wrong result, or nothing happens.  It also lists a build error that is not about your SQL.

Some problems are listed in the known issues of [settings](../settings.md#known-issues) and [the generator](../generator.md#known-issues).

<a id="at-run-time"></a>

## An exception at run time

- **`InvalidCastException` for a NodaTime, PostGIS, enum or composite type, on PostgreSQL.**  A repository made from a connection string makes connections that have none of the plugins and mappings of your data source.  See [a repository made from a connection string](../reference/databases/postgresql/query-methods.md#limit-connection-string-repository).
- **Null in a column whose property cannot be null, on SQL Server.**  With `ANSI_WARNINGS` and `ARITHABORT` both off, SQL Server returns null where an overflow or a division by zero is otherwise an error.  See [`ANSI_WARNINGS` off](../reference/databases/sqlserver/nullability.md#limit-ansi-warnings-off).
- **A `SqlException`, and the transaction is rolled back, on SQL Server.**  An error before the rows can hide a later error of the batch that rolls the transaction back.  See [a hidden rollback](../reference/databases/sqlserver/query-methods.md#limit-hidden-rollback).
- **`SqlException` 130 for an empty or null list, on SQL Server.**  A list after a `NOT` inside an aggregate of your own fails when it is empty or null.  See [a list in an aggregate of your own](../reference/databases/sqlserver/query-methods.md#limit-list-in-an-aggregate).

<a id="wrong-model"></a>

## A model that looks wrong

- **A query parameter is missing from the input model.**  Under the `mssql` dialect, a parameter after a `DECLARE` with no `;` can be taken for a variable.  See [a parameter taken for a variable](../reference/parameters.md#limit-parameter-taken-for-a-variable).
- **The input model has a property for a name that is not a query parameter**, such as `@x` written for an absolute value on PostgreSQL.  SqlSource finds query parameters by how the SQL is written, not by what the database does with it.  A MySQL or MariaDB user variable, and the parameter of a procedure that the SQL creates, are read as query parameters too.  Their queries need the output `sql`, under which these names change no generated code.  See [names that are read as parameters](../reference/parameters.md#names-read-as-parameters).
- **A property can be null though its column is never null, on PostgreSQL.**  SqlSource treats many columns as possibly null when it cannot show that they never are.  See [columns treated as possibly null](../reference/databases/postgresql/nullability.md#limit-columns-treated-as-nullable).
- **A property can be null though its column is never null, on SQL Server.**  SQL Server treats a column as possibly null when it cannot show that it never is.  See [columns treated as possibly null](../reference/databases/sqlserver/nullability.md#limit-columns-treated-as-nullable).
- **SQLSRC267 in a project that uses EF Core 8 or 9.**  EF Core's SQL Server provider brings Microsoft.Data.SqlClient 5.1, which is too old.  See [EF Core and Microsoft.Data.SqlClient](../reference/databases/sqlserver/types.md#limit-ef-core-sqlclient).
- **Only the first result set of a query has a model, on SQL Server.**  A query that returns several result sets gets a model of the first alone, and nothing is reported of the others.  See [several result sets](../reference/databases/sqlserver/index.md#limit-several-result-sets).

<a id="wrong-result"></a>

## A method returns something you did not expect

- **Part of your SQL is missing from the generated SQL.**  SqlSource can read a comment or a string differently from your database, and then removes SQL that your database runs.  See [what is still read differently](../reference/dialects.md#still-read-differently).
- **An empty list after `IN` matches the rows whose value is null, on SQL Server.**  A connection with `ANSI_NULLS` off takes `= NULL` as true for a null.  See [`ANSI_NULLS` off](../reference/databases/sqlserver/query-methods.md#limit-ansi-nulls-off).

<a id="not-described"></a>

## A query is not described, or a file is not found

- **Two projects include one `.sql` file, and entries are missing from its sidecar.**  A run of the tool on one project can remove the entries that the other project needs.  See [a file in two projects](../reference/sidecars.md#limit-file-in-two-projects).
- **SQLSRC224 with `Npgsql cannot read the database's types`, on PostgreSQL.**  An operator `=` of your own on `oid` or `"char"`, in a schema before `pg_catalog` on the search path, stops Npgsql from reading the types.  See [an operator on `oid` or `"char"`](../reference/databases/postgresql/index.md#limit-operator-on-oid).
- **Describing is slow on a table with thousands of partitions, on PostgreSQL.**  The tool looks at every partition of a partitioned table.  See [many partitions](../reference/databases/postgresql/index.md#limit-many-partitions).
- **A function marked `IMMUTABLE` runs while a query is described, on PostgreSQL.**  PostgreSQL can compute a call of such a function with constant arguments in advance.  See [what describing does](../reference/databases/postgresql/index.md#what-describing-does).
- **SQLSRC228 for a `-- param:` type name, on SQL Server.**  SqlSource and SQL Server can read a character that Unicode added or reclassified after version 3.2 in different ways.  See [a type name with such a character](../reference/databases/sqlserver/index.md#limit-type-name-unicode).
- **SQLSRC231 for a parameter name with a character that Unicode added or reclassified after version 3.2, on SQL Server.**  SqlSource and SQL Server can read a character that Unicode added or reclassified after version 3.2 in different ways.  See [a parameter name with such a character](../reference/databases/sqlserver/index.md#limit-parameter-name-unicode).
- **A query that writes a query parameter's name in two ways fails when it is described, on SQL Server.**  SqlSource compares names ignoring case alone, and SQL Server by its collation.  See [a name written in two ways](../reference/databases/sqlserver/index.md#limit-parameter-two-spellings).
- **SQLSRC207 or SQLSRC212 for a project or `.sql` path that exists.**  A full path through a symbolic link does not match the real path that the tool compares it with.  See [a path through a symbolic link](../reference/cli.md#limit-path-through-a-symbolic-link).
- **SQLSRC241 for a query that only another target framework includes.**  The tool reads a project with several target frameworks for its first framework alone.  See [several target frameworks](../reference/describing.md#limit-several-target-frameworks).

<a id="watch"></a>

## The watch does nothing

- **Saving a file starts no pass of `describe --watch`.**  The saves of editors other than Rider on macOS, and saves on Windows, have not been checked.  See [saves that are not seen](../reference/describing.md#limit-saves-not-seen).
- **In Visual Studio or VS Code, the models do not change after the watch writes a sidecar.**  What these editors show after a pass has not been checked.  See [editors that have not been checked](../reference/describing.md#limit-editors-not-checked).
- **After a folder is deleted and made again, saves in it start no pass.**  The watch does not see a few changes to folders far from your project.  See [folder changes that are not seen](../reference/describing.md#limit-folder-changes-not-seen).

<a id="secrets"></a>

## A secret in the output

- **A short password shows in the output or a log.**  The tool does not hide a password shorter than four characters, or one that is the name of its logical database or dialect.  See [a short password](../reference/connections.md#limit-short-password).
