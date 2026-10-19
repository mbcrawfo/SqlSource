# TD-0058 - A type whose schema or name needs quotes cannot be named by the type override

## Problem

A key of the type override is read by [`TypeKeys.TryNormalize`](../../src/SqlSource/Settings/TypeKeys.cs), whose parts hold letters, digits, `_` and `$` alone, with no quotes.  The sidecar records a type's schema and `InternalName` as the catalog has them, so a type of a schema such as `"my schema"`, or a type named such as `"order-status"`, has a key, `my schema.order-status`, that no entry can write.  Such a type cannot be given a C# type.  When it has none of its own, a composite type for one, its `SQLSRC246` cannot be answered with the type override, and the help of the error, from [`QueryTyper`](../../src/SqlSource/Generation/Types/QueryTyper.cs), suggests an entry that is not valid.

## Why it exists

The [phase 5.2 spec](../superpowers/specs/2026-10-18-query-generation-phase-5-2-type-map-design.md)'s grammar gives a key's parts no quotes.  A name that needs quotes for more than its case is rare in a schema, and a quoted part in an MSBuild value, where white space separates entries, needs rules of its own for a space inside quotes.

## Impact

Low.  A project with such a type cannot map it, and the build's help for it is wrong.  A domain over a base type still gets its base type's C# type, which an entry for the base type can change; a composite type can be read by selecting its fields as columns.  `docs/manual/settings.md` has the known issue.

## Proposed fix

Give a key's parts a quoted form, `"my schema"."order-status"`, in `TypeKeys.TryNormalize`, matched by its text without the quotes and compared ordinally, which also answers TD-0057 for a quoted name; teach `TypeOverrideGrammar`'s split of an MSBuild value that white space inside quotes belongs to the entry, as it does inside `<>`; and have `QueryTyper`'s help quote a part that needs it.  Tests in `tests/SqlSource.Tests/Settings/TypeKeysTests.cs`, `tests/SqlSource.Tests/Settings/TypeOverrideGrammarTests.cs` and `tests/SqlSource.Tests/Generator/TypeOverrideLevelTests.cs`.

## Trigger

A user's report of a type that cannot be named, or the design of phase 6's keys for SQL Server, whose names can need brackets.
