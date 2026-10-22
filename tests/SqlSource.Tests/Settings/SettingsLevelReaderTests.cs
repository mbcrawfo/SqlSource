using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Settings;
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
            new Dictionary<SettingId, string> { [SettingId.TypeOverrides] = "int4=long timestampz=int int8=Guid" }
        );

        SettingsLevelReader
            .Read(ref source)
            .TypeOverrides.ShouldNotBeNull()
            .ShouldBe([new TypeOverride("int4", "long", SettingPlace.Property)]);
        source.Invalids.ShouldBe([(SettingId.TypeOverrides, "timestampz=int"), (SettingId.TypeOverrides, "int8=Guid")]);
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
    public void Read_EverySetting_IsAskedForInTheOrderOfTheEnum()
    {
        var source = new FakeSource([]);

        _ = SettingsLevelReader.Read(ref source);

        source.Asked.ShouldBe(Enum.GetValues<SettingId>());
    }

    private static string? TextOf(SettingsLevel level, SettingId setting) => Texts[setting](level);

    // A source over a dictionary.  A choice is written as the member's name.
    private struct FakeSource(Dictionary<SettingId, string> texts, SettingPlace place = SettingPlace.Property)
        : ISettingSource
    {
        public readonly SettingPlace Place => place;

        public List<(SettingId Setting, string Value)> Invalids { get; } = [];

        public List<SettingId> Asked { get; } = [];

        public readonly string? Text(SettingId setting)
        {
            Asked.Add(setting);
            return texts.TryGetValue(setting, out var text) ? text : null;
        }

        public readonly T? Choice<T>(SettingId setting)
            where T : struct, Enum
        {
            Asked.Add(setting);
            return texts.TryGetValue(setting, out var text) && Enum.TryParse<T>(text, out var choice) ? choice : null;
        }

        public readonly void Invalid(SettingId setting, string value) => Invalids.Add((setting, value));
    }
}
