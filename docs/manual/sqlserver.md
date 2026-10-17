# SQL Server

SqlSource reads SQL Server and Azure SQL files by the `mssql` [dialect](dialects.md) today: every query gets its constant or its token method, with T-SQL's comments, strings and `[bracketed]` names read as SQL Server reads them.  Describing SQL Server queries with the `sqlsource` tool comes in a later release.

```xml
<PropertyGroup>
    <SqlSourceDialect>mssql</SqlSourceDialect>
    <SqlSourceOutput>sql</SqlSourceOutput>
</PropertyGroup>
```

Until SQL Server queries can be described, a query of `mssql` whose [output](settings.md#setting-output) is `models` or `codegen`, the default, is [SQLSRC216](diagnostics.md#sqlsrc216) when you run the tool.  It is not [SQLSRC209](diagnostics.md#sqlsrc209), which is for a dialect the tool does not describe, every row marked No in [database support](dialects.md#database-support).  The build is not affected.  Set the output to `sql`, as above, and the tool leaves the queries out.  That is also the fix when SQLSRC209's message suggests the dialect `mssql`.

Under `mssql`, a name that a query declares with `DECLARE @name` is a local variable and not a [query parameter](concepts.md#query-parameter): see [variables declared with `DECLARE`](parameters-and-tokens.md#declared-variables).  The rest of what the dialect changes is on [the dialects page](dialects.md#what-a-dialect-changes).

> **Coming in a later release.**  The tool will describe SQL Server queries, and this page will say what that needs of your database.  See [what works today](README.md#what-works-today).
