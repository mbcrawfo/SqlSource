using System;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests;

// What a PostgreSQL would answer, written by a test: each call the describer is to make, with its answer.  Play
// gives the exchange that answers from it, with no database.  A request the describer asks otherwise than the test
// wrote it has no answer and is a CaptureMissException, so a test of this kind holds the requests too.
internal sealed class PostgresScript
{
    public const string Database = "app";

    // No connection string: under a replay nothing reads it.
    public const string Connection = "replay";

    private readonly DescribeCapture _capture = new(SidecarValues.Engine.Postgres);

    // What "open" answers for a server that opened.
    public static PostgresOpenAnswer Opened { get; } =
        new(
            "18.4",
            "10.0.3",
            [
                new PostgresSetting("search_path", "\"$user\", public"),
                new PostgresSetting("plan_cache_mode", "auto"),
                new PostgresSetting("standard_conforming_strings", "on"),
            ],
            null
        );

    // What "columns" answers for a statement without rows.
    public static PostgresColumnsAnswer NoColumns { get; } = new([], null);

    public PostgresScript Answer<TRequest, TAnswer>(string method, TRequest request, TAnswer answer)
    {
        _capture.Add(new CaptureCall(method, CaptureJson.ToElement(request), CaptureJson.ToElement(answer)));
        return this;
    }

    public PostgresScript Open() => Answer("open", PostgresOpenRequest.Instance, Opened);

    // Made when the script is whole.
    public ReplayingExchange Play() => new(_capture);

    public static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        IDescribeExchange exchange,
        IRunLog? log = null,
        string connection = Connection
    ) =>
        new PostgresDescriber().OpenAsync(
            new OpenRequest(Database, connection, exchange, TimeProvider.System),
            log ?? NullRunLog.Instance,
            TestContext.Current.CancellationToken
        );

    // The session of a script whose "open" answered a server.
    public static async Task<IDescribeSession> SessionAsync(IDescribeExchange exchange, IRunLog? log = null) =>
        (await OpenAsync(exchange, log)).Value.ShouldNotBeNull();
}
