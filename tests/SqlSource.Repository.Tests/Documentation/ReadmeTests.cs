using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace SqlSource.Repository.Tests.Documentation;

// The two packed readmes are a brief introduction, and the site owns getting started.  A readme fails when it grows
// over its cap, in bytes as AgentsFileTests counts an AGENTS.md, and when it gives an install or update command that
// site/start/install.md does not have.  It holds a size and a set of commands, never words.
public sealed partial class ReadmeTests
{
    private const string InstallPage = "site/start/install.md";

    // The info strings, by their first word, of a fenced block of shell commands.
    private static readonly IReadOnlyList<string> ShellInfos = ["console", "bash", "sh", "shell"];

    // How an install or update command starts, after it is normalised.
    private static readonly IReadOnlyList<string> CommandStarts =
    [
        "dotnet add package ",
        "dotnet new tool-manifest",
        "dotnet tool install ",
        "dotnet tool update ",
        "dotnet tool restore",
    ];

    // Fails when a readme is over its cap.  The cure is to move the text to the site and link it, not to raise the cap.
    [Theory]
    [InlineData("README.md", 8000)]
    [InlineData("src/SqlSource.Tool/README.md", 4000)]
    public void Size_OfEachReadme_IsUnderItsCap(string readme, int cap) =>
        new FileInfo(RepositoryRoot.Path(readme)).Length.ShouldBeLessThanOrEqualTo(
            cap,
            $"{readme} is over its cap of {cap} bytes: move the text to the page of site/ that owns it, and link "
                + "that page"
        );

    // Fails when an install or update command of a readme is not one of site/start/install.md, so that a command
    // changes in the readmes and the page in one commit; or when a readme gives fewer commands than it must, so that a
    // reading that finds nothing cannot pass.
    [Theory]
    [InlineData("README.md", 3)]
    [InlineData("src/SqlSource.Tool/README.md", 2)]
    public void InstallCommands_OfEachReadme_AreOnTheInstallPage(string readme, int least)
    {
        var commands = CommandsOf(readme);
        commands.Count.ShouldBeGreaterThanOrEqualTo(
            least,
            $"{readme} should give at least {least} install or update commands; it gives {commands.Count}"
        );

        var page = CommandsOf(InstallPage).ToHashSet(StringComparer.Ordinal);
        commands
            .Where(command => !page.Contains(command))
            .Select(command => $"{readme}: '{command}' is not a command of {InstallPage}")
            .ShouldBeEmpty();
    }

    // Fails when Commands misses a command of a shell block or of a code span, does not normalise one, or keeps a line
    // that is no install or update command, or one of a block of another language.
    [Fact]
    public void Commands_OfMadeUpText_AreTheInstallAndUpdateCommands() =>
        Commands(
                [
                    ("console", "$ dotnet add package SqlSource\n$  dotnet  tool install   SqlSource.Tool\nAdded."),
                    ("csharp", "dotnet tool restore"),
                    ("bash title", "dotnet new tool-manifest"),
                ],
                ["dotnet add package Npgsql", "dotnet sqlsource describe", " dotnet tool update SqlSource.Tool "]
            )
            .ShouldBe([
                "dotnet add package SqlSource",
                "dotnet tool install SqlSource.Tool",
                "dotnet new tool-manifest",
                "dotnet add package Npgsql",
                "dotnet tool update SqlSource.Tool",
            ]);

    private static List<string> CommandsOf(string file) =>
        Commands(
            UserDocs.BlocksOf(file).Select(block => (block.Info, block.Text)),
            UserDocs.CodeSpans(file).Select(span => span.Text)
        );

    // The install and update commands of each line of a shell block and of each code span, normalised: a leading `$ `
    // taken off, the ends trimmed, and each run of spaces made one space.
    internal static List<string> Commands(IEnumerable<(string Info, string Text)> blocks, IEnumerable<string> spans) =>
        [
            .. blocks
                .Where(block => ShellInfos.Contains(block.Info.Split(' ', 2)[0], StringComparer.Ordinal))
                .SelectMany(block => block.Text.Split('\n'))
                .Concat(spans)
                .Select(Normalised)
                .Where(command => CommandStarts.Any(start => command.StartsWith(start, StringComparison.Ordinal))),
        ];

    private static string Normalised(string line)
    {
        var text = line.Trim();
        if (text.StartsWith("$ ", StringComparison.Ordinal))
        {
            text = text[2..];
        }

        return SpacesPattern().Replace(text.Trim(), " ");
    }

    [GeneratedRegex(" +")]
    private static partial Regex SpacesPattern();
}
