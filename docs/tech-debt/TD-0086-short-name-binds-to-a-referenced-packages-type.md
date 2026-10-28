# TD-0086: a short name binds to a type that a package brings

## Problem

A short name in a column type, a type override or a `-- param:` marker, `-- column: account_id -> AccountId`, is the one type of that name that the project or any of its references has, as [`ShortNameIndex`](../../src/SqlSource/Generation/Types/MappedTypes/ShortNameIndex.cs) finds it.  The references include the assemblies that a package brings with it.  So when the project has no type of the name and such an assembly has a `public` one, the name binds to it with no diagnostic: the property gets that type, it has no converter, and only the driver's `InvalidCastException` when the query runs shows the mistake.  It was seen in the floor project [`DatabaseTests/SqlServer.SqlClient61`](../../tests/DatabaseTests/SqlServer.SqlClient61), when it linked a query of the end-to-end project that mapped a column to that project's own `AccountId`: the floor project has no `AccountId`, and Microsoft.Data.SqlClient 6.1 brings Microsoft.Identity.Client, whose `Microsoft.Identity.Client.AccountId` the generated model took.  The build passed.  The query was moved to `Notes.sql`, which the floor project does not link.

## Why it exists

The manual's rule, in [type overrides](../manual/settings.md#setting-type-overrides), is that a short name is the one type of that name in the project and its references, so that a library of ids can be named by short names; the rule draws no line between a reference the project chose and one a package brought.  The case was found by the streaming query methods' work on the floor drivers, which does not change the lookup.

## Impact

Low.  It needs a short name that the project lacks and that some referenced assembly has, which a typo, a type moved or deleted, or a linked file can give.  The manual records it as a known issue of the settings.  The cost is a failure at run time where a build error was expected, and it can appear or vanish when a package is updated and brings another assembly.

## Proposed fix

Report a warning when a short name binds to a type of an assembly that the project does not reference directly, or to a type that has no converter where one is needed: the first needs the direct references told apart, which the compilation does not give, so it would come from the package's MSBuild files, as the project's `PackageReference` items; the second needs only what `ConversionPlanner` already knows, a type that the engine's map never gives with no converter, but it would also warn of a type the driver was taught, an enum after Npgsql's `MapEnum<T>()` for one, so it needs a way to be turned off.  Either is a new diagnostic, with its section in `docs/manual/diagnostics.md` and the known issue removed from `docs/manual/settings.md`.

## Trigger

A user's report of a short name bound to an unexpected type, or the next change to the short-name lookup.
