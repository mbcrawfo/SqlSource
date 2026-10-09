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

    [Fact]
    public void ProjectSettings_ReadTwice_AreEqual() =>
        ProjectSettings
            .Read(new TestOptionsProvider("keep-comments nope").GlobalOptions)
            .ShouldBe(ProjectSettings.Read(new TestOptionsProvider("keep-comments nope").GlobalOptions));
}
