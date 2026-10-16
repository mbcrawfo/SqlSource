using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using SqlSource.Snapshot;
using SqlSource.Tool;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Tests.Fixtures;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic;

// What the expectation theory does with a run: record or compare on a recorded major, skip or refuse on another.
// Public, since a public theory takes it as an argument.
public enum Expectation
{
    Compare,
    Record,
    Skip,
    Refuse,
}

// Describes a realistic case as a run does, in a session of its own on the database "realistic", and renders what
// it gave as the text an expected-pg{major}.txt holds.  The rules every check of a case shares are here too: which
// majors a case runs and is recorded on, and that a description has the columns of the truth.
internal static class RealisticRun
{
    public const string SkipMessage = "The expectations are recorded on PostgreSQL 14 and 18 alone.";

    public const string RefuseMessage = "Record on PostgreSQL 14 or 18.";

    // What is not there, in a rendered line.
    private const string None = "-";

    // The description of each case by its name, once in a run: every check describes against the one container of
    // the run, and a case's folder does not change during it.  A describe that throws is not kept.
    private static readonly ConcurrentDictionary<string, Outcome<QueryDescription, DescribeFailure>> Described = new(
        StringComparer.Ordinal
    );

    // The majors that an expectation and the noise budget are held for, as CI runs them.
    public static IReadOnlyList<int> RecordedMajors { get; } = [14, 18];

    public static Expectation ExpectationOn(bool recording, int major) =>
        (recording, RecordedMajors.Contains(major)) switch
        {
            (true, true) => Expectation.Record,
            (true, false) => Expectation.Refuse,
            (false, true) => Expectation.Compare,
            (false, false) => Expectation.Skip,
        };

    // Whether the server of that major runs the case: a case may need a later one, MERGE ... RETURNING for one.
    public static bool RunsOn(RealisticCase found, int major) =>
        found.MinimumMajor is not { } minimum || minimum <= major;

    // The files of a case's folder: its query, its truth, and the expectation of each recorded major it runs on.
    public static IReadOnlyList<string> FilesOf(RealisticCase found) =>
        [
            RealisticCase.QueryFile,
            RealisticCase.CaseFile,
            .. RecordedMajors
                .Where(major => RunsOn(found, major))
                .Select(major => Path.GetFileName(found.ExpectedPath(major))),
        ];

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

    // The one value of a query, on a connection of its own.
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL is a test's own query of the database it set up."
    )]
    public static async Task<object?> ScalarAsync(string connection, string sql)
    {
        await using var source = NpgsqlDataSource.Create(connection);
        await using var command = source.CreateCommand(sql);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
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
