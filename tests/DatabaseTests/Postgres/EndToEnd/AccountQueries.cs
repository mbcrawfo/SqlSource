namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The project's settings by its settings file: the output methods, whose methods extend the connection in
// AccountQueriesExtensions.
[SqlSourceGenerate(Source = "Queries/Accounts.sql")]
internal static partial class AccountQueries;
