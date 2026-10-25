namespace SqlSource.Tests.Postgres.EndToEnd;

// The project's settings by property: the output codegen, whose methods extend the connection in
// AccountQueriesExtensions.
[SqlSourceGenerate(Path = "Queries/Accounts.sql")]
internal static partial class AccountQueries;
