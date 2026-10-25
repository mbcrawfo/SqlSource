namespace SqlSource.Tests.SqlServer.EndToEnd.Reporting;

// A second namespace that claims Queries/Accounts.sql: it gets models and methods of its own.
[SqlSourceGenerate(Path = "../Queries/Accounts.sql")]
internal static partial class AccountReports;
