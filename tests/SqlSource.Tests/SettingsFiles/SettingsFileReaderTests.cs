using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Settings;
using SqlSource.Settings.ColumnTypes;
using SqlSource.Settings.Libraries;
using SqlSource.Settings.TypeOverrides;
using SqlSource.SettingsFiles;
using Xunit;

namespace SqlSource.Tests.SettingsFiles;

public sealed class SettingsFileReaderTests
{
    /// <summary>The example of the settings files design, "The settings file".</summary>
    internal const string DesignsExample = /*lang=json*/
        """
        {
            "$schema": "https://raw.githubusercontent.com/mbcrawfo/SqlSource/main/schemas/settings-v1.schema.json",
            "root": true,
            "dialect": "postgres",
            "generatorParameters": ["keep-comments", "async-method-suffix"],
            // Dapper reads these through UtcDateTimeOffsetHandler.
            "typeOverrides": {
                "timestamptz": "System.DateTimeOffset",
                "public.\"Email\"": "MyApp.Email",
            },
            // Every query that reads one of these columns gets the C# type, by its short name.
            "columnTypes": {
                "users.id": "UserId",
                "posts.created_by_user_id": "UserId",
            },
        }
        """;

    private static readonly SettingPlace Place = SettingPlace.OfSettingsFile("/app/sqlsource.json", 1);

    // Each key whose value is a string and sets a member of the level, with that member.
    private static readonly Dictionary<string, Func<SettingsLevel, object?>> StringSettings = new()
    {
        [SettingsFileKeys.Database] = static level => level.Database,
        [SettingsFileKeys.Output] = static level => level.Output,
        [SettingsFileKeys.InputModelSuffix] = static level => level.InputModelSuffix,
        [SettingsFileKeys.OutputModelSuffix] = static level => level.OutputModelSuffix,
        [SettingsFileKeys.ModelNamespace] = static level => level.ModelNamespace,
        [SettingsFileKeys.InputModelType] = static level => level.InputModelType,
        [SettingsFileKeys.OutputModelType] = static level => level.OutputModelType,
        [SettingsFileKeys.CollectionType] = static level => level.CollectionType,
        [SettingsFileKeys.NestedClass] = static level => level.NestedClass,
    };

    [Fact]
    public void Read_DesignsExample_SetsEverySettingWithoutErrors()
    {
        var file = SettingsFileReader.Read(DesignsExample, Place);

        file.Errors.ShouldBeEmpty();
        file.Root.ShouldBe(true);
        file.Dialect.ShouldBe(new SqlDialectChoice(SqlDialect.PostgreSql, SqlDialectOptions.None));
        file.Level.Parameters.ShouldBe(GeneratorParameters.KeepComments | GeneratorParameters.AsyncMethodSuffix);
        file.Level.TypeOverrides.ShouldNotBeNull()
            .ShouldBe([
                new TypeOverride("public.\"Email\"", "global::MyApp.Email", Place),
                new TypeOverride("timestamptz", "global::System.DateTimeOffset", Place),
            ]);
        file.Level.SqlServerTypeOverrides.ShouldNotBeNull()
            .ShouldBe([new TypeOverride("public.\"Email\"", "global::MyApp.Email", Place)]);
        file.Level.ColumnTypes.ShouldNotBeNull()
            .ShouldBe([
                new ColumnType("posts.created_by_user_id", "UserId", Place),
                new ColumnType("users.id", "UserId", Place),
            ]);
    }

    [Fact]
    public void Read_EveryStringSetting_IsSetFromItsKey()
    {
        const string Text = /*lang=json,strict*/
            """
            {
                "dialect": "mysql, ansi_quotes",
                "database": "orders",
                "output": "Code-Gen",
                "inputModelSuffix": "Args",
                "outputModelSuffix": "Row",
                "modelNamespace": "App.Models",
                "inputModelType": "sealed record",
                "outputModelType": "class",
                "collectionType": "ireadonlylist",
                "nestedClass": "Queries"
            }
            """;

        var file = SettingsFileReader.Read(Text, Place);

        file.Errors.ShouldBeEmpty();
        file.Dialect.ShouldBe(new SqlDialectChoice(SqlDialect.MySql, SqlDialectOptions.AnsiQuotes));
        file.Level.ShouldBe(
            new SettingsLevel
            {
                Database = "orders",
                Output = OutputKind.CodeGen,
                InputModelSuffix = "Args",
                OutputModelSuffix = "Row",
                ModelNamespace = "App.Models",
                InputModelType = ModelKind.SealedRecord,
                OutputModelType = ModelKind.Class,
                CollectionType = CollectionKind.IReadOnlyList,
                NestedClass = "Queries",
            }
        );
    }

    [Theory]
    [InlineData( /*lang=json,strict*/
        """{"root": false}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"output": "sql"}"""
    )]
    [InlineData("{}")]
    public void Read_RootFalseOrAbsent_IsFalse(string text)
    {
        var file = SettingsFileReader.Read(text, Place);

        file.Root.ShouldBe(false);
        file.Errors.ShouldBeEmpty();
    }

    // The span is the character or token that breaks the grammar, empty at the end of a text cut short.
    [Theory]
    [InlineData("", 0, 0)]
    [InlineData("{", 1, 0)]
    [InlineData("{} {}", 3, 1)]
    [InlineData("{\"a\" 1}", 5, 1)]
    [InlineData("{ /* open", 9, 0)]
    public void Read_TextThatIsNotJson_IsInvalidJsonAndGivesNothing(string text, int start, int length)
    {
        var file = SettingsFileReader.Read(text, Place);

        ShouldGiveNothing(
            file,
            new SettingsFileError(SettingsFileErrorKind.InvalidJson, new(start, length), null, null, null)
        );
    }

    // The object is the first level and the 64th bracket opens the 65th.
    [Fact]
    public void Read_TextNestedDeeperThanTheLimit_IsInvalidJsonAtTheBracketThatOpensLevel65()
    {
        var text = "{\"x\": " + new string('[', 65) + new string(']', 65) + "}";

        var file = SettingsFileReader.Read(text, Place);

        ShouldGiveNothing(
            file,
            new SettingsFileError(
                SettingsFileErrorKind.InvalidJson,
                new(text.IndexOf('[', StringComparison.Ordinal) + 63, 1),
                null,
                null,
                null
            )
        );
    }

    // The span is the whole value, without the white space and comments around it.
    [Theory]
    [InlineData("[]", 0, 2)]
    [InlineData("1", 0, 1)]
    [InlineData("\"a\"", 0, 3)]
    [InlineData("null", 0, 4)]
    [InlineData(" // a comment\n [1, 2,] ", 15, 7)]
    public void Read_TopLevelThatIsNotAnObject_IsNotAnObject(string text, int start, int length)
    {
        var file = SettingsFileReader.Read(text, Place);

        ShouldGiveNothing(
            file,
            new SettingsFileError(SettingsFileErrorKind.NotAnObject, new(start, length), null, null, null)
        );
    }

    // Keys are matched ordinally: "Dialect" is not dialect.
    [Fact]
    public void Read_KeyNotInTheTable_IsUnknown()
    {
        const string Text = /*lang=json,strict*/
            """{"Dialect": "postgres", "bogus": [1, {}]}""";

        var file = SettingsFileReader.Read(Text, Place);

        file.Dialect.ShouldBeNull();
        file.Root.ShouldBe(false);
        file.Errors.ShouldBe([
            new SettingsFileError(SettingsFileErrorKind.UnknownKey, Span(Text, "\"Dialect\""), null, "Dialect", null),
            new SettingsFileError(SettingsFileErrorKind.UnknownKey, Span(Text, "\"bogus\""), null, "bogus", null),
        ]);
    }

    [Fact]
    public void Read_KeyGivenTwice_IsDuplicateAndTheFirstApplies()
    {
        const string Text = /*lang=json,strict*/
            """{"output": "sql", "output": "models", "zzz": 1, "zzz": 2}""";

        var file = SettingsFileReader.Read(Text, Place);

        file.Level.Output.ShouldBe(OutputKind.Sql);
        file.Errors.ShouldBe([
            new SettingsFileError(
                SettingsFileErrorKind.DuplicateKey,
                Span(Text, "\"output\"", 1),
                null,
                "output",
                null
            ),
            new SettingsFileError(SettingsFileErrorKind.UnknownKey, Span(Text, "\"zzz\""), null, "zzz", null),
            new SettingsFileError(SettingsFileErrorKind.DuplicateKey, Span(Text, "\"zzz\"", 1), null, "zzz", null),
        ]);
    }

    [Theory]
    [InlineData("dialect", "1")]
    [InlineData("output", "null")]
    [InlineData("root", "\"yes\"")]
    [InlineData("$schema", "1")]
    [InlineData("generatorParameters", "\"keep-comments\"")]
    [InlineData("typeOverrides", "[]")]
    [InlineData("columnTypes", "\"a.b=X\"")]
    [InlineData("libraryTypes", "[]")]
    [InlineData(
        "nestedClass", /*lang=json,strict*/
        "{ \"a\": \"b\" }"
    )]
    [InlineData("database", "true")]
    public void Read_ValueOfTheWrongType_IsWrongTypeAndSetsNothing(string key, string value)
    {
        var text = $$"""{"{{key}}": {{value}}}""";

        var file = SettingsFileReader.Read(text, Place);

        file.Root.ShouldBe(false);
        file.Dialect.ShouldBeNull();
        file.Level.ShouldBeSameAs(SettingsLevel.None);
        file.Errors.ShouldBe([
            new SettingsFileError(SettingsFileErrorKind.WrongType, Span(text, value), key, null, null),
        ]);
    }

    [Fact]
    public void Read_ElementOrMemberOfTheWrongType_IsWrongTypeAndTheOthersApply()
    {
        const string Parameters = /*lang=json,strict*/
            """{"generatorParameters": ["keep-comments", 1]}""";
        const string Overrides = /*lang=json,strict*/
            """{"typeOverrides": {"int4": "long", "text": {}}}""";

        var parameters = SettingsFileReader.Read(Parameters, Place);
        var overrides = SettingsFileReader.Read(Overrides, Place);

        parameters.Level.Parameters.ShouldBe(GeneratorParameters.KeepComments);
        parameters.Errors.ShouldBe([
            new SettingsFileError(
                SettingsFileErrorKind.WrongType,
                Span(Parameters, "1"),
                "generatorParameters",
                null,
                null
            ),
        ]);
        overrides.Level.TypeOverrides.ShouldNotBeNull().ShouldBe([new TypeOverride("int4", "long", Place)]);
        overrides.Errors.ShouldBe([
            new SettingsFileError(SettingsFileErrorKind.WrongType, Span(Overrides, "{}"), "typeOverrides", null, null),
        ]);
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\" \\t\"")]
    public void Read_EmptyOrWhiteSpaceString_SetsNothingAndIsNoError(string blank)
    {
        var keys = new[] { SettingsFileKeys.Dialect }.Concat(StringSettings.Keys);
        var text = "{" + string.Join(", ", keys.Select(key => $"\"{key}\": {blank}")) + "}";

        var file = SettingsFileReader.Read(text, Place);

        file.Errors.ShouldBeEmpty();
        file.Dialect.ShouldBeNull();
        file.Level.ShouldBeSameAs(SettingsLevel.None);
    }

    [Theory]
    [InlineData("output", "bogus")]
    [InlineData("modelNamespace", "a..b")]
    [InlineData("nestedClass", "class")]
    [InlineData("database", " a b ")]
    [InlineData("dialect", "sqlserver2")]
    [InlineData("dialect", "ansi,ansi-quotes")]
    [InlineData("dialect", " pgsql ")]
    [InlineData("collectionType", " lists ")]
    public void Read_ValueThatIsNotValid_IsInvalidValueAndSetsNothing(string key, string value)
    {
        var text = $$"""{"{{key}}": "{{value}}"}""";

        var file = SettingsFileReader.Read(text, Place);

        file.Dialect.ShouldBeNull();
        file.Level.ShouldBeSameAs(SettingsLevel.None);
        file.Errors.ShouldBe([
            new SettingsFileError(
                SettingsFileErrorKind.InvalidValue,
                Span(text, $"\"{value}\""),
                key,
                value.Trim(),
                null
            ),
        ]);
    }

    [Fact]
    public void Read_GeneratorParameters_FollowTheElementRules()
    {
        const string Misspelt = /*lang=json,strict*/
            """{"generatorParameters": ["keep-coments", "sort-input"]}""";
        const string WithDefault = /*lang=json,strict*/
            """{"generatorParameters": ["default", "sort-input"]}""";

        var empty = SettingsFileReader.Read( /*lang=json,strict*/
            """{"generatorParameters": []}""",
            Place
        );
        var misspelt = SettingsFileReader.Read(Misspelt, Place);
        var withDefault = SettingsFileReader.Read(WithDefault, Place);

        empty.Errors.ShouldBeEmpty();
        empty.Level.Parameters.ShouldBe(GeneratorParameters.None);
        misspelt.Level.Parameters.ShouldBe(GeneratorParameters.SortInput);
        misspelt.Errors.ShouldBe([
            new SettingsFileError(
                SettingsFileErrorKind.InvalidValue,
                Span(Misspelt, "\"keep-coments\""),
                "generatorParameters",
                "keep-coments",
                null
            ),
        ]);
        withDefault.Level.Parameters.ShouldBeNull();
        withDefault.Errors.ShouldBe([
            new SettingsFileError(
                SettingsFileErrorKind.InvalidValue,
                Span(WithDefault, "\"default\""),
                "generatorParameters",
                "default",
                null
            ),
        ]);
    }

    // A wrong JSON type never sets a setting: an array whose every element is of the wrong type sets nothing, as an
    // object of wrong-type members does.  [] is still the empty list: Read_GeneratorParameters_FollowTheElementRules.
    [Theory]
    [InlineData("generatorParameters", "[1]")]
    [InlineData("generatorParameters", "[1, {}]")]
    [InlineData(
        "typeOverrides", /*lang=json,strict*/
        "{\"int4\": 1}"
    )]
    [InlineData(
        "columnTypes", /*lang=json,strict*/
        "{\"users.id\": [], \"posts.id\": null}"
    )]
    [InlineData(
        "libraryTypes", /*lang=json,strict*/
        "{\"nodaTime\": true}"
    )]
    public void Read_ListOfOnlyWrongTypes_SetsNothing(string key, string value)
    {
        var text = $$"""{"{{key}}": {{value}}}""";

        var file = SettingsFileReader.Read(text, Place);

        file.Level.ShouldBeSameAs(SettingsLevel.None);
        file.Errors.ShouldNotBeEmpty();
        file.Errors.ShouldAllBe(error => error.Kind == SettingsFileErrorKind.WrongType && error.Setting == key);
    }

    [Fact]
    public void Read_TypeOverrideMembers_AreReportedByTheirSpansAndCarryThePlace()
    {
        const string Invalid = /*lang=json,strict*/
            """{"typeOverrides": {"int8": "long", " bogus ": " int "}}""";
        const string Repeated = /*lang=json,strict*/
            """{"typeOverrides": {"integer": "long", "int4": "int"}}""";

        var invalid = SettingsFileReader.Read(Invalid, Place);
        var repeated = SettingsFileReader.Read(Repeated, Place);

        invalid.Level.TypeOverrides.ShouldNotBeNull().ShouldBe([new TypeOverride("int8", "long", Place)]);
        invalid.Errors.ShouldBe([
            new SettingsFileError(
                SettingsFileErrorKind.InvalidValue,
                Span(Invalid, "\" bogus \": \" int \""),
                "typeOverrides",
                "bogus",
                "int"
            ),
        ]);
        repeated.Level.TypeOverrides.ShouldNotBeNull().ShouldBe([new TypeOverride("int4", "long", Place)]);
        repeated.Errors.ShouldBe([
            new SettingsFileError(
                SettingsFileErrorKind.RepeatedKey,
                Span(Repeated, "\"int4\""),
                "typeOverrides",
                "int4",
                "integer"
            ),
        ]);
    }

    [Fact]
    public void Read_ColumnTypeMembers_AreReportedByTheirSpansAndCarryThePlace()
    {
        const string Invalid = /*lang=json,strict*/
            """{"columnTypes": {"id": "X", "users.id": "UserId"}}""";
        const string Repeated = /*lang=json,strict*/
            """{"columnTypes": {"users.id": "UserId", "\"users\".id": "Y"}}""";

        var invalid = SettingsFileReader.Read(Invalid, Place);
        var repeated = SettingsFileReader.Read(Repeated, Place);

        invalid.Level.ColumnTypes.ShouldNotBeNull().ShouldBe([new ColumnType("users.id", "UserId", Place)]);
        invalid.Errors.ShouldBe([
            new SettingsFileError(
                SettingsFileErrorKind.InvalidValue,
                Span(Invalid, "\"id\": \"X\""),
                "columnTypes",
                "id",
                "X"
            ),
        ]);
        repeated.Level.ColumnTypes.ShouldNotBeNull().ShouldBe([new ColumnType("users.id", "UserId", Place)]);
        repeated.Errors.ShouldBe([
            new SettingsFileError(
                SettingsFileErrorKind.RepeatedKey,
                Span(Repeated, "\"\\\"users\\\".id\""),
                "columnTypes",
                "\"users\".id",
                "users.id"
            ),
        ]);
    }

    [Fact]
    public void Read_LibraryTypes_EntryCarriesTheMembersLineSpan()
    {
        const string Text = /*lang=json,strict*/
            "{\n  \"dialect\": \"postgres\",\n  \"libraryTypes\": { \"nodaTime\": \"on\" }\n}";

        var file = SettingsFileReader.Read(Text, Place);

        file.Errors.ShouldBeEmpty();
        var entry = file.Level.LibraryTypes.ShouldNotBeNull().ShouldHaveSingleItem();
        entry.Library.ShouldBe(SqlLibrary.NodaTime);
        entry.State.ShouldBe(LibraryState.On);
        entry.Place.ShouldBe(Place);
        entry.Span.ShouldBe(Span(Text, "\"nodaTime\": \"on\""));
        entry.LineSpan.Start.Line.ShouldBe(2);
        entry.LineSpan.ShouldBe(SourceText.From(Text).Lines.GetLinePositionSpan(entry.Span));
    }

    [Fact]
    public void Read_LibraryTypeMembers_AreReportedByTheirSpansAndCarryThePlace()
    {
        const string Invalid = /*lang=json,strict*/
            """{"libraryTypes": {"nodaTime": "maybe"}}""";
        const string Repeated = /*lang=json,strict*/
            """{"libraryTypes": {"nodaTime": "on", "noda-time": "off"}}""";

        var invalid = SettingsFileReader.Read(Invalid, Place);
        var repeated = SettingsFileReader.Read(Repeated, Place);

        invalid.Level.ShouldBeSameAs(SettingsLevel.None);
        invalid.Errors.ShouldBe([
            new SettingsFileError(
                SettingsFileErrorKind.InvalidValue,
                Span(Invalid, "\"nodaTime\": \"maybe\""),
                "libraryTypes",
                "nodaTime",
                "maybe"
            ),
        ]);
        repeated.Level.LibraryTypes.ShouldNotBeNull().Select(static entry => entry.State).ShouldBe([LibraryState.On]);
        repeated.Errors.ShouldBe([
            new SettingsFileError(
                SettingsFileErrorKind.RepeatedKey,
                Span(Repeated, "\"noda-time\""),
                "libraryTypes",
                "noda-time",
                "nodaTime"
            ),
        ]);
    }

    // SettingsLevelReader reads output before nestedClass, and the walk finds the unknown key before either.
    [Fact]
    public void Read_Errors_AreInTheOrderOfTheirPositions()
    {
        const string Text = /*lang=json,strict*/
            """{"nestedClass": "class", "output": "bogus", "bogus": 1}""";

        var file = SettingsFileReader.Read(Text, Place);

        file.Errors.Select(static error => error.Kind)
            .ShouldBe([
                SettingsFileErrorKind.InvalidValue,
                SettingsFileErrorKind.InvalidValue,
                SettingsFileErrorKind.UnknownKey,
            ]);
        file.Errors.Select(static error => error.Span.Start).ShouldBeInOrder();
        file.Errors[0].Setting.ShouldBe(SettingsFileKeys.NestedClass);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData( /*lang=json,strict*/
        """{"$schema": "x", "root": true}"""
    )]
    [InlineData( /*lang=json,strict*/
        """{"dialect": "postgres"}"""
    )]
    public void Read_FileThatSetsNothing_GivesTheSharedEmptyLevel(string text)
    {
        var file = SettingsFileReader.Read(text, Place);

        file.Level.ShouldBeSameAs(SettingsLevel.None);
        file.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Read_TwoReadsOfOneText_AreEqual()
    {
        const string Text = /*lang=json,strict*/
            """{"output": "bogus", "typeOverrides": {"int4": "long"}, "x": 1}""";

        var first = SettingsFileReader.Read(Text, Place);
        var second = SettingsFileReader.Read(Text, Place);

        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
        SettingsFileReader.Read(DesignsExample, Place).ShouldBe(SettingsFileReader.Read(DesignsExample, Place));
    }

    [Fact]
    public void IsAboutTheFile_EachKind_IsFalseOnlyForWhatEachSideReportsForItsOwnSettings()
    {
        foreach (var kind in Enum.GetValues<SettingsFileErrorKind>())
        {
            new SettingsFileError(kind, default, null, null, null).IsAboutTheFile.ShouldBe(
                kind is not (SettingsFileErrorKind.InvalidValue or SettingsFileErrorKind.RepeatedKey),
                kind.ToString()
            );
        }
    }

    private static void ShouldGiveNothing(SettingsFile file, SettingsFileError error)
    {
        file.Root.ShouldBeNull();
        file.Dialect.ShouldBeNull();
        file.Level.ShouldBeSameAs(SettingsLevel.None);
        file.Errors.ShouldBe([error]);
    }

    // The span of the occurrence of part at the index given, counting from zero.
    private static TextSpan Span(string text, string part, int occurrence = 0)
    {
        var start = -1;
        for (var count = 0; count <= occurrence; count++)
        {
            start = text.IndexOf(part, start + 1, StringComparison.Ordinal);
        }

        start.ShouldBeGreaterThanOrEqualTo(0, part);
        return new TextSpan(start, part.Length);
    }
}
