using System;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.SqlServer;

// The calls of the session, each asked through the exchange with the live function that answers it, and timed by the
// run's clock.  Under a replay the time is taken all the same.
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

    // Asks one call, and hands the request, the answer and the time it took to what logs it.
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
        SqlServerParametersRequest request,
        CancellationToken cancellationToken
    ) => AskAsync(SqlServerCallNames.Parameters, request, _live.ParametersAsync, Unlogged, cancellationToken);

    private Task<SqlServerProbeAnswer> ProbeAsync(SqlServerProbeRequest request, CancellationToken cancellationToken) =>
        AskAsync(SqlServerCallNames.Probe, request, _live.ProbeAsync, Unlogged, cancellationToken);

    private Task<SqlServerDeclaredAnswer> DeclaredAsync(
        SqlServerDeclaredRequest request,
        CancellationToken cancellationToken
    ) => AskAsync(SqlServerCallNames.Declared, request, _live.DeclaredAsync, Unlogged, cancellationToken);

    // The describer writes no event of its own until its events are added: the run logs the describe's outcome.
    private static void Unlogged<TRequest, TAnswer>(TRequest request, TAnswer answer, long durationMs)
    {
        _ = request;
        _ = answer;
        _ = durationMs;
    }
}
