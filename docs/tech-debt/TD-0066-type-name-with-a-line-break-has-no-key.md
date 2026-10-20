# TD-0066 - A type whose schema or name holds a line break has no key

## Problem

A part of a type-override key can be quoted and can then hold any character, as [`TypeKeys.TryNormalize`](../../src/SqlSource/Settings/TypeKeys.cs) reads it.  But neither place that a key is written in can carry a line break:

- An MSBuild value goes through the package's trim in [`SqlSource.targets`](../../src/SqlSource/build/SqlSource.targets), which makes one space of each run of white space before the generator reads it, inside quotes too.  So a line break, a tab or two spaces in a row inside a quoted part reach [`TypeOverrideGrammar`](../../src/SqlSource/Settings/TypeOverrideGrammar.cs) as one space, and the key names another type.
- A `-- type-override:` marker is a line comment, so its value ends at the first line break.

A name with a tab or two spaces in a row can still be named by a marker.  A type whose schema or name holds a line break cannot be named anywhere.  When such a type has no C# type, a composite type for one, its `SQLSRC246` help, from `PostgresTypeMap.KeyOf` and [`QueryTyper`](../../src/SqlSource/Generation/Types/QueryTyper.cs), prints a key that holds the line break, which no entry can write.

## Why it exists

The trim makes the package's other values readable when they are written over several lines, and no value of the package had a meaning for white space before keys gained quotes.  A line comment cannot hold a line break by its nature.  A name with a line break is very rare in a schema, so the quoted form was delivered without an escape for one.

## Impact

Low.  Such a type cannot be given a C# type with the type override, and the help of its error suggests an entry that cannot be written.  A domain over such a type can still be mapped by its base type, and a composite's fields can be selected as columns of their own.

## Proposed fix

Give a quoted part an escape for a character that the trim or a line comment would change, such as PostgreSQL's Unicode form, `U&"a\000Ab"`, read by `TypeKeys.TryNormalize` to the same text as the plain quoted form.  Have `TypeKeys.WritePart` write a name that holds a line break, a tab or two spaces in a row in that form, so that the help of `SQLSRC246` gives a key that survives both the trim and a marker.  Tests in `tests/SqlSource.Tests/Settings/TypeKeysTests.cs` and `tests/SqlSource.Tests/Generator/QueryTypesTests.cs`.

## Trigger

A user's report of a type that has no key because its schema or name holds a line break.
