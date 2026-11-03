using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Reporting;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Reporting;

public sealed class ReporterTests : IDisposable
{
    private const string Message = "error SQLSRC200: sqlsource failed unexpectedly: System.Exception: boom\n";

    private static readonly ToolDiagnostic Failure = ToolDiagnostic.Create(
        ToolDiagnostics.UnexpectedFailure,
        "System.Exception",
        "boom"
    );

    private readonly StringWriter _error = new(CultureInfo.InvariantCulture) { NewLine = "\n" };

    public void Dispose() => _error.Dispose();

    [Fact]
    public void Report_ReporterMadeWithATrace_WritesTheErrorAsAnEventOfItsLog()
    {
        var log = new RecordingLog();
        var reporter = new Reporter(_error, new RunTrace(log));

        reporter.Report(Failure);

        log.Events.ShouldHaveSingleItem().Name.ShouldBe("diagnostic");
        reporter.Trace.Log.ShouldBeSameAs(log);
    }

    [Fact]
    public void ReportWithoutWriting_Error_WritesNothingAndIsCountedAndLogged()
    {
        var log = new RecordingLog();
        var reporter = new Reporter(_error, new RunTrace(log));

        reporter.ReportWithoutWriting(Failure);

        _error.ToString().ShouldBe("");
        reporter.Count.ShouldBe(1);
        reporter.UnwrittenCount.ShouldBe(1);
        log.Events.ShouldHaveSingleItem().Name.ShouldBe("diagnostic");
    }

    [Fact]
    public void Report_AfterAnErrorReportedWithoutWriting_CountsOnlyTheWrittenOneAsWritten()
    {
        var reporter = new Reporter(_error);
        reporter.ReportWithoutWriting(Failure);

        reporter.Report(Failure);

        reporter.Count.ShouldBe(2);
        reporter.UnwrittenCount.ShouldBe(1);
        _error.ToString().ShouldBe("sqlsource : " + Message + Expected.See("SQLSRC200"));
    }

    [Fact]
    public void ExitCodeOf_OnlyErrorsReportedWithoutWriting_IsZero()
    {
        var reporter = new Reporter(_error);
        reporter.ReportWithoutWriting(Failure);

        Cli.ExitCodeOf(reporter, 0).ShouldBe(0);
    }

    [Fact]
    public void ExitCodeOf_AWrittenErrorBesideOneReportedWithoutWriting_IsOne()
    {
        var reporter = new Reporter(_error);
        reporter.ReportWithoutWriting(Failure);
        reporter.Report(Failure);

        Cli.ExitCodeOf(reporter, 0).ShouldBe(1);
    }

    [Fact]
    public void Report_ErrorAboutNoFile_StartsWithTheNameOfTheTool()
    {
        var reporter = new Reporter(_error);

        reporter.Report(Failure);

        _error.ToString().ShouldBe("sqlsource : " + Message + Expected.See("SQLSRC200"));
    }

    [Fact]
    public void Report_ErrorAboutAFile_StartsWithItsPath()
    {
        var reporter = new Reporter(_error);

        reporter.Report(Failure with { Path = "/work/App/App.csproj" });

        _error.ToString().ShouldBe("/work/App/App.csproj : " + Message + Expected.See("SQLSRC200"));
    }

    [Fact]
    public void Report_ErrorAtAPosition_CountsTheLineAndTheColumnFromOne()
    {
        var reporter = new Reporter(_error);

        reporter.Report(Failure with { Path = "/work/App/Users.sql", Position = new LinePosition(2, 9) });

        _error.ToString().ShouldBe("/work/App/Users.sql(3,10): " + Message + Expected.See("SQLSRC200"));
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
                    + Expected.See("SQLSRC200")
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
                    + Expected.See("SQLSRC200")
            );
    }

    // A build reads the tool's output a line at a time and takes each line with "error" in it for an error.
    [Fact]
    public void Report_OneLine_JoinsTheContinuationLinesAndTheLinkToTheFirst()
    {
        var reporter = new Reporter(_error, oneLine: true);

        reporter.Report(
            (Failure with { Path = "/work/App/Users.sql", Position = new LinePosition(2, 9) }).WithLines(
                new ContinuationLine("help", "create the table"),
                new ContinuationLine("query", "GetUser, database app")
            )
        );

        _error
            .ToString()
            .ShouldBe(
                "/work/App/Users.sql(3,10): "
                    + Message.TrimEnd('\n')
                    + "; help: create the table; query: GetUser, database app; see: "
                    + Expected.See("SQLSRC200").Replace("    see: ", "", StringComparison.Ordinal)
            );
    }

    [Fact]
    public void Report_OneLineWithNoContinuationLines_IsTheFirstLineAndTheLink()
    {
        var reporter = new Reporter(_error, oneLine: true);

        reporter.Report(Failure);

        _error
            .ToString()
            .ShouldBe(
                "sqlsource : "
                    + Message.TrimEnd('\n')
                    + "; see: "
                    + Expected.See("SQLSRC200").Replace("    see: ", "", StringComparison.Ordinal)
            );
        reporter.Count.ShouldBe(1);
    }

    [Fact]
    public void Report_OneLineWithLineBreaksInTheText_IsOneLine()
    {
        var reporter = new Reporter(_error, oneLine: true);

        reporter.Report(
            (Failure with { Path = "/work/odd\nname/App.csproj" }).WithLines(
                new ContinuationLine("ser\nver", "ERROR: first\r\nDETAIL: second\nthird")
            )
        );

        var text = _error.ToString();
        text.IndexOf('\n', StringComparison.Ordinal).ShouldBe(text.Length - 1);
        text.ShouldContain("; ser ver: ERROR: first DETAIL: second third; see: ");
        text.ShouldStartWith("/work/odd name/App.csproj : error SQLSRC200");
    }

    [Fact]
    public void Report_OneLine_IsWrittenWithOneCallToTheWriter()
    {
        using var writer = new RecordingWriter();
        var reporter = new Reporter(writer, oneLine: true);

        reporter.Report(Failure.WithLines(new ContinuationLine("help", "try again")));

        writer.Writes.Count.ShouldBe(1);
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

        writer.Writes.ShouldBe(["sqlsource : " + Message + "    help: try again\n" + Expected.See("SQLSRC200")]);
    }

    [Fact]
    public void Report_FromManyThreads_KeepsEveryErrorWholeAndCountsIt()
    {
        using var writer = new RecordingWriter();
        var reporter = new Reporter(writer);

        _ = Parallel.For(0, 2000, _ => reporter.Report(Failure));

        reporter.Count.ShouldBe(2000);
        writer.Writes.Count.ShouldBe(2000);
        writer.Writes.ShouldAllBe(write => write == "sqlsource : " + Message + Expected.See("SQLSRC200"));
    }

    [Fact]
    public void Report_PathAndLabelWithLineBreaks_StayOnTheirLines()
    {
        var reporter = new Reporter(_error);

        reporter.Report(
            (Failure with { Path = "/work/odd\nname/App.csproj" }).WithLines(new ContinuationLine("ser\nver", "text"))
        );

        _error
            .ToString()
            .ShouldBe("/work/odd name/App.csproj : " + Message + "    ser ver: text\n" + Expected.See("SQLSRC200"));
    }

    // The exit code 2 is for a run that reported nothing but SQLSRC219.
    [Fact]
    public void Report_Sqlsrc219_IsCountedAsAnErrorAndAsADifference()
    {
        var reporter = new Reporter(_error);

        reporter.Report(Failure);
        reporter.Report(ToolDiagnostic.Create(ToolDiagnostics.SidecarOutOfDate, "GetUser", "the sidecar has no entry"));

        reporter.Count.ShouldBe(2);
        reporter.DifferenceCount.ShouldBe(1);
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
