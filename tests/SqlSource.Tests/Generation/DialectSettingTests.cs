using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Tests.Generator;
using Xunit;

namespace SqlSource.Tests.Generation;

public class DialectSettingTests
{
    private const string Path = "/app/Repo/Users.sql";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n")]
    public void Parse_MissingOrEmpty_IsNotSet(string? value) =>
        DialectSetting.Parse(value).ShouldBe(new DialectSetting(null, null));

    [Theory]
    [InlineData("postgres", nameof(SqlDialect.PostgreSql))]
    [InlineData("MSSQL", nameof(SqlDialect.SqlServer))]
    [InlineData("\n    mysql\n  ", nameof(SqlDialect.MySql))]
    [InlineData("ansi", nameof(SqlDialect.Ansi))]
    public void Parse_NameOfADialect_IsThatDialect(string value, string expected)
    {
        var setting = DialectSetting.Parse(value);

        setting.Dialect.ShouldBe(new SqlDialectChoice(Enum.Parse<SqlDialect>(expected), SqlDialectOptions.None));
        setting.InvalidValue.ShouldBeNull();
    }

    [Theory]
    [InlineData("mysql,ansi-quotes")]
    [InlineData("\n    MySQL ,\n    ANSI_QUOTES\n  ")]
    public void Parse_NameWithAnOption_IsThatDialectWithTheOption(string value) =>
        DialectSetting
            .Parse(value)
            .ShouldBe(new DialectSetting(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.AnsiQuotes), null));

    [Theory]
    [InlineData("pgsql")]
    [InlineData("sql server")]
    [InlineData(" Postgres 16 ")]
    [InlineData("true")]
    [InlineData("postgres,ansi-quotes")]
    [InlineData("mysql,")]
    [InlineData("mysql,ansi")]
    public void Parse_AnythingElse_IsAnsiAndKeepsTheValueAsWritten(string value) =>
        DialectSetting.Parse(value).ShouldBe(new DialectSetting(SqlDialect.Ansi, value));

    [Fact]
    public void ReadProperty_ProjectWithAndWithoutTheProperty_ReadsItFromTheGlobalOptions()
    {
        DialectSetting
            .ReadProperty(new TestOptionsProvider(null).GlobalOptions)
            .ShouldBe(new DialectSetting(null, null));
        DialectSetting
            .ReadProperty(new TestOptionsProvider(null, "oracle").GlobalOptions)
            .ShouldBe(new DialectSetting(SqlDialect.Oracle, null));
        DialectSetting
            .ReadProperty(new TestOptionsProvider(null, "orcl").GlobalOptions)
            .ShouldBe(new DialectSetting(SqlDialect.Ansi, "orcl"));
    }

    [Fact]
    public void ReadMetadata_FileWithAndWithoutTheMetadata_ReadsItFromTheOptionsOfTheFile()
    {
        var file = new InMemoryAdditionalText(Path, "SELECT 1;");
        var other = new InMemoryAdditionalText("/app/Repo/Orders.sql", "SELECT 2;");
        var options = new TestOptionsProvider(null, "oracle", new Dictionary<string, string> { [Path] = "sqlite" });

        DialectSetting.ReadMetadata(options.GetOptions(file)).ShouldBe(new DialectSetting(SqlDialect.Sqlite, null));
        DialectSetting.ReadMetadata(options.GetOptions(other)).ShouldBe(new DialectSetting(null, null));
    }

    // The property is not in a file's options and the metadata is not in the global ones: each is read from its own.
    [Fact]
    public void Read_PropertyAndMetadata_AreNotMistakenForEachOther()
    {
        var file = new InMemoryAdditionalText(Path, "SELECT 1;");
        var options = new TestOptionsProvider(null, "oracle", new Dictionary<string, string> { [Path] = "sqlite" });

        DialectSetting.ReadMetadata(options.GlobalOptions).Dialect.ShouldBeNull();
        DialectSetting.ReadProperty(options.GetOptions(file)).Dialect.ShouldBeNull();
    }

    [Theory]
    // The file's metadata, when it is set.
    [InlineData("mysql", "oracle", nameof(SqlDialect.MySql), null)]
    [InlineData("ansi", "oracle", nameof(SqlDialect.Ansi), null)]
    [InlineData("mysql", null, nameof(SqlDialect.MySql), null)]
    // Or else the project's property.
    [InlineData(null, "oracle", nameof(SqlDialect.Oracle), null)]
    [InlineData("", "oracle", nameof(SqlDialect.Oracle), null)]
    // Or else ANSI.
    [InlineData(null, null, nameof(SqlDialect.Ansi), null)]
    // Metadata that is not a dialect is ANSI, not the project's dialect, and is carried to be reported.
    [InlineData("nope", "oracle", nameof(SqlDialect.Ansi), "nope")]
    // A property that is not a dialect is ANSI too.  It is reported from the project's setting, not from each file.
    [InlineData(null, "nope", nameof(SqlDialect.Ansi), null)]
    public void Resolve_MetadataThenPropertyThenAnsi(
        string? metadata,
        string? property,
        string dialect,
        string? invalid
    )
    {
        var file = new InMemoryAdditionalText(Path, "SELECT 1;");

        var resolved = Resolve(file, metadata, property);

        resolved.File.ShouldBeSameAs(file);
        resolved.NormalizedPath.ShouldBe("app/Repo/Users.sql");
        resolved.Dialect.ShouldBe(new SqlDialectChoice(Enum.Parse<SqlDialect>(dialect), SqlDialectOptions.None));
        resolved.InvalidDialect.ShouldBe(invalid);
        resolved.CommentsWanted.ShouldBeFalse();
    }

    // A value replaces the one it wins over whole: the option of the property is not added to the metadata.
    [Fact]
    public void Resolve_MetadataWithoutAnOption_DoesNotTakeTheOptionOfTheProperty()
    {
        var file = new InMemoryAdditionalText(Path, "SELECT 1;");

        var resolved = Resolve(file, "mysql", "mysql,ansi-quotes");

        resolved.Dialect.ShouldBe(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.None));
    }

    [Fact]
    public void Resolve_NoMetadata_TakesTheOptionOfTheProperty()
    {
        var file = new InMemoryAdditionalText(Path, "SELECT 1;");

        var resolved = Resolve(file, null, "mysql,ansi-quotes");

        resolved.Dialect.ShouldBe(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.AnsiQuotes));
    }

    [Fact]
    public void Resolve_SameFileAndSettings_GivesEqualValues()
    {
        var file = new InMemoryAdditionalText(Path, "SELECT 1;");

        Resolve(file, "mysql", null).ShouldBe(Resolve(file, " MySQL ", "oracle"));
    }

    [Theory]
    // The metadata's list decides when it gives one; else the property's.
    [InlineData(null, false, false, false)]
    [InlineData(null, true, false, true)]
    [InlineData("keep-comments", false, false, true)]
    [InlineData("sort-input", true, false, false)]
    [InlineData("default", true, false, false)]
    // A type that claims the file asks for comments: wanted, whatever MSBuild says.
    [InlineData("default", false, true, true)]
    [InlineData(null, false, true, true)]
    public void Resolve_CommentsWanted_IsWhatAnyLevelOutsideTheFileCanAskFor(
        string? fileParameters,
        bool projectKeepsComments,
        bool claimedByATypeThatKeepsComments,
        bool expected
    ) =>
        Resolve(
            new InMemoryAdditionalText(Path, ""),
            null,
            null,
            fileParameters,
            projectKeepsComments,
            claimedByATypeThatKeepsComments ? ["app/Repo/Users.sql"] : []
        )
            .CommentsWanted.ShouldBe(expected);

    private static FileParseInput Resolve(
        InMemoryAdditionalText file,
        string? metadata,
        string? project,
        string? fileParameters = null,
        bool projectKeepsComments = false,
        params string[] commentPaths
    ) =>
        FileParseInput.Resolve(
            new FileMetadata(
                file,
                DialectSetting.Parse(metadata),
                fileParameters is null
                    ? null
                    : new FileSettings(
                        "app/Repo/Users.sql",
                        new SettingsLevel { Parameters = GeneratorParameterList.Parse(fileParameters, []) },
                        EquatableArray<InvalidSetting>.Empty
                    )
            ),
            DialectSetting.Parse(project),
            projectKeepsComments,
            TestModels.Array(commentPaths)
        );
}
