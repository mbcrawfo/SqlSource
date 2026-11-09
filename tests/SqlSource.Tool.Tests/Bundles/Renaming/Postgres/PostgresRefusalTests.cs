using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Statements;
using SqlSource.Tool.Describing.Postgres.Walk;
using SqlSource.Tool.Testing.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.Postgres;

// Bundles built by hand from PostgreSQL's fixtures: what the renaming refuses, at its place, and the server's errors
// it renames, whose renamed bundle replays to the original's failure and holds none of the server's text.
public sealed class PostgresRefusalTests
{
    // A case whose explain plans a left join, the call at this index.
    private const string Planned = "select-left-join";
    private const int ExplainCall = 7;

    // A case of one declared type, and one of two, the second a domain of the schema's.
    private const string Declared = "select-declared-domain";
    private const string TwoDeclared = "insert-declared";

    private const string DescribeParameters = "describe parameters";

    [Fact]
    public async Task RenameAsync_PlanStringThatDoesNotRead_IsRefusedAtThePlan()
    {
        var raw = Edited(Planned, calls => EditPlan(calls[ExplainCall]!, plan => plan[PlanKeys.Output]![0] = "(u.id"));

        var renamed = await PostgresRenamings.RenameAsync(raw);

        renamed.Failure.ShouldBe(
            new RenamingRefusal(
                RefusalStep.PlanString,
                $"capture.json call {ExplainCall} 'explain' answer.plan",
                "(u.id"
            )
        );
    }

    [Fact]
    public async Task RenameAsync_UnknownStringMember_IsRefusedAtIt()
    {
        var raw = Edited(Declared, calls => calls[3]!["answer"]!["tables"]![0]!["comment"] = "the users of the app");

        var renamed = await PostgresRenamings.RenameAsync(raw);

        renamed.Failure.ShouldBe(
            new RenamingRefusal(
                RefusalStep.Token,
                "capture.json call 3 'tables' answer.tables[].comment",
                "the users of the app"
            )
        );
    }

    [Fact]
    public async Task RenameAsync_UnknownSetting_IsRefused()
    {
        var raw = Edited(
            Declared,
            calls =>
                calls[0]!["answer"]!["settings"]!
                    .AsArray()
                    .Add(new JsonObject { ["name"] = "work_mem", ["value"] = "4MB" })
        );

        var renamed = await PostgresRenamings.RenameAsync(raw);

        renamed.Failure.ShouldBe(
            new RenamingRefusal(RefusalStep.Token, "capture.json call 0 'open' answer.settings[].value", "work_mem")
        );
    }

    [Fact]
    public async Task RenameAsync_ErrorOfPrepare_ReplaysToTheSameFailure()
    {
        var bundle = BundleReplays.PrepareTypeNotDetermined();
        var raw = RawBundles.Of(
            bundle.Query,
            BundleReplays.CaptureOf(bundle),
            expected: null,
            BundleOutcome.Failure("SQLSRC226", DescribeParameters),
            []
        );

        var renamed = await PostgresRenamings.RenamedAsync(raw);

        renamed.Bundle.Info.ShouldNotBeNull().Outcome.ShouldBe(BundleOutcome.Failure("SQLSRC226", DescribeParameters));
        PrepareError(renamed.Bundle)["message"]!.GetValue<string>().ShouldBe("parameter $1");
    }

    [Fact]
    public async Task RenameAsync_ErrorWithTheUsersText_KeepsNoneOfIt()
    {
        var failed = PrepareFailed(
            Declared,
            "42P01",
            "relation \"secret_table\" does not exist",
            position: null,
            hint: "Perhaps you meant to reference the table \"secret_other\"."
        );
        var raw = await WithItsFailureAsync(failed, "SQLSRC225");

        var renamed = await PostgresRenamings.RenamedAsync(raw);

        var error = PrepareError(renamed.Bundle);
        error["sqlState"]!.GetValue<string>().ShouldBe("42P01");
        error["message"]!.GetValue<string>().ShouldBeEmpty();
        RenamingBundles
            .FilesOf(renamed.Bundle)
            .ShouldNotContain(file => file.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RenameAsync_PrepareErrorInTheTypeList_ReplaysToSQLSRC228()
    {
        var fixture = PostgresRenamings.Fixture(TwoDeclared);
        var prepare = RequestOf(BundleReplays.CaptureOf(WrittenBundles.Read(fixture.Folder)));
        prepare.Types.ShouldBe(["varchar(50)", "email_t", "unknown"]);

        // Inside email_t, the second item, which the renaming makes longer.
        var position =
            ParameterPlaceholders.Characters(ParameterPlaceholders.Opening(prepare.Statement))
            + ParameterPlaceholders.Characters(prepare.Types[0])
            + ParameterPlaceholders.Separator.Length
            + 5;
        var failed = PrepareFailed(TwoDeclared, "42704", "type \"email_t\" does not exist", position, hint: null);
        var raw = await WithItsFailureAsync(failed, "SQLSRC228");

        var renamed = await PostgresRenamings.RenamedAsync(raw);

        renamed.Bundle.Info.ShouldNotBeNull().Outcome.ShouldBe(BundleOutcome.Failure("SQLSRC228", DescribeParameters));
        var request = RequestOf(BundleReplays.CaptureOf(renamed.Bundle));
        request.Types[1].ShouldNotBe("email_t");
        var moved = PrepareError(renamed.Bundle)["position"]!.GetValue<int>();
        ParameterPlaceholders.TypeAt(request.Statement, request.Types, moved).ShouldBe(1);
    }

    // A table named as a word of the engine's, partitioned or inherited and scanned without an alias, is printed
    // name_1: neither the renaming nor the token check can tell the planner's suffix from the user's name.
    [Fact]
    public async Task RenameAsync_PartitionChildOfAnEngineWord_IsRefusedAtItsToken()
    {
        var raw = Edited(
            Planned,
            calls =>
                EditPlan(
                    calls[ExplainCall]!,
                    plan =>
                    {
                        var scan = plan[PlanKeys.Plans]![0]![PlanKeys.Plans]![1]!;
                        scan[PlanKeys.RelationName] = "name";
                        scan[PlanKeys.Alias] = "name_1";
                    }
                )
        );

        var renamed = await PostgresRenamings.RenameAsync(raw);

        renamed.Failure.ShouldBe(
            new RenamingRefusal(RefusalStep.Token, $"capture.json call {ExplainCall} 'explain' answer.plan", "name_1")
        );
    }

    // Only _<digits> is the planner's suffix: a plan's word of a mapped name and a letter suffix, users_secretword, is
    // a name of its own, renamed whole, and the bundle is renamed.
    [Fact]
    public async Task RenameAsync_PlanWordWithAMappedStemAndALetterSuffix_KeepsNoneOfIt()
    {
        var raw = Edited(
            Planned,
            calls => EditPlan(calls[ExplainCall]!, plan => plan[PlanKeys.Filter] = "(users_secretword.id > 0)")
        );

        var renamed = await PostgresRenamings.RenamedAsync(raw);

        RenamingBundles
            .FilesOf(renamed.Bundle)
            .ShouldNotContain(file => file.Contains("secretword", StringComparison.OrdinalIgnoreCase));
    }

    // The raw bundle of a case whose calls are edited, with the case's entry.
    private static Bundle Edited(string name, Action<JsonArray> edit)
    {
        var fixture = PostgresRenamings.Fixture(name);
        var capture = JsonNode.Parse(fixture.Capture)!;
        edit(capture["calls"]!.AsArray());
        return RawBundles.Of(
            fixture.Query,
            CaptureJson.Read(capture.ToJsonString()).Value.ShouldNotBeNull(),
            fixture.Expected,
            BundleOutcome.Entry,
            fixture.ExplainSettings
        );
    }

    // An explain call whose plan's root node is edited.
    private static void EditPlan(JsonNode call, Action<JsonNode> edit)
    {
        var answer = call["answer"]!;
        var plan = JsonNode.Parse(answer["plan"]!.GetValue<string>())!;
        edit(plan[0]![PlanKeys.Plan]!);
        answer["plan"] = plan.ToJsonString();
    }

    // The case's open and its prepare, whose answer is the server's error, as a recording writes one.  The describer
    // asks nothing after a prepare that failed.
    private static Bundle PrepareFailed(string name, string sqlState, string message, int? position, string? hint)
    {
        var recorded = BundleReplays.CaptureOf(WrittenBundles.Read(PostgresRenamings.Fixture(name).Folder));
        var error = new PostgresServerError(sqlState, message, Detail: null, hint, position);
        var answer = JsonSerializer.SerializeToElement(
            new PostgresPrepareAnswer(ParameterTypes: null, error),
            CaptureJson.Options
        );
        var capture = new DescribeCapture(PostgresRenamings.Engine);
        capture.Add(recorded.Calls.Single(static call => call.Method == PostgresCallNames.Open));
        var prepare = recorded.Calls.Single(static call => call.Method == PostgresCallNames.Prepare);
        capture.Add(new CaptureCall(prepare.Method, prepare.Request, answer));
        return new Bundle(Info: null, PostgresRenamings.Fixture(name).Query, CaptureJson.Write(capture), null, []);
    }

    // The bundle as a raw bundle of the failure it replays to, which must be the one given, at describe parameters.
    private static async Task<Bundle> WithItsFailureAsync(Bundle bundle, string id)
    {
        var replayed = await BundleReplays.ReplayAsync(bundle, BundleInfos.Path);
        replayed.ShouldBe(new ReplayOutcome.Refused(id, DescribeParameters));

        return RawBundles.Of(
            bundle.Query,
            BundleReplays.CaptureOf(bundle),
            expected: null,
            BundleOutcome.Failure(id, DescribeParameters),
            []
        );
    }

    private static PostgresPrepareRequest RequestOf(DescribeCapture capture) =>
        capture
            .Calls.Single(static call => call.Method == PostgresCallNames.Prepare)
            .Request.Deserialize<PostgresPrepareRequest>(CaptureJson.Options)
            .ShouldNotBeNull();

    private static JsonNode PrepareError(Bundle bundle) =>
        JsonNode.Parse(bundle.Capture)!["calls"]!
            .AsArray()
            .Single(static call => call!["method"]!.GetValue<string>() == PostgresCallNames.Prepare)!["answer"]![
            "error"
        ]!;
}
