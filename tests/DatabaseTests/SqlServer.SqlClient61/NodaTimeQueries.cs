namespace SqlSource.DatabaseTests.SqlServer.SqlClient61;

// The queries of noda_values, both files linked from the end-to-end project, by the settings of their own folder, which
// turn NodaTime on: their methods, extension methods of the connection in NodaTimeQueriesExtensions, and their models
// in NodaTime's types, read and sent through the generated NodaTimeConversions on SqlClient 6.1 and NodaTime 3.1.9.
[SqlSourceGenerate(Path = "../SqlServer/EndToEnd/NodaTime/Queries", SqlLocation = SqlLocation.Direct)]
internal static partial class NodaTimeQueries;
