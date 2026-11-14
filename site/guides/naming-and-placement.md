# Change names, namespaces and visibility

<a id="async-names"></a>

## End the method names with Async

If you want `GetUserAsync` in place of `GetUser`, turn on the Async suffix with the line `-- generator: async-method-suffix`:

```sql postgres
-- generator: async-method-suffix

-- name: GetUser -> one-optional
SELECT id, name, email
FROM users
WHERE id = @userId;
```

```csharp
GetUserDto? user = await users.GetUserAsync(new GetUserParams(1));
```

**What you get.**  The method of `GetUser` is `GetUserAsync`.  The line is in the file header, so each query of the file gets `Async` at the end of its method's name.

**The rules you need.**

- `async-method-suffix` is a generator parameter.  For a whole project, write `"generatorParameters": ["async-method-suffix"]` in `sqlsource.json`.  See [`async-method-suffix`](../reference/generator-parameters.md#gp-async-method-suffix).
- The name of a synchronous method never ends with `Async`.  Under `method-type=both`, the name of the asynchronous method always does.  See [Synchronous methods](../reference/query-methods.md#synchronous-methods).

<a id="model-names"></a>

## Change the model names (Dto and Params)

If you do not want `Dto` and `Params` at the end of the model names, set your own suffixes in the file header:

```sql postgres
-- input-model-suffix: Args
-- output-model-suffix: Row

-- name: GetUser
SELECT id, name FROM users WHERE id = @id;
```

To name the models of one query, write `-- input-model:` and `-- output-model:` inside the query:

```sql postgres
-- name: GetUser
-- input-model: UserArgs
-- output-model: App.Models.UserRow
SELECT id, name FROM users WHERE id = @id;
```

**What you get.**  With the suffixes, `GetUser` gets `GetUserArgs` and `GetUserRow`.  With the two names, it gets `UserArgs`, and `UserRow` in the namespace `App.Models`.

**The rules you need.**

- A suffix stands in the file header only, not inside a query.  For a whole project, write `"outputModelSuffix": "Row"` in `sqlsource.json`.  See [Suffix of a model's name](../reference/models.md#setting-model-suffix).
- `-- input-model:` and `-- output-model:` stand inside a query only.  The name gets no suffix.  See [Name of one query's model](../reference/models.md#setting-model-name).
- Two queries whose models have one full name share one model, if the models are the same.  See [Names](../reference/models.md#model-names).

<a id="model-namespace-task"></a>

## The namespace of the models

By default the models are in the namespace of the type that claims the query.  Set `"modelNamespace": "App.Models"` in the `sqlsource.json` beside your project file, or `-- model-namespace:` in a `.sql` file.  Two types in two namespaces that read one table each get a model of it, one in each namespace, and the setting gives them one.  A name with a period in an `-- output-model:` line, `App.Rows.UserRow`, is a full name and ignores the setting.  See [namespace of the models](../reference/models.md#setting-model-namespace).

<a id="record-or-class"></a>

## Make the models classes, not records

If you need a class and not a record, set the shape of the models:

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "outputModelType": "class",
}
```

**What you get.**  Each output model is a `class`, with a `get` and a `set` for each property, and no constructor of its own.  Your input models, such as `GetUserParams`, are still records, since the sample sets only `outputModelType`.  See [Shape](../reference/models.md#model-shape).

**The rules you need.**

- `inputModelType` sets the shape of the input models.  In a `.sql` file, write `-- output-model-type: class` in the file header or inside a query.  See [Shape of a model](../reference/models.md#setting-model-type).
- To read rows through Dapper into models whose properties are sorted, make the models classes.  See [Run the models through Dapper](dapper.md#models-through-dapper).

<a id="collection-type-task"></a>

## Return a list, not an array

If you want a list, such as `List<T>` or `IReadOnlyList<T>`, in place of an array, set the collection type of the query:

```sql postgres
-- name: ListUsersNewestFirst
-- collection-type: IReadOnlyList
SELECT id, name, email
FROM users
ORDER BY created_at DESC, id;
```

**What you get.**  The method of `ListUsersNewestFirst` returns `Task<IReadOnlyList<ListUsersNewestFirstDto>>`.  For a `List<T>` that you can add to, write `-- collection-type: List`.

**The rules you need.**

- The collection is filled before the task completes, so reading it runs no query.  To read rows one at a time, see [Read rows one at a time](sync-and-streaming.md#stream-rows).  See [Collection type](../reference/return-types.md#setting-collection-type).
- It changes nothing for a query whose shape is not `many`, and nothing under the output `sql` or `models`.  See [Collection type](../reference/return-types.md#setting-collection-type).

<a id="placement"></a>

## Choose where the methods go, and who can see them

If you want to know where the methods go, or want them `internal` or on your class, set the member location and the visibility.  The folder of this file sets the output `methods`:

```sql postgres
-- name: ListOrdersOfUser -> many
-- member-location: class
-- visibility: public
SELECT id, placed_at, shipped_at
FROM orders
WHERE user_id = @userId
ORDER BY placed_at DESC;

-- name: CountOrderLines -> one
-- member-location: nested-class
SELECT count(*) AS line_count
FROM order_lines
WHERE order_id = @orderId;
```

```csharp
[SqlSourceGenerate]
public static partial class OrderQueries;

// elsewhere
ListOrdersOfUserDto[] orders = await OrderQueries.ListOrdersOfUser(connection, new ListOrdersOfUserParams(userId));
```

**What you get.**  `ListOrdersOfUser` is a public static method of `OrderQueries`, and takes the connection first.  `CountOrderLines` is a method of the private class `Sql` nested in `OrderQueries`, so only code inside `OrderQueries` can call it: `Sql.CountOrderLines(connection, ...)`.

**The rules you need.**

- Remove the `-- member-location:` and `-- visibility:` lines of `ListOrdersOfUser`, and it is an `internal` extension method of `DbConnection`, in the class `OrderQueriesExtensions`.  In a repository, a query's method is a public instance method of your class.  See [Where the members go](../reference/members.md#locations).
- Only a `methods` method can go in the extension class.  See [Where the members go](../reference/members.md#setting-member-location).
- `-- visibility: internal` makes a repository method internal, and its interface leaves it out.  See [Visibility](../reference/members.md#setting-visibility).
- The models do not follow the visibility.  They are `public` or `internal` as your class is.  See [Where the members go](../reference/members.md#locations).
- To give the nested class another name, write `-- nested-class: Counts`.  A constant is then `Counts.CountUsers`.  See [Name of the nested class](../reference/members.md#setting-nested-class).

## If it does not work

- **SQLSRC274.**  A method would take a name that is used, such as `GetAsync` of the queries `Get` and `GetAsync` under `async-method-suffix`.  Rename one of the queries.  See [SQLSRC274](../diagnostics.md#sqlsrc274).
- **SQLSRC257.**  Two models of one name are not the same.  Give one query another model name.  See [SQLSRC257](../diagnostics.md#sqlsrc257).
- **SQLSRC122.**  A member location does not fit a query, such as `extension-class` for a repository query.  A value in the file header reaches each query of the file: write it on the query.  See [SQLSRC122](../diagnostics.md#sqlsrc122).
- **SQLSRC123.**  A visibility does not fit the member location, such as `private` in the extension class.  Write the visibility on the query.  See [SQLSRC123](../diagnostics.md#sqlsrc123).
