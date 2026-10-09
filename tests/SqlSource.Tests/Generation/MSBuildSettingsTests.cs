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
