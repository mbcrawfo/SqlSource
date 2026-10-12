using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Tests;

// The describer of the tests.  It is given a description, a failure or an exception for a query's name, makes every
// call through the exchange as a real one must, and keeps what it was asked.  A query it was told nothing about is
// a statement without rows and without typed parameters.
internal sealed class FakeDescriber(SqlDialect dialect = SqlDialect.PostgreSql) : IQueryDescriber
{
    // No describer ships yet, so no descriptor of a server's error exists; the tests bring one.
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

    // A statement that produces rows, where the describer says none unless told.
    public static QueryDescription Rows { get; } =
        new(
            SidecarResultKind.Rows,
            EquatableArray<DescribedParameter>.Empty,
            EquatableArray<SidecarColumn>.Empty,
            null,
            "not-needed",
            "no-origin"
        );

    public SqlDialect Dialect => dialect;

    public ServerInfo Server { get; set; } = ServerWith(driverVersion: "1.2.3");

    public Dictionary<string, QueryDescription> Descriptions { get; } = [with(StringComparer.Ordinal)];

    public Dictionary<string, DescribeFailure> Failures { get; } = [with(StringComparer.Ordinal)];

    public Dictionary<string, Exception> Exceptions { get; } = [with(StringComparer.Ordinal)];

    public DescribeFailure? OpenFailure { get; set; }

    public Exception? OpenException { get; set; }

    // Returned in place of a result, for a describer that returns nothing.
    public bool OpensNothing { get; set; }

    // Returned in place of a failure, for a describer that says it failed and gives no reason.
    public bool OpenFailsWithNothing { get; set; }

    // Returned in place of a session, for a describer that says it opened one and gives none.
    public bool OpensWithNoSession { get; set; }

    // When set, what it gives is returned for every query in place of the query's result: nothing at all, or an
    // outcome that holds nothing.
    public Func<Outcome<QueryDescription, DescribeFailure>?>? DescribeResult { get; set; }

    public Exception? CloseException { get; set; }

    // Runs before each query is described, for a test that cancels the run there.
    public Func<DescribeRequest, Task>? BeforeDescribe { get; set; }

    // Run with the log a describer is given: when a session is asked for, and before each query is described.
    public Action<IRunLog, OpenRequest>? OnOpen { get; set; }

    public Action<IRunLog, DescribeRequest>? OnDescribe { get; set; }

    public List<OpenRequest> Opened { get; } = [];

    public List<DescribeRequest> Described { get; } = [];

    public int Closed { get; private set; }

    // The server of the fake driver.  A driver without a version is named alone.
    public static ServerInfo ServerWith(string version = "16.4", string? driverVersion = null) =>
        new(version, "FakeDriver", driverVersion, EquatableArray<ServerSetting>.Empty);

    public static DescribeFailure Failure(string query, params string[] serverLines) =>
        Failure(query, DescribeStep.DescribeColumns, "check the SQL", serverLines);

    // A failure at a step, or at none, with the help it gives, or none.  Name "step" or "help" where it is null.
    public static DescribeFailure Failure(
        string argument,
        DescribeStep? step,
        string? help,
        params string[] serverLines
    ) =>
        new(Rejected, new EquatableArray<string>([argument]), step, new EquatableArray<string>([.. serverLines]), help);

    public Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
        IRunLog log,
        CancellationToken cancellationToken
    ) =>
        request.Exchange.AskAsync(
            "open",
            request.Database,
            (_, _) =>
            {
                Opened.Add(request);
                OnOpen?.Invoke(log, request);
                if (OpenException is { } exception)
                {
                    throw exception;
                }

                if (OpensNothing)
                {
                    return Task.FromResult<Outcome<IDescribeSession, DescribeFailure>>(null!);
                }

                if (OpenFailsWithNothing)
                {
                    return Task.FromResult(Outcome<IDescribeSession, DescribeFailure>.Failed(null!));
                }

                if (OpensWithNoSession)
                {
                    return Task.FromResult(Outcome<IDescribeSession, DescribeFailure>.Of(null!));
                }

                return Task.FromResult(
                    OpenFailure is { } failure
                        ? Outcome<IDescribeSession, DescribeFailure>.Failed(failure)
                        : Outcome<IDescribeSession, DescribeFailure>.Of(new Session(this, request.Exchange, log))
                );
            },
            cancellationToken
        );

    private sealed class Session(FakeDescriber owner, IDescribeExchange exchange, IRunLog log) : IDescribeSession
    {
        public ServerInfo Server => owner.Server;

        public Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
            DescribeRequest request,
            CancellationToken cancellationToken
        ) =>
            exchange.AskAsync(
                "describe",
                request,
                async (asked, token) =>
                {
                    owner.Described.Add(asked);
                    owner.OnDescribe?.Invoke(log, asked);
                    if (owner.BeforeDescribe is { } before)
                    {
                        await before(asked);
                        token.ThrowIfCancellationRequested();
                    }

                    if (owner.Exceptions.TryGetValue(asked.Name, out var exception))
                    {
                        throw exception;
                    }

                    if (owner.DescribeResult is { } result)
                    {
                        return result()!;
                    }

                    return owner.Failures.TryGetValue(asked.Name, out var failure)
                        ? Outcome<QueryDescription, DescribeFailure>.Failed(failure)
                        : Outcome<QueryDescription, DescribeFailure>.Of(
                            owner.Descriptions.GetValueOrDefault(asked.Name, NoRows)
                        );
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
