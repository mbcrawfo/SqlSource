namespace SqlSource.Tests.SqlServer.EndToEnd;

// The project's settings by property, with the SQL nested in the private class Sql.
[SqlSourceGenerate(Path = "Queries/Accounts.sql")]
internal static partial class AccountQueries;
