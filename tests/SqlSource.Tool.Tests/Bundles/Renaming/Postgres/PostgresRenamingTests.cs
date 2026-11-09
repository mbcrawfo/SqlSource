using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing.Bundles;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.Postgres;

// PostgreSQL's part assembled: its dispatch of the strings that are its own, and a fixture renamed end to end through
// BundleRenamer with the describer a replay makes.
public sealed class PostgresRenamingTests
{
    private const string Fixture = "select-declared-domain";

    // The fixture with a token, and where the cases below add markers and tokens to its query.
    private const string TokenFixture = "delete-declared-only";
    private const string ParamMarker = "-- param: @tenant int\n";
    private const string FilterToken = "{{filter:AND deleted_at IS NOT NULL}}";

    // A word of the engine's vocabulary only as written, since its unquoted fold is not.
    private const string QuotedEngineWord = "RI_FKey_check_ins";

    // A member the table gives the rule Own that RenameOwn does not know would throw at a bundle that holds it.
    [Fact]
    public void OwnOf_EveryMemberTheTableRulesOwn_IsOneOfPostgresqlsOwnStrings()
    {
        var owns = PostgresFieldRules
            .Members.Select(static member => member.Split(' ', 2))
            .Where(static parts => PostgresFieldRules.RuleOf(parts[0], parts[1]) is { Kind: FieldRuleKind.Own })
            .ToList();

        owns.ShouldNotBeEmpty();
        foreach (var parts in owns)
        {
            _ = Should.NotThrow(() => PostgresRenaming.OwnOf(parts[0], parts[1]), parts[0] + " " + parts[1]);
        }
    }

    [Fact]
    public void Spelled_UnquotedAndQuoted_FoldsOnlyTheUnquoted()
    {
        PostgresRenaming.Instance.Spelled("Users", false).ShouldBe("users");
        PostgresRenaming.Instance.Spelled("Users", true).ShouldBe("Users");
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("$12", false)]
    [InlineData("$", false)]
    [InlineData("a", false)]
    public void IsNumberStart_Text_IsAnAsciiDigit(string text, bool expected) =>
        PostgresRenaming.Instance.IsNumberStart(text, 0, text.Length).ShouldBe(expected);

    [Fact]
    public async Task RenameAsync_SelectDeclaredDomain_RenamesAndReplays()
    {
        var fixture = PostgresRenamings.Fixture(Fixture);
        var raw = RawBundles.OfFixture(fixture);

        var renamed = await PostgresRenamings.RenameAsync(raw);

        var result = renamed.Value.ShouldNotBeNull(renamed.Failure?.ToString());
        var files = RenamingBundles.FilesOf(result.Bundle).ToList();
        foreach (var original in (string[])["users", "email_t", "id", "email", "FindUserByEmail"])
        {
            files.ShouldNotContain(file => WordIn(file, original), original);
        }

        foreach (var kept in (string[])["name", "pg_catalog"])
        {
            result.Bundle.Expected.ShouldNotBeNull().ShouldContain($"\"{kept}\"");
        }

        Provenance.Of(result.Bundle.Expected!).ShouldBe(Provenance.Of(raw.Expected!));
        PostgresBundleLeaks.Of(result.Bundle, [fixture.Query], PostgresWords.IsEngineWord).ShouldBeEmpty();
    }

    [Fact]
    public void Members_OfTheEngine_ArePostgresWordsAndRules()
    {
        var engine = PostgresRenaming.Instance;

        engine.Dialect.ShouldBe(SqlDialect.PostgreSql);
        engine.Fold("Users", quoted: false).ShouldBe("users");
        engine.IsEngineWord("SELECT", quoted: false).ShouldBeTrue();
        engine.RuleOf(PostgresCallNames.Explain, "answer.plan").ShouldBe(FieldRule.Own);
        var reused = new JsonObject { ["name"] = "id", ["copies"] = new JsonArray("id_1") };
        var request = new JsonObject { ["reused"] = new JsonArray(reused), ["copies"] = new JsonArray("id_1") };
        var call = new JsonObject { ["request"] = request };
        engine.CopiesIn(PostgresCallNames.Prepare, call).ShouldBeEmpty();
        engine.CopiesIn("parameters", call).ShouldBeEmpty();
    }

    [Fact]
    public void RenameOwn_OpenSetting_RenamesTheSearchPath() =>
        PostgresRenaming
            .Instance.RenameOwn(
                new RenamingField(
                    0,
                    PostgresCallNames.Open,
                    "answer.settings[].value",
                    "app, public",
                    new() { ["name"] = "search_path" }
                ),
                new FakeRenamingScope()
            )
            .ShouldBe("s_app, public");

    [Theory]
    [InlineData(PostgresCallNames.Prepare, "answer.error.message")]
    [InlineData(PostgresCallNames.Check, "answer.driverError.message")]
    public void RenameOwn_Message_IsTheToolsText(string method, string path)
    {
        var field = new RenamingField(
            1,
            method,
            path,
            "relation \"users\" does not exist",
            new() { ["sqlState"] = "42P01" }
        );

        PostgresRenaming.Instance.RenameOwn(field, new FakeRenamingScope()).ShouldBeEmpty();
        PostgresRenaming.Instance.WordsOf(field).ShouldBe([field.Value]);
    }

    [Fact]
    public void RenameOwn_ExplainPlan_RenamesItsNames()
    {
        var plan = /*lang=json,strict*/
            """[{"Plan": {"Node Type": "Seq Scan", "Relation Name": "users", "Alias": "users"}}]""";
        var field = new RenamingField(2, PostgresCallNames.Explain, "answer.plan", plan, Row: null);
        var scope = new FakeRenamingScope();

        var renamed = PostgresRenaming.Instance.RenameOwn(field, scope);

        scope.Refusals.ShouldBeEmpty();
        renamed.ShouldContain("\"Relation Name\": \"t_users\"");
        PostgresRenaming.Instance.WordsOf(field with { Value = renamed }).ShouldContain("\"t_users\"");
    }

    [Theory]
    [InlineData(PostgresCallNames.Prepare, "answer.settings[].value")]
    [InlineData(PostgresCallNames.Open, "answer.driverError.message")]
    [InlineData(PostgresCallNames.Check, "answer.plan")]
    [InlineData(PostgresCallNames.Columns, "answer.columns[].name")]
    public void RenameOwn_AnyOtherMember_Throws(string method, string path)
    {
        var field = new RenamingField(0, method, path, "x", Row: null);

        _ = Should.Throw<InvalidOperationException>(() =>
            PostgresRenaming.Instance.RenameOwn(field, new FakeRenamingScope())
        );
        _ = Should.Throw<InvalidOperationException>(() => PostgresRenaming.Instance.WordsOf(field));
    }

    // Two tokens whose names differ by case alone, as each place that names a token writes them: the generator takes
    // them for two, so they have two replacements.
    [Theory]
    [InlineData("", "{{Extra:}}{{extra:}}")]
    [InlineData("-- token: {{Extra:}}\n", "{{Extra}}{{extra:}}")]
    [InlineData("-- token-ignore: Extra\n", "{{extra:}}")]
    public async Task RenameAsync_TokensThatDifferByCase_AreTwoNames(string markers, string tokens)
    {
        var raw = await WithTokensAsync(markers, tokens);

        var renamed = await PostgresRenamings.RenameAsync(raw);

        var result = renamed.Value.ShouldNotBeNull(renamed.Failure?.ToString());
        var upper = result.Mapping.Single(static name => name.Original == "Extra");
        var lower = result.Mapping.Single(static name => name.Original == "extra");
        upper.Class.ShouldBe(NameClass.Token);
        lower.Class.ShouldBe(NameClass.Token);
        upper.Replacement.ShouldNotBe(lower.Replacement);
    }

    // A token whose name is the engine's word only as written: it is kept, and each place that names it passes the
    // token check, which asks about it as the renaming did.
    [Theory]
    [InlineData("", "{{" + QuotedEngineWord + ":}}")]
    [InlineData("-- token: {{" + QuotedEngineWord + ":}}\n", "{{" + QuotedEngineWord + "}}")]
    [InlineData("-- token-ignore: " + QuotedEngineWord + "\n", "")]
    [InlineData("", " /*+ {{" + QuotedEngineWord + ":}} */", " /*+  */")]
    public async Task RenameAsync_ATokenNamedAsAnEngineWordAsWritten_IsKept(
        string markers,
        string tokens,
        string sent = ""
    )
    {
        var raw = await WithTokensAsync(markers, tokens, sent);

        var renamed = await PostgresRenamings.RenameAsync(raw);

        var result = renamed.Value.ShouldNotBeNull(renamed.Failure?.ToString());
        result.Bundle.Query.ShouldContain(QuotedEngineWord);
        result.Mapping.ShouldNotContain(static name => name.Original == QuotedEngineWord);
    }

    // An ignored token is literal text, which reaches the SQL sent in a hint: there it has the replacement it has in
    // query.sql, whatever its case, so the requests made from the renamed query are the renamed capture's.
    [Theory]
    [InlineData("Extra")]
    [InlineData("extra")]
    public async Task RenameAsync_AnIgnoredTokenInTheSqlSent_HasItsReplacementInTheQuery(string name)
    {
        var hint = $" /*+ {{{{{name}}}}} */";
        var raw = await WithTokensAsync($"-- token-ignore: {name}\n", hint, hint);
        raw.Capture.ShouldContain("{{" + name + "}}");

        var renamed = await PostgresRenamings.RenameAsync(raw);

        var result = renamed.Value.ShouldNotBeNull(renamed.Failure?.ToString());
        var replacement = result.Mapping.Single(mapped => mapped.Original == name).Replacement;
        result.Bundle.Capture.ShouldContain("{{" + replacement + "}}");
    }

    // delete-declared-only with the markers given after its declaration and the tokens given after its token, and the
    // entry that replaying it raw gives.  A token added has an empty default, so the requests are the case's own, but
    // for what the tokens add to the SQL sent, given as sent, which ends each request's SQL and the text sent.
    private static async Task<Bundle> WithTokensAsync(string markers, string tokens, string sent = "")
    {
        var fixture = PostgresRenamings.Fixture(TokenFixture);
        var query = fixture
            .Query.Replace(ParamMarker, ParamMarker + markers, StringComparison.Ordinal)
            .Replace(FilterToken, FilterToken + tokens, StringComparison.Ordinal);
        var capture = JsonNode.Parse(fixture.Capture)!;
        foreach (var call in capture["calls"]!.AsArray())
        {
            if (call!["request"]?["sql"] is { } sql)
            {
                call["request"]!["sql"] = sql.GetValue<string>() + sent;
            }

            // The text the driver sent, which check's answer records.
            if (call["answer"]?["sent"]?["text"] is { } text)
            {
                call["answer"]!["sent"]!["text"] = text.GetValue<string>() + sent;
            }
        }

        var raw = RawBundles.OfFixture(fixture) with
        {
            Query = query,
            Capture = capture.ToJsonString(),
            Expected = null,
        };
        var replayed = await BundleReplays.ReplayAsync(raw, BundleInfos.Path);
        var entry = replayed.ShouldBeOfType<ReplayOutcome.Described>(replayed.ToString()).Entry;
        return raw with
        {
            Expected = SidecarWriter.Write(
                new Sidecar(SidecarFormat.Version, FixtureReplay.ToolVersion, new EquatableArray<SidecarEntry>([entry]))
            ),
        };
    }

    private static bool WordIn(string text, string word) =>
        Regex.IsMatch(text, $"(?<![\\w$]){Regex.Escape(word)}(?![\\w$])", RegexOptions.IgnoreCase);
}
