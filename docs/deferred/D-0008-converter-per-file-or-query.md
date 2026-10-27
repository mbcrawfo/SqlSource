# D-0008 - A converter per file or per query

## Planned

[The research on converters](../superpowers/specs/2026-10-24-query-generation-phase-8-converter-research.md#c-converter-type-named-in-the-override-entry), its option C, proposed naming a converter class in the type override's entry, `public.email=MyApp.Email|MyApp.EmailConverter`, so that the converter is keyed by the database type and follows the override's levels: one file or one query could convert a C# type another way than the project does.  [The 8.2 spec](../superpowers/specs/2026-10-24-query-generation-phase-8-2-converters-design.md) lists it among its non-goals, deferred as `D-0008`.

## Delivered instead

A converter is an edge between two C# types for the whole project: [`ConverterSet`](../../src/SqlSource/Generation/Converters/ConverterSet.cs) holds one per pair of types and direction, a second in the project's source being `SQLSRC278`, and [`ConversionPlanner`](../../src/SqlSource/Generation/Methods/ConversionPlanner.cs) chooses by the property's type and the base map's type alone, whatever chose the property's type: a level of the override, a column type or a `-- param:` or `-- column:` marker.  An entry's text after its C# type is still not valid: [`TypeOverrideGrammar`](../../src/SqlSource/Settings/TypeOverrides/TypeOverrideGrammar.cs) rejects it.  The manual's known issues of [settings](../manual/settings.md#known-issues) say that a type has one conversion in a project, and that a file or query that needs another gives the database type, or the column, a C# type of its own.

## Why deferred

The research judged a converter per file a rare need, and the suffix costly: a new part of the grammar of the settings files and both markers, a member of `TypeOverride`, `SQLSRC112` comparing converters too, and a converter to repeat on every key that maps to the type, a domain, its array and other domains.  It recommended it as a later extension that the attribute leaves room for.

## Remaining work

- **The grammar**: in a settings file, a member of `typeOverrides` or `columnTypes` whose value is an object, `{"type": "MyApp.Email", "converter": "MyApp.EmailConverter"}`, which needs no suffix; in a marker, a suffix after the C# type of the entry, with a character that the value allows, `|` for one, since `:` is taken by `global::` and `,` and `<>` by type arguments; `SQLSRC019` and `SQLSRC111` for a converter that is not valid.
- **The levels**: `TypeOverride` gains the converter type, merged by the override's precedence, and `SQLSRC112` compares it.
- **Resolution and use**: the converter type resolved as a mapped type is, its static methods read by `ConverterReader` and checked as `SQLSRC277`, and its edges used before the project's and the mapped type's for the properties that entry types, which `PropertyType` or the slice of `MethodsInput` must then carry.
- **The manual**: the suffix in [type overrides](../manual/settings.md#setting-type-overrides) and [converters](../manual/settings.md#converters), and the known issue removed.

## Trigger

Users who need one C# type converted two ways in one project, by file or by query.
