# SqlSource manual

This manual is for a .NET developer who keeps SQL in `.sql` files and uses SqlSource to turn it into code: how to write a `.sql` file, which settings change what is generated, how to describe your queries with the `sqlsource` tool, and what each error means.  If you have not used SqlSource yet, start with the [repository README](https://github.com/mbcrawfo/SqlSource/blob/main/README.md); this manual is the reference behind it.  Words such as *marker*, *token* and *sidecar* are defined in [concepts](concepts.md#glossary).

<a id="find-what-you-need"></a>

## Find what you need

| I want to | Go to |
|----|----|
| Understand how the generator, the tool and a sidecar fit together, or what a word means | [Concepts](concepts.md) |
| Write a `.sql` file, or look up a marker | [The marker index](reference/sql-files.md#marker-index) |
| Look up a setting, or where it can be written | [All settings](settings.md#all-settings) |
| Know which place wins when a setting is written twice | [Which place wins](settings.md#precedence) |
| Configure part of a project differently | [Configure one folder differently](settings.md#configure-a-folder), or the [recipe](recipes.md#folder-with-another-dialect) |
| Leave some `.sql` files out | [Which `.sql` files are included](settings.md#included-files), or the [recipe](recipes.md#leave-migration-scripts-out) |
| Give a query parameter a type | [Giving a parameter a type](reference/parameters.md#param-marker) |
| Filter by a list of values, `IN @ids`, or by an optional one | [List parameters](reference/parameters.md#list-parameters), or the [recipe](recipes.md#filter-by-a-list) |
| Choose a table name or a clause at run time | [Tokens](reference/tokens.md#tokens), or the [recipe](recipes.md#table-name-at-run-time) |
| Choose the dialect a file is read by | [Choosing a dialect](reference/dialects.md#choosing-a-dialect) |
| Use SQL Server, or another database | [SQL Server](reference/databases/sqlserver/index.md), [database support](reference/databases/index.md#database-support) |
| See what the generator writes, and where it goes | [The generator](generator.md#locations) |
| Know which models a query gets, what they are named, and what their properties are | [Models](generator.md#models) |
| Run a query with its generated method, and know what it returns and throws | [Query methods](generator.md#query-methods) |
| Give a repository its connection, from a connection string, a data source, a method or a connection it borrows | [The connection](generator.md#repository-connection), or the recipes for [a service of your own](recipes.md#connection-from-a-provider) and [SQL Server](recipes.md#sql-server-repository) |
| Fake a repository in a test, add members to its interface, or turn the interface off | [The interface](generator.md#repository-interface), [repository interfaces](settings.md#repository-interfaces) |
| Run queries on a connection or in a transaction that my own code manages, or with a timeout | [Methods that take a connection](generator.md#methods-that-take-a-connection), [transactions and timeouts](generator.md#repository-transactions) |
| Choose where a query's members go and how visible they are, for a file, a folder or one query | [Where the members go](settings.md#setting-member-location), [visibility](settings.md#setting-visibility), [the nested class](settings.md#setting-nested-class) |
| Choose what a query method returns its rows in, or give the methods `Async` names | [Collection type](settings.md#setting-collection-type), [generator parameters](settings.md#setting-generator-parameters) |
| Process each row as it is read, without holding them all | [Streaming rows](settings.md#streaming-rows) |
| Call a query from code that does not await, with a synchronous method | [Synchronous methods](generator.md#synchronous-methods), or the [recipe](recipes.md#run-without-awaiting) |
| Run several queries in a transaction | [The recipe](recipes.md#queries-in-a-transaction) |
| Run one query for each item of a collection, such as inserting many rows, in one call and one transaction | [Collection methods](generator.md#collection-methods), [`CollectionItemException`](generator.md#collection-item-exception), or the [recipe](recipes.md#insert-many-rows) |
| Use the queries with Dapper or ADO.NET | [The recipe](recipes.md#dapper-and-adonet) |
| Read rows into the models with Dapper | [The recipe](recipes.md#models-through-dapper) |
| Let a test project see the internals of another project, or catch another project's exceptions by type | [Projects that share internals](generator.md#projects-that-share-internals), [public exceptions](settings.md#public-exceptions), or the recipes for [a test project](recipes.md#test-project-sees-internals) and [two data projects](recipes.md#share-the-exceptions) |
| Install the tool and describe my queries | [Install the tool](reference/cli.md#install-the-tool) |
| Have the build describe my queries, or keep it from doing so | [Running in the build](reference/msbuild.md#running-in-the-build), [running the tool in the build](settings.md#online-build) |
| Give the tool a connection, keep it in a `.env` file, or work with several databases | [Connections](reference/connections.md#connections), [a `.env` file](reference/connections.md#env-file), or the [recipe](recipes.md#use-several-databases) |
| Keep one `.env` file at the root of a repository, or read another file or none | [A `.env` file](reference/connections.md#env-file), [the build's `.env` file](settings.md#setting-env-file) |
| Keep the sidecars current after a schema change | [After a schema change](reference/describing.md#after-a-schema-change) |
| Set up git for the sidecars, or mend a merge conflict in one | [Sidecars in git](reference/sidecars.md#sidecars-in-git) |
| Describe each query as I save it, and have my editor start that | [Describe on save](reference/describing.md#describe-on-save), [what your editor shows](reference/describing.md#what-your-editor-shows), or the [recipe](guides/describe-on-save.md#start-the-watcher-with-your-editor) |
| Set up CI | [Check in CI](reference/describing.md#check-in-ci), or the [recipe](guides/ci.md#check-sidecars-in-ci) |
| Look up a command, an option or an exit code | [The command reference](reference/cli.md#command-reference) |
| Report a model, a type or a nullability that is wrong, or a query the tool should describe and refuses | [Reporting a wrong model](guides/report-a-bug.md#report-a-bug), [what is renamed before you share it](reference/bundles.md#bundle-contents) |
| Check a bug-report bundle that someone gave me, or my own before I attach it | [Checking a bundle with `replay`](reference/bundles.md#replay) |
| Know what the tool needs of my database | [PostgreSQL](reference/databases/postgresql/index.md#connecting), [SQL Server](reference/databases/sqlserver/index.md#connecting) |
| Know why a column is recorded as possibly null | Which columns can be null in [PostgreSQL](reference/databases/postgresql/nullability.md#nullability) and [SQL Server](reference/databases/sqlserver/nullability.md#nullability) |
| Know which queries the tool cannot describe | What can be described in [PostgreSQL](reference/databases/postgresql/index.md#what-can-be-described) and [SQL Server](reference/databases/sqlserver/index.md#what-can-be-described) |
| Know which C# type a PostgreSQL or SQL Server type gets, or choose another | Types of [PostgreSQL](reference/databases/postgresql/types.md#type-map) and [SQL Server](reference/databases/sqlserver/types.md#types), [type overrides](settings.md#setting-type-overrides) |
| Read PostgreSQL's or SQL Server's dates and times as NodaTime's types, or turn them off | [NodaTime on PostgreSQL](reference/databases/postgresql/libraries.md#nodatime), [NodaTime on SQL Server](reference/databases/sqlserver/libraries.md#nodatime), [library types](settings.md#setting-library-types) |
| Read PostGIS's or SQL Server's spatial types as NetTopologySuite's geometries, or turn them off | [NetTopologySuite on PostgreSQL](reference/databases/postgresql/libraries.md#nettopologysuite), [NetTopologySuite on SQL Server](reference/databases/sqlserver/libraries.md#nettopologysuite), [library types](settings.md#setting-library-types) |
| Read and send JSON as System.Text.Json's types, or as a class of my own | JSON on [PostgreSQL](reference/databases/postgresql/libraries.md#json) and [SQL Server](reference/databases/sqlserver/libraries.md#json), [a class in a JSON column](settings.md#json-class), [the JSON opt-in](settings.md#json-opt-in), or the [recipe](recipes.md#class-in-a-json-column) |
| Give one column or one parameter a C# type of its own, such as an id's | [Column types](settings.md#setting-column-types), [giving a parameter a type](reference/parameters.md#param-marker), or the [recipe](recipes.md#strongly-typed-id) |
| Read and send a type of my own, such as an id or an `Email`, in the query methods | [Converters](settings.md#converters), or the [recipe](recipes.md#strongly-typed-id) |
| See a whole project that uses SqlSource, with its database | [The PostgreSQL example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/PostgresExamples/README.md), [the SQL Server example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/SqlServerExamples/README.md) |
| Look up a build error or a tool error | [Diagnostics](diagnostics.md) |
| Know where an error is reported, what it costs, or whether the build or the tool reports it | [How errors are reported](reference/errors.md) |
| Find a problem that is not an error | [Troubleshooting](guides/troubleshooting.md) |

<a id="the-three-parts"></a>

## The pages

- [Concepts](concepts.md): how the pieces fit together, and the terms the manual uses.
- [`.sql` files](reference/sql-files.md): the file format, and an index of every marker.
- [Settings](settings.md): every setting, where it can be set and which place wins.
- [Parameters and tokens](reference/parameters.md): query parameters, `-- param:`, list parameters and [tokens](reference/tokens.md).
- [Dialects](reference/dialects.md): how each database's comments and strings are read, and [which databases can be described](reference/databases/index.md#database-support).
- [The generator](generator.md): what the `SqlSource` package generates.
- [The tool](reference/cli.md): the `SqlSource.Tool` package and its `sqlsource` command.
- [PostgreSQL](reference/databases/postgresql/index.md): how SqlSource works with PostgreSQL.
- [SQL Server](reference/databases/sqlserver/index.md): how SqlSource works with SQL Server.
- [Recipes](recipes.md): short how-tos, each a sample and a link to the rules.
- [How errors are reported](reference/errors.md): warnings and information, where an error is reported, what it costs, whether the build or the tool reports it, and every error by area.
- [Diagnostics](diagnostics.md): what each `SQLSRC` error means and how to fix it.
