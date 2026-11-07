using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using SqlSource.DatabaseTests.Postgres.EndToEnd;
using SqlSource.DatabaseTests.Postgres.Realistic.Describer;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing.Realistic;

namespace SqlSource.DatabaseTests.Postgres.Realistic.Generation;

// The seed of the database "realistic" for the shared checks of the runs through Dapper and through a case's method:
// one data source for each test, which opens a pooled connection for each sample and is disposed with the seed.  The
// sample's input model is read through the data source, on a pooled connection of its own rather than the sample's,
// before the sample's transaction begins.
internal sealed class RealisticSeed(PostgresDatabases databases) : IRealisticSeed
{
    private readonly NpgsqlDataSource _source = EndToEndSources.Realistic(databases);

    public async Task<DbConnection> OpenAsync(CancellationToken token) => await _source.OpenConnectionAsync(token);

    public Task<object> InputModelAsync(
        DbConnection connection,
        Type input,
        IReadOnlyList<SidecarParameter> parameters,
        IReadOnlyDictionary<string, string> sample
    ) => RealisticParameters.InputModelAsync(_source, input, parameters, sample);

    public Task RollBackAfterAsync(DbTransaction transaction) =>
        RealisticNullabilityOracle.RollBackAfterAsync((NpgsqlTransaction)transaction);

    public ValueTask DisposeAsync() => _source.DisposeAsync();
}
