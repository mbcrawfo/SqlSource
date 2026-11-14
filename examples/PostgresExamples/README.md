# PostgresExamples

A small project that uses SqlSource with PostgreSQL: what you write, what you get, and how to run the tool.  Its queries run through the methods SqlSource generates, and, in two folders, through Dapper over the generated SQL and models.  It builds with the rest of the repository, so what it shows is what SqlSource does today.  For what SqlSource is and how to install it, start with the [repository's README](../../README.md); the [manual](../../site/index.md) has everything this page does not explain.

## What is in it

| Path | What it shows |
|----|----|
| [`schema.sql`](schema.sql) | The database: users, their orders, the lines of each order and the delivery of an order, with a few rows |
| [`docker-compose.yml`](docker-compose.yml) | Builds and starts PostgreSQL with that schema |
| [`.env.example`](.env.example) | The connection to that database, which the tool reads once it is copied to `.env` |
| [`Postgis/Dockerfile`](Postgis/Dockerfile) | The image that `docker-compose.yml` builds: PostgreSQL 18.6 with PostGIS, which the deliveries' `geography` needs, at an exact version |
| [`Sql/`](Sql) | The output `sql`: `CountUsers` as a constant, and `CountRows` as a method that fills in a [token](../../site/reference/tokens.md#tokens), in a nested class that the file header's [`-- nested-class: Counts`](../../site/settings.md#setting-nested-class) names, from `CountQueries.sql`, the file named for [`CountQueries`](Sql/CountQueries.cs).  They are not described, so they have no sidecar and need no connection |
| [`Models/`](Models) | The output `models`, for every `.sql` file of the folder, which [`StoreQueries`](Models/StoreQueries.cs) takes with [`Source = "."`](../../site/settings.md#setting-source): each query gets a model of its parameters and one of its rows.  `GetOrder` has a nullable column and a `timestamptz` read as a `DateTimeOffset` by the project's [type override](../../site/settings.md#setting-type-overrides); `ListOrderTotals` has a sum that the tool shows is never null, `CountShippedOrders` has a count that the tool shows is never null, and `ListUserDisplayNames` makes a column not null with a [`-- column:` marker](../../site/generator.md#column-marker); `GetOrderTags` has an array column, read into the default record; `GetUser` is `SELECT *`, so its rows are the [table's model](../../site/generator.md#table-models), `UsersDto` |
| [`Methods/`](Methods) | The output `methods`, which [`Methods/sqlsource.json`](Methods/sqlsource.json) sets for its folder: each query gets its models and a [method](../../site/generator.md#query-methods) that runs it, an extension method of the connection, which [`UserQueries`](Methods/UserQueries.cs), [`OrderLineQueries`](Methods/OrderLineQueries.cs) and [`DeliveryQueries`](Methods/DeliveryQueries.cs) call with the caller's `CancellationToken`, but for a synchronous method, each from the file [named for it](../../site/settings.md#setting-source).  [`OrderQueries.sql`](Methods/OrderQueries.sql) puts its methods on [`OrderQueries`](Methods/OrderQueries.cs) itself, by a [`-- member-location:`](../../site/settings.md#setting-member-location) marker on each query: `ListOrdersOfUser`, `FindOrder` and `ListOrdersByStatuses`, by `-- member-location: class` and [`-- visibility: public`](../../site/settings.md#setting-visibility), are public static methods of the class, which take the connection first, `OrderQueries.FindOrderAsync(connection, new FindOrderParams(1))`, and `CountOrderLines`, by `-- member-location: nested-class`, is a method of the class's private nested class `Sql`, which only the class's own `HasLinesAsync` calls.  `ListOrdersByStatuses` filters by a [list parameter](../../site/reference/parameters.md#list-parameters), `status IN @statuses`, written with no parentheses, so `ListOrdersByStatusesParams.Statuses` is an `IEnumerable<string>?` and the method sends a parameter for each status; `ListOrdersNotCancelledAsync` passes it an array, and a null list, by the query's `@statuses IS NULL`, returns every order.  The file's header writes a [generator parameter](../../site/settings.md#setting-generator-parameters), `-- generator: async-method-suffix`, so every method's name ends with `Async`, and `FindOrder` adds another with its own `-- generator: sort-output`, so it gets both and `FindOrderDto` has its properties sorted by name.  The [result shape](../../site/reference/sql-files.md#result-shapes) after a query's name says what its method returns: `ListOrderLines` and `ListLatestOrderPerProduct` are `many` by default and return an array, `FindUserByEmail` is `one-optional` and returns the row or null, `ChangeUserEmail` is `rowcount` and returns the count of rows updated, and `ListUsersNewestFirst` returns an `IReadOnlyList` by its [`-- collection-type:` marker](../../site/settings.md#setting-collection-type).  `StreamUsersOldestFirst`'s marker, `-- collection-type: IAsyncEnumerable`, makes its method [stream its rows](../../site/settings.md#streaming-rows), which `WriteUserNamesAsync` takes in an `await foreach`, one at a time, as each is read.  `ListUsersSortedBy` has a [token](../../site/reference/tokens.md#tokens), a `string` argument of its method.  `AddUser` is `one` and returns the new user's id, the row of its `RETURNING` clause, and its [`-- generator: collection-method`](../../site/generator.md#collection-methods) gives it a second method, an overload that takes a collection of `AddUserParams`, which `AddUsersAsync` calls: it adds every user in one transaction of its own, 1,000 to a round trip, and returns their rows in the order of the users, and a user that the database refuses throws [`CollectionItemException`](../../site/generator.md#collection-item-exception), after which no user is added.  `GetUserName`'s [`-- generator: method-type=both`](../../site/generator.md#synchronous-methods) gives it a synchronous method, `GetUserName`, which returns the row or null and takes no `CancellationToken`, for code that does not await, and which `UserQueries.GetUserName` calls, beside its asynchronous method, which `method-type=both` names `GetUserNameAsync` and `UserQueries.GetUserNameAsync` calls.  `RenameUser`'s `-- generator: collection-method method-type=sync` gives it a synchronous method and a synchronous [collection method](../../site/generator.md#synchronous-methods), which `UserQueries.RenameUsers` calls: it renames every user in one transaction of its own, on the calling thread, and returns the count of rows updated, with no task and no `CancellationToken`.  `OrderLineQueries.sql` gives each `numeric` the type [`Price`](Methods/Price.cs) with a type override, and the method reads `unit_price` through `Price`'s [converters](../../site/settings.md#converters).  `users.email` is of the [domain](../../site/settings.md#setting-type-overrides) `email` over `text`, which the header of `UserQueries.sql` alone gives the type [`EmailAddress`](Methods/EmailAddress.cs), `-- type-override: public.email=EmailAddress`, so the methods read and send each email through its converters, and `Models/` still reads it as a `string`; `FindUserByEmail` compares `@email` with the column, which PostgreSQL gives the domain's base type, so its [`-- param:` marker](../../site/reference/parameters.md#param-marker) declares the parameter as the domain, `-- param: @email public.email`.  The project's [column types](../../site/settings.md#setting-column-types) give the column `order_lines.order_id` the type [`OrderId`](Methods/OrderId.cs), by its short name, in every query that reads it, so `ListOrderLinesDto.OrderId` is an `OrderId`; `ListOrderLines` gives its parameter that type with a [`-- param:` marker](../../site/reference/parameters.md#param-marker), `-- param: @orderId -> OrderId`, so `ListOrderLinesParams.OrderId` is one too, and `ListLatestOrderPerProduct` gives it to a computed column, which the column types do not reach, with a [`-- column:` marker](../../site/generator.md#column-marker), `-- column: latest_order_id -> OrderId`.  The methods read and send each `OrderId` through its converters.  `DeliveryQueries.sql` shows the libraries that the methods read and send.  The project references Npgsql's plugins for NodaTime and NetTopologySuite, so its methods get [NodaTime's types](../../site/postgresql.md#nodatime) and [NetTopologySuite's](../../site/postgresql.md#nettopologysuite), as the [library types](../../site/settings.md#setting-library-types) say.  The project's type override would keep `delivered_at`, a `timestamptz`, a `DateTimeOffset`, so the header of `DeliveryQueries.sql` gives `timestamptz` back to NodaTime, `-- type-override: timestamptz=default`, and `FindDeliveryDto.DeliveredAt` and `RecordDeliveryParams.DeliveredAt` are NodaTime's `Instant`; `location`, a `geography(Point, 4326)`, is NetTopologySuite's `Geometry`.  `details`, a `jsonb`, is [`DeliveryDetails`](Methods/DeliveryDetails.cs), a class of the project, by `FindDelivery`'s `-- column: details -> DeliveryDetails` and `RecordDelivery`'s `-- param: @details -> DeliveryDetails`, and the methods read and send it as [JSON](../../site/postgresql.md#json), through System.Text.Json |
| [`Repository/`](Repository) | The default [output](../../site/settings.md#setting-output), `repository`: each query of `UserRepository.sql`, the file named for [`UserRepository`](Repository/UserRepository.cs), gets its models and a public method of the class itself, such as `ListUserNames(cancellationToken)`, which takes no connection: it opens a connection of the class's `NpgsqlDataSource`, a parameter of its primary constructor that SqlSource finds by its type, runs the query and disposes the connection.  `GetUser` is `one-optional` and returns the row or null, and its generator parameter [`method-type=both`](../../site/generator.md#synchronous-repository-methods) gives it a synchronous method, `GetUser(new GetUserParams(1))`, which opens its connection of the same data source, beside its asynchronous one, `GetUserAsync`; `ListUserNames` returns an array, and `RenameUser` is `rowcount` and returns the count of rows updated.  The class also gets the interface `IUserRepository` of those methods, both of `GetUser`'s among them, which [`UserGreeter`](Repository/UserGreeter.cs) takes, calling each, so that a test of it can pass a fake and needs no database |
| [`DapperSetup.cs`](DapperSetup.cs), [`UtcDateTimeOffsetHandler.cs`](UtcDateTimeOffsetHandler.cs) | What Dapper needs once, before the first query: see [the recipe](../../site/recipes.md#dapper-with-postgresql) |

In `Sql/`, `Methods/` and `Repository/` each class takes the `.sql` file named for it, and in `Models/` the one class takes every `.sql` file of its folder.  The header of `Sql/CountQueries.sql` sets the output `sql` with the [marker](../../site/settings.md#setting-output) `-- output: sql`, [`Models/sqlsource.json`](Models/sqlsource.json) sets `models` for its folder, [`Methods/sqlsource.json`](Methods/sqlsource.json) sets `methods` for its own, and `Repository/` takes the default, `repository`.  Each class's methods run the queries: in `Sql/` and `Models/` the generated SQL through Dapper, into the generated models, in `Methods/` the generated methods, and in `Repository/` the class's own generated methods.  The queries write their parameters in camelCase, `@orderId`: in `Models/` Dapper binds it to the property `OrderId` of the input model, and in `Methods/` and `Repository/` the generated method binds it from that property itself.  Nothing here runs: the project compiles, so a model that changes shape breaks its build.

## Your own project

[`PostgresExamples.csproj`](PostgresExamples.csproj) takes the generator from this repository, so that it builds with the generator of the same commit and shows what SqlSource does now.  The repository's own build files give it its language version, its analyzers and the versions of its packages.  A project of your own adds the packages instead, here with the versions this example builds with:

```bash
dotnet add package SqlSource
dotnet add package Dapper --version 2.1.89
dotnet add package Npgsql --version 10.0.3
dotnet add package Npgsql.NodaTime --version 10.0.3
dotnet add package Npgsql.NetTopologySuite --version 10.0.3
```

Dapper is for `Sql/` and `Models/` alone: a project that runs every query through its method needs only SqlSource and Npgsql, and the plugins of the libraries it uses.  `Npgsql.NodaTime` and `Npgsql.NetTopologySuite` turn NodaTime and NetTopologySuite on for the methods, which then read and send their types; System.Text.Json, which reads and sends `DeliveryDetails`, needs no package.  The rest of the project file is nullable reference types, which your project sets too, and the dialect, the type override and the column types are in [`sqlsource.json`](sqlsource.json), which your project keeps beside its project file: see [the settings](../../site/settings.md#all-settings).  It also keeps [SQLSRC240](../../site/diagnostics.md#sqlsrc240) a warning, which only this repository needs, so that it builds while its version moves ahead of the committed sidecars.

## Start the database

From this folder:

```bash
docker compose up -d --wait
```

The first `up` builds the image, PostgreSQL with PostGIS, from [`Postgis/Dockerfile`](Postgis/Dockerfile), which takes a minute or two; after a change to the Dockerfile, `docker compose up -d --wait --build` builds it again.  PostgreSQL listens on `localhost:55432`, with the database, user and password all `examples`.  They are for this example alone.  If the port is taken, change `55432` under `ports` in `docker-compose.yml`, and the port in the connection strings below.  The schema is applied the first time the database starts; after a change to `schema.sql`, start again from nothing:

```bash
docker compose down -v
```

## Describe the queries

The tool asks the database about each query of `Models/`, `Methods/` and `Repository/`, and records the answer in a `.sql.json` file beside each of their `.sql` files, its sidecar.  The project has one database, so the tool takes its connection from `SQLSOURCE_CONNECTION`.  In a project of your own, with the tool installed:

```bash
export SQLSOURCE_CONNECTION="Host=localhost;Port=55432;Database=examples;Username=examples;Password=examples"
dotnet sqlsource describe
```

The tool describes the queries that have no current entry in their sidecar.  `dotnet sqlsource describe --check` compares the sidecars with what the database says now, and `--force` describes every query again.

This example's sidecars are committed and current.  They are written by `tools/describe-end-to-end.sh`, which starts a database of its own: see [the recording scripts](../../tools/README.md#recording-scripts).  To compare them with the Compose database, run the tool from its source, from the repository's root, with `SQLSOURCE_CONNECTION` set as above, since the repository pins no published tool and another version of it writes another version into every sidecar:

```bash
dotnet run --project src/SqlSource.Tool -- describe --check examples/PostgresExamples/PostgresExamples.csproj
```

See [the tool](../../site/tool.md) for the commands and the connections.

## Describe as you save, and in the build

In a project of your own, copy [`.env.example`](.env.example) to `.env` in the project's folder, and add `.env` to your `.gitignore`, since it holds your password; the tool reads the connection from it, as [the `.env` file](../../site/tool.md#env-file) says.  Then run:

```bash
dotnet sqlsource describe --watch
```

It describes again each time you save a query, as [describe on save](../../site/tool.md#describe-on-save) says, and your editor can start it for you: see [the recipe](../../site/recipes.md#start-the-watcher-with-your-editor).  A build of your project also describes before it compiles, as [running in the build](../../site/tool.md#running-in-the-build) says, so a query that the database rejects is an error of the build, and you need not run `describe` after a change.

This example differs in two ways.  The watch runs from the source, as the commands above do, from the repository's root.  It reads the `.env` of this folder when it runs on the project, so the `export` above is not needed:

```bash
dotnet run --project src/SqlSource.Tool -- describe --watch examples/PostgresExamples/PostgresExamples.csproj
```

And its build stays offline, since this repository's `Directory.Build.props` sets `SqlSourceOnline` to `false`, so that it builds from the committed sidecars.  After you change a query here, run `tools/describe-end-to-end.sh`, which writes the sidecars the repository commits.

## Read a sidecar

Open [`Models/Orders.sql.json`](Models/Orders.sql.json).  It is the tool's record of what the database said about each query: its parameters, and its columns, each with its type and whether it can be null.  You never edit a sidecar: the tool writes it, and the build reports a query whose entry is missing or older than its SQL.  See [the sidecar](../../site/concepts.md#sidecar).

## Build, and see the generated code

```bash
dotnet build
```

Your IDE shows the generated code among the project's dependencies, under its analyzers or source generators, and goes to it from a use such as `Sql.GetOrder`, `GetOrderDto` or `ListOrderLines`.  To write it to disk, build with `EmitCompilerGeneratedFiles`:

```bash
dotnet build -p:EmitCompilerGeneratedFiles=true
```

The files are under `obj/Debug/net10.0/generated/`: one for each class, with the SQL of its queries whose output is `sql` or `models`, one with the methods of each class of `Methods/`, in the classes `UserQueriesExtensions`, `OrderLineQueriesExtensions` and `DeliveryQueriesExtensions`, and in `OrderQueries` and its nested class `Sql`, one with the methods of `UserRepository` and its interface `IUserRepository`, one for each model, and one with the attributes, and one with the exceptions a method throws.  `GetOrderDto`, for instance, is a record with the properties `Id`, `Status`, `PlacedAt`, `ShippedAt` and `CustomerName`.  See [the generator](../../site/generator.md).

## Call the methods

Nothing here runs, but code of yours that uses `Methods/` would give its classes a connection and call their methods, and pass the connection to the static methods of `OrderQueries`:

```csharp
await using var dataSource = StoreDataSource.CreateDataSource(connectionString);
await using var connection = dataSource.CreateConnection();
var userQueries = new UserQueries(connection);
var deliveryQueries = new DeliveryQueries(connection);

FindUserByEmailDto? ada = await userQueries.FindUserByEmailAsync(new EmailAddress("ada@example.com"), cancellationToken);
ListUsersSortedByDto[] users = await userQueries.ListUsersSortedByAsync("created_at DESC", cancellationToken);
int linusId = await userQueries.AddUserAsync("Linus", new EmailAddress("linus@example.com"), cancellationToken);
int[] newIds = await userQueries.AddUsersAsync(
    [
        new AddUserParams("Alan", new EmailAddress("alan@example.com")),
        new AddUserParams("Edsger", new EmailAddress("edsger@example.com")),
    ],
    cancellationToken);
await userQueries.WriteUserNamesAsync(Console.Out, cancellationToken);
string? firstName = userQueries.GetUserName(1);
int renamed = userQueries.RenameUsers([new RenameUserParams("Ada Lovelace", 1), new RenameUserParams("Grace Hopper", 2)]);
FindDeliveryDto? delivery = await deliveryQueries.FindDeliveryAsync(1, cancellationToken);
ListOrdersOfUserDto[] orders = await OrderQueries.ListOrdersOfUserAsync(connection, new ListOrdersOfUserParams(1), cancellationToken: cancellationToken);
bool hasLines = await OrderQueries.HasLinesAsync(connection, 1, cancellationToken);
```

[`StoreDataSource.CreateDataSource`](Methods/StoreDataSource.cs) builds a data source that calls `UseNodaTime()` and `UseNetTopologySuite()`, without which the methods of `DeliveryQueries.sql` throw `InvalidCastException` when they run: see [what a method reads and throws](../../site/postgresql.md#method-results).  The queries of `Sql/` and `Models/` run through Dapper on another, `DapperSetup`'s, which calls neither: on a data source that calls `UseNodaTime()`, Dapper is given an `Instant` for every `timestamptz`, which the models' `DateTimeOffset` cannot take.  A synchronous method, such as `GetUserName`, needs a data source whose connection string does not set `Multiplexing=true`, which Npgsql refuses synchronous calls on, except in a transaction that the caller has begun on the connection.

Each method opens the connection when it is closed and closes it again before it returns; `StreamUsersOldestFirst`, which `WriteUserNamesAsync` calls, opens it when its loop starts and closes it when the loop ends.  To run several queries on one open connection or in a transaction, open the connection yourself and pass the transaction to each generated method: see [the connection](../../site/generator.md#method-connection) and [the recipe](../../site/recipes.md#queries-in-a-transaction).

Code that uses `Repository/` gives `UserRepository` a data source and nothing more, since each method opens a connection of it and disposes it before it returns:

```csharp
await using var dataSource = NpgsqlDataSource.Create(connectionString);
var greeter = new UserGreeter(new UserRepository(dataSource));

string greeting = await greeter.GreetAsync(1, cancellationToken);

// Where the code does not await, through the synchronous GetUser
string sameGreeting = greeter.Greet(1);
```
