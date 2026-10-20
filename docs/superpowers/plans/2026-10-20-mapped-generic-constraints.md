# Check Mapped Generic Type Constraints Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A type that the type override names, whose type arguments break the constraints of its type parameters, is `MappedTypeStatus.NotAPropertyType` (`SQLSRC252` at the query) rather than a compiler error in the generated model.

**Architecture:** `MappedTypeResolver.ResolveType` keeps, inside the step only, the symbol of each name it resolves, so that a part with arguments can be constructed (`INamedTypeSymbol.Construct`, through `GetTypeMembers` of a constructed containing type for a nested one) and each type parameter checked against its argument.  Constraint types are substituted with the arguments before the conversion check.  `MappedType`, the step's output, does not change.

**Tech Stack:** C# (`netstandard2.0`), Roslyn 4.8.0 public API, xunit v3 and Shouldly.

**Spec:** [`docs/tech-debt/TD-0056-mapped-generic-constraints-are-not-checked.md`](../../tech-debt/TD-0056-mapped-generic-constraints-are-not-checked.md) (Problem and Proposed fix), and the resolver section of [`docs/superpowers/specs/2026-10-18-query-generation-phase-5-2-type-map-design.md`](../specs/2026-10-18-query-generation-phase-5-2-type-map-design.md).

## Global Constraints

- Roslyn API floor 4.8.0 (`src/SqlSource/AGENTS.md`): nothing newer, no `AllowsRefLikeType`.
- No symbol leaves `MappedTypeResolver`; `MappedType` stays a value-equal record of strings, enums, bools and `EquatableArray<string>`.
- No collection expression targets `ImmutableArray<T>`.
- A failure is `MappedTypeStatus.NotAPropertyType`; of two failures in one name the first in the enum's order wins (`First`), so a missing, ambiguous or inaccessible part still beats a broken constraint.
- Every commit passes `./pre-commit-validation.sh`.

## Where the code differs from the TD

- The TD lists the value-type, reference-type, unmanaged and `new()` flags; the run's brief adds `notnull`.  The generated model is `#nullable enable` (`ModelEmitter.Header`), so a nullable argument where the parameter forbids null is a warning there (CS8714, CS8634, CS8631).  This plan refuses a nullable argument (written with `?`, or `System.Nullable<T>` longhand) where the parameter has `notnull`, a `class` constraint that is not `class?`, or a constraint type that is not annotated `?` and whose substituted type is not nullable.  Nullability nested inside an argument (`string?[]` against `IEnumerable<string>`) is not checked: `ClassifyConversion` ignores nullability and Roslyn has no public nullable-conversion check.  That stays a warning in the model and is recorded as TD-0067.
- `new()` also fails for a type with `required` members whose parameterless constructor has no `[SetsRequiredMembers]` (CS9040), which the TD does not name.
- A `System.Nullable<T>` argument never meets an interface constraint (CS0313), though `ClassifyConversion` calls it boxing.
- Found in review: a `System.Nullable<T>` argument boxes, for a constraint, only to `object` and `System.ValueType` (CS0312 for `System.Enum`), so that rule replaces the interface rule above; and an interface with a static abstract member it does not implement can be no type argument (CS8920), which is refused alongside the static class and the `ref struct`.
- `ClassifyConversion` is `CSharpExtensions.ClassifyConversion(Compilation, ITypeSymbol, ITypeSymbol)`, whose `Conversion` has `IsBoxing`; the common `CommonConversion` has none.

## Review Focus

1. A constraint type that names a type parameter (`where T : IComparable<T>`, `where U : T`): it must be substituted, or every such type is refused.  Tests: `SelfBox<int>` Found, `PairBox<object, string>` Found.
2. A nested generic whose inner parameter is constrained by the outer one (`Outer<T>.Inner<U> where U : T`).  Tests: `Nest<Base>.Inner<Derived>` Found, the reverse refused.
3. A broken constraint deep inside an argument (`Dictionary<string, ValueBox<string>>`) is the whole name's failure.
4. The BCL's own constraints: `List<Nullable<string>>` (the TD's example) and `Dictionary<int?, string>` (`TKey : notnull`) are refused; `List<int?>` and `Dictionary<int, string?>` stay Found.
5. Names that failed already keep their status: an inaccessible argument with a broken constraint elsewhere is `NotAccessible`.

---

### Task 1: Construct and check constraints in `MappedTypeResolver`

**Files:**
- Modify: `src/SqlSource/Generation/Types/MappedTypeResolver.cs`
- Modify: `src/SqlSource/Generation/Types/MappedTypeStatus.cs` (doc of `NotAPropertyType`)
- Test: `tests/SqlSource.Tests/Generation/Types/MappedTypeResolverTests.cs`

**Interfaces:**
- Consumes: `ClrTypeName` (`Keyword`, `Parts`, `Ranks`), `ClrNamePart.Arguments`, `ClrTypeArgument` (`Type`, `Nullable`), `ClrRank` (`NullableElement`, `Dimensions`).
- Produces: no new public member.  `Resolution` gains `ITypeSymbol? Symbol`, the type as a model writes it (null when failed, or when `use == Use.Whole`, since the whole needs none).

- [ ] **Step 1: Add the constrained types and the failing cases**

Add `#nullable enable` at the top of `ProjectSource` and, in `namespace MyApp`:

```csharp
public interface IShape;
public sealed class Circle : IShape;
public sealed class Square;
public readonly struct Dot : IShape;
public class Base;
public sealed class Derived : Base;
public abstract class Sketch;
public sealed class NoDefault { private NoDefault() { } }
public sealed class Form { public required string Title { get; init; } }
public sealed class SetForm { [System.Diagnostics.CodeAnalysis.SetsRequiredMembers] public SetForm() { } public required string Title { get; init; } }
public sealed class ValueBox<T> where T : struct;
public sealed class RefBox<T> where T : class;
public sealed class MaybeRefBox<T> where T : class?;
public sealed class RawBox<T> where T : unmanaged;
public sealed class KeyBox<T> where T : notnull;
public sealed class NewBox<T> where T : new();
public sealed class ShapeBox<T> where T : IShape;
public sealed class BaseBox<T> where T : Base;
public sealed class SelfBox<T> where T : System.IComparable<T>;
public sealed class PairBox<T, U> where U : T;
public class Nest<T> { public sealed class Inner<U> where U : T; }
```

Add `[InlineData]` rows to `Resolve_Name_GivesItsStatusAndWhatTheTypeIs` (all `global::`-qualified, `isPublic` true for Found, all-false for refused, no warnings):

| Name | Status | |
|---|---|---|
| `System.Collections.Generic.List<System.Nullable<string>>` | NotAPropertyType | |
| `MyApp.ValueBox<int>` / `<MyApp.Dot>` | Found | |
| `MyApp.ValueBox<string>` / `<int?>` / `<System.Nullable<int>>` | NotAPropertyType | |
| `MyApp.RefBox<string>` / `<int[]>` / `<MyApp.IShape>` | Found | |
| `MyApp.RefBox<int>` / `<string?>` | NotAPropertyType | |
| `MyApp.MaybeRefBox<string?>` | Found | |
| `MyApp.RawBox<int>` / `<MyApp.Dot>` | Found | |
| `MyApp.RawBox<string>` / `<System.Collections.Generic.KeyValuePair<int, string>>` / `<int?>` | NotAPropertyType | |
| `MyApp.KeyBox<string>` / `<int>` | Found | |
| `MyApp.KeyBox<string?>` / `<int?>` | NotAPropertyType | |
| `System.Collections.Generic.Dictionary<int, string?>` | Found | |
| `System.Collections.Generic.Dictionary<int?, string>` | NotAPropertyType | |
| `MyApp.NewBox<MyApp.Circle>` / `<int>` / `<int?>` / `<MyApp.UserId>` / `<MyApp.SetForm>` | Found | |
| `MyApp.NewBox<string>` / `<MyApp.Sketch>` / `<MyApp.NoDefault>` / `<MyApp.Form>` / `<int[]>` / `<MyApp.IShape>` | NotAPropertyType | |
| `MyApp.ShapeBox<MyApp.Circle>` / `<MyApp.Dot>` / `<MyApp.IShape>` | Found | |
| `MyApp.ShapeBox<MyApp.Square>` / `<MyApp.Dot?>` / `<MyApp.Circle?>` | NotAPropertyType | |
| `MyApp.BaseBox<MyApp.Derived>` / `<MyApp.Base>` | Found | |
| `MyApp.BaseBox<object>` | NotAPropertyType | |
| `MyApp.SelfBox<int>` / `<string>` | Found | |
| `MyApp.SelfBox<MyApp.UserId>` | NotAPropertyType | |
| `MyApp.PairBox<object, string>` / `<object, int>` / `<string?, string?>` / `<string?, string>` | Found | |
| `MyApp.PairBox<string, object>` / `<string, string?>` | NotAPropertyType | |
| `MyApp.Nest<MyApp.Base>.Inner<MyApp.Derived>` | Found | |
| `MyApp.Nest<MyApp.Derived>.Inner<MyApp.Base>` | NotAPropertyType | |
| `System.Collections.Generic.Dictionary<string, MyApp.ValueBox<string>>` | NotAPropertyType | |
| `MyApp.ValueBox<int>[]` | Found | |

Add to `Resolve_NameWithTwoFailures_GivesTheFirstInTheOrderOfTheStatuses`: `global::System.Collections.Generic.Dictionary<global::Other.Hidden, global::MyApp.ValueBox<string>>` gives `NotAccessible`.

- [ ] **Step 2: Run them and see the NotAPropertyType rows fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~MappedTypeResolverTests"`
Expected: the NotAPropertyType rows fail with `Found`; the Found rows pass.

- [ ] **Step 3: Implement**

In `ResolveType`:
- A keyword's symbol is its special type (`nint`/`nuint` via `CreateNativeIntegerTypeSymbol`).
- Resolve each argument with its symbol.  When every argument has one, construct the found definition: from the outermost containing type inward, a level is the definition when nothing outside it was constructed, else `containing.GetTypeMembers(definition.Name, definition.Arity)[0]`, and a level whose part (index `parts.Count - 1 - depth`) has arguments is `Construct`ed with them and checked.  A failed check is `First(status, NotAPropertyType)`.
- An argument written with `?` is `Nullable<T>` constructed for a value type, and `WithNullableAnnotation(Annotated)` for a reference type.
- The ranks wrap the element in `CreateArrayTypeSymbol(element, Dimensions)` from the last rank written to the first, the first rank's `NullableElement` making the element nullable as above.  Only when `use != Use.Whole`.

New private helpers:
- `static bool MeetsConstraints(Compilation, INamedTypeSymbol constructed)`: for each type parameter and its argument, the flags (`HasReferenceTypeConstraint`: `IsReferenceType`, and not nullable when `ReferenceTypeConstraintNullableAnnotation == NotAnnotated`; `HasValueTypeConstraint`: value type, not `Nullable<T>`; `HasUnmanagedTypeConstraint`: also `IsUnmanagedType`; `HasNotNullConstraint`: not nullable; `HasConstructorConstraint`: `HasPublicParameterlessConstructor`), then each constraint type substituted, converted by `ClassifyConversion` (identity, implicit reference or boxing; a `Nullable<T>` argument never meets an interface), and a nullable argument refused when the constraint's annotation is `NotAnnotated` and the substituted type is not nullable.
- `static ITypeSymbol Substitute(Compilation, ITypeSymbol, INamedTypeSymbol constructed)`: a type parameter of a level of `constructed` (matched by `OriginalDefinition` of its `DeclaringType`) is that level's argument at its `Ordinal`; an array is rebuilt over its element; a generic named type, or one nested in a generic, is rebuilt from its substituted containing type and arguments; anything else is itself.
- `static bool HasPublicParameterlessConstructor(ITypeSymbol)`: a value type unless it declares a non-public parameterless constructor; a class not abstract with a public parameterless constructor; neither when the type or a base has a `required` member (`IPropertySymbol.IsRequired`, `IFieldSymbol.IsRequired`) and that constructor has no `SetsRequiredMembersAttribute`.  An array, interface or type parameter has none.
- `static bool IsNullable(ITypeSymbol)`: `NullableAnnotation == Annotated` or `OriginalDefinition.SpecialType == System_Nullable_T`.

Skip all of it for a name with no arguments in any part.  Rewrite the remarks paragraph that cites TD-0056 to say what is checked, and add the broken constraint to the doc comment of `MappedTypeStatus.NotAPropertyType`.

- [ ] **Step 4: Run the resolver tests**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~MappedTypeResolverTests"`
Expected: all pass.  Then `./pre-commit-validation.sh` passes; commit.

### Task 2: Documents

**Files:**
- Delete: `docs/tech-debt/TD-0056-mapped-generic-constraints-are-not-checked.md`, its row in `docs/tech-debt/README.md`
- Create: `docs/tech-debt/TD-0067-nested-nullability-of-a-mapped-type-argument-is-not-checked.md`, its row, `Next id` to `TD-0068`
- Modify: `docs/manual/postgresql.md` (remove the known issue), `docs/manual/diagnostics.md` (`SQLSRC252`: a type argument that breaks a constraint, with `System.Nullable<string>` and a nullable key of `Dictionary`, and the fix), `docs/architecture/generator.md` (the resolution paragraph: what is constructed and checked, the TD-0067 gap)

- [ ] **Step 1: Edit the documents** as listed; the manual says effects only, no TD ids.
- [ ] **Step 2: `./pre-commit-validation.sh` passes** (the diagnostics-document and link tests among it); commit.
