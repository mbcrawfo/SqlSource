using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Tool;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// A session of the PostgreSQL describer as a run opens one, for a test: the live exchange unless the test brings
// one, the machine's clock, and no log unless the test brings one.
internal static class Sessions
{
    public static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        string connection,
        IDescribeExchange? exchange = null,
        IRunLog? log = null
    ) =>
        new PostgresDescriber().OpenAsync(
            new OpenRequest(
                PostgresContainer.Database,
                connection,
                exchange ?? LiveExchange.Instance,
                TimeProvider.System
            ),
            log ?? NullRunLog.Instance,
            TestContext.Current.CancellationToken
        );

    // Describes one query in a session of its own, which must end with no prepared statement left in it.
    public static async Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        string connection,
        DescribeRequest request,
        IDescribeExchange? exchange = null
    )
    {
        var opened = await OpenAsync(connection, exchange);
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();

        var described = await session.DescribeAsync(request, TestContext.Current.CancellationToken);

        (await PreparedStatementsAsync(session)).ShouldBe(0);
        return described;
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
    // on the session's connection.
    public static async Task<long> PreparedStatementsAsync(PostgresSession session)
    {
        var token = TestContext.Current.CancellationToken;
        await using var command = await session.Connection.CommandAsync(
            "SELECT count(*) FROM pg_catalog.pg_prepared_statements",
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
