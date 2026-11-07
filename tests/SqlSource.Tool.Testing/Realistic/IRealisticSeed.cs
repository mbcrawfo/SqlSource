using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Testing.Realistic;

// The seed of an engine's database "realistic" as RealisticDapperChecks and RealisticMethodChecks run a case over it:
// a connection for each sample, the sample's input model and the rollback of a run that failed, each through the
// engine's driver, so each engine's project holds one, its RealisticSeed.  A check takes one for each test, and
// disposes it.  The input model is read before a transaction begins on the sample's connection, since SqlClient
// refuses a command that is not given the transaction its connection holds.
internal interface IRealisticSeed : IAsyncDisposable
{
    // An open connection to the database "realistic", which the caller disposes.
    Task<DbConnection> OpenAsync(CancellationToken token);

    // The sample's input model, built by RealisticInputs.ModelOf from the values the server reads the sample's values
    // as, read before a transaction begins on the connection.
    Task<object> InputModelAsync(
        DbConnection connection,
        Type input,
        IReadOnlyList<SidecarParameter> parameters,
        IReadOnlyDictionary<string, string> sample
    );

    // The rollback of a run that failed, which fails too when the connection broke and then does not take the place
    // of the run's failure.
    Task RollBackAfterAsync(DbTransaction transaction);
}
