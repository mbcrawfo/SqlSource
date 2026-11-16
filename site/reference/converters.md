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

A type override, a [column type](model-properties.md#setting-column-types) or a `-- param:` marker names a C# type.  The driver reads and sends only the types that it knows.  A converter lets a [query method](query-methods.md#query-methods) read and send a C# type of your choice through a type that the driver reads.  This holds for a type that the driver does not know too.  It is a `static` method, marked `[SqlSourceConverter]`, that takes one value and returns it converted.

In the samples:

- `email` is a domain over `text`, which Npgsql reads as a `string`.
- PostgreSQL gives a parameter compared with a column of a domain the base type of the domain, `text` here.  So [`-- param:`](parameters.md#param-marker) declares `@email` as the domain, and the type override gives the domain the type `Email`.
- The method of `FindUser` reads the column `email` as a `string`, and gives it to `FromDatabase`.
- The method gives the `Email` of `@email` to `ToDatabase`, and sends the `string` that it returns.

SQL Server gives a parameter compared with, or written to, a column of an alias type that alias type.  So with an alias type `dbo.Email` and `dbo.Email=MyApp.Email`, a SQL Server query needs no `-- param:`.

`SqlSourceConverterAttribute` is generated into your project, beside `[SqlSourceGenerate]`, in the namespace `SqlSource`.

**The direction.**

- **A converter converts from the type of its argument to its return type**, whatever its name.
- **A column needs the converter from the type that the driver reads to the type of its property.  A parameter needs the converter back.**  So a query that has both, as `FindUser` does, needs both.
- **A query that needs a direction for which a type has no converter, while the type has the other direction, is [SQLSRC280](../diagnostics.md#sqlsrc280).**
- **A method that takes and returns one type serves both directions.**  `System.DateTime AsUtc(System.DateTime value)` under `timestamp=System.DateTime` converts each `timestamp` as it is read and as it is sent.

**The type the driver reads** is the C# type that the type table of [PostgreSQL](databases/postgresql/types.md#type-table) or [SQL Server](databases/sqlserver/types.md#type-table) gives the database type.  It is the type under the version of the driver that your project references, with no type override or column type applied.  The converter takes or returns that type exactly.

- **A domain or an alias type is read as its base type.**  A converter of a domain over `text` takes and returns a `string`.  A converter of a domain over `timestamptz` takes and returns a `System.DateTime`, even where `timestamptz=System.DateTimeOffset` is in effect.
- **An array is read as an array of the type of its element**: a `string[]` for an `email[]`.
- **A converter of another type is not used.**  A type of yours whose converters are all of other types makes the query [SQLSRC279](../diagnostics.md#sqlsrc279), which names the type to convert from and to.
- **SqlSource never narrows or widens a value.**  A converter of an `int` never reads a `bigint` column, which the driver reads as a `long`.
- **The type can change with the driver.**  Npgsql 8 and 9 read a `date` as a `System.DateTime`, and Npgsql 10 as a `System.DateOnly`, as [which version of Npgsql](databases/postgresql/types.md#which-npgsql) says.  So after an update of Npgsql, a type of yours that a type override gives a `date` can need converters of another type.  So can a type of yours for another database type that the update changes.  SQLSRC279 says which.
- **A database type that the driver reads as no C# type has nothing to convert from**, such as a composite type or `regclass`.  A type override or a column type that gives it a type with converters is SQLSRC279.  Give a composite the type that the data source of the application maps it to, with no converter.

**Where a converter is found.**  SqlSource finds a converter in two places:

- **in the code of your own project**, in any type: on the type itself, as `Email` in the sample, or in a class of conversions;
- **on the type that a type override, a column type or a `-- param:` marker names**, when a referenced assembly declares that type.  An example is a library of your ids.  The library references SqlSource for the attribute, and its own build reports the errors of its converters.

The rules:

- **SqlSource does not find a converter in any other type of a referenced assembly.**
- **When your project and the type of a library both convert between the same two types, your project's converter is used**, with no error.  So a project can replace the conversion of a library.
- **The converter of a library is used only when your project can call it.**  That is when it is `public`, or `internal` in a library that gives your project its internals with `InternalsVisibleTo`.
- **A method of a library that is marked as a converter but is not one is [SQLSRC277](../diagnostics.md#sqlsrc277).**  It is reported at each query whose method would use it, since your project cannot change it.  A method that your project cannot call is one of these.

**SqlSource's own conversions.**  The query methods convert some types themselves, whether the [library types](library-types-and-json.md#setting-library-types) turn the library on or not:

- **On SQL Server, the query methods of a project that references NodaTime convert NodaTime's types.**  They convert from and to the types that Microsoft.Data.SqlClient reads, as [NodaTime on SQL Server](databases/sqlserver/libraries.md#nodatime) lists.
- **On SQL Server, they convert System.Text.Json's types from and to a `string`**: `JsonElement`, `JsonDocument`, `JsonNode`, `JsonObject` and `JsonArray`, as [JSON on SQL Server](databases/sqlserver/libraries.md#json) lists.
- **On Npgsql 8 and 9, they convert `JsonNode`, `JsonObject` and `JsonArray` from and to a `string`.**  Those versions read these types only after `EnableDynamicJson()`, as [JSON on PostgreSQL](databases/postgresql/libraries.md#json) says.
- **On PostgreSQL, SqlSource converts nothing else.**  Npgsql's plugins read and send NodaTime's and NetTopologySuite's types, and Npgsql reads and sends the rest of System.Text.Json's types.
- **A converter of yours between the same two types, in the same direction, is used in place of SqlSource's conversion**, with no error.  SqlSource's conversion is kept for the other direction.  So `static NodaTime.Instant FromDatabase(System.DateTime value)` reads each `datetime2` that is an `Instant` your way, and SqlSource's conversion still sends an `Instant` parameter.
- **On SQL Server, the query methods of a project that references `NetTopologySuite.IO.SqlServerBytes` read and send NetTopologySuite's geometries themselves.**  They do so for a `geography` or a `geometry`, as [NetTopologySuite on SQL Server](databases/sqlserver/libraries.md#nettopologysuite) says.  No converter of yours replaces that.

**A converter is a `static` method of one argument that returns a value.**  It is `public` or `internal`, in a type that is not generic.

- **A method marked `[SqlSourceConverter]` that is not a converter is SQLSRC277**, which lists each rule.  This holds in any project, whatever its output.
- **Its argument can be written with `?`**, as `Email?` or `string?`.  It takes the type without the `?`, since it is never given null.
- **Its return type is never written with `?`.**
- **A converter marked `[Obsolete]` as a warning is used**, and the generated code turns the warning off.  One marked `[Obsolete]` as an error is SQLSRC277.
- **Two converters between the same two types in one direction are [SQLSRC278](../diagnostics.md#sqlsrc278)**, at both.

**Null never reaches a converter.**

- **A column that holds a null gives its property null when the property can be null.**  When the property cannot be null, the column throws as any column does: see [how a row is read](query-methods.md#reading-rows).
- **A parameter whose value is null is sent as the database's `NULL`.**
- **So one pair of converters serves a property that can be null and one that cannot.**

In a query method, a PostgreSQL array parameter is an `IEnumerable<T>` of its element, as [array parameters](databases/postgresql/query-methods.md#array-parameters) say.  Its converters are those of the element.

**An array of PostgreSQL whose C# type is an array of a type of yours is converted element by element.**  Each element goes through the converters of the element type.  The type can be chosen for the element, as `email[]` under `public.email=MyApp.Email`.  It can also be chosen for the array, as `public.user_id[]=MyApp.UserId?[]`.  A `MyApp.Email[]` column is read as a `string[]`, and each element is given to `FromDatabase`.

- **When the element is of a reference type or written with `?`, as `MyApp.UserId?`, a `NULL` element stays null.**  The converter is not called for it.
- **A `NULL` element of a value type written without `?` throws `InvalidCastException`**, "Column 'emails' holds a null element.".  A `MyApp.Email[]` holds such elements.
- **The array is converted whole instead when a type override or a column type gives it a type of yours that is not an array.**  An example is `public.email[]=MyApp.EmailList`.  The same holds when it gives the array an array type that has converters of its own.  Those converters, between that type and `string[]`, are used, and the converters of the element are not.

**Converters apply only where a type override, a column type, a `-- param:` marker or a library of the [library types](library-types-and-json.md#setting-library-types) chose the type.**  They apply only in query methods.

- **A column or parameter whose type the type table gives is never converted.**  So a converter between `System.DateTimeOffset` and a type of yours changes no `timetz` column.
- **A type that a library gives is converted only by a converter of the direction it needs.**  For a column, that is the converter from the type that the driver reads.  For a parameter, it is the converter to that type.  Otherwise the type is read and sent as the library has it, with no error.
- **On PostgreSQL, a converter from `System.DateTime` to `NodaTime.Instant` reads each `timestamptz` that NodaTime gives an `Instant`**, in place of Npgsql's plugin.  When there is no converter back, the plugin still sends a parameter.
- **A converter between `NodaTime.Instant` and a type of yours changes no such `timestamptz`.**
- **A query whose output is `models` calls no converter, and gets none of their errors at its name.**  To read it through Dapper, see [the recipe](../guides/dapper.md#models-through-dapper).
- **The driver reads and sends a type that a type override chose, when the driver reads that type for some database type.**  Examples are `string`, `System.DateTimeOffset` and `System.DateOnly`.  This holds when no converter is between that type and the type that the driver reads.

A query whose database's driver the project does not reference, which [SQLSRC273](../diagnostics.md#sqlsrc273) is about, uses no converter either, and gets none of `SQLSRC277` to `SQLSRC280`.

**Without a converter**, the query method asks the driver for the type that was chosen.

- **That is right for a type that the driver was taught.**  Examples are a C# enum after Npgsql's `MapEnum<T>()`, a composite after `MapComposite<T>()`, and a type of a plugin.
- **It is right for a type that the driver reads already**, such as `System.DateTimeOffset` for a `timestamptz`.
- **A type of yours that the driver does not know, with no converter, throws the driver's `InvalidCastException`** when the query runs.
- **A class of yours in a column that holds JSON is the exception.**  System.Text.Json's serializer reads and sends it, as [a class in a JSON column](library-types-and-json.md#json-class) says.  The types of SqlSource's own conversions, above, are exceptions too.

<a id="limits"></a>

## Limits

<a id="limit-vogen-and-stronglytypedid"></a>

### `InvalidCastException` for an id type made by Vogen or StronglyTypedId

**What you see.**  A query method throws `InvalidCastException` for a type made by Vogen or StronglyTypedId that has no converter.

**Why.**  SqlSource converts a type only through methods marked `[SqlSourceConverter]`.  It never converts through the `Value` property of a type, its `From` method or its constructor.  So the type is given to the driver as it is, and the driver throws.

**What to do.**  Write the two converters in a class of your project.  Each converter calls the type's own members:

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

**What you see.**  A C# type cannot be converted one way in one file or query and another way in another.

**Why.**  A type has one conversion in a project.  The converters between a C# type and the type that the driver reads serve every column and parameter of that C# type, whatever chose it.  No file or query can choose other converters for the same two types.

**What to do.**  To convert a database type, or a column, another way in one file or query, give it a C# type of its own there.  Give that type its own converters.  See [converters](#converters).
