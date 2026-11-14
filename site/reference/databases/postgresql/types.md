# PostgreSQL: types

<a id="type-map"></a>

## Types

```sql postgres
-- name: GetOrder
SELECT id, placed_at, total, tags, born
FROM orders
WHERE id = @id;
```

Each column and each parameter of a described PostgreSQL query gets a C# type: the type that Npgsql reads a value of its PostgreSQL type as.  If `id` is a `bigint`, `placed_at` a `timestamp with time zone`, `total` a `numeric(12,2)`, `tags` a `text[]` and `born` a `date`, they are `long`, `System.DateTime`, `decimal`, `string[]`, and `System.DateTime` on Npgsql 8 and 9 or `System.DateOnly` on Npgsql 10; `@id` is a `long`.  Whether a column can be null is decided in [Which columns can be null](nullability.md#nullability), not by its type.

The build gives these types to each query whose output is `models`, `methods` or `repository` and whose entry in its sidecar is current, and reports an error for a column or parameter that has none, such as [SQLSRC246](../../../diagnostics.md#sqlsrc246) for a type that Npgsql cannot read.  They are the types of the properties of the query's [models](index.md#models), and a query with such an error has no models.  To give a type another C# type, use the [type override](../../../settings.md#setting-type-overrides), `"timestamptz": "System.DateTimeOffset"` in a settings file's `typeOverrides`.  To give one column another, use a [column type](../../../settings.md#setting-column-types), `"users.id": "MyApp.UserId"` in its `columnTypes`, and one parameter, its [`-- param:` marker](../../parameters.md#param-marker), `-- param: @id -> MyApp.UserId`.  A query method reads and sends that C# type as it is, so a type of yours that Npgsql does not read, such as an id or an `Email` over a domain, needs [converters](../../../settings.md#converters) to and from the type of the table below.  A project that references `Npgsql.NodaTime` gets NodaTime's types for the date and time types of its queries whose output is `methods` or `repository`: see [NodaTime](libraries.md#nodatime).  One that references `Npgsql.NetTopologySuite` gets NetTopologySuite's for PostGIS's types: see [NetTopologySuite](libraries.md#nettopologysuite).  A project can read its JSON columns as System.Text.Json's types or as classes of its own: see [JSON](libraries.md#json).

<a id="which-npgsql"></a>

## Which version of Npgsql

A few types are read as another C# type by Npgsql 10 than by Npgsql 8 and 9, so the types follow the `Npgsql` package that the project references, directly or through another package:

- **Npgsql 8, 9 or 10** gets the column of the table below for its version.  Updating Npgsql from 9 to 10 changes the C# types of `date`, `time`, `cidr`, `daterange` and `datemultirange`, as it changes what Npgsql reads.
- **Npgsql older than 8** is [SQLSRC247](../../../diagnostics.md#sqlsrc247).  Update it.
- **Npgsql 11 or later** gets the types of Npgsql 10, with the information [SQLSRC248](../../../diagnostics.md#sqlsrc248), since this version of SqlSource does not know a newer one.
- **No Npgsql**, as in a library that holds the queries and leaves the driver to the application, gets every type that Npgsql 8, 9 and 10 read alike, and models alone: give its queries the output `models`, since a query method needs the driver ([SQLSRC273](../../../diagnostics.md#sqlsrc273)).  A type that they read differently, and a type of Npgsql's own `NpgsqlTypes` namespace, have no C# type without the driver: each is [SQLSRC249](../../../diagnostics.md#sqlsrc249), unless the type override names it.  For the types that differ, the override says which Npgsql the application runs on: `"date": "System.DateOnly"` and `"time": "System.TimeOnly"` in a settings file's `typeOverrides` for Npgsql 10.

<a id="type-table"></a>

## The types

The names are those of PostgreSQL's catalog, which are the [type override's](../../../settings.md#setting-type-overrides) keys, with the other spellings PostgreSQL accepts in parentheses.  A type of your own schema is written in a key with its schema, `public.user_status`, and a schema or a name created in double quotes keeps its quotes there: `public."Order Status"`.  A type's length, precision or scale does not change its C# type, but for `bit` and `varbit`.  A cell "Same" is the C# type of Npgsql 8 and 9.

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

`citext`, `hstore`, `ltree`, `lquery`, `ltxtquery` and `cube` are the types of extensions, and are known by their name in whatever schema the extension was created.  So are PostGIS's `geometry` and `geography`, but a type override names them with that schema, `public.geometry`.  A type that the table does not name gets its C# type by its kind:

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

- **`timestamptz`** is read as a `System.DateTime` whose `Kind` is `Utc`.  `timestamp`, and `date` on Npgsql 8 and 9, are read with the `Kind` `Unspecified`.  `infinity` and `-infinity` in a `timestamptz` are read as `DateTime.MaxValue` and `DateTime.MinValue`, with the `Kind` `Unspecified`.  To give a `timestamptz` the C# type `System.DateTimeOffset`, map it: `timestamptz=System.DateTimeOffset`.  A query method reads and sends it with nothing more; rows read through Dapper need a type handler: see [the recipe](../../../recipes.md#dapper-with-postgresql).
- **`numeric`** values `NaN`, `Infinity` and `-Infinity` cannot be read as a `decimal`, and reading one throws.  Keep them out of the column, or cast it to `float8` in the query, which reads them as a `double`.
- **An `interval` with months or years** cannot be read as a `System.TimeSpan`, and reading one throws.  Map it, `interval=NpgsqlTypes.NpgsqlInterval`, or give one column that type with a [column type](../../../settings.md#setting-column-types), and a query method reads and sends it as an `NpgsqlInterval`.
- **`inet`** is read as a `System.Net.IPAddress`, which drops the netmask.  Map it, `inet=NpgsqlTypes.NpgsqlInet`, or give one column that type with a column type, and a query method reads and sends it as an `NpgsqlInet`, which keeps the netmask.
- **`char(n)`**, `char(1)` included, is a `string`.  The `char` of the table is PostgreSQL's one-byte type, which is written `"char"` with its quotes.
- **`bit(1)`** is a `bool`, and a `bit(1)[]` column a `bool[]`.  A parameter's type has no length, so a parameter of `bit(1)` is a `System.Collections.BitArray`.
- **An array that holds a `NULL` element** needs a nullable element, `int?[]`, for an element of a value type, and the type override gives it: `int4[]=int?[]`.  Npgsql also refuses to read such an array under its default `Array Nullability Mode=Never`, so set `Array Nullability Mode=Always` or `PerInstance` in the application's connection string.  An array of a reference type, `text[]`, is read with its `NULL` elements whatever the mode; map `text[]=string?[]` to say that an element can be null.
- **A multidimensional array** is read as `int[,]` and not `int[]`, and its column's type does not say how many dimensions it has, so a column of `int4[]` that holds a two-dimensional array cannot be read into an `int[]`.  Map it, `int4[]=int[,]`, where every value of the column has two dimensions.
- **The settings of the application's data source can change what Npgsql reads**, and SqlSource cannot see them.  The table is what a data source made with `NpgsqlDataSourceBuilder`, or a plain `NpgsqlConnection`, reads.  `NpgsqlSlimDataSourceBuilder` reads fewer types until its `Enable...()` methods are called.  The AppContext switch `Npgsql.EnableLegacyTimestampBehavior`, and Npgsql 10's legacy date and time resolver, read some date and time types as other C# types; map those types with the type override to the C# types the application reads.
