using System;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.SqlServer;

// The calls of the session, each asked through the exchange with the live function that answers it, and written to
// the log where it happens, with how long it took by the run's clock.  Under a replay the event is written all the
// same.
internal sealed partial class SqlServerSession
{
    // "open" is asked before there is a session, so it takes the exchange and the live functions it is given.
    private static Task<SqlServerOpenAnswer> AskOpenAsync(
        IDescribeExchange exchange,
        SqlServerLive live,
        CancellationToken cancellationToken
    ) =>
        exchange.AskAsync(
            SqlServerCallNames.Open,
            SqlServerOpenRequest.Instance,
            (_, token) => live.OpenAsync(token),
            cancellationToken
        );

    // Asks one call, and hands the request, the answer and the time it took to what writes its event.
    private async Task<TAnswer> AskAsync<TRequest, TAnswer>(
        string method,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TAnswer>> live,
        Action<TRequest, TAnswer, long> log,
        CancellationToken cancellationToken
    )
    {
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync(method, request, live, cancellationToken);
        log(request, answer, (long)_time.GetElapsedTime(started).TotalMilliseconds);
        return answer;
    }

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
