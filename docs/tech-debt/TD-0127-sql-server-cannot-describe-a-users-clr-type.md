# TD-0127 - The SQL Server describer cannot describe a user's CLR type

## Problem

A query that returns a column of a CLR type of a user's own assembly (`CREATE TYPE dbo.Tag EXTERNAL NAME TagAsm.[Tag]`), or takes a parameter of one, ends the describe run with `SQLSRC200`.  SQL Server's describe gives such a type no `system_type_name`: `sys.dm_exec_describe_first_result_set` answers it `NULL`, with the type's `user_type_schema`, `user_type_name` and `assembly_qualified_type_name`, on SQL Server 2017, 2019, 2022 and 2025 alike.  [`SqlServerTypes.Build`](../../src/SqlSource.Tool/Describing/SqlServer/Types/SqlServerTypes.cs) throws an `InvalidOperationException` for a row with no type name, since it takes one to be the server's error, and the run reports the exception as `SQLSRC200`.

It was run through the describer on 2025, against a `SAFE` assembly of a `Format.Native` type: `SELECT id, tag FROM dbo.scratch_tags` and `SELECT id FROM dbo.scratch_tags WHERE tag = @t` each threw `A row of the server's types has no type name or lacks a facet, and the server typed it.`  The fixtures and the realistic corpus hold no user's CLR type, since creating an assembly needs `clr enabled` and, from 2017, `clr strict security` off or the assembly trusted.

## Why it exists

The describer was built and tested against `geometry`, `geography` and `hierarchyid`, which the server names in `system_type_name`, and against alias types, which have a system type.  A user's CLR type was found by the measurements of `td-0071-clr-setop`, which needed an assembly loaded on a server, and the owner chose to record it and fix it in a unit of its own.

## Impact

Medium.  A project with a query that reads or writes a CLR type of its own cannot be described at all: the run ends at that query with a message that calls the tool's failure a bug.  Nothing wrong is generated.  A CLR type of a user's own is rare, and the manual's [limit](../../site/reference/databases/sqlserver/types.md#limit-user-clr-type) says what a user can do.

## Proposed fix

Reproduce the failure through the tool first, with a test of `tests/DatabaseTests/SqlServer` that loads a small `SAFE` assembly of a CLR type, from bytes the test holds, under `clr enabled` and a trusted assembly (`sys.sp_add_trusted_assembly`), and removes it after.  Then build the type of a row with no `system_type_name` from its user type, `[schema].[name]` with its assembly-qualified name, in `SqlServerTypes.Build`, for a column, a parameter of `parameters` and a declared parameter, and give it the generator's answer for an unknown CLR type, `SQLSRC246`, which names the type override.  The nullability of such a column needs nothing new: `SqlServerTypes.MayBeClr` already takes a row with an assembly-qualified name, or with no type name, as a CLR type, and [a column of a CLR type](../architecture/sqlserver-describer.md#a-column-of-a-clr-type) widens it as it does `geometry`.

## Trigger

The unit `mssql-user-clr`, or a user who reports `SQLSRC200` for a query of a CLR type of their own.
