using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlSource.DatabaseTests.SqlServer.EndToEnd;
using SqlSource.DatabaseTests.SqlServer.Realistic.Describer;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing.Realistic;

namespace SqlSource.DatabaseTests.SqlServer.Realistic.Generation;

// The seed of the database "realistic" for the shared checks of the runs through Dapper and through a case's method:
// a new connection for each sample, which the check disposes, so the seed holds nothing to dispose.  The sample's
// values are read on the sample's own connection before its transaction begins, since SqlClient refuses a command
// that is not given the transaction its connection holds.
internal sealed class RealisticSeed(SqlServerDatabases databases) : IRealisticSeed
{
    public async Task<DbConnection> OpenAsync(CancellationToken token)
    {
        var connection = EndToEndSources.Realistic(databases);
        try
        {
            await connection.OpenAsync(token);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        return connection;
    }

    public Task<object> InputModelAsync(
        DbConnection connection,
        Type input,
        IReadOnlyList<SidecarParameter> parameters,
        IReadOnlyDictionary<string, string> sample
    ) => RealisticParameters.InputModelAsync((SqlConnection)connection, input, parameters, sample);

    public Task RollBackAfterAsync(DbTransaction transaction) =>
        RealisticNullabilityOracle.RollBackAfterAsync((SqlTransaction)transaction);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
