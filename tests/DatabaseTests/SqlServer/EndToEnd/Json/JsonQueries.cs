namespace SqlSource.DatabaseTests.SqlServer.EndToEnd.Json;

// The queries of json_values and json_columns, both files of Queries/, whose folder's settings file turns
// System.Text.Json on, so that their models and methods take its types and the classes of Types/, read and sent
// through generated conversions, the serializer or a class's converters: extension methods of the connection in
// JsonQueriesExtensions.
[SqlSourceGenerate(Path = "Queries", SqlLocation = SqlLocation.Direct)]
internal static partial class JsonQueries;
