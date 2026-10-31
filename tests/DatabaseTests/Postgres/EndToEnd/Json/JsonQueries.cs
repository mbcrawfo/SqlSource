namespace SqlSource.DatabaseTests.Postgres.EndToEnd.Json;

// The queries of json_values, whose folder's settings file turns System.Text.Json on, so that their models and methods
// take its types and the classes of Types/, read and sent through the driver, generated conversions, the serializer or
// a class's converters: extension methods of the connection in JsonQueriesExtensions.
[SqlSourceGenerate(Path = "Queries/Json.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class JsonQueries;
