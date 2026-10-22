using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SqlSource.Snapshot;
using SqlSource.Tests.Postgres.Describer;
using SqlSource.Tool;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing.Fixtures;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic.Describer;

// Describes a realistic case as a run does, in a session of its own on the database "realistic", and builds the
// entry a run writes from what it gave.  A rule every check of the describer over a case shares is here too, that
// the server runs the case; that a description has the columns of the truth is RealisticDescriptions' of
// tests/SqlSource.Tool.Testing.
internal static class RealisticDescribe
{
    // The description of each case by its name, once in a run: every check describes against the one container of
    // the run, and a case's folder does not change during it.  A describe that throws is not kept.
    private static readonly ConcurrentDictionary<string, Outcome<QueryDescription, DescribeFailure>> Described = new(
        StringComparer.Ordinal
    );

    // The case of that folder of Cases, read from the repository, once the server of that major runs it.
    public static RealisticCase Start(string name, int major)
    {
        var found = RealisticCases.Read(Path.Combine(RepositoryPaths.RealisticCases, name));
        found.SkipUnlessRunsOn(major);
        return found;
    }

    // The columns and the parameters of a case that is not refused, which must be described with its truth's
    // columns.  A refused case has none to check, and skips.
    public static async Task<(
        IReadOnlyList<SidecarColumn> Columns,
        IReadOnlyList<DescribedParameter> Parameters
    )> DescribedAsync(RealisticCase found, string connection)
    {
        found.SkipIfRefused();
        var described = await DescribeOnceAsync(found, connection);
        return (RealisticDescriptions.ColumnsOf(found, described), described.Value!.Parameters);
    }

    // The request of the case's one query as a run plans it.
    public static DescribeRequest RequestOf(RealisticCase found)
    {
        var (planned, _) = FixtureReplay.Plan(found.AsFixture, TestContext.Current.CancellationToken);
        var query = planned.Query;
        return new DescribeRequest(query.Name, SampleSql.Build(query), query.Parameters);
    }

    // The entry that a run writes for the case's one query from its description, as tools/describe-end-to-end.sh
    // wrote the case's sidecar: the query as the plan of a run holds it, on the database "realistic".
    public static SidecarEntry EntryOf(RealisticCase found, ServerInfo server, QueryDescription description)
    {
        var (planned, dialect) = FixtureReplay.Plan(found.AsFixture, TestContext.Current.CancellationToken);
        return EntryBuilder.Build(planned, dialect, PostgresContainer.RealisticDatabase, server, description);
    }

    // The case's description, described once in a run and kept by the case's name: each check of a case and the
    // noise budget ask for it.
    public static async Task<Outcome<QueryDescription, DescribeFailure>> DescribeOnceAsync(
        RealisticCase found,
        string connection
    )
    {
        if (Described.TryGetValue(found.Name, out var kept))
        {
            return kept;
        }

        return Described.GetOrAdd(found.Name, await DescribeAsync(found, connection));
    }

    public static async Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        RealisticCase found,
        string connection
    )
    {
        var token = TestContext.Current.CancellationToken;
        var request = RequestOf(found);
        var opened = await Sessions.OpenAsync(connection, database: PostgresContainer.RealisticDatabase);
        if (!opened.Succeeded)
        {
            throw new InvalidOperationException(
                $"No session was opened for the case {found}:\n{RealisticDescriptions.RenderFailure(opened.Failure)}"
            );
        }

        var session = opened.Value;
        await using (session)
        {
            return await session.DescribeAsync(request, token);
        }
    }
}
