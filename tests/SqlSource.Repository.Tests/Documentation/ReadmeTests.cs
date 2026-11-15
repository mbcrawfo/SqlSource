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

    // The info strings, by their first word, of a fenced block that can hold commands: a shell's, a console's, plain
    // text, or none.
    private static readonly IReadOnlyList<string> ShellInfos =
    [
        "",
        "console",
        "bash",
        "sh",
        "shell",
        "zsh",
        "text",
        "powershell",
        "pwsh",
        "ps1",
        "cmd",
        "bat",
    ];

    // The prompts a command line of a block can start with.
    private static readonly IReadOnlyList<string> Prompts = ["$ ", "PS> ", "> "];

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
                    ("", "$ dotnet tool install SqlSource.Tool --prerelease"),
                    ("powershell", "PS> dotnet tool update --global SqlSource.Tool"),
                    ("cmd", "> dotnet tool restore"),
                    ("jsonc", "// dotnet add package SqlSource"),
                ],
                [
                    "dotnet add package Npgsql",
                    "dotnet sqlsource describe",
                    " dotnet tool update SqlSource.Tool ",
                    "dotnet package add SqlSource",
                    "dotnet add App.csproj package SqlSource",
                    "dotnet tool installer",
                ]
            )
            .ShouldBe([
                "dotnet add package SqlSource",
                "dotnet tool install SqlSource.Tool",
                "dotnet new tool-manifest",
                "dotnet tool install SqlSource.Tool --prerelease",
                "dotnet tool update --global SqlSource.Tool",
                "dotnet tool restore",
                "dotnet add package Npgsql",
                "dotnet tool update SqlSource.Tool",
                "dotnet package add SqlSource",
                "dotnet add App.csproj package SqlSource",
            ]);

    // Fails when a block of one of these info strings, a shell's, a console's, plain text or none, is not read.  The
    // rows are written out, so that an info string taken off ShellInfos fails here.
    [Theory]
    [InlineData("")]
    [InlineData("console")]
    [InlineData("bash")]
    [InlineData("sh")]
    [InlineData("shell")]
    [InlineData("zsh")]
    [InlineData("text")]
    [InlineData("powershell")]
    [InlineData("pwsh")]
    [InlineData("ps1")]
    [InlineData("cmd")]
    [InlineData("bat")]
    public void Commands_OfABlockOfACommandLanguage_AreRead(string info) =>
        Commands([(info, "dotnet tool restore")], []).ShouldBe(["dotnet tool restore"]);

    private static List<string> CommandsOf(string file) =>
        Commands(
            UserDocs.BlocksOf(file).Select(block => (block.Info, block.Text)),
            UserDocs.CodeSpans(file).Select(span => span.Text)
        );

    // The install and update commands of each line of a block of ShellInfos and of each code span, normalised: a
    // leading prompt taken off, the ends trimmed, and each run of spaces made one space.
    internal static List<string> Commands(IEnumerable<(string Info, string Text)> blocks, IEnumerable<string> spans) =>
        [
            .. blocks
                .Where(block => ShellInfos.Contains(block.Info.Split(' ', 2)[0], StringComparer.Ordinal))
                .SelectMany(block => block.Text.Split('\n'))
                .Concat(spans)
                .Select(Normalised)
                .Where(command => InstallCommandPattern().IsMatch(command)),
        ];

    private static string Normalised(string line)
    {
        var text = line.Trim();
        var prompt = Prompts.FirstOrDefault(start => text.StartsWith(start, StringComparison.Ordinal));
        if (prompt is not null)
        {
            text = text[prompt.Length..];
        }

        return SpacesPattern().Replace(text.Trim(), " ");
    }

    // A command that adds a package, in either order of the SDK's words and with or without a project, or that makes a
    // tool manifest, or installs, updates or restores a tool.
    [GeneratedRegex(
        @"^dotnet (?:add(?: \S+)? package|package add|new tool-manifest|tool (?:install|update|restore))(?: |$)"
    )]
    private static partial Regex InstallCommandPattern();

    [GeneratedRegex(" +")]
    private static partial Regex SpacesPattern();
}
