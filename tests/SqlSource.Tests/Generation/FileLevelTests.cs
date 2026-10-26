using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Chains;
using SqlSource.Generation.MSBuild;
using SqlSource.Generation.Paths;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generation;

// A file's level in stage two: the chain of its folder over the metadata of its item.
public sealed class FileLevelTests
{
    private static readonly SettingsLevel Chain = new() { InputModelSuffix = "In", OutputModelSuffix = "Row" };

    [Fact]
    public void Resolve_NoChainAndNoMetadata_IsNull() =>
        FileLevel.Resolve(Metadata(GeneratorHarness.UsersPath, null), SettingsChain.None).ShouldBeNull();

    // The chain's level itself, not a copy, so that the files of a folder share it.
    [Fact]
    public void Resolve_ChainWithoutMetadata_IsTheChainsLevel()
    {
        var level = FileLevel.Resolve(Metadata(GeneratorHarness.UsersPath, null), new SettingsChain(Chain, null));

        level.ShouldNotBeNull().NormalizedPath.ShouldBe(GeneratorHarness.UsersNormalizedPath);
        level.Level.ShouldBeSameAs(Chain);
    }

    [Fact]
    public void Resolve_ChainOverMetadata_TakesEachSettingFromTheChainBeforeTheMetadata()
    {
        var metadata = new SettingsLevel { InputModelSuffix = "Args", ModelNamespace = "App.Models" };

        var level = FileLevel.Resolve(Metadata(GeneratorHarness.UsersPath, metadata), new SettingsChain(Chain, null));

        level
            .ShouldNotBeNull()
            .Level.ShouldBe(
                new SettingsLevel
                {
                    InputModelSuffix = "In",
                    OutputModelSuffix = "Row",
                    ModelNamespace = "App.Models",
                }
            );
    }

    [Fact]
    public void Resolve_MetadataWithoutAChain_IsTheMetadatasLevel()
    {
        var metadata = new SettingsLevel { InputModelSuffix = "Args" };

        var level = FileLevel.Resolve(Metadata(GeneratorHarness.UsersPath, metadata), SettingsChain.None);

        level.ShouldNotBeNull().Level.ShouldBeSameAs(metadata);
    }

    [Fact]
    public void Resolve_PathThatDoesNotNormalize_IsNull() =>
        FileLevel.Resolve(Metadata("../Users.sql", null), new SettingsChain(Chain, null)).ShouldBeNull();

    // The metadata of a file at the path, with these settings, or none.
    private static FileMetadata Metadata(string path, SettingsLevel? settings) =>
        new(
            new InMemoryAdditionalText(path, "SELECT 1;"),
            new DialectSetting(null, null),
            settings is null
                ? null
                : new FileSettings(SqlPath.Normalize(path)!, settings, EquatableArray<InvalidSetting>.Empty)
        );
}
