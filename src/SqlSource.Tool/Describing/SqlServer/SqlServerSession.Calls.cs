using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tool.Describing.SqlServer;

// The calls of the session, each asked through the exchange by DescriberSession.AskAsync with the live function that
// answers it, and written to the log where it happens, with how long it took by the run's clock.  Under a replay the
// event is written all the same.
internal sealed partial class SqlServerSession
{
    private Task<SqlServerParametersAnswer> ParametersAsync(
        string query,
        SqlServerParametersRequest request,
        CancellationToken cancellationToken
    ) =>
        AskAsync(
            SqlServerCallNames.Parameters,
            request,
            _live.ParametersAsync,
            (asked, answer, durationMs) => SqlServerEvents.Parameters(_log, query, asked, answer, durationMs),
            cancellationToken
        );

    private Task<SqlServerProbeAnswer> ProbeAsync(
        string query,
        SqlServerProbeRequest request,
        CancellationToken cancellationToken
    ) =>
        AskAsync(
            SqlServerCallNames.Probe,
            request,
            _live.ProbeAsync,
            (asked, answer, durationMs) => SqlServerEvents.Probe(_log, query, asked, answer, durationMs),
            cancellationToken
        );

    private Task<SqlServerDeclaredAnswer> DeclaredAsync(
        string query,
        SqlServerDeclaredRequest request,
        CancellationToken cancellationToken
    ) =>
        AskAsync(
            SqlServerCallNames.Declared,
            request,
            _live.DeclaredAsync,
            (asked, answer, durationMs) => SqlServerEvents.Declared(_log, query, asked, answer, durationMs),
            cancellationToken
        );

    private Task<SqlServerColumnsAnswer> ColumnsAsync(
        string query,
        SqlServerColumnsRequest request,
        CancellationToken cancellationToken
    ) =>
        AskAsync(
            SqlServerCallNames.Columns,
            request,
            _live.ColumnsAsync,
            (asked, answer, durationMs) => SqlServerEvents.Columns(_log, query, asked, answer, durationMs),
            cancellationToken
        );

    // The server's answer about the operands the proofs ask, through the text and the live function of "columns".
    private Task<SqlServerColumnsAnswer> OperandsAsync(
        string query,
        SqlServerColumnsRequest request,
        CancellationToken cancellationToken
    ) =>
        AskAsync(
            SqlServerCallNames.Operands,
            request,
            _live.ColumnsAsync,
            (asked, answer, durationMs) => SqlServerEvents.Operands(_log, query, asked, answer, durationMs),
            cancellationToken
        );

    private Task<SqlServerColumnsAnswer> TableAsync(
        string query,
        SqlServerTableRequest request,
        CancellationToken cancellationToken
    ) =>
        AskAsync(
            SqlServerCallNames.Table,
            request,
            _live.TableAsync,
            (asked, answer, durationMs) => SqlServerEvents.Table(_log, query, asked, answer, durationMs),
            cancellationToken
        );
}
