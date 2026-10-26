using System;
using System.Linq;
using Microsoft.CodeAnalysis.Text;
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

    // The object is the first level, so 63 brackets under a key, or 62 inside an element of generatorParameters,
    // reach the limit exactly.  Pass one passes them, and the walk skips them and reads what comes after.
    [Fact]
    public void Read_ValueNestedToTheLimit_IsSkippedAndTheMembersAfterItAreRead()
    {
        var unknown = "{\"bogus\": " + Nested(63) + ", \"output\": \"models\"}";
        var element =
            "{\"generatorParameters\": [\"keep-comments\", "
            + Nested(62)
            + ", \"sort-input\"], \"output\": \"models\"}";

        var underUnknown = SettingsFileReader.Read(unknown, Place);
        var asElement = SettingsFileReader.Read(element, Place);

        underUnknown.Root.ShouldBe(false);
        underUnknown.Level.Output.ShouldBe(OutputKind.Models);
        underUnknown.Errors.ShouldHaveSingleItem().Kind.ShouldBe(SettingsFileErrorKind.UnknownKey);
        asElement.Root.ShouldBe(false);
        asElement.Level.Output.ShouldBe(OutputKind.Models);
        asElement.Level.Parameters.ShouldBe(GeneratorParameters.KeepComments | GeneratorParameters.SortInput);
        var wrongType = asElement.Errors.ShouldHaveSingleItem();
        wrongType.Kind.ShouldBe(SettingsFileErrorKind.WrongType);
        wrongType.Span.ShouldBe(new TextSpan(element.IndexOf("[[", StringComparison.Ordinal), 124));
    }

    private static string Nested(int depth) => new string('[', depth) + new string(']', depth);
}
