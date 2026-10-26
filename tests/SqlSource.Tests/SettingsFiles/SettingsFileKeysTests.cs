using System;
using System.Linq;
using Shouldly;
using SqlSource.Settings;
using SqlSource.SettingsFiles;
using Xunit;

namespace SqlSource.Tests.SettingsFiles;

public sealed class SettingsFileKeysTests
{
    [Fact]
    public void KeyOf_EverySetting_IsAKeyThatGivesItBack()
    {
        foreach (var setting in Enum.GetValues<SettingId>())
        {
            var key = SettingsFileKeys.KeyOf(setting);

            SettingsFileKeys.All.ShouldContain(key, setting.ToString());
            SettingsFileKeys.TryGetSetting(key, out var found).ShouldBeTrue(key);
            found.ShouldBe(setting, key);
        }
    }

    [Fact]
    public void All_EveryKeyButTheThreeTheReaderReadsItself_GivesASetting()
    {
        string[] own = [SettingsFileKeys.Schema, SettingsFileKeys.Root, SettingsFileKeys.Dialect];

        foreach (var key in SettingsFileKeys.All)
        {
            SettingsFileKeys.TryGetSetting(key, out _).ShouldBe(!own.Contains(key), key);
        }

        SettingsFileKeys.All.Length.ShouldBe(Enum.GetValues<SettingId>().Length + own.Length);
        SettingsFileKeys.All.Distinct(StringComparer.Ordinal).Count().ShouldBe(SettingsFileKeys.All.Length);
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("Dialect")]
    [InlineData("")]
    public void TypeOf_KeyNotInAll_Throws(string key)
    {
        SettingsFileKeys.TryGetSetting(key, out _).ShouldBeFalse();
        _ = Should.Throw<ArgumentOutOfRangeException>(() => SettingsFileKeys.TypeOf(key));
    }
}
