using System;
using System.Globalization;
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

// Describes a realistic case as a run does, in a session of its own on the database "realistic", and renders what
// it gave as the text an expected-pg{major}.txt holds.
internal static class RealisticRun
{
    // What is not there, in a rendered line.
    private const string None = "-";

    // The request of the case's one query as a run plans it.
    public static DescribeRequest RequestOf(RealisticCase found)
    {
        var (planned, _) = FixtureReplay.Plan(found.AsFixture, TestContext.Current.CancellationToken);
        var query = planned.Query;
        return new DescribeRequest(query.Name, SampleSql.Build(query), query.Parameters);
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
        if (opened.Value is not { } session)
        {
            throw new InvalidOperationException(
                $"No session was opened for the case {found}: {opened.Failure?.Descriptor.Id}."
            );
        }

        await using (session)
        {
            return await session.DescribeAsync(request, token);
        }
    }

    // One line for each fact, each ending in a line feed.  The kind, the plan and the table match are spelled as the
    // sidecar spells them.
    public static string Render(Outcome<QueryDescription, DescribeFailure> described)
    {
        var text = new StringBuilder();
        if (!described.Succeeded)
        {
            var failure = described.Failure;
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

    public static async Task<int> ServerMajorAsync(string connection)
    {
        await using var source = NpgsqlDataSource.Create(connection);
        await using var opened = await source.OpenConnectionAsync(TestContext.Current.CancellationToken);
        return opened.PostgreSqlVersion.Major;
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
