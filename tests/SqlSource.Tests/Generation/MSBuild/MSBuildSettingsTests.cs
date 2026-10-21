using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Generation.MSBuild;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generation.MSBuild;

public sealed class MSBuildSettingsTests
{
    // The compiler's contract, spelled out: a change to a constant must fail here, not pass by agreeing with itself.
    [Fact]
    public void Prefixes_AreWhereTheCompilerPutsAPropertyAndTheMetadataOfTheSettingsItem()
    {
        MSBuildSettings.PropertyPrefix.ShouldBe("build_property.");
        MSBuildSettings.MetadataPrefix.ShouldBe("build_metadata.SqlSourceSettingsFile.");
        DialectSetting.PropertyName.ShouldBe("build_property.SqlSourceDialect");
        DialectSetting.MetadataName.ShouldBe("build_metadata.SqlSourceSettingsFile.SqlSourceDialect");
    }

    // The names line up with SettingId: the keys are found by its number.
    [Fact]
    public void NameOf_EverySetting_IsItsMSBuildName()
    {
        foreach (var setting in Enum.GetValues<SettingId>())
        {
            MSBuildSettings
                .NameOf(setting)
                .ShouldBe(setting == SettingId.Parameters ? "SqlSourceGeneratorParameters" : "SqlSource" + setting);
        }
    }

    // The compiler is not shown SqlSourceDatabase, and the generator does not judge it when a project shows it
    // itself: the keys of the compiler have no database.
    [Theory]
    [InlineData("main")]
    [InlineData("not a name")]
    public void ReadProject_DatabaseUnderTheKeysOfTheCompiler_IsNotRead(string written)
    {
        var settings = ProjectSettings.Read(Provider("SqlSourceDatabase", written).GlobalOptions);

        settings.Level.ShouldBeSameAs(SettingsLevel.None);
        settings.Invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadFile_DatabaseUnderTheKeysOfTheCompiler_IsNotRead() =>
        ReadFileMetadata(new Dictionary<string, string> { ["SqlSourceDatabase"] = "not a name" }).ShouldBeNull();

    // The tool reads a manifest, which holds the database, under the keys that have it.
    [Theory]
    [InlineData(" main ", "main", null)]
    [InlineData("not a name", null, "not a name")]
    [InlineData("   ", null, null)]
    public void ReadProject_DatabaseUnderTheKeysOfAManifest_IsReadAsAName(
        string written,
        string? expected,
        string? invalid
    )
    {
        var settings = ProjectSettings.Read(
            Provider("SqlSourceDatabase", written).GlobalOptions,
            MSBuildSettings.ManifestProperty
        );

        settings.Level.Database.ShouldBe(expected);
        settings.Invalid.ShouldBe(invalid is null ? [] : [new InvalidSetting("SqlSourceDatabase", invalid)]);
    }

    [Fact]
    public void ReadFile_DatabaseUnderTheKeysOfAManifest_IsReadAsAName()
    {
        var settings = FileSettings.Read(
            GeneratorHarness.UsersPath,
            new TestOptionsProvider(
                null,
                null,
                fileMetadata: new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    [GeneratorHarness.UsersPath] = new Dictionary<string, string> { ["SqlSourceDatabase"] = "files" },
                }
            ).GetOptions(new InMemoryAdditionalText(GeneratorHarness.UsersPath, "")),
            MSBuildSettings.ManifestMetadata
        );

        settings.ShouldNotBeNull().Level.ShouldBe(new SettingsLevel { Database = "files" });
        settings.Invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadProject_TypeOverrides_AreReadAtTheProjectsPlaceWithTheirInvalidEntries()
    {
        var settings = ProjectSettings.Read(
            Provider("SqlSourceTypeOverrides", "timestamptz=System.DateTimeOffset timestampz=int").GlobalOptions
        );

        settings
            .Level.TypeOverrides.ShouldNotBeNull()
            .ShouldBe([new TypeOverride("timestamptz", "global::System.DateTimeOffset", SettingPlace.Property)]);
        settings.Invalid.ShouldBe([new InvalidSetting("SqlSourceTypeOverrides", "timestampz=int")]);
    }

    [Fact]
    public void ReadFile_TypeOverrides_AreReadAtTheMetadatasPlace()
    {
        var settings = ReadFileMetadata(
            new Dictionary<string, string> { ["SqlSourceTypeOverrides"] = "date=System.DateOnly int4=Guid" }
        );

        settings
            .ShouldNotBeNull()
            .Level.TypeOverrides.ShouldNotBeNull()
            .ShouldBe([new TypeOverride("date", "global::System.DateOnly", SettingPlace.Metadata)]);
        settings.Invalid.ShouldBe([new InvalidSetting("SqlSourceTypeOverrides", "int4=Guid")]);
    }

    // The tool does not read the type override, which changes nothing that is described: the keys of a manifest
    // have none for it.
    [Fact]
    public void ReadProject_TypeOverridesUnderTheKeysOfAManifest_AreNotRead()
    {
        var settings = ProjectSettings.Read(
            Provider("SqlSourceTypeOverrides", "int4=long timestampz=int").GlobalOptions,
            MSBuildSettings.ManifestProperty
        );

        settings.Level.ShouldBeSameAs(SettingsLevel.None);
        settings.Invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadFile_TypeOverridesUnderTheKeysOfAManifest_AreNotRead() =>
        FileSettings
            .Read(
                GeneratorHarness.UsersPath,
                new TestOptionsProvider(
                    null,
                    null,
                    fileMetadata: new Dictionary<string, IReadOnlyDictionary<string, string>>
                    {
                        [GeneratorHarness.UsersPath] = new Dictionary<string, string>
                        {
                            ["SqlSourceTypeOverrides"] = "timestampz=int",
                        },
                    }
                ).GetOptions(new InMemoryAdditionalText(GeneratorHarness.UsersPath, "")),
                MSBuildSettings.ManifestMetadata
            )
            .ShouldBeNull();

    [Fact]
    public void ReadProject_NestedClass_IsReadAsATypeName()
    {
        var settings = ProjectSettings.Read(Provider("SqlSourceNestedClass", "Queries").GlobalOptions);

        settings.Level.ShouldBe(new SettingsLevel { NestedClass = "Queries" });
        settings.Invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadProject_NestedClassThatIsNotATypeName_IsInvalidAndNotSet()
    {
        var settings = ProjectSettings.Read(Provider("SqlSourceNestedClass", "App.Sql").GlobalOptions);

        settings.Level.ShouldBeSameAs(SettingsLevel.None);
        settings.Invalid.ShouldBe([new InvalidSetting("SqlSourceNestedClass", "App.Sql")]);
    }

    // The nested class belongs to a type, whose queries can come from several files, so it is never metadata.
    [Fact]
    public void ReadFile_NestedClass_IsNotRead() =>
        ReadFileMetadata(new Dictionary<string, string> { ["SqlSourceNestedClass"] = "not a name" }).ShouldBeNull();

    // The tool does not read the nested class, which changes nothing that is described.
    [Fact]
    public void ReadProject_NestedClassUnderTheKeysOfAManifest_IsNotRead()
    {
        var settings = ProjectSettings.Read(
            Provider("SqlSourceNestedClass", "not a name").GlobalOptions,
            MSBuildSettings.ManifestProperty
        );

        settings.Level.ShouldBeSameAs(SettingsLevel.None);
        settings.Invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadProject_NothingSet_IsTheSharedEmptyLevel()
    {
        var settings = ProjectSettings.Read(new TestOptionsProvider(null, null).GlobalOptions);

        settings.Level.ShouldBeSameAs(SettingsLevel.None);
        settings.Invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadProject_GeneratorParameters_AreReadWithTheirInvalidWords()
    {
        var settings = ProjectSettings.Read(new TestOptionsProvider(null, " keep-comments nope ").GlobalOptions);

        settings.Level.Parameters.ShouldBe(GeneratorParameters.KeepComments);
        settings.Invalid.ShouldBe([new InvalidSetting("SqlSourceGeneratorParameters", "nope")]);
    }

    [Fact]
    public void ReadProject_Output_IsReadAsAChoice()
    {
        var settings = ProjectSettings.Read(
            new TestOptionsProvider(
                null,
                null,
                properties: new Dictionary<string, string?> { ["SqlSourceOutput"] = " Models " }
            ).GlobalOptions
        );

        settings.Level.Output.ShouldBe(OutputKind.Models);
        settings.Invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadProject_OutputThatIsNoChoice_IsInvalidAndNotSet()
    {
        var settings = ProjectSettings.Read(
            new TestOptionsProvider(
                null,
                null,
                properties: new Dictionary<string, string?> { ["SqlSourceOutput"] = "nope" }
            ).GlobalOptions
        );

        settings.Level.Output.ShouldBeNull();
        settings.Invalid.ShouldBe([new InvalidSetting("SqlSourceOutput", "nope")]);
    }

    [Fact]
    public void ReadFile_Output_IsReadAsAChoice()
    {
        var settings = ReadFileMetadata(Output("models"));

        settings.ShouldNotBeNull().Level.Output.ShouldBe(OutputKind.Models);
        settings.Invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadFile_OutputThatIsNoChoice_IsInvalidAndNotSet()
    {
        var settings = ReadFileMetadata(Output("nope"));

        settings.ShouldNotBeNull().Level.Output.ShouldBeNull();
        settings.Invalid.ShouldBe([new InvalidSetting("SqlSourceOutput", "nope")]);
    }

    [Theory]
    [MemberData(nameof(ModelSettings))]
    public void ReadProject_ModelSetting_IsReadOrIsInvalidAndNotSet(string name, string valid, string invalid)
    {
        var read = ProjectSettings.Read(Provider(name, " " + valid + " ").GlobalOptions);
        var rejected = ProjectSettings.Read(Provider(name, invalid).GlobalOptions);

        Resolve(read.Level).ShouldNotBe(Resolve(SettingsLevel.None));
        read.Invalid.ShouldBeEmpty();
        Resolve(rejected.Level).ShouldBe(Resolve(SettingsLevel.None));
        rejected.Level.ShouldBeSameAs(SettingsLevel.None);
        rejected.Invalid.ShouldBe([new InvalidSetting(name, invalid)]);
    }

    [Theory]
    [MemberData(nameof(ModelSettings))]
    public void ReadFile_ModelSetting_IsReadOrIsInvalidAndNotSet(string name, string valid, string invalid)
    {
        var read = ReadFileMetadata(new Dictionary<string, string> { [name] = valid });
        var rejected = ReadFileMetadata(new Dictionary<string, string> { [name] = invalid });

        Resolve(read.ShouldNotBeNull().Level).ShouldNotBe(Resolve(SettingsLevel.None));
        read.Invalid.ShouldBeEmpty();
        Resolve(rejected.ShouldNotBeNull().Level).ShouldBe(Resolve(SettingsLevel.None));
        rejected.Invalid.ShouldBe([new InvalidSetting(name, invalid)]);
    }

    [Fact]
    public void ReadProject_ModelSettings_AreReadIntoTheirMembers()
    {
        var properties = new Dictionary<string, string?>
        {
            ["SqlSourceInputModelSuffix"] = "Args",
            ["SqlSourceOutputModelSuffix"] = "Row",
            ["SqlSourceModelNamespace"] = "App.Models",
            ["SqlSourceInputModelType"] = "sealed class",
            ["SqlSourceOutputModelType"] = "Record",
            ["SqlSourceCollectionType"] = "immutable-array",
        };

        var level = ProjectSettings
            .Read(new TestOptionsProvider(null, null, properties: properties).GlobalOptions)
            .Level;

        level.ShouldBe(
            new SettingsLevel
            {
                InputModelSuffix = "Args",
                OutputModelSuffix = "Row",
                ModelNamespace = "App.Models",
                InputModelType = ModelKind.SealedClass,
                OutputModelType = ModelKind.Record,
                CollectionType = CollectionKind.ImmutableArray,
            }
        );
    }

    [Fact]
    public void ReadFile_NothingSet_IsNull() =>
        FileSettings
            .Read(
                GeneratorHarness.UsersPath,
                new TestOptionsProvider(null, null).GetOptions(
                    new InMemoryAdditionalText(GeneratorHarness.UsersPath, "")
                )
            )
            .ShouldBeNull();

    [Fact]
    public void ReadFile_Metadata_IsReadForThatFile()
    {
        var options = new TestOptionsProvider(
            null,
            null,
            fileMetadata: new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                [GeneratorHarness.UsersPath] = new Dictionary<string, string>
                {
                    ["SqlSourceGeneratorParameters"] = "default",
                },
            }
        );

        var settings = FileSettings.Read(
            GeneratorHarness.UsersNormalizedPath,
            options.GetOptions(new InMemoryAdditionalText(GeneratorHarness.UsersPath, ""))
        );

        settings.ShouldNotBeNull().Level.Parameters.ShouldBe(GeneratorParameters.None);
        settings.NormalizedPath.ShouldBe(GeneratorHarness.UsersNormalizedPath);
    }

    public static TheoryData<string, string, string> ModelSettings =>
        new()
        {
            { "SqlSourceInputModelSuffix", "Args", "A B" },
            { "SqlSourceOutputModelSuffix", "Row", "A.B" },
            { "SqlSourceModelNamespace", "App.Models", "App." },
            { "SqlSourceInputModelType", "sealed class", "struct" },
            { "SqlSourceOutputModelType", "Class", "sealed" },
            { "SqlSourceCollectionType", "IReadOnlyList", "HashSet" },
        };

    private static TestOptionsProvider Provider(string name, string value) =>
        new(null, null, properties: new Dictionary<string, string?> { [name] = value });

    private static QuerySettings Resolve(SettingsLevel level) =>
        QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None, SettingsLevel.None, level);

    private static Dictionary<string, string> Output(string value) => new() { ["SqlSourceOutput"] = value };

    private static FileSettings? ReadFileMetadata(Dictionary<string, string> metadata) =>
        FileSettings.Read(
            GeneratorHarness.UsersNormalizedPath,
            new TestOptionsProvider(
                null,
                null,
                fileMetadata: new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    [GeneratorHarness.UsersPath] = metadata,
                }
            ).GetOptions(new InMemoryAdditionalText(GeneratorHarness.UsersPath, ""))
        );

    [Fact]
    public void ProjectSettings_ReadTwice_AreEqual() =>
        ProjectSettings
            .Read(new TestOptionsProvider(null, "keep-comments nope").GlobalOptions)
            .ShouldBe(ProjectSettings.Read(new TestOptionsProvider(null, "keep-comments nope").GlobalOptions));
}
