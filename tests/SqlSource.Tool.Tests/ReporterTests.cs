using System;
using System.Globalization;
using System.IO;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests;

public sealed class ReporterTests : IDisposable
{
    private const string Message = "error SQLSRC200: sqlsource failed unexpectedly: System.Exception: boom\n";

    private const string See =
        "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc200\n";

    private static readonly ToolDiagnostic Failure = ToolDiagnostic.Create(
        ToolDiagnostics.UnexpectedFailure,
        "System.Exception",
        "boom"
    );

    private readonly StringWriter _error = new(CultureInfo.InvariantCulture) { NewLine = "\n" };

    public void Dispose() => _error.Dispose();

    [Fact]
    public void Report_ErrorAboutNoFile_StartsWithTheNameOfTheTool()
    {
        var reporter = new Reporter(_error);

        reporter.Report(Failure);

        _error.ToString().ShouldBe("sqlsource : " + Message + See);
    }

    [Fact]
    public void Report_ErrorAboutAFile_StartsWithItsPath()
    {
        var reporter = new Reporter(_error);

        reporter.Report(Failure with { Path = "/work/App/App.csproj" });

        _error.ToString().ShouldBe("/work/App/App.csproj : " + Message + See);
    }

    [Fact]
    public void Report_ErrorAtAPosition_CountsTheLineAndTheColumnFromOne()
    {
        var reporter = new Reporter(_error);

        reporter.Report(Failure with { Path = "/work/App/Users.sql", Position = new LinePosition(2, 9) });

        _error.ToString().ShouldBe("/work/App/Users.sql(3,10): " + Message + See);
    }

    [Fact]
    public void Report_ContinuationLines_AreWrittenInOrderWithTheLinkLast()
    {
        var reporter = new Reporter(_error);

        reporter.Report(
            Failure.WithLines(
                new ContinuationLine("server", "ERROR: relation \"users\" does not exist"),
                new ContinuationLine("help", "create the table")
            )
        );

        _error
            .ToString()
            .ShouldBe(
                "sqlsource : "
                    + Message
                    + "    server: ERROR: relation \"users\" does not exist\n"
                    + "    help: create the table\n"
                    + See
            );
    }

    [Fact]
    public void Report_ArgumentWithBracesAndQuotes_IsWrittenAsItIs()
    {
        var reporter = new Reporter(_error);

        reporter.Report(
            ToolDiagnostic.Create(ToolDiagnostics.UnexpectedFailure, "System.Exception", "{0}'s \"x\" {{y}} {")
        );

        _error.ToString().ShouldStartWith("sqlsource : error SQLSRC200: ");
        _error.ToString().ShouldContain(": System.Exception: {0}'s \"x\" {{y}} {\n");
    }

    [Fact]
    public void Report_TextWithLineBreaks_StaysOnItsLine()
    {
        var reporter = new Reporter(_error);

        reporter.Report(
            ToolDiagnostic
                .Create(ToolDiagnostics.UnexpectedFailure, "System.Exception", "one\r\ntwo\nthree\rfour")
                .WithLines(new ContinuationLine("server", "first\nsecond"))
        );

        _error
            .ToString()
            .ShouldBe(
                "sqlsource : error SQLSRC200: sqlsource failed unexpectedly: System.Exception: one two three four\n"
                    + "    server: first second\n"
                    + See
            );
    }

    [Fact]
    public void Count_AfterTwoErrors_IsTwo()
    {
        var reporter = new Reporter(_error);
        reporter.Count.ShouldBe(0);

        reporter.Report(Failure);
        reporter.Report(Failure with { Path = "/work/App/App.csproj" });

        reporter.Count.ShouldBe(2);
    }
}
