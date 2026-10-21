using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests;

// What a SQL Server would answer, written by a test: a capture of "open", answered as Opened, and of the calls the
// test adds.  A describe over it gives the calls the session asked after "open", in order.  A request the session asks
// otherwise than the test wrote it has no answer and is a CaptureMissException.
internal sealed class SqlServerScript
{
    public const string Database = "app";

    private readonly DescribeCapture _capture = new(SidecarValues.Engine.SqlServer);

    // What "open" answers for SQL Server 2017 with the settings a database of its defaults has.
    public static SqlServerOpenAnswer Opened { get; } =
        new(
            "14.0.3560.2",
            3,
            "7.1.1",
            [
                new SqlServerSetting("compatibility_level", "140"),
                new SqlServerSetting("ANSI_NULLS", "ON"),
                new SqlServerSetting("QUOTED_IDENTIFIER", "ON"),
                new SqlServerSetting("collation", "SQL_Latin1_General_CP1_CI_AS"),
            ],
            null
        );

    public SqlServerScript()
    {
        _ = Add("open", SqlServerOpenRequest.Instance, Opened);
    }

    public SqlServerScript Add<TRequest, TAnswer>(string method, TRequest request, TAnswer answer)
    {
        _capture.Add(new CaptureCall(method, CaptureJson.ToElement(request), CaptureJson.ToElement(answer)));
        return this;
    }

    // A capture of the one call "open", answered as the test gives, for a test of what the session makes of it.
    public static ReplayingExchange Answering(SqlServerOpenAnswer answer)
    {
        var capture = new DescribeCapture(SidecarValues.Engine.SqlServer);
        capture.Add(
            new CaptureCall("open", CaptureJson.ToElement(SqlServerOpenRequest.Instance), CaptureJson.ToElement(answer))
        );
        return new ReplayingExchange(capture);
    }

    // Describes the request over the script and gives the outcome and the calls asked after "open".  With a clock,
    // every call moves it by perCall before it is answered, so that the durations in the log are exact.
    public async Task<(
        Outcome<QueryDescription, DescribeFailure> Described,
        IReadOnlyList<(string Method, string Request)> Asked
    )> DescribeAsync(DescribeRequest request, IRunLog? log = null, TestClock? clock = null, TimeSpan perCall = default)
    {
        var replay = new ReplayingExchange(_capture);
        var asked = new DescribeCapture(SidecarValues.Engine.SqlServer);
        IDescribeExchange answering = clock is null ? replay : new ClockedExchange(replay, clock, perCall);
        var opened = await new SqlServerDescriber().OpenAsync(
            Database,
            FixtureReplay.Connection,
            new CapturingExchange(answering, asked),
            log,
            clock
        );
        await using var session = opened.Value.ShouldNotBeNull();
        var described = await session.DescribeAsync(request, TestContext.Current.CancellationToken);
        replay.Unused.ShouldBeEmpty();
        return (
            described,
            [.. asked.Calls.Skip(1).Select(static call => (call.Method, CaptureJson.KeyOf(call.Request)))]
        );
    }
}
