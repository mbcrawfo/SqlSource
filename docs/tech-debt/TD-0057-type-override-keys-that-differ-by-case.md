# TD-0057 - The type override cannot tell apart keys that differ only by case

## Problem

The keys of the type override compare ignoring case: [`TypeOverrideMap`](../../src/SqlSource/Settings/TypeOverrideMap.cs) sorts, merges and finds its entries with `StringComparer.OrdinalIgnoreCase`, and [`TypeKeys`](../../src/SqlSource/Settings/TypeKeys.cs) reads a known name ignoring case.  PostgreSQL keeps apart two types of one schema whose names differ only by case, one created with a quoted name, `public."Status"`, and one without, `public.status`, and the sidecar records each by its `InternalName`, `Status` and `status`.  An entry `public.Status=MyApp.Status` therefore maps both.  Two entries for them are one key: in an MSBuild value the last wins, and in one marker scope two different C# types are `SQLSRC112`.

## Why it exists

The [phase 5.2 spec](../superpowers/specs/2026-10-18-query-generation-phase-5-2-type-map-design.md) decided that keys compare ignoring case, on every engine.  The reasons for keeping it: PostgreSQL folds an unquoted name to lower case and SQL Server compares names ignoring case by default, so one rule serves both engines' keys; two types of one schema whose names differ only by case are rare; and a key compared by case would make an entry written `public.User_Status` miss the type `public.user_status`, a mistake far more likely than such a pair.

## Impact

Low.  A project with such a pair cannot map them to two C# types, and an entry meant for one gives its C# type to the other too.  That has two effects a user can see today:

- A mapping's diagnostics, `SQLSRC250` to `SQLSRC252`, can be reported at the queries of the other type.
- An entry meant for one type silences the other type's `SQLSRC246` or `SQLSRC249`: a composite `public.status`, which has no C# type, is typed by `public.Status=MyApp.Status` instead of reported.

From sub-phase 5.3, the other type's models get the wrong C# type.

## Proposed fix

Keep the keys sorted ignoring case, and in `TypeOverrideMap.Find` prefer an entry whose key equals the type's key ordinally over one that equals it ignoring case, for a key with a schema; give the grammar a quoted name, `public."Status"`, that matches ordinally alone.  `TypeOverrideMap.Over` and `TypeOverrideGrammar.ReadList` then keep two entries whose keys differ by case as two, and `SqlMarkerScope` reports `SQLSRC112` only for keys equal ordinally.  Tests in `tests/SqlSource.Tests/Settings/TypeOverrideMapTests.cs` and `tests/SqlSource.Tests/Generator/TypeOverrideLevelTests.cs`.

## Trigger

A user's report of two types whose names differ only by case, or phase 6, if SQL Server's case-sensitive collations need the same.
