# TD-0067: Nullability in mapped type arguments Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A mapped C# type whose type argument holds a `?` that a constraint of its type parameter does not take, such as `MyApp.Names<System.Collections.Generic.List<string?>>` where `Names<T>` requires `where T : IEnumerable<string>`, is `NotAPropertyType` (`SQLSRC252`) instead of `Found` with a compiler warning CS8631 in its model.

**Architecture:** `GenericConstraints.MeetsConstraints`, after `Converts` finds the conversion a constraint type asks for, walks the argument and the substituted constraint type together, as the compiler's nullable conversion does.  For a constraint type that is generic, it finds among the argument, its base types and its interfaces those of the constraint's definition, and compares their type arguments level by level (containing types' too): an invariant argument, or one that is a value type, must have the same nullability all through; a covariant (`out`) argument must convert to the constraint's with nullability, and a contravariant (`in`) argument the reverse.  An array converts to an array by its element type, covariantly for a reference element.  An oblivious annotation on either side matches anything.  `Substitute` now merges the annotation of a type parameter's use, `T?`, with its argument's, as the compiler's substitution does, so a constraint `IEnumerable<T?>` with `T` = `string` is `IEnumerable<string?>`.

**Tech Stack:** .NET, Roslyn 4.14 public API, xUnit v3, Shouldly.

**Spec:** `docs/tech-debt/TD-0067-nullability-inside-a-mapped-type-argument-is-not-checked.md`

## Global Constraints

- Every new case is a row of `Resolve_GenericNameWhoseArgumentsMeetTheConstraints_IsFound` or `Resolve_GenericNameWhoseArgumentsBreakAConstraint_IsNotAPropertyType`, which ask the compiler first, so each row is checked against CS8631.
- A false refusal is worse than the status quo (a warning), so where the walk cannot tell, such as finding no base type or interface of the constraint's definition, it accepts.

## Where the code differs from the spec

- The spec does not mention these, and the walk needs each to agree with the compiler:
  - The `T?` substitution; without it the walk would refuse a type the compiler takes.
  - `Substitute` kept no annotation when it rebuilt a type (a non-generic type from its definition, an array by `CreateArrayTypeSymbol`, whose element annotation is a separate argument); it now keeps them.
  - `MappedTypeResolver.AsWritten` gave a reference type written without a `?` an oblivious annotation, which matches anything; it is now not annotated, as the `#nullable enable` model reads it.  It also read only the first rank's `?`; a `?` before a later rank now closes the array written before it, as C# reads `string[]?[]`.
  - An array converts to its generic interfaces by its element, covariantly, as to an array: `string[]` meets `IList<string?>` though `IList<T>` is invariant.
  - A containing interface's own variance counts (`IOuter<out T>.IInner<U>`), and a type parameter used where nullable annotations are off keeps its argument's annotation, as the compiler does.
  - The compiler takes the `T` of `System.IEquatable<T>` as contravariant for nullability, so `string`, an `IEquatable<string?>`, meets `where T : IEquatable<T>`.  The walk does the same, and a row pins it.

## Review Focus

- Variance: `out` takes a non-null argument where the constraint's can be null, `in` the reverse; a value type argument in a variant position needs identity.
- Containing types' arguments (`Holder<string?>.Item`) are invariant.
- Delegates (`Func<string?>`, `Action<string>`), arrays (`string?[]`), a struct through boxing, and a constraint written where nullable annotations are off.

---

### Task 1: The walk

**Files:**
- Modify: `src/SqlSource/Generation/Types/MappedTypes/GenericConstraints.cs`
- Test: `tests/SqlSource.Tests/Generation/Types/MappedTypes/MappedTypeResolverTests.cs` (new declarations in `ProjectSource`, rows of the two theories)

- [ ] Add the declarations and rows; run the theories and see the break rows fail at the resolver's status.
- [ ] Add the walk and the annotation merge; run the theories and the resolver tests.

### Task 2: Documents

- [ ] Delete the TD-0067 file and its row of `docs/tech-debt/README.md`.
- [ ] Remove the limit `limit-nullable-type-argument` from `site/reference/type-mapping.md`, its row of `site/guides/troubleshooting.md` and its mention in `site/reference/models.md`; say in `site/diagnostics.md`'s `SQLSRC252` that a `?` inside a type argument breaks a constraint that does not take it.
- [ ] Update `GenericConstraints`' remarks and `docs/architecture/generator.md`.
