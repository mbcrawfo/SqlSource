using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.SqlServer;

// The calls of the session, each asked through the exchange with the live function that answers it.
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
}
