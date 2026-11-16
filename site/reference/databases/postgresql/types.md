# PostgreSQL: types

<a id="type-map"></a>

## Types

```sql postgres
-- name: GetOrder
SELECT id, placed_at, total, tags, born
FROM orders
WHERE id = @id;
```

Each column and each parameter of a described PostgreSQL query gets a C# type.  It is the type that Npgsql reads a value of the PostgreSQL type as.  Suppose that the columns of the sample have these types.  Then they get these C# types:

- `id`, a `bigint`, is a `long`.  The parameter `@id` is a `long` too.
- `placed_at`, a `timestamp with time zone`, is a `System.DateTime`.
- `total`, a `numeric(12,2)`, is a `decimal`.
- `tags`, a `text[]`, is a `string[]`.
- `born`, a `date`, is a `System.DateTime` on Npgsql 8 and 9, or a `System.DateOnly` on Npgsql 10.

The type of a column does not decide whether the column can be null.  See [Which columns can be null](nullability.md#nullability).

The build gives these types to each query whose output is `repository`, `methods` or `models`, and whose entry in its sidecar is current.  They are the types of the properties of the [models](index.md#models) of the query.

- **The build reports an error for a column or a parameter that has no C# type.**  For example, a type that Npgsql cannot read is [SQLSRC246](../../../diagnostics.md#sqlsrc246).  A query with such an error has no models.
- **The [type override](../../type-mapping.md#setting-type-overrides) gives a type another C# type.**  For example, `"timestamptz": "System.DateTimeOffset"` in the `typeOverrides` of a settings file.
- **A [column type](../../model-properties.md#setting-column-types) gives one column another C# type.**  For example, `"users.id": "MyApp.UserId"` in the `columnTypes` of a settings file.
- **The [`-- param:` marker](../../parameters.md#param-marker) gives one query parameter another C# type.**  For example, `-- param: @id -> MyApp.UserId`.
- **A type of yours that Npgsql does not read needs [converters](../../converters.md#converters).**  A query method reads and sends the C# type that you give as it is.  So a type such as an id, or an `Email` over a domain, needs converters to and from the C# type of the table below.
- **A project that references `Npgsql.NodaTime` gets the types of NodaTime for the date and time types.**  This holds for its queries whose output is `repository` or `methods`.  See [NodaTime](libraries.md#nodatime).
- **A project that references `Npgsql.NetTopologySuite` gets the types of NetTopologySuite for the types of PostGIS.**  This holds for its queries whose output is `repository` or `methods`.  See [NetTopologySuite](libraries.md#nettopologysuite).
- **A project can read its JSON columns as the types of System.Text.Json, or as classes of its own.**  See [JSON](libraries.md#json).

<a id="which-npgsql"></a>

## Which version of Npgsql

Npgsql 10 reads a few types as other C# types than Npgsql 8 and 9 do.  So the C# types follow the `Npgsql` package that the project references, directly or through another package.

- **Npgsql 8, 9 or 10 gets the column of the table below for its version.**  If you update Npgsql from 9 to 10, the C# types of `date`, `time`, `cidr`, `daterange` and `datemultirange` change.  Npgsql 10 reads them as other types.
- **The supported versions of Npgsql** are in [Supported versions](../index.md#supported-versions).  An older Npgsql is [SQLSRC247](../../../diagnostics.md#sqlsrc247).  Update it.
- **Npgsql 11 or later gets the types of Npgsql 10.**  The build reports the information [SQLSRC248](../../../diagnostics.md#sqlsrc248), since this version of SqlSource does not know a newer Npgsql.
- **A project with no Npgsql gets every type that Npgsql 8, 9 and 10 read alike, and models alone.**  An example is a library that holds the queries and leaves the driver to the application.  Give its queries the output `models`.  A query method needs the driver: without it, a query whose output is `repository` or `methods` is [SQLSRC273](../../../diagnostics.md#sqlsrc273).
- **Without Npgsql, each of these types is [SQLSRC249](../../../diagnostics.md#sqlsrc249), unless the type override names it.**  The type has no C# type without the driver.
  - A type that Npgsql 8, 9 and 10 read differently.
  - A type of the `NpgsqlTypes` namespace, which is Npgsql's own.
- **For a type that differs, the type override says which Npgsql the application runs on.**  For Npgsql 10, write `"date": "System.DateOnly"` and `"time": "System.TimeOnly"` in the `typeOverrides` of a settings file.

<a id="type-table"></a>

## The types

The table names each type as the catalog of PostgreSQL names it.  These names are the keys of the [type override](../../type-mapping.md#setting-type-overrides).  The other spellings that PostgreSQL accepts are in parentheses.

- **A type of your own schema is written in a key with its schema**, as `public.user_status`.  If the schema or the name was created in double quotes, it keeps its quotes in the key: `public."Order Status"`.
- **The length, precision or scale of a type does not change its C# type**, except for `bit` and `varbit`.
- **A cell "Same" is the C# type of Npgsql 8 and 9.**

| PostgreSQL type | Npgsql 8 and 9 | Npgsql 10 | Without Npgsql |
|----|----|----|----|
| `bool` (`boolean`) | `bool` | Same | Same |
| `int2` (`smallint`) | `short` | Same | Same |
| `int4` (`integer`) | `int` | Same | Same |
| `int8` (`bigint`) | `long` | Same | Same |
| `float4` (`real`) | `float` | Same | Same |
| `float8` (`double precision`) | `double` | Same | Same |
| `numeric` (`numeric(p,s)`, `decimal`), `money` | `decimal` | Same | Same |
| `text`, `varchar` (`character varying`), `bpchar` (`character`, `char(n)`), `name`, `refcursor`, `citext` | `string` | Same | Same |
| `char` (`"char"`, the one-byte type) | `char` | Same | Same |
| `bytea` | `byte[]` | Same | Same |
| `date` | `System.DateTime` | `System.DateOnly` | By the type override only |
| `time` (`time without time zone`) | `System.TimeSpan` | `System.TimeOnly` | By the type override only |
| `timetz` (`time with time zone`) | `System.DateTimeOffset` | Same | Same |
| `timestamp` (`timestamp without time zone`), `timestamptz` (`timestamp with time zone`) | `System.DateTime` | Same | Same |
| `interval` | `System.TimeSpan` | Same | Same |
| `uuid` | `System.Guid` | Same | Same |
| `json`, `jsonb`, `jsonpath`, `xml` | `string` | Same | Same |
| `inet` | `System.Net.IPAddress` | Same | Same |
| `cidr` | `NpgsqlTypes.NpgsqlCidr` | `System.Net.IPNetwork` | By the type override only |
| `macaddr`, `macaddr8` | `System.Net.NetworkInformation.PhysicalAddress` | Same | Same |
| `bit(1)`, `varbit(1)` (`bit varying(1)`) | `bool` | Same | Same |
| `bit(n)`, `varbit(n)`, and either with no length | `System.Collections.BitArray` | Same | Same |
| `point`, `line`, `lseg`, `box`, `path`, `polygon`, `circle` | `NpgsqlTypes.NpgsqlPoint`, `NpgsqlTypes.NpgsqlLine`, `NpgsqlTypes.NpgsqlLSeg`, `NpgsqlTypes.NpgsqlBox`, `NpgsqlTypes.NpgsqlPath`, `NpgsqlTypes.NpgsqlPolygon`, `NpgsqlTypes.NpgsqlCircle` | Same | SQLSRC249 |
| `tsvector`, `tsquery` | `NpgsqlTypes.NpgsqlTsVector`, `NpgsqlTypes.NpgsqlTsQuery` | Same | SQLSRC249 |
| `oid`, `xid`, `cid`, `regtype`, `regconfig` | `uint` | Same | Same |
| `xid8` | `ulong` | Same | Same |
| `tid`, `pg_lsn` | `NpgsqlTypes.NpgsqlTid`, `NpgsqlTypes.NpgsqlLogSequenceNumber` | Same | SQLSRC249 |
| `int2vector`, `oidvector` | `short[]`, `uint[]` | Same | Same |
| `hstore` | `System.Collections.Generic.Dictionary<string, string?>` | Same | Same |
| `ltree`, `lquery`, `ltxtquery` | `string` | Same | Same |
| `cube` | None: [SQLSRC246](../../../diagnostics.md#sqlsrc246).  Update Npgsql to 10, or cast the column to `text` in the query | `NpgsqlTypes.NpgsqlCube` | SQLSRC249 |
| `int4range`, `int8range`, `numrange` | `NpgsqlTypes.NpgsqlRange<int>`, `NpgsqlTypes.NpgsqlRange<long>`, `NpgsqlTypes.NpgsqlRange<decimal>` | Same | SQLSRC249 |
| `tsrange`, `tstzrange` | `NpgsqlTypes.NpgsqlRange<System.DateTime>` | Same | SQLSRC249 |
| `daterange` | `NpgsqlTypes.NpgsqlRange<System.DateTime>` | `NpgsqlTypes.NpgsqlRange<System.DateOnly>` | SQLSRC249 |
| `int4multirange`, `int8multirange`, `nummultirange`, `tsmultirange`, `tstzmultirange`, `datemultirange` | An array of the range's type: `NpgsqlTypes.NpgsqlRange<int>[]` and so on | As the range: `NpgsqlTypes.NpgsqlRange<System.DateOnly>[]` for `datemultirange` | SQLSRC249 |
| `regclass`, `regproc`, `regprocedure`, `regoper`, `regoperator`, `regrole`, `regnamespace`, `regcollation`, `regdictionary` | None: SQLSRC246.  Cast the column to `oid` in the query | Same | Same |
| `pg_snapshot`, `txid_snapshot`, `pg_node_tree` | None: SQLSRC246.  Cast the column to `text` in the query | Same | Same |
| `geometry`, `geography`, PostGIS's types, with or without a modifier such as `(Point,4326)` | None: SQLSRC246.  In the query methods of a project that references `Npgsql.NetTopologySuite`, `NetTopologySuite.Geometries.Geometry`: see [NetTopologySuite](libraries.md#nettopologysuite) | Same | Same |

`citext`, `hstore`, `ltree`, `lquery`, `ltxtquery` and `cube` are types of extensions.  SqlSource knows each of them by its name, whatever schema the extension was created in.  The same holds for `geometry` and `geography` of PostGIS, but a type override names these two with that schema: `public.geometry`.

A type that the table does not name gets its C# type by its kind:

| Kind | C# type |
|----|----|
| An array | Its element's C# type with `[]`: `int[]` for `int4[]`, `string[]` for `text[]`, `string[]` for an array of an enum.  A parameter in a query method is an `IEnumerable<T>` of that element: see [array parameters](query-methods.md#array-parameters) |
| A domain | Its base type's C# type: a domain over `text` is a `string` |
| An enum | `string`.  Npgsql reads an enum as its label.  To read it as a C# enum, map it with the type override, `public.user_status=MyApp.UserStatus`, and call `MapEnum<MyApp.UserStatus>()` on the application's data source builder |
| A range or a multirange that the table does not name, a range of your own over `int4` included | None: SQLSRC246.  Map it with the type override to `NpgsqlTypes.NpgsqlRange<T>` of its subtype's C# type, and call `EnableUnmappedTypes()` on the data source builder, without which Npgsql cannot read it.  A project without Npgsql references it first, since that type is Npgsql's |
| A composite type, which includes a table's row type | None: SQLSRC246.  Map it with the type override to the type the application's data source maps it to, or select its fields as columns of their own |
| A base type of `pg_catalog` that the table does not name, such as `aclitem` | None: SQLSRC246.  No version of Npgsql reads it: cast the column to `text` in the query |
| Any other base type that the table does not name, such as one of an extension SqlSource does not know | None: SQLSRC246.  Map it with the type override to the type the application reads it as |

<a id="type-notes"></a>

## What to know about some types

- **`timestamptz` is read as a `System.DateTime` whose `Kind` is `Utc`.**  `timestamp` is read with the `Kind` `Unspecified`, and so is `date` on Npgsql 8 and 9.  `infinity` and `-infinity` in a `timestamptz` are read as `DateTime.MaxValue` and `DateTime.MinValue`, with the `Kind` `Unspecified`.
- **To read a `timestamptz` as a `System.DateTimeOffset`, map it with the type override**: `timestamptz=System.DateTimeOffset`.  A query method then reads and sends it with nothing more.  Rows that Dapper reads need a type handler: see [the recipe](../../../guides/dapper.md#dapper-with-postgresql).
- **The `numeric` values `NaN`, `Infinity` and `-Infinity` cannot be read as a `decimal`.**  Reading one throws.  Keep them out of the column, or cast the column to `float8` in the query.  They are then read as a `double`.
- **An `interval` with months or years cannot be read as a `System.TimeSpan`.**  Reading one throws.  Map the type, `interval=NpgsqlTypes.NpgsqlInterval`, or give one column that type with a [column type](../../model-properties.md#setting-column-types).  A query method then reads and sends it as an `NpgsqlInterval`.
- **`inet` is read as a `System.Net.IPAddress`, which drops the netmask.**  To keep the netmask, map the type, `inet=NpgsqlTypes.NpgsqlInet`, or give one column that type with a column type.  A query method then reads and sends it as an `NpgsqlInet`.
- **`char(n)` is a `string`, and so is `char(1)`.**  The `char` of the table is the one-byte type of PostgreSQL.  You write it as `"char"`, with its quotes.
- **`bit(1)` is a `bool`, and a `bit(1)[]` column is a `bool[]`.**  The type of a parameter has no length.  So a parameter of `bit(1)` is a `System.Collections.BitArray`.
- **An array of a value type that holds a `NULL` element needs a nullable element**, such as `int?[]`.  The type override gives it: `int4[]=int?[]`.  Npgsql also refuses to read such an array under its default `Array Nullability Mode=Never`.  So set `Array Nullability Mode=Always` or `PerInstance` in the connection string of the application.
- **An array of a reference type, such as `text[]`, is read with its `NULL` elements, whatever the mode.**  To say that an element can be null, map `text[]=string?[]`.
- **A multidimensional array is read as `int[,]`, not as `int[]`.**  The type of its column does not say how many dimensions it has.  So a column of `int4[]` that holds a two-dimensional array cannot be read into an `int[]`.  If every value of the column has two dimensions, map it: `int4[]=int[,]`.
- **The settings of the data source of the application can change what Npgsql reads.**  SqlSource cannot see them.  The table shows what a data source made with `NpgsqlDataSourceBuilder` reads, and what a plain `NpgsqlConnection` reads.  `NpgsqlSlimDataSourceBuilder` reads fewer types until its `Enable...()` methods are called.
- **The legacy behaviours of Npgsql read some date and time types as other C# types.**  They are the AppContext switch `Npgsql.EnableLegacyTimestampBehavior` and the legacy date and time resolver of Npgsql 10.  If the application uses one of them, map those types with the type override to the C# types that the application reads.
