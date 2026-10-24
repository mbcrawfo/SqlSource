# Query generation, phase 8: research on converters

Date: 2026-10-24

An appendix of the [epic outline](2026-10-07-query-generation-epic-design.md)'s phase 8 and of [sub-phase 8.2](2026-10-24-query-generation-phase-8-2-converters-design.md).  The research the owner asked for on how a project declares a conversion for a type the driver does not read, with static methods as the owner's leading hypothesis.  The owner chose static methods; 8.2 settles the form.  Kept as it was written; the code is the source of truth.


## What the repo already settles

- The override is the "what": the database type key goes to a C# type `T`, with precedence query marker > file marker > metadata > property, merged per key (`TypeOverrideMap`). `MappedTypeResolver` already resolves every override type against the compilation, references included, and keeps value-equal `MappedType` records with no symbols. So any lookup that starts from `T`'s symbol costs O(override types) and works across assemblies.
- The base map is the provider. For every database type the map gives the boxed type `B` and the binding: `DbType`, `DataTypeName` = `bindName` (for example `public.email` for the domain), or `SqlDbType` plus facets. That binding does not depend on the override.
- Not every override needs conversion. Today's overrides fall into two groups:
  - **Driver-native**: `timestamptz=DateTimeOffset`, `date=DateOnly`, `time=TimeOnly`, or a PostgreSQL enum to a C# enum with `MapEnum<T>()` (`Notes.sql`'s `NoteKind`). `GetFieldValue<T>` reads these directly.
  - **Driver-foreign**: the `Email` record struct, and strongly-typed ids. These need a converter.

  The generator cannot tell the two groups apart, because `MapEnum`, `MapComposite` and plugins are run-time calls. So the presence of a converter has to be what switches the read and write path, and an override without one keeps `GetFieldValue<T>` and binds `T` directly.
- The grammar has room after an entry. `ClrTypeName.TryParse` rejects any trailing text, so a suffix is invalid today, and giving it a meaning would break nothing. Three limits on a suffix:
  - It cannot contain white space, which separates MSBuild entries.
  - It cannot contain `;` or `#`, because the compiler cuts the value there.
  - `:` collides with `global::`, and `,` and `<>` are taken by generic arguments.

  `|`, `@` or `/` are free, for example `public.email=MyApp.Email|MyApp.EmailConverter`.
- One constraint I found: `[SqlSourceGenerate]` is emitted with `[Conditional("SQLSOURCE_ATTRIBUTES")]`, so it does not survive into metadata. A converter attribute that must be seen on members in another assembly cannot be `[Conditional]`. Dapper.AOT made the same choice for the same reason.

## Prior art

| Tool | Shape | Keyed by | Instance? | Nulls |
|---|---|---|---|---|
| EF Core `ValueConverter<TModel,TProvider>` / `HasConversion` | two expression trees; bulk through `ConfigureConventions().Properties<T>().HaveConversion<C>()` | CLR type (convention) or property | instance, but cannot reach `DbContext` (efcore#12205) | "A `null` value will never be passed to a value converter" (docs) |
| linq2db `MappingSchema.SetConverter<TFrom,TTo>` | lambdas; plus `ConvertBuilder` *auto-discovers* ctor(`from`), `Value`, `op_Implicit`/`op_Explicit`, `Parse`, `ToString` (verified in source) | type pair | schema instance, configure once | — |
| jOOQ `Converter<T,U>` (`from`/`to`) + `Binding` | codegen `forcedTypes`: `userType` + `converter`, matched by regex (`includeExpression` by name; `includeTypes` by DB type, not re-verified) | DB type or column | class | converter handles null itself (docs example) |
| SQLDelight `ColumnAdapter<T,S>` (`decode`/`encode`) | `TEXT AS List<String>` in `.sq`; an adapter **instance passed to the `Database` constructor** | column | instance, DI-friendly | example is NOT NULL only; null handling not stated |
| JPA `AttributeConverter<X,Y>` | `@Converter(autoApply = true)` | Java type, global | instance; CDI-injectable since JPA 2.2 (from memory) | — |
| SQLx `#[derive(Type)] #[sqlx(transparent)]` | encode and decode delegate to the newtype's inner type; traits on the type itself (≈ static abstract) | Rust type | none | `Option<T>` outside |
| sqlc overrides | `db_type` or `column` → `go_type`; column wins; separate `nullable` entries | DB type / column | none | the type implements `sql.Scanner`/`driver.Valuer` (the `database/sql` contract, not stated on the overrides page) |
| Dapper `SqlMapper.TypeHandler<T>` | `Parse(object)`, `SetValue(IDbDataParameter,T)`; `AddTypeHandler(instance)`, a process-global static registry | CLR type | singleton | — |
| Dapper.AOT (1.1.0, Sept 2026; the change merged in Aug 2026) | `[assembly: TypeHandler(typeof(T), typeof(H))]`, `IDbValueHandler<T>`/`DbValueHandler<T>`. The generic `[TypeHandler<,>]` is now `[Obsolete]`: "never implemented". Not generic and not `[Conditional]`, so a package can declare handlers for its own types; adapts vanilla handlers | CLR type, per assembly | public parameterless ctor | `SetNullValue` is separate |
| Npgsql plugins | `MapEnum`/`MapComposite`; custom `IPgTypeInfoResolver` through `AddTypeInfoResolver`, in `Npgsql.Internal` and `[Experimental("NPG9001")]` | — | — | — |

What the strongly-typed-id generators already emit:

- **Vogen**: `Value` and `public static T From(TPrimitive)`, which validates. Its own Dapper handler calls `__Deserialize` to skip validation. `IVogen<TSelf,TPrimitive>`, with `static abstract From`/`TryFrom` and optional cast operators, is opt-in through `StaticAbstractsGeneration`. A `Conversions` flag emits Dapper, EF Core and linq2db converters that reference those libraries directly.
- **StronglyTypedId**: `Value` and a public ctor `(T value)`. The Dapper and EF Core converters come from optional templates.
- **Meziantou**: `Value` and `FromInt32`/`FromString`.
- **A plain `record struct Email(string Value)`**: ctor and `Value`.

What they share is a **`Value` getter**, plus a ctor(P), `From(P)` or `FromXxx(P)`. No single name covers them all.

Other prior art:

- **Npgsql**: a user may prefer the driver route because then Dapper, raw ADO.NET and the generated code all work with no generator conversion. But the resolver API is internal and experimental, so in practice that route is for library authors.
- **SQL Server**: SqlClient has no type-extension point that I know of (not verified), so a converter in generated code is the only route there.

## Options

### A. `[SqlSourceConverter]` on static methods (recommended)

```csharp
static class EmailConversions
{
    [SqlSourceConverter] public static Email ToEmail(string value) => new(value);
    [SqlSourceConverter] public static string FromEmail(Email email) => email.Value;
}
```

**1. Declaration and discovery**

- The attribute is emitted as internal source, C# 7.3, **not `[Conditional]`**, and is added to `AttributeSource.GeneratedTypes` and to the suppressor.
- The generator looks in two places:
  - The project's own source, through `ForAttributeWithMetadataName` (Roslyn 4.3+, so within the 4.8 floor). This gives one value-equal record per method: containing type and method name, the input and output types in `ClrTypeName.Text` form, nullability, a `LocationInfo` and any warning ids. The step is collected, then `Select`ed to an `EquatableArray`.
  - The members of each override type `T`, wherever `T` is declared, read in `MappedTypeResolver`. The attribute is matched by metadata name, so a library that references SqlSource too, and has its own internal copy, can annotate its ids.
- **A method has no direction of its own.** It is an edge `X → Y`. At a column whose type came from an override, `T` comes from the override and `P` is the base map's `B` for that database type: the read path uses the edge `P → T`, and the write path uses `T → P`.
- **Converters apply only where an override set the type** (`PropertyType.Place != null`). So a base-map column never changes. A `DateTime → DateTime` edge is the `Kind` fix, and it applies only under `timestamp=System.DateTime`, at whatever level that override was written.

**2. Validation**

At the method, an error for each of these:

- not `static`;
- generic, or inside a generic type;
- not exactly one by-value parameter (`ref`/`out`/`in`/`params`/optional are refused);
- returns `void`, `Task` or `ValueTask`;
- not accessible within the compilation's assembly (`IsSymbolAccessibleWithin`, which honours `InternalsVisibleTo`);
- a read edge whose return is annotated nullable, which would put CS8601 in generated code, and this repo treats any generated warning as a bug;
- two methods with the same `X → Y` (reported at both).

`[Obsolete]` methods get the `MappedTypeWarnings` treatment, or are refused.

At the query (ids from `SQLSRC272`, placed and reported once per query like `SQLSRC250`), only under `CodeGen`:

- An override type with converters, none of which has the provider `B`. The message names it: "converters for `UserId` take `int`; `bigint` reads as `long`".
- A parameter whose type has a read converter and no write converter, or a column with a write converter and no read converter.

A models-only project never sees these errors.

**3. Provider type**

- `P` must be exactly `B` from the base map. The read is `GetFieldValue<B>`, which phase 8 already relies on.
- The binding is the base map's `DbType`/`DataTypeName`/`SqlDbType`+facets, unchanged, and its value is the write converter's result.
- An `int` converter on a `bigint` column is an error, not a silent narrowing.
- Two edges `UserId ← int` and `UserId ← long` can coexist, so one id type can serve both widths.
- Letting the converter choose any `GetFieldValue` type the driver accepts would need a per-driver table of legal alternatives, which does not exist.

**4. Nulls, arrays and direction**

- Nulls work as in EF Core: the converter never sees null. The read is `IsDBNull ? default : Conv(GetFieldValue<B>(i))`, with `IsDBNull` emitted only for a nullable column, as phase 8 plans. The write is `v is { } x ? Conv(x) : DBNull.Value`.
- Because the converter never sees null, one converter serves both nullable and non-null properties.
- `email[]` gets `Email[]` from the element's override (an existing rule). The generator reads `GetFieldValue<string[]>` and applies `Array.ConvertAll`. A null element throws a clear `InvalidCastException`, the same as Npgsql's `Never` mode does for value types.
- An explicit `public.email[]=X` override needs an `X ↔ string[]` edge, and the element converter is not used for it.
- Each direction is independent, so a read-only column type needs only its read method.

**5. Scope**

- The converter set is global to the project and keyed by type pair.
- Which `T` a column gets still follows the override's precedence. The override decides *whether* to convert, and the converter decides *how*.
- Two different conversions for the same `(T, P)` in different files are not possible. Option C can add that later.

**6. Dapper users**

They get nothing new. They still need a `TypeHandler`, but it can be three lines that call the same static methods, so the conversion is written once. The recipe should show that.

SqlSource *could* emit a `SqlMapper.TypeHandler<Email>` adapter when it detects Dapper; Vogen does exactly that. But it would break the epic's rule that "nothing generated depends on Dapper", it would still need global registration at startup, and Dapper.AOT now has its own different contract. My advice is not in phase 8.

**7. DI**

- There is no instance state, so there is nothing to register and no singleton.
- Conversions that need services, such as an encryption key or a tenant, are out of scope. EF Core has the same limit (efcore#12205).
- An instance method is a diagnostic that says "make it static".
- If state is ever needed, the DI-correct route is SQLDelight's: a converter instance as an argument of the generated method, or of a generated context. That would change phase 8's signature, so not now.

### B. Convention members on the mapped type (zero config)

**Read**: `static T From(P)`, a public ctor `T(P)`, or `explicit operator T(P)`. **Write**: an instance `P Value { get; }`, or `explicit operator P(T)`.

- **Discovery** happens only on `T`'s symbol, in the resolver, so it is cheap and works across assemblies.
- **What works well**:
  - With no code at all, it lines up with Vogen, StronglyTypedId, Meziantou (`Value` only; `FromInt32` would need a name list) and positional records. linq2db's `ConvertBuilder` proves the idea.
  - Dapper users still write a handler.
- **What it costs**:
  - Silent meaning. A ctor or a `Value` getter that is not the database representation gets used anyway. `DateTimeOffset` has a public `ctor(DateTime)`; it has no `Value`, so it fails the pair, but that is luck.
  - There are no diagnostics until a mismatch.
  - Vogen's validating `From` throws on bad stored data.
  - It cannot cover third-party types such as NodaTime `Instant` on SQL Server.
- **Use it as a fallback layered under A**, behind an opt-in or a rule that both halves must match, rather than as the main mechanism.

### C. Converter type named in the override entry

`public.email=MyApp.Email|MyApp.EmailConverter`, where `EmailConverter` is a static class whose one-parameter static methods supply the edges, validated as in A.

- **Keyed by database type**, so it inherits the precedence and levels for free.
- Works across assemblies, because the type is resolved like the override's.
- Works for third-party types.
- The tool ignores it, as it ignores the property today. Phase 8's check binds by `bindName` regardless.
- **Costs**:
  - A new grammar suffix in four places: `TypeOverride` gains a member, and `SQLSRC112` must compare converters too.
  - Repetition for every key that maps to `Email`: the domain, its array, and other domains.
  - A converter can be forgotten at one level.
  - Its best feature, a converter per file, is a rare need.
- **Keep it as the later extension that A leaves room for.**

### D. A static abstract interface, `ISqlSourceConvertible<TSelf, TProvider>`

`static abstract TSelf FromProvider(TProvider)` and `TProvider ToProvider()`.

- The compiler enforces the signature, and the IDE offers "implement interface". The .NET 8 / C# 12 floor supports it.
- **Costs**:
  - The interface would be emitted internal into each project, so a type in another project implements *its own* copy, matched by name. That is workable, but `T` has to reference SqlSource.
  - A `netstandard2.0` domain library, which is common for shared ids, cannot declare static abstract members at all.
  - It couples domain types to the data-access library.
  - It cannot be added to third-party types.
  - It adds nothing the generator needs: generated code calls `Email.FromProvider(x)` directly, and A's validation catches the same mistakes.
  - SQLx's trait-on-type works because traits are idiomatic in Rust; here it is the heaviest option for the user.

## Comparison

| | A attribute | B convention | C in override | D interface |
|---|---|---|---|---|
| User code | 2 small methods | none | 2 methods + suffix | interface impl |
| Third-party `T` | yes | no | yes | no |
| `T` in another project | yes (converter in consumer, or attributed on `T`) | yes | yes | only if it references SqlSource and is not netstandard2.0 |
| Explicit and diagnosable | yes | weak | yes | yes |
| Per-level scoping | through the override only | no | yes | no |
| Matches strongly-typed-id libraries | via a 2-line wrapper | mostly | via a wrapper | no |
| Pipeline cost | one FAWMN step + resolver | resolver only | resolver only | resolver only |
| Grammar change | none | none | yes | none |

## Recommendation

**Go with A**, the owner's hypothesis. It holds up:

- Static, pure and validated. No singleton and no DI story.
- Keyed by `(T, P)` and applied only where an override chose `T`.
- `P` pinned to the base map's boxed type, so the read stays `GetFieldValue<B>` and the binding is the base map's own.
- The converter never sees null.
- Arrays are converted element by element.
- Each direction is optional.

Look for the methods both in the project's source and on `T`'s own members, and do not make the attribute `[Conditional]`. Without a converter an override keeps the driver route (`MapEnum`, plugins, `DateOnly`), so nothing that works today changes. Leave room for B (opt-in zero config for ids) and C (a `|Converter` suffix) as later additions; neither is blocked by A.

Resolving D-0003 needs one caveat. A does **not** make a composite or `regclass` parameter bindable (`SQLSRC232`), because the base map has no `B` for those types. The workaround of a `text` parameter with a cast in the SQL stays.

## Open questions for the owner

1. Should an override to a driver-foreign type with no converter under `CodeGen` be silent, informational, or a configurable warning? The generator cannot prove the driver reads it.
2. Should a `P` other than `B` ever be allowed? For example `GetFieldValue<string>` on a `citext`, or `DateTimeOffset` on `timestamptz` where the driver reads both. Or is exact-`B` the rule?
3. Should B's convention ship in phase 8? If so, on by default or opt-in, and should a validating `From` (Vogen) be used on read?
4. On a nullable column, may a read converter map a non-null value to null? That needs a `T?` return and the generated code's nullability to follow it.
5. Should SqlSource ever emit Dapper (or Dapper.AOT) adapters when it detects Dapper, bending "nothing depends on Dapper"?
6. A per-file or per-query converter (C): is it needed now, or recorded as deferred?
7. Under `CodeGen`, should an override to a C# enum on a SQL Server `int` column be built in (a cast), need a converter, or rely on `GetFieldValue<TEnum>`? I have not verified that SqlClient's `GetFieldValue<TEnum>` unboxes an `int`.

## Not verified

- jOOQ `includeTypes`, `lambdaConverter` and `Converter.ofNullable` (seen in docs I did not reach).
- SQLDelight's and Hibernate's null handling.
- JPA CDI injection (from memory).
- Whether SqlClient has any type-plugin API.
- Whether Npgsql writes a `string` with `DataTypeName` set to an enum's name (needed by an enum converter without `MapEnum`).
- Vogen's default cast-operator settings.
- Whether Dapper.AOT 1.1.0 contains the August `DbValueHandler` change (it is likely, from the dates).
