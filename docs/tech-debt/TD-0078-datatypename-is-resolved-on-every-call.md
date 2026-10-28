# TD-0078: a parameter's `DataTypeName` is resolved on every call

## Problem

A generated PostgreSQL method binds each parameter as a new `NpgsqlParameter<T>` whose `DataTypeName` is the sidecar's `bindName`, as [`PostgresParameterBinder`](../../src/SqlSource/Generation/Emitting/PostgresParameterBinder.cs) writes it: a display name such as `integer`, `timestamp with time zone` or `public.email`.  Npgsql resolves that name on every new parameter, and the resolution allocates.  Measured on Npgsql 10.0.3, against PostgreSQL 18, by the harness of [`AllocationTests`](../../tests/DatabaseTests/Postgres/EndToEnd/AllocationTests.cs) (2,000 calls of `SELECT id, name FROM accounts WHERE id = @accountId` after a warm-up, by `GC.GetTotalAllocatedBytes(true)`), `DataTypeName = "integer"` costs about 113 to 118 bytes per parameter per call, and `"int4"` about the same, where `"pg_catalog.int4"` costs about 22 to 58.  A call of that query allocates about 1.7 to 2.1 KB in all.

## Why it exists

Sub-phase 8.3 binds every parameter by the type the tool described it as, one rule for every row of the type map, and `bindName` is the name the tool's check bound it by.  The sidecar holds the display name, and nothing gives the generator the canonical internal name.  [The spike of phase 8](../superpowers/specs/2026-10-24-query-generation-phase-8-spike.md#8-npgsqls-datatypename) measured an execution "with or without `DataTypeName`" at the same cost, apparently on one parameter reused, so the cost was found only by 8.3's allocation test.  That test compares a generated method with hand-written ADO.NET that makes the same calls, so it charges the cost to both sides and cannot catch it.

## Impact

About 115 bytes per parameter per call, and the CPU of the name's lookup, on every call of every generated PostgreSQL method with parameters: a few percent of a call's allocation for a query with one parameter, more for one with many.  No behaviour changes.

## Proposed fix

Bind by the canonical internal name, schema-qualified, as `pg_catalog.int4`, `pg_catalog.timestamptz`, `public.email`, `pg_catalog._int4` for `integer[]`, which Npgsql resolves with less work.  Either the tool writes that name (a new key of the parameter's type, or `bindName` changed, which changes the sidecar's format and needs its design, schema and examples updated), or the generator derives it from the type's internal name and schema that the sidecar already holds, keeping `bindName` for the tool's check.  Measure again with the same harness before and after, and check the name on Npgsql 8, 9 and 10, and for an array, an enum, a domain, a range and a multirange, as `MethodCompileTests` and the end-to-end `EveryTypeTests` do.  A cached parameter per method is not an option: a method may run concurrently.

## Trigger

A user's report of the methods' allocations, a benchmark of the generated methods, or a change to the sidecar's format for another reason.
