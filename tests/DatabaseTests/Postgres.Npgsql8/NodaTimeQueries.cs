namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// The queries of noda_values, linked from the end-to-end project, by the settings of their own folder, which leave
// NodaTime on: their methods, extension methods of the connection in NodaTimeQueriesExtensions, and their models in
// NodaTime's types, read and written through Npgsql.NodaTime 8.
[SqlSourceGenerate(Path = "../Postgres/EndToEnd/NodaTime/Queries/NodaTime.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class NodaTimeQueries;
