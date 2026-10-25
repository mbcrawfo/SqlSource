# Query generation, phase 8.2: converters - design

Date: 2026-10-24

Sub-phase 8.2 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It lets a project convert a type override's C# type to and from the type the driver reads, with static methods it marks `[SqlSourceConverter]`, so that the methods of [sub-phase 8.1](2026-10-24-query-generation-phase-8-1-methods-design.md) read and bind a strongly-typed id, a wrapper such as `Email`, or any type the driver does not know.  The form was chosen from [the research of 2026-10-24](2026-10-24-query-generation-phase-8-converter-research.md), its option A.

It builds on 8.1.  Sub-phases 8.3 and 8.4 build on it.

## Intent

A type override names a C# type and nothing else.  Through Dapper a project converts it with a type handler; a generated method reads with `GetFieldValue<T>` and binds a `T`, which the driver refuses for a type it was never taught.  Phase 7's converted map rows were to give the conversion, and phase 7 now follows phase 8, so the methods need their own.

The owner's direction: a converter is a static method, because its signature can be validated and most conversions are a pure transform; not a handler instance, which needs a singleton registered somewhere, awkward where everything else comes from dependency injection.  An override with no converter keeps the driver's route, by the owner's decision that it is trusted, so a project that maps an enum with `MapEnum<T>()` or uses a plugin changes nothing.

## Goals

1. `SqlSourceConverterAttribute`, emitted with the other generated types, which marks a static method of one argument as a conversion from its argument's type to its return type.
2. The converters of a project found in its own source and on the members of every C# type a type override names, wherever that type is declared.
3. Each converter validated, with a diagnostic at the method for each fault.
4. A column whose type an override chose, under `CodeGen`, read as the base map's type and converted when a converter from it exists; a parameter so typed converted to the base map's type and bound as the base map binds it.
5. Null never reaching a converter; PostgreSQL arrays of a converted element converted element by element.
6. `D-0003` resolved for every type the base map binds, and its file rewritten to what remains.

## Non-goals

- Picking up conversion members by convention, a `Value` property and a `From` method or a constructor, which would make Vogen's and StronglyTypedId's types work with no code.  Deferred as `D-0007`; the research found it guesses silently.
- A converter chosen per file or per query, by a suffix on the type override's entry.  Deferred as `D-0008`.
- A converter with state, an instance or a service from dependency injection.  Out of the epic: a conversion that needs a key or a tenant is the caller's, before the call and after it.
- A converter that changes the type the driver is asked for.  The database side of a converter is the base map's type exactly; see Design.
- Making a composite, a table's row type or another type `SQLSRC232` refuses bindable.  It has no base map type; the `text` parameter with a cast stays the way.
- A Dapper type handler generated from the converters.  Nothing generated depends on Dapper; the recipe shows a handler of three lines that calls the converters.
- A library's converters shipped by SqlSource, for NodaTime on SQL Server or the like.  Phase 7, built on these.

## Anti-goals

- **A change to a project that has no converter.**  No converter, no change: a base-map column, and an override with no converter, are read and bound as 8.1 does it.
- **A silent narrowing.**  An `int` converter on a `bigint` column is an error, not a cast.
- **A converter called with null, or returning null into a property the model calls not null.**
- **A query diagnostic for a project that does not generate methods.**  A `Models` project's converters are never used and never reported at a query.  A marked method that is no converter is still `SQLSRC277` in any project, since the attribute says what it is meant to be, and a library that declares converters for its own types learns of a fault where it can fix it.

## What exists today

As of the merge of 8.1, which this spec assumes:

- The type override, `SqlSourceTypeOverrides` and `-- type-override:`, resolved by `MappedTypeResolver` against the compilation, references included, into value-equal `MappedType` records; `PropertyType.Place` is set when an override chose the type.
- The base maps give, per database type, the C# type the driver boxes: the type 8.1 reads with `GetFieldValue<T>` when no override applies.
- 8.1's methods read every column with `GetFieldValue<T>` of the property's type and bind every parameter by value through one binding step per engine.
- A PostgreSQL array whose element an override maps takes the mapped element type, `public.email=MyApp.Email` giving `email[]` the type `MyApp.Email[]`.
- `SQLSRC232` refuses a parameter whose type Npgsql cannot bind.
- The end-to-end projects map a domain or alias type to a record struct `Email`, read through Dapper by `EmailHandler`.

## Design

### Declaring a converter

```csharp
public readonly record struct Email(string Value)
{
    [SqlSourceConverter] public static Email FromDatabase(string value) => new(value);
    [SqlSourceConverter] public static string ToDatabase(Email email) => email.Value;
}
```

Or in a class of the project's own:

```csharp
internal static class Conversions
{
    [SqlSourceConverter] public static UserId ToUserId(long value) => new(value);
    [SqlSourceConverter] public static long FromUserId(UserId id) => id.Value;
}
```

A converter is an edge from its argument's type to its return type, with no direction of its own.  Where an override set a column's type to `T` and the base map reads its database type as `B`, the read is the edge `B → T`; for a parameter, the write is `T → B`.  A method whose argument and return types are one type serves both directions.  The method's name is the project's.

`SqlSourceConverterAttribute` is emitted in the post-initialization file, `internal`, `[AttributeUsage(AttributeTargets.Method)]`, in the namespace `SqlSource`, C# 7.3, and **not** `[Conditional]`, so that it stays on a method of a referenced assembly; it joins `AttributeSource.GeneratedTypes`.  A library that declares its ids with converters references SqlSource for the attribute, and the consumer's generator matches its copy by name.

### Finding converters

Two places:

1. **The project's source**, by `ForAttributeWithMetadataName` on `SqlSource.SqlSourceConverterAttribute`: one value-equal record per method, holding its containing type and name, its argument and return types as C# text, its nullability, its position, and its warning ids.
2. **The members of every type an override names**, read when the type is resolved, in source or in metadata, by the same attribute name.  They are a separate output of their own beside `MappedType`, not a member of it, since `MappedType` feeds the models and a converter's edit must not re-run them.

The converters are collected into one project-level value, sorted, as every collected value is.  An edit to a converter re-runs the methods of the types whose files use an override; nothing else.

### Validation

At the method, each `SQLSRC277`, naming the fault:

- not `static`;
- generic, or in a generic type;
- not exactly one argument, or an argument that is `ref`, `out`, `in`, `params` or optional;
- returning `void`, `Task` or `ValueTask`, or a type annotated nullable;
- not accessible from the project, by `IsSymbolAccessibleWithin`, which honours `InternalsVisibleTo`;
- marked `[Obsolete]` as an error.

A converter marked `[Obsolete]` as a warning is used, its warning suppressed in the generated code as sub-phase 5.2 suppresses a mapped type's.  Two converters of one edge in the project's source are `SQLSRC278`, at both; so are two on one mapped type.

A converter found on a mapped type in a referenced assembly has no position in the project.  Its faults are not reported where it is declared, which the project cannot change, but at each query whose method would use it, as `SQLSRC277` naming the method and the fault, and the query gets no method.  When the project's own source and a mapped type each give one edge, the project's converter is used and no diagnostic is reported, so that a project can replace a library's conversion.

### Applying converters

Only where an override chose the type, `PropertyType.Place` set, and only under `CodeGen`.

**`B`**, the database side, is the base map's own type for the column's or parameter's database type on the project's driver, as if no override of any level applied to it or to the types it is built on: a domain's is its base's base-map type, whatever override the base has, and an array's is the array of its element's base-map type.  So a domain over `timestamptz` mapped to `MyApp.When` has `B` `DateTime`, even where `timestamptz=System.DateTimeOffset` is in effect.  `B` depends on the driver where the base map does, `date` on Npgsql 8 and 10 for one, and the manual says that updating Npgsql can then ask for another converter.

**A foreign type** is a `T` that the base map of the file's dialect never gives for any type.  `Email` and `UserId` are foreign; `DateTimeOffset`, `DateOnly` and `string` are not, so a converter between `DateTimeOffset` and another type never makes a plain `timestamptz` column ask for one.

| For `T`, the project has | Column | Parameter |
|----|----|----|
| `B → T` | `GetFieldValue<B>`, then the converter | — |
| `T → B` | — | The converter, then bound as the base map binds `B` |
| `T → B` but not `B → T` | `SQLSRC280` | — |
| `B → T` but not `T → B` | — | `SQLSRC280` |
| No edge between `T` and `B`, and `T` foreign with an edge to or from another type | `SQLSRC279` | `SQLSRC279` |
| No edge between `T` and `B` otherwise | `GetFieldValue<T>`, as 8.1 | Bound as a `T`, as 8.1 |
| Any edge to or from `T`, and the base map has no `B`: a composite, `regclass`, a user-defined range, `hierarchyid` | `SQLSRC279`, saying the database type has no type to convert from | `SQLSRC279`, the same |

The binding of a converted parameter is the base map's for its database type, which 8.3 and 8.4 give: the converter changes the value, never the type sent.  A query with `SQLSRC279` or `SQLSRC280` gets no method, by 8.1's rule for a query that fails a check, and keeps its models.

### Null

The converter never sees null.  A nullable column is `reader.IsDBNull(i) ? null : Convert(reader.GetFieldValue<B>(i))`; a column the model calls not null is `Convert(reader.GetFieldValue<B>(i))`, and the driver's throw on a `NULL` stays the oracle.  A nullable parameter is `value is { } v ? Convert(v) : DBNull.Value`, and so is a not-null parameter of a reference type, which a caller can still pass as null, by 8.1's rule.  So one pair of converters serves a nullable and a not-null property alike.

### Arrays

A PostgreSQL array whose element type an override chose, with no override of the array, is converted by the element's edges: read as `B[]` and converted element by element into `T[]`, and written the other way.  So is an array override whose C# type is an array of a foreign element, `public.email[]=MyApp.Email[]`, when no edge names `MyApp.Email[]` itself.  A `NULL` element of an array of a value type `T` throws `InvalidCastException` naming the column.  Npgsql under its default `Array Nullability Mode` throws one too when the method reads `B[]`, but its message names no column ([the spike](2026-10-24-query-generation-phase-8-spike.md#3-an-array-that-holds-a-null)), so naming it is the method's own; a `NULL` element of a reference type stays null, the converter not called.  An override of the array type itself, `public.email[]=MyApp.Emails`, takes a converter of its own between `MyApp.Emails` and `B[]`, and the element's converter is not used for it.

## Diagnostics

| Id | Severity | Where | About |
|----|----|----|----|
| `SQLSRC277` | Error | The converter in the project's source; the query, for one on a type of a referenced assembly | A method marked `[SqlSourceConverter]` that is not a converter, with the fault |
| `SQLSRC278` | Error | Each of the converters | Two converters of one edge in one place |
| `SQLSRC279` | Error | The query | A foreign type with converters, none of them to or from the type the database type is read as, which the message names; or a type with converters whose database type the base map does not read |
| `SQLSRC280` | Error | The query | An override's type that has converters for its database type, but not for the direction the query needs: from it for a column, to it for a parameter |

The ids follow 8.1's; if 8.1 merged with more or fewer, this sub-phase takes the next free ids in this order and the plan says so.

## Testing

| Where | Cases |
|----|----|
| Generator tests | A converter in the project and one on the mapped type, in source and in a referenced assembly; each fault of `SQLSRC277`; `SQLSRC278`; `SQLSRC279` with a `long` column and an `int` converter; `SQLSRC280` for a column and for a parameter; the text of the read and the write, nullable and not null, of a value type and a reference type, an array and an array override; a converter of one type to itself; an `[Obsolete]` converter; a `Models` project with converters and none of the query diagnostics; no change to a project with no converter; the generated code compiled against both drivers' metadata with no warning |
| Caching tests | An edit to a converter re-runs the methods and not the models; an edit to a type with no converter re-runs nothing of the methods |
| `AttributeConflictTests` | `SqlSourceConverterAttribute` among the suppressed types |

The run-time proof is 8.3's and 8.4's: the end-to-end projects' `Email` read and written through converters, and an id type converted from a `bigint`.

## Documentation

- `docs/manual/settings.md`: the type override's section gains converters, what they are, where they are found, the database side as the base map's type, and null.
- `docs/manual/generator.md`: Query methods gains how a converted type is read and bound.
- `docs/manual/recipes.md`: a converter for a strongly-typed id; and in the Dapper recipe, a type handler that calls the same converters.
- `docs/manual/postgresql.md` and `docs/manual/sqlserver.md`: the Types sections, where a mapped type the driver does not read now has a converter as its answer.
- `README.md`: the FAQ the owner asked for, "I got an `InvalidCastException`; what does it mean and how do I fix it?": a type the type override names that the driver does not read, the two fixes, teaching the driver the type or adding a converter, and a link to the manual.
- `docs/manual/diagnostics.md`: a section for each new id.
- `docs/architecture/generator.md`: the converters' steps.
- `src/SqlSource/AGENTS.md`: the ids taken.
- `docs/deferred/`: `D-0003` rewritten to what remains, the types `SQLSRC232` refuses; `D-0007`, converters by convention; `D-0008`, a converter per file or query.
- `docs/tech-debt/TD-0006`: `SqlSourceConverterAttribute` among the types.
- The epic outline: the row of 8.2.

## Order of work

One pull request, one commit for each step:

1. The outline's row says In progress.  The attribute.
2. Finding converters in the project, with `SQLSRC277` and `SQLSRC278`.
3. Finding converters on the mapped types.
4. Applying them to columns and parameters, with `SQLSRC279` and `SQLSRC280`; null.
5. Arrays.
6. Caching tests.
7. The documents, and the row set to Done.

## Left to the builder

- The records and steps, within the caching rules.
- The text of each message, and the layout of the converted read and write in the emitted code.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec.  The owner chose static methods; the rest follows the research's recommendation:

- The form is an attribute on the method, not a convention, an interface or a suffix on the override's entry.
- A converter is found in the project and on the mapped type alone, not in any other class of a referenced assembly.
- The database side of a converter is the base map's type exactly; a converter of another type is `SQLSRC279`, though the driver might read it.
- A converter applies only where a type override chose the type, never to a base-map column.
- A read converter may not return null.
- A converter marked `[Obsolete]` as a warning is used with the warning suppressed.
- `B` is the pure base map's type, no override of any level applied; `SQLSRC279` is raised only for a foreign type, so a converter of a type the base map gives never turns that type's other columns into errors.
- A library's faulty converter is reported at the query that would use it; the project's own converter wins over a mapped type's for one edge.
- `SQLSRC277` and `SQLSRC278` are reported in any project; the query diagnostics only under `CodeGen`.
- `D-0007` and `D-0008` are deferred, not built.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.
