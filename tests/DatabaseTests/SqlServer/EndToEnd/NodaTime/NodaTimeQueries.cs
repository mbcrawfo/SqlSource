namespace SqlSource.DatabaseTests.SqlServer.EndToEnd.NodaTime;

// The queries of noda_values, both files of Queries/, whose folder's settings file turns NodaTime on, so that their
// models and methods take NodaTime's types, read and sent through the generated NodaTimeConversions: extension methods
// of the connection in NodaTimeQueriesExtensions.
[SqlSourceGenerate(Path = "Queries", SqlLocation = SqlLocation.Direct)]
internal static partial class NodaTimeQueries;
