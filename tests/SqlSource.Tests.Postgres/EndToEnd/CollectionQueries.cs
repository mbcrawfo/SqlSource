namespace SqlSource.Tests.Postgres.EndToEnd;

// Each collection type by a query's marker, which wins over the project's settings file: extension methods of the
// connection in CollectionQueriesExtensions.
[SqlSourceGenerate(Path = "Queries/Collections.sql")]
internal static partial class CollectionQueries;
