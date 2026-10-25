namespace SqlSource.Tests.Postgres.EndToEnd.Reporting;

// A second namespace that claims Queries/Accounts.sql: it gets models and methods of its own, in
// AccountReportsExtensions.
[SqlSourceGenerate(Path = "../Queries/Accounts.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class AccountReports;
