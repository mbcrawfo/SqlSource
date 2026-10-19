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
| Use a database other than PostgreSQL | [Database support](dialects.md#database-support) |
| See what the generator writes, and where it goes | [The generator](generator.md#locations) |
| Use the queries with Dapper or ADO.NET | [The recipe](recipes.md#dapper-and-adonet) |
| Let a test project see the internals of another project | [Projects that share internals](generator.md#projects-that-share-internals) |
| Install the tool and describe my queries | [Install the tool](tool.md#install-the-tool) |
| Give the tool a connection, or work with several databases | [Connections](tool.md#connections), or the [recipe](recipes.md#use-several-databases) |
| Keep the sidecars current after a schema change | [After a schema change](tool.md#after-a-schema-change) |
| Set up CI | [Check in CI](tool.md#check-in-ci), or the [recipe](recipes.md#check-sidecars-in-ci) |
| Look up a command, an option or an exit code | [The command reference](tool.md#command-reference) |
| Know what the tool needs of my PostgreSQL database | [PostgreSQL](postgresql.md#connecting) |
| Know why a column is recorded as possibly null | [Which columns can be null](postgresql.md#nullability) |
| Know which queries the tool cannot describe | [What can be described](postgresql.md#what-can-be-described) |
| Know which C# type a PostgreSQL type gets, or choose another | [Types](postgresql.md#type-map), [type overrides](settings.md#setting-type-overrides) |
| Look up a build error or a tool error | [Diagnostics](diagnostics.md) |
| Find a problem that is not an error | The known issues of [`.sql` files](sql-files.md#known-issues), [settings](settings.md#known-issues), [parameters and tokens](parameters-and-tokens.md#known-issues), [dialects](dialects.md#known-issues), [the generator](generator.md#known-issues), [the tool](tool.md#known-issues) and [PostgreSQL](postgresql.md#known-issues) |

<a id="what-works-today"></a>

## What works today

SqlSource is in early development.  This table is the one list of what works in the current release and what is still to come.  Where a page for a coming feature exists, it marks the feature with a callout that links back here.

| Feature | Status | Where documented |
|----|----|----|
| Queries as string constants | Available | [Constants](generator.md#constants) |
| Methods for queries with tokens | Available | [Token methods](generator.md#token-methods) |
| Reading `.sql` files by the rules of your database's dialect | Available | [Dialects](dialects.md) |
| Settings, and an error for a value that is wrong | Available | [Settings](settings.md#all-settings) |
| The `Output` setting | Works today: it decides which queries the tool describes and the build checks against their sidecar; no effect yet on generated code | [Output](settings.md#setting-output) |
| The settings for models and methods: model suffixes, namespace, shape and name, collection type, `MethodLocation`, and the generator parameters `sort-input`, `sort-output`, `no-table-models` and `async-method-suffix` | Accepted; no effect yet | [All settings](settings.md#all-settings) |
| Result shapes (`-- name: GetUser -> one`), and `null` or `not null` in a `-- param:` marker | Accepted; no effect yet: both are checked, and the tool records a parameter's `null` in the sidecar | [Result shapes](sql-files.md#result-shapes), [giving a parameter a type](parameters-and-tokens.md#param-marker) |
| Describing queries with the `sqlsource` tool, sidecar files, and `--check` | Available | [The tool](tool.md) |
| An error in the build for a query whose sidecar entry is missing or out of date | Available | [Sidecar files](tool.md#sidecar-files), [SQLSRC241](diagnostics.md#sqlsrc241), [SQLSRC244](diagnostics.md#sqlsrc244) |
| Describing PostgreSQL queries | Available | [PostgreSQL](postgresql.md) |
| The C# type of each column and parameter of a described PostgreSQL query, and an error in the build for one that has none | Available; no effect yet on generated code | [Types](postgresql.md#type-map) |
| Describing SQL Server queries | Coming ([SQLSRC216](diagnostics.md#sqlsrc216) until then) | [SQL Server](sqlserver.md) |
| Models for the parameters and rows of a query | Coming | [Models](generator.md#models) |
| Generated query methods | Coming | [Query methods](generator.md#query-methods) |
| Library maps for the types of libraries such as NodaTime | Coming | Not documented until it is delivered |
| Custom type mapping: the type override, `SqlSourceTypeOverrides` and `-- type-override:` | Accepted and checked; takes effect with models in a later release | [Type overrides](settings.md#setting-type-overrides); for a PostgreSQL type that cannot be sent as a parameter, see [SQLSRC232](diagnostics.md#sqlsrc232) |
| Build integration: the tool running in your build | Coming | Not documented until it is delivered; today you run the tool yourself: [install the tool](tool.md#install-the-tool) |
| Bug-report bundles | Coming | Not documented until it is delivered; today attach a [log](tool.md#logs) |

"Accepted; no effect yet" means SqlSource checks the setting and reports an error for a value that is not valid, but the setting changes no generated code yet.

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
