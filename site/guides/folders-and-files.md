# Give a folder its own settings, or leave files out

<a id="configure-a-folder"></a>

## Configure one folder differently

A project reads its `.sql` files as PostgreSQL and keeps the comments of its queries.  One folder, `Legacy/`, holds MySQL files, and their queries take token fragments that are sometimes empty, so the folder turns the token check off.  A [settings file](../reference/settings-file.md#settings-files) in a folder gives it its own settings:

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
    "generatorParameters": ["keep-comments"],
}
```

```jsonc
// Legacy/sqlsource.json
{
    "dialect": "mysql",
    "generatorParameters": ["token-validation=off"],
}
```

Every file under `Legacy/` is read as `mysql`, keeps its comments, and skips the token check.  Every other file is untouched: it is read as `postgres`, keeps its comments, and checks its tokens.  Three things to notice:

- The folder's list names only `token-validation=off`.  It adds to the project's list, so the files of `Legacy/` keep their comments without saying so.  A folder that wants none of the project's list starts its own with `default`: `["default", "token-validation=off"]` would give `Legacy/` every default but the token check.
- The dialect takes the nearest value whole.  Had the project's file set `"mysql,ansi-quotes"` as its dialect, this folder would still be read as plain `mysql`.
- A file's own markers still win.  A `-- generator:` line in one of the files adds its words to the list of both settings files, and `-- generator: default` drops that list, and a `-- dialect: mariadb` overrides both settings files.  See [which place wins](../reference/settings.md#precedence).

The file in `Legacy/` applies to the `.sql` files of its subfolders too, `Legacy/Reports/` among them, unless a settings file nearer to them sets the same setting.  Every setting of a query comes from the settings files of its `.sql` file, whichever type claims it, and never from those of a type's C# file.

Only the settings whose row in [the table](../reference/settings.md#all-settings) lists a settings file can be given this way.  To change a setting for one file, write its marker in the file header, which wins over every settings file.

<a id="leave-migration-scripts-out"></a>

## Leave migration scripts out

A `.sql` file that no `[SqlSourceGenerate]` type claims is not read, so scripts in a folder of their own do no harm.  See [the recipe](#leave-migration-scripts-out) to keep them away from the compiler altogether.

```text
App/
    App.csproj
    Queries/
        UserRepository.cs      [SqlSourceGenerate] on UserRepository
        UserRepository.sql     belongs to UserRepository
    Migrations/
        001_create_users.sql   not claimed by any type
```

A `.sql` file that no type claims is not read, so a folder of migration scripts beside your queries does no harm.  To keep the scripts away from the compiler altogether, remove them from the files the package hands it:

```xml
<ItemGroup>
    <AdditionalFiles Remove="Migrations/**/*.sql" />
</ItemGroup>
```

Rules: [which files belong to a type](../reference/attributes.md#files-of-a-type), [which `.sql` files are included](../reference/msbuild.md#included-files).

<a id="class-for-a-folder"></a>

## One class for a whole folder

If you want one class for every `.sql` file of a folder, and not one class for each file, write `Source = "."` on the class:

```text
Models/
    sqlsource.json      "output": "models"
    StoreQueries.cs     [SqlSourceGenerate(Source = ".")] on StoreQueries
    Orders.sql          belongs to StoreQueries
    Users.sql           belongs to StoreQueries
```

```jsonc
// Models/sqlsource.json
{
  "output": "models",
}
```

```csharp
// Models/StoreQueries.cs
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using SqlSource;

[SqlSourceGenerate(Source = ".")]
public sealed partial class StoreQueries(DbConnection connection)
{
    public Task<GetOrderDto?> GetOrderAsync(int orderId) =>
        connection.QuerySingleOrDefaultAsync<GetOrderDto>(Sql.GetOrder, new GetOrderParams(orderId));
}
```

**What you get.**  `StoreQueries` gets the queries of `Orders.sql` and `Users.sql`.  The folder's settings file sets the output `models`, so each query gets its models and a constant in the class `Sql`.  For the query `GetOrder` of `Orders.sql`, these are `GetOrderParams`, `GetOrderDto` and `Sql.GetOrder`.  The class runs the SQL itself, here through Dapper: see [Use Dapper or your own ADO.NET code](dapper.md#models).

**The rules you need.**

- `Source = "."` gives the class each `.sql` file in the folder of its C# file.  Subfolders are not searched.  See [Source](../reference/attributes.md#setting-source).
- A query name can be used once across all the files of a class.  See [Which files belong to a type](../reference/attributes.md#files-of-a-type).
- Every setting of a query comes from its `.sql` file and the settings files of its folder, not from the class.  See [The attribute](../reference/attributes.md#attribute).
- Write `Source` as a string where the attribute is.  The tool reads the attribute as text, so it cannot read a constant or `nameof` there.  See [`Source` written as text](../reference/attributes.md#limit-source-written-as-text).

## If it does not work

- **SQLSRC004.**  `Source` matches no `.sql` file.  Write the path from the folder of the C# file, not from the project.  See [SQLSRC004](../diagnostics.md#sqlsrc004).
- **SQLSRC008.**  Two files of the folder have a query of one name.  Rename one of the queries.  See [SQLSRC008](../diagnostics.md#sqlsrc008).
- **SQLSRC208, and SQLSRC241 for each query of the class.**  `Source` is a constant, `nameof` or another expression.  Write the string where the attribute is.  See [`Source` written as text](../reference/attributes.md#limit-source-written-as-text).
