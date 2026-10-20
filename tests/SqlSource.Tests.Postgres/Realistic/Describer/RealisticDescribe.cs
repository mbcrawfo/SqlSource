using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SqlSource.Snapshot;
using SqlSource.Tests.Postgres.Describer;
using SqlSource.Tool;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic.Describer;

// Describes a realistic case as a run does, in a session of its own on the database "realistic", and builds the
// entry a run writes from what it gave.  The rules every check of the describer over a case shares are here too:
// that the server runs the case, and that a description has the columns of the truth.
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
        if (!found.RunsOn(major))
        {
            Assert.Skip($"The case needs PostgreSQL {found.MinimumMajor} or later.");
        }

        return found;
    }

    // The columns and the parameters of a case that is not refused, which must be described with its truth's
    // columns.  A refused case has none to check, and skips.
    public static async Task<(
        IReadOnlyList<SidecarColumn> Columns,
        IReadOnlyList<DescribedParameter> Parameters
    )> DescribedAsync(RealisticCase found, string connection)
    {
        if (found.Refusal is not null)
        {
            Assert.Skip($"{found} is refused as {found.Refusal}, so it has no columns to check.");
        }

        var described = await DescribeOnceAsync(found, connection);
        return (ColumnsOf(found, described), described.Value!.Parameters);
    }

    // The columns of a case that is not refused, which must be described, with as many columns as its truth and
    // each of the truth's name at its position.  A failure names the case on its first line.
    public static IReadOnlyList<SidecarColumn> ColumnsOf(
        RealisticCase found,
        Outcome<QueryDescription, DescribeFailure> described
    )
    {
        if (!described.Succeeded)
        {
            Assert.Fail($"{found} was refused:\n{RenderFailure(described.Failure)}");
        }

        IReadOnlyList<SidecarColumn> columns = described.Value.Columns ?? [];
        if (columns.Count != found.Columns.Count)
        {
            Assert.Fail($"{found} is described with {columns.Count} columns, and its truth has {found.Columns.Count}");
        }

        for (var index = 0; index < columns.Count; index++)
        {
            if (columns[index].Name != found.Columns[index].Name)
            {
                Assert.Fail(
                    $"{found} column {index} is described as {columns[index].Name}, and its truth names it "
                        + found.Columns[index].Name
                );
            }
        }

        return columns;
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
                $"No session was opened for the case {found}:\n{RenderFailure(opened.Failure)}"
            );
        }

        var session = opened.Value;
        await using (session)
        {
            return await session.DescribeAsync(request, token);
        }
    }

    // The id, each argument, the help and each line of the server of a failure, a line for each, for a message.
    public static string RenderFailure(DescribeFailure failure)
    {
        var text = new StringBuilder();
        Line(text, $"failure: {failure.Descriptor.Id}");
        foreach (var argument in failure.Arguments)
        {
            Line(text, $"argument: {argument}");
        }

        if (failure.Help is { } help)
        {
            Line(text, $"help: {help}");
        }

        foreach (var line in failure.ServerLines)
        {
            Line(text, $"server: {line}");
        }

        return text.ToString();
    }

    private static void Line(StringBuilder text, FormattableString line) =>
        text.Append(line.ToString(CultureInfo.InvariantCulture)).Append('\n');
}
