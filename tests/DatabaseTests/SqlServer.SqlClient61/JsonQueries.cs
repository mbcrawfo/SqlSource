namespace SqlSource.DatabaseTests.SqlServer.SqlClient61;

// The queries of json_values and json_columns, both files linked from the end-to-end project, by the settings of their
// own folder, which turn System.Text.Json on: their methods, extension methods of the connection in
// JsonQueriesExtensions, and their models in its types and the classes of Types/, read and sent through generated
// conversions, the serializer or a class's converters on SqlClient 6.1.
[SqlSourceGenerate(Path = "../SqlServer/EndToEnd/Json/Queries", SqlLocation = SqlLocation.Direct)]
internal static partial class JsonQueries;
