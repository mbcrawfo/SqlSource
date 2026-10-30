namespace SqlSource.DatabaseTests.Postgres.EndToEnd.NodaTime;

// The queries of noda_values, whose folder's settings file leaves NodaTime on, so that their models and methods take
// NodaTime's types, read and written through Npgsql.NodaTime: extension methods of the connection in
// NodaTimeQueriesExtensions.
[SqlSourceGenerate(Path = "Queries/NodaTime.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class NodaTimeQueries;
