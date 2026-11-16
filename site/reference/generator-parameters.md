# Generator parameters

<a id="setting-generator-parameters"></a>

## Generator parameters

```sql
-- generator: keep-comments

-- name: Report
SELECT /* the database logs this comment */ id FROM users;

-- name: Total
-- generator: token-validation=off
SELECT COUNT(*) FROM {{table:users}};

-- name: Plain
-- generator: default
SELECT /* the database never sees this comment */ id FROM users;
```

A [generator parameter](#setting-generator-parameters) is an option that changes what SqlSource does with a query.  It is not a query parameter: see [three kinds of parameter](#three-kinds-of-parameter).  A word in a `-- generator:` line, or in `generatorParameters` in a [settings file](settings-file.md#settings-files), gives a generator parameter one of its values.  So a word can turn `keep-comments` on or off, or give `transaction` the value `serializable`.

In the sample, the three queries of one file get these values:

- The file header turns `keep-comments` on for every query of the file.  So `Report` keeps its comments.
- `Total` turns `token-validation` off and says nothing of `keep-comments`.  So it skips the token check, and it keeps its comments too.
- `Plain` gives every parameter its default, whatever its file says.  So it does not keep its comments.

**The values.**  Each generator parameter takes one of its own words, its values:

- **Most generator parameters are on or off.**  This is the common case.
- **`transaction` also takes an isolation level**, such as `transaction=serializable`.
- **`method-type` takes `async`, `sync` or `both`.**

**The defaults.**  If no word reaches a query, the query gets the default of every parameter.  `token-validation`, `table-models`, `array-pool`, `list-padding` and `transaction` are on, `method-type` is `async`, and the others are off.

| Parameter | Default |
|----|----|
| [`keep-comments`](#gp-keep-comments) | off |
| [`token-validation`](#gp-token-validation) | on |
| [`sort-input`](#gp-sort-input), [`sort-output`](#gp-sort-output) | off |
| [`table-models`](#gp-table-models) | on |
| [`async-method-suffix`](#gp-async-method-suffix) | off |
| [`array-pool`](#gp-array-pool) | on |
| [`list-padding`](#gp-list-padding) | on |
| [`collection-method`](#gp-collection-method) | off |
| [`transaction`](#gp-transaction) | on |
| [`method-type`](#gp-method-type) | async |
| [`default`](#gp-default) | |

**A word** is `name`, `name=value` or `name=default`:

- **A word has no space around its `=`.**  A `-- generator:` line separates its words by white space.
- **`name=value` gives the parameter one of its values**, such as `keep-comments=off` or `transaction=serializable`.
- **`name` alone is `name=on`.**  `method-type` does not take `on`.
- **`name=default` gives the parameter its default**, whatever the places below say.
- **SqlSource ignores case when it matches names and values.**  So `Keep-Comments=OFF` is `keep-comments=off`.

**`default` alone gives every parameter its default.**  The other words of its place apply over it, wherever it stands among them.  So `-- generator: default keep-comments` is every default with `keep-comments` on, and so is `-- generator: keep-comments default`.  A place can repeat `default`.

**The places**, from the farthest to the nearest:

1. `generatorParameters` in each settings file of the file's folder and of the folders above it, up to the first that says `"root": true`, from the farthest to the nearest.
2. The `-- generator:` lines in the file header.
3. The `-- generator:` lines inside the query.

These rules say which place wins:

- **A place sets only the parameters that it names.**  Every other parameter keeps the value that the places below give it.
- **A parameter has the value of the nearest place that names it.**  If no place names it, it has its default.
- **`default` names every parameter.**  So nothing below the place that says `default` matters.
- **The lines of one place add up.**  Several `-- generator:` lines in the file header are one place, and so are several lines inside one query.
- **A file with no `-- name:` line is one query and one place.**  So all of its `-- generator:` lines add up, wherever they stand.  If `keep-comments` is at its top and `keep-comments=off` is lower down, the two contradict each other.  The second does not override the first.

**In a settings file**, the value of `generatorParameters` is an array of strings, with one word in each: `"generatorParameters": ["sort-output", "token-validation=off"]`.

- **`[]` names no parameter**, so it changes nothing.
- **`["default"]` gives every parameter its default**, whatever the folders above say.
- **SqlSource skips an element that is empty or white space.**

**A mistake** is reported where it is written:

| Mistake | In a `-- generator:` line | In a settings file |
|----|----|----|
| A word that is not a generator parameter, such as `keep-coments` | [SQLSRC109](../diagnostics.md#sqlsrc109) | [SQLSRC019](../diagnostics.md#sqlsrc019) at the element, and the other elements apply |
| A value that is not one of the parameter's, or `default`, such as `keep-comments=yes`, `transaction=snapshoot` or `method-type` alone, which is `method-type=on`, or `default` with a value | [SQLSRC111](../diagnostics.md#sqlsrc111) | [SQLSRC019](../diagnostics.md#sqlsrc019) at the element, and the other elements apply |
| A line with nothing after the colon | [SQLSRC110](../diagnostics.md#sqlsrc110) | |
| One parameter given two different values in one place, such as `keep-comments` and `keep-comments=off`.  `keep-comments=default` beside either is one too, even where the default is the same | [SQLSRC112](../diagnostics.md#sqlsrc112), at the later word | [SQLSRC020](../diagnostics.md#sqlsrc020), at the later element, and the first applies |

- **A mistake in a `-- generator:` line leaves its file without members until you fix it.**  See [a value that is wrong](settings.md#a-value-that-is-wrong).
- **The same value given twice is not a mistake.**  Nor is `default` beside other words.

<a id="gp-keep-comments"></a>

### `keep-comments`

For example, `-- generator: keep-comments` in the file header keeps the comments of every query of the file.  The values of `keep-comments` are `on` and `off`, and its default is `off`.

- **If it is on, comments and blank lines stay in the generated SQL.**  See [what reaches the generated SQL](sql-files.md#generated-sql).
- **A PostgreSQL [collection method](databases/postgresql/query-methods.md#collection-methods) sends its SQL without comments and blank lines**, even if `keep-comments` is on.

<a id="gp-token-validation"></a>

### `token-validation`

For example, `-- generator: token-validation=off` inside a query turns off the check of its token arguments.  The values of `token-validation` are `on` and `off`, and its default is `on`.

- **If it is on, the method of a query with tokens checks its arguments.**  See [validation](tokens.md#token-validation).
- **`token-validation=off` turns the check off.**

<a id="gp-sort-input"></a>

<a id="gp-sort-output"></a>

### `sort-input`, `sort-output`

The values of `sort-input` and of `sort-output` are `on` and `off`, and the default of each is `off`.

- **If `sort-input` is on, SqlSource sorts the properties of the query's input model by name.**
- **If `sort-output` is on, SqlSource sorts the properties of the query's output model by name.**

See [the order of the properties](model-properties.md#property-order).

<a id="gp-table-models"></a>

### `table-models`

The values of `table-models` are `on` and `off`, and its default is `on`.

- **If it is on, a query that reads a whole table gets a row model named after the table.**
- **With `table-models=off`, the query gets a row model named after the query.**

See [table models](models.md#table-models).

<a id="gp-async-method-suffix"></a>

### `async-method-suffix`

For example, under `async-method-suffix` the asynchronous method of the query `GetUser` is `GetUserAsync`.  The values of `async-method-suffix` are `on` and `off`, and its default is `off`.

- **If it is on, the name of a query's [method](connection-methods.md#method-signature) ends with `Async`.**
- **The name of a synchronous method never ends with `Async`.**
- **Under `method-type=both`, the name of an asynchronous method always ends with `Async`.**

See [synchronous methods](query-methods.md#synchronous-methods).

<a id="gp-array-pool"></a>

### `array-pool`

The values of `array-pool` are `on` and `off`, and its default is `on`.  If it is on, these methods read their rows into buffers borrowed from .NET's shared array pool:

- A [`many`](return-types.md#method-shapes) method that returns a collection, unless it returns an `ImmutableList<T>` or an `IImmutableList<T>`.
- A [collection method](collection-methods.md#collection-methods) that reads rows, of the shape `many`, `one` or `one-optional`.

The rules:

- **A method that reads through the pool allocates no collection but the one it returns**, for a result of up to about 1,000 rows.  This is true once the pool holds buffers of the sizes that the method needs.
- **With `array-pool=off`, every such method reads its rows into a collection that it allocates and grows.**  Hand-written code usually does the same.  A method that returns one of the two immutable lists always does.
- **Turn it off to keep the pool out of a project, or to compare the two.**
- **A method that [streams its rows](return-types.md#streaming-rows) is the same with `array-pool` on or off.**  It holds no collection.

<a id="gp-list-padding"></a>

### `list-padding`

For example, a query method sends a [list parameter](parameters.md#list-parameters) of three values as four, with its third value twice.  The values of `list-padding` are `on` and `off`, and its default is `on`.

- **If it is on, a query method pads each list parameter to one of a few sizes.**  The sizes are 1, 2, 4 and so on to 512, and then each multiple of 512.  The method repeats the last value of the list to fill the size.
- **So the database sees a few forms of the query, whatever the counts.**  It keeps a plan of each form.  See [padding](parameters.md#list-padding).
- **With `list-padding=off`, a query method sends each list at its own count of values.**  So each count is a form of its own, and a list sends a parameter for each of its values alone.

<a id="gp-collection-method"></a>

### `collection-method`

The values of `collection-method` are `on` and `off`, and its default is `off`.

- **If it is on, a query with parameters whose output is `repository` or `methods` also gets a [collection method](collection-methods.md#collection-methods).**
- **A collection method is an overload of the query's method.**  It runs the query for each item of a collection of its input models, 1,000 items to a round trip.  It returns what every item returned.

<a id="gp-transaction"></a>

### `transaction`

For example, `-- generator: collection-method transaction=serializable` gives a query a collection method that begins a `serializable` transaction.  `transaction` sets the transaction that a [collection method](collection-methods.md#collection-methods) begins of its own.  Its values are:

- `on`, the default: a transaction at the database's default isolation level.
- `read-uncommitted`, `read-committed`, `repeatable-read`, `serializable` or `snapshot`: a transaction at the isolation level that the value names.
- `off`: no transaction.

The rules:

- **The collection method uses a transaction that the caller gives it, or an ambient one, in place of its own**, whatever `transaction` says.  See [transactions](collection-methods.md#collection-transactions).
- **`transaction` changes nothing for a query without `collection-method`.**
- **An isolation level that the query's database does not have is [SQLSRC297](../diagnostics.md#sqlsrc297).**

<a id="gp-method-type"></a>

### `method-type`

`method-type` says whether a query whose output is `repository` or `methods` gets asynchronous methods or synchronous ones.  Its values are:

- `async`, the default: the query gets an asynchronous method.
- `sync`: the query gets a [synchronous method](query-methods.md#synchronous-methods) in place of the asynchronous one.
- `both`: the query gets the two.

The rules:

- **A query's [collection method](collection-methods.md#collection-methods) follows `method-type` in the same way.**  So does a repository's [interface](repositories.md#repository-interface), which has a member for each method.
- **`method-type` changes nothing at the output `models` or `sql`.**
- **`method-type` takes no `on`, so the word `method-type` alone is [SQLSRC111](../diagnostics.md#sqlsrc111).**  Write one of its values.

<a id="gp-default"></a>

### `default`

For example, `-- generator: default` inside a query gives that query every default, whatever its file says.  `default` is no parameter of its own, and it takes no value.

- **At the place that says `default`, every parameter takes its default.**
- **The other words of that place apply over it.**

<a id="three-kinds-of-parameter"></a>

## Three kinds of parameter

SqlSource uses the word "parameter" for three different things: [query parameters](parameters.md#query-parameters), [generator parameters](#setting-generator-parameters) and [tokens](tokens.md#tokens).  The manual always says which.
