using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using Xunit;

namespace SqlSource.Repository.Tests.Validation;

// The shell scripts run under Git Bash on Windows as well, where docker gets a container's path rewritten unless it is
// run through run_docker, and a Windows program cannot read a path of Git Bash's that is written into a file or
// inside an argument's value.  tools/platform.sh has both conversions.  Each case that fails is a pattern that broke a
// script on Windows.
public sealed class ScriptPortabilityTests
{
    // Folders that hold output, tooling state or another tool's files, and no script of the repository.
    private static readonly string[] SkippedFolders =
    [
        ".git",
        "bin",
        "obj",
        "TestResults",
        "artifacts",
        ".venv",
        ".superpowers",
        "node_modules",
    ];

    private const string AgentWorktrees = ".claude/worktrees";

    [Theory]
    [InlineData("docker run --rm -v \"$repo_root:/mnt\" img")]
    [InlineData("container=\"$(docker run -d img)\"")]
    [InlineData("if docker exec \"$c\" pg_isready; then")]
    [InlineData("    docker pull --quiet \"$image\" >/dev/null")]
    public void Violations_BareDocker_IsOne(string line) =>
        ScriptPortability.Violations("tools/x.sh", line).ShouldHaveSingleItem().ShouldContain("bare docker");

    [Theory]
    [InlineData("run_docker run --rm img")]
    [InlineData("# docker run in a comment")]
    [InlineData("echo 'needs docker running'")]
    public void Violations_NoDockerCommand_IsNone(string line) =>
        ScriptPortability.Violations("tools/x.sh", line).ShouldBeEmpty();

    [Theory]
    [InlineData("work=\"$(mktemp -d)\"\ncat >\"$c/nuget.config\" <<EOF\n<add key=\"k\" value=\"$work/feed\" />\nEOF")]
    [InlineData("work=\"$(mktemp -d)\"\ndotnet build \"-p:SqlSourceToolCommand=$work/stand-in.sh\"")]
    [InlineData("work=\"$(mktemp -d)\"\nfeed=\"$work/feed\"\ndotnet build -p:Feed=$feed")]
    [InlineData("work=\"$(mktemp -d)\"\nexport DOTNET_CLI_HOME=\"$work/home\"")]
    [InlineData(
        "repo_root=\"$(cd \"$(dirname \"$0\")\" && pwd)\"\nprintf '{\"solution\":{\"path\":%s}}' \"$repo_root/x.slnx\""
    )]
    [InlineData("repo_root=\"$(pwd)\"\nrun_docker run --volume \"$repo_root:/mnt\" img")]
    [InlineData(
        "work=\"$(mktemp -d)\"\n" + "printf '@\"%s\" \"%s\" %%*\\r\\n' \"$BASH\" \"$work/s.sh\" >\"$work/stand-in.cmd\""
    )]
    [InlineData("work=\"$(mktemp -d)\"\ndotnet build -p:Feed=\"$work/feed\"")]
    [InlineData(
        "work=\"$(mktemp -d)\"\nconfig=\"$work/nuget.config\"\n"
            + "cat >\"$config\" <<EOF\n<add value=\"$work/feed\" />\nEOF"
    )]
    [InlineData("work=\"$(mktemp -d)\"\necho \"{ \\\"path\\\": \\\"$work/x\\\" }\" >\"$work/a.json\"")]
    public void Violations_UnconvertedHostPath_IsOne(string script) =>
        ScriptPortability
            .Violations("tools/x.sh", script)
            .ShouldHaveSingleItem()
            .ShouldContain("unconverted host path");

    [Theory]
    [InlineData(
        "work=\"$(mktemp -d)\"\nwork_native=\"$(native_path \"$work\")\"\n"
            + "cat >\"$c/nuget.config\" <<EOF\n<add value=\"$work_native/feed\" />\nEOF"
    )]
    [InlineData("repo_root=\"$(pwd)\"\nrun_docker run --volume \"$(native_path \"$repo_root\"):/mnt\" img")]
    [InlineData("work=\"$(mktemp -d)\"\ncat >\"$work/App.csproj\" <<'EOF'\n<Import Project=\"$work/x\" />\nEOF")]
    [InlineData("work=\"$(mktemp -d)\"\ndotnet pack x.csproj --output \"$work/feed\"")]
    [InlineData("version=\"$(cat VERSION)\"\ndotnet add package X --version \"$version\" -p:V=$version")]
    [InlineData(
        "work=\"$(mktemp -d)\"\n" + "printf '@\"%s\" %%*\\r\\n' \"$(native_path \"$BASH\")\" >\"$work/stand-in.cmd\""
    )]
    [InlineData("work=\"$(mktemp -d)\"\necho \"$work\" >\"$work/log.txt\"")]
    public void Violations_ConvertedOrNoHostPath_IsNone(string script) =>
        ScriptPortability.Violations("tools/x.sh", script).ShouldBeEmpty();

    [Fact]
    public void Violations_TheHelperItself_IsNone() =>
        ScriptPortability.Violations("tools/platform.sh", "run_docker() {\n    docker \"$@\"\n}").ShouldBeEmpty();

    [Fact]
    public void Violations_EveryScriptOfTheRepository_IsNone()
    {
        var violations = ScriptsOfTheRepository()
            .SelectMany(path => ScriptPortability.Violations(path, File.ReadAllText(RepositoryRoot.Path(path))))
            .ToList();

        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<string> ScriptsOfTheRepository()
    {
        var root = new DirectoryInfo(RepositoryRoot.Path());
        var pending = new Stack<DirectoryInfo>([root]);
        while (pending.Count > 0)
        {
            var folder = pending.Pop();
            foreach (var file in folder.EnumerateFiles("*.sh"))
            {
                yield return RelativePath(root, file.FullName);
            }

            foreach (var child in folder.EnumerateDirectories())
            {
                if (
                    !SkippedFolders.Contains(child.Name, StringComparer.Ordinal)
                    && RelativePath(root, child.FullName) != AgentWorktrees
                )
                {
                    pending.Push(child);
                }
            }
        }
    }

    private static string RelativePath(DirectoryInfo root, string path) =>
        Path.GetRelativePath(root.FullName, path).Replace('\\', '/');
}
