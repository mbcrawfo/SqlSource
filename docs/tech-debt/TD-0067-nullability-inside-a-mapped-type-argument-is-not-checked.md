# TD-0067 - The nullability inside a mapped type argument is not checked against a constraint

## Problem

[`MappedTypeResolver`](../../src/SqlSource/Generation/Types/MappedTypes/MappedTypeResolver.cs), through [`GenericConstraints`](../../src/SqlSource/Generation/Types/MappedTypes/GenericConstraints.cs), checks each type argument of a C# type that the type override names against the constraints of its type parameter, and refuses an argument written with `?` where a constraint forbids null.  It does not look at the nullability inside an argument.  A mapping such as `MyApp.Names<System.Collections.Generic.List<string?>>`, where `Names<T>` requires `where T : IEnumerable<string>`, is `Found`, and the generated model, which is `#nullable enable`, gets the compiler's warning CS8631 for its property.

## Why it exists

The conversion that a constraint type asks for is checked with `Compilation.ClassifyConversion`, which ignores nullability, and Roslyn 4.8.0 has no public call that says whether a conversion keeps the nullability of every nested type argument.  Checking it means walking the argument and the constraint type side by side, with the variance of each type parameter on the way, as the compiler does.

## Impact

Low.  The model compiles, with a warning for its property, which fails the build only where warnings are errors.  A user who sees it can map the type with arguments whose nullability the constraint takes, as the known issue of [`docs/manual/postgresql.md`](../manual/postgresql.md#known-issues) says.

## Proposed fix

In `GenericConstraints.MeetsConstraints`, after the conversion is found, walk the argument and the substituted constraint type together: for each pair of nested type arguments, an argument that can be null where the other cannot breaks the constraint, unless the variance of the type parameter allows it (an `out` parameter takes a non-null argument where the constraint's can be null, an `in` parameter the reverse).  A failure is `MappedTypeStatus.NotAPropertyType`, with cases in `Resolve_GenericNameWhoseArgumentsMeetTheConstraints_IsFound` and `Resolve_GenericNameWhoseArgumentsBreakAConstraint_IsNotAPropertyType` of `tests/SqlSource.Tests/Generation/Types/MappedTypes/MappedTypeResolverTests.cs`, which compares each with the compiler.

## Trigger

A user's report of a warning CS8631 in a generated model.
