using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Testing;
using SqlSource.Tool.Reporting;
using SqlSource.Tool.Watching;
using Xunit;

namespace SqlSource.Tool.Tests.Watching;

// Holds the VS Code task of docs/manual/recipes.md#start-the-watcher-with-your-editor to what the tool writes.  A
// change to the watch's two markers, to the format of the tool's errors, or to the recipe without the other fails it:
// the task's background patterns must match its own marker alone, and its problem matcher the first line of an error
// alone, never a continuation line, a summary, a marker or an error about no file.
public sealed class EditorRecipeTests
{
    private const string PositionedPath = "/work/App/Queries/Users.sql";
    private const string UnpositionedPath = "/work/App/Queries/Orders.sql";

    [Fact]
    public void VsCodeTask_RunsTheWatch()
    {
        var task = VsCodeTask();

        task.GetProperty("command").GetString().ShouldBe("dotnet sqlsource describe --watch");
        task.GetProperty("isBackground").GetBoolean().ShouldBeTrue();
        task.GetProperty("runOptions").GetProperty("runOn").GetString().ShouldBe("folderOpen");
    }

    [Fact]
    public void VsCodeTask_BeginsPattern_MatchesTheChangeMarkerAlone()
    {
        var begins = Background("beginsPattern");

        Regex.IsMatch(WatchSession.ChangeDetectedMarker, begins).ShouldBeTrue();
        Regex.IsMatch(WatchSession.WatchingMarker, begins).ShouldBeFalse();
    }

    [Fact]
    public void VsCodeTask_EndsPattern_MatchesTheWatchingMarkerAlone()
    {
        var ends = Background("endsPattern");

        Regex.IsMatch(WatchSession.WatchingMarker, ends).ShouldBeTrue();
        Regex.IsMatch(WatchSession.ChangeDetectedMarker, ends).ShouldBeFalse();
    }

    [Fact]
    public void VsCodeTask_Pattern_ReadsTheFirstLineOfAnError()
    {
        var pattern = new Regex(ProblemPattern());

        var matches = ReportedLines().Select(line => pattern.Match(line)).Where(match => match.Success).ToList();

        // Each value is read from the group the recipe maps it to, so a recipe that maps a value to the wrong group
        // fails as a pattern that captures the wrong text does.
        matches.Count.ShouldBe(2);
        Captured(matches[0], "file").ShouldBe(PositionedPath);
        Captured(matches[0], "line").ShouldBe("4");
        Captured(matches[0], "column").ShouldBe("10");
        Captured(matches[0], "severity").ShouldBe("error");
        Captured(matches[0], "code").ShouldBe(ToolDiagnostics.ServerRejectedQuery.Id);
        Captured(matches[0], "message")
            .ShouldBe("The server rejected the query 'GetUser': relation \"user\" does not exist");
        Captured(matches[1], "file").ShouldBe(UnpositionedPath);
        Captured(matches[1], "line").ShouldBe("");
        Captured(matches[1], "column").ShouldBe("");
        Captured(matches[1], "code").ShouldBe(ToolDiagnostics.SidecarNotWritten.Id);
        Captured(matches[1], "message").ShouldStartWith($"The sidecar of '{UnpositionedPath}' was not written");
    }

    // The text of the group that the recipe's pattern names for a value, such as "line".
    private static string Captured(Match match, string value) =>
        match.Groups[ProblemPatternElement().GetProperty(value).GetInt32()].Value;

    [Fact]
    public void VsCodeTask_Pattern_PassesOverEveryOtherLine()
    {
        var pattern = new Regex(ProblemPattern());
        string[] others =
        [
            ReportedLines().First(line => line.StartsWith("    server:", StringComparison.Ordinal)),
            ReportedLines().First(line => line.StartsWith("    see: https://", StringComparison.Ordinal)),
            ReportedLines()
                .First(line => line.Contains(ToolDiagnostics.UnexpectedFailure.Id, StringComparison.Ordinal)),
            WatchSession.ChangeDetectedMarker,
            WatchSession.WatchingMarker,
            "postgres (postgres): 1 described, 0 skipped, 0 failed",
        ];

        foreach (var line in others)
        {
            pattern.IsMatch(line).ShouldBeFalse(line);
        }
    }

    // Three errors as a watch's pass writes them: one at a place in a file, with lines under it, one at a file, and
    // one about no file.
    private static string[] ReportedLines()
    {
        using var error = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
        var reporter = new Reporter(error);

        reporter.Report(
            ToolDiagnostic
                .At(
                    ToolDiagnostics.ServerRejectedQuery,
                    PositionedPath,
                    new LinePosition(3, 9),
                    "GetUser",
                    "relation \"user\" does not exist"
                )
                .WithLines(
                    new ContinuationLine("server", "ERROR: 42P01: relation \"user\" does not exist"),
                    new ContinuationLine("query", "GetUser, database postgres")
                )
        );
        reporter.Report(ToolDiagnostic.ForFile(ToolDiagnostics.SidecarNotWritten, UnpositionedPath));
        reporter.Report(ToolDiagnostic.Create(ToolDiagnostics.UnexpectedFailure, "System.Exception", "boom"));

        return error.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string ProblemPattern() => ProblemPatternElement().GetProperty("regexp").GetString()!;

    private static JsonElement ProblemPatternElement() =>
        VsCodeTask().GetProperty("problemMatcher").GetProperty("pattern");

    private static string Background(string name) =>
        VsCodeTask().GetProperty("problemMatcher").GetProperty("background").GetProperty(name).GetString()!;

    // The one jsonc block of the recipes that holds a background pattern: the VS Code task.
    private static JsonElement VsCodeTask()
    {
        var lines = File.ReadAllLines(RepositoryRoot.Path("docs", "manual", "recipes.md"));
        var blocks = lines
            .Select((line, index) => (line, index))
            .Where(static entry => entry.line == "```jsonc")
            .Select(entry => string.Join('\n', lines.Skip(entry.index + 1).TakeWhile(static line => line != "```")))
            .Where(static block => block.Contains("\"beginsPattern\"", StringComparison.Ordinal))
            .ToList();
        var block = blocks.ShouldHaveSingleItem();

        using var document = JsonDocument.Parse(
            block,
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }
        );
        return document.RootElement.GetProperty("tasks")[0].Clone();
    }
}
