# Converters

<a id="converters"></a>

## Converters

```csharp
using SqlSource;

namespace MyApp;

public readonly record struct Email(string Value)
{
    [SqlSourceConverter]
    public static Email FromDatabase(string value) => new(value);

    [SqlSourceConverter]
    public static string ToDatabase(Email email) => email.Value;
}
```

```sql postgres
-- type-override: public.email=MyApp.Email

-- name: FindUser -> one-optional
-- param: @email public.email
SELECT id, email FROM users WHERE email = @email;
```

A type override, a [column type](model-properties.md#setting-column-types) or a `-- param:` marker names a C# type, and the driver reads and sends only the types it knows.  A converter is how a [query method](query-methods.md#query-methods) reads and sends a C# type of your choice, one the driver does not know among them, through a type the driver reads: a `static` method marked `[SqlSourceConverter]` that takes one value and returns it converted.  Above, `email` is a domain over `text`, which Npgsql reads as a `string`.  PostgreSQL gives a parameter compared with a column of a domain the domain's base type, `text` here, so [`-- param:`](parameters.md#param-marker) declares `@email` as the domain, which the type override then gives the type `Email`.  SQL Server gives a parameter compared with, or written to, a column of an alias type that alias type, so with an alias type `dbo.Email` and `dbo.Email=MyApp.Email`, a SQL Server query needs no `-- param:`.  The method of `FindUser` reads the column `email` as a `string` and gives it to `FromDatabase`, and gives the `Email` of `@email` to `ToDatabase` and sends the `string` it returns.  `SqlSourceConverterAttribute` is generated into your project, beside `[SqlSourceGenerate]`, in the namespace `SqlSource`.

**A converter converts from its argument's type to its return type**, whatever its name.  A column needs the converter from the type the driver reads to its property's type, and a parameter the one back, so a query that has both, as `FindUser` does, needs both.  A query that needs the direction a type has no converter for, while the type has the other, is [SQLSRC280](../diagnostics.md#sqlsrc280).  A method that takes and returns one type serves both directions: `System.DateTime AsUtc(System.DateTime value)` under `timestamp=System.DateTime` converts each `timestamp` as it is read and as it is sent.

**The type the driver reads** is the C# type that the type table of [PostgreSQL](databases/postgresql/types.md#type-table) or [SQL Server](databases/sqlserver/types.md#type-table) gives the database type, under the version of the driver your project references, with no type override or column type applied.  The converter takes or returns that type exactly:

- **A domain or an alias type is read as its base type**: a converter of a domain over `text` takes and returns a `string`, and one of a domain over `timestamptz` a `System.DateTime`, even where `timestamptz=System.DateTimeOffset` is in effect.  An array is read as an array of its element's type, `string[]` for `email[]`.
- **A converter of another type is not used.**  A type of yours whose converters are all of other types makes the query [SQLSRC279](../diagnostics.md#sqlsrc279), which names the type to convert from and to.  A converter of an `int` never reads a `bigint` column, which the driver reads as a `long`: SqlSource never narrows or widens a value.
- **The type can change with the driver.**  Npgsql 8 and 9 read a `date` as a `System.DateTime`, and Npgsql 10 as a `System.DateOnly`, as [which version of Npgsql](databases/postgresql/types.md#which-npgsql) says.  So after an update of Npgsql, a type of yours that a type override gives a `date`, or another type that the update changes, can need converters of another type, and SQLSRC279 says which.
- **A database type that the driver reads as no C# type**, a composite type or `regclass` for one, has nothing to convert from: a type override or a column type that gives it a type with converters is SQLSRC279.  Give a composite the type that the application's data source maps it to, with no converter.

**Where a converter is found.**  SqlSource finds a converter in two places:

- **in your project's own code**, in any type: on the type itself, as `Email`'s above, or in a class of conversions;
- **on the type that a type override, a column type or a `-- param:` marker names**, when that type is declared in a referenced assembly, such as a library of your ids.  The library references SqlSource for the attribute, and its own build reports its converters' errors.

A converter in any other type of a referenced assembly is not found.  When your project and a library's type both convert between the same two types, your project's converter is used, with no error, so that a project can replace a library's conversion.

**SqlSource's own conversions.**  On SQL Server, the query methods of a project that references NodaTime convert NodaTime's types themselves, from and to the types Microsoft.Data.SqlClient reads, as [NodaTime on SQL Server](databases/sqlserver/libraries.md#nodatime) lists, whether the [library types](library-types-and-json.md#setting-library-types) turn NodaTime on or not.  They convert System.Text.Json's types the same way, from and to a `string`, whether the library types turn `json` on or not: on SQL Server `JsonElement`, `JsonDocument`, `JsonNode`, `JsonObject` and `JsonArray`, as [JSON on SQL Server](databases/sqlserver/libraries.md#json) lists, and on Npgsql 8 and 9 `JsonNode`, `JsonObject` and `JsonArray`, which those versions read only after `EnableDynamicJson()`, as [JSON on PostgreSQL](databases/postgresql/libraries.md#json) says.  A converter of yours between the same two types, in the same direction, is used in place of SqlSource's, with no error, and SqlSource's is kept for the other direction: so `static NodaTime.Instant FromDatabase(System.DateTime value)` reads each `datetime2` that is an `Instant` your way, and an `Instant` parameter is still sent by SqlSource's conversion.  On PostgreSQL SqlSource converts nothing else, as Npgsql's plugins read and send NodaTime's and NetTopologySuite's types, and Npgsql the rest of System.Text.Json's.  On SQL Server, the query methods of a project that references `NetTopologySuite.IO.SqlServerBytes` read and send NetTopologySuite's geometries for a `geography` or a `geometry` themselves, as [NetTopologySuite on SQL Server](databases/sqlserver/libraries.md#nettopologysuite) says, and no converter of yours replaces that.  A library's converter is used only when your project can call it: when it is `public`, or `internal` in a library that gives your project its internals with `InternalsVisibleTo`.  A library's converter that is not one, one your project cannot call among them, is [SQLSRC277](../diagnostics.md#sqlsrc277) at each query whose method would use it, since your project cannot change it.

**A converter is a `static` method of one argument that returns a value**, `public` or `internal`, in a type that is not generic.  A method marked `[SqlSourceConverter]` that is not one is SQLSRC277, which lists each rule, in any project, whatever its output.  Its argument may be written with `?`, `Email?` or `string?`: it takes the type without the `?`, since it is never given null.  Its return type is never written with `?`.  A converter marked `[Obsolete]` as a warning is used, and the generated code turns the warning off; one marked `[Obsolete]` as an error is SQLSRC277.  Two converters between the same two types in one direction are [SQLSRC278](../diagnostics.md#sqlsrc278), at both.

**Null never reaches a converter.**  A column that holds a null gives its property null when the property can be null, and throws as any column does when it cannot: see [how a row is read](query-methods.md#reading-rows).  A parameter whose value is null is sent as the database's `NULL`.  So one pair of converters serves a property that can be null and one that cannot.

A PostgreSQL array parameter in a query method is an `IEnumerable<T>` of its element, as [array parameters](databases/postgresql/query-methods.md#array-parameters) say, and its converters are those of the element.

**An array of PostgreSQL whose C# type is an array of a type of yours**, chosen for the element, as `email[]` under `public.email=MyApp.Email`, or for the array, as `public.user_id[]=MyApp.UserId?[]`, is converted element by element by the element's converters: a `MyApp.Email[]` column is read as a `string[]`, and each element given to `FromDatabase`.  A `NULL` element stays null, the converter not called for it, when the element is of a reference type or written with `?`, as `MyApp.UserId?`.  A `NULL` element of a value type written without `?`, as a `MyApp.Email[]` holds, throws `InvalidCastException`, "Column 'emails' holds a null element.".  The array is converted whole instead when a type override or a column type gives the array a type of yours that is not an array, `public.email[]=MyApp.EmailList`, or an array type that has converters of its own: those converters, between that type and `string[]`, are used, and the element's are not.

**Converters apply only where a type override, a column type, a `-- param:` marker or a library of the [library types](library-types-and-json.md#setting-library-types) chose the type, and only in query methods.**  A column or parameter whose type the type table gives is never converted, so a converter between `System.DateTimeOffset` and a type of yours changes no `timetz` column.  A type that a library gives is converted only by a converter of the direction it needs, from the type the driver reads for a column and to it for a parameter, and is otherwise read and sent as the library has it, with no error: on PostgreSQL, a converter from `System.DateTime` to `NodaTime.Instant` reads each `timestamptz` that NodaTime gives an `Instant`, in place of Npgsql's plugin, and a parameter is still sent by the plugin when there is no converter back, while a converter between `NodaTime.Instant` and a type of yours changes no such `timestamptz`.  A query whose output is `models` calls no converter, and gets none of their errors at its name; to read it through Dapper, see [the recipe](../guides/dapper.md#models-through-dapper).  A type override to a type the driver reads for some database type, such as `string`, `System.DateTimeOffset` or `System.DateOnly`, is read and sent as the driver does when no converter is between it and the type the driver reads.

A query whose database's driver the project does not reference, which [SQLSRC273](../diagnostics.md#sqlsrc273) is about, uses no converter either, and gets none of `SQLSRC277` to `SQLSRC280`.

**Without a converter**, the query method asks the driver for the type that was chosen.  That is right for a type the driver was taught, a C# enum after Npgsql's `MapEnum<T>()` for one, a composite after `MapComposite<T>()` or a type of a plugin, and for a type it reads already, such as `System.DateTimeOffset` for a `timestamptz`.  A type of yours that the driver does not know, with no converter, throws the driver's `InvalidCastException` when the query runs.  A class of yours in a column that holds JSON is the exception: System.Text.Json's serializer reads and sends it, as [a class in a JSON column](library-types-and-json.md#json-class) says.  So are the types of SqlSource's own conversions, above.

<a id="limits"></a>

## Limits

<a id="limit-vogen-and-stronglytypedid"></a>

### `InvalidCastException` for an id type made by Vogen or StronglyTypedId

- **A type made by Vogen or StronglyTypedId needs converters written for it.**  SqlSource converts a type only through methods marked `[SqlSourceConverter]`, and never through a type's `Value` property and its `From` method or constructor, so such a type with no converter is given to the driver as it is, which throws `InvalidCastException`.  Write the two converters in a class of your project, each calling the type's own members:

  ```csharp
  using SqlSource;

  internal static class IdConversions
  {
      [SqlSourceConverter]
      public static CustomerId ToCustomerId(long value) => CustomerId.From(value);

      [SqlSourceConverter]
      public static long FromCustomerId(CustomerId id) => id.Value;
  }
  ```

  See [converters](#converters).

<a id="limit-one-conversion-per-type"></a>

### One C# type cannot be converted two ways in two files

- **A type has one conversion in a project.**  The converters between a C# type and the type the driver reads serve every column and parameter of that C# type, whatever chose it, and no file or query can choose others for the same two types.  To convert a database type, or a column, another way in one file or query, give it there a C# type of its own, with its own converters.  See [converters](#converters).
