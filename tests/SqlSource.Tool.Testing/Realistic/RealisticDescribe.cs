using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Testing.Realistic;

// Describes a case of the corpus as a run does, in a session of its own on the corpus's database, opened by the
// engine's describer, and builds the entry a run writes from what it gave.  A rule every check of the describer over
// a case shares is here too, that the server runs the case; that a description has the columns of the truth is
// RealisticDescriptions'.  Each engine's project holds one, in its RealisticCases.
internal sealed class RealisticDescribe(RealisticCorpus corpus, string database, Func<IQueryDescriber> describer)
{
    // The description of each case by its name, once in a run: every check describes against the one container of
    // the run, and a case's folder does not change during it.  A describe that throws is not kept.
    private readonly ConcurrentDictionary<string, Outcome<QueryDescription, DescribeFailure>> _described = new(
        StringComparer.Ordinal
    );

    // The corpus the describer describes the cases of.
    public RealisticCorpus Corpus { get; } = corpus;

    // The case of that folder of Cases, read from the repository, once the server of that major runs it.
    public RealisticCase Start(string name, int major)
    {
        var found = Corpus.ReadNamed(name);
        found.SkipUnlessRunsOn(major);
        return found;
    }

    // The columns and the parameters of a case that is not refused, which must be described with its truth's
    // columns.  A refused case has none to check, and skips.
    public async Task<(
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
    // wrote the case's sidecar: the query as the plan of a run holds it, on the corpus's database.
    public SidecarEntry EntryOf(RealisticCase found, ServerInfo server, QueryDescription description)
    {
        var (planned, dialect) = FixtureReplay.Plan(found.AsFixture, TestContext.Current.CancellationToken);
        return EntryBuilder.Build(planned, dialect, database, server, description);
    }

    // The case's description, described once in a run and kept by the case's name: each check of a case and the
    // noise budget ask for it.
    public async Task<Outcome<QueryDescription, DescribeFailure>> DescribeOnceAsync(
        RealisticCase found,
        string connection
    )
    {
        if (_described.TryGetValue(found.Name, out var kept))
        {
            return kept;
        }

        return _described.GetOrAdd(found.Name, await DescribeAsync(found, connection));
    }

    // The case's description, in a session of its own opened through Describers.OpenAsync, as every test opens one.
    public async Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(RealisticCase found, string connection)
    {
        var token = TestContext.Current.CancellationToken;
        var request = RequestOf(found);
        var session = await OpenAsync(found, connection);
        await using (session)
        {
            return await session.DescribeAsync(request, token);
        }
    }

    // The entry that a run writes for a case that is not refused, described in a session of its own on the corpus's
    // database, for a sidecar held in memory.  A case that is refused fails, as it has no entry.
    public async Task<SidecarEntry> EntryAsync(RealisticCase found, string connection)
    {
        var session = await OpenAsync(found, connection);
        await using (session)
        {
            var described = await session.DescribeAsync(RequestOf(found), TestContext.Current.CancellationToken);
            if (!described.Succeeded)
            {
                Assert.Fail($"{found} was refused:\n{RealisticDescriptions.RenderFailure(described.Failure)}");
            }

            return EntryOf(found, session.Server, described.Value);
        }
    }

    // A session of its own, opened through the describer on the corpus's database.
    private async Task<IDescribeSession> OpenAsync(RealisticCase found, string connection)
    {
        var opened = await describer().OpenAsync(database, connection);
        if (!opened.Succeeded)
        {
            throw new InvalidOperationException(
                $"No session was opened for the case {found}:\n{RealisticDescriptions.RenderFailure(opened.Failure)}"
            );
        }

        return opened.Value;
    }
}
