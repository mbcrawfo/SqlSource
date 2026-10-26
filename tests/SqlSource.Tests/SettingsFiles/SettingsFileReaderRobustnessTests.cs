using System.Linq;
using Shouldly;
using SqlSource.Settings;
using SqlSource.SettingsFiles;
using Xunit;

namespace SqlSource.Tests.SettingsFiles;

// A settings file is written by hand, merged and cut short.  Nothing that happens to one may make the reader throw: an
// exception in the generator costs every type its generated code.
public sealed class SettingsFileReaderRobustnessTests
{
    private const SettingPlace Place = SettingPlace.Metadata;

    [Fact]
    public void Read_DesignsExampleCutShortAnywhere_ReturnsWithAnError()
    {
        var text = SettingsFileReaderTests.DesignsExample.Trim();

        for (var length = 0; length < text.Length; length++)
        {
            var file = SettingsFileReader.Read(text[..length], Place);

            file.Root.ShouldBeNull($"at length {length}");
            file.Errors.Select(static error => error.Kind)
                .ShouldContain(SettingsFileErrorKind.InvalidJson, $"at length {length}");
        }
    }

    [Fact]
    public void Read_DesignsExampleWithACharacterReplacedAnywhere_Returns()
    {
        const string Replacements = "{}[]\",:/*\\1a\0";
        var text = SettingsFileReaderTests.DesignsExample;

        for (var offset = 0; offset < text.Length; offset++)
        {
            foreach (var replacement in Replacements)
            {
                var file = SettingsFileReader.Read(text[..offset] + replacement + text[(offset + 1)..], Place);

                var unread = file.Errors.Any(static error =>
                    error.Kind is SettingsFileErrorKind.InvalidJson or SettingsFileErrorKind.NotAnObject
                );
                (file.Root is null).ShouldBe(unread, $"{replacement} at {offset}");
            }
        }
    }

    [Fact]
    public void Read_DeepNesting_IsInvalidJsonWithoutOverflow()
    {
        var text = "{\"x\": " + new string('[', 10_000) + "}";

        var file = SettingsFileReader.Read(text, Place);

        file.Root.ShouldBeNull();
        file.Errors.ShouldHaveSingleItem().Kind.ShouldBe(SettingsFileErrorKind.InvalidJson);
    }
}
