using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.SqlServer;

/// <summary>Asks a SQL Server to describe queries, through Microsoft.Data.SqlClient.</summary>
internal sealed class SqlServerDescriber : IQueryDescriber
{
    public SqlDialect Dialect => SqlDialect.SqlServer;

    public IReadOnlyList<string> SecretsOf(string connection) => SqlServerConnection.SecretsOf(connection);

    public Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
        IRunLog log,
        CancellationToken cancellationToken
    ) => SqlServerSession.OpenAsync(request, log, cancellationToken);
}
