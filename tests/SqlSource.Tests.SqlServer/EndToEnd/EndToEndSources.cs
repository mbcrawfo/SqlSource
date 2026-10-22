using Dapper;
using Microsoft.Data.SqlClient;
using SqlSource.Tests.SqlServer.EndToEnd.Types;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The connections that the Dapper tests run the end-to-end queries on, set up as the manual's recipe tells a project
// to and no further: Dapper matches snake_case columns to properties and has a handler for each type that a type
// override names and SqlClient does not give.  A test opens the connection it is given and disposes it.
internal static class EndToEndSources
{
    // Dapper's settings are the process's, so they are made once, before the first connection.
    static EndToEndSources()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new EmailHandler());
        SqlMapper.AddTypeHandler(new DateOnlyHandler());
        SqlMapper.AddTypeHandler(new TimeOnlyHandler());
    }

    public static SqlConnection App(SqlServerContainer sqlServer) =>
        new(sqlServer.ConnectionStringOf(SqlServerContainer.AppDatabase));

    public static SqlConnection Billing(SqlServerContainer sqlServer) =>
        new(sqlServer.ConnectionStringOf(SqlServerContainer.BillingDatabase));
}
