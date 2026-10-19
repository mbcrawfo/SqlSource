# TD-0062 - Dapper cannot read rows into a record model with an array column

## Problem

[`ModelEmitter`](../../src/SqlSource/Generation/Emitting/ModelEmitter.cs) writes a `record` or `sealed record` model, the default shape of an output model, as a positional record: its one constructor takes a parameter for each property, of the property's type.  Dapper 2.1 fills a type that has no constructor without parameters by matching a constructor's parameters to the reader's fields by name and by the exact type of each field.  Npgsql gives the type of an array column as `System.Array`, on Npgsql 8 and 10 alike, so a parameter of `int[]` or `int[]?` never matches it.  Dapper then finds no constructor and throws `InvalidOperationException`, whether the array can be null or not, and reads no row.

[`AllTypesQueryTests.QueryAsync_RecordWithAnArray_DapperCannotConstructIt`](../../tests/SqlSource.Tests.Postgres/EndToEnd/AllTypesQueryTests.cs) pins the failure.  The end-to-end query of every type, [`AllTypes.sql`](../../tests/SqlSource.Tests.Postgres/EndToEnd/Queries/AllTypes.sql), sets `-- output-model-type: class` for it.  The realistic corpus's [`array-and-string-agg`](../../tests/SqlSource.Tests.Postgres/Realistic/Cases/array-and-string-agg/query.sql), its one case with an array column, is read through Dapper too, so its class in [`RealisticQueries.cs`](../../tests/SqlSource.Tests.Postgres/Realistic/Generation/RealisticQueries.cs) sets `OutputModelType = GeneratorModelType.Class`.  A class model, and a record with a range, a multirange, `hstore`, a C# enum or a struct that a type handler reads, are filled.

## Why it exists

Sub-phase 5.4 found it with the first Dapper run over real rows.  The owner approved recording it, with the manual's rule that a model with an array column read through Dapper is a class, and no generator fix in that pull request.  The generated query methods of phase 8 call the constructor themselves and do not need Dapper.

## Impact

Low.  A user who reads rows with an array column into the default model through Dapper gets an error at the first query, not a wrong value, and the manual's recipe and known issue say to make the model a class.  The cost is that the default shape is wrong for one common case of the library that phase 5 is for, and each such user meets the error before the rule.

## Proposed fix

Change the generator so that a model with an array property can be read through Dapper.  Two ways, for the owner to choose:

- **A constructor Dapper can use.**  Give a record model with an array property a second constructor, without parameters.  Dapper takes such a constructor first when one exists and then sets the properties, which a positional record gives `init` accessors; check that Dapper writes them, and that the record's equality, `with` and nullability stay as they are.
- **A class where an array property exists.**  Emit a model with an array property as a class, the sealed or open kind that the setting asks for, and say so in the manual's Shape section.  A user's setting is then not what they get, which the manual and perhaps a diagnostic would have to say.

Either way, turn `QueryAsync_RecordWithAnArray_DapperCannotConstructIt` into a test that reads the record, take `-- output-model-type: class` out of `AllTypes.sql` and `OutputModelType = GeneratorModelType.Class` out of the class of `array-and-string-agg` if the default then works, and remove the rule from the recipe and the known issue from `docs/manual/generator.md`.

## Trigger

A user who reads such a model through Dapper and reports the error, or phase 8's methods, whose design decides how a model is constructed.
