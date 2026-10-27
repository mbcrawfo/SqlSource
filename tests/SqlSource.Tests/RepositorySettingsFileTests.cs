using System.IO;
using Shouldly;
using SqlSource.Settings;
using SqlSource.SettingsFiles;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.Tests;

// The settings file at the root of the repository, which every project of it reaches last.
public sealed class RepositorySettingsFileTests
{
    // It sets nothing, so that a setting added there is seen: it would apply to every project that has none of its own.
    [Fact]
    public void RootSettingsFile_OfTheRepository_StopsEveryChainAndSetsNothing()
    {
        var path = Path.Combine(RepositoryRoot.Folder, SettingsFile.FileName);

        var file = SettingsFileReader.Read(File.ReadAllText(path), SettingPlace.OfSettingsFile(path, 0));

        file.Root.ShouldBe(true);
        file.Dialect.ShouldBeNull();
        file.Level.ShouldBe(SettingsLevel.None);
        file.Errors.ShouldBeEmpty();
    }
}
