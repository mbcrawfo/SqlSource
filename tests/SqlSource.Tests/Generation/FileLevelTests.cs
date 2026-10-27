using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Chains;
using SqlSource.Settings;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generation;

// A file's level in stage two: the chain of its folder.
public sealed class FileLevelTests
{
    private static readonly SettingsLevel Chain = new() { InputModelSuffix = "In", OutputModelSuffix = "Row" };

    [Fact]
    public void Resolve_NoChain_IsNull() =>
        FileLevel.Resolve(GeneratorHarness.UsersNormalizedPath, SettingsChain.None).ShouldBeNull();

    // The chain's level itself, not a copy, so that the files of a folder share it.
    [Fact]
    public void Resolve_Chain_IsTheChainsLevel()
    {
        var level = FileLevel.Resolve(GeneratorHarness.UsersNormalizedPath, new SettingsChain(Chain, null));

        level.ShouldNotBeNull().NormalizedPath.ShouldBe(GeneratorHarness.UsersNormalizedPath);
        level.Level.ShouldBeSameAs(Chain);
    }

    [Fact]
    public void Resolve_PathThatDoesNotNormalize_IsNull() =>
        FileLevel.Resolve(null, new SettingsChain(Chain, null)).ShouldBeNull();
}
