using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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

// The errors, refusals and sessions of SQL Server's part that no recorded fixture holds: each case is a fixture's raw
// bundle with its capture edited by hand, its outcome made by replaying it raw, then renamed.
public sealed partial class SqlServerErrorRenamingTests
{
    private const string Engine = "mssql";

    private const string Columns = "columns";

    // The members of a request that hold SQL or declarations.
    private static readonly string[] RequestTexts = ["sql", "params"];

    private static IEngineRenaming Part => EngineRenamings.For(DatabaseEngine.SqlServer).ShouldNotBeNull();

    [Fact]
    public async Task RenameAsync_UndeclaredParameterError_NamesTheRenamedParameter()
    {
        const string Parameter = "cutoff";
        var (raw, copy) = await UndeclaredParameterErrorAsync(Parameter);
        copy.ShouldBe(Parameter + "_1");
        raw.Info.ShouldNotBeNull().Outcome.ShouldBe(BundleOutcome.Failure("SQLSRC226", "describe parameters"));

        var renamed = await RenamedAsync(raw);

        var replacement = renamed
            .Mapping.Single(static name => name.Class == NameClass.Parameter && name.Original == Parameter)
            .Replacement;
        var message = MessagesOf(renamed.Bundle, "parameters", "error.errors").ShouldHaveSingleItem();
        message.ShouldBe("@" + replacement);
        message.ShouldStartWith("@p_");
        message.ShouldNotContain(Parameter, Case.Insensitive);
        renamed.Bundle.Info.ShouldNotBeNull().Outcome.ShouldBe(raw.Info.Outcome);
        renamed.Bundle.Info.ShouldBe(raw.Info with { Renamed = true });
        renamed.Bundle.Expected.ShouldBeNull();
    }

    [Fact]
    public async Task RenameAsync_UndeclaredParameterErrorOfAKeyword_NamesTheKeptParameter()
    {
        const string Parameter = "before";
        var (raw, copy) = await UndeclaredParameterErrorAsync(Parameter);
        copy.ShouldBe(Parameter + "_1");
        raw.Info.ShouldNotBeNull().Outcome.ShouldBe(BundleOutcome.Failure("SQLSRC226", "describe parameters"));

        var renamed = await RenamedAsync(raw);

        // "before" is a word of SQL Server's, kept as it is, and its copy is written as the parameter.
        ParameterOf(renamed.Bundle).ShouldBe(Parameter);
        MessagesOf(renamed.Bundle, "parameters", "error.errors").ShouldBe(["@" + Parameter]);
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

    // A user whose tables are in a system database, as they are when a connection string names master, gets a bundle
    // whose rows of dbo are renamed: the leak search keeps no name from the bundle's rows, so it trusts no row.
    [Theory]
    [InlineData("master")]
    [InlineData("msdb")]
    [InlineData("model")]
    [InlineData("tempdb")]
    public async Task RenameAsync_TablesOfASystemDatabase_LeaksNoNameOfTheUsers(string database)
    {
        const string Session = "fixtures";
        var raw = await EditedAsync("select-left-join", capture => WithDatabase(capture, Session, database));
        raw.Info.ShouldNotBeNull().Outcome.ShouldBe(BundleOutcome.Entry);
        raw.Capture.ShouldNotContain(Session);
        var schema = await File.ReadAllTextAsync(
            Path.Combine(FixtureCases.Root, Engine, "schema.sql"),
            TestContext.Current.CancellationToken
        );

        var renamed = await RenamedAsync(raw);

        renamed.Bundle.Info.ShouldBe(raw.Info with { Renamed = true });
        BundleLeaks.Find(renamed.Bundle, Part, new HashSet<string>(), schema, raw.Query).ShouldBeEmpty();
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

    // update-copies-agree with its reused parameter "before" written as the one given, in query.sql and in every
    // string of the capture that names it or a copy of it, and the answer of "parameters" the error 11502 that names
    // its first copy; and that copy.
    private static async Task<(Bundle Raw, string Copy)> UndeclaredParameterErrorAsync(string parameter)
    {
        string? copy = null;
        var raw = await EditedAsync(
            "update-copies-agree",
            capture =>
            {
                foreach (var call in capture["calls"]!.AsArray().Select(static call => call!.AsObject()))
                {
                    foreach (var member in RequestTexts)
                    {
                        if (call["request"]![member] is { } text)
                        {
                            call["request"]![member] = WithParameter(text.GetValue<string>(), parameter);
                        }
                    }
                }

                var parameters = CallOf(capture, "parameters");
                copy = FirstCopyIn(parameters["request"]!["sql"]!.GetValue<string>(), parameter);
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
            },
            query => WithParameter(query, parameter)
        );
        return (raw, copy.ShouldNotBeNull());
    }

    // Every database member of the node and its children, "database" of open and "sourceDatabase" of a row, that
    // names the database given, named as the other.
    private static void WithDatabase(JsonNode? node, string database, string other)
    {
        if (node is JsonArray items)
        {
            foreach (var item in items)
            {
                WithDatabase(item, database, other);
            }

            return;
        }

        if (node is not JsonObject members)
        {
            return;
        }

        foreach (var (name, value) in members.ToArray())
        {
            if (
                name is "database" or "sourceDatabase"
                && value is JsonValue text
                && text.GetValueKind() == JsonValueKind.String
                && text.GetValue<string>() == database
            )
            {
                members[name] = other;
            }
            else
            {
                WithDatabase(value, database, other);
            }
        }
    }

    // The text with the parameter "@before", and each copy of it, written as the parameter given.
    private static string WithParameter(string text, string parameter) => MyRegex().Replace(text, "@" + parameter);

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

    [GeneratedRegex("@before(?=_|[^A-Za-z0-9]|$)")]
    private static partial Regex MyRegex();
}
