using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.SqlServer;

// A name in three spellings, whose first replacement under some keys holds no hex letter and so tells two cases apart
// at most: the third spelling then has a replacement of its own, and the renaming of a key falls back.
public sealed partial class SqlServerErrorRenamingTests
{
    // The statement of update-copies-agree, which the cases below write again.
    private const string Statement =
        "UPDATE dbo.users SET deleted_at = SYSUTCDATETIME() WHERE created_at < @before OR deleted_at < @before;";

    // The parameter of the cases below, which update-copies-agree names "before", an engine word kept as it is.
    private const string Cutoff = "cutoff";

    // The parameter in three spellings, which the pipeline takes for one parameter.
    private static readonly string[] CutoffSpellings = ["cutoff", "CUTOFF", "Cutoff"];

    // The column "deleted_at" in three spellings, which the pipeline compares in no answer.
    private static readonly string[] DeletedAtSpellings = ["deleted_at", "DELETED_AT", "Deleted_At"];

    [Fact]
    public async Task RenameAsync_AKeyThatFallsBackOnAParameter_IsRefused()
    {
        var raw = await ParameterInThreeSpellingsAsync();
        var key = KeyWhere(CutoffSpellings, NameClass.Parameter, fallsBack: true);

        var refusal = await SqlServerRenamings.RefusedAsync(raw, key);

        refusal.ShouldBe(new RenamingRefusal(RefusalStep.RequestSql, "capture.json call 'parameters'", null));
    }

    [Fact]
    public async Task RenameAsync_AKeyThatDoesNotFallBackOnAParameter_IsOneParameter()
    {
        var raw = await ParameterInThreeSpellingsAsync();
        var key = KeyWhere(CutoffSpellings, NameClass.Parameter, fallsBack: false);

        var renamed = await SqlServerRenamings.RenamedAsync(raw, key);

        var replacements = ReplacementsOf(renamed, CutoffSpellings);
        replacements.Distinct(StringComparer.Ordinal).Count().ShouldBe(CutoffSpellings.Length);
        replacements.ShouldAllBe(replacement =>
            string.Equals(replacement, replacements[0], StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public async Task RenameAsync_KeysThatFallBackThenOneThatDoesNot_RenamesWithTheLast()
    {
        var raw = await ParameterInThreeSpellingsAsync();
        RenamingKey[] keys =
        [
            KeyWhere(CutoffSpellings, NameClass.Parameter, fallsBack: true),
            KeyWhere(CutoffSpellings, NameClass.Parameter, fallsBack: false),
        ];
        var calls = 0;

        var renamed = await BundleRenamer.RenameAsync(
            raw,
            SqlServerRenamings.Part,
            new SqlServerDescriber(),
            () => keys[calls++],
            TestContext.Current.CancellationToken
        );

        calls.ShouldBe(2);
        renamed.Failure.ShouldBeNull();
        var bundle = renamed.Value.ShouldNotBeNull();
        bundle.Mapping.ShouldBe((await SqlServerRenamings.RenamedAsync(raw, keys[1])).Mapping);
    }

    [Fact]
    public async Task RenameAsync_EveryKeyFallsBack_StopsAfterTheAttempts()
    {
        var raw = await ParameterInThreeSpellingsAsync();
        var key = KeyWhere(CutoffSpellings, NameClass.Parameter, fallsBack: true);
        var calls = 0;

        var renamed = await BundleRenamer.RenameAsync(
            raw,
            SqlServerRenamings.Part,
            new SqlServerDescriber(),
            () =>
            {
                calls++;
                calls.ShouldBeLessThanOrEqualTo(BundleRenamer.Attempts);
                return key;
            },
            TestContext.Current.CancellationToken
        );

        calls.ShouldBe(BundleRenamer.Attempts);
        renamed.Value.ShouldBeNull();
        renamed.Failure.ShouldBe(new RenamingRefusal(RefusalStep.RequestSql, "capture.json call 'parameters'", null));
    }

    [Fact]
    public async Task RenameAsync_ARefusalThatDoesNotFallBack_TakesOneKey()
    {
        const string Extra = "a value of no member";
        var raw = await EditedAsync(
            "select-left-join",
            capture => CallOf(capture, Columns)["answer"]!["rows"]![0]!["extra"] = Extra
        );
        var calls = 0;

        var renamed = await BundleRenamer.RenameAsync(
            raw,
            SqlServerRenamings.Part,
            new SqlServerDescriber(),
            () => BundleRenamings.Key((byte)++calls),
            TestContext.Current.CancellationToken
        );

        calls.ShouldBe(1);
        renamed.Failure.ShouldNotBeNull().Step.ShouldBe(RefusalStep.Token);
    }

    // A column in three spellings that no answer compares: the renaming of a key that falls back passes, and is kept.
    [Fact]
    public async Task RenameAsync_AKeyThatFallsBackAndPasses_TakesOneKey()
    {
        var raw = await WithStatementAsync(
            "UPDATE dbo.users SET deleted_at = SYSUTCDATETIME() WHERE created_at < @cutoff OR DELETED_AT < @cutoff "
                + "OR Deleted_At IS NULL;"
        );
        var key = KeyWhere(DeletedAtSpellings, NameClass.Alias, fallsBack: true);
        var calls = 0;

        var renamed = await BundleRenamer.RenameAsync(
            raw,
            SqlServerRenamings.Part,
            new SqlServerDescriber(),
            () =>
            {
                calls++;
                return key;
            },
            TestContext.Current.CancellationToken
        );

        calls.ShouldBe(1);
        renamed.Failure.ShouldBeNull();
        var replacements = ReplacementsOf(renamed.Value.ShouldNotBeNull(), DeletedAtSpellings);
        replacements.ShouldContain(replacement =>
            !string.Equals(replacement, replacements[0], StringComparison.OrdinalIgnoreCase)
        );
    }

    // update-copies-agree with its parameter used three times, in the three spellings of CutoffSpellings.
    private static Task<Bundle> ParameterInThreeSpellingsAsync() =>
        WithStatementAsync(
            "UPDATE dbo.users SET deleted_at = SYSUTCDATETIME() WHERE created_at < @cutoff OR deleted_at < @CUTOFF "
                + "OR created_at = @Cutoff;"
        );

    // update-copies-agree with its statement written as the one given, whose parameter is "cutoff": the parameters
    // request copies each of its uses, in any case, to a copy of its own, and the columns request declares it.
    private static async Task<Bundle> WithStatementAsync(string statement)
    {
        var uses = CutoffUse().Count(statement);
        var use = 0;
        var copied = CutoffUse().Replace(statement, _ => $"@{Cutoff}_{++use}");
        var raw = await EditedAsync(
            "update-copies-agree",
            capture =>
            {
                var columns = CallOf(capture, Columns)["request"]!;
                columns["sql"] = statement;
                columns["params"] = WithParameter(columns["params"]!.GetValue<string>(), Cutoff);
                var parameters = CallOf(capture, "parameters");
                parameters["request"]!["sql"] = copied;
                var reused = parameters["request"]!["reused"]!.AsArray().Single()!.AsObject();
                reused["name"] = Cutoff;
                reused["copies"] = new JsonArray([
                    .. Enumerable.Range(1, uses).Select(static number => (JsonNode)$"{Cutoff}_{number}"),
                ]);
                var rows = parameters["answer"]!["rows"]!.AsArray();
                var row = rows[0]!.ToJsonString();
                rows.Clear();
                foreach (var number in Enumerable.Range(1, uses))
                {
                    var copy = JsonNode.Parse(row)!.AsObject();
                    copy["name"] = $"@{Cutoff}_{number}";
                    rows.Add(copy);
                }
            },
            query => query.Replace(Statement, statement, StringComparison.Ordinal)
        );
        raw.Info.ShouldNotBeNull().Outcome.ShouldBe(BundleOutcome.Entry);
        return raw;
    }

    // The first key of one byte whose mapping, made as the renaming makes it, falls back or does not on the spellings
    // of one name named in their order.
    private static RenamingKey KeyWhere(string[] spellings, NameClass cls, bool fallsBack)
    {
        for (var fill = 0; fill <= byte.MaxValue; fill++)
        {
            var key = BundleRenamings.Key((byte)fill);
            var mapping = new NameMapping(key, SqlServerRenamings.Part.Fold, SqlServerRenamings.Part.Spelled);
            foreach (var spelling in spellings)
            {
                _ = mapping.Name(spelling, quoted: false, cls);
            }

            if (mapping.FellBack == fallsBack)
            {
                return key;
            }
        }

        throw new InvalidOperationException($"No key of one byte falls back: {fallsBack}.");
    }

    // The replacements of the spellings, in their order.
    private static string[] ReplacementsOf(RenamedBundle renamed, string[] spellings) =>
        [.. spellings.Select(spelling => renamed.Mapping.Single(name => name.Original == spelling).Replacement)];

    [GeneratedRegex("@cutoff\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CutoffUse();
}
