# Query generation, phase 5.2: the PostgreSQL type map and the type override - design

Date: 2026-10-18

Sub-phase 5.2 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It gives each parameter and column of a usable sidecar entry its C# type: from the base map of every type Npgsql reads, under the Npgsql the project references, and from the user's type override, a new setting at every level but the attribute.  It emits no model; sub-phase 5.3 does, from the types this sub-phase gives.

It builds on [sub-phase 5.1](2026-10-18-query-generation-phase-5-1-sidecars-in-the-generator-design.md).  Sub-phase 5.3 builds on it.

## Intent

The owner's first measure of phase 5: model generation must map the built-in .NET types to the out-of-the-box PostgreSQL types accurately and reliably.  A wrong C# type is the failure that matters most, because it compiles: a `DateTime` where Npgsql gives a `DateOnly` is a Dapper exception at run time, or a value silently converted.  So the map is not written from documentation alone.  Every row was read from Npgsql 8.0.3, 9.0.3 and 10.0.3 against a PostgreSQL 18 server and cross-checked in Npgsql's source at the 8.0, 9.0 and 10.0 releases (below), every row has a test, and sub-phase 5.4 reads a value of every row through Dapper.

The second measure is speed: the map is a lookup, the probe of the driver runs once per compilation into a small value, and a project without overrides pays nothing for the override.

The override is the user's answer to a type the map chose and they do not want: most projects set it once for the project, and a few override it for a folder, a file or one query.  The owner set its requirements; its design was researched and approved.

## Goals

1. The base map: every PostgreSQL type Npgsql reads with a plain `NpgsqlDataSourceBuilder`, to the type Npgsql boxes, with a diagnostic for every type it cannot read.
2. The driver probe: whether the project references Npgsql, and which of its features it has, from the compilation, by symbol.
3. The diagnostics of the driver: below the floor, newer than the generator knows, and a type that needs Npgsql in a project without it.
4. The type override, `SqlSourceTypeOverrides`, as a property, as `AdditionalFiles` metadata, and as the `-- type-override:` marker in a file's header and inside a query, merged per key.
5. Each type the override names resolved against the compilation, with a diagnostic for one that is not found, not accessible, or cannot be a property's type.
6. For each query that needs an entry and has a usable one, each parameter's and column's C# type, for sub-phase 5.3 to emit.

## Non-goals

- Emitting a model.  Sub-phase 5.3.
- The library maps, NodaTime, NetTopologySuite and `JsonDocument`, and a converted type with read and write expressions.  Phase 7.
- SQL Server's map.  Phase 6, which uses the same override with SQL Server's type names.
- A per-column C# type.  Out of the epic.
- The two date and time options the epic planned, `SqlSourceDateTimeTypes` and `SqlSourceTimestampTzType`.  They are dropped: the override says both, at every level, and the epic outline is updated (below).

## Anti-goals

- **A type that compiles and is wrong.**  Every row is what Npgsql boxes, read from the driver, not what its documentation implies.  A type Npgsql cannot box is an error, never a guess.
- **A map by version number.**  The epic decides that the map tracks the driver by feature, detected by symbol: an assembly version serves diagnostics only.
- **An override that silently does nothing.**  A key that names no type the generator knows, and is not schema-qualified, is an error, so that `timestampz` is caught.
- **Cost for a project without overrides.**  No compilation lookup runs when no level sets the override.

## What exists today

As of the merge of 5.1:

- For each claimed file, the usable entries keyed by query name, from 5.1.  A `PostgresType` holds `Name`, `Kind`, `Schema`, `InternalName`, the facets `Length`, `Precision` and `Scale`, and `Element`, `Base`, `Labels` or `Subtype` by its kind.  A parameter's type also holds `BindName`, which a model does not need.
- The generator has one probe of the compilation, `SupportedFramework`, a `Select` over `CompilationProvider` that gives a `bool`; the epic describes the driver probe as one more such `Select`.
- Settings: `SettingsLevel`, `SettingId`, `SettingsLevelReader`, `MSBuildSettings`, `QuerySettings.Resolve`, and the marker scopes of the parser.  `SQLSRC014` reports an invalid MSBuild value, `SQLSRC111` an invalid marker value and `SQLSRC112` a conflict in one scope.  A value written in MSBuild is cut by the compiler at its first `#` or `;`, which `docs/manual/settings.md` says under "Values written in MSBuild"; the trim targets turn line breaks into spaces.
- `tests/SqlSource.Tests/Package/BuildFileTests.cs` lists the settings that the props show to the compiler, as properties and as metadata, and fails when they differ.
- No code maps a PostgreSQL type to a C# type.  Npgsql 10.0.3 is pinned in `Directory.Packages.props`.

## The base map

The key is the type's `InternalName`, `pg_type.typname`.  A type of `pg_catalog` is matched by its key alone.  A type of an extension, `citext`, `hstore`, `ltree`, `lquery`, `ltxtquery` and `cube`, is matched by its key in any schema, since Npgsql matches it by name, wherever the extension was created.

"8/9" is what Npgsql 8 and 9 box, which are identical for every type; "10" is filled in only where 10 differs.  A cell of `—` is a type Npgsql cannot box, `GetValue` throwing "Reading as 'System.Object' is not supported", which is `SQLSRC246` (below).  "Value" says whether the C# type is a value type; "Npgsql" whether it is a type of the `NpgsqlTypes` namespace, which needs the driver referenced.

| Key | Spelled | 8/9 | 10 | Value | Npgsql | Notes |
|----|----|----|----|----|----|----|
| `bool` | `boolean` | `bool` | | yes | | |
| `int2` | `smallint` | `short` | | yes | | |
| `int4` | `integer` | `int` | | yes | | |
| `int8` | `bigint` | `long` | | yes | | |
| `float4` | `real` | `float` | | yes | | |
| `float8` | `double precision` | `double` | | yes | | |
| `numeric` | `numeric(p,s)` | `decimal` | | yes | | `NaN` and the infinities throw on read; `BigInteger` is an override |
| `money` | `money` | `decimal` | | yes | | |
| `text`, `varchar`, `bpchar`, `name`, `refcursor` | | `string` | | no | | `char(1)` is a `string` too |
| `char` | `"char"` | `char` | | yes | | The one-byte internal type |
| `citext` | extension | `string` | | no | | |
| `bytea` | `bytea` | `byte[]` | | no | | |
| `date` | `date` | `DateTime` | `DateOnly` | yes | | `Kind` `Unspecified` on 8 and 9 |
| `time` | `time without time zone` | `TimeSpan` | `TimeOnly` | yes | | |
| `timetz` | `time with time zone` | `DateTimeOffset` | | yes | | |
| `timestamp` | `timestamp without time zone` | `DateTime` | | yes | | `Kind` `Unspecified` |
| `timestamptz` | `timestamp with time zone` | `DateTime` | | yes | | `Kind` `Utc`; an infinity reads as `MaxValue` or `MinValue` with `Kind` `Unspecified` |
| `interval` | `interval` | `TimeSpan` | | yes | | A value with months throws on read; `NpgsqlTypes.NpgsqlInterval` is an override |
| `uuid` | `uuid` | `Guid` | | yes | | |
| `json`, `jsonb`, `jsonpath`, `xml` | | `string` | | no | | Phase 7's library map can change `json` and `jsonb` |
| `inet` | `inet` | `System.Net.IPAddress` | | no | | The netmask is dropped; `NpgsqlTypes.NpgsqlInet` is an override |
| `cidr` | `cidr` | `NpgsqlTypes.NpgsqlCidr` | `System.Net.IPNetwork` | yes | 8/9 | `NpgsqlCidr` is obsolete on 10 |
| `macaddr`, `macaddr8` | | `System.Net.NetworkInformation.PhysicalAddress` | | no | | |
| `bit`, `varbit` | `bit(n)`, `bit varying(n)` | `bool` when the length is 1, `System.Collections.BitArray` otherwise | | by the type | | Decided by the facet; a type with no length is a `BitArray` |
| `point`, `line`, `lseg`, `box`, `path`, `polygon`, `circle` | | `NpgsqlTypes.NpgsqlPoint` and so on, one type each | | yes | yes | |
| `tsvector` | `tsvector` | `NpgsqlTypes.NpgsqlTsVector` | | no | yes | |
| `tsquery` | `tsquery` | `NpgsqlTypes.NpgsqlTsQuery` | | no | yes | The base class; a value is a subclass |
| `oid`, `xid`, `cid`, `regtype`, `regconfig` | | `uint` | | yes | | |
| `xid8` | `xid8` | `ulong` | | yes | | |
| `tid` | `tid` | `NpgsqlTypes.NpgsqlTid` | | yes | yes | |
| `pg_lsn` | `pg_lsn` | `NpgsqlTypes.NpgsqlLogSequenceNumber` | | yes | yes | |
| `int2vector` | `int2vector` | `short[]` | | no | | |
| `oidvector` | `oidvector` | `uint[]` | | no | | |
| `hstore` | extension | `System.Collections.Generic.Dictionary<string, string?>` | | no | | |
| `ltree`, `lquery`, `ltxtquery` | extension | `string` | | no | | |
| `cube` | extension | — | `NpgsqlTypes.NpgsqlCube` | yes | yes | `SQLSRC246` on 8 and 9 |
| `int4range`, `int8range`, `numrange`, `tsrange`, `tstzrange` | | `NpgsqlTypes.NpgsqlRange<T>` of the subtype's type | | yes | yes | `tstzrange` is `NpgsqlRange<DateTime>` |
| `daterange` | `daterange` | `NpgsqlTypes.NpgsqlRange<DateTime>` | `NpgsqlTypes.NpgsqlRange<DateOnly>` | yes | yes | |
| The built-in multiranges | | `NpgsqlTypes.NpgsqlRange<T>[]` of the range's `T` | as the range, `DateOnly` for `datemultirange` | no | yes | |
| `regclass`, `regproc`, `regprocedure`, `regoper`, `regoperator`, `regrole`, `regnamespace`, `regcollation`, `regdictionary` | | — | — | | | `SQLSRC246`, whose help says to cast to `oid` |
| `pg_snapshot`, `txid_snapshot`, `pg_node_tree` | | — | — | | | `SQLSRC246` |
| `record`, `unknown`, `void` | | — | — | | | The tool refuses these (`SQLSRC229`), so they reach the generator only in a sidecar edited by hand: `SQLSRC246` |

By kind, for a type that is not a key of the table:

| Kind | C# type |
|----|----|
| `array` | The element's type, by this map and the override, with `[]` |
| `domain` | Its base's type, by this map and the override |
| `enum` | `string`.  An enum array is `string[]` |
| `range` of a subtype the table does not list, `multirange` of such a range | `SQLSRC246`, whose help gives the override, `NpgsqlTypes.NpgsqlRange<T>`, and `EnableUnmappedTypes()` |
| `composite`, which includes a table's row type | `SQLSRC246` |
| A kind this version does not know | `SQLSRC246`, as the sidecar format's reader rule 4 decides |

Every C# type is written `global::`-qualified, or as a C# keyword, so that a model compiles in any namespace.

### What the research found against the epic

The map was researched against the drivers for this spec.  Three findings change what the epic's Types in C# section says, and the epic outline is updated with them:

1. **An enum reads as `string` with no opt-in, on 8, 9 and 10.**  The epic said it needs `EnableUnmappedTypes()`.  It does not: Npgsql's base resolver reads any enum as text.  `EnableUnmappedTypes()` maps an enum to a C# enum without `MapEnum<T>()`, and makes a user-defined range readable.  The manual's note on enums is dropped, and the note on user-defined ranges added.
2. **`GetFieldType` is not the boxed type of an array.**  It is `System.Array` for every array, where `GetValue` gives `int[]`.  The rule of the map is the run-time type of `GetValue`'s value, which is what Dapper assigns.
3. **An array of a value type that holds a `NULL` element throws on `GetValue`** under Npgsql's default `Array Nullability Mode=Never`.  The epic said that such an array needs `T?[]`, which the override gives; it also needs the connection string's `Array Nullability Mode=Always` or `PerInstance` for Dapper to read it at all.  The manual says both.

Also from the research, for the manual: a multidimensional array boxes as `int[,]`, which the sidecar cannot tell from `int[]`, so a `T[]` model fails on such data; the AppContext switch `Npgsql.EnableLegacyTimestampBehavior` and Npgsql 10's legacy date and time resolver change what Npgsql boxes, and the generator cannot see either; and `NpgsqlSlimDataSourceBuilder` reads fewer types until its `Enable...()` calls are made.

### The probe

One `Select` over `CompilationProvider`, giving a value-equal record:

| Member | From |
|----|----|
| `Referenced` | `Npgsql.NpgsqlConnection` is found, which every version has |
| `AtLeast8` | `Npgsql.NpgsqlSlimDataSourceBuilder` is found, a type that first appeared in 8.0, on every target framework of 8 |
| `AtLeast10` | `NpgsqlTypes.NpgsqlCube` is found, which first appeared in 10.0 and is a row of the map |
| `Version` | The referenced `Npgsql` assembly's version, for the diagnostics alone |

The epic named `NpgsqlDataSourceBuilder.EnableUnmappedTypes` as the marker of 8; the research confirmed it and found `NpgsqlSlimDataSourceBuilder`, also first in 8.0, which is one type lookup rather than a type and a member.  Either is right; the type is the one taken.  `NpgsqlTypes.NpgsqlCidr`, also new in 8, is avoided, since it is obsolete in 10.

The map is chosen by the probe: the 10 column when `AtLeast10`, the 8/9 column otherwise.  A project that does not reference Npgsql has no driver to choose by, and is never given either column silently: its models may be read by an application on any of the three, and a `DateOnly` model read on Npgsql 9, which boxes a `DateTime`, fails at run time.  So, without Npgsql, a type whose row has a cell in the 10 column, `date`, `time`, `cidr`, `daterange`, `datemultirange`, and an array, a domain or a range whose type comes from one of them, has a C# type only when the type override names its key, which is the project's statement of the driver it runs on: `date=System.DateOnly` or `date=System.DateTime`.  Without one it is `SQLSRC249` at the query.  Every other row is the same on the three and is mapped as the table says.

It runs on every compilation, as `SupportedFramework` does, and costs three type lookups and one reference's identity; its value is equal from one edit to the next, so nothing after it runs again.

### The driver's diagnostics

From the epic, as it applies to a phase with models and no methods:

| Condition | Diagnostic | Where |
|----|----|----|
| Npgsql is referenced, and is older than 8 | `SQLSRC247`, error: names the version found and the floor | No position, once for the project, when a query of the project needs a `postgres` entry |
| Npgsql is referenced, and its assembly's major is above 10 | `SQLSRC248`, information, not a warning, since `TreatWarningsAsErrors` is common: the generator maps by what it knows of 10 | No position, once, as above |
| Npgsql is not referenced, and a parameter's or a column's type is one of the `NpgsqlTypes` namespace, or one whose C# type differs between Npgsql 8 and 9 and Npgsql 10, with no override for its key | `SQLSRC249`, error: names the column or parameter and its type, and says to reference the driver or, for a type that differs between versions, to map it with the override | At the query |

Under `SQLSRC247` the models are still emitted, with the 8/9 column, as the epic decides.  Under `SQLSRC249` the query has no types, and so no models.  `CodeGen` needs the driver whatever the types: that is phase 8's error, since until phase 8 `CodeGen` behaves as `Models`.

## The type override

### The setting

| Place | Written as |
|----|----|
| The project's property | `<SqlSourceTypeOverrides>timestamptz=System.DateTimeOffset public.user_status=MyApp.UserStatus</SqlSourceTypeOverrides>` |
| A file's item metadata | `<AdditionalFiles Update="Legacy/**/*.sql" SqlSourceTypeOverrides="timestamptz=System.DateTime date=System.DateOnly" />` |
| A file's header | `-- type-override: numeric = System.Double` |
| A query | `-- type-override: timestamptz = default` |

There is no attribute level: a dictionary on an attribute is awkward, the owner decided.  The epic outline's settings table gains the row, and the project-only row loses the two date and time options.

An MSBuild value is a list of entries separated by white space, line breaks included, so a property can hold one entry per line:

```xml
<SqlSourceTypeOverrides>
  timestamptz=System.DateTimeOffset
  public.user_status=MyApp.UserStatus
  public.user_id=MyApp.UserId
  int4[]=System.Collections.Generic.List&lt;int?&gt;
</SqlSourceTypeOverrides>
```

A marker holds exactly one entry; several markers in one scope add up, as `-- param:` and `-- token:` do.

### The grammar

One parser, in `Settings/`, for the MSBuild values and the markers alike:

```
list    := entry (WS entry)*            an MSBuild value; WS is spaces or line breaks
marker  := entry                         one entry per -- type-override: line
entry   := key WS? "=" WS? (ctype | "default")
key     := [part "."] part ["[]"]        part: letters, digits, _ and $; no quotes, spaces or facets
ctype   := ["global::"] name ("." name)* rank*   |   keyword rank*
name    := ident ["<" arg ("," arg)* ">"]
arg     := ctype ["?"]
rank    := "[" ","* "]"
keyword := bool byte sbyte short ushort int uint long ulong nint nuint char float double decimal string object
```

- White space counts only inside `<>` or `[]`; anywhere else it separates entries.  Around `=` it is allowed in a marker.
- `?` is allowed on a type argument and before a rank, `int?[]`, `List<int?>`, and never at the end of the whole type: the nullability of a property is its column's, from the description and the `!` and `?` suffixes.
- A C# type is fully qualified or a keyword.  An unqualified name that is not a keyword is an error: the generator cannot see a file's `using`s, and the model is emitted in a namespace of its own choosing.
- Rejected, each an invalid value: a `?` at the end, a tuple, `dynamic`, `void`, a pointer, and any text left after the entry.  Rejecting the text that is left keeps room for a later, additive form, a converter's read and write for phase 7 and 8.
- `;` and `#` cannot be in a value, since the compiler cuts an MSBuild value at either; the grammar needs neither.  A generic's `<` and `>` are escaped in the project file, as XML needs, which the manual shows.

An invalid entry is `SQLSRC014` in an MSBuild value, once per distinct setting and value, and `SQLSRC111` in a marker; the other entries of the value still apply.  `SQLSRC014`'s and `SQLSRC111`'s sections in the manual name the setting.

### The merge

**Per key, most specific first.**  The map in effect for a query starts from the property's; the file's metadata, then the file header's markers, then the query's markers each replace only the keys they name.  `key=default` takes the key out of the map, so that the base map, and from phase 7 a library map, answers for it.  There is no reset of the whole map.

This is an exception to the rule that a level replaces the one below it whole, which the generator parameters follow: the owner's case, a project that maps `timestamptz` to `DateTimeOffset` everywhere and one query that needs `DateTime`, would otherwise have to restate every mapping of the project in that query.  The manual's list of exceptions in `settings.md`, "Which place wins", gains it: a type override replaces only the types it names.

- Two values for one key in one MSBuild value: the last wins, so that `$(SqlSourceTypeOverrides) timestamptz=...` in a `Directory.Build.props` works as a user expects.
- Two different values for one key in one marker scope: `SQLSRC112`.  The same value twice is not an error, as with the other markers.

`SettingsLevel` gains the override as an `EquatableArray` of entries sorted by key, ignoring case, and `QuerySettings.Resolve` merges it where it takes every other setting from the most specific level.  A query with no marker of its own has the map of its file, so the merged map is computed once per file, not per query.

### The keys

1. A key is a type's one-word catalog name, `InternalName`: `timestamptz`, `int4`, `float8`.  Each engine's one-word aliases are accepted for the built-in types, `integer`, `int`, `smallint`, `bigint`, `real`, `float`, `boolean`, `decimal`, by a list in the map.  A spelling of several words, `timestamp with time zone`, is not a key; `SQLSRC014`'s and `SQLSRC111`'s sections say to write the one-word form, which every built-in type has.
2. A type outside the engine's system schema is schema-qualified, `public.user_status`, as the sidecar writes it.  `pg_catalog.int4` is the same key as `int4`.  The extension types of the base map are the exception, since the base map matches them by name in any schema: `citext` matches a `citext` of any schema, and `public.citext` matches the one of `public` alone, the qualified key winning over the bare one for that schema.
3. An unqualified key must be a type the base map of some engine knows, or an alias of one: `timestampz` and an unqualified `user_status` are invalid values, with help that says a type of another schema is written with its schema.  A qualified key is never checked against the map, so that a catalog type the map does not know can still be named, `pg_catalog.newtype`.  The lists of known names live where both the settings and the parser can read them.
4. Keys compare ignoring case, on every engine.  Two types of one schema whose names differ only by case cannot be told apart; `docs/tech-debt` records it.
5. Facets are not part of a key: `numeric` matches `numeric(18,2)`.
6. How a type is looked up, for a type `t`: the override's entry for `t`'s key; else, for an array, `t`'s element looked up the same way, with `[]`; for a domain, its base looked up the same way; else the base map.  So an array's own key, `int4[]` or `_int4`, wins over its element's, and a domain's key wins over its base, while mapping a base also maps every domain over it.  A range and a multirange have only their own key: a mapping of the subtype does not reach them.

The keys are engine-neutral: SQL Server's will be its type names, `datetime2`, and `schema.name` for an alias type, which phase 6 confirms.  A strongly-typed id is a domain on PostgreSQL and an alias type on SQL Server, mapped by its key.  A project of both engines scopes an entry to one engine's files by their metadata; a prefix of the engine on a key was weighed and not taken.

### Resolving the C# types

A named step takes the distinct C# types that any level names, from the project's settings, the files' settings and the parsed files' markers, as a sorted, value-equal list, combines it with the compilation, and gives for each a value-equal record: found or the reason it is not, whether it is a value type, and its accessibility.  It holds no symbol.  A keyword is its special type.  A named type is looked up by its metadata name, ``A.B+C`1``, tried from the longest namespace prefix down so that a nested type is found, with `GetTypesByMetadataName`, the compilation's own assembly first; its type arguments and ranks are resolved the same way.

An empty list gives an empty result at once, so a project without overrides pays nothing.  The step runs on every edit of C#, as the probe does, at the cost of a few lookups per named type, and its value is equal from one edit to the next, so nothing after it runs again unless a type appeared or went.

| Condition | Diagnostic | Where |
|----|----|----|
| No type of the name and arity is found, or several are, across references | `SQLSRC250`, error: names the type and the place that wrote it | At each query that uses the mapping |
| The type is not accessible from the compilation | `SQLSRC251`, error | As above |
| The type cannot be a property's type: a static class, a `ref struct`, an open generic, `System.Void`, or `System.Nullable<T>` as the whole type, which a nullable column would make `Nullable<T>?` (`CS0453`) | `SQLSRC252`, error | As above |

A mapped type marked `[Obsolete]` is the user's choice: the model's file disables `CS0612` and `CS0618` for it, and no other warning, so that the user's own uses of the type warn as they would anyway.

The diagnostic is at the query, not at the setting, because the query is what the error stops, and because an MSBuild value has no position.  A mapping that no query uses is not an error, so that a project can set its mappings before it has the queries; sqlc does the same.  A query with any of these errors has no types, so no model is emitted and no `CS0246` appears in generated code.  Sub-phase 5.3 adds the case of a type less accessible than its model to `SQLSRC251`.

### Precedence

For one parameter or column, the first that answers: the merged override; from phase 7, a library map; the base map under the probe.

## The types of a query

For each query that needs an entry and has a usable one, the generator gives each parameter and column, in order, the C# type text, whether it is a value type, its accessibility and the key it was found by, or the query's diagnostics.  The record is per file and query, not per type, since nothing a type sets changes a type: there is no attribute level.  Its diagnostics are reported from the per-file output of sub-phase 5.1, once per file.  Nothing reads the types yet but the tests and sub-phase 5.3.

A parameter whose `type` is `null`, which no engine in scope writes, is `SQLSRC246`.

## Diagnostics

| Id | Severity | Where | About |
|----|----|----|----|
| `SQLSRC246` | Error | The query | A type the map cannot give a C# type, naming the column or parameter and the type; the help gives the override, or the cast where the table says |
| `SQLSRC247` | Error | None, once | Npgsql older than the floor |
| `SQLSRC248` | Information, configurable as sub-phase 5.1's two warnings are | None, once | Npgsql newer than the generator knows |
| `SQLSRC249` | Error | The query | With no Npgsql referenced, a type of `NpgsqlTypes`, or one that differs between Npgsql versions and has no override |
| `SQLSRC250` | Error | The query | A mapped type that is not found or is ambiguous |
| `SQLSRC251` | Error | The query | A mapped type that is not accessible |
| `SQLSRC252` | Error | The query | A mapped type that cannot be a property's type |

The ids follow sub-phase 5.1's, which end at `SQLSRC245`.  If 5.1 merged with more or fewer, this sub-phase takes the next free ids in this order and the plan says so, and sub-phase 5.3's spec follows.

## The build files

`SqlSourceTypeOverrides` joins the settings of `SqlSource.props`, as a `CompilerVisibleProperty` and a `CompilerVisibleItemMetadata` on `SqlSourceSettingsFile`, trimmed and collected by the targets as the other settings are, and `BuildFileTests`' list.  The tool does not read the setting's MSBuild values, since it changes nothing that is described; the project manifest carries it as it carries every setting the props list, and the tool's planner leaves it alone.  The marker is the parser's, which the tool shares, so an invalid `-- type-override:` is a parse error for the tool as for the build, `SQLSRC111`, and the tool leaves that file out as it leaves out every file with a parse error.

## Testing

| Where | Cases |
|----|----|
| `PostgresTypeMapTests` | Every row of the base map, under each of the probe's three states, with its C# text and value-type flag.  Each kind: an array of a mapped type, of an array's own row, of a domain and of an enum; a domain over a domain; a range of a subtype the map lists and one it does not; a multirange; a composite; an unknown kind.  `bit(1)`, `bit(8)`, `bit` with no length, `varbit(1)` |
| `NpgsqlProbeTests` | The probe against metadata references to Npgsql 8, 9 and 10, which `tests/SqlSource.Tests` downloads as packages without referencing them, and against none.  Each of `SQLSRC247`, `SQLSRC248` and `SQLSRC249`.  With no Npgsql, each type that differs between versions is `SQLSRC249` without an override and its override's type with one, its array and its domain alike, and a type that is the same on all three is mapped |
| `TypeOverrideGrammarTests` | Every form of the grammar, valid and invalid: keywords, generics, nested types, ranks, `?` inside and at the end, `global::`, aliases, a qualified and an unqualified key, `default`, white space and line breaks |
| `TypeOverrideLevelTests` | The merge per key at each level; `default` at each level; the last of two in one MSBuild value; `SQLSRC112` for two in one marker scope; the array, domain and range lookup rules |
| `MappedTypeTests` | A keyword; a type of the project; a type of a reference; a nested type; a generic of each; not found; ambiguous; internal in a reference; a static class; a `ref struct`; an open generic |
| `CachingTests` | A C# edit runs the probe and the resolution of mapped types, and no step after them.  A project with no override runs no lookup.  A `.sql` edit that leaves its markers alone resolves no mapped type again |
| `BuildFileTests` | The new setting in the props and the targets |
| `tests/SqlSource.Tests.RoslynFloor` | As today: the steps use no API newer than Roslyn 4.8.0, which has `GetTypesByMetadataName` and `IsSymbolAccessibleWithin` |

The run-time proof of the map is sub-phase 5.4's, which reads every row through Dapper, on the pinned Npgsql and on 8.

## Documentation

- `docs/manual/postgresql.md`: the Types section, the base map as the user sees it: the database type, its C# type on Npgsql 8 and 9 and on 10, and the notes above that a user needs, the infinities, `NaN`, intervals with months, arrays with `NULL` elements and `Array Nullability Mode`, multidimensional arrays, the slim builder, the legacy switches, a user-defined range.  Its callouts on types replaced.
- `docs/manual/settings.md`: a row for `SqlSourceTypeOverrides`, its section with the four places, the grammar as a user writes it, the merge with `default`, and the exception added to "Which place wins".  The row's status says the setting is checked and takes effect with models in a later release, until sub-phase 5.3 flips it.
- `docs/manual/sql-files.md`: `-- type-override:` in the marker index.
- `docs/manual/diagnostics.md`: a section for each new id; `SQLSRC014`, `SQLSRC111` and `SQLSRC112` name the new setting where they list settings.
- The README: "Custom type mapping" becomes the override, and the "What works today" row for it says what works.
- `docs/deferred/D-0003-custom-type-conversion.md`: the model's type is now covered by the override; a conversion with read and write expressions stays deferred.
- `docs/tech-debt`: a new item for keys that differ only by case.
- `docs/architecture/generator.md`: the probe, the map, the override's settings and the resolution of mapped types, in the sections each belongs to.
- The epic outline, in this spec's pull request: the settings table; Types in C#, for the enum, the arrays and the dropped date and time options, which the override replaces; the probe's markers, now verified.  In the implementation's pull request, the 5.2 row's status.

## Order of work

One pull request, one commit for each step:

1. The epic's 5.2 row says In progress.  The base map and its tests.
2. The probe, its references to the three drivers, and its diagnostics.
3. The override's grammar, its setting at each level, the build files and the merge.
4. The resolution of mapped types, with its diagnostics and caching tests.
5. The types of a query, from the usable entries, with their diagnostics.
6. The documents, and the epic's row set to Done.

## Left to the builder

- The steps and records, within the caching rules.
- The list of one-word aliases, from PostgreSQL's table of type names.
- How the tests obtain the Npgsql 8, 9 and 10 assemblies as metadata references.
- Whether the element of a `bit(1)[]` carries its length in the sidecar, so that it maps to `bool[]`; if it does not, the array maps to `BitArray[]` and the manual says so.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- The marker of Npgsql 8 is `NpgsqlSlimDataSourceBuilder`, not `NpgsqlDataSourceBuilder.EnableUnmappedTypes`.
- A project without Npgsql maps every type that is the same on the three drivers, and needs the override for one that differs, which the review of this spec asked for in place of mapping it by the 10 column.
- A user-defined range is `SQLSRC246`, with the override as its help, since the type Npgsql boxes for it exists only after an opt-in.
- The extension types are matched by their name in any schema, by the base map and by an unqualified key alike.
- An `[Obsolete]` mapped type is accepted, and the model's file disables the two obsolete warnings.
- A mapping that no query uses is not an error.
- The override's names, `SqlSourceTypeOverrides` and `-- type-override:`, the dropping of both date and time options, no engine prefix on a key, and a type less accessible than its model being an error were proposed with the design and left standing by the owner.

## Research

Every row of the base map was read from a live server: Npgsql 8.0.3, 9.0.3 and 10.0.3 from the NuGet cache, on `net10.0`, against `postgres:18`, through a plain `NpgsqlDataSourceBuilder`, recording `GetDataTypeName`, `GetFieldType` and the type of `GetValue`'s value for each type, with and without `EnableUnmappedTypes()`.  The results were checked in Npgsql's source at the 8.0.0, 9.0.0 and 10.0.0 releases: `src/Npgsql/Internal/ResolverFactories/AdoTypeInfoResolverFactory.cs` and its range and multirange parts, `NetworkTypeInfoResolverFactory.cs`, `UnmappedTypeInfoResolverFactory.cs`, `UnsupportedTypeInfoResolver.cs`, `CubeTypeInfoResolverFactory.cs` on 10, and `src/Npgsql/NpgsqlDataSourceBuilder.cs`.  The probe's markers were checked in the compiled metadata of 6.0.11, 8.0.1, 8.0.3, 9.0.3, 10.0.3 and 11.0.0-preview.1.  Sources: [supported types](https://www.npgsql.org/doc/types/basic.html), release notes [8.0](https://www.npgsql.org/doc/release-notes/8.0.html), [9.0](https://www.npgsql.org/doc/release-notes/9.0.html) and [10.0](https://www.npgsql.org/doc/release-notes/10.0.html).

The override's prior art: sqlc's [overrides](https://docs.sqlc.dev/en/latest/howto/overrides.html), SQLx's [type overrides](https://docs.rs/sqlx-core/0.9.0/sqlx_core/config/macros/struct.Config.html), kysely-codegen's [type mapping](https://newreleases.io/project/npm/kysely-codegen/release/0.19.0), and jOOQ's [forced types](https://www.jooq.org/doc/dev/manual/code-generation/codegen-advanced/codegen-config-database/codegen-database-forced-types/codegen-database-forced-types-matching/).  That a value written in MSBuild reaches the generator cut at `#` and `;`, and whole with `=`, `,`, `<>`, `[]` and spaces, was verified with a generator built for it on SDK 10.0.401.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.
