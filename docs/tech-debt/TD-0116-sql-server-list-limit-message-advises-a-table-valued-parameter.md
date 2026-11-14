# TD-0116: The SQL Server message of the list limit advises a table-valued parameter

## Problem

A SQL Server query method whose query would send more than 2,098 parameters throws `ArgumentException` before anything is sent.  Its message ends with the advice of [`ListWriter.LimitOf`](../../src/SqlSource/Generation/Emitting/QueryMethods/ListWriter.cs), which [`ListCountWriter`](../../src/SqlSource/Generation/Emitting/QueryMethods/ListCountWriter.cs) appends after the list's property name:

```console
System.ArgumentException: The query 'ListOrdersByIds' would send 2101 parameters; SQL Server accepts 2098.  Pass fewer values in 'Ids', or use a table-valued parameter. (Parameter 'parameters')
```

A query method cannot send a table-valued parameter.  The manual says so at [Lists on SQL Server](../../site/reference/databases/sqlserver/query-methods.md#list-parameters): such a query needs the output `sql` and the user's own code.  The way a query method can send that many values is one JSON array in an `nvarchar(max)` parameter, read with `OPENJSON`, which the same section and [the limit of parameters](../../site/reference/parameters.md#list-limit) give.  PostgreSQL's advice, `send them as an array with = ANY`, is one that a query method can follow.

## Why it exists

The message was written before the manual settled on `OPENJSON` as the way to send a large list on SQL Server.  It was found while the guide `site/guides/lists.md#large-and-empty-lists` was written, in phase 9 of the documentation overhaul, whose scope allows no change to code.

## Impact

Low.  A user who hits the limit on SQL Server is sent towards a table-valued parameter, then finds that a query method cannot send one, and must find `OPENJSON` in the manual.  The manual's sections on the limit give the right fix, and the guide quotes only the first part of the message.

## Proposed fix

Change the SQL Server advice in `ListWriter.LimitOf` to one that a query method can follow, for example `", or pass the values as JSON with OPENJSON"`.  Then update every place that pins the text:

- the expected messages of `tests/DatabaseTests/SqlServer/EndToEnd/ListQueryTests.cs` and `tests/DatabaseTests/SqlServer.SqlClient61/ListQueryTests.cs`;
- the SQL Server golden files of `tests/SqlSource.Tests/Generator/Golden/sqlserver/` (`lists.txt`, `lists-sync.txt`, `lists-no-padding.txt`), written again with `SQLSOURCE_UPDATE_GOLDEN=1` on `tests/SqlSource.Tests` alone;
- any sample of the message in `site/`, which quotes none of it at this writing but the guide's first part.

## Trigger

Any change to the messages of the list limit, or a user report of the advice.
