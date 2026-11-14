using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using SqlSource.Tool;
using SqlSource.Tool.Describing;
using Xunit;

namespace SqlSource.Repository.Tests.Documentation;

// site/reference/cli.md is where a user looks up the tool's command line.  These tests fail when the tool gains a
// command that is not hidden and the synopsis of #command-reference has no line for it, an option that is not hidden,
// of the root or of a command, and no table under #options has a row for it, or a SQLSOURCE_ variable that its code
// reads and the table under #environment-variables has no row for.  They check neither arguments, exit codes nor the
// words of a row.  A finder that finds nothing fails too, so that a change to how the tool is read cannot pass them
// all by finding no command, no option and no variable.
public sealed partial class CliReferenceTests
{
    private const string Page = "site/reference/cli.md";

    [Fact]
    public void Commands_EachVisibleOne_HasASynopsisLine()
    {
        var commands = Cli.BuildCommands().Subcommands.Where(static command => !command.Hidden).ToList();
        commands.ShouldNotBeEmpty();
        var synopsis = Synopsis(PageLines());

        var missing = commands
            .Select(static command => command.Name)
            .Where(name =>
                !synopsis.Any(line => line.StartsWith($"dotnet sqlsource {name} ", StringComparison.Ordinal))
            )
            .ToList();

        missing.ShouldBeEmpty();
    }

    [Fact]
    public void Options_EachVisibleOne_HasARow()
    {
        var root = Cli.BuildCommands();
        var options = root
            .Options.Concat(root.Subcommands.SelectMany(static command => command.Options))
            .Where(static option => !option.Hidden)
            .Select(static option => option.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        options.ShouldNotBeEmpty();
        var cells = FirstCells(Section(PageLines(), "options"));

        var missing = options
            .Where(name =>
                !cells.Any(cell =>
                    cell.StartsWith($"`{name}`", StringComparison.Ordinal)
                    || cell.StartsWith($"`{name} ", StringComparison.Ordinal)
                )
            )
            .ToList();

        missing.ShouldBeEmpty();
    }

    [Fact]
    public void Variables_EachOne_HasARow()
    {
        var variables = ToolVariables();
        variables.ShouldNotBeEmpty();
        variables.Add(ConnectionVariables.NamedPrefix + "<NAME>");
        var cells = FirstCells(Section(PageLines(), "environment-variables"));

        var missing = variables.Where(name => !cells.Contains($"`{name}`")).ToList();

        missing.ShouldBeEmpty();
    }

    private static string[] PageLines() => File.ReadAllLines(RepositoryRoot.Path(Page));

    // Every string literal "SQLSOURCE_..." of the tool's C# files, outside bin and obj.
    private static List<string> ToolVariables()
    {
        var source = RepositoryRoot.Path("src", "SqlSource.Tool");
        var separator = Path.DirectorySeparatorChar;
        return
        [
            .. Directory
                .EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                {
                    var relative = separator + Path.GetRelativePath(source, path);
                    return !relative.Contains($"{separator}bin{separator}", StringComparison.Ordinal)
                        && !relative.Contains($"{separator}obj{separator}", StringComparison.Ordinal);
                })
                .SelectMany(static path => VariableLiteral().Matches(File.ReadAllText(path)))
                .Select(static match => match.Groups["name"].Value)
                .Distinct(StringComparer.Ordinal),
        ];
    }

    // The lines of the section under the heading that follows <a id="anchor"></a>, down to the next heading.
    private static List<string> Section(string[] lines, string anchor)
    {
        var start = Array.IndexOf(lines, $"<a id=\"{anchor}\"></a>");
        start.ShouldBeGreaterThanOrEqualTo(0, $"{Page} has no anchor {anchor}");
        var heading = Array.FindIndex(lines, start, static line => line.StartsWith('#'));
        heading.ShouldBeGreaterThan(start, $"{Page} has no heading under the anchor {anchor}");

        var section = new List<string>();
        var inFence = false;
        foreach (var line in lines.Skip(heading + 1))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
            }
            else if (!inFence && line.StartsWith('#'))
            {
                break;
            }

            section.Add(line);
        }

        return section;
    }

    // The lines of the fenced text block of #command-reference.
    private static List<string> Synopsis(string[] lines)
    {
        var section = Section(lines, "command-reference");
        var open = section.IndexOf("```text");
        open.ShouldBeGreaterThanOrEqualTo(0, $"#command-reference of {Page} has no text block");
        var synopsis = section.Skip(open + 1).TakeWhile(static line => line != "```").ToList();
        synopsis.ShouldNotBeEmpty();
        return synopsis;
    }

    // The first cell of each row of the tables of a section, header rows and rule lines aside.
    private static List<string> FirstCells(List<string> section)
    {
        var cells = section
            .Select(static line => FirstCell().Match(line))
            .Where(static match => match.Success)
            .Select(static match => match.Groups["cell"].Value.Trim())
            .Where(static cell => cell.StartsWith('`'))
            .ToList();
        cells.ShouldNotBeEmpty();
        return cells;
    }

    [GeneratedRegex("\"(?<name>SQLSOURCE_[A-Z0-9_]+)\"")]
    private static partial Regex VariableLiteral();

    // A cell ends at the first pipe that no backslash escapes, as in `--online <auto\|true>`.
    [GeneratedRegex(@"^\|(?<cell>(?:\\\||[^|])*)\|")]
    private static partial Regex FirstCell();
}
