namespace SqlSource.Tests.SqlServer.EndToEnd.Reporting;

// A second namespace that claims Queries/Accounts.sql, with the SQL on the type: it gets models of its own.
[SqlSourceGenerate(Path = "../Queries/Accounts.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class AccountReports;
