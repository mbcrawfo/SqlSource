namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The query whose converter cancels a token during the read, with its method in CancellationQueriesExtensions, the
// default location.
[SqlSourceGenerate(Source = "Queries/Cancellation.sql")]
internal static partial class CancellationQueries;
