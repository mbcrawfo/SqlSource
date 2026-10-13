using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
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
}
