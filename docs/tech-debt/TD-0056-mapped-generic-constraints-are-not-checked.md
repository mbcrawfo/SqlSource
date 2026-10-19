# TD-0056 - The constraints of a mapped generic type are not checked

## Problem

[`MappedTypeResolver`](../../src/SqlSource/Generation/Types/MappedTypeResolver.cs) resolves each type that the type override names, and each of its type arguments, without constructing the generic type, and never checks the arguments against the type parameters' constraints.  A mapping such as `int4=System.Collections.Generic.List<System.Nullable<string>>`, or `MyApp.Box<string>` where `Box<T>` requires `where T : struct`, is `Found`, and no `SQLSRC252` or other diagnostic says it is wrong.

## Why it exists

The resolution keeps no symbol and constructs nothing, which keeps the step cheap and its value a plain record.  Checking a constraint means constructing the type and walking each type parameter's `HasValueTypeConstraint`, `HasReferenceTypeConstraint`, `HasUnmanagedTypeConstraint`, `HasConstructorConstraint`, `HasNotNullConstraint` and `ConstraintTypes`, with the conversions between the argument and each constraint type that the compiler applies, which Roslyn does not expose as one public call.  A wrong argument is rare, and the compiler reports it anyway.

## Impact

Low.  A model's property of such a type is a compiler error (`CS0453`, `CS0452`, `CS0315` and their kind) in the generated code, rather than an `SQLSRC` error at the query that names the mapping and how to fix it.  The known issue of [`docs/manual/postgresql.md`](../manual/postgresql.md#known-issues) tells a user to check the type's constraints.

## Proposed fix

In `MappedTypeResolver.ResolveType`, for a part with arguments, construct the found definition with the resolved arguments (`INamedTypeSymbol.Construct`, keeping the symbols of the arguments that `ResolveType` finds) and check each type parameter's constraints against its argument: the value-type, reference-type, unmanaged and `new()` flags, and each constraint type by `Compilation.ClassifyConversion` being an identity, implicit reference or boxing conversion.  A failure is `MappedTypeStatus.NotAPropertyType`, with a test in `tests/SqlSource.Tests/Generation/Types/MappedTypeResolverTests.cs` for each kind of constraint.  The fix removes the known issue of `docs/manual/postgresql.md`.

## Trigger

A user's report of a compiler error in a generated model for such a mapping.  Sub-phase 5.3, which emits a model property of the mapped type and so makes the error visible, has landed with phase 5, whose release is the first a user can use.
