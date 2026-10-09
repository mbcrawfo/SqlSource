using System.Collections.Generic;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Settings;
using SqlSource.Tests.Generator;
using Xunit;

namespace SqlSource.Tests.Generation;

public class MSBuildSettingsTests
{
    private const string Path = "/app/Repo/Users.sql";

    [Fact]
    public void ReadProject_NothingSet_IsTheSharedEmptyLevel()
    {
        var settings = ProjectSettings.Read(new TestOptionsProvider(null).GlobalOptions);

        settings.Level.ShouldBeSameAs(SettingsLevel.None);
        settings.Invalid.ShouldBeEmpty();
    }

    [Fact]
    public void ReadProject_GeneratorParameters_AreReadWithTheirInvalidWords()
    {
        var settings = ProjectSettings.Read(new TestOptionsProvider(" keep-comments nope ").GlobalOptions);

        settings.Level.Parameters.ShouldBe(GeneratorParameters.KeepComments);
        settings.Invalid.ShouldBe([new InvalidSetting("SqlSourceGeneratorParameters", "nope")]);
    }

    [Fact]
    public void ReadProject_Output_IsReadAsAChoice()
    {
        var settings = ProjectSettings.Read(
            new TestOptionsProvider(
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

        var level = ProjectSettings.Read(new TestOptionsProvider(null, properties: properties).GlobalOptions).Level;

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
            .Read(Path, new TestOptionsProvider(null).GetOptions(new InMemoryAdditionalText(Path, "")))
            .ShouldBeNull();

    [Fact]
    public void ReadFile_Metadata_IsReadForThatFile()
    {
        var options = new TestOptionsProvider(
            null,
            fileMetadata: new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                [Path] = new Dictionary<string, string> { ["SqlSourceGeneratorParameters"] = "default" },
            }
        );

        var settings = FileSettings.Read(
            "app/Repo/Users.sql",
            options.GetOptions(new InMemoryAdditionalText(Path, ""))
        );

        settings.ShouldNotBeNull().Level.Parameters.ShouldBe(GeneratorParameters.None);
        settings.NormalizedPath.ShouldBe("app/Repo/Users.sql");
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
        new(null, properties: new Dictionary<string, string?> { [name] = value });

    private static QuerySettings Resolve(SettingsLevel level) =>
        QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None, SettingsLevel.None, level);

    private static Dictionary<string, string> Output(string value) => new() { ["SqlSourceOutput"] = value };

    private static FileSettings? ReadFileMetadata(Dictionary<string, string> metadata) =>
        FileSettings.Read(
            "app/Repo/Users.sql",
            new TestOptionsProvider(
                null,
                fileMetadata: new Dictionary<string, IReadOnlyDictionary<string, string>> { [Path] = metadata }
            ).GetOptions(new InMemoryAdditionalText(Path, ""))
        );

    [Fact]
    public void ProjectSettings_ReadTwice_AreEqual() =>
        ProjectSettings
            .Read(new TestOptionsProvider("keep-comments nope").GlobalOptions)
            .ShouldBe(ProjectSettings.Read(new TestOptionsProvider("keep-comments nope").GlobalOptions));
}
