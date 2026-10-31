# SqlSource manual

This manual is for a .NET developer who keeps SQL in `.sql` files and uses SqlSource to turn it into code: how to write a `.sql` file, which settings change what is generated, how to describe your queries with the `sqlsource` tool, and what each error means.  If you have not used SqlSource yet, start with the [repository README](../../README.md); this manual is the reference behind it.  Words such as *marker*, *token* and *sidecar* are defined in [concepts](concepts.md#glossary).

## Find what you need

| I want to | Go to |
|----|----|
| Understand how the generator, the tool and a sidecar fit together, or what a word means | [Concepts](concepts.md) |
| Write a `.sql` file, or look up a marker | [The marker index](sql-files.md#marker-index) |
| Look up a setting, or where it can be written | [All settings](settings.md#all-settings) |
| Know which place wins when a setting is written twice | [Which place wins](settings.md#precedence) |
| Configure part of a project differently | [Configure one folder differently](settings.md#configure-a-folder), or the [recipe](recipes.md#folder-with-another-dialect) |
| Leave some `.sql` files out | [Which `.sql` files are included](settings.md#included-files), or the [recipe](recipes.md#leave-migration-scripts-out) |
| Give a query parameter a type | [Giving a parameter a type](parameters-and-tokens.md#param-marker) |
| Choose a table name or a clause at run time | [Tokens](parameters-and-tokens.md#tokens), or the [recipe](recipes.md#table-name-at-run-time) |
| Choose the dialect a file is read by | [Choosing a dialect](dialects.md#choosing-a-dialect) |
| Use SQL Server, or another database | [SQL Server](sqlserver.md), [database support](dialects.md#database-support) |
| See what the generator writes, and where it goes | [The generator](generator.md#locations) |
| Know which models a query gets, what they are named, and what their properties are | [Models](generator.md#models) |
| Run a query with its generated method, and know what it returns and throws | [Query methods](generator.md#query-methods) |
| Choose what a query method returns its rows in, where the methods go, or give them `Async` names | [Collection type](settings.md#setting-collection-type), [where the methods go](settings.md#setting-method-location), [generator parameters](settings.md#setting-generator-parameters) |
| Process each row as it is read, without holding them all | [Streaming rows](settings.md#streaming-rows) |
| Run several queries in a transaction | [The recipe](recipes.md#queries-in-a-transaction) |
| Use the queries with Dapper or ADO.NET | [The recipe](recipes.md#dapper-and-adonet) |
| Read rows into the models with Dapper | [The recipe](recipes.md#models-through-dapper) |
| Let a test project see the internals of another project | [Projects that share internals](generator.md#projects-that-share-internals) |
| Install the tool and describe my queries | [Install the tool](tool.md#install-the-tool) |
| Give the tool a connection, or work with several databases | [Connections](tool.md#connections), or the [recipe](recipes.md#use-several-databases) |
| Keep the sidecars current after a schema change | [After a schema change](tool.md#after-a-schema-change) |
| Set up CI | [Check in CI](tool.md#check-in-ci), or the [recipe](recipes.md#check-sidecars-in-ci) |
| Look up a command, an option or an exit code | [The command reference](tool.md#command-reference) |
| Know what the tool needs of my database | [PostgreSQL](postgresql.md#connecting), [SQL Server](sqlserver.md#connecting) |
| Know why a column is recorded as possibly null | Which columns can be null in [PostgreSQL](postgresql.md#nullability) and [SQL Server](sqlserver.md#nullability) |
| Know which queries the tool cannot describe | What can be described in [PostgreSQL](postgresql.md#what-can-be-described) and [SQL Server](sqlserver.md#what-can-be-described) |
| Know which C# type a PostgreSQL or SQL Server type gets, or choose another | Types of [PostgreSQL](postgresql.md#type-map) and [SQL Server](sqlserver.md#types), [type overrides](settings.md#setting-type-overrides) |
| Read PostgreSQL's or SQL Server's dates and times as NodaTime's types, or turn them off | [NodaTime on PostgreSQL](postgresql.md#nodatime), [NodaTime on SQL Server](sqlserver.md#nodatime), [library types](settings.md#setting-library-types) |
| Read and send JSON as System.Text.Json's types, or as a class of my own | JSON on [PostgreSQL](postgresql.md#json) and [SQL Server](sqlserver.md#json), [a class in a JSON column](settings.md#json-class), [the JSON opt-in](settings.md#json-opt-in), or the [recipe](recipes.md#class-in-a-json-column) |
| Give one column or one parameter a C# type of its own, such as an id's | [Column types](settings.md#setting-column-types), [giving a parameter a type](parameters-and-tokens.md#param-marker), or the [recipe](recipes.md#strongly-typed-id) |
| Read and send a type of my own, such as an id or an `Email`, in the query methods | [Converters](settings.md#converters), or the [recipe](recipes.md#strongly-typed-id) |
| See a whole project that uses SqlSource, with its database | [The PostgreSQL example](../../examples/PostgresExamples/README.md), [the SQL Server example](../../examples/SqlServerExamples/README.md) |
| Look up a build error or a tool error | [Diagnostics](diagnostics.md) |
| Find a problem that is not an error | The known issues of [`.sql` files](sql-files.md#known-issues), [settings](settings.md#known-issues), [parameters and tokens](parameters-and-tokens.md#known-issues), [dialects](dialects.md#known-issues), [the generator](generator.md#known-issues), [the tool](tool.md#known-issues), [PostgreSQL](postgresql.md#known-issues) and [SQL Server](sqlserver.md#known-issues) |

<a id="what-works-today"></a>

## What works today

SqlSource is in early development.  This table is the one list of what works in the current release and what is still to come.  Where a page for a coming feature exists, it marks the feature with a callout that links back here.

| Feature | Status | Where documented |
|----|----|----|
| Queries as string constants, for the output `sql` or `models` | Available | [Constants](generator.md#constants) |
| Methods for queries with tokens | Available | [Token methods](generator.md#token-methods) |
| Reading `.sql` files by the rules of your database's dialect | Available | [Dialects](dialects.md) |
| Settings, and an error for a value that is wrong | Available | [Settings](settings.md#all-settings) |
| Settings files: `sqlsource.json` in a folder, for it and every folder below | Available | [Settings files](settings.md#settings-files) |
| The `Output` setting | Available | [Output](settings.md#setting-output) |
| The settings for models: model suffixes, namespace, shape and name, and the generator parameters `sort-input`, `sort-output` and `no-table-models` | Available | [All settings](settings.md#all-settings) |
| The settings for query methods: collection type, `MethodLocation`, and the generator parameters `async-method-suffix` and `no-array-pool` | Available | [All settings](settings.md#all-settings) |
| `null` or `not null` in a `-- param:` marker | Available | [Giving a parameter a type](parameters-and-tokens.md#param-marker) |
| Result shapes (`-- name: GetUser -> one`) | Available | [Result shapes](sql-files.md#result-shapes) |
| Describing queries with the `sqlsource` tool, sidecar files, and `--check` | Available | [The tool](tool.md) |
| An error in the build for a query whose sidecar entry is missing or out of date | Available | [Sidecar files](tool.md#sidecar-files), [SQLSRC241](diagnostics.md#sqlsrc241), [SQLSRC244](diagnostics.md#sqlsrc244) |
| Describing PostgreSQL queries | Available | [PostgreSQL](postgresql.md) |
| The C# type of each column and parameter of a described PostgreSQL query, and an error in the build for one that has none | Available | [Types](postgresql.md#type-map) |
| Describing SQL Server queries | Available | [SQL Server](sqlserver.md) |
| The C# type of each column and parameter of a described SQL Server query, and an error in the build for one that has none | Available | [Types](sqlserver.md#types) |
| Models for the parameters and rows of a query: their names, namespace, shape, properties and nullability, table models and shared models | Available | [Models](generator.md#models), [PostgreSQL](postgresql.md#models), [SQL Server](sqlserver.md#models) |
| A column's nullability set by `!` or `?` at the end of its alias, or by a `-- column:` marker, which leaves the column's name alone for Dapper | Available | [Which properties can be null](generator.md#model-nullability), [the `-- column:` marker](generator.md#column-marker) |
| Generated query methods, which run a query on a connection and read its rows into its models, `UnexpectedRowCountException`, and on SQL Server `TransactionRolledBackException` | Available | [Query methods](generator.md#query-methods) |
| Streaming rows: a `many` method that returns `IAsyncEnumerable<T>`, by the collection type `IAsyncEnumerable`, and reads each row as the loop asks for it | Available | [Streaming rows](settings.md#streaming-rows) |
| Each parameter of a query method sent as the database type it was described with | Available | [How a parameter is sent](generator.md#method-parameters), [PostgreSQL](postgresql.md#query-methods), [SQL Server](sqlserver.md#method-parameters) |
| Converters: methods of your own that convert between a C# type of your choice and one the driver reads and sends, for the query methods | Available | [Converters](settings.md#converters), [the recipe](recipes.md#strongly-typed-id) |
| Library maps for the types of libraries such as NodaTime, with `libraryTypes` in a settings file and `-- library-types:` | NodaTime on PostgreSQL and SQL Server, and System.Text.Json on both, available; NetTopologySuite coming | [NodaTime on PostgreSQL](postgresql.md#nodatime), [NodaTime on SQL Server](sqlserver.md#nodatime), JSON on [PostgreSQL](postgresql.md#json) and [SQL Server](sqlserver.md#json), [library types](settings.md#setting-library-types) |
| A class of your own in a JSON column, read and sent through System.Text.Json's serializer, and the JSON opt-in for a column of a string type that holds JSON | Available | [A class in a JSON column](settings.md#json-class), [the JSON opt-in](settings.md#json-opt-in) |
| Custom type mapping: the type override, `typeOverrides` in a settings file and `-- type-override:` | Available | [Type overrides](settings.md#setting-type-overrides); for a PostgreSQL type that cannot be sent as a parameter, see [SQLSRC232](diagnostics.md#sqlsrc232) |
| Column types: a C# type for one column, by the column it is read from, with `columnTypes` in a settings file or a `-- column:` in the file header, or by a `-- column:` inside a query, and for one parameter by `-- param: @id -> Type` | Available | [Column types](settings.md#setting-column-types), [giving a parameter a type](parameters-and-tokens.md#param-marker) |
| C# types written by their short names, `UserId` for `MyApp.UserId`, wherever SqlSource takes a C# type | Available | [Type overrides](settings.md#setting-type-overrides) |
| Build integration: the tool running in your build | Coming | Not documented until it is delivered; today you run the tool yourself: [install the tool](tool.md#install-the-tool) |
| Bug-report bundles | Coming | Not documented until it is delivered; today attach a [log](tool.md#logs) |

## The pages

- [Concepts](concepts.md): how the pieces fit together, and the terms the manual uses.
- [`.sql` files](sql-files.md): the file format, and an index of every marker.
- [Settings](settings.md): every setting, where it can be set and which place wins.
- [Parameters and tokens](parameters-and-tokens.md): query parameters, `-- param:` and tokens.
- [Dialects](dialects.md): how each database's comments and strings are read, and which databases can be described.
- [The generator](generator.md): what the `SqlSource` package generates.
- [The tool](tool.md): the `SqlSource.Tool` package and its `sqlsource` command.
- [PostgreSQL](postgresql.md): how SqlSource works with PostgreSQL.
- [SQL Server](sqlserver.md): how SqlSource works with SQL Server.
- [Recipes](recipes.md): short how-tos, each a sample and a link to the rules.
- [Diagnostics](diagnostics.md): what each `SQLSRC` error means and how to fix it.
