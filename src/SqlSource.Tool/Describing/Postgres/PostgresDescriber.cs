using System.Threading;
using System.Threading.Tasks;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Asks a PostgreSQL to describe queries, through Npgsql.  It is registered nowhere until the describer is whole:
/// the released tool has none for <c>postgres</c>.
/// </summary>
internal sealed class PostgresDescriber : IQueryDescriber
{
    public SqlDialect Dialect => SqlDialect.PostgreSql;

    public Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
        IRunLog log,
        CancellationToken cancellationToken
    ) => PostgresSession.OpenAsync(request, log, cancellationToken);
}
