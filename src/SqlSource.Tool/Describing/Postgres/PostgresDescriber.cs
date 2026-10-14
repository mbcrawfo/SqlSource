using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Asks a PostgreSQL to describe queries, through Npgsql.  It is registered nowhere until sub-phase 3.6 registers it:
/// the released tool has none for <c>postgres</c>.
/// </summary>
/// <remarks>
/// Every <c>explain</c> of the describer carries settings of the planner.  The tool makes it with none, so the
/// planner chooses its own join methods; a test gives some to force a join method, and the replay of a capture that
/// was recorded so gives the same.
/// </remarks>
/// <param name="explainSettings">Settings as <c>name=value</c>, each of <see cref="PlannerSettings" />.</param>
/// <exception cref="ArgumentException">A setting is not of the list.</exception>
internal sealed class PostgresDescriber(IReadOnlyList<string> explainSettings) : IQueryDescriber
{
    private readonly IReadOnlyList<string> _explainSettings = PlannerSettings.Checked(explainSettings);

    /// <summary>The describer as the tool makes it: the planner chooses its own join methods.</summary>
    public PostgresDescriber()
        : this(PlannerSettings.None) { }

    public SqlDialect Dialect => SqlDialect.PostgreSql;

    public Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
        IRunLog log,
        CancellationToken cancellationToken
    ) => PostgresSession.OpenAsync(request, log, _explainSettings, cancellationToken);
}
