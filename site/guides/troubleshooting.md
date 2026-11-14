# Troubleshooting: it built, but something is wrong

Use this page when the build succeeds but something is wrong: an exception at run time, a wrong result, or nothing happens.

Some problems are listed in the known issues of [settings](../settings.md#known-issues), [the generator](../generator.md#known-issues), [the tool](../tool.md#known-issues), [PostgreSQL](../postgresql.md#known-issues) and [SQL Server](../sqlserver.md#known-issues).

<a id="at-run-time"></a>

## An exception at run time

<a id="wrong-model"></a>

## A model that looks wrong

- **A query parameter is missing from the input model.**  Under the `mssql` dialect, a parameter after a `DECLARE` with no `;` can be taken for a variable.  See [a parameter taken for a variable](../reference/parameters.md#limit-parameter-taken-for-a-variable).
- **The input model has a property for a name that is not a query parameter**, such as `@x` written for an absolute value on PostgreSQL.  SqlSource finds query parameters by how the SQL is written, not by what the database does with it.  A MySQL or MariaDB user variable, and the parameter of a procedure that the SQL creates, are read as query parameters too.  With the output `sql` that they need, they change no generated code.  See [names that are read as parameters](../reference/parameters.md#names-read-as-parameters).

<a id="wrong-result"></a>

## A method returns something you did not expect

- **Part of your SQL is missing from the generated SQL.**  SqlSource can read a comment or a string differently from your database, and then removes SQL that your database runs.  See [what is still read differently](../reference/dialects.md#still-read-differently).

<a id="not-described"></a>

## A query is not described, or a file is not found

- **Two projects include one `.sql` file, and entries are missing from its sidecar.**  A run of the tool on one project can remove the entries that the other project needs.  See [a file in two projects](../reference/sidecars.md#limit-file-in-two-projects).

<a id="watch"></a>

## The watch does nothing

<a id="secrets"></a>

## A secret in the output
