namespace SqlSource.DatabaseTests.QueryMethodAllocation.SqlServer;

// The claim of NodaTime.sql of the SQL Server end-to-end project's NodaTimeQueries, repeated so that the methods
// measured here are the ones that project runs: the settings file of the file's folder turns NodaTime on, so their
// models take NodaTime's types, read and sent through the generated NodaTimeConversions.
[SqlSourceGenerate(Path = "../../SqlServer/EndToEnd/NodaTime/Queries/NodaTime.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class NodaTimeQueries;
