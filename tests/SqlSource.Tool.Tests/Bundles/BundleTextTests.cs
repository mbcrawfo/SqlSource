using Shouldly;
using SqlSource.Tool.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles;

public sealed class BundleTextTests
{
    // A control character of each kind, and how it is printed: C0's escape, bell and NUL, DEL, and C1's CSI and next
    // line, and the line breaks, which are C0's too.
    public static TheoryData<string, string> Controls =>
        new()
        {
            { "\u001b", "\\u001B" },
            { "\u0007", "\\u0007" },
            { "\u0000", "\\u0000" },
            { "\u007f", "\\u007F" },
            { "\u009b", "\\u009B" },
            { "\u0085", "\\u0085" },
            { "\n", "\\u000A" },
            { "\r", "\\u000D" },
        };

    [Theory]
    [MemberData(nameof(Controls))]
    public void Of_ControlCharacter_IsWrittenAsItsEscape(string control, string escape) =>
        BundleText.Of("a" + control + "b").ShouldBe("a" + escape + "b");

    [Theory]
    [MemberData(nameof(Controls))]
    public void Of_ControlCharacterBesideATab_KeepsTheTab(string control, string escape) =>
        BundleText.Of("a\t" + control + "\tb").ShouldBe("a\t" + escape + "\tb");

    [Fact]
    public void Of_TextWithoutAControlCharacter_IsTheSameText()
    {
        const string Text = "sqlsource 1.4.0 \u00e9\u4e2d";

        BundleText.Of(Text).ShouldBeSameAs(Text);
    }

    // A text that a JSON writer wrote holds its escapes as text, which is not escaped a second time.
    [Fact]
    public void Of_TextAJsonWriterEscaped_IsTheSameText() =>
        BundleText.Of("name=\"a\\u001Bb\\\\\"").ShouldBe("name=\"a\\u001Bb\\\\\"");

    [Fact]
    public void Of_SeveralControlCharacters_EscapesEachInPlace() =>
        BundleText.Of("\u001b[3A\u001b]0;x\u0007").ShouldBe("\\u001B[3A\\u001B]0;x\\u0007");
}
