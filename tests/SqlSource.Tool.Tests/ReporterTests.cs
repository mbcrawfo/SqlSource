using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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

    [Fact]
    public void Report_Error_IsWrittenWithOneCallToTheWriter()
    {
        // Several threads report.  An error that is several writes can have the lines of another between its own.
        using var writer = new RecordingWriter();
        var reporter = new Reporter(writer);

        reporter.Report(Failure.WithLines(new ContinuationLine("help", "try again")));

        writer.Writes.ShouldBe(["sqlsource : " + Message + "    help: try again\n" + See]);
    }

    [Fact]
    public void Report_FromManyThreads_KeepsEveryErrorWholeAndCountsIt()
    {
        using var writer = new RecordingWriter();
        var reporter = new Reporter(writer);

        _ = Parallel.For(0, 2000, _ => reporter.Report(Failure));

        reporter.Count.ShouldBe(2000);
        writer.Writes.Count.ShouldBe(2000);
        writer.Writes.ShouldAllBe(write => write == "sqlsource : " + Message + See);
    }

    [Fact]
    public void Report_PathAndLabelWithLineBreaks_StayOnTheirLines()
    {
        var reporter = new Reporter(_error);

        reporter.Report(
            (Failure with { Path = "/work/odd\nname/App.csproj" }).WithLines(new ContinuationLine("ser\nver", "text"))
        );

        _error.ToString().ShouldBe("/work/odd name/App.csproj : " + Message + "    ser ver: text\n" + See);
    }

    // Keeps each call of Write apart.  It is not safe for several threads, as a console's writer need not be.
    private sealed class RecordingWriter : TextWriter
    {
        public RecordingWriter()
        {
            NewLine = "\n";
        }

        public List<string> Writes { get; } = [];

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => Writes.Add(value.ToString());

        public override void Write(string? value) => Writes.Add(value ?? "");
    }
}
