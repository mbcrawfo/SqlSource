using System;
using System.Threading.Tasks;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Testing;

// A session opened as a run opens one, for a test: the live exchange, the machine's clock and no log, unless the
// test brings its own.
internal static class Describers
{
    public static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        this IQueryDescriber describer,
        string database,
        string connection,
        IDescribeExchange? exchange = null,
        IRunLog? log = null,
        TimeProvider? time = null
    ) =>
        describer.OpenAsync(
            new OpenRequest(database, connection, exchange ?? LiveExchange.Instance, time ?? TimeProvider.System),
            log ?? NullRunLog.Instance,
            TestContext.Current.CancellationToken
        );
}
