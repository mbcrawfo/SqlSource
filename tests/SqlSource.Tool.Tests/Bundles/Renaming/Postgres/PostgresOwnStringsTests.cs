using System.Text.Json.Nodes;
using Shouldly;
using SqlSource.Tool.Bundles.Renaming;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.Postgres;

public sealed class PostgresOwnStringsTests
{
    private const string SettingPath = "answer.settings[].value";
    private const string MessagePath = "answer.error.message";

    [Fact]
    public void Setting_SearchPath_RenamesTheUsersSchemas()
    {
        var scope = new FakeRenamingScope();

        PostgresOwnStrings
            .Setting(SettingOf("search_path", "\"$user\", public, app, \"My Schema\""), scope)
            .ShouldBe("\"$user\", public, s_app, \"s_My Schema\"");
        scope.Refusals.ShouldBeEmpty();
    }

    [Fact]
    public void Setting_SearchPathWithAQuotedComma_IsOneElement()
    {
        var scope = new FakeRenamingScope();

        PostgresOwnStrings
            .Setting(SettingOf("search_path", "\"my, \"\"schema\"\"\", public"), scope)
            .ShouldBe("\"s_my, \"\"schema\"\"\", public");
    }

    [Fact]
    public void Setting_SearchPathCatalog_IsKept() =>
        PostgresOwnStrings
            .Setting(SettingOf("search_path", "app, PG_CATALOG"), new FakeRenamingScope())
            .ShouldBe("s_app, PG_CATALOG");

    [Fact]
    public void Setting_SearchPathThatDoesNotRead_IsRefused()
    {
        var scope = new FakeRenamingScope();

        _ = PostgresOwnStrings.Setting(SettingOf("search_path", "\"app"), scope);

        scope.Refusals.ShouldBe([
            new RenamingRefusal(RefusalStep.Token, "capture.json call 0 'open' " + SettingPath, "\"app"),
        ]);
    }

    [Theory]
    [InlineData("plan_cache_mode", "force_generic_plan")]
    [InlineData("standard_conforming_strings", "on")]
    public void Setting_PlanCacheMode_IsKept(string name, string value)
    {
        var scope = new FakeRenamingScope();

        PostgresOwnStrings.Setting(SettingOf(name, value), scope).ShouldBe(value);
        scope.Refusals.ShouldBeEmpty();
    }

    [Fact]
    public void Setting_OtherName_IsRefused()
    {
        var scope = new FakeRenamingScope();

        _ = PostgresOwnStrings.Setting(SettingOf("application_name", "secret_app"), scope);

        scope.Refusals.ShouldBe([
            new RenamingRefusal(RefusalStep.Token, "capture.json call 0 'open' " + SettingPath, "application_name"),
        ]);
    }

    // A setting that open starts to read fails here until this renaming gives its value a rule.
    [Fact]
    public void SettingNames_AreTheOnesThisRenames() =>
        PostgresLive.SettingNames.ShouldBe(["search_path", "plan_cache_mode", "standard_conforming_strings"]);

    [Theory]
    [InlineData("42P18", "could not determine data type of parameter $2", "parameter $2")]
    [InlineData("42P08", "inconsistent types deduced for parameter $13", "parameter $13")]
    public void Message_42P18_IsTheParameterItNames(string sqlState, string message, string expected) =>
        PostgresOwnStrings.Message(MessageOf(sqlState, message), new FakeRenamingScope()).ShouldBe(expected);

    [Fact]
    public void Message_42P18WithoutDollar_IsEmpty() =>
        PostgresOwnStrings
            .Message(MessageOf("42P18", "could not determine data type of parameter"), new FakeRenamingScope())
            .ShouldBeEmpty();

    [Theory]
    [InlineData("42P01", "relation \"secret_table\" does not exist")]
    [InlineData("22P02", "invalid input syntax for type integer: \"secret value\"")]
    [InlineData(null, "connection to secret_host refused")]
    public void Message_UsersText_IsEmpty(string? sqlState, string message) =>
        PostgresOwnStrings.Message(MessageOf(sqlState, message), new FakeRenamingScope()).ShouldBeEmpty();

    [Theory]
    [InlineData(ConnectionErrors.MultiplexingRefused, null)]
    [InlineData(ConnectionErrors.UnreadableTypes, null)]
    [InlineData(PostgresLive.StatementNotListed, null)]
    [InlineData(PostgresLive.TransactionRefused, "08P01")]
    [InlineData(PostgresLive.StatementNotListed, "42P01")]
    public void Message_StatementNotListed_IsKept(string message, string? sqlState) =>
        PostgresOwnStrings.Message(MessageOf(sqlState, message), new FakeRenamingScope()).ShouldBe(message);

    [Fact]
    public void Message_OfTheDriversError_IsEmpty()
    {
        var field = new RenamingField(
            0,
            PostgresCallNames.Check,
            "answer.driverError.message",
            PostgresLive.StatementNotListed,
            new JsonObject { ["parameter"] = "id" }
        );

        PostgresOwnStrings.Message(field, new FakeRenamingScope()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("parameter $2")]
    [InlineData(PostgresLive.TransactionRefused)]
    public void WordsOfMessage_OfAWrittenText_IsNothing(string message) =>
        PostgresOwnStrings.WordsOfMessage(MessageOf("42P18", message)).ShouldBeEmpty();

    [Theory]
    [InlineData("relation \"secret_table\" does not exist")]
    [InlineData("parameter $2x")]
    [InlineData("parameter $")]
    public void WordsOfMessage_OfAnUnwrittenText_IsTheText(string message) =>
        PostgresOwnStrings.WordsOfMessage(MessageOf("42P01", message)).ShouldBe([message]);

    [Fact]
    public void WordsOfSetting_OfSearchPath_IsEachElement() =>
        PostgresOwnStrings
            .WordsOfSetting(SettingOf("search_path", "\"$user\", public, s_app, \"s_My \"\"Schema\"\"\", pg_catalog"))
            .ShouldBe(["s_app", "\"s_My \"\"Schema\"\"\""]);

    [Fact]
    public void WordsOfSetting_OfAKeptSetting_IsNothing() =>
        PostgresOwnStrings.WordsOfSetting(SettingOf("plan_cache_mode", "auto")).ShouldBeEmpty();

    [Theory]
    [InlineData("search_path", "\"app")]
    [InlineData("application_name", "secret_app")]
    public void WordsOfSetting_OfAValueNotRead_IsTheValue(string name, string value) =>
        PostgresOwnStrings.WordsOfSetting(SettingOf(name, value)).ShouldBe([value]);

    private static RenamingField SettingOf(string name, string value) =>
        new(0, PostgresCallNames.Open, SettingPath, value, new JsonObject { ["name"] = name, ["value"] = value });

    private static RenamingField MessageOf(string? sqlState, string message) =>
        new(
            1,
            PostgresCallNames.Prepare,
            MessagePath,
            message,
            new JsonObject { ["sqlState"] = sqlState, ["message"] = message }
        );
}
