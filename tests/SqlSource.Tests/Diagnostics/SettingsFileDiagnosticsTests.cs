using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.SettingsFiles;
using SqlSource.Snapshot.Reading;
using Xunit;

namespace SqlSource.Tests.Diagnostics;

public sealed class SettingsFileDiagnosticsTests
{
    private const string Path = "Queries/sqlsource.json";

    [Theory]
    [InlineData(nameof(SettingsFileErrorKind.InvalidJson), "SQLSRC015")]
    [InlineData(nameof(SettingsFileErrorKind.NotAnObject), "SQLSRC015")]
    [InlineData(nameof(SettingsFileErrorKind.UnknownKey), "SQLSRC016")]
    [InlineData(nameof(SettingsFileErrorKind.DuplicateKey), "SQLSRC017")]
    [InlineData(nameof(SettingsFileErrorKind.WrongType), "SQLSRC018")]
    [InlineData(nameof(SettingsFileErrorKind.InvalidValue), "SQLSRC019")]
    [InlineData(nameof(SettingsFileErrorKind.RepeatedKey), "SQLSRC020")]
    public void Create_EachKind_GivesItsDescriptor(string kind, string id)
    {
        var error = new SettingsFileError(
            Enum.Parse<SettingsFileErrorKind>(kind),
            new TextSpan(0, 1),
            SettingsFileKeys.Output,
            "argument",
            "other"
        );

        Create(error).Descriptor.Id.ShouldBe(id);
    }

    [Fact]
    public void Create_KindThatIsNoMember_NamesItsNumberAndNoOtherKind()
    {
        var error = new SettingsFileError((SettingsFileErrorKind)99, new TextSpan(0, 1), null, null, null);

        var diagnostic = Create(error);

        diagnostic.Descriptor.ShouldBe(SqlDiagnostics.SettingsFileNotAnObject);
        Message(diagnostic).ShouldBe("This settings file is not one JSON object: it has an error of kind 99");
    }

    [Fact]
    public void Create_Location_IsTheLineAndColumnOfTheSpan()
    {
        const string Text = /*lang=json,strict*/
            "{\n    \"outptu\": \"sql\"\n}";
        var error = SettingsFileReader.Read(Text, SettingPlace.Metadata).Errors.ShouldHaveSingleItem();

        var location = SettingsFileDiagnostics.Create(error, Path, SourceText.From(Text)).Location;

        location.Path.ShouldBe(Path);
        location.Span.ShouldBe(error.Span);
        location.LineSpan.Start.ShouldBe(new LinePosition(1, 4));
        location.LineSpan.End.ShouldBe(new LinePosition(1, 12));
    }

    [Fact]
    public void Create_UnknownKey_QuotesTheKey() =>
        Message(Error(SettingsFileErrorKind.UnknownKey, null, "outptu"))
            .ShouldBe("'outptu' is not a key of a settings file");

    [Fact]
    public void Create_DuplicateKey_SaysTheFirstApplies() =>
        Message(Error(SettingsFileErrorKind.DuplicateKey, null, "output"))
            .ShouldBe("'output' is written twice in this settings file, and the first applies");

    [Fact]
    public void Create_InvalidDialect_EndsWithTheAcceptedNames() =>
        Message(Error(SettingsFileErrorKind.InvalidValue, SettingsFileKeys.Dialect, "pgsql"))
            .ShouldBe("'pgsql' is not a valid value of dialect.  'dialect' takes " + SqlDialectName.Accepted + ".");

    [Fact]
    public void Create_InvalidValueOfAnotherSetting_HasNoHelp() =>
        Message(Error(SettingsFileErrorKind.InvalidValue, SettingsFileKeys.Output, "model"))
            .ShouldBe("'model' is not a valid value of output");

    [Fact]
    public void Create_DefaultBesideAnotherParameter_SaysItStandsAlone()
    {
        const string Text = /*lang=json,strict*/
            """{ "generatorParameters": ["keep-comments", "Default"] }""";
        var error = SettingsFileReader.Read(Text, SettingPlace.Metadata).Errors.ShouldHaveSingleItem();

        Message(SettingsFileDiagnostics.Create(error, Path, SourceText.From(Text)))
            .ShouldBe(
                "'Default' is not a valid value of generatorParameters.  'default' is the empty list and stands "
                    + "alone: write [] for it."
            );
    }

    [Fact]
    public void Create_UnknownGeneratorParameter_HasNoHelp() =>
        Message(Error(SettingsFileErrorKind.InvalidValue, SettingsFileKeys.GeneratorParameters, "keep-coments"))
            .ShouldBe("'keep-coments' is not a valid value of generatorParameters");

    [Fact]
    public void Create_TypeOverrideKeyInBrackets_GivesTheBracketHelp()
    {
        const string Text = /*lang=json,strict*/
            """{ "typeOverrides": { "[dbo].[Email]": "MyApp.Email" } }""";
        var error = SettingsFileReader.Read(Text, SettingPlace.Metadata).Errors.ShouldHaveSingleItem();

        var message = Message(SettingsFileDiagnostics.Create(error, Path, SourceText.From(Text)));

        message.ShouldStartWith("'[dbo].[Email]: MyApp.Email' is not a valid value of typeOverrides.  ");
        message.ShouldContain("dbo.Email");
        message.ShouldContain("\"dbo\".\"Email\"");
    }

    [Fact]
    public void Create_ColumnTypeKeyInBrackets_HasNoHelp() =>
        Message(Error(SettingsFileErrorKind.InvalidValue, SettingsFileKeys.ColumnTypes, "[users].[id]", "UserId"))
            .ShouldBe("'[users].[id]: UserId' is not a valid value of columnTypes");

    [Fact]
    public void Create_InvalidMember_QuotesKeyAndType()
    {
        const string Text = /*lang=json,strict*/
            """{ "typeOverrides": { " int4 ": " int? " } }""";
        var error = SettingsFileReader.Read(Text, SettingPlace.Metadata).Errors.ShouldHaveSingleItem();

        Message(SettingsFileDiagnostics.Create(error, Path, SourceText.From(Text)))
            .ShouldBe("'int4: int?' is not a valid value of typeOverrides");
    }

    [Theory]
    [InlineData(SettingsFileKeys.Output, "a string")]
    [InlineData(SettingsFileKeys.Root, "true or false")]
    [InlineData(SettingsFileKeys.GeneratorParameters, "an array of strings")]
    [InlineData(SettingsFileKeys.ColumnTypes, "an object whose values are strings")]
    public void Create_WrongType_NamesTheTypeTheKeyTakes(string key, string type) =>
        Message(Error(SettingsFileErrorKind.WrongType, key, null)).ShouldBe($"'{key}' takes {type}");

    // A blank string sets nothing for a key that takes one, but a member whose key or type is blank is reported, as
    // the section of SQLSRC019 says.
    [Fact]
    public void Create_MemberWithABlankKeyOrType_IsReported()
    {
        const string Text = /*lang=json,strict*/
            """
            { "typeOverrides": { " ": "int", "int4": "" }, "columnTypes": { "users.id": " " }, "output": " " }
            """;

        var errors = SettingsFileReader.Read(Text, SettingPlace.Metadata).Errors;

        errors
            .Select(error => Message(SettingsFileDiagnostics.Create(error, Path, SourceText.From(Text))))
            .ShouldBe([
                "': int' is not a valid value of typeOverrides",
                "'int4: ' is not a valid value of typeOverrides",
                "'users.id: ' is not a valid value of columnTypes",
            ]);
    }

    // The reader names a key of the table for every value of the wrong type; any other error still gets a message.
    [Theory]
    [InlineData(null)]
    [InlineData("outptu")]
    public void Create_WrongTypeOfNoKey_SaysAnotherJsonType(string? setting) =>
        Message(Error(SettingsFileErrorKind.WrongType, setting, null)).ShouldBe($"'{setting}' takes another JSON type");

    [Fact]
    public void Create_InvalidJson_NamesTheDepthLimit()
    {
        var message = Message(Error(SettingsFileErrorKind.InvalidJson, null, null));

        message.ShouldBe(
            "This settings file is not one JSON object: the text is not JSON, or nests deeper than 64 levels"
        );
        message.ShouldContain(SidecarTokenizer.MaxDepth.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Create_NotAnObject_SaysItsTopLevelIsNot() =>
        Message(Error(SettingsFileErrorKind.NotAnObject, null, null))
            .ShouldBe("This settings file is not one JSON object: its top level is not an object");

    [Fact]
    public void Create_RepeatedKey_QuotesBothKeysAndTheSetting()
    {
        const string Text = /*lang=json,strict*/
            """{ "typeOverrides": { "integer": "long", "int4": "int" } }""";
        var error = SettingsFileReader.Read(Text, SettingPlace.Metadata).Errors.ShouldHaveSingleItem();

        Message(SettingsFileDiagnostics.Create(error, Path, SourceText.From(Text)))
            .ShouldBe("'int4' is the same key as 'integer' in typeOverrides, and the first applies");
    }

    private static SettingsFileError Error(
        SettingsFileErrorKind kind,
        string? setting,
        string? argument,
        string? other = null
    ) => new(kind, new TextSpan(0, 1), setting, argument, other);

    private static DiagnosticInfo Create(SettingsFileError error) =>
        SettingsFileDiagnostics.Create(error, Path, SourceText.From("{ }"));

    private static string Message(SettingsFileError error) => Message(Create(error));

    private static string Message(DiagnosticInfo diagnostic) =>
        diagnostic.ToDiagnostic().GetMessage(CultureInfo.InvariantCulture);
}
