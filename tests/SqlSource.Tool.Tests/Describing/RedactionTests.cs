using System.Globalization;
using System.IO;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests.Describing;

// Redaction removes the value of a connection, and each part of it, from a text, a failure and a diagnostic, however a
// line break or a format splits it.
public sealed class RedactionTests
{
    [Fact]
    public void Of_TextThatHoldsTheSecretTwice_HasTheMarkInBothPlaces() =>
        Redaction.Of("a s3cret and a s3cret", new ConnectionSecret("s3cret")).ShouldBe("a *** and a ***");

    // The edges of the rule, which holds for a part of any length: the text's own ends count as no letter, and an
    // edge of the part that is no letter, digit or "_" is not checked.
    [Theory]
    [InlineData("pw", "pw", true)]
    [InlineData("pw x", "pw", true)]
    [InlineData("x pw", "pw", true)]
    [InlineData("x!pw", "!pw", true)]
    [InlineData("aaa", "aa", false)]
    public void HoldsToken_PartAtAnEdge_IsWhetherItStandsAsAToken(string text, string part, bool expected) =>
        Redaction.HoldsToken(text, part).ShouldBe(expected);

    // A part that holds another as a token is replaced first.
    [Fact]
    public void Of_PartThatHoldsAShorterOneAsAToken_IsReplacedWhole() =>
        Redaction.Of("pass-word", new ConnectionSecret("Host=db", ["pass", "pass-word"])).ShouldBe("***");

    [Fact]
    public void Of_TwoPartsOneInsideTheOther_AreEachTheMark() =>
        Redaction.Of("hunter2 hunter", new ConnectionSecret("Host=db", ["hunter", "hunter2"])).ShouldBe("*** ***");

    [Fact]
    public void Of_FailureWhoseServerLinesHoldAPart_HasTheMarkThereAndTheWordInsideANameLeft()
    {
        var secret = new ConnectionSecret("Host=db;Password=test", ["test"]);
        var failure = FakeDescriber.Failure("GetUser", "28P01: password test refused", "at latest_orders");

        Redaction.Of(failure, secret).ServerLines.ShouldBe(["28P01: password *** refused", "at latest_orders"]);
    }

    // A describer cuts what a server said into lines, so a part with a line break is in two of them.
    [Fact]
    public void Of_FailureWithAPartOfTwoLinesCutOverTwo_HasTheMarkOnce()
    {
        var secret = new ConnectionSecret("Host=db;Password=a\r\nb", ["a\r\nb"]);
        var failure = FakeDescriber.Failure("GetUser", "x a", "b y");

        Redaction.Of(failure, secret).ServerLines.ShouldBe(["x *** y"]);
    }

    // A describer may write the value again as its driver does, which is not the text of the value.
    [Fact]
    public void Of_RewrittenValueInAMessage_IsTheMark() =>
        Redaction
            .Of(
                "bad 'Host=db;Port=5432;Password=hunter22'",
                new ConnectionSecret(
                    "host=db;password=hunter22",
                    ["hunter22"],
                    forms: ["Host=db;Port=5432;Password=hunter22"]
                )
            )
            .ShouldBe("bad '***'");

    [Fact]
    public void Of_ValueTrimmedInAMessage_IsTheMark() =>
        Redaction
            .Of("cannot use Host=db;Database=app", new ConnectionSecret("Host=db;Database=app \n"))
            .ShouldBe("cannot use ***");

    // A value that was in two lines leaves one, as it does for a part.
    [Fact]
    public void Of_FailureWhoseRewriteIsCutOverTwoServerLines_IsTheMark()
    {
        var secret = new ConnectionSecret(
            "host=db;password=hunter22",
            forms: ["Host=db;Port=5432\r\nPassword=hunter22"]
        );
        var failure = FakeDescriber.Failure("GetUser", "a Host=db;Port=5432", "Password=hunter22 b");

        Redaction.Of(failure, secret).ServerLines.ShouldBe(["a *** b"]);
    }

    // A descriptor of the fake's id with the format a describer built.  The id is no constant, so release tracking
    // has no row for it.
    private static DiagnosticDescriptor WithFormat(string format) =>
        new(
            FakeDescriber.Rejected.Id,
            "Query was rejected",
            format,
            "SqlSource",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: "https://example.test/sqlsrc999"
        );

    private static string Printed(ToolDiagnostic diagnostic)
    {
        using var error = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
        new Reporter(error).Report(diagnostic);
        return error.ToString();
    }

    // Neither the format nor the argument holds the value: together they do.
    [Fact]
    public void Of_ValueThatTheFormatAndAnArgumentMakeTogether_IsRemovedFromTheMessage()
    {
        var diagnostic = ToolDiagnostic.Create(WithFormat("half: Pass{0}"), "word=x");

        var printed = Printed(Redaction.Of(diagnostic, new ConnectionSecret("Password=x")));

        printed.ShouldContain("half: ***\n");
        printed.ShouldNotContain("Password=x");
    }

    [Fact]
    public void Of_ValueThatTwoArgumentsMakeTogether_IsRemovedFromTheMessage()
    {
        var diagnostic = ToolDiagnostic.Create(WithFormat("rejected {0}{1} today"), "Pass", "word=x");

        var printed = Printed(Redaction.Of(diagnostic, new ConnectionSecret("Password=x")));

        printed.ShouldContain("rejected *** today\n");
        printed.ShouldNotContain("Password=x");
    }

    // Reporter writes a line break as a space, which can make a password that holds a space.
    [Fact]
    public void Of_PasswordWithASpaceSplitByALineBreak_IsTheMarkInTheArgumentAndTheLine()
    {
        var diagnostic = ToolDiagnostic
            .Create(WithFormat("rejected {0}"), "my\npass")
            .WithLines(new ContinuationLine("server", "said my\r\npass"));

        var printed = Printed(Redaction.Of(diagnostic, new ConnectionSecret("Host=db;Password=my pass", ["my pass"])));

        printed.ShouldContain("rejected ***\n");
        printed.ShouldContain("    server: said ***\n");
        printed.ShouldNotContain("my pass");
    }

    // A message that holds the value is printed as the one argument of its error, on one line, so a line break of
    // the format can make a password that holds a space there too.
    [Fact]
    public void Of_MessageThatHoldsTheValueAndAPasswordSplitByALineBreakOfTheFormat_IsTheMarkInBoth()
    {
        var diagnostic = ToolDiagnostic.Create(
            WithFormat("rejected {0}{1}, said my\npass"),
            "Host=db;Password=my",
            " pass"
        );

        var printed = Printed(Redaction.Of(diagnostic, new ConnectionSecret("Host=db;Password=my pass", ["my pass"])));

        printed.ShouldContain("rejected ***, said ***\n");
        printed.ShouldNotContain("my pass");
    }

    // A format that a describer built holds a password with a space, cut by a line break, and nothing of the whole
    // value: the message is searched on one line too, and printed as its one argument.
    [Fact]
    public void Of_FormatThatHoldsAPasswordSplitByALineBreak_IsTheMarkInTheMessage()
    {
        var diagnostic = ToolDiagnostic.Create(WithFormat("rejected {0}, said my\npass"), "x");

        var printed = Printed(Redaction.Of(diagnostic, new ConnectionSecret("Host=db;Password=my pass", ["my pass"])));

        printed.ShouldStartWith("sqlsource : error SQLSRC999: rejected x, said ***\n");
        printed.ShouldNotContain("pass");
    }

    // The descriptor of a failure is searched the same way, before an error is made of it.
    [Fact]
    public void Of_FailureWhoseFormatHoldsAPasswordSplitByALineBreak_HasTheMarkInTheFormat()
    {
        var failure = FakeDescriber.Failure("GetUser") with { Descriptor = WithFormat("rejected {0}, said my\npass") };

        Redaction
            .Of(failure, new ConnectionSecret("Host=db;Password=my pass", ["my pass"]))
            .Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture)
            .ShouldBe("rejected {0}, said ***");
    }

    // The label of a line is printed on one line too.
    [Fact]
    public void Of_LineWhoseLabelHoldsAPasswordSplitByALineBreak_IsTheMarkInTheLabel()
    {
        var diagnostic = ToolDiagnostic
            .Create(WithFormat("rejected {0}"), "x")
            .WithLines(new ContinuationLine("said my\npass", "x"));

        var printed = Printed(Redaction.Of(diagnostic, new ConnectionSecret("Host=db;Password=my pass", ["my pass"])));

        printed.ShouldContain("    said ***: x\n");
        printed.ShouldNotContain("my pass");
    }
}
