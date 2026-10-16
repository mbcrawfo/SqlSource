# The generator

The generator is the part of the `SqlSource` package that runs in the compiler.  It reads the `.sql` files of your project and writes C# beside your own code: a string constant for each query, or a method where the query has [tokens](concepts.md#token).  It needs no database and no setup beyond the package and one attribute, in a project that targets .NET 8 or later with C# 12 or later ([SQLSRC003](diagnostics.md#sqlsrc003), [SQLSRC012](diagnostics.md#sqlsrc012)).

```sql
-- name: GetUser
-- summary: Loads one user by id.
SELECT id, name
FROM users
WHERE id = @id;

-- name: ListFrom
-- summary: Lists the rows of one table.
SELECT id, name FROM {{table}} ORDER BY {{orderBy}};
```

```csharp
using Dapper;
using SqlSource;

[SqlSourceGenerate]
public partial class UserRepository(IDbConnection connection)
{
    public Task<User> Get(int id) =>
        connection.QuerySingleAsync<User>(Sql.GetUser, new { id });

    public Task<IEnumerable<User>> List() =>
        connection.QueryAsync<User>(Sql.ListFrom("users", "name DESC"));
}
```

`Sql.GetUser` is a constant that holds the text of the first query, and `Sql.ListFrom` is a method that returns the text of the second with its tokens filled in.  Both are plain strings: the calls above use Dapper, and ADO.NET or any other library can take them as well.  A query that is renamed or removed is a compile error where it is used.

<a id="attribute"></a>

## The attribute

`[SqlSourceGenerate]` marks a type that the generator adds to.  The package puts the attribute and its enums in your project, as internal types in the namespace `SqlSource`, so the code above needs `using SqlSource;` and nothing else.

```csharp
[SqlSourceGenerate(Path = "Queries", SqlLocation = SqlLocation.Direct, Parameters = "keep-comments")]
internal static partial class Reports;
```

- **It goes on a class, a struct, a record or a record struct.**  The type may be static, generic, or nested in other types.
- **The type and every type that contains it must be `partial`** ([SQLSRC001](diagnostics.md#sqlsrc001)), and none may be `file` ([SQLSRC002](diagnostics.md#sqlsrc002)).  The generator adds its members in a second declaration of each.
- **A type with an error gets no members.**  The code that uses them then fails to compile too, so fix the first error the build reports.

Every property of the attribute is a [setting](settings.md), documented there with the other places it can be written and the rule for which place wins:

| Property | Sets |
|----|----|
| `Path` | Which `.sql` files belong to the type: [Path](settings.md#setting-path) |
| `SqlLocation` | Where the generated members go: [Where the SQL goes](settings.md#setting-sql-location) |
| `Parameters` | Generator parameters for the type's queries: [Generator parameters](settings.md#setting-generator-parameters) |
| `Output` | What is generated for the type's queries: [Output](settings.md#setting-output) |
| `InputModelSuffix`, `OutputModelSuffix` | What ends the name of a model: [Suffix of a model's name](settings.md#setting-model-suffix) |
| `ModelNamespace` | The namespace of the models: [Namespace of the models](settings.md#setting-model-namespace) |
| `InputModelType`, `OutputModelType` | The shape of a model: [Shape of a model](settings.md#setting-model-type) |
| `CollectionType` | The type that holds many rows: [Collection type](settings.md#setting-collection-type) |
| `MethodLocation` | Where the methods that run the queries go: [Where the methods go](settings.md#setting-method-location) |

`Path`, `SqlLocation` and `Parameters` change what the generator writes today.  `Output` is read by the tool today and changes no generated code, and the properties for models and methods (the last five rows) are read and checked and change none: see [what works today](README.md#what-works-today).

<a id="files-of-a-type"></a>

## Which files belong to a type

```text
Repository/
    UserRepository.cs      [SqlSourceGenerate] on UserRepository
    Users.sql              belongs to UserRepository
    Orders.sql             belongs to UserRepository
    Reports/
        Monthly.sql        not searched: it is in a subfolder
```

A type gets the files that its [`Path`](settings.md#setting-path) names, by the rules and with the errors given there.  What follows is what the generator does with the files.

- **A type's files are one set.**  A query name is used once across all of them: a second use is [SQLSRC008](diagnostics.md#sqlsrc008), and the file with the second one gets no members for that type.
- **Two types may claim one file**, and each gets its own members from it.  They can ask for different things: each type's attribute applies to its own members only.
- **A `.sql` file that no type claims is not read.**  A folder of migration scripts does no harm.
- **A file with an error produces no members** until it is fixed: see [`.sql` files](sql-files.md).  The other files of the type are unaffected.

<a id="locations"></a>

## Where the members go

```csharp
[SqlSourceGenerate]                                     // Nested, the default
public partial class UserRepository
{
    public string Get() => Sql.GetUser;
}

[SqlSourceGenerate(SqlLocation = SqlLocation.Direct)]
public static partial class UserQueries;

// elsewhere
var sql = UserQueries.GetUser;
```

[`SqlLocation`](settings.md#setting-sql-location) chooses between a private class `Sql` nested in the type (`Nested`, the default) and the type itself (`Direct`).  Its section says what each means.  What to know when you choose:

- Use `Nested` for queries that one class runs, and `Direct` for a type whose job is to hold queries that other code uses.
- With `Nested`, the type cannot have a member or a type parameter named `Sql`, and cannot be called `Sql` itself ([SQLSRC007](diagnostics.md#sqlsrc007)).  Rename it, or use `Direct`.
- A query is not named like the class that holds the members: `Sql` under `Nested`, the type's own name under `Direct` ([SQLSRC009](diagnostics.md#sqlsrc009)).
- A query may share its name with a member that the type inherits, such as `ToString`.  The constant hides it, with no warning in the generated code.

<a id="constants"></a>

## Constants

```csharp
public const string GetUser = "SELECT id, name\nFROM users\nWHERE id = @id;";
```

A query without tokens is a `public const string` with the name of the query.  Its value is the SQL you wrote, with the markers and comments taken out and line endings made `\n`: [what reaches the generated SQL](sql-files.md#generated-sql) lists everything done to it.  A query parameter such as `@id` stays in it as written; you give the database its value when you run the query.

Because it is a constant, it can be used wherever C# wants one, such as another `const` or an attribute argument:

```csharp
public const string Prefix = UserQueries.GetUser;
```

<a id="token-methods"></a>

## Token methods

```csharp
var sql = Sql.ListFrom("users", "name DESC");
// SELECT id, name FROM users ORDER BY name DESC;
```

A query with a token is a `public static string` method with the name of the query, `ListFrom(string table, string orderBy)`, in place of a constant.  Its parameters, and what it returns, are under [tokens](parameters-and-tokens.md#tokens).

A token is replaced by plain text substitution and nothing is escaped.  **Pass only text that your own code chooses**, such as a table name from a fixed list, and a [query parameter](concepts.md#query-parameter) for every value.  By default the method throws for a null, empty or blank argument.  The rules for tokens, their defaults and that check are on [parameters and tokens](parameters-and-tokens.md#tokens).

<a id="intellisense"></a>

## Documentation in IntelliSense

Every generated member has a documentation comment, so your editor shows it where the member is used.  For `GetUser`, the comment is:

```csharp
/// <summary>
/// Loads one user by id.
/// </summary>
/// <remarks>
/// <code>
/// SELECT id, name
/// FROM users
/// WHERE id = @id;
/// </code>
/// </remarks>
public const string GetUser = "SELECT id, name\nFROM users\nWHERE id = @id;";
```

The summary is the query's `-- summary:` ([summaries](sql-files.md#summaries)), or "The `GetUser` query from `Users.sql`." when it has none.  The remarks hold the SQL of the constant, so a reader sees what a query does without opening the `.sql` file.

A token method adds a line for each token and one for the result, and shows its tokens as `{{table}}`:

```csharp
/// <summary>
/// Lists the rows of one table.
/// </summary>
/// <remarks>
/// <code>
/// SELECT id, name FROM {{table}} ORDER BY {{orderBy}};
/// </code>
/// </remarks>
/// <param name="table">The text that replaces <c>{{table}}</c>.</param>
/// <param name="orderBy">The text that replaces <c>{{orderBy}}</c>.</param>
/// <returns>The SQL with each token replaced by its argument.</returns>
public static string ListFrom(string table, string orderBy)
```

Every generated member is documented, so a project that treats a missing comment on a public member (CS1591) as an error gets none from generated code.

<a id="models"></a>

## Models

> **Coming in a later release.**  The generator will write a type for the parameters of each query and a type for a row of its result, from what the tool recorded in the query's sidecar.  See [what works today](README.md#what-works-today).

<a id="query-methods"></a>

## Query methods

> **Coming in a later release.**  The generator will write a method for each query that runs it on a connection and returns its rows.  See [what works today](README.md#what-works-today).

<a id="projects-that-share-internals"></a>

## Projects that share internals

A test project usually sees the internals of the project it tests, through `InternalsVisibleTo`.  If both use SqlSource, each has its own copy of the attribute and its enums (`SqlLocation`, `GeneratorOutput`, `GeneratorModelType`, `GeneratorCollectionType` and `MethodLocation`), and the test project sees two of each.  It uses its own.  The compiler warns about such a conflict (CS0436) wherever the test project's code names one of them, for example at its `[SqlSourceGenerate]`.

SqlSource turns that warning off for these types and for nothing else, as the suppression `SQLSRC901`.  Nothing has to be added to `NoWarn`.  The suppression holds when warnings are errors.  A conflict between two types of your own is still reported, even when one of them is named like a SqlSource type: an `internal class SqlLocation` that you declare in both projects, in a namespace of your own, still gets CS0436.

`SQLSRC901` is not an error and is not in the [diagnostics list](diagnostics.md).

<a id="known-issues"></a>

## Known issues

- **The tool reads the attribute without compiling your code.**  `sqlsource describe` finds `[SqlSourceGenerate]` by reading your C# files as text, so `Path` and `Output` must be written out where the attribute is: `Path = "Queries"` and `Output = GeneratorOutput.Models`.  A constant, `nameof`, a concatenation or a cast there compiles, and the tool reports [SQLSRC208](diagnostics.md#sqlsrc208) and describes nothing for the type.  The build is not affected.  Two more things follow from the same reading.  An alias of the attribute (`using Gen = SqlSource.SqlSourceGenerateAttribute;` and `[Gen]`) works in the build and is not seen by the tool, so the type's queries are not described.  An attribute inside `#if` is read with the compilation constants the tool gets from your project, which may not be those of the configuration you build.  Write the attribute under its own name, and outside `#if` where you can.
- **Under `Direct`, a query named like a member that the type declares is a compiler error.**  A query `Get` in a type that declares a method `Get` gives the compiler's CS0102, in the generated code, and no SQLSRC error.  Rename the query, or use `Nested`.
- **Where the suppression of CS0436 does not run, the warning is shown.**  The suppression is part of the package's analyzers.  A compiler or an editor that does not run it still reports CS0436 in a project that sees the internals of another one that uses SqlSource, and a project that treats warnings as errors then fails to build there.  SqlSource has not been checked in the live analysis of Visual Studio or Rider.  If you see the warning, add `<NoWarn>$(NoWarn);CS0436</NoWarn>` to the project that sees the other's internals.  That also hides a conflict between two types of your own.
