using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tests.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Tests;

// The describer of the tests.  It is given a description, a failure or an exception for a query's name, and keeps
// what it was asked.  It makes every call through the exchange as a real one must: what crosses is the plain
// records of FakeDescriberCalls.cs, which FakeCalls maps to the seam's types on this side, so a run of it can be
// captured and replayed.  Under a replay its "live" functions never run: it then keeps nothing and its hooks do
// not fire.  A query it was told nothing about is a statement without rows and without typed parameters.
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

    // A descriptor that a describer built at run time from a text, a server's for one: the text is in every part
    // of it.  Its id is no constant, so release tracking has no row for it.
    public static DiagnosticDescriptor RejectedWith(string text) =>
        new(
            id: text,
            title: "Rejected by " + text,
            messageFormat: text + " rejected the query '{0}'",
            category: text,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: text,
            helpLinkUri: "https://example.test/" + text
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

    // A statement with every part a description has: a parameter with its declared type, columns of a base type,
    // of an array and of an enum, their origins, the table the result matches, and the provenance.
    public static QueryDescription TypedRows { get; } =
        new(
            SidecarResultKind.Rows,
            TestSidecars.Of(new DescribedParameter("id", TestSidecars.Postgres(), SidecarValues.TypeSource.Declared)),
            TestSidecars.Of(
                TestSidecars.Column(
                    0,
                    "id",
                    TestSidecars.Postgres(),
                    origin: new SidecarOrigin("public", "users", "id"),
                    identity: true,
                    computed: false
                ),
                TestSidecars.Column(
                    1,
                    "tags",
                    TestSidecars.Postgres(
                        "text[]",
                        SidecarValues.Kind.Array,
                        internalName: "_text",
                        element: TestSidecars.Postgres("text", internalName: "text")
                    ),
                    nullable: true
                ),
                TestSidecars.Column(
                    2,
                    "mood",
                    TestSidecars.Postgres("mood", SidecarValues.Kind.Enum, "public", "mood", labels: ["sad", "happy"]),
                    nullable: null,
                    nullableSource: null
                )
            ),
            new SidecarTable("public", "users"),
            SidecarValues.Plan.Walked,
            SidecarValues.TableMatch.Matched
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

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The session is its caller's to close: the run closes what it opened.  The analyzer follows "
            + "a disposable that is returned itself and not one that is returned in an Outcome."
    )]
    public async Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
        IRunLog log,
        CancellationToken cancellationToken
    )
    {
        var answer = await request.Exchange.AskAsync(
            "open",
            FakeOpenRequest.Instance,
            (_, _) =>
            {
                Opened.Add(request);
                OnOpen?.Invoke(log, request);
                if (OpenException is { } exception)
                {
                    throw exception;
                }

                return Task.FromResult(
                    OpenFailure is { } failure
                        ? new FakeOpenAnswer(null, FakeCalls.Plain(failure))
                        : new FakeOpenAnswer(FakeCalls.Plain(Server), null)
                );
            },
            cancellationToken
        );

        // What a describer with a bug returns.  None of it is an answer of an engine, so none of it crosses the
        // exchange.
        if (OpensNothing)
        {
            return null!;
        }

        if (OpenFailsWithNothing)
        {
            return Outcome<IDescribeSession, DescribeFailure>.Failed(null!);
        }

        if (OpensWithNoSession)
        {
            return Outcome<IDescribeSession, DescribeFailure>.Of(null!);
        }

        if (answer.Error is { } error)
        {
            return Outcome<IDescribeSession, DescribeFailure>.Failed(FakeCalls.Seam(error));
        }

        var server = FakeCalls.Seam(
            answer.Server ?? throw new InvalidOperationException("The answer to 'open' holds no server and no error.")
        );
        return Outcome<IDescribeSession, DescribeFailure>.Of(new Session(this, request.Exchange, log, server));
    }

    // The server is what "open" answered, so that a replayed session has the server of its capture.
    private sealed class Session(FakeDescriber owner, IDescribeExchange exchange, IRunLog log, ServerInfo server)
        : IDescribeSession
    {
        public ServerInfo Server => server;

        public async Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
            DescribeRequest request,
            CancellationToken cancellationToken
        )
        {
            var answer = await exchange.AskAsync(
                "describe",
                FakeCalls.Plain(request),
                async (_, token) =>
                {
                    owner.Described.Add(request);
                    owner.OnDescribe?.Invoke(log, request);
                    if (owner.BeforeDescribe is { } before)
                    {
                        await before(request);
                        token.ThrowIfCancellationRequested();
                    }

                    if (owner.Exceptions.TryGetValue(request.Name, out var exception))
                    {
                        throw exception;
                    }

                    return owner.Failures.TryGetValue(request.Name, out var failure)
                        ? new FakeDescribeAnswer(null, FakeCalls.Plain(failure))
                        : new FakeDescribeAnswer(
                            FakeCalls.Plain(owner.Descriptions.GetValueOrDefault(request.Name, NoRows)),
                            null
                        );
                },
                cancellationToken
            );

            // What a describer with a bug returns, in place of the query's result.
            if (owner.DescribeResult is { } result)
            {
                return result()!;
            }

            if (answer.Error is { } error)
            {
                return Outcome<QueryDescription, DescribeFailure>.Failed(FakeCalls.Seam(error));
            }

            return Outcome<QueryDescription, DescribeFailure>.Of(
                FakeCalls.Seam(
                    answer.Description
                        ?? throw new InvalidOperationException(
                            "The answer to 'describe' holds no description and no error."
                        )
                )
            );
        }

        public ValueTask DisposeAsync()
        {
            owner.Closed++;
            return owner.CloseException is { } exception ? throw exception : ValueTask.CompletedTask;
        }
    }
}
