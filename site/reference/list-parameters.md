# List parameters (IN lists)

This page says how a [query parameter](parameters.md) written after `IN` or `NOT IN` takes a list of values, how the list is padded, and how many values it can hold.

<a id="list-parameters"></a>

## List parameters

```sql postgres
-- name: ListProducts
SELECT id, name FROM products
WHERE (@categories IS NULL OR category IN @categories)
  AND id NOT IN @excluded;
```

```csharp
List<int> excluded = [7, 12];
var products = await catalog.ListProducts(new ListProductsParams(["books", "games"], excluded));
```

A list parameter is a query parameter written directly after `IN` or `NOT IN`, with no parentheses.  It holds a list of values.  Above, `ListProductsParams.Categories` is an `IEnumerable<string>?`, and `Excluded` is an `IEnumerable<int>`.  So the [query method](query-methods.md#query-methods) takes an array, a `List<T>`, a `HashSet<T>` or any other sequence.  It sends one parameter for each value: with two categories, `category IN @categories` reaches the database as `category IN (@categories_0, @categories_1)`.

- **Each value's parameter is named after the list.**  Its name is one that no other query parameter of the query has, and that no variable the query [declares](parameters.md#declared-variables) has.
- **`IN (@categories)` is not a list.**  SQL reads it as a list of one value, and `@categories` is one value, as any other query parameter is.
- **A list stands only after `IN` or `NOT IN`, or before `IS NULL` or `IS NOT NULL`.**  Any other use of it is [SQLSRC126](../diagnostics.md#sqlsrc126), at that use: a use as one value, `parent_category = @categories`, or a use in the default of a [token](tokens.md#tokens).
- **A query parameter is a list in every spelling of its name.**
- **`IN` is matched ignoring case.**
- **A comment between `IN` and the query parameter counts as a space.**
- **`position(a IN @p)` is never a list.**  Directly inside the parentheses of a call to `position`, `IN` takes one value, as in PostgreSQL's `position('@' IN @email)`.  So `@email` is an ordinary query parameter there.
- **`position` is the one place.**  Anywhere else, a query parameter directly after `IN` is a list.
- **If another construct of your database takes one value after `IN`, write the value in parentheses**: `IN (@p)`, which SQL reads as the one value.
- **A `-- param:` marker on a list describes its values.**  `-- param: @categories text` gives each value the type `text`, and `-> Category` gives each value the C# type `Category`.
- **`null` in the marker makes the list itself nullable.**
- **Without a type, the database works out the type of the values from the use**, as it would for `category IN (@categories)`: the type of `category`.
- **A list with `-> json` is [SQLSRC126](../diagnostics.md#sqlsrc126) too.**  A list holds values, not JSON.
- **A value of a list is never nullable.**  A list of `integer` is an `IEnumerable<int>`, never an `IEnumerable<int?>`, and no marker makes it one.  Why: a `NULL` among the values of `IN` matches nothing, and a `NULL` among those of `NOT IN` makes it match no row.
- **The property of a list can be null when one of these is true**: the query tests the list for null, as `@categories IS NULL` does, or the marker of the list says `null`.  See [which properties can be null](model-properties.md#model-nullability).
- **If a list is null and its property cannot be null, the query method sends an empty list.**
- **An empty list matches no row after `IN`, and every row after `NOT IN`.**  A null list does the same.
- **`@categories IS NULL` is true for a null list and false for an empty list.**  So in the sample, a null `Categories` keeps every product, and an empty one keeps none.
- **On SQL Server, a list after a `NOT` inside an aggregate, a `GROUP BY` or an `OUTPUT` clause is [SQLSRC127](../diagnostics.md#sqlsrc127)**, under the output `repository` or `methods`.
- **On SQL Server, a list after a `NOT` inside an aggregate of your own fails when it is empty or null**, under the output `repository` or `methods`.  See [the limit](databases/sqlserver/query-methods.md#limit-list-in-an-aggregate).
- **On PostgreSQL, a list in the index predicate of `ON CONFLICT` fails when it is empty or null.**  See [PostgreSQL's parameters](databases/postgresql/index.md#parameters).
- **On PostgreSQL, a list can make the columns of a query's joined tables able to be null.**  This happens when both of these are true:
  - the list stands after a `NOT`, or its truth is used as a value, as in `(o.id IN @ids) IS FALSE`;
  - the query holds a word such as `LEFT`, `RIGHT`, `FULL` or `OUTER`, even as the name of a function.

  Why: an empty list keeps the rows that an outer join filled with null.  [Which columns can be null](databases/postgresql/nullability.md#nullability) gives every word and every use.
- **A null value in a list of a reference type, such as a `string`, is sent as a `NULL`.**  After `IN` it matches nothing, and the other values still match.
- **After `NOT IN`, a null value makes the condition match no row at all**, as SQL's `id NOT IN (1, NULL)` is never true.  Keep nulls out of a list that `NOT IN` reads.
- **A list can be used more than once.**  Every use sends the same parameters.
- **Each list is read once.**  An array, a `List<T>`, a `HashSet<T>` or any other `ICollection<T>` or `IReadOnlyCollection<T>` is counted when the method is called.  Its values are read when they are sent.
- **Any other sequence, such as a LINQ query, is copied into an array once**, when the method is called.
- **A collection must not change between its count and the send.**  If its count of values changed by the time they are sent, the method throws `InvalidOperationException`, "The list 'Excluded' changed after the query counted it.".  Leave a list as it is until the method's task completes.  For a method that [streams its rows](return-types.md#streaming-rows), leave it as it is until the loop has its first row.
- **In the outputs `models` and `sql`, the SQL holds the list as written**, `IN @categories`, for the code that runs it.  Under `models`, the property of the input model is the same `IEnumerable<T>`.  Whether a library runs such SQL is the library's affair.  Dapper, for one, sends one parameter for each value on SQL Server, as the query methods do, but only for a list that the query does not test for null.  Dapper sends a list to PostgreSQL as one array, which `IN @categories` cannot take: see [the recipe](../guides/dapper.md#models-through-dapper).  On PostgreSQL, write [`= ANY(@categories)`](databases/postgresql/query-methods.md#array-parameters) for a query that Dapper runs.

A change to a list's use, such as `IN (@ids)` to `IN @ids`, is a change to the SQL, so the query's sidecar is out of date until it is described again.

<a id="list-padding"></a>

### Padding

```csharp
int[] ids = [4, 8, 15];
// sent as id IN (@ids_0, @ids_1, @ids_2, @ids_3), with @ids_3 another 15
```

A query method rounds the count of each list up to the next size, and fills the list with its last value.  The sizes are 1, 2, 4, 8, 16, 32, 64, 128, 256 and 512, and above 512 each multiple of 512.  So 3 values are sent as 4, and 600 as 1,024.  A repeated value changes no result of `IN` or `NOT IN`.  The database sees a few forms of the query, one for each size, rather than one for each count, and keeps a plan of each.

- **The [generator parameter](generator-parameters.md#setting-generator-parameters) `list-padding` turns the padding on or off.**  It is on by default, and `list-padding=off` sends each list at its own count.
- **Padding never makes a method throw.**  If the padded lists would pass [the limit](#list-limit) and the lists at their own counts would not, the method sends them at their own counts.
- **An empty list or a null one is sent as one parameter**, a `NULL` of the values' type, in `(SELECT @ids_0 WHERE 1 = 0)`, which holds no row.
- **On SQL Server, an empty or null list that no `NOT` reaches is sent as `(@ids_0)`.**  It matches no row either.  Why: SQL Server refuses the subquery inside an aggregate, a `GROUP BY` and an `OUTPUT` clause.

<a id="list-limit"></a>

### The limit of parameters

```console
System.ArgumentException: The query 'ListProducts' would send 70002 parameters; PostgreSQL accepts 65535.  Pass fewer values in 'Excluded', or send them as an array with = ANY. (Parameter 'parameters')
```

PostgreSQL takes at most 65,535 parameters in one query.  SQL Server takes at most 2,098: it takes 2,100, of which Microsoft.Data.SqlClient uses two to send a query with parameters.  Every parameter of the query counts:

- each query parameter that is not a list;
- one for each list that the query tests for null;
- one for each value of each list, or one for an empty list.

A method whose query would send more throws `ArgumentException`.  The exception names the query, the count, the limit and the property of the largest list.  The method throws when it is called, before anything is sent, and so does a method that [streams its rows](return-types.md#streaming-rows).  [Padding](#list-padding) alone never makes it throw.

For a list that large, send it as one parameter.  On PostgreSQL, send it as an [array](databases/postgresql/query-methods.md#array-parameters), `id = ANY(@ids)`.  On SQL Server, send it as [one JSON array](databases/sqlserver/query-methods.md#list-parameters), read with `OPENJSON`.
