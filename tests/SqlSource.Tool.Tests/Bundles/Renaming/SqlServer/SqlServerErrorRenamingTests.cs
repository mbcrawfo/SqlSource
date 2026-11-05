using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Bundles;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.SqlServer;

// The errors and refusals of SQL Server's part, which no recorded fixture holds: each case is a fixture's raw bundle
// with one answer edited by hand, its outcome made by replaying it raw, then renamed.
public sealed class SqlServerErrorRenamingTests
{
    private const string Engine = "mssql";

    private const string Columns = "columns";

    private static IEngineRenaming Part => EngineRenamings.For(DatabaseEngine.SqlServer).ShouldNotBeNull();

    [Fact]
    public async Task RenameAsync_UndeclaredParameterError_NamesTheRenamedParameter()
    {
        string? copy = null;
        var raw = await EditedAsync(
            "update-copies-agree",
            capture =>
            {
                var parameters = CallOf(capture, "parameters");
                copy = FirstCopyIn(parameters["request"]!["sql"]!.GetValue<string>(), "before");
                parameters["answer"] = Answer(
                    ("rows", null),
                    (
                        "error",
                        ServerError(
                            11502,
                            16,
                            1,
                            $"The undeclared parameter '@{copy}' is used more than once in the batch being analyzed."
                        )
                    )
                );
            }
        );
        raw.Info.ShouldNotBeNull().Outcome.ShouldBe(BundleOutcome.Failure("SQLSRC226", "describe parameters"));

        var renamed = await RenamedAsync(raw);

        // The parameter "before" is a word of SQL Server's, kept as it is; its copy is written as the parameter.
        copy.ShouldNotBeNull().ShouldStartWith("before_");
        MessagesOf(renamed.Bundle, "parameters", "error.errors").ShouldBe(["@" + ParameterOf(renamed.Bundle)]);
        renamed.Bundle.Info.ShouldBe(raw.Info with { Renamed = true });
        renamed.Bundle.Expected.ShouldBeNull();
    }

    [Fact]
    public async Task RenameAsync_ColumnsErrorRows_KeepTheirNumbersAndLoseTheirText()
    {
        var raw = await EditedAsync(
            "select-left-join",
            capture =>
                CallOf(capture, Columns)["answer"] = Answer(
                    ("rows", null),
                    (
                        "errors",
                        new JsonArray(
                            new JsonObject
                            {
                                ["number"] = 208,
                                ["type"] = 7,
                                ["message"] = "Invalid object name 'dbo.users'.",
                            }
                        )
                    ),
                    ("error", null)
                )
        );
        raw.Info.ShouldNotBeNull().Outcome.ShouldBe(BundleOutcome.Failure("SQLSRC225", "describe columns"));

        var renamed = await RenamedAsync(raw);

        MessagesOf(renamed.Bundle, Columns, "errors").ShouldBe([""]);
        var errors = CallOf(Parse(renamed.Bundle.Capture), Columns)["answer"]!["errors"]!.AsArray();
        errors.Single()!["number"]!.GetValue<int>().ShouldBe(208);
        errors.Single()!["type"]!.GetValue<int>().ShouldBe(7);
        renamed.Bundle.Capture.ShouldNotContain("users", Case.Insensitive);
        renamed.Bundle.Query.ShouldNotContain("users", Case.Insensitive);
        renamed.Bundle.Info.ShouldBe(raw.Info with { Renamed = true });
        renamed.Bundle.Expected.ShouldBeNull();
    }

    [Fact]
    public async Task RenameAsync_OpenError_KeepsItsNumbers()
    {
        var raw = await EditedAsync(
            "select-left-join",
            capture =>
            {
                var open = CallOf(capture, "open");
                open["answer"] = Answer(
                    ("productVersion", null),
                    ("engineEdition", null),
                    ("driverVersion", open["answer"]!["driverVersion"]!.DeepClone()),
                    ("settings", null),
                    ("error", ServerError(18456, 14, 1, ConnectionErrors.LoginRefused)),
                    ("database", null)
                );
                var calls = capture["calls"]!.AsArray();
                while (calls.Count > 1)
                {
                    calls.RemoveAt(calls.Count - 1);
                }
            }
        );
        raw.Info.ShouldNotBeNull().Outcome.ShouldBe(BundleOutcome.Failure("SQLSRC224", null));

        var renamed = await RenamedAsync(raw);

        MessagesOf(renamed.Bundle, "open", "error.errors").ShouldBe([""]);
        var row = CallOf(Parse(renamed.Bundle.Capture), "open")["answer"]!["error"]!["errors"]!.AsArray().Single()!;
        (row["number"]!.GetValue<int>(), row["class"]!.GetValue<int>(), row["state"]!.GetValue<int>()).ShouldBe(
            (18456, 14, 1)
        );
        renamed.Bundle.Info.ShouldBe(raw.Info with { Renamed = true });
        renamed.Bundle.Expected.ShouldBeNull();
    }

    [Fact]
    public async Task RenameAsync_MemberWithNoRule_IsRefusedAtItsPlace()
    {
        const string Extra = "a value of no member";
        var raw = await EditedAsync(
            "select-left-join",
            capture => CallOf(capture, Columns)["answer"]!["rows"]![0]!["extra"] = Extra
        );
        raw.Info.ShouldNotBeNull().Outcome.ShouldBe(BundleOutcome.Entry);

        var refusal = await RefusedAsync(raw);

        refusal.ShouldBe(
            new RenamingRefusal(RefusalStep.Token, "capture.json call 2 'columns' answer.rows[].extra", Extra)
        );
    }

    [Fact]
    public async Task RenameAsync_ANameThatDiffersFromTheCatalogsByCaseAlone_IsRefusedByTheProof()
    {
        const string Column = "email";
        const string Upper = "EMAIL";
        const string Fixture = "select-computed-not-null";
        TableMatchOf(RawBundles.OfFixture(FixtureCases.Of(Engine).Single(found => found.Name == Fixture)))
            .ShouldBe("matched");
        var raw = await EditedAsync(
            Fixture,
            capture =>
            {
                foreach (var call in capture["calls"]!.AsArray().Select(static call => call!.AsObject()))
                {
                    if (call["request"]!["sql"] is { } sql)
                    {
                        call["request"]!["sql"] = WithColumn(sql.GetValue<string>(), Column, Upper);
                    }
                }

                var row = CallOf(capture, Columns)["answer"]!["rows"]!
                    .AsArray()
                    .Single(static row => row!["name"]?.GetValue<string>() == Column)!;
                row["name"] = Upper;
            },
            query => WithColumn(query, Column, Upper)
        );
        raw.Info.ShouldNotBeNull().Outcome.ShouldBe(BundleOutcome.Entry);
        TableMatchOf(raw).ShouldBe("names-differ");

        var refusal = await RefusedAsync(raw);

        // The tool matches the column to the table's ordinally, so the raw entry matches no table; renamed, the two
        // spellings are one name, the column matches, and the proof finds the entry changed.
        refusal.Step.ShouldBe(RefusalStep.Replay);
        refusal.Place.ShouldBe("expected.json");
        refusal.Token.ShouldNotBeNull().ShouldStartWith("matchesTable: null -> ");
    }

    // The fixture's raw bundle with its capture edited and, when given, its query.sql, whose outcome is what replaying
    // the edited bundle raw gives: an entry, with its expected.json, or a refusal's id and step, with none.
    private static async Task<Bundle> EditedAsync(
        string name,
        Action<JsonObject> capture,
        Func<string, string>? query = null
    )
    {
        var fixture = RawBundles.OfFixture(FixtureCases.Of(Engine).Single(found => found.Name == name));
        var edited = Parse(fixture.Capture);
        capture(edited);
        var read = CaptureJson.Read(edited.ToJsonString());
        read.Failure.ShouldBeNull();
        var bundle = fixture with
        {
            Query = query is null ? fixture.Query : query(fixture.Query),
            Capture = CaptureJson.Write(read.Value.ShouldNotBeNull()),
            Expected = null,
        };

        var token = TestContext.Current.CancellationToken;
        var planned = BundleReplay.Plan(bundle, Engine, RenamingProof.NoPath, token);
        planned.Failure.ShouldBeNull();
        var (planQuery, dialect) = planned.Value;
        var replayed = await BundleReplay.RunAsync(
            planQuery,
            dialect,
            read.Value,
            new SqlServerDescriber(),
            NullRunLog.Instance,
            new TestClock(),
            token
        );
        var info = fixture.Info.ShouldNotBeNull();
        return replayed switch
        {
            ReplayOutcome.Described described => bundle with
            {
                Info = info with { Outcome = BundleOutcome.Entry },
                Expected = SidecarWriter.Write(
                    new Sidecar(
                        SidecarFormat.Version,
                        FixtureReplay.ToolVersion,
                        new EquatableArray<SidecarEntry>([described.Entry])
                    )
                ),
            },
            ReplayOutcome.Refused refused => bundle with
            {
                Info = info with { Outcome = BundleOutcome.Failure(refused.Id, refused.Step) },
            },
            _ => throw new InvalidOperationException($"The edited case {name} replayed raw as {replayed}."),
        };
    }

    private static async Task<RenamedBundle> RenamedAsync(Bundle raw)
    {
        var renamed = await RenameAsync(raw);
        renamed.Failure.ShouldBeNull();
        return renamed.Value.ShouldNotBeNull();
    }

    private static async Task<RenamingRefusal> RefusedAsync(Bundle raw)
    {
        var renamed = await RenameAsync(raw);
        renamed.Value.ShouldBeNull();
        return renamed.Failure.ShouldNotBeNull();
    }

    private static Task<Outcome<RenamedBundle, RenamingRefusal>> RenameAsync(Bundle raw) =>
        BundleRenamer.RenameAsync(
            raw,
            Part,
            new SqlServerDescriber(),
            RenamingBundles.Key(1),
            TestContext.Current.CancellationToken
        );

    private static JsonObject Parse(string capture) => JsonNode.Parse(capture).ShouldNotBeNull().AsObject();

    // The one call of the method.
    private static JsonObject CallOf(JsonObject capture, string method) =>
        capture["calls"]!.AsArray().Single(call => call!["method"]!.GetValue<string>() == method)!.AsObject();

    // An answer of the members given, in their order.
    private static JsonObject Answer(params (string Name, JsonNode? Value)[] members)
    {
        var answer = new JsonObject();
        foreach (var (name, value) in members)
        {
            answer[name] = value;
        }

        return answer;
    }

    // A SqlServerServerError of one row.
    private static JsonObject ServerError(int number, int level, int state, string message) =>
        new()
        {
            ["errors"] = new JsonArray(
                new JsonObject
                {
                    ["number"] = number,
                    ["class"] = level,
                    ["state"] = state,
                    ["message"] = message,
                }
            ),
        };

    // The messages of the rows at the answer's path, "error.errors" or "errors", of the bundle's one call.
    private static string[] MessagesOf(Bundle bundle, string method, string path)
    {
        var node = CallOf(Parse(bundle.Capture), method)["answer"]!;
        foreach (var member in path.Split('.'))
        {
            node = node[member]!;
        }

        return [.. node.AsArray().Select(static row => row!["message"]!.GetValue<string>())];
    }

    // The first copy of the parameter that the SQL holds: "@before_1".
    private static string FirstCopyIn(string sql, string parameter)
    {
        var at = sql.IndexOf("@" + parameter + "_", StringComparison.Ordinal);
        at.ShouldBeGreaterThanOrEqualTo(0);
        var end = at + 1;
        while (end < sql.Length && (char.IsAsciiLetterOrDigit(sql[end]) || sql[end] == '_'))
        {
            end++;
        }

        return sql[(at + 1)..end];
    }

    // The one parameter of the bundle's query, as its query.sql names it: the renamed one, of a renamed bundle.
    private static string ParameterOf(Bundle bundle)
    {
        var planned = BundleReplay.Plan(bundle, Engine, RenamingProof.NoPath, TestContext.Current.CancellationToken);
        planned.Failure.ShouldBeNull();
        return planned.Value.Query.Query.Parameters.ShouldHaveSingleItem().Name;
    }

    // The SQL with the column, a whole word, written in another spelling.
    private static string WithColumn(string sql, string column, string spelling) =>
        Regex.Replace(sql, $@"\b{column}\b", spelling);

    private static string? TableMatchOf(Bundle bundle) =>
        SidecarReader.Read(bundle.Expected.ShouldNotBeNull()).Sidecar.ShouldNotBeNull().Queries.Single().TableMatch;
}
