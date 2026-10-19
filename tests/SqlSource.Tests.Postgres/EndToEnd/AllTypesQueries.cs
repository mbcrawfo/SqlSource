namespace SqlSource.Tests.Postgres.EndToEnd;

// Every type of the type map, read and written, into class models by a file's header.
[SqlSourceGenerate(Path = "Queries/AllTypes.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class AllTypesQueries;
