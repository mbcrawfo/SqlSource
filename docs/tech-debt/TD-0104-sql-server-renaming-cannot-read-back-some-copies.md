# TD-0104 - SQL Server's renaming cannot read back some copies of a reused parameter

## Problem

[`ParameterCopies`](../../src/SqlSource.Tool/Describing/SqlServer/Statements/ParameterCopies.cs) names a copy of a reused parameter as the parameter's name, a separator of one or more `_` and the use's number, and cuts the parameter's part of a copy that would be longer than 127 characters.  [`SqlServerRenaming.CopyOf`](../../src/SqlSource.Tool/Bundles/Renaming/SqlServer/SqlServerRenaming.cs) reads a copy back as the shortest parameter of the query before a separator and digits.  Two copies are not read back right:

- A copy that was cut has no parameter before its separator, so `CopyOf` gives null: the copy of a parameter of 127 `a`s is 125 `a`s and `_1`.  `CopyOf_ACopyCutToFit_IsNull` of [`SqlServerRenamingTests`](../../tests/SqlSource.Tool.Tests/Bundles/Renaming/SqlServer/SqlServerRenamingTests.cs) holds it.
- A query that reuses both `@x` and `@x_` has the copies `x_1` and `x__1`, and `x__1`, a copy of `@x_`, is read back as a copy of `x`, the shortest parameter before a separator.  `CopyOf_ACopyOfTwoParametersOfTheQuery_IsTheShortest` holds it.

Either way the renamed request names a copy that the renamed capture does not answer, so the proof refuses the bundle as `RequestSql`.

## Why it exists

`CopyOf` is given a copy and the query's parameters, and no list of the copies that `ParameterCopies` made, which the capture does not record.  Without it a cut copy cannot be told from a name of the user's, and a separator from a parameter's own trailing `_`.  Both cases are rare, a parameter of more than 120 characters or two reused parameters whose names differ by a trailing `_`, and the proof refuses them, so nothing wrong is posted.

## Impact

Low.  Such a bundle cannot be renamed, and must be sent raw and privately, as with [TD-0102](TD-0102-sql-server-renaming-folds-ignoring-case.md).  No user sees it yet: no command renames a bundle until `diagnose`.

## Proposed fix

Record each copy's parameter in the capture, the `ParameterOfCopy` map of `ParameterCopies`' `CopiedSql`, beside the request that holds the copies, and have `CopyOf` read a copy through that map rather than by its spelling.  This changes the capture's schema and the describer's recording ([the copies and the probe](../architecture/sqlserver-describer.md#the-copies-and-the-probe)).  Then change the two tests above to expect the parameter, update [the copies of a reused parameter](../architecture/bundles.md#the-copies-of-a-reused-parameter) and the folder's `AGENTS.md`, and resolve this item.

## Trigger

A user's report of such a refusal, or the next change to the capture's schema for SQL Server.
