# D-0007 - Converters by convention

## Planned

[The research on converters](../superpowers/specs/2026-10-24-query-generation-phase-8-converter-research.md#b-convention-members-on-the-mapped-type-zero-config), its option B, proposed reading a mapped type's own members as its converters, with no code: a `static T From(P)`, a public constructor `T(P)` or an `explicit operator T(P)` to read, and a `P Value { get; }` or an `explicit operator P(T)` to write.  It would make the types of Vogen, StronglyTypedId and positional records work through the query methods as they are.  [The 8.2 spec](../superpowers/specs/2026-10-24-query-generation-phase-8-2-converters-design.md) lists it among its non-goals, deferred as `D-0007`.

## Delivered instead

Only a method marked `[SqlSourceConverter]` is a converter: [`ConverterReader`](../../src/SqlSource/Generation/Converters/ConverterReader.cs) reads the marked methods of the project's source, and [`MappedTypeResolver`](../../src/SqlSource/Generation/Types/MappedTypes/MappedTypeResolver.cs) those of each mapped type of a referenced assembly.  A type with no marked method is given to the driver as it is, which throws `InvalidCastException` for a type the driver does not know.  The manual's known issues of [settings](../manual/settings.md#known-issues) show the two converters a project writes for such a type, each calling the type's own `From` and `Value`.

## Why deferred

The research found that a convention guesses silently: a constructor or a `Value` property that is not the database's representation would be used anyway, nothing is reported until a value is wrong, and Vogen's validating `From` throws on stored data that no longer validates.  The owner chose static methods marked by the attribute, whose signature can be checked, and the research recommended the convention only as an opt-in layered under them.

## Remaining work

- **Discovery**: in `MappedTypeResolver`, for a mapped type with no marked converter, its members of the convention, as a `Converter` of a new `ConverterOrigin`, for the project's own types as well as a reference's.
- **The rule**: both halves required, read and write, and their `P` equal to the base map's type, so that a type that only looks like an id is never converted; an opt-in, a key of the settings files or an attribute on the type, and whether it is on by default.
- **Validation**: whether a validating `From` is used on read, or a member that skips validation as Vogen's own Dapper handler does, and the diagnostics of a convention that matches in part.
- **The manual**: the convention in [converters](../manual/settings.md#converters), and its known issue removed.

## Trigger

Users of Vogen, StronglyTypedId or another library of strongly-typed ids asking to use their types without writing converters.
