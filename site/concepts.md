# Concepts

SqlSource reads the queries you keep in `.sql` files.  The **generator**, in the `SqlSource` package, runs inside the compiler whenever you build the project, and turns each query into C# code: for a described query its models, a type for its parameters and a type for its rows, and by default a method of a [repository](#repository) that runs it; or a string constant, or a method where the query has tokens, that holds its SQL for your own code to run.  The **tool**, in the `SqlSource.Tool` package, connects to your database, asks it what each query's parameters and columns are, and writes the answer into a **sidecar** file beside the `.sql` file.  You commit the sidecar with the project, and the generator reads it during the build, so that the build needs no database.  On a machine with a connection, the build [runs the tool itself](reference/msbuild.md#running-in-the-build) before it compiles.  A query whose [output](#output) is `models`, `methods` or `repository`, the default, must be described: the build reports an error for one that has no current entry in its sidecar.  A query whose output is `sql` needs neither the tool nor a sidecar.

```mermaid
flowchart LR
    sql[".sql file"] --> generator
    subgraph build["Your build"]
        generator["Generator"]
    end
    generator --> code["Constants, methods and models"]
    sql --> tool["Tool + your database"]
    tool --> sidecar["Sidecar"]
    sidecar -->|checked and read| generator
```

The rest of this page defines the words the manual uses.  Each is used in this one sense everywhere.

<a id="glossary"></a>

## Glossary

### The files

<a id="query"></a>

#### Query

The SQL that follows a `-- name:` line, up to the next `-- name:` line or the end of the file.  A file with no `-- name:` line is one query.  Each query becomes one member of the generated code, named after the query.  See [queries and names](reference/sql-files.md#queries-and-names).

<a id="marker"></a>

#### Marker

A line comment that starts its line and has the form `-- word: value`, where the word is one SqlSource knows, such as `-- name: GetUser` or `-- summary: Loads one user.`  Nothing else in a comment is read.  The [marker index](reference/sql-files.md#marker-index) lists every marker.

<a id="file-header"></a>

#### File header

The part of a `.sql` file before its first `-- name:` line.  It holds comments, such as a licence, and the markers that apply to every query in the file.  See [the file header](reference/sql-files.md#the-file-header).

<a id="sidecar"></a>

#### Sidecar

The tool's record of what the database said about each query's parameters and columns.  It is the file `Users.sql.json` beside `Users.sql`.  It is committed with the project, so that a build needs no database, and it is never edited by hand: the tool writes it.  The build checks it, and reports a query that must be described and has no current entry in it.  See [sidecar files](reference/sidecars.md#sidecar-files).

The generator builds the [models](generator.md#models) and the [query method](#query-method) of a query from its entry in the sidecar.

<a id="settings-file"></a>

#### Settings file

A file named `sqlsource.json` that sets [settings](settings.md#all-settings), such as the dialect and the output, for the `.sql` files of its folder and of every folder below it.  A file in a folder sets what differs there, and one that says `"root": true` keeps the settings files of the folders above it out.  See [settings files](settings.md#settings-files).

### What a query gets

<a id="output"></a>

#### Output

The setting that says what SqlSource produces for a query: its SQL alone, its SQL and its [models](generator.md#models), or its models and a [query method](#query-method), which by default is a [repository](#repository)'s.  It also decides which queries must be described: the ones the tool describes, and the ones the build checks against their sidecar.  See [the output setting](settings.md#setting-output).

<a id="query-method"></a>

#### Query method

What a described query whose [output](#output) is `repository` or `methods` becomes: an asynchronous method that sends the query's parameters, runs it and reads its rows into its models, or, under the generator parameter `method-type`, a [synchronous one](generator.md#synchronous-methods) instead or beside it.  Under `repository` it is an instance method of a [repository](#repository), which gets its own connection; under `methods` it takes the connection, and opens it when it is closed.  It holds the query's SQL, in place of a constant or a token method.  See [query methods](generator.md#query-methods).

<a id="repository"></a>

#### Repository

A type marked `[SqlSourceGenerate]` whose queries have the output `repository`, the default.  Each such query is an instance method of the type, which gets a connection from a member of the type, its connection source, such as a connection string or a data source, so the code that calls it passes none.  The type also gets an interface of those methods, for code that takes a repository and a test that fakes one.  See [repository methods](generator.md#repository-methods).

<a id="token-method"></a>

#### Token method

What a query with a [token](#token) becomes in place of a constant: a static method that takes the text of each token and returns the SQL with the tokens filled in.  See [token methods](generator.md#token-methods).

### Three kinds of parameter

SqlSource uses the word "parameter" for three different things.  The manual always says which.

<a id="query-parameter"></a>

#### Query parameter

A value you pass when you run a query, written in the SQL as `@` and a name: `@id` in `WHERE id = @id`.  SqlSource finds them and leaves them in the SQL exactly as written, but for a [list](reference/parameters.md#list-parameters), `IN @ids`, which a query method writes as one parameter for each value.  A `-- param:` marker gives one its database type, whether it can be null, or a C# type of your choice.  See [parameters and tokens](reference/parameters.md).

<a id="generator-parameter"></a>

#### Generator parameter

An option that changes what the generator does with a query, such as `keep-comments`, turned on or off, or given one of its values, such as `transaction=serializable`, by a word in a `-- generator:` marker or in a [settings file](#settings-file).  Each place sets only the options it names, and the others keep the values that the places further out give them.  It has nothing to do with the parameters of the SQL.  See [generator parameters](settings.md#setting-generator-parameters).

<a id="token"></a>

#### Token

A place in a query's SQL, written `{{table}}`, that your code fills in with text when it asks for the SQL.  A query parameter cannot stand for a table name or a clause; a token can.  A token is replaced by plain text substitution and nothing is escaped, so it must only carry text that your own code chooses.  See [parameters and tokens](reference/tokens.md).

### Two meanings of database

<a id="dialect"></a>

#### Dialect

The set of rules by which SqlSource reads a `.sql` file: where a comment or a string starts and ends, which differs from one database to the next.  It is named after a database, such as `postgres` or `mssql`, and it is set for a project, for some of its files, or for one file.  A dialect connects to nothing.  See [dialects](reference/dialects.md) and [the dialect setting](settings.md#setting-dialect).

<a id="logical-database"></a>

#### Logical database

A name for one particular database your queries run against, such as `billing`.  The tool uses it to choose which connection to describe a query through, a sidecar records it, and a [repository](#repository) that uses several marks a connection source for each.  A query belongs to the logical database its markers or settings name, and to the one named after its dialect when they name none.  See [the database setting](settings.md#setting-database).

The two answer different questions.  The dialect is *which kind of SQL* a file is written in; the logical database is *which database* the tool asks about it.  Several logical databases can share a dialect: `billing` and `reports` can both be `postgres`.

### The packages

<a id="generator"></a>

#### Generator

The part of the `SqlSource` package that runs in the compiler and writes C# from your `.sql` files.  See [the generator](generator.md).

<a id="tool"></a>

#### Tool

The `sqlsource` command of the `SqlSource.Tool` package.  Its `describe` command asks a database about your queries and writes the sidecars.  See [the tool](reference/cli.md#tool).

<a id="describe"></a>

#### Describe

To ask a database, without running a query, what the query's parameters and columns are.  The tool does it and writes the answer to the query's [sidecar](#sidecar).  It needs your schema, not your data.  See [which queries are described](reference/describing.md#which-queries).
