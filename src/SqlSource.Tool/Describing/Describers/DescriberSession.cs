using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SqlSource.Tool.Describing.Describers;

/// <summary>
/// What the sessions of every engine share: how one is opened, how a call is asked and timed, and the rules of a
/// server.  An engine's session derives from it and keeps what is asked, in which order, and what is made of each
/// answer.
/// </summary>
/// <param name="exchange">What every call to the engine goes through: <see cref="OpenRequest.Exchange" />.</param>
/// <param name="time">The run's clock, which times each call.</param>
/// <param name="server">What <c>open</c> answered of the server.</param>
internal abstract class DescriberSession(IDescribeExchange exchange, TimeProvider time, ServerInfo server)
    : IDescribeSession
{
    public ServerInfo Server { get; } = server;

    public abstract Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        DescribeRequest request,
        CancellationToken cancellationToken
    );

    public abstract ValueTask DisposeAsync();

    /// <summary>
    /// Asks the call that opens, through the exchange of <paramref name="request" /> and untimed, and gives the
    /// session of the server that answered: the answer's error, else the failure of a server below the floor, else
    /// the session.
    /// </summary>
    /// <remarks>
    /// The <c>Outcome</c> of the session is made with the constructor, which takes ownership of it.  The connection
    /// is disposed on every path that gives no session, an exception's included; the session owns it otherwise.
    /// </remarks>
    /// <param name="request">The database, its connection, the exchange and the clock.</param>
    /// <param name="connection">The connection the <c>live</c> functions use, made and not yet opened.</param>
    /// <param name="opening">How the engine opens.</param>
    /// <param name="cancellationToken">Ends the call.</param>
    protected static async Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync<TRequest, TAnswer>(
        OpenRequest request,
        IAsyncDisposable connection,
        SessionOpening<TRequest, TAnswer> opening,
        CancellationToken cancellationToken
    )
    {
        var kept = false;
        try
        {
            var answer = await request.Exchange.AskAsync(
                opening.Call,
                opening.Request,
                opening.Live,
                cancellationToken
            );
            if (opening.ErrorOf(answer) is { } failure)
            {
                return Outcome<IDescribeSession, DescribeFailure>.Failed(failure);
            }

            var server = opening.ServerOf(answer);
            if (opening.IsBelowTheFloor(answer, server))
            {
                return Outcome<IDescribeSession, DescribeFailure>.Failed(opening.VersionNotSupported(server));
            }

            var session = opening.Session(answer, server);
            kept = true;
            return new Outcome<IDescribeSession, DescribeFailure>(session);
        }
        finally
        {
            if (!kept)
            {
                await connection.DisposeAsync();
            }
        }
    }

    /// <summary>The server of an answer of <c>open</c> that holds no error.</summary>
    /// <param name="version">The server's version, which an answer without an error holds.</param>
    /// <param name="driver">The name of the driver.</param>
    /// <param name="driverVersion">The version of the driver, or null.</param>
    /// <param name="settings">The settings of the session that change a description.</param>
    protected static ServerInfo ServerOf(
        string? version,
        string driver,
        string? driverVersion,
        IEnumerable<ServerSetting> settings
    ) =>
        new(
            version ?? throw new InvalidOperationException("The answer to 'open' holds no version and no error."),
            driver,
            driverVersion,
            new EquatableArray<ServerSetting>([.. settings])
        );

    /// <summary>
    /// Whether the leading digits of <paramref name="version" /> are a major below <paramref name="minimum" />.  A
    /// version that starts with no digit is not below it, since the tool cannot tell what it is.
    /// </summary>
    protected static bool IsBelow(string version, int minimum) =>
        Versions.LeadingMajor(version) is { } major && major < minimum;

    /// <summary>The <c>Outcome</c> of a describe that failed with <paramref name="failure" />.</summary>
    protected static Outcome<QueryDescription, DescribeFailure> Failed(DescribeFailure failure) =>
        Outcome<QueryDescription, DescribeFailure>.Failed(failure);

    /// <summary>
    /// Asks one call, and hands the request, the answer and the time it took to what writes its event.
    /// </summary>
    protected async Task<TAnswer> AskAsync<TRequest, TAnswer>(
        string method,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TAnswer>> live,
        Action<TRequest, TAnswer, long> log,
        CancellationToken cancellationToken
    )
    {
        var (answer, durationMs) = await TimedAskAsync(method, request, live, cancellationToken);
        log(request, answer, durationMs);
        return answer;
    }

    /// <summary>
    /// Asks one call and writes nothing, for a call whose event holds what is read from its answer: the answer, and
    /// how long it took by the run's clock.
    /// </summary>
    protected async Task<(TAnswer Answer, long DurationMs)> TimedAskAsync<TRequest, TAnswer>(
        string method,
        TRequest request,
        Func<TRequest, CancellationToken, Task<TAnswer>> live,
        CancellationToken cancellationToken
    )
    {
        var started = time.GetTimestamp();
        var answer = await exchange.AskAsync(method, request, live, cancellationToken);
        return (answer, (long)time.GetElapsedTime(started).TotalMilliseconds);
    }
}
