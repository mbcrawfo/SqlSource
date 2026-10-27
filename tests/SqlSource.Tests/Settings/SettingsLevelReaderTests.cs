using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Settings;
using SqlSource.Settings.ColumnTypes;
using SqlSource.Settings.TypeOverrides;
using Xunit;

namespace SqlSource.Tests.Settings;

public sealed class SettingsLevelReaderTests
{
    // The settings that are a text, each with the member of the level it sets.
    private static readonly Dictionary<SettingId, Func<SettingsLevel, string?>> Texts = new()
    {
        [SettingId.Database] = static level => level.Database,
        [SettingId.InputModelSuffix] = static level => level.InputModelSuffix,
        [SettingId.OutputModelSuffix] = static level => level.OutputModelSuffix,
        [SettingId.ModelNamespace] = static level => level.ModelNamespace,
        [SettingId.NestedClass] = static level => level.NestedClass,
    };

    [Fact]
    public void Read_SourceThatHasNothing_IsTheSharedEmptyLevel()
    {
        var source = new FakeSource([]);

        SettingsLevelReader.Read(ref source).ShouldBeSameAs(SettingsLevel.None);
        source.Invalids.ShouldBeEmpty();
    }

    [Theory]
    // An internal enum cannot be the parameter of a public test method, so a row gives the setting's name.
    [InlineData("Database", " main ", "main")]
    [InlineData("InputModelSuffix", " Args ", "Args")]
    [InlineData("OutputModelSuffix", "Row", "Row")]
    [InlineData("ModelNamespace", " App.Models ", "App.Models")]
    [InlineData("NestedClass", " Queries ", "Queries")]
    public void Read_TextThatIsValid_IsSetWithoutTheWhiteSpaceAroundIt(string setting, string written, string expected)
    {
        var id = Enum.Parse<SettingId>(setting);
        var source = new FakeSource(new Dictionary<SettingId, string> { [id] = written });

        var level = SettingsLevelReader.Read(ref source);

        TextOf(level, id).ShouldBe(expected);
        source.Invalids.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Database", " not a name ", "not a name")]
    [InlineData("InputModelSuffix", "a-b", "a-b")]
    [InlineData("OutputModelSuffix", " a b ", "a b")]
    [InlineData("ModelNamespace", "App..Models", "App..Models")]
    [InlineData("NestedClass", " App.Sql ", "App.Sql")]
    [InlineData("NestedClass", "class", "class")]
    public void Read_TextThatIsNotValid_IsRecordedTrimmedAndNotSet(string setting, string written, string recorded)
    {
        var id = Enum.Parse<SettingId>(setting);
        var source = new FakeSource(new Dictionary<SettingId, string> { [id] = written });

        SettingsLevelReader.Read(ref source).ShouldBeSameAs(SettingsLevel.None);
        source.Invalids.ShouldBe([(id, recorded)]);
    }

    [Theory]
    [InlineData("Database")]
    [InlineData("InputModelSuffix")]
    [InlineData("OutputModelSuffix")]
    [InlineData("ModelNamespace")]
    [InlineData("Parameters")]
    [InlineData("TypeOverrides")]
    [InlineData("ColumnTypes")]
    [InlineData("NestedClass")]
    public void Read_TextThatIsWhiteSpace_IsNotSetAndNotRecorded(string setting)
    {
        var source = new FakeSource(new Dictionary<SettingId, string> { [Enum.Parse<SettingId>(setting)] = " \t " });

        SettingsLevelReader.Read(ref source).ShouldBeSameAs(SettingsLevel.None);
        source.Invalids.ShouldBeEmpty();
    }

    [Fact]
    public void Read_Parameters_AreParsedAndEachWordThatIsNoneIsRecorded()
    {
        var source = new FakeSource(
            new Dictionary<SettingId, string> { [SettingId.Parameters] = " keep-comments nope sort-input other " }
        );

        SettingsLevelReader
            .Read(ref source)
            .Parameters.ShouldBe(GeneratorParameters.KeepComments | GeneratorParameters.SortInput);
        source.Invalids.ShouldBe([(SettingId.Parameters, "nope"), (SettingId.Parameters, "other")]);
    }

    [Fact]
    public void Read_TypeOverrides_AreReadSortedAtThePlaceOfTheSource()
    {
        var source = new FakeSource(
            new Dictionary<SettingId, string> { [SettingId.TypeOverrides] = " timestamptz=default\n int4=long " },
            SettingPlace.Metadata
        );

        SettingsLevelReader
            .Read(ref source)
            .TypeOverrides.ShouldNotBeNull()
            .ShouldBe([
                new TypeOverride("int4", "long", SettingPlace.Metadata),
                new TypeOverride("timestamptz", null, SettingPlace.Metadata),
            ]);
        source.Invalids.ShouldBeEmpty();
    }

    // Each entry that is not valid is recorded as written, and the others apply.
    [Fact]
    public void Read_TypeOverrideThatIsNotValid_IsRecordedAndTheOthersApply()
    {
        var source = new FakeSource(
            new Dictionary<SettingId, string> { [SettingId.TypeOverrides] = "int4=long timestampz=int int8=int?" }
        );

        SettingsLevelReader
            .Read(ref source)
            .TypeOverrides.ShouldNotBeNull()
            .ShouldBe([new TypeOverride("int4", "long", SettingPlace.Property)]);
        source.Invalids.ShouldBe([(SettingId.TypeOverrides, "timestampz=int"), (SettingId.TypeOverrides, "int8=int?")]);
    }

    // An MSBuild value serves both engines: each entry is in the list of each engine that reads its key.
    [Fact]
    public void Read_TypeOverridesOfEachEngine_AreInTheListOfEach()
    {
        var source = new FakeSource(
            new Dictionary<SettingId, string> { [SettingId.TypeOverrides] = "int=long nvarchar=string" }
        );

        var level = SettingsLevelReader.Read(ref source);

        level.TypeOverrides.ShouldNotBeNull().ShouldBe([new TypeOverride("int4", "long", SettingPlace.Property)]);
        level
            .SqlServerTypeOverrides.ShouldNotBeNull()
            .ShouldBe([
                new TypeOverride("int", "long", SettingPlace.Property),
                new TypeOverride("nvarchar", "string", SettingPlace.Property),
            ]);
        source.Invalids.ShouldBeEmpty();
    }

    [Fact]
    public void Read_TypeOverridesOfOneEngine_LeaveTheOthersListUnset()
    {
        var source = new FakeSource(new Dictionary<SettingId, string> { [SettingId.TypeOverrides] = "int4=long" });

        var level = SettingsLevelReader.Read(ref source);

        level.TypeOverrides.ShouldNotBeNull().ShouldBe([new TypeOverride("int4", "long", SettingPlace.Property)]);
        level.SqlServerTypeOverrides.ShouldBeNull();
    }

    [Fact]
    public void Read_TypeOverridesThatAreNoneValid_AreNotSet()
    {
        var source = new FakeSource(new Dictionary<SettingId, string> { [SettingId.TypeOverrides] = "timestampz=int" });

        SettingsLevelReader.Read(ref source).ShouldBeSameAs(SettingsLevel.None);
        source.Invalids.ShouldBe([(SettingId.TypeOverrides, "timestampz=int")]);
    }

    [Fact]
    public void Read_ColumnTypes_AreReadSortedAtThePlaceOfTheSource()
    {
        var source = new FakeSource(
            new Dictionary<SettingId, string> { [SettingId.ColumnTypes] = " users.id=UserId\n posts.id=default " },
            SettingPlace.Metadata
        );

        SettingsLevelReader
            .Read(ref source)
            .ColumnTypes.ShouldNotBeNull()
            .ShouldBe([
                new ColumnType("posts.id", null, SettingPlace.Metadata),
                new ColumnType("users.id", "UserId", SettingPlace.Metadata),
            ]);
        source.Invalids.ShouldBeEmpty();
    }

    // Each entry that is not valid is recorded, as written, and the others apply.
    [Fact]
    public void Read_ColumnTypeThatIsNotValid_IsRecordedAndTheOthersApply()
    {
        var source = new FakeSource(
            new Dictionary<SettingId, string> { [SettingId.ColumnTypes] = "users.id=UserId id=UserId posts.id=int?" }
        );

        SettingsLevelReader
            .Read(ref source)
            .ColumnTypes.ShouldNotBeNull()
            .ShouldBe([new ColumnType("users.id", "UserId", SettingPlace.Property)]);
        source.Invalids.ShouldBe([(SettingId.ColumnTypes, "id=UserId"), (SettingId.ColumnTypes, "posts.id=int?")]);
    }

    [Fact]
    public void Read_ColumnTypesThatAreNoneValid_AreNotSet()
    {
        var source = new FakeSource(new Dictionary<SettingId, string> { [SettingId.ColumnTypes] = "id=UserId" });

        SettingsLevelReader.Read(ref source).ShouldBeSameAs(SettingsLevel.None);
        source.Invalids.ShouldBe([(SettingId.ColumnTypes, "id=UserId")]);
    }

    [Fact]
    public void Read_Choices_AreWhatTheSourceDecodes()
    {
        var source = new FakeSource(
            new Dictionary<SettingId, string>
            {
                [SettingId.Output] = "Models",
                [SettingId.InputModelType] = "Record",
                [SettingId.OutputModelType] = "SealedClass",
                [SettingId.CollectionType] = "ImmutableArray",
            }
        );

        SettingsLevelReader
            .Read(ref source)
            .ShouldBe(
                new SettingsLevel
                {
                    Output = OutputKind.Models,
                    InputModelType = ModelKind.Record,
                    OutputModelType = ModelKind.SealedClass,
                    CollectionType = CollectionKind.ImmutableArray,
                }
            );
    }

    [Fact]
    public void Read_ParametersAsElements_WinOverTheText()
    {
        var source = new FakeSource(
            new Dictionary<SettingId, string> { [SettingId.Parameters] = "keep-comments" },
            elements: new Dictionary<SettingId, string[]> { [SettingId.Parameters] = [" sort-input "] }
        );

        SettingsLevelReader.Read(ref source).Parameters.ShouldBe(GeneratorParameters.SortInput);
        source.Asked.ShouldNotContain("Text(Parameters)");
    }

    // An array with no word is the empty list, which is set, as default is.
    [Fact]
    public void Read_ParametersAsNoElement_AreTheEmptyList()
    {
        var source = new FakeSource([], elements: new Dictionary<SettingId, string[]> { [SettingId.Parameters] = [] });

        SettingsLevelReader.Read(ref source).Parameters.ShouldBe(GeneratorParameters.None);
        source.InvalidParts.ShouldBeEmpty();
    }

    [Fact]
    public void Read_ParametersAsElementsWithAnUnknownWord_NameItsIndexAndTheOthersApply()
    {
        var source = new FakeSource(
            [],
            elements: new Dictionary<SettingId, string[]> { [SettingId.Parameters] = ["keep-comments", "bogus"] }
        );

        SettingsLevelReader.Read(ref source).Parameters.ShouldBe(GeneratorParameters.KeepComments);
        source.InvalidParts.ShouldBe([(SettingId.Parameters, 1)]);
        source.Invalids.ShouldBeEmpty();
    }

    [Fact]
    public void Read_ParametersAsElementsOfUnknownWordsAlone_AreNotSet()
    {
        var source = new FakeSource(
            [],
            elements: new Dictionary<SettingId, string[]> { [SettingId.Parameters] = ["bogus"] }
        );

        SettingsLevelReader.Read(ref source).ShouldBeSameAs(SettingsLevel.None);
        source.InvalidParts.ShouldBe([(SettingId.Parameters, 0)]);
    }

    // The members win over the text.  integer is PostgreSQL's int4, which the first member put, so it goes in neither
    // list, SQL Server's int included.
    [Fact]
    public void Read_TypeOverridesAsMembers_AreSetAndEachProblemIsNamedByItsIndex()
    {
        var source = new FakeSource(
            new Dictionary<SettingId, string> { [SettingId.TypeOverrides] = "int8=long" },
            SettingPlace.Metadata,
            members: new Dictionary<SettingId, KeyValuePair<string, string>[]>
            {
                [SettingId.TypeOverrides] =
                [
                    new("int4", "long"),
                    new("bogus", "int"),
                    new("integer", "MyApp.X"),
                    new("nvarchar", "string"),
                ],
            }
        );

        var level = SettingsLevelReader.Read(ref source);

        level.TypeOverrides.ShouldNotBeNull().ShouldBe([new TypeOverride("int4", "long", SettingPlace.Metadata)]);
        level
            .SqlServerTypeOverrides.ShouldNotBeNull()
            .ShouldBe([new TypeOverride("nvarchar", "string", SettingPlace.Metadata)]);
        source.InvalidParts.ShouldBe([(SettingId.TypeOverrides, 1)]);
        source.RepeatedKeys.ShouldBe([(SettingId.TypeOverrides, 2, 0)]);
        source.Asked.ShouldNotContain("Text(TypeOverrides)");
    }

    [Fact]
    public void Read_ColumnTypesAsMembers_AreSetAndEachProblemIsNamedByItsIndex()
    {
        var source = new FakeSource(
            new Dictionary<SettingId, string> { [SettingId.ColumnTypes] = "posts.id=PostId" },
            SettingPlace.Metadata,
            members: new Dictionary<SettingId, KeyValuePair<string, string>[]>
            {
                [SettingId.ColumnTypes] = [new("id", "X"), new("users.id", "UserId"), new("\"users\".id", "Y")],
            }
        );

        SettingsLevelReader
            .Read(ref source)
            .ColumnTypes.ShouldNotBeNull()
            .ShouldBe([new ColumnType("users.id", "UserId", SettingPlace.Metadata)]);
        source.InvalidParts.ShouldBe([(SettingId.ColumnTypes, 0)]);
        source.RepeatedKeys.ShouldBe([(SettingId.ColumnTypes, 2, 1)]);
        source.Asked.ShouldNotContain("Text(ColumnTypes)");
    }

    // An object with no valid member sets nothing, as a text with no valid entry does.
    [Theory]
    [InlineData("TypeOverrides")]
    [InlineData("ColumnTypes")]
    public void Read_MembersThatAreNoneValid_AreNotSet(string setting)
    {
        var id = Enum.Parse<SettingId>(setting);
        var empty = new FakeSource(
            [],
            members: new Dictionary<SettingId, KeyValuePair<string, string>[]> { [id] = [] }
        );
        var invalid = new FakeSource(
            [],
            members: new Dictionary<SettingId, KeyValuePair<string, string>[]> { [id] = [new("id", "")] }
        );

        SettingsLevelReader.Read(ref empty).ShouldBeSameAs(SettingsLevel.None);
        empty.InvalidParts.ShouldBeEmpty();
        SettingsLevelReader.Read(ref invalid).ShouldBeSameAs(SettingsLevel.None);
        invalid.InvalidParts.ShouldBe([(id, 0)]);
    }

    // A list's array or object form is asked for before its text.
    [Fact]
    public void Read_EverySetting_IsAskedForInTheOrderOfTheEnum()
    {
        var source = new FakeSource([]);

        _ = SettingsLevelReader.Read(ref source);

        source.Asked.ShouldBe([
            "Elements(Parameters)",
            "Text(Parameters)",
            "Choice(Output)",
            "Text(Database)",
            "Text(InputModelSuffix)",
            "Text(OutputModelSuffix)",
            "Text(ModelNamespace)",
            "Choice(InputModelType)",
            "Choice(OutputModelType)",
            "Choice(CollectionType)",
            "Members(TypeOverrides)",
            "Text(TypeOverrides)",
            "Members(ColumnTypes)",
            "Text(ColumnTypes)",
            "Text(NestedClass)",
        ]);
    }

    private static string? TextOf(SettingsLevel level, SettingId setting) => Texts[setting](level);

    // A source over dictionaries: the texts, and the elements and members of the settings that have them.  A choice is
    // written as the member's name.
    private struct FakeSource(
        Dictionary<SettingId, string> texts,
        SettingPlace? place = null,
        Dictionary<SettingId, string[]>? elements = null,
        Dictionary<SettingId, KeyValuePair<string, string>[]>? members = null
    ) : ISettingSource
    {
        public readonly SettingPlace Place => place ?? SettingPlace.Property;

        public List<(SettingId Setting, string Value)> Invalids { get; } = [];

        public List<(SettingId Setting, int Index)> InvalidParts { get; } = [];

        public List<(SettingId Setting, int Index, int First)> RepeatedKeys { get; } = [];

        // Each call, as the method and the setting.
        public List<string> Asked { get; } = [];

        public readonly string? Text(SettingId setting)
        {
            Asked.Add($"Text({setting})");
            return texts.TryGetValue(setting, out var text) ? text : null;
        }

        public readonly IReadOnlyList<string>? Elements(SettingId setting)
        {
            Asked.Add($"Elements({setting})");
            return elements is not null && elements.TryGetValue(setting, out var written) ? written : null;
        }

        public readonly IReadOnlyList<KeyValuePair<string, string>>? Members(SettingId setting)
        {
            Asked.Add($"Members({setting})");
            return members is not null && members.TryGetValue(setting, out var written) ? written : null;
        }

        public readonly T? Choice<T>(SettingId setting)
            where T : struct, Enum
        {
            Asked.Add($"Choice({setting})");
            return texts.TryGetValue(setting, out var text) && Enum.TryParse<T>(text, out var choice) ? choice : null;
        }

        public readonly void Invalid(SettingId setting, string value) => Invalids.Add((setting, value));

        public readonly void InvalidPart(SettingId setting, int index) => InvalidParts.Add((setting, index));

        public readonly void RepeatedKey(SettingId setting, int index, int first) =>
            RepeatedKeys.Add((setting, index, first));
    }
}
