using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// Sessions of the PostgreSQL describer for a container test, on the database fixtures unless given, opened through
// Describers.OpenAsync.
internal static class Sessions
{
    public static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        string connection,
        IDescribeExchange? exchange = null,
        IRunLog? log = null,
        string database = PostgresContainer.Database
    ) => new PostgresDescriber().OpenAsync(database, connection, exchange, log);

    // A session that must open, as the describer's own type.
    public static async Task<PostgresSession> OpenSessionAsync(
        string connection,
        IDescribeExchange? exchange = null,
        IRunLog? log = null
    )
    {
        var opened = await OpenAsync(connection, exchange, log);
        return opened.Value.ShouldBeOfType<PostgresSession>();
    }

    // A session that must open, through a capture of every call over the exchange, the live one unless the test
    // brings one.
    public static async Task<(PostgresSession Session, DescribeCapture Capture)> OpenCapturedAsync(
        string connection,
        IDescribeExchange? exchange = null
    )
    {
        var capture = new DescribeCapture(SidecarValues.Engine.Postgres);
        var session = await OpenSessionAsync(
            connection,
            new CapturingExchange(exchange ?? LiveExchange.Instance, capture)
        );
        return (session, capture);
    }

    // Describes one query in a session of its own, which must end with no prepared statement left in it.
    public static async Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        string connection,
        DescribeRequest request,
        IDescribeExchange? exchange = null
    )
    {
        await using var session = await OpenSessionAsync(connection, exchange);

        var described = await session.DescribeAsync(request, TestContext.Current.CancellationToken);

        (await PreparedStatementsAsync(session)).ShouldBe(0);
        return described;
    }

    // A query that must be described, with no failure.
    public static async Task<QueryDescription> ShouldDescribeAsync(
        string connection,
        DescribeRequest request,
        IDescribeExchange? exchange = null
    )
    {
        var described = await DescribeAsync(connection, request, exchange);

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        return described.Value.ShouldNotBeNull();
    }

    public static Task<QueryDescription> ShouldDescribeAsync(
        string connection,
        string sql,
        IDescribeExchange? exchange = null
    ) => ShouldDescribeAsync(connection, DescribeRequests.Of(sql), exchange);

    // Describes one query as DescribeAsync does, through a capture of every call over the exchange, the live one
    // unless the test brings one.
    public static async Task<(
        Outcome<QueryDescription, DescribeFailure> Described,
        DescribeCapture Capture
    )> DescribeCapturedAsync(string connection, DescribeRequest request, IDescribeExchange? exchange = null)
    {
        var capture = new DescribeCapture(SidecarValues.Engine.Postgres);
        var described = await DescribeAsync(
            connection,
            request,
            new CapturingExchange(exchange ?? LiveExchange.Instance, capture)
        );
        return (described, capture);
    }

    // Every text of a failure that a run prints or logs.
    public static IReadOnlyList<string> TextsOf(DescribeFailure failure) =>
        [
            failure.Descriptor.Id,
            failure.Descriptor.Title.ToString(CultureInfo.InvariantCulture),
            failure.Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture),
            .. failure.Arguments,
            .. failure.ServerLines,
            failure.Help ?? string.Empty,
        ];

    // How many prepared statements the session holds.  The view shows those of its own session alone, so this asks
    // on the session's connection, whose search path a test may give a count of another schema.
    public static async Task<long> PreparedStatementsAsync(PostgresSession session)
    {
        var token = TestContext.Current.CancellationToken;
        await using var command = await session.Connection.CommandAsync(
            "SELECT pg_catalog.count(*) FROM pg_catalog.pg_prepared_statements",
            token
        );
        return (long)(await command.ExecuteScalarAsync(token) ?? 0L);
    }

    // Ends the session's backend from a connection of the test's own, as a server that restarts and an
    // administrator do, and returns when the backend has ended.  The session's connection is not told.
    public static async Task EndBackendAsync(PostgresSession session, string connection)
    {
        var token = TestContext.Current.CancellationToken;
        int backend;
        await using (var ask = await session.Connection.CommandAsync("SELECT pg_catalog.pg_backend_pid()", token))
        {
            backend = (await ask.ExecuteScalarAsync(token)).ShouldBeOfType<int>();
        }

        await using var own = NpgsqlDataSource.Create(connection);
        await using var end = own.CreateCommand("SELECT pg_catalog.pg_terminate_backend(@backend, 10000)");
        _ = end.Parameters.AddWithValue("backend", backend);
        (await end.ExecuteScalarAsync(token)).ShouldBe(true, "the backend did not end in ten seconds");
    }
}
