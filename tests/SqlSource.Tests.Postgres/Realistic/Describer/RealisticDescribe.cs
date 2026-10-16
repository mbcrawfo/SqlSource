using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqlSource.Snapshot;
using SqlSource.Tool;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic.Describer;

// What the theory of a case's recorded description does with a run: record or compare on a recorded major, skip or
// refuse on another.
// Public, since a public theory takes it as an argument.
public enum Expectation
{
    Compare,
    Record,
    Skip,
    Refuse,
}

// Describes a realistic case as a run does, in a session of its own on the database "realistic", and renders what
// it gave as the text an expected-pg{major}.txt holds.  The rules every check of the describer over a case shares
// are here too: that the server runs the case, and that a description has the columns of the truth.
internal static class RealisticDescribe
{
    public const string RefuseMessage = "Record on PostgreSQL 14 or 18.";

    // What is not there, in a rendered line.
    private const string None = "-";

    // The description of each case by its name, once in a run: every check describes against the one container of
    // the run, and a case's folder does not change during it.  A describe that throws is not kept.
    private static readonly ConcurrentDictionary<string, Outcome<QueryDescription, DescribeFailure>> Described = new(
        StringComparer.Ordinal
    );

    public static Expectation ExpectationOn(bool recording, int major) =>
        (recording, PostgresContainer.RecordedMajors.Contains(major)) switch
        {
            (true, true) => Expectation.Record,
            (true, false) => Expectation.Refuse,
            (false, true) => Expectation.Compare,
            (false, false) => Expectation.Skip,
        };

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
        var opened = await new PostgresDescriber().OpenAsync(
            new OpenRequest(
                PostgresContainer.RealisticDatabase,
                connection,
                LiveExchange.Instance,
                TimeProvider.System
            ),
            NullRunLog.Instance,
            token
        );
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

    // One line for each fact, each ending in a line feed.  The kind, the plan and the table match are spelled as the
    // sidecar spells them.
    public static string Render(Outcome<QueryDescription, DescribeFailure> described)
    {
        if (!described.Succeeded)
        {
            return RenderFailure(described.Failure);
        }

        var text = new StringBuilder();
        var description = described.Value;
        var kind =
            description.ResultKind == SidecarResultKind.Rows
                ? SidecarValues.ResultKind.Rows
                : SidecarValues.ResultKind.None;
        Line(text, $"kind: {kind}");
        Line(text, $"plan: {description.Plan ?? None}");
        Line(text, $"table-match: {description.TableMatch ?? None}");
        foreach (var parameter in description.Parameters)
        {
            Line(text, $"parameter @{parameter.Name}: {parameter.Type?.Name ?? None} ({parameter.TypeSource ?? None})");
        }

        foreach (var column in description.Columns ?? [])
        {
            var nullable = NullableOf(column.Nullable);
            var source = column.NullableSource ?? None;
            var origin = OriginOf(column.Origin);
            Line(
                text,
                $"column {column.Ordinal} {column.Name}: {column.Type.Name} | nullable {nullable} | {source} | {origin}"
            );
        }

        return text.ToString();
    }

    // The id, each argument, the help and each line of the server, as a failure's expectation holds them.
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

    private static string NullableOf(bool? nullable) =>
        nullable switch
        {
            true => "true",
            false => "false",
            null => "null",
        };

    private static string OriginOf(SidecarOrigin? origin) =>
        origin is null ? None : $"{origin.Schema ?? None}.{origin.Table ?? None}.{origin.Column ?? None}";
}
