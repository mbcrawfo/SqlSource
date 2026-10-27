using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using SqlSource.Tests.SqlServer.EndToEnd.Types;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The connections that the tests run the end-to-end queries and the realistic cases on, through the generated methods
// or through Dapper, set up as the manual's recipe tells a project to and no further: Dapper matches snake_case columns
// to properties and has a handler for each type that a type override of a query it reads names and SqlClient does not
// give.  The methods read the types of Types/ that have converters, Email among them, through those instead.  A test
// disposes the connection it is given, and one that needs it open, a Dapper test or one that holds a transaction, opens
// it first.
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

    public static SqlConnection Realistic(SqlServerContainer sqlServer) => new(sqlServer.RealisticConnectionString);

    // Opens the connection and runs the write in a transaction that is rolled back, so that the seed is as it was.  The
    // write gives the transaction to every call, since SqlClient refuses a command on a connection whose transaction it
    // is not given.  Disposing the transaction rolls it back, and SqlClient skips one that the server has already
    // ended, where a rollback of its own in a finally would throw and take the place of the write's exception.
    public static async Task RolledBackAsync(
        SqlConnection connection,
        Func<DbTransaction, Task> write,
        CancellationToken cancellationToken
    )
    {
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await write(transaction);
    }
}
