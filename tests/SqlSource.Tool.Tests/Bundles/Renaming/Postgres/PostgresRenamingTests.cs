using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.Postgres;

// PostgreSQL's part assembled: its dispatch of the strings that are its own, and a fixture renamed end to end through
// BundleRenamer with the describer a replay makes.
public sealed class PostgresRenamingTests
{
    private const string Fixture = "select-declared-domain";

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
        BundleLeaks.Of(result.Bundle, [fixture.Query], PostgresWords.IsEngineWord).ShouldBeEmpty();
    }

    [Fact]
    public void Members_OfTheEngine_ArePostgresWordsAndRules()
    {
        var engine = PostgresRenaming.Instance;

        engine.Dialect.ShouldBe(SqlDialect.PostgreSql);
        engine.Fold("Users", quoted: false).ShouldBe("users");
        engine.IsEngineWord("SELECT", quoted: false).ShouldBeTrue();
        engine.RuleOf(PostgresCallNames.Explain, "answer.plan").ShouldBe(FieldRule.Own);
        engine.CopyOf("id_1", new HashSet<string>(StringComparer.Ordinal) { "id" }).ShouldBeNull();
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

    private static bool WordIn(string text, string word) =>
        Regex.IsMatch(text, $"(?<![\\w$]){Regex.Escape(word)}(?![\\w$])", RegexOptions.IgnoreCase);
}
