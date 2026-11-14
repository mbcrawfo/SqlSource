# Generator parameters

<a id="setting-generator-parameters"></a>

## Generator parameters

An option that changes what the generator does with a query, such as `keep-comments`, turned on or off, or given one of its values, such as `transaction=serializable`, by a word in a `-- generator:` marker or in a [settings file](settings-file.md#settings-files).  Each place sets only the options it names, and the others keep the values that the places further out give them.  It has nothing to do with the parameters of the SQL.  See [generator parameters](#setting-generator-parameters).

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

A [generator parameter](#setting-generator-parameters) is an option that changes what SqlSource does with a query.  Each takes one of its own words, its values, and a word in a `-- generator:` line, or in `generatorParameters` in a settings file, gives it one.  Most are on or off, the common case, `transaction` also takes an isolation level, and `method-type` takes `async`, `sync` or `both`.  A query that no word reaches gets every default: `token-validation`, `table-models`, `array-pool`, `list-padding` and `transaction` are on, `method-type` is `async`, and the others are off.

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

**A word** is `name`, `name=value` or `name=default`, with no space around its `=`, since a `-- generator:` line separates its words by white space.  `name=value` gives the parameter one of its values, `keep-comments=off` or `transaction=serializable`, and `name` alone is `name=on`, which `method-type` does not take.  `name=default` gives the parameter its default, whatever the places below say.  Names and values are matched ignoring case: `Keep-Comments=OFF` is `keep-comments=off`.

**`default` alone gives every parameter its default**, and the other words of its place apply over it, wherever it stands among them.  `-- generator: default keep-comments` is every default with `keep-comments` on, and so is `-- generator: keep-comments default`.  It may be repeated.

**Each place sets parameters over the places below it.**  A place changes only the parameters it names, and every other parameter keeps the value the places below give it.  In the example, the file header turns `keep-comments` on for every query of the file.  `Report` keeps its comments.  `Total` turns `token-validation` off and says nothing of `keep-comments`, so it skips the token check and keeps its comments too.  `Plain` gives every parameter its default, whatever its file says, so it does not keep its comments.

The places, from the farthest to the nearest:

1. `generatorParameters` in each settings file of the file's folder and of the folders above it, up to the first that says `"root": true`, from the farthest to the nearest.
2. The `-- generator:` lines in the file header.
3. The `-- generator:` lines inside the query.

A parameter has the value of the nearest place that names it, and its default when none does.  `default` names every parameter, so nothing below the place that says it matters.

**The lines of one place add up.**  Several `-- generator:` lines in the file header are one place, and so are several inside one query.  A file with no `-- name:` line is one query and one place, so all of its `-- generator:` lines add up, wherever they stand: `keep-comments` at its top and `keep-comments=off` lower down contradict each other, and the second does not override the first.

**In a settings file** the value is an array of strings, one word in each: `"generatorParameters": ["sort-output", "token-validation=off"]`.  `[]` names no parameter, so it changes nothing, and `["default"]` gives every parameter its default, whatever the folders above say.  An element that is empty or white space is skipped.

**A mistake** is reported where it is written:

| Mistake | In a `-- generator:` line | In a settings file |
|----|----|----|
| A word that is not a generator parameter, such as `keep-coments` | [SQLSRC109](../diagnostics.md#sqlsrc109) | [SQLSRC019](../diagnostics.md#sqlsrc019) at the element, and the other elements apply |
| A value that is not one of the parameter's, or `default`, such as `keep-comments=yes`, `transaction=snapshoot` or `method-type` alone, which is `method-type=on`, or `default` with a value | [SQLSRC111](../diagnostics.md#sqlsrc111) | [SQLSRC019](../diagnostics.md#sqlsrc019) at the element, and the other elements apply |
| A line with nothing after the colon | [SQLSRC110](../diagnostics.md#sqlsrc110) | |
| One parameter given two different values in one place, such as `keep-comments` and `keep-comments=off`.  `keep-comments=default` beside either is one too, even where the default is the same | [SQLSRC112](../diagnostics.md#sqlsrc112), at the later word | [SQLSRC020](../diagnostics.md#sqlsrc020), at the later element, and the first applies |

A mistake in a `-- generator:` line leaves its file without members until it is fixed, as [a value that is wrong](settings.md#a-value-that-is-wrong) says.  The same value given twice is no mistake, and nor is `default` beside other words.

<a id="gp-keep-comments"></a>

### `keep-comments`

Comments and blank lines stay in the generated SQL: see [what reaches the generated SQL](sql-files.md#generated-sql).  A PostgreSQL [collection method](databases/postgresql/query-methods.md#collection-methods) sends its SQL without them all the same

<a id="gp-token-validation"></a>

### `token-validation`

The method of a query with tokens checks its arguments, and `token-validation=off` turns the check off: see [validation](tokens.md#token-validation)

<a id="gp-sort-input"></a>

<a id="gp-sort-output"></a>

### `sort-input`, `sort-output`

The properties of the query's input model, or of its output model, are sorted by name: see [the order of the properties](model-properties.md#property-order)

<a id="gp-table-models"></a>

### `table-models`

A query that reads a whole table gets a row model named after the table, and with `table-models=off` it gets one named after the query: see [table models](models.md#table-models)

<a id="gp-async-method-suffix"></a>

### `async-method-suffix`

The name of a query's [method](connection-methods.md#method-signature) ends with `Async`: `GetUserAsync` for `GetUser`.  A synchronous method's never does, and under `method-type=both` an asynchronous method's always does, as [synchronous methods](query-methods.md#synchronous-methods) say

<a id="gp-array-pool"></a>

### `array-pool`

A [`many`](return-types.md#method-shapes) method that returns a collection reads its rows into buffers borrowed from .NET's shared array pool, unless it returns an `ImmutableList<T>` or an `IImmutableList<T>`, and so does a [collection method](collection-methods.md#collection-methods) that reads rows, of the shape `many`, `one` or `one-optional`.  Once the pool holds buffers of the sizes it needs, a method that reads through the pool allocates no collection for a result of up to about 1,000 rows but the one it returns.  With `array-pool=off`, every such method reads its rows into a collection that it allocates and grows, as hand-written code usually does, and as a method that returns one of the two immutable lists always does.  Turn it off to keep the pool out of a project, or to compare the two.  A method that [streams its rows](return-types.md#streaming-rows) holds no collection, so it is the same with `array-pool` on or off

<a id="gp-list-padding"></a>

### `list-padding`

A query method pads each [list parameter](parameters.md#list-parameters) to one of a few sizes, 1, 2, 4 and so on to 512 and then each multiple of 512, with its last value repeated, so that the database sees a few forms of the query, and keeps a plan of each, whatever the counts: see [padding](parameters.md#list-padding).  With `list-padding=off`, a query method sends each list at its own count of values, so each count is a form of its own, and a list sends a parameter for each of its values alone

<a id="gp-collection-method"></a>

### `collection-method`

A query with parameters whose output is `repository` or `methods` also gets a [collection method](collection-methods.md#collection-methods), an overload of its method that runs it for each item of a collection of its input models, 1,000 items to a round trip, and returns what every item returned

<a id="gp-transaction"></a>

### `transaction`

The transaction that a collection method begins of its own: at the database's default isolation level under `on`, at the level that `read-uncommitted`, `read-committed`, `repeatable-read`, `serializable` or `snapshot` names, and none under `off`.  A transaction that the caller gives the method, or an ambient one, is used instead, whatever it says: see [transactions](collection-methods.md#collection-transactions).  It changes nothing for a query without `collection-method`, and an isolation level that the query's database does not have is [SQLSRC297](../diagnostics.md#sqlsrc297)

<a id="gp-method-type"></a>

### `method-type`

Whether a query whose output is `methods` or `repository` gets asynchronous methods or synchronous ones: under `async` it gets an asynchronous method, under `sync` a [synchronous method](query-methods.md#synchronous-methods) in its place, and under `both` the two.  A query's [collection method](collection-methods.md#collection-methods) follows it in the same way, and so does a repository's [interface](repositories.md#repository-interface), which has a member for each.  It changes nothing at the output `sql` or `models`.  `method-type` takes no `on`, so the word `method-type` alone is [SQLSRC111](../diagnostics.md#sqlsrc111): write one of its values

<a id="gp-default"></a>

### `default`

No parameter of its own: every parameter takes its default at the place that says it, and the place's other words apply over it

<a id="three-kinds-of-parameter"></a>

## Three kinds of parameter

SqlSource uses the word "parameter" for three different things: [query parameters](parameters.md#query-parameters), [generator parameters](#setting-generator-parameters) and [tokens](tokens.md#tokens).  The manual always says which.
