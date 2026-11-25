# Troubleshooting: it built, but something is wrong

Use this page when the build succeeds but something is wrong: an exception at run time, a wrong result, or nothing happens.  It also lists a build error that is not about your SQL.

<a id="at-run-time"></a>

## An exception at run time

- **`InvalidCastException` for a NodaTime, PostGIS, enum or composite type, on PostgreSQL.**  A repository made from a connection string makes connections that have none of the plugins and mappings of your data source.  See [a repository made from a connection string](../reference/databases/postgresql/query-methods.md#limit-connection-string-repository).
- **Null in a column whose property cannot be null, on SQL Server.**  With `ANSI_WARNINGS` and `ARITHABORT` both off, SQL Server returns null where an overflow or a division by zero is otherwise an error.  See [`ANSI_WARNINGS` off](../reference/databases/sqlserver/nullability.md#limit-ansi-warnings-off).
- **Null in a filtered column whose property cannot be null, on SQL Server.**  With `ANSI_NULLS` off, `WHERE x = @id` can keep a row whose `x` is null, which also reaches the other columns of a `LEFT JOIN`'s optional side.  See [`ANSI_NULLS` off](../reference/databases/sqlserver/nullability.md#limit-ansi-nulls-off).
- **A `SqlException`, and the transaction is rolled back, on SQL Server.**  An error before the rows can hide a later error of the batch that rolls the transaction back.  See [a hidden rollback](../reference/databases/sqlserver/query-methods.md#limit-hidden-rollback).
- **`SqlException` 130 for an empty or null list, on SQL Server.**  A list after a `NOT` inside an aggregate of your own fails when it is empty or null.  See [a list in an aggregate of your own](../reference/databases/sqlserver/query-methods.md#limit-list-in-an-aggregate).
- **`UnexpectedRowCountException`, `TransactionRolledBackException` or `CollectionItemException` is not caught by its type.**  Another assembly catches these exceptions by their type only when it can see them.  See [an exception that is not caught by its type](../reference/exceptions.md#limit-caught-by-type).
- **`InvalidCastException` from a query method with parameters, on a connection that wraps the driver's.**  The method works only on a connection whose commands take the driver's own parameters.  See [a connection that wraps the driver's](../reference/query-methods.md#limit-wrapped-connection).
- **`NotSupportedException` from a collection method, on a connection that wraps the driver's.**  Most such connections cannot run a batch of commands.  See [a collection method on a connection that wraps the driver's](../reference/collection-methods.md#limit-wrapped-connection).
- **`InvalidCastException` for an id type made by Vogen or StronglyTypedId.**  SqlSource converts a type only through methods marked `[SqlSourceConverter]`, and such a type has no method marked so.  See [Vogen and StronglyTypedId](../reference/converters.md#limit-vogen-and-stronglytypedid).
- **`InvalidOperationException`, "The connection source 'connections' of 'App.OrderRepository' is null."**  The connection source of a repository method, or the field, property or parameter that holds its factory, is null.  The message names it.  See [the connection](../reference/repository-connections.md#repository-connection) and [a factory that a repository holds](../reference/repository-connections.md#held-factory).
- **"Column 'name' is null.", or on SQL Server Microsoft.Data.SqlClient's `SqlNullValueException`, "Data is Null.".**  A null reached a property that cannot be null.  See [Say that a column can be null](nullability.md#nullable).

<a id="invalidcastexception"></a>

### `InvalidCastException`

Find the message of the exception in this list.  Each entry gives the cause, what to do, and the section that holds the rule.

- **The message names a NodaTime type, such as "Reading as 'NodaTime.Instant' is not supported".**  The project references `Npgsql.NodaTime`, so its PostgreSQL query methods read NodaTime's types.  But the data source that the method ran on never called `UseNodaTime()`.  Call `UseNodaTime()` on the builder of that data source, or turn NodaTime off.  `UseNodaTime()` inside EF Core's `UseNpgsql(...)` teaches only the data source of EF Core.  See [NodaTime on PostgreSQL](../reference/databases/postgresql/libraries.md#nodatime).
- **The message names a NetTopologySuite type, such as "Reading as 'NetTopologySuite.Geometries.Geometry' is not supported".**  The cause is the same as for NodaTime, with `Npgsql.NetTopologySuite` and `UseNetTopologySuite()`.  Call `UseNetTopologySuite()` on the builder of the data source, or turn NetTopologySuite off.  See [NetTopologySuite on PostgreSQL](../reference/databases/postgresql/libraries.md#nettopologysuite).
- **A NodaTime or NetTopologySuite message from a repository whose connection is a connection string.**  Such a repository makes a connection of its own, with no plugin at all.  Give the repository the data source instead.  See [a repository made from a connection string](../reference/databases/postgresql/query-methods.md#limit-connection-string-repository).
- **"Unable to cast object of type 'NetTopologySuite.Geometries.LineString' to type 'NetTopologySuite.Geometries.Point'".**  A column that you mapped to one shape held another.  Map the column to `NetTopologySuite.Geometries.Geometry`, or keep the other shapes out in the query.  See [NetTopologySuite on PostgreSQL](../reference/databases/postgresql/libraries.md#nettopologysuite).
- **The message names a class of yours, in a column of a string type that holds JSON**, such as a `text` or an `nvarchar(max)`.  Say that the column holds JSON with the JSON opt-in, `-> json OrderMetadata`.  The query methods then read and send the class through System.Text.Json's serializer.  See [the JSON opt-in](../reference/library-types-and-json.md#json-opt-in).
- **The message names a type of yours, such as a C# enum for a PostgreSQL enum, an id or an `Email`.**  A query method asked the driver to read a column, or send a parameter, as a C# type that the driver does not read.  Most often a [type override](../reference/type-mapping.md#setting-type-overrides), a [column type](../reference/model-properties.md#setting-column-types) or a `-- param:` line names the type.  The driver was never taught it.  Do one of these:
  - **Teach the driver the type**, on the data source of your application: Npgsql's `MapEnum<T>()` for an enum, `MapComposite<T>()` for a composite type, or the plugin of a library such as NodaTime or NetTopologySuite.
  - **Add converters**: two static methods marked `[SqlSourceConverter]`.  One converts from the type that the driver reads to your type, and the other converts back.  The generated method calls them.  See [converters](../reference/converters.md#converters) and [the recipe](custom-types.md#strongly-typed-id).
- **"Column 'name' is null.", or on SQL Server Microsoft.Data.SqlClient's `SqlNullValueException`, "Data is Null.".**  The column held a null, but its property cannot be null.  Give the column a nullable property, by its alias or a `-- column:` line, or keep the null out in the query.  See [How a row is read](../reference/query-methods.md#reading-rows) and [Say that a column can be null](nullability.md#nullable).
- **"Column 'name' holds a null element."**  An element of a converted array held a null, but the element cannot be null.  Give the element a type that can be null, or keep the null out in the query.  See [converters](../reference/converters.md#converters).
- **"Column 'name' holds a JSON null."**  A JSON column held the JSON `null`, which is no SQL `NULL`.  It was read into a class or a `JsonNode` whose property cannot be null.  Give the column a nullable property, or keep the JSON `null` out in the query.  See [How a row is read](../reference/query-methods.md#reading-rows).
- **From a method with parameters, on a connection that wraps the driver's.**  If the commands of the wrapper create or accept only parameters of their own type, the method fails, typically with an `InvalidCastException`.  Call the method on the driver's connection inside the wrapper.  See [the limit](../reference/query-methods.md#limit-wrapped-connection).

<a id="wrong-model"></a>

## A model that looks wrong

- **SQLSRC231 for a parameter after a `DECLARE`.**  Under the `mssql` dialect, a parameter after a `DECLARE` with no `;` can be taken for a variable, and describing the query stops with [SQLSRC231](../diagnostics.md#sqlsrc231).  End the `DECLARE` with a `;`.  See [a parameter taken for a variable](../reference/parameters.md#limit-parameter-taken-for-a-variable).
- **The input model has a property for a name that is not a query parameter**, such as `@x` written for an absolute value on PostgreSQL.  SqlSource finds query parameters by how the SQL is written, not by what the database does with it.  A MySQL or MariaDB user variable, and the parameter of a procedure that the SQL creates, are read as query parameters too.  Their queries need the output `sql`, under which these names change no generated code.  See [names that are read as parameters](../reference/parameters.md#names-read-as-parameters).
- **A property can be null though its column is never null, on PostgreSQL.**  SqlSource treats many columns as possibly null when it cannot show that they never are.  See [columns treated as possibly null](../reference/databases/postgresql/nullability.md#limit-columns-treated-as-nullable).
- **A property can be null though its column is never null, on SQL Server.**  SQL Server treats a column as possibly null when it cannot show that it never is.  See [columns treated as possibly null](../reference/databases/sqlserver/nullability.md#limit-columns-treated-as-nullable).
- **SQLSRC267 in a project that uses EF Core 8 or 9.**  EF Core's SQL Server provider brings Microsoft.Data.SqlClient 5.1, which is too old.  See [EF Core and Microsoft.Data.SqlClient](../reference/databases/sqlserver/types.md#limit-ef-core-sqlclient).
- **Only the first result set of a query has a model, on SQL Server.**  A query that returns several result sets gets a model of the first alone, and nothing is reported of the others.  See [several result sets](../reference/databases/sqlserver/index.md#limit-several-result-sets).
- **A compiler warning at a `cref` that names one of the exceptions of SqlSource.**  A project that uses another project's exceptions, and also references a project whose exceptions it cannot see, gets CS0419 or CS1574.  See [a `cref` to the exceptions](../reference/exceptions.md#limit-cref).
- **A compile error about `Microsoft.CodeAnalysis.EmbeddedAttribute`.**  SqlSource adds a declaration of this class to your project, and your own declaration of it must join it.  See [your own `EmbeddedAttribute`](../reference/attributes.md#limit-embedded-attribute).
- **SQLSRC019 or SQLSRC111 for a type override's key written `[dbo].[Email]`.**  A key cannot be written in brackets, as T-SQL writes a name.  See [a key in brackets](../reference/type-mapping.md#limit-key-in-brackets).
- **A compiler warning in a model for a `?` inside a type argument.**  SqlSource does not check a `?` inside a type argument against the constraints of a generic type.  See [a `?` inside a type argument](../reference/type-mapping.md#limit-nullable-type-argument).
- **One C# type cannot be converted two ways in two files.**  The converters of a C# type serve every column and parameter of that type in the project.  See [one conversion for each type](../reference/converters.md#limit-one-conversion-per-type).
- **A property is nullable, but its column is never null.**  You can say so in the `.sql` file.  See [Fix a nullable property](nullability.md).
- **A model is wrong for what your database returns, and no entry here explains it.**  Report it with a bundle of the query.  See [Reporting a wrong model](report-a-bug.md#report-a-bug).

<a id="wrong-result"></a>

## A method returns something you did not expect

- **Part of your SQL is missing from the generated SQL.**  SqlSource can read a comment or a string differently from your database, and then removes SQL that your database runs.  See [what is still read differently](../reference/dialects.md#still-read-differently).
- **An empty list after `IN` matches the rows whose value is null, on SQL Server.**  A connection with `ANSI_NULLS` off takes `= NULL` as true for a null.  See [`ANSI_NULLS` off](../reference/databases/sqlserver/query-methods.md#limit-ansi-nulls-off).
- **Dapper sends no value for a parameter such as `@account_id`.**  Dapper sends a property of the input model only to a parameter written as `@` and the property's name.  See [parameter names for Dapper](dapper.md#limit-parameter-names).
- **A `rowcount` method returns `-1`, on SQL Server.**  `SET NOCOUNT ON` in a query or command that has no parameters stays on for the connection's session.  See [`rowcount` after `SET NOCOUNT ON`](../reference/databases/sqlserver/query-methods.md#limit-rowcount-after-nocount).
- **SQLSRC039 on a repository nested in a generic type.**  A member that the repository inherits, or a parameter of its primary constructor, has the repository's name, and a `static` member gives its connection or holds its factory.  See [a static member of a base type with the repository's name](../reference/repository-connections.md#limit-static-member-of-the-repositorys-name).
- **CS0693 in a repository's generated file, or a repository that does not implement its interface.**  An inner type parameter or a nested type has the name of an outer type parameter.  See [a type parameter of an outer type that is hidden](../reference/repositories.md#limit-repeated-type-parameter).
- **CS4003 in a generated asynchronous method.**  A model is in a namespace with a part named `await`.  See [a model in a namespace with a part named `await`](../reference/query-methods.md#limit-model-namespace-await).

<a id="not-described"></a>

## A query is not described, or a file is not found

- **SQLSRC232 says that writing is not supported for an array whose element's name starts with `_`, on PostgreSQL.**  Npgsql cannot send an array of an enum or a range named with `_`.  See [an array parameter of an enum or a range named with `_`](../reference/databases/postgresql/types.md#limit-array-parameter-underscore).
- **SQLSRC231 says that an operator does not exist between `x[]` and `_x`, on PostgreSQL.**  Npgsql sends an array of `x` as the type named `_x`.  See [an array parameter of a type beside a type named `_` and its name](../reference/databases/postgresql/types.md#limit-array-of-a-renamed-type).
- **Two projects include one `.sql` file, and entries are missing from its sidecar.**  A run of the tool on one project can remove the entries that the other project needs.  See [a file in two projects](../reference/sidecars.md#limit-file-in-two-projects).
- **SQLSRC224 with `Npgsql cannot read the database's types`, on PostgreSQL.**  An operator `=` of your own on `oid` or `"char"`, in a schema before `pg_catalog` on the search path, stops Npgsql from reading the types.  See [an operator on `oid` or `"char"`](../reference/databases/postgresql/index.md#limit-operator-on-oid).
- **Describing is slow on a table with thousands of partitions, on PostgreSQL.**  The tool looks at every partition of a partitioned table.  See [many partitions](../reference/databases/postgresql/index.md#limit-many-partitions).
- **A function marked `IMMUTABLE` runs while a query is described, on PostgreSQL.**  PostgreSQL can compute a call of such a function with constant arguments in advance.  See [what describing does](../reference/databases/postgresql/index.md#what-describing-does).
- **SQLSRC228 for a `-- param:` type name, on SQL Server.**  SqlSource and SQL Server can read a character that Unicode added or reclassified after version 3.2 in different ways.  See [a type name with such a character](../reference/databases/sqlserver/index.md#limit-type-name-unicode).
- **SQLSRC231 for a parameter name with a character that Unicode added or reclassified after version 3.2, on SQL Server.**  SqlSource and SQL Server can read a character that Unicode added or reclassified after version 3.2 in different ways.  See [a parameter name with such a character](../reference/databases/sqlserver/index.md#limit-parameter-name-unicode).
- **A query that writes a query parameter's name in two ways fails when it is described, on SQL Server.**  SqlSource compares names ignoring case alone, and SQL Server by its collation.  See [a name written in two ways](../reference/databases/sqlserver/index.md#limit-parameter-two-spellings).
- **SQLSRC208, and nothing is described for a type whose `Source` is a constant or `nameof`.**  The tool reads `[SqlSourceGenerate]` from your C# files as text, without compiling them.  See [`Source` written as text](../reference/attributes.md#limit-source-written-as-text).
- **SQLSRC241 for a type whose `[SqlSourceGenerate]` is inside `#if RELEASE`.**  The tool reads the `Debug` configuration unless you give another.  Run `dotnet sqlsource describe --configuration Release`.  See [`Source` written as text](../reference/attributes.md#limit-source-written-as-text).
- **Sidecars for the files of a type whose `[SqlSourceGenerate]` is an attribute of your own.**  The tool knows the attribute by its name, not its namespace.  See [an attribute of your own named `SqlSourceGenerate`](../reference/attributes.md#limit-attribute-of-the-same-name).
- **A settings file or a `.sql` file is ignored when the project is built through a symbolic link.**  A path through a link and the real path reach different settings files.  See [a build through a symbolic link](../reference/settings-file.md#limit-symbolic-link).
- **SQLSRC241 for a `.sql` file that a build target of your own adds.**  The tool leaves out a file that a target adds while the build runs.  See [a file added by a target](../reference/msbuild.md#limit-file-added-by-a-target).
- **SQLSRC209 on MySQL, SQLite, Oracle or another database.**  Only PostgreSQL and SQL Server queries can be described, so give the file the output `sql`.  See [Use MySQL, SQLite, Oracle or another database](other-databases.md).
- **SQLSRC241 or SQLSRC244 after you pull.**  A query changed, and its sidecar was not described with it.  Run `dotnet sqlsource describe` and commit the sidecars.  See [Describing again](../reference/sidecars.md#describing-again).

<a id="sqlfluff"></a>

## sqlfluff does not do what you expect

- **`CV06` at the end of a query, and `sqlfluff fix` does not fix it.**  With `multiline_newline = True`, a query of several lines without `;` is reported.  End it with `;`.  See [CV06 on a query without `;`](sqlfluff.md#limit-cv06-without-semicolon).
- **sqlfluff reports nothing on the white space or comments after a query.**  The white space and comments between a query without `;` and the next query are neither reported nor fixed.  End the query with `;`.  See [white space and comments after a query without `;`](sqlfluff.md#limit-text-after-query).
- **pip upgrades sqlfluff when it installs the templater, or a pre-commit hook of sqlfluff fails to install.**  The templater needs sqlfluff 4.5 or later.  See [sqlfluff 4.4 or older](sqlfluff.md#limit-sqlfluff-version).

<a id="watch"></a>

## The watch does nothing

- **Saving a file starts no pass of `describe --watch`.**  The saves of editors other than Rider on macOS, and saves on Windows, have not been checked.  See [saves that are not seen](../reference/describing.md#limit-saves-not-seen).
- **In Visual Studio or VS Code, the models do not change after the watch writes a sidecar.**  What these editors show after a pass has not been checked.  See [editors that have not been checked](../reference/describing.md#limit-editors-not-checked).
- **After a folder is deleted and made again, saves in it start no pass.**  The watch does not see a few changes to folders far from your project.  See [folder changes that are not seen](../reference/describing.md#limit-folder-changes-not-seen).

<a id="secrets"></a>

## A secret in the output

- **A short password, or a password typed in the wrong place, shows in the output or a log.**  The tool does not hide a password shorter than four characters, or one that is the name of its logical database or dialect, but where a message gives it as the value of a password, as in `Password=pg;`.  See [a short password](../reference/connections.md#limit-short-password).
