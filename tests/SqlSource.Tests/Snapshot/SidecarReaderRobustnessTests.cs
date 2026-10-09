using System;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;
using static SqlSource.Tests.Snapshot.TestSidecars;

namespace SqlSource.Tests.Snapshot;

// A sidecar is a committed file that people merge, reformat and cut short.  Nothing that happens to one may make
// the reader throw: an exception in the generator costs every type its generated code.
public partial class SidecarReaderRobustnessTests
{
    // The usual way a sidecar becomes unreadable.
    [Fact]
    public void Read_FileWithAMergeConflict_IsInvalidJsonAtTheMarker()
    {
        const string Line = "  \"toolVersion\": \"0.4.0\",\n";
        var text = SidecarExamples
            .Read(SidecarExamples.Users)
            .Replace(
                Line,
                "<<<<<<< HEAD\n" + Line + "=======\n  \"toolVersion\": \"0.5.0\",\n>>>>>>> theirs\n",
                StringComparison.Ordinal
            );

        var result = SidecarReader.Read(text);

        result.Error.ShouldBe(
            new SidecarError(
                SidecarErrorKind.InvalidJson,
                new TextSpan(text.IndexOf("<<<<<<<", StringComparison.Ordinal), 1),
                null
            )
        );
    }

    [Theory]
    [InlineData(SidecarExamples.Users)]
    [InlineData(SidecarExamples.Orders)]
    public void Read_FileCutShortAnywhere_IsInvalidJson(string name)
    {
        var text = SidecarExamples.Read(name).TrimEnd();

        for (var length = 0; length < text.Length; length++)
        {
            var result = SidecarReader.Read(text[..length]);

            result.Error.ShouldNotBeNull().Kind.ShouldBe(SidecarErrorKind.InvalidJson, $"at length {length}");
        }
    }

    [Theory]
    [InlineData(SidecarExamples.Users)]
    [InlineData(SidecarExamples.Orders)]
    public void Read_FileDamagedAnywhere_ReturnsOneOfItsThreeStates(string name)
    {
        const string Replacements = "\"{}[]:,\\0x \n-.eu";
        var text = SidecarExamples.Read(name);

        for (var offset = 0; offset < text.Length; offset += 13)
        {
            ShouldBeOneState(SidecarReader.Read(text.Remove(offset, 1)));
            foreach (var replacement in Replacements)
            {
                var damaged = text[..offset] + replacement + text[(offset + 1)..];

                ShouldBeOneState(SidecarReader.Read(damaged));
            }
        }
    }

    // The examples are in the tool's order, where an entry's arrays are read where they stand.  This file is in the
    // opposite order, where they are passed over and read later, a path no file of the tool takes.
    [Fact]
    public void Read_ReorderedFileCutShortOrDamagedAnywhere_ReturnsOneOfItsThreeStates()
    {
        const string Replacements = "\"{}[]:,\\0x \n-.eu";
        var text = SidecarReaderTests.Reordered;
        _ = SidecarReader.Read(text).Sidecar.ShouldNotBeNull();

        for (var offset = 0; offset < text.Length; offset++)
        {
            SidecarReader
                .Read(text[..offset])
                .Error.ShouldNotBeNull()
                .Kind.ShouldBe(SidecarErrorKind.InvalidJson, $"at length {offset}");
            ShouldBeOneState(SidecarReader.Read(text.Remove(offset, 1)));
            foreach (var replacement in Replacements)
            {
                var damaged = text[..offset] + replacement + text[(offset + 1)..];

                ShouldBeOneState(SidecarReader.Read(damaged));
            }
        }
    }

    [Theory]
    [InlineData(SidecarExamples.Users)]
    [InlineData(SidecarExamples.Orders)]
    public void Read_FileAnEditorRewrote_ReadsTheSameModel(string name)
    {
        var text = SidecarExamples.Read(name);
        var expected = WithoutSpans(SidecarReader.Read(text).Sidecar.ShouldNotBeNull());

        Reread(text.Replace("\n", "\r\n", StringComparison.Ordinal)).ShouldBe(expected);
        Reread(Indentation().Replace(text, match => new string('\t', match.Length / 2))).ShouldBe(expected);
        Reread(LineBreaks().Replace(text, string.Empty)).ShouldBe(expected);
        Reread("\n\n" + text + "\n\n").ShouldBe(expected);
    }

    private static Sidecar Reread(string text) => WithoutSpans(SidecarReader.Read(text).Sidecar.ShouldNotBeNull());

    private static void ShouldBeOneState(SidecarReadResult result)
    {
        if (result.Error is not null)
        {
            result.Sidecar.ShouldBeNull();
            result.FormatVersion.ShouldBeNull();
            return;
        }

        _ = result.FormatVersion.ShouldNotBeNull();
        (result.Sidecar is not null).ShouldBe(result.FormatVersion == SidecarFormat.Version);
    }

    [GeneratedRegex("^ +", RegexOptions.Multiline)]
    private static partial Regex Indentation();

    [GeneratedRegex("\n *")]
    private static partial Regex LineBreaks();
}
