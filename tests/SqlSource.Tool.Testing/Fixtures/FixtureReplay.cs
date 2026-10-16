using System;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Generation;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Planning;
using Xunit;

namespace SqlSource.Tool.Testing.Fixtures;

// Describes the query of a case as a run does, and gives the text of the sidecar that holds its one entry.
// DescribeAsync answers from the case's capture, with no database.  What is wrong with a case fails with the
// case's name.
internal static class FixtureReplay
{
    // The toolVersion of an expected.json: a fixed one, so that a new version of the tool touches no fixture.
    public const string ToolVersion = "fixture";

    // The connection of a replayed session.  Nothing opens, and the run's redaction wants one value to look for.
    public const string Connection = "replay";

    // The text for the case, answered from its capture.  The describer is made for the case, so that a describer
    // can read a setting of the case.
    public static Task<string> DescribeAsync(FixtureCase fixture, Func<FixtureCase, IQueryDescriber> describer)
    {
        var read = CaptureJson.Read(fixture.Capture);
        if (read.Value is not { } capture)
        {
            throw new InvalidOperationException($"The capture of the case {fixture} cannot be read.  {read.Failure}");
        }

        return string.Equals(capture.Engine, fixture.Engine, StringComparison.Ordinal)
            ? DescribeThroughAsync(fixture, describer, Connection, new ReplayingExchange(capture))
            : throw new InvalidOperationException(
                $"The capture of the case {fixture} is of the engine '{capture.Engine}'."
            );
    }

    // The same steps over any exchange and connection: a capturing one records the case.
    public static async Task<string> DescribeThroughAsync(
        FixtureCase fixture,
        Func<FixtureCase, IQueryDescriber> describer,
        string connection,
        IDescribeExchange exchange
    )
    {
        var token = TestContext.Current.CancellationToken;
        var (planned, dialect) = Plan(fixture, token);
        var query = planned.Query;
        var database = planned.Database ?? throw new InvalidOperationException($"The case {fixture} has no database.");

        var opened = await describer(fixture)
            .OpenAsync(
                new OpenRequest(database, connection, exchange, TimeProvider.System),
                NullRunLog.Instance,
                token
            );
        if (opened.Value is not { } session)
        {
            throw new InvalidOperationException(
                $"No session was opened for the case {fixture}: {opened.Failure?.Descriptor.Id}."
            );
        }

        await using (session)
        {
            var described = await session.DescribeAsync(
                new DescribeRequest(query.Name, SampleSql.Build(query), query.Parameters),
                token
            );
            if (described.Value is not { } description)
            {
                throw new InvalidOperationException(
                    $"The query of the case {fixture} was not described: {described.Failure?.Descriptor.Id}."
                );
            }

            var entry = EntryBuilder.Build(planned, dialect, database, session.Server, description);
            return SidecarWriter.Write(
                new Sidecar(SidecarFormat.Version, ToolVersion, new EquatableArray<SidecarEntry>([entry]))
            );
        }
    }

    // The one query of query.sql as the plan of a run holds it: read by the engine's dialect, with the hash of its
    // SQL, for the database its markers name or else the one named as the dialect.
    public static (PlannedQuery Query, SqlDialect Dialect) Plan(FixtureCase fixture, CancellationToken token)
    {
        if (!SqlDialectName.TryParse(fixture.Engine, out var engine))
        {
            throw new InvalidOperationException($"The engine of the case {fixture} is no dialect.");
        }

        // How a failure names the file.
        var file = $"{FixtureCase.QueryFile} of the case {fixture}";
        var parsed = SqlFileReader.Read(
            new SqlFileText(fixture.QueryPath),
            fixture.QueryPath,
            engine,
            invalidDialect: null,
            commentsWanted: false,
            token
        );
        if (parsed.Errors.Count > 0)
        {
            throw new InvalidOperationException($"{file} cannot be parsed: {parsed.Errors[0].Descriptor.Id}.");
        }

        if (parsed.Queries.Count != 1)
        {
            throw new InvalidOperationException($"{file} holds {parsed.Queries.Count} queries, and a case has one.");
        }

        if (parsed.Dialect != engine.Dialect)
        {
            throw new InvalidOperationException($"{file} is read as '{SqlDialectName.Canonical(parsed.Dialect)}'.");
        }

        var query = parsed.Queries[0];
        return (
            new PlannedQuery(
                query,
                NeedsEntry: true,
                query.Markers.Database ?? SqlDialectName.Canonical(parsed.Dialect),
                SqlQueryHash.Compute(parsed.Dialect, query.Segments, query.Tokens, query.Parameters)
            ),
            parsed.Dialect
        );
    }
}
