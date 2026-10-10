using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;

namespace SqlSource.Tool.Tests;

// The describer of the tests.  It is given a description, a failure or an exception for a query's name, makes every
// call through the exchange as a real one must, and keeps what it was asked.  A query it was told nothing about is
// a statement without rows and without typed parameters.
internal sealed class FakeDescriber(SqlDialect dialect = SqlDialect.PostgreSql) : IQueryDescriber
{
    // No describer of this sub-phase has an error of a server, so the tests bring one.
    public static readonly DiagnosticDescriptor Rejected = new(
        id: "SQLSRC999",
        title: "Query was rejected",
        messageFormat: "The server rejected the query '{0}'",
        category: "SqlSource",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: "https://example.test/sqlsrc999"
    );

    public static QueryDescription NoRows { get; } =
        new(SidecarResultKind.None, EquatableArray<DescribedParameter>.Empty, null, null, null, null);

    public SqlDialect Dialect => dialect;

    public ServerInfo Server { get; set; } = new("16.4", "FakeDriver", "1.2.3", EquatableArray<ServerSetting>.Empty);

    public Dictionary<string, QueryDescription> Descriptions { get; } = [with(StringComparer.Ordinal)];

    public Dictionary<string, DescribeFailure> Failures { get; } = [with(StringComparer.Ordinal)];

    public Dictionary<string, Exception> Exceptions { get; } = [with(StringComparer.Ordinal)];

    public DescribeFailure? OpenFailure { get; set; }

    public Exception? OpenException { get; set; }

    // Returned in place of a result, for a describer that returns nothing.
    public bool OpensNothing { get; set; }

    public Exception? CloseException { get; set; }

    // Runs before each query is described, for a test that cancels the run there.
    public Func<DescribeRequest, Task>? BeforeDescribe { get; set; }

    public List<OpenRequest> Opened { get; } = [];

    public List<DescribeRequest> Described { get; } = [];

    public int Closed { get; private set; }

    public static DescribeFailure Failure(string query, params string[] serverLines) =>
        new(
            Rejected,
            new EquatableArray<string>([query]),
            DescribeStep.DescribeColumns,
            new EquatableArray<string>([.. serverLines]),
            "check the SQL"
        );

    public Task<OpenResult> OpenAsync(OpenRequest request, CancellationToken cancellationToken) =>
        request.Exchange.AskAsync(
            "open",
            request.Database,
            (_, _) =>
            {
                Opened.Add(request);
                if (OpenException is { } exception)
                {
                    throw exception;
                }

                if (OpensNothing)
                {
                    return Task.FromResult<OpenResult>(null!);
                }

                return Task.FromResult(
                    OpenFailure is { } failure
                        ? OpenResult.Failed(failure)
                        : OpenResult.Opened(new Session(this, request.Exchange))
                );
            },
            cancellationToken
        );

    private sealed class Session(FakeDescriber owner, IDescribeExchange exchange) : IDescribeSession
    {
        public ServerInfo Server => owner.Server;

        public Task<DescribeResult> DescribeAsync(DescribeRequest request, CancellationToken cancellationToken) =>
            exchange.AskAsync(
                "describe",
                request,
                async (asked, token) =>
                {
                    owner.Described.Add(asked);
                    if (owner.BeforeDescribe is { } before)
                    {
                        await before(asked);
                        token.ThrowIfCancellationRequested();
                    }

                    if (owner.Exceptions.TryGetValue(asked.Name, out var exception))
                    {
                        throw exception;
                    }

                    return owner.Failures.TryGetValue(asked.Name, out var failure)
                        ? DescribeResult.Failed(failure)
                        : DescribeResult.Described(owner.Descriptions.GetValueOrDefault(asked.Name, NoRows));
                },
                cancellationToken
            );

        public ValueTask DisposeAsync()
        {
            owner.Closed++;
            return owner.CloseException is { } exception ? throw exception : ValueTask.CompletedTask;
        }
    }
}
