using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Tests;

// A PostgreSQL describer whose OpenAsync is not async and throws the exception that the test gives for the request,
// before it asks its exchange anything.
internal sealed class ThrowingDescriber(Func<OpenRequest, Exception> throws) : IQueryDescriber
{
    public SqlDialect Dialect => SqlDialect.PostgreSql;

    public IReadOnlyList<string> SecretsOf(string connection, Func<string, string?> getEnvironmentVariable) => [];

    public string? RewriteOf(string connection) => null;

    public Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
        IRunLog log,
        CancellationToken cancellationToken
    ) => throw throws(request);
}
