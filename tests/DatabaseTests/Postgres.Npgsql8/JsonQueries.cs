namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// The queries of json_values, linked from the end-to-end project, by the settings of their own folder, which turn
// System.Text.Json on: their methods, extension methods of the connection in JsonQueriesExtensions, and their models in
// its types and the classes of Types/, the node types read and sent through generated conversions on Npgsql 8.
[SqlSourceGenerate(Path = "../Postgres/EndToEnd/Json/Queries/Json.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class JsonQueries;
