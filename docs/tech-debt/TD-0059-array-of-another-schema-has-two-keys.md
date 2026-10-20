# TD-0059 - An array of a type outside pg_catalog has two keys

## Problem

PostgreSQL names the array of a type with `_` before the type's name, so the array of `public.user_status` is `public._user_status`, and the sidecar records it with that `InternalName`.  [`TypeKeys.TryNormalize`](../../src/SqlSource/Settings/TypeKeys.cs) reads every `pg_catalog._name` as `name[]`, `pg_catalog._pg_class` as `pg_class[]`, and an unqualified `_name` of a type the map knows the same way, `_int4` as `int4[]`, so for a type of `pg_catalog` the two spellings are one key.  A qualified key of any other schema is kept as written, so `public._user_status` and `public.user_status[]` are two keys:

- `OverrideLookup.Find` in [`QueryTyper`](../../src/SqlSource/Generation/Types/QueryTyper.cs) takes the array's own key, `public._user_status`, before its element's with `[]`, `public.user_status[]`, in the merged map.  An entry for the first wins over one for the second whatever the levels, so a query's `public.user_status[]=default` or `public.user_status[]=X` does not replace a property's `public._user_status=Y`.
- [`TypeOverrideMap.Over`](../../src/SqlSource/Settings/TypeOverrideMap.cs) and `TypeOverrideGrammar.ReadList` keep the two as two entries, and `SqlMarkerScope` reports no `SQLSRC112` for two lines of one scope that give them two C# types.

## Why it exists

A type of another schema may itself be named with a leading `_`, `CREATE TYPE public._tag AS ...`, which is no array, and whose own key is `public._tag`.  Reading `public._tag` as `public.tag[]` would make that type's own key unreachable, and the key alone cannot tell the two apart: whether `public._tag` is an array is in the sidecar, not in the key.  For `pg_catalog`, where every name that starts with `_` is an array's, the rule is safe.  The manual tells the user to write such an array as `schema.name[]`.

## Impact

Low.  A user who writes `public._user_status` gets the mapping, and a user who mixes the spellings across levels gets the `_` entry wherever it is, with no error that says so.  A model then has the C# type of the entry the user meant to replace.

## Proposed fix

In `OverrideLookup.Find`, skip the own key of an array of any schema, so that an array is found by its element's key with `[]` alone, as an array of `pg_catalog` is today, and `public._tag` maps only a type of that name that is no array.  Then report, at a query with an array whose own key has an entry, an error that the entry maps nothing and names the key to write, `schema.name[]`, so that a user who wrote the catalog's name learns the other.  Tests in `tests/SqlSource.Tests/Generator/TypeOverrideLevelTests.cs`.

## Trigger

A user's report of an array entry that does not take effect.  The models of sub-phase 5.3, whose properties make the wrong type visible, have landed with phase 5, whose release is the first a user can use.
